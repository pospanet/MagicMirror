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
                var error = JsonConvert.DeserializeObject<OxfordError>(await response.Content.ReadAsStringAsync());
                throw new Exception(error.Message);
            }
        }

        public static async Task<string> CreatePersonInGroup(string groupId, string userId, string userName)
        {
            var content = new StringContent($"{{\"name\": \"{userName}\", \"userData\": \"{userId}\" }}", Encoding.UTF8, "application/json");

            var hc = CreateClient();
            var response = await hc.PostAsync($"persongroups/{groupId}/persons", content);

            if (response.IsSuccessStatusCode)
            {
                var person = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                return person["personId"].GetString();
            }
            else
            {
                var error = JsonConvert.DeserializeObject<OxfordError>(await response.Content.ReadAsStringAsync());
                throw new Exception(error.Message);
            }
        }

        public static async Task<string> AddPersonFaceAsync(string groupId, string personId, InMemoryRandomAccessStream photoStream, string userData = null)
        {
            photoStream.Seek(0);
            var content = new StreamContent(photoStream.AsStreamForRead());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var hc = CreateClient();
            var response = await hc.PostAsync($"persongroups/{groupId}/persons/{personId}/persistedFaces", content);

            // persistedFaceId
            if (response.IsSuccessStatusCode)
            {
                var face = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                return face["persistedFaceId"].ToString();
            }
            else
            {
                var error = JsonConvert.DeserializeObject<OxfordError>(await response.Content.ReadAsStringAsync());
                throw new Exception(error.Message);
            }
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
