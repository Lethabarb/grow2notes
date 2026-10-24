import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes } from '../../routes/routes.tsx';
import type { Draft, ParticipantHeader } from '../notes/noteQueries.ts';

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';

// participants.md's made-up people (A35): Sam Lee, a manager, and Alex Pham, a worker, of one organisation; today in
// Melbourne is Thursday 1 October 2026.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const worker: Me = {
  ...manager,
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
};

const jane: ParticipantHeader = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  status: 'Active',
  firstNoteDate: null,
};

const paths = {
  header: `/api/participants/${jane.id}`,
  page: `/manage/participants/${jane.id}/past-day-note`,
  note: (noteDate: string) => `/api/participants/${jane.id}/notes/${noteDate}`,
  draft: (noteDate: string) => `/api/participants/${jane.id}/notes/${noteDate}/draft`,
  notePage: (noteDate: string) => `/participants/${jane.id}/notes/${noteDate}`,
  pastNotes: `/api/participants/${jane.id}/notes`,
  participants: '/api/admin/participants?includeArchived=true',
  goals: `/api/admin/participants/${jane.id}/goals?includeArchived=true`,
};

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

/** A new note's form, as `GET {base}/draft` gives it. */
const template: Draft = {
  baseVersion: 0,
  isSaved: false,
  listsVersion: 'lists-1',
  guidePrompts: '',
  goals: [],
  commonItemGroups: [],
  narrative: '',
  isFlagged: false,
  flagReason: null,
};

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each request, by its method and path. */
const answers = new Map<string, Answer>();

/** Answers each `GET` of `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(`GET ${path}`, answer);
}

/** Every request sent, as its method and path. */
function sentRequests(): string[] {
  return fetchMock.mock.calls.map(([path, init]) => `${init?.method ?? 'GET'} ${path as string}`);
}

/**
 * The requests about participants, once each. The shell also refreshes `['me']` on mount, which this page's tests are
 * not about, so it is left out.
 */
function participantRequests(): string[] {
  return [...new Set(sentRequests().filter((request) => request.includes('/api/participants/')))];
}

/** Answers Jane's header with `participant`, and, for the note at `noteDate`, no note and a new note's form. */
function serveJane(participant: ParticipantHeader = jane, noteDate = '2026-09-28') {
  serve(paths.header, json(participant));
  serve(paths.note(noteDate), problem(404));
  serve(paths.draft(noteDate), json(template));
}

/**
 * Renders the app at `path` for `me` on a memory router, under StrictMode, which runs each effect twice, as `main.tsx`
 * does, and a query client of its own, in which `['me']` holds `me` already, as after the start-up.
 */
