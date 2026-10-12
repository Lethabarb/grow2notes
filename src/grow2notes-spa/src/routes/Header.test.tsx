import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { onSessionEnded } from '../api/session.ts';
import { buttonClass } from '../components/buttonClass.ts';
import { pageName } from '../copy/pageNames.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { routes, type AppRoute } from './routes.tsx';

// jsdom does not navigate, so the full reload is a stand-in that counts its calls.
vi.mock('./fullPageLoad.ts');

// A made-up worker and manager (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const noteAddress = '/participants/0c4f2a9e-6b13-4d7a-8e52-71f9b3d0a6c8/notes/2026-09-30';

// Made-up screens at addresses from each row of component 2b's table that the app does not route yet.
const madeUp: AppRoute[] = [
  { path: 'flagged', handle: { pageName: pageName.flaggedNotes } },
  { path: 'reports/daily/:date', handle: { pageName: pageName.dailyReport } },
  { path: 'manage/users', handle: { pageName: pageName.users } },
  { path: 'participants/:participantId/notes/:noteDate', handle: { pageName: pageName.note } },
];

const fetchMock = vi.fn<typeof fetch>();

/** `GET /api/auth/me`'s answer for `me`. */
function meAnswer(me: Me): Response {
  return new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } });
}

/** Stubs `fetch` as the server, which answers `GET /api/auth/me` with `me()` each time it is asked. */
function serveMe(me: () => Me) {
  fetchMock.mockImplementation(() => Promise.resolve(meAnswer(me())));
}

const logoutPath = '/api/auth/logout';
const antiforgeryToken = 'made-up-token';

/** How the server meets a `POST /api/auth/logout`: an answer, or a failure as `fetch` has one. */
type Logout = (init?: RequestInit) => Promise<Response>;

/**
 * Stubs `fetch` as the server: `GET /api/auth/me` answers `me`, the antiforgery endpoint gives a token, and each
 * `POST /api/auth/logout` meets the next of `logouts`, in turn.
 */
function serveSignOut(me: Me, ...logouts: Logout[]) {
  fetchMock.mockImplementation((path, init) => {
    if (path === '/api/auth/antiforgery') {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': antiforgeryToken } }));
    }

    if (path === logoutPath) {
      const logout = logouts.shift();
      if (logout === undefined) {
        throw new Error('The test gave no answer for this logout.');
      }

      return logout(init);
    }

    return Promise.resolve(meAnswer(me));
  });
}

/** A logout answered with `status`: a `204` with no body, or any other as a problem (design.md §6.1). */
function answer(status: number): Logout {
  const problem = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(
      status === 204
        ? new Response(null, { status })
        : new Response(problem, { status, headers: { 'Content-Type': 'application/problem+json' } }),
    );
}

/** A logout with no connection, which `fetch` rejects. */
const offline: Logout = () => Promise.reject(new TypeError('Failed to fetch'));

/** A logout that the server never answers: `fetch` rejects only when its signal aborts, with the signal's reason. */
const noAnswer: Logout = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

/** Each logout sent, as its method and the antiforgery token it carried. */
function logoutsSent() {
  return fetchMock.mock.calls
    .filter(([path]) => path === logoutPath)
    .map(([, init]) => ({ method: init?.method, token: new Headers(init?.headers).get('X-XSRF-TOKEN') }));
}

const removers: (() => void)[] = [];

/** A listener for the session-ended event, removed after the test. */
function listenForSessionEnded() {
  const listener = vi.fn();
  removers.push(onSessionEnded(listener));
  return listener;
}

