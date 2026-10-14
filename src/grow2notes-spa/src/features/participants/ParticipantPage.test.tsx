import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes } from '../../routes/routes.tsx';
import type { AdminParticipant } from './participantsQuery.ts';

type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const participantsPath = '/api/admin/participants';
const listPath = '/api/admin/participants?includeArchived=true';

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

// The new participant's ID, as the 201 gives it.
const newId = '7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20';

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

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(method: 'GET' | 'POST', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** The adds sent, each as the JSON of its body. */
function adds(): unknown[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => path === participantsPath && init?.method === 'POST')
    .map(([, init]) => JSON.parse(init?.body as string) as unknown);
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own and StrictMode, as `main.tsx`
 * renders it, with `['me']` holding `me` already, as after the start-up.
 */
function renderApp(path: string, me: Me = manager): Router {
  serve('GET', mePath, json(me));
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

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

async function settle() {
  await wait(1);
}

/** Opens Add participant from the list, as the manager does, with its link. */
async function openFromTheList(): Promise<Router> {
  serve('GET', listPath, json([jane]));
  const router = renderApp('/manage/participants');
  await settle();
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

/** The alert above Save. */
function alert(): HTMLElement {
  return within(main()).getByRole('alert');
}

/** The save status beside Save: the one region in `<main>` that shows, beside the page's hidden one. */
function saveStatus(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
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

  it('after a 201, Back goes to the list, not to an empty form', async () => {
    const router = await openFromTheList();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();

    await act(() => router.navigate(-1));
    await settle();

    expect(router.state.location.pathname).toBe('/manage/participants');
    expect(pageHeading()).toHaveAccessibleName('Participants');
  });

  it('after a 201, its back link goes back to the list, as Back does', async () => {
    const router = await openFromTheList();
    serve('POST', participantsPath, added);
    fill('Jane', 'Citizen', '7', '3', '1987');
    await save();

    await press(screen.getByRole('link', { name: 'Participants' }));

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/manage/participants');
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
