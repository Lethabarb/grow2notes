import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me, type Role } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminUser, UserStatus } from './usersQuery.ts';

const mePath = '/api/auth/me';
const usersPath = '/api/admin/users';
const antiforgeryPath = '/api/auth/antiforgery';

// users.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts, and a
// worker who is not on the list.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

/** A made-up user, invited on 1 September, set up the next day unless still Invited, and deactivated if so. */
function madeUp(id: string, displayName: string, email: string, status: UserStatus, role: Role = 'Worker'): AdminUser {
  return {
    id,
    email,
    displayName,
    role,
    status,
    invitedAtUtc: '2026-09-01T00:00:00Z',
    activatedAtUtc: status === 'Invited' ? null : '2026-09-02T00:00:00Z',
    deactivatedAtUtc: status === 'Deactivated' ? '2026-09-30T00:00:00Z' : null,
    concurrencyStamp: `stamp-${id}`,
  };
}

const sam = madeUp(manager.userId, 'Sam Lee', 'sam.lee@example.org', 'Active', 'Manager');
const jo = madeUp('0c6f2e8a-4b17-4d39-a5e2-7f1b3c9d0a64', 'Jo Smith', 'jo.smith@example.org', 'Active');
const alex = madeUp('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Alex Park', 'alex.park@example.org', 'Invited');
const chris = madeUp('7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20', 'Chris Ng', 'chris.ng@example.org', 'Deactivated');

const people = [sam, chris, jo, alex];

// Resend invite's request for Alex Park (design.md §6.6).
const setupLinkPath = `/api/admin/users/${alex.id}/setup-link`;

/** The four, with Alex Park as `changed` makes them, as the list gives them after a change to Alex. */
function peopleWithAlex(changed: Partial<AdminUser>): AdminUser[] {
  return [sam, chris, jo, { ...alex, ...changed }];
}

// The two times at which the tests' server sends a link, a minute apart: 4:12 pm and 4:13 pm in Melbourne, which is
// on daylight saving time (UTC+11) from 4 October 2026.
const sentAt0512 = 'Wed, 14 Oct 2026 05:12:00 GMT';
const sentAt0513 = 'Wed, 14 Oct 2026 05:13:00 GMT';

// The words of users.md's *States* › User detail, with the typographic apostrophe of src/copy.
const sentAt412pm = 'Setup link sent to alex.park@example.org at 4:12 pm. The old link no longer works.';
const sentAt413pm = 'Setup link sent to alex.park@example.org at 4:13 pm. The old link no longer works.';
const someoneChangedAlex =
  'Someone else has just changed Alex Park’s account. The page now shows the latest details.';

