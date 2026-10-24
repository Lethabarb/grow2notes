import type { Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops, ring, whereFocusIs } from './focus.ts';
import { expectFoundationsToPass } from './foundations.ts';
import {
  breakfast,
  bus,
  goals,
  goalWrites,
  madeUpGoal,
  serveGoals,
  shop,
  sister,
  washing,
  type AdminGoal,
} from './goals.ts';
import {
  boxOf,
  expectAtLeast,
  expectLabelWidth,
  scrollsSideways,
  setTextTo200Percent,
  systemColour,
} from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe, worker } from './me.ts';
import {
  ben,
  failParticipants,
  jane,
  madeUpParticipant,
  participants,
  ravi,
  serveParticipants,
  type AdminParticipant,
} from './participants.ts';
import { holdNext } from './requests.ts';
import { signInThroughSetup } from './setupSignIn.ts';

// The Participants screens and a participant's goals in the published app (S01.02.01 to S01.02.08), against
// participants.md's criteria. The first four tests sign in for real, through setup with a passkey on the virtual
// authenticator, and add, correct, archive and restore a participant, and add, reword, move, archive and restore their
// goals, on the app's own answers; the others answer /api/auth/me, the list, each participant's goals and each change
// in the browser with made-up people and goals (me.ts, participants.ts, goals.ts), so that none of those requests
// reaches the app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// The back link, in the shell's bar between <header> and <main> (participants.md B1).
const backLink = (page: Page) => page.locator('#page-bar').getByRole('link', { name: 'Participants' });

// The list (participants.md component A).
const addParticipant = (page: Page) => main(page).getByRole('link', { name: 'Add participant', exact: true });
const search = (page: Page) => main(page).getByRole('search');
const findField = (page: Page) => main(page).getByRole('textbox', { name: 'Find a participant' });
const clear = (page: Page) => main(page).getByRole('button', { name: 'Clear search' });
const views = (page: Page) => main(page).getByRole('navigation', { name: 'Participants' }).getByRole('link');
const view = (page: Page, name: 'Active' | 'Archived') => views(page).filter({ hasText: name });
// Each row's link, a return point by its key (app-shell.md *The route-change rule (one owner)*), and its list item.
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const rowItems = (page: Page) => main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key]') });
// A sentence in place of the rows, not the search's hidden status region, which says the no-match sentence too.
const sentence = (page: Page, words: string) =>
  main(page).getByText(words, { exact: true }).and(page.locator(':not([role="status"])'));
// The region's line, beside the page's and the search's visually hidden status regions (participants.md *Live
// regions*), on a screen that shows no form, whose save status is a status region too.
const loadLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });

// Add participant and a participant's page (participants.md component B).
type FieldName = 'Given name' | 'Family name' | 'Day' | 'Month' | 'Year';
const field = (page: Page, name: FieldName) => main(page).getByRole('textbox', { name, exact: true });
// The Details form's Save, not an open goal edit's.
const save = (page: Page) => details(page).getByRole('button', { name: 'Save', exact: true });
const details = (page: Page) => main(page).getByRole('region', { name: 'Details' });
const saveStatus = (page: Page) => details(page).getByRole('status');
const saveAlert = (page: Page) => details(page).getByRole('alert');
const archive = (page: Page) => main(page).getByRole('button', { name: 'Archive participant' });
const archiveAlert = (page: Page) => main(page).getByRole('region', { name: 'Actions' }).getByRole('alert');
const actionLinks = (page: Page) => main(page).getByRole('region', { name: 'Actions' }).getByRole('link');
// An active participant's Actions, in Tab order: its links, then Archive participant (participants.md B7).
const actionStops = ['Past notes', 'Write past-day note', 'Export record', 'Archive participant'];
const restore = (page: Page) => main(page).getByRole('button', { name: 'Restore', exact: true });
// The archived notice's words (participants.md B3); the notice holds them, then Restore with its alert above it.
const archivedWords = (name: string) =>
  `${name} is archived, so this page is read-only. They are not on Today and no new notes can be written for them. ` +
  'Their past notes are kept.';
const noticeWords = (page: Page) => main(page).getByText(/ is archived, so this page is read-only\./);
const restoreAlert = (page: Page) => noticeWords(page).locator('xpath=..').getByRole('alert');

// A participant's goals (participants.md B6, list-editor.md *Anatomy*).
const goalsSection = (page: Page) => main(page).getByRole('region', { name: 'Goals' });
// Each active goal's text, in the numbered list's order: in its row, or, read-only, as the row itself.
const goalTexts = (page: Page) => goalsSection(page).locator('ol > li > p, ol > li > div > p');
const archivedSection = (page: Page) => goalsSection(page).locator('details');
const archivedSummary = (page: Page) => archivedSection(page).locator('summary');
const archivedTexts = (page: Page) => archivedSection(page).locator('li > p, li > div > p');
const goalRow = (page: Page, text: string) => goalsSection(page).getByRole('listitem').filter({ hasText: text });
type RowButton = 'Move up' | 'Move down' | 'Edit' | 'Archive' | 'Restore';
/**
 * A goal row's button as its name and its focus stop read: its label, then the goal's text, hidden but for screen
 * readers, with a space before the comma, as the hidden text is a block of its own, which screen readers do not voice
 * (tests/README.md).
 */
const rowName = (label: RowButton, text: string) => `${label} , ${text}`;
const goalButton = (page: Page, label: RowButton, text: string) =>
  goalsSection(page).getByRole('button', { name: rowName(label, text), exact: true });
/** An active goal row's four focus stops, in order (list-editor.md *Anatomy*). */
const rowStops = (text: string) =>
  (['Move up', 'Move down', 'Edit', 'Archive'] as const).map((label) => rowName(label, text));
const newGoal = (page: Page) => goalsSection(page).getByRole('textbox', { name: 'New goal', exact: true });
const addGoal = (page: Page) => goalsSection(page).getByRole('button', { name: 'Add goal', exact: true });
const editGoal = (page: Page) => goalsSection(page).getByRole('textbox', { name: 'Edit goal', exact: true });
const editForm = (page: Page) =>
  goalsSection(page).locator('form').filter({ has: page.getByRole('textbox', { name: 'Edit goal', exact: true }) });
const saveGoal = (page: Page) => editForm(page).getByRole('button', { name: 'Save', exact: true });
const cancelGoal = (page: Page) => editForm(page).getByRole('button', { name: 'Cancel', exact: true });
// A goal field's message above its box, with its hidden "Error: " (form-validation.md).
const goalError = (page: Page) => goalsSection(page).locator('[id$="-error"]');
const conflictMessage = (page: Page) =>
  goalsSection(page).getByText('Someone else changed this list just now. Check it, then try again.', { exact: true });
// The page's one status region, visually hidden, where the goal list says what each change did (participants.md B8).
const pageStatus = (page: Page) => main(page).locator('p.visually-hidden[role="status"]');
// A button busy for 400 ms, which then shows its busy label, and is named by it, in place of the one it rests with.
const busyButton = (page: Page) => main(page).locator('button[data-busy]');
const goalsHelp =
  'Changes apply to notes started from now on. Notes already started or submitted keep the wording they were written '
  + 'with.';
const noGoals = 'No goals yet. Notes will show an empty Goals section.';

/** "Saved 4:12 pm", at whatever time the app saved, the time with its non-breaking space (status-messages.md). */
const savedAtAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

/** Whether the response answers the list's request, the Participants screens' one read. */
const isListRead = (response: Response) =>
  response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/admin/participants';

/** Whether the response answers a change by `method` to the path after `/api/admin/participants`. */
const isChange = (method: 'POST' | 'PUT', path = '') => (response: Response) =>
  response.request().method() === method && new URL(response.url()).pathname === `/api/admin/participants${path}`;

/** Whether the response answers the participant's goals read, the Goals section's one read. */
const isGoalsRead = (participantId: string) => (response: Response) =>
  response.request().method() === 'GET'
  && new URL(response.url()).pathname === `/api/admin/participants/${participantId}/goals`;

/** Whether the response answers a goal write by `method` to the path after `/api/admin` (design.md §6.6). */
const isGoalWrite = (method: 'POST' | 'PUT', path: string) => (response: Response) =>
  response.request().method() === method && new URL(response.url()).pathname === `/api/admin${path}`;

// The time of each change that the browser answers, 4:12 pm in Melbourne on 1 October 2026, before daylight saving.
const answeredAt = 'Thu, 01 Oct 2026 06:12:00 GMT';

