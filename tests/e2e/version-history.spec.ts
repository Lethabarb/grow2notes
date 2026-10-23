import type { Page, Route } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops, ring } from './focus.ts';
import { expectNothingKept } from './kept.ts';
import { scrollsSideways, setTextTo200Percent, shownText, systemColour } from './layout.ts';
import { hiddenLiveRegions, watchLiveRegions } from './liveRegions.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { jane } from './participants.ts';
import { holdNext, recordRequests } from './requests.ts';

// A manager's version history, pages A and B, in the published app (S02.07.01, S02.07.02, S02.08.01, S02.08.02),
// against version-history.md's criteria and the read view's past-day line, as T18 builds them. Each test answers
// /api/auth/me, the participant's header, their note and its versions in the browser, with made-up people and a note
// that Priya Nair submitted and Sam Lee edited twice (me.ts, below), so that none of those requests reaches the app.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
// The version list's links, return points by their version number, and the list items that hold them.
const rows = (page: Page) => main(page).locator('a[data-return-key]');
const rowLine = (page: Page, index: number) => main(page).getByRole('listitem').nth(index).locator('p');
const versionHistoryLink = (page: Page) => main(page).getByRole('link', { name: 'Version history', exact: true });
// A line under the <h1> or the currency line, found by what it starts with.
const lineStarting = (page: Page, start: RegExp) => main(page).locator('p').filter({ hasText: start });
// The region's line, beside the page's visually hidden status region.
const loadLine = (page: Page) => main(page).locator('p[role="status"]:not(.visually-hidden)');

/** The names of the rows' links as Tab reaches them: each version and its description, as they show. */
const rowNames = (page: Page) =>
  rows(page).evaluateAll((links) => links.map((link) => (link as HTMLElement).innerText.replace(/\s+/g, ' ').trim()));

/** How each tick icon is drawn, as it shows: the stroke of its box and, when it is ticked, of its tick. */
const tickStrokes = (page: Page) =>
  main(page)
    .locator('ul[role="list"] svg')
    .evaluateAll((icons) =>
      icons.map((icon) => ({
        box: getComputedStyle(icon.querySelector('rect')!).stroke,
        tick: icon.querySelector('path') === null ? null : getComputedStyle(icon.querySelector('path')!).stroke,
      })),
    );

const noteDate = manager.today;
const noteAddress = `/participants/${jane.id}/notes/${noteDate}`;
const listAddress = `${noteAddress}/versions`;
const versionAddress = (n: number) => `${listAddress}/${n}`;

/** Jane's note for 1 October 2026 in the <h1>, and the same under the version pages' caption. */
const janesHeading = 'Jane Citizen Thursday 1 October 2026';

type Author = { id: string; displayName: string };

const priya: Author = { id: 'b7a1c0de-0000-4000-8000-000000000001', displayName: 'Priya Nair' };
const sam: Author = { id: manager.userId, displayName: manager.displayName };
const jo: Author = { id: 'b7a1c0de-0000-4000-8000-000000000003', displayName: 'Jo Smith' };

/** A version of the made-up note: who saved it and when, its wording, its goals' ticks, its groups and its flag. */
type FakeVersion = {
  createdBy: Author;
  createdAtUtc: string;
  narrative: string;
  tickedGoals: boolean[];
  groups: { name: string; items: { text: string; isTicked: boolean }[] }[];
  flagReason: string | null;
};

type FakeNote = {
  author: Author;
  startedAtUtc: string;
  isPastDayNote: boolean;
  /** Version 1 first. */
  versions: FakeVersion[];
};

const goalTexts = ['Makes own breakfast', 'Catches the bus to day program'];

const outing = { name: 'Community outing', items: [{ text: 'Travelled by bus or train', isTicked: true }] };
const personalCare = { name: 'Personal care', items: [{ text: 'Showered with prompting', isTicked: true }] };

/** What version 1 was, as Priya submitted it at 4:12 pm: the outing picked, and no flag. */
const submitted: FakeVersion = {
  createdBy: priya,
  createdAtUtc: '2026-10-01T06:12:00Z',
  narrative: 'Jane made toast.\nThen a walk to the shops.',
  tickedGoals: [true, false],
  groups: [outing],
  flagReason: null,
};

/** Version 2, Sam's, at 4:40 pm: Personal care picked in place of the outing, and the note flagged. */
const firstEdit: FakeVersion = {
  createdBy: sam,
  createdAtUtc: '2026-10-01T06:40:00Z',
  narrative: 'Jane made toast.\nThen a walk to the shops, and her knee was sore.',
  tickedGoals: [true, true],
  groups: [personalCare],
  flagReason: 'Mentioned pain in her left knee after the walk.',
};

