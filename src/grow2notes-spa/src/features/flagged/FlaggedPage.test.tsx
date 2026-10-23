import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createBrowserRouter, createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import metaLine from '../../components/MetaLine.module.css';
import tagStyles from '../../components/Tag.module.css';
import { routes } from '../../routes/routes.tsx';
import type { ParticipantHeader, SubmittedNote } from '../notes/noteQueries.ts';
import type { TodayList } from '../today/todayQuery.ts';
import type { ToReviewNote } from './toReviewQuery.ts';

// A memory router and a browser router alike.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';
const reviewsPath = '/api/reviews?status=toReview';

// Flagged's two addresses: To review's, and Reviewed's, which shows To review until T19 builds Reviewed.
const flaggedAddresses = ['/flagged', '/flagged?view=reviewed'];

// flagged.md's made-up people (A35): Sam Lee, a manager, signed in, and Alex Pham, a worker, in the JSON that
// MeEndpointTests asserts. Today in Melbourne is Thursday 1 October 2026.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

/** A row as the API answers it, with the note's current version number, which no screen reads. */
type ToReviewAnswer = ToReviewNote & { currentVersionNumber: number };

// Between a day and its month, and a time and am or pm, so that neither pair splits across lines (microcopy.md §3).
const nbsp = ' ';

// flagged.md's three rows, oldest flag first, in Melbourne, which keeps AEST, UTC+10, until 4 October 2026: Sam
// Taylor's note of Tuesday 29 September, flagged at 4:42 pm that day; Jane Citizen's of Wednesday 30 September, flagged
// at 9:01 am the day after, which is still the 30th in UTC; and Lee Wong's of Thursday 1 October, flagged at 11:20 am,
// whose version 2 removed the flag.
const sam: ToReviewAnswer = {
  participantId: '4e7a2c1b-8d3f-4a65-9b20-6c1e5f7d9a38',
  givenName: 'Sam',
  familyName: 'Taylor',
  noteDate: '2026-09-29',
  authorDisplayName: 'Priya Nair',
  flagReason: 'Seemed upset about moving house.',
  flaggedAtUtc: '2026-09-29T06:42:00Z',
  currentVersionNumber: 1,
  flagRemovedLater: false,
};
const jane: ToReviewAnswer = {
  participantId: '2b8f4d6a-1c3e-4f57-a9b0-3d5e7f1a2c46',
  givenName: 'Jane',
  familyName: 'Citizen',
  noteDate: '2026-09-30',
  authorDisplayName: 'Alex P.',
  flagReason: 'Did not want to get out of bed. Said her back hurt.',
  flaggedAtUtc: '2026-09-30T23:01:00Z',
  currentVersionNumber: 1,
  flagRemovedLater: false,
};
const lee: ToReviewAnswer = {
  participantId: '9c1d3e5f-7a2b-4c68-8d0e-1f3a5b7c9d24',
  givenName: 'Lee',
  familyName: 'Wong',
  noteDate: '2026-10-01',
  authorDisplayName: 'Alex P.',
  flagReason: 'Missed lunch.',
  flaggedAtUtc: '2026-10-01T01:20:00Z',
  currentVersionNumber: 2,
  flagRemovedLater: true,
};
const flags = [sam, jane, lee];

// 11:30 pm in Melbourne on Sam's note date.
const lateOnTheDay = '2026-09-29T13:30:00Z';

/** A row's key and return key: its participant and note date. */
const keyOf = (row: ToReviewNote) => `${row.participantId}/${row.noteDate}`;

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts, with the signal's reason. */
const noAnswer: Answer = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

/** `answer`, held back until `release()` is called. */
function held(answer: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: (init) => released.then(() => answer(init)), release };
}

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each `GET`, by its path: a new answer for each. */
const answers = new Map<string, Answer>();

/** Answers each `GET` of `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** Every request for the To review list, as its path. */
function reviewRequests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string).filter((path) => path.startsWith('/api/reviews'));
}

