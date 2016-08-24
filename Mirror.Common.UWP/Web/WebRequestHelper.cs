using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Pospa.Mirror.Common.Web
{
    public class WebRequestHelper : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly HttpClient _httpClient;
        private readonly string _mediaTypeForApi;

        public WebRequestHelper(Uri baseAddress, int defaultHttpTimeout = 120,
            string mediaTypeForApi = "application/json")
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _mediaTypeForApi = mediaTypeForApi;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = baseAddress;
            _httpClient.Timeout = TimeSpan.FromSeconds(defaultHttpTimeout);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaTypeForApi));
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            CancelPendingReguest();
            _httpClient.Dispose();
        }

        public async Task<HttpResponseMessage> SendWebRequestAsync(Uri endPoint, HttpMethod httpMethod,
            object content = null,
            string authorization = null, IDictionary<string, string> headers = null)
        {
            using (HttpRequestMessage message = new HttpRequestMessage(httpMethod, endPoint))
            {
                if (authorization != null)
                {
                    message.Headers.Add(HttpRequestHeader.Authorization.ToString(), authorization);
                }
                if (content != null)
                {
                    message.Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8,
                        _mediaTypeForApi);
                }
                message.Headers.ConnectionClose = true;
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> pair in headers)
                    {
                        message.Headers.Add(pair.Key, pair.Value);
                    }
                }

                HttpResponseMessage responseMessage =
                    await _httpClient.SendAsync(message, _cancellationTokenSource.Token);
                if (responseMessage == null)
                {
                    throw new InvalidOperationException(string.Format(
                        "The response message was null when executing operation {0}.", httpMethod));
                }
                return responseMessage.IsSuccessStatusCode ? responseMessage : null;
            }
        }

        public async Task<T> SendWebRequestAsync<T>(Uri endPoint, HttpMethod httpMethod, object content = null,
            string authorization = null, IDictionary<string, string> headers = null)
        {
            HttpResponseMessage responseMessage =
                await SendWebRequestAsync(endPoint, httpMethod, content, authorization, headers);
            if (responseMessage == null) return default(T);
            string serializedDevice = await responseMessage.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(serializedDevice);
        }

        public async Task<JObject> SendWebRequestDynamicAsync(Uri endPoint, HttpMethod httpMethod,
            object content = null, string authorization = null, IDictionary<string, string> headers = null)
        {
            HttpResponseMessage responseMessage =
                await SendWebRequestAsync(endPoint, httpMethod, content, authorization, headers);
            if (responseMessage == null) return new JObject();
            string serializedDevice = await responseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(serializedDevice);
        }


        public void CancelPendingReguest()
        {
            CancelPendingReguest(TimeSpan.Zero);
        }

        public void CancelPendingReguest(TimeSpan waitTime)
        {
            _cancellationTokenSource.CancelAfter(waitTime);
        }
    }
}