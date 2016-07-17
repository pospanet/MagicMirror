using Mirror.Common.DTO;
using Mirror.Common.Utils;
using MirrorManager.UWP.Helpers;
using MirrorManager.UWP.Services;
using MirrorManager.UWP.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.System.Threading;
using Windows.UI.Core;
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

        private FaceDetectionEffect faceDetectionEffect;

        private const string personGroupId = "userfaces";
        private OxfordPerson currentPerson = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            VisualStateManager.GoToState(this, "InitializingPhoto", false);

            viewModel = (MainPageViewModel)DataContext;

            RegisterOrientationEventHandlers();

            var token = App.Settings.Values["Token"];

            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            var result = await hc.GetAsync("https://graph.microsoft.com/v1.0/me");

            if (result.IsSuccessStatusCode)
            {
                string res = await result.Content.ReadAsStringAsync();
                OfficeUser user = JsonConvert.DeserializeObject<OfficeUser>(res);

                ((MainPageViewModel)DataContext).UserName = user.displayName;

                viewModel.OxfordStatus = "Checking if we know you...";

                bool userRegistered = await checkUserRegistrationAsync();
                if (!userRegistered)
                {
                    viewModel.OxfordStatus = "Looks like we haven't seen you yet. \nNo problem, take some pictures of your face. Five should be enough.";
                }
                else
                {
                    viewModel.OxfordStatus = "Welcome back. Do you want to recalibrate?";
                }
            }

            await InitializeCameraAsync();

            await CreateFaceDetectionEffectAsync();
        }



        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            UnregisterOrientationEventHandlers();

            await CleanupCameraAsync();
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
            if (currentPerson == null)
            {
                Debug.WriteLine("Creating person in group.");

                var userData = new UserData(App.Settings.Values["userID"].ToString(), App.Settings.Values["Token"].ToString());
                var id = await FaceApiService.CreatePersonInGroupAsync(personGroupId, viewModel.UserName, userData);

                Debug.WriteLine("Person created with ID: " + id);

                currentPerson = new OxfordPerson()
                {
                    personId = id,
                    name = viewModel.UserName,
                    userData = JsonConvert.SerializeObject(userData).EncodeBase64(Encoding.UTF8)
                };
            }

            var stream = await capturePhotoStreamAsync();

            if (stream != null)
            {
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);
                photo.Source = image;

                var faceId = await FaceApiService.AddPersonFaceAsync(personGroupId, currentPerson.personId, stream);
                Debug.WriteLine($"Face added to person {currentPerson.personId} with ID: {faceId}");

                stream.Dispose();
            }
            else
            {
                viewModel.OxfordStatus = "Unable to capture the photo.";
            }

        }

        private async Task<InMemoryRandomAccessStream> capturePhotoStreamAsync()
        {
            var stream = new InMemoryRandomAccessStream();

            try
            {
                Debug.WriteLine("Capturing photo...");
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                Debug.WriteLine("Photo captured!");

                stream.Seek(0);

                return stream;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when capturing a photo: {0}", ex.ToString());
                return null;
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
                faceDetectionEffect.Enabled = false;
                faceDetectionEffect = null;
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
                VisualStateManager.GoToState(this, "PhotoReady", false);
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

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
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

        private async Task CreateFaceDetectionEffectAsync()
        {
            var definition = new FaceDetectionEffectDefinition();
            definition.SynchronousDetectionEnabled = false;
            definition.DetectionMode = FaceDetectionMode.Balanced;

            faceDetectionEffect = (FaceDetectionEffect)await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);
            faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;
            faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(66); // min. 33

            faceDetectionEffect.Enabled = true;
        }

        private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            //Debug.WriteLine("Faces: {0}", args.ResultFrame.DetectedFaces.Count);

            var nothing = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                (DataContext as MainPageViewModel).OneFacePresent = (args.ResultFrame.DetectedFaces.Count == 1);
            });
            
        }

        private async Task<bool> checkUserRegistrationAsync()
        {
            var userId = App.Settings.Values["userID"].ToString();
            var people = await FaceApiService.GetPeopleInGroupAsync(personGroupId);

            if (people.Count == 0)
            {
                return false; // no users yet
            }
            
            var res = (from p in people
                      where JsonConvert.DeserializeObject<UserData>(p.userData.DecodeBase64(Encoding.UTF8)).UserId == userId
                      select p).FirstOrDefault();

            if (res == null)
            {
                return false; // user not registered
            }
            else
            {
                currentPerson = res;
                return true;
            }
        }

        private async void UpdateOxford_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Updating...");

            var res = await FaceApiService.UpdatePersonAsync(personGroupId, currentPerson.personId, "Michal Martin");

            Debug.WriteLine(res);
        }

        private async void IdentifyCheck_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Checking if it works...");

            var stream = await capturePhotoStreamAsync();

            if (stream != null)
            {
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);
                photo.Source = image;

                var res = await FaceApiService.IdentifyPersonAsync(personGroupId, stream);
                viewModel.FaceRecognized = (currentPerson.personId == res);
                Debug.WriteLine($"Logged in person: {currentPerson.personId}, identified person: {res}");

                stream.Dispose();
            }
            else
            {
                viewModel.OxfordStatus = "Unable to capture photo.";
            }
        }
    }
}
