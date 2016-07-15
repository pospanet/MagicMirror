using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.IO;
using Newtonsoft.Json;
using Windows.Data.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Mirror.Common.Utils;
using Mirror.Common.DTO;

namespace MirrorManager.UWP.Helpers
{
    public class FaceApiHelper
    {
        static string faceApiKey = App.Current.Resources["FaceApiKey"].ToString();

        public static async Task<List<OxfordPerson>> GetPeopleInGroupAsync(string groupId)
        {
            var hc = CreateClient();
            var response = await hc.GetAsync($"persongroups/{groupId}/persons");

            if (response.IsSuccessStatusCode)
            {
                var people = JsonConvert.DeserializeObject<List<OxfordPerson>>(await response.Content.ReadAsStringAsync());
                return people;
            }
            else
            {
                throw new OxfordException(await response.Content.ReadAsStringAsync());
            }
        }

        public static async Task<string> CreatePersonInGroupAsync(string groupId, string userName, UserData userData)
        {
            userName = userName.Length > 128 ? userName.Substring(0, 127) : userName;

            var jsonUserData = JsonConvert.SerializeObject(userData).EncodeBase64(Encoding.UTF8);

            var content = new StringContent($"{{\"name\": \"{userName}\", \"userData\": \"{jsonUserData}\" }}", Encoding.UTF8, "application/json");

            var hc = CreateClient();
            var response = await hc.PostAsync($"persongroups/{groupId}/persons", content);

            if (response.IsSuccessStatusCode)
            {
                var person = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                return person["personId"].GetString();
            }
            else
            {
                throw new OxfordException(await response.Content.ReadAsStringAsync());
            }
        }

        public static async Task<string> AddPersonFaceAsync(string groupId, string personId, InMemoryRandomAccessStream photoStream, string userData = null)
        {
            photoStream.Seek(0);
            var content = new StreamContent(photoStream.AsStreamForRead());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var hc = CreateClient();
            var response = await hc.PostAsync($"persongroups/{groupId}/persons/{personId}/persistedFaces", content);

            if (response.IsSuccessStatusCode)
            {
                var face = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                return face["persistedFaceId"].ToString();
            }
            else
            {
                throw new OxfordException(await response.Content.ReadAsStringAsync());
            }
        }

        public static async Task<bool> UpdatePersonAsync(string groupId, string personId, string newUserName = null, string newUserData = null)
        {
            if (newUserName == null && newUserData == null)
                return true;

            JsonObject attributes = new JsonObject();
            
            if (newUserName != null)
            {
                attributes.SetNamedValue("name", JsonValue.CreateStringValue(newUserName));
            }

            if (newUserData != null)
            {
                attributes.Add("userData", JsonValue.CreateStringValue(newUserData));
            }

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), $"persongroups/{groupId}/persons/{personId}")
            {
                Content = new StringContent(attributes.ToString(), Encoding.UTF8, "application/json")
            };

            var hc = CreateClient();
            var response = await hc.SendAsync(request);

            return response.IsSuccessStatusCode;
        }

        public static async Task<string> IdentifyPersonAsync(string groupId, InMemoryRandomAccessStream photoStream)
        {
            // First detect face and get Face ID.
            // https://api.projectoxford.ai/face/v1.0/detect
            photoStream.Seek(0);
            var content = new StreamContent(photoStream.AsStreamForRead());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var hc = CreateClient();
            var response = await hc.PostAsync($"detect", content);

            string faceId = null;

            if (response.IsSuccessStatusCode)
            {
                string rawResponse = await response.Content.ReadAsStringAsync();
                rawResponse = rawResponse.Trim(new char[] { '[', ']' });

                JObject face = JObject.Parse(rawResponse);
                faceId = face["faceId"].ToString();
            }
            else
            {
                throw new OxfordException($"Error when sending Detect request: {await response.Content.ReadAsStringAsync()}");
            }

            // Then identify who that is.
            // https://api.projectoxford.ai/face/v1.0/identify
            if (!String.IsNullOrEmpty(faceId))
            {
                OxfordIdentifyRequest req = new OxfordIdentifyRequest(groupId, faceId);
                var requestContent = new StringContent(JsonConvert.SerializeObject(req), Encoding.UTF8, "application/json");
                var resp = await hc.PostAsync("identify", requestContent);

                if (resp.IsSuccessStatusCode)
                {
                    var personRawString = await resp.Content.ReadAsStringAsync();
                    var identifiedPerson = JsonConvert.DeserializeObject<List<OxfordIdentifyResponse>>(personRawString);

                    var personId = identifiedPerson[0].candidates[0].personId;
                    return personId;
                }
                else
                {
                    throw new OxfordException($"Error when sedning Identify request: {await resp.Content.ReadAsStringAsync()}");
                }
            }

            return string.Empty;
        }

        public async static Task<bool> TrainModelAsync(string groupId)
        {
            var hc = CreateClient();
            var resp = await hc.PostAsync($"persongroups/{groupId}/train", null);

            if (!resp.IsSuccessStatusCode)
            {
                throw new OxfordException($"Error when sedning Train request: {await resp.Content.ReadAsStringAsync()}");
            }

            return true;
        }

        private static HttpClient CreateClient()
        {
            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", faceApiKey);
            hc.BaseAddress = new Uri("https://api.projectoxford.ai/face/v1.0/");

            return hc;
        }

    }
}
