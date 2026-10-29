import { test, expect } from '@playwright/test';

test('Verify login is not bypassed by SQL injection', async ({ page }) => {
    await page.goto('https://www.saucedemo.com/')
    const startUrl = page.url()
    await page.fill('#user-name', "' OR '1'='1")
    await page.fill('#password', "' OR '1'='1")
    await page.click('#login-button')
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(startUrl)
})
