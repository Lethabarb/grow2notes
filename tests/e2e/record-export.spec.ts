import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, expectStops, ring } from './focus.ts';
import { expectFoundationsToPass } from './foundations.ts';
import { expectNothingKept } from './kept.ts';
import { expectAtLeast, scrollsSideways, setTextTo200Percent, systemColour } from './layout.ts';
import { manager, serveMe, worker } from './me.ts';
import {
  defaultQuery,
  exportAddress,
  exportDay,
  fileNameOf,
  firstNoteDate,
  isExport,
  recordExports,
  serveNoNotes,
  serveParticipant,
  serveRecord,
} from './recordExport.ts';
import { enterDate } from './report.ts';
import { holdNext, recordRequests } from './requests.ts';

// The Export record screen in the published app, against record-export.md's criteria: its defaults, checks, request,
// results and failures, its keyboard, layout and colours. The made-up manager's session, the participant and the
// export are answered in the browser (me.ts, recordExport.ts); the save step in three browsers is
// record-export-download.spec.ts's.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const fromField = (page: Page) => main(page).getByLabel('From date', { exact: true });
const toField = (page: Page) => main(page).getByLabel('To date', { exact: true });
const pdf = (page: Page) => main(page).getByRole('radio', { name: 'PDF', exact: true });
const word = (page: Page) => main(page).getByRole('radio', { name: 'Word', exact: true });
const history = (page: Page) => main(page).getByRole('checkbox', { name: 'Include earlier versions of edited notes' });
const exportButton = (page: Page) => main(page).getByRole('button', { name: 'Export', exact: true });
const busyButton = (page: Page) => main(page).getByRole('button', { name: 'Exporting…', exact: true });
const alertLine = (page: Page) => page.locator('#export-alert');
const statusLine = (page: Page) => page.locator('#export-status');
const summary = (page: Page) => main(page).getByRole('heading', { name: 'There is a problem' }).locator('..');
const navManage = (page: Page) =>
  page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true });

const readyWords = (format: 'docx' | 'pdf') =>
  `participant-record_${firstNoteDate}_to_${exportDay}.${format} is ready. Look in your downloads.`;

/** The made-up manager on the Export record screen, with the participant and the export answered in the browser. */
async function open(page: Page, header: Parameters<typeof serveParticipant>[1] = {}) {
  await serveMe(page, manager);
  await serveParticipant(page, header);
  await serveRecord(page);
  await page.goto(exportAddress);
  await expect(heading(page)).toHaveText('Export record');
}

test('the title, Manage as current, the defaults, and axe at 375 and 1280 px', async ({ page }) => {
  await open(page);

  await expect(page).toHaveTitle('Grow2Notes – Export record');
  await expect(navManage(page)).toHaveAttribute('aria-current', 'true');
  await expect(main(page).getByRole('definition')).toHaveText('Jane Citizen');
  await expect(fromField(page)).toHaveValue(firstNoteDate);
  await expect(toField(page)).toHaveValue(exportDay);
  await expect(fromField(page)).toHaveAttribute('max', exportDay);
  await expect(toField(page)).toHaveAttribute('max', exportDay);
  await expect(toField(page)).toHaveAttribute('min', firstNoteDate);
  await expect(pdf(page)).not.toBeChecked();
  await expect(word(page)).not.toBeChecked();
  await expect(history(page)).not.toBeChecked();
  await expect(alertLine(page)).toBeEmpty();
  await expect(statusLine(page)).toBeEmpty();
  await expectAxeToPass(page);
});

test('a participant with no submitted note opens with both dates at today', async ({ page }) => {
  await open(page, { firstNoteDate: null });

  await expect(fromField(page)).toHaveValue(exportDay);
  await expect(toField(page)).toHaveValue(exportDay);
});

