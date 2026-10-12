import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { pageName } from '../copy/pageNames.ts';
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

const noteAddress = '/participants/0c4f2a9e-6b13-4d7a-8e52-71f9b3d0a6c8/notes/2026-09-30';

// Made-up screens at addresses from each row of component 2b's table that the app does not route yet.
const madeUp: AppRoute[] = [
  { path: 'flagged', handle: { pageName: pageName.flaggedNotes } },
  { path: 'reports/daily/:date', handle: { pageName: pageName.dailyReport } },
  { path: 'manage', handle: { pageName: pageName.manage } },
  { path: 'manage/users', handle: { pageName: pageName.users } },
  { path: 'participants/:participantId/notes/:noteDate', handle: { pageName: pageName.note } },
];

const fetchMock = vi.fn<typeof fetch>();

/** Stubs `fetch` as the server, which answers `GET /api/auth/me` with `me()` each time it is asked. */
function serveMe(me: () => Me) {
  fetchMock.mockImplementation(() =>
    Promise.resolve(new Response(JSON.stringify(me()), { headers: { 'Content-Type': 'application/json' } })),
  );
}

/** Renders the app with the made-up screens at `path` on a memory router, under a query client of its own. */
function renderShell(path: string) {
  const client = createQueryClient();
  const table = routes.map((route) =>
    route.index ? route : { ...route, children: [...(route.children ?? []), ...madeUp] },
  );
  const router = createMemoryRouter(table, { initialEntries: [path] });
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { client, router };
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

/** The header's links and buttons, in DOM order, each as its role and text. */
function headerTargets(): string[] {
  const targets = screen.getByRole('banner').querySelectorAll('a[href], button');
  return Array.from(targets, (target) => `${target.localName === 'a' ? 'link' : 'button'} ${target.textContent}`);
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Header', () => {
  // Exactly these, in DOM order, which is the order on screen: the only button is Account, so there is no menu button.
  it.each([
    {
      me: worker,
      targets: ['link Grow2Notes', 'link Today', 'button Account'],
      addresses: ['/'],
    },
    {
      me: manager,
      targets: ['link Grow2Notes', 'button Account', 'link Today', 'link Flagged', 'link Report', 'link Manage'],
      addresses: ['/', '/flagged', '/reports/daily/2026-10-01', '/manage'],
    },
  ])("gives a $me.role their role's items in order, the nav's in a list", async ({ me, targets, addresses }) => {
    serveMe(() => me);
    renderShell('/');
    await settle();

    expect(headerTargets()).toEqual(targets);
    const items = within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('listitem');
    expect(items.map((item) => within(item).getByRole('link').getAttribute('href'))).toEqual(addresses);
  });

  // Component 2b's table, with every marked element in the header found, so that a second would fail. At
  // /manage/no-such-page the table would mark Manage, but Page not found shows there.
  it.each([
    { me: manager, path: '/', current: 'Today page' },
    { me: manager, path: '/flagged', current: 'Flagged page' },
    { me: manager, path: '/flagged?view=reviewed', current: 'Flagged page' },
    { me: manager, path: '/reports/daily/2026-10-01', current: 'Report page' },
    { me: manager, path: '/reports/daily/2026-09-14', current: 'Report page' },
    { me: manager, path: '/manage', current: 'Manage page' },
    { me: manager, path: '/manage/users', current: 'Manage true' },
    { me: manager, path: noteAddress, current: 'none' },
    { me: manager, path: '/no-such-page', current: 'none' },
    { me: manager, path: '/manage/no-such-page', current: 'none' },
    { me: worker, path: '/', current: 'Today page' },
    { me: worker, path: noteAddress, current: 'none' },
  ])('at $path, marks $current for a $me.role', async ({ me, path, current }) => {
    serveMe(() => me);
    renderShell(path);
    await settle();

    expect(currentItems()).toEqual(current === 'none' ? [] : [current]);
  });

  it('moves the Report link to the new today once /me is fetched again, with no reload', async () => {
    let today = '2026-10-01';
    serveMe(() => ({ ...manager, today }));
    const { client, router } = renderShell('/');
    await settle();
    const report = screen.getByRole('link', { name: 'Report' });
    const { location } = router.state;

    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-01');

    // Past midnight in Melbourne, /me is asked again, as refreshMe() will be on a screen change or window focus.
    today = '2026-10-02';
    await act(() => client.fetchQuery({ ...meQuery, staleTime: 0 }));
    await settle();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    // The same element on the same location: the header was neither reloaded nor mounted again.
    expect(screen.getByRole('link', { name: 'Report' })).toBe(report);
    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-02');
    expect(router.state.location).toBe(location);
  });
});
