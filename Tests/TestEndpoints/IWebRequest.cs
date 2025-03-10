using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tests.TestEndpoints
{
    public class WebRequestResponse
    {
        public int StatusCode;
        public string Response;
    }

    public class WebRequestParameters
    {
        public Uri Uri;
        public KeyValuePair<string, string>[] Headers;
        public KeyValuePair<string, string>[] GetParameters;
        public KeyValuePair<string, string>[] PostParameters;
        public string Body;
        public string ContentType = "application/json";
        public HttpMethod Method;
    }

    public interface IWebRequest
    {
        Task<WebRequestResponse> Send(WebRequestParameters request);
    }
    
}
