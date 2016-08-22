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
using Microsoft.Graph;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

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
            Claim oid = User.Claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
            var personId = await _userFunctions.getPersonIdAsync(oid.Value);
            if(personId == null)
            {
                var person = await _faceClient.CreatePersonAsync(_configuration["personGroupId"], oid.Value);
                await _userFunctions.setPersonIdAsync(oid.Value, person.PersonId.ToString());
                personId = person.PersonId.ToString();
            }

            byte[] bytes = Convert.FromBase64String(req.image);
            MemoryStream ms = new MemoryStream(bytes);

            try
            {
                var returnedFace = await _faceClient.AddPersonFaceAsync(_configuration["personGroupId"], Guid.Parse(personId), ms);
                return Json(returnedFace);
            }
            catch (FaceAPIException e)
            {
                return Json(new { error = e.ErrorMessage });
            }
        }

        [Route("ajax/deleteIdentity")]
        [HttpPost]
        public async Task<IActionResult> deleteIdentity([FromBody]CustReq req)
        {
            Claim oid = User.Claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
            var personId = await _userFunctions.getPersonIdAsync(oid.Value);
            if(personId == null)
            {
                return Json(new { message = "No identity linked to your account..." });
            }

            await _faceClient.DeletePersonAsync(_configuration["personGroupId"], Guid.Parse(personId));
            await _userFunctions.setPersonIdAsync(oid.Value, null);

            return Json(new { message = "Identity deleted..." });
        }

        [Route("ajax/getAvatar")]
        [HttpGet]
        public async Task<IActionResult> getAvatar()
        {
            GraphServiceClient graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        string accessToken = await HttpContext.Authentication.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);

                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    }));
            try
            {
                var photo = await graphClient.Me.Photo.Content.Request().GetAsync();

                return File(photo, "image/jpg", "avatar.png");
            }
            catch (ServiceException se)
            {
                return Json(se);
            }
        }

        [Route("About")]
        public async Task<IActionResult> About()
        {
            return View();
        }

        [Route("Contact")]
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
