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
                        errorMessage = s.ErrorMessage
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
