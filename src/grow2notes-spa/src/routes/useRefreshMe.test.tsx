import { focusManager, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { getPhase, subscribe, type Phase } from '../api/session.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { routes } from './routes.tsx';

// jsdom does not navigate, so the reload for another person is a mock that the tests read.
vi.mock('./fullPageLoad.ts');

const mePath = '/api/auth/me';
const todayPath = '/api/today';

// A made-up worker and manager (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

/** How the server meets a `GET /api/auth/me`: an answer, or a failure as `fetch` has one. */
type Answer = () => Promise<Response>;

/** A `200` with `me`. */
function answerWith(me: Me): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** `answer`, held back until `release()` is called, and given at once from then on. */
function heldBack(answer: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: () => released.then(answer), release };
}

const fetchMock = vi.fn<typeof fetch>();

// How the server meets each request from now on, which a test changes between the start-up and a refresh.
let server: Answer;

/**
 * Today's list as the server answers it as it answers `/me` now: for the day of the `Me` that `server` gives, with no
 * participants, or as `/me` fails.
 */
async function listOfTheDay(): Promise<Response> {
  const me = await server();
  if (!me.ok) {
    return me;
  }

  const { today } = (await me.json()) as Me;
  return new Response(JSON.stringify({ date: today, participants: [] }), {
    headers: { 'Content-Type': 'application/json' },
  });
}

type Router = ReturnType<typeof createMemoryRouter>;

/**
 * Renders the app at the last of `entries`, with the others behind it in the history, on a memory router built from
 * the route table, under a query client of its own and StrictMode, which runs each effect twice as it mounts, as
 * `main.tsx` renders it.
 */
