import { test, expect } from '@playwright/test';

test('Verify button button is clickable', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.click('#root>div:nth-of-type(1)>div:nth-of-type(1)>div:nth-of-type(1)>div:nth-of-type(1)>nav:nth-of-type(1)>button:nth-of-type(1)')
})
