namespace AgenticAI.Core.Attributes
{
    /// <summary>
    /// Attribute to specify the module for a test
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class TestModuleAttribute : Attribute
    {
        public string Module { get; }

        public TestModuleAttribute(string module)
        {
            Module = module;
        }
    }
}
