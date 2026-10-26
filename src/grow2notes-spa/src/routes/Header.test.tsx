import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { refreshMe, type Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { onSessionEnded, registerSaveFlush } from '../api/session.ts';
import { buttonClass } from '../components/buttonClass.ts';
import { pageName } from '../copy/pageNames.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { rearmUnloadGuard, setLeaveGuard, unloadGuardArmed } from './leaveGuard.ts';
import { routes, type AppRoute } from './routes.tsx';

// jsdom does not navigate, so the full reload is a stand-in that counts its calls.
vi.mock('./fullPageLoad.ts');

// A made-up worker, manager and Support person (A35), in the JSON that MeEndpointTests asserts.
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

const support: Me = {
  userId: 'e7c2a94d-3b58-4f61-8d0a-5a1f9b6c2e34',
  displayName: 'Ivy Chen',
  role: 'Support',
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

const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';

/**
 * `GET /api/auth/me`'s answer for `me`, or, for Today's own requests, which it sends under the header, the list for
 * `me`'s day, with no participants, and no unfinished drafts.
 */
function meAnswer(me: Me, path: unknown): Response {
  let body: unknown = me;
  if (path === todayPath) {
    body = { date: me.today, participants: [] };
  } else if (path === draftsPath) {
    body = [];
  }

  return new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });
}

/** Stubs `fetch` as the server, which answers `GET /api/auth/me` with `me()` each time it is asked. */
function serveMe(me: () => Me) {
  fetchMock.mockImplementation((path) => Promise.resolve(meAnswer(me(), path)));
}

const mePath = '/api/auth/me';
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

    return Promise.resolve(meAnswer(me, path));
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

/** `logout`, with whether the unload guard was armed as each request that met it was sent. */
function notingArmed(logout: Logout) {
  const armedWhenSent: boolean[] = [];
  const noting: Logout = (init) => {
    armedWhenSent.push(unloadGuardArmed());
    return logout(init);
  };
  return { logout: noting, armedWhenSent };
}

/** Each logout sent, as its method and the antiforgery token it carried. */
function logoutsSent() {
  return fetchMock.mock.calls
    .filter(([path]) => path === logoutPath)
    .map(([, init]) => ({ method: init?.method, token: new Headers(init?.headers).get('X-XSRF-TOKEN') }));
}

/**
 * The address of each request sent but `GET /api/auth/me` and Today's lists, which the screen under the header asks
 * for: the antiforgery token's and the logout's.
 */
function signOutRequests() {
  return fetchMock.mock.calls
    .map(([path]) => path)
    .filter((path) => path !== mePath && path !== todayPath && path !== draftsPath);
}

/**
 * A stand-in for a page's leave dialog, registered as the leave guard, as Guide prompts registers its own: each time
 * it is asked, it waits for the test's answer.
 */
function standInGuard() {
  const waiting: ((leave: boolean) => void)[] = [];
  const ask = vi.fn(() => new Promise<boolean>((resolve) => waiting.push(resolve)));
  setLeaveGuard(ask);

  /** Answers the question asked first: `true` is Leave without saving, `false` Stay on this page. */
  async function answer(leave: boolean) {
    const resolve = waiting.shift();
    if (resolve === undefined) {
      throw new Error('The guard has not been asked.');
    }

    resolve(leave);
    await settle();
  }

  return { ask, answer };
}

const removers: (() => void)[] = [];

/**
 * A stand-in for a mounted note form's autosave with a change unsaved, registered as the form registers its own: each
 * time Sign out asks it to save the change, it waits for the test's answer, `true` once saved.
 */
