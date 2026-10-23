import { queryOptions, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useCallback, useSyncExternalStore } from 'react';
import { api } from './api.ts';

/**
 * A user's role as the API gives it, the role's `UserRole` name (user-management.md's `Role`), so that a comparison
 * with any other spelling, such as `'manager'`, does not compile.
 */
export type Role = 'Worker' | 'Manager';

/** The signed-in user (design.md §6.2), as `GET /api/auth/me` and the calls that sign someone in answer. */
export type Me = {
  userId: string;
  displayName: string;
  role: Role;
  organisationName: string;
  /** The date in Melbourne on the server, such as "2026-10-01", which every "today" in the SPA uses (A33). */
  today: string;
  /** The notes waiting for review, for the Flagged item's badge; sent to a manager only (design.md §6.2). */
  toReviewCount?: number;
};

/**
 * The `['me']` query, which `refreshMe()` alone asks for, when the shell decides (app-shell.md *Refresh of
 * `/api/auth/me`*): no observer fetches it by itself, as one that mounted while it held no answer would. `staleTime:
 * Infinity` keeps TanStack's own refetches away from it; the query client's defaults give it one silent retry for no
 * connection or a `5xx`, and none for a `4xx`.
 */
export const meQuery = queryOptions({
  queryKey: ['me'],
  // Without TanStack's signal, as TanStack aborts a request whose query function took it when the query's last
  // observer goes. The shell's goes and comes back while StrictMode mounts it in development, and its second start-up
  // refresh then shares the first request rather than sending another.
  queryFn: () => api<Me>('/api/auth/me'),
  enabled: false,
  staleTime: Infinity,
});

/**
 * Sends `GET /api/auth/me` for the `['me']` query, the one way the app does, on the query client that the caller has
 * from `useQueryClient()`, and gives the request's promise. A request already in flight is shared, so that triggers
 * that fire together send one. The promise rejects when the request fails, and the query keeps the answer it had.
 */
export function refreshMe(queryClient: QueryClient): Promise<Me> {
  return queryClient.fetchQuery({ ...meQuery, staleTime: 0 });
}

/**
 * Puts the `Me` that a sign-in or a setup answered into the `['me']` query (sign-in.md *Interactions* 5). A refresh
 * still in flight is cancelled first, TanStack's order for a write by hand: it went with the cookie from before, maybe
 * another person's, and its answer, landing after this one, would put that person back on screen.
 */
export async function setMe(queryClient: QueryClient, me: Me): Promise<void> {
  // Exact, so that a query under it, such as Today's `['me', 'drafts']`, is left alone.
  await queryClient.cancelQueries({ queryKey: meQuery.queryKey, exact: true });
  queryClient.setQueryData(meQuery.queryKey, me);
}

/**
 * How many answers `['me']` has taken, a refresh's or a sign-in's, so that the shell can tell a new one from the one
 * it held: an answer equal to the one held leaves the query's data the same object, and two can come in the same
 * millisecond, so neither the data nor `dataUpdatedAt` tells.
 */
export function useMeAnswers(): number {
  const queryClient = useQueryClient();
  const subscribe = useCallback(
    (onChange: () => void) => queryClient.getQueryCache().subscribe(onChange),
    [queryClient],
  );
  return useSyncExternalStore(subscribe, () => queryClient.getQueryState(meQuery.queryKey)?.dataUpdateCount ?? 0);
}

/**
 * The signed-in user, for a screen inside the shell, which shows its screens only once `['me']` holds an answer, and
 * keeps that answer through a refresh that fails.
 */
export function useMe(): Me {
  const { data } = useQuery(meQuery);
  if (data === undefined) {
    throw new Error('A screen rendered before GET /api/auth/me answered.');
  }

  return data;
}
