namespace AgenticAI.Core.ZeroCode.Models
{
    /// <summary>
    /// Represents a zero-code test scenario
    /// </summary>
    public class TestScenario
    {
        public string ScenarioId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Module { get; set; } = "Default";
        public List<string> Tags { get; set; } = new();
        public string StartUrl { get; set; } = "";
        public List<RecordedAction> Actions { get; set; } = new();
        public List<Assertion> Assertions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>
    /// Represents a test assertion
    /// </summary>
    public class Assertion
    {
        public string Type { get; set; } = ""; // ElementVisible, TextEquals, UrlContains, etc.
        public string Locator { get; set; } = "";
        public string? ExpectedValue { get; set; }
        public string? Description { get; set; }
    }
}
