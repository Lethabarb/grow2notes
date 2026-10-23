import { stamp, type Ymd } from './format.ts';

/**
 * The API's error codes that the app words (design.md §6.9). A story that words a code adds it here, with its words
 * below, as the server's `ErrorCode` holds only the codes the app answers.
 */
export type ApiErrorCode =
  | 'auth.setup_link_invalid'
  | 'draft.taken_over'
  | 'note.in_progress_by_other'
  | 'note.lists_changed'
  | 'note.not_a_draft'
  | 'note.participant_archived'
  | 'note.past_date_manager_only'
  | 'note.version_conflict'
  | 'review.not_current'
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
   * The takeover banner in the note form's top bar (note-form.md *Conflicts and refusals*), as another page has saved
   * the note since this page last did (design.md §5.6): design.md §4.3's words, word for word.
   */
  'draft.taken_over': 'This note was changed on another device or tab.',
  /**
   * Someone else's draft (note-form.md *States*), with the author's display name in both places, never a first name
   * made from it (microcopy.md): "today's note", or "the note" for an earlier day's (open question 2), started at a
   * time that has its date only when that was another day than the note's (design.md A64), as microcopy.md §3 has no
   * format for a date and time inside a sentence.
   */
  'note.in_progress_by_other': ({ authorDisplayName, startedAtUtc, participantName, noteDate, onToday }: InProgress) =>
    `${authorDisplayName} started ${onToday ? 'today’s note' : 'the note'} for ${participantName} at `
    + `${stamp(startedAtUtc, noteDate)}. Only ${authorDisplayName} can finish it.`,
  /**
   * The lists-changed notice above Goals (note-form.md component 7), as the note form shows the lists again after its
   * first save found them changed: design.md §4.3's words, word for word (microcopy.md §1).
   */
  'note.lists_changed': 'The goal or common-item list was just changed. Please check your ticks.',
  /**
   * A Discard refused as the draft was submitted or discarded meanwhile (note-form.md *States* › *Discard dialog*), in
   * the dialog's alert line, its confirming button gone.
   */
  'note.not_a_draft': 'This draft has already been submitted or discarded.',
  /** The archived dead end in the note form's top bar (note-form.md *Conflicts and refusals*), with the full name. */
  'note.participant_archived': (participantName: string) =>
    `${participantName} is archived, so this note cannot be started. Ask a manager.`,
  /**
   * The after-midnight dead end in the note form's top bar (note-form.md *Conflicts and refusals*): design.md §3.3's
   * words, its contraction included, for a worker's first save of a note for yesterday: a worker's form for an older
   * day does not open (design.md A65).
   */
  'note.past_date_manager_only':
    'It’s now after midnight, so this note can’t be started for yesterday. Ask a manager to record it.',
  /**
   * A note submitted on another device or tab (note-form.md *Confirmation dialog* › *No longer possible*, open question
   * 10): design.md §4.3's takeover sentence, word for word, in the Submit confirmation's alert line, and then the note
   * form's dead end in its top bar, as an autosave of the submitted note meets it too.
   */
  'note.version_conflict': 'This note was changed on another device or tab.',
  /**
   * Mark reviewed's `409`, when its refetch of the note could not say why (participant-notes.md *States*): the note
   * has changed, or was already reviewed, so the person checks the latest version first.
   */
  'review.not_current':
    'This note has changed or was already reviewed. Check the latest version before marking it reviewed.',
  /** The Email address field's message on Invite user (users.md *States*, microcopy.md's words). */
  'user.email_in_use':
    'Another account already uses this email address. Use a different one, or find the person in the Users list.',
} as const satisfies Record<ApiErrorCode, unknown>;
