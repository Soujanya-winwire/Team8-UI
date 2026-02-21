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
            Logger.Info("?? IMPORTANT: This is a BROWSER RECORDING session.");
            Logger.Info("   Actions are NOT automatically captured.");
            Logger.Info("   You need to manually add actions via the UI or use Playwright Codegen.");
            Logger.Info("");
            Logger.Info("?? Recommended: Use Playwright Codegen for automatic recording:");
            Logger.Info($"   playwright codegen {startUrl}");

            _playwright = await Playwright.CreateAsync();
            
            // Use Codegen mode for automatic recording
            var browserType = _config.Browser switch
            {
                Core.Enums.BrowserType.Firefox => _playwright.Firefox,
                Core.Enums.BrowserType.Edge => _playwright.Chromium,
                Core.Enums.BrowserType.Safari => _playwright.Webkit,
                _ => _playwright.Chromium
            };

            _browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 500 // Slow down for better recording
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                RecordVideoDir = _config.VideoPath
            });

            _page = await context.NewPageAsync();

            // Set up action listeners (limited automatic recording)
            SetupActionListeners();

            // Navigate to start URL
            await _page.GotoAsync(startUrl);
            
            Logger.Warning("Browser opened. Perform your test manually.");
            Logger.Warning("Note: Only navigation will be automatically recorded.");
            Logger.Warning("For full action recording, use: playwright codegen " + startUrl);
        }

        private void SetupActionListeners()
        {
            if (_page == null) return;

            // Record navigation
            _page.FrameNavigated += (sender, frame) =>
            {
                if (frame.Url != "about:blank")
                {
                    _recordedActions.Add(new RecordedAction
                    {
                        ActionType = "Navigate",
                        Value = frame.Url,
                        Description = $"Navigate to {frame.Url}",
                        Timestamp = _recordedActions.Count
                    });
                }
            };

            // Record console messages
            _page.Console += (sender, msg) =>
            {
                Logger.Debug($"Console: {msg.Text}");
            };
            
            // Expose a .NET callback that accepts JSON string from the page script
            _page.ExposeFunctionAsync("__recordAction", (string jsonPayload) =>
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(jsonPayload);
                    var root = doc.RootElement;
                    
                    var actionType = root.GetProperty("actionType").GetString() ?? string.Empty;
                    var css = root.TryGetProperty("css", out var pCss) ? pCss.GetString() ?? string.Empty : string.Empty;
                    var xpath = root.TryGetProperty("xpath", out var pXpath) ? pXpath.GetString() ?? string.Empty : string.Empty;
                    var value = root.TryGetProperty("value", out var pVal) ? pVal.GetString() : null;
                    var desc = root.TryGetProperty("description", out var pDesc) ? pDesc.GetString() : null;

                    var action = new RecordedAction
                    {
                        ActionType = actionType,
                        Locator = css ?? string.Empty,
                        Value = value,
                        Description = desc ?? (actionType + " on " + (css ?? xpath)),
                        Timestamp = _recordedActions.Count
                    };

                    if (!string.IsNullOrEmpty(xpath)) action.Metadata["xpath"] = xpath;
                    _recordedActions.Add(action);
                    Logger.Info($"Action recorded (auto): {action.ActionType} {action.Locator} (xpath: {xpath})");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to parse record action payload: {ex.Message}");
                }

                return Task.CompletedTask;
            }).Wait();

            // Inject embedded recorder script (avoids file loading issues)
            var script = @"(function(){
function getAttrSelector(el) {
    if (!el || !el.getAttribute) return null;
    var attrs = ['data-test-id','data-testid','data-test','name','aria-label','placeholder','title','id'];
    for (var i = 0; i < attrs.length; i++) {
        var a = attrs[i];
        try {
            var v = el.getAttribute(a);
            if (v) {
                v = v.trim();
                if (v.length > 0) return '[' + a + ""='"" + v.replace(/'/g, ""\'"") + ""']"";
            }
        } catch(e){}
    }
    return null;
}
function getSimpleSelector(el) {
    if (!el) return '';
    var byAttr = getAttrSelector(el);
    if (byAttr) return byAttr;
    if (el.id) return '#' + el.id;
    var sel = el.tagName.toLowerCase();
    if (el.classList && el.classList.length > 0) {
        sel += '.' + Array.from(el.classList).filter(c=>c.trim()).join('.');
    }
    return sel;
}
function getXPath(element) {
    if (!element) return '';
    if (element.id) {
        return ""//*[@id='"" + element.id + ""']"";
    }
    var parts = [];
    while (element && element.nodeType === Node.ELEMENT_NODE) {
        var nb = 0;
        var sib = element.previousSibling;
        while (sib) {
            if (sib.nodeType === Node.ELEMENT_NODE && sib.nodeName === element.nodeName) nb++;
            sib = sib.previousSibling;
        }
        var prefix = element.prefix ? element.prefix + ':' : '';
        var nth = (nb ? '[' + (nb+1) + ']' : '');
        parts.unshift(prefix + element.localName + nth);
        element = element.parentNode;
    }
    return parts.length ? '/' + parts.join('/') : '';
}
function sendAction(obj){
    try{
        if (window.__recordAction){
            window.__recordAction(JSON.stringify(obj));
        }
    }catch(e){ console.warn('sendAction error', e); }
}
document.addEventListener('click', function(e){
    try{
        var el = e.target;
        var selector = getSimpleSelector(el);
        if (!selector || selector === ''){
            el = el.closest('button, a, input, [role=button]') || el;
            selector = getSimpleSelector(el);
        }
        var xpath = '';
        try{ xpath = getXPath(el); }catch(e){}
        sendAction({ actionType: 'Click', css: selector || el.tagName.toLowerCase(), xpath: xpath, value: '', description: 'Click on ' + (selector || el.tagName.toLowerCase()) });
    }catch(ex){ console.log('record click error', ex); }
}, true);
document.addEventListener('input', function(e){
    try{
        var el = e.target;
        var selector = getSimpleSelector(el);
        var value = el.value || '';
        var xpath = '';
        try{ xpath = getXPath(el); }catch(e){}
        sendAction({ actionType: 'Type', css: selector || el.tagName.toLowerCase(), xpath: xpath, value: value, description: 'Type into ' + (selector || el.tagName.toLowerCase()) });
    }catch(ex){ console.log('record input error', ex); }
}, true);
})();";

            try
            {
                _page.EvaluateAsync(script).Wait();
                Logger.Info("Recorder injection script loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to inject action recorder script: {ex.Message}");
            }
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
