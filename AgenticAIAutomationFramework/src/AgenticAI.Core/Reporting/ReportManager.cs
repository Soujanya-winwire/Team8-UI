using AgenticAI.Core.Models;
using System.Collections.Concurrent;

namespace AgenticAI.Core.Reporting
{
    /// <summary>
    /// Manages multiple reporters and coordinates test reporting
    /// </summary>
    public class ReportManager
    {
        private static ReportManager? _instance;
        private static readonly object _lock = new object();
        private readonly List<IReporter> _reporters;
        private readonly ConcurrentBag<TestCaseResult> _testResults;
        private TestExecutionSummary? _executionSummary;

        private ReportManager()
        {
            _reporters = new List<IReporter>();
            _testResults = new ConcurrentBag<TestCaseResult>();
        }

        public static ReportManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ReportManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void AddReporter(IReporter reporter)
        {
            _reporters.Add(reporter);
        }

        public void StartExecution(TestExecutionSummary summary)
        {
            _executionSummary = summary;
            foreach (var reporter in _reporters)
            {
                reporter.StartExecution(summary);
            }
        }

        public void StartTest(TestCaseResult testCase)
        {
            foreach (var reporter in _reporters)
            {
                reporter.StartTest(testCase);
            }
        }

        public void LogStep(TestStepResult step)
        {
            foreach (var reporter in _reporters)
            {
                reporter.LogStep(step);
            }
        }

        public void EndTest(TestCaseResult testCase)
        {
            _testResults.Add(testCase);
            foreach (var reporter in _reporters)
            {
                reporter.EndTest(testCase);
            }
        }

        public void EndExecution()
        {
            if (_executionSummary == null)
            {
                throw new InvalidOperationException("Execution summary not initialized");
            }

            _executionSummary.EndTime = DateTime.Now;
            _executionSummary.TestResults = _testResults.ToList();

            foreach (var reporter in _reporters)
            {
                reporter.EndExecution(_executionSummary);
                reporter.GenerateReport();
            }
        }

        public TestExecutionSummary GetExecutionSummary()
        {
            return _executionSummary ?? throw new InvalidOperationException("Execution summary not initialized");
        }

        public List<string> GetReportPaths()
        {
            return _reporters.Select(r => r.GetReportPath()).ToList();
        }
    }
}
