# Playwright script generator for AI test cases

def generate_playwright_scripts(test_cases, url):
    scripts = []
    for tc in test_cases:
        steps = []
        steps.append(f"await page.goto('{url}')")
        for step in tc['steps']:
            if 'fill' in step.lower():
                # Example: Fill input
                selector = step.split(' ')[1]
                steps.append(f"await page.fill('{selector}', 'sample')")
            elif 'click' in step.lower():
                selector = step.split(' ')[1]
                steps.append(f"await page.click('{selector}')")
            elif 'submit' in step.lower():
                steps.append("await page.press('form', 'Enter')")
        script = f"test('{tc['name']}', async ({{ page }}) => {{\n    {'\n    '.join(steps)}\n}})"
        scripts.append({'id': tc['id'], 'name': tc['name'], 'script': script})
    return scripts
