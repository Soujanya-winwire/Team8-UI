using System;

namespace AgenticAI.Core.ZeroCode.Recorder
{
    /// <summary>
    /// Element Analyzer - Resolves the actionable parent element when a child element is clicked
    /// Identifies the best element to interact with for stable automation
    /// </summary>
    public class ElementAnalyzer
    {
        /// <summary>
        /// Gets the JavaScript code to inject into the browser for element analysis
        /// </summary>
        public string GetInjectionScript()
        {
            return @"
(function() {
    'use strict';
    
    // Element Analyzer - Finds the most actionable element
    window.__elementAnalyzer = {
        
        /**
         * Find the actionable parent element
         * Returns the best element to interact with (button, link, input, etc.)
         */
        findActionableElement: function(element) {
            if (!element) return null;
            
            // If element itself is actionable, return it
            if (this.isActionable(element)) {
                return element;
            }
            
            // Traverse up the DOM to find actionable parent
            let current = element.parentElement;
            let depth = 0;
            const maxDepth = 5; // Don't go too far up
            
            while (current && depth < maxDepth) {
                if (this.isActionable(current)) {
                    return current;
                }
                current = current.parentElement;
                depth++;
            }
            
            // If no actionable parent found, return original element
            return element;
        },
        
        /**
         * Check if element is actionable (clickable, focusable, etc.)
         */
        isActionable: function(element) {
            if (!element || !element.tagName) return false;
            
            const tagName = element.tagName.toLowerCase();
            
            // Native actionable elements
            const actionableTags = ['a', 'button', 'input', 'select', 'textarea', 
                                   'option', 'label', 'summary'];
            if (actionableTags.includes(tagName)) {
                return true;
            }
            
            // Elements with click handlers or role
            if (element.onclick || element.getAttribute('onclick')) {
                return true;
            }
            
            const role = element.getAttribute('role');
            const clickableRoles = ['button', 'link', 'menuitem', 'tab', 'checkbox', 
                                   'radio', 'switch', 'option'];
            if (role && clickableRoles.includes(role)) {
                return true;
            }
            
            // Elements with tabindex (focusable)
            const tabIndex = element.getAttribute('tabindex');
            if (tabIndex !== null && tabIndex !== '-1') {
                return true;
            }
            
            // Elements with cursor pointer
            const cursor = window.getComputedStyle(element).cursor;
            if (cursor === 'pointer') {
                return true;
            }
            
            return false;
        },
        
        /**
         * Get element hierarchy for context
         */
        getElementHierarchy: function(element, maxDepth = 3) {
            const hierarchy = [];
            let current = element;
            let depth = 0;
            
            while (current && depth < maxDepth) {
                hierarchy.push({
                    tagName: current.tagName,
                    id: current.id || null,
                    className: current.className || null,
                    role: current.getAttribute('role') || null,
                    isActionable: this.isActionable(current)
                });
                current = current.parentElement;
                depth++;
            }
            
            return hierarchy;
        },
        
        /**
         * Analyze element context (what type of component it belongs to)
         */
        analyzeElementContext: function(element) {
            const context = {
                isFormElement: false,
                isNavigationElement: false,
                isContentElement: false,
                isMenuElement: false,
                isModalElement: false,
                componentType: 'unknown'
            };
            
            // Check if in form
            const form = element.closest('form');
            if (form) {
                context.isFormElement = true;
                context.componentType = 'form';
            }
            
            // Check if in navigation
            const nav = element.closest('nav, [role=""navigation""]');
            if (nav) {
                context.isNavigationElement = true;
                context.componentType = 'navigation';
            }
            
            // Check if in menu
            const menu = element.closest('[role=""menu""], [role=""menubar""]');
            if (menu) {
                context.isMenuElement = true;
                context.componentType = 'menu';
            }
            
            // Check if in modal/dialog
            const modal = element.closest('[role=""dialog""], [role=""alertdialog""], .modal, .dialog');
            if (modal) {
                context.isModalElement = true;
                context.componentType = 'modal';
            }
            
            // Check if content element
            const contentRoles = ['article', 'main', 'region', 'complementary'];
            const contentElement = element.closest(contentRoles.map(r => '[role=""' + r + '""]').join(','));
            if (contentElement) {
                context.isContentElement = true;
                if (context.componentType === 'unknown') {
                    context.componentType = 'content';
                }
            }
            
            return context;
        },
        
        /**
         * Check if element is in Shadow DOM
         */
        isInShadowDOM: function(element) {
            let parent = element;
            while (parent) {
                if (parent instanceof ShadowRoot) {
                    return true;
                }
                parent = parent.parentNode;
            }
            return false;
        },
        
        /**
         * Get Shadow DOM path if element is in Shadow DOM
         */
        getShadowDOMPath: function(element) {
            const path = [];
            let current = element;
            
            while (current) {
                if (current instanceof ShadowRoot) {
                    path.unshift({
                        type: 'shadow-root',
                        host: this.getElementIdentifier(current.host)
                    });
                    current = current.host;
                } else {
                    current = current.parentNode;
                }
            }
            
            return path.length > 0 ? path : null;
        },
        
        /**
         * Get a simple element identifier
         */
        getElementIdentifier: function(element) {
            if (!element) return 'unknown';
            if (element.id) return '#' + element.id;
            if (element.className && typeof element.className === 'string') {
                const classes = element.className.split(/\s+/).filter(c => c);
                if (classes.length > 0) return '.' + classes[0];
            }
            return element.tagName.toLowerCase();
        },
        
        /**
         * Check if element is visible and interactable
         */
        isElementInteractable: function(element) {
            if (!element) return false;
            
            // Check if element is displayed
            const style = window.getComputedStyle(element);
            if (style.display === 'none' || style.visibility === 'hidden') {
                return false;
            }
            
            // Check if element has size
            const rect = element.getBoundingClientRect();
            if (rect.width === 0 && rect.height === 0) {
                return false;
            }
            
            // Check if element is disabled
            if (element.disabled || element.getAttribute('aria-disabled') === 'true') {
                return false;
            }
            
            return true;
        },
        
        /**
         * Get element position info
         */
        getElementPosition: function(element) {
            const rect = element.getBoundingClientRect();
            return {
                x: rect.left + window.scrollX,
                y: rect.top + window.scrollY,
                width: rect.width,
                height: rect.height,
                inViewport: (
                    rect.top >= 0 &&
                    rect.left >= 0 &&
                    rect.bottom <= window.innerHeight &&
                    rect.right <= window.innerWidth
                )
            };
        },
        
        /**
         * Complete element analysis
         */
        analyzeElement: function(element) {
            const actionableElement = this.findActionableElement(element);
            
            return {
                originalElement: {
                    tagName: element.tagName,
                    id: element.id || null,
                    className: element.className || null
                },
                actionableElement: {
                    tagName: actionableElement.tagName,
                    id: actionableElement.id || null,
                    className: actionableElement.className || null,
                    role: actionableElement.getAttribute('role') || null
                },
                hierarchy: this.getElementHierarchy(actionableElement),
                context: this.analyzeElementContext(actionableElement),
                isActionResolved: element !== actionableElement,
                isInShadowDOM: this.isInShadowDOM(element),
                shadowDOMPath: this.getShadowDOMPath(element),
                isInteractable: this.isElementInteractable(actionableElement),
                position: this.getElementPosition(actionableElement)
            };
        }
    };
    
    console.log('[ElementAnalyzer] Element analyzer initialized');
})();
";
        }

