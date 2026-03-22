using AgenticAI.Core.Utilities.CodeGeneration;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScriptGenerationController : ControllerBase
    {
        [HttpPost("from-recorded-scenario")]
        public async Task<IActionResult> GenerateFromRecordedScenario([FromBody] ScriptGenerationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ScenarioName))
                {
                    return BadRequest(new { success = false, error = "ScenarioName is required." });
                }

                if (string.IsNullOrWhiteSpace(request.Module))
                {
                    return BadRequest(new { success = false, error = "Module is required." });
                }

                var manager = new ScenarioManager();
                var scenario = manager.LoadScenario(request.ScenarioName, request.Module);

                if (scenario == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = $"Recorded scenario '{request.ScenarioName}' was not found in module '{request.Module}'."
                    });
                }

                var generator = CreateGenerator(request.Framework, request.Language);
                var script = await generator.GenerateTestCodeAsync(scenario);
                var pageObject = await generator.GeneratePageObjectAsync(scenario);

                Dictionary<string, string>? projectFiles = null;
                if (request.IncludeProjectFiles)
                {
                    projectFiles = await generator.GenerateProjectFilesAsync(new List<TestScenario> { scenario });
                }

                var testCases = BuildDerivedTestCases(scenario);
                var advancedRecommendations = BuildAdvancedRecommendations(scenario);

                return Ok(new
                {
                    success = true,
                    scenario = new
                    {
                        scenario.Name,
                        scenario.Module,
                        scenario.Description,
                        actionCount = scenario.Actions?.Count ?? 0,
                        assertionCount = scenario.Assertions?.Count ?? 0,
                        stepCount = scenario.Steps?.Count ?? 0,
                        scenario.Tags
                    },
                    generation = new
                    {
                        framework = generator.GetFrameworkName(),
                        language = generator.GetLanguage(),
                        fileExtension = generator.GetFileExtension(),
                        suggestedFileName = BuildSuggestedFileName(scenario, generator.GetFileExtension())
                    },
                    outputs = new
                    {
                        testScript = script,
                        pageObject = pageObject,
                        projectFiles
                    },
                    derivedTestCases = testCases,
                    advancedConcepts = advancedRecommendations
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        private static ICodeGenerator CreateGenerator(string framework, string language)
        {
            var normalizedFramework = (framework ?? "playwright").Trim().ToLowerInvariant();
            var exportLanguage = ParseLanguage(language);

            return normalizedFramework switch
            {
                "selenium" => new SeleniumCodeGenerator(),
                "cypress" => new CypressCodeGenerator(exportLanguage),
                _ => new PlaywrightCodeGenerator(exportLanguage)
            };
        }

        private static ExportLanguage ParseLanguage(string language)
        {
            var normalized = (language ?? "csharp").Trim().ToLowerInvariant();

            return normalized switch
            {
                "python" => ExportLanguage.Python,
                "javascript" => ExportLanguage.JavaScript,
                "typescript" => ExportLanguage.TypeScript,
                _ => ExportLanguage.CSharp
            };
        }

        private static string BuildSuggestedFileName(TestScenario scenario, string extension)
        {
            var safeName = string.Join("_", (scenario.Name ?? "RecordedScenario")
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

            return safeName + extension;
        }

        private static List<object> BuildDerivedTestCases(TestScenario scenario)
        {
            var baseName = scenario.Name ?? "Recorded_Scenario";
            var tags = scenario.Tags ?? new List<string>();
            var hasTypeActions = (scenario.Actions?.Any(a => string.Equals(a.ActionType, "type", StringComparison.OrdinalIgnoreCase)) ?? false)
                                 || (scenario.Steps?.Any(s => s.StepType == "Action" && s.Action != null && string.Equals(s.Action.ActionType, "type", StringComparison.OrdinalIgnoreCase)) ?? false);

            var cases = new List<object>
            {
                new
                {
                    name = baseName + "_Smoke",
                    objective = "Validate the core happy path from the recorded flow.",
                    priority = "High",
                    type = "Functional",
                    suggestedTags = tags.Concat(new[] { "smoke" }).Distinct().ToList()
                },
                new
                {
                    name = baseName + "_Resilience",
                    objective = "Add explicit assertions and retry-safe steps to improve execution reliability.",
                    priority = "Medium",
                    type = "Stability",
                    suggestedTags = tags.Concat(new[] { "stability" }).Distinct().ToList()
                }
            };

            if (hasTypeActions)
            {
                cases.Add(new
                {
                    name = baseName + "_DataVariants",
                    objective = "Parameterize typed inputs and run with positive/negative/boundary datasets.",
                    priority = "High",
                    type = "Data-Driven",
                    suggestedTags = tags.Concat(new[] { "datadriven", "regression" }).Distinct().ToList()
                });
            }

            return cases;
        }

        private static List<object> BuildAdvancedRecommendations(TestScenario scenario)
        {
            var recommendations = new List<object>();

            var assertionsCount = scenario.Assertions?.Count ?? 0;
            var actionsCount = scenario.Actions?.Count ?? 0;

            recommendations.Add(new
            {
                concept = "Script Generation Pipeline",
                value = "Generate Playwright/Selenium/Cypress scripts directly from recorded scenarios for faster CI onboarding.",
                impact = "High"
            });

            recommendations.Add(new
            {
                concept = "Parameterized Inputs",
                value = "Replace hardcoded typed values with placeholders to support multi-environment and multi-dataset executions.",
                impact = "High"
            });

            if (assertionsCount < 2)
            {
                recommendations.Add(new
                {
                    concept = "Assertion Hardening",
                    value = "Increase assertions for key checkpoints (URL, visibility, text) to avoid false positives.",
                    impact = "High"
                });
            }

            recommendations.Add(new
            {
                concept = "Self-Healing Locators",
                value = "Add fallback locator strategies and object repository mapping for brittle selectors.",
                impact = "Medium"
            });

            recommendations.Add(new
            {
                concept = "Observability",
                value = "Capture step-level screenshots, timings, and logs to speed up triage in failed runs.",
                impact = "Medium"
            });

            recommendations.Add(new
            {
                concept = "Execution Strategy",
                value = actionsCount > 8
                    ? "Split long recorded journeys into smaller reusable scenarios to improve maintainability."
                    : "Use this scenario as a reusable building block in suite-level workflows.",
                impact = "Medium"
            });

            return recommendations;
        }
    }

    public class ScriptGenerationRequest
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Framework { get; set; } = "playwright";
        public string Language { get; set; } = "csharp";
        public bool IncludeProjectFiles { get; set; }
    }
}
