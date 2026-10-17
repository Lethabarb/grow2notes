import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, onTestFinished, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import type { ListRow } from '../../components/ListEditor.tsx';
import { routes } from '../../routes/routes.tsx';
import type { AdminParticipant } from './participantsQuery.ts';

type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const participantsPath = '/api/admin/participants';
const listPath = '/api/admin/participants?includeArchived=true';

/** The one request with which a participant's page reads their goals, archived ones too. */
function goalsPath(participantId: string): string {
  return `/api/admin/participants/${participantId}/goals?includeArchived=true`;
}

// participants.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts,
// and a worker. Today in Melbourne is 1 October 2026.
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

const jane: AdminParticipant = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  dateOfBirth: '1987-03-07',
  status: 'Active',
  rowVersion: 'AAAAAAAAB9E=',
};

const priya: AdminParticipant = {
  id: '0c6f2e8a-4b17-4d39-a5e2-7f1b3c9d0a64',
  givenName: 'Priya',
  familyName: 'Kumar',
  dateOfBirth: '1990-11-23',
  status: 'Active',
  rowVersion: 'AAAAAAAAB9I=',
};

// Jane as the list gives her once a correction is saved, with the version that the row then has.
const janeSaved: AdminParticipant = { ...jane, familyName: 'Citizen-Hall', rowVersion: 'AAAAAAAAB+A=' };

// Jane as the list gives her once another manager has corrected her, with the version that the row then has.
const janeChangedMeanwhile: AdminParticipant = {
  ...jane,
  givenName: 'Jayne',
  dateOfBirth: '1987-03-08',
  rowVersion: 'AAAAAAAAB+E=',
};

// Jane as the list gives her once she is archived, and once she is restored again, each with the version that the row
// then has.
const janeArchived: AdminParticipant = { ...jane, status: 'Archived', rowVersion: 'AAAAAAAAB+I=' };
const janeRestored: AdminParticipant = { ...jane, rowVersion: 'AAAAAAAAB+M=' };

// The new participant's ID, as the 201 gives it.
const newId = '7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20';

// A made-up ID that is on no list.
const unknownId = '4f2a9c7e-1b38-4d65-a0e9-8c3b5d1f7a26';

// 06:12 UTC on 1 October 2026, the answer's Date, is 4:12 pm in Melbourne.
const answeredAt = 'Thu, 01 Oct 2026 06:12:00 GMT';

const nbsp = ' ';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** The `201 {id}` of an add, with the answer's `Date`. */
const added: Answer = () =>
  Promise.resolve(
    new Response(JSON.stringify({ id: newId }), {
      status: 201,
      headers: { 'Content-Type': 'application/json', Date: answeredAt },
    }),
  );

/** The bare `200` of a correction, with the answer's `Date`. */
const corrected: Answer = () => Promise.resolve(new Response(null, { status: 200, headers: { Date: answeredAt } }));

/** The `204` of an archive or a restore. */
const noContent: Answer = () => Promise.resolve(new Response(null, { status: 204 }));

/** `answer`, given `ms` after the request was sent. */
function later(ms: number, answer: Answer): Answer {
  return (init) =>
    new Promise((resolve, reject) => {
      setTimeout(() => {
        answer(init).then(resolve, reject);
      }, ms);
    });
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its extensions such as `errors`. */
function problem(status: number, extensions: Record<string, unknown> = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
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

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each request, by its method and path: a new answer for each. */
const answers = new Map<string, Answer>();

/** The query client of the app that the test rendered last, whose cache a test can look into. */
let queryClient: QueryClient;

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(method: 'GET' | 'POST' | 'PUT', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** The adds sent, each as the JSON of its body. */
function adds(): unknown[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => path === participantsPath && init?.method === 'POST')
    .map(([, init]) => JSON.parse(init?.body as string) as unknown);
}

/** The corrections sent, each as its path, its `If-Match` and the JSON of its body. */
function corrections(): { path: string; ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls
    .filter(([, init]) => init?.method === 'PUT')
    .map(([path, init]) => ({
      path: path as string,
      ifMatch: new Headers(init?.headers).get('If-Match'),
      body: JSON.parse(init?.body as string) as unknown,
    }));
}

/** The archives and restores sent, each as its path, its `If-Match`, its token and its body. */
function statusChanges(): { path: string; ifMatch: string | null; token: string | null; body: unknown }[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => init?.method === 'POST' && /\/(?:archive|restore)$/.test(path as string))
    .map(([path, init]) => {
      const headers = new Headers(init?.headers);
      return {
        path: path as string,
        ifMatch: headers.get('If-Match'),
        token: headers.get('X-XSRF-TOKEN'),
        body: init?.body,
      };
    });
}

/** The requests sent, each as its method and path, in the order sent, but for the antiforgery token's. */
function requests(): string[] {
  return fetchMock.mock.calls
    .filter(([path]) => path !== antiforgeryPath)
    .map(([path, init]) => `${init?.method ?? 'GET'} ${path as string}`);
}

/**
 * Renders the app at `path` for `me` on a memory router, or at the last of `path`'s addresses, after the others in its
 * history, under a query client of its own and StrictMode, as `main.tsx` renders it, with `['me']` holding `me`
 * already, as after the start-up.
 */
function renderApp(path: string | string[], me: Me = manager): Router {
  serve('GET', mePath, json(me));
  const client = createQueryClient();
  queryClient = client;
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

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

async function settle() {
  await wait(1);
}

/** Opens Add participant from the list, as the manager does, with its link, the list scrolled to `listScrolledTo`. */
async function openFromTheList(listScrolledTo = 0): Promise<Router> {
  serve('GET', listPath, json([jane]));
  const router = renderApp('/manage/participants');
  await settle();
  scrollPage(listScrolledTo);
  await press(within(main()).getByRole('link', { name: 'Add participant' }));
  expect(router.state.location.pathname).toBe('/manage/participants/new');
  return router;
}

/** Opens Add participant at its address. */
async function openAdd(): Promise<Router> {
  const router = renderApp('/manage/participants/new');
  await settle();
  return router;
}

function main(): HTMLElement {
  return screen.getByRole('main');
}

function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

function field(name: 'Given name' | 'Family name' | 'Day' | 'Month' | 'Year'): HTMLElement {
  return within(main()).getByRole('textbox', { name });
}

function dateOfBirth(): HTMLElement {
  return within(main()).getByRole('group', { name: 'Date of birth' });
}

function saveButton(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Save' });
}

/** The error summary, by its heading, or `null` while it is not shown. */
function summary(): HTMLElement | null {
  return within(main()).queryByRole('heading', { level: 2, name: 'There is a problem' })?.parentElement ?? null;
}

/** The summary's links' words, in order. */
function summaryLinks(): string[] {
  return within(summary()!).getAllByRole('link').map((link) => link.textContent);
}

/** The words of each inline message, without the hidden "Error: ", in page order. */
function inlineMessages(): string[] {
  return Array.from(main().querySelectorAll('[id$="-error"]'), (message) =>
    (message.textContent ?? '').replace(/^Error: /, ''),
  );
}

/** The fields marked invalid, by their labels, in page order. */
function invalidFields(): string[] {
  return Array.from(
    main().querySelectorAll<HTMLInputElement>('input[aria-invalid="true"]'),
    (input) => input.labels?.[0]?.textContent ?? '',
  );
}

/** The Details section. */
function detailsSection(): HTMLElement {
  return within(main()).getByRole('region', { name: 'Details' });
}

/** The alert above Save, in the Details. */
function alert(): HTMLElement {
  return within(detailsSection()).getByRole('alert');
}

/** The save status beside Save, in the Details. */
function saveStatus(): HTMLElement {
  return within(detailsSection()).getByRole('status');
}

/** Types `text` in `input`, replacing what was there. */
function type(input: HTMLElement, text: string) {
  fireEvent.change(input, { target: { value: text } });
}

/** Types each of the Details. */
function fill(givenName: string, familyName: string, day: string, month: string, year: string) {
  type(field('Given name'), givenName);
  type(field('Family name'), familyName);
  type(field('Day'), day);
  type(field('Month'), month);
  type(field('Year'), year);
}

/** Scrolls the page to `y`, as the person does, with the scroll event that the browser fires. */
function scrollPage(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, configurable: true, writable: true });
  window.dispatchEvent(new Event('scroll'));
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets what it starts run. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
}

