import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes, type AppRoute } from '../routes.tsx';
import { useSetup } from './setupContext.ts';

const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';
const startPath = '/api/auth/setup/start';
const keyPath = '/api/auth/setup/authenticator';
const passwordPath = '/api/auth/setup/password';
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
const codeLabel = 'Enter the 6-digit code from your authenticator app';
const codeHint = 'Find Grow2Notes in the app. If the code changes while you’re typing, you can still use it.';
const wrongFormat = 'The code must be 6 digits';
const codeRejected = 'That code did not work. Enter the code the app shows for Grow2Notes now.';
const chooseMethod = 'Choose how you’ll sign in';
// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = '\u00a0';

// A made-up person, link, password and key (A35), the key 32 base32 characters, as the invite stores one.
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const setupLink = '/setup#u=6f9619ff-8b86-d011-b42d-00cf4fc964ff&t=made-up-token';
const password = 'abcdefghijkl';
const sharedKey = 'GROWTWONOTESMADEUPSETUPKEY234567';
const groups = ['GROW', 'TWON', 'OTES', 'MADE', 'UPSE', 'TUPK', 'EY23', '4567'];
// The new manager that a completed setup signs in, as the server answers it.
const me = {
  userId: '6f9619ff-8b86-d011-b42d-00cf4fc964ff',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Example Care',
  today: '2026-10-13',
};
// A made-up worker (A35) whose session is live in the same browser, as setup's pages show whoever is signed in.
const someoneElse = {
  userId: '0c1d7e52-93a8-4b6f-a2d4-58e1f9c3b706',
  displayName: 'Riley Ng',
  role: 'Worker',
  organisationName: 'Example Care',
  today: '2026-10-13',
};
// Stands for what the tab showed before the emailed link, such as the email itself.
const entryBefore = '/made-up-page-before-setup';

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

/** The bodies of the requests sent to `path`, in order. */
function bodies(path: string): unknown[] {
  return fetchMock.mock.calls
    .filter(([sentPath]) => sentPath === path)
    .map(([, init]) => (typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined));
}

/** The paths of the requests sent after the first `count`. */
function sentAfter(count: number): unknown[] {
  return fetchMock.mock.calls.slice(count).map(([path]) => path);
}

/** A `200` with a JSON body. */
function json(body: unknown): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its extensions such as `code`. */
function problem(status: number, extensions: Record<string, unknown> = {}): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** A `422 validation.failed` for `field`, as the server refuses the code or the password (D70). */
function refused(field: 'totpCode' | 'password'): Promise<Response> {
  return problem(422, { code: 'validation.failed', errors: { [field]: ['A made-up reason.'] } });
}

