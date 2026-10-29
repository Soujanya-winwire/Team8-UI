import { test, expect } from '@playwright/test';

test('loc', async ({ page }) => {
  await page.goto('https://example.com')
  await page.click('#does-not-exist', {timeout: 2000})
})