/**
 * The list's requests as the page mounts: two, as StrictMode mounts the page, unmounts it and mounts it again, and the
 * unmount cancels the first request, whose query function takes the abort signal, so that the second mount sends it
 * again. A production build sends it once.
 */
const oneMount = [reviewsPath, reviewsPath];

/**
 * Renders the app on `router` for `me`, under StrictMode, which runs each effect twice, as `main.tsx` does, and a
 * query client of its own, in which `['me']` holds `me` already, as after the start-up.
 */
function show(router: Router, me: Me) {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
}

/**
 * Renders the app at `path` for `me` on a memory router, after the entries `before` it, as a reload keeps them while
 * the shell's memory of them has gone.
 */
function renderApp(path: string, me: Me = manager, before: readonly string[] = []) {
  // Each with a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a row or a position from one test into the next.
  const initialEntries = [...before, path].map((entry) => ({ ...parsePath(entry), key: crypto.randomUUID() }));
  const router = createMemoryRouter(routes, { initialEntries, initialIndex: before.length });
  show(router, me);
  return router;
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the query's new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** Opens `path` for `me`, and lets the list load. */
async function open(path = '/flagged', me: Me = manager) {
  const router = renderApp(path, me);
  await settle();
  return router;
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets the next screen render. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
}

/** The page becomes visible again, as the browser says so, at the document. */
function becomeVisible() {
  act(() => {
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });
}

/** The window regains focus, as the browser says so. */
function focusWindow() {
  act(() => {
    window.dispatchEvent(new FocusEvent('focus'));
  });
}

function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The region's line in `<main>`, after the list, beside the page's visually hidden status region. */
function line(): HTMLElement {
  return within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'))
    .at(-1)!;
}

/** The link named `name` in the main navigation. */
function navLink(name: string | RegExp): HTMLElement {
  return within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name });
}

/** The elements of the header that have `aria-current`, as their text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** The To review list. */
function list(): HTMLElement {
  return within(main()).getByRole('list');
}

/** The rows' links, in order. */
function rowLinks(): HTMLElement[] {
  return within(list()).getAllByRole('link');
}

/** The rows' keys, in order, from their links' return keys. */
function rowKeys(): (string | undefined)[] {
  return rowLinks().map((link) => link.dataset.returnKey);
}

/** The link of `row`'s row. */
function rowLink(row: ToReviewNote): HTMLElement {
  return rowLinks().find((link) => link.dataset.returnKey === keyOf(row))!;
}

/** The element that describes `link`, its meta line's. */
function descriptionOf(link: HTMLElement): HTMLElement {
  return document.getElementById(link.getAttribute('aria-describedby')!)!;
}

/** The plain lines of `row`'s row after its meta line, as their text. */
function extraLines(row: ToReviewNote): string[] {
  const item = rowLink(row).closest('li')!;
  const description = descriptionOf(rowLink(row));
  return Array.from(item.querySelectorAll('p'))
    .filter((paragraph) => paragraph !== description)
    .map((paragraph) => paragraph.textContent);
}

/** Scrolls the page to `y`, as the person does, with the scroll event that the browser fires. */
function scrollPage(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, configurable: true, writable: true });
  window.dispatchEvent(new Event('scroll'));
}

/** The back link, the one link of the top bar in the shell's before-main bar slot. */
function backLink(): HTMLElement {
  return within(document.getElementById('page-bar')!).getByRole('link');
}

/** The shared note address of `row`'s note. */
function notePage(row: ToReviewNote): string {
  return `/participants/${row.participantId}/notes/${row.noteDate}`;
}

/**
 * Answers the reads of `row`'s note page: its participant's header, and the note as `GET {base}` gives it, submitted
 * by its author when it was flagged, its current version flagged unless a later edit removed the flag.
 */
