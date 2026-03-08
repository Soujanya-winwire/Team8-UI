using AgenticAI.Core.DataDriven;
using AgenticAI.Core.ZeroCode;
using AgenticAI.Core.Interfaces;
using AgenticAI.Core.Enums;

namespace AgenticAI.Core.Examples
{
    /// <summary>
    /// Example demonstrating how to use the advanced data-driven testing features
    /// </summary>
    public class DataDrivenTestingExamples
    {
        /// <summary>
        /// Example 1: Execute a login test with CSV data
        /// </summary>
        public static async Task ExecuteLoginTestWithCsvData(Func<Task<IWebDriver>> driverFactory)
        {
            // Load the test scenario
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("LoginTest", "Authentication");

            if (scenario == null)
            {
                Console.WriteLine("Scenario not found!");
                return;
            }

            // Load test data from CSV file
            var dataSet = TestDataReader.ReadFromFile("SampleLogin.csv");
            
            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from CSV");

            // Execute the scenario with data
            var runner = new DataDrivenRunner(driverFactory);
            var results = await runner.RunAsync(scenario, dataSet);

            // Report results
            Console.WriteLine("\n=== Test Results ===");
            foreach (var result in results)
            {
                Console.WriteLine($"Row {result.RowIndex + 1}: {result.Result.Status}");
                Console.WriteLine($"  Username: {result.DataRow["username"]}");
                Console.WriteLine($"  Expected: {result.DataRow["expectedResult"]}");
                Console.WriteLine($"  Duration: {result.Result.Duration}");
            }
        }

        /// <summary>
        /// Example 2: Execute user registration test with JSON data
        /// </summary>
        public static async Task ExecuteRegistrationTestWithJsonData(Func<Task<IWebDriver>> driverFactory)
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("UserRegistration", "Users");

            if (scenario == null)
            {
                Console.WriteLine("Scenario not found!");
                return;
            }

            // Load test data from JSON file
            var dataSet = TestDataReader.ReadFromFile("SampleUsers.json");
            
            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from JSON");

            var runner = new DataDrivenRunner(driverFactory);
            var results = await runner.RunAsync(scenario, dataSet);

            // Count successes and failures
            int passed = results.Count(r => r.Result.Status == TestStatus.Passed);
            int failed = results.Count(r => r.Result.Status == TestStatus.Failed);

            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Total: {results.Count}");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");
        }

        /// <summary>
        /// Example 3: Execute product search test with Excel data
        /// </summary>
        public static async Task ExecuteProductSearchWithExcelData(Func<Task<IWebDriver>> driverFactory)
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("ProductSearch", "Ecommerce");

            if (scenario == null)
            {
                Console.WriteLine("Scenario not found!");
                return;
            }

            // Load test data from Excel file
            var dataSet = TestDataReader.ReadFromFile("SampleProducts.xlsx");
            
            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from Excel");
            Console.WriteLine($"Columns: {string.Join(", ", dataSet.Columns)}");

            var runner = new DataDrivenRunner(driverFactory);
            var results = await runner.RunAsync(scenario, dataSet);

            // Generate detailed report
            foreach (var result in results)
            {
                var productName = result.DataRow.GetValueOrDefault("productName", "N/A");
                var status = result.Result.Status;
                
                Console.WriteLine($"Product: {productName} - Status: {status}");
                Console.WriteLine($"  Duration: {result.Result.Duration}");
                Console.WriteLine($"  Steps: {result.Result.TotalSteps} (Passed: {result.Result.PassedSteps}, Failed: {result.Result.FailedSteps})");
            }
        }

        /// <summary>
        /// Example 4: Parse inline CSV data for quick testing
        /// </summary>
        public static async Task ExecuteWithInlineCsvData(Func<Task<IWebDriver>> driverFactory)
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("QuickTest", "Smoke");

            if (scenario == null)
            {
                Console.WriteLine("Scenario not found!");
                return;
            }

            // Define test data inline
            var csvData = @"
