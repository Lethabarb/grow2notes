import type * as SimpleWebAuthn from '@simplewebauthn/browser';
import {
  startAuthentication,
  WebAuthnAbortService,
  WebAuthnError,
  type AuthenticationResponseJSON,
  type PublicKeyCredentialRequestOptionsJSON,
} from '@simplewebauthn/browser';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { StrictMode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { PasskeyStep } from './PasskeyStep.tsx';

// The browser's passkey prompt, which jsdom has not got; the rest of SimpleWebAuthn is its own.
vi.mock('@simplewebauthn/browser', async (importOriginal) => ({
  ...(await importOriginal<typeof SimpleWebAuthn>()),
  startAuthentication: vi.fn(),
}));

const ceremony = vi.mocked(startAuthentication);

const antiforgeryPath = '/api/auth/antiforgery';
const optionsPath = '/api/auth/login/passkey/options';
const passkeyPath = '/api/auth/passkey';

// A made-up worker (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const options: PublicKeyCredentialRequestOptionsJSON = {
  challenge: 'c3RlcC1jaGFsbGVuZ2U',
  timeout: 300_000,
  rpId: 'localhost',
  allowCredentials: [{ id: 'Y3JlZGVudGlhbA', type: 'public-key' }],
  userVerification: 'required',
};

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

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that has not answered yet: it rejects only when its signal aborts. */
function noAnswerUntilAborted(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** The error that SimpleWebAuthn gives for a prompt that the person dismissed, or that found no passkey. */
function dismissal(): WebAuthnError {
  return new WebAuthnError({
    message: 'The operation either timed out or was not allowed.',
    code: 'ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY',
    cause: new DOMException('The operation either timed out or was not allowed.', 'NotAllowedError'),
  });
}

type Server = {
  /** Each request for the step's options, `200` with `options` unless given. */
  options?: (init?: RequestInit) => Promise<Response>;
  /** Each passkey sign-in's answer, a `200 Me` unless given. */
  passkey?: (init?: RequestInit) => Promise<Response>;
};

/** Stubs `fetch` as the server: the antiforgery endpoint gives a token, and the step's two calls answer as given. */
function serve({ options: optionsAnswer = () => json(options), passkey = () => json(worker) }: Server = {}) {
  fetchMock.mockImplementation((path, init) => {
    switch (path) {
      case antiforgeryPath:
        return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));
      case optionsPath:
        return optionsAnswer(init);
      case passkeyPath:
        return passkey(init);
      default:
        throw new Error('The test gave no answer for this request.');
    }
  });
}

