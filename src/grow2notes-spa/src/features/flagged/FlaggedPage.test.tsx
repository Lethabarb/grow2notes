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
import type { FlagStatus, ParticipantHeader, SubmittedNote } from '../notes/noteQueries.ts';
import type { TodayList } from '../today/todayQuery.ts';
import type { ReviewedNote } from './reviewedQuery.ts';
import type { ToReviewNote } from './toReviewQuery.ts';

// A memory router and a browser router alike.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';
const reviewsPath = '/api/reviews?status=toReview';
const reviewedPath = '/api/reviews?status=reviewed';

// Flagged's two addresses: To review's, and Reviewed's.
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

/**
 * The `n`th review of the Reviewed list, newest first: Casey Bell's note of Monday 28 September, written by Alex
 * Park, reviewed by Jo Smith one minute before the review before it, the first at 9:30 am on Tuesday 29 September in
 * Melbourne, whose time is written as the server writes it, to the millisecond.
 */
function reviewRow(n: number, over: Partial<ReviewedNote> = {}): ReviewedNote {
  const id = String(n).padStart(12, '0');
  return {
    reviewId: `0f3e5a7c-1b2d-4c6e-8a90-${id}`,
    participantId: `7d2c9e41-5b3a-4f68-9c10-${id}`,
    givenName: 'Casey',
    familyName: 'Bell',
    noteDate: '2026-09-28',
    authorDisplayName: 'Alex Park',
    flagReason: 'Seemed upset about moving house.',
    reviewedBy: 'Jo Smith',
    reviewedAtUtc: new Date(Date.UTC(2026, 8, 28, 23, 30) - n * 60_000).toISOString(),
    comment: null,
    ...over,
  };
}

/** `count` reviews, newest first. */
const reviewRows = (count: number) => Array.from({ length: count }, (_, n) => reviewRow(n));

/** A review's return key. */
const reviewKey = (row: ReviewedNote) => row.reviewId;

/** The request for the page after `row`, as the server's own values are sent back. */
const cursorPath = (row: ReviewedNote) =>
  `${reviewedPath}&before=${encodeURIComponent(row.reviewedAtUtc)}&beforeId=${row.reviewId}`;

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

/** Serves the Reviewed list as its pages of `size`, each at the request that the page before it makes. */
function serveReviewed(rows: readonly ReviewedNote[], size = 30) {
  for (let start = 0; start === 0 || start < rows.length; start += size) {
    const items = rows.slice(start, start + size);
    const path = start === 0 ? reviewedPath : cursorPath(rows[start - 1]);
    serve(path, json({ items, hasMore: start + size < rows.length }));
  }
}

/** Every request for a reviews list, as its path. */
function reviewRequests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string).filter((path) => path.startsWith('/api/reviews'));
}

/** Every request for the Reviewed list, as its path. */
function reviewedRequests(): string[] {
  return reviewRequests().filter((path) => path.startsWith(reviewedPath));
}

/**
 * The list's requests as the page mounts: two, as StrictMode mounts the page, unmounts it and mounts it again, and the
 * unmount cancels the first request, whose query function takes the abort signal, so that the second mount sends it
 * again. A production build sends it once.
 */
const oneMount = [reviewsPath, reviewsPath];

// The query client of the last render, for a test that makes a list fetch while the page that shows it is away.
let lastClient: ReturnType<typeof createQueryClient>;

/**
 * Renders the app on `router` for `me`, under StrictMode, which runs each effect twice, as `main.tsx` does, and a
 * query client of its own, in which `['me']` holds `me` already, as after the start-up.
 */
