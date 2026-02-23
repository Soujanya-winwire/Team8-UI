using AgenticAI.Core.Models;

namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Holds the result of a single data-driven test row execution
    /// </summary>
    public class DataDrivenResult
    {
        /// <summary>
        /// Zero-based index of the data row
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// The data values for this row (column name → value)
        /// </summary>
        public Dictionary<string, string> DataRow { get; set; } = new();

        /// <summary>
        /// The full test execution result for this row
        /// </summary>
        public TestCaseResult Result { get; set; } = new();
    }
}
