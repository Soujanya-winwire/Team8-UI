namespace AgenticAI.Core.ZeroCode.Models
{
    /// <summary>
    /// Represents a recorded user action
    /// </summary>
    public class RecordedAction
    {
        public string ActionType { get; set; } = ""; // Click, Type, Navigate, Select, etc.
        public string Locator { get; set; } = "";
        public string? Value { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public int Timestamp { get; set; }
    }
}
