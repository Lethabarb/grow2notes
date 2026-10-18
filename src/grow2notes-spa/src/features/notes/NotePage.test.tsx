import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import tickList from '../../components/TickList.module.css';
import { routes } from '../../routes/routes.tsx';
import type { TodayList, TodayParticipant } from '../today/todayQuery.ts';
import type { Draft, DraftGoal, ParticipantHeader } from './noteQueries.ts';

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const antiforgeryPath = '/api/auth/antiforgery';

// note-form.md's made-up people (A35): Casey Nguyen, a worker, signed in, and the participants. Today in Melbourne is
// Thursday 1 October 2026.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Casey Nguyen',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// Jordan Ellis, a manager of the same organisation, who may start a note for an earlier day (design.md §5.5 item 4).
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
const garden = goal('2d7b9f4a-6c13-4e58-9a21-5f8e3c0b7d46', 'Waters the garden');

const prompts = 'Mood and wellbeing today?\nWhat did you do together?\nAnything to follow up?';

/** A new note's template, as `GET {base}/draft` gives it, with `goals`, unticked, and no flag. */
function template(goals: readonly DraftGoal[] = [breakfast, bus, sister], guidePrompts = prompts): Draft {
  return {
    baseVersion: 0,
    isSaved: false,
    listsVersion: 'lists-1',
    guidePrompts,
    goals: [...goals],
    narrative: '',
    isFlagged: false,
    flagReason: null,
  };
}

/** The worker's own draft, saved at 9:42 am, with Breakfast and Sister ticked, unflagged. */
function savedDraft(narrative = 'Jane made toast.\nThen a walk to the shops.'): Draft {
  return {
    baseVersion: 0,
    isSaved: true,
    guidePrompts: prompts,
    goals: [{ ...breakfast, isTicked: true }, bus, { ...sister, isTicked: true }],
    narrative,
    isFlagged: false,
    flagReason: null,
    savedAtUtc: '2026-09-30T23:42:00Z',
  };
}

const nbsp = ' ';

/** The paths of `participant`'s header, of their note's form and versions for `noteDate`, and of its page. */
function paths(participant: ParticipantHeader, noteDate = '2026-10-01') {
  return {
    header: `/api/participants/${participant.id}`,
    draft: `/api/participants/${participant.id}/notes/${noteDate}/draft`,
    versions: `/api/participants/${participant.id}/notes/${noteDate}/versions`,
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

/** Answers each save of the note's draft at `path`, a `PUT`, with `answer` from now on. */
function serveSave(path: string, answer: Answer) {
  answers.set(`PUT ${path}`, answer);
}

/** A save's answer: when it was saved. */
function saved(savedAtUtc: string): Answer {
  return json({ savedAtUtc });
}

/** Each request answered with the next of `answers`, and the last one's answer again once they have all been given. */
function inTurn(...answers: Answer[]): Answer {
  let next = 0;
  return (init) => answers[Math.min(next++, answers.length - 1)](init);
}

/** A save as it was sent: its path, and its body as JSON. */
type SentSave = { path: string; body: Record<string, unknown> };

/** Every save sent, in order. */
function saves(): SentSave[] {
  return fetchMock.mock.calls
    .filter(([, init]) => init?.method === 'PUT')
    .map(([path, init]) => ({
      path: path as string,
      body: JSON.parse(init!.body as string) as Record<string, unknown>,
    }));
}

/** A participant of their own for a test that saves, whose saves no other test's numbering has reached. */
function freshParticipant(): ParticipantHeader {
  return { ...jane, id: crypto.randomUUID() };
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

/** Types `value` into the Guided notes box, as one input. */
function type(value: string) {
  fireEvent.input(guidedNotes(), { target: { value } });
}

/** The tick boxes of Goals, in its order. */
function goalBoxes(): HTMLInputElement[] {
  return within(within(main()).getByRole('group', { name: '1. Goals' })).getAllByRole('checkbox');
}

/** The Flag for manager tick box. */
function flagBox(): HTMLInputElement {
  return within(main()).getByRole('checkbox', { name: 'Flag for manager' });
}

/** The Reason block that the flag reveals, which is always in the page. */
function reasonBlock(): HTMLElement {
  const block = main().querySelector<HTMLElement>('#flag-reason-group');
  if (block === null) {
    throw new Error('The form has no #flag-reason-group.');
  }

  return block;
}

/** The Reason field, found by its label, which finds it while it is hidden too. */
function reasonField(): HTMLTextAreaElement {
  return within(reasonBlock()).getByLabelText<HTMLTextAreaElement>('Reason');
}

/** Types `value` into Reason, as one input. */
function typeReason(value: string) {
  fireEvent.input(reasonField(), { target: { value } });
}

/** Reason's count as it shows, hidden from screen readers. */
function reasonCount(): HTMLElement | null {
  return reasonBlock().querySelector('p[aria-hidden="true"]');
}

/** The polite twin of Reason's count, which screen readers hear. */
function reasonTwin(): HTMLElement | null {
  return reasonBlock().querySelector('[aria-live="polite"]');
}

/** The save indicator in the top bar. */
function saveStatus(): HTMLElement {
  const status = pageBar().querySelector<HTMLElement>('#save-status');
  if (status === null) {
    throw new Error('The top bar has no #save-status.');
  }

  return status;
}

/** The save indicator's words as they show, without the comma that only screen readers get. */
function shownSaveStatus(): string {
  const shown = saveStatus().cloneNode(true) as HTMLElement;
  shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
    hidden.remove();
  });
  return shown.textContent;
}

/** The lists-changed notice's region above Goals, a `<div role="status">` in `<main>`. */
function listsNotice(): HTMLElement {
  const notices = within(main())
    .getAllByRole('status')
    .filter((region) => region.tagName === 'DIV');
  expect(notices).toHaveLength(1);
  return notices[0];
}

/**
 * The form's assertive region, after the page's content, which says a dead end's sentence once: visually hidden, where
 * the alert above Submit note and the confirmation's alert line show their words.
 */
function assertive(): HTMLElement {
  const regions = screen.getAllByRole('alert').filter((region) => region.classList.contains('visually-hidden'));
  expect(regions).toHaveLength(1);
  return regions[0];
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
  serve(antiforgeryPath, () =>
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
    expect(within(main()).getAllByRole('checkbox')).toEqual([flagBox()]);
    expect(guidedNotes()).toBeInTheDocument();
  });

  it('shows a draft with its own goals, ticks and text, the prompts not shown over the text', async () => {
    serveNote(jane, json(savedDraft()));
    renderApp(paths(jane).page);
    await settle();

    expect(goalBoxes().map((box) => box.checked)).toEqual([true, false, true]);
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
  it('ticks and unticks a goal when its words are clicked', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    const box = within(main()).getByRole('checkbox', { name: bus.text });

    fireEvent.click(within(main()).getByText(bus.text));
    expect(box).toBeChecked();
    fireEvent.click(within(main()).getByText(bus.text));
    expect(box).not.toBeChecked();
  });
});

