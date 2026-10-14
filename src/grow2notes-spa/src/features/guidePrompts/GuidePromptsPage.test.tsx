import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import textBox from '../../components/TextBox.module.css';
import { routes } from '../../routes/routes.tsx';
import { guidePromptsQuery } from './guidePromptsQuery.ts';

const mePath = '/api/auth/me';
const promptsPath = '/api/admin/guide-prompts';
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

const help = 'Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.';
const limitHint = 'You can enter up to 1,000 characters';

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

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
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

/** The region's line: the status region in `<main>` that shows, beside the page's visually hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
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
