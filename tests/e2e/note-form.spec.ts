import { randomUUID } from 'node:crypto';
import type { Locator, Page, Response } from '@playwright/test';
import { addJaneWithTwoGoals, janeCitizen, twoGoalTexts } from './addParticipant.ts';
import { authenticatorCode } from './authenticatorCode.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, expectStops, ring } from './focus.ts';
import { breakfast, bus, madeUpGoal, sister } from './goals.ts';
import { savedPrompts, servePrompts } from './guidePrompts.ts';
import { expectNothingKept } from './kept.ts';
import {
  boxOf,
  expectAtLeast,
  scrollsSideways,
  setTextTo200Percent,
  shownText,
  systemColour,
} from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import {
  alexStarted,
  daysAfter,
  draftRequests,
  failDraftSaves,
  failNoteReads,
  isDraftSaveRequest,
  notesStore,
  type DraftSave,
  type NotesStore,
} from './notes.ts';
import { ben, jane, madeUpParticipant, participants, ravi } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';
import { codeLabel, setUpWithPassword, signInThroughSetup } from './setupSignIn.ts';
import { endTheSession, signInAgainWithPasskey } from './signedOut.ts';
import { serveMyDrafts, serveToday } from './today.ts';
import { inviteThroughThePage } from './users.ts';

// A participant's note in the published app (S02.02.02 to S02.02.08, S02.03.01 to S02.03.03), against note-form.md's
// criteria for identity, loading, autosave, conflicts and refusals, validation and Submit, Discard, leaving, the
// session, and layout and accessibility, as far as T08, T09, T10 and T12 build the form. The tests that sign in for
// real meet the app's own Today, participant, note, guide prompts, saves, Submit and Discard; the others answer
// /api/auth/me, /api/today, /api/me/drafts, the participant's header, the note, its working copy, Submit and Discard in
// the browser, with made-up people and goals (me.ts, today.ts, notes.ts), so that none of those requests reaches the
// app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const row = (page: Page, name: string) => rows(page).filter({ hasText: name });
const findField = (page: Page) => main(page).getByRole('textbox', { name: 'Find a participant' });

// The note form (note-form.md *Components, in order*): the top bar, in the shell's bar between <header> and <main>,
// with the back link, the save indicator and, after a dead end, its banner; then Goals and Guided notes.
const topBar = (page: Page) => page.locator('#page-bar > div');
const backLink = (page: Page) => page.locator('#page-bar').getByRole('link', { name: 'Today', exact: true });
const saveStatus = (page: Page) => page.locator('#save-status');
const deadEnd = (page: Page) => page.locator('#page-bar').getByRole('region');
// The form's assertive region, after the page's content, which says a dead end's sentence once: visually hidden, where
// the alert above Submit note shows its words.
const alert = (page: Page) => main(page).locator('div.visually-hidden[role="alert"]');
const goals = (page: Page) => main(page).getByRole('group', { name: '1. Goals' });
const goalBox = (page: Page, text: string) => main(page).getByRole('checkbox', { name: text, exact: true });
const goalRow = (page: Page, text: string) => main(page).locator('label').filter({ hasText: text });
const box = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const listsChanged = 'The goal or common-item list was just changed. Please check your ticks.';
// The lists-changed notice's own status region above Goals (note-form.md component 7).
const listsNotice = (page: Page) => main(page).locator('div[role="status"][tabindex="-1"]');
// The region's line, beside the page's visually hidden status region and the lists-changed notice's.
const loadLine = (page: Page) => main(page).locator('p[role="status"]:not(.visually-hidden)');
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
// The page's polite region, visually hidden, which says a dead end's sentence again at a press of Submit note.
const pageStatus = (page: Page) => main(page).locator('p.visually-hidden[role="status"]');

// Flag for manager, Submit note and the Submit confirmation (note-form.md components 11, 12 and 14).
const pastNotesLink = (page: Page) => main(page).getByRole('link', { name: 'Past notes', exact: true });
const flagBox = (page: Page) => main(page).getByRole('checkbox', { name: 'Flag for manager', exact: true });
const flagRow = (page: Page) => main(page).locator('label[for="flag-for-manager"]');
const reasonBox = (page: Page) => main(page).getByRole('textbox', { name: 'Reason', exact: true });
const reasonCount = (page: Page) => main(page).locator('#flag-reason-group p[aria-hidden="true"]');
const submitNote = (page: Page) => main(page).getByRole('button', { name: 'Submit note', exact: true });
const summary = (page: Page) =>
  main(page).locator('div[tabindex="-1"]').filter({ has: page.getByRole('heading', { name: 'There is a problem' }) });
const notesError = (page: Page) => main(page).locator('#guided-notes-error');
const reasonError = (page: Page) => main(page).locator('#flag-reason-error');
const confirmation = (page: Page) => page.getByRole('alertdialog');
const confirmationTitle = (page: Page) => confirmation(page).getByRole('heading', { level: 2 });
const confirmButton = (page: Page, name = 'Jane Citizen') =>
  confirmation(page).getByRole('button', { name: `Submit note for ${name}`, exact: true });
const goBack = (page: Page) => confirmation(page).getByRole('button', { name: 'Go back', exact: true });
const confirmationAlert = (page: Page) => confirmation(page).getByRole('alert');
const confirmationStatus = (page: Page) => confirmation(page).getByRole('status');
// Today's success banner after Submit, before its <h1> (today.md component 2).
const banner = (page: Page) => main(page).locator('[data-route-focus]');
// A row's status line on Today, which describes its link (today.md component 8).
const rowLine = (page: Page, name: string) =>
  main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key]', { hasText: name }) }).locator('p');
// The read view (participant-notes.md *Read view*): the byline, the flag line, the goals and the Guided notes text.
const byline = (page: Page) => main(page).locator('p').filter({ hasText: /^Written by / });
const flagLine = (page: Page) => main(page).locator('p').filter({ hasText: /^Flagged/ });
const readGoals = (page: Page) => main(page).locator('ul[role="list"] > li');
const readText = (page: Page) =>
  main(page).getByRole('heading', { name: '3. Guided notes', exact: true }).locator('xpath=following-sibling::div[1]');
const notFound = 'Page not found';

// What a failed Submit says, and the note submitted elsewhere (note-form.md *Confirmation dialog*).
const notSubmittedOffline = 'Not submitted: no connection. Your draft is saved. Try again.';
const notSubmittedServer = 'Not submitted: something went wrong. Your draft is saved. Try again.';
const changedElsewhere = 'This note was changed on another device or tab.';

/** Any time of day as the app writes it, with its non-breaking space, or "midday" or "midnight" (microcopy.md §3). */
const anyTime = '(\\d{1,2}:\\d{2}\\s[ap]m|midday|midnight)';

/** "Saved 4:12 pm", or the like, at whatever time the app saved, with its non-breaking space (microcopy.md §3). */
const savedAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;
/** "Draft · saved 4:12 pm", or the like, as a reopened draft's indicator shows it. */
const draftSavedAnyTime = /^Draft · saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

// The dead ends' sentences (note-form.md *Conflicts and refusals*).
const afterMidnight =
  'It’s now after midnight, so this note can’t be started for yesterday. Ask a manager to record it.';
const alexOnToday = 'Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it.';
const alexOnEarlierDay = 'Alex P. started the note for Jane Citizen at 9:14 am. Only Alex P. can finish it.';
const janeArchived = 'Jane Citizen is archived, so this note cannot be started. Ask a manager.';

/** The address of `participantId`'s note for `noteDate`. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;

/** Whether the response answers a save of a note's working copy, `PUT {base}/draft`. */
const isDraftSave = (response: Response) => isDraftSaveRequest(response.request());

/** Whether the response answers a read of a note's working copy, `GET {base}/draft`. */
const isDraftRead = (response: Response) =>
  response.request().method() === 'GET' && /^\/api\/participants\/[^/]+\/notes\/[^/]+\/draft$/.test(
    new URL(response.url()).pathname,
  );

/** Whether the response answers a Submit, `POST {base}/versions`. */
const isSubmit = (response: Response) =>
  response.request().method() === 'POST' && /^\/api\/participants\/[^/]+\/notes\/[^/]+\/versions$/.test(
    new URL(response.url()).pathname,
  );

/** A pattern for the next Submit, for `holdNext`. */
const submitRequests = '**/api/participants/*/notes/*/versions';

/**
 * Opens Today as `me` with Jane Citizen and Ravi de Silva, and then Jane's note from her row, answering both from
 * `store`, and waits for the form's <h1>.
 */
async function openJanesNote(page: Page, store: NotesStore, me: Me = worker) {
  await serveMe(page, me);
  await serveToday(page, me.today, participants);
  await store.serve(page, me);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toHaveText(`Jane Citizen Thursday 1 October 2026`);
}

/** Jane Citizen's and Ravi de Silva's notes, Jane with two goals and Ravi with none, and the design's prompts. */
const twoGoals = () =>
  notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] }, prompts: savedPrompts });

/**
 * The browser's own dialogs, such as Chromium's for a `beforeunload` that asks, which the page opens from now on, by
 * their type, each accepted, so that a test that expects none fails on the list.
 */
