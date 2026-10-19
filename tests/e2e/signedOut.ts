import type { Page, Response } from '@playwright/test';
import { expect } from './fixtures.ts';
import { serveMe, type Me } from './me.ts';

/** The session cookie (design.md §8.4), whose removal ends the session as the app's own 30 idle minutes would. */
export const sessionCookie = '__Host-grow2notes';

const isTo = (path: string) => (response: Response) => new URL(response.url()).pathname === path;

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

/**
 * Ends the app's own session, signed in for real, as an idle half hour does, by removing its cookie, then gives the
 * window focus, which refreshes `/api/auth/me`, whose `401` shows view C in place.
 */
export async function endTheSession(page: Page) {
  await page.context().clearCookies({ name: sessionCookie });
  const refreshed = page.waitForResponse(isTo('/api/auth/me'));
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  expect((await refreshed).status()).toBe(401);
}

/** Signs in again in view C with the passkey that setup made on the virtual authenticator. */
export async function signInAgainWithPasskey(page: Page) {
  const signedIn = page.waitForResponse(isTo('/api/auth/passkey'));
  await page.getByRole('main').getByRole('button', { name: 'Sign in with a passkey' }).click();
  expect((await signedIn).status()).toBe(200);
}
