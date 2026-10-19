import { queryOptions, type QueryClient } from '@tanstack/react-query';
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
 * Marks every Today list stale, so that Today fetches its list again as it next shows, after a change that Today
 * shows: a participant added, corrected, archived or restored (participants.md *Interactions*).
 */
export function invalidateToday(queryClient: QueryClient): Promise<void> {
  return queryClient.invalidateQueries({ queryKey: todayKey });
}

/**
 * Drops every Today list after Submit or Discard (note-form.md *After a successful submit* and *Discard draft*), so
 * that Today, which shows next, fetches its list afresh and shows the row only as the server gives it, never a row
 * built here or kept from before, which could still say "Draft" (today.md *Conflicts resolved* 2).
 */
export function removeToday(queryClient: QueryClient): void {
  queryClient.removeQueries({ queryKey: todayKey });
}

// The caller's own unfinished drafts, which Today lists under Your unfinished drafts (today.md component 5).
const myDraftsKey = ['me', 'drafts'] as const;

/**
 * Drops the caller's unfinished drafts after a Discard (note-form.md *Discard draft*), beside Today's lists, so that
 * Today fetches them afresh and never lists the draft discarded.
 */
export function removeMyDrafts(queryClient: QueryClient): void {
  queryClient.removeQueries({ queryKey: myDraftsKey });
}

/**
 * Fetches the Today list that shows again, as TanStack does as the page becomes visible, sharing a request already
 * out, so that the window's `focus` and `visibilitychange`, which come together as a person returns to the tab, send
 * one request between them.
 */
export function refetchToday(queryClient: QueryClient): Promise<void> {
  return queryClient.refetchQueries({ queryKey: todayKey, type: 'active' }, { cancelRefetch: false });
}
