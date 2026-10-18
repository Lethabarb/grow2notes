import { queryOptions } from '@tanstack/react-query';
import { api, ApiError, type StartedBy } from '../../api/api.ts';
import type { ParticipantStatus } from '../participants/participantsQuery.ts';

/** A participant's header, as `GET /api/participants/{participantId}` gives it to anyone signed in (design.md §6.3). */
export type ParticipantHeader = {
  id: string;
  /** As typed, with only its ends trimmed, never re-cased (A5). */
  givenName: string;
  familyName: string;
  status: ParticipantStatus;
};

/** A goal on the note form: its wording as the form shows it, and whether it is ticked as reached. */
export type DraftGoal = { id: string; text: string; isTicked: boolean };

/**
 * The note form with the caller's working copy, as `GET {base}/draft` answers it (design.md §6.3): a new note's
 * template, with `isSaved` false, the lists' version and no flag, or the caller's own draft, with its flag and the time
 * of its last save. `guidePrompts` are the organisation's as they are now; `flagReason` is `null` unless the draft is
 * flagged with a reason.
 */
export type Draft = {
  baseVersion: number;
  isSaved: boolean;
  listsVersion?: string;
  guidePrompts: string;
  goals: DraftGoal[];
  narrative: string;
  isFlagged: boolean;
  flagReason: string | null;
  savedAtUtc?: string;
};

/**
 * What opening the note found: the form, or another user's draft, which only its author may open
 * (`409 note.in_progress_by_other`, design.md §6.3), and which the page shows rather than as a failure.
 */
export type DraftAnswer = { draft: Draft } | { startedBy: StartedBy };

/** `{base}`, the address of a participant's note for a day (design.md §5.5 item 1, §6.3). */
export function noteBase(participantId: string, noteDate: string): string {
  return `/api/participants/${encodeURIComponent(participantId)}/notes/${encodeURIComponent(noteDate)}`;
}

/**
 * Reads the note form with the caller's working copy, `GET {base}/draft`: as the form opens, and again when a first
 * save finds the lists changed (design.md §5.7). Another user's draft is an answer, not a failure.
 */
export async function readDraft(participantId: string, noteDate: string, signal?: AbortSignal): Promise<DraftAnswer> {
  try {
    return { draft: await api<Draft>(`${noteBase(participantId, noteDate)}/draft`, { signal }) };
  } catch (error) {
    if (error instanceof ApiError && error.startedBy !== undefined) {
      return { startedBy: error.startedBy };
    }

    throw error;
  }
}

/**
 * The `['participant', participantId]` query: the name over the note (note-identity-header.md). Fetched afresh as the
 * form opens, as `gcTime: 0` drops it once the form has gone, so that no name kept from before ever shows, and never
 * again while it is open.
 */
export function participantQuery(participantId: string) {
  return queryOptions({
    queryKey: ['participant', participantId],
    queryFn: ({ signal }) =>
      api<ParticipantHeader>(`/api/participants/${encodeURIComponent(participantId)}`, { signal }),
    staleTime: Infinity,
    gcTime: 0,
  });
}

/**
 * The `['noteDraft', participantId, noteDate]` query: the note form (note-form.md *Autosave*), which the form copies
 * into its own state once. Fetched afresh as the form opens, as `gcTime: 0` drops it once the form has gone, and never
 * again while it is open, on focus or on reconnecting either, so that nothing replaces what is on screen.
 */
export function draftQuery(participantId: string, noteDate: string) {
  return queryOptions({
    queryKey: ['noteDraft', participantId, noteDate],
    queryFn: ({ signal }) => readDraft(participantId, noteDate, signal),
    staleTime: Infinity,
    gcTime: 0,
    refetchOnReconnect: false,
  });
}
