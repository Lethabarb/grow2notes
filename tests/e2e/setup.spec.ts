import type { Locator, Page, Route } from '@playwright/test';
import { authenticatorCode } from './authenticatorCode.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite } from './fixtures.ts';
import { boxOf } from './layout.ts';

const linkCheck = '**/api/auth/setup/start';

// tokens.css's --colour-action, a primary button's fill, and --colour-surface, a secondary one's (Button.module.css).
const primaryFill = 'rgb(0, 85, 139)';
const secondaryFill = 'rgb(255, 255, 255)';

/**
 * Each form in which a page could keep what a setup link holds: the user ID and the token as the page reads them, the
 * token as the link escapes it (its '+', '/' and '='), and the fragment as written.
 */
function linkParts(setupLink: string): string[] {
  const fragment = new URL(setupLink).hash.slice(1);
  const link = new URLSearchParams(fragment);
  const [userId, token] = [link.get('u'), link.get('t')];
  if (!userId || !token) {
    throw new Error('The setup link has no user ID or no token.');
  }

  return [userId, token, encodeURIComponent(token), fragment];
}

/** Checks that `history.state`, as JSON, holds no part of the setup link. */
function expectNoLinkIn(historyState: string, setupLink: string) {
  for (const part of linkParts(setupLink)) {
    expect(historyState).not.toContain(part);
  }
}

/**
 * The tab's history, which no script in the page can read, through Chromium's DevTools protocol: every address in it,
 * and the index of the one shown.
 */
async function tabHistory(page: Page): Promise<{ urls: string[]; currentIndex: number }> {
  const devTools = await page.context().newCDPSession(page);
  const { entries, currentIndex } = await devTools.send('Page.getNavigationHistory');
  // Left attached until the page closes: detaching a DevTools session takes the page's virtual authenticator with it,
  // and a later passkey prompt would then find none.
  return { urls: entries.map(({ url }) => url), currentIndex };
}

/** How far the control's text reaches past either side of its box, in CSS pixels, or 0. */
function textPastEdges(control: Locator): Promise<number> {
  return control.evaluate((element) => {
    const box = element.getBoundingClientRect();
    const text = document.createRange();
    text.selectNodeContents(element);
    const lines = [...text.getClientRects()];
    return Math.max(0, ...lines.map((line) => Math.max(box.left - line.left, line.right - box.right)));
  });
}

/**
 * Expects Today in the signed-in frame, for the server's today in the 200 Me that ended setup (S00.05.01's fourth
 * criterion), with the new manager signed in through the session cookie that the browser kept.
 */
async function expectTodayAsNewManager(page: Page, invite: Invite, today: string) {
  await expect(page).toHaveURL((url) => url.pathname === '/');
  const heading = page.getByRole('main').getByRole('heading', { level: 1 });
  await expect(heading).toContainText('Today');
  await expect(heading.locator('time')).toHaveAttribute('datetime', today);
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expect(page).toHaveTitle('Grow2Notes – Today');
  const me = await page.evaluate(async () => {
    const response = await fetch('/api/auth/me');
    return { status: response.status, body: (await response.json()) as unknown };
  });
  expect(me).toEqual({
    status: 200,
    body: expect.objectContaining({ displayName: invite.displayName, role: 'Manager' }),
  });
}

/**
 * Expects the app to keep nothing in the browser's storage or in cookies that script can read (design.md §9.6, D22),
 * and `history.state` to hold the router's own state only (sign-in.md *Privacy and storage*): not the email, no part of
 * the setup link, none of what else the path held, and no word that names the method used.
 */
async function expectNothingKept(page: Page, invite: Invite, held: string[], method: RegExp) {
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
  for (const value of [invite.email, ...held]) {
    expect(kept.historyState).not.toContain(value);
  }
  expectNoLinkIn(kept.historyState, invite.setupLink);
  expect(kept.historyState).not.toMatch(method);
}

/**
 * Expects no entry before the current one in the tab's history to be a setup page, and Back to leave the app
 * (sign-in.md *Setup*: "Back from Today does not return to a setup or sign-in step").
 */
async function expectNoWayBackToSetup(page: Page) {
  const { urls, currentIndex } = await tabHistory(page);
  expect(urls.slice(0, currentIndex)).not.toContainEqual(expect.stringContaining('/setup'));
  const app = new URL(page.url()).origin;
  await page.goBack();
  await expect(page).toHaveURL((url) => url.origin !== app);
}

