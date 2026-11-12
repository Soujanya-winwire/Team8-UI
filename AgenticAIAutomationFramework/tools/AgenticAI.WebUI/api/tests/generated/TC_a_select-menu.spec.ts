import { test, expect } from '@playwright/test';

test('Verify element Select Menu is present', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await expect(page.locator('#item-8>a:nth-of-type(1)').first()).toBeAttached()
})
