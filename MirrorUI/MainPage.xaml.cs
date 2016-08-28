using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Graph;
using Microsoft.HockeyApp;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json.Linq;
using Pospa.Mirror.Common;
using Pospa.Mirror.Common.MSAL;
using Pospa.Mirror.Common.Web;

namespace Pospa.NET.MagicMirror.UI
{
    public sealed partial class MainPage : Page
    {
        #region app/user keys

        private const string OxfordApiKey = "<Oxford Api Key>";
        private const string ClientId = "<Client ID>";
        private const string ClientSecret = "<Client Secret>";
        private const string BingMapsApiKey = "<Bing Maps API key>";
        private const string PersonGroupId = "<Person Group ID>";


        #endregion

        private const float SourceImageHeightLimit = 1024;
        private const BitmapPixelFormat FaceDetectionPixelFormat = BitmapPixelFormat.Gray8;

        private const string Authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47";

        private const string BingMapsBaseUrl = "http://dev.virtualearth.net/";
        private const string BingMapsRouteQuery = "/REST/v1/Routes?wayPoint.1={0}&waypoint.2={1}&key={2}";

        private const string UserParameters = "id,displayName,mail,city,country,officeLocation,postalCode,streetAddress";
        private const string StartDatetimeGraphQueryOption = "startDateTime";
        private const string EndDatetimeGraphQueryOption = "endDateTime";

        private static readonly WebRequestHelper WebRequestHelper;
        private readonly CancellationTokenSource _cancellationTokenSource;


        static MainPage()
        {
            WebRequestHelper = new WebRequestHelper(new Uri(BingMapsBaseUrl));
        }

        public MainPage()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            InitializeComponent();
            InitializeClock();
            InitializeCameraAsync()
                .ContinueWith(
                    mediaCaptureTask => InitMirrorAsync(mediaCaptureTask.Result, _cancellationTokenSource.Token));
        }

