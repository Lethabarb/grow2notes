import type { Request } from '@playwright/test';
import { expect, test } from './fixtures.ts';

// Sign-in's view A with a passkey (sign-in.md), with Chromium's virtual authenticator in place of the device's passkey
// prompt (fixtures.ts). As every test here does, each also fails if its page meets a Content Security Policy violation.

// The antiforgery and session cookies are Secure, so the app issues them only on an HTTPS request (design.md §8.4,
// §9.8), and every sign-in request needs the antiforgery token. CI serves the app at http://localhost, so every request
// here says that it came over HTTPS, as App Service's front end says it, which the app trusts from loopback.
test.use({ extraHTTPHeaders: { 'X-Forwarded-Proto': 'https' } });

// A25's message, which says nothing of why a sign-in failed.
const signInFailed =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';

const postTo = (path: string) => (request: Request) =>
  request.method() === 'POST' && new URL(request.url()).pathname === path;
const isOptionsRequest = postTo('/api/auth/passkey/options');
const isSignInRequest = postTo('/api/auth/passkey');

test('with no passkey for Grow2Notes, a press shows no message and leaves focus on the button', async ({
  page,
  authenticator,
}) => {
  let credentialSent = false;
  page.on('request', (request) => {
    credentialSent ||= isSignInRequest(request);
  });

  // The view asks for options as it renders, so that a press opens the prompt at once (sign-in.md *Interactions* step
  // 2). Their 200 also shows that the run reaches the app as HTTPS, which the request's antiforgery token needs.
  const prefetched = page.waitForResponse((response) => isOptionsRequest(response.request()));
  await page.goto('/');
  expect((await prefetched).status()).toBe(200);

  // After any failure the view asks for fresh options, so their answer comes once the failure has been handled.
  const refetched = page.waitForResponse((response) => isOptionsRequest(response.request()));
  const button = page.getByRole('button', { name: 'Sign in with a passkey' });
  await button.click();
  await refetched;

  // The prompt failed as a dismissal does, which the prompt has already explained (sign-in.md *Passkey prompt
  // dismissed*), so the view says nothing and is ready for another press.
  expect(credentialSent).toBe(false);
  await expect(page.getByRole('alert')).toBeEmpty();
  await expect(page.getByRole('heading', { name: 'There is a problem' })).toHaveCount(0);
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  await expect(button).not.toHaveAttribute('aria-disabled');
  await expect(button).toBeFocused();
  expect(await authenticator.credentials()).toEqual([]);
});

test('a passkey that no account holds gets the failed sign-in message, in the summary, which takes focus', async ({
  page,
  authenticator,
}) => {
  await page.goto('/');
  // For the domain that the passkey options name, App:Origin's host, which is that of the address the tests open.
  await authenticator.addPasskey(new URL(page.url()).hostname);

  const signIn = page.waitForResponse((response) => isSignInRequest(response.request()));
  await page.getByRole('button', { name: 'Sign in with a passkey' }).click();
  // The authenticator signed in with its passkey, whose credential matches no account (design.md §8.2).
  expect((await signIn).status()).toBe(401);

  // The summary is the heading's parent (sign-in.md component 2).
  const summary = page.getByRole('heading', { level: 2, name: 'There is a problem' }).locator('..');
  await expect(summary).toContainText(signInFailed);
  await expect(summary).toBeFocused();
  await expect(page.getByRole('alert')).toBeEmpty();
});
