import { test, expect } from '@playwright/test';

test('Verify login is rejected with invalid credentials', async ({ page }) => {
    await page.goto('https://www.saucedemo.com/')
    const startUrl = page.url()
    await page.fill('#user-name', "invalid_user")
    await page.fill('#password', "wrong_password_123")
    await page.click('#login-button')
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(startUrl)
})