// A made-up ID that is on no list.
const unknownId = '4f2a9c7e-1b38-4d65-a0e9-8c3b5d1f7a26';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code`, if any. */
function problem(status: number, code?: string): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, code });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** The antiforgery endpoint's answer: a `204` with the token in a header (design.md §9.8 item 2). */
const token: Answer = () =>
  Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));

/** A `204` with no body, as Resend invite is answered, sent at `date`, its `Date` header, or with none. */
function noContent(date?: string): Answer {
  return () => Promise.resolve(new Response(null, { status: 204, headers: date === undefined ? {} : { Date: date } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts, with the signal's reason. */
const noAnswer: Answer = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

/** An answer held until the test gives it with `release`. */
function held(answer: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const given = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: async (init) => given.then(() => answer(init)), release };
}

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** How many requests have been sent for `path`. */
function sent(path: string): number {
  return fetchMock.mock.calls.filter(([sentPath]) => sentPath === path).length;
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own and StrictMode, as `main.tsx`
 * renders it. `['me']` holds `me` already, as after the start-up, so that the screen shows with the first render, from
 * which the fake clock then times its region.
 */
function renderApp(path: string, me: Me = manager) {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return router;
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the queries' new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The shell's before-main bar slot, between `<header>` and `<main>`. */
function pageBar(): HTMLElement {
  const slot = document.getElementById('page-bar');
  if (slot === null) {
    throw new Error('The shell rendered no #page-bar.');
  }

  return slot;
}

/** The back link to the list, wherever it is. */
function backLink(): HTMLElement | null {
  return screen.queryByRole('link', { name: 'Users' });
}

/**
 * The region's line: the status region straight under `<main>` that shows, beside the page's visually hidden one and
 * apart from Resend invite's, which is among the actions.
 */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => region.parentElement === main() && !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The summary list's rows, each a `<div>` holding exactly a `<dt>` and its `<dd>`, as their texts. */
function facts(): string[][] {
  const lists = main().querySelectorAll('dl');
  expect(lists).toHaveLength(1);
  return Array.from(lists[0].children, (row) => {
    expect(row.tagName).toBe('DIV');
    expect(Array.from(row.children, (cell) => cell.tagName)).toEqual(['DT', 'DD']);
    return Array.from(row.children, (cell) => cell.textContent);
  });
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Expects Page not found alone in `<main>`, in order: its `<h1>`, its sentence, Go to Today and its status region,
 * empty; with no back link in the page bar; under the nav, which marks no item; titled "Page not found".
 */
function expectPageNotFound() {
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(Array.from(main().children, (element) => element.textContent)).toEqual([
    'Page not found',
    'If you typed or pasted the web address, check it is correct.',
    'Go to Today',
    '',
  ]);
  expect(within(main()).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(backLink()).not.toBeInTheDocument();
  expect(pageBar()).toBeEmptyDOMElement();
  expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
  expect(currentItems()).toEqual([]);
  expect(document.title).toBe('Grow2Notes – Page not found');
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain(): HTMLElement {
  const button = within(main()).getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Resend invite, by either of its labels, or `null` when the page does not offer it. */
function resendButton(): HTMLElement | null {
  return within(main()).queryByRole('button', { name: /^(Resend invite|Sending…)$/ });
}

/** Focuses Resend invite and presses it, as a person does, and returns it. */
function pressResend(): HTMLElement {
  const button = resendButton();
  if (button === null) {
    throw new Error('The page offers no Resend invite.');
  }

  button.focus();
  fireEvent.click(button);
  return button;
}

/** Resend invite's line for its result, the status region after its button. */
function resendLine(): HTMLElement {
  const line = resendButton()?.nextElementSibling;
  expect(line).toHaveAttribute('role', 'status');
  return line as HTMLElement;
}

/** The action error region above the actions (users.md component C item 5). */
function actionAlert(): HTMLElement {
  return within(main()).getByRole('alert');
}

/** Each Resend invite request sent for Alex Park, as its method, its `If-Match` and its body. */
function setupLinksSent() {
  return fetchMock.mock.calls
    .filter(([path]) => path === setupLinkPath)
    .map(([, init]) => ({
      method: init?.method,
      ifMatch: new Headers(init?.headers).get('If-Match'),
      body: init?.body,
    }));
}

/** Lets a request answer, the users refetch that follows it answer too, and TanStack hand on the new state. */
async function answered() {
  await settle();
  await settle();
}

/** As the browser does when the window comes back into view, on which TanStack refetches the queries that opt in. */
function returnToWindow() {
  act(() => {
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });
}

/** Opens Alex Park's page, from a list that has them Invited, ready to send their link. */
async function openAlex() {
  serve(usersPath, json(people));
  serve(antiforgeryPath, token);
  renderApp(`/manage/users/${alex.id}`);
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // React and React Router report each throw that a boundary catches: a refusal is expected, and anything else shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.some((arg) => arg instanceof ApiError && arg.status === 403)) {
      report(...args);
    }
  });
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('User', () => {
  // users.md's *Which actions show, by status*, for the actions built so far: Edit for every status.
  it.each([
    { user: jo, role: 'Worker', status: 'Active' },
    { user: sam, role: 'Manager', status: 'Active' },
    { user: chris, role: 'Worker', status: 'Deactivated' },
  ])(
    'shows $status $user.displayName: the name as the <h1>, the email, role and status, and Edit, no Resend invite',
    async ({ user, role, status }) => {
      serve(usersPath, json(people));
      renderApp(`/manage/users/${user.id}`);
      await settle();

      expect(pageHeading()).toHaveAccessibleName(user.displayName);
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(facts()).toEqual([
        ['Email address', user.email],
        ['Role', role],
        ['Status', status],
      ]);
      const edit = within(main()).getByRole('link', { name: 'Edit' });
      expect(edit).toHaveAttribute('href', `/manage/users/${user.id}/edit`);
      expect(edit).toHaveClass(...buttonClass('secondary').split(' '));
      expect(within(main()).getAllByRole('link')).toEqual([edit]);
      expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
      expect(actionAlert()).toBeEmptyDOMElement();
      expect(actionAlert().compareDocumentPosition(edit)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
      expect(document.querySelector('[disabled]')).toBeNull();
      expect(line()).toBeEmptyDOMElement();
    },
  );

  it('shows Invited Alex Park with Edit, then Resend invite under its description, then its empty line', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${alex.id}`);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(facts()).toEqual([
      ['Email address', 'alex.park@example.org'],
      ['Role', 'Worker'],
      ['Status', 'Invited'],
    ]);
    const edit = within(main()).getByRole('link', { name: 'Edit' });
    expect(edit).toHaveAttribute('href', `/manage/users/${alex.id}/edit`);
    expect(within(main()).getAllByRole('link')).toEqual([edit]);
    const resend = within(main()).getByRole('button');
    expect(resend).toHaveAccessibleName('Resend invite');
    expect(resend).toHaveAccessibleDescription('Sends a new setup link. The old link stops working.');
    expect(resend).toHaveAttribute('type', 'button');
    expect(resend).toHaveClass(...buttonClass('secondary').split(' '));
    expect(resendLine()).toBeEmptyDOMElement();
    expect(actionAlert()).toBeEmptyDOMElement();
    // The alert region, then group 1 in its order (users.md component C items 5 and 6).
    const description = within(main()).getByText('Sends a new setup link. The old link stops working.');
    const inOrder = [actionAlert(), edit, description, resend, resendLine()];
    for (const [index, element] of inOrder.slice(1).entries()) {
      expect(inOrder[index].compareDocumentPosition(element)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    }
    expect(document.querySelector('[disabled]')).toBeNull();
    expect(line()).toBeEmptyDOMElement();
  });

  it('is titled "User", with Manage marked, and holds no name or email in its title or address', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${alex.id}`);
    await settle();

    expect(document.title).toBe('Grow2Notes – User');
    expect(currentItems()).toEqual(['Manage true']);
    const { pathname, search, hash } = router.state.location;
    for (const text of [document.title, `${pathname}${search}${hash}`]) {
      expect(text).not.toContain(alex.displayName);
      expect(text).not.toContain(alex.email);
    }
  });

  it('has its back link, named "Users", to the list, in the page bar outside <main>, which it opens', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${alex.id}`);
    await settle();

    const link = backLink();
    expect(link).toHaveAccessibleName('Users');
    expect(link).toHaveAttribute('href', '/manage/users');
    expect(pageBar()).toContainElement(link);
    expect(main()).not.toContainElement(link);

    fireEvent.click(link!);
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
  });

  it('opens from its row of the list, with nothing in the router state', async () => {
    serve(usersPath, json(people));
    const router = renderApp('/manage/users');
    await settle();

    fireEvent.click(within(main()).getByRole('link', { name: 'Alex Park' }));
    await settle();

    expect(router.state.location.pathname).toBe(`/manage/users/${alex.id}`);
    expect(router.state.location.state).toBeNull();
    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(facts()[0]).toEqual(['Email address', 'alex.park@example.org']);
    expect(document.title).toBe('Grow2Notes – User');
  });
});

