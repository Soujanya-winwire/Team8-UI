// Enhanced Test Recorder Script with Advanced Features
// Captures user interactions and generates stable automation steps
(function() {
    'use strict';
    
    // ============================================================================
    // CONFIGURATION
    // ============================================================================
    const RECORDER_CONFIG = {
        highlightDuration: 2000,
        highlightColor: '#ff6b6b',
        highlightBorderWidth: '3px',
        debounceDelay: 500,
        excludeSelectors: [
            '[data-recorder-overlay]', 
            '[data-recorder-control]',
            '#__playwright_recorder_controls', // Stop recording button overlay
            '#__recorder_stop_btn',            // Stop recording button itself
            '#__recorder_pulse',               // Pulsing indicator
            '#__recorder_action_count'         // Action counter
        ],
        captureMode: true // Event capturing phase
    };

    // ============================================================================
    // ELEMENT ANALYZER - Resolves actionable elements
    // ============================================================================
    class ElementAnalyzer {
        /**
         * Find the actual actionable element from a clicked target
         * If a child element (e.g., span) is clicked inside a button, return the button
         */
        static findActionableElement(target) {
            if (!target) return null;

            // If target itself is actionable, return it
            if (this.isActionableElement(target)) {
                return target;
            }

            // Traverse up the DOM tree to find actionable parent
            const actionableParent = target.closest(
                'button, a, input, select, textarea, [role="button"], [role="link"], ' +
                '[role="checkbox"], [role="radio"], [role="tab"], [role="menuitem"], ' +
                '[onclick], [ng-click], [data-action]'
            );

            return actionableParent || target;
        }

        /**
         * Check if an element is inherently actionable
         */
        static isActionableElement(el) {
            if (!el || !el.tagName) return false;

            const tag = el.tagName.toLowerCase();
            const actionableTags = ['button', 'a', 'input', 'select', 'textarea'];
            
            if (actionableTags.includes(tag)) return true;

            // Check for role attributes
            const role = el.getAttribute('role');
            if (role && ['button', 'link', 'checkbox', 'radio', 'tab', 'menuitem'].includes(role)) {
                return true;
            }

            // Check for click handlers
            if (el.onclick || el.getAttribute('onclick') || el.getAttribute('ng-click')) {
                return true;
            }

            return false;
        }

        /**
         * Check if element should be excluded from recording
         */
        static shouldExclude(el) {
            if (!el) return true;

            // Exclude recorder overlay elements
            for (const selector of RECORDER_CONFIG.excludeSelectors) {
                if (el.matches && el.matches(selector)) return true;
                if (el.closest && el.closest(selector)) return true;
            }

            return false;
        }
    }

    // ============================================================================
    // SMART LOCATOR GENERATOR - Priority-based selector generation
    // ============================================================================
    class SmartLocatorGenerator {
        /**
         * Generate the best selector for an element following priority rules
         * PERMANENT SOLUTION: Prioritize semantic selectors, avoid ALL IDs by default
         */
        static generateSelector(element) {
            if (!element || !element.tagName) return null;

            // Priority 1: data-testid attributes (BEST PRACTICE - Explicitly for testing)
            const testIdSelector = this.getTestIdSelector(element);
            if (testIdSelector) return testIdSelector;

            // Priority 2: aria-label (ACCESSIBILITY - Semantic and stable)
            const ariaSelector = this.getAriaLabelSelector(element);
            if (ariaSelector) return ariaSelector;

            // Priority 3: role + accessible name (ACCESSIBILITY - Semantic HTML)
            const roleSelector = this.getRoleSelector(element);
            if (roleSelector) return roleSelector;

            // Priority 4: text content (HIGHLY STABLE - Visible to users)
            const textSelector = this.getTextSelector(element);
            if (textSelector) return textSelector;

            // Priority 5: name attribute (FORM ELEMENTS - Semantic for forms)
            const nameSelector = this.getNameSelector(element);
            if (nameSelector) return nameSelector;

            // Priority 6: placeholder (INPUT FIELDS - Visible to users)
            const placeholderSelector = this.getPlaceholderSelector(element);
            if (placeholderSelector) return placeholderSelector;

            // Priority 7: CSS selector with stable classes (Avoid IDs entirely)
            const cssSelector = this.getCssSelector(element);
            if (cssSelector) return cssSelector;

            // Priority 8: Composite selector (tag + attributes)
            const compositeSelector = this.getCompositeSelector(element);
            if (compositeSelector) return compositeSelector;

            // Priority 9: XPath (last resort)
            return this.getXPathSelector(element);
        }

        /**
         * Get test id selector (data-testid, data-test-id, data-test, data-qa)
         */
        static getTestIdSelector(element) {
            const testAttrs = ['data-testid', 'data-test-id', 'data-test', 'data-qa', 'data-cy'];
            
            for (const attr of testAttrs) {
                const value = element.getAttribute(attr);
                if (value && value.trim()) {
                    return `[${attr}="${this.escapeSelector(value)}"]`;
                }
            }
            return null;
        }


        /**
         * Get name attribute selector
         */
        static getNameSelector(element) {
            const name = element.getAttribute('name');
            if (!name || !name.trim()) return null;

            const tag = element.tagName.toLowerCase();
            return `${tag}[name="${this.escapeSelector(name)}"]`;
        }

        /**
         * Get aria-label selector
         */
        static getAriaLabelSelector(element) {
            const ariaLabel = element.getAttribute('aria-label');
            if (!ariaLabel || !ariaLabel.trim()) return null;

            return `[aria-label="${this.escapeSelector(ariaLabel)}"]`;
        }

        /**
         * Get role-based selector
         */
        static getRoleSelector(element) {
            const role = element.getAttribute('role');
            if (!role) return null;

            // Try to add accessible name for uniqueness
            const ariaLabel = element.getAttribute('aria-label');
            if (ariaLabel) {
                return `[role="${role}"][aria-label="${this.escapeSelector(ariaLabel)}"]`;
            }

            const ariaLabelledBy = element.getAttribute('aria-labelledby');
            if (ariaLabelledBy) {
                return `[role="${role}"][aria-labelledby="${ariaLabelledBy}"]`;
            }

            return `[role="${role}"]`;
        }

        /**
         * Get text-based selector (for buttons, links)
         */
        static getTextSelector(element) {
            const tag = element.tagName.toLowerCase();
            if (!['button', 'a', 'span'].includes(tag)) return null;

            let text = element.textContent || '';
            text = text.trim();

            // Only use text if it's meaningful and not too long
            if (text && text.length > 0 && text.length < 50 && !text.includes('\n')) {
                // Escape quotes and special characters
                const escapedText = text.replace(/"/g, '\\"');
                return `${tag}:has-text("${escapedText}")`;
            }

            return null;
        }

        /**
         * Get placeholder-based selector (for input fields)
         */
        static getPlaceholderSelector(element) {
            const tag = element.tagName.toLowerCase();
            if (tag !== 'input' && tag !== 'textarea') return null;

            const placeholder = element.getAttribute('placeholder');
            if (!placeholder || !placeholder.trim()) return null;

            return `${tag}[placeholder="${this.escapeSelector(placeholder)}"]`;
        }

        /**
         * Get composite selector (tag + multiple attributes for uniqueness)
         */
        static getCompositeSelector(element) {
            const tag = element.tagName.toLowerCase();
            const attributes = [];

            // Collect stable attributes
            const type = element.getAttribute('type');
            if (type) attributes.push(`[type="${type}"]`);

            const title = element.getAttribute('title');
            if (title) attributes.push(`[title="${this.escapeSelector(title)}"]`);

            const value = element.getAttribute('value');
            if (value && value.length < 20) attributes.push(`[value="${this.escapeSelector(value)}"]`);

            // Return composite selector if we have at least one attribute
            if (attributes.length > 0) {
                return tag + attributes.join('');
            }

            return null;
        }

        /**
         * Get stable CSS selector (avoid nth-child, dynamic classes)
         */
        static getCssSelector(element) {
            const tag = element.tagName.toLowerCase();
            const classes = this.getStableClasses(element);

            if (classes.length > 0) {
                return `${tag}.${classes.join('.')}`;
            }

            // If no stable classes, use tag + attribute combination
            const type = element.getAttribute('type');
            if (type) {
                return `${tag}[type="${type}"]`;
            }

            return tag;
        }

        /**
         * Get stable CSS classes (filter out dynamic ones)
         */
        static getStableClasses(element) {
            if (!element.classList || element.classList.length === 0) return [];

            const dynamicPrefixes = ['ng-', 'mat-', 'css-', '_', 'x-', 'v-', 'ember-', 'react-'];
            const stableClasses = [];

            for (const cls of element.classList) {
                // Skip dynamic classes
                if (dynamicPrefixes.some(prefix => cls.startsWith(prefix))) continue;
                
                // Skip classes with numbers (often dynamic)
                if (/\d{3,}/.test(cls)) continue;

                stableClasses.push(this.escapeSelector(cls));
            }

            return stableClasses.slice(0, 2); // Use max 2 classes
        }

        /**
         * Get XPath selector (fallback)
         */
        static getXPathSelector(element) {
            // Build a readable, stable XPath without relying on IDs
            const segments = [];
            let currentElement = element;

            while (currentElement && currentElement.nodeType === Node.ELEMENT_NODE) {
                let segment = currentElement.tagName.toLowerCase();

                // Prefer name attribute over position
                const name = currentElement.getAttribute('name');
                if (name) {
                    segment += `[@name="${name}"]`;
                } else {
                    // Use position only if necessary
                    const siblings = Array.from(currentElement.parentNode?.children || [])
                        .filter(e => e.tagName === currentElement.tagName);
                    
                    if (siblings.length > 1) {
                        const index = siblings.indexOf(currentElement) + 1;
                        segment += `[${index}]`;
                    }
                }

                segments.unshift(segment);

                // Stop if we have enough specificity
                if (segments.length >= 4) break;

                currentElement = currentElement.parentElement;
            }

            return '//' + segments.join('/');
        }

        /**
         * Escape special characters in selectors
         */
        static escapeSelector(value) {
            return value.replace(/["'\\]/g, '\\$&');
        }

        /**
         * Generate multiple selector options for fallback
         */
        static generateFallbackSelectors(element) {
            const selectors = [];

            const primary = this.generateSelector(element);
            if (primary) selectors.push(primary);

            // Add alternative selectors (NO IDs - only semantic selectors)
            const testId = this.getTestIdSelector(element);
            const aria = this.getAriaLabelSelector(element);
            const text = this.getTextSelector(element);
            const name = this.getNameSelector(element);
            const placeholder = this.getPlaceholderSelector(element);
            const css = this.getCssSelector(element);

            [testId, aria, text, name, placeholder, css].forEach(sel => {
                if (sel && !selectors.includes(sel)) {
                    selectors.push(sel);
                }
            });

            return selectors;
        }
    }

    // ============================================================================
    // SHADOW DOM HANDLER
    // ============================================================================
    class ShadowDOMHandler {
        /**
         * Check if element is inside Shadow DOM
         */
        static isInShadowDOM(element) {
            let node = element;
            while (node) {
                if (node.toString() === '[object ShadowRoot]') {
                    return true;
                }
                node = node.parentNode;
            }
            return false;
        }

        /**
         * Get Shadow DOM path to element
         */
        static getShadowPath(element) {
            const path = [];
            let node = element;

            while (node) {
                if (node.toString() === '[object ShadowRoot]') {
                    const host = node.host;
                    path.unshift({
                        type: 'shadow-root',
                        hostSelector: SmartLocatorGenerator.generateSelector(host)
                    });
                    node = host;
                } else if (node.nodeType === Node.ELEMENT_NODE) {
                    node = node.parentNode;
                } else {
                    break;
                }
            }

            return path;
        }

        /**
         * Generate selector including Shadow DOM path
         */
        static generateShadowSelector(element) {
            const shadowPath = this.getShadowPath(element);
            if (shadowPath.length === 0) return null;

            const elementSelector = SmartLocatorGenerator.generateSelector(element);
            
            return {
                shadowPath: shadowPath,
                elementSelector: elementSelector,
                fullPath: shadowPath.map(p => p.hostSelector).join(' >> ') + ' >> ' + elementSelector
            };
        }
    }

    // ============================================================================
    // IFRAME HANDLER
    // ============================================================================
    class IFrameHandler {
        /**
         * Check if current context is inside an iframe
         */
        static isInIFrame() {
            try {
                return window.self !== window.top;
            } catch (e) {
                // Cross-origin restriction means we're in an iframe
                return true;
            }
        }

        /**
         * Get selector for the iframe containing current context
         */
        static getIFrameSelector() {
            if (!this.isInIFrame()) return null;

            try {
                const iframes = window.parent.document.querySelectorAll('iframe, frame');
                
                for (let i = 0; i < iframes.length; i++) {
                    if (iframes[i].contentWindow === window.self) {
                        return SmartLocatorGenerator.generateSelector(iframes[i]);
                    }
                }
            } catch (e) {
                // Cross-origin restriction - return a generic selector
                console.warn('Cannot access parent frame due to cross-origin policy');
                return 'iframe[src*="' + window.location.hostname + '"]';
            }

            return null;
        }
    }

    // ============================================================================
    // ELEMENT HIGHLIGHTER
    // ============================================================================
    class ElementHighlighter {
        static highlightElement(element) {
            if (!element) return;

            // Store original styles
            const originalOutline = element.style.outline;
            const originalOutlineOffset = element.style.outlineOffset;

            // Apply highlight
            element.style.outline = `${RECORDER_CONFIG.highlightBorderWidth} solid ${RECORDER_CONFIG.highlightColor}`;
            element.style.outlineOffset = '2px';

            // Add pulse animation
            element.style.transition = 'outline 0.3s ease-in-out';

            // Remove highlight after duration
            setTimeout(() => {
                element.style.outline = originalOutline;
                element.style.outlineOffset = originalOutlineOffset;
            }, RECORDER_CONFIG.highlightDuration);
        }

        static addPermanentHighlight(element, color = '#4CAF50') {
            if (!element) return;

            element.style.outline = `2px solid ${color}`;
            element.style.outlineOffset = '1px';
        }

        static removeHighlight(element) {
            if (!element) return;

            element.style.outline = '';
            element.style.outlineOffset = '';
        }
    }

    // ============================================================================
    // ACTION NORMALIZER - Converts DOM events to automation actions
    // ============================================================================
    class ActionNormalizer {
        static normalizeAction(eventType, element, eventData = {}) {
            const tag = element.tagName.toLowerCase();
            const type = element.getAttribute('type');

            switch (eventType) {
                case 'click':
                case 'dblclick':
                    if (tag === 'a') {
                        return { action: 'click', type: 'navigation' };
                    }
                    if (tag === 'button' || type === 'submit') {
                        return { action: eventType === 'dblclick' ? 'dblclick' : 'click', type: 'button' };
                    }
                    if (tag === 'input' && (type === 'checkbox' || type === 'radio')) {
                        return { action: element.checked ? 'check' : 'uncheck', type: 'checkbox' };
                    }
                    return { action: eventType, type: 'general' };

                case 'input':
                case 'change':
                    if (tag === 'input' && type === 'text') {
                        return { action: 'type', type: 'text', value: element.value };
                    }
                    if (tag === 'input' && type === 'password') {
                        return { action: 'type', type: 'password', value: element.value };
                    }
                    if (tag === 'textarea') {
                        return { action: 'type', type: 'textarea', value: element.value };
                    }
                    if (tag === 'select') {
                        const selectedOption = element.options[element.selectedIndex];
                        return { 
                            action: 'select', 
                            type: 'dropdown',
                            value: selectedOption?.value,
                            text: selectedOption?.text
                        };
                    }
                    if (tag === 'input' && type === 'file') {
                        return { action: 'upload', type: 'file', value: element.value };
                    }
                    return { action: 'input', type: 'general', value: element.value };

                case 'keydown':
                    if (eventData.key === 'Enter') {
                        return { action: 'pressEnter', type: 'keyboard' };
                    }
                    if (eventData.key === 'Tab') {
                        return { action: 'pressTab', type: 'keyboard' };
                    }
                    if (eventData.key === 'Escape') {
                        return { action: 'pressEscape', type: 'keyboard' };
                    }
                    return null; // Don't record other keys

                case 'submit':
                    return { action: 'submit', type: 'form' };

                default:
                    return null;
            }
        }
    }

    // ============================================================================
    // NAVIGATION TRACKER
    // ============================================================================
    class NavigationTracker {
        static lastUrl = window.location.href;
        static lastActionTime = Date.now();

        static trackNavigation() {
            const currentUrl = window.location.href;
            
            if (currentUrl !== this.lastUrl) {
                const navigationEvent = {
                    eventType: 'navigation',
                    fromUrl: this.lastUrl,
                    toUrl: currentUrl,
                    timestamp: Date.now()
                };

                // Calculate wait time since last action
                const waitTime = Date.now() - this.lastActionTime;
                if (waitTime > 1000) { // More than 1 second
                    navigationEvent.waitTime = Math.round(waitTime / 1000);
                }

                this.lastUrl = currentUrl;
                RecorderController.sendAction(navigationEvent);
            }
        }

        static updateLastActionTime() {
            this.lastActionTime = Date.now();
        }
    }

    // ============================================================================
    // RECORDER CONTROLLER - Main recording logic
    // ============================================================================
    class RecorderController {
        static isRecording = false;
        static lastInputTime = {};
        static pendingInputActions = {};
        static clickTimeout = null;
        static doubleClickDetected = false;
        static pendingClickElement = null;
        static isNavigationClick = false;

        static initialize() {
            this.setupEventListeners();
            this.startNavigationTracking();
            this.isRecording = true;
            
            // Expose stop method globally for external control
            window.__stopRecording = this.stopRecording.bind(this);
            
            console.log('? Enhanced Test Recorder initialized');
            console.log('?? Recording: clicks, typing, selections, keyboard, form submissions');
            console.log('?? Features: Smart selectors, Shadow DOM, iframes, element analysis');
        }

        static stopRecording() {
            console.log('? Stopping recording - flushing pending actions...');
            
            // Flush any pending click (important!)
            if (this.clickTimeout && this.pendingClickElement) {
                clearTimeout(this.clickTimeout);
                console.log('?? Flushing pending click action');
                this.recordClick(this.pendingClickElement);
            }
            
            // Flush any pending input actions
            Object.keys(this.pendingInputActions).forEach(key => {
                clearTimeout(this.pendingInputActions[key]);
            });
            
            this.isRecording = false;
            this.clickTimeout = null;
            this.pendingClickElement = null;
            this.pendingInputActions = {};
            
            console.log('? Recording stopped successfully');
        }

        static setupEventListeners() {
            // Click events (capturing phase)
            document.addEventListener('click', this.handleClick.bind(this), RECORDER_CONFIG.captureMode);
            
            // Double-click events
            document.addEventListener('dblclick', this.handleDblClick.bind(this), RECORDER_CONFIG.captureMode);
            
            // Right-click events (context menu)
            document.addEventListener('contextmenu', this.handleRightClick.bind(this), RECORDER_CONFIG.captureMode);
            
            // Input events (typing)
            document.addEventListener('input', this.handleInput.bind(this), RECORDER_CONFIG.captureMode);
            
            // Change events (select, checkbox, radio)
            document.addEventListener('change', this.handleChange.bind(this), RECORDER_CONFIG.captureMode);
            
            // Keyboard events
            document.addEventListener('keydown', this.handleKeyDown.bind(this), RECORDER_CONFIG.captureMode);
            
            // Form submission
            document.addEventListener('submit', this.handleSubmit.bind(this), RECORDER_CONFIG.captureMode);

            // BROWSER NAVIGATION ACTIONS
            this.setupBrowserNavigationTracking();

            console.log('? Event listeners attached in capturing mode');
        }

        static setupBrowserNavigationTracking() {
            // CRITICAL: Use sessionStorage to persist state across page reloads
            const getLastAction = () => {
                try {
                    const stored = sessionStorage.getItem('__recorderLastAction');
                    return stored ? JSON.parse(stored) : { type: null, time: 0 };
                } catch {
                    return { type: null, time: 0 };
                }
            };

            const setLastAction = (type) => {
                try {
                    sessionStorage.setItem('__recorderLastAction', JSON.stringify({
                        type: type,
                        time: Date.now()
                    }));
                } catch {}
            };

            const DUPLICATE_THRESHOLD = 2000; // 2 seconds (increased from 1)
            
            let refreshKeyPressed = false;
            let backKeyPressed = false;
            let forwardKeyPressed = false;
            let lastUrl = window.location.href;
            let historyLength = window.history.length;

            // Helper to check if action is duplicate (using sessionStorage)
            const isDuplicate = (actionType) => {
                const lastAction = getLastAction();
                const now = Date.now();
                
                if (actionType === lastAction.type && (now - lastAction.time) < DUPLICATE_THRESHOLD) {
                    console.log(`?? Duplicate ${actionType} suppressed (${now - lastAction.time}ms since last)`);
                    return true;
                }
                
                setLastAction(actionType);
                console.log(`? Recording ${actionType} action`);
                return false;
            };

            // Track keyboard shortcuts for browser actions
            document.addEventListener('keydown', (event) => {
                if (!this.isRecording) return;

                // F5 or Ctrl+R = Refresh
                if (event.key === 'F5' || (event.ctrlKey && (event.key === 'r' || event.key === 'R'))) {
                    event.preventDefault(); // Prevent default to control refresh
                    
                    if (isDuplicate('refresh')) return;
                    
                    console.log('?? Refresh key detected (F5 / Ctrl+R)');
                    refreshKeyPressed = true;
                    this.recordBrowserAction('refresh', 'Page Refresh');
                    
                    // Now allow the refresh
                    setTimeout(() => {
                        window.location.reload();
                    }, 100);
                    
                    // Reset flag after delay
                    setTimeout(() => { refreshKeyPressed = false; }, 3000);
                    return;
                }

                // Alt+Left or Backspace = Browser Back
                if ((event.altKey && event.key === 'ArrowLeft') || 
                    (event.key === 'Backspace' && !event.target.matches('input, textarea, [contenteditable]'))) {
                    if (isDuplicate('back')) return;
                    
                    console.log('?? Browser Back key detected');
                    backKeyPressed = true;
                    this.recordBrowserAction('back', 'Browser Back');
                    
                    setTimeout(() => { backKeyPressed = false; }, 3000);
                    return;
                }

                // Alt+Right = Browser Forward
                if (event.altKey && event.key === 'ArrowRight') {
                    if (isDuplicate('forward')) return;
                    
                    console.log('?? Browser Forward key detected');
                    forwardKeyPressed = true;
                    this.recordBrowserAction('forward', 'Browser Forward');
                    
                    setTimeout(() => { forwardKeyPressed = false; }, 3000);
                    return;
                }
            }, true);

            // Track popstate (browser back/forward button clicks)
            window.addEventListener('popstate', (event) => {
                if (!this.isRecording) return;
                
                // Small delay to let URL change
                setTimeout(() => {
                    const currentUrl = window.location.href;
                    
                    // Check if keyboard shortcut was used
                    if (forwardKeyPressed) {
                        forwardKeyPressed = false;
                        return; // Already recorded
                    }
                    
                    if (backKeyPressed) {
                        backKeyPressed = false;
                        return; // Already recorded
                    }
                    
                    // It's a browser button click - default to 'back'
                    let direction = 'back';
                    
                    if (isDuplicate(direction)) return;
                    
                    console.log(`?? Browser ${direction} button detected`);
                    this.recordBrowserAction(direction, `Browser ${direction.charAt(0).toUpperCase() + direction.slice(1)}`);
                    
                    lastUrl = currentUrl;
                }, 100);
            });

            // Detect page refresh using beforeunload (ONE TIME ONLY per page load)
            let beforeUnloadFired = false;
            window.addEventListener('beforeunload', () => {
                if (!this.isRecording) return;
                if (beforeUnloadFired) return; // Prevent multiple fires
                
                beforeUnloadFired = true;

                // CRITICAL: Don't record refresh when intentionally closing browser
                // Check if we're stopping recording (page close via __stopRecording)
                if (window.__recordingStopping) {
                    console.log('?? beforeunload: Browser closing intentionally, not recording');
                    return;
                }

                // If refresh key was pressed, we already recorded it
                if (refreshKeyPressed) {
                    console.log('?? beforeunload: Refresh already recorded by keyboard');
                    return;
                }

                // Check if it's a refresh vs navigation
                if (!this.isNavigationClick) {
                    if (isDuplicate('refresh')) return;
                    
                    console.log('?? Page refresh detected (refresh button clicked)');
                    this.recordBrowserAction('refresh', 'Page Refresh');
                }
            });

            // Reset beforeUnload flag on page load
            window.addEventListener('load', () => {
                beforeUnloadFired = false;
                console.log('?? Page loaded, beforeUnload flag reset');
            });
        }

        static recordBrowserAction(action, description) {
            // Flush any pending clicks first
            if (this.clickTimeout && this.pendingClickElement) {
                clearTimeout(this.clickTimeout);
                this.recordClick(this.pendingClickElement);
                this.clickTimeout = null;
                this.pendingClickElement = null;
            }

            const actionData = {
                eventType: 'browser',
                actionType: action,
                selector: null,
                element: {
                    tag: 'browser',
                    text: description
                },
                timestamp: Date.now(),
                url: window.location.href
            };

            // Send action - use both normal send and beacon for reliability
            this.sendAction(actionData);
            
            // Also use sendBeacon for guaranteed delivery during page unload
            try {
                if (navigator.sendBeacon && typeof window.__playwrightRecordAction === 'function') {
                    // sendBeacon is more reliable during page unload
                    const blob = new Blob([JSON.stringify(actionData)], { type: 'application/json' });
                    navigator.sendBeacon('/api/recorder/action', blob);
                }
            } catch (e) {
                console.warn('sendBeacon failed:', e);
            }

            NavigationTracker.updateLastActionTime();
        }

        static handleClick(event) {
            if (!this.isRecording) return;

            const target = event.target;
            
            // Find the actionable element
            const actionableElement = ElementAnalyzer.findActionableElement(target);
            
            // Check if should exclude
            if (ElementAnalyzer.shouldExclude(actionableElement)) return;

            // CRITICAL FIX: Check if this is a navigation link
            const isNavigationLink = actionableElement.tagName.toLowerCase() === 'a' && 
                                     actionableElement.href && 
                                     !actionableElement.href.startsWith('javascript:') &&
                                     !actionableElement.href.startsWith('#');

            // If there's a pending click, record it immediately before starting new timer
            if (this.clickTimeout && this.pendingClickElement) {
                clearTimeout(this.clickTimeout);
                // Record the previous pending click
                if (!this.doubleClickDetected) {
                    this.recordClick(this.pendingClickElement);
                }
                this.doubleClickDetected = false;
            }

            // For navigation links, record IMMEDIATELY (no delay)
            // Because page will unload before 300ms timer completes
            if (isNavigationLink) {
                console.log('?? Navigation link detected - recording immediately');
                this.isNavigationClick = true;
                this.recordClick(actionableElement);
                
                // Reset flag after navigation
                setTimeout(() => {
                    this.isNavigationClick = false;
                }, 1000);
                
                return; // Don't set timer for navigation links
            }

            // For other elements, use delayed recording to detect double-clicks
            this.pendingClickElement = actionableElement;

            // Delay click recording to detect if it's part of a double-click
            this.clickTimeout = setTimeout(() => {
                // If no double-click was detected, record the single click
                if (!this.doubleClickDetected) {
                    this.recordClick(actionableElement);
                }
                this.doubleClickDetected = false;
                this.pendingClickElement = null;
                this.clickTimeout = null;
            }, 300);
        }

        static recordClick(actionableElement) {
            // Highlight the element
            ElementHighlighter.highlightElement(actionableElement);

            // Generate selector
            const selector = SmartLocatorGenerator.generateSelector(actionableElement);
            const fallbackSelectors = SmartLocatorGenerator.generateFallbackSelectors(actionableElement);

            // Check Shadow DOM
            const shadowInfo = ShadowDOMHandler.isInShadowDOM(actionableElement) 
                ? ShadowDOMHandler.generateShadowSelector(actionableElement)
                : null;

            // Check iframe context
            const inIFrame = IFrameHandler.isInIFrame();
            const iframeSelector = inIFrame ? IFrameHandler.getIFrameSelector() : null;

            // Normalize action
            const normalizedAction = ActionNormalizer.normalizeAction('click', actionableElement);

            // Get element text
            let text = '';
            if (actionableElement.textContent) {
                text = actionableElement.textContent.trim().substring(0, 50);
            }

            // Send action to recorder
            this.sendAction({
                eventType: 'click',
                actionType: normalizedAction.action,
                selector: selector,
                fallbackSelectors: fallbackSelectors,
                element: {
                    tag: actionableElement.tagName.toLowerCase(),
                    text: text,
                    type: actionableElement.getAttribute('type'),
                    id: actionableElement.id,
                    classes: Array.from(actionableElement.classList || [])
                },
                shadow: shadowInfo,
                iframe: {
                    inIFrame: inIFrame,
                    iframeSelector: iframeSelector
                },
                timestamp: Date.now(),
                url: window.location.href
            });

            NavigationTracker.updateLastActionTime();
        }

        static handleDblClick(event) {
            if (!this.isRecording) return;

            // Mark that a double-click was detected
            this.doubleClickDetected = true;
            
            // Clear any pending click timeout
            if (this.clickTimeout) {
                clearTimeout(this.clickTimeout);
                this.clickTimeout = null;
            }

            const actionableElement = ElementAnalyzer.findActionableElement(event.target);
            if (ElementAnalyzer.shouldExclude(actionableElement)) return;

            ElementHighlighter.highlightElement(actionableElement);

            const selector = SmartLocatorGenerator.generateSelector(actionableElement);

            this.sendAction({
                eventType: 'dblclick',
                actionType: 'dblclick',
                selector: selector,
                element: {
                    tag: actionableElement.tagName.toLowerCase(),
                    text: actionableElement.textContent ? actionableElement.textContent.trim().substring(0, 50) : ''
                },
                iframe: {
                    inIFrame: IFrameHandler.isInIFrame(),
                    iframeSelector: IFrameHandler.getIFrameSelector()
                },
                timestamp: Date.now()
            });

            NavigationTracker.updateLastActionTime();
        }

        static handleRightClick(event) {
            if (!this.isRecording) return;

            const actionableElement = ElementAnalyzer.findActionableElement(event.target);
            if (ElementAnalyzer.shouldExclude(actionableElement)) return;

            ElementHighlighter.highlightElement(actionableElement);

            const selector = SmartLocatorGenerator.generateSelector(actionableElement);

            this.sendAction({
                eventType: 'contextmenu',
                actionType: 'rightclick',
                selector: selector,
                element: {
                    tag: actionableElement.tagName.toLowerCase(),
                    text: actionableElement.textContent ? actionableElement.textContent.trim().substring(0, 50) : ''
                },
                iframe: {
                    inIFrame: IFrameHandler.isInIFrame(),
                    iframeSelector: IFrameHandler.getIFrameSelector()
                },
                timestamp: Date.now()
            });

            NavigationTracker.updateLastActionTime();
        }

        static handleInput(event) {
            if (!this.isRecording) return;

            const element = event.target;
            const tag = element.tagName.toLowerCase();

            // Only handle input/textarea
            if (tag !== 'input' && tag !== 'textarea') return;

            if (ElementAnalyzer.shouldExclude(element)) return;

            const selector = SmartLocatorGenerator.generateSelector(element);
            const actionKey = selector + '_input';

            // Debounce input events - only send after user stops typing
            if (this.pendingInputActions[actionKey]) {
                clearTimeout(this.pendingInputActions[actionKey]);
            }

            this.pendingInputActions[actionKey] = setTimeout(() => {
                const normalizedAction = ActionNormalizer.normalizeAction('input', element);

                this.sendAction({
                    eventType: 'input',
                    actionType: normalizedAction.action,
                    selector: selector,
                    value: element.value,
                    element: {
                        tag: tag,
                        type: element.getAttribute('type'),
                        name: element.getAttribute('name')
                    },
                    iframe: {
                        inIFrame: IFrameHandler.isInIFrame(),
                        iframeSelector: IFrameHandler.getIFrameSelector()
                    },
                    timestamp: Date.now()
                });

                NavigationTracker.updateLastActionTime();
                delete this.pendingInputActions[actionKey];
            }, RECORDER_CONFIG.debounceDelay);
        }

        static handleChange(event) {
            if (!this.isRecording) return;

            const element = event.target;
            if (ElementAnalyzer.shouldExclude(element)) return;

            const selector = SmartLocatorGenerator.generateSelector(element);
            const normalizedAction = ActionNormalizer.normalizeAction('change', element);

            ElementHighlighter.highlightElement(element);

            this.sendAction({
                eventType: 'change',
                actionType: normalizedAction.action,
                selector: selector,
                value: normalizedAction.value,
                text: normalizedAction.text,
                element: {
                    tag: element.tagName.toLowerCase(),
                    type: element.getAttribute('type'),
                    checked: element.checked
                },
                iframe: {
                    inIFrame: IFrameHandler.isInIFrame(),
                    iframeSelector: IFrameHandler.getIFrameSelector()
                },
                timestamp: Date.now()
            });

            NavigationTracker.updateLastActionTime();
        }

        static handleKeyDown(event) {
            if (!this.isRecording) return;

            const element = event.target;
            if (ElementAnalyzer.shouldExclude(element)) return;

            const normalizedAction = ActionNormalizer.normalizeAction('keydown', element, { key: event.key });
            
            // Only record specific keys (Enter, Tab, Escape)
            if (!normalizedAction) return;

            const selector = SmartLocatorGenerator.generateSelector(element);

            this.sendAction({
                eventType: 'keydown',
                actionType: normalizedAction.action,
                selector: selector,
                key: event.key,
                element: {
                    tag: element.tagName.toLowerCase()
                },
                iframe: {
                    inIFrame: IFrameHandler.isInIFrame(),
                    iframeSelector: IFrameHandler.getIFrameSelector()
                },
                timestamp: Date.now()
            });

            NavigationTracker.updateLastActionTime();
        }

        static handleSubmit(event) {
            if (!this.isRecording) return;

            const form = event.target;
            if (ElementAnalyzer.shouldExclude(form)) return;

            const selector = SmartLocatorGenerator.generateSelector(form);

            this.sendAction({
                eventType: 'submit',
                actionType: 'submit',
                selector: selector,
                element: {
                    tag: 'form',
                    action: form.action,
                    method: form.method
                },
                iframe: {
                    inIFrame: IFrameHandler.isInIFrame(),
                    iframeSelector: IFrameHandler.getIFrameSelector()
                },
                timestamp: Date.now()
            });

            NavigationTracker.updateLastActionTime();
        }

        static sendAction(actionData) {
            try {
                // Send to C# recorder via exposed function
                if (typeof window.__playwrightRecordAction === 'function') {
                    window.__playwrightRecordAction(actionData);
                    console.log('?? Recorded:', actionData.actionType, actionData.selector);
                } else {
                    console.error('? Recorder function not available');
                }
            } catch (error) {
                console.error('? Error sending action:', error);
            }
        }

        static startNavigationTracking() {
            // Track URL changes (for SPA navigation)
            setInterval(() => {
                NavigationTracker.trackNavigation();
            }, 500);

            // Track page load
            window.addEventListener('load', () => {
                NavigationTracker.trackNavigation();
            });

            // Track history changes
            const originalPushState = history.pushState;
            history.pushState = function(...args) {
                originalPushState.apply(history, args);
                NavigationTracker.trackNavigation();
            };

            const originalReplaceState = history.replaceState;
            history.replaceState = function(...args) {
                originalReplaceState.apply(history, args);
                NavigationTracker.trackNavigation();
            };

            window.addEventListener('popstate', () => {
                NavigationTracker.trackNavigation();
            });

            // CRITICAL: Flush pending clicks before page unload/navigation
            window.addEventListener('beforeunload', () => {
                console.log('?? Page unloading - flushing pending clicks');
                if (this.clickTimeout && this.pendingClickElement) {
                    clearTimeout(this.clickTimeout);
                    if (!this.doubleClickDetected) {
                        this.recordClick(this.pendingClickElement);
                    }
                    this.clickTimeout = null;
                    this.pendingClickElement = null;
                }
            });

            // ALSO: Flush on visibility change (when tab switches or page navigates)
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'hidden') {
                    console.log('?? Page hidden - flushing pending clicks');
                    if (this.clickTimeout && this.pendingClickElement) {
                        clearTimeout(this.clickTimeout);
                        if (!this.doubleClickDetected) {
                            this.recordClick(this.pendingClickElement);
                        }
                        this.clickTimeout = null;
                        this.pendingClickElement = null;
                    }
                }
            });
        }

        static stop() {
            this.isRecording = false;
            console.log('?? Recording stopped');
        }
    }

    // ============================================================================
    // INITIALIZE RECORDER
    // ============================================================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            RecorderController.initialize();
        });
    } else {
        RecorderController.initialize();
    }

    // Expose control functions globally
    window.__recorderController = RecorderController;
    window.__elementAnalyzer = ElementAnalyzer;
    window.__smartLocatorGenerator = SmartLocatorGenerator;

    console.log('?? Enhanced Test Recorder loaded successfully');
})();
