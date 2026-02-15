using AgenticAI.Core.Models;
using AgenticAI.Core.Enums;
using System.Text;
using System.Net;

namespace AgenticAI.Core.Reporting
{
    /// <summary>
    /// HTML report generator with beautiful UI and comprehensive metrics
    /// </summary>
    public class HtmlReporter : IReporter
    {
        private TestExecutionSummary? _executionSummary;
        private readonly string _reportPath;
        private readonly StringBuilder _htmlContent;

        public HtmlReporter(string reportPath = "TestResults/Reports")
        {
            _reportPath = reportPath;
            _htmlContent = new StringBuilder();
            
            if (!Directory.Exists(_reportPath))
            {
                Directory.CreateDirectory(_reportPath);
            }
        }

        public void StartExecution(TestExecutionSummary summary)
        {
            _executionSummary = summary;
        }

        public void StartTest(TestCaseResult testCase)
        {
            // Implementation handled in EndTest
        }

        public void LogStep(TestStepResult step)
        {
            // Steps are logged as part of test case
        }

        public void EndTest(TestCaseResult testCase)
        {
            _executionSummary?.TestResults.Add(testCase);
        }

        public void EndExecution(TestExecutionSummary summary)
        {
            _executionSummary = summary;
        }

        public void GenerateReport()
        {
            if (_executionSummary == null)
            {
                throw new InvalidOperationException("No test execution data available");
            }

            var reportFileName = Path.Combine(_reportPath, $"TestReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
            var htmlReport = GenerateHtmlContent();
            File.WriteAllText(reportFileName, htmlReport);
            
            // Also create an index.html for easy access
            File.WriteAllText(Path.Combine(_reportPath, "index.html"), htmlReport);
        }

        public string GetReportPath()
        {
            return Path.Combine(_reportPath, "index.html");
        }

        private string GenerateHtmlContent()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine("    <title>Agentic AI Automation Framework - Test Report</title>");
            sb.AppendLine(GetStyles());
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class='container'>");
            
            // Header
            sb.AppendLine(GenerateHeader());
            
            // Dashboard metrics
            sb.AppendLine(GenerateDashboard());
            
            // Test results table
            sb.AppendLine(GenerateTestResultsTable());
            
            // Performance metrics
            if (_executionSummary!.PerformanceMetrics.Count > 0)
            {
                sb.AppendLine(GeneratePerformanceMetrics());
            }
            
            // Flaky tests section
            if (_executionSummary.FlakyTests > 0)
            {
                sb.AppendLine(GenerateFlakyTestsSection());
            }
            
            sb.AppendLine("    </div>");
            sb.AppendLine(GetScripts());
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        private string GenerateHeader()
        {
            var duration = _executionSummary!.Duration.ToString(@"hh\:mm\:ss");
            return $@"
        <header>
            <h1>?? Agentic AI Automation Framework</h1>
            <p class='subtitle'>Test Execution Report</p>
            <div class='header-info'>
                <span><strong>Execution ID:</strong> {_executionSummary.ExecutionId}</span>
                <span><strong>Environment:</strong> {_executionSummary.Environment}</span>
                <span><strong>Browser:</strong> {_executionSummary.Browser}</span>
                <span><strong>OS:</strong> {_executionSummary.OperatingSystem}</span>
                <span><strong>Execution Mode:</strong> {_executionSummary.ExecutionMode}</span>
                <span><strong>Duration:</strong> {duration}</span>
            </div>
        </header>";
        }

        private string GenerateDashboard()
        {
            var passPercentage = _executionSummary!.PassPercentage;
            var status = passPercentage >= 80 ? "success" : passPercentage >= 50 ? "warning" : "danger";

            return $@"
        <div class='dashboard'>
            <div class='metric-card total'>
                <div class='metric-value'>{_executionSummary.TotalTests}</div>
                <div class='metric-label'>Total Tests</div>
            </div>
            <div class='metric-card passed'>
                <div class='metric-value'>{_executionSummary.PassedTests}</div>
                <div class='metric-label'>Passed</div>
            </div>
            <div class='metric-card failed'>
                <div class='metric-value'>{_executionSummary.FailedTests}</div>
                <div class='metric-label'>Failed</div>
            </div>
            <div class='metric-card skipped'>
                <div class='metric-value'>{_executionSummary.SkippedTests}</div>
                <div class='metric-label'>Skipped</div>
            </div>
            <div class='metric-card flaky'>
                <div class='metric-value'>{_executionSummary.FlakyTests}</div>
                <div class='metric-label'>Flaky Tests</div>
            </div>
            <div class='metric-card percentage {status}'>
                <div class='metric-value'>{passPercentage:F1}%</div>
                <div class='metric-label'>Pass Rate</div>
            </div>
        </div>
        
        <div class='progress-section'>
            <div class='progress-bar'>
                <div class='progress-fill passed' style='width: {(_executionSummary.PassedTests * 100.0 / _executionSummary.TotalTests):F1}%'></div>
                <div class='progress-fill failed' style='width: {(_executionSummary.FailedTests * 100.0 / _executionSummary.TotalTests):F1}%'></div>
                <div class='progress-fill skipped' style='width: {(_executionSummary.SkippedTests * 100.0 / _executionSummary.TotalTests):F1}%'></div>
            </div>
        </div>";
        }

        private string GenerateTestResultsTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"
        <div class='section'>
            <h2>&#128213; Test Results</h2>
            <div class='filter-buttons'>
                <button class='filter-btn active' onclick='filterTests(""all"")'>All</button>
                <button class='filter-btn' onclick='filterTests(""passed"")'>Passed</button>
                <button class='filter-btn' onclick='filterTests(""failed"")'>Failed</button>
                <button class='filter-btn' onclick='filterTests(""skipped"")'>Skipped</button>
            </div>
            <table class='results-table'>
                <thead>
                    <tr>
                        <th>Status</th>
                        <th>Module</th>
                        <th>Test Case</th>
                        <th>Duration</th>
                        <th>Steps</th>
                        <th>Retries</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var test in _executionSummary!.TestResults.OrderBy(t => t.Module).ThenBy(t => t.TestCaseName))
            {
                var statusClass = test.Status.ToString().ToLower();
                var statusIcon = GetStatusIcon(test.Status);
                var testId = Guid.NewGuid().ToString("N");
                var testDuration = test.Duration.ToString(@"mm\:ss\.fff");

                sb.AppendLine($@"
                    <tr class='test-row' data-status='{statusClass}'>
                        <td><span class='status-badge {statusClass}'>{statusIcon} {test.Status}</span></td>
                        <td>{test.Module}</td>
                        <td>
                            {test.TestCaseName}
                            {(test.Tags.Count > 0 ? $"<div class='tags'>{string.Join("", test.Tags.Select(t => $"<span class='tag'>{t}</span>"))}</div>" : "")}
                        </td>
                        <td>{testDuration}</td>
                        <td>{test.PassedSteps}/{test.TotalSteps}</td>
                        <td>{(test.RetryCount > 0 ? $"<span class='retry-badge'>{test.RetryCount}</span>" : "-")}</td>
                        <td>
                            <button class='btn-details' onclick='toggleDetails(""{testId}"")'>Details</button>
                        </td>
                    </tr>
                    <tr id='{testId}' class='details-row' style='display: none;'>
                        <td colspan='7'>
                            {GenerateTestDetails(test)}
                        </td>
                    </tr>");
            }

            sb.AppendLine(@"
                </tbody>
            </table>
        </div>");

            return sb.ToString();
        }

        private string GenerateTestDetails(TestCaseResult test)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<div class='test-details'>");

            if (!string.IsNullOrEmpty(test.ErrorMessage))
            {
                sb.AppendLine($@"
                <div class='error-section'>
                    <h4>&#10060; Error Message</h4>
                    <pre class='error-message'>{WebUtility.HtmlEncode(test.ErrorMessage)}</pre>
                    {(!string.IsNullOrEmpty(test.StackTrace) ? $"<pre class='stack-trace'>{WebUtility.HtmlEncode(test.StackTrace)}</pre>" : "")}
                </div>");
            }

            if (test.Steps.Count > 0)
            {
                sb.AppendLine("<h4>&#128195; Test Steps</h4>");
                sb.AppendLine("<table class='steps-table'>");
                sb.AppendLine("<thead><tr><th>Status</th><th>Step</th><th>Description</th><th>Duration</th><th>Screenshot</th></tr></thead>");
                sb.AppendLine("<tbody>");

                foreach (var step in test.Steps)
                {
                    var stepStatusIcon = GetStatusIcon(step.Status);
                    var screenshotLink = !string.IsNullOrEmpty(step.ScreenshotPath) 
                        ? $"<a href='{step.ScreenshotPath}' target='_blank'>View</a>" 
                        : "-";

                    sb.AppendLine($@"
                    <tr class='step-row {step.Status.ToString().ToLower()}'>
                        <td>{stepStatusIcon} {step.Status}</td>
                        <td>{step.StepName}</td>
                        <td>{step.Description}</td>
                        <td>{step.Duration.TotalMilliseconds}ms</td>
                        <td>{screenshotLink}</td>
                    </tr>");

                    if (!string.IsNullOrEmpty(step.ErrorMessage))
                    {
                        sb.AppendLine($@"
                    <tr class='step-error'>
                        <td colspan='5'>
                            <strong>Error:</strong> {WebUtility.HtmlEncode(step.ErrorMessage)}
                        </td>
                    </tr>");
                    }
                }

                sb.AppendLine("</tbody></table>");
            }

            if (!string.IsNullOrEmpty(test.VideoPath))
            {
                sb.AppendLine($@"
                <div class='video-section'>
                    <h4>&#127909; Test Recording</h4>
                    <video controls width='640' height='480'>
                        <source src='{test.VideoPath}' type='video/webm'>
                        Your browser does not support the video tag.
                    </video>
                </div>");
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GeneratePerformanceMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"
        <div class='section'>
            <h2>&#128200; Performance Metrics</h2>
            <div class='metrics-grid'>");

            foreach (var metric in _executionSummary!.PerformanceMetrics)
            {
                sb.AppendLine($@"
                <div class='metric-item'>
                    <div class='metric-name'>{metric.Key}</div>
                    <div class='metric-value'>{metric.Value}</div>
                </div>");
            }

            sb.AppendLine(@"
            </div>
        </div>");

            return sb.ToString();
        }

        private string GenerateFlakyTestsSection()
        {
            var flakyTests = _executionSummary!.TestResults.Where(t => t.RetryCount > 0).ToList();
            var sb = new StringBuilder();

            sb.AppendLine(@"
        <div class='section'>
            <h2>&#9888; Flaky Tests</h2>
            <p class='info'>These tests required retries to pass</p>
            <table class='results-table'>
                <thead>
                    <tr>
                        <th>Test Case</th>
                        <th>Module</th>
                        <th>Retry Count</th>
                        <th>Final Status</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var test in flakyTests)
            {
                sb.AppendLine($@"
                    <tr>
                        <td>{test.TestCaseName}</td>
                        <td>{test.Module}</td>
                        <td><span class='retry-badge'>{test.RetryCount}</span></td>
                        <td><span class='status-badge {test.Status.ToString().ToLower()}'>{GetStatusIcon(test.Status)} {test.Status}</span></td>
                    </tr>");
            }

            sb.AppendLine(@"
                </tbody>
            </table>
        </div>");

            return sb.ToString();
        }

        private string GetStatusIcon(TestStatus status)
        {
            return status switch
            {
                TestStatus.Passed => "&#9989;",
                TestStatus.Failed => "&#10060;",
                TestStatus.Skipped => "&#9898;",
                TestStatus.Warning => "&#9888;",
                TestStatus.Info => "&#8505;",
                _ => "&#128308;"
            };
        }

        private string GetStyles()
        {
            return @"
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 20px;
            min-height: 100vh;
        }

        .container {
            max-width: 1400px;
            margin: 0 auto;
            background: white;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
        }

        header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
            text-align: center;
        }

        header h1 {
            font-size: 2.5em;
            margin-bottom: 10px;
        }

        .subtitle {
            font-size: 1.2em;
            opacity: 0.9;
            margin-bottom: 20px;
        }

        .header-info {
            display: flex;
            justify-content: center;
            flex-wrap: wrap;
            gap: 20px;
            margin-top: 20px;
            font-size: 0.9em;
        }

        .header-info span {
            background: rgba(255,255,255,0.2);
            padding: 8px 16px;
            border-radius: 20px;
        }

        .dashboard {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 20px;
            padding: 40px;
            background: #f8f9fa;
        }

        .metric-card {
            background: white;
            padding: 30px;
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
            text-align: center;
            transition: transform 0.3s ease;
        }

        .metric-card:hover {
            transform: translateY(-5px);
        }

        .metric-card.total {
            border-top: 4px solid #3b82f6;
        }

        .metric-card.passed {
            border-top: 4px solid #10b981;
        }

        .metric-card.failed {
            border-top: 4px solid #ef4444;
        }

        .metric-card.skipped {
            border-top: 4px solid #f59e0b;
        }

        .metric-card.flaky {
            border-top: 4px solid #8b5cf6;
        }

        .metric-card.percentage.success {
            border-top: 4px solid #10b981;
        }

        .metric-card.percentage.warning {
            border-top: 4px solid #f59e0b;
        }

        .metric-card.percentage.danger {
            border-top: 4px solid #ef4444;
        }

        .metric-value {
            font-size: 3em;
            font-weight: bold;
            color: #1f2937;
        }

        .metric-label {
            font-size: 1em;
            color: #6b7280;
            margin-top: 10px;
        }

        .progress-section {
            padding: 0 40px 40px;
            background: #f8f9fa;
        }

        .progress-bar {
            height: 30px;
            background: #e5e7eb;
            border-radius: 15px;
            overflow: hidden;
            display: flex;
        }

        .progress-fill {
            height: 100%;
            transition: width 1s ease;
        }

        .progress-fill.passed {
            background: #10b981;
        }

        .progress-fill.failed {
            background: #ef4444;
        }

        .progress-fill.skipped {
            background: #f59e0b;
        }

        .section {
            padding: 40px;
        }

        .section h2 {
            color: #1f2937;
            margin-bottom: 20px;
            font-size: 1.8em;
        }

        .filter-buttons {
            margin-bottom: 20px;
        }

        .filter-btn {
            padding: 10px 20px;
            margin-right: 10px;
            border: 2px solid #667eea;
            background: white;
            color: #667eea;
            border-radius: 20px;
            cursor: pointer;
            font-weight: 600;
            transition: all 0.3s ease;
        }

        .filter-btn.active,
        .filter-btn:hover {
            background: #667eea;
            color: white;
        }

        .results-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            border-radius: 8px;
            overflow: hidden;
        }

        .results-table th {
            background: #374151;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
        }

        .results-table td {
            padding: 15px;
            border-bottom: 1px solid #e5e7eb;
        }

        .test-row:hover {
            background: #f9fafb;
        }

        .status-badge {
            display: inline-block;
            padding: 6px 12px;
            border-radius: 12px;
            font-weight: 600;
            font-size: 0.85em;
        }

        .status-badge.passed {
            background: #d1fae5;
            color: #065f46;
        }

        .status-badge.failed {
            background: #fee2e2;
            color: #991b1b;
        }

        .status-badge.skipped {
            background: #fef3c7;
            color: #92400e;
        }

        .tags {
            margin-top: 8px;
        }

        .tag {
            display: inline-block;
            background: #e0e7ff;
            color: #3730a3;
            padding: 4px 8px;
            border-radius: 8px;
            font-size: 0.75em;
            margin-right: 5px;
        }

        .retry-badge {
            background: #f3e8ff;
            color: #6b21a8;
            padding: 4px 8px;
            border-radius: 8px;
            font-weight: 600;
        }

        .btn-details {
            background: #667eea;
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 600;
            transition: background 0.3s ease;
        }

        .btn-details:hover {
            background: #5568d3;
        }

        .details-row {
            background: #f9fafb;
        }

        .test-details {
            padding: 20px;
        }

        .error-section {
            background: #fef2f2;
            border-left: 4px solid #ef4444;
            padding: 20px;
            margin-bottom: 20px;
            border-radius: 8px;
        }

        .error-section h4 {
            color: #991b1b;
            margin-bottom: 10px;
        }

        .error-message,
        .stack-trace {
            background: white;
            padding: 15px;
            border-radius: 6px;
            overflow-x: auto;
            margin-top: 10px;
            font-size: 0.85em;
            color: #dc2626;
        }

        .steps-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }

        .steps-table th {
            background: #f3f4f6;
            padding: 10px;
            text-align: left;
            font-weight: 600;
            color: #374151;
        }

        .steps-table td {
            padding: 10px;
            border-bottom: 1px solid #e5e7eb;
        }

        .step-row.passed {
            background: #f0fdf4;
        }

        .step-row.failed {
            background: #fef2f2;
        }

        .step-error {
            background: #fee2e2;
            color: #991b1b;
        }

        .video-section {
            margin-top: 20px;
        }

        .video-section h4 {
            margin-bottom: 10px;
        }

        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 20px;
        }

        .metric-item {
            background: #f9fafb;
            padding: 20px;
            border-radius: 8px;
            border-left: 4px solid #667eea;
        }

        .metric-name {
            color: #6b7280;
            font-size: 0.9em;
            margin-bottom: 8px;
        }

        .metric-item .metric-value {
            font-size: 1.5em;
            font-weight: bold;
            color: #1f2937;
        }

        .info {
            color: #6b7280;
            font-style: italic;
            margin-bottom: 15px;
        }

        @media (max-width: 768px) {
            .dashboard {
                grid-template-columns: repeat(2, 1fr);
            }

            .results-table {
                font-size: 0.9em;
            }

            .results-table th,
            .results-table td {
                padding: 10px;
            }
        }
    </style>";
        }

        private string GetScripts()
        {
            return @"
    <script>
        function toggleDetails(id) {
            const row = document.getElementById(id);
            if (row.style.display === 'none' || row.style.display === '') {
                row.style.display = 'table-row';
            } else {
                row.style.display = 'none';
            }
        }

        function filterTests(status) {
            const rows = document.querySelectorAll('.test-row');
            const buttons = document.querySelectorAll('.filter-btn');
            
            // Update active button
            buttons.forEach(btn => btn.classList.remove('active'));
            event.target.classList.add('active');
            
            // Filter rows
            rows.forEach(row => {
                if (status === 'all') {
                    row.style.display = 'table-row';
                } else {
                    const rowStatus = row.getAttribute('data-status');
                    row.style.display = rowStatus === status ? 'table-row' : 'none';
                }
            });
        }

        // Animate progress bar on load
        window.addEventListener('load', function() {
            const progressFills = document.querySelectorAll('.progress-fill');
            progressFills.forEach(fill => {
                const width = fill.style.width;
                fill.style.width = '0';
                setTimeout(() => {
                    fill.style.width = width;
                }, 100);
            });
        });
    </script>";
        }
    }
}
