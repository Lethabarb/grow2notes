import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createBrowserRouter, createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import tagStyles from '../../components/Tag.module.css';
import { routes } from '../../routes/routes.tsx';
import type { Draft, ParticipantHeader } from '../notes/noteQueries.ts';
import type { AdminParticipant } from '../participants/participantsQuery.ts';
import noteStatus from './NoteStatus.module.css';
import { todayQuery, type TodayList, type TodayNote, type TodayParticipant } from './todayQuery.ts';

// A memory router and a browser router alike.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const antiforgeryPath = '/api/auth/antiforgery';
const adminListPath = '/api/admin/participants?includeArchived=true';

// today.md's made-up people (A35): Sam Lee, a manager, and Alex Pham, a worker, in the JSON that MeEndpointTests
// asserts. Today in Melbourne is Thursday 1 October 2026.
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

/** A made-up participant on the Today list, with no note unless given one. */
function madeUp(id: string, givenName: string, familyName: string, note: TodayNote | null = null): TodayParticipant {
  return { id, givenName, familyName, note };
}

// Today's notes as the rows give them, each made up (A35): Alex P., another worker, started a draft at 9:14 am in
// Melbourne and submitted a note at 4:12 pm; the signed-in user last saved their own draft at 9:42 am.
const alexsDraft: TodayNote = {
  status: 'Draft',
  authorDisplayName: 'Alex P.',
  isMine: false,
  startedAtUtc: '2026-09-30T23:14:00Z',
  isFlagged: false,
};
const myDraft: TodayNote = {
  status: 'Draft',
  authorDisplayName: 'Alex Pham',
  isMine: true,
  startedAtUtc: '2026-09-30T23:05:00Z',
  savedAtUtc: '2026-09-30T23:42:00Z',
  isFlagged: false,
};
const alexsSubmitted: TodayNote = {
  status: 'Submitted',
  authorDisplayName: 'Alex P.',
  isMine: false,
  startedAtUtc: '2026-09-30T23:14:00Z',
  firstSubmittedAtUtc: '2026-10-01T06:12:00Z',
  isFlagged: false,
};
const mySubmitted: TodayNote = { ...alexsSubmitted, authorDisplayName: 'Alex Pham', isMine: true };

const zara = madeUp('1f7c3a9e-5d21-4b48-8e6f-0a2c4d7b9e13', 'Zara', 'Ali');
const jane = madeUp('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Jane', 'Citizen');
const ravi = madeUp('4a8e2c6f-1b39-4d57-9f0a-7c3e5b1d8a26', 'Ravi', 'de Silva');
const priya = madeUp('6b2e8d4a-0f37-4c95-a1d6-3e9f7b5c2a80', 'Priya', 'Kumar');
const sean = madeUp('7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20', 'Seán', 'O’Brien');
const ana = madeUp('8d1f5b3a-6c24-4e98-a7b0-2e9c4f6a1d53', 'Ana', 'Ward');

// In the API's order, by family name, then given name (A5), which sorting by the full name would not give.
const people = [zara, jane, ravi, priya, sean, ana];
const names = ['Zara Ali', 'Jane Citizen', 'Ravi de Silva', 'Priya Kumar', 'Seán O’Brien', 'Ana Ward'];

// With them, for Find a participant: an accented name, and two that hold "Alex", one a given name and one the start
// of a family name.
const mia = madeUp('618514b4-feaa-4052-9f1b-58a69cec966f', 'Mia', 'Alexander');
const zoe = madeUp('1ed79dc1-9d94-4e70-8e19-90b09a353926', 'Zoë', 'Hill');
const alex = madeUp('461db3e7-eacd-473f-8254-3d1fef9ece1b', 'Alex', 'Nguyen');
const findable = [mia, zara, jane, ravi, zoe, priya, alex, sean, ana];
const findableNames = [
  'Mia Alexander',
  'Zara Ali',
  'Jane Citizen',
  'Ravi de Silva',
  'Zoë Hill',
  'Priya Kumar',
  'Alex Nguyen',
  'Seán O’Brien',
  'Ana Ward',
];

const nbsp = ' ';

/** The Today list of `participants` for `date`. */
function listOf(participants: readonly TodayParticipant[], date = '2026-10-01'): TodayList {
  return { date, participants: [...participants] };
}

