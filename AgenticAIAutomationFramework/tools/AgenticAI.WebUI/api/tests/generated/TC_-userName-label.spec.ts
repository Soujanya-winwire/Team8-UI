import { test, expect } from '@playwright/test';

test('Verify element userName-label is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#userName-label').first()).toBeAttached()
})
