using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Diagnostics;
using Microsoft.ProjectOxford.Face;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace MirrorManager.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IConfigurationRoot _configuration;

        public HomeController(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {

            //var a = new CalendarEvent();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromBody] CustReq req)
        {
            string COGNITIVE_KEY = _configuration["COGNITIVE_KEY"];
            var Face = new FaceServiceClient(COGNITIVE_KEY);

            byte[] bytes = Convert.FromBase64String(req.image);
            MemoryStream ms = new MemoryStream(bytes);

            var returnedFace = await Face.DetectAsync(ms);

            return Json(returnedFace);
        }

        //TODO: Return number of faces in picture (used for validation of camera button)

        //TODO: Upload picture and add it to the identity

        //TODO: Discard identity link

        public async Task<IActionResult> About()
        {
            var accessToken = await HttpContext.Authentication.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            var refreshToken = await HttpContext.Authentication.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
            Debug.WriteLine(accessToken);
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
