import type * as SimpleWebAuthn from '@simplewebauthn/browser';
import { startAuthentication, type AuthenticationResponseJSON } from '@simplewebauthn/browser';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode, useEffect, useState } from 'react';
import { createMemoryRouter, Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { ApiError } from '../api/api.ts';
import { meQuery, useMe, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { PageHeading } from '../components/PageHeading.tsx';
import { pageName } from '../copy/pageNames.ts';
import type { AdminUser } from '../features/users/usersQuery.ts';
import { messageStore } from './messageStore.ts';
import { routes, type AppRoute, type RouteHandle } from './routes.tsx';

// The browser's passkey prompt, which jsdom has not got; the rest of SimpleWebAuthn is its own.
vi.mock('@simplewebauthn/browser', async (importOriginal) => ({
  ...(await importOriginal<typeof SimpleWebAuthn>()),
  startAuthentication: vi.fn(),
}));

const mePath = '/api/auth/me';
const usersPath = '/api/admin/users';
const antiforgeryPath = '/api/auth/antiforgery';
const optionsPath = '/api/auth/passkey/options';
const passkeyPath = '/api/auth/passkey';

// A made-up manager (A35), in the JSON that MeEndpointTests asserts, and the same person as a refresh finds them
// renamed, for which the made-up crashing screen throws.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const renamed: Me = { ...manager, displayName: 'Sam Lee-Park' };

// users.md's made-up people (A35): the manager signed in, and a worker whose page is opened.
const sam: AdminUser = {
  id: manager.userId,
  email: 'sam.lee@example.org',
  displayName: 'Sam Lee',
  role: 'Manager',
  status: 'Active',
  invitedAtUtc: '2026-09-01T00:00:00Z',
  activatedAtUtc: '2026-09-02T00:00:00Z',
  deactivatedAtUtc: null,
  concurrencyStamp: 'stamp-sam',
};
const alex: AdminUser = {
  ...sam,
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  email: 'alex.park@example.org',
  displayName: 'Alex Park',
  role: 'Worker',
  concurrencyStamp: 'stamp-alex',
};
const people = [sam, alex];

// What the browser's prompt gives for a made-up passkey, in SimpleWebAuthn's JSON.
const credential: AuthenticationResponseJSON = {
  id: 'Y3JlZGVudGlhbA',
  rawId: 'Y3JlZGVudGlhbA',
  response: {
    authenticatorData: 'YXV0aGVudGljYXRvcg',
    clientDataJSON: 'Y2xpZW50RGF0YQ',
    signature: 'c2lnbmF0dXJl',
    userHandle: 'dXNlcg',
  },
  type: 'public-key',
  clientExtensionResults: {},
  authenticatorAttachment: 'platform',
};

// The success message that the made-up note's Submit leaves for Today, as the note form's will (status-messages.md).
const submitted = 'Note for Jane Citizen submitted';

// The crash that the made-up crashing screen throws.
const crash = new Error('A made-up crash');

/** Today, as T08 and T09 build it, as far as the rule goes: the success banner, read once, the `<h1>` and rows. */
function MadeUpToday() {
  // As Today reads the store (status-messages.md *Implementation notes*): once, and cleared, so that it shows once.
  const [message] = useState(() => messageStore.peek());
  useEffect(() => {
    messageStore.clear();
  }, []);

  return (
    <>
      {message !== undefined && (
        <div tabIndex={-1} data-route-focus>
          <p>{message}</p>
        </div>
      )}
      <PageHeading>Today</PageHeading>
      <ul>
        {['1', '2', '3'].map((id) => (
          <li key={id}>
            <Link to={`/made-up/notes/${id}`}>{`Participant ${id}`}</Link>
          </li>
        ))}
      </ul>
    </>
  );
}

/** A note, with Submit, which goes to Today by a push or, as from the note's back link, a pop, and Discard. */
function MadeUpNote() {
  const { id } = useParams();
  const navigate = useNavigate();

  return (
    <>
      <PageHeading>{`Note ${id}`}</PageHeading>
      <button
        type="button"
        onClick={() => {
          messageStore.set(submitted);
          void navigate('/made-up/today');
        }}
      >
        Submit
      </button>
      <button
        type="button"
        onClick={() => {
          messageStore.set(submitted);
          void navigate(-1);
        }}
      >
        Submit and go back
      </button>
      <button type="button" onClick={() => void navigate('/made-up/today', { replace: true })}>
        Discard
      </button>
    </>
  );
}

/** Flagged notes, with its `?view=` switch. */
function MadeUpFlagged() {
  const [search] = useSearchParams();

  return (
    <>
      <PageHeading>Flagged notes</PageHeading>
      <Link to="?view=toReview">To review</Link>
      <Link to="?view=reviewed">Reviewed</Link>
      <p>{search.get('view') ?? 'toReview'}</p>
    </>
  );
}

/** Add participant at `new`, whose Save replaces the address with the new participant's, in the same screen. */
function MadeUpParticipant() {
  const { id } = useParams();
  const navigate = useNavigate();

  return (
    <>
      <PageHeading>{id === 'new' ? 'Add participant' : `Participant ${id}`}</PageHeading>
      <button
        type="button"
        onClick={() => void navigate('/made-up/participants/7', { replace: true, state: { inScreen: true } })}
      >
        Save
      </button>
    </>
  );
}

// The Daily report's dates, one after another.
const dates = ['2026-10-01', '2026-10-02', '2026-10-03', '2026-10-04'];

/** The Daily report for a date, the first without one in its address, with a link to the next day. */
function MadeUpReport() {
  const { date = dates[0] } = useParams();

  return (
    <>
      <PageHeading>{`Daily report ${date}`}</PageHeading>
      <Link to={`/made-up/report/${dates[dates.indexOf(date) + 1]}`}>Next day</Link>
    </>
  );
}

/** A screen that throws once a refresh finds the person renamed, or once its button is pressed. */
function MadeUpCrashing() {
  const { displayName } = useMe();
  const [pressed, setPressed] = useState(false);
  if (pressed || displayName !== manager.displayName) {
    throw crash;
  }

  return (
    <>
      <PageHeading>Flagged notes</PageHeading>
      <button type="button" onClick={() => setPressed(true)}>
        Mark reviewed
      </button>
    </>
  );
}

// Two routes of one screen, as the Daily report's dates are.
const reportHandle: RouteHandle = { pageName: pageName.dailyReport, screenKey: 'dailyReport' };

// The made-up screens beside the app's.
const madeUp: AppRoute[] = [
  { path: 'made-up/today', element: <MadeUpToday />, handle: { pageName: pageName.today } },
  { path: 'made-up/notes/:id', element: <MadeUpNote />, handle: { pageName: pageName.note } },
  { path: 'made-up/flagged', element: <MadeUpFlagged />, handle: { pageName: pageName.flaggedNotes } },
  { path: 'made-up/participants/:id', element: <MadeUpParticipant />, handle: { pageName: pageName.participant } },
  { path: 'made-up/report', element: <MadeUpReport />, handle: reportHandle },
  { path: 'made-up/report/:date', element: <MadeUpReport />, handle: reportHandle },
  { path: 'made-up/crashing', element: <MadeUpCrashing />, handle: { pageName: pageName.flaggedNotes } },
];

/** `table` with the made-up screens beside its `*` route, inside the screens' error boundary. */
function withMadeUp(table: AppRoute[]): AppRoute[] {
  return table.flatMap((route) => {
    if (route.path === '*') {
      return [...madeUp, route];
    }

    return route.index || route.children === undefined ? [route] : [{ ...route, children: withMadeUp(route.children) }];
  });
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

/** `answer`, given after `ms`. */
function after(ms: number, answer: Answer): Answer {
  return (init) =>
    new Promise((resolve) => {
      setTimeout(() => {
        resolve(answer(init));
      }, ms);
    });
}

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/**
 * Renders the app with the made-up screens at `path` on a memory router, or at the last of `path`'s addresses, after
 * the others in its history, under a query client of its own and StrictMode, as `main.tsx` renders it. With `me`,
 * `['me']` holds it already, as after the start-up, so that the screen shows with the first render; with `null`, the
 * start-up asks the server.
 */
function renderApp(path: string | string[], me: Me | null = manager) {
  const client = createQueryClient();
  if (me !== null) {
    serve(mePath, json(me));
    client.setQueryData(meQuery.queryKey, me);
  }

  const router = createMemoryRouter(withMadeUp(routes), { initialEntries: typeof path === 'string' ? [path] : path });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return router;
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

/** The link named `name` in the main navigation. */
function navLink(name: string): HTMLElement {
  return within(screen.getByRole('banner')).getByRole('link', { name });
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets the next screen render. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
}

/** Goes `delta` entries back or forward in the history, as browser Back and Forward do, a pop. */
async function go(router: ReturnType<typeof renderApp>, delta: number) {
  await act(() => router.navigate(delta));
  await settle();
}

/** Scrolls the page to `y`, as the person does, with the scroll event that the browser fires. */
function scrollPage(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, configurable: true, writable: true });
  window.dispatchEvent(new Event('scroll'));
}

// Spies on every call of scrollTo(), which go on to vitest.setup.ts's stand-in, and of focus(), which go on to jsdom's.
// The app's are the last, as a test's own come before what it then checks.
let scrollTo: MockInstance<typeof window.scrollTo>;
let focus: MockInstance<HTMLElement['focus']>;

/** Expects the app to have scrolled the page to `y`, last, with nothing since. */
function expectScrolledTo(y: number) {
  expect(scrollTo).toHaveBeenLastCalledWith(0, y);
  expect(window.scrollY).toBe(y);
}

/** Expects `element` to have focus, which the app last gave it with `options`. */
function expectFocused(element: HTMLElement, options?: FocusOptions) {
  expect(element).toHaveFocus();
  expect(focus.mock.contexts.at(-1)).toBe(element);
  expect(focus.mock.calls.at(-1)?.[0]).toEqual(options);
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));
    }

    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // React and React Router report each throw that a boundary catches: the made-up crash and a manager's 403 are
  // expected, and anything else shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.some((arg) => arg === crash || (arg instanceof ApiError && arg.status === 403))) {
      report(...args);
    }
  });
  // The browser's default, which the shell must set to manual.
  history.scrollRestoration = 'auto';
  scrollPage(0);
  scrollTo = vi.spyOn(window, 'scrollTo');
  focus = vi.spyOn(HTMLElement.prototype, 'focus');
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  messageStore.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('a first load', () => {
  it.each(['/', '/manage', '/manage/users', '/made-up/today', '/made-up/report', '/no-such-page'])(
    'at %s moves neither focus nor scroll',
    async (path) => {
      serve(usersPath, json(people));
      renderApp(path);
      await settle();

      expect(pageHeading()).toBeInTheDocument();
      expect(document.body).toHaveFocus();
      expect(scrollTo).not.toHaveBeenCalled();
      expect(focus).not.toHaveBeenCalled();
    },
  );

  it('moves nothing on a screen whose <h1> is data, and leaves nothing pending for it', async () => {
    serve(usersPath, after(500, json(people)));
    renderApp(`/manage/users/${alex.id}`);
    await wait(500);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(document.body).toHaveFocus();
  });
});

