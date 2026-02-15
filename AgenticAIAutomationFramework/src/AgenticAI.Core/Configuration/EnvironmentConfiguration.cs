using Newtonsoft.Json;

namespace AgenticAI.Core.Configuration
{
    /// <summary>
    /// Environment-specific configuration settings
    /// </summary>
    public class EnvironmentConfiguration
    {
        public string BaseUrl { get; set; } = "";
        public string ApiBaseUrl { get; set; } = "";
        public Dictionary<string, string> Credentials { get; set; } = new();
        public Dictionary<string, string> ApiKeys { get; set; } = new();
        public Dictionary<string, string> CustomSettings { get; set; } = new();
        public int ApiTimeoutSeconds { get; set; } = 30;
        public string DatabaseConnectionString { get; set; } = "";
    }
}
