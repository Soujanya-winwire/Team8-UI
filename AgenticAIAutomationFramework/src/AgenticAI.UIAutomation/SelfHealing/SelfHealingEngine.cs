using AgenticAI.Core.Logging;
using AgenticAI.UIAutomation.Interfaces;

namespace AgenticAI.UIAutomation.SelfHealing
{
    /// <summary>
    /// Self-healing mechanism for flaky locators
    /// </summary>
    public class SelfHealingEngine
    {
        private readonly Dictionary<string, List<string>> _locatorCache;
        private readonly IWebDriver _driver;

        public SelfHealingEngine(IWebDriver driver)
        {
            _driver = driver;
            _locatorCache = new Dictionary<string, List<string>>();
        }

        public async Task<Core.Interfaces.IWebElement?> FindElementWithHealingAsync(string originalLocator, string strategy = "auto")
        {
            try
            {
                // Try original locator first
                return await _driver.FindElementAsync(originalLocator, strategy);
            }
            catch (Exception)
            {
                Logger.Warning($"Original locator failed: {originalLocator}. Attempting self-healing...");
                
                // Try cached alternative locators
                if (_locatorCache.ContainsKey(originalLocator))
                {
                    foreach (var alternativeLocator in _locatorCache[originalLocator])
                    {
                        try
                        {
                            var element = await _driver.FindElementAsync(alternativeLocator, strategy);
                            Logger.Info($"Self-healing successful with locator: {alternativeLocator}");
                            return element;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                // Try fuzzy matching strategies
                var healedElement = await TryFuzzyMatchingAsync(originalLocator);
                if (healedElement != null)
                {
                    Logger.Info($"Self-healing successful using fuzzy matching");
                    return healedElement;
                }

                Logger.Error($"Self-healing failed for locator: {originalLocator}");
                throw;
            }
        }

        private async Task<Core.Interfaces.IWebElement?> TryFuzzyMatchingAsync(string originalLocator)
        {
            var strategies = new List<string>
            {
                "css",
                "xpath",
                "text"
            };

            foreach (var strategy in strategies)
            {
                try
                {
                    var elements = await _driver.FindElementsAsync(originalLocator, strategy);
                    if (elements.Count > 0)
                    {
                        CacheAlternativeLocator(originalLocator, originalLocator, strategy);
                        return elements.First();
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        public void CacheAlternativeLocator(string originalLocator, string alternativeLocator, string strategy)
        {
            if (!_locatorCache.ContainsKey(originalLocator))
            {
                _locatorCache[originalLocator] = new List<string>();
            }

            var fullLocator = $"{strategy}:{alternativeLocator}";
            if (!_locatorCache[originalLocator].Contains(fullLocator))
            {
                _locatorCache[originalLocator].Add(fullLocator);
            }
        }
    }
}
