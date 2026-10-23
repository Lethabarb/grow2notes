import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import { boxOf, scrollsSideways, shownText } from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe } from './me.ts';
import { notesStore, type NotesStore } from './notes.ts';
import { jane, participants } from './participants.ts';

// Mark reviewed and the re-flag after an edit in the published app (S03.02.01 to S03.02.03, S03.01.02), against
// participant-notes.md's criteria for the read view's review lines and the Review panel, as T16 builds them. Each test
// answers /api/auth/me, the To review list and the notes in the browser, with a made-up manager and a flagged note
// (me.ts, notes.ts), so that none of those requests reaches the app.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const navItem = (page: Page, name: string) => nav(page).getByRole('link', { name, exact: true });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const noneToReview = (page: Page) => main(page).getByText('No flagged notes to review.', { exact: true });
const editLink = (page: Page) => main(page).getByRole('link', { name: 'Edit', exact: true });
const pastNotesLink = (page: Page) => main(page).getByRole('link', { name: 'Past notes', exact: true });
const narrativeBox = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const reasonBox = (page: Page) => main(page).getByRole('textbox', { name: 'Reason', exact: true });
const flagTick = (page: Page) => main(page).locator('label[for="flag-for-manager"]').locator('span');
// Either name, because a busy button renames itself while its request is out.
const saveChanges = (page: Page) => main(page).getByRole('button', { name: /^(Save changes|Saving changes…)$/ });
const cancel = (page: Page) => main(page).getByRole('button', { name: /^(Cancel|Cancelling…)$/ });

// The Review panel: its section, found by its heading, the comment box, Mark reviewed, the alert above the button and,
// once the form has gone, the line that focus moves to.
const panelHeading = (page: Page) => main(page).getByRole('heading', { level: 2, name: 'Review', exact: true });
const panel = (page: Page) => main(page).locator('section').filter({ has: panelHeading(page) });
const comment = (page: Page) => main(page).getByRole('textbox', { name: 'Comment (optional)', exact: true });
const markReviewed = (page: Page) =>
  main(page).getByRole('button', { name: /^(Mark reviewed|Marking reviewed…)$/ });
const requestAlert = (page: Page) => main(page).locator('form [role="alert"]');
const record = (page: Page) => panel(page).locator('p[tabindex="-1"]');
const whileOpen = (page: Page) =>
  main(page).getByText('This note was marked reviewed while you had it open.', { exact: true });
// A status line under the <h1>, found by what it starts with.
const statusLine = (page: Page, start: RegExp) => main(page).locator('p').filter({ hasText: start });

/** The address of `participantId`'s note for `noteDate`. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;

const noteDate = manager.today;
const address = noteAddress(jane.id, noteDate);
const editAddress = `${address}/edit`;
const janesHeading = 'Jane Citizen Thursday 1 October 2026';

const reason = 'Seemed upset about moving house.';
const changedReason = 'Seemed upset about moving house, and limped.';
const firstText = 'Jane stayed in until eleven.';

/** Jo Smith, another manager, who reviews a note before, or after, Sam Lee does. */
const joSmith = { id: '2f6d8b1c-4e07-4a93-b5d2-8c1e7a3f9d60', displayName: 'Jo Smith' };

/** The store's time for a review, 4:12 pm in Melbourne on 1 October 2026, as the record writes it. */
const reviewedAtFourTwelve = 'Thu 1 Oct 2026, 4:12 pm';

/** Jane Citizen's note for today, written by Priya Nair at 4:42 pm, flagged for a manager, so To review. */
function flaggedNote(): NotesStore {
  const store = notesStore({ today: manager.today, participants });
  store.addSubmitted(jane.id, noteDate, {
    authorDisplayName: 'Priya Nair',
    firstSubmittedAtUtc: '2026-10-01T06:42:00Z',
    narrative: firstText,
    flagReason: reason,
  });
  return store;
}

/** Serves the made-up manager's me, To review list and notes from `store`. */
async function serveManager(page: Page, store: NotesStore) {
  await serveMe(page, manager);
  await store.serve(page, manager);
}

