import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me, type Role } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { fullPageLoad } from '../../routes/fullPageLoad.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminUser, UserStatus } from './usersQuery.ts';

// jsdom does not navigate, so the tests see the full page load that your own new address asks for.
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

const people = [sam, chris, jo, alex];

// A second Active manager, with whom Sam Lee is no longer the only one (A26).
const kim = madeUp('b2d6f0a4-7e31-4c9b-8a5d-1f3e7c2b9d60', 'Kim Tran', 'kim.tran@example.org', 'Active', 'Manager');

/** The address of `user`'s Edit user page, and of the `PUT` that saves it (design.md §6.6). */
const editPath = (user: AdminUser) => `/manage/users/${user.id}/edit`;
const userPath = (user: AdminUser) => `/api/admin/users/${user.id}`;

/** `users` with `user` as `changed` makes them, and a new stamp, as the list gives them after a change to them. */
function withChanged(users: readonly AdminUser[], user: AdminUser, changed: Partial<AdminUser>): AdminUser[] {
  return users.map((listed) =>
    listed.id === user.id ? { ...listed, concurrencyStamp: 'stamp-2', ...changed } : listed,
  );
}

// A made-up ID that is on no list.
const unknownId = '4f2a9c7e-1b38-4d65-a0e9-8c3b5d1f7a26';

// The words of users.md's component D and *States* › Edit user, with the typographic apostrophe of src/copy.
const nameHint = 'Shown on the notes they write';
const emailHint = 'Changing the email resets sign-in and sends a setup link to the new address.';
const ownEmailHint =
  'Changing your email resets your sign-in. You will be signed out, and a setup link will be sent to the new address.';
const lastManagerNotice =
  'You are the only active manager, so you cannot change your email or make yourself a worker. First make someone ' +
  'else a manager.';
const lastManagerRole =
  'You are the only active manager, so you cannot make yourself a worker. Make someone else a manager first.';
const lastManagerSupport =
  'You are the only active manager, so you cannot make yourself Support. Make someone else a manager first.';
const lastManagerEmail =
  'You are the only active manager, so you cannot change your email. Make someone else a manager first.';
const changedWhileEditing =
  'Someone else changed this user while you were editing. Check the details, then save again.';
const enterName = 'Enter the person’s name';
const nameTooLong = 'Name must be 100 characters or less';
const enterEmail = 'Enter an email address';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const emailInUse =
  'Another account already uses this email address. Use a different one, or find the person in the Users list.';
