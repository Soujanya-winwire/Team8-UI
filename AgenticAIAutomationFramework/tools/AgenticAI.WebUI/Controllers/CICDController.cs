using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgenticAI.WebUI.Controllers
{
    /// <summary>
    /// CI/CD Dashboard Controller - Provides pipeline status and management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CICDController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CICDController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Get recent GitHub Actions workflow runs
        /// </summary>
        [HttpGet("pipelines")]
        public async Task<IActionResult> GetPipelines()
        {
            try
            {
                var owner = _configuration["GitHub:Owner"] ?? "Soujanya-winwire";
                var repo = _configuration["GitHub:Repo"] ?? "Team8---UI-Automation";
                var token = _configuration["GitHub:Token"];

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AgenticAI-Framework");
                
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                }

                var url = $"https://api.github.com/repos/{owner}/{repo}/actions/runs?per_page=10";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Return mock data if API fails
                    return Ok(new
                    {
                        success = true,
                        message = "Using mock data (configure GitHub token for live data)",
                        pipelines = GetMockPipelines()
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);

                var runs = data.GetProperty("workflow_runs").EnumerateArray().Select(run => new
                {
                    id = run.GetProperty("id").GetInt64(),
                    name = run.GetProperty("name").GetString(),
                    status = run.GetProperty("status").GetString(),
                    conclusion = run.TryGetProperty("conclusion", out var c) ? c.GetString() : null,
                    created_at = run.GetProperty("created_at").GetString(),
                    updated_at = run.GetProperty("updated_at").GetString(),
                    html_url = run.GetProperty("html_url").GetString(),
                    head_branch = run.GetProperty("head_branch").GetString(),
                    head_commit = new
                    {
                        message = run.GetProperty("head_commit").GetProperty("message").GetString(),
                        author = run.GetProperty("head_commit").GetProperty("author").GetProperty("name").GetString()
                    },
                    run_number = run.GetProperty("run_number").GetInt32()
                }).ToList();

                return Ok(new { success = true, pipelines = runs });
            }
            catch (Exception ex)
            {
                // Return mock data on error
                return Ok(new
                {
                    success = true,
                    message = $"Using mock data: {ex.Message}",
                    pipelines = GetMockPipelines()
                });
            }
        }

        /// <summary>
        /// Get CI/CD statistics and metrics
        /// </summary>
        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            try
            {
                // Calculate stats from pipeline data or return mock data
                return Ok(new
                {
                    success = true,
                    stats = new
                    {
                        totalBuilds = 156,
                        successRate = 94.2,
                        averageDuration = "3m 45s",
                        lastBuildStatus = "success",
                        coverage = 78.5,
                        testsRun = 1247,
                        testsPassed = 1189,
                        testsFailed = 58,
                        deploymentsToday = 3,
                        uptime = "99.8%"
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Trigger a new pipeline run
        /// </summary>
        [HttpPost("trigger")]
        public async Task<IActionResult> TriggerPipeline([FromBody] TriggerRequest request)
        {
            try
            {
                var owner = _configuration["GitHub:Owner"] ?? "Soujanya-winwire";
                var repo = _configuration["GitHub:Repo"] ?? "Team8---UI-Automation";
                var token = _configuration["GitHub:Token"];

                if (string.IsNullOrEmpty(token))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "GitHub token not configured. Configure 'GitHub:Token' in user secrets to enable pipeline triggering."
                    });
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AgenticAI-Framework");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                var workflowId = request.WorkflowId ?? "ci-build-test.yml";
                var url = $"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches";
                
                var payload = new
                {
                    @ref = request.Branch ?? "main",
                    inputs = new Dictionary<string, string>()
                };

                var response = await client.PostAsJsonAsync(url, payload);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Ok(new
                    {
                        success = false,
                        message = $"Failed to trigger pipeline: {response.StatusCode}",
                        details = error
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Pipeline triggered successfully! It will appear in the list shortly."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get build trends data for charts
        /// </summary>
        [HttpGet("trends")]
        public IActionResult GetTrends()
        {
            try
            {
                // Mock data - replace with actual historical data
                var trends = new
                {
                    labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
                    successful = new[] { 12, 19, 15, 17, 14, 18, 16 },
                    failed = new[] { 1, 2, 1, 0, 2, 1, 1 },
                    duration = new[] { 3.5, 3.2, 4.1, 3.8, 3.6, 3.9, 3.7 }
                };

                return Ok(new { success = true, trends });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        private List<object> GetMockPipelines()
        {
            return new List<object>
            {
                new
                {
                    id = 1,
                    name = "CI - Build & Test",
                    status = "completed",
                    conclusion = "success",
                    created_at = DateTime.UtcNow.AddHours(-2).ToString("o"),
                    updated_at = DateTime.UtcNow.AddHours(-2).AddMinutes(5).ToString("o"),
                    html_url = "https://github.com/Soujanya-winwire/Team8---UI-Automation/actions",
                    head_branch = "main",
                    head_commit = new
                    {
                        message = "feat: Add cross-browser parallel execution support",
                        author = "Developer"
                    },
                    run_number = 156
                },
                new
                {
                    id = 2,
                    name = "CD - Deploy",
                    status = "completed",
                    conclusion = "success",
                    created_at = DateTime.UtcNow.AddHours(-5).ToString("o"),
                    updated_at = DateTime.UtcNow.AddHours(-5).AddMinutes(3).ToString("o"),
                    html_url = "https://github.com/Soujanya-winwire/Team8---UI-Automation/actions",
                    head_branch = "main",
                    head_commit = new
                    {
                        message = "chore: Update dependencies",
                        author = "Developer"
                    },
                    run_number = 155
                },
                new
                {
                    id = 3,
                    name = "CI - Build & Test",
                    status = "in_progress",
                    conclusion = (string?)null,
                    created_at = DateTime.UtcNow.AddMinutes(-3).ToString("o"),
                    updated_at = DateTime.UtcNow.AddMinutes(-1).ToString("o"),
                    html_url = "https://github.com/Soujanya-winwire/Team8---UI-Automation/actions",
                    head_branch = "develop",
                    head_commit = new
                    {
                        message = "fix: Resolve configuration persistence issue",
                        author = "Developer"
                    },
                    run_number = 154
                }
            };
        }
    }

    public class TriggerRequest
    {
        public string? WorkflowId { get; set; }
        public string? Branch { get; set; }
    }
}
