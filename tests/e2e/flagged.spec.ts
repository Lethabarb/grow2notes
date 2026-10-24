import { randomUUID } from 'node:crypto';
import type { Locator, Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops, ring, tabTo } from './focus.ts';
import { expectFoundationsToPass } from './foundations.ts';
import { expectNothingKept } from './kept.ts';
import { boxOf, expectAtLeast, scrollsSideways, setTextTo200Percent, shownText, systemColour } from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { flaggedItem, manager, serveMe, worker } from './me.ts';
import { notesStore, type NotesStore } from './notes.ts';
import { jane, madeUpParticipant, participants } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';
import {
  failReviewed,
  failReviews,
  isReviewedList,
  serveReviewed,
  serveReviews,
  type ReviewedRow,
} from './reviews.ts';
import { signInThroughSetup } from './setupSignIn.ts';
import { inviteThroughThePage } from './users.ts';

// The Flagged screen's To review list in the published app (S03.01.01, S03.03.01), against flagged.md's criteria for
// access, the To review list, the note opened from it and its visual and device checks, as far as T11 builds them. The
// test that signs in for real meets the app's own Submit, To review list and read view; the others answer
// /api/auth/me, the list and the notes in the browser, with made-up people and flags (me.ts, notes.ts, reviews.ts), so
// that none of those requests reaches the app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const navItem = (page: Page, name: string) => nav(page).getByRole('link', { name, exact: true });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
// Each row's link, a return point by its key, its list item, and the row of a participant by name.
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const rowItems = (page: Page) => main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key]') });
const row = (page: Page, name: string) => rows(page).filter({ hasText: name });
const rowItem = (page: Page, name: string) => rowItems(page).filter({ hasText: name });
// The region's line, beside the page's visually hidden status region.
const loadLine = (page: Page) => main(page).locator('p[role="status"]:not(.visually-hidden)');
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
const noneToReview = (page: Page) => main(page).getByText('No flagged notes to review.', { exact: true });
const noneReviewed = (page: Page) => main(page).getByText('No reviewed notes yet.', { exact: true });
// The To review and Reviewed links, in the nav that the page's <h1> names, and Show older under the Reviewed rows.
const viewLinks = (page: Page) => main(page).getByRole('navigation', { name: 'Flagged notes' });
const viewLink = (page: Page, name: string) => viewLinks(page).getByRole('link', { name, exact: true });
const showOlder = (page: Page) => main(page).getByRole('button', { name: 'Show older', exact: true });
const olderLine = (page: Page) => main(page).locator('div:has(> ol) > p[role="status"]');
// The two view links as Tab reaches them, with the three flags in To review.
const views = ['To review (3)', 'Reviewed'];
// The read view's flag line (participant-notes.md *Read view*).
const flagLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Flagged/ });

/** The address of `participantId`'s note for `noteDate`. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;

/** A pattern for the To review list's requests, for `holdNext`. */
const toReviewRequests = '**/api/reviews?status=toReview';

/** Any time of day as the app writes it, with its non-breaking space, or "midday" or "midnight" (microcopy.md §3). */
const anyTime = '(\\d{1,2}:\\d{2}\\s[ap]m|midday|midnight)';

/** The names of the rows' links as Tab reaches them: each participant's name and the note date, as they show. */
const rowNames = (page: Page) =>
  rows(page).evaluateAll((links) => links.map((link) => (link as HTMLElement).innerText.replace(/\s+/g, ' ').trim()));

/** The words that show on each line of a row, its meta line first, then its plain lines. */
const linesOf = async (item: Locator) => Promise.all((await item.locator('p').all()).map((line) => shownText(line)));

// flagged.md's made-up rows (A35), in Melbourne, which keeps AEST, UTC+10, until 4 October 2026: Sam Taylor's note of
// Tuesday 29 September, by Priya Nair, flagged at 4:42 pm that day; Jane Citizen's of Wednesday 30 September, by Alex
// P., flagged at 9:01 am the day after; and Lee Wong's of Thursday 1 October, by Alex P., flagged at 11:20 am, whose
// version 2 removed the flag.
const samTaylor = madeUpParticipant('4e7a2c1b-8d3f-4a65-9b20-6c1e5f7d9a38', 'Sam', 'Taylor');
const leeWong = madeUpParticipant('9c1d3e5f-7a2b-4c68-8d0e-1f3a5b7c9d24', 'Lee', 'Wong');
const samsReason = 'Seemed upset about moving house.';
const janesReason = 'Did not want to get out of bed. Said her back hurt.';
const leesReason = 'Missed lunch.';
const flagRemoved = 'Flag removed in a later edit';

/** The three flags, added newest first, so that the list's order is the flag time's, as the API gives it. */
function threeFlags(): NotesStore {
  const store = notesStore({ today: manager.today, participants: [jane, leeWong, samTaylor] });
  store.addSubmitted(leeWong.id, '2026-10-01', {
    authorDisplayName: 'Alex P.',
    firstSubmittedAtUtc: '2026-10-01T01:20:00Z',
    narrative: 'Had lunch at two.',
    firstFlagReason: leesReason,
  });
  store.addSubmitted(jane.id, '2026-09-30', {
    authorDisplayName: 'Alex P.',
    firstSubmittedAtUtc: '2026-09-30T23:01:00Z',
    narrative: 'Stayed in bed until eleven.',
    flagReason: janesReason,
  });
  store.addSubmitted(samTaylor.id, '2026-09-29', {
    authorDisplayName: 'Priya Nair',
    firstSubmittedAtUtc: '2026-09-29T06:42:00Z',
    narrative: 'Packed two boxes for the move.',
    flagReason: samsReason,
  });
  return store;
}

/** The names, authors, reasons and note texts of the three flags, none of which the page may keep. */
const madeUp = [
  'Sam',
  'Taylor',
  'Jane',
  'Citizen',
  'Wong',
  'Priya',
  'Alex P.',
  samsReason,
  janesReason,
  leesReason,
  'Had lunch',
  'Stayed in bed',
  'Packed two boxes',
];

/** Answers the page's requests as the made-up manager's, with the To review list and the notes from `store`. */
async function serveFlagged(page: Page, store: NotesStore) {
  await serveMe(page, manager);
  await serveReviews(page, store);
  await store.serve(page, manager);
}

