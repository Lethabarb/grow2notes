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
import { notesStore } from './notes.ts';
import { jane, madeUpParticipant, participants, ravi } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';
import { failToday, serveToday, todayList } from './today.ts';

// Today in the published app (S02.01.01, S02.01.02), against today.md's criteria: its states, the device's own clock
// and zone, Find a participant, reflow, targets, the focus ring and the keyboard alone. Each test answers
// /api/auth/me and /api/today in the browser (me.ts, today.ts) with made-up people (A35), and the note that a row opens
// from a store in memory (notes.ts), so that none of those requests reaches the app; note-form.spec.ts signs in for
// real and meets the app's own Today.

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
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
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