/** Jane as the admin list gives her, active or archived, with the version that the row then has. */
function janeInTheAdminList(status: AdminParticipant['status'], rowVersion: string): AdminParticipant {
  const { id, givenName, familyName } = jane;
  return { id, givenName, familyName, dateOfBirth: '1987-03-07', status, rowVersion };
}

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** A bodiless answer with `status`, such as an archive's `204`. */
function bodiless(status: number): Answer {
  return () => Promise.resolve(new Response(null, { status }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with what it includes. */
function problem(status: number, includes: Record<string, unknown> = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...includes });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/**
 * Answers `participant`'s header and the form of their note for 1 October 2026 (design.md §6.3), which their row
 * opens, the form with `draft`.
 */
function serveNoteOf(participant: TodayParticipant, draft: Answer) {
  const { id, givenName, familyName } = participant;
  serve(`/api/participants/${id}`, json({ id, givenName, familyName, status: 'Active' } satisfies ParticipantHeader));
  serve(`/api/participants/${id}/notes/2026-10-01/draft`, draft);
}

/** A new note's template, as `GET {base}/draft` gives it, with no goals. */
const newNote: Draft = {
  baseVersion: 0,
  isSaved: false,
  listsVersion: 'lists-1',
  guidePrompts: '',
  goals: [],
  narrative: '',
  isFlagged: false,
  flagReason: null,
};

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

/** How the stubbed server answers each request, by its method and path: a new answer for each. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer, method: 'GET' | 'POST' | 'PUT' = 'GET') {
  answers.set(`${method} ${path}`, answer);
}

/** How many `GET`s have been sent for `path`. */
function sent(path: string): number {
  return fetchMock.mock.calls.filter(([sentPath, init]) => sentPath === path && init?.method === 'GET').length;
}

/** Renders the app on `router` under `client` and StrictMode, which runs each effect twice, as `main.tsx` does. */
function show(router: Router, client: QueryClient) {
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
}

/** A memory router of the app's routes at `path`. */
function memoryRouterAt(path: string): Router {
  // With a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a row or a position from one test into the next.
  return createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own. `['me']` holds `me` already,
 * as after the start-up, so that the screen shows with the first render, from which the fake clock then times its
 * region.
 */
function renderApp(path: string, me: Me = worker): { router: Router; client: QueryClient } {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  const router = memoryRouterAt(path);
  show(router, client);
  return { router, client };
}

/** Opens Today for `me` with `list` from the server, and lets it load. */
async function openToday(me: Me = worker, list: TodayList = listOf(people)) {
  serve(todayPath, json(list));
  const opened = renderApp('/', me);
  await settle();
  return opened;
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the queries' new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The text that the `<h1>` shows, without what only screen readers get. */
function shownHeading(): string {
  const shown = pageHeading().cloneNode(true) as HTMLElement;
  shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
    hidden.remove();
  });
  return shown.textContent;
}

/** The region's line: the status region in `<main>` that shows, beside the page's visually hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The list of rows, or `null` while there is none. */
function rowList(): HTMLElement | null {
  return within(main()).queryByRole('list');
}

/** The line that describes `link`, or `null` for a row with none. */
function statusLine(link: HTMLElement): HTMLElement | null {
  const describedBy = link.getAttribute('aria-describedby');
  return describedBy === null ? null : document.getElementById(describedBy);
}

/** The rows' links, in order, each row holding exactly one link and nothing else to read but the line it describes. */
function rowLinks(): HTMLElement[] {
  const list = rowList();
  if (list === null) {
    return [];
  }

  return within(list)
    .getAllByRole('listitem')
    .map((row) => {
      const links = within(row).getAllByRole('link');
      expect(links).toHaveLength(1);
      expect(row.textContent).toBe(`${links[0].textContent}${statusLine(links[0])?.textContent ?? ''}`);
      return links[0];
    });
}

/**
 * The words of the status line of the row named `name`, as they show: without what only screen readers get, and with
 * the space after each dot once, as the browser shows it, where the part before it ends in the space of its own dot.
 */
function shownStatus(name: string): string | undefined {
  const line = statusLine(rowLink(name))?.cloneNode(true) as HTMLElement | undefined;
  line?.querySelectorAll('.visually-hidden').forEach((hidden) => {
    hidden.remove();
  });
  return line?.textContent.replaceAll(/ {2,}/g, ' ');
}

/** The names of the rows shown, in order. */
function listedNames(): string[] {
  return rowLinks().map((link) => link.textContent);
}

/** The row link named `name`. */
function rowLink(name: string): HTMLElement {
  return within(rowList()!).getByRole('link', { name });
}

/** Add participant, or `null` where there is none. */
function addParticipant(): HTMLElement | null {
  return within(main()).queryByRole('link', { name: 'Add participant' });
}

/** The Find a participant landmark, or `null` while Today has none. */
function search(): HTMLElement | null {
  return main().querySelector('search');
}

/** The Find a participant field. */
function field(): HTMLElement {
  return within(search()!).getByRole('textbox', { name: 'Find a participant' });
}

/** Clear, or `null` while the field is empty. */
function clearButton(): HTMLElement | null {
  return within(search()!).queryByRole('button', { name: 'Clear search' });
}

/** The search's own status region, which says the no-match sentence once typing pauses. */
function searchStatus(): HTMLElement {
  return within(search()!).getByRole('status');
}

/** The no-match sentence in place of the rows, beside the search's status, or `null` where there is none. */
function noMatchSentence(): HTMLElement | null {
  const shown = within(main())
    .queryAllByText(/^No participant matches/)
    .filter((element) => !search()?.contains(element));
  expect(shown.length).toBeLessThanOrEqual(1);
  return shown[0] ?? null;
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain(): HTMLElement {
  const button = within(main()).getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets the next screen render. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
}

/** Puts `value` in `field`, as typing or pasting it does. */
function type(field: HTMLElement, value: string) {
  fireEvent.change(field, { target: { value } });
}

/** Goes back one entry, as the browser's Back does, and lets the screen render. */
async function goBack(router: Router) {
  await act(() => router.navigate(-1));
  await settle();
}

/** Scrolls the page to `y`, as the person does, with the scroll event that the browser fires. */
function scrollPage(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, configurable: true, writable: true });
  window.dispatchEvent(new Event('scroll'));
}

/** The window regains focus, as the browser says so. */
function focusWindow() {
  act(() => {
    window.dispatchEvent(new FocusEvent('focus'));
  });
}

/** The page becomes visible again, as the browser says so, at the document. */
function becomeVisible() {
  act(() => {
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });
}

const browserRouters: Router[] = [];

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string, with its method.
  fetchMock.mockImplementation((path, init) => {
    const request = `${init?.method ?? 'GET'} ${path as string}`;
    const answer = answers.get(request);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${request}.`);
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  serve(antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
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
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('Today', () => {
  it('lists every participant in the order the API gives, each a row whose link names them alone', async () => {
    await openToday();

    expect(listedNames()).toEqual(names);
    for (const link of rowLinks()) {
      // No second line: nobody has a note today (A17).
      expect(link).not.toHaveAttribute('aria-describedby');
      expect(link.closest('li')?.querySelector('p')).toBeNull();
    }
  });

  it.each(people)("links $givenName's row to their note for the list's date, as a return point", async (person) => {
    await openToday();

    const link = rowLink(`${person.givenName} ${person.familyName}`);
    expect(link).toHaveAttribute('href', `/participants/${person.id}/notes/2026-10-01`);
    expect(link).toHaveAttribute('data-return-key', person.id);
  });

  it('shows its <h1>, "Today · Thursday 1 October", the hidden <h2>, Find a participant, then the rows', async () => {
    await openToday();

    expect(shownHeading()).toBe(`Today${nbsp}· Thursday 1${nbsp}October`);
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(pageHeading().querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    const hidden = within(main()).getByRole('heading', { level: 2 });
    expect(hidden).toHaveAccessibleName('Participants');
    expect(hidden).toHaveClass('visually-hidden');
    expect(within(main()).getAllByRole('heading', { level: 2 })).toHaveLength(1);
    const inOrder = [pageHeading(), hidden, search()!, field(), rowList()!, rowLink('Zara Ali'), rowLink('Ana Ward')];
    for (const [before, after] of inOrder.slice(0, -1).map((element, index) => [element, inOrder[index + 1]])) {
      expect(before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it("uses the server's date for the <h1> and every row, whatever the device's clock and time zone", async () => {
    // The device on 3 October, in a zone behind UTC; the server's day in Melbourne is Sunday 4 October.
    vi.setSystemTime(new Date('2026-10-03T07:30:00-07:00'));
    vi.stubEnv('TZ', 'America/Los_Angeles');
    expect(new Date().getDate()).toBe(3);
    await openToday({ ...worker, today: '2026-10-04' }, listOf(people, '2026-10-04'));

    expect(shownHeading()).toBe(`Today${nbsp}· Sunday 4${nbsp}October`);
    expect(pageHeading()).toHaveAccessibleName(`Today, Sunday 4${nbsp}October`);
    const links = rowLinks();
    expect(links).toHaveLength(people.length);
    for (const link of links) {
      expect(link.getAttribute('href')).toMatch(/\/notes\/2026-10-04$/);
    }
  });

  it('wraps long, unbroken and accented names, never cutting them short', async () => {
    const long = madeUp(
      '2c9f6b3e-8a14-4e70-9d25-4b7e1f0a6c58',
      'Maximiliana Alexandrina Konstantina Josefina',
      'Featherstonehaugh-Cholmondeley-Marjoribanks-Ffoulkes-Ng',
    );
    const unbroken = madeUp('5d3b9f1a-6e48-4a27-b0c3-8f2d5e7a1b96', 'Lee', 'Wolfeschlegelsteinhausenbergerdorff');
    const hyphenated = madeUp('0a9e7c5b-3d21-4f86-b4e2-6c8a1f3d5b70', 'Kate', 'O’Brien-Smith');
    const accented = madeUp('9c4e2a7f-8b13-4d65-a0f9-3e7b1c5d9a24', 'Zoë', 'Hill');
    const longName = `${long.givenName} ${long.familyName}`;
    expect(longName).toHaveLength(100);
    await openToday(worker, listOf([long, unbroken, hyphenated, accented]));

    expect(listedNames()).toEqual([
      longName,
      'Lee Wolfeschlegelsteinhausenbergerdorff',
      'Kate O’Brien-Smith',
      'Zoë Hill',
    ]);
    for (const link of rowLinks()) {
      expect(link).toHaveStyle({ overflowWrap: 'anywhere' });
      expect(link).not.toHaveStyle({ textOverflow: 'ellipsis' });
      expect(link).not.toHaveStyle({ whiteSpace: 'nowrap' });
      expect(link).not.toHaveStyle({ overflow: 'hidden' });
    }
  });

  it('shows no count and none of "Not started", "Missing", "No note" or "Overdue"', async () => {
    await openToday();

    const belowHeading = (main().textContent ?? '').replace(pageHeading().textContent, '');
    expect(belowHeading).not.toMatch(/\d/);
    expect(belowHeading).not.toMatch(/Not started|Missing|No note|Overdue/i);
  });

  it('titles the page "Grow2Notes – Today", with nothing of the list in it', async () => {
    await openToday();

    expect(document.title).toBe('Grow2Notes – Today');
  });
});

describe("Today, each row's note", () => {
  it.each([
    { form: 'your own draft', note: myDraft, shown: `Draft${nbsp}· You${nbsp}· saved 9:42${nbsp}am` },
    { form: "someone else's draft", note: alexsDraft, shown: `Draft${nbsp}· Alex P.${nbsp}· started 9:14${nbsp}am` },
    {
      form: "someone else's submitted note",
      note: alexsSubmitted,
      shown: `Submitted${nbsp}· Alex P.${nbsp}· 4:12${nbsp}pm`,
    },
    { form: 'your own submitted note', note: mySubmitted, shown: `Submitted${nbsp}· You${nbsp}· 4:12${nbsp}pm` },
  ])('shows $form as "$shown", read as the row link\'s description', async ({ note, shown }) => {
    await openToday(worker, listOf([zara, madeUp(jane.id, 'Jane', 'Citizen', note), ravi]));

    const link = rowLink('Jane Citizen');
    expect(link).toHaveAccessibleName('Jane Citizen');
    expect(shownStatus('Jane Citizen')).toBe(shown);
    // Each dot hidden from screen readers, which read a comma in its place.
    expect(link).toHaveAccessibleDescription(shown.replaceAll(`${nbsp}·`, ','));
    const line = statusLine(link)!;
    expect(line.tagName).toBe('P');
    expect(link.closest('li')).toContainElement(line);
    expect(within(line).queryByText('Flagged')).toBeNull();
  });

  it.each([
    { role: 'a worker', me: worker },
    { role: 'a manager', me: manager },
  ])("reads Alex P.'s draft as \"Draft · Alex P. · started 9:14 am\" for $role alike", async ({ me }) => {
    await openToday(me, listOf([madeUp(jane.id, 'Jane', 'Citizen', alexsDraft)]));

    expect(shownStatus('Jane Citizen')).toBe(`Draft${nbsp}· Alex P.${nbsp}· started 9:14${nbsp}am`);
  });

  it('makes the status word alone bold, and puts each time in a <time> of its instant in UTC', async () => {
    await openToday(
      worker,
      listOf([madeUp(jane.id, 'Jane', 'Citizen', myDraft), madeUp(ravi.id, 'Ravi', 'de Silva', alexsSubmitted)]),
    );

    for (const [name, word, utc, shown] of [
      ['Jane Citizen', 'Draft', '2026-09-30T23:42:00Z', `9:42${nbsp}am`],
      ['Ravi de Silva', 'Submitted', '2026-10-01T06:12:00Z', `4:12${nbsp}pm`],
    ]) {
      const line = statusLine(rowLink(name))!;
      const bold = Array.from(line.querySelectorAll('span')).filter(
        (span) => getComputedStyle(span).fontWeight === '700',
      );
      expect(bold.map((span) => span.textContent)).toEqual([word]);
      const times = line.querySelectorAll('time');
      expect(times).toHaveLength(1);
      expect(times[0]).toHaveAttribute('datetime', utc);
      expect(times[0].textContent).toBe(shown);
    }
  });

  it('keeps each part on one line where it fits, and wraps a long name inside its part, never cut short', async () => {
    const long = 'Alexandra Konstantinopoulou-Featherstonehaugh';
    await openToday(worker, listOf([madeUp(jane.id, 'Jane', 'Citizen', { ...alexsDraft, authorDisplayName: long })]));

    const line = statusLine(rowLink('Jane Citizen'))!;
    expect(shownStatus('Jane Citizen')).toBe(`Draft${nbsp}· ${long}${nbsp}· started 9:14${nbsp}am`);
    expect(line).toHaveStyle({ overflowWrap: 'anywhere' });
    const parts = Array.from(line.querySelectorAll(`.${noteStatus.part}`));
    expect(parts.map((part) => part.textContent)).toEqual([
      `Draft${nbsp}·, `,
      `${long}${nbsp}·, `,
      `started 9:14${nbsp}am`,
    ]);
    for (const part of parts) {
      expect(part).toHaveStyle({ display: 'inline-block' });
      expect(part).not.toHaveStyle({ whiteSpace: 'nowrap' });
      expect(part).not.toHaveStyle({ textOverflow: 'ellipsis' });
    }
  });

  it('shows Flagged after the line of a flagged submitted note alone, after a pause, never on a draft', async () => {
    await openToday(
      worker,
      listOf([
        // A draft's flag is seen by no one else (design.md §3.6), so no draft shows it, whatever it holds.
        madeUp(zara.id, 'Zara', 'Ali', { ...alexsDraft, isFlagged: true }),
        madeUp(jane.id, 'Jane', 'Citizen', { ...alexsSubmitted, isFlagged: true }),
        madeUp(ravi.id, 'Ravi', 'de Silva', alexsSubmitted),
      ]),
    );

    expect(shownStatus('Jane Citizen')).toBe(`Submitted${nbsp}· Alex P.${nbsp}· 4:12${nbsp}pm Flagged`);
    expect(rowLink('Jane Citizen')).toHaveAccessibleDescription(`Submitted, Alex P., 4:12${nbsp}pm, Flagged`);
    const tag = within(statusLine(rowLink('Jane Citizen'))!).getByText('Flagged');
    expect(tag).toHaveClass(tagStyles.tag, tagStyles.attention);
    // After a space, where the line breaks when the tag does not fit, so that it drops to its own line, as at 320 px.
    expect(tag.previousSibling?.textContent).toBe(' ');
    for (const name of ['Zara Ali', 'Ravi de Silva']) {
      expect(within(statusLine(rowLink(name))!).queryByText('Flagged')).toBeNull();
    }
  });

  it('says "midday" for a time of exactly 12:00 in Melbourne, 02:00Z on 1 October 2026', async () => {
    const atMidday = { ...alexsSubmitted, firstSubmittedAtUtc: '2026-10-01T02:00:00Z' };
    await openToday(worker, listOf([madeUp(jane.id, 'Jane', 'Citizen', atMidday)]));

    expect(shownStatus('Jane Citizen')).toBe(`Submitted${nbsp}· Alex P.${nbsp}· midday`);
    expect(statusLine(rowLink('Jane Citizen'))?.querySelector('time')).toHaveAttribute(
      'datetime',
      '2026-10-01T02:00:00Z',
    );
  });

  // Daylight saving starts at 2 am on Sunday 4 October 2026: 15:59Z on 3 October is 1:59 am in standard time (UTC+10),
  // and 16:01Z, two minutes on, 3:01 am in daylight time (UTC+11).
  it('gives the right time just before and just after the change to daylight saving on 4 October 2026', async () => {
    const before = { ...alexsDraft, startedAtUtc: '2026-10-03T15:59:00Z' };
    const after = {
      ...alexsSubmitted,
      startedAtUtc: '2026-10-03T15:30:00Z',
      firstSubmittedAtUtc: '2026-10-03T16:01:00Z',
    };
    await openToday(
      { ...worker, today: '2026-10-04' },
      listOf([madeUp(jane.id, 'Jane', 'Citizen', before), madeUp(ravi.id, 'Ravi', 'de Silva', after)], '2026-10-04'),
    );

    expect(shownStatus('Jane Citizen')).toBe(`Draft${nbsp}· Alex P.${nbsp}· started 1:59${nbsp}am`);
    expect(shownStatus('Ravi de Silva')).toBe(`Submitted${nbsp}· Alex P.${nbsp}· 3:01${nbsp}am`);
  });

  it('gives a row with no note no line and no description, beside rows with one', async () => {
    await openToday(
      worker,
      listOf([madeUp(zara.id, 'Zara', 'Ali', alexsDraft), jane, madeUp(ravi.id, 'Ravi', 'de Silva', mySubmitted)]),
    );

    const link = rowLink('Jane Citizen');
    expect(link).not.toHaveAttribute('aria-describedby');
    expect(link).toHaveAccessibleDescription('');
    expect(link.closest('li')?.querySelector('p')).toBeNull();
    expect(statusLine(rowLink('Zara Ali'))).not.toBeNull();
    expect(statusLine(rowLink('Ravi de Silva'))).not.toBeNull();
  });
});

describe('Today, opening a row', () => {
  // On a browser router, as `main.tsx` uses, so that the history is the window's own.
  it("pushes the list date's note with no router state, leaving only React Router's own in history.state", async () => {
    serve(mePath, json(worker));
    serve(todayPath, json(listOf(people)));
    window.history.replaceState(null, '', '/');
    const client = createQueryClient();
    client.setQueryData(meQuery.queryKey, worker);
    const router = createBrowserRouter(routes);
    browserRouters.push(router);
    show(router, client);
    await settle();
    const entries = window.history.length;

    await press(rowLink('Jane Citizen'));

    expect(window.location.pathname).toBe(`/participants/${jane.id}/notes/2026-10-01`);
    expect(window.history.length).toBe(entries + 1);
    expect(router.state.location.state).toBeNull();
    expect(window.history.state).toEqual({ usr: null, key: expect.any(String) as string, idx: 1 });
  });

  it('focuses the row opened on the way back, where the list was', async () => {
    const { router } = await openToday();
    scrollPage(600);
    await press(rowLink('Priya Kumar'));
    expect(router.state.location.pathname).toBe(`/participants/${priya.id}/notes/2026-10-01`);
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    await goBack(router);

    expect(router.state.location.pathname).toBe('/');
    expect(rowLink('Priya Kumar')).toHaveFocus();
    expect(focus).toHaveBeenLastCalledWith({ preventScroll: true });
    expect(window.scrollY).toBe(600);
  });

  // Today fetches its list only as it shows and as the page comes back into view, so a row's line can be out of date;
  // the row never opens by it, as the note's page asks the server and shows what it says then.
  it("opens the server's empty form at a row whose line says Alex P.'s draft, which was discarded since", async () => {
    const { router } = await openToday(worker, listOf([madeUp(jane.id, 'Jane', 'Citizen', alexsDraft)]));
    serveNoteOf(jane, json(newNote));

    await press(rowLink('Jane Citizen'));
    await settle();

    expect(router.state.location.pathname).toBe(`/participants/${jane.id}/notes/2026-10-01`);
    expect(within(main()).getByRole('textbox', { name: '3. Guided notes' })).toHaveValue('');
    expect(main()).not.toHaveTextContent(/started/);
  });

  it('says who started the note, as the server says, at a row with no line, as Alex P. began it since', async () => {
    const { router } = await openToday(worker, listOf([jane]));
    serveNoteOf(
      jane,
      problem(409, {
        code: 'note.in_progress_by_other',
        authorDisplayName: 'Alex P.',
        startedAtUtc: '2026-09-30T23:14:00Z',
      }),
    );

    await press(rowLink('Jane Citizen'));
    await settle();

    expect(router.state.location.pathname).toBe(`/participants/${jane.id}/notes/2026-10-01`);
    expect(within(main()).getByText(/started/).textContent).toBe(
      `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
    expect(within(main()).queryByRole('textbox')).toBeNull();
  });
});

