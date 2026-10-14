import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/** A participant's status as the API gives it, its `ParticipantStatus` name. */
export type ParticipantStatus = 'Active' | 'Archived';

/** A participant as `GET /api/admin/participants` lists them (design.md §6.6), the whole record. */
export type AdminParticipant = {
  id: string;
  /** As typed, with only its ends trimmed, never re-cased (A5). */
  givenName: string;
  familyName: string;
  /** A calendar date, such as "1987-03-07". */
  dateOfBirth: string;
  status: ParticipantStatus;
  /** The row's `RowVersion` as base64, which a change to the participant sends as `If-Match` (D78). */
  rowVersion: string;
};

/**
 * The `['admin', 'participants']` query, the Participants screens' one (participants.md A4 and B9): every participant
 * of the manager's organisation, archived ones too, so that the Active and Archived views are one list filtered in
 * memory and switching between them never loads. The API sorts them by family name, then given name (A5), and the
 * screens keep that order. Fetched again on mount, as TanStack's `staleTime: 0` has it, but not on window focus, which
 * app-shell.md does not list it for.
 */
export const participantsQuery = queryOptions({
  queryKey: ['admin', 'participants'],
  queryFn: ({ signal }) => api<AdminParticipant[]>('/api/admin/participants?includeArchived=true', { signal }),
});

/** A participant's full name as stored, given name first, "Jane Citizen" (design.md §3.9), never re-cased. */
export function fullName({ givenName, familyName }: Pick<AdminParticipant, 'givenName' | 'familyName'>): string {
  return `${givenName} ${familyName}`;
}
