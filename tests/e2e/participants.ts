import type { Page } from '@playwright/test';

/** A participant as `GET /api/admin/participants` lists them (design.md §6.6). */
export type AdminParticipant = {
  id: string;
  givenName: string;
  familyName: string;
  /** A calendar date, such as "1987-03-07". */
  dateOfBirth: string;
  status: 'Active' | 'Archived';
  /** The row's `RowVersion` as base64, which a change to the participant sends as `If-Match` (D78). */
  rowVersion: string;
};

/** A made-up participant (A35), born on 7 March 1987. */
export function madeUpParticipant(
  id: string,
  givenName: string,
  familyName: string,
  status: AdminParticipant['status'] = 'Active',
): AdminParticipant {
  return { id, givenName, familyName, dateOfBirth: '1987-03-07', status, rowVersion: `AAAAAAAA${id.slice(0, 4)}=` };
}

// Made-up participants (A35): two active and one archived.
export const jane = madeUpParticipant('3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95', 'Jane', 'Citizen');
export const ravi = madeUpParticipant('4a8e2c6f-1b39-4d57-9f0a-7c3e5b1d8a26', 'Ravi', 'de Silva');
export const ben = madeUpParticipant('2c9f6b3e-8a14-4e70-9d25-4b7e1f0a6c58', 'Ben', 'Archer', 'Archived');

/** The three, in the API's order, by family name, then given name (A5). */
export const participants: readonly AdminParticipant[] = [ben, jane, ravi];

// The one request with which the Participants screens read them: every participant, archived ones too.
const listRequest = '**/api/admin/participants?includeArchived=true';

/**
 * Answers each `GET /api/admin/participants?includeArchived=true`, the Participants screens' one read, with `list`, in
 * the browser, as `serveUsers` answers `/api/admin/users`, so that the request never reaches the app, where no made-up
 * manager has a session. A later call overrides it, as Playwright tries the latest route first.
 */
export async function serveParticipants(page: Page, list: readonly AdminParticipant[]): Promise<void> {
  await page.route(listRequest, (route) => route.fulfill({ json: list }));
}

/**
 * Aborts each `GET /api/admin/participants?includeArchived=true` that the page sends from now on, in the browser, as a
 * phone with no signal would, as `failUsers` does. A later call of `serveParticipants` overrides it.
 */
export async function failParticipants(page: Page): Promise<void> {
  await page.route(listRequest, (route) => route.abort('internetdisconnected'));
}
