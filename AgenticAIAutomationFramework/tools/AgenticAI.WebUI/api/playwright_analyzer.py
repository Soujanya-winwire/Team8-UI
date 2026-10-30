from playwright.sync_api import sync_playwright

# Shortest stable selector that contains no spaces or quotes (the script generator splits steps on
# spaces): an id, then a name, then a short DOM path anchored at the nearest ancestor with an id.
SELECTOR_JS = """el => {
    if (el.getAttribute('id')) return '#' + el.getAttribute('id');
    if (el.getAttribute('name')) return '[name=' + el.getAttribute('name') + ']';
    const esc = s => (window.CSS && CSS.escape) ? CSS.escape(s) : s;
    const parts = [];
    let node = el;
    while (node && node.nodeType === 1 && node.tagName.toLowerCase() !== 'html') {
        if (node.id) { parts.unshift('#' + esc(node.id)); break; }
        let index = 1;
        let sibling = node.previousElementSibling;
        while (sibling) { if (sibling.tagName === node.tagName) index++; sibling = sibling.previousElementSibling; }
        parts.unshift(node.tagName.toLowerCase() + ':nth-of-type(' + index + ')');
        node = node.parentElement;
    }
    return parts.join('>');
}"""

# Human-readable label: same priority as before, but whitespace-collapsed and capped so it never
# carries markup or page-sized text into test case titles.
LABEL_JS = """el => {
    const clean = s => (s || '').replace(/\\s+/g, ' ').trim();
    const img = el.querySelector && el.querySelector('img');
    const text = clean(el.getAttribute('aria-label')) || clean(el.getAttribute('placeholder')) ||
        clean(el.getAttribute('name')) || clean(el.getAttribute('id')) || clean(el.innerText || el.textContent) ||
        clean(el.getAttribute('title')) || clean(el.getAttribute('alt')) || clean(img && img.getAttribute('alt')) ||
        clean(el.getAttribute('value')) || clean(el.getAttribute('href'));
    return text.length > 80 ? text.slice(0, 77) + '...' : text;
}"""


def analyze_url(url: str):
    elements = []
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        page.goto(url)
        # Extract input fields
        for el in page.query_selector_all('input,button,select,textarea,a,table,th,td,label'):
            tag = el.evaluate('el => el.tagName.toLowerCase()')
            selector = el.evaluate(SELECTOR_JS)
            label = el.evaluate(LABEL_JS) or tag
            entry = {
                "type": tag,
                "label": label,
                "selector": selector
            }
            if tag == "input":
                # Extra attributes used to pick sensible negative/boundary test cases
                entry["input_type"] = el.evaluate('el => (el.getAttribute("type") || "").toLowerCase()')
                entry["required"] = el.evaluate('el => el.required')
                maxlength = el.evaluate('el => el.maxLength')
                entry["maxlength"] = maxlength if isinstance(maxlength, int) and maxlength > 0 else None
            elements.append(entry)
        browser.close()
    return elements
