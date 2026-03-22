using Microsoft.AspNetCore.Mvc;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Utilities.CodeGeneration;
using AgenticAI.Core.Logging;

namespace AgenticAI.WebUI.Controllers
{
    /// <summary>
    /// API controller for script export and code generation
    /// </summary>
    [ApiController]
    [Route("api/export")]
    public class ScriptExportController : ControllerBase
    {
        private readonly ScriptExporter _exporter = new ScriptExporter();

        /// <summary>
        /// Get available export formats
        /// </summary>
        [HttpGet("formats")]
        public IActionResult GetFormats()
        {
            var formats = Enum.GetValues(typeof(ExportFormat))
                .Cast<ExportFormat>()
                .Select(f => new { format = f.ToString(), name = f })
                .ToList();

            return Ok(new { formats });
        }

        /// <summary>
        /// Get available languages for a format
        /// </summary>
        [HttpGet("languages/{format}")]
        public IActionResult GetLanguagesForFormat(string format)
        {
            if (!Enum.TryParse<ExportFormat>(format, true, out var exportFormat))
                return BadRequest(new { error = "Invalid format" });

            var languages = CodeGeneratorFactory.GetSupportedLanguages(exportFormat)
                .Select(l => l.ToString())
                .ToList();

            return Ok(new { format, languages });
        }

        /// <summary>
        /// Generate test code from a single scenario
        /// </summary>
        [HttpPost("generate-test")]
        public async Task<IActionResult> GenerateTestCode([FromBody] GenerateTestRequest request)
        {
            try
            {
                if (request?.Scenario == null)
                    return BadRequest(new { error = "Scenario is required" });

                if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format) ||
                    !Enum.TryParse<ExportLanguage>(request.Language, true, out var language))
                    return BadRequest(new { error = "Invalid format or language" });

                var options = new CodeGenerationOptions
                {
                    Format = format,
                    Language = language
                };

                var code = await _exporter.ExportTestCodeAsync(request.Scenario, options);

                return Ok(new { code });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating test code: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Export complete project structure
        /// </summary>
        [HttpPost("generate-project")]
        public async Task<IActionResult> GenerateProject([FromBody] GenerateProjectRequest request)
        {
            try
            {
                if (request?.Scenarios == null || request.Scenarios.Count == 0)
                    return BadRequest(new { error = "At least one scenario is required" });

                if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format) ||
                    !Enum.TryParse<ExportLanguage>(request.Language, true, out var language))
                    return BadRequest(new { error = "Invalid format or language" });

                // Validate format/language combination
                var supportedLanguages = CodeGeneratorFactory.GetSupportedLanguages(format);
                if (!supportedLanguages.Contains(language))
                    return BadRequest(new { error = $"Language {language} not supported for {format}" });

                var options = new CodeGenerationOptions
                {
                    Format = format,
                    Language = language,
                    ProjectName = request.ProjectName ?? "AutomationTests",
                    IncludePageObjects = request.IncludePageObjects ?? true,
                    IncludeConfiguration = request.IncludeConfiguration ?? true,
                    IncludeReadme = request.IncludeReadme ?? true,
                    Author = request.Author ?? "Agentic AI Framework"
                };

                var project = await _exporter.ExportProjectAsync(request.Scenarios, options);

                return Ok(new
                {
                    projectName = project.ProjectName,
                    format = project.Format.ToString(),
                    language = project.Language.ToString(),
                    generatedAt = project.GeneratedAt,
                    fileCount = project.Files.Count,
                    files = project.Files.Keys.OrderBy(k => k).ToList()
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating project: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Export project as ZIP file
        /// </summary>
        [HttpPost("export-zip")]
        public async Task<IActionResult> ExportAsZip([FromBody] GenerateProjectRequest request)
        {
            try
            {
                if (request?.Scenarios == null || request.Scenarios.Count == 0)
                    return BadRequest(new { error = "At least one scenario is required" });

                if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format) ||
                    !Enum.TryParse<ExportLanguage>(request.Language, true, out var language))
                    return BadRequest(new { error = "Invalid format or language" });

                var options = new CodeGenerationOptions
                {
                    Format = format,
                    Language = language,
                    ProjectName = request.ProjectName ?? "AutomationTests"
                };

                var zipPath = await _exporter.ExportProjectAsZipAsync(request.Scenarios, options);

                var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
                var fileName = $"{options.ProjectName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

                // Clean up temp file after sending
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Wait for download to start
                    try { System.IO.File.Delete(zipPath); }
                    catch { /* ignored */ }
                });

