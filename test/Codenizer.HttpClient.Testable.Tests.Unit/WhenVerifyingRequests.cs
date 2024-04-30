using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace Codenizer.HttpClient.Testable.Tests.Unit
{
    public class WhenVerifyingRequests
    {
        private readonly TestableMessageHandler _handler;
        private readonly System.Net.Http.HttpClient _client;

        public WhenVerifyingRequests()
        {
            _handler = new TestableMessageHandler();
            _client = new System.Net.Http.HttpClient(_handler)
            {
                BaseAddress = new System.Uri("https://tempuri.org/")
            };

            _handler
                .RespondTo(HttpMethod.Get, "/api/info")
                .With(HttpStatusCode.OK);

            _handler
                .RespondTo(HttpMethod.Post, "/api/info")
                .With(HttpStatusCode.OK);
        }

        [Fact]
        public async void GivenRequest_RequestIsCaptured()
        {
            await _client.GetAsync("/api/info");

            _handler
                .Requests
                .Should()
                .Contain(req => req.RequestUri.PathAndQuery == "/api/info");
        }

        [Fact]
        public async void GivenRequestWithHeaders_RequestHeadersCaptured()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/info");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Test-Header", "Value");
            
            await _client.SendAsync(request);

            _handler
                .Requests
                .Single()
                .Headers
                .Should()
                .Contain(h => h.Key == "Test-Header")
                .And
                .Contain(h => h.Key == "Accept");
        }

        [Fact]
        public async void GivenRequestWithStringContent_CapturedRequestContainsContent()
        {
            await _client.PostAsync("/api/info", new StringContent("test data"));

            _handler
                .Requests
                .Single()
                .GetData()
                .Should()
                .Be("test data");
        }

        [Fact]
        public async void GivenRequestWithByteArrayContent_CapturedRequestContainsContent()
        {
            var content = new byte[] { 0x1, 0x2, 0x3, 0x4 };

            await _client.PostAsync("/api/info", new ByteArrayContent(content));

            _handler
                .Requests
                .Single()
                .GetData()
                .Should()
                .BeEquivalentTo(content);
        }

        [Fact]
        public async void GivenRequestMessageIsDisposed_ReadingContentSucceeds()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://tempuri.org/api/call"))
                   {
                       Content = new StringContent("test")
                   })
            {
                await _client.SendAsync(request);
            }

            var action = () => _handler
                .Requests
                .Single()
                .Content
                .ReadAsStringAsync();

            await action
                .Should()
                .NotThrowAsync("the request should be a copy and not the disposed original request");
        }

        [Fact]
        public async void GivenRequestWithFormContent_CapturedRequestContainsContent()
        {
            var formValues = new[]
            {
                new KeyValuePair<string, string>("field1", "val1"),
                new KeyValuePair<string, string>("field2", "val2")
            };

            await _client.PostAsync("/api/info", new FormUrlEncodedContent(formValues));

            _handler
                .Requests
                .Single()
                .Content
                .Should()
                .BeOfType<FormUrlEncodedContent>();
        }

        [Fact]
        public async void GivenRequestWithFormContent_CapturedFormContentMatchesRequest()
        {
            var formUrlEncodedContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("field1", "val1"),
                new KeyValuePair<string, string>("field2", "val2")
            });

            await _client.PostAsync("/api/info", formUrlEncodedContent);

            var formValuesOnCapturedRequest = await _handler
                .Requests
                .Single()
                .Content
                .As<FormUrlEncodedContent>()
                .ReadAsStringAsync();

            formValuesOnCapturedRequest
                .Should()
                .Be((await formUrlEncodedContent.ReadAsStringAsync()));
        }

        [Fact]
        public async void GivenRequestWithFormContent_AllOriginalHeadersExistOnTheCapturedRequest()
        {
            var formUrlEncodedContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("field1", "val1"),
                new KeyValuePair<string, string>("field2", "val2")
            });

            // Set some additional content headers to verify
            formUrlEncodedContent.Headers.LastModified = DateTimeOffset.UtcNow;
            formUrlEncodedContent.Headers.Expires = DateTimeOffset.UtcNow;

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/info")
            {
                Content = formUrlEncodedContent
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.8));

            await _client.SendAsync(request);

            var capturedRequest = _handler.Requests.Single();
            
            // Using ToString() here because what's sent over the wire is the string representation
            // of the DateTimeOffset and not a strictly serialized representation.
            capturedRequest.Content.Headers.LastModified.ToString().Should().Be(formUrlEncodedContent.Headers.LastModified.ToString());
            capturedRequest.Content.Headers.Expires.ToString().Should().Be(formUrlEncodedContent.Headers.Expires.ToString());
            capturedRequest.Headers.Accept.Should().BeEquivalentTo(request.Headers.Accept);
        }
    }
}