describe('Autosave', () => {
  it.each([
    {
      change: 'tick',
      make: () => {
        fireEvent.click(within(main()).getByText(bus.text));
      },
      narrative: '',
      tickedGoalIds: [bus.id],
    },
    {
      change: 'keystroke',
      make: () => {
        type('J');
      },
      narrative: 'J',
      tickedGoalIds: [],
    },
  ])(
    "sends the first $change in a PUT 2 s later, with the template's lists' version, then says \"Saved 9:42 am\"",
    async ({ make, narrative, tickedGoalIds }) => {
      const participant = freshParticipant();
      serveNote(participant);
      serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
      renderApp(paths(participant).page);
      await settle();
      expect(saveStatus()).toBeEmptyDOMElement();

      make();
      expect(shownSaveStatus()).toBe('Saving…');
      await wait(1_999);
      expect(saves()).toEqual([]);
      await wait(1);

      expect(saves()).toEqual([
        {
          path: paths(participant).draft,
          body: {
            clientId: expect.any(String) as string,
            seq: 1,
            listsVersion: 'lists-1',
            narrative,
            tickedGoalIds,
            isFlagged: false,
            flagReason: null,
          },
        },
      ]);
      expect(saves()[0].body.clientId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
      await settle();
      expect(shownSaveStatus()).toBe(`Saved 9:42${nbsp}am`);
      expect(saveStatus().querySelector('time')).toHaveAttribute('datetime', '2026-09-30T23:42:00Z');
    },
  );

  // design.md §4.3 *No goals set up*: the note can still be written.
  it('saves the note of a participant with no goals, with no ticks', async () => {
    const participant = freshParticipant();
    serveNote(participant, json(template([])));
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();

    type('Jane made toast.');
    await wait(2_000);
    await settle();

    expect(saves().map(({ body }) => [body.narrative, body.tickedGoalIds])).toEqual([['Jane made toast.', []]]);
    expect(shownSaveStatus()).toBe(`Saved 9:42${nbsp}am`);
  });

  it('sends nothing more once the newest change is saved, on a blur or with time', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();

    type('Jane made toast.');
    await wait(2_000);
    await settle();
    guidedNotes().focus();
    guidedNotes().blur();
    await wait(30_000);

    expect(saves()).toHaveLength(1);
  });

  it("saves at least twice in 12 s of non-stop typing, numbered, with no lists' version after the first", async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();

    let typed = '';
    for (let ms = 0; ms < 12_000; ms += 250) {
      typed += 'x';
      type(typed);
      await wait(250);
    }

    const sent = saves().map(({ body }) => body);
    expect(sent.length).toBeGreaterThanOrEqual(2);
    expect(sent.map((body) => body.seq)).toEqual(sent.map((_, index) => index + 1));
    expect(new Set(sent.map((body) => body.clientId)).size).toBe(1);
    expect(sent.map((body) => 'listsVersion' in body)).toEqual(sent.map((_, index) => index === 0));
    const lengths = sent.map((body) => (body.narrative as string).length);
    expect(lengths).toEqual([...lengths].sort((a, b) => a - b));
  });

  it('sends a change at once when the box loses focus, with no wait', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();

    guidedNotes().focus();
    type('Jane made toast.');
    guidedNotes().blur();
    await wait(0);

    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast.']);
  });

  it('sends a tick at once when its box loses focus, with no wait', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();
    const box = within(main()).getByRole('checkbox', { name: sister.text });

    box.focus();
    fireEvent.click(box);
    box.blur();
    await wait(0);

    expect(saves().map(({ body }) => body.tickedGoalIds)).toEqual([[sister.id]]);
  });

  it('keeps "Saving…" through the answer to a save that a change made since missed, until the next', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    const first = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, first.answer);
    renderApp(paths(participant).page);
    await settle();
    type('Jane');
    await wait(2_000);
    expect(saves()).toHaveLength(1);

    type('Jane made toast.');
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    first.release();
    await settle();
    expect(shownSaveStatus()).toBe('Saving…');
    expect(saves()).toHaveLength(1);

    await wait(2_000);
    await settle();
    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane', 'Jane made toast.']);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
  });

  it('sends a save that falls due while one is out as that one is answered, with what the form holds', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    const first = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, first.answer);
    renderApp(paths(participant).page);
    await settle();
    guidedNotes().focus();
    type('Jane');
    guidedNotes().blur();
    await wait(0);

    guidedNotes().focus();
    type('Jane made toast.');
    fireEvent.click(within(main()).getByText(bus.text));
    guidedNotes().blur();
    await wait(0);
    expect(saves()).toHaveLength(1);
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    first.release();
    await wait(0);

    expect(saves().map(({ body }) => [body.seq, body.narrative, body.tickedGoalIds])).toEqual([
      [1, 'Jane', []],
      [2, 'Jane made toast.', [bus.id]],
    ]);
  });

  it('keeps "Saving…" after a save that failed, sending nothing more until the next change or a blur', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, problem(503));
    renderApp(paths(participant).page);
    await settle();

    guidedNotes().focus();
    type('Jane made toast.');
    await wait(2_000);
    await settle();
    expect(saves()).toHaveLength(1);
    await wait(30_000);
    expect(saves()).toHaveLength(1);
    expect(shownSaveStatus()).toBe('Saving…');

    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    guidedNotes().blur();
    await wait(0);
    expect(saves().map(({ body }) => [body.seq, body.listsVersion])).toEqual([
      [1, 'lists-1'],
      [2, 'lists-1'],
    ]);
    await settle();
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
  });

  it('keeps all of 20,050 characters on screen, with "You have 50 characters too many", and sends 20,000', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();

    type('x'.repeat(20_050));
    expect(within(main()).getByText('You have 50 characters too many')).toHaveAttribute('aria-hidden', 'true');
    await wait(2_000);

    expect(guidedNotes()).toHaveValue('x'.repeat(20_050));
    expect(saves().map(({ body }) => body.narrative)).toEqual(['x'.repeat(20_000)]);
  });

  it(
    'shows a reopened draft with "Draft · saved 9:42 am", its text and its ticks, and saves it with no lists\' version',
    async () => {
      const participant = freshParticipant();
      const draft = held(json(savedDraft()));
      serveNote(participant, draft.answer);
      serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
      renderApp(paths(participant).page);
      await settle();
      const whileLoading = saveStatus();

      draft.release();
      await settle();

      expect(shownSaveStatus()).toBe(`Draft · saved 9:42${nbsp}am`);
      expect(saveStatus().textContent).toBe(`Draft ·, saved 9:42${nbsp}am`);
      // A new element, put in the page with its words, rather than words written into the one there while loading.
      expect(saveStatus()).not.toBe(whileLoading);
      expect(guidedNotes()).toHaveValue('Jane made toast.\nThen a walk to the shops.');
      expect(goalBoxes().map((box) => box.checked)).toEqual([true, false, true]);

      fireEvent.click(within(main()).getByText(sister.text));
      await wait(2_000);
      expect(saves().map(({ body }) => body)).toEqual([
        {
          clientId: expect.any(String) as string,
          seq: 1,
          narrative: 'Jane made toast.\nThen a walk to the shops.',
          tickedGoalIds: [breakfast.id],
          isFlagged: false,
          flagReason: null,
        },
      ]);
    },
  );

  it('gives the date with the time of a reopened draft last saved on a day before today', async () => {
    const participant = freshParticipant();
    serveNote(participant, json({ ...savedDraft(), savedAtUtc: '2026-09-29T23:42:00Z' }));
    renderApp(paths(participant).page);
    await settle();

    expect(shownSaveStatus()).toBe(`Draft · saved Wed 30${nbsp}Sep 2026, 9:42${nbsp}am`);
  });

  it('carries on numbering its saves when the form is left and opened again in the same page load', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    serve(todayPath, json({ date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList));
    const { router } = renderApp(paths(participant).page);
    await settle();
    type('Jane');
    await wait(2_000);
    await settle();

    await act(() => router.navigate('/'));
    await settle();
    serve(paths(participant).draft, json(savedDraft('Jane')));
    await act(() => router.navigate(paths(participant).page));
    await settle();
    type('Jane made toast.');
    await wait(2_000);

    const [firstSave, laterSave] = saves().map(({ body }) => body);
    expect(laterSave).toEqual({
      clientId: firstSave.clientId,
      seq: 2,
      narrative: 'Jane made toast.',
      tickedGoalIds: [breakfast.id, sister.id],
      isFlagged: false,
      flagReason: null,
    });
  });
});

