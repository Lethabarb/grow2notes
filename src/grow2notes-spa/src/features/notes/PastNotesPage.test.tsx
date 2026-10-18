import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import tagStyles from '../../components/Tag.module.css';
import { routes } from '../../routes/routes.tsx';
import type { TodayList } from '../today/todayQuery.ts';
import type { Draft, ParticipantHeader, SubmittedNote } from './noteQueries.ts';
import type { PastNote } from './pastNotesQuery.ts';

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';

// participant-notes.md's made-up people (A35): Casey Nguyen, a worker, signed in, and Jordan Ellis, a manager, of the
// same organisation; today in Melbourne is Thursday 1 October 2026.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Casey Nguyen',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const manager: Me = {
  ...worker,
  userId: '9c4d2b7e-3a18-4f65-8d90-7e1b5a3c6f24',
  displayName: 'Jordan Ellis',
  role: 'Manager',
};

const jane: ParticipantHeader = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  status: 'Active',
};

// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

/** The paths of Jane's header, her history, its page, and her note for `noteDate` and its page. */
const paths = {
  header: `/api/participants/${jane.id}`,
  history: `/api/participants/${jane.id}/notes`,
  page: `/participants/${jane.id}/notes`,
  note: (noteDate: string) => `/api/participants/${jane.id}/notes/${noteDate}`,
  draft: (noteDate: string) => `/api/participants/${jane.id}/notes/${noteDate}/draft`,
  notePage: (noteDate: string) => `/participants/${jane.id}/notes/${noteDate}`,
};

/** The history's path for the page before `noteDate`. */
function before(noteDate: string): string {
  return `${paths.history}?before=${noteDate}`;
}

/** The date `days` before `date`, both written `yyyy-MM-dd`, as calendar dates. */
function daysBefore(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d - days)).toISOString().slice(0, 10);
}

/** A submitted note on the history, by Alex P. unless another author is given, not flagged unless said. */
function submitted(noteDate: string, authorDisplayName = 'Alex P.', isFlagged = false): PastNote {
  return { noteDate, status: 'Submitted', authorDisplayName, isFlagged };
}

/** `count` submitted notes, one a day, newest first, from `newest`. */
function notesFrom(newest: string, count: number): PastNote[] {
  return Array.from({ length: count }, (_, index) => submitted(daysBefore(newest, index)));
}

/** A page of the history, as `GET /api/participants/{participantId}/notes` answers it. */
function page(notes: PastNote[], hasMore: boolean) {
  return { notes, hasMore };
}

// Three pages of 30, the first starting at today's note.
const first30 = notesFrom('2026-10-01', 30);
const next30 = notesFrom(daysBefore('2026-10-01', 30), 30);
const last30 = notesFrom(daysBefore('2026-10-01', 60), 30);

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

/** Answers Jane's header, and the first page of her history with `notes`, saying `hasMore`. */
function servePastNotes(notes: PastNote[] = first30, hasMore = true) {
  serve(paths.header, json(jane));
  serve(paths.history, json(page(notes, hasMore)));
}

/** Answers Jane's note for `noteDate` as submitted, by Priya Nair at 4:42 pm that day, its form never read. */
function serveSubmittedNote(noteDate = '2026-10-01') {
  const note: SubmittedNote = {
    status: 'Submitted',
    author: { displayName: 'Priya Nair' },
    firstSubmittedAtUtc: `${daysBefore(noteDate, 0)}T06:42:00Z`,
    current: {
      narrative: 'Jane made toast.',
      goals: [{ text: 'Makes own breakfast', isTicked: true }],
      isFlagged: true,
      flagReason: 'Jane seemed unwell.',
    },
  };
  serve(paths.note(noteDate), json(note));
}

/** Answers Jane's note for `noteDate` as having none, whose form opens on a new note's template. */
function serveNewNote(noteDate = '2026-10-01') {
  const template: Draft = {
    baseVersion: 0,
    isSaved: false,
    listsVersion: 'lists-1',
    guidePrompts: '',
    goals: [],
    narrative: '',
    isFlagged: false,
    flagReason: null,
  };
  serve(paths.note(noteDate), problem(404));
  serve(paths.draft(noteDate), json(template));
}

/** Every `GET` sent, as its path. */
function sentRequests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string);
}