function browserDialogs(page: Page): string[] {
  const seen: string[] = [];
  page.on('dialog', (dialog) => {
    seen.push(dialog.type());
    void dialog.accept();
  });
  return seen;
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  // But the save indicator of a reopened draft, which the form puts in anew with its first words, "Draft · saved …",
  // so that they are the page's own rather than announced as a change (autosave-status.md *Behaviour* 8).
  const announcedLate = liveRegionsAddedWithWords.filter((region) => !/^<p id="save-status"[^>]*>Draft</.test(region));
  expect(announcedLate, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher sees the note form, as participants.spec.ts shows it for the Participants screens: marked to
// fail, it passes only if the check after it fails it.
test.fail('a live region put into the note form with its words fails the test', async ({ page }) => {
  await openJanesNote(page, twoGoals());
  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('role', 'status');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

/** Saves `text` as the organisation's guide prompts through Manage > Guide prompts, as a manager does. */
async function savePromptsThroughThePage(page: Page, text: string) {
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  const field = main(page).getByRole('textbox', { name: 'Guide prompts', exact: true });
  await expect(field).toBeVisible();
  await field.fill(text);
  const saved = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && new URL(response.url()).pathname === '/api/admin/guide-prompts',
  );
  await main(page).locator('form').getByRole('button').click();
  expect((await saved).status()).toBe(200);
  await expect(main(page).locator('form').getByRole('status')).toHaveText(savedAnyTime);
}

const sentence = 'Made her own toast and phoned her sister.';
const newPrompts = 'How did the morning go?\nAnything to follow up?';

test('signed in for real at 375 px, a manager finds Jane on Today, starts her note, and reopens the draft', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  const id = await addJaneWithTwoGoals(page);

  // Find a participant finds her, and her row opens today's note, the server's date in Melbourne in its address.
  await findField(page).fill('cit');
  await expect(rows(page)).toHaveText([janeCitizen.name]);
  await findField(page).fill('zzz');
  await expect(main(page).getByText('No participant matches ‘zzz’.', { exact: true }).first()).toBeVisible();
  await main(page).getByRole('button', { name: 'Clear search' }).click();
  await expect(findField(page)).toBeFocused();
  const href = await row(page, janeCitizen.name).getAttribute('href');
  expect(href).toMatch(new RegExp(`^/participants/${id}/notes/\\d{4}-\\d{2}-\\d{2}$`));
  const opened = page.waitForResponse(isDraftRead);
  await row(page, janeCitizen.name).click();

  // The template: the two goals in the manager's order, unticked, with the lists' version, and nothing saved.
  const template = (await (await opened).json()) as { goals: { id: string; text: string }[]; isSaved: boolean };
  expect(template).toMatchObject({ baseVersion: 0, isSaved: false, listsVersion: expect.any(String), narrative: '' });
  expect(template.goals.map(({ text }) => text)).toEqual(twoGoalTexts);
  await expect(page).toHaveURL(href ?? '');
  await expect(heading(page)).toBeFocused();
  // The date with its non-breaking space, which a pattern does not count as a space.
  await expect(heading(page)).toHaveText(new RegExp(`^${janeCitizen.name}\\s\\w+day\\s\\d{1,2}\\s\\w+\\s\\d{4}$`));
  await expect(page).toHaveTitle(title('Note'));
  await expect(saveStatus(page)).toBeEmpty();

  // A tick on its text, then a sentence: "Saving…", then "Saved …" from the app's answer, the first creating the note.
  const first = page.waitForResponse(isDraftSave);
  await goalRow(page, breakfast.text).locator('span').click();
  await expect(goalBox(page, breakfast.text)).toBeChecked();
  await expect(saveStatus(page)).toHaveText('Saving…');
  const firstAnswer = await first;
  expect(firstAnswer.status()).toBe(201);
  expect(firstAnswer.request().postDataJSON()).toEqual({
    clientId: expect.any(String),
    seq: 1,
    listsVersion: expect.any(String),
    narrative: '',
    tickedGoalIds: [template.goals[0].id],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: false,
    flagReason: null,
  });
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  const second = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(sentence);
  await expect(saveStatus(page)).toHaveText('Saving…');
  expect((await second).status()).toBe(200);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  // The back link returns to the row, focused; reopening shows the draft with both.
  await backLink(page).click();
  await expect(row(page, janeCitizen.name)).toBeFocused();
  await row(page, janeCitizen.name).click();
  await expect(heading(page)).toBeFocused();
  await expect.poll(() => shownText(saveStatus(page))).toMatch(draftSavedAnyTime);
  await expect(goalBox(page, breakfast.text)).toBeChecked();
  await expect(goalBox(page, bus.text)).not.toBeChecked();
  await expect(box(page)).toHaveValue(sentence);

  // Emptied, the draft's box shows the prompts that a manager saves after, the next time it is opened.
  await box(page).fill('');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await savePromptsThroughThePage(page, newPrompts);
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await row(page, janeCitizen.name).click();
  await expect.poll(() => shownText(saveStatus(page))).toMatch(draftSavedAnyTime);
  await expect(box(page)).toHaveValue('');
  await expect(box(page)).toHaveAttribute('placeholder', newPrompts);

  // With the prompts saved as "", the box has no placeholder.
  await savePromptsThroughThePage(page, '');
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await row(page, janeCitizen.name).click();
  await expect.poll(() => shownText(saveStatus(page))).toMatch(draftSavedAnyTime);
  await expect(box(page)).not.toHaveAttribute('placeholder');
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence, breakfast.text);
});

test('signed in for real, an invited worker meets the in-progress sentence, another organisation not found', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
  secondInvite,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  const id = await addJaneWithTwoGoals(page);

  // The manager starts Jane's note.
  await row(page, janeCitizen.name).click();
  await expect(heading(page)).toBeFocused();
  const address = new URL(page.url()).pathname;
  const started = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(sentence);
  expect((await started).status()).toBe(201);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  // The manager invites a worker, who sets up on a phone of their own from the link that admin reset-signin gives.
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(heading(page)).toHaveText('Users');
  const riley = { displayName: 'Riley Sample', email: `riley.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, riley, 'Worker');
  const phone = secondDevice.page;
  await phone.setViewportSize({ width: 375, height: 812 });
  await phone.goto(await resetSignIn(riley.email));
  await main(phone).getByRole('button', { name: 'Use a passkey (recommended)' }).click();

  // The same Today, and at Jane's note, who started it and when, with no form: the note's header alone, to a worker.
  await expect(rows(phone)).toHaveText([janeCitizen.name]);
  const header = phone.waitForResponse((response) => new URL(response.url()).pathname === `/api${address}`);
  const refused = phone.waitForResponse(isDraftRead);
  await row(phone, janeCitizen.name).click();
  expect(await (await header).json()).toEqual({
    status: 'Draft',
    authorDisplayName: invite.displayName,
    startedAtUtc: expect.any(String),
  });
  const problem = await refused;
  expect(problem.status()).toBe(409);
  expect(await problem.json()).toMatchObject({
    code: 'note.in_progress_by_other',
    authorDisplayName: invite.displayName,
    startedAtUtc: expect.any(String),
  });
  await expect(phone).toHaveURL(address);
  await expect(heading(phone)).toBeFocused();
  await expect(heading(phone)).toHaveText(new RegExp(`^${janeCitizen.name} `));
  await expect(
    main(phone).getByText(
      new RegExp(
        `^${invite.displayName} started today’s note for ${janeCitizen.name} at `
          + `(\\d{1,2}:\\d{2}\\s[ap]m|midday|midnight)\\. Only ${invite.displayName} can finish it\\.$`,
      ),
    ),
  ).toBeVisible();
  await expect(main(phone).getByRole('checkbox')).toHaveCount(0);
  await expect(main(phone).getByRole('textbox')).toHaveCount(0);
  await expect(saveStatus(phone)).toHaveCount(0);
  // Past notes, and no Discard draft: a worker may discard only a draft of their own (S02.04.03).
  await expect(main(phone).getByRole('link')).toHaveText(['Past notes']);
  await expect(main(phone).getByRole('button')).toHaveCount(0);
  await expect(main(phone).getByText(sentence)).toHaveCount(0);
  await expectNothingKept(phone, janeCitizen.givenName, janeCitizen.familyName, sentence);

  // The other organisation's manager, signed in on the first browser, meets Page not found at the same address.
  await page.getByRole('button', { name: 'Account' }).click();
  await page.locator('#account-panel').getByRole('button').click();
  await expect(main(page).getByRole('heading', { level: 1 })).toHaveText('Sign in');
  await signInThroughSetup(page, secondInvite, authenticator);
  const notFound = page.waitForResponse(
    (response) => new URL(response.url()).pathname === `/api/participants/${id}`,
  );
  await page.goto(address);
  expect((await notFound).status()).toBe(404);
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  await expect(main(page).getByText(janeCitizen.name)).toHaveCount(0);
});

test("on a slow network, Jane's name is never in the DOM of Ravi's note while it loads", async ({ page }) => {
  const store = twoGoals();
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants);
  // Each answer for a participant or a note 1.5 s after it is sent, so that the loading line shows.
  await store.serve(page, worker, { latencyMs: 1_500 });
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await backLink(page).click();
  await expect(row(page, 'Jane Citizen')).toBeFocused();

  // Every change to the page from now on, once Today's rows have gone, is checked for her name, in <main> and the bar.
  await page.evaluate(() => {
    const seen: string[] = [];
    Object.assign(window, { janeSeen: seen });
    new MutationObserver(() => {
      const shown = document.querySelector('main');
      if (shown === null || shown.querySelector('a[data-return-key]') !== null) {
        return;
      }

      const text = `${shown.textContent ?? ''} ${document.querySelector('#page-bar')?.textContent ?? ''}`;
      if (/Jane|Citizen/.test(text)) {
        seen.push(text);
      }
    }).observe(document.body, { subtree: true, childList: true, characterData: true });
  });
  await row(page, 'Ravi de Silva').click();
  await expect(loadLine(page)).toHaveText('Loading note…');
  await expect(heading(page)).toHaveCount(0);
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  expect(await page.evaluate(() => (window as unknown as { janeSeen: string[] }).janeSeen)).toEqual([]);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', 'Silva');
});

test('loading, a load that failed, Page not found and someone else’s draft, each passing axe', async ({ page }) => {
  const store = twoGoals();
  await serveMe(page, manager);
  await serveToday(page, manager.today, participants);
  await store.serve(page, manager);

  // Loading: the top bar and its back link at once, then "Loading note…", with no <h1>, tick box or text box, and
  // no save status until the form shows.
  const heldDraft = await holdNext(page, draftRequests);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  const draft = await heldDraft();
  await expect(loadLine(page)).toHaveText('Loading note…');
  await expect(backLink(page)).toHaveAttribute('href', '/');
  await expect(saveStatus(page)).toHaveCount(0);
  await expect(heading(page)).toHaveCount(0);
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expect(page).toHaveTitle(title('Note'));
  await expectAxeToPass(page);
  await draft.fallback();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await expect(heading(page)).toBeFocused();

  // No connection: the fallback <h1>, the sentence and Try again within about 2 s, then "still", with focus kept.
  await failNoteReads(page);
  await page.goto(noteAddress(jane.id, manager.today));
  await expect(loadLine(page)).toHaveText('The note did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Note');
  await expect(backLink(page)).toBeVisible();
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expectAxeToPass(page);
  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('The note still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expectAxeToPass(page);
  await store.serve(page, manager);
  await tryAgain(page).click();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await expect(heading(page)).toBeFocused();

  // A date after today, and a participant that the organisation does not have: Page not found.
  for (const address of [noteAddress(jane.id, '2026-10-02'), noteAddress(randomUUID(), manager.today)]) {
    await page.goto(address);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle(title('Page not found'));
  }
  await expectAxeToPass(page);

  // Someone else's draft, opened from Today: the <h1>, focused, and who started it and when, with no form.
  store.startElsewhere(jane.id, manager.today, alexStarted);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
  await expect(main(page).getByText(alexOnToday, { exact: true })).toBeVisible();
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expect(saveStatus(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // And for an earlier day, "started the note for".
  store.startElsewhere(jane.id, '2026-09-30', { ...alexStarted, startedAtUtc: '2026-09-29T23:14:00Z' });
  await page.goto(noteAddress(jane.id, '2026-09-30'));
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  await expect(main(page).getByText(alexOnEarlierDay, { exact: true })).toBeVisible();
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Alex');
});

test('the form new, saving, saved, reopened and with no goals, each passing axe', async ({ page }) => {
  const store = twoGoals();
  await openJanesNote(page, store);

  // New: Goals' legend and hint, each goal unticked in the manager's order, the box with the prompts, and nothing said.
  await expect(page).toHaveTitle(title('Note'));
  await expect(heading(page)).toBeFocused();
  await expect(heading(page).locator('[translate="no"]')).toHaveText('Jane Citizen');
  await expect(heading(page).locator('time')).toHaveAttribute('datetime', worker.today);
  await expect(goals(page).locator('legend')).toHaveText('1. Goals');
  await expect(goals(page)).toHaveAccessibleDescription('Ticked means reached.');
  for (const text of [breakfast.text, bus.text]) {
    await expect(goalBox(page, text)).not.toBeChecked();
  }
  await expect(main(page).locator('label')).toHaveText([
    breakfast.text,
    bus.text,
    '3. Guided notes',
    'Flag for manager',
    'Reason',
  ]);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(['1. Goals', '2. Common items', '3. Guided notes']);
  await expect(box(page)).toHaveAttribute('placeholder', savedPrompts);
  await expect(box(page)).toHaveAttribute('aria-required', 'true');
  await expect(box(page)).not.toHaveAttribute('maxlength');
  await expect(box(page)).toHaveValue('');
  await expect(saveStatus(page)).toBeEmpty();
  await expect(listsNotice(page)).toBeEmpty();
  await expect(alert(page)).toBeEmpty();
  await expect(deadEnd(page)).toHaveCount(0);
  expect(store.saves).toEqual([]);
  await expectAxeToPass(page);

  // Saving: a tick shows "Saving…" while its save is held.
  const heldSave = await holdNext(page, draftRequests);
  await goalBox(page, bus.text).check();
  await expect(saveStatus(page)).toHaveText('Saving…');
  const save = await heldSave();
  await expectAxeToPass(page);

  // Saved: the answer's time, 4:12 pm in Melbourne.
  await save.fallback();
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  expect(store.saves).toEqual([
    {
      clientId: expect.any(String),
      seq: 1,
      listsVersion: expect.any(String),
      narrative: '',
      tickedGoalIds: [bus.id],
      pickedGroupIds: [],
      tickedCommonItemIds: [],
      isFlagged: false,
      flagReason: null,
    },
  ]);
  await box(page).pressSequentially('Caught the bus.');
  await expect(saveStatus(page)).toHaveText('Saving…');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await expectAxeToPass(page);
  expect(store.draft(jane.id, worker.today)).toMatchObject({ ticked: [bus.id], narrative: 'Caught the bus.' });

  // Reopened: "Draft · saved 4:12 pm", with the tick and the text.
  await backLink(page).click();
  await row(page, 'Jane Citizen').click();
  await expect.poll(() => shownText(saveStatus(page))).toBe('Draft · saved 4:12 pm');
  await expect(goalBox(page, bus.text)).toBeChecked();
  await expect(goalBox(page, breakfast.text)).not.toBeChecked();
  await expect(box(page)).toHaveValue('Caught the bus.');
  await expectAxeToPass(page);

  // No goals: the sentence in place of the fieldset.
  await backLink(page).click();
  await row(page, 'Ravi de Silva').click();
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  await expect(main(page).getByText('No goals set up for Ravi yet. A manager can add them.', { exact: true }))
    .toBeVisible();
  await expect(main(page).getByRole('group')).toHaveCount(0);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(['1. Goals', '2. Common items', '3. Guided notes']);
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Ravi', 'Caught the bus', breakfast.text, bus.text);
});

test('lists changed before the first save: the text and a listed tick kept, the notice, and a new save', async ({
  page,
}) => {
  const store = notesStore({
    today: worker.today,
    participants,
    goals: { [jane.id]: [breakfast, bus], [ravi.id]: [breakfast] },
    prompts: savedPrompts,
  });
  await openJanesNote(page, store);

  // A manager archives Makes own breakfast and adds Phones her sister while the form is open.
  store.setGoals(jane.id, [bus, sister]);
  await goalBox(page, breakfast.text).check();
  await goalBox(page, bus.text).check();
  await box(page).pressSequentially('Caught the bus.');
  await expect(main(page).getByText(listsChanged, { exact: true })).toBeVisible();
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  // The first save, sent as focus left the first box, refused; the form read again and saved with the lists' new
  // version, "Saving…" until then; and no save after carries the archived goal's tick.
  const [refused, resent, ...later] = store.saves;
  expect(refused.tickedGoalIds).toEqual([breakfast.id]);
  expect(resent.listsVersion).toEqual(expect.any(String));
  expect(resent.listsVersion).not.toBe(refused.listsVersion);
  for (const save of [resent, ...later]) {
    expect(save.tickedGoalIds).not.toContain(breakfast.id);
  }
  expect(store.draft(jane.id, worker.today)).toMatchObject({ ticked: [bus.id], narrative: 'Caught the bus.' });
  await expect(main(page).locator('label')).toHaveText([
    bus.text,
    sister.text,
    '3. Guided notes',
    'Flag for manager',
    'Reason',
  ]);
  await expect(goalBox(page, bus.text)).toBeChecked();
  await expect(goalBox(page, sister.text)).not.toBeChecked();
  await expect(box(page)).toHaveValue('Caught the bus.');
  // Focus stays in the box, which is still there.
  await expect(box(page)).toBeFocused();
  await expectAxeToPass(page);

  // On Ravi's note, the box with focus goes with the list, so focus goes to the notice.
  await backLink(page).click();
  await row(page, 'Ravi de Silva').click();
  await expect(goalBox(page, breakfast.text)).toBeVisible();
  store.setGoals(ravi.id, [sister]);
  await goalBox(page, breakfast.text).focus();
  await page.keyboard.press('Space');
  await expect(listsNotice(page)).toBeFocused();
  await expect(listsNotice(page)).toHaveText(listsChanged);
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await expect(goalBox(page, sister.text)).not.toBeChecked();
  await expectNothingKept(page, 'Jane', 'Ravi', 'Caught the bus', listsChanged);
});

/**
 * Expects the dead end that `sentence` words (note-form.md *Conflicts and refusals*): the banner in the top bar's
 * second row, named by its sentence, the sentence said once through the form's assertive region, "Not saved", every
 * tick and the text kept, editable and copyable, nothing more sent, and leaving by the back link not blocked; with axe
 * passing at both widths.
 */
async function expectDeadEnd(page: Page, store: NotesStore, sentence: string, typed: string) {
  await expect(deadEnd(page)).toHaveAccessibleName(sentence);
  await expect(deadEnd(page)).toHaveText(sentence);
  await expect(alert(page)).toHaveText(sentence);
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expect(box(page)).toHaveValue(typed);
  await expect(box(page)).toBeEditable();
  await expectAxeToPass(page);

  // The text can still be changed and selected for copying, and a tick changed, with nothing sent.
  const sent = store.saves.length;
  await box(page).press('End');
  await box(page).pressSequentially(' More.');
  await goalBox(page, bus.text).check();
  const selected = await box(page).evaluate((element: HTMLTextAreaElement) => {
    element.select();
    return element.value.slice(element.selectionStart, element.selectionEnd);
  });
  expect(selected).toBe(`${typed} More.`);
  await box(page).blur();
  await expect(saveStatus(page)).toHaveText('Not saved');
  // Past the 2 s after the last change at which a save would have gone.
  await page.waitForTimeout(2_500);
  expect(store.saves).toHaveLength(sent);

  const dialogs = browserDialogs(page);
  await backLink(page).click();
  await expect(page).toHaveURL('/');
  await expect(row(page, 'Jane Citizen')).toBeFocused();
  expect(dialogs).toEqual([]);
}

test('a worker’s first change after midnight, on a clock from 11:58 pm to 12:01 am, is refused, the text kept', async ({
  page,
}) => {
  // 11:58 pm on Thursday 1 October 2026 in Melbourne, before daylight saving starts.
  await page.clock.install({ time: new Date('2026-10-01T13:58:00Z') });
  const store = twoGoals();
  await openJanesNote(page, store);

  // Three minutes on, Melbourne's date is 2 October, on the server too, and the first change comes.
  await page.clock.runFor('03:00');
  store.setToday('2026-10-02');
  await box(page).pressSequentially('Slept well.');
  await expect(deadEnd(page)).toBeVisible();
  expect(store.saves).toHaveLength(1);
  await expectDeadEnd(page, store, afterMidnight, 'Slept well.');
  await expectNothingKept(page, 'Jane', 'Citizen', 'Slept well', afterMidnight);
});

test('the in-progress dead end, for today and for an earlier day, and the archived one', async ({ page }) => {
  // Alex P. starts Jane's note between the form opening and its first change.
  let store = twoGoals();
  await openJanesNote(page, store);
  store.startElsewhere(jane.id, worker.today, alexStarted);
  await box(page).pressSequentially('Went for a walk.');
  await expect(deadEnd(page)).toBeVisible();
  await expectDeadEnd(page, store, alexOnToday, 'Went for a walk.');

  // A manager's note for an earlier day, which Alex P. starts first: "started the note for".
  store = twoGoals();
  await serveMe(page, manager);
  await store.serve(page, manager);
  await page.goto(noteAddress(jane.id, '2026-09-30'));
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  store.startElsewhere(jane.id, '2026-09-30', { ...alexStarted, startedAtUtc: '2026-09-29T23:14:00Z' });
  await box(page).pressSequentially('Went for a walk.');
  await expect(deadEnd(page)).toHaveText(alexOnEarlierDay);
  await expect(alert(page)).toHaveText(alexOnEarlierDay);
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expectAxeToPass(page);

  // Jane archived between the form opening and its first change.
  store = twoGoals();
  await openJanesNote(page, store);
  store.archive(jane.id);
  await box(page).pressSequentially('Went for a walk.');
  await expect(deadEnd(page)).toBeVisible();
  await expectDeadEnd(page, store, janeArchived, 'Went for a walk.');
  await expectNothingKept(page, 'Jane', 'Alex', 'Went for a walk', janeArchived);
});

for (const { width, column } of [
  // The whole column inside the phone's 16 px gutters.
  { width: 375, column: 375 - 2 * 16 },
  // The form's 40rem column.
  { width: 1280, column: 40 * 16 },
]) {
  test(`at ${width} px, the Guided notes box and the Guide prompts editor measure the same, ${column} px wide`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const metrics = (locator: Locator) =>
      locator.evaluate((element) => {
        const style = getComputedStyle(element);
        return {
          fontSize: style.fontSize,
          lineHeight: style.lineHeight,
          padding: style.padding,
          borderWidth: style.borderWidth,
          width: element.getBoundingClientRect().width,
        };
      });

    await serveMe(page, manager);
    await servePrompts(page, savedPrompts);
    await page.goto('/manage/guide-prompts');
    const editor = await metrics(main(page).getByRole('textbox', { name: 'Guide prompts', exact: true }));

    await openJanesNote(page, twoGoals(), manager);
    const notes = await metrics(box(page));
    expect(notes).toEqual(editor);
    expect(notes).toEqual({ fontSize: '18px', lineHeight: '27px', padding: '12px', borderWidth: '2px', width: column });

    // The prompts in the placeholder's own colour, #626262, at full opacity (note-form.md item 42).
    const placeholder = await box(page).evaluate((element) => {
      const style = getComputedStyle(element, '::placeholder');
      return { colour: style.color, opacity: style.opacity };
    });
    expect(placeholder).toEqual({ colour: 'rgb(98, 98, 98)', opacity: '1' });
  });
}

test('the Guided notes box grows with its lines, never scrolling inside, from at least about 6 lines', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await openJanesNote(page, twoGoals());
  const size = () =>
    box(page).evaluate((element) => ({
      height: element.getBoundingClientRect().height,
      scrolls: element.scrollHeight > element.clientHeight,
    }));
  const start = await size();
  // About 6 lines of 27 px, with the padding and border, at the least.
  expect(start.height).toBeGreaterThanOrEqual(6 * 27);
  expect(start.scrolls).toBe(false);

  let last = start.height;
  for (const lines of [10, 20, 40]) {
    await box(page).fill(Array.from({ length: lines }, (_, index) => `Line ${index + 1}`).join('\n'));
    const now = await size();
    expect(now.scrolls, `${lines} lines`).toBe(false);
    expect(now.height, `${lines} lines`).toBeGreaterThan(last);
    last = now.height;
  }
});

test('at 320 px with text at 200%, no state of the form scrolls sideways, and a long name wraps whole', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const word = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const unbroken = madeUpGoal('9a5c1e4b-8d27-4f6a-a4b9-2e7a3c0f6d85', word, 2);
  const store = notesStore({
    today: worker.today,
    participants: [jane, long],
    goals: { [jane.id]: [breakfast, bus, unbroken], [long.id]: [breakfast] },
    prompts: savedPrompts,
  });
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(long.id, worker.today));
  await setTextTo200Percent(page);
  await expect(heading(page)).toContainText('A'.repeat(50));
  expect(await scrollsSideways(page), 'a name of 100 letters with one space').toBe(false);
  expect(await heading(page).evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);

  store.archive(long.id);
  await box(page).pressSequentially('Went for a walk.');
  await expect(deadEnd(page)).toBeVisible();
  expect(await scrollsSideways(page), 'the archived dead end, with the long name').toBe(false);

  await page.goto(noteAddress(jane.id, worker.today));
  await expect(goalBox(page, unbroken.text)).toBeVisible();
  expect(await scrollsSideways(page), 'a goal of a word of 105 letters').toBe(false);
  store.setGoals(jane.id, [bus, unbroken]);
  await goalBox(page, bus.text).check();
  await expect(main(page).getByText(listsChanged, { exact: true })).toBeVisible();
  expect(await scrollsSideways(page), 'the lists-changed notice').toBe(false);

  store.startElsewhere(long.id, '2026-09-30', alexStarted);
  await page.goto(noteAddress(long.id, '2026-09-30'));
  await expect(main(page).getByText(/^Alex P\. started the note for A{50} B{50} at/)).toBeVisible();
  expect(await scrollsSideways(page), 'someone else’s draft, with the long name').toBe(false);

  await failNoteReads(page);
  await page.goto(noteAddress(jane.id, worker.today));
  await expect(tryAgain(page)).toBeVisible();
  expect(await scrollsSideways(page), 'the note did not load').toBe(false);
});

test('targets: a 44 px back link, 56 px goal rows the column wide, and a 48 px Try again, on phone and laptop', async ({
  page,
}) => {
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await openJanesNote(page, twoGoals());
    await expectAtLeast(backLink(page), 44, 44);
    await expectAtLeast(goals(page).locator('label'), 44, 56);
    const column = (await boxOf(box(page))).width;
    for (const label of await goals(page).locator('label').all()) {
      expect(Math.round((await boxOf(label)).width)).toBe(Math.round(column));
    }

    // Each box drawn 40 px, inside its row.
    await expectAtLeast(goals(page).getByRole('checkbox'), 40, 40);

    await failNoteReads(page);
    await page.goto(noteAddress(jane.id, worker.today));
    await expectAtLeast(tryAgain(page), 48, 48);
  }
});

test.describe('on a touch screen', () => {
  test.use({ hasTouch: true });

  test("a goal row toggles when its text is tapped, or the row's far end, and the taps save", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    const store = twoGoals();
    await openJanesNote(page, store);
    await goalRow(page, bus.text).locator('span').tap();
    await expect(goalBox(page, bus.text)).toBeChecked();
    const rowBox = await boxOf(goalRow(page, breakfast.text));
    await page.touchscreen.tap(rowBox.x + rowBox.width - 4, rowBox.y + rowBox.height / 2);
    await expect(goalBox(page, breakfast.text)).toBeChecked();
    await goalRow(page, bus.text).locator('span').tap();
    await expect(goalBox(page, bus.text)).not.toBeChecked();
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
    expect(store.draft(jane.id, worker.today)?.ticked).toEqual([breakfast.id]);
    await expectNothingKept(page, 'Jane', 'Citizen', breakfast.text, bus.text);
  });
});

test('the top bar sticks above 30rem of height and not below, and a focused tick row is never under it', async ({
  page,
}) => {
  const many = Array.from({ length: 12 }, (_, index) =>
    madeUpGoal(`0f6d2a8b-${String(index).padStart(4, '0')}-4c1e-9b7a-3d5f8e2c6a19`, `Goal number ${index + 1}`, index),
  );
  const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: many }, prompts: savedPrompts });

  // Above 30rem, the bar sticks at the top as the page scrolls under it, the one element that does.
  await page.setViewportSize({ width: 375, height: 640 });
  await openJanesNote(page, store);
  await expect(topBar(page)).toHaveCSS('position', 'sticky');
  await box(page).scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => scrollY)).toBeGreaterThan(0);
  expect((await boxOf(topBar(page))).y).toBe(0);
  expect((await boxOf(topBar(page))).height).toBeGreaterThanOrEqual(56);
  const pinned = await page.evaluate(() =>
    [...document.querySelectorAll('*')]
      .filter((element) => ['fixed', 'sticky'].includes(getComputedStyle(element).position))
      .map((element) => element.parentElement?.id ?? element.localName),
  );
  expect(pinned).toEqual(['page-bar']);

  // Shift+Tab up from the box through each goal: each focused box, and its ring, clear of the bar.
  await box(page).focus();
  for (const goal of [...many].reverse()) {
    await page.keyboard.press('Shift+Tab');
    await expect(goalBox(page, goal.text)).toBeFocused();
    const bar = await boxOf(topBar(page));
    const focused = await boxOf(goalBox(page, goal.text));
    // The ring, 3 px wide and 2 px out from the box, as base.css draws it.
    expect(focused.y - 5, `${goal.text} under the bar`).toBeGreaterThanOrEqual(bar.y + bar.height - 0.5);
  }

  // Below 30rem, as on a phone held sideways, the bar scrolls away with the page.
  await page.setViewportSize({ width: 812, height: 375 });
  await expect(topBar(page)).toHaveCSS('position', 'static');
  await box(page).scrollIntoViewIfNeeded();
  expect((await boxOf(topBar(page))).y).toBeLessThan(0);
});

test('in forced colours, the ring is Highlight at each stop of the form, and the dead end keeps its edge', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const store = twoGoals();
  await openJanesNote(page, store);
  const [highlight, canvasText] = await Promise.all([
    systemColour(page, 'Highlight'),
    systemColour(page, 'CanvasText'),
  ]);
  expect(highlight).not.toBe(ring.colour);
  const forcedRing = { ...ring, colour: highlight };
  await expectStops(page, 'Shift+Tab', ['Today'], forcedRing);
  await expectStops(page, 'Tab', ['Past notes', breakfast.text, bus.text, '3. Guided notes'], forcedRing);

  store.archive(jane.id);
  await page.keyboard.type('Went for a walk.');
  await expect(deadEnd(page)).toBeVisible();
  await expect(deadEnd(page)).toHaveCSS('border-top-color', canvasText);
  await expect(deadEnd(page)).toHaveCSS('border-top-style', 'solid');
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the form's Tab order, Space ticks, typing saves, and the back link`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = twoGoals();
    await serveMe(page, worker);
    await serveToday(page, worker.today, participants);
    await store.serve(page, worker);
    await page.goto('/');
    await expect(rows(page)).toHaveCount(2);

    // From Today, Enter on the row opens the note, its <h1> focused.
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();

    // note-form.md *Focus order (Tab)*, for what is built: the back link before <main>, then Past notes, each goal's
    // box, Guided notes, Flag for manager and Submit note, the indicator, the <h1> and the hidden Reason no stops.
    await expectStops(page, 'Tab', [
      'Past notes',
      breakfast.text,
      bus.text,
      '3. Guided notes',
      'Flag for manager',
      'Submit note',
      'outside the page',
    ]);
    await expectStops(page, 'Shift+Tab', [
      'Submit note',
      'Flag for manager',
      '3. Guided notes',
      bus.text,
      breakfast.text,
      'Past notes',
      'Today',
      ...[...shell].reverse(),
    ]);
    await expectStops(page, 'Tab', [...shell.slice(1), 'Today', 'Past notes', breakfast.text]);

    // Space ticks the box with focus; typing in the box, then Tab out of it, saves at once.
    await page.keyboard.press('Space');
    await expect(goalBox(page, breakfast.text)).toBeChecked();
    await expect(saveStatus(page)).toHaveText('Saving…');
    await expectStops(page, 'Tab', [bus.text, '3. Guided notes']);
    await page.keyboard.type('Made toast.');
    await page.keyboard.press('Enter');
    await page.keyboard.type('Phoned her sister.');
    await expectStops(page, 'Tab', ['Flag for manager']);
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
    expect(store.draft(jane.id, worker.today)).toMatchObject({
      ticked: [breakfast.id],
      narrative: 'Made toast.\nPhoned her sister.',
    });

    // The draft saved, More actions follows Submit note (note-form.md component 13).
    await expectStops(page, 'Tab', ['Submit note', 'More actions', 'outside the page']);

    // The back link, by Shift+Tab, returns to Jane's row.
    await expectStops(page, 'Shift+Tab', [
      'More actions',
      'Submit note',
      'Flag for manager',
      '3. Guided notes',
      bus.text,
      breakfast.text,
      'Past notes',
      'Today',
    ]);
    await page.keyboard.press('Enter');
    await expect(row(page, 'Jane Citizen')).toBeFocused();
    await expectNothingKept(page, 'Jane', 'Citizen', 'Made toast', 'Phoned');
  });
}

