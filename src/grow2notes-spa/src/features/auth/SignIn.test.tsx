import type * as SimpleWebAuthn from '@simplewebauthn/browser';
import {
  startAuthentication,
  WebAuthnAbortService,
  WebAuthnError,
  type AuthenticationResponseJSON,
  type PublicKeyCredentialRequestOptionsJSON,
} from '@simplewebauthn/browser';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes } from '../../routes/routes.tsx';

// The browser's passkey prompt, which jsdom has not got; the rest of SimpleWebAuthn is its own.
vi.mock('@simplewebauthn/browser', async (importOriginal) => ({
  ...(await importOriginal<typeof SimpleWebAuthn>()),
  startAuthentication: vi.fn(),
}));

const ceremony = vi.mocked(startAuthentication);

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const optionsPath = '/api/auth/passkey/options';
const passkeyPath = '/api/auth/passkey';
const passwordPath = '/api/auth/login';
const codePath = '/api/auth/login/totp';

// A made-up worker (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// The made-up worker's made-up sign-in details (A35).
const workerEmail = 'alex.pham@example.org';
const workerPassword = 'correct horse battery staple';

/** The options of the `n`th request for them, each with a challenge of its own, as Identity makes them. */
function passkeyOptions(n: number): PublicKeyCredentialRequestOptionsJSON {
  return { challenge: `Y2hhbGxlbmdl-${n}`, timeout: 300_000, rpId: 'localhost', userVerification: 'required' };
}

// What the browser's prompt gives for a made-up passkey, in SimpleWebAuthn's JSON.
const credential: AuthenticationResponseJSON = {
  id: 'Y3JlZGVudGlhbA',
  rawId: 'Y3JlZGVudGlhbA',
  response: {
    authenticatorData: 'YXV0aGVudGljYXRvcg',
    clientDataJSON: 'Y2xpZW50RGF0YQ',
    signature: 'c2lnbmF0dXJl',
    userHandle: 'dXNlcg',
  },
  type: 'public-key',
  clientExtensionResults: {},
  authenticatorAttachment: 'platform',
};

const hint = 'Sign in the way you chose when you set up your account.';
const help = 'If you cannot sign in, ask a manager to reset your sign-in.';
const notice =
  'Passkeys do not work in this browser. To use your passkey, open Grow2Notes in your phone’s main browser, such as '
  + 'Safari or Chrome.';
const failedMessage =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';
const enterEmail = 'Enter your email address';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const enterPassword = 'Enter your password';
const codeLabel = 'Enter the 6-digit code from your authenticator app';
const codeHint =
  'Open your authenticator app and find Grow2Notes. If the code changes while you’re typing, you can still use it.';
const codeFormat = 'The code must be 6 digits';

const fetchMock = vi.fn<typeof fetch>();

/** A JSON answer. */
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

/** The error that SimpleWebAuthn gives for a prompt that the person dismissed, or that found no passkey. */
function dismissal(): WebAuthnError {
  return new WebAuthnError({
    message: 'The operation either timed out or was not allowed.',
    code: 'ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY',
    cause: new DOMException('The operation either timed out or was not allowed.', 'NotAllowedError'),
  });
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that has not answered yet, and does not within the test. */
function noAnswer(): Promise<Response> {
  return new Promise(() => {});
}

type Server = {
  /** Each passkey sign-in's answer, a `200 Me` unless given. */
  passkey?: () => Promise<Response>;
  /** The answer to the `n`th request for passkey options, `passkeyOptions(n)` unless given. */
  options?: (n: number) => Promise<Response>;
  /** Each password step's answer, the `200 {"next":"totp"}` of a right password unless given. */
  password?: () => Promise<Response>;
  /** Each code step's answer, a `200 Me` unless given. */
  code?: () => Promise<Response>;
};

/** Stubs `fetch` as the server: nobody is signed in, the antiforgery endpoint gives a token, and the rest answer so. */
function serve({
  passkey = () => json(worker),
  options = (n) => json(passkeyOptions(n)),
  password = () => json({ next: 'totp' }),
  code = () => json(worker),
}: Server = {}) {
  let issued = 0;
  fetchMock.mockImplementation((path) => {
    switch (path) {
      case mePath:
        return problem(401);
      case antiforgeryPath:
        return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));
      case optionsPath:
        issued += 1;
        return options(issued);
      case passkeyPath:
        return passkey();
      case passwordPath:
        return password();
      case codePath:
        return code();
      default:
        throw new Error('The test gave no answer for this request.');
    }
  });
}

/** The requests sent to `path`. */
function requestsTo(path: string) {
  return fetchMock.mock.calls.filter(([sent]) => sent === path).map(([, init]) => init);
}

