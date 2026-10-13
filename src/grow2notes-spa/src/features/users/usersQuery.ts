import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import type { Role } from '../../api/me.ts';

/** A user's status as the API gives it, its `UserStatus` name. */
export type UserStatus = 'Invited' | 'Active' | 'Deactivated';

/** A user as `GET /api/admin/users` lists them (design.md §6.6). */
export type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  status: UserStatus;
  /** In UTC, with `Z`, as are the two times after it. */
  invitedAtUtc: string;
  activatedAtUtc: string | null;
  deactivatedAtUtc: string | null;
  /** The user's Identity `ConcurrencyStamp`, which a change to the user sends as `If-Match` (D78). */
  concurrencyStamp: string;
};

/**
 * The `['admin', 'users']` query, the Users screens' one (users.md *E. Data and helpers*): every user of the manager's
 * organisation, as the list is returned whole (design.md §6.1). Fetched again on mount and on window focus (§6.8), as
 * app-shell.md lists it among the opt-ins to `refetchOnWindowFocus`, which the query client turns off; `staleTime: 0`
 * is TanStack's default, set so that it stays.
 */
export const usersQuery = queryOptions({
  queryKey: ['admin', 'users'],
  queryFn: ({ signal }) => api<AdminUser[]>('/api/admin/users', { signal }),
  staleTime: 0,
  refetchOnWindowFocus: true,
});
