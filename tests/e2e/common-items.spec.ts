import type { Locator, Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import {
  bed,
  busOrTrain,
  everyNote,
  failCommonItems,
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
import { expectStops, ring } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import {
  boxOf,
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

// Common items in the published app (S01.03.01 to S01.03.04), against common-items.md's criteria for what T06 builds
// (tasks.md, T06's Notes): the Every note group and each other active group's items, with their add forms and
// archived items. Three tests sign in for real, through setup with a passkey on the virtual authenticator, as the
// manager whom admin bootstrap invited with the organisation, and so with its Every note group, empty, and add,
// reword, move, archive and restore its items on the app's own answers; the others answer /api/auth/me, the groups
// list and each item write in the browser with made-up groups and items (me.ts, commonItems.ts), so that none of
// those requests reaches the app.

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
/** An archived items summary's focus stop: its words, then " in [group name]", hidden but for screen readers. */
const summaryStop = (group: string) => `Archived items in ${group}`;
const newItem = (page: Page, group: string) =>
  section(page, group).getByRole('textbox', { name: `New item in ${group}`, exact: true });
const addItem = (page: Page, group: string) =>
  section(page, group).getByRole('button', { name: 'Add item', exact: true });
const editItem = (page: Page) => main(page).getByRole('textbox', { name: 'Edit item', exact: true });
const editForm = (page: Page) =>
  main(page).locator('form').filter({ has: page.getByRole('textbox', { name: 'Edit item', exact: true }) });
const saveItem = (page: Page) => editForm(page).getByRole('button', { name: 'Save', exact: true });
const cancelItem = (page: Page) => editForm(page).getByRole('button', { name: 'Cancel', exact: true });
// A field's message above its box, with its hidden "Error: " (form-validation.md).
const fieldError = (page: Page, group: string) => section(page, group).locator('[id$="-error"]');
const conflictMessage = (page: Page) =>
  main(page).getByText('Someone else changed this list just now. Check it, then try again.', { exact: true });
// The page's one status region, visually hidden, where each item change says what it did (common-items.md component 9).
const pageStatus = (page: Page) => main(page).locator('p.visually-hidden[role="status"]');
// A button busy for 400 ms, which then shows its busy label, and is named by it, in place of the one it rests with.
const busyButton = (page: Page) => main(page).locator('button[data-busy]');

/** Whether the response answers the groups list, Common items' one read. */
const isListRead = (response: Response) =>
  response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/admin/common-item-groups';

/** Whether the response answers an item write by `method` to the path after `/api/admin` (design.md §6.6). */
const isItemWrite = (method: 'POST' | 'PUT', path: string) => (response: Response) =>
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
    const posted = page.waitForResponse(isItemWrite('POST', `/common-item-groups/${groupId}/items`));
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

  // The second reworded in its row: Edit focuses the box with the caret after the words; Save sends the row's version
  // as If-Match and gets the app's 200, with no body and no ETag; focus goes back to Edit.
  const reworded = 'Meal prepared with support';
  await itemButton(page, 'Edit', fiveItems[1]).click();
  await expect(editItem(page)).toBeFocused();
  await expect(editItem(page)).toHaveValue(fiveItems[1]);
  expect(await editItem(page).evaluate((box: HTMLTextAreaElement) => [box.selectionStart, box.selectionEnd])).toEqual(
    [fiveItems[1].length, fiveItems[1].length],
  );
  await editItem(page).fill(reworded);
  const put = page.waitForResponse(isItemWrite('PUT', `/common-items/${ids[1]}`));
  read = page.waitForResponse(isListRead);
  await saveItem(page).click();
  const putAnswer = await put;
  expect(putAnswer.status()).toBe(200);
  expect(putAnswer.request().postDataJSON()).toEqual({ text: reworded });
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
  const reordered = page.waitForResponse(isItemWrite('PUT', `/common-item-groups/${groupId}/items/order`));
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
  const archived = page.waitForResponse(isItemWrite('POST', `/common-items/${ids[2]}/archive`));
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
  const restored = page.waitForResponse(isItemWrite('POST', `/common-items/${ids[2]}/restore`));
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

    // Every note with no items: its add form alone, after the nav (common-items.md *Focus order*).
    const addForm = ['New item in Every note', 'Add item'];
    await expectStops(page, 'Tab', [...addForm, 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Add item', 'New item in Every note', 'Manage']);
    await expectStops(page, 'Tab', ['New item in Every note']);

    // Three items, each typed into New item in Every note and added with Enter on Add item, which leaves focus in the
    // field.
    let ids: string[] = [];
    for (const text of [first, second, third]) {
      await page.keyboard.type(text);
      await expectStops(page, 'Tab', ['Add item']);
      const added = page.waitForResponse(isItemWrite('POST', `/common-item-groups/${groupId}/items`));
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
    await expectStops(page, 'Tab', [...rows, ...addForm, 'outside the page']);

    // The third's Edit: its row becomes the form, Edit item, Save and Cancel in its place, focus in the box with the
    // caret after the words, so that what is typed goes on the end.
    await expectStops(page, 'Shift+Tab', ['Add item', 'New item in Every note', ...rowStops(third).slice(2).reverse()]);
    await page.keyboard.press('Enter');
    await expect(editItem(page)).toBeFocused();
    await expectStops(page, 'Tab', ['Save', 'Cancel', ...addForm]);
    await expectStops(page, 'Shift+Tab', ['New item in Every note', 'Cancel', 'Save', 'Edit item']);
    await page.keyboard.type(' and put away');
    await expectStops(page, 'Tab', ['Save']);
    const saved = page.waitForResponse(isItemWrite('PUT', `/common-items/${ids[2]}`));
    await page.keyboard.press('Enter');
    expect((await saved).request().postDataJSON()).toEqual({ text: thirdReworded });
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
    const moved = page.waitForResponse(isItemWrite('PUT', `/common-item-groups/${groupId}/items/order`));
    await page.keyboard.press('Enter');
    expect((await moved).status()).toBe(204);
    await expect(itemTexts(page, 'Every note')).toHaveText([second, first, thirdReworded]);
    await expect(itemButton(page, 'Move down', first)).toBeFocused();

    // The third archived from its row: Archived items opens, with focus on its Restore, which comes after the summary,
    // last on the page while the section is open.
    await expectStops(page, 'Tab', [...rowStops(first).slice(2), ...rowStops(thirdReworded)]);
    const archived = page.waitForResponse(isItemWrite('POST', `/common-items/${ids[2]}/archive`));
    await page.keyboard.press('Enter');
    expect((await archived).status()).toBe(204);
    await expect(itemButton(page, 'Restore', thirdReworded)).toBeFocused();
    await expect(archivedSection(page, 'Every note')).toHaveAttribute('open', '');
    const stops = [...rowStops(second), ...rowStops(first), ...addForm, summaryStop('Every note')];
    await expectStops(page, 'Tab', ['outside the page']);
    await expectStops(page, 'Shift+Tab', [rowName('Restore', thirdReworded), ...[...stops].reverse(), 'Manage']);

    // Enter on the summary closes it, and Tab then passes Restore by; Enter opens it again.
    await expectStops(page, 'Tab', stops);
    await page.keyboard.press('Enter');
    await expect(archivedSection(page, 'Every note')).not.toHaveAttribute('open');
    await expectStops(page, 'Tab', ['outside the page']);
    await expectStops(page, 'Shift+Tab', [summaryStop('Every note')]);
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', [rowName('Restore', thirdReworded)]);

    // Restored with Enter: the item last in the group, focus on its Archive, and Archived items gone.
    const restored = page.waitForResponse(isItemWrite('POST', `/common-items/${ids[2]}/restore`));
    await page.keyboard.press('Enter');
    expect((await restored).status()).toBe(204);
    await expect(itemTexts(page, 'Every note')).toHaveText([second, first, thirdReworded]);
    await expect(itemButton(page, 'Archive', thirdReworded)).toBeFocused();
    await expect(archivedSection(page, 'Every note')).toHaveCount(0);
    await expectStops(page, 'Tab', [...addForm, 'outside the page']);
    await expectNothingKept(page, first, second, third, thirdReworded);
  });
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
  await expectAxeToPass(page);

  // Only Every note, as admin bootstrap leaves it: Try again brings it, empty, and focuses the <h1>.
  await serveCommonItems(page, [everyNote]);
  await tryAgain(page).click();
  await expect(heading(page)).toBeFocused();
  await expect(groupHeadings(page)).toHaveText(['Every note']);
  await expect(section(page, 'Every note').getByText(everyNoteLine, { exact: true })).toBeVisible();
  await expect(section(page, 'Every note').getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(loadLine(page)).toHaveText('');
  await expectAxeToPass(page);

  // Ready with three groups: Every note first, with its line and no group buttons, then the others in order, the
  // archived one left out; numbers restart in each group; each group's end buttons unavailable, in place and dashed,
  // saying why when pressed; and the archived items closed.
  const store = await serveCommonItems(page, threeGroups);
  await page.goto('/manage/common-items');
  await expect(groupHeadings(page)).toHaveText(['Every note', 'Community outing', 'Personal care']);
  await expect(section(page, 'Every note').getByText(everyNoteLine, { exact: true })).toBeVisible();
  await expect(itemTexts(page, 'Every note')).toHaveText([medication.text, meal.text]);
  await expect(itemTexts(page, 'Community outing')).toHaveText([busOrTrain.text, paid.text, lunch.text]);
  await expect(section(page, 'Personal care').getByText(emptyLine, { exact: true })).toBeVisible();
  await expect(main(page).getByRole('button', { name: / group$/ })).toHaveCount(0);
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

  // Editing an item: the row's form in its place, its box focused, holding the item's words.
  await itemButton(page, 'Edit', paid.text).click();
  await expect(editItem(page)).toBeFocused();
  await expect(editItem(page)).toHaveValue(paid.text);
  await expect(editItem(page)).toHaveId(`item-${paid.id}-text`);
  await expectAxeToPass(page);

  // Field error: whitespace alone, refused on Save above the box, with focus in it, and no "Error: " in the title.
  await editItem(page).fill('   ');
  await saveItem(page).click();
  await expect(fieldError(page, 'Community outing')).toHaveText('Error: Enter the common item');
  await expect(editItem(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(editItem(page)).toBeFocused();
  await expect(page).toHaveTitle(title('Common items'));
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
  await expectNothingKept(page, medication.text, meal.text, busOrTrain.text, paid.text, lunch.text, theirs, typed);
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

/** The text and buttons in `row` whose content does not fit in them, by their text, which none should be. */
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
  // *Layout - phone*), each label width, at least 44 by 48 px.
  for (const { group, text } of activeRows) {
    const words = await textOf(group, text);
    const [up, down, edit, archive] = await boxesOf(text);
    expect(up.y, `Move up under "${text}"`).toBeGreaterThanOrEqual(words.y + words.height);
    expect(down.y, `Move down beside Move up, "${text}"`).toBeCloseTo(up.y, 0);
    expect(archive.y, `Archive beside Edit, "${text}"`).toBeCloseTo(edit.y, 0);
    expect(edit.y, `Edit on the line after Move up, "${text}"`).toBeGreaterThanOrEqual(up.y + up.height);
  }
  await expectLabelWidth(main(page).locator('ol button'));
  await archivedSummary(page, 'Every note').click();
  await expectAtLeast(archivedSummary(page, 'Every note'), 44, 44);
  await expectLabelWidth(archivedSection(page, 'Every note').getByRole('button'));
  await itemButton(page, 'Edit', paid.text).click();
  await expectLabelWidth(editForm(page).getByRole('button'));
  expect((await boxOf(cancelItem(page))).y, 'Cancel beside Save').toBeCloseTo((await boxOf(saveItem(page))).y, 0);
  // Add item keeps the shared button's rule: as wide as the field on a phone, in each group.
  for (const group of groupNames) {
    expect((await boxOf(addItem(page, group))).width, group).toBeCloseTo((await boxOf(newItem(page, group))).width, 0);
    await expectAtLeast(addItem(page, group), 48, 48);
  }
  await cancelItem(page).click();

  // On a laptop, on the 60rem page, the four sit at the top of each row, after the text, on its line; Add item is its
  // label's width; and the help text and the fields stay in the 40rem reading column while the lists use the page.
  await page.setViewportSize({ width: 1280, height: 800 });
  const measure = 40 * 16;
  expect((await boxOf(main(page))).width, 'the page').toBeLessThanOrEqual(60 * 16);
  for (const { group, text } of activeRows) {
    const words = await textOf(group, text);
    for (const [index, button] of (await boxesOf(text)).entries()) {
      expect(button.y, `${labels[index]} on the line of "${text}"`).toBeCloseTo(words.y, 0);
      expect(button.x, `${labels[index]} after "${text}"`).toBeGreaterThanOrEqual(words.x + words.width);
    }
  }
  await expectLabelWidth(main(page).locator('ol button, details button'));
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
  expect(await scrollsSideways(page), 'its edit form').toBe(false);
  await cancelItem(page).click();

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
  await expectNothingKept(page, long.text, longGroup.name, paid.text, lunch.text, 'a'.repeat(20));
});

test('in forced colours, the focus ring shows in Highlight at each stop, unavailable buttons are dashed in GrayText, '
  + 'and the summary markers and row dividers are drawn', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, manager);
  await serveCommonItems(page, threeGroups);
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

  // Each group's stops in turn (common-items.md *Focus order*): its item rows, its add form, then its Archived items.
  const outingRows = [...rowStops(busOrTrain.text), ...rowStops(paid.text), ...rowStops(lunch.text)];
  await expectStops(
    page,
    'Tab',
    [
      ...rowStops(medication.text),
      ...rowStops(meal.text),
      ...['New item in Every note', 'Add item', summaryStop('Every note')],
      ...outingRows,
      ...['New item in Community outing', 'Add item', 'New item in Personal care', 'Add item', 'outside the page'],
    ],
    forcedRing,
  );

  // Every note's Archived items, closed, then opened with Enter, its marker drawn either way, in the system colour that
  // forced colours give the summary's words, not the background's; and each Restore, newest archived first, after it.
  const summary = archivedSummary(page, 'Every note');
  const expectMarkerDrawn = async (shape: 'disclosure-closed' | 'disclosure-open') => {
    await expect(summary).toHaveCSS('display', 'list-item');
    await expect(summary).toHaveCSS('list-style-type', shape);
    const [marker, words] = await summary.evaluate((element) => [
      getComputedStyle(element, '::marker').color,
      getComputedStyle(element).color,
    ]);
    expect(marker, 'the marker in the colour of the summary').toBe(words);
    expect(marker, 'the marker unlike the background').not.toBe(canvas);
  };
  await expectMarkerDrawn('disclosure-closed');
  await expectStops(
    page,
    'Shift+Tab',
    [
      ...['Add item', 'New item in Personal care', 'Add item', 'New item in Community outing'],
      ...[...outingRows].reverse(),
      summaryStop('Every note'),
    ],
    forcedRing,
  );
  await page.keyboard.press('Enter');
  await expectMarkerDrawn('disclosure-open');
  await expectStops(page, 'Tab', [rowName('Restore', bed.text), rowName('Restore', laundry.text)], forcedRing);

  // Each group's end buttons, unavailable: GrayText words and dashed GrayText borders, where the others keep theirs
  // (list-editor.md *States*).
  for (const end of [
    itemButton(page, 'Move up', medication.text),
    itemButton(page, 'Move down', meal.text),
    itemButton(page, 'Move up', busOrTrain.text),
    itemButton(page, 'Move down', lunch.text),
  ]) {
    await expect(end).toHaveCSS('color', grayText);
    await expect(end).toHaveCSS('border-top-color', grayText);
    await expect(end).toHaveCSS('border-top-style', 'dashed');
  }
  await expect(itemButton(page, 'Move down', medication.text)).not.toHaveCSS('color', grayText);
  await expect(itemButton(page, 'Move down', medication.text)).toHaveCSS('border-top-style', 'solid');

  // The rule under each row, active or archived, in CanvasText.
  const everyNoteRows = section(page, 'Every note').locator('ol > li, details li');
  await expect(everyNoteRows).toHaveCount(4);
  for (const row of [...(await everyNoteRows.all()), ...(await itemRows(page, 'Community outing').all())]) {
    await expect(row).toHaveCSS('border-bottom-style', 'solid');
    await expect(row).toHaveCSS('border-bottom-width', '1px');
    await expect(row).toHaveCSS('border-bottom-color', canvasText);
  }
  await expectNothingKept(page, ...shownTexts);
});
