using AgenticAI.Core.Enums;

namespace AgenticAI.Core.Models
{
    /// <summary>
    /// Represents a test case execution result
    /// </summary>
    public class TestCaseResult
    {
        public string TestCaseId { get; set; } = "";
        public string TestCaseName { get; set; } = "";
        public string Module { get; set; } = "";
        public TestStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<TestStepResult> Steps { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public int RetryCount { get; set; }
        public string? VideoPath { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public int TotalSteps => Steps.Count;
        public int PassedSteps => Steps.Count(s => s.Status == TestStatus.Passed);
        public int FailedSteps => Steps.Count(s => s.Status == TestStatus.Failed);
        public int SkippedSteps => Steps.Count(s => s.Status == TestStatus.Skipped);
    }
}
