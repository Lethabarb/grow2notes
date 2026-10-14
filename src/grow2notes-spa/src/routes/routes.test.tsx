import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { pageName } from '../copy/pageNames.ts';
import type { AdminUser } from '../features/users/usersQuery.ts';
import { routes, type AppRoute } from './routes.tsx';

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

// The manager's own row, the one user on the made-up list.
const users: AdminUser[] = [
  {
    id: manager.userId,
    email: 'sam.lee@example.org',
    displayName: manager.displayName,
    role: 'Manager',
    status: 'Active',
    invitedAtUtc: '2026-09-01T00:00:00Z',
    activatedAtUtc: '2026-09-02T00:00:00Z',
    deactivatedAtUtc: null,
    concurrencyStamp: 'b7e0c2d4-1a95-4f36-8c2e-6d9a3f1b0e57',
  },
];

// The made-up answers of the manager screens' requests, by path.
const managerAnswers: Readonly<Record<string, unknown>> = {
  '/api/admin/users': users,
  '/api/admin/guide-prompts': { text: 'Mood and wellbeing today?' },
};

// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

const fetchMock = vi.fn<typeof fetch>();

/** Like `fetch` to a server that has not answered yet: it rejects only when its signal aborts. */
function noAnswer(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/**
 * Stubs `fetch` as the server, which answers `GET /api/admin/users` and `GET /api/admin/guide-prompts` with their
 * made-up answers and any other request, `GET /api/auth/me`, with `me`, or, for none, never answers.
 */
function serveMe(me: Me | undefined) {
  fetchMock.mockImplementation((path, init) =>
    me === undefined
      ? noAnswer(init)
      : Promise.resolve(
          // `api()` sends each request to a path, as a string.
          new Response(JSON.stringify(managerAnswers[path as string] ?? me), {
            headers: { 'Content-Type': 'application/json' },
          }),
        ),
  );
}

/** Renders the app's routes at `path` on a memory router, under a query client of its own. */
function renderApp(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(screen.getByRole('main')).getByRole('heading', { level: 1 });
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** The text that `element` shows on screen, without what only screen readers get. */
function visibleText(element: HTMLElement): string | null {
  const shown = element.cloneNode(true) as HTMLElement;
  shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
    hidden.remove();
  });
  return shown.textContent;
}

/** Expects Page not found in `<main>`, with its own title, under the header and nav, which mark no item. */
function expectPageNotFound() {
  const main = screen.getByRole('main');
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(within(main).getByText('If you typed or pasted the web address, check it is correct.')).toBeInTheDocument();
  expect(within(main).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(document.title).toBe('Grow2Notes – Page not found');
  expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
  expect(currentItems()).toEqual([]);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Today', () => {
  it.each([worker, manager])(
    "shows a $role its heading alone, from the server's today, not the device's clock",
    async (me) => {
      // Two days after the server's today, in Melbourne.
      vi.setSystemTime(new Date('2026-10-03T09:00:00+10:00'));
      serveMe(me);
      renderApp('/');
      await settle();
      const heading = pageHeading();

      expect(visibleText(heading)).toBe(`Today · Thursday 1${nbsp}October`);
      expect(heading).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
      expect(heading.querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      // Nothing else on the page until E02 builds the list.
      expect(screen.getByRole('main').textContent).toBe(heading.textContent);
      expect(document.title).toBe('Grow2Notes – Today');
    },
  );
});

describe('Manage', () => {
  const links = [
    { name: 'Participants', address: '/manage/participants' },
    { name: 'Common items', address: '/manage/common-items' },
    { name: 'Guide prompts', address: '/manage/guide-prompts' },
    { name: 'Users', address: '/manage/users' },
  ];

  it('shows a manager its heading and exactly the four links, in order, in a list', async () => {
    serveMe(manager);
    renderApp('/manage');
    await settle();
    const main = screen.getByRole('main');

    expect(pageHeading()).toHaveAccessibleName('Manage');
    expect(document.title).toBe('Grow2Notes – Manage');
    const listed = within(within(main).getByRole('list')).getAllByRole('link');
    expect(listed.map((link) => ({ name: link.textContent, address: link.getAttribute('href') }))).toEqual(links);
    expect(within(main).getAllByRole('link')).toEqual(listed);
  });

  it.each(links.filter(({ name }) => name !== 'Guide prompts' && name !== 'Users'))(
    'opens Page not found from $name, until its screen is built',
    async ({ name, address }) => {
      serveMe(manager);
      const router = renderApp('/manage');
      await settle();

      fireEvent.click(within(screen.getByRole('main')).getByRole('link', { name }));
      await settle();

      expect(router.state.location.pathname).toBe(address);
      expectPageNotFound();
    },
  );

  it('opens the user list from Users, not Page not found, with Manage still marked', async () => {
    serveMe(manager);
    const router = renderApp('/manage');
    await settle();

    fireEvent.click(within(screen.getByRole('main')).getByRole('link', { name: 'Users' }));
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'Sam Lee' })).toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Users');
    expect(currentItems()).toEqual(['Manage true']);
  });

  it('opens the editor from Guide prompts, not Page not found, with Manage still marked', async () => {
    serveMe(manager);
    const router = renderApp('/manage');
    await settle();

    fireEvent.click(within(screen.getByRole('main')).getByRole('link', { name: 'Guide prompts' }));
    await settle();

    expect(router.state.location.pathname).toBe('/manage/guide-prompts');
    expect(pageHeading()).toHaveAccessibleName('Guide prompts');
    expect(within(screen.getByRole('main')).getByRole('textbox', { name: 'Guide prompts' })).toHaveValue(
      'Mood and wellbeing today?',
    );
    expect(document.title).toBe('Grow2Notes – Guide prompts');
    expect(currentItems()).toEqual(['Manage true']);
  });
});

