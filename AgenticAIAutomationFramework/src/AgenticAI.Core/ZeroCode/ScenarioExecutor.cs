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
                // Check if scenario uses new unified Steps model
                if (scenario.Steps != null && scenario.Steps.Count > 0)
                {
                    // NEW APPROACH: Execute unified steps in order they appear in the array
                    Logger.TestInfo(scenario.Name, $"Executing {scenario.Steps.Count} steps in sequence (unified model)");
                    
                    var orderedSteps = scenario.Steps.ToList();
                    var totalSteps = orderedSteps.Count;
                    
                    // Check if first step is Navigate (if StartUrl is set, it will be added as step 0)
                    var hasNavigateStep = orderedSteps.Any(s => s.StepType == "Action" && s.Action?.ActionType?.ToLower() == "navigate");
                    
                    // Add navigation if needed and not already present
                    var urlToNavigate = !string.IsNullOrEmpty(scenario.StartUrl) 
                        ? scenario.StartUrl 
                        : _config.BaseUrl;
                    
                    if (!string.IsNullOrEmpty(urlToNavigate) && !hasNavigateStep)
                    {
                        Logger.TestInfo(scenario.Name, $"Navigating to: {urlToNavigate}");
                        var isOnlyStep = orderedSteps.Count == 0;
                        await ExecuteNavigationAsync(urlToNavigate, testResult, isOnlyStep);
                        totalSteps++; // Include navigation in total count
                    }
                    
                    for (int i = 0; i < orderedSteps.Count; i++)
                    {
                        var step = orderedSteps[i];
                        var isLastStep = (i == orderedSteps.Count - 1);
                        
                        if (step.StepType == "Action" && step.Action != null)
                        {
                            await ExecuteActionAsync(step.Action, testResult, isLastStep);
                        }
                        else if (step.StepType == "Assertion" && step.Assertion != null)
                        {
                            await ExecuteAssertionAsync(step.Assertion, testResult, isLastStep);
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
                    
                    // Calculate total steps for last step detection
                    var totalActions = scenario.Actions.Count;
                    var assertionsForActions = scenario.Assertions.Where(a => a.ExecuteAfterActionIndex.HasValue).ToList();
                    var remainingAssertions = scenario.Assertions.Where(a => !a.ExecuteAfterActionIndex.HasValue).ToList();
                    var hasNavigation = !string.IsNullOrEmpty(scenario.StartUrl) || !string.IsNullOrEmpty(_config.BaseUrl);
                    var totalSteps = (hasNavigation ? 1 : 0) + totalActions + remainingAssertions.Count;
                    var currentStepIndex = 0;
                    
                    // Navigate to start URL - use Base URL from config if StartUrl is empty
                    var urlToNavigate = !string.IsNullOrEmpty(scenario.StartUrl) 
                        ? scenario.StartUrl 
                        : _config.BaseUrl;

                    if (!string.IsNullOrEmpty(urlToNavigate))
                    {
                        Logger.TestInfo(scenario.Name, $"Navigating to: {urlToNavigate}");
                        var isLastStep = (currentStepIndex == totalSteps - 1);
                        await ExecuteNavigationAsync(urlToNavigate, testResult, isLastStep);
                        currentStepIndex++;
                    }
                    else
                    {
                        Logger.Warning("No Start URL specified and no Base URL configured. Skipping navigation.");
                    }
                    
                    // Execute actions with their assertions interleaved
                    for (int i = 0; i < scenario.Actions.Count; i++)
                    {
                        var isLastStep = (currentStepIndex == totalSteps - 1);
                        
                        // Execute the action
                        await ExecuteActionAsync(scenario.Actions[i], testResult, isLastStep);
                        currentStepIndex++;
                        
                        // Execute assertions that should run after this action
                        var assertionsForThisAction = scenario.Assertions
                            .Where(a => a.ExecuteAfterActionIndex == i)
                            .ToList();
                        
                        foreach (var assertion in assertionsForThisAction)
                        {
                            isLastStep = (currentStepIndex == totalSteps - 1);
                            await ExecuteAssertionAsync(assertion, testResult, isLastStep);
                            currentStepIndex++;
                        }
                    }

                    // Execute remaining assertions that don't have a specific action index (legacy support)
                    foreach (var assertion in remainingAssertions)
                    {
                        var isLastStep = (currentStepIndex == totalSteps - 1);
                        await ExecuteAssertionAsync(assertion, testResult, isLastStep);
                        currentStepIndex++;
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

        private async Task ExecuteNavigationAsync(string url, TestCaseResult testResult, bool isLastStep = false)
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
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Navigation failed: {ex.Message}");
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                
                // Enhanced screenshot logic - same as actions and assertions
                // Only capture if: 1) Last step, OR 2) Step failed
                var shouldCaptureScreenshot = _config.EnableScreenshots && (isLastStep || step.Status == TestStatus.Failed);
                
                if (shouldCaptureScreenshot)
                {
                    try
                    {
                        await Task.Delay(150); // Brief delay to ensure page has loaded
                        
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var statusSuffix = step.Status == TestStatus.Failed ? "_FAILED" : "";
                        var lastStepSuffix = isLastStep ? "_LAST" : "";
                        var screenshotFileName = $"{testResult.TestCaseName}_Navigate{statusSuffix}{lastStepSuffix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        
                        var reason = step.Status == TestStatus.Failed ? "step failed" : "last step";
                        Logger.Info($"Screenshot saved ({reason}): {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
                
                testResult.Steps.Add(step);
            }
        }

        private async Task ExecuteActionAsync(RecordedAction action, TestCaseResult testResult, bool isLastStep = false)
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

                // Smart wait - only wait if element is not immediately available
                // This prevents unnecessary delays while ensuring element is ready
                if (!string.IsNullOrEmpty(action.Locator) && 
                    actionType != "navigate" && 
                    actionType != "wait" && 
                    actionType != "waitforelement")
                {
                    try
                    {
                        // Quick check first - if element is immediately available, don't wait
                        // Otherwise wait up to 2 seconds (much faster than previous 5 seconds)
                        await _driver.WaitForElementAsync(action.Locator, 2);
                    }
                    catch (Exception waitEx)
                    {
                        Logger.Debug($"Quick wait skipped: {waitEx.Message}. Element may already be ready.");
                    }
                }

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
                            await _driver.SelectOptionAsync(action.Locator, action.Value!);
                        }
                        break;

                    case "check":
                    {
                        // If value is set and locator is a name-based selector, build a specific locator
                        // to avoid strict mode violations when multiple inputs share the same name (e.g. radio groups)
                        var checkLocator = action.Locator;
                        if (!string.IsNullOrEmpty(action.Value) &&
                            !string.IsNullOrEmpty(action.Locator) &&
                            action.Locator.Contains("[name="))
                        {
                            // Combine name + value to create a unique CSS selector
                            checkLocator = $"{action.Locator}[value=\"{action.Value}\"]"; 
                        }
                        await _driver.CheckAsync(checkLocator);
                        break;
                    }

                    case "uncheck":
                    {
                        var uncheckLocator = action.Locator;
                        if (!string.IsNullOrEmpty(action.Value) &&
                            !string.IsNullOrEmpty(action.Locator) &&
                            action.Locator.Contains("[name="))
                        {
                            uncheckLocator = $"{action.Locator}[value=\"{action.Value}\"]"; 
                        }
                        await _driver.UncheckAsync(uncheckLocator);
                        break;
                    }

                    case "hover":
                    case "mousemove":
                        await _driver.HoverAsync(action.Locator);
                        break;

                    case "scroll":
                        await _driver.ScrollToAsync(action.Locator);
                        break;

                    case "wait":
                        var waitTime = int.TryParse(action.Value, out var seconds) ? seconds : 5;
                        await Task.Delay(waitTime * 1000);
                        break;

                    case "waitforelement":
                        var timeout = int.TryParse(action.Value, out var timeoutSec) ? timeoutSec : _config.TimeoutInSeconds;
                        await _driver.WaitForElementAsync(action.Locator, timeout);
                        break;

                    // IFrame handling actions
                    case "switchtoframe":
                        await _driver.SwitchToFrameAsync(action.Locator);
                        Logger.Info($"Switched to iframe: {action.Locator}");
                        
                        // Add a small wait to ensure frame content is loaded
                        await Task.Delay(500);
                        break;

                    case "switchtoframebyindex":
                        var frameIndex = int.TryParse(action.Value, out var idx) ? idx : 0;
                        await _driver.SwitchToFrameByIndexAsync(frameIndex);
                        Logger.Info($"Switched to iframe by index: {frameIndex}");
                        
                        // Add a small wait to ensure frame content is loaded
                        await Task.Delay(500);
                        break;

                    case "switchtodefaultcontent":
                        await _driver.SwitchToDefaultContentAsync();
                        Logger.Info("Switched to default content (main frame)");
                        break;

                    case "switchtoparentframe":
                        await _driver.SwitchToParentFrameAsync();
                        Logger.Info("Switched to parent frame");
                        break;

                    default:
                        Logger.Warning($"Unknown action type: {action.ActionType}");
                        break;
                }

                step.Status = TestStatus.Passed;
                Logger.StepInfo(action.ActionType, $"Action executed successfully");
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Action execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                
                // Enhanced screenshot logic:
                // 1. Always capture if this is the LAST step
                // 2. Always capture if the step FAILED
                // 3. If last step AND failed, only capture once (avoid duplicate)
                var shouldCaptureScreenshot = _config.EnableScreenshots && (isLastStep || step.Status == TestStatus.Failed);
                
                if (shouldCaptureScreenshot)
                {
                    try
                    {
                        await Task.Delay(150); // Brief delay to ensure UI has settled
                        
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var statusSuffix = step.Status == TestStatus.Failed ? "_FAILED" : "";
                        var lastStepSuffix = isLastStep ? "_LAST" : "";
                        var screenshotFileName = $"{testResult.TestCaseName}_{action.ActionType}{statusSuffix}{lastStepSuffix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        
                        var reason = step.Status == TestStatus.Failed ? "step failed" : "last step";
                        Logger.Info($"Screenshot saved ({reason}): {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
                
                testResult.Steps.Add(step);
            }
        }

        private async Task ExecuteAssertionAsync(Assertion assertion, TestCaseResult testResult, bool isLastStep = false)
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

                    case "elementnotvisible":
                        var elementNotVisible = await _driver.FindElementAsync(assertion.Locator);
                        var isStillVisible = await elementNotVisible.IsVisibleAsync();
                        if (isStillVisible)
                        {
                            throw new Exception($"Element is still visible (expected it to be hidden): {assertion.Locator}");
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

                    case "textnotcontains":
                        var textNotContains = await _driver.GetTextAsync(assertion.Locator);
                        if (textNotContains.Contains(assertion.ExpectedValue ?? ""))
                        {
                            throw new Exception($"Text contains unexpected value. Expected text to NOT contain: '{assertion.ExpectedValue}', but actual text was: '{textNotContains}'");
                        }
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

                    case "titlecontains":
                        var titleContains = await _driver.GetTitleAsync();
                        if (!titleContains.Contains(assertion.ExpectedValue ?? ""))
                        {
                            throw new Exception($"Title does not contain expected value. Expected: {assertion.ExpectedValue}, Actual title: {titleContains}");
                        }
                        break;

                    case "elementexists":
                        var elements = await _driver.FindElementsAsync(assertion.Locator);
                        if (elements == null || elements.Count == 0)
                        {
                            throw new Exception($"Element does not exist in the DOM: {assertion.Locator}");
                        }
                        break;

                    case "elementnotexists":
                        var elementsNotExist = await _driver.FindElementsAsync(assertion.Locator);
                        if (elementsNotExist != null && elementsNotExist.Count > 0)
                        {
                            throw new Exception($"Element exists in the DOM but was expected to be absent: {assertion.Locator}");
                        }
                        break;

                    case "valueequals":
                        var domValue = await _driver.GetAttributeAsync(assertion.Locator, "value");
                        if (domValue != assertion.ExpectedValue)
                        {
                            throw new Exception($"Value mismatch. Expected: '{assertion.ExpectedValue}', Actual: '{domValue}'");
                        }
                        break;

                    default:
                        Logger.Warning($"Unknown assertion type: {assertion.Type}");
                        break;
                }

                step.Status = TestStatus.Passed;
                Logger.StepInfo("Verify", $"Assertion passed: {assertion.Type}");
            }
            catch (Exception ex)
            {
                step.Status = TestStatus.Failed;
                step.ErrorMessage = ex.Message;
                Logger.Error($"Assertion failed: {ex.Message}");
                throw;
            }
            finally
            {
                step.EndTime = DateTime.Now;
                
                // Enhanced screenshot logic:
                // 1. Always capture if this is the LAST step
                // 2. Always capture if the step FAILED
                // 3. If last step AND failed, only capture once (avoid duplicate)
                var shouldCaptureScreenshot = _config.EnableScreenshots && (isLastStep || step.Status == TestStatus.Failed);
                
                if (shouldCaptureScreenshot)
                {
                    try
                    {
                        var screenshot = await _driver.TakeScreenshotAsync();
                        var statusSuffix = step.Status == TestStatus.Failed ? "_FAILED" : "";
                        var lastStepSuffix = isLastStep ? "_LAST" : "";
                        var screenshotFileName = $"{testResult.TestCaseName}_Verify{statusSuffix}{lastStepSuffix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
                        var screenshotPath = Path.Combine(_config.ScreenshotPath, testResult.Module, screenshotFileName);
                        
                        var directory = Path.GetDirectoryName(screenshotPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        
                        await File.WriteAllBytesAsync(screenshotPath, screenshot);
                        step.ScreenshotPath = screenshotPath;
                        
                        var reason = step.Status == TestStatus.Failed ? "step failed" : "last step";
                        Logger.Info($"Screenshot saved ({reason}): {screenshotPath}");
                    }
                    catch (Exception screenshotEx)
                    {
                        Logger.Warning($"Failed to capture screenshot: {screenshotEx.Message}");
                    }
                }
                
                testResult.Steps.Add(step);
            }
        }
    }
}