        private void InitializeClock()
        {
            DispatcherTimer timer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(15)};
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => { tbTime.Text = DateTime.Now.ToLocalTime().ToString("f"); });
        }

        private async Task InitMirrorAsync(MediaCapture mediaCapture, CancellationToken cancellationToken)
        {
            await
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () => { await Geolocator.RequestAccessAsync(); });

            FaceServiceClient client = new FaceServiceClient(OxfordApiKey);
            FaceDetector faceDetector = FaceDetector.IsSupported ? await FaceDetector.CreateAsync() : null;
            while (!cancellationToken.IsCancellationRequested)
            {
                Stream photoStream = await GetPhotoStreamAsync(mediaCapture);

                if (FaceDetector.IsSupported)
                {
                    SoftwareBitmap image = await ConvertImageForFaceDetection(photoStream.AsRandomAccessStream());

                    IList<DetectedFace> localFace = await faceDetector.DetectFacesAsync(image);

                    if (!localFace.Any())
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            tbName.Text = string.Empty;
                            tbDrive.Text = string.Empty;
                            tbNext.Text = string.Empty;
                        });
                        continue;
                    }
                    HockeyClient.Current.TrackEvent("Face Detected Locally");
                }
                Face[] faces = await client.DetectAsync(photoStream, true, true);
                if (!faces.Any())
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        tbName.Text = string.Empty;
                        tbDrive.Text = string.Empty;
                        tbNext.Text = string.Empty;
                    });
                    continue;
                }
                HockeyClient.Current.TrackEvent("Face Detected Remotely (Oxford)");
                IdentifyResult[] identifyResults;
                try
                {
                    identifyResults =
                        await client.IdentifyAsync(PersonGroupId, faces.Select(face => face.FaceId).ToArray());
                }
                catch (Exception ex)
                {
                    HockeyClient.Current.TrackEvent("Face API IdentifyAsync - Exception",
                        new Dictionary<string, string> {{"Message", ex.Message}});
                    continue;
                }


                Guid[] personIds = identifyResults.Select(r => r.Candidates.First().PersonId).ToArray();

                Task<Person>[] personTasks =
                    personIds.Select(async p => await client.GetPersonAsync(PersonGroupId, p)).ToArray();
                Task.WaitAll(personTasks);

                if (personTasks.Any() && personTasks.First().Result != null)
                {
                    Person person = personTasks.First().Result;
                    HockeyClient.Current.TrackEvent("Face Recognized (Oxford)",
                        new Dictionary<string, string>
                        {
                            {"Person ID", person.PersonId.ToString()},
                            {"Graph User ID", person.Name}
                        });
                    try
                    {
                        await ShowPersonalizedInfoPanel(person);
                    }
                    catch (Exception ex)
                    {
                        HockeyClient.Current.TrackEvent("ShowPersonalizedInfoPanel - Exception",
                            new Dictionary<string, string> {{"Message", ex.Message}});
                    }
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        tbName.Text = string.Empty;
                        tbDrive.Text = string.Empty;
                        tbNext.Text = string.Empty;
                    });
                }
            }
        }

        private async Task ShowPersonalizedInfoPanel(Person person)
        {
            ClientCredential credential = new ClientCredential(ClientId, ClientSecret);
            AzureTableStoreTokenCache tokenCache =
                await AzureTableStoreTokenCache.GetTokenCacheAsync(new TokenCacheConfig(), person.Name);
            AuthenticationContext authContext = new AuthenticationContext(Authority, tokenCache);
            AuthenticationResult accessTokenAuthenticationResult = await
                authContext.AcquireTokenSilentAsync("https://graph.microsoft.com", credential,
                    new UserIdentifier(person.Name, UserIdentifierType.UniqueId));
            GraphServiceClient graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async requestMessage =>
                    {
                        string accessToken = accessTokenAuthenticationResult.AccessToken;

                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    }));
            User me = await graphClient.Me.Request().Select(UserParameters).GetAsync();
            List<QueryOption> options = new List<QueryOption>
            {
                new QueryOption(StartDatetimeGraphQueryOption, DateTime.UtcNow.ToString("o")),
                new QueryOption(EndDatetimeGraphQueryOption, DateTime.UtcNow.AddDays(1).ToString("o"))
            };
            IUserCalendarViewCollectionPage cal = await graphClient.Me.CalendarView.Request(options).Top(1).GetAsync();
            DrivingInfo drivingInfo;
            string displayName, displayDrive, displayNext;
            try
            {
                drivingInfo = await GetDrivingInfoAsync(me.City);
                displayDrive = string.Concat("Office ETA ", (drivingInfo.DurationTrafic/60).ToString("F0"), "mins (",
                    drivingInfo.Distance.ToString("F0"), "Km)");
            }
            catch (Exception ex)
            {
                displayDrive = string.Empty;
                HockeyClient.Current.TrackEvent("GetDrivingInfoAsync - Exception",
                    new Dictionary<string, string> {{"Message", ex.Message}});
            }

            if (!string.IsNullOrEmpty(me.DisplayName))
            {
                displayName = string.Concat("Hi ", me.DisplayName);
            }
            else
            {
                displayName = string.Empty;
                HockeyClient.Current.TrackEvent("User Display Name",
                    new Dictionary<string, string> {{"User", me.Id}});
            }
            if (cal.FirstOrDefault() != null)
            {
                displayNext = string.Concat("Next: ", cal.FirstOrDefault().Subject, " @ ",
                    cal.FirstOrDefault().Location.DisplayName);
            }
            else
            {
                displayNext = string.Empty;
                HockeyClient.Current.TrackEvent("User Calendar",
                    new Dictionary<string, string> {{"User", me.Id}});
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                tbName.Text = displayName;
                tbDrive.Text = displayDrive;
                tbNext.Text = displayNext;
            });
        }

        private static async Task<DrivingInfo> GetDrivingInfoAsync(string to, string from = null)
        {
            string realFrom = from;
            if (string.IsNullOrEmpty(realFrom))
            {
                Geolocator geolocator = new Geolocator();
                Geoposition location = await geolocator.GetGeopositionAsync();
                realFrom = string.Concat(location.Coordinate.Point.Position.Latitude, ',',
                    location.Coordinate.Point.Position.Longitude);
            }
            JObject route = await WebRequestHelper.SendWebRequestDynamicAsync(
                new Uri(string.Format(BingMapsRouteQuery, realFrom, to, BingMapsApiKey), UriKind.Relative),
                HttpMethod.Get);
            JToken dataToken = route.SelectToken(@"resourceSets[0].resources[0]");
            return new DrivingInfo(dataToken.SelectToken("travelDistance").Value<decimal>(),
                dataToken.SelectToken("travelDuration").Value<decimal>(),
                dataToken.SelectToken("travelDurationTraffic").Value<decimal>());
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
                float scalingFactor = SourceImageHeightLimit/decoder.PixelHeight;
                transform.ScaledWidth = (uint) Math.Floor(decoder.PixelWidth*scalingFactor);
                transform.ScaledHeight = (uint) Math.Floor(decoder.PixelHeight*scalingFactor);
            }


            SoftwareBitmap image =
                await
                    decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform,
                        ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

            if (image.BitmapPixelFormat != FaceDetectionPixelFormat)
            {
                image = SoftwareBitmap.Convert(image, FaceDetectionPixelFormat);
            }
            return image;
        }

        private static async Task<MediaCapture> InitializeCameraAsync()
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