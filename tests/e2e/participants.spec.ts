import type { Locator, Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite, type VirtualAuthenticator } from './fixtures.ts';
import { whereFocusIs } from './focus.ts';
import { boxOf } from './layout.ts';
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

// The Participants screens in the published app (S01.02.01 to S01.02.04), against participants.md's criteria. The
// first three tests sign in for real, through setup with a passkey on the virtual authenticator, and add, correct,
// archive and restore a participant on the app's own answers; the others answer /api/auth/me, the list and each change
// in the browser with made-up people (me.ts, participants.ts), so that none of those requests reaches the app.

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
const save = (page: Page) => main(page).getByRole('button', { name: 'Save', exact: true });
const saveStatus = (page: Page) => main(page).locator('form').getByRole('status');
const saveAlert = (page: Page) => main(page).locator('form').getByRole('alert');
const archive = (page: Page) => main(page).getByRole('button', { name: 'Archive participant' });
const archiveAlert = (page: Page) => main(page).getByRole('region', { name: 'Actions' }).getByRole('alert');
const restore = (page: Page) => main(page).getByRole('button', { name: 'Restore', exact: true });
// The archived notice's words (participants.md B3); the notice holds them, then Restore with its alert above it.
const archivedWords = (name: string) =>
  `${name} is archived, so this page is read-only. They are not on Today and no new notes can be written for them. ` +
  'Their past notes are kept.';
const noticeWords = (page: Page) => main(page).getByText(/ is archived, so this page is read-only\./);
const restoreAlert = (page: Page) => noticeWords(page).locator('xpath=..').getByRole('alert');

/** "Saved 4:12 pm", at whatever time the app saved, the time with its non-breaking space (status-messages.md). */
const savedAtAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

/** Whether the response answers the list's request, the Participants screens' one read. */
const isListRead = (response: Response) =>
  response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/admin/participants';

/** Whether the response answers a change by `method` to the path after `/api/admin/participants`. */
const isChange = (method: 'POST' | 'PUT', path = '') => (response: Response) =>
  response.request().method() === method && new URL(response.url()).pathname === `/api/admin/participants${path}`;

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

/**
 * Reports, through `added`, each live region, an element whose role is `status` or `alert`, that the Participants
 * screens put into the document with words already in it, as a screen reader may not announce them (participants.md
 * *Cross-cutting*). The app puts its elements in with `appendChild` and `insertBefore`, which the script wraps before
 * the app's own runs, so that each subtree is looked at as it joins the document.
 */
async function watchLiveRegions(page: Page): Promise<string[]> {
  const added: string[] = [];
  await page.exposeFunction('liveRegionAddedWithWords', (region: string) => {
    added.push(region);
  });
  await page.addInitScript(() => {
    const regions = '[role="status"], [role="alert"]';
    function check(parent: Node, node: Node) {
      if (!parent.isConnected || !(node instanceof Element) || !location.pathname.startsWith('/manage/participants')) {
        return;
      }

      for (const region of [node, ...node.querySelectorAll(regions)].filter((element) => element.matches(regions))) {
        if ((region.textContent ?? '').trim() !== '') {
          void (window as unknown as { liveRegionAddedWithWords: (region: string) => Promise<void> })
            .liveRegionAddedWithWords(region.outerHTML);
        }
      }
    }

    const { appendChild, insertBefore } = Node.prototype;
    Node.prototype.appendChild = function <T extends Node>(this: Node, node: T): T {
      check(this, node);
      return appendChild.call(this, node) as T;
    };
    Node.prototype.insertBefore = function <T extends Node>(this: Node, node: T, child: Node | null): T {
      check(this, node);
      return insertBefore.call(this, node, child) as T;
    };
  });
  return added;
}

/** Each live region in the page whose own display is `none`, which would stop it announcing anything. */
const hiddenLiveRegions = (page: Page) =>
  page.evaluate(() =>
    [...document.querySelectorAll('[role="status"], [role="alert"]')]
      .filter((region) => getComputedStyle(region).display === 'none')
      .map((region) => region.outerHTML),
  );

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page);
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

/** The focus ring as it shows (foundations.md): whether the element matches `:focus-visible`, and its outline. */
type Ring = { focusVisible: boolean; style: string; width: string; colour: string; inViewport: boolean };

