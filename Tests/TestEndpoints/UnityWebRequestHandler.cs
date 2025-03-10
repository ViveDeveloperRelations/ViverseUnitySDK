using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tests.TestEndpoints
{
    public class UnityWebRequestHandler : IWebRequest
    {
        public async Task<WebRequestResponse> Send(WebRequestParameters request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            UnityWebRequest unityWebRequest = CreateUnityWebRequest(request);

            await unityWebRequest.SendWebRequest();

            return new WebRequestResponse
            {
                StatusCode = (int)unityWebRequest.responseCode,
                Response = unityWebRequest.downloadHandler?.text
            };
        }

        private UnityWebRequest CreateUnityWebRequest(WebRequestParameters request)
        {
            UnityWebRequest unityWebRequest;

            if (request.Method == HttpMethod.Get)
            {
                string url = AddGetParametersToUrl(request.Uri.ToString(), request.GetParameters);
                unityWebRequest = UnityWebRequest.Get(url);
            }
            else if (request.Method == HttpMethod.Post)
            {
                if (request.PostParameters != null && request.PostParameters.Length > 0)
                {
                    // Convert KeyValuePair[] to Dictionary<string, string>
                    var formData = new Dictionary<string, string>();
                    foreach (var param in request.PostParameters)
                    {
                        formData[param.Key] = param.Value;
                    }
                    unityWebRequest = UnityWebRequest.Post(request.Uri.ToString(), formData);
                }
                else if (!string.IsNullOrEmpty(request.Body))
                {
                    unityWebRequest = new UnityWebRequest(request.Uri.ToString(), "POST");
                    unityWebRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(request.Body));
                    unityWebRequest.downloadHandler = new DownloadHandlerBuffer();
                    unityWebRequest.SetRequestHeader("Content-Type", request.ContentType);
                }
                else
                {
                    throw new ArgumentException("Either PostParameters or Body must be provided for POST requests.");
                }
            }
            else
            {
                throw new NotSupportedException($"HTTP method {request.Method} is not supported.");
            }

            AddHeaders(unityWebRequest, request.Headers);

            return unityWebRequest;
        }

        private string AddGetParametersToUrl(string url, KeyValuePair<string, string>[] getParameters)
        {
            if (getParameters == null || getParameters.Length == 0)
                return url;

            var uriBuilder = new UriBuilder(url);
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (var param in getParameters)
            {
                query[param.Key] = param.Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }

        private void AddHeaders(UnityWebRequest unityWebRequest, KeyValuePair<string, string>[] headers)
        {
            if (headers == null)
                return;

            foreach (var header in headers)
            {
                unityWebRequest.SetRequestHeader(header.Key, header.Value);
            }
        }
    }
}