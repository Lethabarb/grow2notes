import type { Page } from '@playwright/test';

/** Jane Citizen, the made-up participant of the Export record screen (record-export.md, A35). */
export const participantId = '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95';

/** Her earliest submitted note, and the made-up today (Thursday 1 October 2026). */
export const firstNoteDate = '2026-01-01';
export const exportDay = '2026-10-01';

/** The Export record screen's address. */
export const exportAddress = `/manage/participants/${participantId}/export`;

const contentTypes = {
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  pdf: 'application/pdf',
} as const;

/** The test of the URL of the participant's export request, for `holdNext` and `page.route`. */
export const isExport = (url: URL) => url.pathname === `/api/reports/participants/${participantId}/export`;

/** What `serveParticipant` answers: the participant's header, as `GET /api/participants/{id}` gives it (§6.3). */
type Header = { status?: 'Active' | 'Archived'; firstNoteDate?: string | null };

/**
 * Answers each `GET /api/participants/{id}` that the page sends from now on with Jane Citizen's header, in the browser,
 * so that the request never reaches the app, where no made-up manager has a session.
 */
export async function serveParticipant(page: Page, header: Header = {}) {
  const { status = 'Active', firstNoteDate: first = firstNoteDate } = header;
  await page.route(`**/api/participants/${participantId}`, (route) =>
    route.fulfill({
      json: { id: participantId, givenName: 'Jane', familyName: 'Citizen', status, firstNoteDate: first },
    }),
  );
}

/** The name that the server gives the file for a request's query (A80). */
export function fileNameOf(search: URLSearchParams): string {
  return `participant-record_${search.get('from') ?? ''}_to_${search.get('to') ?? ''}.${search.get('format') ?? ''}`;
}

/**
 * Answers each `GET /api/reports/participants/{id}/export` that the page sends from now on with made-up bytes, in the
 * browser, named as the server names the file, with `filename` and `filename*`, so that the request never reaches the
 * app. The real files are checked by the server's tests.
 */
export async function serveRecord(page: Page): Promise<void> {
  await page.route(isExport, (route) => {
    const search = new URL(route.request().url()).searchParams;
    const name = fileNameOf(search);
    return route.fulfill({
      status: 200,
      contentType: contentTypes[search.get('format') === 'pdf' ? 'pdf' : 'docx'],
      headers: { 'Content-Disposition': `attachment; filename=${name}; filename*=UTF-8''${name}` },
      body: Buffer.from('Made-up bytes, not a real file.'),
    });
  });
}

/** Answers each such request with `404 report.no_notes`, as the server does for a range with no submitted note. */
export async function serveNoNotes(page: Page): Promise<void> {
  await page.route(isExport, (route) =>
    route.fulfill({
      status: 404,
      contentType: 'application/problem+json',
      body: JSON.stringify({ type: 'about:blank', title: 'Not Found', status: 404, code: 'report.no_notes' }),
    }),
  );
}

/** Records the query of each export request that the page sends from now on, such as `?from=...&format=pdf`. */
export function recordExports(page: Page): string[] {
  const sent: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (isExport(url)) {
      sent.push(url.search);
    }
  });
  return sent;
}

/** The query of an export of the default range, as the page writes it. */
export const defaultQuery = (format: 'docx' | 'pdf', includeHistory = false) =>
  `?from=${firstNoteDate}&to=${exportDay}&format=${format}&includeHistory=${String(includeHistory)}`;
