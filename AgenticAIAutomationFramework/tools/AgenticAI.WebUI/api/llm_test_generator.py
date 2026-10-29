# Generates SQA-style test cases from analyzed page elements.
# Replace with actual LLM integration (OpenAI, Azure, etc.) if desired.

PRIORITY_BY_TYPE = {
    'button': 'High',
    'input': 'High',
    'select': 'Medium',
    'textarea': 'Medium',
    'checkbox': 'Medium',
    'radio': 'Medium',
    'link': 'Low',
    'image': 'Low',
}

MODULE_KEYWORDS = (
    ('login', 'Authentication'),
    ('user', 'Authentication'),
    ('password', 'Authentication'),
    ('search', 'Search'),
    ('cart', 'Checkout'),
    ('checkout', 'Checkout'),
    ('nav', 'Navigation'),
    ('menu', 'Navigation'),
)


def _module_for(selector: str) -> str:
    s = (selector or '').lower()
    for keyword, module in MODULE_KEYWORDS:
        if keyword in s:
            return module
    return 'UI'


def _build_case(id_, title, steps, expected, test_data, el_type, preconditions=None):
    """
    Builds a test case in the original flat shape (id/name/steps/expected) that
    playwright_script_generator.py already consumes, extended with SQA template
    fields (module/priority/type/preconditions/detailed_steps/test_data/
    postconditions/tags) for the Excel export and the richer UI display.
    """
    detailed_steps = [
        {'step_number': i + 1, 'action': step, 'expected': 'Action completes without error.'}
        for i, step in enumerate(steps)
    ]
    detailed_steps.append({
        'step_number': len(steps) + 1,
        'action': 'Observe the result',
        'expected': expected,
    })

    return {
        'id': id_,
        'name': title,
        'title': title,
        'module': _module_for(id_),
        'priority': PRIORITY_BY_TYPE.get(el_type, 'Medium'),
        'type': 'Positive',
        'preconditions': preconditions or 'The page under test is loaded and the target element is visible and enabled.',
        'steps': steps,
        'detailed_steps': detailed_steps,
        'test_data': test_data,
        'expected': expected,
        'postconditions': 'No unintended side effects; application state is unchanged except for the tested action.',
        'tags': [el_type, 'UI', 'Smoke'],
    }


def generate_test_cases(elements):
    test_cases = []
    for el in elements:
        el_type = el.get('type', '').lower()
        selector = el.get('selector', '')
        label = el.get('label', selector)
        tc_id = f"TC_{selector}"

        if el_type == 'input':
            test_cases.append(_build_case(
                tc_id, f"Verify input field {label} accepts text",
                ["Navigate to page", f"Fill {selector} with sample text", "Submit form if applicable"],
                f"Input field {label} should accept text", 'sample text', el_type))
        elif el_type == 'button':
            test_cases.append(_build_case(
                tc_id, f"Verify button {label} is clickable",
                ["Navigate to page", f"Click {selector}"],
                f"Button {label} should trigger action", 'N/A', el_type))
        elif el_type == 'checkbox':
            test_cases.append(_build_case(
                tc_id, f"Verify checkbox {label} can be checked/unchecked",
                ["Navigate to page", f"Check {selector}", f"Uncheck {selector}"],
                f"Checkbox {label} should toggle state", 'N/A', el_type))
        elif el_type == 'radio':
            test_cases.append(_build_case(
                tc_id, f"Verify radio button {label} can be selected",
                ["Navigate to page", f"Select {selector}"],
                f"Radio button {label} should be selectable", 'N/A', el_type))
        elif el_type == 'select':
            test_cases.append(_build_case(
                tc_id, f"Verify dropdown {label} options can be selected",
                ["Navigate to page", f"Select option in {selector}"],
                f"Dropdown {label} should allow option selection", 'First available option', el_type))
        elif el_type == 'textarea':
            test_cases.append(_build_case(
                tc_id, f"Verify textarea {label} accepts text",
                ["Navigate to page", f"Fill {selector} with sample text"],
                f"Textarea {label} should accept text", 'sample text', el_type))
        elif el_type == 'link':
            test_cases.append(_build_case(
                tc_id, f"Verify link {label} is clickable",
                ["Navigate to page", f"Click {selector}"],
                f"Link {label} should navigate correctly", 'N/A', el_type))
        elif el_type == 'image':
            test_cases.append(_build_case(
                tc_id, f"Verify image {label} is visible",
                ["Navigate to page", f"Check visibility of {selector}"],
                f"Image {label} should be visible", 'N/A', el_type))
        else:
            test_cases.append(_build_case(
                tc_id, f"Verify element {label} is present",
                ["Navigate to page", f"Check presence of {selector}"],
                f"Element {label} should be present", 'N/A', el_type))
    return test_cases
