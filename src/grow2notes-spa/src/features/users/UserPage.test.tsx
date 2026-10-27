import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, onTestFinished, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me, type Role } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { fullPageLoad } from '../../routes/fullPageLoad.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminUser, UserStatus } from './usersQuery.ts';

// jsdom does not navigate, so the tests see the full page load that your own reset or deactivation asks for.
vi.mock('../../routes/fullPageLoad.ts');

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
const ivy = madeUp('d4a9c1e7-6b38-4f02-8e5a-0c7b2d9f1a63', 'Ivy Chen', 'ivy.chen@example.org', 'Active', 'Support');

const people = [sam, chris, jo, alex];

// A second Active manager, with whom Sam Lee is no longer the only one (A26).
const kim = madeUp('b2d6f0a4-7e31-4c9b-8a5d-1f3e7c2b9d60', 'Kim Tran', 'kim.tran@example.org', 'Active', 'Manager');

// Resend invite's request for Alex Park (design.md §6.6).
const setupLinkPath = `/api/admin/users/${alex.id}/setup-link`;

/** The request of a change to `user`: `setup-link`, which Reset sign-in sends too, `deactivate` or `reactivate`. */
const changePath = (user: AdminUser, change: 'setup-link' | 'deactivate' | 'reactivate') =>
  `/api/admin/users/${user.id}/${change}`;

/** The four, with Alex Park as `changed` makes them, as the list gives them after a change to Alex. */
function peopleWithAlex(changed: Partial<AdminUser>): AdminUser[] {
  return [sam, chris, jo, { ...alex, ...changed }];
}

