# Quick Visual Test Guide - Add & Reorder Features

## ?? What to Test

### ? Visual Elements to Verify

#### 1. **Actions Table Header**
```
????????????????????????????????????????????????????????????
? ?? Scenario Flow (14 steps)     [+ Add Step]           ?
????????????????????????????????????????????????????????????
```
- [ ] "Add Step" button appears on the right side
- [ ] Button has green styling
- [ ] Button shows plus icon

#### 2. **Actions Row Buttons**
```
?????????????????????????????????????????????????????????
? # ?Action? Locator  ?Value ?    Actions               ?
?????????????????????????????????????????????????????????
? 1 ?Click ? #login   ?  -   ? [?][?][+][?][??]       ?
?????????????????????????????????????????????????????????
```
- [ ] 5 buttons in each row
- [ ] Buttons in order: Up, Down, Add, Edit, Delete
- [ ] Up/Down buttons are blue
- [ ] Add button is green
- [ ] Edit button is purple
- [ ] Delete button is red

#### 3. **Button States**

**First Row (Step 1)**
```
[? grayed] [? blue] [+ green] [? purple] [?? red]
```
- [ ] Move Up is disabled (grayed out, opacity 0.3)
- [ ] Move Down is enabled (blue)
- [ ] Other buttons normal

**Last Row (Step 14)**
```
[? blue] [? grayed] [+ green] [? purple] [?? red]
```
- [ ] Move Up is enabled (blue)
- [ ] Move Down is disabled (grayed out, opacity 0.3)
- [ ] Other buttons normal

**Middle Rows (Steps 2-13)**
```
[? blue] [? blue] [+ green] [? purple] [?? red]
```
- [ ] Both Move Up and Move Down enabled
- [ ] All buttons clickable

## ?? Functional Tests

### Test 1: Add Step at End ?
**Steps**:
1. Open scenario "Add-Cart-Get"
2. Click "Add Step" button (top-right of table)
3. Select "Click" action
4. Enter locator: `#test-button`
5. Click "Add Step"

**Expected**:
- [ ] Modal opens
- [ ] New step appears as step 15
- [ ] Step count updates: "Scenario Flow (15 steps)"
- [ ] Success message: "Step added successfully"

### Test 2: Add Step Between ?
**Steps**:
1. Open scenario with 10+ steps
2. Find step 5
3. Click the Plus (green) icon on step 5
4. Select "Type" action
5. Enter locator: `#username`
6. Enter value: `testuser`
7. Click "Add Step"

**Expected**:
- [ ] Modal opens
- [ ] New step appears as step 6
- [ ] Previous step 6 becomes step 7
- [ ] All subsequent steps renumber
- [ ] Success message appears

### Test 3: Move Step Up ?
**Steps**:
1. Open scenario with 5+ steps
2. Remember step 3 details (e.g., "Click #login")
3. Click Move Up (?) on step 3

**Expected**:
- [ ] Step 3 becomes step 2
- [ ] Previous step 2 becomes step 3
- [ ] Success message: "Step moved up"
- [ ] Modal refreshes instantly

### Test 4: Move Step Down ?
**Steps**:
1. Open scenario with 5+ steps
2. Remember step 2 details
3. Click Move Down (?) on step 2

**Expected**:
- [ ] Step 2 becomes step 3
- [ ] Previous step 3 becomes step 2
- [ ] Success message: "Step moved down"
- [ ] Modal refreshes instantly

### Test 5: Disabled Buttons ?
**Steps**:
1. Open any scenario with steps
2. Look at step 1
3. Try to click Move Up button

**Expected**:
- [ ] Button is grayed out
- [ ] Cursor changes to "not-allowed"
- [ ] Button doesn't respond to clicks
- [ ] No error appears

**Repeat for last step Move Down button**

### Test 6: Multiple Reorders ?
**Steps**:
1. Open scenario with 10 steps
2. Move step 5 up twice (becomes step 3)
3. Move step 3 down once (becomes step 4)
4. Click "Save Changes"

**Expected**:
- [ ] Each move works instantly
- [ ] Step numbers update each time
- [ ] Final position is step 4
- [ ] Save succeeds
- [ ] Reload shows correct order

### Test 7: Add Assertion ?
**Steps**:
1. Open scenario
2. Scroll to Assertions section (or create one)
3. Click "Add Assertion" button
4. Select "Text Contains"
5. Enter locator: `.success-message`
6. Enter expected value: `Success`
7. Click "Add Step"

**Expected**:
- [ ] Modal opens
- [ ] New assertion appears
- [ ] Assertion count updates
- [ ] Success message appears