/** Presses Save. */
async function save() {
  await press(saveButton());
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

// jsdom has no scrollIntoView, which a summary link calls on its field's label or legend.
const scrollIntoView = vi.fn<(this: Element, options?: boolean | ScrollIntoViewOptions) => void>();

beforeEach(() => {
  vi.useFakeTimers();
  serve('GET', antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  // No goals, unless a test gives them, so that a page whose goals a test is not about loads with its participant.
  serve('GET', goalsPath(jane.id), json([]));
  serve('GET', goalsPath(priya.id), json([]));
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(`${init?.method ?? 'GET'} ${path}`) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  Element.prototype.scrollIntoView = scrollIntoView;
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  scrollIntoView.mockReset();
  Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Add participant', () => {
  it('shows the back link before <main>, the <h1>, then Details: fields, alert, Save and status', async () => {
    await openAdd();

    const back = screen.getByRole('link', { name: 'Participants' });
    expect(back).toHaveAttribute('href', '/manage/participants');
    expect(main()).not.toContainElement(back);
    expect(back.compareDocumentPosition(main())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(pageHeading()).toHaveAccessibleName('Add participant');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    const details = within(main()).getByRole('region', { name: 'Details' });
    expect(within(details).getByRole('heading', { level: 2 })).toHaveAccessibleName('Details');
    const inOrder = [
      pageHeading(),
      field('Given name'),
      field('Family name'),
      dateOfBirth(),
      field('Day'),
      field('Month'),
      field('Year'),
      alert(),
      saveButton(),
      saveStatus(),
    ];
    for (const [before, after] of inOrder.slice(0, -1).map((element, index) => [element, inOrder[index + 1]])) {
      expect(before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
    expect(saveButton()).toHaveAttribute('type', 'submit');
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Add participant');
    expect(currentItems()).toEqual(['Manage true']);
  });

  it('describes the date of birth by its hint, as a group, its parts each labelled', async () => {
    await openAdd();

    expect(dateOfBirth().tagName).toBe('FIELDSET');
    expect(dateOfBirth()).toHaveAttribute('role', 'group');
    expect(dateOfBirth()).toHaveAccessibleDescription('For example, 27 3 1987');
    for (const part of ['Day', 'Month', 'Year'] as const) {
      expect(dateOfBirth()).toContainElement(field(part));
    }
  });

  it('gives each field autocomplete off, no name, no maxlength and no pattern, and fills nothing in', async () => {
    await openAdd();

    const inputs = within(main()).getAllByRole('textbox');
    expect(inputs).toHaveLength(5);
    for (const input of inputs) {
      expect(input).toHaveAttribute('type', 'text');
      expect(input).toHaveAttribute('autocomplete', 'off');
      expect(input).toHaveAttribute('spellcheck', 'false');
      expect(input).toHaveValue('');
      for (const attribute of ['name', 'maxlength', 'pattern', 'placeholder']) {
        expect(input).not.toHaveAttribute(attribute);
      }
    }

    for (const name of ['Given name', 'Family name'] as const) {
      expect(field(name)).toHaveAttribute('autocorrect', 'off');
      expect(field(name)).toHaveAttribute('autocapitalize', 'words');
      expect(field(name)).not.toHaveAttribute('inputmode');
    }

    for (const part of ['Day', 'Month', 'Year'] as const) {
      expect(field(part)).toHaveAttribute('inputmode', 'numeric');
    }

  });

  it('shows no error before Save, however the fields are typed', async () => {
    await openAdd();

    fill(' ', 'a'.repeat(101), '31', '13', '87');

    expect(summary()).toBeNull();
    expect(inlineMessages()).toEqual([]);
    expect(invalidFields()).toEqual([]);
    expect(document.title).toBe('Grow2Notes – Add participant');
    expect(adds()).toEqual([]);
  });

  it('on Save with nothing typed shows each message inline and in the summary, and sends nothing', async () => {
    await openAdd();

    await save();

    const messages = ['Enter the given name', 'Enter the family name', 'Enter the date of birth'];
    expect(inlineMessages()).toEqual(messages);
    expect(summaryLinks()).toEqual(messages);
    expect(summary()).toHaveFocus();
    expect(invalidFields()).toEqual(['Given name', 'Family name', 'Day', 'Month', 'Year']);
    expect(field('Given name')).toHaveAccessibleDescription('Error: Enter the given name');
    expect(dateOfBirth()).toHaveAccessibleDescription('For example, 27 3 1987 Error: Enter the date of birth');
    expect(document.title).toBe('Error: Grow2Notes – Add participant');
    expect(adds()).toEqual([]);
  });

  it('focuses the summary on every failed Save, even with the same messages', async () => {
    await openAdd();
    await save();
    field('Family name').focus();

    await save();

    expect(summary()).toHaveFocus();
    expect(summaryLinks()).toEqual(['Enter the given name', 'Enter the family name', 'Enter the date of birth']);
  });

  it("follows a summary link to its field, the date's first part in error, with no change to the address", async () => {
    const router = await openAdd();
    fill('Jane', 'Citizen', '7', '', '');
    await save();
    const { key, pathname, search, hash } = router.state.location;

    fireEvent.click(within(summary()!).getByRole('link', { name: 'Date of birth must include a month and year' }));

    expect(field('Month')).toHaveFocus();
    expect(scrollIntoView.mock.contexts).toEqual([within(dateOfBirth()).getByText('Date of birth')]);
    expect(router.state.location).toMatchObject({ key, pathname, search, hash });
    expect(invalidFields()).toEqual(['Month', 'Year']);

    fireEvent.click(within(summary()!).getByRole('link'));
    expect(field('Month')).toHaveFocus();
  });

  it("takes a field's message away as it is fixed, and \"Error: \" off the title with the last", async () => {
    await openAdd();
    await save();

    type(field('Given name'), 'Jane');
    expect(inlineMessages()).toEqual(['Enter the family name', 'Enter the date of birth']);
    expect(summaryLinks()).toEqual(['Enter the family name', 'Enter the date of birth']);
    expect(field('Given name')).not.toHaveAttribute('aria-invalid');

    type(field('Family name'), 'Citizen');
    // Checked again at each change once it shows a message, so it says what is wrong now.
    type(field('Day'), '7');
    expect(inlineMessages()).toEqual(['Date of birth must include a month and year']);
    expect(invalidFields()).toEqual(['Month', 'Year']);
    expect(document.title).toBe('Error: Grow2Notes – Add participant');

    type(field('Month'), '3');
    field('Year').focus();
    type(field('Year'), '1987');
    expect(inlineMessages()).toEqual([]);
    expect(invalidFields()).toEqual([]);
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Add participant');
    // Taking a message away moves no focus.
    expect(field('Year')).toHaveFocus();
  });

  it('checks no field that showed no message as another is typed', async () => {
    await openAdd();
    type(field('Given name'), 'Jane');
    await save();

    type(field('Given name'), '');

    expect(inlineMessages()).toEqual(['Enter the family name', 'Enter the date of birth']);
  });

  it.each([
    ['given', 'a'.repeat(101), 'Citizen', 'Given name must be 100 characters or less', 'Given name'],
    ['family', 'Jane', 'b'.repeat(101), 'Family name must be 100 characters or less', 'Family name'],
  ])('refuses a %s name of 101 characters', async (_which, givenName, familyName, message, label) => {
    await openAdd();
    fill(givenName, familyName, '7', '3', '1987');

    await save();

    expect(inlineMessages()).toEqual([message]);
    expect(invalidFields()).toEqual([label]);
    expect(adds()).toEqual([]);
  });

  it.each([
    ['31', '4', '1987', 'Date of birth must be a real date', ['Day']],
    ['7', '13', '1987', 'Date of birth must be a real date', ['Month']],
    ['7', '3', '87', 'Year must include 4 numbers', ['Year']],
    ['1', '10', '2026', 'Date of birth must be in the past', ['Day', 'Month', 'Year']],
  ])('refuses the date %s / %s / %s with "%s"', async (day, month, year, message, parts) => {
    await openAdd();
    fill('Jane', 'Citizen', day, month, year);

    await save();

    expect(inlineMessages()).toEqual([message]);
    expect(summaryLinks()).toEqual([message]);
    expect(invalidFields()).toEqual(parts);
    expect(adds()).toEqual([]);
  });

  it('sends the names with only their ends trimmed and the date as a calendar date, with the token', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('  Seán ', "O'Brien-Nguyễn\t", ' 07 ', 'Sept.', '1987');

    await save();

    expect(adds()).toEqual([{ givenName: 'Seán', familyName: "O'Brien-Nguyễn", dateOfBirth: '1987-09-07' }]);
    const [, init] = fetchMock.mock.calls.find(([path, sent]) => path === participantsPath && sent?.method === 'POST')!;
    expect(new Headers(init?.headers).get('X-XSRF-TOKEN')).toBe('token-1');
  });

  it("after a 201 is the participant's page, in place, focus on Save, saying when it was saved", async () => {
    const router = await openFromTheList();
    serve('POST', participantsPath, added);
    fill(' Jane ', 'Citizen', '7', '3', '1987');
    const given = field('Given name');
    const button = saveButton();

    await save();

    expect(router.state.location.pathname).toBe(`/manage/participants/${newId}`);
    expect(router.state.location.state).toEqual({ inScreen: true });
    expect(router.state.historyAction).toBe('REPLACE');
    // The same page, never mounted again, so the fields keep what was typed and Save keeps focus.
    expect(saveButton()).toBe(button);
    expect(button).toHaveFocus();
    expect(field('Given name')).toBe(given);
    expect(given).toHaveValue(' Jane ');
    expect(saveStatus().textContent).toBe(`Saved 4:12${nbsp}pm`);
    expect(saveStatus().querySelector('time')).toHaveAttribute('dateTime', '2026-10-01T06:12:00.000Z');
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(document.title).toBe('Grow2Notes – Participant');
    expect(alert()).toBeEmptyDOMElement();
    expect(adds()).toHaveLength(1);
  });

  it('after a 201, Back goes to the list, not to an empty form, with its <h1> focused where it was', async () => {
    const router = await openFromTheList(300);
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();

    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(pageHeading()).toHaveAccessibleName('Participants');
    // No row was opened from the list, so its <h1> takes focus, and the list is where it was left.
    expect(pageHeading()).toHaveFocus();
    expect(window.scrollY).toBe(300);
  });

  it('after a 201, its back link goes back to the list, as Back does', async () => {
    const router = await openFromTheList(300);
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();

    await press(screen.getByRole('link', { name: 'Participants' }));

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(pageHeading()).toHaveAccessibleName('Participants');
    expect(pageHeading()).toHaveFocus();
    expect(window.scrollY).toBe(300);
  });

  it('empties "Saved" at the next edit, so that it never stands for changes not saved', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    expect(saveStatus()).toHaveTextContent('Saved');

    type(field('Family name'), 'Citizens');

    expect(saveStatus()).toBeEmptyDOMElement();
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
  });

  it('sends the add once when its answer is lost, says so above Save, and keeps the fields', async () => {
    await openAdd();
    serve('POST', participantsPath, noConnection);
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();
    await wait(60_000);

    expect(adds()).toHaveLength(1);
    expect(alert()).toHaveTextContent('Not saved: no connection. Try again.');
    expect(saveButton()).toHaveFocus();
    expect(summary()).toBeNull();
    expect(field('Given name')).toHaveValue('Jane');
    expect(pageHeading()).toHaveAccessibleName('Add participant');
  });

  it('sends the add once when no answer comes in 10 seconds, and says there was no connection', async () => {
    await openAdd();
    serve('POST', participantsPath, noAnswer);
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();
    await wait(9_000);
    expect(alert()).toBeEmptyDOMElement();
    await wait(60_000);

    expect(adds()).toHaveLength(1);
    expect(alert()).toHaveTextContent('Not saved: no connection. Try again.');
    expect(saveButton()).toHaveFocus();
  });

  it('says that something went wrong for a server error, and the next press empties it', async () => {
    await openAdd();
    serve('POST', participantsPath, problem(500));
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    expect(alert()).toHaveTextContent('Not saved: something went wrong. Try again.');
    serve('POST', participantsPath, noAnswer);

    await save();

    expect(alert()).toBeEmptyDOMElement();
    expect(adds()).toHaveLength(2);
  });

  it('shows Save busy after 400 ms, and a press while it is sends nothing more', async () => {
    await openAdd();
    serve('POST', participantsPath, noAnswer);
    fill('Jane', 'Citizen', '7', '3', '1987');
    const button = saveButton();
    await press(button);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('data-busy');

    await wait(400);
    expect(button).toHaveAttribute('data-busy');
    expect(button).toHaveAccessibleName('Saving…');
    await press(button);

    expect(adds()).toHaveLength(1);
  });

  it.each([
    ['givenName', 'Enter the given name', ['Given name']],
    ['familyName', 'Enter the family name', ['Family name']],
    ['dateOfBirth', 'Date of birth must be in the past', ['Day', 'Month', 'Year']],
  ])("shows the server's refusal of %s on its field, as Save's own checks would", async (name, message, fields) => {
    await openAdd();
    serve('POST', participantsPath, problem(422, { code: 'validation.failed', errors: { [name]: ['Refused'] } }));
    fill('Jane', 'Citizen', '30', '9', '2026');

    await save();

    expect(inlineMessages()).toEqual([message]);
    expect(summaryLinks()).toEqual([message]);
    expect(summary()).toHaveFocus();
    expect(invalidFields()).toEqual(fields);
    expect(alert()).toBeEmptyDOMElement();
    expect(document.title).toBe('Error: Grow2Notes – Add participant');
  });

  it('says that something went wrong for a refusal of a field that the page does not have', async () => {
    await openAdd();
    serve('POST', participantsPath, problem(422, { code: 'validation.failed', errors: { madeUp: ['Refused'] } }));
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();

    expect(alert()).toHaveTextContent('Not saved: something went wrong. Try again.');
    expect(summary()).toBeNull();
    expect(saveButton()).toHaveFocus();
  });

  it('shows a worker Page not found, and sends nothing', async () => {
    renderApp('/manage/participants/new', worker);
    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(fetchMock.mock.calls.filter(([path]) => path !== mePath)).toEqual([]);
  });
});

/** The page's `<h1>`, or `null` while it has none. */
function headingOrNone(): HTMLElement | null {
  return screen.queryByRole('heading', { level: 1 });
}

/** The back link to the list, wherever it is. */
function backLink(): HTMLElement | null {
  return screen.queryByRole('link', { name: 'Participants' });
}

/** The region's line: the status region in `<main>` that shows outside the Details, beside the page's hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden') && region.closest('section') === null);
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** Each field's value, in page order. */
function values(): string[] {
  return (['Given name', 'Family name', 'Day', 'Month', 'Year'] as const).map(
    (name) => (field(name) as HTMLInputElement).value,
  );
}

/** Opens Jane's page from her row of the list, as the manager does, the list holding `people`. */
async function openJane(people: AdminParticipant[] = [jane, priya]): Promise<Router> {
  serve('GET', listPath, json(people));
  const router = renderApp('/manage/participants');
  await settle();
  await press(within(main()).getByRole('link', { name: 'Jane Citizen' }));
  expect(router.state.location.pathname).toBe(`/manage/participants/${jane.id}`);
  return router;
}

/**
 * Opens a participant's page at `path` from Manage, as a link to it would, so that the shell's arrival focus is due
 * whenever the page's `<h1>` comes.
 */
async function openFromManage(path: string): Promise<Router> {
  const router = renderApp('/manage');
  await settle();
  await act(() => router.navigate(path));
  return router;
}

describe("A participant's page", () => {
  it('opens from its row with the name as the <h1>, focused, and the Details filled in from the list', async () => {
    await openJane();

    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    // The date of birth as a person types it, with no leading zeros.
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
    expect(document.title).toBe('Grow2Notes – Participant');
    expect(currentItems()).toEqual(['Manage true']);
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(summary()).toBeNull();
  });

  it('has its back link to the list, which goes back to the row opened, focusing it', async () => {
    const router = await openJane();
    expect(backLink()).toHaveAttribute('href', '/manage/participants');
    expect(main()).not.toContainElement(backLink());

    await press(backLink()!);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(within(main()).getByRole('link', { name: 'Jane Citizen' })).toHaveFocus();
  });

  it('holds no name or date of birth in its address or title', async () => {
    const router = await openJane();
    type(field('Given name'), 'Janet');

    const { pathname, search, hash } = router.state.location;
    for (const text of [document.title, `${pathname}${search}${hash}`, JSON.stringify(router.state.location.state)]) {
      for (const detail of ['Jane', 'Janet', 'Citizen', '1987']) {
        expect(text).not.toContain(detail);
      }
    }
  });

  it('sends the correction with the version that it loaded, then shows when it was saved, focus on Save', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, corrected);
    serve('GET', listPath, json([janeSaved, priya]));
    const button = saveButton();
    type(field('Family name'), ' Citizen-Hall ');

    await save();

    expect(corrections()).toEqual([
      {
        path: `${participantsPath}/${jane.id}`,
        ifMatch: '"AAAAAAAAB9E="',
        body: { givenName: 'Jane', familyName: 'Citizen-Hall', dateOfBirth: '1987-03-07' },
      },
    ]);
    const [, init] = fetchMock.mock.calls.find(([, sent]) => sent?.method === 'PUT')!;
    expect(new Headers(init?.headers).get('X-XSRF-TOKEN')).toBe('token-1');
    expect(saveStatus().textContent).toBe(`Saved 4:12${nbsp}pm`);
    expect(saveStatus().querySelector('time')).toHaveAttribute('dateTime', '2026-10-01T06:12:00.000Z');
    expect(button).toHaveFocus();
    expect(alert()).toBeEmptyDOMElement();
    expect(document.title).toBe('Grow2Notes – Participant');
  });

  it('changes the <h1> only once the save is done, to the name as saved, never as it is typed', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, later(2000, corrected));
    serve('GET', listPath, json([{ ...janeSaved, givenName: 'Janet' }, priya]));

    type(field('Given name'), ' Janet ');
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    await save();
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    await wait(2000);

    expect(pageHeading()).toHaveAccessibleName('Janet Citizen');
    expect(field('Given name')).toHaveValue(' Janet ');
  });

  it('reads the list again after a save, and sends its version with the next', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, corrected);
    serve('GET', listPath, json([janeSaved, priya]));
    type(field('Family name'), 'Citizen-Hall');
    await save();
    const listReads = requests().filter((request) => request === `GET ${listPath}`).length;

    type(field('Day'), '17');
    await save();

    expect(corrections().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="', '"AAAAAAAAB+A="']);
    // Each save's own read follows its answer.
    expect(requests().slice(-2)).toEqual([`PUT ${participantsPath}/${jane.id}`, `GET ${listPath}`]);
    expect(requests().filter((request) => request === `GET ${listPath}`)).toHaveLength(listReads + 1);
  });

  it('empties "Saved" at the next edit, so that it never stands for changes not saved', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, corrected);
    serve('GET', listPath, json([janeSaved, priya]));
    type(field('Family name'), 'Citizen-Hall');
    await save();
    expect(saveStatus()).toHaveTextContent('Saved');

    type(field('Month'), '4');

    expect(saveStatus()).toBeEmptyDOMElement();
  });

  it('keeps what is typed when the list is fetched again, and sends the version that it loaded', async () => {
    serve('GET', listPath, json([jane, priya]));
    const router = renderApp('/manage/participants');
    await settle();
    // The page reads the list again as it opens; another manager has corrected Jane meanwhile.
    serve('GET', listPath, later(500, json([janeChangedMeanwhile, priya])));
    await press(within(main()).getByRole('link', { name: 'Jane Citizen' }));
    expect(router.state.location.pathname).toBe(`/manage/participants/${jane.id}`);
    type(field('Family name'), 'Citizen-Hall');

    await wait(500);

    // Copied once, as the page opened: neither the typed name nor the other fields take the list's new values.
    expect(values()).toEqual(['Jane', 'Citizen-Hall', '7', '3', '1987']);
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');

    serve('PUT', `${participantsPath}/${jane.id}`, problem(412, { code: 'precondition.failed' }));
    await save();

    expect(corrections().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="']);
  });

  it(
    "says that someone else changed the details on a 412, keeping what is typed, and sends the list's version next",
    async () => {
      await openJane();
      serve('PUT', `${participantsPath}/${jane.id}`, problem(412, { code: 'precondition.failed' }));
      serve('GET', listPath, json([janeChangedMeanwhile, priya]));
      type(field('Family name'), 'Citizen-Hall');
      const button = saveButton();

      await save();

      expect(alert()).toHaveTextContent(
        'Someone else changed these details while you were editing. Check the details, then save again.',
      );
      expect(button).toHaveFocus();
      expect(values()).toEqual(['Jane', 'Citizen-Hall', '7', '3', '1987']);
      expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
      expect(saveStatus()).toBeEmptyDOMElement();
      expect(summary()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Participant');
      // The list read again for the participant's version, after the refusal.
      expect(requests().slice(-2)).toEqual([`PUT ${participantsPath}/${jane.id}`, `GET ${listPath}`]);

      serve('PUT', `${participantsPath}/${jane.id}`, corrected);
      await save();

      expect(corrections().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="', '"AAAAAAAAB+E="']);
      expect(alert()).toBeEmptyDOMElement();
      expect(saveStatus()).toHaveTextContent('Saved');
    },
  );

  it('sends the correction once when its answer is lost, says so above Save, and keeps the fields', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, noConnection);
    type(field('Family name'), 'Citizen-Hall');

    await save();
    await wait(60_000);

    expect(corrections()).toHaveLength(1);
    expect(alert()).toHaveTextContent('Not saved: no connection. Try again.');
    expect(saveButton()).toHaveFocus();
    expect(values()).toEqual(['Jane', 'Citizen-Hall', '7', '3', '1987']);
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(summary()).toBeNull();
  });

  it('sends the correction once when no answer comes in 10 seconds, and says there was no connection', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, noAnswer);
    type(field('Family name'), 'Citizen-Hall');

    await save();
    await wait(9_000);
    expect(alert()).toBeEmptyDOMElement();
    await wait(60_000);

    expect(corrections()).toHaveLength(1);
    expect(alert()).toHaveTextContent('Not saved: no connection. Try again.');
    expect(saveButton()).toHaveFocus();
  });

  it('says that something went wrong for a server error, and the next press empties it', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, problem(500));
    type(field('Family name'), 'Citizen-Hall');
    await save();
    expect(alert()).toHaveTextContent('Not saved: something went wrong. Try again.');
    serve('PUT', `${participantsPath}/${jane.id}`, noAnswer);

    await save();

    expect(alert()).toBeEmptyDOMElement();
    expect(corrections()).toHaveLength(2);
  });

  it('checks the fields on Save, as Add participant does, and sends nothing while one fails', async () => {
    await openJane();
    type(field('Given name'), ' ');
    type(field('Year'), '2026');
    type(field('Month'), '10');
    type(field('Day'), '1');

    await save();

    expect(inlineMessages()).toEqual(['Enter the given name', 'Date of birth must be in the past']);
    expect(summaryLinks()).toEqual(['Enter the given name', 'Date of birth must be in the past']);
    expect(summary()).toHaveFocus();
    expect(document.title).toBe('Error: Grow2Notes – Participant');
    expect(corrections()).toEqual([]);
  });

  it("shows the server's refusal of a field on its field, as Save's own checks would", async () => {
    await openJane();
    serve(
      'PUT',
      `${participantsPath}/${jane.id}`,
      problem(422, { code: 'validation.failed', errors: { dateOfBirth: ['Refused'] } }),
    );
    type(field('Year'), '2026');
    type(field('Month'), '9');
    type(field('Day'), '30');

    await save();

    expect(inlineMessages()).toEqual(['Date of birth must be in the past']);
    expect(summary()).toHaveFocus();
    expect(invalidFields()).toEqual(['Day', 'Month', 'Year']);
    expect(alert()).toBeEmptyDOMElement();
  });

  it('reads the list before the next save when its read after a save did not load', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, corrected);
    serve('GET', listPath, noConnection);
    type(field('Family name'), 'Citizen-Hall');
    await save();
    // The read's silent retry, 1 s on, which fails too.
    await wait(1000);
    expect(saveStatus()).toHaveTextContent('Saved');
    serve('GET', listPath, json([janeSaved, priya]));

    type(field('Day'), '17');
    await save();

    expect(requests().slice(-3)).toEqual([
      `GET ${listPath}`,
      `PUT ${participantsPath}/${jane.id}`,
      `GET ${listPath}`,
    ]);
    expect(corrections().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="', '"AAAAAAAAB+A="']);
  });
});

