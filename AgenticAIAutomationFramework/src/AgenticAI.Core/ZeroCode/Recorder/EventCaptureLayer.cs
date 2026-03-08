using System;
using System.Collections.Generic;

namespace AgenticAI.Core.ZeroCode.Recorder
{
    /// <summary>
    /// Event Capture Layer - Captures browser events in recording mode
    /// Listens to: click, dblclick, input, change, keydown, submit, scroll
    /// </summary>
    public class EventCaptureLayer
    {
        private readonly List<string> _supportedEvents = new List<string>
        {
            "click",
            "dblclick",
            "input",
            "change",
            "keydown",
            "submit",
            "scroll"
        };

        /// <summary>
        /// Event fired when a browser event is captured
        /// </summary>
        public event Action<CapturedEvent>? OnEventCaptured;

        /// <summary>
        /// Gets the JavaScript code to inject into the browser for event capture
        /// </summary>
        public string GetInjectionScript()
        {
            return @"
(function() {
    'use strict';
    
    // Configuration
    const DEBOUNCE_DELAY = 300; // ms for input events
    const SCROLL_THROTTLE = 500; // ms for scroll events
    
    // State management
    let lastInputEvent = null;
    let lastScrollEvent = null;
    let scrollTimeout = null;
    let inputTimeout = null;
    
    // Helper: Capture event data
    function captureEventData(event, eventType) {
        const target = event.target;
        const timestamp = Date.now();
        
        return {
            type: eventType,
            target: {
                tagName: target.tagName,
                id: target.id || '',
                className: target.className || '',
                name: target.name || '',
                type: target.type || '',
                value: target.value || '',
                checked: target.checked,
                selectedIndex: target.selectedIndex,
                innerText: getElementText(target),
                attributes: getElementAttributes(target)
            },
            event: {
                key: event.key,
                keyCode: event.keyCode,
                shiftKey: event.shiftKey,
                ctrlKey: event.ctrlKey,
                altKey: event.altKey,
                metaKey: event.metaKey,
                button: event.button,
                clientX: event.clientX,
                clientY: event.clientY,
                scrollX: window.scrollX,
                scrollY: window.scrollY
            },
            context: {
                url: window.location.href,
                title: document.title,
                inIFrame: isInIFrame(),
                iframeSelector: getIFrameSelector(),
                timestamp: timestamp
            }
        };
    }
    
    // Helper: Get element text (up to 100 chars)
    function getElementText(element) {
        if (!element) return '';
        const text = (element.innerText || element.textContent || '').trim();
        return text.length > 100 ? text.substring(0, 100) + '...' : text;
    }
    
    // Helper: Get relevant element attributes
    function getElementAttributes(element) {
        const attrs = {};
        const relevantAttrs = ['data-testid', 'data-test-id', 'data-test', 'data-qa', 
                               'aria-label', 'placeholder', 'title', 'role', 'href'];
        
        relevantAttrs.forEach(attr => {
            const value = element.getAttribute(attr);
            if (value) attrs[attr] = value;
        });
        
        return attrs;
    }
    
    // Helper: Check if in iframe
    function isInIFrame() {
        try {
            return window.self !== window.top;
        } catch (e) {
            return true;
        }
    }
    
    // Helper: Get iframe selector
    function getIFrameSelector() {
        if (!isInIFrame()) return null;
        
        try {
            const iframes = window.parent.document.querySelectorAll('iframe, frame');
            for (let i = 0; i < iframes.length; i++) {
                if (iframes[i].contentWindow === window.self) {
                    const iframe = iframes[i];
                    if (iframe.id) return '#' + iframe.id;
                    if (iframe.name) return 'iframe[name=""' + iframe.name + '""]';
                    if (iframe.src) {
                        const srcPart = iframe.src.substring(iframe.src.lastIndexOf('/') + 1).split('?')[0];
                        return 'iframe[src*=""' + srcPart + '""]';
                    }
                    return 'iframe:nth-of-type(' + (i + 1) + ')';
                }
            }
        } catch (e) {
            // Cross-origin restriction
        }
        return null;
    }
    
    // Helper: Send event to recorder
    function sendToRecorder(eventData) {
        if (typeof window.__playwrightRecordAction === 'function') {
            window.__playwrightRecordAction(eventData);
        } else {
            console.warn('[EventCapture] Recorder function not available');
        }
    }
    
    // Event Handlers
    
    // Click events
    document.addEventListener('click', function(event) {
        const eventData = captureEventData(event, 'click');
        sendToRecorder(eventData);
        console.log('[EventCapture] Click captured', eventData);
    }, true);
    
    // Double-click events
    document.addEventListener('dblclick', function(event) {
        const eventData = captureEventData(event, 'dblclick');
        sendToRecorder(eventData);
        console.log('[EventCapture] Double-click captured', eventData);
    }, true);
    
    // Input events (with debouncing)
    document.addEventListener('input', function(event) {
        const target = event.target;
        
        // Clear previous timeout
        if (inputTimeout) {
            clearTimeout(inputTimeout);
        }
        
        // Debounce rapid input events
        inputTimeout = setTimeout(() => {
            const eventData = captureEventData(event, 'input');
            sendToRecorder(eventData);
            console.log('[EventCapture] Input captured (debounced)', eventData);
        }, DEBOUNCE_DELAY);
    }, true);
    
    // Change events (for select, checkbox, radio)
    document.addEventListener('change', function(event) {
        const eventData = captureEventData(event, 'change');
        sendToRecorder(eventData);
        console.log('[EventCapture] Change captured', eventData);
    }, true);
    
    // Keydown events (for special keys like Enter, Escape)
    document.addEventListener('keydown', function(event) {
        // Only capture special keys
        const specialKeys = ['Enter', 'Escape', 'Tab', 'F1', 'F2', 'F3', 'F4', 'F5', 
                            'F6', 'F7', 'F8', 'F9', 'F10', 'F11', 'F12'];
        
        if (specialKeys.includes(event.key)) {
            const eventData = captureEventData(event, 'keydown');
            sendToRecorder(eventData);
            console.log('[EventCapture] Keydown captured', eventData);
        }
    }, true);
    
    // Submit events
    document.addEventListener('submit', function(event) {
        const eventData = captureEventData(event, 'submit');
        sendToRecorder(eventData);
        console.log('[EventCapture] Submit captured', eventData);
    }, true);
    
    // Scroll events (with throttling)
    window.addEventListener('scroll', function(event) {
        if (scrollTimeout) return;
        
        scrollTimeout = setTimeout(() => {
            const eventData = {
                type: 'scroll',
                target: {
                    tagName: 'WINDOW',
                    scrollX: window.scrollX,
                    scrollY: window.scrollY
                },
                event: {
                    scrollX: window.scrollX,
                    scrollY: window.scrollY
                },
                context: {
                    url: window.location.href,
                    title: document.title,
                    inIFrame: isInIFrame(),
                    iframeSelector: getIFrameSelector(),
                    timestamp: Date.now()
                }
            };
            
            sendToRecorder(eventData);
            console.log('[EventCapture] Scroll captured (throttled)', eventData);
            scrollTimeout = null;
        }, SCROLL_THROTTLE);
    }, true);
    
    console.log('[EventCapture] Event capture layer initialized');
    console.log('[EventCapture] Listening for: click, dblclick, input, change, keydown, submit, scroll');
})();
";
        }

