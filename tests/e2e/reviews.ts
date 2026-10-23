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

/**
 * A review on the Reviewed list, as `GET /api/reviews?status=reviewed` gives it to a manager (design.md §6.4): the
 * review's ID; its note's participant's ID and names as stored; the note's date; its author's display name as stored;
 * the reason of the flag that the review cleared, whole; the reviewer's display name as stored; when it was made, in
 * UTC, written with `Z` to the millisecond, as the server writes it; and its comment, as written, or `null`.
 */
export type ReviewedRow = {
  reviewId: string;
  participantId: string;
  givenName: string;
  familyName: string;
  noteDate: string;
  authorDisplayName: string;
  flagReason: string;
  reviewedBy: string;
  reviewedAtUtc: string;
  comment: string | null;
};

/** A page of the Reviewed list is at most 30 reviews (design.md §6.1 *Lists*). */
const reviewedPageSize = 30;

/**
 * One page of `rows`, as the API pages them: newest first, then by ID, the page after the review at `before` and
 * `beforeId` if both are given, with whether older ones remain.
 */
export function reviewedPage(rows: readonly ReviewedRow[], before: string | null, beforeId: string | null) {
  const newestFirst = [...rows].sort(
    (a, b) => b.reviewedAtUtc.localeCompare(a.reviewedAtUtc) || b.reviewId.localeCompare(a.reviewId),
  );
  const after =
    before === null || beforeId === null
      ? newestFirst
      : newestFirst.filter(
          (row) => row.reviewedAtUtc < before || (row.reviewedAtUtc === before && row.reviewId < beforeId),
        );
  return { items: after.slice(0, reviewedPageSize), hasMore: after.length > reviewedPageSize };
}

/** Whether a request is for the Reviewed list, `GET /api/reviews?status=reviewed`. */
export const isReviewedList = (url: URL) =>
  url.pathname === '/api/reviews' && url.searchParams.get('status') === 'reviewed';

/**
 * Answers each `GET /api/reviews?status=reviewed` that the page sends from now on with `rows`, 30 at a time, the page
 * after the review that `before` and `beforeId` name, in the browser, so that the request never reaches the app. A
 * later call overrides it, as Playwright tries the latest route first.
 */
export async function serveReviewed(page: Page, rows: readonly ReviewedRow[]): Promise<void> {
  await page.route(isReviewedList, (route) => {
    const { searchParams } = new URL(route.request().url());
    return route.fulfill({ json: reviewedPage(rows, searchParams.get('before'), searchParams.get('beforeId')) });
  });
}

/**
 * Aborts each `GET /api/reviews?status=reviewed` that the page sends from now on, in the browser, as a phone with no
 * signal would; with `olderOnly`, only each page after the first, which Show older asks for with `before`. A failure of
 * the first load shows about 1 s after the request, after the query client's silent retry. A later `serveReviewed`
 * overrides it.
 */
export async function failReviewed(page: Page, { olderOnly = false } = {}): Promise<void> {
  await page.route(
    (url) => isReviewedList(url) && (!olderOnly || url.searchParams.has('before')),
    (route) => route.abort('internetdisconnected'),
  );
}
