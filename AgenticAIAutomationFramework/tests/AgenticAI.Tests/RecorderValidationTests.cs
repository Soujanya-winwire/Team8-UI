using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.Logging;
using Newtonsoft.Json;

namespace AgenticAI.RecorderTests
{
    /// <summary>
    /// Validation tests for Enhanced Test Recorder
    /// Demonstrates all 10 implemented features
    /// </summary>
    public class RecorderValidationTests
    {
        /// <summary>
        /// Test 1: Basic Click Recording with Smart Selectors
        /// Validates: Event capture, Element Analyzer, Smart Locator Generator
        /// </summary>
        public static async Task Test_BasicClickRecording()
        {
            Logger.Info("=== Test 1: Basic Click Recording ===");
            
            var recorder = new TestRecorder("BasicClickTest", "Validation");
            recorder.SetScenarioDescription("Validates click recording with smart selectors");
            recorder.AddTag("validation");
            
            // Subscribe to action events
            int actionCount = 0;
            recorder.OnActionCaptured += (action) =>
            {
                actionCount++;
                Logger.Info($"  ? Captured: {action.ActionType} - {action.Locator}");
            };
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click on 'Form Authentication' link (should use text selector)");
            Logger.Info("2. Observe element highlighting");
            Logger.Info("3. Verify smart selector is generated");
            Logger.Info("\n??  Perform actions in browser, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info($"\n? Test Complete: {actionCount} actions recorded");
            Logger.Info($"?? Scenario saved with {scenario.Actions.Count} actions");
            
            // Validate selectors
            foreach (var action in scenario.Actions)
            {
                Logger.Info($"  Action: {action.ActionType} ? {action.Locator}");
            }
        }

        /// <summary>
        /// Test 2: Input Debouncing
        /// Validates: Input event capture, Debouncing, Action deduplication
        /// </summary>
        public static async Task Test_InputDebouncing()
        {
            Logger.Info("\n=== Test 2: Input Debouncing ===");
            
            var recorder = new TestRecorder("InputDebouncingTest", "Validation");
            recorder.AddTag("input");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/login");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Type 'tomsmith' in username field (type all at once)");
            Logger.Info("2. Type 'SuperSecretPassword!' in password field");
            Logger.Info("3. Verify only 2 Type actions recorded (not one per keystroke)");
            Logger.Info("\n??  Perform actions, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            // Validate debouncing
            var typeActions = scenario.Actions.Where(a => a.ActionType == "Type").ToList();
            Logger.Info($"\n? Type actions recorded: {typeActions.Count}");
            
            foreach (var action in typeActions)
            {
                Logger.Info($"  Type: {action.Locator} = \"{action.Value}\"");
            }
            
            if (typeActions.Count <= 2)
            {
                Logger.Info("? PASS: Input properly debounced");
            }
            else
            {
                Logger.Warning($"?? WARN: Expected ?2 type actions, got {typeActions.Count}");
            }
        }

        /// <summary>
        /// Test 3: Element Analyzer - Child Element Resolution
        /// Validates: Element Analyzer correctly identifies actionable parent
        /// </summary>
        public static async Task Test_ElementAnalyzer()
        {
            Logger.Info("\n=== Test 3: Element Analyzer ===");
            
            var recorder = new TestRecorder("ElementAnalyzerTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click on links that contain icons or spans");
            Logger.Info("2. Verify recorder identifies <a> tag, not inner <span>");
            Logger.Info("3. Check console logs for 'Element Analyzer' messages");
            Logger.Info("\n??  Click on various elements, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Actions Recorded:");
            foreach (var action in scenario.Actions.Where(a => a.ActionType == "Click"))
            {
                Logger.Info($"  Click: {action.Locator}");
                
                // Validate no child elements recorded
                if (action.Locator.Contains("span") || action.Locator.Contains("i.icon"))
                {
                    Logger.Warning("  ?? Child element detected - Element Analyzer may need review");
                }
                else
                {
                    Logger.Info("  ? Actionable element correctly identified");
                }
            }
        }

        /// <summary>
        /// Test 4: Smart Selector Priority
        /// Validates: Selector priority system (data-testid > id > name > aria-label)
        /// </summary>
        public static async Task Test_SmartSelectorPriority()
        {
            Logger.Info("\n=== Test 4: Smart Selector Priority ===");
            
            var recorder = new TestRecorder("SelectorPriorityTest", "Validation");
            
            Logger.Info("\n?? Creating test page with various selector types...");
            
            // For this test, you would need a test page with elements having different attributes
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/login");
            
            Logger.Info("\n?? VALIDATION CRITERIA:");
            Logger.Info("Priority 1: data-testid attributes");
            Logger.Info("Priority 2: Stable IDs (no timestamps)");
            Logger.Info("Priority 3: name attributes");
            Logger.Info("Priority 4: aria-label");
            Logger.Info("Priority 5+: role, text, CSS, XPath");
            Logger.Info("\n??  Interact with form, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Selector Quality Analysis:");
            
            int dataTestIdCount = 0;
            int idCount = 0;
            int nameCount = 0;
            int ariaCount = 0;
            int otherCount = 0;
            
            foreach (var action in scenario.Actions)
            {
                if (string.IsNullOrEmpty(action.Locator)) continue;
                
                if (action.Locator.Contains("data-testid") || action.Locator.Contains("data-test"))
                    dataTestIdCount++;
                else if (action.Locator.StartsWith("#"))
                    idCount++;
                else if (action.Locator.Contains("[name="))
                    nameCount++;
                else if (action.Locator.Contains("aria-label"))
                    ariaCount++;
                else
                    otherCount++;
            }
            
            Logger.Info($"  data-testid: {dataTestIdCount}");
            Logger.Info($"  ID: {idCount}");
            Logger.Info($"  name: {nameCount}");
            Logger.Info($"  aria-label: {ariaCount}");
            Logger.Info($"  other: {otherCount}");
        }

        /// <summary>
        /// Test 5: Dynamic Selector Avoidance
        /// Validates: No nth-child, no dynamic IDs, no framework classes
        /// </summary>
        public static async Task Test_DynamicSelectorAvoidance()
        {
            Logger.Info("\n=== Test 5: Dynamic Selector Avoidance ===");
            
            var recorder = new TestRecorder("DynamicAvoidanceTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click on multiple links and buttons");
            Logger.Info("2. After recording, we'll check for dynamic patterns");
            Logger.Info("\n??  Perform actions, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n?? Analyzing selectors for dynamic patterns...");
            
            var dynamicPatterns = new[]
            {
                "nth-child",
                "nth-of-type",
                @"\d{4,}",  // Long numbers
                "ng-scope",
                "mat-",
                "css-",
                "ember-",
                "react-"
            };
            
            int dynamicCount = 0;
            foreach (var action in scenario.Actions)
            {
                if (string.IsNullOrEmpty(action.Locator)) continue;
                
                foreach (var pattern in dynamicPatterns)
                {
                    if (action.Locator.Contains(pattern))
                    {
                        Logger.Warning($"  ?? Dynamic pattern found: {pattern} in {action.Locator}");
                        dynamicCount++;
                    }
                }
            }
            
            if (dynamicCount == 0)
            {
                Logger.Info("? PASS: No dynamic selectors detected!");
            }
            else
            {
                Logger.Warning($"?? Found {dynamicCount} potentially dynamic selector(s)");
            }
        }

        /// <summary>
        /// Test 6: Navigation and Wait Tracking
        /// Validates: Auto navigation detection, wait time calculation
        /// </summary>
        public static async Task Test_NavigationTracking()
        {
            Logger.Info("\n=== Test 6: Navigation and Wait Tracking ===");
            
            var recorder = new TestRecorder("NavigationTrackingTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click on any link to navigate to another page");
            Logger.Info("2. Wait 2-3 seconds on the new page");
            Logger.Info("3. Click back button");
            Logger.Info("4. Verify Navigate and Wait actions are auto-recorded");
            Logger.Info("\n??  Perform navigation, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Action Sequence:");
            foreach (var action in scenario.Actions)
            {
                Logger.Info($"  [{action.Timestamp}] {action.ActionType}: {action.Description}");
            }
            
            // Check for navigation actions
            var navActions = scenario.Actions.Where(a => a.ActionType == "Navigate").ToList();
            var waitActions = scenario.Actions.Where(a => a.ActionType == "Wait").ToList();
            
            Logger.Info($"\n?? Navigation: {navActions.Count} action(s)");
            Logger.Info($"?? Wait: {waitActions.Count} action(s)");
            
            if (navActions.Count > 0)
            {
                Logger.Info("? PASS: Navigation tracking working");
            }
        }

        /// <summary>
        /// Test 7: Action Normalization
        /// Validates: DOM events properly converted to automation actions
        /// </summary>
        public static async Task Test_ActionNormalization()
        {
            Logger.Info("\n=== Test 7: Action Normalization ===");
            
            var recorder = new TestRecorder("ActionNormalizationTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/checkboxes");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Check a checkbox ? Should record 'Check' action");
            Logger.Info("2. Uncheck it ? Should record 'Uncheck' action");
            Logger.Info("3. Press Enter in any field ? Should record 'PressEnter' action");
            Logger.Info("\n??  Perform various interactions, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Normalized Actions:");
            
            var normalizedActions = new[] { "Check", "Uncheck", "Pressenter", "Select", "Submit" };
            
            foreach (var action in scenario.Actions)
            {
                if (normalizedActions.Contains(action.ActionType))
                {
                    Logger.Info($"  ? {action.ActionType}: {action.Locator}");
                }
            }
            
            var checkActions = scenario.Actions.Count(a => a.ActionType == "Check" || a.ActionType == "Uncheck");
            if (checkActions > 0)
            {
                Logger.Info($"? PASS: Action normalization working ({checkActions} checkbox action(s))");
            }
        }

        /// <summary>
        /// Test 8: Element Highlighting
        /// Validates: Visual feedback during recording
        /// </summary>
        public static async Task Test_ElementHighlighting()
        {
            Logger.Info("\n=== Test 8: Element Highlighting ===");
            
            var recorder = new TestRecorder("HighlightingTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click on various elements");
            Logger.Info("2. Observe RED OUTLINE appearing on clicked elements");
            Logger.Info("3. Outline should fade after 2 seconds");
            Logger.Info("4. This confirms element highlighting is working");
            Logger.Info("\n?? WATCH FOR: Red outlines on clicked elements");
            Logger.Info("??  Click elements to see highlighting, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Element highlighting validation complete");
            Logger.Info("   (Visual confirmation required)");
        }

        /// <summary>
        /// Test 9: IFrame Detection and Recording
        /// Validates: Automatic iframe context management
        /// </summary>
        public static async Task Test_IFrameDetection()
        {
            Logger.Info("\n=== Test 9: IFrame Detection ===");
            
            var recorder = new TestRecorder("IFrameTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/iframe");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Click inside the iframe");
            Logger.Info("2. Type some text in the editor");
            Logger.Info("3. Verify 'SwitchToFrame' action is auto-added");
            Logger.Info("\n??  Interact with iframe content, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Action Sequence:");
            foreach (var action in scenario.Actions)
            {
                Logger.Info($"  [{action.Timestamp}] {action.ActionType}: {action.Locator}");
            }
            
            // Check for iframe actions
            var frameActions = scenario.Actions.Where(a => 
                a.ActionType == "SwitchToFrame" || 
                a.ActionType == "SwitchToDefaultContent").ToList();
            
            if (frameActions.Count > 0)
            {
                Logger.Info($"\n? PASS: IFrame detection working ({frameActions.Count} frame switch(es))");
            }
            else
            {
                Logger.Warning("?? No frame switch actions detected");
            }
        }

        /// <summary>
        /// Test 10: Form Submission and Keyboard Events
        /// Validates: Submit event, keydown events (Enter, Tab, Escape)
        /// </summary>
        public static async Task Test_FormSubmissionAndKeyboard()
        {
            Logger.Info("\n=== Test 10: Form Submission and Keyboard ===");
            
            var recorder = new TestRecorder("FormKeyboardTest", "Validation");
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/login");
            
            Logger.Info("\n?? TEST STEPS:");
            Logger.Info("1. Type in username field");
            Logger.Info("2. Press TAB to move to password field");
            Logger.Info("3. Type in password field");
            Logger.Info("4. Press ENTER to submit form");
            Logger.Info("5. Verify 'PressTab', 'PressEnter' or 'Submit' actions recorded");
            Logger.Info("\n??  Perform actions using keyboard, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            Logger.Info("\n? Action Sequence:");
            foreach (var action in scenario.Actions)
            {
                Logger.Info($"  {action.ActionType}: {action.Description}");
            }
            
            // Check for keyboard actions
            var keyboardActions = scenario.Actions.Where(a => 
                a.ActionType.StartsWith("Press") || 
                a.ActionType == "Submit").ToList();
            
            if (keyboardActions.Count > 0)
            {
                Logger.Info($"\n? PASS: Keyboard/Submit actions recorded ({keyboardActions.Count})");
            }
        }

        /// <summary>
        /// Test 11: Comprehensive Test - All Features
        /// Validates: All 10 features in a single test flow
        /// </summary>
        public static async Task Test_ComprehensiveScenario()
        {
            Logger.Info("\n=== Test 11: Comprehensive Scenario - ALL FEATURES ===");
            
            var recorder = new TestRecorder("ComprehensiveTest", "Validation");
            recorder.SetScenarioDescription("Comprehensive test covering all 10 enhanced features");
            recorder.AddTag("comprehensive");
            recorder.AddTag("all-features");
            
            var featureChecklist = new Dictionary<string, bool>
            {
                ["Event Capture"] = false,
                ["Element Analyzer"] = false,
                ["Smart Selectors"] = false,
                ["Input Debouncing"] = false,
                ["Action Normalization"] = false,
                ["Element Highlighting"] = false,
                ["IFrame Detection"] = false,
                ["Navigation Tracking"] = false
            };
            
            recorder.OnActionCaptured += (action) =>
            {
                // Track feature usage
                if (action.ActionType == "Type" && !featureChecklist["Input Debouncing"])
                {
                    featureChecklist["Input Debouncing"] = true;
                }
                if (action.ActionType == "SwitchToFrame")
                {
                    featureChecklist["IFrame Detection"] = true;
                }
                if (action.ActionType == "Navigate" && action.Timestamp > 0)
                {
                    featureChecklist["Navigation Tracking"] = true;
                }
                if (action.Locator.Contains("data-testid"))
                {
                    featureChecklist["Smart Selectors"] = true;
                }
            };
            
            await recorder.StartRecordingAsync("https://the-internet.herokuapp.com/");
            
            Logger.Info("\n?? COMPREHENSIVE TEST STEPS:");
            Logger.Info("1. Click on 'Form Authentication' (validates: Click, Element Analyzer, Smart Selector)");
            Logger.Info("2. Type in username (validates: Input Debouncing)");
            Logger.Info("3. Type in password");
            Logger.Info("4. Click Login button (validates: Action Normalization)");
            Logger.Info("5. Observe highlighting (validates: Element Highlighting)");
            Logger.Info("6. Check for navigation (validates: Navigation Tracking)");
            Logger.Info("\n??  Perform the test scenario, then press Enter to stop...");
            Console.ReadLine();
            
            var scenario = await recorder.StopRecordingAsync();
            
            // Add assertions
            recorder.AddAssertion("ElementVisible", ".flash", "true", "Verify flash message");
            
            Logger.Info("\n?? Feature Coverage Report:");
            Logger.Info("=" + new string('=', 50));
            
            foreach (var feature in featureChecklist)
            {
                var status = feature.Value ? "? USED" : "?? NOT DETECTED";
                Logger.Info($"  {feature.Key.PadRight(25)} {status}");
            }
            
            Logger.Info("\n?? Recording Statistics:");
            Logger.Info($"  Total Actions: {scenario.Actions.Count}");
            Logger.Info($"  Click Actions: {scenario.Actions.Count(a => a.ActionType == "Click")}");
            Logger.Info($"  Type Actions: {scenario.Actions.Count(a => a.ActionType == "Type")}");
            Logger.Info($"  IFrame Switches: {scenario.Actions.Count(a => a.ActionType == "SwitchToFrame")}");
            Logger.Info($"  Navigations: {scenario.Actions.Count(a => a.ActionType == "Navigate")}");
            Logger.Info($"  Assertions: {scenario.Assertions.Count}");
            
            // Save scenario
            var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
            var outputPath = Path.Combine("TestScenarios", "Validation", "Comprehensive_Test.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, json);
            
            Logger.Info($"\n? Scenario saved: {outputPath}");
        }

        /// <summary>
        /// Run all validation tests
        /// </summary>
        public static async Task RunAllValidationTests()
        {
            Logger.Info("?????????????????????????????????????????????????????????");
            Logger.Info("?   ENHANCED RECORDER - VALIDATION TEST SUITE          ?");
            Logger.Info("?????????????????????????????????????????????????????????");
            Logger.Info("");
            
            var tests = new (string TestName, Func<Task> TestMethod)[]
            {
                ("Basic Click Recording", Test_BasicClickRecording),
                ("Input Debouncing", Test_InputDebouncing),
                ("Element Analyzer", Test_ElementAnalyzer),
                ("Smart Selector Priority", Test_SmartSelectorPriority),
                ("Dynamic Selector Avoidance", Test_DynamicSelectorAvoidance),
                ("Navigation Tracking", Test_NavigationTracking),
                ("Form & Keyboard", Test_FormSubmissionAndKeyboard),
                ("Element Highlighting", Test_ElementHighlighting),
                ("IFrame Detection", Test_IFrameDetection),
                ("Comprehensive Scenario", Test_ComprehensiveScenario)
            };
            
            Logger.Info("Select test to run:");
            for (int i = 0; i < tests.Length; i++)
            {
                Logger.Info($"  {i + 1}. {tests[i].Item1}");
            }
            Logger.Info($"  {tests.Length + 1}. Run ALL tests");
            Logger.Info("");
            
            Console.Write("Enter choice: ");
            var choice = Console.ReadLine();
            
            if (int.TryParse(choice, out var index))
            {
                if (index > 0 && index <= tests.Length)
                {
                    // Run single test
                    await tests[index - 1].Item2();
                }
                else if (index == tests.Length + 1)
                {
                    // Run all tests
                    for (int i = 0; i < tests.Length; i++)
                    {
                        Logger.Info($"\n\n[{i + 1}/{tests.Length}] Running: {tests[i].Item1}");
                        Logger.Info(new string('-', 60));
                        await tests[i].Item2();
                        
                        if (i < tests.Length - 1)
                        {
                            Logger.Info("\n\nPress Enter to continue to next test...");
                            Console.ReadLine();
                        }
                    }
                    
                    Logger.Info("\n\n?????????????????????????????????????????????????????????");
                    Logger.Info("?   ALL VALIDATION TESTS COMPLETED                      ?");
                    Logger.Info("?????????????????????????????????????????????????????????");
                }
            }
        }
    }

    /// <summary>
    /// Program entry point for validation tests
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Logger.Info("Enhanced Recorder Validation Suite");
            Logger.Info("====================================\n");
            
            await RecorderValidationTests.RunAllValidationTests();
            
            Logger.Info("\n\n? Validation complete!");
            Logger.Info("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
