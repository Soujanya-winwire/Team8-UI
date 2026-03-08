using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using AgenticAI.UIAutomation.Interfaces;
using AgenticAI.Core.Utilities;
using AgenticAI.Core.Logging;

namespace AgenticAI.UIAutomation.Drivers
{
    /// <summary>
    /// Factory for creating web drivers based on configuration
    /// MIGRATION NOTE: Framework now uses Playwright exclusively for better performance and reliability
    /// </summary>
    public class WebDriverFactory
    {
        public static async Task<IWebDriver> CreateDriverAsync(FrameworkConfiguration? config = null)
        {
            config ??= ConfigurationManager.Instance.FrameworkConfig;

            // GLOBAL FRAMEWORK CHANGE: Playwright is now the primary and recommended automation engine
            // SeleniumDriver is deprecated but kept for backward compatibility
            IWebDriver driver = config.AutomationFramework switch
            {
                AutomationFramework.Playwright => new PlaywrightDriver(config),
                AutomationFramework.Selenium => CreateSeleniumDriverWithWarning(config),
                _ => new PlaywrightDriver(config) // Default to Playwright
            };

            // Initialize the driver
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

        private static IWebDriver CreateSeleniumDriverWithWarning(FrameworkConfiguration config)
        {
            // Log deprecation warning
            Logger.Warning("?? DEPRECATION WARNING: SeleniumDriver is deprecated and will be removed in future versions.");
            Logger.Warning("?? Please update your configuration to use Playwright for better performance and reliability.");
            Logger.Warning("?? All Selenium features have been migrated to Playwright with enhanced capabilities.");
            
            return new SeleniumDriver(config);
        }
    }
}