/** Opens Flagged as the made-up manager, its list and its notes from `store`, and waits for its rows. */
async function openFlagged(page: Page, store: NotesStore) {
  await serveFlagged(page, store);
  await page.goto('/flagged');
  await expect(rows(page)).toHaveCount(store.toReview().length);
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/flagged');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher sees Flagged, as participants.spec.ts shows it for the Participants screens: marked to fail,
// it passes only if the check after it fails it.
test.fail('a live region put into Flagged with its words fails the test', async ({ page }) => {
  await openFlagged(page, threeFlags());
  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('role', 'status');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

test('To review loading, with rows, empty and failed, each passing axe; Page not found for a worker and a 403', async ({
  page,
}) => {
  test.slow();
  const store = threeFlags();
  await serveFlagged(page, store);

  // Waiting: the static <h1>, the title and Flagged current at once, and the status line in the page, empty; then,
  // after 1 s, "Loading flagged notes…" there, with no row.
  const held = await holdNext(page, toReviewRequests);
  await page.goto('/flagged');
  const first = await held();
  await expect(heading(page)).toHaveText('Flagged notes');
  await expect(page).toHaveTitle(title('Flagged notes'));
  await expect(flaggedItem(page)).toHaveAttribute('aria-current', 'page');
  await expect(loadLine(page)).toBeEmpty();
  await expect(loadLine(page)).toHaveText('Loading flagged notes…');
  await expect(rows(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await first.fallback();

  // With rows: oldest flag first, as the API gives them, each link named by the name and the note date, and
  // described by its meta line alone, "{author} · flagged {time}", the date too for a flag on a later day; then the
  // reason and, on Lee's, "Flag removed in a later edit", as plain lines; no tag and no button.
  await expect(rows(page)).toHaveText([
    'Sam Taylor, Tuesday 29 September 2026',
    'Jane Citizen, Wednesday 30 September 2026',
    'Lee Wong, Thursday 1 October 2026',
  ]);
  await expect(rows(page).first()).toHaveAccessibleName(/^Sam Taylor ?, Tuesday 29\sSeptember 2026$/);
  await expect(rows(page).first().locator('time')).toHaveAttribute('datetime', '2026-09-29');
  await expect(rows(page).nth(0)).toHaveAccessibleDescription(/^Priya Nair ?, flagged 4:42\spm$/);
  await expect(rows(page).nth(1)).toHaveAccessibleDescription(/^Alex P\. ?, flagged Thu 1\sOct 2026, 9:01\sam$/);
  await expect(rows(page).nth(2)).toHaveAccessibleDescription(/^Alex P\. ?, flagged 11:20\sam$/);
  expect(await linesOf(rowItems(page).nth(0))).toEqual(['Priya Nair · flagged 4:42 pm', `Reason: ${samsReason}`]);
  expect(await linesOf(rowItems(page).nth(1))).toEqual([
    'Alex P. · flagged Thu 1 Oct 2026, 9:01 am',
    `Reason: ${janesReason}`,
  ]);
  expect(await linesOf(rowItems(page).nth(2))).toEqual([
    'Alex P. · flagged 11:20 am',
    `Reason: ${leesReason}`,
    flagRemoved,
  ]);
  await expect(rowItems(page).first().locator('p').first().locator('time')).toHaveAttribute(
    'datetime',
    '2026-09-29T06:42:00Z',
  );
  // The meta line has no status word, so no part of it is bold or in the text colour (status-tags.md *Built*).
  const looks = await rowItems(page).first().locator('p').first().evaluate((line) =>
    [line, ...line.querySelectorAll('*')].map((part) => {
      const { fontWeight, color } = getComputedStyle(part);
      return { fontWeight, color };
    }),
  );
  expect(new Set(looks.map(({ fontWeight }) => fontWeight))).toEqual(new Set(['400']));
  expect(new Set(looks.map(({ color }) => color))).toEqual(new Set([looks[0].color]));
  expect(looks[0].color, 'the secondary text colour, not the text colour, --colour-text').not.toBe('rgb(11, 12, 12)');
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByText('Flagged', { exact: true })).toHaveCount(0);
  await expect(main(page).getByText('To review', { exact: true })).toHaveCount(0);
  await expectAxeToPass(page);

  // Empty: the sentence alone, with no list.
  await serveReviews(page, notesStore({ today: manager.today, participants }));
  await page.goto('/flagged');
  await expect(noneToReview(page)).toBeVisible();
  await expect(main(page).locator('ol')).toHaveCount(0);
  await expectAxeToPass(page);

  // The first load failed: the sentence and Try again under the <h1>, then "still", with focus kept on Try again, and
  // the rows, with the <h1> focused, once a press loads them.
  await failReviews(page, 'no connection');
  await page.goto('/flagged');
  await expect(loadLine(page)).toHaveText('Flagged notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Flagged notes');
  await expect(tryAgain(page)).toBeVisible();
  await expectAxeToPass(page);
  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('Flagged notes still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await serveReviews(page, store);
  await tryAgain(page).click();
  await expect(rows(page)).toHaveCount(3);
  await expect(heading(page)).toBeFocused();
  await expectNothingKept(page, ...madeUp);

  // A manager whose list the API refuses, as after a change of role, and a worker at either address: Page not found,
  // with no nav item current.
  await failReviews(page, 403);
  await page.goto('/flagged');
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  await expect(nav(page).locator('[aria-current]')).toHaveCount(0);
  await serveMe(page, worker);
  for (const address of ['/flagged', '/flagged?view=reviewed']) {
    await page.goto(address);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle(title('Page not found'));
    await expect(flaggedItem(page)).toHaveCount(0);
  }
  await expectAxeToPass(page);
});

test('rows at least 56 px tall, and a click on a row’s Reason line or empty right side opens its note', async ({
  page,
}) => {
  await serveFlagged(page, threeFlags());
  const lee = rowItem(page, 'Lee Wong');
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/flagged');
    await expect(rows(page)).toHaveCount(3);
    await expectAtLeast(rowItems(page), 44, 56);
    await expect(lee.getByText(flagRemoved, { exact: true })).toBeVisible();

    // By the mouse at a point, as the link's ::after covers the row, so that a click on a line reaches the link.
    const reasonLine = await boxOf(lee.getByText(`Reason: ${leesReason}`, { exact: true }));
    const leesRow = await boxOf(lee);
    for (const [where, x, y] of [
      ['its Reason line', reasonLine.x + reasonLine.width / 2, reasonLine.y + reasonLine.height / 2],
      // In the row's padding, past every line's text.
      ['its empty right-hand side', leesRow.x + leesRow.width - 8, leesRow.y + leesRow.height / 2],
    ] as const) {
      await page.mouse.click(x, y);
      await expect(page, `${where} at ${width} px`).toHaveURL(noteAddress(leeWong.id, '2026-10-01'));
      await expect(heading(page)).toHaveText('Lee Wong Thursday 1 October 2026');
      await backLink(page, 'Flagged').click();
      await expect(page).toHaveURL('/flagged');
      await expect(row(page, 'Lee Wong')).toBeFocused();
    }
  }
  await expectNothingKept(page, ...madeUp);
});

test('at 320 px with text at 200%, no sideways scroll, and long names, authors and reasons wrap whole', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const unbroken = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const longReason = `${unbroken} ${'Y'.repeat(200 - unbroken.length - 1)}`;
  expect(longReason).toHaveLength(200);
  const store = notesStore({ today: manager.today, participants: [long, samTaylor] });
  store.addSubmitted(long.id, '2026-09-30', {
    authorDisplayName: 'X'.repeat(100),
    firstSubmittedAtUtc: '2026-09-30T23:01:00Z',
    firstFlagReason: longReason,
  });
  store.addSubmitted(samTaylor.id, '2026-09-29', {
    authorDisplayName: 'Priya Nair',
    firstSubmittedAtUtc: '2026-09-29T06:42:00Z',
    flagReason: samsReason,
  });
  await page.setViewportSize({ width: 320, height: 640 });
  await openFlagged(page, store);
  await setTextTo200Percent(page);

  expect(await scrollsSideways(page), 'To review, with a long name, author and reason').toBe(false);
  const item = rowItem(page, 'A'.repeat(50));
  expect(await linesOf(item)).toEqual([
    `${'X'.repeat(100)} · flagged Thu 1 Oct 2026, 9:01 am`,
    `Reason: ${longReason}`,
    flagRemoved,
  ]);
  // The name whole in the link, and the row and each of its lines holding their text, none cut short or spilling past.
  await expect(item.locator('a')).toHaveText(new RegExp(`^${'A'.repeat(50)} ${'B'.repeat(50)}, Wednesday`));
  for (const box of [item, ...(await item.locator('p').all())]) {
    expect(
      await box.evaluate((element) => element.scrollWidth <= element.clientWidth),
      `${(await box.textContent()) ?? ''} fits its row`,
    ).toBe(true);
    await expect(box).not.toHaveCSS('text-overflow', 'ellipsis');
  }

  await failReviews(page, 'no connection');
  await page.goto('/flagged');
  await expect(loadLine(page)).toHaveText('Flagged notes did not load: no connection. Try again.', { timeout: 2_000 });
  expect(await scrollsSideways(page), 'the first load failed').toBe(false);
  await expectNothingKept(page, 'A'.repeat(50), 'B'.repeat(50), 'X'.repeat(100), unbroken, ...madeUp);
});

test('in forced colours, Flagged keeps its bar, and the ring is Highlight at each stop, the back link too', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await openFlagged(page, threeFlags());
  const [highlight, linkText] = await Promise.all([systemColour(page, 'Highlight'), systemColour(page, 'LinkText')]);
  const forcedRing = { ...ring, colour: highlight };

  // The current item's bar is a real border, which forced colours keep, in the link's colour.
  await expect(flaggedItem(page)).toHaveAttribute('aria-current', 'page');
  await expect(flaggedItem(page)).toHaveCSS('border-bottom-style', 'solid');
  await expect(flaggedItem(page)).toHaveCSS('border-bottom-width', '4px');
  await expect(flaggedItem(page)).toHaveCSS('border-bottom-color', linkText);

  // The current view's bar is a real border too, in CanvasText.
  const canvasText = await systemColour(page, 'CanvasText');
  await expect(viewLink(page, 'To review (3)')).toHaveAttribute('aria-current', 'page');
  await expect(viewLink(page, 'To review (3)')).toHaveCSS('border-bottom-style', 'solid');
  await expect(viewLink(page, 'To review (3)')).toHaveCSS('border-bottom-width', '4px');
  await expect(viewLink(page, 'To review (3)')).toHaveCSS('border-bottom-color', canvasText);

  await heading(page).focus();
  const names = await rowNames(page);
  await expectStops(page, 'Tab', [...views, ...names], forcedRing);
  await expectStops(
    page,
    'Shift+Tab',
    [names[1], names[0], ...[...views].reverse(), 'Manage', 'Report', 'Flagged'],
    forcedRing,
  );
  await expectStops(page, 'Tab', ['Report', 'Manage', ...views, names[0]], forcedRing);
  await page.keyboard.press('Enter');
  await expect(heading(page)).toBeFocused();
  await expectStops(page, 'Shift+Tab', ['Flagged'], forcedRing);
  await expectNothingKept(page, ...madeUp);
});

test('with 3 to review, the Flagged badge keeps its border in forced colours, and the header passes axe', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const store = threeFlags();
  await serveFlagged(page, store);
  await serveMe(page, { ...manager, toReviewCount: 3 });
  await page.goto('/flagged');
  await expect(rows(page)).toHaveCount(3);

  await expect(flaggedItem(page)).toHaveAccessibleName(/^Flagged ?, 3 to review$/);
  const pill = flaggedItem(page).locator('[aria-hidden="true"]');
  await expect(pill).toHaveText('3');
  await expect(pill).toHaveCSS('border-top-style', 'solid');
  await expect(pill).toHaveCSS('border-top-width', '1px');
  await expect(pill).not.toHaveCSS('border-top-color', 'rgba(0, 0, 0, 0)');
  await expectAxeToPass(page);
});

test('with no flags, Flagged shows its sentence alone, with no list and no badge', async ({ page }) => {
  await serveFlagged(page, notesStore({ today: manager.today, participants }));
  await serveMe(page, { ...manager, toReviewCount: 0 });
  await page.goto('/flagged');

  await expect(noneToReview(page)).toBeVisible();
  await expect(main(page).locator('ol')).toHaveCount(0);
  await expect(flaggedItem(page)).toHaveAccessibleName('Flagged');
  await expect(flaggedItem(page).locator('span')).toHaveCount(0);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the nav, each row, a row's note and its back link, then the row again`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await openFlagged(page, threeFlags());

    // flagged.md's list Tab order, for what is built: the skip link and the nav, then each row's link, with no other
    // stop in a row.
    const shell = ['Skip to main content', 'Grow2Notes', 'Account', 'Today', 'Flagged', 'Report', 'Manage'];
    const names = await rowNames(page);
    expect(names).toEqual([
      expect.stringMatching(/^Sam Taylor ?, Tuesday 29 September 2026$/),
      expect.stringMatching(/^Jane Citizen ?, Wednesday 30 September 2026$/),
      expect.stringMatching(/^Lee Wong ?, Thursday 1 October 2026$/),
    ]);
    await expectStops(page, 'Tab', [...shell, ...views, ...names, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [names[2], names[1]]);

    // Enter opens Jane's note, its <h1> focused; Shift+Tab reaches the back link, "Flagged", whose Enter returns to the
    // list, focus on her row.
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-30'));
    await expect(heading(page)).toBeFocused();
    await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
    await expect(nav(page).locator('[aria-current]')).toHaveCount(0);
    await expectStops(page, 'Shift+Tab', ['Flagged']);
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/flagged');
    await expect(row(page, 'Jane Citizen')).toBeFocused();
    await expectStops(page, 'Tab', [names[2], 'outside the page']);
    await expectNothingKept(page, ...madeUp);
  });
}

// Casey Bell's note of Monday 28 September, by Alex Park, flagged for a manager and reviewed by Jo Smith, who is the
// Reviewed list's one made-up note, reviewed again and again (A35). Its time is 9:30 am on Tuesday 29 September in
// Melbourne, then a minute earlier for each review after it.
const casey = madeUpParticipant('7d2c9e41-5b3a-4f68-9c10-000000000001', 'Casey', 'Bell');
const caseysReason = 'Seemed upset about moving house.';
const caseysComment = 'Rang her mum.';
const joSmith = { id: '2f6d8b1c-4e07-4a93-b5d2-8c1e7a3f9d60', displayName: 'Jo Smith' };

/** `count` reviews of Casey's note, newest first, as the API gives them; `over` changes the `n`th. */
function reviewedRows(count: number, over: (n: number) => Partial<ReviewedRow> = () => ({})): ReviewedRow[] {
  return Array.from({ length: count }, (_, n) => ({
    reviewId: `0f3e5a7c-1b2d-4c6e-8a90-${String(n).padStart(12, '0')}`,
    participantId: casey.id,
    givenName: casey.givenName,
    familyName: casey.familyName,
    noteDate: '2026-09-28',
    authorDisplayName: 'Alex Park',
    flagReason: caseysReason,
    reviewedBy: joSmith.displayName,
    reviewedAtUtc: new Date(Date.UTC(2026, 8, 28, 23, 30) - n * 60_000).toISOString(),
    comment: null,
    ...over(n),
  }));
}

/** The store that holds Casey's reviewed note, for the page that opens a row of the Reviewed list. */
function reviewedStore(): NotesStore {
  const store = notesStore({ today: manager.today, participants: [casey] });
  store.addSubmitted(casey.id, '2026-09-28', {
    authorDisplayName: 'Alex Park',
    firstSubmittedAtUtc: '2026-09-28T06:42:00Z',
    narrative: 'Packed one box for the move.',
    flagReason: caseysReason,
    flagStatus: 'Reviewed',
    reviews: [{ versionNumber: 1, reviewedBy: joSmith, reviewedAtUtc: '2026-09-28T23:30:00.000Z', comment: null }],
  });
  return store;
}

/** What the Reviewed list's pages show: the names, authors, reasons and comments, none of which the page may keep. */
const reviewedMadeUp = ['Casey', 'Bell', 'Alex Park', 'Jo Smith', caseysReason, caseysComment, 'Packed one box'];

/** Opens Reviewed as the made-up manager, with `rows` for its list, and waits for its first row, or its sentence. */
async function openReviewed(page: Page, reviews: readonly ReviewedRow[]) {
  await serveFlagged(page, reviewedStore());
  // After the store's own, which answers the Reviewed list from the reviews that the store made, as Playwright tries
  // the latest route first.
  await serveReviewed(page, reviews);
  await page.goto('/flagged?view=reviewed');
  await expect(reviews.length === 0 ? noneReviewed(page) : rows(page).first()).toBeVisible();
}

/** The animation frames after a scroll, in which a page that loaded on scrolling would have asked. */
const twoFrames = (page: Page) =>
  page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => {
            resolve();
          });
        });
      }),
  );

