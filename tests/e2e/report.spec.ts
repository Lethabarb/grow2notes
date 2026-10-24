import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, expectStops } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import { boxOf, scrollsSideways, setTextTo200Percent } from './layout.ts';
import { answerMe, manager, serveMe, worker } from './me.ts';
import {
  enterDate,
  isExport,
  recordExports,
  reportAddress,
  reportDay,
  serveHasNotes,
  serveNoNotes,
  serveReport,
} from './report.ts';
import { holdNext, recordRequests } from './requests.ts';

// The Report screen in the published app, against daily-report.md's criteria: its date field and steps, the has-notes
// check, the download buttons, their keyboard, layout and failure lines, and the history of a day's address. The
// made-up manager's session, the has-notes check and the export are answered in the browser (me.ts, report.ts), so
// every test registers serveHasNotes before it navigates; the download itself is report-download.spec.ts's, in three
// browsers.

const previousDayAddress = '/reports/daily/2026-09-30';
const noNotes = /^No submitted notes for Thursday 1\sOctober 2026\.$/;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const button = (page: Page, name: string) => main(page).getByRole('button', { name, exact: true });
// The steps' glyphs are hidden from the accessible name, which is the words alone; the tab stops read the text shown.
const previousDay = (page: Page) => main(page).getByRole('link', { name: 'Previous day', exact: true });
const nextDay = (page: Page) => main(page).getByRole('link', { name: 'Next day', exact: true });
const dateField = (page: Page) => main(page).getByLabel('Date', { exact: true });
const dateStatus = (page: Page) => page.locator('#report-date-status');
const fieldError = (page: Page) => page.locator('#report-date-error');
const alertLine = (page: Page) => main(page).getByRole('alert');
// The status under the failure line: the second of the page's three (the date's, this one, and the page's own).
const downloadStatus = (page: Page) => main(page).getByRole('status').nth(1);
const navReport = (page: Page) =>
  page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Report', exact: true });

/** The made-up manager on the Report screen at `address`, with a has-notes answer for every day but `emptyDays`. */
async function open(page: Page, address = reportAddress, emptyDays: readonly string[] = []) {
  await serveMe(page, manager);
  await serveHasNotes(page, emptyDays);
  await serveReport(page);
  await page.goto(address);
  await expect(heading(page)).toHaveText('Daily report');
}

test('S1: the title, the nav item, the field with its value and latest date, and axe at 375 and 1280 px', async ({
  page,
}) => {
  await open(page, previousDayAddress);

  await expect(page).toHaveTitle('Grow2Notes – Daily report');
  await expect(navReport(page)).toHaveAttribute('aria-current', 'page');
  await expect(dateField(page)).toHaveValue('2026-09-30');
  await expect(dateField(page)).toHaveAttribute('max', reportDay);
  await expect(dateStatus(page)).toHaveText(/^Wednesday 30\sSeptember 2026$/);
  await expect(previousDay(page)).toHaveAttribute('href', '/reports/daily/2026-09-29');
  await expect(nextDay(page)).toHaveAttribute('href', reportAddress);
  await expect(main(page).getByRole('button')).toHaveText(['Download Word', 'Download PDF']);
  await expect(alertLine(page)).toBeEmpty();
  await expectAxeToPass(page);
});

test('the steps share a row at 375 px and stack at 320 px, and nothing scrolls sideways at 320 px or 200%', async ({
  page,
}) => {
  await open(page, previousDayAddress);

  await page.setViewportSize({ width: 375, height: 800 });
  const [previousAt375, nextAt375] = [await boxOf(previousDay(page)), await boxOf(nextDay(page))];
  expect(Math.abs(previousAt375.y - nextAt375.y)).toBeLessThan(2);
  expect(nextAt375.x).toBeGreaterThan(previousAt375.x);
  expect(await scrollsSideways(page)).toBe(false);

  await page.setViewportSize({ width: 320, height: 800 });
  const [previousAt320, nextAt320] = [await boxOf(previousDay(page)), await boxOf(nextDay(page))];
  expect(nextAt320.y).toBeGreaterThanOrEqual(previousAt320.y + previousAt320.height);
  expect(await scrollsSideways(page)).toBe(false);

  await setTextTo200Percent(page);
  expect(await scrollsSideways(page)).toBe(false);
});

