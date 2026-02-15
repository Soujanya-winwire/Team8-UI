using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Designer;
using System.CommandLine;
using System.Diagnostics;

Console.WriteLine("??????????????????????????????????????????????????????????");
Console.WriteLine("?   ?? Zero-Code Testing Launcher                      ?");
Console.WriteLine("??????????????????????????????????????????????????????????");
Console.WriteLine();

var rootCommand = new RootCommand("Launch Zero-Code Testing Tools");

// Visual Designer Command
var designerCommand = new Command("designer", "Launch Visual Test Designer UI")
{
    Handler = System.CommandLine.Invocation.CommandHandler.Create(LaunchDesigner)
};

// Test Runner Command
var runCommand = new Command("run", "Run zero-code test scenarios");
var scenarioOption = new Option<string?>("--scenario", "Scenario name to execute");
var moduleOption = new Option<string?>("--module", "Module to execute");
var tagOption = new Option<string?>("--tag", "Tag to filter scenarios");

runCommand.AddOption(scenarioOption);
runCommand.AddOption(moduleOption);
runCommand.AddOption(tagOption);

runCommand.SetHandler(async (scenario, module, tag) =>
{
    await RunTestsAsync(scenario, module, tag);
}, scenarioOption, moduleOption, tagOption);

// List Scenarios Command
var listCommand = new Command("list", "List all available test scenarios")
{
    Handler = System.CommandLine.Invocation.CommandHandler.Create(ListScenarios)
};

rootCommand.AddCommand(designerCommand);
rootCommand.AddCommand(runCommand);
rootCommand.AddCommand(listCommand);

return await rootCommand.InvokeAsync(args);

void LaunchDesigner()
{
    Console.WriteLine("?? Generating Visual Test Designer...");
    Console.WriteLine();

    var designer = new VisualTestDesigner("TestDesigner");
    var htmlPath = designer.GenerateDesignerUI();

    Console.WriteLine("? Visual Test Designer generated!");
    Console.WriteLine($"?? Location: {Path.GetFullPath(htmlPath)}");
    Console.WriteLine();

    try
    {
        var fullPath = Path.GetFullPath(htmlPath);
        var psi = new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        };
        Process.Start(psi);
        
        Console.WriteLine("?? Designer opened in your browser!");
        Console.WriteLine();
        Console.WriteLine("????????????????????????????????????????????????????????");
        Console.WriteLine("?? Quick Tips:");
        Console.WriteLine("????????????????????????????????????????????????????????");
        Console.WriteLine("• Use Configuration tab to set URLs");
        Console.WriteLine("• Create tests visually with buttons");
        Console.WriteLine("• Download JSON and save to TestScenarios/");
        Console.WriteLine("• Run tests using 'zerotool run' command");
        Console.WriteLine("????????????????????????????????????????????????????????");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"??  Could not auto-open: {ex.Message}");
        Console.WriteLine($"Please open manually: {Path.GetFullPath(htmlPath)}");
    }
}

async Task RunTestsAsync(string? scenario, string? module, string? tag)
{
    var runner = new ZeroCodeTestRunner();
    
    Console.WriteLine("?? Starting Zero-Code Test Execution...");
    Console.WriteLine();

    try
    {
        if (!string.IsNullOrEmpty(scenario) && !string.IsNullOrEmpty(module))
        {
            Console.WriteLine($"?? Executing Scenario: {scenario}");
            Console.WriteLine($"?? Module: {module}");
            var result = await runner.ExecuteScenarioAsync(scenario, module);
            PrintResult(result);
        }
        else if (!string.IsNullOrEmpty(module))
        {
            Console.WriteLine($"?? Executing Module: {module}");
            var results = await runner.ExecuteModuleAsync(module);
            PrintResults(results);
        }
        else if (!string.IsNullOrEmpty(tag))
        {
            Console.WriteLine($"???  Executing Tag: {tag}");
            var results = await runner.ExecuteByTagAsync(tag);
            PrintResults(results);
        }
        else
        {
            Console.WriteLine("?? Executing All Scenarios...");
            var summary = await runner.ExecuteAllScenariosAsync();
            PrintSummary(summary);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Execution failed: {ex.Message}");
    }
}

void ListScenarios()
{
    Console.WriteLine("?? Available Test Scenarios:");
    Console.WriteLine();

    var scenariosPath = "TestScenarios";
    if (!Directory.Exists(scenariosPath))
    {
        Console.WriteLine("??  No scenarios found. Create some using the designer!");
        return;
    }

    var jsonFiles = Directory.GetFiles(scenariosPath, "*.json", SearchOption.AllDirectories);
    
    var groupedByModule = jsonFiles
        .Select(f => new { 
            Path = f, 
            Module = Path.GetFileName(Path.GetDirectoryName(f)) ?? "Unknown",
            Name = Path.GetFileNameWithoutExtension(f)
        })
        .GroupBy(x => x.Module);

    foreach (var group in groupedByModule)
    {
        Console.WriteLine($"?? {group.Key}");
        foreach (var file in group)
        {
            Console.WriteLine($"   ?? {file.Name}");
        }
        Console.WriteLine();
    }

    Console.WriteLine($"Total: {jsonFiles.Length} scenarios");
}

void PrintResult(AgenticAI.Core.Models.TestCaseResult result)
{
    var icon = result.Status == AgenticAI.Core.Enums.TestStatus.Passed ? "?" : "?";
    Console.WriteLine();
    Console.WriteLine($"{icon} {result.TestCaseName} - {result.Status}");
    Console.WriteLine($"   Duration: {(result.EndTime - result.StartTime).TotalSeconds:F2}s");
    Console.WriteLine($"   Steps: {result.Steps.Count}");
    
    if (!string.IsNullOrEmpty(result.ErrorMessage))
    {
        Console.WriteLine($"   Error: {result.ErrorMessage}");
    }
}

void PrintResults(List<AgenticAI.Core.Models.TestCaseResult> results)
{
    Console.WriteLine();
    Console.WriteLine("????????????????????????????????????????????????????????");
    
    foreach (var result in results)
    {
        PrintResult(result);
    }

    var passed = results.Count(r => r.Status == AgenticAI.Core.Enums.TestStatus.Passed);
    var failed = results.Count(r => r.Status == AgenticAI.Core.Enums.TestStatus.Failed);

    Console.WriteLine();
    Console.WriteLine("????????????????????????????????????????????????????????");
    Console.WriteLine($"Total: {results.Count} | ? Passed: {passed} | ? Failed: {failed}");
    Console.WriteLine("????????????????????????????????????????????????????????");
}

void PrintSummary(AgenticAI.Core.Models.TestExecutionSummary summary)
{
    Console.WriteLine();
    Console.WriteLine("????????????????????????????????????????????????????????");
    Console.WriteLine("   ?? Zero-Code Test Execution Summary");
    Console.WriteLine("????????????????????????????????????????????????????????");
    Console.WriteLine($"Total Tests: {summary.TotalTests}");
    Console.WriteLine($"? Passed: {summary.PassedTests}");
    Console.WriteLine($"? Failed: {summary.FailedTests}");
    Console.WriteLine($"??  Skipped: {summary.SkippedTests}");
    Console.WriteLine($"??  Duration: {summary.Duration.TotalSeconds:F2}s");
    Console.WriteLine($"?? Pass Rate: {summary.PassPercentage:F1}%");
    Console.WriteLine($"?? Browser: {summary.Browser}");
    Console.WriteLine($"?? OS: {summary.OperatingSystem}");
    Console.WriteLine($"?? Environment: {summary.Environment}");
    Console.WriteLine("????????????????????????????????????????????????????????");
}
