using System.Net;

namespace AgenticAI.APIAutomation.Models
{
    /// <summary>
    /// API response model
    /// </summary>
    public class ApiResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; } = "";
        public Dictionary<string, string> Headers { get; set; } = new();
        public TimeSpan ResponseTime { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
