import { test, expect } from '@playwright/test';

test('Verify element userEmail-label is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#userEmail-label').first()).toBeAttached()
})
