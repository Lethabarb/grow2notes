import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminUser } from './usersQuery.ts';

const mePath = '/api/auth/me';
const usersPath = '/api/admin/users';
const antiforgeryPath = '/api/auth/antiforgery';

// users.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts, and a
// worker.
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

const sam: AdminUser = {
  id: manager.userId,
  email: 'sam.lee@example.org',
  displayName: 'Sam Lee',
  role: 'Manager',
  status: 'Active',
  invitedAtUtc: '2026-09-01T00:00:00Z',
  activatedAtUtc: '2026-09-02T00:00:00Z',
  deactivatedAtUtc: null,
  concurrencyStamp: `stamp-${manager.userId}`,
};

// Alex Park, as the list gives them once invited.
const alex: AdminUser = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  email: 'alex.park@example.org',
  displayName: 'Alex Park',
  role: 'Worker',
  status: 'Invited',
  invitedAtUtc: '2026-10-01T00:00:00Z',
  activatedAtUtc: null,
  deactivatedAtUtc: null,
  concurrencyStamp: 'stamp-alex',
};

// The words of users.md's *States* › Invite user.
const enterName = 'Enter the person’s name';
const nameTooLong = 'Name must be 100 characters or less';
const enterEmail = 'Enter an email address';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const emailInUse =
  'Another account already uses this email address. Use a different one, or find the person in the Users list.';