                return File(zipBytes, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error exporting ZIP: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get project structure summary
        /// </summary>
        [HttpPost("project-summary")]
        public async Task<IActionResult> GetProjectSummary([FromBody] GenerateProjectRequest request)
        {
            try
            {
                if (request?.Scenarios == null || request.Scenarios.Count == 0)
                    return BadRequest(new { error = "At least one scenario is required" });

                if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format) ||
                    !Enum.TryParse<ExportLanguage>(request.Language, true, out var language))
                    return BadRequest(new { error = "Invalid format or language" });

                var options = new CodeGenerationOptions
                {
                    Format = format,
                    Language = language,
                    ProjectName = request.ProjectName ?? "AutomationTests"
                };

                var summary = await _exporter.GenerateProjectStructureSummaryAsync(request.Scenarios, options);

                return Ok(new { summary });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating summary: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get page object code for a scenario
        /// </summary>
        [HttpPost("generate-page-object")]
        public async Task<IActionResult> GeneratePageObject([FromBody] GenerateTestRequest request)
        {
            try
            {
                if (request?.Scenario == null)
                    return BadRequest(new { error = "Scenario is required" });

                if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format) ||
                    !Enum.TryParse<ExportLanguage>(request.Language, true, out var language))
                    return BadRequest(new { error = "Invalid format or language" });

                ICodeGenerator generator = CodeGeneratorFactory.CreateGenerator(format, language);
                var code = await generator.GeneratePageObjectAsync(request.Scenario);

                return Ok(new { code });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating page object: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Batch export to multiple formats
        /// </summary>
        [HttpPost("export-multiple")]
        public async Task<IActionResult> ExportMultipleFormats([FromBody] ExportMultipleRequest request)
        {
            try
            {
                if (request?.Scenarios == null || request.Scenarios.Count == 0)
                    return BadRequest(new { error = "At least one scenario is required" });

                var basePath = Path.Combine(Path.GetTempPath(), $"ExportTemp_{Guid.NewGuid()}");

                var results = await _exporter.ExportMultipleFormatsAsync(
                    request.Scenarios,
                    Enum.TryParse<ExportLanguage>(request.Language, true, out var lang) ? lang : ExportLanguage.CSharp,
                    basePath
                );

                // Create a master ZIP with all frameworks
                var masterZipPath = Path.Combine(
                    Path.GetTempPath(),
                    $"AllFrameworks_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                );

                using (var zipStream = System.IO.File.Create(masterZipPath))
                using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var (format, path) in results)
                    {
                        if (Directory.Exists(path))
                        {
                            AddDirectoryToZip(archive, path, format.ToString());
                        }
                    }
                }

                var zipBytes = await System.IO.File.ReadAllBytesAsync(masterZipPath);

                // Clean up
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    try
                    {
                        Directory.Delete(basePath, true);
                        System.IO.File.Delete(masterZipPath);
                    }
                    catch { /* ignored */ }
                });

                var fileName = $"AllFrameworks_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                return File(zipBytes, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in batch export: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        #region Helper Methods

        private void AddDirectoryToZip(System.IO.Compression.ZipArchive archive, string directoryPath, string prefix)
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var entryPath = $"{prefix}/{relativePath}".Replace("\\", "/");
                var entry = archive.CreateEntry(entryPath);

                using (var entryStream = entry.Open())
                using (var fileStream = System.IO.File.OpenRead(file))
                {
                    fileStream.CopyTo(entryStream);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Request model for generating test code
    /// </summary>
    public class GenerateTestRequest
    {
        public TestScenario? Scenario { get; set; }
        public string Format { get; set; } = "Playwright";
        public string Language { get; set; } = "CSharp";
    }

    /// <summary>
    /// Request model for generating project
    /// </summary>
    public class GenerateProjectRequest
    {
        public List<TestScenario>? Scenarios { get; set; }
        public string Format { get; set; } = "Playwright";
        public string Language { get; set; } = "CSharp";
        public string? ProjectName { get; set; }
        public bool? IncludePageObjects { get; set; }
        public bool? IncludeConfiguration { get; set; }
        public bool? IncludeReadme { get; set; }
        public string? Author { get; set; }
    }

    /// <summary>
    /// Request model for exporting to multiple formats
    /// </summary>
    public class ExportMultipleRequest
    {
        public List<TestScenario>? Scenarios { get; set; }
        public string Language { get; set; } = "CSharp";
    }
}
