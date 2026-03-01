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
        
        /// <summary>
        /// Event fired when a new action is captured
        /// </summary>
        public event Action<RecordedAction>? OnActionCaptured;

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
        /// Returns the number of currently recorded actions
        /// </summary>
        public int GetRecordedActionCount() => _recordedActions.Count;

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
                SlowMo = 50,
                Args = new[] { "--start-maximized", "--disable-blink-features=AutomationControlled" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport,
                RecordVideoDir = _config.VideoPath
            });

            _page = await context.NewPageAsync();

            // Set up action listeners for comprehensive recording
            await SetupActionListeners();

            // Capture console logs from the browser for debugging
            _page.Console += (sender, msg) =>
            {
                if (msg.Text.Contains("??")) 
                    Logger.Debug($"[BROWSER] {msg.Text}");
                else if (msg.Type == "error")
                    Logger.Error($"[BROWSER ERROR] {msg.Text}");
            };

            // Navigate to start URL
            await _page.GotoAsync(startUrl, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.Load,
                Timeout = 60000 
            });
            
            Logger.Info("✅ Browser opened and ready for recording!");
            Logger.Info("🎬 Perform your test actions - clicks, typing, selections will be captured automatically.");
            Logger.Info("✋ Click 'Stop Recording' when done.");
            
            // Automatically capture the initial navigation as the first action
            var navigateAction = new RecordedAction
            {
                ActionType = "Navigate",
                Locator = startUrl,
                Value = startUrl,
                Description = $"Navigate to {startUrl}",
                Timestamp = 0
            };
            _recordedActions.Add(navigateAction);
            Logger.Info($"📌 Captured Navigate: {startUrl}");
            OnActionCaptured?.Invoke(navigateAction);
        }

        private async Task SetupActionListeners()
        {
            if (_page == null) return;

            // Expose a function in the browser to call when actions occur
            await _page.ExposeFunctionAsync("__playwrightRecordAction", (JsonElement eventData) =>
            {
                try
                {
                    string type = eventData.GetProperty("type").GetString() ?? "";
                    string selector = eventData.GetProperty("selector").GetString() ?? "";
                    
                    RecordedAction action = new RecordedAction
                    {
                        ActionType = "Click",
                        Locator = selector,
                        Timestamp = _recordedActions.Count
                    };

                    switch (type.ToLower())
                    {
                        case "click":
                            action.ActionType = "Click";
                            string text = eventData.TryGetProperty("text", out var t) ? t.GetString() : "";
                            action.Description = $"Click on {selector}" + (string.IsNullOrEmpty(text) ? "" : $" (\"{text}\")");
                            break;
                        case "fill":
                            action.ActionType = "Type";
                            action.Value = eventData.GetProperty("value").GetString();
                            action.Description = $"Type \"{action.Value}\" into {selector}";
                            break;
                        case "select":
                            action.ActionType = "Select";
                            action.Value = eventData.GetProperty("value").GetString();
                            string optionText = eventData.TryGetProperty("text", out var ot) ? ot.GetString() : "";
                            action.Description = $"Select \"{optionText}\" from {selector}";
                            break;
                        case "check":
                            action.ActionType = "Check";
                            action.Description = $"Check {selector}";
                            break;
                        case "uncheck":
                            action.ActionType = "Uncheck";
                            action.Description = $"Uncheck {selector}";
                            break;
                    }

                    // Deduplicate rapid "fill" events for the same selector
                    if (type == "fill" && _recordedActions.Count > 0)
                    {
                        var last = _recordedActions.Last();
                        if (last.ActionType == "Type" && last.Locator == selector)
                        {
                            last.Value = action.Value;
                            last.Description = action.Description;
                            return Task.CompletedTask;
                        }
                    }

                    _recordedActions.Add(action);
                    Logger.Info($"?? Captured {action.ActionType}: {selector}");
                    
                    // Update action count in browser overlay
                    try
                    {
                        if (_page != null)
                        {
                            _ = _page.EvaluateAsync($"if (window.__updateRecorderCount) window.__updateRecorderCount({_recordedActions.Count});");
                        }
                    }
                    catch { /* Ignore if page is closed or function doesn't exist */ }
                    
                    // Trigger event for real-time updates
                    OnActionCaptured?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Record action error: {ex.Message}");
                }
                
                return Task.CompletedTask;
            });

            // Load recorder control panel overlay
            var overlayScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ZeroCode", "Resources", "RecorderOverlay.js");
            if (File.Exists(overlayScriptPath))
            {
                var overlayScript = await File.ReadAllTextAsync(overlayScriptPath);
                await _page.AddInitScriptAsync(overlayScript);
                Logger.Info("✓ Recorder control panel loaded in browser");
            }
            
            // Use AddInitScript to inject tracking on EVERY page load/navigation
            // This runs before ANY page code, on EVERY navigation
            await _page.AddInitScriptAsync(@"
(function() {
    // Remove the __recorderReady guard so re-navigation re-registers listeners
    
    window.__getSelector = function(el) {
        if (!el) return '';
        
        var testAttrs = ['data-testid', 'data-test-id', 'data-test', 'data-qa'];
        for (var i = 0; i < testAttrs.length; i++) {
            var val = el.getAttribute(testAttrs[i]);
            if (val) return '[' + testAttrs[i] + '=""' + val + '""]';
        }
        
        if (el.id && !el.id.match(/\d{4,}/)) return '#' + el.id;
        if (el.name) return '[name=""' + el.name + '""]';
        if (el.placeholder) return '[placeholder=""' + el.placeholder + '""]';
        
        var ariaLabel = el.getAttribute('aria-label');
        if (ariaLabel) return '[aria-label=""' + ariaLabel + '""]';
        
        if ((el.tagName === 'BUTTON' || el.tagName === 'A') && el.innerText && el.innerText.trim()) {
            var txt = el.innerText.trim();
            if (txt.length > 0 && txt.length < 50) return 'text=""' + txt + '""';
        }

        var selector = el.tagName.toLowerCase();
        if (el.className && typeof el.className === 'string') {
            var classes = el.className.split(/\s+/).filter(function(c) { return c && !c.match(/^(ng-|mat-|css-|_)/); });
            if (classes.length > 0) selector += '.' + classes[0];
        }
        
        return selector;
    };
    
    document.addEventListener('click', function(e) {
        var el = (e.target.closest ? e.target.closest('button, a, input[type=submit], input[type=button], [role=button]') : null) || e.target;
        if (!el) return;
        
        var selector = window.__getSelector(el);
        var text = (el.innerText ? el.innerText.trim().substring(0, 30) : '') || '';
        
        if (typeof window.__playwrightRecordAction === 'function') {
            window.__playwrightRecordAction({
                type: 'click',
                selector: selector,
                text: text,
                tagName: el.tagName
            });
            console.log('RECORDER: Click captured on ' + selector);
        } else {
            console.error('RECORDER: __playwrightRecordAction not found!');
        }
    }, true);
    
    document.addEventListener('input', function(e) {
        var el = e.target;
        if (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA') return;
        if (el.type === 'checkbox' || el.type === 'radio') return;
        
        var selector = window.__getSelector(el);
        if (typeof window.__playwrightRecordAction === 'function') {
            window.__playwrightRecordAction({
                type: 'fill',
                selector: selector,
                value: el.value
            });
        }
    }, true);

    document.addEventListener('change', function(e) {
        var el = e.target;
        var selector = window.__getSelector(el);
        
        if (el.tagName === 'SELECT') {
            var option = el.options[el.selectedIndex];
            if (typeof window.__playwrightRecordAction === 'function') {
                window.__playwrightRecordAction({
                    type: 'select',
                    selector: selector,
                    value: option.value,
                    text: option.text
                });
                console.log('RECORDER: Select captured on ' + selector + ' = ' + option.value);
            }
        } else if (el.tagName === 'INPUT' && (el.type === 'checkbox' || el.type === 'radio')) {
            if (typeof window.__playwrightRecordAction === 'function') {
                // Build a value-specific selector to avoid strict mode violations on duplicate name attributes
                var specificSelector = selector;
                if (el.name && el.value) {
                    specificSelector = '[name=' + el.name + '][value=' + el.value + ']';
                } else if (el.id) {
                    specificSelector = '#' + el.id;
                }
                window.__playwrightRecordAction({
                    type: el.checked ? 'check' : 'uncheck',
                    selector: specificSelector,
                    value: el.value
                });
                console.log('RECORDER: ' + (el.checked ? 'Check' : 'Uncheck') + ' captured on ' + specificSelector);
            }
        }
    }, true);
    
    console.log('RECORDER: Init script loaded, listeners attached');
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
            
            // Sync actions to scenario before returning
            _scenario.Actions = _recordedActions;
            
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
