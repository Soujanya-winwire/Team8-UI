using AgenticAI.Core.Enums;

namespace AgenticAI.Core.Models
{
    /// <summary>
    /// Represents a test step execution result
    /// </summary>
    public class TestStepResult
    {
        public string StepName { get; set; } = "";
        public string Description { get; set; } = "";
        public TestStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public string? ScreenshotPath { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
