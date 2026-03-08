using System;
using System.Collections.Generic;
using System.Linq;
using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.ZeroCode.Recorder
{
    /// <summary>
    /// Scenario Builder - Orchestrates all recorder components to build stable test scenarios
    /// Combines: Event Capture -> Element Analysis -> Locator Generation -> Action Normalization
    /// </summary>
    public class ScenarioBuilder
    {
        private readonly EventCaptureLayer _eventCapture;
        private readonly ElementAnalyzer _elementAnalyzer;
        private readonly SmartLocatorGenerator _locatorGenerator;
        private readonly ActionNormalizer _actionNormalizer;
        private readonly List<NormalizedAction> _capturedActions;
        private readonly IFrameContext _frameContext;
        
        private NormalizedAction? _lastAction;
        private string _currentUrl = "";
        private bool _isRecording;

        /// <summary>
        /// Event fired when a new action is captured
        /// </summary>
        public event Action<RecordedAction>? OnActionCaptured;

        public ScenarioBuilder()
        {
            _eventCapture = new EventCaptureLayer();
            _elementAnalyzer = new ElementAnalyzer();
            _locatorGenerator = new SmartLocatorGenerator();
            _actionNormalizer = new ActionNormalizer();
            _capturedActions = new List<NormalizedAction>();
            _frameContext = new IFrameContext();
            
            // Subscribe to event capture
            _eventCapture.OnEventCaptured += HandleCapturedEvent;
            
            _isRecording = false;
        }

        /// <summary>
        /// Start building a scenario
        /// </summary>
        public void StartRecording(string startUrl)
        {
            _isRecording = true;
            _currentUrl = startUrl;
            _capturedActions.Clear();
            _lastAction = null;
            
            Logger.Info("[ScenarioBuilder] Recording started");
        }

        /// <summary>
        /// Stop recording and build the final scenario
        /// </summary>
        public TestScenario StopRecording(string scenarioName, string module, string? description = null)
        {
            _isRecording = false;
            
            Logger.Info($"[ScenarioBuilder] Recording stopped. Captured {_capturedActions.Count} actions");
            
            // Build scenario
            var scenario = new TestScenario
            {
                ScenarioId = Guid.NewGuid().ToString(),
                Name = scenarioName,
                Module = module,
                Description = description ?? $"Recorded test scenario for {scenarioName}",
                StartUrl = _currentUrl,
                Tags = new List<string> { "recorded", "automated" },
                Actions = ConvertToRecordedActions(_capturedActions),
                Assertions = new List<Assertion>(),
                CreatedAt = DateTime.Now,
                ModifiedAt = null
            };
            
            // Add smart waits between steps
            scenario.Actions = AddSmartWaits(scenario.Actions);
            
            // Optimize actions (remove redundant steps)
            scenario.Actions = OptimizeActions(scenario.Actions);
            
            Logger.Info($"[ScenarioBuilder] Scenario built with {scenario.Actions.Count} optimized actions");
            
            return scenario;
        }

        /// <summary>
        /// Get the browser injection scripts
        /// </summary>
        public string GetBrowserInjectionScripts()
        {
            return _eventCapture.GetInjectionScript() + "\n\n" +
                   _elementAnalyzer.GetInjectionScript() + "\n\n" +
                   _locatorGenerator.GetInjectionScript() + "\n\n" +
                   GetHighlightingScript() + "\n\n" +
                   GetNavigationDetectionScript();
        }

        /// <summary>
        /// Handle captured browser events
        /// </summary>
        private void HandleCapturedEvent(CapturedEvent capturedEvent)
        {
            if (!_isRecording)
                return;
            
            try
            {
                Logger.Debug($"[ScenarioBuilder] Processing {capturedEvent.Type} event");
                
                // Check for navigation (URL change)
                if (capturedEvent.Context.Url != _currentUrl)
                {
                    HandleNavigation(capturedEvent.Context.Url);
                }
                
                // Handle iframe context
                HandleIFrameContext(capturedEvent);
                
                // Analyze element
                var analyzedElement = _elementAnalyzer.AnalyzeElement(capturedEvent);
                
                // Generate locator
                var locator = _locatorGenerator.GenerateLocator(capturedEvent);
                
                Logger.Debug($"[ScenarioBuilder] Generated locator: {locator.Locator} (strategy: {locator.Strategy}, confidence: {locator.Confidence})");
                
                // Normalize action
                var normalizedAction = _actionNormalizer.Normalize(capturedEvent, locator);
                
                if (normalizedAction == null)
                {
                    Logger.Debug($"[ScenarioBuilder] Action ignored (not normalized)");
                    return;
                }
                
                // Check for deduplication
                if (_actionNormalizer.ShouldDeduplicate(normalizedAction, _lastAction))
                {
                    Logger.Debug($"[ScenarioBuilder] Action deduplicated");
                    
                    // Merge with last action
                    if (_lastAction != null)
                    {
                        _lastAction = _actionNormalizer.MergeActions(normalizedAction, _lastAction);
                        
                        // Update the last recorded action
                        if (_capturedActions.Count > 0)
                        {
                            _capturedActions[_capturedActions.Count - 1] = _lastAction;
                        }
                        
                        // Fire event for UI update
                        var recordedAction = _lastAction.ToRecordedAction(_capturedActions.Count - 1);
                        OnActionCaptured?.Invoke(recordedAction);
                    }
                    
                    return;
                }
                
                // Add action
                _capturedActions.Add(normalizedAction);
                _lastAction = normalizedAction;
                
                Logger.Info($"[ScenarioBuilder] Action captured: {normalizedAction.ActionType} - {normalizedAction.Description}");
                
                // Fire event
                var action = normalizedAction.ToRecordedAction(_capturedActions.Count - 1);
                OnActionCaptured?.Invoke(action);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ScenarioBuilder] Error processing event: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle navigation detection
        /// </summary>
        private void HandleNavigation(string newUrl)
        {
            if (newUrl == _currentUrl)
                return;
            
            Logger.Info($"[ScenarioBuilder] Navigation detected: {_currentUrl} -> {newUrl}");
            
            // Add navigation action
            var navigationAction = new NormalizedAction
            {
                ActionType = "Navigate",
                Locator = newUrl,
                Value = newUrl,
                Description = $"Navigate to {newUrl}",
                Confidence = 100
            };
            
            _capturedActions.Add(navigationAction);
            _currentUrl = newUrl;
            
            // Fire event
            var action = navigationAction.ToRecordedAction(_capturedActions.Count - 1);
            OnActionCaptured?.Invoke(action);
        }

        /// <summary>
        /// Handle iframe context switches
        /// </summary>
        private void HandleIFrameContext(CapturedEvent capturedEvent)
        {
            var inIFrame = capturedEvent.Context.InIFrame;
            var iframeSelector = capturedEvent.Context.IFrameSelector;
            
            // Element is in iframe but we're in main content
            if (inIFrame && !_frameContext.IsInFrame && !string.IsNullOrEmpty(iframeSelector))
            {
                Logger.Info($"[ScenarioBuilder] Switching to iframe: {iframeSelector}");
                
                var switchAction = new NormalizedAction
                {
                    ActionType = "SwitchToFrame",
                    Locator = iframeSelector,
                    Value = null,
                    Description = $"Switch to iframe: {iframeSelector}",
                    Confidence = 90
                };
                
                _capturedActions.Add(switchAction);
                _frameContext.EnterFrame(iframeSelector, iframeSelector);
                
                // Fire event
                var action = switchAction.ToRecordedAction(_capturedActions.Count - 1);
                OnActionCaptured?.Invoke(action);
            }
            // Element is in main content but we're in iframe
            else if (!inIFrame && _frameContext.IsInFrame)
            {
                Logger.Info($"[ScenarioBuilder] Switching to default content");
                
                var switchAction = new NormalizedAction
                {
                    ActionType = "SwitchToDefaultContent",
                    Locator = "",
                    Value = null,
                    Description = "Switch to default content",
                    Confidence = 90
                };
                
                _capturedActions.Add(switchAction);
                _frameContext.SwitchToDefaultContent();
                
                // Fire event
                var action = switchAction.ToRecordedAction(_capturedActions.Count - 1);
                OnActionCaptured?.Invoke(action);
            }
        }

        /// <summary>
        /// Convert normalized actions to recorded actions
        /// </summary>
        private List<RecordedAction> ConvertToRecordedActions(List<NormalizedAction> normalizedActions)
        {
            var recordedActions = new List<RecordedAction>();
            
            for (int i = 0; i < normalizedActions.Count; i++)
            {
                recordedActions.Add(normalizedActions[i].ToRecordedAction(i));
            }
            
            return recordedActions;
        }

        /// <summary>
        /// Add smart waits between steps
        /// </summary>
        private List<RecordedAction> AddSmartWaits(List<RecordedAction> actions)
        {
            var optimizedActions = new List<RecordedAction>();
            
            for (int i = 0; i < actions.Count; i++)
            {
                var currentAction = actions[i];
                optimizedActions.Add(currentAction);
                
                // Add wait after navigation
                if (currentAction.ActionType == "Navigate" || currentAction.ActionType == "Click")
                {
                    // Check if next action is not a wait
                    if (i + 1 < actions.Count && actions[i + 1].ActionType != "Wait")
                    {
                        // Add implicit wait
                        optimizedActions.Add(new RecordedAction
                        {
                            ActionType = "Wait",
                            Locator = "",
                            Value = "1",
                            Description = "Wait for page to load",
                            Timestamp = currentAction.Timestamp
                        });
                    }
                }
            }
            
            return optimizedActions;
        }

        /// <summary>
        /// Optimize actions (remove redundant steps)
        /// </summary>
        private List<RecordedAction> OptimizeActions(List<RecordedAction> actions)
        {
            var optimized = new List<RecordedAction>();
            
            for (int i = 0; i < actions.Count; i++)
            {
                var currentAction = actions[i];
                
                // Remove consecutive duplicate actions
                if (i > 0 && AreActionsDuplicate(optimized.Last(), currentAction))
                {
                    Logger.Debug($"[ScenarioBuilder] Removing duplicate action: {currentAction.ActionType}");
                    continue;
                }
                
                // Remove rapid consecutive scrolls (keep only the last one)
                if (currentAction.ActionType == "Scroll" && i + 1 < actions.Count && actions[i + 1].ActionType == "Scroll")
                {
                    Logger.Debug($"[ScenarioBuilder] Skipping intermediate scroll action");
                    continue;
                }
                
                optimized.Add(currentAction);
            }
            
            return optimized;
        }

        /// <summary>
        /// Check if two actions are duplicates
        /// </summary>
        private bool AreActionsDuplicate(RecordedAction action1, RecordedAction action2)
        {
            return action1.ActionType == action2.ActionType &&
                   action1.Locator == action2.Locator &&
                   action1.Value == action2.Value;
        }

        /// <summary>
        /// Get element highlighting script
        /// </summary>
        private string GetHighlightingScript()
        {
            return @"
(function() {
    'use strict';
    
    let highlightedElement = null;
    let highlightBorder = null;
    
    // Create highlight border element
    function createHighlightBorder() {
        if (highlightBorder) return highlightBorder;
        
        highlightBorder = document.createElement('div');
        highlightBorder.style.position = 'fixed';
        highlightBorder.style.border = '3px solid #FF6B6B';
        highlightBorder.style.backgroundColor = 'rgba(255, 107, 107, 0.1)';
        highlightBorder.style.pointerEvents = 'none';
        highlightBorder.style.zIndex = '999999';
        highlightBorder.style.transition = 'all 0.2s ease';
        document.body.appendChild(highlightBorder);
        
        return highlightBorder;
    }
    
    // Highlight element
    function highlightElement(element) {
        if (!element) return;
        
        const border = createHighlightBorder();
        const rect = element.getBoundingClientRect();
        
        border.style.left = rect.left + 'px';
        border.style.top = rect.top + 'px';
        border.style.width = rect.width + 'px';
        border.style.height = rect.height + 'px';
        border.style.display = 'block';
        
        highlightedElement = element;
        
        // Auto-remove after 2 seconds
        setTimeout(() => {
            if (border) {
                border.style.display = 'none';
            }
        }, 2000);
    }
    
    // Expose highlighting function
    window.__highlightElement = highlightElement;
    
    // Highlight elements on hover during recording
    document.addEventListener('mouseover', function(e) {
        if (window.__isRecording) {
            highlightElement(e.target);
        }
    }, true);
    
    console.log('[Highlighting] Element highlighting enabled');
})();
";
        }

        /// <summary>
        /// Get navigation detection script
        /// </summary>
        private string GetNavigationDetectionScript()
        {
            return @"
(function() {
    'use strict';
    
    let lastUrl = window.location.href;
    
    // Detect URL changes (for SPAs)
    setInterval(function() {
        const currentUrl = window.location.href;
        if (currentUrl !== lastUrl) {
            console.log('[NavigationDetection] URL changed:', lastUrl, '->', currentUrl);
            
            if (typeof window.__playwrightRecordAction === 'function') {
                window.__playwrightRecordAction({
                    type: 'navigation',
                    target: { tagName: 'NAVIGATION' },
                    event: {},
                    context: {
                        url: currentUrl,
                        title: document.title,
                        inIFrame: false,
                        iframeSelector: null,
                        timestamp: Date.now()
                    }
                });
            }
            
            lastUrl = currentUrl;
        }
    }, 500);
    
    console.log('[NavigationDetection] Navigation detection enabled');
})();
";
        }

        /// <summary>
        /// Get current action count
        /// </summary>
        public int GetActionCount()
        {
            return _capturedActions.Count;
        }
    }
}
