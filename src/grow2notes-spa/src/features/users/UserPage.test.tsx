import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me, type Role } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminUser, UserStatus } from './usersQuery.ts';

const mePath = '/api/auth/me';
const usersPath = '/api/admin/users';

// users.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts, and a
// worker who is not on the list.
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

/** A made-up user, invited on 1 September, set up the next day unless still Invited, and deactivated if so. */
function madeUp(id: string, displayName: string, email: string, status: UserStatus, role: Role = 'Worker'): AdminUser {
  return {
    id,
    email,
    displayName,
    role,
    status,
    invitedAtUtc: '2026-09-01T00:00:00Z',
    activatedAtUtc: status === 'Invited' ? null : '2026-09-02T00:00:00Z',
    deactivatedAtUtc: status === 'Deactivated' ? '2026-09-30T00:00:00Z' : null,
    concurrencyStamp: `stamp-${id}`,
  };
}

const sam = madeUp(manager.userId, 'Sam Lee', 'sam.lee@example.org', 'Active', 'Manager');
const jo = madeUp('0c6f2e8a-4b17-4d39-a5e2-7f1b3c9d0a64', 'Jo Smith', 'jo.smith@example.org', 'Active');
const alex = madeUp('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Alex Park', 'alex.park@example.org', 'Invited');
const chris = madeUp('7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20', 'Chris Ng', 'chris.ng@example.org', 'Deactivated');

const people = [sam, chris, jo, alex];

