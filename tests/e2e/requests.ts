import type { Page, Route } from '@playwright/test';

/**
 * Holds the page's next request for `pattern` until the test answers it or lets it go on to the app. Gives a function
 * that waits for the request and gives its route.
 */
export async function holdNext(page: Page, pattern: string): Promise<() => Promise<Route>> {
  let hold: (route: Route) => void = () => undefined;
  const held = new Promise<Route>((resolve) => {
    hold = resolve;
  });
  await page.route(pattern, (route) => hold(route), { times: 1 });
  return () => held;
}