## ?? Visual Checklist

### Button Appearance
- [ ] All buttons same size (padding: 4px 8px)
- [ ] All icons centered in buttons
- [ ] 4px gap between buttons
- [ ] Buttons wrap on small screens
- [ ] Hover effect on all enabled buttons
- [ ] Tooltips appear on hover

### Colors
- [ ] Move Up/Down: Light blue background (#f0f9ff), blue icon (#0284c7)
- [ ] Add Step: Light green background (#ecfdf5), green icon (#059669)
- [ ] Edit: Light gray background (#f1f5f9), purple icon (#7c3aed)
- [ ] Delete: Light red background (#fef2f2), red icon (#ef4444)
- [ ] Disabled: Opacity 0.3, grayed out

### Layout
- [ ] Buttons aligned center in Actions column
- [ ] Buttons don't overflow column
- [ ] Table responsive on resize
- [ ] No horizontal scrollbar
- [ ] Text readable at all sizes

## ?? Edge Cases to Test

### Edge Case 1: Single Step Scenario
1. Create scenario with only 1 step
2. Open it

**Expected**:
- [ ] Move Up disabled
- [ ] Move Down disabled
- [ ] Add Step works
- [ ] Edit works
- [ ] Delete works (leaves 0 steps)

### Edge Case 2: Reorder with Assertions
1. Create scenario with actions and assertions
2. Link assertion to action 3
3. Move action 3 to position 5

**Expected**:
- [ ] Assertion follows to new position
- [ ] `afterActionIndex` updates from 3 to 5
- [ ] Assertion still linked correctly
- [ ] Save preserves linkage

### Edge Case 3: Rapid Reordering
1. Click Move Up 10 times rapidly

**Expected**:
- [ ] Each click processes
- [ ] No errors in console
- [ ] Final position correct
- [ ] Modal doesn't freeze

### Edge Case 4: Add While Scrolled
1. Open scenario with 20+ steps
2. Scroll to bottom
3. Click "Add Step"
4. Add new step

**Expected**:
- [ ] Modal stays open
- [ ] Scroll position maintained
- [ ] New step appears
- [ ] Can see new step after add

## ?? Screenshot Comparison

### Before
```
Actions Column:
[?] [??]
```
- Only 2 buttons
- No reorder capability
- No inline add

### After
```
Actions Column:
[?] [?] [+] [?] [??]
```
- 5 buttons
- Full reorder capability
- Inline add anywhere

## ? Success Criteria

| Feature | Status |
|---------|--------|
| Add Step button visible | ? |
| Plus icon in each row | ? |
| Move Up button works | ? |
| Move Down button works | ? |
| Disabled states correct | ? |
| Colors match design | ? |
| Tooltips appear | ? |
| Success messages show | ? |
| Modal refreshes correctly | ? |
| Assertions update | ? |
| Save preserves order | ? |

## ?? Quick Test Script

```javascript
// Run in browser console after opening scenario modal

// Test 1: Count buttons
const firstRow = document.querySelector('tbody tr:first-child');
const buttons = firstRow.querySelectorAll('button');
console.log(`Buttons in first row: ${buttons.length}`); // Should be 5

// Test 2: Check disabled state
const moveUpBtn = buttons[0];
console.log(`Move Up disabled: ${moveUpBtn.disabled}`); // Should be true

// Test 3: Check button colors
buttons.forEach((btn, i) => {
    const bg = window.getComputedStyle(btn).backgroundColor;
    console.log(`Button ${i} background: ${bg}`);
});

// Test 4: Check total steps
const stepCount = document.querySelectorAll('tbody tr').length;
console.log(`Total steps: ${stepCount}`);
```

## ?? Testing Notes

- Test on different browsers (Chrome, Firefox, Edge)
- Test on different screen sizes (1920px, 1366px, 1024px)
- Test with keyboard navigation (Tab, Enter, Space)
- Test with screen reader if available
- Check browser console for errors
- Verify no memory leaks on multiple opens/closes

---

## ? Final Checklist

Before marking complete:
- [ ] All visual elements appear correctly
- [ ] All buttons work as expected
- [ ] Disabled states function properly
- [ ] Colors match design specifications
- [ ] Tooltips are helpful and accurate
- [ ] Success messages are clear
- [ ] No console errors
- [ ] Responsive on all screen sizes
- [ ] Save functionality preserves changes
- [ ] Reload shows correct data

**If all checked**: ? **Features working perfectly!**  
**If any unchecked**: ?? **Review failed items and fix**