const passkeyButton = (page: Page) =>
  page.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' });

const passwordLink = (page: Page) =>
  page.getByRole('main').getByRole('link', { name: 'Use a password and authenticator app' });

test('a setup link leaves the address bar and the history before it is checked, then view E shows', async ({
  page,
  invite,
}) => {
  // Held back, so that what follows sees the page while the link check has no answer.
  let hold: (route: Route) => void = () => undefined;
  const held = new Promise<Route>((resolve) => {
    hold = resolve;
  });
  await page.route(linkCheck, (route) => hold(route), { times: 1 });

  await page.goto(invite.setupLink);
  const check = await held;

  await expect(page).toHaveURL((url) => url.pathname === '/setup' && url.hash === '');
  // No entry keeps the fragment, which holds the token, and the router's state holds no part of the link either.
  expect((await tabHistory(page)).urls).not.toContainEqual(expect.stringContaining('#'));
  expectNoLinkIn(JSON.stringify(await page.evaluate(() => history.state as unknown)), invite.setupLink);
  await check.continue();

  const main = page.getByRole('main');
  await expect(main.getByRole('heading', { level: 2, name: 'Choose how you’ll sign in' })).toBeVisible();
  await expect(main.getByRole('heading', { level: 1 })).toHaveText('Set up your account');
  await expect(main.getByRole('term')).toHaveText(['Name', 'Email address']);
  await expect(main.getByRole('definition')).toHaveText([invite.displayName, invite.email]);
  await expect(passkeyButton(page)).toHaveCSS('background-color', primaryFill);
  await expect(passwordLink(page)).toHaveCSS('background-color', secondaryFill);
  await expect(passwordLink(page)).toHaveAttribute('href', '/setup/password');
  await expectAxeToPass(page);
});

test('the passkey path signs in at /, with no way Back and nothing kept, uses up the link and signs in again', async ({
  page,
  invite,
  authenticator,
  baseURL,
}) => {
  await page.goto(invite.setupLink);
  const finished = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/setup/passkey');
  await passkeyButton(page).click();
  const { today } = (await (await finished).json()) as { today: string };

  // S00.05.01's fourth criterion, for the passkey path.
  await expectTodayAsNewManager(page, invite, today);
  // One discoverable passkey, for the domain that the app is reached at (design.md §8.4).
  expect(await authenticator.credentials()).toEqual([
    expect.objectContaining({ isResidentCredential: true, rpId: new URL(baseURL ?? '').hostname }),
  ]);
  await expectNothingKept(page, invite, [], /passkey/i);
  // Each step replaced the entry before it, so the tab holds one entry for all of setup, now Today's.
  await expectNoWayBackToSetup(page);

  await page.goto(invite.setupLink);
  const main = page.getByRole('main');
  await expect(main.getByRole('heading', { level: 1 })).toHaveText('This link has expired');
  await expect(main).toContainText('Ask a manager to send a new one.');
  await expect(main.getByRole('button')).toHaveCount(0);
  await expectAxeToPass(page);

  // Signing in a second time works (sign-in.md › Setup; S00.04.04): with the session gone, the address opened shows
  // sign-in, where the passkey that setup made signs the new manager in, and the page they asked for takes its place.
  await page.context().clearCookies();
  await page.goto('/manage');
  const signIn = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/passkey');
  await main.getByRole('button', { name: 'Sign in with a passkey' }).click();
  expect((await signIn).status()).toBe(200);
  await expect(main.getByRole('heading', { level: 1 })).toHaveText('Manage');
  await expect(main.getByRole('heading', { level: 1 })).toBeFocused();
  await expect(page).toHaveURL((url) => url.pathname === '/manage');
  await expect(page).toHaveTitle('Grow2Notes – Manage');
  const again = await page.evaluate(async () => (await fetch('/api/auth/me')).json() as Promise<unknown>);
  expect(again).toEqual(expect.objectContaining({ displayName: invite.displayName, role: 'Manager' }));
});

