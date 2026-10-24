import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
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
  firstNoteDate: null,
};

// Between a day and its month and between a time and am or pm, so that neither pair splits across lines.
const nbsp = '\u00A0';

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

/** Opens version `n` at its address, and lets it load. */
async function openVersion(n: number, me: Me = manager) {
  const opened = renderApp(paths.versionPage(n), me);
  await settle();
  return opened;
}

/** The `<p>` that holds `text`. */
function paragraphOf(text: string | RegExp): HTMLElement {
  return within(main()).getByText(text).closest('p')!;
}

/** The requests for version `n`, as their paths. */
function versionRequests(n: number): string[] {
  return sentRequests().filter((path) => path === paths.version(n));
}

/** The link of version `n`'s row on the list. */
function rowLink(n: number): HTMLElement {
  return within(main())
    .getAllByRole('link')
    .find((link) => link.dataset.returnKey === String(n))!;
}

describe('A version while it loads', () => {
  it('shows nothing for 999 ms, then "Loading version 2…", and nothing of the version', async () => {
    serveHistory();
    serve(paths.version(2), noAnswer);
    renderApp(paths.versionPage(2));

    expect(backLink()).toHaveAccessibleName('Version history');
    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading version 2…');
    expect(within(main()).queryByRole('heading')).toBeNull();
  });
});