/** Renders the app with the made-up screens at `path` on a memory router, under a query client of its own. */
function renderShell(path: string) {
  const client = createQueryClient();
  const table = routes.map((route) =>
    route.index ? route : { ...route, children: [...(route.children ?? []), ...madeUp] },
  );
  const router = createMemoryRouter(table, { initialEntries: [path] });
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { client, router };
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

/**
 * The header's links and buttons that Tab reaches, in DOM order, which is the order of Tab, as nothing in the header
 * has a positive `tabindex`: those in the panel only while it is open.
 */
function reachableTargets(): Element[] {
  const targets = screen.getByRole('banner').querySelectorAll('a[href], button');
  return Array.from(targets).filter((target) => target.closest('[hidden]') === null);
}

/** The header's links and buttons that Tab reaches, in DOM order, each as its role and text. */
function headerTargets(): string[] {
  return reachableTargets().map((target) => `${target.localName === 'a' ? 'link' : 'button'} ${target.textContent}`);
}

/** The Account panel, which has no role to find it by. */
function accountPanel(): HTMLElement {
  const panel = document.getElementById('account-panel');
  if (panel === null) {
    throw new Error('There is no #account-panel.');
  }

  return panel;
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  for (const remove of removers.splice(0)) {
    remove();
  }
  fetchMock.mockReset();
  vi.mocked(fullPageLoad).mockReset();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Header', () => {
  // Exactly these, in DOM order, which is the order on screen: with the panel closed, the only button is Account, so
  // there is no menu button.
  it.each([
    {
      me: worker,
      targets: ['link Grow2Notes', 'link Today', 'button Account'],
      addresses: ['/'],
    },
    {
      me: manager,
      targets: ['link Grow2Notes', 'button Account', 'link Today', 'link Flagged', 'link Report', 'link Manage'],
      addresses: ['/', '/flagged', '/reports/daily/2026-10-01', '/manage'],
    },
  ])("gives a $me.role their role's items in order, the nav's in a list", async ({ me, targets, addresses }) => {
    serveMe(() => me);
    renderShell('/');
    await settle();

    expect(headerTargets()).toEqual(targets);
    const items = within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('listitem');
    expect(items.map((item) => within(item).getByRole('link').getAttribute('href'))).toEqual(addresses);
  });

  // Component 2b's table, with every marked element in the header found, so that a second would fail. At
  // /manage/no-such-page the table would mark Manage, but Page not found shows there.
  it.each([
    { me: manager, path: '/', current: 'Today page' },
    { me: manager, path: '/flagged', current: 'Flagged page' },
    { me: manager, path: '/flagged?view=reviewed', current: 'Flagged page' },
    { me: manager, path: '/reports/daily/2026-10-01', current: 'Report page' },
    { me: manager, path: '/reports/daily/2026-09-14', current: 'Report page' },
    { me: manager, path: '/manage', current: 'Manage page' },
    { me: manager, path: '/manage/users', current: 'Manage true' },
    { me: manager, path: noteAddress, current: 'none' },
    { me: manager, path: '/no-such-page', current: 'none' },
    { me: manager, path: '/manage/no-such-page', current: 'none' },
    { me: worker, path: '/', current: 'Today page' },
    { me: worker, path: noteAddress, current: 'none' },
  ])('at $path, marks $current for a $me.role', async ({ me, path, current }) => {
    serveMe(() => me);
    renderShell(path);
    await settle();

    expect(currentItems()).toEqual(current === 'none' ? [] : [current]);
  });

  it('moves the Report link to the new today once /me is fetched again, with no reload', async () => {
    let today = '2026-10-01';
    serveMe(() => ({ ...manager, today }));
    const { client, router } = renderShell('/');
    await settle();
    const report = screen.getByRole('link', { name: 'Report' });
    const { location } = router.state;

    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-01');

    // Past midnight in Melbourne, /me is asked again, as refreshMe() will be on a screen change or window focus.
    today = '2026-10-02';
    await act(() => client.fetchQuery({ ...meQuery, staleTime: 0 }));
    await settle();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    // The same element on the same location: the header was neither reloaded nor mounted again.
    expect(screen.getByRole('link', { name: 'Report' })).toBe(report);
    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-02');
    expect(router.state.location).toBe(location);
  });
});