test('a passkey that the device did not verify is not set up, and both ways on still work', async ({
  page,
  invite,
  authenticator,
}) => {
  await authenticator.setUserVerified(false);
  await page.goto(invite.setupLink);
  await passkeyButton(page).click();

  const alert = page.getByRole('main').getByRole('alert');
  await expect(alert).toHaveText(
    'Your passkey was not set up. Try again, or open the link from your email in your phone’s browser.',
  );
  await expect(passkeyButton(page)).toBeFocused();
  const [alertBox, buttonBox] = [await boxOf(alert), await boxOf(passkeyButton(page))];
  expect(alertBox.y + alertBox.height).toBeLessThanOrEqual(buttonBox.y);

  // The password link still leads on, and setup's memory still holds the account on the way back.
  await passwordLink(page).click();
  await expect(page).toHaveURL((url) => url.pathname === '/setup/password');
  await page.goBack();

  await authenticator.setUserVerified(true);
  await passkeyButton(page).click();
  await expect(page).toHaveURL((url) => url.pathname === '/');
  expect(await authenticator.credentials()).toHaveLength(1);
});

test('without WebAuthn, view E says to open the link in the browser, with the password option primary', async ({
  page,
  invite,
}) => {
  // As in an email app's own browser that has no passkeys (sign-in.md).
  await page.addInitScript(() => {
    Reflect.deleteProperty(window, 'PublicKeyCredential');
  });
  await page.goto(invite.setupLink);

  const main = page.getByRole('main');
  await expect(main.getByText('Open this link in your browser')).toBeVisible();
  await expect(main).toContainText(
    'Passkeys cannot be set up here. Go back to the email and open the link in your phone’s browser, such as Safari '
      + 'or Chrome.',
  );
  await expect(passkeyButton(page)).toHaveCount(0);
  await expect(passwordLink(page)).toHaveCSS('background-color', primaryFill);
  await expectAxeToPass(page);
});

test('at 320 px with text at 200%, view E fits the window, with each label inside its button', async ({
  page,
  invite,
}) => {
  // The browser's text size at twice its default 16 px, as a person sets it, in a window as narrow as WCAG's reflow
  // test (sign-in.md *Layout and accessibility*).
  await page.setViewportSize({ width: 320, height: 720 });
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  await page.goto(invite.setupLink);
  await expect(passwordLink(page)).toBeVisible();

  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
  // "authenticator" is wider than the buttons there, so it breaks inside them rather than running past their edges.
  for (const control of [passkeyButton(page), passwordLink(page)]) {
    expect(await textPastEdges(control), `${control.toString()} keeps its text inside it`).toBeLessThan(1);
  }

  // From the h1, which view E focuses: the skip link wraps there, and its ring, outside its box, stays in the window.
  await page.locator('#page-heading').focus();
  await page.keyboard.press('Shift+Tab');
  const skipLink = page.getByRole('link', { name: 'Skip to main content' });
  await expect(skipLink).toBeFocused();
  const ring = await skipLink.evaluate((element) => {
    const { outlineStyle, outlineOffset, outlineWidth } = getComputedStyle(element);
    return {
      style: outlineStyle,
      right: element.getBoundingClientRect().right + parseFloat(outlineOffset) + parseFloat(outlineWidth),
    };
  });
  expect(ring.style).toBe('solid');
  expect(ring.right).toBeLessThanOrEqual(320);
});

test('a setup page opened with no link says to open the link from the email again', async ({ page }) => {
  await page.goto('/setup');

  const main = page.getByRole('main');
  await expect(main.getByRole('heading', { level: 1 })).toHaveText('Set up your account');
  await expect(main).toContainText('To set up your account, open the link from your email again.');
  await expectAxeToPass(page);
});

test('a link check with no connection says so, and Try again checks the link held in memory', async ({
  page,
  invite,
}) => {
  await page.route(linkCheck, (route) => route.abort('internetdisconnected'), { times: 1 });
  await page.goto(invite.setupLink);

  const main = page.getByRole('main');
  await expect(main.getByText('Your setup link was not checked: no connection. Try again.')).toBeVisible();
  await expectAxeToPass(page);

  // The address no longer has the fragment, so the link comes from the page's memory.
  await main.getByRole('button', { name: 'Try again' }).click();
  await expect(main.getByRole('definition')).toHaveText([invite.displayName, invite.email]);
});

