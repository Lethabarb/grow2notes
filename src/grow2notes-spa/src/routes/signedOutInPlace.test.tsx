import type * as SimpleWebAuthn from '@simplewebauthn/browser';
import { startAuthentication, type AuthenticationResponseJSON } from '@simplewebauthn/browser';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { getPhase } from '../api/session.ts';
import type { AdminUser } from '../features/users/usersQuery.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { rearmUnloadGuard, unloadGuardArmed } from './leaveGuard.ts';
import { routes } from './routes.tsx';

// The signed-out-in-place view, sign-in's view C (S00.04.08; sign-in.md *Sign in again, in place*; app-shell.md
// component 7 and its Session criteria; session-timeout.md *The signed-out state*), in the whole app, on a memory
// router.

// The browser's passkey prompt, which jsdom has not got; the rest of SimpleWebAuthn is its own.
vi.mock('@simplewebauthn/browser', async (importOriginal) => ({
  ...(await importOriginal<typeof SimpleWebAuthn>()),
  startAuthentication: vi.fn(),
}));

// jsdom does not navigate, so the reload for another person is a mock that the tests read.
vi.mock('./fullPageLoad.ts');

const ceremony = vi.mocked(startAuthentication);

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const optionsPath = '/api/auth/passkey/options';
const passkeyPath = '/api/auth/passkey';
const passwordPath = '/api/auth/login';
const codePath = '/api/auth/login/totp';
const logoutPath = '/api/auth/logout';
const usersPath = '/api/admin/users';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';

// users.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts, and Alex
// Park, Invited.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
// A made-up manager of the same organisation (A35), who signs in on the same laptop.
const someoneElse: Me = {
  userId: '0c1d7e52-93a8-4b6f-a2d4-58e1f9c3b706',
  displayName: 'Riley Ng',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const alex: AdminUser = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  email: 'alex.park@example.org',
  displayName: 'Alex Park',
  role: 'Worker',
  status: 'Invited',
  invitedAtUtc: '2026-09-01T00:00:00Z',
  activatedAtUtc: null,
  deactivatedAtUtc: null,
  concurrencyStamp: 'stamp-alex',
};
const setupLinkPath = `${usersPath}/${alex.id}/setup-link`;

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

const signedOutHeading = 'You’ve been signed out';
const signedOutSentence = 'Sign in again to go back to where you were.';
const hint = 'Sign in the way you chose when you set up your account.';
const help = 'If you cannot sign in, ask a manager to reset your sign-in.';
const codeLabel = 'Enter the 6-digit code from your authenticator app';
const failedMessage =
  'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** An answer held until the test gives it with `release`. */