const flagReason = 'Mentioned a sore knee after the walk.';

test('signed in for real at 375 px, a worker flags and submits a note: Today, the read view, Past notes', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
  secondInvite,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  const id = await addJaneWithTwoGoals(page);

  // The manager invites a worker, who sets up on a phone of their own from the link that admin reset-signin gives.
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(heading(page)).toHaveText('Users');
  const riley = { displayName: 'Riley Sample', email: `riley.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, riley, 'Worker');
  const phone = secondDevice.page;
  await phone.setViewportSize({ width: 375, height: 812 });
  await phone.goto(await resetSignIn(riley.email));
  await main(phone).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(rows(phone)).toHaveText([janeCitizen.name]);

  // The worker fills in Jane's note: a goal ticked, a sentence, and the flag with its reason, each saved.
  const opened = phone.waitForResponse(isDraftRead);
  await row(phone, janeCitizen.name).click();
  const template = (await (await opened).json()) as { goals: { id: string; text: string }[] };
  await expect(heading(phone)).toBeFocused();
  const address = new URL(phone.url()).pathname;
  const noteDate = address.slice(-10);
  await goalRow(phone, breakfast.text).locator('span').click();
  await box(phone).pressSequentially(sentence);
  await flagRow(phone).locator('span').click();
  await expect(flagBox(phone)).toBeChecked();
  await reasonBox(phone).fill(flagReason);
  await expect(saveStatus(phone)).toHaveText(savedAnyTime);

  // Submit note: the check save, then the confirmation with the name, the date and the flag, focused on its title.
  const checked = phone.waitForResponse(isDraftSave);
  await submitNote(phone).click();
  expect((await checked).status()).toBe(200);
  await expect(confirmationTitle(phone)).toBeFocused();
  await expect(confirmationTitle(phone)).toHaveText(`Submit today’s note for ${janeCitizen.name}`);
  await expect(confirmation(phone).locator('time')).toHaveAttribute('datetime', noteDate);
  await expect(confirmation(phone).locator('time')).toHaveText(/^\w+day \d{1,2}\s\w+ \d{4}$/);
  await expect(confirmation(phone).getByText('Flagged for manager: Yes', { exact: true })).toBeVisible();
  await expect(goBack(phone)).toBeVisible();

  // Confirmed: version 1 from the app, with the tick, the text and the flag, and one key, the note To review.
  const posted = phone.waitForResponse(isSubmit);
  await confirmButton(phone).click();
  const answer = await posted;
  expect(answer.status()).toBe(201);
  expect(answer.request().postDataJSON()).toEqual({
    baseVersion: 0,
    narrative: sentence,
    tickedGoalIds: [template.goals[0].id],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: true,
    flagReason,
  });
  expect(answer.request().headers()['idempotency-key']).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-/);
  expect(await answer.json()).toEqual({ versionNumber: 1, createdAtUtc: expect.any(String), flagStatus: 'ToReview' });

  // Today, with the banner focused and Jane's row Submitted by "You", flagged.
  await expect(phone).toHaveURL('/');
  await expect(banner(phone)).toBeFocused();
  await expect(banner(phone)).toHaveText(`Note for ${janeCitizen.name} submitted`);
  await expect.poll(() => shownText(rowLine(phone, janeCitizen.name))).toMatch(
    new RegExp(`^Submitted · You · ${anyTime} Flagged$`),
  );

  /** Expects the read view of the note that Riley submitted, as anyone in the organisation sees it. */
  async function expectRileysNote(on: Page) {
    await expect(heading(on)).toBeFocused();
    await expect(heading(on)).toHaveText(new RegExp(`^${janeCitizen.name}\\s\\w+day \\d{1,2}\\s\\w+ \\d{4}$`));
    await expect.poll(() => shownText(byline(on))).toMatch(
      new RegExp(`^Written by Riley Sample · Submitted ${anyTime}$`),
    );
    await expect.poll(() => shownText(flagLine(on))).toBe(`Flagged Reason: ${flagReason}`);
    await expect(readGoals(on)).toHaveText([`${breakfast.text}, ticked`, `${bus.text}, not ticked`]);
    await expect(readText(on)).toHaveText(sentence);
    await expect(main(on).getByRole('checkbox')).toHaveCount(0);
    await expect(main(on).getByRole('textbox')).toHaveCount(0);
    await expect(saveStatus(on)).toHaveCount(0);
  }

  // Her row opens the read view, from the app's GET {base}; its Past notes lists the note, and its back link returns.
  const read = phone.waitForResponse((response) => new URL(response.url()).pathname === `/api${address}`);
  await row(phone, janeCitizen.name).click();
  expect((await read).status()).toBe(200);
  await expect(phone).toHaveURL(address);
  await expectRileysNote(phone);
  await pastNotesLink(phone).click();
  await expect(phone).toHaveURL(`/participants/${id}/notes`);
  await expect(heading(phone)).toBeFocused();
  await expect(heading(phone)).toHaveText(`Past notes ${janeCitizen.name}`);
  await expect(rows(phone)).toHaveCount(1);
  await expect(rows(phone).locator('time')).toHaveAttribute('datetime', noteDate);
  await expect.poll(() => shownText(main(phone).getByRole('listitem').locator('p'))).toBe(
    'Submitted · Riley Sample Flagged',
  );
  await phone.locator('#page-bar').getByRole('link', { name: 'Note', exact: true }).click();
  await expect(phone).toHaveURL(address);
  await expectRileysNote(phone);
  await expectNothingKept(phone, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason, 'submitted');

  // The manager's Today shows the note as Riley's, flagged, and opens the same read view.
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await expect.poll(() => shownText(rowLine(page, janeCitizen.name))).toMatch(
    new RegExp(`^Submitted · Riley Sample · ${anyTime} Flagged$`),
  );
  await row(page, janeCitizen.name).click();
  await expectRileysNote(page);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason);

  // The other organisation's manager meets Page not found at the note and at its Past notes.
  await page.getByRole('button', { name: 'Account' }).click();
  await page.locator('#account-panel').getByRole('button').click();
  await expect(main(page).getByRole('heading', { level: 1 })).toHaveText('Sign in');
  await signInThroughSetup(page, secondInvite, authenticator);
  for (const elsewhere of [address, `/participants/${id}/notes`]) {
    const refused = page.waitForResponse(
      (response) => new URL(response.url()).pathname === `/api/participants/${id}`,
    );
    await page.goto(elsewhere);
    expect((await refused).status()).toBe(404);
    await expect(heading(page)).toHaveText(notFound);
    await expect(main(page).getByText(janeCitizen.name)).toHaveCount(0);
    await expect(main(page).getByText(sentence)).toHaveCount(0);
  }
});

test('a lost answer to Submit, tried again in the confirmation with the same key, makes one version', async ({
  page,
}) => {
  const store = twoGoals();
  await openJanesNote(page, store);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await submitNote(page).click();
  await expect(confirmationTitle(page)).toBeFocused();

  // The version is made, but its answer is lost on the way: the failure sentence, with focus kept on the button.
  store.loseNextSubmitAnswer();
  await confirmButton(page).click();
  await expect(confirmationAlert(page)).toHaveText(notSubmittedOffline);
  await expect(confirmButton(page)).toBeFocused();
  expect(store.submitted(jane.id, worker.today)).toMatchObject({ narrative: 'Went to the shops.' });

  // Pressed again: the same key, answered 200 with the version already made, unflagged, and Today.
  const retried = page.waitForResponse(isSubmit);
  await confirmButton(page).click();
  const answer = await retried;
  expect(answer.status()).toBe(200);
  expect(await answer.json()).toEqual({
    versionNumber: 1,
    createdAtUtc: '2026-10-01T06:12:00.000Z',
    flagStatus: 'None',
  });
  await expect(banner(page)).toBeFocused();
  await expect(banner(page)).toHaveText('Note for Jane Citizen submitted');
  const [first, second] = store.submits;
  expect(store.submits).toHaveLength(2);
  expect(first.idempotencyKey).toEqual(expect.any(String));
  expect(second).toEqual(first);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', 'submitted');
});

test('Submit: the flag ticked, errors, each state of the confirmation and the conflict, each passing axe', async ({
  page,
}) => {
  test.slow();
  const store = twoGoals();
  await openJanesNote(page, store);

  // The flag ticked: Reason shows, with focus kept on the box and its count, and the tick saved.
  await flagBox(page).check();
  await expect(flagBox(page)).toBeFocused();
  await expect(flagBox(page)).toHaveAttribute('aria-expanded', 'true');
  await expect(reasonBox(page)).toBeVisible();
  await expect(reasonCount(page)).toHaveText('You can enter up to 200 characters');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  expect(store.saves.at(-1)).toMatchObject({ isFlagged: true, flagReason: null });
  await expectAxeToPass(page);

  // Field errors: Guided notes empty and no reason, each inline above its field and in the summary, which is focused,
  // with "Error: " in the title, and nothing opened or sent.
  await submitNote(page).click();
  await expect(summary(page)).toBeFocused();
  await expect(summary(page).getByRole('link')).toHaveText([
    'Write your notes in the Guided notes box. The grey text is only a guide.',
    'Enter a reason for the flag',
  ]);
  await expect(page).toHaveTitle(`Error: ${title('Note')}`);
  await expect(notesError(page)).toHaveText(
    'Error: Write your notes in the Guided notes box. The grey text is only a guide.',
  );
  await expect(reasonError(page)).toHaveText('Error: Enter a reason for the flag');
  await expect(box(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(reasonBox(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(confirmation(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // Each goes with the keystroke that fixes it, and the summary and the title's "Error: " with the last.
  await box(page).pressSequentially('W');
  await expect(notesError(page)).toHaveCount(0);
  await expect(summary(page).getByRole('link')).toHaveText(['Enter a reason for the flag']);
  await reasonBox(page).pressSequentially('S');
  await expect(summary(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Note'));
  await reasonBox(page).pressSequentially('eemed tired.');
  await box(page).pressSequentially('ent for a walk.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  // Open: the title focused, naming the dialog with the name; the date and the flag line its description.
  await submitNote(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  await expect(confirmation(page)).toHaveAccessibleName('Submit today’s note for Jane Citizen');
  await expect(confirmation(page)).toHaveAccessibleDescription(/^Thursday 1\sOctober 2026 ?Flagged for manager: Yes$/);
  await expect(confirmButton(page)).toBeVisible();
  await expect(confirmationAlert(page)).toBeEmpty();
  await expect(confirmationStatus(page)).toBeEmpty();
  await expectAxeToPass(page);

  // Busy: both buttons aria-disabled, their labels kept, and "Submitting…" after 1 s.
  const held = await holdNext(page, submitRequests);
  await confirmButton(page).click();
  const post = await held();
  await expect(confirmationStatus(page)).toHaveText('Submitting…');
  await expect(confirmButton(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(goBack(page)).toHaveAttribute('aria-disabled', 'true');
  await expectAxeToPass(page);

  // Failed, with no connection and then from the server: the dialog kept open, with focus on the confirm button.
  await post.abort('internetdisconnected');
  await expect(confirmationAlert(page)).toHaveText(notSubmittedOffline);
  await expect(confirmButton(page)).toBeFocused();
  await expect(confirmationStatus(page)).toBeEmpty();
  await expectAxeToPass(page);
  const failed = await holdNext(page, submitRequests);
  await confirmButton(page).click();
  await (await failed()).fulfill({ status: 503 });
  await expect(confirmationAlert(page)).toHaveText(notSubmittedServer);
  await expect(confirmButton(page)).toBeFocused();
  await expectAxeToPass(page);

  // A 422 for a text that the form's checks pass, U+0085 alone: the dialog closed, and the summary's one unlinked
  // item, focused.
  await goBack(page).click();
  await expect(confirmation(page)).toHaveCount(0);
  await expect(submitNote(page)).toBeFocused();
  await box(page).fill('\u0085');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await submitNote(page).click();
  await confirmButton(page).click();
  await expect(confirmation(page)).toHaveCount(0);
  await expect(summary(page)).toBeFocused();
  await expect(summary(page).getByRole('listitem')).toHaveText([notSubmittedServer]);
  await expect(summary(page).getByRole('link')).toHaveCount(0);
  await expectAxeToPass(page);

  // No longer possible: the note submitted in another tab meanwhile, the confirm button gone and focus on Go back.
  await box(page).fill('Went for a walk.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await submitNote(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  store.submitElsewhere(jane.id, worker.today);
  await confirmButton(page).click();
  await expect(confirmationAlert(page)).toHaveText(changedElsewhere);
  await expect(confirmButton(page)).toHaveCount(0);
  await expect(goBack(page)).toBeFocused();
  await expectAxeToPass(page);

  // Go back: the form a dead end, its text kept, Submit note unavailable, and a press opening nothing.
  await goBack(page).click();
  await expect(confirmation(page)).toHaveCount(0);
  await expect(deadEnd(page)).toHaveText(changedElsewhere);
  await expect(alert(page)).toHaveText(changedElsewhere);
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expect(box(page)).toHaveValue('Went for a walk.');
  await expect(submitNote(page)).toHaveAttribute('aria-disabled', 'true');
  await expectAxeToPass(page);
  const sent = store.submits.length;
  await submitNote(page).click({ force: true });
  await expect(pageStatus(page)).toHaveText(changedElsewhere);
  await expect(confirmation(page)).toHaveCount(0);
  expect(store.submits).toHaveLength(sent);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went for a walk', 'Seemed tired', changedElsewhere);
});

test('a note submitted in another tab: the next save is a dead end, the text kept, Submit opens nothing', async ({
  page,
}) => {
  const store = twoGoals();
  await openJanesNote(page, store);
  await box(page).pressSequentially('Went');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  store.submitElsewhere(jane.id, worker.today);
  await box(page).pressSequentially(' for a walk.');
  await expect(deadEnd(page)).toBeVisible();
  await expect(submitNote(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(submitNote(page)).toHaveAccessibleDescription(`Not saved ${changedElsewhere}`);
  await submitNote(page).click({ force: true });
  await expect(pageStatus(page)).toHaveText(changedElsewhere);
  await expect(confirmation(page)).toHaveCount(0);
  expect(store.submits).toEqual([]);
  // Back at the top, as the press scrolled Submit note into view: axe's target-size rule counts only the part of a
  // target that the sticky top bar leaves uncovered, and the bar covers part of the Past notes link at some scroll
  // positions, as any sticky bar covers what passes under it.
  await page.evaluate(() => window.scrollTo(0, 0));
  await expectDeadEnd(page, store, changedElsewhere, 'Went for a walk.');
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went for a walk', changedElsewhere);
});

test('at 320 px with text at 200%, the flag, errors and confirmation never scroll sideways', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const longName = `${'A'.repeat(50)} ${'B'.repeat(50)}`;
  const store = notesStore({ today: worker.today, participants: [long], prompts: savedPrompts });
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(long.id, worker.today));
  await setTextTo200Percent(page);
  await expect(heading(page)).toContainText('A'.repeat(50));

  // The flag ticked, with a reason of 210 characters, and its count.
  await flagBox(page).check();
  await reasonBox(page).fill('Wolfeschlegelsteinhausenbergerdorff'.repeat(6));
  await expect(reasonCount(page)).toHaveText('You have 10 characters too many');
  expect(await scrollsSideways(page), 'the flag ticked, the reason too long').toBe(false);

  // Both fields' errors and the summary.
  await submitNote(page).click();
  await expect(summary(page)).toBeFocused();
  expect(await scrollsSideways(page), 'the errors').toBe(false);

  // The confirmation, its title and its confirm button each holding the name whole, inside the dialog.
  await reasonBox(page).fill('Seemed tired.');
  await box(page).pressSequentially('Went for a walk.');
  await submitNote(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  await expect(confirmationTitle(page)).toHaveText(`Submit today’s note for ${longName}`);
  expect(await scrollsSideways(page), 'the confirmation').toBe(false);
  for (const part of [confirmation(page), confirmationTitle(page), confirmButton(page, longName)]) {
    expect(await part.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    const { x, width } = await boxOf(part);
    expect(x + width).toBeLessThanOrEqual(320);
  }
});

test('targets: a 56 px flag row, a 48 px Submit note, a 44 px Past notes link and 48 px dialog buttons', async ({
  page,
}) => {
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await openJanesNote(page, twoGoals());
    await expectAtLeast(pastNotesLink(page), 44, 44);
    await expectAtLeast(flagRow(page), 44, 56);
    expect(Math.round((await boxOf(flagRow(page))).width)).toBe(Math.round((await boxOf(box(page))).width));
    await expectAtLeast(flagBox(page), 40, 40);
    await expectAtLeast(submitNote(page), 44, 48);
    await box(page).pressSequentially('Went for a walk.');
    await submitNote(page).click();
    await expectAtLeast(confirmation(page).getByRole('button'), 44, 48);
  }
});

test('in forced colours, the ring is Highlight on the flag, Reason, Submit note and the dialog’s buttons', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await openJanesNote(page, twoGoals());
  const forcedRing = { ...ring, colour: await systemColour(page, 'Highlight') };
  await box(page).pressSequentially('Went for a walk.');
  await expectStops(page, 'Tab', ['Flag for manager'], forcedRing);
  await page.keyboard.press('Space');
  await expectStops(page, 'Tab', ['Reason'], forcedRing);
  await page.keyboard.type('Seemed tired.');
  await expectStops(page, 'Tab', ['Submit note'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(confirmationTitle(page)).toBeFocused();
  await expectStops(page, 'Tab', ['Submit note for Jane Citizen', 'Go back'], forcedRing);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the flag, Reason, Submit note, its confirmation, then Today's banner`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = twoGoals();
    await serveMe(page, worker);
    await serveToday(page, worker.today, participants, store.todayNotes(worker));
    await store.serve(page, worker);
    await page.goto('/');
    await expect(rows(page)).toHaveCount(2);
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();

    // note-form.md *Focus order (Tab)*: Flag for manager after Guided notes, then Reason only while it is ticked, then
    // Submit note; Space ticks the flag, with focus kept on it.
    await expectStops(page, 'Tab', ['Past notes', breakfast.text, bus.text, '3. Guided notes']);
    await page.keyboard.type('Made toast.');
    await expectStops(page, 'Tab', ['Flag for manager']);
    await page.keyboard.press('Space');
    await expect(flagBox(page)).toBeChecked();
    await expect(flagBox(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Reason']);
    await page.keyboard.type('Seemed tired.');
    await expectStops(page, 'Tab', ['Submit note']);
    // The draft saved, More actions follows Submit note (note-form.md component 13).
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
    await expectStops(page, 'Tab', ['More actions', 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      'More actions',
      'Submit note',
      'Reason',
      'Flag for manager',
      '3. Guided notes',
    ]);
    await expectStops(page, 'Tab', ['Flag for manager', 'Reason', 'Submit note']);

    // Enter on Submit note: the confirmation, focused on its title, then its two buttons; Escape closes it, with focus
    // back on Submit note and nothing sent.
    await page.keyboard.press('Enter');
    await expect(confirmationTitle(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Submit note for Jane Citizen', 'Go back']);
    await page.keyboard.press('Escape');
    await expect(confirmation(page)).toHaveCount(0);
    await expect(submitNote(page)).toBeFocused();
    expect(store.submits).toEqual([]);

    // Enter again, then Enter on the confirm button: Today, the banner focused, and the rows after it.
    await page.keyboard.press('Enter');
    await expect(confirmationTitle(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Submit note for Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(banner(page)).toBeFocused();
    await expect.poll(() => shownText(rowLine(page, 'Jane Citizen'))).toBe('Submitted · You · 4:12 pm Flagged');
    await expectStops(page, 'Tab', ['Find a participant', 'Jane Citizen', 'Ravi de Silva', 'outside the page']);
    expect(store.submitted(jane.id, worker.today)).toMatchObject({
      narrative: 'Made toast.',
      isFlagged: true,
      flagReason: 'Seemed tired.',
    });
    await expectNothingKept(page, 'Jane', 'Citizen', 'Made toast', 'Seemed tired', 'submitted');
  });
}

// Autosave through a dropped signal, a lost answer, a second tab, a closed tab, leaving the form and the session's end
// (T10: S02.02.04 to S02.02.06), against note-form.md's items 9, 11 to 16, 34 and 36.

/** The save indicator's failed state, the top bar's second row (design.md §4.3). */
const notSavedRetrying = 'Not saved: no connection. Keep this page open; retrying.';
// The takeover banner, the top bar's second row, named by its sentence, and its two choices (note-form.md component 1).
const takeover = (page: Page) => page.locator('#page-bar').getByRole('region', { name: changedElsewhere });
const keepButton = (page: Page) =>
  takeover(page).getByRole('button', { name: 'Keep the text on this screen', exact: true });
const loadButton = (page: Page) => takeover(page).getByRole('button', { name: 'Load the other version', exact: true });

/**
 * Records the body of each save of a note's working copy that `page` sends from now on, across page loads. Gives a
 * function that gives them once the browser has reported every request that the page sent before the call, as
 * `recordRequests` does (tests/README.md), which it can only while the page is online.
 */
function recordSaves(page: Page): () => Promise<DraftSave[]> {
  const sent: DraftSave[] = [];
  page.on('request', (request) => {
    if (isDraftSaveRequest(request)) {
      sent.push(request.postDataJSON() as DraftSave);
    }
  });
  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return [...sent];
  };
}

/**
 * Opens Jane Citizen's note from her row on Today, signed in for real, waits for its <h1>, and gives her two goals' IDs
 * from the form's read, in the manager's order.
 */
async function openJaneCitizensNote(page: Page): Promise<string[]> {
  const opened = page.waitForResponse(isDraftRead);
  await row(page, janeCitizen.name).click();
  const { goals: listed } = (await (await opened).json()) as { goals: { id: string }[] };
  await expect(heading(page)).toBeFocused();
  return listed.map(({ id }) => id);
}

/** Makes the page hidden, as a switch to another app does, or visible again, as the browser reports it. */
async function setHidden(page: Page, hidden: boolean) {
  await page.evaluate((isHidden) => {
    if (isHidden) {
      Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
    } else {
      Reflect.deleteProperty(document, 'visibilityState');
    }

    document.dispatchEvent(new Event('visibilitychange'));
  }, hidden);
}

test('signed in for real at 375 px, offline: the sentence in about 1 s, Submit unavailable, Saved unasked', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  await openJaneCitizensNote(page);
  const started = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially('Went to the shops.');
  expect((await started).status()).toBe(201);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  // The signal drops as the person types on: the indicator's exact sentence, as the top bar's second row, within about
  // 1 s, with Submit note unavailable and described by it (note-form.md item 11).
  await page.context().setOffline(true);
  await box(page).pressSequentially(' Bought bread.');
  await expect(saveStatus(page)).toHaveText(notSavedRetrying, { timeout: 1_000 });
  await expect(submitNote(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(submitNote(page)).toHaveAccessibleDescription(notSavedRetrying);

  // A press of Submit note opens nothing and says the sentence again in the page's polite region (note-form.md
  // item 25).
  await submitNote(page).click({ force: true });
  await expect(pageStatus(page)).toHaveText(notSavedRetrying);
  await expect(confirmation(page)).toHaveCount(0);

  // Online again, the app's answer to the save that waited brings "Saved …" with no action.
  const recovered = page.waitForResponse(isDraftSave);
  await page.context().setOffline(false);
  expect((await recovered).status()).toBe(200);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await expect(submitNote(page)).not.toHaveAttribute('aria-disabled', 'true');

  // Opened again, the draft holds what was typed while offline.
  await backLink(page).click();
  await row(page, janeCitizen.name).click();
  await expect.poll(() => shownText(saveStatus(page))).toMatch(draftSavedAnyTime);
  await expect(box(page)).toHaveValue('Went to the shops. Bought bread.');
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops', notSavedRetrying);
});

test('signed in for real at 375 px, the first save’s answer lost: sent again, Saved, with no banner, one note', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  await openJaneCitizensNote(page);
  const address = new URL(page.url()).pathname;
  const saves = recordSaves(page);

  // The first save reaches the app, which creates the note, but its answer is lost on the way back (design.md §14 M2).
  let lostAnswer: number | undefined;
  await page.route(draftRequests, async (route) => {
    if (route.request().method() !== 'PUT' || lostAnswer !== undefined) {
      await route.fallback();
      return;
    }

    lostAnswer = (await route.fetch()).status();
    await route.abort('internetdisconnected');
  });
  const retried = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(sentence);

  // Its retry, a second later, is the page's next save, which the app applies: "Saved …", and no takeover banner
  // (note-form.md item 12).
  const answer = await retried;
  expect(lostAnswer).toBe(201);
  expect(answer.status()).toBe(200);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await expect(takeover(page)).toHaveCount(0);
  await expect(alert(page)).toBeEmpty();
  const [first, again, ...more] = await saves();
  expect(more).toEqual([]);
  expect(again).toEqual({ ...first, seq: first.seq + 1 });

  // Saved once: Jane's one draft on Today, and the text as typed, once, as the note opens again.
  await backLink(page).click();
  await expect(rows(page)).toHaveText([janeCitizen.name]);
  await expect.poll(() => shownText(rowLine(page, janeCitizen.name))).toMatch(
    new RegExp(`^Draft · You · saved ${anyTime}$`),
  );
  await row(page, janeCitizen.name).click();
  await expect(page).toHaveURL(address);
  await expect(box(page)).toHaveValue(sentence);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence);
});

test('signed in for real at 375 px, two tabs on a draft: the older meets the banner, Keep wins, Load replaces', async ({
  page,
  invite,
  authenticator,
  secondTab,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  const [breakfastId, busId] = await openJaneCitizensNote(page);
  const address = new URL(page.url()).pathname;
  await goalRow(page, breakfast.text).locator('span').click();
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  // A second tab opens the draft and adds to it: its first save takes the working copy over.
  await secondTab.setViewportSize({ width: 375, height: 812 });
  await secondTab.goto(address);
  await expect(box(secondTab)).toHaveValue('Went to the shops.');
  await expect(goalBox(secondTab, breakfast.text)).toBeChecked();
  await box(secondTab).press('ControlOrMeta+End');
  const tookOver = secondTab.waitForResponse(isDraftSave);
  await box(secondTab).pressSequentially(' Typed in the second tab.');
  const tookOverAnswer = await tookOver;
  expect(tookOverAnswer.status()).toBe(200);
  const secondTabFirst = tookOverAnswer.request().postDataJSON() as DraftSave;
  expect(secondTabFirst.seq).toBe(1);
  await expect(saveStatus(secondTab)).toHaveText(savedAnyTime);

  // The first tab's next save meets the banner (note-form.md item 15): "Not saved", the sentence said once, focus kept
  // in the box, and Submit note unavailable.
  const refused = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(' Bought bread.');
  const refusedAnswer = await refused;
  expect(refusedAnswer.status()).toBe(409);
  expect(await refusedAnswer.json()).toMatchObject({ code: 'draft.taken_over' });
  await expect(takeover(page)).toBeVisible();
  await expect(saveStatus(page)).toHaveText('Not saved');
  await expect(alert(page)).toHaveText(changedElsewhere);
  await expect(box(page)).toBeFocused();
  await expect(submitNote(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(box(page)).toHaveValue('Went to the shops. Bought bread.');

  // Typing and ticking go on, kept and not sent, past the 2 s at which a save would go, and past a blur.
  const sentByFirstTab = recordSaves(page);
  await box(page).pressSequentially(' Walked home.');
  await goalBox(page, bus.text).check();
  await flagBox(page).check();
  await reasonBox(page).fill(flagReason);
  await page.waitForTimeout(2_500);
  expect(await sentByFirstTab()).toEqual([]);

  // Keep the text on this screen: everything on screen, under a new clientId as the page's save 1, which the app
  // applies; the banner gone, focus back in the box, and "Saved …".
  const kept = page.waitForResponse(isDraftSave);
  await keepButton(page).click();
  const keptAnswer = await kept;
  expect(keptAnswer.status()).toBe(200);
  const firstTabText = 'Went to the shops. Bought bread. Walked home.';
  const keptBody = keptAnswer.request().postDataJSON() as DraftSave;
  expect(keptBody).toEqual({
    clientId: expect.any(String),
    seq: 1,
    narrative: firstTabText,
    tickedGoalIds: [breakfastId, busId],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: true,
    flagReason,
  });
  expect(keptBody.clientId).not.toBe((refusedAnswer.request().postDataJSON() as DraftSave).clientId);
  await expect(takeover(page)).toHaveCount(0);
  await expect(box(page)).toBeFocused();
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await expect(submitNote(page)).not.toHaveAttribute('aria-disabled', 'true');

  // Keep won: the second tab's next save meets the banner in turn, and nothing on its screen changes until Load.
  const secondRefused = secondTab.waitForResponse(isDraftSave);
  await box(secondTab).pressSequentially(' Lost to Load.');
  expect((await secondRefused).status()).toBe(409);
  await expect(takeover(secondTab)).toBeVisible();
  await expect(box(secondTab)).toHaveValue('Went to the shops. Typed in the second tab. Lost to Load.');
  await expect(goalBox(secondTab, bus.text)).not.toBeChecked();
  await expect(flagBox(secondTab)).not.toBeChecked();

  // Load the other version replaces the ticks, the text, the flag and the reason, with focus on the <h1> and "Saved …"
  // at the other version's time (note-form.md items 15 and 16).
  const reread = secondTab.waitForResponse(isDraftRead);
  await loadButton(secondTab).click();
  expect((await reread).status()).toBe(200);
  await expect(heading(secondTab)).toBeFocused();
  await expect(takeover(secondTab)).toHaveCount(0);
  await expect(box(secondTab)).toHaveValue(firstTabText);
  await expect(goalBox(secondTab, breakfast.text)).toBeChecked();
  await expect(goalBox(secondTab, bus.text)).toBeChecked();
  await expect(flagBox(secondTab)).toBeChecked();
  await expect(reasonBox(secondTab)).toHaveValue(flagReason);
  await expect(saveStatus(secondTab)).toHaveText(savedAnyTime);

  // Its next save goes under a new clientId as save 1, which takes the working copy over again.
  await box(secondTab).press('ControlOrMeta+End');
  const loadedNext = secondTab.waitForResponse(isDraftSave);
  await box(secondTab).pressSequentially(' Had tea.');
  const loadedNextAnswer = await loadedNext;
  expect(loadedNextAnswer.status()).toBe(200);
  const loadedNextBody = loadedNextAnswer.request().postDataJSON() as DraftSave;
  expect(loadedNextBody).toMatchObject({ seq: 1, narrative: `${firstTabText} Had tea.` });
  expect(loadedNextBody.clientId).not.toBe(secondTabFirst.clientId);
  for (const tab of [page, secondTab]) {
    await expectNothingKept(tab, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops', changedElsewhere);
  }
});

test('signed in for real, a tab killed mid-sentence: the note opened again lacks at most its last few seconds', async ({
  page,
  invite,
  authenticator,
  secondTab,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  const address = (await row(page, janeCitizen.name).getAttribute('href')) ?? '';

  // In a tab of its own, a sentence, saved; then a long one typed with no pause, at 20 characters a second at most,
  // and the tab killed in the middle of it, as a phone kills a tab in the background, so that nothing more of the page
  // runs, not even the page-hidden save that a tab closed by the browser sends (note-form.md item 9, which its own test
  // proves): the note keeps only what the saves made while typing carried.
  await secondTab.setViewportSize({ width: 375, height: 812 });
  await secondTab.goto(address);
  const first = 'Went to the shops and bought bread.';
  await box(secondTab).pressSequentially(first);
  await expect(saveStatus(secondTab)).toHaveText(savedAnyTime);
  const second =
    ' Then she walked home along the river, stopping twice to rest on the benches by the boat ramp, and talked about'
    + ' the ducks, the weather and her sister, whom she phoned as soon as she got in, to say';
  await box(secondTab).pressSequentially(second, { delay: 50 });
  const crashed = secondTab.waitForEvent('crash');
  // Chromium's chrome://crash, as Playwright's own tests crash a page: the renderer stops at once, unloading nothing,
  // so that this navigation never ends.
  void secondTab.goto('chrome://crash').catch(() => {});
  await crashed;
  await secondTab.close();

  // Opened again in the first tab, the note holds the text as typed but for its last few seconds (design.md §14 M2):
  // a save goes at most 5 s after the first change that it carries, so every character typed more than 6 s before the
  // tab was killed, and so some of the second sentence.
  await row(page, janeCitizen.name).click();
  await expect(box(page)).toBeVisible();
  const kept = await box(page).inputValue();
  const typed = first + second;
  expect(typed.startsWith(kept), 'the text kept is the start of the text typed').toBe(true);
  expect(kept.length, 'some of the second sentence').toBeGreaterThan(first.length);
  expect(typed.length - kept.length, 'the characters lost').toBeLessThanOrEqual(6 * 20);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, first);
});

test('signed in for real, the form left and opened again in the same page load saves on from the next seq', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  await openJaneCitizensNote(page);
  const saves = recordSaves(page);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  const [last] = (await saves()).slice(-1);

  // Left by the back link and opened again: the next save is the page's next, under the same clientId, which the app
  // applies rather than taking it for one that it holds (note-form.md item 13).
  await backLink(page).click();
  await row(page, janeCitizen.name).click();
  await expect.poll(() => shownText(saveStatus(page))).toMatch(draftSavedAnyTime);
  await box(page).press('ControlOrMeta+End');
  const next = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(' Bought bread.');
  const answer = await next;
  expect(answer.status()).toBe(200);
  expect(answer.request().postDataJSON()).toMatchObject({
    clientId: last.clientId,
    seq: last.seq + 1,
    narrative: 'Went to the shops. Bought bread.',
  });
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  // Opened once more, it holds the words added.
  await backLink(page).click();
  await row(page, janeCitizen.name).click();
  await expect(box(page)).toHaveValue('Went to the shops. Bought bread.');
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops');
});

test('signed in for real at 375 px, the back link and Past notes save a change, then go; offline, they stay', async ({
  page,
  invite,
  authenticator,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  const id = await addJaneWithTwoGoals(page);
  await openJaneCitizensNote(page);
  const address = new URL(page.url()).pathname;
  const dialogs = browserDialogs(page);

  // The back link with a change waiting: the change sent at once, "Saving…" while it is out, and Today only once the
  // app has answered it (note-form.md item 34).
  const heldSave = await holdNext(page, draftRequests);
  await box(page).pressSequentially('Went to the shops.');
  await backLink(page).click();
  const save = await heldSave();
  expect(save.request().method()).toBe('PUT');
  await expect(saveStatus(page)).toHaveText('Saving…');
  await expect(page).toHaveURL(address);
  const saved = page.waitForResponse(isDraftSave);
  await save.continue();
  expect((await saved).status()).toBe(201);
  await expect(page).toHaveURL('/');
  await expect(row(page, janeCitizen.name)).toBeFocused();

  // Past notes, under the <h1>, with a change waiting: the change saved, then Past notes.
  await row(page, janeCitizen.name).click();
  await box(page).press('ControlOrMeta+End');
  const second = page.waitForResponse(isDraftSave);
  await box(page).pressSequentially(' Bought bread.');
  await pastNotesLink(page).click();
  expect((await second).status()).toBe(200);
  await expect(page).toHaveURL(`/participants/${id}/notes`);
  await expect(heading(page)).toHaveText(`Past notes ${janeCitizen.name}`);

  // Offline, with "Not saved: …" shown: each link stays, saying the sentence again in the page's polite region.
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await row(page, janeCitizen.name).click();
  await expect(box(page)).toHaveValue('Went to the shops. Bought bread.');
  await page.context().setOffline(true);
  await box(page).press('ControlOrMeta+End');
  await box(page).pressSequentially(' Walked home.');
  await expect(saveStatus(page)).toHaveText(notSavedRetrying);
  for (const link of [backLink(page), pastNotesLink(page)]) {
    await pageStatus(page).evaluate((region) => {
      region.textContent = '';
    });
    await link.click();
    await expect(pageStatus(page)).toHaveText(notSavedRetrying);
    await expect(page).toHaveURL(address);
    await expect(box(page)).toHaveValue('Went to the shops. Bought bread. Walked home.');
  }

  // Online again, "Saved …" with no action, and the back link leaves at once.
  await page.context().setOffline(false);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await backLink(page).click();
  await expect(page).toHaveURL('/');
  expect(dialogs).toEqual([]);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops', notSavedRetrying);
});

test('signed in for real, a session ended mid-note: the same person signs in again, one save; another, Today', async ({
  page,
  invite,
  secondInvite,
  authenticator,
}) => {
  test.slow();
  // The other person, of an organisation of their own, set up first with a password, then gone from the browser, so
  // that the virtual authenticator holds only the first person's passkey (sign-in-again.spec.ts).
  const other = await setUpWithPassword(page, secondInvite);
  await page.context().clearCookies();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  const [breakfastId] = await openJaneCitizensNote(page);
  const address = new URL(page.url()).pathname;
  const saves = recordSaves(page);
  await goalRow(page, breakfast.text).locator('span').click();
  await flagBox(page).check();
  await reasonBox(page).fill(flagReason);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  const sentBefore = await saves();
  const [lastBefore] = sentBefore.slice(-1);

  // A change waiting as the app's session ends: view C in place, at the same address, and nothing sent while it shows,
  // past the 5 s within which a save would go.
  await box(page).pressSequentially(' Bought bread.');
  await endTheSession(page);
  await expect(heading(page)).toHaveText('You’ve been signed out');
  await expect(page).toHaveURL(address);
  await page.waitForTimeout(5_500);
  expect(await saves()).toHaveLength(sentBefore.length);

  // The same person signs in again with their passkey: the form as it was, with focus on its <h1>, and one save, under
  // the same clientId as the page's next, which the app applies, with no takeover banner (note-form.md item 36).
  const resent = page.waitForResponse(isDraftSave);
  await signInAgainWithPasskey(page);
  const resentAnswer = await resent;
  expect(resentAnswer.status()).toBe(200);
  expect(resentAnswer.request().postDataJSON()).toEqual({
    clientId: lastBefore.clientId,
    seq: lastBefore.seq + 1,
    narrative: 'Went to the shops. Bought bread.',
    tickedGoalIds: [breakfastId],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: true,
    flagReason,
  });
  await expect(heading(page)).toHaveText(new RegExp(`^${janeCitizen.name} `));
  await expect(heading(page)).toBeFocused();
  await expect(goalBox(page, breakfast.text)).toBeChecked();
  await expect(reasonBox(page)).toHaveValue(flagReason);
  await expect(box(page)).toHaveValue('Went to the shops. Bought bread.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await expect(takeover(page)).toHaveCount(0);
  await expect(page).toHaveURL(address);
  expect(await saves()).toHaveLength(sentBefore.length + 1);

  // Another change waiting as the session ends again, and the other person signs in on view C: a full page load of
  // their Today, with nothing of the first person's sent.
  await box(page).pressSequentially(' Walked home.');
  await endTheSession(page);
  await main(page).getByRole('textbox', { name: 'Email address' }).fill(secondInvite.email);
  await page.getByLabel('Password', { exact: true }).fill(other.password);
  await main(page).getByRole('button', { name: 'Continue' }).click();
  await page.getByLabel(codeLabel).fill(authenticatorCode(other.key));
  const reload = page.waitForRequest((request) => request.isNavigationRequest());
  await main(page).getByRole('button', { name: 'Sign in', exact: true }).click();
  expect(new URL((await reload).url()).pathname).toBe('/');
  await expect(heading(page)).toContainText('Today');
  await expect(page).toHaveURL('/');
  await expect(box(page)).toHaveCount(0);
  expect(await saves()).toHaveLength(sentBefore.length + 1);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, 'Went to the shops', flagReason);
});

test('the page made hidden sends the newest state at once, with keepalive, while autosave’s timers wait', async ({
  page,
}) => {
  const store = twoGoals();
  // The page's clock, paused once the form is open, so that none of autosave's timers sends a save.
  await page.clock.install();
  await openJanesNote(page, store);
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  // Each request's method and keepalive, as the page's own fetch is called with them.
  await page.evaluate(() => {
    const sent: { method: string; keepalive: boolean | undefined }[] = [];
    Object.assign(window, { fetchesSent: sent });
    const send = window.fetch.bind(window);
    window.fetch = (input, init) => {
      sent.push({ method: init?.method ?? 'GET', keepalive: init?.keepalive });
      return send(input, init);
    };
  });
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saving…');
  expect(store.saves).toEqual([]);

  // Hidden, as on a switch to another app (note-form.md item 9): the save goes at once, with keepalive, and its answer
  // shows as any save's.
  const sentAsHidden = page.waitForRequest(isDraftSaveRequest);
  await setHidden(page, true);
  expect((await sentAsHidden).postDataJSON()).toMatchObject({ seq: 1, narrative: 'Went to the shops.' });
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  const fetchesSent = await page.evaluate(
    () => (window as unknown as { fetchesSent: { method: string; keepalive: boolean | undefined }[] }).fetchesSent,
  );
  expect(fetchesSent.filter(({ method }) => method === 'PUT')).toEqual([{ method: 'PUT', keepalive: true }]);
  expect(store.saves).toHaveLength(1);
  await setHidden(page, false);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops');
});

test('a draft reopened in a new page load, its first save lost, saves as a new page’s first, with no banner', async ({
  page,
}) => {
  const store = twoGoals();
  await openJanesNote(page, store);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  // Opened again in a new page load, whose saves have a clientId of their own, while the draft holds the earlier
  // page's; the new page's first save is lost before it reaches the server, as with no signal, so that its retry, 1 s
  // later, is its save 2, which the server answers 409 draft.taken_over (design.md §5.6).
  await page.reload();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await page.route(
    ({ pathname }) => pathname.endsWith('/draft'),
    (route) => (route.request().method() === 'PUT' ? route.abort('internetdisconnected') : route.fallback()),
    { times: 1 },
  );
  const before = store.saves.length;
  await box(page).press('ControlOrMeta+End');
  await box(page).pressSequentially(' Bought bread.');

  // None of the page's saves of the note had been answered, so it sends the save again at once as a new page's
  // first, under a new clientId as save 1, which takes the note over: "Saved", and no banner (T10's Notes).
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm', { timeout: 5_000 });
  await expect(takeover(page)).toHaveCount(0);
  const sent = store.saves.slice(before);
  expect(sent.map(({ seq, narrative }) => [seq, narrative])).toEqual([
    [2, 'Went to the shops. Bought bread.'],
    [1, 'Went to the shops. Bought bread.'],
  ]);
  expect(sent[1].clientId).not.toBe(sent[0].clientId);
  expect(store.draft(jane.id, worker.today)).toMatchObject({ narrative: 'Went to the shops. Bought bread.' });
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops');
});

/**
 * Makes the open note's saves fail, as a lost signal does, and types `typed`, then expects "Saving…" through the first
 * failure and the failed state once its retry, 1 s later, has failed too.
 */
async function expectNotSavedAfterTyping(page: Page, typed: string) {
  await failDraftSaves(page);
  await box(page).pressSequentially(typed);
  await expect(saveStatus(page)).toHaveText('Saving…');
  await expect(saveStatus(page)).toHaveText(notSavedRetrying, { timeout: 5_000 });
}

/**
 * Lets `store` answer the open note's saves again and waits for the next retry's "Saved 4:12 pm", then saves the draft
 * from another tab, with another text, Makes own breakfast unticked, the bus ticked and the flag with a reason, and
 * types `typed`, whose save meets the takeover banner.
 */
async function expectTakeoverAfterTyping(page: Page, store: NotesStore, typed: string) {
  await store.serve(page, worker);
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm', { timeout: 15_000 });
  store.saveElsewhere(jane.id, worker.today, {
    narrative: 'Typed in another tab.',
    tickedGoalIds: [bus.id],
    isFlagged: true,
    flagReason,
    savedAtUtc: '2026-10-01T06:20:00.000Z',
  });
  await box(page).pressSequentially(typed);
  await expect(takeover(page)).toBeVisible();
  await expect(saveStatus(page)).toHaveText('Not saved');
}

test('Not saved and the takeover banner pass axe; the choices 44 px, stacked on a phone, side by side after', async ({
  page,
}) => {
  test.slow();
  const store = twoGoals();
  await openJanesNote(page, store);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  // Not saved: the indicator as the top bar's second row, under the back link and the bar's full width, its warning
  // icon hidden from screen readers, and Submit note unavailable.
  await expectNotSavedAfterTyping(page, ' Bought bread.');
  await expect(saveStatus(page).locator('svg')).toHaveAttribute('aria-hidden', 'true');
  await expect(submitNote(page)).toHaveAttribute('aria-disabled', 'true');
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    const link = await boxOf(backLink(page));
    const status = await boxOf(saveStatus(page));
    // The bar's row, in the form's column, which holds the back link and the indicator.
    const barRow = await boxOf(saveStatus(page).locator('xpath=..'));
    expect(Math.round(status.x), `the row's start at ${width} px`).toBe(Math.round(link.x));
    expect(Math.round(status.x), `the bar's start at ${width} px`).toBe(Math.round(barRow.x));
    expect(Math.round(status.width), `the bar's width at ${width} px`).toBe(Math.round(barRow.width));
    expect(status.y, `the row under the back link at ${width} px`).toBeGreaterThanOrEqual(link.y + link.height);
  }
  await expectAxeToPass(page);

  // The takeover banner, with "Not saved", its icon hidden, and Keep, then Load.
  await expectTakeoverAfterTyping(page, store, ' Walked home.');
  await expect(takeover(page).locator('svg')).toHaveAttribute('aria-hidden', 'true');
  await expect(takeover(page).getByRole('button')).toHaveText([
    'Keep the text on this screen',
    'Load the other version',
  ]);
  await expectAxeToPass(page);

  // On a phone, each choice the banner's width, Keep above Load; on a laptop, side by side, Keep first; each at least
  // 44 px high and wide.
  await page.setViewportSize({ width: 375, height: 812 });
  await expectAtLeast(takeover(page).getByRole('button'), 44, 44);
  const [keepOnPhone, loadOnPhone] = [await boxOf(keepButton(page)), await boxOf(loadButton(page))];
  const words = await boxOf(takeover(page).locator('p'));
  expect(Math.round(loadOnPhone.x)).toBe(Math.round(keepOnPhone.x));
  expect(Math.round(loadOnPhone.width)).toBe(Math.round(keepOnPhone.width));
  expect(Math.round(keepOnPhone.x + keepOnPhone.width)).toBe(Math.round(words.x + words.width));
  expect(loadOnPhone.y).toBeGreaterThanOrEqual(keepOnPhone.y + keepOnPhone.height);
  await page.setViewportSize({ width: 1280, height: 800 });
  await expectAtLeast(takeover(page).getByRole('button'), 44, 44);
  const [keepOnLaptop, loadOnLaptop] = [await boxOf(keepButton(page)), await boxOf(loadButton(page))];
  expect(Math.round(loadOnLaptop.y)).toBe(Math.round(keepOnLaptop.y));
  expect(loadOnLaptop.x).toBeGreaterThanOrEqual(keepOnLaptop.x + keepOnLaptop.width);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', notSavedRetrying, changedElsewhere);
});

test('at 320 px with text at 200%, neither Not saved nor the takeover banner scrolls sideways', async ({ page }) => {
  const store = twoGoals();
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(jane.id, worker.today));
  await setTextTo200Percent(page);
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  await expectNotSavedAfterTyping(page, ' Bought bread.');
  expect(await scrollsSideways(page), 'Not saved').toBe(false);

  await expectTakeoverAfterTyping(page, store, ' Walked home.');
  expect(await scrollsSideways(page), 'the takeover banner').toBe(false);
  for (const choice of [keepButton(page), loadButton(page)]) {
    expect(await choice.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    const { x, width } = await boxOf(choice);
    expect(x + width).toBeLessThanOrEqual(320);
  }
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', notSavedRetrying, changedElsewhere);
});

test('in forced colours, the failed row and the takeover banner keep their 2 px edges, in CanvasText', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const store = twoGoals();
  await openJanesNote(page, store);
  const canvasText = await systemColour(page, 'CanvasText');
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  const expectEdge = async (row: Locator) => {
    for (const side of ['top', 'right', 'bottom', 'left']) {
      await expect(row).toHaveCSS(`border-${side}-color`, canvasText);
      await expect(row).toHaveCSS(`border-${side}-style`, 'solid');
      await expect(row).toHaveCSS(`border-${side}-width`, '2px');
    }
  };

  await expectNotSavedAfterTyping(page, ' Bought bread.');
  await expectEdge(saveStatus(page));
  await expectTakeoverAfterTyping(page, store, ' Walked home.');
  await expectEdge(takeover(page));
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', notSavedRetrying, changedElsewhere);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the takeover banner's Keep and Load after the back link, each by a key`, async ({
    page,
  }) => {
    // Tall enough that the box, below Common items' sentence and under the banner, stays on screen.
    await page.setViewportSize({ width, height: 1000 });
    const store = twoGoals();
    await serveMe(page, worker);
    await serveToday(page, worker.today, participants);
    await store.serve(page, worker);
    await page.goto('/');
    await expect(rows(page)).toHaveCount(2);
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Past notes', breakfast.text, bus.text, '3. Guided notes']);
    await page.keyboard.type('Went to the shops.');
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

    // Another tab saves the note, and the next words meet the banner, with focus kept in the box.
    const elsewhere = {
      narrative: 'Typed in another tab.',
      tickedGoalIds: [bus.id],
      isFlagged: true,
      flagReason,
      savedAtUtc: '2026-10-01T06:20:00.000Z',
    };
    store.saveElsewhere(jane.id, worker.today, elsewhere);
    await page.keyboard.type(' Bought bread.');
    await expect(takeover(page)).toBeVisible();
    await expectFocusAt(page, '3. Guided notes');

    // note-form.md *Focus order (Tab)*: the back link, then Keep and Load, then <main>, from Past notes.
    await expectStops(page, 'Shift+Tab', [
      bus.text,
      breakfast.text,
      'Past notes',
      'Load the other version',
      'Keep the text on this screen',
      'Today',
      'Account',
    ]);
    await expectStops(page, 'Tab', ['Today', 'Keep the text on this screen']);

    // Enter on Keep: the text on screen saved as a new clientId's save 1, the banner gone, and focus back in the box.
    await page.keyboard.press('Enter');
    await expect(takeover(page)).toHaveCount(0);
    await expectFocusAt(page, '3. Guided notes');
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
    expect(store.saves.at(-1)).toMatchObject({ seq: 1, narrative: 'Went to the shops. Bought bread.' });
    expect(store.draft(jane.id, worker.today)).toMatchObject({ narrative: 'Went to the shops. Bought bread.' });

    // Taken over again: Space on Load shows the other version, with focus on the <h1>, then the form's Tab order, with
    // Reason, as the other version is flagged.
    store.saveElsewhere(jane.id, worker.today, elsewhere);
    await page.keyboard.type(' Walked home.');
    await expect(takeover(page)).toBeVisible();
    await expectStops(page, 'Shift+Tab', [bus.text, breakfast.text, 'Past notes', 'Load the other version']);
    await page.keyboard.press('Space');
    await expect(heading(page)).toBeFocused();
    await expect(takeover(page)).toHaveCount(0);
    await expect(box(page)).toHaveValue('Typed in another tab.');
    await expect(saveStatus(page)).toHaveText('Saved 4:20 pm');
    await expectStops(page, 'Tab', [
      'Past notes',
      breakfast.text,
      bus.text,
      '3. Guided notes',
      'Flag for manager',
      'Reason',
      'Submit note',
      'More actions',
      'outside the page',
    ]);
    await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', 'Typed in another tab', changedElsewhere);
  });
}

// Discard draft and a draft from an earlier day (T12: S02.01.04, S02.02.08, S02.03.03, S02.04.03), against
// note-form.md's components 4, 13 and 15, its item 33, *States* › *Discard dialog* and *Where focus goes*.

// More actions, under Submit note, and Discard draft in its panel (note-form.md component 13); the Discard dialog's
// confirming button (component 15), in the Submit confirmation's place.
const moreActions = (page: Page) => main(page).getByRole('button', { name: 'More actions', exact: true });
const discardDraft = (page: Page) => main(page).getByRole('button', { name: 'Discard draft', exact: true });
const confirmDiscard = (page: Page, name = 'Jane Citizen') =>
  confirmation(page).getByRole('button', { name: `Discard draft for ${name}`, exact: true });
// The confirmation's <dialog>, open or closed: the page takes a closed one away once its close event has run, and a key
// pressed before then would meet the one going.
const dialogInPage = (page: Page) => page.locator('dialog[role="alertdialog"]');
// A manager's read-only draft (participant-notes.md *Someone else's draft, manager*): its flag row and its reason.
const draftFlagRow = (page: Page) => main(page).locator('p').filter({ hasText: /^Flag for manager/ });
const draftReason = (page: Page) => main(page).locator('p').filter({ hasText: /^Reason: / });
// The rows of Your unfinished drafts on Today, each a return point by its participant and date (today.md component 5).
const draftRows = (page: Page) => main(page).locator('a[data-return-key*="/"]');

// The Discard dialog's words (note-form.md *States* › *Discard dialog*), and the warning style's red, tokens.css's
// --colour-error, on its confirming button alone.
const nobodyCanOpenIt = 'Nobody will be able to open this draft again.';
const notDiscardedOffline = 'Not discarded: no connection. Try again.';
const notDiscardedServer = 'Not discarded: something went wrong. Try again.';
const noLongerADraft = 'This draft has already been submitted or discarded.';
const red = 'rgb(164, 0, 15)';

/** A pattern for the next Discard, for `holdNext`. */
const discardRequests = '**/api/participants/*/notes/*/discard';

/** Whether the response answers a Discard, `POST {base}/discard`. */
const isDiscard = (response: Response) =>
  response.request().method() === 'POST' && /^\/api\/participants\/[^/]+\/notes\/[^/]+\/discard$/.test(
    new URL(response.url()).pathname,
  );

/** Opens Today as the worker, its rows' statuses from `store`, then Jane's note from her row, focused on its <h1>. */
async function openJanesNoteFromToday(page: Page, store: NotesStore) {
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, store.todayNotes(worker));
  await store.serve(page, worker);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toBeFocused();
}

test('signed in for real at 375 px, a worker discards and starts again; a manager reads it, then discards', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
}) => {
  test.slow();
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);

  // The manager invites a worker, who sets up on a phone of their own from the link that admin reset-signin gives.
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(heading(page)).toHaveText('Users');
  const riley = { displayName: 'Riley Sample', email: `riley.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, riley, 'Worker');
  const phone = secondDevice.page;
  await phone.setViewportSize({ width: 375, height: 812 });
  await phone.goto(await resetSignIn(riley.email));
  await main(phone).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(rows(phone)).toHaveText([janeCitizen.name]);

  // The worker starts Jane's note: More actions only once the app has answered its first save.
  await openJaneCitizensNote(phone);
  const address = new URL(phone.url()).pathname;
  await expect(moreActions(phone)).toHaveCount(0);
  const started = phone.waitForResponse(isDraftSave);
  await box(phone).pressSequentially(sentence);
  expect((await started).status()).toBe(201);
  await expect(saveStatus(phone)).toHaveText(savedAnyTime);

  // More actions, then Discard draft: the dialog names Jane, on its focused title; Go back gives focus to More actions.
  await moreActions(phone).click();
  await expect(moreActions(phone)).toHaveAttribute('aria-expanded', 'true');
  await discardDraft(phone).click();
  await expect(confirmationTitle(phone)).toBeFocused();
  await expect(confirmation(phone)).toHaveAccessibleName(`Discard the draft note for ${janeCitizen.name}?`);
  await expect(confirmation(phone)).toHaveAccessibleDescription(nobodyCanOpenIt);
  await goBack(phone).click();
  await expect(dialogInPage(phone)).toHaveCount(0);
  await expect(moreActions(phone)).toBeFocused();
  await expect(moreActions(phone)).toHaveAttribute('aria-expanded', 'false');

  // Discarded, by the app's 204: Today in the form's place, its <h1> focused, no banner, and Jane's row with no status.
  await moreActions(phone).click();
  await discardDraft(phone).click();
  const discarded = phone.waitForResponse(isDiscard);
  await confirmDiscard(phone, janeCitizen.name).click();
  expect((await discarded).status()).toBe(204);
  await expect(phone).toHaveURL('/');
  await expect(heading(phone)).toBeFocused();
  await expect(heading(phone)).toContainText('Today');
  await expect(banner(phone)).toHaveCount(0);
  await expect(rows(phone)).toHaveText([janeCitizen.name]);
  await expect(rowLine(phone, janeCitizen.name)).toHaveCount(0);

  // Her Past notes lists nothing; and the same day's note starts afresh, its first save creating it (design.md §14 M2).
  await openJaneCitizensNote(phone);
  await expect(box(phone)).toHaveValue('');
  await expect(moreActions(phone)).toHaveCount(0);
  await pastNotesLink(phone).click();
  await expect(heading(phone)).toHaveText(`Past notes ${janeCitizen.name}`);
  await expect(main(phone).getByText(`No notes yet for ${janeCitizen.name}.`, { exact: true })).toBeVisible();
  await phone.locator('#page-bar').getByRole('link', { name: 'Note', exact: true }).click();
  await expect(phone).toHaveURL(address);
  const restarted = phone.waitForResponse(isDraftSave);
  await goalRow(phone, breakfast.text).locator('span').click();
  expect((await restarted).status()).toBe(201);
  // The save that holds everything typed next, sent as focus leaves Reason.
  const complete = phone.waitForResponse((response) => {
    const body = isDraftSave(response) ? (response.request().postDataJSON() as DraftSave) : undefined;
    return body?.narrative === sentence && body.flagReason === flagReason;
  });
  await box(phone).pressSequentially(sentence);
  await flagRow(phone).locator('span').click();
  await reasonBox(phone).fill(flagReason);
  await reasonBox(phone).blur();
  expect((await complete).status()).toBe(200);
  await expect(saveStatus(phone)).toHaveText(savedAnyTime);
  await expectNothingKept(phone, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason);

  // The manager's Today shows Riley's draft, whose note reads to a manager as its header and its working copy: the
  // sentence, Discard draft and Past notes, the ticks in order, the text and the flag row with its reason, never the
  // Flagged tag.
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await expect.poll(() => shownText(rowLine(page, janeCitizen.name))).toMatch(
    new RegExp(`^Draft · Riley Sample · started ${anyTime}$`),
  );
  const read = page.waitForResponse((response) => new URL(response.url()).pathname === `/api${address}`);
  await row(page, janeCitizen.name).click();
  expect(await (await read).json()).toEqual({
    status: 'Draft',
    authorDisplayName: riley.displayName,
    startedAtUtc: expect.any(String),
    current: {
      narrative: sentence,
      isFlagged: true,
      flagReason,
      goals: [
        { text: breakfast.text, isTicked: true },
        { text: bus.text, isTicked: false },
      ],
      commonItemGroups: [],
    },
  });
  await expect(heading(page)).toBeFocused();
  await expect(
    main(page).getByText(
      new RegExp(
        `^Riley Sample started today’s note for ${janeCitizen.name} at ${anyTime}\\. `
          + 'Only Riley Sample can finish it\\.$',
      ),
    ),
  ).toBeVisible();
  await expect(main(page).getByRole('button')).toHaveText(['Discard draft']);
  await expect(pastNotesLink(page)).toBeVisible();
  await expect(readGoals(page)).toHaveText([`${breakfast.text}, ticked`, `${bus.text}, not ticked`]);
  await expect(readText(page)).toHaveText(sentence);
  await expect(draftFlagRow(page)).toHaveText('Flag for manager, ticked');
  await expect.poll(() => shownText(draftReason(page))).toBe(`Reason: ${flagReason}`);
  await expect(main(page).getByText('Flagged', { exact: true })).toHaveCount(0);
  await expect(main(page).getByRole('checkbox')).toHaveCount(0);
  await expect(main(page).getByRole('textbox')).toHaveCount(0);

  // Discard draft says who started it and when; the app's 204 lands the manager on Today, Jane's row with no status.
  await discardDraft(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  await expect(confirmation(page)).toHaveAccessibleDescription(
    new RegExp(`^Riley Sample started it at ${anyTime}\\. ?Nobody will be able to open this draft again\\.$`),
  );
  const managersDiscard = page.waitForResponse(isDiscard);
  await confirmDiscard(page, janeCitizen.name).click();
  expect((await managersDiscard).status()).toBe(204);
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toBeFocused();
  await expect(rows(page)).toHaveText([janeCitizen.name]);
  await expect(rowLine(page, janeCitizen.name)).toHaveCount(0);
  await expectNothingKept(page, janeCitizen.givenName, janeCitizen.familyName, sentence, flagReason);

  // The worker's Today shows the row with no status either.
  await nav(phone).getByRole('link', { name: 'Today', exact: true }).click();
  await expect(rows(phone)).toHaveText([janeCitizen.name]);
  await expect(rowLine(phone, janeCitizen.name)).toHaveCount(0);
});

test('More actions after the first save, its panel, and each state of the Discard dialog, passing axe', async ({
  page,
}) => {
  test.slow();
  const store = twoGoals();
  await openJanesNote(page, store);

  // A new note has no More actions; once its first save is answered, it shows, closed, controlling its hidden panel.
  await expect(moreActions(page)).toHaveCount(0);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');
  const panel = page.locator(`[id="${(await moreActions(page).getAttribute('aria-controls')) ?? ''}"]`);
  await expect(panel).toBeHidden();
  const submitted = await boxOf(submitNote(page));
  expect(Math.round((await boxOf(moreActions(page))).y - submitted.y - submitted.height), '1.5rem under Submit note')
    .toBe(24);

  // Open: the panel holds Discard draft, secondary, not red; Escape closes it, focus on More actions.
  await moreActions(page).click();
  await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'true');
  await expect(panel).toBeVisible();
  await expect(panel.getByRole('button')).toHaveText(['Discard draft']);
  await expect(discardDraft(page)).not.toHaveCSS('background-color', red);
  await expectAxeToPass(page);
  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await expect(moreActions(page)).toBeFocused();
  await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');

  // The dialog: its title focused, with no ring, naming Jane; its one line; the warning-styled confirming button, then
  // Go back; its alert and status lines there and empty.
  await moreActions(page).click();
  await discardDraft(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  await expect(confirmationTitle(page)).toHaveCSS('outline-style', 'none');
  await expect(confirmation(page)).toHaveAccessibleName('Discard the draft note for Jane Citizen?');
  await expect(confirmation(page)).toHaveAccessibleDescription(nobodyCanOpenIt);
  await expect(confirmation(page).getByRole('button')).toHaveText(['Discard draft for Jane Citizen', 'Go back']);
  await expect(confirmDiscard(page)).toHaveCSS('background-color', red);
  await expect(confirmationAlert(page)).toBeEmpty();
  await expect(confirmationStatus(page)).toBeEmpty();
  await expectAxeToPass(page);

  // Go back: closed with nothing sent, the panel closed, and focus on More actions.
  await goBack(page).click();
  await expect(dialogInPage(page)).toHaveCount(0);
  await expect(moreActions(page)).toBeFocused();
  await expect(panel).toBeHidden();
  expect(store.discarded(jane.id, worker.today)).toEqual([]);

  // Busy: both buttons aria-disabled, their labels kept, and "Discarding…" after 1 s.
  await moreActions(page).click();
  await discardDraft(page).click();
  const held = await holdNext(page, discardRequests);
  await confirmDiscard(page).click();
  const post = await held();
  await expect(confirmationStatus(page)).toHaveText('Discarding…');
  await expect(confirmDiscard(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(goBack(page)).toHaveAttribute('aria-disabled', 'true');
  await expectAxeToPass(page);

  // Failed, with no connection and then from the server: the dialog kept open, with focus on the confirming button.
  await post.abort('internetdisconnected');
  await expect(confirmationAlert(page)).toHaveText(notDiscardedOffline);
  await expect(confirmDiscard(page)).toBeFocused();
  await expect(confirmationStatus(page)).toBeEmpty();
  await expectAxeToPass(page);
  const failed = await holdNext(page, discardRequests);
  await confirmDiscard(page).click();
  await (await failed()).fulfill({ status: 503 });
  await expect(confirmationAlert(page)).toHaveText(notDiscardedServer);
  await expect(confirmDiscard(page)).toBeFocused();
  await expectAxeToPass(page);

  // Submitted in another tab meanwhile, so a draft no longer: the sentence, the confirming button gone, and focus on
  // Go back, which goes to Today in the form's place, its <h1> focused.
  store.submitElsewhere(jane.id, worker.today);
  await confirmDiscard(page).click();
  await expect(confirmationAlert(page)).toHaveText(noLongerADraft);
  await expect(confirmDiscard(page)).toHaveCount(0);
  await expect(goBack(page)).toBeFocused();
  await expectAxeToPass(page);
  await goBack(page).click();
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toBeFocused();
  await expect(banner(page)).toHaveCount(0);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', noLongerADraft, notDiscardedServer);
});

test('Discard waits for the save out, saves nothing after, and lands on Today’s <h1>, the row with no status', async ({
  page,
}) => {
  const store = twoGoals();
  await openJanesNoteFromToday(page, store);
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

  // A change, whose save is held as focus leaves the box for More actions.
  const heldSave = await holdNext(page, draftRequests);
  await box(page).pressSequentially(' Bought bread.');
  await moreActions(page).click();
  const save = await heldSave();
  await discardDraft(page).click();
  const discards: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/discard')) {
      discards.push(request.url());
    }
  });

  // Confirming sends nothing while the save is out: the dialog busy, "Discarding…" after 1 s.
  await confirmDiscard(page).click();
  await expect(confirmationStatus(page)).toHaveText('Discarding…');
  expect(discards).toEqual([]);

  // Its answer, then the Discard's 204: Today in the form's place, its <h1> focused, no banner, Jane's row with no
  // status, and the draft discarded with every word saved.
  const discarded = page.waitForResponse(isDiscard);
  await save.fallback();
  expect((await discarded).status()).toBe(204);
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toBeFocused();
  await expect(banner(page)).toHaveCount(0);
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(rowLine(page, 'Jane Citizen')).toHaveCount(0);
  expect(discards).toHaveLength(1);
  expect(store.discarded(jane.id, worker.today)).toEqual([
    expect.objectContaining({ narrative: 'Went to the shops. Bought bread.', discardedByUserId: worker.userId }),
  ]);

  // Nothing saved after it, past the 2 s at which a save would go.
  const sent = store.saves.length;
  await page.waitForTimeout(2_500);
  expect(store.saves).toHaveLength(sent);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops');
});

test('yesterday’s draft, from Your unfinished drafts: its notice and indicator, “Submit the note for”, Today', async ({
  page,
}) => {
  const store = twoGoals();
  // The worker's own drafts, each started at 9:14 am on its day and saved at 9:42 am: Jane's from yesterday, and Ben
  // Archer's from the day before, Ben being archived since (A35).
  for (const [participantId, noteDate] of [
    [jane.id, '2026-09-30'],
    [ben.id, '2026-09-29'],
  ]) {
    const theDayBefore = daysAfter(noteDate, -1);
    const startedBy = { authorDisplayName: worker.displayName, startedAtUtc: `${theDayBefore}T23:14:00Z` };
    store.startElsewhere(participantId, noteDate, startedBy, worker.userId);
    store.saveElsewhere(participantId, noteDate, {
      narrative: 'Walked to the shop.',
      savedAtUtc: `${theDayBefore}T23:42:00Z`,
    });
  }
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, store.todayNotes(worker));
  await serveMyDrafts(page, store.myDrafts(worker));
  await store.serve(page, worker);
  await page.goto('/');
  await expect(draftRows(page)).toHaveText(['Ben Archer', 'Jane Citizen']);

  // Jane's: the <h1> with its date, then the earlier-day notice, then Past notes; the indicator with the save's day.
  await draftRows(page).filter({ hasText: 'Jane Citizen' }).click();
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  await expect(
    main(page).getByText('This draft is for Wednesday 30 September 2026. Submitting it now keeps that date.', {
      exact: true,
    }),
  ).toBeVisible();
  expect((await main(page).innerText()).replace(/\s+/g, ' ')).toMatch(
    /^Jane Citizen Wednesday 30 September 2026 This draft is for Wednesday 30 September 2026\. .+ Past notes 1\. /,
  );
  await expect.poll(() => shownText(saveStatus(page))).toBe('Draft · saved Wed 30 Sep 2026, 9:42 am');
  await expect(box(page)).toHaveValue('Walked to the shop.');
  await expectAxeToPass(page);

  // The Discard dialog says its date first.
  await moreActions(page).click();
  await discardDraft(page).click();
  await expect(confirmation(page)).toHaveAccessibleDescription(
    /^This draft is for Wednesday 30\sSeptember 2026\. ?Nobody will be able to open this draft again\.$/,
  );
  await expectAxeToPass(page);
  await goBack(page).click();
  await expect(dialogInPage(page)).toHaveCount(0);

  // Submit note: "Submit the note for", with the note's own date, which its version keeps; then Today, its banner
  // focused, and the draft no longer under Your unfinished drafts.
  await submitNote(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  await expect(confirmation(page)).toHaveAccessibleName('Submit the note for Jane Citizen');
  await expect(confirmation(page).locator('time')).toHaveAttribute('datetime', '2026-09-30');
  await expectAxeToPass(page);
  await confirmButton(page).click();
  await expect(banner(page)).toBeFocused();
  await expect(banner(page)).toHaveText('Note for Jane Citizen submitted');
  expect(store.submitted(jane.id, '2026-09-30')).toMatchObject({ narrative: 'Walked to the shop.' });
  await expect(draftRows(page)).toHaveText(['Ben Archer']);

  // Ben's, archived since, with its own notice, submitted the same way: Today then has no section at all.
  await draftRows(page).click();
  await expect(heading(page)).toHaveText('Ben Archer Tuesday 29 September 2026');
  await expect(
    main(page).getByText('This draft is for Tuesday 29 September 2026. Submitting it now keeps that date.', {
      exact: true,
    }),
  ).toBeVisible();
  await submitNote(page).click();
  await expect(confirmation(page)).toHaveAccessibleName('Submit the note for Ben Archer');
  await confirmButton(page, 'Ben Archer').click();
  await expect(banner(page)).toHaveText('Note for Ben Archer submitted');
  expect(store.submitted(ben.id, '2026-09-29')).toMatchObject({ narrative: 'Walked to the shop.' });
  await expect(main(page).getByRole('heading', { name: 'Your unfinished drafts' })).toHaveCount(0);
  await expect(draftRows(page)).toHaveCount(0);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Ben', 'Archer', 'Walked to the shop', 'submitted');
});

test('at 320 px with text at 200%, neither More actions’ panel nor the Discard dialog scrolls sideways', async ({
  page,
}) => {
  const long = madeUpParticipant('8f4b0d3a-7c16-4e59-b3a8-1d6f2b9e5c74', 'A'.repeat(50), 'B'.repeat(50));
  const longName = `${'A'.repeat(50)} ${'B'.repeat(50)}`;
  const store = notesStore({ today: worker.today, participants: [long], prompts: savedPrompts });
  await serveMe(page, worker);
  await store.serve(page, worker);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(noteAddress(long.id, worker.today));
  await setTextTo200Percent(page);
  await box(page).pressSequentially('Went for a walk.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await moreActions(page).click();
  await expect(discardDraft(page)).toBeVisible();
  expect(await scrollsSideways(page), 'More actions’ panel').toBe(false);

  // The dialog's title and its confirming button each hold the name whole, inside the dialog, its failure too.
  await discardDraft(page).click();
  await expect(confirmationTitle(page)).toHaveText(`Discard the draft note for ${longName}?`);
  expect(await scrollsSideways(page), 'the Discard dialog').toBe(false);
  for (const part of [confirmation(page), confirmationTitle(page), confirmDiscard(page, longName)]) {
    expect(await part.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
    const { x, width } = await boxOf(part);
    expect(x + width).toBeLessThanOrEqual(320);
  }
  await page.route(discardRequests, (route) => route.abort('internetdisconnected'));
  await confirmDiscard(page, longName).click();
  await expect(confirmationAlert(page)).toHaveText(notDiscardedOffline);
  expect(await scrollsSideways(page), 'the Discard dialog’s failure').toBe(false);
});

test('targets: More actions, Discard draft and the Discard dialog’s buttons 48 px, on phone and laptop', async ({
  page,
}) => {
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await openJanesNote(page, twoGoals());
    await box(page).pressSequentially('Went for a walk.');
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
    await expectAtLeast(moreActions(page), 44, 48);
    await moreActions(page).click();
    await expectAtLeast(discardDraft(page), 44, 48);
    await discardDraft(page).click();
    await expectAtLeast(confirmation(page).getByRole('button'), 44, 48);
  }
});

test('at 28:00 the warning closes the Discard dialog, or the panel alone, and gives focus to More actions', async ({
  page,
}) => {
  // Before the page loads, so that its timers and Date run on Playwright's clock, which the test runs on to 28:00.
  await page.clock.install();
  const store = twoGoals();
  await openJanesNote(page, store);
  await page.route('**/api/auth/ping', (route) => route.fulfill({ status: 204 }));
  await box(page).pressSequentially('Went to the shops.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  const warning = page.getByRole('dialog', { name: 'You’ll be signed out in 2 minutes' });
  const staySignedIn = warning.getByRole('button', { name: 'Stay signed in' });

  // The Discard dialog open: closed as Go back, the panel with it, nothing sent; after Stay signed in, focus on More
  // actions, as after Go back (note-form.md *Session*).
  await moreActions(page).click();
  await discardDraft(page).click();
  await expect(confirmationTitle(page)).toBeFocused();
  const sent = recordRequests(page);
  await page.clock.runFor('28:00');
  await expect(staySignedIn).toBeFocused();
  await expect(dialogInPage(page)).toHaveCount(0);
  await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');
  await staySignedIn.click();
  await expect(warning).toBeHidden();
  await expect(moreActions(page)).toBeFocused();

  // The panel alone open, focus on Discard draft: closed, focus moved to More actions, where the warning gives it back.
  await page.keyboard.press('Enter');
  await expectStops(page, 'Tab', ['Discard draft']);
  await page.clock.runFor('28:00');
  await expect(staySignedIn).toBeFocused();
  await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');
  await expect(discardDraft(page)).toBeHidden();
  await staySignedIn.click();
  await expect(warning).toBeHidden();
  await expect(moreActions(page)).toBeFocused();
  expect(await sent()).toEqual(['POST /api/auth/ping', 'POST /api/auth/ping']);
  expect(store.discarded(jane.id, worker.today)).toEqual([]);
});

test('in forced colours, the ring is Highlight on More actions, Discard draft and the dialog’s buttons', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await openJanesNote(page, twoGoals());
  const forcedRing = { ...ring, colour: await systemColour(page, 'Highlight') };
  await box(page).pressSequentially('Went for a walk.');
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  await expectStops(page, 'Tab', ['Flag for manager', 'Submit note', 'More actions'], forcedRing);
  await page.keyboard.press('Enter');
  await expectStops(page, 'Tab', ['Discard draft'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(confirmationTitle(page)).toBeFocused();
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
  test(`keyboard only at ${width} px: More actions after Submit note, its panel, the Discard dialog, Today`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = twoGoals();
    await serveMe(page, worker);
    await serveToday(page, worker.today, participants, store.todayNotes(worker));
    await store.serve(page, worker);
    await page.goto('/');
    await expect(rows(page)).toHaveCount(2);
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    await expectStops(page, 'Tab', [...shell, 'Find a participant', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Past notes', breakfast.text, bus.text, '3. Guided notes']);
    await page.keyboard.type('Went to the shops.');
    await expectStops(page, 'Tab', ['Flag for manager', 'Submit note']);
    await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');

    // note-form.md *Focus order (Tab)*: More actions after Submit note, then Discard draft while its panel is open.
    await expectStops(page, 'Tab', ['More actions', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['More actions']);
    await page.keyboard.press('Enter');
    await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'true');
    await expectStops(page, 'Tab', ['Discard draft', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Discard draft', 'More actions', 'Submit note']);
    await expectStops(page, 'Tab', ['More actions', 'Discard draft']);

    // Escape in the panel closes it, focus on More actions; Space opens it again.
    await page.keyboard.press('Escape');
    await expect(moreActions(page)).toBeFocused();
    await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');
    await expectStops(page, 'Tab', ['outside the page']);
    await expectStops(page, 'Shift+Tab', ['More actions']);
    await page.keyboard.press('Space');
    await expectStops(page, 'Tab', ['Discard draft']);

    // Enter on Discard draft: the dialog on its title, then its two buttons. Escape, and Go back by Enter, each close
    // it with nothing sent, focus on More actions and its panel closed.
    await page.keyboard.press('Enter');
    await expect(confirmationTitle(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Discard draft for Jane Citizen', 'Go back']);
    await page.keyboard.press('Escape');
    await expect(dialogInPage(page)).toHaveCount(0);
    await expect(moreActions(page)).toBeFocused();
    await expect(moreActions(page)).toHaveAttribute('aria-expanded', 'false');
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Discard draft']);
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Discard draft for Jane Citizen', 'Go back']);
    await page.keyboard.press('Enter');
    await expect(dialogInPage(page)).toHaveCount(0);
    await expect(moreActions(page)).toBeFocused();
    expect(store.discarded(jane.id, worker.today)).toEqual([]);

    // Then Enter on the confirming button: Today, its <h1> focused, then its rows.
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Discard draft']);
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Discard draft for Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Find a participant', 'Jane Citizen', 'Ravi de Silva', 'outside the page']);
    expect(store.discarded(jane.id, worker.today)).toHaveLength(1);
    await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops');
  });
}