/** Version 3, Sam's, at 5:03 pm: the flag taken off, the same group. */
const secondEdit: FakeVersion = { ...firstEdit, createdAtUtc: '2026-10-01T07:03:00Z', flagReason: null };

const edited: FakeNote = {
  author: priya,
  startedAtUtc: '2026-10-01T05:00:00Z',
  isPastDayNote: false,
  versions: [submitted, firstEdit, secondEdit],
};

/** A note that Jo Smith, a manager, wrote on Saturday 3 October for Thursday 1 October, submitted once. */
const pastDay: FakeNote = {
  author: jo,
  startedAtUtc: '2026-10-03T00:14:00Z',
  isPastDayNote: true,
  versions: [{ ...submitted, createdBy: jo, createdAtUtc: '2026-10-03T00:40:00Z' }],
};

/** A version as `current` of the note gives it (design.md §6.3). */
const currentOf = ({ narrative, tickedGoals, groups, flagReason }: FakeVersion, versionNumber: number) => ({
  versionNumber,
  narrative,
  goals: goalTexts.map((text, index) => ({ text, isTicked: tickedGoals[index] })),
  commonItemGroups: groups,
  isFlagged: flagReason !== null,
  flagReason,
});

/** A version as `GET {base}/versions/{n}` gives it: the same, and who saved it and when. */
const fullVersion = (version: FakeVersion, versionNumber: number) => ({
  ...currentOf(version, versionNumber),
  createdBy: version.createdBy,
  createdAtUtc: version.createdAtUtc,
});

/**
 * Answers `GET` of Jane's header, her note for 1 October 2026 as a manager gets it, its version list, newest first, and
 * each version, in the browser, from `note`. A later call overrides it, as Playwright tries the latest route first.
 */
async function serveNote(page: Page, note: FakeNote) {
  const { versions } = note;
  const latest = versions.at(-1)!;
  const own = `/api/participants/${jane.id}`;
  const noteBody = {
    status: 'Submitted',
    author: note.author,
    startedAtUtc: note.startedAtUtc,
    firstSubmittedAtUtc: versions[0].createdAtUtc,
    isPastDayNote: note.isPastDayNote,
    ...(versions.length > 1
      ? { isEdited: true, lastEdit: { by: latest.createdBy.displayName, atUtc: latest.createdAtUtc } }
      : { isEdited: false }),
    current: currentOf(latest, versions.length),
    flagStatus: 'None',
    reviews: [],
    versionCount: versions.length,
  };
  const list = versions
    .map(({ createdBy, createdAtUtc, flagReason }, index) => ({
      versionNumber: index + 1,
      createdBy,
      createdAtUtc,
      isFlagged: flagReason !== null,
    }))
    .reverse();
  const header = { id: jane.id, givenName: jane.givenName, familyName: jane.familyName, status: 'Active' };
  const versionPath = new RegExp(`^${own}/notes/${noteDate}/versions/([0-9]+)$`);

  await page.route(
    (url) => url.pathname.startsWith('/api/participants/'),
    (route) => {
      const { pathname } = new URL(route.request().url());
      const number = Number(versionPath.exec(pathname)?.[1]);
      if (pathname === own) {
        return route.fulfill({ json: header });
      }

      if (pathname === `${own}/notes/${noteDate}`) {
        return route.fulfill({ json: noteBody });
      }

      if (pathname === `${own}/notes/${noteDate}/versions`) {
        return route.fulfill({ json: list });
      }

      if (number >= 1 && number <= versions.length) {
        return route.fulfill({ json: fullVersion(versions[number - 1], number) });
      }

      return route.fulfill({ status: 404, json: { type: 'about:blank', title: 'Not found', status: 404 } });
    },
  );
}

/** Signs `me` in with the made-up note served, and opens the address. */
async function open(page: Page, address: string, note: FakeNote = edited, me: Me = manager) {
  await serveMe(page, me);
  await serveNote(page, note);
  await page.goto(address);
}

let liveRegionsAddedWithWords: string[] = [];

test.beforeEach(async ({ page }) => {
  liveRegionsAddedWithWords = await watchLiveRegions(page, '/');
});

test.afterEach(async ({ page }) => {
  expect(liveRegionsAddedWithWords, 'live regions put into the page with their words').toEqual([]);
  expect(await hiddenLiveRegions(page), 'live regions with display: none').toEqual([]);
});

