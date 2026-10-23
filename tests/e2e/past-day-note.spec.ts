import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { breakfast, bus, goals, serveGoals } from './goals.ts';
import { savedPrompts } from './guidePrompts.ts';
import { expectNothingKept } from './kept.ts';
import { shownText } from './layout.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { notesStore, type NoteGroup, type NotesStore } from './notes.ts';
import { ben, jane, participants, serveParticipants } from './participants.ts';
import { recordRequests } from './requests.ts';
import { serveMyDrafts, serveToday } from './today.ts';

// Write past-day note in the published app (S02.08.01, S02.08.02, and the past-day half of S01.02.04, S02.07.01 and
// S02.07.02), against participants.md C and note-form.md's criteria for a manager's note of an earlier day, as T18
// builds them. Each test answers /api/auth/me, the participants, Today's lists, the participant's header, their notes
// and the version list in the browser, with made-up people and a store of notes (me.ts, participants.ts, notes.ts), so
// that none of those requests reaches the app.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
const link = (page: Page, name: string) => main(page).getByRole('link', { name, exact: true });
const writePastDayNote = (page: Page) => link(page, 'Write past-day note');
const noteDateField = (page: Page) => main(page).getByLabel('Note date', { exact: true });
const continueButton = (page: Page) => main(page).getByRole('button', { name: 'Continue', exact: true });
const saveStatus = (page: Page) => page.locator('#save-status');
const box = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const pickBox = (page: Page, name: string) => main(page).getByRole('checkbox', { name, exact: true });
const submitNote = (page: Page) => main(page).getByRole('button', { name: 'Submit note', exact: true });
const confirmation = (page: Page) => page.getByRole('alertdialog');
// A line of the note's page, found by what it starts with.
const lineStarting = (page: Page, start: RegExp) => main(page).locator('p').filter({ hasText: start });

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;
const pastNotesAddress = (participantId: string) => `/participants/${participantId}/notes`;
const participantAddress = (participantId: string) => `/manage/participants/${participantId}`;
const pastDayAddress = (participantId: string) => `${participantAddress(participantId)}/past-day-note`;

/** "Saved 4:12 pm", or the like, at whatever time the app saved. */
const savedAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

const medication = { id: '1a4c7e90-2b5d-4f81-9c36-8e0a2d4f6b17', text: 'Medication prompted' };
const trains = { id: '5e80bcd4-6f91-4dc5-9a7a-2c4e6b8dafb5', text: 'Travelled by bus or train' };
const showered = { id: '8bc3efa7-9cd4-4a08-9dae-5f7b9ea03c18', text: 'Showered' };
const everyNote: NoteGroup = {
  id: '0e2a4c6f-1b3d-4e5f-8a7b-9c0d1e2f3a4b',
  name: 'Every note',
  isEveryNote: true,
  items: [medication],
};
const outing: NoteGroup = { id: '4f6a8c0e-3d5f-4b7a-9c1e-2a4c6e8f0b3d', name: 'Community outing', items: [trains] };
const personalCare: NoteGroup = {
  id: '7a9c1e3f-6b8d-4e0a-8c2e-5b7d9f1a3c4e',
  name: 'Personal care',
  items: [showered],
};

/** Priya Nair, another worker, who wrote the notes that Jane already has. */
const priya = { name: 'Priya Nair', id: 'b7a1c0de-0000-4000-8000-000000000001' };

/** Jane's goals and the organisation's groups, with no note yet. */
function emptyStore(): NotesStore {
  return notesStore({
    today: manager.today,
    participants,
    goals: { [jane.id]: [breakfast, bus] },
    commonItemGroups: [everyNote, outing, personalCare],
    prompts: savedPrompts,
  });
}

/** Adds a note of Priya's for `noteDate` that picked `picked`. */
function addPriyasNote(store: NotesStore, noteDate: string, picked: NoteGroup[]) {
  store.addSubmitted(jane.id, noteDate, {
    authorDisplayName: priya.name,
    authorUserId: priya.id,
    firstSubmittedAtUtc: `${noteDate}T06:42:00Z`,
    narrative: 'Jane made toast.',
    goals: [{ id: breakfast.id, text: breakfast.text, isTicked: true }],
    pickedGroupIds: picked.map(({ id }) => id),
  });
}

/**
 * Answers a manager's `GET {base}/versions` for the notes in `store`, which the store does not know: one version, the
 * author's, newest first. A later call overrides the store's own route, as Playwright tries the latest route first.
 */
