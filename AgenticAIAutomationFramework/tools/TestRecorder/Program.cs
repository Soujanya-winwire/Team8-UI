using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.ZeroCode.Models;
using Newtonsoft.Json;
using System.CommandLine;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║   🎬 Agentic AI Test Recorder - Zero-Code Testing    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");
Console.WriteLine();

var rootCommand = new RootCommand("Test Recorder Tool - Record user interactions as automated tests");

var scenarioNameOption = new Option<string>(
    name: "--scenario",
    description: "Name of the test scenario",
    getDefaultValue: () => "RecordedTest");
scenarioNameOption.AddAlias("-s");

var urlOption = new Option<string>(
    name: "--url",
    description: "Starting URL of the application",
    getDefaultValue: () => "https://www.saucedemo.com");
urlOption.AddAlias("-u");

var moduleOption = new Option<string>(
    name: "--module",
    description: "Module/Feature name for organizing tests",
    getDefaultValue: () => "Default");
moduleOption.AddAlias("-m");

var outputOption = new Option<string>(
    name: "--output",
    description: "Output directory for test scenarios",
    getDefaultValue: () => "TestScenarios");
outputOption.AddAlias("-o");

var modeOption = new Option<string>(
    name: "--mode",
    description: "Recording mode: interactive or guided",
    getDefaultValue: () => "interactive");
modeOption.AddAlias("-md");

rootCommand.AddOption(scenarioNameOption);
rootCommand.AddOption(urlOption);
rootCommand.AddOption(moduleOption);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(modeOption);

rootCommand.SetHandler(async (scenarioName, url, module, output, mode) =>
{
    await RecordTestScenarioAsync(scenarioName, url, module, output, mode);
}, scenarioNameOption, urlOption, moduleOption, outputOption, modeOption);

return await rootCommand.InvokeAsync(args);

async Task RecordTestScenarioAsync(string scenarioName, string url, string module, string outputDir, string mode)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"📝 Scenario Name: {scenarioName}");
    Console.WriteLine($"🌐 Starting URL: {url}");
    Console.WriteLine($"📦 Module: {module}");
    Console.WriteLine($"📂 Output Directory: {outputDir}");
    Console.ResetColor();
    Console.WriteLine();

    if (mode == "guided")
    {
        await GuidedRecordingAsync(scenarioName, url, module, outputDir);
    }
    else
    {
        await InteractiveRecordingAsync(scenarioName, url, module, outputDir);
    }
}

async Task InteractiveRecordingAsync(string scenarioName, string url, string module, string outputDir)
{
    var recorder = new TestRecorder(scenarioName, module);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("🎬 Starting browser in recording mode...");
    Console.WriteLine("⚠️  Note: Playwright will open with Inspector for action recording");
    Console.ResetColor();
    Console.WriteLine();

    await recorder.StartRecordingAsync(url);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Browser opened! Perform your test actions.");
    Console.WriteLine("📍 The browser will record your interactions automatically.");
    Console.ResetColor();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Press ENTER when you're done recording...");
    Console.ResetColor();
    Console.ReadLine();

    var scenario = await recorder.StopRecordingAsync();

    // Prompt for description and tags
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("📝 Enter scenario description (optional): ");
    Console.ResetColor();
    var description = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(description))
    {
        scenario.Description = description;
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("🏷️  Enter tags (comma-separated, optional): ");
    Console.ResetColor();
    var tagsInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(tagsInput))
    {
        scenario.Tags = tagsInput.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
    }

    // Save scenario
    await SaveScenarioAsync(scenario, outputDir);
}

