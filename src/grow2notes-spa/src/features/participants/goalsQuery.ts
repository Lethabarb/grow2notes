import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import type { ListApi, ListRow } from '../../components/ListEditor.tsx';

/** The path of a participant's goals, which the list reads and an add posts to (design.md §6.6). */
function goalsPath(participantId: string): string {
  return `/api/admin/participants/${encodeURIComponent(participantId)}/goals`;
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
 * `ListEditor`'s way to a participant's goals (list-editor.md *Implementation notes*): their query; the one scope of
 * every write on them, `goals:<participantId>` (participants.md B6); and the add, whose `201` has no body.
 */
export function goalsApi(participantId: string): ListApi {
  return {
    query: goalsQuery(participantId),
    scopeId: `goals:${participantId}`,
    add: (text) => api<undefined>(goalsPath(participantId), { method: 'POST', body: { text } }),
  };
}
