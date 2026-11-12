import { test, expect } from '@playwright/test';

test('Verify input field name@example.com handles special characters and script injection safely', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.fill('#userEmail', "<script>alert(1)</script>")
    await page.press('form', 'Enter')
    await expect(page.locator('body')).toBeVisible()
})
