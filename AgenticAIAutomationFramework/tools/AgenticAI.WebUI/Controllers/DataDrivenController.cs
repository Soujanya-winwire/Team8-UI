using AgenticAI.Core.DataDriven;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.ZeroCode;
using AgenticAI.UIAutomation.Drivers;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AgenticAI.WebUI.Controllers
{
    /// <summary>
    /// API endpoints for data-driven Selenium test execution
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DataDrivenController : ControllerBase
    {
        // ──────────────────────────────────────────────
        // POST api/datadriven/preview
        // ──────────────────────────────────────────────
        /// <summary>
        /// Parse a CSV or JSON data set and return column headers + row count (no execution)
        /// </summary>
        [HttpPost("preview")]
        public IActionResult Preview([FromBody] DataPreviewRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DataContent))
                    return BadRequest(new { success = false, error = "Data content is required." });

                var dataSet = ParseDataSet(request.DataFormat, request.DataContent);

                return Ok(new
                {
                    success = true,
                    columns = dataSet.Columns,
                    rowCount = dataSet.RowCount,
                    preview = dataSet.Rows.Take(3).Select(r => r).ToList()
                });
            }
            catch (Exception ex)
            {
                Log.Warning("DataDriven preview failed: {Message}", ex.Message);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ──────────────────────────────────────────────
        // POST api/datadriven/execute
        // ──────────────────────────────────────────────
        /// <summary>
        /// Execute a test scenario once per data row, substituting ${ColumnName} placeholders
        /// </summary>
        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] DataDrivenExecuteRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ScenarioName))
                    return BadRequest(new { success = false, error = "ScenarioName is required." });
                if (string.IsNullOrWhiteSpace(request.Module))
                    return BadRequest(new { success = false, error = "Module is required." });
                if (string.IsNullOrWhiteSpace(request.DataContent))
                    return BadRequest(new { success = false, error = "DataContent is required." });

                // Load scenario
                var manager = new ScenarioManager();
                var scenario = manager.LoadScenario(request.ScenarioName, request.Module);

                if (scenario == null)
                    return NotFound(new
                    {
                        success = false,
                        error = $"Scenario '{request.ScenarioName}' not found in module '{request.Module}'"
                    });

                // Parse data set
                var dataSet = ParseDataSet(request.DataFormat, request.DataContent);

                if (dataSet.RowCount == 0)
                    return BadRequest(new { success = false, error = "Data set contains no rows." });

                Log.Information("DataDriven execute: scenario={Scenario}, rows={Rows}", request.ScenarioName, dataSet.RowCount);

                // Execute
                var runner = new DataDrivenRunner(async () =>
                {
                    var driver = await WebDriverFactory.CreateDriverAsync();
                    return (IWebDriver)driver;
                });

                var results = await runner.RunAsync(scenario, dataSet);

                // Save each result to history
                await SaveDataDrivenResultsToHistory(results, scenario, request);

                // Build serializable response
                var resultDtos = results.Select(r => new
                {
                    rowIndex = r.RowIndex,
                    rowNumber = r.RowIndex + 1,
                    dataRow = r.DataRow,
                    testCaseName = r.Result.TestCaseName,
                    status = r.Result.Status.ToString(),
                    startTime = r.Result.StartTime.ToString("o"),
                    endTime = r.Result.EndTime.ToString("o"),
                    duration = r.Result.EndTime != default && r.Result.StartTime != default
                        ? (r.Result.EndTime - r.Result.StartTime).TotalSeconds.ToString("F2") + "s"
                        : "0s",
                    errorMessage = r.Result.ErrorMessage,
                    steps = r.Result.Steps.Select(s => new
                    {
                        stepName = s.StepName,
                        description = s.Description,
                        status = s.Status.ToString(),
                        errorMessage = s.ErrorMessage,
                        screenshotPath = s.ScreenshotPath
                    }).ToList()
                }).ToList();

                int passed = results.Count(r => r.Result.Status == Core.Enums.TestStatus.Passed);
                int failed = results.Count(r => r.Result.Status != Core.Enums.TestStatus.Passed);

                return Ok(new
                {
                    success = true,
                    scenarioName = request.ScenarioName,
                    module = request.Module,
                    totalRows = dataSet.RowCount,
                    passed,
                    failed,
                    results = resultDtos
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DataDriven execute error");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Save each data-driven test result to execution history
        /// </summary>
        private async Task SaveDataDrivenResultsToHistory(
            List<DataDrivenResult> results, 
            Core.ZeroCode.Models.TestScenario scenario,
            DataDrivenExecuteRequest request)
        {
            var historyDir = Path.Combine(Directory.GetCurrentDirectory(), "TestHistory");
            if (!Directory.Exists(historyDir))
            {
                Directory.CreateDirectory(historyDir);
            }
            var historyFilePath = Path.Combine(historyDir, "execution-history.json");
            
            try
            {
                // Load existing history
                List<TestExecutionHistory> history;
                if (System.IO.File.Exists(historyFilePath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(historyFilePath);
                    history = System.Text.Json.JsonSerializer.Deserialize<List<TestExecutionHistory>>(json, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                        ?? new List<TestExecutionHistory>();
                }
                else
                {
                    history = new List<TestExecutionHistory>();
                }
                
                var browser = GetBrowserFromConfig();
                var environment = GetEnvironmentFromConfig();
                
                foreach (var result in results)
                {
                    // Format the test name to include row number and data
                    var dataInfo = string.Join(", ", result.DataRow.Take(2).Select(kv => $"{kv.Key}={kv.Value}"));
                    var testName = $"{scenario.Name} [Row {result.RowIndex + 1}: {dataInfo}]";
                    
                    // Calculate duration
                    var durationSeconds = 0.0;
                    if (result.Result.EndTime != default && result.Result.StartTime != default)
                    {
                        durationSeconds = (result.Result.EndTime - result.Result.StartTime).TotalSeconds;
                    }
                    
                    // Create history entry
                    var historyEntry = new TestExecutionHistory
                    {
                        ExecutionId = Guid.NewGuid().ToString(),
                        ScenarioName = testName,
                        Module = scenario.Module,
                        ExecutedAt = DateTime.Now.ToString("o"),
                        Duration = (int)durationSeconds,
                        Status = result.Result.Status.ToString(),
                        Browser = browser,
                        Environment = environment,
                        Steps = result.Result.Steps.Select(s => new StepResult
                        {
                            StepName = s.StepName,
                            Description = s.Description,
                            Status = s.Status.ToString(),
                            Error = s.ErrorMessage,
                            ScreenshotPath = s.ScreenshotPath
                        }).ToList(),
                        Error = result.Result.ErrorMessage,
                        Screenshots = result.Result.Steps
                            .Where(s => !string.IsNullOrEmpty(s.ScreenshotPath))
                            .Select(s => s.ScreenshotPath)
                            .ToList()
                    };
                    
                    // Add to beginning of list (most recent first)
                    history.Insert(0, historyEntry);
                }
                
                // Clean up old records (keep last 365 days)
                var oneYearAgo = DateTime.Now.AddDays(-365);
                history = history
                    .Where(h => DateTime.Parse(h.ExecutedAt) >= oneYearAgo)
                    .ToList();
                
                // Save updated history
                var updatedJson = System.Text.Json.JsonSerializer.Serialize(history, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(historyFilePath, updatedJson);
                
                Log.Information("✅ Saved {Count} data-driven results to history", results.Count);
            }
            catch (Exception ex)
            {
                Log.Warning("⚠️ Failed to save data-driven results to history: {Message}", ex.Message);
                // Don't fail the entire execution if history save fails
            }
        }

        /// <summary>
        /// Get browser from configuration
        /// </summary>
        private string GetBrowserFromConfig()
        {
            try
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "framework-config.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (config != null && config.ContainsKey("Browser"))
                    {
                        return config["Browser"]?.ToString() ?? "Chrome";
                    }
                }
            }
            catch { }
            
            return "Chrome";
        }

        /// <summary>
        /// Get environment from configuration
        /// </summary>
        private string GetEnvironmentFromConfig()
        {
            try
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "framework-config.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (config != null && config.ContainsKey("Environment"))
                    {
                        return config["Environment"]?.ToString() ?? "QA";
                    }
                }
            }
            catch { }
            
            return "QA";
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────
        private static DataTestSet ParseDataSet(string format, string content)
        {
            return (format ?? "CSV").ToUpperInvariant() switch
            {
                "JSON" => DataSetReader.ParseJson(content),
                _ => DataSetReader.ParseCsv(content)
            };
        }
    }

    // ──────────────────────────────────────────────
    // Request DTOs
    // ──────────────────────────────────────────────
    public class DataPreviewRequest
    {
        /// <summary>CSV or JSON</summary>
        public string DataFormat { get; set; } = "CSV";
        public string DataContent { get; set; } = string.Empty;
    }

    public class DataDrivenExecuteRequest
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        /// <summary>CSV or JSON</summary>
        public string DataFormat { get; set; } = "CSV";
        public string DataContent { get; set; } = string.Empty;
    }
}
