import type { Page, Route } from '@playwright/test';
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

/** A second Active manager, Kim Tran, with whom Sam Lee is no longer the organisation's only one (A26). */
export const kim = madeUpUser(
  'b2d6f0a4-7e31-4c9b-8a5d-1f3e7c2b9d60',
  'Kim Tran',
  'kim.tran@example.org',
  'Active',
  'Manager',
);

/** `users` with `user` as `changed` makes them, and a new stamp, as the list gives them after a change to them. */
export function withChanged(users: readonly AdminUser[], user: AdminUser, changed: Partial<AdminUser>): AdminUser[] {
  return users.map((listed) =>
    listed.id === user.id ? { ...listed, concurrencyStamp: 'stamp-2', ...changed } : listed,
  );
}

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

/** How a test answers Invite user's `POST /api/admin/users`: a status, with a problem's members, or no connection. */
export type InviteAnswer = { status: number; problem?: Record<string, unknown> } | 'no connection';

/** Answers a request as `answer` says: with that status and an RFC 9457 problem, or, for no connection, not at all. */
async function answerWith(route: Route, answer: InviteAnswer): Promise<void> {
  if (answer === 'no connection') {
    await route.abort('internetdisconnected');
    return;
  }

  // An RFC 9457 problem, as the API answers a failure (design.md §6.1).
  const problem = { type: 'about:blank', title: 'Problem', status: answer.status, ...answer.problem };
  await route.fulfill({
    status: answer.status,
    contentType: 'application/problem+json',
    body: JSON.stringify(problem),
  });
}

/**
 * Answers each `POST /api/admin/users` that the page sends from now on, in the browser, as `answer` says, so that the
 * invite never reaches the app, and passes every other request on to the routes before it, such as `serveUsers`'s.
 * Call it after them, as Playwright tries the latest route first.
 */
export async function answerInvites(page: Page, answer: InviteAnswer): Promise<void> {
  await page.route('**/api/admin/users', async (route) => {
    await (route.request().method() === 'POST' ? answerWith(route, answer) : route.fallback());
  });
}

/**
 * The patterns of the changes to a user that a user's page sends (design.md §6.6), for any user: Resend invite's and
 * Reset sign-in's `POST /api/admin/users/{id}/setup-link`, Deactivate's and Reactivate's.
 */
export const setupLinks = '**/api/admin/users/*/setup-link';
export const deactivations = '**/api/admin/users/*/deactivate';
export const reactivations = '**/api/admin/users/*/reactivate';

/**
 * How a test answers a change to a user: the app's `204` sent at `sentAt`, its `Date` header, such as "Wed, 14 Oct
 * 2026 05:12:00 GMT", from which Resend invite takes the time; or as an invite is answered.
 */
export type ChangeAnswer = { sentAt: string } | InviteAnswer;

/**
 * Answers each change that the page sends from now on to `pattern`, one of the three above, in the browser, as
 * `answer` says, so that none reaches the app. A later call overrides it, as Playwright tries the latest route first.
 */
export async function answerChanges(page: Page, pattern: string, answer: ChangeAnswer): Promise<void> {
  await page.route(pattern, async (route) => {
    if (typeof answer === 'object' && 'sentAt' in answer) {
      await route.fulfill({ status: 204, headers: { Date: answer.sentAt } });
    } else {
      await answerWith(route, answer);
    }
  });
}

/** Answers each Resend invite or Reset sign-in that the page sends from now on, as `answerChanges` does. */
export function answerSetupLinks(page: Page, answer: ChangeAnswer): Promise<void> {
  return answerChanges(page, setupLinks, answer);
}

/**
 * Holds the page's next `POST /api/admin/users` until the test lets it go on to the app, as `holdNext` holds a request
 * (requests.ts), and passes every other request on. Gives a function that waits for the invite and gives its route.
 */
export async function holdNextInvite(page: Page): Promise<() => Promise<Route>> {
  let hold: (route: Route) => void = () => undefined;
  const held = new Promise<Route>((resolve) => {
    hold = resolve;
  });
  let holding = true;
  await page.route('**/api/admin/users', async (route) => {
    if (holding && route.request().method() === 'POST') {
      holding = false;
      hold(route);
    } else {
      await route.fallback();
    }
  });
  return () => held;
}
