import { queryOptions, type QueryClient } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/** A participant on the Today list (design.md §6.3): their ID and their names as stored. T09 adds today's note. */
export type TodayParticipant = {
  id: string;
  /** As typed, with only its ends trimmed, never re-cased (A5). */
  givenName: string;
  familyName: string;
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
 * Fetches the Today list that shows again, as TanStack does as the page becomes visible, sharing a request already
 * out, so that the window's `focus` and `visibilitychange`, which come together as a person returns to the tab, send
 * one request between them.
 */
export function refetchToday(queryClient: QueryClient): Promise<void> {
  return queryClient.refetchQueries({ queryKey: todayKey, type: 'active' }, { cancelRefetch: false });
}
