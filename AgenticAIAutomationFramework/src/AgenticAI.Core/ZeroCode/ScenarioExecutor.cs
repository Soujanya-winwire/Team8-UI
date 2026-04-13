using AgenticAI.Core.Configuration;
using AgenticAI.Core.DataDriven;
using AgenticAI.Core.Enums;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.ZeroCode.Models;
using System.Text.RegularExpressions;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Executes zero-code test scenarios
    /// Supports runtime parameter resolution for data-driven testing
    /// </summary>
    public class ScenarioExecutor
    {
        private readonly IWebDriver _driver;
        private readonly FrameworkConfiguration _config;
        
        /// <summary>
        /// Current dataset for resolving placeholders at runtime
        /// Set by DataDrivenRunner before executing each iteration
        /// </summary>
        public Dictionary<string, string>? CurrentDataset { get; set; }

        public ScenarioExecutor(IWebDriver driver)
        {
            _driver = driver;
            _config = ConfigurationManager.Instance.FrameworkConfig;
            CurrentDataset = null;
        }
        
        /// <summary>
        /// Set the current dataset for placeholder resolution
        /// </summary>
        /// <param name="dataset">Dictionary of parameter names to values</param>
        public void SetDataset(Dictionary<string, string>? dataset)
        {
            CurrentDataset = dataset;
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
                // CRITICAL FIX: Ensure Steps array is properly built before execution
                // This handles cases where the scenario is loaded directly from JSON
                // and the Steps array is outdated or doesn't include new assertions
                EnsureStepsArrayIsValid(scenario);
                
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
                    
                    // Resolve placeholders in StartUrl at runtime if CurrentDataset is available
                    if (!string.IsNullOrEmpty(urlToNavigate) && CurrentDataset != null && CurrentDataset.Count > 0)
                    {
                        urlToNavigate = ParameterResolver.ResolveParameters(urlToNavigate, CurrentDataset);
                        Logger.Debug($"Resolved StartUrl '{scenario.StartUrl}' → '{urlToNavigate}'");
                    }
                    
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
                    var beforeAssertions = scenario.Assertions.Where(a => a.ExecuteBeforeActionIndex.HasValue).ToList();
                    var afterAssertions = scenario.Assertions.Where(a => a.ExecuteAfterActionIndex.HasValue).ToList();
                    var remainingAssertions = scenario.Assertions.Where(a => !a.ExecuteAfterActionIndex.HasValue && !a.ExecuteBeforeActionIndex.HasValue).ToList();
                    var hasNavigation = !string.IsNullOrEmpty(scenario.StartUrl) || !string.IsNullOrEmpty(_config.BaseUrl);
                    var totalSteps = (hasNavigation ? 1 : 0) + totalActions + beforeAssertions.Count + afterAssertions.Count + remainingAssertions.Count;
                    var currentStepIndex = 0;
                    
                    // Navigate to start URL - use Base URL from config if StartUrl is empty
                    var urlToNavigate = !string.IsNullOrEmpty(scenario.StartUrl) 
                        ? scenario.StartUrl 
                        : _config.BaseUrl;
                    
                    // Resolve placeholders in StartUrl at runtime if CurrentDataset is available
                    if (!string.IsNullOrEmpty(urlToNavigate) && CurrentDataset != null && CurrentDataset.Count > 0)
                    {
                        urlToNavigate = ParameterResolver.ResolveParameters(urlToNavigate, CurrentDataset);
                        Logger.Debug($"Resolved StartUrl '{scenario.StartUrl}' → '{urlToNavigate}'");
                    }

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
                        // Execute precondition assertions (BEFORE action)
                        var beforeList = scenario.Assertions
                            .Where(a => a.ExecuteBeforeActionIndex == i)
                            .ToList();
                        
                        foreach (var assertion in beforeList)
                        {
                            var isLastStep = (currentStepIndex == totalSteps - 1);
                            await ExecuteAssertionAsync(assertion, testResult, isLastStep);
                            currentStepIndex++;
                        }
                        
                        // Execute the action
                        var isLastActionStep = (currentStepIndex == totalSteps - 1);
                        await ExecuteActionAsync(scenario.Actions[i], testResult, isLastActionStep);
                        currentStepIndex++;
                        
                        // Execute postcondition assertions (AFTER action)
                        var afterList = scenario.Assertions
                            .Where(a => a.ExecuteAfterActionIndex == i)
                            .ToList();
                        
                        foreach (var assertion in afterList)
                        {
                            var isLastAssertion = (currentStepIndex == totalSteps - 1);
                            await ExecuteAssertionAsync(assertion, testResult, isLastAssertion);
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
                // IMPORTANT: Skip for scroll actions with "window" locator (page scroll)
                if (!string.IsNullOrEmpty(action.Locator) && 
                    actionType != "navigate" && 
                    actionType != "wait" && 
                    actionType != "waitforelement" &&
                    !(actionType == "scroll" && action.Locator == "window"))  // Don't wait for "window" element
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
                        // ENHANCED: Scroll to element before clicking to ensure visibility
                        try
                        {
                            await _driver.ScrollToAsync(action.Locator);
                            Logger.Debug($"Scrolled to element before clicking: {action.Locator}");
                        }
                        catch (Exception scrollEx)
                        {
                            Logger.Debug($"Could not scroll to element before click: {scrollEx.Message}");
                            // Don't fail on scroll, proceed with click anyway
                        }
                        
                        await _driver.ClickAsync(action.Locator);
                        Logger.Info($"Successfully clicked on: {action.Locator}");
                        break;

                    case "type":
                    case "fill":
                    case "input":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            var resolvedValue = ResolveActionValueFromDataset(action);
                            step.Description = $"Type \"{resolvedValue}\" into {action.Locator}";
                            
                            // ENHANCED: Scroll to input field before typing to ensure visibility
                            try
                            {
                                await _driver.ScrollToAsync(action.Locator);
                                Logger.Debug($"Scrolled to field before typing: {action.Locator}");
                            }
                            catch (Exception scrollEx)
                            {
                                Logger.Debug($"Could not scroll to field before typing: {scrollEx.Message}");
                                // Don't fail on scroll, proceed with typing anyway
                            }
                            
                            // CRITICAL FIX: Check if element is a radio button or checkbox
                            // These elements should be clicked, not filled
                            try
                            {
                                var elementType = await _driver.GetAttributeAsync(action.Locator, "type");
                                
                                if (elementType != null && (elementType.ToLower() == "radio" || elementType.ToLower() == "checkbox"))
                                {
                                    // For radio buttons and checkboxes, use click instead of type
                                    await _driver.ClickAsync(action.Locator);
                                    Logger.Info($"Successfully clicked {elementType} input: {action.Locator}");
                                }
                                else
                                {
                                    // Normal text inputs - use type
                                    await _driver.TypeAsync(action.Locator, resolvedValue!);
                                    Logger.Info($"Successfully typed into: {action.Locator}");
                                }
                            }
                            catch (Exception typeCheckEx)
                            {
                                Logger.Debug($"Could not determine input type: {typeCheckEx.Message}. Attempting to type...");
                                // If we can't determine type, try typing anyway
                                await _driver.TypeAsync(action.Locator, resolvedValue!);
                                Logger.Info($"Successfully typed into: {action.Locator}");
                            }
                        }
                        break;

                    case "navigate":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            // Resolve placeholders at runtime if CurrentDataset is available
                            var resolvedUrl = action.Value;
                            if (CurrentDataset != null && CurrentDataset.Count > 0)
                            {
                                resolvedUrl = ParameterResolver.ResolveParameters(action.Value!, CurrentDataset);
                                Logger.Debug($"Resolved URL '{action.Value}' → '{resolvedUrl}' for Navigate action");
                            }
                            
                            await _driver.NavigateAsync(resolvedUrl!);
                        }
                        break;

                    case "select":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            var resolvedOption = ResolveActionValueFromDataset(action);
                                step.Description = $"Select \"{resolvedOption}\" from {action.Locator}";
                            
                            await _driver.SelectOptionAsync(action.Locator, resolvedOption!);
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
                        // GLOBAL FIX: Enhanced scroll with special value support
                        // Handle window/page scrolling (captured during recording)
                        if (action.Locator == "window" && !string.IsNullOrEmpty(action.Value))
                        {
                            try
                            {
                                // Parse JSON coordinates: {"x":0,"y":183}
                                var scrollData = System.Text.Json.JsonDocument.Parse(action.Value);
                                var x = scrollData.RootElement.GetProperty("x").GetInt32();
                                var y = scrollData.RootElement.GetProperty("y").GetInt32();
                                
                                await _driver.ExecuteScriptAsync($"window.scrollTo({{ left: {x}, top: {y}, behavior: 'smooth' }})");
                                await Task.Delay(500); // Wait for scroll animation
                                Logger.Info($"Scrolled page to position: x={x}, y={y}");
                            }
                            catch (Exception jsonEx)
                            {
                                Logger.Warning($"Failed to parse scroll JSON, trying legacy format: {jsonEx.Message}");
                                
                                // Fallback: try comma-separated format
                                if (action.Value.Contains(","))
                                {
                                    var coords = action.Value.Split(',');
                                    if (coords.Length == 2 && int.TryParse(coords[0].Trim(), out var x) && int.TryParse(coords[1].Trim(), out var y))
                                    {
                                        await _driver.ExecuteScriptAsync($"window.scrollTo({{ left: {x}, top: {y}, behavior: 'smooth' }})");
                                        await Task.Delay(500);
                                        Logger.Info($"Scrolled page to coordinates: ({x}, {y})");
                                    }
                                }
                            }
                        }
                        else if (string.IsNullOrEmpty(action.Locator) && !string.IsNullOrEmpty(action.Value))
                        {
                            // Handle special scroll values: "top", "bottom", or pixel coordinates
                            var scrollValue = action.Value.ToLower().Trim();
                            
                            if (scrollValue == "top")
                            {
                                // Scroll to top of page
                                await _driver.ExecuteScriptAsync("window.scrollTo({ top: 0, behavior: 'smooth' })");
                                await Task.Delay(500); // Wait for scroll to complete
                                Logger.Info("Scrolled to top of page");
                            }
                            else if (scrollValue == "bottom")
                            {
                                // Scroll to bottom of page
                                await _driver.ExecuteScriptAsync("window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' })");
                                await Task.Delay(500); // Wait for scroll to complete
                                Logger.Info("Scrolled to bottom of page");
                            }
                            else if (scrollValue.Contains(","))
                            {
                                // Parse coordinates: "x,y" format
                                var coords = scrollValue.Split(',');
                                if (coords.Length == 2 && int.TryParse(coords[0].Trim(), out var x) && int.TryParse(coords[1].Trim(), out var y))
                                {
                                    await _driver.ExecuteScriptAsync($"window.scrollTo({{ left: {x}, top: {y}, behavior: 'smooth' }})");
                                    await Task.Delay(500);
                                    Logger.Info($"Scrolled to coordinates: ({x}, {y})");
                                }
                                else
                                {
                                    Logger.Warning($"Invalid scroll coordinates format: {action.Value}. Expected 'x,y' format.");
                                }
                            }
                            else if (int.TryParse(scrollValue, out var pixels))
                            {
                                // Scroll by pixel amount (vertical)
                                await _driver.ExecuteScriptAsync($"window.scrollBy({{ top: {pixels}, behavior: 'smooth' }})");
                                await Task.Delay(500);
                                Logger.Info($"Scrolled by {pixels} pixels");
                            }
                            else
                            {
                                Logger.Warning($"Unknown scroll value: {action.Value}");
                            }
                        }
                        else if (!string.IsNullOrEmpty(action.Locator) && action.Locator != "window")
                        {
                            // Scroll to specific element using locator
                            try
                            {
                                await _driver.ScrollToAsync(action.Locator);
                                Logger.Info($"Scrolled to element: {action.Locator}");
                            }
                            catch (Exception scrollEx)
                            {
                                // ENHANCED: If element scroll fails, try page-level scroll to make element visible
                                Logger.Warning($"Element scroll failed: {scrollEx.Message}. Trying page scroll...");
                                
                                try
                                {
                                    // Try scrolling the page to reveal the element
                                    await _driver.ExecuteScriptAsync(@"
                                        (function() {
                                            // Scroll down in increments to find the element
                                            const scrollInterval = window.innerHeight * 0.8;
                                            
                                            // Initial scroll attempts
                                            for (let i = 0; i < 3; i++) {
                                                window.scrollBy({ top: scrollInterval, behavior: 'smooth' });
                                            }
                                        })()
                                    ");
                                    await Task.Delay(1500); // Wait for scroll and content load
                                    
                                    // Retry scrolling to element after page scroll
                                    await _driver.ScrollToAsync(action.Locator);
                                    Logger.Info($"Successfully scrolled to element after page scroll: {action.Locator}");
                                }
                                catch (Exception retryEx)
                                {
                                    Logger.Error($"Scroll failed even after retry: {retryEx.Message}");
                                    throw;
                                }
                            }
                        }
                        else
                        {
                            Logger.Warning("Scroll action requires either a locator or a value (top/bottom/pixels)");
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

                    // PRIORITY 1 ACTIONS - Quick Wins ?
                    
                    case "doubleclick":
                        await _driver.DoubleClickAsync(action.Locator);
                        Logger.Info($"Double-clicked on: {action.Locator}");
                        break;

                    case "rightclick":
                    case "contextclick":
                        await _driver.RightClickAsync(action.Locator);
                        Logger.Info($"Right-clicked on: {action.Locator}");
                        break;

                    case "clear":
                        await _driver.ClearAsync(action.Locator);
                        Logger.Info($"Cleared input field: {action.Locator}");
                        break;

                    case "presskey":
                    case "press":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            await _driver.PressKeyAsync(action.Locator, action.Value!);
                            Logger.Info($"Pressed key '{action.Value}' on: {action.Locator}");
                        }
                        else
                        {
                            Logger.Warning("PressKey action requires a Value (key name like 'Enter', 'Tab', 'Escape')");
                        }
                        break;

                    case "refresh":
                    case "reload":
                        await _driver.RefreshAsync();
                        Logger.Info("Page refreshed");
                        // Add a small wait to ensure page has reloaded
                        await Task.Delay(500);
                        break;

                    case "goback":
                    case "back":
                        await _driver.GoBackAsync();
                        Logger.Info("Navigated back in browser history");
                        // Add a small wait to ensure navigation is complete
                        await Task.Delay(500);
                        break;

                    case "goforward":
                    case "forward":
                        await _driver.GoForwardAsync();
                        Logger.Info("Navigated forward in browser history");
                        // Add a small wait to ensure navigation is complete
                        await Task.Delay(500);
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

        private string ResolveActionValueFromDataset(RecordedAction action)
        {
            var originalValue = action.Value ?? string.Empty;

            if (CurrentDataset == null || CurrentDataset.Count == 0)
                return originalValue;

            if (action.Metadata != null && action.Metadata.TryGetValue("ParameterName", out var paramName))
            {
                if (TryGetDatasetValue(paramName, out var mappedByMetadata))
                {
                    Logger.Debug($"Resolved parameter '{paramName}' → '{mappedByMetadata}' for {action.ActionType} action");
                    return mappedByMetadata;
                }
            }

            if (ParameterResolver.ContainsPlaceholders(originalValue))
            {
                var resolvedPlaceholderValue = ParameterResolver.ResolveParameters(originalValue, CurrentDataset);
                Logger.Debug($"Resolved placeholder '{originalValue}' → '{resolvedPlaceholderValue}' for {action.ActionType} action");
                return resolvedPlaceholderValue;
            }

            var inferredName = ParameterResolver.InferParameterName(action.Locator ?? string.Empty, action.ActionType ?? "Type");
            if (!string.IsNullOrWhiteSpace(inferredName) && TryGetDatasetValue(inferredName, out var mappedByLocator))
            {
                Logger.Debug($"Resolved locator-inferred parameter '{inferredName}' → '{mappedByLocator}' for {action.ActionType} action");
                return mappedByLocator;
            }

            if (TryGetDatasetValue(originalValue, out var mappedByRawValue))
            {
                Logger.Debug($"Resolved raw value-key '{originalValue}' → '{mappedByRawValue}' for {action.ActionType} action");
                return mappedByRawValue;
            }

            if (TryResolveAliasOrCompositeValue(action, out var mappedByAlias))
            {
                Logger.Debug($"Resolved alias/composite mapping for '{action.ActionType}' → '{mappedByAlias}'");
                return mappedByAlias;
            }

            return originalValue;
        }

        private bool TryResolveAliasOrCompositeValue(RecordedAction action, out string resolvedValue)
        {
            resolvedValue = string.Empty;

            if (CurrentDataset == null || CurrentDataset.Count == 0)
                return false;

            var metadataKey = action.Metadata != null && action.Metadata.TryGetValue("ParameterName", out var meta)
                ? meta
                : string.Empty;

            var inferredKey = ParameterResolver.InferParameterName(action.Locator ?? string.Empty, action.ActionType ?? "Type") ?? string.Empty;
            var originalValue = action.Value ?? string.Empty;
            var locator = (action.Locator ?? string.Empty).ToLowerInvariant();

            var normalizedCandidates = new List<string>
            {
                NormalizeDatasetKey(metadataKey),
                NormalizeDatasetKey(inferredKey),
                NormalizeDatasetKey(originalValue)
            }.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();

            // Common semantic aliases
            if (normalizedCandidates.Any(k => k.Contains("nameexamplecom") || k == "email" || k.Contains("email")) ||
                locator.Contains("name@example.com") || locator.Contains("email"))
            {
                if (TryGetDatasetValue("email", out var emailValue))
                {
                    resolvedValue = emailValue;
                    return true;
                }
            }

            if (normalizedCandidates.Any(k => k == "fullname" || k == "name") || locator.Contains("full name"))
            {
                if (TryGetDatasetValue("fullName", out var directFullName))
                {
                    resolvedValue = directFullName;
                    return true;
                }

                var hasFirstName = TryGetDatasetValue("first_name", out var firstName) || TryGetDatasetValue("firstName", out firstName);
                var hasLastName = TryGetDatasetValue("last_name", out var lastName) || TryGetDatasetValue("lastName", out lastName);

                if (hasFirstName || hasLastName)
                {
                    resolvedValue = $"{firstName} {lastName}".Trim();
                    if (!string.IsNullOrWhiteSpace(resolvedValue))
                        return true;
                }
            }

            if (normalizedCandidates.Any(k => k == "currentaddress" || k == "address") || locator.Contains("current address"))
            {
                if (TryGetDatasetValue("current_address", out var currentAddress) ||
                    TryGetDatasetValue("currentAddress", out currentAddress) ||
                    TryGetDatasetValue("address", out currentAddress))
                {
                    resolvedValue = currentAddress;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetDatasetValue(string key, out string value)
        {
            value = string.Empty;

            if (CurrentDataset == null || CurrentDataset.Count == 0 || string.IsNullOrWhiteSpace(key))
                return false;

            if (CurrentDataset.TryGetValue(key, out var exactValue))
            {
                value = exactValue;
                return true;
            }

            var directCaseInsensitive = CurrentDataset
                .FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(directCaseInsensitive.Key))
            {
                value = directCaseInsensitive.Value;
                return true;
            }

            var normalizedKey = NormalizeDatasetKey(key);
            var normalizedMatch = CurrentDataset
                .FirstOrDefault(kv => NormalizeDatasetKey(kv.Key) == normalizedKey);

            if (!string.IsNullOrWhiteSpace(normalizedMatch.Key))
            {
                value = normalizedMatch.Value;
                return true;
            }

            return false;
        }

        private static string NormalizeDatasetKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var withoutSymbols = Regex.Replace(key, "[^a-zA-Z0-9]", string.Empty);
            return withoutSymbols.ToLowerInvariant();
        }

        private async Task ExecuteAssertionAsync(Assertion assertion, TestCaseResult testResult, bool isLastStep = false)
        {
            var step = new TestStepResult
            {
                StepName = "Verify",
                Description = assertion.Description ?? $"Verify {assertion.Type}",
                StartTime = DateTime.Now
            };
            
            // Resolve placeholders in ExpectedValue at runtime if CurrentDataset is available
            var resolvedExpectedValue = assertion.ExpectedValue;
            if (!string.IsNullOrEmpty(assertion.ExpectedValue) && CurrentDataset != null && CurrentDataset.Count > 0)
            {
                resolvedExpectedValue = ParameterResolver.ResolveParameters(assertion.ExpectedValue!, CurrentDataset);
                Logger.Debug($"Resolved assertion ExpectedValue '{assertion.ExpectedValue}' → '{resolvedExpectedValue}'");
            }

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
                        if (actualText != resolvedExpectedValue)
                        {
                            throw new Exception($"Text mismatch. Expected: '{resolvedExpectedValue}', Actual: '{actualText}'");
                        }
                        Logger.Info($"Text equals assertion passed. Expected: '{resolvedExpectedValue}', Actual: '{actualText}'");
                        break;

                    case "textcontains":
                        var text = await _driver.GetTextAsync(assertion.Locator);
                        if (!text.Contains(resolvedExpectedValue ?? ""))
                        {
                            throw new Exception($"Text does not contain expected value. Expected text to contain: '{resolvedExpectedValue}', but actual text was: '{text}'");
                        }
                        Logger.Info($"Text contains assertion passed. Expected to contain: '{resolvedExpectedValue}', Actual text: '{text}'");
                        break;

                    case "textnotcontains":
                        var textNotContains = await _driver.GetTextAsync(assertion.Locator);
                        if (textNotContains.Contains(resolvedExpectedValue ?? ""))
                        {
                            throw new Exception($"Text contains unexpected value. Expected text to NOT contain: '{resolvedExpectedValue}', but actual text was: '{textNotContains}'");
                        }
                        break;

                    case "urlcontains":
                        var currentUrl = await _driver.GetCurrentUrlAsync();
                        if (!currentUrl.Contains(resolvedExpectedValue ?? ""))
                        {
                            throw new Exception($"URL does not contain expected value: {resolvedExpectedValue}");
                        }
                        break;

                    case "titleequals":
                        var title = await _driver.GetTitleAsync();
                        if (title != resolvedExpectedValue)
                        {
                            throw new Exception($"Title mismatch. Expected: {resolvedExpectedValue}, Actual: {title}");
                        }
                        break;

                    case "titlecontains":
                        var titleContains = await _driver.GetTitleAsync();
                        if (!titleContains.Contains(resolvedExpectedValue ?? ""))
                        {
                            throw new Exception($"Title does not contain expected value. Expected: {resolvedExpectedValue}, Actual title: {titleContains}");
                        }
                        break;

                    case "elementexists":
                        // Wait for element with retry logic (better for DDT and dynamic content)
                        var maxRetries = 30; // 30 retries = ~15 seconds total (increased for slow-loading React sites)
                        var retryDelay = 500; // 500ms between retries
                        IList<Core.Interfaces.IWebElement>? elements = null;
                        
                        for (int i = 0; i <= maxRetries; i++)
                        {
                            elements = await _driver.FindElementsAsync(assertion.Locator);
                            if (elements != null && elements.Count > 0)
                            {
                                break; // Element found!
                            }
                            
                            if (i < maxRetries)
                            {
                                await Task.Delay(retryDelay);
                            }
                        }
                        
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

        /// <summary>
        /// Ensures the Steps array is properly built from Actions and Assertions arrays
        /// This is critical for proper execution order of preconditions and postconditions
        /// </summary>
        private void EnsureStepsArrayIsValid(TestScenario scenario)
        {
            if (scenario.Actions == null || scenario.Actions.Count == 0)
            {
                return;
            }

            scenario.Assertions ??= new List<Assertion>();
            
            // Always rebuild the Steps array to ensure assertions are in correct order
            // This handles cases where:
            // 1. Steps array is null or empty
            // 2. Steps array is outdated (doesn't include newly generated assertions)
            // 3. Assertions have been added but Steps wasn't updated
            var unifiedSteps = new List<TestStep>();
            int orderIndex = 0;

            for (int actionIndex = 0; actionIndex < scenario.Actions.Count; actionIndex++)
            {
                var action = scenario.Actions[actionIndex];

                // Add BEFORE assertions (preconditions) first
                var beforeAssertions = scenario.Assertions
                    .Where(a => a.ExecuteBeforeActionIndex == actionIndex)
                    .ToList();

                foreach (var assertion in beforeAssertions)
                {
                    unifiedSteps.Add(new TestStep
                    {
                        Order = orderIndex++,
                        StepType = "Assertion",
                        Action = null,
                        Assertion = assertion
                    });
                }

                // Add the action
                unifiedSteps.Add(new TestStep
                {
                    Order = orderIndex++,
                    StepType = "Action",
                    Action = action,
                    Assertion = null
                });

                // Add AFTER assertions (postconditions) last
                var afterAssertions = scenario.Assertions
                    .Where(a => a.ExecuteAfterActionIndex == actionIndex)
                    .ToList();

                foreach (var assertion in afterAssertions)
                {
                    unifiedSteps.Add(new TestStep
                    {
                        Order = orderIndex++,
                        StepType = "Assertion",
                        Action = null,
                        Assertion = assertion
                    });
                }
            }

            // Add unassigned assertions at the end
            var unassignedAssertions = scenario.Assertions
                .Where(a => !a.ExecuteBeforeActionIndex.HasValue && !a.ExecuteAfterActionIndex.HasValue)
                .ToList();

            foreach (var assertion in unassignedAssertions)
            {
                unifiedSteps.Add(new TestStep
                {
                    Order = orderIndex++,
                    StepType = "Assertion",
                    Action = null,
                    Assertion = assertion
                });
            }

            scenario.Steps = unifiedSteps;
            
            Logger.Debug($"Rebuilt Steps array: {unifiedSteps.Count} total steps from {scenario.Actions.Count} actions and {scenario.Assertions.Count} assertions");
        }
    }
}
