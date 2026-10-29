# Generates SQA-style test cases from analyzed page elements.
# Replace with actual LLM integration (OpenAI, Azure, etc.) if desired.

import re

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


def _build_case(id_, title, steps, expected, test_data, el_type, preconditions=None,
                case_type='Positive', priority=None, module=None, tags=None):
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
        'module': module or _module_for(id_),
        'priority': priority or PRIORITY_BY_TYPE.get(el_type, 'Medium'),
        'type': case_type,
        'preconditions': preconditions or 'The page under test is loaded and the target element is visible and enabled.',
        'steps': steps,
        'detailed_steps': detailed_steps,
        'test_data': test_data,
        'expected': expected,
        'postconditions': 'No unintended side effects; application state is unchanged except for the tested action.',
        'tags': tags or [el_type, 'UI', 'Smoke'],
    }


# Input types that take free text; others (submit, checkbox, file...) get no text-based negative cases.
TEXT_INPUT_TYPES = ('', 'text', 'email', 'password', 'search', 'tel', 'url', 'number')
SCRIPT_PAYLOAD = '<script>alert(1)</script>'
SQL_PAYLOAD = "' OR '1'='1"
DEFAULT_LONG_LENGTH = 256
SUBMIT_KEYWORDS = ('login', 'log in', 'log-in', 'sign in', 'signin', 'sign-in', 'submit', 'continue')


def _is_clean_selector(selector):
    # The analyzer falls back to raw outerHTML when an element has neither id nor name
    return bool(selector) and (selector.startswith('#') or selector.startswith('[name='))


def _input_type(el):
    return (el.get('input_type') or '').lower()


def _negative_input_cases(el):
    """Negative and boundary cases for one free-text input (additive to its positive case)."""
    selector = el.get('selector', '')
    label = el.get('label', selector)
    base = f"TC_{selector}"
    long_length = el['maxlength'] + 1 if el.get('maxlength') else DEFAULT_LONG_LENGTH
    submit = "Submit form if applicable"
    return [
        _build_case(
            f"{base}_NEG01", f"Verify input field {label} handles special characters and script injection safely",
            ["Navigate to page", f'Fill {selector} with "{SCRIPT_PAYLOAD}"', submit, "Verify page remains responsive"],
            f"Input {label} is sanitized or rejected; no script executes and the page stays usable",
            SCRIPT_PAYLOAD, 'input', case_type='Negative', priority='High',
            tags=['input', 'UI', 'Negative', 'Security', 'Regression']),
        _build_case(
            f"{base}_NEG02", f"Verify input field {label} rejects whitespace-only value",
            ["Navigate to page", f'Fill {selector} with "   "', submit, "Verify user remains on the same page"],
            f"Whitespace-only value in {label} is rejected or trimmed with a validation message",
            '(3 spaces)', 'input', case_type='Negative', priority='Medium',
            tags=['input', 'UI', 'Negative', 'Validation', 'Regression']),
        _build_case(
            f"{base}_BND01", f"Verify input field {label} handles a {long_length}-character value",
            ["Navigate to page", f'Fill {selector} with "{{{long_length} chars}}"', "Verify page remains responsive"],
            f"Input {label} truncates or rejects values over the length limit without errors",
            f"{long_length} characters", 'input', case_type='Boundary', priority='Medium',
            tags=['input', 'UI', 'Boundary', 'Validation', 'Regression']),
    ]


