import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes } from '../../routes/routes.tsx';
import type { TodayList, TodayParticipant } from '../today/todayQuery.ts';
import type { Draft, DraftGoal, ParticipantHeader } from './noteQueries.ts';

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';

// note-form.md's made-up people (A35): Casey Nguyen, a worker, signed in, and the participants. Today in Melbourne is
// Thursday 1 October 2026.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Casey Nguyen',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const jane: ParticipantHeader = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  status: 'Active',
};
const priya: ParticipantHeader = {
  id: '6b2e8d4a-0f37-4c95-a1d6-3e9f7b5c2a80',
  givenName: 'Priya',
  familyName: 'Kumar',
  status: 'Active',
};
const zara: ParticipantHeader = {
  id: '1f7c3a9e-5d21-4b48-8e6f-0a2c4d7b9e13',
  givenName: 'Zara',
  familyName: 'Ali',
  status: 'Active',
};

/** A goal of Jane's, unticked. */
function goal(id: string, text: string): DraftGoal {
  return { id, text, isTicked: false };
}

const breakfast = goal('0b2f6c1e-4d38-4a95-9e07-6c1d8f3a5b24', 'Makes own breakfast');
const bus = goal('5e9a3d7c-1b46-4f82-a0d3-8c2e6b4f1a97', 'Catches bus to day program');
const sister = goal('8a1c6e3f-7d52-4b09-b3e4-1f9d2c7a5e68', 'Phones her sister');

const prompts = 'Mood and wellbeing today?\nWhat did you do together?\nAnything to follow up?';

/** A new note's template, as `GET {base}/draft` gives it, with `goals`, unticked. */
function template(goals: readonly DraftGoal[] = [breakfast, bus, sister], guidePrompts = prompts): Draft {
  return { baseVersion: 0, isSaved: false, listsVersion: 'lists-1', guidePrompts, goals: [...goals], narrative: '' };
}

/** The worker's own draft, saved at 9:42 am, with Breakfast and Sister ticked. */
function savedDraft(narrative = 'Jane made toast.\nThen a walk to the shops.'): Draft {
  return {
    baseVersion: 0,
    isSaved: true,
    guidePrompts: prompts,
    goals: [{ ...breakfast, isTicked: true }, bus, { ...sister, isTicked: true }],
    narrative,
    savedAtUtc: '2026-09-30T23:42:00Z',
  };
}

const nbsp = ' ';

/** The paths of `participant`'s header and of their note's form for `noteDate`. */
function paths(participant: ParticipantHeader, noteDate = '2026-10-01') {
  return {
    header: `/api/participants/${participant.id}`,
    draft: `/api/participants/${participant.id}/notes/${noteDate}/draft`,
    page: `/participants/${participant.id}/notes/${noteDate}`,
  };
}