function standInForm() {
  let unsaved = true;
  const waiting: ((saved: boolean) => void)[] = [];
  const flushNow = vi.fn(() => new Promise<boolean>((resolve) => waiting.push(resolve)));
  removers.push(registerSaveFlush({ unsaved: () => unsaved, flushNow }));

  /** Answers the flush asked first. */
  async function answer(saved: boolean) {
    const resolve = waiting.shift();
    if (resolve === undefined) {
      throw new Error('The form has not been asked.');
    }

    unsaved = !saved;
    resolve(saved);
    await settle();
  }

  return { flushNow, answer };
}

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
  setLeaveGuard(null);
  rearmUnloadGuard();
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

  it('gives Support the wordmark and Account only, with no navigation, and Sign out in the panel', async () => {
    serveMe(() => support);
    renderShell('/');
    await settle();

    expect(headerTargets()).toEqual(['link Grow2Notes', 'button Account']);
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Account' }));
    expect(within(accountPanel()).getByRole('button', { name: 'Sign out' })).toBeVisible();
    expect(headerTargets()).toEqual(['link Grow2Notes', 'button Account', 'button Sign out']);
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

    // Past midnight in Melbourne, /me is asked again, as the shell asks on a screen change or window focus.
    today = '2026-10-02';
    await act(() => refreshMe(client));
    await settle();

    expect(fetchMock.mock.calls.filter(([path]) => path === mePath)).toHaveLength(2);
    // The same element on the same location: the header was neither reloaded nor mounted again.
    expect(screen.getByRole('link', { name: 'Report' })).toBe(report);
    expect(report).toHaveAttribute('href', '/reports/daily/2026-10-02');
    expect(router.state.location).toBe(location);
  });
});

