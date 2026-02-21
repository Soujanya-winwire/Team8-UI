using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
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

        public PlaywrightDriver(FrameworkConfiguration config)
        {
            _config = config;
        }

        public async Task InitializeAsync()
        {
            _playwright = await Playwright.CreateAsync();

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = _config.Headless,
                SlowMo = 100
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
            _page.SetDefaultTimeout(_config.TimeoutInSeconds * 1000);
        }

        public async Task NavigateAsync(string url)
        {
            if (_page == null)
            {
                await InitializeAsync();
            }
            await _page!.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        }

        public async Task<Core.Interfaces.IWebElement> FindElementAsync(string locator, string strategy = "auto")
        {
            var elementLocator = GetLocator(locator, strategy);
            return await Task.FromResult<Core.Interfaces.IWebElement>(new PlaywrightElement(_page!, elementLocator));
        }

        public async Task<IList<Core.Interfaces.IWebElement>> FindElementsAsync(string locator, string strategy = "auto")
        {
            // Use Playwright Locator API to support css, xpath, text and other strategies reliably
            var locatorObj = GetLocator(locator, strategy);

            // Get element handles from locator
            var handles = await locatorObj.ElementHandlesAsync();
            var list = handles.Select(e => new PlaywrightElement(_page!, e)).Cast<Core.Interfaces.IWebElement>().ToList();
            return await Task.FromResult<IList<Core.Interfaces.IWebElement>>(list);
        }

        public async Task ClickAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            await _page!.ClickAsync(selector);
        }

        public async Task TypeAsync(string locator, string text)
        {
            var selector = GetSelector(locator, "auto");
            await _page!.FillAsync(selector, text);
        }

        public async Task<string> GetTextAsync(string locator)
        {
            var selector = GetSelector(locator, "auto");
            return await _page!.TextContentAsync(selector) ?? "";
        }

        public async Task<string> GetAttributeAsync(string locator, string attribute)
        {
            var selector = GetSelector(locator, "auto");
            return await _page!.GetAttributeAsync(selector, attribute) ?? "";
        }

        public async Task WaitForElementAsync(string locator, int timeoutSeconds = 30)
        {
            var selector = GetSelector(locator, "auto");
            await _page!.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                Timeout = timeoutSeconds * 1000
            });
        }

        public async Task<byte[]> TakeScreenshotAsync()
        {
            return await _page!.ScreenshotAsync(new PageScreenshotOptions
            {
                FullPage = true
            });
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

        private ILocator AutoDetectLocator(string locator)
        {
            // Auto-detect locator strategy
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
                // Try text-based locator
                return _page!.GetByText(locator);
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
