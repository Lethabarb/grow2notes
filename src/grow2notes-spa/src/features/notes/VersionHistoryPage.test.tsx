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
import type { NoteAuthor, ParticipantHeader, SubmittedNote } from './noteQueries.ts';
import type { NoteVersion, VersionSummary } from './versionQueries.ts';

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';

// version-history.md's made-up people (A35): Casey Nguyen, a worker, and Jordan Ellis, a manager, who is signed in,
// Priya Nair, the note's author, Sam Lee, a manager who edited it, and Jo Smith, a manager who wrote a past-day note.
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
const priya: NoteAuthor = { id: 'b7a1c0de-0000-4000-8000-000000000001', displayName: 'Priya Nair' };
const sam: NoteAuthor = { id: 'b7a1c0de-0000-4000-8000-000000000002', displayName: 'Sam Lee' };
const jo: NoteAuthor = { id: 'b7a1c0de-0000-4000-8000-000000000003', displayName: 'Jo Smith' };

const jane: ParticipantHeader = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  status: 'Active',
};

// Between a day and its month and between a time and am or pm, so that neither pair splits across lines.
const nbsp = ' ';

const noteDate = '2026-10-01';

const paths = {
  header: `/api/participants/${jane.id}`,
  note: `/api/participants/${jane.id}/notes/${noteDate}`,
  versions: `/api/participants/${jane.id}/notes/${noteDate}/versions`,
  version: (n: number) => `/api/participants/${jane.id}/notes/${noteDate}/versions/${n}`,
  notePage: `/participants/${jane.id}/notes/${noteDate}`,
  page: `/participants/${jane.id}/notes/${noteDate}/versions`,
  versionPage: (n: number) => `/participants/${jane.id}/notes/${noteDate}/versions/${n}`,
};

/** A row of the version list, as the API gives it, with its flag, which the page does not read. */
function summary(versionNumber: number, createdBy: NoteAuthor, createdAtUtc: string): VersionSummary {
  return { versionNumber, createdBy, createdAtUtc, isFlagged: false } as VersionSummary;
}

// Three versions, 4:12 pm, 4:40 pm and 5:03 pm on 1 October 2026 in Melbourne, in an order that is not the page's.
const v1 = summary(1, priya, '2026-10-01T06:12:00Z');
const v2 = summary(2, priya, '2026-10-01T06:40:00Z');
const v3 = summary(3, sam, '2026-10-01T07:03:00Z');
const shuffled = [v2, v3, v1];

/** Jane's note as `GET {base}` gives it to a manager, by `author`, with `current` at `versionNumber`. */
function submittedNote(author: NoteAuthor = priya, versionNumber = 3, isPastDayNote = false): SubmittedNote {
  return {
    status: 'Submitted',
    author,
    startedAtUtc: isPastDayNote ? '2026-10-01T00:14:00Z' : '2026-10-01T05:00:00Z',
    firstSubmittedAtUtc: '2026-10-01T06:12:00Z',
    isPastDayNote,
    isEdited: versionNumber > 1,
    ...(versionNumber > 1 ? { lastEdit: { by: sam.displayName, atUtc: '2026-10-01T07:03:00Z' } } : {}),
    current: {
      versionNumber,
      narrative: 'Jane made toast.',
      goals: [],
      commonItemGroups: [],
      isFlagged: false,
      flagReason: null,
    },
  } as SubmittedNote;
}

/** One version in full, for the row that opens it. */
function versionOf(row: VersionSummary): NoteVersion {
  return {
    versionNumber: row.versionNumber,
    narrative: `Wording of version ${row.versionNumber}.`,
    goals: [{ text: 'Makes own breakfast', isTicked: true }],
    commonItemGroups: [],
    isFlagged: false,
    flagReason: null,
    createdBy: row.createdBy,
    createdAtUtc: row.createdAtUtc,
  };
}

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

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each `GET`, by its path. */
const answers = new Map<string, Answer>();

/** Answers each `GET` of `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** Answers Jane's header, her note and its version list, and each version in full. */
function serveHistory(note: SubmittedNote = submittedNote(), versions: VersionSummary[] = shuffled) {
  serve(paths.header, json(jane));
  serve(paths.note, json(note));
  serve(paths.versions, json(versions));
  for (const row of versions) {
    serve(paths.version(row.versionNumber), json(versionOf(row)));
  }
}

/** Every `GET` sent, as its path. */
function sentRequests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string);
}