describe('A participant just added', () => {
  it('saves the next change as a correction, with the version that the list gives them', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    serve('GET', listPath, json([{ ...jane, id: newId, rowVersion: 'AAAAAAAAB+A=' }, priya]));
    serve('PUT', `${participantsPath}/${newId}`, corrected);

    type(field('Family name'), 'Citizen-Hall');
    await save();

    expect(adds()).toHaveLength(1);
    expect(corrections()).toEqual([
      {
        path: `${participantsPath}/${newId}`,
        ifMatch: '"AAAAAAAAB+A="',
        body: { givenName: 'Jane', familyName: 'Citizen-Hall', dateOfBirth: '1987-03-07' },
      },
    ]);
    // Their version read first, as the add's answer gives none.
    expect(requests().slice(-3)).toEqual([
      `GET ${listPath}`,
      `PUT ${participantsPath}/${newId}`,
      `GET ${listPath}`,
    ]);
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen-Hall');
    expect(saveStatus().textContent).toBe(`Saved 4:12${nbsp}pm`);
    expect(saveButton()).toHaveFocus();
  });

  it('sends no correction, and says so, when their version cannot be read', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    serve('GET', listPath, noConnection);

    type(field('Family name'), 'Citizen-Hall');
    await save();
    await wait(1000);

    expect(corrections()).toEqual([]);
    expect(alert()).toHaveTextContent('Not saved: no connection. Try again.');
    expect(saveButton()).toHaveFocus();
  });
});

