import subprocess
import os

def execute_playwright_scripts(scripts):
    results = []
    os.makedirs('tests/generated', exist_ok=True)
    for script in scripts:
        script_path = f"tests/generated/{script['id']}.spec.ts"
        with open(script_path, 'w') as f:
            f.write(script['script'])
        try:
            # Run Playwright test
            result = subprocess.run([
                'npx', 'playwright', 'test', script_path
            ], capture_output=True, text=True)
            status = 'PASS' if result.returncode == 0 else 'FAIL'
            results.append({
                'testId': script['id'],
                'status': status,
                'output': result.stdout,
                'error': result.stderr,
                'executionTime': 'N/A',
                'screenshot': f"reports/screenshots/{script['id']}.png" if status == 'FAIL' else ''
            })
        except Exception as e:
            results.append({
                'testId': script['id'],
                'status': 'ERROR',
                'output': '',
                'error': str(e),
                'executionTime': 'N/A',
                'screenshot': ''
            })
    return results
