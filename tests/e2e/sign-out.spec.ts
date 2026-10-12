import type { Page, Request, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { boxOf } from './layout.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { expectViewA } from './signedOut.ts';

// Sign out in the published app (S00.04.06), against app-shell.md › Account and Sign out.

const isTo = (path: string) => (message: Request | Response) => new URL(message.url()).pathname === path;
const isLogout = isTo('/api/auth/logout');
const isMe = isTo('/api/auth/me');

const account = (page: Page) => page.getByRole('button', { name: 'Account' });
const panel = (page: Page) => page.locator('#account-panel');
// The panel's one button, by its place rather than its name, which is "Signing out…" while it is busy.
const signOut = (page: Page) => panel(page).getByRole('button');

/**
 * Answers `/api/auth/me` with `me` through `serveMe` until the page sends the logout, and from then on lets it through
 * to the app, which answers `401`, as nobody is signed in there, so the page that the logout reloads is signed out.
 * Gives the logout requests that the page sends.
 */
async function serveMeUntilLogout(page: Page, me: Me): Promise<Request[]> {
  const logouts: Request[] = [];
  page.on('request', (request) => {
    if (isLogout(request)) {
      logouts.push(request);
    }
  });
  await serveMe(page, me);
  // Tried before serveMe's answer, as Playwright tries the latest route first. The logout's request event comes before
  // any request of the page that the logout's answer loads, so every /me from that page on reaches the app.
  await page.route('**/api/auth/me', (route) => (logouts.length === 0 ? route.fallback() : route.continue()));
  return logouts;
}

/** The app's own `401` to `/api/auth/me`, which a page that has loaded with no session meets. */
const appSaysSignedOut = (page: Page) =>
  page.waitForResponse((response) => isMe(response) && response.status() === 401);

/** Expects the page to meet the app's `401` from `/api/auth/me` and show sign-in's view A, as smoke.spec.ts does. */
async function expectSignedOut(page: Page, appMe: Promise<Response>) {
  await appMe;
  await expectViewA(page);
}

for (const me of [worker, manager]) {
  const role = me.role.toLowerCase();

  for (const width of [375, 1280]) {
    test(`as a ${role} at ${width} px, Sign out sends one logout, then / loads afresh, signed out`, async ({
      page,
    }) => {
      const logouts = await serveMeUntilLogout(page, me);
      await page.setViewportSize({ width, height: 800 });
      // From /manage, the manager's Manage page and a worker's Page not found, rather than /: a page that loads its
      // own address replaces itself whichever way it loads, so only from another address does the reload's address
      // show, and history.length show that the page was replaced rather than added.
      await page.goto('/manage');
      await account(page).click();
      await expect(panel(page).getByText(`Signed in as ${me.displayName}`, { exact: true })).toBeVisible();

      // Gone once / has loaded again, as the new page starts with nothing in memory.
      await page.evaluate(() => Reflect.set(window, 'beforeSignOut', true));
      const historyLength = await page.evaluate(() => history.length);
      const logoutAnswer = page.waitForResponse(isLogout);
      const appMe = appSaysSignedOut(page);
      const loaded = page.waitForEvent('load');
      await signOut(page).click();

      // Nobody is signed in to the app, so its answer is the 401 that counts as done (S00.04.06's Notes).
      expect((await logoutAnswer).status()).toBe(401);
      await loaded;
      await expect(page).toHaveURL('/');
      expect(await page.evaluate(() => Reflect.has(window, 'beforeSignOut'))).toBe(false);
      // Replaced, not added: Back cannot reach the signed-in page.
      expect(await page.evaluate(() => history.length)).toBe(historyLength);
      await expectSignedOut(page, appMe);

      // One logout in all, none from the page loaded since, a POST with the token that api() fetched for it.
      expect(logouts).toHaveLength(1);
      expect(logouts[0].method()).toBe('POST');
      expect(await logouts[0].headerValue('X-XSRF-TOKEN')).toBeTruthy();
    });
  }

  test(`as a ${role}, Sign out offline says so in the panel, with focus on it, still signed in`, async ({ page }) => {
    await serveMe(page, me);
    await page.goto('/');
    await account(page).click();
    const status = panel(page).getByRole('status');
    await expect(status).toBeEmpty();
    await expectAxeToPass(page);

    await page.context().setOffline(true);
    await signOut(page).click();

    await expect(status).toHaveText('Not signed out: no connection. Try again.');
    // Ready again, with its own name, rather than busy.
    await expect(signOut(page)).toBeFocused();
    await expect(signOut(page)).toHaveAccessibleName('Sign out');
    await expect(signOut(page)).not.toHaveAttribute('aria-disabled', 'true');
    // Still signed in, with the panel open.
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expect(account(page)).toHaveAttribute('aria-expanded', 'true');
    await expect(panel(page).getByText(`Signed in as ${me.displayName}`, { exact: true })).toBeVisible();
    await expectAxeToPass(page);
  });

  test(`as a ${role}, at 320 px with text at 200%, Sign out is the panel's full width, in the window`, async ({
    page,
  }) => {
    await serveMe(page, me);
    await page.setViewportSize({ width: 320, height: 800 });
    // Text at 200%: Chromium's default text size at 32 px, where it is 16 (S00.05.01's Notes).
    const devTools = await page.context().newCDPSession(page);
    await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
    await page.goto('/');
    await expect(page.locator('body')).toHaveCSS('font-size', '36px');
    await account(page).click();

    const [panelBox, buttonBox] = [await boxOf(panel(page)), await boxOf(signOut(page))];
    expect(buttonBox.x).toBeCloseTo(panelBox.x, 0);
    expect(buttonBox.width).toBeCloseTo(panelBox.width, 0);
    await expect(signOut(page)).toBeInViewport({ ratio: 1 });
    const scrollsSideways = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(scrollsSideways, 'sideways scroll').toBe(false);
  });
}

test('after the manager has followed Manage and signed out, Back shows no signed-in screen', async ({ page }) => {
  await serveMeUntilLogout(page, manager);
  await page.goto('/');
  await page.getByRole('banner').getByRole('link', { name: 'Manage', exact: true }).click();
  await expect(page).toHaveURL('/manage');

  await account(page).click();
  const reloaded = appSaysSignedOut(page);
  await signOut(page).click();
  await expectSignedOut(page, reloaded);
  await expect(page).toHaveURL('/');

  // The entry before Manage's, which the signed-in page also made, now loads afresh and meets the app's 401.
  const back = appSaysSignedOut(page);
  await page.goBack();
  await expect(page).toHaveURL('/');
  await expectSignedOut(page, back);
});

test('signed in through setup, Sign out clears the site, leaves no cookie and ends the session', async ({
  page,
  invite,
  authenticator,
  baseURL,
}) => {
  // A real session, from S00.04.02's setup with a passkey on the virtual authenticator, as setup.spec.ts makes one.
  await page.goto(invite.setupLink);
  await page.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(page).toHaveURL('/');
  await account(page).click();
  await expect(panel(page).getByText(`Signed in as ${invite.displayName}`, { exact: true })).toBeVisible();
  expect(await page.context().cookies(baseURL)).toContainEqual(
    expect.objectContaining({ name: '__Host-grow2notes' }),
  );

  const logoutAnswer = page.waitForResponse(isLogout);
  const reload = page.waitForRequest((request) => request.isNavigationRequest() && isTo('/')(request));
  const appMe = appSaysSignedOut(page);
  await signOut(page).click();

  const answer = await logoutAnswer;
  expect(answer.status()).toBe(204);
  // Exactly as design.md §6.2 and §8.5 write it.
  expect(await answer.headerValue('Clear-Site-Data')).toBe('"cache", "cookies", "storage"');
  // The browser cleared every cookie for the origin, the antiforgery cookie with the session's, so the reload of /
  // carries none. Checked on that request rather than in the jar afterwards, as the page that it loads may set cookies
  // of its own: sign-in's view A sets its antiforgery and ceremony cookies as it asks for passkey options.
  expect(await (await reload).headerValue('cookie')).toBeNull();
  await expectSignedOut(page, appMe);
  await expect(page).toHaveURL('/');

  // No session cookie since, and the app answers the page's own request for who is signed in with 401.
  expect(await page.context().cookies(baseURL)).not.toContainEqual(
    expect.objectContaining({ name: '__Host-grow2notes' }),
  );
  expect(await page.evaluate(async () => (await fetch('/api/auth/me')).status)).toBe(401);
  // Signing out ends the session only: the passkey stays on the device, for the next sign-in.
  expect(await authenticator.credentials()).toHaveLength(1);
});
