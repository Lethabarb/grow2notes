import { QueryClientProvider, useQuery } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, useParams } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError } from '../api/api.ts';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { LoadRegion } from '../components/LoadRegion.tsx';
import { PageHeading } from '../components/PageHeading.tsx';
import { pageName } from '../copy/pageNames.ts';
import { routes, type AppRoute } from './routes.tsx';

const mePath = '/api/auth/me';

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

// Made-up IDs of the made-up record: one that the server refuses or fails, and one it answers.
const refusedId = '2d9f4b61-0c7e-4a53-8e1b-6f3a9c0d2e47';
const foundId = '8b5e1a3c-7f20-4d96-b4c8-1e6d0a9f3b52';

const refusedAddress = `/manage/made-up/${refusedId}`;
const foundAddress = `/manage/made-up/${foundId}`;

// The crash that the made-up screen outside ManagerOnly throws.
const crash = new Error('A made-up crash');

type MadeUpRecord = { id: string; name: string };

/** The made-up record's path in the API, by its ID. */
function recordPath(id: string): string {
  return `/api/made-up/${id}`;
}

/** The path of the made-up record's goals, which the region loads with the record. */
function goalsPath(id: string): string {
  return `/api/made-up/${id}/goals`;
}

/** A screen's query, which passes TanStack's signal to `api()`, as every screen's does. */
function useApi<T>(path: string) {
  return useQuery({ queryKey: [path], queryFn: ({ signal }) => api<T>(path, { signal }) });
}

/** A made-up manager screen of one record: its `<h1>` at once, and a region that loads the record and its goals. */
function MadeUpRecordPage() {
  const id = useParams().id ?? '';
  const record = useApi<MadeUpRecord>(recordPath(id));
  const goals = useApi<string[]>(goalsPath(id));
  return (
    <>
      <PageHeading>Made-up record</PageHeading>
      <LoadRegion queries={[record, goals]} loading="Loading record…" what="This record">
        {/* Called only once every query has data. */}
        {() => <p>{record.data!.name}</p>}
      </LoadRegion>
    </>
  );
}

/** A made-up screen that throws every time it renders. */
function Broken(): never {
  throw crash;
}

// The made-up record, titled with a page name from the list, and the made-up screen that throws.
const recordRoute: AppRoute = {
  path: 'manage/made-up/:id',
  element: <MadeUpRecordPage />,
  handle: { pageName: pageName.user },
};
const brokenRoute: AppRoute = { path: 'made-up', element: <Broken />, handle: { pageName: pageName.participants } };