describe('the Account button and panel', () => {
  /** Renders the shell at `path` for `me`, and gives the Account button once /me has answered. */
  async function renderAccount(me: Me, path = '/') {
    serveMe(() => me);
    const { router } = renderShell(path);
    await settle();
    return { router, account: screen.getByRole('button', { name: 'Account' }) };
  }

  it.each([worker, manager])("toggles its panel, straight after Account's row, for a $role", async (me) => {
    const { account } = await renderAccount(me);
    const panel = accountPanel();

    expect(account).toHaveAttribute('aria-controls', panel.id);
    expect(panel.previousElementSibling).toContainElement(account);
    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(panel).toHaveAttribute('hidden');

    fireEvent.click(account);
    expect(account).toHaveAttribute('aria-expanded', 'true');
    expect(panel).not.toHaveAttribute('hidden');
    expect(panel).toBeVisible();

    fireEvent.click(account);
    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(panel).toHaveAttribute('hidden');
  });

  it.each([
    { me: worker, text: 'Signed in as Alex Pham' },
    { me: manager, text: 'Signed in as Sam Lee' },
  ])('names the display name from /me: $text', async ({ me, text }) => {
    const { account } = await renderAccount(me);

    fireEvent.click(account);

    expect(within(accountPanel()).getByText(text)).toBeVisible();
  });

  // The worker's Today is before Account, in row 1, Sign out in the panel, and the manager's Report after the panel, in
  // row 2.
  it.each([
    { me: worker, target: { role: 'button', name: 'Account' } },
    { me: worker, target: { role: 'link', name: 'Today' } },
    { me: worker, target: { role: 'button', name: 'Sign out' } },
    { me: manager, target: { role: 'button', name: 'Account' } },
    { me: manager, target: { role: 'button', name: 'Sign out' } },
    { me: manager, target: { role: 'link', name: 'Report' } },
  ] as const)('closes on Escape on $target.name for a $me.role, and focuses Account', async ({ me, target }) => {
    const { account } = await renderAccount(me);
    fireEvent.click(account);
    const element = screen.getByRole(target.role, { name: target.name });
    element.focus();

    fireEvent.keyDown(element, { key: 'Escape' });

    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(accountPanel()).toHaveAttribute('hidden');
    expect(account).toHaveFocus();
  });

  // Today at / goes nowhere new, but is still a location change.
  it.each([
    { me: manager, link: 'Flagged', path: '/flagged' },
    { me: worker, link: 'Today', path: '/' },
  ])('closes when a $me.role follows $link', async ({ me, link, path }) => {
    const { router, account } = await renderAccount(me);
    const { key } = router.state.location;
    fireEvent.click(account);

    fireEvent.click(screen.getByRole('link', { name: link }));
    await settle();

    expect(router.state.location).toMatchObject({ pathname: path });
    expect(router.state.location.key).not.toBe(key);
    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(accountPanel()).toHaveAttribute('hidden');
  });

  it('stays closed on going back to where it was opened', async () => {
    const { router, account } = await renderAccount(manager);
    fireEvent.click(account);
    fireEvent.click(screen.getByRole('link', { name: 'Flagged' }));
    await settle();

    await act(() => router.navigate(-1));

    expect(router.state.location).toMatchObject({ pathname: '/' });
    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(accountPanel()).toHaveAttribute('hidden');
  });

  it('is named "Account", with a decorative chevron, and never has disabled', async () => {
    const { account } = await renderAccount(manager);
    const chevron = account.querySelector('svg');

    expect(account).toHaveAccessibleName('Account');
    expect(chevron).toHaveAttribute('aria-hidden', 'true');
    expect(chevron).toHaveAttribute('focusable', 'false');
    expect(chevron?.querySelector('path')).toHaveAttribute('stroke', 'currentColor');
    expect(account).not.toHaveAttribute('disabled');

    fireEvent.click(account);
    expect(account).not.toHaveAttribute('disabled');

    fireEvent.keyDown(account, { key: 'Escape' });
    expect(account).not.toHaveAttribute('disabled');
  });
});