def _login_form_cases(elements):
    """Credential-form negative cases, generated only when a username, password and submit control exist."""
    def text_of(e):
        return f"{e['selector']} {e.get('label', '')}".lower()

    inputs = [e for e in elements if e.get('type', '').lower() == 'input' and _is_clean_selector(e.get('selector', ''))]
    password = next((e for e in inputs if _input_type(e) == 'password' or 'pass' in text_of(e)), None)
    username = next((e for e in inputs if e is not password and _input_type(e) in TEXT_INPUT_TYPES
                     and any(k in text_of(e) for k in ('user', 'email', 'login', 'uname'))), None)
    if not username or not password:
        return []
    # Pick the real submit control: a named one (login/submit/sign in) first, then a submit-type input;
    # otherwise fall back to pressing Enter in the form rather than clicking an arbitrary button.
    buttons = [e for e in elements if _is_clean_selector(e.get('selector', '')) and '>' not in e['selector']
               and (e.get('type', '').lower() == 'button' or _input_type(e) in ('submit', 'button'))]
    submit = (next((e for e in buttons if any(k in text_of(e) for k in SUBMIT_KEYWORDS)), None)
              or next((e for e in buttons if _input_type(e) == 'submit'), None))
    u, p = username['selector'], password['selector']
    press = [f"Click {submit['selector']}"] if submit else ["Submit form if applicable"]
    expected = "Login is rejected, an error is shown and the user stays on the login page"

    def case(suffix, title, fills, data, case_type='Negative', priority='High'):
        steps = ["Navigate to page"] + fills + press + ["Verify user remains on the same page"]
        return _build_case(f"TC_LOGIN_{suffix}", title, steps, expected, data, 'input',
                           case_type=case_type, priority=priority, module='Authentication',
                           tags=['Authentication', case_type, 'Regression'])

    return [
        case('NEG01', "Verify login is rejected with empty username and password", [], 'username: (empty), password: (empty)'),
        case('NEG02', "Verify login is rejected with invalid credentials",
             [f'Fill {u} with "invalid_user"', f'Fill {p} with "wrong_password_123"'],
             'username: invalid_user, password: wrong_password_123'),
        case('NEG03', "Verify login is rejected with username only",
             [f'Fill {u} with "valid_user"'], 'username: valid_user, password: (empty)'),
        case('NEG04', "Verify login is rejected with password only",
             [f'Fill {p} with "some_password"'], 'username: (empty), password: some_password'),
        case('NEG05', "Verify login is not bypassed by SQL injection",
             [f'Fill {u} with "{SQL_PAYLOAD}"', f'Fill {p} with "{SQL_PAYLOAD}"'],
             f'username: {SQL_PAYLOAD}, password: {SQL_PAYLOAD}', priority='Critical'),
        case('BND01', f"Verify login handles a {DEFAULT_LONG_LENGTH}-character username",
             [f'Fill {u} with "{{{DEFAULT_LONG_LENGTH} chars}}"', f'Fill {p} with "some_password"'],
             f'username: {DEFAULT_LONG_LENGTH} characters', case_type='Boundary', priority='Medium'),
    ]


ELEMENT_NOUNS = {'a': 'link', 'img': 'image', 'th': 'table header', 'td': 'table cell'}


def _is_path_selector(selector):
    # DOM-path fallback from the analyzer (element has neither id nor name)
    return '>' in (selector or '') and ':nth-of-type' in selector


def _short_id(el):
    """Readable id for path-selector elements, e.g. TC_a_fork-me-on-github."""
    slug = re.sub(r'[^a-z0-9]+', '-', (el.get('label') or '').lower()).strip('-')[:40]
    return f"TC_{(el.get('type') or 'element').lower()}_{slug or 'element'}"


def _humanize_steps(test_cases, elements):
    """
    Show readable names instead of DOM paths in the displayed/exported steps. Only the display copy
    (detailed_steps) changes; 'steps' keeps the real selector for the script generator.
    """
    names = {}
    for el in elements:
        selector = el.get('selector', '')
        if _is_path_selector(selector):
            tag = (el.get('type') or 'element').lower()
            names[selector] = f"the {ELEMENT_NOUNS.get(tag, tag)} \"{el.get('label') or tag}\""
    for sel in sorted(names, key=len, reverse=True):
        for tc in test_cases:
            for step in tc['detailed_steps']:
                step['action'] = step['action'].replace(sel, names[sel])


def generate_test_cases(elements):
    test_cases = []
    for el in elements:
        el_type = el.get('type', '').lower()
        selector = el.get('selector', '')
        label = el.get('label') or selector
        tc_id = _short_id(el) if _is_path_selector(selector) else f"TC_{selector}"

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
        if el_type == 'input' and _is_clean_selector(selector) and _input_type(el) in TEXT_INPUT_TYPES:
            test_cases.extend(_negative_input_cases(el))
    test_cases.extend(_login_form_cases(elements))
    # Repeated selectors on a page would otherwise yield duplicate ids (and overwritten spec files)
    seen = {}
    for tc in test_cases:
        n = seen.get(tc['id'], 0)
        seen[tc['id']] = n + 1
        if n:
            tc['id'] = f"{tc['id']}_{n + 1}"
    _humanize_steps(test_cases, elements)
    return test_cases
