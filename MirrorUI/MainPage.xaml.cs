﻿using System;
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
using Pospa.Mirror.Common.MSAL;
using Pospa.Mirror.Common.Web;
using OpenWeatherMap;

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
        private const string OpenWeatherKey = "<OpenWeatherMap Key>";

        #endregion

        private const int CameraId = 0;

        private const float SourceImageHeightLimit = 1024;
        private const BitmapPixelFormat FaceDetectionPixelFormat = BitmapPixelFormat.Gray8;

        private const string Authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47";

        private const string BingMapsBaseUrl = "http://dev.virtualearth.net/";
        private const string BingMapsRouteQuery = "/REST/v1/Routes?wayPoint.1={0}&waypoint.2={1}&key={2}";

        private const string UserParameters = "id,displayName,mail,city,country,officeLocation,postalCode,streetAddress";
        private const string StartDatetimeGraphQueryOption = "startDateTime";
        private const string EndDatetimeGraphQueryOption = "endDateTime";
        private const string MicrosoftGraphEndpoint = "https://graph.microsoft.com";

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
            DispatcherTimer timer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(5)};
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => {
                    tbTime.Text = DateTime.Now.ToLocalTime().ToString("HH:mm");
                    tbDate.Text = DateTime.Now.ToLocalTime().ToString("dddd, MMMM d");
                });
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
                Stream photoStream;
                try
                {
                    photoStream = await GetPhotoStreamAsync(mediaCapture);
                }
                catch (Exception ex)
                {
                    HockeyClient.Current.TrackEvent("InitMirrorAsync (GetPhotoStreamAsync) - Exception",
                        new Dictionary<string, string> {{"Message", ex.Message}, {"Stack", ex.StackTrace}});
                    continue;
                }
                if (FaceDetector.IsSupported && faceDetector!=null)
                {
                    SoftwareBitmap image = await ConvertImageForFaceDetection(photoStream.AsRandomAccessStream());

                    IList<DetectedFace> localFace;
                    try
                    {
                        localFace = await faceDetector.DetectFacesAsync(image);
                    }
                    catch (Exception ex)
                    {
                        HockeyClient.Current.TrackEvent("InitMirrorAsync (DetectFacesAsync Locally) - Exception",
                            new Dictionary<string, string> {{"Message", ex.Message}, {"Stack", ex.StackTrace}});
                        continue;
                    }

                    if (!localFace.Any())
                    {
                        await ClearScrean();
                        continue;
                    }
                    HockeyClient.Current.TrackEvent("Face Detected Locally");
                }
                try
                {
                    await ShowPersonalizedInformation(client, photoStream);

                }
                catch (Exception ex)
                {
                    HockeyClient.Current.TrackEvent("InitMirrorAsync (ShowPersonalizedInformation) - Exception",
                        new Dictionary<string, string> {{"Message", ex.Message}, {"Stack", ex.StackTrace}});
                }
            }
        }

        private async Task ClearScrean()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                tbName.Text = string.Empty;
                tbDrive.Text = string.Empty;
                tbNext.Text = string.Empty;
            });
        }

        private async Task ShowPersonalizedInformation(FaceServiceClient client, Stream photoStream)
        {
            Face[] faces;
            try
            {
                faces = await client.DetectAsync(photoStream, true, true);
            }
            catch (Exception ex)
            {
                faces = new Face[0];
                HockeyClient.Current.TrackEvent("ShowPersonalizedInformation (DetectAsync) - Exception",
                    new Dictionary<string, string> {{"Message", ex.Message}, {"Stack", ex.StackTrace}});
            }

            if (!faces.Any())
            {
                await ClearScrean();
                return;
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
                HockeyClient.Current.TrackEvent("ShowPersonalizedInformation (IdentifyAsync) - Exception",
                    new Dictionary<string, string> { { "Message", ex.Message }, { "Stack", ex.StackTrace } });
                return;
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
                    HockeyClient.Current.TrackEvent("ShowPersonalizedInformation (ShowPersonalizedInfoPanel) - Exception",
                        new Dictionary<string, string> { { "Message", ex.Message }, { "Stack", ex.StackTrace } });
                }
            }
            else
            {
                await ClearScrean();
            }
        }

        private async Task ShowPersonalizedInfoPanel(Person person)
        {
            ClientCredential credential = new ClientCredential(ClientId, ClientSecret);
            AzureTableStoreTokenCache tokenCache =
                await AzureTableStoreTokenCache.GetTokenCacheAsync(new TokenCacheConfig(), person.Name);
            AuthenticationContext authContext = new AuthenticationContext(Authority, tokenCache);
            AuthenticationResult accessTokenAuthenticationResult = await
                authContext.AcquireTokenSilentAsync(MicrosoftGraphEndpoint, credential,
                    new UserIdentifier(person.Name, UserIdentifierType.UniqueId));
            GraphServiceClient graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    (requestMessage) =>
                    {
                        string accessToken = accessTokenAuthenticationResult.AccessToken;
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                        return Task.FromResult(0);
                    }));
            User me = await graphClient.Me.Request().Select(UserParameters).GetAsync();

            // Get user's unread mail count
            var inbox = await graphClient.Me.MailFolders.Inbox.Request().GetAsync();
            var unreadMail = inbox.UnreadItemCount;
            if (unreadMail > 99) unreadMail = 99;
            tbMail.Text = unreadMail.ToString();

            // Weather
            var weather = await GetCurrentWeather();
            int currentTemperature = (int)weather.Temperature.Value;
            tbWeatherValue.Text = currentTemperature.ToString();
            var icon = GetWeatherIcon(weather.Weather.Icon);
            WeatherImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(base.BaseUri, icon));
            tbWeatherText.Text = weather.Weather.Value;

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
                HockeyClient.Current.TrackEvent("User Driving Info - Exception",
                    new Dictionary<string, string> { { "Message", ex.Message }, { "Stack", ex.StackTrace } });
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

        private static async Task<CurrentWeatherResponse> GetCurrentWeather()
        {
            var client = new OpenWeatherMapClient(OpenWeatherKey);

            Geolocator geolocator = new Geolocator();
            Geoposition location = await geolocator.GetGeopositionAsync(TimeSpan.MaxValue, TimeSpan.FromSeconds(5));

            Coordinates coordinates = new Coordinates();
            coordinates.Latitude = location.Coordinate.Point.Position.Latitude;
            coordinates.Longitude = location.Coordinate.Point.Position.Longitude;

            var weather = await client.CurrentWeather.GetByCoordinates(coordinates, MetricSystem.Metric);
            return weather;
        }
        private static string GetWeatherIcon(string icon)
        {
            string asset;
            switch (icon)
            {
                case "01d":
                    asset = "01d.png";
                    break;
                case "01n":
                    asset = "01n.png";
                    break;
                case "02d":
                    asset = "02d.png";
                    break;
                case "02n":
                    asset = "02n.png";
                    break;
                case "03d":
                case "03n":
                case "04d":
                case "04n":
                    asset = "03or4.png";
                    break;
                case "09n":
                case "09d":
                    asset = "09.png";
                    break;
                case "10d":
                case "10n":
                    asset = "09.png";
                    break;
                case "11d":
                    asset = "11d.png";
                    break;
                case "11n":
                    asset = "11n.png";
                    break;
                case "13d":
                case "13n":
                    asset = "13.png";
                    break;
                case "50n":
                case "50d":
                default:
                    asset = "50.png";
                    break;
            }
            return "Assets/Weather/" + asset;
        }

        private static async Task<DrivingInfo> GetDrivingInfoAsync(string to, string from = null)
        {
            string realFrom = from;
            if (string.IsNullOrEmpty(realFrom))
            {
                Geolocator geolocator = new Geolocator();
                Geoposition location = await geolocator.GetGeopositionAsync(TimeSpan.MaxValue, TimeSpan.FromSeconds(5));
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
            DeviceInformation cameraDevice = allVideoDevices.Skip(CameraId).FirstOrDefault();

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
            catch (UnauthorizedAccessException ex)
            {
                HockeyClient.Current.TrackEvent("InitializeCameraAsync (UnauthorizedAccessException) - Exception",
                new Dictionary<string, string> { { "Message", ex.Message }, { "Stack", ex.StackTrace } });
                return null;
            }
            catch (Exception ex)
            {
                HockeyClient.Current.TrackEvent("InitializeCameraAsync - Exception",
           new Dictionary<string, string> { { "Message", ex.Message }, { "Stack", ex.StackTrace } });
                return null;
            }
            return mediaCapture;
        }
    }
}