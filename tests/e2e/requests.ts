import type { Page, Route } from '@playwright/test';

/**
 * Holds the page's next request for `pattern`, a glob or, for an address that a glob cannot tell apart by its query, a
 * test of the URL, as `page.route` takes them, until the test answers it or lets it go on to the app. Gives a function
 * that waits for the request and gives its route.
 */
export async function holdNext(
  page: Page,
  pattern: string | ((url: URL) => boolean),
): Promise<() => Promise<Route>> {
  let hold: (route: Route) => void = () => undefined;
  const held = new Promise<Route>((resolve) => {
    hold = resolve;
  });
  await page.route(pattern, (route) => hold(route), { times: 1 });
  return () => held;
}

/**
 * Records each request that the page sends from now on, but the test's own `GET /healthz` and the antiforgery token's,
 * as its method, path and, for a change, its `If-Match`. Gives a function that gives them once the browser has
 * reported every request that the page sent before the call: it reports them in the order they were sent
 * (tests/README.md).
 */
export function recordRequests(page: Page): () => Promise<string[]> {
  const sent: string[] = [];
  page.on('request', (request) => {
    const { pathname } = new URL(request.url());
    if (pathname !== '/healthz' && pathname !== '/api/auth/antiforgery') {
      const ifMatch = request.headers()['if-match'];
      sent.push(`${request.method()} ${pathname}${ifMatch === undefined ? '' : ` ${ifMatch}`}`);
    }
  });

  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return sent;
  };
}
