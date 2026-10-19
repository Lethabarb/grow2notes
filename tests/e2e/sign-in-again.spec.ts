import { randomUUID } from 'node:crypto';
import type { Page, Request, Response } from '@playwright/test';
import { authenticatorCode } from './authenticatorCode.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite, type VirtualAuthenticator } from './fixtures.ts';
import { failMe, manager, serveMe } from './me.ts';
import { codeLabel, setUpWithPassword } from './setupSignIn.ts';
import { endTheSession, sessionCookie, signInAgainWithPasskey } from './signedOut.ts';
import { alex, answerSetupLinks, people, sam, serveUsers } from './users.ts';

// Signing in again in place, sign-in's view C, in the published app (S00.04.08), against sign-in.md's *Sign in again,
// in place* and app-shell.md component 7 and its Session criteria. The first two tests sign in for real, through setup
// on the virtual authenticator, and end the app's own session by removing its cookie, as an idle half hour or the
// 12-hour limit does; the third answers /api/auth/me and the Users screens in the browser (me.ts, users.ts).

const main = (page: Page) => page.getByRole('main');
// The one <h1> that shows: view C's while it does, as the screen's is hidden under it.
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nameField = (page: Page) => page.locator('#displayName');
// Invite user's email field, which view C's own, with an id of its own, never hides.
const inviteEmailField = (page: Page) => page.locator('#email');
const managerRadio = (page: Page) => page.locator('#role-manager');
const sendInvite = (page: Page) => main(page).getByRole('button', { name: /^(Send invite|Sending…)$/ });
const pageBar = (page: Page) => page.locator('#page-bar');

const signedOutHeading = 'You’ve been signed out';
const signedOutSentence = 'Sign in again to go back to where you were.';
const failedMessage =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';

const isTo = (path: string) => (message: Request | Response) => new URL(message.url()).pathname === path;
const isInvite = (request: Request) => request.method() === 'POST' && isTo('/api/admin/users')(request);
const isSetupLink = (request: Request) =>
  request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/setup-link');

/**
 * Counts the requests that the page sends from now on that `counted` picks, across page loads. Gives a function that
 * gives the count once the browser has reported every request that the page sent before the call: it reports them in
 * the order they were sent, so it has reported each once the test has the answer to a request of its own
 * (tests/README.md).
 */
function countRequests(page: Page, counted: (request: Request) => boolean): () => Promise<number> {
  let count = 0;
  page.on('request', (request) => {
    if (counted(request)) {
      count += 1;
    }
  });

  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return count;
  };
}