username,password,expectedResult
testuser1,pass123,success
testuser2,pass456,success
invaliduser,wrongpass,failure
";

            // Parse the CSV data
            var dataSet = DataSetReader.ParseCsv(csvData);

            var runner = new DataDrivenRunner(driverFactory);
            var results = await runner.RunAsync(scenario, dataSet);

            foreach (var result in results)
            {
                Console.WriteLine($"{result.DataRow["username"]}: {result.Result.Status}");
            }
        }

        /// <summary>
        /// Example 5: Parse inline JSON data for API testing
        /// </summary>
        public static async Task ExecuteWithInlineJsonData(Func<Task<IWebDriver>> driverFactory)
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("ApiTest", "API");

            if (scenario == null)
            {
                Console.WriteLine("Scenario not found!");
                return;
            }

            // Define test data as JSON
            var jsonData = @"[
                {
                    ""endpoint"": ""/api/users"",
                    ""method"": ""GET"",
                    ""expectedStatus"": ""200""
                },
                {
                    ""endpoint"": ""/api/products"",
                    ""method"": ""POST"",
                    ""expectedStatus"": ""201""
                }
            ]";

            var dataSet = DataSetReader.ParseJson(jsonData);

            var runner = new DataDrivenRunner(driverFactory);
            var results = await runner.RunAsync(scenario, dataSet);

            foreach (var result in results)
            {
                Console.WriteLine($"{result.DataRow["method"]} {result.DataRow["endpoint"]}: {result.Result.Status}");
            }
        }

        /// <summary>
        /// Example 6: Use both placeholder syntaxes
        /// </summary>
        public static void DemonstratePlaceholderSubstitution()
        {
            var testData = new Dictionary<string, string>
            {
                { "username", "john.doe" },
                { "password", "SecurePass123" },
                { "email", "john.doe@example.com" },
                { "firstName", "John" },
                { "lastName", "Doe" }
            };

            // Both syntaxes work
            var template1 = "Login with ${username} and ${password}";
            var template2 = "Email: {{email}}, Name: {{firstName}} {{lastName}}";
            var template3 = "Mixed: ${username} / {{email}}";

            var result1 = DataSetReader.SubstitutePlaceholders(template1, testData);
            var result2 = DataSetReader.SubstitutePlaceholders(template2, testData);
            var result3 = DataSetReader.SubstitutePlaceholders(template3, testData);

            Console.WriteLine(result1);
            // Output: Login with john.doe and SecurePass123

            Console.WriteLine(result2);
            // Output: Email: john.doe@example.com, Name: John Doe

            Console.WriteLine(result3);
            // Output: Mixed: john.doe / john.doe@example.com
        }

        /// <summary>
        /// Example 7: List available test data files
        /// </summary>
        public static void ListAvailableTestDataFiles()
        {
            var files = TestDataReader.GetAvailableTestDataFiles();

            Console.WriteLine("Available Test Data Files:");
            foreach (var file in files)
            {
                Console.WriteLine($"  - {file}");
            }
        }

        /// <summary>
        /// Example 8: Generate sample Excel files for testing
        /// </summary>
        public static void GenerateSampleExcelFiles()
        {
            var testDataPath = TestDataReader.TestDataPath;

            // Create sample users Excel file
            var usersPath = Path.Combine(testDataPath, "SampleUsers.xlsx");
            ExcelDataGenerator.CreateSampleUsersExcel(usersPath);
            Console.WriteLine($"Created: {usersPath}");

            // Create sample products Excel file
            var productsPath = Path.Combine(testDataPath, "SampleProducts.xlsx");
            ExcelDataGenerator.CreateSampleProductsExcel(productsPath);
            Console.WriteLine($"Created: {productsPath}");
        }

        /// <summary>
        /// Example 9: Custom configuration of TestData path
        /// </summary>
        public static void ConfigureCustomTestDataPath()
        {
            // Set a custom path to TestData folder
            TestDataReader.TestDataPath = @"C:\MyProject\CustomTestData";

            // Now all file reads will use this path
            var dataSet = TestDataReader.ReadFromFile("MyCustomData.csv");
        }

        /// <summary>
        /// Example 10: Error handling and validation
        /// </summary>
        public static async Task ExecuteWithErrorHandling(Func<Task<IWebDriver>> driverFactory)
        {
            try
            {
                var scenarioManager = new ScenarioManager();
                var scenario = scenarioManager.LoadScenario("MyTest", "MyModule");

                if (scenario == null)
                {
                    throw new InvalidOperationException("Scenario not found");
                }

                // Try to load test data
                DataTestSet dataSet;
                try
                {
                    dataSet = TestDataReader.ReadFromFile("TestData.csv");
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine($"Test data file not found: {ex.Message}");
                    Console.WriteLine("Available files:");
                    foreach (var file in TestDataReader.GetAvailableTestDataFiles())
                    {
                        Console.WriteLine($"  - {file}");
                    }
                    return;
                }

                // Validate data has required columns
                var requiredColumns = new[] { "username", "password" };
                var missingColumns = requiredColumns.Where(col => !dataSet.Columns.Contains(col, StringComparer.OrdinalIgnoreCase)).ToList();

                if (missingColumns.Any())
                {
                    Console.WriteLine($"Missing required columns: {string.Join(", ", missingColumns)}");
                    Console.WriteLine($"Available columns: {string.Join(", ", dataSet.Columns)}");
                    return;
                }

                // Execute tests
                var runner = new DataDrivenRunner(driverFactory);
                var results = await runner.RunAsync(scenario, dataSet);

                // Process results
                Console.WriteLine($"Executed {results.Count} test cases");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
