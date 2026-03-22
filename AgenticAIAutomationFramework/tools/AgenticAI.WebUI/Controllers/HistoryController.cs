using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
        /// Delete multiple test executions (bulk delete)
        /// </summary>
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteMultipleExecutions([FromBody] DeleteTestsRequest request)
        {
            try
            {
                if (request == null || request.Tests == null || request.Tests.Count == 0)
                {
                    return BadRequest(new { success = false, error = "No tests specified for deletion" });
                }

                var history = await LoadHistoryAsync();
                var originalCount = history.Count;
                
                // Remove tests that match the scenarioName and executedAt criteria
                foreach (var testToDelete in request.Tests)
                {
                    history.RemoveAll(h => 
                        h.ScenarioName == testToDelete.ScenarioName && 
                        h.ExecutedAt == testToDelete.ExecutedAt);
                }
                
                var deletedCount = originalCount - history.Count;
                
                if (deletedCount > 0)
                {
                    await SaveHistoryAsync(history);
                    return Ok(new 
                    { 
                        success = true, 
                        deletedCount = deletedCount,
                        message = $"Successfully deleted {deletedCount} test result{(deletedCount > 1 ? "s" : "")}" 
                    });
                }
                else
                {
                    return NotFound(new { success = false, error = "No matching test results found" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = $"Failed to delete test results: {ex.Message}"
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

        [HttpPost("export-html")]
        public async Task<IActionResult> ExportSelectedAsHtml([FromBody] DeleteTestsRequest request)
        {
            try
            {
                if (request?.Tests == null || request.Tests.Count == 0)
                {
                    return BadRequest(new { success = false, error = "Select at least one test result to export." });
                }

                var history = await LoadHistoryAsync();
                var selected = MatchSelectedExecutions(history, request.Tests);

                if (selected.Count == 0)
                {
                    return NotFound(new { success = false, error = "No matching test results found for export." });
                }

                var html = BuildInteractiveHtmlReport(selected);
                var bytes = Encoding.UTF8.GetBytes(html);
                var fileName = $"test-results-{DateTime.Now:yyyyMMdd-HHmmss}.html";

                return File(bytes, "text/html; charset=utf-8", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = $"Failed to export HTML report: {ex.Message}" });
            }
        }

        [HttpPost("export-extent")]
        public async Task<IActionResult> ExportSelectedAsExtent([FromBody] DeleteTestsRequest request)
        {
            try
            {
                if (request?.Tests == null || request.Tests.Count == 0)
                    return BadRequest(new { success = false, error = "Select at least one test result to export." });

                var history = await LoadHistoryAsync();
                var selected = MatchSelectedExecutions(history, request.Tests);

                if (selected.Count == 0)
                    return NotFound(new { success = false, error = "No matching test results found for export." });

                var html = BuildExtentReport(selected);
                var bytes = Encoding.UTF8.GetBytes(html);
                var fileName = $"extent-report-{DateTime.Now:yyyyMMdd-HHmmss}.html";
                return File(bytes, "text/html; charset=utf-8", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = $"Failed to export Extent report: {ex.Message}" });
            }
        }

        // Private helper methods
        private List<TestExecutionHistory> MatchSelectedExecutions(List<TestExecutionHistory> history, List<TestIdentifier> selectedTests)
        {
            var selectedSet = new HashSet<string>(selectedTests
                .Where(t => !string.IsNullOrWhiteSpace(t.ScenarioName) && !string.IsNullOrWhiteSpace(t.ExecutedAt))
                .Select(t => $"{t.ScenarioName}|{t.ExecutedAt}"), StringComparer.OrdinalIgnoreCase);

            return history
                .Where(h => selectedSet.Contains($"{h.ScenarioName}|{h.ExecutedAt}"))
                .OrderByDescending(h => h.ExecutedAt)
                .ToList();
        }

        private string BuildInteractiveHtmlReport(List<TestExecutionHistory> selected)
        {
            var rows = new StringBuilder();
                        var passedCount = selected.Count(x => string.Equals(x.Status, "Passed", StringComparison.OrdinalIgnoreCase));
                        var failedCount = selected.Count(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase));
                        var otherCount = selected.Count - passedCount - failedCount;
                        var avgDuration = selected.Count == 0 ? 0 : Math.Round(selected.Average(x => Math.Max(0, x.Duration)), 2);

            foreach (var item in selected)
            {
                var statusClass = item.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase)
                    ? "passed"
                    : item.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "failed" : "other";

                                var module = string.IsNullOrWhiteSpace(item.Module) ? "N/A" : item.Module;
                                var browser = string.IsNullOrWhiteSpace(item.Browser) ? "Chrome" : item.Browser;
                                var environment = string.IsNullOrWhiteSpace(item.Environment) ? "QA" : item.Environment;
                                var status = string.IsNullOrWhiteSpace(item.Status) ? "Unknown" : item.Status;

                var stepDetails = item.Steps == null || item.Steps.Count == 0
                    ? "<div class='muted'>No step logs available</div>"
                    : string.Join("", item.Steps.Select(step =>
                        $"<div class='step {WebUtility.HtmlEncode((step.Status ?? string.Empty).ToLowerInvariant())}'><strong>{WebUtility.HtmlEncode(step.StepName)}</strong> - {WebUtility.HtmlEncode(step.Status)}{(string.IsNullOrWhiteSpace(step.Error) ? string.Empty : $"<div class='err'>{WebUtility.HtmlEncode(step.Error)}</div>")}</div>"));

                rows.Append($@"
                    <tr data-status=""{WebUtility.HtmlEncode(item.Status ?? string.Empty)}"">
                        <td>{WebUtility.HtmlEncode(item.ScenarioName)}</td>
                                                <td>{WebUtility.HtmlEncode(module)}</td>
                                                <td><span class=""badge {statusClass}"">{WebUtility.HtmlEncode(status)}</span></td>
                        <td>{item.Duration}s</td>
                                                <td>{WebUtility.HtmlEncode(browser)}</td>
                                                <td>{WebUtility.HtmlEncode(environment)}</td>
                        <td>{WebUtility.HtmlEncode(item.ExecutedAt)}</td>
                        <td>
                            <details>
                                                                <summary>View Steps</summary>
                                <div class=""steps"">{stepDetails}</div>
                            </details>
                        </td>
                    </tr>");
            }

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
    <title>WinUITest - Test Results Report</title>
  <style>
        :root {{
            --bg: #f5f7fb;
            --panel: #ffffff;
            --text: #0f172a;
            --muted: #64748b;
            --border: #e2e8f0;
            --blue: #1d4ed8;
            --red: #dc2626;
            --green: #059669;
            --amber: #d97706;
        }}
        * {{ box-sizing: border-box; }}
        body {{ font-family: Segoe UI, Arial, sans-serif; margin: 0; background: var(--bg); color: var(--text); }}
        .container {{ max-width: 1320px; margin: 0 auto; padding: 20px; }}
        .report-header {{
            background: linear-gradient(135deg, #eff6ff, #f8fafc);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 16px 18px;
            margin-bottom: 14px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
            flex-wrap: wrap;
        }}
        .title {{ margin: 0; font-size: 1.2rem; font-weight: 700; }}
        .meta {{ color: var(--muted); font-size: 0.9rem; margin-top: 4px; }}
        .brand-winui {{ color: var(--blue); }}
        .brand-test {{ color: var(--red); }}
        .stats {{ display: grid; grid-template-columns: repeat(5, minmax(140px, 1fr)); gap: 10px; margin-bottom: 14px; }}
        .stat {{ background: var(--panel); border: 1px solid var(--border); border-radius: 10px; padding: 12px; }}
        .stat .k {{ font-size: 1.2rem; font-weight: 700; }}
        .stat .l {{ font-size: 0.82rem; color: var(--muted); margin-top: 2px; }}
        .toolbar {{ background: var(--panel); border: 1px solid var(--border); border-radius: 10px; padding: 10px; margin-bottom: 10px; display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }}
        input, select {{ padding: 8px 10px; border: 1px solid #cbd5e1; border-radius: 6px; background: #fff; min-height: 36px; }}
        .table-wrap {{ background: var(--panel); border: 1px solid var(--border); border-radius: 10px; overflow: auto; }}
        table {{ width: 100%; border-collapse: collapse; min-width: 980px; }}
        th, td {{ padding: 10px; border-bottom: 1px solid #edf2f7; text-align: left; vertical-align: top; font-size: 0.92rem; }}
        th {{ background: #f8fafc; font-weight: 600; color: #334155; position: sticky; top: 0; z-index: 1; }}
        .badge {{ padding: 3px 9px; border-radius: 999px; color: #fff; font-size: 11px; font-weight: 700; letter-spacing: 0.2px; }}
        .passed {{ background: var(--green); }}
        .failed {{ background: var(--red); }}
        .other {{ background: var(--amber); }}
        details > summary {{ cursor: pointer; color: var(--blue); font-weight: 600; }}
        .steps {{ margin-top: 8px; padding: 8px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; max-width: 560px; }}
        .step {{ padding: 6px 0; border-bottom: 1px dashed #e5e7eb; }}
        .step:last-child {{ border-bottom: none; }}
        .err {{ color: #b91c1c; margin-top: 4px; }}
        .muted {{ color: var(--muted); }}
        .footer {{ color: var(--muted); font-size: 0.82rem; margin-top: 10px; }}
        @media (max-width: 1024px) {{ .stats {{ grid-template-columns: repeat(2, minmax(140px, 1fr)); }} }}
        @media (max-width: 640px) {{ .stats {{ grid-template-columns: 1fr; }} .container {{ padding: 12px; }} }}
  </style>
</head>
<body>
    <div class=""container"">
        <div class=""report-header"">
            <div>
                <h1 class=""title""><span class=""brand-winui"">WinUI</span><span class=""brand-test"">Test</span> Results Report</h1>
                <div class=""meta"">Generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss} • Interactive HTML</div>
            </div>
            <div class=""meta""><strong>{selected.Count}</strong> selected result(s)</div>
        </div>

        <div class=""stats"">
            <div class=""stat""><div class=""k"">{selected.Count}</div><div class=""l"">Total</div></div>
            <div class=""stat""><div class=""k"">{passedCount}</div><div class=""l"">Passed</div></div>
            <div class=""stat""><div class=""k"">{failedCount}</div><div class=""l"">Failed</div></div>
            <div class=""stat""><div class=""k"">{otherCount}</div><div class=""l"">Other</div></div>
            <div class=""stat""><div class=""k"">{avgDuration:F2}s</div><div class=""l"">Average Duration</div></div>
        </div>

        <div class=""toolbar"">
            <input id=""search"" placeholder=""Search by test/module/browser/environment"" style=""min-width:280px;flex:1;"" />
            <select id=""statusFilter"" style=""min-width:180px;"">
                <option value="""">All Statuses</option>
                <option value=""Passed"">Passed</option>
                <option value=""Failed"">Failed</option>
                <option value=""Skipped"">Skipped</option>
            </select>
            <span class=""meta"" id=""visibleCount""></span>
        </div>

        <div class=""table-wrap"">
            <table id=""resultsTable"">
                <thead>
                    <tr><th>Test Case</th><th>Module</th><th>Status</th><th>Duration</th><th>Browser</th><th>Environment</th><th>Executed At</th><th>Details</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
        </div>

        <div class=""footer"">Tip: Use search + status filters for quick analysis across selected executions.</div>
  </div>
  <script>
    const search = document.getElementById('search');
    const status = document.getElementById('statusFilter');
        const visibleCount = document.getElementById('visibleCount');
    const rows = Array.from(document.querySelectorAll('#resultsTable tbody tr'));
    function apply() {{
      const q = (search.value || '').toLowerCase().trim();
      const s = status.value;
            let visible = 0;
      rows.forEach(r => {{
        const text = r.textContent.toLowerCase();
        const rowStatus = r.getAttribute('data-status');
        const matchQ = !q || text.includes(q);
        const matchS = !s || rowStatus === s;
                const isVisible = (matchQ && matchS);
                r.style.display = isVisible ? '' : 'none';
                if (isVisible) visible++;
      }});
            visibleCount.textContent = `Showing ${{visible}} of ${{rows.length}}`;
    }}
    search.addEventListener('input', apply);
    status.addEventListener('change', apply);
        apply();
  </script>
</body>
</html>";
        }

        private string BuildExtentReport(List<TestExecutionHistory> selected)
        {
            var passedCount = selected.Count(x => string.Equals(x.Status, "Passed", StringComparison.OrdinalIgnoreCase));
            var failedCount = selected.Count(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase));
            var otherCount = selected.Count - passedCount - failedCount;
            var avgDuration = selected.Count == 0 ? 0.0 : Math.Round(selected.Average(x => Math.Max(0, x.Duration)), 2);
            var passRate = selected.Count == 0 ? 0.0 : Math.Round((double)passedCount / selected.Count * 100, 1);
            var passW = selected.Count == 0 ? 0.0 : Math.Round((double)passedCount / selected.Count * 100, 1);
            var failW = selected.Count == 0 ? 0.0 : Math.Round((double)failedCount / selected.Count * 100, 1);
            var otherW = Math.Max(0.0, 100.0 - passW - failW);

            var byModule = selected
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Module) ? "Default" : t.Module)
                .OrderBy(g => g.Key);

            var cards = new StringBuilder();
            foreach (var grp in byModule)
            {
                cards.Append($@"<div class=""module-section""><div class=""module-header"">&#9670; {WebUtility.HtmlEncode(grp.Key)}</div><div class=""module-cards"">");
                foreach (var t in grp)
                {
                    var sKey = string.IsNullOrWhiteSpace(t.Status) ? "Unknown" : t.Status;
                    var sCls = sKey.Equals("Passed", StringComparison.OrdinalIgnoreCase) ? "passed" :
                               sKey.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "failed" : "other";
                    var sIcon = sCls == "passed" ? "&#10003;" : sCls == "failed" ? "&#10007;" : "&#9888;";
                    var stepCount = t.Steps?.Count ?? 0;
                    var execDate = DateTimeOffset.TryParse(t.ExecutedAt, out var dt)
                        ? dt.LocalDateTime.ToString("MMM dd, yyyy HH:mm")
                        : (t.ExecutedAt ?? "").Substring(0, Math.Min(16, (t.ExecutedAt ?? "").Length));

                    var stepsHtml = new StringBuilder();
                    if (t.Steps != null && t.Steps.Count > 0)
                    {
                        foreach (var step in t.Steps)
                        {
                            var ss = (step.Status ?? "unknown").ToLowerInvariant();
                            var si = ss == "passed" ? "&#10003;" : ss == "failed" ? "&#10007;" : "&#9472;";
                            stepsHtml.Append($@"<div class=""step-row {ss}""><span class=""si"">{si}</span><div><span class=""sn"">{WebUtility.HtmlEncode(step.StepName)}</span>{(string.IsNullOrWhiteSpace(step.Error) ? "" : $"<div class=\"se\">{WebUtility.HtmlEncode(step.Error)}</div>")}</div></div>");
                        }
                    }

                    var errHtml = string.IsNullOrWhiteSpace(t.Error) ? "" :
                        $"<div class=\"test-err\">Error: {WebUtility.HtmlEncode(t.Error)}</div>";

                    var stepBlock = stepCount > 0
                        ? $@"<details class=""steps-wrap""><summary class=""steps-sum"">{stepCount} Step{(stepCount == 1 ? "" : "s")}</summary><div class=""tc-steps"">{stepsHtml}</div></details>"
                        : "<div class=\"no-steps\">No steps recorded</div>";

                    cards.Append($@"<div class=""test-card {sCls}""><div class=""tc-top""><span class=""sdot {sCls}"">{sIcon}</span><span class=""tc-name"">{WebUtility.HtmlEncode(t.ScenarioName)}</span><span class=""tc-badge {sCls}"">{WebUtility.HtmlEncode(sKey.ToUpperInvariant())}</span></div><div class=""tc-meta""><span>&#9201; {t.Duration}s</span><span>&#128421; {WebUtility.HtmlEncode(t.Browser ?? "Chrome")}</span><span>&#127759; {WebUtility.HtmlEncode(t.Environment ?? "QA")}</span><span>&#128197; {WebUtility.HtmlEncode(execDate)}</span></div>{errHtml}{stepBlock}</div>");
                }
                cards.Append("</div></div>");
            }

            return $@"<!DOCTYPE html>
<html lang=""en""><head><meta charset=""UTF-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>WinUITest — Extent Report</title><style>
*{{box-sizing:border-box;margin:0;padding:0}}body{{font-family:'Segoe UI',Arial,sans-serif;background:#f0f4f8;color:#1e293b}}
.er-nav{{background:#1b2631;color:#ecf0f1;padding:12px 24px;display:flex;align-items:center;gap:14px;flex-wrap:wrap}}
.brand-w{{color:#5b9bd5;font-weight:700;font-size:1.15rem}}.brand-t{{color:#e84354;font-weight:700;font-size:1.15rem}}
.rpt-lbl{{color:#bdc3c7;font-size:1rem}}.gen-t{{margin-left:auto;font-size:0.78rem;color:#95a5a6}}
.er-dash{{background:#fff;border-bottom:1px solid #dfe6e9;padding:14px 24px}}
.stats-row{{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:12px}}
.sc{{flex:1;min-width:100px;padding:12px;border-radius:8px;border:1px solid #e2e8f0;text-align:center}}
.sc .sv{{font-size:1.5rem;font-weight:700}}.sc .sl{{font-size:0.72rem;color:#7f8c8d;text-transform:uppercase;margin-top:2px}}
.sc-tot .sv{{color:#2c3e50}}.sc-pass .sv{{color:#27ae60}}.sc-fail .sv{{color:#e84354}}.sc-oth .sv{{color:#f39c12}}.sc-rate .sv{{color:#2980b9}}.sc-dur .sv{{color:#16a085}}
.chartbar{{height:10px;border-radius:999px;background:#ecf0f1;overflow:hidden;display:flex}}
.cb-p{{background:#27ae60;height:100%}}.cb-f{{background:#e84354;height:100%}}.cb-o{{background:#f39c12;height:100%}}
.er-body{{padding:16px 24px}}
.filter-row{{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px;align-items:center}}
.filter-row input,.filter-row select{{padding:8px 10px;border:1px solid #dfe6e9;border-radius:6px;background:#fff;font-size:0.88rem}}
.filter-row input{{flex:1;min-width:200px}}.vc{{font-size:0.82rem;color:#7f8c8d;margin-left:auto}}
.module-section{{margin-bottom:18px}}
.module-header{{font-size:0.78rem;text-transform:uppercase;letter-spacing:.8px;color:#7f8c8d;font-weight:700;padding:6px 2px;border-bottom:2px solid #dfe6e9;margin-bottom:10px;display:flex;align-items:center;gap:6px}}
.module-cards{{display:grid;grid-template-columns:repeat(auto-fill,minmax(340px,1fr));gap:10px}}
.test-card{{background:#fff;border-radius:8px;border:1px solid #e2e8f0;border-left:4px solid #bdc3c7;overflow:hidden}}
.test-card:hover{{box-shadow:0 2px 10px rgba(0,0,0,.08)}}
.test-card.passed{{border-left-color:#27ae60}}.test-card.failed{{border-left-color:#e84354}}.test-card.other{{border-left-color:#f39c12}}
.tc-top{{padding:10px 14px;display:flex;align-items:center;gap:8px;border-bottom:1px solid #f8f9fa}}
.sdot{{font-size:0.9rem;width:18px;text-align:center;flex-shrink:0}}
.sdot.passed{{color:#27ae60}}.sdot.failed{{color:#e84354}}.sdot.other{{color:#f39c12}}
.tc-name{{flex:1;font-weight:600;font-size:0.9rem;color:#2c3e50;line-height:1.3}}
.tc-badge{{font-size:10px;font-weight:700;padding:2px 8px;border-radius:999px;color:#fff;flex-shrink:0}}
.tc-badge.passed{{background:#27ae60}}.tc-badge.failed{{background:#e84354}}.tc-badge.other{{background:#f39c12}}
.tc-meta{{padding:7px 14px;display:flex;gap:12px;flex-wrap:wrap;font-size:0.8rem;color:#7f8c8d;background:#fafbfc;border-bottom:1px solid #f0f0f0}}
.test-err{{margin:8px 14px;padding:7px 10px;background:#fef2f2;border:1px solid #fecaca;border-radius:5px;font-size:0.82rem;color:#c0392b}}
.steps-wrap{{border-top:1px solid #f0f0f0}}
.steps-sum{{padding:7px 14px;font-size:0.82rem;font-weight:600;color:#5b9bd5;cursor:pointer;list-style:none;user-select:none}}
.steps-sum::-webkit-details-marker{{display:none}}
.tc-steps{{padding:2px 14px 8px}}
.no-steps{{padding:7px 14px;font-size:0.8rem;color:#bdc3c7;font-style:italic}}
.step-row{{display:flex;gap:8px;padding:5px 0;border-bottom:1px dashed #f0f0f0;font-size:0.83rem;align-items:flex-start}}
.step-row:last-child{{border-bottom:none}}
.si{{width:16px;text-align:center;flex-shrink:0;font-size:0.82rem}}
.step-row.passed .si{{color:#27ae60}}.step-row.failed .si{{color:#e84354}}
.sn{{color:#34495e;font-weight:500}}.se{{margin-top:3px;padding:3px 8px;background:#fef2f2;border-radius:4px;border-left:3px solid #e84354;color:#c0392b;font-size:0.78rem}}
@media(max-width:600px){{.module-cards{{grid-template-columns:1fr}}.er-nav,.er-dash,.er-body{{padding:10px}}}}
</style></head><body>
<div class=""er-nav""><span class=""brand-w"">WinUI</span><span class=""brand-t"">Test</span><span class=""rpt-lbl"">&nbsp;Extent Report</span><span class=""gen-t"">Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}</span></div>
<div class=""er-dash"">
  <div class=""stats-row"">
    <div class=""sc sc-tot""><div class=""sv"">{selected.Count}</div><div class=""sl"">Total</div></div>
    <div class=""sc sc-pass""><div class=""sv"">{passedCount}</div><div class=""sl"">Passed</div></div>
    <div class=""sc sc-fail""><div class=""sv"">{failedCount}</div><div class=""sl"">Failed</div></div>
    <div class=""sc sc-oth""><div class=""sv"">{otherCount}</div><div class=""sl"">Other</div></div>
    <div class=""sc sc-rate""><div class=""sv"">{passRate}%</div><div class=""sl"">Pass Rate</div></div>
    <div class=""sc sc-dur""><div class=""sv"">{avgDuration}s</div><div class=""sl"">Avg Duration</div></div>
  </div>
  <div class=""chartbar""><div class=""cb-p"" style=""width:{passW}%"" title=""Passed""></div><div class=""cb-f"" style=""width:{failW}%"" title=""Failed""></div><div class=""cb-o"" style=""width:{otherW}%"" title=""Other""></div></div>
</div>
<div class=""er-body"">
  <div class=""filter-row""><input id=""q"" placeholder=""Search test / module / browser...""/><select id=""sf""><option value="""">All Statuses</option><option value=""passed"">Passed</option><option value=""failed"">Failed</option><option value=""other"">Other</option></select><span class=""vc"" id=""vc""></span></div>
  {cards}
</div>
<script>
var allCards=Array.from(document.querySelectorAll('.test-card'));
var q=document.getElementById('q'),sf=document.getElementById('sf'),vc=document.getElementById('vc');
function doFilter(){{var qv=q.value.toLowerCase(),sv=sf.value,vis=0;
  allCards.forEach(function(c){{var txt=c.textContent.toLowerCase(),cs=Array.from(c.classList).filter(function(x){{return x!=='test-card';}})[0]||'';
    var mq=!qv||txt.indexOf(qv)>-1,ms=!sv||cs===sv;c.style.display=(mq&&ms)?'':'none';if(mq&&ms)vis++;
  }});
  vc.textContent='Showing '+vis+' of '+allCards.length;}}q.addEventListener('input',doFilter);sf.addEventListener('change',doFilter);doFilter();
</script></body></html>";
        }

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

    // Request model for bulk delete
    public class DeleteTestsRequest
    {
        public List<TestIdentifier> Tests { get; set; } = new List<TestIdentifier>();
    }

    public class TestIdentifier
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string ExecutedAt { get; set; } = string.Empty;
    }
}
