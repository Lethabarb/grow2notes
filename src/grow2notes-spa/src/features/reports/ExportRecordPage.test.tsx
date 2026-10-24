import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes } from '../../routes/routes.tsx';
import type { ParticipantHeader } from '../notes/noteQueries.ts';

const mePath = '/api/auth/me';
const today = '2026-10-01';
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today,
};
const worker: Me = {
  ...manager,
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
};

const jane: ParticipantHeader = {
  id: '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95',
  givenName: 'Jane',
  familyName: 'Citizen',
  status: 'Active',
  firstNoteDate: '2026-01-01',
};

const headerPath = `/api/participants/${jane.id}`;
const exportAddress = `/manage/participants/${jane.id}/export`;
const exportBase = `/api/reports/participants/${jane.id}/export?`;
const bytes = new Uint8Array([0x25, 0x50, 0x44, 0x46]);

// Between a day and its month (microcopy.md §3).
const nbsp = String.fromCodePoint(0xa0);
const noNotes = `No submitted notes for Jane Citizen between 1${nbsp}January 2026 and 1${nbsp}October 2026.`;
const wentWrong = 'Not exported: something went wrong. Try again.';
const noConnectionWords = 'Not exported: no connection. Try again.';
const tooMany = 'Not exported: too many exports in the last minute. Wait a minute, then try again.';
const savedAs = (format: string, from = '2026-01-01', to = today) => `participant-record_${from}_to_${to}.${format}`;

/** The path of the export request for a form, in the order the page writes its query. */
function exportPath(format: string, { from = '2026-01-01', to = today, history = false } = {}) {
  return `${exportBase}from=${from}&to=${to}&format=${format}&includeHistory=${String(history)}`;
}

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

const fetchMock = vi.fn<typeof fetch>();
const createObjectURL = vi.fn<(blob: Blob) => string>();
const revokeObjectURL = vi.fn<(url: string) => void>();
const scrollIntoView = vi.fn<(this: Element, options?: boolean | ScrollIntoViewOptions) => void>();
/** The `download` name of each link clicked. */
let saved: string[] = [];
/** The header the server gives, and what it answers to an export in place of the file, which a test sets. */
let header: Answer;
let exportAnswer: Answer | undefined;
/** Who the server says is signed in. */
let currentMe = manager;

function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure, with a code and a `422`'s field errors when it has them. */
function problem(status: number, extra: { code?: string; errors?: Record<string, string[]> } = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extra });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts. */
const noAnswer: Answer = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

/** An answer that waits for `release()`, then gives `answer`'s. */
function held(answer: Answer): { answer: Answer; release: () => void } {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: (init) => gate.then(() => answer(init)), release };
}

/** An answer that comes after `ms`, as a long export does. */
function after(ms: number, answer: Answer): Answer {
  return (init) => new Promise<Response>((resolve) => setTimeout(() => resolve(answer(init)), ms));
}

/** The file, named as the server names it from the query. */
const fileFor: Answer = () => Promise.resolve(new Response(bytes, { status: 200 }));

function serveRequest(input: string, init?: RequestInit): Promise<Response> {
  if (input === mePath) {
    return json(currentMe)();
  }

  if (input === headerPath) {
    return header(init);
  }

  if (input.startsWith(exportBase)) {
    const query = new URLSearchParams(input.slice(exportBase.length));
    const name = savedAs(query.get('format')!, query.get('from')!, query.get('to')!);
    const answer: Answer =
      exportAnswer ??
      (() =>
        Promise.resolve(
          new Response(bytes, { status: 200, headers: { 'Content-Disposition': `attachment; filename=${name}` } }),
        ));
    return answer(init);
  }

  return problem(404)();
}

function requests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string);
}

function exports(): string[] {
  return requests().filter((path) => path.startsWith(exportBase));
}

function meRequests(): number {
  return requests().filter((path) => path === mePath).length;
}

/** The abort signal of the one export request. */
function exportSignal(): AbortSignal | null | undefined {
  return fetchMock.mock.calls.find(([input]) => (input as string).startsWith(exportBase))?.[1]?.signal;
}

/**
 * Renders the app at `path` for `me` on a memory router, under StrictMode, which runs each effect twice, as `main.tsx`
 * does, and a query client in which `['me']` holds `me` already, as after the start-up.
 */
