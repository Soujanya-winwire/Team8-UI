using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Highlights elements during test execution for debugging
    /// </summary>
    public class ElementHighlighter
    {
        /// <summary>
        /// Highlight an element with a red border
        /// </summary>
        public static async Task HighlightElementAsync(IWebDriver driver, string locator, int durationMs = 2000)
        {
            try
            {
                const string highlightScript = @"
                    var element = arguments[0];
                    var originalBorder = element.style.border;
                    element.style.border = '3px solid red';
                    element.style.boxShadow = '0 0 20px rgba(255, 0, 0, 0.8)';
                    
                    setTimeout(() => {
                        element.style.border = originalBorder;
                        element.style.boxShadow = '';
                    }, arguments[1]);
                ";

                var element = await driver.FindElementAsync(locator);
                await driver.ExecuteScriptAsync(highlightScript);
                Logger.Debug($"Element highlighted: {locator}");
                await Task.Delay(durationMs);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error highlighting element: {ex.Message}");
            }
        }

        /// <summary>
        /// Highlight multiple elements
        /// </summary>
        public static async Task HighlightElementsAsync(IWebDriver driver, string locator, int durationMs = 2000)
        {
            try
            {
                const string highlightMultipleScript = @"
                    var elements = document.querySelectorAll(arguments[0]);
                    var originalStates = [];
                    
                    elements.forEach(el => {
                        originalStates.push({
                            border: el.style.border,
                            boxShadow: el.style.boxShadow
                        });
                        el.style.border = '3px solid green';
                        el.style.boxShadow = '0 0 20px rgba(0, 255, 0, 0.8)';
                    });
                    
                    setTimeout(() => {
                        elements.forEach((el, idx) => {
                            el.style.border = originalStates[idx].border;
                            el.style.boxShadow = originalStates[idx].boxShadow;
                        });
                    }, arguments[1]);
                    
                    return elements.length;
                ";

                var count = await driver.ExecuteScriptAsync<int>(highlightMultipleScript);
                Logger.Debug($"Highlighted {count} elements matching: {locator}");
                await Task.Delay(durationMs);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error highlighting elements: {ex.Message}");
            }
        }

        /// <summary>
        /// Scroll element into view with highlight
        /// </summary>
        public static async Task ScrollAndHighlightAsync(IWebDriver driver, string locator, int durationMs = 2000)
        {
            try
            {
                const string scrollAndHighlightScript = @"
                    var element = document.querySelector(arguments[0]);
                    if (element) {
                        element.scrollIntoView({behavior: 'smooth', block: 'center'});
                        element.style.border = '3px solid blue';
                        element.style.backgroundColor = 'rgba(0, 150, 255, 0.2)';
                        
                        setTimeout(() => {
                            element.style.border = '';
                            element.style.backgroundColor = '';
                        }, arguments[1]);
                    }
                ";

                await driver.ExecuteScriptAsync(scrollAndHighlightScript);
                Logger.Debug($"Scrolled and highlighted element: {locator}");
                await Task.Delay(durationMs);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error scrolling and highlighting: {ex.Message}");
            }
        }

        /// <summary>
        /// Show element dimensions and position
        /// </summary>
        public static async Task<Dictionary<string, object>> GetElementDimensionsAsync(
            IWebDriver driver,
            string locator)
        {
            try
            {
                const string dimensionScript = @"
                    var element = document.querySelector(arguments[0]);
                    if (!element) return null;
                    
                    var rect = element.getBoundingClientRect();
                    var parent = element.parentElement;
                    var parentRect = parent ? parent.getBoundingClientRect() : null;
                    
                    return {
                        width: rect.width,
                        height: rect.height,
                        x: rect.x,
                        y: rect.y,
                        top: rect.top,
                        bottom: rect.bottom,
                        left: rect.left,
                        right: rect.right,
                        parentWidth: parentRect ? parentRect.width : null,
                        parentHeight: parentRect ? parentRect.height : null,
                        isVisible: rect.width > 0 && rect.height > 0,
                        isInViewport: rect.top >= 0 && rect.left >= 0 && 
                                     rect.bottom <= window.innerHeight && 
                                     rect.right <= window.innerWidth
                    };
                ";

                var dimensions = await driver.ExecuteScriptAsync<Dictionary<string, object>>(dimensionScript);
                Logger.Debug($"Element dimensions retrieved: {locator}");
                return dimensions ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting element dimensions: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Get element CSS styles
        /// </summary>
        public static async Task<Dictionary<string, string>> GetElementStylesAsync(
            IWebDriver driver,
            string locator)
        {
            try
            {
                const string styleScript = @"
                    var element = document.querySelector(arguments[0]);
                    if (!element) return {};
                    
                    var styles = window.getComputedStyle(element);
                    var result = {};
                    
                    var properties = ['display', 'position', 'color', 'backgroundColor', 
                                    'fontSize', 'fontWeight', 'padding', 'margin', 
                                    'border', 'visibility', 'opacity'];
                    
                    properties.forEach(prop => {
                        result[prop] = styles[prop];
                    });
                    
                    return result;
                ";

                var styles = await driver.ExecuteScriptAsync<Dictionary<string, string>>(styleScript);
                Logger.Debug($"Element styles retrieved: {locator}");
                return styles ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting element styles: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Visualize element hierarchy
        /// </summary>
        public static async Task<string> GetElementHierarchyAsync(IWebDriver driver, string locator)
        {
            try
            {
                const string hierarchyScript = @"
                    var element = document.querySelector(arguments[0]);
                    if (!element) return '';
                    
                    var path = [];
                    var current = element;
                    
                    while (current && current.nodeType === Node.ELEMENT_NODE) {
                        var tag = current.tagName.toLowerCase();
                        var id = current.id ? '#' + current.id : '';
                        var classes = current.className ? '.' + current.className.replace(/\s+/g, '.') : '';
                        path.unshift(tag + id + classes);
                        current = current.parentElement;
                    }
                    
                    return path.join(' > ');
                ";

                var hierarchy = await driver.ExecuteScriptAsync<string>(hierarchyScript);
                Logger.Debug($"Element hierarchy: {hierarchy}");
                return hierarchy ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting element hierarchy: {ex.Message}");
                return "";
            }
        }
    }

    /// <summary>
    /// Advanced DOM inspection and manipulation
    /// </summary>
    public class DomInspector
    {
        /// <summary>
        /// Get HTML of an element and its children
        /// </summary>
        public static async Task<string> GetElementHtmlAsync(IWebDriver driver, string locator)
        {
            try
            {
                const string htmlScript = "return document.querySelector(arguments[0]).outerHTML;";
                var html = await driver.ExecuteScriptAsync<string>(htmlScript);
                return html ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting element HTML: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Count elements matching selector
        /// </summary>
        public static async Task<int> CountElementsAsync(IWebDriver driver, string locator)
        {
            try
            {
                const string countScript = "return document.querySelectorAll(arguments[0]).length;";
                var count = await driver.ExecuteScriptAsync<int>(countScript);
                return count;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error counting elements: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Find elements by text content
        /// </summary>
        public static async Task<List<string>> FindElementsByTextAsync(
            IWebDriver driver,
            string text)
        {
            try
            {
                const string findScript = @"
                    var results = [];
                    var walker = document.createTreeWalker(
                        document.body,
                        NodeFilter.SHOW_TEXT,
                        null,
                        false
                    );
                    
                    var node;
                    while (node = walker.nextNode()) {
                        if (node.textContent.includes(arguments[0])) {
                            results.push(node.parentElement ? node.parentElement.tagName.toLowerCase() : 'unknown');
                        }
                    }
                    
                    return results;
                ";

                var results = await driver.ExecuteScriptAsync<List<string>>(findScript);
                return results ?? new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error finding elements by text: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Check if element is in DOM
        /// </summary>
        public static async Task<bool> IsElementInDomAsync(IWebDriver driver, string locator)
        {
            try
            {
                const string checkScript = "return !!document.querySelector(arguments[0]);";
                var exists = await driver.ExecuteScriptAsync<bool>(checkScript);
                return exists;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all attributes of an element
        /// </summary>
        public static async Task<Dictionary<string, string>> GetAllAttributesAsync(
            IWebDriver driver,
            string locator)
        {
            try
            {
                const string attributeScript = @"
                    var element = document.querySelector(arguments[0]);
                    if (!element) return {};
                    
                    var attrs = {};
                    Array.from(element.attributes).forEach(attr => {
                        attrs[attr.name] = attr.value;
                    });
                    return attrs;
                ";

                var attributes = await driver.ExecuteScriptAsync<Dictionary<string, string>>(attributeScript);
                return attributes ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error getting all attributes: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
    }
}
