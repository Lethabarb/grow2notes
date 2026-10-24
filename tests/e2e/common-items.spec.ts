import type { Locator, Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import {
  bed,
  busOrTrain,
  everyNote,
  failCommonItems,
  groupWrites,
  itemWrites,
  laundry,
  listRead,
  lunch,
  madeUpGroup,
  madeUpItem,
  meal,
  medication,
  paid,
  serveCommonItems,
  threeGroups,
  type AdminCommonItemGroup,
} from './commonItems.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, expectStops, ring } from './focus.ts';
import { expectTextAt7To1 } from './foundations.ts';
import { expectNothingKept } from './kept.ts';
import {
  boxOf,
  contrastOf,
  expectAtLeast,
  expectLabelWidth,
  scrollsSideways,
  setTextTo200Percent,
  systemColour,
} from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe } from './me.ts';
import { holdNext } from './requests.ts';
import { signInThroughSetup } from './setupSignIn.ts';

// Common items in the published app (S01.03.01 to S01.03.08), against common-items.md's criteria for what T06 and T07
// build (tasks.md, T06's and T07's Notes): the Every note group and each other active group's items, with their add
// forms and archived items; and the groups themselves, added, renamed, moved, archived and restored, with an item
// moved between them. Four tests sign in for real, through setup with a passkey on the virtual authenticator, as the
// manager whom admin bootstrap invited with the organisation, and so with its Every note group, empty, and add, reword,
// move, archive and restore its items, or add, rename, move, archive and restore groups and move an item into one, on
// the app's own answers; the others answer /api/auth/me, the groups list and each group and item write in the browser
// with made-up groups and items (me.ts, commonItems.ts), so that none of those requests reaches the app.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// The help text, once at the top of the page (common-items.md component 2).
const help = (page: Page) => main(page).locator('#items-help');
const helpText =
  'Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes apply '
  + 'to notes started from now on.';
// The region's line, beside the page's visually hidden status region (empty-loading-error.md).
const loadLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });

// A group's section, a region named by its <h2> (common-items.md component 5), and what is in it.
const section = (page: Page, group: string) => main(page).getByRole('region', { name: group, exact: true });
const groupHeadings = (page: Page) => main(page).getByRole('heading', { level: 2 });
const everyNoteLine = 'Always shown first, on every note.';
const emptyLine = 'No items in this group. It does not show on notes until it has one.';
// Each active item's text, in the numbered list's order.
const itemTexts = (page: Page, group: string) => section(page, group).locator('ol > li > div > p');
const itemRows = (page: Page, group: string) => section(page, group).locator('ol > li');
const itemRow = (page: Page, group: string, text: string) =>
  section(page, group).getByRole('listitem').filter({ hasText: text });
const archivedSection = (page: Page, group: string) => section(page, group).locator('details');
const archivedSummary = (page: Page, group: string) => archivedSection(page, group).locator('summary');
const archivedTexts = (page: Page, group: string) => archivedSection(page, group).locator('li > div > p');
type RowButton = 'Move up' | 'Move down' | 'Edit' | 'Archive' | 'Restore';
/**
 * An item row's button as its name and its focus stop read: its label, then the item's text, hidden but for screen
 * readers, with a space before the comma, as the hidden text is a block of its own (tests/README.md).
 */
const rowName = (label: RowButton, text: string) => `${label} , ${text}`;
const itemButton = (page: Page, label: RowButton, text: string) =>
  main(page).getByRole('button', { name: rowName(label, text), exact: true });
/** An active item row's four focus stops, in order (common-items.md *Focus order*). */
const rowStops = (text: string) =>
  (['Move up', 'Move down', 'Edit', 'Archive'] as const).map((label) => rowName(label, text));
type GroupButton = 'Move up' | 'Move down' | 'Rename' | 'Archive' | 'Restore';
/**
 * A group's button, in its heading row or, for Restore, in Archived groups, as its name and its focus stop read: its
 * label, then ", [group name] group", hidden but for screen readers, as a row's are (common-items.md component 5).
 */
const groupButtonName = (label: GroupButton, group: string) => `${label} , ${group} group`;
const groupButton = (page: Page, label: GroupButton, group: string) =>
  main(page).getByRole('button', { name: groupButtonName(label, group), exact: true });
// Every group button on the page, by the hidden words after its label, which neither Add group nor a row has.
const groupButtonPattern = / , .+ group$/;
const groupButtons = (page: Page) => main(page).getByRole('button', { name: groupButtonPattern });
/** A group's four focus stops, in order (common-items.md *Focus order*); Every note has none. */
const groupStops = (group: string) =>
  (['Move up', 'Move down', 'Rename', 'Archive'] as const).map((label) => groupButtonName(label, group));
// A group's heading row: its <h2>, then its alert slot and buttons (common-items.md component 5).
const groupHead = (page: Page, group: string) => section(page, group).getByRole('heading', { level: 2 }).locator('..');
// A rename's form, under the group's heading.
const groupNameField = (page: Page) => main(page).getByRole('textbox', { name: 'Group name', exact: true });
const renameForm = (page: Page) =>
  main(page).locator('form').filter({ has: page.getByRole('textbox', { name: 'Group name', exact: true }) });
const saveRename = (page: Page) => renameForm(page).getByRole('button', { name: 'Save', exact: true });
const cancelRename = (page: Page) => renameForm(page).getByRole('button', { name: 'Cancel', exact: true });
// The New group form, after the last group's section (common-items.md component 7).
const newGroup = (page: Page) => main(page).getByRole('textbox', { name: 'New group', exact: true });
const newGroupForm = (page: Page) =>
  main(page).locator('form').filter({ has: page.getByRole('textbox', { name: 'New group', exact: true }) });
const addGroup = (page: Page) => main(page).getByRole('button', { name: 'Add group', exact: true });
// Archived groups, after the New group form, and the name of each group in it (common-items.md component 8).
const archivedGroups = (page: Page) =>
  main(page).locator('details').filter({ has: page.locator('summary', { hasText: /^Archived groups$/ }) });
const archivedGroupsSummary = (page: Page) => archivedGroups(page).locator('summary');
const archivedGroupNames = (page: Page) => archivedGroups(page).locator('li > div > p');
/** An archived items summary's focus stop: its words, then " in [group name]", hidden but for screen readers. */
const summaryStop = (group: string) => `Archived items in ${group}`;
/** The New group form's focus stops, after the last group's section (common-items.md *Focus order*). */
const newGroupStops = ['New group', 'Add group'];
/** The same, the other way round, as Shift+Tab reaches them. */
const newGroupStopsBack = [...newGroupStops].reverse();
const newItem = (page: Page, group: string) =>
  section(page, group).getByRole('textbox', { name: `New item in ${group}`, exact: true });
const addItem = (page: Page, group: string) =>
  section(page, group).getByRole('button', { name: 'Add item', exact: true });
const editItem = (page: Page) => main(page).getByRole('textbox', { name: 'Edit item', exact: true });
const editForm = (page: Page) =>
  main(page).locator('form').filter({ has: page.getByRole('textbox', { name: 'Edit item', exact: true }) });
const saveItem = (page: Page) => editForm(page).getByRole('button', { name: 'Save', exact: true });
const cancelItem = (page: Page) => editForm(page).getByRole('button', { name: 'Cancel', exact: true });
// An item's Group radios, in a fieldset whose legend is "Group", one per active group, each named by the group, and
// the row of each, which is all its label (common-items.md component 6).
const groupRadios = (page: Page) =>
  editForm(page).getByRole('group', { name: 'Group', exact: true }).getByRole('radio');
const groupRadio = (page: Page, group: string) => editForm(page).getByRole('radio', { name: group, exact: true });
const radioRows = (page: Page) => editForm(page).locator('label').filter({ has: page.getByRole('radio') });
// A field's message above its box, with its hidden "Error: " (form-validation.md).
const fieldError = (page: Page, group: string) => section(page, group).locator('[id$="-error"]');
// The list conflict above the groups, which takes focus (common-items.md component 4).
const conflictText = 'Someone else changed this list just now. Check it, then try again.';
const conflictMessage = (page: Page) => main(page).getByText(conflictText, { exact: true });
// The page's one status region, visually hidden, where each change says what it did (common-items.md component 9).
const pageStatus = (page: Page) => main(page).locator('p.visually-hidden[role="status"]');
// A button busy for 400 ms, which then shows its busy label, and is named by it, in place of the one it rests with.
const busyButton = (page: Page) => main(page).locator('button[data-busy]');

/** Whether the response answers the groups list, Common items' one read. */
const isListRead = (response: Response) =>
  response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/admin/common-item-groups';

/** Whether the response answers a write by `method` to the path after `/api/admin` (design.md §6.6). */
const isWrite = (method: 'POST' | 'PUT', path: string) => (response: Response) =>
  response.request().method() === method && new URL(response.url()).pathname === `/api/admin${path}`;

/** The groups that a response to the list holds. */
const groupsIn = async (response: Promise<Response>) => (await (await response).json()) as AdminCommonItemGroup[];

/** The Every note group, with its items, in a list as the API gives it. */
const everyNoteIn = (groups: AdminCommonItemGroup[]) => {
  const found = groups.find((group) => group.isEveryNote);
  if (found === undefined) {
    throw new Error('The list holds no Every note group.');
  }

  return found;
};

/** The group of `name`, with its items, in a list as the API gives it. */
const groupIn = (groups: AdminCommonItemGroup[], name: string) => {
  const found = groups.find((group) => group.name === name);
  if (found === undefined) {
    throw new Error(`The list holds no group named ${name}.`);
  }

  return found;
};

