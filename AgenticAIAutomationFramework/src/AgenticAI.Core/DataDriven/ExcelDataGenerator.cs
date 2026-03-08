using OfficeOpenXml;

namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Helper class to create sample Excel test data files
    /// This is used for testing and demonstration purposes
    /// </summary>
    public static class ExcelDataGenerator
    {
        /// <summary>
        /// Create a sample Excel file with test user data
        /// </summary>
        public static void CreateSampleUsersExcel(string filePath)
        {
            // Set EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("TestUsers");

            // Add headers
            worksheet.Cells[1, 1].Value = "username";
            worksheet.Cells[1, 2].Value = "password";
            worksheet.Cells[1, 3].Value = "email";
            worksheet.Cells[1, 4].Value = "role";
            worksheet.Cells[1, 5].Value = "expectedResult";

            // Format header row
            using (var range = worksheet.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            // Add sample data rows
            var data = new object[,]
            {
                { "admin@example.com", "Admin123!", "admin@example.com", "Admin", "success" },
                { "user1@example.com", "User123!", "user1@example.com", "User", "success" },
                { "testuser@example.com", "Test123!", "testuser@example.com", "Tester", "success" },
                { "invalid@example.com", "wrongpassword", "invalid@example.com", "None", "failure" },
                { "demo@example.com", "Demo123!", "demo@example.com", "Demo", "success" }
            };

            for (int row = 0; row < data.GetLength(0); row++)
            {
                for (int col = 0; col < data.GetLength(1); col++)
                {
                    worksheet.Cells[row + 2, col + 1].Value = data[row, col];
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Save the file
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            package.SaveAs(fileInfo);
        }

        /// <summary>
        /// Create a sample Excel file with product test data
        /// </summary>
        public static void CreateSampleProductsExcel(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Products");

            // Add headers
            worksheet.Cells[1, 1].Value = "productName";
            worksheet.Cells[1, 2].Value = "productCode";
            worksheet.Cells[1, 3].Value = "price";
            worksheet.Cells[1, 4].Value = "quantity";
            worksheet.Cells[1, 5].Value = "category";

            // Format header row
            using (var range = worksheet.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            }

            // Add sample data
            var data = new object[,]
            {
                { "Laptop", "PROD001", "999.99", "10", "Electronics" },
                { "Mouse", "PROD002", "29.99", "50", "Electronics" },
                { "Keyboard", "PROD003", "79.99", "30", "Electronics" },
                { "Monitor", "PROD004", "299.99", "15", "Electronics" },
                { "Desk Chair", "PROD005", "199.99", "8", "Furniture" }
            };

            for (int row = 0; row < data.GetLength(0); row++)
            {
                for (int col = 0; col < data.GetLength(1); col++)
                {
                    worksheet.Cells[row + 2, col + 1].Value = data[row, col];
                }
            }

            worksheet.Cells.AutoFitColumns();

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            package.SaveAs(fileInfo);
        }
    }
}
