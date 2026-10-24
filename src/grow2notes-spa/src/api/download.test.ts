import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './api.ts';
import { download } from './download.ts';

const exportPath = '/api/reports/daily/2026-10-01/export?format=docx';
const fallbackName = 'daily-notes_2026-10-01.docx';
const bytes = new Uint8Array([0x50, 0x4b, 0x03, 0x04]);

const fetchMock = vi.fn<typeof fetch>();
const createObjectURL = vi.fn<(blob: Blob) => string>();
const revokeObjectURL = vi.fn<(url: string) => void>();

/** What the temporary link was when it was clicked. */
type Clicked = { name: string; rel: string; hasTarget: boolean; href: string; hidden: boolean; connected: boolean };
let clicked: Clicked[] = [];

/** A `200` with a file and, when given, a `Content-Disposition`. */
function file(disposition?: string) {
  const headers: Record<string, string> = disposition === undefined ? {} : { 'Content-Disposition': disposition };
  return Promise.resolve(new Response(bytes, { status: 200, headers }));
}

/** Like `fetch` to a server that never answers: it rejects only when its signal aborts. */
function noAnswer(_input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
  // jsdom has neither, and none of its anchors navigates.
  Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true, writable: true });
  Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true, writable: true });
  createObjectURL.mockReturnValue('blob:https://grow2notes.test/1');
  clicked = [];
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
    clicked.push({
      name: this.download,
      rel: this.rel,
      hasTarget: this.hasAttribute('target'),
      href: this.href,
      hidden: this.hidden === true,
      connected: this.isConnected,
    });
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
});

describe('download', () => {
  it.each([
    [
      'filename* first, percent-decoded',
      "attachment; filename=plain.docx; filename*=UTF-8''Zo%C3%AB%20notes.docx",
      'Zoë notes.docx',
    ],
    ['a quoted filename', 'attachment; filename="daily-notes_2026-10-01.pdf"', 'daily-notes_2026-10-01.pdf'],
    ['a bare filename', 'attachment; filename=daily-notes_2026-10-01.pdf', 'daily-notes_2026-10-01.pdf'],
    ['a bare filename before other parameters', 'attachment; filename=a.docx; size=10', 'a.docx'],
    ['a filename when filename* does not decode', "attachment; filename=b.docx; filename*=UTF-8''%E0%A4%A", 'b.docx'],
    ['the fallback when there is no filename', 'attachment', fallbackName],
    ['the fallback when the filename is empty', 'attachment; filename=""', fallbackName],
  ])('names the file by %s', async (_case, disposition, name) => {
    fetchMock.mockResolvedValue(await file(disposition));

    const saved = await download(exportPath, fallbackName, new AbortController().signal);

    expect(clicked.map((link) => link.name)).toEqual([name]);
    expect(saved).toBe(name);
  });

  it('names the file by the fallback when the answer has no Content-Disposition', async () => {
    fetchMock.mockResolvedValue(await file());

    const saved = await download(exportPath, fallbackName, new AbortController().signal);

    expect(clicked.map((link) => link.name)).toEqual([fallbackName]);
    expect(saved).toBe(fallbackName);
  });

  it('sends a GET of the path and saves the bytes through a hidden link, appended, clicked and removed', async () => {
    fetchMock.mockResolvedValue(await file('attachment; filename=daily-notes_2026-10-01.docx'));

    await download(exportPath, fallbackName, new AbortController().signal);

    expect(fetchMock.mock.calls[0][0]).toBe(exportPath);
    expect(fetchMock.mock.calls[0][1]?.method).toBe('GET');
    expect(createObjectURL).toHaveBeenCalledOnce();
    const blob = createObjectURL.mock.calls[0][0];
    expect(new Uint8Array(await blob.arrayBuffer())).toEqual(bytes);
    expect(clicked).toEqual([
      {
        name: 'daily-notes_2026-10-01.docx',
        rel: 'noopener',
        hasTarget: false,
        href: 'blob:https://grow2notes.test/1',
        hidden: true,
        connected: true,
      },
    ]);
    expect(document.querySelector('a[download]')).toBeNull();
  });

  it('revokes the URL after 60 seconds, and not before', async () => {
    fetchMock.mockResolvedValue(await file());

    await download(exportPath, fallbackName, new AbortController().signal);

    await vi.advanceTimersByTimeAsync(59_999);
    expect(revokeObjectURL).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1);
    expect(revokeObjectURL).toHaveBeenCalledExactlyOnceWith('blob:https://grow2notes.test/1');
  });

  it('saves nothing when the answer is a refusal', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ code: 'report.no_notes' }), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    );

    await expect(download(exportPath, fallbackName, new AbortController().signal)).rejects.toStrictEqual(
      new ApiError(404, 'report.no_notes'),
    );
    expect(createObjectURL).not.toHaveBeenCalled();
    expect(clicked).toEqual([]);
  });

  it('fails at 30 seconds with no answer, and not at 10', async () => {
    fetchMock.mockImplementation(noAnswer);
    let settled = false;
    const request = download(exportPath, fallbackName, new AbortController().signal);
    const done = () => {
      settled = true;
    };
    void request.then(done, done);

    await vi.advanceTimersByTimeAsync(10_000);
    expect(settled).toBe(false);

    await vi.advanceTimersByTimeAsync(19_999);
    expect(settled).toBe(false);

    await vi.advanceTimersByTimeAsync(1);
    await expect(request).rejects.toStrictEqual(new ApiError(0));
  });

  it("rethrows the caller's abort as it came", async () => {
    fetchMock.mockImplementation(noAnswer);
    const caller = new AbortController();
    const reason = new DOMException('The page was left.', 'AbortError');
    const request = download(exportPath, fallbackName, caller.signal);

    caller.abort(reason);

    await expect(request).rejects.toBe(reason);
    expect(clicked).toEqual([]);
  });
});
