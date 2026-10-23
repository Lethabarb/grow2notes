import type { Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops } from './focus.ts';
import { breakfast, bus } from './goals.ts';
import { savedPrompts } from './guidePrompts.ts';
import { expectNothingKept } from './kept.ts';
import { scrollsSideways, shownText } from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { failEditRequests, notesStore, type NotesStore } from './notes.ts';
import { jane, participants } from './participants.ts';
import { holdNext } from './requests.ts';
import { serveMyDrafts, serveToday } from './today.ts';

// Editing a submitted note in the published app (S02.06.01 to S02.06.03), against note-form.md's criteria for edit
// mode: the Edit action, Save changes and Cancel, the version-conflict notice and the cannot-edit page, as T14
// builds them. Each test answers /api/auth/me, /api/today, /api/me/drafts, the participant's header, the note, its
// working copy, Save changes and Cancel in the browser, with made-up people and goals (me.ts, today.ts, notes.ts), so
// that none of those requests reaches the app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const saveStatus = (page: Page) => page.locator('#save-status');
const box = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const editLink = (page: Page) => main(page).getByRole('link', { name: 'Edit', exact: true });
const pastNotesLink = (page: Page) => main(page).getByRole('link', { name: 'Past notes', exact: true });
// Either name, because a busy button renames itself while its request is out.
const saveChanges = (page: Page) => main(page).getByRole('button', { name: /^(Save changes|Saving changes…)$/ });
const cancel =(page: Page) => main(page).getByRole('button', { name: /^(Cancel|Cancelling…)$/ });
// The alert above the buttons, where a request that failed says so.
const requestAlert = (page: Page) => main(page).locator('form [role="alert"]');
const byline = (page: Page) => main(page).locator('p').filter({ hasText: /^Written by / });
const editedLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Edited/ });
const readText = (page: Page) =>
  main(page).getByRole('heading', { name: '3. Guided notes', exact: true }).locator('xpath=following-sibling::div[1]');
// The version-conflict notice (note-form.md component 6), a region named by its heading.
const notice = (page: Page) => main(page).getByRole('region', { name: /while you were editing/ });
const startAgain = (page: Page) => notice(page).getByRole('button', { name: 'Start again from the latest version' });

/** The address of `participantId`'s note for `noteDate`. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;

const address = noteAddress(jane.id, worker.today);
const editAddress = `${address}/edit`;
const janesHeading = 'Jane Citizen Thursday 1 October 2026';

/** Riley Chen, another worker, who did not write Jane's note and may not edit it. */
const riley: Me = { ...worker, userId: '7e2c5a91-3b84-4d06-8f1a-9c0d6b3e5a72', displayName: 'Riley Chen' };

/** What the author wrote, and what the edits write. */
const firstText = 'Went to the shops.';
const editedText = 'Went to the shops. Then home for lunch.';

/** "Saved 4:12 pm", the store's time for each save, with its non-breaking space (microcopy.md §3). */
const savedAtFourTwelve = 'Saved 4:12 pm';

/**
 * Jane Citizen's note for Thursday 1 October 2026, written by the worker, Alex Pham, at 3:10 pm with Breakfast ticked
 * and Bus not, at version 1, or, with `edited`, at version 2, last changed by Jo Smith; and her two goals.
 */
function janesNote({ edited = false } = {}): NotesStore {
  const store = notesStore({
    today: worker.today,
    participants,
    goals: { [jane.id]: [breakfast, bus] },
    prompts: savedPrompts,
  });
  store.addSubmitted(jane.id, worker.today, {
    authorDisplayName: worker.displayName,
    authorUserId: worker.userId,
    firstSubmittedAtUtc: '2026-10-01T05:10:00Z',
    narrative: firstText,
    goals: [
      { id: breakfast.id, text: breakfast.text, isTicked: true },
      { id: bus.id, text: bus.text, isTicked: false },
    ],
    ...(edited ? { lastEdit: { by: 'Jo Smith', atUtc: '2026-10-01T05:40:00Z' } } : {}),
  });
  return store;
}

