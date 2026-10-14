import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { onSessionEnded } from '../../api/session.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import textBox from '../../components/TextBox.module.css';
import { confirmLeave, disarmUnloadGuard, rearmUnloadGuard } from '../../routes/leaveGuard.ts';
import { routes } from '../../routes/routes.tsx';
import { guidePromptsQuery } from './guidePromptsQuery.ts';

const mePath = '/api/auth/me';
const promptsPath = '/api/admin/guide-prompts';
/** The key under which `serve` takes the answer to `PUT /api/admin/guide-prompts`, as the `GET` has the path alone. */
const savePath = `PUT ${promptsPath}`;
const antiforgeryPath = '/api/auth/antiforgery';
const address = '/manage/guide-prompts';

// A made-up manager, signed in, and worker (A35), in the JSON that MeEndpointTests asserts.
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

// The design's sketch of the prompts (guide-prompts.md), with a blank line inside them, which is kept too.
const saved = 'Mood and wellbeing today?\nWhat did you do together?\n\nAnything to follow up?';
// The organisation's row version, as the API writes it in the ETag, in quotes.
const etag = 'AAAAAAAAB9E=';

// The row's version once a save has moved it on, as another manager's save does too.
const nextVersion = 'AAAAAAAAB9I=';
// 06:12 UTC on 1 October 2026, as an answer's Date says it: 4:12 pm in Melbourne, before daylight saving starts.
const savedAt = 'Thu, 01 Oct 2026 06:12:00 GMT';

const help = 'Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.';
const limitHint = 'You can enter up to 1,000 characters';
const tooLong = 'Guide prompts must be 1,000 characters or less';
const conflict =
  'Another manager saved the guide prompts while you were editing. Your text is still here. Select Save to replace '
  + 'their version with yours.';
const title = 'Grow2Notes – Guide prompts';
// The leave dialog's words (guide-prompts.md component 7).
const leaveTitle = 'Leave without saving?';
const leaveBody = 'Your changes to the guide prompts are not saved.';
const stay = 'Stay on this page';
const leave = 'Leave without saving';

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** The `GET`'s `200`: the prompts, with the row's version in the `ETag`. */
function prompts(text: string, version = etag): Answer {
  return () =>
    Promise.resolve(
      new Response(JSON.stringify({ text }), {
        headers: { 'Content-Type': 'application/json', ETag: `"${version}"` },
      }),
    );
}

/** An answer that comes `ms` after the request, on the fake clock, unless the request is aborted first. */
function after(ms: number, answer: Answer): Answer {
  return (init) =>
    new Promise((resolve, reject) => {
      setTimeout(resolve, ms);
      const signal = init?.signal;
      signal?.addEventListener('abort', () => {
        reject(signal.reason as Error);
      });
    }).then(() => answer(init));
}

/**
 * The `PUT`'s `200`, with no body: the row's new version in the `ETag`, and the server's time in the `Date`, unless
 * `date` is `null`.
 */