// The password path (sign-in.md views F and G), with a password of 12 lowercase letters, which design.md §8.4's
// options take, though it has no digit, capital or symbol.
const password = 'quietharbour';
const passwordHint = 'Use at least 12 characters.';
const codeHint = 'Find Grow2Notes in the app. If the code changes while you’re typing, you can still use it.';

const passwordField = (page: Page) => page.getByLabel('Create a password', { exact: true });

const showHideButton = (page: Page) =>
  page.getByRole('main').getByRole('button', { name: /^(Show|Hide) password$/ });

const continueButton = (page: Page) => page.getByRole('main').getByRole('button', { name: 'Continue' });

const codeField = (page: Page) => page.getByLabel('Enter the 6-digit code from your authenticator app');

const finishSetupButton = (page: Page) => page.getByRole('main').getByRole('button', { name: 'Finish setup' });

/** The answer to the next `POST /api/auth/setup/password`, which view G sends with the code. */
const setupPasswordAnswer = (page: Page) =>
  page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/setup/password');

/** Opens the setup link and follows view E's password link to view F. */
async function openViewF(page: Page, invite: Invite) {
  await page.goto(invite.setupLink);
  await passwordLink(page).click();
  await expect(page).toHaveURL((url) => url.pathname === '/setup/password');
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toHaveText('Create a password');
}

/** Presses view F's Continue and waits on view G for the setup key, which it returns as G shows it. */
async function continueToViewG(page: Page): Promise<string> {
  await continueButton(page).click();
  await expect(page).toHaveURL((url) => url.pathname === '/setup/app');
  // In capitals, in groups of four with nothing between them (sign-in.md component 16).
  const key = page.getByRole('main').getByRole('definition');
  await expect(key).toHaveText(/^[A-Z2-7]{32}$/);
  return key.innerText();
}

test('view F refuses an empty password, one of only spaces and one of 11 characters, and shows and hides it', async ({
  page,
  invite,
}) => {
  await openViewF(page, invite);
  const field = passwordField(page);
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toBeFocused();
  await expect(field).toHaveAccessibleDescription(passwordHint);
  // At a laptop's width Show/Hide sits beside the field, which only 320 px or 200% text wraps (sign-in.md *Layout -
  // laptop*).
  const [fieldBox, toggleBox] = [await boxOf(field), await boxOf(showHideButton(page))];
  expect(toggleBox.x).toBeGreaterThanOrEqual(fieldBox.x + fieldBox.width);
  expect(toggleBox.y).toBeLessThan(fieldBox.y + fieldBox.height);
  await expectAxeToPass(page);

  // Each message on the field, which takes focus from Continue, with F still shown (sign-in.md *Setup*: "Create a
  // password rejects…").
  for (const [typed, message] of [
    ['', 'Enter a password'],
    [' '.repeat(12), 'Enter a password'],
    [password.slice(0, 11), 'Password must be 12 characters or more'],
  ]) {
    await field.fill(typed);
    await continueButton(page).click();
    await expect(field).toHaveAccessibleDescription(`${passwordHint} Error: ${message}`);
    await expect(field).toBeFocused();
    await expect(page).toHaveURL((url) => url.pathname === '/setup/password');
    await expect(page).toHaveTitle('Error: Grow2Notes – Set up your account');
  }
  await expectAxeToPass(page);

  // Show reveals the password and says so, keeping focus, Hide hides it again, and so does Continue (sign-in.md › Sign
  // in, "On view F, Show reveals the password…").
  const said = page.getByRole('main').locator('[aria-live="polite"]');
  await expect(said).toBeEmpty();
  await showHideButton(page).click();
  await expect(field).toHaveAttribute('type', 'text');
  await expect(said).toHaveText('Your password is visible');
  await expect(page.getByRole('button', { name: 'Hide password' })).toBeFocused();
  await showHideButton(page).click();
  await expect(field).toHaveAttribute('type', 'password');
  await expect(said).toHaveText('Your password is hidden');
  await expect(page.getByRole('button', { name: 'Show password' })).toBeFocused();
  await showHideButton(page).click();
  await continueButton(page).click();
  await expect(field).toHaveAttribute('type', 'password');
  await expect(said).toHaveText('Your password is hidden');
});

