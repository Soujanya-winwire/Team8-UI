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
            await _page!.GotoAsync(url);
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
            
            // If we're in a frame, click within frame context
            if (_currentFrame != null)
            {
                Logger.Debug($"Clicking in frame context: {selector}");
                await _currentFrame.ClickAsync(selector, new FrameClickOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            }
            else
            {
                await _page!.ClickAsync(selector, new PageClickOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            }
        }

        public async Task CheckAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
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
            
            await locatorEl.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

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
            
            await locatorEl.ScrollIntoViewIfNeededAsync();
        }

        public async Task TypeAsync(string locator, string text)
        {
            var selector = GetSelector(locator, "auto");
            
            // Use frame context if available
            if (_currentFrame != null)
            {
                Logger.Debug($"Typing in frame context: {selector}");
                await _currentFrame.FillAsync(selector, text, new FrameFillOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            }
            else
            {
                await _page!.FillAsync(selector, text, new PageFillOptions { Timeout = _config.TimeoutInSeconds * 1000 });
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

        // IFrame handling methods - FIXED for Playwright
        public async Task SwitchToFrameAsync(string frameLocator)
        {
            if (_page == null)
                throw new InvalidOperationException("Page not initialized");

            var selector = GetSelector(frameLocator, "auto");
            
            // In Playwright, get the actual frame object
            Logger.Info($"Switching to iframe: {selector}");
            
            // Wait for frame to be available
            await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = _config.TimeoutInSeconds * 1000 });
            
            // Get the frame by selector
            var frameElement = await _page.QuerySelectorAsync(selector);
            if (frameElement == null)
            {
                throw new Exception($"Frame not found: {selector}");
            }
            
            // Get the content frame
            var frame = await frameElement.ContentFrameAsync();
            if (frame == null)
            {
                throw new Exception($"Could not get content frame for: {selector}");
            }
            
            _currentFrame = frame;
            _currentFrameLocator = selector;
            
            Logger.Info($"✅ Successfully switched to iframe: {selector}");
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
            if (locator.StartsWith("//") || locator.StartsWith("(//"))
            {
                return $"xpath={locator}";
            }
            return locator;
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
