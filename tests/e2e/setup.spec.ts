import type { Locator, Page, Route } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
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

/** Every address in the tab's history, which no script in the page can read, through Chromium's DevTools protocol. */
async function historyUrls(page: Page): Promise<string[]> {
  const devTools = await page.context().newCDPSession(page);
  const { entries } = await devTools.send('Page.getNavigationHistory');
  // Left attached until the page closes: detaching a DevTools session takes the page's virtual authenticator with it,
  // and a later passkey prompt would then find none.
  return entries.map(({ url }) => url);
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
  expect(await historyUrls(page)).not.toContainEqual(expect.stringContaining('#'));
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

  // Today, in the signed-in frame, for the server's today in the 200 Me that ended setup (S00.05.01's fourth
  // criterion, for the passkey path).
  await expect(page).toHaveURL((url) => url.pathname === '/');
  const heading = page.getByRole('main').getByRole('heading', { level: 1 });
  await expect(heading).toContainText('Today');
  await expect(heading.locator('time')).toHaveAttribute('datetime', today);
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expect(page).toHaveTitle('Grow2Notes – Today');
  // Signed in as the new manager, with the session cookie that the browser kept.
  const me = await page.evaluate(async () => {
    const response = await fetch('/api/auth/me');
    return { status: response.status, body: (await response.json()) as unknown };
  });
  expect(me).toEqual({
    status: 200,
    body: expect.objectContaining({ displayName: invite.displayName, role: 'Manager' }),
  });
  // One discoverable passkey, for the domain that the app is reached at (design.md §8.4).
  expect(await authenticator.credentials()).toEqual([
    expect.objectContaining({ isResidentCredential: true, rpId: new URL(baseURL ?? '').hostname }),
  ]);

  // The app keeps nothing in the browser's storage or in cookies that script can read (design.md §9.6, D22), and
  // history.state holds the router's own state only (sign-in.md *Privacy and storage*).
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
  expect(kept.historyState).not.toContain(invite.email);
  expectNoLinkIn(kept.historyState, invite.setupLink);
  expect(kept.historyState).not.toMatch(/passkey/i);

  // Each step replaced the entry before it, so no entry is a setup page, and Back leaves the app.
  expect(await historyUrls(page)).not.toContainEqual(expect.stringContaining('/setup'));
  await page.goBack();
  await expect(page).not.toHaveURL(/\/setup/);

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
