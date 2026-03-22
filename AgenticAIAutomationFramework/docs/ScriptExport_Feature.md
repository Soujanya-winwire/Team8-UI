# Script Export Feature - Documentation

## Overview

The Script Export feature allows you to convert zero-code test scenarios recorded in the Agentic AI Automation Framework into executable test scripts for multiple automation frameworks and programming languages.

**Supported Frameworks:**
- **Selenium** (C# with NUnit)
- **Playwright** (C#, Python, JavaScript, TypeScript)
- **Cypress** (JavaScript, TypeScript)

## Features

### Multi-Framework Support
Export the same test scenarios to different automation frameworks without manual recoding.

### Multi-Language Support
- **Selenium**: C# (NUnit)
- **Playwright**: C#, Python, JavaScript, TypeScript
- **Cypress**: JavaScript, TypeScript

### Complete Project Generation
- Test files with proper framework conventions
- Page Object Models
- Configuration files (csproj, package.json, pytest.ini, etc.)
- README with setup instructions
- Project structure and dependencies

### Multiple Export Options
- Generate test code for individual scenarios
- Export complete project structure
- Download as ZIP archive
- Batch export to all frameworks simultaneously
- Get project structure summary

## API Endpoints

### 1. Get Available Formats
```
GET /api/export/formats
```

**Response:**
```json
{
  "formats": [
    { "format": "Selenium", "name": "Selenium" },
    { "format": "Playwright", "name": "Playwright" },
    { "format": "Cypress", "name": "Cypress" }
  ]
}
```

### 2. Get Languages for Format
```
GET /api/export/languages/{format}
```

**Parameters:**
- `format`: Selenium, Playwright, or Cypress

**Example Response:**
```json
{
  "format": "Playwright",
  "languages": ["CSharp", "Python", "JavaScript", "TypeScript"]
}
```

### 3. Generate Test Code
```
POST /api/export/generate-test
Content-Type: application/json

{
  "scenario": { /* TestScenario object */ },
  "format": "Playwright",
  "language": "CSharp"
}
```

**Response:**
```json
{
  "code": "using Microsoft.Playwright;\n..."
}
```

### 4. Generate Complete Project
```
POST /api/export/generate-project
Content-Type: application/json

{
  "scenarios": [ /* Array of TestScenario objects */ ],
  "format": "Playwright",
  "language": "CSharp",
  "projectName": "MyAutomationTests",
  "includePageObjects": true,
  "includeConfiguration": true,
  "includeReadme": true,
  "author": "Your Name"
}
```

**Response:**
```json
{
  "projectName": "MyAutomationTests",
  "format": "Playwright",
  "language": "CSharp",
  "generatedAt": "2024-01-15T10:30:00Z",
  "fileCount": 15,
  "files": [
    "Tests/LoginTest.cs",
    "Pages/LoginPage.cs",
    "playwright.config.ts",
    "README.md",
    ...
  ]
}
```

### 5. Export as ZIP
```
POST /api/export/export-zip
Content-Type: application/json

{
  "scenarios": [ /* Array of TestScenario objects */ ],
  "format": "Playwright",
  "language": "CSharp",
  "projectName": "MyAutomationTests"
}
```

**Response:** Binary ZIP file download

### 6. Get Project Summary
```
POST /api/export/project-summary
Content-Type: application/json

{
  "scenarios": [ /* Array of TestScenario objects */ ],
  "format": "Playwright",
  "language": "CSharp",
  "projectName": "MyAutomationTests"
}
```

**Response:**
```json
{
  "summary": "=== MyAutomationTests Project Structure ===\nFramework: Playwright\nLanguage: CSharp\n..."
}
```

### 7. Generate Page Object
```
POST /api/export/generate-page-object
Content-Type: application/json

{
  "scenario": { /* TestScenario object */ },
  "format": "Playwright",
  "language": "CSharp"
}
```

**Response:**
```json
{
  "code": "public class LoginPage\n{\n  private readonly IPage _page;\n  ..."
}
```

### 8. Batch Export to Multiple Formats
```
POST /api/export/export-multiple
Content-Type: application/json

{
  "scenarios": [ /* Array of TestScenario objects */ ],
  "language": "CSharp"
}
```

**Response:** ZIP file containing all three frameworks (Selenium, Playwright, Cypress)

## Generated Project Structure

### For Selenium (C#)
```
AutomationTests/
├── AutomationTests.csproj
├── README.md
├── Configuration/
│   └── Settings.json
├── Tests/
│   ├── BaseTest.cs
│   └── [ScenarioName]Test.cs
├── Pages/
│   └── [ScenarioName]Page.cs
└── Utilities/
    └── WebDriverHelper.cs
```

### For Playwright
#### C#
```
AutomationTests/
├── AutomationTests.csproj
├── playwright.config.ts
├── README.md
├── Tests/
│   └── [ScenarioName]Test.cs
└── Pages/
    └── [ScenarioName]Page.cs
```

#### Python
```
AutomationTests/
├── requirements.txt
├── pytest.ini
├── README.md
├── tests/
│   └── test_[scenario_name].py
└── pages/
    └── [scenario_name]_page.py
```

#### JavaScript
```
AutomationTests/
├── package.json
├── playwright.config.js
├── README.md
├── tests/
│   └── [scenarioName].spec.js
└── pages/
    └── [scenarioName]Page.js
```

#### TypeScript
```
AutomationTests/
├── package.json
├── playwright.config.ts
├── tsconfig.json
├── README.md
├── tests/
│   └── [scenarioName].spec.ts
└── pages/
    └── [scenarioName]Page.ts
```

### For Cypress
```
AutomationTests/
├── package.json
├── cypress.config.js
├── README.md
├── cypress/
│   ├── e2e/
│   │   └── [scenarioName].cy.js
│   └── support/
│       ├── commands.js
│       └── pages/
│           └── [scenarioName]Page.js
```

## Language & Framework Support Matrix

| Framework | C# | Python | JavaScript | TypeScript |
|-----------|:--:|:------:|:----------:|:----------:|
| Selenium | ✓ | ✗ | ✗ | ✗ |
| Playwright | ✓ | ✓ | ✓ | ✓ |
| Cypress | ✗ | ✗ | ✓ | ✓ |

## Usage Examples

### Example 1: Export Single Test to Playwright C#

```csharp
var exportController = new ScriptExportController();

var testScenario = new TestScenario 
{
    Name = "Login Test",
    Module = "Authentication",
    StartUrl = "https://example.com/login",
    Steps = new List<TestStep> { /* ... */ }
};

var request = new GenerateTestRequest
{
    Scenario = testScenario,
    Format = "Playwright",
    Language = "CSharp"
};

var result = await exportController.GenerateTestCode(request);
```

### Example 2: Export Project as ZIP

```csharp
var scenarios = new List<TestScenario> { /* ... */ };

var request = new GenerateProjectRequest
{
    Scenarios = scenarios,
    Format = "Cypress",
    Language = "JavaScript",
    ProjectName = "E2ETests",
    IncludePageObjects = true,
    IncludeReadme = true
};

var result = await exportController.ExportAsZip(request);
// Returns ZIP file: E2ETests_20240115_103000.zip
```

### Example 3: Batch Export to All Frameworks

```csharp
var scenarios = new List<TestScenario> { /* ... */ };

var request = new ExportMultipleRequest
{
    Scenarios = scenarios,
    Language = "CSharp"
};

var result = await exportController.ExportMultipleFormats(request);
// Returns ZIP containing:
// - AutomationTests_Selenium/
// - AutomationTests_Playwright/
// - AutomationTests_Cypress/
```

## Code Generation Features

### Action Mapping
Supports conversion of recorded actions to framework-specific code:

| Action | Selenium | Playwright C# | Playwright Python | Playwright JS | Cypress |
|--------|:--------:|:-------------:|:-----------------:|:-------------:|:-------:|
| Click | ✓ | ✓ | ✓ | ✓ | ✓ |
| Type | ✓ | ✓ | ✓ | ✓ | ✓ |
| Select | ✓ | ✓ | ✓ | ✓ | ✓ |
| Hover | ✓ | ✓ | ✓ | ✓ | ✓ |
| DoubleClick | ✓ | ✓ | ✓ | ✓ | ✓ |
| RightClick | ✓ | ✓ | ✓ | ✓ | ✓ |
| Check | ✓ | ✓ | ✓ | ✓ | ✓ |
| Uncheck | ✓ | ✓ | ✓ | ✓ | ✓ |
| Navigate | ✓ | ✓ | ✓ | ✓ | ✓ |

### Assertion Mapping
Supports conversion of recorded assertions to framework-specific code:

| Assertion | Selenium | Playwright | Cypress |
|-----------|:--------:|:----------:|:-------:|
| ElementVisible | ✓ | ✓ | ✓ |
| ElementExists | ✓ | ✓ | ✓ |
| TextEquals | ✓ | ✓ | ✓ |
| TextContains | ✓ | ✓ | ✓ |
| URLContains | ✓ | ✓ | ✓ |
| TitleEquals | ✓ | ✓ | ✓ |
| ElementDisabled | ✓ | ✓ | ✓ |
| ElementEnabled | ✓ | ✓ | ✓ |

### Locator Support
Automatically converts locators to framework-specific format:
- CSS Selectors (#id, .class, [attr="value"])
- XPath (//div[@class='value'])
- ID Selectors
- Class Selectors
- Attribute Selectors

## Configuration Options

The `CodeGenerationOptions` class provides full control over generated projects:

```csharp
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
}
```

## Quick Start - Using Exported Projects

### Selenium (C#)
```bash
cd AutomationTests
dotnet restore
dotnet test
```

### Playwright (C#)
```bash
cd AutomationTests
dotnet restore
dotnet test
```

### Playwright (Python)
```bash
cd AutomationTests
pip install -r requirements.txt
pytest
```

### Playwright (JavaScript/TypeScript)
```bash
cd AutomationTests
npm install
npm test
```

### Cypress
```bash
cd AutomationTests
npm install
npm run test:open  # Interactive
npm run test       # Headless
```

## Best Practices

1. **Naming Conventions**: Test scenario names should be descriptive and follow camelCase or snake_case conventions for language compatibility.

2. **Locators**: Use stable, unique locators (IDs, data-test attributes) rather than XPath or CSS selectors based on element positions.

3. **Page Objects**: Enable page object generation to maintain test maintainability.

4. **Configuration**: Include configuration files for framework-specific settings like timeouts, browsers, and headless mode.

5. **README**: Generate README for each project to document setup, execution, and customization steps.

## Error Handling

All endpoints return appropriate HTTP status codes:
- **200 OK**: Successful generation
- **400 Bad Request**: Invalid input (missing scenarios, unsupported format/language combination)
- **500 Internal Server Error**: Unexpected server error

Error responses include descriptive `error` field:
```json
{
  "error": "Language CSharp not supported for Cypress"
}
```

## Limitations

- Test data parameterization from recorded steps is basic; enhance with dynamic data sources as needed
- Advanced Playwright features (context, fixtures) may require manual adjustment
- Selenium tests target ChromeDriver; modify for other browsers as needed
- Custom wait strategies should be manually added to generated code

## Future Enhancements

- [ ] Support for additional frameworks (Protractor, TestCafe)
- [ ] Test data parameterization from Excel/CSV sources
- [ ] CI/CD pipeline generation (GitHub Actions, Azure Pipelines)
- [ ] Test report template generation
- [ ] Performance testing framework export
- [ ] Mobile app testing framework support
