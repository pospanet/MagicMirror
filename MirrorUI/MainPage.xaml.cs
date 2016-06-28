using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Pospa.NET.MagicMirror.UI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly MediaCapture _mediaCapture;

        public MainPage()
        {
            this.InitializeComponent();
            InitializeSensorsAsync();
            InitializeCameraAsync();
        }

        private async Task InitializeSensorsAsync()
        {
        }

        private async Task<MediaCapture> InitializeCameraAsync()
        {
            DeviceInformationCollection allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            DeviceInformation cameraDevice =allVideoDevices.FirstOrDefault();

            if (cameraDevice == null)
            {
                return null;
            }

            var mediaCapture = new MediaCapture();

            var mediaInitSettings = new MediaCaptureInitializationSettings {VideoDeviceId = cameraDevice.Id};

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
