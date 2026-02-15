# Script to clean Git repository from Visual Studio temp files
# Run this to remove .vs folder and other temp files from Git

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Git Repository Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Stop tracking .vs folder and other temp files
Write-Host "Removing Visual Studio temp files from Git..." -ForegroundColor Yellow

# Remove .vs folder from Git (but keep it locally)
git rm -r --cached .vs/ 2>$null
Write-Host "? Removed .vs/ folder" -ForegroundColor Green

# Remove copilot-chat folder
git rm -r --cached copilot-chat/ 2>$null
Write-Host "? Removed copilot-chat/ folder" -ForegroundColor Green

# Remove FileContentIndex files
git rm --cached .vs/AgenticAIAutomationFramework/FileContentIndex/*.vsidx 2>$null
Write-Host "? Removed FileContentIndex files" -ForegroundColor Green

# Remove bin/obj folders
Get-ChildItem -Path . -Directory -Recurse -Include bin,obj | ForEach-Object {
    $relativePath = $_.FullName.Substring((Get-Location).Path.Length + 1)
    git rm -r --cached $relativePath 2>$null
}
Write-Host "? Removed bin/obj folders" -ForegroundColor Green

Write-Host ""
Write-Host "Repository cleaned!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. git add .gitignore" -ForegroundColor White
Write-Host "  2. git commit -m 'Remove temp files and add .gitignore'" -ForegroundColor White
Write-Host "  3. Contact repository owner for push access" -ForegroundColor White
Write-Host ""
