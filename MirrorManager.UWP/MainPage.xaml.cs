﻿using MirrorManager.UWP.DAO;
using MirrorManager.UWP.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MirrorManager.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var token = App.Settings.Values["Token"];

            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            var result = await hc.GetAsync("https://graph.microsoft.com/v1.0/me");

            if (result.IsSuccessStatusCode)
            {
                string res = await result.Content.ReadAsStringAsync();
                UserObject user = JsonConvert.DeserializeObject<UserObject>(res);

                var viewModel = ((MainPageViewModel)DataContext).UserName = user.displayName;
            }
        }
    }
}
