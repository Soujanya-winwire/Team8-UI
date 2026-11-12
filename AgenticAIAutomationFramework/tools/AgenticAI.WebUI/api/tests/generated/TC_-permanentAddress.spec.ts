import { test, expect } from '@playwright/test';

test('Verify textarea permanentAddress accepts text', async ({ page }) => {
    await page.goto('https://demoqa.com/text-box')
    await page.fill('#permanentAddress', 'sample')
})