test('S2: on today, Next day is a link with no address, dashed, and axe passes', async ({ page }) => {
  await open(page);

  await expect(nextDay(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(nextDay(page)).not.toHaveAttribute('href');
  await expect(nextDay(page)).toHaveCSS('border-top-style', 'dashed');
  await expect(dateStatus(page)).toHaveText(/^Thursday 1\sOctober 2026$/);
  await expectAxeToPass(page);
});

test('S4: a day with no submitted notes says so, and both buttons are unavailable, focusable and send nothing', async ({
  page,
}) => {
  const exports = recordExports(page);
  await open(page, reportAddress, [reportDay]);

  await expect(dateStatus(page)).toHaveText(noNotes);
  for (const name of ['Download Word', 'Download PDF']) {
    await expect(button(page, name)).toHaveAttribute('aria-disabled', 'true');
    await expect(button(page, name)).toHaveCSS('border-top-style', 'dashed');
    await button(page, name).focus();
    await expect(button(page, name)).toBeFocused();
    await button(page, name).click({ force: true });
  }

  expect(exports).toEqual([]);
  await expectAxeToPass(page);
});

test('S14: a day that turns out to have no notes shows the same, with no failure line, and focus stays', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveHasNotes(page);
  await serveNoNotes(page);
  await page.goto(reportAddress);

  await button(page, 'Download PDF').click();

  await expect(dateStatus(page)).toHaveText(noNotes);
  await expect(alertLine(page)).toBeEmpty();
  await expect(downloadStatus(page)).toBeEmpty();
  await expect(button(page, 'Download PDF')).toHaveAttribute('aria-disabled', 'true');
  await expect(button(page, 'Download PDF')).toBeFocused();
  await expectAxeToPass(page);
  await expectNothingKept(page);
});

test('S7: an emptied field says "Enter a date", a press focuses it and sends nothing, and axe passes', async ({
  page,
}) => {
  const exports = recordExports(page);
  await open(page);

  await enterDate(dateField(page), '');
  await dateField(page).blur();

  await expect(fieldError(page)).toHaveText(/Enter a date$/);
  await expect(dateField(page)).toHaveAttribute('aria-invalid', 'true');
  await expectAxeToPass(page);

  await button(page, 'Download Word').click({ force: true });

  await expect(dateField(page)).toBeFocused();
  expect(exports).toEqual([]);
});

test('a future date and Enter shows the error and sends no request; Previous day clears it', async ({ page }) => {
  const sent = recordRequests(page);
  await open(page);
  const checks = async () => (await sent()).filter((request) => request.includes('/has-notes'));
  expect(await checks()).toHaveLength(1);

  await enterDate(dateField(page), '2026-10-05');
  await page.keyboard.press('Enter');

  await expect(fieldError(page)).toHaveText(/Date must be today or in the past$/);
  await expect(page).toHaveURL(reportAddress);
  expect(await checks()).toHaveLength(1);

  await previousDay(page).click();

  await expect(page).toHaveURL(previousDayAddress);
  await expect(fieldError(page)).toHaveCount(0);
  await expect(dateField(page)).toHaveValue('2026-09-30');
});

test('S10: a slow download is quiet for 399 ms, then says "Downloading…" and that the file is being prepared', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await open(page);
  const heldExport = await holdNext(page, isExport);

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await button(page, 'Download Word').click();
  const route = await heldExport();
  await page.clock.runFor(399);
  await expect(button(page, 'Download Word')).toHaveAttribute('aria-disabled', 'true');
  await expect(downloadStatus(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(button(page, 'Downloading…')).toBeVisible();
  await expect(downloadStatus(page)).toHaveText('Preparing the Word file… Keep this page open.');
  await page.clock.resume();
  await expectAxeToPass(page);

  const download = page.waitForEvent('download');
  await route.fallback();

  expect((await download).suggestedFilename()).toBe('daily-notes_2026-10-01.docx');
  await expect(downloadStatus(page)).toHaveText('daily-notes_2026-10-01.docx is ready. Look in your downloads.');
  await expect(button(page, 'Download Word')).not.toHaveAttribute('aria-disabled');
});

test('S11: no connection says so above the buttons, focus stays, and axe passes', async ({ page }) => {
  await open(page);
  await page.route(isExport, (route) => route.abort('internetdisconnected'));

  await button(page, 'Download PDF').click();

  await expect(alertLine(page)).toHaveText('Not downloaded: no connection. Try again.');
  await expect(button(page, 'Download PDF')).toBeFocused();
  await expect(button(page, 'Download PDF')).not.toHaveAttribute('aria-disabled');
  await expectAxeToPass(page);
  await expectNothingKept(page);
});

test('keyboard: Tab goes from the field through both steps to Download Word and Download PDF', async ({ page }) => {
  await open(page);

  await heading(page).focus();
  await expectStops(page, 'Tab', ['Date']);
  // The browser's date field has a stop for each part of the date, which stay one element, so the number of presses
  // is the browser's.
  for (let presses = 0; presses < 8; presses += 1) {
    if (await previousDay(page).evaluate((link) => link === document.activeElement)) {
      break;
    }

    await page.keyboard.press('Tab');
  }

  await expectFocusAt(page, '‹ Previous day');
  await expectStops(page, 'Tab', ['Next day ›', 'Download Word', 'Download PDF']);
  await expectStops(page, 'Shift+Tab', ['Download Word']);
});

test('keyboard: Enter on the unavailable Next day does nothing, and on Download Word saves the file and stays', async ({
  page,
}) => {
  await open(page);
  await nextDay(page).focus();

  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(reportAddress);
  await expect(nextDay(page)).toBeFocused();

  await button(page, 'Download Word').focus();
  const download = page.waitForEvent('download');
  await page.keyboard.press('Enter');

  expect((await download).suggestedFilename()).toBe('daily-notes_2026-10-01.docx');
  await expect(button(page, 'Download Word')).toBeFocused();
  await expectNothingKept(page);
});

test('keyboard: Enter on Next day onto today keeps focus on it, now unavailable', async ({ page }) => {
  await open(page, previousDayAddress);
  await nextDay(page).focus();

  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(reportAddress);
  await expect(nextDay(page)).toBeFocused();
  await expect(nextDay(page)).toHaveAttribute('aria-disabled', 'true');
});

test('history: Previous day twice, then Back keeps focus and scroll, and the last Back leaves the screen', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 240 });
  await serveMe(page, manager);
  await serveHasNotes(page);
  await page.goto('/');
  await navReport(page).click();
  await expect(page).toHaveURL(reportAddress);
  // The shell's arrival, its focus on the <h1>, is over before the page is scrolled.
  await expect(heading(page)).toBeFocused();
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  await page.evaluate(() => window.scrollTo(0, 80));
  expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(0);

  await previousDay(page).click();
  await expect(page).toHaveURL(previousDayAddress);
  await previousDay(page).click();
  await expect(page).toHaveURL('/reports/daily/2026-09-29');
  const kept = await page.evaluate(() => window.scrollY);
  expect(kept).toBeGreaterThan(0);

  for (const address of [previousDayAddress, reportAddress]) {
    await page.goBack();
    await expect(page).toHaveURL(address);
    await expect(previousDay(page)).toBeFocused();
    expect(await page.evaluate(() => window.scrollY), address).toBe(kept);
  }

  await page.goBack();
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toContainText('Today');
  await expectNothingKept(page);
});

