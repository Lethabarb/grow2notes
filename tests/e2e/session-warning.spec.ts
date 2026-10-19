import type { Page, Request, Response, Route } from '@playwright/test';
import { addJaneWithTwoGoals, janeCitizen } from './addParticipant.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite, type VirtualAuthenticator } from './fixtures.ts';
import { whereFocusIs } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import { boxOf } from './layout.ts';
import { isDraftSaveRequest } from './notes.ts';
import { holdNext } from './requests.ts';

// The warning before the idle sign-out in the published app (S00.04.07, and S02.02.05 on the note form), against
// app-shell.md component 6 and its Session criteria. Each test signs in for real, through setup with a passkey on the
// virtual authenticator, so that Stay signed in's ping meets a live session. The page's timers and Date run on
// Playwright's clock, which runs the page's 28 minutes in a moment, while the app's own clock runs in real time, so
// that the session that the page warns of is still live in the app (tests/README.md).

const isTo = (path: string) => (message: Request | Response) => new URL(message.url()).pathname === path;
const isPing = isTo('/api/auth/ping');
const isMe = isTo('/api/auth/me');
const isHealthCheck = isTo('/healthz');
/** Whether the response answers a save of a note's working copy, `PUT {base}/draft`. */
const isDraftSave = (response: Response) => isDraftSaveRequest(response.request());

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
 * until Today shows.
 */
async function signInOnTheClock(page: Page, invite: Invite, authenticator: VirtualAuthenticator) {
  await page.clock.install();
  await page.goto(invite.setupLink);
  // The refresh that setup sends as it moves to /, the first 200, has its answer before the clock is paused, so that
  // the Today link's refresh, which would share one still out, is a request of its own.
  const refreshed = page.waitForResponse((response) => isMe(response) && response.status() === 200);
  await page.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await refreshed;
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);
}

/** Pauses the clock a second on, so that it is paused ahead of the real time that passes while the test reads it. */
async function pauseTheClock(page: Page) {
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
}

/**
 * Signs in on the clock as `signInOnTheClock` does, then pauses it, so that the page's time moves only as the test runs
 * it on, and the nav's Today link sends a request that the app accepts, `GET /api/auth/me`, from which the page's idle
 * time counts. The link keeps focus, where the warning gives it back as it closes.
 */