test('the view links: a switch pushes one entry, focus stays on the link, an unknown view is put right', async ({
  page,
}) => {
  await serveFlagged(page, threeFlags());
  await serveReviewed(page, reviewedRows(2));
  await page.goto('/flagged');
  await expect(rows(page)).toHaveCount(3);
  await expect(viewLinks(page).locator('[aria-current]')).toHaveText(['To review (3)']);

  // A switch: the address and the list change, nothing scrolls, and focus stays on the link pressed.
  await viewLink(page, 'Reviewed').click();
  await expect(page).toHaveURL('/flagged?view=reviewed');
  await expect(rows(page)).toHaveCount(2);
  await expect(viewLink(page, 'Reviewed')).toBeFocused();
  await expect(viewLinks(page).locator('[aria-current]')).toHaveText(['Reviewed']);
  await expect(flaggedItem(page)).toHaveAttribute('aria-current', 'page');
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await expect(page).toHaveTitle(title('Flagged notes'));
  await expectNothingKept(page, ...madeUp, ...reviewedMadeUp);

  // Back returns to the view before, one entry back.
  await page.goBack();
  await expect(page).toHaveURL('/flagged');
  await expect(rows(page)).toHaveCount(3);
  await expect(viewLinks(page).locator('[aria-current]')).toHaveText(['To review (3)']);

  // Any other view shows To review, with the address put right and no new entry.
  await page.goto('/flagged?view=xyz');
  await expect(page).toHaveURL('/flagged');
  await expect(rows(page)).toHaveCount(3);
  await page.goBack();
  await expect(page).not.toHaveURL(/xyz/);
});

