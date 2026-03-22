using AgenticAI.Core.Models;
using AgenticAI.Core.Logging;
using System.Text.Json;

namespace AgenticAI.Core.Reporting
{
    /// <summary>
    /// JSON format reporter for test results
    /// </summary>
    public class JsonReporter : IReporter
    {
        private string _reportPath = "";
        private string _reportDirectory = "";
        private JsonReportData _reportData = new();

        public class JsonReportData
        {
            public DateTime ExecutionStartTime { get; set; }
            public DateTime ExecutionEndTime { get; set; }
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public double ExecutionTimeSeconds { get; set; }
            public List<TestResultJson> Tests { get; set; } = new();
            public Dictionary<string, object> Summary { get; set; } = new();
        }

        public class TestResultJson
        {
            public string TestCaseId { get; set; } = "";
            public string TestCaseName { get; set; } = "";
            public string Module { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public long DurationMs { get; set; }
            public List<string> Tags { get; set; } = new();
            public string? ErrorMessage { get; set; }
            public List<StepResultJson> Steps { get; set; } = new();
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        public class StepResultJson
        {
            public string StepName { get; set; } = "";
            public string Status { get; set; } = "";
            public string? Message { get; set; }
            public long DurationMs { get; set; }
            public Dictionary<string, object> Details { get; set; } = new();
        }

        public JsonReporter(string reportPath)
        {
            _reportDirectory = Path.GetDirectoryName(reportPath) ?? "";
            if (string.IsNullOrEmpty(_reportDirectory))
            {
                _reportDirectory = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(_reportDirectory);
            _reportPath = Path.Combine(_reportDirectory, "TestResults.json");
        }

        public void StartExecution(TestExecutionSummary summary)
        {
            _reportData.ExecutionStartTime = DateTime.Now;
            Logger.Info($"JSON Report: Starting execution - {_reportData.ExecutionStartTime}");
        }

        public void StartTest(TestCaseResult testCase)
        {
            Logger.Debug($"JSON Report: Starting test - {testCase.TestCaseName}");
        }

        public void LogStep(TestStepResult step)
        {
            // Steps will be logged when test ends
        }

        public void EndTest(TestCaseResult testCase)
        {
            var testJson = new TestResultJson
            {
                TestCaseId = testCase.TestCaseId,
                TestCaseName = testCase.TestCaseName,
                Module = testCase.Module,
                Status = testCase.Status.ToString(),
                StartTime = testCase.StartTime,
                EndTime = testCase.EndTime,
                DurationMs = (long)testCase.Duration.TotalMilliseconds,
                Tags = testCase.Tags,
                ErrorMessage = testCase.ErrorMessage,
                Metadata = testCase.Metadata
            };

            // Add steps
            foreach (var step in testCase.Steps)
            {
                testJson.Steps.Add(new StepResultJson
                {
                    StepName = step.StepName,
                    Status = step.Status.ToString(),
                    Message = step.Description ?? step.ErrorMessage,
                    DurationMs = (long)step.Duration.TotalMilliseconds
                });
            }

            _reportData.Tests.Add(testJson);

            // Update counts
            if (testCase.Status == Enums.TestStatus.Passed) _reportData.PassedTests++;
            else if (testCase.Status == Enums.TestStatus.Failed) _reportData.FailedTests++;
            else if (testCase.Status == Enums.TestStatus.Skipped) _reportData.SkippedTests++;

            _reportData.TotalTests++;
            Logger.Debug($"JSON Report: Test ended - {testCase.TestCaseName} ({testCase.Status})");
        }

        public void EndExecution(TestExecutionSummary summary)
        {
            _reportData.ExecutionEndTime = DateTime.Now;
            _reportData.ExecutionTimeSeconds = (summary.EndTime - summary.StartTime).TotalSeconds;

            _reportData.Summary = new Dictionary<string, object>
            {
                { "TotalTests", _reportData.TotalTests },
                { "PassedTests", _reportData.PassedTests },
                { "FailedTests", _reportData.FailedTests },
                { "SkippedTests", _reportData.SkippedTests },
                { "SuccessRate", _reportData.TotalTests > 0 ? (_reportData.PassedTests * 100.0 / _reportData.TotalTests) : 0 },
                { "ExecutionTimeSeconds", _reportData.ExecutionTimeSeconds },
                { "ExecutionStartTime", _reportData.ExecutionStartTime },
                { "ExecutionEndTime", _reportData.ExecutionEndTime }
            };

            Logger.Info($"JSON Report: Execution ended - {_reportData.ExecutionEndTime}");
        }

        public void GenerateReport()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_reportData, options);
            File.WriteAllText(_reportPath, json);
            Logger.Info($"JSON report generated: {_reportPath}");
        }

