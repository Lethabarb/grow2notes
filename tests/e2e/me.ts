import { setTimeout as wait } from 'node:timers/promises';
import type { Page } from '@playwright/test';

/** The signed-in user, as `GET /api/auth/me` answers (design.md §6.2). */
export type Me = {
  userId: string;
  displayName: string;
  role: 'Worker' | 'Manager';
  organisationName: string;
  /** The date in Melbourne on the server (A33). */
  today: string;
};

// A made-up worker and manager (A35), in the JSON that MeEndpointTests asserts.
export const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
export const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

/**
 * Answers each `GET /api/auth/me` that the page sends from now on with `me`, in the browser, so that the request never
 * reaches the app: nobody can sign in in the end-to-end run until the sign-in stories are built (S00.05.01's Notes).
 * Everything else the page loads is the published app's, but Today's lists (`serveEmptyToday`). A test that changes
 * the person calls this again, which overrides the earlier answers, as Playwright tries the latest route first, and
 * then loads the page again, so that the SPA starts with the new person.
 */
export async function serveMe(page: Page, me: Me): Promise<void> {
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: me }));
  await serveEmptyToday(page, me);
}

/**
 * Answers each `GET /api/today` with no participants on `me`'s day, and each `GET /api/me/drafts` with no unfinished
 * drafts, in the browser: Today, where the made-up person lands, asks for both, which the app, holding no session for
 * them, would answer `401`, ending the session in place.
 */
async function serveEmptyToday(page: Page, me: Me): Promise<void> {
  await page.route('**/api/today', (route) => route.fulfill({ json: { date: me.today, participants: [] } }));
  await page.route('**/api/me/drafts', (route) => route.fulfill({ json: [] }));
}

/**
 * Fails each `GET /api/auth/me` that the page sends from now on, in the browser, as `serveMe` answers it.
 * `'no connection'` aborts it, as a phone with no signal would, rather than `context.setOffline`, which would stop the
 * page itself loading; a status answers it with that status and no body, such as `503` for a server problem.
 */
export async function failMe(page: Page, failure: 'no connection' | number): Promise<void> {
  await page.route('**/api/auth/me', (route) =>
    failure === 'no connection' ? route.abort('internetdisconnected') : route.fulfill({ status: failure }),
  );
}

/**
 * Answers each `GET /api/auth/me` that the page sends from now on with `me`, as `serveMe` does, but `ms` later, and
 * Today's lists as `serveMe` does, at once.
 */
export async function holdMe(page: Page, me: Me, ms: number): Promise<void> {
  await page.route('**/api/auth/me', async (route) => {
    await wait(ms);
    await route.fulfill({ json: me });
  });
  await serveEmptyToday(page, me);
}

/**
 * Answers each `GET /api/auth/me` that the page sends from now on with `body` as JSON, as `serveMe` does, though it is
 * not a `Me`, so that the page throws as it reads it: a `today` that is a number crashes Today, and `null` the shell.
 */
export async function answerMe(page: Page, body: unknown): Promise<void> {
  await page.route('**/api/auth/me', (route) =>
    route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) }),
  );
}
