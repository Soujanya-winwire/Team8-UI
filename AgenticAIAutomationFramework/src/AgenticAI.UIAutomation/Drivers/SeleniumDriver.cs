using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.UIAutomation.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SeleniumWebDriver = OpenQA.Selenium.IWebDriver;
using SeleniumWebElement = OpenQA.Selenium.IWebElement;

namespace AgenticAI.UIAutomation.Drivers
{
    /// <summary>
    /// Selenium-based web driver implementation
    /// </summary>
    public class SeleniumDriver : Interfaces.IWebDriver
    {
        private SeleniumWebDriver? _driver;
        private WebDriverWait? _wait;
        private readonly FrameworkConfiguration _config;

        public SeleniumDriver(FrameworkConfiguration config)
        {
            _config = config;
        }

        public Task InitializeAsync()
        {
            _driver = _config.Browser switch
            {
                BrowserType.Chrome => CreateChromeDriver(),
                BrowserType.Firefox => CreateFirefoxDriver(),
                BrowserType.Edge => CreateEdgeDriver(),
                BrowserType.Safari => CreateSafariDriver(),
                _ => CreateChromeDriver()
            };

            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(_config.TimeoutInSeconds));
            _driver.Manage().Window.Maximize();
            
            return Task.CompletedTask;
        }

        private SeleniumWebDriver CreateChromeDriver()
        {
            var options = new ChromeOptions();
            if (_config.Headless)
            {
                options.AddArgument("--headless");
            }
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            
            return new ChromeDriver(options);
        }

        private SeleniumWebDriver CreateFirefoxDriver()
        {
            var options = new FirefoxOptions();
            if (_config.Headless)
            {
                options.AddArgument("--headless");
            }
            
            return new FirefoxDriver(options);
        }

        private SeleniumWebDriver CreateEdgeDriver()
        {
            var options = new EdgeOptions();
            if (_config.Headless)
            {
                options.AddArgument("--headless");
            }
            options.AddArgument("--start-maximized");
            
            return new EdgeDriver(options);
        }

        private SeleniumWebDriver CreateSafariDriver()
        {
            var options = new SafariOptions();
            return new SafariDriver(options);
        }

        public Task NavigateAsync(string url)
        {
            if (_driver == null)
            {
                InitializeAsync().Wait();
            }
            _driver!.Navigate().GoToUrl(url);
            return Task.CompletedTask;
        }

        public Task<Core.Interfaces.IWebElement> FindElementAsync(string locator, string strategy = "auto")
        {
            var by = GetBy(locator, strategy);
            var element = _wait!.Until(ExpectedConditions.ElementIsVisible(by));
            return Task.FromResult<Core.Interfaces.IWebElement>(new SeleniumElement(element, _driver!));
        }

        public Task<IList<Core.Interfaces.IWebElement>> FindElementsAsync(string locator, string strategy = "auto")
        {
            var by = GetBy(locator, strategy);
            var elements = _driver!.FindElements(by);
            return Task.FromResult<IList<Core.Interfaces.IWebElement>>(
                elements.Select(e => new SeleniumElement(e, _driver!)).Cast<Core.Interfaces.IWebElement>().ToList()
            );
        }

        public Task ClickAsync(string locator)
        {
            var by = GetBy(locator, "auto");
            var element = _wait!.Until(ExpectedConditions.ElementToBeClickable(by));
            element.Click();
            return Task.CompletedTask;
        }

        public Task TypeAsync(string locator, string text)
        {
            var by = GetBy(locator, "auto");
            var element = _wait!.Until(ExpectedConditions.ElementIsVisible(by));
            element.Clear();
            element.SendKeys(text);
            return Task.CompletedTask;
        }

        public Task<string> GetTextAsync(string locator)
        {
            var by = GetBy(locator, "auto");
            var element = _wait!.Until(ExpectedConditions.ElementIsVisible(by));
            return Task.FromResult(element.Text);
        }

        public Task<string> GetAttributeAsync(string locator, string attribute)
        {
            var by = GetBy(locator, "auto");
            var element = _driver!.FindElement(by);
            return Task.FromResult(element.GetAttribute(attribute) ?? "");
        }

        public Task WaitForElementAsync(string locator, int timeoutSeconds = 30)
        {
            var by = GetBy(locator, "auto");
            var customWait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(timeoutSeconds));
            customWait.Until(ExpectedConditions.ElementIsVisible(by));
            return Task.CompletedTask;
        }

        public Task<byte[]> TakeScreenshotAsync()
        {
            var screenshot = ((ITakesScreenshot)_driver!).GetScreenshot();
            return Task.FromResult(screenshot.AsByteArray);
        }

        public Task<string> GetTitleAsync()
        {
            return Task.FromResult(_driver!.Title);
        }

        public Task<string> GetCurrentUrlAsync()
        {
            return Task.FromResult(_driver!.Url);
        }

        public Task CloseAsync()
        {
            _driver?.Quit();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _driver?.Dispose();
            return Task.CompletedTask;
        }

        private By GetBy(string locator, string strategy)
        {
            return strategy.ToLower() switch
            {
                "id" => By.Id(locator),
                "name" => By.Name(locator),
                "css" => By.CssSelector(locator),
                "xpath" => By.XPath(locator),
                "classname" => By.ClassName(locator),
                "tagname" => By.TagName(locator),
                "linktext" => By.LinkText(locator),
                "partiallinktext" => By.PartialLinkText(locator),
                "auto" => AutoDetectBy(locator),
                _ => By.CssSelector(locator)
            };
        }

        private By AutoDetectBy(string locator)
        {
            // Auto-detect locator strategy
            if (locator.StartsWith("//") || locator.StartsWith("(//"))
            {
                return By.XPath(locator);
            }
            else if (locator.StartsWith("#"))
            {
                return By.CssSelector(locator);
            }
            else if (locator.Contains("[") || locator.Contains("."))
            {
                return By.CssSelector(locator);
            }
            else
            {
                // Try ID first
                return By.Id(locator);
            }
        }

        public SeleniumWebDriver GetDriver() => _driver!;
    }

    /// <summary>
    /// Selenium element wrapper
    /// </summary>
    public class SeleniumElement : Interfaces.IWebElement
    {
        private readonly SeleniumWebElement _element;
        private readonly SeleniumWebDriver _driver;

        public SeleniumElement(SeleniumWebElement element, SeleniumWebDriver driver)
        {
            _element = element;
            _driver = driver;
        }

        public Task ClickAsync()
        {
            _element.Click();
            return Task.CompletedTask;
        }

        public Task TypeAsync(string text)
        {
            _element.Clear();
            _element.SendKeys(text);
            return Task.CompletedTask;
        }

        public Task<string> GetTextAsync()
        {
            return Task.FromResult(_element.Text);
        }

        public Task<string> GetAttributeAsync(string attribute)
        {
            return Task.FromResult(_element.GetAttribute(attribute) ?? "");
        }

        public Task<bool> IsVisibleAsync()
        {
            return Task.FromResult(_element.Displayed);
        }

        public Task<bool> IsEnabledAsync()
        {
            return Task.FromResult(_element.Enabled);
        }

        public Task WaitForAsync(int timeoutSeconds = 30)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(d => _element.Displayed);
            return Task.CompletedTask;
        }
    }
}