function renderApp(path: string, me: Me = manager) {
  currentMe = me;
  const client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  // With a key of its own, as a browser gives it, so that no position is carried from one test into the next.
  const router = createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
  const { unmount } = render(
    <StrictMode>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return { router, unmount };
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function advance(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

async function settle() {
  await advance(1);
}

async function openPage(me: Me = manager, participant: ParticipantHeader = jane) {
  header = json(participant);
  const opened = renderApp(exportAddress, me);
  await settle();
  return opened;
}

/** Arrives on the page by a move within the app, which is what gives the `<h1>` focus on arrival. */
async function arriveOnPage() {
  header = json(jane);
  const opened = renderApp('/no-such-page');
  await settle();
  await act(() => opened.router.navigate(exportAddress));
  await settle();
  return opened;
}

async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await settle();
}

function main(): HTMLElement {
  return screen.getByRole('main');
}

const pageHeading = () => within(main()).getByRole('heading', { level: 1 });
const fromField = () => within(main()).getByLabelText<HTMLInputElement>('From date');
const toField = () => within(main()).getByLabelText<HTMLInputElement>('To date');
const pdfRadio = () => within(main()).getByRole('radio', { name: 'PDF' });
const wordRadio = () => within(main()).getByRole('radio', { name: 'Word' });
const historyBox = () =>
  within(main()).getByRole<HTMLInputElement>('checkbox', { name: 'Include earlier versions of edited notes' });
const exportButton = () => within(main()).getByRole('button', { name: 'Export' });
const status = () => document.getElementById('export-status')!;
const alertBox = () => document.getElementById('export-alert')!;
const summaryHeading = () => screen.queryByRole('heading', { name: 'There is a problem' });
const summary = () => summaryHeading()?.parentElement ?? null;

function enter(field: HTMLInputElement, value: string) {
  fireEvent.change(field, { target: { value } });
}

/** The message above a field, or the Format group, without its hidden "Error: " prefix. */
function errorOf(id: string): string | undefined {
  return document.getElementById(`${id}-error`)?.textContent.replace(/^Error:\s*/, '');
}

/** Chooses `format` and presses Export. */
async function exportAs(format: 'pdf' | 'docx') {
  await press(format === 'pdf' ? pdfRadio() : wordRadio());
  await press(exportButton());
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockImplementation((input, init) => serveRequest(input as string, init));
  header = json(jane);
  exportAnswer = undefined;
  // jsdom has neither URL.createObjectURL nor a scrollIntoView, and none of its anchors navigates.
  Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true, writable: true });
  Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true, writable: true });
  Element.prototype.scrollIntoView = scrollIntoView;
  createObjectURL.mockReturnValue('blob:https://grow2notes.test/1');
  saved = [];
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
    saved.push(this.download);
  });
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  vi.restoreAllMocks();
  fetchMock.mockReset();
  createObjectURL.mockReset();
  revokeObjectURL.mockReset();
  scrollIntoView.mockReset();
  Reflect.deleteProperty(URL, 'createObjectURL');
  Reflect.deleteProperty(URL, 'revokeObjectURL');
  Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
  vi.unstubAllGlobals();
  vi.useRealTimers();
  localStorage.clear();
  sessionStorage.clear();
});