function renderApp(path: string, me: Me = manager): Router {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // With a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a position from one test into the next.
  const router = createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return router;
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

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

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The back link in the top bar. */
function backLink(): HTMLElement {
  return within(document.getElementById('page-bar')!).getByRole('link');
}

/** The Note date field. */
function dateField(): HTMLInputElement {
  return within(main()).getByLabelText<HTMLInputElement>('Note date');
}

function continueButton(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Continue' });
}

/** Enters `value` in the Note date field, as a picked or typed date. */
function enter(value: string) {
  fireEvent.change(dateField(), { target: { value } });
}

/** The message above the Note date field, if it has one. */
function message(): HTMLElement | null {
  return document.getElementById('past-day-date-error');
}

async function openPage(me: Me = manager, participant: ParticipantHeader = jane) {
  serveJane(participant);
  const router = renderApp(paths.page, me);
  await settle();
  return router;
}

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
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('Write past-day note', () => {
  it('names the participant and the page in its <h1>, and titles the page', async () => {
    await openPage();

    expect(pageHeading()).toHaveAccessibleName('Jane Citizen, Write past-day note');
    expect(pageHeading().querySelector('[translate="no"]')).toHaveTextContent('Jane Citizen');
    expect(document.title).toBe('Grow2Notes – Write past-day note');
  });

  it('has one date field, empty, with the day before today as its latest date and no earliest', async () => {
    await openPage();

    const field = dateField();
    expect(field).toHaveAttribute('type', 'date');
    expect(field).toHaveValue('');
    expect(field).toHaveAttribute('max', '2026-09-30');
    expect(field).not.toHaveAttribute('min');
    expect(field).toHaveAttribute('autocomplete', 'off');
    expect(field).toHaveAccessibleDescription('A date before today');
    expect(field.closest('form')).toHaveAttribute('novalidate');
    expect(continueButton()).toHaveAttribute('type', 'submit');
  });

  it('sends only the participant’s header while the date is chosen', async () => {
    await openPage();
    enter('2026-09-28');

    expect(participantRequests()).toEqual([`GET ${paths.header}`]);
  });

  it.each([
    { what: 'an empty date', value: '', words: 'Enter the note date' },
    { what: 'today', value: '2026-10-01', words: 'Note date must be in the past' },
    { what: 'a future date', value: '2026-10-02', words: 'Note date must be in the past' },
    { what: 'a year of five digits', value: '20260-01-01', words: 'Note date must be in the past' },
  ])('says "$words" for $what, above the field, which it focuses, and sends nothing', async ({ value, words }) => {
    await openPage();
    const before = sentRequests();
    enter(value);

    await press(continueButton());

    expect(message()).toHaveTextContent(words);
    expect(message()!.compareDocumentPosition(dateField()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(dateField()).toHaveAttribute('aria-invalid', 'true');
    expect(dateField()).toHaveFocus();
    expect(dateField()).toHaveAccessibleDescription(`Error: ${words} A date before today`);
    expect(within(main()).queryByRole('alert')).toBeNull();
    expect(sentRequests()).toEqual(before);
  });

  it('checks the date again on each change while the message shows, and clears it once the date is valid', async () => {
    await openPage();
    await press(continueButton());
    expect(message()).toHaveTextContent('Enter the note date');

    enter('2026-10-01');
    expect(message()).toHaveTextContent('Note date must be in the past');

    enter('2026-09-30');
    expect(message()).toBeNull();
    expect(dateField()).not.toHaveAttribute('aria-invalid');
    expect(dateField()).toHaveAccessibleDescription('A date before today');
  });

  it('says nothing about a date that has not been tried, as it is typed', async () => {
    await openPage();

    enter('2026-10-05');

    expect(message()).toBeNull();
  });

  it('replaces its own entry with the note’s address for a valid date, sending and creating nothing', async () => {
    const router = await openPage();
    enter('2026-09-28');

    await press(continueButton());

    expect(router.state.location.pathname).toBe(paths.notePage('2026-09-28'));
    expect(router.state.historyAction).toBe('REPLACE');
    expect(sentRequests().filter((request) => !request.startsWith('GET '))).toEqual([]);
  });

  it.each(['2026-09-30', '1999-12-31'])('accepts %s, the day before today or long before it', async (value) => {
    serve(paths.note(value), problem(404));
    serve(paths.draft(value), json(template));
    const router = await openPage();
    enter(value);

    await press(continueButton());

    expect(router.state.location.pathname).toBe(paths.notePage(value));
  });

  it('gives an archived participant the sentence alone, with no field and no Continue', async () => {
    await openPage(manager, { ...jane, status: 'Archived' });

    expect(pageHeading()).toHaveAccessibleName('Jane Citizen, Write past-day note');
    expect(
      within(main()).getByText('Jane Citizen is archived, so no new notes can be written for them.'),
    ).toBeVisible();
    expect(within(main()).queryByLabelText('Note date')).toBeNull();
    expect(within(main()).queryByRole('button')).toBeNull();
    expect(participantRequests()).toEqual([`GET ${paths.header}`]);
  });

  it('shows Page not found for a participant not in the organisation', async () => {
    serve(paths.header, problem(404));
    renderApp(paths.page);
    await settle();

    expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
  });

  it('gives a worker Page not found, and sends nothing about the participant', async () => {
    serveJane();
    renderApp(paths.page, worker);
    await settle();

    expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
    expect(participantRequests()).toEqual([]);
  });
});

describe('The back link of Write past-day note', () => {
  it('reads Participant, to the participant’s page, when nothing in the app opened it', async () => {
    await openPage();

    expect(backLink()).toHaveAccessibleName('Participant');
    expect(backLink()).toHaveAttribute('href', `/manage/participants/${jane.id}`);
  });

  it('reads Participant, and pops, when participant detail opened it', async () => {
    serve(paths.participants, json([{ ...jane, dateOfBirth: '1987-03-07', rowVersion: 'AAAAAAAAB9E=' }]));
    serve(paths.goals, json([]));
    serveJane();
    const router = renderApp(`/manage/participants/${jane.id}`);
    await settle();
    await press(within(main()).getByRole('link', { name: 'Write past-day note' }));

    expect(router.state.location.pathname).toBe(paths.page);
    expect(backLink()).toHaveAccessibleName('Participant');
    await press(backLink());
    expect(router.state.location.pathname).toBe(`/manage/participants/${jane.id}`);
    expect(router.state.historyAction).toBe('POP');
  });

  it('reads Past notes, and pops, when Past notes opened it', async () => {
    serve(paths.pastNotes, json({ notes: [], hasMore: false }));
    serveJane();
    const router = renderApp(`/participants/${jane.id}/notes`);
    await settle();
    await press(within(main()).getByRole('link', { name: 'Write past-day note' }));

    expect(router.state.location.pathname).toBe(paths.page);
    expect(backLink()).toHaveAccessibleName('Past notes');
    expect(backLink()).toHaveAttribute('href', `/participants/${jane.id}/notes`);
    await press(backLink());
    expect(router.state.location.pathname).toBe(`/participants/${jane.id}/notes`);
    expect(router.state.historyAction).toBe('POP');
  });
});