describe('A dead end at the first save', () => {
  const afterMidnight =
    'It’s now after midnight, so this note can’t be started for yesterday. Ask a manager to record it.';

  it.each([
    {
      deadEnd: 'after midnight, on a form opened at 11:58 pm whose first change came at 12:01 am',
      me: worker,
      noteDate: '2026-10-01',
      // 11:58 pm on 1 October in Melbourne, three minutes before the first change.
      openedAt: '2026-10-01T13:58:00Z',
      waited: 3 * 60_000,
      refusal: problem(422, { code: 'note.past_date_manager_only' }),
      sentence: afterMidnight,
    },
    {
      deadEnd: "someone else's draft of today's note",
      me: worker,
      noteDate: '2026-10-01',
      openedAt: '2026-10-01T00:30:00Z',
      waited: 0,
      // 9:14 am on 1 October in Melbourne.
      refusal: startedBy('2026-09-30T23:14:00Z'),
      sentence: `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    },
    {
      deadEnd: "someone else's draft of an earlier day's note, which a manager may start",
      me: manager,
      noteDate: '2026-09-30',
      openedAt: '2026-10-01T00:30:00Z',
      waited: 0,
      // 9:14 am on 30 September in Melbourne.
      refusal: startedBy('2026-09-29T23:14:00Z'),
      sentence: `Alex P. started the note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    },
    {
      deadEnd: 'an archived participant',
      me: worker,
      noteDate: '2026-10-01',
      openedAt: '2026-10-01T00:30:00Z',
      waited: 0,
      refusal: problem(422, { code: 'note.participant_archived' }),
      sentence: 'Jane Citizen is archived, so this note cannot be started. Ask a manager.',
    },
  ])(
    'shows $deadEnd in the top bar\'s second row, says it once, and keeps the ticks and text',
    async ({ me, noteDate, openedAt, waited, refusal, sentence }) => {
      vi.setSystemTime(new Date(openedAt));
      const participant = freshParticipant();
      serveNote(participant, json(template()), noteDate);
      serveSave(paths(participant, noteDate).draft, refusal);
      renderApp(paths(participant, noteDate).page, me);
      await settle();
      const region = assertive();
      expect(region).toBeEmptyDOMElement();
      await wait(waited);

      fireEvent.click(within(main()).getByText(bus.text));
      type('Jane made toast.');
      await wait(2_000);
      await settle();

      expect(saves()).toHaveLength(1);
      const banner = within(pageBar()).getByRole('region', { name: sentence });
      expect(banner.tagName).toBe('SECTION');
      expect(banner.textContent).toBe(sentence);
      expect(follows(banner, saveStatus())).toBe(true);
      expect(within(banner).queryByRole('status')).toBeNull();
      // The region there from the first render, which says the sentence once.
      expect(assertive()).toBe(region);
      expect(region.textContent).toBe(sentence);
      expect(region).toHaveClass('visually-hidden');
      expect(shownSaveStatus()).toBe('Not saved');
      expect(guidedNotes()).toHaveValue('Jane made toast.');
      expect(within(main()).getByRole('checkbox', { name: bus.text })).toBeChecked();
    },
  );

  it('stops autosave, keeping every tick and the text to change, and never stops the person leaving', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, problem(422, { code: 'note.past_date_manager_only' }));
    serve(todayPath, json({ date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList));
    const { router } = renderApp(paths(participant).page);
    await settle();
    guidedNotes().focus();
    type('Jane made toast.');
    guidedNotes().blur();
    await wait(0);
    await settle();
    expect(shownSaveStatus()).toBe('Not saved');

    guidedNotes().focus();
    type('Jane made toast. Then a walk to the shops.');
    fireEvent.click(within(main()).getByText(sister.text));
    guidedNotes().blur();
    await wait(30_000);

    expect(saves()).toHaveLength(1);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(guidedNotes()).toHaveValue('Jane made toast. Then a walk to the shops.');
    expect(guidedNotes()).not.toHaveAttribute('readonly');
    expect(within(main()).getByRole('checkbox', { name: sister.text })).toBeChecked();
    expect(within(main()).getByRole('checkbox', { name: sister.text })).toBeEnabled();

    await press(within(pageBar()).getByRole('link', { name: 'Today' }));
    await settle();
    expect(router.state.location.pathname).toBe('/');
  });
});