describe('Sign out', () => {
  const noConnection = 'Not signed out: no connection. Try again.';
  const serverFailure = 'Not signed out: something went wrong. Try again.';

  /** Renders the shell for whoever the server answers, opens the Account panel, and focuses Sign out in it. */
  async function openPanel() {
    renderShell('/');
    await settle();
    fireEvent.click(screen.getByRole('button', { name: 'Account' }));
    const panel = accountPanel();
    const signOut = within(panel).getByRole('button', { name: 'Sign out' });
    signOut.focus();
    return { signOut, line: within(panel).getByRole('status') };
  }

  /** Whether the header is still signed in: the nav, and Account with its panel open. */
  function headerSignedIn(): boolean {
    return (
      screen.queryByRole('navigation', { name: 'Main' }) !== null
      && screen.queryByRole('button', { name: 'Account', expanded: true }) !== null
    );
  }

  it.each([
    { me: worker, signedInAs: 'Signed in as Alex Pham' },
    { me: manager, signedInAs: 'Signed in as Sam Lee' },
  ])('follows "$signedInAs", before an empty status line, and comes next after Account', async ({ me, signedInAs }) => {
    serveMe(() => me);
    renderShell('/');
    await settle();
    const panel = accountPanel();
    // In the page before the panel first opens, so that the words later written into it are announced.
    const line = panel.querySelector('[role="status"]');
    expect(line).toBeEmptyDOMElement();

    const account = screen.getByRole('button', { name: 'Account' });
    fireEvent.click(account);
    const signOut = within(panel).getByRole('button', { name: 'Sign out' });

    const contents = panel.querySelectorAll('p, button');
    expect(contents).toHaveLength(3);
    expect(contents[0].textContent).toBe(signedInAs);
    expect(contents[1]).toBe(signOut);
    expect(contents[2]).toBe(line);
    expect(within(panel).getByRole('status')).toBe(line);
    expect(line).toBeEmptyDOMElement();
    // The shared Button's secondary style and nothing of its own: the full width on a phone, as wide as its label from
    // 40rem (Button.module.css).
    expect(signOut).toHaveAttribute('class', buttonClass('secondary'));
    const targets = reachableTargets();
    expect(targets[targets.indexOf(account) + 1]).toBe(signOut);
  });

  it('sends one POST /api/auth/logout with the antiforgery token, and no dialog, and no more while busy', async () => {
    serveSignOut(worker, noAnswer);
    const confirm = vi.spyOn(window, 'confirm');
    const { signOut } = await openPanel();

    fireEvent.click(signOut);
    await settle();
    fireEvent.click(signOut);
    fireEvent.click(signOut);
    await act(() => vi.advanceTimersByTimeAsync(1_000));

    expect(logoutsSent()).toEqual([{ method: 'POST', token: antiforgeryToken }]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(confirm).not.toHaveBeenCalled();
  });

  it('says "Signing out…" from 400 ms, aria-disabled but never disabled, and keeps focus', async () => {
    serveSignOut(manager, noAnswer);
    const { signOut } = await openPanel();
    const busyLabel = within(signOut).getByText('Signing out…');

    fireEvent.click(signOut);
    await act(() => vi.advanceTimersByTimeAsync(399));

    expect(signOut).toHaveAccessibleName('Sign out');
    expect(busyLabel).not.toBeVisible();
    expect(signOut).toHaveAttribute('aria-disabled', 'true');
    expect(signOut).not.toHaveAttribute('disabled');
    expect(signOut).toHaveFocus();

    await act(() => vi.advanceTimersByTimeAsync(1));

    expect(signOut).toHaveAccessibleName('Signing out…');
    expect(busyLabel).toBeVisible();
    expect(signOut).toHaveAttribute('aria-disabled', 'true');
    expect(signOut).not.toHaveAttribute('disabled');
    expect(signOut).toHaveFocus();
  });

  // A 401 is a sign-out that counts as done: there was no session left to end. The checks come once React has
  // rendered, as the mocked page never goes: what they find is what the person has while the new page loads.
  it.each([204, 401])('loads / afresh once after a %i, busy and signed in until the page goes', async (status) => {
    serveSignOut(worker, answer(status));
    const ended = listenForSessionEnded();
    const { signOut } = await openPanel();

    fireEvent.click(signOut);
    await settle();
    await act(() => vi.advanceTimersByTimeAsync(10_000));

    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(signOut).toHaveAttribute('aria-disabled', 'true');
    expect(headerSignedIn()).toBe(true);
    expect(ended).not.toHaveBeenCalled();

    fireEvent.click(signOut);
    await settle();

    expect(logoutsSent()).toHaveLength(1);
    expect(fullPageLoad).toHaveBeenCalledOnce();
  });

  it.each([
    { failure: 'a network failure', logout: offline, wait: 0, sentence: noConnection },
    { failure: 'no answer in 10 seconds', logout: noAnswer, wait: 10_000, sentence: noConnection },
    { failure: 'a 503', logout: answer(503), wait: 0, sentence: serverFailure },
  ])('after $failure, says "$sentence", with Sign out ready and focused', async ({ logout, wait, sentence }) => {
    serveSignOut(manager, logout);
    const { signOut, line } = await openPanel();

    fireEvent.click(signOut);
    await act(() => vi.advanceTimersByTimeAsync(wait));

    expect(line.textContent).toBe(sentence);
    expect(signOut).not.toHaveAttribute('aria-disabled');
    expect(signOut).toHaveAccessibleName('Sign out');
    expect(signOut).toHaveFocus();
    expect(fullPageLoad).not.toHaveBeenCalled();
    expect(headerSignedIn()).toBe(true);
  });

  it('empties the status line when pressed again, before the request, and says the new failure', async () => {
    const linesWhenSent: (string | null)[] = [];
    /** `logout`, noting the status line's words when the request is sent. */
    function noting(logout: Logout): Logout {
      return (init) => {
        linesWhenSent.push(within(accountPanel()).getByRole('status').textContent);
        return logout(init);
      };
    }
    serveSignOut(worker, noting(answer(503)), noting(offline));
    const { signOut, line } = await openPanel();
    fireEvent.click(signOut);
    await settle();
    expect(line.textContent).toBe(serverFailure);

    fireEvent.click(signOut);

    expect(line).toBeEmptyDOMElement();
    await settle();
    expect(linesWhenSent).toEqual(['', '']);
    expect(line.textContent).toBe(noConnection);
  });
});
