import type { Page, Request, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite } from './fixtures.ts';
import { expectStops, tabTo } from './focus.ts';
import { expectNoWayBackToSetup } from './setupHistory.ts';
import { setUpWithPasswordAndPasskey, setUpWithPasswordOnly } from './setupSignIn.ts';
import { expectViewA, signInAgainWithPasskey } from './signedOut.ts';

// The four ways to sign in (D97) in the published app, against sign-in.md: a password only, and a password and a
// passkey, from setup to a second sign-in. A passkey alone and a password with an authenticator app have their own
// specs (setup.spec.ts, sign-in.spec.ts), which D97 leaves as they were. The virtual authenticator (fixtures.ts)
// answers each passkey prompt. As every test here does, each also fails if its page meets a Content Security Policy
// violation.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const emailField = (page: Page) => page.getByLabel('Email address');
// A password field has no role, so it is found by its label.
const passwordField = (page: Page) => page.getByLabel('Password', { exact: true });
const continueButton = (page: Page) => main(page).getByRole('button', { name: 'Continue' });
const passkeyStepButton = (page: Page) => main(page).getByRole('button', { name: 'Sign in with a passkey' });
const goBackButton = (page: Page) => main(page).getByRole('button', { name: 'Go back' });
const createPasswordField = (page: Page) => page.getByLabel('Create a password', { exact: true });
const isTo = (path: string) => (message: Request | Response) => new URL(message.url()).pathname === path;

/** Ends the session and opens `/`, as a person does who comes back later, which shows sign-in's view A. */
async function comeBackSignedOut(page: Page) {
  await page.context().clearCookies();
  await page.goto('/');
  await expectViewA(page);
}

