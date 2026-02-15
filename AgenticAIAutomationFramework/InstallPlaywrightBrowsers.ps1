#!/usr/bin/env pwsh
# Automated Playwright Browser Installation Script
# This script will automatically install Playwright browsers for the Agentic AI Framework

Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "   ?? Playwright Browser Installer" -ForegroundColor Yellow
Write-Host "   Automated installation for Agentic AI Framework" -ForegroundColor Green
Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"
$projectRoot = $PSScriptRoot

Write-Host "?? Project Root: $projectRoot" -ForegroundColor Gray
Write-Host ""

# Step 1: Check if .NET is installed
Write-Host "Step 1: Checking .NET installation..." -ForegroundColor Cyan
try {
    $dotnetVersion = dotnet --version
    Write-Host "? .NET installed: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "? .NET is not installed. Please install .NET 9 SDK first." -ForegroundColor Red
    Write-Host "   Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
Write-Host ""

# Step 2: Build the UIAutomation project
Write-Host "Step 2: Building UIAutomation project..." -ForegroundColor Cyan
$uiAutomationProject = Join-Path $projectRoot "src" | Join-Path -ChildPath "AgenticAI.UIAutomation" | Join-Path -ChildPath "AgenticAI.UIAutomation.csproj"

if (Test-Path $uiAutomationProject) {
    Write-Host "   Building: $uiAutomationProject" -ForegroundColor Gray
    
    # Change to project directory to avoid path issues
    Push-Location (Split-Path $uiAutomationProject -Parent)
    
    try {
        Write-Host "   Running: dotnet build --configuration Debug" -ForegroundColor DarkGray
        dotnet build --configuration Debug --verbosity minimal
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "? UIAutomation project built successfully" -ForegroundColor Green
        } else {
            Write-Host "??  Build had some issues but continuing..." -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "? UIAutomation project not found at: $uiAutomationProject" -ForegroundColor Red
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
Write-Host ""

# Step 3: Install Playwright CLI tool globally
Write-Host "Step 3: Installing Playwright CLI tool..." -ForegroundColor Cyan
try {
    # Check if already installed
    $existingTool = dotnet tool list --global | Select-String "microsoft.playwright.cli"
    
    if ($existingTool) {
        Write-Host "   Playwright CLI already installed, updating..." -ForegroundColor Yellow
        dotnet tool update --global Microsoft.Playwright.CLI 2>&1 | Out-Null
    } else {
        Write-Host "   Installing Playwright CLI..." -ForegroundColor Gray
        dotnet tool install --global Microsoft.Playwright.CLI 2>&1 | Out-Null
    }
    
    Write-Host "? Playwright CLI tool installed" -ForegroundColor Green
} catch {
    Write-Host "??  Note: Could not install global tool, will use project-local installation" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Install Playwright browsers
Write-Host "Step 4: Installing Playwright browsers..." -ForegroundColor Cyan
Write-Host "   This will download Chromium, Firefox, and WebKit browsers" -ForegroundColor Gray
Write-Host "   Download size: ~300 MB (one-time download)" -ForegroundColor Gray
Write-Host ""

$installSuccess = $false

# Method 1: Try using project-local playwright script
$playwrightScriptPath = Join-Path $projectRoot "src" | Join-Path -ChildPath "AgenticAI.UIAutomation" | Join-Path -ChildPath "bin" | Join-Path -ChildPath "Debug" | Join-Path -ChildPath "net9.0" | Join-Path -ChildPath "playwright.ps1"

if (Test-Path $playwrightScriptPath) {
    Write-Host "   Method 1: Using project-local Playwright installation..." -ForegroundColor Gray
    Write-Host "   Script: $playwrightScriptPath" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        # Change to the directory containing the script
        $playwrightDir = Split-Path $playwrightScriptPath -Parent
        Push-Location $playwrightDir
        
        try {
            # Run the script from its directory
            & pwsh -File ".\playwright.ps1" install
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "? Playwright browsers installed successfully!" -ForegroundColor Green
                $installSuccess = $true
            } else {
                Write-Host ""
                Write-Host "??  Method 1 failed (exit code: $LASTEXITCODE), trying Method 2..." -ForegroundColor Yellow
            }
        } finally {
            Pop-Location
        }
    } catch {
        Write-Host ""
        Write-Host "??  Method 1 failed: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "   Trying Method 2..." -ForegroundColor Gray
    }
} else {
    Write-Host "??  Project-local script not found at: $playwrightScriptPath" -ForegroundColor Yellow
    Write-Host "   (This is normal if the build didn't complete)" -ForegroundColor Gray
}

# Method 2: Try using global playwright command
if (-not $installSuccess) {
    Write-Host ""
    Write-Host "   Method 2: Using global Playwright CLI..." -ForegroundColor Gray
    
    try {
        $playwrightExe = Get-Command playwright -ErrorAction SilentlyContinue
        
        if ($playwrightExe) {
            Write-Host "   Found global playwright at: $($playwrightExe.Source)" -ForegroundColor DarkGray
            playwright install
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "? Playwright browsers installed successfully (global)!" -ForegroundColor Green
                $installSuccess = $true
            } else {
                Write-Host ""
                Write-Host "??  Method 2 failed (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   Global playwright command not found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host ""
        Write-Host "??  Method 2 failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Method 3: Direct installation via dotnet tool run
if (-not $installSuccess) {
    Write-Host ""
    Write-Host "   Method 3: Using dotnet tool run..." -ForegroundColor Gray
    
    try {
        dotnet tool run playwright install
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "? Playwright browsers installed successfully!" -ForegroundColor Green
            $installSuccess = $true
        } else {
            Write-Host ""
            Write-Host "??  Method 3 failed (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host ""
        Write-Host "??  Method 3 failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Method 4: Manual installation using PowerShell script directory navigation
if (-not $installSuccess) {
    Write-Host ""
    Write-Host "   Method 4: Manual PowerShell script execution..." -ForegroundColor Gray
    
    $binPath = Join-Path $projectRoot "src" | Join-Path -ChildPath "AgenticAI.UIAutomation" | Join-Path -ChildPath "bin" | Join-Path -ChildPath "Debug" | Join-Path -ChildPath "net9.0"
    
    if (Test-Path $binPath) {
        try {
            Push-Location $binPath
            
            if (Test-Path ".\playwright.ps1") {
                Write-Host "   Found playwright.ps1 in build directory" -ForegroundColor Gray
                Write-Host "   Executing: .\playwright.ps1 install" -ForegroundColor DarkGray
                
                .\playwright.ps1 install
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host ""
                    Write-Host "? Playwright browsers installed successfully!" -ForegroundColor Green
                    $installSuccess = $true
                } else {
                    Write-Host ""
                    Write-Host "??  Method 4 failed (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
                }
            }
            
            Pop-Location
        } catch {
            Write-Host ""
            Write-Host "??  Method 4 failed: $($_.Exception.Message)" -ForegroundColor Yellow
            Pop-Location
        }
    }
}

if (-not $installSuccess) {
    Write-Host ""
    Write-Host "? Failed to install browsers using all automated methods" -ForegroundColor Red
    Write-Host ""
    Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Yellow
    Write-Host "  Manual Installation Steps:" -ForegroundColor Yellow
    Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1: Install Playwright CLI globally and run install" -ForegroundColor White
    Write-Host "  1. Open PowerShell as Administrator" -ForegroundColor Gray
    Write-Host "  2. Run: dotnet tool install --global Microsoft.Playwright.CLI" -ForegroundColor Cyan
    Write-Host "  3. Close and reopen PowerShell" -ForegroundColor Gray
    Write-Host "  4. Run: playwright install" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Option 2: Navigate to build directory manually" -ForegroundColor White
    Write-Host "  1. Open PowerShell" -ForegroundColor Gray
    $buildDir = Join-Path $projectRoot "src\AgenticAI.UIAutomation\bin\Debug\net9.0"
    Write-Host "  2. Run: cd `"$buildDir`"" -ForegroundColor Cyan
    Write-Host "  3. Run: .\playwright.ps1 install" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Option 3: Use npm (if you have Node.js)" -ForegroundColor White
    Write-Host "  1. Run: npm install -g playwright" -ForegroundColor Cyan
    Write-Host "  2. Run: playwright install" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
Write-Host ""

# Step 5: Verify installation
Write-Host "Step 5: Verifying installation..." -ForegroundColor Cyan
try {
    $playwrightCmd = Get-Command playwright -ErrorAction SilentlyContinue
    if ($playwrightCmd) {
        $playwrightVersion = playwright --version 2>&1
        Write-Host "? Playwright version: $playwrightVersion" -ForegroundColor Green
    } else {
        Write-Host "??  Playwright command not available globally, but browsers may be installed" -ForegroundColor Yellow
    }
} catch {
    Write-Host "??  Could not verify version, but browsers may be installed" -ForegroundColor Yellow
}
Write-Host ""

# Step 6: Check browser installation
Write-Host "Step 6: Checking installed browsers..." -ForegroundColor Cyan
$browsersPath = Join-Path $env:USERPROFILE "AppData\Local\ms-playwright"

if (Test-Path $browsersPath) {
    $browsers = Get-ChildItem $browsersPath -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
    Write-Host "? Browsers installed at: $browsersPath" -ForegroundColor Green
    Write-Host "   Browsers found:" -ForegroundColor Gray
    if ($browsers) {
        foreach ($browser in $browsers) {
            Write-Host "   - $browser" -ForegroundColor White
        }
    } else {
        Write-Host "   (Scanning subdirectories...)" -ForegroundColor Gray
    }
} else {
    Write-Host "??  Browser directory not found at expected location" -ForegroundColor Yellow
    Write-Host "   Expected: $browsersPath" -ForegroundColor Gray
    Write-Host "   This is OK if browsers were installed to a different location" -ForegroundColor Gray
}
Write-Host ""

# Success!
Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "   ? Installation Complete!" -ForegroundColor Green
Write-Host "????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host "What's next?" -ForegroundColor Yellow
Write-Host "  1. Start the Web UI: .\LaunchWebUI.ps1" -ForegroundColor White
Write-Host "  2. Go to 'Record Test' page" -ForegroundColor White
Write-Host "  3. Try 'Start Recording' - browser should open now!" -ForegroundColor White
Write-Host "  4. Or run: playwright codegen https://www.saucedemo.com" -ForegroundColor White
Write-Host ""
Write-Host "?? Tip: If recording doesn't work, try running:" -ForegroundColor Yellow
Write-Host "   playwright install" -ForegroundColor Cyan
Write-Host "   from PowerShell (as Administrator)" -ForegroundColor Gray
Write-Host ""
Write-Host "Happy Testing! ??" -ForegroundColor Green
Write-Host ""

# Pause so user can see the results
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
