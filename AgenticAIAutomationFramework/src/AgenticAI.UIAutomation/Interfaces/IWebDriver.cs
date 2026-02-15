using Microsoft.Playwright;
using OpenQA.Selenium;

namespace AgenticAI.UIAutomation.Interfaces
{
    /// <summary>
    /// Unified interface for browser automation supporting both Playwright and Selenium
    /// Implements the core IWebDriver interface
    /// </summary>
    public interface IWebDriver : Core.Interfaces.IWebDriver
    {
        // UI-specific extensions can be added here if needed
    }

    /// <summary>
    /// Unified web element interface
    /// Implements the core IWebElement interface
    /// </summary>
    public interface IWebElement : Core.Interfaces.IWebElement
    {
        // UI-specific extensions can be added here if needed
    }
}