/** From Flagged, opens Jane's note: the read view, its <h1> focused, and its panel. */
async function openFromFlagged(page: Page, store: NotesStore) {
  await serveManager(page, store);
  await page.goto('/flagged');
  await expect(rows(page)).toHaveCount(1);
  await rows(page).click();
  await expect(page).toHaveURL(address);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(heading(page)).toBeFocused();
  await expect(panelHeading(page)).toBeVisible();
}

/** Counts the requests for `GET /api/auth/me` that the page sends from now on. */
function countMeRequests(page: Page): () => number {
  let count = 0;
  page.on('request', (request) => {
    if (new URL(request.url()).pathname === '/api/auth/me') {
      count += 1;
    }
  });
  return () => count;
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

test('from Flagged: a comment, Mark reviewed, the record focused, me asked for again, and the row gone from Flagged', async ({
  page,
}) => {
  const store = flaggedNote();
  const meRequests = countMeRequests(page);
  await openFromFlagged(page, store);

  // The default: the tag after the flag line, the panel under the sections, Mark reviewed available.
  await expect(statusLine(page, /^To review/)).toBeVisible();
  await expect(comment(page)).toHaveAccessibleDescription(
    /^Shown in the daily report and record exports, and cannot be changed later\s+You can enter up to 500 characters$/,
  );
  await expect(markReviewed(page)).not.toHaveAttribute('aria-disabled', 'true');
  await expectAxeToPass(page);

  await comment(page).fill('Called her mum.\nGP booked.');
  const before = meRequests();
  await markReviewed(page).click();

  // Success: the record, focused, with its comment; the status line now Reviewed; me asked for again.
  await expect(record(page)).toBeFocused();
  await expect
    .poll(() => shownText(record(page)))
    .toBe(`Reviewed by ${manager.displayName} on ${reviewedAtFourTwelve}`);
  await expect(panel(page)).toContainText('Comment: Called her mum.');
  await expect(comment(page)).toHaveCount(0);
  await expect(markReviewed(page)).toHaveCount(0);
  await expect(statusLine(page, /^Reviewed/).first()).toBeVisible();
  await expect(statusLine(page, /^To review/)).toHaveCount(0);
  await expect(panelHeading(page)).toBeVisible();
  expect(store.reviewRequests).toEqual([{ versionNumber: 1, comment: 'Called her mum.\nGP booked.' }]);
  expect(store.submitted(jane.id, noteDate)?.flagStatus).toBe('Reviewed');
  await expect.poll(meRequests).toBeGreaterThan(before);
  await expectAxeToPass(page);

  // Back on Flagged the row has gone, with no reload.
  await backLink(page, 'Flagged').click();
  await expect(page).toHaveURL('/flagged');
  await expect(noneToReview(page)).toBeVisible();
  await expect(rows(page)).toHaveCount(0);
  await expectNothingKept(page, firstText, reason, 'Called her mum.');
});

test('a comment over 500 characters is refused with a message and focus on the box, and clears once it fits', async ({
  page,
}) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);

  await comment(page).fill('x'.repeat(501));
  await markReviewed(page).click();

  await expect(main(page).getByText('Comment must be 500 characters or less', { exact: true })).toBeVisible();
  await expect(comment(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(comment(page)).toBeFocused();
  expect(store.reviewRequests).toEqual([]);
  await expectAxeToPass(page);

  await comment(page).fill('x'.repeat(500));
  await expect(main(page).getByText('Comment must be 500 characters or less', { exact: true })).toHaveCount(0);
  await expect(comment(page)).not.toHaveAttribute('aria-invalid', 'true');
});

test('a review whose answer is lost says so, keeps the comment, and shows success on the second press', async ({
  page,
}) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);
  await comment(page).fill('Called her mum.');
  store.loseNextReviewAnswer();

  await markReviewed(page).click();

  // The review landed at the server, but its answer never reached the page: the failure, the comment and the button.
  await expect(requestAlert(page)).toHaveText('Not marked reviewed: no connection. Try again.');
  await expect(markReviewed(page)).toBeFocused();
  await expect(comment(page)).toHaveValue('Called her mum.');
  await expectAxeToPass(page);

  // The second press meets 409, finds its own review at that version, and shows what it was: a success.
  await markReviewed(page).click();
  await expect(record(page)).toBeFocused();
  await expect
    .poll(() => shownText(record(page)))
    .toBe(`Reviewed by ${manager.displayName} on ${reviewedAtFourTwelve}`);
  await expect(panel(page)).toContainText('Comment: Called her mum.');
  await expect(whileOpen(page)).toHaveCount(0);
  expect(store.reviewRequests).toEqual([
    { versionNumber: 1, comment: 'Called her mum.' },
    { versionNumber: 1, comment: 'Called her mum.' },
  ]);
  await expectAxeToPass(page);
});