const chooseRole = 'Choose Worker or Manager';
const noConnectionSentence = 'Not sent: no connection. Try again.';
const somethingWentWrong = 'Not sent: something went wrong. Try again.';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** The `201` with no body that `POST /api/admin/users` answers (design.md §6.6). */
const created: Answer = () => Promise.resolve(new Response(null, { status: 201 }));

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code` and `errors`, if any. */
function problem(status: number, extensions: object = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** A `422 validation.failed` naming each field in `errors` (design.md §6.1). */
function fieldsRefused(errors: Record<string, string[]>): Answer {
  return problem(422, { code: 'validation.failed', errors });
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

/** How the stubbed server answers each method and path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(method: 'GET' | 'POST', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** The bodies of the invites sent, in order. */
function invitesSent(): unknown[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => path === usersPath && init?.method === 'POST')
    .map(([, init]) => JSON.parse(init?.body as string) as unknown);
}

/** How many times the list has been asked for. */
function listsAskedFor(): number {
  return fetchMock.mock.calls.filter(([path, init]) => path === usersPath && init?.method === 'GET').length;
}

/**
 * Renders the app at the last of `entries` for `me` on a memory router, under a query client of its own and
 * StrictMode, as `main.tsx` renders it. `['me']` holds `me` already, as after the start-up.
 */
function renderApp(entries: string[], me: Me = manager) {
  serve('GET', mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // Each entry with a key of its own, as a browser gives it, where a memory router gives every test's first entry the
  // key "default", under which the shell's memory would carry a row or a position from one test into the next.
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

/** Lets each request answer and TanStack hand on its new state, on a `setTimeout` of 0, until the page has it. */
async function settle() {
  await wait(1);
  await wait(1);
}

const main = () => screen.getByRole('main');
const pageHeading = () => within(main()).getByRole('heading', { level: 1 });
const nameField = () => within(main()).getByRole('textbox', { name: 'Name' });
const emailField = () => within(main()).getByRole('textbox', { name: 'Email address' });
const roleGroup = () => within(main()).getByRole('group', { name: 'Role' });
const workerRadio = () => within(main()).getByRole('radio', { name: 'Worker' });
const managerRadio = () => within(main()).getByRole('radio', { name: 'Manager' });
const sendInvite = () => within(main()).getByRole('button', { name: /^(Send invite|Sending…)$/ });
const alertLine = () => within(main()).getByRole('alert');

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

/** Focuses Send invite and presses it, as a person does, and returns it. */
function pressSend(): HTMLElement {
  const button = sendInvite();
  button.focus();
  fireEvent.click(button);
  return button;
}

/**
 * Fills the form with Alex Park's name, with the spaces around it that a paste can bring, and address, from which an
 * email field takes them itself (HTML's value sanitization).
 */
function fillAlex() {
  type(nameField(), ' Alex Park ');
  type(emailField(), 'alex.park@example.org');
}

/** Expects the fields to hold what was typed, Manager chosen when `role` says so. */
function expectKept(name: string, email: string, role: 'Worker' | 'Manager' = 'Worker') {
  expect(nameField()).toHaveValue(name);
  expect(emailField()).toHaveValue(email);
  expect(workerRadio()).toHaveProperty('checked', role === 'Worker');
  expect(managerRadio()).toHaveProperty('checked', role === 'Manager');
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string, with its method.
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(`${init?.method} ${path}`) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  serve('GET', antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  serve('GET', usersPath, json([sam]));
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Invite user', () => {
  it('opens with Worker chosen, both fields empty and no error, titled "Invite user", with Manage marked', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Invite user');
    expect(document.title).toBe('Grow2Notes – Invite user');
    expect(nameField()).toHaveValue('');
    expect(emailField()).toHaveValue('');
    expect(workerRadio()).toBeChecked();
    expect(managerRadio()).not.toBeChecked();
    expect(summary()).toBeNull();
    expect(main().querySelector('[aria-invalid]')).toBeNull();
    expect(alertLine()).toBeEmptyDOMElement();
    expect(document.querySelector('[disabled]')).toBeNull();
    const marked = screen.getByRole('banner').querySelectorAll('[aria-current]');
    expect(Array.from(marked, (item) => `${item.textContent} ${item.getAttribute('aria-current')}`)).toEqual([
      'Manage true',
    ]);
  });

  it('has its back link, "Users", to the list in the page bar, and its parts in order in <main>', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    const back = screen.getByRole('link', { name: 'Users' });
    expect(back).toHaveAttribute('href', '/manage/users');
    expect(document.getElementById('page-bar')).toContainElement(back);
    expect(main()).not.toContainElement(back);
    const parts = [pageHeading(), nameField(), emailField(), roleGroup(), alertLine(), sendInvite()];
    for (const [index, part] of parts.slice(1).entries()) {
      expect(parts[index].compareDocumentPosition(part)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    }

    expect(sendInvite()).toHaveAttribute('type', 'submit');
    expect(sendInvite().closest('form')).toHaveAttribute('novalidate');
  });

  it("goes back by its back link to the list that opened it, as Back does, which focuses the list's <h1>", async () => {
    const router = renderApp(['/manage/users']);
    await settle();
    fireEvent.click(within(main()).getByRole('link', { name: 'Invite user' }));
    await settle();

    fireEvent.click(screen.getByRole('link', { name: 'Users' }));
    await settle();

    // Back one entry, adding none, so that Forward reaches the form again.
    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(pageHeading()).toHaveFocus();
  });

  it('from a pasted address, pushes the list by its back link, which focuses the <h1> at the top', async () => {
    // After the list, as the tab may hold it, but opened afresh, so that nothing says that the list opened the page.
    const router = renderApp(['/manage/users', '/manage/users/new']);
    await settle();
    Object.defineProperty(window, 'scrollY', { value: 300, configurable: true, writable: true });

    fireEvent.click(screen.getByRole('link', { name: 'Users' }));
    await settle();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(pageHeading()).toHaveFocus();
    expect(window.scrollY).toBe(0);
  });

  it('labels each field, describes it by its hint, and keeps autofill, spelling and a length limit off', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    expect(nameField()).toHaveAccessibleDescription('Shown on the notes they write');
    expect(nameField()).toHaveAttribute('type', 'text');
    expect(nameField()).toHaveAttribute('autocapitalize', 'words');
    expect(emailField()).toHaveAccessibleDescription(
      'Grow2Notes will email them a setup link. It works once and lasts 7 days.',
    );
    expect(emailField()).toHaveAttribute('type', 'email');
    expect(emailField()).toHaveAttribute('autocapitalize', 'none');
    expect(emailField()).toHaveAttribute('autocorrect', 'off');
    for (const field of [nameField(), emailField()]) {
      expect(field).toHaveAttribute('autocomplete', 'off');
      expect(field).toHaveAttribute('spellcheck', 'false');
      expect(field).not.toHaveAttribute('maxlength');
      expect(field).not.toHaveAttribute('required');
    }

    expect(within(roleGroup()).getAllByRole('radio')).toEqual([workerRadio(), managerRadio()]);
    expect(workerRadio()).toHaveAccessibleDescription('Writes notes and reads past notes');
    expect(managerRadio()).toHaveAccessibleDescription(
      'Can also review flagged notes, download reports, and set up participants and users',
    );
    expect(workerRadio()).toHaveAttribute('name', 'role');
    expect(managerRadio()).toHaveAttribute('name', 'role');
  });

  it('chooses Manager with a tap on its label, and sends nothing as it does', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    fireEvent.click(screen.getByText('Manager'));

    expect(managerRadio()).toBeChecked();
    expect(workerRadio()).not.toBeChecked();
    expect(invitesSent()).toEqual([]);
  });
});

describe('Invite user, checked before it sends', () => {
  it('shows both messages inline and in the focused summary when sent empty, and sends nothing', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    pressSend();
    await settle();

    expect(summaryItems()).toEqual([
      { text: enterName, href: '#displayName' },
      { text: enterEmail, href: '#email' },
    ]);
    expect(summary()).toHaveFocus();
    expect(nameField()).toHaveAccessibleDescription(`Shown on the notes they write Error: ${enterName}`);
    expect(nameField()).toHaveAttribute('aria-invalid', 'true');
    expect(emailField()).toHaveAccessibleDescription(
      `Grow2Notes will email them a setup link. It works once and lasts 7 days. Error: ${enterEmail}`,
    );
    expect(emailField()).toHaveAttribute('aria-invalid', 'true');
    expect(document.title).toBe('Error: Grow2Notes – Invite user');
    expect(fetchMock.mock.calls.filter(([path]) => path !== mePath)).toEqual([]);
  });

  it('takes focus to the summary again on each failed press, even with the same messages', async () => {
    renderApp(['/manage/users/new']);
    await settle();
    pressSend();
    await settle();
    nameField().focus();

    pressSend();
    await settle();

    expect(summary()).toHaveFocus();
  });

  it('removes a message and its summary item as the field is fixed, with focus left in it', async () => {
    renderApp(['/manage/users/new']);
    await settle();
    pressSend();
    await settle();

    nameField().focus();
    type(nameField(), 'A');
    expect(summaryItems()).toEqual([{ text: enterEmail, href: '#email' }]);
    expect(nameField()).not.toHaveAttribute('aria-invalid');
    expect(nameField()).toHaveAccessibleDescription('Shown on the notes they write');
    expect(nameField()).toHaveFocus();

    emailField().focus();
    type(emailField(), 'a');
    expect(emailField()).toHaveAccessibleDescription(
      `Grow2Notes will email them a setup link. It works once and lasts 7 days. Error: ${emailFormat}`,
    );
    type(emailField(), 'a@b');
    await settle();

    expect(summary()).toBeNull();
    expect(emailField()).not.toHaveAttribute('aria-invalid');
    expect(emailField()).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Invite user');
  });

  it('leaves a field with no message unchecked as another is typed in', async () => {
    renderApp(['/manage/users/new']);
    await settle();
    type(emailField(), 'alex');
    pressSend();
    await settle();

    type(emailField(), 'alex@');

    expect(summaryItems()).toEqual([
      { text: enterName, href: '#displayName' },
      { text: emailFormat, href: '#email' },
    ]);
  });

  it.each([
    {
      given: '"alex" as the address',
      name: 'Alex Park',
      email: 'alex',
      items: [{ text: emailFormat, href: '#email' }],
    },
    {
      given: 'an address with two "@"',
      name: 'Alex Park',
      email: 'alex@park@example.org',
      items: [{ text: emailFormat, href: '#email' }],
    },
    {
      given: 'a name of 101 characters',
      name: 'A'.repeat(101),
      email: 'alex.park@example.org',
      items: [{ text: nameTooLong, href: '#displayName' }],
    },
    {
      given: 'a name of spaces alone and no address',
      name: '   ',
      email: '',
      items: [
        { text: enterName, href: '#displayName' },
        { text: enterEmail, href: '#email' },
      ],
    },
  ])('refuses $given, and sends nothing', async ({ name, email, items }) => {
    renderApp(['/manage/users/new']);
    await settle();
    type(nameField(), name);
    type(emailField(), email);

    pressSend();
    await settle();

    expect(summaryItems()).toEqual(items);
    expect(invitesSent()).toEqual([]);
    expectKept(name, email);
  });

  it('keeps a pasted name of 150 characters whole, and says that it is too long', async () => {
    renderApp(['/manage/users/new']);
    await settle();

    type(nameField(), 'B'.repeat(150));
    type(emailField(), 'alex.park@example.org');
    pressSend();
    await settle();

    expect(nameField()).toHaveValue('B'.repeat(150));
    expect(summaryItems()).toEqual([{ text: nameTooLong, href: '#displayName' }]);
  });

  it('sends a name of 100 characters once trimmed, as the server counts it', async () => {
    serve('POST', usersPath, noAnswer);
    renderApp(['/manage/users/new']);
    await settle();

    type(nameField(), ` ${'C'.repeat(100)} `);
    type(emailField(), 'alex.park@example.org');
    pressSend();
    await settle();

    expect(summary()).toBeNull();
    expect(invitesSent()).toEqual([{ displayName: 'C'.repeat(100), email: 'alex.park@example.org', role: 'Worker' }]);
  });
});

describe('Invite user, sending', () => {
  it('sends the fields trimmed with the role chosen, once however often hit, "Sending…" from 400 ms', async () => {
    serve('POST', usersPath, noAnswer);
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();
    fireEvent.click(managerRadio());

    const button = pressSend();
    pressSend();
    await wait(399);
    pressSend();

    expect(invitesSent()).toEqual([{ displayName: 'Alex Park', email: 'alex.park@example.org', role: 'Manager' }]);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).toHaveAccessibleName('Send invite');
    expect(button).toHaveFocus();

    await wait(1);
    expect(button).toHaveAccessibleName('Sending…');
    act(() => {
      vi.advanceTimersToNextFrame();
    });
    expect(main().querySelector('p.visually-hidden[role="status"]')).toHaveTextContent('Sending…');
    // The fields stay as typed, and can still be changed.
    expectKept(' Alex Park ', 'alex.park@example.org', 'Manager');
    expect(invitesSent()).toHaveLength(1);
  });

  it('sends a press made with Enter in a field once, and none while busy', async () => {
    serve('POST', usersPath, noAnswer);
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();

    fireEvent.submit(emailField().closest('form')!);
    await settle();
    // Enter in a field presses the form's submit button, which ignores it while busy.
    fireEvent.click(sendInvite());
    await settle();

    expect(invitesSent()).toHaveLength(1);
  });

  it('on 201 refetches the list, replaces Invite user with it, and focuses its <h1>, with no banner', async () => {
    const list = held(json([sam, alex]));
    serve('POST', usersPath, created);
    const router = renderApp(['/manage/users']);
    await settle();
    fireEvent.click(within(main()).getByRole('link', { name: 'Invite user' }));
    await settle();
    const before = listsAskedFor();
    serve('GET', usersPath, list.answer);
    fillAlex();

    const button = pressSend();
    await settle();

    // The invite is made, and the page waits, busy, for the list that holds it.
    expect(invitesSent()).toHaveLength(1);
    expect(listsAskedFor()).toBe(before + 1);
    expect(router.state.location.pathname).toBe('/manage/users/new');
    expect(button).toHaveAttribute('aria-disabled', 'true');

    list.release();
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expect(router.state.historyAction).toBe('REPLACE');
    expect(pageHeading()).toHaveAccessibleName('Users');
    expect(pageHeading()).toHaveFocus();
    expect(document.title).toBe('Grow2Notes – Users');
    const row = within(main()).getByRole('link', { name: 'Alex Park' });
    expect(row).toHaveAccessibleDescription('alex.park@example.org, Invited, Worker');
    expect(within(main()).queryByRole('alert')).not.toBeInTheDocument();
    for (const region of within(main()).getAllByRole('status')) {
      expect(region).toBeEmptyDOMElement();
    }

    // Back goes to the entry before the form, which the list has replaced.
    await act(() => router.navigate(-1));
    expect(router.state.location.pathname).toBe('/manage/users');
    expect(screen.queryByRole('heading', { level: 1, name: 'Invite user' })).not.toBeInTheDocument();
  });

  it('shows the list on 201 when it was never loaded, which it then loads itself', async () => {
    serve('POST', usersPath, created);
    serve('GET', usersPath, json([sam, alex]));
    const router = renderApp(['/manage/users/new']);
    await settle();
    fillAlex();

    pressSend();
    await settle();
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expect(pageHeading()).toHaveFocus();
    expect(within(main()).getByRole('link', { name: 'Alex Park' })).toBeInTheDocument();
  });

  it('goes nowhere when the invite ends after the manager has left the page', async () => {
    const answer = held(created);
    serve('POST', usersPath, answer.answer);
    const router = renderApp(['/manage/users', '/manage/users/new']);
    await settle();
    fillAlex();
    pressSend();
    await settle();

    fireEvent.click(screen.getByRole('link', { name: 'Users' }));
    await settle();
    fireEvent.click(within(main()).getByRole('link', { name: 'Sam Lee' }));
    await settle();
    answer.release();
    await settle();

    expect(router.state.location.pathname).toBe(`/manage/users/${sam.id}`);
  });
});

describe('Invite user, refused by the server', () => {
  it('shows an address that has an account on Email, linked from the focused summary, values kept', async () => {
    serve('POST', usersPath, problem(409, { code: 'user.email_in_use' }));
    renderApp(['/manage/users/new']);
    await settle();
    type(nameField(), 'Sam Lee');
    type(emailField(), 'SAM.LEE@example.org');
    fireEvent.click(managerRadio());

    pressSend();
    await settle();

    expect(summaryItems()).toEqual([{ text: emailInUse, href: '#email' }]);
    expect(summary()).toHaveFocus();
    expect(emailField()).toHaveAttribute('aria-invalid', 'true');
    expect(emailField()).toHaveAccessibleDescription(
      `Grow2Notes will email them a setup link. It works once and lasts 7 days. Error: ${emailInUse}`,
    );
    expect(document.title).toBe('Error: Grow2Notes – Invite user');
    expectKept('Sam Lee', 'SAM.LEE@example.org', 'Manager');
    expect(sendInvite()).not.toHaveAttribute('aria-disabled');

    // A new address clears it as it is typed, and nothing else does.
    type(nameField(), 'Sam Leigh');
    expect(summaryItems()).toEqual([{ text: emailInUse, href: '#email' }]);
    type(emailField(), 'sam.leigh@example.org');
    await settle();
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Invite user');
  });

  it.each<{ given: string; email: string; errors: Record<string, string[]>; items: SummaryItem[] }>([
    {
      given: 'an address with a letter outside ASCII, under email',
      email: 'zoë@example.org',
      errors: { email: ['Enter an email address in the correct format, like name@example.com'] },
      items: [{ text: emailFormat, href: '#email' }],
    },
    {
      given: 'a role, under role',
      email: 'alex.park@example.org',
      errors: { role: ['Choose Worker or Manager'] },
      items: [{ text: chooseRole, href: '#role-worker' }],
    },
    {
      given: 'a name, under displayName',
      email: 'alex.park@example.org',
      errors: { displayName: ['Name must be 100 characters or less'] },
      items: [{ text: nameTooLong, href: '#displayName' }],
    },
    {
      given: 'a field the form does not have, under an unknown key',
      email: 'alex.park@example.org',
      errors: { organisationId: ['The field is not allowed.'] },
      items: [{ text: somethingWentWrong }],
    },
    {
      given: 'several fields, the unknown first and the rest in page order',
      email: 'zoë@example.org',
      errors: { role: ['x'], somethingElse: ['y'], email: ['z'] },
      items: [
        { text: somethingWentWrong },
        { text: emailFormat, href: '#email' },
        { text: chooseRole, href: '#role-worker' },
      ],
    },
  ])('shows a 422 for $given in its own words, with focus on the summary and every value kept', async (case_) => {
    serve('POST', usersPath, fieldsRefused(case_.errors));
    renderApp(['/manage/users/new']);
    await settle();
    type(nameField(), 'Zoë Hill');
    type(emailField(), case_.email);

    pressSend();
    await settle();

    expect(summaryItems()).toEqual(case_.items);
    expect(summary()).toHaveFocus();
    expect(document.title).toBe('Error: Grow2Notes – Invite user');
    expectKept('Zoë Hill', case_.email);
    expect(alertLine()).toBeEmptyDOMElement();
  });

  it("puts the role's message under its legend, describing the group, until a role is chosen", async () => {
    serve('POST', usersPath, fieldsRefused({ role: ['Choose Worker or Manager'] }));
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();
    pressSend();
    await settle();

    expect(roleGroup()).toHaveAccessibleDescription(`Error: ${chooseRole}`);
    const legend = roleGroup().querySelector('legend')!;
    const message = document.getElementById('role-error')!;
    expect(message).toHaveTextContent(chooseRole);
    expect(legend.compareDocumentPosition(message)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(message.compareDocumentPosition(workerRadio())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(workerRadio()).not.toHaveAttribute('aria-invalid');

    Element.prototype.scrollIntoView = vi.fn();
    fireEvent.click(within(summary()!).getByRole('link', { name: chooseRole }));
    expect(workerRadio()).toHaveFocus();
    Reflect.deleteProperty(Element.prototype, 'scrollIntoView');

    fireEvent.click(managerRadio());
    await settle();
    expect(summary()).toBeNull();
    expect(roleGroup()).not.toHaveAccessibleDescription();
  });

  it.each([
    { given: 'no connection', answer: noConnection, sentence: noConnectionSentence },
    { given: 'a 500', answer: problem(500), sentence: somethingWentWrong },
    { given: 'a 403', answer: problem(403), sentence: somethingWentWrong },
    {
      given: 'a 409 with another code',
      answer: problem(409, { code: 'user.last_manager' }),
      sentence: somethingWentWrong,
    },
    {
      given: 'a 422 that names no field',
      answer: problem(422, { code: 'validation.failed' }),
      sentence: somethingWentWrong,
    },
  ])('says what $given means above Send invite, with focus left on it and nothing cleared', async (case_) => {
    serve('POST', usersPath, case_.answer);
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();

    const button = pressSend();
    await settle();

    expect(alertLine().textContent).toBe(case_.sentence);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Invite user');
    expectKept(' Alex Park ', 'alex.park@example.org');
  });

  it('says "no connection" for an answer that never comes, after its 10 seconds', async () => {
    serve('POST', usersPath, noAnswer);
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();

    pressSend();
    await wait(10_000);
    await settle();

    expect(alertLine().textContent).toBe(noConnectionSentence);
    expect(sendInvite()).not.toHaveAttribute('aria-disabled');
  });

  it('empties the alert at once on the next press, and says it again when that fails too', async () => {
    serve('POST', usersPath, noConnection);
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();
    pressSend();
    await settle();
    const region = alertLine();

    pressSend();
    expect(region).toBeEmptyDOMElement();
    await settle();

    expect(region.textContent).toBe(noConnectionSentence);
    expect(invitesSent()).toHaveLength(2);
  });

  it('says nothing of a 401, which the session flow has, and keeps every value under sign-in again', async () => {
    serve('POST', usersPath, problem(401));
    renderApp(['/manage/users/new']);
    await settle();
    fillAlex();

    pressSend();
    await settle();

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    // The form, hidden under it, as it was, with nothing in its alert line and no summary.
    const hiddenForm = main().querySelector<HTMLElement>(':scope > [hidden]');
    if (hiddenForm === null) {
      throw new Error('No page is hidden in <main>.');
    }

    const kept = within(hiddenForm);
    expect(kept.getByRole('alert', { hidden: true })).toBeEmptyDOMElement();
    expect(summary()).toBeNull();
    expect(kept.getByRole('textbox', { name: 'Name', hidden: true })).toHaveValue(' Alex Park ');
    expect(kept.getByRole('textbox', { name: 'Email address', hidden: true })).toHaveValue('alex.park@example.org');
    expect(kept.getByRole('radio', { name: 'Worker', hidden: true })).toBeChecked();
    expect(kept.getByRole('button', { name: 'Send invite', hidden: true })).not.toHaveAttribute('aria-disabled');
  });
});

describe('Invite user, kept private', () => {
  it('holds no name or email in the title, the address, the history state or storage', async () => {
    serve('POST', usersPath, problem(409, { code: 'user.email_in_use' }));
    const router = renderApp(['/manage/users', '/manage/users/new']);
    await settle();
    fillAlex();
    pressSend();
    await settle();

    /** Expects nothing of Alex Park's where the page could leave it. */
    function expectNothingKept() {
      const { pathname, search, hash } = router.state.location;
      const state = JSON.stringify(router.state.location.state as unknown);
      for (const place of [document.title, `${pathname}${search}${hash}`, state]) {
        expect(place).not.toContain('Alex');
        expect(place).not.toContain('alex.park');
      }

      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
    }

    expectNothingKept();
    serve('POST', usersPath, created);
    serve('GET', usersPath, json([sam, alex]));
    pressSend();
    await settle();
    await settle();

    expect(router.state.location.pathname).toBe('/manage/users');
    expectNothingKept();
  });
});

describe('Invite user for a worker', () => {
  it('shows Page not found, and sends nothing', async () => {
    renderApp(['/manage/users/new'], worker);
    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([path]) => path !== mePath)).toEqual([]);
  });
});
