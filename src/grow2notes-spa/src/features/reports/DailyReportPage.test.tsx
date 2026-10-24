import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import { DailyReportPage } from './DailyReportPage.tsx';

const today = '2026-10-01';
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today,
};
const bytes = new Uint8Array([0x50, 0x4b, 0x03, 0x04]);

// Between a day and its month (microcopy.md §3).
const nbsp = String.fromCodePoint(0xa0);
const noNotes = `No submitted notes for Thursday 1${nbsp}October 2026.`;
const todayLong = `Thursday 1${nbsp}October 2026`;
const dateNotFuture = 'Date must be today or in the past';
const wentWrong = 'Not downloaded: something went wrong. Try again.';

const exportPath = (format: string, date = today) => `/api/reports/daily/${date}/export?format=${format}`;
const hasNotesPath = (date = today) => `/api/reports/daily/${date}/has-notes`;

type Answer = (init?: RequestInit) => Promise<Response>;

const fetchMock = vi.fn<typeof fetch>();
const createObjectURL = vi.fn<(blob: Blob) => string>();
const revokeObjectURL = vi.fn<(url: string) => void>();
/** The `download` name of each link clicked. */
let saved: string[] = [];
/** What a path answers in place of the default, which a test sets. */
let answers = new Map<string, Answer>();
/** The query client and router of the last `open()`. */
let client: ReturnType<typeof createQueryClient>;
let router: ReturnType<typeof createMemoryRouter>;

function json(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** A `200` with a file whose `Content-Disposition` names it as the server does. */
function file(format: string, date = today) {
  return Promise.resolve(
    new Response(bytes, {
      status: 200,
      headers: { 'Content-Disposition': `attachment; filename=daily-notes_${date}.${format}` },
    }),
  );
}

/** A problem answer, as the API writes one. */
function problem(status: number, code?: string): Answer {
  const extensions = code === undefined ? {} : { code };
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions });
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

/** A server that answers a file after `ms`, as a long report does. */
function fileAfter(ms: number, format: string): Answer {
  return () => new Promise<Response>((resolve) => setTimeout(() => resolve(file(format)), ms));
}

/** The server: any has-notes check says yes and any export is a file, unless a test has set an answer. */
function serve(path: string, init?: RequestInit): Promise<Response> {
  const answer = answers.get(path);
  if (answer !== undefined) {
    return answer(init);
  }

  const check = /^\/api\/reports\/daily\/([\d-]+)\/has-notes$/.exec(path);
  if (check !== null) {
    return json({ date: check[1], hasNotes: true });
  }

  const report = /^\/api\/reports\/daily\/([\d-]+)\/export\?format=(docx|pdf)$/.exec(path);
  if (report !== null) {
    return file(report[2], report[1]);
  }

  return path === '/api/auth/me' ? json(manager) : problem(404)();
}

/** Each path requested, in order. */
function requests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string);
}

function exports(): string[] {
  return requests().filter((path) => path.includes('/export?'));
}

/** The abort signal of the request for `path`. */
function signalOf(path: string): AbortSignal | null | undefined {
  return fetchMock.mock.calls.find(([input]) => input === path)?.[1]?.signal;
}

