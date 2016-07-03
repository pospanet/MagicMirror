using MirrorManager.UWP.DAO;
using MirrorManager.UWP.Helpers;
using MirrorManager.UWP.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MirrorManager.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainPageViewModel viewModel;

        private MediaCapture mediaCapture;
        private bool isInitialized;
        private bool isPreviewing;

        private bool externalCamera;
        private bool mirroringPreview;

        private readonly DisplayRequest displayRequest = new DisplayRequest();

        private readonly DisplayInformation displayInformation = DisplayInformation.GetForCurrentView();
        private DisplayOrientations displayOrientation = DisplayOrientations.Portrait;

        private readonly SimpleOrientationSensor orientationSensor = SimpleOrientationSensor.GetDefault();
        private SimpleOrientation deviceOrientation = SimpleOrientation.NotRotated;

        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        private VideoEncodingProperties videoProperties;
        private FaceTracker faceTracker;
        private ThreadPoolTimer frameProcessingTimer;
        private SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1); // face tracking only one at a time

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            RegisterOrientationEventHandlers();

            var token = App.Settings.Values["Token"];

            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            var result = await hc.GetAsync("https://graph.microsoft.com/v1.0/me");

            if (result.IsSuccessStatusCode)
            {
                string res = await result.Content.ReadAsStringAsync();
                UserObject user = JsonConvert.DeserializeObject<UserObject>(res);

                ((MainPageViewModel)DataContext).UserName = user.displayName;
            }

            await InitializeCameraAsync();

            if (faceTracker == null)
            {
                faceTracker = await FaceTracker.CreateAsync();
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            UnregisterOrientationEventHandlers();

            await CleanupCameraAsync();

            if (frameProcessingTimer != null)
            {
                frameProcessingTimer.Cancel();
                frameProcessingTimer = null;
            }
        }

        private void RegisterOrientationEventHandlers()
        {
            if (orientationSensor != null)
            {
                orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
                deviceOrientation = orientationSensor.GetCurrentOrientation();
            }

            displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
            displayOrientation = displayInformation.CurrentOrientation;
        }

        private void UnregisterOrientationEventHandlers()
        {
            if (orientationSensor != null)
            {
                orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
        }

        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            displayOrientation = sender.CurrentOrientation;

            if (isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        private void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                deviceOrientation = args.Orientation;
            }
        }

        private async void TakePicture_Click(object sender, RoutedEventArgs e)
        {
            var stream = new InMemoryRandomAccessStream();

            try
            {
                Debug.WriteLine("Taking photo...");
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                Debug.WriteLine("Photo taken!");

                var photoOrientation = ConvertOrientationToPhotoOrientation(GetCameraOrientation());

                var image = new BitmapImage();
                stream.Seek(0);
                await image.SetSourceAsync(stream);
                photo.Source = image;

                await FaceApiHelper.AddPersonFaceAsync("", "", stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when taking a photo: {0}", ex.ToString());
            }
            finally
            {
                stream.Dispose();
            }

        }

        private async Task InitializeCameraAsync()
        {
            if (mediaCapture == null)
            {
                var videoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                DeviceInformation cameraDevice = videoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back);
                cameraDevice = cameraDevice ?? videoDevices.FirstOrDefault();

                if (cameraDevice == null)
                {
                    // TODO: camera not found
                    return;
                }

                mediaCapture = new MediaCapture();
                var mediaInitSettings = new MediaCaptureInitializationSettings() { VideoDeviceId = cameraDevice.Id };

                try
                {
                    await mediaCapture.InitializeAsync(mediaInitSettings);
                    isInitialized = true;

                    var deviceController = this.mediaCapture.VideoDeviceController;
                    videoProperties = deviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                }
                catch (UnauthorizedAccessException)
                {
                    //TODO: denied access to camera
                }
                catch (Exception ex)
                {
                    //TODO: exception when initializing...
                }

                if (isInitialized)
                {
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        externalCamera = true; // assuming
                    }
                    else
                    {
                        externalCamera = false;
                        mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front); // front camera needs mirroring
                    }

                    await StartPreviewAsync();

                    TimeSpan timerInterval = TimeSpan.FromMilliseconds(66);
                    frameProcessingTimer = ThreadPoolTimer.CreatePeriodicTimer(new TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);
                }
            }
        }

        private async Task CleanupCameraAsync()
        {
            if (isInitialized)
            {
                if (isPreviewing)
                {
                    await StopPreviewAsync();
                }

                isInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.Dispose();
                mediaCapture = null;
            }

        }

        private async Task StartPreviewAsync()
        {
            displayRequest.RequestActive();

            photoPreview.Source = mediaCapture;
            photoPreview.FlowDirection = FlowDirection.LeftToRight; // podle zvolené kamery

            try
            {
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (Exception ex)
            {
                // error when starting the preview
            }

            if (isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        private async Task StopPreviewAsync()
        {
            try
            {
                await mediaCapture.StopPreviewAsync();
                isPreviewing = false;
            }
            catch (Exception ex)
            {
                // error when stopping preview
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                photoPreview.Source = null;
                displayRequest.RequestRelease();
            });
        }

        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (externalCamera) return;

            //Populate orientation variables with the current state
           displayOrientation = displayInformation.CurrentOrientation;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(displayOrientation);

            //The rotation direction needs to be inverted if the preview is being mirrored
            if (mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);
            await mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);

        }

        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        private PhotoOrientation ConvertOrientationToPhotoOrientation(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return PhotoOrientation.Rotate90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return PhotoOrientation.Rotate180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return PhotoOrientation.Rotate270;
                case SimpleOrientation.NotRotated:
                default:
                    return PhotoOrientation.Normal;
            }
        }

        private SimpleOrientation GetCameraOrientation()
        {
            if (externalCamera)
            {
                // Cameras that are not attached to the device do not rotate along with it, so apply no rotation
                return SimpleOrientation.NotRotated;
            }

            // If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
            if (mirroringPreview)
            {
                // This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
                switch (deviceOrientation)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        return SimpleOrientation.Rotated270DegreesCounterclockwise;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        return SimpleOrientation.Rotated90DegreesCounterclockwise;
                }
            }

            return deviceOrientation;
        }

        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {

            // If a lock is being held it means we're still waiting for processing work on the previous frame to complete.
            // In this situation, don't wait on the semaphore but exit immediately.
            if (!frameProcessingSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                IList<DetectedFace> faces = null;

                // Create a VideoFrame object specifying the pixel format we want our capture image to be (NV12 bitmap in this case).
                // GetPreviewFrame will convert the native webcam frame into this format.
                const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Nv12;
                using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
                {
                    await mediaCapture.GetPreviewFrameAsync(previewFrame);

                    // The returned VideoFrame should be in the supported NV12 format but we need to verify this.
                    if (FaceDetector.IsBitmapPixelFormatSupported(previewFrame.SoftwareBitmap.BitmapPixelFormat))
                    {
                        faces = await this.faceTracker.ProcessNextFrameAsync(previewFrame);
                        Debug.WriteLine("Got faces: " + faces.Count.ToString());

                        var nothing = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                            (DataContext as MainPageViewModel).OneFacePresent = (faces.Count == 1);
                        });
                        
                    }
                    else
                    {
                        throw new System.NotSupportedException("PixelFormat '" + InputPixelFormat.ToString() + "' is not supported by FaceDetector");
                    }
                }
            }
            catch (Exception ex)
            {
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //this.rootPage.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                });
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }
        }

    }
}