describe('a new screen', () => {
  it.each([
    { link: 'Grow2Notes', heading: /^Today/ },
    { link: 'Today', heading: /^Today/ },
    { link: 'Flagged', heading: /^Page not found$/ },
    { link: 'Report', heading: /^Page not found$/ },
    { link: 'Manage', heading: /^Manage$/ },
  ])("from the nav link $link focuses the new screen's <h1> and scrolls to the top", async ({ link, heading }) => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();
    scrollPage(600);

    await press(navLink(link));

    expect(pageHeading()).toHaveTextContent(heading);
    expectFocused(pageHeading());
    expectScrolledTo(0);
  });

  it("from a row focuses the new screen's <h1> and scrolls to the top, with nothing in the router state", async () => {
    const router = renderApp('/made-up/today');
    await settle();
    scrollPage(800);

    await press(within(main()).getByRole('link', { name: 'Participant 2' }));

    expect(router.state.location.pathname).toBe('/made-up/notes/2');
    expect(router.state.location.state).toBeNull();
    expectFocused(pageHeading());
    expect(pageHeading()).toHaveAccessibleName('Note 2');
    expectScrolledTo(0);
  });

  it('on a pop, back or forward, comes back to the position kept for its entry, and focuses its <h1>', async () => {
    const router = renderApp('/made-up/today');
    await settle();
    scrollPage(800);
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    scrollPage(150);

    await go(router, -1);

    expect(pageHeading()).toHaveAccessibleName('Today');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(800);

    await go(router, 1);

    expect(pageHeading()).toHaveAccessibleName('Note 2');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(150);
  });

  it('on a pop to an entry with no position kept, as one from before a reload, scrolls to the top', async () => {
    // The history before the reload: Manage, then Today, then a note, which the reload opened. Not from the first
    // entry, whose key is the same in every test.
    const router = renderApp(['/manage', '/made-up/today', '/made-up/notes/1']);
    await settle();
    scrollPage(300);

    await go(router, -1);

    expect(pageHeading()).toHaveAccessibleName('Today');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(0);
  });
});

