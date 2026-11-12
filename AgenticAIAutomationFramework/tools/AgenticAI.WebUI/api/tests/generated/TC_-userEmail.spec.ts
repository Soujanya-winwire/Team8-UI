import { test, expect } from '@playwright/test';

test('Verify input field name@example.com accepts text', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.fill('#userEmail', 'sample')
    await page.press('form', 'Enter')
})
