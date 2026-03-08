# Advanced Data-Driven Testing - TestData Module

## Overview
The TestData module now supports advanced data-driven testing with multiple data formats and dynamic variable substitution.

## Supported Data Formats
- **CSV** - Comma-separated values files
- **JSON** - JSON array of objects
- **Excel** - .xlsx and .xls files (EPPlus library)

## Features

### 1. File-Based Data Reading
Read test data directly from files in the TestData folder:

```csharp
using AgenticAI.Core.DataDriven;

// Auto-detect format based on file extension
var dataSet = TestDataReader.ReadFromFile("SampleLogin.csv");
var dataSet = TestDataReader.ReadFromFile("SampleUsers.json");
var dataSet = TestDataReader.ReadFromFile("SampleUsers.xlsx");

// Or use specific format readers
var dataSet = TestDataReader.ReadFromCsvFile("TestData/SampleLogin.csv");
var dataSet = TestDataReader.ReadFromJsonFile("TestData/SampleUsers.json");
var dataSet = TestDataReader.ReadFromExcelFile("TestData/SampleUsers.xlsx");
```

### 2. String-Based Parsing
Parse data from strings (useful for inline test data):

```csharp
// Parse CSV string
var csvData = "username,password\nuser1,pass1\nuser2,pass2";
var dataSet = DataSetReader.ParseCsv(csvData);

// Parse JSON string
var jsonData = @"[
    { ""username"": ""user1"", ""password"": ""pass1"" },
    { ""username"": ""user2"", ""password"": ""pass2"" }
]";
var dataSet = DataSetReader.ParseJson(jsonData);

// Parse Excel file
var dataSet = DataSetReader.ParseExcel("path/to/file.xlsx");
```

### 3. Data Structure
All methods return `DataTestSet` with `List<Dictionary<string,string>>`:

```csharp
public class DataTestSet
{
    public List<string> Columns { get; set; }
    public List<Dictionary<string, string>> Rows { get; set; }
    public int RowCount => Rows.Count;
}

// Access data
foreach (var row in dataSet.Rows)
{
    Console.WriteLine($"Username: {row["username"]}, Password: {row["password"]}");
}
```

### 4. Dynamic Variable Substitution
Supports both `${variable}` and `{{variable}}` syntax (case-insensitive):

```csharp
var template = "Login with ${username} and password {{password}}";
var row = new Dictionary<string, string>
{
    { "username", "testuser" },
    { "password", "testpass" }
};

var result = DataSetReader.SubstitutePlaceholders(template, row);
// Result: "Login with testuser and password testpass"
```

### 5. Data-Driven Test Execution
Execute scenarios with test data:

```csharp
using AgenticAI.Core.DataDriven;
using AgenticAI.Core.ZeroCode;

// Load scenario
var scenarioManager = new ScenarioManager();
var scenario = scenarioManager.LoadScenario("LoginTest", "Authentication");

// Load test data
var dataSet = TestDataReader.ReadFromFile("LoginTestData.csv");

// Execute with data
var runner = new DataDrivenRunner(async () => await WebDriverFactory.CreateDriverAsync());
var results = await runner.RunAsync(scenario, dataSet);

// Check results
foreach (var result in results)
{
    Console.WriteLine($"Row {result.RowIndex + 1}: {result.Result.Status}");
    Console.WriteLine($"Data: {string.Join(", ", result.DataRow.Select(kv => $"{kv.Key}={kv.Value}"))}");
}
```

## File Formats

### CSV Format
```csv
username,password,email,expectedResult
admin@test.com,Admin123!,admin@test.com,success
user@test.com,User123!,user@test.com,success
invalid@test.com,wrong,invalid@test.com,failure
```

### JSON Format
```json
[
  {
    "username": "admin@test.com",
    "password": "Admin123!",
    "email": "admin@test.com",
    "expectedResult": "success"
  },
  {
    "username": "user@test.com",
    "password": "User123!",
    "email": "user@test.com",
    "expectedResult": "success"
  }
]
```

### Excel Format
- First row must contain column headers
- Subsequent rows contain test data
- Empty rows are automatically skipped
- Can specify worksheet name or use first sheet by default

## Creating Sample Excel Files

Use the ExcelDataGenerator helper class:

```csharp
using AgenticAI.Core.DataDriven;

// Create sample users Excel file
ExcelDataGenerator.CreateSampleUsersExcel("TestData/SampleUsers.xlsx");

// Create sample products Excel file
ExcelDataGenerator.CreateSampleProductsExcel("TestData/SampleProducts.xlsx");
```

## Integration with Test Scenarios

### In Test Scenario Actions
Use placeholders in your scenario action values:

```json
{
  "name": "Login Test",
  "actions": [
    {
      "actionType": "Navigate",
      "value": "https://example.com/login"
    },
    {
      "actionType": "Type",
      "locator": "#username",
      "value": "${username}"
    },
    {
      "actionType": "Type",
      "locator": "#password",
      "value": "{{password}}"
    },
    {
      "actionType": "Click",
      "locator": "#login-button"
    }
  ]
}
```

### Execute with Data
The framework automatically substitutes placeholders with actual values from each data row.

## API Reference

### TestDataReader Class

| Method | Description |
|--------|-------------|
| `ReadFromFile(string fileName)` | Auto-detect and read from file |
| `ReadFromCsvFile(string filePath)` | Read CSV file |
| `ReadFromJsonFile(string filePath)` | Read JSON file |
| `ReadFromExcelFile(string filePath, string? worksheetName = null)` | Read Excel file |
| `GetAvailableTestDataFiles()` | List all available test data files |

### DataSetReader Class

| Method | Description |
|--------|-------------|
| `ParseCsv(string csvContent)` | Parse CSV string |
| `ParseJson(string jsonContent)` | Parse JSON string |
| `ParseExcel(string filePath, string? worksheetName = null)` | Parse Excel file |
| `SubstitutePlaceholders(string template, Dictionary<string,string> row)` | Replace placeholders with values |

### DataDrivenRunner Class

| Method | Description |
|--------|-------------|
| `RunAsync(TestScenario scenario, DataTestSet dataSet)` | Execute scenario once per data row |

## Best Practices

1. **File Organization**: Keep test data files in the `TestData` folder organized by module or feature
2. **Naming Convention**: Use descriptive names like `LoginTestData.csv`, `UserRegistrationData.json`
3. **Column Names**: Use clear, descriptive column names that match placeholder names
4. **Data Validation**: Validate test data before execution
5. **Error Handling**: Handle missing files and invalid data gracefully
6. **Documentation**: Document the expected data format for each test scenario

## Example Test Data Files

Sample files included:
- `TestData/SampleLogin.csv` - Login test data
- `TestData/SampleUsers.json` - User management test data
- `TestData/SampleUsers.xlsx` - Excel format user data (generate using ExcelDataGenerator)

## Troubleshooting

### File Not Found Error
- Ensure test data files are in the `TestData` folder
- Check file name and extension
- Verify the TestDataPath is correctly configured

### Invalid Data Format
- Check CSV has proper headers and comma-separated values
- Verify JSON is a valid array of objects
- Ensure Excel file has headers in first row

### Placeholder Not Replaced
- Check placeholder syntax: `${columnName}` or `{{columnName}}`
- Verify column name matches exactly (case-insensitive)
- Ensure the column exists in your test data

## License Note
EPPlus library is used for Excel support. The framework is configured for non-commercial use. For commercial use, please review EPPlus licensing requirements.
