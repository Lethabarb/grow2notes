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
 * Everything else the page loads is the published app's. A test that changes the person calls this again, which
 * overrides the earlier answer, as Playwright tries the latest route first, and then loads the page again, so that the
 * SPA starts with the new person.
 */
export async function serveMe(page: Page, me: Me): Promise<void> {
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: me }));
}
