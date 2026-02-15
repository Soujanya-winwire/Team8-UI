namespace AgenticAI.Core.Attributes
{
    /// <summary>
    /// Attribute to enable retry mechanism for flaky tests
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RetryAttribute : Attribute
    {
        public int MaxRetries { get; }

        public RetryAttribute(int maxRetries = 2)
        {
            MaxRetries = maxRetries;
        }
    }
}
