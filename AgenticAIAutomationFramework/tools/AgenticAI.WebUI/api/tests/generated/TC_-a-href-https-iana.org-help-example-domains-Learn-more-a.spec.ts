import { test, expect } from '@playwright/test';

test('Verify element Learn more is present', async ({ page }) => {
    await page.goto('https://example.com')
})
