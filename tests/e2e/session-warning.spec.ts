import type { Page, Request, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite, type VirtualAuthenticator } from './fixtures.ts';
import { whereFocusIs } from './focus.ts';
import { boxOf } from './layout.ts';
import { holdNext } from './requests.ts';

// The warning before the idle sign-out in the published app (S00.04.07), against app-shell.md component 6 and its
// Session criteria. Each test signs in for real, through setup with a passkey on the virtual authenticator, so that
// Stay signed in's ping meets a live session. The page's timers and Date run on Playwright's clock, which runs the
// page's 28 minutes in a moment, while the app's own clock runs in real time, so that the session that the page warns
// of is still live in the app (tests/README.md).

const isTo = (path: string) => (message: Request | Response) => new URL(message.url()).pathname === path;
const isPing = isTo('/api/auth/ping');
const isMe = isTo('/api/auth/me');
const isHealthCheck = isTo('/healthz');

// The heading at each step of the countdown, with the typographic apostrophe, and the status line's connection error
// (app-shell.md component 6).
const twoMinutes = 'You’ll be signed out in 2 minutes';
const oneMinute = 'You’ll be signed out in 1 minute';
const fortySeconds = 'You’ll be signed out in 40 seconds';
const twentySeconds = 'You’ll be signed out in 20 seconds';
const noConnection = 'No connection. Try again.';

const warning = (page: Page) => page.locator('dialog');
const heading = (page: Page) => warning(page).getByRole('heading', { level: 2 });
const statusLine = (page: Page) => warning(page).getByRole('status');
const staySignedIn = (page: Page) => warning(page).getByRole('button', { name: 'Stay signed in' });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const todayLink = (page: Page) => nav(page).getByRole('link', { name: 'Today', exact: true });

/**
 * The page's idle time since the newest request that the app accepted, on Playwright's paused clock: `runTo('27:59')`
 * runs the clock on until the page has been idle that long, and `restart()` says that the app has just accepted a
 * request, from which the idle time counts again.
 */
type IdleTime = { runTo: (time: string) => Promise<void>; restart: () => void };

function idleTime(page: Page): IdleTime {
  let idle = 0;
  return {
    runTo: async (time) => {
      const [minutes, seconds] = time.split(':').map(Number);
      const target = (minutes * 60 + seconds) * 1_000;
      await page.clock.runFor(target - idle);
      idle = target;
    },
    restart: () => {
      idle = 0;
    },
  };
}

/**
 * Signs the invited manager in through setup with a passkey on the virtual authenticator, as setup.spec.ts does, with
 * Playwright's clock installed before the page loads, so that the page's timers and `Date` run on it, as the real ones
 * until Today shows. The clock is then paused, so that the page's time moves only as the test runs it on, and the
 * nav's Today link sends a request that the app accepts, `GET /api/auth/me`, from which the page's idle time counts.
 * The link keeps focus, where the warning gives it back as it closes.
 */
async function signInThroughSetup(
  page: Page,
  invite: Invite,
  authenticator: VirtualAuthenticator,
): Promise<IdleTime> {
  await page.clock.install();
  await page.goto(invite.setupLink);
  // The refresh that setup sends as it moves to /, the first 200, has its answer before the clock is paused, so that
  // the Today link's refresh, which would share one still out, is a request of its own.
  const refreshed = page.waitForResponse((response) => isMe(response) && response.status() === 200);
  await page.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await refreshed;
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);

  // A second on, so that the clock is paused ahead of the real time that passes while the test reads it.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  const accepted = page.waitForResponse(isMe);
  await todayLink(page).click();
  expect((await accepted).status()).toBe(200);
  await expect(todayLink(page)).toBeFocused();
  return idleTime(page);
}

/**
 * Records each request that the page sends from now on, but the test's own `GET /healthz`, as its method and path.
 * Gives a function that gives those sent since it was last called, once the browser has reported every request that
 * the page sent before the call: it reports a page's requests in the order that it sends them, so it has reported
 * each once the test has the answer to a request of its own, as start-up.spec.ts counts them.
 */
function recordRequests(page: Page): () => Promise<string[]> {
  const sent: string[] = [];
  page.on('request', (request) => {
    if (!isHealthCheck(request)) {
      sent.push(`${request.method()} ${new URL(request.url()).pathname}`);
    }
  });

  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return sent.splice(0);
  };
}