// base.css's ring, 3 px of --colour-focus, on an element on screen.
const ring: Ring = { focusVisible: true, style: 'solid', width: '3px', colour: 'rgb(11, 12, 12)', inViewport: true };

/**
 * Where focus is, as `whereFocusIs` says, but by the text that shows, so that a button's busy label, hidden until it is
 * busy, is left out, and naming a field by its label; with the ring that shows on the element or, for a row's link, on
 * the `::after` that covers the row (LinkRow.module.css); no ring outside the page or on it.
 */
function focusStop(page: Page): Promise<{ name: string; ring?: Ring }> {
  return page.evaluate(() => {
    const element = document.activeElement;
    if (!document.hasFocus()) {
      return { name: 'outside the page' };
    }

    if (!(element instanceof HTMLElement) || element === document.body) {
      return { name: 'the page itself' };
    }

    const text = element instanceof HTMLInputElement ? element.labels?.[0]?.innerText : element.innerText;
    const own = getComputedStyle(element);
    const drawn = own.outlineStyle === 'none' ? getComputedStyle(element, '::after') : own;
    const box = element.getBoundingClientRect();
    return {
      name: (text ?? '').replace(/\s+/g, ' ').trim(),
      ring: {
        focusVisible: element.matches(':focus-visible'),
        style: drawn.outlineStyle,
        width: drawn.outlineWidth,
        colour: drawn.outlineColor,
        inViewport: box.bottom > 0 && box.top < innerHeight && box.right > 0 && box.left < innerWidth,
      },
    };
  });
}

/**
 * Presses `key` once for each of `stops`, expecting focus to land on each in turn with `expected`'s ring showing
 * (backlog README §9's keyboard-only check). Past the page's last stop, Tab takes focus out of the page, to the
 * browser, and the next Shift+Tab brings it back to that stop.
 */
async function expectStops(page: Page, key: 'Tab' | 'Shift+Tab', stops: readonly string[], expected = ring) {
  for (const [index, stop] of stops.entries()) {
    await page.keyboard.press(key);
    const reached = await focusStop(page);
    expect(reached.name, `${key} number ${index + 1}, to ${stop}`).toBe(stop);
    if (reached.ring !== undefined) {
      expect(reached.ring, `the focus ring on ${stop}`).toEqual(expected);
    }
  }
}

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

/**
 * Text at 200%: Chromium's default text size at 32 px, where it is 16, which makes the body's 18 px 36 (S00.05.01's
 * Notes).
 */
async function setTextTo200Percent(page: Page) {
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  await expect(page.locator('body')).toHaveCSS('font-size', '36px');
}

/** Whether the page scrolls sideways. */
const scrollsSideways = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);

/** Expects each element that the locator finds, once one shows, to be at least `width` by `height` CSS pixels. */
async function expectAtLeast(locator: Locator, width: number, height: number) {
  await expect(locator.first()).toBeVisible();
  for (const target of await locator.all()) {
    const box = await boxOf(target);
    const name = `${(await target.textContent()) ?? ''} ${await target.evaluate((element) => element.localName)}`;
    expect(Math.round(box.width), `the width of ${name}`).toBeGreaterThanOrEqual(width);
    expect(Math.round(box.height), `the height of ${name}`).toBeGreaterThanOrEqual(height);
  }
}

