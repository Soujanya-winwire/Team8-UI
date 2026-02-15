# Automated Project Cleanup Script
# This script will clean up unused files and organize the project

param(
    [switch]$DryRun = $false,
    [switch]$NoBackup = $false
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFolder = "backup_cleanup_$timestamp"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Project Cleanup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Function to safely remove item
function Remove-ItemSafely {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        if ($DryRun) {
            Write-Host "[DRY RUN] Would remove: $Description" -ForegroundColor Yellow
            Write-Host "  Path: $Path" -ForegroundColor Gray
        } else {
            Write-Host "Removing: $Description" -ForegroundColor Green
            Remove-Item $Path -Force -Recurse -ErrorAction SilentlyContinue
            Write-Host "  ? Removed: $Path" -ForegroundColor Gray
        }
    } else {
        Write-Host "Skipped (not found): $Description" -ForegroundColor Gray
    }
}

# Function to move item to archive
function Move-ToArchive {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        $archivePath = "docs\archive"
        if (-not (Test-Path $archivePath)) {
            if (-not $DryRun) {
                New-Item -ItemType Directory -Path $archivePath -Force | Out-Null
            }
        }
        
        if ($DryRun) {
            Write-Host "[DRY RUN] Would archive: $Description" -ForegroundColor Yellow
            Write-Host "  From: $Path" -ForegroundColor Gray
            Write-Host "  To: $archivePath" -ForegroundColor Gray
        } else {
            Write-Host "Archiving: $Description" -ForegroundColor Green
            $fileName = Split-Path $Path -Leaf
            Move-Item $Path -Destination "$archivePath\$fileName" -Force
            Write-Host "  ? Archived: $fileName" -ForegroundColor Gray
        }
    }
}

