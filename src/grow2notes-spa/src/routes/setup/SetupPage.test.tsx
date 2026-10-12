import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes, type AppRoute } from '../routes.tsx';
import { useSetup } from './setupContext.ts';

const startPath = '/api/auth/setup/start';
const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';
const title = 'Grow2Notes – Set up your account';

// A made-up person and link (A35). The token has the three characters that the emailed link escapes, which the page
// must read back exactly.
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const userId = '6f9619ff-8b86-d011-b42d-00cf4fc964ff';
const token = 'CfDJ8Nq+2x/Yw==';
const setupLink = `/setup#u=${userId}&t=${encodeURIComponent(token)}`;

type Answer = (init?: RequestInit) => Promise<Response>;

const fetchMock = vi.fn<typeof fetch>();

/** The answers to `POST /api/auth/setup/start`, one for each request, in turn. */
const answers: Answer[] = [];

/** A `200` with a JSON body. */
function json(body: unknown): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code` if it has one. */
function problem(status: number, code?: string): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status, code }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that never answers: it rejects only when its signal aborts, with the signal's reason. */
function noAnswer(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** An answer that comes when the test says, as the server's or as no connection. */
function later() {
  let settle: { give: (answer: Response) => void; fail: () => void } | undefined;
  const answer = new Promise<Response>((resolve, reject) => {
    settle = { give: resolve, fail: () => reject(new TypeError('Failed to fetch')) };
  });
  return { answer: () => answer, ...settle! };
}

/** The path of each request sent, but the shell's own `GET /api/auth/me`, in order. */
function pageRequests(): unknown[] {
  return fetchMock.mock.calls.map(([path]) => path).filter((path) => path !== mePath);
}

/** The body of each link check sent, in order. */
function checks() {
  return fetchMock.mock.calls
    .filter(([path]) => path === startPath)
    .map(([, init]) => JSON.parse(init?.body as string) as unknown);
}

/** Shows what the setup layout holds, as view E does. */
function SetupMemory() {
  const { account: held } = useSetup();
  return <p data-testid="setup-memory">{held === undefined ? '' : `${held.displayName}, ${held.email}`}</p>;
}

/** The app's route table, with the setup layout's memory shown under each of its pages. */
function withSetupMemory(table: AppRoute[]): AppRoute[] {
  return table.map((route) => {
    if (route.index) {
      return route;
    }

    const children =
      route.path === 'setup'
        ? route.children?.map((child) => ({ ...child, element: <>{child.element}<SetupMemory /></> }))
        : route.children && withSetupMemory(route.children);
    return { ...route, children };
  });
}

const routers: ReturnType<typeof createBrowserRouter>[] = [];

/**
 * Opens the app at `path` on a browser router, as `main.tsx` does, so that the address and the history are the
 * window's own, under a query client of its own and StrictMode, which runs each effect twice.
 */
function open(path: string) {
  window.history.replaceState(null, '', path);
  const router = createBrowserRouter(withSetupMemory(routes));
  routers.push(router);
  render(
    <StrictMode>
      <QueryClientProvider client={createQueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return router;
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** The link check's status line, before the page's own status region, which is the last in `<main>`. */
function line() {
  const regions = within(screen.getByRole('main')).getAllByRole('status');
  expect(regions).toHaveLength(2);
  return regions[0];
}

function heading() {
  return screen.getByRole('heading', { level: 1 });
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain() {
  const button = screen.getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Nothing about the person, the link or the check is kept in storage. */
function expectNothingStored() {
  expect(localStorage).toHaveLength(0);
  expect(sessionStorage).toHaveLength(0);
}

beforeEach(() => {
  vi.useFakeTimers();
  // The wrapper fetches its antiforgery token before the first change, and keeps it for the next.
  fetchMock.mockImplementation((path, init) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up' } }));
    }

    // The shell's, for a person with no session.
    if (path === mePath) {
      return problem(401);
    }

    const answer = path === startPath ? answers.shift() : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  for (const router of routers.splice(0)) {
    router.dispose();
  }

  answers.length = 0;
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('SetupPage', () => {
  it('takes the link out of the address and the history while its check is in flight, and checks it once', async () => {
    answers.push(later().answer);
    const entries = window.history.length;

    const router = open(setupLink);
    await wait(0);

    expect(window.location.pathname).toBe('/setup');
    expect(window.location.hash).toBe('');
    // Replaced, not pushed: no entry holds the link.
    expect(window.history.length).toBe(entries);
    const kept = JSON.stringify(window.history.state);
    for (const part of [userId, token, encodeURIComponent(token)]) {
      expect(kept).not.toContain(part);
    }

    expect(router.state.location.state).toBeNull();
    // Once, though StrictMode ran the page's effects twice, with the link's parts read back exactly.
    expect(checks()).toEqual([{ userId, token }]);
    expect(heading()).toHaveTextContent('Set up your account');
    expectNothingStored();
  });

  it('shows nothing for 1 s, then "Checking your link…" in a status line there from the start', async () => {
    answers.push(later().answer);
    open(setupLink);

    await wait(999);
    expect(line()).toBeEmptyDOMElement();

    await wait(1);
    expect(line()).toHaveTextContent('Checking your link…');
    expect(heading().compareDocumentPosition(line())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it('shows view E once the link checks, with focus on its h1, and holds the account in memory only', async () => {
    answers.push(() => json(account));
    open(setupLink);

    await wait(0);

    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(screen.getByTestId('setup-memory')).toHaveTextContent('Sam Lee, sam.lee@example.org');
    expect(within(screen.getByRole('main')).getAllByRole('status')).toHaveLength(1);
    expect(document.title).toBe(title);
    expectNothingStored();
  });

  it.each([
    {
      answer: () => problem(410, 'auth.setup_link_invalid'),
      shown: 'This link has expired',
      sentence: 'Ask a manager to send a new one.',
    },
    {
      answer: () => problem(401),
      shown: 'Your setup session timed out',
      sentence: 'Open the link from your email again.',
    },
  ])('shows "$shown" as a whole page, with focus on its h1, one sentence and no button', async ({
    answer,
    shown,
    sentence,
  }) => {
    answers.push(answer);
    open(setupLink);

    await wait(0);

    expect(heading()).toHaveTextContent(shown);
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent(sentence);
    const main = within(screen.getByRole('main'));
    expect(main.queryByRole('button')).not.toBeInTheDocument();
    expect(main.queryByRole('link')).not.toBeInTheDocument();
    // The page's own status region only: focus on the h1 says it.
    expect(main.getAllByRole('status')).toHaveLength(1);
    expect(document.title).toBe(title);
    expectNothingStored();
  });

  describe('when the check fails in a way that trying again may get past', () => {
    it.each([
      {
        failure: 'no connection',
        answer: noConnection,
        failsAfter: 0,
        sentence: 'Your setup link was not checked: no connection. Try again.',
      },
      {
        failure: 'no answer in 10 s',
        answer: noAnswer,
        failsAfter: 10_000,
        sentence: 'Your setup link was not checked: no connection. Try again.',
      },
      {
        failure: 'a 503',
        answer: () => problem(503),
        failsAfter: 0,
        sentence: 'Your setup link was not checked: something went wrong. Try again.',
      },
      {
        failure: 'a 429',
        answer: () => problem(429),
        failsAfter: 0,
        sentence: 'Too many tries. Wait 1 minute, then try again.',
      },
    ])('says so for $failure, with Try again after the line and "Error: " in front of the title', async ({
      answer,
      failsAfter,
      sentence,
    }) => {
      answers.push(answer);
      open(setupLink);

      await wait(failsAfter);

      expect(line().textContent).toBe(sentence);
      const tryAgain = screen.getByRole('button', { name: 'Try again' });
      expect(line()).not.toContainElement(tryAgain);
      expect(line().compareDocumentPosition(tryAgain)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
      expect(document.title).toBe(`Error: ${title}`);
      expectNothingStored();
    });

    it('sends the link in memory again on Try again, busy "Checking…", and shows view E when it checks', async () => {
      answers.push(noConnection);
      open(setupLink);
      await wait(0);
      const answer = later();
      answers.push(answer.answer);

      const button = pressTryAgain();
      fireEvent.click(button);

      expect(button).toHaveFocus();
      expect(button).toHaveAttribute('aria-disabled', 'true');
      await wait(399);
      expect(button).toHaveAccessibleName('Try again');
      await wait(1);
      expect(button).toHaveAccessibleName('Checking…');
      expect(line()).toBeEmptyDOMElement();
      // One request for the two presses, with the link held in memory, as the address has none any more.
      expect(window.location.hash).toBe('');
      expect(checks()).toEqual([
        { userId, token },
        { userId, token },
      ]);

      answer.give(new Response(JSON.stringify(account), { headers: { 'Content-Type': 'application/json' } }));
      await wait(0);

      expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
      expect(heading()).toHaveFocus();
      expect(screen.getByTestId('setup-memory')).toHaveTextContent('Sam Lee, sam.lee@example.org');
      expect(document.title).toBe(title);
      expectNothingStored();
    });

    it('empties the line while Try again runs and writes the sentence again when it fails, focus kept', async () => {
      answers.push(() => problem(503));
      open(setupLink);
      await wait(0);
      const answer = later();
      answers.push(answer.answer);

      const button = pressTryAgain();
      await wait(0);
      expect(line()).toBeEmptyDOMElement();

      answer.fail();
      await wait(0);

      expect(line().textContent).toBe('Your setup link was not checked: no connection. Try again.');
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(document.title).toBe(`Error: ${title}`);
    });

    it('shows a whole-page message with the plain title when Try again gets one', async () => {
      answers.push(noConnection, () => problem(410, 'auth.setup_link_invalid'));
      open(setupLink);
      await wait(0);
      expect(document.title).toBe(`Error: ${title}`);

      pressTryAgain();
      await wait(0);

      expect(heading()).toHaveTextContent('This link has expired');
      expect(heading()).toHaveFocus();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(document.title).toBe(title);
    });
  });

  describe('with no link and nothing in memory', () => {
    it.each(['/setup', '/setup/password', '/setup/app'])(
      'shows the no-link message at %s, with focus on its h1, and sends nothing',
      async (path) => {
        open(path);

        await wait(5000);

        expect(heading()).toHaveTextContent('Set up your account');
        expect(heading()).toHaveFocus();
        expect(heading().nextElementSibling).toHaveTextContent(
          'To set up your account, open the link from your email again.',
        );
        expect(screen.queryByRole('button')).not.toBeInTheDocument();
        expect(within(screen.getByRole('main')).getAllByRole('status')).toHaveLength(1);
        expect(screen.getByTestId('setup-memory')).toBeEmptyDOMElement();
        expect(pageRequests()).toEqual([]);
        expect(document.title).toBe(title);
      },
    );

    it.each([`/setup#u=${userId}`, `/setup#t=${encodeURIComponent(token)}`])(
      'takes a link with a part missing, %s, out of the address, and shows the no-link message',
      async (path) => {
        open(path);

        await wait(0);

        expect(window.location.hash).toBe('');
        expect(heading()).toHaveFocus();
        expect(heading().nextElementSibling).toHaveTextContent(
          'To set up your account, open the link from your email again.',
        );
        expect(pageRequests()).toEqual([]);
      },
    );
  });
});