        /// <summary>
        /// Fire the captured event to listeners
        /// </summary>
        public void ProcessCapturedEvent(CapturedEvent capturedEvent)
        {
            OnEventCaptured?.Invoke(capturedEvent);
        }
    }

    /// <summary>
    /// Represents a captured browser event
    /// </summary>
    public class CapturedEvent
    {
        public string Type { get; set; } = "";
        public EventTarget Target { get; set; } = new EventTarget();
        public EventDetails Event { get; set; } = new EventDetails();
        public EventContext Context { get; set; } = new EventContext();
    }

    public class EventTarget
    {
        public string TagName { get; set; } = "";
        public string Id { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
        public bool Checked { get; set; }
        public int SelectedIndex { get; set; }
        public string InnerText { get; set; } = "";
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    public class EventDetails
    {
        public string? Key { get; set; }
        public int? KeyCode { get; set; }
        public bool ShiftKey { get; set; }
        public bool CtrlKey { get; set; }
        public bool AltKey { get; set; }
        public bool MetaKey { get; set; }
        public int? Button { get; set; }
        public int? ClientX { get; set; }
        public int? ClientY { get; set; }
        public int ScrollX { get; set; }
        public int ScrollY { get; set; }
    }

    public class EventContext
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public bool InIFrame { get; set; }
        public string? IFrameSelector { get; set; }
        public long Timestamp { get; set; }
    }
}
