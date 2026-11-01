import type * as SimpleWebAuthn from '@simplewebauthn/browser';
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

// The browser's side of WebAuthn, which jsdom does not have: whether it has WebAuthn, the prompt, and its cancel.
const webAuthn = vi.hoisted(() => ({
  supported: true,
  startRegistration: vi.fn<typeof SimpleWebAuthn.startRegistration>(),
  cancelCeremony: vi.fn<() => void>(),
}));

vi.mock('@simplewebauthn/browser', () => ({
  browserSupportsWebAuthn: () => webAuthn.supported,
  startRegistration: webAuthn.startRegistration,
  WebAuthnAbortService: { cancelCeremony: webAuthn.cancelCeremony },
}));

const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';
const startPath = '/api/auth/setup/start';
const keyPath = '/api/auth/setup/authenticator';
const optionsPath = '/api/auth/setup/passkey/options';
const passwordOnlyPath = '/api/auth/setup/password-only';
const passwordAndPasskeyPath = '/api/auth/setup/password-and-passkey';
const appLink = 'Use a password and authenticator app';
const passwordOnlyLink = 'Use a password only';
const passwordAndPasskeyLink = 'Use a password and a passkey';
const passkeyNext =
  'When you select Finish setup, this device asks for your face, fingerprint or PIN to make your passkey.';
// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';
const passkeyNotSetUp =
  'Your passkey was not set up. Try again, or open the link from your email in your phone’s browser.';
const title = 'Grow2Notes – Set up your account';
const hint = 'Use at least 12 characters.';
const enterPassword = 'Enter a password';
const tooShort = 'Password must be 12 characters or more';

// A made-up person and link (A35).
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const setupLink = '/setup#u=6f9619ff-8b86-d011-b42d-00cf4fc964ff&t=made-up-token';
const me = {
  userId: '6f9619ff-8b86-d011-b42d-00cf4fc964ff',
  displayName: 'Sam Lee',
  role: 'Worker',
  organisationName: 'Example Care',
  today: '2026-10-12',
};
// A made-up password (A35) of 12 characters with spaces round it, which setup sends as typed.
const longPassword = ' made-up pass ';

// A made-up passkey, as the prompt makes it.
const credential: SimpleWebAuthn.RegistrationResponseJSON = {
  id: 'bWFkZS11cA',
  rawId: 'bWFkZS11cA',
  response: { attestationObject: 'bWFkZS11cA', clientDataJSON: 'bWFkZS11cA' },
  type: 'public-key',
  clientExtensionResults: {},
};

type Answer = () => Promise<Response>;

const fetchMock = vi.fn<typeof fetch>();

/** The answers to each request the page sends, in turn, for the paths that these tests give answers for. */
const answers: Record<string, Answer[]> = {};

/** The number of creation options served, from which each new one takes its challenge. */
let served = 0;

/** Whom the app's session is for, and so whom `/api/auth/me` answers: nobody, until a test's setup ends. */
let session: typeof me | undefined;

/** The query client of the page that `open` rendered. */
let client: QueryClient;

