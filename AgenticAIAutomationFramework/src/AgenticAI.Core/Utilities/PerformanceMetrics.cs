using System.Diagnostics;
using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Tracks performance metrics for UI automation tests
    /// </summary>
    public class PerformanceMetrics
    {
        private Dictionary<string, PerformanceMetric> _metrics = new();
        private Stack<Stopwatch> _stopwatches = new();

        public class PerformanceMetric
        {
            public string Name { get; set; } = "";
            public long ElapsedMilliseconds { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string? Category { get; set; } = "General";
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        /// <summary>
        /// Start tracking a performance metric
        /// </summary>
        public void StartMetric(string name, string? category = null)
        {
            var sw = Stopwatch.StartNew();
            _stopwatches.Push(sw);
            Logger.Debug($"Performance tracking started for: {name} (Category: {category ?? "General"})");
        }

        /// <summary>
        /// End tracking and record the metric
        /// </summary>
        public long EndMetric(string name, string? category = null)
        {
            if (_stopwatches.Count == 0)
            {
                Logger.Warning($"No active stopwatch for metric: {name}");
                return 0;
            }

            var sw = _stopwatches.Pop();
            sw.Stop();

            var metric = new PerformanceMetric
            {
                Name = name,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                StartTime = DateTime.Now.AddMilliseconds(-sw.ElapsedMilliseconds),
                EndTime = DateTime.Now,
                Category = category ?? "General"
            };

            _metrics[name] = metric;
            Logger.Debug($"Performance metric recorded: {name} = {sw.ElapsedMilliseconds}ms");

            return sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// Get a specific metric
        /// </summary>
        public PerformanceMetric? GetMetric(string name)
        {
            return _metrics.ContainsKey(name) ? _metrics[name] : null;
        }

        /// <summary>
        /// Get all metrics
        /// </summary>
        public IEnumerable<PerformanceMetric> GetAllMetrics()
        {
            return _metrics.Values;
        }

        /// <summary>
        /// Get metrics by category
        /// </summary>
        public IEnumerable<PerformanceMetric> GetMetricsByCategory(string category)
        {
            return _metrics.Values.Where(m => m.Category == category);
        }

        /// <summary>
        /// Get average performance for a category
        /// </summary>
        public double GetAverageForCategory(string category)
        {
            var metrics = GetMetricsByCategory(category).ToList();
            return metrics.Count > 0 ? metrics.Average(m => m.ElapsedMilliseconds) : 0;
        }

        /// <summary>
        /// Get slowest metric in a category
        /// </summary>
        public PerformanceMetric? GetSlowestInCategory(string category)
        {
            return GetMetricsByCategory(category).OrderByDescending(m => m.ElapsedMilliseconds).FirstOrDefault();
        }

        /// <summary>
        /// Get fastest metric in a category
        /// </summary>
        public PerformanceMetric? GetFastestInCategory(string category)
        {
            return GetMetricsByCategory(category).OrderBy(m => m.ElapsedMilliseconds).FirstOrDefault();
        }

        /// <summary>
        /// Add custom metadata to a metric
        /// </summary>
        public void AddMetadata(string metricName, string key, object value)
        {
            if (_metrics.ContainsKey(metricName))
            {
                _metrics[metricName].Metadata[key] = value;
            }
        }

        /// <summary>
        /// Clear all metrics
        /// </summary>
        public void Clear()
        {
            _metrics.Clear();
            _stopwatches.Clear();
        }

        /// <summary>
        /// Generate a performance summary report
        /// </summary>
        public string GenerateSummary()
        {
            var categories = _metrics.Values.Select(m => m.Category).Distinct();
            var summary = new System.Text.StringBuilder();

            summary.AppendLine("=== Performance Metrics Summary ===");
            summary.AppendLine($"Total Metrics: {_metrics.Count}");
            summary.AppendLine();

            foreach (var category in categories)
            {
                var categoryMetrics = GetMetricsByCategory(category!).ToList();
                summary.AppendLine($"Category: {category}");
                summary.AppendLine($"  Count: {categoryMetrics.Count}");
                summary.AppendLine($"  Average: {GetAverageForCategory(category!):F2}ms");
                summary.AppendLine($"  Slowest: {GetSlowestInCategory(category!)?.Name} ({GetSlowestInCategory(category!)?.ElapsedMilliseconds}ms)");
                summary.AppendLine($"  Fastest: {GetFastestInCategory(category!)?.Name} ({GetFastestInCategory(category!)?.ElapsedMilliseconds}ms)");
                summary.AppendLine();
            }

            return summary.ToString();
        }
    }
}
