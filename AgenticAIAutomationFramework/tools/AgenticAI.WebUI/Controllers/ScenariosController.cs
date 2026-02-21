using AgenticAI.Core.Interfaces;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.UIAutomation.Drivers;
using AgenticAI.WebUI.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

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
                    scenarios = scenarios.Select(s => new
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
                
                await _hubContext.Clients.All.SendAsync("ReceiveTestResult", result);
                
                // Save to execution history
                try
                {
                    var historyEntry = new
                    {
                        scenarioName = name,
                        module = module,
                        executedAt = DateTime.Now.ToString("o"),
                        // result.Duration is a TimeSpan — format it as a string
                        duration = result.Duration.ToString(),
                        status = result.Status.ToString(),
                        error = result.ErrorMessage
                    };
                    
                    // Send to history API
                    using var httpClient = new HttpClient();
                    var historyJson = System.Text.Json.JsonSerializer.Serialize(historyEntry);
                    var content = new StringContent(historyJson, System.Text.Encoding.UTF8, "application/json");
                    await httpClient.PostAsync("http://localhost:5000/api/history", content);
                }
                catch (Exception historyEx)
                {
                    // Don't fail execution if history save fails
                    Console.WriteLine($"Failed to save history: {historyEx.Message}");
                }
                
                return Ok(new
                {
                    success = true,
                    result
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
                
                var runner = new ZeroCodeTestRunner(async () =>
                {
                    var driver = await WebDriverFactory.CreateDriverAsync();
                    return (IWebDriver)driver;
                });
                
                var results = await runner.ExecuteModuleAsync(module);
                
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
                
                var runner = new ZeroCodeTestRunner(async () =>
                {
                    var driver = await WebDriverFactory.CreateDriverAsync();
                    return (IWebDriver)driver;
                });
                
                var results = await runner.ExecuteByTagAsync(tag);
                
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
    }
}
