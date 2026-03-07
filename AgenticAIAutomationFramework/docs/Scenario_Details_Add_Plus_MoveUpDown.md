# Scenario Details - Add Plus Icon and Move Up/Down Features

## ? **Feature Addition Complete!**

### ?? **What Was Added**

The Scenario Details modal now includes:
1. **Plus (+) icon** to add steps manually at any position
2. **Move Up (?)** button to move steps upward
3. **Move Down (?)** button to move steps downward
4. **"Add Step"** button at the top to append steps at the end

---

## ?? **New Features**

### **1. Add Step Button (Top Right)**

Located next to "Scenario Flow" heading:

```
??????????????????????????????????????????????????
? ? Scenario Flow (16 steps)    [+ Add Step]  ?
??????????????????????????????????????????????????
```

**Features:**
- Positioned at top right of table
- Adds a new step at the end of the flow
- Opens the "Add Test Step" modal
- Green primary button style

### **2. Plus (+) Icon Per Row**

Each action row now has a Plus icon:

```
Actions Column:
[?] Move Up
[?] Move Down
[+] Add Step Below  ? NEW!
[?] Edit
[??] Delete
```

**Features:**
- Adds a new step immediately after the current step
- Positioned between Move Down and Edit icons
- Green color (#059669)
- Opens "Add Test Step" modal with position parameter

### **3. Move Up (?) Button**

```
[?] Move Up
```

**Features:**
- Moves the current step one position up
- Disabled for the first step (opacity: 0.3)
- Blue color (#0284c7)
- Updates assertion indices automatically
- Shows success message: "Step moved up"

### **4. Move Down (?) Button**

```
[?] Move Down
```

**Features:**
- Moves the current step one position down
- Disabled for the last step (opacity: 0.3)
- Blue color (#0284c7)
- Updates assertion indices automatically
- Shows success message: "Step moved down"

---

## ?? **Visual Layout**

### **Actions Column (5 Buttons)**

```
????????????????????????????????????????????
? #  ? Action ? Locator ? Value ? Actions ?
???????????????????????????????????????????
? 1  ?Navigate? ...     ? ...   ? Actions ?
?    ?        ?         ?       ?  [?][?] ?
?    ?        ?         ?       ?  [+]    ?
?    ?        ?         ?       ?  [?][??]?
???????????????????????????????????????????
```

**Button Layout (Row 1 - First Step):**
```
[?] - Disabled (opacity: 0.3)
[?] - Enabled
[+] - Enabled
[?] - Enabled
[??] - Enabled
```

**Button Layout (Row 2-15 - Middle Steps):**
```
[?] - Enabled
[?] - Enabled
[+] - Enabled
[?] - Enabled
[??] - Enabled
```

**Button Layout (Row 16 - Last Step):**
```
[?] - Enabled
[?] - Disabled (opacity: 0.3)
[+] - Enabled
[?] - Enabled
[??] - Enabled
```

---

## ?? **Button Specifications**

### **Move Up Button**

```css
padding: 3px 6px;
background: transparent;
color: #0284c7;
border: none;
cursor: pointer;
font-size: 12px;
opacity: 0.3;  /* when disabled */
```

**Behavior:**
- Swaps current step with previous step
- Updates assertion afterActionIndex for linked assertions
- Re-renders the modal
- Shows success notification

### **Move Down Button**

```css
padding: 3px 6px;
background: transparent;
color: #0284c7;
border: none;
cursor: pointer;
font-size: 12px;
opacity: 0.3;  /* when disabled */
```

### **Plus (+) Button**

```css
padding: 3px 6px;
background: transparent;
color: #059669;
border: none;
cursor: pointer;
font-size: 12px;
```

### **Actions Column Width**

```css
width: 120px;  /* increased from 80px */
```

---

## ? **Features Summary**

### **Button Count in Actions Column**

| Before | After | Change |
|--------|-------|--------|
| 2 buttons | **5 buttons** | +3 buttons |
| Edit, Delete | Move Up, Move Down, Plus, Edit, Delete | Added reordering |

### **New Capabilities**

? **Add steps at any position** - Plus icon per row  
? **Add steps at the end** - "Add Step" button at top  
? **Move steps upward** - Move Up button  
? **Move steps downward** - Move Down button  
? **Automatic index updates** - Assertions follow steps  
? **Visual feedback** - Success notifications  
? **Disabled states** - First/last steps handled correctly  

---

## ?? **Testing Instructions**

### **1. Test Move Up**

1. Open Scenario Details for any scenario with 3+ steps
2. Click Move Up (?) on step 3
3. ? Verify step 3 is now step 2
4. ? Verify "Step moved up" notification shows
5. Try clicking Move Up on step 1
6. ? Verify button is disabled (grayed out)

### **2. Test Move Down**

1. Open Scenario Details
2. Click Move Down (?) on step 2
3. ? Verify step 2 is now step 3
4. ? Verify "Step moved down" notification shows
5. Try clicking Move Down on last step
6. ? Verify button is disabled (grayed out)

### **3. Test Plus Icon**

1. Open Scenario Details
2. Click Plus (+) icon on step 5
3. ? Verify "Add Test Step" modal opens
4. Add a new action (e.g., "Wait")
5. ? Verify new step appears at position 6

### **4. Test Add Step Button**

1. Open Scenario Details
2. Click "Add Step" button at top right
3. ? Verify "Add Test Step" modal opens
4. Add a new action
5. ? Verify new step appears at the end

---

## ?? **Summary**

The Scenario Details modal now has **complete step management** with:

? **5 action buttons** per row (was 2)  
? **Move Up/Down** buttons for reordering  
? **Plus (+) icon** to insert steps  
? **Add Step button** to append steps  
? **Automatic index updates** for linked assertions  
? **Disabled states** for boundary steps  
? **Success notifications** for all operations  
? **120px Actions column** to fit all buttons  
? **Preserved functionality** (no breaking changes)  

**Result**: A powerful, intuitive step management interface! ??

---

## ?? **Files Modified**

- `tools/AgenticAI.WebUI/wwwroot/scenario-editor.js` - Added Plus icon, Move Up/Down functions

**Note**: Restart the WebUI to see the changes.
