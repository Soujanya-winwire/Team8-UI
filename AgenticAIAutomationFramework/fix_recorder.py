import re

path = r'src\AgenticAI.Core\ZeroCode\TestRecorder.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

print(f"File loaded: {len(content)} chars")

# Find specificSelector line and show what's there
idx = content.find('specificSelector = ')
if idx >= 0:
    snippet = content[idx:idx+150]
    print(f"Found at idx {idx}:")
    print(repr(snippet))
    
    # The line with unescaped quotes inside the C# verbatim string
    # Replace just the problematic assignment line
    # Use line-by-line approach
    lines = content.split('\n')
    fixed = 0
    for i, line in enumerate(lines):
        if 'specificSelector = ' in line and 'name=' in line and 'value=' in line:
            print(f"Line {i+1}: {repr(line)}")
            lines[i] = "                    specificSelector = '[name=' + el.name + '][value=' + el.value + ']';"
            fixed += 1
    
    if fixed > 0:
        content = '\n'.join(lines)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"FIXED {fixed} line(s)")
    else:
        print("No matching line found")
else:
    print("specificSelector not found in file")