        /// <summary>
        /// Analyze an element based on captured event data
        /// </summary>
        public AnalyzedElement AnalyzeElement(CapturedEvent capturedEvent)
        {
            // This is a server-side placeholder for element analysis
            // The actual analysis happens in the browser via the injected script
            // This method processes the results returned from the browser
            
            return new AnalyzedElement
            {
                TagName = capturedEvent.Target.TagName,
                Id = capturedEvent.Target.Id,
                ClassName = capturedEvent.Target.ClassName,
                IsActionable = IsActionableTagName(capturedEvent.Target.TagName),
                Context = DetermineContext(capturedEvent)
            };
        }

        private bool IsActionableTagName(string tagName)
        {
            var actionableTags = new[] { "A", "BUTTON", "INPUT", "SELECT", "TEXTAREA", "OPTION", "LABEL" };
            return Array.Exists(actionableTags, tag => tag.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        }

        private string DetermineContext(CapturedEvent capturedEvent)
        {
            // Basic context determination based on tag name
            var tagName = capturedEvent.Target.TagName.ToUpper();
            
            if (tagName == "INPUT" || tagName == "TEXTAREA" || tagName == "SELECT")
                return "form";
            
            if (tagName == "A")
                return "navigation";
            
            if (tagName == "BUTTON")
                return "interaction";
            
            return "content";
        }
    }

    /// <summary>
    /// Represents an analyzed element with its actionable properties
    /// </summary>
    public class AnalyzedElement
    {
        public string TagName { get; set; } = "";
        public string Id { get; set; } = "";
        public string ClassName { get; set; } = "";
        public bool IsActionable { get; set; }
        public string Context { get; set; } = "";
    }
}
