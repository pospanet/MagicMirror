using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace MirrorManager.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            progress.Visibility = Visibility.Visible;
            await SignInUserAsync();
            progress.Visibility = Visibility.Collapsed;
        }

        private async Task SignInUserAsync()
        {
            var token = await AuthenticationHelper.GetTokenAsync();

            if (token != null)
            {
                txtStatus.Text = "Done";
                App.Settings.Values["Token"] = token;
                Frame.Navigate(typeof(MainPage));
            }
            else
            {
                txtStatus.Text = "not done";
            }
        }
    }
}
