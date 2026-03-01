using AgenticAI.Core.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AgenticAI.Core.Configuration
{
    /// <summary>
    /// Main configuration class for the automation framework
    /// </summary>
    public class FrameworkConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AutomationFramework AutomationFramework { get; set; } = AutomationFramework.Playwright;

        [JsonConverter(typeof(StringEnumConverter))]
        public BrowserType Browser { get; set; } = BrowserType.Chrome;

        [JsonConverter(typeof(StringEnumConverter))]
        public Enums.OperatingSystem OperatingSystem { get; set; } = Enums.OperatingSystem.Windows;

        [JsonConverter(typeof(StringEnumConverter))]
        public Enums.Environment Environment { get; set; } = Enums.Environment.QA;

        [JsonConverter(typeof(StringEnumConverter))]
        public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Sequential;

        // Base URL for testing - Optional, can be overridden by individual test scenarios
        public string BaseUrl { get; set; } = string.Empty;

        public bool Headless { get; set; } = false;
        public bool EnableVideo { get; set; } = true;
        public bool EnableScreenshots { get; set; } = true;
        public bool EnableTracing { get; set; } = false;
        public int MaxRetryCount { get; set; } = 2;
        public int TimeoutInSeconds { get; set; } = 30;
        public int ParallelWorkers { get; set; } = 4;
        public bool EnableSelfHealing { get; set; } = true;
        
        // Cross-browser parallel execution
        public bool CrossBrowserParallelExecution { get; set; } = false;
        
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public List<BrowserType> ParallelBrowsers { get; set; } = new List<BrowserType> 
        { 
            BrowserType.Chrome, 
            BrowserType.Firefox, 
            BrowserType.Edge 
        };
        public bool EnableAccessibilityTesting { get; set; } = false;
        public bool EnableVisualRegression { get; set; } = false;
        public bool EnablePerformanceMetrics { get; set; } = true;
        
        // Cloud execution settings
        public bool UseCloudExecution { get; set; } = false;
        public string CloudProvider { get; set; } = ""; // BrowserStack, SauceLabs
        public string CloudUsername { get; set; } = "";
        public string CloudAccessKey { get; set; } = "";

        // Paths
        public string ScreenshotPath { get; set; } = "TestResults/Screenshots";
        public string VideoPath { get; set; } = "TestResults/Videos";
        public string ReportPath { get; set; } = "TestResults/Reports";
        public string LogPath { get; set; } = "TestResults/Logs";
    }
}
