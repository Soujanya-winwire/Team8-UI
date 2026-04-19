using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.Core.Logging;
using AgenticAI.UIAutomation.Interfaces;
using Microsoft.Playwright;
using CoreBrowserType = AgenticAI.Core.Enums.BrowserType;

namespace AgenticAI.UIAutomation.Drivers
{
    /// <summary>
    /// Playwright-based web driver implementation
    /// </summary>
    public class PlaywrightDriver : IWebDriver
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private IBrowserContext? _context;
        private readonly FrameworkConfiguration _config;
        
        // Track current frame context for Playwright
        private IFrame? _currentFrame;
        private string? _currentFrameLocator;

        public PlaywrightDriver(FrameworkConfiguration config)
        {
            _config = config;
        }

        public async Task InitializeAsync()
        {
            _playwright = await Playwright.CreateAsync();

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = _config.Headless
            };

            _browser = _config.Browser switch
            {
                CoreBrowserType.Chrome => await _playwright.Chromium.LaunchAsync(launchOptions),
                CoreBrowserType.Chromium => await _playwright.Chromium.LaunchAsync(launchOptions),
                CoreBrowserType.Firefox => await _playwright.Firefox.LaunchAsync(launchOptions),
                CoreBrowserType.Edge => await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = _config.Headless,
                    Channel = "msedge"
                }),
                CoreBrowserType.Safari => await _playwright.Webkit.LaunchAsync(launchOptions),
                _ => await _playwright.Chromium.LaunchAsync(launchOptions)
            };

            var contextOptions = new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                RecordVideoDir = _config.EnableVideo ? _config.VideoPath : null
            };

            _context = await _browser.NewContextAsync(contextOptions);
            
            if (_config.EnableTracing)
            {
                await _context.Tracing.StartAsync(new TracingStartOptions
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true
                });
            }

            _page = await _context.NewPageAsync();
        }

        public async Task NavigateAsync(string url)
        {
            if (_page == null)
            {
                await InitializeAsync();
            }
            
            // Navigate with NetworkIdle first
            await _page!.GotoAsync(url, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _config.TimeoutInSeconds * 1000
            });
            
            // Additional wait for React/dynamic content to render
            // Wait for body to be fully loaded as a baseline
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                {
                    Timeout = 5000
                });
                
                // Give React apps a moment to initialize after DOM is ready
                await Task.Delay(1000);
            }
            catch
            {
                // If additional wait fails, continue - NetworkIdle should be sufficient for most cases
            }
        }

        /// <summary>
        /// Highlight an element during playback for visual verification
        /// </summary>
        private async Task HighlightElementAsync(string selector)
        {
            if (_page == null) return;

            try
            {
                var highlightScript = @"
                    (selector) => {
                        try {
                            const element = document.querySelector(selector);
                            if (!element) return;
                            
                            // Store original styles
                            const originalBorder = element.style.border;
                            const originalBoxShadow = element.style.boxShadow;
                            const originalOutline = element.style.outline;
                            
                            // Apply highlight styles
                            element.style.border = '3px solid #4CAF50';
                            element.style.boxShadow = '0 0 20px #4CAF50';
                            element.style.outline = '2px solid #81C784';
                            
                            // Scroll element into view
                            element.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
                            
                            // Remove highlight after 1.5 seconds
                            setTimeout(() => {
                                element.style.border = originalBorder;
                                element.style.boxShadow = originalBoxShadow;
                                element.style.outline = originalOutline;
                            }, 1500);
                        } catch (e) {
                            console.warn('Highlight failed:', e);
                        }
                    }
                ";

                // Execute in frame context if applicable
                if (_currentFrame != null)
                {
                    await _currentFrame.EvaluateAsync(highlightScript, selector);
                }
                else
                {
                    await _page.EvaluateAsync(highlightScript, selector);
                }

                // Small delay to let highlight be visible before action
                await Task.Delay(300);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Highlight element failed: {ex.Message}");
                // Don't fail test if highlighting fails
            }
        }

        public async Task<Core.Interfaces.IWebElement> FindElementAsync(string locator, string strategy = "auto")
        {
            // Always return page-scoped element - frame context is handled in action methods
            var elementLocator = _currentFrame != null 
                ? GetFrameLocator(locator, strategy)
                : GetLocator(locator, strategy);
                
            return await Task.FromResult<Core.Interfaces.IWebElement>(new PlaywrightElement(_page!, elementLocator));
        }

        public async Task<IList<Core.Interfaces.IWebElement>> FindElementsAsync(string locator, string strategy = "auto")
        {
            ILocator locatorObj = _currentFrame != null 
                ? GetFrameLocator(locator, strategy)
                : GetLocator(locator, strategy);

            var handles = await locatorObj.ElementHandlesAsync();
            var list = handles.Select(e => new PlaywrightElement(_page!, e)).Cast<Core.Interfaces.IWebElement>().ToList();
            return await Task.FromResult<IList<Core.Interfaces.IWebElement>>(list);
        }

        public async Task ClickAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // Wait for element to be present and visible with retry logic
            ILocator locatorEl;
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }

            // Retry for up to 15 seconds (30 * 500ms)
            bool elementFound = false;
            for (int i = 0; i <= 30; i++)
            {
                try
                {
                    var count = await locatorEl.CountAsync();
                    if (count > 0)
                    {
                        elementFound = true;
                        break;
                    }
                }
                catch { }
                
                if (i < 30) // Don't wait after last attempt
                {
                    await Task.Delay(500);
                }
            }

            if (!elementFound)
            {
                Logger.Error($"Element not found after 15 seconds: {selector}");
                throw new TimeoutException($"Element not found: {selector}");
            }

            // Wait for element to be visible and enabled
            try
            {
                await locatorEl.First.WaitForAsync(new LocatorWaitForOptions 
                { 
                    State = WaitForSelectorState.Visible,
                    Timeout = 5000 
                });
            }
            catch (Exception ex)
            {
                Logger.Debug($"Element not visible after wait: {selector}. Error: {ex.Message}");
            }

            // Scroll element into view
            try
            {
                await locatorEl.First.ScrollIntoViewIfNeededAsync();
            }
            catch { }
            
            // Highlight element before clicking
            await HighlightElementAsync(selector);
            
            // Click with retry
            int maxClickRetries = 3;
            Exception? lastException = null;
            
            for (int retry = 0; retry < maxClickRetries; retry++)
            {
                try
                {
                    // If we're in a frame, click within frame context
                    if (_currentFrame != null)
                    {
                        Logger.Debug($"Clicking in frame context: {selector}");
                        await _currentFrame.ClickAsync(selector, new FrameClickOptions { Timeout = 10000 });
                    }
                    else
                    {
                        await _page!.ClickAsync(selector, new PageClickOptions { Timeout = 10000 });
                    }
                    
                    // Success - exit retry loop
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Logger.Debug($"Click attempt {retry + 1}/{maxClickRetries} failed for {selector}: {ex.Message}");
                    
                    if (retry < maxClickRetries - 1)
                    {
                        await Task.Delay(1000); // Wait before retry
                    }
                }
            }
            
            // All retries failed
            Logger.Error($"All click attempts failed for {selector}");
            throw lastException ?? new Exception($"Failed to click element: {selector}");
        }

        public async Task CheckAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // Highlight element before checking
            await HighlightElementAsync(selector);
            
            ILocator locatorEl;
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }

            int count = await locatorEl.CountAsync();
            if (count > 1)
            {
                locatorEl = locatorEl.First;
                Logger.Debug($"Check: locator '{selector}' matched {count} elements, using .First()");
            }

            await locatorEl.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            var inputType = await locatorEl.EvaluateAsync<string>("el => el.type ? el.type.toLowerCase() : ''");
            if (inputType == "radio")
            {
                var isChecked = await locatorEl.IsCheckedAsync();
                if (!isChecked)
                {
                    await locatorEl.ClickAsync();
                }
            }
            else
            {
                var isChecked = await locatorEl.IsCheckedAsync();
                if (!isChecked)
                {
                    await locatorEl.CheckAsync();
                }
            }
        }

        public async Task UncheckAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // Highlight element before unchecking
            await HighlightElementAsync(selector);
            
            ILocator locatorEl;
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }

            int count = await locatorEl.CountAsync();
            if (count > 1)
            {
                locatorEl = locatorEl.First;
                Logger.Debug($"Uncheck: locator '{selector}' matched {count} elements, using .First()");
            }

            await locatorEl.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            var inputType = await locatorEl.EvaluateAsync<string>("el => el.type ? el.type.toLowerCase() : ''");
            if (inputType == "radio")
            {
                Logger.Debug("Uncheck called on radio button — skipping (radio buttons cannot be unchecked)");
                return;
            }
            else
            {
                var isChecked = await locatorEl.IsCheckedAsync();
                if (isChecked)
                {
                    await locatorEl.UncheckAsync();
                }
            }
        }

        public async Task SelectOptionAsync(string locator, string value)
        {
            var selector = GetSelector(locator, "auto");
            
            // Highlight element before selecting
            await HighlightElementAsync(selector);
            
            ILocator locatorEl;
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }
            
            // ENHANCED: Longer timeout for React/dynamic components (5 seconds instead of default 30s timeout)
            // This is especially important for datepickers and dropdowns that render dynamically
            await locatorEl.WaitForAsync(new LocatorWaitForOptions { 
                State = WaitForSelectorState.Visible,
                Timeout = 5000 // 5 seconds for select to become visible
            });

            try
            {
                await locatorEl.SelectOptionAsync(new SelectOptionValue { Value = value });
                return;
            }
            catch { /* fall through */ }

            try
            {
                await locatorEl.SelectOptionAsync(new SelectOptionValue { Label = value });
                return;
            }
            catch { /* fall through */ }

            if (int.TryParse(value, out var index))
            {
                await locatorEl.SelectOptionAsync(new SelectOptionValue { Index = index });
            }
            else
            {
                throw new Exception($"Could not select option '{value}' by value or label in element '{locator}'");
            }
        }

        public async Task HoverAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // Highlight element before hovering
            await HighlightElementAsync(selector);
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                await _currentFrame.HoverAsync(selector);
            }
            else
            {
                await _page!.HoverAsync(selector);
            }
        }

        public async Task ScrollToAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // GLOBAL FIX: Support multiple fallback selectors for scroll
            var selectors = SplitFallbackSelectors(selector);
            Exception? lastException = null;
            
            foreach (var selectorToTry in selectors)
            {
                try
                {
                    ILocator locatorEl;
                    
                    // Use frame context if available
                    if (_currentFrame != null)
                    {
                        locatorEl = _currentFrame.Locator(selectorToTry.Trim());
                    }
                    else
                    {
                        locatorEl = _page!.Locator(selectorToTry.Trim());
                    }
                    
                    // ENHANCED: Wait for element to be attached to DOM before scrolling
                    await locatorEl.WaitForAsync(new LocatorWaitForOptions 
                    { 
                        State = WaitForSelectorState.Attached,
                        Timeout = 10000 // 10 second timeout
                    });
                    
                    // ENHANCED: Scroll with multiple strategies for robustness
                    try
                    {
                        // Strategy 1: ScrollIntoViewIfNeeded (Playwright's smart scroll)
                        await locatorEl.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions 
                        {
                            Timeout = 5000
                        });
                        Logger.Debug($"Scrolled to element using ScrollIntoViewIfNeeded: {selectorToTry}");
                    }
                    catch
                    {
                        // Strategy 2: Fallback to JavaScript scroll (more aggressive)
                        Logger.Debug("ScrollIntoViewIfNeeded failed, trying JavaScript scroll...");
                        await locatorEl.EvaluateAsync(@"
                            element => {
                                element.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
                            }
                        ");
                        
                        // Wait for scroll to complete
                        await Task.Delay(500);
                        Logger.Debug($"Scrolled to element using JavaScript: {selectorToTry}");
                    }
                    
                    // If we reached here, scroll was successful
                    if (selectors.Count > 1 && selectorToTry != selectors[0])
                    {
                        Logger.Info($"Scroll succeeded with fallback selector: {selectorToTry}");
                    }
                    return; // Success - exit method
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (selectors.Count > 1)
                    {
                        Logger.Debug($"Scroll failed with selector '{selectorToTry}': {ex.Message}. Trying next fallback...");
                    }
                    continue; // Try next selector
                }
            }
            
            // If we get here, all selectors failed
            throw new Exception($"Failed to scroll to element with any of the provided selectors. Last error: {lastException?.Message}", lastException);
        }

        public async Task TypeAsync(string locator, string text)
        {
            var selector = GetSelector(locator, "auto");
            
            // Get the input type before typing
            ILocator locatorEl;
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }

            string inputType = "";
            try
            {
                inputType = await locatorEl.First.GetAttributeAsync("type") ?? "";
                inputType = inputType.ToLower();
            }
            catch { }

            // Validate and transform data based on input type
            string valueToType = text;
            if (!string.IsNullOrEmpty(inputType))
            {
                switch (inputType)
                {
                    case "date":
                        // Check if the value is already in YYYY-MM-DD format
                        if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}$"))
                        {
                            // Try to parse as date and convert
                            if (DateTime.TryParse(text, out DateTime parsedDate))
                            {
                                valueToType = parsedDate.ToString("yyyy-MM-DD");
                                Logger.Debug($"Converted date '{text}' to '{valueToType}' for date input");
                            }
                            else
                            {
                                var errorMsg = $"Invalid date value '{text}' for date field {selector}. Expected format: YYYY-MM-DD (e.g., 2000-01-15)";
                                Logger.Error(errorMsg);
                                throw new ArgumentException(errorMsg);
                            }
                        }
                        break;

                    case "number":
                        // Validate it's a number
                        if (!double.TryParse(text, out _))
                        {
                            var errorMsg = $"Invalid number value '{text}' for number field {selector}. Expected numeric value (e.g., 123 or 45.67)";
                            Logger.Error(errorMsg);
                            throw new ArgumentException(errorMsg);
                        }
                        break;

                    case "email":
                        // Basic email validation
                        if (!text.Contains("@") || !text.Contains("."))
                        {
                            Logger.Debug($"Warning: '{text}' may not be a valid email for field {selector}");
                        }
                        break;

                    case "tel":
                    case "phone":
                        // Remove non-numeric characters if present
                        var numericOnly = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d]", "");
                        if (string.IsNullOrEmpty(numericOnly))
                        {
                            Logger.Debug($"Warning: '{text}' contains no numeric digits for phone field {selector}");
                        }
                        break;
                }
            }
            
            // Highlight element before typing
            await HighlightElementAsync(selector);
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                Logger.Debug($"Typing in frame context: {selector} (type: {inputType}, value: {valueToType})");
                await _currentFrame.FillAsync(selector, valueToType, new FrameFillOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            }
            else
            {
                await _page!.FillAsync(selector, valueToType, new PageFillOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            }
        }

        public async Task<string> GetTextAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                return await _currentFrame.TextContentAsync(selector) ?? "";
            }
            else
            {
                return await _page!.TextContentAsync(selector) ?? "";
            }
        }

        public async Task<string> GetAttributeAsync(string locator, string attribute)
        {
            var selector = GetSelector(locator, "auto");
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                return await _currentFrame.GetAttributeAsync(selector, attribute) ?? "";
            }
            else
            {
                return await _page!.GetAttributeAsync(selector, attribute) ?? "";
            }
        }

        public async Task WaitForElementAsync(string locator, int timeoutSeconds = 30)
        {
            var selector = GetSelector(locator, "auto");
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                await _currentFrame.WaitForSelectorAsync(selector, new FrameWaitForSelectorOptions
                {
                    Timeout = timeoutSeconds * 1000
                });
            }
            else
            {
                await _page!.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                {
                    Timeout = timeoutSeconds * 1000
                });
            }
        }

        public async Task<byte[]> TakeScreenshotAsync()
        {
            return await _page!.ScreenshotAsync();
        }

        public async Task<string> GetTitleAsync()
        {
            return await _page!.TitleAsync();
        }

        public async Task<string> GetCurrentUrlAsync()
        {
            return _page!.Url;
        }

        public async Task CloseAsync()
        {
            if (_config.EnableTracing && _context != null)
            {
                await _context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = Path.Combine(_config.ReportPath, "trace.zip")
                });
            }

            if (_page != null)
            {
                await _page.CloseAsync();
            }

            if (_context != null)
            {
                await _context.CloseAsync();
            }

            if (_browser != null)
            {
                await _browser.CloseAsync();
            }
        }

        public async Task DisposeAsync()
        {
            await CloseAsync();
            _playwright?.Dispose();
        }

        // Additional navigation methods
        public async Task RefreshAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");
            await _page.ReloadAsync();
        }

        public async Task GoBackAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");
            await _page.GoBackAsync();
        }

        public async Task GoForwardAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");
            await _page.GoForwardAsync();
        }

        // Additional element interaction methods
        public async Task ClearAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            ILocator locatorEl;
            
            if (_currentFrame != null)
            {
                locatorEl = _currentFrame.Locator(selector);
            }
            else
            {
                locatorEl = _page!.Locator(selector);
            }
            
            await locatorEl.ClearAsync();
        }

        public async Task DoubleClickAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            if (_currentFrame != null)
            {
                await _currentFrame.DblClickAsync(selector);
            }
            else
            {
                await _page!.DblClickAsync(selector);
            }
        }

        public async Task RightClickAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            
            if (_currentFrame != null)
            {
                await _currentFrame.ClickAsync(selector, new FrameClickOptions { Button = MouseButton.Right });
            }
            else
            {
                await _page!.ClickAsync(selector, new PageClickOptions { Button = MouseButton.Right });
            }
        }

        public async Task PressKeyAsync(string locator, string key)
        {
            var selector = GetSelector(locator, "auto");
            
            if (_currentFrame != null)
            {
                await _currentFrame.PressAsync(selector, key);
            }
            else
            {
                await _page!.PressAsync(selector, key);
            }
        }

        // Element state methods
        public async Task<bool> IsElementVisibleAsync(string locator)
        {
            try
            {
                var selector = GetSelector(locator, "auto");
                ILocator locatorEl;
                
                if (_currentFrame != null)
                {
                    locatorEl = _currentFrame.Locator(selector);
                }
                else
                {
                    locatorEl = _page!.Locator(selector);
                }
                
                return await locatorEl.IsVisibleAsync();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsElementEnabledAsync(string locator)
        {
            try
            {
                var selector = GetSelector(locator, "auto");
                ILocator locatorEl;
                
                if (_currentFrame != null)
                {
                    locatorEl = _currentFrame.Locator(selector);
                }
                else
                {
                    locatorEl = _page!.Locator(selector);
                }
                
                return await locatorEl.IsEnabledAsync();
            }
            catch
            {
                return false;
            }
        }

        // IFrame handling methods - ENHANCED with smart frame detection
        public async Task SwitchToFrameAsync(string frameLocator)
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");

            var selector = GetSelector(frameLocator, "auto");
            
            Logger.Info($"🔍 Attempting to switch to iframe: {selector}");
            
            // GLOBAL FIX: Check if we're already in the correct frame
            if (_currentFrame != null && _currentFrameLocator == selector)
            {
                Logger.Info($"✅ Already in target iframe: {selector} - Skipping switch");
                return;
            }
            
            // GLOBAL FIX: Smart frame detection with multiple search strategies
            IFrame? targetFrame = null;
            IElementHandle? frameElement = null;
            
            try
            {
                // Strategy 1: Try finding frame from main page (for top-level iframes)
                Logger.Debug($"Strategy 1: Looking for iframe from main page context...");
                
                // Wait for frame with timeout
                try
                {
                    await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 5000 });
                    frameElement = await _page.QuerySelectorAsync(selector);
                    
                    if (frameElement != null)
                    {
                        targetFrame = await frameElement.ContentFrameAsync();
                        if (targetFrame != null)
                        {
                            Logger.Debug($"✅ Found iframe from main page");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Frame not found in main page: {ex.Message}");
                }
                
                // Strategy 2: Try finding frame from current frame context (for nested iframes)
                if (targetFrame == null && _currentFrame != null)
                {
                    Logger.Debug($"Strategy 2: Looking for nested iframe inside current frame...");
                    try
                    {
                        await _currentFrame.WaitForSelectorAsync(selector, new FrameWaitForSelectorOptions { Timeout = 5000 });
                        frameElement = await _currentFrame.QuerySelectorAsync(selector);
                        
                        if (frameElement != null)
                        {
                            targetFrame = await frameElement.ContentFrameAsync();
                            if (targetFrame != null)
                            {
                                Logger.Debug($"✅ Found nested iframe inside current frame");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Nested iframe not found: {ex.Message}");
                    }
                }
                
                // Strategy 3: Search by URL pattern in all frames
                if (targetFrame == null)
                {
                    Logger.Debug($"Strategy 3: Searching all frames by URL pattern...");
                    
                    // Extract URL pattern from selector if it uses src attribute
                    var urlPattern = ExtractUrlPatternFromSelector(selector);
                    
                    if (!string.IsNullOrEmpty(urlPattern))
                    {
                        var allFrames = _page.Frames.ToList();
                        Logger.Debug($"Total frames on page: {allFrames.Count}");
                        
                        foreach (var frame in allFrames)
                        {
                            var frameUrl = frame.Url;
                            Logger.Debug($"  Checking frame URL: {frameUrl}");
                            
                            if (frameUrl.Contains(urlPattern, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFrame = frame;
                                Logger.Debug($"✅ Found matching frame by URL pattern: {urlPattern}");
                                break;
                            }
                        }
                    }
                }
                
                // Strategy 4: If we're already in a frame and target not found, assume content is in current frame
                if (targetFrame == null && _currentFrame != null)
                {
                    Logger.Warning($"⚠️ Target iframe '{selector}' not found. Content may have loaded in current iframe. Staying in current frame context.");
                    Logger.Info($"✅ Continuing in current iframe: {_currentFrameLocator}");
                    return; // Stay in current frame instead of failing
                }
                
                // If still not found, throw error
                if (targetFrame == null)
                {
                    throw new Exception($"Frame not found: {selector}. Tried main page, current frame, nested frames, and URL pattern matching.");
                }
                
                // Switch to the found frame
                _currentFrame = targetFrame;
                _currentFrameLocator = selector;
                
                Logger.Info($"✅ Successfully switched to iframe: {selector}");
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Failed to switch to iframe '{selector}': {ex.Message}");
                throw;
            }
        }
        
        // Helper method to extract URL pattern from iframe selector
        private string ExtractUrlPatternFromSelector(string selector)
        {
            // Extract URL pattern from selectors like: iframe[src*="courses.example.com"]
            if (selector.Contains("src*=") || selector.Contains("src~=") || selector.Contains("src|="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(selector, @"src[*~|]=['""]([^'""]+)['""]");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            return string.Empty;
        }

        public async Task SwitchToFrameByIndexAsync(int index)
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");

            var frames = _page.Frames.ToList();
            if (index < 0 || index >= frames.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Frame index {index} out of range. Available frames: {frames.Count}");

            _currentFrame = frames[index];
            _currentFrameLocator = $"iframe:nth-of-type({index + 1})";
            
            Logger.Info($"✅ Switched to iframe by index: {index}");
        }

        public async Task SwitchToDefaultContentAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");

            _currentFrame = null;
            _currentFrameLocator = null;
            
            Logger.Info("✅ Switched to default content (main frame)");
            await Task.CompletedTask;
        }

        public async Task SwitchToParentFrameAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");

            // In Playwright, parent frame access is limited
            // For now, just switch to default content
            await SwitchToDefaultContentAsync();
            
            Logger.Info("✅ Switched to parent frame (defaulted to main content)");
        }

        private ILocator GetLocator(string locator, string strategy)
        {
            return strategy.ToLower() switch
            {
                "css" => _page!.Locator(locator),
                "xpath" => _page!.Locator($"xpath={locator}"),
                "text" => _page!.GetByText(locator),
                "placeholder" => _page!.GetByPlaceholder(locator),
                "role" => _page!.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = locator }),
                "testid" => _page!.GetByTestId(locator),
                "auto" => AutoDetectLocator(locator),
                _ => _page!.Locator(locator)
            };
        }

        private ILocator GetFrameLocator(string locator, string strategy)
        {
            if (_currentFrame == null)
                throw new InvalidOperationException("Not in frame context");
                
            return strategy.ToLower() switch
            {
                "css" => _currentFrame.Locator(locator),
                "xpath" => _currentFrame.Locator($"xpath={locator}"),
                "text" => _currentFrame.GetByText(locator),
                "placeholder" => _currentFrame.GetByPlaceholder(locator),
                "role" => _currentFrame.GetByRole(AriaRole.Button, new FrameGetByRoleOptions { Name = locator }),
                "testid" => _currentFrame.GetByTestId(locator),
                "auto" => AutoDetectFrameLocator(locator),
                _ => _currentFrame.Locator(locator)
            };
        }

        private ILocator AutoDetectLocator(string locator)
        {
            if (locator.StartsWith("//") || locator.StartsWith("(//"))
            {
                return _page!.Locator($"xpath={locator}");
            }
            else if (locator.StartsWith("#") || locator.Contains("[") || locator.Contains("."))
            {
                return _page!.Locator(locator);
            }
            else
            {
                return _page!.GetByText(locator);
            }
        }

        private ILocator AutoDetectFrameLocator(string locator)
        {
            if (_currentFrame == null)
                throw new InvalidOperationException("Not in frame context");
                
            if (locator.StartsWith("//") || locator.StartsWith("(//"))
            {
                return _currentFrame.Locator($"xpath={locator}");
            }
            else if (locator.StartsWith("#") || locator.Contains("[") || locator.Contains("."))
            {
                return _currentFrame.Locator(locator);
            }
            else
            {
                return _currentFrame.GetByText(locator);
            }
        }

        private string GetSelector(string locator, string strategy)
        {
            return strategy.ToLower() switch
            {
                "css" => locator,
                "xpath" => $"xpath={locator}",
                "text" => $"text={locator}",
                "auto" => AutoDetectSelector(locator),
                _ => locator
            };
        }

        private string AutoDetectSelector(string locator)
        {
            // Handle XPath
            if (locator.StartsWith("//") || locator.StartsWith("(//"))
            {
                return $"xpath={locator}";
            }
            
            // GLOBAL FIX: Handle invalid CSS ID selectors (IDs starting with numbers)
            // CSS selector "#1vinD" is invalid → Convert to [id="1vinD"]
            if (locator.StartsWith("#"))
            {
                var id = locator.Substring(1); // Remove '#'
                
                // Check if ID starts with number or invalid character
                if (IsInvalidCssId(id))
                {
                    // Convert #1vinD → [id="1vinD"]
                    Logger.Debug($"Converting invalid CSS ID selector '{locator}' to attribute selector '[id=\"{id}\"]'");
                    return $"[id=\"{id}\"]";
                }
            }
            
            return locator;
        }
        
        // GLOBAL FIX: Check if ID is invalid for CSS selector syntax
        private bool IsInvalidCssId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            
            // CSS IDs cannot start with:
            // - Numbers: "1vinD", "123abc" ❌
            // - Special characters (except hyphen and underscore): "!abc", "@test" ❌
            // - Double hyphen: "--abc" ❌
            
            var firstChar = id[0];
            
            // Check if starts with number
            if (char.IsDigit(firstChar))
            {
                return true;
            }
            
            // Check if starts with invalid special character (not letter, not hyphen, not underscore)
            if (!char.IsLetter(firstChar) && firstChar != '-' && firstChar != '_')
            {
                return true;
            }
            
            // Check if starts with double hyphen
            if (id.StartsWith("--"))
            {
                return true;
            }
            
            return false;
        }

        // Helper method to split fallback selectors
        private List<string> SplitFallbackSelectors(string locator)
        {
            // Split by comma, but only if not inside brackets (to preserve CSS attribute selectors)
            var selectors = new List<string>();
            var current = new System.Text.StringBuilder();
            int bracketDepth = 0;
            
            foreach (char c in locator)
            {
                if (c == '[')
                {
                    bracketDepth++;
                    current.Append(c);
                }
                else if (c == ']')
                {
                    bracketDepth--;
                    current.Append(c);
                }
                else if (c == ',' && bracketDepth == 0)
                {
                    // This comma is a selector separator
                    var selector = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(selector))
                    {
                        selectors.Add(selector);
                    }
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            
            // Add the last selector
            var lastSelector = current.ToString().Trim();
            if (!string.IsNullOrEmpty(lastSelector))
            {
                selectors.Add(lastSelector);
            }
            
            return selectors.Count > 0 ? selectors : new List<string> { locator };
        }

        // JavaScript execution methods for advanced scenarios
        public async Task<T> ExecuteScriptAsync<T>(string script)
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");
                
            return await _page.EvaluateAsync<T>(script);
        }

        public async Task ExecuteScriptAsync(string script)
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");
                
            await _page.EvaluateAsync(script);
        }

        public IPage GetPage() => _page!;
    }

    /// <summary>
    /// Playwright element wrapper
    /// </summary>
    public class PlaywrightElement : Interfaces.IWebElement
    {
        private readonly IPage _page;
        private readonly ILocator? _locator;
        private readonly IElementHandle? _element;

        public PlaywrightElement(IPage page, ILocator locator)
        {
            _page = page;
            _locator = locator;
        }

        public PlaywrightElement(IPage page, IElementHandle element)
        {
            _page = page;
            _element = element;
        }

        public async Task ClickAsync()
        {
            if (_locator != null)
            {
                await _locator.ClickAsync();
            }
            else if (_element != null)
            {
                await _element.ClickAsync();
            }
        }

        public async Task TypeAsync(string text)
        {
            if (_locator != null)
            {
                await _locator.FillAsync(text);
            }
            else if (_element != null)
            {
                await _element.FillAsync(text);
            }
        }

        public async Task<string> GetTextAsync()
        {
            if (_locator != null)
            {
                return await _locator.TextContentAsync() ?? "";
            }
            else if (_element != null)
            {
                return await _element.TextContentAsync() ?? "";
            }
            return "";
        }

        public async Task<string> GetAttributeAsync(string attribute)
        {
            if (_locator != null)
            {
                return await _locator.GetAttributeAsync(attribute) ?? "";
            }
            else if (_element != null)
            {
                return await _element.GetAttributeAsync(attribute) ?? "";
            }
            return "";
        }

        public async Task<bool> IsVisibleAsync()
        {
            if (_locator != null)
            {
                return await _locator.IsVisibleAsync();
            }
            else if (_element != null)
            {
                return await _element.IsVisibleAsync();
            }
            return false;
        }

        public async Task<bool> IsEnabledAsync()
        {
            if (_locator != null)
            {
                return await _locator.IsEnabledAsync();
            }
            else if (_element != null)
            {
                return await _element.IsEnabledAsync();
            }
            return false;
        }

        public async Task WaitForAsync(int timeoutSeconds = 30)
        {
            if (_locator != null)
            {
                await _locator.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = timeoutSeconds * 1000
                });
            }
        }
    }
}
