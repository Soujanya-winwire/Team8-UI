using AgenticAI.Core.Configuration;
using AgenticAI.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        /// <summary>
        /// Get current framework configuration
        /// </summary>
        [HttpGet]
        public IActionResult GetConfiguration()
        {
            try
            {
                var config = AgenticAI.Core.Configuration.ConfigurationManager.Instance.FrameworkConfig;
                
                return Ok(new
                {
                    success = true,
                    configuration = config
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Update framework configuration
        /// </summary>
        [HttpPut]
        public IActionResult UpdateConfiguration([FromBody] FrameworkConfiguration config)
        {
            try
            {
                AgenticAI.Core.Configuration.ConfigurationManager.Instance.UpdateFrameworkConfiguration(c =>
                {
                    c.AutomationFramework = config.AutomationFramework;
                    c.Browser = config.Browser;
                    c.OperatingSystem = config.OperatingSystem;
                    c.Environment = config.Environment;
                    c.ExecutionMode = config.ExecutionMode;
                    c.BaseUrl = config.BaseUrl;
                    c.Headless = config.Headless;
                    c.EnableVideo = config.EnableVideo;
                    c.EnableScreenshots = config.EnableScreenshots;
                    c.EnableTracing = config.EnableTracing;
                    c.MaxRetryCount = config.MaxRetryCount;
                    c.TimeoutInSeconds = config.TimeoutInSeconds;
                    c.ParallelWorkers = config.ParallelWorkers;
                    c.EnableSelfHealing = config.EnableSelfHealing;
                    c.EnableAccessibilityTesting = config.EnableAccessibilityTesting;
                    c.EnableVisualRegression = config.EnableVisualRegression;
                    c.EnablePerformanceMetrics = config.EnablePerformanceMetrics;
                });
                
                return Ok(new
                {
                    success = true,
                    message = "Configuration updated successfully",
                    configuration = AgenticAI.Core.Configuration.ConfigurationManager.Instance.FrameworkConfig
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get environment configuration
        /// </summary>
        [HttpGet("environment/{env}")]
        public IActionResult GetEnvironmentConfiguration(string env)
        {
            try
            {
                if (!Enum.TryParse<AgenticAI.Core.Enums.Environment>(env, true, out var environment))
                {
                    return BadRequest(new { success = false, error = "Invalid environment" });
                }
                
                var config = AgenticAI.Core.Configuration.ConfigurationManager.Instance.GetEnvironmentConfig(environment);
                
                return Ok(new
                {
                    success = true,
                    environment = env,
                    configuration = config
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Update environment configuration
        /// </summary>
        [HttpPut("environment/{env}")]
        public IActionResult UpdateEnvironmentConfiguration(string env, [FromBody] EnvironmentConfiguration config)
        {
            try
            {
                if (!Enum.TryParse<AgenticAI.Core.Enums.Environment>(env, true, out var environment))
                {
                    return BadRequest(new { success = false, error = "Invalid environment" });
                }
                
                AgenticAI.Core.Configuration.ConfigurationManager.Instance.SaveEnvironmentConfiguration(environment, config);
                
                return Ok(new
                {
                    success = true,
                    message = "Environment configuration updated successfully",
                    environment = env,
                    configuration = config
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get available browser types
        /// </summary>
        [HttpGet("browsers")]
        public IActionResult GetBrowserTypes()
        {
            var browsers = Enum.GetValues<BrowserType>()
                .Select(b => new { name = b.ToString(), value = (int)b })
                .ToList();
                
            return Ok(new { success = true, browsers });
        }

        /// <summary>
        /// Get available environments
        /// </summary>
        [HttpGet("environments")]
        public IActionResult GetEnvironments()
        {
            var environments = Enum.GetValues<AgenticAI.Core.Enums.Environment>()
                .Select(e => new { name = e.ToString(), value = (int)e })
                .ToList();
                
            return Ok(new { success = true, environments });
        }

        /// <summary>
        /// Get available execution modes
        /// </summary>
        [HttpGet("execution-modes")]
        public IActionResult GetExecutionModes()
        {
            var modes = Enum.GetValues<ExecutionMode>()
                .Select(m => new { name = m.ToString(), value = (int)m })
                .ToList();
                
            return Ok(new { success = true, modes });
        }
    }
}
