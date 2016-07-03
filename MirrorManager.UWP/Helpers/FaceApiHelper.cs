using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.IO;

namespace MirrorManager.UWP.Helpers
{
    public class FaceApiHelper
    {
        static string faceApiKey = App.Current.Resources["FaceApiKey"].ToString();

        public static async Task<bool> AddPersonFaceAsync(string groupId, string personId, InMemoryRandomAccessStream photoStream, string userData = null)
        {
            HttpClient hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", faceApiKey);
            hc.BaseAddress = new Uri("https://api.projectoxford.ai/face/v1.0/");

            photoStream.Seek(0);
            var content = new StreamContent(photoStream.AsStreamForRead());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await hc.PostAsync($"persongroups/{groupId}/persons/{personId}/persistedFaces", content);
            //var response = await hc.PostAsync("https://api.projectoxford.ai/face/v1.0/persongroups/userfaces/persons/7bc99d41-853a-4c15-b6dc-baf95bb06bd8/persistedFaces", content);

            return response.IsSuccessStatusCode;
        }

    }
}