test('Reviewed loading, with rows, Show older, empty and failed, each passing axe', async ({ page }) => {
  test.slow();
  const all = reviewedRows(61, (n) => (n === 0 ? { comment: caseysComment } : {}));
  await serveFlagged(page, reviewedStore());
  await serveReviewed(page, all);

  // Waiting: the static <h1> and Reviewed current at once, the status line in the page, empty; then, after 1 s,
  // "Loading reviewed notes…" there, with no row.
  const held = await holdNext(page, isReviewedList);
  await page.goto('/flagged?view=reviewed');
  const first = await held();
  await expect(heading(page)).toHaveText('Flagged notes');
  await expect(viewLink(page, 'Reviewed')).toHaveAttribute('aria-current', 'page');
  await expect(loadLine(page)).toBeEmpty();
  await expect(loadLine(page)).toHaveText('Loading reviewed notes…');
  await expect(rows(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await first.fallback();

  // With rows: newest first, 30, each link named by the name and the note date, and described by its meta line
  // alone, "{author} · reviewed by {reviewer} on {date and time}", then the reason and the comment, if any, as plain
  // lines; no tag, and no button but Show older, with its status line empty and nothing counted.
  await expect(rows(page)).toHaveCount(30);
  await expect(rows(page).first()).toHaveAccessibleName(/^Casey Bell ?, Monday 28\sSeptember 2026$/);
  await expect(rows(page).first()).toHaveAccessibleDescription(
    /^Alex Park ?, reviewed by Jo Smith on Tue 29\sSep 2026, 9:30\sam$/,
  );
  expect(await linesOf(rowItems(page).nth(0))).toEqual([
    'Alex Park · reviewed by Jo Smith on Tue 29 Sep 2026, 9:30 am',
    `Reason: ${caseysReason}`,
    `Comment: ${caseysComment}`,
  ]);
  expect(await linesOf(rowItems(page).nth(1))).toEqual([
    'Alex Park · reviewed by Jo Smith on Tue 29 Sep 2026, 9:29 am',
    `Reason: ${caseysReason}`,
  ]);
  await expect(rowItems(page).first().locator('p').first().locator('time')).toHaveAttribute(
    'datetime',
    all[0].reviewedAtUtc,
  );
  await expect(main(page).getByRole('button')).toHaveCount(1);
  await expect(showOlder(page)).toBeVisible();
  await expect(olderLine(page)).toBeEmpty();
  await expect(main(page).getByText(/\d+ (notes|of)|end of|Showing|page \d/i)).toHaveCount(0);
  await expectAxeToPass(page);

  // Show older: 30 more below, focus on the first new row, then the last one, with the button gone.
  await showOlder(page).click();
  await expect(rows(page)).toHaveCount(60);
  await expect(rows(page).nth(30)).toBeFocused();
  await expectAxeToPass(page);
  await showOlder(page).click();
  await expect(rows(page)).toHaveCount(61);
  await expect(rows(page).nth(60)).toBeFocused();
  await expect(showOlder(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // Empty: the sentence alone, with no list or button.
  await serveReviewed(page, []);
  await page.goto('/flagged?view=reviewed');
  await expect(noneReviewed(page)).toBeVisible();
  await expect(main(page).locator('ol')).toHaveCount(0);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expectAxeToPass(page);

  // The first load failed: the sentence and Try again under the <h1>, then "still", with focus kept on Try again, and
  // the rows, with the <h1> focused, once a press loads them.
  await failReviewed(page);
  await page.goto('/flagged?view=reviewed');
  await expect(loadLine(page)).toHaveText('Reviewed notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Flagged notes');
  await expect(tryAgain(page)).toBeVisible();
  await expectAxeToPass(page);
  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('Reviewed notes still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await serveReviewed(page, all);
  await tryAgain(page).click();
  await expect(rows(page)).toHaveCount(30);
  await expect(heading(page)).toBeFocused();
  await expectNothingKept(page, ...reviewedMadeUp);
});

test('Show older with no connection keeps the rows, focus on the button and the sentence, then says "still"', async ({
  page,
}) => {
  await openReviewed(page, reviewedRows(61));
  await expect(rows(page)).toHaveCount(30);
  await failReviewed(page, { olderOnly: true });

  await showOlder(page).click();
  await expect(olderLine(page)).toHaveText('Older notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(showOlder(page)).toBeFocused();
  await expect(rows(page)).toHaveCount(30);
  await expectAxeToPass(page);

  await showOlder(page).click();
  await expect(olderLine(page)).toHaveText('Older notes still did not load: no connection. Try again.');
  await expect(showOlder(page)).toBeFocused();
  await expect(rows(page)).toHaveCount(30);
  await expectAxeToPass(page);
});

test('scrolling Reviewed loads and counts nothing, and an idle Flagged sends nothing', async ({ page }) => {
  await openReviewed(page, reviewedRows(61));
  await expect(rows(page)).toHaveCount(30);
  const sent = recordRequests(page);

  const scrolledTo = await page.evaluate(() => {
    window.scrollTo(0, document.documentElement.scrollHeight);
    return window.scrollY;
  });
  expect(scrolledTo).toBeGreaterThan(0);
  await twoFrames(page);
  await expect(showOlder(page)).toBeInViewport({ ratio: 1 });
  await expect(rows(page)).toHaveCount(30);
  await expect(main(page).getByText(/\d+ (notes|of)|end of|Showing|page \d/i)).toHaveCount(0);

  // Nothing is asked for on a timer: no polling of either list or of the count.
  await page.waitForTimeout(1_500);
  expect(await sent()).toEqual([]);
});

test('a window focus with no connection keeps the rows and the number, and announces nothing', async ({ page }) => {
  await openReviewed(page, reviewedRows(31));
  await expect(viewLink(page, 'To review (0)')).toBeVisible();
  await expect(rows(page)).toHaveCount(30);
  await failReviews(page, 'no connection');
  await failReviewed(page);
  let failures = 0;
  page.on('requestfailed', (request) => {
    if (new URL(request.url()).pathname === '/api/reviews') {
      failures += 1;
    }
  });

  await page.evaluate(() => window.dispatchEvent(new Event('focus')));
  // Both lists, each once more after the query client's silent retry, and the watcher in afterEach sees no words.
  await expect.poll(() => failures).toBeGreaterThanOrEqual(2);
  await twoFrames(page);
  await expect(rows(page)).toHaveCount(30);
  await expect(viewLink(page, 'To review (0)')).toBeVisible();
  await expect(olderLine(page)).toBeEmpty();
  await expectNothingKept(page, ...reviewedMadeUp);
});

test('open the 10th of 60 Reviewed rows, Back shows all 60 with the scroll and focus kept; a reload gives 30', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 667 });
  await openReviewed(page, reviewedRows(60));
  await showOlder(page).click();
  await expect(rows(page)).toHaveCount(60);

  const tenth = rows(page).nth(9);
  await tenth.scrollIntoViewIfNeeded();
  const kept = await page.evaluate(() => window.scrollY);
  expect(kept).toBeGreaterThan(0);
  for (const [way, goBack] of [
    ['the back link', () => backLink(page, 'Flagged').click()],
    ['browser Back', () => page.goBack()],
  ] as const) {
    await tenth.click();
    await expect(page).toHaveURL(noteAddress(casey.id, '2026-09-28'));
    await expect(heading(page)).toBeFocused();
    await goBack();
    await expect(page, way).toHaveURL('/flagged?view=reviewed');
    await expect(rows(page), way).toHaveCount(60, { timeout: 500 });
    await expect(tenth, way).toBeFocused();
    expect(await page.evaluate(() => window.scrollY), way).toBe(kept);
  }

  // A reload starts from the newest 30, at the top, as nothing is stored on the device.
  await page.reload();
  await expect(rows(page)).toHaveCount(30);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await expectNothingKept(page, ...reviewedMadeUp);
});

test('at 320 px with text at 200%, Reviewed has no sideways scroll, the view links wrap, and long text wraps', async ({
  page,
}) => {
  const unbroken = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const longReason = `${unbroken} ${'Y'.repeat(200 - unbroken.length - 1)}`;
  const longComment = `${'Z'.repeat(60)}\nA second line, ${'with many words '.repeat(12)}`;
  const long = reviewedRows(2, (n) => ({
    givenName: 'A'.repeat(50),
    familyName: 'B'.repeat(50),
    authorDisplayName: 'X'.repeat(100),
    reviewedBy: 'W'.repeat(100),
    flagReason: longReason,
    comment: n === 0 ? longComment : null,
  }));
  await page.setViewportSize({ width: 320, height: 640 });
  await openReviewed(page, long);
  await setTextTo200Percent(page);

  expect(await scrollsSideways(page), 'Reviewed, with a long name, author, reviewer, reason and comment').toBe(false);
  // The two view links on two rows, each inside the viewport.
  const toReview = await boxOf(viewLink(page, 'To review (0)'));
  const reviewed = await boxOf(viewLink(page, 'Reviewed'));
  expect(reviewed.y, 'the view links wrap onto two rows').toBeGreaterThan(toReview.y);
  for (const box of [toReview, reviewed]) {
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(320);
  }

  const item = rowItems(page).first();
  expect(await linesOf(item)).toEqual([
    `${'X'.repeat(100)} · reviewed by ${'W'.repeat(100)} on Tue 29 Sep 2026, 9:30 am`,
    `Reason: ${longReason}`,
    `Comment: ${'Z'.repeat(60)} A second line, ${'with many words '.repeat(12)}`.trim(),
  ]);
  await expect(item.locator('a')).toHaveText(new RegExp(`^${'A'.repeat(50)} ${'B'.repeat(50)}, Monday`));
  for (const box of [item, ...(await item.locator('p').all())]) {
    expect(
      await box.evaluate((element) => element.scrollWidth <= element.clientWidth),
      `${(await box.textContent()) ?? ''} fits its row`,
    ).toBe(true);
    await expect(box).not.toHaveCSS('text-overflow', 'ellipsis');
  }

  await failReviewed(page);
  await page.goto('/flagged?view=reviewed');
  await expect(loadLine(page)).toHaveText('Reviewed notes did not load: no connection. Try again.', { timeout: 2_000 });
  expect(await scrollsSideways(page), 'the first load failed').toBe(false);
  await expectNothingKept(page, 'A'.repeat(50), 'B'.repeat(50), 'X'.repeat(100), 'W'.repeat(100), unbroken);
});

test('in forced colours, Reviewed keeps its current bar, and the ring is Highlight at each stop', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await openReviewed(page, reviewedRows(2));
  const highlight = await systemColour(page, 'Highlight');
  const canvasText = await systemColour(page, 'CanvasText');
  const forcedRing = { ...ring, colour: highlight };

  await expect(viewLink(page, 'Reviewed')).toHaveAttribute('aria-current', 'page');
  await expect(viewLink(page, 'Reviewed')).toHaveCSS('border-bottom-style', 'solid');
  await expect(viewLink(page, 'Reviewed')).toHaveCSS('border-bottom-width', '4px');
  await expect(viewLink(page, 'Reviewed')).toHaveCSS('border-bottom-color', canvasText);

  await heading(page).focus();
  const names = await rowNames(page);
  await expectStops(page, 'Tab', ['To review (0)', 'Reviewed', ...names], forcedRing);
  await expectStops(page, 'Shift+Tab', [names[0], 'Reviewed', 'To review (0)'], forcedRing);
  await expectNothingKept(page, ...reviewedMadeUp);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the view links, each Reviewed row, Show older and then the new row`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await openReviewed(page, reviewedRows(31));

    // The skip link and the nav, then the two view links, each row's link and Show older, with no other stop: every
    // one a native link or button.
    const shell = ['Skip to main content', 'Grow2Notes', 'Account', 'Today', 'Flagged', 'Report', 'Manage'];
    const names = await rowNames(page);
    expect(names).toHaveLength(30);
    await expectStops(page, 'Tab', [...shell, 'To review (0)', 'Reviewed', ...names, 'Show older']);
    const tags = await main(page)
      .locator('a, button, input, select, textarea, summary, [tabindex]:not([tabindex="-1"])')
      .evaluateAll((elements) => [...new Set(elements.map((element) => element.tagName))].sort());
    expect(tags).toEqual(['A', 'BUTTON']);

    // Enter on Show older adds the 31st row, with focus on it, and Shift+Tab returns to the row before it.
    await page.keyboard.press('Enter');
    await expect(rows(page)).toHaveCount(31);
    await expect(rows(page).last()).toBeFocused();
    await expect(showOlder(page)).toHaveCount(0);
    await expectStops(page, 'Shift+Tab', [names[29]]);
    await expectNothingKept(page, ...reviewedMadeUp);
  });
}

// The participant whom the test that signs in for real adds, and the note and reason that the worker submits.
const janeCitizen = { givenName: 'Jane', familyName: 'Citizen', name: 'Jane Citizen' };
const sentence = 'Walked to the park and back.';
const flagReason = 'Mentioned a sore knee after the walk.';
const reviewComment = 'Called her to check on the knee.';

// The Review panel of the read view: its comment box, Mark reviewed, and the record that replaces the form.
const commentBox = (page: Page) => main(page).getByRole('textbox', { name: 'Comment (optional)', exact: true });
const markReviewed = (page: Page) => main(page).getByRole('button', { name: /^(Mark reviewed|Marking reviewed…)$/ });
const reviewRecord = (page: Page) =>
  main(page)
    .locator('section')
    .filter({ has: page.getByRole('heading', { level: 2, name: 'Review', exact: true }) })
    .locator('p[tabindex="-1"]');

/** "Saved 4:12 pm", or the like, at whatever time the app saved, with its non-breaking space (microcopy.md §3). */
const savedAnyTime = new RegExp(`^Saved ${anyTime}$`);

/** Whether the response answers the To review list's request. */
const isToReview = (response: Response) => new URL(response.url()).pathname === '/api/reviews';

/** Whether the response answers a Submit, `POST {base}/versions`. */
const isSubmit = (response: Response) =>
  response.request().method() === 'POST' && /^\/api\/participants\/[^/]+\/notes\/[^/]+\/versions$/.test(
    new URL(response.url()).pathname,
  );

/** From Today with no participants, adds Jane Citizen through the empty state's Add participant, then goes back. */
async function addJane(page: Page) {
  await expect(main(page).getByText('No participants yet.', { exact: true })).toBeVisible();
  await main(page).getByRole('link', { name: 'Add participant', exact: true }).click();
  await expect(heading(page)).toHaveText('Add participant');
  await main(page).getByRole('textbox', { name: 'Given name', exact: true }).fill(janeCitizen.givenName);
  await main(page).getByRole('textbox', { name: 'Family name', exact: true }).fill(janeCitizen.familyName);
  await main(page).getByRole('textbox', { name: 'Day', exact: true }).fill('7');
  await main(page).getByRole('textbox', { name: 'Month', exact: true }).fill('3');
  await main(page).getByRole('textbox', { name: 'Year', exact: true }).fill('1987');
  const added = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/admin/participants',
  );
  await main(page).getByRole('region', { name: 'Details' }).getByRole('button', { name: 'Save', exact: true }).click();
  expect((await added).status()).toBe(201);
  await expect(heading(page)).toHaveText(janeCitizen.name);
  await navItem(page, 'Today').click();
  await expect(rows(page)).toHaveText([janeCitizen.name]);
}

test('signed in for real at 375 px, a worker’s flagged submit reaches both managers’ To review lists', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
  thirdDevice,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJane(page);

  // The manager invites a worker and a second manager, each set up on a phone of their own from the link that admin
  // reset-signin gives, landing on Today.
  await navItem(page, 'Manage').click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(heading(page)).toHaveText('Users');
  const riley = { displayName: 'Riley Sample', email: `riley.sample.${randomUUID()}@example.org` };
  const morgan = { displayName: 'Morgan Sample', email: `morgan.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, riley, 'Worker');
  await inviteThroughThePage(page, morgan, 'Manager');
  const phone = secondDevice.page;
  const secondManager = thirdDevice.page;
  for (const [device, person] of [[phone, riley], [secondManager, morgan]] as const) {
    await device.setViewportSize({ width: 375, height: 812 });
    await device.goto(await resetSignIn(person.email));
    await main(device).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
    await expect(rows(device)).toHaveText([janeCitizen.name]);
  }
  // Nothing is flagged yet, so neither manager's menu word carries a number.
  await expect(flaggedItem(page)).toHaveAccessibleName('Flagged');

  // The second manager opens Flagged before the submit: the app's empty list.
  const empty = secondManager.waitForResponse(isToReview);
  await flaggedItem(secondManager).click();
  const emptyAnswer = await empty;
  expect(emptyAnswer.status()).toBe(200);
  expect(await emptyAnswer.json()).toEqual([]);
  await expect(heading(secondManager)).toBeFocused();
  await expect(heading(secondManager)).toHaveText('Flagged notes');
  await expect(noneToReview(secondManager)).toBeVisible();
  await expect(flaggedItem(secondManager)).toHaveAccessibleName('Flagged');
  await expect(viewLink(secondManager, 'To review (0)')).toBeVisible();

  // The worker fills in Jane's note, flags it with a reason and submits it through the confirmation.
  await row(phone, janeCitizen.name).click();
  await expect(heading(phone)).toBeFocused();
  const address = new URL(phone.url()).pathname;
  const noteDate = address.slice(-10);
  await main(phone).getByRole('textbox', { name: '3. Guided notes' }).pressSequentially(sentence);
  await main(phone).locator('label[for="flag-for-manager"]').locator('span').click();
  await main(phone).getByRole('textbox', { name: 'Reason', exact: true }).fill(flagReason);
  await expect(phone.locator('#save-status')).toHaveText(savedAnyTime);
  await main(phone).getByRole('button', { name: 'Submit note', exact: true }).click();
  const confirmation = phone.getByRole('alertdialog');
  await expect(confirmation.getByRole('heading', { level: 2 })).toBeFocused();
  const posted = phone.waitForResponse(isSubmit);
  await confirmation.getByRole('button', { name: `Submit note for ${janeCitizen.name}`, exact: true }).click();
  const answer = await posted;
  expect(answer.status()).toBe(201);
  const { createdAtUtc } = (await answer.json()) as { createdAtUtc: string };
  await expect(main(phone).locator('[data-route-focus]')).toHaveText(`Note for ${janeCitizen.name} submitted`);

  /** Expects To review to hold the one row of the note that Riley flagged, as every manager sees it. */
  async function expectRileysFlag(on: Page) {
    await expect(rows(on)).toHaveCount(1);
    await expect(rows(on)).toHaveAccessibleName(new RegExp(`^${janeCitizen.name} ?, \\w+day \\d{1,2}\\s\\w+ \\d{4}$`));
    await expect(rows(on).locator('time')).toHaveAttribute('datetime', noteDate);
    await expect(rows(on)).toHaveAccessibleDescription(new RegExp(`^Riley Sample ?, flagged ${anyTime}$`));
    expect(await linesOf(rowItems(on))).toEqual([
      expect.stringMatching(new RegExp(`^Riley Sample · flagged ${anyTime}$`)),
      `Reason: ${flagReason}`,
    ]);
    await expect(rowItems(on).locator('p').first().locator('time')).toHaveAttribute('datetime', createdAtUtc);
  }

  // The first manager, choosing Flagged, sees the flag: the name, the date, "Riley Sample · flagged" and the time, and
  // the reason, from the app's own list.
  const listed = page.waitForResponse(isToReview);
  await flaggedItem(page).click();
  expect((await listed).status()).toBe(200);
  await expect(heading(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Flagged notes'));
  await expectRileysFlag(page);
  // The number is in the menu on this screen change, and in the first view link.
  await expect(flaggedItem(page)).toHaveAccessibleName(/^Flagged ?, 1 to review$/);
  await expect(viewLink(page, 'To review (1)')).toBeVisible();

  // The second manager, on their next page load, Today and back to Flagged: the empty list kept in memory at once,
  // then the same row, over it, from the list asked for again.
  await navItem(secondManager, 'Today').click();
  await expect(rows(secondManager)).toHaveText([janeCitizen.name]);
  await expect(flaggedItem(secondManager)).toHaveAccessibleName(/^Flagged ?, 1 to review$/);
  const asked = await holdNext(secondManager, toReviewRequests);
  await flaggedItem(secondManager).click();
  const again = await asked();
  await expect(noneToReview(secondManager)).toBeVisible();
  await again.fallback();
  await expectRileysFlag(secondManager);
  await expect(noneToReview(secondManager)).toHaveCount(0);
  await expectNothingKept(secondManager, janeCitizen.givenName, janeCitizen.familyName, flagReason);

  // The first opens it: the read view, with no nav item current, whose back link "Flagged" returns to the list, focus
  // on the row.
  await row(page, janeCitizen.name).click();
  await expect(page).toHaveURL(address);
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText(new RegExp(`^${janeCitizen.name}\\s\\w+day \\d{1,2}\\s\\w+ \\d{4}$`));
  await expect.poll(() => shownText(flagLine(page))).toBe(`Flagged Reason: ${flagReason}`);
  await expect(main(page).getByText(sentence, { exact: true })).toBeVisible();
  await expect(nav(page).locator('[aria-current]')).toHaveCount(0);
  await backLink(page, 'Flagged').click();
  await expect(page).toHaveURL('/flagged');
  await expect(row(page, janeCitizen.name)).toBeFocused();
  await expectRileysFlag(page);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason);

  // The first manager marks the note reviewed, with a comment: the record, then, back on Flagged, the row gone, the
  // menu word alone and the first view link at 0; Reviewed holds the review: who, when, the reason and the comment.
  await row(page, janeCitizen.name).click();
  await expect(page).toHaveURL(address);
  await commentBox(page).fill(reviewComment);
  await markReviewed(page).click();
  await expect(reviewRecord(page)).toHaveText(/^Reviewed by .+ on .+$/);
  const recorded = await shownText(reviewRecord(page));
  await backLink(page, 'Flagged').click();
  await expect(page).toHaveURL('/flagged');
  await expect(noneToReview(page)).toBeVisible();
  await expect(rows(page)).toHaveCount(0);
  await expect(flaggedItem(page)).toHaveAccessibleName(/^Flagged$/);
  await expect(viewLink(page, 'To review (0)')).toBeVisible();
  await viewLink(page, 'Reviewed').click();
  await expect(rows(page)).toHaveCount(1);
  expect(await linesOf(rowItems(page))).toEqual([
    `Riley Sample · ${recorded.replace(/^Reviewed/, 'reviewed')}`,
    `Reason: ${flagReason}`,
    `Comment: ${reviewComment}`,
  ]);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason, reviewComment);

  // The worker, who has no Flagged item, meets Page not found at its address.
  await expect(flaggedItem(phone)).toHaveCount(0);
  await phone.goto('/flagged');
  await expect(heading(phone)).toHaveText('Page not found');
  await expect(phone).toHaveTitle(title('Page not found'));
  await expect(main(phone).getByText(janeCitizen.name)).toHaveCount(0);
  await expectNothingKept(phone, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason);
});

// The ring's geometry (foundations.md item 10, T26). Both come from global rules, base.css's :focus-visible and
// LinkRow.module.css's, so this one screen proves them for every screen that has a view link or a row.
test('the ring sits 2 px off a view link, and 3 px inside a To review row', async ({ page }) => {
  await openFlagged(page, threeFlags());
  await heading(page).focus();

  await tabTo(page, viewLink(page, 'To review (3)'));
  await expect(viewLink(page, 'To review (3)')).toHaveCSS('outline-offset', '2px');

  const first = rows(page).first();
  await tabTo(page, first);
  const [pseudo, own] = await first.evaluate((link) => {
    const after = getComputedStyle(link, '::after');
    return [
      { style: after.outlineStyle, width: after.outlineWidth, offset: after.outlineOffset },
      getComputedStyle(link).outlineStyle,
    ];
  });
  expect(own, 'the link itself draws no ring').toBe('none');
  expect(pseudo, 'the ring on the row, drawn by the link’s ::after').toEqual({
    style: 'solid',
    width: '3px',
    offset: '-3px',
  });
});

// Flagged's foundations (T26), each in a test of its own, as the call changes the text size and spacing for good.
test('To review with its rows passes the foundations', async ({ page }) => {
  test.slow();
  await openFlagged(page, threeFlags());

  await expectFoundationsToPass(page);
});

test('Reviewed with Show older passes the foundations', async ({ page }) => {
  test.slow();
  await openReviewed(page, reviewedRows(31));
  await expect(rows(page)).toHaveCount(30);
  await expect(showOlder(page)).toBeVisible();

  await expectFoundationsToPass(page);
});
