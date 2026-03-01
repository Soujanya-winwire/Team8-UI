# ? Clear Old Configuration Script
# This script removes any saved configuration with hardcoded URLs

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ?? Clearing Old Configuration" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$configFile = "Configuration\frameworkConfig.json"
$configDir = "Configuration"

# Check if configuration file exists
if (Test-Path $configFile) {
    Write-Host "?? Found old configuration file: $configFile" -ForegroundColor Yellow
    Write-Host "   Deleting..." -ForegroundColor Gray
    
    Remove-Item $configFile -Force
    
    if (Test-Path $configFile) {
        Write-Host "? Failed to delete configuration file" -ForegroundColor Red
    } else {
        Write-Host "? Configuration file deleted successfully" -ForegroundColor Green
    }
} else {
    Write-Host "??  No saved configuration file found" -ForegroundColor Gray
    Write-Host "   (This is good - it means no old config exists)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "?? Instructions:" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Open your browser" -ForegroundColor White
Write-Host "2. Press F12 to open Developer Tools" -ForegroundColor White
Write-Host "3. Go to: Application ? Local Storage" -ForegroundColor White
Write-Host "4. Find 'agenticai-config' and delete it" -ForegroundColor White
Write-Host "5. Refresh the page (F5)" -ForegroundColor White
Write-Host ""
Write-Host "After these steps:" -ForegroundColor Cyan
Write-Host "  ? Base URL field will be empty" -ForegroundColor Green
Write-Host "  ? Placeholder will show: https://your-application-url.com" -ForegroundColor Green
Write-Host "  ? No hardcoded URLs anywhere" -ForegroundColor Green
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Would you like me to create a JavaScript snippet to clear localStorage?" -ForegroundColor Yellow
Write-Host "Copy and paste it in the browser console (F12)." -ForegroundColor Yellow
Write-Host ""

$snippet = @"
// ?? Clear AgenticAI Configuration from Browser Cache
// Copy and paste this in your browser console (F12 ? Console tab)

console.log('?? Clearing AgenticAI configuration from localStorage...');

// Remove the cached configuration
localStorage.removeItem('agenticai-config');

console.log('? Configuration cleared!');
console.log('?? Now refresh the page (F5) to see empty Base URL field');

// Auto-refresh option
if (confirm('Configuration cleared! Refresh page now?')) {
    location.reload();
}
"@

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "?? JavaScript Snippet (Copy & Paste in Browser Console):" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host $snippet -ForegroundColor White
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Save snippet to file
$snippetFile = "ClearConfigurationSnippet.js"
$snippet | Out-File -FilePath $snippetFile -Encoding UTF8
Write-Host "?? Snippet saved to: $snippetFile" -ForegroundColor Green
Write-Host ""

Write-Host "? Script completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Open Web UI in browser: http://localhost:5000" -ForegroundColor White
Write-Host "2. Press F12 ? Console tab" -ForegroundColor White
Write-Host "3. Paste the snippet from $snippetFile" -ForegroundColor White
Write-Host "4. Press Enter" -ForegroundColor White
Write-Host "5. Refresh the page" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
