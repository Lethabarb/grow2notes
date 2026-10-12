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
const signInPath = '/api/auth/passkey';

// A made-up worker (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

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

const failedMessage =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';

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

/**
 * Stubs `fetch` as the server: nobody is signed in, the antiforgery endpoint gives a token, each sign-in gets a new
 * answer from `signIn`, a `200 Me` unless given, and the `n`th request for passkey options gets the answer that
 * `options(n)` gives, `passkeyOptions(n)` unless given.
 */
function serve(
  signIn: () => Promise<Response> = () => json(worker),
  options: (n: number) => Promise<Response> = (n) => json(passkeyOptions(n)),
) {
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
      case signInPath:
        return signIn();
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

/** Presses `button` as a keyboard or a pointer does, with focus on it first. */
function press(button: HTMLElement) {
  button.focus();
  fireEvent.click(button);
}

/** The error summary, which holds its heading, or null while it does not show. */
function summary() {
  return screen.queryByRole('heading', { level: 2, name: 'There is a problem' })?.parentElement ?? null;
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

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
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

    const [init] = requestsTo(signInPath);
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
      serve(() => problem(status));
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
      expect(screen.getByRole('alert')).toBeEmptyDOMElement();
      expect(button).not.toHaveAttribute('aria-disabled');
      // New options, as the server has ended the ceremony of the old.
      expect(requestsTo(optionsPath)).toHaveLength(2);
    });
  });

  it('empties the summary on the next press, and focuses it again when that sign-in is refused too', async () => {
    serve(() => problem(401));
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
    expect(screen.getByRole('alert')).toBeEmptyDOMElement();
    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(requestsTo(signInPath)).toHaveLength(0);
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
      signIn: () => json(worker),
      message: 'Not signed in: something went wrong. Try again.',
    },
    {
      failure: 'no connection',
      prompt: () => Promise.resolve(credential),
      signIn: noConnection,
      message: 'Not signed in: no connection. Try again.',
    },
    {
      failure: 'a 503',
      prompt: () => Promise.resolve(credential),
      signIn: () => problem(503),
      message: 'Not signed in: something went wrong. Try again.',
    },
  ])('given $failure', ({ prompt, signIn, message }) => {
    it('says so in the alert line above the button, where focus stays, with no summary', async () => {
      ceremony.mockImplementation(prompt);
      serve(signIn);
      await renderSignedOut();
      const button = passkeyButton();

      press(button);
      await wait(0);

      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent(message);
      expect(alert.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Sign in');
      expect(requestsTo(optionsPath)).toHaveLength(2);
    });

    it('empties the alert line on the next press, so that the same failure is announced again', async () => {
      ceremony.mockImplementation(prompt);
      serve(signIn);
      await renderSignedOut();
      const button = passkeyButton();
      press(button);
      await wait(0);

      press(button);

      expect(screen.getByRole('alert')).toBeEmptyDOMElement();

      await wait(0);

      expect(screen.getByRole('alert')).toHaveTextContent(message);
    });
  });

  // Each request for options ends the ceremony of those before it, so a prompt opened with the old ones while new ones
  // are on their way would sign a challenge the server no longer holds, and the sign-in would be refused.
  describe.each([
    { failure: 'a dismissed prompt', prompt: () => Promise.reject(dismissal()), signIn: () => json(worker) },
    { failure: 'a refused sign-in', prompt: () => Promise.resolve(credential), signIn: () => problem(401) },
  ])('after $failure', ({ prompt, signIn }) => {
    it('a press while new options are on their way waits for them, and never uses the old', async () => {
      ceremony.mockImplementationOnce(prompt);
      const refetch = later();
      serve(signIn, (n) => (n === 2 ? refetch.promise : json(passkeyOptions(n))));
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

    expect(screen.getByRole('alert')).toHaveTextContent('Not signed in: no connection. Try again.');
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
    expect(requestsTo(signInPath)).toHaveLength(0);
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
      serve(() => answer.promise);
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
    serve(
      vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => problem(401))
        .mockImplementationOnce(() => json(worker)),
    );
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
