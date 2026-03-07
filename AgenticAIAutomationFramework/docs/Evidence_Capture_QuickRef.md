# Evidence Capture - Quick Reference

## ?? What Changed

Enhanced test execution to capture screenshots at critical points:
1. **Last Step** - Always capture (shows final state)
2. **Failed Steps** - Always capture (shows failure point)
3. **No Duplicates** - If last step fails, only capture once

## ?? Screenshot Behavior

### Before
```
? Step 1: Navigate  ? Screenshot
? Step 2: Click     ? Screenshot  
? Step 3: Type      ? Screenshot
? Step 4: Verify    ? Screenshot
Total: 4 screenshots (ALL steps)
```

### After
```
? Step 1: Navigate  ? No screenshot
? Step 2: Click     ? No screenshot
? Step 3: Type      ? No screenshot
? Step 4: Verify    ? Screenshot (LAST STEP)
Total: 1 screenshot
```

### With Failure
```
? Step 1: Navigate  ? No screenshot
? Step 2: Click     ? Screenshot (FAILED)
Test stops
Total: 1 screenshot
```

### Last Step Fails
```
? Step 1: Navigate  ? No screenshot
? Step 2: Click     ? No screenshot
? Step 3: Verify    ? Screenshot (FAILED + LAST)
Total: 1 screenshot (not 2)
```

## ?? How to Verify

### 1. Execute a Test
```
Navigate to: Test Results ? Execute any test
```

### 2. Check Evidence Tab
```
Click test row ? Evidence tab ? See screenshots
```

### 3. Expected Results

**All Pass:**
- ? Last step has screenshot
- ? Other steps have no screenshots

**Mid-Test Fail:**
- ? Failed step has screenshot
- ? Test stops at failure

**Last Step Fail:**
- ? Last step has ONE screenshot
- ? Filename contains `_FAILED_LAST`

## ?? Screenshot Files

### Naming Convention
```
{TestName}_{StepType}{_FAILED}{_LAST}_{Timestamp}.png
```

### Examples

**Passed last step:**
```
Login_Test_Verify_LAST_2024-01-15_14-30-45-123.png
```

**Failed step:**
```
Checkout_Test_Click_FAILED_2024-01-15_14-30-45-456.png
```

**Failed last step:**
```
Payment_Test_Verify_FAILED_LAST_2024-01-15_14-30-45-789.png
```

## ?? Storage Impact

| Scenario | Before | After | Savings |
|----------|--------|-------|---------|
| 10 steps, all pass | 10 screenshots | 1 screenshot | 90% |
| 10 steps, fail at step 5 | 5 screenshots | 1 screenshot | 80% |
| 10 steps, last fails | 10 screenshots | 1 screenshot | 90% |

**Average:** 85-90% reduction in screenshot count

## ?? Deployment

### 1. Rebuild
```powershell
dotnet clean
dotnet build
```

### 2. Restart
```powershell
.\LaunchWebUI.ps1
```

### 3. Test
```
Execute any test ? Check Evidence tab
```

## ?? Log Messages

### Look For
```
[INFO] Screenshot saved (last step): path/to/screenshot.png
[INFO] Screenshot saved (step failed): path/to/screenshot.png
```

### Not This
```
[INFO] Screenshot saved: path/to/screenshot.png  (no reason)
```

## ? Checklist

After deployment, verify:

- [ ] Last step of passing test has screenshot
- [ ] Failed steps have screenshots
- [ ] No duplicate screenshots on last step failure
- [ ] Screenshots appear in Evidence tab
- [ ] Filename contains correct suffixes
- [ ] UI layout unchanged
- [ ] Other tabs (Logs, etc.) work normally

## ?? Troubleshooting

### No Screenshots Captured

**Check:**
```csharp
// In FrameworkConfiguration
EnableScreenshots = true;  // Must be true
```

### Too Many Screenshots

**Cause:** Running old version

**Solution:** Rebuild and restart

### Screenshots Not in Evidence Tab

**Cause:** UI not refreshed

**Solution:** Hard refresh browser (Ctrl+Shift+R)

## ?? Support

See detailed documentation:
- `docs/Evidence_Capture_Enhancement.md` - Full implementation details

---

## ?? TL;DR

**What:** Enhanced screenshot capture  
**When:** Last step + Failed steps  
**Why:** Better evidence, less storage  
**How:** Rebuild + Restart  
**Result:** 90% fewer screenshots, same evidence quality

? **No UI changes**  
? **Backward compatible**  
? **Ready to deploy**
