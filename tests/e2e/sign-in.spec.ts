import type { Locator, Page, Request } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, expectStops } from './focus.ts';
import { expectFoundationsToPass } from './foundations.ts';
import { codeLabel } from './setupSignIn.ts';

// Sign-in (sign-in.md): view A with a passkey, with Chromium's virtual authenticator in place of the device's passkey
// prompt (fixtures.ts), and A's email and password form with its code step, view B. As every test here does, each also
// fails if its page meets a Content Security Policy violation.

// A25's message, which says nothing of why a sign-in failed.
const signInFailed =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';

const postTo = (path: string) => (request: Request) =>
  request.method() === 'POST' && new URL(request.url()).pathname === path;
const isOptionsRequest = postTo('/api/auth/passkey/options');
const isSignInRequest = postTo('/api/auth/passkey');
const isPasswordRequest = postTo('/api/auth/login');
const isCodeRequest = postTo('/api/auth/login/totp');

// A made-up address that no account holds (A35), and a made-up password.
const email = 'jordan.nobody@example.org';
const password = 'Lantern-Orbit-Kettle-42';

// The alert line directly above `button`, where its request's failures show (sign-in.md component 2a): view A has one
// above the passkey button and one above Continue, and view B one above Sign in.
const alertAbove = (button: Locator) => button.locator('xpath=preceding-sibling::*[1][@role="alert"]');

// The summary is the heading's parent (sign-in.md component 2).
const summaryOf = (page: Page) => page.getByRole('heading', { level: 2, name: 'There is a problem' }).locator('..');

const emailField = (page: Page) => page.getByLabel('Email address');
// A password field has no role, so it is found by its label.
const passwordField = (page: Page) => page.getByLabel('Password', { exact: true });
const continueButton = (page: Page) => page.getByRole('button', { name: 'Continue' });
const codeField = (page: Page) =>
  page.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' });
const signInButton = (page: Page) => page.getByRole('button', { name: 'Sign in', exact: true });

/** The requests that the page sends from now on that `matches`, in the order sent. */
function requestsThat(page: Page, matches: (request: Request) => boolean): Request[] {
  const sent: Request[] = [];
  page.on('request', (request) => {
    if (matches(request)) {
      sent.push(request);
    }
  });
  return sent;
}

/**
 * Answers the page's `POST /api/auth/login` with the `200 {"next":"totp"}` that the app gives a right password, in the
 * browser, so that the code step shows for any email and password, and its layout and refusals need no account with a
 * password: a password sign-in that succeeds is setup.spec.ts's password path signing in again (tests/README.md). The
 * pattern matches that path only, so the code still goes to the app's own `POST /api/auth/login/totp`.
 */
async function acceptAnyPassword(page: Page): Promise<void> {
  await page.route('**/api/auth/login', (route) => route.fulfill({ json: { next: 'totp' } }));
}

/**
 * Counts the page's calls of `navigator.credentials.get`, each of which starts a passkey prompt, through an init script
 * that wraps it before the app's own script runs. Gives a function that reads the count.
 */
async function countPasskeyPrompts(page: Page): Promise<() => Promise<number>> {
  await page.addInitScript(() => {
    const { credentials } = navigator;
    const get = credentials.get.bind(credentials);
    let prompts = 0;
    Object.defineProperty(window, 'passkeyPrompts', { get: () => prompts });
    credentials.get = (options) => {
      prompts += 1;
      return get(options);
    };
  });

  return () => page.evaluate(() => Reflect.get(window, 'passkeyPrompts') as number);
}

/**
 * Expects the page to have kept nothing that was typed (sign-in.md › Privacy and storage, as setup.spec.ts checks
 * setup): nothing in `localStorage`, `sessionStorage` or IndexedDB, no cookie that script can read, none of `typed` in
 * `history.state`, and the address that the test opened, `opened`, with no query or fragment added.
 */
async function expectNothingKept(page: Page, opened: string, typed: readonly string[]) {
  const kept = await page.evaluate(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDB: (await indexedDB.databases()).length,
    cookies: document.cookie,
    historyState: JSON.stringify(history.state),
  }));
  expect(kept).toEqual({
    localStorage: 0,
    sessionStorage: 0,
    indexedDB: 0,
    cookies: '',
    historyState: expect.any(String),
  });
  for (const value of typed) {
    expect(kept.historyState).not.toContain(value);
  }

  await expect(page).toHaveURL(opened);
}

