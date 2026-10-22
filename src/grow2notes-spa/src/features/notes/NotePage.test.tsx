import { onlineManager, QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import buttonStyles from '../../components/Button.module.css';
import noticeStyles from '../../components/Notice.module.css';
import tagStyles from '../../components/Tag.module.css';
import tickList from '../../components/TickList.module.css';
import type { TickedItem } from '../../components/TickListRead.tsx';
import { fullPageLoad } from '../../routes/fullPageLoad.ts';
import { disarmUnloadGuard, rearmUnloadGuard } from '../../routes/leaveGuard.ts';
import { routes } from '../../routes/routes.tsx';
import type { TodayList, TodayNote, TodayParticipant } from '../today/todayQuery.ts';
import type { VersionSaved } from './NoteForm.tsx';
import type {
  Draft,
  DraftGroup,
  DraftItem,
  DraftNote,
  DraftRead,
  NoteAnswer,
  ParticipantHeader,
  ReadGroup,
  SubmittedNote,
} from './noteQueries.ts';
import notePage from './NotePage.module.css';
import readView from './ReadView.module.css';

// jsdom does not navigate, so the full page load of Sign out, and of another person's sign-in, is a stand-in that
// counts its calls.
vi.mock('../../routes/fullPageLoad.ts');

// A memory router of the app's routes.
type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';
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

/** A goal of Jane's, or a common item, unticked. */
function goal(id: string, text: string): DraftItem {
  return { id, text, isTicked: false };
}

const breakfast = goal('0b2f6c1e-4d38-4a95-9e07-6c1d8f3a5b24', 'Makes own breakfast');
const bus = goal('5e9a3d7c-1b46-4f82-a0d3-8c2e6b4f1a97', 'Catches bus to day program');
const sister = goal('8a1c6e3f-7d52-4b09-b3e4-1f9d2c7a5e68', 'Phones her sister');
const garden = goal('2d7b9f4a-6c13-4e58-9a21-5f8e3c0b7d46', 'Waters the garden');

const prompts = 'Mood and wellbeing today?\nWhat did you do together?\nAnything to follow up?';

/** A group of common items, with `items`, unticked, picked or not as the template copies it. */
function group(id: string, name: string, items: readonly DraftItem[], isPicked = false): DraftGroup {
  return { id, name, isEveryNote: false, isPicked, items: [...items] };
}

const medication = goal('c1a4e7b2-3f58-4d19-8b60-9e2d7a5c3f81', 'Medication prompted');
const mealPrepared = goal('d6b3f8a1-4e29-4c70-9a15-0f7c2e8b4d63', 'Meal prepared');
const trains = goal('e8c5a9d3-6b41-4f02-b7e8-1a3d9c6f2b74', 'Travelled by bus or train');
const purchases = goal('f2d7b1e4-8a63-4d95-a0c9-3b5e1f7a8c26', 'Paid for own purchases');
const showered = goal('a9e4c6f2-1d75-4b38-8c03-7d2b5a1e9f40', 'Showered');

/** Every note, with its items unticked, which is never picked. */
const everyNote: DraftGroup = {
  id: '4b7d1e9a-2c63-4f85-a8b0-6e3f9c1d5a72',
  name: 'Every note',
  isEveryNote: true,
  isPicked: false,
  items: [medication, mealPrepared],
};
const outing = group('7c2a8e5d-9b14-4d63-a1f7-4e0b6d3c8a95', 'Community outing', [trains, purchases]);
const personalCare = group('b3f6d9a2-5e81-4c47-9d20-8a1c7e4b6f53', 'Personal care', [showered]);

/**
 * A new note's template, as `GET {base}/draft` gives it, with `goals`, unticked, `commonItemGroups` and no flag. A
 * group's `isPicked` is what the template copies.
 */
function template(
  goals: readonly DraftItem[] = [breakfast, bus, sister],
  guidePrompts = prompts,
  commonItemGroups: readonly DraftGroup[] = [],
): Draft {
  return {
    baseVersion: 0,
    isSaved: false,
    listsVersion: 'lists-1',
    guidePrompts,
    goals: [...goals],
    commonItemGroups: [...commonItemGroups],
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
    commonItemGroups: [],
    narrative,
    isFlagged: false,
    flagReason: null,
    savedAtUtc: '2026-09-30T23:42:00Z',
  };
}

const nbsp = ' ';

/**
 * The paths of `participant`'s header, of their note for `noteDate`, `{base}`, and of its form, versions and discard,
 * and of its page.
 */
function paths(participant: ParticipantHeader, noteDate = '2026-10-01') {
  return {
    header: `/api/participants/${participant.id}`,
    note: `/api/participants/${participant.id}/notes/${noteDate}`,
    draft: `/api/participants/${participant.id}/notes/${noteDate}/draft`,
    versions: `/api/participants/${participant.id}/notes/${noteDate}/versions`,
    discard: `/api/participants/${participant.id}/notes/${noteDate}/discard`,
    page: `/participants/${participant.id}/notes/${noteDate}`,
  };
}

/** A participant on the Today list, with no note today. */
function onToday({ id, givenName, familyName }: ParticipantHeader): TodayParticipant {
  return { id, givenName, familyName, note: null };
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

/** `GET {base}`'s `404` for no live note, which the page opens the form's read for, as it does for a draft. */
const noNote = problem(404);

/** `GET {base}`'s answer for the worker's own draft, started at 9:05 am, whose form the form's own read gives. */
const aDraft = json({
  status: 'Draft',
  authorDisplayName: 'Casey Nguyen',
  startedAtUtc: '2026-09-30T23:05:00Z',
} satisfies NoteAnswer);

/**
 * Answers Jane's header and Alex P.'s draft of her note for `noteDate`, started at `startedAtUtc`: `GET {base}` with
 * its header, and with `current`, the working copy that a manager alone gets, if given; and the form's read refused,
 * as only its author may open it.
 */
function serveAlexsDraft(startedAtUtc: string, noteDate = '2026-10-01', current?: DraftRead) {
  serveNote(jane, startedBy(startedAtUtc), noteDate);
  const header: DraftNote = { status: 'Draft', authorDisplayName: 'Alex P.', startedAtUtc };
  serve(paths(jane, noteDate).note, json(current === undefined ? header : { ...header, current }));
}

/**
 * Answers `participant`'s header, their note for `noteDate` as having none yet, which opens the form's read for a
 * draft alike, and the form's read with `draft`.
 */
function serveNote(participant: ParticipantHeader, draft: Answer = json(template()), noteDate = '2026-10-01') {
  const { header, note, draft: draftPath } = paths(participant, noteDate);
  serve(header, json(participant));
  serve(note, noNote);
  serve(draftPath, draft);
}

/** Every note and Community outing as a saved note shows them: the picked group, every item ticked or not. */
const shownGroups: ReadGroup[] = [
  {
    name: everyNote.name,
    items: [
      { text: medication.text, isTicked: true },
      { text: mealPrepared.text, isTicked: false },
    ],
  },
  {
    name: outing.name,
    items: [
      { text: trains.text, isTicked: true },
      { text: purchases.text, isTicked: false },
    ],
  },
];

type SubmittedOptions = {
  narrative?: string;
  goals?: TickedItem[];
  /** The groups the note shows, as the API gives them; none unless given. */
  commonItemGroups?: ReadGroup[];
  /** The flag's reason, for a note flagged for a manager; none for one that is not. */
  reason?: string;
  firstSubmittedAtUtc?: string;
  /** Who saved the current version, and when, for a note edited since it was submitted; none for one that was not. */
  lastEdit?: { by: string; atUtc: string };
};

/**
 * Jane's note as `GET {base}` gives it once submitted: written by Priya Nair, another worker, at 4:42 pm in Melbourne
 * on 1 October 2026 unless given another time, with Breakfast and Sister ticked, and not flagged unless given a reason.
 * Given a last edit it is at version 2, else at version 1.
 */
function submittedNote({
  narrative = 'Jane made toast.\nThen a walk to the shops.',
  goals = [
    { text: breakfast.text, isTicked: true },
    { text: bus.text, isTicked: false },
    { text: sister.text, isTicked: true },
  ],
  commonItemGroups = [],
  reason,
  firstSubmittedAtUtc = '2026-10-01T06:42:00Z',
  lastEdit,
}: SubmittedOptions = {}): SubmittedNote {
  const flag =
    reason === undefined
      ? ({ isFlagged: false, flagReason: null } as const)
      : ({ isFlagged: true, flagReason: reason } as const);
  return {
    status: 'Submitted',
    author: { id: 'b7a1c0de-0000-4000-8000-000000000001', displayName: 'Priya Nair' },
    firstSubmittedAtUtc,
    current: { versionNumber: lastEdit === undefined ? 1 : 2, narrative, goals, commonItemGroups, ...flag },
    ...(lastEdit === undefined ? { isEdited: false } : { isEdited: true, lastEdit }),
  };
}

/** Answers `participant`'s header and their note for `noteDate` as `note`, submitted, whose form is never read. */
function serveSubmitted(
  participant: ParticipantHeader,
  note: SubmittedNote = submittedNote(),
  noteDate = '2026-10-01',
) {
  const { header, note: notePath } = paths(participant, noteDate);
  serve(header, json(participant));
  serve(notePath, json(note));
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

/** The top bar in the slot: the static bar, or the form's sticky one. */
function topBar(): HTMLElement {
  const bar = pageBar().firstElementChild;
  if (!(bar instanceof HTMLElement)) {
    throw new Error('The slot holds no top bar.');
  }

  return bar;
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
  return shown(saveStatus());
}

/** The text that `element` shows, without what only screen readers get. */
function shown(element: HTMLElement): string {
  const copy = element.cloneNode(true) as HTMLElement;
  copy.querySelectorAll('.visually-hidden').forEach((hidden) => {
    hidden.remove();
  });
  return copy.textContent;
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

/** The save indicator's sentence once a failed save is being retried (design.md §4.3). */
const notSavedRetrying = 'Not saved: no connection. Keep this page open; retrying.';

/** The browser going offline or online, with the event that it fires. */
function goOnline(online: boolean) {
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'));
  });
}

/** Counts the changes to `element` and what it holds from now on, as its words being written again would be one. */
function watchChanges(element: HTMLElement): () => number {
  let changes = 0;
  const observer = new MutationObserver((records) => {
    changes += records.length;
  });
  observer.observe(element, { attributes: true, characterData: true, childList: true, subtree: true });
  return () => changes + observer.takeRecords().length;
}

/** Every text written into the live region `region` from now on, in turn, each of which a screen reader says. */
function watchWords(region: HTMLElement): () => string[] {
  const words: string[] = [];
  const take = (records: MutationRecord[]) => {
    for (const record of records) {
      record.addedNodes.forEach((node) => {
        words.push(node.textContent ?? '');
      });
    }
  };
  const observer = new MutationObserver(take);
  observer.observe(region, { childList: true, subtree: true });
  return () => {
    take(observer.takeRecords());
    return words;
  };
}

/** A save refused as another page has saved the note since this page last did (design.md §5.6). */
const takenOver = problem(409, { code: 'draft.taken_over' });

/** Whether the page asks before it goes, as a reload or a closed tab would find. */
function unloadAsks(): boolean {
  const event = new Event('beforeunload', { cancelable: true });
  window.dispatchEvent(event);
  return event.defaultPrevented;
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
  // No unfinished drafts on Today, for a test that goes there.
  serve(draftsPath, json([]));
  serve(antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
  scrollPage(0);
});

afterEach(() => {
  onlineManager.setOnline(true);
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('The note form while it loads', () => {
  it('shows the back link to Today in a static top bar at once, with no save status, and nothing of the note', () => {
    serve(paths(jane).header, noAnswer);
    serve(paths(jane).note, noAnswer);
    renderApp(paths(jane).page);

    const back = within(pageBar()).getByRole('link', { name: 'Today' });
    expect(back).toHaveAttribute('href', '/');
    expect(topBar()).not.toHaveStyle({ position: 'sticky' });
    expect(pageBar().querySelector('#save-status')).toBeNull();
    expect(showsAnyOfTheNote()).toBe(false);
    expect(document.title).toBe('Grow2Notes – Note');
  });

  // The form's read waits for the note's, which says that the form is what to show.
  it.each(['header', 'note', 'draft'] as const)(
    'shows no identity, tick box or text box while the %s has not answered and the others have',
    async (waiting) => {
      const answers = { header: json(jane), note: noNote, draft: json(template()) };
      const late = held(answers[waiting]);
      for (const read of ['header', 'note', 'draft'] as const) {
        serve(paths(jane)[read], read === waiting ? late.answer : answers[read]);
      }
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
    serve(paths(jane).note, noAnswer);
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

  // note-form.md component 5: a plain link on its own line under the <h1>, outside the form, before Goals.
  it("has the Past notes link under the <h1>, before Goals, to the participant's Past notes", async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    const link = within(main()).getByRole('link', { name: 'Past notes' });
    expect(link).toHaveAttribute('href', `/participants/${jane.id}/notes`);
    expect(link.closest('form')).toBeNull();
    expect(follows(link, pageHeading())).toBe(true);
    expect(follows(within(main()).getByRole('group', { name: '1. Goals' }), link)).toBe(true);
    expect(within(main()).getAllByRole('link')).toEqual([link]);
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

  // The top bar's save status, in place before the form's first words are written into it (autosave-status.md
  // *Behaviour* 8), and the bar sticky, as it is only over the form (note-form.md component 1).
  it('makes the top bar sticky, with the save status after the back link, empty for a new note', async () => {
    serveNote(jane);
    renderApp(paths(jane).page);
    await settle();

    expect(topBar()).toHaveStyle({ position: 'sticky' });
    const status = saveStatus();
    expect(status.tagName).toBe('P');
    expect(status).toHaveAttribute('role', 'status');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status).toHaveAttribute('aria-atomic', 'true');
    expect(status).toBeEmptyDOMElement();
    expect(follows(status, within(pageBar()).getByRole('link', { name: 'Today' }))).toBe(true);
  });

  // As the shell fetches every query again after signing in again in place (app-shell.md *Sign in again, same person*),
  // even once the note has been submitted elsewhere.
  it('keeps its ticks and text, and the form, when the note is fetched again, whatever comes', async () => {
    serveNote(jane);
    const { client } = renderApp(paths(jane).page);
    await settle();
    fireEvent.click(within(main()).getByText(bus.text));
    fireEvent.input(guidedNotes(), { target: { value: 'Jane made toast.' } });
    serve(paths(jane).note, json(submittedNote()));
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
    serve(paths(jane).note, noNote);
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
    serve(paths(jane).note, noNote);
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

// Someone else's draft at the shared note address (design.md §4.2, §4.4; participant-notes.md view C, items 27 and 28,
// *Someone else's draft, worker*, *Someone else's draft, manager* and *Discard dialog*, and *Focus order*).

/** Alex P.'s working copy of Jane's note, as a manager reads it: Breakfast and Sister ticked, flagged with a reason. */
function alexsWorkingCopy(change: Partial<DraftRead> = {}): DraftRead {
  return {
    narrative: 'Jane made toast.\nThen a walk to the shops.',
    goals: [
      { text: breakfast.text, isTicked: true },
      { text: bus.text, isTicked: false },
      { text: sister.text, isTicked: true },
    ],
    commonItemGroups: [],
    isFlagged: true,
    flagReason: 'Mentioned pain in her left knee\nafter the walk.',
    ...change,
  };
}

type AlexsDraftOptions = {
  noteDate?: string;
  /** 9:14 am in Melbourne on 1 October unless given. */
  startedAtUtc?: string;
  /** How `POST {base}/discard` is answered. */
  discard?: Answer;
};

/**
 * Opens Alex P.'s draft of Jane's note for `noteDate` from Today, as `me`, `GET {base}` holding `current` if it is
 * given, as for a manager, and gives the router and the query client.
 */
async function openAlexsDraft(
  me: Me,
  current?: DraftRead,
  { noteDate = '2026-10-01', startedAtUtc = '2026-09-30T23:14:00Z', discard = discarded }: AlexsDraftOptions = {},
) {
  serveAlexsDraft(startedAtUtc, noteDate, current);
  answers.set(`POST ${paths(jane, noteDate).discard}`, discard);
  serve(todayPath, json({ date: '2026-10-01', participants: [onToday(jane)] } satisfies TodayList));
  const opened = renderApp('/', me);
  await settle();
  await act(() => opened.router.navigate(paths(jane, noteDate).page));
  await settle();
  return opened;
}

/** The sentence that says who started the draft and when. */
function startedSentence(): HTMLElement {
  return within(main()).getByText(/started/);
}

/** A manager's Discard draft, on someone else's draft. */
function managersDiscard(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Discard draft' });
}

/** The read-only flag row of a draft that a manager reads. */
function flagRow(): HTMLElement {
  const row = main().querySelector<HTMLElement>(`p.${tickList.readRow}`);
  if (row === null) {
    throw new Error('The page has no read-only flag row.');
  }

  return row;
}

/** The elements that Tab stops on in the top bar and `<main>`, in the document's order. */
function tabStops(): HTMLElement[] {
  const focusable = 'a[href], button, input, select, textarea, [tabindex]';
  return [pageBar(), main()]
    .flatMap((part) => [...part.querySelectorAll<HTMLElement>(focusable)])
    .filter((element) => element.tabIndex >= 0 && element.closest('[hidden]') === null);
}

describe("Someone else's draft", () => {
  it("says who started today's note and when, focusing the <h1>, with no form", async () => {
    // 9:14 am in Melbourne on 1 October.
    serveAlexsDraft('2026-09-30T23:14:00Z');
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
    await openAlexsDraft(worker, undefined, { noteDate: '2026-09-30', startedAtUtc: '2026-09-29T23:14:00Z' });

    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Wednesday 30${nbsp}September 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(startedSentence().textContent).toBe(
      `Alex P. started the note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(within(main()).queryByRole('textbox')).toBeNull();
  });

  it('gives a worker the <h1>, the sentence and Past notes alone: no Discard draft, text, goal or flag', async () => {
    await openAlexsDraft(worker);

    const link = within(main()).getByRole('link', { name: 'Past notes' });
    expect(link).toHaveAttribute('href', `/participants/${jane.id}/notes`);
    expect(follows(link, startedSentence())).toBe(true);
    expect(within(main()).getAllByRole('link').map((each) => each.textContent)).toEqual(['Past notes']);
    expect(within(main()).queryAllByRole('button')).toEqual([]);
    expect(within(main()).queryAllByRole('heading', { level: 2 })).toEqual([]);
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main().querySelector(`form, input, textarea, .${tickList.icon}`)).toBeNull();
    expect(main()).not.toHaveTextContent(/Discard|Goals|Guided notes|Flag|Reason|toast|Nothing written/);
    expect(tabStops().map((stop) => stop.textContent)).toEqual(['Today', 'Past notes']);
  });
});

describe("Someone else's draft, read by a manager", () => {
  it('shows the sentence, Discard draft, Past notes, the goals ticked in order, the text, then the flag', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy());

    expect(pageHeading()).toHaveFocus();
    expect(startedSentence().textContent).toBe(
      `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
    const discard = managersDiscard();
    expect(discard).toHaveAttribute('type', 'button');
    expect(discard).toHaveClass(buttonStyles.secondary);
    expect(follows(discard, startedSentence())).toBe(true);
    const link = within(main()).getByRole('link', { name: 'Past notes' });
    expect(follows(link, discard)).toBe(true);
    const goals = section('1. Goals');
    expect(follows(goals, link)).toBe(true);
    expect(within(goals).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Makes own breakfast, ticked',
      'Catches bus to day program, not ticked',
      'Phones her sister, ticked',
    ]);
    expect(narrative().textContent).toBe('Jane made toast.\nThen a walk to the shops.');
    expect(follows(narrative(), goals)).toBe(true);
    expect(within(main()).getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      '1. Goals',
      '2. Common items',
      '3. Guided notes',
    ]);
    expect(main().querySelector('form, input, textarea')).toBeNull();
  });

  it('shows "2. Common items" with the groups it is given, each item ticked or not, no question', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy({ commonItemGroups: shownGroups }));

    const common = section('2. Common items');
    expect(follows(common, section('1. Goals'))).toBe(true);
    expect(follows(section('3. Guided notes'), common)).toBe(true);
    expect(within(common).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      'Every note',
      'Community outing',
    ]);
    expect(within(common).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Medication prompted, ticked',
      'Meal prepared, not ticked',
      'Travelled by bus or train, ticked',
      'Paid for own purchases, not ticked',
    ]);
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(main()).not.toHaveTextContent('Which of these happened?');
  });

  it('says "No common items set" under "2. Common items" for a draft whose working copy shows none', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy());

    const common = section('2. Common items');
    expect(within(common).getByText('No common items set').tagName).toBe('P');
    expect(within(common).queryByRole('list')).toBeNull();
  });

  it('shows a ticked flag as the read-only "Flag for manager" row with its reason, never the Flagged tag', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy());

    const row = flagRow();
    expect(row.textContent).toBe('Flag for manager, ticked');
    expect(shown(row)).toBe('Flag for manager');
    expect(row.querySelector('svg path')).not.toBeNull();
    expect(row.closest(`.${readView.section}`)).not.toBeNull();
    expect(follows(row, narrative())).toBe(true);
    const reason = within(main()).getByText(/^Reason:/);
    expect(reason.textContent).toBe('Reason: Mentioned pain in her left knee\nafter the walk.');
    expect(reason).toHaveClass(readView.written);
    expect(follows(reason, row)).toBe(true);
    expect(main().querySelector(`.${tagStyles.tag}`)).toBeNull();
    expect(main()).not.toHaveTextContent('Flagged');
    expect(within(main()).queryByRole('checkbox')).toBeNull();
  });

  it('shows an unflagged draft\'s flag row as not ticked, with no Reason', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy({ isFlagged: false, flagReason: null }));

    expect(flagRow().textContent).toBe('Flag for manager, not ticked');
    expect(flagRow().querySelector('svg path')).toBeNull();
    expect(main()).not.toHaveTextContent(/Reason|Flagged/);
  });

  it.each(['', ' \n '])('says "Nothing written yet." under Guided notes for a text of %j', async (text) => {
    await openAlexsDraft(manager, alexsWorkingCopy({ narrative: text }));

    const guided = section('3. Guided notes');
    expect(within(guided).getByText('Nothing written yet.').tagName).toBe('P');
    expect(guided.querySelector(`div.${readView.written}`)).toBeNull();
  });

  it('shows the sentence and Past notes alone for a draft started between the note and form reads', async () => {
    serveNote(jane, startedBy('2026-09-30T23:14:00Z'));
    serve(todayPath, json({ date: '2026-10-01', participants: [onToday(jane)] } satisfies TodayList));
    const { router } = renderApp('/', manager);
    await settle();

    await act(() => router.navigate(paths(jane).page));
    await settle();

    expect(startedSentence()).toBeInTheDocument();
    expect(tabStops().map((stop) => stop.textContent)).toEqual(['Today', 'Past notes']);
    expect(within(main()).queryAllByRole('heading', { level: 2 })).toEqual([]);
  });

  it('has the back link, Discard draft and Past notes as its Tab stops, in that order', async () => {
    await openAlexsDraft(manager, alexsWorkingCopy());

    expect(tabStops().map((stop) => stop.textContent)).toEqual(['Today', 'Discard draft', 'Past notes']);
  });

  it('is "Grow2Notes – Note", with no name or text in the address or history.state', async () => {
    const { router } = await openAlexsDraft(manager, alexsWorkingCopy());

    expect(document.title).toBe('Grow2Notes – Note');
    const kept = /Jane|Citizen|Alex|toast|knee/;
    expect(JSON.stringify(router.state.location)).not.toMatch(kept);
    expect(JSON.stringify(window.history.state)).not.toMatch(kept);
    expect(window.location.href).not.toMatch(kept);
  });
});

