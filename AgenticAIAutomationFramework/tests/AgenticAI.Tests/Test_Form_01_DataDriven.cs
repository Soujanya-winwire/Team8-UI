using AgenticAI.Core.DataDriven;
using AgenticAI.Core.ZeroCode;
using AgenticAI.UIAutomation.Drivers;
using AgenticAI.Core.Interfaces;

namespace AgenticAI.Tests.Examples
{
    /// <summary>
    /// Example: Execute Test_Form_01 with Excel data
    /// </summary>
    [TestFixture]
    public class Test_Form_01_DataDriven
    {
        /// <summary>
        /// Execute Test_Form_01 scenario with CSV data
        /// </summary>
        [Test]
        [Category("DataDriven")]
        public async Task ExecuteTest_Form_01_WithCSV()
        {
            // Load the recorded scenario
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("Test_Form_01", "Default");

            Assert.That(scenario, Is.Not.Null, "Scenario 'Test_Form_01' not found");

            // Load test data from CSV file
            var dataSet = TestDataReader.ReadFromFile("Test_Form_01_Data.csv");

            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from CSV");

            // Execute the scenario with data
            var runner = new DataDrivenRunner(async () =>
            {
                var driver = await WebDriverFactory.CreateDriverAsync();
                return (IWebDriver)driver;
            });

            var results = await runner.RunAsync(scenario, dataSet);

            // Report results
            Console.WriteLine("\n=== Test Results ===");
            foreach (var result in results)
            {
                Console.WriteLine($"Row {result.RowIndex + 1}: {result.Result.Status}");
                Console.WriteLine($"  Data: {string.Join(", ", result.DataRow.Select(kv => $"{kv.Key}={kv.Value}"))}");
                Console.WriteLine($"  Duration: {result.Result.Duration.TotalSeconds:F2}s");

                if (result.Result.ErrorMessage != null)
                {
                    Console.WriteLine($"  Error: {result.Result.ErrorMessage}");
                }
            }

            // Assert at least one test passed
            var passedCount = results.Count(r => r.Result.Status == Core.Enums.TestStatus.Passed);
            Assert.That(passedCount, Is.GreaterThan(0), "At least one test should pass");
        }

        /// <summary>
        /// Execute Test_Form_01 scenario with JSON data
        /// </summary>
        [Test]
        [Category("DataDriven")]
        public async Task ExecuteTest_Form_01_WithJSON()
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("Test_Form_01", "Default");

            Assert.That(scenario, Is.Not.Null, "Scenario 'Test_Form_01' not found");

            // Load test data from JSON file
            var dataSet = TestDataReader.ReadFromFile("Test_Form_01_Data.json");

            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from JSON");

            var runner = new DataDrivenRunner(async () =>
            {
                var driver = await WebDriverFactory.CreateDriverAsync();
                return (IWebDriver)driver;
            });

            var results = await runner.RunAsync(scenario, dataSet);

            // Summary
            int passed = results.Count(r => r.Result.Status == Core.Enums.TestStatus.Passed);
            int failed = results.Count(r => r.Result.Status == Core.Enums.TestStatus.Failed);

            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Total: {results.Count}");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");

            Assert.That(passed, Is.GreaterThan(0), "At least one test should pass");
        }

        /// <summary>
        /// Execute Test_Form_01 scenario with Excel data (NEW!)
        /// </summary>
        [Test]
        [Category("DataDriven")]
        [Category("Excel")]
        public async Task ExecuteTest_Form_01_WithExcel()
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("Test_Form_01", "Default");

            Assert.That(scenario, Is.Not.Null, "Scenario 'Test_Form_01' not found");

            // Load test data from Excel file (supports .xlsx and .xls)
            var dataSet = TestDataReader.ReadFromFile("Test_Form_01_Data.xlsx");

            Console.WriteLine($"Loaded {dataSet.RowCount} test cases from Excel");
            Console.WriteLine($"Columns: {string.Join(", ", dataSet.Columns)}");

            var runner = new DataDrivenRunner(async () =>
            {
                var driver = await WebDriverFactory.CreateDriverAsync();
                return (IWebDriver)driver;
            });

            var results = await runner.RunAsync(scenario, dataSet);

            // Detailed report
            Console.WriteLine("\n=== Detailed Results ===");
            foreach (var result in results)
            {
                Console.WriteLine($"\nRow {result.RowIndex + 1}:");
                Console.WriteLine($"  Status: {result.Result.Status}");
                Console.WriteLine($"  Steps: {result.Result.TotalSteps} (Passed: {result.Result.PassedSteps}, Failed: {result.Result.FailedSteps})");
                Console.WriteLine($"  Duration: {result.Result.Duration.TotalSeconds:F2}s");

                // Show data values
                foreach (var kvp in result.DataRow)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }

            int passed = results.Count(r => r.Result.Status == Core.Enums.TestStatus.Passed);
            Assert.That(passed, Is.GreaterThan(0), "At least one test should pass");
        }

        /// <summary>
        /// Execute Test_Form_01 with inline CSV data (quick testing)
        /// </summary>
        [Test]
        [Category("DataDriven")]
        [Category("QuickTest")]
        public async Task ExecuteTest_Form_01_WithInlineData()
        {
            var scenarioManager = new ScenarioManager();
            var scenario = scenarioManager.LoadScenario("Test_Form_01", "Default");

            Assert.That(scenario, Is.Not.Null, "Scenario 'Test_Form_01' not found");

            // Define test data inline (no file needed)
            var csvData = @"
firstName,lastName,email,phone,expectedResult
TestUser1,Smith,test1@example.com,555-0001,success
TestUser2,Jones,test2@example.com,555-0002,success
";

            var dataSet = DataSetReader.ParseCsv(csvData);

            var runner = new DataDrivenRunner(async () =>
            {
                var driver = await WebDriverFactory.CreateDriverAsync();
                return (IWebDriver)driver;
            });

            var results = await runner.RunAsync(scenario, dataSet);

            foreach (var result in results)
            {
                Console.WriteLine($"{result.DataRow["firstName"]} {result.DataRow["lastName"]}: {result.Result.Status}");
            }

            Assert.That(results.Count, Is.EqualTo(2));
        }
    }
}