describe("A participant's page while the list loads", () => {
  it('shows its back link and no <h1>, then nothing more until "Loading participant…" at 1 s', async () => {
    serve('GET', listPath, noAnswer);
    renderApp(`/manage/participants/${jane.id}`);

    expect(backLink()).toBeInTheDocument();
    expect(headingOrNone()).not.toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expect(main().textContent).toBe('');

    await wait(1);
    expect(line()).toHaveTextContent('Loading participant…');
    expect(main().textContent).toBe('Loading participant…');
    expect(headingOrNone()).not.toBeInTheDocument();
  });

  it('focuses the name as it arrives, the focus having waited for it', async () => {
    serve('GET', listPath, later(2000, json([jane, priya])));
    await openFromManage(`/manage/participants/${jane.id}`);
    expect(headingOrNone()).not.toBeInTheDocument();

    await wait(2000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
  });

  it.each([
    ['no connection', noConnection],
    ['something went wrong', problem(500)],
  ])(
    'says that the participant did not load: %s, under the <h1> "Participant", which takes the focus',
    async (cause, answer) => {
      serve('GET', listPath, answer);
      await openFromManage(`/manage/participants/${jane.id}`);

      // After the query client's silent retry.
      await wait(1000);
      await settle();

      expect(pageHeading()).toHaveAccessibleName('Participant');
      expect(pageHeading()).toHaveFocus();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(line().textContent).toBe(`This participant did not load: ${cause}. Try again.`);
      expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
      expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
      expect(backLink()).toBeInTheDocument();
      expect(document.title).toBe('Grow2Notes – Participant');
    },
  );

  it(
    'says that the participant still did not load after a Try again that fails, focus still on Try again',
    async () => {
      serve('GET', listPath, noConnection);
      renderApp(`/manage/participants/${jane.id}`);
      await wait(1000);
      await settle();
      const button = within(main()).getByRole('button', { name: 'Try again' });

      await press(button);
      // Its one silent retry, as on the first load.
      await wait(1000);
      await settle();

      expect(line().textContent).toBe('This participant still did not load: no connection. Try again.');
      expect(pageHeading()).toHaveAccessibleName('Participant');
      expect(button).toHaveFocus();
    },
  );

  it('shows the participant after a Try again that loads, with focus on their name, the one <h1>', async () => {
    serve('GET', listPath, noConnection);
    renderApp(`/manage/participants/${jane.id}`);
    await wait(1000);
    await settle();
    serve('GET', listPath, json([jane, priya]));

    await press(within(main()).getByRole('button', { name: 'Try again' }));

    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([pageHeading()]);
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
  });
});

describe('A participant who cannot be shown', () => {
  it.each([unknownId, 'not-an-id'])(
    'shows Page not found in place of the whole page for %s, an ID not on the list, at the same address',
    async (id) => {
      serve('GET', listPath, json([jane, priya]));
      const router = renderApp(`/manage/participants/${id}`);

      await settle();

      expect(pageHeading()).toHaveAccessibleName('Page not found');
      expect(document.title).toBe('Grow2Notes – Page not found');
      expect(backLink()).not.toBeInTheDocument();
      expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
      expect(router.state.location.pathname).toBe(`/manage/participants/${id}`);
    },
  );

  it('shows Page not found in place of the whole page for a 403 on the list', async () => {
    // React and React Router report the refusal that the route's boundary catches, which is expected here.
    const report = console.error;
    const reported = vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
      if (!args.some((arg) => arg instanceof ApiError && arg.status === 403)) {
        report(...args);
      }
    });
    onTestFinished(() => reported.mockRestore());
    serve('GET', listPath, problem(403));
    renderApp(`/manage/participants/${jane.id}`);

    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(backLink()).not.toBeInTheDocument();
  });

  it('shows a worker Page not found, and sends nothing', async () => {
    serve('GET', listPath, json([jane, priya]));
    renderApp(`/manage/participants/${jane.id}`, worker);
    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(fetchMock.mock.calls.filter(([path]) => path !== mePath)).toEqual([]);
  });
});