function show(router: Router, me: Me) {
  serve(mePath, json(me));
  const client = createQueryClient();
  lastClient = client;
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

/** The shown view's list of rows, not the list of the view links. */
function list(): HTMLElement {
  return main().querySelector<HTMLElement>('ol')!;
}

/** The nav of the two view links, named by the page's `<h1>`. */
function viewLinks(): HTMLElement {
  return within(main()).getByRole('navigation', { name: 'Flagged notes' });
}

/** The view link named `name`. */
function viewLink(name: string): HTMLElement {
  return within(viewLinks()).getByRole('link', { name });
}

/** The view links' texts, in order. */
function viewLabels(): (string | null)[] {
  return within(viewLinks())
    .getAllByRole('link')
    .map((link) => link.textContent);
}

/** The view links that have `aria-current`, as their text and the attribute's value. */
function currentViews(): string[] {
  const current = viewLinks().querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** The Show older button. */
function showOlder(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Show older' });
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

/** The link of the Reviewed row of `row`. */
function reviewedLink(row: ReviewedNote): HTMLElement {
  return rowLinks().find((link) => link.dataset.returnKey === reviewKey(row))!;
}

/** The plain lines of `link`'s row after its meta line, as their text. */
function linesAfter(link: HTMLElement): (string | null)[] {
  const description = descriptionOf(link);
  return Array.from(link.closest('li')!.querySelectorAll('p'))
    .filter((paragraph) => paragraph !== description)
    .map((paragraph) => paragraph.textContent);
}

/** The plain lines of `row`'s row after its meta line, as their text. */
function extraLines(row: ToReviewNote): (string | null)[] {
  return linesAfter(rowLink(row));
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
function notePage(row: Pick<ToReviewNote, 'participantId' | 'noteDate'>): string {
  return `/participants/${row.participantId}/notes/${row.noteDate}`;
}

/**
 * Answers the reads of `row`'s note page: its participant's header, and the note as `GET {base}` gives it, submitted
 * by its author when it was flagged, its current version flagged unless a later edit removed the flag, and, when
 * `flagStatus` is given, with that review status, which a manager's read view of a To review note shows the Review
 * panel for.
 */
function serveNote(row: ToReviewAnswer, flagStatus?: FlagStatus) {
  const { participantId: id, givenName, familyName } = row;
  const participant: ParticipantHeader = { id, givenName, familyName, status: 'Active', firstNoteDate: null };
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
    ...(flagStatus === undefined ? {} : { flagStatus }),
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

/** A review as the flag it cleared, which is how its note reads to the note page. */
function asFlag(row: ReviewedNote): ToReviewAnswer {
  return { ...row, flaggedAtUtc: row.reviewedAtUtc, currentVersionNumber: 1, flagRemovedLater: false };
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
    // A write is answered under "POST /path", so that only a test that expects one lets it through.
    const method = init?.method ?? 'GET';
    const request = method === 'GET' ? (path as string) : `${method} ${path as string}`;
    const answer = answers.get(request);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${request}.`);
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
    expect(main().querySelector('ol')).toBeNull();
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
  // flagged.md component 1, which ?view= leaves marked.
  it.each([
    { path: '/flagged', keys: flags.map(keyOf), current: 'To review (3) page' },
    { path: '/flagged?view=reviewed', keys: reviewRows(2).map(reviewKey), current: 'Reviewed page' },
  ])('marks Flagged as the current page at $path, and the view shown in its own links', async (shown) => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(2));
    await open(shown.path);

    expect(navLink('Flagged')).toHaveAttribute('aria-current', 'page');
    expect(currentItems()).toEqual(['Flagged page']);
    expect(currentViews()).toEqual([shown.current]);
    expect(viewLinks().querySelectorAll('[aria-current="page"]')).toHaveLength(1);
    expect(rowKeys()).toEqual(shown.keys);
  });
});

describe('the views of Flagged notes', () => {
  // flagged.md component 3 and tabs-segmented.md *Behaviour*: the address is the view's only memory.
  it.each([
    { path: '/flagged', current: 'To review (3) page' },
    { path: '/flagged?view=reviewed', current: 'Reviewed page' },
  ])('shows the view that $path names, marking its link as current', async ({ path, current }) => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(1));
    const router = await open(path);

    expect(currentViews()).toEqual([current]);
    expect(router.state.historyAction).toBe('POP');
    expect(document.title).toBe('Grow2Notes – Flagged notes');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('shows To review for any other view, and puts the address right with a replace, no new entry', async () => {
    serve(reviewsPath, json(flags));
    const router = await open('/flagged?view=xyz');

    expect(router.state.historyAction).toBe('REPLACE');
    expect(router.state.location.search).toBe('');
    expect(currentViews()).toEqual(['To review (3) page']);
    expect(rowKeys()).toEqual(flags.map(keyOf));
    expect(reviewedRequests()).toEqual([]);
  });

  it('pushes one entry for a switch, with focus on the link and the page where it was, and Back returns', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(2));
    const router = await open();
    scrollPage(300);

    await press(viewLink('Reviewed'));
    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.search).toBe('?view=reviewed');
    expect(viewLink('Reviewed')).toHaveFocus();
    expect(window.scrollY).toBe(300);
    expect(rowKeys()).toEqual(reviewRows(2).map(reviewKey));

    await act(() => router.navigate(-1));
    await settle();
    expect(router.state.location.search).toBe('');
    expect(rowKeys()).toEqual(flags.map(keyOf));
    expect(currentViews()).toEqual(['To review (3) page']);
  });

  // flagged.md component 3: the count is To review's own list, and Reviewed has none.
  it('names To review with no number before its list has loaded, and Reviewed with none ever', async () => {
    serve(reviewsPath, noAnswer);
    renderApp('/flagged');

    expect(viewLabels()).toEqual(['To review', 'Reviewed']);
    await wait(1_500);
    expect(viewLabels()).toEqual(['To review', 'Reviewed']);
  });

  it.each([
    { rows: flags, label: 'To review (3)' },
    { rows: [], label: 'To review (0)' },
  ])('names To review "$label" once its list has loaded, on either view', async ({ rows, label }) => {
    serve(reviewsPath, json(rows));
    serveReviewed(reviewRows(2));
    await open();
    expect(viewLabels()).toEqual([label, 'Reviewed']);

    await press(viewLink('Reviewed'));
    expect(viewLabels()).toEqual([label, 'Reviewed']);
  });

  it('names To review with no number when its first load failed', async () => {
    serve(reviewsPath, noConnection);
    renderApp('/flagged');
    await wait(1_000);
    await settle();

    expect(viewLabels()).toEqual(['To review', 'Reviewed']);
  });

  it('keeps the number through a refresh in the background that failed', async () => {
    serve(reviewsPath, json(flags));
    await open();
    serve(reviewsPath, noConnection);

    focusWindow();
    await wait(1_000);
    await settle();

    expect(viewLabels()).toEqual(['To review (3)', 'Reviewed']);
    expect(rowKeys()).toEqual(flags.map(keyOf));
  });

  it('asks for Reviewed only once its view is shown, and for To review on both', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(2));
    await open();
    expect(reviewedRequests()).toEqual([]);

    await press(viewLink('Reviewed'));
    expect(reviewedRequests()).toEqual([reviewedPath]);
  });

  it('asks for both lists when it opens on Reviewed', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(2));
    await open('/flagged?view=reviewed');

    expect(reviewRequests()).toContain(reviewsPath);
    expect(reviewRequests()).toContain(reviewedPath);
    expect(viewLabels()).toEqual(['To review (3)', 'Reviewed']);
  });
});

describe('the Reviewed list', () => {
  it("lists the reviews in the API's order, in an <ol>, each row one link named like a To review row's", async () => {
    // In none of time or ID order.
    const rows = [reviewRow(1), reviewRow(0), reviewRow(2)];
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    await open('/flagged?view=reviewed');

    expect(rowKeys()).toEqual(rows.map(reviewKey));
    expect(list().tagName).toBe('OL');
    expect(reviewedLink(rows[0])).toHaveAccessibleName(`Casey Bell, Monday 28${nbsp}September 2026`);
    expect(reviewedLink(rows[0])).toHaveAttribute(
      'href',
      `/participants/${rows[0].participantId}/notes/2026-09-28`,
    );
    expect(list().querySelector(`.${tagStyles.tag}`)).toBeNull();
    for (const row of within(list()).getAllByRole('listitem')) {
      expect(within(row).getAllByRole('link')).toHaveLength(1);
    }
  });

  // The words and the formatter of the Review panel's record (design.md A74).
  it('says who reviewed it and when, as the Review panel does, in a <time>', async () => {
    const row = reviewRow(0);
    serve(reviewsPath, json(flags));
    serveReviewed([row]);
    await open('/flagged?view=reviewed');

    expect(reviewedLink(row)).toHaveAccessibleDescription(
      `Alex Park, reviewed by Jo Smith on Tue 29${nbsp}Sep 2026, 9:30${nbsp}am`,
    );
    expect(descriptionOf(reviewedLink(row)).querySelector('time')).toHaveAttribute('datetime', row.reviewedAtUtc);
    expect(descriptionOf(reviewedLink(row)).querySelector(`.${metaLine.status}`)).toBeNull();
  });

  it('shows the reason up to its first line break and the comment as written, none when there is none', async () => {
    const rows = [
      reviewRow(0, {
        flagReason: 'Fell over in the kitchen.\nCalled her sister.',
        comment: 'Rang her mum.\nShe is fine.',
      }),
      reviewRow(1),
    ];
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    await open('/flagged?view=reviewed');

    expect(linesAfter(reviewedLink(rows[0]))).toEqual([
      'Reason: Fell over in the kitchen.',
      'Comment: Rang her mum.\nShe is fine.',
    ]);
    expect(within(reviewedLink(rows[0]).closest('li')!).getByText(/^Comment:/)).toHaveStyle({
      whiteSpace: 'pre-wrap',
    });
    expect(linesAfter(reviewedLink(rows[1]))).toEqual(['Reason: Seemed upset about moving house.']);
    expect(list()).not.toHaveTextContent('Called her sister');
  });

  it('gives a note reviewed twice a row for each review, with no key warning', async () => {
    const rows = [reviewRow(0), reviewRow(1, { participantId: reviewRow(0).participantId })];
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    await open('/flagged?view=reviewed');

    expect(rowKeys()).toEqual(rows.map(reviewKey));
    expect(rowLinks().map((link) => link.getAttribute('href'))).toEqual(
      Array(2).fill(`/participants/${rows[0].participantId}/notes/2026-09-28`),
    );
    expect(console.error).not.toHaveBeenCalled();
  });

  it('says "No reviewed notes yet." with no list, when nothing is reviewed', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed([]);
    await open('/flagged?view=reviewed');

    expect(within(main()).getByText('No reviewed notes yet.').tagName).toBe('P');
    expect(main().querySelector('ol')).toBeNull();
    expect(within(main()).queryByRole('button')).toBeNull();
  });

  // chronological-list.md: nothing loads on scroll, and nothing counts the pages.
  it('adds a page at the last review\'s time and ID when Show older is pressed, to 61 rows once each', async () => {
    const rows = reviewRows(61);
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    await open('/flagged?view=reviewed');
    expect(rowKeys()).toEqual(rows.slice(0, 30).map(reviewKey));
    scrollPage(5_000);
    await settle();
    expect(new Set(reviewedRequests())).toEqual(new Set([reviewedPath]));

    await press(showOlder());
    expect(rowKeys()).toEqual(rows.slice(0, 60).map(reviewKey));
    expect(reviewedRequests().at(-1)).toBe(cursorPath(rows[29]));
    expect(reviewedRequests().at(-1)).toContain(`&before=${encodeURIComponent(rows[29].reviewedAtUtc)}&beforeId=`);

    await press(showOlder());
    expect(rowKeys()).toEqual(rows.map(reviewKey));
    expect(new Set(rowKeys()).size).toBe(61);
    expect(reviewedRequests().at(-1)).toBe(cursorPath(rows[59]));
    expect(within(main()).queryByRole('button', { name: 'Show older' })).toBeNull();
    expect(main()).not.toHaveTextContent(/Showing|page \d/i);
  });

  it('does nothing for a press of Show older while the page before it is still loading', async () => {
    const rows = reviewRows(61);
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    const next = held(json({ items: rows.slice(30, 60), hasMore: true }));
    await open('/flagged?view=reviewed');
    serve(cursorPath(rows[29]), next.answer);
    fetchMock.mockClear();

    await press(showOlder());
    await press(showOlder());
    expect(reviewedRequests()).toEqual([cursorPath(rows[29])]);
    expect(showOlder()).toHaveAttribute('aria-disabled', 'true');

    next.release();
    await settle();
    expect(rowKeys()).toHaveLength(60);
    expect(reviewedRequests()).toEqual([cursorPath(rows[29])]);
  });

  it('says "Loading reviewed notes…" at 1 s and not before', async () => {
    serve(reviewsPath, json(flags));
    serve(reviewedPath, noAnswer);
    renderApp('/flagged?view=reviewed');

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading reviewed notes…');
    expect(main().querySelector('ol')).toBeNull();
  });

  it('says the list did not load, then "still", with Try again, which when it works focuses the <h1>', async () => {
    serve(reviewsPath, json(flags));
    serve(reviewedPath, noConnection);
    renderApp('/flagged?view=reviewed');
    await wait(1_000);
    await settle();

    expect(line().textContent).toBe('Reviewed notes did not load: no connection. Try again.');
    expect(main().querySelector('ol')).toBeNull();

    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await wait(1_000);
    await settle();
    expect(line().textContent).toBe('Reviewed notes still did not load: no connection. Try again.');

    serveReviewed(reviewRows(2));
    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await settle();
    expect(rowKeys()).toEqual(reviewRows(2).map(reviewKey));
    expect(pageHeading()).toHaveFocus();
  });

  it('keeps the rows and says why when Show older failed, "still" when it fails again', async () => {
    const rows = reviewRows(61);
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    serve(cursorPath(rows[29]), noConnection);
    await open('/flagged?view=reviewed');

    await press(showOlder());
    await wait(1_000);
    await settle();
    expect(within(main()).getByText('Older notes did not load: no connection. Try again.')).toBeInTheDocument();
    expect(rowKeys()).toEqual(rows.slice(0, 30).map(reviewKey));

    await press(showOlder());
    await wait(1_000);
    await settle();
    expect(within(main()).getByText('Older notes still did not load: no connection. Try again.')).toBeInTheDocument();
    expect(rowKeys()).toEqual(rows.slice(0, 30).map(reviewKey));
  });

  it('shows Page not found for a 403 or a 404 from the list, in place of the whole screen', async () => {
    serve(reviewsPath, json(flags));
    serve(reviewedPath, problem(403));
    await open('/flagged?view=reviewed');

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});

describe('focus and place on Flagged notes', () => {
  /**
   * The two lists with `all` three rows, each served with only the rows at `keep`, as a refresh gives them once
   * others have gone from it.
   */
  const lists = [
    {
      name: 'To review',
      path: '/flagged',
      serveKeeping: (keep: number[]) => {
        serve(reviewsPath, json(keep.map((index) => flags[index])));
      },
    },
    {
      name: 'Reviewed',
      path: '/flagged?view=reviewed',
      serveKeeping: (keep: number[]) => {
        serve(reviewsPath, json(flags));
        serveReviewed(keep.map((index) => reviewRows(3)[index]));
      },
    },
  ];

  it('asks for To review again on window focus, and for Reviewed too while it is shown', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(2));
    await open();

    fetchMock.mockClear();
    focusWindow();
    await settle();
    expect(reviewRequests()).toEqual([reviewsPath]);

    await press(viewLink('Reviewed'));
    fetchMock.mockClear();
    focusWindow();
    await settle();
    expect([...reviewRequests()].sort()).toEqual([reviewedPath, reviewsPath].sort());
  });

  // flagged.md *Interactions and focus*, S03.03.05: never to <body>.
  it.each(
    lists.flatMap((shown) => [
      { ...shown, which: 'a middle row', focused: 1, keep: [0, 2], now: 1 },
      { ...shown, which: 'the last row', focused: 2, keep: [0, 1], now: 1 },
      { ...shown, which: 'the first row', focused: 0, keep: [1, 2], now: 0 },
    ]),
  )('$name: sends focus to the row now in its place when a refresh takes away $which', async (scenario) => {
    scenario.serveKeeping([0, 1, 2]);
    await open(scenario.path);
    const keys = rowKeys();
    rowLinks()[scenario.focused].focus();

    scenario.serveKeeping(scenario.keep);
    focusWindow();
    await settle();

    expect(rowKeys()).toEqual(scenario.keep.map((index) => keys[index]));
    expect(rowLinks()[scenario.now]).toHaveFocus();
  });

  it.each(lists)('$name: sends focus to the <h1> when a refresh empties the list', async (scenario) => {
    scenario.serveKeeping([0, 1, 2]);
    await open(scenario.path);
    rowLinks()[1].focus();

    scenario.serveKeeping([]);
    focusWindow();
    await settle();

    expect(main().querySelector('ol')).toBeNull();
    expect(pageHeading()).toHaveFocus();
    expect(document.body).not.toHaveFocus();
  });

  it.each(lists)('$name: leaves focus where it is when the row that has it stays', async (scenario) => {
    scenario.serveKeeping([0, 1, 2]);
    await open(scenario.path);
    rowLinks()[2].focus();

    scenario.serveKeeping([1, 2]);
    focusWindow();
    await settle();

    expect(rowLinks()[1]).toHaveFocus();
    expect(rowLinks()).toHaveLength(2);
  });

  it('leaves focus alone when a refresh takes away a row that did not have it', async () => {
    serve(reviewsPath, json(flags));
    await open();
    viewLink('Reviewed').focus();

    serve(reviewsPath, json([sam, lee]));
    focusWindow();
    await settle();

    expect(viewLink('Reviewed')).toHaveFocus();
  });

  it.each([
    {
      how: 'browser Back',
      back: async (router: Router) => {
        await act(() => router.navigate(-1));
      },
    },
    {
      how: 'the "Flagged" back link',
      back: async () => {
        await press(backLink());
      },
    },
  ])('opened from the 10th Reviewed row after Show older, shows the 60 rows with focus on it by $how', async (go) => {
    const rows = reviewRows(60);
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    serveNote(asFlag(rows[9]));
    const router = await open('/flagged?view=reviewed');
    await press(showOlder());
    expect(rowKeys()).toHaveLength(60);

    await press(reviewedLink(rows[9]));
    await settle();
    expect(router.state.location.pathname).toBe(notePage(rows[9]));
    expect(backLink()).toHaveAccessibleName('Flagged');

    await go.back(router);
    await settle();

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/flagged');
    expect(router.state.location.search).toBe('?view=reviewed');
    expect(rowKeys()).toEqual(rows.map(reviewKey));
    expect(reviewedLink(rows[9])).toHaveFocus();
  });

  // The review itself is made on the note page, which the test stands in for with the list that then follows.
  it('goes back from a note reviewed to the list without it, focus on the new first row, refresh after', async () => {
    serve(reviewsPath, json(flags));
    serveNote(sam);
    await open();
    await press(rowLink(sam));
    await settle();
    const refresh = held(json([jane, lee]));
    serve(reviewsPath, refresh.answer);

    await press(backLink());
    expect(rowLink(sam)).toHaveFocus();

    refresh.release();
    await settle();
    expect(rowKeys()).toEqual([keyOf(jane), keyOf(lee)]);
    expect(rowLinks()[0]).toHaveFocus();
  });

  it('goes back from a note reviewed to the list without it, focus on the new first row, refresh before', async () => {
    serve(reviewsPath, json(flags));
    serveNote(sam);
    await open();
    await press(rowLink(sam));
    await settle();
    serve(reviewsPath, json([jane, lee]));
    await act(() => lastClient.refetchQueries({ queryKey: ['reviews', 'toReview'] }));

    await press(backLink());
    await settle();

    expect(rowKeys()).toEqual([keyOf(jane), keyOf(lee)]);
    expect(rowLinks()[0]).toHaveFocus();
  });

  it('starts Reviewed at the newest 30 after a push to Flagged from the menu, and after a reload', async () => {
    const rows = reviewRows(61);
    serve(reviewsPath, json(flags));
    serveReviewed(rows);
    serve(todayPath, json({ date: manager.today, participants: [] } satisfies TodayList));
    await open('/flagged?view=reviewed');
    await press(showOlder());
    expect(rowKeys()).toHaveLength(60);

    await press(navLink('Today'));
    fetchMock.mockClear();
    await press(navLink('Flagged'));
    await press(viewLink('Reviewed'));

    expect(rowKeys()).toEqual(rows.slice(0, 30).map(reviewKey));
    expect(new Set(reviewedRequests())).toEqual(new Set([reviewedPath]));
  });

  it('starts Reviewed at the newest 30 in a new query client, as after a reload', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(61));
    await open('/flagged?view=reviewed');

    expect(rowKeys()).toHaveLength(30);
    expect(new Set(reviewedRequests())).toEqual(new Set([reviewedPath]));
  });

  it('keeps the pages through a switch of view', async () => {
    serve(reviewsPath, json(flags));
    serveReviewed(reviewRows(61));
    await open('/flagged?view=reviewed');
    await press(showOlder());

    await press(viewLink('To review (3)'));
    await press(viewLink('Reviewed'));

    expect(rowKeys()).toHaveLength(60);
  });

  // S03.01.02: the menu's number and the list's label both follow a review.
  it('shows the new number in the menu link and the To review label after Mark reviewed', async () => {
    const counted = (toReviewCount: number): Me => ({ ...manager, toReviewCount });
    const reviewedAtUtc = '2026-10-01T02:00:00Z';
    serve(reviewsPath, json(flags));
    serveNote(sam, 'ToReview');
    // The token that a change asks for first, unless an earlier test left one held.
    serve('/api/auth/antiforgery', () =>
      Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
    );
    renderApp('/flagged', counted(3));
    await settle();
    expect(navLink('Flagged, 3 to review')).toBeInTheDocument();
    expect(viewLabels()).toEqual(['To review (3)', 'Reviewed']);

    await press(rowLink(sam));
    await settle();
    serve(`POST /api/participants/${sam.participantId}/notes/${sam.noteDate}/reviews`, json({ reviewedAtUtc }));
    serve(reviewsPath, json([jane, lee]));
    serve(mePath, json(counted(2)));
    await press(within(main()).getByRole('button', { name: 'Mark reviewed' }));
    await settle();

    await press(backLink());
    await settle();
    expect(navLink('Flagged, 2 to review')).toBeInTheDocument();
    expect(viewLabels()).toEqual(['To review (2)', 'Reviewed']);
    expect(rowKeys()).toEqual([keyOf(jane), keyOf(lee)]);
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
    expect(main().querySelector('ol')).toBeNull();

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