test('a note that another manager reviewed first shows their record and the comment that was not saved', async ({
  page,
}) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);
  await comment(page).fill('Looks fine to me.');
  store.reviewElsewhere(jane.id, noteDate, joSmith, 'Already followed up.');

  await markReviewed(page).click();

  await expect(whileOpen(page)).toBeFocused();
  await expect(panel(page)).toContainText(`Reviewed by ${joSmith.displayName} on ${reviewedAtFourTwelve}`);
  await expect(panel(page)).toContainText('Comment: Already followed up.');
  await expect(panel(page)).toContainText('Your comment was not saved:');
  await expect(panel(page)).toContainText('Looks fine to me.');
  await expect(comment(page)).toHaveCount(0);
  await expect(markReviewed(page)).toHaveCount(0);
  expect(store.reviewRequests).toEqual([{ versionNumber: 1, comment: 'Looks fine to me.' }]);
  await expectAxeToPass(page);
});

test('a note edited since it was opened shows the new text, keeps the comment, and the next press names the new version', async ({
  page,
}) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);
  await comment(page).fill('Will check both.');
  store.editElsewhere(jane.id, noteDate, joSmith.displayName, { narrative: 'Jane stayed in, then ate lunch.' });

  await markReviewed(page).click();

  await expect(requestAlert(page)).toHaveText(
    'This note was edited after you opened it. Read the latest version, then mark it reviewed.',
  );
  await expect(main(page).getByText('Jane stayed in, then ate lunch.', { exact: true })).toBeVisible();
  await expect(comment(page)).toHaveValue('Will check both.');
  await expect(markReviewed(page)).toBeFocused();
  await expect(panelHeading(page)).toBeVisible();
  await expectAxeToPass(page);

  await markReviewed(page).click();
  await expect(record(page)).toBeFocused();
  expect(store.reviewRequests).toEqual([
    { versionNumber: 1, comment: 'Will check both.' },
    { versionNumber: 2, comment: 'Will check both.' },
  ]);
});

test('an unsent comment survives Edit and Cancel, is lost by a reload, and is never kept in the browser', async ({
  page,
}) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);
  await comment(page).fill('Not decided yet.');

  await editLink(page).click();
  await expect(page).toHaveURL(editAddress);
  await expect(narrativeBox(page)).toBeVisible();
  await expectNothingKept(page, 'Not decided yet.');
  await cancel(page).click();
  await expect(page).toHaveURL(address);

  await expect(comment(page)).toHaveValue('Not decided yet.');
  await expectNothingKept(page, 'Not decided yet.');

  await page.reload();
  await expect(panelHeading(page)).toBeVisible();
  await expect(comment(page)).toHaveValue('');
  await expectNothingKept(page, 'Not decided yet.');
});