test('history: one visit to the field adds one entry, however many dates are typed in it', async ({ page }) => {
  await serveMe(page, manager);
  await serveHasNotes(page);
  await page.goto('/');
  await navReport(page).click();
  await expect(page).toHaveURL(reportAddress);
  // The shell's arrival focus on the <h1> comes first: it would take focus from the field, and end the visit.
  await expect(heading(page)).toBeFocused();

  await enterDate(dateField(page), '2026-09-20');
  await expect(page).toHaveURL('/reports/daily/2026-09-20');
  await enterDate(dateField(page), '2026-09-21');
  await expect(page).toHaveURL('/reports/daily/2026-09-21');
  await expect(dateStatus(page)).toHaveText(/^Monday 21\sSeptember 2026$/);

  await page.goBack();
  await expect(page).toHaveURL(reportAddress);
  await page.goBack();
  await expect(page).toHaveURL('/');
  await expectNothingKept(page);
});

test('arriving: no date and a later date are replaced by today, and a date that is not real is Page not found', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveHasNotes(page);

  for (const address of ['/reports/daily', '/reports/daily/2026-10-09']) {
    await page.goto('/');
    await page.goto(address);
    await expect(page).toHaveURL(reportAddress);
    await expect(heading(page)).toHaveText('Daily report');
    await page.goBack();
    await expect(page).toHaveURL('/');
  }

  for (const address of ['/reports/daily/2026-02-29', '/reports/daily/2026-13-45']) {
    await page.goto(address);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle('Grow2Notes – Page not found');
  }
});

test.describe('on a device in another zone with the wrong date', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test("the nav's Report opens the server's today, not the device's", async ({ page }) => {
    await page.clock.setFixedTime(new Date('2030-01-01T00:00:00Z'));
    await serveMe(page, manager);
    await serveHasNotes(page);
    await page.goto('/');

    await navReport(page).click();

    await expect(page).toHaveURL(reportAddress);
    await expect(dateField(page)).toHaveValue(reportDay);
    await expect(dateField(page)).toHaveAttribute('max', reportDay);
  });
});

test('S20: when /me says it is a new day, Next day gains its address and the page stays put', async ({ page }) => {
  await open(page);
  await expect(nextDay(page)).not.toHaveAttribute('href');

  await answerMe(page, { ...manager, today: '2026-10-02' });
  await page.evaluate(() => window.dispatchEvent(new Event('focus')));

  await expect(nextDay(page)).toHaveAttribute('href', '/reports/daily/2026-10-02');
  await expect(page).toHaveURL(reportAddress);
  await expect(dateField(page)).toHaveValue(reportDay);
});

test('a worker sees Page not found at every report address, and no has-notes or export request is sent', async ({
  page,
}) => {
  await serveMe(page, worker);
  await serveHasNotes(page);
  await serveReport(page);
  const sent = recordRequests(page);
  const exports = recordExports(page);

  for (const address of [reportAddress, '/reports/daily']) {
    await page.goto(address);

    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle('Grow2Notes – Page not found');
    await expect(main(page).getByRole('button', { name: /^Download/ })).toHaveCount(0);
  }

  expect((await sent()).filter((request) => /\/(export|has-notes)/.test(request))).toEqual([]);
  expect(exports).toEqual([]);
});
