import type { Locator, Page } from '@playwright/test';

/** The day of the made-up report (daily-report.md): Thursday 1 October 2026. */
export const reportDay = '2026-10-01';

/** The Report screen's address. */
export const reportAddress = `/reports/daily/${reportDay}`;

const contentTypes = {
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  pdf: 'application/pdf',
} as const;

/** The test of the URL of the made-up day's export request, for `holdNext` and `page.route`. */
export const isExport = (url: URL) => url.pathname === `/api/reports/daily/${reportDay}/export`;

/**
 * Answers each `GET /api/reports/daily/{date}/has-notes` that the page sends from now on, in the browser, with the
 * date and whether it is not one of `emptyDays`. Registered before the page loads in every spec that lands on the
 * Report screen: otherwise the request reaches the app, where no made-up manager has a session, and its `401` ends the
 * session in place.
 */
export async function serveHasNotes(page: Page, emptyDays: readonly string[] = []): Promise<void> {
  await page.route('**/api/reports/daily/*/has-notes', (route) => {
    const date = new URL(route.request().url()).pathname.split('/')[4];
    return route.fulfill({ json: { date, hasNotes: !emptyDays.includes(date) } });
  });
}

/**
 * Sets the date field's value as a person's change does, in the same visit to the field if it has focus already. A
 * script's plain assignment to a controlled input's `value` is not seen by React as a change, so the setter is the
 * prototype's, followed by the `input` event.
 */
export async function enterDate(field: Locator, value: string): Promise<void> {
  await field.evaluate((input: HTMLInputElement, typed) => {
    if (document.activeElement !== input) {
      input.focus();
    }

    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(input, typed);
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }, value);
}

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

/**
 * Serves the Report screen's page without `upgrade-insecure-requests` in its Content Security Policy, so that WebKit
 * loads the app over the plain HTTP that the end-to-end run uses. WebKit upgrades `http://localhost` requests under
 * that directive, to a port that has no TLS, where Chromium and Firefox leave them alone; production is HTTPS only.
 */
export async function serveOverHttp(page: Page): Promise<void> {
  await page.route(
    (url) => url.pathname === reportAddress,
    async (route) => {
      const response = await route.fetch();
      const policy = response.headers()['content-security-policy'] ?? '';
      await route.fulfill({
        response,
        headers: {
          ...response.headers(),
          'content-security-policy': policy.replace(/;?\s*upgrade-insecure-requests/, ''),
        },
      });
    },
  );
}
