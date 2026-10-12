import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { pageName } from '../copy/pageNames.ts';
import { routes, type AppRoute } from './routes.tsx';

const mePath = '/api/auth/me';

// A made-up worker (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// Made-up screens under the shell: a section that names its page, with one child that names its own and one that
// names none.
const madeUp: AppRoute[] = [
  {
    path: 'made-up',
    handle: { pageName: pageName.participants },
    children: [{ path: 'named', handle: { pageName: pageName.participant } }, { path: 'unnamed' }],
  },
];

const fetchMock = vi.fn<typeof fetch>();

/** Stubs `fetch` as the server, which answers each request with a new answer from `answer`. */
function serveMe(answer: (init?: RequestInit) => Promise<Response>) {
  fetchMock.mockImplementation((_path, init) => answer(init));
}

/** A `200` with a JSON body. */
function json(body: unknown): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that has not answered yet: it rejects only when its signal aborts. */
function noAnswer(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** The app's route table with `extra` routes added under the shell. */
function tableWith(extra: AppRoute[]): AppRoute[] {
  return routes.map((route) => (route.index ? route : { ...route, children: [...(route.children ?? []), ...extra] }));
}

/** Renders the app at `path` on a memory router built from the table, under a query client of its own. */
function renderShell(path: string, extra: AppRoute[] = []) {
  const client = createQueryClient();
  const router = createMemoryRouter(tableWith(extra), { initialEntries: [path] });
  const { container } = render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { client, router, container };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

// The shell before /me answers, and once it has answered 200.
const states = [
  { state: 'before /me answers', answer: noAnswer },
  { state: 'once /me has answered 200', answer: () => json(worker) },
];

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the shell set, and an address with no fragment.
  document.title = '';
  window.history.replaceState(null, '', '/');
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('AppShell', () => {
  describe('until /me answers 200', () => {
    function expectSignedOutFrame() {
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Grow2Notes' })).not.toBeInTheDocument();
      expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).toBeInTheDocument();
      expect(document.title).toBe('Grow2Notes');
    }

    it('shows the signed-out frame while the answer is pending', async () => {
      serveMe(noAnswer);
      renderShell('/');
      await wait(0);

      expect(fetchMock).toHaveBeenCalledWith(mePath, expect.objectContaining({ method: 'GET' }));
      expectSignedOutFrame();
    });

    it.each([
      { failure: 'a 401', answer: () => problem(401) },
      { failure: 'a 503', answer: () => problem(503) },
      { failure: 'no connection', answer: noConnection },
    ])('shows the signed-out frame after $failure', async ({ answer }) => {
      serveMe(answer);
      const { client } = renderShell('/');
      // Past the query client's one silent retry, for the failures that get one.
      await wait(60_000);

      expect(client.getQueryState(meQuery.queryKey)?.status).toBe('error');
      expectSignedOutFrame();
    });
  });

  describe('once /me has answered 200', () => {
    it('links the wordmark to Today and titles the page with its route\'s page name', async () => {
      serveMe(() => json(worker));
      renderShell('/');
      await wait(0);

      expect(screen.getByRole('link', { name: 'Grow2Notes' })).toHaveAttribute('href', '/');
      expect(document.title).toBe('Grow2Notes – Today');
    });

    it.each([
      { path: '/made-up', title: 'Grow2Notes – Participants' },
      { path: '/made-up/named', title: 'Grow2Notes – Participant' },
      { path: '/made-up/unnamed', title: 'Grow2Notes – Participants' },
    ])('titles $path from the deepest route that names its page', async ({ path, title }) => {
      serveMe(() => json(worker));
      renderShell(path, madeUp);
      await wait(0);

      expect(document.title).toBe(title);
    });

    // A screen that puts "Error: " in front takes it off itself, when its errors clear or it goes (ErrorSummary), so
    // the shell must leave it while the screen may still show them.
    it('sets the title when the page name changes, and leaves a change of its own while the name stays', async () => {
      serveMe(() => json(worker));
      const { router } = renderShell('/', madeUp);
      await wait(0);

      await act(() => router.navigate('/made-up'));
      expect(document.title).toBe('Grow2Notes – Participants');

      // As a form does after a failed submit.
      document.title = 'Error: Grow2Notes – Participants';
      await act(() => router.navigate('/made-up/unnamed'));
      expect(document.title).toBe('Error: Grow2Notes – Participants');

      await act(() => router.navigate('/made-up/named'));
      expect(document.title).toBe('Grow2Notes – Participant');
    });
  });

  describe('on a setup page', () => {
    const setupPaths = ['/setup', '/setup/password', '/setup/app'];

    describe.each([
      { state: 'while /me is pending', answer: noAnswer },
      { state: 'after a 401 from /me', answer: () => problem(401) },
      { state: 'once /me has answered 200', answer: () => json(worker) },
    ])('$state', ({ answer }) => {
      it.each(setupPaths)('shows %s in the signed-out frame, titled with its page name', async (path) => {
        serveMe(answer);
        renderShell(path);
        await wait(0);

        expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
        expect(screen.queryByRole('link', { name: 'Grow2Notes' })).not.toBeInTheDocument();
        expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).toBeInTheDocument();
        const heading = within(screen.getByRole('main')).getByRole('heading', { level: 1 });
        expect(heading).toHaveTextContent('Set up your account');
        expect(document.title).toBe('Grow2Notes – Set up your account');
      });
    });

    it('titles the page again on leaving setup for Today, and on coming back', async () => {
      serveMe(() => json(worker));
      const { router } = renderShell('/setup');
      await wait(0);

      await act(() => router.navigate('/'));
      expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
      expect(document.title).toBe('Grow2Notes – Today');

      await act(() => router.navigate(-1));
      expect(router.state.location.pathname).toBe('/setup');
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
      expect(document.title).toBe('Grow2Notes – Set up your account');
    });
  });

  describe.each(states)('$state', ({ answer }) => {
    it.each(['/', '/setup'])(
      'renders the skip link, <header>, #page-bar and one <main>, in that order, at %s',
      async (path) => {
        serveMe(answer);
        const { container } = renderShell(path);
        await wait(0);

        const order = Array.from(
          container.children,
          (element) => element.localName + (element.id && `#${element.id}`),
        );
        expect(order).toEqual(['a', 'header', 'div#page-bar', 'main#main-content']);
        expect(container.firstElementChild).toBe(screen.getByRole('link', { name: 'Skip to main content' }));
        expect(document.querySelectorAll('main')).toHaveLength(1);
        expect(screen.getByRole('main')).toHaveAttribute('tabindex', '-1');
      },
    );

    it('moves focus to <main> from the skip link, with no change of address or history', async () => {
      serveMe(answer);
      const { router } = renderShell('/');
      await wait(0);
      const { hash } = window.location;
      const { length } = window.history;
      const location = router.state.location;

      fireEvent.click(screen.getByRole('link', { name: 'Skip to main content' }));
      // jsdom follows a link to a fragment in a task of its own, after the click.
      await wait(0);

      expect(screen.getByRole('main')).toHaveFocus();
      expect(window.location.hash).toBe(hash);
      expect(window.history.length).toBe(length);
      expect(router.state.location).toBe(location);
    });
  });
});
