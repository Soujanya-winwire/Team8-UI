using AgenticAI.Core.Configuration;
using AgenticAI.Core.Logging;
using AgenticAI.UIAutomation.Interfaces;
using AgenticAI.UIAutomation.SelfHealing;

namespace AgenticAI.UIAutomation.PageObjects
{
    /// <summary>
    /// Base class for all page objects with built-in utilities
    /// </summary>
    public abstract class BasePage
    {
        protected readonly IWebDriver Driver;
        protected readonly FrameworkConfiguration Config;
        protected readonly SelfHealingEngine? SelfHealing;

        protected BasePage(IWebDriver driver)
        {
            Driver = driver;
            Config = ConfigurationManager.Instance.FrameworkConfig;
            
            if (Config.EnableSelfHealing)
            {
                SelfHealing = new SelfHealingEngine(driver);
            }
        }

        protected async Task<Core.Interfaces.IWebElement> FindElementAsync(string locator, string strategy = "auto")
        {
            try
            {
                if (SelfHealing != null && Config.EnableSelfHealing)
                {
                    var element = await SelfHealing.FindElementWithHealingAsync(locator, strategy);
                    return element ?? await Driver.FindElementAsync(locator, strategy);
                }
                return await Driver.FindElementAsync(locator, strategy);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to find element: {locator}. Error: {ex.Message}");
                throw;
            }
        }

        protected async Task ClickAsync(string locator, string description = "")
        {
            Logger.StepInfo("Click", $"Clicking element: {description ?? locator}");
            await Driver.ClickAsync(locator);
        }

        protected async Task TypeAsync(string locator, string text, string description = "")
        {
            Logger.StepInfo("Type", $"Typing into element: {description ?? locator}");
            await Driver.TypeAsync(locator, text);
        }

        protected async Task<string> GetTextAsync(string locator)
        {
            return await Driver.GetTextAsync(locator);
        }

        protected async Task NavigateToAsync(string url)
        {
            Logger.Info($"Navigating to: {url}");
            await Driver.NavigateAsync(url);
        }

        protected async Task WaitForElementAsync(string locator, int timeoutSeconds = 30)
        {
            await Driver.WaitForElementAsync(locator, timeoutSeconds);
        }

        protected async Task<string> TakeScreenshotAsync(string fileName)
        {
            var screenshot = await Driver.TakeScreenshotAsync();
            var screenshotPath = Path.Combine(Config.ScreenshotPath, $"{fileName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            
            var directory = Path.GetDirectoryName(screenshotPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(screenshotPath, screenshot);
            Logger.Info($"Screenshot saved: {screenshotPath}");
            return screenshotPath;
        }

        public abstract Task<bool> IsPageLoadedAsync();
    }
}
