import type { Page } from '@playwright/test';
import type { NotesStore } from './notes.ts';

/**
 * A note on the To review list, as `GET /api/reviews?status=toReview` gives it to a manager (design.md §6.4): its
 * participant's ID and names as stored; its date; its author's display name as stored; the reason, whole, and the time
 * of its flagged version, in UTC, written with `Z`; the number of its current version; and whether that version is not
 * flagged, a later edit having removed the flag.
 */
export type ToReviewRow = {
  participantId: string;
  givenName: string;
  familyName: string;
  noteDate: string;
  authorDisplayName: string;
  flagReason: string;
  flaggedAtUtc: string;
  currentVersionNumber: number;
  flagRemovedLater: boolean;
};

// The Flagged screen's one read.
const toReviewRequest = '**/api/reviews?status=toReview';

/**
 * Answers each `GET /api/reviews?status=toReview` that the page sends from now on with `store`'s To review list, in
 * the browser, so that the request never reaches the app, where no made-up manager has a session. The list is worked
 * out afresh for each request, so that a flag that a Submit to the store makes shows on the next one. A later call
 * overrides it, as Playwright tries the latest route first.
 */
export async function serveReviews(page: Page, store: NotesStore): Promise<void> {
  await page.route(toReviewRequest, (route) => route.fulfill({ json: store.toReview() }));
}

/**
 * Fails each `GET /api/reviews?status=toReview` that the page sends from now on, in the browser: `'no connection'`
 * aborts it, as a phone with no signal would, and a status answers it with that status and no body, such as `403` for
 * a manager whose role has changed. A failure of the first load shows about 1 s after the request, after the query
 * client's silent retry. A later `serveReviews` overrides it.
 */
export async function failReviews(page: Page, failure: 'no connection' | number): Promise<void> {
  await page.route(toReviewRequest, (route) =>
    failure === 'no connection' ? route.abort('internetdisconnected') : route.fulfill({ status: failure }),
  );
}