describe('Lists changed before the first save', () => {
  /**
   * Opens a new note for a participant of its own, whose first save is answered `409 note.lists_changed`, and the
   * saves after it `201`, held back until the test lets them go; and holds back the form as it is read again.
   */
  async function openWithListsChanging() {
    const participant = freshParticipant();
    serveNote(participant);
    const resaved = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, inTurn(problem(409, { code: 'note.lists_changed' }), resaved.answer));
    renderApp(paths(participant).page);
    await settle();
    // As the form is now, with Phones her sister archived and Waters the garden added since the form opened.
    const reload = held(json({ ...template([breakfast, garden, bus]), listsVersion: 'lists-2' }));
    serve(paths(participant).draft, reload.answer);
    return { participant, reload, resaved };
  }

  it(
    "shows the lists as they are now, keeping the text and a listed goal's tick, and saves again with their version",
    async () => {
      const { participant, reload, resaved } = await openWithListsChanging();
      const notice = listsNotice();
      expect(notice).toBeEmptyDOMElement();
      fireEvent.click(within(main()).getByText(breakfast.text));
      fireEvent.click(within(main()).getByText(sister.text));
      type('Jane made toast.');

      await wait(2_000);
      const sent = sentRequests();
      const { draft } = paths(participant);
      expect(sent.slice(sent.indexOf(`PUT ${draft}`))).toEqual([`PUT ${draft}`, `GET ${draft}`]);
      expect(shownSaveStatus()).toBe('Saving…');
      expect(notice).toBeEmptyDOMElement();
      reload.release();
      await settle();

      expect(shownSaveStatus()).toBe('Saving…');
      expect(listsNotice()).toBe(notice);
      expect(notice.textContent).toBe('The goal or common-item list was just changed. Please check your ticks.');
      expect(follows(within(main()).getByRole('group', { name: '1. Goals' }), notice)).toBe(true);
      expect(goalBoxes().map((box) => [box.labels?.[0].textContent, box.checked])).toEqual([
        [breakfast.text, true],
        [garden.text, false],
        [bus.text, false],
      ]);
      expect(guidedNotes()).toHaveValue('Jane made toast.');
      expect(saves().map(({ body }) => [body.listsVersion, body.tickedGoalIds, body.narrative])).toEqual([
        ['lists-1', [breakfast.id, sister.id], 'Jane made toast.'],
        ['lists-2', [breakfast.id], 'Jane made toast.'],
      ]);
      resaved.release();
      await settle();
      expect(shownSaveStatus()).toBe(`Saved 9:42${nbsp}am`);

      type('Jane made toast. Then a walk.');
      await wait(2_000);
      await settle();
      expect(saves()[2].body).not.toHaveProperty('listsVersion');
      expect(notice).toHaveTextContent('Please check your ticks.');
    },
  );

  it.each([
    {
      focused: 'a tick box whose goal is still listed',
      box: () => within(main()).getByRole('checkbox', { name: bus.text }),
    },
    { focused: 'the Guided notes box', box: () => guidedNotes() },
  ])('keeps focus in $focused', async ({ box }) => {
    const { reload } = await openWithListsChanging();
    const focused = box();
    focused.focus();
    if (focused instanceof HTMLTextAreaElement) {
      type('Jane made toast.');
    } else {
      fireEvent.click(focused);
    }

    await wait(2_000);
    reload.release();
    await settle();

    expect(listsNotice()).toHaveTextContent('Please check your ticks.');
    expect(focused).toHaveFocus();
  });

  // Another user's first save came between this one and the form read again (design.md §5.5 item 3).
  it("shows the in-progress dead end when the form read again is someone else's draft", async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, problem(409, { code: 'note.lists_changed' }));
    renderApp(paths(participant).page);
    await settle();
    serve(paths(participant).draft, startedBy('2026-09-30T23:14:00Z'));

    type('Jane made toast.');
    await wait(2_000);
    await settle();

    const sentence = `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`;
    expect(within(pageBar()).getByRole('region', { name: sentence })).toBeInTheDocument();
    expect(assertive().textContent).toBe(sentence);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(saves()).toHaveLength(1);
    expect(listsNotice()).toBeEmptyDOMElement();
    expect(guidedNotes()).toHaveValue('Jane made toast.');
  });

  it('moves focus to the notice when the tick box that had it has gone', async () => {
    const { reload } = await openWithListsChanging();
    const box = within(main()).getByRole('checkbox', { name: sister.text });
    box.focus();
    fireEvent.click(box);

    await wait(2_000);
    reload.release();
    await settle();

    expect(box).not.toBeInTheDocument();
    expect(listsNotice()).toHaveFocus();
    expect(listsNotice()).toHaveAttribute('tabindex', '-1');
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

describe('Flag for manager', () => {
  const reason = 'Jane seemed unwell after lunch.';

  // jsdom has no scrollIntoView. The spy records each call, of which a tick must make none.
  const scrollIntoView = vi.fn<(this: Element, options?: boolean | ScrollIntoViewOptions) => void>();

  beforeEach(() => {
    Element.prototype.scrollIntoView = scrollIntoView;
  });

  afterEach(() => {
    Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
    scrollIntoView.mockReset();
  });

  /** Opens a new note for a participant of its own, whose saves are answered with 9:42 am, and gives them. */
  async function openSaving(): Promise<ParticipantHeader> {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    renderApp(paths(participant).page);
    await settle();
    return participant;
  }

  /** The flag and the reason of each save sent, in order. */
  function sentFlags(): unknown[][] {
    return saves().map(({ body }) => [body.isFlagged, body.flagReason]);
  }

  it("is a row after Guided notes, on the tick lists' row and box, with Reason in the page and hidden", async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    const box = flagBox();
    expect(box).toHaveAttribute('id', 'flag-for-manager');
    expect(box).toHaveClass(tickList.box);
    expect(box.closest('label')).toHaveClass(tickList.row);
    expect(box).not.toBeChecked();
    expect(box).toHaveAttribute('aria-controls', 'flag-reason-group');
    expect(box).toHaveAttribute('aria-expanded', 'false');
    expect(follows(box, guidedNotes())).toBe(true);
    expect(within(box.closest('label')!.parentElement!).queryByRole('heading')).toBeNull();
    expect(reasonBlock()).toHaveAttribute('hidden');
    expect(follows(reasonBlock(), box)).toBe(true);
    expect(within(main()).queryByRole('textbox', { name: 'Reason' })).toBeNull();
    expect(reasonTwin()).toBeEmptyDOMElement();
  });

  it('shows Reason at once when ticked, with focus kept on the box and nothing scrolled', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    scrollPage(400);
    const box = flagBox();

    box.focus();
    fireEvent.click(box);

    expect(box).toBeChecked();
    expect(box).toHaveAttribute('aria-expanded', 'true');
    expect(reasonBlock()).not.toHaveAttribute('hidden');
    expect(box).toHaveFocus();
    expect(window.scrollY).toBe(400);
    expect(scrollIntoView).not.toHaveBeenCalled();
    const field = within(main()).getByRole('textbox', { name: 'Reason' });
    expect(field).toBe(reasonField());
    expect(follows(field, box)).toBe(true);
    expect(field).toHaveAttribute('id', 'flag-reason');
    expect(field).toHaveAttribute('rows', '4');
    expect(field).toHaveAttribute('aria-required', 'true');
    expect(field).toHaveAccessibleDescription('You can enter up to 200 characters');
    expect(main().querySelector('#flag-reason-hint')).toHaveClass('visually-hidden');
    for (const attribute of ['required', 'maxlength', 'placeholder', 'aria-invalid']) {
      expect(field).not.toHaveAttribute(attribute);
    }

    expect(field).toHaveValue('');
    expect(field).toHaveStyle({ resize: 'vertical' });
    expect(within(reasonBlock()).getByText('Reason', { selector: 'label' })).toHaveStyle({ fontWeight: '700' });
    expect(reasonCount()?.textContent).toBe('You can enter up to 200 characters');
    expect(reasonTwin()).toBeEmptyDOMElement();
  });

  it.each([
    [47, 'You have 153 characters remaining'],
    [199, 'You have 1 character remaining'],
  ])('counts %i characters as "%s", not in bold', async (length, words) => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    fireEvent.click(flagBox());

    typeReason('x'.repeat(length));

    expect(reasonCount()?.textContent).toBe(words);
    expect(reasonCount()).not.toHaveStyle({ fontWeight: '700' });
  });

  it('counts 212 characters as "You have 12 characters too many", bold, keeping them all, after a pause', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    fireEvent.click(flagBox());

    typeReason('x'.repeat(212));

    expect(reasonCount()?.textContent).toBe('You have 12 characters too many');
    expect(reasonCount()).toHaveStyle({ fontWeight: '700', color: 'var(--colour-error)' });
    expect(reasonField()).toHaveValue('x'.repeat(212));
    expect(reasonField()).not.toHaveAttribute('aria-invalid');
    await wait(999);
    expect(reasonTwin()).toBeEmptyDOMElement();
    await wait(1);
    expect(reasonTwin()?.textContent).toBe('You have 12 characters too many');
  });

  it('saves a tick, a reason and an untick, each a change, with no reason while it is blank or unticked', async () => {
    await openSaving();

    fireEvent.click(flagBox());
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(2_000);
    await settle();
    typeReason('   ');
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(2_000);
    await settle();
    typeReason(reason);
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(2_000);
    await settle();
    fireEvent.click(flagBox());
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(2_000);
    await settle();

    expect(sentFlags()).toEqual([
      [true, null],
      [true, null],
      [true, reason],
      [false, null],
    ]);
    expect(shownSaveStatus()).toBe(`Saved 9:42${nbsp}am`);
  });

  it('keeps the reason while unticked, sending none, and brings it back on a re-tick, with no error', async () => {
    await openSaving();
    fireEvent.click(flagBox());
    typeReason(reason);

    fireEvent.click(flagBox());
    await wait(2_000);
    await settle();

    expect(reasonBlock()).toHaveAttribute('hidden');
    expect(flagBox()).toHaveAttribute('aria-expanded', 'false');
    expect(reasonField()).toHaveValue(reason);
    expect(sentFlags()).toEqual([[false, null]]);

    fireEvent.click(flagBox());
    await wait(2_000);

    expect(reasonBlock()).not.toHaveAttribute('hidden');
    expect(reasonField()).toHaveValue(reason);
    expect(reasonField()).not.toHaveAttribute('aria-invalid');
    expect(reasonCount()?.textContent).toBe('You have 169 characters remaining');
    expect(sentFlags()).toEqual([
      [false, null],
      [true, reason],
    ]);
  });

  it('keeps all of 210 characters on screen and sends the first 200', async () => {
    await openSaving();
    fireEvent.click(flagBox());

    typeReason('x'.repeat(210));
    await wait(2_000);

    expect(reasonField()).toHaveValue('x'.repeat(210));
    expect(sentFlags()).toEqual([[true, 'x'.repeat(200)]]);
  });

  it('sends 199 characters of a reason whose 200th would split a surrogate pair', async () => {
    await openSaving();
    fireEvent.click(flagBox());

    // "😀" is a surrogate pair, two code units, the 200th and 201st here.
    typeReason(`${'x'.repeat(199)}😀${'y'.repeat(9)}`);
    await wait(2_000);

    expect(reasonCount()?.textContent).toBe('You have 10 characters too many');
    expect(sentFlags()).toEqual([[true, 'x'.repeat(199)]]);
  });

  it('shows a draft loaded flagged with its reason from its first render, saying no count', async () => {
    const participant = freshParticipant();
    const draft = held(json({ ...savedDraft(), isFlagged: true, flagReason: reason }));
    serveNote(participant, draft.answer);
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    renderApp(paths(participant).page);
    await settle();
    // Each element whose hidden attribute changed, from before the form is in the page.
    const toggled: Node[] = [];
    const observer = new MutationObserver((records) => {
      toggled.push(...records.map((record) => record.target));
    });
    observer.observe(main(), { subtree: true, attributes: true, attributeFilter: ['hidden'] });

    draft.release();
    await settle();
    await wait(5_000);
    observer.disconnect();

    expect(flagBox()).toBeChecked();
    expect(flagBox()).toHaveAttribute('aria-expanded', 'true');
    expect(reasonBlock()).not.toHaveAttribute('hidden');
    expect(toggled).not.toContain(reasonBlock());
    expect(reasonField()).toHaveValue(reason);
    expect(reasonCount()?.textContent).toBe('You have 169 characters remaining');
    expect(reasonTwin()).toBeEmptyDOMElement();

    fireEvent.click(within(main()).getByText(bus.text));
    await wait(2_000);
    expect(sentFlags()).toEqual([[true, reason]]);
  });
});