function serveNote(row: ToReviewAnswer) {
  const { participantId: id, givenName, familyName } = row;
  const participant: ParticipantHeader = { id, givenName, familyName, status: 'Active' };
  const flag = row.flagRemovedLater
    ? ({ isFlagged: false, flagReason: null } as const)
    : ({ isFlagged: true, flagReason: row.flagReason } as const);
  const goals = [{ text: 'Catches bus to day program', isTicked: true }];
  const edit = { by: 'Jo Smith', atUtc: row.flaggedAtUtc };
  const note: SubmittedNote = {
    status: 'Submitted',
    author: { id: '6f3a8c10-0000-4000-8000-000000000002', displayName: row.authorDisplayName },
    startedAtUtc: row.flaggedAtUtc,
    firstSubmittedAtUtc: row.flaggedAtUtc,
    isPastDayNote: false,
    ...(row.currentVersionNumber > 1 ? ({ isEdited: true, lastEdit: edit } as const) : ({ isEdited: false } as const)),
    current: {
      versionNumber: row.currentVersionNumber,
      narrative: 'Had lunch at the cafe, then a walk.',
      goals,
      commonItemGroups: [],
      ...flag,
    },
  };
  serve(`/api/participants/${id}`, json(participant));
  serve(`/api/participants/${id}/notes/${row.noteDate}`, json(note));
}

/** `html` with the IDs that React's `useId` makes, which differ from one mount to the next, taken out. */
function withoutIds(html: string): string {
  return html.replace(/_r_[0-9a-z]+_/g, '');
}