const archivePath = `${participantsPath}/${jane.id}/archive`;
const restorePath = `${participantsPath}/${jane.id}/restore`;

// The archived notice's words for Jane (participants.md B3).
const archivedWords =
  'Jane Citizen is archived, so this page is read-only. They are not on Today and no new notes can be written for '
  + 'them. Their past notes are kept.';

/** The Actions section, or `null` while the page has none. */
function actionsSection(): HTMLElement | null {
  return within(main()).queryByRole('region', { name: 'Actions' });
}

function archiveButton(): HTMLElement {
  return within(main()).getByRole('button', { name: 'Archive participant' });
}

/** The alert above Archive participant, in Actions. */
function archiveAlert(): HTMLElement {
  return within(actionsSection()!).getByRole('alert');
}

/** The archived notice, by its words, whatever the name, or `null` while the page shows none. */
function notice(): HTMLElement | null {
  // The words are in the notice's body, beside its icon.
  return within(main()).queryByText(/ is archived, so this page is read-only\./)?.parentElement?.parentElement ?? null;
}

function restoreButton(): HTMLElement {
  return within(notice()!).getByRole('button', { name: 'Restore' });
}

/** The alert above Restore, in the notice. */
function restoreAlert(): HTMLElement {
  return within(notice()!).getByRole('alert');
}

/** Each row of the read-only Details, as its term and its value. */
function detailsRows(): string[][] {
  return Array.from(detailsSection().querySelectorAll('dl > div'), (row) => [
    row.querySelector('dt')?.textContent ?? '',
    row.querySelector('dd')?.textContent ?? '',
  ]);
}

/** The names of the list's rows, in order. */
function rowNames(): string[] {
  return Array.from(main().querySelectorAll('[data-return-key]'), (row) => row.textContent);
}

/** Where Tab goes next from `element`, as a page with no positive `tabindex` orders the stops. */
function nextTabStop(element: HTMLElement): HTMLElement | undefined {
  const stops = Array.from(
    document.body.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]'),
  ).filter((stop) => stop.tabIndex >= 0);
  return stops.find((stop) => (element.compareDocumentPosition(stop) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0);
}

