using AgenticAI.Core.Enums;
using AgenticAI.Core.Utilities;
using NUnit.Framework;

namespace AgenticAI.Tests
{
    [TestFixture]
    public class FlakinessDetectorTests
    {
        [Test]
        public void IsTestFlaky_ReturnsFalse_WhenBelowMinimumRuns()
        {
            var detector = FlakinessDetector.Instance;
            var testName = $"Smoke.NotEnoughRuns_{Guid.NewGuid():N}";
            detector.Clear();

            detector.RecordExecution(testName, TestStatus.Failed, 100);
            detector.RecordExecution(testName, TestStatus.Passed, 100);

            Assert.That(detector.IsTestFlaky(testName), Is.False);
        }

        [Test]
        public void IsTestFlaky_ReturnsTrue_WhenFailureRateWithinThreshold()
        {
            var detector = FlakinessDetector.Instance;
            var testName = $"Smoke.Flaky_{Guid.NewGuid():N}";
            detector.Clear();

            // 2 failures out of 5 runs = 40% failure rate -> flaky (default threshold 30%)
            detector.RecordExecution(testName, TestStatus.Passed, 100);
            detector.RecordExecution(testName, TestStatus.Failed, 100, "boom");
            detector.RecordExecution(testName, TestStatus.Passed, 100);
            detector.RecordExecution(testName, TestStatus.Failed, 100, "boom again");
            detector.RecordExecution(testName, TestStatus.Passed, 100);

            Assert.That(detector.IsTestFlaky(testName), Is.True);

            var report = detector.GetFlakinessReport(testName);
            Assert.Multiple(() =>
            {
                Assert.That(report.TotalRuns, Is.EqualTo(5));
                Assert.That(report.FailCount, Is.EqualTo(2));
                Assert.That(report.IsFlaky, Is.True);
            });
        }

        [Test]
        public void IsTestFlaky_ReturnsFalse_WhenConsistentlyFailing()
        {
            var detector = FlakinessDetector.Instance;
            var testName = $"Smoke.AlwaysFails_{Guid.NewGuid():N}";
            detector.Clear();

            for (int i = 0; i < 5; i++)
            {
                detector.RecordExecution(testName, TestStatus.Failed, 100, "consistently broken");
            }

            // A consistently failing test is broken, not flaky
            Assert.That(detector.IsTestFlaky(testName), Is.False);
        }

        [Test]
        public void SaveToFile_ThenLoadFromFile_RoundTripsHistory()
        {
            var detector = FlakinessDetector.Instance;
            var testName = $"Smoke.Persisted_{Guid.NewGuid():N}";
            detector.Clear();

            detector.RecordExecution(testName, TestStatus.Passed, 50);
            detector.RecordExecution(testName, TestStatus.Failed, 75, "flaky failure");

            var tempFile = Path.Combine(Path.GetTempPath(), $"flakiness-history-{Guid.NewGuid():N}.json");
            try
            {
                detector.SaveToFile(tempFile);
                Assert.That(File.Exists(tempFile), Is.True);

                detector.Clear();
                Assert.That(detector.GetExecutionHistory(testName), Is.Null);

                detector.LoadFromFile(tempFile);
                var history = detector.GetExecutionHistory(testName);

                Assert.That(history, Is.Not.Null);
                Assert.That(history!.TotalRuns, Is.EqualTo(2));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Test]
        public void LoadFromFile_DoesNotThrow_WhenFileMissing()
        {
            var detector = FlakinessDetector.Instance;
            var missingFile = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");

            Assert.DoesNotThrow(() => detector.LoadFromFile(missingFile));
        }
    }
}
