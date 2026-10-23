import type { Page } from '@playwright/test';

/** The day of the made-up report (daily-report.md): Thursday 1 October 2026. */
export const reportDay = '2026-10-01';

/** The Report screen's address. */
export const reportAddress = `/reports/daily/${reportDay}`;

const contentTypes = {
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  pdf: 'application/pdf',
} as const;

const isExport = (url: URL) => url.pathname === `/api/reports/daily/${reportDay}/export`;

/**
 * Answers each `GET /api/reports/daily/2026-10-01/export` that the page sends from now on with made-up bytes, in the
 * browser, named as the server names the file, with `filename` and `filename*` (design.md A80), so that the request
 * never reaches the app, where no made-up manager has a session. The real files are checked by the server's tests.
 */
export async function serveReport(page: Page): Promise<void> {
  await page.route(isExport, (route) => {
    const format = new URL(route.request().url()).searchParams.get('format') === 'pdf' ? 'pdf' : 'docx';
    const name = `daily-notes_${reportDay}.${format}`;
    return route.fulfill({
      status: 200,
      contentType: contentTypes[format],
      headers: { 'Content-Disposition': `attachment; filename=${name}; filename*=UTF-8''${name}` },
      body: Buffer.from('Made-up bytes, not a real file.'),
    });
  });
}

/** Answers each such request with `404 report.no_notes`, as the server does for a day with no submitted note. */
export async function serveNoNotes(page: Page): Promise<void> {
  await page.route(isExport, (route) =>
    route.fulfill({
      status: 404,
      contentType: 'application/problem+json',
      body: JSON.stringify({ type: 'about:blank', title: 'Not Found', status: 404, code: 'report.no_notes' }),
    }),
  );
}

/** Records the query of each export request that the page sends from now on, such as `?format=docx`. */
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