/** Expects view C at `address`: its <h1>, focused, its sentence, the title, and no nav, Account or page bar. */
async function expectSignedOutAt(page: Page, address: string) {
  await expect(heading(page)).toHaveText(signedOutHeading);
  await expect(heading(page)).toBeFocused();
  await expect(main(page)).toContainText(signedOutSentence);
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  await expect(page).toHaveURL(address);
  await expect(page.getByRole('navigation')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Account' })).toHaveCount(0);
  await expect(pageBar(page)).toBeHidden();
}

/**
 * Signs the invited manager in through setup with a passkey on the virtual authenticator, as setup.spec.ts does, and
 * opens Invite user from the Users list, through Manage, as a manager does.
 */
async function signInAndOpenInviteUser(page: Page, invite: Invite, authenticator: VirtualAuthenticator) {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(main(page).getByRole('list')).toBeVisible();
  await main(page).getByRole('link', { name: 'Invite user', exact: true }).click();
  await expect(heading(page)).toHaveText('Invite user');
}

/** Fills Invite user half way, for a made-up person with a new `example.org` address, as Manager, and gives them. */
async function fillInviteHalfWay(page: Page) {
  const person = { displayName: 'Jo Sample', email: `jo.sample.${randomUUID()}@example.org` };
  await nameField(page).fill(person.displayName);
  await inviteEmailField(page).fill(person.email);
  await managerRadio(page).check();
  return person;
}

/** Expects Invite user's fields, shown or hidden, to hold `person`, as Manager. */
async function expectInviteKept(page: Page, person: { displayName: string; email: string }) {
  await expect(nameField(page)).toHaveValue(person.displayName);
  await expect(inviteEmailField(page)).toHaveValue(person.email);
  await expect(managerRadio(page)).toBeChecked();
}

/**
 * The element that has focus: outside the page, the page itself, or the element by its id, or, for one with none,
 * its own words, those of a button's first part.
 */
const focused = (page: Page) =>
  page.evaluate(() => {
    const element = document.activeElement;
    if (!document.hasFocus()) {
      return 'outside the page';
    }

    if (element === null || element === document.body) {
      return 'the page itself';
    }

    return element.id || ((element.firstElementChild ?? element).textContent ?? '');
  });

test('signed in for real, a 401 over a half-filled Invite user shows view C, and the passkey brings it back', async ({
  page,
  invite,
  authenticator,
}) => {
  await signInAndOpenInviteUser(page, invite, authenticator);
  const person = await fillInviteHalfWay(page);
  const address = page.url();
  const invitesSent = countRequests(page, isInvite);

  // A real 401: the session cookie removed, then the window's focus.
  await endTheSession(page);

  await expectSignedOutAt(page, address);
  await expect(nameField(page)).toBeHidden();
  await expectInviteKept(page, person);
  await expectAxeToPass(page);

  await signInAgainWithPasskey(page);

  // The form as it was, at the same address, with focus on its <h1>, the frame signed in again.
  await expect(heading(page)).toHaveText('Invite user');
  await expect(heading(page)).toBeFocused();
  await expect(nameField(page)).toBeVisible();
  await expectInviteKept(page, person);
  await expect(page).toHaveURL(address);
  await expect(page).toHaveTitle('Grow2Notes – Invite user');
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expect(pageBar(page).getByRole('link', { name: 'Users' })).toBeVisible();
  expect(await invitesSent()).toBe(0);

  // A Send invite that meets the 401 shows view C too, and is not sent again after signing in.
  await page.context().clearCookies({ name: sessionCookie });
  const refused = page.waitForResponse((response) => isInvite(response.request()));
  await sendInvite(page).click();
  expect((await refused).status()).toBe(401);
  await expectSignedOutAt(page, address);

  await signInAgainWithPasskey(page);

  await expect(heading(page)).toHaveText('Invite user');
  await expect(heading(page)).toBeFocused();
  await expectInviteKept(page, person);
  await expect(main(page).getByRole('alert')).toBeEmpty();
  await expect(sendInvite(page)).not.toHaveAttribute('aria-disabled');
  expect(await invitesSent()).toBe(1);
});

test('another person signing in on view C over Invite user reloads to their Today, and no invite is sent', async ({
  page,
  invite,
  secondInvite,
  authenticator,
}) => {
  const invitesSent = countRequests(page, isInvite);
  // The other person, of an organisation of their own, set up first with a password, then gone from the browser.
  const other = await setUpWithPassword(page, secondInvite);
  await page.context().clearCookies();

  await signInAndOpenInviteUser(page, invite, authenticator);
  await fillInviteHalfWay(page);
  await endTheSession(page);
  await expectSignedOutAt(page, page.url());

  // A wrong password leaves view C up, with the Sign-in failed summary, focused.
  await main(page).getByRole('textbox', { name: 'Email address' }).fill(secondInvite.email);
  await page.getByLabel('Password', { exact: true }).fill('not the password');
  const refused = page.waitForResponse(isTo('/api/auth/login'));
  await main(page).getByRole('button', { name: 'Continue' }).click();
  expect((await refused).status()).toBe(401);
  const summary = main(page).locator('div[tabindex="-1"]:has(> h2)');
  await expect(summary).toBeFocused();
  await expect(summary).toContainText(failedMessage);
  await expect(heading(page)).toHaveText(signedOutHeading);
  await expect(page).toHaveTitle('Error: Grow2Notes – Sign in');

  // The right one, then the code step, under view C's <h1>, its label an <h2>.
  await page.getByLabel('Password', { exact: true }).fill(other.password);
  await main(page).getByRole('button', { name: 'Continue' }).click();
  await expect(main(page).getByRole('heading', { level: 2, name: codeLabel })).toBeVisible();
  await expect(heading(page)).toHaveText(signedOutHeading);
  await expect(page.getByLabel(codeLabel)).toBeFocused();
  await expectAxeToPass(page);
  const reload = page.waitForRequest((request) => request.isNavigationRequest());
  await page.getByLabel(codeLabel).fill(authenticatorCode(other.key));
  await main(page).getByRole('button', { name: 'Sign in', exact: true }).click();

  // A full page load of Today, for the other person, with none of the first's form.
  expect(new URL((await reload).url()).pathname).toBe('/');
  await expect(heading(page)).toContainText('Today');
  await expect(page).toHaveURL('/');
  await expect(nameField(page)).toHaveCount(0);
  await page.getByRole('button', { name: 'Account' }).click();
  await expect(page.getByText(`Signed in as ${secondInvite.displayName}`, { exact: true })).toBeVisible();
  expect(await invitesSent()).toBe(0);
});

test("on a user's page, a Resend invite's 401 shows view C over the hidden back link, never sent again", async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await answerSetupLinks(page, { status: 401 });
  const resendsSent = countRequests(page, isSetupLink);
  await page.goto(`/manage/users/${alex.id}`);
  await expect(heading(page)).toHaveText(alex.displayName);
  const backLink = pageBar(page).getByRole('link', { name: 'Users' });
  await expect(backLink).toBeVisible();

  // The session has gone, so that /api/auth/me answers 401 too, as the window's focus asks it again below.
  await failMe(page, 401);
  await main(page).getByRole('button', { name: 'Resend invite' }).click();

  await expectSignedOutAt(page, page.url());
  await expect(backLink).toBeHidden();
  // From the <h1>, Tab goes through view C's controls, out of the page, and back by the skip link, never to the back
  // link or the hidden page.
  const stops: string[] = [];
  for (let stop = 0; stop < 7; stop += 1) {
    await page.keyboard.press('Tab');
    stops.push(await focused(page));
  }
  expect(stops).toEqual([
    'Sign in with a passkey',
    'signed-out-email',
    'signed-out-password',
    'Continue',
    'outside the page',
    'Skip to main content',
    'Sign in with a passkey',
  ]);
  await expectAxeToPass(page);

  // Signing in again as Sam Lee, through the password and code steps, which the browser answers for the app, as it
  // answers /api/auth/me for the new session.
  await page.route('**/api/auth/login', (route) => route.fulfill({ json: { next: 'totp' } }));
  await page.route('**/api/auth/login/totp', (route) => route.fulfill({ json: manager }));
  await serveMe(page, manager);
  await main(page).getByRole('textbox', { name: 'Email address' }).fill(sam.email);
  await page.getByLabel('Password', { exact: true }).fill('made-up password');
  await main(page).getByRole('button', { name: 'Continue' }).click();
  await page.getByLabel(codeLabel).fill('123456');
  await main(page).getByRole('button', { name: 'Sign in', exact: true }).click();

  await expect(heading(page)).toHaveText(alex.displayName);
  await expect(heading(page)).toBeFocused();
  await expect(backLink).toBeVisible();
  await expect(main(page).getByRole('alert')).toBeEmpty();
  await expect(main(page).locator('button + [role="status"]')).toBeEmpty();
  expect(await resendsSent()).toBe(1);
});
