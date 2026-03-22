using AgenticAI.Core.ZeroCode.Models;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Interface for code generation
    /// </summary>
    public interface ICodeGenerator
    {
        /// <summary>
        /// Generate test code from scenario
        /// </summary>
        Task<string> GenerateTestCodeAsync(TestScenario scenario);

        /// <summary>
        /// Generate page object code
        /// </summary>
        Task<string> GeneratePageObjectAsync(TestScenario scenario);

        /// <summary>
        /// Generate project configuration/setup files
        /// </summary>
        Task<Dictionary<string, string>> GenerateProjectFilesAsync(List<TestScenario> scenarios);

        /// <summary>
        /// Get the framework name
        /// </summary>
        string GetFrameworkName();

        /// <summary>
        /// Get the language used
        /// </summary>
        string GetLanguage();

        /// <summary>
        /// Get file extension
        /// </summary>
        string GetFileExtension();
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        Selenium,
        Playwright,
        Cypress
    }

    /// <summary>
    /// Export language options (where applicable)
    /// </summary>
    public enum ExportLanguage
    {
        CSharp,
        Python,
        JavaScript,
        TypeScript
    }

    /// <summary>
    /// Code generation options
    /// </summary>
    public class CodeGenerationOptions
    {
        public ExportFormat Format { get; set; } = ExportFormat.Playwright;
        public ExportLanguage Language { get; set; } = ExportLanguage.CSharp;
        public string ProjectName { get; set; } = "AutomationProject";
        public string ProjectPath { get; set; } = "";
        public bool IncludePageObjects { get; set; } = true;
        public bool IncludeConfiguration { get; set; } = true;
        public bool IncludeHelpers { get; set; } = true;
        public string Namespace { get; set; } = "AutomationFramework.Tests";
        public bool IncludeReadme { get; set; } = true;
        public string Author { get; set; } = "Agentic AI Framework";
        public bool HybridExport { get; set; } = false;
    }

    /// <summary>
    /// Generated project structure
    /// </summary>
    public class GeneratedProject
    {
        public string ProjectName { get; set; } = "";
        public ExportFormat Format { get; set; }
        public ExportLanguage Language { get; set; }
        public Dictionary<string, string> Files { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