test('the list opens from the read view; a version has its own groups and flag; both Backs; the current link', async ({
  page,
}) => {
  // A window short enough for the list to scroll, so that the place kept on Back is not the top.
  await page.setViewportSize({ width: 375, height: 300 });
  await open(page, noteAddress);
  await expect(heading(page)).toHaveText(janesHeading);
  await versionHistoryLink(page).click();

  // Page A: the <h1> focused, the current version, and the versions newest first, each with who saved it and when.
  await expect(page).toHaveURL(listAddress);
  await expect(page).toHaveTitle('Grow2Notes – Version history');
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText(`Version history ${janesHeading}`);
  await expect(main(page).getByText('The current version is version 3.', { exact: true })).toBeVisible();
  await expect(rows(page)).toHaveText(['Version 3', 'Version 2', 'Version 1']);
  await expect.poll(() => shownText(rowLine(page, 0))).toBe('Sam Lee (manager) · Thu 1 Oct 2026, 5:03 pm');
  await expect.poll(() => shownText(rowLine(page, 1))).toBe('Sam Lee (manager) · Thu 1 Oct 2026, 4:40 pm');
  await expect.poll(() => shownText(rowLine(page, 2))).toBe('Submitted · Priya Nair · Thu 1 Oct 2026, 4:12 pm');
  await expect(main(page).getByText('Submitted', { exact: true })).toHaveCount(1);
  await expect(main(page).getByText('Not edited since submit')).toHaveCount(0);
  await expect(main(page).getByText('Flagged', { exact: true })).toHaveCount(0);

  // Version 2: its own groups and its own flag, and no control but the links.
  const kept = await page.evaluate(() => {
    window.scrollTo(0, document.documentElement.scrollHeight);
    return window.scrollY;
  });
  expect(kept).toBeGreaterThan(0);
  const second = rows(page).nth(1);
  await second.click();
  await expect(page).toHaveURL(versionAddress(2));
  await expect(heading(page)).toBeFocused();
  await expect(heading(page)).toHaveText(`Version 2 ${janesHeading}`);
  await expect(lineStarting(page, /^This is an earlier version/)).toHaveText(
    'This is an earlier version. The current version is version 3.',
  );
  await expect
    .poll(() => shownText(lineStarting(page, /^Saved by/)))
    .toBe('Saved by Sam Lee (manager), Thu 1 Oct 2026, 4:40 pm');
  await expect.poll(() => shownText(lineStarting(page, /^Flagged/))).toBe(
    'Flagged Reason: Mentioned pain in her left knee after the walk.',
  );
  await expect(main(page).getByRole('heading', { level: 3, name: 'Personal care' })).toBeVisible();
  await expect(main(page).getByText('Community outing')).toHaveCount(0);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByRole('link')).toHaveCount(1);

  // The back link and browser Back both return to the list where it was, with the row opened focused.
  for (const [way, goBack] of [
    ['the back link', () => backLink(page, 'Version history').click()],
    ['browser Back', () => page.goBack()],
  ] as const) {
    await goBack();
    await expect(page, way).toHaveURL(listAddress);
    await expect(second, way).toBeFocused();
    expect(await page.evaluate(() => window.scrollY), way).toBe(kept);
    if (way === 'the back link') {
      await second.click();
      await expect(page).toHaveURL(versionAddress(2));
    }
  }

  // The link to the current version: version 3, which is current, unflagged, and has the same group.
  await second.click();
  await main(page).getByRole('link', { name: 'version 3', exact: true }).click();
  await expect(page).toHaveURL(versionAddress(3));
  await expect(heading(page)).toHaveText(`Version 3 ${janesHeading}`);
  await expect(lineStarting(page, /^This is the current version/)).toHaveText('This is the current version.');
  await expect(main(page).getByRole('link')).toHaveCount(0);
  await expect(main(page).getByText('Flagged', { exact: true })).toHaveCount(0);
  await expect(main(page).getByRole('heading', { level: 3, name: 'Personal care' })).toBeVisible();

  // Version 1: the outing, the first submit, and no flag.
  await page.goto(versionAddress(1));
  await expect(heading(page)).toHaveText(`Version 1 ${janesHeading}`);
  await expect.poll(() => shownText(lineStarting(page, /^Submitted by/))).toBe(
    'Submitted by Priya Nair, Thu 1 Oct 2026, 4:12 pm',
  );
  await expect(main(page).getByRole('heading', { level: 3, name: 'Community outing' })).toBeVisible();
  await expect(main(page).getByText('Personal care')).toHaveCount(0);

  // Nothing of the note is kept where the page could keep it.
  await expectNothingKept(page, 'Jane', 'Citizen', 'Priya', 'Sam Lee');
});

