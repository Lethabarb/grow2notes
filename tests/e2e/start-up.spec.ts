import type { Locator, Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { answerMe, failMe, holdMe, manager, serveMe, worker } from './me.ts';
import { notesStore } from './notes.ts';
import { serveHasNotes } from './report.ts';
import { serveReviews } from './reviews.ts';

// The start-up's loading line and messages, the refresh of /api/auth/me and the crash pages in the published app
// (S00.05.03), against app-shell.md component 4 and *Refresh of `/api/auth/me`*. Each test answers, fails or holds
// /api/auth/me in the browser (me.ts), and the one that opens Flagged its To review list (reviews.ts), so none of them
// reaches the app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

// tokens.css's --colour-text, the body text's colour, which every message in the shell keeps: none is red (app-shell.md
// component 4).
const textColour = 'rgb(11, 12, 12)';

const heading = (page: Page) => page.getByRole('main').getByRole('heading', { level: 1 });
// The start-up's one status region, its line in <main> (app-shell.md *Live regions*).
const statusLine = (page: Page) => page.getByRole('main').getByRole('status');
const tryAgain = (page: Page) => page.getByRole('main').getByRole('button', { name: 'Try again' });
const savedIsKept = (page: Page) => page.getByRole('main').getByText('Anything already saved is kept.');

async function expectInTextColour(locators: Locator[]) {
  for (const locator of locators) {
    await expect(locator).toHaveCSS('color', textColour);
  }
}

test('with no connection, Could not connect shows within 2 s, Try again says still, then brings Today', async ({
  page,
}) => {
  await failMe(page, 'no connection');
  await page.goto('/');

  // Within 2 s of the page loading, as the request and its one silent retry, 1 s later, each fail at once.
  await expect(statusLine(page)).toHaveText('Grow2Notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Could not connect to Grow2Notes');
  await expect(page).toHaveTitle(title('Could not connect'));
  // A first failure moves no focus (app-shell.md *States*).
  await expect(page.locator('body')).toBeFocused();
  await expectInTextColour([heading(page), statusLine(page)]);
  await expectAxeToPass(page);

  await tryAgain(page).click();
  await expect(statusLine(page)).toHaveText('Grow2Notes still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();

  await serveMe(page, worker);
  await tryAgain(page).click();
  // The button has gone with the message, so focus goes to the new <h1> (app-shell.md *Accessibility checklist*).
  await expect(heading(page)).toContainText('Today');
  await expect(heading(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Today'));
});

test('with a 503, There is a problem shows with its sentence and title', async ({ page }) => {
  await failMe(page, 503);
  await page.goto('/');

  await expect(statusLine(page)).toHaveText('Grow2Notes did not load: something went wrong. Try again.');
  await expect(heading(page)).toHaveText('There is a problem with Grow2Notes');
  await expect(page).toHaveTitle(title('There is a problem'));
  await expectInTextColour([heading(page), statusLine(page)]);
  await expectAxeToPass(page);
});

test('with /api/auth/me held for 2 s, Loading… shows in the status line and goes with the answer', async ({
  page,
}) => {
  await holdMe(page, worker, 2_000);
  await page.goto('/');

  // From 1 s (empty-loading-error.md *Timing*).
  await expect(statusLine(page)).toHaveText('Loading…');
  await expect(page).toHaveTitle('Grow2Notes');

  await expect(heading(page)).toContainText('Today');
  await expect(page.getByRole('main')).not.toContainText('Loading…');
});

test('with the script held back, the boot line is hidden at first and shows by 2 s', async ({ page }) => {
  await serveMe(page, worker);
  let releaseScript = () => {};
  const scriptReleased = new Promise<void>((resolve) => {
    releaseScript = resolve;
  });
  await page.route('**/assets/*.js', async (route) => {
    await scriptReleased;
    await route.continue();
  });
  // Not waiting for DOMContentLoaded, which waits for the module script, nor for the load event.
  await page.goto('/', { waitUntil: 'commit' });

  const boot = page.locator('#root > p.boot');
  // Once the stylesheet has given the line its rule, from which it waits 1 s (base.css).
  await expect(boot).toHaveCSS('animation-delay', '1s');
  await expect(boot).toBeHidden();
  await expect(boot).toBeVisible({ timeout: 2_000 });
  await expect(boot).toHaveText('Loading…');

  // React replaces it as the app mounts.
  releaseScript();
  await expect(heading(page)).toContainText('Today');
  await expect(boot).toHaveCount(0);
});

test.describe('with JavaScript off', () => {
  test.use({ javaScriptEnabled: false });

  test('the <noscript> line shows, titled Grow2Notes, and the boot line does not', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveTitle('Grow2Notes');
    // getByText does not find the <noscript> line, though it shows (S00.05.03's Notes).
    const noScript = page.locator('noscript > p');
    await expect(noScript).toBeVisible();
    await expect(noScript).toHaveText(
      'Grow2Notes needs JavaScript. Turn it on in your browser settings, then reload this page.',
    );
    // Hidden by @media (scripting: none) for good, not only for the 1 s that its animation waits.
    await expect(page.locator('p.boot')).toHaveCSS('display', 'none');
  });
});

test('a today that Today cannot read shows the crash page under the nav; Go to Today loads / afresh', async ({
  page,
}) => {
  // A number where the API gives a date, which Today's heading cannot read, so that the screen throws as it renders.
  await answerMe(page, { ...worker, today: 20261001 });
  await page.goto('/');

  await expect(heading(page)).toHaveText('There is a problem with Grow2Notes');
  await expect(savedIsKept(page)).toBeVisible();
  await expect(page).toHaveTitle(title('There is a problem'));
  // In <main>, under the nav, which stays (app-shell.md component 4).
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Account' })).toBeVisible();
  await expect(page.locator('main')).toHaveCount(1);
  await expectInTextColour([heading(page), savedIsKept(page)]);
  await expectAxeToPass(page);

  // A plain link, not the router's, so the page loads afresh, with nothing of what memory held: the marker goes too.
  const goToToday = page.getByRole('main').getByRole('link', { name: 'Go to Today' });
  await expect(goToToday).toHaveAttribute('href', '/');
  await page.evaluate(() => Reflect.set(window, 'beforeGoToToday', true));
  await serveMe(page, worker);
  const loaded = page.waitForEvent('load');
  await goToToday.click();
  await loaded;

  await expect(page).toHaveURL('/');
  await expect(heading(page)).toContainText('Today');
  expect(await page.evaluate(() => Reflect.has(window, 'beforeGoToToday'))).toBe(false);
});

test("a null answer, which the header cannot read, shows the shell's crash page with no nav", async ({ page }) => {
  await answerMe(page, null);
  await page.goto('/');

  await expect(heading(page)).toHaveText('There is a problem with Grow2Notes');
  await expect(savedIsKept(page)).toBeVisible();
  await expect(page).toHaveTitle(title('There is a problem'));
  // The skip link, the header with the wordmark as text, and <main>, with no nav or Account (app-shell.md component 4).
  await expect(page.locator('#root > a')).toHaveText('Skip to main content');
  const header = page.getByRole('banner');
  await expect(header).toHaveText('Grow2Notes');
  await expect(header.getByRole('link')).toHaveCount(0);
  await expect(page.locator('nav')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Account' })).toHaveCount(0);
  await expect(page.locator('main')).toHaveCount(1);
  await expectInTextColour([heading(page), savedIsKept(page)]);
  await expectAxeToPass(page);
});

test('/api/auth/me is asked once at start-up, per nav link and per focus, and never when left alone', async ({
  page,
}) => {
  let sent = 0;
  page.on('request', (request) => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/auth/me') {
      sent += 1;
    }
  });

  /**
   * Expects `count` requests for `/api/auth/me` in all: once the last step's has been counted, and again once the test
   * has the answer to a request of its own, to the app's liveness endpoint, by which any other that the page had sent
   * has been reported too, as the browser reports a page's requests in the order that it sends them.
   */
  async function expectSent(count: number) {
    await expect.poll(() => sent).toBe(count);
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    expect(sent).toBe(count);
  }

  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await serveMe(page, manager);
  await serveReviews(page, notesStore({ today: manager.today, participants: [] }));
  await serveHasNotes(page);
  await page.goto('/');
  await expect(heading(page)).toContainText('Today');
  await expectSent(1);

  // Each of the manager's nav links, Today's first, to the address already open (app-shell.md *Refresh of
  // `/api/auth/me`*: every location.key change).
  const nav = page.getByRole('navigation', { name: 'Main' });
  const steps: [link: string, pageName: string][] = [
    ['Today', 'Today'],
    ['Flagged', 'Flagged notes'],
    ['Report', 'Daily report'],
    ['Manage', 'Manage'],
  ];
  let count = 1;
  for (const [link, pageName] of steps) {
    await nav.getByRole('link', { name: link, exact: true }).click();
    await expect(page).toHaveTitle(title(pageName));
    count += 1;
    await expectSent(count);
  }

  // Together, as when a person comes back to the tab, they send one request, as the second shares the first's.
  const visibility = await page.evaluate(() => {
    window.dispatchEvent(new Event('focus'));
    document.dispatchEvent(new Event('visibilitychange'));
    return document.visibilityState;
  });
  expect(visibility).toBe('visible');
  count += 1;
  await expectSent(count);

  // No timer asks again (design.md §6.8).
  await page.clock.runFor('30:00');
  await expectSent(count);
});
