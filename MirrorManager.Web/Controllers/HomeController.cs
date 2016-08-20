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
//using Microsoft.ProjectOxford.Face;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.ProjectOxford.Face;
using MirrorManager.Web.Models;
using System.Security.Claims;

namespace MirrorManager.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IConfigurationRoot _configuration;
        private FaceServiceClient _faceClient;
        private UserFunctions _userFunctions;

        public HomeController(IConfigurationRoot configuration, UserFunctions userFunctions)
        {
            _configuration = configuration;
            _faceClient = new FaceServiceClient(_configuration["COGNITIVE_KEY"]);
            _userFunctions = userFunctions;
        }

        public async Task<IActionResult> Index()
        {
            Claim oid = User.Claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
            var personId = await _userFunctions.getPersonIdAsync(oid.Value);

            return View();
        }

        [Route("ajax/checkFace")]
        [HttpPost]
        public async Task<IActionResult> checkFace([FromBody]CustReq req)
        {
            byte[] bytes = Convert.FromBase64String(req.image);
            MemoryStream ms = new MemoryStream(bytes);
            
            var returnedFace = await _faceClient.DetectAsync(ms);

            return Json(returnedFace);
        }

        [Route("ajax/linkFace")]
        [HttpPost]
        public async Task<IActionResult> linkFace([FromBody]CustReq req)
        {
            //TODO: Upload picture and add it to the identity
            return null;
        }

        [Route("ajax/removeFace")]
        [HttpPost]
        public async Task<IActionResult> removeFace([FromBody]CustReq req)
        {
            //TODO: Discard identity link
            return null;
        }


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
