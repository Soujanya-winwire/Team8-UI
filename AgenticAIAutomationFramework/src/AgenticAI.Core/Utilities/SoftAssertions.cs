using AgenticAI.Core.Logging;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Soft assertions allow tests to continue even when assertions fail
    /// All failures are collected and reported at the end
    /// </summary>
    public class SoftAssertions
    {
        private List<AssertionFailure> _failures = new();

        public class AssertionFailure
        {
            public string AssertionName { get; set; } = "";
            public string Expected { get; set; } = "";
            public string Actual { get; set; } = "";
            public string Message { get; set; } = "";
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Assert that a condition is true
        /// </summary>
        public void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                RecordFailure("AssertTrue", "true", "false", message);
            }
        }

        /// <summary>
        /// Assert that a condition is false
        /// </summary>
        public void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                RecordFailure("AssertFalse", "false", "true", message);
            }
        }

        /// <summary>
        /// Assert that two values are equal
        /// </summary>
        public void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                RecordFailure("AssertEqual", expected?.ToString() ?? "null", actual?.ToString() ?? "null", message);
            }
        }

        /// <summary>
        /// Assert that a string contains a substring
        /// </summary>
        public void AssertContains(string text, string substring, string message)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(substring))
            {
                RecordFailure("AssertContains", $"contains '{substring}'", text ?? "null", message);
            }
        }

        /// <summary>
        /// Assert that a string does not contain a substring
        /// </summary>
        public void AssertNotContains(string text, string substring, string message)
        {
            if (!string.IsNullOrEmpty(text) && text.Contains(substring))
            {
                RecordFailure("AssertNotContains", $"does not contain '{substring}'", text, message);
            }
        }

        /// <summary>
        /// Assert that a value is null
        /// </summary>
        public void AssertNull(object? value, string message)
        {
            if (value != null)
            {
                RecordFailure("AssertNull", "null", value.ToString() ?? "object", message);
            }
        }

        /// <summary>
        /// Assert that a value is not null
        /// </summary>
        public void AssertNotNull(object? value, string message)
        {
            if (value == null)
            {
                RecordFailure("AssertNotNull", "not null", "null", message);
            }
        }

        /// <summary>
        /// Assert that a collection is empty
        /// </summary>
        public void AssertEmpty<T>(IEnumerable<T> collection, string message)
        {
            if (collection.Any())
            {
                RecordFailure("AssertEmpty", "empty collection", $"collection with {collection.Count()} items", message);
            }
        }

        /// <summary>
        /// Assert that a collection is not empty
        /// </summary>
        public void AssertNotEmpty<T>(IEnumerable<T> collection, string message)
        {
            if (!collection.Any())
            {
                RecordFailure("AssertNotEmpty", "non-empty collection", "empty collection", message);
            }
        }

        /// <summary>
        /// Assert that a string matches a pattern
        /// </summary>
        public void AssertMatches(string pattern, string text, string message)
        {
            if (string.IsNullOrEmpty(text) || !System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
            {
                RecordFailure("AssertMatches", $"matches pattern '{pattern}'", text ?? "null", message);
            }
        }

        /// <summary>
        /// Record a custom assertion failure
        /// </summary>
        public void RecordFailure(string assertionName, string expected, string actual, string message)
        {
            var failure = new AssertionFailure
            {
                AssertionName = assertionName,
                Expected = expected,
                Actual = actual,
                Message = message,
                Timestamp = DateTime.Now
            };

            _failures.Add(failure);
            Logger.Warning($"Soft Assertion Failed: {message} (Expected: {expected}, Actual: {actual})");
        }

        /// <summary>
        /// Check if there are any assertion failures
        /// </summary>
        public bool HasFailures => _failures.Count > 0;

        /// <summary>
        /// Get the number of failures
        /// </summary>
        public int FailureCount => _failures.Count;

        /// <summary>
        /// Get all failures
        /// </summary>
        public IReadOnlyList<AssertionFailure> GetFailures()
        {
            return _failures.AsReadOnly();
        }

        /// <summary>
        /// Assert all - throws exception if there are any failures
        /// </summary>
        public void AssertAll()
        {
            if (_failures.Count > 0)
            {
                var message = new System.Text.StringBuilder();
                message.AppendLine($"Soft Assertions Failed: {_failures.Count} assertion(s) failed");
                foreach (var failure in _failures)
                {
                    message.AppendLine($"  - {failure.AssertionName}: {failure.Message}");
                    message.AppendLine($"    Expected: {failure.Expected}, Actual: {failure.Actual}");
                }

                Clear();
                throw new AssertionException(message.ToString());
            }
        }

        /// <summary>
        /// Get a formatted report of failures
        /// </summary>
        public string GetFailureReport()
        {
            if (_failures.Count == 0)
            {
                return "No assertion failures recorded.";
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== Soft Assertion Failures: {_failures.Count} ===");
            for (int i = 0; i < _failures.Count; i++)
            {
                var failure = _failures[i];
                report.AppendLine($"{i + 1}. {failure.AssertionName}");
                report.AppendLine($"   Message: {failure.Message}");
                report.AppendLine($"   Expected: {failure.Expected}");
                report.AppendLine($"   Actual: {failure.Actual}");
                report.AppendLine($"   Time: {failure.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                report.AppendLine();
            }

            return report.ToString();
        }

        /// <summary>
        /// Clear all recorded failures
        /// </summary>
        public void Clear()
        {
            _failures.Clear();
        }
    }

    /// <summary>
    /// Exception thrown when soft assertions fail
    /// </summary>
    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }
}