/** A browser with WebAuthn, which jsdom is not: `browserSupportsWebAuthn()` asks only for the constructor. */
function withWebAuthn() {
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/**
 * Renders the app's routes at `path` on a memory router, with nobody signed in, under a query client of its own, and
 * lets view A show.
 */
async function renderSignedOut(path = '/', strict = false) {
  const queryClient = createQueryClient();
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const app = (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
  const { unmount } = render(strict ? <StrictMode>{app}</StrictMode> : app);
  await wait(0);
  return { queryClient, router, unmount };
}

/** The passkey button, by its name before any press, as its busy label renames it. */
function passkeyButton() {
  return screen.getByRole('button', { name: 'Sign in with a passkey' });
}

/** Continue, by its name before any press, as its busy label renames it. */
function continueButton() {
  return screen.getByRole<HTMLButtonElement>('button', { name: 'Continue' });
}

function emailField(): HTMLInputElement {
  return screen.getByRole<HTMLInputElement>('textbox', { name: 'Email address' });
}

function passwordField(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Password');
}

/** Presses `button` as a keyboard or a pointer does, with focus on it first. */
function press(button: HTMLElement) {
  button.focus();
  fireEvent.click(button);
}

/** Puts `value` in `field`, as typing or pasting it does. */
function type(field: HTMLInputElement, value: string) {
  fireEvent.change(field, { target: { value } });
}

/** Types the made-up worker's email address and password into the form. */
function fillIn() {
  type(emailField(), workerEmail);
  type(passwordField(), workerPassword);
}

function codeField(): HTMLInputElement {
  return screen.getByRole<HTMLInputElement>('textbox', { name: codeLabel });
}

/** The code step's Sign in, by its name before any press, as its busy label renames it. */
function signInButton() {
  return screen.getByRole<HTMLButtonElement>('button', { name: 'Sign in' });
}

/** Renders view A at `path`, sends the made-up worker's password, and lets the code step show in its place. */
async function renderCodeStep(path = '/') {
  const rendered = await renderSignedOut(path);
  fillIn();
  press(continueButton());
  await wait(0);
  return rendered;
}

/**
 * Enter in `field`, as a browser takes it and jsdom does not: implicit submission, which presses the form's default
 * button, its first submit button, with focus left in the field (HTML's *Implicit submission*).
 */
function pressEnter(field: HTMLInputElement) {
  const button = field.form?.querySelector('button[type="submit"]');
  if (!(button instanceof HTMLButtonElement)) {
    throw new Error('The field is in no form with a submit button.');
  }

  field.focus();
  fireEvent.keyDown(field, { key: 'Enter' });
  fireEvent.click(button);
}

/** The error summary, which holds its heading, or null while it does not show. */
function summary() {
  return screen.queryByRole('heading', { level: 2, name: 'There is a problem' })?.parentElement ?? null;
}

/** The summary's items, each as its words, and its link's target where it has one. */
function summaryItems() {
  const shown = summary();
  if (shown === null) {
    return [];
  }

  return within(shown)
    .getAllByRole('listitem')
    .map((item) => {
      const link = item.querySelector('a');
      return link === null ? { text: item.textContent } : { text: link.textContent, href: link.getAttribute('href') };
    });
}

/** The alert line directly above `button`, where its request's failures show (sign-in.md component 2a). */
function alertAbove(button: HTMLElement) {
  const alert = button.previousElementSibling;
  if (!(alert instanceof HTMLElement) || alert.getAttribute('role') !== 'alert') {
    throw new Error('No alert line comes directly before the button.');
  }

  return alert;
}

/** The elements in `container` that Tab reaches, in order: focusable, not hidden and not taken out of the order. */
function tabStops(container: HTMLElement): Element[] {
  const focusable = container.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]');
  return Array.from(focusable).filter((element) => element.closest('[hidden]') === null && element.tabIndex >= 0);
}

/** Fails unless the elements come in the document in the order given. */
function expectInOrder(...elements: Element[]) {
  for (const [index, element] of elements.slice(1).entries()) {
    expect(elements[index].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

/** An answer still on its way, which the test gives when it chooses. */
function later() {
  let resolve!: (answer: Response) => void;
  const promise = new Promise<Response>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

/**
 * A passkey prompt as SimpleWebAuthn's own opens one, under `WebAuthnAbortService`'s signal, which ends only when the
 * view cancels it, with the error SimpleWebAuthn gives for that.
 */
function promptUntilCancelled() {
  const signals: AbortSignal[] = [];
  ceremony.mockImplementation(
    () =>
      new Promise((_resolve, reject) => {
        const signal = WebAuthnAbortService.createNewAbortSignal();
        signals.push(signal);
        signal.addEventListener('abort', () => {
          reject(
            new WebAuthnError({
              message: 'Authentication ceremony was sent an abort signal',
              code: 'ERROR_CEREMONY_ABORTED',
              cause: signal.reason as Error,
            }),
          );
        });
      }),
  );
  return signals;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  withWebAuthn();
  ceremony.mockResolvedValue(credential);
  // index.html's, before the app sets any.
  document.title = 'Grow2Notes';
});

afterEach(() => {
  vi.restoreAllMocks();
  fetchMock.mockReset();
  ceremony.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('signing in with a passkey', () => {
  describe('the options', () => {
    it('are asked for once as view A renders, and open no prompt', async () => {
      serve();
      await renderSignedOut();

      expect(requestsTo(optionsPath)).toEqual([expect.objectContaining({ method: 'POST' })]);
      expect(ceremony).not.toHaveBeenCalled();
    });

    it('are asked for once under StrictMode too, whose second run of the effects joins the first request', async () => {
      serve();
      await renderSignedOut('/', true);

      expect(requestsTo(optionsPath)).toHaveLength(1);
    });

    it('are not asked for in a browser with no WebAuthn', async () => {
      vi.stubGlobal('PublicKeyCredential', undefined);
      serve();
      await renderSignedOut();

      expect(screen.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
      expect(requestsTo(optionsPath)).toHaveLength(0);
    });

    it('open the prompt in the press itself, before any await, while they are 3 min 59 s old', async () => {
      serve();
      await renderSignedOut();
      await wait(239_000);

      press(passkeyButton());

      // Nothing awaited since the press: the prompt opened in it, with the options fetched as the view rendered.
      expect(ceremony).toHaveBeenCalledExactlyOnceWith({ optionsJSON: passkeyOptions(1) });
      expect(requestsTo(optionsPath)).toHaveLength(1);
    });

    it('are fetched again at 4 minutes old, before the prompt opens with the new ones', async () => {
      serve();
      await renderSignedOut();
      await wait(240_000);

      press(passkeyButton());

      expect(ceremony).not.toHaveBeenCalled();

      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(2);
      expect(ceremony).toHaveBeenCalledExactlyOnceWith({ optionsJSON: passkeyOptions(2) });
    });
  });

  it('sends the credential as JSON.stringify writes it, with the antiforgery token', async () => {
    serve();
    await renderSignedOut();

    press(passkeyButton());
    await wait(0);

    const [init] = requestsTo(passkeyPath);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ credentialJson: JSON.stringify(credential) });
    const headers = new Headers(init?.headers);
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-XSRF-TOKEN')).toBe('token-1');
  });

  it('shows the screen in its place after a 200 Me, at the same address, its heading focused', async () => {
    serve();
    const { queryClient, router } = await renderSignedOut('/manage/anything');
    const { location } = router.state;

    press(passkeyButton());
    await wait(0);

    expect(queryClient.getQueryData(['me'])).toEqual(worker);
    // The screen at that address, which no route matches.
    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toHaveFocus();
    expect(screen.queryByRole('heading', { name: 'Sign in' })).not.toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(router.state.location).toBe(location);
    // The app took the sign-in's answer, with no second request.
    expect(requestsTo(mePath)).toHaveLength(1);
  });

  describe.each([401, 429])('given a %i from the sign-in', (status) => {
    it('shows the A25 message as the summary\'s one item, unlinked, which takes focus', async () => {
      serve({ passkey: () => problem(status) });
      await renderSignedOut();
      const button = passkeyButton();

      press(button);
      await wait(0);

      expect(summary()).toHaveFocus();
      // The summary's list, the only one in view A.
      const list = within(screen.getByRole('list'));
      expect(list.getByRole('listitem')).toHaveTextContent(failedMessage);
      expect(list.queryByRole('link')).not.toBeInTheDocument();
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
      expect(alertAbove(button)).toBeEmptyDOMElement();
      expect(button).not.toHaveAttribute('aria-disabled');
      // New options, as the server has ended the ceremony of the old.
      expect(requestsTo(optionsPath)).toHaveLength(2);
    });
  });

  it('empties the summary on the next press, and focuses it again when that sign-in is refused too', async () => {
    serve({ passkey: () => problem(401) });
    await renderSignedOut();
    const button = passkeyButton();
    press(button);
    await wait(0);

    press(button);

    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Sign in');

    await wait(0);

    expect(summary()).toHaveFocus();
    expect(document.title).toBe('Error: Grow2Notes – Sign in');
    expect(ceremony).toHaveBeenLastCalledWith({ optionsJSON: passkeyOptions(2) });
  });

  it('says nothing when the prompt is dismissed, and leaves focus on the button', async () => {
    ceremony.mockRejectedValue(dismissal());
    serve();
    await renderSignedOut();
    const button = passkeyButton();

    press(button);
    await wait(0);

    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(summary()).toBeNull();
    expect(alertAbove(button)).toBeEmptyDOMElement();
    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(requestsTo(passkeyPath)).toHaveLength(0);
    expect(requestsTo(optionsPath)).toHaveLength(2);
  });

  describe.each([
    {
      failure: 'another error from the prompt',
      prompt: () =>
        Promise.reject(
          new WebAuthnError({
            message: 'The RP ID "localhost" is invalid for this domain',
            code: 'ERROR_INVALID_RP_ID',
            cause: new DOMException('The operation is insecure.', 'SecurityError'),
          }),
        ),
      passkey: () => json(worker),
      message: 'Not signed in: something went wrong. Try again.',
    },
    {
      failure: 'no connection',
      prompt: () => Promise.resolve(credential),
      passkey: noConnection,
      message: 'Not signed in: no connection. Try again.',
    },
    {
      failure: 'a 503',
      prompt: () => Promise.resolve(credential),
      passkey: () => problem(503),
      message: 'Not signed in: something went wrong. Try again.',
    },
  ])('given $failure', ({ prompt, passkey, message }) => {
    it('says so in the alert line above the button, where focus stays, with no summary', async () => {
      ceremony.mockImplementation(prompt);
      serve({ passkey });
      await renderSignedOut();
      const button = passkeyButton();

      press(button);
      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
      expect(alertAbove(continueButton())).toBeEmptyDOMElement();
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(requestsTo(optionsPath)).toHaveLength(2);
    });

    it('empties the alert line on the next press, so that the same failure is announced again', async () => {
      ceremony.mockImplementation(prompt);
      serve({ passkey });
      await renderSignedOut();
      const button = passkeyButton();
      press(button);
      await wait(0);

      press(button);

      expect(alertAbove(button)).toBeEmptyDOMElement();

      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
    });
  });

  // Each request for options ends the ceremony of those before it, so a prompt opened with the old ones while new ones
  // are on their way would sign a challenge the server no longer holds, and the sign-in would be refused.
  describe.each([
    { failure: 'a dismissed prompt', prompt: () => Promise.reject(dismissal()), passkey: () => json(worker) },
    { failure: 'a refused sign-in', prompt: () => Promise.resolve(credential), passkey: () => problem(401) },
  ])('after $failure', ({ prompt, passkey }) => {
    it('a press while new options are on their way waits for them, and never uses the old', async () => {
      ceremony.mockImplementationOnce(prompt);
      const refetch = later();
      serve({ passkey, options: (n) => (n === 2 ? refetch.promise : json(passkeyOptions(n))) });
      await renderSignedOut();
      const button = passkeyButton();
      press(button);
      await wait(0);
      expect(requestsTo(optionsPath)).toHaveLength(2);

      press(button);
      await wait(0);

      // The press joined the request on its way, and opened no prompt.
      expect(requestsTo(optionsPath)).toHaveLength(2);
      expect(ceremony).toHaveBeenCalledOnce();

      refetch.resolve(await json(passkeyOptions(2)));
      await wait(0);

      expect(ceremony.mock.calls).toEqual([[{ optionsJSON: passkeyOptions(1) }], [{ optionsJSON: passkeyOptions(2) }]]);
    });
  });

  it('says there is no connection when new options cannot be fetched for a press', async () => {
    serve();
    await renderSignedOut();
    await wait(240_000);
    fetchMock.mockImplementation(noConnection);
    const button = passkeyButton();

    press(button);
    await wait(0);

    expect(alertAbove(button)).toHaveTextContent('Not signed in: no connection. Try again.');
    expect(button).toHaveFocus();
    expect(ceremony).not.toHaveBeenCalled();
  });

  it('sends nothing for a second press while busy, and keeps focus on the button', async () => {
    ceremony.mockReturnValue(new Promise(() => {}));
    serve();
    await renderSignedOut();
    const button = passkeyButton();

    press(button);
    press(button);
    await wait(0);

    expect(ceremony).toHaveBeenCalledOnce();
    expect(requestsTo(optionsPath)).toHaveLength(1);
    expect(requestsTo(passkeyPath)).toHaveLength(0);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('disabled');
    expect(button).toHaveFocus();
  });

  it('shows the busy label "Signing in…" at 400 ms, not at 399 ms, and writes it to the status region', async () => {
    ceremony.mockReturnValue(new Promise(() => {}));
    serve();
    await renderSignedOut();
    const button = passkeyButton();

    press(button);
    await wait(399);

    expect(button).toHaveAccessibleName('Sign in with a passkey');
    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    await wait(1);
    act(() => {
      vi.advanceTimersToNextFrame();
    });

    expect(button).toHaveAccessibleName('Signing in…');
    expect(screen.getByRole('status')).toHaveTextContent('Signing in…');
  });

  describe('when the view goes', () => {
    it('cancels the prompt, and says and fetches nothing after it', async () => {
      const signals = promptUntilCancelled();
      const consoleError = vi.spyOn(console, 'error');
      serve();
      const { unmount } = await renderSignedOut();
      press(passkeyButton());
      const sent = fetchMock.mock.calls.length;

      unmount();
      await wait(0);

      expect(signals).toHaveLength(1);
      expect(signals[0].aborted).toBe(true);
      expect(fetchMock.mock.calls.length).toBe(sent);
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(consoleError).not.toHaveBeenCalled();
    });

    it('opens no prompt with the options that come after it', async () => {
      serve();
      const { unmount } = await renderSignedOut();
      await wait(240_000);
      press(passkeyButton());

      unmount();
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(2);
      expect(ceremony).not.toHaveBeenCalled();
    });

    it('says and fetches nothing for a sign-in refused after it', async () => {
      const answer = later();
      serve({ passkey: () => answer.promise });
      const { unmount } = await renderSignedOut();
      press(passkeyButton());
      await wait(0);

      unmount();
      answer.resolve(await problem(401));
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(1);
      expect(document.title).toBe('Grow2Notes – Sign in');
    });
  });

  it('leaves nothing in localStorage, sessionStorage or the query client but the me query', async () => {
    serve({
      passkey: vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => problem(401))
        .mockImplementationOnce(() => json(worker)),
    });
    const { queryClient } = await renderSignedOut();
    const button = passkeyButton();

    press(button);
    await wait(0);
    press(button);
    await wait(0);

    expect(queryClient.getQueryData(['me'])).toEqual(worker);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
    expect(queryClient.getMutationCache().getAll()).toEqual([]);
    expect(queryClient.getQueryCache().getAll().map(({ queryKey }) => queryKey)).toEqual([['me']]);
  });
});

describe('signing in with an email address and password', () => {
  describe('the form', () => {
    it('follows the passkey button and "or", with the help line after it', async () => {
      serve();
      await renderSignedOut();
      const main = within(screen.getByRole('main'));
      const passkey = passkeyButton();
      const form = continueButton().closest('form');

      expectInOrder(
        main.getByRole('heading', { level: 1, name: 'Sign in' }),
        main.getByText(hint),
        alertAbove(passkey),
        passkey,
        main.getByText('or', { selector: 'p' }),
        emailField(),
        passwordField(),
        alertAbove(continueButton()),
        continueButton(),
        main.getByText(help),
      );
      expect(form).toHaveAttribute('novalidate');
      expect(form).not.toHaveAttribute('action');
      expect(emailField().form).toBe(form);
      expect(passwordField().form).toBe(form);
      expect(alertAbove(continueButton()).closest('form')).toBe(form);
      // Outside the form, so that Enter in a field never opens the passkey prompt.
      expect(passkey).toHaveAttribute('type', 'button');
      expect(passkey.closest('form')).toBeNull();
      expect(continueButton()).toHaveAttribute('type', 'submit');
      expect(continueButton().form).toBe(form);
      expect(main.getByText(help, { selector: 'p' }).closest('form')).toBeNull();
    });

    it('has an email field that a password manager fills, with none of the phone\'s corrections', async () => {
      serve();
      await renderSignedOut();
      const field = emailField();

      expect(field).toHaveAccessibleName('Email address');
      expect(field).toHaveAttribute('id', 'email');
      expect(field).toHaveAttribute('name', 'email');
      expect(field).toHaveAttribute('type', 'email');
      expect(field).toHaveAttribute('autocomplete', 'username');
      expect(field).toHaveAttribute('spellcheck', 'false');
      expect(field).toHaveAttribute('autocapitalize', 'none');
      expect(field).toHaveAttribute('autocorrect', 'off');
      expect(field).not.toHaveAttribute('placeholder');
      expect(field).not.toHaveAttribute('autofocus');
      expect(field).not.toHaveAttribute('required');
      expect(field).not.toHaveAttribute('aria-invalid');
      expect(field).toHaveValue('');
    });

    it('has a password field with no limit on its length and no Show/Hide', async () => {
      serve();
      await renderSignedOut();
      const field = passwordField();

      expect(field).toHaveAccessibleName('Password');
      expect(field).toHaveAttribute('id', 'password');
      expect(field).toHaveAttribute('name', 'password');
      expect(field).toHaveAttribute('type', 'password');
      expect(field).toHaveAttribute('autocomplete', 'current-password');
      expect(field).toHaveAttribute('spellcheck', 'false');
      expect(field).toHaveAttribute('autocapitalize', 'none');
      expect(field).toHaveAttribute('autocorrect', 'off');
      expect(field).not.toHaveAttribute('maxlength');
      expect(field).not.toHaveAttribute('placeholder');
      expect(field).not.toHaveAttribute('required');
      expect(field).not.toHaveAttribute('aria-invalid');
      expect(field).toHaveValue('');
    });

    it('takes Tab from the passkey button to the email, the password and Continue, and nothing else', async () => {
      serve();
      await renderSignedOut();

      expect(tabStops(screen.getByRole('main'))).toEqual([
        passkeyButton(),
        emailField(),
        passwordField(),
        continueButton(),
      ]);
      // Nothing has focus as the view shows (sign-in.md *Where focus goes*).
      expect(document.body).toHaveFocus();
    });

    it('has no Show/Hide, "Forgot password", "Remember me" or "remember this device" (A22, D68)', async () => {
      serve();
      await renderSignedOut();
      const main = within(screen.getByRole('main'));

      expect(main.getAllByRole('button')).toEqual([passkeyButton(), continueButton()]);
      expect(main.queryByRole('checkbox')).not.toBeInTheDocument();
      expect(main.queryByRole('link')).not.toBeInTheDocument();
      expect(main.queryByText(/show|hide|forgot|remember/i)).not.toBeInTheDocument();
    });

    it('has only the passkey button in the primary style, and Continue in the secondary', async () => {
      serve();
      await renderSignedOut();

      expect(passkeyButton()).toHaveAttribute('class', buttonClass('primary'));
      expect(continueButton()).toHaveAttribute('class', buttonClass('secondary'));
    });

    it('has no "or" in a browser with no WebAuthn, where the notice comes before it, and it still sends', async () => {
      vi.stubGlobal('PublicKeyCredential', undefined);
      serve({ password: () => problem(401) });
      await renderSignedOut();
      const main = within(screen.getByRole('main'));

      expect(main.queryByText('or')).not.toBeInTheDocument();
      expectInOrder(main.getByText(hint), main.getByText(notice), emailField(), continueButton(), main.getByText(help));
      expect(main.getAllByRole('alert')).toEqual([alertAbove(continueButton())]);

      fillIn();
      press(continueButton());
      await wait(0);

      expect(requestsTo(passwordPath)).toHaveLength(1);
      expect(requestsTo(optionsPath)).toHaveLength(0);
      expect(summary()).toHaveFocus();
    });
  });

  it('sends the form for Enter in the password field, and never opens the passkey prompt', async () => {
    serve();
    await renderSignedOut();
    fillIn();

    pressEnter(passwordField());
    await wait(0);

    expect(requestsTo(passwordPath)).toHaveLength(1);
    expect(ceremony).not.toHaveBeenCalled();
    expect(requestsTo(passkeyPath)).toHaveLength(0);
  });

  it('sends the email trimmed and the password as typed, by itself, with the antiforgery token', async () => {
    serve();
    await renderSignedOut();
    // A browser takes the spaces off an email field's value itself, but not a no-break space, as an address copied
    // from a web page may hold.
    type(emailField(), `${String.fromCodePoint(0xa0)}${workerEmail} `);
    type(passwordField(), ` ${workerPassword} `);

    press(continueButton());
    await wait(0);

    const [init] = requestsTo(passwordPath);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ email: workerEmail, password: ` ${workerPassword} ` });
    const headers = new Headers(init?.headers);
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-XSRF-TOKEN')).toBe('token-1');
  });

  describe('checked before anything is sent', () => {
    // jsdom has no scrollIntoView, which a summary link calls before it focuses its field.
    beforeEach(() => {
      Element.prototype.scrollIntoView = vi.fn<(options?: boolean | ScrollIntoViewOptions) => void>();
    });

    afterEach(() => {
      Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
    });

    describe.each([
      { given: 'both fields empty', email: '', password: '', errors: { email: enterEmail, password: enterPassword } },
      {
        given: 'only whitespace in both fields',
        email: ' \t ',
        password: '   ',
        errors: { email: enterEmail, password: enterPassword },
      },
      { given: 'an email with no "@"', email: 'alex.pham', password: workerPassword, errors: { email: emailFormat } },
      {
        given: 'an email with nothing before its "@"',
        email: '@example.org',
        password: workerPassword,
        errors: { email: emailFormat },
      },
      {
        given: 'an email with nothing after its "@"',
        email: 'alex@',
        password: '',
        errors: { email: emailFormat, password: enterPassword },
      },
      {
        given: 'an email with two "@"',
        email: 'alex@pham@example.org',
        password: workerPassword,
        errors: { email: emailFormat },
      },
      { given: 'no password', email: workerEmail, password: '', errors: { password: enterPassword } },
    ])('given $given', ({ email, password, errors }) => {
      it('shows each message on its field and linked in the summary, which takes focus, sending nothing', async () => {
        serve();
        await renderSignedOut();
        type(emailField(), email);
        type(passwordField(), password);

        press(continueButton());
        await wait(0);

        expect(summary()).toHaveFocus();
        expect(summaryItems()).toEqual([
          ...(errors.email === undefined ? [] : [{ text: errors.email, href: '#email' }]),
          ...(errors.password === undefined ? [] : [{ text: errors.password, href: '#password' }]),
        ]);
        for (const [field, message] of [
          [emailField(), errors.email],
          [passwordField(), errors.password],
        ] as const) {
          if (message === undefined) {
            expect(field).not.toHaveAttribute('aria-invalid');
            expect(field).not.toHaveAccessibleDescription();
          } else {
            expect(field).toHaveAttribute('aria-invalid', 'true');
            expect(field).toHaveAccessibleDescription(`Error: ${message}`);
          }
        }
        expect(document.title).toBe('Error: Grow2Notes – Sign in');
        // Nothing sent, the token's request included, and what was typed kept, the email as the browser keeps an
        // email field's value, with no whitespace at either end.
        expect(requestsTo(passwordPath)).toHaveLength(0);
        expect(requestsTo(antiforgeryPath)).toHaveLength(0);
        expect(emailField()).toHaveValue(email.trim());
        expect(passwordField()).toHaveValue(password);
        expect(alertAbove(continueButton())).toBeEmptyDOMElement();
        expect(continueButton()).not.toHaveAttribute('aria-disabled');
      });
    });

    it('focuses each field from its link in the summary', async () => {
      serve();
      await renderSignedOut();
      press(continueButton());
      await wait(0);

      fireEvent.click(screen.getByRole('link', { name: enterPassword }));

      expect(passwordField()).toHaveFocus();

      fireEvent.click(screen.getByRole('link', { name: enterEmail }));

      expect(emailField()).toHaveFocus();
    });

    it('focuses the summary again on each press that finds the same messages', async () => {
      serve();
      await renderSignedOut();
      press(continueButton());
      await wait(0);

      // From Continue, where the press puts focus first.
      press(continueButton());
      await wait(0);

      expect(summary()).toHaveFocus();
    });

    it('takes each message, link and aria-invalid away as its field is fixed, leaving focus in the field', async () => {
      serve();
      await renderSignedOut();
      press(continueButton());
      await wait(0);

      emailField().focus();
      type(emailField(), workerEmail);

      expect(emailField()).not.toHaveAttribute('aria-invalid');
      expect(emailField()).not.toHaveAccessibleDescription();
      expect(screen.queryByText(enterEmail)).not.toBeInTheDocument();
      expect(summaryItems()).toEqual([{ text: enterPassword, href: '#password' }]);
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
      expect(emailField()).toHaveFocus();

      passwordField().focus();
      type(passwordField(), workerPassword);

      // The summary and the title's "Error: " go with the last message.
      expect(passwordField()).not.toHaveAttribute('aria-invalid');
      expect(passwordField()).not.toHaveAccessibleDescription();
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(passwordField()).toHaveFocus();
      expect(requestsTo(passwordPath)).toHaveLength(0);
    });

    it('checks a field in error again as it changes, saying what is wrong with it now, focus unmoved', async () => {
      serve();
      await renderSignedOut();
      press(continueButton());
      await wait(0);
      emailField().focus();

      type(emailField(), 'alex.pham');

      expect(emailField()).toHaveAccessibleDescription(`Error: ${emailFormat}`);
      expect(summaryItems()[0]).toEqual({ text: emailFormat, href: '#email' });
      expect(emailField()).toHaveFocus();

      // A password of only whitespace is still empty.
      type(passwordField(), '  ');

      expect(passwordField()).toHaveAccessibleDescription(`Error: ${enterPassword}`);
    });

    it('puts no message on a field that had none while it changes, until the next press', async () => {
      serve();
      await renderSignedOut();
      type(emailField(), workerEmail);
      press(continueButton());
      await wait(0);

      type(emailField(), 'alex.pham');

      expect(emailField()).not.toHaveAttribute('aria-invalid');
      expect(summaryItems()).toEqual([{ text: enterPassword, href: '#password' }]);
    });
  });

  // The password step's answer, which the next step takes; a right password leaves view A as it is until then.
  describe.each([401, 429])('given a %i from the password step', (status) => {
    it('shows the A25 message in the summary, which takes focus, and empties the password, not the email', async () => {
      serve({ password: () => problem(status) });
      await renderSignedOut();
      fillIn();
      const button = continueButton();

      press(button);
      await wait(0);

      expect(summary()).toHaveFocus();
      expect(summaryItems()).toEqual([{ text: failedMessage }]);
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
      expect(emailField()).toHaveValue(workerEmail);
      expect(passwordField()).toHaveValue('');
      // It marks no field, which would say which part was wrong (A25).
      expect(emailField()).not.toHaveAttribute('aria-invalid');
      expect(passwordField()).not.toHaveAttribute('aria-invalid');
      expect(alertAbove(button)).toBeEmptyDOMElement();
      expect(button).not.toHaveAttribute('aria-disabled');
    });
  });

  it('empties the summary on the next press, and focuses it again when that password is refused too', async () => {
    serve({ password: () => problem(401) });
    await renderSignedOut();
    fillIn();
    press(continueButton());
    await wait(0);

    type(passwordField(), workerPassword);
    press(continueButton());

    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Sign in');

    await wait(0);

    expect(summary()).toHaveFocus();
    expect(requestsTo(passwordPath)).toHaveLength(2);
  });

  it('lists a refused passkey\'s message above the form\'s, and Continue then shows only its own', async () => {
    serve({ passkey: () => problem(401) });
    await renderSignedOut();
    press(continueButton());
    await wait(0);

    press(passkeyButton());
    await wait(0);

    expect(summary()).toHaveFocus();
    expect(summaryItems()).toEqual([
      { text: failedMessage },
      { text: enterEmail, href: '#email' },
      { text: enterPassword, href: '#password' },
    ]);

    press(continueButton());
    await wait(0);

    expect(summaryItems()).toEqual([
      { text: enterEmail, href: '#email' },
      { text: enterPassword, href: '#password' },
    ]);
  });

  describe.each([
    { failure: 'no connection', answer: noConnection, message: 'Not signed in: no connection. Try again.' },
    { failure: 'a 503', answer: () => problem(503), message: 'Not signed in: something went wrong. Try again.' },
  ])('given $failure', ({ answer, message }) => {
    it('says so in the alert line above Continue, where focus stays, keeping both fields, no summary', async () => {
      serve({ password: answer });
      await renderSignedOut();
      fillIn();
      const button = continueButton();

      press(button);
      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
      expect(alertAbove(passkeyButton())).toBeEmptyDOMElement();
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(emailField()).toHaveValue(workerEmail);
      expect(passwordField()).toHaveValue(workerPassword);
    });

    it('empties the alert line on the next press, so that the same failure is announced again', async () => {
      serve({ password: answer });
      await renderSignedOut();
      fillIn();
      const button = continueButton();
      press(button);
      await wait(0);

      press(button);

      expect(alertAbove(button)).toBeEmptyDOMElement();

      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
    });
  });

  // Identity keeps a passkey ceremony and the code step in one cookie, so the options' answer must not come after the
  // password's (S00.04.05's Notes).
  describe.each([
    { ending: 'with the options', answer: () => json(passkeyOptions(1)) },
    { ending: 'with a 503', answer: () => problem(503) },
  ])('while a request for passkey options is on its way, ending $ending', ({ answer }) => {
    it('sends the password only once that request has ended', async () => {
      const pending = later();
      serve({ options: () => pending.promise });
      await renderSignedOut();
      fillIn();
      const button = continueButton();

      press(button);
      await wait(0);

      expect(requestsTo(passwordPath)).toHaveLength(0);
      expect(button).toHaveAttribute('aria-disabled', 'true');

      pending.resolve(await answer());
      await wait(0);

      expect(requestsTo(passwordPath)).toHaveLength(1);
      expect(requestsTo(optionsPath)).toHaveLength(1);
    });
  });

  // Nor may a request for options start while the password is out, as its answer could then come after the password's.
  describe.each([
    { answer: 'refused', password: () => problem(401), options: 2 },
    { answer: 'accepted', password: () => json({ next: 'totp' }), options: 1 },
  ])('while the password is out, and once it is $answer', ({ password, options }) => {
    it('a passkey press with no fresh options asks for them only once the password\'s answer leaves A', async () => {
      const answer = later();
      serve({ password: () => answer.promise });
      await renderSignedOut();
      // Options this old are not used, so the press asks for new ones.
      await wait(240_000);
      fillIn();
      press(continueButton());
      await wait(0);

      press(passkeyButton());
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(1);
      expect(ceremony).not.toHaveBeenCalled();

      answer.resolve(await password());
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(options);
      expect(ceremony.mock.calls).toEqual(options === 2 ? [[{ optionsJSON: passkeyOptions(2) }]] : []);
    });

    it('a prompt that fails asks for new options only once the password\'s answer leaves A', async () => {
      let dismiss!: (error: unknown) => void;
      ceremony.mockReturnValue(
        new Promise((_resolve, reject) => {
          dismiss = reject;
        }),
      );
      const answer = later();
      serve({ password: () => answer.promise });
      await renderSignedOut();
      press(passkeyButton());
      fillIn();
      press(continueButton());
      await wait(0);
      expect(requestsTo(passwordPath)).toHaveLength(1);

      dismiss(dismissal());
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(1);

      answer.resolve(await password());
      await wait(0);

      expect(requestsTo(optionsPath)).toHaveLength(options);
    });
  });

  it('sends nothing for a second press or Enter while busy, and keeps focus on Continue, never disabled', async () => {
    serve({ password: noAnswer });
    await renderSignedOut();
    fillIn();
    const button = continueButton();

    press(button);
    press(button);
    await wait(0);

    expect(requestsTo(passwordPath)).toHaveLength(1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('disabled');
    expect(button).toHaveFocus();

    pressEnter(passwordField());
    await wait(0);

    expect(requestsTo(passwordPath)).toHaveLength(1);
  });

  it('shows the busy label "Checking…" at 400 ms, not at 399 ms, and writes it to the status region', async () => {
    serve({ password: noAnswer });
    await renderSignedOut();
    fillIn();
    const button = continueButton();

    press(button);
    await wait(399);

    expect(button).toHaveAccessibleName('Continue');
    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    await wait(1);
    act(() => {
      vi.advanceTimersToNextFrame();
    });

    expect(button).toHaveAccessibleName('Checking…');
    expect(screen.getByRole('status')).toHaveTextContent('Checking…');
  });

  it('sends nothing once the view has gone while it waited for passkey options', async () => {
    const pending = later();
    serve({ options: () => pending.promise });
    const { unmount } = await renderSignedOut();
    fillIn();
    press(continueButton());
    await wait(0);

    unmount();
    pending.resolve(await json(passkeyOptions(1)));
    await wait(0);

    expect(requestsTo(passwordPath)).toHaveLength(0);
  });

  it('leaves no email or password in localStorage, sessionStorage or the query client', async () => {
    serve({
      password: vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => problem(401))
        .mockImplementationOnce(() => json({ next: 'totp' })),
    });
    const { queryClient } = await renderSignedOut();
    fillIn();
    press(continueButton());
    await wait(0);
    type(passwordField(), workerPassword);
    press(continueButton());
    await wait(0);

    expect(requestsTo(passwordPath)).toHaveLength(2);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
    expect(queryClient.getMutationCache().getAll()).toEqual([]);
    const cached = JSON.stringify(queryClient.getQueryCache().getAll().map(({ queryKey, state }) => [queryKey, state]));
    expect(cached).not.toContain(workerEmail);
    expect(cached).not.toContain(workerPassword);
  });
});

describe('the code step', () => {
  it('shows in view A\'s place, at the same address and with the same title, its field focused', async () => {
    serve();
    const { router } = await renderSignedOut();
    const { location } = router.state;
    const entries = window.history.length;
    fillIn();

    press(continueButton());
    await wait(0);

    const main = within(screen.getByRole('main'));
    const heading = main.getByRole('heading', { level: 1 });
    expect(heading).toHaveAccessibleName(codeLabel);
    expect(heading.firstElementChild).toBe(main.getByText(codeLabel, { selector: 'label' }));
    expect(codeField()).toHaveFocus();
    expect(codeField()).toHaveAccessibleDescription(codeHint);
    expectInOrder(
      heading,
      main.getByText(codeHint),
      codeField(),
      alertAbove(signInButton()),
      signInButton(),
      main.getByText(help),
    );
    // Nothing of A's is left.
    expect(main.getAllByRole('heading')).toEqual([heading]);
    expect(main.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
    expect(main.queryByRole('textbox', { name: 'Email address' })).not.toBeInTheDocument();
    expect(main.queryByText('or')).not.toBeInTheDocument();
    expect(summary()).toBeNull();
    expect(alertAbove(signInButton())).toBeEmptyDOMElement();
    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(router.state.location).toBe(location);
    expect(window.history.length).toBe(entries);
  });

  it('has one code field for a number pad and a filled-in code, in a form of its own with Sign in', async () => {
    serve();
    await renderCodeStep();
    const field = codeField();
    const form = signInButton().closest('form');

    expect(field).toHaveAttribute('id', 'code');
    expect(field).toHaveAttribute('name', 'code');
    expect(field).toHaveAttribute('type', 'text');
    expect(field).toHaveAttribute('inputmode', 'numeric');
    expect(field).toHaveAttribute('autocomplete', 'one-time-code');
    expect(field).not.toHaveAttribute('maxlength');
    expect(field).not.toHaveAttribute('placeholder');
    expect(field).not.toHaveAttribute('aria-invalid');
    expect(field).toHaveValue('');
    expect(form).toHaveAttribute('novalidate');
    expect(form).not.toHaveAttribute('action');
    expect(field.form).toBe(form);
    expect(alertAbove(signInButton()).closest('form')).toBe(form);
    expect(signInButton()).toHaveAttribute('type', 'submit');
    expect(screen.getByText(help, { selector: 'p' }).closest('form')).toBeNull();
  });

  it('takes Tab from the field to Sign in, the one button and primary, with no "remember this device"', async () => {
    serve();
    await renderCodeStep();
    const main = within(screen.getByRole('main'));

    expect(tabStops(screen.getByRole('main'))).toEqual([codeField(), signInButton()]);
    expect(main.getAllByRole('button')).toEqual([signInButton()]);
    expect(signInButton()).toHaveAttribute('class', buttonClass('primary'));
    expect(main.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(main.queryByRole('link')).not.toBeInTheDocument();
    expect(main.queryByText(/remember|forgot/i)).not.toBeInTheDocument();
  });

  describe('checked before anything is sent', () => {
    describe.each([
      { given: 'no code', typed: '', message: codeLabel },
      { given: 'only spaces and a dash', typed: ' - ', message: codeLabel },
      { given: 'five digits', typed: '12345', message: codeFormat },
      { given: 'letters', typed: 'abc', message: codeFormat },
    ])('given $given', ({ typed, message }) => {
      it('shows the message on the field, which takes focus, and "Error: " in the title, sending nothing', async () => {
        serve();
        await renderCodeStep();
        type(codeField(), typed);

        press(signInButton());
        await wait(0);

        expect(codeField()).toHaveFocus();
        expect(codeField()).toHaveAttribute('aria-invalid', 'true');
        expect(codeField()).toHaveAccessibleDescription(`${codeHint} Error: ${message}`);
        expectInOrder(screen.getByText(codeHint), screen.getByText(message, { selector: 'p' }), codeField());
        // On the field only, as the code step's form has one field (form-validation.md rule 6).
        expect(summary()).toBeNull();
        expect(document.title).toBe('Error: Grow2Notes – Sign in');
        expect(requestsTo(codePath)).toHaveLength(0);
        expect(codeField()).toHaveValue(typed);
        expect(alertAbove(signInButton())).toBeEmptyDOMElement();
        expect(signInButton()).not.toHaveAttribute('aria-disabled');
      });
    });

    it('focuses the field again on each press that finds the same message', async () => {
      serve();
      await renderCodeStep();
      press(signInButton());
      await wait(0);

      press(signInButton());
      await wait(0);

      expect(codeField()).toHaveFocus();
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
    });

    it('takes the message, aria-invalid and "Error: " away as the code is fixed, focus left in the field', async () => {
      serve();
      await renderCodeStep();
      type(codeField(), '12345');
      press(signInButton());
      await wait(0);

      // Checked again at each change, so that the message says what is wrong now.
      type(codeField(), '');

      expect(codeField()).toHaveAccessibleDescription(`${codeHint} Error: ${codeLabel}`);

      type(codeField(), '123 456');

      expect(codeField()).not.toHaveAttribute('aria-invalid');
      expect(codeField()).toHaveAccessibleDescription(codeHint);
      expect(document.getElementById('code-error')).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(codeField()).toHaveFocus();
      expect(requestsTo(codePath)).toHaveLength(0);
    });
  });

  it.each([
    { kind: 'a space', typed: '123 456' },
    { kind: 'a hyphen', typed: '123-456' },
    { kind: 'full-width digits', typed: '１２３４５６' },
  ])('sends a code typed with $kind as its six digits, with the antiforgery token', async ({ typed }) => {
    serve();
    await renderCodeStep();
    type(codeField(), typed);

    press(signInButton());
    await wait(0);

    const [init] = requestsTo(codePath);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ code: '123456' });
    const headers = new Headers(init?.headers);
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-XSRF-TOKEN')).toBe('token-1');
  });

  it('sends nothing as six digits are typed, however long they wait, and sends them for Enter', async () => {
    serve();
    await renderCodeStep();

    type(codeField(), '123456');
    await wait(60_000);

    expect(requestsTo(codePath)).toHaveLength(0);

    pressEnter(codeField());
    await wait(0);

    expect(requestsTo(codePath)).toHaveLength(1);
  });

  it('shows the screen in its place after a 200 Me, at the same address, its heading focused', async () => {
    serve();
    const { queryClient, router } = await renderSignedOut('/manage/anything');
    const { location } = router.state;
    const entries = window.history.length;
    fillIn();
    press(continueButton());
    await wait(0);
    type(codeField(), '123456');

    press(signInButton());
    await wait(0);

    expect(queryClient.getQueryData(['me'])).toEqual(worker);
    // The screen at that address, which no route matches.
    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toHaveFocus();
    expect(screen.queryByRole('textbox', { name: codeLabel })).not.toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(router.state.location).toBe(location);
    expect(window.history.length).toBe(entries);
    // The app took the sign-in's answer, with no second request.
    expect(requestsTo(mePath)).toHaveLength(1);
  });

  // The code step lasts 5 minutes from the accepted password (D69); a refusal from 4 min 50 s on ends it.
  describe.each([401, 429])('given a %i for the code', (status) => {
    it('at 4 min 49 s shows the A25 message in the summary, which takes focus, and empties the code', async () => {
      serve({ code: () => problem(status) });
      await renderCodeStep();
      await wait(289_000);
      type(codeField(), '123456');
      const button = signInButton();

      press(button);
      await wait(0);

      expect(summary()).toHaveFocus();
      expect(summaryItems()).toEqual([{ text: failedMessage }]);
      expectInOrder(
        screen.getByRole('heading', { level: 2, name: 'There is a problem' }),
        screen.getByRole('heading', { level: 1, name: codeLabel }),
        codeField(),
      );
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
      expect(codeField()).toHaveValue('');
      // It marks no field, which would say which part was wrong (A25).
      expect(codeField()).not.toHaveAttribute('aria-invalid');
      expect(codeField()).toHaveAccessibleDescription(codeHint);
      expect(alertAbove(button)).toBeEmptyDOMElement();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(screen.queryByRole('textbox', { name: 'Email address' })).not.toBeInTheDocument();
    });

    it('at 4 min 50 s shows view A again, with the email, the summary focused and new passkey options', async () => {
      serve({ code: () => problem(status) });
      await renderCodeStep();
      await wait(290_000);
      type(codeField(), '123456');

      press(signInButton());
      await wait(0);

      expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument();
      expect(screen.queryByRole('textbox', { name: codeLabel })).not.toBeInTheDocument();
      expect(summary()).toHaveFocus();
      expect(summaryItems()).toEqual([{ text: failedMessage }]);
      expect(document.title).toBe('Error: Grow2Notes – Sign in');
      expect(emailField()).toHaveValue(workerEmail);
      expect(passwordField()).toHaveValue('');
      expect(emailField()).not.toHaveAttribute('aria-invalid');
      expect(passwordField()).not.toHaveAttribute('aria-invalid');
      expect(alertAbove(continueButton())).toBeEmptyDOMElement();
      expect(continueButton()).not.toHaveAttribute('aria-disabled');
      expect(requestsTo(optionsPath)).toHaveLength(2);

      // The new options open the prompt straight from the press.
      press(passkeyButton());

      expect(ceremony).toHaveBeenCalledExactlyOnceWith({ optionsJSON: passkeyOptions(2) });
    });
  });

  it('leaves view A\'s passkey sign-in asking for options and saying what it must once A shows again', async () => {
    serve({ code: () => problem(401), passkey: () => problem(401) });
    await renderCodeStep();
    await wait(290_000);
    type(codeField(), '123456');
    press(signInButton());
    await wait(0);
    expect(requestsTo(optionsPath)).toHaveLength(2);

    // The options fetched as A showed again are now 4 minutes old, so the press asks for new ones first.
    await wait(240_000);
    press(passkeyButton());
    await wait(0);

    expect(requestsTo(optionsPath)).toHaveLength(4);
    expect(ceremony).toHaveBeenCalledExactlyOnceWith({ optionsJSON: passkeyOptions(3) });
    expect(requestsTo(passkeyPath)).toHaveLength(1);
    expect(summary()).toHaveFocus();
    expect(summaryItems()).toEqual([{ text: failedMessage }]);
  });

  it('shows a refusal after a fixed format error with no message on the field, which is not marked', async () => {
    serve({ code: () => problem(401) });
    await renderCodeStep();
    type(codeField(), '12345');
    press(signInButton());
    await wait(0);
    type(codeField(), '123456');

    press(signInButton());
    await wait(0);

    expect(summary()).toHaveFocus();
    expect(codeField()).not.toHaveAttribute('aria-invalid');
    expect(codeField()).toHaveAccessibleDescription(codeHint);
    expect(document.getElementById('code-error')).toBeNull();
    // One "Error: ", the summary's.
    expect(document.title).toBe('Error: Grow2Notes – Sign in');
  });

  it('empties the summary on the next press, and focuses it again when that code is refused too', async () => {
    serve({ code: () => problem(401) });
    await renderCodeStep();
    type(codeField(), '123456');
    press(signInButton());
    await wait(0);

    type(codeField(), '654321');
    press(signInButton());

    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Sign in');

    await wait(0);

    expect(summary()).toHaveFocus();
    expect(requestsTo(codePath)).toHaveLength(2);
  });

  describe.each([
    { failure: 'no connection', answer: noConnection, message: 'Not signed in: no connection. Try again.' },
    { failure: 'a 503', answer: () => problem(503), message: 'Not signed in: something went wrong. Try again.' },
  ])('given $failure', ({ answer, message }) => {
    // Only a refusal ends the step: a request that got no answer may be sent again.
    it.each([
      { at: '4 min 49 s', after: 289_000 },
      { at: '4 min 50 s', after: 290_000 },
    ])('at $at says so above Sign in, where focus stays, keeping the code, with no summary', async ({ after }) => {
      serve({ code: answer });
      await renderCodeStep();
      await wait(after);
      type(codeField(), '123456');
      const button = signInButton();

      press(button);
      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(codeField()).toHaveValue('123456');
      expect(codeField()).not.toHaveAttribute('aria-invalid');
    });

    it('empties the alert line on the next press, so that the same failure is announced again', async () => {
      serve({ code: answer });
      await renderCodeStep();
      type(codeField(), '123456');
      const button = signInButton();
      press(button);
      await wait(0);

      press(button);

      expect(alertAbove(button)).toBeEmptyDOMElement();

      await wait(0);

      expect(alertAbove(button)).toHaveTextContent(message);
      expect(requestsTo(codePath)).toHaveLength(2);
    });
  });

  it('asks for no passkey options while it shows, through refusals and failures', async () => {
    serve({
      code: vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => problem(401))
        .mockImplementationOnce(noConnection),
    });
    await renderCodeStep();

    for (const sent of [1, 2]) {
      await wait(60_000);
      type(codeField(), '123456');
      press(signInButton());
      await wait(0);
      expect(requestsTo(codePath)).toHaveLength(sent);
    }
    await wait(169_000);

    expect(requestsTo(optionsPath)).toHaveLength(1);
  });

  it('cancels a passkey prompt still open as it shows, which then says and asks for nothing', async () => {
    const signals = promptUntilCancelled();
    const consoleError = vi.spyOn(console, 'error');
    serve({ code: () => problem(401) });
    await renderSignedOut();
    press(passkeyButton());
    fillIn();

    press(continueButton());
    await wait(0);

    expect(signals).toHaveLength(1);
    expect(signals[0].aborted).toBe(true);
    expect(codeField()).toHaveFocus();
    expect(summary()).toBeNull();
    expect(alertAbove(signInButton())).toBeEmptyDOMElement();
    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(requestsTo(optionsPath)).toHaveLength(1);
    expect(requestsTo(passkeyPath)).toHaveLength(0);
    expect(consoleError).not.toHaveBeenCalled();

    // Nor does it say anything once A shows again.
    await wait(290_000);
    type(codeField(), '123456');
    press(signInButton());
    await wait(0);

    expect(alertAbove(passkeyButton())).toBeEmptyDOMElement();
    expect(passkeyButton()).not.toHaveAttribute('aria-disabled');
    expect(summaryItems()).toEqual([{ text: failedMessage }]);
  });

  it('sends one request per press, nothing for a second press or Enter while busy, never disabled', async () => {
    serve({ code: noAnswer });
    await renderCodeStep();
    type(codeField(), '123456');
    const button = signInButton();

    press(button);
    press(button);
    await wait(0);

    expect(requestsTo(codePath)).toHaveLength(1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('disabled');
    expect(button).toHaveFocus();

    pressEnter(codeField());
    await wait(0);

    expect(requestsTo(codePath)).toHaveLength(1);
  });

  it('shows the busy label "Signing in…" at 400 ms, not at 399 ms, and writes it to the status region', async () => {
    serve({ code: noAnswer });
    await renderCodeStep();
    type(codeField(), '123456');
    const button = signInButton();

    press(button);
    await wait(399);

    expect(button).toHaveAccessibleName('Sign in');
    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    await wait(1);
    act(() => {
      vi.advanceTimersToNextFrame();
    });

    expect(button).toHaveAccessibleName('Signing in…');
    expect(screen.getByRole('status')).toHaveTextContent('Signing in…');
  });

  it('leaves no email, password or code in localStorage, sessionStorage or the query client', async () => {
    serve({
      code: vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => problem(401))
        .mockImplementationOnce(() => json(worker)),
    });
    const { queryClient } = await renderCodeStep();
    type(codeField(), '135 790');
    press(signInButton());
    await wait(0);
    type(codeField(), '246 801');
    press(signInButton());
    await wait(0);

    expect(queryClient.getQueryData(['me'])).toEqual(worker);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
    expect(queryClient.getMutationCache().getAll()).toEqual([]);
    // Not the times of the queries' updates, whose digits could hold a code by chance.
    const cached = JSON.stringify(
      queryClient.getQueryCache().getAll().map(({ queryKey, state }) => [queryKey, state.data, state.error]),
    );
    for (const typed of [workerEmail, workerPassword, '135 790', '135790', '246 801', '246801']) {
      expect(cached).not.toContain(typed);
    }
  });
});