/** Renders the app at `path` for `me` on a memory router, under StrictMode and a query client of its own. */
function renderApp(path: string, me: Me = manager): { router: Router; client: QueryClient } {
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

/** Opens the version history at its address, and lets it load. */
async function openHistory(me: Me = manager) {
  const opened = renderApp(paths.page, me);
  await settle();
  return opened;
}

function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The back link in the top bar, which the shell holds in the slot before `<main>`. */
function backLink(): HTMLElement {
  const bar = document.getElementById('page-bar')?.firstElementChild;
  if (!(bar instanceof HTMLElement)) {
    throw new Error('The slot holds no top bar.');
  }

  return within(bar).getByRole('link');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The region's line in `<main>`, beside the page's visually hidden status region. */
function line(): HTMLElement {
  return within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'))
    .at(-1)!;
}

/** The list of versions. */
function list(): HTMLElement {
  return within(main()).getByRole('list');
}

/** The rows' links, in order. */
function rowLinks(): HTMLElement[] {
  return within(list()).getAllByRole('link');
}

/** The row link of version `n`. */
function rowLink(n: number): HTMLElement {
  return rowLinks().find((link) => link.dataset.returnKey === String(n))!;
}

/** The text of a time that the page shows, as the app words it. */
const at = {
  v1: `Thu 1${nbsp}Oct 2026, 4:12${nbsp}pm`,
  v2: `Thu 1${nbsp}Oct 2026, 4:40${nbsp}pm`,
  v3: `Thu 1${nbsp}Oct 2026, 5:03${nbsp}pm`,
};

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    if ((init?.method ?? 'GET') !== 'GET') {
      throw new Error(`Version history sent a ${init?.method} to ${path as string}.`);
    }

    const answer = answers.get(path as string);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${path as string}.`);
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // No unfinished drafts on Today, for a test that goes there.
  serve('/api/me/drafts', json([]));
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
  Object.defineProperty(window, 'scrollY', { value: 0, configurable: true, writable: true });
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('The version history while it loads', () => {
  // version-history.md *States*: nothing for a second, then the loading line, with no spinner or skeleton.
  it('shows the back link, nothing for 999 ms, then "Loading version history…", and none of the page', async () => {
    serve(paths.header, noAnswer);
    serve(paths.note, noAnswer);
    serve(paths.versions, noAnswer);
    renderApp(paths.page);

    expect(backLink()).toHaveAccessibleName('Note');
    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading version history…');
    expect(within(main()).queryByRole('heading')).toBeNull();
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main().querySelector('svg, [aria-busy], progress')).toBeNull();
  });
});

describe('The version history once loaded', () => {
  it('is titled "Grow2Notes – Version history", with the caption, name and note date in its <h1>', async () => {
    serveHistory();
    await openHistory();

    expect(document.title).toBe('Grow2Notes – Version history');
    expect(pageHeading()).toHaveAccessibleName(`Version history Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(within(pageHeading()).getByText('Jane Citizen')).toHaveAttribute('translate', 'no');
    expect(screen.getAllByRole('heading')).toEqual([pageHeading()]);
    expect(backLink()).toHaveAccessibleName('Note');
    expect(backLink()).toHaveAttribute('href', paths.notePage);
  });

  // version-history.md item 9: sorted on the client, whatever order the API gives.
  it('lists the versions newest first in an <ol reversed>, and names the highest as the current version', async () => {
    serveHistory();
    await openHistory();

    expect(list().tagName).toBe('OL');
    expect(list()).toHaveAttribute('reversed');
    expect(rowLinks().map((link) => link.textContent)).toEqual(['Version 3', 'Version 2', 'Version 1']);
    const hrefs = rowLinks().map((link) => link.getAttribute('href'));
    expect(hrefs).toEqual([3, 2, 1].map((n) => `${paths.page}/${n}`));
    expect(within(main()).getByText('The current version is version 3.').tagName).toBe('P');
    expect(within(main()).queryByText('Not edited since submit')).toBeNull();
  });

  it('describes each row by who saved it and when, with Submitted on version 1 alone, no tag or chevron', async () => {
    serveHistory();
    await openHistory();

    const [third, second, first] = rowLinks();
    expect(third).toHaveAccessibleDescription(`Sam Lee (manager), ${at.v3}`);
    expect(second).toHaveAccessibleDescription(`Priya Nair, ${at.v2}`);
    expect(first).toHaveAccessibleDescription(`Submitted, Priya Nair, ${at.v1}`);
    expect(within(list()).getAllByText('Submitted')).toHaveLength(1);
    expect(within(list()).getByText('Submitted')).toHaveStyle({ fontWeight: '700' });
    expect(third.closest('li')!.querySelector('time')).toHaveAttribute('datetime', v3.createdAtUtc);
    expect(list().querySelector(`.${tagStyles.tag}`)).toBeNull();
    expect(list().querySelector('svg')).toBeNull();
    expect(within(list()).getAllByRole('link')).toHaveLength(3);
  });

  // version-history.md item 9: the rule is who saved it against who wrote the note, never anyone's role.
  it('adds "(manager)" to a saver exactly when they are not the note\'s author, whatever their role', async () => {
    const author: NoteAuthor = { id: manager.userId, displayName: manager.displayName };
    serveHistory(submittedNote(author), [
      summary(1, author, '2026-10-01T06:12:00Z'),
      summary(2, sam, '2026-10-01T06:40:00Z'),
      summary(3, author, '2026-10-01T07:03:00Z'),
    ]);
    await openHistory();

    const [third, second, first] = rowLinks();
    expect(third).toHaveAccessibleDescription(`Jordan Ellis, ${at.v3}`);
    expect(second).toHaveAccessibleDescription(`Sam Lee (manager), ${at.v2}`);
    expect(first).toHaveAccessibleDescription(`Submitted, Jordan Ellis, ${at.v1}`);
  });

  it('says "Not edited since submit" for one version, and still lists it', async () => {
    serveHistory(submittedNote(priya, 1), [v1]);
    await openHistory();

    expect(within(main()).getByText('The current version is version 1.')).toBeInTheDocument();
    expect(within(main()).getByText('Not edited since submit').tagName).toBe('P');
    expect(rowLinks().map((link) => link.textContent)).toEqual(['Version 1']);
  });

  it('shows the past-day line for a past-day note, and not otherwise', async () => {
    serveHistory(submittedNote(jo, 3, true));
    await openHistory();

    const pastDay = within(main()).getByText('Past-day note').closest('p')!;
    expect(pastDay).toHaveTextContent("Past-day note, written by Jo Smith (manager) on Thu 1 Oct 2026, 10:14 am");
    expect(within(pastDay).getByText('Past-day note')).toHaveClass(tagStyles.tag, tagStyles.neutral);
    expect(pastDay.querySelector('time')).toHaveAttribute('datetime', '2026-10-01T00:14:00Z');
  });

  it('shows no past-day line for a note written on its own day', async () => {
    serveHistory();
    await openHistory();

    expect(within(main()).queryByText('Past-day note')).toBeNull();
  });
});

