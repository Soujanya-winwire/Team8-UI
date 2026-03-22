using AgenticAI.Core.Configuration;
using AgenticAI.Core.DataDriven;
using AgenticAI.Core.Logging;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AgenticAI.WebUI.Hubs;

namespace AgenticAI.WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecorderController : ControllerBase
    {
        private static TestRecorder? _activeRecorder;
        private readonly IHubContext<TestExecutionHub> _hubContext;

        public RecorderController(IHubContext<TestExecutionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Start recording a new test scenario
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartRecording([FromBody] StartRecordingRequest request)
        {
            try
            {
                if (_activeRecorder != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "A recording session is already active. Please stop it first."
                    });
                }

                // Validate: Check if scenario with same name already exists across ALL modules (global uniqueness)
                var scenarioManager = new ScenarioManager();
                var allScenarios = scenarioManager.LoadAllScenarios();
                var duplicateScenario = allScenarios.FirstOrDefault(s => 
                    s.Name != null && s.Name.Equals(request.ScenarioName, StringComparison.OrdinalIgnoreCase));

                if (duplicateScenario != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Scenario name '{request.ScenarioName}' already exists in module '{duplicateScenario.Module}'. Please use a different name."
                    });
                }

                _activeRecorder = new TestRecorder(request.ScenarioName, request.Module);
                _activeRecorder.SetScenarioDescription(request.Description);

                foreach (var tag in request.Tags)
                {
                    _activeRecorder.AddTag(tag);
                }

                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate",
                    request.ScenarioName, "recording", "Recording session started");

                // Start recording in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Subscribe to real-time action capture
                        _activeRecorder.OnActionCaptured += (action) =>
                        {
                            _hubContext.Clients.All.SendAsync("ReceiveRecordedAction", action).Wait();
                        };

                        await _activeRecorder.StartRecordingAsync(request.StartUrl);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Recording error: {ex.Message}");
                    }
                });

                return Ok(new
                {
                    success = true,
                    message = "Recording started. Browser opened. Perform your test actions.",
                    scenarioName = request.ScenarioName,
                    module = request.Module
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Stop recording and save the scenario
        /// </summary>
        [HttpPost("stop")]
        public async Task<IActionResult> StopRecording()
        {
            try
            {
                if (_activeRecorder == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "No active recording session found."
                    });
                }

                var scenario = await _activeRecorder.StopRecordingAsync();

                // Ensure scenario has required properties
                if (scenario == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Failed to retrieve scenario from recorder."
                    });
                }

                // Initialize collections if null
                if (scenario.Actions == null)
                {
                    scenario.Actions = new List<RecordedAction>();
                }

                if (scenario.Assertions == null)
                {
                    scenario.Assertions = new List<Assertion>();
                }

                if (scenario.Tags == null)
                {
                    scenario.Tags = new List<string>();
                }

                // Set timestamps
                scenario.CreatedAt = DateTime.Now;
                scenario.ModifiedAt = null;

                // Auto-generate safe assertions from recorded actions (non-breaking: preserve existing/manual assertions)
                var autoAssertionCount = AppendAutoAssertionsFromActions(scenario);
                if (autoAssertionCount > 0)
                {
                    Logger.Info($"Auto-generated {autoAssertionCount} assertion(s) for recorded actions in scenario '{scenario.Name}'");
                }

                // Save the scenario
                try
                {
                    var manager = new ScenarioManager();
                    manager.SaveScenario(scenario);
                }
                catch (Exception saveEx)
                {
                    Logger.Error($"Failed to save scenario: {saveEx.Message}");
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Recording stopped but failed to save: {saveEx.Message}"
                    });
                }

                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate",
                    scenario.Name, "completed", "Recording completed and saved");

                var result = new
                {
                    success = true,
                    message = "Recording stopped and scenario saved successfully",
                    scenario = new
                    {
                        scenarioId = scenario.ScenarioId ?? Guid.NewGuid().ToString(),
                        name = scenario.Name ?? "Unnamed Test",
                        module = scenario.Module ?? "Default",
                        description = scenario.Description ?? "",
                        startUrl = scenario.StartUrl ?? "",
                        tags = scenario.Tags ?? new List<string>(),
                        actions = scenario.Actions ?? new List<RecordedAction>(),
                        assertions = scenario.Assertions ?? new List<Assertion>(),
                        actionCount = scenario.Actions?.Count ?? 0,
                        assertionCount = scenario.Assertions?.Count ?? 0,
                        createdAt = scenario.CreatedAt,
                        modifiedAt = scenario.ModifiedAt
                    }
                };

                _activeRecorder = null;

                return Ok(result);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error stopping recording: {ex.Message}");
                _activeRecorder = null;
                return BadRequest(new
                {
                    success = false,
                    error = $"Failed to stop recording: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Add a manual action during recording
        /// </summary>
        [HttpPost("action")]
        public IActionResult AddAction([FromBody] RecordedAction action)
        {
            try
            {
                if (_activeRecorder == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "No active recording session."
                    });
                }

                _activeRecorder.AddAction(
                    action.ActionType,
                    action.Locator,
                    action.Value,
                    action.Description
                );

                return Ok(new
                {
                    success = true,
                    message = "Action added to recording"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Add an assertion during recording
        /// </summary>
        [HttpPost("assertion")]
        public IActionResult AddAssertion([FromBody] Assertion assertion)
        {
            try
            {
                if (_activeRecorder == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "No active recording session."
                    });
                }

                _activeRecorder.AddAssertion(
                    assertion.Type,
                    assertion.Locator,
                    assertion.ExpectedValue,
                    assertion.Description,
                    assertion.ExecuteAfterActionIndex
                );

                return Ok(new
                {
                    success = true,
                    message = "Assertion added to recording"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get recording status
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetRecordingStatus()
        {
            return Ok(new
            {
                success = true,
                isRecording = _activeRecorder != null,
                message = _activeRecorder != null
                    ? "Recording session is active"
                    : "No active recording session"
            });
        }

        /// <summary>
        /// Start Playwright Codegen for automatic recording
        /// </summary>
        [HttpPost("start-codegen")]
        public async Task<IActionResult> StartCodegen([FromBody] CodegenStartRequest request)
        {
            try {
                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate",
                    "Codegen", "info", "Starting Playwright Codegen...");

                // Build the command
                var command = $"playwright codegen {request.StartUrl}";
                if (!string.IsNullOrEmpty(request.Browser))
                {
                    command += $" --browser={request.Browser}";
                }

                // Execute the command in a new process
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                System.Diagnostics.Process.Start(processStartInfo);

                Logger.Info($"Playwright Codegen started for: {request.StartUrl}");

                return Ok(new
                {
                    success = true,
                    message = "Playwright Codegen started successfully",
                    command = command,
                    scenarioName = request.ScenarioName,
                    module = request.Module
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start Codegen: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Save a test scenario created via Codegen
        /// </summary>
        [HttpPost("save-codegen")]
        public async Task<IActionResult> SaveCodegenTest([FromBody] SaveCodegenRequest request)
        {
            try
            {
                // Create a test scenario with the provided information
                var scenario = new TestScenario
                {
                    ScenarioId = Guid.NewGuid().ToString(),
                    Name = request.TestName,
                    Module = request.Module,
                    Description = request.Description,
                    StartUrl = request.StartUrl,
                    Tags = request.Tags ?? new List<string>(),
                    Actions = new List<RecordedAction>(),  // Will be added by user from Codegen output
                    Assertions = new List<Assertion>(),
                    CreatedAt = DateTime.Now,
                    ModifiedAt = null
                };

                // Save the scenario
                var manager = new ScenarioManager();
                manager.SaveScenario(scenario);

                await _hubContext.Clients.All.SendAsync("ReceiveTestUpdate",
                    scenario.Name, "completed", "Test scenario saved");

                Logger.Info($"Codegen test saved: {scenario.Name}");

                return Ok(new
                {
                    success = true,
                    message = "Test scenario saved successfully",
                    scenario = new
                    {
                        scenarioId = scenario.ScenarioId,
                        name = scenario.Name,
                        module = scenario.Module,
                        description = scenario.Description
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save Codegen test: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
        
        /// <summary>
        /// Self-test endpoint - runs an automated recording session to verify the capture pipeline
        /// </summary>
        [HttpPost("selftest")]
        public async Task<IActionResult> SelfTest()
        {
            try
            {
                Logger.Info("Starting recorder self-test...");
                
                var capturedActions = new List<RecordedAction>();
                var recorder = new TestRecorder("SelfTest_Recording", "AutoVerification");
                
                recorder.OnActionCaptured += (action) =>
                {
                    capturedActions.Add(action);
                    Logger.Info($"Self-test captured: {action.ActionType} on {action.Locator}");
                };
                
                // Start recording (opens browser, navigates, captures Navigate action automatically)
                await recorder.StartRecordingAsync("https://rahulshettyacademy.com/AutomationPractice/");
                
                // Wait for initial page load
                await Task.Delay(3000);
                
                // Stop recording
                var scenario = await recorder.StopRecordingAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Self-test completed",
                    capturedCount = capturedActions.Count,
                    scenarioActionCount = scenario.Actions?.Count ?? 0,
                    actions = capturedActions.Select(a => new { a.ActionType, a.Locator, a.Value, a.Description })
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Self-test error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        private static int AppendAutoAssertionsFromActions(TestScenario scenario)
        {
            if (scenario.Actions == null || scenario.Actions.Count == 0)
            {
                return 0;
            }

            scenario.Assertions ??= new List<Assertion>();

            var existingKeys = new HashSet<string>(
                scenario.Assertions.Select(GetAssertionDedupKey),
                StringComparer.OrdinalIgnoreCase);

            int addedCount = 0;

            for (int actionIndex = 0; actionIndex < scenario.Actions.Count; actionIndex++)
            {
                var action = scenario.Actions[actionIndex];
                var generatedList = BuildAutoAssertions(action, actionIndex);
                
                foreach (var generated in generatedList)
                {
                    var key = GetAssertionDedupKey(generated);
                    if (existingKeys.Contains(key))
                    {
                        continue;
                    }

                    scenario.Assertions.Add(generated);
                    existingKeys.Add(key);
                    addedCount++;
                }
            }

            return addedCount;
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
            return string.Join("|",
                assertion.Type ?? string.Empty,
                assertion.Locator ?? string.Empty,
                assertion.ExpectedValue ?? string.Empty,
                assertion.ExecuteAfterActionIndex?.ToString() ?? "none");
        }
    }

    public class StartRecordingRequest
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Module { get; set; } = "Default";
        public string Description { get; set; } = string.Empty;
        public string StartUrl { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class CodegenStartRequest
    {
        public string StartUrl { get; set; } = string.Empty;
        public string Browser { get; set; } = "chromium";
        public string ScenarioName { get; set; } = string.Empty;
        public string Module { get; set; } = "Default";
    }

    public class SaveCodegenRequest
    {
        public string TestName { get; set; } = string.Empty;
        public string Module { get; set; } = "Default";
        public string Description { get; set; } = string.Empty;
        public string StartUrl { get; set; } = string.Empty;
        public string Browser { get; set; } = "chromium";
        public List<string>? Tags { get; set; }
    }
}
