import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { rearmUnloadGuard } from '../../routes/leaveGuard.ts';
import { routes } from '../../routes/routes.tsx';
import { leaveFor } from './leaveFor.ts';

// The Connect an AI assistant screen (S07.01.07; mcp-server.md §4.4), in the whole app, on a memory router.

// jsdom does not navigate, so the way out is a stand-in that the tests read.
vi.mock('./leaveFor.ts');

const address = '/connect?request=made-up-handle';
const connectPath = '/api/connect';
const mePath = '/api/auth/me';
const next = '/oauth/authorize?client_id=https%3A%2F%2Fassistant.example%2Fclient.json&decision=made-up-decision';

// Made-up people (A35): Sam Lee, the manager in the JSON that MeEndpointTests asserts, a worker and a Support person.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const worker: Me = {
  ...manager,
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
};
const support: Me = {
  ...manager,
  userId: 'e7c2a94d-3b58-4f61-8d0a-5a1f9b6c2e34',
  displayName: 'Ivy Chen',
  role: 'Support',
};

// A made-up registration, as GET /api/connect gives it.
const assistant = {
  assistantName: 'Example Assistant',
  company: 'Example AI Ltd',
  dataLocation: 'in the United States',
  clientIdHost: 'assistant.example',
  redirectHost: 'callback.assistant.example',
  loopbackOnly: false,
};

type Answer = () => Promise<Response>;

const json =
  (body: unknown): Answer =>
  () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
const problem =
  (status: number): Answer =>
  () =>
    Promise.resolve(
      new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status }), {
        status,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    );
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

