using AgenticAI.APIAutomation.Models;
using RestSharp;
using ApiContentType = AgenticAI.APIAutomation.Models.ContentType;

namespace AgenticAI.APIAutomation.Builders
{
    /// <summary>
    /// Fluent API request builder
    /// </summary>
    public class ApiRequestBuilder
    {
        private readonly ApiRequest _request;

        public ApiRequestBuilder()
        {
            _request = new ApiRequest();
        }

        public ApiRequestBuilder WithEndpoint(string endpoint)
        {
            _request.Endpoint = endpoint;
            return this;
        }

        public ApiRequestBuilder WithMethod(Method method)
        {
            _request.Method = method;
            return this;
        }

        public ApiRequestBuilder Get(string endpoint)
        {
            _request.Endpoint = endpoint;
            _request.Method = Method.Get;
            return this;
        }

        public ApiRequestBuilder Post(string endpoint)
        {
            _request.Endpoint = endpoint;
            _request.Method = Method.Post;
            return this;
        }

        public ApiRequestBuilder Put(string endpoint)
        {
            _request.Endpoint = endpoint;
            _request.Method = Method.Put;
            return this;
        }

        public ApiRequestBuilder Delete(string endpoint)
        {
            _request.Endpoint = endpoint;
            _request.Method = Method.Delete;
            return this;
        }

        public ApiRequestBuilder Patch(string endpoint)
        {
            _request.Endpoint = endpoint;
            _request.Method = Method.Patch;
            return this;
        }

        public ApiRequestBuilder WithHeader(string key, string value)
        {
            _request.Headers[key] = value;
            return this;
        }

        public ApiRequestBuilder WithHeaders(Dictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                _request.Headers[header.Key] = header.Value;
            }
            return this;
        }

        public ApiRequestBuilder WithBearerToken(string token)
        {
            _request.Headers["Authorization"] = $"Bearer {token}";
            return this;
        }

        public ApiRequestBuilder WithBasicAuth(string username, string password)
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
            _request.Headers["Authorization"] = $"Basic {credentials}";
            return this;
        }

        public ApiRequestBuilder WithApiKey(string key, string value)
        {
            _request.Headers[key] = value;
            return this;
        }

        public ApiRequestBuilder WithQueryParameter(string key, string value)
        {
            _request.QueryParameters[key] = value;
            return this;
        }

        public ApiRequestBuilder WithQueryParameters(Dictionary<string, string> parameters)
        {
            foreach (var param in parameters)
            {
                _request.QueryParameters[param.Key] = param.Value;
            }
            return this;
        }

        public ApiRequestBuilder WithJsonBody(object body)
        {
            _request.Body = body;
            _request.ContentType = ApiContentType.Json;
            return this;
        }

        public ApiRequestBuilder WithRawBody(string body, ApiContentType contentType = ApiContentType.Json)
        {
            _request.RawBody = body;
            _request.ContentType = contentType;
            return this;
        }

        public ApiRequestBuilder WithTimeout(int seconds)
        {
            _request.TimeoutSeconds = seconds;
            return this;
        }

        public ApiRequestBuilder WithContentType(ApiContentType contentType)
        {
            _request.ContentType = contentType;
            return this;
        }

        public ApiRequest Build()
        {
            return _request;
        }
    }
}