/** `users` with `user` as `changed` makes them, and a new stamp, as the list gives them after a change to them. */
function withChanged(users: readonly AdminUser[], user: AdminUser, changed: Partial<AdminUser>): AdminUser[] {
  return users.map((listed) =>
    listed.id === user.id ? { ...listed, concurrencyStamp: 'stamp-2', ...changed } : listed,
  );
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
 * Renders the app at `path` for `me` on a memory router, or at the last of `path`'s addresses, after the others in its
 * history, under a query client of its own and StrictMode, as `main.tsx` renders it. `['me']` holds `me` already, as
 * after the start-up, so that the screen shows with the first render, from which the fake clock then times its region.
 */
function renderApp(path: string | string[], me: Me = manager) {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // Each entry with a key of its own, as a browser gives it, where a memory router gives every test's first entry the
  // key "default", under which the shell's memory would carry a row or a position from one test into the next.
  const initialEntries = (typeof path === 'string' ? [path] : path).map((entry) => ({
    ...parsePath(entry),
    key: crypto.randomUUID(),
  }));
  const router = createMemoryRouter(routes, { initialEntries });
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

/**
 * The `<div>` of `<main>` that the shell holds the screen in, which the signed-out-in-place view hides (app-shell.md
 * component 7).
 */
function screenContainer(): Element | null {
  return main().firstElementChild;
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
 * The region's line: the status region straight under the screen's container that shows, beside the page's visually
 * hidden one and apart from Resend invite's, which is among the actions.
 */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter(
      (region) => region.parentElement === screenContainer() && !region.classList.contains('visually-hidden'),
    );
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

/** The summary list's Status row, its third, the `<div>` that takes focus once an action has changed the status. */
function statusRow(): Element | undefined {
  const row = main().querySelector('dl')?.children[2];
  expect(row?.firstElementChild).toHaveTextContent(/^Status$/);
  return row;
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Expects Page not found alone in the screen's container, in order: its `<h1>`, its sentence, Go to Today and its
 * status region, empty; with no back link in the page bar; under the nav, which marks no item; titled "Page not
 * found".
 */
function expectPageNotFound() {
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(Array.from(screenContainer()?.children ?? [], (element) => element.textContent)).toEqual([
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

/** The action error region above the actions (users.md component C item 5), apart from an open dialog's alert line. */
function actionAlert(): HTMLElement {
  const regions = within(main())
    .getAllByRole('alert')
    .filter((region) => region.closest('dialog') === null);
  expect(regions).toHaveLength(1);
  return regions[0];
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

/** Opens `user`'s page for Sam Lee, from a list of `users`, ready to send a change. */
async function openPage(user: AdminUser, users: readonly AdminUser[] = people) {
  serve(usersPath, json(users));
  serve(antiforgeryPath, token);
  renderApp(`/manage/users/${user.id}`);
  await settle();
}

/**
 * The actions as they read, in order: the error region's words, each link and button by its label, each description
 * and the notice's words, and the rule, apart from Resend invite's line and an open dialog.
 */
function actionsAsRead(): string[] {
  const actions = actionAlert().parentElement;
  const parts = actions?.querySelectorAll('[role="alert"], a, button, p:not([role]), hr') ?? [];
  return Array.from(parts)
    .filter((part) => part.closest('dialog') === null)
    .map((part) => {
      switch (part.localName) {
        case 'a':
          return `link: ${part.textContent}`;
        case 'button':
          // Its label, without the busy label that shares its cell, hidden until it is busy.
          return `button: ${part.firstElementChild?.textContent}`;
        case 'hr':
          return 'rule';
        default:
          return part.getAttribute('role') === 'alert' ? `alert: ${part.textContent}` : (part.textContent ?? '');
      }
    });
}

/** The page's button whose label is `name`, outside any dialog, or `null` when the page does not offer it. */
function pageButton(name: string): HTMLElement | null {
  const buttons = within(main())
    .queryAllByRole('button', { name: new RegExp(`^${name}`) })
    .filter((button) => button.closest('dialog') === null);
  return buttons[0] ?? null;
}

/** Focuses the page's button whose label is `name` and presses it, as a person does, and returns it. */
function press(name: string): HTMLElement {
  const button = pageButton(name);
  if (button === null) {
    throw new Error(`The page offers no ${name}.`);
  }

  button.focus();
  fireEvent.click(button);
  return button;
}

/** The open dialog, or `null`. */
function openDialog(): HTMLDialogElement | null {
  const element = document.querySelector('dialog[open]');
  return element instanceof HTMLDialogElement ? element : null;
}

/** The open dialog, which there must be. */
function dialog(): HTMLDialogElement {
  const element = openDialog();
  if (element === null) {
    throw new Error('No dialog is open.');
  }

  return element;
}

/** The dialog's buttons, in order, by their names. */
function dialogButtons(): string[] {
  return within(dialog())
    .getAllByRole('button')
    .map((button) => button.firstElementChild?.textContent ?? '');
}

/** The dialog's button named `name`. */
function dialogButton(name: string): HTMLElement {
  return within(dialog()).getByRole('button', { name });
}

/** Focuses the dialog's button named `name` and presses it, and returns it. */
function pressInDialog(name: string): HTMLElement {
  const button = dialogButton(name);
  button.focus();
  fireEvent.click(button);
  return button;
}

/** As the browser does for Escape or Android's back gesture: `cancel`, then the close, unless `cancel` was stopped. */
function closeRequest() {
  const element = dialog();
  const cancel = new Event('cancel', { cancelable: true });
  fireEvent(element, cancel);
  if (!cancel.defaultPrevented) {
    element.close();
  }
}

/** Each request sent to `path`, as its method and its `If-Match`. */
function sentTo(path: string) {
  return fetchMock.mock.calls
    .filter(([sentPath]) => sentPath === path)
    .map(([, init]) => ({ method: init?.method, ifMatch: new Headers(init?.headers).get('If-Match') }));
}

/** The `<p>`s of the dialog's body, by their text. */
function dialogBody(): string[] {
  const body = document.getElementById(dialog().getAttribute('aria-describedby') ?? '');
  return Array.from(body?.children ?? [], (line) => line.textContent ?? '');
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
  vi.mocked(fullPageLoad).mockReset();
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

// The words of users.md's component C, items 6 and 7, and *States* › User detail.
const resetDescription = 'For a lost phone, a new phone or a forgotten password. Check who is asking first.';
const deactivateDescription = 'For someone who no longer works here. Their notes stay.';
const reactivateDescription = 'Lets them sign in again.';
const lastManagerNotice =
  'You are the only active manager, so you cannot reset your own sign-in, deactivate your account or make yourself ' +
  'a worker. To do any of these, first make someone else a manager.';

// An Active user's actions after Edit: Reset sign-in, then the rule and Deactivate, each under its description.
const resetThenDeactivate = [
  resetDescription,
  'button: Reset sign-in',
  'rule',
  deactivateDescription,
  'button: Deactivate',
];

describe('User', () => {
  // users.md's *Which actions show, by status* (AC21), and, on your own page with a second active manager, AC22.
  it.each([
    { user: jo, role: 'Worker', status: 'Active', users: people, after: resetThenDeactivate },
    {
      user: chris,
      role: 'Worker',
      status: 'Deactivated',
      users: people,
      after: ['rule', reactivateDescription, 'button: Reactivate'],
    },
    { user: sam, role: 'Manager', status: 'Active', users: [...people, kim], after: resetThenDeactivate },
    { user: kim, role: 'Manager', status: 'Active', users: [...people, kim], after: resetThenDeactivate },
    { user: ivy, role: 'Support', status: 'Active', users: [...people, ivy], after: resetThenDeactivate },
  ])(
    'shows $status $user.displayName: the <h1>, the email, role and status, Edit, then its actions in order',
    async ({ user, role, status, users, after }) => {
      await openPage(user, users);

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
      expect(actionsAsRead()).toEqual(['alert: ', 'link: Edit', ...after]);
      // Each button named again by the description above it (users.md component C items 6 and 7).
      const described = after.filter((_, index) => after[index + 1]?.startsWith('button: '));
      for (const [index, button] of within(main()).getAllByRole('button').entries()) {
        expect(button).toHaveAccessibleDescription(described[index]);
        expect(button).toHaveAttribute('type', 'button');
        expect(button).toHaveClass(...buttonClass('secondary').split(' '));
      }
      expect(document.querySelector('[disabled]')).toBeNull();
      expect(openDialog()).toBeNull();
      expect(line()).toBeEmptyDOMElement();
    },
  );

  it("shows on the only active manager's own page Edit, the rule and the notice, never a button (AC22)", async () => {
    await openPage(sam);

    expect(facts()[2]).toEqual(['Status', 'Active']);
    expect(actionsAsRead()).toEqual(['alert: ', 'link: Edit', 'rule', lastManagerNotice]);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    // Plain content: no role, no live region and no focus stop of its own (status-messages.md).
    const notice = within(main()).getByText(lastManagerNotice).closest('div:not([class*="body"])');
    expect(notice).not.toHaveAttribute('role');
    expect(notice).not.toHaveAttribute('aria-live');
    expect(notice).not.toHaveAttribute('tabindex');
    expect(notice?.querySelector('[tabindex], a, button')).toBeNull();
    expect(document.querySelector('[disabled]')).toBeNull();
  });

  it('shows the notice only on your own page: Jo Smith, with Sam Lee the only active manager, has both', async () => {
    await openPage(jo);

    expect(within(main()).queryByText(lastManagerNotice)).not.toBeInTheDocument();
    expect(pageButton('Reset sign-in')).toBeInTheDocument();
    expect(pageButton('Deactivate')).toBeInTheDocument();
  });

  it('makes the Status row alone a target that script can focus, out of the tab order, with no live role', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${jo.id}`);
    await settle();

    const rows = Array.from(main().querySelector('dl')?.children ?? []);
    expect(rows.map((row) => row.getAttribute('tabindex'))).toEqual([null, null, '-1']);
    expect(statusRow()).not.toHaveAttribute('role');
    expect(statusRow()).not.toHaveAttribute('aria-live');
    expect(statusRow()).not.toHaveFocus();
  });

  it('shows Invited Alex Park with Edit, Resend invite under its description, its line, then Deactivate', async () => {
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
    expect(actionsAsRead()).toEqual([
      'alert: ',
      'link: Edit',
      'Sends a new setup link. The old link stops working.',
      'button: Resend invite',
      'rule',
      deactivateDescription,
      'button: Deactivate',
    ]);
    expect(pageButton('Deactivate')).toHaveAccessibleDescription(deactivateDescription);
    const [resend] = within(main()).getAllByRole('button');
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

  it('has its back link, named "Users", to the list, in the page bar outside <main>', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${alex.id}`);
    await settle();

    const link = backLink();
    expect(link).toHaveAccessibleName('Users');
    expect(link).toHaveAttribute('href', '/manage/users');
    expect(pageBar()).toContainElement(link);
    expect(main()).not.toContainElement(link);
  });

  it('from a pasted address, pushes the list by its back link, which focuses the <h1> at the top', async () => {
    serve(usersPath, json(people));
    // After the list, as the tab may hold it, but opened afresh, so that nothing says that the list opened the page.
    const router = renderApp(['/manage/users', `/manage/users/${alex.id}`]);
    await settle();
    Object.defineProperty(window, 'scrollY', { value: 300, configurable: true, writable: true });

    fireEvent.click(backLink()!);
    await settle();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(pageHeading()).toHaveFocus();
    expect(window.scrollY).toBe(0);
  });

  it('opens from its row of the list, with no router state, and goes back there by its back link', async () => {
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

    fireEvent.click(backLink()!);
    await settle();

    // Back one entry, adding none, so that Forward reaches the user again.
    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/users');
    expect(within(main()).getByRole('link', { name: 'Alex Park' })).toHaveFocus();
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe(`/manage/users/${alex.id}`);
  });

  it.each(['ctrlKey', 'metaKey', 'shiftKey'])(
    'leaves a click on its back link with %s to the browser, which follows the href, though the list opened it',
    async (modifier) => {
      serve(usersPath, json(people));
      const router = renderApp('/manage/users');
      await settle();
      fireEvent.click(within(main()).getByRole('link', { name: 'Alex Park' }));
      await settle();
      const { location } = router.state;
      // Whether the click was prevented by the time it reached the window, which then prevents it, as jsdom cannot
      // follow a link to another page.
      const prevented: boolean[] = [];
      const followHref = (event: MouseEvent) => {
        prevented.push(event.defaultPrevented);
        event.preventDefault();
      };
      window.addEventListener('click', followHref);
      onTestFinished(() => window.removeEventListener('click', followHref));

      fireEvent.click(backLink()!, { [modifier]: true });
      await settle();

      expect(prevented).toEqual([false]);
      expect(router.state.location).toBe(location);
    },
  );
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

  it('on a 412 whose list shows Alex now Active, takes Resend invite away, focus going to the Status row', async () => {
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
    expect(statusRow()).toHaveFocus();
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

  it('says nothing of a 401, which the session flow has: sign-in again, over the page', async () => {
    await openAlex();
    serve(setupLinkPath, problem(401));

    pressResend();
    await answered();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    // The page, hidden under it as it was, with nothing in its error region or Resend invite's line.
    const hiddenPage = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenPage === null) {
      throw new Error('No page is hidden in <main>.');
    }

    const kept = within(hiddenPage);
    expect(kept.getByRole('alert', { hidden: true })).toBeEmptyDOMElement();
    const resend = kept.getByRole('button', { name: 'Resend invite', hidden: true });
    expect(resend).not.toHaveAttribute('aria-disabled');
    expect(resend.nextElementSibling).toBeEmptyDOMElement();
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

// The words of users.md's *Dialogs* for Jo Smith, with the typographic apostrophe of src/copy.
const resetJoTitle = 'Reset sign-in for Jo Smith?';
const resetJoBody = [
  'Only do this after you have checked, by phone or in person, that Jo Smith is the one asking.',
  'Their current way of signing in will be removed, and they will be signed out everywhere. A new setup link will ' +
    'be emailed to jo.smith@example.org.',
];
const resetJo = 'Reset sign-in for Jo Smith';
const someoneChangedJoGoBack =
  'Someone else has just changed Jo Smith’s account. Go back to see the latest details.';
const someoneChangedJo = 'Someone else has just changed Jo Smith’s account. The page now shows the latest details.';

// Sam Lee and a second Active manager, Kim Tran, so that Sam Lee's own page offers Reset sign-in and Deactivate.
const twoManagers = [...people, kim];

// Jo Smith, Deactivated on 14 October, as the list gives them after a deactivation.
const joDeactivated = withChanged(people, jo, { status: 'Deactivated', deactivatedAtUtc: '2026-10-14T05:00:00Z' });

/** The dialog's alert line, above its buttons. */
const dialogAlert = () => within(dialog()).getByRole('alert');

/** The dialog's status line, below its buttons, which says that it is still busy after 1 s. */
const dialogStatus = () => within(dialog()).getByRole('status');

describe('Reset sign-in', () => {
  const resetPath = changePath(jo, 'setup-link');

  it('opens its dialog on its title, its confirm button red, sending nothing; Go back, Escape close it', async () => {
    await openPage(jo);

    const trigger = press('Reset sign-in');

    expect(dialog()).toHaveAttribute('role', 'alertdialog');
    expect(dialog()).toHaveAccessibleName(resetJoTitle);
    expect(dialogBody()).toEqual(resetJoBody);
    const title = within(dialog()).getByRole('heading', { level: 2 });
    expect(title).toHaveAttribute('tabindex', '-1');
    expect(title).toHaveFocus();
    expect(dialogButtons()).toEqual([resetJo, 'Go back']);
    expect(dialogButton(resetJo)).toHaveClass(...buttonClass('warning').split(' '));
    expect(dialogButton('Go back')).toHaveClass(...buttonClass('secondary').split(' '));
    // The trigger is not red: only the dialog's last step is (users.md component C item 6).
    expect(trigger).toHaveClass(...buttonClass('secondary').split(' '));
    expect(dialogAlert()).toBeEmptyDOMElement();
    expect(dialogStatus()).toBeEmptyDOMElement();

    pressInDialog('Go back');
    await settle();
    expect(openDialog()).toBeNull();

    press('Reset sign-in');
    closeRequest();
    await settle();
    expect(openDialog()).toBeNull();
    expect(sentTo(resetPath)).toEqual([]);
    expect(facts()[2]).toEqual(['Status', 'Active']);
  });

  it('sends one POST with If-Match, busy until the list comes, then shows Jo Invited, focus on Status', async () => {
    await openPage(jo);
    serve(resetPath, noContent(sentAt0512));
    const list = held(json(withChanged(people, jo, { status: 'Invited' })));
    serve(usersPath, list.answer);
    press('Reset sign-in');

    const confirm = pressInDialog(resetJo);
    fireEvent.click(confirm);
    await answered();

    expect(sentTo(resetPath)).toEqual([{ method: 'POST', ifMatch: `"stamp-${jo.id}"` }]);
    // Open and busy until the page holds Jo's new stamp.
    expect(confirm).toHaveAttribute('aria-disabled', 'true');
    expect(dialogButton('Go back')).toHaveAttribute('aria-disabled', 'true');

    list.release();
    await answered();

    expect(openDialog()).toBeNull();
    expect(facts()[2]).toEqual(['Status', 'Invited']);
    expect(statusRow()).toHaveFocus();
    // Resend invite in its place, and no banner: the error region and Resend invite's line are empty.
    expect(actionsAsRead()).toEqual([
      'alert: ',
      'link: Edit',
      'Sends a new setup link. The old link stops working.',
      'button: Resend invite',
      'rule',
      deactivateDescription,
      'button: Deactivate',
    ]);
    expect(resendLine()).toBeEmptyDOMElement();
    expect(line()).toBeEmptyDOMElement();
    expect(sentTo(resetPath)).toHaveLength(1);
  });

  it('while busy ignores presses and Escape, keeps its labels, and says "Resetting sign-in…" from 1 s', async () => {
    await openPage(jo);
    serve(resetPath, noAnswer);
    press('Reset sign-in');

    const confirm = pressInDialog(resetJo);
    pressInDialog(resetJo);
    pressInDialog('Go back');
    closeRequest();
    await wait(999);

    expect(openDialog()).not.toBeNull();
    expect(dialogButtons()).toEqual([resetJo, 'Go back']);
    expect(confirm).toHaveAttribute('aria-disabled', 'true');
    expect(dialogButton('Go back')).toHaveAttribute('aria-disabled', 'true');
    expect(dialogStatus()).toBeEmptyDOMElement();

    await wait(1);
    expect(dialogStatus()).toHaveTextContent('Resetting sign-in…');
    expect(dialogButtons()).toEqual([resetJo, 'Go back']);
    expect(sentTo(resetPath)).toHaveLength(1);
    expect(document.querySelector('[disabled]')).toBeNull();
  });

  it('on a 412 takes away its confirm button and says so, focus on Go back, the page fetched anew behind', async () => {
    await openPage(jo);
    serve(resetPath, problem(412, 'precondition.failed'));
    serve(usersPath, json(withChanged(people, jo, { displayName: 'Jo Smithers' })));
    press('Reset sign-in');

    pressInDialog(resetJo);
    await answered();

    expect(dialogAlert().textContent).toBe(someoneChangedJoGoBack);
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();
    expect(dialogButton('Go back')).not.toHaveAttribute('aria-disabled');
    // The dialog keeps the words it opened with, while the page behind it shows Jo as they are now.
    expect(dialog()).toHaveAccessibleName(resetJoTitle);
    expect(pageHeading()).toHaveAccessibleName('Jo Smithers');

    pressInDialog('Go back');
    await settle();

    expect(openDialog()).toBeNull();
    expect(pageButton('Reset sign-in')).toBeInTheDocument();
    expect(sentTo(resetPath)).toHaveLength(1);
  });

  it('after a 412 whose list shows Jo now Deactivated, Go back leaves focus on the Status row', async () => {
    await openPage(jo);
    serve(resetPath, problem(412, 'precondition.failed'));
    serve(usersPath, json(joDeactivated));
    press('Reset sign-in');
    pressInDialog(resetJo);
    await answered();

    pressInDialog('Go back');
    await settle();

    expect(openDialog()).toBeNull();
    expect(facts()[2]).toEqual(['Status', 'Deactivated']);
    expect(pageButton('Reset sign-in')).toBeNull();
    expect(statusRow()).toHaveFocus();
  });

  it('on your own page with a second active manager has the "your own" words, and its 204 loads afresh', async () => {
    await openPage(sam, twoManagers);
    serve(changePath(sam, 'setup-link'), noContent(sentAt0512));
    const listsBefore = sent(usersPath);

    press('Reset sign-in');

    expect(dialog()).toHaveAccessibleName('Reset your own sign-in?');
    expect(dialogBody()).toEqual([
      'You will be signed out everywhere now. A new setup link will be emailed to sam.lee@example.org.',
    ]);
    expect(dialogButtons()).toEqual(['Reset my sign-in', 'Go back']);
    expect(dialogButton('Reset my sign-in')).toHaveClass(...buttonClass('warning').split(' '));

    pressInDialog('Reset my sign-in');
    await answered();

    // Your session has ended, so the app loads afresh, on sign-in, with no list fetched before it goes.
    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(sent(usersPath)).toBe(listsBefore);
    expect(dialogButton('Reset my sign-in')).toHaveAttribute('aria-disabled', 'true');
  });

  it('on your own page, a 409 user.last_manager gives the "you" words with Go back alone, focused', async () => {
    await openPage(sam, twoManagers);
    serve(changePath(sam, 'setup-link'), problem(409, 'user.last_manager'));
    // Kim Tran deactivated themselves at the same moment, so the list fetched again has Sam Lee the only one.
    serve(usersPath, json(withChanged(twoManagers, kim, { status: 'Deactivated' })));
    press('Reset sign-in');

    pressInDialog('Reset my sign-in');
    await answered();

    expect(dialogAlert().textContent).toBe(
      'You are the only active manager, so you cannot reset your own sign-in. Make someone else a manager first.',
    );
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();
    expect(within(main()).getByText(lastManagerNotice)).toBeInTheDocument();

    pressInDialog('Go back');
    await settle();

    expect(openDialog()).toBeNull();
    expect(statusRow()).toHaveFocus();
    expect(fullPageLoad).not.toHaveBeenCalled();
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: 'Not reset: no connection. Try again.' },
    { given: 'a 500', answer: problem(500), sentence: 'Not reset: something went wrong. Try again.' },
    {
      given: 'a 409 user.not_active',
      answer: problem(409, 'user.not_active'),
      sentence: 'Not reset: something went wrong. Try again.',
    },
  ])('on $given says so, both buttons ready, focus on the confirm button, sending nothing by itself', async (case_) => {
    await openPage(jo);
    serve(resetPath, case_.answer);
    const listsBefore = sent(usersPath);
    press('Reset sign-in');

    const confirm = pressInDialog(resetJo);
    await answered();

    expect(dialogAlert().textContent).toBe(case_.sentence);
    expect(dialogButtons()).toEqual([resetJo, 'Go back']);
    expect(confirm).toHaveFocus();
    expect(confirm).not.toHaveAttribute('aria-disabled');
    expect(dialogButton('Go back')).not.toHaveAttribute('aria-disabled');
    expect(sent(usersPath)).toBe(listsBefore);

    await wait(60_000);
    expect(sentTo(resetPath)).toHaveLength(1);
  });

  it('empties its alert line as the confirm button is pressed again', async () => {
    await openPage(jo);
    serve(resetPath, noConnection);
    press('Reset sign-in');
    pressInDialog(resetJo);
    await answered();
    expect(dialogAlert().textContent).toBe('Not reset: no connection. Try again.');
    serve(resetPath, noAnswer);

    pressInDialog(resetJo);

    expect(dialogAlert()).toBeEmptyDOMElement();
    await settle();
    expect(sentTo(resetPath)).toHaveLength(2);
  });

  it('on a 401 closes the dialog before view C shows over the page, which keeps Reset sign-in', async () => {
    await openPage(jo);
    serve(resetPath, problem(401));
    const shownWithViewC: (HTMLDialogElement | null)[] = [];
    const close = vi.spyOn(HTMLDialogElement.prototype, 'close');
    press('Reset sign-in');

    pressInDialog(resetJo);
    // As view C's <h1> mounts, the dialog has already been closed (confirm-dialog.md *Behaviour* 8).
    const observer = new MutationObserver(() => {
      if (document.getElementById('signed-out-heading') !== null && shownWithViewC.length === 0) {
        shownWithViewC.push(openDialog());
      }
    });
    observer.observe(document.body, { childList: true, subtree: true });
    await answered();
    observer.disconnect();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(close).toHaveBeenCalled();
    expect(shownWithViewC).toEqual([null]);
    expect(openDialog()).toBeNull();
    const hiddenPage = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenPage === null) {
      throw new Error('No page is hidden in <main>.');
    }

    expect(within(hiddenPage).getByRole('button', { name: 'Reset sign-in', hidden: true })).toBeInTheDocument();
    expect(within(hiddenPage).getAllByRole('alert', { hidden: true })[0]).toBeEmptyDOMElement();
    expect(sentTo(resetPath)).toHaveLength(1);
  });

  it('closed by the browser while busy, shows how its request went on the page: a 412 above the actions', async () => {
    await openPage(jo);
    const answer = held(problem(412, 'precondition.failed'));
    serve(resetPath, answer.answer);
    serve(usersPath, json(withChanged(people, jo, {})));
    press('Reset sign-in');
    pressInDialog(resetJo);

    // A second Escape, which a browser may let through though the first was stopped (confirm-dialog.md *Behaviour* 7).
    act(() => {
      dialog().close();
    });
    await settle();
    expect(openDialog()).toBeNull();
    answer.release();
    await answered();

    expect(actionAlert().textContent).toBe(someoneChangedJo);
    expect(openDialog()).toBeNull();
  });
});

// The words of users.md's *Dialogs* for Deactivate.
const deactivateJoBody = 'Jo Smith will be signed out everywhere now and can’t sign in. Their notes stay.';

describe('Deactivate', () => {
  const deactivatePath = changePath(jo, 'deactivate');

  it("opens its dialog on its title, with design.md's words and a blue confirm button, sending nothing", async () => {
    await openPage(jo);

    press('Deactivate');

    expect(dialog()).toHaveAccessibleName('Deactivate Jo Smith?');
    expect(dialog()).toHaveAccessibleDescription(deactivateJoBody);
    expect(dialogBody()).toEqual([deactivateJoBody]);
    expect(within(dialog()).getByRole('heading', { level: 2 })).toHaveFocus();
    expect(dialogButtons()).toEqual(['Deactivate Jo Smith', 'Go back']);
    expect(dialogButton('Deactivate Jo Smith')).toHaveClass(...buttonClass('primary').split(' '));
    expect(dialogAlert()).toBeEmptyDOMElement();
    expect(dialogStatus()).toBeEmptyDOMElement();

    closeRequest();
    await settle();
    expect(openDialog()).toBeNull();
    press('Deactivate');
    pressInDialog('Go back');
    await settle();
    expect(openDialog()).toBeNull();
    expect(sentTo(deactivatePath)).toEqual([]);
  });

  it("has an Invited person's own words, of the setup link that will stop working", async () => {
    await openPage(alex);

    press('Deactivate');

    expect(dialog()).toHaveAccessibleName('Deactivate Alex Park?');
    expect(dialogBody()).toEqual(['Alex Park has not set up their account yet. Their setup link will stop working.']);
    expect(dialogButtons()).toEqual(['Deactivate Alex Park', 'Go back']);
  });

  it('sends one POST with If-Match, then shows Jo Deactivated, focus on Status, Reactivate alone', async () => {
    await openPage(jo);
    serve(deactivatePath, noContent());
    const list = held(json(joDeactivated));
    serve(usersPath, list.answer);
    press('Deactivate');

    const confirm = pressInDialog('Deactivate Jo Smith');
    fireEvent.click(confirm);
    await wait(999);
    expect(dialogStatus()).toBeEmptyDOMElement();
    await wait(1);
    expect(dialogStatus()).toHaveTextContent('Deactivating…');
    expect(dialogButtons()).toEqual(['Deactivate Jo Smith', 'Go back']);

    list.release();
    await answered();

    expect(sentTo(deactivatePath)).toEqual([{ method: 'POST', ifMatch: `"stamp-${jo.id}"` }]);
    expect(openDialog()).toBeNull();
    expect(facts()[2]).toEqual(['Status', 'Deactivated']);
    expect(statusRow()).toHaveFocus();
    expect(actionsAsRead()).toEqual(['alert: ', 'link: Edit', 'rule', reactivateDescription, 'button: Reactivate']);
    expect(line()).toBeEmptyDOMElement();
  });

  it('on your own page with a second active manager has the "your own" words, and its 204 loads afresh', async () => {
    await openPage(sam, twoManagers);
    serve(changePath(sam, 'deactivate'), noContent());
    const listsBefore = sent(usersPath);

    press('Deactivate');

    expect(dialog()).toHaveAccessibleName('Deactivate your own account?');
    expect(dialogBody()).toEqual(['You will be signed out everywhere now and cannot sign in again. Your notes stay.']);
    expect(dialogButtons()).toEqual(['Deactivate my account', 'Go back']);

    pressInDialog('Deactivate my account');
    await answered();

    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(sent(usersPath)).toBe(listsBefore);
  });

  it('on a 412 takes away its confirm button and says so, focus on Go back', async () => {
    await openPage(jo);
    serve(deactivatePath, problem(412, 'precondition.failed'));
    serve(usersPath, json(withChanged(people, jo, {})));
    press('Deactivate');

    pressInDialog('Deactivate Jo Smith');
    await answered();

    expect(dialogAlert().textContent).toBe(someoneChangedJoGoBack);
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();
  });

  it('on your own page, a 409 user.last_manager gives the "you" words with Go back alone', async () => {
    await openPage(sam, twoManagers);
    serve(changePath(sam, 'deactivate'), problem(409, 'user.last_manager'));
    serve(usersPath, json(withChanged(twoManagers, kim, { role: 'Worker' })));
    press('Deactivate');

    pressInDialog('Deactivate my account');
    await answered();

    expect(dialogAlert().textContent).toBe(
      'You are the only active manager, so you cannot deactivate your account. Make someone else a manager first.',
    );
    expect(dialogButtons()).toEqual(['Go back']);
    expect(dialogButton('Go back')).toHaveFocus();
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: 'Not deactivated: no connection. Try again.' },
    { given: 'a 500', answer: problem(500), sentence: 'Not deactivated: something went wrong. Try again.' },
  ])('on $given says so, both buttons ready, focus on the confirm button, sending nothing by itself', async (case_) => {
    await openPage(jo);
    serve(deactivatePath, case_.answer);
    press('Deactivate');

    const confirm = pressInDialog('Deactivate Jo Smith');
    await answered();

    expect(dialogAlert().textContent).toBe(case_.sentence);
    expect(confirm).toHaveFocus();
    expect(confirm).not.toHaveAttribute('aria-disabled');

    await wait(60_000);
    expect(sentTo(deactivatePath)).toHaveLength(1);
  });
});

describe('Reactivate', () => {
  const reactivatePath = changePath(chris, 'reactivate');

  it('sends one POST with If-Match, no dialog, busy, "Reactivating…" from 400 ms, then Active, focused', async () => {
    await openPage(chris);
    serve(reactivatePath, noContent());
    const list = held(json(withChanged(people, chris, { status: 'Active', deactivatedAtUtc: null })));
    serve(usersPath, list.answer);

    const button = press('Reactivate');
    press('Reactivate');
    await wait(399);
    press('Reactivate');

    expect(openDialog()).toBeNull();
    expect(sentTo(reactivatePath)).toEqual([{ method: 'POST', ifMatch: `"stamp-${chris.id}"` }]);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).toHaveAccessibleName('Reactivate');
    await wait(1);
    expect(button).toHaveAccessibleName('Reactivating…');
    act(() => {
      vi.advanceTimersToNextFrame();
    });
    expect(main().querySelector('p.visually-hidden[role="status"]')).toHaveTextContent('Reactivating…');

    list.release();
    await answered();

    expect(facts()[2]).toEqual(['Status', 'Active']);
    expect(statusRow()).toHaveFocus();
    expect(actionsAsRead()).toEqual(['alert: ', 'link: Edit', ...resetThenDeactivate]);
    expect(sentTo(reactivatePath)).toHaveLength(1);
  });

  it('shows Invited, with Resend invite, focus on Status, for someone with no way to sign in', async () => {
    await openPage(chris);
    serve(reactivatePath, noContent());
    serve(
      usersPath,
      json(withChanged(people, chris, { status: 'Invited', activatedAtUtc: null, deactivatedAtUtc: null })),
    );

    press('Reactivate');
    await answered();

    expect(facts()[2]).toEqual(['Status', 'Invited']);
    expect(statusRow()).toHaveFocus();
    expect(pageButton('Resend invite')).toBeInTheDocument();
    expect(pageButton('Deactivate')).toBeInTheDocument();
  });

  it('on a 412 fetches the list, then says so, focus kept on Reactivate while Chris is still Deactivated', async () => {
    await openPage(chris);
    serve(reactivatePath, problem(412, 'precondition.failed'));
    serve(usersPath, json(withChanged(people, chris, { displayName: 'Chris Nguyen' })));

    const button = press('Reactivate');
    await answered();

    // Named as the page showed them as it was pressed, which now shows them as they are.
    expect(actionAlert().textContent).toBe(
      'Someone else has just changed Chris Ng’s account. The page now shows the latest details.',
    );
    expect(pageHeading()).toHaveAccessibleName('Chris Nguyen');
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');

    await wait(60_000);
    expect(sentTo(reactivatePath)).toHaveLength(1);
    serve(reactivatePath, noAnswer);
    press('Reactivate');
    expect(actionAlert()).toBeEmptyDOMElement();
    await settle();
    expect(sentTo(reactivatePath).map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${chris.id}"`, '"stamp-2"']);
  });

  it('on a 412 whose list shows Chris now Active, focus goes to the Status row, as Reactivate has gone', async () => {
    await openPage(chris);
    serve(reactivatePath, problem(412, 'precondition.failed'));
    serve(usersPath, json(withChanged(people, chris, { status: 'Active', deactivatedAtUtc: null })));

    press('Reactivate');
    await answered();

    expect(actionAlert().textContent).toBe(
      'Someone else has just changed Chris Ng’s account. The page now shows the latest details.',
    );
    expect(pageButton('Reactivate')).toBeNull();
    expect(statusRow()).toHaveFocus();
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: 'Not reactivated: no connection. Try again.' },
    { given: 'a 500', answer: problem(500), sentence: 'Not reactivated: something went wrong. Try again.' },
  ])('on $given says so above the actions, keeps focus, and sends nothing by itself', async (case_) => {
    await openPage(chris);
    serve(reactivatePath, case_.answer);
    const listsBefore = sent(usersPath);

    const button = press('Reactivate');
    await answered();

    expect(actionAlert().textContent).toBe(case_.sentence);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(sent(usersPath)).toBe(listsBefore);

    await wait(60_000);
    expect(sentTo(reactivatePath)).toHaveLength(1);
  });

  it('says nothing of a 401, which the session flow has', async () => {
    await openPage(chris);
    serve(reactivatePath, problem(401));

    press('Reactivate');
    await answered();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    const hiddenPage = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenPage === null) {
      throw new Error('No page is hidden in <main>.');
    }

    expect(within(hiddenPage).getAllByRole('alert', { hidden: true })[0]).toBeEmptyDOMElement();
    expect(within(hiddenPage).getByRole('button', { name: 'Reactivate', hidden: true })).not.toHaveAttribute(
      'aria-disabled',
    );
  });
});
