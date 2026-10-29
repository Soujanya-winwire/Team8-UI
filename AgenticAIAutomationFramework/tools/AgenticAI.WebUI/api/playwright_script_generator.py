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
            if 'remains on the same page' in lowered:
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
        script = f"test('{tc['name']}', async ({{ page }}) => {{\n    {'\n    '.join(steps)}\n}})"
        scripts.append({'id': tc['id'], 'name': tc['name'], 'script': script})
    return scripts
