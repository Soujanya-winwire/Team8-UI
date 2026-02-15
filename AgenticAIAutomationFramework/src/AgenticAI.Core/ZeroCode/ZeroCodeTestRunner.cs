using AgenticAI.Core.Configuration;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.Reporting;
using AgenticAI.Core.ZeroCode.Models;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Runs zero-code test scenarios without writing code
    /// </summary>
    public class ZeroCodeTestRunner
    {
        private readonly ScenarioManager _scenarioManager;
        private readonly FrameworkConfiguration _config;
        private readonly Func<Task<IWebDriver>> _driverFactory;

        public ZeroCodeTestRunner(Func<Task<IWebDriver>> driverFactory)
        {
            _scenarioManager = new ScenarioManager();
            _config = ConfigurationManager.Instance.FrameworkConfig;
            _driverFactory = driverFactory;
        }

        /// <summary>
        /// Execute a single scenario by name
        /// </summary>
        public async Task<TestCaseResult> ExecuteScenarioAsync(string scenarioName, string module = "Default")
        {
            var scenario = _scenarioManager.LoadScenario(scenarioName, module);
            
            if (scenario == null)
            {
                throw new FileNotFoundException($"Scenario not found: {scenarioName} in module {module}");
            }

            return await ExecuteScenarioAsync(scenario);
        }

        /// <summary>
        /// Execute a test scenario
        /// </summary>
        public async Task<TestCaseResult> ExecuteScenarioAsync(TestScenario scenario)
        {
            var driver = await _driverFactory();
            var executor = new ScenarioExecutor(driver);

            try
            {
                return await executor.ExecuteScenarioAsync(scenario);
            }
            finally
            {
                await driver.CloseAsync();
                await driver.DisposeAsync();
            }
        }

        /// <summary>
        /// Execute multiple scenarios by module
        /// </summary>
        public async Task<List<TestCaseResult>> ExecuteModuleAsync(string module)
        {
            var scenarios = _scenarioManager.LoadScenariosByModule(module);
            Logger.Info($"Executing {scenarios.Count} scenarios in module: {module}");

            var results = new List<TestCaseResult>();

            if (_config.ExecutionMode == Core.Enums.ExecutionMode.Parallel)
            {
                var tasks = scenarios.Select(s => ExecuteScenarioAsync(s));
                var taskResults = await Task.WhenAll(tasks);
                results.AddRange(taskResults);
            }
            else
            {
                foreach (var scenario in scenarios)
                {
                    var result = await ExecuteScenarioAsync(scenario);
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Execute scenarios by tag
        /// </summary>
        public async Task<List<TestCaseResult>> ExecuteByTagAsync(string tag)
        {
            var scenarios = _scenarioManager.LoadScenariosByTag(tag);
            Logger.Info($"Executing {scenarios.Count} scenarios with tag: {tag}");

            var results = new List<TestCaseResult>();

            if (_config.ExecutionMode == Core.Enums.ExecutionMode.Parallel)
            {
                var tasks = scenarios.Select(s => ExecuteScenarioAsync(s));
                var taskResults = await Task.WhenAll(tasks);
                results.AddRange(taskResults);
            }
            else
            {
                foreach (var scenario in scenarios)
                {
                    var result = await ExecuteScenarioAsync(scenario);
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Execute all available scenarios
        /// </summary>
        public async Task<TestExecutionSummary> ExecuteAllScenariosAsync()
        {
            var scenarios = _scenarioManager.LoadAllScenarios();
            Logger.Info($"Executing all scenarios. Total: {scenarios.Count}");

            var summary = new TestExecutionSummary
            {
                StartTime = DateTime.Now,
                Browser = _config.Browser,
                OperatingSystem = _config.OperatingSystem,
                Environment = _config.Environment,
                ExecutionMode = _config.ExecutionMode
            };

            ReportManager.Instance.StartExecution(summary);

            if (_config.ExecutionMode == Core.Enums.ExecutionMode.Parallel)
            {
                var tasks = scenarios.Select(s => ExecuteScenarioAsync(s));
                var results = await Task.WhenAll(tasks);
                summary.TestResults.AddRange(results);
            }
            else
            {
                foreach (var scenario in scenarios)
                {
                    var result = await ExecuteScenarioAsync(scenario);
                    summary.TestResults.Add(result);
                }
            }

            summary.EndTime = DateTime.Now;
            
            Logger.Info($"Execution completed. Passed: {summary.PassedTests}, Failed: {summary.FailedTests}");
            
            return summary;
        }
    }
}
