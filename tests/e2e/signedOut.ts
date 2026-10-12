import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';

/**
 * Expects sign-in's view A in the signed-out frame (sign-in.md *Routes* and component 1): its heading and title, and
 * the wordmark as text, with no nav and no Account. The heading shows only once the app's `401` to `/api/auth/me` has
 * been handled, so a signed-in screen fails these checks every time.
 */
export async function expectViewA(page: Page) {
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toHaveText('Sign in');
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  const header = page.getByRole('banner');
  await expect(header).toHaveText('Grow2Notes');
  await expect(header.getByRole('link')).toHaveCount(0);
  await expect(page.locator('nav')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Account' })).toHaveCount(0);
  await expect(page.locator('main')).toHaveCount(1);
}
