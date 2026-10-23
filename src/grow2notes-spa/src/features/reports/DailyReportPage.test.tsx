import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../api/queryClient.ts';
import { DailyReportPage } from './DailyReportPage.tsx';

const exportPath = (format: string) => `/api/reports/daily/2026-10-01/export?format=${format}`;
const bytes = new Uint8Array([0x50, 0x4b, 0x03, 0x04]);

// Between a day and its month (microcopy.md §3).
const nbsp = ' ';

const fetchMock = vi.fn<typeof fetch>();
const createObjectURL = vi.fn<(blob: Blob) => string>();
const revokeObjectURL = vi.fn<(url: string) => void>();
/** The `download` name of each link clicked. */
let saved: string[] = [];

/** A `200` with a file whose `Content-Disposition` names it as the server does. */
function file(format: string) {
  return () =>
    Promise.resolve(
      new Response(bytes, {
        status: 200,
        headers: { 'Content-Disposition': `attachment; filename=daily-notes_2026-10-01.${format}` },
      }),
    );
}

/** A problem answer, as the API writes one. */
function problem(status: number, code?: string) {
  const extensions = code === undefined ? {} : { code };
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts. */
function noAnswer(_input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** Each path requested, in order. */
function requests(): string[] {
  return fetchMock.mock.calls.map(([path]) => path as string);
}

/** Renders the page at `/reports/daily/{date}` on a memory router. */
function open(date = '2026-10-01') {
  const router = createMemoryRouter([{ path: '/reports/daily/:date', element: <DailyReportPage /> }], {
    initialEntries: [`/reports/daily/${date}`],
  });
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

function word(): HTMLElement {
  return screen.getByRole('button', { name: 'Download Word' });
}

function pdf(): HTMLElement {
  return screen.getByRole('button', { name: 'Download PDF' });
}

async function press(button: HTMLElement) {
  fireEvent.click(button);
  await settle();
}

/** The failure line's words. */
function alertText(): string | null {
  return screen.getByRole('alert').textContent;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
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
  vi.useRealTimers();
  localStorage.clear();
  sessionStorage.clear();
});

describe('the Report screen', () => {
  it('has the <h1> "Daily report", the failure line, and the two buttons, Word first', () => {
    open();

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getByRole('heading', { level: 1 })).toHaveAccessibleName('Daily report');
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual([
      'Download Word',
      'Download PDF',
    ]);
    expect(screen.getByRole('alert')).toBeEmptyDOMElement();
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });

  it.each([
    ['Download Word', 'docx'],
    ['Download PDF', 'pdf'],
  ])('sends the export when %s is pressed, and saves the file under the name in the answer', async (name, format) => {
    fetchMock.mockImplementation(file(format));
    open();

    await press(screen.getByRole('button', { name }));

    expect(requests()).toEqual([exportPath(format)]);
    expect(saved).toEqual([`daily-notes_2026-10-01.${format}`]);
    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(screen.getByRole('alert')).toBeEmptyDOMElement();
    expect(word()).not.toHaveAttribute('aria-disabled');
    expect(pdf()).not.toHaveAttribute('aria-disabled');
  });

  it('names the file by the date and format when the answer does not name it', async () => {
    fetchMock.mockImplementation(() => Promise.resolve(new Response(bytes, { status: 200 })));
    open();

    await press(pdf());

    expect(saved).toEqual(['daily-notes_2026-10-01.pdf']);
  });

  describe('while a download is out', () => {
    it('makes both buttons aria-disabled, sends nothing more, and shows "Downloading…" at 400 ms', async () => {
      fetchMock.mockImplementation(noAnswer);
      open();

      await press(word());

      expect(word()).toHaveAttribute('aria-disabled', 'true');
      expect(pdf()).toHaveAttribute('aria-disabled', 'true');
      expect(word()).not.toHaveAttribute('data-busy');
      await act(() => vi.advanceTimersByTimeAsync(399));
      expect(word()).not.toHaveAttribute('data-busy');
      await act(() => vi.advanceTimersByTimeAsync(1));
      expect(word()).toHaveAttribute('data-busy');
      expect(word()).toHaveTextContent('Downloading…');
      expect(pdf()).not.toHaveAttribute('data-busy');
      expect(pdf()).not.toHaveTextContent('Downloading…');

      await press(pdf());
      await press(word());

      expect(requests()).toEqual([exportPath('docx')]);
    });

    it('keeps focus where it was', async () => {
      fetchMock.mockImplementation(noAnswer);
      open();
      pdf().focus();

      await press(pdf());

      expect(pdf()).toHaveFocus();
      expect(pdf()).toHaveTextContent('Downloading…');
    });

    it('gives the buttons back once the file is saved', async () => {
      fetchMock.mockImplementation(file('docx'));
      open();

      await press(word());
      await press(word());

      expect(requests()).toEqual([exportPath('docx'), exportPath('docx')]);
    });
  });

  describe('when the download fails', () => {
    it.each([
      ['there is no connection', noConnection, 'Not downloaded: no connection. Try again.'],
      ['the server fails', problem(500), 'Not downloaded: something went wrong. Try again.'],
      ['the answer is a 403', problem(403), 'Not downloaded: something went wrong. Try again.'],
      [
        'the day has no notes',
        problem(404, 'report.no_notes'),
        `No submitted notes for Thursday 1${nbsp}October 2026.`,
      ],
    ])('says so above the buttons when %s', async (_case, answer, words) => {
      fetchMock.mockImplementation(answer);
      open();

      await press(word());

      expect(alertText()).toBe(words);
      expect(saved).toEqual([]);
      expect(word()).not.toHaveAttribute('aria-disabled');
      expect(pdf()).not.toHaveAttribute('aria-disabled');
    });

    it('says nothing for a 401, which the session flow has', async () => {
      fetchMock.mockImplementation(problem(401));
      open();

      await press(word());

      expect(alertText()).toBe('');
      expect(word()).not.toHaveAttribute('aria-disabled');
    });

    it('empties the line on the next press', async () => {
      fetchMock.mockImplementation(problem(500));
      open();
      await press(word());
      expect(alertText()).toBe('Not downloaded: something went wrong. Try again.');

      fetchMock.mockImplementation(noAnswer);
      await press(pdf());

      expect(alertText()).toBe('');
    });
  });

  it('sends nothing for a date that is not yyyy-MM-dd, and says something went wrong', async () => {
    open('..%2Fusers');

    await press(word());

    expect(fetchMock).not.toHaveBeenCalled();
    expect(alertText()).toBe('Not downloaded: something went wrong. Try again.');
    expect(word()).not.toHaveAttribute('aria-disabled');
  });

  it('aborts the request in flight when the page goes, and saves nothing after', async () => {
    fetchMock.mockImplementation(noAnswer);
    const { unmount } = open();
    await press(word());
    const signal = fetchMock.mock.calls[0][1]?.signal;
    expect(signal?.aborted).toBe(false);

    unmount();
    await settle();

    expect(signal?.aborted).toBe(true);
    expect(saved).toEqual([]);
  });

  it('keeps nothing in the browser', async () => {
    fetchMock.mockImplementation(file('docx'));
    open();

    await press(word());
    fetchMock.mockImplementation(problem(404, 'report.no_notes'));
    await press(pdf());

    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });
});