test('the warning opens at 28:00 over the page, counts down, closes on its ping, and at 30:00 sends nothing', async ({
  page,
  invite,
  authenticator,
}) => {
  const idle = await signInThroughSetup(page, invite, authenticator);
  const sentSince = recordRequests(page);

  // At 28:00 and not before, over the page, with focus on its one button (app-shell.md *28:00 idle on a visible page*).
  await idle.runTo('27:59');
  await expect(warning(page)).toBeHidden();
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  expect(await warning(page).evaluate((dialog) => dialog.matches(':modal'))).toBe(true);
  await expect(warning(page)).toHaveAccessibleName(twoMinutes);
  await expect(heading(page)).toHaveText(twoMinutes);
  await expect(statusLine(page)).toBeEmpty();
  await expect(staySignedIn(page)).toBeFocused();

  // A click on the nav behind it, on Manage, and one on the page behind, away from the nav, each land on its backdrop
  // and change nothing (app-shell.md *Click the backdrop*).
  const manage = await boxOf(nav(page).getByRole('link', { name: 'Manage', exact: true }));
  const bottomLeft = await page.evaluate(() => [8, window.innerHeight - 8]);
  for (const [x, y] of [[manage.x + manage.width / 2, manage.y + manage.height / 2], bottomLeft]) {
    expect(await page.evaluate(([atX, atY]) => document.elementFromPoint(atX, atY)?.localName, [x, y])).toBe('dialog');
    await page.mouse.click(x, y);
    await expect(warning(page)).toBeVisible();
    await expect(heading(page)).toHaveText(twoMinutes);
    await expect(page).toHaveURL('/');
    await expect(page).toHaveTitle('Grow2Notes – Today');
  }
  expect(await sentSince()).toEqual([]);

  // The heading changes at 29:00, 29:20 and 29:40, and not before, with no request sent.
  for (const [before, at, from, to] of [
    ['28:59', '29:00', twoMinutes, oneMinute],
    ['29:19', '29:20', oneMinute, fortySeconds],
    ['29:39', '29:40', fortySeconds, twentySeconds],
  ]) {
    await idle.runTo(before);
    await expect(heading(page)).toHaveText(from);
    await idle.runTo(at);
    await expect(heading(page)).toHaveText(to);
  }
  await expect(warning(page)).toHaveAccessibleName(twentySeconds);
  expect(await sentSince()).toEqual([]);

  // Stay signed in sends the ping, and the warning stays open, its button busy, until the app's answer, 204, which
  // closes it, with focus back where it was (app-shell.md *Press Stay signed in*).
  const heldPing = await holdNext(page, '**/api/auth/ping');
  await staySignedIn(page).click();
  const ping = await heldPing();
  await expect(warning(page)).toBeVisible();
  await expect(staySignedIn(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(staySignedIn(page)).not.toHaveAttribute('disabled');
  const answer = page.waitForResponse(isPing);
  await ping.continue();
  expect((await answer).status()).toBe(204);
  await expect(warning(page)).toBeHidden();
  await expect(todayLink(page)).toBeFocused();
  // One ping, a POST with the antiforgery token that api() fetched first, for the person whom setup signed in.
  expect(await ping.request().headerValue('X-XSRF-TOKEN')).toBeTruthy();
  expect(await sentSince()).toEqual(['GET /api/auth/antiforgery', 'POST /api/auth/ping']);

  // 28 minutes from the ping, the warning opens again, and Escape acts as Stay signed in, with one ping, which the app
  // answers 204 (app-shell.md *Escape or Android back on the warning*).
  idle.restart();
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  await expect(staySignedIn(page)).toBeFocused();
  const escaped = page.waitForResponse(isPing);
  await page.keyboard.press('Escape');
  expect((await escaped).status()).toBe(204);
  await expect(warning(page)).toBeHidden();
  await expect(todayLink(page)).toBeFocused();
  expect(await sentSince()).toEqual(['POST /api/auth/ping']);

  // Left alone, it closes at 30:00, as the session has ended, with no request sent at any mark, as one would extend the
  // session (session-timeout.md *The session clock*). The signed-out view that then shows is S00.04.08's.
  idle.restart();
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  await idle.runTo('29:59');
  await expect(warning(page)).toBeVisible();
  await idle.runTo('30:00');
  await expect(warning(page)).toBeHidden();
  expect(await sentSince()).toEqual([]);
});

test('Tab and Shift+Tab take focus from Stay signed in out of the page and back, never behind the warning', async ({
  page,
  invite,
  authenticator,
}) => {
  const idle = await signInThroughSetup(page, invite, authenticator);
  await idle.runTo('28:00');
  await expect(staySignedIn(page)).toBeFocused();

  // As focus comes back into the page, the window's focus refreshes /api/auth/me (app-shell.md *Window regains focus*),
  // a request that the app accepts, which starts the clock afresh and so closes the warning. The refresh is held until
  // the keys have been pressed, and the clock with it, so that the warning stays open until then.
  const heldRefresh = await holdNext(page, '**/api/auth/me');

  // Stay signed in is the page's only stop, as the page behind is inert: from it, Tab and Shift+Tab each take focus
  // out of the page, to the browser's own controls, and the next brings it back (app-shell.md *Tab order*).
  const stops: string[] = [];
  for (const key of ['Tab', 'Tab', 'Shift+Tab', 'Shift+Tab']) {
    await page.keyboard.press(key);
    stops.push(await whereFocusIs(page));
  }
  expect(stops).toEqual(['outside the page', 'Stay signed in', 'outside the page', 'Stay signed in']);
  await expect(warning(page)).toBeVisible();

  // The refresh's answer, the app's 200, closes it, with focus back where it was.
  const refresh = await heldRefresh();
  const answer = page.waitForResponse(isMe);
  await refresh.continue();
  expect((await answer).status()).toBe(200);
  await expect(warning(page)).toBeHidden();
  await expect(todayLink(page)).toBeFocused();
});

test('with no connection, the open warning says so and counts on, and passes axe with and without its error', async ({
  page,
  invite,
  authenticator,
}) => {
  const idle = await signInThroughSetup(page, invite, authenticator);

  // The page's first change since the sign-in fetches an antiforgery token for the person now signed in, a request
  // that the app accepts, which would close the warning however its ping went. So the first warning's ping, which the
  // app answers, gets the token that the page then holds.
  await idle.runTo('28:00');
  const answered = page.waitForResponse(isPing);
  await staySignedIn(page).click();
  expect((await answered).status()).toBe(204);
  await expect(warning(page)).toBeHidden();

  idle.restart();
  await page.route('**/api/auth/ping', (route) => route.abort('internetdisconnected'));
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  await expect(statusLine(page)).toBeEmpty();

  // axe's own timers run on the page's clock too, so the clock runs as the real one from here on, and the countdown
  // with it.
  await page.clock.resume();
  await expectAxeToPass(page);

  const failed = page.waitForEvent('requestfailed', isPing);
  await staySignedIn(page).click();
  expect(await (await failed).headerValue('X-XSRF-TOKEN')).toBeTruthy();
  await expect(statusLine(page)).toHaveText(noConnection);
  await expect(warning(page)).toBeVisible();
  await expect(staySignedIn(page)).toBeFocused();
  await expect(staySignedIn(page)).not.toHaveAttribute('aria-disabled', 'true');
  await expect(heading(page)).toHaveText(twoMinutes);
  await expectAxeToPass(page);

  // The countdown goes on, with the error still said: a minute on is past 29:00, by the seconds that axe took, and
  // before 29:20.
  await page.clock.runFor('01:00');
  await expect(heading(page)).toHaveText(oneMinute);
  await expect(statusLine(page)).toHaveText(noConnection);
  await expect(warning(page)).toBeVisible();
});

test('at 320 px with text at 200%, the warning wraps and scrolls inside itself, with no sideways scroll', async ({
  page,
  invite,
  authenticator,
}) => {
  // A small phone's window.
  await page.setViewportSize({ width: 320, height: 568 });
  // Text at 200%: Chromium's default text size at 32 px, where it is 16 (S00.05.01's Notes). The DevTools session stays
  // attached, as detaching one takes the page's virtual authenticator with it (setup.spec.ts).
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  const idle = await signInThroughSetup(page, invite, authenticator);
  await expect(page.locator('body')).toHaveCSS('font-size', '36px');

  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  const { headingLines, ...fit } = await warning(page).evaluate((dialog) => {
    const box = dialog.getBoundingClientRect();
    const button = dialog.querySelector('button')?.getBoundingClientRect();
    const sentence = document.createRange();
    sentence.selectNodeContents(dialog.querySelector('h2') ?? dialog);
    return {
      headingLines: new Set([...sentence.getClientRects()].map(({ top }) => Math.round(top))).size,
      inWindow: box.left >= 0 && box.top >= 0 && box.right <= window.innerWidth && box.bottom <= window.innerHeight,
      buttonInside: button !== undefined && button.left >= box.left && button.right <= box.right,
      scrollsInside: dialog.scrollHeight > dialog.clientHeight,
      scrollsSideways: dialog.scrollWidth > dialog.clientWidth,
      pageScrollsSideways: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    };
  });
  expect(headingLines, 'lines of the heading').toBeGreaterThan(1);
  expect(fit).toEqual({
    inWindow: true,
    buttonInside: true,
    scrollsInside: true,
    scrollsSideways: false,
    pageScrollsSideways: false,
  });
  // Focused as the warning opens, Stay signed in is scrolled into view inside it, whole.
  await expect(staySignedIn(page)).toBeInViewport({ ratio: 1 });
});