describe('the badge on the Flagged item', () => {
  /** The Flagged link in the main navigation, by its name, which is "Flagged" or "Flagged, N to review". */
  function flaggedLink(name: string): HTMLElement {
    return within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name });
  }

  it('names the link "Flagged, 3 to review", with the digit in a hidden pill and the words in hidden text', async () => {
    serveMe(() => ({ ...manager, toReviewCount: 3 }));
    renderShell('/');
    await settle();

    const link = flaggedLink('Flagged, 3 to review');
    const pill = within(link).getByText('3');

    expect(pill).toHaveAttribute('aria-hidden', 'true');
    expect(within(link).getByText(', 3 to review')).toHaveClass('visually-hidden');
    expect(link).toHaveAttribute('href', '/flagged');
  });

  it.each([{ count: 0 }, { count: undefined }])('shows the word alone for a count of $count', async ({ count }) => {
    serveMe(() => ({ ...manager, toReviewCount: count }));
    renderShell('/');
    await settle();

    const link = flaggedLink('Flagged');

    expect(link).toHaveTextContent(/^Flagged$/);
    expect(link.querySelector('span')).toBeNull();
  });

  it('shows the exact number, with no cap', async () => {
    serveMe(() => ({ ...manager, toReviewCount: 104 }));
    renderShell('/');
    await settle();

    expect(within(flaggedLink('Flagged, 104 to review')).getByText('104')).toBeInTheDocument();
  });

  it('gives the pill no aria-live, role or title', async () => {
    serveMe(() => ({ ...manager, toReviewCount: 3 }));
    renderShell('/');
    await settle();

    const pill = within(flaggedLink('Flagged, 3 to review')).getByText('3');

    for (const attribute of ['aria-live', 'role', 'title']) {
      expect(pill).not.toHaveAttribute(attribute);
    }
  });

  it('gives a worker, whose answer has no count, no pill', async () => {
    serveMe(() => worker);
    renderShell('/');
    await settle();

    expect(screen.queryByRole('link', { name: /^Flagged/ })).toBeNull();
    expect(screen.getByRole('banner').querySelector('span[aria-hidden]')).toBeNull();
  });

  it('keeps the number it had when a refresh fails', async () => {
    serveMe(() => ({ ...manager, toReviewCount: 3 }));
    const { client } = renderShell('/');
    await settle();
    expect(flaggedLink('Flagged, 3 to review')).toBeInTheDocument();

    fetchMock.mockImplementation(() => Promise.reject(new TypeError('Failed to fetch')));
    act(() => {
      refreshMe(client).catch(() => undefined);
    });
    // Past the query client's one silent retry.
    await act(() => vi.advanceTimersByTimeAsync(60_000));

    expect(flaggedLink('Flagged, 3 to review')).toBeInTheDocument();
  });

  it('follows /me on a screen change and on window focus, and sends nothing else while idle', async () => {
    let count = 3;
    serveMe(() => ({ ...manager, toReviewCount: count }));
    const { router } = renderShell('/');
    await settle();
    expect(flaggedLink('Flagged, 3 to review')).toBeInTheDocument();

    count = 4;
    await act(() => router.navigate('/flagged'));
    await settle();
    expect(flaggedLink('Flagged, 4 to review')).toBeInTheDocument();

    count = 5;
    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await settle();
    expect(flaggedLink('Flagged, 5 to review')).toBeInTheDocument();

    const sent = fetchMock.mock.calls.length;
    await act(() => vi.advanceTimersByTimeAsync(10 * 60_000));

    expect(fetchMock.mock.calls).toHaveLength(sent);
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
    { me: support, signedInAs: 'Signed in as Ivy Chen' },
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
    // With no page guarding, confirmLeave() answers at once, but through a promise, which this lets settle: only then
    // does the button go busy.
    await settle();
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

  it('empties the status line as it signs out again, and says the new failure into it', async () => {
    serveSignOut(worker, answer(503), noAnswer);
    const { signOut, line } = await openPanel();
    fireEvent.click(signOut);
    await settle();
    expect(line.textContent).toBe(serverFailure);

    fireEvent.click(signOut);
    await settle();

    // The second logout is out, unanswered, with the line already empty.
    expect(logoutsSent()).toHaveLength(2);
    expect(line).toBeEmptyDOMElement();

    await act(() => vi.advanceTimersByTimeAsync(10_000));

    expect(line.textContent).toBe(noConnection);
  });

  // A note form's change waiting for its save, as app-shell.md *Press Sign out* step 2 has it: the note form itself is
  // in NotePage.test.tsx.
  it("saves a note form's change waiting after going busy, then sends the logout, disarmed", async () => {
    const { logout, armedWhenSent } = notingArmed(answer(204));
    serveSignOut(worker, logout);
    const form = standInForm();
    const { signOut } = await openPanel();

    fireEvent.click(signOut);
    await settle();
    await act(() => vi.advanceTimersByTimeAsync(400));

    expect(form.flushNow).toHaveBeenCalledOnce();
    expect(signOutRequests()).toEqual([]);
    expect(signOut).toHaveAccessibleName('Signing out…');
    expect(signOut).toHaveAttribute('aria-disabled', 'true');
    expect(unloadGuardArmed()).toBe(true);

    await form.answer(true);

    expect(logoutsSent()).toEqual([{ method: 'POST', token: antiforgeryToken }]);
    expect(armedWhenSent).toEqual([false]);
    expect(fullPageLoad).toHaveBeenCalledOnce();
  });

  it("sends no logout when a note form's change cannot be saved, saying so, with Sign out ready", async () => {
    serveSignOut(manager);
    const form = standInForm();
    const { signOut, line } = await openPanel();
    fireEvent.click(signOut);
    await settle();

    await form.answer(false);

    expect(line.textContent).toBe('Not signed out: your note is not saved yet. Keep this page open; retrying.');
    expect(signOutRequests()).toEqual([]);
    expect(signOut).not.toHaveAttribute('aria-disabled');
    expect(signOut).toHaveAccessibleName('Sign out');
    expect(signOut).toHaveFocus();
    expect(unloadGuardArmed()).toBe(true);
    expect(fullPageLoad).not.toHaveBeenCalled();
    expect(headerSignedIn()).toBe(true);
  });

  // The tests above run with no page guarding, as on every screen but Guide prompts with unsaved text; those below
  // stand in for that page's leave dialog (app-shell.md *Press Sign out* and *Sign out fails*).
  it('with no page guarding, sends the one logout at once, the unload guard disarmed before it', async () => {
    const { logout, armedWhenSent } = notingArmed(answer(204));
    serveSignOut(worker, logout);
    const { signOut } = await openPanel();

    fireEvent.click(signOut);
    await settle();

    expect(logoutsSent()).toEqual([{ method: 'POST', token: antiforgeryToken }]);
    expect(armedWhenSent).toEqual([false]);
    expect(fullPageLoad).toHaveBeenCalledOnce();
  });

  it("asks the page's guard before any request and before going busy, and on Stay sends nothing", async () => {
    serveSignOut(manager);
    const guard = standInGuard();
    const { signOut, line } = await openPanel();

    fireEvent.click(signOut);
    await act(() => vi.advanceTimersByTimeAsync(1_000));

    expect(guard.ask).toHaveBeenCalledOnce();
    expect(signOutRequests()).toEqual([]);
    expect(signOut).not.toHaveAttribute('aria-disabled');
    expect(signOut).toHaveAccessibleName('Sign out');

    await guard.answer(false);
    await act(() => vi.advanceTimersByTimeAsync(1_000));

    expect(signOutRequests()).toEqual([]);
    expect(signOut).not.toHaveAttribute('aria-disabled');
    expect(signOut).toHaveAccessibleName('Sign out');
    expect(signOut).toHaveFocus();
    expect(line).toBeEmptyDOMElement();
    expect(unloadGuardArmed()).toBe(true);
    expect(fullPageLoad).not.toHaveBeenCalled();
    expect(headerSignedIn()).toBe(true);
  });

  it('leaves the panel as it was on Stay, the failure line from before kept', async () => {
    serveSignOut(worker, answer(503));
    const { signOut, line } = await openPanel();
    fireEvent.click(signOut);
    await settle();
    expect(line.textContent).toBe(serverFailure);
    const guard = standInGuard();

    fireEvent.click(signOut);
    await settle();
    await guard.answer(false);

    expect(guard.ask).toHaveBeenCalledOnce();
    expect(logoutsSent()).toHaveLength(1);
    expect(line.textContent).toBe(serverFailure);
  });

  it.each([204, 401])('on Leave, disarms the unload guard before the logout and reloads after a %i', async (status) => {
    const { logout, armedWhenSent } = notingArmed(answer(status));
    serveSignOut(manager, logout);
    const guard = standInGuard();
    const { signOut } = await openPanel();
    fireEvent.click(signOut);
    await settle();
    expect(signOutRequests()).toEqual([]);

    await guard.answer(true);

    expect(guard.ask).toHaveBeenCalledOnce();
    expect(logoutsSent()).toEqual([{ method: 'POST', token: antiforgeryToken }]);
    expect(armedWhenSent).toEqual([false]);
    expect(unloadGuardArmed()).toBe(false);
    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(signOut).toHaveAttribute('aria-disabled', 'true');
  });

  it.each([
    { failure: 'a network failure', logout: offline, sentence: noConnection },
    { failure: 'a 503', logout: answer(503), sentence: serverFailure },
  ])('on Leave and $failure, arms the unload guard again and says so', async ({ logout, sentence }) => {
    const noted = notingArmed(logout);
    serveSignOut(worker, noted.logout);
    const guard = standInGuard();
    const { signOut, line } = await openPanel();
    fireEvent.click(signOut);
    await settle();

    await guard.answer(true);

    expect(noted.armedWhenSent).toEqual([false]);
    expect(unloadGuardArmed()).toBe(true);
    expect(line.textContent).toBe(sentence);
    expect(signOut).not.toHaveAttribute('aria-disabled');
    expect(signOut).toHaveFocus();
    expect(fullPageLoad).not.toHaveBeenCalled();
    expect(headerSignedIn()).toBe(true);
  });
});