/** A participant on the Today list. */
function onToday({ id, givenName, familyName }: ParticipantHeader): TodayParticipant {
  return { id, givenName, familyName };
}

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with what it includes. */
function problem(status: number, includes: Record<string, unknown> = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...includes });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** Someone else's draft, which only its author may open (design.md §6.9). */
function startedBy(startedAtUtc: string): Answer {
  return problem(409, { code: 'note.in_progress_by_other', authorDisplayName: 'Alex P.', startedAtUtc });
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

/** How the stubbed server answers each request, by its method and path: a new answer for each. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each `GET` for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(`GET ${path}`, answer);
}

/** Answers `participant`'s header and their note's form for `noteDate`. */
function serveNote(participant: ParticipantHeader, draft: Answer = json(template()), noteDate = '2026-10-01') {
  const { header, draft: draftPath } = paths(participant, noteDate);
  serve(header, json(participant));
  serve(draftPath, draft);
}

/** Every request sent, as its method and path. */
function sentRequests(): string[] {
  return fetchMock.mock.calls.map(([path, init]) => `${init?.method ?? 'GET'} ${path as string}`);
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

/**
 * Renders the app at `path` for the worker on a memory router, under a query client of its own. `['me']` holds the
 * worker already, as after the start-up, so that the screen shows with the first render, from which the fake clock
 * then times its region.
 */
function renderApp(path: string, me: Me = worker): { router: Router; client: QueryClient } {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // With a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a row or a position from one test into the next.
  const router = createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
  show(router, client);
  return { router, client };
}

/** Opens Today with `participants` on it, then Jane's row, as a person does, and lets her note load. */
async function openFromToday(participants: readonly ParticipantHeader[] = [zara, jane, priya]) {
  serve(todayPath, json({ date: '2026-10-01', participants: participants.map(onToday) } satisfies TodayList));
  const opened = renderApp('/');
  await settle();
  await press(rowLink('Jane Citizen'));
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

/** The shell's before-main bar slot, which holds the top bar. */
function pageBar(): HTMLElement {
  const slot = document.getElementById('page-bar');
  if (slot === null) {
    throw new Error('The shell rendered no #page-bar.');
  }

  return slot;
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The region's line in `<main>`, beside the page's visually hidden status region and the lists-changed one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => region.tagName === 'P' && !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The Guided notes box. */
function guidedNotes(): HTMLTextAreaElement {
  return within(main()).getByRole('textbox', { name: '3. Guided notes' });
}

/** Today's row link named `name`. */
function rowLink(name: string): HTMLElement {
  return within(within(main()).getByRole('list')).getByRole('link', { name });
}

/** Whether the screen shows anything of a note's content: its `<h1>`, a tick box or a text box. */
function showsAnyOfTheNote(): boolean {
  return (
    within(main()).queryByRole('heading', { level: 1 }) !== null
    || within(main()).queryByRole('checkbox') !== null
    || within(main()).queryByRole('textbox') !== null
  );
}

/** Whether `later` comes after `earlier` in the document. */
function follows(later: Node, earlier: Node): boolean {
  return (earlier.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
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
  scrollPage(0);
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('The note form while it loads', () => {
  it('shows the back link to Today and the empty save status in the top bar at once, and nothing of the note', () => {
    serve(paths(jane).header, noAnswer);
    serve(paths(jane).draft, noAnswer);
    renderApp(paths(jane).page);

    const back = within(pageBar()).getByRole('link', { name: 'Today' });
    expect(back).toHaveAttribute('href', '/');
    const status = pageBar().querySelector('#save-status');
    expect(status?.tagName).toBe('P');
    expect(status).toHaveAttribute('role', 'status');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status).toHaveAttribute('aria-atomic', 'true');
    expect(status).toBeEmptyDOMElement();
    expect(follows(status!, back)).toBe(true);
    expect(showsAnyOfTheNote()).toBe(false);
    expect(document.title).toBe('Grow2Notes – Note');
  });

  it.each(['header', 'draft'] as const)(
    'shows no identity, tick box or text box while only the %s has answered the other',
    async (waiting) => {
      const answered = waiting === 'header' ? 'draft' : 'header';
      const late = held(waiting === 'header' ? json(jane) : json(template()));
      serve(paths(jane)[waiting], late.answer);
      serve(paths(jane)[answered], answered === 'header' ? json(jane) : json(template()));
      renderApp(paths(jane).page);

      await settle();
      expect(showsAnyOfTheNote()).toBe(false);

      late.release();
      await settle();
      expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    },
  );

  it('says nothing for 999 ms, then "Loading note…" at 1,000 ms, in a status line there from the start', async () => {
    serve(paths(jane).header, noAnswer);
    serve(paths(jane).draft, noAnswer);
    renderApp(paths(jane).page);

    expect(line()).toBeEmptyDOMElement();
    await wait(999);
    expect(line()).toBeEmptyDOMElement();

    await wait(1);
    expect(line()).toHaveTextContent(/^Loading note…$/);
    expect(showsAnyOfTheNote()).toBe(false);
  });
});

describe('The note form once loaded', () => {
  it("shows the participant's name, as stored and never translated, then the date, in one <h1>", async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    const heading = pageHeading();
    expect(heading).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(heading).toHaveAttribute('tabindex', '-1');
    const name = within(heading).getByText('Jane Citizen');
    expect(name).toHaveAttribute('translate', 'no');
    const date = heading.querySelector('time');
    expect(date).toHaveAttribute('datetime', '2026-10-01');
    expect(date?.textContent).toBe(`Thursday 1${nbsp}October 2026`);
    expect(follows(date!, name)).toBe(true);
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Note');
  });

  it('wraps a long name at any width, never upper-casing it or cutting it short', async () => {
    const long = {
      ...jane,
      givenName: 'Maximiliana Alexandrina Konstantina Josefina',
      familyName: 'Featherstonehaugh-Cholmondeley-Marjoribanks-Ffoulkes-Ng',
    };
    serveNote(long);
    renderApp(paths(long).page);
    await settle();

    const name = within(pageHeading()).getByText(`${long.givenName} ${long.familyName}`);
    expect(name.textContent).toHaveLength(100);
    expect(name).toHaveStyle({ overflowWrap: 'break-word' });
    for (const cut of [{ textTransform: 'uppercase' }, { textOverflow: 'ellipsis' }, { whiteSpace: 'nowrap' }]) {
      expect(name).not.toHaveStyle(cut);
      expect(pageHeading()).not.toHaveStyle(cut);
    }
  });

  it('shows Goals, with its hint and an unticked row for each goal in the order given, then Guided notes', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    const goals = within(main()).getByRole('group', { name: '1. Goals' });
    expect(goals).toHaveAccessibleDescription('Ticked means reached.');
    const boxes = within(goals).getAllByRole('checkbox');
    expect(boxes.map((box) => box.closest('label')?.textContent)).toEqual([breakfast.text, bus.text, sister.text]);
    expect(boxes.map((box) => (box as HTMLInputElement).checked)).toEqual([false, false, false]);
    expect(guidedNotes()).toHaveValue('');
    const listsChanged = goals.previousElementSibling;
    expect(listsChanged).toHaveAttribute('role', 'status');
    expect(listsChanged).toBeEmptyDOMElement();
    const guidedNotesHeading = within(main()).getByRole('heading', { name: '3. Guided notes' });
    const inOrder = [pageHeading(), listsChanged!, goals, guidedNotesHeading];
    for (const [index, element] of inOrder.slice(1).entries()) {
      expect(follows(element, inOrder[index])).toBe(true);
    }
  });

  it("says that Jane has no goals in place of the tick boxes, with no fieldset, and keeps Guided notes", async () => {
    serveNote(jane, json(template([])));
    renderApp(paths(jane).page);
    await settle();

    expect(within(main()).getByRole('heading', { level: 2, name: '1. Goals' })).toBeInTheDocument();
    expect(within(main()).getByText('No goals set up for Jane yet. A manager can add them.')).toBeInTheDocument();
    expect(main().querySelector('fieldset')).toBeNull();
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(guidedNotes()).toBeInTheDocument();
  });

  it('shows a draft with its own goals, ticks and text, the prompts not shown over the text', async () => {
    serveNote(jane, json(savedDraft()));
    renderApp(paths(jane).page);
    await settle();

    const boxes = within(main()).getAllByRole('checkbox');
    expect(boxes.map((box) => (box as HTMLInputElement).checked)).toEqual([true, false, true]);
    expect(guidedNotes()).toHaveValue('Jane made toast.\nThen a walk to the shops.');
  });

  // As the shell fetches every query again after signing in again in place (app-shell.md *Sign in again, same person*).
  it('keeps its ticks and text, and the form, when the note is fetched again, whatever comes', async () => {
    serveNote(jane);
    const { client } = renderApp(paths(jane).page);
    await settle();
    fireEvent.click(within(main()).getByText(bus.text));
    fireEvent.input(guidedNotes(), { target: { value: 'Jane made toast.' } });
    serve(paths(jane).draft, startedBy('2026-09-30T23:14:00Z'));

    await act(() => client.invalidateQueries());
    await settle();

    expect(within(main()).getByRole('checkbox', { name: bus.text })).toBeChecked();
    expect(guidedNotes()).toHaveValue('Jane made toast.');
    expect(within(main()).queryByText(/started/)).toBeNull();
  });

  it('focuses the <h1> on arriving from Today', async () => {
    serveNote(jane);
    const { router } = await openFromToday();

    expect(router.state.location.pathname).toBe(paths(jane).page);
    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
  });
});

describe('The note form when it did not load', () => {
  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])('shows the <h1> "Note", "The note did not load: $cause. Try again." and Try again', async ({ cause, answer }) => {
    serve(paths(jane).header, json(jane));
    serve(paths(jane).draft, answer);
    renderApp(paths(jane).page);

    // After its one silent retry.
    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Note');
    expect(line().textContent).toBe(`The note did not load: ${cause}. Try again.`);
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(within(main()).queryByRole('textbox')).toBeNull();
    expect(within(pageBar()).getByRole('link', { name: 'Today' })).toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Note');
  });

  it('shows the note after a Try again that loads it, with focus on its <h1>', async () => {
    serve(paths(jane).header, noConnection);
    serve(paths(jane).draft, json(template()));
    renderApp(paths(jane).page);
    await wait(1000);
    await settle();
    serve(paths(jane).header, json(jane));

    const tryAgain = within(main()).getByRole('button', { name: 'Try again' });
    tryAgain.focus();
    fireEvent.click(tryAgain);
    await settle();

    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(guidedNotes()).toBeInTheDocument();
  });

  // Another organisation's participant, one that does not exist, or a date after today (design.md §6.3).
  it.each(['header', 'draft'] as const)('shows Page not found in place of the screen for a 404 of the %s', async (
    which,
  ) => {
    serveNote(jane);
    serve(paths(jane)[which], problem(404));
    renderApp(paths(jane).page);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(pageBar()).toBeEmptyDOMElement();
  });
});

describe("Someone else's draft", () => {
  it("says who started today's note and when, focusing the <h1>, with no form", async () => {
    // 9:14 am in Melbourne on 1 October.
    serveNote(jane, startedBy('2026-09-30T23:14:00Z'));
    await openFromToday();

    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    const sentence = within(main()).getByText(/started/);
    expect(sentence.textContent).toBe(
      `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
    expect(follows(sentence, pageHeading())).toBe(true);
    expect(sentence.closest('[role]')).toBeNull();
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(within(main()).queryByRole('textbox')).toBeNull();
    expect(document.title).toBe('Grow2Notes – Note');
  });

  it('says who started the note for an earlier day, and when, focusing the <h1>, with no form', async () => {
    // 9:14 am in Melbourne on 30 September.
    serveNote(jane, startedBy('2026-09-29T23:14:00Z'), '2026-09-30');
    serve(todayPath, json({ date: '2026-10-01', participants: [onToday(jane)] } satisfies TodayList));
    const { router } = renderApp('/');
    await settle();

    await act(() => router.navigate(paths(jane, '2026-09-30').page));
    await settle();

    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Wednesday 30${nbsp}September 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(within(main()).getByText(/started/).textContent).toBe(
      `Alex P. started the note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(within(main()).queryByRole('textbox')).toBeNull();
  });
});

describe('Ticking a goal', () => {
  it('ticks and unticks a goal when its words are clicked, sending nothing', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    const sent = sentRequests().length;
    const box = within(main()).getByRole('checkbox', { name: bus.text });

    fireEvent.click(within(main()).getByText(bus.text));
    expect(box).toBeChecked();
    fireEvent.click(within(main()).getByText(bus.text));
    expect(box).not.toBeChecked();
    await settle();

    expect(sentRequests()).toHaveLength(sent);
  });
});

describe('The Guided notes box', () => {
  it('is labelled by its <h2>, required to screen readers, with nothing that cuts the text short', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    const box = guidedNotes();
    expect(within(main()).getByRole('heading', { level: 2, name: '3. Guided notes' })).toContainElement(
      box.labels[0],
    );
    expect(box).toHaveAttribute('id', 'guided-notes');
    expect(box).toHaveAttribute('aria-required', 'true');
    expect(box).toHaveAttribute('spellcheck', 'true');
    expect(box).toHaveAttribute('autocorrect', 'on');
    expect(box).toHaveAttribute('autocapitalize', 'sentences');
    expect(box).toHaveAttribute('autocomplete', 'off');
    expect(box).toHaveAttribute('writingsuggestions', 'false');
    for (const attribute of ['required', 'maxlength', 'rows', 'inputmode', 'enterkeyhint', 'wrap']) {
      expect(box).not.toHaveAttribute(attribute);
    }
  });

  it('shows the guide prompts as its placeholder, line breaks and all, and a copy that holds its height', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    expect(guidedNotes()).toHaveAttribute('placeholder', prompts);
    expect(guidedNotes().parentElement).toHaveAttribute('data-prompts', prompts);
  });

  it("shows the prompts as they are now in a draft's empty box", async () => {
    serveNote(jane, json({ ...savedDraft(''), guidePrompts: 'What did Jane enjoy today?' }));
    renderApp(paths(jane).page);
    await settle();

    expect(guidedNotes()).toHaveValue('');
    expect(guidedNotes()).toHaveAttribute('placeholder', 'What did Jane enjoy today?');
  });

  it('has no placeholder when no prompts are saved', async () => {
    serveNote(jane, json(template([breakfast], '')));
    renderApp(paths(jane).page);
    await settle();

    expect(guidedNotes()).not.toHaveAttribute('placeholder');
  });

  it('shows no count at 17,999 characters, and "You have 2,000 characters remaining" from 18,000', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    fireEvent.input(guidedNotes(), { target: { value: 'x'.repeat(17_999) } });
    expect(within(main()).queryByText(/characters? (remaining|too many)/)).toBeNull();

    fireEvent.input(guidedNotes(), { target: { value: 'x'.repeat(18_000) } });
    const count = within(main()).getByText('You have 2,000 characters remaining');
    expect(count).toHaveAttribute('aria-hidden', 'true');
    expect(guidedNotes()).toHaveValue('x'.repeat(18_000));
  });
});