async function signInThroughSetup(
  page: Page,
  invite: Invite,
  authenticator: VirtualAuthenticator,
): Promise<IdleTime> {
  await signInOnTheClock(page, invite, authenticator);
  await pauseTheClock(page);
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

test('warns from 28:00 to its ping; at 30:00 view C shows, sending nothing, until a live session’s refresh', async ({
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
  // session (session-timeout.md *The session clock*), and the signed-out view shows in place of Today, with focus on
  // its <h1> (app-shell.md *Session*).
  idle.restart();
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  await idle.runTo('29:59');
  await expect(warning(page)).toBeVisible();
  expect(await sentSince()).toEqual([]);
  await idle.runTo('30:00');
  await expect(warning(page)).toBeHidden();
  const pageHeading = page.getByRole('main').getByRole('heading', { level: 1 });
  await expect(pageHeading).toHaveText('You’ve been signed out');
  await expect(pageHeading).toBeFocused();
  await expect(page).toHaveTitle('Grow2Notes – Sign in');
  await expect(page).toHaveURL('/');
  await expect(nav(page)).toHaveCount(0);
  expect(await sentSince()).toEqual([]);

  // It asks for passkey options, as sign-in does, but only once the app has surely ended the session, 30:05 after the
  // ping's answer came by the page's clock, which is paused, so that the ping went and came at the same moment: a
  // request with the session cookie before then could reach the app inside its 30 minutes and renew them (A61).
  await idle.runTo('30:04');
  expect(await sentSince()).toEqual([]);
  await idle.runTo('30:05');
  expect(await sentSince()).toEqual(['POST /api/auth/passkey/options']);

  // The app's own clock has barely moved, so the session is alive, as when another tab kept it: the window's focus
  // refreshes /api/auth/me, whose 200 brings Today back as it was, with focus on its <h1> (session-timeout.md
  // *Behaviour* 5). The hidden Today fetches its list again with it, in either order (today.md *Refresh*).
  const refreshed = page.waitForResponse(isMe);
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  expect((await refreshed).status()).toBe(200);
  await expect(pageHeading).toContainText('Today');
  await expect(pageHeading).toBeFocused();
  await expect(page).toHaveTitle('Grow2Notes – Today');
  await expect(todayLink(page)).toBeVisible();
  expect((await sentSince()).sort()).toEqual(['GET /api/auth/me', 'GET /api/today']);
});

test('Tab and Shift+Tab take focus from Stay signed in out of the page and back, never behind the warning', async ({
  page,
  invite,
  authenticator,
}) => {
  const idle = await signInThroughSetup(page, invite, authenticator);
  await idle.runTo('28:00');
  await expect(staySignedIn(page)).toBeFocused();

  // As focus comes back into the page, the window's focus refreshes /api/auth/me (app-shell.md *Window regains focus*)
  // and fetches Today's list again behind the warning (today.md *Refresh*), requests that the app accepts, whose
  // answers start the clock afresh and so close the warning. Both are held until the keys have been pressed, and the
  // clock with them, so that the warning stays open until then. The list's route stays until the end, as Playwright
  // lets every held request go once no route is left.
  const heldRefresh = await holdNext(page, '**/api/auth/me');
  const heldLists: Route[] = [];
  await page.route('**/api/today', (route) => {
    heldLists.push(route);
  });

  // Stay signed in is the page's only stop, as the page behind is inert: from it, Tab and Shift+Tab each take focus
  // out of the page, to the browser's own controls, and the next brings it back (app-shell.md *Tab order*).
  const stops: string[] = [];
  for (const key of ['Tab', 'Tab', 'Shift+Tab', 'Shift+Tab']) {
    await page.keyboard.press(key);
    stops.push(await whereFocusIs(page));
  }
  expect(stops).toEqual(['outside the page', 'Stay signed in', 'outside the page', 'Stay signed in']);
  await expect(warning(page)).toBeVisible();

  // Their answers, the app's 200s, close it, as the clock decides once no request is out, with focus back where it was.
  // The second focus shared the first's requests.
  const refresh = await heldRefresh();
  await expect.poll(() => heldLists.length).toBe(1);
  const answers = Promise.all([page.waitForResponse(isMe), page.waitForResponse(isTo('/api/today'))]);
  await refresh.continue();
  await heldLists[0].continue();
  expect((await answers).map((answer) => answer.status())).toEqual([200, 200]);
  await expect(warning(page)).toBeHidden();
  await expect(todayLink(page)).toBeFocused();
  expect(heldLists).toHaveLength(1);
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

test('on the note form, a change made at 27:59 goes at 28:00 in place of the warning, which stays closed', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInOnTheClock(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  await page.getByRole('main').locator('a[data-return-key]', { hasText: janeCitizen.name }).click();
  const box = page.getByRole('main').getByRole('textbox', { name: '3. Guided notes' });
  await expect(box).toBeVisible();

  // On the paused clock, the note's first save goes as its 2 s pass, and the app accepts it: the idle time counts
  // from then.
  await pauseTheClock(page);
  await box.pressSequentially('Went to the shops.');
  const started = page.waitForResponse(isDraftSave);
  await page.clock.runFor(2_000);
  expect((await started).status()).toBe(201);
  const idle = idleTime(page);
  const sentSince = recordRequests(page);

  // A change at 27:59, whose own save would be due at 28:01.
  await idle.runTo('27:59');
  await expect(warning(page)).toBeHidden();
  await box.pressSequentially(' Bought bread.');
  expect(await sentSince()).toEqual([]);

  // At 28:00 the change goes in place of the warning, and the app's answer starts the idle time afresh, so no warning
  // opens (app-shell.md *28:00 idle on a visible page*; session-timeout.md *The session clock* step 3).
  const flushed = page.waitForResponse(isDraftSave);
  await idle.runTo('28:00');
  const answer = await flushed;
  expect(answer.status()).toBe(200);
  expect(answer.request().postDataJSON()).toMatchObject({ narrative: 'Went to the shops. Bought bread.' });
  await expect(warning(page)).toBeHidden();
  expect(await sentSince()).toEqual([`PUT ${new URL(answer.url()).pathname}`]);

  // Nothing more goes when the change's own save was due, and the warning opens only 28 minutes after the save.
  idle.restart();
  await idle.runTo('00:02');
  expect(await sentSince()).toEqual([]);
  await idle.runTo('27:59');
  await expect(warning(page)).toBeHidden();
  await idle.runTo('28:00');
  await expect(warning(page)).toBeVisible();
  await expect(heading(page)).toHaveText(twoMinutes);
  expect(await sentSince()).toEqual([]);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops');
});