describe('Page not found', () => {
  it.each([
    { me: manager, path: '/flagged' },
    { me: manager, path: '/reports/daily/2026-10-01' },
    { me: manager, path: '/no-such-page' },
    { me: worker, path: '/no-such-page' },
    { me: worker, path: '/manage' },
  ])('shows at $path for a $me.role, with the nav still there and no item current', async ({ me, path }) => {
    serveMe(me);
    renderApp(path);
    await settle();

    expectPageNotFound();
  });

  it.each(['Flagged', 'Report'])("opens from a manager's %s link, until its screen is built", async (name) => {
    serveMe(manager);
    renderApp('/');
    await settle();

    fireEvent.click(screen.getByRole('link', { name }));
    await settle();

    expectPageNotFound();
  });

  // ManagerOnly, and the Page not found it shows a worker, stay while the route under them changes, and the shell sets
  // that route's title, Users, which Page not found must replace again.
  it('keeps its title while a worker goes from one manager screen to another', async () => {
    serveMe(worker);
    const router = renderApp('/manage');
    await settle();

    await act(() => router.navigate('/manage/users'));

    expectPageNotFound();
  });

  it('goes to Today from Go to Today, where the shell sets the title again', async () => {
    serveMe(worker);
    const router = renderApp('/no-such-page');
    await settle();

    fireEvent.click(screen.getByRole('link', { name: 'Go to Today' }));
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(document.title).toBe('Grow2Notes – Today');
  });
});

describe('every page', () => {
  it.each([
    { page: 'Today', me: worker, path: '/' },
    { page: 'Manage', me: manager, path: '/manage' },
    { page: 'Page not found', me: manager, path: '/no-such-page' },
    { page: 'Page not found', me: worker, path: '/manage' },
  ])('has one <h1> and one status region, both in <main>: $page at $path', async ({ me, path }) => {
    serveMe(me);
    renderApp(path);
    await settle();
    const main = screen.getByRole('main');

    const headings = screen.getAllByRole('heading', { level: 1 });
    expect(headings).toHaveLength(1);
    expect(main).toContainElement(headings[0]);
    const regions = screen.getAllByRole('status');
    expect(regions).toHaveLength(1);
    expect(main).toContainElement(regions[0]);
  });
});

// Stand-ins for a route's parameters: a date for one named after a date, an ID for any other, and an address that no
// route matches for a splat.
function madeUpSegment(segment: string): string {
  if (segment === '*') {
    return 'no-such-page';
  }

  if (!segment.startsWith(':')) {
    return segment;
  }

  return /date$/i.test(segment) ? '2026-09-30' : '4e7a2c1b-8d35-4f60-9b1e-2a6c0d5f8e73';
}

/** Every address that a route of `table` matches, with made-up IDs and dates for its parameters. */
function addresses(table: readonly AppRoute[], parent: readonly string[] = []): string[] {
  return table.flatMap((route) => {
    if (route.index) {
      return [`/${parent.join('/')}`];
    }

    const segments = route.path?.split('/').filter((segment) => segment !== '') ?? [];
    const path = [...parent, ...segments.map(madeUpSegment)];
    const own = route.path === undefined ? [] : [`/${path.join('/')}`];
    return [...own, ...addresses(route.children ?? [], path)];
  });
}

describe('the page titles', () => {
  const everyAddress = [...new Set(addresses(routes))];
  const escaped = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  // app-shell.md's first Structure criterion: "Grow2Notes – " and one name from the list, or the app's name alone.
  const title = new RegExp(
    `^(?:(?:Error: )?Grow2Notes – (?:${Object.values(pageName).map(escaped).join('|')})|Grow2Notes)$`,
  );

  it('walk every route in the table', () => {
    expect(everyAddress).toEqual(expect.arrayContaining(['/', '/manage', '/no-such-page']));
  });

  describe.each([
    { who: 'anyone, before /me answers', me: undefined },
    { who: 'a worker', me: worker },
    { who: 'a manager', me: manager },
  ])('for $who', ({ me }) => {
    it.each(everyAddress)('name a page from the list at %s, and no person or organisation', async (path) => {
      serveMe(me);
      renderApp(path);
      await settle();

      expect(document.title).toMatch(title);
      for (const name of [worker.displayName, manager.displayName, worker.organisationName]) {
        expect(document.title).not.toContain(name);
      }
    });
  });
});