/** Serves `me`'s Today, drafts and notes from `store`, and opens Jane's note at its address, the read view. */
async function openRead(page: Page, store: NotesStore, me: Me = worker, path = address) {
  await serveMe(page, me);
  await serveToday(page, me.today, participants, store.todayNotes(me));
  await serveMyDrafts(page, store.myDrafts(me));
  await store.serve(page, me);
  await page.goto(path);
}

/** Opens Jane's note as `me`, then Edit, and waits for the edit form's <h1>. */
async function openEdit(page: Page, store: NotesStore, me: Me = worker) {
  await openRead(page, store, me);
  await expect(heading(page)).toHaveText(janesHeading);
  await editLink(page).click();
  await expect(page).toHaveURL(editAddress);
  await expect(heading(page)).toHaveText(/^Editing submitted note \(version \d+\) Jane Citizen /);
}

/** Whether the response answers a Save changes, `POST {base}/versions`. */
const isVersion = (response: Response) =>
  response.request().method() === 'POST' && /\/notes\/[^/]+\/versions$/.test(new URL(response.url()).pathname);

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  // But the save indicator of a reopened pending edit, which the form puts in anew with its first words, "Saved …", so
  // that they are the page's own rather than announced as a change (autosave-status.md *Behaviour* 8).
  const announcedLate = liveRegionsAddedWithWords.filter((region) => !/^<p id="save-status"[^>]*>Saved/.test(region));
  expect(announcedLate, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

test('an author edits a submitted note, saves, and sees the Edited line, then the Edited tag on Past notes', async ({
  page,
}) => {
  const store = janesNote();
  await openRead(page, store);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(editedLine(page)).toHaveCount(0);

  // Edit is first in the action row, a link that replaces the entry; the form opens on the note as it is.
  await expect(main(page).getByRole('link')).toHaveText(['Edit', 'Past notes']);
  await editLink(page).click();
  await expect(page).toHaveURL(editAddress);
  await expect(page).toHaveTitle(title('Note'));
  await expect(heading(page)).toHaveText(`Editing submitted note (version 1) ${janesHeading}`);
  await expect(heading(page)).toBeFocused();
  await expect(saveStatus(page)).toBeEmpty();
  await expect(box(page)).toHaveValue(firstText);
  await expect(main(page).getByRole('button', { name: 'More actions' })).toHaveCount(0);
  await expect(saveChanges(page)).toBeVisible();
  await expect(cancel(page)).toBeVisible();

  // The first change makes the pending edit, answered 200, and "Saved", never "Draft".
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(store.saves.map(({ baseVersion }) => baseVersion)).toEqual([1]);
  expect(store.pendingEdit(jane.id, worker.today, worker)).toMatchObject({ narrative: editedText, baseVersion: 1 });
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 1, narrative: firstText });

  // Save changes: the check save, then version 2 at base 1, and the read view in the form's place, its <h1> focused.
  await saveChanges(page).click();
  await expect(page).toHaveURL(address);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(heading(page)).toBeFocused();
  await expect(readText(page)).toHaveText(editedText);
  await expect(main(page).locator('form')).toHaveCount(0);
  expect(store.submits.map(({ body }) => body.baseVersion)).toEqual([1]);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({
    currentVersionNumber: 2,
    narrative: editedText,
    authorUserId: worker.userId,
  });
  expect(store.pendingEdit(jane.id, worker.today, worker)).toBeUndefined();

  // The Edited line after the byline, and the Edited tag on Past notes.
  await expect.poll(() => shownText(byline(page))).toMatch(/^Written by Alex Pham · Submitted 3:10 pm$/);
  await expect.poll(() => shownText(editedLine(page))).toBe('Edited last change by Alex Pham, 4:12 pm');
  await pastNotesLink(page).click();
  await expect(main(page).getByText('Edited', { exact: true })).toHaveCount(1);
  await expectNothingKept(page, 'Went to the shops');
});