const fetchMock = vi.fn<typeof fetch>();
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each `method` request for `path`, whatever its query, with `answer`. */
function serve(method: 'GET' | 'POST', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** The JSON bodies of the requests sent to `/api/connect` with `method`. */
function bodiesSent(method: 'GET' | 'POST'): unknown[] {
  return fetchMock.mock.calls
    .filter(([sent, init]) => (sent as string).split('?')[0] === connectPath && init?.method === method)
    .map(([, init]) => (typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined));
}

/** Renders the app at `path` for `me`, or for nobody, and lets the screen show. */
async function renderApp(path: string, me: Me | 'nobody' = manager) {
  serve('GET', mePath, me === 'nobody' ? problem(401) : json(me));
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const view = render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  await settle();
  return { router, view };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets each request answer and TanStack hand on its new state, until the page has it. */
async function settle() {
  await wait(1);
  await wait(1);
}

const main = () => screen.getByRole('main');
const heading = () => within(main()).getByRole('heading', { level: 1 });
const allow = () => within(main()).getByRole('button', { name: 'Allow' });
const cancel = () => within(main()).getByRole('button', { name: 'Cancel' });
const where = (router: { state: { location: { pathname: string; search: string } } }) =>
  router.state.location.pathname + router.state.location.search;

/** The sentences of the screen, in order, but not the region's status line. */
const sentences = () => Array.from(main().querySelectorAll('p:not([role="status"])'), (paragraph) => paragraph.textContent);

/** The screen's request alert, which the screen always renders, directly above its buttons. */
const alert = () => within(main()).getByRole('alert');

function expectProblemPage() {
  expect(heading()).toHaveAccessibleName('There is a problem with Grow2Notes');
  expect(document.title).toBe('Grow2Notes – There is a problem');
  expect(screen.queryByRole('button', { name: 'Allow' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument();
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock.mockImplementation((path, init) => {
    const request = `${init?.method} ${(path as string).split('?')[0]}`;
    const answer = answers.get(request);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${request}.`);
    }

    return answer();
  });
  vi.stubGlobal('fetch', fetchMock);
  serve('GET', '/api/auth/antiforgery', () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up-token' } })),
  );
  serve('GET', connectPath, json(assistant));
  serve('POST', connectPath, json({ next }));
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.mocked(leaveFor).mockReset();
  rearmUnloadGuard();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('a manager or a Support account', () => {
  it('sees the assistant as registered, with both hosts, and Allow then Cancel', async () => {
    await renderApp(address);

    expect(heading()).toHaveAccessibleName('Connect an AI assistant to Grow2Notes');
    expect(document.title).toBe('Grow2Notes – Connect an AI assistant');
    expect(sentences()).toEqual([
      'Signed in as Sam Lee (manager).',
      'Example Assistant, from Example AI Ltd, wants to connect. It identifies itself as assistant.example and will ' +
        'send you back to callback.assistant.example.',
      'It will be able to look up and change Grow2Notes setup for you: users, participants and their goals, common ' +
        'items and guide prompts. It cannot see notes.',
      'Whatever you look up or change through it, including participants’ names and goals, dates of birth you type ' +
        'when adding or correcting a participant, and staff names and email addresses, is sent to Example AI Ltd and ' +
        'stored in the United States.',
      'Use only the Example Assistant account your organisation has approved. Only continue if you started this ' +
        'from Example Assistant yourself just now.',
    ]);
    const buttons = within(main()).getAllByRole('button');
    expect(buttons).toHaveLength(2);
    expect(buttons[0]).toHaveAccessibleName('Allow');
    expect(buttons[1]).toHaveAccessibleName('Cancel');
    expect(bodiesSent('GET')).toHaveLength(1);
    expect(fetchMock).toHaveBeenCalledWith('/api/connect?request=made-up-handle', expect.anything());
  });

  it('is told "(Support)" for a Support account', async () => {
    await renderApp(address, support);

    expect(sentences()[0]).toBe('Signed in as Ivy Chen (Support).');
    expect(allow()).toBeInTheDocument();
  });

  it('is warned that the assistant runs on the computer only when every address registered is on it', async () => {
    const warning = 'This assistant runs on your computer. Other programs on this computer could pretend to be it.';
    const first = await renderApp(address);
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    first.view.unmount();

    serve('GET', connectPath, json({ ...assistant, loopbackOnly: true }));
    await renderApp(address);
    expect(screen.getByText(warning)).toBeInTheDocument();
  });

  it('posts Allow, and goes to the address that the server answers with, the buttons held meanwhile', async () => {
    await renderApp(address);

    fireEvent.click(allow());
    await settle();

    expect(bodiesSent('POST')).toEqual([{ request: 'made-up-handle', allow: true }]);
    expect(leaveFor).toHaveBeenCalledExactlyOnceWith(next);
    expect(allow()).toHaveAttribute('aria-disabled', 'true');
    expect(cancel()).toHaveAttribute('aria-disabled', 'true');
  });

  it('posts Cancel with allow false, and goes to the address that the server answers with', async () => {
    await renderApp(address);

    fireEvent.click(cancel());
    await settle();

    expect(bodiesSent('POST')).toEqual([{ request: 'made-up-handle', allow: false }]);
    expect(leaveFor).toHaveBeenCalledExactlyOnceWith(next);
  });

  it('holds both buttons while the request is out, and marks the one pressed busy', async () => {
    let release = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    serve('POST', connectPath, async () => {
      await held;
      return json({ next })();
    });
    await renderApp(address);
    // Once busy for 400 ms the button's visible text is its busy label, so it is no longer found by "Allow".
    const allowButton = allow();
    const cancelButton = cancel();

    fireEvent.click(allowButton);
    await wait(500);

    expect(allowButton).toHaveAttribute('aria-disabled', 'true');
    expect(allowButton).toHaveAttribute('data-busy');
    expect(cancelButton).toHaveAttribute('aria-disabled', 'true');
    expect(cancelButton).not.toHaveAttribute('data-busy');
    fireEvent.click(cancelButton);
    expect(bodiesSent('POST')).toHaveLength(1);

    release();
    await settle();
    expect(leaveFor).toHaveBeenCalledOnce();
  });

  it('shows the general error, and does not leave, for an address that is not the server’s', async () => {
    serve('POST', connectPath, json({ next: 'https://elsewhere.example/callback?code=made-up' }));
    await renderApp(address);

    fireEvent.click(allow());
    await settle();

    expectProblemPage();
    expect(leaveFor).not.toHaveBeenCalled();
  });

  it.each([403, 404])('shows the general error when Allow is answered %i', async (status) => {
    serve('POST', connectPath, problem(status));
    await renderApp(address);

    fireEvent.click(allow());
    await settle();

    expectProblemPage();
    expect(leaveFor).not.toHaveBeenCalled();
  });

  it('says that Allow failed above the buttons for a 500, and stays, ready to press again', async () => {
    serve('POST', connectPath, problem(500));
    await renderApp(address);

    fireEvent.click(allow());
    await settle();

    expect(alert().textContent).toBe('Not connected: something went wrong. Try again.');
    expect(alert().nextElementSibling).toContainElement(allow());
    expect(heading()).toHaveAccessibleName('Connect an AI assistant to Grow2Notes');
    expect(allow()).not.toHaveAttribute('aria-disabled');
    expect(cancel()).not.toHaveAttribute('aria-disabled');
    expect(leaveFor).not.toHaveBeenCalled();
  });

  it('says that Cancel failed for no connection', async () => {
    serve('POST', connectPath, noConnection);
    await renderApp(address);

    fireEvent.click(cancel());
    await settle();

    expect(alert().textContent).toBe('Not cancelled: no connection. Try again.');
  });
});

describe('a worker', () => {
  it('is refused in a line, and has Cancel only', async () => {
    await renderApp(address, worker);

    expect(heading()).toHaveAccessibleName('Connect an AI assistant to Grow2Notes');
    expect(sentences()).toEqual(['Only managers can connect an AI assistant to Grow2Notes.']);
    const buttons = within(main()).getAllByRole('button');
    expect(buttons).toHaveLength(1);
    expect(buttons[0]).toHaveAccessibleName('Cancel');
    expect(screen.queryByText(/Example Assistant/)).not.toBeInTheDocument();
  });

  it('posts Cancel with allow false, and goes to the address that the server answers with', async () => {
    await renderApp(address, worker);

    fireEvent.click(cancel());
    await settle();

    expect(bodiesSent('POST')).toEqual([{ request: 'made-up-handle', allow: false }]);
    expect(leaveFor).toHaveBeenCalledExactlyOnceWith(next);
  });
});

describe('a request that does not load', () => {
  it('is the general error when the address has no request', async () => {
    await renderApp('/connect');

    expectProblemPage();
    expect(bodiesSent('GET')).toHaveLength(0);
  });

  it.each([403, 404])('is the general error for a %i, at the same address', async (status) => {
    serve('GET', connectPath, problem(status));
    const { router } = await renderApp(address);

    expectProblemPage();
    expect(where(router)).toBe(address);
  });

  it('says that the details did not load for a 500, with Try again, and the screen’s heading', async () => {
    serve('GET', connectPath, problem(500));
    await renderApp(address);

    // After its one silent retry.
    await wait(1000);
    await settle();

    expect(heading()).toHaveAccessibleName('Connect an AI assistant to Grow2Notes');
    expect(
      within(main()).getByText('The assistant’s details did not load: something went wrong. Try again.'),
    ).toBeInTheDocument();
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('shows sign-in in place for a 401, at the same address, then the screen once signed in', async () => {
    const { router } = await renderApp(address, 'nobody');

    expect(heading()).toHaveAccessibleName('Sign in');
    expect(where(router)).toBe(address);
    expect(bodiesSent('GET')).toHaveLength(0);

    serve('POST', '/api/auth/login', json({ next: 'totp' }));
    serve('POST', '/api/auth/login/totp', json(manager));
    fireEvent.change(screen.getByRole('textbox', { name: 'Email address' }), {
      target: { value: 'sam.lee@example.org' },
    });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct horse battery staple' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await settle();
    fireEvent.change(screen.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' }), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await settle();

    expect(heading()).toHaveAccessibleName('Connect an AI assistant to Grow2Notes');
    expect(allow()).toBeInTheDocument();
    expect(where(router)).toBe(address);
  });
});
