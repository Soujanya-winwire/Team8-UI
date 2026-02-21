using AgenticAI.Core.Configuration;
using AgenticAI.Core.Logging;
using AgenticAI.Core.ZeroCode.Models;
using Microsoft.Playwright;
using Newtonsoft.Json;

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
            
            // Expose a .NET callback to the page so in-page scripts can report actions
            // callback now receives css and xpath locators separately
            _page.ExposeFunctionAsync("__recordAction", (string actionType, string cssLocator, string xpathLocator, string? value, string? description) =>
            {
                var action = new RecordedAction
                {
                    ActionType = actionType,
                    Locator = cssLocator ?? string.Empty,
                    Value = value,
                    Description = description ?? (actionType + " on " + (cssLocator ?? xpathLocator)),
                    Timestamp = _recordedActions.Count
                };

                // store xpath in metadata for fallback
                if (!string.IsNullOrEmpty(xpathLocator))
                {
                    action.Metadata["xpath"] = xpathLocator;
                }

                // also capture data-testid if available (page script will pass as attribute)
                // page script may include dataTestId in description or as part of css - keep flexible

                _recordedActions.Add(action);
                Logger.Info($"Action recorded (auto): {action.ActionType} {action.Locator} (xpath: {xpathLocator})");
                return Task.CompletedTask;
            }).Wait();

            // Inject JS to capture common user interactions (clicks and input changes)
            // Uses the exposed __recordAction to send events back to .NET
            var script = @"() => {
                function getSimpleSelector(el) {
                    if (!el) return '';
                    if (el.id) return '#' + el.id;
                    var sel = el.tagName.toLowerCase();
                    if (el.classList && el.classList.length > 0) {
                        sel += '.' + Array.from(el.classList).filter(c=>c.trim()).join('.');
                    }
                    return sel;
                }

                function getXPath(element) {
                    if (element.id) {
                        return '//*[@id="' + element.id + '"]';
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
                    return parts.length ? '/' + parts.join('/') : null;
                }

                document.addEventListener('click', function(e) {
                    try {
                        var el = e.target;
                        var selector = getSimpleSelector(el);
                        // Some elements (like buttons inside spans) may need the closest clickable
                        if (!selector || selector === '') {
                            el = el.closest('button, a, input, [role=""button""]') || el;
                            selector = getSimpleSelector(el);
                        }
                        try {
                            var xpath = getXPath(el) || '';
                            window.__recordAction('Click', selector || el.tagName.toLowerCase(), xpath, '', 'Click on ' + (selector || el.tagName.toLowerCase()));
                        } catch(e) {
                            window.__recordAction('Click', selector || el.tagName.toLowerCase(), '', '', 'Click on ' + (selector || el.tagName.toLowerCase()));
                        }
                    } catch (ex) { console.log('record click error', ex); }
                }, true);

                document.addEventListener('input', function(e) {
                    try {
                        var el = e.target;
                        var selector = getSimpleSelector(el);
                        var value = el.value || '';
                        try {
                            var xpath = getXPath(el) || '';
                            window.__recordAction('Type', selector || el.tagName.toLowerCase(), xpath, value, 'Type into ' + (selector || el.tagName.toLowerCase()));
                        } catch(e) {
                            window.__recordAction('Type', selector || el.tagName.toLowerCase(), '', value, 'Type into ' + (selector || el.tagName.toLowerCase()));
                        }
                    } catch (ex) { console.log('record input error', ex); }
                }, true);
            }";

            try
            {
                // Fire-and-forget the injection - if it fails we still have navigation recorded
                _page.EvaluateAsync(script).Wait();
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
