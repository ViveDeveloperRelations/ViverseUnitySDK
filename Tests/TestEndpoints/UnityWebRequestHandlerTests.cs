using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.TestEndpoints
{
    public class UnityWebRequestHandlerTests
    {
        private IWebRequest _webRequestHandler;

        [SetUp]
        public void Setup()
        {
            _webRequestHandler = new UnityWebRequestHandler();
        }

        [UnityTest]
        public IEnumerator TestGetRequest()
        {
            var requestParameters = new WebRequestParameters
            {
                Uri = new Uri("https://jsonplaceholder.typicode.com/posts/1"),
                Method = HttpMethod.Get
            };

            Task<WebRequestResponse> task = _webRequestHandler.Send(requestParameters);
            yield return new WaitUntil(() => task.IsCompleted);

            WebRequestResponse response = task.Result;

            Assert.AreEqual(200, response.StatusCode);
            Assert.IsNotNull(response.Response);
        }

        [UnityTest]
        public IEnumerator TestPostRequest()
        {
            var requestParameters = new WebRequestParameters
            {
                Uri = new Uri("https://jsonplaceholder.typicode.com/posts"),
                Method = HttpMethod.Post,
                Body = "{\"title\": \"foo\", \"body\": \"bar\", \"userId\": 1}",
                ContentType = "application/json"
            };

            Task<WebRequestResponse> task = _webRequestHandler.Send(requestParameters);
            yield return new WaitUntil(() => task.IsCompleted);

            WebRequestResponse response = task.Result;

            Assert.AreEqual(201, response.StatusCode);
            Assert.IsNotNull(response.Response);
        }

        [UnityTest]
        public IEnumerator TestGetRequestWithParameters()
        {
            var requestParameters = new WebRequestParameters
            {
                Uri = new Uri("https://jsonplaceholder.typicode.com/comments"),
                Method = HttpMethod.Get,
                GetParameters = new[]
                {
                    new KeyValuePair<string, string>("postId", "1")
                }
            };

            Task<WebRequestResponse> task = _webRequestHandler.Send(requestParameters);
            yield return new WaitUntil(() => task.IsCompleted);

            WebRequestResponse response = task.Result;

            Assert.AreEqual(200, response.StatusCode);
            Assert.IsNotNull(response.Response);
        }

        [UnityTest]
        public IEnumerator TestRequestWithHeaders()
        {
            var requestParameters = new WebRequestParameters
            {
                Uri = new Uri("https://jsonplaceholder.typicode.com/posts/1"),
                Method = HttpMethod.Get,
                Headers = new[]
                {
                    new KeyValuePair<string, string>("Accept", "application/json")
                }
            };

            Task<WebRequestResponse> task = _webRequestHandler.Send(requestParameters);
            yield return new WaitUntil(() => task.IsCompleted);

            WebRequestResponse response = task.Result;

            Assert.AreEqual(200, response.StatusCode);
            Assert.IsNotNull(response.Response);
        }
    }
}