test('Cancel leaves the note unchanged, deleting the pending edit, and lands on the read view', async ({ page }) => {
  const store = janesNote();
  await openEdit(page, store);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(store.pendingEdit(jane.id, worker.today, worker)).toBeDefined();

  await cancel(page).click();

  await expect(page).toHaveURL(address);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(heading(page)).toBeFocused();
  await expect(readText(page)).toHaveText(firstText);
  await expect(editedLine(page)).toHaveCount(0);
  expect(store.cancels).toHaveLength(1);
  expect(store.pendingEdit(jane.id, worker.today, worker)).toBeUndefined();
  expect(store.submits).toEqual([]);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 1, narrative: firstText });
  expect(store.saves).toHaveLength(1);
});

test('a pending edit, reopened, shows its words, "Saved", and no Draft, and Cancel deletes it', async ({ page }) => {
  const store = janesNote();
  store.startEdit(jane.id, worker.today, worker, { narrative: editedText, savedAtUtc: '2026-10-01T06:12:00Z' });
  await openRead(page, store, worker, editAddress);

  await expect(heading(page)).toHaveText(`Editing submitted note (version 1) ${janesHeading}`);
  await expect(box(page)).toHaveValue(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect(saveStatus(page)).not.toContainText('Draft');
  expect(store.saves).toEqual([]);
  await cancel(page).click();
  await expect(readText(page)).toHaveText(firstText);
  expect(store.pendingEdit(jane.id, worker.today, worker)).toBeUndefined();
});

test('a worker who did not write the note sees no Edit, and /edit gives the cannot-edit page', async ({ page }) => {
  const store = janesNote();
  await openRead(page, store, riley);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(main(page).getByRole('link')).toHaveText(['Past notes']);
  await expect(editLink(page)).toHaveCount(0);

  await page.goto(editAddress);

  await expect(heading(page)).toHaveText('You cannot edit this note');
  await expect(main(page)).toContainText('Only the person who wrote it, or a manager, can edit it.');
  await expect(page).toHaveTitle(title('Note'));
  await expect(page).toHaveURL(editAddress);
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expectAxeToPass(page);
  expect(store.saves).toEqual([]);

  await main(page).getByRole('link', { name: 'Read the note', exact: true }).click();
  await expect(page).toHaveURL(address);
  await expect(heading(page)).toHaveText(janesHeading);
  await expect(readText(page)).toHaveText(firstText);
});

test('a manager edits a worker’s note, and the byline keeps the worker', async ({ page }) => {
  const store = janesNote();
  await openEdit(page, store, manager);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);

  await saveChanges(page).click();

  await expect(page).toHaveURL(address);
  await expect(readText(page)).toHaveText(editedText);
  await expect.poll(() => shownText(byline(page))).toMatch(/^Written by Alex Pham · Submitted 3:10 pm$/);
  await expect.poll(() => shownText(editedLine(page))).toBe('Edited last change by Sam Lee, 4:12 pm');
  expect(store.submitted(jane.id, worker.today)).toMatchObject({
    authorUserId: worker.userId,
    authorDisplayName: worker.displayName,
    currentVersionNumber: 2,
  });
});

test('two editors meet the notice, Start again keeps the changes, and Save changes makes version 4', async ({
  page,
}) => {
  test.slow();
  const store = janesNote({ edited: true });
  await openEdit(page, store);
  await expect(heading(page)).toHaveText(`Editing submitted note (version 2) ${janesHeading}`);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);

  // The manager, on a page of their own, saves version 3 from the same version 2.
  const second = await page.context().newPage();
  await openEdit(second, store, manager);
  await box(second).fill('The manager’s words.');
  await expect(saveStatus(second)).toHaveText(savedAtFourTwelve);
  await saveChanges(second).click();
  await expect(second).toHaveURL(address);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 3 });
  await second.close();

  // The author's Save changes meets the newer version: the notice, focused, with it laid out, and nothing saved.
  await saveChanges(page).click();
  await expect(notice(page)).toBeFocused();
  await expect(notice(page).getByRole('heading', { name: /while you were editing/ })).toHaveText(
    'Sam Lee saved version 3 at 4:12 pm while you were editing.',
  );
  await expect(notice(page)).toContainText('Your changes are still below.');
  await expect(notice(page).locator('details')).toHaveAttribute('open', '');
  await expect(notice(page).getByText('Version 3 by Sam Lee', { exact: true })).toBeVisible();
  await expect(notice(page)).toContainText('The manager’s words.');
  await expect(startAgain(page)).toBeVisible();
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expect(saveChanges(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(box(page)).toHaveValue(editedText);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 3 });
  await expectAxeToPass(page);

  // Start again: the changes kept, the caption on version 3, the details closed, the <h1> focused, "Saved".
  await startAgain(page).click();
  await expect(heading(page)).toHaveText(`Editing submitted note (version 3) ${janesHeading}`);
  await expect(heading(page)).toBeFocused();
  await expect(notice(page).locator('details')).not.toHaveAttribute('open');
  await expect(startAgain(page)).toHaveCount(0);
  await expect(box(page)).toHaveValue(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect(saveChanges(page)).not.toHaveAttribute('aria-disabled');
  expect(store.pendingEdit(jane.id, worker.today, worker)).toMatchObject({ baseVersion: 3, narrative: editedText });

  await saveChanges(page).click();

  await expect(page).toHaveURL(address);
  await expect(readText(page)).toHaveText(editedText);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 4, narrative: editedText });
  expect(store.submits.map(({ body }) => body.baseVersion)).toEqual([2, 2, 3]);
  await expect.poll(() => shownText(editedLine(page))).toBe('Edited last change by Alex Pham, 4:12 pm');
});

