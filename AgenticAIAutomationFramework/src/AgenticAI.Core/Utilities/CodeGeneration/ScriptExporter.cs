using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;
using System.IO.Compression;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Manages code generation and project export
    /// </summary>
    public class ScriptExporter
    {
        // Generate HybridDriver.cs
        private string GenerateHybridDriverFile(CodeGenerationOptions options)
        {
            return "namespace HybridFramework {\n    public class HybridDriver {\n        // Hybrid driver logic here\n    }\n}";
        }

        // Generate HybridConfig.json
        private string GenerateHybridConfigFile(CodeGenerationOptions options)
        {
            return "{\n  \"framework\": \"Hybrid\",\n  \"projectName\": \"" + options.ProjectName + "\"\n}";
        }

    // Generate config.json
    private string GenerateConfigFile(CodeGenerationOptions options)
    {
        return "{\n  \"projectName\": \"" + options.ProjectName + "\",\n  \"namespace\": \"" + options.Namespace + "\"\n}";
    }

    // Generate appsettings.json
    private string GenerateAppSettings(CodeGenerationOptions options)
    {
        return "{\n  \"author\": \"" + options.Author + "\",\n  \"generatedAt\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"\n}";
    }

    // Generate Helpers.cs
    private string GenerateHelpersFile(CodeGenerationOptions options)
    {
        return "namespace " + options.Namespace + " {\n    public static class Helpers {\n        // Add helper methods here\n    }\n}";
    }
        /// Export a single test scenario to code
        /// </summary>
        public async Task<string> ExportTestCodeAsync(
            TestScenario scenario,
            CodeGenerationOptions options)
        {
            Logger.Info($"Exporting test '{scenario.Name}' as {options.Format} ({options.Language})");

            ICodeGenerator generator = options.Format switch
            {
                ExportFormat.Selenium => new SeleniumCodeGenerator(),
                ExportFormat.Playwright => new PlaywrightCodeGenerator(options.Language),
                ExportFormat.Cypress => new CypressCodeGenerator(options.Language),
                _ => throw new ArgumentException($"Unknown format: {options.Format}")
            };

            var code = await generator.GenerateTestCodeAsync(scenario);
            Logger.Info($"Test code generated successfully");
            return code;
        }

        /// <summary>
        /// Export a complete project structure
        /// </summary>
        public async Task<GeneratedProject> ExportProjectAsync(List<TestScenario> scenarios, CodeGenerationOptions options)
        {
            Logger.Info($"Exporting project '{options.ProjectName}' as {options.Format}");

            ICodeGenerator generator = options.Format switch
            {
                ExportFormat.Selenium => new SeleniumCodeGenerator(),
                ExportFormat.Playwright => new PlaywrightCodeGenerator(options.Language),
                ExportFormat.Cypress => new CypressCodeGenerator(options.Language),
                _ => throw new ArgumentException($"Unknown format: {options.Format}")
            };

            var project = new GeneratedProject
            {
                ProjectName = options.ProjectName,
                Format = options.Format,
                Language = options.Language,
                GeneratedAt = DateTime.Now
            };

            // Hybrid Driven Framework export
            if (options.HybridExport)
            {
                if (!project.Files.ContainsKey("HybridFramework/HybridDriver.cs"))
                    project.Files["HybridFramework/HybridDriver.cs"] = GenerateHybridDriverFile(options);
                if (!project.Files.ContainsKey("HybridFramework/HybridConfig.json"))
                    project.Files["HybridFramework/HybridConfig.json"] = GenerateHybridConfigFile(options);
            }

            // Add project files
            var projectFiles = await generator.GenerateProjectFilesAsync(scenarios);
            foreach (var file in projectFiles)
            {
                project.Files[file.Key] = file.Value;
            }

            // Add configuration files if requested
            if (options.IncludeConfiguration)
            {
                // Example: Add config.json or appsettings.json
                if (!project.Files.ContainsKey("config.json"))
                    project.Files["config.json"] = GenerateConfigFile(options);
                if (!project.Files.ContainsKey("appsettings.json"))
                    project.Files["appsettings.json"] = GenerateAppSettings(options);
            }

            // Add helper files if requested
            if (options.IncludeHelpers)
            {
                if (!project.Files.ContainsKey("Helpers/Helpers.cs"))
                    project.Files["Helpers/Helpers.cs"] = GenerateHelpersFile(options);
            }

            // Add test files
            var testDir = options.Format == ExportFormat.Cypress ? "cypress/e2e/" : "Tests/";
            foreach (var scenario in scenarios)
            {
                var testCode = await generator.GenerateTestCodeAsync(scenario);
                var testFileName = NormalizeFileName(scenario.Name) + generator.GetFileExtension();
                project.Files[$"{testDir}{testFileName}"] = testCode;

                if (options.IncludePageObjects)
                {
                    var pageCode = await generator.GeneratePageObjectAsync(scenario);
                    var pageFileName = NormalizeFileName(scenario.Name) + "_page" + generator.GetFileExtension();
                    var pageDir = options.Format == ExportFormat.Cypress ? "cypress/support/pages/" : "Pages/";
                    project.Files[$"{pageDir}{pageFileName}"] = pageCode;
                }
            }

            // Launchers so the exported project is runnable straight after unzipping
            if (options.IncludeRunScripts)
            {
                foreach (var script in GenerateRunScripts(options))
                {
                    if (!project.Files.ContainsKey(script.Key))
                        project.Files[script.Key] = script.Value;
                }
            }

            // Add README if requested
            if (options.IncludeReadme && !project.Files.ContainsKey("README_EXPORT.md"))
            {
                project.Files["README_EXPORT.md"] = GenerateExportReadme(scenarios, options);
            }

            Logger.Info($"Project exported with {project.Files.Count} files");
            return project;
        }

        /// <summary>
        /// Export project to disk
        /// </summary>
        public async Task<string> ExportProjectToPathAsync(
            List<TestScenario> scenarios,
            CodeGenerationOptions options)
        {
            var project = await ExportProjectAsync(scenarios, options);

            if (string.IsNullOrEmpty(options.ProjectPath))
                options.ProjectPath = Path.Combine(Directory.GetCurrentDirectory(), options.ProjectName);

            Directory.CreateDirectory(options.ProjectPath);

            foreach (var file in project.Files)
            {
                var filePath = Path.Combine(options.ProjectPath, file.Key);
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllTextAsync(filePath, file.Value);
                Logger.Debug($"Created file: {filePath}");
            }

            Logger.Info($"Project exported to: {options.ProjectPath}");
            return options.ProjectPath;
        }

        /// <summary>
        /// Export project as ZIP archive
        /// </summary>
        public async Task<string> ExportProjectAsZipAsync(
            List<TestScenario> scenarios,
            CodeGenerationOptions options,
            string? outputPath = null)
        {
            var project = await ExportProjectAsync(scenarios, options);

            outputPath ??= Path.Combine(
                Directory.GetCurrentDirectory(),
                $"{options.ProjectName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            );

            using (var zipStream = File.Create(outputPath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in project.Files)
                {
                    var entry = archive.CreateEntry(file.Key);
                    if (file.Key.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                        entry.ExternalAttributes = 0x81ED << 16; // -rwxr-xr-x, so ./run-tests.sh works after unzip
                    using (var entryStream = entry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        await writer.WriteAsync(file.Value);
                    }
                }
            }

            Logger.Info($"Project exported as ZIP: {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Generate project structure summary
        /// </summary>
        public async Task<string> GenerateProjectStructureSummaryAsync(
            List<TestScenario> scenarios,
            CodeGenerationOptions options)
        {
            var project = await ExportProjectAsync(scenarios, options);

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"=== {options.ProjectName} Project Structure ===");
            summary.AppendLine($"Framework: {options.Format}");
            summary.AppendLine($"Language: {options.Language}");
            summary.AppendLine($"Generated: {project.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine();

            summary.AppendLine($"Total Files: {project.Files.Count}");
            summary.AppendLine($"Test Scenarios: {scenarios.Count}");
            summary.AppendLine();

            summary.AppendLine("Directory Structure:");
            var directories = new HashSet<string>();
            foreach (var file in project.Files.Keys.OrderBy(f => f))
            {
                var dir = Path.GetDirectoryName(file) ?? ".";
                if (!directories.Contains(dir))
                {
                    directories.Add(dir);
                    var indent = new string(' ', (dir.Split('/').Length - 1) * 2);
                    summary.AppendLine($"{indent}{Path.GetFileName(dir)}/");
                }
                var fileName = Path.GetFileName(file);
                var fileIndent = new string(' ', (file.Split('/').Length) * 2);
                summary.AppendLine($"{fileIndent}{fileName}");
            }

            return summary.ToString();
        }

        /// <summary>
        /// Batch export multiple formats
        /// </summary>
        public async Task<Dictionary<ExportFormat, string>> ExportMultipleFormatsAsync(
            List<TestScenario> scenarios,
            ExportLanguage language = ExportLanguage.CSharp,
            string? basePath = null)
        {
            var results = new Dictionary<ExportFormat, string>();
            basePath ??= Directory.GetCurrentDirectory();

            var formats = new[] { ExportFormat.Selenium, ExportFormat.Playwright, ExportFormat.Cypress };

            foreach (var format in formats)
            {
                try
                {
                    var options = new CodeGenerationOptions
                    {
                        Format = format,
                        Language = language,
                        ProjectName = $"AutomationTests_{format}",
                        ProjectPath = Path.Combine(basePath, $"AutomationTests_{format}")
                    };

                    var path = await ExportProjectToPathAsync(scenarios, options);
                    results[format] = path;
                    Logger.Info($"Successfully exported {format} project");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error exporting {format} project: {ex.Message}");
                }
            }

            return results;
        }

        #region Run scripts

        private sealed record RunStep(string Label, string Windows, string Unix, bool IsTestRun = false);

        /// <summary>
        /// Builds run-tests.bat (CRLF) and run-tests.sh (LF) for the exported project:
        /// check prerequisites, install dependencies/browsers, then run the tests.
        /// </summary>
        private Dictionary<string, string> GenerateRunScripts(CodeGenerationOptions options)
        {
            var steps = new List<RunStep>();
            var prerequisites = new List<(string Tool, string Hint)>();

            switch (options.Format)
            {
                case ExportFormat.Selenium:
                    prerequisites.Add(("dotnet", ".NET SDK 9 from https://dotnet.microsoft.com/download"));
                    steps.Add(new RunStep("Running tests", "dotnet test %*", "dotnet test \"$@\"", true));
                    break;

                case ExportFormat.Cypress:
                    prerequisites.Add(("node", "Node.js 18+ from https://nodejs.org"));
                    steps.Add(new RunStep("Installing dependencies", "call npm install", "npm install"));
                    steps.Add(new RunStep("Running tests", "call npx cypress run %*", "npx cypress run \"$@\"", true));
                    break;

                case ExportFormat.Playwright when options.Language == ExportLanguage.CSharp:
                    prerequisites.Add(("dotnet", ".NET SDK 9 from https://dotnet.microsoft.com/download"));
                    steps.Add(new RunStep("Building project", "dotnet build", "dotnet build"));
                    steps.Add(new RunStep("Installing Playwright browsers",
                        "powershell -NoProfile -ExecutionPolicy Bypass -File bin\\Debug\\net9.0\\playwright.ps1 install chromium",
                        "pwsh bin/Debug/net9.0/playwright.ps1 install chromium"));
                    steps.Add(new RunStep("Running tests", "dotnet test --no-build %*", "dotnet test --no-build \"$@\"", true));
                    prerequisites.Add(("pwsh", "PowerShell 7 from https://aka.ms/powershell (macOS/Linux only; used to install browsers)"));
                    break;

                case ExportFormat.Playwright when options.Language == ExportLanguage.Python:
                    prerequisites.Add(("python", "Python 3.9+ from https://www.python.org"));
                    steps.Add(new RunStep("Creating virtual environment",
                        "if not exist .venv python -m venv .venv", "[ -d .venv ] || python3 -m venv .venv"));
                    steps.Add(new RunStep("Installing dependencies",
                        ".venv\\Scripts\\python -m pip install -r requirements.txt",
                        ".venv/bin/python -m pip install -r requirements.txt"));
                    steps.Add(new RunStep("Installing Playwright browsers",
                        ".venv\\Scripts\\python -m playwright install chromium",
                        ".venv/bin/python -m playwright install chromium"));
                    steps.Add(new RunStep("Running tests",
                        ".venv\\Scripts\\python -m pytest %*", ".venv/bin/python -m pytest \"$@\"", true));
                    break;

                default: // Playwright JavaScript / TypeScript
                    prerequisites.Add(("node", "Node.js 18+ from https://nodejs.org"));
                    steps.Add(new RunStep("Installing dependencies", "call npm install", "npm install"));
                    steps.Add(new RunStep("Installing Playwright browsers", "call npx playwright install chromium", "npx playwright install chromium"));
                    steps.Add(new RunStep("Running tests", "call npx playwright test %*", "npx playwright test \"$@\"", true));
                    break;
            }

            return new Dictionary<string, string>
            {
                ["run-tests.bat"] = BuildBatchScript(options, prerequisites, steps),
                ["run-tests.sh"] = BuildShellScript(options, prerequisites, steps)
            };
        }

        private static string BuildBatchScript(CodeGenerationOptions options, List<(string Tool, string Hint)> prerequisites, List<RunStep> steps)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("REM Generated by Agentic AI Framework: installs dependencies and runs the tests");
            sb.AppendLine("setlocal");
            sb.AppendLine("cd /d \"%~dp0\"");
            sb.AppendLine($"echo === {options.ProjectName} ({options.Format} / {options.Language}) ===");
            sb.AppendLine();

            foreach (var (tool, hint) in prerequisites.Where(p => p.Tool != "pwsh")) // Windows ships PowerShell
            {
                var windowsTool = tool == "python" ? "python" : tool;
                sb.AppendLine($"where {windowsTool} >nul 2>nul");
                sb.AppendLine($"if errorlevel 1 (echo ERROR: {tool} was not found. Install {hint} & set EXITCODE=1 & goto :end)");
            }
            sb.AppendLine();

            var number = 0;
            foreach (var step in steps)
            {
                number++;
                sb.AppendLine($"echo [{number}/{steps.Count}] {step.Label}...");
                sb.AppendLine(step.Windows);
                sb.AppendLine("set EXITCODE=%errorlevel%");
                sb.AppendLine(step.IsTestRun
                    ? "goto :end"
                    : $"if not \"%EXITCODE%\"==\"0\" (echo ERROR: {step.Label} failed. & goto :end)");
                sb.AppendLine();
            }

            sb.AppendLine(":end");
            sb.AppendLine("if \"%EXITCODE%\"==\"0\" (echo All tests passed.) else (echo Finished with errors, exit code %EXITCODE%.)");
            sb.AppendLine("if \"%CI%\"==\"\" pause");
            sb.AppendLine("exit /b %EXITCODE%");
            return sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private static string BuildShellScript(CodeGenerationOptions options, List<(string Tool, string Hint)> prerequisites, List<RunStep> steps)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("#!/usr/bin/env bash\n");
            sb.Append("# Generated by Agentic AI Framework: installs dependencies and runs the tests\n");
            sb.Append("cd \"$(dirname \"$0\")\" || exit 1\n");
            sb.Append($"echo \"=== {options.ProjectName} ({options.Format} / {options.Language}) ===\"\n\n");

            foreach (var (tool, hint) in prerequisites)
            {
                var unixTool = tool == "python" ? "python3" : tool;
                sb.Append($"command -v {unixTool} >/dev/null 2>&1 || {{ echo \"ERROR: {unixTool} was not found. Install {hint}\"; exit 1; }}\n");
            }
            sb.Append('\n');

            var number = 0;
            foreach (var step in steps)
            {
                number++;
                sb.Append($"echo \"[{number}/{steps.Count}] {step.Label}...\"\n");
                if (step.IsTestRun)
                {
                    sb.Append(step.Unix + "\n");
                    sb.Append("code=$?\n");
                    sb.Append("if [ $code -eq 0 ]; then echo \"All tests passed.\"; else echo \"Finished with errors, exit code $code.\"; fi\n");
                    sb.Append("exit $code\n");
                }
                else
                {
                    sb.Append($"{step.Unix} || {{ echo \"ERROR: {step.Label} failed.\"; exit 1; }}\n\n");
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Helper Methods

        private string NormalizeFileName(string name)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                name.ToLower(),
                @"[^a-z0-9]+",
                "_"
            ).Trim('_');
        }

        private string GenerateExportReadme(List<TestScenario> scenarios, CodeGenerationOptions options)
        {
            var readme = new System.Text.StringBuilder();

            readme.AppendLine($"# {options.ProjectName}");
            readme.AppendLine();
            readme.AppendLine($"**Framework:** {options.Format}");
            readme.AppendLine($"**Language:** {options.Language}");
            readme.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            readme.AppendLine($"**Author:** {options.Author}");
            readme.AppendLine();

            readme.AppendLine("## Test Scenarios");
            readme.AppendLine();
            foreach (var scenario in scenarios)
            {
                readme.AppendLine($"### {scenario.Name}");
                if (!string.IsNullOrEmpty(scenario.Description))
                    readme.AppendLine($"- **Description:** {scenario.Description}");
                readme.AppendLine($"- **Module:** {scenario.Module}");
                if (scenario.Tags.Count > 0)
                    readme.AppendLine($"- **Tags:** {string.Join(", ", scenario.Tags)}");
                if (!string.IsNullOrEmpty(scenario.StartUrl))
                    readme.AppendLine($"- **Start URL:** {scenario.StartUrl}");
                readme.AppendLine($"- **Steps:** {scenario.Steps.Count + scenario.Actions.Count}");
                readme.AppendLine();
            }

            readme.AppendLine("## Quick Start");
            if (options.IncludeRunScripts)
            {
                readme.AppendLine("Run everything (install dependencies, install browsers, execute tests) with one command:");
                readme.AppendLine("```bash");
                readme.AppendLine("run-tests.bat        # Windows");
                readme.AppendLine("./run-tests.sh       # macOS / Linux");
                readme.AppendLine("```");
                readme.AppendLine("Extra arguments are passed to the test runner, e.g. `run-tests.bat --headed` for Playwright (Node).");
                readme.AppendLine();
                readme.AppendLine("Or run the steps manually:");
            }
            switch (options.Format)
            {
                case ExportFormat.Selenium:
                    readme.AppendLine("```bash");
                    readme.AppendLine("dotnet test");
                    readme.AppendLine("```");
                    break;
                case ExportFormat.Playwright:
                    readme.AppendLine(options.Language switch
                    {
                        ExportLanguage.Python => "```bash\npip install -r requirements.txt\npython -m playwright install chromium\npytest\n```",
                        ExportLanguage.CSharp => "```bash\ndotnet build\npwsh bin/Debug/net9.0/playwright.ps1 install chromium\ndotnet test\n```",
                        _ => "```bash\nnpm install\nnpx playwright install chromium\nnpm test\n```"
                    });
                    break;
                case ExportFormat.Cypress:
                    readme.AppendLine("```bash");
                    readme.AppendLine("npm install");
                    readme.AppendLine("npm run test:open");
                    readme.AppendLine("```");
                    break;
            }

            readme.AppendLine();
            readme.AppendLine($"**Total Test Scenarios:** {scenarios.Count}");
            readme.AppendLine($"**Total Steps:** {scenarios.Sum(s => s.Steps.Count + s.Actions.Count)}");

            return readme.ToString();
        }

        #endregion
    }

}
