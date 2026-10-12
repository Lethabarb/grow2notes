import type * as SimpleWebAuthn from '@simplewebauthn/browser';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createBrowserRouter } from 'react-router';
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
const optionsPath = '/api/auth/setup/passkey/options';
const passkeyPath = '/api/auth/setup/passkey';
const title = 'Grow2Notes – Set up your account';
// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

// A made-up person and link (A35).
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const userId = '6f9619ff-8b86-d011-b42d-00cf4fc964ff';
const setupLink = `/setup#u=${userId}&t=made-up-token`;
const me = { userId, displayName: 'Sam Lee', role: 'Worker', organisationName: 'Example Care', today: '2026-10-12' };
// A made-up manager (A35) whose session is live in the same browser, as setup's pages show whoever is signed in.
const someoneElse = {
  userId: '0c1d7e52-93a8-4b6f-a2d4-58e1f9c3b706',
  displayName: 'Riley Ng',
  role: 'Manager',
  organisationName: 'Example Care',
  today: '2026-10-12',
};

// Made-up creation options, each with a challenge of its own, and the passkey that the prompt makes with them.
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

const credential: SimpleWebAuthn.RegistrationResponseJSON = {
  id: 'bWFkZS11cA',
  rawId: 'bWFkZS11cA',
  response: { attestationObject: 'bWFkZS11cA', clientDataJSON: 'bWFkZS11cA' },
  type: 'public-key',
  clientExtensionResults: {},
};

type Answer = (init?: RequestInit) => Promise<Response>;

const fetchMock = vi.fn<typeof fetch>();

/** The answers to each request the page sends, in turn, but for the antiforgery token's. */
const answers: Record<string, Answer[]> = { [startPath]: [], [optionsPath]: [], [passkeyPath]: [] };

/** The number of creation options served, from which each new one takes its challenge. */
let served = 0;

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

/** An answer, or a passkey, that comes when the test says. */
function later<T>() {
  let give: (value: T) => void = () => undefined;
  let fail: (error: unknown) => void = () => undefined;
  const value = new Promise<T>((resolve, reject) => {
    give = resolve;
    fail = reject;
  });
  return { value, give, fail };
}

/** A WebAuthn error as the browser raises it, which the page never names to the person. */
function browserError(name: string): DOMException {
  return new DOMException(`A made-up ${name}.`, name);
}

/** The bodies of the requests sent to `path`, in order. */
function sent(path: string): unknown[] {
  return fetchMock.mock.calls
    .filter(([sentTo]) => sentTo === path)
    .map(([, init]) => (typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined));
}