const chooseRole = 'Choose Worker, Manager or Support';
const noConnectionSentence = 'Not saved: no connection. Try again.';
const somethingWentWrong = 'Not saved: something went wrong. Try again.';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** The `200` with no body and no `ETag` that `PUT /api/admin/users/{id}` answers (design.md §6.6, D78). */
const saved: Answer = () => Promise.resolve(new Response(null, { status: 200 }));

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code` and `errors`, if any. */
function problem(status: number, extensions: object = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** The antiforgery endpoint's answer: a `204` with the token in a header (design.md §9.8 item 2). */
const token: Answer = () =>
  Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));

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

/** Each save of `user` sent, as its `If-Match` and its body. */
function savesOf(user: AdminUser) {
  return fetchMock.mock.calls
    .filter(([path, init]) => path === userPath(user) && init?.method === 'PUT')
    .map(([, init]) => ({
      ifMatch: new Headers(init?.headers).get('If-Match'),
      body: JSON.parse(init?.body as string) as unknown,
    }));
}

/** How many times the list has been asked for. */
function listsAskedFor(): number {
  return fetchMock.mock.calls.filter(([path, init]) => path === usersPath && init?.method === 'GET').length;
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
  const entries = typeof path === 'string' ? [path] : path;
  const initialEntries = entries.map((entry) => ({ ...parsePath(entry), key: crypto.randomUUID() }));
  const router = createMemoryRouter(routes, { initialEntries, initialIndex: entries.length - 1 });
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

/** Lets a request answer, the users refetch that follows it answer too, and TanStack hand on the new state. */
async function answered() {
  await settle();
  await settle();
  await settle();
}

/** Opens `user`'s Edit user page for `me`, from a list of `users`, ready to save. */
async function openEdit(user: AdminUser, users: readonly AdminUser[] = people, me: Me = manager) {
  serve(usersPath, json(users));
  serve(antiforgeryPath, token);
  const router = renderApp(editPath(user), me);
  await settle();
  return router;
}

/** As the browser does when the window comes back into view, on which TanStack refetches the queries that opt in. */
function returnToWindow() {
  act(() => {
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
  });
}

const main = () => screen.getByRole('main');
const pageHeading = () => within(main()).getByRole('heading', { level: 1 });
const nameField = () => within(main()).getByRole('textbox', { name: 'Name' });
const emailField = () => within(main()).getByRole('textbox', { name: 'Email address' });
const roleGroup = () => within(main()).getByRole('group', { name: 'Role' });
const workerRadio = () => within(main()).getByRole('radio', { name: 'Worker' });
const managerRadio = () => within(main()).getByRole('radio', { name: 'Manager' });
const supportRadio = () => within(main()).getByRole('radio', { name: 'Support (IT operator)' });
const saveButton = () => within(main()).getByRole('button', { name: /^(Save|Saving…)$/ });
const alertLine = () => within(main()).getByRole('alert');

/** The shell's before-main bar slot, between `<header>` and `<main>`. */
function pageBar(): HTMLElement {
  const slot = document.getElementById('page-bar');
  if (slot === null) {
    throw new Error('The shell rendered no #page-bar.');
  }

  return slot;
}

/** The region's line, the status region in `<main>` beside the page's visually hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The error summary, by its heading, or `null` when none shows. */
function summary(): HTMLElement | null {
  return screen.queryByRole('heading', { level: 2, name: 'There is a problem' })?.parentElement ?? null;
}

/** An item of the error summary, as its text and the `href` of its link, if it has one. */
type SummaryItem = { text: string | null; href?: string | null };

/** The summary's items. */
function summaryItems(): SummaryItem[] {
  const shown = summary();
  if (shown === null) {
    return [];
  }

  return within(shown)
    .getAllByRole('listitem')
    .map((item) => {
      const link = item.querySelector('a');
      return link === null ? { text: item.textContent } : { text: item.textContent, href: link.getAttribute('href') };
    });
}

/** Types `value` into `field` as one change, as a paste is. */
function type(field: HTMLElement, value: string) {
  fireEvent.change(field, { target: { value } });
}

/** Focuses Save and presses it, as a person does, and returns it. */
function pressSave(): HTMLElement {
  const button = saveButton();
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Expects the fields to hold these values, the radio of `role` chosen. */
function expectKept(name: string, email: string, role: Role) {
  expect(nameField()).toHaveValue(name);
  expect(emailField()).toHaveValue(email);
  expect(workerRadio()).toHaveProperty('checked', role === 'Worker');
  expect(managerRadio()).toHaveProperty('checked', role === 'Manager');
}

/** The user page's Status row's value. */
function statusShown(): string | null | undefined {
  return main().querySelector('dl')?.children[2]?.querySelector('dd')?.textContent;
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
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
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.mocked(fullPageLoad).mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Edit user', () => {
  it("opens with Jo Smith's values and the hint for someone else, titled Edit user, with Manage marked", async () => {
    await openEdit(jo);

    expect(pageHeading()).toHaveAccessibleName('Edit user');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(document.title).toBe('Grow2Notes – Edit user');
    expectKept('Jo Smith', 'jo.smith@example.org', 'Worker');
    expect(nameField()).toHaveAccessibleDescription(nameHint);
    expect(emailField()).toHaveAccessibleDescription(emailHint);
    expect(emailField()).toHaveAttribute('type', 'email');
    for (const field of [nameField(), emailField()]) {
      expect(field).toHaveAttribute('autocomplete', 'off');
      expect(field).not.toHaveAttribute('maxlength');
      expect(field).not.toHaveAttribute('name');
    }

    expect(within(main()).queryByText(lastManagerNotice)).not.toBeInTheDocument();
    expect(summary()).toBeNull();
    expect(main().querySelector('[aria-invalid]')).toBeNull();
    expect(alertLine()).toBeEmptyDOMElement();
    expect(line()).toBeEmptyDOMElement();
    expect(document.querySelector('[disabled]')).toBeNull();
    expect(currentItems()).toEqual(['Manage true']);
  });

  it("starts the Role radios on Sam Lee's role, Manager, on their own page with the hint for themselves", async () => {
    await openEdit(sam, [...people, kim]);

    expectKept('Sam Lee', 'sam.lee@example.org', 'Manager');
    expect(emailField()).toHaveAccessibleDescription(ownEmailHint);
    expect(within(main()).queryByText(lastManagerNotice)).not.toBeInTheDocument();
  });

  it('has its back link, named by the user, to their page in the page bar, then its parts in <main>', async () => {
    await openEdit(alex);

    const back = within(pageBar()).getByRole('link');
    expect(back).toHaveAccessibleName('Alex Park');
    expect(back).toHaveAttribute('href', `/manage/users/${alex.id}`);
    expect(main()).not.toContainElement(back);
    const parts = [pageHeading(), nameField(), emailField(), roleGroup(), alertLine(), saveButton()];
    for (const [index, part] of parts.slice(1).entries()) {
      expect(parts[index].compareDocumentPosition(part)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    }

    expect(saveButton()).toHaveAttribute('type', 'submit');
    expect(saveButton().closest('form')).toHaveAttribute('novalidate');
  });

  it('shows the notice under the <h1> on your own page as the only active manager, the fields still open', async () => {
    await openEdit(sam);

    const notice = within(main()).getByText(lastManagerNotice);
    expect(pageHeading().compareDocumentPosition(notice)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(notice.compareDocumentPosition(nameField())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    // Plain content: no role, no live region and no focus stop of its own (status-messages.md).
    const box = notice.closest('div:not([class*="body"])');
    expect(box).not.toHaveAttribute('role');
    expect(box).not.toHaveAttribute('tabindex');
    expect(emailField()).toHaveAccessibleDescription(ownEmailHint);
    // Never a read-only look-alike (users.md component D item 5).
    expect(emailField()).not.toHaveAttribute('readonly');
    expect(workerRadio()).not.toHaveAttribute('readonly');
    expect(document.querySelector('[disabled]')).toBeNull();
  });

  it('shows its <h1> at once, no field, "Loading user…" from 1 s, then the fields, its <h1> kept focused', async () => {
    const list = held(json(people));
    serve(usersPath, list.answer);
    const router = renderApp('/manage');
    await settle();

    await act(() => router.navigate(editPath(jo)));
    expect(pageHeading()).toHaveAccessibleName('Edit user');
    expect(pageHeading()).toHaveFocus();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    // Until the list names them, by the page that the link goes to.
    expect(within(pageBar()).getByRole('link')).toHaveAccessibleName('User');
    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    await wait(1);
    expect(line().textContent).toBe('Loading user…');

    list.release();
    await settle();

    expectKept('Jo Smith', 'jo.smith@example.org', 'Worker');
    expect(line()).toBeEmptyDOMElement();
    expect(pageHeading()).toHaveFocus();
    expect(within(pageBar()).getByRole('link')).toHaveAccessibleName('Jo Smith');
  });

  it('keeps its <h1> above "This user did not load: …" and Try again, which focuses it as the list loads', async () => {
    serve(usersPath, noConnection);
    renderApp(editPath(jo));
    await wait(2_000);

    expect(pageHeading()).toHaveAccessibleName('Edit user');
    expect(line().textContent).toBe('This user did not load: no connection. Try again.');
    expect(pageHeading().compareDocumentPosition(line())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();

    const tryAgain = within(main()).getByRole('button', { name: 'Try again' });
    tryAgain.focus();
    fireEvent.click(tryAgain);
    await wait(2_000);
    expect(line().textContent).toBe('This user still did not load: no connection. Try again.');
    expect(tryAgain).toHaveFocus();

    serve(usersPath, json(people));
    fireEvent.click(tryAgain);
    await settle();
    await settle();

    expectKept('Jo Smith', 'jo.smith@example.org', 'Worker');
    expect(pageHeading()).toHaveAccessibleName('Edit user');
    expect(pageHeading()).toHaveFocus();
  });

  it('shows Page not found in place of the whole page for an ID not on the list', async () => {
    serve(usersPath, json(people));
    renderApp(`/manage/users/${unknownId}/edit`);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    expect(pageBar()).toBeEmptyDOMElement();
  });

  it("opens from the user page's Edit, and goes back by its back link, as Back does, to its <h1>", async () => {
    serve(usersPath, json(people));
    const router = renderApp(`/manage/users/${jo.id}`);
    await settle();
    fireEvent.click(within(main()).getByRole('link', { name: 'Edit' }));
    await settle();
    expect(router.state.location.pathname).toBe(editPath(jo));
    expect(pageHeading()).toHaveAccessibleName('Edit user');
    expect(pageHeading()).toHaveFocus();

    fireEvent.click(within(pageBar()).getByRole('link', { name: 'Jo Smith' }));
    await settle();

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe(`/manage/users/${jo.id}`);
    expect(pageHeading()).toHaveAccessibleName('Jo Smith');
    expect(pageHeading()).toHaveFocus();
  });

  it("from a pasted address, pushes the user's page by its back link", async () => {
    serve(usersPath, json(people));
    const router = renderApp([`/manage/users/${jo.id}`, editPath(jo)]);
    await settle();

    fireEvent.click(within(pageBar()).getByRole('link', { name: 'Jo Smith' }));
    await settle();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe(`/manage/users/${jo.id}`);
    expect(pageHeading()).toHaveFocus();
  });

  it('shows a worker Page not found, and sends nothing (AC1)', async () => {
    renderApp(editPath(jo), worker);
    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([path]) => path !== mePath)).toEqual([]);
  });
});

describe('Edit user, checked before it saves', () => {
  it('shows the fields at fault inline and in the focused summary, titled "Error: …", and sends nothing', async () => {
    await openEdit(jo);
    type(nameField(), '  ');
    type(emailField(), 'jo');

    pressSave();
    await settle();

    expect(summaryItems()).toEqual([
      { text: enterName, href: '#displayName' },
      { text: emailFormat, href: '#email' },
    ]);
    expect(summary()).toHaveFocus();
    expect(nameField()).toHaveAttribute('aria-invalid', 'true');
    expect(emailField()).toHaveAccessibleDescription(`${emailHint} Error: ${emailFormat}`);
    expect(document.title).toBe('Error: Grow2Notes – Edit user');
    expect(savesOf(jo)).toEqual([]);

    // Each message goes as its field is fixed, and the title's prefix with the last.
    type(nameField(), 'Jo Smith');
    expect(summaryItems()).toEqual([{ text: emailFormat, href: '#email' }]);
    type(emailField(), '');
    expect(summaryItems()).toEqual([{ text: enterEmail, href: '#email' }]);
    type(emailField(), 'jo.smith@example.org');
    await settle();
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Edit user');
  });

  it('says a name of 101 characters is too long, and keeps it whole', async () => {
    await openEdit(jo);
    type(nameField(), 'J'.repeat(101));

    pressSave();
    await settle();

    expect(summaryItems()).toEqual([{ text: nameTooLong, href: '#displayName' }]);
    expect(nameField()).toHaveValue('J'.repeat(101));
    expect(savesOf(jo)).toEqual([]);
  });

  it.each([
    {
      given: 'Worker',
      change: () => fireEvent.click(workerRadio()),
      items: [{ text: lastManagerRole, href: '#role-worker' }],
    },
    {
      given: 'Support',
      change: () => fireEvent.click(supportRadio()),
      items: [{ text: lastManagerSupport, href: '#role-worker' }],
    },
    {
      given: 'a new address',
      change: () => type(emailField(), 'sam.leigh@example.org'),
      items: [{ text: lastManagerEmail, href: '#email' }],
    },
    {
      given: 'both',
      change: () => {
        type(emailField(), 'sam.leigh@example.org');
        fireEvent.click(workerRadio());
      },
      items: [
        { text: lastManagerEmail, href: '#email' },
        { text: lastManagerRole, href: '#role-worker' },
      ],
    },
  ])('refuses $given on your own page as the only active manager, and sends nothing (AC35)', async (case_) => {
    await openEdit(sam);
    case_.change();

    pressSave();
    await settle();

    expect(summaryItems()).toEqual(case_.items);
    expect(summary()).toHaveFocus();
    expect(document.title).toBe('Error: Grow2Notes – Edit user');
    expect(savesOf(sam)).toEqual([]);
  });

  it("puts Worker's message under the legend until Manager is chosen again", async () => {
    await openEdit(sam);
    fireEvent.click(workerRadio());
    pressSave();
    await settle();

    expect(roleGroup()).toHaveAccessibleDescription(`Error: ${lastManagerRole}`);

    fireEvent.click(managerRadio());
    await settle();
    expect(summary()).toBeNull();
    expect(roleGroup()).not.toHaveAccessibleDescription();
    expect(document.title).toBe('Grow2Notes – Edit user');
  });

  it('sends a new name and your own address in other letter case as the only active manager (AC35, D76)', async () => {
    await openEdit(sam);
    serve(userPath(sam), saved);
    type(nameField(), 'Sam Leigh');
    type(emailField(), ' SAM.LEE@example.org ');

    pressSave();
    await answered();

    expect(savesOf(sam)).toEqual([
      {
        ifMatch: `"stamp-${sam.id}"`,
        body: { displayName: 'Sam Leigh', email: 'SAM.LEE@example.org', role: 'Manager' },
      },
    ]);
  });
});

describe('Edit user, saving', () => {
  it('sends one PUT with If-Match and the values trimmed, however often hit, with "Saving…" from 400 ms', async () => {
    await openEdit(jo);
    serve(userPath(jo), noAnswer);
    type(nameField(), ' Jo Smyth ');
    type(emailField(), ' jo.smyth@example.org ');
    fireEvent.click(managerRadio());

    const button = pressSave();
    pressSave();
    await wait(399);
    pressSave();

    expect(savesOf(jo)).toEqual([
      {
        ifMatch: `"stamp-${jo.id}"`,
        body: { displayName: 'Jo Smyth', email: 'jo.smyth@example.org', role: 'Manager' },
      },
    ]);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).toHaveAccessibleName('Save');
    expect(button).toHaveFocus();

    await wait(1);
    expect(button).toHaveAccessibleName('Saving…');
    act(() => {
      vi.advanceTimersToNextFrame();
    });
    expect(main().querySelector('p.visually-hidden[role="status"]')).toHaveTextContent('Saving…');
    // The fields stay as typed, and can still be changed.
    // The email field takes the spaces off by itself (HTML's value sanitization).
    expectKept(' Jo Smyth ', 'jo.smyth@example.org', 'Manager');
  });

  it.each([
    {
      given: "a new name and role for Jo Smith, whose page's Status stays Active",
      user: jo,
      typed: { displayName: 'Jo Smyth', role: 'Manager' as const },
      changed: { displayName: 'Jo Smyth', role: 'Manager' as const },
      status: 'Active',
    },
    {
      given: 'a new address for Active Jo Smith, who is then Invited',
      user: jo,
      typed: { email: 'jo.smyth@example.org' },
      changed: { email: 'jo.smyth@example.org', status: 'Invited' as const, activatedAtUtc: null },
      status: 'Invited',
    },
    {
      given: 'a new address for Deactivated Chris Ng, who stays Deactivated (D75)',
      user: chris,
      typed: { email: 'chris.ng@example.com' },
      changed: { email: 'chris.ng@example.com' },
      status: 'Deactivated',
    },
    {
      given: "Jo Smith's address in other letter case, which leaves their Status as it was (D76)",
      user: jo,
      typed: { email: 'Jo.Smith@example.org' },
      changed: { email: 'Jo.Smith@example.org' },
      status: 'Active',
    },
  ])(
    "saves $given: once the list is fetched again, replaces Edit with the user's page, its <h1> focused",
    async ({ user, typed, changed, status }) => {
      serve(usersPath, json(people));
      serve(antiforgeryPath, token);
      const router = renderApp([`/manage/users/${user.id}`, editPath(user)]);
      await settle();
      const list = held(json(withChanged(people, user, changed)));
      serve(userPath(user), saved);
      const before = listsAskedFor();
      serve(usersPath, list.answer);
      if (typed.displayName !== undefined) {
        type(nameField(), typed.displayName);
      }
      if (typed.email !== undefined) {
        type(emailField(), typed.email);
      }
      if (typed.role !== undefined) {
        fireEvent.click(managerRadio());
      }

      const button = pressSave();
      await answered();

      // Saved, and waiting, busy, for the list that holds the change (users.md *E. Data and helpers*, Mutations).
      expect(savesOf(user)).toHaveLength(1);
      expect(listsAskedFor()).toBe(before + 1);
      expect(router.state.location.pathname).toBe(editPath(user));
      expect(button).toHaveAttribute('aria-disabled', 'true');

      list.release();
      await answered();

      expect(router.state.location.pathname).toBe(`/manage/users/${user.id}`);
      expect(router.state.historyAction).toBe('REPLACE');
      expect(pageHeading()).toHaveAccessibleName(changed.displayName ?? user.displayName);
      expect(pageHeading()).toHaveFocus();
      expect(document.title).toBe('Grow2Notes – User');
      expect(statusShown()).toBe(status);
      // No banner (users.md component C item 2).
      for (const region of [...within(main()).getAllByRole('alert'), ...within(main()).getAllByRole('status')]) {
        expect(region).toBeEmptyDOMElement();
      }
      expect(fullPageLoad).not.toHaveBeenCalled();

      // Back goes to the page before the form, which the user's page has replaced.
      await act(() => router.navigate(-1));
      expect(router.state.location.pathname).toBe(`/manage/users/${user.id}`);
      expect(within(main()).queryByRole('heading', { level: 1, name: 'Edit user' })).not.toBeInTheDocument();
    },
  );

  it("on a 412 fetches the list, says so above Save, keeps the values, then saves with the new stamp", async () => {
    await openEdit(jo);
    serve(userPath(jo), problem(412, { code: 'precondition.failed' }));
    serve(usersPath, json(withChanged(people, jo, { displayName: 'Joanne Smith' })));
    const before = listsAskedFor();
    type(nameField(), 'Jo Smyth');
    fireEvent.click(managerRadio());

    const button = pressSave();
    await answered();

    expect(alertLine().textContent).toBe(changedWhileEditing);
    expect(listsAskedFor()).toBe(before + 1);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Edit user');
    // Every value as typed, and the email, untouched, as the form took it, not as the list now gives it.
    expectKept('Jo Smyth', 'jo.smith@example.org', 'Manager');

    serve(userPath(jo), saved);
    pressSave();
    expect(alertLine()).toBeEmptyDOMElement();
    await answered();

    expect(savesOf(jo).map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${jo.id}"`, '"stamp-2"']);
    expect(pageHeading()).toHaveAccessibleName('Joanne Smith');
    expect(pageHeading()).toHaveFocus();
  });

  it('changes no field and keeps its stamp as the list is fetched again while it is open', async () => {
    await openEdit(jo);
    type(nameField(), 'Jo Smyth');
    serve(usersPath, json(withChanged(people, jo, { displayName: 'Joanne Smith', email: 'joanne@example.org' })));

    returnToWindow();
    await answered();

    expectKept('Jo Smyth', 'jo.smith@example.org', 'Worker');
    serve(userPath(jo), noAnswer);
    pressSave();
    await settle();
    expect(savesOf(jo).map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${jo.id}"`]);
  });

  it("takes the user's values and stamp afresh on a history jump from another user's Edit user", async () => {
    serve(usersPath, json(people));
    serve(antiforgeryPath, token);
    const router = renderApp([editPath(jo), editPath(alex)]);
    await settle();
    expectKept('Alex Park', 'alex.park@example.org', 'Worker');

    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe(editPath(jo));
    expect(within(pageBar()).getByRole('link')).toHaveAccessibleName('Jo Smith');
    expectKept('Jo Smith', 'jo.smith@example.org', 'Worker');
    serve(userPath(jo), saved);
    pressSave();
    await answered();

    expect(savesOf(jo)).toEqual([
      { ifMatch: `"stamp-${jo.id}"`, body: { displayName: 'Jo Smith', email: 'jo.smith@example.org', role: 'Worker' } },
    ]);
    expect(savesOf(alex)).toEqual([]);
    expect(router.state.location.pathname).toBe(`/manage/users/${jo.id}`);
    expect(pageHeading()).toHaveAccessibleName('Jo Smith');
  });
});