/** The width of the page's content, which scrolls sideways when it is wider than the window. */
const contentWidth = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth);

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
  await expect(alertAbove(button)).toBeEmpty();
  await expect(alertAbove(continueButton(page))).toBeEmpty();
  await expect(summaryOf(page)).toHaveCount(0);
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
  const button = page.getByRole('button', { name: 'Sign in with a passkey' });
  await button.click();
  // The authenticator signed in with its passkey, whose credential matches no account (design.md §8.2).
  expect((await signIn).status()).toBe(401);

  const summary = summaryOf(page);
  await expect(summary).toContainText(signInFailed);
  await expect(summary).toBeFocused();
  await expect(alertAbove(button)).toBeEmpty();
  await expect(alertAbove(continueButton(page))).toBeEmpty();
});

test('Continue with both fields empty lists both messages as links in the summary, which takes focus', async ({
  page,
}) => {
  const passwords = requestsThat(page, isPasswordRequest);
  await page.goto('/');
  // <main> is empty until the app has answered GET /api/auth/me, so axe waits for the form.
  await expect(continueButton(page)).toBeVisible();
  await expectAxeToPass(page);

  await continueButton(page).click();

  const summary = summaryOf(page);
  await expect(summary).toBeFocused();
  await expect(summary.getByRole('listitem')).toHaveText(['Enter your email address', 'Enter your password']);
  await expect(summary.getByRole('link')).toHaveText(['Enter your email address', 'Enter your password']);
  await expect(page).toHaveTitle('Error: Grow2Notes – Sign in');
  await expect(emailField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(passwordField(page)).toHaveAttribute('aria-invalid', 'true');
  await expectAxeToPass(page);
  // Checked last, after axe's two passes, by when a request that the press had sent would have shown.
  expect(passwords).toEqual([]);
});

test('Enter with an address that no account holds gets the failed sign-in message, and no passkey prompt', async ({
  page,
  authenticator,
}) => {
  const prompts = await countPasskeyPrompts(page);
  const passwords = requestsThat(page, isPasswordRequest);
  await page.goto('/');

  await emailField(page).fill(email);
  await passwordField(page).fill(password);
  const answer = page.waitForResponse((response) => isPasswordRequest(response.request()));
  await passwordField(page).press('Enter');
  // The app finds no account with the address, and refuses it as it refuses a wrong password (design.md §8.2).
  expect((await answer).status()).toBe(401);
  expect(passwords.map((request) => request.postDataJSON() as unknown)).toEqual([{ email, password }]);

  const summary = summaryOf(page);
  await expect(summary).toBeFocused();
  await expect(summary.getByRole('listitem')).toHaveText([signInFailed]);
  // Linked to no field and marking none, which would say which part was wrong (A25).
  await expect(summary.getByRole('link')).toHaveCount(0);
  await expect(emailField(page)).not.toHaveAttribute('aria-invalid');
  await expect(passwordField(page)).not.toHaveAttribute('aria-invalid');
  await expect(emailField(page)).toHaveValue(email);
  await expect(passwordField(page)).toHaveValue('');
  await expect(alertAbove(continueButton(page))).toBeEmpty();
  expect(await prompts()).toBe(0);
  await expectNothingKept(page, '/', [email, password]);
  await expectAxeToPass(page);

  // The count is the page's own: a press of the passkey button starts a prompt, which the authenticator, holding no
  // passkey for the app, ends at once, so that none stays open.
  await page.getByRole('button', { name: 'Sign in with a passkey' }).click();
  await expect.poll(prompts).toBe(1);
  expect(await authenticator.credentials()).toEqual([]);
});

test('with the password accepted, the code step shows in its place, and a code meets the app’s own 401', async ({
  page,
}) => {
  await acceptAnyPassword(page);
  const codes = requestsThat(page, isCodeRequest);
  await page.goto('/');
  await emailField(page).fill(email);
  await passwordField(page).fill(password);
  await continueButton(page).click();

  // View B, in A's place at the same address, with the same title, and focus in its field (sign-in.md *Routes* and
  // *Where focus goes*).
  await expect(codeField(page)).toBeFocused();
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toHaveText(
    'Enter the 6-digit code from your authenticator app',
  );
  await expect(emailField(page)).toHaveCount(0);
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  await expect(page).toHaveURL('/');
  await expectAxeToPass(page);

  await codeField(page).fill('12345');
  await signInButton(page).click();

  // Shown on the page, as well as named by the field's description, which takes hidden text too.
  await expect(page.getByText('The code must be 6 digits')).toBeVisible();
  await expect(codeField(page)).toHaveAccessibleDescription(/The code must be 6 digits$/);
  await expect(codeField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(codeField(page)).toBeFocused();
  await expect(page).toHaveTitle('Error: Grow2Notes – Sign in');
  expect(codes).toEqual([]);
  await expectAxeToPass(page);

  await codeField(page).fill('123 456');
  const answer = page.waitForResponse((response) => isCodeRequest(response.request()));
  await signInButton(page).click();
  // No code step is under way in the app, whose two-factor cookie holds A's passkey ceremony at most, so it refuses
  // the code as it refuses a wrong one.
  expect((await answer).status()).toBe(401);
  // One request in all, for the second press, with the code's six digits.
  expect(codes.map((request) => request.postDataJSON() as unknown)).toEqual([{ code: '123456' }]);

  const summary = summaryOf(page);
  await expect(summary).toBeFocused();
  await expect(summary.getByRole('listitem')).toHaveText([signInFailed]);
  await expect(codeField(page)).toHaveValue('');
  await expect(codeField(page)).not.toHaveAttribute('aria-invalid');
  await expect(alertAbove(signInButton(page))).toBeEmpty();
  await expectNothingKept(page, '/', [email, password, '12345', '123 456', '123456']);
  await expectAxeToPass(page);
});

test('at 320 px with text at 200%, neither the form nor the code step scrolls sideways, with their messages', async ({
  page,
}) => {
  await acceptAnyPassword(page);
  await page.setViewportSize({ width: 320, height: 720 });
  // Text at 200%: Chromium's default text size at 32 px, where it is 16 (S00.05.01's Notes).
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  await page.goto('/');
  await expect(page.locator('body')).toHaveCSS('font-size', '36px');
  await expect(continueButton(page)).toBeVisible();
  expect(await contentWidth(page), 'view A').toBeLessThanOrEqual(320);

  // The longest of the form's messages, with its example address, in the summary and above the field.
  await emailField(page).fill('jordan');
  await continueButton(page).click();
  await expect(summaryOf(page).getByRole('link')).toHaveText([
    'Enter an email address in the correct format, like name@example.com',
    'Enter your password',
  ]);
  expect(await contentWidth(page), 'view A with its messages').toBeLessThanOrEqual(320);

  await emailField(page).fill(email);
  await passwordField(page).fill(password);
  await continueButton(page).click();
  await expect(codeField(page)).toBeFocused();
  expect(await contentWidth(page), 'view B').toBeLessThanOrEqual(320);

  await codeField(page).fill('12345');
  await signInButton(page).click();
  await expect(codeField(page)).toHaveAttribute('aria-invalid', 'true');
  expect(await contentWidth(page), 'view B with its message').toBeLessThanOrEqual(320);

  await codeField(page).fill('123456');
  await signInButton(page).click();
  await expect(summaryOf(page)).toBeFocused();
  expect(await contentWidth(page), 'view B with the summary').toBeLessThanOrEqual(320);
});

// sign-in.md *Focus order (Tab)*: A is the skip link, the passkey button, Email address, Password and Continue; after a
// failure the summary's links come before them; B is the code field, which has focus as it shows, then Sign in.
test('view A has its Tab order, a ring at each stop, and passes the foundations', async ({ page }) => {
  test.slow();
  await page.goto('/');
  await expect(continueButton(page)).toBeVisible();

  await expectStops(page, 'Tab', [
    'Skip to main content',
    'Sign in with a passkey',
    'Email address',
    'Password',
    'Continue',
  ]);
  await expectFoundationsToPass(page);
});

test('view A with its error summary has the summary’s links first in Tab order, and passes the foundations', async ({
  page,
}) => {
  test.slow();
  await page.goto('/');
  await continueButton(page).click();
  await expect(summaryOf(page)).toBeFocused();

  await expectStops(page, 'Tab', [
    'Enter your email address',
    'Enter your password',
    'Sign in with a passkey',
    'Email address',
    'Password',
    'Continue',
  ]);
  await expectFoundationsToPass(page);
});

test('the code step, view B, has its field focused, then Sign in, and passes the foundations', async ({ page }) => {
  test.slow();
  await acceptAnyPassword(page);
  await page.goto('/');
  await emailField(page).fill(email);
  await passwordField(page).fill(password);
  await continueButton(page).click();
  await expect(codeField(page)).toBeFocused();

  await expectFocusAt(page, codeLabel);
  await expectStops(page, 'Tab', ['Sign in']);
  await expectFoundationsToPass(page);
});
