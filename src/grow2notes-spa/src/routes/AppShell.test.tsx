import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { buttonClass } from '../components/buttonClass.ts';
import { pageName } from '../copy/pageNames.ts';
import { routes, type AppRoute } from './routes.tsx';

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const optionsPath = '/api/auth/passkey/options';

const hint = 'Sign in the way you chose when you set up your account.';
const help = 'If you cannot sign in, ask a manager to reset your sign-in.';
const notice =
  'Passkeys do not work in this browser. To use your passkey, open Grow2Notes in your phone’s main browser, such as '
  + 'Safari or Chrome.';

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

/**
 * Stubs `fetch` as the server, which answers each request to `GET /api/auth/me` with a new answer from `answer`, and
 * the sign-in view's request for passkey options with options, after a token.
 */
function serveMe(answer: (init?: RequestInit) => Promise<Response>) {
  fetchMock.mockImplementation((path, init) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));
    }

    return path === optionsPath ? json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }) : answer(init);
  });
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

/** A browser with WebAuthn, which jsdom is not: `browserSupportsWebAuthn()` asks only for the constructor. */
function withWebAuthn() {
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
}

/** How many requests have gone to `GET /api/auth/me`. */
function meRequests() {
  return fetchMock.mock.calls.filter(([path, init]) => path === mePath && init?.method === 'GET').length;
}

/** Fails unless the elements come in the document in the order given. */
function expectInOrder(...elements: HTMLElement[]) {
  for (const [index, element] of elements.slice(1).entries()) {
    expect(elements[index].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

// The shell before /me answers, after its 401, and once it has answered 200.
const states = [
  { state: 'before /me answers', answer: noAnswer },
  { state: 'after a 401 from /me', answer: () => problem(401) },
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
    /** Expects the signed-out frame: the wordmark as plain text, and no nav or Account. */
    function expectSignedOutFrame() {
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Grow2Notes' })).not.toBeInTheDocument();
      expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).toBeInTheDocument();
    }

    it('shows the signed-out frame, with an empty <main> and the bare title, while the answer is pending', async () => {
      serveMe(noAnswer);
      renderShell('/');
      await wait(0);

      expect(fetchMock).toHaveBeenCalledWith(mePath, expect.objectContaining({ method: 'GET' }));
      expectSignedOutFrame();
      expect(screen.getByRole('main')).toBeEmptyDOMElement();
      expect(document.title).toBe('Grow2Notes');
    });

    it.each([
      { failure: 'a 503', answer: () => problem(503) },
      { failure: 'no connection', answer: noConnection },
    ])('shows the signed-out frame, with an empty <main> and the bare title, after $failure', async ({ answer }) => {
      serveMe(answer);
      const { client } = renderShell('/');
      // Past the query client's one silent retry.
      await wait(60_000);

      expect(client.getQueryState(meQuery.queryKey)?.status).toBe('error');
      expectSignedOutFrame();
      expect(screen.getByRole('main')).toBeEmptyDOMElement();
      expect(document.title).toBe('Grow2Notes');
    });

    describe.each(['/', '/manage/anything'])('given a 401 at %s', (path) => {
      it('shows view A in the signed-out frame at that address, titled Sign in, with nothing focused', async () => {
        withWebAuthn();
        serveMe(() => problem(401));
        const { router } = renderShell(path);
        const { location } = router.state;
        await wait(0);

        expectSignedOutFrame();
        const main = within(screen.getByRole('main'));
        const heading = main.getByRole('heading', { level: 1, name: 'Sign in' });
        const button = main.getByRole('button', { name: 'Sign in with a passkey' });
        expect(button).toHaveAttribute('type', 'button');
        expect(button).toHaveClass(...buttonClass('primary').split(' '));
        // The alert lines, the passkey button's first, are in the page, empty, before anything can fail (sign-in.md
        // component 2a).
        const alerts = main.getAllByRole('alert');
        for (const alert of alerts) {
          expect(alert).toBeEmptyDOMElement();
        }
        expectInOrder(heading, main.getByText(hint), alerts[0], button, main.getByText(help));
        // No error summary until a sign-in fails.
        expect(main.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
        // The page's status region, empty (sign-in.md component 18).
        expect(main.getByRole('status')).toBeEmptyDOMElement();

        expect(document.title).toBe('Grow2Notes – Sign in');
        expect(router.state.location).toBe(location);
        expect(document.body).toHaveFocus();
      });
    });

    it('shows the notice in place of the passkey button in a browser with no WebAuthn', async () => {
      vi.stubGlobal('PublicKeyCredential', undefined);
      serveMe(() => problem(401));
      renderShell('/');
      await wait(0);

      const main = within(screen.getByRole('main'));
      expect(main.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
      expectInOrder(
        main.getByRole('heading', { level: 1, name: 'Sign in' }),
        main.getByText(hint),
        main.getByText(notice),
        main.getByText(help),
      );
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

    it('shows no sign-in, and moves no focus', async () => {
      withWebAuthn();
      serveMe(() => json(worker));
      renderShell('/');
      await wait(0);

      const main = within(screen.getByRole('main'));
      expect(main.queryByRole('heading', { name: 'Sign in' })).not.toBeInTheDocument();
      expect(main.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
      // A first load moves no focus (app-shell.md component 7); only a sign-in focuses the screen's heading.
      expect(document.body).toHaveFocus();
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
        // Setup takes sign-in's place whatever /me answers, a 401 included.
        expect(screen.queryByRole('heading', { name: 'Sign in' })).not.toBeInTheDocument();
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

  it.each([
    { answer: 'a 401', serveAnswer: () => problem(401) },
    { answer: 'a 200', serveAnswer: () => json(worker) },
  ])('asks /me once after $answer, and once more when the window regains focus', async ({ serveAnswer }) => {
    withWebAuthn();
    serveMe(serveAnswer);
    renderShell('/');
    await wait(0);
    expect(meRequests()).toBe(1);

    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await wait(60_000);

    expect(meRequests()).toBe(2);
  });
});
