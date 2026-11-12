import { test, expect } from '@playwright/test';

test('Verify element currentAddress-label is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#currentAddress-label').first()).toBeAttached()
})
