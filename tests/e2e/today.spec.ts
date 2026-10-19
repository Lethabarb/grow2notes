import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops, ring } from './focus.ts';
import { breakfast, bus } from './goals.ts';
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
import { daysAfter, notesStore, type NotesStore } from './notes.ts';
import { ben, jane, madeUpParticipant, participants, ravi } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';
import { failToday, serveMyDrafts, serveToday, todayList, type MyDraft, type TodayNote } from './today.ts';

// Today in the published app (S02.01.01 to S02.01.04), against today.md's criteria: its states, each row's note
// status, the device's own clock and zone, Find a participant, the banner after Submit, Your unfinished drafts,
// reflow, targets, the focus ring and the keyboard alone. Each test answers /api/auth/me, /api/today and
// /api/me/drafts in the browser (me.ts, today.ts) with made-up people (A35), and the note that a row opens from a store
// in memory (notes.ts), so that none of those requests reaches the app; note-form.spec.ts signs in for real and meets
// the app's own Today.

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// Each row's link, a return point by its key (app-shell.md *The route-change rule (one owner)*), and its list item.
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const rowItems = (page: Page) => main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key]') });
const row = (page: Page, name: string) => rows(page).filter({ hasText: name });
const search = (page: Page) => main(page).getByRole('search');
const findField = (page: Page) => main(page).getByRole('textbox', { name: 'Find a participant' });
const clear = (page: Page) => main(page).getByRole('button', { name: 'Clear search' });
// The search's own status region, visually hidden, which says the no-match sentence once typing has paused.
const searchStatus = (page: Page) => search(page).getByRole('status');
// A sentence in place of the rows, not the search's hidden status region, which says the no-match sentence too.
const sentence = (page: Page, words: string) =>
  main(page).getByText(words, { exact: true }).and(page.locator(':not([role="status"])'));
// The region's line, beside the page's and the search's visually hidden status regions (today.md *Live regions*).
const loadLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
const addParticipant = (page: Page) => main(page).getByRole('link', { name: 'Add participant', exact: true });
// The note's back link, in the shell's bar between <header> and <main> (note-form.md component 1).
const backLink = (page: Page) => page.locator('#page-bar').getByRole('link', { name: 'Today', exact: true });
// A row's status line, which describes its link, and its Flagged tag (today.md component 8).
const rowLine = (page: Page, name: string) => rowItems(page).filter({ hasText: name }).locator('p');
const flagged = (page: Page) => main(page).getByText('Flagged', { exact: true });
// The success banner after Submit, before the <h1>, which the shell focuses (today.md component 2).
const banner = (page: Page) => main(page).locator('[data-route-focus]');

/** The `<h1>`'s words on Thursday 1 October 2026, `me.ts`'s day. */
const todayHeading = 'Today · Thursday 1 October';

/** Each element of the page that is fixed or sticky, by its tag, which could cover the control with focus. */
const pinned = (page: Page) =>
  page.evaluate(() =>
    [...document.querySelectorAll('*')]
      .filter((element) => ['fixed', 'sticky'].includes(getComputedStyle(element).position))
      .map((element) => element.localName),
  );

/**
 * Holds back, in each page that loads from now on, each timer of exactly 1 s that the page sets, such as the region's
 * before its loading line (empty-loading-error.md *Timing*), until the function that it gives runs them, so that the
 * region stays waiting for as long as the test needs, while axe's own timers, and every other, run as they would.
 */
async function holdOneSecondTimers(page: Page): Promise<() => Promise<void>> {
  await page.addInitScript(() => {
    const held = new Map<number, () => void>();
    let lastId = 0;
    const { setTimeout: set, clearTimeout: clear } = window;
    window.setTimeout = ((handler: TimerHandler, timeout?: number, ...args: unknown[]) => {
      if (timeout !== 1_000 || typeof handler !== 'function') {
        return set(handler, timeout, ...args);
      }

      // Below zero, where the browser's own IDs never are, so that clearing one clears the right timer.
      lastId -= 1;
      held.set(lastId, () => {
        Reflect.apply(handler, window, args);
      });
      return lastId;
    }) as typeof window.setTimeout;
    window.clearTimeout = ((id?: number) => {
      if (id !== undefined && !held.delete(id)) {
        clear(id);
      }
    }) as typeof window.clearTimeout;
    Object.assign(window, {
      runHeldTimers: () => {
        const due = [...held.values()];
        held.clear();
        for (const run of due) {
          run();
        }
      },
    });
  });
  return () => page.evaluate(() => (window as unknown as { runHeldTimers: () => void }).runHeldTimers());
}

