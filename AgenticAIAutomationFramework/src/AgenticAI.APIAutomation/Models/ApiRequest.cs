using RestSharp;

namespace AgenticAI.APIAutomation.Models
{
    /// <summary>
    /// API request model
    /// </summary>
    public class ApiRequest
    {
        public string Endpoint { get; set; } = "";
        public Method Method { get; set; } = Method.Get;
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, string> QueryParameters { get; set; } = new();
        public object? Body { get; set; }
        public string? RawBody { get; set; }
        public ContentType ContentType { get; set; } = ContentType.Json;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public enum ContentType
    {
        Json,
        Xml,
        FormUrlEncoded,
        FormData,
        Text
    }
}
