using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities.CodeGeneration
{
    /// <summary>
    /// Generates Cypress test code in JavaScript or TypeScript
    /// </summary>
    public class CypressCodeGenerator : ICodeGenerator
    {
        private ExportLanguage _language;

        public CypressCodeGenerator(ExportLanguage language = ExportLanguage.JavaScript)
        {
            _language = language;
        }

        public string GetFrameworkName() => "Cypress";
        public string GetLanguage() => _language.ToString();
        public string GetFileExtension() => _language == ExportLanguage.TypeScript ? ".cy.ts" : ".cy.js";

        public async Task<string> GenerateTestCodeAsync(TestScenario scenario)
        {
            return _language == ExportLanguage.TypeScript
                ? await GenerateTypeScriptTestAsync(scenario)
                : await GenerateJavaScriptTestAsync(scenario);
        }

        public async Task<string> GeneratePageObjectAsync(TestScenario scenario)
        {
            return _language == ExportLanguage.TypeScript
                ? await GenerateTypeScriptPageObjectAsync(scenario)
                : await GenerateJavaScriptPageObjectAsync(scenario);
        }

        public async Task<Dictionary<string, string>> GenerateProjectFilesAsync(List<TestScenario> scenarios)
        {
            var files = new Dictionary<string, string>();

            files["package.json"] = GeneratePackageJson();
            files["cypress.config.js"] = GenerateCypressConfig();
            files["README.md"] = GenerateReadme();
            files[".gitignore"] = GenerateGitignore();
            files["cypress/support/e2e.js"] = GenerateSupport();

            return files;
        }

        #region TypeScript Implementation

        private async Task<string> GenerateTypeScriptTestAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();

            code.AppendLine("describe('" + scenario.Name + "', () => {");
            code.AppendLine("  beforeEach(() => {");
            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"    cy.visit('{scenario.StartUrl}');");
            code.AppendLine("  });");
            code.AppendLine();

            code.AppendLine($"  it('{scenario.Name}', () => {{");
            code.Append(await GenerateTypeScriptStepsAsync(scenario));
            code.AppendLine("  });");
            code.AppendLine("});");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateTypeScriptPageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine($"export class {pageObjectName} {{");
            code.AppendLine();

            var locators = ExtractLocators(scenario);
            var locatorIndex = 1;
            foreach (var locator in locators.Distinct())
            {
                code.AppendLine($"  get element{locatorIndex}() {{");
                code.AppendLine($"    return cy.get('{locator}');");
                code.AppendLine("  }");
                code.AppendLine();
                locatorIndex++;
            }

            code.AppendLine("  visit() {");
            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"    cy.visit('{scenario.StartUrl}');");
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
            return action.ActionType?.ToLower() switch
            {
                "click" => $"    cy.get('{action.Locator}').click();\n",
                "type" or "input" => $"    cy.get('{action.Locator}').clear().type('{EscapeString(action.Value)}');\n",
                "select" => $"    cy.get('{action.Locator}').select('{action.Value}');\n",
                "hover" => $"    cy.get('{action.Locator}').trigger('mouseover');\n",
                "check" => $"    cy.get('{action.Locator}').check();\n",
                "uncheck" => $"    cy.get('{action.Locator}').uncheck();\n",
                "navigate" or "goto" => $"    cy.visit('{action.Value}');\n",
                "doubleclick" => $"    cy.get('{action.Locator}').dblclick();\n",
                "rightclick" => $"    cy.get('{action.Locator}').rightclick();\n",
                _ => $"    // TODO: Implement action {action.ActionType}\n"
            };
        }

        private string GenerateTypeScriptAssertionCode(Assertion assertion)
        {
            return assertion.Type?.ToLower() switch
            {
                "elementvisible" or "visible" => $"    cy.get('{assertion.Locator}').should('be.visible');\n",
                "elementexists" or "exists" => $"    cy.get('{assertion.Locator}').should('exist');\n",
                "textequals" => $"    cy.get('{assertion.Locator}').should('have.text', '{assertion.ExpectedValue}');\n",
                "textcontains" => $"    cy.get('{assertion.Locator}').should('contain', '{assertion.ExpectedValue}');\n",
                "urlcontains" => $"    cy.url().should('include', '{assertion.ExpectedValue}');\n",
                "titleequals" => $"    cy.title().should('eq', '{assertion.ExpectedValue}');\n",
                "elementdisabled" => $"    cy.get('{assertion.Locator}').should('be.disabled');\n",
                "elementenabled" => $"    cy.get('{assertion.Locator}').should('not.be.disabled');\n",
                _ => $"    // TODO: Implement assertion {assertion.Type}\n"
            };
        }

        #endregion

        #region JavaScript Implementation

        private async Task<string> GenerateJavaScriptTestAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();

            code.AppendLine("describe('" + scenario.Name + "', () => {");
            code.AppendLine("  beforeEach(() => {");
            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"    cy.visit('{scenario.StartUrl}');");
            code.AppendLine("  });");
            code.AppendLine();

            code.AppendLine($"  it('{scenario.Name}', () => {{");
            code.Append(await GenerateJavaScriptStepsAsync(scenario));
            code.AppendLine("  });");
            code.AppendLine("});");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateJavaScriptPageObjectAsync(TestScenario scenario)
        {
            var code = new System.Text.StringBuilder();
            var pageObjectName = NormalizeClassName(scenario.Name) + "Page";

            code.AppendLine($"class {pageObjectName} {{");
            code.AppendLine();

            var locators = ExtractLocators(scenario);
            var locatorIndex = 1;
            foreach (var locator in locators.Distinct())
            {
                code.AppendLine($"  get element{locatorIndex}() {{");
                code.AppendLine($"    return cy.get('{locator}');");
                code.AppendLine("  }");
                code.AppendLine();
                locatorIndex++;
            }

            code.AppendLine("  visit() {");
            if (!string.IsNullOrEmpty(scenario.StartUrl))
                code.AppendLine($"    cy.visit('{scenario.StartUrl}');");
            code.AppendLine("  }");
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine($"export default new {pageObjectName}();");

            return await Task.FromResult(code.ToString());
        }

        private async Task<string> GenerateJavaScriptStepsAsync(TestScenario scenario)
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

        #endregion

        #region Project Files

        private string GeneratePackageJson()
        {
            return @"{
  ""name"": ""cypress-automation"",
  ""version"": ""1.0.0"",
  ""description"": ""Cypress test automation project"",
  ""scripts"": {
    ""test"": ""cypress run"",
    ""test:open"": ""cypress open"",
    ""test:headless"": ""cypress run --headless""
  },
  ""keywords"": [""cypress"", ""automation"", ""testing""],
  ""author"": ""Agentic AI"",
  ""license"": ""ISC"",
  ""devDependencies"": {
    ""cypress"": ""^13.6.1"",
    ""@cypress/webpack-dev-server"": ""^3.7.0""
  }
}";
        }

        private string GenerateCypressConfig()
        {
            return @"const { defineConfig } = require('cypress');

module.exports = defineConfig({
  e2e: {
    baseUrl: 'http://localhost:3000',
    viewportWidth: 1920,
    viewportHeight: 1080,
    defaultCommandTimeout: 5000,
    requestTimeout: 5000,
    responseTimeout: 5000,
    pageLoadTimeout: 30000,
    chromeWebSecurity: false,
    video: true,
    videoCompression: 32,
    screenshotOnRunFailure: true,
    setupNodeEvents(on, config) {
      // implement node event listeners here
    },
  },
  component: {
    devServer: {
      framework: 'react',
      bundler: 'webpack',
    },
  },
});";
        }

        private string GenerateReadme()
        {
            return @"# Cypress Test Automation

Modern E2E testing with Cypress.

## Installation

```bash
npm install
```

## Running Tests

```bash
# Open Cypress Test Runner
npm run test:open

# Run tests in headless mode
npm run test:headless

# Run all tests
npm test
```

## Project Structure

- `cypress/e2e/` - Test files
- `cypress/support/` - Support files and commands
- `cypress/fixtures/` - Test data

## Features

- Interactive test runner
- Time travel debugging
- Network stubbing
- Screenshots and videos
- Automatic waiting
- Cross-browser testing (Chrome, Firefox, Edge)

## Best Practices

1. Use Page Object Model pattern
2. Avoid using `cy.pause()` in production tests
3. Use explicit waits instead of hard waits
4. Keep tests independent
5. Use fixtures for test data
6. Organize tests by feature/module
";
        }

        private string GenerateGitignore()
        {
            return @"node_modules/
cypress/videos/
cypress/screenshots/
cypress/downloads/
.DS_Store
dist/
coverage/
*.log";
        }

        private string GenerateSupport()
        {
            return @"// Cypress custom commands

Cypress.Commands.add('login', (email, password) => {
  cy.get('[data-testid=email]').type(email);
  cy.get('[data-testid=password]').type(password);
  cy.get('[data-testid=login-btn]').click();
});

Cypress.Commands.add('logout', () => {
  cy.get('[data-testid=logout-btn]').click();
});

beforeEach(() => {
  // Hook for before each test
});

afterEach(() => {
  // Hook for after each test
});";
        }

        #endregion

        #region Helper Methods

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

        private string NormalizeClassName(string name)
        {
            var parts = name.Split(new[] { ' ', '_', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()));
        }

        private string EscapeString(string? value)
        {
            return value?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"") ?? "";
        }

        #endregion
    }
}