describe('Opening the note form', () => {
  it("sends its two reads alone, beside the shell's /me, and nothing on scrolling, focusing or a tap", async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    const opened = new Set(sentRequests());
    expect(opened).toEqual(new Set([`GET ${mePath}`, `GET ${paths(jane).header}`, `GET ${paths(jane).draft}`]));
    const sent = sentRequests().length;

    scrollPage(400);
    guidedNotes().focus();
    fireEvent.click(guidedNotes());
    guidedNotes().blur();
    await wait(10_000);

    expect(sentRequests()).toHaveLength(sent);
  });
});

describe('The note form, keyed by participant and date', () => {
  it("never shows one participant's name while another's note loads", async () => {
    serveNote(jane);
    const { router } = renderApp(paths(jane).page);
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    const priyasDraft = held(json(template()));
    serveNote(priya, priyasDraft.answer);

    await act(() => router.navigate(paths(priya).page));
    await settle();

    expect(document.body).not.toHaveTextContent('Jane Citizen');
    expect(showsAnyOfTheNote()).toBe(false);
    priyasDraft.release();
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Priya Kumar Thursday 1${nbsp}October 2026`);
  });
});

describe('Back to Today', () => {
  it("pops back to Today on the back link, focusing the row opened, where the list was", async () => {
    serveNote(jane);
    serve(todayPath, json({ date: '2026-10-01', participants: [zara, jane, priya].map(onToday) }));
    const { router } = renderApp('/');
    await settle();
    scrollPage(600);
    await press(rowLink('Jane Citizen'));
    await settle();
    const entries = router.state.historyAction;
    expect(entries).toBe('PUSH');
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    await press(within(pageBar()).getByRole('link', { name: 'Today' }));

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('POP');
    expect(rowLink('Jane Citizen')).toHaveFocus();
    expect(focus).toHaveBeenLastCalledWith({ preventScroll: true });
    expect(window.scrollY).toBe(600);
  });

  it('focuses the row opened, where the list was, on browser Back, and keeps it as the list comes again', async () => {
    serveNote(jane);
    serve(todayPath, json({ date: '2026-10-01', participants: [zara, jane, priya].map(onToday) }));
    const { router } = renderApp('/');
    await settle();
    scrollPage(600);
    await press(rowLink('Jane Citizen'));
    await settle();
    const refetch = held(json({ date: '2026-10-01', participants: [zara, jane, priya].map(onToday) }));
    serve(todayPath, refetch.answer);

    await goBack(router);
    expect(rowLink('Jane Citizen')).toHaveFocus();
    expect(window.scrollY).toBe(600);

    refetch.release();
    await settle();
    expect(rowLink('Jane Citizen')).toHaveFocus();
    expect(window.scrollY).toBe(600);
  });

  it("focuses Today's <h1> when the row opened has gone from the list", async () => {
    serveNote(jane);
    const { router, client } = await openFromToday();
    // The list as it now comes, with Jane archived meanwhile, and none kept from before.
    client.removeQueries({ queryKey: ['today'] });
    serve(todayPath, json({ date: '2026-10-01', participants: [zara, priya].map(onToday) }));

    await goBack(router);
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(within(main()).queryByRole('link', { name: 'Jane Citizen' })).toBeNull();
    expect(pageHeading()).toHaveFocus();
  });
});
