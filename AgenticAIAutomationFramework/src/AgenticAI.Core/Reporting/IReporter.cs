using AgenticAI.Core.Models;

namespace AgenticAI.Core.Reporting
{
    /// <summary>
    /// Interface for test reporters
    /// </summary>
    public interface IReporter
    {
        void StartExecution(TestExecutionSummary summary);
        void StartTest(TestCaseResult testCase);
        void LogStep(TestStepResult step);
        void EndTest(TestCaseResult testCase);
        void EndExecution(TestExecutionSummary summary);
        void GenerateReport();
        string GetReportPath();
    }
}
