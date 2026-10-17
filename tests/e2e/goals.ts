import type { Page } from '@playwright/test';

/** A goal as `GET /api/admin/participants/{participantId}/goals` lists it (design.md §6.6). */
export type AdminGoal = {
  id: string;
  text: string;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the goal is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the goal sends as `If-Match` (D78). */
  rowVersion: string;
};

// The one request with which a participant's page reads their goals, with the participant: archived ones too.
const goalsRequest = '**/api/admin/participants/*/goals?includeArchived=true';

/**
 * Answers each participant's goals request, `GET /api/admin/participants/{participantId}/goals?includeArchived=true`,
 * with `goals`, in the browser, as `serveParticipants` answers the participant list, so that the request never reaches
 * the app, where no made-up manager has a session. A later call overrides it, as Playwright tries the latest route
 * first.
 */
export async function serveGoals(page: Page, goals: readonly AdminGoal[]): Promise<void> {
  await page.route(goalsRequest, (route) => route.fulfill({ json: goals }));
}
