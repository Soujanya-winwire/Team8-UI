using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Advanced wait strategies with custom conditions
    /// </summary>
    public class AdvancedWaitHelper
    {
        /// <summary>
        /// Wait for a custom condition with exponential backoff
        /// </summary>
        public static async Task<T> WaitForConditionAsync<T>(
            Func<Task<T>> condition,
            Func<T, bool> validator,
            int timeoutSeconds = 30,
            string? description = null,
            int initialDelayMs = 100)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var delayMs = initialDelayMs;
            var maxDelay = 5000; // Max 5 second delay between retries

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    var result = await condition();
                    if (validator(result))
                    {
                        Logger.Debug($"Wait condition satisfied: {description ?? "custom condition"}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Wait condition check failed: {ex.Message}");
                }

                await Task.Delay(Math.Min(delayMs, maxDelay));
                delayMs = (int)(delayMs * 1.5); // Exponential backoff
            }

            throw new TimeoutException($"Timeout waiting for condition: {description ?? "custom condition"} after {timeoutSeconds} seconds");
        }

        /// <summary>
        /// Wait for element to be stable (not changing)
        /// </summary>
        public static async Task WaitForElementStableAsync(
            IWebDriver driver,
            string locator,
            int timeoutSeconds = 10)
        {
            Logger.Info($"Waiting for element to stabilize: {locator}");
            
            string? previousText = null;
            string? previousAttribute = null;
            int stableCount = 0;
            const int requiredStableChecks = 2;

            await WaitForConditionAsync(
                async () =>
                {
                    try
                    {
                        var currentText = await driver.GetTextAsync(locator);
                        var currentAttribute = await driver.GetAttributeAsync(locator, "class");

                        if (currentText == previousText && currentAttribute == previousAttribute)
                        {
                            stableCount++;
                        }
                        else
                        {
                            stableCount = 0;
                        }

                        previousText = currentText;
                        previousAttribute = currentAttribute;

                        return stableCount >= requiredStableChecks;
                    }
                    catch
                    {
                        return false;
                    }
                },
                x => x,
                timeoutSeconds,
                $"Element stable: {locator}"
            );
        }

        /// <summary>
        /// Wait for JavaScript to complete execution
        /// </summary>
        public static async Task WaitForJavaScriptAsync(IWebDriver driver, int timeoutSeconds = 30)
        {
            Logger.Info("Waiting for JavaScript to complete execution");

            const string jsCheckScript = @"
                return (typeof $ !== 'undefined' ? $.active == 0 : true) &&
                       (typeof jQuery !== 'undefined' ? jQuery.active == 0 : true) &&
                       document.readyState === 'complete';
            ";

            await WaitForConditionAsync(
                async () => await driver.ExecuteScriptAsync<bool>(jsCheckScript),
                x => x,
                timeoutSeconds,
                "JavaScript execution"
            );
        }

        /// <summary>
        /// Wait for page to load (document ready state)
        /// </summary>
        public static async Task WaitForPageLoadAsync(IWebDriver driver, int timeoutSeconds = 30)
        {
            Logger.Info("Waiting for page load");

            const string readyStateScript = "return document.readyState === 'complete';";

            await WaitForConditionAsync(
                async () => await driver.ExecuteScriptAsync<bool>(readyStateScript),
                x => x,
                timeoutSeconds,
                "Page load complete"
            );
        }

        /// <summary>
        /// Wait for network requests to complete
        /// </summary>
        public static async Task WaitForNetworkIdleAsync(IWebDriver driver, int timeoutSeconds = 30)
        {
            Logger.Info("Waiting for network to be idle");

            const string networkIdleScript = @"
                return (typeof fetch !== 'undefined') && 
                       (document.readyState === 'complete' || document.readyState === 'interactive');
            ";

            await WaitForConditionAsync(
                async () => await driver.ExecuteScriptAsync<bool>(networkIdleScript),
                x => x,
                timeoutSeconds,
                "Network idle"
            );
        }

        /// <summary>
        /// Wait for multiple elements with condition
        /// </summary>
        public static async Task WaitForElementsAsync(
            IWebDriver driver,
            string locator,
            int expectedCount,
            int timeoutSeconds = 30)
        {
            Logger.Info($"Waiting for {expectedCount} elements matching: {locator}");

            await WaitForConditionAsync(
                async () =>
                {
                    try
                    {
                        var elements = await driver.FindElementsAsync(locator);
                        return elements.Count;
                    }
                    catch
                    {
                        return 0;
                    }
                },
                x => x == expectedCount,
                timeoutSeconds,
                $"Found {expectedCount} elements"
            );
        }

        /// <summary>
        /// Wait for element attribute to have a specific value
        /// </summary>
        public static async Task WaitForAttributeValueAsync(
            IWebDriver driver,
            string locator,
            string attributeName,
            string expectedValue,
            int timeoutSeconds = 30)
        {
            Logger.Info($"Waiting for attribute {attributeName} to equal: {expectedValue}");

            await WaitForConditionAsync(
                async () => await driver.GetAttributeAsync(locator, attributeName),
                x => x == expectedValue,
                timeoutSeconds,
                $"Attribute {attributeName} = {expectedValue}"
            );
        }

        /// <summary>
        /// Wait for element text to contain a substring
        /// </summary>
        public static async Task WaitForTextContainsAsync(
            IWebDriver driver,
            string locator,
            string expectedText,
            int timeoutSeconds = 30)
        {
            Logger.Info($"Waiting for element text to contain: {expectedText}");

            await WaitForConditionAsync(
                async () => await driver.GetTextAsync(locator),
                x => x.Contains(expectedText),
                timeoutSeconds,
                $"Text contains: {expectedText}"
            );
        }

        /// <summary>
        /// Wait for function to return a specific result
        /// </summary>
        public static async Task<T> WaitForFunctionAsync<T>(
            Func<Task<T>> function,
            T expectedValue,
            int timeoutSeconds = 30,
            string? description = null)
        {
            Logger.Info($"Waiting for function result: {description ?? "custom function"}");

            return await WaitForConditionAsync(
                function,
                x => Equals(x, expectedValue),
                timeoutSeconds,
                description ?? "Function result",
                100
            );
        }

        /// <summary>
        /// Poll a condition multiple times at regular intervals
        /// </summary>
        public static async Task PollAsync(
            Func<Task<bool>> condition,
            int intervalMs = 500,
            int maxAttempts = 10,
            string? description = null)
        {
            Logger.Info($"Polling condition: {description ?? "custom condition"}");

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (await condition())
                    {
                        Logger.Debug($"Polling condition satisfied on attempt {attempt + 1}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Polling attempt {attempt + 1} failed: {ex.Message}");
                }

                if (attempt < maxAttempts - 1)
                {
                    await Task.Delay(intervalMs);
                }
            }

            throw new TimeoutException($"Polling failed: {description ?? "custom condition"} after {maxAttempts} attempts");
        }
    }
}
