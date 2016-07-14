using System;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;

namespace MirrorManager.UWP
{
    internal static class AuthenticationHelper
    {
        static string clientId = App.Current.Resources["ida:ClientID"].ToString();
        static string tenant = App.Current.Resources["ida:Domain"].ToString();
        static string AADInstance = App.Current.Resources["ida:AADInstance"].ToString();
        static string authority = AADInstance + tenant;

        //Use "organizations" as your authority when you want the app to work on any Azure Tenant.
        //static string authority = "organizations";

        public const string ResourceUrl = "https://graph.microsoft.com/";

        private static WebAccountProvider aadAccountProvider = null;
        private static WebAccount userAccount = null;

        public static async Task<string> GetTokenAsync()
        {

            string token = null;

            aadAccountProvider = await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.microsoft.com", authority);

            // Check if there's a record of the last account used with the app
            var userID = App.Settings.Values["userID"];

            if (userID != null)
            {

                WebTokenRequest webTokenRequest = new WebTokenRequest(aadAccountProvider, string.Empty, clientId);
                webTokenRequest.Properties.Add("resource", ResourceUrl);

                userAccount = await WebAuthenticationCoreManager.FindAccountAsync(aadAccountProvider, (string)userID);

                WebTokenRequestResult webTokenRequestResult = await WebAuthenticationCoreManager.RequestTokenAsync(webTokenRequest, userAccount);
                if (webTokenRequestResult.ResponseStatus == WebTokenRequestStatus.Success || webTokenRequestResult.ResponseStatus == WebTokenRequestStatus.AccountSwitch)
                {
                    WebTokenResponse webTokenResponse = webTokenRequestResult.ResponseData[0];
                    userAccount = webTokenResponse.WebAccount;
                    token = webTokenResponse.Token;
                }
                else
                {
                    // The saved account could not be used for getting a token
                    // Make sure that the UX is ready for a new sign in
                    SignOut();
                }

            }
            else
            {
                // There is no recorded user. Start a sign in flow without imposing a specific account.
                WebTokenRequest webTokenRequest = new WebTokenRequest(aadAccountProvider, string.Empty, clientId, WebTokenRequestPromptType.ForceAuthentication);
                webTokenRequest.Properties.Add("resource", ResourceUrl);

                WebTokenRequestResult webTokenRequestResult = await WebAuthenticationCoreManager.RequestTokenAsync(webTokenRequest);
                if (webTokenRequestResult.ResponseStatus == WebTokenRequestStatus.Success)
                {
                    WebTokenResponse webTokenResponse = webTokenRequestResult.ResponseData[0];
                    userAccount = webTokenResponse.WebAccount;
                    token = webTokenResponse.Token;

                }
            }

            // We succeeded in getting a valid user.
            if (userAccount != null)
            {
                // save user ID in local storage
                App.Settings.Values["userID"] = userAccount.Id;
                App.Settings.Values["userEmail"] = userAccount.UserName;
                App.Settings.Values["userName"] = userAccount.Properties["DisplayName"];

                return token;
            }

            // We didn't succeed in getting a valid user. Clear the app settings so that another user can sign in.
            else
            {

                SignOut();
                return null;
            }


        }

        /// <summary>
        /// Signs the user out of the service.
        /// </summary>
        public static void SignOut()
        {
            //Clear stored values from last authentication.
            App.Settings.Values["userID"] = null;
            App.Settings.Values["userEmail"] = null;
            App.Settings.Values["userName"] = null;

        }

        public static string GetAppRedirectURI() => string.Format("ms-appx-web://microsoft.aad.brokerplugin/{0}", WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host).ToUpper();

    }
}