describe('A version once loaded', () => {
  it('is titled "Grow2Notes – Version history", with "Version 2", the name and the date in its <h1>', async () => {
    serveHistory();
    await openVersion(2);

    expect(document.title).toBe('Grow2Notes – Version history');
    expect(pageHeading()).toHaveAccessibleName(`Version 2 Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(backLink()).toHaveAccessibleName('Version history');
    expect(backLink()).toHaveAttribute('href', paths.page);
  });

  it('says the current version is current, with no link', async () => {
    serveHistory();
    await openVersion(3);

    const current = paragraphOf('This is the current version.');
    expect(current).toHaveStyle({ fontWeight: '700' });
    expect(within(current).queryByRole('link')).toBeNull();
    expect(within(main()).queryByRole('link')).toBeNull();
  });

  it('says an earlier version is earlier, and links to the current one', async () => {
    serveHistory();
    await openVersion(2);

    const earlier = paragraphOf('version 3');
    expect(earlier).toHaveTextContent('This is an earlier version. The current version is version 3.');
    expect(earlier).toHaveStyle({ fontWeight: '700' });
    const link = within(earlier).getByRole('link', { name: 'version 3' });
    expect(link).toHaveAttribute('href', paths.versionPage(3));
  });

  it('follows the link to the current version, which then says it is current', async () => {
    serveHistory();
    const { router } = await openVersion(1);

    await press(within(main()).getByRole('link', { name: 'version 3' }));

    expect(router.state.location.pathname).toBe(paths.versionPage(3));
    expect(pageHeading()).toHaveAccessibleName(`Version 3 Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(within(main()).getByText('This is the current version.')).toBeInTheDocument();
  });

  // version-history.md *Conflicts resolved*: the list is fetched afresh, so a number as high as its highest is current.
  it('treats a number above the highest of the list as the current version, the list may lag', async () => {
    serveHistory();
    serve(paths.version(4), json(versionOf(summary(4, sam, '2026-10-01T07:30:00Z'))));
    await openVersion(4);

    expect(within(main()).getByText('This is the current version.')).toBeInTheDocument();
    expect(within(main()).queryByRole('link')).toBeNull();
  });

  it.each([
    { n: 1, row: v1, words: `Submitted by Priya Nair, ${at.v1}` },
    { n: 2, row: v2, words: `Saved by Priya Nair, ${at.v2}` },
    { n: 3, row: v3, words: `Saved by Sam Lee (manager), ${at.v3}` },
  ])('says who saved version $n and when', async ({ n, row, words }) => {
    serveHistory();
    await openVersion(n);

    const saved = paragraphOf(/^(?:Submitted|Saved) by/);
    expect(saved).toHaveTextContent(words.replaceAll(nbsp, ' '));
    expect(saved.querySelector('time')).toHaveAttribute('datetime', row.createdAtUtc);
  });

  it('shows the past-day line for a past-day note, after the saved-by line', async () => {
    serveHistory(submittedNote(jo, 3, true));
    await openVersion(2);

    const pastDay = paragraphOf('Past-day note');
    expect(pastDay).toHaveTextContent('Past-day note, written by Jo Smith (manager) on Thu 1 Oct 2026, 10:14 am');
    expect(paragraphOf(/^Saved by/).nextElementSibling).toBe(pastDay);
  });

  it('shows no past-day line for a note written on its own day', async () => {
    serveHistory();
    await openVersion(2);

    expect(within(main()).queryByText('Past-day note')).toBeNull();
  });

  // participant-notes.md component 4 and version-history.md B item 7: the version's own flag, and none of the note's.
  it("shows the Flagged line only for a flagged version, with that version's own reason", async () => {
    serveHistory({ ...submittedNote(), flagStatus: 'ToReview' });
    serve(
      paths.version(2),
      json({ ...versionOf(v2), isFlagged: true, flagReason: 'Jane seemed unwell.\nShe was pale.' }),
    );
    await openVersion(2);

    const flagged = paragraphOf('Flagged');
    expect(flagged).toHaveTextContent('Flagged, Reason: Jane seemed unwell. She was pale.');
    expect(flagged.lastElementChild!.textContent).toBe('Reason: Jane seemed unwell.\nShe was pale.');
    // The note's own lines describe the note, not a version, and live on the read view.
    expect(within(main()).queryByText(/^(?:Edited|To review|Reviewed)$/)).toBeNull();
    expect(within(main()).queryByText(/last change by/)).toBeNull();
  });

  it('shows no Flagged line for a version that is not flagged, whatever the note is', async () => {
    serveHistory({ ...submittedNote(), flagStatus: 'ToReview' });
    await openVersion(3);

    expect(within(main()).queryByText('Flagged')).toBeNull();
    expect(within(main()).queryByText('To review')).toBeNull();
  });

  it("shows only the groups that were picked in that version, and the version's own goals and wording", async () => {
    serveHistory();
    serve(
      paths.version(2),
      json({
        ...versionOf(v2),
        goals: [
          { text: 'Makes own breakfast', isTicked: true },
          { text: 'Catches bus to day program', isTicked: false },
        ],
        commonItemGroups: [
          { name: 'Every note', items: [{ text: 'Medication prompted', isTicked: true }] },
          { name: 'Personal care', items: [{ text: 'Showered', isTicked: false }] },
        ],
      }),
    );
    await openVersion(2);

    const groups = within(main()).getAllByRole('heading', { level: 3 });
    expect(groups.map((group) => group.textContent)).toEqual(['Every note', 'Personal care']);
    expect(within(main()).queryByText('Community outing')).toBeNull();
    expect(within(main()).getByText(/Catches bus to day program/)).toBeInTheDocument();
    expect(within(main()).getByText('Wording of version 2.')).toBeInTheDocument();
  });

  // version-history.md B item 10: the stored text, as a text node, never trimmed.
  it('shows the Guided notes exactly as stored, every space and line break kept', async () => {
    const stored = '  Jane was keen.\n\n   She caught the bus <b>today</b>.  ';
    serveHistory();
    serve(paths.version(2), json({ ...versionOf(v2), narrative: stored }));
    await openVersion(2);

    const heading = within(main()).getByRole('heading', { level: 2, name: '3. Guided notes' });
    const written = heading.nextElementSibling!;
    expect(written.textContent).toBe(stored);
    expect(written.children).toHaveLength(0);
    expect(written).toHaveStyle({ whiteSpace: 'pre-wrap' });
  });

  it('has no control but the back link and the link to the current version', async () => {
    serveHistory({ ...submittedNote(), flagStatus: 'ToReview' });
    await openVersion(1);

    expect(within(main()).queryByRole('button')).toBeNull();
    expect(within(main()).getAllByRole('link').map((link) => link.textContent)).toEqual(['version 3']);
    for (const absent of [/Edit/, /Restore/, /Mark reviewed/, /Previous/, /Next/, /Past notes/]) {
      expect(screen.queryByText(absent)).toBeNull();
    }
  });
});

describe('A version at an address that is not one', () => {
  it.each(['0', '-1', '1.5', '02', 'abc'])('shows Page not found at /versions/%s, sending nothing', async (segment) => {
    serveHistory();
    renderApp(`${paths.page}/${segment}`);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(sentRequests().filter((path) => path.startsWith('/api/participants'))).toEqual([]);
  });

  it('shows a worker Page not found, and sends nothing about the participant or the note', async () => {
    serveHistory();
    await openVersion(2, worker);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(sentRequests().filter((path) => path.startsWith('/api/participants'))).toEqual([]);
  });

  it.each([404, 403])('shows Page not found for a %s from the version', async (status) => {
    serveHistory();
    serve(paths.version(9), problem(status));
    await openVersion(9);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(main()).not.toHaveTextContent(/Jane|Version 9/);
  });
});

describe('A version when it did not load', () => {
  it('says "Version 2 did not load: …", and Try again sends the version again, then shows it', async () => {
    serveHistory();
    serve(paths.version(2), noConnection);
    renderApp(paths.versionPage(2));
    // After its one silent retry.
    await wait(1_000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Version history');
    expect(line().textContent).toBe('Version 2 did not load: no connection. Try again.');
    const failed = versionRequests(2).length;

    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await wait(1_000);
    await settle();
    expect(line().textContent).toBe('Version 2 still did not load: no connection. Try again.');
    expect(versionRequests(2).length).toBeGreaterThan(failed);

    serve(paths.version(2), json(versionOf(v2)));
    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Version 2 Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(within(main()).queryByRole('button')).toBeNull();
  });
});

describe('Opening a version again', () => {
  // A version is insert-only, so what was fetched stays fetched while the app is open.
  it('sends no second request for a version that has loaded, on coming back to it', async () => {
    serveHistory();
    const { router } = await openVersion(2);
    const first = versionRequests(2).length;

    await press(backLink());
    expect(router.state.location.pathname).toBe(paths.page);
    await press(rowLink(2));

    expect(router.state.location.pathname).toBe(paths.versionPage(2));
    expect(pageHeading()).toHaveAccessibleName(`Version 2 Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(versionRequests(2)).toHaveLength(first);
  });
});
