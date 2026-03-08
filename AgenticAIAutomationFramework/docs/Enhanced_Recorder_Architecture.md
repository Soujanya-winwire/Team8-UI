# Enhanced Automation Recorder Architecture

## Overview

The automation recorder has been refactored with a modular architecture designed to improve accuracy, stability, and maintainability of recorded test scenarios.

## Architecture Components

### 1. **Event Capture Layer** (`EventCaptureLayer.cs`)
Captures browser events in recording mode with comprehensive event listening.

**Supported Events:**
- ✅ `click` - Click interactions
- ✅ `dblclick` - Double-click interactions  
- ✅ `input` - Text input (debounced)
- ✅ `change` - Select dropdowns, checkboxes, radio buttons
- ✅ `keydown` - Special keys (Enter, Escape, Tab, F-keys)
- ✅ `submit` - Form submissions
- ✅ `scroll` - Page scrolling (throttled)

**Features:**
- Debouncing for rapid input events (300ms)
- Throttling for scroll events (500ms)
- Captures element context and metadata
- IFrame detection and context tracking

### 2. **Element Analyzer** (`ElementAnalyzer.cs`)
Resolves the actionable parent element when a child element is clicked.

**Key Functions:**
- **`findActionableElement()`** - Traverses up to 5 levels to find the best actionable element
- **`isActionable()`** - Determines if an element is interactive
- Detects clickable elements based on:
  - Native actionable tags (`button`, `a`, `input`, etc.)
  - Click handlers (`onclick`)
  - ARIA roles (`button`, `link`, `menuitem`, etc.)
  - Tab index (focusable elements)
  - CSS cursor style (`pointer`)

**Benefits:**
- Prevents recording clicks on nested span/div elements inside buttons
- Ensures stable selectors by targeting the actual interactive element
- Improves test reliability

### 3. **Smart Locator Generator** (`SmartLocatorGenerator.cs`)
Generates stable and maintainable element locators using a priority-based strategy.

**Locator Priority (Highest to Lowest):**
1. ⭐ **data-testid** (95% confidence) - `data-testid`, `data-test-id`, `data-test`, `data-qa`, `data-cy`
2. ⭐ **Stable ID** (90% confidence) - Non-dynamic `id` attributes
3. ⭐ **Name** (85% confidence) - `name` attribute + type
4. ⭐ **aria-label** (80% confidence) - Accessibility labels
5. ⭐ **Role** (70% confidence) - ARIA role attributes
6. ⭐ **Text Content** (65% confidence) - Visible text for buttons/links (< 50 chars)
7. ⭐ **CSS Selector** (55% confidence) - Tag + stable classes
8. ⭐ **XPath** (40% confidence) - Fallback locator

**Dynamic ID Detection:**
Automatically detects and ignores dynamic IDs:
- Long digit sequences (4+ digits) - `button-12345678`
- Long hex strings (8+ chars) - `elem-a3f4b2c1d5e6`
- Underscore + number suffix - `item_123`
- UUIDs - `550e8400-e29b-41d4-a716-446655440000`
- Keywords - `timestamp`, `random`, `uuid`, `guid`, `temp`, `tmp`, `generated`
- Pure numeric IDs - `123456`

**Utility Class Filtering:**
Filters out framework and utility classes:
- Angular: `ng-*`
- Material UI: `mat-*`, `MuiBox*`
- CSS-in-JS: `css-*`, `emotion-*`, `jsx-*`, `sc-*`
- Styled-components: `_*`
- Tailwind utilities: `p-1`, `m-2`, `w-full`, etc.

### 4. **Action Normalizer** (`ActionNormalizer.cs`)
Normalizes DOM events into standard automation keywords.

**Event → Action Mapping:**
- `click` → `Click`, `Check`, `Uncheck` (context-aware)
- `dblclick` → `DoubleClick`
- `input` → `Type`
- `change` → `Select`, `Check`, `Uncheck`, `UploadFile`
- `keydown` → `PressEnter`, `PressEscape`, `PressTab`, etc.
- `submit` → `Submit`
- `scroll` → `Scroll`

**Features:**
- Context-aware action detection
- Automatic deduplication of rapid events
- Action merging (updates Type value instead of creating duplicates)
- Confidence scoring for each action

### 5. **Scenario Builder** (`ScenarioBuilder.cs`)
Orchestrates all components to build complete test scenarios.

**Responsibilities:**
- Coordinates Event Capture → Element Analysis → Locator Generation → Action Normalization
- Handles iframe context switching automatically
- Detects navigation (URL changes) and adds Navigate actions
- Adds smart waits between critical actions
- Optimizes final scenario (removes redundant steps)
- Manages recording state

**Features:**
- Element highlighting during recording
- Navigation detection for SPAs
- Automatic iframe context management
- Smart wait insertion after navigation/clicks
- Action optimization and deduplication

## Key Features

### 🎯 Improved Accuracy
- Finds actionable parent elements instead of recording decorative children
- Priority-based locator strategy ensures stable selectors
- Dynamic ID detection prevents fragile tests

### 🔍 Smart Locator Selection
- Prefers semantic attributes (`data-testid`, `aria-label`)
- Detects and avoids dynamic IDs and utility classes
- Generates XPath as fallback only

