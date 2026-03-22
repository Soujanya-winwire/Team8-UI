using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using System;
using System.Text.RegularExpressions;

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
                
                // Set the current dataset for runtime parameter resolution
                executor.SetDataset(row);

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
            var stepInputActionIndex = 0;
            var legacyInputActionIndex = 0;

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
                    var boundAction = BindActionToRow(step.Action, row, ref stepInputActionIndex);

                    boundStep.Action = new RecordedAction
                    {
                        ActionType = boundAction.ActionType,
                        Locator = boundAction.Locator,
                        Value = boundAction.Value,
                        Description = boundAction.Description,
                        Metadata = boundAction.Metadata
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
                var boundAction = BindActionToRow(action, row, ref legacyInputActionIndex);

                bound.Actions.Add(new RecordedAction
                {
                    ActionType = boundAction.ActionType,
                    Locator = boundAction.Locator,
                    Value = boundAction.Value,
                    Description = boundAction.Description,
                    Metadata = boundAction.Metadata
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

        private static RecordedAction BindActionToRow(RecordedAction action, Dictionary<string, string> row, ref int inputActionIndex)
        {
            var actionType = (action.ActionType ?? string.Empty).Trim().ToLowerInvariant();
            var substitutedLocator = DataSetReader.SubstitutePlaceholders(action.Locator, row);
            var originalValue = action.Value ?? string.Empty;
            var substitutedValue = DataSetReader.SubstitutePlaceholders(originalValue, row);
            var metadata = new Dictionary<string, string>(action.Metadata ?? new Dictionary<string, string>());

            if (IsDataEntryAction(actionType) && row.Count > 0)
            {
                if (metadata.TryGetValue("ParameterName", out var parameterName) &&
                    TryGetRowValue(row, parameterName, out var metadataMappedValue))
                {
                    substitutedValue = metadataMappedValue;
                }
                else
                {
                    var inferredName = ParameterResolver.InferParameterName(substitutedLocator ?? string.Empty, actionType);
                    if (!string.IsNullOrWhiteSpace(inferredName) && TryGetRowValue(row, inferredName, out var inferredMappedValue))
                    {
                        metadata["ParameterName"] = inferredName;
                        substitutedValue = inferredMappedValue;
                    }
                    else if (TryGetRowValue(row, originalValue, out var rawValueMapped))
                    {
                        metadata["ParameterName"] = originalValue;
                        substitutedValue = rawValueMapped;
                    }
                        else
                        {
                            // Priority 4: Fuzzy match — check if any column name appears in the locator
                            // or if the locator fragments appear in column names (case-insensitive).
                            // This covers: column "username" matching locator "#username", "[name=user_name]",
                            // "[data-testid=usernameInput]", etc.
                            var fuzzyKey = !string.IsNullOrWhiteSpace(substitutedLocator)
                                ? FindBestColumnMatch(substitutedLocator, row)
                                : null;

                            if (fuzzyKey != null && TryGetRowValue(row, fuzzyKey, out var fuzzyMappedValue))
                            {
                                metadata["ParameterName"] = fuzzyKey;
                                substitutedValue = fuzzyMappedValue;
                            }
                            else if (!ParameterResolver.ContainsPlaceholders(originalValue) &&
                                     string.Equals(substitutedValue, originalValue, StringComparison.Ordinal))
                            {
                                    // Priority 5 (safe fallback): only auto-map when there is exactly one
                                    // data column; otherwise keep the original value to avoid random
                                    // cross-column assignment.
                                    if (row.Count == 1)
                                    {
                                        var selectedKey = row.Keys.First();
                                        substitutedValue = row[selectedKey] ?? string.Empty;
                                        metadata["ParameterName"] = selectedKey;
                                        inputActionIndex++;
                                    }
                            }
                        }
                }
            }

                return new RecordedAction
            {
                ActionType = action.ActionType,
                Locator = substitutedLocator,
                Value = substitutedValue,
                Description = action.Description,
                Metadata = metadata
            };
        }

        private static bool IsDataEntryAction(string actionType)
        {
            return actionType == "type" || actionType == "fill" || actionType == "input" || actionType == "select";
        }

            /// <summary>
            /// Attempts to find the best-matching column name from the row by comparing normalised
            /// column names against the text fragments extracted from a CSS selector or XPath.
            ///
            /// Matching precedence (highest → lowest):
            ///   1. Exact normalised match: locator contains the normalised column name as a whole token
            ///   2. Partial match: normalised column name is a substring of any locator token, or vice-versa
            ///
            /// Examples:
            ///   column "username"  — locator "#username"              → token "username"  → exact match
            ///   column "email"     — locator "[name=userEmail]"        → token "useremail" → partial match ("email" in "useremail")
            ///   column "firstName" — locator "[data-testid=firstName]" → token "firstname" → exact match
            ///   column "password"  — locator "input[type=password]"    → token "password"  → exact match
            /// </summary>
            private static string? FindBestColumnMatch(string locator, Dictionary<string, string> row)
            {
                if (string.IsNullOrWhiteSpace(locator) || row.Count == 0)
                    return null;

                // Extract meaningful tokens from the locator by removing CSS/XPath punctuation.
                // "#userEmail" → "userEmail", "[name=first_name]" → "first_name", etc.
                var tokenPattern = Regex.Matches(locator, @"[a-zA-Z][a-zA-Z0-9_\-]*");
                var locatorTokens = tokenPattern
                    .Cast<Match>()
                    .Select(m => Normalize(m.Value))
                    .Where(t => t.Length > 1)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Skip noise tokens that appear in almost every locator
                var noise = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "input", "button", "select", "textarea", "form", "div", "span",
                    "class", "id", "type", "name", "value", "aria", "label", "data",
                    "testid", "test", "qa", "placeholder", "text", "nth", "child"
                };

                string? bestKey = null;
                int bestScore = 0;

                foreach (var col in row.Keys)
                {
                    var colNorm = Normalize(col);
                    if (string.IsNullOrWhiteSpace(colNorm) || noise.Contains(colNorm))
                        continue;

                    int score = 0;

                    // Score 3: a locator token exactly equals the normalised column name
                    if (locatorTokens.Contains(colNorm))
                    {
                        score = 3;
                    }
                    else
                    {
                        foreach (var token in locatorTokens)
                        {
                            if (noise.Contains(token)) continue;

                            // Score 2: column name is a substring of a locator token or vice-versa
                            if (token.Contains(colNorm, StringComparison.OrdinalIgnoreCase) ||
                                colNorm.Contains(token, StringComparison.OrdinalIgnoreCase))
                            {
                                score = Math.Max(score, 2);
                            }
                        }
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestKey = col;
                    }
                }

                // Only accept a match with a meaningful score
                return bestScore >= 2 ? bestKey : null;
            }

            /// <summary>
            /// Normalise a name/token to a lowercase, separator-free form for fuzzy comparison.
            /// E.g. "first_name" → "firstname", "userEmail" → "useremail"
            /// </summary>
            private static string Normalize(string s)
                => Regex.Replace(s, @"[-_\s]", string.Empty).ToLowerInvariant();

        private static bool TryGetRowValue(Dictionary<string, string> row, string key, out string value)
        {
            value = string.Empty;

            if (string.IsNullOrWhiteSpace(key) || row.Count == 0)
                return false;

            if (row.TryGetValue(key, out var directValue))
            {
                value = directValue;
                return true;
            }

            var caseInsensitive = row.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(caseInsensitive.Key))
            {
                value = caseInsensitive.Value;
                return true;
            }

            return false;
        }

        private static string FormatRow(Dictionary<string, string> row)
            => string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
