# Zero-Code Test Designer

## Getting Started

1. Open `index.html` in your browser
2. Create a new test scenario or record one
3. Configure your test actions and assertions
4. Save the scenario JSON file to `TestScenarios/{ModuleName}/` directory
5. Run tests using: `ZeroCodeTestRunner`

## Recording Tests

To record tests automatically:
```bash
dotnet run --project tools/TestRecorder -- --scenario "My Test" --url "https://example.com"
```

## Configuration

Update your application URLs in the Configuration tab:
- Set Base URLs for each environment (Dev, QA, Prod)
- Set API URLs for API testing
- Configure browser, OS, and execution preferences

All configurations are saved to `Configuration/` directory.

## Running Zero-Code Tests

```csharp
var runner = new ZeroCodeTestRunner();

// Run single scenario
await runner.ExecuteScenarioAsync("Login Test", "Authentication");

// Run all scenarios in a module
await runner.ExecuteModuleAsync("Authentication");

// Run scenarios by tag
await runner.ExecuteByTagAsync("smoke");
```