test('a conflict met by a background save shows the notice without moving focus', async ({ page }) => {
  const store = janesNote({ edited: true });
  await openEdit(page, store);
  // Another editor saves version 3 before this page's first save, which is based on version 2.
  store.editElsewhere(jane.id, worker.today, 'Sam Lee', { narrative: 'The manager’s words.' });

  await box(page).focus();
  await box(page).pressSequentially(' Then home.');

  await expect(notice(page)).toBeVisible();
  await expect(notice(page).getByRole('heading', { name: /while you were editing/ })).toHaveText(
    'Sam Lee saved version 3 at 4:12 pm while you were editing.',
  );
  await expect(box(page)).toBeFocused();
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expect(saveChanges(page)).toHaveAttribute('aria-disabled', 'true');
  expect(store.pendingEdit(jane.id, worker.today, worker)).toBeUndefined();
  expect(store.submits).toEqual([]);

  // A press of Save changes while it waits focuses the notice and sends nothing.
  await saveChanges(page).click({ force: true });
  await expect(notice(page)).toBeFocused();
  expect(store.submits).toEqual([]);
});

// The 400 ms before a page button shows its busy label is timed in NotePage.test.tsx, with a fake clock.
test('Save changes and Cancel show their busy labels while their requests are out, and Saving changes is said', async ({
  page,
}) => {
  const store = janesNote();
  await openEdit(page, store);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);

  const heldVersion = await holdNext(page, '**/api/participants/*/notes/*/versions');
  await saveChanges(page).click();
  const post = await heldVersion();
  await expect(saveChanges(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(saveChanges(page)).toHaveAccessibleName('Saving changes…');
  await expect(main(page).locator('p.visually-hidden[role="status"]')).toHaveText('Saving changes…');
  await expect(cancel(page)).toBeVisible();
  await post.fallback();
  await expect(page).toHaveURL(address);

  // Cancel, on another note's pending edit, held in the same way.
  const other = janesNote();
  await openEdit(page, other);
  const heldCancel = await holdNext(page, (url) => url.pathname.endsWith('/draft'));
  await cancel(page).click();
  const remove = await heldCancel();
  await expect(cancel(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(cancel(page)).toHaveAccessibleName('Cancelling…');
  await remove.fallback();
  await expect(page).toHaveURL(address);
});

test('offline, Save changes and Cancel say so above the buttons, keep the text, and work when tried again', async ({
  page,
}) => {
  const store = janesNote();
  await openEdit(page, store);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await failEditRequests(page);

  await saveChanges(page).click();
  await expect(requestAlert(page)).toHaveText('Changes not saved: no connection. Try again.');
  await expect(box(page)).toHaveValue(editedText);
  await expect(page).toHaveURL(editAddress);
  await expect(saveChanges(page)).not.toHaveAttribute('aria-disabled');
  await expect(page.getByRole('alertdialog')).toHaveCount(0);

  await cancel(page).click();
  await expect(requestAlert(page)).toHaveText('Not cancelled: no connection. Try again.');
  await expect(page).toHaveURL(editAddress);
  await expect(cancel(page)).not.toHaveAttribute('aria-disabled');

  // Back online, Save changes goes.
  await store.serve(page, worker);
  await saveChanges(page).click();
  await expect(page).toHaveURL(address);
  await expect(readText(page)).toHaveText(editedText);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 2 });
});

test('a lost answer to Save changes, tried again with nothing typed, makes one version with one key', async ({
  page,
}) => {
  const store = janesNote();
  await openEdit(page, store);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);

  // The version is made, but its answer is lost on the way.
  store.loseNextSubmitAnswer();
  await saveChanges(page).click();
  await expect(requestAlert(page)).toHaveText('Changes not saved: no connection. Try again.');
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 2 });
  const savesBefore = store.saves.length;

  // The same key, answered 200 with that version, and no save sent first.
  const retried = page.waitForResponse(isVersion);
  await saveChanges(page).click();
  const answer = await retried;
  expect(answer.status()).toBe(200);
  expect(await answer.json()).toEqual({ versionNumber: 2, createdAtUtc: '2026-10-01T06:12:00.000Z' });
  await expect(page).toHaveURL(address);
  await expect(readText(page)).toHaveText(editedText);
  expect(store.submits).toHaveLength(2);
  expect(store.submits[1]).toEqual(store.submits[0]);
  expect(store.saves).toHaveLength(savesBefore);
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ currentVersionNumber: 2 });
});

