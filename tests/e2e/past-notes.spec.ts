import { randomUUID } from 'node:crypto';
import type { Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops, ring } from './focus.ts';
import { breakfast, bus } from './goals.ts';
import { expectNothingKept } from './kept.ts';
import { boxOf, expectAtLeast, scrollsSideways, setTextTo200Percent, shownText, systemColour } from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { alexStarted, daysAfter, failNoteReads, notesStore, type NotesStore } from './notes.ts';
import { jane, madeUpParticipant, participants, ravi } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';
import { serveToday } from './today.ts';

// A participant's Past notes, the read view of a submitted note and someone else's draft in the published app
// (S02.04.01 to S02.04.03), against participant-notes.md's criteria for the history list, the read view and both
// screens, as far as T09 and T12 build them. Each test answers /api/auth/me, /api/today, the participant's header,
// their history and their notes in the browser, with made-up people and notes (me.ts, today.ts, notes.ts), so that
// none of those requests reaches the app; note-form.spec.ts signs in for real and meets the app's own read view, Past
// notes and someone else's draft.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
// Each row's link, Today's or Past notes', a return point by its key, its list item and its line, which describes it.
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const rowItems = (page: Page) => main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key]') });
const row = (page: Page, name: string) => rows(page).filter({ hasText: name });
const rowLine = (page: Page, name: string) => rowItems(page).filter({ hasText: name }).locator('p');
// The region's line, after the page's content, beside Show older's and the page's visually hidden one.
const loadLine = (page: Page) => main(page).locator('p[role="status"]:not(.visually-hidden)').last();
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
// Past notes (participant-notes.md components 3 to 7): Edit participant, the list, Show older and its status line.
const editParticipant = (page: Page) => main(page).getByRole('link', { name: 'Edit participant', exact: true });
const showOlder = (page: Page) => main(page).getByRole('button', { name: 'Show older', exact: true });
const olderLine = (page: Page) => main(page).locator('div:has(> ol) > p[role="status"]');
// The read view (participant-notes.md *Read view*): the Past notes link, the byline, the flag line, the goals and the
// Guided notes text.
const pastNotesLink = (page: Page) => main(page).getByRole('link', { name: 'Past notes', exact: true });
const byline = (page: Page) => main(page).locator('p').filter({ hasText: /^Written by / });
const flagLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Flagged/ });
const flagged = (page: Page) => main(page).getByText('Flagged', { exact: true });
const editedLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Edited/ });
const readGoals = (page: Page) => main(page).locator('ul[role="list"] > li');
const readText = (page: Page) =>
  main(page).getByRole('heading', { name: '3. Guided notes', exact: true }).locator('xpath=following-sibling::div[1]');

/** The address of `participantId`'s note for `noteDate`, and of their Past notes. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;
const pastNotesAddress = (participantId: string) => `/participants/${participantId}/notes`;

/** Whether the URL is a note's, `GET {base}`, or a history's, as Show older asks for it, with `before`. */
const isNote = (url: URL) => /^\/api\/participants\/[^/]+\/notes\/[^/]+$/.test(url.pathname);
const isOlderNotes = (url: URL) =>
  /^\/api\/participants\/[^/]+\/notes$/.test(url.pathname) && url.searchParams.has('before');
const isFirstNotes = (url: URL) =>
  /^\/api\/participants\/[^/]+\/notes$/.test(url.pathname) && !url.searchParams.has('before');

/** Each element of the page that is fixed or sticky, by its tag, which could cover the control with focus. */
const pinned = (page: Page) =>
  page.evaluate(() =>
    [...document.querySelectorAll('*')]
      .filter((element) => ['fixed', 'sticky'].includes(getComputedStyle(element).position))
      .map((element) => element.localName),
  );

/**
 * Fires what the browser fires as a person comes back to the tab: the window's `focus`, and `visibilitychange`, which
 * bubbles from the document to the window, where TanStack Query listens for it, as a browser's own does.
 */
const comeBack = (page: Page) =>
  page.evaluate(() => {
    window.dispatchEvent(new Event('focus'));
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });

/** Waits two of the page's frames, by which time a scroll's events and intersection observers have run. */
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

/** The names of the rows' links as Tab reaches them: each date, as it shows. */
const rowNames = (page: Page) =>
  rows(page).evaluateAll((links) => links.map((link) => (link as HTMLElement).innerText.replace(/\s+/g, ' ').trim()));

// Made-up notes (A35): Priya Nair's, on Jane's today flagged with two goals, and on Ravi's today at midday with none;
// one of Jane's from 29 September submitted on 1 October; and Sam Lee's draft of Jane's 30 September.
const priya = 'Priya Nair';
const flagReason = 'Mentioned pain in his left knee after the walk.';
const narrative = 'Walked to the shop.\n\nBought bread and milk.';

/** Jane's and Ravi's notes, as above. */
function janeAndRavi(): NotesStore {
  const store = notesStore({ today: worker.today, participants });
  store.addSubmitted(jane.id, worker.today, {
    authorDisplayName: priya,
    firstSubmittedAtUtc: '2026-10-01T06:42:00Z',
    narrative,
    goals: [
      { text: breakfast.text, isTicked: true },
      { text: bus.text, isTicked: false },
    ],
    flagReason,
  });
  store.addSubmitted(ravi.id, worker.today, {
    authorDisplayName: priya,
    firstSubmittedAtUtc: '2026-10-01T02:00:00Z',
    narrative: 'Read the paper.',
  });
  store.addSubmitted(jane.id, '2026-09-29', {
    authorDisplayName: priya,
    firstSubmittedAtUtc: '2026-09-30T22:05:00Z',
    narrative: 'Slept in.',
  });
  store.startElsewhere(jane.id, '2026-09-30', { authorDisplayName: 'Sam Lee', startedAtUtc: '2026-09-29T23:14:00Z' });
  return store;
}

/**
 * Jane's notes on each of `count` days back from today, newest first: Priya Nair's and Alex P.'s by turns, every third
 * flagged; with `draftLast`, the oldest is Sam Lee's draft instead.
 */
