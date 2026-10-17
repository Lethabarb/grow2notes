import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes } from '../../routes/routes.tsx';
import { participantsQuery, type AdminParticipant, type ParticipantStatus } from './participantsQuery.ts';

type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const participantsPath = '/api/admin/participants?includeArchived=true';

// participants.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts,
// and a worker.
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

/** A made-up participant, born on 7 March 1987. */
function madeUp(id: string, givenName: string, familyName: string, status: ParticipantStatus): AdminParticipant {
  return { id, givenName, familyName, dateOfBirth: '1987-03-07', status, rowVersion: `AAAAAAAA${id.slice(0, 4)}=` };
}

const zara = madeUp('1f7c3a9e-5d21-4b48-8e6f-0a2c4d7b9e13', 'Zara', 'Ali', 'Active');
const ben = madeUp('2c9f6b3e-8a14-4e70-9d25-4b7e1f0a6c58', 'Ben', 'Archer', 'Archived');
const jane = madeUp('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Jane', 'Citizen', 'Active');
const ravi = madeUp('4a8e2c6f-1b39-4d57-9f0a-7c3e5b1d8a26', 'Ravi', 'de Silva', 'Active');
const zoe = madeUp('5d3b9f1a-6e48-4a27-b0c3-8f2d5e7a1b96', 'Zoë', 'Hill', 'Archived');
const priya = madeUp('6b2e8d4a-0f37-4c95-a1d6-3e9f7b5c2a80', 'Priya', 'Kumar', 'Active');
const sean = madeUp('7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20', 'Seán', 'O’Brien', 'Active');
const ana = madeUp('8d1f5b3a-6c24-4e98-a7b0-2e9c4f6a1d53', 'Ana', 'Ward', 'Active');