describe('Today while the list loads', () => {
  it('shows its <h1> at once, then nothing more until "Loading participants…" at 1 s', async () => {
    serve(todayPath, noAnswer);
    renderApp('/');

    expect(document.title).toBe('Grow2Notes – Today');
    expect(shownHeading()).toBe(`Today${nbsp}· Thursday 1${nbsp}October`);
    // In the page from the first render, empty, so that the words written into it later are read.
    expect(line()).toBeEmptyDOMElement();
    expect(main().textContent).toBe(pageHeading().textContent);

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(rowList()).toBeNull();

    await wait(1);
    expect(line()).toHaveTextContent(/^Loading participants…$/);
    expect(main().textContent).toBe(`${pageHeading().textContent}Loading participants…`);
    expect(document.title).toBe('Grow2Notes – Today');
  });

  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])('says the list did not load: $cause, with Try again, after its silent retry', async ({ cause, answer }) => {
    serve(todayPath, answer);
    renderApp('/');

    await wait(999);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    await wait(1);
    await settle();

    expect(line().textContent).toBe(`The participant list did not load: ${cause}. Try again.`);
    const tryAgain = within(main()).getByRole('button', { name: 'Try again' });
    expect(pageHeading().compareDocumentPosition(line()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(line().compareDocumentPosition(tryAgain) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(main()).queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    expect(rowList()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Today');
  });

  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])('marks Try again busy while it runs, then says the list still did not load: $cause, focus kept', async ({
    cause,
    answer,
  }) => {
    serve(todayPath, answer);
    renderApp('/');
    await wait(1000);
    await settle();
    const again = held(answer);
    serve(todayPath, again.answer);

    const button = pressTryAgain();
    await settle();

    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toBeDisabled();
    expect(line()).toBeEmptyDOMElement();

    again.release();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe(`The participant list still did not load: ${cause}. Try again.`);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
  });

  it('shows the rows after a Try again that loads, with focus on the <h1>', async () => {
    serve(todayPath, noConnection);
    renderApp('/');
    await wait(1000);
    await settle();
    serve(todayPath, json(listOf(people)));

    pressTryAgain();
    await settle();

    expect(listedNames()).toEqual(names);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });
});