test('an unticked Format and a To date before From show their errors, in the summary too, and axe passes', async ({
  page,
}) => {
  await open(page);
  const sent = recordExports(page);
  await enterDate(toField(page), '2025-12-31');

  await exportButton(page).click();

  await expect(page).toHaveTitle('Error: Grow2Notes – Export record');
  await expect(summary(page)).toBeFocused();
  await expect(summary(page).getByRole('link')).toHaveText([
    /^To date must be the same as or after 1\sJanuary 2026$/,
    'Choose PDF or Word',
  ]);
  await expect(page.locator('#export-to-error')).toHaveText(/To date must be the same as or after 1\sJanuary 2026/);
  await expect(toField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(page.locator('#export-format-error')).toContainText('Choose PDF or Word');
  await expectAxeToPass(page);

  await summary(page).getByRole('link', { name: 'Choose PDF or Word' }).click();
  await expect(pdf(page)).toBeFocused();
  expect(new URL(page.url()).hash).toBe('');
  await pdf(page).check();
  await enterDate(toField(page), exportDay);
  await expect(summary(page)).toHaveCount(0);
  await expect(page).toHaveTitle('Grow2Notes – Export record');
  expect(sent).toEqual([]);
});

test('Export saves the file, says it is ready, keeps the form, and axe passes', async ({ page }) => {
  await open(page);
  const sent = recordExports(page);
  await word(page).check();
  await history(page).check();

  const download = page.waitForEvent('download');
  await exportButton(page).click();

  expect((await download).suggestedFilename()).toBe(fileNameOf(new URLSearchParams(defaultQuery('docx', true))));
  expect(sent).toEqual([defaultQuery('docx', true)]);
  await expect(statusLine(page)).toHaveText(readyWords('docx'));
  await expect(exportButton(page)).toBeFocused();
  await expect(word(page)).toBeChecked();
  await expect(history(page)).toBeChecked();
  await expect(fromField(page)).toHaveValue(firstNoteDate);
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen');

  await enterDate(fromField(page), '2026-02-01');
  await expect(statusLine(page)).toBeEmpty();
});

test('a range with no submitted notes says so, with the dates sent, not as a failure', async ({ page }) => {
  await open(page);
  await serveNoNotes(page);
  await pdf(page).check();

  await exportButton(page).click();

  await expect(statusLine(page)).toHaveText(
    /^No submitted notes for Jane Citizen between 1\sJanuary 2026 and 1\sOctober 2026\.$/,
  );
  await expect(alertLine(page)).toBeEmpty();
  await expect(summary(page)).toHaveCount(0);
  await expect(exportButton(page)).toBeFocused();
  await expectAxeToPass(page);
});

test('no connection says so above Export, focus stays, and axe passes', async ({ page }) => {
  await open(page);
  await page.route(isExport, (route) => route.abort('internetdisconnected'));
  await pdf(page).check();

  await exportButton(page).click();

  await expect(alertLine(page)).toHaveText('Not exported: no connection. Try again.');
  await expect(exportButton(page)).toBeFocused();
  await expect(exportButton(page)).not.toHaveAttribute('aria-disabled');
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen');
});

test('a slow export is quiet for 399 ms, then says "Exporting…" and that the file is being prepared', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await open(page);
  const heldExport = await holdNext(page, isExport);
  await pdf(page).check();

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await exportButton(page).click();
  const route = await heldExport();
  await page.clock.runFor(399);
  await expect(exportButton(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(statusLine(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(busyButton(page)).toBeVisible();
  await expect(statusLine(page)).toHaveText('Preparing the PDF file… Keep this page open.');
  await page.clock.resume();

  const download = page.waitForEvent('download');
  await route.fallback();

  expect((await download).suggestedFilename()).toBe(`participant-record_${firstNoteDate}_to_${exportDay}.pdf`);
  await expect(statusLine(page)).toHaveText(readyWords('pdf'));
});

test('keyboard: Tab goes through both dates, the Format group, the box and Export, each with a ring', async ({
  page,
}) => {
  await open(page);

  // Each date field has a stop for every part of the date, which stay one element, so the number of presses is the
  // browser's; the Format group is one stop, at its first radio while none is chosen.
  async function tabTo(id: string) {
    for (let presses = 0; presses < 10; presses += 1) {
      await page.keyboard.press('Tab');
      if (await page.evaluate((target) => document.activeElement?.id === target, id)) {
        return;
      }
    }
  }

  await heading(page).focus();
  await tabTo('export-from');
  await expectFocusAt(page, 'From date');
  await tabTo('export-to');
  await expectFocusAt(page, 'To date');
  await tabTo('export-format');
  await expectFocusAt(page, 'PDF');
  await expectStops(page, 'Tab', ['Include earlier versions of edited notes', 'Export']);

  await pdf(page).focus();
  await page.keyboard.press('ArrowDown');
  await expect(word(page)).toBeChecked();
  await expect(word(page)).toBeFocused();
});

test('keyboard: Enter on Export with a format chosen saves the file and stays', async ({ page }) => {
  await open(page);
  await pdf(page).check();
  await exportButton(page).focus();

  const download = page.waitForEvent('download');
  await page.keyboard.press('Enter');

  expect((await download).suggestedFilename()).toBe(`participant-record_${firstNoteDate}_to_${exportDay}.pdf`);
  await expect(exportButton(page)).toBeFocused();
  await expectNothingKept(page, 'Jane', 'Citizen');
});

test('nothing scrolls sideways at 320 px or at 200% text, with a .docx name in the status line', async ({ page }) => {
  await open(page);
  await word(page).check();
  await exportButton(page).click();
  await expect(statusLine(page)).toHaveText(readyWords('docx'));

  await page.setViewportSize({ width: 320, height: 800 });
  expect(await scrollsSideways(page)).toBe(false);

  await setTextTo200Percent(page);
  expect(await scrollsSideways(page)).toBe(false);
});

test('the date fields and Export are at least 48 px high, and the radio and box rows 56 px', async ({ page }) => {
  await open(page);

  await page.setViewportSize({ width: 375, height: 800 });
  await expectAtLeast(fromField(page), 44, 48);
  await expectAtLeast(toField(page), 44, 48);
  await expectAtLeast(exportButton(page), 44, 48);
  await expectAtLeast(main(page).locator('label:has(input[type="radio"])'), 44, 56);
  await expectAtLeast(main(page).locator('label:has(input[type="checkbox"])'), 44, 56);
});

test('in forced colours the borders, the radio, the error bar and the ring stay', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await open(page);
  const highlight = await systemColour(page, 'Highlight');
  const forcedRing = { ...ring, colour: highlight };

  await expect(fromField(page)).toHaveCSS('border-top-style', 'solid');
  await expect(fromField(page)).toHaveCSS('border-top-width', '2px');
  await expect(pdf(page)).toHaveCSS('border-top-style', 'solid');
  await expect(history(page)).toHaveCSS('border-top-style', 'solid');

  await exportButton(page).click();
  await expect(main(page).locator('[data-invalid]').first()).toHaveCSS('border-left-style', 'solid');
  await expect(main(page).locator('[data-invalid]').first()).toHaveCSS('border-left-width', '4px');
  await expect(summary(page)).toBeFocused();

  await pdf(page).focus();
  await expectStops(page, 'Tab', ['Include earlier versions of edited notes', 'Export'], forcedRing);
  await expect(pdf(page)).not.toBeChecked();
  await pdf(page).check();
  await expect(pdf(page)).toBeChecked();
});

test.describe('on a device in another zone with the wrong date', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test("the defaults are the server's, not the device's", async ({ page }) => {
    await page.clock.setFixedTime(new Date('2030-01-01T00:00:00Z'));
    await open(page);

    await expect(fromField(page)).toHaveValue(firstNoteDate);
    await expect(toField(page)).toHaveValue(exportDay);
    await expect(toField(page)).toHaveAttribute('max', exportDay);
  });

  test("with no submitted note, both dates are the server's today", async ({ page }) => {
    await page.clock.setFixedTime(new Date('2030-01-01T00:00:00Z'));
    await open(page, { firstNoteDate: null });

    await expect(fromField(page)).toHaveValue(exportDay);
    await expect(toField(page)).toHaveValue(exportDay);
  });
});

test('an archived participant gets the same screen, and the export works', async ({ page }) => {
  await open(page, { status: 'Archived' });
  const sent = recordExports(page);

  await expect(fromField(page)).toHaveValue(firstNoteDate);
  await expect(toField(page)).toHaveValue(exportDay);
  await expect(main(page).getByRole('definition')).toHaveText('Jane Citizen');
  await pdf(page).check();
  const download = page.waitForEvent('download');
  await exportButton(page).click();

  expect((await download).suggestedFilename()).toBe(`participant-record_${firstNoteDate}_to_${exportDay}.pdf`);
  expect(sent).toEqual([defaultQuery('pdf')]);
  await expectAxeToPass(page);
});

test('a worker sees Page not found, and no participant or export request is sent', async ({ page }) => {
  await serveMe(page, worker);
  await serveParticipant(page);
  await serveRecord(page);
  const sent = recordRequests(page);

  await page.goto(exportAddress);

  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle('Grow2Notes – Page not found');
  await expect(main(page).getByRole('button', { name: 'Export' })).toHaveCount(0);
  expect((await sent()).filter((request) => /\/api\/(participants|reports)\//.test(request))).toEqual([]);
});

// Export record's foundations (T26), each in a test of its own, as the call changes the text size and spacing for good.
test('the default form passes the foundations', async ({ page }) => {
  test.slow();
  await open(page);
  await expect(fromField(page)).toHaveValue(firstNoteDate);

  await expectFoundationsToPass(page);
});

test('the exported state passes the foundations', async ({ page }) => {
  test.slow();
  await open(page);
  await word(page).check();
  await exportButton(page).click();
  await expect(statusLine(page)).toHaveText(readyWords('docx'));

  await expectFoundationsToPass(page);
});
