using System;
using System.Collections.Generic;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Helper class for getting appropriate code generator
    /// </summary>
    public static class CodeGeneratorFactory
    {
        /// <summary>
        /// Create code generator based on format and language
        /// </summary>
        public static ICodeGenerator CreateGenerator(ExportFormat format, ExportLanguage language)
        {
            return format switch
            {
                ExportFormat.Selenium => new SeleniumCodeGenerator(),
                ExportFormat.Playwright => new PlaywrightCodeGenerator(language),
                ExportFormat.Cypress => new CypressCodeGenerator(language),
                _ => throw new ArgumentException($"Unknown format: {format}")
            };
        }

        /// <summary>
        /// Get supported languages for a format
        /// </summary>
        public static List<ExportLanguage> GetSupportedLanguages(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Selenium => new() { ExportLanguage.CSharp },
                ExportFormat.Playwright => new() { ExportLanguage.CSharp, ExportLanguage.Python, ExportLanguage.JavaScript, ExportLanguage.TypeScript },
                ExportFormat.Cypress => new() { ExportLanguage.JavaScript, ExportLanguage.TypeScript },
                _ => new()
            };
        }

        /// <summary>
        /// Get the default language for a format
        /// </summary>
        public static ExportLanguage GetDefaultLanguage(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Selenium => ExportLanguage.CSharp,
                ExportFormat.Playwright => ExportLanguage.CSharp,
                ExportFormat.Cypress => ExportLanguage.JavaScript,
                _ => ExportLanguage.CSharp
            };
        }
    }
}
