import { infiniteQueryOptions, type InfiniteData, type QueryClient } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/**
 * A review on the Reviewed list, as `GET /api/reviews?status=reviewed` gives it to a manager (design.md §6.4): the
 * review's ID, which is the row's key and the page's tie-break; its note's participant's ID and names as stored; the
 * note's date; its author's display name as stored; the reason of the flag that the review cleared, whole; the
 * reviewer's display name as stored; when it was reviewed, in UTC, written with `Z`; and its comment, as written, or
 * `null`.
 */
export type ReviewedNote = {
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

/** A page of the Reviewed list: up to 30 reviews, newest first, and whether older ones remain. */
export type ReviewedPage = { items: ReviewedNote[]; hasMore: boolean };

/** Where the next page starts: after the last review's time and ID, as the server's order breaks a tie by ID. */
type Cursor = { before: string; beforeId: string };

// Every page loaded stays in memory while the manager reads a note opened from them, as the session's idle limit is 30
// minutes (chronological-list.md *Implementation notes*), so that Back shows them all again.
const keptFor = 30 * 60_000;

// One function, so that TanStack keeps its result while the pages stay the same.
const rowsOf = (data: InfiniteData<ReviewedPage, Cursor | undefined>) => data.pages.flatMap((page) => page.items);

/**
 * The `['reviews', 'reviewed']` query (flagged.md component 6): the reviews, 30 at a time, newest first, the first page
 * with no cursor and each next page after the last review's `reviewedAtUtc`, as the server gave it, and its
 * `reviewId`, while the last page said `hasMore`; given as the rows of every page loaded. Every loaded page is fetched
 * again in order when the page becomes visible, as Past notes' are; with no `maxPages`, none is dropped while it is
 * open. Under `['reviews']` too, so that a review or an edit marks it stale with the To review list.
 */
export const reviewedQuery = infiniteQueryOptions({
  queryKey: ['reviews', 'reviewed'],
  queryFn: ({ pageParam, signal }) =>
    api<ReviewedPage>(
      `/api/reviews?status=reviewed${
        pageParam === undefined ? '' : `&before=${encodeURIComponent(pageParam.before)}&beforeId=${pageParam.beforeId}`
      }`,
      { signal },
    ),
  initialPageParam: undefined as Cursor | undefined,
  getNextPageParam: (last): Cursor | undefined => {
    const item = last.items.at(-1);
    return last.hasMore && item !== undefined ? { before: item.reviewedAtUtc, beforeId: item.reviewId } : undefined;
  },
  select: rowsOf,
  staleTime: 0,
  gcTime: keptFor,
  refetchOnWindowFocus: true,
});

/**
 * Drops every page of the Reviewed list that an earlier visit loaded, so that a new visit starts from the newest 30 at
 * the top.
 */
export function dropReviewed(queryClient: QueryClient): void {
  queryClient.removeQueries({ queryKey: reviewedQuery.queryKey });
}