describe('Opening the note form', () => {
  it("sends its two reads alone, beside the shell's /me, and nothing on scrolling, focusing or a tap", async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();
    // Each read twice, as StrictMode mounts the page, unmounts it and mounts it again: the unmount cancels the first
    // request, whose query function takes the abort signal, and the second mount sends it again. A production build
    // sends each once. Counted rather than compared as a set, so that a read sent once more as the form opens fails.
    const { header, draft } = paths(jane);
    expect(sentRequests().toSorted()).toEqual(
      [`GET ${mePath}`, `GET ${header}`, `GET ${header}`, `GET ${draft}`, `GET ${draft}`].toSorted(),
    );
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

// Submit note, its checks and its confirmation (note-form.md components 2 and 12 to 14, *Validation*, *Pressing Submit
// note*, *In the confirmation* and *After a successful submit*).

/** Answers each Submit of a note, a `POST {base}/versions` to `path`, with `answer` from now on. */
function serveSubmit(path: string, answer: Answer) {
  answers.set(`POST ${path}`, answer);
}

/** Submit's answer, `201`, or `200` for a retry whose first answer was lost: version 1, saved at 4:12 pm. */
function versionSaved(status: 200 | 201 = 201): Answer {
  const body = JSON.stringify({ versionNumber: 1, createdAtUtc: '2026-10-01T06:12:00Z' });
  return () => Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/json' } }));
}

/** A Submit as it was sent: its path, its `Idempotency-Key` and its body as JSON. */
type SentSubmit = { path: string; key: string | null; body: Record<string, unknown> };

/** Every Submit sent, in order. */
function submits(): SentSubmit[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => init?.method === 'POST' && (path as string).endsWith('/versions'))
    .map(([path, init]) => ({
      path: path as string,
      key: new Headers(init?.headers).get('Idempotency-Key'),
      body: JSON.parse(init!.body as string) as Record<string, unknown>,
    }));
}

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

/** Submit note, at the end of the form. */
function submitButton(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Submit note' });
}

/** The Submit confirmation, if it is open. */
function confirmation(): HTMLElement | null {
  return screen.queryByRole('alertdialog');
}

/** The open Submit confirmation. */
function openConfirmation(): HTMLElement {
  const dialog = confirmation();
  if (dialog === null) {
    throw new Error('The Submit confirmation is not open.');
  }

  return dialog;
}

/** A button of the open confirmation, by its name. */
function dialogButton(name: string): HTMLElement {
  return within(openConfirmation()).getByRole('button', { name });
}

/** The words of the open confirmation's buttons, in order. */
function dialogButtons(): string[] {
  return within(openConfirmation())
    .getAllByRole('button')
    .map((button) => button.textContent);
}

/** The error summary at the top of `<main>`, if it shows. */
function errorSummary(): HTMLElement | null {
  return within(main()).queryByRole('heading', { level: 2, name: 'There is a problem' })?.parentElement ?? null;
}

/** The error summary's items, in order: each one's words, and where its link goes, if it has one. */
function summaryItems(): [string, string | undefined][] {
  return within(errorSummary()!)
    .getAllByRole('listitem')
    .map((item) => [item.textContent, item.querySelector('a')?.getAttribute('href') ?? undefined]);
}

/** The page's visually hidden polite region, its `PageStatus`. */
function pageStatus(): HTMLElement {
  const regions = within(main())
    .getAllByRole('status')
    .filter((region) => region.tagName === 'P' && region.classList.contains('visually-hidden'));
  expect(regions).toHaveLength(1);
  return regions[0];
}

/** Today's success banner, found by its words. */
function banner(): HTMLElement | null {
  const words = within(main()).queryByText('Note for Jane Citizen submitted');
  return words?.closest<HTMLElement>('[data-route-focus]') ?? null;
}

/**
 * Opens a new note for a participant of their own, Jane Citizen by name, whose saves are answered at 9:42 am and whose
 * Submit gets `submit`, with Today's list to land on, and gives them and the router.
 */
async function openToSubmit(draft: Answer = json(template()), submit: Answer = versionSaved()) {
  const participant = freshParticipant();
  serveNote(participant, draft);
  serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
  serveSubmit(paths(participant).versions, submit);
  serve(todayPath, json({ date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList));
  const { router } = renderApp(paths(participant).page);
  await settle();
  return { participant, router };
}

/** Writes `text` in the Guided notes box and lets it save. */
async function write(text = 'Jane made toast.') {
  type(text);
  await wait(2_000);
  await settle();
}

/** Presses the confirmation's confirming button, and lets its request be answered. */
async function confirmSubmit() {
  await press(dialogButton('Submit note for Jane Citizen'));
  await settle();
}

/**
 * Stands in for what a browser does with focus as a modal dialog opens and closes, which vitest.setup.ts's stand-ins
 * leave out (HTML's dialog focusing steps): showModal() focuses the element with `autofocus`, or else the first
 * button, and close() gives focus back to what had it as the dialog opened. Otherwise as those stand-ins, `close`
 * fired in a task of its own. Gives the two spies.
 */
function focusLikeABrowser() {
  const openedFrom = new Map<HTMLDialogElement, Element | null>();
  const opened = vi
    .spyOn(HTMLDialogElement.prototype, 'showModal')
    .mockImplementation(function showModal(this: HTMLDialogElement) {
      openedFrom.set(this, document.activeElement);
      this.open = true;
      this.querySelector<HTMLElement>('[autofocus], button')?.focus();
    });
  const closed = vi
    .spyOn(HTMLDialogElement.prototype, 'close')
    .mockImplementation(function close(this: HTMLDialogElement, returnValue?: string) {
      if (!this.open) {
        return;
      }

      if (returnValue !== undefined) {
        this.returnValue = returnValue;
      }

      this.open = false;
      (openedFrom.get(this) as HTMLElement | null | undefined)?.focus();
      setTimeout(() => {
        this.dispatchEvent(new Event('close'));
      });
    });
  return { opened, closed };
}

describe('Submit note', () => {
  it('is the submit button of a <form noValidate> round the fields, after the flag, under an alert', async () => {
    await openToSubmit();

    const form = submitButton().closest('form');
    expect(form).toHaveAttribute('novalidate');
    expect(submitButton()).toHaveAttribute('type', 'submit');
    for (const field of [goalBoxes()[0], guidedNotes(), flagBox(), reasonField()]) {
      expect(form).toContainElement(field);
    }
    const alert = form!.querySelector('div[role="alert"]');
    expect(alert).toBeEmptyDOMElement();
    expect(follows(alert!, reasonBlock())).toBe(true);
    expect(follows(submitButton(), alert!)).toBe(true);
    expect(submitButton()).toHaveAttribute('aria-describedby', 'save-status');
    expect(submitButton()).not.toHaveAttribute('aria-disabled');
    expect(submitButton()).not.toHaveAttribute('disabled');
  });

  it('is aria-disabled while "Saving…" shows, described by it, and available once the change is saved', async () => {
    await openToSubmit();

    type('Jane made toast.');

    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(submitButton()).toHaveAttribute('data-unavailable');
    expect(submitButton()).not.toHaveAttribute('disabled');
    expect(submitButton()).toHaveAccessibleDescription('Saving…');
    await wait(2_000);
    await settle();
    expect(submitButton()).not.toHaveAttribute('aria-disabled');
    expect(submitButton()).toHaveAccessibleDescription(`Saved 9:42${nbsp}am`);
  });

  it('is aria-disabled with no connection, as the change stays unsaved', async () => {
    const { participant } = await openToSubmit();
    serveSave(paths(participant).draft, noConnection);

    await write();

    expect(saves()).toHaveLength(1);
    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(submitButton()).toHaveAccessibleDescription('Saving…');
  });

  it("is aria-disabled after a dead end, described by the indicator and the dead end's sentence", async () => {
    const { participant } = await openToSubmit();
    serveSave(paths(participant).draft, problem(422, { code: 'note.participant_archived' }));

    await write();

    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(submitButton()).toHaveAttribute('aria-describedby', 'save-status dead-end');
    expect(submitButton()).toHaveAccessibleDescription(
      'Not saved Jane Citizen is archived, so this note cannot be started. Ask a manager.',
    );
  });
});

describe("Submit's checks", () => {
  // jsdom has no scrollIntoView, which a summary link calls.
  const scrollIntoView = vi.fn<(this: Element, options?: boolean | ScrollIntoViewOptions) => void>();

  beforeEach(() => {
    Element.prototype.scrollIntoView = scrollIntoView;
  });

  afterEach(() => {
    Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
    scrollIntoView.mockReset();
  });

  const blank = 'Write your notes in the Guided notes box. The grey text is only a guide.';

  it('refuses a blank box, its message above it, the summary focused at the top, "Error: " in the title', async () => {
    await openToSubmit();

    await press(submitButton());

    const summary = errorSummary();
    expect(summary).toHaveFocus();
    expect(summary).toHaveAttribute('tabindex', '-1');
    expect(follows(pageHeading(), summary!)).toBe(true);
    expect(summaryItems()).toEqual([[blank, '#guided-notes']]);
    const message = main().querySelector('#guided-notes-error');
    expect(message?.textContent).toBe(`Error: ${blank}`);
    expect(follows(message!, within(main()).getByRole('heading', { level: 2, name: '3. Guided notes' }))).toBe(true);
    expect(follows(guidedNotes(), message!)).toBe(true);
    expect(guidedNotes()).toHaveAttribute('aria-invalid', 'true');
    expect(guidedNotes()).toHaveAccessibleDescription(`Error: ${blank}`);
    expect(guidedNotes().closest('[data-invalid]')).toContainElement(message as HTMLElement);
    expect(document.title).toBe('Error: Grow2Notes – Note');
    expect(saves()).toEqual([]);
    expect(confirmation()).toBeNull();
  });

  it('refuses spaces alone as blank, and words it with no guide when no prompts are set', async () => {
    await openToSubmit(json(template([breakfast], '')));
    await write('     ');

    await press(submitButton());

    expect(summaryItems()).toEqual([['Write your notes in the Guided notes box', '#guided-notes']]);
    expect(confirmation()).toBeNull();
  });

  it('refuses 19,995 characters and ten spaces as too long, as the count says, counting the whole text', async () => {
    await openToSubmit();
    await write(`${'x'.repeat(19_995)}${' '.repeat(10)}`);
    expect(within(main()).getByText('You have 5 characters too many', { selector: '[aria-hidden="true"]' }))
      .toBeInTheDocument();

    await press(submitButton());

    expect(summaryItems()).toEqual([['Guided notes must be 20,000 characters or less', '#guided-notes']]);
    expect(saves()).toHaveLength(1);
    expect(confirmation()).toBeNull();
  });

  it.each([
    { reason: 'none', typed: '', message: 'Enter a reason for the flag' },
    { reason: 'spaces alone', typed: '   ', message: 'Enter a reason for the flag' },
    { reason: '201 characters', typed: 'x'.repeat(201), message: 'Reason must be 200 characters or less' },
  ])('refuses a flagged note with $reason for its reason, after Guided notes in the summary', async ({
    typed,
    message,
  }) => {
    await openToSubmit();
    fireEvent.click(flagBox());
    typeReason(typed);

    await press(submitButton());

    expect(summaryItems()).toEqual([
      [blank, '#guided-notes'],
      [message, '#flag-reason'],
    ]);
    const inline = main().querySelector('#flag-reason-error');
    expect(inline?.textContent).toBe(`Error: ${message}`);
    expect(follows(inline!, main().querySelector('#flag-reason-hint')!)).toBe(true);
    expect(follows(reasonField(), inline!)).toBe(true);
    expect(reasonField()).toHaveAttribute('aria-invalid', 'true');
    expect(reasonField()).toHaveAttribute('aria-describedby', 'flag-reason-error flag-reason-hint');
    expect(reasonField()).toHaveAccessibleDescription(`Error: ${message} You can enter up to 200 characters`);
    expect(reasonBlock()).toHaveAttribute('data-invalid');
    expect(reasonField()).toHaveValue(typed);
  });

  it('focuses the summary again on every failed press, even with the same errors', async () => {
    await openToSubmit();
    await press(submitButton());
    expect(errorSummary()).toHaveFocus();
    guidedNotes().focus();

    await press(submitButton());

    expect(errorSummary()).toHaveFocus();
  });

  it("clears each message on the input that fixes it, the summary and the title's prefix with the last", async () => {
    await openToSubmit();
    fireEvent.click(flagBox());
    await press(submitButton());
    expect(summaryItems()).toHaveLength(2);

    type('J');

    expect(main().querySelector('#guided-notes-error')).toBeNull();
    expect(guidedNotes()).not.toHaveAttribute('aria-invalid');
    expect(guidedNotes().closest('[data-invalid]')).toBeNull();
    expect(summaryItems()).toEqual([['Enter a reason for the flag', '#flag-reason']]);
    expect(document.title).toBe('Error: Grow2Notes – Note');

    typeReason('Jane seemed unwell after lunch.');

    expect(errorSummary()).toBeNull();
    expect(main().querySelector('#flag-reason-error')).toBeNull();
    expect(reasonField()).not.toHaveAttribute('aria-invalid');
    expect(reasonField()).toHaveAttribute('aria-describedby', 'flag-reason-hint');
    expect(document.title).toBe('Grow2Notes – Note');
  });

  it("takes the reason's message away with the untick, and gives none on a re-tick until the next press", async () => {
    await openToSubmit();
    await write();
    fireEvent.click(flagBox());
    await press(submitButton());
    expect(summaryItems()).toEqual([['Enter a reason for the flag', '#flag-reason']]);

    fireEvent.click(flagBox());

    expect(errorSummary()).toBeNull();
    expect(main().querySelector('#flag-reason-error')).toBeNull();
    expect(reasonBlock()).not.toHaveAttribute('data-invalid');
    expect(document.title).toBe('Grow2Notes – Note');

    fireEvent.click(flagBox());

    expect(main().querySelector('#flag-reason-error')).toBeNull();
    expect(reasonField()).not.toHaveAttribute('aria-invalid');
  });

  it('goes from a summary link to its field, the label scrolled into view first, the address unchanged', async () => {
    const { participant, router } = await openToSubmit();
    await press(submitButton());

    fireEvent.click(within(errorSummary()!).getByRole('link', { name: blank }));

    expect(guidedNotes()).toHaveFocus();
    expect(scrollIntoView.mock.contexts).toEqual([guidedNotes().labels[0]]);
    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(router.state.location.hash).toBe('');
  });
});

describe('Pressing Submit note', () => {
  it('sends one check save of a draft with no change, "Saving…" until it is answered, then confirms', async () => {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft()));
    const check = held(saved('2026-10-01T05:42:00Z'));
    serveSave(paths(participant).draft, check.answer);
    renderApp(paths(participant).page);
    await settle();

    await press(submitButton());

    expect(saves()).toEqual([
      {
        path: paths(participant).draft,
        body: {
          clientId: expect.any(String) as string,
          seq: 1,
          narrative: 'Jane made toast.\nThen a walk to the shops.',
          tickedGoalIds: [breakfast.id, sister.id],
          isFlagged: false,
          flagReason: null,
        },
      },
    ]);
    expect(shownSaveStatus()).toBe('Saving…');
    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(confirmation()).toBeNull();

    check.release();
    await settle();

    expect(shownSaveStatus()).toBe(`Saved 3:42${nbsp}pm`);
    expect(openConfirmation()).toBeInTheDocument();
    expect(saves()).toHaveLength(1);
  });

  it('waits for the save out, then sends one check save, opening the confirmation once it is answered', async () => {
    const { participant } = await openToSubmit();
    const first = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, first.answer);
    guidedNotes().focus();
    type('Jane made toast.');
    guidedNotes().blur();
    await wait(0);
    expect(saves()).toHaveLength(1);
    const check = held(saved('2026-09-30T23:43:00Z'));
    serveSave(paths(participant).draft, check.answer);

    await press(submitButton());

    expect(saves()).toHaveLength(1);
    first.release();
    await settle();
    expect(saves().map(({ body }) => [body.seq, body.narrative])).toEqual([
      [1, 'Jane made toast.'],
      [2, 'Jane made toast.'],
    ]);
    expect(confirmation()).toBeNull();

    check.release();
    await settle();
    expect(openConfirmation()).toBeInTheDocument();
    expect(saves()).toHaveLength(2);
  });

  /** Opens a new note whose first save, of "Jane made toast.", is out and held, and gives its release. */
  async function openWithASaveOut() {
    const { participant } = await openToSubmit();
    const first = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, first.answer);
    guidedNotes().focus();
    type('Jane made toast.');
    guidedNotes().blur();
    await wait(0);
    serveSave(paths(participant).draft, saved('2026-09-30T23:43:00Z'));
    return first.release;
  }

  it('confirms and sends what the check save sent, with a change made while the save out was answered', async () => {
    const release = await openWithASaveOut();

    await press(submitButton());
    type('Jane made toast. Then a walk.');
    fireEvent.click(flagBox());
    typeReason('Jane seemed unwell after lunch.');
    release();
    await settle();

    expect(saves().map(({ body }) => [body.narrative, body.isFlagged])).toEqual([
      ['Jane made toast.', false],
      ['Jane made toast. Then a walk.', true],
    ]);
    expect(openConfirmation()).toHaveAccessibleDescription(`Thursday 1${nbsp}October 2026 Flagged for manager: Yes`);
    await confirmSubmit();

    expect(submits().map(({ body }) => [body.narrative, body.isFlagged, body.flagReason])).toEqual([
      ['Jane made toast. Then a walk.', true, 'Jane seemed unwell after lunch.'],
    ]);
  });

  it('checks what the check save sent, opening only the summary for a change made meanwhile that fails', async () => {
    const release = await openWithASaveOut();

    await press(submitButton());
    fireEvent.click(flagBox());
    release();
    await settle();

    expect(saves().map(({ body }) => body.isFlagged)).toEqual([false, true]);
    expect(confirmation()).toBeNull();
    expect(errorSummary()).toHaveFocus();
    expect(summaryItems()).toEqual([['Enter a reason for the flag', '#flag-reason']]);
  });

  it('opens nothing when the check save fails, then or later', async () => {
    const { participant } = await openToSubmit();
    await write();
    serveSave(paths(participant).draft, problem(503));

    await press(submitButton());
    await wait(30_000);

    expect(saves()).toHaveLength(2);
    expect(confirmation()).toBeNull();
    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
  });

  it('ends at the dead end, opening nothing, when the check save finds the note submitted elsewhere', async () => {
    const { participant } = await openToSubmit();
    await write();
    serveSave(paths(participant).draft, problem(409, { code: 'note.version_conflict' }));

    await press(submitButton());

    const sentence = 'This note was changed on another device or tab.';
    expect(within(pageBar()).getByRole('region', { name: sentence })).toBeInTheDocument();
    expect(assertive().textContent).toBe(sentence);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(confirmation()).toBeNull();
    expect(guidedNotes()).toHaveValue('Jane made toast.');
  });

  it("opens nothing during a dead end, saying its sentence again in the page's polite region", async () => {
    const { participant } = await openToSubmit();
    serveSave(paths(participant).draft, problem(422, { code: 'note.participant_archived' }));
    await write();
    expect(pageStatus()).toBeEmptyDOMElement();

    await press(submitButton());
    await wait(16);

    expect(pageStatus().textContent).toBe('Jane Citizen is archived, so this note cannot be started. Ask a manager.');
    expect(saves()).toHaveLength(1);
    expect(confirmation()).toBeNull();
    expect(errorSummary()).toBeNull();
  });

  it("answers Enter's implicit submission as a press, its checks first", async () => {
    await openToSubmit();

    fireEvent.submit(submitButton().closest('form')!);
    await settle();

    expect(errorSummary()).toHaveFocus();
    expect(saves()).toEqual([]);
    expect(confirmation()).toBeNull();
  });

  it('ignores a second press while its check save is out, and Enter twice after Submit sends nothing', async () => {
    const { participant } = await openToSubmit();
    await write();
    const check = held(saved('2026-09-30T23:43:00Z'));
    serveSave(paths(participant).draft, check.answer);

    await press(submitButton());
    await press(submitButton());
    expect(saves()).toHaveLength(2);
    check.release();
    await settle();

    const focused = document.activeElement ?? document.body;
    expect(focused).toBe(within(openConfirmation()).getByRole('heading', { level: 2 }));
    for (const key of ['Enter', 'Enter']) {
      fireEvent.keyDown(focused, { key });
      fireEvent.keyUp(focused, { key });
    }
    await wait(1_000);

    expect(submits()).toEqual([]);
    expect(screen.getAllByRole('alertdialog')).toHaveLength(1);
    expect(saves()).toHaveLength(2);
  });
});