/** Expects the save status to say "Saved 4:12 pm", the answer's `Date` in Melbourne, with its non-breaking space. */
async function expectSavedAt412(page: Page) {
  await expect(saveStatus(page)).toHaveText('Saved 4:12 pm');
  expect(await saveStatus(page).textContent()).toBe('Saved 4:12\u00a0pm');
}

/**
 * Answers each change that the page sends from now on by `method` to the path after `/api/admin/participants`, in the
 * browser, as the app would: with the status, the body, if any, and a `Date` from which the save status reads; or, for
 * `'no connection'`, by aborting it. A later call overrides it, as Playwright tries the latest route first.
 */
async function answerChanges(
  page: Page,
  method: 'POST' | 'PUT',
  path: string,
  answer: { status: number; json?: unknown } | 'no connection',
) {
  await page.route(`**/api/admin/participants${path}`, (route) => {
    if (route.request().method() !== method) {
      return route.fallback();
    }

    if (answer === 'no connection') {
      return route.abort('internetdisconnected');
    }

    const { status, json } = answer;
    const headers = { date: answeredAt };
    return route.fulfill(json === undefined ? { status, headers } : { status, headers, json });
  });
}

// Each live region that the Participants screens put into the document with words already in it (participants.md
// *Cross-cutting*).
let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/manage/participants');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher fails a test whose page puts a live region in with its words, which no other test can show,
// as the app puts none in. Marked to fail, it passes only if something fails it, and nothing in it is asserted, so what
// fails it is the check after it ends (as in fixtures.spec.ts). Were the watcher to miss the region, as it would if the
// Participants routes moved from under its address check or the region went in another way, Playwright would fail the
// run.
test.fail('a live region put into a Participants screen with its words fails the test', async ({ page }) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants');
  await heading(page).waitFor();

  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('role', 'status');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

/**
 * Expects nothing of a participant's, nor what was typed to find one, to be kept where it could outlive the screen
 * (participants.md *Access and privacy*, D22): not in the address, which holds IDs alone, the title, which holds the
 * page's name alone, or `history.state`, which holds React Router's own key and index, the first entry's index alone,
 * and, after Add participant's `201`, `inScreen`; and nothing in the origin's storage.
 */
async function expectNothingKept(page: Page, held: readonly string[]) {
  const kept = await page.evaluate(async () => ({
    address: decodeURIComponent(location.href),
    title: document.title,
    historyState: history.state as Record<string, unknown>,
    storage: {
      localStorage: localStorage.length,
      sessionStorage: sessionStorage.length,
      indexedDB: (await indexedDB.databases()).length,
      cacheStorage: (await caches.keys()).length,
    },
  }));
  const named = Object.keys(kept.historyState).filter((name) => kept.historyState[name] !== undefined);
  expect(named.filter((name) => !['usr', 'key', 'idx'].includes(name))).toEqual([]);
  expect([undefined, null, { inScreen: true }]).toContainEqual(kept.historyState.usr);
  expect(kept.storage).toEqual({ localStorage: 0, sessionStorage: 0, indexedDB: 0, cacheStorage: 0 });
  const places = { address: kept.address, title: kept.title, historyState: JSON.stringify(kept.historyState) };
  for (const value of held) {
    for (const [place, text] of Object.entries(places)) {
      expect(text, `${place} holds no "${value}"`).not.toContain(value);
    }
  }
}

/** Fills in the Details, the date of birth as three parts. */
async function fillDetails(page: Page, givenName: string, familyName: string, [day, month, year]: string[]) {
  await field(page, 'Given name').fill(givenName);
  await field(page, 'Family name').fill(familyName);
  await field(page, 'Day').fill(day);
  await field(page, 'Month').fill(month);
  await field(page, 'Year').fill(year);
}

// The participant whom the tests that sign in for real add, with an apostrophe in the family name (participants.md
// *Add participant and Details*).
const siobhan = { givenName: 'Siobhan', familyName: "O'Brien", name: "Siobhan O'Brien" };

test("signed in for real at 375 px, a manager adds O'Brien, corrects the date of birth, archives and restores", async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  const firstRead = page.waitForResponse(isListRead);
  await main(page).getByRole('link', { name: 'Participants', exact: true }).click();

  // The app's own answer, for the organisation that admin bootstrap made, which has no participants yet.
  expect(await (await firstRead).json()).toEqual([]);
  await expect(sentence(page, 'No participants yet.')).toBeVisible();
  await expect(search(page)).toHaveCount(0);
  await expect(views(page)).toHaveText(['Active', 'Archived']);

  await addParticipant(page).click();
  await expect(heading(page)).toHaveText('Add participant');
  await fillDetails(page, siobhan.givenName, siobhan.familyName, ['7', '3', '1987']);
  const added = page.waitForResponse(isChange('POST'));
  await save(page).click();

  // 201 {id}, the names as typed, then the same page as the participant's, with focus kept on Save.
  const addAnswer = await added;
  expect(addAnswer.status()).toBe(201);
  expect(addAnswer.request().postDataJSON()).toEqual({
    givenName: siobhan.givenName,
    familyName: siobhan.familyName,
    dateOfBirth: '1987-03-07',
  });
  const { id } = (await addAnswer.json()) as { id: string };
  await expect(page).toHaveURL(`/manage/participants/${id}`);
  await expect(heading(page)).toHaveText(siobhan.name);
  await expect(page).toHaveTitle(title('Participant'));
  await expect(saveStatus(page)).toHaveText(savedAtAnyTime);
  await expect(save(page)).toBeFocused();

  // Back to the list, which the app now gives with them, active, as typed. No row was opened, so the <h1> takes focus.
  const listed = page.waitForResponse(isListRead);
  await backLink(page).click();
  const [siobhanListed] = (await (await listed).json()) as AdminParticipant[];
  expect(siobhanListed).toEqual({
    id,
    givenName: siobhan.givenName,
    familyName: siobhan.familyName,
    dateOfBirth: '1987-03-07',
    status: 'Active',
    rowVersion: expect.any(String),
  });
  await expect(page).toHaveURL('/manage/participants');
  await expect(heading(page)).toBeFocused();
  await expect(rows(page)).toHaveText([siobhan.name]);
  const row = rows(page).first();

  // Their row opens their page, whose date of birth is corrected with their version as If-Match. The page reads the
  // list again as it opens, which the test lets finish, so that the next read is the one after the change.
  const opened = page.waitForResponse(isListRead);
  await row.click();
  await opened;
  await expect(heading(page)).toHaveText(siobhan.name);
  await expect(heading(page)).toBeFocused();
  await expect(field(page, 'Day')).toHaveValue('7');
  await field(page, 'Day').fill('8');
  const corrected = page.waitForResponse(isChange('PUT', `/${id}`));
  const correctedRead = page.waitForResponse(isListRead);
  await save(page).click();
  const correctAnswer = await corrected;
  expect(correctAnswer.status()).toBe(200);
  expect(await correctAnswer.request().headerValue('if-match')).toBe(`"${siobhanListed.rowVersion}"`);
  expect(await (await correctedRead).json()).toEqual([
    expect.objectContaining({ id, dateOfBirth: '1987-03-08', status: 'Active' }),
  ]);
  await expect(saveStatus(page)).toHaveText(savedAtAnyTime);
  await expect(save(page)).toBeFocused();

  // The back link goes back to the list's own entry, where focus returns to their row.
  await backLink(page).click();
  await expect(row).toBeFocused();

  // Archived from their page, with no dialog: the page turns read-only, its notice focused.
  const reopened = page.waitForResponse(isListRead);
  await row.click();
  await reopened;
  const archived = page.waitForResponse(isChange('POST', `/${id}/archive`));
  const archivedRead = page.waitForResponse(isListRead);
  await archive(page).click();
  expect((await archived).status()).toBe(204);
  expect(await (await archivedRead).json()).toEqual([expect.objectContaining({ id, status: 'Archived' })]);
  await expect.poll(() => whereFocusIs(page)).toContain(archivedWords(siobhan.name));
  await expect(main(page).getByRole('term')).toHaveText(['Given name', 'Family name', 'Date of birth']);
  await expect(main(page).getByRole('definition')).toHaveText([siobhan.givenName, siobhan.familyName, '8 March 1987']);
  await expect(archive(page)).toHaveCount(0);
  await expect(backLink(page)).toHaveAttribute('href', '/manage/participants?view=archived');

  // Restored: the notice goes, the form comes back filled in afresh, and the <h1> takes focus.
  const restored = page.waitForResponse(isChange('POST', `/${id}/restore`));
  const restoredRead = page.waitForResponse(isListRead);
  await restore(page).click();
  expect((await restored).status()).toBe(204);
  expect(await (await restoredRead).json()).toEqual([expect.objectContaining({ id, status: 'Active' })]);
  await expect(heading(page)).toBeFocused();
  await expect(noticeWords(page)).toHaveCount(0);
  await expect(field(page, 'Day')).toHaveValue('8');
  await expect(backLink(page)).toHaveAttribute('href', '/manage/participants');

  // Back once more to their row, in the Active view, where they are again.
  await backLink(page).click();
  await expect(row).toBeFocused();
  await expectNothingKept(page, [siobhan.givenName, siobhan.familyName, '1987-03-0', '8 March']);
});