        public string GetReportPath()
        {
            return _reportPath;
        }
    }

    /// <summary>
    /// XML format reporter for test results
    /// </summary>
    public class XmlReporter : IReporter
    {
        private string _reportPath = "";
        private string _reportDirectory = "";
        private List<TestCaseResult> _testResults = new();
        private DateTime _executionStartTime;
        private DateTime _executionEndTime;

        public XmlReporter(string reportPath)
        {
            _reportDirectory = Path.GetDirectoryName(reportPath) ?? "";
            if (string.IsNullOrEmpty(_reportDirectory))
            {
                _reportDirectory = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(_reportDirectory);
            _reportPath = Path.Combine(_reportDirectory, "TestResults.xml");
        }

        public void StartExecution(TestExecutionSummary summary)
        {
            _executionStartTime = DateTime.Now;
            Logger.Info($"XML Report: Starting execution - {_executionStartTime}");
        }

        public void StartTest(TestCaseResult testCase)
        {
            Logger.Debug($"XML Report: Starting test - {testCase.TestCaseName}");
        }

        public void LogStep(TestStepResult step)
        {
            // Steps will be logged when test ends
        }

        public void EndTest(TestCaseResult testCase)
        {
            _testResults.Add(testCase);
            Logger.Debug($"XML Report: Test ended - {testCase.TestCaseName} ({testCase.Status})");
        }

        public void EndExecution(TestExecutionSummary summary)
        {
            _executionEndTime = DateTime.Now;
            Logger.Info($"XML Report: Execution ended - {_executionEndTime}");
        }

        public void GenerateReport()
        {
            var doc = new System.Xml.XmlDocument();
            var root = doc.CreateElement("TestResults");
            root.SetAttribute("ExecutionStart", _executionStartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            root.SetAttribute("ExecutionEnd", _executionEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
            root.SetAttribute("TotalTests", _testResults.Count.ToString());
            root.SetAttribute("PassedTests", _testResults.Count(t => t.Status == Enums.TestStatus.Passed).ToString());
            root.SetAttribute("FailedTests", _testResults.Count(t => t.Status == Enums.TestStatus.Failed).ToString());
            doc.AppendChild(root);

            foreach (var testResult in _testResults)
            {
                var testElement = doc.CreateElement("Test");
                testElement.SetAttribute("Name", testResult.TestCaseName);
                testElement.SetAttribute("Status", testResult.Status.ToString());
                testElement.SetAttribute("Module", testResult.Module);
                testElement.SetAttribute("Duration", testResult.Duration.TotalMilliseconds.ToString());
                testElement.SetAttribute("StartTime", testResult.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                testElement.SetAttribute("EndTime", testResult.EndTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                {
                    var errorElement = doc.CreateElement("Error");
                    errorElement.InnerText = testResult.ErrorMessage;
                    testElement.AppendChild(errorElement);
                }

                foreach (var step in testResult.Steps)
                {
                    var stepElement = doc.CreateElement("Step");
                    stepElement.SetAttribute("Name", step.StepName);
                    stepElement.SetAttribute("Status", step.Status.ToString());
                    if (!string.IsNullOrEmpty(step.Description))
                    {
                        stepElement.InnerText = step.Description;
                    }
                    else if (!string.IsNullOrEmpty(step.ErrorMessage))
                    {
                        stepElement.InnerText = step.ErrorMessage;
                    }
                    testElement.AppendChild(stepElement);
                }

                root.AppendChild(testElement);
            }

            doc.Save(_reportPath);
            Logger.Info($"XML report generated: {_reportPath}");
        }

        public string GetReportPath()
        {
            return _reportPath;
        }
    }
}
