using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.ZeroCode.Models;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Executes zero-code test scenarios
    /// </summary>
    public class ScenarioExecutor
    {
        private readonly IWebDriver _driver;
        private readonly FrameworkConfiguration _config;

        public ScenarioExecutor(IWebDriver driver)
        {
            _driver = driver;
            _config = ConfigurationManager.Instance.FrameworkConfig;
        }

        public async Task<TestCaseResult> ExecuteScenarioAsync(TestScenario scenario)
        {
            var testResult = new TestCaseResult
            {
                TestCaseId = scenario.ScenarioId,
                TestCaseName = scenario.Name,
                Module = scenario.Module,
                Tags = scenario.Tags,
                StartTime = DateTime.Now
            };

            Logger.TestInfo(scenario.Name, $"Starting scenario execution: {scenario.Description}");

            try
            {
                // Navigate to start URL - use Base URL from config if StartUrl is empty
                var urlToNavigate = !string.IsNullOrEmpty(scenario.StartUrl) 
                    ? scenario.StartUrl 
                    : _config.BaseUrl;

                if (!string.IsNullOrEmpty(urlToNavigate))
                {
                    Logger.TestInfo(scenario.Name, $"Navigating to: {urlToNavigate}");
                    await ExecuteNavigationAsync(urlToNavigate, testResult);
                }
                else
                {
                    Logger.Warning("No Start URL specified and no Base URL configured. Skipping navigation.");
                }

                // Check if scenario uses new unified Steps model
                if (scenario.Steps != null && scenario.Steps.Count > 0)
                {
                    // NEW APPROACH: Execute unified steps in order
                    Logger.TestInfo(scenario.Name, $"Executing {scenario.Steps.Count} steps in sequence (unified model)");
                    
                    var orderedSteps = scenario.Steps.OrderBy(s => s.Order).ToList();
                    
                    foreach (var step in orderedSteps)
                    {
                        if (step.StepType == "Action" && step.Action != null)
                        {
                            await ExecuteActionAsync(step.Action, testResult);
                        }
                        else if (step.StepType == "Assertion" && step.Assertion != null)
                        {
                            await ExecuteAssertionAsync(step.Assertion, testResult);
                        }
                        else
                        {
                            Logger.Warning($"Invalid step at order {step.Order}: StepType={step.StepType}, has data={step.Action != null || step.Assertion != null}");
                        }
                    }
                }
                else
                {
                    // LEGACY APPROACH: Execute actions then assertions (backward compatibility)
                    Logger.TestInfo(scenario.Name, "Using legacy execution model (Actions then Assertions)");
                    
                    // Execute actions with their assertions interleaved
                    for (int i = 0; i < scenario.Actions.Count; i++)
                    {
                        // Execute the action
                        await ExecuteActionAsync(scenario.Actions[i], testResult);
                        
                        // Execute assertions that should run after this action
                        var assertionsForThisAction = scenario.Assertions
                            .Where(a => a.ExecuteAfterActionIndex == i)
                            .ToList();
                        
                        foreach (var assertion in assertionsForThisAction)
                        {
                            await ExecuteAssertionAsync(assertion, testResult);
                        }
                    }

                    // Execute remaining assertions that don't have a specific action index (legacy support)
                    var remainingAssertions = scenario.Assertions
                        .Where(a => !a.ExecuteAfterActionIndex.HasValue)
                        .ToList();
                    
                    foreach (var assertion in remainingAssertions)
                    {
                        await ExecuteAssertionAsync(assertion, testResult);
                    }
                }

                testResult.Status = TestStatus.Passed;
                Logger.TestInfo(scenario.Name, "Scenario execution completed successfully");
            }
            catch (Exception ex)
            {
                testResult.Status = TestStatus.Failed;
                testResult.ErrorMessage = ex.Message;
                testResult.StackTrace = ex.StackTrace;
                Logger.Error($"Scenario execution failed: {ex.Message}");
            }
            finally
            {
                testResult.EndTime = DateTime.Now;
            }

            return testResult;
        }

        private async Task ExecuteNavigationAsync(string url, TestCaseResult testResult)
        {
            var step = new TestStepResult
            {
                StepName = "Navigate",
                Description = $"Navigate to {url}",
                StartTime = DateTime.Now
            };

            try
            {
                await _driver.NavigateAsync(url);
                step.Status = TestStatus.Passed;
                Logger.StepInfo("Navigate", $"Successfully navigated to {url}");
                
                // Capture screenshot after successful navigation
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_Navigate_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Info($"Screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Navigation failed: {ex.Message}");
                
                // Capture screenshot on failure
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_Navigate_FAILED_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Warning($"Failure screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture failure screenshot: {screenshotEx.Message}");
                    }
                }
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                testResult.Steps.Add(step);
            }
        }

        private async Task ExecuteActionAsync(RecordedAction action, TestCaseResult testResult)
        {
            var step = new TestStepResult
            {
                StepName = action.ActionType,
                Description = action.Description ?? $"{action.ActionType} on {action.Locator}",
                StartTime = DateTime.Now
            };

            try
            {
                // Normalize action type
                var actionType = (action.ActionType ?? string.Empty).ToLower().Trim();

                switch (actionType)
                {
                    case "click":
                        await _driver.ClickAsync(action.Locator);
                        break;

                    case "type":
                    case "fill":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            await _driver.TypeAsync(action.Locator, action.Value!);
                        }
                        break;

                    case "navigate":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            await _driver.NavigateAsync(action.Value!);
                        }
                        break;

                    case "select":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            await _driver.TypeAsync(action.Locator, action.Value!);
                        }
                        break;

                    case "wait":
                        var waitTime = int.TryParse(action.Value, out var seconds) ? seconds : 5;
                        await Task.Delay(waitTime * 1000);
                        break;

                    case "waitforelement":
                        var timeout = int.TryParse(action.Value, out var timeoutSec) ? timeoutSec : _config.TimeoutInSeconds;
                        await _driver.WaitForElementAsync(action.Locator, timeout);
                        break;

                    default:
                        Logger.Warning($"Unknown action type: {action.ActionType}");
                        break;
                }

                step.Status = TestStatus.Passed;
                Logger.StepInfo(action.ActionType, $"Action executed successfully");
                
                // Capture screenshot after successful action
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_{action.ActionType}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Info($"Screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Action execution failed: {ex.Message}");
                
                // Capture screenshot on failure
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_{action.ActionType}_FAILED_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Info($"Failure screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture failure screenshot: {screenshotEx.Message}");
                    }
                }
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                testResult.Steps.Add(step);
            }
        }

        private async Task ExecuteAssertionAsync(Assertion assertion, TestCaseResult testResult)
        {
            var step = new TestStepResult
            {
                StepName = "Verify",
                Description = assertion.Description ?? $"Verify {assertion.Type}",
                StartTime = DateTime.Now
            };

            try
            {
                switch (assertion.Type.ToLower())
                {
                    case "elementvisible":
                        var element = await _driver.FindElementAsync(assertion.Locator);
                        var isVisible = await element.IsVisibleAsync();
                        if (!isVisible)
                        {
                            throw new Exception($"Element not visible: {assertion.Locator}");
                        }
                        break;

                    case "textequals":
                        var actualText = await _driver.GetTextAsync(assertion.Locator);
                        if (actualText != assertion.ExpectedValue)
                        {
                            throw new Exception($"Text mismatch. Expected: '{assertion.ExpectedValue}', Actual: '{actualText}'");
                        }
                        Logger.Info($"Text equals assertion passed. Expected: '{assertion.ExpectedValue}', Actual: '{actualText}'");
                        break;

                    case "textcontains":
                        var text = await _driver.GetTextAsync(assertion.Locator);
                        if (!text.Contains(assertion.ExpectedValue ?? ""))
                        {
                            throw new Exception($"Text does not contain expected value. Expected text to contain: '{assertion.ExpectedValue}', but actual text was: '{text}'");
                        }
                        Logger.Info($"Text contains assertion passed. Expected to contain: '{assertion.ExpectedValue}', Actual text: '{text}'");
                        break;

                    case "urlcontains":
                        var currentUrl = await _driver.GetCurrentUrlAsync();
                        if (!currentUrl.Contains(assertion.ExpectedValue ?? ""))
                        {
                            throw new Exception($"URL does not contain expected value: {assertion.ExpectedValue}");
                        }
                        break;

                    case "titleequals":
                        var title = await _driver.GetTitleAsync();
                        if (title != assertion.ExpectedValue)
                        {
                            throw new Exception($"Title mismatch. Expected: {assertion.ExpectedValue}, Actual: {title}");
                        }
                        break;

                    default:
                        Logger.Warning($"Unknown assertion type: {assertion.Type}");
                        break;
                }

                step.Status = TestStatus.Passed;
                Logger.StepInfo("Verify", $"Assertion passed: {assertion.Type}");
                
                // Capture screenshot after successful assertion
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_Verify_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Info($"Screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Assertion failed: {ex.Message}");
                
                // Capture screenshot on failure
                if (_config.EnableScreenshots)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var screenshotFileName = $"{testResult.TestCaseName}_Verify_FAILED_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        Logger.Warning($"Failure screenshot saved: {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture failure screenshot: {screenshotEx.Message}");
                    }
                }
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                testResult.Steps.Add(step);
            }
        }
    }
}
