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
        
        /// <summary>
        /// If true, this scenario will be skipped during execution
        /// </summary>
        public bool IsSkipped { get; set; } = false;
        
        /// <summary>
        /// Reason for skipping (optional)
        /// </summary>
        public string? SkipReason { get; set; }
        
        /// <summary>
        /// Legacy: Kept for backward compatibility with existing test scenarios
        /// New scenarios should use Steps instead
        /// </summary>
        public List<RecordedAction> Actions { get; set; } = new();
        
        /// <summary>
        /// Legacy: Kept for backward compatibility with existing test scenarios
        /// New scenarios should use Steps instead
        /// </summary>
        public List<Assertion> Assertions { get; set; } = new();
        
        /// <summary>
        /// Unified list of executable steps (actions + assertions) in execution order
        /// This is the new recommended approach for inline assertion execution
        /// </summary>
        public List<TestStep> Steps { get; set; } = new();
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>
    /// Represents a single executable step in a test scenario
    /// Can be either an action or an assertion
    /// </summary>
    public class TestStep
    {
        /// <summary>
        /// Type of step: "Action" or "Assertion"
        /// </summary>
        public string StepType { get; set; } = "";
        
        /// <summary>
        /// Execution order (0-based index)
        /// </summary>
        public int Order { get; set; }
        
        /// <summary>
        /// Action data (populated if StepType = "Action")
        /// </summary>
        public RecordedAction? Action { get; set; }
        
        /// <summary>
        /// Assertion data (populated if StepType = "Assertion")
        /// </summary>
        public Assertion? Assertion { get; set; }
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
        
        /// <summary>
        /// Index of the action before which this assertion should be executed (precondition).
        /// Use this for validating element state before performing an action.
        /// </summary>
        public int? ExecuteBeforeActionIndex { get; set; }
        
        /// <summary>
        /// Index of the action after which this assertion should be executed (postcondition).
        /// Use this for validating results after an action completes.
        /// Kept for backward compatibility with legacy scenarios.
        /// </summary>
        public int? ExecuteAfterActionIndex { get; set; }
    }
}
