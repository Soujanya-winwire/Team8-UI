import { test, expect } from '@playwright/test';

test('Verify input field Full Name accepts text', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.fill('#userName', 'sample')
    await page.press('form', 'Enter')
})