describe('The Submit confirmation', () => {
  it("asks \"Submit today's note for\" and the name, on its title, with the date, the flag, two buttons", async () => {
    await openToSubmit();
    await write();

    await press(submitButton());

    const dialog = openConfirmation();
    expect(dialog).toHaveAccessibleName('Submit today’s note for Jane Citizen');
    const title = within(dialog).getByRole('heading', { level: 2 });
    expect(title).toHaveFocus();
    expect(within(title).getByText('Jane Citizen')).toHaveAttribute('translate', 'no');
    expect(dialog).toHaveAccessibleDescription(`Thursday 1${nbsp}October 2026 Flagged for manager: No`);
    expect(dialog.querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
    expect(dialogButtons()).toEqual(['Submit note for Jane Citizen', 'Go back']);
    expect(within(dialog).getByRole('alert')).toBeEmptyDOMElement();
    expect(within(dialog).getByRole('status')).toBeEmptyDOMElement();
  });

  it('says "Flagged for manager: Yes" for a note flagged with a reason', async () => {
    await openToSubmit();
    fireEvent.click(flagBox());
    typeReason('Jane seemed unwell after lunch.');
    await write();

    await press(submitButton());

    expect(openConfirmation()).toHaveAccessibleDescription(`Thursday 1${nbsp}October 2026 Flagged for manager: Yes`);
  });

  // note-form.md item 24: the reason typed is kept on screen, and sent with no flag.
  it('says "Flagged for manager: No" for a note unticked after a reason was typed, which sends no reason', async () => {
    await openToSubmit();
    fireEvent.click(flagBox());
    typeReason('Jane seemed unwell after lunch.');
    fireEvent.click(flagBox());
    await write();
    await press(submitButton());

    expect(openConfirmation()).toHaveAccessibleDescription(`Thursday 1${nbsp}October 2026 Flagged for manager: No`);
    await confirmSubmit();

    expect(submits().map(({ body }) => [body.isFlagged, body.flagReason])).toEqual([[false, null]]);
  });

  it('asks "Submit the note for" the name for a note of an earlier day, with its date', async () => {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft()), '2026-09-30');
    serveSave(paths(participant, '2026-09-30').draft, saved('2026-10-01T05:42:00Z'));
    renderApp(paths(participant, '2026-09-30').page, manager);
    await settle();

    await press(submitButton());

    expect(openConfirmation()).toHaveAccessibleName('Submit the note for Jane Citizen');
    expect(openConfirmation()).toHaveAccessibleDescription(
      `Wednesday 30${nbsp}September 2026 Flagged for manager: No`,
    );
  });

  it('wraps an unbroken name of 100 characters in its title and its confirming button, never cutting it', async () => {
    const long = { ...freshParticipant(), givenName: 'X'.repeat(50), familyName: 'Y'.repeat(49) };
    const name = `${long.givenName} ${long.familyName}`;
    serveNote(long, json(savedDraft()));
    serveSave(paths(long).draft, saved('2026-10-01T05:42:00Z'));
    renderApp(paths(long).page);
    await settle();

    await press(submitButton());

    expect(name).toHaveLength(100);
    expect(openConfirmation()).toHaveAccessibleName(`Submit today’s note for ${name}`);
    for (const element of [within(openConfirmation()).getByText(name), dialogButton(`Submit note for ${name}`)]) {
      expect(element).toHaveStyle({ overflowWrap: 'anywhere' });
      for (const cut of [{ textTransform: 'uppercase' }, { textOverflow: 'ellipsis' }, { whiteSpace: 'nowrap' }]) {
        expect(element).not.toHaveStyle(cut);
      }
    }
  });

  it('goes back with nothing sent, focus on Submit note, and autosave going on', async () => {
    focusLikeABrowser();
    await openToSubmit();
    await write();
    await press(submitButton());

    await press(dialogButton('Go back'));
    await settle();

    expect(confirmation()).toBeNull();
    expect(submitButton()).toHaveFocus();
    expect(submits()).toEqual([]);
    type('Jane made toast. Then a walk.');
    await wait(2_000);
    expect(saves().map(({ body }) => body.narrative)).toEqual([
      'Jane made toast.',
      'Jane made toast.',
      'Jane made toast. Then a walk.',
    ]);
  });

  it('sends the version with its key, the content as autosave sends it, and "Submitting…" after 1 s', async () => {
    const { participant } = await openToSubmit(json(template()), noAnswer);
    fireEvent.click(within(main()).getByText(bus.text));
    fireEvent.click(flagBox());
    typeReason('Jane seemed unwell after lunch.');
    await write();
    await press(submitButton());
    const status = within(openConfirmation()).getByRole('status');

    await press(dialogButton('Submit note for Jane Citizen'));

    expect(submits()).toEqual([
      {
        path: paths(participant).versions,
        key: expect.stringMatching(uuid) as string,
        body: {
          baseVersion: 0,
          narrative: 'Jane made toast.',
          tickedGoalIds: [bus.id],
          isFlagged: true,
          flagReason: 'Jane seemed unwell after lunch.',
        },
      },
    ]);
    // Both buttons busy, with their words kept, and the status line empty until the request has taken 1 s.
    expect(dialogButtons()).toEqual(['Submit note for Jane Citizen', 'Go back']);
    for (const button of within(openConfirmation()).getAllByRole('button')) {
      expect(button).toHaveAttribute('aria-disabled', 'true');
    }
    await wait(998);
    expect(status).toBeEmptyDOMElement();
    await wait(1);
    expect(status).toHaveTextContent(/^Submitting…$/);
  });

  it('sends no save while its request is out, though a browser closed it, and its failure shows', async () => {
    const post = held(noConnection);
    await openToSubmit(json(template()), post.answer);
    await write();
    await press(submitButton());
    await press(dialogButton('Submit note for Jane Citizen'));

    // A repeated Escape that a browser lets through while it is busy (confirm-dialog.md *Behaviour* 7).
    openConfirmation().closest('dialog')!.close();
    await settle();
    expect(confirmation()).toBeNull();
    type('Jane made toast. Then a walk.');
    await wait(5_000);
    expect(saves()).toHaveLength(2);

    post.release();
    await settle();

    const alert = submitButton().closest('form')!.querySelector('div[role="alert"]');
    expect(alert?.textContent).toBe('Not submitted: no connection. Your draft is saved. Try again.');
    expect(saves().map(({ body }) => body.narrative).at(-1)).toBe('Jane made toast. Then a walk.');
  });

  it('keeps Submit note busy while its request is out after a browser closed it, a press sending nothing', async () => {
    const post = held(noConnection);
    await openToSubmit(json(template()), post.answer);
    await write();
    await press(submitButton());
    await press(dialogButton('Submit note for Jane Citizen'));
    openConfirmation().closest('dialog')!.close();
    await settle();

    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    await press(submitButton());
    await wait(5_000);
    expect(saves()).toHaveLength(2);
    expect(confirmation()).toBeNull();

    post.release();
    await settle();

    expect(submitButton()).not.toHaveAttribute('aria-disabled');
    await press(submitButton());
    await settle();
    expect(saves()).toHaveLength(3);
    const [first] = submits();
    await confirmSubmit();
    expect(submits()).toHaveLength(2);
    expect(submits()[1].key).not.toBe(first.key);
  });

  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])(
    'stays open for "Not submitted: $cause…", focus on its confirming button, and a retry sends the same key',
    async ({ cause, answer }) => {
      await openToSubmit(json(template()), inTurn(answer, versionSaved(200)));
      await write();
      await press(submitButton());
      const confirm = dialogButton('Submit note for Jane Citizen');

      await press(confirm);

      expect(within(openConfirmation()).getByRole('alert').textContent).toBe(
        `Not submitted: ${cause}. Your draft is saved. Try again.`,
      );
      expect(confirm).toHaveFocus();
      expect(confirm).not.toHaveAttribute('aria-disabled');

      // Its first answer lost, the retry finds the version saved: 200, a success too.
      await press(confirm);
      await settle();

      const [first, retry] = submits();
      expect(first.key).toMatch(uuid);
      expect(retry.key).toBe(first.key);
      expect(retry.body).toEqual(first.body);
      expect(banner()).toHaveFocus();
    },
  );

  it('makes a new key for each opening', async () => {
    await openToSubmit(json(template()), problem(503));
    await write();
    await press(submitButton());
    await confirmSubmit();
    await press(dialogButton('Go back'));
    await settle();

    await press(submitButton());
    await confirmSubmit();

    const [first, second] = submits();
    expect(second.key).toMatch(uuid);
    expect(second.key).not.toBe(first.key);
  });

  it('takes its confirming button away for a note submitted elsewhere, then the form is a dead end', async () => {
    await openToSubmit(json(template()), problem(409, { code: 'note.version_conflict' }));
    await write();
    await press(submitButton());

    await confirmSubmit();

    const sentence = 'This note was changed on another device or tab.';
    expect(within(openConfirmation()).getByRole('alert').textContent).toBe(sentence);
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();
    expect(assertive()).toBeEmptyDOMElement();

    await press(dialogButton('Go back'));
    await settle();

    expect(confirmation()).toBeNull();
    expect(within(pageBar()).getByRole('region', { name: sentence })).toBeInTheDocument();
    expect(assertive().textContent).toBe(sentence);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(guidedNotes()).toHaveValue('Jane made toast.');
    type('Jane made toast. Then a walk.');
    await wait(5_000);
    expect(saves()).toHaveLength(2);
    expect(submits()).toHaveLength(1);
  });

  it.each([
    // .NET counts U+0085 as blank, where trim() keeps it, so the form's checks pass a text that the server refuses.
    { field: 'narrative', text: '\u0085' },
    { field: 'idempotencyKey', text: 'Jane made toast.' },
  ])('closes for a 422 under $field, which the checks do not explain, focusing one unlinked item', async ({
    field,
    text,
  }) => {
    const refused = problem(422, { code: 'validation.failed', errors: { [field]: ['Refused.'] } });
    await openToSubmit(json(template()), refused);
    await write(text);
    await press(submitButton());

    await confirmSubmit();

    expect(confirmation()).toBeNull();
    expect(errorSummary()).toHaveFocus();
    expect(summaryItems()).toEqual([
      ['Not submitted: something went wrong. Your draft is saved. Try again.', undefined],
    ]);
    expect(guidedNotes()).not.toHaveAttribute('aria-invalid');
    expect(within(main()).queryByText('Refused.')).toBeNull();
    expect(document.title).toBe('Error: Grow2Notes – Note');
    type(`${text} Then a walk.`);
    await wait(2_000);
    expect(saves()).toHaveLength(3);
  });

  it('closes for a 401 before sign-in shows in place, sending nothing more', async () => {
    await openToSubmit(json(template()), problem(401));
    await write();
    await press(submitButton());
    const dialog = openConfirmation();
    const close = vi.spyOn(HTMLDialogElement.prototype, 'close');
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    await confirmSubmit();
    await settle();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(pageHeading()).toHaveFocus();
    expect(close.mock.contexts).toEqual([dialog]);
    const viewCFocused = focus.mock.invocationCallOrder[focus.mock.contexts.indexOf(pageHeading())];
    expect(close.mock.invocationCallOrder[0]).toBeLessThan(viewCFocused);
    expect(confirmation()).toBeNull();
    expect(submits()).toHaveLength(1);
  });

  it('closes as Go back as the 28-minute warning opens, sending nothing, focus on Submit note after Stay', async () => {
    const { opened, closed } = focusLikeABrowser();
    await openToSubmit();
    await write();
    await press(submitButton());
    const dialog = openConfirmation();

    await wait(28 * 60_000);

    const warning = screen.getByRole<HTMLDialogElement>('dialog', { name: 'You’ll be signed out in 2 minutes' });
    expect(closed.mock.contexts).toEqual([dialog]);
    expect(opened.mock.contexts.at(-1)).toBe(warning);
    expect(closed.mock.invocationCallOrder[0]).toBeLessThan(opened.mock.invocationCallOrder.at(-1)!);
    expect(confirmation()).toBeNull();
    expect(submits()).toEqual([]);
    answers.set('POST /api/auth/ping', () => Promise.resolve(new Response(null, { status: 204 })));

    await press(within(warning).getByRole('button', { name: 'Stay signed in' }));
    await settle();

    expect(warning).not.toHaveAttribute('open');
    expect(submitButton()).toHaveFocus();
    expect(submits()).toEqual([]);
  });
});