async Task GuidedRecordingAsync(string scenarioName, string url, string module, string outputDir)
{
    Console.WriteLine("🎯 Guided Recording Mode - Build your test step by step");
    Console.WriteLine();

    var scenario = new TestScenario
    {
        Name = scenarioName,
        Module = module,
        StartUrl = url
    };

    Console.Write("📝 Enter scenario description: ");
    scenario.Description = Console.ReadLine() ?? "";

    Console.Write("🏷️  Enter tags (comma-separated): ");
    var tagsInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(tagsInput))
    {
        scenario.Tags = tagsInput.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
    }

    Console.WriteLine();
    Console.WriteLine("📋 Add Test Actions (enter 'done' when finished):");
    Console.WriteLine();
    Console.WriteLine("Available Actions:");
    Console.WriteLine("  1. Click         - Click on an element");
    Console.WriteLine("  2. Type          - Type text into an input field");
    Console.WriteLine("  3. Navigate      - Navigate to a URL");
    Console.WriteLine("  4. Wait          - Wait for specified seconds");
    Console.WriteLine("  5. WaitForElement - Wait for element to appear");
    Console.WriteLine();

    int actionCount = 0;
    while (true)
    {
        Console.Write($"Action #{actionCount + 1} type (or 'done'): ");
        var actionType = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(actionType) || actionType.ToLower() == "done")
            break;

        Console.Write("  Locator (CSS selector or XPath): ");
        var locator = Console.ReadLine() ?? "";

        string? value = null;
        if (actionType.ToLower() == "type" || actionType.ToLower() == "navigate" || 
            actionType.ToLower() == "wait" || actionType.ToLower() == "waitforelement")
        {
            Console.Write($"  Value/Text: ");
            value = Console.ReadLine();
        }

        Console.Write("  Description (optional): ");
        var description = Console.ReadLine();

        scenario.Actions.Add(new RecordedAction
        {
            ActionType = actionType,
            Locator = locator,
            Value = value,
            Description = string.IsNullOrWhiteSpace(description) ? $"{actionType} on {locator}" : description,
            Timestamp = actionCount
        });

        actionCount++;
        Console.WriteLine($"  ✅ Action added!");
        Console.WriteLine();
    }

    Console.WriteLine();
    Console.WriteLine("✔️ Add Assertions (enter 'done' when finished):");
    Console.WriteLine();
    Console.WriteLine("Available Assertions:");
    Console.WriteLine("  1. ElementVisible  - Verify element is visible");
    Console.WriteLine("  2. TextEquals      - Verify text equals expected value");
    Console.WriteLine("  3. TextContains    - Verify text contains expected value");
    Console.WriteLine("  4. UrlContains     - Verify URL contains expected value");
    Console.WriteLine("  5. TitleEquals     - Verify page title equals expected value");
    Console.WriteLine();

    int assertionCount = 0;
    while (true)
    {
        Console.Write($"Assertion #{assertionCount + 1} type (or 'done'): ");
        var assertionType = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(assertionType) || assertionType.ToLower() == "done")
            break;

        string locator = "";
        if (assertionType.ToLower() != "urlcontains" && assertionType.ToLower() != "titleequals")
        {
            Console.Write("  Locator (CSS selector or XPath): ");
            locator = Console.ReadLine() ?? "";
        }

        string? expectedValue = null;
        if (assertionType.ToLower() != "elementvisible")
        {
            Console.Write("  Expected Value: ");
            expectedValue = Console.ReadLine();
        }

        Console.Write("  Description (optional): ");
        var description = Console.ReadLine();

        scenario.Assertions.Add(new Assertion
        {
            Type = assertionType,
            Locator = locator,
            ExpectedValue = expectedValue,
            Description = string.IsNullOrWhiteSpace(description) ? $"Verify {assertionType}" : description
        });

        assertionCount++;
        Console.WriteLine($"  ✅ Assertion added!");
        Console.WriteLine();
    }

    await SaveScenarioAsync(scenario, outputDir);
}

async Task SaveScenarioAsync(TestScenario scenario, string outputDir)
{
    // Create output directory structure
    var moduleDir = Path.Combine(outputDir, scenario.Module);
    if (!Directory.Exists(moduleDir))
    {
        Directory.CreateDirectory(moduleDir);
    }

    // Save as JSON
    var fileName = $"{scenario.Name.Replace(" ", "_")}.json";
    var filePath = Path.Combine(moduleDir, fileName);
    
    var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
    await File.WriteAllTextAsync(filePath, json);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("════════════════════════════════════════════════════════");
    Console.WriteLine("✅ Test Scenario Saved Successfully!");
    Console.WriteLine("════════════════════════════════════════════════════════");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"📁 File: {filePath}");
    Console.WriteLine($"📊 Actions: {scenario.Actions.Count}");
    Console.WriteLine($"✔️  Assertions: {scenario.Assertions.Count}");
    Console.WriteLine($"🏷️  Tags: {string.Join(", ", scenario.Tags)}");
    Console.ResetColor();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("🚀 To run this test:");
    Console.ResetColor();
    Console.WriteLine($"   var runner = new ZeroCodeTestRunner();");
    Console.WriteLine($"   await runner.ExecuteScenarioAsync(\"{scenario.Name}\", \"{scenario.Module}\");");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("════════════════════════════════════════════════════════");
    Console.ResetColor();
}