test('the password path signs in at /, with no way Back and nothing kept, and uses up the link', async ({
  page,
  invite,
}) => {
  await openViewF(page, invite);
  await passwordField(page).fill(password);
  await continueToViewG(page);

  // The QR code that the server drew, square and at least 200 px across, shown at 200 × 200 (totp-setup.md).
  const main = page.getByRole('main');
  const qrCode = main.getByRole('img', { name: 'QR code for adding Grow2Notes to your authenticator app' });
  const drawn = await qrCode.evaluate(async (image: HTMLImageElement) => {
    await image.decode();
    return { width: image.naturalWidth, height: image.naturalHeight };
  });
  expect(drawn.width).toBeGreaterThanOrEqual(200);
  expect(drawn.height).toBe(drawn.width);
  expect(await boxOf(qrCode)).toMatchObject({ width: 200, height: 200 });
  await expectAxeToPass(page);

  // Browser Back, then the back link, each return to view F with the password still in its field, from which Continue
  // reaches view G again (sign-in.md *Setup*: "Back on view G…").
  await page.goBack();
  await expect(page).toHaveURL((url) => url.pathname === '/setup/password');
  await expect(passwordField(page)).toHaveValue(password);
  await continueToViewG(page);
  await page.getByRole('link', { name: 'Create a password' }).click();
  await expect(page).toHaveURL((url) => url.pathname === '/setup/password');
  await expect(passwordField(page)).toHaveValue(password);
  const key = await continueToViewG(page);

  // The key's code for a step ten minutes away, well outside those Identity accepts: refused, and emptied from the
  // field, which takes focus for the code the app shows now (sign-in.md *Setup*: "A wrong setup code…").
  const wrongCode = authenticatorCode(key, new Date(Date.now() + 10 * 60 * 1000));
  const refused = setupPasswordAnswer(page);
  await codeField(page).fill(wrongCode);
  await finishSetupButton(page).click();
  expect((await refused).status()).toBe(422);
  await expect(codeField(page)).toHaveAccessibleDescription(
    `${codeHint} Error: That code did not work. Enter the code the app shows for Grow2Notes now.`,
  );
  await expect(codeField(page)).toHaveValue('');
  await expect(codeField(page)).toBeFocused();
  await expectAxeToPass(page);

  // The code that an authenticator app holding the key shows now, with the password still in setup's memory.
  const code = authenticatorCode(key);
  const finished = setupPasswordAnswer(page);
  await codeField(page).fill(code);
  await finishSetupButton(page).click();
  const answer = await finished;
  expect(answer.status()).toBe(200);
  const { today } = (await answer.json()) as { today: string };

  // S00.05.01's fourth criterion, for the password path.
  await expectTodayAsNewManager(page, invite, today);
  await expectNothingKept(page, invite, [password, wrongCode, code, key], /password|authenticator/i);
  // View G went back to /setup's entry, which became Today's, so the tab holds one entry for all of setup before the
  // current one, and F's and G's are left as Forward's.
  await expectNoWayBackToSetup(page);

  await page.goto(invite.setupLink);
  await expect(main.getByRole('heading', { level: 1 })).toHaveText('This link has expired');
  await expect(main).toContainText('Ask a manager to send a new one.');
});

test('at 320 px with text at 200%, F and G fit, Show/Hide wraps under the field and the key between groups', async ({
  page,
  invite,
}) => {
  // As view E's test above sets them.
  await page.setViewportSize({ width: 320, height: 720 });
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  await openViewF(page, invite);

  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
  const [fieldBox, toggleBox] = [await boxOf(passwordField(page)), await boxOf(showHideButton(page))];
  expect(toggleBox.y).toBeGreaterThanOrEqual(fieldBox.y + fieldBox.height);

  await passwordField(page).fill(password);
  await continueToViewG(page);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);

  // The key wraps between its groups of four and never inside one.
  const groups = await page
    .getByRole('main')
    .getByRole('definition')
    .locator('span')
    .evaluateAll((spans) =>
      spans.map((span) => {
        const text = document.createRange();
        text.selectNodeContents(span);
        return { top: span.getBoundingClientRect().top, lines: text.getClientRects().length };
      }),
    );
  expect(groups).toHaveLength(8);
  expect(groups.map(({ lines }) => lines)).toEqual(groups.map(() => 1));
  expect(new Set(groups.map(({ top }) => top)).size).toBeGreaterThan(1);
});
