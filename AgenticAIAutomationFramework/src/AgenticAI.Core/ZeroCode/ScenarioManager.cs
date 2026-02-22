using AgenticAI.Core.ZeroCode.Models;
using Newtonsoft.Json;

namespace AgenticAI.Core.ZeroCode
{
    /// <summary>
    /// Manages zero-code test scenarios - save, load, and execute
    /// </summary>
    public class ScenarioManager
    {
        private readonly string _scenariosPath;

        public ScenarioManager(string scenariosPath = "TestScenarios")
        {
            // Try to resolve the path relative to the current directory first
            _scenariosPath = scenariosPath;
            
            // If the path doesn't exist, try to find it relative to the solution root
            if (!Directory.Exists(_scenariosPath))
            {
                var currentDir = Directory.GetCurrentDirectory();
                var solutionPath = FindSolutionRoot(currentDir);
                
                if (solutionPath != null)
                {
                    _scenariosPath = Path.Combine(solutionPath, scenariosPath);
                }
                
                // Create directory if it still doesn't exist
                if (!Directory.Exists(_scenariosPath))
                {
                    Directory.CreateDirectory(_scenariosPath);
                }
            }
        }

        public void SaveScenario(TestScenario scenario)
        {
            scenario.ModifiedAt = DateTime.Now;
            
            // Build unified Steps collection for inline execution
            BuildUnifiedStepsCollection(scenario);
            
            var moduleDir = Path.Combine(_scenariosPath, scenario.Module);
            if (!Directory.Exists(moduleDir))
            {
                Directory.CreateDirectory(moduleDir);
            }

            var filePath = Path.Combine(moduleDir, $"{SanitizeFileName(scenario.Name)}.json");
            var json = JsonConvert.SerializeObject(scenario, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        /// <summary>
        /// Builds the unified Steps collection from Actions and Assertions
        /// This ensures assertions execute inline at their correct position
        /// </summary>
        private void BuildUnifiedStepsCollection(TestScenario scenario)
        {
            // Initialize Steps collection
            scenario.Steps = new List<TestStep>();
            int stepOrder = 0;
            
            // Process each action and its associated assertions
            for (int i = 0; i < scenario.Actions.Count; i++)
            {
                // Add the action as a step
                scenario.Steps.Add(new TestStep
                {
                    StepType = "Action",
                    Order = stepOrder++,
                    Action = scenario.Actions[i],
                    Assertion = null
                });
                
                // Find assertions that should execute after this action
                var assertionsForThisAction = scenario.Assertions
                    .Where(a => a.ExecuteAfterActionIndex == i)
                    .ToList();
                
                // Add each assertion as a step immediately after the action
                foreach (var assertion in assertionsForThisAction)
                {
                    scenario.Steps.Add(new TestStep
                    {
                        StepType = "Assertion",
                        Order = stepOrder++,
                        Action = null,
                        Assertion = assertion
                    });
                }
            }
            
            // Add any assertions without a specific action index at the end
            var remainingAssertions = scenario.Assertions
                .Where(a => !a.ExecuteAfterActionIndex.HasValue)
                .ToList();
            
            foreach (var assertion in remainingAssertions)
            {
                scenario.Steps.Add(new TestStep
                {
                    StepType = "Assertion",
                    Order = stepOrder++,
                    Action = null,
                    Assertion = assertion
                });
            }
        }

        public TestScenario? LoadScenario(string scenarioName, string module = "Default")
        {
            var filePath = Path.Combine(_scenariosPath, module, $"{SanitizeFileName(scenarioName)}.json");
            
            if (!File.Exists(filePath))
            {
                // Log for debugging
                Console.WriteLine($"Scenario file not found: {filePath}");
                Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"Scenarios path: {_scenariosPath}");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var scenario = JsonConvert.DeserializeObject<TestScenario>(json);
            
            if (scenario != null)
            {
                // Ensure Steps collection is built for scenarios that don't have it
                // (backward compatibility with legacy scenarios)
                if (scenario.Steps == null || scenario.Steps.Count == 0)
                {
                    BuildUnifiedStepsCollection(scenario);
                }
            }
            
            return scenario;
        }

        public List<TestScenario> LoadAllScenarios()
        {
            var scenarios = new List<TestScenario>();
            
            if (!Directory.Exists(_scenariosPath))
            {
                return scenarios;
            }

            var files = Directory.GetFiles(_scenariosPath, "*.json", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var scenario = JsonConvert.DeserializeObject<TestScenario>(json);
                    if (scenario != null)
                    {
                        // Ensure Steps collection is built for backward compatibility
                        if (scenario.Steps == null || scenario.Steps.Count == 0)
                        {
                            BuildUnifiedStepsCollection(scenario);
                        }
                        scenarios.Add(scenario);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load scenario from {file}: {ex.Message}");
                }
            }

            return scenarios;
        }

        public List<TestScenario> LoadScenariosByModule(string module)
        {
            return LoadAllScenarios().Where(s => s.Module == module).ToList();
        }

        public List<TestScenario> LoadScenariosByTag(string tag)
        {
            return LoadAllScenarios().Where(s => s.Tags.Contains(tag)).ToList();
        }

        public void DeleteScenario(string scenarioName, string module = "Default")
        {
            var filePath = Path.Combine(_scenariosPath, module, $"{SanitizeFileName(scenarioName)}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        private string? FindSolutionRoot(string currentPath)
        {
            var directory = new DirectoryInfo(currentPath);
            
            while (directory != null)
            {
                // Look for .sln file or TestScenarios folder
                if (directory.GetFiles("*.sln").Any() || 
                    Directory.Exists(Path.Combine(directory.FullName, "TestScenarios")))
                {
                    return directory.FullName;
                }
                
                directory = directory.Parent;
            }
            
            return null;
        }
    }
}