describe('User while the list loads', () => {
  it('shows its back link and no <h1>, then nothing more until "Loading user…" at 1 s', async () => {
    serve(usersPath, noAnswer);
    renderApp(`/manage/users/${alex.id}`);

    expect(backLink()).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expect(main().textContent).toBe('');

    await wait(1);
    expect(line()).toHaveTextContent('Loading user…');
    expect(main().textContent).toBe('Loading user…');
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('says the user did not load, under the <h1> "User", with Try again, after its silent retry', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);

    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('User');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(line().textContent).toBe('This user did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(main().querySelector('dl')).toBeNull();
    expect(backLink()).toBeInTheDocument();
  });

  it('says the user still did not load after a Try again that fails, with focus still on Try again', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('This user still did not load: no connection. Try again.');
    expect(pageHeading()).toHaveAccessibleName('User');
    expect(button).toHaveFocus();
  });

  it('shows the user after a Try again that loads, with focus on their name, the one <h1>', async () => {
    serve(usersPath, noConnection);
    renderApp(`/manage/users/${alex.id}`);
    await wait(1000);
    await settle();
    serve(usersPath, json(people));

    pressTryAgain();
    await settle();

    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([pageHeading()]);
    expect(pageHeading()).toHaveAccessibleName('Alex Park');
    expect(pageHeading()).toHaveFocus();
    expect(line()).toBeEmptyDOMElement();
  });

  it('shows Page not found in place of the whole page for a 403, at the same address', async () => {
    serve(usersPath, problem(403));
    const router = renderApp(`/manage/users/${alex.id}`);

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe(`/manage/users/${alex.id}`);
  });
});

