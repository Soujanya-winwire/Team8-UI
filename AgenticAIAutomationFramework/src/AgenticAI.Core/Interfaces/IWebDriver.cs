namespace AgenticAI.Core.Interfaces
{
    /// <summary>
    /// Core unified interface for browser automation
    /// </summary>
    public interface IWebDriver
    {
        Task NavigateAsync(string url);
        Task<IWebElement> FindElementAsync(string locator, string strategy = "auto");
        Task<IList<IWebElement>> FindElementsAsync(string locator, string strategy = "auto");
        Task ClickAsync(string locator);
        Task CheckAsync(string locator);
        Task UncheckAsync(string locator);
        Task SelectOptionAsync(string locator, string value);
        Task HoverAsync(string locator);
        Task ScrollToAsync(string locator);
        Task TypeAsync(string locator, string text);
        Task<string> GetTextAsync(string locator);
        Task<string> GetAttributeAsync(string locator, string attribute);
        Task WaitForElementAsync(string locator, int timeoutSeconds = 30);
        Task<byte[]> TakeScreenshotAsync();
        Task<string> GetTitleAsync();
        Task<string> GetCurrentUrlAsync();
        Task CloseAsync();
        Task DisposeAsync();
        
        // JavaScript execution (for advanced scenarios like page-level scrolling)
        Task<T> ExecuteScriptAsync<T>(string script);
        Task ExecuteScriptAsync(string script);
        
        // Additional navigation methods
        Task RefreshAsync();
        Task GoBackAsync();
        Task GoForwardAsync();
        
        // Additional element interaction methods
        Task ClearAsync(string locator);
        Task DoubleClickAsync(string locator);
        Task RightClickAsync(string locator);
        Task PressKeyAsync(string locator, string key);
        
        // Element state methods
        Task<bool> IsElementVisibleAsync(string locator);
        Task<bool> IsElementEnabledAsync(string locator);
        
        // IFrame handling methods
        Task SwitchToFrameAsync(string frameLocator);
        Task SwitchToFrameByIndexAsync(int index);
        Task SwitchToDefaultContentAsync();
        Task SwitchToParentFrameAsync();
    }

    /// <summary>
    /// Core unified web element interface
    /// </summary>
    public interface IWebElement
    {
        Task ClickAsync();
        Task TypeAsync(string text);
        Task<string> GetTextAsync();
        Task<string> GetAttributeAsync(string attribute);
        Task<bool> IsVisibleAsync();
        Task<bool> IsEnabledAsync();
        Task WaitForAsync(int timeoutSeconds = 30);
    }
}