// Five made-up goals (A35), for design.md §14 M1's "a participant with five goals on a phone".
const fiveGoals = [
  'Makes own breakfast',
  'Catches the bus to day program and phones when she arrives',
  'Phones her sister',
  'Walks to the shop',
  'Uses the washing machine',
];

test('signed in for real at 375 px, a manager adds five goals, then rewords, moves, archives and restores one', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Participants', exact: true }).click();
  await addParticipant(page).click();
  await fillDetails(page, siobhan.givenName, siobhan.familyName, ['7', '3', '1987']);
  const added = page.waitForResponse(isChange('POST'));
  await save(page).click();
  const { id } = (await (await added).json()) as { id: string };
  await expect(heading(page)).toHaveText(siobhan.name);
  await expect(save(page)).toBeFocused();

  // No goals yet, and none fetched, so the next Tab from Save is New goal (participants.md *Interactions*).
  await expect(goalsSection(page).getByText(noGoals)).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(newGoal(page)).toBeFocused();

  // Five goals, one after another, each typed into the field that the Add before it left focus in (design.md §14 M1):
  // each the app's 201 with no body, then the list read again with it at the end, the field emptied and the add said.
  let listed: AdminGoal[] = [];
  for (const [index, text] of fiveGoals.entries()) {
    await page.keyboard.type(text);
    const posted = page.waitForResponse(isGoalWrite('POST', `/participants/${id}/goals`));
    const reread = page.waitForResponse(isGoalsRead(id));
    await addGoal(page).click();
    const post = await posted;
    expect(post.status()).toBe(201);
    expect(post.request().postDataJSON()).toEqual({ text });
    expect(await post.text()).toBe('');
    listed = (await (await reread).json()) as AdminGoal[];
    expect(listed.map((goal) => [goal.text, goal.sortOrder, goal.archivedAtUtc])).toEqual(
      fiveGoals.slice(0, index + 1).map((each, sortOrder) => [each, sortOrder, null]),
    );
    await expect(goalTexts(page)).toHaveText(fiveGoals.slice(0, index + 1));
    await expect(newGoal(page)).toBeFocused();
    await expect(newGoal(page)).toHaveValue('');
    await expect(pageStatus(page)).toHaveText('Goal added to the end of the list');
  }
  const ids = listed.map((goal) => goal.id);

  // The second reworded in its row: Edit focuses the box with the caret after the words; Save sends the row's version
  // as If-Match and gets the app's 200 with the goal's new version as its ETag; focus goes back to Edit.
  const reworded = 'Catches the bus to day program and texts when she arrives';
  await goalButton(page, 'Edit', fiveGoals[1]).click();
  await expect(editGoal(page)).toBeFocused();
  await expect(editGoal(page)).toHaveValue(fiveGoals[1]);
  expect(await editGoal(page).evaluate((box: HTMLTextAreaElement) => [box.selectionStart, box.selectionEnd])).toEqual(
    [fiveGoals[1].length, fiveGoals[1].length],
  );
  await editGoal(page).fill(reworded);
  const put = page.waitForResponse(isGoalWrite('PUT', `/goals/${ids[1]}`));
  let reread = page.waitForResponse(isGoalsRead(id));
  await saveGoal(page).click();
  const putAnswer = await put;
  expect(putAnswer.status()).toBe(200);
  expect(putAnswer.request().postDataJSON()).toEqual({ text: reworded });
  expect(await putAnswer.request().headerValue('if-match')).toBe(`"${listed[1].rowVersion}"`);
  listed = (await (await reread).json()) as AdminGoal[];
  expect(listed[1]).toMatchObject({ id: ids[1], text: reworded, sortOrder: 1 });
  expect(await putAnswer.headerValue('etag')).toBe(`"${listed[1].rowVersion}"`);
  const afterReword = [fiveGoals[0], reworded, ...fiveGoals.slice(2)];
  await expect(goalTexts(page)).toHaveText(afterReword);
  await expect(goalButton(page, 'Edit', reworded)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Goal saved');

  // The first moved down: the whole order, built from the list, sent; the app's 204; the list read again in the new
  // order, and focus back on the goal's Move down.
  const reordered = page.waitForResponse(isGoalWrite('PUT', `/participants/${id}/goals/order`));
  reread = page.waitForResponse(isGoalsRead(id));
  await goalButton(page, 'Move down', fiveGoals[0]).click();
  const order = await reordered;
  expect(order.status()).toBe(204);
  expect(order.request().postDataJSON()).toEqual({ goalIds: [ids[1], ids[0], ...ids.slice(2)] });
  listed = (await (await reread).json()) as AdminGoal[];
  expect(listed.map((goal) => [goal.id, goal.sortOrder])).toEqual(
    [ids[1], ids[0], ...ids.slice(2)].map((each, sortOrder) => [each, sortOrder]),
  );
  const afterMove = [reworded, fiveGoals[0], ...fiveGoals.slice(2)];
  await expect(goalTexts(page)).toHaveText(afterMove);
  await expect(goalButton(page, 'Move down', fiveGoals[0])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Moved down to number 2 of 5');

  // The third archived: its version as If-Match, the app's 204, then Archived goals open with it, its Restore focused.
  const archived = page.waitForResponse(isGoalWrite('POST', `/goals/${ids[2]}/archive`));
  reread = page.waitForResponse(isGoalsRead(id));
  await goalButton(page, 'Archive', fiveGoals[2]).click();
  const archiveAnswer = await archived;
  expect(archiveAnswer.status()).toBe(204);
  expect(await archiveAnswer.request().headerValue('if-match')).toBe(`"${listed[2].rowVersion}"`);
  listed = (await (await reread).json()) as AdminGoal[];
  expect(listed.find((goal) => goal.id === ids[2])).toMatchObject({ sortOrder: 2, archivedAtUtc: expect.any(String) });
  await expect(goalTexts(page)).toHaveText([reworded, fiveGoals[0], fiveGoals[3], fiveGoals[4]]);
  await expect(archivedSection(page)).toHaveAttribute('open', '');
  await expect(archivedTexts(page)).toHaveText([fiveGoals[2]]);
  await expect(goalButton(page, 'Restore', fiveGoals[2])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Goal archived');

  // Restored: the app's 204, the goal at the end of the list, focus on its Archive, and Archived goals gone.
  const restored = page.waitForResponse(isGoalWrite('POST', `/goals/${ids[2]}/restore`));
  reread = page.waitForResponse(isGoalsRead(id));
  await goalButton(page, 'Restore', fiveGoals[2]).click();
  const restoreAnswer = await restored;
  expect(restoreAnswer.status()).toBe(204);
  expect(await restoreAnswer.request().headerValue('if-match')).toBe(
    `"${listed.find((goal) => goal.id === ids[2])?.rowVersion}"`,
  );
  listed = (await (await reread).json()) as AdminGoal[];
  expect(listed.find((goal) => goal.id === ids[2])).toMatchObject({ sortOrder: 5, archivedAtUtc: null });
  await expect(goalTexts(page)).toHaveText([reworded, fiveGoals[0], fiveGoals[3], fiveGoals[4], fiveGoals[2]]);
  await expect(archivedSection(page)).toHaveCount(0);
  await expect(goalButton(page, 'Archive', fiveGoals[2])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Goal restored to the end of the list');
  await expectNothingKept(page, [...fiveGoals, reworded, siobhan.givenName, siobhan.familyName]);
});

// Two made-up goals (A35) for the keyboard-only check, one of which is archived.
const [kept, archivedLater] = ['Makes own breakfast', 'Phones her sister'];

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: a participant and their goals, with the focus ring at each stop`, async ({
    page,
    invite,
    authenticator,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    // Setup, which setup.spec.ts proves, is pressed with the mouse; from Today on, every move is a key.
    await signInThroughSetup(page, invite, authenticator);
    await expect(heading(page)).toBeFocused();

    // To Manage, the nav's last item, then Participants, the first of its links.
    await expectStops(page, 'Shift+Tab', ['Manage']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toHaveText('Manage');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Participants']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toHaveText('Participants');
    await expect(heading(page)).toBeFocused();
    await expect(sentence(page, 'No participants yet.')).toBeVisible();

    // The list (participants.md *Keyboard*): the skip link and the nav, then Add participant and the two views, with
    // nobody to find yet.
    const nav = ['Grow2Notes', 'Account', 'Today', 'Flagged', 'Report', 'Manage'];
    await expectStops(page, 'Shift+Tab', [...nav].reverse().concat('Skip to main content'));
    await expectStops(page, 'Tab', [...nav, 'Add participant', 'Active', 'Archived', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Archived', 'Active', 'Add participant']);
    await page.keyboard.press('Enter');

    // Add participant: the back link, the fields and Save.
    await expect(heading(page)).toHaveText('Add participant');
    await expect(heading(page)).toBeFocused();
    const form = ['Given name', 'Family name', 'Day', 'Month', 'Year', 'Save'];
    // Goals' add form alone, while the participant has no goal yet.
    const addForm = ['New goal', 'Add goal'];
    await expectStops(page, 'Shift+Tab', ['Participants', 'Manage']);
    await expectStops(page, 'Tab', ['Participants', ...form, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [...form].reverse());
    for (const [index, typed] of [siobhan.givenName, siobhan.familyName, '7', '3', '1987'].entries()) {
      await page.keyboard.type(typed);
      await expectStops(page, 'Tab', [form[index + 1]]);
    }
    const added = page.waitForResponse(isChange('POST'));
    await page.keyboard.press('Enter');
    const addAnswer = await added;
    expect(addAnswer.status()).toBe(201);
    const { id } = (await addAnswer.json()) as { id: string };
    await expect(heading(page)).toHaveText(siobhan.name);
    await expect(saveStatus(page)).toHaveText(savedAtAnyTime);
    await expect(save(page)).toBeFocused();
    // The next Tab reaches Goals (participants.md *Interactions*), then Actions' links and Archive participant.
    await expectStops(page, 'Tab', [...addForm, ...actionStops, 'outside the page']);

    // The date of birth corrected.
    await expectStops(page, 'Shift+Tab', [
      'Archive participant',
      'Export record',
      'Write past-day note',
      'Past notes',
      ...[...addForm].reverse(),
      'Save',
      'Year',
      'Month',
      'Day',
    ]);
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type('8');
    await expectStops(page, 'Tab', ['Month', 'Year', 'Save']);
    const corrected = page.waitForResponse(isChange('PUT', `/${id}`));
    await page.keyboard.press('Enter');
    expect((await corrected).status()).toBe(200);
    await expect(saveStatus(page)).toHaveText(savedAtAnyTime);
    await expect(save(page)).toBeFocused();

    // Two goals, each typed into New goal and added with Enter on Add goal, which leaves focus in New goal.
    await expectStops(page, 'Tab', ['New goal']);
    let listed: AdminGoal[] = [];
    for (const text of [kept, archivedLater]) {
      await page.keyboard.type(text);
      await expectStops(page, 'Tab', ['Add goal']);
      const addedGoal = page.waitForResponse(isGoalWrite('POST', `/participants/${id}/goals`));
      const reread = page.waitForResponse(isGoalsRead(id));
      await page.keyboard.press('Enter');
      expect((await addedGoal).status()).toBe(201);
      listed = (await (await reread).json()) as AdminGoal[];
      await expect(newGoal(page)).toBeFocused();
      await expect(newGoal(page)).toHaveValue('');
    }
    await expect(goalTexts(page)).toHaveText([kept, archivedLater]);

    // From Save, each goal row's Move up, Move down, Edit and Archive, the end ones unavailable but there, then New
    // goal and Add goal, then Past notes, Write past-day note and Archive participant (participants.md *Keyboard*).
    const bothRows = [...rowStops(kept), ...rowStops(archivedLater)];
    await expectStops(page, 'Shift+Tab', [...bothRows].reverse().concat('Save'));
    await expectStops(page, 'Tab', [...bothRows, ...addForm, ...actionStops, 'outside the page']);

    // The second archived from its row: Archived goals opens, with focus on its Restore, which comes after the summary
    // and before Actions while the section is open.
    await expectStops(page, 'Shift+Tab', [
      'Archive participant',
      'Export record',
      'Write past-day note',
      'Past notes',
      ...[...addForm].reverse(),
      rowName('Archive', archivedLater),
    ]);
    const archivedGoal = page.waitForResponse(isGoalWrite('POST', `/goals/${listed[1].id}/archive`));
    await page.keyboard.press('Enter');
    expect((await archivedGoal).status()).toBe(204);
    await expect(goalButton(page, 'Restore', archivedLater)).toBeFocused();
    await expect(archivedSection(page)).toHaveAttribute('open', '');
    const goalStops = [...rowStops(kept), ...addForm, 'Archived goals'];
    await expectStops(page, 'Tab', [...actionStops, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      'Archive participant',
      'Export record',
      'Write past-day note',
      'Past notes',
      rowName('Restore', archivedLater),
      ...[...goalStops].reverse(),
      'Save',
    ]);

    // Enter on the summary closes it, and Tab then passes Restore by.
    await expectStops(page, 'Tab', goalStops);
    await page.keyboard.press('Enter');
    await expect(archivedSection(page)).not.toHaveAttribute('open');
    await expectStops(page, 'Tab', ['Past notes', 'Write past-day note', 'Export record', 'Archive participant']);

    // Archived: the notice takes focus, then an archived participant's page has the back link, Restore, the Archived
    // goals summary, as its goals are text alone, and Past notes.
    const archived = page.waitForResponse(isChange('POST', `/${id}/archive`));
    await page.keyboard.press('Enter');
    expect((await archived).status()).toBe(204);
    await expect.poll(() => whereFocusIs(page)).toContain(archivedWords(siobhan.name));
    await expectStops(page, 'Tab', ['Restore', 'Archived goals', 'Past notes', 'Export record', 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      'Export record',
      'Past notes',
      'Archived goals',
      'Restore',
      'Participants',
      'Manage',
    ]);
    await expectStops(page, 'Tab', ['Participants', 'Restore']);

    // Restored: the <h1> takes focus, and the form, the goal's buttons, the add form and Archive participant are back.
    const restored = page.waitForResponse(isChange('POST', `/${id}/restore`));
    await page.keyboard.press('Enter');
    expect((await restored).status()).toBe(204);
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', [...form, ...goalStops, ...actionStops, 'outside the page']);

    // Back to the list, where they are now to be found: Find a participant, then Clear while it holds text.
    await expectStops(page, 'Shift+Tab', [
      'Archive participant',
      'Export record',
      'Write past-day note',
      'Past notes',
      ...[...form, ...goalStops].reverse(),
      'Participants',
    ]);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toHaveText('Participants');
    await expect(heading(page)).toBeFocused();
    await expect(rows(page)).toHaveText([siobhan.name]);
    await expectStops(page, 'Tab', [
      'Add participant',
      'Find a participant',
      'Active',
      'Archived',
      siobhan.name,
      'outside the page',
    ]);
    await expectStops(page, 'Shift+Tab', [siobhan.name, 'Archived', 'Active', 'Find a participant']);
    await page.keyboard.type('obr');
    await expectStops(page, 'Tab', ['Clear search', 'Active', 'Archived', siobhan.name]);
    await expectStops(page, 'Shift+Tab', ['Archived', 'Active', 'Clear search']);
    await page.keyboard.press('Enter');
    await expect(findField(page)).toBeFocused();
    await expect(findField(page)).toHaveValue('');

    // Their row opens their page, and its back link returns focus to the row.
    await expectStops(page, 'Tab', ['Active', 'Archived', siobhan.name]);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toHaveText(siobhan.name);
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Shift+Tab', ['Participants']);
    await page.keyboard.press('Enter');
    await expect(rows(page).first()).toBeFocused();
    await expectNothingKept(page, [siobhan.givenName, siobhan.familyName, kept, archivedLater]);
  });
}

test('the list in each of its states, the views and the search, passes axe at phone and laptop width', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants');

  // The Active view: the active by family name, Find a participant, and Active marked.
  await expect(heading(page)).toHaveText('Participants');
  await expect(page).toHaveTitle(title('Participants'));
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(view(page, 'Active')).toHaveAttribute('aria-current', 'page');
  await expect(findField(page)).toHaveAttribute('autocomplete', 'off');
  await expectAxeToPass(page);

  // The Archived view, marked in its place.
  await view(page, 'Archived').click();
  await expect(page).toHaveURL('/manage/participants?view=archived');
  await expect(rows(page)).toHaveText(['Ben Archer']);
  await expect(main(page).locator('[aria-current]')).toHaveText(['Archived']);
  await expectAxeToPass(page);

  // No match: the text kept, Clear shown, and the sentence in place of the rows.
  await findField(page).fill('Zed');
  await expect(sentence(page, 'No participant matches ‘Zed’.')).toBeVisible();
  await expect(clear(page)).toBeVisible();
  await expect(rows(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await expectNothingKept(page, ['Zed', 'Ben', 'Archer', 'Jane', 'Citizen', 'Ravi', 'Silva', '1987', '7 March']);

  // Clear empties the field and gives it focus again.
  await clear(page).click();
  await expect(findField(page)).toBeFocused();
  await expect(rows(page)).toHaveText(['Ben Archer']);

  // Every participant archived: the Active view's sentence, with the search kept.
  await serveParticipants(page, [ben]);
  await page.goto('/manage/participants');
  await expect(sentence(page, 'No active participants.')).toBeVisible();
  await expect(findField(page)).toBeVisible();
  await expectAxeToPass(page);

  // Nobody archived: the Archived view's sentence, with the search kept.
  await serveParticipants(page, [jane, ravi]);
  await page.goto('/manage/participants?view=archived');
  await expect(sentence(page, 'No archived participants.')).toBeVisible();
  await expect(findField(page)).toBeVisible();
  await expectAxeToPass(page);

  // Nobody at all: the sentence, Add participant and the two views, and no search (participants.md *States*).
  await serveParticipants(page, []);
  await page.goto('/manage/participants');
  await expect(sentence(page, 'No participants yet.')).toBeVisible();
  await expect(addParticipant(page)).toBeVisible();
  await expect(views(page)).toHaveText(['Active', 'Archived']);
  await expect(search(page)).toHaveCount(0);
  await expectAxeToPass(page);
});

test('the list held shows "Loading participants…" from 1 s, then its failure and "still", each passing axe', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await serveMe(page, manager);
  const heldList = await holdNext(page, '**/api/admin/participants?includeArchived=true');
  await page.goto('/manage');
  await expect(heading(page)).toHaveText('Manage');

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await main(page).getByRole('link', { name: 'Participants', exact: true }).click();
  const list = await heldList();
  await page.clock.runFor(999);
  await expect(heading(page)).toHaveText('Participants');
  await expect(addParticipant(page)).toBeVisible();
  await expect(loadLine(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(loadLine(page)).toHaveText('Loading participants…');
  await page.clock.resume();
  await expectAxeToPass(page);

  // No connection, for the held request and the query client's one silent retry.
  await failParticipants(page);
  await list.abort('internetdisconnected');
  await expect(loadLine(page)).toHaveText('The participant list did not load: no connection. Try again.');
  await expect(search(page)).toHaveCount(0);
  await expectAxeToPass(page);

  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('The participant list still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expectAxeToPass(page);

  await serveParticipants(page, participants);
  await tryAgain(page).click();
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(heading(page)).toBeFocused();
});

test('Add participant: the form, a failed Save, no connection and the 201, each passing axe', async ({ page }) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants/new');

  await expect(heading(page)).toHaveText('Add participant');
  await expect(page).toHaveTitle(title('Add participant'));
  await expect(backLink(page)).toHaveAttribute('href', '/manage/participants');
  // Someone else's details: nothing asked of the browser's autofill, and nothing that a form could send by name.
  for (const input of await main(page).getByRole('textbox').all()) {
    await expect(input).toHaveAttribute('autocomplete', 'off');
    await expect(input).not.toHaveAttribute('name');
    await expect(input).not.toHaveAttribute('maxlength');
  }
  await expectAxeToPass(page);

  // Every field missing: the summary above the <h1>, focused, and the title marked.
  await save(page).click();
  await expect.poll(() => whereFocusIs(page)).toMatch(/^There is a problem/);
  await expect(page).toHaveTitle(`Error: ${title('Add participant')}`);
  await expectAxeToPass(page);

  // No connection: the alert above Save, the typed values kept and focus on Save.
  await fillDetails(page, siobhan.givenName, siobhan.familyName, ['7', '3', '1987']);
  await expect(page).toHaveTitle(title('Add participant'));
  await answerChanges(page, 'POST', '', 'no connection');
  await save(page).click();
  await expect(saveAlert(page)).toHaveText('Not saved: no connection. Try again.');
  await expect(save(page)).toBeFocused();
  await expect(field(page, 'Family name')).toHaveValue(siobhan.familyName);
  await expectAxeToPass(page);

  // The 201: the address replaced, the name as the <h1>, "Saved 4:12 pm" from its Date, and Archive participant.
  const added = madeUpParticipant('5b2e8d4a-7c1f-4e93-a6d0-3f9b1c7e2a58', siobhan.givenName, siobhan.familyName);
  await serveParticipants(page, [...participants, added]);
  await answerChanges(page, 'POST', '', { status: 201, json: { id: added.id } });
  await save(page).click();
  await expect(page).toHaveURL(`/manage/participants/${added.id}`);
  await expect(heading(page)).toHaveText(siobhan.name);
  await expect(page).toHaveTitle(title('Participant'));
  await expectSavedAt412(page);
  await expect(save(page)).toBeFocused();
  await expect(archive(page)).toBeVisible();
  await expectAxeToPass(page);
  await expectNothingKept(page, [siobhan.givenName, siobhan.familyName, '1987', '7 March']);
});

test("a participant's page saved, stale, refused, archived and restored, each passing axe", async ({ page }) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, []);
  await page.goto(`/manage/participants/${jane.id}`);

  await expect(heading(page)).toHaveText('Jane Citizen');
  await expect(page).toHaveTitle(title('Participant'));
  await expect(field(page, 'Given name')).toHaveValue('Jane');
  await expectAxeToPass(page);

  // Saved: the 200's Date as "Saved 4:12 pm".
  const corrected: AdminParticipant = { ...jane, dateOfBirth: '1987-03-08', rowVersion: 'AAAAAAAB3e8b=' };
  await serveParticipants(page, [ben, corrected, ravi]);
  await answerChanges(page, 'PUT', `/${jane.id}`, { status: 200 });
  await field(page, 'Day').fill('8');
  await save(page).click();
  await expectSavedAt412(page);
  await expect(save(page)).toBeFocused();
  await expectAxeToPass(page);

  // Stale: the alert above Save, what was typed kept, focus on Save.
  await answerChanges(page, 'PUT', `/${jane.id}`, { status: 412 });
  await field(page, 'Given name').fill('Janet');
  await save(page).click();
  await expect(saveAlert(page)).toHaveText(
    'Someone else changed these details while you were editing. Check the details, then save again.',
  );
  await expect(field(page, 'Given name')).toHaveValue('Janet');
  await expect(heading(page)).toHaveText('Jane Citizen');
  await expect(save(page)).toBeFocused();
  await expectAxeToPass(page);

  // Archive refused as stale, the participant still active: the alert above Archive participant.
  await answerChanges(page, 'POST', `/${jane.id}/archive`, { status: 412 });
  await archive(page).click();
  await expect(archiveAlert(page)).toHaveText(
    'Someone else changed this participant just now. Check the page, then try again.',
  );
  await expectAxeToPass(page);

  // Archive with no connection.
  await answerChanges(page, 'POST', `/${jane.id}/archive`, 'no connection');
  await archive(page).click();
  await expect(archiveAlert(page)).toHaveText('Not archived: no connection. Try again.');
  await expect(archive(page)).toBeFocused();
  await expectAxeToPass(page);

  // Archived: read-only, with the notice focused and the date of birth in words.
  await serveParticipants(page, [ben, { ...corrected, status: 'Archived', rowVersion: 'AAAAAAAC3e8b=' }, ravi]);
  await answerChanges(page, 'POST', `/${jane.id}/archive`, { status: 204 });
  await archive(page).click();
  await expect.poll(() => whereFocusIs(page)).toContain(archivedWords('Jane Citizen'));
  await expect(main(page).getByRole('definition')).toHaveText(['Jane', 'Citizen', '8 March 1987']);
  await expect(actionLinks(page)).toHaveText(['Past notes', 'Export record']);
  await expect(archive(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // Restore with no connection: the alert above Restore, in the notice.
  await answerChanges(page, 'POST', `/${jane.id}/restore`, 'no connection');
  await restore(page).click();
  await expect(restoreAlert(page)).toHaveText('Not restored: no connection. Try again.');
  await expect(restore(page)).toBeFocused();
  await expectAxeToPass(page);

  // Restored: the notice gone, the form back, and the <h1> focused.
  await serveParticipants(page, [ben, { ...corrected, rowVersion: 'AAAAAAAD3e8b=' }, ravi]);
  await answerChanges(page, 'POST', `/${jane.id}/restore`, { status: 204 });
  await restore(page).click();
  await expect(heading(page)).toBeFocused();
  await expect(noticeWords(page)).toHaveCount(0);
  await expect(field(page, 'Given name')).toHaveValue('Jane');
  await expectAxeToPass(page);
  await expectNothingKept(page, ['Jane', 'Janet', 'Citizen', '1987', '8 March']);
});

test("an archived participant's page, a page that did not load and an unknown ID, each passing axe", async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, []);

  // Opened on an archived participant: the notice there as plain content, which takes no focus, and Actions with Past
  // notes and Export record.
  await page.goto(`/manage/participants/${ben.id}`);
  await expect(heading(page)).toHaveText('Ben Archer');
  await expect(noticeWords(page)).toHaveText(archivedWords('Ben Archer'));
  await expect(noticeWords(page).locator('xpath=../..')).not.toHaveAttribute('tabindex');
  await expect(noticeWords(page).locator('xpath=../..')).not.toBeFocused();
  await expect(main(page).getByRole('definition')).toHaveText(['Ben', 'Archer', '7 March 1987']);
  await expect(actionLinks(page)).toHaveText(['Past notes', 'Export record']);
  await expect(actionLinks(page).first()).toHaveAttribute('href', `/participants/${ben.id}/notes`);
  await expect(actionLinks(page).last()).toHaveAttribute('href', `/manage/participants/${ben.id}/export`);
  await expect(archive(page)).toHaveCount(0);
  await expect(backLink(page)).toHaveAttribute('href', '/manage/participants?view=archived');
  await expectAxeToPass(page);

  // An ID not on the list: Page not found, with no back link.
  await page.goto('/manage/participants/7c4e1a9b-2f6d-4b83-9e05-8a1d3c6f2b47');
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  await expect(backLink(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // No connection: the fallback <h1>, the sentence and Try again, within 2 s of the page loading.
  await failParticipants(page);
  await page.goto(`/manage/participants/${jane.id}`);
  await expect(loadLine(page)).toHaveText('This participant did not load: no connection. Try again.', {
    timeout: 2_000,
  });
  await expect(heading(page)).toHaveText('Participant');
  await expect(tryAgain(page)).toBeVisible();
  await expect(backLink(page)).toHaveAttribute('href', '/manage/participants');
  await expectAxeToPass(page);

  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('This participant still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expectAxeToPass(page);
});

test("a participant's page loading, then saving, archiving and restoring, each passing axe while it waits", async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveGoals(page, []);

  // "Loading participant…" from 1 s, with the back link alone before it.
  const heldList = await holdNext(page, '**/api/admin/participants?includeArchived=true');
  await page.goto(`/manage/participants/${jane.id}`);
  const list = await heldList();
  await expect(loadLine(page)).toHaveText('Loading participant…');
  await expect(heading(page)).toHaveCount(0);
  await expect(backLink(page)).toBeVisible();
  await expectAxeToPass(page);
  await serveParticipants(page, participants);
  await list.fulfill({ json: participants });
  await expect(heading(page)).toHaveText('Jane Citizen');

  // Each change held until its button is busy and axe has passed, then answered.
  const changes = [
    { path: `/${jane.id}`, press: save, busy: 'Saving…', status: 200 },
    { path: `/${jane.id}/archive`, press: archive, busy: 'Archiving…', status: 204 },
    { path: `/${jane.id}/restore`, press: restore, busy: 'Restoring…', status: 204 },
  ];
  for (const [index, { path, press, busy, status }] of changes.entries()) {
    const heldChange = await holdNext(page, `**/api/admin/participants${path}`);
    await serveParticipants(page, [ben, { ...jane, status: index === 1 ? 'Archived' : 'Active' }, ravi]);
    await press(page).click();
    const change = await heldChange();
    await expect(busyButton(page)).toHaveText(busy, { useInnerText: true });
    await expectAxeToPass(page);
    await change.fulfill({ status, headers: { date: answeredAt } });
    await expect(busyButton(page)).toHaveCount(0);
  }
});

test('at 320 px with text at 200%, nothing on the Participants screens scrolls sideways', async ({ page }) => {
  const long = madeUpParticipant('6d2f8b1e-5a94-4c37-a1e6-9b4d0f7c3e52', 'Bartholomew', 'Featherstonehaugh-Montgomery');
  await serveMe(page, manager);
  await serveParticipants(page, [ben, jane, ravi, long]);
  await serveGoals(page, []);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/manage/participants');
  await setTextTo200Percent(page);
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva', 'Bartholomew Featherstonehaugh-Montgomery']);
  expect(await scrollsSideways(page), 'the list').toBe(false);

  await findField(page).fill('Wolfeschlegelsteinhausenbergerdorff');
  await expect(sentence(page, 'No participant matches ‘Wolfeschlegelsteinhausenbergerdorff’.')).toBeVisible();
  expect(await scrollsSideways(page), 'the no-match sentence').toBe(false);

  await addParticipant(page).click();
  await save(page).click();
  await expect.poll(() => whereFocusIs(page)).toMatch(/^There is a problem/);
  expect(await scrollsSideways(page), 'Add participant with every field in error').toBe(false);

  await backLink(page).click();
  await rows(page).filter({ hasText: 'Bartholomew' }).click();
  await answerChanges(page, 'PUT', `/${long.id}`, 'no connection');
  await save(page).click();
  await expect(saveAlert(page)).toHaveText('Not saved: no connection. Try again.');
  expect(await scrollsSideways(page), 'a participant with a long name, not saved').toBe(false);

  await backLink(page).click();
  await view(page, 'Archived').click();
  await rows(page).filter({ hasText: 'Ben Archer' }).click();
  await answerChanges(page, 'POST', `/${ben.id}/restore`, 'no connection');
  await restore(page).click();
  await expect(restoreAlert(page)).toHaveText('Not restored: no connection. Try again.');
  expect(await scrollsSideways(page), 'an archived participant, not restored').toBe(false);
});

test('targets: 44 px links, fields and summary, 48 px buttons and search, 56 px rows, on phone and laptop', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, goals);

  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/manage/participants');
    await expectAtLeast(rowItems(page), 44, 56);
    await findField(page).fill('j');
    await expectAtLeast(addParticipant(page), 48, 48);
    await expectAtLeast(findField(page), 44, 48);
    await expectAtLeast(clear(page), 48, 48);
    await expectAtLeast(views(page), 44, 44);

    await rows(page).first().click();
    await expect(heading(page)).toHaveText('Jane Citizen');
    await expectAtLeast(backLink(page), 44, 44);
    await expectAtLeast(main(page).getByRole('textbox'), 44, 44);
    await expectAtLeast(save(page), 48, 48);
    await expectAtLeast(archive(page), 48, 48);
    // Each goal row's four buttons and Add goal, then the Archived goals summary and, once it is open, each Restore.
    await expectAtLeast(goalsSection(page).getByRole('button'), 44, 48);
    await expectAtLeast(archivedSummary(page), 44, 44);
    await archivedSummary(page).click();
    await expectAtLeast(archivedSection(page).getByRole('button'), 44, 48);

    await page.goto(`/manage/participants/${ben.id}`);
    await expectAtLeast(restore(page), 48, 48);
    await expectAtLeast(archivedSummary(page), 44, 44);
  }
});

test('in forced colours, the focus ring shows in Highlight at each stop, and unavailable buttons in GrayText', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, goals);
  await page.goto('/manage/participants');
  await expect(rows(page)).toHaveCount(2);
  await heading(page).focus();
  // The colours that forced colours give Highlight, which base.css's ring takes there, and GrayText, which an
  // unavailable button takes, each unlike the ring's own colour and the other.
  const [highlight, grayText] = await Promise.all([systemColour(page, 'Highlight'), systemColour(page, 'GrayText')]);
  expect(new Set([highlight, grayText, ring.colour]).size).toBe(3);
  const forcedRing = { ...ring, colour: highlight };

  await expectStops(page, 'Tab', ['Add participant', 'Find a participant'], forcedRing);
  await page.keyboard.type('a');
  await expectStops(page, 'Tab', ['Clear search', 'Active', 'Archived'], forcedRing);
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Enter');
  await expectStops(page, 'Tab', ['Active', 'Archived', 'Jane Citizen', 'Ravi de Silva'], forcedRing);

  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('Enter');
  await expect(heading(page)).toHaveText('Jane Citizen');
  await expectStops(page, 'Shift+Tab', ['Participants'], forcedRing);
  await expectStops(
    page,
    'Tab',
    [
      ...['Given name', 'Family name', 'Day', 'Month', 'Year', 'Save'],
      ...[breakfast, bus, sister].flatMap((goal) => rowStops(goal.text)),
      ...['New goal', 'Add goal', 'Archived goals'],
    ],
    forcedRing,
  );
  await page.keyboard.press('Enter');
  await expectStops(page, 'Tab', [rowName('Restore', washing.text), rowName('Restore', shop.text)], forcedRing);
  await expectStops(page, 'Tab', actionStops, forcedRing);

  // Move up on the first goal and Move down on the last, unavailable: GrayText words and dashed GrayText borders, where
  // the others keep theirs (list-editor.md *States*).
  for (const end of [goalButton(page, 'Move up', breakfast.text), goalButton(page, 'Move down', sister.text)]) {
    await expect(end).toHaveCSS('color', grayText);
    await expect(end).toHaveCSS('border-top-color', grayText);
    await expect(end).toHaveCSS('border-top-style', 'dashed');
  }
  await expect(goalButton(page, 'Move down', breakfast.text)).not.toHaveCSS('color', grayText);
  await expect(goalButton(page, 'Move down', breakfast.text)).toHaveCSS('border-top-style', 'solid');

  await serveParticipants(page, [ben, { ...jane, status: 'Archived', rowVersion: 'AAAAAAAB3e8b=' }, ravi]);
  await answerChanges(page, 'POST', `/${jane.id}/archive`, { status: 204 });
  await page.keyboard.press('Enter');
  await expect.poll(() => whereFocusIs(page)).toContain(archivedWords('Jane Citizen'));
  await expectStops(page, 'Tab', ['Restore', 'Archived goals'], forcedRing);
});

test("a participant's goals in each of their states pass axe at phone and laptop width", async ({ page }) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  const store = await serveGoals(page, goals);
  await page.goto(`/manage/participants/${jane.id}`);

  // Default: the help text, the active goals numbered in the list's order, Move up on the first and Move down on the
  // last unavailable, in place and dashed, saying why when pressed, and the archived goals' section closed.
  await expect(goalsSection(page).getByText(goalsHelp)).toBeVisible();
  await expect(goalTexts(page)).toHaveText([breakfast.text, bus.text, sister.text]);
  for (const end of [goalButton(page, 'Move up', breakfast.text), goalButton(page, 'Move down', sister.text)]) {
    await expect(end).toBeVisible();
    await expect(end).toHaveAttribute('aria-disabled', 'true');
    await expect(end).toHaveCSS('border-top-style', 'dashed');
  }
  await goalButton(page, 'Move up', breakfast.text).press('Enter');
  await expect(pageStatus(page)).toHaveText('Already at the top');
  await expect(archivedSection(page)).not.toHaveAttribute('open');
  await expectAxeToPass(page);

  // Busy: a move held until Move down shows "Moving…" and axe has passed, the order on screen unchanged meanwhile.
  const heldMove = await holdNext(page, goalWrites.reorder);
  await goalButton(page, 'Move down', breakfast.text).click();
  const move = await heldMove();
  await expect(busyButton(page)).toHaveText('Moving…', { useInnerText: true });
  await expect(goalTexts(page)).toHaveText([breakfast.text, bus.text, sister.text]);
  await expectAxeToPass(page);
  await move.fallback();
  await expect(goalTexts(page)).toHaveText([bus.text, breakfast.text, sister.text]);
  await expect(goalButton(page, 'Move down', breakfast.text)).toBeFocused();

  // Editing: the row's form in its place, its box focused and holding the goal's words.
  await goalButton(page, 'Edit', bus.text).click();
  await expect(editGoal(page)).toBeFocused();
  await expect(editGoal(page)).toHaveValue(bus.text);
  await expectAxeToPass(page);

  // Field error: whitespace alone, refused on Save above the box, with focus in it, no error summary and no "Error: "
  // in the title.
  await editGoal(page).fill('   ');
  await saveGoal(page).click();
  await expect(goalError(page)).toHaveText('Error: Enter the goal');
  await expect(editGoal(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(editGoal(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Participant'));
  await expectAxeToPass(page);

  // Edit conflict: someone else rewords the goal first, so the Save is stale; the typed words stay, with theirs.
  const theirs = 'Catches the bus to day program';
  const typed = 'Catches the bus to day program and texts when she arrives';
  store.changeElsewhere(bus.id, { text: theirs });
  await editGoal(page).fill(typed);
  await expect(goalError(page)).toHaveCount(0);
  await saveGoal(page).click();
  await expect(editForm(page).getByRole('alert')).toHaveText(
    `Someone else changed this goal while you were editing. It now says: ${theirs}. Save to replace it with your `
      + 'wording, or Cancel to keep it.',
  );
  await expect(editGoal(page)).toHaveValue(typed);
  await expectAxeToPass(page);
  await cancelGoal(page).click();
  await expect(goalButton(page, 'Edit', theirs)).toBeFocused();

  // Request error: an Archive with no connection says so in its row, with focus kept on Archive.
  await page.route(goalWrites.archive, (route) => route.abort('internetdisconnected'), { times: 1 });
  await goalButton(page, 'Archive', sister.text).click();
  await expect(goalRow(page, sister.text).getByRole('alert')).toHaveText('Not archived: no connection. Try again.');
  await expect(goalButton(page, 'Archive', sister.text)).toBeFocused();
  await expectAxeToPass(page);

  // List conflict: someone else archives the last goal, so a move built from the list as the page last read it is
  // refused; the list is read again, with the message above it, focused.
  store.changeElsewhere(sister.id, { archived: true });
  await goalButton(page, 'Move down', theirs).click();
  await expect(conflictMessage(page)).toBeFocused();
  await expect(goalTexts(page)).toHaveText([theirs, breakfast.text]);
  await expectAxeToPass(page);

  // Archived section open: Archive puts the goal first in it, opening it, with focus on its Restore, and the list
  // conflict goes.
  await goalButton(page, 'Archive', breakfast.text).click();
  await expect(goalButton(page, 'Restore', breakfast.text)).toBeFocused();
  await expect(archivedSection(page)).toHaveAttribute('open', '');
  await expect(archivedTexts(page)).toHaveText([breakfast.text, sister.text, washing.text, shop.text]);
  await expect(goalTexts(page)).toHaveText([theirs]);
  await expect(conflictMessage(page)).toHaveCount(0);
  await expect(pageStatus(page)).toHaveText('Goal archived');
  await expectAxeToPass(page);

  // Field error on Add: nothing typed.
  await addGoal(page).click();
  await expect(goalError(page)).toHaveText('Error: Enter the goal');
  await expect(newGoal(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(newGoal(page)).toBeFocused();
  await expectAxeToPass(page);
  await expectNothingKept(page, [breakfast.text, bus.text, sister.text, washing.text, shop.text, theirs, typed]);

  // No goals: the sentence in place of the list, the add form, and no archived goals' section.
  await serveGoals(page, []);
  await page.goto(`/manage/participants/${jane.id}`);
  await expect(goalsSection(page).getByText(noGoals)).toBeVisible();
  await expect(goalsSection(page).getByRole('list')).toHaveCount(0);
  await expect(archivedSection(page)).toHaveCount(0);
  await expect(newGoal(page)).toBeVisible();
  await expectAxeToPass(page);

  // Read-only, for an archived participant: the goals numbered as text, with no help text, buttons or add form, and
  // the archived ones as text too.
  await serveGoals(page, goals);
  await page.goto(`/manage/participants/${ben.id}`);
  await expect(goalTexts(page)).toHaveText([breakfast.text, bus.text, sister.text]);
  await expect(goalsSection(page).locator('ol')).toBeVisible();
  await expect(goalsSection(page).getByRole('button')).toHaveCount(0);
  await expect(goalsSection(page).getByText(goalsHelp)).toHaveCount(0);
  await expect(newGoal(page)).toHaveCount(0);
  await archivedSummary(page).click();
  await expect(archivedTexts(page)).toHaveText([washing.text, shop.text]);
  await expect(archivedSection(page).getByRole('button')).toHaveCount(0);
  await expectAxeToPass(page);
  await expectNothingKept(page, [breakfast.text, bus.text, sister.text, washing.text, shop.text]);
});

test('goal rows: two lines of label-width buttons at 375 px, beside the text at 1280 px, and under it at 200%', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, goals);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto(`/manage/participants/${jane.id}`);
  await expect(goalTexts(page)).toHaveText([breakfast.text, bus.text, sister.text]);
  const labels = ['Move up', 'Move down', 'Edit', 'Archive'] as const;
  const boxesOf = (text: string) => Promise.all(labels.map((label) => boxOf(goalButton(page, label, text))));

  // On a phone, Move up and Move down share a line under the text, and Edit and Archive the next (list-editor.md
  // *Phone and laptop*), each label width, at least 44 by 48 px.
  for (const goal of [breakfast, bus, sister]) {
    const text = await boxOf(goalRow(page, goal.text).locator('p').first());
    const [up, down, edit, archive] = await boxesOf(goal.text);
    expect(up.y, `Move up under "${goal.text}"`).toBeGreaterThanOrEqual(text.y + text.height);
    expect(down.y, `Move down beside Move up, "${goal.text}"`).toBeCloseTo(up.y, 0);
    expect(archive.y, `Archive beside Edit, "${goal.text}"`).toBeCloseTo(edit.y, 0);
    expect(edit.y, `Edit on the line after Move up, "${goal.text}"`).toBeGreaterThanOrEqual(up.y + up.height);
  }
  await expectLabelWidth(goalsSection(page).locator('ol button'));
  await archivedSummary(page).click();
  await expectLabelWidth(archivedSection(page).getByRole('button'));
  await goalButton(page, 'Edit', bus.text).click();
  await expectLabelWidth(editForm(page).getByRole('button'));
  expect((await boxOf(cancelGoal(page))).y, 'Cancel beside Save').toBeCloseTo((await boxOf(saveGoal(page))).y, 0);
  // Add goal keeps the shared button's rule: as wide as the field on a phone.
  expect((await boxOf(addGoal(page))).width).toBeCloseTo((await boxOf(newGoal(page))).width, 0);
  await cancelGoal(page).click();

  // On a laptop, the four sit at the top of the row, after the text, on its line; Add goal is its label's width, and
  // the field stays in the reading column while the list uses the wider page.
  await page.setViewportSize({ width: 1280, height: 800 });
  for (const goal of [breakfast, bus, sister]) {
    const text = await boxOf(goalRow(page, goal.text).locator('p').first());
    for (const [index, button] of (await boxesOf(goal.text)).entries()) {
      expect(button.y, `${labels[index]} on the line of "${goal.text}"`).toBeCloseTo(text.y, 0);
      expect(button.x, `${labels[index]} after "${goal.text}"`).toBeGreaterThanOrEqual(text.x + text.width);
    }
  }
  await expectLabelWidth(goalsSection(page).locator('ol button, details button'));
  expect((await boxOf(addGoal(page))).width).toBeLessThan((await boxOf(newGoal(page))).width);
  expect((await boxOf(newGoal(page))).width).toBeLessThan((await boxOf(goalsSection(page).locator('ol'))).width);

  // At 200% text, the text keeps its 16rem, so the four drop under it by themselves.
  await setTextTo200Percent(page);
  for (const goal of [breakfast, bus, sister]) {
    const text = await boxOf(goalRow(page, goal.text).locator('p').first());
    for (const [index, button] of (await boxesOf(goal.text)).entries()) {
      expect(button.y, `${labels[index]} under "${goal.text}"`).toBeGreaterThanOrEqual(text.y + text.height);
    }
  }
});

test('at 320 px with text at 200%, no goal state scrolls sideways', async ({ page }) => {
  const long = madeUpGoal('1a6c4d9f-8b7c-4e0a-9c5b-7c3e0f2b6a48', 'Wolfeschlegelsteinhausenbergerdorff'.repeat(5), 0);
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  const store = await serveGoals(page, [long, ...goals]);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto(`/manage/participants/${jane.id}`);
  await setTextTo200Percent(page);
  await expect(goalTexts(page)).toHaveText([long.text, breakfast.text, bus.text, sister.text]);
  expect(await scrollsSideways(page), 'the goals, one a word of 175 letters').toBe(false);

  await goalButton(page, 'Edit', long.text).click();
  expect(await scrollsSideways(page), 'its edit form').toBe(false);

  await newGoal(page).fill('a'.repeat(250));
  await addGoal(page).click();
  await expect(goalError(page)).toHaveText('Error: Goal must be 200 characters or less');
  expect(await scrollsSideways(page), 'the add form with 250 characters, refused').toBe(false);
  await newGoal(page).fill('');

  await page.route(goalWrites.archive, (route) => route.abort('internetdisconnected'), { times: 1 });
  await goalButton(page, 'Archive', bus.text).click();
  await expect(goalRow(page, bus.text).getByRole('alert')).toHaveText('Not archived: no connection. Try again.');
  expect(await scrollsSideways(page), 'a goal not archived').toBe(false);

  store.changeElsewhere(sister.id, { archived: true });
  await goalButton(page, 'Move down', breakfast.text).click();
  await expect(conflictMessage(page)).toBeFocused();
  expect(await scrollsSideways(page), 'the list conflict').toBe(false);

  await goalButton(page, 'Archive', breakfast.text).click();
  await expect(goalButton(page, 'Restore', breakfast.text)).toBeFocused();
  expect(await scrollsSideways(page), 'the archived goals open').toBe(false);

  // The same goals, as the browser holds them, read-only on an archived participant's page.
  await page.goto(`/manage/participants/${ben.id}`);
  await archivedSummary(page).click();
  await expect(goalTexts(page)).toHaveText([long.text, bus.text]);
  await expect(archivedTexts(page)).toHaveText([breakfast.text, sister.text, washing.text, shop.text]);
  expect(await scrollsSideways(page), "an archived participant's goals").toBe(false);
  await expectNothingKept(page, [long.text, breakfast.text, bus.text, sister.text, 'a'.repeat(20)]);
});

test("a worker meets Page not found at the list, Add participant and a participant's page, sending no request", async ({
  page,
}) => {
  const adminRequests: string[] = [];
  page.on('request', (request) => {
    if (new URL(request.url()).pathname.startsWith('/api/admin/')) {
      adminRequests.push(request.url());
    }
  });
  await serveMe(page, worker);

  for (const path of ['/manage/participants', '/manage/participants/new', `/manage/participants/${jane.id}`]) {
    await page.goto(path);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle(title('Page not found'));
    await expect(main(page).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
    await expect(backLink(page)).toHaveCount(0);
  }
  await expectAxeToPass(page);

  // The browser reports a page's requests in the order that it sends them (tests/README.md).
  await page.evaluate(() => fetch('/healthz'));
  expect(adminRequests).toEqual([]);
});

// The Participants screens' foundations (T26), each in a test of its own, as the call changes the text size and spacing
// for good.
test('the Active list passes the foundations', async ({ page }) => {
  test.slow();
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants');
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);

  await expectFoundationsToPass(page);
});

test('the Archived view passes the foundations', async ({ page }) => {
  test.slow();
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants?view=archived');
  await expect(rows(page)).toHaveText(['Ben Archer']);

  await expectFoundationsToPass(page);
});

test('Add participant with its errors passes the foundations', async ({ page }) => {
  test.slow();
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants/new');
  await save(page).click();
  await expect.poll(() => whereFocusIs(page)).toMatch(/^There is a problem/);

  await expectFoundationsToPass(page);
});

test("a participant's page with goals and Archived goals open passes the foundations", async ({ page }) => {
  test.slow();
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await serveGoals(page, goals);
  await page.goto(`/manage/participants/${jane.id}`);
  await expect(goalTexts(page)).toHaveText([breakfast.text, bus.text, sister.text]);
  await archivedSummary(page).click();
  await expect(archivedTexts(page)).toHaveText([washing.text, shop.text]);

  await expectFoundationsToPass(page);
});
