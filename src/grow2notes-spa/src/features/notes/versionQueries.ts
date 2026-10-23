import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import { strings } from '../../copy/strings.ts';
import { noteBase, type NoteAuthor, type SubmittedNote } from './noteQueries.ts';

/**
 * A row of the version list, as `GET {base}/versions` gives it to a manager (design.md §6.3): the version's number, who
 * saved it and when, in UTC. The API also says whether the version is flagged, which no screen reads (design.md D71).
 */
export type VersionSummary = { versionNumber: number; createdBy: NoteAuthor; createdAtUtc: string };

/** One version in full, as `GET {base}/versions/{n}` gives it: the note's `current` shape, and who saved it and when. */
export type NoteVersion = SubmittedNote['current'] & { createdBy: NoteAuthor; createdAtUtc: string };

// Newest first, whatever order the API gives (version-history.md item 9): one function, so that TanStack keeps its
// result while the rows stay the same.
const newestFirst = (versions: VersionSummary[]) => [...versions].sort((a, b) => b.versionNumber - a.versionNumber);

/**
 * The `['notes', participantId, noteDate, 'versions']` query (version-history.md *Data and queries*): every version of
 * the note, newest first, so that the first row is the current version, the screen's only source for it. Under the
 * note's prefix, so that a Save changes, which drops the prefix, and a review, which marks it stale, refresh it. It is
 * fetched again as the page opens and when the window regains focus, so that a version that another manager has just
 * saved shows without a reload.
 */
export function versionsQuery(participantId: string, noteDate: string) {
  return queryOptions({
    queryKey: ['notes', participantId, noteDate, 'versions'],
    queryFn: ({ signal }) => api<VersionSummary[]>(`${noteBase(participantId, noteDate)}/versions`, { signal }),
    select: newestFirst,
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
}

/**
 * The `['notes', participantId, noteDate, 'versions', versionNumber]` query: one version, which is never fetched again
 * once it has loaded, as a version is insert-only (design.md §5.8) and can never change under a reader.
 */
export function versionQuery(participantId: string, noteDate: string, versionNumber: number) {
  return queryOptions({
    queryKey: ['notes', participantId, noteDate, 'versions', versionNumber],
    queryFn: ({ signal }) =>
      api<NoteVersion>(`${noteBase(participantId, noteDate)}/versions/${versionNumber}`, { signal }),
    staleTime: 'static',
  });
}

/**
 * Who saved a version, for its row and its page: the name as stored, and " (manager)" after anyone but the note's
 * author, whose current role is never asked (version-history.md item 9).
 */
export function saverOf(createdBy: NoteAuthor, author: NoteAuthor): string {
  return createdBy.displayName + (createdBy.id === author.id ? '' : strings.versionHistory.managerSuffix);
}