test('one version says "Not edited since submit"; a past-day note has its past-day line on both pages', async ({
  page,
}) => {
  await open(page, listAddress, pastDay);
  await expect(heading(page)).toHaveText(`Version history ${janesHeading}`);
  await expect(main(page).getByText('The current version is version 1.', { exact: true })).toBeVisible();
  await expect(main(page).getByText('Not edited since submit', { exact: true })).toBeVisible();
  await expect(rows(page)).toHaveText(['Version 1']);
  await expect.poll(() => shownText(lineStarting(page, /^Past-day note/))).toBe(
    'Past-day note written by Jo Smith (manager) on Sat 3 Oct 2026, 10:14 am',
  );
  await rows(page).click();
  await expect(heading(page)).toHaveText(`Version 1 ${janesHeading}`);
  await expect.poll(() => shownText(lineStarting(page, /^Past-day note/))).toBe(
    'Past-day note written by Jo Smith (manager) on Sat 3 Oct 2026, 10:14 am',
  );
  await expect.poll(() => shownText(lineStarting(page, /^Submitted by/))).toBe(
    'Submitted by Jo Smith, Sat 3 Oct 2026, 10:40 am',
  );

  // The read view puts the same line where its byline was.
  await page.goto(noteAddress);
  await expect.poll(() => shownText(lineStarting(page, /^Past-day note/))).toBe(
    'Past-day note written by Jo Smith (manager) on Sat 3 Oct 2026, 10:14 am',
  );
  await expect(lineStarting(page, /^Written by/)).toHaveCount(0);

  await page.goto(listAddress);
  await expect(main(page).getByText('Not edited since submit', { exact: true })).toBeVisible();
  await expectAxeToPass(page);
});

test('a worker who types either address gets Page not found, and no request for the note is sent', async ({ page }) => {
  await serveMe(page, worker);
  await serveNote(page, edited);
  const sent = recordRequests(page);

  for (const address of [listAddress, versionAddress(2)]) {
    await page.goto(address);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(page).toHaveTitle('Grow2Notes – Page not found');
  }

  expect((await sent()).filter((request) => request.includes('/api/participants'))).toEqual([]);
  await expectAxeToPass(page);
});

test('a version address that is not a whole number from 1 is Page not found, and sends nothing', async ({ page }) => {
  await serveMe(page, manager);
  await serveNote(page, edited);
  const sent = recordRequests(page);

  // '1.5' is left to the unit test: the server answers a dotted path itself, as a file, before the SPA loads.
  for (const segment of ['0', '02', 'abc']) {
    await page.goto(`${listAddress}/${segment}`);
    await expect(heading(page)).toHaveText('Page not found');
  }

  expect((await sent()).filter((request) => request.includes('/api/participants'))).toEqual([]);
});

test('both pages, in each state, pass axe', async ({ page }) => {
  await open(page, listAddress);
  await expect(rows(page)).toHaveCount(3);
  await expectAxeToPass(page);

  // B, an earlier version, flagged; and B, the current one.
  await page.goto(versionAddress(2));
  await expect(lineStarting(page, /^Flagged/)).toBeVisible();
  await expectAxeToPass(page);
  await page.goto(versionAddress(3));
  await expect(lineStarting(page, /^This is the current version/)).toBeVisible();
  await expectAxeToPass(page);

  await open(page, listAddress, { ...edited, versions: [submitted] });
  await expect(main(page).getByText('Not edited since submit', { exact: true })).toBeVisible();
  await expectAxeToPass(page);
});

test('at 320 px with text at 200%, neither page scrolls sideways', async ({ page }) => {
  const unbroken = 'Wolfeschlegelsteinhausenbergerdorff'.repeat(3);
  const longAuthor = { ...priya, displayName: 'X'.repeat(100) };
  const long: FakeNote = {
    ...edited,
    author: longAuthor,
    versions: [
      { ...submitted, createdBy: longAuthor, narrative: `${unbroken}\n${'Slept in. '.repeat(500)}` },
      { ...firstEdit, flagReason: 'Y'.repeat(200) },
      secondEdit,
    ],
  };
  await page.setViewportSize({ width: 320, height: 640 });
  await open(page, listAddress, long);
  await setTextTo200Percent(page);
  await expect(rows(page)).toHaveCount(3);
  expect(await scrollsSideways(page), 'the version list, with a long saver').toBe(false);

  for (const n of [1, 2, 3]) {
    await page.goto(versionAddress(n));
    await expect(heading(page)).toContainText(`Version ${n}`);
    expect(await scrollsSideways(page), `version ${n}, with a long saver, reason and text`).toBe(false);
  }
});

