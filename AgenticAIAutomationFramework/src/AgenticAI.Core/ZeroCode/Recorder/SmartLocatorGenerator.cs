using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AgenticAI.Core.ZeroCode.Recorder
{
    /// <summary>
    /// Smart Locator Generator - Generates stable and maintainable element locators
    /// Priority: data-testid > id > name > aria-label > role > text content > CSS selector > XPath fallback
    /// Detects and ignores dynamic IDs
    /// </summary>
    public class SmartLocatorGenerator
    {
        // Regex patterns to detect dynamic IDs
        private static readonly Regex DynamicIdPattern = new Regex(
            @"(\d{4,})|([a-f0-9]{8,})|(_\d+$)|(^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$)|(timestamp)|(random)|(uuid)|(guid)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Gets the JavaScript code to inject into the browser for smart locator generation
        /// </summary>
        public string GetInjectionScript()
        {
            return @"
(function() {
    'use strict';
    
    // Smart Locator Generator
    window.__smartLocatorGenerator = {
        
        /**
         * Dynamic ID detection patterns
         */
        dynamicIdPatterns: [
            /\d{4,}/,                           // Long sequences of digits (timestamps, etc.)
            /[a-f0-9]{8,}/,                     // Long hex strings
            /_\d+$/,                            // IDs ending with underscore and number
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, // UUIDs
            /timestamp|random|uuid|guid/i,      // Common dynamic ID keywords
            /^\d+$/,                            // Pure numeric IDs
            /temp|tmp|generated/i               // Temporary/generated IDs
        ],
        
        /**
         * Check if an ID appears to be dynamically generated
         */
        isDynamicId: function(id) {
            if (!id || typeof id !== 'string') return true;
            
            return this.dynamicIdPatterns.some(pattern => pattern.test(id));
        },
        
        /**
         * Generate locator with priority strategy
         */
        generateLocator: function(element) {
            if (!element) return { locator: '', strategy: 'none', confidence: 0 };
            
            // Priority 1: data-testid (highest priority)
            const testIdLocator = this.getTestIdLocator(element);
            if (testIdLocator) return testIdLocator;
            
            // Priority 2: Stable ID (non-dynamic)
            const idLocator = this.getIdLocator(element);
            if (idLocator) return idLocator;
            
            // Priority 3: Name attribute
            const nameLocator = this.getNameLocator(element);
            if (nameLocator) return nameLocator;
            
            // Priority 4: aria-label
            const ariaLabelLocator = this.getAriaLabelLocator(element);
            if (ariaLabelLocator) return ariaLabelLocator;
            
            // Priority 5: Role + accessible name
            const roleLocator = this.getRoleLocator(element);
            if (roleLocator) return roleLocator;
            
            // Priority 6: Text content (for buttons, links)
            const textLocator = this.getTextLocator(element);
            if (textLocator) return textLocator;
            
            // Priority 7: Stable CSS selector
            const cssLocator = this.getCssLocator(element);
            if (cssLocator) return cssLocator;
            
            // Priority 8: XPath fallback
            const xpathLocator = this.getXPathLocator(element);
            return xpathLocator;
        },
        
        /**
         * Priority 1: data-testid attributes
         */
        getTestIdLocator: function(element) {
            const testIdAttrs = ['data-testid', 'data-test-id', 'data-test', 'data-qa', 'data-cy'];
            
            for (const attr of testIdAttrs) {
                const value = element.getAttribute(attr);
                if (value && value.trim()) {
                    return {
                        locator: '[' + attr + '=""' + value + '""]',
                        strategy: 'data-testid',
                        confidence: 95,
                        attribute: attr,
                        value: value
                    };
                }
            }
            
            return null;
        },
        
        /**
         * Priority 2: Stable ID (non-dynamic)
         */
        getIdLocator: function(element) {
            const id = element.id;
            
            if (id && !this.isDynamicId(id)) {
                return {
                    locator: '#' + id,
                    strategy: 'id',
                    confidence: 90,
                    value: id
                };
            }
            
            return null;
        },
        
        /**
         * Priority 3: Name attribute
         */
        getNameLocator: function(element) {
            const name = element.getAttribute('name');
            
            if (name && name.trim()) {
                // Check if multiple elements share this name
                const tagName = element.tagName.toLowerCase();
                const selector = tagName + '[name=""' + name + '""]';
                const matches = document.querySelectorAll(selector);
                
                if (matches.length === 1) {
                    return {
                        locator: selector,
                        strategy: 'name',
                        confidence: 85,
                        value: name
                    };
                } else if (element.type) {
                    // Add type to make it more specific
                    const specificSelector = tagName + '[name=""' + name + '""][type=""' + element.type + '""]';
                    return {
                        locator: specificSelector,
                        strategy: 'name',
                        confidence: 80,
                        value: name
                    };
                }
            }
            
            return null;
        },
        
        /**
         * Priority 4: aria-label
         */
        getAriaLabelLocator: function(element) {
            const ariaLabel = element.getAttribute('aria-label');
            
            if (ariaLabel && ariaLabel.trim()) {
                const tagName = element.tagName.toLowerCase();
                return {
                    locator: tagName + '[aria-label=""' + ariaLabel + '""]',
                    strategy: 'aria-label',
                    confidence: 80,
                    value: ariaLabel
                };
            }
            
            return null;
        },
        
        /**
         * Priority 5: Role + accessible name
         */
        getRoleLocator: function(element) {
            const role = element.getAttribute('role') || this.getImplicitRole(element);
            
            if (role) {
                const accessibleName = this.getAccessibleName(element);
                
                if (accessibleName) {
                    return {
                        locator: '[role=""' + role + '""][aria-label=""' + accessibleName + '""]',
                        strategy: 'role',
                        confidence: 75,
                        role: role,
                        value: accessibleName
                    };
                }
                
                return {
                    locator: '[role=""' + role + '""]',
                    strategy: 'role',
                    confidence: 60,
                    role: role
                };
            }
            
            return null;
        },
        
        /**
         * Priority 6: Text content (for buttons, links, labels)
         */
        getTextLocator: function(element) {
            const tagName = element.tagName.toLowerCase();
            const textElements = ['a', 'button', 'label', 'span'];
            
            if (textElements.includes(tagName)) {
                const text = this.getVisibleText(element);
                
                if (text && text.length > 0 && text.length < 50) {
                    // Escape special characters
                    const escapedText = text.replace(/[""']/g, '\\$&');
                    
                    return {
                        locator: tagName + ':has-text(""' + escapedText + '"")',
                        strategy: 'text',
                        confidence: 70,
                        value: text
                    };
                }
            }
            
            return null;
        },
        
        /**
         * Priority 7: Stable CSS selector
         */
        getCssLocator: function(element) {
            const tagName = element.tagName.toLowerCase();
            let selector = tagName;
            
            // Add stable classes (ignore dynamic/utility classes)
            const stableClasses = this.getStableClasses(element);
            if (stableClasses.length > 0) {
                selector += '.' + stableClasses.join('.');
            }
            
            // Add type attribute for inputs
            if (element.type && tagName === 'input') {
                selector += '[type=""' + element.type + '""]';
            }
            
            // Add placeholder as additional specificity
            const placeholder = element.getAttribute('placeholder');
            if (placeholder && placeholder.trim()) {
                selector += '[placeholder=""' + placeholder + '""]';
            }
            
            // Check uniqueness
            const matches = document.querySelectorAll(selector);
            if (matches.length === 1 && matches[0] === element) {
                return {
                    locator: selector,
                    strategy: 'css',
                    confidence: 65
                };
            }
            
            // Add nth-child if not unique
            const parent = element.parentElement;
            if (parent) {
                const siblings = Array.from(parent.children).filter(e => e.tagName === element.tagName);
                const index = siblings.indexOf(element);
                
                if (index >= 0) {
                    selector += ':nth-of-type(' + (index + 1) + ')';
                    return {
                        locator: selector,
                        strategy: 'css',
                        confidence: 55
                    };
                }
            }
            
            return {
                locator: selector,
                strategy: 'css',
                confidence: 50
            };
        },
        
        /**
         * Priority 8: XPath fallback
         */
        getXPathLocator: function(element) {
            const xpath = this.generateXPath(element);
            return {
                locator: xpath,
                strategy: 'xpath',
                confidence: 40
            };
        },
        
        /**
         * Helper: Get stable classes (filter out dynamic/utility classes)
         */
        getStableClasses: function(element) {
            if (!element.className || typeof element.className !== 'string') {
                return [];
            }
            
            const classes = element.className.split(/\s+/).filter(c => c.trim());
            
            // Filter out dynamic and utility classes
            return classes.filter(cls => {
                // Ignore classes with numbers (often dynamic)
                if (/\d{3,}/.test(cls)) return false;
                
                // Ignore common utility/framework classes
                const ignorePatterns = [
                    /^ng-/,           // Angular
                    /^mat-/,          // Material
                    /^css-/,          // CSS-in-JS
                    /^_/,             // Styled-components
                    /^jsx-/,          // JSX
                    /^sc-/,           // Styled-components
                    /^emotion-/,      // Emotion
                    /^MuiBox/,        // Material-UI
                    /^tailwind/,      // Tailwind random classes
                    /^p-\d+$/,        // Utility classes like p-1, m-2
                    /^m-\d+$/,
                    /^w-\d+$/,
                    /^h-\d+$/
                ];
                
                return !ignorePatterns.some(pattern => pattern.test(cls));
            });
        },
        
        /**
         * Helper: Get visible text from element
         */
        getVisibleText: function(element) {
            if (!element) return '';
            
            // Get text content, trimmed
            const text = (element.textContent || element.innerText || '').trim();
            
            // Filter out text from hidden child elements
            const clone = element.cloneNode(true);
            const hiddenElements = clone.querySelectorAll('[style*=""display: none""], [style*=""visibility: hidden""]');
            hiddenElements.forEach(el => el.remove());
            
            return (clone.textContent || clone.innerText || '').trim();
        },
        
        /**
         * Helper: Get accessible name for element
         */
        getAccessibleName: function(element) {
            // aria-label
            const ariaLabel = element.getAttribute('aria-label');
            if (ariaLabel) return ariaLabel;
            
            // aria-labelledby
            const labelledBy = element.getAttribute('aria-labelledby');
            if (labelledBy) {
                const labelElement = document.getElementById(labelledBy);
                if (labelElement) return labelElement.textContent.trim();
            }
            
            // Associated label (for inputs)
            if (element.id) {
                const label = document.querySelector('label[for=""' + element.id + '""]');
                if (label) return label.textContent.trim();
            }
            
            // Title attribute
            const title = element.getAttribute('title');
            if (title) return title;
            
            return '';
        },
        
        /**
         * Helper: Get implicit ARIA role for element
         */
        getImplicitRole: function(element) {
            const tagName = element.tagName.toLowerCase();
            const roleMap = {
                'a': 'link',
                'button': 'button',
                'input': element.type === 'checkbox' ? 'checkbox' : 
                        element.type === 'radio' ? 'radio' : 'textbox',
                'textarea': 'textbox',
                'select': 'combobox',
                'nav': 'navigation',
                'main': 'main',
                'header': 'banner',
                'footer': 'contentinfo',
                'article': 'article',
                'section': 'region',
                'aside': 'complementary',
                'form': 'form',
                'img': 'img',
                'ul': 'list',
                'ol': 'list',
                'li': 'listitem',
                'table': 'table',
                'th': 'columnheader',
                'td': 'cell'
            };
            
            return roleMap[tagName] || null;
        },
        
        /**
         * Helper: Generate XPath for element
         */
        generateXPath: function(element) {
            if (!element) return '';
            
            if (element.id && !this.isDynamicId(element.id)) {
                return '//*[@id=""' + element.id + '""]';
            }
            
            const parts = [];
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
                const nth = index > 0 ? '[' + (index + 1) + ']' : '';
                parts.unshift(tagName + nth);
                
                current = current.parentNode;
            }
            
            return '/' + parts.join('/');
        }
    };
    
    console.log('[SmartLocatorGenerator] Smart locator generator initialized');
})();
";
        }

        /// <summary>
        /// Generate a locator for an element based on captured event
        /// </summary>
        public LocatorResult GenerateLocator(CapturedEvent capturedEvent)
        {
            // Priority-based locator generation
            
            // Priority 1: data-testid
            if (TryGetTestIdLocator(capturedEvent, out var testIdLocator))
                return testIdLocator;
            
            // Priority 2: Stable ID (non-dynamic)
            if (TryGetIdLocator(capturedEvent, out var idLocator))
                return idLocator;
            
            // Priority 3: Name attribute
            if (TryGetNameLocator(capturedEvent, out var nameLocator))
                return nameLocator;
            
            // Priority 4: aria-label
            if (TryGetAriaLabelLocator(capturedEvent, out var ariaLocator))
                return ariaLocator;
            
            // Priority 5: Role
            if (TryGetRoleLocator(capturedEvent, out var roleLocator))
                return roleLocator;
            
            // Priority 6: Text content
            if (TryGetTextLocator(capturedEvent, out var textLocator))
                return textLocator;
            
            // Priority 7: CSS selector
            if (TryGetCssLocator(capturedEvent, out var cssLocator))
                return cssLocator;
            
            // Priority 8: XPath fallback
            return GetXPathLocator(capturedEvent);
        }

        private bool TryGetTestIdLocator(CapturedEvent evt, out LocatorResult result)
        {
            var testIdAttrs = new[] { "data-testid", "data-test-id", "data-test", "data-qa", "data-cy" };
            
            foreach (var attr in testIdAttrs)
            {
                if (evt.Target.Attributes.TryGetValue(attr, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    result = new LocatorResult
                    {
                        Locator = $"[{attr}='{value}']",
                        Strategy = "data-testid",
                        Confidence = 95
                    };
                    return true;
                }
            }
            
            result = null!;
            return false;
        }

        private bool TryGetIdLocator(CapturedEvent evt, out LocatorResult result)
        {
            if (!string.IsNullOrWhiteSpace(evt.Target.Id) && !IsDynamicId(evt.Target.Id))
            {
                result = new LocatorResult
                {
                    Locator = $"#{evt.Target.Id}",
                    Strategy = "id",
                    Confidence = 90
                };
                return true;
            }
            
            result = null!;
            return false;
        }

        private bool TryGetNameLocator(CapturedEvent evt, out LocatorResult result)
        {
            if (evt.Target.Attributes.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                var selector = $"{evt.Target.TagName.ToLower()}[name='{name}']";
                
                if (!string.IsNullOrWhiteSpace(evt.Target.Type))
                {
                    selector = $"{selector}[type='{evt.Target.Type}']";
                }
                
                result = new LocatorResult
                {
                    Locator = selector,
                    Strategy = "name",
                    Confidence = 85
                };
                return true;
            }
            
            result = null!;
            return false;
        }

        private bool TryGetAriaLabelLocator(CapturedEvent evt, out LocatorResult result)
        {
            if (evt.Target.Attributes.TryGetValue("aria-label", out var ariaLabel) && !string.IsNullOrWhiteSpace(ariaLabel))
            {
                result = new LocatorResult
                {
                    Locator = $"{evt.Target.TagName.ToLower()}[aria-label='{ariaLabel}']",
                    Strategy = "aria-label",
                    Confidence = 80
                };
                return true;
            }
            
            result = null!;
            return false;
        }

        private bool TryGetRoleLocator(CapturedEvent evt, out LocatorResult result)
        {
            if (evt.Target.Attributes.TryGetValue("role", out var role) && !string.IsNullOrWhiteSpace(role))
            {
                result = new LocatorResult
                {
                    Locator = $"[role='{role}']",
                    Strategy = "role",
                    Confidence = 70
                };
                return true;
            }
            
            result = null!;
            return false;
        }

        private bool TryGetTextLocator(CapturedEvent evt, out LocatorResult result)
        {
            var textElements = new[] { "A", "BUTTON", "LABEL", "SPAN" };
            
            if (Array.Exists(textElements, tag => tag.Equals(evt.Target.TagName, StringComparison.OrdinalIgnoreCase)))
            {
                var text = evt.Target.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length < 50)
                {
                    result = new LocatorResult
                    {
                        Locator = $"{evt.Target.TagName.ToLower()}:has-text('{text}')",
                        Strategy = "text",
                        Confidence = 65
                    };
                    return true;
                }
            }
            
            result = null!;
            return false;
        }

        private bool TryGetCssLocator(CapturedEvent evt, out LocatorResult result)
        {
            var selector = evt.Target.TagName.ToLower();
            
            if (!string.IsNullOrWhiteSpace(evt.Target.ClassName))
            {
                var classes = evt.Target.ClassName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var stableClasses = classes.Where(c => !IsUtilityClass(c)).ToList();
                
                if (stableClasses.Any())
                {
                    selector += "." + string.Join(".", stableClasses.Take(2));
                }
            }
            
            result = new LocatorResult
            {
                Locator = selector,
                Strategy = "css",
                Confidence = 55
            };
            return true;
        }

        private LocatorResult GetXPathLocator(CapturedEvent evt)
        {
            return new LocatorResult
            {
                Locator = $"//{evt.Target.TagName.ToLower()}",
                Strategy = "xpath",
                Confidence = 40
            };
        }

        private bool IsDynamicId(string id)
        {
            return DynamicIdPattern.IsMatch(id);
        }

        private bool IsUtilityClass(string className)
        {
            var utilityPatterns = new[] { "ng-", "mat-", "css-", "_", "jsx-", "sc-", "emotion-", "p-", "m-", "w-", "h-" };
            return utilityPatterns.Any(pattern => className.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Represents a generated locator with its strategy and confidence level
    /// </summary>
    public class LocatorResult
    {
        public string Locator { get; set; } = "";
        public string Strategy { get; set; } = "";
        public int Confidence { get; set; }
    }
}