/** Shows what the setup layout holds. */
function SetupMemory() {
  const { account: held } = useSetup();
  return <p data-testid="setup-memory">{held === undefined ? '' : held.email}</p>;
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

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/**
 * Opens the emailed link on a browser router, as `main.tsx` does, under a query client of its own and StrictMode,
 * which runs each effect twice, and waits for view E, whose options the page prefetches.
 */
async function openViewE(): Promise<{ router: ReturnType<typeof createBrowserRouter>; client: QueryClient }> {
  answers[startPath].push(() => json(account));
  window.history.replaceState(null, '', setupLink);
  const router = createBrowserRouter(withSetupMemory(routes));
  routers.push(router);
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

function passkeyButton() {
  return screen.getByRole('button', { name: 'Use a passkey (recommended)' });
}

/** Focuses the passkey button and presses it, as a keyboard user would, and returns it. */
function pressPasskey() {
  const button = passkeyButton();
  button.focus();
  fireEvent.click(button);
  return button;
}

function alert() {
  return within(screen.getByRole('main')).getByRole('alert');
}

function heading() {
  return screen.getByRole('heading', { level: 1 });
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
  webAuthn.supported = true;
  // A prompt still open, unless a test answers it.
  webAuthn.startRegistration.mockReturnValue(new Promise(() => undefined));
  // The wrapper fetches its antiforgery token before the first change, and keeps it for the next; new options are
  // served whenever the test gives no other answer for them.
  fetchMock.mockImplementation((path, init) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up' } }));
    }

    // The shell's, for a person with no session until setup ends.
    if (path === mePath) {
      return problem(401);
    }

    const answer = answers[path as string]?.shift();
    if (answer !== undefined) {
      return answer(init);
    }

    if (path === optionsPath) {
      served += 1;
      return json(creationOptions(`challenge-${served}`));
    }

    throw new Error('The test gave no answer for this request.');
  });
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  for (const router of routers.splice(0)) {
    router.dispose();
  }

  for (const queue of Object.values(answers)) {
    queue.length = 0;
  }

  fetchMock.mockReset();
  webAuthn.startRegistration.mockReset();
  webAuthn.cancelCeremony.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('ChooseMethod (view E)', () => {
  it('shows the account and the two methods in order, with their names and hints, and the plain title', async () => {
    await openViewE();

    const main = within(screen.getByRole('main'));
    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(main.getAllByRole('term').map((term) => term.textContent)).toEqual(['Name', 'Email address']);
    expect(main.getAllByRole('definition').map((value) => value.textContent)).toEqual([
      'Sam Lee',
      'sam.lee@example.org',
    ]);
    const choose = main.getByRole('heading', { level: 2 });
    expect(choose).toHaveTextContent('Choose how you’ll sign in');

    const button = passkeyButton();
    expect(button).toHaveAttribute('type', 'button');
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(button).toHaveAccessibleDescription(
      'You’ll sign in with your face, fingerprint or PIN, the same way you unlock your phone. Set it up on the phone '
        + 'you’ll use for Grow2Notes.',
    );
    const link = main.getByRole('link', { name: 'Use a password and authenticator app' });
    expect(link).toHaveAttribute('href', '/setup/password');
    expect(link).toHaveAttribute('class', buttonClass('secondary'));
    expect(link).toHaveAccessibleDescription(
      'You’ll create a password and use a free app that shows a 6-digit code each time you sign in.',
    );
    // There from the start, and empty until a press fails.
    expect(alert()).toBeEmptyDOMElement();
    expectInOrder(
      heading(),
      main.getByText('Name'),
      main.getByText('If these are wrong, ask a manager.'),
      choose,
      alert(),
      button,
      main.getByText(/^You’ll sign in with your face/),
      main.getByText('or'),
      link,
      main.getByText(/^You’ll create a password/),
    );
    expect(document.title).toBe(title);
  });

  it('prefetches one options object as it shows, under StrictMode too, and opens no prompt by itself', async () => {
    await openViewE();
    await wait(10 * 60_000);

    expect(sent(optionsPath)).toHaveLength(1);
    expect(webAuthn.startRegistration).not.toHaveBeenCalled();
    expect(sent(passkeyPath)).toHaveLength(0);
  });

  it('opens the prompt in the press itself, before any await, with options under 4 minutes old', async () => {
    await openViewE();
    await wait(4 * 60_000 - 1);

    pressPasskey();

    // Nothing was awaited between the press and here.
    expect(webAuthn.startRegistration).toHaveBeenCalledExactlyOnceWith({ optionsJSON: creationOptions('challenge-1') });
    expect(sent(optionsPath)).toHaveLength(1);
  });

  it('fetches new options first once the held ones are 4 minutes old, then opens the prompt with them', async () => {
    await openViewE();
    await wait(4 * 60_000);

    pressPasskey();
    expect(webAuthn.startRegistration).not.toHaveBeenCalled();
    await wait(0);

    expect(sent(optionsPath)).toHaveLength(2);
    expect(webAuthn.startRegistration).toHaveBeenCalledExactlyOnceWith({ optionsJSON: creationOptions('challenge-2') });
  });

  it('waits for the prefetch still on its way, rather than asking for options a second time', async () => {
    const options = later<Response>();
    answers[optionsPath].push(() => options.value);
    await openViewE();

    pressPasskey();
    await wait(0);
    expect(webAuthn.startRegistration).not.toHaveBeenCalled();

    options.give(
      new Response(JSON.stringify(creationOptions('slow')), { headers: { 'Content-Type': 'application/json' } }),
    );
    await wait(0);

    expect(sent(optionsPath)).toHaveLength(1);
    expect(webAuthn.startRegistration).toHaveBeenCalledExactlyOnceWith({ optionsJSON: creationOptions('slow') });
  });

  it('sends the passkey with the name Grow2Notes, and on 200 replaces /setup with Today and drops setup', async () => {
    webAuthn.startRegistration.mockResolvedValueOnce(credential);
    answers[passkeyPath].push(() => json(me));
    const { router, client } = await openViewE();
    const entries = window.history.length;
    const meRequests = sent(mePath).length;

    pressPasskey();
    await wait(0);

    expect(sent(passkeyPath)).toEqual([{ credentialJson: JSON.stringify(credential), name: 'Grow2Notes' }]);
    expect(window.location.pathname).toBe('/');
    // Replaced, not pushed, so that Back from Today does not come back to setup.
    expect(window.history.length).toBe(entries);
    expect(router.state.location.state).toBeNull();
    expect(screen.queryByTestId('setup-memory')).not.toBeInTheDocument();
    // The shell has the signed-in user from the answer, and shows Today for its today in the signed-in frame
    // (sign-in.md *Interactions* 5), whatever the new location's one refresh of /me answers: here the test's 401.
    expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
    expect(sent(mePath)).toHaveLength(meRequests + 1);
    expect(heading()).toHaveAccessibleName(`Today, Monday 12${nbsp}October`);
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Today');
    const kept = JSON.stringify(window.history.state);
    for (const part of [account.email, account.displayName, 'passkey']) {
      expect(kept).not.toContain(part);
    }

    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it('keeps its Me in [\'me\'] when a refresh sent before its answer answers after it, for another', async () => {
    // Another person is signed in, so /me answers them until setup's answer replaces the cookie; the refresh that the
    // window's focus sends as the prompt closes went with their cookie, and is held until after setup's answer.
    let session = someoneElse;
    const stale = later<Response>();
    let holdNext = false;
    const server = fetchMock.getMockImplementation();
    fetchMock.mockImplementation((path, init) => {
      if (path !== mePath) {
        return server?.(path, init) ?? noConnection();
      }

      if (holdNext) {
        holdNext = false;
        return stale.value;
      }

      return json(session);
    });
    const prompt = later<SimpleWebAuthn.RegistrationResponseJSON>();
    webAuthn.startRegistration.mockReturnValueOnce(prompt.value);
    answers[passkeyPath].push(() => {
      session = me;
      return json(me);
    });
    const { client } = await openViewE();
    expect(client.getQueryData(meQuery.queryKey)).toEqual(someoneElse);

    pressPasskey();
    holdNext = true;
    act(() => {
      window.dispatchEvent(new Event('focus'));
    });
    prompt.give(credential);
    await wait(0);
    stale.give(new Response(JSON.stringify(someoneElse), { headers: { 'Content-Type': 'application/json' } }));
    await wait(0);

    expect(window.location.pathname).toBe('/');
    expect(client.getQueryData(meQuery.queryKey)).toEqual(me);
    expect(screen.getByText('Signed in as Sam Lee')).toBeInTheDocument();
    expect(screen.queryByText('Signed in as Riley Ng')).not.toBeInTheDocument();
    // A worker's nav, not the manager's.
    expect(within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('link')).toHaveLength(1);
  });

  describe('when the passkey is not set up', () => {
    const notSetUp =
      'Your passkey was not set up. Try again, or open the link from your email in your phone’s browser.';

    it.each([
      { failure: 'a prompt the person stopped', prompt: browserError('NotAllowedError'), sentence: notSetUp },
      { failure: 'an abort that the view did not start', prompt: browserError('AbortError'), sentence: notSetUp },
      { failure: 'a passkey already on the device', prompt: browserError('InvalidStateError'), sentence: notSetUp },
      {
        failure: 'a 422 for the passkey',
        answer: () => problem(422, { code: 'validation.failed', errors: { credentialJson: ['Not set up.'] } }),
        sentence: notSetUp,
      },
      { failure: 'no connection', answer: noConnection, sentence: 'Not set up: no connection. Try again.' },
      { failure: 'a 503', answer: () => problem(503), sentence: 'Not set up: something went wrong. Try again.' },
      { failure: 'a 429', answer: () => problem(429), sentence: 'Too many tries. Wait 1 minute, then try again.' },
    ])('says so for $failure above the button, keeps focus there and fetches new options', async ({
      prompt,
      answer,
      sentence,
    }) => {
      if (prompt !== undefined) {
        webAuthn.startRegistration.mockRejectedValueOnce(prompt);
      }

      if (answer !== undefined) {
        webAuthn.startRegistration.mockResolvedValueOnce(credential);
        answers[passkeyPath].push(answer);
      }

      await openViewE();

      const button = pressPasskey();
      await wait(0);

      expect(alert().textContent).toBe(sentence);
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(alert().compareDocumentPosition(button)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
      expect(document.title).toBe(`Error: ${title}`);
      // The prefetch, then new options for the next press, which opens the prompt at once with them.
      expect(sent(optionsPath)).toHaveLength(2);
      expect(screen.getByRole('link', { name: 'Use a password and authenticator app' })).toBeInTheDocument();
      expect(window.location.pathname).toBe('/setup');

      pressPasskey();
      expect(webAuthn.startRegistration).toHaveBeenLastCalledWith({ optionsJSON: creationOptions('challenge-2') });
    });

    it('says "Not set up: no connection" when the options for the press do not come', async () => {
      // The prefetch fails quietly, so the press asks again.
      answers[optionsPath].push(noConnection, noConnection);
      await openViewE();

      const button = pressPasskey();
      await wait(0);

      expect(webAuthn.startRegistration).not.toHaveBeenCalled();
      expect(alert().textContent).toBe('Not set up: no connection. Try again.');
      expect(button).toHaveFocus();
      expect(document.title).toBe(`Error: ${title}`);
    });

    it('clears the alert and the title\'s "Error: " at the next press', async () => {
      webAuthn.startRegistration.mockRejectedValueOnce(browserError('NotAllowedError'));
      await openViewE();
      pressPasskey();
      await wait(0);
      expect(document.title).toBe(`Error: ${title}`);
      webAuthn.startRegistration.mockReturnValueOnce(later<SimpleWebAuthn.RegistrationResponseJSON>().value);

      pressPasskey();

      expect(alert()).toBeEmptyDOMElement();
      expect(document.title).toBe(title);
    });

    it('takes "Error: " off the title when the password link leaves view E', async () => {
      webAuthn.startRegistration.mockRejectedValueOnce(browserError('NotAllowedError'));
      await openViewE();
      pressPasskey();
      await wait(0);
      expect(document.title).toBe(`Error: ${title}`);

      fireEvent.click(screen.getByRole('link', { name: 'Use a password and authenticator app' }));
      await wait(0);

      expect(window.location.pathname).toBe('/setup/password');
      expect(document.title).toBe(title);
    });
  });

  it('shows the busy label at 400 ms, not before, and sends nothing more for a second press while busy', async () => {
    const made = later<SimpleWebAuthn.RegistrationResponseJSON>();
    webAuthn.startRegistration.mockReturnValueOnce(made.value);
    const answer = later<Response>();
    answers[passkeyPath].push(() => answer.value);
    await openViewE();

    const button = pressPasskey();
    fireEvent.click(button);

    expect(button).toHaveAttribute('aria-disabled', 'true');
    await wait(399);
    expect(button).toHaveAccessibleName('Use a passkey (recommended)');
    await wait(1);
    expect(button).toHaveAccessibleName('Setting up your account…');

    made.give(credential);
    await wait(0);
    fireEvent.click(button);
    await wait(0);

    expect(button).toHaveFocus();
    expect(webAuthn.startRegistration).toHaveBeenCalledOnce();
    expect(sent(passkeyPath)).toHaveLength(1);
    expect(sent(optionsPath)).toHaveLength(1);
  });

  it.each([
    { call: 'the prefetch', options: [() => problem(401)], press: false },
    { call: 'the options for a press', options: [noConnection, () => problem(401)], press: true },
    { call: 'the passkey', passkey: [() => problem(401)], press: true },
  ])('shows the timed-out page, with focus on its h1, and drops setup, for a 401 from $call', async ({
    options = [],
    passkey = [],
    press,
  }) => {
    answers[optionsPath].push(...options);
    answers[passkeyPath].push(...passkey);
    webAuthn.startRegistration.mockResolvedValueOnce(credential);
    await openViewE();

    if (press) {
      pressPasskey();
      await wait(0);
    }

    expect(heading()).toHaveTextContent('Your setup session timed out');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent('Open the link from your email again.');
    expect(within(screen.getByRole('main')).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByTestId('setup-memory')).toBeEmptyDOMElement();
    expect(document.title).toBe(title);
  });

  it('cancels the prompt when view E goes, and then does nothing with it', async () => {
    const made = later<SimpleWebAuthn.RegistrationResponseJSON>();
    webAuthn.startRegistration.mockReturnValueOnce(made.value);
    await openViewE();
    pressPasskey();
    const cancelled = webAuthn.cancelCeremony.mock.calls.length;

    fireEvent.click(screen.getByRole('link', { name: 'Use a password and authenticator app' }));
    await wait(0);
    made.fail(browserError('AbortError'));
    await wait(0);

    expect(webAuthn.cancelCeremony).toHaveBeenCalledTimes(cancelled + 1);
    expect(window.location.pathname).toBe('/setup/password');
    expect(sent(passkeyPath)).toHaveLength(0);
    expect(sent(optionsPath)).toHaveLength(1);
    expect(document.title).toBe(title);
  });

  it('shows the notice for the passkey button with WebAuthn missing, and makes the password primary', async () => {
    webAuthn.supported = false;
    await openViewE();
    await wait(5000);

    const main = within(screen.getByRole('main'));
    expect(main.queryByRole('button')).not.toBeInTheDocument();
    expect(main.queryByRole('alert')).not.toBeInTheDocument();
    const notice = main.getByText('Open this link in your browser');
    expect(notice.tagName).toBe('P');
    expect(notice.nextElementSibling).toHaveTextContent(
      'Passkeys cannot be set up here. Go back to the email and open the link in your phone’s browser, such as '
        + 'Safari or Chrome.',
    );
    const link = main.getByRole('link', { name: 'Use a password and authenticator app' });
    expect(link).toHaveAttribute('class', buttonClass('primary'));
    expectInOrder(main.getByRole('heading', { level: 2 }), notice, main.getByText('or'), link);
    expect(main.queryByText(/^You’ll sign in with your face/)).not.toBeInTheDocument();
    expect(sent(optionsPath)).toHaveLength(0);
    expect(webAuthn.startRegistration).not.toHaveBeenCalled();
  });
});
