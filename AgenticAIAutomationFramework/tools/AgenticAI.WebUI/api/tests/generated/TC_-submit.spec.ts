import { test, expect } from '@playwright/test';

test('Verify button submit is clickable', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.click('#submit')
})