function manyNotes(count: number, { draftLast = false } = {}): NotesStore {
  const store = notesStore({ today: worker.today, participants });
  for (let back = 0; back < count; back += 1) {
    const noteDate = daysAfter(worker.today, -back);
    if (draftLast && back === count - 1) {
      store.startElsewhere(jane.id, noteDate, { authorDisplayName: 'Sam Lee', startedAtUtc: `${noteDate}T00:14:00Z` });
    } else {
      store.addSubmitted(jane.id, noteDate, {
        authorDisplayName: back % 2 === 0 ? priya : 'Alex P.',
        firstSubmittedAtUtc: `${noteDate}T06:12:00Z`,
        narrative: `Note number ${back + 1}.`,
        flagReason: back % 3 === 0 ? 'Seemed tired.' : undefined,
      });
    }
  }

  return store;
}

/** Opens Today as `me`, its rows from `store`, and waits for them. */
async function openToday(page: Page, store: NotesStore, me: Me = worker, latencyMs = 0) {
  await serveMe(page, me);
  await serveToday(page, me.today, participants, store.todayNotes(me));
  await store.serve(page, me, { latencyMs });
  await page.goto('/');
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher sees Past notes, as participants.spec.ts shows it for the Participants screens: marked to
// fail, it passes only if the check after it fails it.
test.fail('a live region put into Past notes with its words fails the test', async ({ page }) => {
  await serveMe(page, worker);
  await janeAndRavi().serve(page, worker);
  await page.goto(pastNotesAddress(jane.id));
  await expect(rows(page)).toHaveCount(3);
  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('role', 'status');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

test('an edited note shows the Edited tag on its row and the Edited line on its read view, passing axe', async ({
  page,
}) => {
  const store = notesStore({ today: worker.today, participants });
  store.addSubmitted(jane.id, worker.today, {
    authorDisplayName: priya,
    firstSubmittedAtUtc: '2026-10-01T06:42:00Z',
    narrative,
    flagReason,
    lastEdit: { by: 'Jo Smith', atUtc: '2026-10-01T22:01:00Z' },
  });
  store.addSubmitted(jane.id, '2026-09-30', {
    authorDisplayName: priya,
    firstSubmittedAtUtc: '2026-09-30T06:42:00Z',
    lastEdit: { by: 'Jo Smith', atUtc: '2026-09-30T07:03:00Z' },
  });
  store.addSubmitted(jane.id, '2026-09-29', { authorDisplayName: priya, firstSubmittedAtUtc: '2026-09-29T06:42:00Z' });
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.goto(pastNotesAddress(jane.id));

  // Past notes: Flagged, then Edited, on the first row; Edited alone on the second; neither on the third.
  await expect(rows(page)).toHaveCount(3);
  await expect(rows(page).first()).toHaveAccessibleDescription(/^Submitted ?, Priya Nair ?, Flagged ?, Edited$/);
  await expect(rows(page).nth(1)).toHaveAccessibleDescription(/^Submitted ?, Priya Nair ?, Edited$/);
  await expect(rows(page).nth(2)).toHaveAccessibleDescription(/^Submitted ?, Priya Nair$/);
  await expect
    .poll(() => shownText(rowItems(page).first().locator('p')))
    .toBe('Submitted · Priya Nair Flagged Edited');
  await expect(main(page).getByText('Edited', { exact: true })).toHaveCount(2);
  await expectAxeToPass(page);

  // The read view: the byline, the Edited line with its date, as it was made on a later day, then the flag line.
  await rows(page).first().click();
  await expect(heading(page)).toBeFocused();
  await expect.poll(() => shownText(byline(page))).toBe(`Written by ${priya} · Submitted 4:42 pm`);
  await expect.poll(() => shownText(editedLine(page))).toBe('Edited last change by Jo Smith, Fri 2 Oct 2026, 8:01 am');
  await expect(editedLine(page).locator('time')).toHaveAttribute('datetime', '2026-10-01T22:01:00Z');
  await expect.poll(() => shownText(flagLine(page))).toBe(`Flagged Reason: ${flagReason}`);
  const bylineBox = await boxOf(byline(page));
  const editedBox = await boxOf(editedLine(page));
  const flagBox = await boxOf(flagLine(page));
  expect(bylineBox.y).toBeLessThan(editedBox.y);
  expect(editedBox.y).toBeLessThan(flagBox.y);
  await expectAxeToPass(page);

  // On the note's own date the time stands alone.
  await page.goto(noteAddress(jane.id, '2026-09-30'));
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  await expect.poll(() => shownText(editedLine(page))).toBe('Edited last change by Jo Smith, 5:03 pm');

  // One version: no Edited line.
  await page.goto(noteAddress(jane.id, '2026-09-29'));
  await expect(heading(page)).toHaveText('Jane Citizen Tuesday 29 September 2026');
  await expect(editedLine(page)).toHaveCount(0);
});

test('the read view loading, flagged, with no goals, submitted later, failed and not found, passing axe', async ({
  page,
}) => {
  const store = janeAndRavi();
  await openToday(page, store);
  await expect.poll(() => shownText(rowLine(page, 'Jane Citizen'))).toBe(`Submitted · ${priya} · 4:42 pm Flagged`);

  // Loading: the static top bar and its back link at once, then "Loading note…" after 1 s, and nothing of the note.
  const held = await holdNext(page, isNote);
  await row(page, 'Jane Citizen').click();
  const note = await held();
  await expect(loadLine(page)).toHaveText('Loading note…');
  await expect(heading(page)).toHaveCount(0);
  await expect(main(page).getByText(priya)).toHaveCount(0);
  await expect(backLink(page, 'Today')).toBeVisible();
  await expect(page).toHaveTitle(title('Note'));
  await expectAxeToPass(page);
  await note.fallback();

  // Flagged: the <h1> focused, the byline, the flag line, each goal ticked or not as text, and the text as written.
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await expect(heading(page).locator('[translate="no"]')).toHaveText('Jane Citizen');
  await expect.poll(() => shownText(byline(page))).toBe(`Written by ${priya} · Submitted 4:42 pm`);
  await expect(byline(page).locator('time')).toHaveAttribute('datetime', '2026-10-01T06:42:00Z');
  expect(await byline(page).locator('time').textContent()).toBe('4:42 pm');
  expect(await heading(page).locator('time').textContent()).toBe('Thursday 1 October 2026');
  await expect.poll(() => shownText(flagLine(page))).toBe(`Flagged Reason: ${flagReason}`);
  await expect(readGoals(page)).toHaveText([`${breakfast.text}, ticked`, `${bus.text}, not ticked`]);
  expect(await readText(page).textContent()).toBe(narrative);
  await expect(readText(page)).toHaveCSS('white-space', 'pre-wrap');
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText([
    '1. Goals',
    '2. Common items',
    '3. Guided notes',
  ]);
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByRole('link')).toHaveText(['Past notes']);
  for (const absent of ['Edit', 'Version history', 'To review', 'Reviewed', 'Review']) {
    await expect(main(page).getByText(absent, { exact: true })).toHaveCount(0);
  }
  await expect(page).toHaveTitle(title('Note'));
  await expectAxeToPass(page);

  // The text is not fetched again as the window comes back, though the same events make Past notes, which opts in to
  // refetchOnWindowFocus, fetch its rows again, so they do reach TanStack Query.
  const sent = recordRequests(page);
  await comeBack(page);
  expect((await sent()).filter((request) => request.includes('/notes'))).toEqual([]);
  await pastNotesLink(page).click();
  await expect(rows(page)).toHaveCount(3);
  const sentOnPastNotes = recordRequests(page);
  await comeBack(page);
  expect(await sentOnPastNotes()).toContain(`GET /api/participants/${jane.id}/notes`);

  // Ravi's, unflagged, with no goals, submitted at midday.
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await row(page, 'Ravi de Silva').click();
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  await expect.poll(() => shownText(byline(page))).toBe(`Written by ${priya} · Submitted midday`);
  await expect(main(page).getByText('No goals set', { exact: true })).toBeVisible();
  await expect(readGoals(page)).toHaveCount(0);
  await expect(flagLine(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // Jane's of 29 September, submitted on 1 October: the time with its date.
  await page.goto(noteAddress(jane.id, '2026-09-29'));
  await expect(heading(page)).toHaveText('Jane Citizen Tuesday 29 September 2026');
  await expect.poll(() => shownText(byline(page))).toBe(`Written by ${priya} · Submitted Thu 1 Oct 2026, 8:05 am`);
  await expectAxeToPass(page);

  // No connection: the fallback <h1> and the form's failure sentence, as the page cannot yet tell which screen it is.
  await failNoteReads(page);
  await page.goto(noteAddress(jane.id, worker.today));
  await expect(loadLine(page)).toHaveText('The note did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Note');
  await expect(tryAgain(page)).toBeVisible();
  await expectAxeToPass(page);

  // Another organisation's participant, and a day before yesterday with no note for a worker: Page not found.
  await store.serve(page, worker);
  for (const address of [noteAddress(randomUUID(), worker.today), noteAddress(jane.id, '2026-09-20')]) {
    await page.goto(address);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle(title('Page not found'));
  }
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', priya, 'Walked to the shop', flagReason);
});

test('the read view is the same from Today and from Past notes, but for its back link', async ({ page }) => {
  const store = janeAndRavi();
  await openToday(page, store);
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  await expect(backLink(page, 'Today')).toHaveAttribute('href', '/');
  const fromToday = await main(page).innerText();

  await pastNotesLink(page).click();
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await row(page, 'Thursday 1 October 2026').click();
  await expect(heading(page)).toBeFocused();
  await expect(backLink(page, 'Past notes')).toHaveAttribute('href', pastNotesAddress(jane.id));
  expect(await main(page).innerText()).toBe(fromToday);
  await backLink(page, 'Past notes').click();
  await expect(row(page, 'Thursday 1 October 2026')).toBeFocused();
});

test('Past notes loading, listed, Show older loading, failed and ended, empty, failed and not found, axe', async ({
  page,
}) => {
  test.slow();
  const store = manyNotes(31, { draftLast: true });
  await serveMe(page, manager);
  await store.serve(page, manager);

  // Loading: the static top bar and its back link at once, then "Loading notes…" after 1 s, with no <h1> or row.
  const held = await holdNext(page, isFirstNotes);
  await page.goto(pastNotesAddress(jane.id));
  const first = await held();
  await expect(loadLine(page)).toHaveText('Loading notes…');
  await expect(heading(page)).toHaveCount(0);
  await expect(rows(page)).toHaveCount(0);
  await expect(backLink(page, 'Today')).toBeVisible();
  await expect(page).toHaveTitle(title('Past notes'));
  await expectAxeToPass(page);
  await first.fallback();

  // Listed: the <h1>, its caption and the name, Edit participant, then the newest 30, newest first, each named by its
  // date alone and described by its status, author and tags, then Show older and its empty status line. A manager's
  // rows have To review after Flagged.
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await expect(editParticipant(page)).toHaveAttribute('href', `/manage/participants/${jane.id}`);
  await expect(main(page).locator('ol[role="list"] > li')).toHaveCount(30);
  await expect(rows(page).first()).toHaveText('Thursday 1 October 2026');
  expect(await rows(page).first().textContent()).toBe('Thursday 1 October 2026');
  await expect(rows(page).first().locator('time')).toHaveAttribute('datetime', worker.today);
  await expect(rows(page).nth(1)).toHaveText('Wednesday 30 September 2026');
  await expect(rows(page).first()).toHaveAccessibleDescription(/^Submitted ?, Priya Nair ?, Flagged ?, To review$/);
  await expect(rows(page).nth(1)).toHaveAccessibleDescription(/^Submitted ?, Alex P\.$/);
  await expect
    .poll(() => shownText(rowItems(page).first().locator('p')))
    .toBe('Submitted · Priya Nair Flagged To review');
  await expect(showOlder(page)).toBeVisible();
  await expect(olderLine(page)).toBeEmpty();
  await expect(main(page).getByText(/\d+ (notes|of)|end of/i)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Past notes'));
  await expectAxeToPass(page);

  // Show older loading: aria-disabled, never disabled, a second press sending nothing, and its line after 1 s.
  const olderRequests: string[] = [];
  page.on('request', (request) => {
    if (isOlderNotes(new URL(request.url()))) {
      olderRequests.push(request.url());
    }
  });
  const heldOlder = await holdNext(page, isOlderNotes);
  await showOlder(page).click();
  const older = await heldOlder();
  await expect(showOlder(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(showOlder(page)).not.toHaveAttribute('disabled');
  await showOlder(page).click({ force: true });
  await expect(olderLine(page)).toHaveText('Loading older notes…');
  await expect(rows(page)).toHaveCount(30);
  await expectAxeToPass(page);
  expect(olderRequests).toEqual([expect.stringContaining(`before=${daysAfter(worker.today, -29)}`)]);

  // Ended: the 31st row below, Sam Lee's draft with no tag, focus on it, and the button gone.
  await older.fallback();
  await expect(rows(page)).toHaveCount(31);
  await expect(rows(page).last()).toBeFocused();
  await expect(rows(page).last()).toHaveAccessibleDescription(/^Draft ?, Sam Lee$/);
  await expect(rowItems(page).last().getByText('Flagged', { exact: true })).toHaveCount(0);
  await expect(showOlder(page)).toHaveCount(0);
  await expect(olderLine(page)).toBeEmpty();
  expect(olderRequests).toHaveLength(1);
  await expectAxeToPass(page);

  // Failed: a new visit starts from the newest 30; with no connection, the sentence with the rows kept and focus on
  // the button, then "still".
  await failNoteReads(page, { olderOnly: true });
  await page.goto(pastNotesAddress(jane.id));
  await expect(rows(page)).toHaveCount(30);
  await showOlder(page).click();
  await expect(olderLine(page)).toHaveText('Older notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(showOlder(page)).toBeFocused();
  await expect(rows(page)).toHaveCount(30);
  await expectAxeToPass(page);
  await showOlder(page).click();
  await expect(olderLine(page)).toHaveText('Older notes still did not load: no connection. Try again.');
  await expect(showOlder(page)).toBeFocused();
  await expectAxeToPass(page);

  // Empty: the sentence alone, with no list or button, and Edit participant still there.
  await store.serve(page, manager);
  await page.goto(pastNotesAddress(ravi.id));
  await expect(main(page).getByText('No notes yet for Ravi de Silva.', { exact: true })).toBeVisible();
  await expect(main(page).getByRole('list')).toHaveCount(0);
  await expect(showOlder(page)).toHaveCount(0);
  await expect(editParticipant(page)).toBeVisible();
  await expectAxeToPass(page);

  // The first load failed: the fallback <h1>, the sentence and Try again, then "still", with focus on Try again.
  await failNoteReads(page);
  await page.goto(pastNotesAddress(jane.id));
  await expect(loadLine(page)).toHaveText('Past notes did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Past notes');
  await expectAxeToPass(page);
  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('Past notes still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expectAxeToPass(page);
  await store.serve(page, manager);
  await tryAgain(page).click();
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await expect(heading(page)).toBeFocused();

  // Another organisation's participant: Page not found.
  await page.goto(pastNotesAddress(randomUUID()));
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  await expectAxeToPass(page);

  // A worker sees no Edit participant and no Export record.
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.goto(pastNotesAddress(jane.id));
  await expect(rows(page)).toHaveCount(30);
  await expect(editParticipant(page)).toHaveCount(0);
  await expect(main(page).getByRole('link', { name: 'Export record', exact: true })).toHaveCount(0);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', priya, 'Sam Lee');
});

test('90 rows loaded, the 75th opened, Back shows all 90 with the scroll and focus kept; a push starts afresh', async ({
  page,
}) => {
  const store = manyNotes(90);
  await openToday(page, store);
  await row(page, 'Jane Citizen').click();
  await pastNotesLink(page).click();
  await expect(heading(page)).toBeFocused();
  await expect(rows(page)).toHaveCount(30);

  // Scrolling to the end, with older notes still to show, asks for nothing: only Show older loads them.
  const sent = recordRequests(page);
  const scrolledTo = await page.evaluate(() => {
    window.scrollTo(0, document.documentElement.scrollHeight);
    return window.scrollY;
  });
  expect(scrolledTo).toBeGreaterThan(0);
  await twoFrames(page);
  expect(await sent()).toEqual([]);
  await expect(rows(page)).toHaveCount(30);
  await expect(showOlder(page)).toBeInViewport({ ratio: 1 });

  for (const loaded of [60, 90]) {
    await showOlder(page).click();
    await expect(rows(page)).toHaveCount(loaded);
    await expect(rows(page).nth(loaded - 30)).toBeFocused();
  }
  await expect(showOlder(page)).toHaveCount(0);

  // The 75th opened, then the back link, and browser Back: every row at once, the scroll where it was, and focus on
  // the 75th.
  const seventyFifth = rows(page).nth(74);
  const date = await seventyFifth.locator('time').getAttribute('datetime');
  await seventyFifth.scrollIntoViewIfNeeded();
  const kept = await page.evaluate(() => window.scrollY);
  expect(kept).toBeGreaterThan(0);
  for (const [way, goBack] of [
    ['the back link', () => backLink(page, 'Past notes').click()],
    ['browser Back', () => page.goBack()],
  ] as const) {
    await seventyFifth.click();
    await expect(page).toHaveURL(noteAddress(jane.id, date ?? ''));
    await expect(heading(page)).toBeFocused();
    await goBack();
    await expect(rows(page), way).toHaveCount(90, { timeout: 500 });
    await expect(loadLine(page)).toBeEmpty();
    await expect(seventyFifth, way).toBeFocused();
    expect(await page.evaluate(() => window.scrollY), way).toBe(kept);
  }

  // From the note, Past notes is a new visit: the newest 30, at the top, with the <h1> focused.
  await seventyFifth.click();
  await pastNotesLink(page).click();
  await expect(heading(page)).toBeFocused();
  await expect(rows(page)).toHaveCount(30);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await expectNothingKept(page, 'Jane', 'Citizen', priya, 'Alex P.', 'Note number');
});

test("on a slow network, Jane's name is never in the DOM of Ravi's read view or Past notes", async ({ page }) => {
  const store = janeAndRavi();
  // Each answer for a participant, a history or a note 1.5 s after it is sent, so that the loading lines show.
  await openToday(page, store, worker, 1_500);
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await pastNotesLink(page).click();
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await row(page, 'Tuesday 29 September 2026').click();
  await expect(heading(page)).toHaveText('Jane Citizen Tuesday 29 September 2026');
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);

  // Every change to the page from now on, away from Today, is checked for her name, in <main> and the top bar.
  await page.evaluate(() => {
    const seen: string[] = [];
    Object.assign(window, { janeSeen: seen });
    new MutationObserver(() => {
      if (location.pathname === '/') {
        return;
      }

      const text = `${document.querySelector('main')?.textContent ?? ''} ${
        document.querySelector('#page-bar')?.textContent ?? ''
      }`;
      if (/Jane|Citizen/.test(text)) {
        seen.push(text);
      }
    }).observe(document.body, { subtree: true, childList: true, characterData: true });
  });
  await row(page, 'Ravi de Silva').click();
  await expect(loadLine(page)).toHaveText('Loading note…');
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  await pastNotesLink(page).click();
  await expect(loadLine(page)).toHaveText('Loading notes…');
  await expect(heading(page)).toHaveText('Past notes Ravi de Silva');
  await expect(rows(page)).toHaveCount(1);
  expect(await page.evaluate(() => (window as unknown as { janeSeen: string[] }).janeSeen)).toEqual([]);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', 'Silva', priya);
});

test('at 320 px with text at 200%, neither the read view nor Past notes scrolls sideways', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const unbroken = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const opening = ['Walked to the shop.', '', unbroken, '<b>hi</b>', '', ''].join('\n');
  const text = `${opening}${'Bought bread and milk. '.repeat(1_000)}`.slice(0, 20_000);
  const store = notesStore({ today: worker.today, participants: [long] });
  for (let back = 0; back < 31; back += 1) {
    const noteDate = daysAfter(worker.today, -back);
    store.addSubmitted(long.id, noteDate, {
      authorDisplayName: 'X'.repeat(100),
      firstSubmittedAtUtc: `${noteDate}T06:12:00Z`,
      narrative: back === 0 ? text : 'Slept in.',
      goals: [{ text: unbroken, isTicked: true }],
      flagReason: 'Y'.repeat(200),
    });
  }
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(long.id, worker.today));
  await setTextTo200Percent(page);

  // The read view: the 20,000 characters whole and literal, blank lines kept, with no element made of them.
  await expect(heading(page)).toContainText('A'.repeat(50));
  expect(text).toHaveLength(20_000);
  expect(await readText(page).textContent()).toBe(text);
  await expect(readText(page).locator('*')).toHaveCount(0);
  expect(await scrollsSideways(page), 'the read view, with a long name, goal, reason and text').toBe(false);
  expect(await heading(page).evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);

  // Past notes, its rows' authors of 100 letters, each Flagged tag on a line of its own, and Show older's failure.
  await failNoteReads(page, { olderOnly: true });
  await page.goto(pastNotesAddress(long.id));
  await expect(rows(page)).toHaveCount(30);
  expect(await scrollsSideways(page), 'Past notes, with a long name and authors').toBe(false);
  const line = rowItems(page).first().locator('p');
  const author = await boxOf(line.getByText('X'.repeat(100)));
  const tag = await boxOf(line.getByText('Flagged', { exact: true }));
  expect(tag.y, 'the tag under the author').toBeGreaterThanOrEqual(author.y + author.height - 1);
  await showOlder(page).click();
  await expect(olderLine(page)).toHaveText('Older notes did not load: no connection. Try again.');
  expect(await scrollsSideways(page), 'Show older failed').toBe(false);
});

test('nothing is sticky on the read view or Past notes, and targets are 44, 48 and 56 px, on phone and laptop', async ({
  page,
}) => {
  const store = manyNotes(31);
  await serveMe(page, manager);
  await store.serve(page, manager);
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 640 });
    await page.goto(noteAddress(jane.id, worker.today));
    await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
    expect(await pinned(page), `the read view at ${width} px`).toEqual([]);
    await expect(page.locator('#page-bar > div')).toHaveCSS('position', 'static');
    await expectAtLeast(backLink(page, 'Today'), 44, 44);
    await expectAtLeast(pastNotesLink(page), 44, 44);

    await page.goto(pastNotesAddress(jane.id));
    await expect(rows(page)).toHaveCount(30);
    expect(await pinned(page), `Past notes at ${width} px`).toEqual([]);
    await expectAtLeast(backLink(page, 'Today'), 44, 44);
    await expectAtLeast(editParticipant(page), 44, 48);
    await expectAtLeast(rowItems(page), 44, 56);
    await expectAtLeast(showOlder(page), 44, 48);

    await failNoteReads(page);
    await page.goto(pastNotesAddress(jane.id));
    await expectAtLeast(tryAgain(page), 48, 48);
    await store.serve(page, manager);
  }
});

test('in forced colours, the ring is Highlight at each stop of the read view and Past notes, tags bordered', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const store = manyNotes(31);
  await openToday(page, store);
  const [highlight, canvasText] = await Promise.all([
    systemColour(page, 'Highlight'),
    systemColour(page, 'CanvasText'),
  ]);
  const forcedRing = { ...ring, colour: highlight };
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  await expect(flagged(page)).toHaveCSS('border-top-style', 'solid');
  await expect(flagged(page)).toHaveCSS('border-top-color', canvasText);
  await expectStops(page, 'Shift+Tab', ['Today'], forcedRing);
  await expectStops(page, 'Tab', ['Past notes'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(heading(page)).toBeFocused();
  const tag = rowItems(page).first().getByText('Flagged', { exact: true });
  await expect(tag).toHaveCSS('border-top-style', 'solid');
  await expect(tag).toHaveCSS('border-top-color', canvasText);
  await expectStops(page, 'Shift+Tab', ['Note'], forcedRing);
  await expectStops(page, 'Tab', [...(await rowNames(page)), 'Show older'], forcedRing);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the read view and Past notes in their Tab order, Show older and Back`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = manyNotes(31);
    await openToday(page, store);
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();

    // participant-notes.md *Focus order (Tab)*, read view, for what is built: the back link, then Past notes.
    await expectStops(page, 'Tab', ['Past notes', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Past notes', 'Today', ...[...shell].reverse()]);
    await expectStops(page, 'Tab', [...shell.slice(1), 'Today', 'Past notes']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();
    await expect(heading(page)).toHaveText('Past notes Jane Citizen');

    // History: the back link, one stop per row, then Show older; a manager's Edit participant comes first (below).
    const names = await rowNames(page);
    expect(names).toHaveLength(30);
    await expectStops(page, 'Shift+Tab', ['Note']);
    await expectStops(page, 'Tab', [...names, 'Show older', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Show older']);
    await page.keyboard.press('Enter');
    await expect(rows(page)).toHaveCount(31);
    await expect(rows(page).last()).toBeFocused();
    await expectStops(page, 'Tab', ['outside the page']);
    await expectStops(page, 'Shift+Tab', [(await rowNames(page))[30]]);

    // Enter on that row opens its note; the back link, by Shift+Tab, returns to the row.
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();
    await expect(heading(page)).toHaveText('Jane Citizen Tuesday 1 September 2026');
    await expectStops(page, 'Shift+Tab', ['Past notes']);
    await page.keyboard.press('Enter');
    await expect(rows(page).last()).toBeFocused();
    await expect(rows(page)).toHaveCount(31);
    await expectStops(page, 'Shift+Tab', [names[29]]);
    await expectStops(page, 'Shift+Tab', [names[28]]);
    await expectNothingKept(page, 'Jane', 'Citizen', priya, 'Note number');

    // A manager's Write past-day note, Export record and Edit participant, between the back link and the rows.
    await serveMe(page, manager);
    await store.serve(page, manager);
    await page.goto(pastNotesAddress(jane.id));
    await heading(page).focus();
    await expectStops(page, 'Tab', ['Write past-day note', 'Export record', 'Edit participant', names[0]]);
    await expectStops(page, 'Shift+Tab', ['Edit participant', 'Export record', 'Write past-day note', 'Today']);
  });
}

// Someone else's draft at the shared note address (S02.04.03; participant-notes.md *Someone else's draft*, items 27
// and 28), made up (A35): Alex P.'s of Jane's today, started at 9:14 am, with Makes own breakfast ticked, a sentence
// and the flag with its reason; Alex P.'s of Ravi's today, left empty; and Alex P.'s of Jane's yesterday. A worker gets
// the in-progress sentence and Past notes; a manager the draft read-only too, with Discard draft and its dialog.

const discardDraft = (page: Page) => main(page).getByRole('button', { name: 'Discard draft', exact: true });
// The draft's flag as a read-only tick row, and its reason under it (participant-notes.md component 9's neighbours).
const flagRow = (page: Page) => main(page).locator('p').filter({ hasText: /^Flag for manager/ });
const reasonLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Reason: / });
// The Discard dialog (confirm-dialog.md's dialog 2, manager variant).
const dialog = (page: Page) => page.getByRole('alertdialog');
// The confirmation's <dialog>, open or closed: the page takes a closed one away once its close event has run, and a key
// pressed before then would meet the one going.
const dialogInPage = (page: Page) => page.locator('dialog[role="alertdialog"]');
const dialogTitle = (page: Page) => dialog(page).getByRole('heading', { level: 2 });
const dialogButtons = (page: Page) => dialog(page).getByRole('button');
const confirmDiscard = (page: Page, name = 'Jane Citizen') =>
  dialog(page).getByRole('button', { name: `Discard draft for ${name}`, exact: true });
const goBack = (page: Page) => dialog(page).getByRole('button', { name: 'Go back', exact: true });
const dialogAlert = (page: Page) => dialog(page).getByRole('alert');
const dialogStatus = (page: Page) => dialog(page).getByRole('status');

// The in-progress sentences (participant-notes.md *States* › *Read view*), for today's note and an earlier day's.
const alexOnToday = 'Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it.';
const alexOnEarlierDay = 'Alex P. started the note for Jane Citizen at 9:14 am. Only Alex P. can finish it.';
const noLongerADraft = 'This draft has already been submitted or discarded.';

// The warning style's red, tokens.css's --colour-error, on the dialog's confirming button alone.
const red = 'rgb(164, 0, 15)';

const draftText = 'Went to the shops.\n\nBought bread.';
const draftReason = 'Seemed tired after the walk.';

/** Whether the response answers a Discard, `POST {base}/discard`. */
const isDiscard = (response: Response) =>
  response.request().method() === 'POST' && /^\/api\/participants\/[^/]+\/notes\/[^/]+\/discard$/.test(
    new URL(response.url()).pathname,
  );

/** Alex P.'s three drafts, as above. */
function alexsDrafts(): NotesStore {
  const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] } });
  store.startElsewhere(jane.id, worker.today, alexStarted);
  store.saveElsewhere(jane.id, worker.today, {
    narrative: draftText,
    tickedGoalIds: [breakfast.id],
    isFlagged: true,
    flagReason: draftReason,
    savedAtUtc: '2026-09-30T23:42:00Z',
  });
  store.startElsewhere(ravi.id, worker.today, alexStarted);
  store.startElsewhere(jane.id, '2026-09-30', { ...alexStarted, startedAtUtc: '2026-09-29T23:14:00Z' });
  return store;
}

test('someone else’s draft: a worker’s view, and a manager’s read-only one, never with the Flagged tag, axe', async ({
  page,
}) => {
  const store = alexsDrafts();
  await openToday(page, store);

  // A worker: the <h1>, the sentence and Past notes, and nothing of the draft, no discard control among it.
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await expect(main(page).getByText(alexOnToday, { exact: true })).toBeVisible();
  await expect(main(page).getByRole('link')).toHaveText(['Past notes']);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveCount(0);
  for (const absent of [breakfast.text, 'Went to the shops', 'Flag for manager', draftReason, 'Flagged']) {
    await expect(main(page).getByText(absent)).toHaveCount(0);
  }
  await expect(page).toHaveTitle(title('Note'));
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Alex', 'Went to the shops', draftReason);

  // A manager: the same sentence, then Discard draft and Past notes, then the draft's sections as the read view shows
  // a note's, its goals in the snapshot's order and its text as written, and its flag as a read-only tick row with the
  // reason, never the Flagged tag; nothing to type in or tick, and no save status.
  await serveMe(page, manager);
  await serveToday(page, manager.today, participants, store.todayNotes(manager));
  await store.serve(page, manager);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  await expect(main(page).getByText(alexOnToday, { exact: true })).toBeVisible();
  await expect(main(page).getByRole('button')).toHaveText(['Discard draft']);
  await expect(main(page).getByRole('link')).toHaveText(['Past notes']);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText([
    '1. Goals',
    '2. Common items',
    '3. Guided notes',
  ]);
  await expect(readGoals(page)).toHaveText([`${breakfast.text}, ticked`, `${bus.text}, not ticked`]);
  expect(await readText(page).textContent()).toBe(draftText);
  await expect(flagRow(page)).toHaveText('Flag for manager, ticked');
  await expect.poll(() => shownText(reasonLine(page))).toBe(`Reason: ${draftReason}`);
  await expect(flagged(page)).toHaveCount(0);
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expect(page.locator('#save-status')).toHaveCount(0);
  // In that order, as it reads.
  expect((await main(page).innerText()).replace(/\s+/g, ' ')).toMatch(
    /^Jane Citizen Thursday 1 October 2026 Alex P\. started .+ Discard draft Past notes 1\. Goals /,
  );
  await expect(page).toHaveTitle(title('Note'));
  await expectAxeToPass(page);

  // Ravi's, unflagged and empty: "not ticked" with no Reason, "No goals set" and "Nothing written yet.".
  await page.goto(noteAddress(ravi.id, manager.today));
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  await expect(flagRow(page)).toHaveText('Flag for manager, not ticked');
  await expect(reasonLine(page)).toHaveCount(0);
  await expect(main(page).getByText('No goals set', { exact: true })).toBeVisible();
  await expect(main(page).getByText('Nothing written yet.', { exact: true })).toBeVisible();
  await expectAxeToPass(page);

  // Jane's from yesterday: "started the note for".
  await page.goto(noteAddress(jane.id, '2026-09-30'));
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  await expect(main(page).getByText(alexOnEarlierDay, { exact: true })).toBeVisible();
  await expect(discardDraft(page)).toBeVisible();
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', 'Alex', 'Went to the shops', draftReason);
});

test('a manager’s Discard draft: the dialog’s lines, Go back and Escape, a 409, and a 204 to Today’s <h1>', async ({
  page,
}) => {
  const store = alexsDrafts();
  await openToday(page, store, manager);
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  const sent = recordRequests(page);

  // Opened on its title, which draws no ring: the name in the title and on the warning-styled confirming button, and
  // who started the draft and when, before the line that always ends the body. The trigger is not red.
  await discardDraft(page).click();
  await expect(dialogTitle(page)).toBeFocused();
  await expect(dialogTitle(page)).toHaveCSS('outline-style', 'none');
  await expect(dialog(page)).toHaveAccessibleName('Discard the draft note for Jane Citizen?');
  await expect(dialog(page)).toHaveAccessibleDescription(
    /^Alex P\. started it at 9:14\sam\. ?Nobody will be able to open this draft again\.$/,
  );
  await expect(dialogButtons(page)).toHaveText(['Discard draft for Jane Citizen', 'Go back']);
  await expect(confirmDiscard(page)).toHaveCSS('background-color', red);
  await expect(discardDraft(page)).not.toHaveCSS('background-color', red);
  await expect(dialogAlert(page)).toBeEmpty();
  await expect(dialogStatus(page)).toBeEmpty();
  await expectAxeToPass(page);

  // Go back, and Escape, close it with nothing sent, focus back on Discard draft.
  await goBack(page).click();
  await expect(dialogInPage(page)).toHaveCount(0);
  await expect(discardDraft(page)).toBeFocused();
  await discardDraft(page).click();
  await expect(dialogTitle(page)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialogInPage(page)).toHaveCount(0);
  await expect(discardDraft(page)).toBeFocused();
  expect((await sent()).filter((request) => request.startsWith('POST'))).toEqual([]);

  // Yesterday's: its date first. Submitted by Alex P. meanwhile, it is a draft no longer: the sentence, the confirming
  // button gone and focus on Go back, which then goes to Today.
  await page.goto(noteAddress(jane.id, '2026-09-30'));
  await discardDraft(page).click();
  await expect(dialog(page)).toHaveAccessibleDescription(
    new RegExp(
      '^This draft is for Wednesday 30\\sSeptember 2026\\. ?Alex P\\. started it at 9:14\\sam\\. ?'
        + 'Nobody will be able to open this draft again\\.$',
    ),
  );
  await expectAxeToPass(page);
  store.submitElsewhere(jane.id, '2026-09-30');
  await confirmDiscard(page).click();
  await expect(dialogAlert(page)).toHaveText(noLongerADraft);
  await expect(confirmDiscard(page)).toHaveCount(0);
  await expect(goBack(page)).toBeFocused();
  await expectAxeToPass(page);
  await goBack(page).click();
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toBeFocused();

  // Today's draft discarded: its 204, then Today in the note's place, its <h1> focused, with no banner, the row with no
  // status, and the draft, its words kept, discarded by the manager.
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await row(page, 'Jane Citizen').click();
  await discardDraft(page).click();
  const discarded = page.waitForResponse(isDiscard);
  await confirmDiscard(page).click();
  expect((await discarded).status()).toBe(204);
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toContainText('Today');
  await expect(main(page).locator('[data-route-focus]')).toHaveCount(0);
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(rowLine(page, 'Jane Citizen')).toHaveCount(0);
  expect(store.discarded(jane.id, manager.today)).toEqual([
    expect.objectContaining({ narrative: draftText, discardedByUserId: manager.userId }),
  ]);

  // Past notes lists it no more: yesterday's note alone, now submitted.
  await row(page, 'Jane Citizen').click();
  await expect(main(page).getByRole('textbox', { name: '3. Guided notes' })).toHaveValue('');
  await pastNotesLink(page).click();
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await expect(rows(page)).toHaveText(['Wednesday 30 September 2026']);
  await expect(rows(page)).toHaveAccessibleDescription(/^Submitted ?, Alex P\.$/);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Alex', 'Went to the shops', draftReason, noLongerADraft);
});

test('at 320 px with text at 200%, no view of someone else’s draft or the Discard dialog scrolls sideways', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const longName = `${'A'.repeat(50)} ${'B'.repeat(50)}`;
  const unbroken = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const store = notesStore({ today: worker.today, participants: [long] });
  store.startElsewhere(long.id, worker.today, { ...alexStarted, authorDisplayName: 'X'.repeat(100) });
  store.saveElsewhere(long.id, worker.today, { narrative: unbroken, isFlagged: true, flagReason: 'Y'.repeat(200) });
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(long.id, worker.today));
  await setTextTo200Percent(page);
  await expect(main(page).getByText(/^X{100} started today’s note for A{50} B{50} at/)).toBeVisible();
  expect(await scrollsSideways(page), 'a worker’s view, with a long name and author').toBe(false);

  await serveMe(page, manager);
  await store.serve(page, manager);
  await page.reload();
  await setTextTo200Percent(page);
  await expect(reasonLine(page)).toBeVisible();
  expect(await scrollsSideways(page), 'a manager’s view, with a long text and reason').toBe(false);

  // The dialog's title and its confirming button each hold the name whole, inside the dialog.
  await discardDraft(page).click();
  await expect(dialogTitle(page)).toHaveText(`Discard the draft note for ${longName}?`);
  expect(await scrollsSideways(page), 'the Discard dialog').toBe(false);
  for (const part of [dialog(page), dialogTitle(page), confirmDiscard(page, longName)]) {
    expect(await part.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    const { x, width } = await boxOf(part);
    expect(x + width).toBeLessThanOrEqual(320);
  }
});

test('someone else’s draft: nothing sticky, Discard draft and the dialog’s buttons 48 px, Past notes 44 px', async ({
  page,
}) => {
  const store = alexsDrafts();
  await serveMe(page, manager);
  await store.serve(page, manager);
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 640 });
    await page.goto(noteAddress(jane.id, manager.today));
    await expect(discardDraft(page)).toBeVisible();
    expect(await pinned(page), `a manager's view at ${width} px`).toEqual([]);
    await expectAtLeast(backLink(page, 'Today'), 44, 44);
    await expectAtLeast(discardDraft(page), 44, 48);
    await expectAtLeast(pastNotesLink(page), 44, 44);
    await discardDraft(page).click();
    await expectAtLeast(dialogButtons(page), 44, 48);
  }
});

test('at 28:00 the warning closes a manager’s Discard dialog, sending nothing; then focus is on Discard draft', async ({
  page,
}) => {
  // Before the page loads, so that its timers and Date run on Playwright's clock, which the test runs on to 28:00.
  await page.clock.install();
  const store = alexsDrafts();
  await serveMe(page, manager);
  await store.serve(page, manager);
  await page.goto(noteAddress(jane.id, manager.today));
  await page.route('**/api/auth/ping', (route) => route.fulfill({ status: 204 }));
  await discardDraft(page).click();
  await expect(dialogTitle(page)).toBeFocused();
  const sent = recordRequests(page);

  await page.clock.runFor('28:00');
  const warning = page.getByRole('dialog', { name: 'You’ll be signed out in 2 minutes' });
  const staySignedIn = warning.getByRole('button', { name: 'Stay signed in' });
  await expect(staySignedIn).toBeFocused();
  await expect(dialogInPage(page)).toHaveCount(0);
  await staySignedIn.click();
  await expect(warning).toBeHidden();
  // Where the closed dialog gave focus back, as its Go back does, and so the warning too.
  await expect(discardDraft(page)).toBeFocused();
  expect(await sent()).toEqual(['POST /api/auth/ping']);
  expect(store.discarded(jane.id, manager.today)).toEqual([]);
});

test('in forced colours, the ring is Highlight at each stop of someone else’s draft and its dialog, bordered', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, manager);
  await alexsDrafts().serve(page, manager);
  await page.goto(noteAddress(jane.id, manager.today));
  const forcedRing = { ...ring, colour: await systemColour(page, 'Highlight') };
  await heading(page).focus();
  await expectStops(page, 'Shift+Tab', ['Today'], forcedRing);
  await expectStops(page, 'Tab', ['Discard draft', 'Past notes'], forcedRing);
  await expectStops(page, 'Shift+Tab', ['Discard draft'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(dialogTitle(page)).toBeFocused();
  await expectStops(page, 'Tab', ['Discard draft for Jane Citizen', 'Go back'], forcedRing);

  // The warning button's fill gives way to the theme's, so its border is what draws it, in the theme's button text.
  const buttonText = await systemColour(page, 'ButtonText');
  for (const side of ['top', 'right', 'bottom', 'left']) {
    await expect(confirmDiscard(page)).toHaveCSS(`border-${side}-style`, 'solid');
    await expect(confirmDiscard(page)).toHaveCSS(`border-${side}-width`, '2px');
    await expect(confirmDiscard(page)).toHaveCSS(`border-${side}-color`, buttonText);
  }
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: someone else's draft in each view's Tab order, and its dialog by keys`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = alexsDrafts();
    await openToday(page, store);
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();

    // A worker's (participant-notes.md *Focus order (Tab)*): the back link, then Past notes.
    await expectStops(page, 'Tab', ['Past notes', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Past notes', 'Today', ...[...shell].reverse()]);

    // A manager's: the back link, Discard draft, then Past notes.
    await serveMe(page, manager);
    await serveToday(page, manager.today, participants, store.todayNotes(manager));
    await store.serve(page, manager);
    await page.goto(noteAddress(jane.id, manager.today));
    await heading(page).focus();
    await expectStops(page, 'Tab', ['Discard draft', 'Past notes', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Past notes', 'Discard draft', 'Today', 'Manage']);
    await expectStops(page, 'Tab', ['Today', 'Discard draft']);

    // Enter opens the dialog on its title, then its two buttons; Go back by Enter, and Escape, each give focus back to
    // Discard draft, with nothing sent.
    const sent = recordRequests(page);
    await page.keyboard.press('Enter');
    await expect(dialogTitle(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Discard draft for Jane Citizen', 'Go back']);
    await page.keyboard.press('Enter');
    await expect(dialogInPage(page)).toHaveCount(0);
    await expect(discardDraft(page)).toBeFocused();
    await page.keyboard.press('Space');
    await expect(dialogTitle(page)).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(dialogInPage(page)).toHaveCount(0);
    await expect(discardDraft(page)).toBeFocused();
    expect((await sent()).filter((request) => request.startsWith('POST'))).toEqual([]);

    // Enter on the confirming button discards it: Today, its <h1> focused, then its stops.
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Discard draft for Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Find a participant', 'Jane Citizen', 'Ravi de Silva', 'outside the page']);
    await expectNothingKept(page, 'Jane', 'Citizen', 'Alex', 'Went to the shops', draftReason);
  });
}