### 📦 IFrame Support
- Detects iframe context automatically
- Adds `SwitchToFrame` actions when needed
- Tracks current frame context

### 🎨 Shadow DOM Traversal
- Detects elements in Shadow DOM
- Generates appropriate locators for shadow elements

### ✨ Element Highlighting
- Highlights elements as they're recorded
- Visual feedback with red border (2-second duration)
- Helps verify correct element selection

### 🔄 Navigation Detection
- Detects URL changes (including SPA navigation)
- Automatically adds Navigate actions
- Tracks page transitions

### ⏱️ Smart Waits
- Automatically adds waits after navigation
- Waits after click actions that trigger page loads
- Prevents race conditions

### 🎭 Event Deduplication
- Debounces rapid input events
- Throttles scroll events
- Merges duplicate Type actions
- Prevents action spam

## Usage Example

```csharp
using AgenticAI.Core.ZeroCode.Recorder;

// Create scenario builder
var builder = new ScenarioBuilder();

// Subscribe to action capture events
builder.OnActionCaptured += (action) => {
    Console.WriteLine($"Captured: {action.Description}");
};

// Start recording
builder.StartRecording("https://www.example.com");

// ... inject scripts into browser and let user interact ...

// Stop recording and get optimized scenario
var scenario = builder.StopRecording(
    scenarioName: "Login_Test",
    module: "Authentication",
    description: "Test user login flow"
);

// scenario.Actions now contains optimized, stable automation steps
```

## Browser Injection

The recorder injects JavaScript into the browser to capture events:

```csharp
var builder = new ScenarioBuilder();
string injectionScript = builder.GetBrowserInjectionScripts();

// Inject into browser (Playwright example)
await page.AddInitScriptAsync(injectionScript);
```

The injected script includes:
- Event capture layer
- Element analyzer
- Smart locator generator  
- Element highlighting
- Navigation detection

## Configuration

### Timing Configuration
- **Debounce Input**: 300ms (adjustable in EventCaptureLayer)
- **Throttle Scroll**: 500ms (adjustable in EventCaptureLayer)
- **Highlight Duration**: 2 seconds

### Locator Confidence Thresholds
- data-testid: 95%
- Stable ID: 90%
- Name: 85%
- aria-label: 80%
- Role: 70%
- Text: 65%
- CSS: 55%
- XPath: 40%

## Benefits Over Previous Implementation

### Before (Old recorder.inject.js)
❌ Only captured click and input events  
❌ Basic CSS selectors with no priority  
❌ No dynamic ID detection  
❌ No parent element resolution  
❌ No action normalization  
❌ No event deduplication  
❌ No element highlighting  
❌ Limited iframe support

### After (Enhanced Architecture)
✅ Captures 7 event types (click, dblclick, input, change, keydown, submit, scroll)  
✅ 8-tier priority locator strategy  
✅ Dynamic ID detection and filtering  
✅ Smart parent element resolution  
✅ Context-aware action normalization  
✅ Event deduplication and merging  
✅ Visual element highlighting  
✅ Full iframe and Shadow DOM support  
✅ Navigation detection  
✅ Smart waits between steps  
✅ Action optimization

## File Structure

```
src/AgenticAI.Core/ZeroCode/
├── Recorder/
│   ├── EventCaptureLayer.cs       # Event capture and preprocessing
│   ├── ElementAnalyzer.cs         # Element analysis and resolution
│   ├── SmartLocatorGenerator.cs   # Priority-based locator generation
│   ├── ActionNormalizer.cs        # DOM event normalization
│   └── ScenarioBuilder.cs         # Orchestration and scenario building
├── recorder.inject.js             # Enhanced browser injection script
└── TestRecorder.cs               # Main recorder orchestrator
```

## Testing

To test the enhanced recorder:

1. Launch the framework: `.\LaunchWebUI.ps1`
2. Navigate to the recorder UI
3. Start recording on any web application
4. Perform interactions (clicks, typing, selections, etc.)
5. Observe:
   - Element highlighting on interaction
   - Console logs showing captured actions with locator strategy
   - Stable locators in the generated scenario
6. Stop recording and review the generated test scenario

## Future Enhancements

Potential improvements:
- Machine learning-based locator selection
- Visual element recognition
- Self-healing locators
- Accessibility compliance checking
- Performance metrics capture
- Screenshot capture on action
- Video recording integration

## Troubleshooting

### Actions not being captured
- Check browser console for JavaScript errors
- Verify `window.__playwrightRecordAction` is exposed
- Ensure injection script loaded successfully

### Poor locator quality
- Add `data-testid` attributes to your application
- Ensure elements have semantic HTML (proper roles, labels)
- Avoid deeply nested DOM structures

### Duplicate actions recorded
- Check debounce/throttle timing configuration
- Verify deduplication logic in ActionNormalizer

## Contributing

When extending the recorder:
1. Update the appropriate component (Event Capture, Analyzer, Locator, Normalizer)
2. Maintain separation of concerns
3. Add confidence scoring for new locator strategies
4. Update this documentation
5. Add unit tests for new functionality

---

**Version**: 2.0  
**Last Updated**: March 2026  
**Author**: Agentic AI Automation Framework Team
