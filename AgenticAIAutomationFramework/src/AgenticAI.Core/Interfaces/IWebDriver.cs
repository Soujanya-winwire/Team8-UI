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
        Task TypeAsync(string locator, string text);
        Task<string> GetTextAsync(string locator);
        Task<string> GetAttributeAsync(string locator, string attribute);
        Task WaitForElementAsync(string locator, int timeoutSeconds = 30);
        Task<byte[]> TakeScreenshotAsync();
        Task<string> GetTitleAsync();
        Task<string> GetCurrentUrlAsync();
        Task CloseAsync();
        Task DisposeAsync();
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
