using AgenticAI.APIAutomation.Models;
using AgenticAI.Core.Logging;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using System.Net;

namespace AgenticAI.APIAutomation.Validation
{
    /// <summary>
    /// API response validation utilities
    /// </summary>
    public class ApiValidator
    {
        public static void ValidateStatusCode(ApiResponse response, HttpStatusCode expectedStatus)
        {
            if (response.StatusCode != expectedStatus)
            {
                var message = $"Status code mismatch. Expected: {expectedStatus}, Actual: {response.StatusCode}";
                Logger.Error(message);
                throw new AssertionException(message);
            }
            Logger.Info($"Status code validation passed: {expectedStatus}");
        }

        public static void ValidateStatusCodeInRange(ApiResponse response, int minCode, int maxCode)
        {
            var statusCode = (int)response.StatusCode;
            if (statusCode < minCode || statusCode > maxCode)
            {
                var message = $"Status code {statusCode} is not in expected range [{minCode}-{maxCode}]";
                Logger.Error(message);
                throw new AssertionException(message);
            }
            Logger.Info($"Status code validation passed: {statusCode} in range [{minCode}-{maxCode}]");
        }

        public static void ValidateResponseTime(ApiResponse response, int maxMilliseconds)
        {
            if (response.ResponseTime.TotalMilliseconds > maxMilliseconds)
            {
                var message = $"Response time {response.ResponseTime.TotalMilliseconds}ms exceeds maximum {maxMilliseconds}ms";
                Logger.Warning(message);
                throw new AssertionException(message);
            }
            Logger.Info($"Response time validation passed: {response.ResponseTime.TotalMilliseconds}ms");
        }

        public static async Task ValidateJsonSchemaAsync(ApiResponse response, string schemaJson)
        {
            try
            {
                var schema = await JsonSchema.FromJsonAsync(schemaJson);
                var errors = schema.Validate(response.Content);
                
                if (errors.Count > 0)
                {
                    var message = $"JSON schema validation failed: {string.Join(", ", errors.Select(e => e.ToString()))}";
                    Logger.Error(message);
                    throw new AssertionException(message);
                }
                
                Logger.Info("JSON schema validation passed");
            }
            catch (Exception ex) when (ex is not AssertionException)
            {
                Logger.Error($"Schema validation error: {ex.Message}");
                throw;
            }
        }

        public static void ValidateJsonPath(ApiResponse response, string jsonPath, object expectedValue)
        {
            var json = JObject.Parse(response.Content);
            var token = json.SelectToken(jsonPath);
            
            if (token == null)
            {
                var message = $"JSON path not found: {jsonPath}";
                Logger.Error(message);
                throw new AssertionException(message);
            }

            var actualValue = token.ToString();
            var expectedValueStr = expectedValue.ToString();
            
            if (actualValue != expectedValueStr)
            {
                var message = $"Value mismatch at {jsonPath}. Expected: {expectedValueStr}, Actual: {actualValue}";
                Logger.Error(message);
                throw new AssertionException(message);
            }
            
            Logger.Info($"JSON path validation passed: {jsonPath} = {expectedValueStr}");
        }

        public static void ValidateHeader(ApiResponse response, string headerName, string expectedValue)
        {
            if (!response.Headers.ContainsKey(headerName))
            {
                var message = $"Header not found: {headerName}";
                Logger.Error(message);
                throw new AssertionException(message);
            }

            if (response.Headers[headerName] != expectedValue)
            {
                var message = $"Header value mismatch. Expected: {expectedValue}, Actual: {response.Headers[headerName]}";
                Logger.Error(message);
                throw new AssertionException(message);
            }
            
            Logger.Info($"Header validation passed: {headerName}");
        }

        public static void ValidateContainsText(ApiResponse response, string text)
        {
            if (!response.Content.Contains(text))
            {
                var message = $"Response does not contain expected text: {text}";
                Logger.Error(message);
                throw new AssertionException(message);
            }
            Logger.Info($"Text validation passed: '{text}' found in response");
        }
    }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }
}
