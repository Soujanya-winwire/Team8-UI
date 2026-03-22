# Quick test script for data-driven execution
cd "c:\Users\SoujanyaMaligireddy\Team8---UI-Automation\AgenticAIAutomationFramework"

Write-Host "Testing Data-Driven Execution..." -ForegroundColor Cyan

# Run the scenario with CSV
dotnet run --project tests\AgenticAI.Tests -- `
    --scenario Test_05 `
    --module Datadriven `
    --dataset TestData\TestParameterMapping.csv

Write-Host "`nExecution complete!" -ForegroundColor Green
