using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Accessibility testing for WCAG compliance
    /// </summary>
    public class AccessibilityTester
    {
        public enum AccessibilityLevel
        {
            A,      // Level A
            AA,     // Level AA (most common)
            AAA     // Level AAA (strictest)
        }

        public class AccessibilityIssue
        {
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public string Severity { get; set; } = "";  // Critical, Major, Minor
            public string WcagLevel { get; set; } = "";  // A, AA, AAA
            public string AffectedElement { get; set; } = "";
            public string Recommendation { get; set; } = "";
            public DateTime DetectedTime { get; set; } = DateTime.Now;
        }

        public class AccessibilityReport
        {
            public int TotalIssues { get; set; }
            public int CriticalIssues { get; set; }
            public int MajorIssues { get; set; }
            public int MinorIssues { get; set; }
            public List<AccessibilityIssue> Issues { get; set; } = new();
            public bool IsCompliant { get; set; }
            public AccessibilityLevel TargetLevel { get; set; } = AccessibilityLevel.AA;
            public DateTime TestTime { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Check for missing alt text on images
        /// </summary>
        public static async Task<List<AccessibilityIssue>> CheckMissingAltTextAsync(IWebDriver driver)
        {
            Logger.Info("Checking for missing alt text on images...");
            var issues = new List<AccessibilityIssue>();

            try
            {
                var images = await driver.FindElementsAsync("img");
                foreach (var img in images)
                {
                    var alt = await driver.GetAttributeAsync("img", "alt");
                    if (string.IsNullOrWhiteSpace(alt))
                    {
                        issues.Add(new AccessibilityIssue
                        {
                            Code = "A11Y-001",
                            Description = "Image missing alt text",
                            Severity = "Critical",
                            WcagLevel = "A",
                            Recommendation = "Add descriptive alt text to all images"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking alt text: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Check for sufficient color contrast
        /// </summary>
        public static async Task<List<AccessibilityIssue>> CheckColorContrastAsync(IWebDriver driver)
        {
            Logger.Info("Checking for sufficient color contrast...");
            var issues = new List<AccessibilityIssue>();

            try
            {
                const string contrastScript = @"
                    function getComputedStyle(el, prop) {
                        return window.getComputedStyle(el)[prop];
                    }
                    
                    function getLuminance(r, g, b) {
                        var a = [r, g, b].map(v => {
                            v = v / 255;
                            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
                        });
                        return a[0] * 0.2126 + a[1] * 0.7152 + a[2] * 0.0722;
                    }
                    
                    var textElements = document.querySelectorAll('p, span, a, button, label, h1, h2, h3, h4, h5, h6');
                    var issues = [];
                    
                    textElements.forEach(el => {
                        var bgColor = getComputedStyle(el, 'backgroundColor');
                        var fgColor = getComputedStyle(el, 'color');
                        // Simplified check - just return if colors are found
                        if (bgColor && fgColor && bgColor !== 'rgba(0, 0, 0, 0)') {
                            issues.push({element: el.tagName, bg: bgColor, fg: fgColor});
                        }
                    });
                    
                    return issues;
                ";

                var contrastIssues = await driver.ExecuteScriptAsync<dynamic>(contrastScript);
                if (contrastIssues != null)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        Code = "A11Y-002",
                        Description = "Possible insufficient color contrast",
                        Severity = "Minor",
                        WcagLevel = "AA",
                        Recommendation = "Ensure text has at least 4.5:1 contrast ratio for normal text, 3:1 for large text"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking contrast: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Check for proper form labels
        /// </summary>
        public static async Task<List<AccessibilityIssue>> CheckFormLabelsAsync(IWebDriver driver)
        {
            Logger.Info("Checking for proper form labels...");
            var issues = new List<AccessibilityIssue>();

            try
            {
                var inputs = await driver.FindElementsAsync("input");
                foreach (var input in inputs)
                {
                    var id = await driver.GetAttributeAsync("input", "id");
                    if (!string.IsNullOrEmpty(id))
                    {
                        try
                        {
                            var label = await driver.FindElementAsync($"label[for='{id}']");
                        }
                        catch
                        {
                            issues.Add(new AccessibilityIssue
                            {
                                Code = "A11Y-003",
                                Description = "Form input missing associated label",
                                Severity = "Major",
                                WcagLevel = "A",
                                AffectedElement = id,
                                Recommendation = "Associate all form inputs with labels using the 'for' attribute"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking form labels: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Check for keyboard accessibility
        /// </summary>
        public static async Task<List<AccessibilityIssue>> CheckKeyboardAccessibilityAsync(IWebDriver driver)
        {
            Logger.Info("Checking for keyboard accessibility...");
            var issues = new List<AccessibilityIssue>();

            try
            {
                const string keyboardScript = @"
                    var interactiveElements = document.querySelectorAll('button, a, input, select, textarea');
                    var issues = 0;
                    
                    interactiveElements.forEach(el => {
                        if (el.tabIndex < 0 && el.tagName !== 'BUTTON' && el.tagName !== 'A') {
                            issues++;
                        }
                    });
                    
                    return issues;
                ";

                var count = await driver.ExecuteScriptAsync<int>(keyboardScript);
                if (count > 0)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        Code = "A11Y-004",
                        Description = $"{count} interactive element(s) not keyboard accessible",
                        Severity = "Major",
                        WcagLevel = "A",
                        Recommendation = "Ensure all interactive elements are keyboard accessible"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking keyboard accessibility: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Check for proper heading structure
        /// </summary>
        public static async Task<List<AccessibilityIssue>> CheckHeadingStructureAsync(IWebDriver driver)
        {
            Logger.Info("Checking for proper heading structure...");
            var issues = new List<AccessibilityIssue>();

            try
            {
                const string headingScript = @"
                    var headings = document.querySelectorAll('h1, h2, h3, h4, h5, h6');
                    var headingLevels = [];
                    var issues = [];
                    
                    headings.forEach(h => {
                        var level = parseInt(h.tagName[1]);
                        headingLevels.push(level);
                    });
                    
                    // Check for skipped levels
                    for (var i = 1; i < headingLevels.length; i++) {
                        if (headingLevels[i] - headingLevels[i-1] > 1) {
                            issues.push('Heading level skipped');
                        }
                    }
                    
                    // Check for multiple h1 tags
                    var h1Count = document.querySelectorAll('h1').length;
                    if (h1Count !== 1) {
                        issues.push('Should have exactly one h1 tag');
                    }
                    
                    return issues;
                ";

                var headingIssues = await driver.ExecuteScriptAsync<List<string>>(headingScript);
                if (headingIssues?.Count > 0)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        Code = "A11Y-005",
                        Description = "Improper heading hierarchy",
                        Severity = "Minor",
                        WcagLevel = "A",
                        Recommendation = "Use proper heading hierarchy (h1, h2, h3, etc. in order)"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking heading structure: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Run full accessibility audit
        /// </summary>
        public static async Task<AccessibilityReport> RunFullAuditAsync(
            IWebDriver driver,
            AccessibilityLevel targetLevel = AccessibilityLevel.AA)
        {
            Logger.Info($"Starting full accessibility audit (Target Level: {targetLevel})...");

            var report = new AccessibilityReport { TargetLevel = targetLevel };

            var allIssues = new List<AccessibilityIssue>();
            allIssues.AddRange(await CheckMissingAltTextAsync(driver));
            allIssues.AddRange(await CheckColorContrastAsync(driver));
            allIssues.AddRange(await CheckFormLabelsAsync(driver));
            allIssues.AddRange(await CheckKeyboardAccessibilityAsync(driver));
            allIssues.AddRange(await CheckHeadingStructureAsync(driver));

            report.Issues = allIssues;
            report.TotalIssues = allIssues.Count;
            report.CriticalIssues = allIssues.Count(i => i.Severity == "Critical");
            report.MajorIssues = allIssues.Count(i => i.Severity == "Major");
            report.MinorIssues = allIssues.Count(i => i.Severity == "Minor");

            // Determine compliance based on target level
            report.IsCompliant = targetLevel switch
            {
                AccessibilityLevel.A => report.CriticalIssues == 0,
                AccessibilityLevel.AA => report.CriticalIssues + report.MajorIssues == 0,
                AccessibilityLevel.AAA => report.TotalIssues == 0,
                _ => false
            };

            Logger.Info($"Accessibility audit complete. Issues found: {report.TotalIssues}. Compliant: {report.IsCompliant}");

            return report;
        }

        /// <summary>
        /// Generate accessibility report
        /// </summary>
        public static string GenerateReport(AccessibilityReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Accessibility Report ===");
            sb.AppendLine($"Target Level: WCAG {report.TargetLevel}");
            sb.AppendLine($"Compliant: {(report.IsCompliant ? "✓ Yes" : "✗ No")}");
            sb.AppendLine($"Test Time: {report.TestTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Issues Summary:");
            sb.AppendLine($"  Total Issues: {report.TotalIssues}");
            sb.AppendLine($"  Critical: {report.CriticalIssues}");
            sb.AppendLine($"  Major: {report.MajorIssues}");
            sb.AppendLine($"  Minor: {report.MinorIssues}");

            if (report.Issues.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Issues:");
                foreach (var issue in report.Issues)
                {
                    sb.AppendLine($"  [{issue.Severity}] {issue.Code}: {issue.Description}");
                    sb.AppendLine($"    Recommendation: {issue.Recommendation}");
                }
            }

            return sb.ToString();
        }
    }
}
