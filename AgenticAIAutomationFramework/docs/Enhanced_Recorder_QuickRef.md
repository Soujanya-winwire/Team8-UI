# Enhanced Recorder Quick Reference

## 🎯 Quick Start

```csharp
// 1. Create scenario builder
var builder = new ScenarioBuilder();

// 2. Start recording
builder.StartRecording("https://example.com");

// 3. Get injection scripts
string scripts = builder.GetBrowserInjectionScripts();

// 4. Inject into browser (Playwright)
await page.AddInitScriptAsync(scripts);

// 5. Stop and build scenario
var scenario = builder.StopRecording("Test_Name", "Module");
```

## 📊 Locator Priority

| Priority | Strategy | Confidence | Example |
|----------|----------|------------|---------|
| 1 | data-testid | 95% | `[data-testid='login-btn']` |
| 2 | Stable ID | 90% | `#submitButton` |
| 3 | Name | 85% | `input[name='username'][type='text']` |
| 4 | aria-label | 80% | `button[aria-label='Submit']` |
| 5 | Role | 70% | `[role='button']` |
| 6 | Text | 65% | `button:has-text("Click Me")` |
| 7 | CSS | 55% | `button.primary-btn` |
| 8 | XPath | 40% | `//button[contains(@class,'btn')]` |

## 🎭 Event Types

| DOM Event | Automation Action | Description |
|-----------|------------------|-------------|
| `click` | Click / Check / Uncheck | Context-aware |
| `dblclick` | DoubleClick | Double-click action |
| `input` | Type | Text input (debounced 300ms) |
| `change` | Select / Check / Uncheck / UploadFile | Based on element type |
| `keydown` | PressEnter / PressEscape / PressTab | Special keys only |
| `submit` | Submit | Form submission |
| `scroll` | Scroll | Throttled 500ms |

## 🚫 Dynamic ID Patterns (Ignored)

```javascript
/\d{4,}/                    // button-12345678
/[a-f0-9]{8,}/              // elem-a3f4b2c1
/_\d+$/                     // item_123
/uuid|guid|timestamp/i      // btn-uuid-xyz
/^\d+$/                     // 123456
/temp|tmp|generated/i       // temp-element
```

## 🎨 Utility Classes (Filtered)

```javascript
ng-*        // Angular
mat-*       // Material UI
css-*       // CSS-in-JS
_*          // Styled-components
jsx-*       // JSX
sc-*        // Styled-components
p-*, m-*    // Tailwind utilities
w-*, h-*    // Width/height utilities
```

## 🔍 Element Analyzer Rules

```javascript
// Actionable tags
['a', 'button', 'input', 'select', 'textarea', 'option', 'label']

// Actionable roles
['button', 'link', 'menuitem', 'tab', 'checkbox', 'radio', 'switch']

// Additional checks
- Has onclick handler
- tabindex >= 0
- CSS cursor: pointer
```

## 📦 IFrame Handling

```javascript
// Automatic detection
isInIFrame() → true/false

// Selector generation
#myFrame                    // By ID
iframe[name="content"]      // By name
iframe:nth-of-type(2)       // By position

// Actions added automatically
SwitchToFrame               // When entering iframe
SwitchToDefaultContent      // When leaving iframe
```

## ⏱️ Smart Waits

Waits automatically added after:
- ✅ Navigate actions
- ✅ Click actions (that may trigger navigation)
- ❌ Type actions (no wait needed)

## 🎯 Optimization Rules

### Deduplication
- **Type actions**: Merge rapid input on same element
- **Scroll actions**: Keep only latest in rapid sequence
- **Duplicate actions**: Remove exact duplicates

### Smart Waits
- After Navigate: 1 second
- After Click: 1 second (if next action exists)

## 💡 Best Practices

### For Developers
```html
<!-- Add data-testid attributes -->
<button data-testid="submit-btn">Submit</button>

<!-- Use semantic HTML -->
<button role="button" aria-label="Close dialog">×</button>

<!-- Stable IDs (avoid dynamic) -->
<input id="username" />          ✅ Good
<input id="field_1234567890" />  ❌ Bad (timestamp)
```

### For Testers
1. **Start with clean state**: Navigate to starting URL
2. **Perform actions slowly**: Allow time for page updates
3. **Avoid rapid clicking**: Debouncing may merge actions
4. **Use visible elements**: Hidden elements won't be captured
5. **Check console**: Look for recorder logs

## 🐛 Debugging

### Enable Debug Logging
```javascript
// In browser console
window.__recorderDebug = true;
```

### Check Event Capture
```javascript
// Should see these logs:
[EventCapture] Click captured
[Recorder] Click: [data-testid='btn'] (data-testid, confidence: 95)
```

### Verify Locator Quality
```javascript
// High confidence (> 80): ✅ Excellent
// Medium confidence (60-80): ⚠️ Acceptable
// Low confidence (< 60): ❌ Consider improving selectors
```

## 🔧 Customization

### Adjust Debounce/Throttle
```javascript
// In EventCaptureLayer.cs GetInjectionScript()
const CONFIG = {
    DEBOUNCE_INPUT: 300,      // Change to 500ms
    THROTTLE_SCROLL: 500,     // Change to 1000ms
    HIGHLIGHT_DURATION: 2000  // Change to 3000ms
};
```

### Add Custom Event
```csharp
// In EventCaptureLayer.cs
_supportedEvents.Add("contextmenu"); // Right-click

// Add handler in GetInjectionScript()
document.addEventListener('contextmenu', function(event) {
    // Capture right-click
}, true);
```

### Add Custom Locator Strategy
```csharp
// In SmartLocatorGenerator.cs
private bool TryGetCustomLocator(CapturedEvent evt, out LocatorResult result)
{
    // Your custom logic
    if (evt.Target.Attributes.TryGetValue("my-custom-attr", out var value))
    {
        result = new LocatorResult
        {
            Locator = $"[my-custom-attr='{value}']",
            Strategy = "custom",
            Confidence = 88
        };
        return true;
    }
    result = null!;
    return false;
}
```

## 📚 Additional Resources

- **[Enhanced_Recorder_Architecture.md](Enhanced_Recorder_Architecture.md)** - Full architecture documentation
- **[EventCaptureLayer.cs](../src/AgenticAI.Core/ZeroCode/Recorder/EventCaptureLayer.cs)** - Event capture implementation
- **[SmartLocatorGenerator.cs](../src/AgenticAI.Core/ZeroCode/Recorder/SmartLocatorGenerator.cs)** - Locator generation
- **[ActionNormalizer.cs](../src/AgenticAI.Core/ZeroCode/Recorder/ActionNormalizer.cs)** - Action normalization
- **[ScenarioBuilder.cs](../src/AgenticAI.Core/ZeroCode/Recorder/ScenarioBuilder.cs)** - Orchestration

## 🚀 Performance Tips

1. **Use data-testid**: Fastest and most reliable (95% confidence)
2. **Avoid deep nesting**: Keeps locators simple
3. **Stable class names**: Avoid auto-generated classes
4. **Semantic HTML**: Use proper roles and labels
5. **Unique IDs**: Non-dynamic IDs are second-best option

---

**Quick Help**: If elements aren't being captured, check browser console for `[Recorder]` logs.
