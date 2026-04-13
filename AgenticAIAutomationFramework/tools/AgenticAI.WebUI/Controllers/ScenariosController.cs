using AgenticAI.Core.Interfaces;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Configuration;
using AgenticAI.Core.DataDriven;
using AgenticAI.UIAutomation.Drivers;
using AgenticAI.WebUI.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using FrameworkConfigManager = AgenticAI.Core.Configuration.ConfigurationManager;

namespace AgenticAI.WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScenariosController : ControllerBase
    {
        private readonly IHubContext<TestExecutionHub> _hubContext;

        public ScenariosController(IHubContext<TestExecutionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Get all available test scenarios
        /// </summary>
        [HttpGet]
        public IActionResult GetAllScenarios()
        {
            try
            {
                var manager = new ScenarioManager();
                var scenarios = manager.LoadAllScenarios();
                
                return Ok(new
                {
                    success = true,
                    count = scenarios.Count,
                    scenarios = scenarios.OrderByDescending(s => s.CreatedAt).Select(s => new
                    {
                        s.ScenarioId,
                        s.Name,
                        s.Description,
                        s.Module,
                        s.Tags,
                        s.StartUrl,
                        actionCount = s.Actions.Count,
                        assertionCount = s.Assertions.Count,
                        s.CreatedAt,
                        s.ModifiedAt
                    })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get scenarios by module
        /// </summary>
        [HttpGet("module/{module}")]
        public IActionResult GetScenariosByModule(string module)
        {
            try
            {
                var manager = new ScenarioManager();
                var scenarios = manager.LoadScenariosByModule(module);
                
                return Ok(new
                {
                    success = true,
                    module,
                    count = scenarios.Count,
                    scenarios
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get scenarios by tag
        /// </summary>
        [HttpGet("tag/{tag}")]
        public IActionResult GetScenariosByTag(string tag)
        {
            try
            {
                var manager = new ScenarioManager();
                var scenarios = manager.LoadScenariosByTag(tag);
                
                return Ok(new
                {
                    success = true,
                    tag,
                    count = scenarios.Count,
                    scenarios
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific scenario
        /// </summary>
        [HttpGet("{module}/{name}")]
        public IActionResult GetScenario(string module, string name)
        {
            try
            {
                var manager = new ScenarioManager();
                var scenario = manager.LoadScenario(name, module);
                
                if (scenario == null)
                {
                    return NotFound(new { success = false, error = "Scenario not found" });
                }
                
                // GLOBAL FIX: Clean up legacy issues in old scenarios
                CleanupLegacyScenarioIssues(scenario);
                
                // Auto-generate assertions if missing or incomplete
                // This ensures that old scenarios and new scenarios both have proper assertions
                EnsureScenarioHasAssertions(scenario);
                
                return Ok(new { success = true, scenario });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// GLOBAL FIX: Automatically clean up legacy issues in old scenarios
        /// Fixes incorrect locators, removes duplicates, normalizes data
        /// </summary>
        private void CleanupLegacyScenarioIssues(TestScenario scenario)
        {
            if (scenario == null) return;

            // Fix 1: Replace incorrect locators in Actions
            if (scenario.Actions != null)
            {
                foreach (var action in scenario.Actions)
                {
                    // Fix: button:has-text("Submit") → #submit (or other semantic IDs)
                    if (!string.IsNullOrEmpty(action.Locator) && action.Locator.Contains("button:has-text"))
                    {
                        // Extract button text
                        var match = System.Text.RegularExpressions.Regex.Match(action.Locator, @"button:has-text\([""']?(\w+)[""']?\)");
                        if (match.Success)
                        {
                            var buttonText = match.Groups[1].Value.ToLower();
                            
                            // Map common button texts to their semantic IDs
                            var buttonIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "submit", "#submit" },
                                { "login", "#login" },
                                { "signin", "#signin" },
                                { "signup", "#signup" },
                                { "register", "#register" },
                                { "search", "#search" },
                                { "cancel", "#cancel" },
                                { "save", "#save" },
                                { "delete", "#delete" },
                                { "ok", "#ok" }
                            };

                            if (buttonIdMap.TryGetValue(buttonText, out var semanticId))
                            {
                                action.Locator = semanticId;
                                action.Description = action.Description?.Replace($"button:has-text(\"{buttonText}\")", semanticId)
                                                   ?? $"Click on {semanticId}";
                                Log.Debug($"Fixed action locator: button:has-text(\"{buttonText}\") → {semanticId}");
                            }
                        }
                    }
                }
            }

            // Fix 2: Remove duplicate and fix incorrect locators in Assertions
            if (scenario.Assertions != null && scenario.Assertions.Count > 0)
            {
                var cleanedAssertions = new List<Assertion>();
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var assertion in scenario.Assertions)
                {
                    // Fix incorrect locators in assertions
                    if (!string.IsNullOrEmpty(assertion.Locator) && assertion.Locator.Contains("button:has-text"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(assertion.Locator, @"button:has-text\([""']?(\w+)[""']?\)");
                        if (match.Success)
                        {
                            var buttonText = match.Groups[1].Value.ToLower();
                            var buttonIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "submit", "#submit" },
                                { "login", "#login" },
                                { "signin", "#signin" },
                                { "signup", "#signup" }
                            };

                            if (buttonIdMap.TryGetValue(buttonText, out var semanticId))
                            {
                                assertion.Locator = semanticId;
                                Log.Debug($"Fixed assertion locator: button:has-text(\"{buttonText}\") → {semanticId}");
                            }
                        }
                    }

                    // Deduplicate using proper key
                    var key = GetAssertionDedupKey(assertion);
                    if (!seenKeys.Contains(key))
                    {
                        cleanedAssertions.Add(assertion);
                        seenKeys.Add(key);
                    }
                    else
                    {
                        Log.Debug($"Removed duplicate assertion: {assertion.Type} on {assertion.Locator}");
                    }
                }

                scenario.Assertions = cleanedAssertions;
            }

            // Fix 3: Clear outdated Steps array (will be rebuilt)
            scenario.Steps = new List<TestStep>();
        }

        /// <summary>
        /// Ensures a scenario has auto-generated assertions for its actions
        /// CRITICAL FIX: Respects user edits - only auto-generates if NO assertions exist for an action
        /// </summary>
        private void EnsureScenarioHasAssertions(TestScenario scenario)
        {
            if (scenario.Actions == null || scenario.Actions.Count == 0)
            {
                return;
            }

            scenario.Assertions ??= new List<Assertion>();

            // CRITICAL FIX: Track which actions already have assertions (by action index, not locator)
            // This prevents auto-generation from overwriting user edits
            var actionsWithBeforeAssertions = new HashSet<int>(
                scenario.Assertions
                    .Where(a => a.ExecuteBeforeActionIndex.HasValue)
                    .Select(a => a.ExecuteBeforeActionIndex.Value)
            );

            var actionsWithAfterAssertions = new HashSet<int>(
                scenario.Assertions
                    .Where(a => a.ExecuteAfterActionIndex.HasValue)
                    .Select(a => a.ExecuteAfterActionIndex.Value)
            );

            bool addedNewAssertions = false;

            for (int actionIndex = 0; actionIndex < scenario.Actions.Count; actionIndex++)
            {
                var action = scenario.Actions[actionIndex];
                var generatedList = BuildAutoAssertions(action, actionIndex);
                
                foreach (var generated in generatedList)
                {
                    // Check if an assertion already exists for this action (before or after)
                    bool alreadyExists = false;
                    
                    if (generated.ExecuteBeforeActionIndex.HasValue)
                    {
                        alreadyExists = actionsWithBeforeAssertions.Contains(generated.ExecuteBeforeActionIndex.Value);
                    }
                    else if (generated.ExecuteAfterActionIndex.HasValue)
                    {
                        alreadyExists = actionsWithAfterAssertions.Contains(generated.ExecuteAfterActionIndex.Value);
                    }

                    // Skip if user already defined an assertion for this action (respects user edits)
                    if (alreadyExists)
                    {
                        continue;
                    }

                    // Add the auto-generated assertion
                    scenario.Assertions.Add(generated);
                    
                    // Track that this action now has an assertion
                    if (generated.ExecuteBeforeActionIndex.HasValue)
                    {
                        actionsWithBeforeAssertions.Add(generated.ExecuteBeforeActionIndex.Value);
                    }
                    else if (generated.ExecuteAfterActionIndex.HasValue)
                    {
                        actionsWithAfterAssertions.Add(generated.ExecuteAfterActionIndex.Value);
                    }
                    
                    addedNewAssertions = true;
                }
            }

            // CRITICAL FIX: If we added new assertions, rebuild the Steps array
            // to ensure they're executed in the correct order
            if (addedNewAssertions || scenario.Steps == null || scenario.Steps.Count == 0)
            {
                RebuildStepsArray(scenario);
            }
        }

        /// <summary>
        /// Rebuilds the Steps array from Actions and Assertions arrays
        /// This ensures assertions execute in the correct order (before/after actions)
        /// </summary>
        private void RebuildStepsArray(TestScenario scenario)
        {
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
        }

        private static List<Assertion> BuildAutoAssertions(RecordedAction action, int actionIndex)
        {
            var assertions = new List<Assertion>();
            
            // Skip parametrization steps - they shouldn't have auto-assertions
            // Any action with ParameterName metadata is a data-driven parameterization step
            if (action.Metadata != null &&
                action.Metadata.TryGetValue("ParameterName", out var parameterName) &&
                !string.IsNullOrWhiteSpace(parameterName))
            {
                // This is a parametrization value step, skip assertion
                return assertions;
            }

            var actionType = (action.ActionType ?? string.Empty).Trim().ToLowerInvariant();
            var locator = action.Locator ?? string.Empty;
            var value = action.Value;

            switch (actionType)
            {
                case "navigate":
                    // Postcondition: Verify navigation completed successfully
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        break;
                    }

                    assertions.Add(new Assertion
                    {
                        Type = "UrlContains",
                        ExpectedValue = GetStableUrlToken(value),
                        Description = "Verify navigation completed",
                        ExecuteAfterActionIndex = actionIndex
                    });
                    break;

                case "type":
                case "input":
                case "fill":
                    if (string.IsNullOrWhiteSpace(locator))
                    {
                        break;
                    }

                    // Precondition: Verify element is visible before typing
                    assertions.Add(new Assertion
                    {
                        Type = "ElementVisible",
                        Locator = locator,
                        Description = "Verify element is visible before input",
                        ExecuteBeforeActionIndex = actionIndex
                    });

                    // Postcondition: Verify value was entered correctly
                    var expectedValue = BuildExpectedValueForValueAssertion(action, value);
                    if (!string.IsNullOrWhiteSpace(expectedValue))
                    {
                        assertions.Add(new Assertion
                        {
                            Type = "ValueEquals",
                            Locator = locator,
                            ExpectedValue = expectedValue,
                            Description = "Verify value entered correctly",
                            ExecuteAfterActionIndex = actionIndex
                        });
                    }
                    break;

                case "select":
                    if (string.IsNullOrWhiteSpace(locator))
                    {
                        break;
                    }

                    // Precondition: Verify dropdown is visible before selection
                    assertions.Add(new Assertion
                    {
                        Type = "ElementVisible",
                        Locator = locator,
                        Description = "Verify dropdown is visible before selection",
                        ExecuteBeforeActionIndex = actionIndex
                    });
                    break;

                case "click":
                    if (string.IsNullOrWhiteSpace(locator))
                    {
                        break;
                    }

                    // Precondition: Verify element exists before clicking
                    // Use ElementExists instead of ElementVisible because click action
                    // will automatically scroll to element, so it doesn't need to be visible yet
                    assertions.Add(new Assertion
                    {
                        Type = "ElementExists",
                        Locator = locator,
                        Description = "Verify element exists before click",
                        ExecuteBeforeActionIndex = actionIndex
                    });
                    break;

                case "submit":
                    if (string.IsNullOrWhiteSpace(locator))
                    {
                        break;
                    }

                    // Precondition: Verify element exists before submitting
                    // Use ElementExists instead of ElementVisible because submit action
                    // will automatically scroll to element, so it doesn't need to be visible yet
                    assertions.Add(new Assertion
                    {
                        Type = "ElementExists",
                        Locator = locator,
                        Description = "Verify element exists before submit",
                        ExecuteBeforeActionIndex = actionIndex
                    });
                    break;

                case "wait":
                case "waitforelement":
                case "switchtoframe":
                case "switchtodefaultcontent":
                case "screenshot":
                case "hover":
                    // No assertions for non-interactive or utility actions
                    break;

                default:
                    // Avoid trivial assertions for unknown action types
                    break;
            }
            
            return assertions;
        }

        private static string BuildExpectedValueForValueAssertion(RecordedAction action, string? fallbackValue)
        {
            if (action.Metadata != null &&
                action.Metadata.TryGetValue("ParameterName", out var parameterName) &&
                !string.IsNullOrWhiteSpace(parameterName))
            {
                return ParameterResolver.WrapAsPlaceholder(parameterName);
            }

            return fallbackValue ?? string.Empty;
        }

        private static string GetStableUrlToken(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                var path = parsed.AbsolutePath?.TrimEnd('/') ?? string.Empty;
                return string.IsNullOrWhiteSpace(path) || path == "/"
                    ? parsed.Host
                    : $"{parsed.Host}{path}";
            }

            return url;
        }

        private static string GetAssertionDedupKey(Assertion assertion)
        {
            // CRITICAL FIX: Include BOTH ExecuteBeforeActionIndex AND ExecuteAfterActionIndex
            // to properly deduplicate precondition and postcondition assertions
            return $"{assertion.Type}|{assertion.Locator}|{assertion.ExpectedValue}|{assertion.ExecuteBeforeActionIndex}|{assertion.ExecuteAfterActionIndex}";
        }

        /// <summary>
        /// Extract test data columns from Type actions in a scenario
        /// Returns column names and structure for data-driven testing
        /// </summary>
        [HttpGet("{module}/{name}/testdata")]
        public IActionResult GetScenarioTestDataStructure(string module, string name)
        {
            try
            {
                var manager = new ScenarioManager();
                var scenario = manager.LoadScenario(name, module);
                
                if (scenario == null)
                {
                    return NotFound(new { success = false, error = "Scenario not found" });
                }

                // Extract columns from data-entry actions in recordings
                var dataEntryActions = new List<RecordedAction>();
                var dataEntryActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "type", "input", "fill", "select"
                };
                
                // Check new Steps model first
                if (scenario.Steps != null && scenario.Steps.Count > 0)
                {
                    dataEntryActions.AddRange(
                        scenario.Steps
                            .Where(s => s.StepType == "Action" && s.Action != null && 
                                   dataEntryActionTypes.Contains(s.Action.ActionType ?? string.Empty))
                            .Select(s => s.Action)
                            .Where(a => a != null)
                    );
                }
                
                // Fallback to legacy Actions model
                if (dataEntryActions.Count == 0 && scenario.Actions != null)
                {
                    dataEntryActions.AddRange(
                        scenario.Actions
                            .Where(a => dataEntryActionTypes.Contains(a.ActionType ?? string.Empty))
                    );
                }

                if (dataEntryActions.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No data-entry actions found, using default columns",
                        columns = new[] { "testData", "expectedResult", "tag" },
                        data = new object[] { }
                    });
                }

                // Extract column names (prefer explicit ParameterName from recording metadata)
                var columns = new List<string>();
                foreach (var action in dataEntryActions)
                {
                    string columnName;
                    if (action.Metadata != null && action.Metadata.TryGetValue("ParameterName", out var parameterName) && !string.IsNullOrWhiteSpace(parameterName))
                    {
                        columnName = parameterName.Trim();
                    }
                    else
                    {
                        // If no ParameterName in metadata, try to infer from locator using ParameterResolver
                        var inferredName = AgenticAI.Core.DataDriven.ParameterResolver.InferParameterName(action.Locator ?? string.Empty, action.ActionType ?? "Type");
                        columnName = !string.IsNullOrWhiteSpace(inferredName) 
                            ? inferredName 
                            : ExtractFieldNameFromLocator(action.Locator ?? string.Empty, columns.Count + 1);
                    }
                    
                    if (!columns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                    {
                        columns.Add(columnName);
                    }
                }

                // DON'T automatically add "tag" column - only use actual test parameters

                return Ok(new
                {
                    success = true,
                    scenarioName = scenario.Name,
                    module = module,
                    columns = columns,
                    typeActionCount = dataEntryActions.Count,
                    data = new object[] { }  // Empty array for user to fill in
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract test data structure from scenario");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new test scenario
        /// </summary>
        [HttpPost]
        public IActionResult CreateScenario([FromBody] TestScenario scenario)
        {
            try
            {
                var manager = new ScenarioManager();
                scenario.CreatedAt = DateTime.Now;
                manager.SaveScenario(scenario);
                
                return Ok(new
                {
                    success = true,
                    message = "Scenario created successfully",
                    scenario
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing scenario
        /// </summary>
        [HttpPut("{module}/{name}")]
        public IActionResult UpdateScenario(string module, string name, [FromBody] TestScenario scenario)
        {
            try
            {
                var manager = new ScenarioManager();
                scenario.Module = module;
                scenario.Name = name;
                scenario.ModifiedAt = DateTime.Now;
                
                // GLOBAL FIX: Clean up before saving
                CleanupLegacyScenarioIssues(scenario);
                
                manager.SaveScenario(scenario);
                
                return Ok(new
                {
                    success = true,
                    message = "Scenario updated successfully",
                    scenario
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Delete a scenario
        /// </summary>
        [HttpDelete("{module}/{name}")]
        public IActionResult DeleteScenario(string module, string name)
        {
            try
            {
                var manager = new ScenarioManager();
                manager.DeleteScenario(name, module);
                
                return Ok(new
                {
                    success = true,
                    message = "Scenario deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Execute a single scenario
        /// </summary>
        [HttpPost("execute/{module}/{name}")]
        public async Task<IActionResult> ExecuteScenario(string module, string name)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", name, "running", "Starting test execution...");
                
                var runner = new ZeroCodeTestRunner(async () =>
                {
                    var driver = await WebDriverFactory.CreateDriverAsync();
                    return (IWebDriver)driver;
                });
                
                var result = await runner.ExecuteScenarioAsync(name, module);

                // Build a serializable DTO to ensure enums and durations are strings for the web UI
                var resultDto = new
                {
                    testCaseId = result.TestCaseId,
                    testCaseName = result.TestCaseName,
                    module = result.Module,
                    status = result.Status.ToString(),
                    startTime = result.StartTime.ToString("o"),
                    endTime = result.EndTime.ToString("o"),
                    duration = result.EndTime != default && result.StartTime != default ? (result.EndTime - result.StartTime).ToString() : "0s",
                    steps = result.Steps.Select(s => new
                    {
                        stepName = s.StepName,
                        description = s.Description,
                        status = s.Status.ToString(),
                        startTime = s.StartTime.ToString("o"),
                        endTime = s.EndTime.ToString("o"),
                        duration = s.EndTime != default && s.StartTime != default ? (s.EndTime - s.StartTime).ToString() : "0s",
                        errorMessage = s.ErrorMessage,
                        screenshotPath = s.ScreenshotPath
                    }).ToList(),
                    retryCount = result.RetryCount,
                    errorMessage = result.ErrorMessage,
                    stackTrace = result.StackTrace,
                    tags = result.Tags
                };

                await _hubContext.Clients.All.SendAsync("ReceiveTestResult", resultDto);

                // Save to execution history
                try
                {
                    var config = FrameworkConfigManager.Instance.FrameworkConfig;
                    var historyEntry = new
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        ScenarioName = name,
                        Module = module,
                        ExecutedAt = DateTime.Now.ToString("o"),
                        Duration = (int)result.Duration.TotalSeconds,
                        Status = result.Status.ToString(),
                        Browser = config.Browser.ToString(),
                        Environment = config.Environment.ToString(),
                        Error = result.ErrorMessage,
                        VideoPath = result.VideoPath,
                        Screenshots = result.Steps
                            .Where(s => !string.IsNullOrEmpty(s.ScreenshotPath))
                            .Select(s => s.ScreenshotPath)
                            .ToList(),
                        Steps = result.Steps.Select(s => new
                        {
                            StepName = s.StepName,
                            Description = s.Description,
                            Status = s.Status.ToString(),
                            Error = s.ErrorMessage,
                            ScreenshotPath = s.ScreenshotPath
                        }).ToList()
                    };

                    // Send to history API
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                    var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("http://localhost:5000/api/history", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Information($"Test execution saved to history: {name}");
                    }
                    else
                    {
                        Log.Warning($"Failed to save history. Status: {response.StatusCode}");
                    }
                }
                catch (Exception historyEx)
                {
                    // Don't fail execution if history save fails
                    Log.Warning($"Failed to save history: {historyEx.Message}");
                }

                return Ok(new
                {
                    success = true,
                    result = resultDto
                });
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", name, "failed", ex.Message);
                
                // Save failed execution to history
                try
                {
                    var historyEntry = new
                    {
                        scenarioName = name,
                        module = module,
                        executedAt = DateTime.Now.ToString("o"),
                        duration = "0s",
                        status = "Failed",
                        error = ex.Message
                    };
                    
                    using var httpClient = new HttpClient();
                    var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                    var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                    await httpClient.PostAsync("http://localhost:5000/api/history", content);
                }
                catch { }
                
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Execute all scenarios in a module
        /// </summary>
        [HttpPost("execute/module/{module}")]
        public async Task<IActionResult> ExecuteModule(string module)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", module, "running", "Starting module execution...");
                
                // Create runner with browser-specific factory for cross-browser support
                var runner = new ZeroCodeTestRunner(
                    driverFactory: async () =>
                    {
                        var driver = await WebDriverFactory.CreateDriverAsync();
                        return (IWebDriver)driver;
                    },
                    browserSpecificDriverFactory: async (browserType) =>
                    {
                        // Create a custom config with the specific browser
                        var config = FrameworkConfigManager.Instance.FrameworkConfig;
                        var customConfig = new AgenticAI.Core.Configuration.FrameworkConfiguration
                        {
                            AutomationFramework = config.AutomationFramework,
                            Browser = browserType, // Override with specific browser
                            OperatingSystem = config.OperatingSystem,
                            Environment = config.Environment,
                            ExecutionMode = config.ExecutionMode,
                            BaseUrl = config.BaseUrl,
                            Headless = config.Headless,
                            EnableVideo = config.EnableVideo,
                            EnableScreenshots = config.EnableScreenshots,
                            EnableTracing = config.EnableTracing,
                            MaxRetryCount = config.MaxRetryCount,
                            TimeoutInSeconds = config.TimeoutInSeconds,
                            ParallelWorkers = config.ParallelWorkers,
                            EnableSelfHealing = config.EnableSelfHealing,
                            ScreenshotPath = config.ScreenshotPath,
                            VideoPath = config.VideoPath,
                            ReportPath = config.ReportPath,
                            LogPath = config.LogPath
                        };
                        
                        var driver = await WebDriverFactory.CreateDriverAsync(customConfig);
                        return (IWebDriver)driver;
                    }
                );
                
                var results = await runner.ExecuteModuleAsync(module);
                
                // Save each test result to history
                var config = FrameworkConfigManager.Instance.FrameworkConfig;
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                foreach (var result in results)
                {
                    try
                    {
                        var historyEntry = new
                        {
                            ExecutionId = Guid.NewGuid().ToString(),
                            ScenarioName = result.TestCaseName,
                            Module = result.Module,
                            ExecutedAt = DateTime.Now.ToString("o"),
                            Duration = (int)result.Duration.TotalSeconds,
                            Status = result.Status.ToString(),
                            Browser = config.Browser.ToString(),
                            Environment = config.Environment.ToString(),
                            Error = result.ErrorMessage,
                            VideoPath = result.VideoPath,
                            Screenshots = result.Steps
                                .Where(s => !string.IsNullOrEmpty(s.ScreenshotPath))
                                .Select(s => s.ScreenshotPath)
                                .ToList(),
                            Steps = result.Steps.Select(s => new
                            {
                                StepName = s.StepName,
                                Description = s.Description,
                                Status = s.Status.ToString(),
                                Error = s.ErrorMessage,
                                ScreenshotPath = s.ScreenshotPath
                            }).ToList()
                        };
                        
                        var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                        var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                        await httpClient.PostAsync("http://localhost:5000/api/history", content);
                    }
                    catch (Exception historyEx)
                    {
                        Log.Warning($"Failed to save history for {result.TestCaseName}: {historyEx.Message}");
                    }
                }
                
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", module, "completed", $"Module execution completed. {results.Count} tests executed.");
                
                return Ok(new
                {
                    success = true,
                    module,
                    count = results.Count,
                    results
                });
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", module, "failed", ex.Message);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Execute scenarios by tag
        /// </summary>
        [HttpPost("execute/tag/{tag}")]
        public async Task<IActionResult> ExecuteByTag(string tag)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", tag, "running", "Starting tagged tests execution...");
                
                // Create runner with browser-specific factory for cross-browser support
                var runner = new ZeroCodeTestRunner(
                    driverFactory: async () =>
                    {
                        var driver = await WebDriverFactory.CreateDriverAsync();
                        return (IWebDriver)driver;
                    },
                    browserSpecificDriverFactory: async (browserType) =>
                    {
                        var config = FrameworkConfigManager.Instance.FrameworkConfig;
                        var customConfig = new AgenticAI.Core.Configuration.FrameworkConfiguration
                        {
                            AutomationFramework = config.AutomationFramework,
                            Browser = browserType,
                            OperatingSystem = config.OperatingSystem,
                            Environment = config.Environment,
                            ExecutionMode = config.ExecutionMode,
                            BaseUrl = config.BaseUrl,
                            Headless = config.Headless,
                            EnableVideo = config.EnableVideo,
                            EnableScreenshots = config.EnableScreenshots,
                            EnableTracing = config.EnableTracing,
                            MaxRetryCount = config.MaxRetryCount,
                            TimeoutInSeconds = config.TimeoutInSeconds,
                            ParallelWorkers = config.ParallelWorkers,
                            EnableSelfHealing = config.EnableSelfHealing,
                            ScreenshotPath = config.ScreenshotPath,
                            VideoPath = config.VideoPath,
                            ReportPath = config.ReportPath,
                            LogPath = config.LogPath
                        };
                        
                        var driver = await WebDriverFactory.CreateDriverAsync(customConfig);
                        return (IWebDriver)driver;
                    }
                );
                
                var results = await runner.ExecuteByTagAsync(tag);
                
                // Save each test result to history
                var config = FrameworkConfigManager.Instance.FrameworkConfig;
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                foreach (var result in results)
                {
                    try
                    {
                        var historyEntry = new
                        {
                            ExecutionId = Guid.NewGuid().ToString(),
                            ScenarioName = result.TestCaseName,
                            Module = result.Module,
                            ExecutedAt = DateTime.Now.ToString("o"),
                            Duration = (int)result.Duration.TotalSeconds,
                            Status = result.Status.ToString(),
                            Browser = config.Browser.ToString(),
                            Environment = config.Environment.ToString(),
                            Error = result.ErrorMessage,
                            VideoPath = result.VideoPath,
                            Screenshots = result.Steps
                                .Where(s => !string.IsNullOrEmpty(s.ScreenshotPath))
                                .Select(s => s.ScreenshotPath)
                                .ToList(),
                            Steps = result.Steps.Select(s => new
                            {
                                StepName = s.StepName,
                                Description = s.Description,
                                Status = s.Status.ToString(),
                                Error = s.ErrorMessage,
                                ScreenshotPath = s.ScreenshotPath
                            }).ToList()
                        };
                        
                        var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                        var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                        await httpClient.PostAsync("http://localhost:5000/api/history", content);
                    }
                    catch (Exception historyEx)
                    {
                        Log.Warning($"Failed to save history for {result.TestCaseName}: {historyEx.Message}");
                    }
                }
                
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", tag, "completed", $"Tagged tests execution completed. {results.Count} tests executed.");
                
                return Ok(new
                {
                    success = true,
                    tag,
                    count = results.Count,
                    results
                });
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", tag, "failed", ex.Message);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Execute all scenarios
        /// </summary>
        [HttpPost("execute/all")]
        public async Task<IActionResult> ExecuteAll()
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", "all", "running", "Starting full test suite execution...");
                
                var runner = new ZeroCodeTestRunner(async () =>
                {
                    var driver = await WebDriverFactory.CreateDriverAsync();
                    return (IWebDriver)driver;
                });
                
                var summary = await runner.ExecuteAllScenariosAsync();
                
                // Save each test result to history
                using var httpClient = new HttpClient();
                foreach (var result in summary.TestResults)
                {
                    try
                    {
                        var historyEntry = new
                        {
                            scenarioName = result.TestCaseName,
                            module = result.Module,
                            executedAt = DateTime.Now.ToString("o"),
                            duration = (int)result.Duration.TotalSeconds,
                            status = result.Status.ToString(),
                            error = result.ErrorMessage,
                            videoPath = result.VideoPath,
                            screenshots = result.Steps
                                .Where(s => !string.IsNullOrEmpty(s.ScreenshotPath))
                                .Select(s => s.ScreenshotPath)
                                .ToList(),
                            steps = result.Steps.Select(s => new
                            {
                                stepName = s.StepName,
                                status = s.Status.ToString(),
                                error = s.ErrorMessage,
                                screenshotPath = s.ScreenshotPath
                            }).ToList()
                        };
                        
                        var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                        var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                        await httpClient.PostAsync("http://localhost:5000/api/history", content);
                    }
                    catch (Exception historyEx)
                    {
                        Log.Warning($"Failed to save history for {result.TestCaseName}: {historyEx.Message}");
                    }
                }
                
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", "all", "completed", "Full test suite execution completed.");
                
                return Ok(new
                {
                    success = true,
                    summary
                });
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate", "all", "failed", ex.Message);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get available modules
        /// </summary>
        [HttpGet("modules")]
        public IActionResult GetModules()
        {
            try
            {
                var manager = new ScenarioManager();
                var scenarios = manager.LoadAllScenarios();
                var modules = scenarios.Select(s => s.Module).Distinct().ToList();
                
                return Ok(new
                {
                    success = true,
                    count = modules.Count,
                    modules
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get available tags
        /// </summary>
        [HttpGet("tags")]
        public IActionResult GetTags()
        {
            try
            {
                var manager = new ScenarioManager();
                var scenarios = manager.LoadAllScenarios();
                var tags = scenarios.SelectMany(s => s.Tags).Distinct().ToList();
                
                return Ok(new
                {
                    success = true,
                    count = tags.Count,
                    tags
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Extract a clean field name from a locator string
        /// Handles CSS selectors (#id, .class), XPath, and HTML attributes
        /// Examples: 
        ///   - #firstName → firstName
        ///   - input[placeholder="first_name"] → first_name
        ///   - input[name="email"] → email
        ///   - //*[@id="userId"] → userId
        /// </summary>
        private string ExtractFieldNameFromLocator(string locator, int fieldIndex)
        {
            if (string.IsNullOrEmpty(locator))
                return $"field_{fieldIndex}";

            locator = locator.Trim();

            // Handle CSS ID selector (#id)
            if (locator.StartsWith("#"))
            {
                var idValue = locator.Substring(1).Split('[', '.', ' ')[0].Trim();
                if (!string.IsNullOrEmpty(idValue))
                    return NormalizeFieldName(idValue);
            }

            // Handle CSS class selector (.class)
            if (locator.StartsWith("."))
            {
                var classValue = locator.Substring(1).Split('[', '.', ' ')[0].Trim();
                if (!string.IsNullOrEmpty(classValue) && !classValue.Equals("form-control", StringComparison.OrdinalIgnoreCase))
                    return NormalizeFieldName(classValue);
            }

            // Try to extract placeholder value from HTML attributes
            var placeholderMatch = System.Text.RegularExpressions.Regex.Match(
                locator, 
                @"placeholder\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (placeholderMatch.Success)
            {
                return NormalizeFieldName(placeholderMatch.Groups[1].Value);
            }

            // Try to extract name attribute value
            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                locator, 
                @"name\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (nameMatch.Success)
            {
                return NormalizeFieldName(nameMatch.Groups[1].Value);
            }

            // Also support unquoted name attributes: [name=email]
            var unquotedNameMatch = System.Text.RegularExpressions.Regex.Match(
                locator,
                @"name\s*=\s*([a-zA-Z0-9_\-@.]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (unquotedNameMatch.Success)
            {
                return NormalizeFieldName(unquotedNameMatch.Groups[1].Value);
            }

            // Try to extract id attribute value from XPath or HTML
            var idMatch = System.Text.RegularExpressions.Regex.Match(
                locator, 
                @"id\s*=\s*[""']([^""']+)[""']|@id\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (idMatch.Success)
            {
                var value = idMatch.Groups[1].Value ?? idMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(value))
                    return NormalizeFieldName(value);
            }

            // Try to extract data-testid value
            var testidMatch = System.Text.RegularExpressions.Regex.Match(
                locator, 
                @"data-(?:testid|test|qa)\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (testidMatch.Success)
            {
                return NormalizeFieldName(testidMatch.Groups[1].Value);
            }

            // Support unquoted data attributes: [data-test=username]
            var unquotedDataMatch = System.Text.RegularExpressions.Regex.Match(
                locator,
                @"data-(?:testid|test|qa)\s*=\s*([a-zA-Z0-9_\-@.]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (unquotedDataMatch.Success)
            {
                return NormalizeFieldName(unquotedDataMatch.Groups[1].Value);
            }

            // Try aria-label text as semantic field name
            var ariaLabelMatch = System.Text.RegularExpressions.Regex.Match(
                locator,
                @"aria-label\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (ariaLabelMatch.Success)
            {
                return NormalizeFieldName(ariaLabelMatch.Groups[1].Value);
            }

            // Fallback to generic name
            return $"field_{fieldIndex}";
        }

        /// <summary>
        /// Normalize a field name to be clean and professional
        /// </summary>
        private string NormalizeFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return fieldName;

            var normalized = fieldName
                .Trim()
                .Replace("-", "_")
                .Replace(" ", "_")
                .ToLower();

            if (normalized.Contains("@") || normalized.Contains("example.com") || normalized.Contains("email"))
                return "email";

            return normalized;
        }
    }
}