function renderApp(...entries: string[]) {
  const client = createQueryClient();
  // Each entry with a key of its own, as a browser gives it, where a memory router gives every test's first entry the
  // key "default", under which the shell's memory would carry a row or a position from one test into the next.
  const initialEntries = entries.map((entry) => ({ ...parsePath(entry), key: crypto.randomUUID() }));
  const router = createMemoryRouter(routes, { initialEntries, initialIndex: entries.length - 1 });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return { client, router };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** How many requests have gone to `GET /api/auth/me`. */
function meRequests() {
  return fetchMock.mock.calls.filter(([path, init]) => path === mePath && init?.method === 'GET').length;
}

/** The window regains focus, as the browser says so. */
function focusWindow() {
  window.dispatchEvent(new FocusEvent('focus'));
}

/** The page becomes `state`, as the browser says so: at the document, from which the event bubbles to the window. */
function becomes(state: DocumentVisibilityState) {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
  document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(screen.getByRole('main')).getByRole('heading', { level: 1 });
}

/** The main navigation. */
function nav(): HTMLElement {
  return screen.getByRole('navigation', { name: 'Main' });
}

/** The main navigation's links, as their names. */
function navItems(): string[] {
  return within(nav())
    .getAllByRole('link')
    .map((link) => link.textContent);
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Watches every status region in the page, those in a hidden panel too, and gives the function that stops and gives
 * each change made to one since, so that words written and taken out again are found as well.
 */
function watchStatusRegions(): () => MutationRecord[] {
  const changes: MutationRecord[] = [];
  const observer = new MutationObserver((records) => {
    changes.push(...records);
  });
  const regions = document.querySelectorAll('[role="status"]');
  if (regions.length === 0) {
    throw new Error('The page has no status region to watch.');
  }

  for (const region of regions) {
    observer.observe(region, { childList: true, characterData: true, subtree: true });
  }

  return () => {
    changes.push(...observer.takeRecords());
    observer.disconnect();
    return changes;
  };
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock.mockImplementation((path) => (path === todayPath ? listOfTheDay() : server()));
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  focusManager.setFocused(undefined);
  fetchMock.mockReset();
  vi.mocked(fullPageLoad).mockReset();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the refresh of /api/auth/me', () => {
  it("sends one GET /api/auth/me at start-up, and nothing else but the screen's own request", async () => {
    server = answerWith(manager);
    renderApp('/');
    await wait(0);

    expect(meRequests()).toBe(1);
    expect(new Set(fetchMock.mock.calls.map(([path]) => path))).toEqual(new Set([mePath, todayPath]));
  });

  // Each once the start-up's request has been answered.
  it.each<{ trigger: string; entries?: string[]; at?: string; fire: (router: Router) => unknown }>([
    {
      trigger: 'a nav link',
      at: '/manage',
      fire: () => fireEvent.click(within(nav()).getByRole('link', { name: 'Manage' })),
    },
    {
      trigger: 'a link to the address already open',
      fire: () => fireEvent.click(within(nav()).getByRole('link', { name: 'Today' })),
    },
    { trigger: 'Back', entries: ['/manage', '/'], at: '/manage', fire: (router) => router.navigate(-1) },
    { trigger: 'a window focus', fire: focusWindow },
    { trigger: 'visibilitychange to visible', fire: () => becomes('visible') },
    {
      trigger: 'focus and visibilitychange to visible, fired together',
      fire: () => {
        becomes('visible');
        focusWindow();
      },
    },
  ])('sends one more for $trigger', async ({ entries = ['/'], at = '/', fire }) => {
    server = answerWith(manager);
    const { router } = renderApp(...entries);
    await wait(0);
    expect(meRequests()).toBe(1);

    await act(async () => {
      await fire(router);
    });
    // Long enough for any request after the one expected, such as a retry, to have been sent.
    await wait(60_000);

    expect(meRequests()).toBe(2);
    expect(router.state.location.pathname).toBe(at);
  });

  it.each([
    { trigger: 'visibilitychange to hidden', fire: () => becomes('hidden') },
    {
      trigger: "TanStack's own focus",
      fire: () => {
        focusManager.setFocused(false);
        focusManager.setFocused(true);
      },
    },
    { trigger: '30 minutes with nothing done', fire: () => undefined },
  ])('sends none for $trigger', async ({ fire }) => {
    server = answerWith(manager);
    renderApp('/');
    await wait(0);

    act(fire);
    await wait(30 * 60_000);

    expect(meRequests()).toBe(1);
  });

  it.each([
    { failure: 'no connection', answer: noConnection },
    { failure: 'a 503', answer: problem(503) },
  ])(
    'leaves the screen, the nav, the title and focus as they were after $failure, and writes to no status region',
    async ({ answer }) => {
      server = answerWith(manager);
      const { client } = renderApp('/');
      await wait(0);
      const heading = pageHeading();
      const navigation = nav();
      const items = navItems();
      const report = within(navigation).getByRole('link', { name: 'Report' });
      report.focus();
      const stopWatching = watchStatusRegions();

      server = answer;
      act(focusWindow);
      // Past the query client's one silent retry.
      await wait(60_000);

      expect(client.getQueryState(meQuery.queryKey)).toMatchObject({ status: 'error', fetchStatus: 'idle' });
      expect(pageHeading()).toBe(heading);
      expect(heading).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
      expect(nav()).toBe(navigation);
      expect(navItems()).toEqual(items);
      expect(report).toHaveAttribute('href', '/reports/daily/2026-10-01');
      expect(document.title).toBe('Grow2Notes – Today');
      expect(report).toHaveFocus();
      expect(stopWatching()).toEqual([]);
    },
  );

  // A 401 has ended the session (app-shell.md *Window regains focus*).
  it('shows the signed-out-in-place view after a 401, at the same address, over the screen as it was', async () => {
    server = answerWith(manager);
    const { client, router } = renderApp('/');
    await wait(0);
    const heading = pageHeading();
    const { location } = router.state;
    within(nav()).getByRole('link', { name: 'Report' }).focus();

    server = problem(401);
    act(focusWindow);
    // Long enough for a retry, which a 4xx never gets.
    await wait(60_000);

    expect(meRequests()).toBe(2);
    expect(client.getQueryState(meQuery.queryKey)).toMatchObject({ status: 'error', fetchStatus: 'idle' });
    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(pageHeading()).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
    expect(router.state.location).toBe(location);
    // Today's heading, the same element, hidden with its screen.
    expect(heading).toBeInTheDocument();
    expect(heading.closest('[hidden]')).not.toBeNull();
    expect(heading).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
  });

  it.each([
    { failure: 'a 401', answer: problem(401), sent: 2 },
    { failure: 'no connection', answer: noConnection, sent: 3 },
    { failure: 'a 503', answer: problem(503), sent: 3 },
  ])("leaves sign-in's view A as it was while a refresh is in flight, and after $failure", async ({ answer, sent }) => {
    server = problem(401);
    renderApp('/');
    await wait(0);
    const heading = pageHeading();
    expect(heading).toHaveAccessibleName('Sign in');

    const held = heldBack(answer);
    server = held.answer;
    act(focusWindow);
    await wait(5_000);

    expect(pageHeading()).toBe(heading);
    expect(document.title).toBe('Grow2Notes – Sign in');

    held.release();
    // Past the query client's one silent retry.
    await wait(60_000);

    expect(meRequests()).toBe(sent);
    expect(pageHeading()).toBe(heading);
    expect(document.title).toBe('Grow2Notes – Sign in');
  });

  it("shows the app in view A's place when a refresh answers 200, as after a sign-in in another tab", async () => {
    server = problem(401);
    renderApp('/');
    await wait(0);
    expect(pageHeading()).toHaveAccessibleName('Sign in');

    server = answerWith(worker);
    act(focusWindow);
    await wait(0);

    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(document.title).toBe('Grow2Notes – Today');
  });

  it("asks on setup's pages too, which a 401 leaves as they were", async () => {
    server = problem(401);
    renderApp('/setup/password');
    await wait(0);
    const heading = pageHeading();
    expect(heading).toHaveAccessibleName('Set up your account');

    act(focusWindow);
    await wait(0);

    expect(meRequests()).toBe(2);
    expect(pageHeading()).toBe(heading);
    expect(document.title).toBe('Grow2Notes – Set up your account');
  });

  it("moves the Report link and Today's heading to a new today, with no reload", async () => {
    server = answerWith(manager);
    const { router } = renderApp('/');
    await wait(0);
    const heading = pageHeading();
    const report = within(nav()).getByRole('link', { name: 'Report' });
    const { location } = router.state;
    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-01');

    // Past midnight in Melbourne, the server's today has moved on (A33).
    server = answerWith({ ...manager, today: '2026-10-02' });
    act(focusWindow);
    await wait(0);

    expect(meRequests()).toBe(2);
    // The same elements at the same location: nothing was loaded or mounted again.
    expect(within(nav()).getByRole('link', { name: 'Report' })).toBe(report);
    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-02');
    expect(pageHeading()).toBe(heading);
    expect(heading).toHaveAccessibleName(`Today, Friday 2${nbsp}October`);
    expect(heading.querySelector('time')).toHaveAttribute('datetime', '2026-10-02');
    expect(router.state.location).toBe(location);
  });

  it.each([
    { from: worker, to: 'Manager', before: ['Today'], after: ['Today', 'Flagged', 'Report', 'Manage'] },
    { from: manager, to: 'Worker', before: ['Today', 'Flagged', 'Report', 'Manage'], after: ['Today'] },
  ] as const)('rebuilds the nav at once for a $from.role made a $to', async ({ from, to, before, after }) => {
    server = answerWith(from);
    const { router } = renderApp('/');
    await wait(0);
    const { location } = router.state;
    expect(navItems()).toEqual(before);

    server = answerWith({ ...from, role: to });
    act(focusWindow);
    await wait(0);

    expect(navItems()).toEqual(after);
    expect(currentItems()).toEqual(['Today page']);
    expect(router.state.location).toBe(location);
    expect(fullPageLoad).not.toHaveBeenCalled();
  });

  it('shows a manager made a worker Page not found on /manage', async () => {
    server = answerWith(manager);
    const { router } = renderApp('/manage');
    await wait(0);
    const { location } = router.state;
    expect(pageHeading()).toHaveAccessibleName('Manage');

    server = answerWith({ ...manager, role: 'Worker' });
    act(focusWindow);
    await wait(0);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(navItems()).toEqual(['Today']);
    expect(currentItems()).toEqual([]);
    expect(router.state.location).toBe(location);
    expect(fullPageLoad).not.toHaveBeenCalled();
  });

  // A sign-in in another tab of the same browser, by someone else, whose cookie the refresh went with (T01's Notes).
  it('loads the page afresh, once, when a refresh answers for another person', async () => {
    server = answerWith(manager);
    renderApp('/manage');
    await wait(0);

    server = answerWith(worker);
    act(focusWindow);
    await wait(0);
    act(focusWindow);
    await wait(0);

    expect(meRequests()).toBe(3);
    expect(fullPageLoad).toHaveBeenCalledOnce();
  });
});

describe('the session clock', () => {
  it("runs for the shell's whole life, held on the visible trigger's refresh and its silent retry", async () => {
    server = answerWith(worker);
    renderApp('/');
    await wait(0);
    const told: Phase[] = [];
    const unsubscribe = subscribe(() => {
      told.push(getPhase());
    });

    act(() => {
      becomes('hidden');
    });
    await wait(28 * 60_000 + 30_000);
    const held = heldBack(noConnection);
    server = held.answer;
    act(() => {
      becomes('visible');
    });
    await wait(5_000);
    held.release();
    await wait(0);

    expect(meRequests()).toBe(2);
    expect(getPhase()).toBe('active');

    // The query client's silent retry, after which the time decides.
    await wait(1_000);

    expect(meRequests()).toBe(3);
    expect(getPhase()).toBe('warn-2m');

    cleanup();

    expect(getPhase()).toBe('active');
    expect(told).toEqual(['warn-2m', 'active']);
    unsubscribe();
  });

  /** Renders the worker's Today, and hides the page for 31 minutes, with no request sent. */
  async function hideTodayFor31Minutes() {
    server = answerWith(worker);
    renderApp('/');
    await wait(0);
    act(() => {
      becomes('hidden');
    });
    await wait(31 * 60_000);
  }

  // app-shell.md *Session*: "Hidden for 31 minutes, then visible: no warning flashes; the /me result decides".
  it("after 31 minutes hidden, shows the signed-out view for the refresh's 401, with no warning first", async () => {
    await hideTodayFor31Minutes();
    const heading = pageHeading();

    server = problem(401);
    act(() => {
      becomes('visible');
    });
    await wait(0);

    expect(meRequests()).toBe(2);
    expect(document.querySelector('dialog')).not.toHaveAttribute('open');
    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(pageHeading()).toHaveFocus();
    expect(heading.closest('[hidden]')).not.toBeNull();
    expect(document.title).toBe('Grow2Notes – Sign in');
  });

  it("after 31 minutes hidden, leaves the screen as it was for the refresh's 200, with no warning", async () => {
    await hideTodayFor31Minutes();
    const heading = pageHeading();
    const navigation = nav();
    const stopWatching = watchStatusRegions();

    act(() => {
      becomes('visible');
    });
    await wait(0);

    expect(meRequests()).toBe(2);
    expect(document.querySelector('dialog')).not.toHaveAttribute('open');
    expect(pageHeading()).toBe(heading);
    expect(heading.closest('[hidden]')).toBeNull();
    expect(nav()).toBe(navigation);
    expect(document.title).toBe('Grow2Notes – Today');
    expect(stopWatching()).toEqual([]);
  });
});
