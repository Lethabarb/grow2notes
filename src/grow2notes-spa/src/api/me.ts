import { queryOptions } from '@tanstack/react-query';
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
};

/**
 * The `['me']` query. `staleTime: Infinity` keeps TanStack's own refetches away from it, as the shell decides when to
 * ask again (app-shell.md *Refresh*); the query client's defaults give it one silent retry for no connection or a
 * `5xx`, and none for a `4xx`.
 */
export const meQuery = queryOptions({
  queryKey: ['me'],
  queryFn: ({ signal }) => api<Me>('/api/auth/me', { signal }),
  staleTime: Infinity,
});