describe('Export record', () => {
  describe('arriving', () => {
    it('opens with From at the first submitted note, To at today, no format and the box unticked', async () => {
      await openPage();

      expect(fromField()).toHaveValue('2026-01-01');
      expect(toField()).toHaveValue(today);
      expect(pdfRadio()).not.toBeChecked();
      expect(wordRadio()).not.toBeChecked();
      expect(historyBox()).not.toBeChecked();
      expect(errorOf('export-from')).toBeUndefined();
      expect(summaryHeading()).toBeNull();
    });

    it('opens with From and To at today for a participant with no submitted note', async () => {
      await openPage(manager, { ...jane, firstNoteDate: null });

      expect(fromField()).toHaveValue(today);
      expect(toField()).toHaveValue(today);
    });

    it('takes its dates from the server, whatever the device clock says', async () => {
      vi.setSystemTime(new Date('2030-06-15T03:00:00Z'));
      await openPage(manager, { ...jane, firstNoteDate: null });

      expect(fromField()).toHaveValue(today);
      expect(toField()).toHaveValue(today);
      expect(fromField()).toHaveAttribute('max', today);
    });

    it('limits both dates to today, and To to a complete From', async () => {
      await openPage();

      expect(fromField()).toHaveAttribute('max', today);
      expect(fromField()).not.toHaveAttribute('min');
      expect(toField()).toHaveAttribute('max', today);
      expect(toField()).toHaveAttribute('min', '2026-01-01');

      enter(fromField(), '2026-03-05');
      expect(toField()).toHaveAttribute('min', '2026-03-05');

      enter(fromField(), '');
      expect(toField()).not.toHaveAttribute('min');
    });

    it('has date inputs, a Format group of two radios, a box and Export, in that order, in one form', async () => {
      await openPage();

      const form = fromField().closest('form')!;
      expect(form).toHaveAttribute('novalidate');
      expect(fromField()).toHaveAttribute('type', 'date');
      expect(fromField()).toHaveAttribute('name', 'from');
      expect(toField()).toHaveAttribute('type', 'date');
      expect(toField()).toHaveAttribute('name', 'to');
      expect(within(form).getByRole('group', { name: 'Format' })).toBeInTheDocument();
      expect(pdfRadio()).toHaveAttribute('id', 'export-format');
      expect(wordRadio()).toHaveAttribute('id', 'export-format-docx');
      expect(pdfRadio()).toHaveAccessibleDescription('For printing and filing');
      expect(wordRadio()).toHaveAccessibleDescription(
        'Can be edited, so use it if you need to remove anything before handing it over',
      );
      expect(historyBox()).toHaveAttribute('id', 'export-includeHistory');
      expect(historyBox()).toHaveAccessibleDescription(
        'Adds each earlier version in full, which can include corrected wording or another person’s information',
      );
      expect(exportButton()).toHaveAttribute('type', 'submit');
      const inOrder = [
        fromField(),
        toField(),
        pdfRadio(),
        wordRadio(),
        historyBox(),
        alertBox(),
        status(),
        exportButton(),
      ];
      for (let i = 1; i < inOrder.length; i += 1) {
        expect(inOrder[i - 1].compareDocumentPosition(inOrder[i]) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      }
    });

    it('shows the participant as a term and its name as stored, not a disabled field', async () => {
      await openPage();

      const term = within(main()).getByText('Participant');
      expect(term.tagName).toBe('DT');
      expect(term.nextElementSibling).toHaveTextContent('Jane Citizen');
      expect(term.nextElementSibling).toHaveAttribute('translate', 'no');
      expect(within(main()).queryByDisplayValue('Jane Citizen')).toBeNull();
    });

    it('has one <h1>, "Export record", with the title "Grow2Notes – Export record" and no name in it', async () => {
      await openPage();

      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(pageHeading()).toHaveAccessibleName('Export record');
      expect(document.title).toBe('Grow2Notes – Export record');
    });

    it('focuses the <h1> on arrival from another screen', async () => {
      await arriveOnPage();

      expect(pageHeading()).toHaveFocus();
    });

    it('has the alert and the status line, both empty, from the first render', async () => {
      await openPage();

      expect(alertBox()).toHaveAttribute('role', 'alert');
      expect(alertBox()).toBeEmptyDOMElement();
      expect(status()).toHaveAttribute('role', 'status');
      expect(status()).toBeEmptyDOMElement();
    });

    it('shows the defaults again on a new visit, and touches no browser storage', async () => {
      const setItem = vi.spyOn(Storage.prototype, 'setItem');
      const { unmount } = await openPage();
      enter(fromField(), '2026-05-05');
      await press(historyBox());
      await press(pdfRadio());
      unmount();

      await openPage();

      expect(fromField()).toHaveValue('2026-01-01');
      expect(pdfRadio()).not.toBeChecked();
      expect(historyBox()).not.toBeChecked();
      expect(setItem).not.toHaveBeenCalled();
      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
    });

    it('works the same for an archived participant', async () => {
      await openPage(manager, { ...jane, status: 'Archived' });

      expect(pageHeading()).toHaveAccessibleName('Export record');
      expect(fromField()).toHaveValue('2026-01-01');

      await exportAs('pdf');

      expect(exports()).toEqual([exportPath('pdf')]);
    });

    it('gives a worker Page not found, and sends nothing about the participant or an export', async () => {
      await openPage(worker);

      expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
      expect(requests()).not.toContain(headerPath);
      expect(exports()).toEqual([]);
    });

    it('shows Page not found for a participant not in the organisation', async () => {
      header = problem(404);
      renderApp(exportAddress);
      await settle();

      expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
    });

    it('says the form did not load, with Try again, when the participant did not', async () => {
      header = noConnection;
      renderApp(exportAddress);
      await advance(10_000);

      expect(main()).toHaveTextContent('The export form did not load: no connection. Try again.');
      expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    });
  });

  describe('checking the form', () => {
    it('says "Choose PDF or Word" under the legend and in the summary, and sends nothing', async () => {
      await openPage();
      await press(exportButton());

      expect(errorOf('export-format')).toBe('Choose PDF or Word');
      expect(within(summary()!).getByRole('link', { name: 'Choose PDF or Word' })).toHaveAttribute(
        'href',
        '#export-format',
      );
      expect(summary()).toHaveFocus();
      expect(document.title).toBe('Error: Grow2Notes – Export record');
      expect(exports()).toEqual([]);
      expect(within(main()).getByRole('group', { name: 'Format' })).toHaveAccessibleDescription(
        'Error: Choose PDF or Word',
      );
    });

    it('says the To date must not be before the From date, on To alone, with the From date in words', async () => {
      await openPage();
      enter(toField(), '2025-12-31');
      await press(pdfRadio());
      await press(exportButton());

      const words = `To date must be the same as or after 1${nbsp}January 2026`;
      expect(errorOf('export-to')).toBe(words);
      expect(errorOf('export-from')).toBeUndefined();
      expect(toField()).toHaveAttribute('aria-invalid', 'true');
      expect(toField()).toHaveAttribute('aria-describedby', 'export-to-error');
      expect(fromField()).not.toHaveAttribute('aria-invalid');
      expect(within(summary()!).getAllByRole('link').map((link) => link.textContent)).toEqual([words]);
      expect(exports()).toEqual([]);
    });

    it.each([
      { field: fromField, id: 'export-from', words: 'Enter the From date' },
      { field: toField, id: 'export-to', words: 'Enter the To date' },
    ])('says "$words" for an empty or incomplete date', async ({ field, id, words }) => {
      await openPage();
      enter(field(), '');
      await press(pdfRadio());
      await press(exportButton());

      expect(errorOf(id)).toBe(words);
      expect(within(summary()!).getByRole('link', { name: words })).toHaveAttribute('href', `#${id}`);
      expect(exports()).toEqual([]);
    });

    it('lists every error in page order: From, To, then Format', async () => {
      await openPage();
      enter(fromField(), '');
      enter(toField(), '');
      await press(exportButton());

      expect(within(summary()!).getAllByRole('link').map((link) => link.textContent)).toEqual([
        'Enter the From date',
        'Enter the To date',
        'Choose PDF or Word',
      ]);
    });

    it('shows no error before the first press, on blur or while typing', async () => {
      await openPage();

      enter(fromField(), '');
      fromField().focus();
      fireEvent.blur(fromField());
      enter(toField(), '2025-01-01');

      expect(errorOf('export-from')).toBeUndefined();
      expect(errorOf('export-to')).toBeUndefined();
      expect(errorOf('export-format')).toBeUndefined();
      expect(summaryHeading()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Export record');
    });

    it('clears an error at once when its field is fixed, and the summary and title prefix with the last', async () => {
      await openPage();
      enter(fromField(), '');
      await press(exportButton());
      expect(summary()).toBeInTheDocument();

      enter(fromField(), '2026-02-01');
      expect(errorOf('export-from')).toBeUndefined();
      expect(fromField()).not.toHaveAttribute('aria-invalid');
      expect(within(summary()!).queryByRole('link', { name: 'Enter the From date' })).toBeNull();
      expect(within(summary()!).getAllByRole('link')).toHaveLength(1);

      await press(pdfRadio());
      expect(errorOf('export-format')).toBeUndefined();
      expect(summaryHeading()).toBeNull();
      expect(document.title).toBe('Grow2Notes – Export record');
    });

    it('checks the To date again when the From date changes, while To shows an error', async () => {
      await openPage();
      enter(toField(), '2026-02-01');
      enter(fromField(), '2026-03-01');
      await press(pdfRadio());
      await press(exportButton());
      expect(errorOf('export-to')).toBe(`To date must be the same as or after 1${nbsp}March 2026`);

      enter(fromField(), '2026-04-01');
      expect(errorOf('export-to')).toBe(`To date must be the same as or after 1${nbsp}April 2026`);

      enter(fromField(), '2026-01-01');
      expect(errorOf('export-to')).toBeUndefined();
    });

    it('moves focus to the summary again on a second press with the same errors', async () => {
      await openPage();
      await press(exportButton());
      expect(summary()).toHaveFocus();

      fromField().focus();
      await press(exportButton());

      expect(summary()).toHaveFocus();
    });

    it('focuses the field a summary link names, the first radio for Format, and leaves the address alone', async () => {
      const { router } = await openPage();
      enter(fromField(), '');
      await press(exportButton());
      const before = router.state.location;

      fireEvent.click(within(summary()!).getByRole('link', { name: 'Choose PDF or Word' }));
      expect(pdfRadio()).toHaveFocus();
      fireEvent.click(within(summary()!).getByRole('link', { name: 'Enter the From date' }));
      expect(fromField()).toHaveFocus();

      expect(router.state.location.hash).toBe('');
      expect(router.state.location.pathname).toBe(before.pathname);
      expect(window.location.hash).toBe('');
    });

    it('sends a To date after today, which is no error', async () => {
      await openPage();
      enter(toField(), '2026-10-05');
      await press(pdfRadio());
      await press(exportButton());

      expect(summaryHeading()).toBeNull();
      expect(exports()).toEqual([exportPath('pdf', { to: '2026-10-05' })]);
    });
  });

  describe('the request', () => {
    it('sends one GET whose query matches the form', async () => {
      await openPage();
      enter(fromField(), '2026-02-01');
      enter(toField(), '2026-03-01');
      await press(historyBox());
      await exportAs('docx');

      expect(exports()).toEqual([exportPath('docx', { from: '2026-02-01', to: '2026-03-01', history: true })]);
    });

    it('sends includeHistory=false with the box unticked', async () => {
      await openPage();
      await exportAs('pdf');

      expect(exports()).toEqual([exportPath('pdf')]);
    });

    it('sends one request for a double click, and one for two Enters', async () => {
      exportAnswer = noAnswer;
      await openPage();
      await press(pdfRadio());

      fireEvent.click(exportButton());
      fireEvent.click(exportButton());
      await settle();
      fireEvent.submit(exportButton().closest('form')!);
      fireEvent.submit(exportButton().closest('form')!);
      await settle();

      expect(exports()).toEqual([exportPath('pdf')]);
    });

    it('keeps Export focusable and focused while busy: aria-disabled, never disabled', async () => {
      exportAnswer = noAnswer;
      await openPage();
      await press(pdfRadio());
      const button = exportButton();

      await press(button);

      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toHaveAttribute('disabled');
      expect(button).toHaveFocus();
    });

    it.each([
      ['pdf', 'Preparing the PDF file… Keep this page open.'],
      ['docx', 'Preparing the Word file… Keep this page open.'],
    ] as const)('shows nothing for 400 ms, then "Exporting…" and the preparing line for %s', async (format, line) => {
      exportAnswer = noAnswer;
      await openPage();
      await press(format === 'pdf' ? pdfRadio() : wordRadio());
      const button = exportButton();

      await press(button);
      await advance(398);
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toHaveAttribute('data-busy');
      expect(status()).toBeEmptyDOMElement();

      await advance(1);
      expect(button).toHaveAttribute('data-busy');
      expect(button).toHaveTextContent('Exporting…');
      expect(status()).toHaveTextContent(line);
      expect(status()).toHaveAttribute('data-preparing');
    });

    it('says the file is ready by the name in the answer, with focus and the form kept', async () => {
      exportAnswer = after(1_000, () =>
        Promise.resolve(
          new Response(bytes, {
            status: 200,
            headers: { 'Content-Disposition': `attachment; filename=${savedAs('pdf')}` },
          }),
        ),
      );
      await openPage();
      enter(toField(), '2026-09-30');
      await press(pdfRadio());
      await press(historyBox());
      const button = exportButton();

      await press(button);
      await advance(1_000);

      expect(saved).toEqual([savedAs('pdf')]);
      expect(status()).toHaveTextContent(`${savedAs('pdf')} is ready. Look in your downloads.`);
      expect(status()).not.toHaveAttribute('data-preparing');
      expect(alertBox()).toBeEmptyDOMElement();
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(fromField()).toHaveValue('2026-01-01');
      expect(toField()).toHaveValue('2026-09-30');
      expect(pdfRadio()).toBeChecked();
      expect(historyBox()).toBeChecked();
    });

    it('names the file by its dates and format when the answer does not', async () => {
      exportAnswer = fileFor;
      await openPage();
      await exportAs('docx');

      expect(saved).toEqual([savedAs('docx')]);
    });

    it('waits for a 25 second answer, with the busy label up at 24.9 seconds, and shows no alert', async () => {
      exportAnswer = after(25_000, () =>
        Promise.resolve(
          new Response(bytes, {
            status: 200,
            headers: { 'Content-Disposition': `attachment; filename=${savedAs('docx')}` },
          }),
        ),
      );
      await openPage();
      await press(wordRadio());
      const button = exportButton();

      await press(button);
      await advance(24_900);
      expect(button).toHaveTextContent('Exporting…');
      expect(status()).toHaveTextContent('Preparing the Word file… Keep this page open.');
      await advance(100);

      expect(saved).toEqual([savedAs('docx')]);
      expect(status()).toHaveTextContent('is ready. Look in your downloads.');
      expect(alertBox()).toBeEmptyDOMElement();
    });

    it('empties the alert on the next press', async () => {
      exportAnswer = problem(500);
      await openPage();
      await exportAs('pdf');
      expect(alertBox()).toHaveTextContent(wentWrong);

      exportAnswer = noAnswer;
      await press(exportButton());

      expect(alertBox()).toBeEmptyDOMElement();
    });
  });

  describe('what the server answers', () => {
    it('says the range has no submitted notes, with the dates sent, not red and not in the summary', async () => {
      exportAnswer = problem(404, { code: 'report.no_notes' });
      await openPage();
      await press(pdfRadio());
      const button = exportButton();

      await press(button);

      expect(status().textContent).toBe(noNotes);
      expect(alertBox()).toBeEmptyDOMElement();
      expect(summaryHeading()).toBeNull();
      expect(button).toHaveFocus();
      expect(saved).toEqual([]);
    });

    it('names the dates sent in the no-notes line, though a date was edited while the request was out', async () => {
      const waiting = held(problem(404, { code: 'report.no_notes' }));
      exportAnswer = waiting.answer;
      await openPage();
      await exportAs('pdf');

      enter(fromField(), '2026-02-01');
      waiting.release();
      await settle();

      expect(status().textContent).toBe(noNotes);
      expect(fromField()).toHaveValue('2026-02-01');
    });

    it('gives the generic message for a 404 with no code', async () => {
      exportAnswer = problem(404);
      await openPage();
      await exportAs('pdf');

      expect(alertBox()).toHaveTextContent(wentWrong);
      expect(status()).toBeEmptyDOMElement();
      expect(exports()).toHaveLength(1);
    });

    it.each([
      ['no connection', noConnection, noConnectionWords],
      ['a 500', problem(500), wentWrong],
      ['a 429', problem(429), tooMany],
    ])('shows the alert for %s, keeps focus on Export, and sends one request', async (_name, answer, words) => {
      exportAnswer = answer;
      await openPage();
      await press(pdfRadio());
      const button = exportButton();

      await press(button);
      await advance(5_000);

      expect(alertBox().textContent).toBe(words);
      expect(button).toHaveFocus();
      expect(exports()).toHaveLength(1);
      expect(saved).toEqual([]);
    });

    it('shows no alert for a 403, and asks who is signed in', async () => {
      exportAnswer = problem(403);
      await openPage();
      await press(pdfRadio());
      const before = meRequests();

      await press(exportButton());
      await settle();

      expect(alertBox()).toBeEmptyDOMElement();
      expect(meRequests()).toBeGreaterThan(before);
    });

    it('shows a 422 on To in the field and the summary, and focuses the summary', async () => {
      exportAnswer = problem(422, { code: 'validation.failed', errors: { to: ['To must be a date.'] } });
      await openPage();
      await exportAs('pdf');

      expect(errorOf('export-to')).toBe('Enter the To date');
      expect(within(summary()!).getByRole('link', { name: 'Enter the To date' })).toBeInTheDocument();
      expect(summary()).toHaveFocus();
      expect(alertBox()).toBeEmptyDOMElement();
    });

    it('maps a 422 on from and format to the screen’s own words', async () => {
      exportAnswer = problem(422, {
        code: 'validation.failed',
        errors: { from: ['From must be a date.'], format: ['Format must be pdf or docx.'] },
      });
      await openPage();
      await exportAs('pdf');

      expect(errorOf('export-from')).toBe('Enter the From date');
      expect(errorOf('export-format')).toBe('Choose PDF or Word');
    });

    it('moves focus to the summary again for a second 422 in a row', async () => {
      exportAnswer = problem(422, { code: 'validation.failed', errors: { to: ['To must be a date.'] } });
      await openPage();
      await exportAs('pdf');
      expect(summary()).toHaveFocus();

      fromField().focus();
      await press(exportButton());

      expect(summary()).toHaveFocus();
      expect(exports()).toHaveLength(2);
    });

    it('gives the generic message for a 422 that names a field the form does not have', async () => {
      exportAnswer = problem(422, { code: 'validation.failed', errors: { includeHistory: ['No.'] } });
      await openPage();
      await exportAs('pdf');

      expect(alertBox()).toHaveTextContent(wentWrong);
      expect(summaryHeading()).toBeNull();
    });

    it('shows no export message for a 401, and sends no second request', async () => {
      exportAnswer = problem(401);
      await openPage();
      await exportAs('pdf');
      await advance(5_000);

      expect(alertBox()).toBeEmptyDOMElement();
      expect(status()).toBeEmptyDOMElement();
      expect(exports()).toEqual([exportPath('pdf')]);
    });

    it('announces two identical failures: the alert is empty while the second request is out', async () => {
      exportAnswer = problem(500);
      await openPage();
      await exportAs('pdf');
      expect(alertBox()).toHaveTextContent(wentWrong);

      const waiting = held(problem(500));
      exportAnswer = waiting.answer;
      await press(exportButton());
      expect(alertBox()).toBeEmptyDOMElement();

      waiting.release();
      await settle();
      expect(alertBox()).toHaveTextContent(wentWrong);
    });
  });

  describe('leaving and changing', () => {
    it('aborts the request when the page goes, and saves and shows nothing after', async () => {
      exportAnswer = noAnswer;
      const { unmount } = await openPage();
      await exportAs('pdf');
      const signal = exportSignal();
      expect(signal?.aborted).toBe(false);

      unmount();
      await settle();

      expect(signal?.aborted).toBe(true);
      expect(saved).toEqual([]);
    });

    it('aborts the request on a move to another screen, and leaves no message', async () => {
      exportAnswer = noAnswer;
      const { router } = await openPage();
      await exportAs('pdf');
      const signal = exportSignal();

      await act(() => router.navigate('/no-such-page'));
      await settle();

      expect(signal?.aborted).toBe(true);
      expect(saved).toEqual([]);
      expect(document.getElementById('export-alert')).toBeNull();
    });

    it('empties the status line when a date changes after a result, and leaves the alert alone', async () => {
      await openPage();
      await exportAs('pdf');
      expect(status()).toHaveTextContent('is ready.');

      enter(toField(), '2026-09-30');

      expect(status()).toBeEmptyDOMElement();
    });

    it('empties the no-notes line when a date changes', async () => {
      exportAnswer = problem(404, { code: 'report.no_notes' });
      await openPage();
      await exportAs('pdf');
      expect(status().textContent).toBe(noNotes);

      enter(fromField(), '2026-02-01');

      expect(status()).toBeEmptyDOMElement();
    });

    it('keeps the status line when the format or the box changes', async () => {
      await openPage();
      await exportAs('pdf');

      await press(wordRadio());
      await press(historyBox());

      expect(status()).toHaveTextContent('is ready.');
    });

    it('keeps the preparing line when a date changes while the request is out', async () => {
      exportAnswer = noAnswer;
      await openPage();
      await exportAs('pdf');
      await advance(400);
      expect(status()).toHaveTextContent('Preparing the PDF file… Keep this page open.');

      enter(fromField(), '2026-02-01');

      expect(status()).toHaveTextContent('Preparing the PDF file… Keep this page open.');
      expect(exports()).toHaveLength(1);
    });

    it('keeps nothing in the browser after an export', async () => {
      await openPage();
      await exportAs('docx');

      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
    });
  });
});
