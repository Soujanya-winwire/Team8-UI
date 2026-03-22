using AgenticAI.Core.Interfaces;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Configuration;
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
                
                return Ok(new { success = true, scenario });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
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
                        columnName = ExtractFieldNameFromLocator(action.Locator, columns.Count + 1);
                    }
                    
                    if (!columns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                    {
                        columns.Add(columnName);
                    }
                }

                // Add standard columns if not already present
                if (!columns.Contains("tag", StringComparer.OrdinalIgnoreCase))
                    columns.Add("tag");

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
