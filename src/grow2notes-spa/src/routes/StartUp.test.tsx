import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import indexHtml from '../../index.html?raw';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { strings } from '../copy/strings.ts';
import { routes } from './routes.tsx';
import { StartUp } from './StartUp.tsx';

const mePath = '/api/auth/me';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';

// A made-up worker (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

/** How the server meets a `GET /api/auth/me`: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with `me`. */
function answerWith(me: Me): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
}

/** `answer`, given after `ms`. */
function after(ms: number, answer: Answer): Answer {
  return (init) =>
    new Promise((resolve) => {
      setTimeout(() => {
        resolve(answer(init));
      }, ms);
    });
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
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

// The two start-up messages (app-shell.md component 4).
const couldNotConnect = {
  heading: 'Could not connect to Grow2Notes',
  sentence: 'Grow2Notes did not load: no connection. Try again.',
  still: 'Grow2Notes still did not load: no connection. Try again.',
  title: 'Grow2Notes – Could not connect',
};
const thereIsAProblem = {
  heading: 'There is a problem with Grow2Notes',
  sentence: 'Grow2Notes did not load: something went wrong. Try again.',
  still: 'Grow2Notes still did not load: something went wrong. Try again.',
  title: 'Grow2Notes – There is a problem',
};

// Each failure of the start-up's request, the time by which its message shows, and the requests sent by then: one, and
// for no connection, a timeout or a 5xx its one silent retry, a second later.
const failures = [
  { failure: 'no connection', answer: noConnection, by: 2_000, sent: 2, message: couldNotConnect },
  { failure: 'two 10-second timeouts', answer: noAnswer, by: 21_000, sent: 2, message: couldNotConnect },
  { failure: 'a 503', answer: problem(503), by: 2_000, sent: 2, message: thereIsAProblem },
  { failure: 'a 500', answer: problem(500), by: 2_000, sent: 2, message: thereIsAProblem },
  { failure: 'a 429', answer: problem(429), by: 2_000, sent: 1, message: thereIsAProblem },
];

const fetchMock = vi.fn<typeof fetch>();

// How the server meets each request from now on, which a test changes between the start-up and Try again.
let server: Answer;

/**
 * Renders the app at `path` on a memory router built from the route table, under a query client of its own and
 * StrictMode, as `main.tsx` renders it.
 */
function renderApp(path = '/') {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <StrictMode>
      <QueryClientProvider client={createQueryClient()}>
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

/**
 * Lets TanStack hand the query's new state to the page, which it does on a `setTimeout` of 0 that the fake clock runs
 * 1 ms later when it is set while the clock is moving.
 */
async function settle() {
  await wait(1);
}

/** How many requests have gone to `GET /api/auth/me`. */
function meRequests() {
  return fetchMock.mock.calls.filter(([path, init]) => path === mePath && init?.method === 'GET').length;
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The start-up's status line, which must be the one status region in the page. */
function line(): HTMLElement {
  return screen.getByRole('status');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** Records the text of each node written into `element` from now on, and gives the function that stops and gives it. */
function watchWrites(element: HTMLElement): () => string[] {
  const written: string[] = [];
  const record = (records: MutationRecord[]) => {
    for (const { type, target, addedNodes } of records) {
      if (type === 'characterData') {
        written.push(target.textContent ?? '');
      }

      for (const node of addedNodes) {
        written.push(node.textContent ?? '');
      }
    }
  };
  const observer = new MutationObserver(record);
  observer.observe(element, { childList: true, characterData: true, subtree: true });
  return () => {
    record(observer.takeRecords());
    observer.disconnect();
    return written;
  };
}

/** Expects the signed-out frame: the wordmark as plain text, and no nav or Account. */
function expectSignedOutFrame() {
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
  expect(screen.queryByRole('link', { name: 'Grow2Notes' })).not.toBeInTheDocument();
  expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).toBeInTheDocument();
}

/**
 * Expects `message` in `<main>`, alone and in order: its `<h1>`, its status line saying `sentence`, the page's one
 * status region, and Try again after it, outside it, under the title `message.title`.
 */
function expectMessage(message: typeof couldNotConnect, sentence = message.sentence) {
  const heading = pageHeading();
  const button = within(main()).getByRole('button', { name: 'Try again' });
  expect(heading).toHaveAccessibleName(message.heading);
  expect(line().textContent).toBe(sentence);
  expect(Array.from(main().children)).toEqual([heading, line(), button]);
  expect(document.title).toBe(message.title);
}

/** The page becomes visible, as the browser says so: at the document, from which the event bubbles to the window. */
function becomeVisible() {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible');
  document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain() {
  const button = screen.getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock.mockImplementation((path, init) => {
    // Today, the screen that the start-up shows, has no participants and no unfinished drafts.
    if (path === todayPath || path === draftsPath) {
      return Promise.resolve(
        new Response(JSON.stringify(path === todayPath ? { date: worker.today, participants: [] } : []), {
          headers: { 'Content-Type': 'application/json' },
        }),
      );
    }

    if (path !== mePath) {
      throw new Error('The test gave no answer for this request.');
    }

    return server(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('the start-up', () => {
  it('renders its status line, empty, from the first render', () => {
    // No effect runs here, so a line that an effect added would be missing.
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={createQueryClient()}>
        <StartUp onTryAgainUsed={() => {}} />
      </QueryClientProvider>,
    );

    expect(markup).toMatch(/^<p role="status" class="[^"]+"><\/p>$/);
  });

  it('shows nothing in its line when /me answers at 900 ms, then the screen', async () => {
    server = after(900, answerWith(worker));
    renderApp();
    const stopWatching = watchWrites(line());

    await wait(899);
    expect(line()).toBeEmptyDOMElement();

    await wait(1);
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    await wait(60_000);
    expect(stopWatching()).toEqual([]);
    expect(screen.queryByText('Loading…')).not.toBeInTheDocument();
  });

  it('says "Loading…" from 1,000 ms, titled Grow2Notes, when /me answers at 1,500 ms', async () => {
    server = after(1500, answerWith(worker));
    renderApp();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();

    await wait(1);
    expect(line()).toHaveTextContent('Loading…');
    // The line alone, as the page's one status region.
    expect(Array.from(main().children)).toEqual([line()]);
    expect(document.title).toBe('Grow2Notes');
    expectSignedOutFrame();

    await wait(500);
    await settle();
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(screen.queryByText('Loading…')).not.toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Today');
  });

  it('says "Loading…" until 21 seconds when /me never answers', async () => {
    server = noAnswer;
    renderApp();

    await wait(20_999);

    expect(line()).toHaveTextContent('Loading…');
    expect(within(main()).queryByRole('heading')).not.toBeInTheDocument();
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
  });

  describe.each(failures)('after $failure', ({ answer, by, sent, message }) => {
    it(`shows "${message.heading}" by ${by.toLocaleString('en-AU')} ms, titled "${message.title}"`, async () => {
      server = answer;
      renderApp();

      await wait(by);

      expectMessage(message);
      expectSignedOutFrame();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(screen.getAllByRole('status')).toHaveLength(1);
      // Outside the line, so that its label is not read as part of the sentence.
      expect(line()).not.toContainElement(screen.getByRole('button', { name: 'Try again' }));
    });

    it('moves no focus, and sends nothing more by itself', async () => {
      server = answer;
      renderApp();
      await wait(by);
      expect(meRequests()).toBe(sent);

      await wait(30 * 60_000);

      expect(document.body).toHaveFocus();
      expect(meRequests()).toBe(sent);
      expectMessage(message);
    });
  });

  describe('Try again', () => {
    it('empties the line at once, asks /me again, and says "Loading…" from 400 ms, keeping focus', async () => {
      server = noConnection;
      renderApp();
      await wait(2_000);
      const heading = pageHeading();
      server = noAnswer;

      const button = pressTryAgain();

      expect(meRequests()).toBe(3);
      expect(line()).toBeEmptyDOMElement();
      expect(button).toHaveFocus();
      expect(button).toHaveAttribute('aria-disabled', 'true');

      await wait(399);
      expect(button).toHaveAccessibleName('Try again');

      await wait(1);
      expect(button).toHaveAccessibleName('Loading…');
      expect(button).toHaveFocus();
      expect(line()).toBeEmptyDOMElement();
      expect(pageHeading()).toBe(heading);
      expect(document.title).toBe(couldNotConnect.title);
    });

    it.each([
      { failure: 'no connection again', first: noConnection, again: noConnection, message: couldNotConnect },
      { failure: 'a 503 again', first: problem(503), again: problem(503), message: thereIsAProblem },
      { failure: 'a 503 after no connection', first: noConnection, again: problem(503), message: thereIsAProblem },
      { failure: 'no connection after a 503', first: problem(503), again: noConnection, message: couldNotConnect },
    ])('says "still did not load" for $failure, with focus kept on it', async ({ first, again, message }) => {
      server = first;
      renderApp();
      await wait(2_000);
      const heading = pageHeading();
      server = again;

      const button = pressTryAgain();
      // Its one silent retry, as at the start-up.
      await wait(999);
      expect(line()).toBeEmptyDOMElement();

      await wait(1);
      await settle();
      expectMessage(message, message.still);
      expect(pageHeading()).toBe(heading);
      expect(screen.getByRole('button', { name: 'Try again' })).toBe(button);
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(meRequests()).toBe(4);
    });

    it('shows Today with focus on its <h1> when it is answered 200', async () => {
      server = problem(503);
      renderApp();
      await wait(2_000);
      server = answerWith(worker);

      pressTryAgain();
      await settle();

      expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
      expect(pageHeading()).toHaveFocus();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
      expect(screen.queryByText(/did not load/)).not.toBeInTheDocument();
      expect(document.title).toBe('Grow2Notes – Today');
    });

    // A click that leaves focus where it was, on <body>, as one does in Safari on macOS, so that Try again's own focus
    // never marks it used (app-shell.md *Accessibility checklist*).
    it.each([
      { answered: '200', reply: answerWith(worker), heading: `Today, Thursday 1${nbsp}October` },
      { answered: '401', reply: problem(401), heading: 'Sign in' },
    ])('hands focus to the new <h1> after a click that does not focus it, answered $answered', async ({
      reply,
      heading,
    }) => {
      server = noConnection;
      renderApp();
      await wait(2_000);
      expect(document.body).toHaveFocus();
      server = reply;

      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await settle();

      expect(pageHeading()).toHaveAccessibleName(heading);
      expect(pageHeading()).toHaveFocus();
    });

    it('hands focus to the <h1> when a refresh brings the screen while Try again has focus', async () => {
      server = noConnection;
      renderApp();
      await wait(2_000);
      screen.getByRole('button', { name: 'Try again' }).focus();
      server = answerWith(worker);

      act(() => {
        window.dispatchEvent(new FocusEvent('focus'));
      });
      await settle();

      expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
      expect(pageHeading()).toHaveFocus();
    });

    it('leaves focus where it is when it has moved on from Try again before the screen comes', async () => {
      server = noConnection;
      renderApp();
      await wait(2_000);
      pressTryAgain();
      await wait(2_000);
      expect(line().textContent).toBe(couldNotConnect.still);
      const skipLink = screen.getByRole('link', { name: 'Skip to main content' });
      skipLink.focus();
      server = answerWith(worker);

      act(() => {
        window.dispatchEvent(new FocusEvent('focus'));
      });
      await settle();

      expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
      expect(skipLink).toHaveFocus();
    });

    it('shows view A, titled Sign in, with focus on its <h1> and no start-up line, when answered 401', async () => {
      server = noConnection;
      const router = renderApp('/manage');
      await wait(2_000);
      const { location } = router.state;
      server = problem(401);

      pressTryAgain();
      await settle();

      expect(pageHeading()).toHaveAccessibleName('Sign in');
      expect(pageHeading()).toHaveFocus();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(document.title).toBe('Grow2Notes – Sign in');
      expectSignedOutFrame();
      expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
      expect(screen.queryByText(/did not load/)).not.toBeInTheDocument();
      // Sign-in's own status region, empty, and nothing of the start-up's beside it.
      expect(within(main()).getByRole('status')).toBeEmptyDOMElement();
      expect(router.state.location).toBe(location);
    });
  });

  describe("the shell's own refresh, while a message shows", () => {
    it.each([
      { failure: 'no connection', after: 'the message', again: noConnection, pressFirst: false },
      { failure: 'a 503', after: 'the message', again: problem(503), pressFirst: false },
      { failure: 'no connection', after: 'a failed Try again', again: noConnection, pressFirst: true },
      { failure: 'a 503', after: 'a failed Try again', again: problem(503), pressFirst: true },
    ])(
      'changes nothing and says nothing when a focus and the page becoming visible meet $failure after $after',
      async ({ again, pressFirst }) => {
        server = noConnection;
        renderApp();
        await wait(2_000);
        if (pressFirst) {
          pressTryAgain();
          await wait(2_000);
        }
        const sentence = pressFirst ? couldNotConnect.still : couldNotConnect.sentence;
        expectMessage(couldNotConnect, sentence);
        const button = screen.getByRole('button', { name: 'Try again' });
        const focused = document.activeElement;
        server = again;
        const stopWatching = watchWrites(main());

        act(() => {
          window.dispatchEvent(new FocusEvent('focus'));
        });
        // While it runs, with its silent retry to come.
        await wait(500);
        expectMessage(couldNotConnect, sentence);
        expect(button).not.toHaveAttribute('aria-disabled');
        await wait(1_500);
        act(becomeVisible);
        await wait(2_000);

        expect(meRequests()).toBe(pressFirst ? 8 : 6);
        expect(stopWatching()).toEqual([]);
        expectMessage(couldNotConnect, sentence);
        expect(document.activeElement).toBe(focused);
      },
    );
  });

  describe("on setup's pages", () => {
    describe.each([
      { failure: 'no connection', answer: noConnection },
      { failure: 'a 503', answer: problem(503) },
      { failure: 'no answer', answer: noAnswer },
    ])('after $failure', ({ answer }) => {
      it.each(['/setup', '/setup/password', '/setup/app'])(
        'shows %s in the signed-out frame, with no start-up message',
        async (path) => {
          server = answer;
          renderApp(path);

          await wait(60_000);

          expectSignedOutFrame();
          expect(pageHeading()).toHaveAccessibleName('Set up your account');
          expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
          expect(document.title).toBe('Grow2Notes – Set up your account');
          expect(screen.queryByText(/did not load/)).not.toBeInTheDocument();
          expect(screen.queryByText('Loading…')).not.toBeInTheDocument();
          expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
        },
      );
    });
  });
});

describe('index.html', () => {
  it('says what the start-up says, in #root, before the script runs', () => {
    const page = new DOMParser().parseFromString(indexHtml, 'text/html');
    const root = page.getElementById('root');

    expect(root?.children).toHaveLength(1);
    expect(root?.querySelector(':scope > p.boot')?.textContent).toBe(strings.loading);
  });
});
