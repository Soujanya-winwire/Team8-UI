import subprocess
import os
import time

# Opt-in features (all off by default, so existing behaviour is unchanged):
#   AITESTGEN_TRACE=1     keep a Playwright trace for failing tests
#   AITESTGEN_RETRIES=N   retry failing tests N times (flaky detection)
TRACE_ENABLED = os.getenv('AITESTGEN_TRACE', '0') == '1'
RETRIES = int(os.getenv('AITESTGEN_RETRIES', '0') or 0)

# Ordered (category, keywords) heuristics used to classify a failure.
FAILURE_CATEGORIES = [
    ('TIMEOUT', ['timeout', 'timed out']),
    ('LOCATOR', ['strict mode violation', 'waiting for locator', 'locator resolved to', 'element not found', 'no element']),
    ('ASSERTION', ['expect(', 'expected', 'tobe', 'tohave', 'assertion']),
    ('NETWORK', ['net::err', 'econnrefused', 'enotfound', 'err_connection', 'ssl']),
    ('ENVIRONMENT', ['browser has been closed', 'executable doesn', 'playwright install', 'enoent']),
]


def classify_failure(text):
    """Best-effort failure category; informational only."""
    lowered = (text or '').lower()
    for category, keywords in FAILURE_CATEGORIES:
        if any(k in lowered for k in keywords):
            return category
    return 'UNKNOWN'


def execute_playwright_scripts(scripts):
    results = []
    os.makedirs('tests/generated', exist_ok=True)
    for script in scripts:
        script_path = f"tests/generated/{script['id']}.spec.ts"
        with open(script_path, 'w') as f:
            f.write(script['script'])
        started = time.perf_counter()
        try:
            # Run Playwright test
            command = ['npx', 'playwright', 'test', script_path]
            if RETRIES > 0:
                command.append(f'--retries={RETRIES}')
            if TRACE_ENABLED:
                command.append('--trace=retain-on-failure')
            result = subprocess.run(command, capture_output=True, text=True)
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
