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
        
        // IFrame context tracking
        private readonly IFrameContext _frameContext;
        private IFrameDetector? _frameDetector;
        
        // Track last action time for duplicate prevention
        private DateTime _lastActionTime = DateTime.MinValue;
        private string _lastActionType = "";
        
        /// <summary>
        /// Event fired when a new action is captured
        /// </summary>
        public event Action<RecordedAction>? OnActionCaptured;
        
        /// <summary>
        /// Event fired when recorder needs a placeholder name for a typed value
        /// Arguments: (fieldName, actualValue) => placeholderName
        /// </summary>
        public event Func<string, string, Task<string>>? OnPlaceholderNeeded;

        public TestRecorder(string scenarioName, string module = "Default")
        {
            _recordedActions = new List<RecordedAction>();
            _frameContext = new IFrameContext();
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

            // Initialize iframe detector
            _frameDetector = new IFrameDetector(_frameContext);

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
                    // Extract enhanced action data
                    string eventType = eventData.GetProperty("eventType").GetString() ?? "";
                    string actionType = eventData.GetProperty("actionType").GetString() ?? "";
                    string selector = eventData.GetProperty("selector").GetString() ?? "";
                    
                    // Get iframe context
                    bool inIFrame = false;
                    string? iframeSelector = null;
                    if (eventData.TryGetProperty("iframe", out var iframeData))
                    {
                        inIFrame = iframeData.TryGetProperty("inIFrame", out var iframeFlag) && iframeFlag.GetBoolean();
                        iframeSelector = iframeData.TryGetProperty("iframeSelector", out var iframeSel) ? iframeSel.GetString() : null;
                    }
                    
                    // Handle navigation events
                    if (eventType == "navigation")
                    {
                        string toUrl = eventData.TryGetProperty("toUrl", out var url) ? url.GetString() ?? "" : "";
                        int waitTime = eventData.TryGetProperty("waitTime", out var wait) ? wait.GetInt32() : 0;
                        
                        // Add wait action if needed
                        if (waitTime > 0)
                        {
                            var waitAction = new RecordedAction
                            {
                                ActionType = "Wait",
                                Value = waitTime.ToString(),
                                Description = $"Wait {waitTime} seconds",
                                Timestamp = _recordedActions.Count
                            };
                            _recordedActions.Add(waitAction);
                            OnActionCaptured?.Invoke(waitAction);
                        }
                        
                        // Add navigation action
                        var navAction = new RecordedAction
                        {
                            ActionType = "Navigate",
                            Locator = toUrl,
                            Value = toUrl,
                            Description = $"Navigate to {toUrl}",
                            Timestamp = _recordedActions.Count
                        };
                        _recordedActions.Add(navAction);
                        Logger.Info($"📌 Captured Navigation: {toUrl}");
                        OnActionCaptured?.Invoke(navAction);
                        return Task.CompletedTask;
                    }
                    
                    // Get value and text
                    string? value = eventData.TryGetProperty("value", out var val) ? val.GetString() : null;
                    string? text = null;
                    if (eventData.TryGetProperty("element", out var elemData))
                    {
                        text = elemData.TryGetProperty("text", out var txt) ? txt.GetString() : null;
                    }
                    if (eventData.TryGetProperty("text", out var directText))
                    {
                        text = directText.GetString();
                    }
                    
                    RecordedAction action = new RecordedAction
                    {
                        ActionType = CapitalizeFirst(actionType),
                        Locator = selector,
                        Value = value,
                        Timestamp = _recordedActions.Count
                    };
                    
                    // ALWAYS infer parameter names for Type and Input actions (smart recording)
                    // This enables both direct execution AND data-driven testing automatically
                    if ((actionType.ToLower() == "type" || actionType.ToLower() == "input") && !string.IsNullOrWhiteSpace(value))
                    {
                        string parameterName = GeneratePlaceholderName(selector, value);
                        
                        // Store BOTH the actual value (for direct execution) AND parameter name (for data-driven)
                        action.Value = value; // Keep actual value for non-data-driven execution
                        action.Metadata["ParameterName"] = parameterName; // Store parameter name for data-driven execution
                        
                        Logger.Info($"📝 Captured {actionType}: {value} (Parameter: {parameterName})");
                    }

                    // Handle iframe context
                    if (inIFrame && !string.IsNullOrEmpty(iframeSelector))
                    {
                        // Check if we need to add iframe switch action
                        if (!_frameContext.IsInSameFrame(iframeSelector))
                        {
                            // Add switch to iframe action
                            var switchAction = new RecordedAction
                            {
                                ActionType = "SwitchToFrame",
                                Locator = iframeSelector,
                                Description = $"Switch to iframe: {iframeSelector}",
                                Timestamp = _recordedActions.Count
                            };
                            _recordedActions.Add(switchAction);
                            _frameContext.EnterFrame(iframeSelector, iframeSelector);
                            Logger.Info($"🔄 Auto-added iframe switch: {iframeSelector}");
                            OnActionCaptured?.Invoke(switchAction);
                        }
                    }
                    else if (!inIFrame && _frameContext.IsInFrame)
                    {
                        // Element is in main content but we're in iframe - switch out
                        var defaultAction = new RecordedAction
                        {
                            ActionType = "SwitchToDefaultContent",
                            Locator = "",
                            Description = "Switch to default content",
                            Timestamp = _recordedActions.Count
                        };
                        _recordedActions.Add(defaultAction);
                        _frameContext.SwitchToDefaultContent();
                        Logger.Info("🔄 Auto-added switch to default content");
                        OnActionCaptured?.Invoke(defaultAction);
                    }

                    // Generate description based on action type
                    switch (actionType.ToLower())
                    {
                        case "click":
                            action.Description = $"Click on {selector}" + (string.IsNullOrEmpty(text) ? "" : $" (\"{text}\")");
                            break;
                        case "dblclick":
                            action.Description = $"Double-click on {selector}";
                            break;
                        case "type":
                            action.Description = $"Type \"{action.Value}\" into {selector}";
                            break;
                        case "select":
                            action.Description = $"Select \"{text ?? value}\" from {selector}";
                            break;
                        case "check":
                            action.Description = $"Check {selector}";
                            break;
                        case "uncheck":
                            action.Description = $"Uncheck {selector}";
                            break;
                        case "pressenter":
                            action.Description = $"Press Enter on {selector}";
                            break;
                        case "presstab":
                            action.Description = $"Press Tab on {selector}";
                            break;
                        case "pressescape":
                            action.Description = $"Press Escape on {selector}";
                            break;
                        case "submit":
                            action.Description = $"Submit form {selector}";
                            break;
                        case "upload":
                            action.Description = $"Upload file to {selector}";
                            break;
                        default:
                            action.Description = $"{action.ActionType} on {selector}";
                            break;
                    }

                    // Deduplicate rapid "type" events for the same selector (handled by debouncing in JS)
                    if (actionType == "type" && _recordedActions.Count > 0)
                    {
                        var last = _recordedActions.Last();
                        if (last.ActionType == "Type" && last.Locator == selector)
                        {
                            // Update existing action instead of adding duplicate
                            last.Value = action.Value;
                            last.Description = action.Description;
                            last.Timestamp = _recordedActions.Count - 1;
                            
                            // Update overlay count
                            if (_page != null)
                            {
                                _ = _page.EvaluateAsync($"if (window.__updateRecorderCount) window.__updateRecorderCount({_recordedActions.Count});");
                            }
                            return Task.CompletedTask;
                        }
                    }

                    // CRITICAL FIX: Deduplicate browser actions (refresh, back, forward)
                    // These can fire multiple times due to browser events
                    if (actionType == "refresh" || actionType == "back" || actionType == "forward")
                    {
                        var timeSinceLastAction = DateTime.Now - _lastActionTime;
                        
                        // If same browser action within 2 seconds, it's a duplicate
                        if (_lastActionType == actionType && timeSinceLastAction.TotalSeconds < 2)
                        {
                            Logger.Warning($"⚠️ Duplicate {actionType} action suppressed (received {timeSinceLastAction.TotalMilliseconds:F0}ms after previous)");
                            return Task.CompletedTask; // Don't add duplicate
                        }
                        
                        // Update tracking
                        _lastActionTime = DateTime.Now;
                        _lastActionType = actionType;
                    }

                    _recordedActions.Add(action);
                    var contextInfo = _frameContext.IsInFrame ? $" [in iframe: {_frameContext.CurrentFrame?.Selector}]" : "";
                    Logger.Info($"📌 Captured {action.ActionType}: {selector}{contextInfo}");
                    
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

            // NOTE: Stop Recording overlay removed - users should stop from WebUI
            // The overlay was causing issues:
            // 1. Button clicks were being recorded as test steps
            // 2. Button didn't reliably stop recording
            // 3. Interfered with test recording process
            
            // Load the enhanced recorder script with advanced features
            var enhancedRecorderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ZeroCode", "Resources", "recorder.enhanced.js");
            if (File.Exists(enhancedRecorderPath))
            {
                var enhancedRecorderScript = await File.ReadAllTextAsync(enhancedRecorderPath);
                await _page.AddInitScriptAsync(enhancedRecorderScript);
                Logger.Info("✅ Enhanced recorder loaded with smart selectors, Shadow DOM, and iframe support");
            }
            else
            {
                // Fallback to inline enhanced recorder if file not found
                Logger.Warning("Enhanced recorder script not found, using inline fallback");
                await _page.AddInitScriptAsync(@"
(function() {
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

    window.__isInIFrame = function() {
        try {
            return window.self !== window.top;
        } catch (e) {
            return true;
        }
    };

    window.__getIFrameSelector = function() {
        if (!window.__isInIFrame()) return null;
        
        try {
            var iframes = window.parent.document.querySelectorAll('iframe, frame');
            for (var i = 0; i < iframes.length; i++) {
                if (iframes[i].contentWindow === window.self) {
                    var iframe = iframes[i];
                    if (iframe.id) return '#' + iframe.id;
                    if (iframe.name) return 'iframe[name=""' + iframe.name + '""]';
                    if (iframe.src) {
                        var srcPart = iframe.src.substring(iframe.src.lastIndexOf('/') + 1).split('?')[0];
                        return 'iframe[src*=""' + srcPart + '""]';
                    }
                    return 'iframe:nth-of-type(' + (i + 1) + ')';
                }
            }
        } catch (e) {
            // Cross-origin restriction
        }
        return null;
    };
    
    document.addEventListener('click', function(e) {
        var el = (e.target.closest ? e.target.closest('button, a, input[type=submit], input[type=button], [role=button]') : null) || e.target;
        if (!el) return;
        
        var selector = window.__getSelector(el);
        var text = (el.innerText ? el.innerText.trim().substring(0, 30) : '') || '';
        var inIFrame = window.__isInIFrame();
        var iframeSelector = inIFrame ? window.__getIFrameSelector() : null;
        
        if (typeof window.__playwrightRecordAction === 'function') {
            window.__playwrightRecordAction({
                type: 'click',
                selector: selector,
                text: text,
                tagName: el.tagName,
                inIFrame: inIFrame,
                iframeSelector: iframeSelector
            });
            console.log('RECORDER: Click captured on ' + selector + (inIFrame ? ' [in iframe: ' + iframeSelector + ']' : ''));
        } else {
            console.error('RECORDER: __playwrightRecordAction not found!');
        }
    }, true);
    
    document.addEventListener('input', function(e) {
        var el = e.target;
        if (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA') return;
        if (el.type === 'checkbox' || el.type === 'radio') return;
        
        var selector = window.__getSelector(el);
        var inIFrame = window.__isInIFrame();
        var iframeSelector = inIFrame ? window.__getIFrameSelector() : null;
        
        if (typeof window.__playwrightRecordAction === 'function') {
            window.__playwrightRecordAction({
                type: 'fill',
                selector: selector,
                value: el.value,
                inIFrame: inIFrame,
                iframeSelector: iframeSelector
            });
        }
    }, true);

    document.addEventListener('change', function(e) {
        var el = e.target;
        var selector = window.__getSelector(el);
        var inIFrame = window.__isInIFrame();
        var iframeSelector = inIFrame ? window.__getIFrameSelector() : null;
        
        if (el.tagName === 'SELECT') {
            var option = el.options[el.selectedIndex];
            if (typeof window.__playwrightRecordAction === 'function') {
                window.__playwrightRecordAction({
                    type: 'select',
                    selector: selector,
                    value: option.value,
                    text: option.text,
                    inIFrame: inIFrame,
                    iframeSelector: iframeSelector
                });
                console.log('RECORDER: Select captured on ' + selector + ' = ' + option.value + (inIFrame ? ' [in iframe: ' + iframeSelector + ']' : ''));
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
                    value: el.value,
                    inIFrame: inIFrame,
                    iframeSelector: iframeSelector
                });
                console.log('RECORDER: ' + (el.checked ? 'Check' : 'Uncheck') + ' captured on ' + specificSelector + (inIFrame ? ' [in iframe: ' + iframeSelector + ']' : ''));
            }
        }
    }, true);
    
    console.log('RECORDER: Init script loaded, listeners attached' + (window.__isInIFrame() ? ' [INSIDE IFRAME]' : ' [MAIN FRAME]'));
})();
");
            }
            
            Logger.Info("✅ Enhanced action tracking enabled with element analysis and smart selectors");
        }

        /// <summary>
        /// Stop recording and save the scenario
        /// </summary>
        public async Task<TestScenario> StopRecordingAsync()
        {
            Logger.Info("Stopping test recording...");
            
            // Flush any pending clicks in the browser (NOT stop recording again!)
            if (_page != null)
            {
                try
                {
                    Logger.Info("Flushing pending recorder clicks...");
                    // Call the JavaScript flush function (NOT __stopRecording to avoid loop)
                    await _page.EvaluateAsync(@"() => { 
                        if (window.RecorderController && window.RecorderController.clickTimeout) {
                            clearTimeout(window.RecorderController.clickTimeout);
                            if (window.RecorderController.pendingClickElement) {
                                window.RecorderController.recordClick(window.RecorderController.pendingClickElement);
                            }
                        }
                    }");
                    
                    // Give a small delay to ensure all actions are sent
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error flushing recorder actions: {ex.Message}");
                }
            }
            
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
            
            // Add delay to allow any browser dialogs to be displayed
            Logger.Info("Waiting for browser to complete any pending operations...");
            await Task.Delay(1000);
            
            if (_page != null)
            {
                try
                {
                    Logger.Info("Closing browser page...");
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
        public void AddAssertion(string type, string locator, string? expectedValue = null, string? description = null, int? executeAfterActionIndex = null)
        {
            var assertion = new Assertion
            {
                Type = type,
                Locator = locator,
                ExpectedValue = expectedValue,
                Description = description ?? $"Verify {type}",
                ExecuteAfterActionIndex = executeAfterActionIndex
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

        /// <summary>
        /// Detect iframe context for an element and add switch actions if needed
        /// </summary>
        private async Task<bool> HandleIFrameContextAsync(string elementSelector)
        {
            if (_frameDetector == null || _page == null)
                return false;

            try
            {
                // Detect if element is in an iframe
                var detection = await _frameDetector.DetectIFrameAsync(
                    elementSelector,
                    async (script) => await _page.EvaluateAsync<JsonElement>(script)
                );

                if (!detection.ElementFound)
                {
                    Logger.Debug($"Element '{elementSelector}' not found in any frame");
                    return false;
                }

                // Check if we need to switch frames
                if (!_frameDetector.NeedsFrameSwitch(detection))
                {
                    Logger.Debug("Already in correct frame context");
                    return true;
                }

                // Generate and add frame switch actions
                var switchActions = _frameDetector.GenerateSwitchActions(detection);
                foreach (var action in switchActions)
                {
                    _recordedActions.Add(action);
                    Logger.Info($"🔄 Auto-added: {action.Description}");
                    OnActionCaptured?.Invoke(action);

                    // Update frame context
                    if (action.ActionType == "SwitchToFrame")
                    {
                        _frameContext.EnterFrame(action.Locator, action.Locator, 
                            int.TryParse(action.Value, out var idx) ? idx : -1);
                    }
                    else if (action.ActionType == "SwitchToDefaultContent")
                    {
                        _frameContext.SwitchToDefaultContent();
                    }
                }

                Logger.Info($"✅ IFrame context handled for '{elementSelector}': {_frameContext.GetContextInfo()}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"IFrame detection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate a meaningful placeholder name for a field based on its selector
        /// Auto-generates from selector attributes
        /// </summary>
        private string GeneratePlaceholderName(string selector, string actualValue)
        {
            // Try to extract meaningful name from selector
            string parameterName = ExtractFieldNameFromSelector(selector);
            
            return parameterName;
        }
        
        /// <summary>
        /// Extract a meaningful field name from a CSS selector
        /// Examples: input[name="email"] → email, #userEmail → email, #userName → username
        /// </summary>
        private string ExtractFieldNameFromSelector(string selector)
        {
            // Try to extract from name attribute
            var nameMatch = System.Text.RegularExpressions.Regex.Match(selector, @"name[*=]?[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                return SanitizePlaceholderName(nameMatch.Groups[1].Value);
            }
            
            // Try to extract from id
            var idMatch = System.Text.RegularExpressions.Regex.Match(selector, @"#([a-zA-Z][\w-]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                string idValue = idMatch.Groups[1].Value;
                return SanitizePlaceholderName(idValue);
            }
            
            // Try to extract from placeholder attribute
            var placeholderMatch = System.Text.RegularExpressions.Regex.Match(selector, @"placeholder[*=]?[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (placeholderMatch.Success)
            {
                return SanitizePlaceholderName(placeholderMatch.Groups[1].Value);
            }
            
            // Try to detect from input type or common patterns
            if (selector.Contains("email", StringComparison.OrdinalIgnoreCase))
                return "email";
            if (selector.Contains("password", StringComparison.OrdinalIgnoreCase))
                return "password";
            if (selector.Contains("username", StringComparison.OrdinalIgnoreCase))
                return "username";
            if (selector.Contains("phone", StringComparison.OrdinalIgnoreCase) || selector.Contains("mobile", StringComparison.OrdinalIgnoreCase))
                return "phone";
            if (selector.Contains("first", StringComparison.OrdinalIgnoreCase) && selector.Contains("name", StringComparison.OrdinalIgnoreCase))
                return "firstName";
            if (selector.Contains("last", StringComparison.OrdinalIgnoreCase) && selector.Contains("name", StringComparison.OrdinalIgnoreCase))
                return "lastName";
            if (selector.Contains("address", StringComparison.OrdinalIgnoreCase))
                return "address";
            
            // Default fallback
            return "value";
        }
        
        /// <summary>
        /// Remove special characters and ensure valid placeholder name
        /// Converts to camelCase and removes common prefixes
        /// Examples: userEmail → email, txtPassword → password, inputUserName → username
        /// </summary>
        private string SanitizePlaceholderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "value";
            
            // Remove common prefixes that don't add meaning
            var prefixesToRemove = new[] { "user", "input", "txt", "field", "box", "ctl", "ctrl" };
            string lowerName = name.ToLower();
            
            foreach (var prefix in prefixesToRemove)
            {
                // Check if name starts with prefix followed by uppercase letter or underscore/dash
                if (lowerName.StartsWith(prefix))
                {
                    // Extract the part after the prefix
                    string remaining = name.Substring(prefix.Length);
                    
                    // If remaining starts with uppercase, underscore, or dash, use it
                    if (remaining.Length > 0 && (char.IsUpper(remaining[0]) || remaining[0] == '_' || remaining[0] == '-'))
                    {
                        name = remaining.TrimStart('_', '-');
                        break;
                    }
                }
            }
                
            // Remove special characters and spaces
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s-]", "");
            
            // Convert to camelCase
            var parts = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "value";
                
            var result = parts[0].ToLower();
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    result += char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
                }
            }
            
            // Ensure it starts with a letter
            if (!char.IsLetter(result[0]))
                result = "field" + char.ToUpper(result[0]) + result.Substring(1);
                
            return result;
        }

        /// <summary>
        /// Helper method to capitalize first letter of a string
        /// </summary>
        private static string CapitalizeFirst(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }
    }
}