async function serveVersionList(page: Page, store: NotesStore) {
  await page.route(
    (url) => /^\/api\/participants\/[^/]+\/notes\/[^/]+\/versions$/.test(url.pathname),
    (route) => {
      if (route.request().method() !== 'GET') {
        return route.fallback();
      }

      const [, , , participantId, , noteDate] = new URL(route.request().url()).pathname.split('/');
      const held = store.submitted(participantId, noteDate)!;
      const createdBy = { id: held.authorUserId, displayName: held.authorDisplayName };
      return route.fulfill({
        json: [{ versionNumber: 1, createdBy, createdAtUtc: held.firstSubmittedAtUtc, isFlagged: held.isFlagged }],
      });
    },
  );
}

/** Answers everything the screens ask for as `me`, from `store`, and opens `address`. */
async function open(page: Page, store: NotesStore, address: string, me: Me = manager) {
  await serveMe(page, me);
  await serveParticipants(page, participants);
  await serveGoals(page, goals);
  await serveToday(page, me.today, participants, store.todayNotes(me));
  await serveMyDrafts(page, store.myDrafts(me));
  await store.serve(page, me);
  await serveVersionList(page, store);
  await page.goto(address);
}

/** Enters `date` in Note date, and presses Continue. */
async function continueWith(page: Page, date: string) {
  await noteDateField(page).fill(date);
  await continueButton(page).click();
}

