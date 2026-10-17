import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';
import { serveMe, type Me } from './me.ts';

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

/**
 * Signs in on view C, sign-in in place, as `me`, through the password and code steps, which the browser answers for
 * the app, as it answers `/api/auth/me` for the new session from then on, so that the address and password can be
 * any.
 */
export async function signInInPlaceAs(page: Page, me: Me) {
  const main = page.getByRole('main');
  await page.route('**/api/auth/login', (route) => route.fulfill({ json: { next: 'totp' } }));
  await page.route('**/api/auth/login/totp', (route) => route.fulfill({ json: me }));
  await serveMe(page, me);
  await main.getByRole('textbox', { name: 'Email address' }).fill('someone@example.org');
  await page.getByLabel('Password', { exact: true }).fill('made-up password');
  await main.getByRole('button', { name: 'Continue' }).click();
  await page.getByLabel('Enter the 6-digit code from your authenticator app').fill('123456');
  await main.getByRole('button', { name: 'Sign in', exact: true }).click();
}
