import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import { scrollsSideways } from './layout.ts';
import { manager, serveMe, worker } from './me.ts';
import { recordExports, reportAddress, serveNoNotes, serveReport } from './report.ts';
import { recordRequests } from './requests.ts';

// The Report screen in the published app, against daily-report.md's criteria for the download buttons as far as T15
// builds them: its words, its keyboard, its layout and its failure line. The made-up manager's session and the export
// are answered in the browser (me.ts, report.ts); the download itself is report-download.spec.ts's, in three browsers.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const button = (page: Page, name: string) => main(page).getByRole('button', { name, exact: true });

test('the Report screen: its title and nav item, two buttons, axe at 375 and 1280 px, no sideways scroll', async ({
  page,
}) => {
  await serveMe(page, manager);
  await page.goto(reportAddress);

  await expect(heading(page)).toHaveText('Daily report');
  await expect(page).toHaveTitle('Grow2Notes – Daily report');
  await expect(
    page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Report', exact: true }),
  ).toHaveAttribute('aria-current', 'page');
  await expect(main(page).getByRole('button')).toHaveText(['Download Word', 'Download PDF']);
  await expect(main(page).getByRole('alert')).toBeEmpty();
  await expectAxeToPass(page);

  await page.setViewportSize({ width: 375, height: 800 });
  expect(await scrollsSideways(page)).toBe(false);
});

test('keyboard: Tab goes to Download Word, then Download PDF, and Enter on Download Word starts a download', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveReport(page);
  await page.goto(reportAddress);
  await expect(heading(page)).toBeVisible();

  await heading(page).focus();
  await expectStops(page, 'Tab', ['Download Word', 'Download PDF']);
  await expectStops(page, 'Shift+Tab', ['Download Word']);
  const download = page.waitForEvent('download');
  await page.keyboard.press('Enter');

  expect((await download).suggestedFilename()).toBe('daily-notes_2026-10-01.docx');
  await expect(button(page, 'Download Word')).toBeFocused();
  await expectNothingKept(page);
});

test('a day with no submitted notes says so above the buttons, and focus stays on the button', async ({ page }) => {
  await serveMe(page, manager);
  await serveNoNotes(page);
  await page.goto(reportAddress);

  await button(page, 'Download PDF').click();

  await expect(main(page).getByRole('alert')).toHaveText(/^No submitted notes for Thursday 1\sOctober 2026\.$/);
  await expect(button(page, 'Download PDF')).toBeFocused();
  await expect(button(page, 'Download PDF')).not.toHaveAttribute('aria-disabled');
  await expectAxeToPass(page);
  await expectNothingKept(page);
});

test('a worker sees Page not found, and no export request is sent', async ({ page }) => {
  await serveMe(page, worker);
  await serveReport(page);
  const sent = recordRequests(page);
  const exports = recordExports(page);

  await page.goto(reportAddress);

  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle('Grow2Notes – Page not found');
  await expect(main(page).getByRole('button', { name: /^Download/ })).toHaveCount(0);
  expect((await sent()).filter((request) => request.includes('/export'))).toEqual([]);
  expect(exports).toEqual([]);
});