test('the edit form and the cannot-edit page pass axe, and the form has no sideways scroll at 375 px', async ({
  page,
}) => {
  const store = janesNote({ edited: true });
  await page.setViewportSize({ width: 375, height: 812 });
  await openEdit(page, store);
  await box(page).fill(editedText);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expectAxeToPass(page);
  expect(await scrollsSideways(page)).toBe(false);
  await saveChanges(page).scrollIntoViewIfNeeded();
  await expect(saveChanges(page)).toBeInViewport();
  await expect(cancel(page)).toBeVisible();
  expect(await scrollsSideways(page)).toBe(false);

  // The notice and the newer version, at 375 px too.
  store.editElsewhere(jane.id, worker.today, 'Sam Lee', { narrative: 'The manager’s words, a long unbroken line.' });
  await saveChanges(page).click();
  await expect(notice(page)).toBeFocused();
  await expectAxeToPass(page);
  expect(await scrollsSideways(page)).toBe(false);

  await openRead(page, store, riley, editAddress);
  await expect(heading(page)).toHaveText('You cannot edit this note');
  await expectAxeToPass(page);
  expect(await scrollsSideways(page)).toBe(false);
});

test('the keyboard alone goes through the form, Save changes and Cancel, and then the notice’s button', async ({
  page,
}) => {
  const store = janesNote({ edited: true });
  await openEdit(page, store);
  await expect(heading(page)).toBeFocused();

  // note-form.md *Focus order (Tab)*: Past notes, each goal's box, Guided notes, Flag for manager, Save changes,
  // Cancel.
  await expectStops(page, 'Tab', ['Past notes', breakfast.text, bus.text, '3. Guided notes']);
  await page.keyboard.type(' Then home.');
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expectStops(page, 'Tab', ['Flag for manager', 'Save changes', 'Cancel']);

  // A newer version makes Save changes meet the conflict: Enter on it focuses the notice, before the form.
  store.editElsewhere(jane.id, worker.today, 'Sam Lee', { narrative: 'The manager’s words.' });
  await expectStops(page, 'Shift+Tab', ['Save changes']);
  await page.keyboard.press('Enter');
  await expect(notice(page)).toBeFocused();
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(startAgain(page)).toBeFocused();
  await expectStops(page, 'Tab', [breakfast.text]);

  // Enter on Start again, reached by Shift+Tab, rebases the edit and puts focus on the <h1>.
  await expectStops(page, 'Shift+Tab', ['Start again from the latest version']);
  await page.keyboard.press('Enter');
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText(/^Editing submitted note \(version 3\) /);
});