/** Types the email and password on view A and presses Continue, and gives the answer to the password. */
async function sendPassword(page: Page, invite: Invite, password: string): Promise<Response> {
  await emailField(page).fill(invite.email);
  await passwordField(page).fill(password);
  const answered = page.waitForResponse(isTo('/api/auth/login'));
  await continueButton(page).click();
  return answered;
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

test('a password only: setup ends on Today with no way Back, and the second sign-in is the password alone', async ({
  page,
  invite,
}) => {
  const { password } = await setUpWithPasswordOnly(page, invite.setupLink);
  await expect(page).toHaveURL((url) => url.pathname === '/');
  // F's entry went back to the one for `/setup`, which the setup layout replaced with Today's.
  await expectNoWayBackToSetup(page);

  await comeBackSignedOut(page);
  const answer = await sendPassword(page, invite, password);

  // The answer is the signed-in person, not a step to take next.
  expect(answer.status()).toBe(200);
  expect(await answer.json()).toEqual(
    expect.objectContaining({ displayName: invite.displayName, role: 'Manager' }),
  );
  await expect(heading(page)).toContainText('Today');
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  // Neither a code step nor a passkey step came between.
  await expect(page.getByLabel('Enter the 6-digit code from your authenticator app')).toHaveCount(0);
  await expect(main(page).getByRole('heading', { name: 'Use your passkey' })).toHaveCount(0);
});

test('a password only: a wrong password gets the failed sign-in message and no session', async ({ page, invite }) => {
  await setUpWithPasswordOnly(page, invite.setupLink);
  await comeBackSignedOut(page);

  const answer = await sendPassword(page, invite, 'not the password');

  expect(answer.status()).toBe(401);
  await expect(main(page)).toContainText('Sign-in failed. Check your details and try again.');
  await expect(heading(page)).toHaveText('Sign in');
});

test('a password and a passkey: signs in with the password, then the passkey, or with the passkey alone', async ({
  page,
  invite,
  authenticator,
}) => {
  test.slow();
  const { password } = await setUpWithPasswordAndPasskey(page, invite.setupLink);
  expect(await authenticator.credentials()).toHaveLength(1);
  await expectNoWayBackToSetup(page);

  await comeBackSignedOut(page);
  const answer = await sendPassword(page, invite, password);
  expect(answer.status()).toBe(200);
  expect(await answer.json()).toEqual({ next: 'passkey' });

  // The passkey step takes the place of view A, at the same address, and holds focus on its one action.
  await expect(heading(page)).toHaveText('Use your passkey');
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  await expect(passkeyStepButton(page)).toBeFocused();
  await expect(passwordField(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // Keyboard only: the button, then Go back, then the button again, which Enter presses.
  await expectStops(page, 'Tab', ['Go back']);
  await expectStops(page, 'Shift+Tab', ['Sign in with a passkey']);
  const signedIn = page.waitForResponse(isTo('/api/auth/passkey'));
  await page.keyboard.press('Enter');
  expect((await signedIn).status()).toBe(200);
  await expect(heading(page)).toContainText('Today');

  // The passkey alone still signs this person in from view A (A142), so a password and a passkey is no stronger than
  // a passkey.
  await comeBackSignedOut(page);
  await signInAgainWithPasskey(page);
  await expect(heading(page)).toContainText('Today');
});

test('a password and a passkey: a prompt that fails leaves the step as it is, and Go back returns to A', async ({
  page,
  invite,
  authenticator,
}) => {
  test.slow();
  const prompts = await countPasskeyPrompts(page);
  const { password } = await setUpWithPasswordAndPasskey(page, invite.setupLink);
  await comeBackSignedOut(page);
  await sendPassword(page, invite, password);
  await expect(heading(page)).toHaveText('Use your passkey');
  let credentialSent = false;
  page.on('request', (request) => {
    credentialSent ||= isTo('/api/auth/passkey')(request);
  });

  // The device does not verify the person, as when they cancel, so no credential is made.
  await authenticator.setUserVerified(false);
  await passkeyStepButton(page).click();
  await expect.poll(prompts).toBeGreaterThan(0);

  // Nothing is said (sign-in.md *Passkey prompt dismissed*): the step stays, ready for another press.
  await expect(passkeyStepButton(page)).not.toHaveAttribute('aria-disabled', 'true');
  await expect(passkeyStepButton(page)).toBeFocused();
  await expect(main(page).getByRole('alert')).toBeEmpty();
  await expect(heading(page)).toHaveText('Use your passkey');
  expect(credentialSent).toBe(false);

  // Another press, with the person verified, signs in with the same step: the password need not be typed again.
  await authenticator.setUserVerified(true);
  await passkeyStepButton(page).click();
  await expect(heading(page)).toContainText('Today');
});

test('a password and a passkey: Go back returns to A with the email kept and the password field focused', async ({
  page,
  invite,
  authenticator,
}) => {
  test.slow();
  const { password } = await setUpWithPasswordAndPasskey(page, invite.setupLink);
  expect(await authenticator.credentials()).toHaveLength(1);
  await comeBackSignedOut(page);
  await sendPassword(page, invite, password);
  await expect(heading(page)).toHaveText('Use your passkey');

  await goBackButton(page).click();

  await expect(heading(page)).toHaveText('Sign in');
  await expect(emailField(page)).toHaveValue(invite.email);
  await expect(passwordField(page)).toHaveValue('');
  await expect(passwordField(page)).toBeFocused();
  // No message: going back is the person's own choice.
  await expect(main(page).getByRole('heading', { level: 2, name: 'There is a problem' })).toHaveCount(0);
});

test('views E and F for each password choice pass axe, and the passkey step does', async ({ page, invite }) => {
  test.slow();
  await page.goto(invite.setupLink);
  await expect(main(page).getByRole('heading', { level: 2, name: 'Choose how you’ll sign in' })).toBeVisible();
  await expectAxeToPass(page);

  await main(page).getByRole('link', { name: 'Use a password only' }).click();
  await expect(createPasswordField(page)).toBeVisible();
  await expect(main(page).getByRole('button', { name: 'Finish setup' })).toBeVisible();
  await expectAxeToPass(page);

  await page.goBack();
  await main(page).getByRole('link', { name: 'Use a password and a passkey' }).click();
  await expect(createPasswordField(page)).toBeVisible();
  await expect(main(page).getByRole('button', { name: 'Finish setup' })).toBeVisible();
  await expect(main(page)).toContainText(
    'When you select Finish setup, this device asks for your face, fingerprint or PIN to make your passkey.',
  );
  await expectAxeToPass(page);
});

test('a password only can be set up with the keyboard alone', async ({ page, invite }) => {
  test.slow();
  await page.goto(invite.setupLink);
  await expect(main(page).getByRole('heading', { level: 2, name: 'Choose how you’ll sign in' })).toBeVisible();

  // From the h1, which view E focuses: on through the passkey button and the app link, with a ring at each.
  const passwordOnly = main(page).getByRole('link', { name: 'Use a password only' });
  expect(await tabTo(page, passwordOnly)).toEqual([
    'Use a passkey (recommended)',
    'Use a password and authenticator app',
  ]);
  await page.keyboard.press('Enter');
  await expect(createPasswordField(page)).toBeVisible();

  expect(await tabTo(page, createPasswordField(page))).toEqual(['Set up your account']);
  await page.keyboard.type('quietharbour');
  const finished = page.waitForResponse(isTo('/api/auth/setup/password-only'));
  // Enter in the field presses Finish setup, F's only action.
  await page.keyboard.press('Enter');
  expect((await finished).status()).toBe(200);
  await expect(heading(page)).toContainText('Today');
});
