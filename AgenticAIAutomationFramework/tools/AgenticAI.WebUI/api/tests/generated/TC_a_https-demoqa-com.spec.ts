import { test, expect } from '@playwright/test';

test('Verify element https://demoqa.com is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#root>header:nth-of-type(1)>a:nth-of-type(1)').first()).toBeAttached()
})