/** Opens Today as `me`, on `me`'s day, with Jane Citizen and Ravi de Silva, and waits for their rows. */
async function openToday(page: Page, me: Me = worker) {
  await serveMe(page, me);
  await serveToday(page, me.today, participants);
  await page.goto('/');
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  // Every screen that these tests open: Today, at /, and the note that a row opens.
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  // But the save indicator of a draft that a row of Your unfinished drafts reopens, which the form puts in anew with
  // its first words, "Draft · saved …", so that they are the page's own rather than announced as a change
  // (autosave-status.md *Behaviour* 8).
  const announcedLate = liveRegionsAddedWithWords.filter((region) => !/^<p id="save-status"[^>]*>Draft</.test(region));
  expect(announcedLate, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

// Shows that the watcher sees Today, as participants.spec.ts shows it for the Participants screens: marked to fail, it
// passes only if the check after it fails it.
test.fail('a live region put into Today with its words fails the test', async ({ page }) => {
  await openToday(page);
  await main(page).evaluate((element) => {
    const region = document.createElement('p');
    region.setAttribute('role', 'status');
    region.textContent = 'Made-up words';
    element.appendChild(region);
  });
});

test('Today waiting, loading, ready, with no match and with no participants, each passing axe', async ({ page }) => {
  const runHeldTimers = await holdOneSecondTimers(page);
  await serveMe(page, manager);
  const heldList = await holdNext(page, '**/api/today');
  await page.goto('/');
  const list = await heldList();

  // Waiting: the <h1> at once, and the region's line in the page and empty, with no search box (today.md *Region
  // states*), for as long as axe takes, as its 1 s is held.
  await expect.poll(() => shownText(heading(page))).toBe(todayHeading);
  await expect(page).toHaveTitle(title('Today'));
  await expect(loadLine(page)).toBeEmpty();
  await expect(search(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await expect(loadLine(page)).toBeEmpty();

  // Loading, once the 1 s has passed.
  await runHeldTimers();
  await expect(loadLine(page)).toHaveText('Loading participants…');
  await expect(search(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Today'));
  await expectAxeToPass(page);

  // Ready: the hidden <h2>, then Find a participant, then a row for each, by the name alone, with no second line.
  await serveToday(page, manager.today, participants);
  await list.fulfill({ json: todayList(manager.today, participants) });
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText('Participants');
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveClass('visually-hidden');
  await expect(rowItems(page).locator('p')).toHaveCount(0);
  await expect(loadLine(page)).toBeEmpty();
  await expect(page).toHaveTitle(title('Today'));
  await expectAxeToPass(page);

  // No match: the sentence in place of the rows at once, and in the search's status after the pause.
  await findField(page).fill('Zed');
  await expect(sentence(page, 'No participant matches ‘Zed’.')).toBeVisible();
  await expect(rows(page)).toHaveCount(0);
  await expect(searchStatus(page)).toHaveText('No participant matches ‘Zed’.');
  await expect(page).toHaveTitle(title('Today'));
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Zed', 'Jane', 'Citizen', 'Ravi', 'Silva');

  // No participants: for a manager, the sentence and Add participant, with no search box.
  await serveToday(page, manager.today, []);
  await page.goto('/');
  await expect(sentence(page, 'No participants yet.')).toBeVisible();
  await expect(addParticipant(page)).toHaveAttribute('href', '/manage/participants/new');
  await expect(search(page)).toHaveCount(0);
  await expectAxeToPass(page);

  // And for a worker, the sentence alone.
  await serveMe(page, worker);
  await serveToday(page, worker.today, []);
  await page.goto('/');
  await expect(sentence(page, 'No participants yet.')).toBeVisible();
  await expect(addParticipant(page)).toHaveCount(0);
  await expect(search(page)).toHaveCount(0);
  await expectAxeToPass(page);
});

test('offline, the load-failed sentence within about 2 s, then "still", and Try again that loads focuses <h1>', async ({
  page,
}) => {
  await serveMe(page, worker);
  await failToday(page, 'no connection');
  await page.goto('/');
  await expect(loadLine(page)).toHaveText('The participant list did not load: no connection. Try again.', {
    timeout: 2_000,
  });
  await expect.poll(() => shownText(heading(page))).toBe(todayHeading);
  await expect(search(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Today'));
  await expectAxeToPass(page);

  await tryAgain(page).click();
  await expect(loadLine(page)).toHaveText('The participant list still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expect(tryAgain(page)).not.toHaveAttribute('disabled');
  await expectAxeToPass(page);

  // A server problem: its own cause, with no status code.
  await failToday(page, 503);
  await page.goto('/');
  await expect(loadLine(page)).toHaveText('The participant list did not load: something went wrong. Try again.');
  await expectAxeToPass(page);

  await serveToday(page, worker.today, participants);
  await tryAgain(page).click();
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(heading(page)).toBeFocused();
  await expect(tryAgain(page)).toHaveCount(0);
});

test.describe('with the device in Los Angeles on 2 October', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test("the <h1> and every row's link use the server's date in Melbourne, 4 October", async ({ page }) => {
    // 9 am on Friday 2 October in Los Angeles, two days behind the server's Sunday in Melbourne.
    await page.clock.install({ time: new Date('2026-10-02T16:00:00Z') });
    const sunday: Me = { ...worker, today: '2026-10-04' };
    const store = notesStore({ today: sunday.today, participants });
    await serveMe(page, sunday);
    await serveToday(page, sunday.today, participants);
    await store.serve(page, sunday);
    await page.goto('/');

    await expect(rows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
    expect(await page.evaluate(() => new Date().toDateString())).toBe('Fri Oct 02 2026');
    await expect.poll(() => shownText(heading(page))).toBe('Today · Sunday 4 October');
    await expect(heading(page).locator('time')).toHaveAttribute('datetime', '2026-10-04');
    for (const link of await rows(page).all()) {
      await expect(link).toHaveAttribute('href', /^\/participants\/[0-9a-f-]{36}\/notes\/2026-10-04$/);
    }

    // The note that a row opens is for that day too.
    await row(page, 'Jane Citizen').click();
    await expect(page).toHaveURL(`/participants/${jane.id}/notes/2026-10-04`);
    await expect(heading(page)).toHaveText('Jane Citizen Sunday 4 October 2026');
    await expect(heading(page).locator('time')).toHaveAttribute('datetime', '2026-10-04');
  });
});

test('Find a participant filters on each key with no request, says no match, and Clear keeps focus', async ({
  page,
}) => {
  const zoe = madeUpParticipant('5d3a9c1e-7b24-4f68-a0e5-3c8d2f6b1a97', 'Zoë', 'O’Brien-Smith');
  await serveMe(page, worker);
  await serveToday(page, worker.today, [jane, zoe, ravi]);
  await notesStore({ today: worker.today, participants: [jane, zoe, ravi] }).serve(page, worker);
  await page.goto('/');
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Zoë O’Brien-Smith', 'Ravi de Silva']);
  const sent = recordRequests(page);

  // Each key filters the rows, in their order, with no request.
  await findField(page).pressSequentially('zoe');
  await expect(rows(page)).toHaveText(['Zoë O’Brien-Smith']);
  await findField(page).fill('cit jane');
  await expect(rows(page)).toHaveText(['Jane Citizen']);
  await findField(page).fill('s');
  await expect(rows(page)).toHaveText(['Zoë O’Brien-Smith', 'Ravi de Silva']);

  // No match: the sentence at once, and the status after the pause, emptied once matches return.
  await findField(page).fill('xyz');
  await expect(sentence(page, 'No participant matches ‘xyz’.')).toBeVisible();
  await expect(searchStatus(page)).toHaveText('No participant matches ‘xyz’.');
  await findField(page).fill('obr');
  await expect(rows(page)).toHaveText(['Zoë O’Brien-Smith']);
  await expect(searchStatus(page)).toBeEmpty();

  // Enter and Escape change nothing; Clear empties the field and leaves focus in it.
  await findField(page).press('Enter');
  await findField(page).press('Escape');
  await expect(findField(page)).toHaveValue('obr');
  await expect(page).toHaveURL('/');
  await clear(page).click();
  await expect(findField(page)).toBeFocused();
  await expect(findField(page)).toHaveValue('');
  await expect(clear(page)).toHaveCount(0);
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Zoë O’Brien-Smith', 'Ravi de Silva']);
  expect(await sent()).toEqual([]);

  // What was typed is gone after leaving Today, by a row, and coming back, by the note's back link.
  await findField(page).fill('ravi');
  await expect(rows(page)).toHaveText(['Ravi de Silva']);
  await expectNothingKept(page, 'ravi', 'Ravi', 'Silva', 'Zoë', 'Jane');
  await row(page, 'Ravi de Silva').click();
  await expect(heading(page)).toHaveText('Ravi de Silva Thursday 1 October 2026');
  await backLink(page).click();
  await expect(row(page, 'Ravi de Silva')).toBeFocused();
  await expect(findField(page)).toHaveValue('');
  await expect(rows(page)).toHaveText(['Jane Citizen', 'Zoë O’Brien-Smith', 'Ravi de Silva']);
  await expectNothingKept(page, 'ravi', 'Ravi', 'Silva', 'Zoë', 'Jane');
});

test('at 320 px with text at 200%, nothing on Today scrolls sideways, and Clear wraps under the field', async ({
  page,
}) => {
  const long = madeUpParticipant('6d2f8b1e-5a94-4c37-a1e6-9b4d0f7c3e52', 'Bartholomew', 'Featherstonehaugh-Montgomery');
  const unbroken = madeUpParticipant('7e3a9c2f-6b05-4d48-b2f7-0c5e1a8d4f63', 'A'.repeat(50), 'B'.repeat(50));
  await serveMe(page, manager);
  await serveToday(page, manager.today, [jane, long, unbroken, ravi]);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/');
  await setTextTo200Percent(page);
  await expect(rows(page)).toHaveCount(4);
  expect(await scrollsSideways(page), 'the list, with a name of 100 letters and no space').toBe(false);
  // Every name whole, never cut short.
  for (const link of await rows(page).all()) {
    expect(await link.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
  }

  await findField(page).fill('Wolfeschlegelsteinhausenbergerdorff');
  await expect(sentence(page, 'No participant matches ‘Wolfeschlegelsteinhausenbergerdorff’.')).toBeVisible();
  expect(await scrollsSideways(page), 'the no-match sentence').toBe(false);
  const field = await boxOf(findField(page));
  expect((await boxOf(clear(page))).y, 'Clear under the field').toBeGreaterThanOrEqual(field.y + field.height);

  await serveToday(page, manager.today, []);
  await page.goto('/');
  await expect(addParticipant(page)).toBeVisible();
  expect(await scrollsSideways(page), 'no participants').toBe(false);

  await failToday(page, 'no connection');
  await page.goto('/');
  await expect(tryAgain(page)).toBeVisible();
  expect(await scrollsSideways(page), 'the load failed').toBe(false);
});

test('targets: 56 px rows, a 48 px search field, Clear and Try again, a 44 px link, and nothing pinned', async ({
  page,
}) => {
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await openToday(page, manager);
    await expectAtLeast(rowItems(page), 44, 56);
    await findField(page).fill('a');
    await expectAtLeast(findField(page), 44, 48);
    await expectAtLeast(clear(page), 48, 48);
    expect(await pinned(page), `Today at ${width} px`).toEqual([]);

    await serveToday(page, manager.today, []);
    await page.goto('/');
    await expectAtLeast(addParticipant(page), 44, 44);
    expect(await pinned(page), `no participants at ${width} px`).toEqual([]);

    await failToday(page, 'no connection');
    await page.goto('/');
    await expectAtLeast(tryAgain(page), 48, 48);
    expect(await pinned(page), `the load failed at ${width} px`).toEqual([]);
  }
});

test('in forced colours, the focus ring shows in Highlight at each of Today’s stops', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await serveMe(page, worker);
  await failToday(page, 'no connection');
  await page.goto('/');
  await expect(tryAgain(page)).toBeVisible();
  const highlight = await systemColour(page, 'Highlight');
  expect(highlight).not.toBe(ring.colour);
  const forcedRing = { ...ring, colour: highlight };

  // The load failed: Try again after the header's.
  await heading(page).focus();
  await expectStops(page, 'Tab', ['Try again'], forcedRing);

  // Ready: the search box, Clear while it holds text, and each row's ring on the whole row.
  await serveToday(page, worker.today, participants);
  await page.keyboard.press('Enter');
  await expect(heading(page)).toBeFocused();
  await expectStops(page, 'Tab', ['Find a participant'], forcedRing);
  await page.keyboard.type('cit');
  await expectStops(page, 'Tab', ['Clear search', 'Jane Citizen'], forcedRing);
  await expectStops(page, 'Shift+Tab', ['Clear search'], forcedRing);
  await page.keyboard.press('Enter');
  await expect(findField(page)).toBeFocused();
  await expectStops(page, 'Tab', ['Jane Citizen', 'Ravi de Silva'], forcedRing);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: Today's Tab order, a row's note and back, with the ring at each stop`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] } });
    await store.serve(page, worker);
    await openToday(page);

    // From the top of the page (today.md *Tab order*): the skip link, the header and nav, then Find a participant and
    // the rows, the <h1> being no stop; then out of the page.
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    const list = ['Find a participant', 'Jane Citizen', 'Ravi de Silva'];
    await expectStops(page, 'Tab', [...shell, ...list, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [...shell, ...list].reverse());

    // Clear comes after the field only while it holds text; Enter in the field does nothing.
    await expectStops(page, 'Tab', ['Grow2Notes', 'Today', 'Account', 'Find a participant']);
    await page.keyboard.type('jane');
    await page.keyboard.press('Enter');
    await expectStops(page, 'Tab', ['Clear search', 'Jane Citizen', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['Jane Citizen', 'Clear search']);
    await page.keyboard.press('Enter');
    await expect(findField(page)).toBeFocused();
    await expect(findField(page)).toHaveValue('');

    // Enter on a row opens its note, whose <h1> takes focus; the back link is the stop before the form.
    await expectStops(page, 'Tab', ['Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(`/participants/${jane.id}/notes/${worker.today}`);
    await expect(heading(page)).toBeFocused();
    await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
    await expectStops(page, 'Shift+Tab', ['Today', 'Account']);

    // The back link returns to the row, which takes focus again.
    await expectStops(page, 'Tab', ['Today']);
    await page.keyboard.press('Enter');
    await expect(row(page, 'Jane Citizen')).toBeFocused();
    await expectStops(page, 'Tab', ['Ravi de Silva']);
    await expect(backLink(page)).toHaveCount(0);
    await expectNothingKept(page, 'Jane', 'Citizen', 'Ravi', 'Silva', 'jane');
  });
}

// Made-up participants for the status forms (A35), beside Jane and Ravi: one named Alex, whose note nobody has started.
const alexMorgan = madeUpParticipant('1d7e3a9c-5b28-4f61-a0d4-8c2e6f9b3a17', 'Alex', 'Morgan');
const kim = madeUpParticipant('2e8f4b0d-6c39-4a72-b1e5-9d3f7a0c4b28', 'Kim', 'Nguyen');
const mia = madeUpParticipant('3f9a5c1e-7d4a-4b83-82f6-0e4a8b1d5c39', 'Mia', 'Patel');
const lee = madeUpParticipant('4a0b6d2f-8e5b-4c94-93a7-1f5b9c2e6d4a', 'Lee', 'Walker');

// Times in Melbourne on Thursday 1 October 2026, before daylight saving starts: 9:14 am, 9:42 am, midday and 4:12 pm.
const at914 = '2026-09-30T23:14:00Z';
const at942 = '2026-09-30T23:42:00Z';
const midday = '2026-10-01T02:00:00Z';
const at412 = '2026-10-01T06:12:00Z';

/** A note on Today, someone else's unless `isMine`, as `GET /api/today` gives it. */
const todayNote = (note: Partial<TodayNote> & Pick<TodayNote, 'status'>): TodayNote => ({
  authorDisplayName: 'Alex P.',
  isMine: false,
  startedAtUtc: at914,
  isFlagged: false,
  ...note,
});

test('each row reads one of the four status forms, Flagged on a flagged submitted note alone, passing axe', async ({
  page,
}) => {
  const notes: Record<string, TodayNote> = {
    [jane.id]: todayNote({ status: 'Draft', authorDisplayName: worker.displayName, isMine: true, savedAtUtc: at942 }),
    [ravi.id]: todayNote({ status: 'Draft' }),
    [kim.id]: todayNote({ status: 'Submitted', firstSubmittedAtUtc: at412, isFlagged: true }),
    [mia.id]: todayNote({
      status: 'Submitted',
      authorDisplayName: worker.displayName,
      isMine: true,
      firstSubmittedAtUtc: at412,
    }),
    [lee.id]: todayNote({
      status: 'Submitted',
      authorDisplayName: worker.displayName,
      isMine: true,
      firstSubmittedAtUtc: midday,
    }),
  };
  const listed = [jane, ravi, alexMorgan, kim, mia, lee];
  await serveMe(page, worker);
  await serveToday(page, worker.today, listed, (id) => notes[id] ?? null);
  await page.goto('/');
  await expect(rows(page)).toHaveText([
    'Jane Citizen',
    'Ravi de Silva',
    'Alex Morgan',
    'Kim Nguyen',
    'Mia Patel',
    'Lee Walker',
  ]);

  // Each line as it shows, and as its row link's description, each dot read as a comma, which Chromium's reckoning of
  // the description puts after a space between the spans; none for a row with no note.
  const lines = {
    'Jane Citizen': ['Draft · You · saved 9:42 am', /^Draft ?, You ?, saved 9:42\sam$/],
    'Ravi de Silva': ['Draft · Alex P. · started 9:14 am', /^Draft ?, Alex P\. ?, started 9:14\sam$/],
    'Kim Nguyen': ['Submitted · Alex P. · 4:12 pm Flagged', /^Submitted ?, Alex P\. ?, 4:12\spm ?, Flagged$/],
    'Mia Patel': ['Submitted · You · 4:12 pm', /^Submitted ?, You ?, 4:12\spm$/],
    'Lee Walker': ['Submitted · You · midday', /^Submitted ?, You ?, midday$/],
  } as const;
  for (const [name, [shown, described]] of Object.entries(lines)) {
    await expect.poll(() => shownText(rowLine(page, name)), name).toBe(shown);
    await expect(row(page, name)).toHaveAccessibleDescription(described);
  }
  await expect(rowLine(page, 'Alex Morgan')).toHaveCount(0);
  await expect(flagged(page)).toHaveCount(1);
  await expect(rowLine(page, 'Kim Nguyen').getByText('Flagged', { exact: true })).toBeVisible();

  // The status word bold; each time in a <time>, with its non-breaking space, and 12:00 as "midday"; and the <h1>'s
  // date with one between the day and the month.
  await expect(rowLine(page, 'Jane Citizen').getByText('Draft', { exact: true })).toHaveCSS('font-weight', '700');
  await expect(rowLine(page, 'Jane Citizen').locator('time')).toHaveAttribute('datetime', at942);
  expect(await rowLine(page, 'Jane Citizen').locator('time').textContent()).toBe('9:42 am');
  await expect(rowLine(page, 'Kim Nguyen').locator('time')).toHaveAttribute('datetime', at412);
  expect(await rowLine(page, 'Lee Walker').locator('time').textContent()).toBe('midday');
  expect(await heading(page).locator('time').textContent()).toBe('Thursday 1 October');
  await expectAxeToPass(page);

  // "Alex" matches the participant named Alex, never a row only because Alex P. wrote its note.
  await findField(page).fill('Alex');
  await expect(rows(page)).toHaveText(['Alex Morgan']);
  await expectNothingKept(page, 'Alex', 'Jane', 'Kim', 'Mia', 'Lee', 'Ravi');
});

test('after Submit, the banner before the <h1> is focused, the row Submitted from the fresh list, then gone', async ({
  page,
}) => {
  const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] } });
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, store.todayNotes(worker));
  await store.serve(page, worker);
  await page.goto('/');
  await row(page, 'Jane Citizen').click();
  const notesBox = main(page).getByRole('textbox', { name: '3. Guided notes' });
  await notesBox.pressSequentially('Went to the shops.');
  await expect(page.locator('#save-status')).toHaveText('Saved 4:12 pm');
  await main(page).getByRole('button', { name: 'Submit note', exact: true }).click();
  const dialog = page.getByRole('alertdialog');
  await expect(dialog.getByRole('heading', { level: 2 })).toBeFocused();

  // Confirmed, with the next list held: Today, popped to, with the banner focused before the <h1> and no row until the
  // server's list answers, so no row built from before ever says Draft.
  const heldList = await holdNext(page, '**/api/today');
  await dialog.getByRole('button', { name: 'Submit note for Jane Citizen' }).click();
  const list = await heldList();
  await expect(page).toHaveURL('/');
  await expect(banner(page)).toBeFocused();
  await expect(banner(page)).toHaveText('Note for Jane Citizen submitted');
  await expect(banner(page)).not.toHaveAttribute('role');
  await expect(banner(page)).not.toHaveAttribute('aria-live');
  expect(
    await banner(page).evaluate((element) => {
      const h1 = document.querySelector('main h1');
      return h1 !== null && (element.compareDocumentPosition(h1) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
    }),
    'the banner before the <h1>',
  ).toBe(true);
  await expect(rows(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await list.fallback();
  await expect.poll(() => shownText(rowLine(page, 'Jane Citizen'))).toBe('Submitted · You · 4:12 pm');
  await expect(rowLine(page, 'Jane Citizen')).not.toContainText('Draft');
  await expect(banner(page)).toBeFocused();
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Citizen', 'Went to the shops', 'submitted');

  // Gone after leaving Today, by the row, which opens the read view now, and coming back by its back link.
  await row(page, 'Jane Citizen').click();
  await expect(heading(page)).toHaveText('Jane Citizen Thursday 1 October 2026');
  await backLink(page).click();
  await expect(row(page, 'Jane Citizen')).toBeFocused();
  await expect(banner(page)).toHaveCount(0);

  // Opened by its address rather than from Today, the form is put in Today's place: Back never reopens it.
  await page.goto(`/participants/${ravi.id}/notes/${worker.today}`);
  await notesBox.pressSequentially('Read the paper.');
  await expect(page.locator('#save-status')).toHaveText('Saved 4:12 pm');
  const entries = await page.evaluate(() => history.length);
  await main(page).getByRole('button', { name: 'Submit note', exact: true }).click();
  await dialog.getByRole('button', { name: 'Submit note for Ravi de Silva' }).click();
  await expect(page).toHaveURL('/');
  await expect(banner(page)).toBeFocused();
  await expect(banner(page)).toHaveText('Note for Ravi de Silva submitted');
  expect(await page.evaluate(() => history.length)).toBe(entries);
  await page.reload();
  await expect(rows(page)).toHaveCount(2);
  await expect(banner(page)).toHaveCount(0);
  await expectNothingKept(page, 'Jane', 'Ravi', 'Read the paper', 'submitted');
});

test('at 320 px with text at 200%, the status lines wrap inside their rows and Flagged drops to its own line', async ({
  page,
}) => {
  const longAuthor = 'W'.repeat(100);
  const notes: Record<string, TodayNote> = {
    [jane.id]: todayNote({
      status: 'Submitted',
      authorDisplayName: longAuthor,
      firstSubmittedAtUtc: at412,
      isFlagged: true,
    }),
    [ravi.id]: todayNote({ status: 'Submitted', isMine: true, firstSubmittedAtUtc: at412, isFlagged: true }),
  };
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, (id) => notes[id] ?? null);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/');
  await setTextTo200Percent(page);
  await expect(flagged(page)).toHaveCount(2);
  expect(await scrollsSideways(page), 'status lines with an author of 100 letters and no space').toBe(false);
  for (const name of ['Jane Citizen', 'Ravi de Silva']) {
    const time = await boxOf(rowLine(page, name).locator('time'));
    const tag = await boxOf(rowLine(page, name).getByText('Flagged', { exact: true }));
    const line = await boxOf(rowLine(page, name));
    expect(tag.y, `${name}'s tag under the time`).toBeGreaterThanOrEqual(time.y + time.height - 1);
    expect(Math.round(tag.x), `${name}'s tag at the start of its line`).toBe(Math.round(line.x));
    expect(line.x + line.width, `${name}'s line inside the page`).toBeLessThanOrEqual(320);
  }
});

test('in forced colours, the Flagged tag keeps its border, in the text colour', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const notes: Record<string, TodayNote> = {
    [jane.id]: todayNote({ status: 'Submitted', firstSubmittedAtUtc: at412, isFlagged: true }),
  };
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, (id) => notes[id] ?? null);
  await page.goto('/');
  const canvasText = await systemColour(page, 'CanvasText');
  await expect(flagged(page)).toHaveCSS('border-top-style', 'solid');
  await expect(flagged(page)).toHaveCSS('border-top-color', canvasText);
  expect(parseFloat(await flagged(page).evaluate((tag) => getComputedStyle(tag).borderTopWidth))).toBeGreaterThan(0);
});

// Your unfinished drafts (S02.01.04, today.md component 5), from the worker's own drafts in a store (notes.ts), each
// started at 9:14 am and saved at 9:42 am on its day in Melbourne, with made-up words (A35): Jane Citizen's from
// yesterday, Ben Archer's from the day before, Ben being archived since, and Jane's for today, which her row on the
// list shows instead; and a pending edit of Ravi de Silva's note of 28 September, which T14 builds, given beside them.

// The section's <h2>, then its rows, each a return point by its participant and date, and the list's rows, each by
// its participant alone.
const draftsHeading = (page: Page) =>
  main(page).getByRole('heading', { level: 2, name: 'Your unfinished drafts', exact: true });
const draftRows = (page: Page) => main(page).locator('a[data-return-key*="/"]');
const draftRow = (page: Page, name: string) => draftRows(page).filter({ hasText: name });
const draftItems = (page: Page) =>
  main(page).getByRole('listitem').filter({ has: page.locator('a[data-return-key*="/"]') });
const listRows = (page: Page) => main(page).locator('a[data-return-key]:not([data-return-key*="/"])');
const listLine = (page: Page, name: string) =>
  main(page)
    .getByRole('listitem')
    .filter({ has: page.locator('a[data-return-key]:not([data-return-key*="/"])', { hasText: name }) })
    .locator('p');
const notesBox = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });

/** The address of `participantId`'s note for `noteDate`. */
const noteAddress = (participantId: string, noteDate: string) => `/participants/${participantId}/notes/${noteDate}`;

/**
 * Puts `me`'s own draft of a participant's note for `noteDate` in the store, as a page of theirs had started it at
 * 9:14 am that day in Melbourne and saved a sentence at 9:42 am, before daylight saving starts on 4 October.
 */
function addOwnDraft(store: NotesStore, participantId: string, noteDate: string, me: Me = worker) {
  const theDayBefore = daysAfter(noteDate, -1);
  const startedBy = { authorDisplayName: me.displayName, startedAtUtc: `${theDayBefore}T23:14:00Z` };
  store.startElsewhere(participantId, noteDate, startedBy, me.userId);
  store.saveElsewhere(participantId, noteDate, {
    narrative: 'Walked to the shop.',
    savedAtUtc: `${theDayBefore}T23:42:00Z`,
  });
}

/** The worker's pending edit of Ravi's submitted note of 28 September, saved at 4:12 pm that day. */
const ravisEdit: MyDraft = {
  participantId: ravi.id,
  givenName: ravi.givenName,
  familyName: ravi.familyName,
  noteDate: '2026-09-28',
  kind: 'pendingEdit',
  savedAtUtc: '2026-09-28T06:12:00Z',
};

/** The worker's drafts of Jane's yesterday, Ben's of the day before and Jane's today, in a store of their own. */
function ownDrafts(): NotesStore {
  const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] } });
  addOwnDraft(store, jane.id, '2026-09-30');
  addOwnDraft(store, ben.id, '2026-09-29');
  addOwnDraft(store, jane.id, worker.today);
  return store;
}