test('Participants, a participant, Write past-day note: the title, the focused <h1>, and today refused', async ({
  page,
}) => {
  await open(page, emptyStore(), '/manage/participants');
  await main(page).getByRole('link', { name: 'Jane Citizen', exact: true }).click();
  await expect(page).toHaveURL(participantAddress(jane.id));
  await writePastDayNote(page).click();

  await expect(page).toHaveURL(pastDayAddress(jane.id));
  await expect(page).toHaveTitle(title('Write past-day note'));
  await expect(heading(page)).toHaveText('Jane Citizen, Write past-day note');
  await expect(heading(page)).toBeFocused();
  await expect(noteDateField(page)).toHaveValue('');
  await expect(noteDateField(page)).toHaveAttribute('max', '2026-09-30');
  await expect(noteDateField(page)).not.toHaveAttribute('min');
  await expect(main(page).getByText('A date before today', { exact: true })).toBeVisible();
  await expect(backLink(page, 'Participant')).toHaveAttribute('href', participantAddress(jane.id));
  await expectAxeToPass(page);

  // Today is not a past date: the message above the field, with the field focused, and no request sent.
  const sent = recordRequests(page);
  await continueWith(page, manager.today);
  await expect(main(page).getByText('Note date must be in the past', { exact: true })).toBeVisible();
  await expect(noteDateField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(noteDateField(page)).toBeFocused();
  await expect(page).toHaveURL(pastDayAddress(jane.id));
  await expectAxeToPass(page);

  // Fixing the date clears the message, and a date left empty has its own.
  await noteDateField(page).fill('2026-09-30');
  await expect(main(page).getByText('Note date must be in the past', { exact: true })).toHaveCount(0);
  await noteDateField(page).fill('');
  await continueButton(page).click();
  await expect(main(page).getByText('Enter the note date', { exact: true })).toBeVisible();
  await expect(noteDateField(page)).toBeFocused();
  expect((await sent()).filter((request) => request.includes('/notes/'))).toEqual([]);
});

test('a date between two submitted notes: the past-day notice, and the earlier note’s picks', async ({ page }) => {
  const store = emptyStore();
  addPriyasNote(store, '2026-09-25', [outing]);
  addPriyasNote(store, '2026-09-29', [personalCare]);
  await open(page, store, pastDayAddress(jane.id));

  await continueWith(page, '2026-09-27');

  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-27'));
  await expect(heading(page)).toHaveText('Jane Citizen Sunday 27 September 2026');
  await expect(
    main(page).getByText('Past-day note for Sunday 27 September 2026, written on Thursday 1 October 2026.', {
      exact: true,
    }),
  ).toBeVisible();
  await expect(main(page).getByText(/^This draft is for /)).toHaveCount(0);
  await expect(pickBox(page, outing.name)).toBeChecked();
  await expect(pickBox(page, personalCare.name)).not.toBeChecked();
  await expectAxeToPass(page);
});

test('type, autosave, Submit: "Submit the note for", then the past-day line on the read view and history', async ({
  page,
}) => {
  const store = emptyStore();
  await open(page, store, pastDayAddress(jane.id));
  await continueWith(page, '2026-09-27');
  await box(page).fill('Jane made toast.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  await submitNote(page).click();
  await expect(confirmation(page)).toHaveAccessibleName('Submit the note for Jane Citizen');
  await confirmation(page).getByRole('button', { name: 'Submit note for Jane Citizen', exact: true }).click();
  await expect.poll(() => store.submits.length, { timeout: 15_000 }).toBe(1);
  await expect(page).toHaveURL('/');

  // The read view puts the past-day line in place of Written by, then when it was submitted.
  await page.goto(noteAddress(jane.id, '2026-09-27'));
  await expect(heading(page)).toHaveText('Jane Citizen Sunday 27 September 2026');
  await expect
    .poll(() => shownText(lineStarting(page, /^Past-day note/)))
    .toBe('Past-day note written by Sam Lee (manager) on Thu 1 Oct 2026, 4:12 pm');
  await expect(lineStarting(page, /^Written by/)).toHaveCount(0);

  // Version history has it too.
  await link(page, 'Version history').click();
  await expect(heading(page)).toHaveText('Version history Jane Citizen Sunday 27 September 2026');
  await expect
    .poll(() => shownText(lineStarting(page, /^Past-day note/)))
    .toBe('Past-day note written by Sam Lee (manager) on Thu 1 Oct 2026, 4:12 pm');
  await expectNothingKept(page, 'Jane', 'Citizen', 'Sam Lee');
});

test('the note’s back link reads Participant, and returns there, skipping the date page', async ({ page }) => {
  await open(page, emptyStore(), participantAddress(jane.id));
  await writePastDayNote(page).click();
  await continueWith(page, '2026-09-27');

  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-27'));
  await expect(backLink(page, 'Participant')).toHaveAttribute('href', participantAddress(jane.id));
  await backLink(page, 'Participant').click();
  await expect(page).toHaveURL(participantAddress(jane.id));
  await expect(heading(page)).toHaveText('Jane Citizen');
});

test('browser Back from the note skips the date page too', async ({ page }) => {
  await open(page, emptyStore(), participantAddress(jane.id));
  await writePastDayNote(page).click();
  await continueWith(page, '2026-09-27');
  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-27'));

  await page.goBack();

  await expect(page).toHaveURL(participantAddress(jane.id));
});

test('from Past notes, the note’s back link reads Past notes, and returns there', async ({ page }) => {
  await open(page, emptyStore(), pastNotesAddress(jane.id));
  await writePastDayNote(page).click();
  await expect(page).toHaveURL(pastDayAddress(jane.id));
  await expect(backLink(page, 'Past notes')).toBeVisible();
  await continueWith(page, '2026-09-27');

  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-27'));
  await expect(backLink(page, 'Past notes')).toHaveAttribute('href', pastNotesAddress(jane.id));
  await backLink(page, 'Past notes').click();
  await expect(page).toHaveURL(pastNotesAddress(jane.id));
});

test('a date that already has a submitted note opens its read view, with Edit', async ({ page }) => {
  const store = emptyStore();
  addPriyasNote(store, '2026-09-27', []);
  await open(page, store, pastDayAddress(jane.id));

  await continueWith(page, '2026-09-27');

  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-27'));
  await expect(heading(page)).toHaveText('Jane Citizen Sunday 27 September 2026');
  await expect(link(page, 'Edit')).toBeVisible();
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
});

test('for an archived participant the links are gone and the page says no new notes can be written', async ({
  page,
}) => {
  await open(page, emptyStore(), participantAddress(ben.id));
  await expect(heading(page)).toHaveText('Ben Archer');
  await expect(link(page, 'Past notes')).toBeVisible();
  await expect(writePastDayNote(page)).toHaveCount(0);

  await page.goto(pastNotesAddress(ben.id));
  await expect(heading(page)).toHaveText('Past notes Ben Archer');
  await expect(link(page, 'Edit participant')).toBeVisible();
  await expect(writePastDayNote(page)).toHaveCount(0);

  await page.goto(pastDayAddress(ben.id));
  await expect(heading(page)).toHaveText('Ben Archer, Write past-day note');
  await expect(
    main(page).getByText('Ben Archer is archived, so no new notes can be written for them.', { exact: true }),
  ).toBeVisible();
  await expect(noteDateField(page)).toHaveCount(0);
  await expect(continueButton(page)).toHaveCount(0);
  await expectAxeToPass(page);
});

test('a worker has no link to it, and typing its address gives Page not found, with nothing sent about Jane', async ({
  page,
}) => {
  const store = emptyStore();
  await open(page, store, pastNotesAddress(jane.id), worker);
  await expect(heading(page)).toHaveText('Past notes Jane Citizen');
  await expect(writePastDayNote(page)).toHaveCount(0);

  const sent = recordRequests(page);
  await page.goto(pastDayAddress(jane.id));
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  expect((await sent()).filter((request) => request.includes('/api/participants'))).toEqual([]);
  await expectAxeToPass(page);
});
