import type { Page } from '@playwright/test';
import { manager, type Me } from './me.ts';

/** A user as `GET /api/admin/users` lists them (design.md §6.6). */
export type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  role: Me['role'];
  status: 'Invited' | 'Active' | 'Deactivated';
  /** In UTC, with `Z`, as are the two times after it. */
  invitedAtUtc: string;
  activatedAtUtc: string | null;
  deactivatedAtUtc: string | null;
  /** The user's Identity `ConcurrencyStamp`, which a change to the user sends as `If-Match` (D78). */
  concurrencyStamp: string;
};

/**
 * A made-up user (A35), invited on 1 September, set up on 2 September unless still Invited, and deactivated on
 * 30 September if Deactivated.
 */
export function madeUpUser(
  id: string,
  displayName: string,
  email: string,
  status: AdminUser['status'],
  role: AdminUser['role'] = 'Worker',
): AdminUser {
  return {
    id,
    email,
    displayName,
    role,
    status,
    invitedAtUtc: '2026-09-01T00:00:00Z',
    activatedAtUtc: status === 'Invited' ? null : '2026-09-02T00:00:00Z',
    deactivatedAtUtc: status === 'Deactivated' ? '2026-09-30T00:00:00Z' : null,
    concurrencyStamp: `stamp-${id}`,
  };
}

// users.md's made-up people (A35): Sam Lee, me.ts's manager, who is signed in, and three workers.
export const sam = madeUpUser(manager.userId, manager.displayName, 'sam.lee@example.org', 'Active', 'Manager');
export const jo = madeUpUser('0c6f2e8a-4b17-4d39-a5e2-7f1b3c9d0a64', 'Jo Smith', 'jo.smith@example.org', 'Active');
export const alex = madeUpUser('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Alex Park', 'alex.park@example.org', 'Invited');
export const chris = madeUpUser(
  '7a1d4f9c-2e53-4b86-9c07-5f3e1a8d6b20',
  'Chris Ng',
  'chris.ng@example.org',
  'Deactivated',
);

/** The four, in no particular order, as the API sorts nothing (design.md §6.6). */
export const people: readonly AdminUser[] = [sam, chris, jo, alex];

/**
 * Answers each `GET /api/admin/users` that the page sends from now on with `users`, in the browser, as `serveMe`
 * answers `/api/auth/me`, so that the request never reaches the app, where no made-up manager has a session. A later
 * call overrides it, as Playwright tries the latest route first.
 */
export async function serveUsers(page: Page, users: readonly AdminUser[]): Promise<void> {
  await page.route('**/api/admin/users', (route) => route.fulfill({ json: users }));
}

/**
 * Aborts each `GET /api/admin/users` that the page sends from now on, in the browser, as a phone with no signal would,
 * as `failMe` does with `'no connection'`.
 */
export async function failUsers(page: Page): Promise<void> {
  await page.route('**/api/admin/users', (route) => route.abort('internetdisconnected'));
}
