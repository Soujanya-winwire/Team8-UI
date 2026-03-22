using AgenticAI.Core.Enums;
using AgenticAI.Core.Logging;
using System.Collections.Concurrent;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Detects and tracks flaky tests - tests that fail intermittently
    /// </summary>
    public class FlakinessDetector
    {
        private ConcurrentDictionary<string, TestExecutionHistory> _executionHistory = new();
        private static FlakinessDetector? _instance;
        private static readonly object _lockObject = new();

        public class TestExecutionHistory
        {
            public string TestName { get; set; } = "";
            public List<TestExecution> Executions { get; set; } = new();
            public int TotalRuns => Executions.Count;
            public int PassedRuns => Executions.Count(e => e.Status == TestStatus.Passed);
            public int FailedRuns => Executions.Count(e => e.Status == TestStatus.Failed);
            public double FailureRate => TotalRuns > 0 ? (double)FailedRuns / TotalRuns : 0;
        }

        public class TestExecution
        {
            public DateTime ExecutionTime { get; set; }
            public TestStatus Status { get; set; }
            public long DurationMs { get; set; }
            public string? FailureMessage { get; set; }
            public string? StackTrace { get; set; }
        }

        public class FlakinessReport
        {
            public string TestName { get; set; } = "";
            public int TotalRuns { get; set; }
            public int PassCount { get; set; }
            public int FailCount { get; set; }
            public double FailureRate { get; set; }
            public bool IsFlaky { get; set; }
            public string FlakinessSeverity { get; set; } = "None"; // None, Low, Medium, High
            public List<string> FailureMessages { get; set; } = new();
            public double AverageDurationMs { get; set; }
            public double StdDevDurationMs { get; set; }
        }

        public static FlakinessDetector Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new FlakinessDetector();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Record a test execution
        /// </summary>
        public void RecordExecution(string testName, TestStatus status, long durationMs, string? failureMessage = null, string? stackTrace = null)
        {
            var execution = new TestExecution
            {
                ExecutionTime = DateTime.Now,
                Status = status,
                DurationMs = durationMs,
                FailureMessage = failureMessage,
                StackTrace = stackTrace
            };

            _executionHistory.AddOrUpdate(testName,
                new TestExecutionHistory { TestName = testName, Executions = new List<TestExecution> { execution } },
                (key, existing) =>
                {
                    existing.Executions.Add(execution);
                    return existing;
                });

            Logger.Debug($"Execution recorded for test: {testName} - Status: {status}");
        }

        /// <summary>
        /// Check if a test is flaky based on execution history
        /// </summary>
        public bool IsTestFlaky(string testName, int minRunsForDetection = 5, double flakinessThreshold = 0.3)
        {
            if (!_executionHistory.TryGetValue(testName, out var history))
            {
                return false;
            }

            if (history.TotalRuns < minRunsForDetection)
            {
                return false;
            }

            // Test is flaky if failure rate is between threshold and 99%
            return history.FailureRate >= flakinessThreshold && history.FailureRate < 0.99;
        }

        /// <summary>
        /// Get flakiness report for a test
        /// </summary>
        public FlakinessReport GetFlakinessReport(string testName)
        {
            if (!_executionHistory.TryGetValue(testName, out var history))
            {
                return new FlakinessReport { TestName = testName };
            }

            var durations = history.Executions.Select(e => (double)e.DurationMs).ToList();
            var avgDuration = durations.Count > 0 ? durations.Average() : 0;
            var stdDev = durations.Count > 1 ? CalculateStandardDeviation(durations) : 0;

            var failureMessages = history.Executions
                .Where(e => e.Status == TestStatus.Failed && !string.IsNullOrEmpty(e.FailureMessage))
                .Select(e => e.FailureMessage)
                .Distinct()
                .ToList();

            var failureRate = history.FailureRate;
            var severity = failureRate switch
            {
                >= 0.7 => "High",
                >= 0.4 => "Medium",
                >= 0.2 => "Low",
                _ => "None"
            };

            return new FlakinessReport
            {
                TestName = testName,
                TotalRuns = history.TotalRuns,
                PassCount = history.PassedRuns,
                FailCount = history.FailedRuns,
                FailureRate = failureRate,
                IsFlaky = IsTestFlaky(testName),
                FlakinessSeverity = severity,
                FailureMessages = failureMessages!,
                AverageDurationMs = avgDuration,
                StdDevDurationMs = stdDev
            };
        }

        /// <summary>
        /// Get all flaky tests
        /// </summary>
        public List<FlakinessReport> GetFlakyTests(double flakinessThreshold = 0.3)
        {
            var reports = new List<FlakinessReport>();

            foreach (var testName in _executionHistory.Keys)
            {
                var report = GetFlakinessReport(testName);
                if (report.IsFlaky)
                {
                    reports.Add(report);
                }
            }

            return reports.OrderByDescending(r => r.FailureRate).ToList();
        }

        /// <summary>
        /// Get execution history for a test
        /// </summary>
        public TestExecutionHistory? GetExecutionHistory(string testName)
        {
            _executionHistory.TryGetValue(testName, out var history);
            return history;
        }

        /// <summary>
        /// Generate a flakiness report for all tests
        /// </summary>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            var flakyTests = GetFlakyTests();

            report.AppendLine("=== Test Flakiness Report ===");
            report.AppendLine($"Total Tests Tracked: {_executionHistory.Count}");
            report.AppendLine($"Flaky Tests: {flakyTests.Count}");
            report.AppendLine();

            if (flakyTests.Count == 0)
            {
                report.AppendLine("No flaky tests detected.");
                return report.ToString();
            }

            report.AppendLine("Flaky Tests (by severity):");
            var byServerity = flakyTests.GroupBy(t => t.FlakinessSeverity).OrderByDescending(g => g.Key);
            foreach (var group in byServerity)
            {
                report.AppendLine($"\n{group.Key} Severity ({group.Count()} tests):");
                foreach (var test in group)
                {
                    report.AppendLine($"  - {test.TestName}");
                    report.AppendLine($"    Failure Rate: {test.FailureRate:P1}");
                    report.AppendLine($"    Runs: {test.TotalRuns} (P: {test.PassCount}, F: {test.FailCount})");
                    report.AppendLine($"    Avg Duration: {test.AverageDurationMs:F2}ms ± {test.StdDevDurationMs:F2}ms");
                    if (test.FailureMessages.Count > 0)
                    {
                        report.AppendLine($"    Common Failures:");
                        foreach (var msg in test.FailureMessages.Take(2))
                        {
                            report.AppendLine($"      - {msg}");
                        }
                    }
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Clear all execution history
        /// </summary>
        public void Clear()
        {
            _executionHistory.Clear();
        }

        /// <summary>
        /// Calculate standard deviation of durations
        /// </summary>
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;

            var mean = values.Average();
            var sumOfSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquaredDifferences / (values.Count - 1));
        }
    }
}
