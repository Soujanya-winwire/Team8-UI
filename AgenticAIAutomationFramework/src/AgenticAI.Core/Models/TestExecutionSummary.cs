using AgenticAI.Core.Enums;

namespace AgenticAI.Core.Models
{
    /// <summary>
    /// Represents overall test execution summary
    /// </summary>
    public class TestExecutionSummary
    {
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public BrowserType Browser { get; set; }
        public Enums.OperatingSystem OperatingSystem { get; set; }
        public Enums.Environment Environment { get; set; }
        public ExecutionMode ExecutionMode { get; set; }
        public List<TestCaseResult> TestResults { get; set; } = new();
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();

        public int TotalTests => TestResults.Count;
        public int PassedTests => TestResults.Count(t => t.Status == TestStatus.Passed);
        public int FailedTests => TestResults.Count(t => t.Status == TestStatus.Failed);
        public int SkippedTests => TestResults.Count(t => t.Status == TestStatus.Skipped);
        public double PassPercentage => TotalTests > 0 ? (PassedTests * 100.0) / TotalTests : 0;
        public int FlakyTests => TestResults.Count(t => t.RetryCount > 0);
    }
}
