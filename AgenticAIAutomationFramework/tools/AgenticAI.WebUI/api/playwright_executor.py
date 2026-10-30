import subprocess
import os
import re
import shutil
import time

# Opt-in features (all off by default, so existing behaviour is unchanged):
#   AITESTGEN_TRACE=1     keep a Playwright trace for failing tests
#   AITESTGEN_RETRIES=N   retry failing tests N times (flaky detection)
TRACE_ENABLED = os.getenv('AITESTGEN_TRACE', '0') == '1'
RETRIES = int(os.getenv('AITESTGEN_RETRIES', '0') or 0)

# "Show browser" runs: a visible browser, slowed down so each action can be followed.
HEADED_CONFIG = 'playwright.headed.config.ts'
HEADED_CONFIG_BODY = (
    "import { defineConfig } from '@playwright/test';\n\n"
    "export default defineConfig({\n"
    "  use: { headless: false, launchOptions: { slowMo: 400 } },\n"
    "});\n"
)

# Ordered (category, keywords) heuristics used to classify a failure.
FAILURE_CATEGORIES = [
    # Most specific first: Playwright prints "Timeout: Nms" inside assertion and locator
    # failures too, so TIMEOUT must be the last-resort category.
    ('ENVIRONMENT', ['browser has been closed', 'executable doesn', 'playwright install', 'enoent', 'cannot find module']),
    ('NETWORK', ['net::err', 'econnrefused', 'enotfound', 'err_connection', 'ssl_error', 'err_cert']),
    ('ASSERTION', ['expect(', 'assertionerror']),
    ('LOCATOR', ['strict mode violation', 'waiting for locator', 'locator resolved to', 'element not found', 'no element']),
    ('TIMEOUT', ['timeout', 'timed out']),
]


def classify_failure(text):
    """Best-effort failure category; informational only."""
    lowered = (text or '').lower()
    for category, keywords in FAILURE_CATEGORIES:
        if any(k in lowered for k in keywords):
            return category
    return 'UNKNOWN'


def safe_file_stem(test_id):
    """Turn a test id (which may contain HTML, URLs or punctuation) into a valid filename stem."""
    stem = re.sub(r'[^A-Za-z0-9._-]+', '-', str(test_id)).strip('-.')[:80]
    return stem or 'test'


def execute_playwright_scripts(scripts, headed=False):
    results = []
    os.makedirs('tests/generated', exist_ok=True)
    if headed:
        with open(HEADED_CONFIG, 'w') as f:
            f.write(HEADED_CONFIG_BODY)
    for script in scripts:
        script_path = f"tests/generated/{safe_file_stem(script['id'])}.spec.ts"
        with open(script_path, 'w') as f:
            body = script['script']
            # Generated scripts are bare test() calls; add the import the project exporter also adds
            if '@playwright/test' not in body:
                body = "import { test, expect } from '@playwright/test';\n\n" + body + "\n"
            f.write(body)
        started = time.perf_counter()
        try:
            # Run Playwright test
            # On Windows npx is npx.cmd, which subprocess only finds via its full path
            npx = shutil.which('npx') or 'npx'
            command = [npx, 'playwright', 'test', script_path]
            if headed:
                command.append(f'--config={HEADED_CONFIG}')
            if RETRIES > 0:
                command.append(f'--retries={RETRIES}')
            if TRACE_ENABLED:
                command.append('--trace=retain-on-failure')
            # stdin is closed and a timeout is set so a missing package can't hang the API on an install prompt
            result = subprocess.run(command, capture_output=True, text=True, stdin=subprocess.DEVNULL, timeout=300)
            status = 'PASS' if result.returncode == 0 else 'FAIL'
            elapsed = f"{time.perf_counter() - started:.2f}s"
            entry = {
                'testId': script['id'],
                'status': status,
                'output': result.stdout,
                'error': result.stderr,
                'executionTime': elapsed,
                'screenshot': f"reports/screenshots/{script['id']}.png" if status == 'FAIL' else ''
            }
            if status == 'FAIL':
                entry['failureCategory'] = classify_failure(result.stdout + result.stderr)
            elif RETRIES > 0 and 'flaky' in result.stdout.lower():
                entry['flaky'] = True
            results.append(entry)
        except Exception as e:
            results.append({
                'testId': script['id'],
                'status': 'ERROR',
                'output': '',
                'error': str(e),
                'executionTime': f"{time.perf_counter() - started:.2f}s",
                'screenshot': ''
            })
    return results
