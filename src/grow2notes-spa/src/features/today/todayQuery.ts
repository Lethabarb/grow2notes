import { partialMatchKey, queryOptions, type Query, type QueryClient } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/**
 * A participant's live note for the list's date, as their row shows it (design.md §4.2 item 4, §6.3): its author's
 * display name as stored, and whether the signed-in user is its author; when it was started; on their own draft alone,
 * when they last saved it; on a submitted note, when it was first submitted; and whether its current version is
 * flagged, which a draft never is. Each time is in UTC, written with `Z`.
 */
export type TodayNote = { authorDisplayName: string; startedAtUtc: string; isFlagged: boolean } & (
  | { status: 'Draft'; isMine: true; savedAtUtc: string }
  | { status: 'Draft'; isMine: false }
  | { status: 'Submitted'; isMine: boolean; firstSubmittedAtUtc: string }
);

/** A participant on the Today list (design.md §6.3): their ID, their names as stored, and their note, if any. */
export type TodayParticipant = {
  id: string;
  /** As typed, with only its ends trimmed, never re-cased (A5). */
  givenName: string;
  familyName: string;
  note: TodayNote | null;
};

/** The Today list, as `GET /api/today` answers it (design.md §6.3). */
export type TodayList = {
  /** The date in Melbourne on the server as it answered, such as "2026-10-01", which the rows' links name. */
  date: string;
  /** The organisation's active participants, sorted by family name, then given name (A5), which Today keeps. */
  participants: TodayParticipant[];
};

// What every Today list's key starts with, whatever its date.
const todayKey = ['today'] as const;

/**
 * The `['today', date]` query (today.md component 4), for `me.today`, so that a new day in Melbourne loads that day's
 * list rather than show the last day's (today.md *Conflicts resolved* 14). Fetched again on mount, as `staleTime: 0`
 * has it, and as the page becomes visible, as app-shell.md lists it among the opt-ins to `refetchOnWindowFocus`,
 * which the query client turns off; Today adds the window's `focus` through `refetchToday()`.
 */
export function todayQuery(date: string) {
  return queryOptions({
    queryKey: [...todayKey, date],
    queryFn: ({ signal }) => api<TodayList>('/api/today', { signal }),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
}

/**
 * One of the signed-in user's own working copies of a live note, as `GET /api/me/drafts` lists it (design.md §6.3):
 * its participant's ID and names as stored, whether or not they are archived since; its note's date; and whether it is
 * a draft or a pending edit on a submitted note, which tells its row's address (note-form.md *Routes*). The answer
 * also holds when it was last saved, `savedAtUtc`, which no screen reads.
 */
export type MyDraft = {
  participantId: string;
  givenName: string;
  familyName: string;
  noteDate: string;
  kind: 'draft' | 'pendingEdit';
};

const myDraftsKey = ['me', 'drafts'] as const;

/**
 * The `['me', 'drafts']` query, the caller's unfinished drafts, in no promised order, from which Today builds Your
 * unfinished drafts (today.md component 5). Fetched again on mount, and with the day's list as the page becomes
 * visible and on the window's `focus`, through `refetchToday()` (today.md *Refresh*).
 */
export const myDraftsQuery = queryOptions({
  queryKey: myDraftsKey,
  queryFn: ({ signal }) => api<MyDraft[]>('/api/me/drafts', { signal }),
  staleTime: 0,
  refetchOnWindowFocus: true,
});

/** Whether `query` is one of Today's lists: a day's, whatever its date, or the caller's unfinished drafts. */
function isTodays({ queryKey }: Query): boolean {
  return partialMatchKey(queryKey, todayKey) || partialMatchKey(queryKey, myDraftsKey);
}

/**
 * Marks Today's lists stale, every day's and the caller's unfinished drafts, so that Today fetches them again as it
 * next shows, after a change that Today shows: a participant added, corrected, archived or restored (participants.md
 * *Interactions*), whose name a draft's row shows too, and a note form left, however it went.
 */
export function invalidateToday(queryClient: QueryClient): Promise<void> {
  return queryClient.invalidateQueries({ predicate: isTodays });
}

/**
 * Drops Today's lists, every day's and the caller's unfinished drafts, after Submit or Discard (note-form.md *After a
 * successful submit* and *Discard draft*), so that Today, which shows next, fetches them afresh and shows the row only
 * as the server gives it, never a row built here or kept from before, which could still say "Draft", or a draft
 * under Your unfinished drafts that is one no longer (today.md *Conflicts resolved* 2).
 */
export function removeToday(queryClient: QueryClient): void {
  queryClient.removeQueries({ predicate: isTodays });
}

/**
 * Fetches Today's lists that show again, as TanStack does as the page becomes visible, sharing a request already out,
 * so that the window's `focus` and `visibilitychange`, which come together as a person returns to the tab, send one
 * request for each list between them.
 */
export function refetchToday(queryClient: QueryClient): Promise<void> {
  return queryClient.refetchQueries({ predicate: isTodays, type: 'active' }, { cancelRefetch: false });
}
