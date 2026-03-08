# Generate Excel test data files for data-driven testing
Write-Host "Generating Sample Excel Test Data Files..." -ForegroundColor Cyan

# Navigate to the tests directory
$testDir = Join-Path $PSScriptRoot "tests\AgenticAI.Tests"
Set-Location $testDir

# Run the test that generates Excel files
dotnet test --filter "Category=ExcelGeneration" --logger "console;verbosity=detailed"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Excel files generated successfully in TestData folder!" -ForegroundColor Green
    Write-Host "`nGenerated files:" -ForegroundColor Yellow
    Write-Host "  - TestData/SampleUsers.xlsx" -ForegroundColor White
    Write-Host "  - TestData/SampleProducts.xlsx" -ForegroundColor White
    Write-Host "`nYou can now use these files or create your own Excel files." -ForegroundColor Cyan
} else {
    Write-Host "`n❌ Failed to generate Excel files" -ForegroundColor Red
}