function stored(version = nextVersion, date: string | null = savedAt): Answer {
  const headers = new Headers({ ETag: `"${version}"` });
  if (date !== null) {
    headers.set('Date', date);
  }

  return () => Promise.resolve(new Response(null, { status: 200, headers }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code` and `errors` if it has them. */
function problem(status: number, details: { code?: string; errors?: Record<string, string[]> } = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...details });
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

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, Answer>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: Answer) {
  answers.set(path, answer);
}

/** How many requests have been sent, for `path` or, with none, in all. */
function sent(path?: string): number {
  return fetchMock.mock.calls.filter(([sentPath]) => path === undefined || sentPath === path).length;
}

/** Each `PUT` of the prompts sent, as the `text` of its JSON body and its `If-Match`. */
function putsSent(): { text: unknown; ifMatch: string | null }[] {
  return fetchMock.mock.calls
    .filter(([path, init]) => path === promptsPath && init?.method === 'PUT')
    .map(([, init]) => ({
      text: (JSON.parse(init?.body as string) as { text: unknown }).text,
      ifMatch: new Headers(init?.headers).get('If-Match'),
    }));
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own and StrictMode, as `main.tsx`
 * renders it, after the addresses in `before`, which Back goes to. `['me']` holds `me` already, as after the start-up,
 * so that the screen shows with the first render, from which the fake clock then times its region.
 */
function renderApp(path: string, me: Me = manager, before: string[] = []) {
  serve(mePath, json(me));
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  const router = createMemoryRouter(routes, { initialEntries: [...before, path] });
  render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return { router, client };
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

/** The field, named by the `<h1>`'s label. */
function field(): HTMLTextAreaElement {
  return within(main()).getByRole<HTMLTextAreaElement>('textbox', { name: 'Guide prompts' });
}

/**
 * The region's line: the status region in `<main>` that shows, beside the page's visually hidden one, outside the form,
 * which has the save status.
 */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden') && region.closest('form') === null);
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The form round the field. */
function form(): HTMLFormElement {
  const found = field().closest('form');
  if (found === null) {
    throw new Error('The field is not in a form.');
  }

  return found;
}

/** Save, by its name, or by its busy label once it has been busy for 400 ms. */
function saveButton(): HTMLElement {
  return within(form()).getByRole('button', { name: /^(?:Save|Saving…)$/ });
}

/** Focuses Save and presses it, as a keyboard user would, and returns it. */
function pressSave(): HTMLElement {
  const button = saveButton();
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Puts `value` in the field as one input, as typing, pasting or deleting gives it. */
function enter(value: string) {
  fireEvent.input(field(), { target: { value } });
}

/** The save status beside Save. */
function saveStatus(): HTMLElement {
  return within(form()).getByRole('status');
}

/** The alert above Save, for a request that failed. */
function alert(): HTMLElement {
  return within(form()).getByRole('alert');
}

/** The count under the box, which shows from 900 characters, or `null`. */
function shownCount(): HTMLElement | null {
  return form().querySelector('p[aria-hidden="true"]');
}

/** The count's visually hidden twin, which screen readers hear. */
function countTwin(): HTMLElement {
  const twin = form().querySelector<HTMLElement>('[aria-live="polite"]');
  if (twin === null) {
    throw new Error('The form has no polite twin of the count.');
  }

  return twin;
}

/** Whether the page asks before it goes: whether it cancels a `beforeunload`, as a page with text not saved does. */
function warnsBeforeUnload(): boolean {
  const event = new Event('beforeunload', { cancelable: true });
  window.dispatchEvent(event);
  return event.defaultPrevented;
}

/**
 * `count` characters of text, in lines of 90 with a line break after each, which count as one character each, and
 * never one at the end, which Save would trim: 1,000 characters hold 10 line breaks.
 */
function textOf(count: number): string {
  return Array.from({ length: count }, (_, index) => (index % 91 === 90 && index < count - 1 ? '\n' : 'x')).join('');
}

/** Renders the page with `text` saved, and waits for the field. */
async function openWith(text = saved) {
  serve(promptsPath, prompts(text));
  const rendered = renderApp(address);
  await settle();
  return rendered;
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** Expects neither the field nor any button but Try again, such as Save, in `<main>`. */
function expectNoForm() {
  expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
  expect(within(main()).queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain(): HTMLElement {
  const button = within(main()).getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** The leave dialog, or `null` while it is closed. */
function leaveDialog(): HTMLDialogElement | null {
  return screen.queryByRole<HTMLDialogElement>('alertdialog');
}

/** The leave dialog, which must be open. */
function openDialog(): HTMLDialogElement {
  const dialog = leaveDialog();
  if (dialog === null) {
    throw new Error('The leave dialog is not open.');
  }

  return dialog;
}

/** Presses the main navigation's link named `name`, and lets the router take or block it. */
async function follow(name: string) {
  fireEvent.click(within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name }));
  await settle();
}

/** Presses the leave dialog's button named `name`, and lets the `close` event that its stand-in queues run. */
async function pressInDialog(name: string) {
  fireEvent.click(within(openDialog()).getByRole('button', { name }));
  await settle();
}

/**
 * The ways to close the leave dialog but Leave without saving, each of which means Stay (guide-prompts.md component
 * 7): its Stay on this page; Escape or Android's back gesture, for which the browser fires `cancel`, then closes it;
 * and a plain `close()`, with which the shell closes an open dialog before its warning.
 */
const staying: [string, (dialog: HTMLDialogElement) => void][] = [
  [stay, (dialog) => fireEvent.click(within(dialog).getByRole('button', { name: stay }))],
  [
    'Escape',
    (dialog) => {
      fireEvent(dialog, new Event('cancel', { cancelable: true }));
      dialog.close();
    },
  ],
  ['a plain close()', (dialog) => dialog.close()],
];

/** Asks the page whether the person may leave, as Sign out does first. */
function askToLeave(): Promise<boolean> {
  const asked: Promise<boolean>[] = [];
  act(() => {
    asked.push(confirmLeave());
  });
  return asked[0];
}

/** How many `beforeunload` listeners the app has on the window, as it adds and removes them from now on. */
function watchBeforeUnload(): () => number {
  const added = vi.spyOn(window, 'addEventListener');
  const removed = vi.spyOn(window, 'removeEventListener');
  const count = (calls: unknown[][]) => calls.filter(([type]) => type === 'beforeunload').length;
  return () => count(added.mock.calls) - count(removed.mock.calls);
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string; a `PUT` is answered by its own key.
  fetchMock.mockImplementation((path, init) => {
    const key = typeof path === 'string' && init?.method === 'PUT' ? `PUT ${path}` : path;
    const answer = typeof key === 'string' ? answers.get(key) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // The token that each change carries, which `api()` asks for before its first one.
  serve(antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up-token' } })),
  );
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
  // As a Sign out that failed leaves it, so that a test that disarmed it leaves no other test disarmed.
  rearmUnloadGuard();
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('Guide prompts', () => {
  it('shows its <h1>, which labels the field, then the help text and the limit hint, with Manage marked', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();

    const heading = pageHeading();
    expect(heading).toHaveAccessibleName('Guide prompts');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    const label = heading.querySelector('label');
    expect(label).toHaveTextContent(/^Guide prompts$/);
    expect(label).toHaveAttribute('for', field().id);
    const [helpText, hint] = [within(main()).getByText(help), within(main()).getByText(limitHint)];
    expect(helpText.localName).toBe('p');
    expect(hint.localName).toBe('p');
    // In the order they show: the <h1>, the help text, the limit hint, then the field (guide-prompts.md, Components).
    expect(heading.compareDocumentPosition(helpText)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(helpText.compareDocumentPosition(hint)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(hint.compareDocumentPosition(field())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(document.title).toBe('Grow2Notes – Guide prompts');
    expect(currentItems()).toEqual(['Manage true']);
  });

  it('holds the saved text exactly, line breaks kept, in the shared text box, in the 40rem column', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();

    expect(field()).toHaveValue(saved);
    expect(field()).toHaveClass(textBox.box);
    const grow = field().parentElement;
    expect(grow).toHaveClass(textBox.grow);
    expect(grow).toHaveAttribute('data-value', saved);
    expect(pageHeading().closest(`.${textBox.column}`)).toContainElement(field());
  });

  it('names the field by the <h1> and describes it by the help text and the limit hint', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();

    expect(field()).toHaveAccessibleName('Guide prompts');
    expect(field()).not.toHaveAttribute('aria-label');
    expect(field()).toHaveAccessibleDescription(`${help} ${limitHint}`);
    expect(field()).not.toHaveAttribute('aria-invalid');
    expect(field()).not.toHaveAttribute('aria-required');
  });

  it('gives the field its input aids and none of the attributes that it must never have', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();

    expect(field()).toHaveAttribute('name', 'text');
    expect(field()).toHaveAttribute('spellcheck', 'true');
    expect(field()).toHaveAttribute('autocapitalize', 'sentences');
    expect(field()).toHaveAttribute('autocomplete', 'off');
    expect(field()).toHaveAttribute('writingsuggestions', 'false');
    for (const attribute of ['placeholder', 'maxlength', 'disabled', 'readonly', 'autofocus']) {
      expect(field()).not.toHaveAttribute(attribute);
    }
    // No form action, which would empty the box after a save in React 19 (guide-prompts.md component 6).
    const form = field().closest('form');
    expect(form).toHaveAttribute('novalidate');
    expect(form).not.toHaveAttribute('action');
  });

  it('never focuses the field by itself', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();
    await wait(60_000);

    expect(field()).not.toHaveFocus();
    expect(document.body).toHaveFocus();
  });

  it('shows an empty saved value as an empty box, with no placeholder', async () => {
    serve(promptsPath, prompts(''));
    renderApp(address);
    await settle();

    expect(field()).toHaveValue('');
    expect(field()).not.toHaveAttribute('placeholder');
    expect(field().parentElement).toHaveAttribute('data-value', '');
  });

  it("gives the query the text and the ETag's token, without its quotes", async () => {
    serve(promptsPath, prompts(saved));
    const { client } = renderApp(address);
    await settle();

    expect(client.getQueryData(guidePromptsQuery.queryKey)).toEqual({ text: saved, etag });
  });

  it('sends nothing while typing, pasting and deleting, and keeps the copy behind the box in step', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();
    const before = sent();
    const typed = `${saved}\nHow`;
    const pasted = `${typed} did the day go?\nAny visitors?`;

    fireEvent.input(field(), { target: { value: typed } });
    fireEvent.paste(field());
    fireEvent.input(field(), { target: { value: pasted } });
    expect(field().parentElement).toHaveAttribute('data-value', pasted);
    fireEvent.input(field(), { target: { value: saved } });
    fireEvent.input(field(), { target: { value: '' } });
    await wait(60_000);

    expect(sent()).toBe(before);
    expect(field()).toHaveValue('');
    expect(field().parentElement).toHaveAttribute('data-value', '');
  });

  it('keeps what the field holds when the query is fetched again', async () => {
    serve(promptsPath, prompts(saved));
    const { client } = renderApp(address);
    await settle();
    const before = sent(promptsPath);
    // Another manager has saved in the meantime.
    serve(promptsPath, prompts('What went well today?', 'AAAAAAAAB9I='));

    await act(() => client.refetchQueries({ queryKey: guidePromptsQuery.queryKey }));
    await settle();

    expect(sent(promptsPath)).toBe(before + 1);
    expect(client.getQueryData(guidePromptsQuery.queryKey)).toEqual({
      text: 'What went well today?',
      etag: 'AAAAAAAAB9I=',
    });
    expect(field()).toHaveValue(saved);
    expect(field().parentElement).toHaveAttribute('data-value', saved);
  });

  it('is not fetched again when the window comes back into view', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address);
    await settle();
    const before = sent(promptsPath);

    act(() => {
      document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
    });
    await wait(60_000);

    expect(sent(promptsPath)).toBe(before);
  });
});

describe('Guide prompts while the prompts load', () => {
  it('shows its <h1>, help and limit hint at once, then "Loading guide prompts…" at 1 s, and no field', async () => {
    serve(promptsPath, noAnswer);
    renderApp(address);

    expect(pageHeading()).toHaveAccessibleName('Guide prompts');
    expect(within(main()).getByText(help)).toBeInTheDocument();
    expect(within(main()).getByText(limitHint)).toBeInTheDocument();

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    expectNoForm();
    expect(main().textContent).toBe(`Guide prompts${help}${limitHint}`);

    await wait(1);
    expect(line()).toHaveTextContent(/^Loading guide prompts…$/);
    expectNoForm();
  });

  it('mounts the field only with the answer, after 3 s, with no field or Save before it', async () => {
    serve(promptsPath, after(3000, prompts(saved)));
    renderApp(address);

    for (const ms of [0, 999, 1, 1999]) {
      await wait(ms);
      expectNoForm();
    }
    await wait(1);
    await settle();

    expect(field()).toHaveValue(saved);
    expect(line()).toBeEmptyDOMElement();
    expect(field()).not.toHaveFocus();
  });

  it('says the prompts did not load, with Try again, once its silent retry has found no connection too', async () => {
    serve(promptsPath, noConnection);
    renderApp(address);

    await wait(999);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    await wait(1);
    await settle();

    expect(line().textContent).toBe('The guide prompts did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expectNoForm();
    expect(pageHeading()).toHaveAccessibleName('Guide prompts');
  });

  it('says the prompts still did not load after a Try again that fails, with focus still on Try again', async () => {
    serve(promptsPath, noConnection);
    renderApp(address);
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The guide prompts still did not load: no connection. Try again.');
    expect(button).toHaveFocus();
    expectNoForm();
  });

  it('mounts the field after a Try again that loads, with focus on the <h1>, not the field', async () => {
    serve(promptsPath, noConnection);
    renderApp(address);
    await wait(1000);
    await settle();
    serve(promptsPath, prompts(saved));

    pressTryAgain();
    await settle();

    expect(field()).toHaveValue(saved);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });

  it('says something went wrong for a 500, after its silent retry', async () => {
    serve(promptsPath, problem(500));
    renderApp(address);

    await wait(1000);
    await settle();

    expect(line().textContent).toBe('The guide prompts did not load: something went wrong. Try again.');
    expectNoForm();
  });

  it('shows Page not found in place of the screen for a 403, at the same address', async () => {
    serve(promptsPath, problem(403));
    const { router } = renderApp(address);

    await settle();

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(within(main()).queryByText(help)).not.toBeInTheDocument();
    expectNoForm();
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(router.state.location.pathname).toBe(address);
  });
});

describe('Guide prompts on a second visit', () => {
  it("sends a fresh GET and mounts the field from its answer alone, never from the first visit's", async () => {
    serve(promptsPath, prompts(saved));
    const { router } = renderApp(address);
    await settle();
    await act(() => router.navigate('/manage'));
    await settle();
    const before = sent(promptsPath);
    // Another manager has saved since, and the answer to the next request takes 2 s.
    const newer = 'What went well today?';
    serve(promptsPath, after(2000, prompts(newer, 'AAAAAAAAB9I=')));

    await act(() => router.navigate(address));

    expect(sent(promptsPath)).toBeGreaterThan(before);
    await wait(1999);
    expectNoForm();
    await wait(1);
    await settle();
    expect(field()).toHaveValue(newer);
  });
});

describe('Guide prompts for a worker', () => {
  it('shows Page not found, and sends no request for the prompts', async () => {
    serve(promptsPath, prompts(saved));
    renderApp(address, worker);

    await wait(60_000);

    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');
    expect(currentItems()).toEqual([]);
    expectNoForm();
    expect(sent(promptsPath)).toBe(0);
  });
});

describe('Guide prompts, counting', () => {
  it('shows no count below 900 characters of trimmed text, with spaces and lines at the ends not counted', async () => {
    await openWith();

    enter(textOf(899));
    expect(shownCount()).toBeNull();
    enter(`  \n${textOf(899)}\n\n  `);
    expect(shownCount()).toBeNull();
    enter(`  \n${textOf(900)}\n\n  `);
    expect(shownCount()?.textContent).toBe('You have 100 characters remaining');
  });

  it.each([
    [900, 'You have 100 characters remaining'],
    [999, 'You have 1 character remaining'],
    [1_000, 'You have 0 characters remaining'],
  ])('at %i characters, says "%s" under the box, hidden from screen readers', async (length, words) => {
    await openWith();

    enter(textOf(length));

    const count = shownCount();
    expect(count?.textContent).toBe(words);
    expect(count).toHaveAttribute('aria-hidden', 'true');
    expect(count).not.toHaveAttribute('data-over-limit');
    expect(field()).not.toHaveAttribute('data-over-limit');
    expect(field().compareDocumentPosition(count!)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it('at 1,001 characters, says "You have 1 character too many", the box marked over but not invalid', async () => {
    await openWith();

    enter(textOf(1_001));
    await wait(60_000);

    expect(shownCount()?.textContent).toBe('You have 1 character too many');
    expect(shownCount()).toHaveAttribute('data-over-limit');
    expect(field()).toHaveAttribute('data-over-limit');
    expect(field()).not.toHaveAttribute('aria-invalid');
    expect(field()).toHaveAccessibleDescription(`${help} ${limitHint}`);
    expect(within(form()).queryByText(tooLong)).not.toBeInTheDocument();
  });

  it('says the count to screen readers once typing has stopped for 1 s, never at each keystroke', async () => {
    await openWith();
    // In the form from its first render, empty, so that a screen reader hears what is written into it.
    expect(countTwin()).toBeEmptyDOMElement();
    expect(countTwin()).toHaveClass('visually-hidden');
    expect(countTwin()).not.toHaveAttribute('aria-hidden');

    enter(textOf(900));
    await wait(999);
    expect(countTwin()).toBeEmptyDOMElement();
    // A keystroke within the second starts it again.
    enter(textOf(901));
    await wait(999);
    expect(countTwin()).toBeEmptyDOMElement();
    await wait(1);
    expect(countTwin().textContent).toBe('You have 99 characters remaining');

    enter(textOf(1_001));
    await wait(1_000);
    expect(countTwin().textContent).toBe('You have 1 character too many');

    // Below 900, it says nothing.
    enter(saved);
    await wait(1_000);
    expect(countTwin()).toBeEmptyDOMElement();
  });

  it('shows the count of loaded text from 900 characters at once, and says nothing until typing', async () => {
    await openWith(textOf(950));

    expect(shownCount()?.textContent).toBe('You have 50 characters remaining');
    await wait(60_000);
    expect(countTwin()).toBeEmptyDOMElement();
  });
});

describe('Guide prompts, too long', () => {
  it('keeps a 1,200-character paste whole, and Save shows the error, focuses the field and sends nothing', async () => {
    await openWith();
    const pasted = textOf(1_200);

    fireEvent.paste(field());
    enter(pasted);
    expect(field()).toHaveValue(pasted);
    expect(shownCount()?.textContent).toBe('You have 200 characters too many');
    const save = pressSave();
    await wait(60_000);

    expect(putsSent()).toEqual([]);
    expect(field()).toHaveValue(pasted);
    expect(field()).toHaveFocus();
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(field()).toHaveAttribute('aria-invalid', 'true');
    expect(field()).toHaveAccessibleDescription(`${help} ${limitHint} Error: ${tooLong}`);
    // Above the box, "Error:" hidden but read, in the group that has the bar (guide-prompts.md 6a).
    const message = within(form()).getByText(tooLong);
    expect(message.id).toBe('guidePrompts-error');
    expect(message.textContent).toBe(`Error: ${tooLong}`);
    expect(within(message).getByText('Error:')).toHaveClass('visually-hidden');
    expect(message.compareDocumentPosition(field())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    const group = form().querySelector('[data-invalid]');
    expect(group).toContainElement(message);
    expect(group).toContainElement(field());
    expect(group).toContainElement(shownCount());
    expect(document.title).toBe(`Error: ${title}`);
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus()).toBeEmptyDOMElement();

    // Still over by one, then down to 1,000, which clears it silently.
    enter(textOf(1_001));
    expect(field()).toHaveAttribute('aria-invalid', 'true');
    enter(textOf(1_000));
    expect(within(form()).queryByText(tooLong)).not.toBeInTheDocument();
    expect(field()).not.toHaveAttribute('aria-invalid');
    expect(field()).toHaveAccessibleDescription(`${help} ${limitHint}`);
    expect(form().querySelector('[data-invalid]')).toBeNull();
    expect(document.title).toBe(title);
    expect(field()).toHaveFocus();
  });

  it('moves focus to the field at each press while the text is too long', async () => {
    await openWith();
    enter(textOf(1_001));

    pressSave();
    expect(field()).toHaveFocus();
    // Back on Save, as Tab takes it, for a second press.
    pressSave();
    await settle();

    expect(field()).toHaveFocus();
    expect(within(form()).getAllByText(tooLong)).toHaveLength(1);
    expect(putsSent()).toEqual([]);
  });

  it('shows a 422 for the text as too long, with focus on the field', async () => {
    await openWith();
    serve(savePath, problem(422, { code: 'validation.failed', errors: { text: ['The text is too long.'] } }));

    enter(textOf(1_000));
    pressSave();
    await settle();

    expect(putsSent()).toHaveLength(1);
    expect(field()).toHaveAttribute('aria-invalid', 'true');
    expect(field()).toHaveAccessibleDescription(`${help} ${limitHint} Error: ${tooLong}`);
    expect(field()).toHaveFocus();
    expect(alert()).toBeEmptyDOMElement();
  });
});

describe('Guide prompts, saving', () => {
  it("sends the text trimmed at its ends alone, with If-Match from the GET's ETag, the field as typed", async () => {
    await openWith();
    serve(savePath, stored());
    const typed = '\n  Mood today?\n\n  What did you do together?  \n\n';

    enter(typed);
    const save = pressSave();
    await settle();

    expect(putsSent()).toEqual([{ text: 'Mood today?\n\n  What did you do together?', ifMatch: `"${etag}"` }]);
    expect(field()).toHaveValue(typed);
    expect(save).toHaveFocus();
  });

  it('sends "" for spaces and line breaks alone, which leaves the boxes with no prompts', async () => {
    await openWith();
    serve(savePath, stored());

    enter('  \n\n \t ');
    pressSave();
    await settle();

    expect(putsSent()).toEqual([{ text: '', ifMatch: `"${etag}"` }]);
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
  });

  it('saves 1,000 characters holding 10 line breaks, as the server counts them too', async () => {
    await openWith();
    serve(savePath, stored());
    const text = textOf(1_000);
    expect(text.match(/\n/g)).toHaveLength(10);

    enter(text);
    pressSave();
    await settle();

    expect(putsSent()).toEqual([{ text, ifMatch: `"${etag}"` }]);
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(field()).not.toHaveAttribute('aria-invalid');
  });

  it("says Saved and the time of the answer's Date in Melbourne, focus on Save, until the next keystroke", async () => {
    await openWith();
    serve(savePath, stored());

    enter('What went well today?');
    const save = pressSave();
    await settle();

    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(saveStatus().querySelector('time')).toHaveAttribute('datetime', '2026-10-01T06:12:00.000Z');
    expect(save).toHaveFocus();
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(alert()).toBeEmptyDOMElement();
    expect(document.title).toBe(title);

    enter('What went well today?!');
    expect(saveStatus()).toBeEmptyDOMElement();
  });

  it('says "Saved" alone when the answer has no Date, never the time on the device', async () => {
    await openWith();
    serve(savePath, stored(nextVersion, null));

    pressSave();
    await settle();

    expect(saveStatus().textContent).toBe('Saved');
    expect(saveStatus().querySelector('time')).toBeNull();
  });

  it('sends the next save with the ETag that the last 200 gave', async () => {
    await openWith();
    serve(savePath, stored());
    enter('What went well today?');
    pressSave();
    await settle();

    serve(savePath, stored('AAAAAAAAB9M='));
    enter('What went well today?\nAny visitors?');
    pressSave();
    await settle();

    expect(putsSent()).toEqual([
      { text: 'What went well today?', ifMatch: `"${etag}"` },
      { text: 'What went well today?\nAny visitors?', ifMatch: `"${nextVersion}"` },
    ]);
  });

  it('is never given disabled, with no change or over the limit, and two quick presses send one PUT', async () => {
    await openWith();
    expect(saveButton()).not.toHaveAttribute('disabled');
    expect(saveButton()).not.toHaveAttribute('aria-disabled');
    enter(textOf(1_001));
    expect(saveButton()).not.toHaveAttribute('disabled');
    expect(saveButton()).not.toHaveAttribute('aria-disabled');
    serve(savePath, after(2_000, stored()));
    enter('What went well today?');

    const save = pressSave();
    fireEvent.click(save);
    await settle();

    expect(save).toHaveAttribute('aria-disabled', 'true');
    expect(save).not.toHaveAttribute('disabled');
    await wait(2_000);
    expect(putsSent()).toHaveLength(1);
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
  });

  it('with the PUT held 2 s, Save and its status say "Saving…" from 400 ms, the field stays editable', async () => {
    await openWith();
    serve(savePath, after(2_000, stored()));
    const pageRegion = within(main())
      .getAllByRole('status')
      .find((region) => region.classList.contains('visually-hidden'));

    const save = pressSave();
    await wait(399);
    expect(save).toHaveAccessibleName('Save');
    expect(save).not.toHaveAttribute('data-busy');
    expect(saveStatus()).toBeEmptyDOMElement();
    await wait(1);
    expect(save).toHaveAccessibleName('Saving…');
    expect(save).toHaveAttribute('data-busy');
    expect(saveStatus().textContent).toBe('Saving…');
    expect(within(saveStatus()).getByText('Saving…')).toHaveClass('visually-hidden');
    // Said once, by the save status: Save does not write it to the page's region too (guide-prompts.md 6e).
    expect(pageRegion).toBeEmptyDOMElement();

    for (const attribute of ['disabled', 'readonly', 'aria-disabled', 'aria-readonly']) {
      expect(field()).not.toHaveAttribute(attribute);
    }
    enter(`${saved}\nAny visitors?`);
    expect(field()).toHaveValue(`${saved}\nAny visitors?`);
    await wait(1_600);

    expect(save).toHaveAccessibleName('Save');
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(putsSent()).toEqual([{ text: saved, ifMatch: `"${etag}"` }]);
  });

  it('empties the status at each press, so that two saves in a row are both announced', async () => {
    await openWith();
    serve(savePath, stored());
    pressSave();
    await settle();
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');

    serve(savePath, after(1_000, stored('AAAAAAAAB9M=')));
    pressSave();
    expect(saveStatus()).toBeEmptyDOMElement();
    await wait(1_000);

    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(putsSent()).toHaveLength(2);
  });

  it('leaves the page not saved when the manager types while a save is out, and saved when they do not', async () => {
    await openWith();
    expect(warnsBeforeUnload()).toBe(false);
    enter('What went well today?');
    expect(warnsBeforeUnload()).toBe(true);
    serve(savePath, after(2_000, stored()));

    pressSave();
    await wait(1_000);
    enter('What went well today?\nAny visitors?');
    await wait(1_000);

    // The press's save is done, and says so (settings-textarea.md *Behaviour* 6), but what was typed since is not.
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(warnsBeforeUnload()).toBe(true);

    serve(savePath, stored('AAAAAAAAB9M='));
    pressSave();
    await settle();
    expect(putsSent().at(-1)).toEqual({ text: 'What went well today?\nAny visitors?', ifMatch: `"${nextVersion}"` });
    expect(warnsBeforeUnload()).toBe(false);
  });

  it.each([
    ['no connection', noConnection, 'Not saved: no connection. Try again.'],
    ['a 500', problem(500), 'Not saved: something went wrong. Try again.'],
    ['a 428', problem(428, { code: 'precondition.required' }), 'Not saved: something went wrong. Try again.'],
  ])('after %s, says so above Save, keeps the text and focus, and sends one PUT a press', async (_, answer, words) => {
    await openWith();
    serve(savePath, answer);
    const typed = 'What went well today?';

    enter(typed);
    const save = pressSave();
    await settle();

    expect(alert().textContent).toBe(words);
    expect(alert().compareDocumentPosition(save)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(field()).toHaveValue(typed);
    expect(save).toHaveFocus();
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(document.title).toBe(`Error: ${title}`);
    expect(warnsBeforeUnload()).toBe(true);
    await wait(60_000);
    expect(putsSent()).toHaveLength(1);

    pressSave();
    // Emptied at the press, so that the same words, written again, are announced again.
    expect(alert()).toBeEmptyDOMElement();
    await settle();
    expect(alert().textContent).toBe(words);
    expect(putsSent()).toHaveLength(2);
    expect(field()).toHaveValue(typed);
  });

  it("after a 412, keeps the text, makes the other manager's text the base, and saves next with its ETag", async () => {
    await openWith();
    const theirs = 'What went well today?';
    const mine = `${saved}\nAny visitors?`;
    serve(savePath, problem(412, { code: 'precondition.failed' }));
    serve(promptsPath, after(1_000, prompts(theirs, nextVersion)));

    enter(mine);
    const save = pressSave();
    await settle();
    // Busy until the refetch has answered, so that the next press sends the new ETag.
    expect(save).toHaveAttribute('aria-disabled', 'true');
    expect(alert()).toBeEmptyDOMElement();
    await wait(1_000);

    expect(alert().textContent).toBe(conflict);
    expect(field()).toHaveValue(mine);
    expect(save).toHaveFocus();
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(warnsBeforeUnload()).toBe(true);

    serve(savePath, stored('AAAAAAAAB9M='));
    pressSave();
    await settle();

    expect(putsSent()).toEqual([
      { text: mine, ifMatch: `"${etag}"` },
      { text: mine, ifMatch: `"${nextVersion}"` },
    ]);
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus().textContent).toBe('Saved 4:12\u00A0pm');
    expect(warnsBeforeUnload()).toBe(false);
  });

  it('refreshes /me after a 403, so that the role guard shows Page not found once it says worker', async () => {
    const { router } = await openWith();
    serve(savePath, problem(403));
    serve(mePath, json(worker));
    const before = sent(mePath);

    pressSave();
    await settle();
    await settle();

    expect(sent(mePath)).toBe(before + 1);
    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(router.state.location.pathname).toBe(address);
    expect(putsSent()).toHaveLength(1);
  });

  it('shows nothing for a 401, which ends the session, for the shell to sign the person in again', async () => {
    await openWith();
    const ended = vi.fn();
    const stopListening = onSessionEnded(ended);
    serve(savePath, problem(401));
    const typed = 'What went well today?';

    enter(typed);
    const save = pressSave();
    await settle();
    stopListening();

    expect(ended).toHaveBeenCalledOnce();
    expect(alert()).toBeEmptyDOMElement();
    expect(saveStatus()).toBeEmptyDOMElement();
    expect(field()).not.toHaveAttribute('aria-invalid');
    expect(field()).toHaveValue(typed);
    expect(save).not.toHaveAttribute('aria-disabled');
    expect(document.title).toBe(title);
    expect(putsSent()).toHaveLength(1);
  });
});

describe('Guide prompts, leaving', () => {
  const typed = `${saved}\nAny visitors?`;

  it('asks before a link to another page while the text is not saved, with focus on Stay on this page', async () => {
    const { router } = await openWith();
    enter(typed);

    await follow('Today');

    const dialog = openDialog();
    expect(dialog).toHaveAccessibleName(leaveTitle);
    expect(dialog).toHaveAccessibleDescription(leaveBody);
    const [stayButton, leaveButton] = within(dialog).getAllByRole('button');
    expect(stayButton).toHaveTextContent(stay);
    expect(stayButton).toHaveFocus();
    expect(stayButton.className).toContain(buttonClass('primary'));
    // Not red: a few lines of prompts are easy to type again (guide-prompts.md component 7).
    expect(leaveButton).toHaveTextContent(leave);
    expect(leaveButton.className).toContain(buttonClass('secondary'));
    expect(router.state.location.pathname).toBe(address);
    expect(field()).toHaveValue(typed);
  });

  it.each(staying)('stays as it was after %s, and asks again at the next link', async (_, close) => {
    const { router } = await openWith();
    enter(typed);
    await follow('Today');

    close(openDialog());
    await settle();

    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe(address);
    expect(field()).toHaveValue(typed);
    // The navigation's blocker was reset, not left blocked.
    await follow('Manage');
    expect(openDialog()).toHaveAccessibleName(leaveTitle);
    expect(router.state.location.pathname).toBe(address);
  });

  it('goes to the page on Leave without saving, and then asks nothing before Sign out or a reload', async () => {
    const listeners = watchBeforeUnload();
    const { router } = await openWith();
    enter(typed);
    expect(listeners()).toBe(1);
    await follow('Today');

    await pressInDialog(leave);

    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
    expect(pageHeading()).toHaveTextContent(/^Today/);
    expect(listeners()).toBe(0);
    await expect(askToLeave()).resolves.toBe(true);
    expect(leaveDialog()).not.toBeInTheDocument();
  });

  it.each([
    ['as loaded', []],
    ['typed, then deleted back', [typed, saved]],
    ['changed at its ends alone, which Save trims', [`\n${saved}  \n`]],
  ])('asks nothing for a link while the text is %s', async (_, inputs: string[]) => {
    const { router } = await openWith();
    for (const input of inputs) {
      enter(input);
    }

    await follow('Today');

    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it('asks nothing for a change of address that keeps the page, such as its query', async () => {
    const { router } = await openWith();
    enter(typed);

    await act(() => router.navigate(`${address}?from=manage`));
    await settle();

    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.search).toBe('?from=manage');
    expect(field()).toHaveValue(typed);
  });

  it('asks before Back within the app, which Stay on this page undoes and Leave without saving takes', async () => {
    serve(promptsPath, prompts(saved));
    const { router } = renderApp(address, manager, ['/manage']);
    await settle();
    enter(typed);

    await act(() => router.navigate(-1));
    await settle();
    expect(openDialog()).toHaveAccessibleName(leaveTitle);
    expect(within(openDialog()).getByRole('button', { name: stay })).toHaveFocus();
    expect(router.state.location.pathname).toBe(address);
    await pressInDialog(stay);
    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe(address);
    expect(field()).toHaveValue(typed);

    await act(() => router.navigate(-1));
    await settle();
    await pressInDialog(leave);

    expect(router.state.location.pathname).toBe('/manage');
    expect(pageHeading()).toHaveAccessibleName('Manage');
  });

  it.each(staying)('asks Sign out the same dialog while the text is not saved, staying after %s', async (_, close) => {
    await openWith();
    enter(typed);

    const asked = askToLeave();
    expect(openDialog()).toHaveAccessibleName(leaveTitle);
    expect(within(openDialog()).getByRole('button', { name: stay })).toHaveFocus();
    close(openDialog());
    await settle();

    await expect(asked).resolves.toBe(false);
    expect(leaveDialog()).not.toBeInTheDocument();
    expect(field()).toHaveValue(typed);
  });

  it('lets Sign out go on after Leave without saving', async () => {
    await openWith();
    enter(typed);

    const asked = askToLeave();
    await pressInDialog(leave);

    await expect(asked).resolves.toBe(true);
    expect(leaveDialog()).not.toBeInTheDocument();
  });

  it('lets Sign out go on at once, with no dialog, while the text is as saved, typed and deleted back', async () => {
    await openWith();
    enter(typed);
    enter(saved);

    const asked = askToLeave();

    expect(leaveDialog()).not.toBeInTheDocument();
    await expect(asked).resolves.toBe(true);
  });

  it('has a beforeunload listener only while the text is not saved, asking unless the shell disarms it', async () => {
    const listeners = watchBeforeUnload();
    await openWith();
    expect(listeners()).toBe(0);
    expect(warnsBeforeUnload()).toBe(false);

    enter(typed);
    expect(listeners()).toBe(1);
    expect(warnsBeforeUnload()).toBe(true);
    // As Sign out leaves it before its full page load, and then a logout that failed.
    disarmUnloadGuard();
    expect(warnsBeforeUnload()).toBe(false);
    rearmUnloadGuard();
    expect(warnsBeforeUnload()).toBe(true);

    enter(saved);
    expect(listeners()).toBe(0);

    enter(typed);
    serve(savePath, stored());
    pressSave();
    await settle();
    expect(saveStatus().textContent).toBe('Saved 4:12 pm');
    expect(listeners()).toBe(0);
    expect(warnsBeforeUnload()).toBe(false);
  });
});

describe('Today, Manage and Users', () => {
  // S01.04.02's own criterion: design.md asks for a leave warning on Guide prompts alone.
  it.each([
    ['/', 'Manage', '/manage'],
    ['/manage', 'Today', '/'],
    ['/manage/users', 'Today', '/'],
  ])('at %s, ask nothing before Sign out, a reload or a link', async (path, link, next) => {
    serve('/api/admin/users', noAnswer);
    const listeners = watchBeforeUnload();
    const { router } = renderApp(path);
    await settle();

    await expect(askToLeave()).resolves.toBe(true);
    expect(listeners()).toBe(0);
    await follow(link);

    expect(leaveDialog()).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe(next);
  });
});
