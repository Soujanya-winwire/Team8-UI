# Playwright script generator for AI test cases
import json
import re


def _fill_value(step):
    """JS literal for a step like  Fill #id with "value"  ({N chars} expands to N characters).
    Steps without a quoted value keep the original 'sample' fill."""
    start = step.find(' with "')
    end = step.rfind('"')
    if start == -1 or end < start + 7:
        return "'sample'"
    value = re.sub(r'\{(\d+) chars\}', lambda m: 'A' * int(m.group(1)), step[start + 7:end])
    return json.dumps(value)


def generate_playwright_scripts(test_cases, url):
    scripts = []
    for tc in test_cases:
        steps = []
        steps.append(f"await page.goto('{url}')")
        if any('remains on the same page' in s.lower() for s in tc['steps']):
            steps.append("const startUrl = page.url()")
        for step in tc['steps']:
            lowered = step.lower()
            # Element checks come first: their selectors may contain words like 'click' or 'submit'
            if lowered.startswith('check presence of '):
                steps.append(f"await expect(page.locator('{step.split(' ')[3]}').first()).toBeAttached()")
            elif lowered.startswith('check visibility of '):
                steps.append(f"await expect(page.locator('{step.split(' ')[3]}').first()).toBeVisible()")
            elif lowered.startswith('uncheck '):
                steps.append(f"await page.uncheck('{step.split(' ')[1]}')")
            elif lowered.startswith('check '):
                steps.append(f"await page.check('{step.split(' ')[1]}')")
            elif lowered.startswith('select option in '):
                steps.append(f"await page.selectOption('{step.split(' ')[3]}', {{ index: 0 }})")
            elif lowered.startswith('select '):
                steps.append(f"await page.check('{step.split(' ')[1]}')")
            elif 'remains on the same page' in lowered:
                steps.append("await page.waitForTimeout(1000)")
                steps.append("await expect(page).toHaveURL(startUrl)")
            elif 'remains responsive' in lowered:
                steps.append("await expect(page.locator('body')).toBeVisible()")
            elif 'fill' in lowered:
                # Example: Fill input
                selector = step.split(' ')[1]
                steps.append(f"await page.fill('{selector}', {_fill_value(step)})")
            elif 'click' in lowered:
                selector = step.split(' ')[1]
                steps.append(f"await page.click('{selector}')")
            elif 'submit' in lowered:
                steps.append("await page.press('form', 'Enter')")
        name = tc['name'].replace('\\', '\\\\').replace("'", "\\'")
        script = f"test('{name}', async ({{ page }}) => {{\n    {'\n    '.join(steps)}\n}})"
        scripts.append({'id': tc['id'], 'name': tc['name'], 'script': script})
    return scripts
