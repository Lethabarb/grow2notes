import { infiniteQueryOptions, queryOptions, type InfiniteData, type QueryClient } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import type { FlagStatus, ParticipantHeader } from './noteQueries.ts';

/**
 * A live note on a participant's history, as `GET /api/participants/{participantId}/notes` gives it to anyone signed
 * in (design.md §6.3): its date, its status, its author's display name as stored, its current version's number and
 * whether it has more than one, and whether that version is flagged, which a draft never is. A draft has no version
 * (0) and is never edited. A manager's rows also hold `flagStatus` on a submitted note, which a worker's never do.
 */
export type PastNote = { noteDate: string; authorDisplayName: string; currentVersionNumber: number } & (
  | { status: 'Draft'; isEdited: false; isFlagged: false }
  | { status: 'Submitted'; isEdited: boolean; isFlagged: boolean; flagStatus?: FlagStatus }
);

/** A page of the history (design.md §6.1 *Lists*): up to 30 live notes, newest first, and whether older ones
 * remain. */
type PastNotesPage = { notes: PastNote[]; hasMore: boolean };

// Every page loaded stays in memory while the person reads a note opened from them, as the session's idle limit is 30
// minutes (chronological-list.md *Implementation notes*), so that Back shows them all again.
const keptFor = 30 * 60_000;

// What every query of a participant's Past notes starts with: its pages and its header.
const pastNotesKey = (participantId: string) => ['pastNotes', participantId] as const;

// The rows of every page loaded, in the API's order: one function, so that TanStack keeps its result while the pages
// stay the same.
const rowsOf = (data: InfiniteData<PastNotesPage, string | undefined>) => data.pages.flatMap((page) => page.notes);

/** A row's stable key, its note's date, as there is one live note per participant and day (design.md §5.5 item 1). */
export function noteDateOf(note: PastNote): string {
  return note.noteDate;
}

/**
 * The `['pastNotes', participantId]` query (participant-notes.md component 5): the history, 30 at a time, the first
 * page with no `before`, as the server's default, tomorrow in Melbourne, starts at today's note, and each next page
 * before the last row's date, while the last page said `hasMore`; given as the rows of every page loaded, in the API's
 * order. Every loaded page is fetched again in order, silently, when the window regains focus, as app-shell.md lists
 * this query among the opt-ins to `refetchOnWindowFocus`; with no `maxPages`, no page is ever dropped while it is open.
 */
export function pastNotesQuery(participantId: string) {
  return infiniteQueryOptions({
    queryKey: pastNotesKey(participantId),
    queryFn: ({ pageParam, signal }) =>
      api<PastNotesPage>(
        `/api/participants/${encodeURIComponent(participantId)}/notes${
          pageParam === undefined ? '' : `?before=${encodeURIComponent(pageParam)}`
        }`,
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => (last.hasMore ? last.notes.at(-1)?.noteDate : undefined),
    select: rowsOf,
    gcTime: keptFor,
    refetchOnWindowFocus: true,
  });
}

/**
 * The participant over their Past notes, `GET /api/participants/{participantId}`, kept with the pages, so that Back
 * shows the heading and every row at once; a query of its own, as the note's page reads the same header afresh on
 * every visit (`participantQuery`).
 */
export function pastNotesHeaderQuery(participantId: string) {
  return queryOptions({
    queryKey: [...pastNotesKey(participantId), 'participant'],
    queryFn: ({ signal }) =>
      api<ParticipantHeader>(`/api/participants/${encodeURIComponent(participantId)}`, { signal }),
    gcTime: keptFor,
  });
}

/**
 * Drops every page and the header of `participantId`'s Past notes that an earlier visit loaded, so that a new visit
 * starts from the newest 30 at the top (participant-notes.md item 14).
 */
export function dropPastNotes(queryClient: QueryClient, participantId: string): void {
  queryClient.removeQueries({ queryKey: pastNotesKey(participantId) });
}

/**
 * Marks `participantId`'s Past notes stale after a Discard (note-form.md *Discard draft*), so that a pop back to them
 * fetches every page loaded again and lists the draft no more. Exact, so that their header, the name, is left alone.
 */
export function invalidatePastNotes(queryClient: QueryClient, participantId: string): Promise<void> {
  return queryClient.invalidateQueries({ queryKey: pastNotesKey(participantId), exact: true });
}
