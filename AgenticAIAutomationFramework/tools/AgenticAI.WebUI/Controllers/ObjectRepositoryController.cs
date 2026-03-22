using Microsoft.AspNetCore.Mvc;
using AgenticAI.Core.Utilities.ObjectRepository;
using AgenticAI.Core.Logging;

namespace AgenticAI.WebUI.Controllers
{
    /// <summary>
    /// Object Repository API Controller
    /// Manages test objects similar to Ranorex Studio
    /// </summary>
    [ApiController]
    [Route("api/object-repository")]
    public class ObjectRepositoryController : ControllerBase
    {
        private static readonly ObjectRepository _repository = new ObjectRepository();

        /// <summary>
        /// Create a new test object
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateObject([FromBody] TestObject obj)
        {
            try
            {
                if (string.IsNullOrEmpty(obj.Name))
                    return BadRequest(new { error = "Object name is required" });

                var created = await _repository.CreateObjectAsync(obj);
                return Ok(new { success = true, data = created });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating object: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get all test objects
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllObjects()
        {
            try
            {
                var objects = await _repository.GetAllObjectsAsync();
                return Ok(new { success = true, data = objects, count = objects.Count });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving objects: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get test objects by category
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetObjectsByCategory(string category)
        {
            try
            {
                var objects = await _repository.GetObjectsByCategoryAsync(category);
                return Ok(new { success = true, data = objects, count = objects.Count, category });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving objects by category: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get single test object
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetObject(string id)
        {
            try
            {
                var obj = await _repository.GetObjectAsync(id);
                if (obj == null)
                    return NotFound(new { error = "Object not found" });

                return Ok(new { success = true, data = obj });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving object: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Update test object
        /// </summary>
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateObject(string id, [FromBody] TestObject obj)
        {
            try
            {
                obj.Id = id;
                var updated = await _repository.UpdateObjectAsync(obj);
                if (updated == null)
                    return NotFound(new { error = "Object not found" });

                return Ok(new { success = true, data = updated });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating object: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete test object
        /// </summary>
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteObject(string id)
        {
            try
            {
                var success = await _repository.DeleteObjectAsync(id);
                if (!success)
                    return NotFound(new { error = "Object not found" });

                return Ok(new { success = true, message = "Object deleted" });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting object: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Search objects by name
        /// </summary>
        [HttpGet("search/name")]
        public async Task<IActionResult> SearchByName([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                    return BadRequest(new { error = "Query is required" });

                var results = await _repository.SearchByNameAsync(query);
                return Ok(new { success = true, data = results, count = results.Count, query });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error searching objects: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Search objects by locator
        /// </summary>
        [HttpGet("search/locator")]
        public async Task<IActionResult> SearchByLocator([FromQuery] string locator)
        {
            try
            {
                if (string.IsNullOrEmpty(locator))
                    return BadRequest(new { error = "Locator is required" });

                var results = await _repository.SearchByLocatorAsync(locator);
                return Ok(new { success = true, data = results, count = results.Count, locator });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error searching by locator: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get objects by tag
        /// </summary>
        [HttpGet("tags/{tag}")]
        public async Task<IActionResult> GetObjectsByTag(string tag)
        {
            try
            {
                var objects = await _repository.GetObjectsByTagAsync(tag);
                return Ok(new { success = true, data = objects, count = objects.Count, tag });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving objects by tag: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get repository statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var totalCount = await _repository.GetTotalObjectCountAsync();
                var categoryStats = await _repository.GetCategoryStatisticsAsync();
                var mostUsed = await _repository.GetMostUsedObjectsAsync(10);

                return Ok(new { success = true, data = new
                {
                    totalObjects = totalCount,
                    categories = categoryStats.Select(c => new { c.category, c.count }),
                    mostUsedObjects = mostUsed.Select(m => new { m.name, m.usage }),
                    lastUpdated = DateTime.Now
                }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting statistics: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generate smart locators for element data
        /// </summary>
        [HttpPost("generate-locators")]
        public IActionResult GenerateLocators([FromBody] Dictionary<string, string> attributes)
        {
            try
            {
                if (attributes == null || !attributes.Any())
                    return BadRequest(new { error = "Element attributes are required" });

                var css = SmartLocatorEngine.GenerateCSSSelector(attributes);
                var xpath = SmartLocatorEngine.GenerateXPath(attributes);
                var robust = SmartLocatorEngine.GenerateRobustLocator(attributes);

                return Ok(new
                {
                    cssSelector = css,
                    xpathExpression = xpath,
                    robustLocator = robust,
                    isValid = SmartLocatorEngine.ValidateLocator(css)
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating locators: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Analyze element and create test object
        /// </summary>
        [HttpPost("analyze-element")]
        public IActionResult AnalyzeElement([FromBody] Dictionary<string, string> attributes)
        {
            try
            {
                if (attributes == null || !attributes.Any())
                    return BadRequest(new { error = "Element attributes are required" });

                var testObject = ElementAnalyzer.AnalyzeElement(attributes);

                return Ok(new
                {
                    success = true,
                    data = testObject,
                    elementType = testObject.ElementType,
                    suggestedName = testObject.Name
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error analyzing element: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Update object locator
        /// </summary>
        [HttpPut("update-locator/{id}")]
        public async Task<IActionResult> UpdateLocator(string id, [FromBody] UpdateLocatorRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NewLocator))
                    return BadRequest(new { error = "New locator is required" });

                if (!SmartLocatorEngine.ValidateLocator(request.NewLocator))
                    return BadRequest(new { error = "Invalid locator syntax" });

                var success = await _repository.UpdateLocatorAsync(id, request.NewLocator);
                if (!success)
                    return NotFound(new { error = "Object not found or locator update failed" });

                return Ok(new { success = true, message = "Locator updated successfully" });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating locator: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Export objects from repository
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportObjects([FromQuery] string category = null)
        {
            try
            {
                var objects = await _repository.ExportObjectsAsync(category);
                return Ok(new { success = true, data = objects, count = objects.Count, exportedAt = DateTime.Now });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error exporting objects: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Import objects to repository
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> ImportObjects([FromBody] List<TestObject> objects)
        {
            try
            {
                if (objects == null || !objects.Any())
                    return BadRequest(new { error = "Objects list is empty" });

                var count = await _repository.ImportObjectsAsync(objects);
                return Ok(new { success = true, importedCount = count });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error importing objects: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request model for updating locator
    /// </summary>
    public class UpdateLocatorRequest
    {
        public string NewLocator { get; set; }
    }
}
