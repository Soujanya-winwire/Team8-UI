using AgenticAI.Core.TestBase;
using NUnit.Framework;

namespace AgenticAI.Tests.Demo
{
    /// <summary>
    /// Standalone demo for the FlakinessDetector wiring in BaseTest. Not part of the regular
    /// regression suite (excluded by the "ManualDemo" category) — run it directly to see the
    /// flakiness-history.json and flakiness-report.txt artifacts get produced across repeated runs.
    ///
    /// Usage:
    ///   dotnet test --filter "Category=ManualDemo"   (run this 4-5 times in a row)
    ///   then inspect TestResults/Logs/flakiness-history.json and TestResults/Reports/flakiness-report.txt
    /// </summary>
    [TestFixture]
    [Category("ManualDemo")]
    public class FlakyDetectionDemoTests : BaseTest
    {
        private static readonly string CounterFile = Path.Combine(Path.GetTempPath(), "agenticai-demo-run-counter.txt");

        [Test]
        public void DemoTest_AlternatesPassAndFail_ToSimulateFlakiness()
        {
            var runNumber = GetAndIncrementRunNumber();
            LogStep("CheckRunParity", $"Run #{runNumber}", AgenticAI.Core.Enums.TestStatus.Passed);

            // Fails on every 3rd run to produce a failure rate that crosses the flakiness threshold
            // after a handful of executions, without failing (or passing) every single time.
            Assert.That(runNumber % 3, Is.Not.EqualTo(0),
                $"Simulated intermittent failure on run #{runNumber} (fails every 3rd run by design)");
        }

        private static int GetAndIncrementRunNumber()
        {
            int runNumber = File.Exists(CounterFile) ? int.Parse(File.ReadAllText(CounterFile)) + 1 : 1;
            File.WriteAllText(CounterFile, runNumber.ToString());
            return runNumber;
        }
    }
}
