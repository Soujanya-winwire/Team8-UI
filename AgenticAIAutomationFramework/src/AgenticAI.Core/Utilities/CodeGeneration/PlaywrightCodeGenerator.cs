using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Generates Playwright test code in C#, Python, or TypeScript/JavaScript
    /// </summary>
    public class PlaywrightCodeGenerator : ICodeGenerator
    {
        private ExportLanguage _language;

        public PlaywrightCodeGenerator(ExportLanguage language = ExportLanguage.CSharp)
        {
            _language = language;
        }

        public string GetFrameworkName() => "Playwright";
        public string GetLanguage() => _language.ToString();
        public string GetFileExtension() => _language switch
        {
            ExportLanguage.Python => ".py",
            ExportLanguage.JavaScript => ".js",
            ExportLanguage.TypeScript => ".spec.ts",
            _ => ".cs"
        };

        public async Task<string> GenerateTestCodeAsync(TestScenario scenario)
        {
            return _language switch
            {
                ExportLanguage.Python => await GeneratePythonTestAsync(scenario),
                ExportLanguage.JavaScript => await GenerateJavaScriptTestAsync(scenario),
                ExportLanguage.TypeScript => await GenerateTypeScriptTestAsync(scenario),
                _ => await GenerateCSharpTestAsync(scenario)
            };
        }

        public async Task<string> GeneratePageObjectAsync(TestScenario scenario)
        {
            return _language switch
            {
                ExportLanguage.Python => await GeneratePythonPageObjectAsync(scenario),
                ExportLanguage.JavaScript => await GenerateJavaScriptPageObjectAsync(scenario),
                ExportLanguage.TypeScript => await GenerateTypeScriptPageObjectAsync(scenario),
                _ => await GenerateCSharpPageObjectAsync(scenario)
            };
        }

        public async Task<Dictionary<string, string>> GenerateProjectFilesAsync(List<TestScenario> scenarios)
        {
            return _language switch
            {
                ExportLanguage.Python => await GeneratePythonProjectFilesAsync(scenarios),
                ExportLanguage.JavaScript => await GenerateJavaScriptProjectFilesAsync(scenarios),
                ExportLanguage.TypeScript => await GenerateTypeScriptProjectFilesAsync(scenarios),
                _ => await GenerateCSharpProjectFilesAsync(scenarios)
            };
        }

        #region C# Implementation

        private async Task<string> GenerateCSharpTestAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var methodName = NormalizeMethodName(scenario.Name);

            code.AppendLine("using Microsoft.Playwright;");
            code.AppendLine("using NUnit.Framework;");
            code.AppendLine("using System;");
            code.AppendLine("using System.Threading.Tasks;");
            code.AppendLine();

            code.AppendLine("namespace AutomationFramework.Tests");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// Test: {scenario.Name}");
            code.AppendLine($"    /// Module: {scenario.Module}");
            code.AppendLine("    /// </summary>");
            code.AppendLine("    [TestFixture]");
            code.AppendLine($"    public class {NormalizeClassName(scenario.Name)}Tests");
            code.AppendLine("    {");
            code.AppendLine("        private IPlaywright _playwright;");
            code.AppendLine("        private IBrowser _browser;");
            code.AppendLine("        private IPage _page;");
            code.AppendLine();

            code.AppendLine("        [OneTimeSetUp]");
            code.AppendLine("        public async Task SetUpBrowser()");
            code.AppendLine("        {");
            code.AppendLine("            _playwright = await Playwright.CreateAsync();");
            code.AppendLine("            _browser = await _playwright.Chromium.LaunchAsync();");
            code.AppendLine("        }");
            code.AppendLine();

            code.AppendLine("        [SetUp]");
            code.AppendLine("        public async Task SetUp()");
            code.AppendLine("        {");
            code.AppendLine("            _page = await _browser.NewPageAsync();");
            code.AppendLine("        }");
            code.AppendLine();

            code.AppendLine("        [Test]");
            if (scenario.Tags.Count > 0)
                code.AppendLine($"        [Category(\"{string.Join("\", \"", scenario.Tags)}\")]");
            code.AppendLine($"        public async Task {methodName}()");
            code.AppendLine("        {");

            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"            await _page.GotoAsync(\"{scenario.StartUrl}\");");

            code.Append(await GenerateCSharpStepsAsync(scenario));
            code.AppendLine("        }");
            code.AppendLine();

            code.AppendLine("        [TearDown]");
            code.AppendLine("        public async Task TearDown()");
            code.AppendLine("        {");
            code.AppendLine("            await _page.CloseAsync();");
            code.AppendLine("        }");
            code.AppendLine();

            code.AppendLine("        [OneTimeTearDown]");
            code.AppendLine("        public async Task TearDownBrowser()");
            code.AppendLine("        {");
            code.AppendLine("            await _browser.CloseAsync();");
            code.AppendLine("            _playwright?.Dispose();");
            code.AppendLine("        }");
            code.AppendLine("    }");
            code.AppendLine("}");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateCSharpPageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine("using Microsoft.Playwright;");
            code.AppendLine("using System.Collections.Generic;");
            code.AppendLine();

            code.AppendLine("namespace AutomationFramework.Pages");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// Page Object for {scenario.Name}");
            code.AppendLine("    /// </summary>");
            code.AppendLine($"    public class {pageObjectName}");
            code.AppendLine("    {");
            code.AppendLine("        private IPage _page;");
            code.AppendLine();

            code.AppendLine($"        public {pageObjectName}(IPage page)");
            code.AppendLine("        {");
            code.AppendLine("            _page = page;");
            code.AppendLine("        }");
            code.AppendLine();

            var locators = ExtractLocators(scenario);
            int locatorIndex = 1;
            foreach (var locator in locators.Distinct())
            {
                var propertyName = $"Element{locatorIndex}";
                code.AppendLine($"        public ILocator {propertyName} => _page.Locator(\"{EscapeString(locator)}\");");
                locatorIndex++;
            }

            code.AppendLine("    }");
            code.AppendLine("}");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateCSharpStepsAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var allSteps = scenario.Steps.Count > 0
                ? scenario.Steps
                : ConvertActionsToSteps(scenario);

            foreach (var step in allSteps.OrderBy(s => s.Order))
            {
                if (step.StepType == "Action" && step.Action != null)
                    code.Append(GenerateCSharpActionCode(step.Action));
                else if (step.StepType == "Assertion" && step.Assertion != null)
                    code.Append(await GenerateCSharpAssertionCodeAsync(step.Assertion));
            }

            return await Task.FromResult(code.ToString());
        }

        private string GenerateCSharpActionCode(RecordedAction action)
        {
            var code = new System.Text.StringBuilder();

            switch (action.ActionType?.ToLower())
            {
                case "click":
                    code.AppendLine($"            await _page.ClickAsync(\"{EscapeString(action.Locator)}\");");
                    break;
                case "type":
                case "input":
                    code.AppendLine($"            await _page.FillAsync(\"{EscapeString(action.Locator)}\", \"{EscapeString(action.Value)}\");");
                    break;
                case "select":
                    code.AppendLine($"            await _page.SelectOptionAsync(\"{EscapeString(action.Locator)}\", \"{EscapeString(action.Value)}\");");
                    break;
                case "hover":
                    code.AppendLine($"            await _page.HoverAsync(\"{EscapeString(action.Locator)}\");");
                    break;
                case "check":
                    code.AppendLine($"            await _page.CheckAsync(\"{EscapeString(action.Locator)}\");");
                    break;
                case "uncheck":
                    code.AppendLine($"            await _page.UncheckAsync(\"{EscapeString(action.Locator)}\");");
                    break;
                case "navigate":
                case "goto":
                    code.AppendLine($"            await _page.GotoAsync(\"{EscapeString(action.Value)}\");");
                    break;
                default:
                    code.AppendLine($"            // TODO: Implement action {action.ActionType}");
                    break;
            }

            code.AppendLine();
            return code.ToString();
        }

        private async Task<string> GenerateCSharpAssertionCodeAsync(Assertion assertion)
        {
            var code = new System.Text.StringBuilder();

            switch (assertion.Type?.ToLower())
            {
                case "elementvisible":
                case "visible":
                    code.AppendLine($"            await Expect(_page.Locator(\"{EscapeString(assertion.Locator)}\")).ToBeVisibleAsync();");
                    break;
                case "elementexists":
                case "exists":
                    code.AppendLine($"            await Expect(_page.Locator(\"{EscapeString(assertion.Locator)}\")).ToHaveCountAsync(1);");
                    break;
                case "textequals":
                case "textcontains":
                    code.AppendLine($"            await Expect(_page.Locator(\"{EscapeString(assertion.Locator)}\")).ToContainTextAsync(\"{EscapeString(assertion.ExpectedValue)}\");");
                    break;
                case "urlcontains":
                    code.AppendLine($"            await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(\"{EscapeString(assertion.ExpectedValue)}\"));");
                    break;
                case "titleequals":
                    code.AppendLine($"            await Expect(_page).ToHaveTitleAsync(\"{EscapeString(assertion.ExpectedValue)}\");");
                    break;
                default:
                    code.AppendLine($"            // TODO: Implement assertion {assertion.Type}");
                    break;
            }

            code.AppendLine();
            return await Task.FromResult(code.ToString());
        }

        private async Task<Dictionary<string, string>> GenerateCSharpProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            files["TestProject.csproj"] = GenerateCSharpCsproj();
            files["README.md"] = GenerateCSharpReadme();
            files["Configuration/Settings.json"] = GeneratePlaywrightSettingsJson();
            files["Helpers/BrowserHelper.cs"] = GenerateCSharpBrowserHelper();

            return files;
        }

        private string GenerateCSharpCsproj()
        {
            return @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.Playwright"" Version=""1.40.0"" />
    <PackageReference Include=""NUnit"" Version=""4.1.0"" />
    <PackageReference Include=""NUnit3TestAdapter"" Version=""4.5.0"" />
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.8.2"" />
  </ItemGroup>

</Project>";
        }

        private string GenerateCSharpReadme()
        {
            return @"# Playwright Test Project (C#)

Modern web automation testing with Playwright in C#.

## Prerequisites

- .NET 9.0 SDK or later
- Run playwright install for browser dependencies

## Installation

```bash
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Playwright.NUnit
playwright install
```

## Running Tests

```bash
dotnet test
```

## Project Structure

- `Tests/` - Test classes
- `Pages/` - Page Object Models
- `Helpers/` - Utility helpers
- `Configuration/` - Test config
";
        }

        private string GenerateCSharpBrowserHelper()
        {
            return @"using Microsoft.Playwright;

namespace AutomationFramework.Helpers
{
    public static class BrowserHelper
    {
        public static async Task<IBrowserContext> CreateContextAsync(IBrowser browser)
        {
            return await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });
        }

        public static async Task WaitForNavigationAsync(IPage page, Func<Task> action)
        {
            var navigationTask = page.WaitForNavigationAsync();
            await action();
            await navigationTask;
        }
    }
}";
        }

        #endregion

        #region TypeScript/JavaScript Implementation

        private async Task<string> GenerateTypeScriptTestAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var fileName = NormalizeFileName(scenario.Name);

            code.AppendLine("import { test, expect } from '@playwright/test';");
            code.AppendLine();

            code.AppendLine($"test.describe('{scenario.Name}', () => {{");
            code.AppendLine();

            code.AppendLine($"  test('{scenario.Name}', async ({{ page }}) => {{");

            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"    await page.goto('{scenario.StartUrl}');");

            code.Append(await GenerateTypeScriptStepsAsync(scenario));
            code.AppendLine("  });");
            code.AppendLine("});");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateTypeScriptPageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine("import { Page, Locator } from '@playwright/test';");
            code.AppendLine();

            code.AppendLine($"export class {pageObjectName} {{");
            code.AppendLine("  readonly page: Page;");
            code.AppendLine();

            var locators = ExtractLocators(scenario);
            foreach (var locator in locators.Distinct())
            {
                var propertyName = $"element{locators.IndexOf(locator) + 1}";
                code.AppendLine($"  readonly {propertyName}: Locator;");
            }

            code.AppendLine();
            code.AppendLine($"  constructor(page: Page) {{");
            code.AppendLine("    this.page = page;");

            int index = 1;
            foreach (var locator in locators.Distinct())
            {
                code.AppendLine($"    this.element{index} = page.locator('{locator}');");
                index++;
            }

            code.AppendLine("  }");
            code.AppendLine("}");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateTypeScriptStepsAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var allSteps = scenario.Steps.Count > 0
                ? scenario.Steps
                : ConvertActionsToSteps(scenario);

            foreach (var step in allSteps.OrderBy(s => s.Order))
            {
                if (step.StepType == "Action" && step.Action != null)
                    code.Append(GenerateTypeScriptActionCode(step.Action));
                else if (step.StepType == "Assertion" && step.Assertion != null)
                    code.Append(GenerateTypeScriptAssertionCode(step.Assertion));
            }

            return await Task.FromResult(code.ToString());
        }

        private string GenerateTypeScriptActionCode(RecordedAction action)
        {
            var code = new System.Text.StringBuilder();

            switch (action.ActionType?.ToLower())
            {
                case "click":
                    code.AppendLine($"    await page.click('{action.Locator}');");
                    break;
                case "type":
                case "input":
                    code.AppendLine($"    await page.fill('{action.Locator}', '{EscapeString(action.Value)}');");
                    break;
                case "select":
                    code.AppendLine($"    await page.selectOption('{action.Locator}', '{action.Value}');");
                    break;
                case "hover":
                    code.AppendLine($"    await page.hover('{action.Locator}');");
                    break;
                case "check":
                    code.AppendLine($"    await page.check('{action.Locator}');");
                    break;
                case "uncheck":
                    code.AppendLine($"    await page.uncheck('{action.Locator}');");
                    break;
                case "navigate":
                case "goto":
                    code.AppendLine($"    await page.goto('{action.Value}');");
                    break;
            }

            code.AppendLine();
            return code.ToString();
        }

        private string GenerateTypeScriptAssertionCode(Assertion assertion)
        {
            var code = new System.Text.StringBuilder();

            switch (assertion.Type?.ToLower())
            {
                case "elementvisible":
                case "visible":
                    code.AppendLine($"    await expect(page.locator('{assertion.Locator}')).toBeVisible();");
                    break;
                case "elementexists":
                case "exists":
                    code.AppendLine($"    await expect(page.locator('{assertion.Locator}')).toHaveCount(1);");
                    break;
                case "textequals":
                case "textcontains":
                    code.AppendLine($"    await expect(page.locator('{assertion.Locator}')).toContainText('{assertion.ExpectedValue}');");
                    break;
                case "urlcontains":
                    code.AppendLine($"    await expect(page).toHaveURL(/{assertion.ExpectedValue}/);");
                    break;
                case "titleequals":
                    code.AppendLine($"    await expect(page).toHaveTitle('{assertion.ExpectedValue}');");
                    break;
            }

            code.AppendLine();
            return code.ToString();
        }

        private async Task<string> GenerateJavaScriptTestAsync(TestScenario scenario)
        {
            // Generate as JavaScript (non-typed version)
            var tsCode = await GenerateTypeScriptTestAsync(scenario);
            return tsCode.Replace(": Page", "").Replace(": string", "");
        }

        private async Task<string> GenerateJavaScriptPageObjectAsync(TestScenario scenario)
        {
            var tsCode = await GenerateTypeScriptPageObjectAsync(scenario);
            return tsCode.Replace("import { Page, Locator } from '@playwright/test';", "")
                        .Replace(": Page", "")
                        .Replace("readonly page: Page;", "")
                        .Replace(": Locator", "");
        }

        private async Task<Dictionary<string, string>> GenerateTypeScriptProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            files["package.json"] = GeneratePackageJson();
            files["playwright.config.ts"] = GeneratePlaywrightConfigTs();
            files["README.md"] = GenerateTypeScriptReadme();
            files["tsconfig.json"] = GenerateTsConfig();

            return files;
        }

        private async Task<Dictionary<string, string>> GenerateJavaScriptProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            files["package.json"] = GeneratePackageJson();
            files["playwright.config.js"] = GeneratePlaywrightConfigJs();
            files["README.md"] = GenerateJavaScriptReadme();

            return files;
        }

        private string GeneratePackageJson()
        {
            return @"{
  ""name"": ""playwright-automation"",
  ""version"": ""1.0.0"",
  ""description"": ""Playwright test automation project"",
  ""scripts"": {
    ""test"": ""playwright test"",
    ""test:ui"": ""playwright test --ui"",
    ""test:debug"": ""playwright test --debug""
  },
  ""dependencies"": {
    ""@playwright/test"": ""^1.40.0""
  },
  ""devDependencies"": {
    ""@types/node"": ""^20.0.0"",
    ""typescript"": ""^5.0.0""
  }
}";
        }

        private string GeneratePlaywrightConfigTs()
        {
            return @"import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
});";
        }

        private string GeneratePlaywrightConfigJs()
        {
            return @"module.exports = {
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...require('@playwright/test').devices['Desktop Chrome'] },
    },
  ],
};";
        }

        private string GenerateTsConfig()
        {
            return @"{
  ""compilerOptions"": {
    ""target"": ""ES2020"",
    ""useDefineForClassFields"": true,
    ""lib"": [""ES2020"", ""DOM"", ""DOM.Iterable""],
    ""module"": ""ESNext"",
    ""skipLibCheck"": true,
    ""esModuleInterop"": true,
    ""allowSyntheticDefaultImports"": true,
    ""strict"": true,
    ""resolveJsonModule"": true
  },
  ""include"": [""tests/**/*.ts""],
  ""exclude"": [""node_modules""]
}";
        }

        private string GenerateTypeScriptReadme()
        {
            return @"# Playwright Test Automation (TypeScript)

Browser automation testing with Playwright using TypeScript.

## Installation

```bash
npm install
npx playwright install
```

## Running Tests

```bash
# Run all tests
npm test

# Run with UI mode
npm run test:ui

# Run in debug mode
npm run test:debug
```

## Project Structure

- `tests/` - Test files
- `pages/` - Page Object Models
- `tests/fixtures/` - Test fixtures
- `playwright.config.ts` - Playwright configuration

## Features

- Cross-browser testing (Chrome, Firefox, Safari)
- Parallel test execution
- HTML reporting
- Video recording on failure
- Trace recording
";
        }

        private string GenerateJavaScriptReadme()
        {
            return @"# Playwright Test Automation (JavaScript)

Browser automation testing with Playwright using JavaScript.

## Installation

```bash
npm install
npx playwright install
```

## Running Tests

```bash
npm test
```

## Features

- Cross-browser testing
- Parallel execution
- HTML reporting
";
        }

        private async Task<string> GenerateJavaScriptStepsAsync(TestScenario scenario)
        {
            return await GenerateTypeScriptStepsAsync(scenario);
        }

        private async Task<string> GenerateJavaScriptAssertionCodeAsync(Assertion assertion)
        {
            return GenerateTypeScriptAssertionCode(assertion);
        }

        #endregion

        #region Python Implementation

        private async Task<string> GeneratePythonTestAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            code.AppendLine("import asyncio");
            code.AppendLine("import pytest");
            code.AppendLine("from playwright.async_api import async_playwright");
            code.AppendLine();

            code.AppendLine($"@pytest.mark.asyncio");
            code.AppendLine($"async def test_{NormalizeMethodName(scenario.Name).ToLower()}():");
            code.AppendLine("    async with async_playwright() as p:");
            code.AppendLine("        browser = await p.chromium.launch()");
            code.AppendLine("        page = await browser.new_page()");
            code.AppendLine();

            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"        await page.goto('{scenario.StartUrl}')");

            code.Append(await GeneratePythonStepsAsync(scenario));

            code.AppendLine("        await browser.close()");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GeneratePythonPageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine("from playwright.async_api import Page");
            code.AppendLine();

            code.AppendLine($"class {pageObjectName}:");
            code.AppendLine("    def __init__(self, page: Page):");
            code.AppendLine("        self.page = page");
            code.AppendLine();

            var locators = ExtractLocators(scenario);
            int index = 1;
            foreach (var locator in locators.Distinct())
            {
                code.AppendLine($"    @property");
                code.AppendLine($"    def element_{index}(self):");
                code.AppendLine($"        return self.page.locator('{locator}')");
                code.AppendLine();
                index++;
            }

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GeneratePythonStepsAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var allSteps = scenario.Steps.Count > 0
                ? scenario.Steps
                : ConvertActionsToSteps(scenario);

            foreach (var step in allSteps.OrderBy(s => s.Order))
            {
                if (step.StepType == "Action" && step.Action != null)
                    code.Append("        " + GeneratePythonActionCode(step.Action));
                else if (step.StepType == "Assertion" && step.Assertion != null)
                    code.Append("        " + await GeneratePythonAssertionCodeAsync(step.Assertion));
            }

            return await Task.FromResult(code.ToString());
        }

        private string GeneratePythonActionCode(RecordedAction action)
        {
            return action.ActionType?.ToLower() switch
            {
                "click" => $"await page.click('{action.Locator}')\n",
                "type" or "input" => $"await page.fill('{action.Locator}', '{EscapeString(action.Value)}')\n",
                "select" => $"await page.select_option('{action.Locator}', '{action.Value}')\n",
                "hover" => $"await page.hover('{action.Locator}')\n",
                "check" => $"await page.check('{action.Locator}')\n",
                "uncheck" => $"await page.uncheck('{action.Locator}')\n",
                "navigate" or "goto" => $"await page.goto('{action.Value}')\n",
                _ => $"# TODO: Implement action {action.ActionType}\n"
            };
        }

        private async Task<string> GeneratePythonAssertionCodeAsync(Assertion assertion)
        {
            var code = assertion.Type?.ToLower() switch
            {
                "elementvisible" or "visible" => $"assert await page.locator('{assertion.Locator}').is_visible()\n",
                "elementexists" or "exists" => $"assert await page.locator('{assertion.Locator}').count() == 1\n",
                "textequals" or "textcontains" => $"assert '{assertion.ExpectedValue}' in await page.locator('{assertion.Locator}').text_content()\n",
                "urlcontains" => $"assert '{assertion.ExpectedValue}' in page.url\n",
                "titleequals" => $"assert page.title == '{assertion.ExpectedValue}'\n",
                _ => $"# TODO: Implement assertion {assertion.Type}\n"
            };

            return await Task.FromResult(code);
        }

        private async Task<Dictionary<string, string>> GeneratePythonProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            files["requirements.txt"] = "playwright==1.40.0\npytest==7.4.3\npytest-asyncio==0.21.1";
            files["pytest.ini"] = GeneratePytestIni();
            files["README.md"] = GeneratePythonReadme();
            files[".gitignore"] = "*.pyc\n__pycache__/\n.pytest_cache/\nplayer_artifacts/";

            return files;
        }

        private string GeneratePytestIni()
        {
            return @"[pytest]
asyncio_mode = auto
testpaths = tests
python_files = test_*.py
python_classes = Test*
python_functions = test_*
addopts = -v --tb=short";
        }

        private string GeneratePythonReadme()
        {
            return @"# Playwright Test Automation (Python)

Browser automation testing with Playwright using Python.

## Installation

```bash
pip install -r requirements.txt
playwright install
```

## Running Tests

```bash
pytest
```

## Features

- Async/await support for fast execution
- Pytest integration
- Cross-browser testing
- Easy to read and maintain
";
        }

        #endregion

        #region Helper Methods

        private string GeneratePlaywrightSettingsJson()
        {
            return @"{
  ""browser"": ""chromium"",
  ""headless"": false,
  ""slowMo"": 0,
  ""timeout"": 30000,
  ""navigationTimeout"": 30000,
  ""baseUrl"": ""http://localhost:3000"",
  ""reportPath"": ""./test-results""
}";
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
            return char.ToLower(normalized[0]) + normalized.Substring(1);
        }

        private string NormalizeClassName(string name)
        {
            var parts = name.Split(new[] { ' ', '_', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()));
        }

        private string NormalizeFileName(string name)
        {
            return System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_").ToLower();
        }

        private string EscapeString(string? value)
        {
            return value?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"") ?? "";
        }

        #endregion
    }
}
