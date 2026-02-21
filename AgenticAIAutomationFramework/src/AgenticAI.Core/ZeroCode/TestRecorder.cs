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
            
            Logger.Info($"Starting test recording for: {_scenario.Name}");
            Logger.Info($"Opening browser in recording mode...");
            Logger.Info($"Navigate to: {startUrl}");
            Logger.Info("✅ AUTOMATIC ACTION RECORDING ENABLED");
            Logger.Info("   All clicks, typing, selections, and scrolls will be captured!");
            Logger.Info("   Simply interact with your application naturally.");
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

            // Set up action listeners for comprehensive recording
            SetupActionListeners();

            // Navigate to start URL
            await _page.GotoAsync(startUrl, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.Load,
                Timeout = 60000 
            });
            
            Logger.Info("✅ Browser opened and ready for recording!");
            Logger.Info("🎬 Perform your test actions - clicks, typing, selections will be captured automatically.");
            Logger.Info("✋ Click 'Stop Recording' when done.");
        }

        private async Task SetupNativeActionTracking()
        {
            if (_page == null) return;

            // Inject JavaScript to capture all user interactions
            _page.EvaluateAsync(@"
                (() => {
                    window.recordedActions = [];
                    
                    // Capture Click Events
                    document.addEventListener('click', (e) => {
                        const target = e.target;
                        const selector = getOptimalSelector(target);
                        window.recordedActions.push({
                            type: 'Click',
                            selector: selector,
                            tagName: target.tagName,
                            text: target.innerText?.substring(0, 50) || '',
                            timestamp: Date.now()
                        });
                    }, true);
                    
                    // Capture Input/Type Events
                    document.addEventListener('input', (e) => {
                        const target = e.target;
                        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                            const selector = getOptimalSelector(target);
                            window.recordedActions.push({
                                type: 'Type',
                                selector: selector,
                                value: target.value,
                                timestamp: Date.now()
                            });
                        }
                    }, true);
                    
                    // Capture Select Changes
                    document.addEventListener('change', (e) => {
                        const target = e.target;
                        if (target.tagName === 'SELECT') {
                            const selector = getOptimalSelector(target);
                            window.recordedActions.push({
                                type: 'Select',
                                selector: selector,
                                value: target.value,
                                timestamp: Date.now()
                            });
                        }
                    }, true);
                    
                    // Capture Scroll Events (throttled)
                    let scrollTimeout;
                    window.addEventListener('scroll', (e) => {
                        clearTimeout(scrollTimeout);
                        scrollTimeout = setTimeout(() => {
                            window.recordedActions.push({
                                type: 'Scroll',
                                selector: 'window',
                                value: `${window.scrollX},${window.scrollY}`,
                                timestamp: Date.now()
                            });
                        }, 500);
                    }, true);
                    
                    // Helper function to generate optimal selector
                    function getOptimalSelector(element) {
                        // Try ID first
                        if (element.id) {
                            return '#' + element.id;
                        }
                        
                        // Try name attribute
                        if (element.name) {
                            return `[name='${element.name}']`;
                        }
                        
                        // Try data-testid or data-test
                        if (element.dataset.testid) {
                            return `[data-testid='${element.dataset.testid}']`;
                        }
                        if (element.dataset.test) {
                            return `[data-test='${element.dataset.test}']`;
                        }
                        
                        // Try unique class combination
                        if (element.className && typeof element.className === 'string') {
                            const classes = element.className.trim().split(/\s+/).filter(c => c);
                            if (classes.length > 0) {
                                const selector = element.tagName.toLowerCase() + '.' + classes.join('.');
                                if (document.querySelectorAll(selector).length === 1) {
                                    return selector;
                                }
                            }
                        }
                        
                        // Generate XPath as last resort
                        let path = '';
                        let current = element;
                        while (current && current.nodeType === Node.ELEMENT_NODE) {
                            let index = 0;
                            let sibling = current.previousSibling;
                            while (sibling) {
                                if (sibling.nodeType === Node.ELEMENT_NODE && sibling.nodeName === current.nodeName) {
                                    index++;
                                }
                                sibling = sibling.previousSibling;
                            }
                            const tagName = current.nodeName.toLowerCase();
                            const pathIndex = index > 0 ? `[${index + 1}]` : '';
                            path = '/' + tagName + pathIndex + path;
                            current = current.parentNode;
                        }
                        return path || element.tagName.toLowerCase();
                    }
                })();
            ");

            // Record navigation
            _page.FrameNavigated += (sender, frame) =>
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
            
            // Collect JavaScript-recorded actions
            if (_page != null)
            {
                try
                {
                    var recordedActionsJson = await _page.EvaluateAsync<string>(@"
                        JSON.stringify(window.recordedActions || [])
                    ");
                    
                    if (!string.IsNullOrEmpty(recordedActionsJson))
                    {
                        var jsActions = JsonConvert.DeserializeObject<List<JsRecordedAction>>(recordedActionsJson);
                        
                        if (jsActions != null && jsActions.Count > 0)
                        {
                            Logger.Info($"Retrieved {jsActions.Count} actions from JavaScript recorder");
                            
                            // Deduplicate and convert JS actions to RecordedAction
                            var lastInput = new Dictionary<string, (string value, long timestamp)>();
                            
                            foreach (var jsAction in jsActions)
                            {
                                // For Type actions, only keep the final value for each input
                                if (jsAction.Type == "Type")
                                {
                                    var key = jsAction.Selector;
                                    if (!lastInput.ContainsKey(key) || jsAction.Timestamp > lastInput[key].timestamp)
                                    {
                                        lastInput[key] = (jsAction.Value, jsAction.Timestamp);
                                    }
                                    continue;
                                }
                                
                                // Add other actions directly
                                _recordedActions.Add(new RecordedAction
                                {
                                    ActionType = jsAction.Type,
                                    Locator = jsAction.Selector,
                                    Value = jsAction.Value,
                                    Description = GenerateActionDescription(jsAction),
                                    Timestamp = _recordedActions.Count
                                });
                            }
                            
                            // Add final input values
                            foreach (var kvp in lastInput)
                            {
                                _recordedActions.Add(new RecordedAction
                                {
                                    ActionType = "Type",
                                    Locator = kvp.Key,
                                    Value = kvp.Value.value,
                                    Description = $"Type '{kvp.Value.value}' into {kvp.Key}",
                                    Timestamp = _recordedActions.Count
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to retrieve JavaScript-recorded actions: {ex.Message}");
                }
            }
            
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
        
        private string GenerateActionDescription(JsRecordedAction action)
        {
            return action.Type switch
            {
                "Click" => $"Click on {action.TagName} {(string.IsNullOrEmpty(action.Text) ? action.Selector : $"'{action.Text.Substring(0, Math.Min(30, action.Text.Length))}'")}" ,
                "Type" => $"Type '{action.Value}' into {action.Selector}",
                "Select" => $"Select '{action.Value}' from {action.Selector}",
                "Scroll" => $"Scroll to position {action.Value}",
                _ => $"{action.Type} on {action.Selector}"
            };
        }
        
        // Helper class for JavaScript action deserialization
        private class JsRecordedAction
        {
            [JsonProperty("type")]
            public string Type { get; set; } = "";
            
            [JsonProperty("selector")]
            public string Selector { get; set; } = "";
            
            [JsonProperty("value")]
            public string Value { get; set; } = "";
            
            [JsonProperty("tagName")]
            public string TagName { get; set; } = "";
            
            [JsonProperty("text")]
            public string Text { get; set; } = "";
            
            [JsonProperty("timestamp")]
            public long Timestamp { get; set; }
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