/** An answer that comes when the test gives it. */
function later() {
  let give: (response: Response) => void = () => undefined;
  const value = new Promise<Response>((resolve) => {
    give = resolve;
  });
  return { value, give };
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

/**
 * Records each element added to the page from now on, and gives a function that returns them all, so that a test can
 * find whether something was ever shown, however briefly.
 */
function recordAdded(): () => Element[] {
  const added: Element[] = [];
  const keep = (records: MutationRecord[]) => {
    for (const record of records) {
      added.push(...Array.from(record.addedNodes).filter((node) => node instanceof Element));
    }
  };
  const observer = new MutationObserver(keep);
  observer.observe(document.body, { childList: true, subtree: true });
  return () => {
    keep(observer.takeRecords());
    observer.disconnect();
    return added;
  };
}

/**
 * Opens the app at `path` on a memory router, after the `before` entries, under a query client of its own and
 * StrictMode.
 */
async function open(path: string, before: string[] = []) {
  const router = createMemoryRouter(withSetupMemory(routes), {
    initialEntries: [...before, path],
    initialIndex: before.length,
  });
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

/**
 * Opens the emailed link, after the `before` entries, follows view E's password link to F, and goes on from there to
 * view G.
 */
async function openViewG(before: string[] = []) {
  const opened = await open(setupLink, before);
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

function codeField(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>(codeLabel);
}

/** Finish setup, by its name at rest or busy. */
function finishButton() {
  return screen.getByRole('button', { name: /^Finish(?:ing)? setup/ });
}

/** The request-failure alert above Finish setup. */
function alert() {
  return within(screen.getByRole('main')).getByRole('alert');
}

/** Focuses Finish setup and presses it, as a keyboard user would, and returns it. */
function pressFinish() {
  const button = finishButton();
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Types `typed` into the code field, as it is, and presses Finish setup. */
function finishWith(typed: string) {
  fireEvent.change(codeField(), { target: { value: typed } });
  return pressFinish();
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

/**
 * The elements that Tab reaches, in order: what is focusable, neither hidden nor in a closed dialog, such as the
 * session warning, and not taken out with `tabindex="-1"`.
 */
function tabStops(): Element[] {
  const focusable = document.body.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]');
  return Array.from(focusable).filter(
    (element) => element.closest('[hidden], dialog:not([open])') === null && element.tabIndex >= 0,
  );
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

    // No Copy button: the one button is the code step's.
    expect(main.getAllByRole('button')).toEqual([finishButton()]);
    expect(steps()[2]).toContainElement(finishButton());
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

describe('AuthenticatorSetup (view G), its code step', () => {
  it('has in step 3 the line, then a form of the code field and its hint, the alert and Finish setup', async () => {
    await openViewG();
    await settle();

    const step = within(steps()[2]);
    const form = codeField().form;
    expect(form?.noValidate).toBe(true);
    expect(steps()[2]).toContainElement(form);
    expectInOrder(
      step.getByText(stepThree),
      step.getByText(codeLabel, { selector: 'label' }),
      step.getByText(codeHint),
      codeField(),
      alert(),
      finishButton(),
    );
    expect(form).toContainElement(alert());
    expect(form).toContainElement(finishButton());

    expect(codeField()).toHaveAttribute('type', 'text');
    expect(codeField()).toHaveAttribute('name', 'code');
    expect(codeField()).toHaveAttribute('inputmode', 'numeric');
    expect(codeField()).toHaveAttribute('autocomplete', 'one-time-code');
    expect(codeField()).not.toHaveAttribute('maxlength');
    expect(codeField()).toHaveValue('');
    expect(codeField()).toHaveAccessibleDescription(codeHint);
    expect(codeField()).not.toHaveAttribute('aria-invalid');
    // The h1 takes focus as G shows, not the field at the bottom of the steps.
    expect(heading()).toHaveFocus();

    // There from the start, and empty until a request fails.
    expect(alert()).toBeEmptyDOMElement();
    expect(finishButton()).toHaveAccessibleName('Finish setup');
    expect(finishButton()).toHaveAttribute('type', 'submit');
    expect(finishButton()).toHaveClass(...buttonClass('primary').split(' '));
    expect(finishButton()).not.toHaveAttribute('aria-disabled');
    expect(document.title).toBe(title);
  });

  it('is reached by Tab in the order back link, code field, Finish setup, after the skip link', async () => {
    await openViewG();
    await settle();

    expect(tabStops()).toEqual([
      screen.getByRole('link', { name: 'Skip to main content' }),
      backLink(),
      codeField(),
      finishButton(),
    ]);
  });

  describe('on Finish setup', () => {
    it.each([
      { typed: '', kind: 'nothing', message: codeLabel },
      { typed: ' - – ', kind: 'only spaces and dashes', message: codeLabel },
      { typed: '12345', kind: 'five digits', message: wrongFormat },
      { typed: '1234567', kind: 'seven digits', message: wrongFormat },
      { typed: 'abcdef', kind: 'letters', message: wrongFormat },
    ])('says "$message" for $kind, on the field, with focus there, and sends nothing', async ({
      typed,
      message,
    }) => {
      const { router } = await openViewG();
      await settle();

      finishWith(typed);
      await wait(0);

      expect(codeField()).toHaveFocus();
      expect(codeField()).toHaveAttribute('aria-invalid', 'true');
      expect(codeField()).toHaveAttribute('aria-describedby', 'code-hint code-error');
      expect(codeField()).toHaveAccessibleDescription(`${codeHint} Error: ${message}`);
      expectInOrder(screen.getByText(codeHint), screen.getByText(message, { selector: 'p' }), codeField());
      expect(codeField()).toHaveValue(typed);
      expect(alert()).toBeEmptyDOMElement();
      expect(document.title).toBe(`Error: ${title}`);
      expect(sent(passwordPath)).toBe(0);
      expect(router.state.location.pathname).toBe('/setup/app');
    });

    it('moves focus to the field again on each press that finds the same message', async () => {
      await openViewG();
      await settle();
      finishWith('12345');
      await wait(0);
      expect(codeField()).toHaveFocus();

      pressFinish();
      await wait(0);

      expect(codeField()).toHaveFocus();
      expect(screen.getAllByText(wrongFormat)).toHaveLength(1);
    });

    it('checks again as the person types after a message, and takes it and the title\'s "Error: " away', async () => {
      await openViewG();
      await settle();
      finishWith('');
      await wait(0);

      fireEvent.change(codeField(), { target: { value: '123' } });
      expect(codeField()).toHaveAccessibleDescription(`${codeHint} Error: ${wrongFormat}`);
      expect(document.title).toBe(`Error: ${title}`);

      fireEvent.change(codeField(), { target: { value: '123 456' } });
      await wait(0);

      expect(codeField()).not.toHaveAttribute('aria-invalid');
      expect(codeField()).toHaveAccessibleDescription(codeHint);
      expect(document.title).toBe(title);
      expect(sent(passwordPath)).toBe(0);
    });

    it.each([
      { typed: '123456', kind: 'six digits' },
      { typed: '123 456', kind: 'a space' },
      { typed: '123-456', kind: 'a hyphen' },
      { typed: '１２３４５６', kind: 'full-width digits' },
    ])('sends a code with $kind as its six digits, with the password as F took it', async ({ typed }) => {
      serve(passwordPath, noAnswer);
      await openViewG();
      await settle();

      finishWith(typed);
      await wait(0);

      expect(bodies(passwordPath)).toEqual([{ password, totpCode: '123456' }]);
    });
  });

  it('shows a refused code on the field, empties it, keeps the password, and takes a new code', async () => {
    serve(passwordPath, () => refused('totpCode'));
    const { router } = await openViewG();
    await settle();

    finishWith('123 456');
    await wait(0);

    expect(codeField()).toHaveValue('');
    expect(codeField()).toHaveFocus();
    expect(codeField()).toHaveAttribute('aria-invalid', 'true');
    expect(codeField()).toHaveAccessibleDescription(`${codeHint} Error: ${codeRejected}`);
    expect(alert()).toBeEmptyDOMElement();
    expect(finishButton()).not.toHaveAttribute('aria-disabled');
    expect(document.title).toBe(`Error: ${title}`);
    expect(router.state.location.pathname).toBe('/setup/app');
    expect(screen.getByTestId('setup-password').textContent).toBe(password);
    serve(passwordPath, noAnswer);

    finishWith('654-321');
    await wait(0);

    expect(bodies(passwordPath)).toEqual([
      { password, totpCode: '123456' },
      { password, totpCode: '654321' },
    ]);
    expect(document.title).toBe(title);
  });

  it('goes back one entry to view F for a refused password, with its message on F\'s focused field', async () => {
    serve(passwordPath, () => refused('password'));
    const { router } = await openViewG();
    await settle();

    finishWith('123456');
    await wait(0);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/setup/password');
    const field = screen.getByLabelText('Create a password');
    expect(field).toHaveValue(password);
    expect(field).toHaveFocus();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveAccessibleDescription(
      'Use at least 12 characters. Error: Password must be 12 characters or more',
    );
    expect(document.title).toBe(`Error: ${title}`);

    // F's own check passes it, so Continue takes it again, which ends the refusal.
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await wait(0);

    expect(router.state.location.pathname).toBe('/setup/app');
    expect(heading()).toHaveFocus();
    expect(document.title).toBe(title);
    fireEvent.click(backLink());
    await wait(0);
    expect(screen.getByLabelText('Create a password')).not.toHaveAttribute('aria-invalid');
    expect(heading()).toHaveFocus();
  });

  it.each([
    { failure: 'no connection', answer: noConnection, sentence: 'Not set up: no connection. Try again.' },
    { failure: 'a 503', answer: () => problem(503), sentence: 'Not set up: something went wrong. Try again.' },
    { failure: 'a 429', answer: () => problem(429), sentence: 'Too many tries. Wait 1 minute, then try again.' },
  ])('says so for $failure above Finish setup, where focus stays, and sends nothing by itself', async ({
    answer,
    sentence,
  }) => {
    serve(passwordPath, answer);
    const { router } = await openViewG();
    await settle();

    const button = finishWith('123 456');
    await wait(0);

    expect(alert().textContent).toBe(sentence);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(alert().compareDocumentPosition(button)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(codeField()).toHaveValue('123 456');
    expect(codeField()).not.toHaveAttribute('aria-invalid');
    expect(document.title).toBe(`Error: ${title}`);
    expect(router.state.location.pathname).toBe('/setup/app');
    await wait(60_000);
    expect(sent(passwordPath)).toBe(1);
  });

  it('clears the alert and the title\'s "Error: " at the next press', async () => {
    serve(passwordPath, noConnection);
    await openViewG();
    await settle();
    finishWith('123456');
    await wait(0);
    expect(document.title).toBe(`Error: ${title}`);
    serve(passwordPath, noAnswer);

    pressFinish();

    expect(alert()).toBeEmptyDOMElement();
    expect(document.title).toBe(title);
  });

  it('shows the timed-out page for a 401, and drops setup\'s memory', async () => {
    serve(passwordPath, () => problem(401));
    await openViewG();
    await settle();

    finishWith('123456');
    await wait(0);

    expect(heading()).toHaveTextContent('Your setup session timed out');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent('Open the link from your email again.');
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByTestId('setup-account')).toBeEmptyDOMElement();
    expect(screen.getByTestId('setup-password')).toBeEmptyDOMElement();
    expect(document.title).toBe(title);
  });

  it('shows the busy label at 400 ms, not before, and sends nothing more for a second press while busy', async () => {
    serve(passwordPath, noAnswer);
    await openViewG();
    await settle();

    const button = finishWith('123456');
    fireEvent.click(button);

    expect(button).toHaveAttribute('aria-disabled', 'true');
    await wait(399);
    expect(button).toHaveAccessibleName('Finish setup');
    await wait(1);
    expect(button).toHaveAccessibleName('Finishing setup…');

    fireEvent.click(button);
    await wait(0);

    expect(button).toHaveFocus();
    expect(sent(passwordPath)).toBe(1);
    // The field stays as it was, never disabled.
    expect(codeField()).not.toHaveAttribute('disabled');
    expect(codeField()).toHaveValue('123456');
  });

  it('on 200, puts the Me in [\'me\'], then makes /setup\'s entry Today\'s, which sends only its own /me', async () => {
    serve(passwordPath, () => {
      // The session that setup starts, which /me then answers for.
      serve(mePath, () => json(me));
      return json(me);
    });
    const { router, client } = await openViewG([entryBefore]);
    await settle();
    const requests = fetchMock.mock.calls.length;
    const added = recordAdded();

    finishWith('123 456');
    await wait(0);

    expect(bodies(passwordPath)).toEqual([{ password, totpCode: '123456' }]);
    expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
    expect(router.state.historyAction).toBe('REPLACE');
    expect(router.state.location.pathname).toBe('/');
    expect(router.state.location.state).toBeNull();
    // Today for the Me's today, in the signed-in frame (sign-in.md *Interactions* 12), which the new location's one
    // refresh of /me, for the session that setup started, leaves as it is.
    expect(heading()).toHaveAccessibleName(`Today, Tuesday 13${nbsp}October`);
    expect(heading().querySelector('time')).toHaveAttribute('datetime', me.today);
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Today');
    expect(screen.queryByTestId('setup-password')).not.toBeInTheDocument();
    // View E never showed on the way, so neither did its h2, nor did its passkey prefetch go, which would get a 401
    // for a user who is now Active.
    const shown = added();
    expect(shown.some((element) => element.textContent?.includes('Tuesday 13'))).toBe(true);
    expect(shown.filter((element) => element.textContent?.includes(chooseMethod))).toEqual([]);
    await wait(10 * 60_000);
    // The new location's one refresh (app-shell.md *Refresh of `/api/auth/me`*), and nothing more.
    expect(sentAfter(requests)).toEqual([passwordPath, mePath]);

    // The two entries after Today's are Forward's, where setup's memory has gone.
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/setup/password');
    expect(heading().nextElementSibling).toHaveTextContent(
      'To set up your account, open the link from your email again.',
    );

    // Back from Today reaches the entry before setup.
    await act(() => router.navigate(-2));
    expect(router.state.location.pathname).toBe(entryBefore);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it('finishes setup when its answer comes after G has gone, and Today then takes /setup\'s entry', async () => {
    const answer = later();
    serve(passwordPath, () => answer.value);
    const { router, client } = await openViewG([entryBefore]);
    await settle();
    finishWith('123456');
    await wait(0);
    fireEvent.click(backLink());
    await wait(0);

    // The session that setup starts, which /me then answers for.
    serve(mePath, () => json(me));
    answer.give(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
    await wait(0);

    // The person stays where they went, now signed in.
    expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
    expect(router.state.location.pathname).toBe('/setup/password');

    fireEvent.click(screen.getByRole('link', { name: 'Set up your account' }));
    await wait(0);

    expect(router.state.location.pathname).toBe('/');
    expect(heading()).toHaveAccessibleName(`Today, Tuesday 13${nbsp}October`);
    await act(() => router.navigate(-1));
    expect(router.state.location.pathname).toBe(entryBefore);
  });

  it('keeps its Me in [\'me\'] when a refresh sent before its answer answers after it, for another', async () => {
    // Another person is signed in, so /me answers them until setup's answer replaces the cookie.
    let session = someoneElse;
    serve(mePath, () => json(session));
    const answer = later();
    serve(passwordPath, () => answer.value);
    const { router, client } = await openViewG();
    await settle();
    expect(client.getQueryData(meQuery.queryKey)).toEqual(someoneElse);
    finishWith('123456');
    await wait(0);

    // The window regains focus while Finish setup's request is out, and the shell's refresh goes with the other
    // person's cookie; its answer is held until after setup's.
    const stale = later();
    serve(mePath, () => {
      serve(mePath, () => json(session));
      return stale.value;
    });
    act(() => {
      window.dispatchEvent(new Event('focus'));
    });
    session = me;
    answer.give(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
    await wait(0);
    stale.give(new Response(JSON.stringify(someoneElse), { headers: { 'Content-Type': 'application/json' } }));
    await wait(0);

    expect(router.state.location.pathname).toBe('/');
    expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
    expect(screen.getByText('Signed in as Sam Lee')).toBeInTheDocument();
    expect(screen.queryByText('Signed in as Riley Ng')).not.toBeInTheDocument();
    // A manager's nav, not the worker's.
    expect(within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name: 'Manage' }))
      .toBeInTheDocument();
  });
});
