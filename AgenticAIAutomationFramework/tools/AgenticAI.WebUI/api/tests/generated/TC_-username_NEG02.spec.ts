import { test, expect } from '@playwright/test';

test('Verify input field Full Name rejects whitespace-only value', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    const startUrl = page.url()
    await page.fill('#userName', "   ")
    await page.press('form', 'Enter')
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(startUrl)
})