/** Renders the page at `/reports/daily/{date}`, or `/reports/daily` for none, after the addresses `before` it. */
function open(date: string | null = today, { me = manager, before = [] as string[] } = {}) {
  const here = date === null ? '/reports/daily' : `/reports/daily/${date}`;
  router = createMemoryRouter(
    [
      { path: '/', element: <p>Elsewhere</p> },
      { path: '/reports/daily/:date?', element: <DailyReportPage />, handle: { screenKey: 'dailyReport' } },
    ],
    { initialEntries: [...before, here], initialIndex: before.length },
  );
  client = createQueryClient();
  client.setQueryData(meQuery.queryKey, me);
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

async function advance(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
  await settle();
}

async function back() {
  await act(() => router.navigate(-1));
}

function path(): string {
  return router.state.location.pathname;
}

const word = () => screen.getByRole('button', { name: 'Download Word' });
const pdf = () => screen.getByRole('button', { name: 'Download PDF' });
const previousDay = () => screen.getByRole('link', { name: 'Previous day' });
const nextDay = () => screen.getByRole('link', { name: 'Next day' });

function field(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Date');
}

/** The status under the field, and the one under the failure line: the first two of the page's three. */
const dateStatus = () => screen.getAllByRole('status')[0];
const downloadStatus = () => screen.getAllByRole('status')[1];

function alertText(): string | null {
  return screen.getByRole('alert').textContent;
}

function fieldError(): HTMLElement | null {
  return document.getElementById('report-date-error');
}

function type(value: string) {
  fireEvent.change(field(), { target: { value } });
}

async function press(button: HTMLElement) {
  fireEvent.click(button);
  await settle();
}

function expectAvailable(button: HTMLElement) {
  expect(button).not.toHaveAttribute('aria-disabled');
  expect(button).not.toHaveAttribute('data-unavailable');
}

function expectUnavailable(button: HTMLElement) {
  expect(button).toHaveAttribute('aria-disabled', 'true');
  expect(button).toHaveAttribute('data-unavailable');
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockImplementation((input, init) => serve(input as string, init));
  answers = new Map();
  // jsdom has neither, and none of its anchors navigates.
  Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true, writable: true });
  Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true, writable: true });
  createObjectURL.mockReturnValue('blob:https://grow2notes.test/1');
  saved = [];
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
    saved.push(this.download);
  });
});

afterEach(() => {
  vi.restoreAllMocks();
  fetchMock.mockReset();
  createObjectURL.mockReset();
  revokeObjectURL.mockReset();
  Reflect.deleteProperty(URL, 'createObjectURL');
  Reflect.deleteProperty(URL, 'revokeObjectURL');
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.useRealTimers();
  localStorage.clear();
  sessionStorage.clear();
});