// Each live region that Common items puts into the document with words already in it (common-items.md *Accessibility
// checklist*, *Screen reader*).
let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/manage/common-items');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher fails a test whose page puts a live region in with its words on Common items: here a polite
// region in <main>, as a character count's twin is, where the one in participants.spec.ts puts a status region on the
// Participants screens. Marked to fail, it passes only if the check after it fails it; were the watcher to miss the
// region, as it would if Common items moved from under its address, Playwright would fail the run.
test.fail('a live region put into Common items with its words fails the test', async ({ page }) => {
  await serveMe(page, manager);
  await serveCommonItems(page, [everyNote]);
  await page.goto('/manage/common-items');
  await expect(section(page, 'Every note')).toBeVisible();

  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('aria-live', 'polite');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

// Five made-up common items (A35), as a manager might set Every note up.
const fiveItems = ['Medication prompted', 'Meal prepared', 'Laundry done', 'Bed made', 'Went for a walk'];

test('signed in for real at 375 px, a manager adds five items to Every note, then rewords, moves, archives and '
  + 'restores one', async ({ page, invite, authenticator }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  let read = page.waitForResponse(isListRead);
  await main(page).getByRole('link', { name: 'Common items', exact: true }).click();

  // The organisation's one group, which admin bootstrap made with it: Every note, first, empty, with its line and the
  // empty sentence, and no button but Add item (S01.03.01; common-items.md *States*, "Every note with no items").
  const [bootstrapped] = await groupsIn(read);
  expect(bootstrapped).toMatchObject({ name: 'Every note', isEveryNote: true, archivedAtUtc: null, items: [] });
  const groupId = bootstrapped.id;
  await expect(heading(page)).toHaveText('Common items');
  await expect(heading(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Common items'));
  await expect(help(page)).toHaveText(helpText);
  await expect(groupHeadings(page)).toHaveText(['Every note']);
  await expect(section(page, 'Every note').getByText(everyNoteLine, { exact: true })).toBeVisible();
  await expect(section(page, 'Every note').getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(section(page, 'Every note').getByRole('button')).toHaveText(['Add item'], { useInnerText: true });

  // The next Tab from the <h1> is the add field, as nothing else comes before it.
  await page.keyboard.press('Tab');
  await expect(newItem(page, 'Every note')).toBeFocused();

  // Five items, one after another, each typed into the field that the Add before it left focus in (design.md §14 M1):
  // each the app's 201 with no body, then the list read again with it at the end, the field emptied and the add said.
  let listed: AdminCommonItemGroup['items'] = [];
  for (const [index, text] of fiveItems.entries()) {
    await page.keyboard.type(text);
    const posted = page.waitForResponse(isWrite('POST', `/common-item-groups/${groupId}/items`));
    read = page.waitForResponse(isListRead);
    await addItem(page, 'Every note').click();
    const post = await posted;
    expect(post.status()).toBe(201);
    expect(post.request().postDataJSON()).toEqual({ text });
    expect(await post.text()).toBe('');
    expect(post.headers().location).toBeUndefined();
    listed = everyNoteIn(await groupsIn(read)).items;
    expect(listed.map((item) => [item.text, item.sortOrder, item.archivedAtUtc])).toEqual(
      fiveItems.slice(0, index + 1).map((each, sortOrder) => [each, sortOrder, null]),
    );
    await expect(itemTexts(page, 'Every note')).toHaveText(fiveItems.slice(0, index + 1));
    await expect(newItem(page, 'Every note')).toBeFocused();
    await expect(newItem(page, 'Every note')).toHaveValue('');
    await expect(pageStatus(page)).toHaveText('Item added to the end of the list');
  }
  await expect(section(page, 'Every note').getByText(emptyLine)).toHaveCount(0);
  const ids = listed.map((item) => item.id);

  // The second reworded in its row: Edit focuses the box with the caret after the words, with no Group radios while
  // Every note is the only group; Save sends the words with the item's own group, and the row's version as If-Match,
  // and gets the app's 200, with no body and no ETag; focus goes back to Edit.
  const reworded = 'Meal prepared with support';
  await itemButton(page, 'Edit', fiveItems[1]).click();
  await expect(editItem(page)).toBeFocused();
  await expect(editItem(page)).toHaveValue(fiveItems[1]);
  expect(await editItem(page).evaluate((box: HTMLTextAreaElement) => [box.selectionStart, box.selectionEnd])).toEqual(
    [fiveItems[1].length, fiveItems[1].length],
  );
  await expect(editForm(page).getByRole('radio')).toHaveCount(0);
  await editItem(page).fill(reworded);
  const put = page.waitForResponse(isWrite('PUT', `/common-items/${ids[1]}`));
  read = page.waitForResponse(isListRead);
  await saveItem(page).click();
  const putAnswer = await put;
  expect(putAnswer.status()).toBe(200);
  expect(putAnswer.request().postDataJSON()).toEqual({ text: reworded, groupId });
  expect(await putAnswer.request().headerValue('if-match')).toBe(`"${listed[1].rowVersion}"`);
  expect(await putAnswer.text()).toBe('');
  expect(putAnswer.headers().etag).toBeUndefined();
  listed = everyNoteIn(await groupsIn(read)).items;
  expect(listed[1]).toMatchObject({ id: ids[1], text: reworded, sortOrder: 1 });
  const afterReword = [fiveItems[0], reworded, ...fiveItems.slice(2)];
  await expect(itemTexts(page, 'Every note')).toHaveText(afterReword);
  await expect(itemButton(page, 'Edit', reworded)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Item saved');

  // The first moved down: the group's active IDs, built from the list, sent; the app's 204; the list read again in the
  // new order, and focus back on the item's Move down.
  const reordered = page.waitForResponse(isWrite('PUT', `/common-item-groups/${groupId}/items/order`));
  read = page.waitForResponse(isListRead);
  await itemButton(page, 'Move down', fiveItems[0]).click();
  const order = await reordered;
  expect(order.status()).toBe(204);
  expect(order.request().postDataJSON()).toEqual({ commonItemIds: [ids[1], ids[0], ...ids.slice(2)] });
  listed = everyNoteIn(await groupsIn(read)).items;
  expect(listed.map((item) => [item.id, item.sortOrder])).toEqual(
    [ids[1], ids[0], ...ids.slice(2)].map((each, sortOrder) => [each, sortOrder]),
  );
  await expect(itemTexts(page, 'Every note')).toHaveText([reworded, fiveItems[0], ...fiveItems.slice(2)]);
  await expect(itemButton(page, 'Move down', fiveItems[0])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Moved down to number 2 of 5');

  // The third archived: its version as If-Match, the app's 204, then Archived items open with it, its Restore focused.
  const archived = page.waitForResponse(isWrite('POST', `/common-items/${ids[2]}/archive`));
  read = page.waitForResponse(isListRead);
  await itemButton(page, 'Archive', fiveItems[2]).click();
  const archiveAnswer = await archived;
  expect(archiveAnswer.status()).toBe(204);
  expect(await archiveAnswer.request().headerValue('if-match')).toBe(`"${listed[2].rowVersion}"`);
  listed = everyNoteIn(await groupsIn(read)).items;
  expect(listed.find((item) => item.id === ids[2])).toMatchObject({ sortOrder: 2, archivedAtUtc: expect.any(String) });
  await expect(itemTexts(page, 'Every note')).toHaveText([reworded, fiveItems[0], fiveItems[3], fiveItems[4]]);
  await expect(archivedSection(page, 'Every note')).toHaveAttribute('open', '');
  await expect(archivedTexts(page, 'Every note')).toHaveText([fiveItems[2]]);
  await expect(itemButton(page, 'Restore', fiveItems[2])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Item archived');

  // Restored: the app's 204, the item at the end of the group, focus on its Archive, and Archived items gone.
  const restored = page.waitForResponse(isWrite('POST', `/common-items/${ids[2]}/restore`));
  read = page.waitForResponse(isListRead);
  await itemButton(page, 'Restore', fiveItems[2]).click();
  const restoreAnswer = await restored;
  expect(restoreAnswer.status()).toBe(204);
  expect(await restoreAnswer.request().headerValue('if-match')).toBe(
    `"${listed.find((item) => item.id === ids[2])?.rowVersion}"`,
  );
  listed = everyNoteIn(await groupsIn(read)).items;
  expect(listed.find((item) => item.id === ids[2])).toMatchObject({ sortOrder: 5, archivedAtUtc: null });
  await expect(itemTexts(page, 'Every note')).toHaveText([
    reworded,
    fiveItems[0],
    fiveItems[3],
    fiveItems[4],
    fiveItems[2],
  ]);
  await expect(archivedSection(page, 'Every note')).toHaveCount(0);
  await expect(itemButton(page, 'Archive', fiveItems[2])).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Item restored to the end of the list');
  await expectNothingKept(page, ...fiveItems, reworded);
});

// Two made-up groups (A35), as common-items.md's sketch has them, the second renamed; three made-up items for the
// renamed one, the last added to the first group by mistake and moved.
const [outingName, careName] = ['Community outing', 'Personal care'];
const careRenamed = 'Personal care at home';
const [showered, teeth, misplaced] = ['Showered', 'Brushed teeth', 'Changed into clean clothes'];

test('signed in for real at 375 px, a manager adds two groups, renames one, adds items to it, moves an item into it, '
  + 'moves a group down, then archives and restores the renamed one', async ({ page, invite, authenticator }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signInThroughSetup(page, invite, authenticator);
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  let read = page.waitForResponse(isListRead);
  await main(page).getByRole('link', { name: 'Common items', exact: true }).click();

  // Only Every note, as admin bootstrap leaves it: no group buttons, then the New group form, and no Archived groups
  // (common-items.md *States*, "Only Every note").
  let listed = await groupsIn(read);
  expect(listed.map((group) => group.name)).toEqual(['Every note']);
  await expect(groupHeadings(page)).toHaveText(['Every note']);
  await expect(groupButtons(page)).toHaveCount(0);
  await expect(newGroup(page)).toHaveValue('');
  await expect(archivedGroups(page)).toHaveCount(0);

  // Two groups, each typed into New group and added with Add group: the app's 201 with the group's ID and no Location,
  // then the list read again with the group last, empty; the field emptied, focus in the group's New item field, the
  // next useful step, and the add said (design.md §14 M1).
  const ids: string[] = [];
  for (const [index, name] of [outingName, careName].entries()) {
    await newGroup(page).fill(name);
    const posted = page.waitForResponse(isWrite('POST', '/common-item-groups'));
    read = page.waitForResponse(isListRead);
    await addGroup(page).click();
    const post = await posted;
    expect(post.status()).toBe(201);
    expect(post.request().postDataJSON()).toEqual({ name });
    expect(post.headers().location).toBeUndefined();
    const { id } = (await post.json()) as { id: string };
    ids.push(id);
    listed = await groupsIn(read);
    expect(listed.at(-1)).toMatchObject({ id, name, sortOrder: index, archivedAtUtc: null, items: [] });
    expect(listed.at(-1)?.isEveryNote).toBe(false);
    await expect(groupHeadings(page)).toHaveText(['Every note', outingName, careName].slice(0, index + 2));
    await expect(section(page, name).getByText(emptyLine, { exact: true })).toBeVisible();
    await expect(newItem(page, name)).toBeFocused();
    await expect(newGroup(page)).toHaveValue('');
    await expect(pageStatus(page)).toHaveText('Group added at the end of the list');
  }
  const [outingId, careId] = ids;

  // An item added to Community outing that belongs in the other group.
  await newItem(page, outingName).fill(misplaced);
  const misplacedAdd = page.waitForResponse(isWrite('POST', `/common-item-groups/${outingId}/items`));
  read = page.waitForResponse(isListRead);
  await addItem(page, outingName).click();
  expect((await misplacedAdd).status()).toBe(201);
  listed = await groupsIn(read);
  await expect(itemTexts(page, outingName)).toHaveText([misplaced]);

  // Personal care renamed in its section: Rename puts the Group name field under the group's <h2>, which stays, filled
  // with the name and focused with the caret after it; Save sends the name with the group's version as If-Match and
  // gets the app's 200, with no body and no ETag; then the heading and the New item label say the new name, and focus
  // is back on Rename.
  await groupButton(page, 'Rename', careName).click();
  await expect(section(page, careName).getByRole('textbox', { name: 'Group name', exact: true })).toBeFocused();
  await expect(groupNameField(page)).toHaveValue(careName);
  const caret = await groupNameField(page).evaluate((box: HTMLInputElement) => [box.selectionStart, box.selectionEnd]);
  expect(caret).toEqual([careName.length, careName.length]);
  await expect(section(page, careName).getByRole('button', { name: groupButtonPattern })).toHaveCount(0);
  await page.keyboard.type(' at home');
  const renamed = page.waitForResponse(isWrite('PUT', `/common-item-groups/${careId}`));
  read = page.waitForResponse(isListRead);
  await saveRename(page).click();
  const renameAnswer = await renamed;
  expect(renameAnswer.status()).toBe(200);
  expect(renameAnswer.request().postDataJSON()).toEqual({ name: careRenamed });
  expect(await renameAnswer.request().headerValue('if-match')).toBe(`"${groupIn(listed, careName).rowVersion}"`);
  expect(await renameAnswer.text()).toBe('');
  expect(renameAnswer.headers().etag).toBeUndefined();
  listed = await groupsIn(read);
  expect(groupIn(listed, careRenamed)).toMatchObject({ id: careId, sortOrder: 1 });
  await expect(groupHeadings(page)).toHaveText(['Every note', outingName, careRenamed]);
  await expect(groupButton(page, 'Rename', careRenamed)).toBeFocused();
  await expect(newItem(page, careRenamed)).toBeVisible();
  await expect(pageStatus(page)).toHaveText('Group renamed');

  // Two items added to the renamed group, each at its end, focus staying in its field.
  for (const text of [showered, teeth]) {
    await newItem(page, careRenamed).fill(text);
    const added = page.waitForResponse(isWrite('POST', `/common-item-groups/${careId}/items`));
    read = page.waitForResponse(isListRead);
    await addItem(page, careRenamed).click();
    expect((await added).status()).toBe(201);
    listed = await groupsIn(read);
    await expect(newItem(page, careRenamed)).toBeFocused();
  }
  await expect(itemTexts(page, careRenamed)).toHaveText([showered, teeth]);

  // The misplaced item moved through its Edit: the Group radios, one per active group in order, its own group chosen;
  // Save, with the renamed group chosen, sends its words and that group, with its version as If-Match, and gets the
  // app's 200; then the item is last in the renamed group, Community outing empty, and focus on the item's Edit there.
  const item = groupIn(listed, outingName).items[0];
  await itemButton(page, 'Edit', misplaced).click();
  await expect(editItem(page)).toBeFocused();
  await expect(groupRadios(page)).toHaveCount(3);
  for (const [index, name] of ['Every note', outingName, careRenamed].entries()) {
    await expect(groupRadios(page).nth(index)).toHaveAccessibleName(name);
  }
  await expect(groupRadio(page, outingName)).toBeChecked();
  await groupRadio(page, careRenamed).check();
  const moved = page.waitForResponse(isWrite('PUT', `/common-items/${item.id}`));
  read = page.waitForResponse(isListRead);
  await saveItem(page).click();
  const moveAnswer = await moved;
  expect(moveAnswer.status()).toBe(200);
  expect(moveAnswer.request().postDataJSON()).toEqual({ text: misplaced, groupId: careId });
  expect(await moveAnswer.request().headerValue('if-match')).toBe(`"${item.rowVersion}"`);
  listed = await groupsIn(read);
  expect(groupIn(listed, careRenamed).items.map((each) => [each.text, each.sortOrder])).toEqual([
    [showered, 0],
    [teeth, 1],
    [misplaced, 2],
  ]);
  expect(groupIn(listed, outingName).items).toEqual([]);
  await expect(itemTexts(page, careRenamed)).toHaveText([showered, teeth, misplaced]);
  await expect(section(page, outingName).getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(
    section(page, careRenamed).getByRole('button', { name: rowName('Edit', misplaced), exact: true }),
  ).toBeFocused();
  await expect(pageStatus(page)).toHaveText(`Item saved and moved to the end of ${careRenamed}`);

  // Community outing moved down, its Move up unavailable as the first group after Every note: every active group's ID
  // but Every note's sent, the two swapped; the app's 204; the sections in the new order, focus on the same button.
  await expect(groupButton(page, 'Move up', outingName)).toHaveAttribute('aria-disabled', 'true');
  await expect(groupButton(page, 'Move down', careRenamed)).toHaveAttribute('aria-disabled', 'true');
  const reordered = page.waitForResponse(isWrite('PUT', '/common-item-groups/order'));
  read = page.waitForResponse(isListRead);
  await groupButton(page, 'Move down', outingName).click();
  const order = await reordered;
  expect(order.status()).toBe(204);
  expect(order.request().postDataJSON()).toEqual({ groupIds: [careId, outingId] });
  listed = await groupsIn(read);
  expect(listed.map((group) => [group.name, group.sortOrder])).toEqual([
    ['Every note', 0],
    [careRenamed, 0],
    [outingName, 1],
  ]);
  await expect(groupHeadings(page)).toHaveText(['Every note', careRenamed, outingName]);
  await expect(groupButton(page, 'Move down', outingName)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Group moved down');

  // The renamed group archived, with no dialog: its version as If-Match, the app's 204; its section gone, its items
  // still active, and Archived groups there, open, with it first, focus on its Restore.
  const archived = page.waitForResponse(isWrite('POST', `/common-item-groups/${careId}/archive`));
  read = page.waitForResponse(isListRead);
  await groupButton(page, 'Archive', careRenamed).click();
  const archiveAnswer = await archived;
  expect(archiveAnswer.status()).toBe(204);
  expect(await archiveAnswer.request().headerValue('if-match')).toBe(`"${groupIn(listed, careRenamed).rowVersion}"`);
  listed = await groupsIn(read);
  expect(groupIn(listed, careRenamed)).toMatchObject({ sortOrder: 0, archivedAtUtc: expect.any(String) });
  expect(groupIn(listed, careRenamed).items.map((each) => each.archivedAtUtc)).toEqual([null, null, null]);
  await expect(groupHeadings(page)).toHaveText(['Every note', outingName]);
  await expect(archivedGroups(page)).toHaveAttribute('open', '');
  await expect(archivedGroupNames(page)).toHaveText([careRenamed]);
  await expect(groupButton(page, 'Restore', careRenamed)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Group archived');

  // Restored: the app's 204, the section last among the groups with its items, focus on its Archive, and Archived
  // groups gone.
  const restored = page.waitForResponse(isWrite('POST', `/common-item-groups/${careId}/restore`));
  read = page.waitForResponse(isListRead);
  await groupButton(page, 'Restore', careRenamed).click();
  const restoreAnswer = await restored;
  expect(restoreAnswer.status()).toBe(204);
  expect(await restoreAnswer.request().headerValue('if-match')).toBe(`"${groupIn(listed, careRenamed).rowVersion}"`);
  listed = await groupsIn(read);
  expect(groupIn(listed, careRenamed)).toMatchObject({ sortOrder: 2, archivedAtUtc: null });
  await expect(groupHeadings(page)).toHaveText(['Every note', outingName, careRenamed]);
  await expect(itemTexts(page, careRenamed)).toHaveText([showered, teeth, misplaced]);
  await expect(archivedGroups(page)).toHaveCount(0);
  await expect(groupButton(page, 'Archive', careRenamed)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Group restored to the end of the list');
  await expectNothingKept(page, outingName, careName, careRenamed, showered, teeth, misplaced);
});

// Three made-up items (A35) for the keyboard-only check, the third reworded, then archived and restored.
const [first, second, third] = ['Medication prompted', 'Meal prepared', 'Laundry done'];
const thirdReworded = 'Laundry done and put away';

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: Every note's items, with the focus ring at each stop`, async ({
    page,
    invite,
    authenticator,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    // Setup, which setup.spec.ts proves, is pressed with the mouse; from Today on, every move is a key.
    await signInThroughSetup(page, invite, authenticator);
    await expect(heading(page)).toBeFocused();

    // To Manage, the nav's last item, then Common items, the second of its links.
    await expectStops(page, 'Shift+Tab', ['Manage']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toHaveText('Manage');
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Participants', 'Common items']);
    let read = page.waitForResponse(isListRead);
    await page.keyboard.press('Enter');
    const groupId = everyNoteIn(await groupsIn(read)).id;
    await expect(heading(page)).toHaveText('Common items');
    await expect(heading(page)).toBeFocused();
    await expect(section(page, 'Every note').getByText(emptyLine, { exact: true })).toBeVisible();

    // Every note with no items: its add form alone, after the nav, then the New group form (common-items.md *Focus
    // order*).
    const addForm = ['New item in Every note', 'Add item'];
    await expectStops(page, 'Tab', [...addForm, ...newGroupStops, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [...newGroupStopsBack, 'Add item', 'New item in Every note', 'Manage']);
    await expectStops(page, 'Tab', ['New item in Every note']);

    // Three items, each typed into New item in Every note and added with Enter on Add item, which leaves focus in the
    // field.
    let ids: string[] = [];
    for (const text of [first, second, third]) {
      await page.keyboard.type(text);
      await expectStops(page, 'Tab', ['Add item']);
      const added = page.waitForResponse(isWrite('POST', `/common-item-groups/${groupId}/items`));
      read = page.waitForResponse(isListRead);
      await page.keyboard.press('Enter');
      expect((await added).status()).toBe(201);
      ids = everyNoteIn(await groupsIn(read)).items.map((item) => item.id);
      await expect(newItem(page, 'Every note')).toBeFocused();
      await expect(newItem(page, 'Every note')).toHaveValue('');
    }
    await expect(itemTexts(page, 'Every note')).toHaveText([first, second, third]);

    // Each item row's Move up, Move down, Edit and Archive, the end ones unavailable but there, then the add form.
    const rows = [...rowStops(first), ...rowStops(second), ...rowStops(third)];
    await expectStops(page, 'Shift+Tab', [...[...rows].reverse(), 'Manage']);
    await expectStops(page, 'Tab', [...rows, ...addForm, ...newGroupStops, 'outside the page']);

    // The third's Edit: its row becomes the form, Edit item, Save and Cancel in its place, focus in the box with the
    // caret after the words, so that what is typed goes on the end.
    await expectStops(page, 'Shift+Tab', [
      ...newGroupStopsBack,
      'Add item',
      'New item in Every note',
      ...rowStops(third).slice(2).reverse(),
    ]);
    await page.keyboard.press('Enter');
    await expect(editItem(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Save', 'Cancel', ...addForm]);
    await expectStops(page, 'Shift+Tab', ['New item in Every note', 'Cancel', 'Save', 'Edit item']);
    await page.keyboard.type(' and put away');
    await expectStops(page, 'Tab', ['Save']);
    const saved = page.waitForResponse(isWrite('PUT', `/common-items/${ids[2]}`));
    await page.keyboard.press('Enter');
    expect((await saved).request().postDataJSON()).toEqual({ text: thirdReworded, groupId });
    await expect(itemButton(page, 'Edit', thirdReworded)).toBeFocused();

    // Edit again, then Cancel: the typed words dropped, focus back on Edit.
    await page.keyboard.press('Enter');
    await expect(editItem(page)).toBeFocused();
    await page.keyboard.type(' twice');
    await expectStops(page, 'Tab', ['Save', 'Cancel']);
    await page.keyboard.press('Enter');
    await expect(itemButton(page, 'Edit', thirdReworded)).toBeFocused();
    await expect(itemTexts(page, 'Every note')).toHaveText([first, second, thirdReworded]);

    // The first moved down with Enter on its Move down, where focus stays as the item goes to row 2.
    await expectStops(page, 'Shift+Tab', [
      ...rowStops(thirdReworded).slice(0, 2).reverse(),
      ...rowStops(second).reverse(),
      ...rowStops(first).slice(1).reverse(),
    ]);
    const moved = page.waitForResponse(isWrite('PUT', `/common-item-groups/${groupId}/items/order`));
    await page.keyboard.press('Enter');
    expect((await moved).status()).toBe(204);
    await expect(itemTexts(page, 'Every note')).toHaveText([second, first, thirdReworded]);
    await expect(itemButton(page, 'Move down', first)).toBeFocused();

    // The third archived from its row: Archived items opens, with focus on its Restore, which comes after the summary,
    // last in the group's section while the section is open, before the New group form.
    await expectStops(page, 'Tab', [...rowStops(first).slice(2), ...rowStops(thirdReworded)]);
    const archived = page.waitForResponse(isWrite('POST', `/common-items/${ids[2]}/archive`));
    await page.keyboard.press('Enter');
    expect((await archived).status()).toBe(204);
    await expect(itemButton(page, 'Restore', thirdReworded)).toBeFocused();
    await expect(archivedSection(page, 'Every note')).toHaveAttribute('open', '');
    const stops = [...rowStops(second), ...rowStops(first), ...addForm, summaryStop('Every note')];
    await expectStops(page, 'Tab', [...newGroupStops, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      ...newGroupStopsBack,
      rowName('Restore', thirdReworded),
      ...[...stops].reverse(),
      'Manage',
    ]);

    // Enter on the summary closes it, and Tab then passes Restore by; Enter opens it again.
    await expectStops(page, 'Tab', stops);
    await page.keyboard.press('Enter');
    await expect(archivedSection(page, 'Every note')).not.toHaveAttribute('open');
    await expectStops(page, 'Tab', [...newGroupStops, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [...newGroupStopsBack, summaryStop('Every note')]);
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', [rowName('Restore', thirdReworded)]);

    // Restored with Enter: the item last in the group, focus on its Archive, and Archived items gone.
    const restored = page.waitForResponse(isWrite('POST', `/common-items/${ids[2]}/restore`));
    await page.keyboard.press('Enter');
    expect((await restored).status()).toBe(204);
    await expect(itemTexts(page, 'Every note')).toHaveText([second, first, thirdReworded]);
    await expect(itemButton(page, 'Archive', thirdReworded)).toBeFocused();
    await expect(archivedSection(page, 'Every note')).toHaveCount(0);
    await expectStops(page, 'Tab', [...addForm, ...newGroupStops, 'outside the page']);
    await expectNothingKept(page, first, second, third, thirdReworded);
  });
}

/**
 * Every Tab stop of the page with `threeGroups` served, from the <h1> on, the archived sections closed (common-items.md
 * *Focus order*): each group's buttons, but Every note's, which has none; its item rows; its add form; and its Archived
 * items summary, if it has archived items; then the New group form and the Archived groups summary.
 */
const threeGroupsStops = [
  ...rowStops(medication.text),
  ...rowStops(meal.text),
  ...['New item in Every note', 'Add item', summaryStop('Every note')],
  ...groupStops('Community outing'),
  ...rowStops(busOrTrain.text),
  ...rowStops(paid.text),
  ...rowStops(lunch.text),
  ...['New item in Community outing', 'Add item'],
  ...groupStops('Personal care'),
  ...['New item in Personal care', 'Add item'],
  ...newGroupStops,
  'Archived groups',
];

// A made-up group (A35), added with the keyboard alone, then renamed.
const [respite, respiteRenamed] = ['Respite', 'Respite care'];

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: each group's buttons, a rename, the Group radios and Archived groups, with the `
    + 'focus ring at each stop and on the list conflict', async ({ page }) => {
    await serveMe(page, manager);
    const store = await serveCommonItems(page, threeGroups);
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/manage/common-items');
    await expect(groupHeadings(page)).toHaveText(groupNames);
    await heading(page).focus();

    // Nothing needs a shortcut or a drag (SC 2.1.4, 2.5.7), and nothing is fixed or sticky, which could cover the stop
    // that has focus (SC 2.4.11).
    await expect(page.locator('[accesskey], [aria-keyshortcuts], [draggable="true"]')).toHaveCount(0);
    const pinned = await page.evaluate(() =>
      [...document.querySelectorAll('*')]
        .filter((element) => ['fixed', 'sticky'].includes(getComputedStyle(element).position))
        .map((element) => element.localName),
    );
    expect(pinned, 'fixed or sticky elements').toEqual([]);

    // From the <h1>, every stop in turn, each other group's four after its heading, then out of the page; and back to
    // New group.
    await expectStops(page, 'Tab', [...threeGroupsStops, 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Archived groups', ...newGroupStopsBack]);

    // A group added with Enter in New group, which sends its one-field form: its section last, just above New group,
    // and focus in its New item field.
    await page.keyboard.type(respite);
    const added = page.waitForResponse(isWrite('POST', '/common-item-groups'));
    await page.keyboard.press('Enter');
    const { id: respiteId } = (await (await added).json()) as { id: string };
    await expect(newItem(page, respite)).toBeFocused();
    await expectFocusAt(page, `New item in ${respite}`);
    await expect(groupHeadings(page)).toHaveText([...groupNames, respite]);
    await expect(newGroup(page)).toHaveValue('');
    await expectStops(page, 'Tab', ['Add item', ...newGroupStops, 'Archived groups', 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      'Archived groups',
      ...newGroupStopsBack,
      'Add item',
      `New item in ${respite}`,
      ...groupStops(respite).slice(2).reverse(),
    ]);

    // Rename with Enter: the Group name field, Save and Cancel in the buttons' place, then the group's add form.
    await page.keyboard.press('Enter');
    await expect(groupNameField(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Save', 'Cancel', `New item in ${respite}`]);
    await expectStops(page, 'Shift+Tab', ['Cancel', 'Save', 'Group name']);

    // Cancel with Enter drops what was typed, focus back on Rename; Save, after it once more, renames the group, focus
    // back on Rename under the new name.
    await page.keyboard.type(' care');
    await expectStops(page, 'Tab', ['Save', 'Cancel']);
    await page.keyboard.press('Enter');
    await expect(groupButton(page, 'Rename', respite)).toBeFocused();
    await expectFocusAt(page, groupButtonName('Rename', respite));
    await page.keyboard.press('Enter');
    await page.keyboard.type(' care');
    await expectStops(page, 'Tab', ['Save']);
    await page.keyboard.press('Enter');
    await expect(groupButton(page, 'Rename', respiteRenamed)).toBeFocused();
    await expectFocusAt(page, groupButtonName('Rename', respiteRenamed));
    await expect(groupHeadings(page)).toHaveText([...groupNames, respiteRenamed]);

    // Move down on the last group, unavailable, says so; Move up with Enter puts it before Personal care, focus staying
    // on its Move up as its whole section moves.
    await expectStops(page, 'Shift+Tab', [groupButtonName('Move down', respiteRenamed)]);
    await expect(groupButton(page, 'Move down', respiteRenamed)).toHaveAttribute('aria-disabled', 'true');
    await page.keyboard.press('Enter');
    await expect(pageStatus(page)).toHaveText('Already at the bottom');
    await expectStops(page, 'Shift+Tab', [groupButtonName('Move up', respiteRenamed)]);
    await page.keyboard.press('Enter');
    await expect(groupHeadings(page)).toHaveText(['Every note', 'Community outing', respiteRenamed, 'Personal care']);
    await expect(groupButton(page, 'Move up', respiteRenamed)).toBeFocused();
    await expectFocusAt(page, groupButtonName('Move up', respiteRenamed));
    await expect(pageStatus(page)).toHaveText('Group moved up');

    // Bought lunch's Edit, back past Community outing's add form, with Enter: the Group radios are one stop, at the
    // group chosen, the item's own; the arrows move between them, choosing each; Tab leaves the set for Save, and
    // Shift+Tab comes back to the radio chosen.
    await expectStops(page, 'Shift+Tab', [
      'Add item',
      'New item in Community outing',
      ...rowStops(lunch.text).slice(2).reverse(),
    ]);
    await page.keyboard.press('Enter');
    await expect(editItem(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Community outing']);
    await expectStops(page, 'ArrowDown', [respiteRenamed, 'Personal care']);
    await expectStops(page, 'ArrowUp', [respiteRenamed]);
    await expect(groupRadio(page, respiteRenamed)).toBeChecked();
    await expectStops(page, 'Tab', ['Save', 'Cancel', 'New item in Community outing']);
    await expectStops(page, 'Shift+Tab', ['Cancel', 'Save', respiteRenamed, 'Edit item']);

    // Saved with Enter: the item at the end of the group chosen, focus on its Edit there.
    await expectStops(page, 'Tab', [respiteRenamed, 'Save']);
    const moved = page.waitForResponse(isWrite('PUT', `/common-items/${lunch.id}`));
    await page.keyboard.press('Enter');
    expect((await moved).request().postDataJSON()).toEqual({ text: lunch.text, groupId: respiteId });
    await expect(
      section(page, respiteRenamed).getByRole('button', { name: rowName('Edit', lunch.text), exact: true }),
    ).toBeFocused();
    await expectFocusAt(page, rowName('Edit', lunch.text));
    await expect(itemTexts(page, respiteRenamed)).toHaveText([lunch.text]);

    // The group archived with Enter: Archived groups opens with it first, focus on its Restore, then the older one's,
    // the page's last stops.
    await expectStops(page, 'Shift+Tab', [
      ...rowStops(lunch.text).slice(0, 2).reverse(),
      groupButtonName('Archive', respiteRenamed),
    ]);
    await page.keyboard.press('Enter');
    await expect(groupButton(page, 'Restore', respiteRenamed)).toBeFocused();
    await expectFocusAt(page, groupButtonName('Restore', respiteRenamed));
    await expect(archivedGroups(page)).toHaveAttribute('open', '');
    await expect(archivedGroupNames(page)).toHaveText([respiteRenamed, 'Overnight respite']);
    await expectStops(page, 'Tab', [groupButtonName('Restore', 'Overnight respite'), 'outside the page']);
    await expectStops(page, 'Shift+Tab', [
      groupButtonName('Restore', 'Overnight respite'),
      groupButtonName('Restore', respiteRenamed),
      'Archived groups',
      ...newGroupStopsBack,
    ]);

    // Enter on the summary closes it, and Tab then passes the Restores by; Enter opens it again.
    await expectStops(page, 'Tab', ['Add group', 'Archived groups']);
    await page.keyboard.press('Enter');
    await expect(archivedGroups(page)).not.toHaveAttribute('open');
    await expectStops(page, 'Tab', ['outside the page']);
    await expectStops(page, 'Shift+Tab', ['Archived groups']);
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', [groupButtonName('Restore', respiteRenamed)]);

    // Restored with Enter: its section last among the groups, with its item, focus on its Archive.
    await page.keyboard.press('Enter');
    await expect(groupButton(page, 'Archive', respiteRenamed)).toBeFocused();
    await expectFocusAt(page, groupButtonName('Archive', respiteRenamed));
    await expect(groupHeadings(page)).toHaveText([...groupNames, respiteRenamed]);
    await expect(itemTexts(page, respiteRenamed)).toHaveText([lunch.text]);
    await expect(archivedGroupNames(page)).toHaveText(['Overnight respite']);

    // Someone else archives Personal care, so a Move up with Enter, built from the list as the page last read it, is
    // refused: the list is read again, and the conflict above the groups takes focus, with the ring.
    store.changeElsewhere(groupIn([...threeGroups], 'Personal care').id, { archived: true });
    await expectStops(page, 'Shift+Tab', [...groupStops(respiteRenamed).slice(0, 3)].reverse());
    await page.keyboard.press('Enter');
    await expect(conflictMessage(page)).toBeFocused();
    await expectFocusAt(page, conflictText);
    await expect(groupHeadings(page)).toHaveText(['Every note', 'Community outing', respiteRenamed]);
    await expectNothingKept(page, respite, respiteRenamed, lunch.text, ...groupNames.slice(1));
  });
}

/**
 * Expects the shell's landmarks alone, a banner, a navigation and the <main>, and in it a region for each of `groups`,
 * the active groups, named by its <h2>, in order, but no form landmark, as no form has a name; and no `aria-label` in
 * <main>, so that every name there is the words that show (common-items.md *Accessibility checklist*; SC 2.5.3).
 */
async function expectLandmarks(page: Page, groups: readonly string[]) {
  for (const role of ['banner', 'navigation', 'main'] as const) {
    await expect(page.getByRole(role), role).toHaveCount(1);
  }

  await expect(page.getByRole('form')).toHaveCount(0);
  await expect(main(page).getByRole('region')).toHaveCount(groups.length);
  for (const [index, group] of groups.entries()) {
    await expect(main(page).getByRole('region').nth(index)).toHaveAccessibleName(group);
  }

  await expect(main(page).locator('[aria-label]')).toHaveCount(0);
}

/**
 * Expects a field refused on a press to show it by more than its words and its colour: a 4 px bar down its group and a
 * 3 px border (form-validation.md *Anatomy*; SC 1.4.1).
 */
async function expectErrorDrawn(field: Locator) {
  const group = field.locator('xpath=ancestor::*[@data-invalid][1]');
  await expect(group).toHaveCSS('border-left-style', 'solid');
  await expect(group).toHaveCSS('border-left-width', '4px');
  await expect(field).toHaveCSS('border-top-style', 'solid');
  await expect(field).toHaveCSS('border-top-width', '3px');
}

test('Common items in each of its states passes axe at phone and laptop width', async ({ page }) => {
  await serveMe(page, manager);
  await failCommonItems(page);
  const heldRead = await holdNext(page, listRead);
  await page.goto('/manage/common-items');

  // Loading: the <h1> and the help text at once, then "Loading common items…" from 1 s, and no add form.
  const read = await heldRead();
  await expect(heading(page)).toHaveText('Common items');
  await expect(page).toHaveTitle(title('Common items'));
  await expect(help(page)).toHaveText(helpText);
  await expect(loadLine(page)).toHaveText('Loading common items…');
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expectAxeToPass(page);

  // Load failed: no connection, after the query's one silent retry, with Try again.
  await read.abort('internetdisconnected');
  await expect(loadLine(page)).toHaveText('Common items did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeVisible();
  await expectAtLeast(tryAgain(page), 44, 48);
  await expectAxeToPass(page);

  // Only Every note, as admin bootstrap leaves it: Try again, reached by Tab from the <h1> and pressed with Enter,
  // brings it, empty, and focuses the <h1>.
  await serveCommonItems(page, [everyNote]);
  await heading(page).focus();
  await expectStops(page, 'Tab', ['Try again']);
  await page.keyboard.press('Enter');
  await expect(heading(page)).toBeFocused();
  await expect(groupHeadings(page)).toHaveText(['Every note']);
  await expect(section(page, 'Every note').getByText(everyNoteLine, { exact: true })).toBeVisible();
  await expect(section(page, 'Every note').getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(loadLine(page)).toHaveText('');
  await expectAxeToPass(page);

  // Ready with three groups: Every note first, with its line and no group buttons, then the others in order, the
  // archived one in Archived groups alone; numbers restart in each group; each group's end buttons unavailable, in
  // place and dashed, saying why when pressed; and the archived items and groups closed.
  const store = await serveCommonItems(page, threeGroups);
  await page.goto('/manage/common-items');
  await expect(groupHeadings(page)).toHaveText(['Every note', 'Community outing', 'Personal care']);
  await expect(section(page, 'Every note').getByText(everyNoteLine, { exact: true })).toBeVisible();
  await expect(itemTexts(page, 'Every note')).toHaveText([medication.text, meal.text]);
  await expect(itemTexts(page, 'Community outing')).toHaveText([busOrTrain.text, paid.text, lunch.text]);
  await expect(section(page, 'Personal care').getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(section(page, 'Every note').getByRole('button', { name: groupButtonPattern })).toHaveCount(0);
  for (const end of [
    itemButton(page, 'Move up', medication.text),
    itemButton(page, 'Move down', meal.text),
    itemButton(page, 'Move up', busOrTrain.text),
    itemButton(page, 'Move down', lunch.text),
  ]) {
    await expect(end).toBeVisible();
    await expect(end).toHaveAttribute('aria-disabled', 'true');
    await expect(end).toHaveCSS('border-top-style', 'dashed');
  }
  await itemButton(page, 'Move up', busOrTrain.text).press('Enter');
  await expect(pageStatus(page)).toHaveText('Already at the top');
  await expect(archivedSection(page, 'Every note')).not.toHaveAttribute('open');
  await expectLandmarks(page, groupNames);
  await expectAxeToPass(page);

  // Busy: a move held until Move down shows "Moving…" and axe has passed, the order on screen unchanged meanwhile.
  const heldMove = await holdNext(page, itemWrites.reorder);
  await itemButton(page, 'Move down', busOrTrain.text).click();
  const move = await heldMove();
  await expect(busyButton(page)).toHaveText('Moving…', { useInnerText: true });
  await expect(itemTexts(page, 'Community outing')).toHaveText([busOrTrain.text, paid.text, lunch.text]);
  await expectAxeToPass(page);
  await move.fallback();
  await expect(itemTexts(page, 'Community outing')).toHaveText([paid.text, busOrTrain.text, lunch.text]);
  await expect(itemButton(page, 'Move down', busOrTrain.text)).toBeFocused();
  await expect(pageStatus(page)).toHaveText('Moved down to number 2 of 3');

  // Editing an item: the row's form in its place, its box focused, holding the item's words, with the Group radios,
  // one per active group in order, named item-{id}-group, the item's own group chosen.
  await itemButton(page, 'Edit', paid.text).click();
  await expect(editItem(page)).toBeFocused();
  await expect(editItem(page)).toHaveValue(paid.text);
  await expect(editItem(page)).toHaveId(`item-${paid.id}-text`);
  await expect(groupRadios(page)).toHaveCount(groupNames.length);
  for (const [index, name] of groupNames.entries()) {
    await expect(groupRadios(page).nth(index)).toHaveAccessibleName(name);
    await expect(groupRadios(page).nth(index)).toHaveAttribute('name', `item-${paid.id}-group`);
  }
  await expect(groupRadio(page, 'Community outing')).toBeChecked();
  await expectLandmarks(page, groupNames);
  await expectAxeToPass(page);

  // Field error: whitespace alone, refused on Save above the box, with focus in it, and no "Error: " in the title.
  await editItem(page).fill('   ');
  await saveItem(page).click();
  await expect(fieldError(page, 'Community outing')).toHaveText('Error: Enter the common item');
  await expect(editItem(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(editItem(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Common items'));
  await expectErrorDrawn(editItem(page));
  await expectAxeToPass(page);

  // Edit conflict: someone else rewords the item first, so the Save is stale; the typed words stay, with theirs.
  const theirs = 'Paid for a ticket';
  const typed = 'Paid for a ticket at the counter with cash';
  store.changeElsewhere(paid.id, { text: theirs });
  await editItem(page).fill(typed);
  await expect(fieldError(page, 'Community outing')).toHaveCount(0);
  await saveItem(page).click();
  await expect(editForm(page).getByRole('alert')).toHaveText(
    `Someone else changed this item while you were editing. It now says: ‘${theirs}’. Save to replace it with your `
      + 'wording, or Cancel to keep it.',
  );
  await expect(editItem(page)).toHaveValue(typed);
  await expectAxeToPass(page);
  await cancelItem(page).click();
  await expect(itemButton(page, 'Edit', theirs)).toBeFocused();

  // Request error: an Archive with no connection says so in its row, with focus kept on Archive.
  await page.route(itemWrites.archive, (route) => route.abort('internetdisconnected'), { times: 1 });
  await itemButton(page, 'Archive', lunch.text).click();
  await expect(itemRow(page, 'Community outing', lunch.text).getByRole('alert')).toHaveText(
    'Not archived: no connection. Try again.',
  );
  await expect(itemButton(page, 'Archive', lunch.text)).toBeFocused();
  await expectAxeToPass(page);

  // List conflict: someone else archives the group's last item, so a move built from the list as the page last read
  // it is refused; the list is read again, with the message above the groups, focused.
  store.changeElsewhere(lunch.id, { archived: true });
  await itemButton(page, 'Move down', busOrTrain.text).click();
  await expect(conflictMessage(page)).toBeFocused();
  await expect(itemTexts(page, 'Community outing')).toHaveText([theirs, busOrTrain.text]);
  await expectAxeToPass(page);

  // Archived items open: Archive puts the item first in its group's section, opening it, with focus on its Restore;
  // the list conflict goes, and Every note's section, which nobody opened, stays closed.
  await itemButton(page, 'Archive', busOrTrain.text).click();
  await expect(itemButton(page, 'Restore', busOrTrain.text)).toBeFocused();
  await expect(archivedSection(page, 'Community outing')).toHaveAttribute('open', '');
  await expect(archivedTexts(page, 'Community outing')).toHaveText([busOrTrain.text, lunch.text]);
  await expect(itemTexts(page, 'Community outing')).toHaveText([theirs]);
  await expect(conflictMessage(page)).toHaveCount(0);
  await expect(pageStatus(page)).toHaveText('Item archived');
  await expect(archivedSection(page, 'Every note')).not.toHaveAttribute('open');
  await expectAxeToPass(page);

  // Field error on Add: Personal care's, with nothing typed.
  await addItem(page, 'Personal care').click();
  await expect(fieldError(page, 'Personal care')).toHaveText('Error: Enter the common item');
  await expect(newItem(page, 'Personal care')).toHaveAttribute('aria-invalid', 'true');
  await expect(newItem(page, 'Personal care')).toBeFocused();
  await expectAxeToPass(page);

  // Renaming a group: the Group name field under the group's heading, which stays, so that its section keeps its name,
  // holding the name, focused; then its field error, a name of spaces alone refused on Save.
  await groupButton(page, 'Rename', 'Personal care').click();
  await expect(section(page, 'Personal care').getByRole('textbox', { name: 'Group name', exact: true })).toBeFocused();
  await expect(groupNameField(page)).toHaveValue('Personal care');
  await expect(groupNameField(page)).toHaveId(`group-${groupIn([...threeGroups], 'Personal care').id}-name`);
  await expectLandmarks(page, groupNames);
  await expectAxeToPass(page);
  await groupNameField(page).fill('   ');
  await saveRename(page).click();
  await expect(renameForm(page).locator('[id$="-error"]')).toHaveText('Error: Enter the group name');
  await expect(groupNameField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(groupNameField(page)).toBeFocused();
  await expectErrorDrawn(groupNameField(page));
  await expectAxeToPass(page);
  await cancelRename(page).click();
  await expect(groupButton(page, 'Rename', 'Personal care')).toBeFocused();

  // Archived groups open: a group's Archive, with no dialog, opens it with the group first, newest archived first,
  // focus on its Restore.
  await expect(archivedGroups(page)).not.toHaveAttribute('open');
  await groupButton(page, 'Archive', 'Personal care').click();
  await expect(groupButton(page, 'Restore', 'Personal care')).toBeFocused();
  await expect(archivedGroups(page)).toHaveAttribute('open', '');
  await expect(archivedGroupNames(page)).toHaveText(['Personal care', 'Overnight respite']);
  await expect(groupHeadings(page)).toHaveText(['Every note', 'Community outing']);
  await expect(pageStatus(page)).toHaveText('Group archived');
  await expectLandmarks(page, ['Every note', 'Community outing']);
  await expectAxeToPass(page);
  await expectNothingKept(
    page,
    medication.text,
    meal.text,
    busOrTrain.text,
    paid.text,
    lunch.text,
    theirs,
    typed,
    ...groupNames.slice(1),
  );
});

// Each active item row of the three groups served, by group.
const activeRows = [
  { group: 'Every note', text: medication.text },
  { group: 'Every note', text: meal.text },
  { group: 'Community outing', text: busOrTrain.text },
  { group: 'Community outing', text: paid.text },
  { group: 'Community outing', text: lunch.text },
];
const groupNames = ['Every note', 'Community outing', 'Personal care'];
// Each item text of the three groups served, active and archived, none of which the page should keep.
const shownTexts = [...activeRows.map(({ text }) => text), laundry.text, bed.text];

type Box = Awaited<ReturnType<typeof boxOf>>;

/** Whether two boxes on the page overlap. */
const overlap = (a: Box, b: Box) =>
  a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;

/**
 * The text and buttons in `row`, an item's row or a group's heading row, whose content does not fit in them, by their
 * text, which none should be.
 */
const clippedIn = (row: Locator) =>
  row.evaluate((element) =>
    [...element.querySelectorAll('p, button')]
      .filter((each) => each.scrollWidth > each.clientWidth || each.scrollHeight > each.clientHeight)
      .map((each) => each.textContent),
  );

test('item rows: two lines of label-width buttons at 375 px, beside the text on the 60rem page at 1280 px, and under '
  + 'it at 200% with no overlap or clipping', async ({ page }) => {
  await serveMe(page, manager);
  await serveCommonItems(page, threeGroups);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/manage/common-items');
  await expect(itemTexts(page, 'Community outing')).toHaveText([busOrTrain.text, paid.text, lunch.text]);
  const labels = ['Move up', 'Move down', 'Edit', 'Archive'] as const;
  const boxesOf = (text: string) => Promise.all(labels.map((label) => boxOf(itemButton(page, label, text))));
  const textOf = (group: string, text: string) => boxOf(itemRow(page, group, text).locator('p').first());

  // On a phone, Move up and Move down share a line under the text, and Edit and Archive the next (common-items.md
  // *Layout - phone*), each label width, at least 44 by 48 px, and 8 px apart.
  for (const { group, text } of activeRows) {
    const words = await textOf(group, text);
    const [up, down, edit, archive] = await boxesOf(text);
    expect(up.y, `Move up under "${text}"`).toBeGreaterThanOrEqual(words.y + words.height);
    expect(down.y, `Move down beside Move up, "${text}"`).toBeCloseTo(up.y, 0);
    expect(archive.y, `Archive beside Edit, "${text}"`).toBeCloseTo(edit.y, 0);
    expect(edit.y, `Edit on the line after Move up, "${text}"`).toBeGreaterThanOrEqual(up.y + up.height);
    expectApart([up, down, edit, archive], `the buttons of "${text}"`);
  }
  await expectLabelWidth(main(page).locator('ol button'));
  await archivedSummary(page, 'Every note').click();
  await expectAtLeast(archivedSummary(page, 'Every note'), 44, 44);
  await expectLabelWidth(archivedSection(page, 'Every note').getByRole('button'));
  await itemButton(page, 'Edit', paid.text).click();
  await expectLabelWidth(editForm(page).getByRole('button'));
  expect((await boxOf(cancelItem(page))).y, 'Cancel beside Save').toBeCloseTo((await boxOf(saveItem(page))).y, 0);
  expectApart(await Promise.all([saveItem(page), cancelItem(page)].map(boxOf)), 'Save and Cancel');
  // Add item keeps the shared button's rule: as wide as the field on a phone, in each group.
  for (const group of groupNames) {
    expect((await boxOf(addItem(page, group))).width, group).toBeCloseTo((await boxOf(newItem(page, group))).width, 0);
    await expectAtLeast(addItem(page, group), 48, 48);
  }
  await cancelItem(page).click();

  // On a laptop, on the 60rem page, the four sit at the top of each row, after the text, on its line, 8 px apart; Add
  // item is its label's width; and the help text and the fields stay in the 40rem reading column while the lists use
  // the page.
  await page.setViewportSize({ width: 1280, height: 800 });
  const measure = 40 * 16;
  expect((await boxOf(main(page))).width, 'the page').toBeLessThanOrEqual(60 * 16);
  for (const { group, text } of activeRows) {
    const words = await textOf(group, text);
    const boxes = await boxesOf(text);
    for (const [index, button] of boxes.entries()) {
      expect(button.y, `${labels[index]} on the line of "${text}"`).toBeCloseTo(words.y, 0);
      expect(button.x, `${labels[index]} after "${text}"`).toBeGreaterThanOrEqual(words.x + words.width);
    }
    expectApart(boxes, `the buttons of "${text}"`);
  }
  await expectLabelWidth(main(page).locator('section ol button, section details button'));
  expect((await boxOf(help(page))).width, 'the help text').toBeLessThanOrEqual(measure);
  for (const group of groupNames) {
    const field = await boxOf(newItem(page, group));
    expect(field.width, `New item in ${group}`).toBeLessThanOrEqual(measure);
    expect((await boxOf(addItem(page, group))).width, `Add item in ${group}`).toBeLessThan(field.width);
  }
  expect((await boxOf(section(page, 'Community outing').locator('ol'))).width).toBeGreaterThan(measure);
  await itemButton(page, 'Edit', paid.text).click();
  expect((await boxOf(editItem(page))).width, 'Edit item').toBeLessThanOrEqual(measure);
  await cancelItem(page).click();

  // At 200% text, the text keeps its 16rem, so the four drop under it by themselves, none over another or cut off.
  await setTextTo200Percent(page);
  for (const { group, text } of activeRows) {
    const row = itemRow(page, group, text);
    const [words, edge] = await Promise.all([textOf(group, text), boxOf(row)]);
    const buttons = await boxesOf(text);
    for (const [index, button] of buttons.entries()) {
      expect(button.y, `${labels[index]} under "${text}"`).toBeGreaterThanOrEqual(words.y + words.height);
      expect(button.x + button.width, `${labels[index]} inside its row`).toBeLessThanOrEqual(edge.x + edge.width);
      for (const other of buttons.slice(index + 1)) {
        expect(overlap(button, other), `${labels[index]} over another button of "${text}"`).toBe(false);
      }
    }
    expect(await clippedIn(row), `cut off in "${text}"`).toEqual([]);
  }
  await expectNothingKept(page, ...shownTexts);
});

/**
 * Expects at least 8 px between each of `boxes` and the one before it, beside it or on the line under it (A32;
 * SC 2.5.8, 2.5.5).
 */
function expectApart(boxes: readonly Box[], what: string) {
  for (const [index, box] of boxes.slice(1).entries()) {
    const before = boxes[index];
    const gap = Math.max(box.x - (before.x + before.width), box.y - (before.y + before.height));
    expect(Math.round(gap), `the space between ${what} ${index + 1} and ${index + 2}`).toBeGreaterThanOrEqual(8);
  }
}

test('group heading rows: two lines of label-width buttons at 375 px, beside the name at 1280 px and under it at 200% '
  + 'with no overlap or clipping; 48 px name fields no wider than 40rem; 56 px radio rows', async ({ page }) => {
  await serveMe(page, manager);
  await serveCommonItems(page, threeGroups);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/manage/common-items');
  await expect(groupHeadings(page)).toHaveText(groupNames);
  const others = groupNames.slice(1);
  const labels = ['Move up', 'Move down', 'Rename', 'Archive'] as const;
  const boxesOf = (group: string) => Promise.all(labels.map((label) => boxOf(groupButton(page, label, group))));
  const nameOf = (group: string) => boxOf(section(page, group).getByRole('heading', { level: 2 }));
  const radioRowBoxes = async () => Promise.all((await radioRows(page).all()).map(boxOf));

  // On a phone, each group's Move up and Move down share a line under its name, and Rename and Archive the next
  // (common-items.md *Layout - phone*), each label width, at least 44 by 48 px, and 8 px apart; Add group label width,
  // where Add item keeps the shared full width; and the New group field 48 px tall.
  for (const group of others) {
    const name = await nameOf(group);
    const [up, down, rename, archive] = await boxesOf(group);
    expect(up.y, `Move up under ${group}`).toBeGreaterThanOrEqual(name.y + name.height);
    expect(down.y, `Move down beside Move up, ${group}`).toBeCloseTo(up.y, 0);
    expect(archive.y, `Archive beside Rename, ${group}`).toBeCloseTo(rename.y, 0);
    expect(rename.y, `Rename on the line after Move up, ${group}`).toBeGreaterThan(up.y + up.height);
    expectApart([up, down, rename, archive], `${group}'s buttons`);
  }
  await expectLabelWidth(groupButtons(page));
  await expectLabelWidth(addGroup(page));
  expect((await boxOf(newGroup(page))).height, 'New group').toBeCloseTo(48, 0);

  // Archived groups' summary at least 44 px tall and, open, each Restore label width.
  await archivedGroupsSummary(page).click();
  await expectAtLeast(archivedGroupsSummary(page), 44, 44);
  await expectLabelWidth(archivedGroups(page).getByRole('button'));

  // Renaming: the Group name field 48 px tall, Save and Cancel label width, 8 px apart.
  await groupButton(page, 'Rename', 'Community outing').click();
  expect((await boxOf(groupNameField(page))).height, 'Group name').toBeCloseTo(48, 0);
  await expectLabelWidth(renameForm(page).getByRole('button'));
  expectApart(await Promise.all([saveRename(page), cancelRename(page)].map(boxOf)), 'Save and Cancel');
  await cancelRename(page).click();

  // Editing an item: each Group radio's row, which is all its label, at least 56 px tall, and 8 px apart.
  await itemButton(page, 'Edit', paid.text).click();
  await expectAtLeast(radioRows(page), 44, 56);
  expectApart(await radioRowBoxes(), 'the radio rows');
  await cancelItem(page).click();

  // On a laptop, each group's name and its four buttons on one line, the buttons after the name, at its top, 8 px
  // apart; still label width; and the New group and Group name fields in the 40rem reading column.
  await page.setViewportSize({ width: 1280, height: 800 });
  const measure = 40 * 16;
  for (const group of others) {
    const name = await nameOf(group);
    const boxes = await boxesOf(group);
    for (const [index, button] of boxes.entries()) {
      expect(button.y, `${labels[index]} on the line of ${group}`).toBeCloseTo(name.y, 0);
      expect(button.x, `${labels[index]} after ${group}`).toBeGreaterThanOrEqual(name.x + name.width);
    }
    expectApart(boxes, `${group}'s buttons`);
  }
  await expectLabelWidth(groupButtons(page));
  await expectLabelWidth(addGroup(page));
  expect((await boxOf(newGroup(page))).width, 'New group').toBeLessThanOrEqual(measure);
  await groupButton(page, 'Rename', 'Community outing').click();
  expect((await boxOf(groupNameField(page))).width, 'Group name').toBeLessThanOrEqual(measure);
  await cancelRename(page).click();
  await itemButton(page, 'Edit', paid.text).click();
  expectApart(await radioRowBoxes(), 'the radio rows');
  await cancelItem(page).click();

  // At 200% text, the name keeps its 16rem, so the four drop under it by themselves, none over another or cut off.
  await setTextTo200Percent(page);
  for (const group of others) {
    const [name, edge] = await Promise.all([nameOf(group), boxOf(groupHead(page, group))]);
    const buttons = await boxesOf(group);
    for (const [index, button] of buttons.entries()) {
      expect(button.y, `${labels[index]} under ${group}`).toBeGreaterThanOrEqual(name.y + name.height);
      expect(button.x + button.width, `${labels[index]} inside its row`).toBeLessThanOrEqual(edge.x + edge.width);
      for (const other of buttons.slice(index + 1)) {
        expect(overlap(button, other), `${labels[index]} over another button of ${group}`).toBe(false);
      }
    }
    expect(await clippedIn(groupHead(page, group)), `cut off in ${group}'s heading row`).toEqual([]);
  }
  await expectNothingKept(page, ...others, 'Overnight respite', ...shownTexts);
});

test('at 320 px with text at 200%, no Common items state scrolls sideways', async ({ page }) => {
  const word = 'Wolfeschlegelsteinhausenbergerdorff';
  const long = madeUpItem('9cc4f018-adb5-4b09-9ebe-6a8cafc13f29', word.repeat(5), 0);
  const longGroup = madeUpGroup('4c6e8a0d-5f7b-4c93-8ebf-3a4b5c6d7e8f', `Outings to ${word}`, 3, [long]);
  await serveMe(page, manager);
  const store = await serveCommonItems(page, [...threeGroups, longGroup]);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/manage/common-items');
  await setTextTo200Percent(page);
  await expect(itemTexts(page, longGroup.name)).toHaveText([long.text]);
  expect(await scrollsSideways(page), 'a long group name, and an item a word of 175 letters').toBe(false);

  await itemButton(page, 'Edit', long.text).click();
  await expect(groupRadio(page, longGroup.name)).toBeChecked();
  expect(await scrollsSideways(page), 'its edit form, with a Group radio of the long name').toBe(false);
  await cancelItem(page).click();

  await groupButton(page, 'Rename', longGroup.name).click();
  await expect(groupNameField(page)).toHaveValue(longGroup.name);
  expect(await scrollsSideways(page), 'the long group renamed').toBe(false);
  await cancelRename(page).click();

  await newGroup(page).fill('a'.repeat(250));
  await addGroup(page).click();
  await expect(newGroupForm(page).locator('[id$="-error"]')).toHaveText(
    'Error: Group name must be 200 characters or less',
  );
  expect(await scrollsSideways(page), 'the New group form with 250 characters, refused').toBe(false);
  await newGroup(page).fill('');

  await page.route(groupWrites.archive, (route) => route.abort('internetdisconnected'), { times: 1 });
  await groupButton(page, 'Archive', longGroup.name).click();
  await expect(groupHead(page, longGroup.name).getByRole('alert')).toHaveText(
    'Not archived: no connection. Try again.',
  );
  expect(await scrollsSideways(page), 'the long group not archived').toBe(false);

  await newItem(page, 'Every note').fill('a'.repeat(250));
  await addItem(page, 'Every note').click();
  await expect(fieldError(page, 'Every note')).toHaveText('Error: Common item must be 200 characters or less');
  expect(await scrollsSideways(page), 'the add form with 250 characters, refused').toBe(false);
  await newItem(page, 'Every note').fill('');

  await page.route(itemWrites.archive, (route) => route.abort('internetdisconnected'), { times: 1 });
  await itemButton(page, 'Archive', paid.text).click();
  await expect(itemRow(page, 'Community outing', paid.text).getByRole('alert')).toHaveText(
    'Not archived: no connection. Try again.',
  );
  expect(await scrollsSideways(page), 'an item not archived').toBe(false);

  store.changeElsewhere(lunch.id, { archived: true });
  await itemButton(page, 'Move down', paid.text).click();
  await expect(conflictMessage(page)).toBeFocused();
  expect(await scrollsSideways(page), 'the list conflict').toBe(false);

  await itemButton(page, 'Archive', long.text).click();
  await expect(itemButton(page, 'Restore', long.text)).toBeFocused();
  expect(await scrollsSideways(page), 'the archived items open').toBe(false);

  await groupButton(page, 'Archive', longGroup.name).click();
  await expect(groupButton(page, 'Restore', longGroup.name)).toBeFocused();
  await expect(archivedGroupNames(page)).toHaveText([longGroup.name, 'Overnight respite']);
  expect(await scrollsSideways(page), 'Archived groups open, with the long name').toBe(false);
  await expectNothingKept(page, long.text, longGroup.name, paid.text, lunch.text, 'a'.repeat(20));
});

test('in forced colours, the focus ring shows in Highlight at each stop, the radios and the list conflict included, '
  + 'unavailable buttons are dashed in GrayText, and summary markers, radio dots and row dividers are drawn', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, manager);
  const store = await serveCommonItems(page, threeGroups);
  await page.goto('/manage/common-items');
  await expect(groupHeadings(page)).toHaveCount(3);
  await heading(page).focus();
  // The colours that forced colours give Highlight, which base.css's ring takes there, GrayText, which an unavailable
  // button takes, CanvasText, which borders take, and Canvas, the background, each unlike the ring's own colour and
  // the others.
  const [highlight, grayText, canvasText, canvas] = await Promise.all(
    ['Highlight', 'GrayText', 'CanvasText', 'Canvas'].map((keyword) => systemColour(page, keyword)),
  );
  expect(new Set([highlight, grayText, canvasText, canvas, ring.colour]).size).toBe(5);
  const forcedRing = { ...ring, colour: highlight };

  // Each group's stops in turn (common-items.md *Focus order*): its group buttons but on Every note, its item rows, its
  // add form, then its Archived items; then the New group form and the Archived groups summary, closed.
  const outingRows = [...rowStops(busOrTrain.text), ...rowStops(paid.text), ...rowStops(lunch.text)];
  await expectStops(page, 'Tab', [...threeGroupsStops, 'outside the page'], forcedRing);

  // Every note's Archived items and Archived groups, closed, then the first opened with Enter, each marker drawn either
  // way, in the system colour that forced colours give the summary's words, not the background's; and each Restore,
  // newest archived first, after it.
  const summary = archivedSummary(page, 'Every note');
  const expectMarkerDrawn = async (drawn: Locator, shape: 'disclosure-closed' | 'disclosure-open') => {
    await expect(drawn).toHaveCSS('display', 'list-item');
    await expect(drawn).toHaveCSS('list-style-type', shape);
    const [marker, words] = await drawn.evaluate((element) => [
      getComputedStyle(element, '::marker').color,
      getComputedStyle(element).color,
    ]);
    expect(marker, 'the marker in the colour of the summary').toBe(words);
    expect(marker, 'the marker unlike the background').not.toBe(canvas);
  };
  await expectMarkerDrawn(archivedGroupsSummary(page), 'disclosure-closed');
  await expectMarkerDrawn(summary, 'disclosure-closed');
  await expectStops(
    page,
    'Shift+Tab',
    [
      'Archived groups',
      ...newGroupStopsBack,
      ...['Add item', 'New item in Personal care'],
      ...groupStops('Personal care').reverse(),
      ...['Add item', 'New item in Community outing'],
      ...[...outingRows].reverse(),
      ...groupStops('Community outing').reverse(),
      summaryStop('Every note'),
    ],
    forcedRing,
  );
  await page.keyboard.press('Enter');
  await expectMarkerDrawn(summary, 'disclosure-open');
  await expectStops(page, 'Tab', [rowName('Restore', bed.text), rowName('Restore', laundry.text)], forcedRing);

  // Each group's end item buttons, and the end group buttons, unavailable: GrayText words and dashed GrayText borders,
  // where the others keep theirs (list-editor.md *States*).
  for (const end of [
    itemButton(page, 'Move up', medication.text),
    itemButton(page, 'Move down', meal.text),
    itemButton(page, 'Move up', busOrTrain.text),
    itemButton(page, 'Move down', lunch.text),
    groupButton(page, 'Move up', 'Community outing'),
    groupButton(page, 'Move down', 'Personal care'),
  ]) {
    await expect(end).toHaveCSS('color', grayText);
    await expect(end).toHaveCSS('border-top-color', grayText);
    await expect(end).toHaveCSS('border-top-style', 'dashed');
  }
  for (const available of [
    itemButton(page, 'Move down', medication.text),
    groupButton(page, 'Move down', 'Community outing'),
  ]) {
    await expect(available).not.toHaveCSS('color', grayText);
    await expect(available).toHaveCSS('border-top-style', 'solid');
  }

  // The rule under each row, active or archived, in CanvasText.
  const everyNoteRows = section(page, 'Every note').locator('ol > li, details li');
  await expect(everyNoteRows).toHaveCount(4);
  for (const row of [...(await everyNoteRows.all()), ...(await itemRows(page, 'Community outing').all())]) {
    await expect(row).toHaveCSS('border-bottom-style', 'solid');
    await expect(row).toHaveCSS('border-bottom-width', '1px');
    await expect(row).toHaveCSS('border-bottom-color', canvasText);
  }

  // An item's Group radios, from its edit box: one Tab stop, at the radio chosen, and an arrow moving the choice, each
  // with the ring in Highlight; every circle drawn, and the dot of the chosen one alone, both borders, which forced
  // colours keep, in a system colour unlike the background (ChoiceRow.module.css).
  await itemButton(page, 'Edit', paid.text).click();
  await expect(editItem(page)).toBeFocused();
  await expectStops(page, 'Tab', ['Community outing'], forcedRing);
  await expectStops(page, 'ArrowDown', ['Personal care'], forcedRing);
  for (const radio of await groupRadios(page).all()) {
    const drawn = await radio.evaluate((element: HTMLInputElement) => {
      const [circle, dot] = [getComputedStyle(element), getComputedStyle(element, '::before')];
      return {
        circle: [circle.borderTopStyle, circle.borderTopColor],
        dot: [dot.visibility, dot.borderTopStyle, dot.borderTopColor],
        checked: element.checked,
      };
    });
    const name = (await radio.getAttribute('id')) ?? '';
    expect(drawn.circle[0], `the circle of ${name}`).toBe('solid');
    expect(drawn.circle[1], `the circle of ${name}`).not.toBe(canvas);
    expect(drawn.dot.slice(0, 2), `the dot of ${name}`).toEqual([drawn.checked ? 'visible' : 'hidden', 'solid']);
    expect(drawn.dot[2], `the dot of ${name}`).not.toBe(canvas);
  }
  await expect(groupRadio(page, 'Personal care')).toBeChecked();

  // The list conflict, after a move with Enter that someone else's archive made stale, takes focus with the ring in
  // Highlight.
  await expectStops(page, 'Tab', ['Save', 'Cancel'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(itemButton(page, 'Edit', paid.text)).toBeFocused();
  store.changeElsewhere(lunch.id, { archived: true });
  await expectStops(page, 'Shift+Tab', [rowName('Move down', paid.text)], forcedRing);
  await page.keyboard.press('Enter');
  await expect(conflictMessage(page)).toBeFocused();
  await expectFocusAt(page, conflictText, forcedRing);
  await expectNothingKept(page, ...shownTexts);
});

test('text is 7:1 against its background, and button borders, radio circles and the focus ring are 3:1', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveCommonItems(page, threeGroups);
  await page.goto('/manage/common-items');
  await expect(groupHeadings(page)).toHaveText(groupNames);

  // Every text at 7:1, beyond what axe's AA rules check (common-items.md *Accessibility checklist*, *Visual*), by axe's
  // AAA rule, which holds large text, such as a heading, to 4.5:1: ready, with the archived sections open, and in an
  // item's edit form, with its Group radios, then its field error.
  await expectTextAt7To1(page, 'ready');
  await archivedSummary(page, 'Every note').click();
  await archivedGroupsSummary(page).click();
  await expectTextAt7To1(page, 'with the archived sections open');
  await itemButton(page, 'Edit', meal.text).click();
  await expectTextAt7To1(page, 'in an edit form');
  await editItem(page).fill('');
  await saveItem(page).click();
  await expect(editItem(page)).toHaveAttribute('aria-invalid', 'true');
  await expectTextAt7To1(page, 'with a field error');
  await cancelItem(page).click();

  // The help text, Every note's line, each group's name, which axe holds to 4.5:1 alone, and the unavailable labels, on
  // their grey, each at 7:1.
  const texts: [string, Locator][] = [
    ['the help text', help(page)],
    ["Every note's line", section(page, 'Every note').getByText(everyNoteLine, { exact: true })],
    ...groupNames.map((name): [string, Locator] => [name, section(page, name).getByRole('heading', { level: 2 })]),
    ["Community outing's Move up, unavailable", groupButton(page, 'Move up', 'Community outing')],
    ["Personal care's Move down, unavailable", groupButton(page, 'Move down', 'Personal care')],
    [`${medication.text}'s Move up, unavailable`, itemButton(page, 'Move up', medication.text)],
  ];
  for (const [what, text] of texts) {
    expect(await contrastOf(text, 'color'), what).toBeGreaterThanOrEqual(7);
  }

  // Each group button's border, dashed or not, and Add group's, against the page, at 3:1.
  for (const button of [...(await groupButtons(page).all()), addGroup(page)]) {
    const name = await button.innerText();
    expect(await contrastOf(button, 'border-top-color'), `the border of ${name}`).toBeGreaterThanOrEqual(3);
  }

  // An item's Group radios: each label at 7:1, and each circle at 3:1 against its row.
  await itemButton(page, 'Edit', paid.text).click();
  for (const row of await radioRows(page).all()) {
    const name = await row.innerText();
    expect(await contrastOf(row.locator('span'), 'color'), `the label of ${name}`).toBeGreaterThanOrEqual(7);
    const circle = await contrastOf(row.getByRole('radio'), 'border-top-color');
    expect(circle, `the circle of ${name}`).toBeGreaterThanOrEqual(3);
  }

  // The focus ring, reached by Tab, on the radio chosen and on a group's button, at 3:1 against the page.
  await expectStops(page, 'Tab', ['Community outing']);
  expect(await contrastOf(groupRadio(page, 'Community outing'), 'outline-color'), 'the ring').toBeGreaterThanOrEqual(3);
  await expectStops(page, 'Tab', ['Save', 'Cancel']);
  await page.keyboard.press('Enter');
  await expectStops(page, 'Shift+Tab', [
    ...rowStops(paid.text).slice(0, 2).reverse(),
    ...rowStops(busOrTrain.text).reverse(),
    groupButtonName('Archive', 'Community outing'),
  ]);
  expect(await contrastOf(groupButton(page, 'Archive', 'Community outing'), 'outline-color')).toBeGreaterThanOrEqual(3);
  await expectNothingKept(page, ...shownTexts, ...groupNames.slice(1));
});
