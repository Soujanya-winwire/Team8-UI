using AgenticAI.Core.Configuration;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.Models;
using AgenticAI.Core.Reporting;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Enums;

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
        private readonly Func<BrowserType, Task<IWebDriver>>? _browserSpecificDriverFactory; // Optional factory for cross-browser

        public ZeroCodeTestRunner(Func<Task<IWebDriver>> driverFactory, Func<BrowserType, Task<IWebDriver>>? browserSpecificDriverFactory = null)
        {
            _scenarioManager = new ScenarioManager();
            _config = ConfigurationManager.Instance.FrameworkConfig;
            _driverFactory = driverFactory;
            _browserSpecificDriverFactory = browserSpecificDriverFactory;
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
                Logger.Info($"Using parallel execution with {_config.ParallelWorkers} workers");
                Logger.Info($"🔍 DEBUG: CrossBrowserParallelExecution = {_config.CrossBrowserParallelExecution}");
                Logger.Info($"🔍 DEBUG: ParallelBrowsers = {(_config.ParallelBrowsers != null ? string.Join(", ", _config.ParallelBrowsers) : "NULL")}");
                Logger.Info($"🔍 DEBUG: ParallelBrowsers.Count = {_config.ParallelBrowsers?.Count ?? 0}");
                Logger.Info($"🔍 DEBUG: _browserSpecificDriverFactory = {(_browserSpecificDriverFactory != null ? "PROVIDED" : "NULL")}");
                
                // Check if cross-browser parallel execution is enabled
                if (_config.CrossBrowserParallelExecution && _config.ParallelBrowsers != null && _config.ParallelBrowsers.Count > 1)
                {
                    Logger.Info($"✅ Cross-browser parallel execution enabled with browsers: {string.Join(", ", _config.ParallelBrowsers)}");
                    results.AddRange(await ExecuteCrossBrowserParallelAsync(scenarios));
                }
                else
                {
                    Logger.Warning($"❌ Cross-browser NOT enabled. Reason: CrossBrowser={_config.CrossBrowserParallelExecution}, Browsers={_config.ParallelBrowsers?.Count ?? 0}");
                    // Regular parallel execution with same browser
                    using var semaphore = new SemaphoreSlim(_config.ParallelWorkers, _config.ParallelWorkers);
                    var tasks = scenarios.Select(async scenario =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var driver = await _driverFactory();
                            var executor = new ScenarioExecutor(driver);

                            try
                            {
                                Logger.Info($"[Parallel] Starting: {scenario.Name}");
                                var result = await executor.ExecuteScenarioAsync(scenario);
                                Logger.Info($"[Parallel] Completed: {scenario.Name} - {result.Status}");
                                return result;
                            }
                            finally
                            {
                                await driver.CloseAsync();
                                await driver.DisposeAsync();
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    var taskResults = await Task.WhenAll(tasks);
                    results.AddRange(taskResults);
                }
                
                Logger.Info($"Parallel execution complete. Passed: {results.Count(r => r.Status == Core.Enums.TestStatus.Passed)}, Failed: {results.Count(r => r.Status == Core.Enums.TestStatus.Failed)}");
            }
            else
            {
                Logger.Info("Using sequential execution");
                
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
                Logger.Info($"Using parallel execution with {_config.ParallelWorkers} workers");
                
                // Check if cross-browser parallel execution is enabled
                if (_config.CrossBrowserParallelExecution && _config.ParallelBrowsers != null && _config.ParallelBrowsers.Count > 1)
                {
                    Logger.Info($"Cross-browser parallel execution enabled with browsers: {string.Join(", ", _config.ParallelBrowsers)}");
                    results.AddRange(await ExecuteCrossBrowserParallelAsync(scenarios));
                }
                else
                {
                    // Regular parallel execution with same browser
                    using var semaphore = new SemaphoreSlim(_config.ParallelWorkers, _config.ParallelWorkers);
                    var tasks = scenarios.Select(async scenario =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var driver = await _driverFactory();
                            var executor = new ScenarioExecutor(driver);

                            try
                            {
                                Logger.Info($"[Parallel] Starting: {scenario.Name}");
                                var result = await executor.ExecuteScenarioAsync(scenario);
                                Logger.Info($"[Parallel] Completed: {scenario.Name} - {result.Status}");
                                return result;
                            }
                            finally
                            {
                                await driver.CloseAsync();
                                await driver.DisposeAsync();
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    var taskResults = await Task.WhenAll(tasks);
                    results.AddRange(taskResults);
                }
                
                Logger.Info($"Parallel execution complete. Passed: {results.Count(r => r.Status == Core.Enums.TestStatus.Passed)}, Failed: {results.Count(r => r.Status == Core.Enums.TestStatus.Failed)}");
            }
            else
            {
                Logger.Info("Using sequential execution");
                
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
                Logger.Info($"Using parallel execution with {_config.ParallelWorkers} workers");
                
                // Check if cross-browser parallel execution is enabled
                if (_config.CrossBrowserParallelExecution && _config.ParallelBrowsers != null && _config.ParallelBrowsers.Count > 1)
                {
                    Logger.Info($"Cross-browser parallel execution enabled with browsers: {string.Join(", ", _config.ParallelBrowsers)}");
                    var results = await ExecuteCrossBrowserParallelAsync(scenarios);
                    summary.TestResults.AddRange(results);
                }
                else
                {
                    // Regular parallel execution with same browser
                    using var semaphore = new SemaphoreSlim(_config.ParallelWorkers, _config.ParallelWorkers);
                    var tasks = scenarios.Select(async scenario =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var driver = await _driverFactory();
                            var executor = new ScenarioExecutor(driver);

                            try
                            {
                                Logger.Info($"[Parallel] Starting: {scenario.Name}");
                                var result = await executor.ExecuteScenarioAsync(scenario);
                                Logger.Info($"[Parallel] Completed: {scenario.Name} - {result.Status}");
                                return result;
                            }
                            finally
                            {
                                await driver.CloseAsync();
                                await driver.DisposeAsync();
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    var results = await Task.WhenAll(tasks);
                    summary.TestResults.AddRange(results);
                }
                
                Logger.Info($"Parallel execution complete. Passed: {summary.PassedTests}, Failed: {summary.FailedTests}");
            }
            else
            {
                Logger.Info("Using sequential execution");
                
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

        /// <summary>
        /// Execute tests in parallel across multiple browsers
        /// </summary>
        private async Task<List<TestCaseResult>> ExecuteCrossBrowserParallelAsync(List<TestScenario> scenarios)
        {
            var results = new List<TestCaseResult>();
            var browsers = _config.ParallelBrowsers;

            if (_browserSpecificDriverFactory == null)
            {
                Logger.Warning("⚠️ Cross-browser parallel execution requested but no browser-specific factory provided. Falling back to regular parallel execution.");
                // Fall back to regular parallel with same browser
                using var semaphore = new SemaphoreSlim(_config.ParallelWorkers, _config.ParallelWorkers);
                var tasks = scenarios.Select(async scenario =>
                {
                    await semaphore.WaitAsync();
                    try
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
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();
                return (await Task.WhenAll(tasks)).ToList();
            }

            Logger.Info($"🌐 Starting cross-browser parallel execution with {browsers.Count} browsers: {string.Join(", ", browsers)}");

            // Distribute scenarios across browsers in round-robin fashion
            using var sem = new SemaphoreSlim(_config.ParallelWorkers, _config.ParallelWorkers);
            
            var testTasks = new List<Task<TestCaseResult>>();
            for (int i = 0; i < scenarios.Count; i++)
            {
                var scenarioIndex = i; // Capture for closure
                var scenario = scenarios[scenarioIndex];
                var targetBrowser = browsers[scenarioIndex % browsers.Count];
                
                await sem.WaitAsync();
                
                var task = Task.Run(async () =>
                {
                    IWebDriver? driver = null;
                    try
                    {
                        Logger.Info($"[Cross-Browser] Test {scenarioIndex + 1}/{scenarios.Count}: {scenario.Name} → {targetBrowser}");
                        
                        // Create driver with specific browser using the factory
                        driver = await _browserSpecificDriverFactory(targetBrowser);
                        var executor = new ScenarioExecutor(driver);

                        var result = await executor.ExecuteScenarioAsync(scenario);
                        
                        // Store browser info in result metadata
                        result.Tags.Add($"Browser:{targetBrowser}");
                        
                        Logger.Info($"[Cross-Browser] ✓ Completed: {scenario.Name} on {targetBrowser} - {result.Status}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[Cross-Browser] ✗ Failed: {scenario.Name} on {targetBrowser} - {ex.Message}");
                        throw;
                    }
                    finally
                    {
                        if (driver != null)
                        {
                            try
                            {
                                await driver.CloseAsync();
                                await driver.DisposeAsync();
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning($"Error disposing driver for {scenario.Name}: {ex.Message}");
                            }
                        }
                        sem.Release();
                    }
                });
                
                testTasks.Add(task);
            }

            var taskResults = await Task.WhenAll(testTasks);
            results.AddRange(taskResults);

            Logger.Info($"🏁 Cross-browser execution complete. Total: {results.Count}, Passed: {results.Count(r => r.Status == TestStatus.Passed)}, Failed: {results.Count(r => r.Status == TestStatus.Failed)}");

            return results;
        }
    }
}
