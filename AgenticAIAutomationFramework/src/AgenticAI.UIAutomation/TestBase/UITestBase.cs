using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.Core.TestBase;
using AgenticAI.UIAutomation.Drivers;
using AgenticAI.UIAutomation.Interfaces;
using NUnit.Framework;

namespace AgenticAI.UIAutomation.TestBase
{
    /// <summary>
    /// Base class for UI automation tests
    /// </summary>
    public abstract class UITestBase : BaseTest
    {
        protected IWebDriver Driver { get; private set; } = null!;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            InitializeDriver().Wait();
        }

        private async Task InitializeDriver()
        {
            Driver = await WebDriverFactory.CreateDriverAsync(Config);
            
            // Navigate to base URL if configured
            if (!string.IsNullOrEmpty(EnvConfig.BaseUrl))
            {
                await Driver.NavigateAsync(EnvConfig.BaseUrl);
            }
        }

        [TearDown]
        public override async Task TearDown()
        {
            try
            {
                await base.TearDown();
            }
            finally
            {
                if (Driver != null)
                {
                    await Driver.CloseAsync();
                    await Driver.DisposeAsync();
                }
            }
        }

        protected override async Task CaptureScreenshotOnFailureAsync()
        {
            try
            {
                var testName = TestContext.CurrentContext.Test.Name;
                var screenshotPath = await CaptureScreenshotAsync(testName);
                
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CurrentTestResult.Metadata["FailureScreenshot"] = screenshotPath;
                }
            }
            catch (Exception ex)
            {
                Core.Logging.Logger.Error($"Failed to capture screenshot: {ex.Message}");
            }
        }

        protected new async Task<string?> CaptureScreenshotAsync(string fileName)
        {
            try
            {
                var screenshot = await Driver.TakeScreenshotAsync();
                var screenshotPath = Path.Combine(Config.ScreenshotPath, 
                    $"{fileName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                
                var directory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(screenshotPath, screenshot);
                return screenshotPath;
            }
            catch (Exception ex)
            {
                Core.Logging.Logger.Error($"Screenshot capture failed: {ex.Message}");
                return null;
            }
        }

        protected void ExecuteStep(string stepName, Action action, string description = "")
        {
            var startTime = DateTime.Now;
            try
            {
                action();
                LogStep(stepName, description, TestStatus.Passed);
                CheckVisualRegressionAsync(stepName).Wait();
            }
            catch (Exception ex)
            {
                var screenshotPath = CaptureScreenshotAsync($"{stepName}_Failed").Result;
                LogStep(stepName, description, TestStatus.Failed, ex.Message, screenshotPath);
                throw;
            }
        }

        protected async Task ExecuteStepAsync(string stepName, Func<Task> action, string description = "")
        {
            var startTime = DateTime.Now;
            try
            {
                await action();
                LogStep(stepName, description, TestStatus.Passed);
                await CheckVisualRegressionAsync(stepName);
            }
            catch (Exception ex)
            {
                var screenshotPath = await CaptureScreenshotAsync($"{stepName}_Failed");
                LogStep(stepName, description, TestStatus.Failed, ex.Message, screenshotPath);
                throw;
            }
        }

        /// <summary>
        /// When visual regression is enabled (Config.EnableVisualRegression), captures a screenshot for the
        /// step and compares it against a stored baseline, logging any detected difference. Disabled by
        /// default, so this is a no-op unless a test explicitly opts in via configuration.
        /// </summary>
        private async Task CheckVisualRegressionAsync(string stepName)
        {
            if (!Config.EnableVisualRegression)
            {
                return;
            }

            try
            {
                var screenshotPath = await CaptureScreenshotAsync($"{stepName}_visual");
                if (string.IsNullOrEmpty(screenshotPath))
                {
                    return;
                }

                var baselineDirectory = Path.Combine(Config.ScreenshotPath, "VisualBaselines");
                var result = await Core.Utilities.ScreenshotComparison.CompareWithBaselineAsync(
                    screenshotPath, baselineDirectory, stepName);

                if (!result.AreIdentical)
                {
                    Core.Logging.Logger.Warning(
                        $"Visual regression detected for step '{stepName}': {result.SimilarityPercentage:F1}% similar to baseline");
                }
            }
            catch (Exception ex)
            {
                Core.Logging.Logger.Error($"Visual regression check failed for step '{stepName}': {ex.Message}");
            }
        }
    }
}
