from playwright.sync_api import sync_playwright

def analyze_url(url: str):
    elements = []
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        page.goto(url)
        # Extract input fields
        for el in page.query_selector_all('input,button,select,textarea,a,table,th,td,label'):
            tag = el.evaluate('el => el.tagName.toLowerCase()')
            selector = el.evaluate('el => el.getAttribute("id")')
            if selector:
                selector = f"#{selector}"
            else:
                selector = el.evaluate('el => el.getAttribute("name")')
                if selector:
                    selector = f"[name={selector}]"
                else:
                    selector = page.evaluate('el => el.outerHTML', el)
            label = el.evaluate('el => el.getAttribute("aria-label") || el.getAttribute("placeholder") || el.getAttribute("name") || el.getAttribute("id") || el.textContent')
            elements.append({
                "type": tag,
                "label": label,
                "selector": selector
            })
        browser.close()
    return elements