function held(answer: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const given = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: async (init) => given.then(() => answer(init)), release };
}

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each method and path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(method: 'GET' | 'POST', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** How many `method` requests have been sent to `path`. */
function sent(method: 'GET' | 'POST', path: string): number {
  return fetchMock.mock.calls.filter(([sentPath, init]) => sentPath === path && init?.method === method).length;
}

/** A browser with WebAuthn, which jsdom is not: `browserSupportsWebAuthn()` asks only for the constructor. */
function withWebAuthn() {
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
}

/**
 * Renders the app at `path` on a memory router, under a query client of its own and StrictMode, as `main.tsx` renders
 * it, for `me`, whose session the start-up's `/api/auth/me` finds, or for nobody, and lets the screen show.
 */
async function renderApp(path: string, me: Me | 'nobody' = manager) {
  serve('GET', mePath, me === 'nobody' ? problem(401) : json(me));
  const client = createQueryClient();
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  await settle();
  return { client, router };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets each request answer and TanStack hand on its new state, on a `setTimeout` of 0, until the page has it. */
async function settle() {
  await wait(1);
  await wait(1);
}

const main = () => screen.getByRole('main');
/** The one `<h1>` shown, which the accessibility tree has: view C's while it shows, as the screen's is hidden. */
const shownHeading = () => within(main()).getByRole('heading', { level: 1 });
const nameField = () => screen.getByRole('textbox', { name: 'Name' });
const emailField = () => screen.getByRole('textbox', { name: 'Email address' });

/** The screen, hidden in `<main>` under view C, or `null` while none is. */
function hiddenScreen(): HTMLElement | null {
  return main().querySelector<HTMLElement>(':scope > [hidden]');
}

/** The window regains focus, as the browser says so, which refreshes `/api/auth/me` (app-shell.md *Refresh*). */
function focusWindow() {
  act(() => {
    window.dispatchEvent(new FocusEvent('focus'));
  });
}

/** Focuses `button` and presses it, as a person does. */
function press(button: HTMLElement) {
  button.focus();
  fireEvent.click(button);
}

/** Puts `value` in `field`, as typing or pasting it does. */
function type(field: HTMLElement, value: string) {
  fireEvent.change(field, { target: { value } });
}

/** Ends the session over the screen, as the window's refresh of `/api/auth/me` finds it gone. */
async function endTheSession() {
  serve('GET', mePath, problem(401));
  focusWindow();
  await settle();
}

/** Signs in again in view C with an email, a password and a code, the server answering the code with `me`. */
async function signInWithPassword(me: Me = manager) {
  serve('POST', codePath, json(me));
  type(emailField(), 'sam.lee@example.org');
  type(screen.getByLabelText('Password'), 'correct horse battery staple');
  press(screen.getByRole('button', { name: 'Continue' }));
  await settle();
  type(screen.getByRole('textbox', { name: codeLabel }), '123456');
  press(screen.getByRole('button', { name: 'Sign in' }));
  await settle();
}

/** Fills Invite user half way: a name and an address, with Manager chosen. */
function fillInviteHalfWay() {
  type(nameField(), 'Jo Smith');
  type(emailField(), 'jo.smith@example.org');
  fireEvent.click(screen.getByRole('radio', { name: 'Manager' }));
}

/** Expects Invite user's fields, shown or hidden, to hold what `fillInviteHalfWay` typed. */
function expectInviteKept() {
  const form = within(main());
  expect(form.getByRole('textbox', { name: 'Name', hidden: true })).toHaveValue('Jo Smith');
  expect(form.getAllByRole('textbox', { name: 'Email address', hidden: true }).at(-1)).toHaveValue(
    'jo.smith@example.org',
  );
  expect(form.getByRole('radio', { name: 'Manager', hidden: true })).toBeChecked();
}

/** Fails unless the elements come in the document in the order given. */
function expectInOrder(...elements: Element[]) {
  for (const [index, element] of elements.slice(1).entries()) {
    expect(elements[index].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

/** The elements that Tab reaches, in order: focusable, not hidden and not taken out of the order. */
function tabStops(): Element[] {
  const focusable = document.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]');
  return Array.from(focusable).filter((element) => element.closest('[hidden]') === null && element.tabIndex >= 0);
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string, with its method.
  fetchMock.mockImplementation((path, init) => {
    const request = `${init?.method} ${path as string}`;
    const answer = answers.get(request);
    if (answer === undefined) {
      throw new Error(`The test gave no answer for ${request}.`);
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  serve('GET', antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  serve('POST', optionsPath, json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
  serve('POST', passwordPath, json({ next: 'totp' }));
  serve('GET', usersPath, json([alex]));
  serve('GET', todayPath, json({ date: manager.today, participants: [] }));
  serve('GET', draftsPath, json([]));
  ceremony.mockResolvedValue(credential);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  ceremony.mockReset();
  vi.mocked(fullPageLoad).mockReset();
  rearmUnloadGuard();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the signed-out-in-place view', () => {
  it('shows over a half-filled Invite user for a 401, at the same address, the form hidden as it was', async () => {
    withWebAuthn();
    const { router } = await renderApp('/manage/users/new');
    fillInviteHalfWay();
    nameField().focus();
    const inviteHeading = shownHeading();
    const { location } = router.state;

    await endTheSession();

    // View C first in <main>, then the screen, hidden but mounted: the same <h1>, with the typed values.
    const heading = shownHeading();
    expect(heading).toHaveAccessibleName(signedOutHeading);
    expect(heading).toHaveFocus();
    expect(heading).toHaveAttribute('tabindex', '-1');
    const view = within(main());
    expectInOrder(
      heading,
      view.getByText(signedOutSentence),
      view.getByText(hint),
      view.getByRole('button', { name: 'Sign in with a passkey' }),
      view.getByText('or'),
      emailField(),
      screen.getByLabelText('Password'),
      view.getByRole('button', { name: 'Continue' }),
      view.getByText(help),
    );
    expect(view.queryByRole('heading', { name: 'Sign in' })).not.toBeInTheDocument();
    const form = hiddenScreen();
    expect(main().lastElementChild).toBe(form);
    expect(form).toContainElement(inviteHeading);
    expectInviteKept();
    // Neither in the accessibility tree nor reached by Tab.
    expect(screen.queryByRole('textbox', { name: 'Name' })).not.toBeInTheDocument();
    expect(tabStops().filter((stop) => form?.contains(stop))).toEqual([]);

    // The signed-out frame, with no nav, Account or warning, and the bar slot hidden with the back link in it.
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).not.toHaveAttribute('href');
    expect(document.getElementById('page-bar')).toHaveAttribute('hidden');
    expect(screen.queryByRole('link', { name: 'Users' })).not.toBeInTheDocument();
    expect(document.querySelector('dialog')).not.toHaveAttribute('open');
    expect(document.querySelectorAll('main')).toHaveLength(1);

    expect(document.title).toBe('Grow2Notes – Sign in');
    expect(router.state.location).toBe(location);
    // No two elements share an id, the hidden form's email field and view C's among them.
    const ids = Array.from(document.querySelectorAll('[id]'), (element) => element.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("signs the same person in again to the form as it was, focused on its <h1>, and sends none of it", async () => {
    const { router } = await renderApp('/manage/users/new');
    fillInviteHalfWay();
    const inviteHeading = shownHeading();
    const { location } = router.state;
    await endTheSession();

    await signInWithPassword();

    expect(hiddenScreen()).toBeNull();
    expect(shownHeading()).toBe(inviteHeading);
    expect(inviteHeading).toHaveFocus();
    expect(nameField()).toHaveValue('Jo Smith');
    expectInviteKept();
    expect(document.title).toBe('Grow2Notes – Invite user');
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Account' })).toBeInTheDocument();
    expect(document.getElementById('page-bar')).not.toHaveAttribute('hidden');
    expect(screen.getByRole('link', { name: 'Users' })).toBeVisible();
    expect(router.state.location).toBe(location);
    expect(sent('POST', usersPath)).toBe(0);
  });

  it('signs in again with a passkey too, in a browser with WebAuthn', async () => {
    withWebAuthn();
    await renderApp('/manage/users/new');
    fillInviteHalfWay();
    await endTheSession();

    serve('POST', passkeyPath, json(manager));
    press(screen.getByRole('button', { name: 'Sign in with a passkey' }));
    await settle();

    expect(ceremony).toHaveBeenCalledOnce();
    expect(shownHeading()).toHaveAccessibleName('Invite user');
    expect(shownHeading()).toHaveFocus();
    expectInviteKept();
  });

  it('gives the form back its "Error: " title after signing in again over its error summary', async () => {
    await renderApp('/manage/users/new');
    press(screen.getByRole('button', { name: 'Send invite' }));
    await settle();
    expect(document.title).toBe('Error: Grow2Notes – Invite user');

    await endTheSession();
    expect(document.title).toBe('Grow2Notes – Sign in');
    await signInWithPassword();

    expect(document.title).toBe('Error: Grow2Notes – Invite user');
  });

  it('shows over a Send invite that met the 401, which is not sent again after signing in', async () => {
    await renderApp('/manage/users/new');
    fillInviteHalfWay();
    serve('POST', usersPath, problem(401));
    press(screen.getByRole('button', { name: 'Send invite' }));
    await settle();

    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(shownHeading()).toHaveFocus();
    await signInWithPassword();
    await wait(60_000);

    expect(sent('POST', usersPath)).toBe(1);
    expect(shownHeading()).toHaveAccessibleName('Invite user');
    expectInviteKept();
    expect(within(main()).getByRole('alert')).toBeEmptyDOMElement();
    expect(screen.getByRole('button', { name: 'Send invite' })).not.toHaveAttribute('aria-disabled');
  });

  it("hides a user's back link from sight and Tab, and after a 401 Resend invite refetches the users", async () => {
    await renderApp(`/manage/users/${alex.id}`);
    serve('POST', setupLinkPath, problem(401));
    press(screen.getByRole('button', { name: 'Resend invite' }));
    await settle();
    const listed = sent('GET', usersPath);

    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    const pageBar = document.getElementById('page-bar');
    if (pageBar === null) {
      throw new Error('The shell rendered no #page-bar.');
    }

    expect(pageBar).toHaveAttribute('hidden');
    const backLink = within(pageBar).getByRole('link', { name: 'Users', hidden: true });
    expect(backLink).toHaveAttribute('href', '/manage/users');
    expect(screen.queryByRole('link', { name: 'Users' })).not.toBeInTheDocument();
    expect(tabStops()).not.toContain(backLink);

    await signInWithPassword();
    await settle();

    // Back as it was, its users fetched again once for the new session, and the resend not sent again.
    expect(shownHeading()).toHaveAccessibleName('Alex Park');
    expect(shownHeading()).toHaveFocus();
    expect(screen.getByRole('link', { name: 'Users' })).toBe(backLink);
    expect(tabStops()).toContain(backLink);
    expect(sent('GET', usersPath)).toBe(listed + 1);
    expect(sent('POST', setupLinkPath)).toBe(1);
    expect(within(main()).getByRole('alert')).toBeEmptyDOMElement();
    expect(screen.getByRole('button', { name: 'Resend invite' }).nextElementSibling).toBeEmptyDOMElement();
  });

  it("focuses a user's name as it loads, after signing in again over their page, whose list met the 401", async () => {
    serve('GET', usersPath, problem(401));
    await renderApp(`/manage/users/${alex.id}`);
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    // The name is data, so the hidden page has no <h1> yet for the focus that signing in again gives it.
    expect(hiddenScreen()?.querySelector('h1')).toBeNull();

    serve('GET', usersPath, json([alex]));
    await signInWithPassword();
    await settle();

    expect(shownHeading()).toHaveAccessibleName('Alex Park');
    expect(shownHeading()).toHaveFocus();
  });

  it('closes for a refresh that finds the session alive, kept by another tab, with nothing else fetched', async () => {
    await renderApp(`/manage/users/${alex.id}`);
    const userHeading = shownHeading();
    serve('POST', setupLinkPath, problem(401));
    press(screen.getByRole('button', { name: 'Resend invite' }));
    await settle();
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    const requests = fetchMock.mock.calls.length;

    serve('GET', mePath, json(manager));
    focusWindow();
    await settle();

    expect(shownHeading()).toBe(userHeading);
    expect(userHeading).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – User');
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.slice(requests).map(([path]) => path)).toEqual([mePath]);
  });

  it.each([
    { way: 'a sign-in in it', answer: () => signInWithPassword(someoneElse) },
    {
      way: 'a refresh, after a sign-in in another tab',
      answer: async () => {
        serve('GET', mePath, json(someoneElse));
        focusWindow();
        await settle();
      },
    },
  ])('loads the page afresh for another person, from $way, with the form still hidden', async ({ answer }) => {
    // Whether a page's beforeunload warning would still ask, as the reload starts.
    const armedAtReload: boolean[] = [];
    vi.mocked(fullPageLoad).mockImplementation(() => {
      armedAtReload.push(unloadGuardArmed());
    });
    await renderApp('/manage/users/new');
    fillInviteHalfWay();
    await endTheSession();

    await answer();
    await wait(60_000);

    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(armedAtReload).toEqual([false]);
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(hiddenScreen()).not.toBeNull();
    expect(screen.queryByText('Signed in as Riley Ng')).not.toBeInTheDocument();
    expect(sent('POST', usersPath)).toBe(0);
  });

  it('stays up for a wrong password, with the Sign-in failed summary focused', async () => {
    await renderApp('/manage/users/new');
    await endTheSession();
    serve('POST', passwordPath, problem(401));

    type(emailField(), 'sam.lee@example.org');
    type(screen.getByLabelText('Password'), 'wrong');
    press(screen.getByRole('button', { name: 'Continue' }));
    await settle();

    const summary = screen.getByRole('heading', { level: 2, name: 'There is a problem' }).parentElement;
    expect(summary).toHaveFocus();
    expect(summary).toHaveTextContent(failedMessage);
    expectInOrder(summary ?? main(), shownHeading());
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(hiddenScreen()).not.toBeNull();
    expect(document.title).toBe('Error: Grow2Notes – Sign in');
  });

  it('keeps its <h1> over the code step, whose label is an <h2>, its field focused', async () => {
    await renderApp('/manage/users/new');
    await endTheSession();

    type(emailField(), 'sam.lee@example.org');
    type(screen.getByLabelText('Password'), 'correct horse battery staple');
    press(screen.getByRole('button', { name: 'Continue' }));
    await settle();

    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(within(main()).getByText(signedOutSentence)).toBeInTheDocument();
    const codeHeading = within(main()).getByRole('heading', { level: 2, name: codeLabel });
    const codeField = screen.getByRole('textbox', { name: codeLabel });
    expect(codeHeading).toContainElement(within(codeHeading).getByText(codeLabel, { selector: 'label' }));
    expect(codeField).toHaveFocus();
    expect(document.querySelectorAll('h1:not([hidden] h1)')).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Sign in');
  });

  it('shows at 30:00 idle as the warning closes, focused on its <h1>, and goes for a refresh that is 200', async () => {
    await renderApp('/');
    const today = shownHeading();
    const dialog = document.querySelector('dialog');

    await wait(28 * 60_000);
    expect(dialog).toHaveAttribute('open');
    expect(hiddenScreen()).toBeNull();
    await wait(2 * 60_000);

    expect(dialog).not.toHaveAttribute('open');
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(shownHeading()).toHaveFocus();
    expect(hiddenScreen()).toContainElement(today);
    expect(document.title).toBe('Grow2Notes – Sign in');
    const requests = fetchMock.mock.calls.length;

    // Another tab kept the session alive: the window's refresh finds it, and the screen comes back as it was. The
    // hidden Today fetches its lists again with it, in any order (today.md *Refresh*).
    focusWindow();
    await settle();

    expect(fetchMock.mock.calls.slice(requests).map(([path]) => path).sort()).toEqual(
      [mePath, todayPath, draftsPath].sort(),
    );
    expect(shownHeading()).toBe(today);
    expect(today).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Today');
    expect(getPhase()).toBe('active');
  });

  // The server's 30 minutes start as it takes a request, after the send from which the clock counts, so a request with
  // the session cookie at 30:00 could renew the session under "You've been signed out" (A61). Here every answer comes
  // at once, so the session has surely ended 5 seconds after the clock's 30:00.
  it('asks for passkey options at 30:00 idle only once the server has surely ended the session', async () => {
    withWebAuthn();
    await renderApp('/');
    const requests = fetchMock.mock.calls.length;

    await wait(30 * 60_000);
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    await wait(4_900);
    expect(fetchMock.mock.calls.slice(requests)).toEqual([]);
    await wait(200);

    expect(sent('POST', optionsPath)).toBe(1);
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
  });

  it('asks for passkey options at a press before then, and not again once the wait is over', async () => {
    withWebAuthn();
    await renderApp('/');
    await wait(30 * 60_000);
    // The prompt stays open, as while a person chooses their passkey, whose ceremony new options would end.
    ceremony.mockReturnValue(new Promise<AuthenticationResponseJSON>(() => {}));

    press(screen.getByRole('button', { name: 'Sign in with a passkey' }));
    await settle();
    expect(sent('POST', optionsPath)).toBe(1);
    expect(ceremony).toHaveBeenCalledOnce();
    await wait(60_000);

    expect(sent('POST', optionsPath)).toBe(1);
  });

  it('asks for passkey options at once over a 401, which says that the session has ended', async () => {
    withWebAuthn();
    await renderApp('/manage/users/new');

    await endTheSession();

    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);
    expect(sent('POST', optionsPath)).toBe(1);
  });

  it('stays closed for a 401 to a request sent before the newest sign-in, which lands after it', async () => {
    await renderApp(`/manage/users/${alex.id}`);
    serve('POST', setupLinkPath, problem(401));
    press(screen.getByRole('button', { name: 'Resend invite' }));
    await settle();
    expect(shownHeading()).toHaveAccessibleName(signedOutHeading);

    // The page becomes visible again: its refresh of /me finds no session, and the hidden user page's list is fetched
    // with the cookie from before, its answer held until after the sign-in.
    serve('GET', mePath, problem(401));
    const stale = held(problem(401));
    serve('GET', usersPath, stale.answer);
    const listed = sent('GET', usersPath);
    act(() => {
      document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
    });
    await settle();
    expect(sent('GET', usersPath)).toBe(listed + 1);
    await signInWithPassword();
    serve('GET', usersPath, json([alex]));
    stale.release();
    await settle();
    await settle();

    expect(getPhase()).toBe('active');
    expect(shownHeading()).toHaveAccessibleName('Alex Park');
    expect(hiddenScreen()).toBeNull();
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    // The list fetched once more for the new session, after the one that went before it.
    expect(sent('GET', usersPath)).toBe(listed + 2);
  });
});

describe('nothing in place', () => {
  it("shows for a 401 over sign-in's view A", async () => {
    await renderApp('/manage', 'nobody');

    focusWindow();
    await settle();

    expect(shownHeading()).toHaveAccessibleName('Sign in');
    expect(screen.queryByText(signedOutHeading)).not.toBeInTheDocument();
  });

  it('shows for a 401 over a setup page, for someone signed in', async () => {
    await renderApp('/setup/password');

    await endTheSession();

    expect(shownHeading()).toHaveAccessibleName('Set up your account');
    expect(screen.queryByText(signedOutHeading)).not.toBeInTheDocument();
  });

  it("shows for Sign out's 401, which counts as signed out, before the page loads afresh", async () => {
    await renderApp('/');
    serve('POST', logoutPath, problem(401));
    fireEvent.click(screen.getByRole('button', { name: 'Account' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    await settle();

    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(screen.queryByText(signedOutHeading)).not.toBeInTheDocument();
    expect(getPhase()).toBe('active');
  });
});
