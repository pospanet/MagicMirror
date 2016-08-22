using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace Pospa.NET.MagicMirror.UI
{
    public sealed partial class MainPage : Page
    {
        private const string OxfordApiKey = "<API key>";
        private const string PersonGroupId = "<person group ID>";
        private const double MinConfidenceLevel = 75;
        const float SourceImageHeightLimit = 1024;
        const BitmapPixelFormat FaceDetectionPixelFormat = BitmapPixelFormat.Gray8;


        private readonly List<Person> _lastSeenPersonList;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MainPage()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _lastSeenPersonList = new List<Person>();
            InitializeComponent();
            InitializeCameraAsync()
                .ContinueWith(
                    mediaCaptureTask => InitMirrorAsync(mediaCaptureTask.Result, _cancellationTokenSource.Token));
        }

        private async Task InitMirrorAsync(MediaCapture mediaCapture, CancellationToken cancellationToken)
        {
            FaceServiceClient client = new FaceServiceClient(OxfordApiKey);
            FaceDetector faceDetector = await FaceDetector.CreateAsync();
            while (!cancellationToken.IsCancellationRequested)
            {
                Stream photoStream = await GetPhotoStreamAsync(mediaCapture);

                SoftwareBitmap image = await ConvertImageForFaceDetection(photoStream.AsRandomAccessStream());

                IList<DetectedFace> localFace = await faceDetector.DetectFacesAsync(image);

                if (!localFace.Any())
                {
                    continue;
                }
                Face[] faces = await client.DetectAsync(photoStream, true, true);


                IdentifyResult[] identifyResults =
                    await client.IdentifyAsync(PersonGroupId, faces.Select(face => face.FaceId).ToArray());

                Guid[] persons =
                    identifyResults.Where(
                        result =>
                            result.Candidates.OrderByDescending(candidate => candidate.Confidence).First().Confidence >
                            MinConfidenceLevel)
                        .Select(
                            result =>
                                result.Candidates.OrderByDescending(candidate => candidate.Confidence).First().PersonId)
                        .ToArray();

                Person[] allPersons = await client.GetPersonsAsync(PersonGroupId);

                Person[] allKnownPersons = allPersons.Where(person => persons.Contains(person.PersonId)).ToArray();

                foreach (Person person in allPersons.Where(p=>!_lastSeenPersonList.Select(lsp=>lsp.PersonId).Contains(p.PersonId)))
                {
                    ShowPersonalizedInfoPanel(person);
                }
            }
        }

        private async Task ShowPersonalizedInfoPanel(Person person)
        {
            throw new NotImplementedException();
        }

        private static async Task<Stream> GetPhotoStreamAsync(MediaCapture mediaCapture)
        {
            InMemoryRandomAccessStream photoStream = new InMemoryRandomAccessStream();
            await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), photoStream);
            photoStream.AsStreamForRead().Seek(0, SeekOrigin.Begin);
            return photoStream.AsStreamForRead();
        }

        private static async Task<SoftwareBitmap> ConvertImageForFaceDetection(IRandomAccessStream imageStream)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(imageStream);

            BitmapTransform transform = new BitmapTransform();

            if (decoder.PixelHeight > SourceImageHeightLimit)
            {
                float scalingFactor = (float)SourceImageHeightLimit / (float)decoder.PixelHeight;
                transform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * scalingFactor);
                transform.ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * scalingFactor);
            }


            SoftwareBitmap image = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

            if (image.BitmapPixelFormat != FaceDetectionPixelFormat)
            {
                image = SoftwareBitmap.Convert(image, FaceDetectionPixelFormat);
            }
            return image;
        }

        private async Task<MediaCapture> InitializeCameraAsync()
        {
            DeviceInformationCollection allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            DeviceInformation cameraDevice = allVideoDevices.FirstOrDefault();

            if (cameraDevice == null)
            {
                return null;
            }

            MediaCapture mediaCapture = new MediaCapture();

            MediaCaptureInitializationSettings mediaInitSettings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = cameraDevice.Id
            };

            try
            {
                await mediaCapture.InitializeAsync(mediaInitSettings);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
            return mediaCapture;
        }
    }
}