namespace AgenticAI.Core.Attributes
{
    /// <summary>
    /// Attribute to tag tests for selective execution
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class TestTagAttribute : Attribute
    {
        public string Tag { get; }

        public TestTagAttribute(string tag)
        {
            Tag = tag;
        }
    }
}