describe('Submit, Discard and Today', () => {
  it('after Submit, a push to Today focuses the success banner, not the <h1>, and shows the top', async () => {
    const router = renderApp('/made-up/today');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    scrollPage(400);

    await press(within(main()).getByRole('button', { name: 'Submit' }));

    expect(router.state.location.pathname).toBe('/made-up/today');
    const banner = within(main()).getByText(submitted).parentElement!;
    expect(banner).toHaveAttribute('data-route-focus');
    expectFocused(banner);
    expect(pageHeading()).not.toHaveFocus();
    expectScrolledTo(0);
    expect(router.state.location.state).toBeNull();
  });

  it('after Submit, the pop back to Today focuses the banner, not the row, and shows the top', async () => {
    const router = renderApp('/made-up/today');
    await settle();
    scrollPage(800);
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    scrollPage(400);

    await press(within(main()).getByRole('button', { name: 'Submit and go back' }));

    expect(router.state.location.pathname).toBe('/made-up/today');
    expect(router.state.historyAction).toBe('POP');
    expectFocused(within(main()).getByText(submitted).parentElement!);
    // The top, not the position kept for Today's entry.
    expectScrolledTo(0);
  });

  it('shows the message once: not again on coming back to Today', async () => {
    const router = renderApp('/made-up/today');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    await press(within(main()).getByRole('button', { name: 'Submit' }));
    expect(within(main()).getByText(submitted)).toBeInTheDocument();

    await press(navLink('Manage'));
    await act(() => router.navigate('/made-up/today'));
    await settle();

    expect(within(main()).queryByText(submitted)).not.toBeInTheDocument();
    expect(main().querySelector('[data-route-focus]')).toBeNull();
    expectFocused(pageHeading());
    expect(messageStore.peek()).toBeUndefined();
  });

  it('after Discard, a replace to Today focuses its <h1>, not the row, and scrolls to the top', async () => {
    const router = renderApp('/made-up/today');
    await settle();
    scrollPage(800);
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    scrollPage(400);

    await press(within(main()).getByRole('button', { name: 'Discard' }));

    expect(router.state.location.pathname).toBe('/made-up/today');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveAccessibleName('Today');
    expectFocused(pageHeading());
    expectScrolledTo(0);
  });
});

