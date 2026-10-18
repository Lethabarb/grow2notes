import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import type { ListApi, ListRow } from '../../components/ListEditor.tsx';

/**
 * The path of a participant's goals, which the list reads and an add posts to, and under which their order is put
 * (design.md §6.6).
 */
function goalsPath(participantId: string): string {
  return `/api/admin/participants/${encodeURIComponent(participantId)}/goals`;
}

/** The path of a goal, which a reword puts to, and under which it is archived and restored (design.md §6.6). */
function goalPath(goalId: string): string {
  return `/api/admin/goals/${encodeURIComponent(goalId)}`;
}

/**
 * The `['admin', 'goals', participantId]` query, a participant's page's second (participants.md B9): every goal of the
 * participant, archived ones too, in the API's order. Outside `['admin', 'participants']`, so that an invalidation of
 * the participant list fetches no goals; and keyed by the participant's ID alone, never by a goal's words. Fetched
 * again on mount, as TanStack's `staleTime: 0` has it, but not on window focus, which app-shell.md does not list it
 * for.
 */
export function goalsQuery(participantId: string) {
  return queryOptions({
    queryKey: ['admin', 'goals', participantId],
    queryFn: ({ signal }) => api<ListRow[]>(`${goalsPath(participantId)}?includeArchived=true`, { signal }),
  });
}

/**
 * `ListEditor`'s way to a participant's goals (list-editor.md *Implementation notes*): their query, whose data is the
 * goals themselves; the one scope of every write on them, `goals:<participantId>` (participants.md B6); the add,
 * whose `201` has no body; a goal's reword, with its `rowVersion` as `If-Match`, whose `200` has none either, and
 * whose `ETag` the editor does not read, as it reads each goal's next version from the list; the reorder of the
 * active goals, with no `If-Match`, as the set of their IDs is its check (design.md §6.7), whose `204` has no body;
 * and a goal's archive and restore, with no body and its `rowVersion` as `If-Match`, whose `204`s have none.
 */
export function goalsApi(participantId: string): ListApi<ListRow[]> {
  return {
    query: goalsQuery(participantId),
    rowsOf: (goals) => goals,
    scopeId: `goals:${participantId}`,
    add: (text) => api<undefined>(goalsPath(participantId), { method: 'POST', body: { text } }),
    update: (id, text, rowVersion) =>
      api<undefined>(goalPath(id), { method: 'PUT', body: { text }, ifMatch: rowVersion }),
    reorder: (goalIds) => api<undefined>(`${goalsPath(participantId)}/order`, { method: 'PUT', body: { goalIds } }),
    archive: (id, rowVersion) => api<undefined>(`${goalPath(id)}/archive`, { method: 'POST', ifMatch: rowVersion }),
    restore: (id, rowVersion) => api<undefined>(`${goalPath(id)}/restore`, { method: 'POST', ifMatch: rowVersion }),
  };
}
