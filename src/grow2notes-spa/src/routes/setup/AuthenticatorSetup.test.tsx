import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes, type AppRoute } from '../routes.tsx';
import { useSetup } from './setupContext.ts';

const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';
const startPath = '/api/auth/setup/start';
const keyPath = '/api/auth/setup/authenticator';
const title = 'Grow2Notes – Set up your account';
const intro =
  'An authenticator app is a free app on your phone. It shows a new 6-digit code every 30 seconds. You enter the code '
  + 'after your password each time you sign in.';
const stepOne =
  'Get an authenticator app on your phone if you do not have one, for example Google Authenticator or Microsoft '
  + 'Authenticator. Any authenticator app works.';
const stepTwo = 'Add Grow2Notes to the app. Scan the QR code, or use the setup key.';
const leadIn = 'Using this phone? Type the setup key into the app. If the app asks for a name, type Grow2Notes.';
const keyNote = 'Your app might call this a ‘key’ or ‘secret key’. You do not need to type the spaces.';
const stepThree = 'Come back to this page.';
const qrNotLoaded = 'The QR code did not load. Use the setup key instead.';

// A made-up person, link, password and key (A35), the key 32 base32 characters, as the invite stores one.
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const setupLink = '/setup#u=6f9619ff-8b86-d011-b42d-00cf4fc964ff&t=made-up-token';
const password = 'abcdefghijkl';
const sharedKey = 'GROWTWONOTESMADEUPSETUPKEY234567';
const groups = ['GROW', 'TWON', 'OTES', 'MADE', 'UPSE', 'TUPK', 'EY23', '4567'];

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, (init?: RequestInit) => Promise<Response>>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: (init?: RequestInit) => Promise<Response>) {
  answers.set(path, answer);
}