describe('not a new screen', () => {
  it('a ?view= switch, either way, keeps focus on the link pressed and does not scroll', async () => {
    const router = renderApp('/made-up/flagged');
    await settle();
    scrollPage(250);
    const reviewed = within(main()).getByRole('link', { name: 'Reviewed' });

    await press(reviewed);

    expect(router.state.location.search).toBe('?view=reviewed');
    expect(main()).toHaveTextContent('reviewed');
    expect(reviewed).toHaveFocus();

    await go(router, -1);
    expect(router.state.location.search).toBe('');
    await go(router, 1);
    expect(router.state.location.search).toBe('?view=reviewed');

    expect(reviewed).toHaveFocus();
    expect(scrollTo).not.toHaveBeenCalled();
    expect(window.scrollY).toBe(250);
  });

  it('keeps the position for the entry that a ?view= switch adds, for a pop back to it before it scrolls', async () => {
    const router = renderApp('/made-up/flagged');
    await settle();
    scrollPage(250);
    await press(within(main()).getByRole('link', { name: 'Reviewed' }));
    await press(navLink('Manage'));

    await go(router, -1);

    expect(router.state.location.search).toBe('?view=reviewed');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(250);
  });

  it('a nav link to the screen already open keeps focus on the link and does not scroll', async () => {
    const router = renderApp('/manage');
    await settle();
    scrollPage(200);
    const { key } = router.state.location;

    await press(navLink('Manage'));

    expect(router.state.location.key).not.toBe(key);
    expect(navLink('Manage')).toHaveFocus();
    expect(scrollTo).not.toHaveBeenCalled();
  });

  it('the replace after Add participant, its state inScreen, keeps focus on Save and does not scroll', async () => {
    const router = renderApp('/made-up/participants/new');
    await settle();
    scrollPage(250);
    const save = within(main()).getByRole('button', { name: 'Save' });

    await press(save);

    expect(router.state.location.pathname).toBe('/made-up/participants/7');
    expect(router.state.location.state).toEqual({ inScreen: true });
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveAccessibleName('Participant 7');
    expect(save).toHaveFocus();
    expect(scrollTo).not.toHaveBeenCalled();
  });

  it('but a pop back to that entry from another screen is a new screen', async () => {
    const router = renderApp('/made-up/participants/new');
    await settle();
    await press(within(main()).getByRole('button', { name: 'Save' }));
    scrollPage(250);
    await press(navLink('Manage'));

    await go(router, -1);

    expect(router.state.location.state).toEqual({ inScreen: true });
    expect(pageHeading()).toHaveAccessibleName('Participant 7');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(250);
  });

  it('a change between routes of one screen key, pushed or popped, keeps focus and does not scroll', async () => {
    const router = renderApp('/made-up/report');
    await settle();
    scrollPage(300);
    const next = within(main()).getByRole('link', { name: 'Next day' });

    await press(next);
    expect(router.state.location.pathname).toBe('/made-up/report/2026-10-02');
    expect(pageHeading()).toHaveAccessibleName('Daily report 2026-10-02');
    await press(next);
    expect(router.state.location.pathname).toBe('/made-up/report/2026-10-03');
    // Back to the first date's entry, which the address made without a date or any state, as the nav link makes it.
    await go(router, -2);
    expect(router.state.location.pathname).toBe('/made-up/report');

    expect(next).toHaveFocus();
    expect(scrollTo).not.toHaveBeenCalled();
    expect(window.scrollY).toBe(300);
  });

  it('keeps one scroll position for every entry of one screen key', async () => {
    const router = renderApp('/made-up/report');
    await settle();
    scrollPage(300);
    await press(within(main()).getByRole('link', { name: 'Next day' }));
    scrollPage(450);
    await press(navLink('Manage'));
    expectScrolledTo(0);

    // Two back, to the first date's entry, which the page left at 300 before it went on to the next date.
    await go(router, -2);

    expect(router.state.location.pathname).toBe('/made-up/report');
    expectFocused(pageHeading(), { preventScroll: true });
    expectScrolledTo(450);
  });
});

