import { test, expect } from '@playwright/test';

test('Verify element Broken Links - Images is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#item-6>a:nth-of-type(1)').first()).toBeAttached()
})
