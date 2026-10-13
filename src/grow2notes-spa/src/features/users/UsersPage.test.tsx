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

// In no particular order, as the API sorts nothing (design.md §6.6).
const people = [sam, chris, jo, alex];

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

/** As the browser does when the window comes back into view, on which TanStack refetches the queries that opt in. */
function returnToWindow() {
  act(() => {
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** Invite user. */
function inviteUser(): HTMLElement {
  return within(main()).getByRole('link', { name: 'Invite user' });
}

/** The region's line: the status region in `<main>` that shows, beside the page's visually hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The lists in `<main>`, each as the names of its rows' links, in order, every row holding exactly one link. */
function listedNames(): string[][] {
  return within(main())
    .queryAllByRole('list')
    .map((list) =>
      within(list)
        .getAllByRole('listitem')
        .map((row) => {
          const links = within(row).getAllByRole('link');
          expect(links).toHaveLength(1);
          return links[0].textContent;
        }),
    );
}

/** The detail under a row's link, as the lines it shows, without what only screen readers get. */
function shownLines(link: HTMLElement): string[] {
  const detail = document.getElementById(link.getAttribute('aria-describedby') ?? '');
  return Array.from(detail?.children ?? [], (line) => {
    const shown = line.cloneNode(true) as HTMLElement;
    shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
      hidden.remove();
    });
    return shown.textContent;
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
  expect(within(main()).queryByRole('link', { name: 'Invite user' })).not.toBeInTheDocument();
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

describe('Users', () => {
  it('lists the current users by name, then the Deactivated under their own <h2>', async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();

    expect(listedNames()).toEqual([['Alex Park', 'Jo Smith', 'Sam Lee'], ['Chris Ng']]);
    const [current, deactivated] = within(main()).getAllByRole('list');
    const heading = within(main()).getByRole('heading', { level: 2 });
    expect(heading).toHaveAccessibleName('Deactivated');
    expect(current.compareDocumentPosition(heading)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(heading.compareDocumentPosition(deactivated)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(document.title).toBe('Grow2Notes – Users');
    expect(currentItems()).toEqual(['Manage true']);
  });

  it.each([
    {
      user: alex,
      lines: ['alex.park@example.org', 'Invited · Worker'],
      description: 'alex.park@example.org, Invited, Worker',
    },
    {
      user: jo,
      lines: ['jo.smith@example.org', 'Active · Worker'],
      description: 'jo.smith@example.org, Active, Worker',
    },
    {
      user: sam,
      lines: ['sam.lee@example.org', 'Active · Manager'],
      description: 'sam.lee@example.org, Active, Manager',
    },
    {
      user: chris,
      lines: ['chris.ng@example.org', 'Deactivated · Worker'],
      description: 'chris.ng@example.org, Deactivated, Worker',
    },
  ])(
    "gives $user.displayName's row one link, named by the name alone, described by the email, status and role",
    async ({ user, lines, description }) => {
      serve(usersPath, json(people));
      renderApp('/manage/users');
      await settle();

      const link = within(main()).getByRole('link', { name: user.displayName });
      expect(link).toHaveAccessibleName(user.displayName);
      expect(link).toHaveAccessibleDescription(description);
      expect(link).toHaveAttribute('href', `/manage/users/${user.id}`);
      expect(shownLines(link)).toEqual(lines);
    },
  );

  it("gives the viewer's own row no \"(you)\"", async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();

    const own = within(main()).getByRole('link', { name: 'Sam Lee' });
    expect(own.closest('li')).not.toHaveTextContent('(you)');
  });

  it('sorts a name with an accent and one in lower case as the collator does, in each list', async () => {
    const zara = madeUp('1f7c3a9e-5d21-4b48-8e6f-0a2c4d7b9e13', 'Zara Hill', 'zara.hill@example.org', 'Active');
    const david = madeUp('6b2e8d4a-0f37-4c95-a1d6-3e9f7b5c2a80', 'david kim', 'david.kim@example.org', 'Invited');
    const emile = madeUp('9e4a1c7f-3b62-4d05-b8e9-6c1f2a4d8e37', 'Émile Durand', 'emile.durand@example.org', 'Active');
    const elise = madeUp(
      '2c9f6b3e-8a14-4e70-9d25-4b7e1f0a6c58',
      'Élise Moreau',
      'elise.moreau@example.org',
      'Deactivated',
    );
    const ben = madeUp('5d3b9f1a-6e48-4a27-b0c3-8f2d5e7a1b96', 'ben hall', 'ben.hall@example.org', 'Deactivated');
    serve(usersPath, json([zara, elise, david, sam, chris, emile, ben, alex]));
    renderApp('/manage/users');
    await settle();

    const expected = [
      ['Alex Park', 'david kim', 'Émile Durand', 'Sam Lee', 'Zara Hill'],
      ['ben hall', 'Chris Ng', 'Élise Moreau'],
    ];
    expect(listedNames()).toEqual(expected);
    // A sort by code unit would put a name in lower case, and then one with an accent, after Z.
    expect(expected.map((names) => names.toSorted())).not.toEqual(expected);
  });

  it('keeps the API order of names that differ only in letter case or an accent, which the collator ties', async () => {
    const accent = madeUp('4a8e2c6f-1b39-4d57-9f0a-7c3e5b1d8a26', 'Zoë Hill', 'zoe.hill@example.org', 'Active');
    const lower = madeUp('8d1f5b3a-6c24-4e98-a7b0-2e9c4f6a1d53', 'zoe hill', 'z.hill@example.org', 'Active');
    const upper = madeUp('2b7d9e4c-3f15-4a60-8c2b-5d8a1e7f3c09', 'Zoe Hill', 'zoe.h@example.org', 'Active');
    serve(usersPath, json([accent, lower, sam, upper]));
    renderApp('/manage/users');
    await settle();

    const ties = ['Zoë Hill', 'zoe hill', 'Zoe Hill'];
    expect(listedNames()).toEqual([['Sam Lee', ...ties]]);
    // Without `sensitivity: 'base'`, or with localeCompare, case and accent would order them.
    expect(ties.toSorted(new Intl.Collator('en-AU').compare)).not.toEqual(ties);
  });

  // Vite lowers syntax to its build targets but adds no built-in method, and Firefox 114, one of those targets, has no
  // Array.prototype.toSorted, which came in Firefox 115.
  it('lists the users in a browser with no Array.prototype.toSorted, as Firefox 114, a build target, is', async () => {
    const toSorted = Object.getOwnPropertyDescriptor(Array.prototype, 'toSorted');
    Reflect.deleteProperty(Array.prototype, 'toSorted');
    try {
      serve(usersPath, json(people));
      renderApp('/manage/users');
      await settle();

      expect(listedNames()).toEqual([['Alex Park', 'Jo Smith', 'Sam Lee'], ['Chris Ng']]);
    } finally {
      Object.defineProperty(Array.prototype, 'toSorted', toSorted!);
    }
  });

  it('shows one list and no <h2> when nobody is Deactivated', async () => {
    serve(usersPath, json([sam, jo, alex]));
    renderApp('/manage/users');
    await settle();

    expect(listedNames()).toEqual([['Alex Park', 'Jo Smith', 'Sam Lee']]);
    expect(within(main()).queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
  });

  it('shows its <h1> and Invite user, a primary-styled link to the invite page, before the list', async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    const link = inviteUser();
    expect(link).toHaveAttribute('href', '/manage/users/new');
    expect(link).toHaveClass(...buttonClass('primary').split(' '));
    expect(pageHeading().compareDocumentPosition(link)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(link.compareDocumentPosition(within(main()).getAllByRole('list')[0])).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });
});

describe('Users while the list loads', () => {
  it('shows its <h1> and Invite user at once, then nothing more until "Loading users…" at 1 s', async () => {
    serve(usersPath, noAnswer);
    renderApp('/manage/users');

    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(inviteUser()).toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    // No rows, and no empty sentence.
    expect(main().textContent).toBe('UsersInvite user');

    await wait(1);
    expect(line()).toHaveTextContent('Loading users…');
    expect(main().textContent).toBe('UsersInvite userLoading users…');
  });

  it('says the list did not load, with Try again, once its silent retry has found no connection too', async () => {
    serve(usersPath, noConnection);
    renderApp('/manage/users');

    await wait(999);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    await wait(1);
    await settle();

    expect(line().textContent).toBe('The user list did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(within(main()).queryByRole('list')).not.toBeInTheDocument();
    expect(pageHeading()).toHaveAccessibleName('Users');
  });

  it('says the list still did not load after a Try again that fails, with focus still on Try again', async () => {
    serve(usersPath, noConnection);
    renderApp('/manage/users');
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The user list still did not load: no connection. Try again.');
    expect(button).toHaveFocus();
  });

  it('shows the rows after a Try again that loads, with focus on the <h1>', async () => {
    serve(usersPath, noConnection);
    renderApp('/manage/users');
    await wait(1000);
    await settle();
    serve(usersPath, json(people));

    pressTryAgain();
    await settle();

    expect(listedNames()).toEqual([['Alex Park', 'Jo Smith', 'Sam Lee'], ['Chris Ng']]);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });

  it('says something went wrong for a 500, after its silent retry', async () => {
    serve(usersPath, problem(500));
    renderApp('/manage/users');

    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The user list did not load: something went wrong. Try again.');
  });

  it('shows Page not found in place of the screen for a 403, at the same address', async () => {
    serve(usersPath, problem(403));
    const router = renderApp('/manage/users');

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe('/manage/users');
  });
});

describe('Users on window focus', () => {
  it('fetches the list again, and leaves the rows as they were, announcing nothing, when that fails', async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();
    const before = sent(usersPath);
    serve(usersPath, noConnection);

    returnToWindow();
    await wait(60_000);

    expect(sent(usersPath)).toBeGreaterThan(before);
    expect(listedNames()).toEqual([['Alex Park', 'Jo Smith', 'Sam Lee'], ['Chris Ng']]);
    for (const region of within(main()).getAllByRole('status')) {
      expect(region).toBeEmptyDOMElement();
    }
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
  });

  it('replaces the rows with the new list when that loads', async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users');
    await settle();
    // Alex Park has set up their account, and Chris Ng is reactivated.
    serve(usersPath, json([sam, jo, { ...alex, status: 'Active' }, { ...chris, status: 'Active' }]));

    returnToWindow();
    await settle();

    expect(listedNames()).toEqual([['Alex Park', 'Chris Ng', 'Jo Smith', 'Sam Lee']]);
    const link = within(main()).getByRole('link', { name: 'Alex Park' });
    expect(shownLines(link)).toEqual(['alex.park@example.org', 'Active · Worker']);
  });
});

describe('Users for a worker', () => {
  it('shows Page not found, and sends no request for the list', async () => {
    serve(usersPath, json(people));
    renderApp('/manage/users', worker);

    await wait(60_000);

    expectPageNotFound();
    expect(sent(usersPath)).toBe(0);
  });
});