test('a manager’s edit of a Reviewed note with a changed reason puts it in To review again, and unticking keeps it there', async ({
  page,
}) => {
  const store = notesStore({ today: manager.today, participants });
  store.addSubmitted(jane.id, noteDate, {
    authorDisplayName: 'Priya Nair',
    firstSubmittedAtUtc: '2026-10-01T06:42:00Z',
    narrative: firstText,
    flagReason: reason,
    flagStatus: 'Reviewed',
    reviews: [
      {
        versionNumber: 1,
        reviewedBy: joSmith,
        reviewedAtUtc: '2026-10-01T06:12:00Z',
        comment: 'Followed up.',
      },
    ],
  });
  await serveManager(page, store);

  // Reviewed when opened: the record in the status lines, and no panel; the list holds nothing.
  await page.goto(address);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect.poll(() => shownText(statusLine(page, /^Reviewed/))).toBe(
    `Reviewed by ${joSmith.displayName} on ${reviewedAtFourTwelve}`,
  );
  await expect(main(page).getByText('Comment: Followed up.', { exact: true })).toBeVisible();
  await expect(panelHeading(page)).toHaveCount(0);
  await expect(comment(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // A changed reason raises the flag again: the note reads To review, with the panel, and is on Flagged at once.
  await editLink(page).click();
  await expect(reasonBox(page)).toHaveValue(reason);
  await reasonBox(page).fill(changedReason);
  await saveChanges(page).click();
  await expect(page).toHaveURL(address);
  await expect(statusLine(page, /^To review/)).toBeVisible();
  await expect(panelHeading(page)).toBeVisible();
  expect(store.submitted(jane.id, noteDate)?.flagStatus).toBe('ToReview');
  await navItem(page, 'Flagged').click();
  await expect(rows(page)).toHaveCount(1);
  await expect(main(page)).toContainText(`Reason: ${changedReason}`);

  // Unticking the flag in a later edit keeps the note in To review, on the reason that raised it.
  await rows(page).click();
  await expect(page).toHaveURL(address);
  await editLink(page).click();
  await flagTick(page).click();
  await saveChanges(page).click();
  await expect(page).toHaveURL(address);
  await expect.poll(() => shownText(statusLine(page, /^To review/))).toBe('To review Flag removed in a later edit');
  await expect(main(page)).toContainText(`Reason: ${changedReason}`);
  await expect(panelHeading(page)).toBeVisible();
  expect(store.submitted(jane.id, noteDate)?.flagStatus).toBe('ToReview');
  await expectAxeToPass(page);
});

test('the back label stays Flagged across Edit, Cancel and Save changes', async ({ page }) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);
  await expect(backLink(page, 'Flagged')).toBeVisible();

  await editLink(page).click();
  await expect(page).toHaveURL(editAddress);
  await expect(backLink(page, 'Flagged')).toBeVisible();
  await cancel(page).click();
  await expect(page).toHaveURL(address);
  await expect(backLink(page, 'Flagged')).toBeVisible();

  await editLink(page).click();
  await narrativeBox(page).fill('Jane stayed in until noon.');
  await saveChanges(page).click();
  await expect(page).toHaveURL(address);
  await expect(main(page).getByText('Jane stayed in until noon.', { exact: true })).toBeVisible();
  await expect(backLink(page, 'Flagged')).toBeVisible();
});

test('keyboard only: the back link, Edit, Past notes, the comment, then Mark reviewed', async ({ page }) => {
  const store = flaggedNote();
  await openFromFlagged(page, store);

  await expectStops(page, 'Shift+Tab', ['Flagged']);
  await expectStops(page, 'Tab', ['Edit', 'Past notes', 'Comment (optional)', 'Mark reviewed']);
  await expect(pastNotesLink(page)).toBeVisible();
});

test('at 375 px nothing scrolls sideways, Mark reviewed is as wide as the box, and the comment is at least 16 px', async ({
  page,
}) => {
  const store = flaggedNote();
  await page.setViewportSize({ width: 375, height: 812 });
  await serveManager(page, store);
  await page.goto(address);
  await expect(panelHeading(page)).toBeVisible();

  await comment(page).fill('X'.repeat(500));
  expect(await scrollsSideways(page), 'the panel, with a comment of 500 unbroken characters').toBe(false);
  const [field, button] = [await boxOf(comment(page)), await boxOf(markReviewed(page))];
  expect(Math.abs(field.width - button.width), 'the button as wide as the box').toBeLessThanOrEqual(1);
  const size = await comment(page).evaluate((box) => parseFloat(getComputedStyle(box).fontSize));
  expect(size, 'the comment’s font size, so that iOS does not zoom').toBeGreaterThanOrEqual(16);

  await markReviewed(page).click();
  await expect(record(page)).toBeFocused();
  expect(await scrollsSideways(page), 'the record, with a comment of 500 unbroken characters').toBe(false);
});