describe('a data <h1>', () => {
  it("on a user's page, the name takes focus as the list arrives", async () => {
    serve(usersPath, after(500, json(people)));
    const router = renderApp('/manage');
    await settle();
    scrollPage(100);

    // As a link to it does.
    await act(() => router.navigate(`/manage/users/${alex.id}`));
    await settle();

    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expectScrolledTo(0);

    await wait(500);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expectFocused(pageHeading(), { preventScroll: true });
  });

  it("the failed load's fallback <h1> takes the focus, with the failure under it", async () => {
    serve(usersPath, noConnection);
    const router = renderApp('/manage');
    await settle();

    await act(() => router.navigate(`/manage/users/${alex.id}`));
    // Past its one silent retry.
    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('User');
    expect(pageHeading()).toHaveFocus();
    expect(main()).toHaveTextContent('This user did not load: no connection. Try again.');
  });

  it('Page not found, for a refused region, takes the focus', async () => {
    serve(usersPath, after(100, problem(403)));
    const router = renderApp('/manage');
    await settle();

    await act(() => router.navigate(`/manage/users/${alex.id}`));
    await wait(100);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(pageHeading()).toHaveFocus();
  });

  it('after a sign-in, the name takes focus as the list arrives', async () => {
    vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
    vi.mocked(startAuthentication).mockResolvedValue(credential);
    serve(mePath, problem(401));
    serve(optionsPath, json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
    serve(passkeyPath, json(manager));
    serve(usersPath, after(500, json(people)));
    renderApp(`/manage/users/${alex.id}`, null);
    await settle();

    await press(within(main()).getByRole('button', { name: 'Sign in with a passkey' }));
    await settle();

    expect(screen.queryByRole('heading', { name: 'Sign in' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();

    await wait(500);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(pageHeading()).toHaveFocus();
  });

  it("after the start-up's Try again, the name takes focus as the list arrives", async () => {
    serve(mePath, noConnection);
    renderApp(`/manage/users/${alex.id}`, null);
    // Past the start-up's one silent retry.
    await wait(2_000);
    expect(pageHeading()).toHaveAccessibleName('Could not connect to Grow2Notes');
    serve(mePath, json(manager));
    serve(usersPath, after(500, json(people)));

    await press(within(main()).getByRole('button', { name: 'Try again' }));

    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();

    await wait(500);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(pageHeading()).toHaveFocus();
  });

  it('after a Try again that loads the list, the name takes focus', async () => {
    serve(usersPath, noConnection);
    const router = renderApp('/manage');
    await settle();
    await act(() => router.navigate(`/manage/users/${alex.id}`));
    await wait(1000);
    await settle();
    serve(usersPath, json(people));

    await press(within(main()).getByRole('button', { name: 'Try again' }));

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(pageHeading()).toHaveFocus();
  });
});

describe('a whole-page message with no location change', () => {
  it('Page not found, in place of a region refused after its fixed <h1> took focus, takes the focus', async () => {
    serve(usersPath, after(100, problem(403)));
    renderApp('/manage');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Users' }));
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(pageHeading()).toHaveFocus();

    await wait(100);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expectFocused(pageHeading());
  });

  it.each([
    {
      cause: 'a refresh that finds the person renamed',
      crashIt: async () => {
        serve(mePath, json(renamed));
        act(() => {
          window.dispatchEvent(new FocusEvent('focus'));
        });
        await settle();
      },
    },
    {
      cause: 'a press',
      crashIt: () => press(within(main()).getByRole('button', { name: 'Mark reviewed' })),
    },
  ])('the crash page, in place of a screen that crashes after $cause, takes the focus', async ({ crashIt }) => {
    const router = renderApp('/manage');
    await settle();
    await act(() => router.navigate('/made-up/crashing'));
    await settle();
    expect(pageHeading()).toHaveFocus();
    const { location } = router.state;

    await crashIt();

    expect(router.state.location).toBe(location);
    expect(pageHeading()).toHaveAccessibleName('There is a problem with Grow2Notes');
    expectFocused(pageHeading());
  });

  it('leaves focus on what had it when that stays, as a nav link does', async () => {
    serve(usersPath, after(100, problem(403)));
    renderApp('/manage');
    await settle();
    await press(within(main()).getByRole('link', { name: 'Users' }));
    navLink('Today').focus();

    await wait(100);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(navLink('Today')).toHaveFocus();
  });
});

describe('memory only', () => {
  it('leaves scrolling to the app, and keeps nothing in storage or the router state', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    const router = renderApp('/made-up/today');
    await settle();
    scrollPage(800);
    await press(within(main()).getByRole('link', { name: 'Participant 2' }));
    await press(within(main()).getByRole('button', { name: 'Submit and go back' }));
    await press(navLink('Manage'));
    await go(router, -1);
    // As the page is left or reloaded, when React Router's <ScrollRestoration> would write its positions to
    // sessionStorage.
    window.dispatchEvent(new Event('pagehide'));

    expect(history.scrollRestoration).toBe('manual');
    expect(setItem).not.toHaveBeenCalled();
    expect(sessionStorage).toHaveLength(0);
    expect(localStorage).toHaveLength(0);
    expect(router.state.location.state).toBeNull();
  });

  it('empties the success-message store with a reload', async () => {
    messageStore.set(submitted);

    // A reload runs the app's modules afresh.
    vi.resetModules();
    const reloaded = await import('./messageStore.ts');

    expect(reloaded.messageStore.peek()).toBeUndefined();
    expect(messageStore.peek()).toBe(submitted);
  });

  it('drops a focus left pending for a heading with a reload', async () => {
    serve(usersPath, noAnswer);
    const router = renderApp('/manage');
    await settle();
    await act(() => router.navigate(`/manage/users/${alex.id}`));
    await settle();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    cleanup();

    // A reload runs the app's modules afresh.
    vi.resetModules();
    const reloaded = await import('../components/PageHeading.tsx');
    const { unmount } = render(<reloaded.PageHeading>Today</reloaded.PageHeading>);

    expect(screen.getByRole('heading', { level: 1 })).not.toHaveFocus();
    expect(document.body).toHaveFocus();

    // Before the reload, the next heading would have taken it.
    unmount();
    render(<PageHeading>Today</PageHeading>);
    expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
  });
});