/** Gives `path` its next answer. */
function serve(path: string, answer: Answer) {
  (answers[path] ??= []).push(answer);
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

/** A `422` that names `field`. */
function refusedField(field: string): Promise<Response> {
  return problem(422, { code: 'validation.failed', errors: { [field]: ['A made-up reason.'] } });
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** A request that no answer ever comes to. */
function noAnswer(): Promise<Response> {
  return new Promise(() => undefined);
}

/** A WebAuthn error as the browser raises it, which the page never names to the person. */
function browserError(name: string): DOMException {
  return new DOMException(`A made-up ${name}.`, name);
}

/** Made-up creation options, each with a challenge of its own. */
function creationOptions(challenge: string): SimpleWebAuthn.PublicKeyCredentialCreationOptionsJSON {
  return {
    rp: { id: 'grow2notes.example', name: 'Grow2Notes' },
    user: { id: 'NmY5NjE5ZmY', name: account.email, displayName: account.displayName },
    challenge,
    pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
    timeout: 300_000,
    authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
  };
}

/** The bodies of the requests sent to `path`, in order. */
function sent(path: string): unknown[] {
  return fetchMock.mock.calls
    .filter(([sentTo]) => sentTo === path)
    .map(([, init]) => (typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined));
}

/** The path of each request sent, but the shell's own `GET /api/auth/me` and view G's `GET` of its key, in order. */
function pageRequests(): unknown[] {
  return fetchMock.mock.calls.map(([path]) => path).filter((path) => path !== mePath && path !== keyPath);
}

/** Shows what the setup layout holds: the account's email, the password as held, and the password choice. */
function SetupMemory() {
  const { account: held, password, passwordChoice } = useSetup();
  return (
    <>
      <p data-testid="setup-account">{held?.email ?? ''}</p>
      <p data-testid="setup-password">{password ?? ''}</p>
      <p data-testid="setup-choice">{passwordChoice}</p>
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

/** Opens the app at `path` on a memory router, under a query client of its own and StrictMode. */
async function open(path: string) {
  const router = createMemoryRouter(withSetupMemory(routes), { initialEntries: [path] });
  client = createQueryClient();
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  await wait(0);
  return router;
}

/** Opens the emailed link and follows view E's link named `choice` to view F, as a person does. */
async function openViewF(choice = appLink) {
  const router = await open(setupLink);
  fireEvent.click(screen.getByRole('link', { name: choice }));
  await wait(0);
  expect(router.state.location.pathname).toBe('/setup/password');
  return router;
}

function heading() {
  return screen.getByRole('heading', { level: 1 });
}

function field(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Create a password');
}

function toggle() {
  return screen.getByRole('button', { name: /^(Show|Hide) password$/ });
}

function continueButton() {
  return screen.getByRole('button', { name: 'Continue' });
}

function finishButton() {
  return screen.getByRole('button', { name: 'Finish setup' });
}

function alert() {
  return within(screen.getByRole('main')).getByRole('alert');
}

function backLink() {
  return screen.getByRole('link', { name: 'Set up your account' });
}

/** The toggle's polite line. */
function toggleLine() {
  const line = screen.getByRole('main').querySelector('[aria-live="polite"]');
  if (line === null) {
    throw new Error('View F rendered no polite line.');
  }

  return line;
}

function usernameField(): HTMLInputElement {
  const username = screen.getByRole('main').querySelector<HTMLInputElement>('input[name="username"]');
  if (username === null) {
    throw new Error('View F rendered no username field.');
  }

  return username;
}

/** Types `value` into the field, as it is, and presses Continue as a keyboard user would. */
function submit(value: string) {
  fireEvent.change(field(), { target: { value } });
  continueButton().focus();
  fireEvent.click(continueButton());
}

/** Types `value` into the field, as it is, and presses Finish setup as a keyboard user would, and returns it. */
function finishWith(value: string) {
  fireEvent.change(field(), { target: { value } });
  const button = finishButton();
  button.focus();
  fireEvent.click(button);
  return button;
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
  served = 0;
  session = undefined;
  webAuthn.supported = true;
  // A prompt still open, unless a test answers it.
  webAuthn.startRegistration.mockReturnValue(new Promise(() => undefined));
  fetchMock.mockImplementation((path) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up' } }));
    }

    // The shell's, for a person with no session until a test's setup ends.
    if (path === mePath) {
      return session === undefined ? problem(401) : json(session);
    }

    if (path === startPath) {
      return Promise.resolve(
        new Response(JSON.stringify(account), { headers: { 'Content-Type': 'application/json' } }),
      );
    }

    // View G's, once Continue has gone on to it.
    if (path === keyPath) {
      return Promise.resolve(
        new Response(JSON.stringify({ sharedKey: 'GROWTWONOTESMADEUPSETUPKEY234567' }), {
          headers: { 'Content-Type': 'application/json' },
        }),
      );
    }

    const answer = answers[path as string]?.shift();
    if (answer !== undefined) {
      return answer();
    }

    // New options are served whenever the test gives no other answer for them.
    if (path === optionsPath) {
      served += 1;
      return json(creationOptions(`challenge-${served}`));
    }

    throw new Error('View F sends no request.');
  });
  vi.stubGlobal('fetch', fetchMock);
  document.title = '';
});

afterEach(() => {
  for (const queue of Object.values(answers)) {
    queue.length = 0;
  }

  fetchMock.mockReset();
  webAuthn.startRegistration.mockReset();
  webAuthn.cancelCeremony.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('CreatePassword (view F)', () => {
  it('shows the back link, then a form of the h1 labelling the field, the hint, field, Show and Continue', async () => {
    await openViewF();

    const pageBar = document.getElementById('page-bar');
    expect(pageBar).toContainElement(backLink());
    expect(backLink()).toHaveAttribute('href', '/setup');

    const main = within(screen.getByRole('main'));
    const form = heading().closest('form');
    expect(form).not.toBeNull();
    expect(form?.noValidate).toBe(true);
    expect(heading()).toHaveTextContent('Create a password');
    expect(heading()).toHaveFocus();
    expect(heading()).toContainElement(screen.getByText('Create a password', { selector: 'label' }));

    // Not the arrival focus, which only the server's refusal makes it (AuthenticatorSetup.test.tsx).
    expect(field()).not.toHaveAttribute('data-route-focus');
    expect(field()).toHaveAttribute('type', 'password');
    expect(field()).toHaveAttribute('autocomplete', 'new-password');
    expect(field()).toHaveAttribute('minlength', '12');
    expect(field()).toHaveAttribute('spellcheck', 'false');
    expect(field()).toHaveAttribute('autocapitalize', 'none');
    expect(field()).toHaveAttribute('autocorrect', 'off');
    expect(field()).toHaveValue('');
    expect(field()).toHaveAttribute('aria-describedby', 'password-hint');
    expect(field()).toHaveAccessibleDescription(hint);
    expect(field()).not.toHaveAttribute('aria-invalid');

    expect(toggle()).toHaveAccessibleName('Show password');
    expect(toggle()).toHaveTextContent('Show');
    expect(toggle()).toHaveAttribute('type', 'button');
    expect(toggle()).toHaveAttribute('aria-controls', field().id);
    expect(toggle()).toHaveStyle({ minBlockSize: 'var(--target-button)' });
    // There from the start, and saying nothing until Show is pressed.
    expect(toggleLine()).toBeEmptyDOMElement();

    expect(continueButton()).toHaveAttribute('type', 'submit');
    expect(continueButton()).toHaveClass(...buttonClass('primary').split(' '));
    expect(continueButton()).not.toHaveAttribute('aria-disabled');

    expect(main.queryByRole('alert')).not.toBeInTheDocument();
    expectInOrder(
      heading(),
      main.getByText(hint),
      field(),
      toggle(),
      toggleLine(),
      usernameField(),
      continueButton(),
    );
    expect(form).toContainElement(continueButton());
    expect(document.title).toBe(title);
  });

  it('has no confirm field, strength meter, list of rules or maxLength', async () => {
    await openViewF();

    const form = heading().closest('form');
    expect(form?.querySelectorAll('input[type="password"]')).toHaveLength(1);
    expect(form?.querySelectorAll('input:not([hidden])')).toHaveLength(1);
    expect(field()).not.toHaveAttribute('maxlength');
    const main = within(screen.getByRole('main'));
    expect(main.queryByRole('meter')).not.toBeInTheDocument();
    expect(main.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(main.queryByRole('list')).not.toBeInTheDocument();
  });

  it('holds the email in a hidden username field, which takes no focus', async () => {
    await openViewF();

    const username = usernameField();
    expect(username.form).toBe(field().form);
    expect(username).toHaveAttribute('type', 'email');
    expect(username).toHaveAttribute('autocomplete', 'username');
    expect(username).toHaveValue(account.email);
    expect(username).toHaveAttribute('readonly');
    expect(username).toHaveAttribute('hidden');
    expect(username).not.toBeVisible();
    expect(tabStops()).not.toContain(username);
  });

  it('sends nothing and fetches no creation options for a password and an authenticator app', async () => {
    await openViewF();
    // View E's own options, fetched as it showed, and none of view F's.
    expect(sent(optionsPath)).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Finish setup' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('main')).queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByTestId('setup-choice')).toHaveTextContent('authenticatorApp');
  });

  it('is reached by Tab in the order back link, field, Show, Continue, after the skip link', async () => {
    await openViewF();

    expect(tabStops()).toEqual([
      screen.getByRole('link', { name: 'Skip to main content' }),
      backLink(),
      field(),
      toggle(),
      continueButton(),
    ]);
  });

  describe('on Continue', () => {
    it.each([
      { value: '', kind: 'an empty field', message: enterPassword },
      { value: ' '.repeat(12), kind: 'twelve spaces', message: enterPassword },
      { value: '\t', kind: 'a tab', message: enterPassword },
      { value: ' '.repeat(12), kind: 'twelve no-break spaces', message: enterPassword },
      { value: 'abcdefghijk', kind: '11 characters', message: tooShort },
    ])('says "$message" for $kind, on the field, with focus there, and sends nothing', async ({ value, message }) => {
      const router = await openViewF();
      const requests = pageRequests().length;

      submit(value);
      await wait(0);

      expect(field()).toHaveFocus();
      expect(field()).toHaveAttribute('aria-invalid', 'true');
      expect(field()).toHaveAttribute('aria-describedby', 'password-hint password-error');
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${message}`);
      expectInOrder(screen.getByText(hint), screen.getByText(message), field());
      expect(field()).toHaveValue(value);
      expect(document.title).toBe(`Error: ${title}`);
      expect(router.state.location.pathname).toBe('/setup/password');
      expect(screen.getByTestId('setup-password')).toBeEmptyDOMElement();
      expect(pageRequests()).toHaveLength(requests);
    });

    it('moves focus to the field again on each press that finds the same message', async () => {
      await openViewF();
      submit('');
      await wait(0);
      expect(field()).toHaveFocus();

      submit('');
      await wait(0);

      expect(field()).toHaveFocus();
      expect(screen.getAllByText(enterPassword)).toHaveLength(1);
    });

    it.each([
      { value: 'abcdefghijkl', kind: '12 lowercase letters' },
      { value: 'éàüøçñåßîôûë', kind: '12 accented letters' },
      { value: ' abcdefghij ', kind: '12 characters with spaces round them, kept' },
      { value: 'abcdefghijk ', kind: '11 letters and a space' },
    ])('puts $kind in setup\'s memory as typed and pushes /setup/app, sending nothing', async ({ value }) => {
      const router = await openViewF();
      const requests = pageRequests();

      submit(value);
      await wait(0);

      expect(router.state.historyAction).toBe('PUSH');
      expect(router.state.location.pathname).toBe('/setup/app');
      expect(screen.getByTestId('setup-password').textContent).toBe(value);
      expect(screen.getByTestId('setup-account')).toHaveTextContent(account.email);
      expect(pageRequests()).toEqual(requests);
      expect(router.state.location.state).toBeNull();
      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
      expect(document.title).toBe(title);
    });

    it('checks again as the person types after a message, and takes it and the title\'s "Error: " away', async () => {
      await openViewF();
      submit('');
      await wait(0);

      fireEvent.change(field(), { target: { value: 'abc' } });
      expect(screen.getByText(tooShort)).toBeInTheDocument();
      expect(screen.queryByText(enterPassword)).not.toBeInTheDocument();
      expect(document.title).toBe(`Error: ${title}`);

      fireEvent.change(field(), { target: { value: 'abcdefghijkl' } });
      await wait(0);

      expect(screen.queryByText(tooShort)).not.toBeInTheDocument();
      expect(field()).not.toHaveAttribute('aria-invalid');
      expect(field()).toHaveAttribute('aria-describedby', 'password-hint');
      expect(document.title).toBe(title);
    });

    it('shows no message as the person types before the first press', async () => {
      await openViewF();

      fireEvent.change(field(), { target: { value: 'abc' } });
      await wait(0);

      expect(screen.queryByText(tooShort)).not.toBeInTheDocument();
      expect(field()).not.toHaveAttribute('aria-invalid');
    });
  });

  describe('Show and Hide', () => {
    it('reveal and hide the password, keep focus and say so in the polite line', async () => {
      await openViewF();
      fireEvent.change(field(), { target: { value: 'abcdefghijkl' } });
      const button = toggle();
      button.focus();

      fireEvent.click(button);

      expect(field()).toHaveAttribute('type', 'text');
      expect(button).toHaveAccessibleName('Hide password');
      expect(button).toHaveTextContent('Hide');
      expect(toggleLine()).toHaveTextContent('Your password is visible');
      expect(button).toHaveFocus();

      fireEvent.click(button);

      expect(field()).toHaveAttribute('type', 'password');
      expect(button).toHaveAccessibleName('Show password');
      expect(button).toHaveTextContent('Show');
      expect(toggleLine()).toHaveTextContent('Your password is hidden');
      expect(button).toHaveFocus();
      expect(field()).toHaveValue('abcdefghijkl');
    });

    it('hide the password again when Continue is pressed', async () => {
      await openViewF();
      fireEvent.click(toggle());
      expect(field()).toHaveAttribute('type', 'text');

      submit('abcdefghijk');
      await wait(0);

      expect(field()).toHaveAttribute('type', 'password');
      expect(toggle()).toHaveAccessibleName('Show password');
      expect(toggleLine()).toHaveTextContent('Your password is hidden');
      expect(field()).toHaveFocus();
    });
  });

  it('goes back one entry to view E on the back link, adding none, with the h1 focused', async () => {
    const router = await openViewF();
    submit('');
    await wait(0);

    fireEvent.click(backLink());
    await wait(0);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/setup');
    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Choose how you’ll sign in');
    expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
    expect(document.title).toBe(title);
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/setup/password');
  });

  it('starts with the password in memory when the person comes back from /setup/app', async () => {
    const router = await openViewF();
    submit(' abcdefghij ');
    await wait(0);
    expect(router.state.location.pathname).toBe('/setup/app');

    await act(() => router.navigate(-1));
    await wait(0);

    expect(router.state.location.pathname).toBe('/setup/password');
    expect(field()).toHaveValue(' abcdefghij ');
    expect(field()).toHaveAttribute('type', 'password');
    expect(heading()).toHaveFocus();
    expect(toggleLine()).toBeEmptyDOMElement();
  });

  it('shows the no-link message with nothing in memory, with no back link or form, and sends nothing', async () => {
    await open('/setup/password');
    await wait(5000);

    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent(
      'To set up your account, open the link from your email again.',
    );
    expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
    expect(screen.getByRole('main').querySelector('form, input')).toBeNull();
    expect(pageRequests()).toEqual([]);
    expect(document.title).toBe(title);
  });

  describe('for a password only', () => {
    it('reads Finish setup, with no hint, and fetches no creation options of its own', async () => {
      await openViewF(passwordOnlyLink);

      expect(screen.getByTestId('setup-choice')).toHaveTextContent('passwordOnly');
      expect(finishButton()).toHaveAttribute('type', 'submit');
      expect(finishButton()).toHaveClass(...buttonClass('primary').split(' '));
      expect(screen.queryByRole('button', { name: 'Continue' })).not.toBeInTheDocument();
      expect(screen.queryByText(/^When you select Finish setup/)).not.toBeInTheDocument();
      expect(alert()).toBeEmptyDOMElement();
      expectInOrder(heading(), field(), toggle(), usernameField(), alert(), finishButton());
      expect(sent(optionsPath)).toHaveLength(1);
      expect(document.title).toBe(title);
    });

    it('sends the password as typed and nothing else, then goes back one entry to Today, dropping setup', async () => {
      serve(passwordOnlyPath, () => {
        session = me;
        return json(me);
      });
      const router = await openViewF(passwordOnlyLink);

      finishWith(longPassword);
      await wait(0);

      expect(sent(passwordOnlyPath)).toEqual([{ password: longPassword }]);
      expect(pageRequests().filter((path) => path === passwordAndPasskeyPath)).toEqual([]);
      expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
      // F's entry went back to `/setup`'s, which the setup layout replaced with Today's.
      expect(router.state.historyAction).toBe('REPLACE');
      expect(router.state.location.pathname).toBe('/');
      expect(screen.queryByTestId('setup-account')).not.toBeInTheDocument();
      expect(heading()).toHaveAccessibleName(`Today, Monday 12${nbsp}October`);
      expect(router.state.location.state).toBeNull();
      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
    });

    it.each([
      { value: '', message: enterPassword },
      { value: 'abcdefghijk', message: tooShort },
    ])('sends nothing when the check finds "$message", and shows it on the field', async ({ value, message }) => {
      await openViewF(passwordOnlyLink);

      finishWith(value);
      await wait(0);

      expect(field()).toHaveFocus();
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${message}`);
      expect(sent(passwordOnlyPath)).toEqual([]);
    });

    it('shows the refusal of the password on the field, which takes focus, and keeps the password', async () => {
      serve(passwordOnlyPath, () => refusedField('password'));
      await openViewF(passwordOnlyLink);

      finishWith(longPassword);
      await wait(0);

      expect(field()).toHaveFocus();
      expect(field()).toHaveValue(longPassword);
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${tooShort}`);
      expect(alert()).toBeEmptyDOMElement();
      expect(document.title).toBe(`Error: ${title}`);
    });

    it.each([
      { failure: 'no connection', answer: noConnection, sentence: 'Not set up: no connection. Try again.' },
      { failure: 'a 503', answer: () => problem(503), sentence: 'Not set up: something went wrong. Try again.' },
      { failure: 'a 429', answer: () => problem(429), sentence: 'Too many tries. Wait 1 minute, then try again.' },
    ])('says so for $failure above Finish setup, where focus stays, with the password kept', async ({
      answer,
      sentence,
    }) => {
      serve(passwordOnlyPath, answer);
      const router = await openViewF(passwordOnlyLink);

      const button = finishWith(longPassword);
      await wait(0);

      expect(alert().textContent).toBe(sentence);
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(alert().compareDocumentPosition(button)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
      expect(field()).toHaveValue(longPassword);
      expect(document.title).toBe(`Error: ${title}`);
      expect(router.state.location.pathname).toBe('/setup/password');
      await wait(60_000);
      expect(sent(passwordOnlyPath)).toHaveLength(1);
    });

    it('shows the timed-out page for a 401, and drops the setup memory', async () => {
      serve(passwordOnlyPath, () => problem(401));
      await openViewF(passwordOnlyLink);

      finishWith(longPassword);
      await wait(0);

      expect(heading()).toHaveTextContent('Your setup session timed out');
      expect(heading()).toHaveFocus();
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(screen.getByTestId('setup-account')).toBeEmptyDOMElement();
      expect(screen.getByTestId('setup-choice')).toHaveTextContent('authenticatorApp');
    });

    it('sends nothing more for a second submit while the first is on its way', async () => {
      serve(passwordOnlyPath, noAnswer);
      await openViewF(passwordOnlyLink);

      const button = finishWith(longPassword);
      expect(button).toHaveAttribute('aria-disabled', 'true');
      fireEvent.click(button);
      await wait(0);

      expect(sent(passwordOnlyPath)).toHaveLength(1);
    });
  });

  describe('for a password and a passkey', () => {
    // View E's own options came first, so view F's are the second set served.
    const first = creationOptions('challenge-2');

    it('reads Finish setup under the hint that says what the device will ask for, described by it', async () => {
      await openViewF(passwordAndPasskeyLink);

      expect(screen.getByTestId('setup-choice')).toHaveTextContent('passwordAndPasskey');
      const note = screen.getByText(passkeyNext);
      expect(finishButton()).toHaveAccessibleDescription(passkeyNext);
      expect(screen.queryByRole('button', { name: 'Continue' })).not.toBeInTheDocument();
      expectInOrder(heading(), field(), toggle(), usernameField(), note, alert(), finishButton());
    });

    it('fetches creation options once as it shows, under StrictMode, and opens no prompt by itself', async () => {
      const router = await open(setupLink);
      const forViewE = sent(optionsPath).length;

      fireEvent.click(screen.getByRole('link', { name: passwordAndPasskeyLink }));
      await wait(10 * 60_000);

      expect(router.state.location.pathname).toBe('/setup/password');
      expect(sent(optionsPath)).toHaveLength(forViewE + 1);
      expect(webAuthn.startRegistration).not.toHaveBeenCalled();
      expect(sent(passwordAndPasskeyPath)).toEqual([]);
    });

    it('opens the prompt in the press itself, then sends the password with the credential, to Today', async () => {
      webAuthn.startRegistration.mockResolvedValueOnce(credential);
      serve(passwordAndPasskeyPath, () => {
        session = me;
        return json(me);
      });
      const router = await openViewF(passwordAndPasskeyLink);

      finishWith(longPassword);

      // Nothing was awaited between the press and here.
      expect(webAuthn.startRegistration).toHaveBeenCalledExactlyOnceWith({ optionsJSON: first });
      await wait(0);

      expect(sent(passwordAndPasskeyPath)).toEqual([
        { password: longPassword, credentialJson: JSON.stringify(credential) },
      ]);
      expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
      expect(router.state.historyAction).toBe('REPLACE');
      expect(router.state.location.pathname).toBe('/');
      expect(screen.queryByTestId('setup-account')).not.toBeInTheDocument();
      expect(heading()).toHaveAccessibleName(`Today, Monday 12${nbsp}October`);
    });

    it('fetches new options as the field changes once the held ones are 4 minutes old, for a prompt now', async () => {
      await openViewF(passwordAndPasskeyLink);
      await wait(4 * 60_000);
      const requests = sent(optionsPath).length;

      fireEvent.change(field(), { target: { value: longPassword } });
      await wait(0);
      expect(sent(optionsPath)).toHaveLength(requests + 1);
      // A change with options held asks for none.
      fireEvent.change(field(), { target: { value: `${longPassword}!` } });
      await wait(0);
      expect(sent(optionsPath)).toHaveLength(requests + 1);

      finishWith(longPassword);

      expect(webAuthn.startRegistration).toHaveBeenCalledExactlyOnceWith({
        optionsJSON: creationOptions('challenge-3'),
      });
    });

    it('fetches the options first when none are held at the press, then opens the prompt with them', async () => {
      await openViewF(passwordAndPasskeyLink);
      // Typed while the options were fresh, so that nothing fetched new ones as the time went by.
      fireEvent.change(field(), { target: { value: longPassword } });
      await wait(4 * 60_000);
      const requests = sent(optionsPath).length;

      const button = finishButton();
      button.focus();
      fireEvent.click(button);
      expect(webAuthn.startRegistration).not.toHaveBeenCalled();
      await wait(0);

      expect(button).toHaveFocus();
      expect(sent(optionsPath).length).toBeGreaterThan(requests);
      expect(webAuthn.startRegistration).toHaveBeenCalledOnce();
    });

    it('says the passkey was not set up for a prompt error, keeps the password and fetches new options', async () => {
      webAuthn.startRegistration.mockRejectedValueOnce(browserError('NotAllowedError'));
      await openViewF(passwordAndPasskeyLink);
      const requests = sent(optionsPath).length;

      const button = finishWith(longPassword);
      await wait(0);

      expect(alert().textContent).toBe(passkeyNotSetUp);
      expect(button).toHaveFocus();
      expect(field()).toHaveValue(longPassword);
      expect(sent(passwordAndPasskeyPath)).toEqual([]);
      expect(sent(optionsPath)).toHaveLength(requests + 1);
      expect(document.title).toBe(`Error: ${title}`);

      fireEvent.click(button);
      expect(webAuthn.startRegistration).toHaveBeenLastCalledWith({ optionsJSON: creationOptions('challenge-3') });
      expect(alert()).toBeEmptyDOMElement();
    });

    it('says the passkey was not set up when the server refuses the credential, and keeps the password', async () => {
      webAuthn.startRegistration.mockResolvedValueOnce(credential);
      serve(passwordAndPasskeyPath, () => refusedField('credentialJson'));
      await openViewF(passwordAndPasskeyLink);

      const button = finishWith(longPassword);
      await wait(0);

      expect(alert().textContent).toBe(passkeyNotSetUp);
      expect(button).toHaveFocus();
      expect(field()).toHaveValue(longPassword);
      expect(field()).not.toHaveAttribute('aria-invalid');
    });

    it('shows the refusal of the password on the field, not as a passkey that was not set up', async () => {
      webAuthn.startRegistration.mockResolvedValueOnce(credential);
      serve(passwordAndPasskeyPath, () => refusedField('password'));
      await openViewF(passwordAndPasskeyLink);

      finishWith(longPassword);
      await wait(0);

      expect(field()).toHaveFocus();
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${tooShort}`);
      expect(alert()).toBeEmptyDOMElement();
    });

    it.each([
      { failure: 'a 429', answer: () => problem(429), sentence: 'Too many tries. Wait 1 minute, then try again.' },
      { failure: 'a 503', answer: () => problem(503), sentence: 'Not set up: something went wrong. Try again.' },
    ])('says so for $failure above Finish setup', async ({ answer, sentence }) => {
      webAuthn.startRegistration.mockResolvedValueOnce(credential);
      serve(passwordAndPasskeyPath, answer);
      await openViewF(passwordAndPasskeyLink);

      finishWith(longPassword);
      await wait(0);

      expect(alert().textContent).toBe(sentence);
      expect(field()).toHaveValue(longPassword);
    });

    it('shows the timed-out page for a 401 from the creation options', async () => {
      const router = await open(setupLink);
      serve(optionsPath, () => problem(401));

      fireEvent.click(screen.getByRole('link', { name: passwordAndPasskeyLink }));
      await wait(0);

      expect(router.state.location.pathname).toBe('/setup/password');
      expect(heading()).toHaveTextContent('Your setup session timed out');
      expect(screen.getByTestId('setup-account')).toBeEmptyDOMElement();
    });

    it('never opens the prompt when the check finds the password wrong, and sends nothing', async () => {
      await openViewF(passwordAndPasskeyLink);

      finishWith('abcdefghijk');
      await wait(0);

      expect(webAuthn.startRegistration).not.toHaveBeenCalled();
      expect(field()).toHaveFocus();
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${tooShort}`);
      expect(sent(passwordAndPasskeyPath)).toEqual([]);
    });

    it('sends nothing more for a second submit while the first is on its way', async () => {
      webAuthn.startRegistration.mockResolvedValueOnce(credential);
      serve(passwordAndPasskeyPath, noAnswer);
      await openViewF(passwordAndPasskeyLink);

      const button = finishWith(longPassword);
      await wait(0);
      fireEvent.click(button);
      await wait(0);

      expect(webAuthn.startRegistration).toHaveBeenCalledOnce();
      expect(sent(passwordAndPasskeyPath)).toHaveLength(1);
    });

    it('cancels the prompt when view F goes', async () => {
      await openViewF(passwordAndPasskeyLink);
      const cancelled = webAuthn.cancelCeremony.mock.calls.length;

      fireEvent.click(backLink());
      await wait(0);

      expect(webAuthn.cancelCeremony.mock.calls.length).toBeGreaterThan(cancelled);
    });
  });
});