describe('After Submit', () => {
  it('pops back to Today, the banner focused before its <h1>, and fetches the list afresh', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:42:00Z'));
    serveSubmit(paths(participant).versions, versionSaved());
    const list = { date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList;
    serve(todayPath, json(list));
    const { router } = renderApp('/');
    await settle();
    await press(rowLink('Jane Citizen'));
    await settle();
    await write();
    await press(submitButton());
    const fresh = held(json(list));
    serve(todayPath, fresh.answer);

    await confirmSubmit();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('POP');
    expect(banner()).toHaveFocus();
    expect(banner()).toHaveTextContent(/^Note for Jane Citizen submitted$/);
    expect(banner()?.nextElementSibling).toBe(pageHeading());
    for (const attribute of ['role', 'aria-live']) {
      expect(banner()).not.toHaveAttribute(attribute);
    }
    // No row kept from before the Submit: the rows come with the list that the server gives now.
    expect(within(main()).queryByRole('list')).toBeNull();
    const sent = sentRequests();
    expect(sent.lastIndexOf(`GET ${todayPath}`)).toBeGreaterThan(sent.indexOf(`POST ${paths(participant).versions}`));
    fresh.release();
    await settle();
    expect(rowLink('Jane Citizen')).toBeInTheDocument();
    expect(banner()).toHaveFocus();
  });

  it('puts Today in place of a form opened from its address, the banner focused, and shows it once', async () => {
    const { participant, router } = await openToSubmit();
    await write();
    await press(submitButton());

    await confirmSubmit();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(banner()).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Today');

    // Gone once Today is left and opened again.
    await press(rowLink('Jane Citizen'));
    await settle();
    expect(router.state.location.pathname).toBe(paths(participant).page);
    await press(within(pageBar()).getByRole('link', { name: 'Today' }));
    await settle();
    expect(router.state.location.pathname).toBe('/');
    expect(banner()).toBeNull();
  });
});