describe('User for an ID not on the list', () => {
  it('shows Page not found in place of the whole page, at the same address', async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${unknownId}`);

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe(`/manage/users/${unknownId}`);
  });
});

describe('User for a worker', () => {
  it('shows Page not found, and sends no request for the list', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${alex.id}`, worker);

    await wait(60_000);

    expectPageNotFound();
    expect(sent(usersPath)).toBe(0);
  });
});

describe('Resend invite', () => {
  it('sends one POST with the stamp as If-Match, however often pressed, busy, "Sending…" from 400 ms', async () => {
    await openAlex();
    serve(setupLinkPath, noAnswer);

    const button = pressResend();
    pressResend();
    await wait(399);
    pressResend();

    expect(setupLinksSent()).toEqual([{ method: 'POST', ifMatch: `"stamp-${alex.id}"`, body: undefined }]);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).toHaveAccessibleName('Resend invite');
    expect(button).toHaveFocus();

    await wait(1);
    expect(button).toHaveAccessibleName('Sending…');
    act(() => {
      vi.advanceTimersToNextFrame();
    });
    expect(main().querySelector('p.visually-hidden[role="status"]')).toHaveTextContent('Sending…');
    expect(document.querySelector('[disabled]')).toBeNull();
    expect(setupLinksSent()).toHaveLength(1);
  });

  it('waits, busy, for the list, then says when the server sent the link, in Melbourne, with focus kept', async () => {
    // The device's clock on another day, in a zone behind UTC, neither of which the sentence reads.
    vi.setSystemTime(new Date('2031-02-03T22:45:00Z'));
    vi.stubEnv('TZ', 'America/Los_Angeles');
    expect(new Date('2031-02-03T22:45:00Z').getHours()).toBe(14);
    await openAlex();
    serve(setupLinkPath, noContent(sentAt0512));
    const list = held(json(peopleWithAlex({ concurrencyStamp: 'stamp-2' })));
    serve(usersPath, list.answer);
    const listsBefore = sent(usersPath);

    const button = pressResend();
    await answered();

    // The link has gone; the button stays busy until the list holds Alex Park's new stamp.
    expect(setupLinksSent()).toHaveLength(1);
    expect(sent(usersPath)).toBe(listsBefore + 1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(resendLine()).toBeEmptyDOMElement();

    list.release();
    await answered();

    expect(resendLine().textContent).toBe(sentAt412pm);
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(button).toHaveFocus();
    expect(actionAlert()).toBeEmptyDOMElement();
    expect(facts()[2]).toEqual(['Status', 'Invited']);
  });

  it('leaves out "at …" when the answer has no Date header, never reading the device clock', async () => {
    await openAlex();
    serve(setupLinkPath, noContent());

    pressResend();
    await answered();

    expect(resendLine().textContent).toBe('Setup link sent to alex.park@example.org. The old link no longer works.');
  });

  it('empties the line at a second press a minute later, then gives the new time, with the new stamp', async () => {
    await openAlex();
    serve(setupLinkPath, noContent(sentAt0512));
    serve(usersPath, json(peopleWithAlex({ concurrencyStamp: 'stamp-2' })));
    pressResend();
    await answered();
    expect(resendLine().textContent).toBe(sentAt412pm);
    serve(setupLinkPath, noContent(sentAt0513));
    serve(usersPath, json(peopleWithAlex({ concurrencyStamp: 'stamp-3' })));
    await wait(60_000);

    const button = pressResend();
    // Emptied at the press, so that the sentence is written into an empty region and announced again.
    expect(resendLine()).toBeEmptyDOMElement();
    await answered();

    expect(resendLine().textContent).toBe(sentAt413pm);
    expect(button).toHaveFocus();
    expect(setupLinksSent().map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${alex.id}"`, '"stamp-2"']);
  });

  it('on a 412 refetches the list, then says someone changed the account, keeping focus and new stamp', async () => {
    await openAlex();
    serve(setupLinkPath, problem(412, 'precondition.failed'));
    const list = held(json(peopleWithAlex({ displayName: 'Alex Parker', concurrencyStamp: 'stamp-2' })));
    serve(usersPath, list.answer);

    const button = pressResend();
    await answered();

    // Said once the page shows Alex as they are now, so the button waits, busy, for the list.
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(actionAlert()).toBeEmptyDOMElement();

    list.release();
    await answered();

    expect(actionAlert().textContent).toBe(someoneChangedAlex);
    expect(pageHeading()).toHaveAccessibleName('Alex Parker');
    expect(resendLine()).toBeEmptyDOMElement();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(button).toHaveFocus();

    serve(setupLinkPath, noContent(sentAt0512));
    pressResend();
    // The alert empties at the press, as the line does.
    expect(actionAlert()).toBeEmptyDOMElement();
    await answered();

    expect(setupLinksSent().map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${alex.id}"`, '"stamp-2"']);
    expect(resendLine().textContent).toBe(sentAt412pm);
  });

  it('on a 412 whose list shows Alex now Active, takes Resend invite away, focus going to the <h1>', async () => {
    await openAlex();
    serve(setupLinkPath, problem(412, 'precondition.failed'));
    serve(
      usersPath,
      json(peopleWithAlex({ status: 'Active', activatedAtUtc: '2026-10-14T05:00:00Z', concurrencyStamp: 'stamp-2' })),
    );

    pressResend();
    await answered();

    expect(actionAlert().textContent).toBe(someoneChangedAlex);
    expect(facts()[2]).toEqual(['Status', 'Active']);
    expect(resendButton()).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: 'Not sent: no connection. Try again.' },
    { given: 'a 500', answer: problem(500), sentence: 'Not sent: something went wrong. Try again.' },
    { given: 'a 403', answer: problem(403), sentence: 'Not sent: something went wrong. Try again.' },
    {
      given: 'a 409 user.not_active',
      answer: problem(409, 'user.not_active'),
      sentence: 'Not sent: something went wrong. Try again.',
    },
  ])('says what $given means above the actions, keeps focus, and sends nothing by itself', async (case_) => {
    await openAlex();
    serve(setupLinkPath, case_.answer);
    const listsBefore = sent(usersPath);

    const button = pressResend();
    await answered();

    expect(actionAlert().textContent).toBe(case_.sentence);
    expect(resendLine()).toBeEmptyDOMElement();
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(sent(usersPath)).toBe(listsBefore);

    await wait(60_000);
    expect(setupLinksSent()).toHaveLength(1);
  });

  it('says "no connection" for an answer that never comes, after its 10 seconds', async () => {
    await openAlex();
    serve(setupLinkPath, noAnswer);

    pressResend();
    await wait(10_000);
    await settle();

    expect(actionAlert().textContent).toBe('Not sent: no connection. Try again.');
    expect(resendButton()).not.toHaveAttribute('aria-disabled');
  });

  it('says nothing of a 401, which the session flow has', async () => {
    await openAlex();
    serve(setupLinkPath, problem(401));

    pressResend();
    await answered();

    expect(actionAlert()).toBeEmptyDOMElement();
    expect(resendLine()).toBeEmptyDOMElement();
  });

  it('drops its sentence once the list shows Alex no longer Invited, never to come back with it', async () => {
    await openAlex();
    serve(setupLinkPath, noContent(sentAt0512));
    pressResend();
    await answered();
    expect(resendLine().textContent).toBe(sentAt412pm);

    serve(usersPath, json(peopleWithAlex({ status: 'Active', concurrencyStamp: 'stamp-2' })));
    returnToWindow();
    await answered();
    expect(resendButton()).not.toBeInTheDocument();

    serve(usersPath, json(peopleWithAlex({ concurrencyStamp: 'stamp-3' })));
    returnToWindow();
    await answered();
    expect(resendLine()).toBeEmptyDOMElement();
  });
});
