using AgenticAI.Core.Logging;
using System.Security.Cryptography;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Advanced screenshot comparison for visual regression testing
    /// </summary>
    public class ScreenshotComparison
    {
        public class ScreenshotInfo
        {
            public string Id { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string Hash { get; set; } = "";
            public DateTime CaptureTime { get; set; } = DateTime.Now;
            public int Width { get; set; }
            public int Height { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        public class ComparisonResult
        {
            public bool AreIdentical { get; set; }
            public double SimilarityPercentage { get; set; } // 0-100
            public List<string> Differences { get; set; } = new();
            public string? ComparisonImagePath { get; set; }
            public long FileSizeDifferenceMb { get; set; }
            public DateTime ComparisonTime { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Compute hash of a screenshot
        /// </summary>
        public static string ComputeScreenshotHash(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Screenshot not found: {imagePath}");
            }

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(imagePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return Convert.ToHexString(hash);
                }
            }
        }

        /// <summary>
        /// Create screenshot info from file
        /// </summary>
        public static ScreenshotInfo CreateScreenshotInfo(string imagePath, string id)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Screenshot not found: {imagePath}");
            }

            var fileInfo = new FileInfo(imagePath);
            var hash = ComputeScreenshotHash(imagePath);

            return new ScreenshotInfo
            {
                Id = id,
                FilePath = imagePath,
                Hash = hash,
                CaptureTime = DateTime.Now,
                Metadata = new Dictionary<string, object>
                {
                    { "FileSize", fileInfo.Length },
                    { "LastModified", fileInfo.LastWriteTime }
                }
            };
        }

        /// <summary>
        /// Compare two screenshots
        /// </summary>
        public static ComparisonResult CompareScreenshots(string baselinePath, string actualPath)
        {
            Logger.Info($"Comparing screenshots: {Path.GetFileName(baselinePath)} vs {Path.GetFileName(actualPath)}");

            var baselineInfo = CreateScreenshotInfo(baselinePath, "baseline");
            var actualInfo = CreateScreenshotInfo(actualPath, "actual");

            var result = new ComparisonResult
            {
                AreIdentical = baselineInfo.Hash == actualInfo.Hash,
                ComparisonTime = DateTime.Now
            };

            if (result.AreIdentical)
            {
                result.SimilarityPercentage = 100;
                Logger.Info("Screenshots are identical");
                return result;
            }

            // Calculate similarity based on file size difference
            var baselineSize = new FileInfo(baselinePath).Length;
            var actualSize = new FileInfo(actualPath).Length;
            var sizeDiff = Math.Abs(baselineSize - actualSize);
            var avgSize = (baselineSize + actualSize) / 2.0;
            var sizeSimilarity = Math.Max(0, 100 - (sizeDiff / avgSize * 100));

            result.SimilarityPercentage = sizeSimilarity;
            result.FileSizeDifferenceMb = sizeDiff / (1024 * 1024);

            if (sizeSimilarity < 80)
            {
                result.Differences.Add($"Significant size difference: {result.FileSizeDifferenceMb}MB");
            }

            Logger.Warning($"Screenshots differ. Similarity: {result.SimilarityPercentage:F1}%");

            return result;
        }

        /// <summary>
        /// Compare screenshot with baseline and create diff
        /// </summary>
        public static async Task<ComparisonResult> CompareWithBaselineAsync(
            string actualScreenshotPath,
            string baselineDirectory,
            string screenshotName)
        {
            var baselinePath = Path.Combine(baselineDirectory, $"{screenshotName}_baseline.png");

            if (!File.Exists(baselinePath))
            {
                Logger.Warning($"Baseline screenshot not found: {baselinePath}");
                Directory.CreateDirectory(baselineDirectory);
                File.Copy(actualScreenshotPath, baselinePath);

                return new ComparisonResult
                {
                    AreIdentical = true,
                    SimilarityPercentage = 100,
                    Differences = new List<string> { "Baseline created from actual screenshot" }
                };
            }

            return CompareScreenshots(baselinePath, actualScreenshotPath);
        }

        /// <summary>
        /// Generate a report of visual regression
        /// </summary>
        public static string GenerateComparisonReport(ComparisonResult result)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("=== Screenshot Comparison Report ===");
            report.AppendLine($"Identical: {result.AreIdentical}");
            report.AppendLine($"Similarity: {result.SimilarityPercentage:F2}%");
            report.AppendLine($"Comparison Time: {result.ComparisonTime:yyyy-MM-dd HH:mm:ss}");

            if (result.Differences.Count > 0)
            {
                report.AppendLine("Differences Found:");
                foreach (var diff in result.Differences)
                {
                    report.AppendLine($"  - {diff}");
                }
            }

            if (!string.IsNullOrEmpty(result.ComparisonImagePath))
            {
                report.AppendLine($"Comparison Image: {result.ComparisonImagePath}");
            }

            return report.ToString();
        }

        /// <summary>
        /// Archive screenshots for regression testing
        /// </summary>
        public static async Task ArchiveScreenshotAsync(
            string screenshotPath,
            string archiveDirectory,
            string screenshotName,
            string testName)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var archivePath = Path.Combine(
                archiveDirectory,
                testName,
                $"{screenshotName}_{timestamp}.png"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            if (File.Exists(screenshotPath))
            {
                File.Copy(screenshotPath, archivePath, overwrite: true);
                Logger.Debug($"Screenshot archived: {archivePath}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Get all baseline screenshots in directory
        /// </summary>
        public static List<ScreenshotInfo> GetBaselineScreenshots(string baselineDirectory)
        {
            var screenshots = new List<ScreenshotInfo>();

            if (!Directory.Exists(baselineDirectory))
            {
                return screenshots;
            }

            var files = Directory.GetFiles(baselineDirectory, "*_baseline.png");
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file)
                    .Replace("_baseline", "");
                screenshots.Add(CreateScreenshotInfo(file, name));
            }

            return screenshots;
        }

        /// <summary>
        /// Clean old archived screenshots
        /// </summary>
        public static void CleanOldScreenshots(string archiveDirectory, int daysToKeep = 30)
        {
            if (!Directory.Exists(archiveDirectory))
            {
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var files = Directory.GetFiles(archiveDirectory, "*.png", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                    Logger.Debug($"Deleted old screenshot: {file}");
                }
            }
        }
    }
}
