import { test, expect } from '@playwright/test';

test('fail', async ({ page }) => {
  await page.goto('https://example.com')
  await expect(page).toHaveTitle('Nope', {timeout: 2000})
})