/** The requests sent to `path`. */
function requestsTo(path: string) {
  return fetchMock.mock.calls.filter(([sent]) => sent === path).map(([, init]) => init);
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Renders the step under a query client of its own, and lets its options come. */
async function renderStep(server: Server = {}, { strict = false, heading = false } = {}) {
  serve(server);
  const queryClient = createQueryClient();
  const onRefused = vi.fn();
  const onBack = vi.fn();
  const step = (
    <QueryClientProvider client={queryClient}>
      <PasskeyStep
        heading={heading ? <h1>You’ve been signed out</h1> : undefined}
        onRefused={onRefused}
        onBack={onBack}
      />
    </QueryClientProvider>
  );
  const { unmount } = render(strict ? <StrictMode>{step}</StrictMode> : step);
  await wait(0);
  return { queryClient, onRefused, onBack, unmount };
}

function signInButton() {
  return screen.getByRole<HTMLButtonElement>('button', { name: 'Sign in with a passkey' });
}

function goBackButton() {
  return screen.getByRole<HTMLButtonElement>('button', { name: 'Go back' });
}

/** Presses `button` as a keyboard or a pointer does, with focus on it first. */
function press(button: HTMLElement) {
  button.focus();
  fireEvent.click(button);
}

/**
 * The alert line directly above the button, where its request's failures show. A test that holds the button past
 * the 400 ms at which its busy label takes over its name passes it in, as it can no longer be found by that name.
 */
function alert(button: HTMLElement = signInButton()) {
  const line = button.previousElementSibling;
  if (!(line instanceof HTMLElement) || line.getAttribute('role') !== 'alert') {
    throw new Error('No alert line comes directly before the button.');
  }

  return line;
}

/**
 * A passkey prompt as SimpleWebAuthn's own opens one, under `WebAuthnAbortService`'s signal, which ends only when the
 * view cancels it.
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
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
  ceremony.mockResolvedValue(credential);
});

afterEach(() => {
  vi.restoreAllMocks();
  fetchMock.mockReset();
  ceremony.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the passkey step', () => {
  it('asks for its options once under StrictMode, and focuses its button', async () => {
    await renderStep({}, { strict: true });

    expect(requestsTo(optionsPath)).toHaveLength(1);
    expect(requestsTo(optionsPath)[0]?.method).toBe('POST');
    expect(signInButton()).toHaveFocus();
    expect(screen.getByRole('heading', { level: 1, name: 'Use your passkey' })).toBeInTheDocument();
  });

  it('has an <h2> under the heading that it is given', async () => {
    await renderStep({}, { heading: true });

    expect(screen.getByRole('heading', { level: 1, name: 'You’ve been signed out' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Use your passkey' })).toBeInTheDocument();
  });

  it('opens the prompt with its options straight from the press, then signs in with the credential', async () => {
    const { queryClient, onRefused } = await renderStep();

    press(signInButton());

    // Before any await: the prompt is open as the press returns.
    expect(ceremony).toHaveBeenCalledExactlyOnceWith({ optionsJSON: options });
    await wait(0);

    const [init] = requestsTo(passkeyPath);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ credentialJson: JSON.stringify(credential) });
    expect(queryClient.getQueryData(['me'])).toEqual(worker);
    expect(onRefused).not.toHaveBeenCalled();
  });

  it('says and does nothing for a dismissed prompt, and the next press uses the options that it holds', async () => {
    const { onRefused } = await renderStep();
    ceremony.mockRejectedValueOnce(dismissal());

    press(signInButton());
    await wait(0);

    expect(alert()).toBeEmptyDOMElement();
    expect(signInButton()).not.toHaveAttribute('aria-disabled');
    expect(signInButton()).toHaveFocus();
    expect(requestsTo(passkeyPath)).toHaveLength(0);
    expect(onRefused).not.toHaveBeenCalled();

    press(signInButton());
    await wait(0);

    expect(ceremony).toHaveBeenCalledTimes(2);
    expect(ceremony).toHaveBeenLastCalledWith({ optionsJSON: options });
    expect(requestsTo(optionsPath)).toHaveLength(1);
    expect(requestsTo(passkeyPath)).toHaveLength(1);
  });

  describe.each([401, 429])('given a %i', (status) => {
    it('for the credential, ends the step, once', async () => {
      const { onRefused } = await renderStep({ passkey: () => problem(status) });

      press(signInButton());
      await wait(0);

      expect(onRefused).toHaveBeenCalledOnce();
    });

    it('for the options, ends the step, once, even under StrictMode', async () => {
      const { onRefused } = await renderStep({ options: () => problem(status) }, { strict: true });

      expect(onRefused).toHaveBeenCalledOnce();
      expect(ceremony).not.toHaveBeenCalled();
    });
  });

  it('asks again for options older than 4 minutes, and ends the step for the refusal', async () => {
    const { onRefused } = await renderStep({
      options: vi
        .fn<() => Promise<Response>>()
        .mockImplementationOnce(() => json(options))
        .mockImplementationOnce(() => problem(401)),
    });
    await wait(4 * 60_000);

    press(signInButton());
    await wait(0);

    expect(requestsTo(optionsPath)).toHaveLength(2);
    expect(ceremony).not.toHaveBeenCalled();
    expect(onRefused).toHaveBeenCalledOnce();
  });

  describe.each([
    { failure: 'no connection', answer: noConnection, message: 'Not signed in: no connection. Try again.' },
    { failure: 'a 503', answer: () => problem(503), message: 'Not signed in: something went wrong. Try again.' },
  ])('given $failure', ({ answer, message }) => {
    it('for the credential, says so above the button, where focus stays, and the step stays', async () => {
      const { onRefused } = await renderStep({ passkey: answer });

      press(signInButton());
      await wait(0);

      expect(alert()).toHaveTextContent(message);
      expect(signInButton()).toHaveFocus();
      expect(signInButton()).not.toHaveAttribute('aria-disabled');
      expect(onRefused).not.toHaveBeenCalled();
    });

    it('for the options, says nothing until a press asks again, and then says so', async () => {
      const { onRefused } = await renderStep({ options: answer });
      expect(alert()).toBeEmptyDOMElement();

      press(signInButton());
      await wait(0);

      expect(alert()).toHaveTextContent(message);
      expect(requestsTo(optionsPath)).toHaveLength(2);
      expect(ceremony).not.toHaveBeenCalled();
      expect(onRefused).not.toHaveBeenCalled();
    });
  });

  // A call that the server completes after the SPA has stopped waiting has used the password step, so both wait as
  // long as sign-in (D99).
  it('waits 3 minutes for the credential, busy, before it says there is no connection', async () => {
    await renderStep({ passkey: noAnswerUntilAborted });

    const button = signInButton();
    press(button);
    await wait(179_999);

    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(alert(button)).toBeEmptyDOMElement();

    await wait(1);

    expect(alert(button)).toHaveTextContent('Not signed in: no connection. Try again.');
    expect(button).not.toHaveAttribute('aria-disabled');
  });

  it('waits 3 minutes for its options before a press that awaits them says there is no connection', async () => {
    await renderStep({ options: noAnswerUntilAborted });

    const button = signInButton();
    press(button);
    await wait(179_999);

    expect(alert(button)).toBeEmptyDOMElement();
    expect(ceremony).not.toHaveBeenCalled();

    await wait(1);

    expect(alert(button)).toHaveTextContent('Not signed in: no connection. Try again.');
  });

  describe('Go back', () => {
    it('calls onBack as the step shows', async () => {
      const { onBack } = await renderStep();

      press(goBackButton());

      expect(onBack).toHaveBeenCalledOnce();
    });

    it('calls onBack while the prompt is open', async () => {
      promptUntilCancelled();
      const { onBack } = await renderStep();
      press(signInButton());
      await wait(0);

      press(goBackButton());

      expect(onBack).toHaveBeenCalledOnce();
    });

    it('calls onBack after a dismissed prompt and after a failure', async () => {
      const { onBack } = await renderStep({ passkey: () => problem(503) });
      ceremony.mockRejectedValueOnce(dismissal());
      press(signInButton());
      await wait(0);
      press(goBackButton());
      press(signInButton());
      await wait(0);

      press(goBackButton());

      expect(onBack).toHaveBeenCalledTimes(2);
    });
  });

  it('cancels a prompt still open when it goes', async () => {
    const signals = promptUntilCancelled();
    const { unmount } = await renderStep();
    press(signInButton());
    await wait(0);

    unmount();

    expect(signals).toHaveLength(1);
    expect(signals[0].aborted).toBe(true);
  });

  it('says that passkeys do not work in a browser with no WebAuthn, fetches nothing, and keeps Go back', async () => {
    vi.unstubAllGlobals();
    vi.stubGlobal('fetch', fetchMock);

    const { onBack } = await renderStep();

    expect(screen.getByText(/Passkeys do not work in this browser/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Use your passkey' })).toHaveFocus();
    expect(fetchMock).not.toHaveBeenCalled();

    press(goBackButton());

    expect(onBack).toHaveBeenCalledOnce();
  });
});
