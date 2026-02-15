using AgenticAI.APIAutomation.Models;
using AgenticAI.Core.Configuration;
using AgenticAI.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Diagnostics;
using System.Net;
using ApiContentType = AgenticAI.APIAutomation.Models.ContentType;

namespace AgenticAI.APIAutomation.Client
{
    /// <summary>
    /// Robust API client with support for REST, authentication, and validation
    /// </summary>
    public class ApiClient
    {
        private readonly RestClient _client;
        private readonly FrameworkConfiguration _config;
        private readonly EnvironmentConfiguration _envConfig;
        private Dictionary<string, object> _contextData;

        public ApiClient(string? baseUrl = null)
        {
            _config = ConfigurationManager.Instance.FrameworkConfig;
            _envConfig = ConfigurationManager.Instance.CurrentEnvironmentConfig;
            
            var clientOptions = new RestClientOptions(baseUrl ?? _envConfig.ApiBaseUrl)
            {
                MaxTimeout = _envConfig.ApiTimeoutSeconds * 1000
            };

            _client = new RestClient(clientOptions);
            _contextData = new Dictionary<string, object>();
        }

        public async Task<ApiResponse> SendRequestAsync(ApiRequest apiRequest)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new ApiResponse();

            try
            {
                var request = BuildRequest(apiRequest);
                Logger.Info($"Sending {apiRequest.Method} request to: {apiRequest.Endpoint}");
                Logger.Debug($"Request Headers: {JsonConvert.SerializeObject(apiRequest.Headers)}");
                
                if (apiRequest.Body != null || !string.IsNullOrEmpty(apiRequest.RawBody))
                {
                    Logger.Debug($"Request Body: {apiRequest.RawBody ?? JsonConvert.SerializeObject(apiRequest.Body)}");
                }

                var restResponse = await _client.ExecuteAsync(request);
                stopwatch.Stop();

                response.StatusCode = restResponse.StatusCode;
                response.Content = restResponse.Content ?? "";
                response.ResponseTime = stopwatch.Elapsed;
                response.IsSuccess = restResponse.IsSuccessful;
                response.ErrorMessage = restResponse.ErrorMessage;

                if (restResponse.Headers != null)
                {
                    foreach (var header in restResponse.Headers)
                    {
                        response.Headers[header.Name!] = header.Value?.ToString() ?? "";
                    }
                }

                Logger.Info($"Response Status: {response.StatusCode} | Time: {response.ResponseTime.TotalMilliseconds}ms");
                Logger.Debug($"Response Body: {response.Content}");

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, $"API request failed: {apiRequest.Endpoint}");
                
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.IsSuccess = false;
                response.ErrorMessage = ex.Message;
                response.ResponseTime = stopwatch.Elapsed;
                
                return response;
            }
        }

        private RestRequest BuildRequest(ApiRequest apiRequest)
        {
            var request = new RestRequest(apiRequest.Endpoint, apiRequest.Method);

            // Add headers
            foreach (var header in apiRequest.Headers)
            {
                request.AddHeader(header.Key, header.Value);
            }

            // Add query parameters
            foreach (var param in apiRequest.QueryParameters)
            {
                request.AddQueryParameter(param.Key, param.Value);
            }

            // Add body
            if (!string.IsNullOrEmpty(apiRequest.RawBody))
            {
                var contentType = apiRequest.ContentType switch
                {
                    ApiContentType.Json => "application/json",
                    ApiContentType.Xml => "application/xml",
                    ApiContentType.Text => "text/plain",
                    _ => "application/json"
                };
                request.AddStringBody(apiRequest.RawBody, contentType);
            }
            else if (apiRequest.Body != null)
            {
                request.AddJsonBody(apiRequest.Body);
            }

            return request;
        }

        public T? ParseResponse<T>(ApiResponse response)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse response: {ex.Message}");
                throw;
            }
        }

        public JObject ParseJsonResponse(ApiResponse response)
        {
            try
            {
                return JObject.Parse(response.Content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse JSON response: {ex.Message}");
                throw;
            }
        }

        public void StoreContextData(string key, object value)
        {
            _contextData[key] = value;
            Logger.Debug($"Stored context data: {key} = {value}");
        }

        public T? GetContextData<T>(string key)
        {
            if (_contextData.ContainsKey(key))
            {
                return (T?)_contextData[key];
            }
            return default;
        }

        public void ClearContextData()
        {
            _contextData.Clear();
        }

        // Authentication helpers
        public void SetBearerToken(string token)
        {
            _client.AddDefaultHeader("Authorization", $"Bearer {token}");
            Logger.Info("Bearer token set for API client");
        }

        public void SetBasicAuth(string username, string password)
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
            _client.AddDefaultHeader("Authorization", $"Basic {credentials}");
            Logger.Info("Basic authentication set for API client");
        }

        public void SetApiKey(string key, string value, bool inHeader = true)
        {
            if (inHeader)
            {
                _client.AddDefaultHeader(key, value);
            }
            Logger.Info($"API key set: {key}");
        }
    }
}