describe('Edit user, your own account', () => {
  it('lands on Today as a worker, its <h1> focused, with no Manage in the nav and no banner (AC38)', async () => {
    const router = await openEdit(sam, [...people, kim]);
    serve(userPath(sam), saved);
    const before = listsAskedFor();
    // The shell asks who is signed in on the new entry, and the answer says the role has changed.
    serve(mePath, json({ ...manager, role: 'Worker' }));
    fireEvent.click(workerRadio());

    pressSave();
    await answered();

    expect(savesOf(sam)).toEqual([
      {
        ifMatch: `"stamp-${sam.id}"`,
        body: { displayName: 'Sam Lee', email: 'sam.lee@example.org', role: 'Worker' },
      },
    ]);
    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveTextContent(/^Today/);
    expect(pageHeading()).toHaveFocus();
    const nav = screen.getByRole('navigation', { name: 'Main' });
    expect(within(nav).queryByRole('link', { name: 'Manage' })).not.toBeInTheDocument();
    expect(within(main()).queryByRole('alert')).not.toBeInTheDocument();
    // The list is a manager's, which the server now refuses you (design.md §8.4).
    expect(listsAskedFor()).toBe(before);
    expect(fullPageLoad).not.toHaveBeenCalled();
  });

  it('lands on Today as Support, which shows the Support page with no nav and no banner', async () => {
    const router = await openEdit(sam, [...people, kim]);
    serve(userPath(sam), saved);
    const before = listsAskedFor();
    serve(mePath, json({ ...manager, role: 'Support' }));
    fireEvent.click(supportRadio());

    pressSave();
    await answered();

    expect(savesOf(sam)).toEqual([
      {
        ifMatch: `"stamp-${sam.id}"`,
        body: { displayName: 'Sam Lee', email: 'sam.lee@example.org', role: 'Support' },
      },
    ]);
    expect(router.state.location.pathname).toBe('/');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveTextContent('This account is only used to connect an AI assistant.');
    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument();
    expect(within(main()).queryByRole('alert')).not.toBeInTheDocument();
    expect(listsAskedFor()).toBe(before);
    expect(fullPageLoad).not.toHaveBeenCalled();
  });

  it('loads the app afresh after your own new address, which has ended your session, still busy (AC38)', async () => {
    await openEdit(sam, [...people, kim]);
    serve(userPath(sam), saved);
    const before = listsAskedFor();
    type(emailField(), 'sam.leigh@example.org');

    const button = pressSave();
    await answered();

    expect(fullPageLoad).toHaveBeenCalledOnce();
    expect(listsAskedFor()).toBe(before);
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('lands on your own page after your own address in other letter case, with no reload (D76)', async () => {
    const router = await openEdit(sam);
    serve(userPath(sam), saved);
    serve(usersPath, json(withChanged(people, sam, { email: 'Sam.Lee@example.org' })));
    type(emailField(), 'Sam.Lee@example.org');

    pressSave();
    await answered();

    expect(fullPageLoad).not.toHaveBeenCalled();
    expect(router.state.location.pathname).toBe(`/manage/users/${sam.id}`);
    expect(pageHeading()).toHaveAccessibleName('Sam Lee');
    expect(pageHeading()).toHaveFocus();
    expect(statusShown()).toBe('Active');
  });

  it('shows your own new name in the Account panel once saved', async () => {
    await openEdit(sam);
    serve(userPath(sam), saved);
    serve(usersPath, json(withChanged(people, sam, { displayName: 'Sam Leigh' })));
    serve(mePath, json({ ...manager, displayName: 'Sam Leigh' }));
    type(nameField(), 'Sam Leigh');

    pressSave();
    await answered();

    expect(pageHeading()).toHaveAccessibleName('Sam Leigh');
    fireEvent.click(screen.getByRole('button', { name: 'Account' }));
    expect(screen.getByText('Signed in as Sam Leigh')).toBeVisible();
  });
});

describe('Edit user, refused by the server', () => {
  it('shows an address that another account has on Email, linked from the focused summary, values kept', async () => {
    await openEdit(jo);
    serve(userPath(jo), problem(409, { code: 'user.email_in_use' }));
    type(emailField(), 'ALEX.PARK@example.org');

    pressSave();
    await answered();

    expect(summaryItems()).toEqual([{ text: emailInUse, href: '#email' }]);
    expect(summary()).toHaveFocus();
    expect(emailField()).toHaveAccessibleDescription(`${emailHint} Error: ${emailInUse}`);
    expect(document.title).toBe('Error: Grow2Notes – Edit user');
    expectKept('Jo Smith', 'ALEX.PARK@example.org', 'Worker');
    expect(alertLine()).toBeEmptyDOMElement();
  });

  it.each<{ given: string; errors: Record<string, string[]>; items: SummaryItem[] }>([
    {
      given: 'an address with a letter outside ASCII, under email',
      errors: { email: ['x'] },
      items: [{ text: emailFormat, href: '#email' }],
    },
    { given: 'a role, under role', errors: { role: ['x'] }, items: [{ text: chooseRole, href: '#role-worker' }] },
    {
      given: 'a name, under displayName',
      errors: { displayName: ['x'] },
      items: [{ text: nameTooLong, href: '#displayName' }],
    },
    {
      given: 'a field the form does not have, under an unknown key',
      errors: { organisationId: ['x'] },
      items: [{ text: somethingWentWrong }],
    },
  ])('shows a 422 for $given in its own words, with focus on the summary and every value kept', async (case_) => {
    await openEdit(jo);
    serve(userPath(jo), problem(422, { code: 'validation.failed', errors: case_.errors }));
    type(emailField(), 'zoë@example.org');

    pressSave();
    await answered();

    expect(summaryItems()).toEqual(case_.items);
    expect(summary()).toHaveFocus();
    expect(document.title).toBe('Error: Grow2Notes – Edit user');
    expectKept('Jo Smith', 'zoë@example.org', 'Worker');
    expect(alertLine()).toBeEmptyDOMElement();
  });

  it.each([
    {
      given: 'Worker',
      change: () => fireEvent.click(workerRadio()),
      items: [{ text: lastManagerRole, href: '#role-worker' }],
    },
    {
      given: 'Support',
      change: () => fireEvent.click(supportRadio()),
      items: [{ text: lastManagerSupport, href: '#role-worker' }],
    },
    {
      given: 'a new address',
      change: () => type(emailField(), 'sam.leigh@example.org'),
      items: [{ text: lastManagerEmail, href: '#email' }],
    },
  ])('shows the race\'s 409 user.last_manager for $given on that field, and then the notice', async (case_) => {
    await openEdit(sam, [...people, kim]);
    serve(userPath(sam), problem(409, { code: 'user.last_manager' }));
    // Kim Tran deactivated themselves as Sam Lee saved, so the list fetched again has Sam Lee the only one.
    serve(usersPath, json(withChanged([...people, kim], kim, { status: 'Deactivated' })));
    case_.change();

    pressSave();
    await answered();

    expect(summaryItems()).toEqual(case_.items);
    expect(summary()).toHaveFocus();
    expect(within(main()).getByText(lastManagerNotice)).toBeInTheDocument();
    expect(savesOf(sam)).toHaveLength(1);
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: noConnectionSentence },
    { given: 'a 500', answer: problem(500), sentence: somethingWentWrong },
    { given: 'a 404', answer: problem(404), sentence: somethingWentWrong },
  ])('says what $given means above Save, focus left on it, nothing cleared or sent again (AC33)', async (case_) => {
    await openEdit(jo);
    serve(userPath(jo), case_.answer);
    type(nameField(), 'Jo Smyth');

    const button = pressSave();
    await answered();

    expect(alertLine().textContent).toBe(case_.sentence);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Edit user');
    expectKept('Jo Smyth', 'jo.smith@example.org', 'Worker');
    await wait(60_000);
    expect(savesOf(jo)).toHaveLength(1);
  });

  it('says "no connection" for an answer that never comes, after its 10 seconds', async () => {
    await openEdit(jo);
    serve(userPath(jo), noAnswer);

    pressSave();
    await wait(10_000);
    await settle();

    expect(alertLine().textContent).toBe(noConnectionSentence);
    expect(saveButton()).not.toHaveAttribute('aria-disabled');
  });

  it('says nothing of a 401, which the session flow has, and keeps every value under sign-in again', async () => {
    await openEdit(jo);
    serve(userPath(jo), problem(401));
    type(nameField(), 'Jo Smyth');

    pressSave();
    await answered();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    const hiddenPage = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenPage === null) {
      throw new Error('No page is hidden in <main>.');
    }

    const kept = within(hiddenPage);
    expect(kept.getByRole('alert', { hidden: true })).toBeEmptyDOMElement();
    expect(kept.getByRole('textbox', { name: 'Name', hidden: true })).toHaveValue('Jo Smyth');
    expect(kept.getByRole('button', { name: 'Save', hidden: true })).not.toHaveAttribute('aria-disabled');
  });
});

describe('Edit user, kept private', () => {
  it('holds no name or email in the title, the address, the history state or storage', async () => {
    const router = await openEdit(jo);
    serve(userPath(jo), problem(409, { code: 'user.email_in_use' }));
    type(nameField(), 'Jo Smyth');
    type(emailField(), 'jo.smyth@example.org');
    pressSave();
    await answered();

    /** Expects nothing of Jo Smith's where the page could leave it. */
    function expectNothingKept() {
      const { pathname, search, hash } = router.state.location;
      const state = JSON.stringify(router.state.location.state as unknown);
      for (const place of [document.title, `${pathname}${search}${hash}`, state]) {
        for (const value of ['Jo', 'jo.smith', 'jo.smyth']) {
          expect(place).not.toContain(value);
        }
      }

      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
    }

    expectNothingKept();
    serve(userPath(jo), saved);
    serve(usersPath, json(withChanged(people, jo, { displayName: 'Jo Smyth', email: 'jo.smyth@example.org' })));
    pressSave();
    await answered();

    expect(router.state.location.pathname).toBe(`/manage/users/${jo.id}`);
    expectNothingKept();
  });
});