for (const width of [375, 1280]) {
  test(`keyboard only at ${width} px: the back link, each version, a version's link, and back to its row`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await open(page, listAddress);
    await expect(rows(page)).toHaveCount(3);
    await heading(page).focus();

    // Page A: the back link before <main>, then one stop for each version, newest first, and no other.
    const names = await rowNames(page);
    await expectStops(page, 'Shift+Tab', ['Note']);
    await expectStops(page, 'Tab', [...names, 'outside the page']);
    await expectStops(page, 'Shift+Tab', [names[2], names[1]]);

    // Enter opens version 2, its <h1> focused; its back link, then the link to the current version, and no other stop.
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(versionAddress(2));
    await expect(heading(page)).toBeFocused();
    await expectStops(page, 'Shift+Tab', ['Version history']);
    await expectStops(page, 'Tab', ['version 3', 'outside the page']);
    await expectStops(page, 'Shift+Tab', ['version 3', 'Version history']);

    // Enter on the back link returns to the list, focus on the row that was open.
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(listAddress);
    await expect(rows(page).nth(1)).toBeFocused();
    await expectStops(page, 'Tab', [names[2], 'outside the page']);
  });
}

test('in forced colours, the ring is Highlight at each stop of both pages, and the boxes and ticks are drawn', async ({
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await open(page, listAddress);
  await expect(rows(page)).toHaveCount(3);
  const [highlight, canvasText] = await Promise.all([
    systemColour(page, 'Highlight'),
    systemColour(page, 'CanvasText'),
  ]);
  expect(highlight).not.toBe(ring.colour);
  const forcedRing = { ...ring, colour: highlight };

  await heading(page).focus();
  const names = await rowNames(page);
  await expectStops(page, 'Shift+Tab', ['Note'], forcedRing);
  await expectStops(page, 'Tab', names, forcedRing);

  // Enter on the last stop opens version 1: two goals, one ticked, and the outing's one item, ticked.
  await page.keyboard.press('Enter');
  await expect(heading(page)).toHaveText(`Version 1 ${janesHeading}`);
  await expect(heading(page)).toBeFocused();
  await expectStops(page, 'Shift+Tab', ['Version history'], forcedRing);
  await expectStops(page, 'Tab', ['version 3'], forcedRing);
  expect(await tickStrokes(page), 'the box of each tick icon, and the tick of each ticked one').toEqual([
    { box: canvasText, tick: canvasText },
    { box: canvasText, tick: null },
    { box: canvasText, tick: canvasText },
  ]);
});

test('both pages, while they load and when they do not, pass axe', async ({ page }) => {
  await serveMe(page, manager);
  await serveNote(page, edited);
  const own = `/api/participants/${jane.id}/notes/${noteDate}/versions`;
  const listRequest = (url: URL) => url.pathname === own;
  const versionRequest = (url: URL) => url.pathname === `${own}/2`;
  const refuse = (route: Route) => route.abort('internetdisconnected');

  // A, waiting: the loading line after 1 s and no row.
  const heldList = await holdNext(page, listRequest);
  await page.goto(listAddress);
  const waitingList = await heldList();
  await expect(loadLine(page)).toHaveText('Loading version history…');
  await expect(rows(page)).toHaveCount(0);
  await expectAxeToPass(page);
  await waitingList.fallback();
  await expect(rows(page)).toHaveCount(3);

  // B, waiting.
  const heldVersion = await holdNext(page, versionRequest);
  await page.goto(versionAddress(2));
  const waitingVersion = await heldVersion();
  await expect(loadLine(page)).toHaveText('Loading version 2…');
  await expectAxeToPass(page);
  await waitingVersion.fallback();
  await expect(heading(page)).toHaveText(`Version 2 ${janesHeading}`);

  // A, failed: the sentence, the page's name as the <h1>, and Try again.
  await page.route(listRequest, refuse);
  await page.goto(listAddress);
  await expect(loadLine(page)).toHaveText('Version history did not load: no connection. Try again.');
  await expect(main(page).getByRole('button', { name: 'Try again' })).toBeVisible();
  await expectAxeToPass(page);
  await page.unroute(listRequest, refuse);

  // B, failed.
  await page.route(versionRequest, refuse);
  await page.goto(versionAddress(2));
  await expect(loadLine(page)).toContainText('did not load: no connection. Try again.');
  await expect(main(page).getByRole('button', { name: 'Try again' })).toBeVisible();
  await expectAxeToPass(page);
});
