using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgenticAI.WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly string _historyFilePath;
        private readonly int _retentionDays = 365; // 1 year retention

        public HistoryController()
        {
            var historyDir = Path.Combine(Directory.GetCurrentDirectory(), "TestHistory");
            if (!Directory.Exists(historyDir))
            {
                Directory.CreateDirectory(historyDir);
            }
            _historyFilePath = Path.Combine(historyDir, "execution-history.json");
        }

        /// <summary>
        /// Get all test execution history (last 1 year)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var history = await LoadHistoryAsync();
                
                // Filter to last year only
                var oneYearAgo = DateTime.Now.AddDays(-_retentionDays);
                var filteredHistory = history
                    .Where(h => DateTime.Parse(h.ExecutedAt) >= oneYearAgo)
                    .OrderByDescending(h => h.ExecutedAt)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    history = filteredHistory,
                    count = filteredHistory.Count
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = true,
                    history = new List<TestExecutionHistory>(),
                    count = 0
                });
            }
        }

        /// <summary>
        /// Get execution history for a specific scenario
        /// </summary>
        [HttpGet("{scenarioName}")]
        public async Task<IActionResult> GetScenarioHistory(string scenarioName)
        {
            try
            {
                var history = await LoadHistoryAsync();
                
                var scenarioHistory = history
                    .Where(h => h.ScenarioName.Equals(scenarioName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(h => h.ExecutedAt)
                    .Take(50) // Last 50 executions
                    .ToList();

                return Ok(new
                {
                    success = true,
                    history = scenarioHistory,
                    count = scenarioHistory.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = $"Failed to load scenario history: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Save a new test execution result
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveExecution([FromBody] TestExecutionHistory execution)
        {
            try
            {
                var history = await LoadHistoryAsync();
                
                // Add new execution
                execution.ExecutedAt = DateTime.Now.ToString("o");
                history.Insert(0, execution);

                // Clean up old records (> 1 year)
                var oneYearAgo = DateTime.Now.AddDays(-_retentionDays);
                history = history
                    .Where(h => DateTime.Parse(h.ExecutedAt) >= oneYearAgo)
                    .ToList();

                // Save updated history
                await SaveHistoryAsync(history);

                return Ok(new
                {
                    success = true,
                    message = "Execution saved to history",
                    executionId = execution.ExecutionId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = $"Failed to save execution: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Delete a specific execution from history
        /// </summary>
        [HttpDelete("{executionId}")]
        public async Task<IActionResult> DeleteExecution(string executionId)
        {
            try
            {
                var history = await LoadHistoryAsync();
                
                var removed = history.RemoveAll(h => h.ExecutionId == executionId);
                
                if (removed > 0)
                {
                    await SaveHistoryAsync(history);
                    return Ok(new { success = true, message = "Execution deleted" });
                }
                else
                {
                    return NotFound(new { success = false, error = "Execution not found" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = $"Failed to delete execution: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Clean up old records (automatically removes > 1 year)
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupHistory()
        {
            try
            {
                var history = await LoadHistoryAsync();
                var originalCount = history.Count;

                // Keep only last year
                var oneYearAgo = DateTime.Now.AddDays(-_retentionDays);
                history = history
                    .Where(h => DateTime.Parse(h.ExecutedAt) >= oneYearAgo)
                    .ToList();

                await SaveHistoryAsync(history);

                var removedCount = originalCount - history.Count;

                return Ok(new
                {
                    success = true,
                    message = $"Removed {removedCount} old records",
                    removed = removedCount,
                    remaining = history.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = $"Failed to cleanup history: {ex.Message}"
                });
            }
        }

        // Private helper methods
        private async Task<List<TestExecutionHistory>> LoadHistoryAsync()
        {
            if (!System.IO.File.Exists(_historyFilePath))
            {
                return new List<TestExecutionHistory>();
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(_historyFilePath);
                var history = JsonSerializer.Deserialize<List<TestExecutionHistory>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return history ?? new List<TestExecutionHistory>();
            }
            catch
            {
                return new List<TestExecutionHistory>();
            }
        }

        private async Task SaveHistoryAsync(List<TestExecutionHistory> history)
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await System.IO.File.WriteAllTextAsync(_historyFilePath, json);
        }
    }

    // Model for test execution history
    public class TestExecutionHistory
    {
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public string ScenarioName { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string ExecutedAt { get; set; } = DateTime.Now.ToString("o");
        public int Duration { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Browser { get; set; } = "Chrome";
        public string Environment { get; set; } = "QA";
        public List<StepResult>? Steps { get; set; }
        public string? Error { get; set; }
        public string? VideoPath { get; set; }
        public List<string>? Screenshots { get; set; }
    }

    public class StepResult
    {
        public string StepName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? ScreenshotPath { get; set; }
    }
}
