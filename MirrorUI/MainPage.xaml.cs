using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
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

        private readonly CancellationTokenSource _cancellationTokenSource;

        public MainPage()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            InitializeComponent();
            InitializeCameraAsync()
                .ContinueWith(
                    mediaCaptureTask => InitMirrorAsync(mediaCaptureTask.Result, _cancellationTokenSource.Token));
        }

        private async Task InitMirrorAsync(MediaCapture mediaCapture, CancellationToken cancellationToken)
        {
            FaceServiceClient client = new FaceServiceClient(OxfordApiKey);
            while (!cancellationToken.IsCancellationRequested)
            {
                InMemoryRandomAccessStream photoStream = new InMemoryRandomAccessStream();
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), photoStream);
                Face[] faces = await client.DetectAsync(photoStream.AsStreamForRead());
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
                foreach (Person person in allPersons.Where(person => persons.Contains(person.PersonId)))
                {
                    //ToDo
                }
            }
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