/** `table` with the made-up record beside Manage under `ManagerOnly`, and the screen that throws beside `*`. */
function withMadeUp(table: AppRoute[]): AppRoute[] {
  return table.flatMap((route) => {
    if (route.path === '*') {
      return [brokenRoute, route];
    }

    if (route.index || route.children === undefined) {
      return [route];
    }

    const children = route.children.some(({ path }) => path === 'manage')
      ? [...route.children, recordRoute]
      : withMadeUp(route.children);
    return [{ ...route, children }];
  });
}

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = () => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** A `200` with a JSON body that comes after `ms`. */
function jsonAfter(ms: number, body: unknown): Answer {
  return () =>
    new Promise((resolve) => {
      setTimeout(() => {
        resolve(json(body)());
      }, ms);
    });
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code` if it has one. */
function problem(status: number, code?: string): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, code });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** The made-up record answered at `id` with `answer`, and its goals with none. */
function serveRecord(id: string, answer: Answer) {
  serve(recordPath(id), answer);
  serve(goalsPath(id), json([]));
}

/**
 * Renders the app at `path` on a memory router built from the route table with the made-up screens, under a query
 * client of its own and StrictMode, as `main.tsx` renders it.
 */
function renderApp(path: string) {
  const client = createQueryClient();
  const router = createMemoryRouter(withMadeUp(routes), { initialEntries: [path] });
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

/** Lets TanStack hand the queries' new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/**
 * The screen's elements, in the `<div>` of `<main>` that the shell holds the route in, which the signed-out-in-place
 * view hides (app-shell.md component 7).
 */
function screenElements(): Element[] {
  return Array.from(main().firstElementChild?.children ?? []);
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The made-up record's region's line, the screen's one status region. */
function line(): HTMLElement {
  return within(main()).getByRole('status');
}

/** The main navigation's links, as their names. */
function navItems(): string[] {
  return within(screen.getByRole('navigation', { name: 'Main' }))
    .getAllByRole('link')
    .map((link) => link.textContent);
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Expects Page not found alone in `<main>`, in order: the page's one `<h1>`, its sentence, Go to Today, and the page's
 * one status region, empty; under a manager's nav, which marks no item; titled "Page not found".
 */
function expectPageNotFound() {
  const heading = pageHeading();
  expect(heading).toHaveAccessibleName('Page not found');
  expect(screenElements().map((element) => element.textContent)).toEqual([
    'Page not found',
    'If you typed or pasted the web address, check it is correct.',
    'Go to Today',
    '',
  ]);
  expect(screen.getAllByRole('heading', { level: 1 })).toEqual([heading]);
  expect(screen.getAllByRole('status')).toHaveLength(1);
  expect(navItems()).toEqual(['Today', 'Flagged', 'Report', 'Manage']);
  expect(currentItems()).toEqual([]);
  expect(document.title).toBe('Grow2Notes – Page not found');
}

/** Expects the made-up record's screen, under the nav, which marks Manage as the section, titled with its page name. */
function expectRecordScreen() {
  expect(pageHeading()).toHaveAccessibleName('Made-up record');
  expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  expect(currentItems()).toEqual(['Manage true']);
  expect(document.title).toBe('Grow2Notes – User');
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path) => {
    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer();
  });
  vi.stubGlobal('fetch', fetchMock);
  serve(mePath, json(manager));
  // React and React Router report each throw that a boundary catches: a refusal and the made-up crash are expected,
  // and anything else shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    const expected = args.some(
      (arg) => arg === crash || (arg instanceof ApiError && (arg.status === 403 || arg.status === 404)),
    );
    if (!expected) {
      report(...args);
    }
  });
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

describe('Page not found for a refused region', () => {
  it.each([403, 404])(
    'shows in place of the whole screen for a %i, at the same address, with the nav marking no item',
    async (status) => {
      serveRecord(refusedId, problem(status));
      const { router } = renderApp(refusedAddress);
      await settle();

      expectPageNotFound();
      expect(router.state.location.pathname).toBe(refusedAddress);
    },
  );

  it('keeps the loading line, for a 404, until a query held 3 s answers, then shows Page not found', async () => {
    serve(recordPath(refusedId), problem(404));
    serve(goalsPath(refusedId), jsonAfter(3000, []));
    renderApp(refusedAddress);

    await wait(1000);
    expect(line()).toHaveTextContent('Loading record…');
    await wait(1999);
    expect(line()).toHaveTextContent('Loading record…');
    expect(pageHeading()).toHaveAccessibleName('Made-up record');

    await wait(1);
    await settle();
    expectPageNotFound();
  });

  it("shows the region's server sentence for a 500, in the screen", async () => {
    serveRecord(refusedId, problem(500));
    renderApp(refusedAddress);

    // After its one silent retry.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('This record did not load: something went wrong. Try again.');
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expectRecordScreen();
  });

  it('shows nothing in the region for a 401, which the session flow has: sign-in again, over the screen', async () => {
    serveRecord(refusedId, problem(401));
    renderApp(refusedAddress);

    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    // The screen, hidden under it as it was, with its region's line empty and no Try again.
    const hiddenScreen = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenScreen === null) {
      throw new Error('No screen is hidden in <main>.');
    }

    const kept = within(hiddenScreen);
    expect(kept.getByRole('heading', { level: 1, hidden: true })).toHaveAccessibleName('Made-up record');
    expect(kept.getByRole('status', { hidden: true })).toBeEmptyDOMElement();
    expect(kept.queryByRole('button', { hidden: true })).not.toBeInTheDocument();
  });

  it("shows the screen of an ID answered 200 next, with Manage marked and the route's title back", async () => {
    serveRecord(refusedId, problem(404));
    serveRecord(foundId, json({ id: foundId, name: 'Riverbend house' }));
    const { router } = renderApp(refusedAddress);
    await settle();
    expectPageNotFound();

    // As a link to it does.
    await act(() => router.navigate(foundAddress));
    await settle();

    expect(within(main()).getByText('Riverbend house')).toBeInTheDocument();
    expectRecordScreen();
  });

  it('shows when a Try again after no connection is answered 404', async () => {
    serveRecord(refusedId, noConnection);
    renderApp(refusedAddress);
    // After its one silent retry.
    await wait(1000);
    await settle();
    expect(line().textContent).toBe('This record did not load: no connection. Try again.');
    serve(recordPath(refusedId), problem(404));

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await settle();

    expectPageNotFound();
  });

  it('leaves the screen as it was when a refetch in the background is answered 404', async () => {
    serveRecord(foundId, json({ id: foundId, name: 'Riverbend house' }));
    const { client } = renderApp(foundAddress);
    await settle();
    serve(recordPath(foundId), problem(404));

    act(() => {
      void client.refetchQueries({ queryKey: [recordPath(foundId)] });
    });
    await settle();

    expect(client.getQueryState([recordPath(foundId)])?.status).toBe('error');
    expect(within(main()).getByText('Riverbend house')).toBeInTheDocument();
    expect(line()).toBeEmptyDOMElement();
    expectRecordScreen();
  });
});

describe('You cannot edit this note for a region that a refused edit failed', () => {
  it('shows in place of the whole screen for a 403 note.not_editable, under the nav, with the route title kept', async () => {
    serveRecord(refusedId, problem(403, 'note.not_editable'));
    const { router } = renderApp(refusedAddress);
    await settle();

    const heading = pageHeading();
    expect(heading).toHaveAccessibleName('You cannot edit this note');
    expect(screenElements().map((element) => element.textContent)).toEqual([
      'You cannot edit this note',
      'Only the person who wrote it, or a manager, can edit it.',
      'Read the note',
      '',
    ]);
    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([heading]);
    expect(navItems()).toEqual(['Today', 'Flagged', 'Report', 'Manage']);
    // It is not Page not found, so the shell keeps the route's own title and marks its section.
    expect(document.title).toBe('Grow2Notes – User');
    expect(currentItems()).toEqual(['Manage true']);
    expect(router.state.location.pathname).toBe(refusedAddress);
  });

  it('shows after the same address was answered 200 before, and gives way to a screen answered 200 next', async () => {
    serveRecord(foundId, json({ id: foundId, name: 'Riverbend house' }));
    serveRecord(refusedId, problem(403, 'note.not_editable'));
    const { router } = renderApp(foundAddress);
    await settle();

    await act(() => router.navigate(refusedAddress));
    await settle();
    expect(pageHeading()).toHaveAccessibleName('You cannot edit this note');

    await act(() => router.navigate(foundAddress));
    await settle();
    expectRecordScreen();
  });
});

describe('Page not found in place of a route', () => {
  it('gives a worker whom a refresh makes a manager Manage on /manage, titled Manage', async () => {
    serve(mePath, json(worker));
    const { router } = renderApp('/manage');
    await settle();
    const { location } = router.state;
    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');

    serve(mePath, json(manager));
    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Manage');
    expect(document.title).toBe('Grow2Notes – Manage');
    expect(currentItems()).toEqual(['Manage page']);
    expect(router.state.location).toBe(location);
  });

  it("keeps its title as it takes the place of a screen's crash page, for a worker going to /manage", async () => {
    serve(mePath, json(worker));
    const { router } = renderApp('/made-up');
    await settle();
    expect(pageHeading()).toHaveAccessibleName('There is a problem with Grow2Notes');

    await act(() => router.navigate('/manage'));
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
  });
});