describe('the Report screen', () => {
  describe('structure', () => {
    it('has the <h1>, the field, the steps, the date status, the alert, the status, then Word and PDF', async () => {
      const { container } = open();
      await settle();

      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      const order = Array.from(
        container.querySelectorAll('h1, input, a, [role="status"], [role="alert"], button'),
        (element) => {
          if (element.id === 'report-date-status') {
            return 'date status';
          }

          if (element.localName === 'input') {
            return 'field';
          }

          if (element.getAttribute('role') === 'alert') {
            return 'alert';
          }

          if (element.classList.contains('visually-hidden')) {
            return 'page status';
          }

          return element.getAttribute('role') === 'status' ? 'download status' : element.textContent;
        },
      );
      expect(order).toEqual([
        'Daily report',
        'field',
        '‹ Previous day',
        'Next day ›',
        'date status',
        'alert',
        'download status',
        'Download Word',
        'Download PDF',
        'page status',
      ]);
    });

    it('has a date field with today as its latest, and the date status as the buttons description', async () => {
      open('2026-09-30');
      await settle();

      expect(field()).toHaveAttribute('type', 'date');
      expect(field()).toHaveValue('2026-09-30');
      expect(field()).toHaveAttribute('max', today);
      expect(field()).not.toHaveAttribute('min');
      expect(field()).not.toBeDisabled();
      expect(field()).toHaveAttribute('aria-describedby', 'report-date-status');
      expect(word()).toHaveAttribute('aria-describedby', 'report-date-status');
      expect(pdf()).toHaveAttribute('aria-describedby', 'report-date-status');
      expect(dateStatus()).toHaveAttribute('id', 'report-date-status');
      expect(dateStatus().textContent).toBe(`Wednesday 30${nbsp}September 2026`);
      expect(dateStatus().querySelector('time')).toHaveAttribute('datetime', '2026-09-30');
    });
  });

  describe('arriving', () => {
    it.each([null, '2026-10-09'])('replaces %s with today, so that Back does not return to it', async (date) => {
      open(date, { before: ['/'] });
      await settle();

      expect(path()).toBe(`/reports/daily/${today}`);
      expect(field()).toHaveValue(today);
      await back();
      expect(path()).toBe('/');
    });

    it.each(['2026-02-29', '2026-13-45', '1899-12-31', '0050-01-01', 'last-week'])(
      'shows Page not found for %s and sends no request',
      async (date) => {
        open(date);
        await settle();

        expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
        expect(fetchMock).not.toHaveBeenCalled();
      },
    );

    it('opens 1900-01-01, the earliest date', async () => {
      open('1900-01-01');
      await settle();

      expect(field()).toHaveValue('1900-01-01');
    });
  });

  describe('stepping', () => {
    it('links Previous day to the day before, and Next day to the day after', async () => {
      open('2026-10-03', { me: { ...manager, today: '2026-10-10' } });
      await settle();

      expect(previousDay()).toHaveAttribute('href', '/reports/daily/2026-10-02');
      expect(nextDay()).toHaveAttribute('href', '/reports/daily/2026-10-04');
    });

    it('pushes one entry for each step, and Back walks back through the days', async () => {
      open(today, { before: ['/'] });
      await settle();

      await press(previousDay());
      await press(previousDay());
      expect(path()).toBe('/reports/daily/2026-09-29');
      expect(field()).toHaveValue('2026-09-29');
      await press(nextDay());
      expect(path()).toBe('/reports/daily/2026-09-30');

      await back();
      expect(path()).toBe('/reports/daily/2026-09-29');
      await back();
      expect(path()).toBe('/reports/daily/2026-09-30');
      await back();
      expect(path()).toBe(`/reports/daily/${today}`);
      await back();
      expect(path()).toBe('/');
    });

    it('has Next day unavailable on today: a link with no address, in the tab order, that does nothing', async () => {
      open(today, { before: ['/'] });
      await settle();

      const link = nextDay();
      expect(link).not.toHaveAttribute('href');
      expect(link).toHaveAttribute('role', 'link');
      expect(link).toHaveAttribute('aria-disabled', 'true');
      expect(link).toHaveAttribute('tabindex', '0');
      expect(link).toHaveAttribute('data-unavailable');
      link.focus();
      expect(link).toHaveFocus();

      await press(link);

      expect(path()).toBe(`/reports/daily/${today}`);
      await back();
      expect(path()).toBe('/');
    });

    it('has Previous day always available, with no earliest date', async () => {
      open('1900-01-01');
      await settle();

      expect(previousDay()).toHaveAttribute('href', '/reports/daily/1899-12-31');
    });

    it('keeps focus on the same Next day, now unavailable, when a step arrives on today', async () => {
      open('2026-09-30');
      await settle();
      const link = nextDay();
      link.focus();

      await press(link);

      expect(path()).toBe(`/reports/daily/${today}`);
      expect(nextDay()).toBe(link);
      expect(link).toHaveFocus();
      expect(link).not.toHaveAttribute('href');
      expect(link).toHaveAttribute('aria-disabled', 'true');
    });

    it('makes Next day available when /me says it is a new day, and stays on the day it was on (S20)', async () => {
      open();
      await settle();
      expect(nextDay()).not.toHaveAttribute('href');

      act(() => {
        client.setQueryData(meQuery.queryKey, { ...manager, today: '2026-10-02' });
      });
      await settle();

      expect(nextDay()).toHaveAttribute('href', '/reports/daily/2026-10-02');
      expect(path()).toBe(`/reports/daily/${today}`);
      expect(field()).toHaveValue(today);
    });

    it('takes today from /me, not from a device with the wrong zone and clock', async () => {
      vi.stubEnv('TZ', 'America/Los_Angeles');
      vi.setSystemTime(new Date('2030-01-01T00:00:00Z'));
      open(null);
      await settle();

      expect(path()).toBe(`/reports/daily/${today}`);
      expect(field()).toHaveValue(today);
      expect(field()).toHaveAttribute('max', today);
    });
  });

  describe('the date field', () => {
    it('pushes the date it commits after a 750 ms pause, and replaces it for the rest of the visit', async () => {
      open(today, { before: ['/'] });
      await settle();
      act(() => {
        field().focus();
      });

      type('2026-09-15');
      await advance(749);
      expect(path()).toBe(`/reports/daily/${today}`);
      await advance(1);
      expect(path()).toBe('/reports/daily/2026-09-15');
      expect(field()).toHaveValue('2026-09-15');

      type('2026-09-16');
      await advance(750);
      expect(path()).toBe('/reports/daily/2026-09-16');

      await back();
      expect(path()).toBe(`/reports/daily/${today}`);
      expect(field()).toHaveValue(today);
    });

    it('pushes again in a new visit to the field', async () => {
      open(today, { before: ['/'] });
      await settle();
      act(() => {
        field().focus();
      });
      type('2026-09-15');
      await advance(750);
      act(() => {
        field().blur();
      });
      await settle();
      act(() => {
        field().focus();
      });

      type('2026-09-16');
      await advance(750);
      await back();

      expect(path()).toBe('/reports/daily/2026-09-15');
    });

    it('commits on Enter at once', async () => {
      open();
      await settle();

      type('2026-09-15');
      fireEvent.keyDown(field(), { key: 'Enter' });
      await settle();

      expect(path()).toBe('/reports/daily/2026-09-15');
    });

    it('does not commit a typed date once Back has gone to another, whatever the pause', async () => {
      open(today, { before: ['/reports/daily/2026-09-30'] });
      await settle();

      type('2026-09-15');
      await advance(300);
      await back();
      await advance(1000);

      expect(path()).toBe('/reports/daily/2026-09-30');
      expect(field()).toHaveValue('2026-09-30');
    });

    it('does not commit a half-typed year such as 0202, and shows no error for it', async () => {
      open();
      await settle();

      type('0202-10-01');
      await advance(750);

      expect(path()).toBe(`/reports/daily/${today}`);
      expect(field()).not.toHaveAttribute('aria-invalid');
      expect(fieldError()).toBeNull();
      expect(dateStatus()).toBeEmptyDOMElement();
    });

    it('shows nothing in the date status while a valid date waits to be committed', async () => {
      open();
      await settle();

      type('2026-09-15');

      expect(dateStatus()).toBeEmptyDOMElement();
      expect(path()).toBe(`/reports/daily/${today}`);
    });

    it.each([
      ['Enter', () => fireEvent.keyDown(field(), { key: 'Enter' })],
      ['blur', () => fireEvent.blur(field())],
    ])('says a future date is not allowed on %s, and commits nothing', async (_how, leave) => {
      open();
      await settle();

      type('2026-10-05');
      leave();
      await advance(2000);

      expect(fieldError()).toHaveTextContent(dateNotFuture);
      expect(field()).toHaveAttribute('aria-invalid', 'true');
      expect(field()).toHaveAttribute('aria-describedby', 'report-date-error');
      expect(dateStatus()).toHaveTextContent(dateNotFuture);
      expect(path()).toBe(`/reports/daily/${today}`);
      expectUnavailable(word());
      expectUnavailable(pdf());
    });

    it('steps from the date in the address, and clears the error, when Previous day follows an error', async () => {
      open();
      await settle();
      type('2026-10-05');
      fireEvent.blur(field());

      await press(previousDay());

      expect(path()).toBe('/reports/daily/2026-09-30');
      expect(field()).toHaveValue('2026-09-30');
      expect(fieldError()).toBeNull();
      expect(field()).not.toHaveAttribute('aria-invalid');
      expectAvailable(word());
    });

    it('says "Enter a date" for an emptied field on blur', async () => {
      open();
      await settle();

      type('');
      fireEvent.blur(field());

      expect(fieldError()).toHaveTextContent('Enter a date');
      expect(dateStatus()).toHaveTextContent('Enter a date');
    });

    it('clears the error once the value passes', async () => {
      open();
      await settle();
      type('2026-10-05');
      fireEvent.blur(field());

      type('2026-09-15');

      expect(fieldError()).toBeNull();
      expect(field()).not.toHaveAttribute('aria-invalid');
    });
  });

  describe('whether the day has notes', () => {
    it('leaves the buttons available while the check is out', async () => {
      answers.set(hasNotesPath(), noAnswer);
      open();
      await settle();

      expectAvailable(word());
      expectAvailable(pdf());
      expect(dateStatus()).not.toHaveTextContent('No submitted notes');
    });

    it('says the day has no submitted notes, and makes both buttons unavailable but focusable', async () => {
      answers.set(hasNotesPath(), () => json({ date: today, hasNotes: false }));
      open();
      await settle();

      expect(dateStatus().textContent).toBe(noNotes);
      expectUnavailable(word());
      expectUnavailable(pdf());
      word().focus();
      expect(word()).toHaveFocus();

      await press(word());
      await press(pdf());

      expect(exports()).toEqual([]);
    });

    it('shows the date for a day with notes', async () => {
      open();
      await settle();

      expect(dateStatus().textContent).toBe(todayLong);
      expect(requests()).toEqual([hasNotesPath()]);
    });

    it.each([
      ['503', problem(503)],
      ['no connection', noConnection],
    ])('leaves the buttons available, and the alert empty, when the check fails with %s', async (_case, answer) => {
      answers.set(hasNotesPath(), answer);
      open();
      await advance(1500);

      expectAvailable(word());
      expectAvailable(pdf());
      expect(dateStatus()).not.toHaveTextContent('No submitted notes');
      expect(alertText()).toBe('');
    });

    it("never shows the last day's answer on the next day", async () => {
      answers.set(hasNotesPath(), () => json({ date: today, hasNotes: false }));
      answers.set(hasNotesPath('2026-09-30'), noAnswer);
      open();
      await settle();
      expect(dateStatus().textContent).toBe(noNotes);

      await press(previousDay());

      expect(path()).toBe('/reports/daily/2026-09-30');
      expect(dateStatus()).not.toHaveTextContent('No submitted notes');
      expectAvailable(word());
      expectAvailable(pdf());
    });

    it("asks again when the window takes focus, so that a worker's first note shows without a reload", async () => {
      let hasNotes = false;
      answers.set(hasNotesPath(), () => json({ date: today, hasNotes }));
      open();
      await settle();
      expect(dateStatus().textContent).toBe(noNotes);

      hasNotes = true;
      act(() => {
        window.dispatchEvent(new Event('focus'));
      });
      await settle();

      expect(dateStatus().textContent).toBe(todayLong);
      expectAvailable(word());
      expectAvailable(pdf());
    });

    it('shows Page not found when the check is refused with a 403', async () => {
      answers.set(hasNotesPath(), problem(403));
      open();
      await settle();

      expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Page not found');
    });
  });

  describe('downloading', () => {
    it.each([
      ['Download Word', 'docx'],
      ['Download PDF', 'pdf'],
    ])('sends the export when %s is pressed, and saves the file under the name in the answer', async (name, format) => {
      open();
      await settle();

      await press(screen.getByRole('button', { name }));

      expect(exports()).toEqual([exportPath(format)]);
      expect(saved).toEqual([`daily-notes_${today}.${format}`]);
      expect(createObjectURL).toHaveBeenCalledOnce();
      expect(alertText()).toBe('');
      expectAvailable(word());
      expectAvailable(pdf());
    });

    it('names the file by the date and format when the answer does not name it', async () => {
      answers.set(exportPath('pdf'), () => Promise.resolve(new Response(bytes, { status: 200 })));
      open();
      await settle();

      await press(pdf());

      expect(saved).toEqual([`daily-notes_${today}.pdf`]);
      expect(downloadStatus()).toHaveTextContent(`daily-notes_${today}.pdf is ready. Look in your downloads.`);
    });

    it.each([
      ['docx', 'Download Word', 'Download PDF', 'Preparing the Word file… Keep this page open.'],
      ['pdf', 'Download PDF', 'Download Word', 'Preparing the PDF file… Keep this page open.'],
    ])('shows nothing for 400 ms, then the busy label and the line, for %s', async (format, name, otherName, line) => {
      answers.set(exportPath(format), noAnswer);
      open();
      await settle();
      const pressed = screen.getByRole('button', { name });
      const other = screen.getByRole('button', { name: otherName });

      await press(pressed);
      await advance(399);

      expect(pressed).toHaveAttribute('aria-disabled', 'true');
      expect(pressed).not.toHaveAttribute('data-busy');
      expect(downloadStatus()).toBeEmptyDOMElement();

      await advance(1);

      expect(pressed).toHaveAttribute('data-busy');
      expect(pressed).toHaveTextContent('Downloading…');
      expect(other).not.toHaveAttribute('data-busy');
      expect(other).not.toHaveTextContent('Downloading…');
      expect(other).toHaveAttribute('aria-disabled', 'true');
      expect(downloadStatus()).toHaveTextContent(line);
    });

    it('says the file is ready, and keeps focus where it was', async () => {
      answers.set(exportPath('docx'), fileAfter(1000, 'docx'));
      open();
      await settle();
      word().focus();

      await press(word());
      await advance(500);
      expect(downloadStatus()).toHaveTextContent('Preparing the Word file… Keep this page open.');
      await advance(500);

      expect(downloadStatus()).toHaveTextContent(`daily-notes_${today}.docx is ready. Look in your downloads.`);
      expect(downloadStatus()).not.toHaveTextContent('Preparing');
      expect(word()).toHaveFocus();
      expect(word()).not.toHaveAttribute('data-busy');
    });

    it('waits for a 25 second answer, with both lines up at 24.9 seconds, and saves it', async () => {
      answers.set(exportPath('docx'), fileAfter(25_000, 'docx'));
      open();
      await settle();
      // Held before the press: the busy label takes the button's name once it shows.
      const wordButton = word();

      await press(wordButton);
      await advance(24_900);
      expect(wordButton).toHaveTextContent('Downloading…');
      expect(downloadStatus()).toHaveTextContent('Preparing the Word file… Keep this page open.');
      await advance(100);

      expect(saved).toEqual([`daily-notes_${today}.docx`]);
      expect(downloadStatus()).toHaveTextContent('is ready. Look in your downloads.');
      expect(alertText()).toBe('');
    });

    it('sends one request for a double click, and none for the other button while one is out', async () => {
      answers.set(exportPath('docx'), noAnswer);
      open();
      await settle();

      fireEvent.click(word());
      fireEvent.click(word());
      await settle();
      await press(pdf());
      await press(word());

      expect(exports()).toEqual([exportPath('docx')]);
    });

    it.each([
      ['there is no connection', noConnection, 'Not downloaded: no connection. Try again.'],
      ['the server fails', problem(500), wentWrong],
      [
        'there are too many downloads',
        problem(429),
        'Not downloaded: too many downloads in the last minute. Wait a minute, then try again.',
      ],
    ])('says so above the buttons, with one request, when %s', async (_case, answer, words) => {
      answers.set(exportPath('docx'), answer);
      open();
      await settle();
      word().focus();

      await press(word());

      expect(alertText()).toBe(words);
      expect(exports()).toEqual([exportPath('docx')]);
      expect(saved).toEqual([]);
      expectAvailable(word());
      expectAvailable(pdf());
      expect(word()).toHaveFocus();
      expect(downloadStatus()).toBeEmptyDOMElement();
    });

    it('empties the line on the next press', async () => {
      answers.set(exportPath('docx'), problem(500));
      answers.set(exportPath('pdf'), noAnswer);
      open();
      await settle();
      await press(word());
      expect(alertText()).toBe(wentWrong);

      await press(pdf());

      expect(alertText()).toBe('');
    });

    it('says the day has no notes, not that the download failed, for a 404 report.no_notes (S14)', async () => {
      answers.set(exportPath('docx'), problem(404, 'report.no_notes'));
      open();
      await settle();
      word().focus();

      await press(word());

      expect(dateStatus().textContent).toBe(noNotes);
      expectUnavailable(word());
      expectUnavailable(pdf());
      expect(alertText()).toBe('');
      expect(downloadStatus()).toBeEmptyDOMElement();
      expect(word()).toHaveFocus();
      expect(saved).toEqual([]);
    });

    it('says nothing for a 401, which the session flow has', async () => {
      answers.set(exportPath('docx'), problem(401));
      open();
      await settle();

      await press(word());

      expect(alertText()).toBe('');
      expect(downloadStatus()).toBeEmptyDOMElement();
      expect(exports()).toEqual([exportPath('docx')]);
      expectAvailable(word());
    });

    it('asks who is signed in after a 403, so that a manager who is one no longer gets Page not found', async () => {
      answers.set(exportPath('docx'), problem(403));
      open();
      await settle();

      await press(word());
      await settle();

      expect(requests()).toContain('/api/auth/me');
      expect(alertText()).toBe(wentWrong);
    });

    it('aborts the request when Previous day is pressed, and saves and shows nothing after', async () => {
      answers.set(exportPath('docx'), noAnswer);
      open();
      await settle();
      await press(word());
      const signal = signalOf(exportPath('docx'));
      expect(signal?.aborted).toBe(false);

      await press(previousDay());

      expect(signal?.aborted).toBe(true);
      expect(saved).toEqual([]);
      expect(alertText()).toBe('');
      expect(downloadStatus()).toBeEmptyDOMElement();
      expectAvailable(word());
    });

    it('aborts the request on Back, and leaves no message', async () => {
      answers.set(exportPath('docx', '2026-09-30'), noAnswer);
      open('2026-09-30', { before: [`/reports/daily/${today}`] });
      await settle();
      await press(word());
      const signal = signalOf(exportPath('docx', '2026-09-30'));

      await back();

      expect(path()).toBe(`/reports/daily/${today}`);
      expect(signal?.aborted).toBe(true);
      expect(alertText()).toBe('');
      expect(downloadStatus()).toBeEmptyDOMElement();
      expect(saved).toEqual([]);
    });

    it('aborts the request when the page goes, and saves nothing after', async () => {
      answers.set(exportPath('docx'), noAnswer);
      const { unmount } = open();
      await settle();
      await press(word());
      const signal = signalOf(exportPath('docx'));
      expect(signal?.aborted).toBe(false);

      unmount();
      await settle();

      expect(signal?.aborted).toBe(true);
      expect(saved).toEqual([]);
    });

    it("sends the new day's request when a press follows Previous day at once", async () => {
      answers.set(exportPath('docx'), noAnswer);
      open();
      await settle();
      await press(word());

      await press(previousDay());
      await press(pdf());

      expect(exports()).toEqual([exportPath('docx'), exportPath('pdf', '2026-09-30')]);
      expect(saved).toEqual(['daily-notes_2026-09-30.pdf']);
    });

    it('downloads the date in the field, moves the address to it, and is not aborted by its own move', async () => {
      answers.set(exportPath('docx', '2026-09-15'), noAnswer);
      open();
      await settle();

      const wordButton = word();

      type('2026-09-15');
      await press(wordButton);
      const signal = signalOf(exportPath('docx', '2026-09-15'));
      await advance(1000);

      expect(path()).toBe('/reports/daily/2026-09-15');
      expect(signal?.aborted).toBe(false);
      expect(wordButton).toHaveAttribute('aria-disabled', 'true');
      expect(wordButton).toHaveTextContent('Downloading…');
      expect(exports()).toEqual([exportPath('docx', '2026-09-15')]);
    });

    it('sends nothing for a value that is not a date, and focuses the field with its error', async () => {
      open();
      await settle();

      type('');
      await press(word());

      expect(exports()).toEqual([]);
      expect(field()).toHaveFocus();
      expect(fieldError()).toHaveTextContent('Enter a date');
    });

    it('sends nothing for a future date in the field, and focuses the field with its error', async () => {
      open();
      await settle();

      type('2026-10-05');
      await press(pdf());

      expect(exports()).toEqual([]);
      expect(field()).toHaveFocus();
      expect(fieldError()).toHaveTextContent(dateNotFuture);
    });

    it('explains an error when an unavailable button is pressed, by focusing the field', async () => {
      open();
      await settle();
      type('2026-10-05');
      fireEvent.blur(field());
      expectUnavailable(word());

      await press(word());

      expect(field()).toHaveFocus();
      expect(exports()).toEqual([]);
    });
  });

  it('keeps nothing in the browser', async () => {
    open();
    await settle();

    await press(word());
    answers.set(exportPath('pdf'), problem(404, 'report.no_notes'));
    await press(pdf());
    type('2026-09-15');
    await advance(750);

    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });
});
