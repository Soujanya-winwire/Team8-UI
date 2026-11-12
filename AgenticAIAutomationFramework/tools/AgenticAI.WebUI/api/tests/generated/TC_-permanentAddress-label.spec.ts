import { test, expect } from '@playwright/test';

test('Verify element permanentAddress-label is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#permanentAddress-label').first()).toBeAttached()
})
