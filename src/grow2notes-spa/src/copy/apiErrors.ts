import { stamp, type Ymd } from './format.ts';

/**
 * The API's error codes that the app words (design.md §6.9). A story that words a code adds it here, with its words
 * below, as the server's `ErrorCode` holds only the codes the app answers.
 */
export type ApiErrorCode =
  | 'auth.setup_link_invalid'
  | 'note.in_progress_by_other'
  | 'note.lists_changed'
  | 'note.participant_archived'
  | 'note.past_date_manager_only'
  | 'user.email_in_use';

/** What the in-progress sentence names: the server's author and start time, and what the note form knows. */
type InProgress = {
  authorDisplayName: string;
  startedAtUtc: string;
  participantName: string;
  noteDate: Ymd;
  /** Whether the note date is not before the server's Melbourne today, which "today's note" then names. */
  onToday: boolean;
};

/**
 * The words for each code (microcopy.md §8), in the shape its screen shows them. Checked against the union rather than
 * typed as one record, so that a code in the union with no words fails the type check while each entry keeps its own
 * shape.
 */
export const apiErrorCopy = {
  /** sign-in.md's *Link expired, used or replaced*: a whole-page message, design.md §4.1's sentence split in two. */
  'auth.setup_link_invalid': { heading: 'This link has expired', body: 'Ask a manager to send a new one.' },
  /**
   * Someone else's draft (note-form.md *States*), with the author's display name in both places, never a first name
   * made from it (microcopy.md): "today's note", or "the note" for an earlier day's (open question 2), started at a
   * time that has its date only when that was another day than the note's (microcopy.md §3, *Reference day*).
   */
  'note.in_progress_by_other': ({ authorDisplayName, startedAtUtc, participantName, noteDate, onToday }: InProgress) =>
    `${authorDisplayName} started ${onToday ? 'today’s note' : 'the note'} for ${participantName} at `
    + `${stamp(startedAtUtc, noteDate)}. Only ${authorDisplayName} can finish it.`,
  /**
   * The lists-changed notice above Goals (note-form.md component 7), as the note form shows the lists again after its
   * first save found them changed: design.md §4.3's words, word for word (microcopy.md §1).
   */
  'note.lists_changed': 'The goal or common-item list was just changed. Please check your ticks.',
  /** The archived dead end in the note form's top bar (note-form.md *Conflicts and refusals*), with the full name. */
  'note.participant_archived': (participantName: string) =>
    `${participantName} is archived, so this note cannot be started. Ask a manager.`,
  /**
   * The after-midnight dead end in the note form's top bar (note-form.md *Conflicts and refusals*): design.md §3.3's
   * words, its contraction included, for a worker's first save of a note for a day before today.
   */
  'note.past_date_manager_only':
    'It’s now after midnight, so this note can’t be started for yesterday. Ask a manager to record it.',
  /** The Email address field's message on Invite user (users.md *States*, microcopy.md's words). */
  'user.email_in_use':
    'Another account already uses this email address. Use a different one, or find the person in the Users list.',
} as const satisfies Record<ApiErrorCode, unknown>;
