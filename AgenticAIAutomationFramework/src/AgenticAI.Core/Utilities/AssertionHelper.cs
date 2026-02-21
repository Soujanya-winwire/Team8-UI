using System;
using System.Collections.Generic;

namespace AgenticAI.Core.Utilities
{
    /// <summary>
    /// Lightweight assertion helper supporting soft assertions collection.
    /// </summary>
    public class AssertionHelper
    {
        private readonly List<string> _errors = new();

        public void IsTrue(bool condition, string message)
        {
            if (!condition) _errors.Add(message);
        }

        public void AreEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                _errors.Add(message);
            }
        }

        public void Fail(string message)
        {
            _errors.Add(message);
        }

        public void ThrowIfAny()
        {
            if (_errors.Count > 0)
            {
                throw new AggregateException("Soft assertion failures:\n" + string.Join("\n", _errors));
            }
        }
    }
}
