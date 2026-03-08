# Data-Driven Testing Quick Start Guide

## What is Data-Driven Testing?
Data-driven testing allows you to execute the same test scenario multiple times with different sets of test data. Each row in your test data represents one execution of the scenario.

## Quick Start in 3 Steps

### Step 1: Prepare Your Test Data

Create a test data file in the `TestData` folder. You can use CSV, JSON, or Excel format.

**Example: LoginTestData.csv**
```csv
username,password,expectedResult
admin@test.com,Admin123!,success
user@test.com,User123!,success
invalid@test.com,wrongpass,failure
```

**Example: LoginTestData.json**
```json
[
  { "username": "admin@test.com", "password": "Admin123!", "expectedResult": "success" },
  { "username": "user@test.com", "password": "User123!", "expectedResult": "success" },
  { "username": "invalid@test.com", "password": "wrongpass", "expectedResult": "failure" }
]
```

### Step 2: Create a Test Scenario with Placeholders

Use `${columnName}` or `{{columnName}}` placeholders in your test scenario:

```json
{
  "name": "Login Test",
  "module": "Authentication",
  "startUrl": "https://example.com/login",
  "steps": [
    {
      "stepType": "Action",
      "order": 1,
      "action": {
        "actionType": "Type",
        "locator": "#username",
        "value": "${username}",
        "description": "Enter username"
      }
    },
    {
      "stepType": "Action",
      "order": 2,
      "action": {
        "actionType": "Type",
        "locator": "#password",
        "value": "{{password}}",
        "description": "Enter password"
      }
    },
    {
      "stepType": "Action",
      "order": 3,
      "action": {
        "actionType": "Click",
        "locator": "#login-button",
        "description": "Click login"
      }
    }
  ]
}
```

### Step 3: Execute the Test with Data

```csharp
using AgenticAI.Core.DataDriven;
using AgenticAI.Core.ZeroCode;

// Load scenario
var scenarioManager = new ScenarioManager();
var scenario = scenarioManager.LoadScenario("Login Test", "Authentication");

// Load test data
var dataSet = TestDataReader.ReadFromFile("LoginTestData.csv");

// Execute
var runner = new DataDrivenRunner(async () => await WebDriverFactory.CreateDriverAsync());
var results = await runner.RunAsync(scenario, dataSet);

// View results
foreach (var result in results)
{
    Console.WriteLine($"Row {result.RowIndex + 1}: {result.Result.Status}");
}
```

## Supported Data Formats

### CSV (Comma-Separated Values)
- Simple text format
- First row = column headers
- Easy to create in Excel or any text editor
- File extension: `.csv`

### JSON (JavaScript Object Notation)
- Structured data format
- Each object represents one test case
- Good for complex data structures
- File extension: `.json`

### Excel
- Rich formatting options
- Multiple worksheets supported
- Easy to manage large datasets
- File extensions: `.xlsx`, `.xls`

## Placeholder Syntax

Both syntaxes are supported (case-insensitive):

| Syntax | Example | When to Use |
|--------|---------|-------------|
| `${variable}` | `${username}` | Standard syntax, compatible with shell variables |
| `{{variable}}` | `{{password}}` | Mustache-style, popular in templates |

**Examples:**
```
Login URL: https://example.com/login?user=${username}
Email: {{email}}
Full Name: {{firstName}} {{lastName}}
```

## Common Use Cases

### 1. Login Tests with Multiple Users
Test data contains different user credentials to validate various login scenarios.

### 2. Form Validation
Test data includes valid and invalid inputs to verify form validation rules.

### 3. Search Functionality
Test data contains different search terms and expected results.

### 4. E-commerce Checkout
Test data includes different product combinations, quantities, and payment methods.

### 5. User Registration
Test data contains different user profiles to test registration flow.

## File Location

All test data files should be placed in the `TestData` folder at the solution root:

```
AgenticAIAutomationFramework/
├── TestData/
│   ├── LoginTestData.csv
│   ├── SampleUsers.json
│   ├── SampleProducts.xlsx
│   └── ...
├── TestScenarios/
├── src/
└── tests/
```

## Generating Sample Excel Files

Use the built-in generator to create sample Excel files:

```csharp
using AgenticAI.Core.DataDriven;

// Generate sample users Excel file
ExcelDataGenerator.CreateSampleUsersExcel("TestData/SampleUsers.xlsx");

// Generate sample products Excel file
ExcelDataGenerator.CreateSampleProductsExcel("TestData/SampleProducts.xlsx");
```

Or run the test:
```powershell
dotnet test --filter "Category=ExcelGeneration"
```

## Tips & Best Practices

1. **Start Small**: Begin with CSV files for simple test cases, move to Excel for complex scenarios

2. **Descriptive Column Names**: Use clear names like `username`, `password`, `expectedResult`

3. **Include Expected Results**: Add columns for expected outcomes to validate test results

4. **Separate Concerns**: Create different files for different test modules (login data, registration data, etc.)

5. **Version Control**: Commit test data files to Git for team collaboration

6. **Data Security**: Never commit sensitive real credentials. Use test accounts only.

7. **Documentation**: Add a comment row or README explaining the data structure

## Troubleshooting

### Problem: File Not Found
**Solution**: Verify the file is in the `TestData` folder and has the correct name/extension

### Problem: Placeholders Not Replaced
**Solution**: Check that column names in your data file match the placeholders (case-insensitive)

### Problem: Excel File Not Opening
**Solution**: Ensure EPPlus package is installed and the file is not corrupted

### Problem: Empty Rows
**Solution**: Excel parser automatically skips empty rows. Remove unnecessary blank rows from your data.

## Next Steps

1. Create your first test data file
2. Add placeholders to an existing test scenario
3. Run the data-driven test
4. Review results and iterate

For more advanced features and API reference, see [README_DataDriven.md](README_DataDriven.md)

## Need Help?

- Check the examples in `DataDrivenExamples.cs`
- Review existing test data files in `TestData/` folder
- Run unit tests: `dotnet test --filter "FullyQualifiedName~DataDrivenTests"`
