using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace Codenizer.HttpClient.Testable
{
    /// <summary>
    /// Implements a builder to configure a response
    /// </summary>
    public class RequestBuilder : IRequestBuilder, IResponseBuilder
    {
        private RequestBuilder _root;

        /// <summary>
        /// Creates a new instance that matches the HTTP method, path and query string and content types
        /// </summary>
        /// <param name="method">The HTTP method to match</param>
        /// <param name="pathAndQuery">The path and query string to match</param>
        /// <param name="contentType">The MIME type to match, <c>null</c> matches any MIME type</param>
        /// <param name="root">Optional. The root <see cref="RequestBuilder"/> that this instance belongs to, this is used for requests that have a sequence of responses.</param>
        internal RequestBuilder(HttpMethod method, string pathAndQuery, string contentType, RequestBuilder root = null)
        {
            _root = root;
            Method = method;
            PathAndQuery = pathAndQuery;

            if (pathAndQuery.Contains("?"))
            {
                var parts = pathAndQuery.Split('?');

                PathAndQuery = parts[0];

                QueryParameters = parts[1]
                    .Split('&')
                    .Select(p => p.Split('='))
                    .ToDictionary(p => p[0], p => p.Length == 2 ? p[1] : null);
            }

            ContentType = contentType;
        }

        public Dictionary<string, string> QueryParameters { get; } = new Dictionary<string, string>();

        public string PathAndQuery { get; }
        public string ContentType { get; }
        public HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.InternalServerError;
        public HttpMethod Method { get; }
        public string Data { get; private set; }
        public string MediaType { get; private set; }
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public TimeSpan Duration { get; private set; } = TimeSpan.Zero;
        public Action<HttpRequestMessage> ActionWhenCalled { get; private set; }
        public List<string> Cookies { get; } = new List<string>();
        public List<QueryStringAssertion> QueryStringAssertions { get; } = new List<QueryStringAssertion>();
        public List<RequestBuilder> ResponseSequence { get; } = new List<RequestBuilder>();
        public int ResponseSequenceCounter { get; set; }

        /// <summary>
        /// Respond with the given HTTP status code
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <returns>The current <see cref="IRequestBuilder"/> instance</returns>
        public IResponseBuilder With(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
            return this;
        }

        public IRequestBuilderForQueryString ForQueryStringParameter(string key)
        {
            return new RequestBuilderForQueryString(this, key);
        }

        /// <summary>
        /// Add a sequence of responses for this request
        /// </summary>
        /// <remarks>Using WithSequence allows you to have multiple calls to the same endpoint with different responses.</remarks>
        /// <param name="builder">A <see cref="IRequestBuilder"/> instance used to configure the response for this step in the sequence of responses</param>
        /// <returns>A <see cref="IRequestBuilder"/> instance</returns>
        public IRequestBuilder WithSequence(Action<IRequestBuilder> builder)
        {
            var requestBuilder = new RequestBuilder(Method, PathAndQuery, ContentType, _root ?? this);
            
            builder(requestBuilder);

            var root = _root ?? this;

            if (root != null)
            {
                root.ResponseSequence.Add(requestBuilder);
            }

            return requestBuilder;
        }

        /// <summary>
        /// Respond with the given content
        /// </summary>
        /// <param name="mimeType">The MIME type of the response</param>
        /// <param name="data">The string representation of the response</param>
        /// <returns>The current <see cref="IRequestBuilder"/> instance</returns>
        public IResponseBuilder AndContent(string mimeType, string data)
        {
            MediaType = mimeType;
            Data = data;

            return this;
        }

        /// <summary>
        /// Add the given HTTP headers to the response
        /// </summary>
        /// <remarks>If the header already exists the supplied value will be added to it separated by a comma</remarks>
        /// <param name="headers">A dictionary containing the headers to add</param>
        /// <returns>The current <see cref="IRequestBuilder"/> instance</returns>
        public IResponseBuilder AndHeaders(Dictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                if (Headers.ContainsKey(header.Key))
                {
                    Headers[header.Key] += "," + header.Value;
                }
                else
                {
                    Headers.Add(header.Key, header.Value);
                }
            }

            return this;
        }

        /// <summary>
        /// Delay the response
        /// </summary>
        /// <param name="time">The time to delay</param>
        /// <returns>The current <see cref="IRequestBuilder"/> instance</returns>
        public IResponseBuilder Taking(TimeSpan time)
        {
            Duration = time;

            return this;
        }

        /// <summary>
        /// Invoke an action when this request is made
        /// </summary>
        /// <param name="action">The action to invoke</param>
        /// <returns>The current <see cref="IRequestBuilder"/> instance</returns>
        public IResponseBuilder WhenCalled(Action<HttpRequestMessage> action)
        {
            ActionWhenCalled = action;

            return this;
        }

        public IResponseBuilder AndCookie(string name,
            string value,
            DateTime? expiresAt = null,
            string sameSite = null,
            bool? secure = null,
            string path = null,
            string domain = null,
            int? maxAge = null)
        {
            var parameters = new List<string>();

            if (expiresAt != null)
            {
                parameters.Add($"Expires={expiresAt:R}");
            }

            if (!string.IsNullOrEmpty(sameSite))
            {
                parameters.Add($"SameSite={sameSite}");
            }

            if (secure.GetValueOrDefault(false))
            {
                parameters.Add("Secure");
            }

            if (!string.IsNullOrEmpty(path))
            {
                parameters.Add($"Path={path}");
            }

            if (!string.IsNullOrEmpty(domain))
            {
                parameters.Add($"Domain={domain}");
            }

            if (maxAge != null)
            {
                parameters.Add($"MaxAge={maxAge.Value}");
            }

            var cookieString = $"{name}={value}";

            if (parameters.Any())
            {
                cookieString += "; " + string.Join(";", parameters);
            }

            Cookies.Add(cookieString);

            return this;
        }
    }

    public interface IRequestBuilderForQueryString
    {
        IRequestBuilder WithAnyValue();
        IRequestBuilder WithValue(string value);
    }

    public interface IRequestBuilder
    {
        IResponseBuilder With(HttpStatusCode statusCode);
        IRequestBuilderForQueryString ForQueryStringParameter(string key);
        IRequestBuilder WithSequence(Action<IRequestBuilder> builder);
    }

    public interface IResponseBuilder
    {
        IResponseBuilder AndContent(string mimeType, string data);
        IResponseBuilder AndHeaders(Dictionary<string, string> headers);
        IResponseBuilder Taking(TimeSpan time);
        IResponseBuilder WhenCalled(Action<HttpRequestMessage> action);
        IResponseBuilder AndCookie(string name, string value, DateTime? expiresAt = null, string sameSite = null,
            bool? secure = null, string path = null, string domain = null, int? maxAge = null);
    }
}