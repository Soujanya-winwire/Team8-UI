using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Utilities;

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

                // Execute each action
                foreach (var action in scenario.Actions)
                {
                    await ExecuteActionAsync(action, testResult);
                }

                // Verify assertions
                foreach (var assertion in scenario.Assertions)
                {
                    await ExecuteAssertionAsync(assertion, testResult);
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
                        // Attempt CSS first, then XPath fallback (stored in Metadata["xpath"]).
                        var cssSelector = action.Locator;
                        action.Metadata.TryGetValue("xpath", out var xpathSelector);

                        await RetryHelper.ExecuteWithRetryAsync(async () =>
                        {
                            // Try CSS selector
                            if (!string.IsNullOrEmpty(cssSelector))
                            {
                                var elements = await _driver.FindElementsAsync(cssSelector, "css");
                                if (elements != null && elements.Count > 0)
                                {
                                    await elements[0].ClickAsync();
                                    return true;
                                }
                            }

                            // Try generic/auto lookup (driver may detect xpath)
                            try
                            {
                                var elemsAuto = await _driver.FindElementsAsync(action.Locator);
                                if (elemsAuto != null && elemsAuto.Count > 0)
                                {
                                    await elemsAuto[0].ClickAsync();
                                    return true;
                                }
                            }
                            catch { }

                            // Try XPath fallback
                            if (!string.IsNullOrEmpty(xpathSelector))
                            {
                                var elementsXPath = await _driver.FindElementsAsync(xpathSelector, "xpath");
                                if (elementsXPath != null && elementsXPath.Count > 0)
                                {
                                    await elementsXPath[0].ClickAsync();
                                    return true;
                                }
                            }

                            // As last resort attempt driver click with provided locator
                            await _driver.ClickAsync(action.Locator);
                            return true;
                        }, _config.MaxRetryCount);
                        break;

                    case "type":
                    case "fill":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            var css = action.Locator;
                            action.Metadata.TryGetValue("xpath", out var xpath);

                            await RetryHelper.ExecuteWithRetryAsync(async () =>
                            {
                                // Try find by CSS
                                if (!string.IsNullOrEmpty(css))
                                {
                                    try
                                    {
                                        var el = await _driver.FindElementAsync(css, "css");
                                        await el.TypeAsync(action.Value!);
                                        return true;
                                    }
                                    catch { }
                                }

                                // Try auto-detect
                                try
                                {
                                    var elAuto = await _driver.FindElementAsync(action.Locator);
                                    await elAuto.TypeAsync(action.Value!);
                                    return true;
                                }
                                catch { }

                                // XPath fallback
                                if (!string.IsNullOrEmpty(xpath))
                                {
                                    var elXpath = await _driver.FindElementAsync(xpath, "xpath");
                                    await elXpath.TypeAsync(action.Value!);
                                    return true;
                                }

                                // As a final attempt, use driver type
                                await _driver.TypeAsync(action.Locator, action.Value!);
                                return true;
                            }, _config.MaxRetryCount);
                        }
                        break;

                    case "navigate":
                        if (!string.IsNullOrEmpty(action.Value))
                        {
                            await RetryHelper.ExecuteWithRetryAsync(async () =>
                            {
                                await _driver.NavigateAsync(action.Value!);
                                return true;
                            }, _config.MaxRetryCount);
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
                            throw new Exception($"Text mismatch. Expected: {assertion.ExpectedValue}, Actual: {actualText}");
                        }
                        break;

                    case "textcontains":
                        var text = await _driver.GetTextAsync(assertion.Locator);
                        if (!text.Contains(assertion.ExpectedValue ?? ""))
                        {
                            throw new Exception($"Text does not contain expected value: {assertion.ExpectedValue}");
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
                testResult.Steps.Add(step);
            }
        }
    }
}