// In the API's order, by family name, then given name (A5), active and archived together.
const people = [zara, ben, jane, ravi, zoe, priya, sean, ana];
const activeNames = ['Zara Ali', 'Jane Citizen', 'Ravi de Silva', 'Priya Kumar', 'Seán O’Brien', 'Ana Ward'];
const archivedNames = ['Ben Archer', 'Zoë Hill'];

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

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** How many requests have been sent for `path`. */
function sent(path: string): number {
  return fetchMock.mock.calls.filter(([sentPath]) => sentPath === path).length;
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own and StrictMode, as `main.tsx`
 * renders it. `['me']` holds `me` already, as after the start-up, so that the screen shows with the first render, from
 * which the fake clock then times its region.
 */
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

/** Opens the list at `path` with the made-up people, and lets it load. */
async function openList(path = '/manage/participants', list: readonly AdminParticipant[] = people) {
  serve(participantsPath, json(list));
  const opened = renderApp(path);
  await settle();
  return opened;
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** Add participant. */
function addParticipant(): HTMLElement {
  return within(main()).getByRole('link', { name: 'Add participant' });
}

/** The Find a participant landmark, or `null` while the screen has none. */
function search(): HTMLElement | null {
  return main().querySelector('search');
}

/** The Find a participant field. */
function field(): HTMLElement {
  return within(main()).getByRole('textbox', { name: 'Find a participant' });
}

/** Types `text` in the field, as each key does, replacing what was there. */
function type(text: string) {
  fireEvent.change(field(), { target: { value: text } });
}

/** The Active / Archived navigation, named by the page's `<h1>`. */
function views(): HTMLElement {
  return within(main()).getByRole('navigation', { name: 'Participants' });
}

/** The link to one of the two views. */
function viewLink(name: 'Active' | 'Archived'): HTMLElement {
  return within(views()).getByRole('link', { name });
}

/** The region's line: the status region in `<main>` that shows, beside the page's and the search's hidden ones. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The search's own status region, which says the no-match sentence after a pause. */
function searchStatus(): HTMLElement {
  return within(search()!).getByRole('status');
}

/** The names of the rows shown, in order, each row holding exactly one link and nothing else to read. */
function listedNames(): string[] {
  const lists = within(main()).queryAllByRole('list').filter((list) => !views().contains(list));
  if (lists.length === 0) {
    return [];
  }

  expect(lists).toHaveLength(1);
  return within(lists[0])
    .getAllByRole('listitem')
    .map((row) => {
      const links = within(row).getAllByRole('link');
      expect(links).toHaveLength(1);
      expect(row.textContent).toBe(links[0].textContent);
      return links[0].textContent;
    });
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** Expects Page not found in `<main>`, titled "Page not found", under the nav, which marks no item. */
function expectPageNotFound() {
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(within(main()).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(within(main()).queryByRole('link', { name: 'Add participant' })).not.toBeInTheDocument();
  expect(document.title).toBe('Grow2Notes – Page not found');
  expect(currentItems()).toEqual([]);
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

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // React and React Router report each throw that a boundary catches: a refusal is expected, and anything else shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.some((arg) => arg instanceof ApiError && arg.status === 403)) {
      report(...args);
    }
  });
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

describe('Participants', () => {
  it('lists the active participants in the order the API gives, by family name, then given name', async () => {
    await openList();

    expect(listedNames()).toEqual(activeNames);
    expect(document.title).toBe('Grow2Notes – Participants');
    expect(currentItems()).toEqual(['Manage true']);
  });

  it.each([zara, ravi, sean])(
    "gives $givenName's row one link, named by the full name as stored, to their page, as a return point",
    async (participant) => {
      await openList();

      const link = within(main()).getByRole('link', { name: `${participant.givenName} ${participant.familyName}` });
      expect(link).toHaveAttribute('href', `/manage/participants/${participant.id}`);
      expect(link).toHaveAttribute('data-return-key', participant.id);
      // No status line: the view names it.
      expect(link).not.toHaveAttribute('aria-describedby');
    },
  );

  it('shows its <h1>, Add participant, Find a participant, Active and Archived, then the rows', async () => {
    await openList();

    expect(pageHeading()).toHaveAccessibleName('Participants');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    const add = addParticipant();
    expect(add).toHaveAttribute('href', '/manage/participants/new');
    expect(add).toHaveClass(...buttonClass('primary').split(' '));
    const firstRow = within(main()).getByRole('link', { name: 'Zara Ali' });
    const inOrder = [pageHeading(), add, field(), viewLink('Active'), viewLink('Archived'), firstRow];
    for (const [before, after] of inOrder.slice(0, -1).map((element, index) => [element, inOrder[index + 1]])) {
      expect(before.compareDocumentPosition(after)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    }
  });

  it('gives Find a participant a label, a description, and nothing that keeps or corrects the text', async () => {
    await openList();

    const input = field();
    expect(search()).toContainElement(input);
    expect(within(search()!).queryByRole('button')).not.toBeInTheDocument();
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveAccessibleDescription('The list of participants below changes as you type.');
    expect(input).toHaveAttribute('autocomplete', 'off');
    expect(input).toHaveAttribute('autocorrect', 'off');
    expect(input).toHaveAttribute('spellcheck', 'false');
    expect(input).not.toHaveAttribute('placeholder');
    expect(input).not.toHaveAttribute('name');
    expect(input).not.toHaveFocus();
    expect(search()!.querySelector('form')).toBeNull();
    // Always in the page, empty, so that the words written into it later are read.
    expect(searchStatus()).toBeEmptyDOMElement();
  });
});

describe('Participants, finding a participant', () => {
  it('filters the view as each key is typed, keeping the API order, with focus in the field', async () => {
    await openList();

    field().focus();
    type('a');

    // Given names first would put Ana Ward before Zara Ali.
    expect(listedNames()).toEqual(['Zara Ali', 'Ana Ward']);
    expect(field()).toHaveFocus();
    expect(field()).toHaveValue('a');
  });

  it.each([
    ['cit jane', ['Jane Citizen']],
    ['Jane.', ['Jane Citizen']],
    ['sean', ['Seán O’Brien']],
    ['obr', ['Seán O’Brien']],
    ['de s', ['Ravi de Silva']],
  ])('finds the right person for "%s"', async (typed, names) => {
    await openList();

    type(typed);

    expect(listedNames()).toEqual(names);
  });

  it.each(['   ', '.'])('shows the whole view and no message for "%s"', async (typed) => {
    await openList();

    type(typed);
    await wait(1000);

    expect(listedNames()).toEqual(activeNames);
    expect(within(main()).queryByText(/No participant matches/)).not.toBeInTheDocument();
    expect(searchStatus()).toBeEmptyDOMElement();
  });

  it('shows the no-match sentence in place of the rows at once, and says it once typing pauses 500 ms', async () => {
    await openList();

    type('x');
    await wait(300);
    type('xy');
    await wait(300);
    type(' xyz ');

    expect(listedNames()).toEqual([]);
    const sentence = within(main()).getByText('No participant matches ‘xyz’.');
    expect(sentence.tagName).toBe('P');
    expect(sentence).not.toHaveAttribute('role');
    // Not said while the keys came less than 500 ms apart.
    expect(searchStatus()).toBeEmptyDOMElement();

    await wait(499);
    expect(searchStatus()).toBeEmptyDOMElement();
    await wait(1);
    expect(searchStatus()).toHaveTextContent(/^No participant matches ‘xyz’\.$/);

    // The text stays in the field, with Clear.
    expect(field()).toHaveValue(' xyz ');
    expect(within(search()!).getByRole('button', { name: 'Clear search' })).toBeInTheDocument();
  });

  it('empties the search region again once something matches', async () => {
    await openList();
    type('xyz');
    await wait(500);

    type('z');
    await wait(500);

    expect(listedNames()).toEqual(['Zara Ali']);
    expect(searchStatus()).toBeEmptyDOMElement();
  });

  it('shows Clear only while the field holds text, which empties it and puts focus back in it', async () => {
    await openList();
    expect(within(search()!).queryByRole('button')).not.toBeInTheDocument();
    type('jane');

    const clear = within(search()!).getByRole('button', { name: 'Clear search' });
    expect(clear).toHaveTextContent('Clear');
    // Its mouse-down never takes focus from the field, so a phone keeps its keyboard open.
    field().focus();
    expect(fireEvent.mouseDown(clear)).toBe(false);
    fireEvent.click(clear);

    expect(field()).toHaveValue('');
    expect(field()).toHaveFocus();
    expect(within(search()!).queryByRole('button')).not.toBeInTheDocument();
    expect(listedNames()).toEqual(activeNames);
  });

  it('does nothing for Enter or Escape in the field', async () => {
    const { router } = await openList();
    field().focus();
    type('jane');

    fireEvent.keyDown(field(), { key: 'Enter' });
    fireEvent.keyDown(field(), { key: 'Escape' });
    await settle();

    expect(field()).toHaveValue('jane');
    expect(field()).toHaveFocus();
    expect(listedNames()).toEqual(['Jane Citizen']);
    expect(router.state.location.pathname).toBe('/manage/participants');
  });

  it('keeps Add participant where it is, the same link, whatever is typed and in either view', async () => {
    await openList();
    const add = addParticipant();
    const placed = () => add.parentElement?.previousElementSibling;
    expect(placed()).toBe(pageHeading());

    type('a');
    expect(addParticipant()).toBe(add);
    type('xyz');
    expect(addParticipant()).toBe(add);
    await press(viewLink('Archived'));
    expect(addParticipant()).toBe(add);
    expect(placed()).toBe(pageHeading());
  });

  it('keeps what is typed out of the address, the title, the router state, the query keys and storage', async () => {
    const { router, client } = await openList();

    type('Citizen');
    await press(viewLink('Archived'));
    await wait(1000);

    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(
      '/manage/participants?view=archived',
    );
    expect(document.title).toBe('Grow2Notes – Participants');
    expect(JSON.stringify(router.state.location.state ?? null)).not.toContain('Citizen');
    const keys = client.getQueryCache().getAll().map(({ queryKey }) => queryKey);
    expect(keys).toEqual([['me'], ['admin', 'participants']]);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });
});

describe('Participants, Active and Archived', () => {
  /** The links in the view navigation that are marked current. */
  function currentViews(): string[] {
    return within(views())
      .getAllByRole('link')
      .filter((link) => link.hasAttribute('aria-current'))
      .map((link) => `${link.textContent} ${link.getAttribute('aria-current')}`);
  }

  it('names the navigation by the <h1>, with Active, the default, first and current, and no other ARIA', async () => {
    await openList();

    expect(views()).toHaveAttribute('aria-labelledby', pageHeading().id);
    expect(within(views()).getAllByRole('link').map((link) => link.textContent)).toEqual(['Active', 'Archived']);
    expect(viewLink('Active')).toHaveAttribute('href', '/manage/participants');
    expect(viewLink('Archived')).toHaveAttribute('href', '/manage/participants?view=archived');
    expect(currentViews()).toEqual(['Active page']);
    expect(within(views()).queryByRole('tab')).not.toBeInTheDocument();
  });

  it('switches to Archived with a push, keeping what is typed and focus on the link, with no request', async () => {
    const { router } = await openList();
    type('a');
    scrollPage(300);
    const requests = sent(participantsPath);

    await press(viewLink('Archived'));

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.search).toBe('?view=archived');
    expect(field()).toHaveValue('a');
    expect(listedNames()).toEqual(['Ben Archer']);
    expect(viewLink('Archived')).toHaveFocus();
    expect(currentViews()).toEqual(['Archived page']);
    expect(window.scrollY).toBe(300);
    // Both views are the one list.
    expect(sent(participantsPath)).toBe(requests);

    await press(viewLink('Active'));

    expect(router.state.location.search).toBe('');
    expect(field()).toHaveValue('a');
    expect(listedNames()).toEqual(['Zara Ali', 'Ana Ward']);
    expect(viewLink('Active')).toHaveFocus();
    expect(currentViews()).toEqual(['Active page']);
  });

  it('goes back to the view before with Back, keeping what is typed', async () => {
    const { router } = await openList();
    await press(viewLink('Archived'));
    type('hill');

    await goBack(router);

    expect(router.state.location.search).toBe('');
    expect(currentViews()).toEqual(['Active page']);
    expect(field()).toHaveValue('hill');
    expect(within(main()).getByText('No participant matches ‘hill’.')).toBeInTheDocument();
  });

  it('opens on the Archived view at its address, as after a reload', async () => {
    await openList('/manage/participants?view=archived');

    expect(currentViews()).toEqual(['Archived page']);
    expect(listedNames()).toEqual(archivedNames);
    expect(document.title).toBe('Grow2Notes – Participants');
  });

  it('shows Active for any other view, and puts the address right with a replace', async () => {
    const { router } = await openList('/manage/participants?view=other');

    expect(router.state.historyAction).toBe('REPLACE');
    expect(router.state.location.search).toBe('');
    expect(currentViews()).toEqual(['Active page']);
    expect(listedNames()).toEqual(activeNames);
  });

  it('filters the Archived view as the Active one, saying when nothing there matches', async () => {
    await openList('/manage/participants?view=archived');

    type('zoe');
    expect(listedNames()).toEqual(['Zoë Hill']);

    type('jane');
    expect(listedNames()).toEqual([]);
    expect(within(main()).getByText('No participant matches ‘jane’.')).toBeInTheDocument();
  });
});

describe('Participants, empty', () => {
  it('shows only "No participants yet." under Add participant and the views when there are none', async () => {
    await openList('/manage/participants', []);

    expect(search()).toBeNull();
    expect(addParticipant().parentElement?.previousElementSibling).toBe(pageHeading());
    expect(views()).toBeInTheDocument();
    expect(listedNames()).toEqual([]);
    expect(main()).toHaveTextContent(/^ParticipantsAdd participantActiveArchivedNo participants yet\.$/);

    await press(viewLink('Archived'));
    expect(within(main()).getByText('No participants yet.')).toBeInTheDocument();
    expect(search()).toBeNull();
  });

  it('says "No active participants." with the search box still there when everyone is archived', async () => {
    await openList('/manage/participants', [ben, zoe]);

    expect(within(main()).getByText('No active participants.')).toBeInTheDocument();
    expect(search()).not.toBeNull();
    expect(listedNames()).toEqual([]);
  });

  it('says "No archived participants." with the search box still there when nobody is archived', async () => {
    await openList('/manage/participants?view=archived', [zara, jane]);

    expect(within(main()).getByText('No archived participants.')).toBeInTheDocument();
    expect(search()).not.toBeNull();
    expect(listedNames()).toEqual([]);
  });
});

describe('Participants while the list loads', () => {
  it('shows its <h1> and Add participant at once, then nothing more until "Loading participants…" at 1 s', async () => {
    serve(participantsPath, noAnswer);
    renderApp('/manage/participants');

    expect(pageHeading()).toHaveAccessibleName('Participants');
    expect(addParticipant()).toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    // No search box, no views, no rows and no empty sentence.
    expect(main().textContent).toBe('ParticipantsAdd participant');
    expect(search()).toBeNull();

    await wait(1);
    expect(line()).toHaveTextContent('Loading participants…');
    expect(main().textContent).toBe('ParticipantsAdd participantLoading participants…');
  });

  it('says the list did not load, with Try again, once its silent retry has found no connection too', async () => {
    serve(participantsPath, noConnection);
    renderApp('/manage/participants');

    await wait(999);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    await wait(1);
    await settle();

    expect(line().textContent).toBe('The participant list did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(pageHeading()).toHaveAccessibleName('Participants');
    expect(addParticipant()).toBeInTheDocument();
    expect(search()).toBeNull();
    expect(within(main()).queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('says something went wrong for a 500, after its silent retry', async () => {
    serve(participantsPath, problem(500));
    renderApp('/manage/participants');

    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The participant list did not load: something went wrong. Try again.');
  });

  it.each([
    { cause: 'no connection', answer: noConnection },
    { cause: 'something went wrong', answer: problem(503) },
  ])('says the list still did not load: $cause, after a Try again that fails, with focus on Try again', async ({
    cause,
    answer,
  }) => {
    serve(participantsPath, answer);
    renderApp('/manage/participants');
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe(`The participant list still did not load: ${cause}. Try again.`);
    expect(button).toHaveFocus();
  });

  it('shows the rows after a Try again that loads, with focus on the <h1>', async () => {
    serve(participantsPath, noConnection);
    renderApp('/manage/participants');
    await wait(1000);
    await settle();
    serve(participantsPath, json(people));

    pressTryAgain();
    await settle();

    expect(listedNames()).toEqual(activeNames);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });

  it('shows Page not found in place of the screen for a 403, at the same address', async () => {
    serve(participantsPath, problem(403));
    const { router } = renderApp('/manage/participants');

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe('/manage/participants');
  });
});

describe('Participants for a worker', () => {
  it.each(['/manage/participants', '/manage/participants?view=archived'])(
    'shows Page not found at %s, and sends no request for the list',
    async (path) => {
      serve(participantsPath, json(people));
      renderApp(path, worker);

      await wait(60_000);

      expectPageNotFound();
      expect(sent(participantsPath)).toBe(0);
    },
  );
});

describe('Participants, back from a participant', () => {
  /** Opens the list at `path`, scrolled to 600, types `typed`, and opens the row of `participant` from it. */
  async function openParticipant(participant: AdminParticipant, path = '/manage/participants', typed = '') {
    const { router, client } = await openList(path);
    // Their page reads their goals with them.
    serve(`/api/admin/participants/${participant.id}/goals?includeArchived=true`, json([]));
    scrollPage(600);
    if (typed !== '') {
      type(typed);
    }

    await press(within(main()).getByRole('link', { name: `${participant.givenName} ${participant.familyName}` }));

    expect(router.state.location.pathname).toBe(`/manage/participants/${participant.id}`);
    expect(window.scrollY).toBe(0);
    return { router, client };
  }

  it('focuses the row opened, where the list was, with what was typed gone', async () => {
    const { router } = await openParticipant(priya, '/manage/participants', 'kum');
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    await goBack(router);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/participants');
    const row = within(main()).getByRole('link', { name: 'Priya Kumar' });
    expect(row).toHaveFocus();
    expect(focus.mock.contexts.at(-1)).toBe(row);
    expect(focus).toHaveBeenLastCalledWith({ preventScroll: true });
    expect(window.scrollY).toBe(600);
    // Leaving the screen discarded the search, so the whole view is back.
    expect(field()).toHaveValue('');
    expect(listedNames()).toEqual(activeNames);
  });

  it('focuses the row opened from the Archived view, back on that view', async () => {
    const { router } = await openParticipant(zoe, '/manage/participants?view=archived');

    await goBack(router);

    expect(router.state.location.search).toBe('?view=archived');
    expect(within(main()).getByRole('link', { name: 'Zoë Hill' })).toHaveFocus();
  });

  it('focuses the <h1> when the row opened is no longer in the view', async () => {
    const { router, client } = await openParticipant(jane);
    // Archived on their page, which reads the list again before the manager comes back.
    const archived = people.map((p) => (p === jane ? { ...jane, status: 'Archived' as const } : p));
    serve(participantsPath, json(archived));
    client.setQueryData(participantsQuery.queryKey, archived);

    await goBack(router);

    expect(listedNames()).toEqual(activeNames.filter((name) => name !== 'Jane Citizen'));
    expect(pageHeading()).toHaveFocus();
  });

  // After the tests above, each of which opened a row from the first entry of its own history.
  it('focuses the <h1> at the top on Back from Add participant, as no row was opened', async () => {
    const { router } = await openList();

    await press(addParticipant());
    await goBack(router);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(pageHeading()).toHaveFocus();
    expect(window.scrollY).toBe(0);
  });
});
