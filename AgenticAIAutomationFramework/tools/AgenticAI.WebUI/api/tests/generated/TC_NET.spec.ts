import { test, expect } from '@playwright/test';

test('net', async ({ page }) => {
  await page.goto('https://this-domain-does-not-exist-12345.com')
})