/** How many requests have been sent for `path`. */
function sent(path: string) {
  return fetchMock.mock.calls.filter(([sentPath]) => sentPath === path).length;
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

/** Shows what the setup layout holds: the account's email and the password, as held. */
function SetupMemory() {
  const { account: held, password: heldPassword } = useSetup();
  return (
    <>
      <p data-testid="setup-account">{held?.email ?? ''}</p>
      <p data-testid="setup-password">{heldPassword ?? ''}</p>
    </>
  );
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

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/**
 * Lets TanStack hand the queries' new state to the page, which it does on a `setTimeout` of 0 that the fake clock
 * runs 1 ms later when it is set while the clock is moving.
 */
async function settle() {
  await wait(1);
}

/** Opens the app at `path` on a memory router, under a query client of its own and StrictMode. */
async function open(path: string) {
  const router = createMemoryRouter(withSetupMemory(routes), { initialEntries: [path] });
  const client = createQueryClient();
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  await wait(0);
  return { router, client };
}

/** Creates a password on view F and presses Continue, as a person does, which pushes view G. */
async function continueFromViewF() {
  fireEvent.change(screen.getByLabelText('Create a password'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
  await wait(0);
}

/** Opens the emailed link, follows view E's password link to F, and goes on from there to view G. */
async function openViewG() {
  const opened = await open(setupLink);
  fireEvent.click(screen.getByRole('link', { name: 'Use a password and authenticator app' }));
  await wait(0);
  await continueFromViewF();
  expect(opened.router.state.location.pathname).toBe('/setup/app');
  return opened;
}

function heading() {
  return screen.getByRole('heading', { level: 1 });
}

function backLink() {
  return screen.getByRole('link', { name: 'Create a password' });
}

function steps() {
  return within(screen.getByRole('list')).getAllByRole('listitem');
}

function qrCode() {
  return screen.getByRole('img', { name: 'QR code for adding Grow2Notes to your authenticator app' });
}

/** The key region's line, in step 2, apart from the page's own status region. */
function keyLine() {
  return within(steps()[1]).getByRole('status');
}

/** The setup key itself: the value of the term "Setup key". */
function setupKey(): HTMLElement {
  const key = screen.getByText('Setup key', { selector: 'dt' }).nextElementSibling;
  if (!(key instanceof HTMLElement) || key.tagName !== 'DD') {
    throw new Error('The term "Setup key" has no value after it.');
  }

  return key;
}

/** Whether the query cache holds the key anywhere. */
function cacheHoldsKey(client: QueryClient) {
  return client
    .getQueryCache()
    .getAll()
    .some((query) => JSON.stringify(query.state.data ?? null).includes(sharedKey));
}

/** The elements that Tab reaches, in order: what is focusable, not hidden and not taken out with `tabindex="-1"`. */
function tabStops(): Element[] {
  const focusable = document.body.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]');
  return Array.from(focusable).filter((element) => element.closest('[hidden]') === null && element.tabIndex >= 0);
}

/** Each element comes after the one before it in the page. */
function expectInOrder(...elements: Element[]) {
  for (const [index, element] of elements.slice(1).entries()) {
    expect(elements[index].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

beforeEach(() => {
  vi.useFakeTimers();
  serve(antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up' } })),
  );
  // The shell's, for a person with no session.
  serve(mePath, () => Promise.resolve(new Response(null, { status: 401 })));
  serve(startPath, () => json(account));
  // As the server answers it, with the URI that no screen shows.
  serve(keyPath, () => json({ sharedKey, authenticatorUri: 'otpauth://totp/made-up' }));
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('AuthenticatorSetup (view G)', () => {
  it('shows the back link, then the h1, the intro and three steps, with the QR code, lead-in and key', async () => {
    await openViewG();
    await settle();

    expect(document.getElementById('page-bar')).toContainElement(backLink());
    expect(backLink()).toHaveAttribute('href', '/setup/password');

    const main = within(screen.getByRole('main'));
    expect(heading()).toHaveTextContent('Set up your authenticator app');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent(intro);
    expect(screen.getByRole('list').tagName).toBe('OL');
    expect(steps()).toHaveLength(3);
    expect(steps()[0]).toHaveTextContent(stepOne);
    expect(steps()[2]).toHaveTextContent(stepThree);

    expect(qrCode()).toHaveAttribute('src', '/api/auth/setup/authenticator/qr.png');
    expect(qrCode()).toHaveAttribute('width', '200');
    expect(qrCode()).toHaveAttribute('height', '200');
    expectInOrder(
      heading(),
      main.getByText(intro),
      main.getByText(stepOne),
      main.getByText(stepTwo),
      qrCode(),
      main.getByText(leadIn),
      main.getByText('Setup key'),
      setupKey(),
      main.getByText(keyNote),
      keyLine(),
      main.getByText(stepThree),
    );
    expect(steps()[1]).toContainElement(qrCode());
    expect(steps()[1]).toContainElement(setupKey());
    expect(keyLine()).toBeEmptyDOMElement();

    // No Copy button, and nothing else to press but the back link.
    expect(main.queryByRole('button')).not.toBeInTheDocument();
    expect(tabStops()).toEqual([screen.getByRole('link', { name: 'Skip to main content' }), backLink()]);
    expect(document.title).toBe(title);
  });

  it('draws the QR code at its own pixels on a white panel that a browser never darkens', async () => {
    await openViewG();

    const panel = qrCode().parentElement;
    expect(panel).toHaveStyle({ background: 'var(--colour-surface)', colorScheme: 'only light' });
    expect(qrCode()).toHaveStyle({ imageRendering: 'pixelated' });
  });

  it('shows the key in capitals, in groups of four whose text and selection hold no spaces', async () => {
    serve(keyPath, () => json({ sharedKey: sharedKey.toLowerCase(), authenticatorUri: 'otpauth://totp/made-up' }));
    await openViewG();
    await settle();

    const key = setupKey();
    expect(Array.from(key.children, (group) => [group.tagName, group.textContent])).toEqual(
      groups.map((group) => ['SPAN', group]),
    );
    expect(key.textContent).toBe(sharedKey);
    expect(key).toHaveAttribute('translate', 'no');
    expect(key).toHaveStyle({ fontFamily: 'var(--font-family-code)', userSelect: 'all' });

    const selection = window.getSelection();
    const range = document.createRange();
    range.selectNodeContents(key);
    selection?.removeAllRanges();
    selection?.addRange(range);
    expect(selection?.toString()).toBe(sharedKey);
  });

  it('fetches the key as G shows, and says "Loading…" if it has not come after 1 s, and not before', async () => {
    serve(keyPath, noAnswer);
    await openViewG();
    expect(sent(keyPath)).toBeGreaterThan(0);

    await wait(999);
    expect(keyLine()).toBeEmptyDOMElement();
    expect(qrCode()).toBeInTheDocument();

    await wait(1);
    expect(keyLine()).toHaveTextContent('Loading…');
    expect(screen.queryByText('Setup key')).not.toBeInTheDocument();
  });

  describe('when the key does not load', () => {
    it.each([
      { failure: 'no connection', answer: noConnection, cause: 'no connection' },
      { failure: 'a 503', answer: () => problem(503), cause: 'something went wrong' },
      { failure: 'a 429', answer: () => problem(429), cause: 'something went wrong' },
    ])('says so at once for $failure, with Try again, which fetches it again', async ({ answer, cause }) => {
      serve(keyPath, answer);
      await openViewG();
      await settle();

      expect(keyLine().textContent).toBe(`The setup key did not load: ${cause}. Try again.`);
      const tryAgain = screen.getByRole('button', { name: 'Try again' });
      expect(keyLine()).not.toContainElement(tryAgain);
      expect(steps()[1]).toContainElement(tryAgain);
      expect(qrCode()).toBeInTheDocument();
      // Never sent again by itself.
      const requests = sent(keyPath);
      await wait(5000);
      expect(sent(keyPath)).toBe(requests);
      serve(keyPath, () => json({ sharedKey, authenticatorUri: 'otpauth://totp/made-up' }));

      tryAgain.focus();
      fireEvent.click(tryAgain);
      await settle();

      expect(sent(keyPath)).toBe(requests + 1);
      expect(setupKey().textContent).toBe(sharedKey);
      expect(keyLine()).toBeEmptyDOMElement();
      expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    });

    it('shows the timed-out page for a 401, and drops setup\'s memory', async () => {
      serve(keyPath, () => problem(401));
      const { router } = await openViewG();
      await settle();

      expect(heading()).toHaveTextContent('Your setup session timed out');
      expect(heading()).toHaveFocus();
      expect(heading().nextElementSibling).toHaveTextContent('Open the link from your email again.');
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(screen.queryByRole('img')).not.toBeInTheDocument();
      expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
      expect(screen.getByTestId('setup-account')).toBeEmptyDOMElement();
      expect(screen.getByTestId('setup-password')).toBeEmptyDOMElement();
      expect(document.title).toBe(title);

      await act(() => router.navigate(-1));
      expect(router.state.location.pathname).toBe('/setup/password');
      expect(heading().nextElementSibling).toHaveTextContent(
        'To set up your account, open the link from your email again.',
      );
    });
  });

  it('puts the QR code\'s line in place of the image when it does not load, and keeps the key', async () => {
    await openViewG();
    await settle();

    fireEvent.error(qrCode());

    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    const line = screen.getByText(qrNotLoaded);
    expect(steps()[1]).toContainElement(line);
    expectInOrder(screen.getByText(stepTwo), line, screen.getByText(leadIn), setupKey());
    expect(setupKey().textContent).toBe(sharedKey);
  });

  it('drops the key from the query cache once G has gone, and fetches it again when G shows again', async () => {
    const { client } = await openViewG();
    await settle();
    expect(cacheHoldsKey(client)).toBe(true);
    const requests = sent(keyPath);

    fireEvent.click(backLink());
    await wait(0);

    expect(heading()).toHaveTextContent('Create a password');
    expect(cacheHoldsKey(client)).toBe(false);

    await continueFromViewF();
    await settle();

    expect(sent(keyPath)).toBeGreaterThan(requests);
    expect(setupKey().textContent).toBe(sharedKey);
  });

  it('goes back one entry to view F on the back link, adding none, with the password still in the field', async () => {
    const { router } = await openViewG();

    fireEvent.click(backLink());
    await wait(0);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/setup/password');
    expect(heading()).toHaveFocus();
    expect(screen.getByLabelText('Create a password')).toHaveValue(password);
    expect(screen.queryByRole('link', { name: 'Create a password' })).not.toBeInTheDocument();
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/setup/app');
  });

  it.each([
    {
      memory: 'nothing',
      reach: async () => {
        await open('/setup/app');
      },
    },
    {
      memory: 'the account but no password',
      reach: async () => {
        const { router } = await open(setupLink);
        await act(() => router.navigate('/setup/app'));
      },
    },
  ])('shows the no-link message with $memory in memory, and fetches no key', async ({ reach }) => {
    await reach();
    await wait(5000);

    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent(
      'To set up your account, open the link from your email again.',
    );
    expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(sent(keyPath)).toBe(0);
    expect(document.title).toBe(title);
  });
});
