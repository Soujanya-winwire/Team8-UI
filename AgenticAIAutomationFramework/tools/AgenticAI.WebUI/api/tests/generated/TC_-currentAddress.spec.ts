import { test, expect } from '@playwright/test';

test('Verify textarea Current Address accepts text', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.fill('#currentAddress', 'sample')
})
