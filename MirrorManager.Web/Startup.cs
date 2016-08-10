using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
//using MirrorManager.Web.MSAL;
using Microsoft.AspNetCore.Http;

namespace MirrorManager.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

            services.AddAuthentication(
                SharedOptions => SharedOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme);

            services.AddOptions();

            services.Configure<IConfigurationRoot>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseCookieAuthentication();

            var Authority = Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"];


            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
            {
                ClientId = Configuration["Authentication:AzureAd:ClientId"],
                ClientSecret = Configuration["Authentication:AzureAd:ClientSecret"],
                Authority = Authority,
                CallbackPath = Configuration["Authentication:AzureAd:CallbackPath"],
                ResponseType = OpenIdConnectResponseType.CodeIdToken,
                //Events = new OpenIdConnectEvents()
                //{
                //    OnAuthorizationCodeReceived = async context =>
                //    {
                //        AzureTableStoreTokenCache tokenCache = await AzureTableStoreTokenCache.GetTokenCacheAsync(new MSAL.Configuration.TokenCacheConfig(), "test");

                //        // given the authorization code
                //        var authorizationCode = context.ProtocolMessage.Code;
                //        var request = context.HttpContext.Request;
                //        var redirectUri = new Uri(context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase + "/signin-oidc");

                //        // get and verify the access token and refresh token
                //        var credential = new ClientCredential(Configuration["Authentication:AzureAd:ClientId"], Configuration["Authentication:AzureAd:ClientSecret"]);
                //        var authContext = new AuthenticationContext(Authority, tokenCache);
                //        var result = await authContext.AcquireTokenByAuthorizationCodeAsync(authorizationCode, redirectUri, credential, "https://graph.windows.net");

                //        // serialize the per-user TokenCache
                //        var tokenBlob = tokenCache.Serialize();

                //        // and store it in the authentication properties so that the Controller can access it
                //        context.HandleCodeRedemption(result.AccessToken, result.IdToken);
                //    }
                //}
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}