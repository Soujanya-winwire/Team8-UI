import { test, expect } from '@playwright/test';

test('Verify login is rejected with empty username and password', async ({ page }) => {
    await page.goto('https://www.saucedemo.com/')
    const startUrl = page.url()
    await page.click('#login-button')
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(startUrl)
})
