namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Reads test data from various file formats (CSV, JSON, Excel) in the TestData folder
    /// Returns data as List&lt;Dictionary&lt;string,string&gt;&gt; for data-driven testing
    /// </summary>
    public static class TestDataReader
    {
        private static string? _testDataPath;

        /// <summary>
        /// Gets or sets the path to the TestData folder
        /// Defaults to "TestData" relative to the solution root
        /// </summary>
        public static string TestDataPath
        {
            get
            {
                if (string.IsNullOrEmpty(_testDataPath))
                {
                    _testDataPath = FindTestDataFolder();
                }
                return _testDataPath;
            }
            set => _testDataPath = value;
        }

        /// <summary>
        /// Read test data from a file in the TestData folder
        /// Automatically detects format based on file extension (.csv, .json, .xlsx, .xls)
        /// </summary>
        /// <param name="fileName">Name of the file (with or without extension)</param>
        /// <returns>DataTestSet containing rows and columns from the file</returns>
        public static DataTestSet ReadFromFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            // Try to find the file in TestData folder
            string? filePath = FindTestDataFile(fileName);

            if (filePath == null || !File.Exists(filePath))
            {
                throw new FileNotFoundException($"Test data file '{fileName}' not found in TestData folder: {TestDataPath}");
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".csv" => ReadFromCsvFile(filePath),
                ".json" => ReadFromJsonFile(filePath),
                ".xlsx" or ".xls" => ReadFromExcelFile(filePath),
                _ => throw new NotSupportedException($"File format '{extension}' is not supported. Use .csv, .json, .xlsx, or .xls")
            };
        }

        /// <summary>
        /// Read test data from a CSV file
        /// </summary>
        public static DataTestSet ReadFromCsvFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV file not found: {filePath}");

            string content = File.ReadAllText(filePath);
            return DataSetReader.ParseCsv(content);
        }

        /// <summary>
        /// Read test data from a JSON file
        /// </summary>
        public static DataTestSet ReadFromJsonFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");

            string content = File.ReadAllText(filePath);
            return DataSetReader.ParseJson(content);
        }

        /// <summary>
        /// Read test data from an Excel file (.xlsx or .xls)
        /// Reads the first worksheet by default
        /// </summary>
        public static DataTestSet ReadFromExcelFile(string filePath, string? worksheetName = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            return DataSetReader.ParseExcel(filePath, worksheetName);
        }

        /// <summary>
        /// Returns all available test data files in the TestData folder
        /// </summary>
        public static List<string> GetAvailableTestDataFiles()
        {
            if (!Directory.Exists(TestDataPath))
                return new List<string>();

            var files = Directory.GetFiles(TestDataPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetFileName(f))
                .ToList();

            return files;
        }

        /// <summary>
        /// Find a test data file by name (with or without extension)
        /// Searches in TestData folder and subfolders
        /// </summary>
        private static string? FindTestDataFile(string fileName)
        {
            if (!Directory.Exists(TestDataPath))
                return null;

            // If file already has an extension, search for exact match
            if (Path.HasExtension(fileName))
            {
                var exactPath = Path.Combine(TestDataPath, fileName);
                if (File.Exists(exactPath))
                    return exactPath;

                // Search in subfolders
                var files = Directory.GetFiles(TestDataPath, fileName, SearchOption.AllDirectories);
                return files.FirstOrDefault();
            }

            // Try with different extensions
            string[] extensions = { ".csv", ".json", ".xlsx", ".xls" };
            foreach (var ext in extensions)
            {
                var filePath = Path.Combine(TestDataPath, fileName + ext);
                if (File.Exists(filePath))
                    return filePath;

                // Search in subfolders
                var files = Directory.GetFiles(TestDataPath, fileName + ext, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }

            return null;
        }

        /// <summary>
        /// Find the TestData folder by searching from current directory up to solution root
        /// </summary>
        private static string FindTestDataFolder()
        {
            string currentPath = Directory.GetCurrentDirectory();
            
            // Try current directory first
            var testDataPath = Path.Combine(currentPath, "TestData");
            if (Directory.Exists(testDataPath))
                return testDataPath;

            // Search upwards for solution root
            string? solutionRoot = FindSolutionRoot(currentPath);
            if (solutionRoot != null)
            {
                testDataPath = Path.Combine(solutionRoot, "TestData");
                if (Directory.Exists(testDataPath))
                    return testDataPath;
            }

            // Default to current directory + TestData
            return Path.Combine(currentPath, "TestData");
        }

        private static string? FindSolutionRoot(string currentPath)
        {
            var directory = new DirectoryInfo(currentPath);
            while (directory != null)
            {
                if (directory.GetFiles("*.sln").Length > 0)
                    return directory.FullName;

                directory = directory.Parent;
            }
            return null;
        }
    }
}