describe("A manager's Discard of someone else's draft", () => {
  it("opens the dialog on its title, saying who started today's draft and at what time", async () => {
    await openAlexsDraft(manager, alexsWorkingCopy());

    await press(managersDiscard());

    const dialog = openConfirmation();
    expect(dialog).toHaveAccessibleName('Discard the draft note for Jane Citizen?');
    expect(within(dialog).getByRole('heading', { level: 2 })).toHaveFocus();
    expect(dialog).toHaveAccessibleDescription(
      `Alex P. started it at 9:14${nbsp}am. Nobody will be able to open this draft again.`,
    );
    expect(dialogButtons()).toEqual(['Discard draft for Jane Citizen', 'Go back']);
    expect(dialogButton('Discard draft for Jane Citizen')).toHaveClass(buttonStyles.warning);
    expect(dialogButton('Go back')).toHaveClass(buttonStyles.secondary);
    expect(discards()).toEqual([]);
  });

  it("says an earlier day's date first, then who started it and at what time", async () => {
    // 9:14 am in Melbourne on 30 September.
    await openAlexsDraft(manager, alexsWorkingCopy(), {
      noteDate: '2026-09-30',
      startedAtUtc: '2026-09-29T23:14:00Z',
    });

    await press(managersDiscard());

    expect(openConfirmation()).toHaveAccessibleDescription(
      `This draft is for Wednesday 30${nbsp}September 2026. Alex P. started it at 9:14${nbsp}am. `
        + 'Nobody will be able to open this draft again.',
    );
  });

  it.each([
    { how: 'Go back', close: () => press(dialogButton('Go back')) },
    {
      how: 'a plain close(), as Escape and Back give',
      close: async () => {
        openConfirmation().closest('dialog')!.close();
        await settle();
      },
    },
  ])('closes on $how with nothing sent, focus back on Discard draft', async ({ close }) => {
    await openAlexsDraft(manager, alexsWorkingCopy());
    await press(managersDiscard());

    await close();
    await settle();

    expect(confirmation()).toBeNull();
    expect(managersDiscard()).toHaveFocus();
    expect(discards()).toEqual([]);
  });

  it("puts Today in the draft's place, its <h1> focused, with its lists fetched afresh", async () => {
    const drafted: TodayNote = {
      authorDisplayName: 'Alex P.',
      startedAtUtc: '2026-09-30T23:14:00Z',
      isFlagged: false,
      status: 'Draft',
      isMine: false,
    };
    serveAlexsDraft('2026-09-30T23:14:00Z', '2026-10-01', alexsWorkingCopy());
    answers.set(`POST ${paths(jane).discard}`, discarded);
    const row = onToday(jane);
    serve(todayPath, json({ date: '2026-10-01', participants: [{ ...row, note: drafted }] } satisfies TodayList));
    const { router, client } = renderApp('/', manager);
    await settle();
    await press(rowLink('Jane Citizen'));
    await settle();
    // Kept from earlier visits: Today's lists, from the one just left, and the participant's Past notes.
    expect(client.getQueryData(['me', 'drafts'])).toEqual([]);
    client.setQueryData(['pastNotes', jane.id], { pages: [{ notes: [], hasMore: false }], pageParams: [null] });
    await press(managersDiscard());
    const fresh = held(json({ date: '2026-10-01', participants: [row] } satisfies TodayList));
    const freshDrafts = held(json([]));
    serve(todayPath, fresh.answer);
    serve(draftsPath, freshDrafts.answer);

    await confirmDiscard();
    await settle();

    expect(discards()).toEqual([`POST ${paths(jane).discard}`]);
    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Today');
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(client.getQueryData(['me', 'drafts'])).toBeUndefined();
    const sent = sentRequests();
    expect(sent.lastIndexOf(`GET ${draftsPath}`)).toBeGreaterThan(sent.indexOf(`POST ${paths(jane).discard}`));
    expect(client.getQueryState(['pastNotes', jane.id])?.isInvalidated).toBe(true);
    fresh.release();
    freshDrafts.release();
    await settle();
    expect(rowLink('Jane Citizen')).toBeInTheDocument();
    expect(main()).not.toHaveTextContent(/Draft|Alex P\./);
    expect(pageHeading()).toHaveFocus();
  });

  it('takes its confirming button away for a draft no longer a draft, Go back then going to Today', async () => {
    const { router } = await openAlexsDraft(manager, alexsWorkingCopy(), {
      discard: problem(409, { code: 'note.not_a_draft' }),
    });
    await press(managersDiscard());

    await confirmDiscard();

    expect(within(openConfirmation()).getByRole('alert').textContent).toBe(
      'This draft has already been submitted or discarded.',
    );
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();

    await press(dialogButton('Go back'));
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveFocus();
    expect(discards()).toHaveLength(1);
  });

  it('says why above Discard draft when its request fails after a browser closed the dialog', async () => {
    const post = held(noConnection);
    const { router } = await openAlexsDraft(manager, alexsWorkingCopy(), { discard: post.answer });
    const alert = managersDiscard().previousElementSibling;
    expect(alert).toHaveAttribute('role', 'alert');
    expect(alert).toBeEmptyDOMElement();
    await press(managersDiscard());
    await press(dialogButton('Discard draft for Jane Citizen'));
    // A repeated Escape that a browser lets through while it is busy (confirm-dialog.md *Behaviour* 7).
    openConfirmation().closest('dialog')!.close();
    await settle();

    post.release();
    await settle();

    expect(alert?.textContent).toBe('Not discarded: no connection. Try again.');
    expect(managersDiscard()).toHaveFocus();
    expect(router.state.location.pathname).toBe(paths(jane).page);

    await press(managersDiscard());

    expect(alert).toBeEmptyDOMElement();
    expect(openConfirmation()).toBeInTheDocument();
  });

  it('closes as Go back as the 28-minute warning opens, nothing sent, focus on Discard draft after Stay', async () => {
    const { opened } = focusLikeABrowser();
    await openAlexsDraft(manager, alexsWorkingCopy());
    await press(managersDiscard());
    const dialog = openConfirmation();

    await wait(28 * 60_000);

    const warning = screen.getByRole<HTMLDialogElement>('dialog', { name: 'You’ll be signed out in 2 minutes' });
    expect(opened.mock.contexts.at(-1)).toBe(warning);
    expect(dialog).not.toHaveAttribute('open');
    expect(confirmation()).toBeNull();
    answers.set('POST /api/auth/ping', () => Promise.resolve(new Response(null, { status: 204 })));

    await press(within(warning).getByRole('button', { name: 'Stay signed in' }));
    await settle();

    expect(managersDiscard()).toHaveFocus();
    expect(discards()).toEqual([]);
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
            pickedGroupIds: [],
            tickedCommonItemIds: [],
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
      expect(pageBar().querySelector('#save-status')).toBeNull();

      draft.release();
      await settle();

      expect(shownSaveStatus()).toBe(`Draft${nbsp}· saved 9:42${nbsp}am`);
      expect(saveStatus().textContent).toBe(`Draft${nbsp}·, saved 9:42${nbsp}am`);
      // Put in the page with its words, as there is none while loading, rather than words written into one there.
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
          pickedGroupIds: [],
          tickedCommonItemIds: [],
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

    expect(shownSaveStatus()).toBe(`Draft${nbsp}· saved Wed 30${nbsp}Sep 2026, 9:42${nbsp}am`);
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
    serve(paths(participant).note, aDraft);
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
      pickedGroupIds: [],
      tickedCommonItemIds: [],
      isFlagged: false,
      flagReason: null,
    });
  });
});

describe('A save that fails', () => {
  it('keeps "Saving…" through one failure, then says "Not saved: …" once in the top bar, until saved', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, noConnection);
    renderApp(paths(participant).page);
    await settle();

    type('Jane made toast.');
    await wait(2_000);
    expect(saves()).toHaveLength(1);
    await wait(999);
    expect(shownSaveStatus()).toBe('Saving…');

    await wait(1);
    expect(saves()).toHaveLength(2);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    // The indicator itself, the top bar's live region after the back link, with an icon that screen readers skip.
    expect(saveStatus()).toHaveAttribute('role', 'status');
    expect(saveStatus().textContent).toBe(notSavedRetrying);
    expect(saveStatus().querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(follows(saveStatus(), within(topBar()).getByRole('link', { name: 'Today' }))).toBe(true);

    const changes = watchChanges(saveStatus());
    await wait(2_000 + 4_000 + 8_000);
    expect(saves()).toHaveLength(5);
    expect(changes()).toBe(0);

    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    await wait(10_000);
    await settle();
    expect(saves().map(({ body }) => [body.seq, body.narrative])).toEqual(
      [1, 2, 3, 4, 5, 6].map((seq) => [seq, 'Jane made toast.']),
    );
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(saveStatus().querySelector('svg')).toBeNull();
  });

  it('says "Not saved: …" at once as the browser goes offline, sending nothing, then "Saved …" online', async () => {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    renderApp(paths(participant).page);
    await settle();
    type('Jane made toast.');

    goOnline(false);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    await wait(30_000);
    expect(saves()).toEqual([]);

    goOnline(true);
    await settle();
    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast.']);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
  });
});