describe('Today with no participants', () => {
  it('says "No participants yet." to a worker, with nothing to do about it', async () => {
    await openToday(worker, listOf([]));

    expect(within(main()).getByText('No participants yet.').tagName).toBe('P');
    expect(addParticipant()).toBeNull();
    expect(rowList()).toBeNull();
    expect(within(main()).getByRole('heading', { level: 2 })).toHaveAccessibleName('Participants');
    expect(document.title).toBe('Grow2Notes – Today');
  });

  it('gives a manager Add participant under the sentence, to the Add participant page', async () => {
    const { router } = await openToday(manager, listOf([]));

    const add = addParticipant()!;
    expect(add).toHaveAttribute('href', '/manage/participants/new');
    expect(within(main()).getByText('No participants yet.').compareDocumentPosition(add)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );

    await press(add);

    expect(router.state.location.pathname).toBe('/manage/participants/new');
    expect(pageHeading()).toHaveAccessibleName('Add participant');
  });

  it('gives a manager with participants no Add participant', async () => {
    await openToday(manager);

    expect(addParticipant()).toBeNull();
  });
});

describe('Today, finding a participant', () => {
  /** Opens Today for a worker, Alex Pham, on the people whose names test the matching. */
  function openFindable() {
    return openToday(worker, listOf(findable));
  }

  it('gives Find a participant its label and description, with nothing typed, no Clear and nothing said', async () => {
    await openFindable();

    const input = field();
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveValue('');
    expect(input).toHaveAccessibleDescription('The list of participants below changes as you type.');
    expect(input).not.toHaveAttribute('placeholder');
    expect(input).not.toHaveFocus();
    expect(clearButton()).toBeNull();
    expect(search()!.querySelector('form')).toBeNull();
    // In the page from the start, empty, so that the words written into it later are read.
    expect(searchStatus()).toBeEmptyDOMElement();
    expect(listedNames()).toEqual(findableNames);
  });

  it.each(['jan', 'JANE', 'cit jane', 'citizen, jane', 'Jane.'])('finds Jane Citizen alone for "%s"', async (typed) => {
    await openFindable();

    type(field(), typed);

    expect(listedNames()).toEqual(['Jane Citizen']);
  });

  it.each([
    ['zoe', 'Zoë Hill'],
    ['obr', 'Seán O’Brien'],
  ])('finds "%s" as %s, accents and apostrophes aside', async (typed, name) => {
    await openFindable();

    type(field(), typed);

    expect(listedNames()).toEqual([name]);
  });

  it('matches "Alex" to only the names that hold it, never a row whose note Alex P. or Alex Pham wrote', async () => {
    // Everyone else has a note today, Alex P.'s, or that of Alex Pham, who is signed in.
    const written = findable.map((person) =>
      person === mia || person === alex ? person : { ...person, note: person === jane ? mySubmitted : alexsDraft },
    );
    await openToday(worker, listOf(written));

    type(field(), 'Alex');

    expect(listedNames()).toEqual(['Mia Alexander', 'Alex Nguyen']);
  });

  it('filters the rows on every key, in the API order, with focus kept in the field and no request', async () => {
    await openFindable();
    const requests = fetchMock.mock.calls.length;
    field().focus();

    type(field(), 'a');
    // Sorting by the full name would put Alex Nguyen first.
    expect(listedNames()).toEqual(['Mia Alexander', 'Zara Ali', 'Alex Nguyen', 'Ana Ward']);
    type(field(), 'al');
    expect(listedNames()).toEqual(['Mia Alexander', 'Zara Ali', 'Alex Nguyen']);
    type(field(), 'ali');
    expect(listedNames()).toEqual(['Zara Ali']);
    await wait(10_000);

    expect(field()).toHaveValue('ali');
    expect(field()).toHaveFocus();
    expect(fetchMock.mock.calls.length).toBe(requests);
  });

  it.each(['   ', '.'])('shows every row and says nothing for "%s"', async (typed) => {
    await openFindable();

    type(field(), typed);
    await wait(1000);

    expect(listedNames()).toEqual(findableNames);
    expect(noMatchSentence()).toBeNull();
    expect(searchStatus()).toBeEmptyDOMElement();
  });

  it('shows the no-match sentence at once in place of the rows, and says it once typing pauses 500 ms', async () => {
    await openFindable();

    type(field(), 'x');
    await wait(300);
    type(field(), 'xy');
    await wait(300);
    type(field(), ' xyz ');

    expect(rowList()).toBeNull();
    // From the text as typed, its ends trimmed, after the search box and outside it, with no role and in plain text.
    const sentence = noMatchSentence()!;
    expect(sentence).toHaveTextContent(/^No participant matches ‘xyz’\.$/);
    expect(sentence.tagName).toBe('P');
    expect(sentence).not.toHaveAttribute('role');
    expect(sentence).toHaveStyle({ overflowWrap: 'anywhere' });
    expect(search()!.compareDocumentPosition(sentence)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(addParticipant()).toBeNull();
    // Not said while the keys came less than 500 ms apart.
    expect(searchStatus()).toBeEmptyDOMElement();

    await wait(499);
    expect(searchStatus()).toBeEmptyDOMElement();
    await wait(1);
    expect(searchStatus()).toHaveTextContent(/^No participant matches ‘xyz’\.$/);
    expect(field()).toHaveValue(' xyz ');
    expect(clearButton()).toBeInTheDocument();
  });

  it('brings the rows back at once when something matches again, and empties the search status', async () => {
    await openFindable();
    type(field(), 'xyz');
    await wait(500);
    expect(searchStatus()).toHaveTextContent(/^No participant matches ‘xyz’\.$/);

    type(field(), 'z');

    expect(listedNames()).toEqual(['Zara Ali', 'Zoë Hill']);
    expect(noMatchSentence()).toBeNull();
    await wait(500);
    expect(searchStatus()).toBeEmptyDOMElement();
  });

  it('shows Clear only while there is text, which it empties, keeping focus in the field, all rows back', async () => {
    await openFindable();
    field().focus();
    type(field(), 'xyz');

    const clear = clearButton()!;
    expect(clear).toHaveTextContent('Clear');
    // Its mouse-down never takes focus from the field, so a phone keeps its keyboard open.
    expect(fireEvent.mouseDown(clear)).toBe(false);
    fireEvent.click(clear);

    expect(field()).toHaveValue('');
    expect(field()).toHaveFocus();
    expect(clearButton()).toBeNull();
    expect(noMatchSentence()).toBeNull();
    expect(listedNames()).toEqual(findableNames);
  });

  it('puts focus back in the field when Clear is pressed from the keyboard', async () => {
    await openFindable();
    type(field(), 'jane');

    await press(clearButton()!);

    expect(field()).toHaveValue('');
    expect(field()).toHaveFocus();
    expect(listedNames()).toEqual(findableNames);
  });

  it('changes nothing for Enter or Escape in the field', async () => {
    const { router } = await openFindable();
    field().focus();
    type(field(), 'jane');
    const requests = fetchMock.mock.calls.length;

    for (const key of ['Enter', 'Escape']) {
      fireEvent.keyDown(field(), { key });
      fireEvent.keyUp(field(), { key });
    }
    await settle();

    expect(field()).toHaveValue('jane');
    expect(field()).toHaveFocus();
    expect(listedNames()).toEqual(['Jane Citizen']);
    expect(router.state.location.pathname).toBe('/');
    expect(fetchMock.mock.calls.length).toBe(requests);
  });

  it('keeps the field, its text and focus through a refetch, filtering the list that comes back', async () => {
    await openFindable();
    const input = field();
    input.focus();
    type(input, 'jan');
    const list = held(json(listOf([...findable, madeUp('2b7d9e4f-3a61-4c85-9e02-6f1a8c3d5b47', 'Janet', 'Young')])));
    serve(todayPath, list.answer);

    becomeVisible();
    await settle();

    // While the request is out, as it was: neither taken away nor made unavailable.
    expect(field()).toBe(input);
    expect(input).toBeEnabled();
    expect(input).not.toHaveAttribute('aria-disabled');
    expect(input).toHaveFocus();

    list.release();
    await settle();

    expect(field()).toBe(input);
    expect(input).toHaveValue('jan');
    expect(input).toHaveFocus();
    expect(listedNames()).toEqual(['Jane Citizen', 'Janet Young']);
  });

  it('keeps the field and its text when a refetch in the background fails', async () => {
    await openFindable();
    const input = field();
    type(input, 'jan');
    serve(todayPath, noConnection);

    becomeVisible();
    // The silent retry fails too.
    await wait(2000);

    expect(field()).toBe(input);
    expect(input).toHaveValue('jan');
    expect(listedNames()).toEqual(['Jane Citizen']);
    expect(line()).toBeEmptyDOMElement();
  });

  it("keeps what is typed through a new day's list, which it filters", async () => {
    await openFindable();
    type(field(), 'jan');
    serve(mePath, json({ ...worker, today: '2026-10-02' }));
    serve(todayPath, json(listOf([jane, priya], '2026-10-02')));

    focusWindow();
    await settle();

    expect(shownHeading()).toBe(`Today${nbsp}· Friday 2${nbsp}October`);
    expect(field()).toHaveValue('jan');
    expect(listedNames()).toEqual(['Jane Citizen']);
    expect(rowLink('Jane Citizen')).toHaveAttribute('href', `/participants/${jane.id}/notes/2026-10-02`);
  });

  // On a browser router, as `main.tsx` uses, so that the address and history are the window's own.
  it('keeps what is typed out of the address, history.state, the title, the query keys and storage', async () => {
    serve(mePath, json(worker));
    serve(todayPath, json(listOf(findable)));
    window.history.replaceState(null, '', '/');
    const client = createQueryClient();
    client.setQueryData(meQuery.queryKey, worker);
    const router = createBrowserRouter(routes);
    browserRouters.push(router);
    show(router, client);
    await settle();

    type(field(), 'Citizen');
    await wait(1000);

    expect(listedNames()).toEqual(['Jane Citizen']);
    expect(`${window.location.pathname}${window.location.search}${window.location.hash}`).toBe('/');
    expect(router.state.location.state).toBeNull();
    expect(JSON.stringify(window.history.state)).not.toMatch(/citizen/i);
    expect(document.title).toBe('Grow2Notes – Today');
    const keys = client.getQueryCache().getAll().map(({ queryKey }) => queryKey);
    expect(keys).toEqual([['me'], ['today', '2026-10-01']]);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it('is gone after a row is opened and Today comes back, which focuses that row', async () => {
    const { router } = await openFindable();
    type(field(), 'kum');
    expect(listedNames()).toEqual(['Priya Kumar']);
    await press(rowLink('Priya Kumar'));
    expect(router.state.location.pathname).toBe(`/participants/${priya.id}/notes/2026-10-01`);

    await goBack(router);

    expect(router.state.location.pathname).toBe('/');
    expect(field()).toHaveValue('');
    expect(clearButton()).toBeNull();
    expect(listedNames()).toEqual(findableNames);
    expect(rowLink('Priya Kumar')).toHaveFocus();
  });

  it('is gone after leaving Today by the nav and coming back by it', async () => {
    await openToday(manager, listOf(findable));
    type(field(), 'jan');
    const nav = within(screen.getByRole('banner'));

    await press(nav.getByRole('link', { name: 'Manage' }));
    expect(search()).toBeNull();
    await press(nav.getByRole('link', { name: 'Today' }));
    await settle();

    expect(field()).toHaveValue('');
    expect(listedNames()).toEqual(findableNames);
    expect(pageHeading()).toHaveFocus();
  });
});

describe('Today, Find a participant only once there is someone to find', () => {
  it('has no search box while waiting or while "Loading participants…" shows', async () => {
    serve(todayPath, noAnswer);
    renderApp('/');

    expect(search()).toBeNull();
    await wait(999);
    expect(search()).toBeNull();
    await wait(1);

    expect(line()).toHaveTextContent(/^Loading participants…$/);
    expect(search()).toBeNull();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('has no search box after a failed load, and shows it once Try again loads the list', async () => {
    serve(todayPath, noConnection);
    renderApp('/');
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The participant list did not load: no connection. Try again.');
    expect(search()).toBeNull();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();

    serve(todayPath, json(listOf(people)));
    pressTryAgain();
    await settle();

    expect(field()).toHaveValue('');
    expect(listedNames()).toEqual(names);
  });

  it.each([
    { role: 'worker', me: worker },
    { role: 'manager', me: manager },
  ])('has no search box for a $role when there are no participants', async ({ me }) => {
    await openToday(me, listOf([]));

    expect(within(main()).getByText('No participants yet.')).toBeInTheDocument();
    expect(search()).toBeNull();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
  });
});

describe('Today, refreshed', () => {
  // A click back into a window left visible beside another gives it focus alone.
  it.each([
    { trigger: 'the page becomes visible', fire: becomeVisible },
    { trigger: 'the window regains focus', fire: focusWindow },
  ])('fetches the list again, once, as $trigger, keeping focus and scroll, then polls nothing', async ({ fire }) => {
    await openToday();
    const link = rowLink('Ravi de Silva');
    link.focus();
    scrollPage(400);
    const before = sent(todayPath);
    serve(todayPath, json(listOf([...people, madeUp('0e4c8a2f-7b19-4d63-a5f0-9c2e6b8d4a17', 'Mei', 'Zhang')])));

    fire();
    await settle();

    expect(sent(todayPath)).toBe(before + 1);
    expect(listedNames()).toEqual([...names, 'Mei Zhang']);
    expect(rowLink('Ravi de Silva')).toBe(link);
    expect(link).toHaveFocus();
    expect(window.scrollY).toBe(400);
    await wait(10 * 60_000);
    expect(sent(todayPath)).toBe(before + 1);
  });

  // A return to the tab gives both, each in a task of its own, the second while the first's request is out.
  it.each([
    { order: 'visible, then focus', first: becomeVisible, second: focusWindow },
    { order: 'focus, then visible', first: focusWindow, second: becomeVisible },
  ])('sends one request for both of a return to the tab ($order)', async ({ first, second }) => {
    await openToday();
    const before = sent(todayPath);
    const list = held(json(listOf(people)));
    serve(todayPath, list.answer);

    first();
    await settle();
    second();
    await settle();
    list.release();
    await settle();

    expect(sent(todayPath)).toBe(before + 1);
    expect(listedNames()).toEqual(names);
  });

  it('polls nothing: a page left alone sends no request', async () => {
    await openToday();
    const requests = fetchMock.mock.calls.length;

    await wait(10 * 60_000);

    expect(fetchMock.mock.calls.length).toBe(requests);
  });

  it("keeps the server's order through a refetch, never sorting the rows itself", async () => {
    await openToday();
    // Renamed meanwhile, which moves Zara to the end in the server's order.
    const renamed = { ...zara, familyName: 'Young' };
    serve(todayPath, json(listOf([jane, ravi, priya, sean, ana, renamed])));

    becomeVisible();
    await settle();

    expect(listedNames()).toEqual([...names.slice(1), 'Zara Young']);
  });

  it('leaves the rows as they were when a refetch in the background fails', async () => {
    await openToday();
    serve(todayPath, noConnection);

    becomeVisible();
    // The silent retry fails too.
    await wait(2000);

    expect(listedNames()).toEqual(names);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });

  it("loads the new day's list when /me gives a new today, linking its rows on the list's date", async () => {
    await openToday();
    serve(mePath, json({ ...worker, today: '2026-10-02' }));
    serve(todayPath, json(listOf([jane, priya], '2026-10-02')));

    focusWindow();
    await settle();

    expect(shownHeading()).toBe(`Today${nbsp}· Friday 2${nbsp}October`);
    expect(listedNames()).toEqual(['Jane Citizen', 'Priya Kumar']);
    expect(rowLink('Jane Citizen')).toHaveAttribute('href', `/participants/${jane.id}/notes/2026-10-02`);
  });

  it("asks /me again, once, when the list answers for another day than the <h1>'s", async () => {
    // The start-up's refresh answers before midnight, and the list just after it.
    serve(todayPath, json(listOf(people, '2026-10-02')));
    renderApp('/');
    serve(mePath, json({ ...worker, today: '2026-10-02' }));
    await settle();
    await settle();

    expect(shownHeading()).toBe(`Today${nbsp}· Friday 2${nbsp}October`);
    expect(rowLink('Jane Citizen')).toHaveAttribute('href', `/participants/${jane.id}/notes/2026-10-02`);
    await wait(60_000);
    expect(sent(mePath)).toBe(2);
  });
});

describe('Today, arriving', () => {
  it.each(['Today', 'Grow2Notes'])('focuses the <h1> on arriving by the header link %s', async (name) => {
    serve(todayPath, json(listOf(people)));
    const { router } = renderApp('/manage', manager);
    await settle();

    await press(within(screen.getByRole('banner')).getByRole('link', { name }));

    expect(router.state.location.pathname).toBe('/');
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(pageHeading()).toHaveFocus();
    expect(listedNames()).toEqual(names);
  });
});

describe('Today, signed out', () => {
  // From a page load, whose start-up finds the session, which has ended by the time the list is asked for.
  it('shows no load error for a 401, and loads the list after signing in again in place', async () => {
    serve(mePath, json(worker));
    serve(todayPath, problem(401));
    serve('/api/auth/login', json({ next: 'totp' }), 'POST');
    serve('/api/auth/login/totp', json(worker), 'POST');
    show(memoryRouterAt('/'), createQueryClient());
    await settle();
    await settle();

    expect(within(main()).getByRole('heading', { level: 1 })).toHaveAccessibleName('You’ve been signed out');
    const hiddenScreen = main().querySelector<HTMLElement>(':scope > [hidden]')!;
    expect(hiddenScreen.querySelector('[role="status"]:not(.visually-hidden)')).toBeEmptyDOMElement();
    expect(hiddenScreen.querySelector('button')).toBeNull();

    serve(todayPath, json(listOf(people)));
    type(screen.getByRole('textbox', { name: 'Email address' }), 'alex.pham@example.org');
    type(screen.getByLabelText('Password'), 'correct horse battery staple');
    await press(screen.getByRole('button', { name: 'Continue' }));
    await settle();
    type(screen.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' }), '123456');
    await press(screen.getByRole('button', { name: 'Sign in' }));
    await settle();

    expect(listedNames()).toEqual(names);
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
  });
});

describe('Today after a change to a participant', () => {
  /** Whether the Today list cached for 1 October is marked for fetching again. */
  function todayInvalidated(client: QueryClient): boolean | undefined {
    return client.getQueryState(todayQuery(manager.today).queryKey)?.isInvalidated;
  }

  /** Goes to Jane's page by its address, as the admin list gives her, from the screen shown. */
  async function goToJane(router: Router, janeNow: AdminParticipant) {
    serve(adminListPath, json([janeNow]));
    serve(`/api/admin/participants/${jane.id}/goals?includeArchived=true`, json([]));
    await act(() => router.navigate(`/manage/participants/${jane.id}`));
    await settle();
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
  }

  /** Goes back to Today by the nav, as the manager does, and lets its list load. */
  async function backToToday() {
    await press(within(screen.getByRole('banner')).getByRole('link', { name: 'Today' }));
    await settle();
  }

  it("leaves Today to fetch its list again after Add participant's 201", async () => {
    const { router, client } = await openToday(manager);
    await act(() => router.navigate('/manage/participants/new'));
    await settle();
    const newId = '0e4c8a2f-7b19-4d63-a5f0-9c2e6b8d4a17';
    serve(
      '/api/admin/participants',
      () =>
        Promise.resolve(
          new Response(JSON.stringify({ id: newId }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
        ),
      'POST',
    );
    type(within(main()).getByRole('textbox', { name: 'Given name' }), 'Mei');
    type(within(main()).getByRole('textbox', { name: 'Family name' }), 'Zhang');
    type(within(main()).getByRole('textbox', { name: 'Day' }), '7');
    type(within(main()).getByRole('textbox', { name: 'Month' }), '3');
    type(within(main()).getByRole('textbox', { name: 'Year' }), '1987');
    expect(todayInvalidated(client)).toBe(false);

    await press(within(main()).getByRole('button', { name: 'Save' }));

    expect(router.state.location.pathname).toBe(`/manage/participants/${newId}`);
    expect(todayInvalidated(client)).toBe(true);
  });

  it("leaves Today to fetch its list again after a correction's 200, which shows the new name there", async () => {
    const { router, client } = await openToday(manager);
    await goToJane(router, janeInTheAdminList('Active', 'AAAAAAAAB9E='));
    serve(`/api/admin/participants/${jane.id}`, bodiless(200), 'PUT');
    serve(adminListPath, json([{ ...janeInTheAdminList('Active', 'AAAAAAAAB+A='), givenName: 'Janet' }]));
    const janet = { ...jane, givenName: 'Janet' };
    serve(todayPath, json(listOf(people.map((person) => (person === jane ? janet : person)))));
    type(within(main()).getByRole('textbox', { name: 'Given name' }), 'Janet');
    expect(todayInvalidated(client)).toBe(false);

    await press(within(main()).getByRole('button', { name: 'Save' }));

    expect(todayInvalidated(client)).toBe(true);
    await backToToday();
    expect(listedNames()).toEqual(names.map((name) => (name === 'Jane Citizen' ? 'Janet Citizen' : name)));
  });

  it('takes a participant archived on their page off Today, and Restore puts them back', async () => {
    const { router, client } = await openToday(manager);
    await goToJane(router, janeInTheAdminList('Active', 'AAAAAAAAB9E='));
    serve(`/api/admin/participants/${jane.id}/archive`, bodiless(204), 'POST');
    serve(adminListPath, json([janeInTheAdminList('Archived', 'AAAAAAAAB+I=')]));
    serve(todayPath, json(listOf(people.filter((person) => person !== jane))));
    expect(todayInvalidated(client)).toBe(false);

    await press(within(main()).getByRole('button', { name: 'Archive participant' }));

    expect(todayInvalidated(client)).toBe(true);
    await backToToday();
    expect(listedNames()).toEqual(names.filter((name) => name !== 'Jane Citizen'));

    await goToJane(router, janeInTheAdminList('Archived', 'AAAAAAAAB+I='));
    serve(`/api/admin/participants/${jane.id}/restore`, bodiless(204), 'POST');
    serve(adminListPath, json([janeInTheAdminList('Active', 'AAAAAAAAB+M=')]));
    serve(todayPath, json(listOf(people)));
    expect(todayInvalidated(client)).toBe(false);

    await press(within(main()).getByRole('button', { name: 'Restore' }));

    expect(todayInvalidated(client)).toBe(true);
    await backToToday();
    expect(listedNames()).toEqual(names);
  });
});