/** Opens Today as the worker with `store`'s notes and drafts, and `more` beside them, and waits for the section. */
async function openTodayWithDrafts(page: Page, store: NotesStore, more: readonly MyDraft[] = []) {
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants, store.todayNotes(worker));
  await serveMyDrafts(page, () => [...store.myDrafts(worker)(), ...more]);
  await store.serve(page, worker);
  await page.goto('/');
  await expect(draftsHeading(page)).toBeVisible();
}

test('Your unfinished drafts: yesterday’s, an archived person’s and a pending edit’s rows above the list, axe', async ({
  page,
}) => {
  const store = ownDrafts();
  await openTodayWithDrafts(page, store, [ravisEdit]);

  // After the <h1>, the section's <h2>, then a row for each draft or pending edit that the list does not show, by
  // family name, given name, then date: each named by the participant alone and described by its date, with no status,
  // time or tag, and nothing to say that Ben is archived. Jane's draft for today is her row's on the list.
  await expect(draftRows(page)).toHaveText(['Ben Archer', 'Jane Citizen', 'Ravi de Silva']);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(['Your unfinished drafts', 'Participants']);
  const dates = [
    { name: 'Ben Archer', noteDate: '2026-09-29', date: 'Tuesday 29 September 2026' },
    { name: 'Jane Citizen', noteDate: '2026-09-30', date: 'Wednesday 30 September 2026' },
    { name: 'Ravi de Silva', noteDate: '2026-09-28', date: 'Monday 28 September 2026' },
  ];
  for (const { name, noteDate, date } of dates) {
    const item = draftItems(page).filter({ hasText: name });
    // The date with its non-breaking space, which a pattern does not count as a space.
    const described = new RegExp(`^${date.replace(/ (\w+ 2026)$/, '\\s$1')}$`);
    await expect(draftRow(page, name)).toHaveAccessibleDescription(described);
    await expect(item.locator('time')).toHaveAttribute('datetime', noteDate);
    // The link's words, then the line's, and nothing more.
    await expect.poll(() => shownText(item)).toBe(`${name}${date}`);
  }

  // Each opens the person's own working copy: a draft at the note's address, a pending edit at its edit form.
  await expect(draftRow(page, 'Ben Archer')).toHaveAttribute('href', noteAddress(ben.id, '2026-09-29'));
  await expect(draftRow(page, 'Jane Citizen')).toHaveAttribute('href', noteAddress(jane.id, '2026-09-30'));
  await expect(draftRow(page, 'Ravi de Silva')).toHaveAttribute('href', `${noteAddress(ravi.id, '2026-09-28')}/edit`);
  await expect(listRows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect.poll(() => shownText(listLine(page, 'Jane Citizen'))).toBe('Draft · You · saved 9:42 am');
  expect(
    await draftsHeading(page).evaluate((element) => {
      const searchBox = document.querySelector('main search');
      const after = Node.DOCUMENT_POSITION_FOLLOWING;
      return searchBox !== null && (element.compareDocumentPosition(searchBox) & after) !== 0;
    }),
    'the section before the search box',
  ).toBe(true);
  await expect(page).toHaveTitle(title('Today'));
  await expectAxeToPass(page);

  // Find a participant filters the list alone: the section stays as it is, for a match and for none.
  await findField(page).fill('ravi');
  await expect(listRows(page)).toHaveText(['Ravi de Silva']);
  await expect(draftRows(page)).toHaveText(['Ben Archer', 'Jane Citizen', 'Ravi de Silva']);
  await findField(page).fill('zzz');
  await expect(sentence(page, 'No participant matches ‘zzz’.')).toBeVisible();
  await expect(draftRows(page)).toHaveText(['Ben Archer', 'Jane Citizen', 'Ravi de Silva']);
  await expectAxeToPass(page);
  await expectNothingKept(page, 'Jane', 'Ben', 'Archer', 'Ravi', 'zzz', 'Walked to the shop');

  // With no participants on the list, Ben's draft shows beside "No participants yet.", with no search box.
  await serveToday(page, worker.today, []);
  await serveMyDrafts(page, () => store.myDrafts(worker)().filter((draft) => draft.participantId === ben.id));
  await page.goto('/');
  await expect(draftRows(page)).toHaveText(['Ben Archer']);
  await expect(sentence(page, 'No participants yet.')).toBeVisible();
  await expect(search(page)).toHaveCount(0);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(['Your unfinished drafts', 'Participants']);
  await expectAxeToPass(page);

  // With nothing left once Jane's for today is dropped, no section and no <h2> of it.
  await serveToday(page, worker.today, participants, store.todayNotes(worker));
  await serveMyDrafts(page, () => store.myDrafts(worker)().filter((draft) => draft.noteDate === worker.today));
  await page.goto('/');
  await expect(listRows(page)).toHaveText(['Jane Citizen', 'Ravi de Silva']);
  await expect(draftsHeading(page)).toHaveCount(0);
  await expect(draftRows(page)).toHaveCount(0);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(['Participants']);
});

test('a row of Your unfinished drafts opens its draft, an archived person’s too, and Back focuses that row', async ({
  page,
}) => {
  await openTodayWithDrafts(page, ownDrafts());

  // Yesterday's draft of Jane's, with its words, then the back link: the section's row is focused, not the list's.
  await draftRow(page, 'Jane Citizen').click();
  await expect(page).toHaveURL(noteAddress(jane.id, '2026-09-30'));
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
  await expect(notesBox(page)).toHaveValue('Walked to the shop.');
  await backLink(page).click();
  await expect(draftRow(page, 'Jane Citizen')).toBeFocused();

  // Ben's, though he is archived since: the draft's form, not the dead end that a new note of his would meet; then
  // browser Back.
  await draftRow(page, 'Ben Archer').click();
  await expect(heading(page)).toHaveText('Ben Archer Tuesday 29 September 2026');
  await expect(notesBox(page)).toHaveValue('Walked to the shop.');
  await expect(notesBox(page)).toBeEditable();
  await expect(page.locator('#page-bar').getByRole('region')).toHaveCount(0);
  await page.goBack();
  await expect(draftRow(page, 'Ben Archer')).toBeFocused();
  await expectNothingKept(page, 'Jane', 'Ben', 'Archer', 'Walked to the shop');
});

test('at 320 px with text at 200%, Your unfinished drafts never scrolls sideways, and a long name wraps whole', async ({
  page,
}) => {
  const unbroken = madeUpParticipant('7e3a9c2f-6b05-4d48-b2f7-0c5e1a8d4f63', 'A'.repeat(50), 'B'.repeat(50));
  const store = notesStore({ today: worker.today, participants: [jane, unbroken, ravi] });
  addOwnDraft(store, unbroken.id, '2026-09-30');
  await serveMe(page, worker);
  await serveToday(page, worker.today, [jane, unbroken, ravi], store.todayNotes(worker));
  await serveMyDrafts(page, store.myDrafts(worker));
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/');
  await setTextTo200Percent(page);
  await expect(draftRows(page)).toHaveCount(1);
  expect(await scrollsSideways(page), 'the section, with a name of 100 letters and no space').toBe(false);
  for (const part of [draftRows(page), draftItems(page).locator('p')]) {
    expect(await part.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
  }

  // And beside "No participants yet.".
  await serveToday(page, worker.today, []);
  await page.goto('/');
  await expect(draftRows(page)).toHaveCount(1);
  expect(await scrollsSideways(page), 'the section with no participants').toBe(false);
});

test('targets: Your unfinished drafts’ rows 56 px, and nothing pinned, on phone and laptop', async ({ page }) => {
  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await openTodayWithDrafts(page, ownDrafts(), [ravisEdit]);
    await expectAtLeast(draftItems(page), 44, 56);
    expect(await pinned(page), `Today with the section at ${width} px`).toEqual([]);
  }
});

test('in forced colours, the focus ring shows in Highlight on each row of Your unfinished drafts', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await openTodayWithDrafts(page, ownDrafts(), [ravisEdit]);
  const forcedRing = { ...ring, colour: await systemColour(page, 'Highlight') };
  await heading(page).focus();
  await expectStops(page, 'Tab', ['Ben Archer', 'Jane Citizen', 'Ravi de Silva', 'Find a participant'], forcedRing);
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: Your unfinished drafts' rows before Find a participant, and back to one`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await openTodayWithDrafts(page, ownDrafts(), [ravisEdit]);

    // today.md *Tab order*: the header's, then the section's rows, then Find a participant and the list's rows.
    const shell = ['Skip to main content', 'Grow2Notes', 'Today', 'Account'];
    const drafts = ['Ben Archer', 'Jane Citizen', 'Ravi de Silva'];
    const list = ['Find a participant', 'Jane Citizen', 'Ravi de Silva'];
    await expectStops(page, 'Tab', [...shell, ...drafts, ...list, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [...shell, ...drafts, ...list].reverse());

    // Enter on the section's Jane Citizen opens yesterday's draft, its <h1> focused; the back link, by Enter, comes
    // back to that row.
    await expectStops(page, 'Tab', ['Grow2Notes', 'Today', 'Account', 'Ben Archer', 'Jane Citizen']);
    await page.keyboard.press('Enter');
    await expect(heading(page)).toBeFocused();
    await expect(heading(page)).toHaveText('Jane Citizen Wednesday 30 September 2026');
    await expectStops(page, 'Shift+Tab', ['Today']);
    await page.keyboard.press('Enter');
    await expect(draftRow(page, 'Jane Citizen')).toBeFocused();
    await expectStops(page, 'Tab', ['Ravi de Silva', 'Find a participant']);
    await expectNothingKept(page, 'Jane', 'Ben', 'Archer', 'Ravi', 'Walked to the shop');
  });
}