describe('Changed on another device or tab', () => {
  const sentence = 'This note was changed on another device or tab.';

  /** The text, ticks, flag and reason on screen as the note is taken over. */
  const onScreen = {
    narrative: 'Jane made toast. Then a walk.',
    tickedGoalIds: [breakfast.id, bus.id, sister.id],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: true,
    flagReason: 'Seemed tired.',
  };

  /** The version that another page saved, at 9:50 am, which Load the other version reads. */
  const otherVersion: Draft = {
    ...savedDraft('Jane went to the library.'),
    goals: [breakfast, { ...bus, isTicked: true }, sister],
    isFlagged: true,
    flagReason: 'Seemed tired after lunch.',
    savedAtUtc: '2026-09-30T23:50:00Z',
  };

  /**
   * Opens the worker's draft of a participant of their own, whose first save from this page is answered at 9:43 am, its
   * second `409 draft.taken_over`, and those after it at 9:44 am; and writes "Jane made toast.", which saves.
   */
  async function openToLose(): Promise<ParticipantHeader> {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft('Jane')));
    serveSave(
      paths(participant).draft,
      inTurn(saved('2026-09-30T23:43:00Z'), takenOver, saved('2026-09-30T23:44:00Z')),
    );
    renderApp(paths(participant).page);
    await settle();
    await write('Jane made toast.');
    return participant;
  }

  /**
   * Opens the note as `openToLose()` does; then, with Bus ticked, the flag ticked with a reason and the text written
   * on, focus left in the box, saves again, which meets the takeover.
   */
  async function loseTheNote(): Promise<ParticipantHeader> {
    const participant = await openToLose();

    fireEvent.click(within(main()).getByText(bus.text));
    fireEvent.click(flagBox());
    typeReason(onScreen.flagReason);
    guidedNotes().focus();
    await write(onScreen.narrative);
    return participant;
  }

  /** The takeover banner in the top bar, if it shows. */
  function takeoverBanner(): HTMLElement | null {
    const banner = within(pageBar()).queryByRole('region', { name: sentence });
    return banner !== null && within(banner).queryAllByRole('button').length > 0 ? banner : null;
  }

  /** A choice of the takeover banner, by its name. */
  function choice(name: 'Keep the text on this screen' | 'Load the other version'): HTMLElement {
    return within(takeoverBanner()!).getByRole('button', { name });
  }

  /** What the form shows: its text, its goals' ticks, its flag and its reason. */
  function shownContent() {
    return [guidedNotes().value, goalBoxes().map((box) => box.checked), flagBox().checked, reasonField().value];
  }

  /** The reads of the note form, `GET {base}/draft`, sent for `participant`. */
  function draftReads(participant: ParticipantHeader): number {
    return sentRequests().filter((request) => request === `GET ${paths(participant).draft}`).length;
  }

  it('shows the takeover banner after the back link, its two choices next in Tab order, with "Not saved"', async () => {
    await loseTheNote();

    expect(saves().map(({ body }) => body.seq)).toEqual([1, 2]);
    const banner = takeoverBanner()!;
    expect(banner.tagName).toBe('SECTION');
    expect(within(banner).getByText(sentence)).toHaveAttribute('id', 'taken-over');
    expect(banner.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(within(banner).queryByRole('status')).toBeNull();
    expect(within(banner).queryByRole('alert')).toBeNull();
    const choices = within(banner).getAllByRole('button');
    expect(choices.map((button) => button.textContent)).toEqual([
      'Keep the text on this screen',
      'Load the other version',
    ]);
    expect(choices[0]).toHaveClass(buttonStyles.primary);
    expect(choices[1]).toHaveClass(buttonStyles.secondary);
    for (const button of choices) {
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('aria-disabled');
    }
    // In the Tab order, the back link, then Keep and Load, then <main>.
    const focusable = [...pageBar().querySelectorAll('a[href], button, input, textarea, [tabindex]')];
    expect(focusable).toEqual([within(topBar()).getByRole('link', { name: 'Today' }), ...choices]);
    expect(follows(main(), choices[1])).toBe(true);
    expect(follows(banner, saveStatus())).toBe(true);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(saveStatus().querySelector('svg')).toBeNull();
    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(submitButton()).toHaveAttribute('aria-describedby', 'save-status taken-over');
    expect(submitButton()).toHaveAccessibleDescription(`Not saved ${sentence}`);
  });

  it('takes the place of "Not saved: …" in the top bar when a save being retried meets the takeover', async () => {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft('Jane')));
    serveSave(
      paths(participant).draft,
      inTurn(saved('2026-09-30T23:43:00Z'), noConnection, noConnection, takenOver),
    );
    renderApp(paths(participant).page);
    await settle();
    await write('Jane made toast.');
    type(onScreen.narrative);
    await wait(2_000 + 1_000);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    expect(saveStatus()).toHaveClass(notePage.attentionRow);

    await wait(2_000);
    await settle();

    expect(saves().map(({ body }) => body.seq)).toEqual([1, 2, 3, 4]);
    expect(takeoverBanner()).not.toBeNull();
    expect(shownSaveStatus()).toBe('Not saved');
    expect(saveStatus().querySelector('svg')).toBeNull();
    expect(saveStatus()).not.toHaveClass(notePage.attentionRow);
    await wait(60_000);
    expect(saves()).toHaveLength(4);
  });

  it('says its sentence once, assertively, focus kept, keeping what is typed meanwhile and sending none', async () => {
    await loseTheNote();

    expect(assertive().textContent).toBe(sentence);
    expect(document.activeElement).toBe(guidedNotes());

    const changes = watchChanges(assertive());
    type('Jane made toast. Then a walk to the shops.');
    fireEvent.click(within(main()).getByText(breakfast.text));
    guidedNotes().blur();
    await wait(30_000);

    expect(saves()).toHaveLength(2);
    expect(changes()).toBe(0);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(guidedNotes()).toHaveValue('Jane made toast. Then a walk to the shops.');
    expect(goalBoxes()[0]).not.toBeChecked();
  });

  it('sends all on screen on Keep, under a new clientId from 1, focus back in the box, then "Saved …"', async () => {
    await loseTheNote();
    type('Jane made toast. Then a walk to the shops.');

    await press(choice('Keep the text on this screen'));

    const [first, , kept] = saves().map(({ body }) => body);
    expect(kept).toEqual({
      ...onScreen,
      clientId: expect.stringMatching(uuid) as string,
      seq: 1,
      narrative: 'Jane made toast. Then a walk to the shops.',
    });
    expect(kept.clientId).not.toBe(first.clientId);
    expect(document.activeElement).toBe(guidedNotes());
    expect(takeoverBanner()).toBeNull();
    expect(assertive()).toBeEmptyDOMElement();
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(submitButton()).not.toHaveAttribute('aria-disabled');

    await write('Jane made toast. Then a walk to the shops and back.');
    expect(saves()[3].body).toMatchObject({ clientId: kept.clientId, seq: 2 });
  });

  it('gives focus to the <h1> on Keep when nothing had it as the note was taken over', async () => {
    await openToLose();
    guidedNotes().focus();
    type('Jane made toast. Then a walk.');
    guidedNotes().blur();
    await settle();
    expect(takeoverBanner()).not.toBeNull();

    await press(choice('Keep the text on this screen'));

    expect(document.activeElement).toBe(pageHeading());
    expect(saves().map(({ body }) => [body.seq, body.narrative])).toEqual([
      [1, 'Jane made toast.'],
      [2, 'Jane made toast. Then a walk.'],
      [1, 'Jane made toast. Then a walk.'],
    ]);
  });

  it("shows the other version's ticks, text, flag and reason on Load, focus on the <h1>, saving from 1", async () => {
    const participant = await loseTheNote();
    const other = held(json(otherVersion));
    serve(paths(participant).draft, other.answer);
    type('Jane made toast. Then a walk to the shops.');

    await press(choice('Load the other version'));
    // Both choices wait for its read.
    expect(choice('Keep the text on this screen')).toHaveAttribute('aria-disabled', 'true');
    expect(choice('Load the other version')).toHaveAttribute('aria-disabled', 'true');
    await press(choice('Keep the text on this screen'));
    expect(saves()).toHaveLength(2);

    other.release();
    await settle();

    expect(shownContent()).toEqual([
      otherVersion.narrative,
      [false, true, false],
      true,
      otherVersion.flagReason,
    ]);
    expect(document.activeElement).toBe(pageHeading());
    expect(takeoverBanner()).toBeNull();
    expect(shownSaveStatus()).toBe(`Saved 9:50${nbsp}am`);
    expect(saveStatus().querySelector('time')).toHaveAttribute('datetime', '2026-09-30T23:50:00Z');
    expect(submitButton()).not.toHaveAttribute('aria-disabled');
    expect(draftReads(participant)).toBe(2);
    expect(saves()).toHaveLength(2);

    await write('Jane went to the library. Then lunch.');
    const [first, , next] = saves().map(({ body }) => body);
    expect(next).toEqual({
      clientId: expect.stringMatching(uuid) as string,
      seq: 1,
      narrative: 'Jane went to the library. Then lunch.',
      tickedGoalIds: [bus.id],
      pickedGroupIds: [],
      tickedCommonItemIds: [],
      isFlagged: true,
      flagReason: 'Seemed tired after lunch.',
    });
    expect(next.clientId).not.toBe(first.clientId);
  });

  it('replaces nothing on screen while the banner shows, nor on Keep, as only Load does (item 16)', async () => {
    const participant = await loseTheNote();
    serve(paths(participant).draft, json(otherVersion));
    const kept = [onScreen.narrative, [true, true, true], true, onScreen.flagReason];

    act(() => {
      window.dispatchEvent(new Event('focus'));
      document.dispatchEvent(new Event('visibilitychange'));
    });
    goOnline(false);
    goOnline(true);
    await wait(60_000);
    expect(shownContent()).toEqual(kept);

    await press(choice('Keep the text on this screen'));
    await wait(30_000);

    expect(shownContent()).toEqual(kept);
    expect(draftReads(participant)).toBe(1);
  });

  it('opens no confirmation when its check save meets the takeover, a press then saying the sentence', async () => {
    const { participant } = await openToSubmit();
    await write();
    serveSave(paths(participant).draft, inTurn(takenOver, saved('2026-09-30T23:44:00Z')));

    await press(submitButton());

    expect(saves()).toHaveLength(2);
    expect(takeoverBanner()).not.toBeNull();
    expect(confirmation()).toBeNull();
    expect(document.activeElement).toBe(submitButton());
    expect(pageStatus()).toBeEmptyDOMElement();

    await press(submitButton());
    await wait(16);
    expect(pageStatus().textContent).toBe(sentence);
    expect(saves()).toHaveLength(2);
    expect(confirmation()).toBeNull();

    await press(choice('Keep the text on this screen'));
    await wait(30_000);
    expect(saves().map(({ body }) => body.seq)).toEqual([1, 2, 1]);
    expect(document.activeElement).toBe(submitButton());
    expect(confirmation()).toBeNull();
  });

  it.each([
    { failure: 'no connection', answer: noConnection, cause: 'no connection' },
    { failure: 'a failure of the server', answer: problem(500), cause: 'something went wrong' },
  ])("keeps the banner, its choices ready, when Load's read meets $failure, saying so politely", async (failing) => {
    const participant = await loseTheNote();
    serve(paths(participant).draft, failing.answer);

    await press(choice('Load the other version'));
    await wait(16);

    expect(pageStatus().textContent).toBe(`The note did not load: ${failing.cause}. Try again.`);
    expect(choice('Keep the text on this screen')).not.toHaveAttribute('aria-disabled');
    expect(choice('Load the other version')).not.toHaveAttribute('aria-disabled');
    expect(shownSaveStatus()).toBe('Not saved');
    expect(shownContent()).toEqual([onScreen.narrative, [true, true, true], true, onScreen.flagReason]);
    expect(saves()).toHaveLength(2);
  });

  it.each([
    {
      deadEnd: 'the version-conflict dead end when it finds no draft, as the note was submitted meanwhile',
      answer: problem(404),
      words: sentence,
    },
    {
      deadEnd: "the in-progress dead end when it finds someone else's draft",
      // 9:14 am on 1 October in Melbourne.
      answer: startedBy('2026-09-30T23:14:00Z'),
      words: `Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`,
    },
  ])("ends at $deadEnd on Load's read, saying it, focus on the <h1>", async ({ answer, words }) => {
    const participant = await loseTheNote();
    serve(paths(participant).draft, answer);
    const changes = watchChanges(assertive());

    await press(choice('Load the other version'));

    const banner = within(pageBar()).getByRole('region', { name: words });
    expect(within(banner).queryByRole('button')).toBeNull();
    expect(takeoverBanner()).toBeNull();
    expect(assertive().textContent).toBe(words);
    // Written again, so said, though the version-conflict dead end's words are the takeover's.
    expect(changes()).toBeGreaterThan(0);
    expect(shownSaveStatus()).toBe('Not saved');
    expect(document.activeElement).toBe(pageHeading());
    expect(submitButton()).toHaveAttribute('aria-describedby', 'save-status dead-end');
    expect(guidedNotes()).toHaveValue(onScreen.narrative);
    type('Jane made toast. Then a walk to the shops.');
    await wait(30_000);
    expect(saves()).toHaveLength(2);
  });

  it("shows a new note's empty form on Load when the note was discarded meanwhile, saving it anew", async () => {
    const participant = await loseTheNote();
    serve(paths(participant).draft, json(template()));

    await press(choice('Load the other version'));

    expect(shownContent()).toEqual(['', [false, false, false], false, '']);
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(takeoverBanner()).toBeNull();
    expect(document.activeElement).toBe(pageHeading());

    await write('Jane made toast.');
    const [first, , next] = saves().map(({ body }) => body);
    expect(next).toMatchObject({ seq: 1, listsVersion: 'lists-1', narrative: 'Jane made toast.' });
    expect(next.clientId).not.toBe(first.clientId);
  });

  it('asks before the page goes while the banner shows, and not once Keep has saved', async () => {
    await openToLose();
    expect(unloadAsks()).toBe(false);

    await write('Jane made toast. Then a walk.');
    expect(takeoverBanner()).not.toBeNull();
    expect(unloadAsks()).toBe(true);

    await press(choice('Keep the text on this screen'));
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(unloadAsks()).toBe(false);
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
  it.each([
    { found: 'no note yet', answer: noNote },
    { found: 'a draft', answer: aDraft },
  ])("sends its three reads alone, the note's before the form's, for $found, and nothing on a tap", async ({
    answer,
  }) => {
    serveNote(jane);
    serve(paths(jane).note, answer);
    renderApp(paths(jane).page);
    await settle();
    // The header and the note twice, as StrictMode mounts the page, unmounts it and mounts it again: the unmount
    // cancels the first request, whose query function takes the abort signal, and the second mount sends it again. A
    // production build sends each once. The form's read once, sent only once the note has answered, by then mounted
    // for good. Counted rather than compared as a set, so that a read sent once more as the form opens fails. Not the
    // antiforgery token, which the form fetches as it opens unless an earlier test left one held (T10's Notes).
    const { header, note, draft } = paths(jane);
    const reads = () => sentRequests().filter((request) => request !== `GET ${antiforgeryPath}`);
    expect(reads().toSorted()).toEqual(
      [`GET ${mePath}`, `GET ${header}`, `GET ${header}`, `GET ${note}`, `GET ${note}`, `GET ${draft}`].toSorted(),
    );
    expect(reads().indexOf(`GET ${draft}`)).toBeGreaterThan(reads().lastIndexOf(`GET ${note}`));
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

/**
 * Submit's answer, `201`, or `200` for a retry whose first answer was lost: version 1, saved at 4:12 pm, and the
 * note's flag status, None unless given another.
 */
function versionSaved(status: 200 | 201 = 201, flagStatus: VersionSaved['flagStatus'] = 'None'): Answer {
  const body = JSON.stringify({
    versionNumber: 1,
    createdAtUtc: '2026-10-01T06:12:00Z',
    flagStatus,
  } satisfies VersionSaved);
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

  it('is aria-disabled while "Not saved: …" shows, described by it', async () => {
    const { participant } = await openToSubmit();
    serveSave(paths(participant).draft, noConnection);

    type('Jane made toast.');
    await wait(3_000);

    expect(submitButton()).toHaveAttribute('aria-disabled', 'true');
    expect(submitButton()).toHaveAccessibleDescription(notSavedRetrying);
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
          pickedGroupIds: [],
          tickedCommonItemIds: [],
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

  it('opens nothing when its check save fails into "Not saved: …", nor once a later save is answered', async () => {
    const { participant } = await openToSubmit();
    await write();
    serveSave(paths(participant).draft, noConnection);

    await press(submitButton());
    expect(saves()).toHaveLength(2);
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(1_000);
    expect(saves()).toHaveLength(3);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    expect(confirmation()).toBeNull();

    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    await wait(2_000);
    await settle();
    expect(saves()).toHaveLength(4);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(confirmation()).toBeNull();
  });

  it('opens nothing for a change typed while its check save is out, nor once a later save is answered', async () => {
    const { participant } = await openToSubmit();
    await write();
    const check = held(saved('2026-09-30T23:43:00Z'));
    serveSave(paths(participant).draft, check.answer);

    await press(submitButton());
    type('Jane made toast. Then a walk.');
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    check.release();
    await settle();
    expect(confirmation()).toBeNull();

    await wait(2_000);
    await settle();
    expect(saves().map(({ body }) => body.narrative)).toEqual([
      'Jane made toast.',
      'Jane made toast.',
      'Jane made toast. Then a walk.',
    ]);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(confirmation()).toBeNull();
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

  it("opens nothing while \"Not saved: …\" shows, saying its sentence again in the page's polite region", async () => {
    const { participant } = await openToSubmit();
    serveSave(paths(participant).draft, noConnection);
    type('Jane made toast.');
    await wait(3_000);
    expect(pageStatus()).toBeEmptyDOMElement();

    await press(submitButton());
    await wait(16);

    expect(pageStatus().textContent).toBe(notSavedRetrying);
    expect(saves()).toHaveLength(2);
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
          pickedGroupIds: [],
          tickedCommonItemIds: [],
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
    { field: 'pickedGroupIds', text: 'Jane made toast.' },
    { field: 'tickedCommonItemIds', text: 'Jane made toast.' },
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
  it('pops back to Today, the banner focused before its <h1>, then both lists anew, the row Submitted', async () => {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft()));
    serveSave(paths(participant).draft, saved('2026-10-01T06:10:00Z'));
    serveSubmit(paths(participant).versions, versionSaved());
    // The worker's own draft, last saved at 9:42 am, then submitted at 4:12 pm.
    const started = { authorDisplayName: 'Casey Nguyen', startedAtUtc: '2026-09-30T23:05:00Z', isFlagged: false };
    const drafted: TodayNote = { ...started, status: 'Draft', isMine: true, savedAtUtc: '2026-09-30T23:42:00Z' };
    const submitted: TodayNote = {
      ...started,
      status: 'Submitted',
      isMine: true,
      firstSubmittedAtUtc: '2026-10-01T06:12:00Z',
    };
    const row = onToday(participant);
    serve(todayPath, json({ date: '2026-10-01', participants: [{ ...row, note: drafted }] } satisfies TodayList));
    const { router, client } = renderApp('/');
    await settle();
    expect(rowLink('Jane Citizen')).toHaveAccessibleDescription(`Draft, You, saved 9:42${nbsp}am`);
    await press(rowLink('Jane Citizen'));
    await settle();
    await write();
    await press(submitButton());
    const fresh = held(json({ date: '2026-10-01', participants: [{ ...row, note: submitted }] } satisfies TodayList));
    const freshDrafts = held(json([]));
    serve(todayPath, fresh.answer);
    serve(draftsPath, freshDrafts.answer);

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
    // No row kept from before the Submit: the rows come with the lists that the server gives now, the day's and the
    // caller's unfinished drafts.
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(main()).not.toHaveTextContent('Draft');
    expect(client.getQueryData(['me', 'drafts'])).toBeUndefined();
    const sent = sentRequests();
    for (const path of [todayPath, draftsPath]) {
      expect(sent.lastIndexOf(`GET ${path}`)).toBeGreaterThan(sent.indexOf(`POST ${paths(participant).versions}`));
    }
    fresh.release();
    await settle();
    // Both, in one render, as Today shows its lists together.
    expect(within(main()).queryByRole('list')).toBeNull();
    freshDrafts.release();
    await settle();
    expect(rowLink('Jane Citizen')).toHaveAccessibleDescription(`Submitted, You, 4:12${nbsp}pm`);
    expect(main()).not.toHaveTextContent('Draft');
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
    serve(paths(participant).note, json(submittedNote()));
    await press(rowLink('Jane Citizen'));
    await settle();
    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(within(main()).getByText(/^Written by/)).toBeInTheDocument();
    await press(within(pageBar()).getByRole('link', { name: 'Today' }));
    await settle();
    expect(router.state.location.pathname).toBe('/');
    expect(banner()).toBeNull();
  });

  // note-form.md *After a successful submit* step 2: a note now in To review changes the Flagged badge, so the form
  // asks for `me` before it leaves, and the shell's own refresh on reaching Today shares that request, held here as a
  // slow network holds it (app-shell.md *Refresh of `/api/auth/me`*).
  it.each([201, 200] as const)(
    'asks for /api/auth/me once, from the form, after a flagged Submit answered %i with To review',
    async (status) => {
      const { participant, router } = await openToSubmit(json(template()), versionSaved(status, 'ToReview'));
      fireEvent.click(flagBox());
      typeReason('Jane seemed unwell after lunch.');
      await write();
      await press(submitButton());
      const me = held(json(worker));
      const sentFrom = meRequestsFrom(router, me.answer);

      await confirmSubmit();
      await settle();
      me.release();
      await settle();

      expect(router.state.location.pathname).toBe('/');
      expect(sentFrom).toEqual([paths(participant).page]);
      const sent = sentRequests();
      expect(sent.slice(sent.indexOf(`POST ${paths(participant).versions}`))).toContain(`GET ${mePath}`);
    },
  );

  it('asks for /api/auth/me only from the shell on reaching Today after an unflagged Submit', async () => {
    const { router } = await openToSubmit();
    await write();
    await press(submitButton());
    const sentFrom = meRequestsFrom(router);

    await confirmSubmit();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(sentFrom).toEqual(['/']);
  });
});

/**
 * Answers each `GET /api/auth/me` from now on with `answer`, and gives where the app was, by its path, as each was
 * sent: on the form, from the form, and on Today, from the shell's refresh on arriving.
 */
function meRequestsFrom(router: Router, answer: Answer = json(worker)): string[] {
  const sentFrom: string[] = [];
  serve(mePath, (init) => {
    sentFrom.push(router.state.location.pathname);
    return answer(init);
  });
  return sentFrom;
}

// More actions and the Discard dialog on the form (note-form.md components 13 and 15, item 33, *States* › *Discard
// dialog*, *Interactions* › *Discard draft* and *Session*, and *Where focus goes*; confirm-dialog.md's dialog 2).

/** `POST {base}/discard`'s `204`. */
const discarded: Answer = () => Promise.resolve(new Response(null, { status: 204 }));

/** Every Discard sent, as its method and path. */
function discards(): string[] {
  return sentRequests().filter((request) => request.startsWith('POST ') && request.endsWith('/discard'));
}

/** More actions, under Submit note, if it shows. */
function moreActions(): HTMLElement | null {
  return within(main()).queryByRole('button', { name: 'More actions' });
}

/** Discard draft, in More actions' panel, found while the panel is hidden too. */
function discardDraft(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Discard draft', hidden: true });
}

/**
 * Opens the worker's own draft of a participant of their own, Jane Citizen by name, dated `noteDate`, at its address,
 * whose saves are answered at 9:44 am and whose Discard gets `discard`, with Today's list to land on, and gives them,
 * the router and the query client.
 */
async function openToDiscard(discard: Answer = discarded, noteDate = '2026-10-01') {
  const participant = freshParticipant();
  serveNote(participant, json(savedDraft()), noteDate);
  serveSave(paths(participant, noteDate).draft, saved('2026-09-30T23:44:00Z'));
  answers.set(`POST ${paths(participant, noteDate).discard}`, discard);
  serve(todayPath, json({ date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList));
  const opened = renderApp(paths(participant, noteDate).page);
  await settle();
  return { participant, ...opened };
}

/** Opens More actions' panel and presses Discard draft in it, which opens the Discard dialog. */
async function openDiscardDialog() {
  await press(moreActions()!);
  await press(discardDraft());
}

/** Presses the Discard dialog's confirming button, and lets its request be answered. */
async function confirmDiscard() {
  await press(dialogButton('Discard draft for Jane Citizen'));
  await settle();
}

describe('More actions', () => {
  it('is not on a new note, and shows once its first save is answered', async () => {
    const participant = freshParticipant();
    const save = held(saved('2026-09-30T23:42:00Z'));
    serveNote(participant);
    serveSave(paths(participant).draft, save.answer);
    renderApp(paths(participant).page);
    await settle();
    expect(moreActions()).toBeNull();

    type('Jane made toast.');
    await wait(2_000);
    expect(shownSaveStatus()).toBe('Saving…');
    expect(moreActions()).toBeNull();

    save.release();
    await settle();
    expect(moreActions()).toBeInTheDocument();
  });

  it('shows on a reopened draft as a disclosure after Submit note, outside the form, its panel hidden', async () => {
    await openToDiscard();

    const button = moreActions()!;
    expect(button).toHaveAttribute('type', 'button');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(button).toHaveClass(buttonStyles.secondary);
    expect(button.closest('form')).toBeNull();
    expect(follows(button, submitButton())).toBe(true);
    const panel = document.getElementById(button.getAttribute('aria-controls')!);
    expect(panel).toHaveAttribute('hidden');
    expect(panel?.contains(discardDraft())).toBe(true);
    expect(follows(panel!, button)).toBe(true);
    expect(within(main()).queryByRole('button', { name: 'Discard draft' })).toBeNull();
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('opens and closes its panel on each press, focus kept on it, Discard draft secondary', async () => {
    await openToDiscard();

    await press(moreActions()!);

    expect(moreActions()).toHaveAttribute('aria-expanded', 'true');
    expect(moreActions()).toHaveFocus();
    expect(within(main()).getByRole('button', { name: 'Discard draft' })).toHaveClass(buttonStyles.secondary);
    expect(confirmation()).toBeNull();

    await press(moreActions()!);

    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(within(main()).queryByRole('button', { name: 'Discard draft' })).toBeNull();
  });

  it.each(['More actions', 'Discard draft'])('closes its panel on Escape on %s, focus on More actions', async (on) => {
    await openToDiscard();
    await press(moreActions()!);
    const focused = within(main()).getByRole('button', { name: on });
    focused.focus();

    fireEvent.keyDown(focused, { key: 'Escape' });

    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(within(main()).queryByRole('button', { name: 'Discard draft' })).toBeNull();
    expect(moreActions()).toHaveFocus();
  });

  it('shows while the takeover banner waits, as the draft is still theirs', async () => {
    const { participant } = await openToDiscard();
    serveSave(paths(participant).draft, inTurn(saved('2026-09-30T23:44:00Z'), takenOver));
    await write('Jane made toast.');

    await write('Jane made toast. Then a walk.');

    const sentence = 'This note was changed on another device or tab.';
    expect(within(pageBar()).getByRole('region', { name: sentence })).toBeInTheDocument();
    expect(moreActions()).toBeInTheDocument();
  });

  describe.each([
    { code: 'note.in_progress_by_other', refusal: startedBy('2026-10-01T00:05:00Z') },
    { code: 'note.version_conflict', refusal: problem(409, { code: 'note.version_conflict' }) },
  ])('once a save of the draft meets $code', ({ refusal }) => {
    it('goes with its open panel, focus on Discard draft going to the <h1>', async () => {
      const { participant } = await openToDiscard();
      serveSave(paths(participant).draft, refusal);
      await press(moreActions()!);
      discardDraft().focus();

      type('Jane made toast.');
      await wait(2_000);
      await settle();

      expect(shownSaveStatus()).toBe('Not saved');
      expect(moreActions()).toBeNull();
      expect(within(main()).queryByRole('button', { name: 'Discard draft', hidden: true })).toBeNull();
      expect(pageHeading()).toHaveFocus();
    });

    it('closes the Discard dialog with nothing sent, focus on the <h1>', async () => {
      const { participant } = await openToDiscard();
      serveSave(paths(participant).draft, refusal);
      type('Jane made toast.');
      await openDiscardDialog();
      expect(confirmation()).not.toBeNull();

      await wait(2_000);
      await settle();

      expect(confirmation()).toBeNull();
      expect(moreActions()).toBeNull();
      expect(pageHeading()).toHaveFocus();
      expect(discards()).toEqual([]);
    });

    it('sends no Discard when the save that a confirming press waits for meets it', async () => {
      const { participant } = await openToDiscard();
      const save = held(refusal);
      serveSave(paths(participant).draft, save.answer);
      await write('Jane made toast.');
      await openDiscardDialog();
      await press(dialogButton('Discard draft for Jane Citizen'));

      save.release();
      await settle();
      await wait(5_000);

      expect(discards()).toEqual([]);
      expect(confirmation()).toBeNull();
      expect(moreActions()).toBeNull();
      expect(pageHeading()).toHaveFocus();
    });
  });
});

describe('The Discard dialog', () => {
  it('asks "Discard the draft note for" the name on its title, saying nobody can open it again', async () => {
    await openToDiscard();

    await openDiscardDialog();

    const dialog = openConfirmation();
    expect(dialog).toHaveAccessibleName('Discard the draft note for Jane Citizen?');
    expect(within(dialog).getByRole('heading', { level: 2 })).toHaveFocus();
    expect(dialog).toHaveAccessibleDescription('Nobody will be able to open this draft again.');
    expect(dialogButtons()).toEqual(['Discard draft for Jane Citizen', 'Go back']);
    expect(dialogButton('Discard draft for Jane Citizen')).toHaveClass(buttonStyles.warning);
    expect(dialogButton('Go back')).toHaveClass(buttonStyles.secondary);
    expect(within(dialog).getByRole('alert')).toBeEmptyDOMElement();
    expect(within(dialog).getByRole('status')).toBeEmptyDOMElement();
    expect(discards()).toEqual([]);
  });

  it("says the date of a draft from an earlier day before the line that always ends it", async () => {
    await openToDiscard(discarded, '2026-09-30');

    await openDiscardDialog();

    expect(openConfirmation()).toHaveAccessibleDescription(
      `This draft is for Wednesday 30${nbsp}September 2026. Nobody will be able to open this draft again.`,
    );
  });

  it('shows "Discarding…" once its request has taken 1 s, both buttons busy, their words kept', async () => {
    await openToDiscard(noAnswer);
    await openDiscardDialog();
    const status = within(openConfirmation()).getByRole('status');

    await press(dialogButton('Discard draft for Jane Citizen'));

    expect(dialogButtons()).toEqual(['Discard draft for Jane Citizen', 'Go back']);
    for (const button of within(openConfirmation()).getAllByRole('button')) {
      expect(button).toHaveAttribute('aria-disabled', 'true');
    }
    await wait(998);
    expect(status).toBeEmptyDOMElement();
    await wait(1);
    expect(status).toHaveTextContent(/^Discarding…$/);
  });

  it('waits for the save out, then sends the Discard, and no save after the confirming press', async () => {
    const { participant, router } = await openToDiscard();
    const save = held(saved('2026-09-30T23:44:00Z'));
    serveSave(paths(participant).draft, save.answer);
    await write('Jane made toast.');
    type('Jane made toast. Then a walk.');
    await openDiscardDialog();

    await press(dialogButton('Discard draft for Jane Citizen'));
    expect(discards()).toEqual([]);

    save.release();
    await settle();
    await wait(5_000);

    const sent = sentRequests();
    expect(discards()).toEqual([`POST ${paths(participant).discard}`]);
    expect(sent.indexOf(`POST ${paths(participant).discard}`)).toBeGreaterThan(
      sent.indexOf(`PUT ${paths(participant).draft}`),
    );
    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast.']);
    // The leave guard lets the move go, though a change was made that is not saved, as the draft has gone.
    expect(router.state.location.pathname).toBe('/');
  });

  it("puts Today in the form's place, its <h1> focused, with no banner, and its lists fetched afresh", async () => {
    const participant = freshParticipant();
    serveNote(participant, json(savedDraft()));
    serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
    answers.set(`POST ${paths(participant).discard}`, discarded);
    const row = onToday(participant);
    const drafted: TodayNote = {
      authorDisplayName: 'Casey Nguyen',
      startedAtUtc: '2026-09-30T23:05:00Z',
      isFlagged: false,
      status: 'Draft',
      isMine: true,
      savedAtUtc: '2026-09-30T23:42:00Z',
    };
    serve(todayPath, json({ date: '2026-10-01', participants: [{ ...row, note: drafted }] } satisfies TodayList));
    const { router, client } = renderApp('/');
    await settle();
    await press(rowLink('Jane Citizen'));
    await settle();
    // Kept from earlier visits: Today's lists, from the one just left, and the participant's Past notes.
    expect(client.getQueryData(['me', 'drafts'])).toEqual([]);
    client.setQueryData(['pastNotes', participant.id], { pages: [{ notes: [], hasMore: false }], pageParams: [null] });
    await openDiscardDialog();
    const fresh = held(json({ date: '2026-10-01', participants: [row] } satisfies TodayList));
    const freshDrafts = held(json([]));
    serve(todayPath, fresh.answer);
    serve(draftsPath, freshDrafts.answer);

    await confirmDiscard();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveFocus();
    expect(main().querySelector('[data-route-focus]')).toBeNull();
    expect(document.title).toBe('Grow2Notes – Today');
    // No row kept from before the Discard: the rows come with the lists that the server gives now.
    expect(within(main()).queryByRole('list')).toBeNull();
    expect(client.getQueryData(['me', 'drafts'])).toBeUndefined();
    const sent = sentRequests();
    expect(sent.lastIndexOf(`GET ${draftsPath}`)).toBeGreaterThan(
      sent.indexOf(`POST ${paths(participant).discard}`),
    );
    expect(client.getQueryState(['pastNotes', participant.id])?.isInvalidated).toBe(true);
    fresh.release();
    freshDrafts.release();
    await settle();
    expect(rowLink('Jane Citizen')).toBeInTheDocument();
    expect(main()).not.toHaveTextContent('Draft');
    expect(pageHeading()).toHaveFocus();
  });

  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])('stays open for "Not discarded: $cause…", focus on its confirming button, ready to try again', async ({
    cause,
    answer,
  }) => {
    const { participant, router } = await openToDiscard(inTurn(answer, discarded));
    await openDiscardDialog();
    const confirm = dialogButton('Discard draft for Jane Citizen');

    await press(confirm);
    await settle();

    expect(within(openConfirmation()).getByRole('alert').textContent).toBe(`Not discarded: ${cause}. Try again.`);
    expect(confirm).toHaveFocus();
    expect(confirm).not.toHaveAttribute('aria-disabled');
    expect(router.state.location.pathname).toBe(paths(participant).page);

    await press(confirm);
    await settle();

    expect(discards()).toHaveLength(2);
    expect(router.state.location.pathname).toBe('/');
  });

  it('takes its confirming button away for a draft no longer a draft, Go back then going to Today', async () => {
    const { router } = await openToDiscard(problem(409, { code: 'note.not_a_draft' }));
    await openDiscardDialog();

    await confirmDiscard();

    expect(within(openConfirmation()).getByRole('alert').textContent).toBe(
      'This draft has already been submitted or discarded.',
    );
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();

    await press(dialogButton('Go back'));
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveFocus();
    expect(discards()).toHaveLength(1);
  });

  it.each([
    { how: 'Go back', close: () => press(dialogButton('Go back')) },
    {
      how: 'a plain close(), as Escape and Back give',
      close: async () => {
        openConfirmation().closest('dialog')!.close();
        await settle();
      },
    },
  ])('closes on $how with nothing sent, its panel closed, focus on More actions', async ({ close }) => {
    await openToDiscard();
    await openDiscardDialog();

    await close();
    await settle();

    expect(confirmation()).toBeNull();
    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(moreActions()).toHaveFocus();
    expect(discards()).toEqual([]);
  });

  it('lets autosave go on after Go back, sending at once a change that a failed Discard held', async () => {
    const { participant } = await openToDiscard(noConnection);
    type('Jane made toast. Then a walk.');
    await openDiscardDialog();
    await confirmDiscard();
    await wait(5_000);
    expect(saves()).toEqual([]);

    await press(dialogButton('Go back'));
    await settle();

    expect(saves().map(({ path, body }) => [path, body.narrative])).toEqual([
      [paths(participant).draft, 'Jane made toast. Then a walk.'],
    ]);
  });

  it('sends no save while its request is out, though a browser closed it, and its failure shows', async () => {
    const post = held(noConnection);
    await openToDiscard(post.answer);
    await openDiscardDialog();
    await press(dialogButton('Discard draft for Jane Citizen'));
    // A repeated Escape that a browser lets through while it is busy (confirm-dialog.md *Behaviour* 7).
    openConfirmation().closest('dialog')!.close();
    await settle();
    type('Jane made toast. Then a walk.');
    await wait(5_000);
    expect(saves()).toEqual([]);

    post.release();
    await settle();

    const alert = submitButton().closest('form')!.querySelector('div[role="alert"]');
    expect(alert?.textContent).toBe('Not discarded: no connection. Try again.');
    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast. Then a walk.']);
  });

  it("keeps Submit's failure above Submit note as it closes on Go back, discarding nothing", async () => {
    const post = held(noConnection);
    await openToSubmit(json(template()), post.answer);
    await write();
    await press(submitButton());
    await press(dialogButton('Submit note for Jane Citizen'));
    openConfirmation().closest('dialog')!.close();
    await settle();
    post.release();
    await settle();
    const alert = submitButton().closest('form')!.querySelector('div[role="alert"]');
    const failure = 'Not submitted: no connection. Your draft is saved. Try again.';
    expect(alert?.textContent).toBe(failure);

    await openDiscardDialog();
    await press(dialogButton('Go back'));
    await settle();

    expect(alert?.textContent).toBe(failure);
    expect(discards()).toEqual([]);
  });

  it('lands on Today as its request succeeds, though a browser closed it meanwhile', async () => {
    const post = held(discarded);
    const { router } = await openToDiscard(post.answer);
    await openDiscardDialog();
    await press(dialogButton('Discard draft for Jane Citizen'));
    openConfirmation().closest('dialog')!.close();
    await settle();

    post.release();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(pageHeading()).toHaveFocus();
  });

  it('closes as Go back as the 28-minute warning opens, nothing sent, focus on More actions after Stay', async () => {
    const { opened } = focusLikeABrowser();
    await openToDiscard();
    await openDiscardDialog();
    const dialog = openConfirmation();

    await wait(28 * 60_000);

    const warning = screen.getByRole<HTMLDialogElement>('dialog', { name: 'You’ll be signed out in 2 minutes' });
    expect(opened.mock.contexts.at(-1)).toBe(warning);
    expect(dialog).not.toHaveAttribute('open');
    expect(confirmation()).toBeNull();
    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    answers.set('POST /api/auth/ping', () => Promise.resolve(new Response(null, { status: 204 })));

    await press(within(warning).getByRole('button', { name: 'Stay signed in' }));
    await settle();

    expect(warning).not.toHaveAttribute('open');
    expect(moreActions()).toHaveFocus();
    expect(discards()).toEqual([]);
    expect(saves()).toEqual([]);
  });

  it('leaves focus where it was moved after a browser closed it, as its failure lets the warning open', async () => {
    focusLikeABrowser();
    const post = held(noConnection);
    await openToDiscard(post.answer);
    await openDiscardDialog();
    await press(dialogButton('Discard draft for Jane Citizen'));
    openConfirmation().closest('dialog')!.close();
    await settle();
    guidedNotes().focus();
    // The request out holds the session clock's phase, so the warning waits for it to settle.
    await wait(28 * 60_000);
    expect(screen.queryByRole('dialog', { name: 'You’ll be signed out in 2 minutes' })).toBeNull();

    post.release();
    await settle();

    const warning = screen.getByRole<HTMLDialogElement>('dialog', { name: 'You’ll be signed out in 2 minutes' });
    answers.set('POST /api/auth/ping', () => Promise.resolve(new Response(null, { status: 204 })));
    await press(within(warning).getByRole('button', { name: 'Stay signed in' }));
    await settle();

    expect(guidedNotes()).toHaveFocus();
    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(discards()).toHaveLength(1);
  });

  it('closes its panel as the warning opens, focus on Discard draft going to More actions after Stay', async () => {
    focusLikeABrowser();
    await openToDiscard();
    await press(moreActions()!);
    discardDraft().focus();

    await wait(28 * 60_000);

    const warning = screen.getByRole<HTMLDialogElement>('dialog', { name: 'You’ll be signed out in 2 minutes' });
    expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
    expect(within(main()).queryByRole('button', { name: 'Discard draft' })).toBeNull();
    answers.set('POST /api/auth/ping', () => Promise.resolve(new Response(null, { status: 204 })));

    await press(within(warning).getByRole('button', { name: 'Stay signed in' }));
    await settle();

    expect(moreActions()).toHaveFocus();
    expect(confirmation()).toBeNull();
    expect(discards()).toEqual([]);
    expect(saves()).toEqual([]);
  });
});

// A draft from an earlier day (note-form.md *Kinds of note* and component 4; design.md §3.4 *Earlier-day drafts* and
// §4.3), opened on Thursday 1 October 2026.

/** The earlier-day notice's words on a draft of Wednesday 30 September 2026. */
const earlierDayWords = `This draft is for Wednesday 30${nbsp}September 2026. Submitting it now keeps that date.`;

/** The earlier-day notice, found by its words, if it shows. */
function earlierDayNotice(): HTMLElement | null {
  return within(main()).queryByText(/^This draft is for /)?.closest<HTMLElement>(`.${noticeStyles.notice}`) ?? null;
}

/** The worker's own draft as `savedDraft()` has it, but last saved at 9:42 am on Wednesday 30 September 2026. */
function savedEarlier(): Draft {
  return { ...savedDraft(), savedAtUtc: '2026-09-29T23:42:00Z' };
}

/**
 * Opens, for `me`, their note dated `noteDate` of a participant of their own, Jane Citizen by name, as `draft` holds
 * it, the worker's own draft last saved on 30 September unless another, with its saves answered at 9:44 am on
 * 1 October, its Submit `201` and Today's list to land on; and gives the participant.
 */
async function openOwnNote(noteDate: string, draft: Draft = savedEarlier(), me: Me = worker) {
  const participant = freshParticipant();
  serveNote(participant, json(draft), noteDate);
  serveSave(paths(participant, noteDate).draft, saved('2026-09-30T23:44:00Z'));
  serveSubmit(paths(participant, noteDate).versions, versionSaved());
  serve(todayPath, json({ date: '2026-10-01', participants: [onToday(participant)] } satisfies TodayList));
  renderApp(paths(participant, noteDate).page, me);
  await settle();
  return participant;
}

describe('A draft from an earlier day', () => {
  it('says that it is for its date, which submitting keeps, after the date and before Past notes', async () => {
    await openOwnNote('2026-09-30');

    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Wednesday 30${nbsp}September 2026`);
    const notice = earlierDayNotice()!;
    expect(notice.textContent).toBe(earlierDayWords);
    expect(follows(notice, pageHeading())).toBe(true);
    expect(follows(within(main()).getByRole('link', { name: 'Past notes' }), notice)).toBe(true);
    expect(notice.closest('form')).toBeNull();
    // Plain page content, read in its place: never a live region, and never focused.
    for (const attribute of ['role', 'aria-live', 'tabindex']) {
      expect(notice).not.toHaveAttribute(attribute);
    }
  });

  it.each([
    { kind: "the worker's own draft of today", noteDate: '2026-10-01', draft: savedDraft() },
    { kind: 'a new note of today', noteDate: '2026-10-01', draft: template() },
    { kind: "the worker's form for yesterday, not yet started", noteDate: '2026-09-30', draft: template() },
  ])('shows no notice on $kind', async ({ noteDate, draft }) => {
    await openOwnNote(noteDate, draft);

    expect(guidedNotes()).toBeInTheDocument();
    expect(earlierDayNotice()).toBeNull();
  });

  it("shows none on a manager's new note of an earlier day, once its first save is answered too", async () => {
    await openOwnNote('2026-09-28', template(), manager);

    await write();

    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(earlierDayNotice()).toBeNull();
  });

  it('says "Draft · saved Wed 30 Sep 2026, 9:42 am" until its first change, the notice kept after it', async () => {
    await openOwnNote('2026-09-30');

    expect(shownSaveStatus()).toBe(`Draft${nbsp}· saved Wed 30${nbsp}Sep 2026, 9:42${nbsp}am`);
    expect(saveStatus().querySelector('time')).toHaveAttribute('datetime', '2026-09-29T23:42:00Z');

    await write();

    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(earlierDayNotice()?.textContent).toBe(earlierDayWords);
  });

  it.each([
    {
      title: 'Submit the note for',
      noteDate: '2026-09-30',
      draft: savedEarlier(),
      date: `Wednesday 30${nbsp}September 2026`,
    },
    {
      title: 'Submit today’s note for',
      noteDate: '2026-10-01',
      draft: savedDraft(),
      date: `Thursday 1${nbsp}October 2026`,
    },
  ])("asks \"$title\" the name for the worker's own draft of $noteDate, with its date", async ({
    noteDate,
    draft,
    title,
    date,
  }) => {
    await openOwnNote(noteDate, draft);

    await press(submitButton());

    expect(openConfirmation()).toHaveAccessibleName(`${title} Jane Citizen`);
    expect(openConfirmation()).toHaveAccessibleDescription(`${date} Flagged for manager: No`);
  });

  it("sends the version to its own date's address, then lands on Today", async () => {
    const participant = await openOwnNote('2026-09-30');
    await press(submitButton());

    await confirmSubmit();
    await settle();

    expect(submits().map(({ path }) => path)).toEqual([paths(participant, '2026-09-30').versions]);
    expect(banner()).toHaveFocus();
  });

  it("appears on today's draft, unannounced, once `me` gives the next day with the form open", async () => {
    await openOwnNote('2026-10-01', savedDraft());
    guidedNotes().focus();
    serve(mePath, json({ ...worker, today: '2026-10-02' }));

    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await settle();

    const notice = earlierDayNotice()!;
    expect(notice.textContent).toBe(
      `This draft is for Thursday 1${nbsp}October 2026. Submitting it now keeps that date.`,
    );
    for (const attribute of ['role', 'aria-live', 'tabindex']) {
      expect(notice).not.toHaveAttribute(attribute);
    }
    expect(guidedNotes()).toHaveFocus();
  });
});

// Leaving the form within the app, and the browser's own warning (note-form.md *Leaving the form* and items 34 and
// 35, autosave-status.md *Behaviour* 7).
describe('Leaving the form', () => {
  const takeoverSentence = 'This note was changed on another device or tab.';

  /** How the person leaves the form within the app. */
  type Way = 'the back link' | 'the Past notes link' | 'Back';
  const ways: Way[] = ['the back link', 'the Past notes link', 'Back'];

  // The dead ends that a save can meet (note-form.md *Conflicts and refusals*).
  const deadEnds = [
    { deadEnd: 'after midnight', refusal: problem(422, { code: 'note.past_date_manager_only' }) },
    // 9:14 am on 1 October in Melbourne.
    { deadEnd: "someone else's draft", refusal: startedBy('2026-09-30T23:14:00Z') },
    { deadEnd: 'an archived participant', refusal: problem(422, { code: 'note.participant_archived' }) },
    { deadEnd: 'a note submitted elsewhere', refusal: problem(409, { code: 'note.version_conflict' }) },
  ];

  /**
   * Opens a new note for a participant of their own from Today, as a person does, whose saves `save` answers, with
   * their Past notes, which have none, to go to.
   */
  async function openToLeave(save: Answer = saved('2026-09-30T23:42:00Z')) {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, save);
    serve(`/api/participants/${participant.id}/notes`, json({ notes: [], hasMore: false }));
    const { router, client } = await openFromToday([participant]);
    return { participant, router, client };
  }

  /** Leaves the form `way`, as the person does, and lets what follows render. */
  async function leave(way: Way, router: Router) {
    switch (way) {
      case 'the back link':
        await press(within(pageBar()).getByRole('link', { name: 'Today' }));
        break;
      case 'the Past notes link':
        await press(within(main()).getByRole('link', { name: 'Past notes' }));
        break;
      case 'Back':
        await goBack(router);
        break;
    }
  }

  /** Where leaving `way` goes: Today, which opened the form, or the participant's Past notes. */
  function destination(way: Way, participant: ParticipantHeader): string {
    return way === 'the Past notes link' ? `/participants/${participant.id}/notes` : '/';
  }

  /** The takeover banner in the top bar, if it shows. */
  function takeoverBanner(): HTMLElement | null {
    return within(pageBar()).queryByRole('region', { name: takeoverSentence });
  }

  afterEach(() => {
    rearmUnloadGuard();
  });

  it.each(ways)('sends a change waiting on %s, "Saving…" until it is saved, then leaves', async (way) => {
    const save = held(saved('2026-09-30T23:42:00Z'));
    const { participant, router } = await openToLeave(save.answer);
    type('Jane made toast.');

    await leave(way, router);

    expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast.']);
    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(shownSaveStatus()).toBe('Saving…');

    save.release();
    await settle();

    expect(router.state.location.pathname).toBe(destination(way, participant));
    await wait(30_000);
    expect(saves()).toHaveLength(1);
  });

  it('sends a change typed while the save out is answered too, before it leaves', async () => {
    const save = held(saved('2026-09-30T23:42:00Z'));
    const { router } = await openToLeave(save.answer);
    type('Jane made toast.');
    await leave('the back link', router);
    type('Jane made toast. Then a walk.');

    save.release();
    await settle();

    expect(saves().map(({ body }) => [body.seq, body.narrative])).toEqual([
      [1, 'Jane made toast.'],
      [2, 'Jane made toast. Then a walk.'],
    ]);
    expect(router.state.location.pathname).toBe('/');
  });

  it.each(ways)('stays on %s while "Not saved: …" shows, saying it once in the polite region', async (way) => {
    const { participant, router } = await openToLeave(noConnection);
    type('Jane made toast.');
    await wait(3_000);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    const indicatorChanges = watchChanges(saveStatus());
    const said = watchWords(pageStatus());

    await leave(way, router);
    await wait(16);

    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(said()).toEqual([notSavedRetrying]);
    expect(indicatorChanges()).toBe(0);
    expect(saves()).toHaveLength(2);
  });

  it.each(ways)('stays on %s while the takeover banner shows, saying its sentence once politely', async (way) => {
    const { participant, router } = await openToLeave(
      inTurn(saved('2026-09-30T23:43:00Z'), takenOver, saved('2026-09-30T23:44:00Z')),
    );
    await write('Jane made toast.');
    await write('Jane made toast. Then a walk.');
    expect(takeoverBanner()).not.toBeNull();
    const alerts = watchChanges(assertive());
    const said = watchWords(pageStatus());

    await leave(way, router);
    await wait(16);

    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(said()).toEqual([takeoverSentence]);
    expect(alerts()).toBe(0);
    expect(takeoverBanner()).not.toBeNull();
    expect(saves()).toHaveLength(2);
  });

  it.each(ways.flatMap((way) => [403, 422].map((status) => ({ way, status }))))(
    'stays on $way when the save that it sends meets a $status, saying "Not saved: …" once politely',
    async ({ way, status }) => {
      const { participant, router } = await openToLeave(problem(status));
      type('Jane made toast.');
      const said = watchWords(pageStatus());

      await leave(way, router);
      await wait(16);

      expect(saves()).toHaveLength(1);
      expect(router.state.location.pathname).toBe(paths(participant).page);
      expect(said()).toEqual([notSavedRetrying]);
    },
  );

  it('stays when its save fails into "Not saved: …", said once by the top bar, going nowhere once saved', async () => {
    const { participant, router } = await openToLeave(
      inTurn(noConnection, noConnection, saved('2026-09-30T23:44:00Z')),
    );
    type('Jane made toast.');
    const said = watchWords(pageStatus());

    await leave('the back link', router);
    expect(shownSaveStatus()).toBe('Saving…');
    await wait(1_000);
    await wait(16);

    expect(shownSaveStatus()).toBe(notSavedRetrying);
    expect(router.state.location.pathname).toBe(paths(participant).page);
    // The top bar's live region has said it.
    expect(said()).toEqual([]);

    await wait(2_000);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(router.state.location.pathname).toBe(paths(participant).page);
  });

  it('stays when the save that it sends meets the takeover, which the banner says once', async () => {
    const { participant, router } = await openToLeave(inTurn(saved('2026-09-30T23:43:00Z'), takenOver));
    await write('Jane made toast.');
    type('Jane made toast. Then a walk.');
    const alerts = watchWords(assertive());
    const said = watchWords(pageStatus());

    await leave('the back link', router);
    await wait(16);

    expect(takeoverBanner()).not.toBeNull();
    expect(router.state.location.pathname).toBe(paths(participant).page);
    expect(alerts()).toEqual([takeoverSentence]);
    expect(said()).toEqual([]);
  });

  it.each(ways.flatMap((way) => deadEnds.map((end) => ({ way, ...end }))))(
    'stays on $way when the save that it sends meets the dead end of $deadEnd, said once, then leaves on another',
    async ({ way, refusal }) => {
      const save = held(refusal);
      const { participant, router } = await openToLeave(save.answer);
      type('Jane made toast.');
      const alerts = watchWords(assertive());
      const said = watchWords(pageStatus());
      await leave(way, router);
      const focused = document.activeElement;

      save.release();
      await settle();
      await wait(16);

      expect(router.state.location.pathname).toBe(paths(participant).page);
      expect(shownSaveStatus()).toBe('Not saved');
      expect(alerts()).toHaveLength(1);
      expect(within(pageBar()).getByRole('region', { name: alerts()[0] })).toBeInTheDocument();
      expect(said()).toEqual([]);
      expect(document.activeElement).toBe(focused);
      expect(guidedNotes()).toHaveValue('Jane made toast.');

      await leave(way, router);

      expect(router.state.location.pathname).toBe(destination(way, participant));
      expect(saves()).toHaveLength(1);
    },
  );

  it.each(ways)('leaves on %s at once with nothing unsaved, sending nothing', async (way) => {
    const { participant, router } = await openToLeave();
    await write('Jane made toast.');

    await leave(way, router);

    expect(router.state.location.pathname).toBe(destination(way, participant));
    expect(saves()).toHaveLength(1);
  });

  it.each(ways.flatMap((way) => deadEnds.map((end) => ({ way, ...end }))))(
    'leaves on $way at once after the dead end of $deadEnd, with a change since, sending nothing more',
    async ({ way, refusal }) => {
      const { participant, router } = await openToLeave(refusal);
      await write('Jane made toast.');
      expect(shownSaveStatus()).toBe('Not saved');
      type('Jane made toast. Then a walk.');

      await leave(way, router);

      expect(router.state.location.pathname).toBe(destination(way, participant));
      expect(saves()).toHaveLength(1);
    },
  );

  it('listens for beforeunload from the first unsaved change, through "Not saved: …", until saved', async () => {
    const added = vi.spyOn(window, 'addEventListener');
    const removed = vi.spyOn(window, 'removeEventListener');
    const ofUnload = (spy: typeof added | typeof removed) =>
      spy.mock.calls.filter(([event]) => event === 'beforeunload').length;
    const listening = () => ofUnload(added) - ofUnload(removed);
    await openToLeave(inTurn(noConnection, noConnection, saved('2026-09-30T23:44:00Z')));
    expect(listening()).toBe(0);
    expect(unloadAsks()).toBe(false);

    type('Jane made toast.');
    await settle();
    expect(listening()).toBe(1);
    expect(unloadAsks()).toBe(true);

    await wait(3_000);
    expect(shownSaveStatus()).toBe(notSavedRetrying);
    expect(listening()).toBe(1);

    await wait(2_000);
    expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    expect(listening()).toBe(0);
    expect(unloadAsks()).toBe(false);
  });

  it('asks nothing before the page goes once the shell has disarmed the warning, a change unsaved', async () => {
    await openToLeave(noAnswer);
    type('Jane made toast.');
    await settle();
    expect(unloadAsks()).toBe(true);

    disarmUnloadGuard();

    expect(unloadAsks()).toBe(false);
  });

  it("marks Today's lists stale as the form is left for Past notes, its unfinished drafts with the day's", async () => {
    const { participant, router, client } = await openToLeave();
    const todaysKeys = [['today', '2026-10-01'], ['me', 'drafts']];
    for (const key of todaysKeys) {
      // As fresh as a list fetched while the form was open.
      client.setQueryData(key, client.getQueryData(key));
      expect(client.getQueryState(key)?.isInvalidated).toBe(false);
    }

    await leave('the Past notes link', router);

    expect(router.state.location.pathname).toBe(`/participants/${participant.id}/notes`);
    for (const key of todaysKeys) {
      expect(client.getQueryState(key)?.isInvalidated).toBe(true);
    }
  });

  it("lands on Today after Submit's 201, though a change was made while its request was out", async () => {
    const post = held(versionSaved());
    const { router } = await openToSubmit(json(template()), post.answer);
    await write();
    await press(submitButton());
    await press(dialogButton('Submit note for Jane Citizen'));
    // A repeated Escape that a browser lets through while it is busy (confirm-dialog.md *Behaviour* 7).
    openConfirmation().closest('dialog')!.close();
    await settle();
    type('Jane made toast. Then a walk.');
    const said = watchWords(pageStatus());

    post.release();
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(banner()).toHaveFocus();
    expect(said()).toEqual([]);
    expect(saves()).toHaveLength(2);
  });
});

// The note form's side of the session (note-form.md *Session* and item 36; app-shell.md's Session and Sign out
// criteria; session-timeout.md *The session clock* step 3 and *The signed-out state*; S02.02.05).
describe('The session and Sign out', () => {
  const signedOutHeading = 'You’ve been signed out';
  const takeoverSentence = 'This note was changed on another device or tab.';
  const notSignedOut = 'Not signed out: your note is not saved yet. Keep this page open; retrying.';
  const logout = 'POST /api/auth/logout';

  beforeEach(() => {
    // A fake system time, as session.test.ts has: 9 am on Thursday 1 October 2026 in Melbourne.
    vi.setSystemTime(Date.parse('2026-10-01T09:00:00+10:00'));
    answers.set(logout, () => Promise.resolve(new Response(null, { status: 204 })));
  });

  afterEach(() => {
    vi.mocked(fullPageLoad).mockReset();
    rearmUnloadGuard();
  });

  /** Opens a new note for a participant of their own at its address, whose saves `save` answers. */
  async function openForSession(save: Answer = saved('2026-09-30T23:42:00Z')) {
    const participant = freshParticipant();
    serveNote(participant);
    serveSave(paths(participant).draft, save);
    const { router } = renderApp(paths(participant).page);
    await settle();
    return { participant, router };
  }

  /**
   * The window regains focus, whose refresh of `/api/auth/me` the server accepts: the newest request, from whose send
   * time the session clock counts. Gives that time.
   */
  async function refreshNow(): Promise<number> {
    const from = Date.now();
    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await settle();
    return from;
  }

  /** Moves the fake clock on to `clock`, in minutes and seconds, as '27:59', after `from`. */
  async function at(clock: string, from: number) {
    const [minutes, seconds] = clock.split(':').map(Number);
    await wait(from + (minutes * 60 + seconds) * 1000 - Date.now());
  }

  /** The warning before the idle sign-out, if it is open. */
  function warning(): HTMLElement | null {
    return screen.queryByRole('dialog', { name: /^You’ll be signed out in/ });
  }

  /** The Guided notes box, found while the signed-out-in-place view hides it too. */
  function hiddenBox(): HTMLTextAreaElement {
    return within(main()).getByRole('textbox', { name: '3. Guided notes', hidden: true });
  }

  /** Makes the page hidden or visible, with the event that the browser fires. */
  function showPage(state: DocumentVisibilityState) {
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });
  }

  /** Puts `value` in `field`, as typing or pasting it does. */
  function fill(field: HTMLElement, value: string) {
    fireEvent.change(field, { target: { value } });
  }

  /** Signs in again in the signed-out-in-place view with an email, a password and a code, as `me`. */
  async function signInAgain(me: Me = worker) {
    answers.set('POST /api/auth/login', json({ next: 'totp' }));
    answers.set('POST /api/auth/login/totp', json(me));
    serve(mePath, json(me));
    fill(screen.getByRole('textbox', { name: 'Email address' }), 'casey.nguyen@example.org');
    fill(screen.getByLabelText('Password'), 'correct horse battery staple');
    await press(screen.getByRole('button', { name: 'Continue' }));
    await settle();
    fill(screen.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' }), '123456');
    await press(screen.getByRole('button', { name: 'Sign in' }));
    await settle();
  }

  /** Ends the session as the save of `text` meets a `401`, which `/api/auth/me` answers from then on too. */
  async function endTheSessionOnASave(text: string) {
    serve(mePath, problem(401));
    type(text);
    await wait(2_000);
    await settle();
    expect(pageHeading()).toHaveAccessibleName(signedOutHeading);
  }

  /** The takeover banner in the top bar, if it shows. */
  function takeoverBanner(): HTMLElement | null {
    return within(pageBar()).queryByRole('region', { name: takeoverSentence });
  }

  /** The Account panel, which has no role to find it by. */
  function accountPanel(): HTMLElement {
    const panel = document.getElementById('account-panel');
    if (panel === null) {
      throw new Error('There is no #account-panel.');
    }

    return panel;
  }

  /** Opens the Account panel and presses Sign out in it, as the person does, and gives the button. */
  async function pressSignOut(): Promise<HTMLElement> {
    fireEvent.click(screen.getByRole('button', { name: 'Account' }));
    const signOut = within(accountPanel()).getByRole('button', { name: 'Sign out' });
    await press(signOut);
    return signOut;
  }

  /** How many logouts have been sent. */
  function logouts(): number {
    return sentRequests().filter((request) => request === logout).length;
  }

  describe('at 28:00 idle', () => {
    it('sends a change made at 27:59 at 28:00, and opens no warning once it is answered', async () => {
      await openForSession();
      const from = await refreshNow();
      await at('27:59', from);
      type('Jane made toast.');

      await wait(999);
      expect(saves()).toEqual([]);
      await wait(1);

      // At 28:00, before the 2 s after the change.
      expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast.']);
      await settle();
      expect(warning()).toBeNull();
      expect(shownSaveStatus()).toBe(`Saved 9:42${nbsp}am`);
      // 28 minutes from the save's send time.
      await at('55:59', from);
      expect(warning()).toBeNull();
      await at('56:00', from);
      expect(warning()).not.toBeNull();
      expect(saves()).toHaveLength(1);
    });

    it('lets the warning open as a save that fails at 28:00 settles, with "Not saved: …" under it', async () => {
      // The first save holds the antiforgery token, whose request at 28:00 would be accepted, a new idle period.
      await openForSession(inTurn(saved('2026-09-30T23:42:00Z'), noConnection));
      await write('Jane made toast.');
      const from = await refreshNow();
      await at('27:59', from);
      type('Jane made toast. Then a walk.');

      await at('28:00', from);
      expect(saves()).toHaveLength(2);
      expect(warning()).toBeNull();
      await at('28:01', from);

      // Its retry failed too.
      expect(saves()).toHaveLength(3);
      expect(warning()).not.toBeNull();
      expect(shownSaveStatus()).toBe(notSavedRetrying);
    });

    it('sends nothing with nothing unsaved, and warns', async () => {
      await openForSession();
      await write('Jane made toast.');
      const from = await refreshNow();

      await at('27:59', from);
      expect(warning()).toBeNull();
      await at('28:00', from);

      expect(warning()).not.toBeNull();
      expect(saves()).toHaveLength(1);
    });

    it("stops a failing server's retries 28 minutes after the change, so the warning opens 28 minutes on", async () => {
      // Every save after the first answered 500, which the clock counts as accepted, as the server renews the session
      // with it (A57).
      await openForSession(inTurn(saved('2026-09-30T23:42:00Z'), problem(500)));
      await write('Jane made toast.');
      const from = Date.now();
      type('Jane made toast. Then a walk.');

      await at('28:00', from);
      const sent = saves().length;
      expect(warning()).toBeNull();
      expect(shownSaveStatus()).toBe(notSavedRetrying);

      // The last retry went at 27:57, 10 s after the one before, and nothing more is sent, so the clock warns 28
      // minutes after it, as on any idle page.
      await at('55:56', from);
      expect(warning()).toBeNull();
      await at('55:58', from);
      expect(warning()).not.toBeNull();
      expect(saves()).toHaveLength(sent);
      expect(shownSaveStatus()).toBe(notSavedRetrying);
    });
  });

  describe('signed out in place', () => {
    it('sends nothing while signed out, then the same person signing in again sends the change at once', async () => {
      const { router } = await openForSession(
        inTurn(saved('2026-09-30T23:42:00Z'), problem(401), saved('2026-09-30T23:44:00Z')),
      );
      const { location } = router.state;
      fireEvent.click(goalBoxes()[0]);
      await write('Jane made toast.');
      fireEvent.click(flagBox());
      typeReason('Seemed tired.');
      await endTheSessionOnASave('Jane made toast. Then a walk.');
      const text = 'Jane made toast. Then a walk. Home by three.';

      // A change, a blur and the page hidden, while the view hides the form.
      fireEvent.input(hiddenBox(), { target: { value: text } });
      fireEvent.focusOut(hiddenBox());
      showPage('hidden');
      await wait(30_000);
      showPage('visible');
      await wait(30_000);
      expect(saves()).toHaveLength(2);

      await signInAgain();

      const [, lost, resent] = saves();
      expect(saves()).toHaveLength(3);
      expect(resent.body).toMatchObject({
        clientId: lost.body.clientId,
        seq: 3,
        narrative: text,
        tickedGoalIds: [breakfast.id],
        pickedGroupIds: [],
        tickedCommonItemIds: [],
        isFlagged: true,
        flagReason: 'Seemed tired.',
      });
      expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
      expect(pageHeading()).toHaveFocus();
      expect(guidedNotes()).toHaveValue(text);
      expect(goalBoxes().map((box) => box.checked)).toEqual([true, false, false]);
      expect(flagBox()).toBeChecked();
      expect(reasonField()).toHaveValue('Seemed tired.');
      expect(takeoverBanner()).toBeNull();
      expect(router.state.location).toBe(location);
      await settle();
      expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
      await wait(30_000);
      expect(saves()).toHaveLength(3);
    });

    it("holds a change waiting as the window's refresh meets a 401, until the same person signs in", async () => {
      const { participant } = await openForSession(
        inTurn(saved('2026-09-30T23:42:00Z'), saved('2026-09-30T23:44:00Z')),
      );
      await write('Jane made toast.');
      serve(mePath, problem(401));
      type('Jane made toast. Then a walk.');

      act(() => {
        window.dispatchEvent(new FocusEvent('focus'));
      });
      await wait(60_000);

      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);
      expect(saves()).toHaveLength(1);

      await signInAgain();

      const clientId = saves()[0].body.clientId;
      expect(saves().map(({ path, body }) => [path, body.clientId, body.seq, body.narrative])).toEqual([
        [paths(participant).draft, clientId, 1, 'Jane made toast.'],
        [paths(participant).draft, clientId, 2, 'Jane made toast. Then a walk.'],
      ]);
    });

    it('fetches a token as the same person signs in again, so that the page-hidden save is one request', async () => {
      await openForSession();
      await write('Jane made toast.');
      serve(mePath, problem(401));
      act(() => {
        window.dispatchEvent(new FocusEvent('focus'));
      });
      await settle();
      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);

      // The sign-in drops the token that the page held (api.ts), and the form fetches another as it shows again.
      await signInAgain();
      const tokenRequests = () => sentRequests().filter((request) => request === `GET ${antiforgeryPath}`).length;
      const fetched = tokenRequests();
      type('Jane made toast. Then a walk.');
      showPage('hidden');
      await settle();

      expect(tokenRequests()).toBe(fetched);
      const hiddenSave = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').slice(1);
      expect(hiddenSave.map(([, init]) => [init?.keepalive, new Headers(init?.headers).has('X-XSRF-TOKEN')])).toEqual([
        [true, true],
      ]);
    });

    it('stops retrying at 30:00 idle with no connection, sending nothing until the same person signs in', async () => {
      const { participant } = await openForSession(inTurn(saved('2026-09-30T23:42:00Z'), noConnection));
      await write('Jane made toast.');
      const from = await refreshNow();
      await at('27:59', from);
      type('Jane made toast. Then a walk.');
      await at('28:01', from);
      expect(warning()).not.toBeNull();

      await at('30:00', from);
      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);
      const sent = saves().length;
      const text = 'Jane made toast. Then a walk. Home by three.';
      fireEvent.input(hiddenBox(), { target: { value: text } });
      fireEvent.focusOut(hiddenBox());
      await at('40:00', from);
      expect(saves()).toHaveLength(sent);

      serveSave(paths(participant).draft, saved('2026-09-30T23:44:00Z'));
      await signInAgain();

      expect(saves()).toHaveLength(sent + 1);
      const { clientId } = saves()[0].body;
      expect(saves().at(-1)?.body).toMatchObject({ clientId, seq: sent + 1, narrative: text });
      expect(takeoverBanner()).toBeNull();
      await settle();
      expect(shownSaveStatus()).toBe(`Saved 9:44${nbsp}am`);
    });

    it("loads the page afresh for another person's sign-in, sending nothing of the earlier person's", async () => {
      await openForSession(inTurn(saved('2026-09-30T23:42:00Z'), problem(401)));
      await write('Jane made toast.');
      await endTheSessionOnASave('Jane made toast. Then a walk.');

      await signInAgain(manager);
      await wait(60_000);

      expect(fullPageLoad).toHaveBeenCalledOnce();
      expect(saves()).toHaveLength(2);
      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);
    });

    it("never sends Submit's POST again after its 401, once the same person has signed in again", async () => {
      await openToSubmit(json(template()), problem(401));
      await write();
      await press(submitButton());
      serve(mePath, problem(401));
      await confirmSubmit();
      await settle();
      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);

      await signInAgain();
      await wait(60_000);

      expect(submits()).toHaveLength(1);
      expect(confirmation()).toBeNull();
      expect(saves()).toHaveLength(2);
      expect(pageHeading()).toHaveFocus();
    });

    it('closes the Discard dialog for its 401 before sign-in, and never sends it again once signed in', async () => {
      await openToDiscard(problem(401));
      type('Jane made toast. Then a walk.');
      await openDiscardDialog();
      serve(mePath, problem(401));

      await confirmDiscard();
      await settle();

      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);
      expect(pageHeading()).toHaveFocus();
      expect(confirmation()).toBeNull();

      await signInAgain();
      await wait(60_000);

      expect(discards()).toHaveLength(1);
      expect(confirmation()).toBeNull();
      expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
      expect(pageHeading()).toHaveFocus();
      expect(moreActions()).toHaveAttribute('aria-expanded', 'false');
      // Autosave alone goes on, sending the change that the Discard's pause held.
      expect(saves().map(({ body }) => body.narrative)).toEqual(['Jane made toast. Then a walk.']);
    });

    it('opens no confirmation from a check save that met a 401, its change sent by autosave alone', async () => {
      const { participant } = await openToSubmit();
      serveSave(
        paths(participant).draft,
        inTurn(saved('2026-09-30T23:42:00Z'), problem(401), saved('2026-09-30T23:44:00Z')),
      );
      await write();
      serve(mePath, problem(401));
      type('Jane made toast. Then a walk.');
      await press(submitButton());
      await settle();
      expect(pageHeading()).toHaveAccessibleName(signedOutHeading);

      await signInAgain();
      await wait(60_000);

      expect(confirmation()).toBeNull();
      expect(submits()).toEqual([]);
      expect(saves().map(({ body }) => [body.seq, body.narrative])).toEqual([
        [1, 'Jane made toast.'],
        [2, 'Jane made toast. Then a walk.'],
        [3, 'Jane made toast. Then a walk.'],
      ]);
    });
  });

  describe('Sign out', () => {
    it('sends a change waiting before POST /api/auth/logout, busy meanwhile, then loads / afresh', async () => {
      const save = held(saved('2026-09-30T23:42:00Z'));
      const { participant } = await openForSession(save.answer);
      type('Jane made toast.');

      const signOut = await pressSignOut();
      await wait(400);

      expect(saves()).toHaveLength(1);
      expect(logouts()).toBe(0);
      expect(signOut).toHaveAccessibleName('Signing out…');
      expect(signOut).toHaveAttribute('aria-disabled', 'true');

      save.release();
      await settle();

      expect(sentRequests().filter((request) => request.startsWith('PUT') || request === logout)).toEqual([
        `PUT ${paths(participant).draft}`,
        logout,
      ]);
      expect(fullPageLoad).toHaveBeenCalledOnce();
    });

    it.each([
      { failure: 'fails, and its retry too', save: noConnection, settles: 1_000 },
      { failure: 'meets a 422', save: problem(422), settles: 0 },
      { failure: 'meets a 403', save: problem(403), settles: 0 },
    ])('sends no logout when the save of a change waiting $failure, saying so, Sign out ready', async ({
      save,
      settles,
    }) => {
      await openForSession(save);
      type('Jane made toast.');

      const signOut = await pressSignOut();
      await wait(settles);
      await settle();

      expect(within(accountPanel()).getByRole('status').textContent).toBe(notSignedOut);
      expect(signOut).not.toHaveAttribute('aria-disabled');
      expect(signOut).toHaveAccessibleName('Sign out');
      expect(signOut).toHaveFocus();
      expect(logouts()).toBe(0);
      expect(fullPageLoad).not.toHaveBeenCalled();
      expect(unloadAsks()).toBe(true);
      await wait(30_000);
      expect(logouts()).toBe(0);
    });

    it('signs out after a dead end, with a change since, sending nothing more', async () => {
      await openForSession(problem(409, { code: 'note.version_conflict' }));
      await write('Jane made toast.');
      expect(shownSaveStatus()).toBe('Not saved');
      type('Jane made toast. Then a walk.');

      await pressSignOut();
      await settle();

      expect(logouts()).toBe(1);
      expect(fullPageLoad).toHaveBeenCalledOnce();
      expect(saves()).toHaveLength(1);
    });

    it('is refused while the takeover banner shows, saying so, with nothing sent', async () => {
      await openForSession(inTurn(saved('2026-09-30T23:43:00Z'), takenOver));
      await write('Jane made toast.');
      await write('Jane made toast. Then a walk.');
      expect(takeoverBanner()).not.toBeNull();

      const signOut = await pressSignOut();
      await settle();

      expect(within(accountPanel()).getByRole('status').textContent).toBe(notSignedOut);
      expect(signOut).not.toHaveAttribute('aria-disabled');
      expect(logouts()).toBe(0);
      expect(fullPageLoad).not.toHaveBeenCalled();
      expect(saves()).toHaveLength(2);
      expect(takeoverBanner()).not.toBeNull();
    });
  });
});

/** The read view's section under the `<h2>` named `name`. */
function section(name: string): HTMLElement {
  const heading = within(main()).getByRole('heading', { level: 2, name });
  const block = heading.closest(`.${readView.section}`);
  if (!(block instanceof HTMLElement)) {
    throw new Error(`"${name}" is in no section.`);
  }

  return block;
}

/** The Guided notes text under its heading, as the read view shows it. */
function narrative(): HTMLElement {
  const text = section('3. Guided notes').querySelector(`div.${readView.written}`);
  if (!(text instanceof HTMLElement)) {
    throw new Error('The read view shows no Guided notes text.');
  }

  return text;
}

/** The byline and the status lines under the `<h1>`, in order. */
function statusLines(): HTMLElement[] {
  return [...main().querySelectorAll<HTMLElement>(`.${readView.line}`)];
}

describe('The read view of a submitted note', () => {
  it.each(['header', 'note'] as const)(
    'shows nothing of the note while the %s has not answered, then all of it at once',
    async (waiting) => {
      const answers = { header: json(jane), note: json(submittedNote()) };
      const late = held(answers[waiting]);
      serve(paths(jane).header, waiting === 'header' ? late.answer : answers.header);
      serve(paths(jane).note, waiting === 'note' ? late.answer : answers.note);
      renderApp(paths(jane).page);

      await settle();
      expect(within(main()).queryByRole('heading')).toBeNull();
      expect(within(main()).queryByRole('list')).toBeNull();
      expect(main()).not.toHaveTextContent(/Priya Nair|toast/);

      late.release();
      await settle();
      expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
      expect(within(section('1. Goals')).getAllByRole('listitem')).toHaveLength(3);
      expect(narrative()).toHaveTextContent(/^Jane made toast\./);
    },
  );

  it('focuses the <h1>, the name as stored, never translated, then the date, on arriving from Today', async () => {
    serveSubmitted(jane);
    const { router } = await openFromToday();

    expect(router.state.location.pathname).toBe(paths(jane).page);
    const heading = pageHeading();
    expect(heading).toHaveFocus();
    expect(heading).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(within(heading).getByText('Jane Citizen')).toHaveAttribute('translate', 'no');
    expect(heading.querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Note');
  });

  // Nothing on the read view is sticky (participant-notes.md item 29), and it has no save status.
  it('has a static top bar, with the back link to Today alone', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    expect(topBar()).not.toHaveStyle({ position: 'sticky' });
    expect(within(topBar()).getByRole('link', { name: 'Today' })).toHaveAttribute('href', '/');
    expect(within(topBar()).getAllByRole('link')).toHaveLength(1);
    expect(pageBar().querySelector('#save-status')).toBeNull();
  });

  it('says who wrote it, then its time alone when it was submitted on the note date in Melbourne', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    const [byline] = statusLines();
    expect(shown(byline)).toBe(`Written by Priya Nair${nbsp}· Submitted 4:42${nbsp}pm`);
    expect(byline.textContent).toBe(`Written by Priya Nair${nbsp}·, Submitted 4:42${nbsp}pm`);
    expect(byline.querySelector('time')).toHaveAttribute('datetime', '2026-10-01T06:42:00Z');
    expect(follows(byline, pageHeading())).toBe(true);
  });

  // 8:05 am in Melbourne on Friday 2 October, still in standard time (UTC+10).
  it('gives the submitted time with its date when it was submitted on a later day', async () => {
    serveSubmitted(jane, submittedNote({ firstSubmittedAtUtc: '2026-10-01T22:05:00Z' }));
    renderApp(paths(jane).page);
    await settle();

    expect(shown(statusLines()[0])).toBe(
      `Written by Priya Nair${nbsp}· Submitted Fri 2${nbsp}Oct 2026, 8:05${nbsp}am`,
    );
  });

  it('shows Flagged, then the reason as written, in the text colour, on a flagged note alone', async () => {
    serveSubmitted(jane, submittedNote({ reason: 'Mentioned pain in his left knee\nafter the walk.' }));
    renderApp(paths(jane).page);
    await settle();

    const lines = statusLines();
    expect(lines).toHaveLength(2);
    const [, flag] = lines;
    const tag = within(flag).getByText('Flagged');
    expect(tag).toHaveClass(tagStyles.tag, tagStyles.attention);
    expect(flag.textContent).toBe('Flagged, Reason: Mentioned pain in his left knee\nafter the walk.');
    const reason = within(flag).getByText(/^Reason:/);
    expect(reason).toHaveClass(readView.written);
    expect(reason).toHaveStyle({ whiteSpace: 'pre-wrap', overflowWrap: 'break-word' });
    expect(follows(reason, tag)).toBe(true);
  });

  // 7:03 am on Thursday 1 October in UTC is 5:03 pm in Melbourne, the note date; 10:01 pm is 9:01 am on the Friday.
  it('says Edited and who made the last change, then the time alone when that was on the note date', async () => {
    serveSubmitted(jane, submittedNote({ lastEdit: { by: 'Jo Smith', atUtc: '2026-10-01T07:03:00Z' } }));
    renderApp(paths(jane).page);
    await settle();

    const lines = statusLines();
    expect(lines).toHaveLength(2);
    const [byline, edited] = lines;
    const tag = within(edited).getByText('Edited');
    expect(tag).toHaveClass(tagStyles.tag, tagStyles.neutral);
    expect(edited.textContent).toBe(`Edited, last change by Jo Smith, 5:03${nbsp}pm`);
    expect(shown(edited)).toBe(`Edited last change by Jo Smith, 5:03${nbsp}pm`);
    expect(edited.querySelector('time')).toHaveAttribute('datetime', '2026-10-01T07:03:00Z');
    expect(follows(edited, byline)).toBe(true);
  });

  it('gives the time of the last change with its date when that was on a later day', async () => {
    serveSubmitted(jane, submittedNote({ lastEdit: { by: 'Jo Smith', atUtc: '2026-10-01T23:01:00Z' } }));
    renderApp(paths(jane).page);
    await settle();

    expect(shown(statusLines()[1])).toBe(`Edited last change by Jo Smith, Fri 2${nbsp}Oct 2026, 9:01${nbsp}am`);
  });

  it('shows the Edited line between the byline and the flag line', async () => {
    serveSubmitted(
      jane,
      submittedNote({ reason: 'Jane seemed unwell.', lastEdit: { by: 'Jo Smith', atUtc: '2026-10-01T07:03:00Z' } }),
    );
    renderApp(paths(jane).page);
    await settle();

    const [byline, edited, flag] = statusLines();
    expect(shown(byline)).toMatch(/^Written by/);
    expect(shown(edited)).toMatch(/^Edited last change by/);
    expect(shown(flag)).toMatch(/^Flagged/);
    expect(follows(edited, byline)).toBe(true);
    expect(follows(flag, edited)).toBe(true);
    expect(follows(within(main()).getByRole('link', { name: 'Past notes' }), flag)).toBe(true);
  });

  // participant-notes.md component 5, the action row, which holds Past notes alone until T14 and T18 add theirs.
  it('has the Past notes link after the byline and the flag line, before Goals', async () => {
    serveSubmitted(jane, submittedNote({ reason: 'Jane seemed unwell.' }));
    renderApp(paths(jane).page);
    await settle();

    const link = within(main()).getByRole('link', { name: 'Past notes' });
    expect(link).toHaveAttribute('href', `/participants/${jane.id}/notes`);
    expect(follows(link, statusLines()[1])).toBe(true);
    expect(follows(section('1. Goals'), link)).toBe(true);
  });

  it('shows no flag line, and no Reason, on a note that is not flagged', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    expect(statusLines()).toHaveLength(1);
    expect(main()).not.toHaveTextContent(/Flagged|Reason/);
  });

  it('shows no Edited line on a note with one version', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    expect(statusLines()).toHaveLength(1);
    expect(main()).not.toHaveTextContent(/Edited|last change/);
  });

  // checkbox-list.md `TickListRead`: text, never a form control, the state in words after a pause.
  it('lists every goal in the snapshot order, each read as ticked or not ticked, never as a control', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    const goals = section('1. Goals');
    const items = within(goals).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual([
      'Makes own breakfast, ticked',
      'Catches bus to day program, not ticked',
      'Phones her sister, ticked',
    ]);
    expect(items.map(shown)).toEqual([breakfast.text, bus.text, sister.text]);
    expect(within(main()).queryByRole('checkbox')).toBeNull();
    expect(main().querySelector('input, textarea, form')).toBeNull();
    expect(follows(goals, statusLines()[0])).toBe(true);
  });

  it('says "No goals set" for a note with none, with no list', async () => {
    serveSubmitted(jane, submittedNote({ goals: [] }));
    renderApp(paths(jane).page);
    await settle();

    const goals = section('1. Goals');
    expect(within(goals).queryByRole('list')).toBeNull();
    expect(within(goals).getByText('No goals set')).toBeInTheDocument();
  });

  // participant-notes.md item 17a: Every note and the picked groups, as the API gives them, each item ticked or not.
  it('shows "2. Common items" between Goals and Guided notes, an <h3> per group, items ticked or not', async () => {
    serveSubmitted(jane, submittedNote({ commonItemGroups: shownGroups }));
    renderApp(paths(jane).page);
    await settle();

    const common = section('2. Common items');
    expect(follows(common, section('1. Goals'))).toBe(true);
    expect(follows(section('3. Guided notes'), common)).toBe(true);
    const headings = within(common).getAllByRole('heading', { level: 3 });
    expect(headings.map((heading) => heading.textContent)).toEqual(['Every note', 'Community outing']);
    const [everyNoteList, outingList] = within(common).getAllByRole('list');
    expect(within(everyNoteList).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Medication prompted, ticked',
      'Meal prepared, not ticked',
    ]);
    expect(within(outingList).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Travelled by bus or train, ticked',
      'Paid for own purchases, not ticked',
    ]);
    expect(common.querySelector('.' + readView.groups)).not.toBeNull();
    expect(within(main()).queryByRole('checkbox')).toBeNull();
  });

  it('says "No common items set" for a note that shows none, with no list', async () => {
    serveSubmitted(jane);
    renderApp(paths(jane).page);
    await settle();

    const common = section('2. Common items');
    expect(within(common).queryByRole('list')).toBeNull();
    expect(within(common).queryByRole('heading', { level: 3 })).toBeNull();
    expect(within(common).getByText('No common items set').tagName).toBe('P');
  });

  it('never asks "Which of these happened?" and shows no group but the ones it is given', async () => {
    serveSubmitted(jane, submittedNote({ commonItemGroups: shownGroups }));
    renderApp(paths(jane).page);
    await settle();

    expect(main()).not.toHaveTextContent('Which of these happened?');
    expect(main()).not.toHaveTextContent(personalCare.name);
    expect(within(main()).queryByRole('group')).toBeNull();
  });

  // participant-notes.md item 18: escaped text, never HTML, its line breaks kept and wrapped anywhere. jsdom lays out
  // nothing, so the rules that keep 320 px from scrolling sideways are checked here, and the page itself end to end.
  it('shows 20,000 characters with blank lines, unbroken strings and <b>hi</b> whole and literal', async () => {
    const start = '<b>hi</b> Jane made toast.\n\n\nThen ';
    const text = `${start}${'x'.repeat(10_000)}\n\n${'y'.repeat(20_000 - start.length - 10_002)}`;
    expect(text).toHaveLength(20_000);
    serveSubmitted(jane, submittedNote({ narrative: text }));
    renderApp(paths(jane).page);
    await settle();

    expect(narrative().textContent).toBe(text);
    expect(narrative().querySelector('b')).toBeNull();
    expect(narrative()).toHaveStyle({ whiteSpace: 'pre-wrap', overflowWrap: 'break-word' });
    expect(follows(narrative(), section('1. Goals'))).toBe(true);
  });

  // participant-notes.md item 23: the text does not change under a reader, and comes afresh as the page opens again.
  it('sends no request for the note on window focus, and reads it afresh when opened again', async () => {
    serveSubmitted(jane);
    serve(todayPath, json({ date: '2026-10-01', participants: [onToday(jane)] } satisfies TodayList));
    const { router } = renderApp(paths(jane).page);
    await settle();
    const reads = () => sentRequests().filter((request) => request === `GET ${paths(jane).note}`).length;
    const before = reads();

    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
      document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
    });
    await wait(5_000);
    expect(reads()).toBe(before);

    await press(within(pageBar()).getByRole('link', { name: 'Today' }));
    await press(rowLink('Jane Citizen'));
    await settle();
    expect(router.state.location.pathname).toBe(paths(jane).page);
    expect(reads()).toBeGreaterThan(before);
  });

  // participant-notes.md item 21; T14 adds Edit, T16 a manager's review lines and panel, and T18 Version history.
  it("has no Edit, Version history, To review, Reviewed or review panel in a worker's DOM, and no form", async () => {
    serveSubmitted(jane, submittedNote({ reason: 'Jane seemed unwell.' }));
    renderApp(paths(jane).page);
    await settle();

    expect(within(main()).getAllByRole('link').map((link) => link.textContent)).toEqual(['Past notes']);
    expect(within(main()).queryAllByRole('button')).toEqual([]);
    expect(main()).not.toHaveTextContent(/Edit|Version history|To review|Reviewed|Mark reviewed|Comment/);
    expect(main().querySelector('form, textarea')).toBeNull();
    expect(sentRequests()).not.toContain(`GET ${paths(jane).draft}`);
  });

  it('shows "The note did not load: …" with Try again when the note was not read, asking for no form', async () => {
    serve(paths(jane).header, json(jane));
    serve(paths(jane).note, noConnection);
    renderApp(paths(jane).page);

    // After its one silent retry.
    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Note');
    expect(line().textContent).toBe('The note did not load: no connection. Try again.');
    expect(sentRequests()).not.toContain(`GET ${paths(jane).draft}`);

    serve(paths(jane).note, json(submittedNote()));
    await press(within(main()).getByRole('button', { name: 'Try again' }));
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Jane Citizen Thursday 1${nbsp}October 2026`);
    expect(pageHeading()).toHaveFocus();
    expect(narrative()).toBeInTheDocument();
  });
});

// Common items and the picker (group-picker.md; note-form.md items 30c to 30j), on a note whose template has groups.
describe('Common items and group picks', () => {
  /** Every note, Community outing picked, as the template copies it from the last note, and Personal care not. */
  const copied = [everyNote, { ...outing, isPicked: true }, personalCare];

  const listsNoticeWords = 'The goal or common-item list was just changed. Please check your ticks.';

  /** A picker box, by its group's name. */
  function pickBox(name: string): HTMLInputElement {
    return within(main()).getByRole('checkbox', { name });
  }

  /** The tick box of a common item, which is not in the accessibility tree while its group is not picked. */
  function itemBox(item: DraftItem): HTMLInputElement {
    return within(main()).getByRole('checkbox', { name: item.text });
  }

  /** Opens a new note whose template has `groups`, as `openToSubmit()` opens one. */
  function openWithGroups(groups: readonly DraftGroup[] = copied) {
    return openToSubmit(json(template([breakfast, bus, sister], prompts, groups)));
  }

  it('shows "2. Common items" between Goals and Guided notes, with Every note and the picker', async () => {
    await openWithGroups();

    const heading = within(main()).getByRole('heading', { level: 2, name: '2. Common items' });
    expect(follows(heading, within(main()).getByRole('group', { name: '1. Goals' }))).toBe(true);
    expect(follows(guidedNotes(), heading)).toBe(true);
    expect(within(main()).getByRole('group', { name: 'Every note' })).toHaveAccessibleDescription('Ticked means done.');
    expect(within(main()).getByRole('group', { name: 'Which of these happened?' })).toBeInTheDocument();
  });

  it('shows "No common items set up." for a draft with no groups, as one saved before common items has', async () => {
    serveNote(jane, json(savedDraft()));
    renderApp(paths(jane).page);
    await settle();

    const heading = within(main()).getByRole('heading', { level: 2, name: '2. Common items' });
    expect(heading.parentElement).toHaveTextContent('No common items set up.');
    expect(main().querySelector('#common-hint, #picker-hint')).toBeNull();
    expect(follows(guidedNotes(), heading)).toBe(true);
  });

  it('opens with the copied picks ticked, their items unticked, and sends nothing until the first change', async () => {
    await openWithGroups();

    expect(pickBox(outing.name)).toBeChecked();
    expect(pickBox(outing.name)).toHaveAttribute('aria-expanded', 'true');
    expect(pickBox(personalCare.name)).not.toBeChecked();
    expect(pickBox(personalCare.name)).toHaveAttribute('aria-expanded', 'false');
    expect(itemBox(trains)).not.toBeChecked();
    expect(itemBox(purchases)).not.toBeChecked();
    expect(within(main()).queryByRole('checkbox', { name: showered.text })).toBeNull();
    await wait(60_000);
    expect(saves()).toEqual([]);

    fireEvent.click(within(main()).getByText(medication.text));
    await wait(2_000);

    expect(saves()).toHaveLength(1);
    expect(saves()[0].body).toMatchObject({
      tickedGoalIds: [],
      pickedGroupIds: [outing.id],
      tickedCommonItemIds: [medication.id],
    });
  });

  it('shows a group ticked, its items unticked, focus staying on its box', async () => {
    await openWithGroups();
    const box = pickBox(personalCare.name);
    box.focus();

    fireEvent.click(box);

    expect(box).toHaveFocus();
    expect(box).toBeChecked();
    expect(box).toHaveAttribute('aria-expanded', 'true');
    expect(main().querySelector(`#group-${personalCare.id}`)).not.toHaveAttribute('hidden');
    expect(itemBox(showered)).not.toBeChecked();
    await wait(2_000);
    expect(saves()[0].body).toMatchObject({ pickedGroupIds: [outing.id, personalCare.id] });
  });

  it('hides a group unticked, with its ticked items, at once, opening no dialog and asking nothing', async () => {
    await openWithGroups();
    fireEvent.click(itemBox(trains));
    fireEvent.click(within(main()).getByText(medication.text));
    await wait(2_000);
    expect(saves()[0].body).toMatchObject({
      pickedGroupIds: [outing.id],
      tickedCommonItemIds: [medication.id, trains.id],
    });

    const box = pickBox(outing.name);
    box.focus();
    fireEvent.click(box);

    expect(document.querySelector('dialog[open]')).toBeNull();
    expect(box).toHaveFocus();
    expect(box).toHaveAttribute('aria-expanded', 'false');
    expect(main().querySelector(`#group-${outing.id}`)).toHaveAttribute('hidden');
    expect(within(main()).queryByRole('checkbox', { name: trains.text })).toBeNull();
    await wait(2_000);
    expect(saves()[1].body).toMatchObject({ pickedGroupIds: [], tickedCommonItemIds: [medication.id] });

    fireEvent.click(box);

    expect(itemBox(trains)).not.toBeChecked();
    expect(itemBox(purchases)).not.toBeChecked();
    expect(itemBox(medication)).toBeChecked();
    await wait(2_000);
    expect(saves()[2].body).toMatchObject({ pickedGroupIds: [outing.id], tickedCommonItemIds: [medication.id] });
  });

  it('submits with nothing picked, the confirmation saying nothing of groups, both lists in the version', async () => {
    await openWithGroups([everyNote, outing, personalCare]);
    fireEvent.click(itemBox(medication));
    await write();

    await press(submitButton());

    expect(confirmation()).not.toBeNull();
    expect(confirmation()).not.toHaveTextContent(/common|outing|care|picked/i);
    await confirmSubmit();
    expect(submits()).toHaveLength(1);
    expect(submits()[0].body).toMatchObject({
      tickedGoalIds: [],
      pickedGroupIds: [],
      tickedCommonItemIds: [medication.id],
    });
  });

  /** Opens a new note with `opened`, whose first save is `409 note.lists_changed`, and the form read `reloaded`. */
  async function openWithListsChanging(opened: readonly DraftGroup[], reloaded: readonly DraftGroup[]) {
    const participant = freshParticipant();
    serveNote(participant, json(template([breakfast, bus, sister], prompts, opened)));
    const resaved = held(saved('2026-09-30T23:42:00Z'));
    serveSave(paths(participant).draft, inTurn(problem(409, { code: 'note.lists_changed' }), resaved.answer));
    renderApp(paths(participant).page);
    await settle();
    const reload = held(json({ ...template([breakfast, bus, sister], prompts, reloaded), listsVersion: 'lists-2' }));
    serve(paths(participant).draft, reload.answer);
    return { participant, reload, resaved };
  }

  // Community outing archived, and Meal prepared moved from Every note into Personal care, which is not picked.
  const reloadedGroups = [
    { ...everyNote, items: [medication] },
    { ...personalCare, items: [showered, mealPrepared] },
  ];

  it('reloads the lists keeping the picks still offered, the ticks still shown, the text and the notice', async () => {
    const { reload, resaved } = await openWithListsChanging(copied, reloadedGroups);
    fireEvent.click(itemBox(medication));
    fireEvent.click(itemBox(mealPrepared));
    fireEvent.click(itemBox(trains));
    type('Jane made toast.');
    await wait(2_000);
    expect(saves()[0].body).toMatchObject({
      pickedGroupIds: [outing.id],
      tickedCommonItemIds: [medication.id, mealPrepared.id, trains.id],
    });

    reload.release();
    await settle();

    expect(listsNotice()).toHaveTextContent(listsNoticeWords);
    expect(guidedNotes()).toHaveValue('Jane made toast.');
    expect(within(main()).queryByRole('checkbox', { name: outing.name })).toBeNull();
    expect(pickBox(personalCare.name)).not.toBeChecked();
    expect(itemBox(medication)).toBeChecked();
    expect(within(main()).queryByRole('checkbox', { name: mealPrepared.text })).toBeNull();
    expect(saves()).toHaveLength(2);
    expect(saves()[1].body).toMatchObject({
      listsVersion: 'lists-2',
      narrative: 'Jane made toast.',
      pickedGroupIds: [],
      tickedCommonItemIds: [medication.id],
    });
    resaved.release();
    await settle();

    fireEvent.click(pickBox(personalCare.name));
    expect(within(main()).getByRole('checkbox', { name: mealPrepared.text })).not.toBeChecked();
  });

  it.each([
    {
      focused: 'an Every note item moved into a group that is not picked',
      box: () => itemBox(mealPrepared),
      goesTo: 'the notice',
    },
    { focused: 'the box of a group now archived', box: () => pickBox(outing.name), goesTo: 'the notice' },
    { focused: 'a box that survives', box: () => pickBox(personalCare.name), goesTo: 'the box' },
  ])('sends focus to $goesTo after the reload, from $focused', async ({ box, goesTo }) => {
    const { reload } = await openWithListsChanging(copied, reloadedGroups);
    const focused = box();
    focused.focus();
    fireEvent.click(focused);
    await wait(2_000);

    reload.release();
    await settle();

    expect(listsNotice()).toHaveTextContent(listsNoticeWords);
    if (goesTo === 'the notice') {
      expect(focused).not.toBeInTheDocument();
      expect(listsNotice()).toHaveFocus();
    } else {
      expect(focused).toBeInTheDocument();
      expect(focused).toHaveFocus();
      expect(focused).toBeChecked();
      expect(main().querySelector(`#group-${personalCare.id}`)).not.toHaveAttribute('hidden');
    }
  });

  describe('when the note is changed on another device or tab', () => {
    const keepWords = 'Keep the text on this screen';
    const loadWords = 'Load the other version';

    /** Opens the worker's draft with the copied picks, saves twice, the second meeting the takeover, Trains ticked. */
    async function loseTheNote() {
      const participant = freshParticipant();
      serveNote(participant, json({ ...savedDraft('Jane'), commonItemGroups: copied }));
      serveSave(
        paths(participant).draft,
        inTurn(saved('2026-09-30T23:43:00Z'), takenOver, saved('2026-09-30T23:44:00Z')),
      );
      renderApp(paths(participant).page);
      await settle();
      await write('Jane made toast.');
      fireEvent.click(itemBox(trains));
      await write('Jane made toast. Then a walk.');
      expect(saves()).toHaveLength(2);
      return participant;
    }

    it('sends the on-screen picks and ticks on Keep, under a new clientId from 1', async () => {
      await loseTheNote();

      await press(screen.getByRole('button', { name: keepWords }));

      expect(saves()).toHaveLength(3);
      expect(saves()[2].body).toMatchObject({
        seq: 1,
        pickedGroupIds: [outing.id],
        tickedCommonItemIds: [trains.id],
        narrative: 'Jane made toast. Then a walk.',
      });
      expect(saves()[2].body.clientId).not.toBe(saves()[0].body.clientId);
    });

    it('replaces the picks and ticks with the other version’s on Load, and saves them from 1', async () => {
      const participant = await loseTheNote();
      const theirs: Draft = {
        ...savedDraft('Jane went to the library.'),
        commonItemGroups: [
          everyNote,
          outing,
          { ...personalCare, isPicked: true, items: [{ ...showered, isTicked: true }] },
        ],
        savedAtUtc: '2026-09-30T23:50:00Z',
      };
      serve(paths(participant).draft, json(theirs));

      await press(screen.getByRole('button', { name: loadWords }));
      await wait(16);

      expect(pickBox(outing.name)).not.toBeChecked();
      expect(within(main()).queryByRole('checkbox', { name: trains.text })).toBeNull();
      expect(pickBox(personalCare.name)).toBeChecked();
      expect(itemBox(showered)).toBeChecked();
      await write('Jane went to the library. Then lunch.');
      expect(saves()[2].body).toMatchObject({
        seq: 1,
        pickedGroupIds: [personalCare.id],
        tickedCommonItemIds: [showered.id],
      });
      expect(saves()[2].body.clientId).not.toBe(saves()[0].body.clientId);
    });
  });
});
