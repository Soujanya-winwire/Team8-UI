using AgenticAI.Core.ZeroCode.Models;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities.ObjectRepository
{
    /// <summary>
    /// Test object model for Ranorex-like repository
    /// </summary>
    public class TestObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Category { get; set; } // e.g., LoginPage, CheckoutPage
        public string Description { get; set; }
        public string LocatorType { get; set; } = "CSS"; // CSS, XPath, ID, Class
        public string LocatorValue { get; set; }
        public List<string> AlternateLocators { get; set; } = new();
        public Dictionary<string, string> Attributes { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int UsageCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public string ElementType { get; set; } // Button, TextBox, Link, etc.
        public List<TestObjectTag> Tags { get; set; } = new();
    }

    /// <summary>
    /// Tag for organizing test objects
    /// </summary>
    public class TestObjectTag
    {
        public string Name { get; set; }
        public string Color { get; set; }
    }

    /// <summary>
    /// Object repository interface
    /// </summary>
    public interface IObjectRepository
    {
        // CRUD Operations
        Task<TestObject> CreateObjectAsync(TestObject obj);
        Task<TestObject> GetObjectAsync(string id);
        Task<List<TestObject>> GetObjectsByCategoryAsync(string category);
        Task<List<TestObject>> GetAllObjectsAsync();
        Task<TestObject> UpdateObjectAsync(TestObject obj);
        Task<bool> DeleteObjectAsync(string id);

        // Search & Filter
        Task<List<TestObject>> SearchByNameAsync(string query);
        Task<List<TestObject>> SearchByLocatorAsync(string locator);
        Task<List<TestObject>> GetObjectsByTagAsync(string tag);

        // Statistics
        Task<int> GetTotalObjectCountAsync();
        Task<int> GetCategoryCountAsync(string category);
        Task<List<(string category, int count)>> GetCategoryStatisticsAsync();
        Task<List<(string name, int usage)>> GetMostUsedObjectsAsync(int topN = 10);

        // Batch Operations
        Task<int> ImportObjectsAsync(List<TestObject> objects);
        Task<List<TestObject>> ExportObjectsAsync(string category = null);
        Task<bool> UpdateLocatorAsync(string objectId, string newLocator);
    }

    /// <summary>
    /// Smart locator generation engine
    /// </summary>
    public class SmartLocatorEngine
    {
        /// <summary>
        /// Generate optimal CSS selector
        /// </summary>
        public static string GenerateCSSSelector(Dictionary<string, string> attributes)
        {
            if (!attributes.Any()) return null;

            // Priority: ID > data-test > name > unique combination
            if (attributes.ContainsKey("id") && !string.IsNullOrEmpty(attributes["id"]))
                return $"#{attributes["id"]}";

            if (attributes.ContainsKey("data-test") && !string.IsNullOrEmpty(attributes["data-test"]))
                return $"[data-test='{attributes["data-test"]}']";

            if (attributes.ContainsKey("name") && !string.IsNullOrEmpty(attributes["name"]))
                return $"[name='{attributes["name"]}']";

            // Build unique combination
            var selector = "div"; // Default element
            if (attributes.ContainsKey("tag"))
                selector = attributes["tag"];

            var conditions = new List<string>();
            if (attributes.ContainsKey("class") && !string.IsNullOrEmpty(attributes["class"]))
                conditions.Add($".{attributes["class"].Split(' ')[0]}"); // First class only

            if (attributes.ContainsKey("type") && !string.IsNullOrEmpty(attributes["type"]))
                conditions.Add($"[type='{attributes["type"]}']");

            if (attributes.ContainsKey("placeholder") && !string.IsNullOrEmpty(attributes["placeholder"]))
                conditions.Add($"[placeholder='{attributes["placeholder"]}']");

            return selector + string.Join("", conditions);
        }

        /// <summary>
        /// Generate XPath expression
        /// </summary>
        public static string GenerateXPath(Dictionary<string, string> attributes)
        {
            if (!attributes.Any()) return null;

            var conditions = new List<string>();

            if (attributes.ContainsKey("id") && !string.IsNullOrEmpty(attributes["id"]))
                return $"//*[@id='{attributes["id"]}']";

            if (attributes.ContainsKey("name") && !string.IsNullOrEmpty(attributes["name"]))
                conditions.Add($"@name='{attributes["name"]}'");

            if (attributes.ContainsKey("class") && !string.IsNullOrEmpty(attributes["class"]))
                conditions.Add($"contains(@class, '{attributes["class"].Split(' ')[0]}'");

            if (attributes.ContainsKey("type") && !string.IsNullOrEmpty(attributes["type"]))
                conditions.Add($"@type='{attributes["type"]}'");

            if (attributes.ContainsKey("text") && !string.IsNullOrEmpty(attributes["text"]))
                conditions.Add($"text()='{attributes["text"]}'");

            var tag = attributes.ContainsKey("tag") ? attributes["tag"] : "*";
            
            if(!conditions.Any())
                return $"//{tag}";

            return $"//{tag}[{string.Join(" and ", conditions)}]";
        }

        /// <summary>
        /// Generate robust locator with fallbacks
        /// </summary>
        public static string GenerateRobustLocator(Dictionary<string, string> attributes)
        {
            var primary = GenerateCSSSelector(attributes);
            var xpath = GenerateXPath(attributes);
            
            return $"{primary} | {xpath}"; // Fallback syntax
        }

        /// <summary>
        /// Validate locator works
        /// </summary>
        public static bool ValidateLocator(string locator)
        {
            if (string.IsNullOrEmpty(locator)) return false;
            
            try
            {
                // Basic validation for CSS selectors
                if (locator.StartsWith("#") || locator.StartsWith(".") || locator.StartsWith("["))
                    return true;

                // Basic validation for XPath
                if (locator.StartsWith("//") || locator.StartsWith("(/"))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Element analyzer for recording enhancements
    /// </summary>
    public class ElementAnalyzer
    {
        /// <summary>
        /// Analyze captured element and extract features
        /// </summary>
        public static TestObject AnalyzeElement(Dictionary<string, string> elementAttributes, string elementName = null)
        {
            var testObject = new TestObject
            {
                Name = elementName ?? GenerateElementName(elementAttributes),
                Attributes = elementAttributes,
                ElementType = DetectElementType(elementAttributes),
                LocatorValue = SmartLocatorEngine.GenerateCSSSelector(elementAttributes) 
                    ?? SmartLocatorEngine.GenerateXPath(elementAttributes)
            };

            // Generate alternate locators
            var css = SmartLocatorEngine.GenerateCSSSelector(elementAttributes);
            var xpath = SmartLocatorEngine.GenerateXPath(elementAttributes);
            
            if (!string.IsNullOrEmpty(css)) testObject.AlternateLocators.Add(css);
            if (!string.IsNullOrEmpty(xpath)) testObject.AlternateLocators.Add(xpath);

            testObject.LocatorType = testObject.LocatorValue?.StartsWith("//") == true ? "XPath" : "CSS";

            return testObject;
        }

        /// <summary>
        /// Detect element type from attributes
        /// </summary>
        public static string DetectElementType(Dictionary<string, string> attributes)
        {
            if (!attributes.ContainsKey("tag"))
                return "Unknown";

            var tag = attributes["tag"].ToLower();

            return tag switch
            {
                "button" => "Button",
                "input" => GetInputType(attributes),
                "a" => "Link",
                "img" => "Image",
                "table" => "Table",
                "form" => "Form",
                "select" => "Dropdown",
                "textarea" => "TextArea",
                "checkbox" => "Checkbox",
                "radio" => "RadioButton",
                _ => char.ToUpper(tag[0]) + tag.Substring(1)
            };
        }

        private static string GetInputType(Dictionary<string, string> attributes)
        {
            if (!attributes.ContainsKey("type"))
                return "TextBox";

            return attributes["type"].ToLower() switch
            {
                "text" => "TextBox",
                "password" => "PasswordBox",
                "checkbox" => "Checkbox",
                "radio" => "RadioButton",
                "submit" => "SubmitButton",
                "button" => "Button",
                "email" => "EmailBox",
                "number" => "NumberBox",
                "date" => "DatePicker",
                _ => "Input"
            };
        }

        private static string GenerateElementName(Dictionary<string, string> attributes)
        {
            if (attributes.ContainsKey("id") && !string.IsNullOrEmpty(attributes["id"]))
                return attributes["id"];

            if (attributes.ContainsKey("name") && !string.IsNullOrEmpty(attributes["name"]))
                return attributes["name"];

            if (attributes.ContainsKey("placeholder") && !string.IsNullOrEmpty(attributes["placeholder"]))
                return attributes["placeholder"];

            if (attributes.ContainsKey("text") && !string.IsNullOrEmpty(attributes["text"]))
                return attributes["text"].Substring(0, Math.Min(30, attributes["text"].Length));

            var elementType = DetectElementType(attributes);
            return $"{elementType}_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }

    /// <summary>
    /// Object repository implementation with in-memory storage (can be backed by database)
    /// </summary>
    public class ObjectRepository : IObjectRepository
    {
        private List<TestObject> _objects = new();

        public async Task<TestObject> CreateObjectAsync(TestObject obj)
        {
            if (string.IsNullOrEmpty(obj.Id))
                obj.Id = Guid.NewGuid().ToString();

            obj.CreatedAt = DateTime.Now;
            obj.UpdatedAt = DateTime.Now;

            _objects.Add(obj);
            Logger.Info($"Object '{obj.Name}' added to repository");
            return await Task.FromResult(obj);
        }

        public async Task<TestObject> GetObjectAsync(string id)
        {
            return await Task.FromResult(_objects.FirstOrDefault(o => o.Id == id));
        }

        public async Task<List<TestObject>> GetObjectsByCategoryAsync(string category)
        {
            return await Task.FromResult(_objects.Where(o => o.Category == category && o.IsActive).ToList());
        }

        public async Task<List<TestObject>> GetAllObjectsAsync()
        {
            return await Task.FromResult(_objects.Where(o => o.IsActive).ToList());
        }

        public async Task<TestObject> UpdateObjectAsync(TestObject obj)
        {
            var existing = _objects.FirstOrDefault(o => o.Id == obj.Id);
            if (existing == null)
                return null;

            obj.UpdatedAt = DateTime.Now;
            obj.CreatedAt = existing.CreatedAt; // Preserve creation date
            
            _objects.Remove(existing);
            _objects.Add(obj);
            
            Logger.Info($"Object '{obj.Name}' updated");
            return await Task.FromResult(obj);
        }

        public async Task<bool> DeleteObjectAsync(string id)
        {
            var obj = _objects.FirstOrDefault(o => o.Id == id);
            if (obj == null)
                return false;

            obj.IsActive = false; // Soft delete
            Logger.Info($"Object with ID '{id}' deleted");
            return await Task.FromResult(true);
        }

        public async Task<List<TestObject>> SearchByNameAsync(string query)
        {
            return await Task.FromResult(_objects
                .Where(o => o.IsActive && o.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }

        public async Task<List<TestObject>> SearchByLocatorAsync(string locator)
        {
            return await Task.FromResult(_objects
                .Where(o => o.IsActive && (o.LocatorValue.Contains(locator) || 
                       o.AlternateLocators.Any(l => l.Contains(locator))))
                .ToList());
        }

        public async Task<List<TestObject>> GetObjectsByTagAsync(string tag)
        {
            return await Task.FromResult(_objects
                .Where(o => o.IsActive && o.Tags.Any(t => t.Name == tag))
                .ToList());
        }

        public async Task<int> GetTotalObjectCountAsync()
        {
            return await Task.FromResult(_objects.Count(o => o.IsActive));
        }

        public async Task<int> GetCategoryCountAsync(string category)
        {
            return await Task.FromResult(_objects.Count(o => o.IsActive && o.Category == category));
        }

        public async Task<List<(string category, int count)>> GetCategoryStatisticsAsync()
        {
            var stats = _objects
                .Where(o => o.IsActive)
                .GroupBy(o => o.Category)
                .Select(g => (g.Key, g.Count()))
                .ToList();

            return await Task.FromResult(stats);
        }

        public async Task<List<(string name, int usage)>> GetMostUsedObjectsAsync(int topN = 10)
        {
            var mostUsed = _objects
                .Where(o => o.IsActive)
                .OrderByDescending(o => o.UsageCount)
                .Take(topN)
                .Select(o => (o.Name, o.UsageCount))
                .ToList();

            return await Task.FromResult(mostUsed);
        }

        public async Task<int> ImportObjectsAsync(List<TestObject> objects)
        {
            int count = 0;
            foreach (var obj in objects)
            {
                if (string.IsNullOrEmpty(obj.Id))
                    obj.Id = Guid.NewGuid().ToString();

                _objects.Add(obj);
                count++;
            }

            Logger.Info($"Imported {count} objects to repository");
            return await Task.FromResult(count);
        }

        public async Task<List<TestObject>> ExportObjectsAsync(string category = null)
        {
            var query = _objects.Where(o => o.IsActive);
            
            if (!string.IsNullOrEmpty(category))
                query = query.Where(o => o.Category == category);

            return await Task.FromResult(query.ToList());
        }

        public async Task<bool> UpdateLocatorAsync(string objectId, string newLocator)
        {
            var obj = _objects.FirstOrDefault(o => o.Id == objectId);
            if (obj == null)
                return false;

            if (!SmartLocatorEngine.ValidateLocator(newLocator))
                return false;

            obj.AlternateLocators.Add(obj.LocatorValue);
            obj.LocatorValue = newLocator;
            obj.UpdatedAt = DateTime.Now;

            return await Task.FromResult(true);
        }
    }
}
