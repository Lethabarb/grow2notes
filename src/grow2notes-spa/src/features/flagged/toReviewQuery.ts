import { queryOptions, type QueryClient } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/**
 * A note on the To review list, as `GET /api/reviews?status=toReview` gives it to a manager (design.md §6.4): its
 * participant's ID and names as stored; its date; its author's display name as stored; the reason, whole, and the time
 * of its flagged version, in UTC, written with `Z`; and whether a later edit removed the flag. The answer also holds
 * the note's `currentVersionNumber`, which no screen reads.
 */
export type ToReviewNote = {
  participantId: string;
  givenName: string;
  familyName: string;
  noteDate: string;
  authorDisplayName: string;
  flagReason: string;
  flaggedAtUtc: string;
  flagRemovedLater: boolean;
};

// The rows stay in memory while the manager reads a note opened from them, as the session's idle limit is 30 minutes
// (chronological-list.md *Implementation notes*), so that Back shows them at once, as Past notes keeps its pages.
const keptFor = 30 * 60_000;

const toReviewKey = ['reviews', 'toReview'] as const;

/**
 * The `['reviews', 'toReview']` query (flagged.md item 4 and component 6): the whole list, oldest flag first, in the
 * API's order. Fetched again on every mount, as `staleTime: 0` has it, and as the page becomes visible, as app-shell.md
 * lists it among the opt-ins to `refetchOnWindowFocus`, which the query client turns off; Flagged adds the window's
 * `focus` through `refetchToReview()`, as the badge refreshes on both (flagged.md *Interactions and focus*).
 */
export const toReviewQuery = queryOptions({
  queryKey: toReviewKey,
  queryFn: ({ signal }) => api<ToReviewNote[]>('/api/reviews?status=toReview', { signal }),
  staleTime: 0,
  gcTime: keptFor,
  refetchOnWindowFocus: true,
});

/**
 * Fetches the To review list that shows again, as TanStack does as the page becomes visible, sharing a request already
 * out, so that the window's `focus` and `visibilitychange`, which come together as a person returns to the tab, send
 * one request between them.
 */
export function refetchToReview(queryClient: QueryClient): Promise<void> {
  return queryClient.refetchQueries({ queryKey: toReviewKey, type: 'active' }, { cancelRefetch: false });
}
