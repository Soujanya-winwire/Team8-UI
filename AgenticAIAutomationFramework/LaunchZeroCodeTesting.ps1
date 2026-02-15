# Zero-Code Testing Quick Launch Script
# For SauceDemo Application (https://www.saucedemo.com)

Write-Host "??????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?   ?? Zero-Code Testing - Quick Launch Menu           ?" -ForegroundColor Cyan
Write-Host "?   Application: SauceDemo                              ?" -ForegroundColor Cyan
Write-Host "??????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

Write-Host "Choose an option:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. ?? Launch Visual Test Designer (Web UI - No Code!)" -ForegroundColor Green
Write-Host "2. ?? Record a New Test (Interactive Browser)" -ForegroundColor Green
Write-Host "3. ?? Create Test (Guided CLI)" -ForegroundColor Green
Write-Host "4. ??  Run Sample Login Test" -ForegroundColor Cyan
Write-Host "5. ?? Run All Smoke Tests" -ForegroundColor Cyan
Write-Host "6. ?? Run All Zero-Code Scenarios" -ForegroundColor Cyan
Write-Host "7. ?? List All Available Scenarios" -ForegroundColor White
Write-Host "8. ???  Build Framework" -ForegroundColor White
Write-Host "0. ? Exit" -ForegroundColor Red
Write-Host ""

$choice = Read-Host "Enter your choice (0-8)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "?? Launching Visual Test Designer..." -ForegroundColor Green
        dotnet run --project tools/VisualDesigner
    }
    "2" {
        Write-Host ""
        $scenarioName = Read-Host "Enter scenario name (e.g., My_Login_Test)"
        $module = Read-Host "Enter module name (e.g., Authentication)"
        
        Write-Host ""
        Write-Host "?? Starting test recording..." -ForegroundColor Green
        Write-Host "Browser will open. Perform your actions and press ENTER in console when done." -ForegroundColor Yellow
        
        dotnet run --project tools/TestRecorder -- --scenario "$scenarioName" --url "https://www.saucedemo.com" --module "$module"
    }
    "3" {
        Write-Host ""
        Write-Host "?? Starting guided test creation..." -ForegroundColor Green
        dotnet run --project tools/TestRecorder -- --mode guided --url "https://www.saucedemo.com"
    }
    "4" {
        Write-Host ""
        Write-Host "??  Running sample login test..." -ForegroundColor Cyan
        dotnet run --project tools/ZeroCodeTool -- run --scenario "Login_With_Valid_Credentials" --module "Authentication"
    }
    "5" {
        Write-Host ""
        Write-Host "?? Running all smoke tests..." -ForegroundColor Cyan
        dotnet run --project tools/ZeroCodeTool -- run --tag "smoke"
    }
    "6" {
        Write-Host ""
        Write-Host "?? Running all zero-code scenarios..." -ForegroundColor Cyan
        Write-Host "This may take a few minutes..." -ForegroundColor Yellow
        dotnet test --filter "FullyQualifiedName~Execute_All_ZeroCode_Scenarios"
    }
    "7" {
        Write-Host ""
        Write-Host "?? Available Test Scenarios:" -ForegroundColor White
        dotnet run --project tools/ZeroCodeTool -- list
    }
    "8" {
        Write-Host ""
        Write-Host "???  Building framework..." -ForegroundColor White
        dotnet build
    }
    "0" {
        Write-Host ""
        Write-Host "?? Goodbye!" -ForegroundColor Yellow
        exit
    }
    default {
        Write-Host ""
        Write-Host "? Invalid choice. Please run the script again." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
