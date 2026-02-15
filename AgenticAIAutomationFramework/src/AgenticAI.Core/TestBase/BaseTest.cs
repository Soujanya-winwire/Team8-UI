using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.Reporting;
using NUnit.Framework;

namespace AgenticAI.Core.TestBase
{
    /// <summary>
    /// Base class for all test classes with comprehensive setup and teardown
    /// </summary>
    public abstract class BaseTest
    {
        protected FrameworkConfiguration Config { get; private set; } = null!;
        protected EnvironmentConfiguration EnvConfig { get; private set; } = null!;
        protected TestCaseResult CurrentTestResult { get; private set; } = null!;
        protected int CurrentRetryCount { get; set; }

        [OneTimeSetUp]
        public virtual void OneTimeSetup()
        {
            // Initialize logger
            Logger.Initialize();
            Logger.Info("=== Test Suite Initialization ===");

            // Load configuration
            Config = ConfigurationManager.Instance.FrameworkConfig;
            EnvConfig = ConfigurationManager.Instance.CurrentEnvironmentConfig;

            // Setup reporters
            ReportManager.Instance.AddReporter(new HtmlReporter(Config.ReportPath));

            // Create necessary directories
            CreateDirectories();

            Logger.Info($"Framework: {Config.AutomationFramework}");
            Logger.Info($"Environment: {Config.Environment}");
            Logger.Info($"Browser: {Config.Browser}");
            Logger.Info($"OS: {Config.OperatingSystem}");
        }

        [SetUp]
        public virtual void Setup()
        {
            var testName = TestContext.CurrentContext.Test.Name;
            var className = TestContext.CurrentContext.Test.ClassName ?? "Unknown";

            CurrentTestResult = new TestCaseResult
            {
                TestCaseId = Guid.NewGuid().ToString(),
                TestCaseName = testName,
                Module = GetModuleName(),
                StartTime = DateTime.Now,
                Tags = GetTestTags()
            };

            Logger.TestInfo(testName, "Test started");
            ReportManager.Instance.StartTest(CurrentTestResult);
        }

        [TearDown]
        public virtual async Task TearDown()
        {
            var testName = TestContext.CurrentContext.Test.Name;
            var testResult = TestContext.CurrentContext.Result;

            CurrentTestResult.EndTime = DateTime.Now;

            // Determine test status
            CurrentTestResult.Status = testResult.Outcome.Status switch
            {
                NUnit.Framework.Interfaces.TestStatus.Passed => TestStatus.Passed,
                NUnit.Framework.Interfaces.TestStatus.Failed => TestStatus.Failed,
                NUnit.Framework.Interfaces.TestStatus.Skipped => TestStatus.Skipped,
                NUnit.Framework.Interfaces.TestStatus.Inconclusive => TestStatus.Warning,
                _ => TestStatus.Info
            };

            if (CurrentTestResult.Status == TestStatus.Failed)
            {
                CurrentTestResult.ErrorMessage = testResult.Message;
                CurrentTestResult.StackTrace = testResult.StackTrace;
                
                // Capture screenshot on failure
                await CaptureScreenshotOnFailureAsync();
            }

            CurrentTestResult.RetryCount = CurrentRetryCount;

            Logger.TestInfo(testName, $"Test ended with status: {CurrentTestResult.Status}");
            ReportManager.Instance.EndTest(CurrentTestResult);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            Logger.Info("=== Test Suite Cleanup ===");
            
            try
            {
                ReportManager.Instance.EndExecution();
                var reportPaths = ReportManager.Instance.GetReportPaths();
                
                Logger.Info("Test reports generated:");
                foreach (var path in reportPaths)
                {
                    Logger.Info($"  - {Path.GetFullPath(path)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during test suite cleanup");
            }
            finally
            {
                Logger.Close();
            }
        }

        protected void LogStep(string stepName, string description, TestStatus status, 
            string? errorMessage = null, string? screenshotPath = null)
        {
            var step = new TestStepResult
            {
                StepName = stepName,
                Description = description,
                Status = status,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                ErrorMessage = errorMessage,
                ScreenshotPath = screenshotPath
            };

            CurrentTestResult.Steps.Add(step);
            ReportManager.Instance.LogStep(step);
            
            Logger.StepInfo(stepName, $"{description} - {status}");
        }

        protected async Task<string?> CaptureScreenshotAsync(string fileName)
        {
            // To be implemented in derived classes
            return null;
        }

        protected virtual async Task CaptureScreenshotOnFailureAsync()
        {
            // To be implemented in UI test classes
        }

        private void CreateDirectories()
        {
            Directory.CreateDirectory(Config.ScreenshotPath);
            Directory.CreateDirectory(Config.VideoPath);
            Directory.CreateDirectory(Config.ReportPath);
            Directory.CreateDirectory(Config.LogPath);
        }

        private string GetModuleName()
        {
            var testClass = TestContext.CurrentContext.Test.ClassName;
            if (string.IsNullOrEmpty(testClass))
            {
                return "Default";
            }

            // Extract module from namespace or class attributes
            var parts = testClass.Split('.');
            return parts.Length > 1 ? parts[^2] : "Default";
        }

        private List<string> GetTestTags()
        {
            var tags = new List<string>();
            var categories = TestContext.CurrentContext.Test.Properties["Category"];
            
            if (categories != null)
            {
                foreach (var category in categories)
                {
                    if (category != null)
                    {
                        tags.Add(category.ToString()!);
                    }
                }
            }

            return tags;
        }
    }
}
