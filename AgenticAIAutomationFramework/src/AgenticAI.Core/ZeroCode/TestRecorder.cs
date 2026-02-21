using AgenticAI.Core.Configuration;
using AgenticAI.Core.Logging;
using AgenticAI.Core.ZeroCode.Models;
using Microsoft.Playwright;
using Newtonsoft.Json;
using System.Text.Json;
using System.IO;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Records user interactions and generates test scenarios
    /// Zero-code test creation by recording user actions
    /// </summary>
    public class TestRecorder
    {
        private readonly List<RecordedAction> _recordedActions;
        private readonly TestScenario _scenario;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private readonly FrameworkConfiguration _config;

        public TestRecorder(string scenarioName, string module = "Default")
        {
            _recordedActions = new List<RecordedAction>();
            _scenario = new TestScenario
            {
                ScenarioId = Guid.NewGuid().ToString(),
                Name = scenarioName,
                Module = module,
                Description = "",
                StartUrl = "",
                Tags = new List<string>(),
                Actions = new List<RecordedAction>(),
                Assertions = new List<Assertion>(),
                CreatedAt = DateTime.Now,
                ModifiedAt = null
            };
            _config = ConfigurationManager.Instance.FrameworkConfig;
        }

        /// <summary>
        /// Start recording user actions on the application
        /// </summary>
        public async Task StartRecordingAsync(string startUrl)
        {
            _scenario.StartUrl = startUrl;
            
            Logger.Info($"?? Starting test recording for: {_scenario.Name}");
            Logger.Info($"?? Opening browser in recording mode...");
            Logger.Info($"?? Navigate to: {startUrl}");
            Logger.Info("");
            Logger.Info("? AUTOMATIC RECORDING ENABLED");
            Logger.Info("   All clicks, typing, and selections will be captured automatically");
            Logger.Info("   Perform your test actions in the browser");
            Logger.Info("   Close the browser or call StopRecording when done");
            Logger.Info("");

            _playwright = await Playwright.CreateAsync();
            
            // Use Chromium for best recording support
            var browserType = _playwright.Chromium;

            _browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 1000, // Slow down significantly for recording
                Args = new[] { "--start-maximized" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                RecordVideoDir = _config.VideoPath
            });

            _page = await context.NewPageAsync();

            // Set up native Playwright action tracking BEFORE any navigation
            await SetupNativeActionTracking();

            // Navigate to start URL
            await _page.GotoAsync(startUrl, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.Load,
                Timeout = 60000 
            });
            
            Logger.Info("? Browser opened and recorder is active!");
            Logger.Info("?? Perform your test actions now...");
        }

        private async Task SetupNativeActionTracking()
        {
            if (_page == null) return;

            // Expose function for recording actions from the page - THIS MUST BE DONE FIRST
            await _page.ExposeFunctionAsync("__playwrightRecordAction", (object eventData) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(eventData);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var type = root.GetProperty("type").GetString() ?? "";
                    var selector = root.GetProperty("selector").GetString() ?? "";
                    
                    if (type == "click")
                    {
                        var text = root.TryGetProperty("text", out var t) ? t.GetString() : "";
                        var action = new RecordedAction
                        {
                            ActionType = "Click",
                            Locator = selector,
                            Description = $"Click on \"{text}\"",
                            Timestamp = _recordedActions.Count
                        };
                        _recordedActions.Add(action);
                        Logger.Info($"? Click recorded: {selector}");
                    }
                    else if (type == "fill")
                    {
                        var value = root.GetProperty("value").GetString() ?? "";
                        var action = new RecordedAction
                        {
                            ActionType = "Type",
                            Locator = selector,
                            Value = value,
                            Description = $"Type \"{value}\" into {selector}",
                            Timestamp = _recordedActions.Count
                        };
                        _recordedActions.Add(action);
                        Logger.Info($"? Type recorded: {selector} = {value}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Record action error: {ex.Message}");
                }
                
                return Task.CompletedTask;
            });

            // Use AddInitScript to inject tracking on EVERY page load/navigation
            // This is the KEY - it runs before ANY page code, on EVERY navigation
            await _page.AddInitScriptAsync(@"
(function() {
    // Mark as injected
    if (window.__recorderReady) return;
    window.__recorderReady = true;
    
    console.log('?? Recorder Active - Ready to capture actions');
    
    // Helper to generate selector
    window.__getSelector = function(el) {
        if (!el) return '';
        
        // Priority 1: data-test attributes
        const testAttrs = ['data-testid', 'data-test-id', 'data-test', 'data-qa'];
        for (let attr of testAttrs) {
            const val = el.getAttribute(attr);
            if (val) return `[${attr}=""${val}""]`;
        }
        
        // Priority 2: ID
        if (el.id) return '#' + el.id;
        
        // Priority 3: name
        if (el.name) return `[name=""${el.name}""]`;
        
        // Priority 4: placeholder
        if (el.placeholder) return `[placeholder=""${el.placeholder}""]`;
        
        // Priority 5: aria-label
        const ariaLabel = el.getAttribute('aria-label');
        if (ariaLabel) return `[aria-label=""${ariaLabel}""]`;
        
        // Fallback: tag with classes
        let selector = el.tagName.toLowerCase();
        if (el.className && typeof el.className === 'string') {
            const classes = el.className.trim().split(/\s+/).filter(c => c && !c.match(/^ng-|mat-|_/));
            if (classes.length > 0 && classes.length < 4) {
                selector += '.' + classes.join('.');
            }
        }
        
        return selector;
    };
    
    // Track all clicks
    document.addEventListener('mousedown', function(e) {
        setTimeout(function() {
            const el = e.target.closest('button, a, input[type=submit], input[type=button], [role=button]') || e.target;
            const selector = window.__getSelector(el);
            const text = el.textContent?.trim().substring(0, 50) || '';
            
            // Send to Playwright
            if (window.__playwrightRecordAction) {
                window.__playwrightRecordAction({
                    type: 'click',
                    selector: selector,
                    text: text,
                    timestamp: Date.now()
                });
                console.log('?? Click recorded:', selector);
            }
        }, 100);
    }, true);
    
    // Track all input changes
    let inputTimers = {};
    document.addEventListener('input', function(e) {
        const el = e.target;
        if (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA') return;
        
        const selector = window.__getSelector(el);
        const value = el.value;
        
        clearTimeout(inputTimers[selector]);
        inputTimers[selector] = setTimeout(function() {
            if (window.__playwrightRecordAction) {
                window.__playwrightRecordAction({
                    type: 'fill',
                    selector: selector,
                    value: value,
                    timestamp: Date.now()
                });
                console.log('?? Type recorded:', selector, '=', value);
            }
        }, 1000);
    }, true);
    
    console.log('? Recorder script initialized - perform actions now');
})();
");

            Logger.Info("? Native action tracking enabled using AddInitScript");
        }

        /// <summary>
        /// Stop recording and save the scenario
        /// </summary>
        public async Task<TestScenario> StopRecordingAsync()
        {
            Logger.Info("Stopping test recording...");
            
            // Copy recorded actions to scenario
            _scenario.Actions = new List<RecordedAction>(_recordedActions);
            
            // Ensure all collections are initialized
            if (_scenario.Assertions == null)
            {
                _scenario.Assertions = new List<Assertion>();
            }
            
            if (_scenario.Tags == null)
            {
                _scenario.Tags = new List<string>();
            }

            // Set timestamps
            _scenario.ModifiedAt = DateTime.Now;
            
            if (_page != null)
            {
                try
                {
                    await _page.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error closing page: {ex.Message}");
                }
            }
            
            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error closing browser: {ex.Message}");
                }
            }

            _playwright?.Dispose();

            Logger.Info($"Recording completed. Captured {_recordedActions.Count} actions.");
            
            return _scenario;
        }

        /// <summary>
        /// Add a manual action to the recording
        /// </summary>
        public void AddAction(string actionType, string locator, string? value = null, string? description = null)
        {
            var action = new RecordedAction
            {
                ActionType = actionType,
                Locator = locator,
                Value = value,
                Description = description ?? $"{actionType} on {locator}",
                Timestamp = _recordedActions.Count
            };
            
            _recordedActions.Add(action);
            Logger.Info($"Action recorded: {action.Description}");
        }

        /// <summary>
        /// Add an assertion to the scenario
        /// </summary>
        public void AddAssertion(string type, string locator, string? expectedValue = null, string? description = null)
        {
            var assertion = new Assertion
            {
                Type = type,
                Locator = locator,
                ExpectedValue = expectedValue,
                Description = description ?? $"Verify {type}"
            };
            
            _scenario.Assertions.Add(assertion);
            Logger.Info($"Assertion added: {assertion.Description}");
        }

        public void SetScenarioDescription(string description)
        {
            _scenario.Description = description;
        }

        public void AddTag(string tag)
        {
            if (!_scenario.Tags.Contains(tag))
            {
                _scenario.Tags.Add(tag);
            }
        }
    }
}