function follows(before: Node, after: Node): boolean {
  return (before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

/** Opens archived Jane's page from her row of the list's Archived view, as the manager does. */
async function openArchivedJane(): Promise<Router> {
  serve('GET', listPath, json([janeArchived, priya]));
  const router = renderApp('/manage/participants?view=archived');
  await settle();
  await press(within(main()).getByRole('link', { name: 'Jane Citizen' }));
  expect(router.state.location.pathname).toBe(`/manage/participants/${jane.id}`);
  return router;
}

describe('Archive participant', () => {
  it('is in Actions under Details, alone, its empty alert above it, and the next Tab stop after Add goal', async () => {
    await openJane();

    const actions = actionsSection()!;
    const button = archiveButton();
    expect(within(actions).getByRole('heading', { level: 2 })).toHaveAccessibleName('Actions');
    expect(follows(detailsSection(), actions)).toBe(true);
    expect(within(actions).getAllByRole('button')).toEqual([button]);
    expect(within(actions).queryAllByRole('link')).toEqual([]);
    expect(button).toHaveAttribute('type', 'button');
    expect(archiveAlert()).toBeEmptyDOMElement();
    expect(follows(archiveAlert(), button)).toBe(true);
    expect(nextTabStop(addGoalButton())).toBe(button);
    expect(notice()).toBeNull();
    expect(backLink()).toHaveAttribute('href', '/manage/participants');
  });

  it('comes with the participant on Add participant, the next Tab stop after Add goal once the 201 is in', async () => {
    await openAdd();
    expect(actionsSection()).toBeNull();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();

    expect(nextTabStop(addGoalButton())).toBe(archiveButton());
  });

  it(
    'archives with the version that the page loaded, then shows the page read-only, focusing the notice',
    async () => {
      const router = await openJane();
      serve('POST', archivePath, noContent);
      serve('GET', listPath, json([janeArchived, priya]));

      await press(archiveButton());

      // No dialog first, no body, and the list read again after the answer, for her next version.
      expect(statusChanges()).toEqual([
        { path: archivePath, ifMatch: '"AAAAAAAAB9E="', token: 'token-1', body: undefined },
      ]);
      expect(requests().slice(-2)).toEqual([`POST ${archivePath}`, `GET ${listPath}`]);
      expect(notice()).toHaveFocus();
      expect(notice()).toHaveAttribute('tabindex', '-1');
      for (const attribute of ['role', 'aria-live', 'data-route-focus']) {
        expect(notice()).not.toHaveAttribute(attribute);
      }
      expect(within(notice()!).getByText(archivedWords)).toBeInTheDocument();
      expect(nextTabStop(notice()!)).toBe(restoreButton());
      expect(restoreAlert()).toBeEmptyDOMElement();
      expect(follows(pageHeading(), notice()!)).toBe(true);
      expect(follows(notice()!, detailsSection())).toBe(true);
      expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
      expect(detailsRows()).toEqual([
        ['Given name', 'Jane'],
        ['Family name', 'Citizen'],
        ['Date of birth', `7${nbsp}March 1987`],
      ]);
      expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
      expect(within(main()).queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
      expect(within(main()).queryByRole('button', { name: 'Archive participant' })).not.toBeInTheDocument();
      expect(actionsSection()).toBeNull();
      expect(backLink()).toHaveAttribute('href', '/manage/participants?view=archived');
      expect(document.title).toBe('Grow2Notes – Participant');
      expect(router.state.location.pathname).toBe(`/manage/participants/${jane.id}`);
    },
  );

  it('drops what was typed, and the error summary, as the page turns read-only', async () => {
    await openJane();
    type(field('Given name'), ' ');
    type(field('Family name'), 'Citizen-Hall');
    await save();
    expect(summary()).toHaveFocus();
    serve('POST', archivePath, noContent);
    serve('GET', listPath, json([janeArchived, priya]));

    await press(archiveButton());

    expect(corrections()).toEqual([]);
    expect(summary()).toBeNull();
    expect(document.title).toBe('Grow2Notes – Participant');
    expect(detailsRows().map(([, value]) => value)).toEqual(['Jane', 'Citizen', `7${nbsp}March 1987`]);
    expect(notice()).toHaveFocus();
  });

  it.each([
    ['no connection', noConnection],
    ['something went wrong', problem(500)],
  ])('says "Not archived: %s" above it, sent once, focus staying on it, and the next press empties it', async (
    cause,
    answer,
  ) => {
    await openJane();
    serve('POST', archivePath, answer);
    const button = archiveButton();

    await press(button);
    await wait(60_000);

    expect(statusChanges()).toHaveLength(1);
    expect(archiveAlert()).toHaveTextContent(`Not archived: ${cause}. Try again.`);
    expect(button).toHaveFocus();
    expect(notice()).toBeNull();
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);

    serve('POST', archivePath, noAnswer);
    await press(button);

    expect(archiveAlert()).toBeEmptyDOMElement();
    expect(statusChanges()).toHaveLength(2);
  });

  it('shows itself busy after 400 ms, "Archiving…", and a press while it is sends nothing more', async () => {
    await openJane();
    serve('POST', archivePath, noAnswer);
    const button = archiveButton();

    await press(button);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('data-busy');
    await wait(400);

    expect(button).toHaveAttribute('data-busy');
    expect(button).toHaveAccessibleName('Archiving…');
    await press(button);
    expect(statusChanges()).toHaveLength(1);
  });

  it('on a 412 reads the list again, and shows them archived, as someone else has, focusing the notice', async () => {
    await openJane();
    serve('POST', archivePath, problem(412, { code: 'precondition.failed' }));
    serve('GET', listPath, json([janeArchived, priya]));

    await press(archiveButton());

    expect(requests().slice(-2)).toEqual([`POST ${archivePath}`, `GET ${listPath}`]);
    expect(notice()).toHaveFocus();
    expect(restoreAlert()).toBeEmptyDOMElement();
    expect(actionsSection()).toBeNull();
    expect(backLink()).toHaveAttribute('href', '/manage/participants?view=archived');
  });

  it(
    'on a 412 that finds them still active, says that someone else changed them, and sends the list\'s version next',
    async () => {
      await openJane();
      serve('POST', archivePath, problem(412, { code: 'precondition.failed' }));
      serve('GET', listPath, json([janeSaved, priya]));
      const button = archiveButton();

      await press(button);

      expect(archiveAlert()).toHaveTextContent(
        'Someone else changed this participant just now. Check the page, then try again.',
      );
      expect(button).toHaveFocus();
      expect(notice()).toBeNull();
      // Nothing else changes: the fields keep their copy, and the <h1> the name as the page knew it.
      expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
      expect(pageHeading()).toHaveAccessibleName('Jane Citizen');

      serve('POST', archivePath, noContent);
      serve('GET', listPath, json([{ ...janeSaved, status: 'Archived', rowVersion: 'AAAAAAAAB+E=' }, priya]));
      await press(button);

      expect(statusChanges().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="', '"AAAAAAAAB+A="']);
      expect(notice()).toHaveFocus();
      // Archived, as the list now gives them.
      expect(pageHeading()).toHaveAccessibleName('Jane Citizen-Hall');
      expect(detailsRows().map(([, value]) => value)).toEqual(['Jane', 'Citizen-Hall', `7${nbsp}March 1987`]);
    },
  );

  it('archives a participant just added with the version that the list gives them, read first', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    serve('GET', listPath, json([{ ...jane, id: newId, rowVersion: 'AAAAAAAAB+A=' }, priya]));
    serve('POST', `${participantsPath}/${newId}/archive`, noContent);

    await press(archiveButton());

    expect(statusChanges().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB+A="']);
    expect(requests().slice(-3)).toEqual([
      `GET ${listPath}`,
      `POST ${participantsPath}/${newId}/archive`,
      `GET ${listPath}`,
    ]);
    expect(notice()).toHaveFocus();
  });

  it(
    'shows them archived when the read after the archive does not load, and reads the list before Restore',
    async () => {
      await openJane();
      serve('POST', archivePath, noContent);
      serve('GET', listPath, noConnection);

      await press(archiveButton());
      // The read's silent retry, 1 s on, which fails too.
      await wait(1000);

      expect(notice()).toHaveFocus();
      expect(detailsRows().map(([, value]) => value)).toEqual(['Jane', 'Citizen', `7${nbsp}March 1987`]);
      serve('GET', listPath, json([janeArchived, priya]));
      serve('POST', restorePath, noContent);

      await press(restoreButton());

      expect(requests().slice(-3)).toEqual([`GET ${listPath}`, `POST ${restorePath}`, `GET ${listPath}`]);
      expect(statusChanges().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB9E="', '"AAAAAAAAB+I="']);
    },
  );

  it('turns the page read-only when a Details save finds them archived by someone else', async () => {
    await openJane();
    serve('PUT', `${participantsPath}/${jane.id}`, problem(412, { code: 'precondition.failed' }));
    serve('GET', listPath, json([janeArchived, priya]));
    type(field('Family name'), 'Citizen-Hall');

    await save();

    expect(notice()).toHaveFocus();
    expect(detailsRows().map(([, value]) => value)).toEqual(['Jane', 'Citizen', `7${nbsp}March 1987`]);
    expect(within(main()).queryByText(/Someone else changed/)).not.toBeInTheDocument();
  });
});

describe("An archived participant's page", () => {
  it('opens read-only, the notice static and unfocused under the name, which takes the focus', async () => {
    await openArchivedJane();

    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(within(notice()!).getByText(archivedWords)).toBeInTheDocument();
    expect(notice()).not.toHaveAttribute('tabindex');
    expect(follows(pageHeading(), notice()!)).toBe(true);
    expect(restoreAlert()).toBeEmptyDOMElement();
    expect(follows(restoreAlert(), restoreButton())).toBe(true);
    expect(restoreButton()).toHaveAttribute('type', 'button');
    expect(nextTabStop(pageHeading())).toBe(restoreButton());
    expect(within(detailsSection()).getByRole('heading', { level: 2 })).toHaveAccessibleName('Details');
    expect(detailsRows()).toEqual([
      ['Given name', 'Jane'],
      ['Family name', 'Citizen'],
      ['Date of birth', `7${nbsp}March 1987`],
    ]);
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    // No Actions until later tasks add the links that an archived participant keeps.
    expect(actionsSection()).toBeNull();
    expect(within(main()).queryByRole('heading', { name: 'Actions' })).not.toBeInTheDocument();
    expect(backLink()).toHaveAttribute('href', '/manage/participants?view=archived');
    expect(document.title).toBe('Grow2Notes – Participant');
  });

  it('restores with the version that it loaded, then is the form again, focusing the <h1>, no banner', async () => {
    await openArchivedJane();
    serve('POST', restorePath, noContent);
    serve('GET', listPath, json([janeRestored, priya]));

    await press(restoreButton());

    expect(statusChanges()).toEqual([
      { path: restorePath, ifMatch: '"AAAAAAAAB+I="', token: 'token-1', body: undefined },
    ]);
    expect(requests().slice(-2)).toEqual([`POST ${restorePath}`, `GET ${listPath}`]);
    expect(notice()).toBeNull();
    expect(pageHeading()).toHaveFocus();
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(within(main()).queryByText(/restored/i)).not.toBeInTheDocument();
    expect(main().querySelector('[data-route-focus]')).toBeNull();
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(archiveAlert()).toBeEmptyDOMElement();
    expect(backLink()).toHaveAttribute('href', '/manage/participants');

    // Editable again, with the version that the list gave after the restore.
    serve('PUT', `${participantsPath}/${jane.id}`, corrected);
    serve('GET', listPath, json([{ ...janeRestored, familyName: 'Citizen-Hall', rowVersion: 'AAAAAAAAB+Q=' }, priya]));
    type(field('Family name'), 'Citizen-Hall');
    await save();

    expect(corrections().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB+M="']);
    expect(saveStatus()).toHaveTextContent('Saved');
  });

  it.each([
    ['no connection', noConnection],
    ['something went wrong', problem(500)],
  ])('says "Not restored: %s" in the notice above Restore, which keeps the focus', async (cause, answer) => {
    await openArchivedJane();
    serve('POST', restorePath, answer);
    const button = restoreButton();

    await press(button);
    await wait(60_000);

    expect(statusChanges()).toHaveLength(1);
    expect(restoreAlert()).toHaveTextContent(`Not restored: ${cause}. Try again.`);
    expect(button).toHaveFocus();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();

    serve('POST', restorePath, noAnswer);
    await press(button);

    expect(restoreAlert()).toBeEmptyDOMElement();
  });

  it('shows Restore busy after 400 ms, "Restoring…", and a press while it is sends nothing more', async () => {
    await openArchivedJane();
    serve('POST', restorePath, noAnswer);
    const button = restoreButton();

    await press(button);
    await wait(400);

    expect(button).toHaveAttribute('data-busy');
    expect(button).toHaveAccessibleName('Restoring…');
    await press(button);
    expect(statusChanges()).toHaveLength(1);
  });

  it('on a 412 reads the list again, and shows them active, as someone else has, focusing the <h1>', async () => {
    await openArchivedJane();
    serve('POST', restorePath, problem(412, { code: 'precondition.failed' }));
    serve('GET', listPath, json([janeRestored, priya]));

    await press(restoreButton());

    expect(requests().slice(-2)).toEqual([`POST ${restorePath}`, `GET ${listPath}`]);
    expect(notice()).toBeNull();
    expect(pageHeading()).toHaveFocus();
    expect(values()).toEqual(['Jane', 'Citizen', '7', '3', '1987']);
    expect(archiveAlert()).toBeEmptyDOMElement();
  });

  it(
    'on a 412 that finds them still archived, says so in the notice, and sends the list\'s version next',
    async () => {
      await openArchivedJane();
      serve('POST', restorePath, problem(412, { code: 'precondition.failed' }));
      serve('GET', listPath, json([{ ...janeArchived, rowVersion: 'AAAAAAAAB+Q=' }, priya]));
      const button = restoreButton();

      await press(button);

      expect(restoreAlert()).toHaveTextContent(
        'Someone else changed this participant just now. Check the page, then try again.',
      );
      expect(button).toHaveFocus();
      serve('POST', restorePath, noContent);
      serve('GET', listPath, json([janeRestored, priya]));

      await press(button);

      expect(statusChanges().map(({ ifMatch }) => ifMatch)).toEqual(['"AAAAAAAAB+I="', '"AAAAAAAAB+Q="']);
      expect(notice()).toBeNull();
      expect(pageHeading()).toHaveFocus();
    },
  );
});

describe('The list after Archive and Restore', () => {
  it(
    'pops from the back link of a participant archived on their page to the Active view, focusing its <h1>',
    async () => {
      const router = await openJane();
      serve('POST', archivePath, noContent);
      serve('GET', listPath, json([janeArchived, priya]));
      await press(archiveButton());
      expect(backLink()).toHaveAttribute('href', '/manage/participants?view=archived');

      await press(backLink()!);

      // The entry that opened her, where her row has gone.
      expect(router.state.historyAction).toBe('POP');
      expect(router.state.location.pathname).toBe('/manage/participants');
      expect(router.state.location.search).toBe('');
      expect(pageHeading()).toHaveAccessibleName('Participants');
      expect(pageHeading()).toHaveFocus();
      expect(rowNames()).toEqual(['Priya Kumar']);

      await press(within(main()).getByRole('link', { name: 'Archived' }));

      expect(rowNames()).toEqual(['Jane Citizen']);
    },
  );

  it('goes there by browser Back too, focusing its <h1>', async () => {
    const router = await openJane();
    serve('POST', archivePath, noContent);
    serve('GET', listPath, json([janeArchived, priya]));
    await press(archiveButton());

    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(pageHeading()).toHaveFocus();
    expect(rowNames()).toEqual(['Priya Kumar']);
  });

  it('shows a participant restored on their page in the Active view only', async () => {
    const router = await openArchivedJane();
    serve('POST', restorePath, noContent);
    serve('GET', listPath, json([janeRestored, priya]));
    await press(restoreButton());

    await press(backLink()!);

    // The Archived view that opened her, where she is no longer.
    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.search).toBe('?view=archived');
    expect(pageHeading()).toHaveFocus();
    expect(rowNames()).toEqual([]);
    expect(within(main()).getByText('No archived participants.')).toBeInTheDocument();

    await press(within(main()).getByRole('link', { name: 'Active' }));

    expect(rowNames()).toEqual(['Jane Citizen', 'Priya Kumar']);
  });
});

// Jane's goals as the API gives them (A35), in its order, two active and two archived, the newer archived second.
const janesGoals: ListRow[] = [
  {
    id: '6b1e9d4a-3c27-4f58-8a90-1d2e3f4a5b6c',
    text: 'Makes own breakfast',
    sortOrder: 0,
    archivedAtUtc: null,
    rowVersion: 'AAAAAAAAC9E=',
  },
  {
    id: '7c2f0e5b-4d38-4a69-9b01-2e3f4a5b6c7d',
    text: 'Uses the washing machine',
    sortOrder: 1,
    archivedAtUtc: '2026-09-21T02:30:00Z',
    rowVersion: 'AAAAAAAAC9I=',
  },
  {
    id: '8d3a1f6c-5e49-4b7a-8c12-3f4a5b6c7d8e',
    text: 'Catches the bus to day program and phones when she arrives',
    sortOrder: 2,
    archivedAtUtc: null,
    rowVersion: 'AAAAAAAAC9M=',
  },
  {
    id: '9e4b2a7d-6f5a-4c8b-9d23-4a5b6c7d8e9f',
    text: 'Walks to the shop',
    sortOrder: 3,
    archivedAtUtc: '2026-09-28T05:45:00Z',
    rowVersion: 'AAAAAAAAC9Q=',
  },
];

// Jane's active goals' texts, in the API's order.
const janesActiveGoals = ['Makes own breakfast', 'Catches the bus to day program and phones when she arrives'];

const goalsHelp =
  'Changes apply to notes started from now on. Notes already started or submitted keep the wording they were written '
  + 'with.';

const noGoals = 'No goals yet. Notes will show an empty Goals section.';

/** The Goals section, or `null` while the page has none. */
function goalsSection(): HTMLElement | null {
  return within(main()).queryByRole('region', { name: 'Goals' });
}

/** The texts of the active goals, numbered in the Goals section's `<ol>`, in order, each its row's first paragraph. */
function activeGoals(): (string | undefined)[] {
  return Array.from(goalsSection()!.querySelectorAll('ol > li'), (item) => item.querySelector('p')?.textContent);
}

/** The texts of the archived goals, in the Goals section's `<details>`, in order. */
function archivedGoals(): string[] {
  return Array.from(goalsSection()!.querySelectorAll('details ul > li'), (item) => item.textContent);
}

/** The Goals section's New goal field. */
function newGoal(): HTMLElement {
  return within(goalsSection()!).getByRole('textbox', { name: 'New goal' });
}

/** The Goals section's Add goal, by its name while it is not busy. */
function addGoalButton(): HTMLElement {
  return within(goalsSection()!).getByRole('button', { name: 'Add goal' });
}

/** The page's status region, which a goal's add writes to. */
function pageStatus(): HTMLElement {
  return main().querySelector<HTMLElement>('p.visually-hidden[role="status"]')!;
}

/** The path that a goal of the participant whose ID is `participantId` is added at. */
function goalAddPath(participantId: string): string {
  return `${participantsPath}/${participantId}/goals`;
}

/** The bare `201` of a goal's add. */
const goalAdded: Answer = () => Promise.resolve(new Response(null, { status: 201 }));

/** The goals' adds sent, each as its path, its token and the JSON of its body. */
function goalAdds(): { path: string; token: string | null; body: unknown }[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => init?.method === 'POST' && (path as string).endsWith('/goals'))
    .map(([path, init]) => ({
      path: path as string,
      token: new Headers(init?.headers).get('X-XSRF-TOKEN'),
      body: JSON.parse(init?.body as string) as unknown,
    }));
}

/** The path that the goal whose ID is `goalId` is reworded at. */
function goalUpdatePath(goalId: string): string {
  return `/api/admin/goals/${goalId}`;
}

/** The bare `200` of a goal's reword, with the goal's new `ETag`, which the page does not read. */
const goalReworded: Answer = () =>
  Promise.resolve(new Response(null, { status: 200, headers: { ETag: '"AAAAAAAAD9F="' } }));

/** The goals' rewords sent, each as its path, its `If-Match`, its token and the JSON of its body. */
function goalRewords(): { path: string; ifMatch: string | null; token: string | null; body: unknown }[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => init?.method === 'PUT' && (path as string).startsWith('/api/admin/goals/'))
    .map(([path, init]) => {
      const headers = new Headers(init?.headers);
      return {
        path: path as string,
        ifMatch: headers.get('If-Match'),
        token: headers.get('X-XSRF-TOKEN'),
        body: JSON.parse(init?.body as string) as unknown,
      };
    });
}

/** A goal as the list gives it once it is added, first in the list. */
const breakfastAdded: ListRow = {
  id: '8d3a1f6c-5e49-4b7a-ac12-3f4a5b6c7d8e',
  text: 'Makes own breakfast',
  sortOrder: 0,
  archivedAtUtc: null,
  rowVersion: 'AAAAAAAAD9E=',
};

/** Opens Jane's page from her row of the list, as the manager does, her goals being `goals`. */
async function openJaneWithGoals(goals: ListRow[]): Promise<Router> {
  serve('GET', goalsPath(jane.id), json(goals));
  return openJane();
}

describe("A participant's goals", () => {
  it('are in Goals, between Details and Actions: the help text, then the active ones numbered, in order', async () => {
    await openJaneWithGoals(janesGoals);

    const goals = goalsSection()!;
    expect(within(goals).getByRole('heading', { level: 2 })).toHaveAccessibleName('Goals');
    expect(follows(detailsSection(), goals)).toBe(true);
    expect(follows(goals, actionsSection()!)).toBe(true);
    expect(follows(within(goals).getByText(goalsHelp), goals.querySelector('ol')!)).toBe(true);
    expect(activeGoals()).toEqual(janesActiveGoals);
    for (const item of goals.querySelectorAll<HTMLElement>('ol > li')) {
      expect(within(item).getByRole('alert')).toBeEmptyDOMElement();
    }
  });

  it('show the archived ones newest archived first, in a closed section after the list', async () => {
    await openJaneWithGoals(janesGoals);

    const details = goalsSection()!.querySelector('details')!;
    expect(details.open).toBe(false);
    expect(details.querySelector('summary')?.textContent).toBe('Archived goals');
    expect(follows(goalsSection()!.querySelector('ol')!, details)).toBe(true);
    expect(archivedGoals()).toEqual(['Walks to the shop', 'Uses the washing machine']);
  });

  it('say "No goals yet." in place of the list when there are none, with no archived section', async () => {
    await openJaneWithGoals([]);

    expect(within(goalsSection()!).getByText(noGoals)).toBeInTheDocument();
    expect(goalsSection()!.querySelector('ol')).toBeNull();
    expect(goalsSection()!.querySelector('details')).toBeNull();
  });

  it('are keyed by the participant alone, never by their words', async () => {
    await openJaneWithGoals(janesGoals);

    const keys = queryClient.getQueryCache().getAll().map(({ queryKey }) => JSON.stringify(queryKey));
    expect(keys).toContain(JSON.stringify(['admin', 'goals', jane.id]));
    for (const { text } of janesGoals) {
      expect(keys.filter((key) => key.includes(text))).toEqual([]);
    }
  });

  it('of an archived participant are numbered text, with no help, button or alert, and no Restore', async () => {
    serve('GET', goalsPath(jane.id), json(janesGoals));
    await openArchivedJane();

    const goals = goalsSection()!;
    expect(follows(detailsSection(), goals)).toBe(true);
    expect(activeGoals()).toEqual(janesActiveGoals);
    expect(archivedGoals()).toEqual(['Walks to the shop', 'Uses the washing machine']);
    expect(within(goals).queryByText(goalsHelp)).toBeNull();
    expect(within(goals).queryAllByRole('button')).toEqual([]);
    expect(within(goals).queryAllByRole('alert')).toEqual([]);
    expect(within(goals).queryAllByRole('textbox')).toEqual([]);
  });

  it('of an archived participant with none say "No goals yet."', async () => {
    await openArchivedJane();

    expect(within(goalsSection()!).getByText(noGoals)).toBeInTheDocument();
  });

  it("after Add participant's 201, are the next Tab stop from Save, at New goal, past the help", async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();

    expect(saveButton()).toHaveFocus();
    expect(nextTabStop(saveButton())).toBe(newGoal());
    expect(follows(within(goalsSection()!).getByText(noGoals), newGoal())).toBe(true);
  });

  it('take one added on the page: sent with the token, the list read again, then focus kept in New goal', async () => {
    await openJaneWithGoals([]);
    serve('POST', goalAddPath(jane.id), goalAdded);
    serve('GET', goalsPath(jane.id), json([breakfastAdded]));
    type(newGoal(), ' Makes own\nbreakfast ');

    await press(addGoalButton());

    expect(goalAdds()).toEqual([
      { path: goalAddPath(jane.id), token: 'token-1', body: { text: 'Makes own breakfast' } },
    ]);
    expect(requests().slice(-2)).toEqual([`POST ${goalAddPath(jane.id)}`, `GET ${goalsPath(jane.id)}`]);
    expect(activeGoals()).toEqual(['Makes own breakfast']);
    expect(newGoal()).toHaveValue('');
    expect(newGoal()).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Goal added to the end of the list');
  });

  it('take one reworded on the page: sent with token and version, the list read again, focus on Edit', async () => {
    await openJaneWithGoals(janesGoals);
    const [breakfast] = janesGoals;
    serve('PUT', goalUpdatePath(breakfast.id), goalReworded);
    serve(
      'GET',
      goalsPath(jane.id),
      json(janesGoals.map((goal) => (goal === breakfast ? { ...goal, text: 'Makes own lunch' } : goal))),
    );
    await press(within(goalsSection()!).getByRole('button', { name: `Edit, ${breakfast.text}` }));
    type(within(goalsSection()!).getByRole('textbox', { name: 'Edit goal' }), ' Makes own\nlunch ');

    await press(within(goalsSection()!).getByRole('button', { name: 'Save' }));

    expect(goalRewords()).toEqual([
      {
        path: goalUpdatePath(breakfast.id),
        ifMatch: `"${breakfast.rowVersion}"`,
        token: 'token-1',
        body: { text: 'Makes own lunch' },
      },
    ]);
    expect(requests().slice(-2)).toEqual([`PUT ${goalUpdatePath(breakfast.id)}`, `GET ${goalsPath(jane.id)}`]);
    expect(activeGoals()).toEqual(['Makes own lunch', janesActiveGoals[1]]);
    expect(within(goalsSection()!).getByRole('button', { name: 'Edit, Makes own lunch' })).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Goal saved');
  });

  it('of a participant just added take one added on the page, reading their list only after it', async () => {
    await openAdd();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();
    serve('POST', goalAddPath(newId), goalAdded);
    serve('GET', goalsPath(newId), json([breakfastAdded]));
    type(newGoal(), 'Makes own breakfast');

    await press(addGoalButton());

    expect(requests().slice(-2)).toEqual([`POST ${goalAddPath(newId)}`, `GET ${goalsPath(newId)}`]);
    expect(activeGoals()).toEqual(['Makes own breakfast']);
    expect(newGoal()).toHaveFocus();
  });

  it.each([
    ['lost', noConnection, 0],
    ['not given in 10 seconds', noAnswer, 10_000],
  ])('are added once when the answer is %s, which says so above Add goal, the text kept', async (_, answer, ms) => {
    await openJaneWithGoals([]);
    serve('POST', goalAddPath(jane.id), answer);
    type(newGoal(), 'Makes own breakfast');
    const button = addGoalButton();

    await press(button);
    await wait(ms);
    await wait(60_000);

    expect(goalAdds()).toHaveLength(1);
    expect(within(goalsSection()!).getByRole('alert')).toHaveTextContent('Not added: no connection. Try again.');
    expect(newGoal()).toHaveValue('Makes own breakfast');
    expect(button).toHaveFocus();
    expect(activeGoals()).toEqual([]);
  });

  it('are not on Add participant before its 201, then come empty, with no request for them', async () => {
    await openAdd();
    expect(goalsSection()).toBeNull();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');

    await save();
    await wait(60_000);

    expect(within(goalsSection()!).getByText(noGoals)).toBeInTheDocument();
    expect(follows(detailsSection(), goalsSection()!)).toBe(true);
    expect(follows(goalsSection()!, actionsSection()!)).toBe(true);
    expect(requests().filter((request) => request.includes('/goals'))).toEqual([]);
  });
});

describe("A participant's page while their goals load", () => {
  it('shows "Loading participant…" at 1 s while their goals are held, then the page, the name focused', async () => {
    serve('GET', listPath, json([jane, priya]));
    serve('GET', goalsPath(jane.id), later(2000, json(janesGoals)));
    await openFromManage(`/manage/participants/${jane.id}`);

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expect(headingOrNone()).not.toBeInTheDocument();
    expect(backLink()).toBeInTheDocument();

    await wait(1);
    expect(line()).toHaveTextContent('Loading participant…');
    expect(headingOrNone()).not.toBeInTheDocument();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();

    await wait(1000);
    await settle();
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(activeGoals()).toEqual(janesActiveGoals);
  });

  it('says that the participant did not load when their goals did not, and shows them after a Try again', async () => {
    serve('GET', listPath, json([jane, priya]));
    serve('GET', goalsPath(jane.id), noConnection);
    await openFromManage(`/manage/participants/${jane.id}`);

    // After the query client's silent retry.
    await wait(1000);
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Participant');
    expect(pageHeading()).toHaveFocus();
    expect(line().textContent).toBe('This participant did not load: no connection. Try again.');
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    expect(goalsSection()).toBeNull();
    expect(backLink()).toBeInTheDocument();

    serve('GET', goalsPath(jane.id), json(janesGoals));
    await press(within(main()).getByRole('button', { name: 'Try again' }));

    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([pageHeading()]);
    expect(pageHeading()).toHaveAccessibleName('Jane Citizen');
    expect(pageHeading()).toHaveFocus();
    expect(activeGoals()).toEqual(janesActiveGoals);
  });

  it('shows Page not found in place of the whole page when their goals are not found', async () => {
    // React and React Router report the refusal that the route's boundary catches, which is expected here.
    const report = console.error;
    const reported = vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
      if (!args.some((arg) => arg instanceof ApiError && arg.status === 404)) {
        report(...args);
      }
    });
    onTestFinished(() => reported.mockRestore());
    serve('GET', listPath, json([jane, priya]));
    serve('GET', goalsPath(jane.id), problem(404));
    renderApp(`/manage/participants/${jane.id}`);

    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(backLink()).not.toBeInTheDocument();
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
  });
});
