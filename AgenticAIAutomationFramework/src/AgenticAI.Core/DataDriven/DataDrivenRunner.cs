using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;

namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Executes a test scenario multiple times — once per data row in a DataTestSet.
    /// Substitutes ${ColumnName} placeholders in each action's Value before execution.
    /// Does not modify ScenarioExecutor or ZeroCodeTestRunner.
    /// </summary>
    public class DataDrivenRunner
    {
        private readonly Func<Task<IWebDriver>> _driverFactory;

        public DataDrivenRunner(Func<Task<IWebDriver>> driverFactory)
        {
            _driverFactory = driverFactory;
        }

        /// <summary>
        /// Run the scenario once for each row in the provided DataTestSet.
        /// </summary>
        /// <param name="scenario">The base test scenario (not mutated)</param>
        /// <param name="dataSet">The data set with all rows to iterate</param>
        /// <returns>One DataDrivenResult per data row</returns>
        public async Task<List<DataDrivenResult>> RunAsync(TestScenario scenario, DataTestSet dataSet)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));

            var results = new List<DataDrivenResult>();

            Logger.Info($"[DataDrivenRunner] Starting data-driven execution of '{scenario.Name}' with {dataSet.RowCount} row(s).");

            for (int rowIndex = 0; rowIndex < dataSet.Rows.Count; rowIndex++)
            {
                var row = dataSet.Rows[rowIndex];
                Logger.Info($"[DataDrivenRunner] Row {rowIndex + 1}/{dataSet.RowCount}: {FormatRow(row)}");

                var boundScenario = BindScenarioToRow(scenario, row);

                var driver = await _driverFactory();
                var executor = new ScenarioExecutor(driver);

                try
                {
                    var testResult = await executor.ExecuteScenarioAsync(boundScenario);

                    results.Add(new DataDrivenResult
                    {
                        RowIndex = rowIndex,
                        DataRow = new Dictionary<string, string>(row),
                        Result = testResult
                    });

                    Logger.Info($"[DataDrivenRunner] Row {rowIndex + 1} result: {testResult.Status}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[DataDrivenRunner] Row {rowIndex + 1} threw an unexpected exception: {ex.Message}");

                    // Still record a failure result so we have a complete result set
                    results.Add(new DataDrivenResult
                    {
                        RowIndex = rowIndex,
                        DataRow = new Dictionary<string, string>(row),
                        Result = new Models.TestCaseResult
                        {
                            TestCaseId = boundScenario.ScenarioId,
                            TestCaseName = boundScenario.Name,
                            Module = boundScenario.Module,
                            Tags = boundScenario.Tags,
                            StartTime = DateTime.Now,
                            EndTime = DateTime.Now,
                            Status = Enums.TestStatus.Failed,
                            ErrorMessage = ex.Message,
                            StackTrace = ex.StackTrace
                        }
                    });
                }
                finally
                {
                    try
                    {
                        await driver.CloseAsync();
                        await driver.DisposeAsync();
                    }
                    catch (Exception disposeEx)
                    {
                        Logger.Warning($"[DataDrivenRunner] Error disposing driver for row {rowIndex + 1}: {disposeEx.Message}");
                    }
                }
            }

            Logger.Info($"[DataDrivenRunner] Completed. Passed: {results.Count(r => r.Result.Status == Enums.TestStatus.Passed)}, Failed: {results.Count(r => r.Result.Status == Enums.TestStatus.Failed)}");
            return results;
        }

        /// <summary>
        /// Deep-clones the scenario and substitutes all ${ColumnName} placeholders in action values.
        /// The original scenario object is never mutated.
        /// </summary>
        private static TestScenario BindScenarioToRow(TestScenario scenario, Dictionary<string, string> row)
        {
            var bound = new TestScenario
            {
                ScenarioId = Guid.NewGuid().ToString(),
                Name = scenario.Name,
                Description = scenario.Description,
                Module = scenario.Module,
                Tags = new List<string>(scenario.Tags),
                StartUrl = DataSetReader.SubstitutePlaceholders(scenario.StartUrl, row),
                CreatedAt = scenario.CreatedAt,
                ModifiedAt = scenario.ModifiedAt
            };

            // Clone unified Steps
            foreach (var step in scenario.Steps)
            {
                var boundStep = new TestStep
                {
                    StepType = step.StepType,
                    Order = step.Order
                };

                if (step.StepType == "Action" && step.Action != null)
                {
                    boundStep.Action = new RecordedAction
                    {
                        ActionType = step.Action.ActionType,
                        Locator = DataSetReader.SubstitutePlaceholders(step.Action.Locator, row),
                        Value = DataSetReader.SubstitutePlaceholders(step.Action.Value ?? string.Empty, row),
                        Description = step.Action.Description
                    };
                }
                else if (step.StepType == "Assertion" && step.Assertion != null)
                {
                    boundStep.Assertion = new Assertion
                    {
                        Type = step.Assertion.Type,
                        Locator = DataSetReader.SubstitutePlaceholders(step.Assertion.Locator, row),
                        ExpectedValue = DataSetReader.SubstitutePlaceholders(step.Assertion.ExpectedValue ?? string.Empty, row),
                        Description = step.Assertion.Description,
                        ExecuteAfterActionIndex = step.Assertion.ExecuteAfterActionIndex
                    };
                }

                bound.Steps.Add(boundStep);
            }

            // Clone legacy Actions
            foreach (var action in scenario.Actions)
            {
                bound.Actions.Add(new RecordedAction
                {
                    ActionType = action.ActionType,
                    Locator = DataSetReader.SubstitutePlaceholders(action.Locator, row),
                    Value = DataSetReader.SubstitutePlaceholders(action.Value ?? string.Empty, row),
                    Description = action.Description
                });
            }

            // Clone legacy Assertions
            foreach (var assertion in scenario.Assertions)
            {
                bound.Assertions.Add(new Assertion
                {
                    Type = assertion.Type,
                    Locator = DataSetReader.SubstitutePlaceholders(assertion.Locator, row),
                    ExpectedValue = DataSetReader.SubstitutePlaceholders(assertion.ExpectedValue ?? string.Empty, row),
                    Description = assertion.Description,
                    ExecuteAfterActionIndex = assertion.ExecuteAfterActionIndex
                });
            }

            return bound;
        }

        private static string FormatRow(Dictionary<string, string> row)
            => string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
