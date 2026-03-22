using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Generates Selenium WebDriver test code in C#
    /// </summary>
    public class SeleniumCodeGenerator : ICodeGenerator
    {
        public string GetFrameworkName() => "Selenium WebDriver";
        public string GetLanguage() => "C#";
        public string GetFileExtension() => ".cs";

        public async Task<string> GenerateTestCodeAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var methodName = NormalizeMethodName(scenario.Name);

            code.AppendLine("using OpenQA.Selenium;");
            code.AppendLine("using OpenQA.Selenium.Chrome;");
            code.AppendLine("using NUnit.Framework;");
            code.AppendLine("using System;");
            code.AppendLine("using System.Threading.Tasks;");
            code.AppendLine();

            code.AppendLine("namespace AutomationFramework.Tests");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// Test: {scenario.Name}");
            code.AppendLine($"    /// Module: {scenario.Module}");
            if (!string.IsNullOrEmpty(scenario.Description))
                code.AppendLine($"    /// Description: {scenario.Description}");
            code.AppendLine("    /// </summary>");
            code.AppendLine("    [TestFixture]");
            code.AppendLine($"    public class {NormalizeClassName(scenario.Name)}Tests");
            code.AppendLine("    {");
            code.AppendLine("        private IWebDriver _driver;");
            code.AppendLine();

            // Setup
            code.AppendLine("        [SetUp]");
            code.AppendLine("        public void Setup()");
            code.AppendLine("        {");
            code.AppendLine("            _driver = new ChromeDriver();");
            code.AppendLine("            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);");
            code.AppendLine("        }");
            code.AppendLine();

            // Test method
            code.AppendLine("        [Test]");
            if (scenario.Tags.Count > 0)
            {
                code.AppendLine($"        [Category(\"{string.Join("\", \"", scenario.Tags)}\")]");
            }
            code.AppendLine($"        public void {methodName}()");
            code.AppendLine("        {");

            // Navigate
            if (!string.IsNullOrEmpty(scenario.StartUrl))
            {
                code.AppendLine($"            _driver.Navigate().GoToUrl(\"{scenario.StartUrl}\");");
                code.AppendLine();
            }

            // Generate step code
            var stepCode = GenerateSteps(scenario);
            code.Append(stepCode);

            code.AppendLine("        }");
            code.AppendLine();

            // Teardown
            code.AppendLine("        [TearDown]");
            code.AppendLine("        public void Teardown()");
            code.AppendLine("        {");
            code.AppendLine("            _driver?.Quit();");
            code.AppendLine("        }");
            code.AppendLine("    }");
            code.AppendLine("}");

            return await Task.FromResult(code.ToString());
        }

        public async Task<string> GeneratePageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine("using OpenQA.Selenium;");
            code.AppendLine("using System.Collections.Generic;");
            code.AppendLine();

            code.AppendLine("namespace AutomationFramework.Pages");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// Page Object for {scenario.Name}");
            code.AppendLine("    /// </summary>");
            code.AppendLine($"    public class {pageObjectName}");
            code.AppendLine("    {");
            code.AppendLine("        private IWebDriver _driver;");
            code.AppendLine();

            code.AppendLine($"        public {pageObjectName}(IWebDriver driver)");
            code.AppendLine("        {");
            code.AppendLine("            _driver = driver;");
            code.AppendLine("        }");
            code.AppendLine();

            // Generate locators
            var locators = ExtractLocators(scenario);
            int locatorIndex = 1;
            foreach (var locator in locators.Distinct())
            {
                var propertyName = $"Element{locatorIndex}";
                code.AppendLine($"        public IWebElement {propertyName} => _driver.FindElement(By.CssSelector(\"{locator}\"));");
                locatorIndex++;
            }

            code.AppendLine("    }");
            code.AppendLine("}");

            return await Task.FromResult(code.ToString());
        }

        public async Task<Dictionary<string, string>> GenerateProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            // Package.json replacement for NuGet
            var csprojContent = GenerateCsproj(scenarios);
            files["TestProject.csproj"] = csprojContent;

            // README
            files["README.md"] = GenerateReadme();

            // Configuration
            files["Configuration/Settings.json"] = GenerateSettingsJson();

            // Helpers
            files["Helpers/WebDriverHelper.cs"] = GenerateWebDriverHelper();

            // Base test class
            files["Base/BaseTest.cs"] = await GenerateBaseTestAsync();

            return files;
        }

        private string GenerateSteps(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();

            var allSteps = scenario.Steps.Count > 0 
                ? scenario.Steps 
                : ConvertActionsToSteps(scenario);

            foreach (var step in allSteps.OrderBy(s => s.Order))
            {
                if (step.StepType == "Action" && step.Action != null)
                {
                    code.Append(GenerateActionCode(step.Action));
                }
                else if (step.StepType == "Assertion" && step.Assertion != null)
                {
                    code.Append(GenerateAssertionCode(step.Assertion));
                }
            }

            return code.ToString();
        }

        private string GenerateActionCode(RecordedAction action)
        {
            var code = new System.Text.StringBuilder();
            var locatorCode = FormatLocator(action.Locator);

            switch (action.ActionType?.ToLower())
            {
                case "click":
                    code.AppendLine($"            _driver.FindElement({locatorCode}).Click();");
                    break;
                case "type":
                case "input":
                    code.AppendLine($"            var element = _driver.FindElement({locatorCode});");
                    code.AppendLine($"            element.Clear();");
                    code.AppendLine($"            element.SendKeys(\"{EscapeString(action.Value)}\");");
                    break;
                case "select":
                    code.AppendLine($"            var selectElement = new SelectElement(_driver.FindElement({locatorCode}));");
                    code.AppendLine($"            selectElement.SelectByValue(\"{EscapeString(action.Value)}\");");
                    break;
                case "hover":
                    code.AppendLine($"            var actions = new Actions(_driver);");
                    code.AppendLine($"            actions.MoveToElement(_driver.FindElement({locatorCode})).Perform();");
                    break;
                case "check":
                    code.AppendLine($"            var element = _driver.FindElement({locatorCode});");
                    code.AppendLine($"            if (!element.Selected) element.Click();");
                    break;
                case "uncheck":
                    code.AppendLine($"            var element = _driver.FindElement({locatorCode});");
                    code.AppendLine($"            if (element.Selected) element.Click();");
                    break;
                case "navigate":
                case "goto":
                    code.AppendLine($"            _driver.Navigate().GoToUrl(\"{action.Value}\");");
                    break;
                default:
                    code.AppendLine($"            // TODO: Implement action {action.ActionType}");
                    break;
            }

            code.AppendLine();
            return code.ToString();
        }

        private string GenerateAssertionCode(Assertion assertion)
        {
            var code = new System.Text.StringBuilder();
            var locatorCode = FormatLocator(assertion.Locator);

            switch (assertion.Type?.ToLower())
            {
                case "elementvisible":
                case "visible":
                    code.AppendLine($"            Assert.That(_driver.FindElement({locatorCode}).Displayed, Is.True, \"Element should be visible\");");
                    break;
                case "elementexists":
                case "exists":
                    code.AppendLine($"            Assert.That(_driver.FindElements({locatorCode}).Count, Is.GreaterThan(0), \"Element should exist\");");
                    break;
                case "textequals":
                case "textcontains":
                    code.AppendLine($"            var text = _driver.FindElement({locatorCode}).Text;");
                    code.AppendLine($"            Assert.That(text, Does.Contain(\"{EscapeString(assertion.ExpectedValue)}\"), \"Text should contain expected value\");");
                    break;
                case "urlcontains":
                    code.AppendLine($"            Assert.That(_driver.Url, Does.Contain(\"{assertion.ExpectedValue}\"), \"URL should contain expected value\");");
                    break;
                case "titleequals":
                    code.AppendLine($"            Assert.That(_driver.Title, Is.EqualTo(\"{assertion.ExpectedValue}\"), \"Page title should match\");");
                    break;
                default:
                    code.AppendLine($"            // TODO: Implement assertion {assertion.Type}");
                    break;
            }

            code.AppendLine();
            return code.ToString();
        }

        private string GenerateCsproj(List<TestScenario> scenarios)
        {
            return @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Selenium.WebDriver"" Version=""4.15.1"" />
    <PackageReference Include=""Selenium.WebDriver.ChromeDriver"" Version=""121.0.6167.160"" />
    <PackageReference Include=""NUnit"" Version=""4.1.0"" />
    <PackageReference Include=""NUnit3TestAdapter"" Version=""4.5.0"" />
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.8.2"" />
  </ItemGroup>

</Project>";
        }

        private string GenerateReadme()
        {
            return @"# Selenium WebDriver Test Project

Generated test automation project using Selenium WebDriver in C#.

## Prerequisites

- .NET 9.0 SDK or later
- Chrome browser
- ChromeDriver (automatically managed by WebDriver Manager in newer versions)

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter TestClassName

# Run with detailed output
dotnet test -v detailed
```

## Project Structure

- `Tests/` - Test classes
- `Pages/` - Page Object Model classes
- `Helpers/` - Utility helpers
- `Configuration/` - Test configuration

## Best Practices

1. Use Page Object Model for maintainability
2. Keep locators in page objects
3. Use descriptive test names
4. Add appropriate waits for dynamic elements
5. Use relative XPath/CSS selectors when possible
";
        }

        private string GenerateSettingsJson()
        {
            return @"{
  ""browser"": ""chrome"",
  ""headless"": false,
  ""implicitWaitSeconds"": 10,
  ""explicitWaitSeconds"": 30,
  ""pageLoadTimeoutSeconds"": 30,
  ""baseUrl"": ""http://localhost:3000"",
  ""reportPath"": ""./TestResults""
}";
        }

        private string GenerateWebDriverHelper()
        {
            return @"using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AutomationFramework.Helpers
{
    public static class WebDriverHelper
    {
        public static IWebDriver CreateChromeDriver(bool headless = false)
        {
            var options = new ChromeOptions();
            options.AddArgument(""--start-maximized"");
            
            if (headless)
                options.AddArgument(""--headless"");

            return new ChromeDriver(options);
        }

        public static void WaitForElement(IWebDriver driver, By locator, int timeoutSeconds = 10)
        {
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(driver => driver.FindElement(locator));
        }

        public static void ScrollToElement(IWebDriver driver, IWebElement element)
        {
            var executor = (IJavaScriptExecutor)driver;
            executor.ExecuteScript(""arguments[0].scrollIntoView(true);"", element);
        }
    }
}";
        }

        private async Task<string> GenerateBaseTestAsync()
        {
            return await Task.FromResult(@"using NUnit.Framework;
using OpenQA.Selenium;
using System;

namespace AutomationFramework.Base
{
    [TestFixture]
    public abstract class BaseTest
    {
        protected IWebDriver Driver;

        [SetUp]
        public virtual void Setup()
        {
            Driver = Helpers.WebDriverHelper.CreateChromeDriver();
        }

        [TearDown]
        public virtual void Teardown()
        {
            Driver?.Quit();
        }
    }
}");
        }

        private List<TestStep> ConvertActionsToSteps(TestScenario scenario)
        {
            var steps = new List<TestStep>();
            int order = 0;

            foreach (var action in scenario.Actions)
            {
                steps.Add(new TestStep
                {
                    Order = order++,
                    StepType = "Action",
                    Action = action
                });
            }

            foreach (var assertion in scenario.Assertions)
            {
                steps.Add(new TestStep
                {
                    Order = order++,
                    StepType = "Assertion",
                    Assertion = assertion
                });
            }

            return steps;
        }

        private List<string> ExtractLocators(TestScenario scenario)
        {
            var locators = new List<string>();
            
            var allSteps = scenario.Steps.Count > 0 
                ? scenario.Steps 
                : ConvertActionsToSteps(scenario);

            foreach (var step in allSteps)
            {
                if (step.Action?.Locator != null)
                    locators.Add(step.Action.Locator);
                if (step.Assertion?.Locator != null)
                    locators.Add(step.Assertion.Locator);
            }

            return locators;
        }

        private string NormalizeMethodName(string name)
        {
            var normalized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9]", "");
            return char.ToUpper(normalized[0]) + normalized.Substring(1) + "Test";
        }

        private string NormalizeClassName(string name)
        {
            var parts = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()));
        }

        private string FormatLocator(string locator)
        {
            if (string.IsNullOrEmpty(locator)) return "By.Id(\"\")";

            return locator switch
            {
                var l when l.StartsWith("#") => $"By.Id(\"{EscapeString(l.Substring(1))}\")",
                var l when l.StartsWith(".") => $"By.ClassName(\"{EscapeString(l.Substring(1))}\")",
                var l when l.StartsWith("//") => $"By.XPath(\"{EscapeString(l)}\")",
                _ => $"By.CssSelector(\"{EscapeString(locator)}\")"
            };
        }

        private string EscapeString(string? value)
        {
            return value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }
    }
}