// The browser routers that tests make, each disposed of after its test, as it listens to the window's history.
const browserRouters: Router[] = [];

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    if ((init?.method ?? 'GET') !== 'GET') {
      throw new Error(`Flagged sent a ${init?.method} to ${path as string}.`);
    }

    const answer = answers.get(path as string);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${path as string}.`);
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // No unfinished drafts on Today, for a test that goes there.
  serve(draftsPath, json([]));
  // React and React Router report each throw that a boundary catches: the list's 403 and 404 are expected, and
  // anything else shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.some((arg) => arg instanceof ApiError && (arg.status === 403 || arg.status === 404))) {
      report(...args);
    }
  });
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
  scrollPage(0);
});

afterEach(() => {
  for (const router of browserRouters.splice(0)) {
    router.dispose();
  }

  window.history.replaceState(null, '', '/');
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('Flagged notes while the list loads', () => {
  // flagged.md component 2 and states L1 and L2: the static <h1> at once, then nothing for a second.
  it('is titled, its <h1> shown at once, then nothing until "Loading flagged notes…" at 1 s', async () => {
    serve(reviewsPath, noAnswer);
    renderApp('/flagged');

    expect(document.title).toBe('Grow2Notes – Flagged notes');
    expect(pageHeading()).toHaveAccessibleName('Flagged notes');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading flagged notes…');
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main().querySelector('svg, [aria-busy], progress')).toBeNull();
  });
});

describe('the To review list', () => {
  it("lists its rows in the API's order, never sorting them, in an <ol>", async () => {
    // In none of name, note date or flag time order.
    serve(reviewsPath, json([lee, sam, jane]));
    await open();

    expect(rowKeys()).toEqual([keyOf(lee), keyOf(sam), keyOf(jane)]);
    expect(list().tagName).toBe('OL');
    expect(list()).toHaveAttribute('role', 'list');
    expect(reviewRequests()).toEqual(oneMount);
  });

  // flagged.md item 4: the name and date name the row; the author and the flag time describe it.
  it('names a row by the full name and note date, described by the author and flag time, none of it bold', async () => {
    serve(reviewsPath, json(flags));
    await open();

    const link = rowLink(sam);
    expect(link).toHaveAccessibleName(`Sam Taylor, Tuesday 29${nbsp}September 2026`);
    expect(link).toHaveAttribute('href', `/participants/${sam.participantId}/notes/2026-09-29`);
    expect(link.querySelector('time')).toHaveAttribute('datetime', '2026-09-29');
    expect(link.querySelector('time')).toHaveStyle({ display: 'block' });
    expect(link).toHaveAccessibleDescription(`Priya Nair, flagged 4:42${nbsp}pm`);
    const description = descriptionOf(link);
    expect(description.querySelector('time')).toHaveAttribute('datetime', sam.flaggedAtUtc);
    // No status word: every part secondary (flagged.md *Cross-cutting*).
    expect(description.querySelector(`.${metaLine.status}`)).toBeNull();
    for (const element of [description, ...description.querySelectorAll('*')]) {
      expect(element).toHaveStyle({ color: 'var(--colour-text-secondary)' });
      expect(element).not.toHaveStyle({ fontWeight: '700' });
    }
  });

  // microcopy.md §3's reference day, the note date, in Melbourne, whatever the date in UTC.
  it.each([
    { when: 'a later day in Melbourne than the note date', row: jane, stamp: `Thu 1${nbsp}Oct 2026, 9:01${nbsp}am` },
    { when: 'the note date', row: sam, stamp: `4:42${nbsp}pm` },
    { when: 'the note date at 11:30 pm', row: { ...sam, flaggedAtUtc: lateOnTheDay }, stamp: `11:30${nbsp}pm` },
  ])('says when it was flagged on $when as "flagged $stamp"', async ({ row, stamp }) => {
    serve(reviewsPath, json([row]));
    await open();

    expect(rowLink(row)).toHaveAccessibleDescription(`${row.authorDisplayName}, flagged ${stamp}`);
    expect(descriptionOf(rowLink(row)).querySelector('time')).toHaveAttribute('datetime', row.flaggedAtUtc);
  });

  it('shows the reason after the meta line, whole, up to its first line break, outside the description', async () => {
    const long = `${'Seemed upset about moving house. '.repeat(7).slice(0, 199)}.`;
    expect(long).toHaveLength(200);
    const rows = [
      { ...sam, flagReason: long },
      { ...jane, flagReason: 'Fell over in the kitchen.\nCalled her sister.' },
      { ...lee, flagReason: 'Missed lunch.\r\nAte later.', flagRemovedLater: false },
    ];
    serve(reviewsPath, json(rows));
    await open();

    expect(rows.map(extraLines)).toEqual([
      [`Reason: ${long}`],
      ['Reason: Fell over in the kitchen.'],
      ['Reason: Missed lunch.'],
    ]);
    expect(list()).not.toHaveTextContent(/Called her sister|Ate later/);
    expect(rowLink(jane)).toHaveAccessibleDescription(`Alex P., flagged Thu 1${nbsp}Oct 2026, 9:01${nbsp}am`);
  });

  it('says "Flag removed in a later edit" on a row whose current version is not flagged, and on no other', async () => {
    serve(reviewsPath, json(flags));
    await open();

    expect(extraLines(lee)).toEqual(['Reason: Missed lunch.', 'Flag removed in a later edit']);
    expect(extraLines(sam)).toEqual(['Reason: Seemed upset about moving house.']);
    expect(extraLines(jane)).toEqual(['Reason: Did not want to get out of bed. Said her back hurt.']);
    expect(within(list()).getAllByText('Flag removed in a later edit')).toHaveLength(1);
  });

  // flagged.md item 4: the view names the state, so no row carries a tag, and a row is one link.
  it('has no tag and no button in any row, each row holding its one link alone', async () => {
    serve(reviewsPath, json(flags));
    await open();

    expect(list().querySelector(`.${tagStyles.tag}`)).toBeNull();
    expect(within(list()).queryByRole('button')).toBeNull();
    for (const row of within(list()).getAllByRole('listitem')) {
      expect(within(row).getAllByRole('link')).toHaveLength(1);
    }
  });

  // design.md §4.6 *States*, flagged.md L6.
  it('says "No flagged notes to review." with no list, when nothing is flagged', async () => {
    serve(reviewsPath, json([]));
    await open();

    expect(within(main()).getByText('No flagged notes to review.').tagName).toBe('P');
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main().querySelector('ol')).toBeNull();
    expect(within(main()).queryByRole('button')).toBeNull();
  });

  // flagged.md component 6: on mount, as the page becomes visible and as the window regains focus.
  it('asks for the list again on each visit, showing the rows it has meanwhile, and on window focus', async () => {
    serve(reviewsPath, json([]));
    serve(todayPath, json({ date: manager.today, participants: [] } satisfies TodayList));
    await open();
    await press(navLink('Today'));
    const refetch = held(json(flags));
    serve(reviewsPath, refetch.answer);
    fetchMock.mockClear();

    await press(navLink('Flagged'));
    expect(reviewRequests()).toEqual(oneMount);
    expect(within(main()).getByText('No flagged notes to review.')).toBeInTheDocument();
    refetch.release();
    await settle();
    expect(rowKeys()).toEqual(flags.map(keyOf));

    serve(reviewsPath, json(flags));
    for (const event of [becomeVisible, focusWindow]) {
      fetchMock.mockClear();
      event();
      await settle();
      expect(reviewRequests()).toEqual([reviewsPath]);
    }

    // Both together, as a person returning to the tab brings them, share one request.
    fetchMock.mockClear();
    focusWindow();
    becomeVisible();
    await settle();
    expect(reviewRequests()).toEqual([reviewsPath]);
    expect(rowKeys()).toEqual(flags.map(keyOf));
  });
});

describe('Flagged in the navigation', () => {
  // flagged.md component 1, whose ?view= changes nothing.
  it.each(flaggedAddresses)('marks Flagged as the current page at %s', async (path) => {
    serve(reviewsPath, json(flags));
    await open(path);

    expect(navLink('Flagged')).toHaveAttribute('aria-current', 'page');
    expect(currentItems()).toEqual(['Flagged page']);
    expect(rowKeys()).toEqual(flags.map(keyOf));
  });
});

describe('Page not found on Flagged', () => {
  // flagged.md L20 and *Access and navigation*.
  it.each(flaggedAddresses)('shows a worker Page not found at %s, asking for no list', async (path) => {
    await open(path, worker);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(main()).not.toHaveTextContent('Flagged notes');
    const nav = screen.getByRole('navigation', { name: 'Main' });
    expect(within(nav).queryByRole('link', { name: 'Flagged' })).toBeNull();
    expect(reviewRequests()).toEqual([]);
  });

  // As after a change of role: the API refuses a worker's request.
  it.each([403, 404])('shows a manager Page not found for a %i from the list, no nav item current', async (status) => {
    serve(reviewsPath, problem(status));
    await open();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(main()).not.toHaveTextContent('Flagged notes');
    expect(currentItems()).toEqual([]);
  });
});

describe('Flagged notes when the list did not load', () => {
  // flagged.md L13, after its one silent retry.
  it.each([
    { failure: 'no connection', answer: noConnection },
    { failure: 'something went wrong', answer: problem(500) },
  ])('says the list did not load, for $failure, under the <h1>, with Try again', async ({ answer, failure }) => {
    serve(reviewsPath, answer);
    renderApp('/flagged');
    await wait(1_000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Flagged notes');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(line().textContent).toBe(`Flagged notes did not load: ${failure}. Try again.`);
    const tryAgain = within(main()).getByRole('button', { name: 'Try again' });
    expect(within(main()).queryByRole('list')).toBeNull();

    serve(reviewsPath, json(flags));
    await press(tryAgain);
    await settle();
    expect(rowKeys()).toEqual(flags.map(keyOf));
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button')).toBeNull();
  });
});

describe('A note opened from Flagged', () => {
  // flagged.md component 7 and *Interactions and focus*: the list's place kept in memory, and its rows shown at once.
  it('has the back link "Flagged", which goes back one entry to the rows kept, focus on the row opened', async () => {
    serve(reviewsPath, json(flags));
    serveNote(lee);
    const router = await open();
    scrollPage(600);

    await press(rowLink(lee));
    await settle();
    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe(notePage(lee));
    expect(pageHeading()).toHaveAccessibleName(`Lee Wong Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(backLink()).toHaveAccessibleName('Flagged');
    expect(backLink()).toHaveAttribute('href', '/flagged');
    // The list asked for again as it shows, answered only after the rows kept in memory have shown.
    const refetch = held(json(flags));
    serve(reviewsPath, refetch.answer);
    fetchMock.mockClear();

    await press(backLink());

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/flagged');
    expect(rowKeys()).toEqual(flags.map(keyOf));
    expect(rowLink(lee)).toHaveFocus();
    expect(window.scrollY).toBe(600);
    expect(reviewRequests()).toEqual(oneMount);

    refetch.release();
    await settle();
    expect(rowKeys()).toEqual(flags.map(keyOf));
    expect(rowLink(lee)).toHaveFocus();

    // And the same by browser Back.
    serve(reviewsPath, json(flags));
    await press(rowLink(lee));
    await settle();
    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe('/flagged');
    expect(rowLink(lee)).toHaveFocus();
    expect(window.scrollY).toBe(600);
  });

  // app-shell.md component 2b: a note's address marks no nav item, whichever list opened it.
  it('marks no nav item as current', async () => {
    serve(reviewsPath, json(flags));
    serveNote(sam);
    await open();

    await press(rowLink(sam));
    await settle();

    expect(backLink()).toHaveAccessibleName('Flagged');
    expect(navLink('Flagged')).not.toHaveAttribute('aria-current');
    expect(currentItems()).toEqual([]);
  });

  // participant-notes.md item 28a: only the back link's label differs between the routes that open the read view.
  it('shows the same read view as from Today, but for the back link', async () => {
    const today: TodayList = {
      date: manager.today,
      participants: [
        {
          id: lee.participantId,
          givenName: lee.givenName,
          familyName: lee.familyName,
          note: {
            status: 'Submitted',
            authorDisplayName: lee.authorDisplayName,
            isMine: false,
            startedAtUtc: '2026-09-30T23:14:00Z',
            firstSubmittedAtUtc: lee.flaggedAtUtc,
            isFlagged: false,
          },
        },
      ],
    };
    serve(todayPath, json(today));
    serve(reviewsPath, json(flags));
    serveNote(lee);
    const router = renderApp('/');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Lee Wong' }));
    await settle();
    expect(backLink()).toHaveAccessibleName('Today');
    const fromToday = withoutIds(main().innerHTML);

    await press(navLink('Flagged'));
    await press(rowLink(lee));
    await settle();

    expect(router.state.location.pathname).toBe(notePage(lee));
    expect(backLink()).toHaveAccessibleName('Flagged');
    expect(withoutIds(main().innerHTML)).toBe(fromToday);
    expect(pageHeading()).toHaveFocus();
  });

  // flagged.md component 7: opened from outside the app, the shell's memory holds nothing of how it was reached.
  it.each([
    { how: 'after a reload', before: ['/flagged'] },
    { how: 'from a pasted address', before: [] },
  ])('has the back link "Today" $how, which adds an entry for Today', async ({ before }) => {
    serveNote(lee);
    serve(todayPath, json({ date: manager.today, participants: [] } satisfies TodayList));
    const router = renderApp(notePage(lee), manager, before);
    await settle();

    expect(pageHeading()).toHaveAccessibleName(`Lee Wong Thursday 1${nbsp}October 2026`);
    expect(backLink()).toHaveAccessibleName('Today');
    expect(backLink()).toHaveAttribute('href', '/');

    await press(backLink());

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/');
  });

  // On a browser router, as `main.tsx` uses, so that the history is the window's own: where the note was opened from
  // is kept in memory alone (app-shell.md *Where a screen was opened from*).
  it("pushes the note with no router state, leaving only React Router's own in history.state", async () => {
    serve(reviewsPath, json(flags));
    serveNote(lee);
    window.history.replaceState({ usr: null, key: crypto.randomUUID(), idx: 0 }, '', '/flagged');
    const router = createBrowserRouter(routes);
    browserRouters.push(router);
    show(router, manager);
    await settle();

    await press(rowLink(lee));
    await settle();

    expect(window.location.pathname).toBe(notePage(lee));
    expect(backLink()).toHaveAccessibleName('Flagged');
    expect(router.state.location.state).toBeNull();
    expect(window.history.state).toEqual({ usr: null, key: expect.any(String) as string, idx: 1 });
  });
});