# Step 1: Create Backup
if (-not $NoBackup -and -not $DryRun) {
    Write-Host "Step 1: Creating backup..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
    Get-ChildItem -Path . -Filter "*.md" -File | ForEach-Object {
        Copy-Item $_.FullName -Destination $backupFolder -Force
    }
    Write-Host "? Backup created: $backupFolder" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Step 1: Backup skipped (NoBackup flag or DryRun)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 2: Remove Empty Directories
Write-Host "Step 2: Removing empty directories..." -ForegroundColor Cyan
Remove-ItemSafely -Path "tools\SeleniumRecorder" -Description "Empty SeleniumRecorder directory"
Write-Host ""

# Step 3: Archive Duplicate Documentation
Write-Host "Step 3: Archiving duplicate documentation..." -ForegroundColor Cyan

# Create docs/archive folder
if (-not $DryRun -and -not (Test-Path "docs\archive")) {
    New-Item -ItemType Directory -Path "docs\archive" -Force | Out-Null
}

# Recording Fix Guides (keep latest only)
$recordingDocsToArchive = @(
    "RECORDING_ISSUES_FIXED.md",
    "RECORDING_FIXES_QUICK_REFERENCE.md",
    "FINAL_RECORDING_FIXES.md",
    "QUICK_FIX_3_ISSUES.md",
    "RECORDING_ERROR_QUICK_TEST.md"
)

foreach ($doc in $recordingDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old recording fix doc: $doc"
}

# UI Fix Guides (keep latest only)
$uiDocsToArchive = @(
    "UI_IMPROVEMENTS_SUMMARY.md",
    "VISUAL_CHANGES_QUICK_REFERENCE.md",
    "UI_LOADING_FIX.md",
    "QUICK_FIX_LOADING.md"
)

foreach ($doc in $uiDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old UI fix doc: $doc"
}

# Execute Button Fix
Move-ToArchive -Path "EXECUTE_BUTTON_QUICK_TEST.md" -Description "Execute button quick test (superseded)"

# Playwright Installation Guides
$playwrightDocsToArchive = @(
    "PLAYWRIGHT_QUICK_FIX_CARD.md",
    "YOUR_EXACT_PLAYWRIGHT_FIX.md",
    "START_HERE_PLAYWRIGHT_FIX.md",
    "ONEDRIVE_PATH_FIX.md",
    "YOUR_QUICK_FIX.md",
    "PLAYWRIGHT_ONEDRIVE_PATH_FIX.md"
)

foreach ($doc in $playwrightDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old Playwright doc: $doc"
}

# Enhancement Plans
$enhancementDocsToArchive = @(
    "FRAMEWORK_ENHANCEMENT_PLAN.md",
    "QUICK_ACTION_PLAN.md"
)

foreach ($doc in $enhancementDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old enhancement plan: $doc"
}

# Revert/Fix Cards
$revertDocsToArchive = @(
    "CLEAN_STATE_QUICK_START.md",
    "REVERTED_TO_WORKING_STATE.md",
    "REVERT_QUICK_CARD.md",
    "INSTANT_FIX_CARD.md"
)

foreach ($doc in $revertDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old revert doc: $doc"
}

# Navigation Fix
Move-ToArchive -Path "NAVIGATION_FIX.md" -Description "Old navigation fix (superseded by visual version)"

# Simplified Recording
Move-ToArchive -Path "SIMPLIFIED_RECORDING_GUIDE.md" -Description "Old simplified guide (superseded by visual)"

# Phase 1 Documentation
$phase1DocsToArchive = @(
    "PHASE1_STEP1.2_RECORDING_SIMPLIFICATION_COMPLETE.md",
    "QUICK_TEST_RECORDING_SIMPLIFIED.md",
    "STEP_1.2_COMPLETE_SUMMARY.md"
)

foreach ($doc in $phase1DocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old Phase 1 doc: $doc"
}

# Three Fixes
Move-ToArchive -Path "THREE_FIXES_QUICK_TEST.md" -Description "Three fixes quick test (superseded)"

# Final Two Fixes
Move-ToArchive -Path "FINAL_TWO_FIXES_QUICK_TEST.md" -Description "Final two fixes quick test (superseded)"

# Quick Start/Summary Files
$quickStartDocsToArchive = @(
    "QUICK_START_ENHANCED.md",
    "INSTANT_TEST_CARD_RECORDING.md"
)

foreach ($doc in $quickStartDocsToArchive) {
    Move-ToArchive -Path $doc -Description "Old quick start: $doc"
}

Write-Host ""

# Step 4: Remove Duplicate Scripts
Write-Host "Step 4: Removing duplicate PowerShell scripts..." -ForegroundColor Cyan
Remove-ItemSafely -Path "InstallPlaywright-Simple.ps1" -Description "Duplicate Playwright script (simple version)"
Remove-ItemSafely -Path "InstallPlaywright-OneDriveFix.ps1" -Description "Duplicate Playwright script (OneDrive fix)"
Write-Host ""

# Step 5: Add WebUI to Solution
Write-Host "Step 5: Adding AgenticAI.WebUI to solution..." -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "[DRY RUN] Would run: dotnet sln add tools\AgenticAI.WebUI\AgenticAI.WebUI.csproj" -ForegroundColor Yellow
} else {
    try {
        $output = dotnet sln add tools\AgenticAI.WebUI\AgenticAI.WebUI.csproj 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "? AgenticAI.WebUI added to solution" -ForegroundColor Green
        } else {
            Write-Host "Note: WebUI might already be in solution or error occurred" -ForegroundColor Yellow
            Write-Host "  Output: $output" -ForegroundColor Gray
        }
    } catch {
        Write-Host "Note: Could not add WebUI to solution - might already exist" -ForegroundColor Yellow
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Cleanup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN COMPLETE - No changes were made" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To perform actual cleanup, run:" -ForegroundColor Cyan
    Write-Host "  .\Cleanup-Project.ps1" -ForegroundColor White
    Write-Host ""
    Write-Host "To skip backup (not recommended):" -ForegroundColor Cyan
    Write-Host "  .\Cleanup-Project.ps1 -NoBackup" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "Cleanup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Changes made:" -ForegroundColor Cyan
    Write-Host "  ? Empty directories removed" -ForegroundColor Gray
    Write-Host "  ? Duplicate documentation archived" -ForegroundColor Gray
    Write-Host "  ? Duplicate scripts removed" -ForegroundColor Gray
    Write-Host "  ? WebUI added to solution" -ForegroundColor Gray
    
    if (-not $NoBackup) {
        Write-Host ""
        Write-Host "Backup location: $backupFolder" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Archived documentation location: docs\archive\" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review docs\archive\ folder" -ForegroundColor White
Write-Host "  2. Commit changes to git" -ForegroundColor White
Write-Host "  3. Review if VisualDesigner and ZeroCodeTool are still needed" -ForegroundColor White
Write-Host ""