describe('The version history for someone who may not see it', () => {
  it('shows a worker Page not found, and sends nothing about the participant or the note', async () => {
    serveHistory();
    await openHistory(worker);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(sentRequests().filter((path) => path.startsWith('/api/participants'))).toEqual([]);
  });

  it.each([404, 403])('shows Page not found for a %s from the version list', async (status) => {
    serveHistory();
    serve(paths.versions, problem(status));
    await openHistory();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(main()).not.toHaveTextContent(/Jane|Version history/);
  });
});

describe('The version history when it did not load', () => {
  it('shows the fallback <h1>, "did not load" and Try again, then "still", then the list', async () => {
    serveHistory();
    serve(paths.versions, noConnection);
    renderApp(paths.page);
    // After its one silent retry.
    await wait(1_000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Version history');
    expect(line().textContent).toBe('Version history did not load: no connection. Try again.');
    expect(within(main()).queryByRole('list')).toBeNull();

    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await wait(1_000);
    await settle();
    expect(line().textContent).toBe('Version history still did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toHaveFocus();

    serve(paths.versions, json(shuffled));
    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Version history Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(rowLinks()).toHaveLength(3);
  });
});

describe('Going to and from a version', () => {
  // version-history.md *Interactions and focus*: the row opened is where focus returns on a pop back.
  it('opens a version from its row, and returns to the list with that row focused on the back link', async () => {
    serveHistory();
    const { router } = await openHistory();

    await press(rowLink(2));
    expect(router.state.location.pathname).toBe(paths.versionPage(2));

    await press(backLink());
    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe(paths.page);
    expect(rowLink(2)).toHaveFocus();
  });

  it('returns to the list with that row focused on the browser Back too', async () => {
    serveHistory();
    const { router } = await openHistory();

    await press(rowLink(1));
    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe(paths.page);
    expect(rowLink(1)).toHaveFocus();
  });

  it('refetches the list, but not the note, when the window regains focus', async () => {
    serveHistory();
    await openHistory();
    fetchMock.mockClear();

    act(() => {
      document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
    });
    await settle();

    expect(sentRequests()).toEqual([paths.versions]);
  });
});