// A made-up ID that is on no list.
const unknownId = '4f2a9c7e-1b38-4d65-a0e9-8c3b5d1f7a26';

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
function renderApp(path: string, me: Me = manager) {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  const router = createMemoryRouter(routes, { initialEntries: [path] });
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

/** The shell's before-main bar slot, between `<header>` and `<main>`. */
function pageBar(): HTMLElement {
  const slot = document.getElementById('page-bar');
  if (slot === null) {
    throw new Error('The shell rendered no #page-bar.');
  }

  return slot;
}

/** The back link to the list, wherever it is. */
function backLink(): HTMLElement | null {
  return screen.queryByRole('link', { name: 'Users' });
}

/** The region's line: the status region in `<main>` that shows, beside the page's visually hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The summary list's rows, each a `<div>` holding exactly a `<dt>` and its `<dd>`, as their texts. */
function facts(): string[][] {
  const lists = main().querySelectorAll('dl');
  expect(lists).toHaveLength(1);
  return Array.from(lists[0].children, (row) => {
    expect(row.tagName).toBe('DIV');
    expect(Array.from(row.children, (cell) => cell.tagName)).toEqual(['DT', 'DD']);
    return Array.from(row.children, (cell) => cell.textContent);
  });
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Expects Page not found alone in `<main>`, in order: its `<h1>`, its sentence, Go to Today and its status region,
 * empty; with no back link in the page bar; under the nav, which marks no item; titled "Page not found".
 */
function expectPageNotFound() {
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(Array.from(main().children, (element) => element.textContent)).toEqual([
    'Page not found',
    'If you typed or pasted the web address, check it is correct.',
    'Go to Today',
    '',
  ]);
  expect(within(main()).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(backLink()).not.toBeInTheDocument();
  expect(pageBar()).toBeEmptyDOMElement();
  expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
  expect(currentItems()).toEqual([]);
  expect(document.title).toBe('Grow2Notes – Page not found');
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain(): HTMLElement {
  const button = within(main()).getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
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
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('User', () => {
  it.each([
    { user: alex, role: 'Worker', status: 'Invited' },
    { user: jo, role: 'Worker', status: 'Active' },
    { user: sam, role: 'Manager', status: 'Active' },
    { user: chris, role: 'Worker', status: 'Deactivated' },
  ])(
    'shows $status $user.displayName: the name as the <h1>, the email, role and status, and Edit alone',
    async ({ user, role, status }) => {
      serve(usersPath, json(people));
      renderApp(`/manage/users/${user.id}`);
      await settle();

      expect(pageHeading()).toHaveAccessibleName(user.displayName);
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(facts()).toEqual([
        ['Email address', user.email],
        ['Role', role],
        ['Status', status],
      ]);
      const edit = within(main()).getByRole('link', { name: 'Edit' });
      expect(edit).toHaveAttribute('href', `/manage/users/${user.id}/edit`);
      expect(edit).toHaveClass(...buttonClass('secondary').split(' '));
      expect(within(main()).getAllByRole('link')).toEqual([edit]);
      expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
      expect(document.querySelector('[disabled]')).toBeNull();
      expect(line()).toBeEmptyDOMElement();
    },
  );

  it('is titled "User", with Manage marked, and holds no name or email in its title or address', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${alex.id}`);
    await settle();

    expect(document.title).toBe('Grow2Notes – User');
    expect(currentItems()).toEqual(['Manage true']);
    const { pathname, search, hash } = router.state.location;
    for (const text of [document.title, `${pathname}${search}${hash}`]) {
      expect(text).not.toContain(alex.displayName);
      expect(text).not.toContain(alex.email);
    }
  });

  it('has its back link, named "Users", to the list, in the page bar outside <main>, which it opens', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${alex.id}`);
    await settle();

    const link = backLink();
    expect(link).toHaveAccessibleName('Users');
    expect(link).toHaveAttribute('href', '/manage/users');
    expect(pageBar()).toContainElement(link);
    expect(main()).not.toContainElement(link);

    fireEvent.click(link!);
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
  });

  it('opens from its row of the list, with nothing in the router state', async () => {
    serve(usersPath, json(people));
    const router = renderApp('/manage/users');
    await settle();

    fireEvent.click(within(main()).getByRole('link', { name: 'Alex Park' }));
    await settle();

    expect(router.state.location.pathname).toBe(`/manage/users/${alex.id}`);
    expect(router.state.location.state).toBeNull();
    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(facts()[0]).toEqual(['Email address', 'alex.park@example.org']);
    expect(document.title).toBe('Grow2Notes – User');
  });
});

describe('User while the list loads', () => {
  it('shows its back link and no <h1>, then nothing more until "Loading user…" at 1 s', async () => {
    serve(usersPath, noAnswer);
    renderApp(`/manage/users/${alex.id}`);

    expect(backLink()).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expect(main().textContent).toBe('');

    await wait(1);
    expect(line()).toHaveTextContent('Loading user…');
    expect(main().textContent).toBe('Loading user…');
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('says the user did not load, under the <h1> "User", with Try again, after its silent retry', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);

    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('User');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(line().textContent).toBe('This user did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(main().querySelector('dl')).toBeNull();
    expect(backLink()).toBeInTheDocument();
  });

  it('says the user still did not load after a Try again that fails, with focus still on Try again', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('This user still did not load: no connection. Try again.');
    expect(pageHeading()).toHaveAccessibleName('User');
    expect(button).toHaveFocus();
  });

  it('shows the user after a Try again that loads, with focus on their name, the one <h1>', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);
    await wait(1000);
    await settle();
    serve(usersPath, json(people));

    pressTryAgain();
    await settle();

    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([pageHeading()]);
    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(pageHeading()).toHaveFocus();
    expect(line()).toBeEmptyDOMElement();
  });

  it('shows Page not found in place of the whole page for a 403, at the same address', async () => {
    serve(usersPath, problem(403));
    const router = renderApp(`/manage/users/${alex.id}`);

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe(`/manage/users/${alex.id}`);
  });
});

describe('User for an ID not on the list', () => {
  it('shows Page not found in place of the whole page, at the same address', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${unknownId}`);

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe(`/manage/users/${unknownId}`);
  });
});

describe('User for a worker', () => {
  it('shows Page not found, and sends no request for the list', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${alex.id}`, worker);

    await wait(60_000);

    expectPageNotFound();
    expect(sent(usersPath)).toBe(0);
  });
});