/** Sets up the invited manager's account through setup with a passkey on the virtual authenticator, ending on Today. */
async function signInThroughSetup(page: Page, invite: Invite, authenticator: VirtualAuthenticator) {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);
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

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: add, correct, archive and restore, with the focus ring at each stop`, async ({
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
    // Until T05 builds Goals, Archive participant follows Save.
    await expectStops(page, 'Tab', ['Archive participant', 'outside the page']);

    // The date of birth corrected.
    await expectStops(page, 'Shift+Tab', ['Archive participant', 'Save', 'Year', 'Month', 'Day']);
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type('8');
    await expectStops(page, 'Tab', ['Month', 'Year', 'Save']);
    const corrected = page.waitForResponse(isChange('PUT', `/${id}`));
    await page.keyboard.press('Enter');
    expect((await corrected).status()).toBe(200);
    await expect(saveStatus(page)).toHaveText(savedAtAnyTime);
    await expect(save(page)).toBeFocused();

    // Archived: the notice takes focus, then an archived participant's page has the back link and Restore.
    await expectStops(page, 'Tab', ['Archive participant']);
    const archived = page.waitForResponse(isChange('POST', `/${id}/archive`));
    await page.keyboard.press('Enter');
    expect((await archived).status()).toBe(204);
    await expect.poll(() => whereFocusIs(page)).toContain(archivedWords(siobhan.name));
    await expectStops(page, 'Tab', ['Restore', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Restore', 'Participants', 'Manage']);
    await expectStops(page, 'Tab', ['Participants', 'Restore']);

    // Restored: the <h1> takes focus, and the form and Archive participant are back.
    const restored = page.waitForResponse(isChange('POST', `/${id}/restore`));
    await page.keyboard.press('Enter');
    expect((await restored).status()).toBe(204);
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Tab', [...form, 'Archive participant', 'outside the page']);

    // Back to the list, where they are now to be found: Find a participant, then Clear while it holds text.
    await expectStops(page, 'Shift+Tab', ['Archive participant', ...[...form].reverse(), 'Participants']);
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
  await expect(main(page).getByRole('region', { name: 'Actions' })).toHaveCount(0);
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

  // Opened on an archived participant: the notice there as plain content, which takes no focus, and no Actions.
  await page.goto(`/manage/participants/${ben.id}`);
  await expect(heading(page)).toHaveText('Ben Archer');
  await expect(noticeWords(page)).toHaveText(archivedWords('Ben Archer'));
  await expect(noticeWords(page).locator('xpath=../..')).not.toHaveAttribute('tabindex');
  await expect(noticeWords(page).locator('xpath=../..')).not.toBeFocused();
  await expect(main(page).getByRole('definition')).toHaveText(['Ben', 'Archer', '7 March 1987']);
  await expect(main(page).getByRole('region', { name: 'Actions' })).toHaveCount(0);
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
  // A button busy for 400 ms, which then shows its busy label, and is named by it, in place of the one it rests with.
  const busyButton = main(page).locator('button[data-busy]');

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
    await expect(busyButton).toHaveText(busy, { useInnerText: true });
    await expectAxeToPass(page);
    await change.fulfill({ status, headers: { date: answeredAt } });
    await expect(busyButton).toHaveCount(0);
  }
});

test('at 320 px with text at 200%, nothing on the Participants screens scrolls sideways', async ({ page }) => {
  const long = madeUpParticipant('6d2f8b1e-5a94-4c37-a1e6-9b4d0f7c3e52', 'Bartholomew', 'Featherstonehaugh-Montgomery');
  await serveMe(page, manager);
  await serveParticipants(page, [ben, jane, ravi, long]);
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

test('targets: 44 px links and fields, 48 px buttons and search, 56 px rows, at phone and laptop width', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveParticipants(page, participants);

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

    await page.goto(`/manage/participants/${ben.id}`);
    await expectAtLeast(restore(page), 48, 48);
  }
});

test('in forced colours, the focus ring shows in the system Highlight colour at each stop', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, manager);
  await serveParticipants(page, participants);
  await page.goto('/manage/participants');
  await expect(rows(page)).toHaveCount(2);
  await heading(page).focus();
  // The colour that forced colours give Highlight, which base.css's ring takes there.
  const highlight = await page.evaluate(() => {
    const probe = document.createElement('span');
    probe.style.outlineColor = 'Highlight';
    document.body.append(probe);
    const colour = getComputedStyle(probe).outlineColor;
    probe.remove();
    return colour;
  });
  expect(highlight).not.toBe(ring.colour);
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
    ['Given name', 'Family name', 'Day', 'Month', 'Year', 'Save', 'Archive participant'],
    forcedRing,
  );

  await serveParticipants(page, [ben, { ...jane, status: 'Archived', rowVersion: 'AAAAAAAB3e8b=' }, ravi]);
  await answerChanges(page, 'POST', `/${jane.id}/archive`, { status: 204 });
  await page.keyboard.press('Enter');
  await expect.poll(() => whereFocusIs(page)).toContain(archivedWords('Jane Citizen'));
  await expectStops(page, 'Tab', ['Restore'], forcedRing);
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
