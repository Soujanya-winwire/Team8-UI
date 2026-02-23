using System.Text.Json;

namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Represents a set of test data rows to be used in data-driven testing
    /// </summary>
    public class DataTestSet
    {
        /// <summary>
        /// Column headers in the order they appear in the data source
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// Each row is a dictionary of column name → value
        /// </summary>
        public List<Dictionary<string, string>> Rows { get; set; } = new();

        /// <summary>
        /// Total number of data rows
        /// </summary>
        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// Parses CSV or JSON strings into a DataTestSet
    /// </summary>
    public static class DataSetReader
    {
        /// <summary>
        /// Parse a CSV string (first row is headers, remaining rows are data)
        /// </summary>
        public static DataTestSet ParseCsv(string csvContent)
        {
            if (string.IsNullOrWhiteSpace(csvContent))
                throw new ArgumentException("CSV content cannot be empty.", nameof(csvContent));

            var result = new DataTestSet();
            var lines = csvContent
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                throw new InvalidDataException("CSV content has no data rows.");

            // Parse headers
            var headers = ParseCsvLine(lines[0]);
            result.Columns = headers;

            // Parse data rows
            for (int i = 1; i < lines.Count; i++)
            {
                var values = ParseCsvLine(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int col = 0; col < headers.Count; col++)
                {
                    row[headers[col]] = col < values.Count ? values[col] : string.Empty;
                }

                result.Rows.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Parse a JSON array of objects into a DataTestSet.
        /// Each object's keys become columns; all unique keys across all objects are collected.
        /// </summary>
        public static DataTestSet ParseJson(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
                throw new ArgumentException("JSON content cannot be empty.", nameof(jsonContent));

            var result = new DataTestSet();

            var docs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent)
                       ?? throw new InvalidDataException("JSON content must be a non-null array of objects.");

            // Collect all unique column names (preserving first-seen order)
            var columnSet = new List<string>();
            foreach (var doc in docs)
            {
                foreach (var key in doc.Keys)
                {
                    if (!columnSet.Contains(key, StringComparer.OrdinalIgnoreCase))
                        columnSet.Add(key);
                }
            }
            result.Columns = columnSet;

            // Build rows
            foreach (var doc in docs)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in columnSet)
                {
                    if (doc.TryGetValue(col, out var element))
                    {
                        row[col] = element.ValueKind == JsonValueKind.String
                            ? element.GetString() ?? string.Empty
                            : element.ToString();
                    }
                    else
                    {
                        row[col] = string.Empty;
                    }
                }
                result.Rows.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Substitute ${ColumnName} placeholders in a string with values from the given row
        /// </summary>
        public static string SubstitutePlaceholders(string template, Dictionary<string, string> row)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            foreach (var kvp in row)
            {
                template = template.Replace($"${{{kvp.Key}}}", kvp.Value,
                    StringComparison.OrdinalIgnoreCase);
            }
            return template;
        }

        // Parse a single CSV line, handling quoted fields
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote inside quoted field
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString().Trim());
            return fields;
        }
    }
}