/**
 * The first page's requests as the page opens: two, as StrictMode mounts the page, unmounts it and mounts it again, and
 * the unmount cancels the first request, whose query function takes the abort signal, so that the second mount sends
 * it again. A production build sends it once.
 */
const firstLoad = [paths.history, paths.history];

/** Every request for a page of Jane's history, as its path. */
function historyRequests(): string[] {
  return sentRequests().filter((path) => path.startsWith(paths.history) && !path.startsWith(`${paths.history}/`));
}

/**
 * Renders the app at `path` for `me` on a memory router, under StrictMode, which runs each effect twice, as
 * `main.tsx` does, and a query client of its own, in which `['me']` holds `me` already, as after the start-up.
 */
function renderApp(path: string, me: Me = worker): { router: Router; client: QueryClient } {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // With a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a row or a position from one test into the next.
  const router = createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return { router, client };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the queries' new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets the next screen render. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
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

function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The top bar in the shell's before-main bar slot. */
function topBar(): HTMLElement {
  const bar = document.getElementById('page-bar')?.firstElementChild;
  if (!(bar instanceof HTMLElement)) {
    throw new Error('The slot holds no top bar.');
  }

  return bar;
}

/** The back link in the top bar. */
function backLink(): HTMLElement {
  return within(topBar()).getByRole('link');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The region's line in `<main>`, after the list, beside the page's visually hidden status region and Show older's. */
function line(): HTMLElement {
  return within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'))
    .at(-1)!;
}

/** Show older's status line, which follows the list and the button. */
function olderLine(): HTMLElement {
  return list().parentElement!.querySelector<HTMLElement>(':scope > [role="status"]')!;
}

/** The list of notes. */
function list(): HTMLElement {
  return within(main()).getByRole('list');
}

/** The rows' links, in order. */
function rowLinks(): HTMLElement[] {
  return within(list()).getAllByRole('link');
}

/** The row link of the note dated `noteDate`. */
function rowLink(noteDate: string): HTMLElement {
  return rowLinks().find((link) => link.dataset.returnKey === noteDate)!;
}

/** The rows' note dates, in order, from their links' return keys. */
function rowDates(): (string | undefined)[] {
  return rowLinks().map((link) => link.dataset.returnKey);
}

function showOlder(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Show older' });
}

/** Opens Jane's Past notes at its address, and lets it load. */
async function openPastNotes(me: Me = worker) {
  const opened = renderApp(paths.page, me);
  await settle();
  return opened;
}

/** Presses Show older, and lets the next page, answered with `answer`, come in. */
async function pressShowOlder(path: string, answer: Answer) {
  serve(path, answer);
  await press(showOlder());
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    if ((init?.method ?? 'GET') !== 'GET') {
      throw new Error(`Past notes sent a ${init?.method} to ${path as string}.`);
    }

    const answer = answers.get(path as string);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${path as string}.`);
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  serve('/api/auth/antiforgery', () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
  scrollPage(0);
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('Past notes while they load', () => {
  // participant-notes.md item 8: nothing for a second, then the loading line, with no spinner or skeleton.
  it('shows the back link at once, nothing for 999 ms, then "Loading notes…", and nothing of the list', async () => {
    serve(paths.header, noAnswer);
    serve(paths.history, noAnswer);
    renderApp(paths.page);

    expect(backLink()).toHaveAccessibleName('Today');
    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading notes…');
    expect(within(main()).queryByRole('heading')).toBeNull();
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main().querySelector('svg, [aria-busy], progress')).toBeNull();
  });

  // participant-notes.md component 3: the <h1> renders only once both have loaded, never a cached or partial one.
  it.each(['header', 'history'] as const)(
    'shows nothing of the list while the %s has not answered, then all of it at once',
    async (waiting) => {
      const answers = { header: json(jane), history: json(page(first30, true)) };
      const late = held(answers[waiting]);
      serve(paths.header, waiting === 'header' ? late.answer : answers.header);
      serve(paths.history, waiting === 'history' ? late.answer : answers.history);
      renderApp(paths.page);

      await settle();
      expect(within(main()).queryByRole('heading')).toBeNull();
      expect(within(main()).queryByRole('list')).toBeNull();
      expect(main()).not.toHaveTextContent(/Jane|Citizen|October/);

      late.release();
      await settle();
      expect(pageHeading()).toHaveAccessibleName('Past notes Jane Citizen');
      expect(rowLinks()).toHaveLength(30);
      expect(showOlder()).toBeInTheDocument();
    },
  );
});

describe('Past notes once loaded', () => {
  it('is titled "Grow2Notes – Past notes", with the caption and the name as stored, never translated', async () => {
    servePastNotes();
    await openPastNotes();

    expect(document.title).toBe('Grow2Notes – Past notes');
    const heading = pageHeading();
    expect(heading).toHaveAccessibleName('Past notes Jane Citizen');
    expect(within(heading).getByText('Jane Citizen')).toHaveAttribute('translate', 'no');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getAllByRole('heading')).toEqual([heading]);
  });

  // participant-notes.md item 1: the first load asks with no before, as the server's default starts at today.
  it("asks for the first page with no before, and lists its rows in the API's order, never sorting them", async () => {
    const unsorted = [submitted('2026-09-28'), submitted('2026-10-01'), submitted('2026-09-30')];
    servePastNotes(unsorted, false);
    await openPastNotes();

    expect(historyRequests()).toEqual(firstLoad);
    expect(rowDates()).toEqual(['2026-09-28', '2026-10-01', '2026-09-30']);
    expect(list().tagName).toBe('OL');
    expect(list()).toHaveAttribute('role', 'list');
  });

  // participant-notes.md items 6, 9 and 10: the date alone names the row; its status, author and tags describe it.
  it("names each row by its date alone, in a <time>, described by its status, author and tags", async () => {
    // A draft's flag is seen by no one else (design.md §3.6), so no draft row shows it, whatever comes.
    const draft = { noteDate: '2026-09-30', status: 'Draft', authorDisplayName: 'Sam Lee', isFlagged: true };
    servePastNotes(
      [submitted('2026-10-01', 'Alex P.', true), draft as unknown as PastNote, submitted('2026-09-29', 'Priya Nair')],
      false,
    );
    await openPastNotes();

    const [flagged, drafted, plain] = rowLinks();
    expect(flagged).toHaveAccessibleName(`Thursday 1${nbsp}October 2026`);
    expect(flagged).toHaveAccessibleDescription('Submitted, Alex P., Flagged');
    expect(flagged).toHaveAttribute('href', paths.notePage('2026-10-01'));
    expect(flagged.querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
    const tag = within(document.getElementById(flagged.getAttribute('aria-describedby')!)!).getByText('Flagged');
    expect(tag).toHaveClass(tagStyles.tag, tagStyles.attention);
    expect(drafted).toHaveAccessibleName(`Wednesday 30${nbsp}September 2026`);
    expect(drafted).toHaveAccessibleDescription('Draft, Sam Lee');
    expect(plain).toHaveAccessibleName(`Tuesday 29${nbsp}September 2026`);
    expect(plain).toHaveAccessibleDescription('Submitted, Priya Nair');
    expect(within(list()).getAllByText('Flagged')).toEqual([tag]);
    // Each status word bold, as on Today.
    expect(within(list()).getByText('Draft')).toHaveStyle({ fontWeight: '700' });
    expect(rowDates()).toEqual(['2026-10-01', '2026-09-30', '2026-09-29']);
  });

  it('has a static top bar, with the back link to Today, when nothing in the app opened it', async () => {
    servePastNotes();
    await openPastNotes();

    expect(topBar()).not.toHaveStyle({ position: 'sticky' });
    expect(backLink()).toHaveAccessibleName('Today');
    expect(backLink()).toHaveAttribute('href', '/');
  });

  // participant-notes.md items 4 and 11: the manager's actions, there even with no notes; Write past-day note and
  // Export record come with T18 and T21.
  it("gives a manager Edit participant, a button-styled link to the participant's page, after the <h1>", async () => {
    servePastNotes();
    await openPastNotes(manager);

    const edit = within(main()).getByRole('link', { name: 'Edit participant' });
    expect(edit).toHaveAttribute('href', `/manage/participants/${jane.id}`);
    expect(edit.className).toMatch(/button/);
    expect(pageHeading().compareDocumentPosition(edit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(edit.compareDocumentPosition(list()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('gives a worker no manager action, the first row straight after the <h1>', async () => {
    servePastNotes();
    await openPastNotes(worker);

    expect(within(main()).queryByRole('link', { name: 'Edit participant' })).toBeNull();
    expect(main()).not.toHaveTextContent(/Edit participant|Write past-day note|Export record/);
  });

  // participant-notes.md item 12.
  it.each([worker, manager])(
    'says "No notes yet for Jane Citizen." with no list or button, for a $role',
    async (me) => {
      servePastNotes([], false);
      await openPastNotes(me);

      expect(within(main()).getByText('No notes yet for Jane Citizen.').tagName).toBe('P');
      expect(within(main()).queryByRole('list')).toBeNull();
      expect(within(main()).queryByRole('button')).toBeNull();
      expect(within(main()).queryAllByRole('link').map((link) => link.textContent)).toEqual(
        me.role === 'Manager' ? ['Edit participant'] : [],
      );
    },
  );

  // participant-notes.md items 3 and 4: nothing loads on scroll, and nothing is counted.
  it('sends no request as the page scrolls, and shows no count, total or end-of-list text', async () => {
    servePastNotes();
    await openPastNotes();

    scrollPage(5_000);
    await wait(5_000);
    expect(historyRequests()).toEqual(firstLoad);

    await pressShowOlder(before('2026-09-02'), json(page(next30.slice(0, 4), false)));
    scrollPage(9_000);
    await wait(5_000);
    expect(historyRequests()).toEqual([...firstLoad, before('2026-09-02')]);
    const words = main().textContent.replace(/(?:Mon|Tues|Wednes|Thurs|Fri|Satur|Sun)day \d+\s\w+ \d{4}/g, '');
    expect(words).not.toMatch(/\d|showing|total|page|end of|no more|older notes/i);
  });

  it('refetches every page loaded, in order, when the page becomes visible again', async () => {
    servePastNotes();
    await openPastNotes();
    await pressShowOlder(before('2026-09-02'), json(page(next30, true)));
    fetchMock.mockClear();

    act(() => {
      document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
    });
    await settle();
    await settle();

    expect(historyRequests()).toEqual([paths.history, before('2026-09-02')]);
    expect(rowLinks()).toHaveLength(60);
  });
});

describe('Show older', () => {
  // participant-notes.md items 2 and 5.
  it("asks before the last row's date, adds 30 below, the rows above unmoved, focusing the first new row", async () => {
    servePastNotes();
    await openPastNotes();
    const shown = rowLinks();

    await pressShowOlder(before('2026-09-02'), json(page(next30, true)));

    expect(historyRequests()).toEqual([...firstLoad, before('2026-09-02')]);
    expect(rowDates()).toEqual([...first30, ...next30].map((note) => note.noteDate));
    expect(rowLinks().slice(0, 30)).toEqual(shown);
    expect(rowLink('2026-09-01')).toHaveFocus();
    expect(showOlder()).toBeInTheDocument();
    expect(olderLine()).toBeEmptyDOMElement();
  });

  it('shows only while the last page said hasMore: gone, focus on the first new row, never on <body>', async () => {
    servePastNotes();
    await openPastNotes();

    await pressShowOlder(before('2026-09-02'), json(page(next30.slice(0, 5), false)));

    expect(rowLinks()).toHaveLength(35);
    expect(within(main()).queryByRole('button', { name: 'Show older' })).toBeNull();
    expect(rowLink('2026-09-01')).toHaveFocus();
    expect(document.activeElement).not.toBe(document.body);
  });

  it('is not there when the first page said there are no more', async () => {
    servePastNotes(first30, false);
    await openPastNotes();

    expect(rowLinks()).toHaveLength(30);
    expect(within(main()).queryByRole('button')).toBeNull();
  });

  // chronological-list.md *Behaviour*: a race, such as a draft discarded meanwhile, can bring no row.
  it('focuses the last row when it brings no row and the button goes', async () => {
    servePastNotes();
    await openPastNotes();

    await pressShowOlder(before('2026-09-02'), json(page([], false)));

    expect(within(main()).queryByRole('button', { name: 'Show older' })).toBeNull();
    expect(rowLink('2026-09-02')).toHaveFocus();
  });

  // participant-notes.md item 6.
  it('sends one request for two quick presses, aria-disabled from the press, never disabled', async () => {
    servePastNotes();
    await openPastNotes();
    serve(before('2026-09-02'), noAnswer);
    const button = showOlder();

    button.focus();
    fireEvent.click(button);
    fireEvent.click(button);
    await settle();

    expect(historyRequests()).toEqual([...firstLoad, before('2026-09-02')]);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('disabled');
    expect(button).toHaveAccessibleName('Show older');
    expect(button).toHaveFocus();
  });

  it('says "Loading older notes…" only once it has taken 1 s, its label unchanged, and nothing once in', async () => {
    servePastNotes();
    await openPastNotes();
    const late = held(json(page(next30, true)));
    serve(before('2026-09-02'), late.answer);

    await press(showOlder());
    await wait(998);
    expect(olderLine()).toBeEmptyDOMElement();
    await wait(1);
    expect(olderLine().textContent).toBe('Loading older notes…');
    expect(showOlder()).toHaveAccessibleName('Show older');

    late.release();
    await settle();
    await settle();
    expect(olderLine()).toBeEmptyDOMElement();
    expect(rowLinks()).toHaveLength(60);
  });

  // participant-notes.md item 7: after its one silent retry, within about 2 s.
  it(
    'offline, says why, keeping the rows and focus on the button, then "still" the second time, cleared by a press',
    async () => {
      servePastNotes();
      await openPastNotes();
      serve(before('2026-09-02'), noConnection);

      await press(showOlder());
      await wait(1_000);
      await settle();

      expect(olderLine().textContent).toBe('Older notes did not load: no connection. Try again.');
      expect(rowLinks()).toHaveLength(30);
      expect(showOlder()).toHaveFocus();
      expect(showOlder()).not.toHaveAttribute('aria-disabled');

      await press(showOlder());
      expect(olderLine()).toBeEmptyDOMElement();
      await wait(1_000);
      await settle();

      expect(olderLine().textContent).toBe('Older notes still did not load: no connection. Try again.');
      expect(showOlder()).toHaveFocus();

      serve(before('2026-09-02'), json(page(next30, false)));
      await press(showOlder());
      await settle();
      expect(olderLine()).toBeEmptyDOMElement();
      expect(rowLink('2026-09-01')).toHaveFocus();
    },
  );

  it('says "something went wrong" for a server error', async () => {
    servePastNotes();
    await openPastNotes();
    serve(before('2026-09-02'), problem(500));

    await press(showOlder());
    await wait(1_000);
    await settle();

    expect(olderLine().textContent).toBe('Older notes did not load: something went wrong. Try again.');
  });
});

describe('Past notes when they did not load', () => {
  it('shows the fallback <h1>, "Past notes did not load: …" and Try again, then "still", then the list', async () => {
    serve(paths.header, json(jane));
    serve(paths.history, noConnection);
    renderApp(paths.page);
    // After its one silent retry.
    await wait(1_000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Past notes');
    expect(line().textContent).toBe('Past notes did not load: no connection. Try again.');
    const tryAgain = within(main()).getByRole('button', { name: 'Try again' });
    expect(within(main()).queryByRole('list')).toBeNull();

    await press(tryAgain);
    await wait(1_000);
    await settle();
    expect(line().textContent).toBe('Past notes still did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toHaveFocus();

    serve(paths.history, json(page(first30, true)));
    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await settle();
    expect(pageHeading()).toHaveAccessibleName('Past notes Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(rowLinks()).toHaveLength(30);
  });

  // participant-notes.md *States*: another organisation's ID, or a bad one, is Page not found.
  it('shows Page not found for a participant not in the organisation', async () => {
    serve(paths.header, problem(404));
    serve(paths.history, problem(404));
    renderApp(paths.page);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(main()).not.toHaveTextContent(/Jane|Past notes/);
  });
});

describe('Going to and from Past notes', () => {
  // participant-notes.md items 13 and 28a: the place kept in memory, and every page loaded shown again at once.
  it('shows all 90 rows at once on Back from the 75th, with the scroll kept and focus on its link', async () => {
    servePastNotes();
    const { router } = await openPastNotes();
    await pressShowOlder(before('2026-09-02'), json(page(next30, true)));
    await pressShowOlder(before('2026-08-03'), json(page(last30, true)));
    expect(rowLinks()).toHaveLength(90);
    const opened = rowLinks()[74].dataset.returnKey!;
    serveSubmittedNote(opened);
    scrollPage(4_000);

    await press(rowLink(opened));
    await settle();
    expect(router.state.location.pathname).toBe(paths.notePage(opened));
    expect(backLink()).toHaveAccessibleName('Past notes');
    expect(backLink()).toHaveAttribute('href', paths.page);
    // The refetch of every page, as the list shows again, which may run in the background and moves nothing.
    const refetch = held(json(page(first30, true)));
    serve(paths.history, refetch.answer);

    await press(backLink());

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe(paths.page);
    expect(rowLinks()).toHaveLength(90);
    expect(rowLink(opened)).toHaveFocus();
    expect(window.scrollY).toBe(4_000);

    refetch.release();
    await settle();
    await settle();
    await settle();
    expect(rowLinks()).toHaveLength(90);
    expect(rowLink(opened)).toHaveFocus();

    // And the same by browser Back.
    await press(rowLink(opened));
    await settle();
    await goBack(router);

    expect(router.state.location.pathname).toBe(paths.page);
    expect(rowLinks()).toHaveLength(90);
    expect(rowLink(opened)).toHaveFocus();
    expect(window.scrollY).toBe(4_000);
  });

  // participant-notes.md item 14: a push is a new visit, whatever an earlier one loaded.
  it("starts at the newest 30, at the top, with the <h1> focused, from a note's Past notes link", async () => {
    servePastNotes();
    serveNewNote();
    const { router } = await openPastNotes();
    await pressShowOlder(before('2026-09-02'), json(page(next30, true)));
    await press(rowLink('2026-10-01'));
    await settle();
    expect(router.state.location.pathname).toBe(paths.notePage('2026-10-01'));
    scrollPage(800);
    fetchMock.mockClear();

    await press(within(main()).getByRole('link', { name: 'Past notes' }));
    await settle();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe(paths.page);
    expect(historyRequests()).toEqual(firstLoad);
    expect(sentRequests()).toContain(paths.header);
    expect(rowLinks()).toHaveLength(30);
    expect(window.scrollY).toBe(0);
    expect(pageHeading()).toHaveFocus();
    expect(backLink()).toHaveAccessibleName('Note');
    expect(backLink()).toHaveAttribute('href', paths.notePage('2026-10-01'));
  });

  it('pops back to the note on its back link, "Note"', async () => {
    servePastNotes();
    serveNewNote();
    const { router } = renderApp(paths.notePage('2026-10-01'));
    await settle();
    await press(within(main()).getByRole('link', { name: 'Past notes' }));
    await settle();

    await press(backLink());

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe(paths.notePage('2026-10-01'));
  });

  // participant-notes.md item 28a: only the back link's label differs between the routes that open the read view.
  it('opens the same read view from Today and from Past notes, but for the back link', async () => {
    const today: TodayList = {
      date: '2026-10-01',
      participants: [
        {
          id: jane.id,
          givenName: jane.givenName,
          familyName: jane.familyName,
          note: {
            status: 'Submitted',
            authorDisplayName: 'Priya Nair',
            isMine: false,
            startedAtUtc: '2026-09-30T23:14:00Z',
            firstSubmittedAtUtc: '2026-10-01T06:42:00Z',
            isFlagged: true,
          },
        },
      ],
    };
    serve(todayPath, json(today));
    servePastNotes();
    serveSubmittedNote();
    const { router } = renderApp('/');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Jane Citizen' }));
    await settle();
    expect(backLink()).toHaveAccessibleName('Today');
    const fromToday = withoutIds(main().innerHTML);

    await press(within(main()).getByRole('link', { name: 'Past notes' }));
    await settle();
    await press(rowLink('2026-10-01'));
    await settle();

    expect(router.state.location.pathname).toBe(paths.notePage('2026-10-01'));
    expect(backLink()).toHaveAccessibleName('Past notes');
    expect(withoutIds(main().innerHTML)).toBe(fromToday);
    expect(pageHeading()).toHaveFocus();

    await press(backLink());
    expect(router.state.location.pathname).toBe(paths.page);
    expect(rowLink('2026-10-01')).toHaveFocus();
  });
});

/** `html` with the IDs that React's `useId` makes, which differ from one mount to the next, taken out. */
function withoutIds(html: string): string {
  return html.replace(/_r_[0-9a-z]+_/g, '');
}
