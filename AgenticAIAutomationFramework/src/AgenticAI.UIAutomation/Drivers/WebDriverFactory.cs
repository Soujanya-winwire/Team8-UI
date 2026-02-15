using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.UIAutomation.Interfaces;

namespace AgenticAI.UIAutomation.Drivers
{
    /// <summary>
    /// Factory for creating web drivers based on configuration
    /// </summary>
    public class WebDriverFactory
    {
        public static async Task<IWebDriver> CreateDriverAsync(FrameworkConfiguration? config = null)
        {
            config ??= ConfigurationManager.Instance.FrameworkConfig;

            IWebDriver driver = config.AutomationFramework switch
            {
                AutomationFramework.Playwright => new PlaywrightDriver(config),
                AutomationFramework.Selenium => new SeleniumDriver(config),
                _ => new PlaywrightDriver(config)
            };

            // Initialize the driver (only Playwright needs async initialization)
            if (driver is PlaywrightDriver playwrightDriver)
            {
                await playwrightDriver.InitializeAsync();
            }
            else if (driver is SeleniumDriver seleniumDriver)
            {
                await seleniumDriver.InitializeAsync();
            }

            return driver;
        }
    }
}
