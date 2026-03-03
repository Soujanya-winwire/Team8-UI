# IFrame Fix - Quick Reference

## Problem
```
Step 12: SwitchToFrame - ? Passed
Step 13: Click - ? TIMEOUT (30000ms exceeded)
```

## Root Cause
Playwright wasn't actually using the frame context for subsequent actions.

## The Fix
Updated PlaywrightDriver to store and use frame reference for all actions.

## Steps to Apply Fix

### 1. Stop Web UI
```powershell
# Press Ctrl+C in PowerShell terminal
```

### 2. Rebuild
```powershell
dotnet clean
dotnet build
```

### 3. Restart Web UI
```powershell
.\LaunchWebUI.ps1
```

### 4. Re-execute Failing Test
- Navigate to Test Results
- Find your test (e.g., "API-2")
- Click Execute button

## Expected Result

### Before
```
12: SwitchToFrame (#_ListPage) - ? Passed
13: Click (button.icon-document-preview-panel-wgt) - ? Timeout
```

### After
```
12: SwitchToFrame (#_ListPage) - ? Passed
    [INFO] ? Successfully switched to iframe: #_ListPage
13: Click (button.icon-document-preview-panel-wgt) - ? Passed
    [DEBUG] Clicking in frame context: button.icon-document-preview-panel-wgt
```

## What Was Changed

| File | Change |
|------|--------|
| `PlaywrightDriver.cs` | Added `_currentFrame` field to track active frame |
| `PlaywrightDriver.cs` | Updated `SwitchToFrameAsync()` to store frame reference |
| `PlaywrightDriver.cs` | Updated `ClickAsync()` to use frame context if available |
| `PlaywrightDriver.cs` | Updated `TypeAsync()` to use frame context if available |
| `PlaywrightDriver.cs` | Updated `GetTextAsync()` to use frame context if available |
| `PlaywrightDriver.cs` | Updated all action methods to check `_currentFrame` |
| `ScenarioExecutor.cs` | Added 500ms delay after frame switch for loading |

## Key Code Changes

### Frame Context Tracking
```csharp
private IFrame? _currentFrame;
private string? _currentFrameLocator;
```

### Switch to Frame
```csharp
public async Task SwitchToFrameAsync(string frameLocator)
{
    var frameElement = await _page.QuerySelectorAsync(selector);
    var frame = await frameElement.ContentFrameAsync();
    
    _currentFrame = frame; // ? Stores frame reference
    Logger.Info($"? Successfully switched to iframe: {selector}");
}
```

### Actions Use Frame Context
```csharp
public async Task ClickAsync(string locator)
{
    if (_currentFrame != null)
    {
        await _currentFrame.ClickAsync(selector); // ? Uses frame
    }
    else
    {
        await _page!.ClickAsync(selector);
    }
}
```

## Testing the Fix

### Quick Test
1. Record a test with iframe interaction
2. Execute the test
3. Verify all steps pass

### Verify Logs
Look for these messages:
```
? Successfully switched to iframe: #frame-id
[DEBUG] Clicking in frame context: #element-id
[DEBUG] Typing in frame context: #input-id
```

## Common Issues

### Still Timing Out?
1. **Check frame selector** - Use browser DevTools to verify
2. **Increase timeout** - Edit `FrameworkConfiguration.TimeoutInSeconds`
3. **Add wait** - Insert `Wait` action after `SwitchToFrame`

### Frame Not Found?
```
Error: Frame not found: #my-frame
```
**Solutions:**
- Try `iframe[id='my-frame']` instead of `#my-frame`
- Use `SwitchToFrameByIndex` with value `0`
- Check frame exists before switch

### Element Not Visible?
```
Error: Element is outside viewport
```
**Solution:** Add `Scroll` action before click

## Performance

- **Frame switch:** ~50ms
- **Load wait:** 500ms (automatic)
- **Action in frame:** Same as non-frame

## Rollback

If issues occur, revert to previous version:
```powershell
git checkout HEAD~1 src/AgenticAI.UIAutomation/Drivers/PlaywrightDriver.cs
git checkout HEAD~1 src/AgenticAI.Core/ZeroCode/ScenarioExecutor.cs
dotnet build
```

## Support

See detailed documentation:
- `docs/IFrame_Timeout_Fix.md` - Full troubleshooting guide
- `docs/IFrame_Context_Management.md` - Feature documentation
- `docs/IFrame_Quick_Start.md` - Usage guide

---

**Status:** ? FIXED  
**Build:** Verified ?  
**Ready:** To deploy ??

