import { count } from './format.ts';
import type { PageName } from './pageNames.ts';
import { strings, type Cause } from './strings.ts';

const characters = (n: number) => count(n, 'character', 'characters');

/** Words with slots (microcopy.md §8), each shape written here once. */
export const msg = {
  /** A page's title (app-shell.md component 3), "Grow2Notes – Today", with an en dash and a name from the list. */
  pageTitle: (name: PageName) => `${strings.appName} – ${name}`,
  /** The Account panel's first line (app-shell.md component 2d): whose session it is on a shared device (D21). */
  signedInAs: (displayName: string) => `Signed in as ${displayName}`,
  /**
   * microcopy.md §4's Load failed: "[Thing] did not load: [cause]. Try again.", or, `again` after a Try again that
   * failed, "[Thing] still did not load: [cause]. Try again.", new words that a screen reader announces again.
   */
  loadFailed: (what: string, cause: Cause, again: boolean) =>
    `${what} ${again ? 'still did not load' : 'did not load'}: ${strings.cause[cause]}. Try again.`,
  /**
   * microcopy.md §4's Action failed: "Not [done]: [cause]. Try again.", with what is safe, if anything, before "Try
   * again.", as in "Not submitted: no connection. Your draft is saved. Try again."
   */
  notDone: (done: string, cause: Cause, safe?: string) =>
    `Not ${done}: ${strings.cause[cause]}.${safe === undefined ? '' : ` ${safe}`} Try again.`,
  /** The setup link check's failure (sign-in.md *Setup*), with Try again, which sends the link held in memory. */
  setupLinkNotChecked: (cause: Cause) => `Your setup link was not checked: ${strings.cause[cause]}. Try again.`,
  /**
   * In place of a list that Find a participant has emptied, and read out after a pause in typing (search-filter.md),
   * with the text as typed, its ends trimmed, in typographic quotes.
   */
  noParticipantMatches: (typed: string) => `No participant matches ‘${typed}’.`,
  /**
   * In place of Goals' tick boxes on a new note or a draft of a participant with no goals (design.md §4.3), with their
   * given name, as design.md writes it.
   */
  noGoalsSetUp: (givenName: string) => `No goals set up for ${givenName} yet. A manager can add them.`,
  /**
   * The Submit confirmation's confirming button (note-form.md component 14), with the participant's full name, which
   * the title shows too (design.md §3.9).
   */
  submitNoteFor: (name: string) => `Submit note for ${name}`,
  /**
   * A Submit that failed (note-form.md *Confirmation dialog*, microcopy.md §9): in the confirmation's alert line, or
   * above Submit note when a browser closed the confirmation while its request was out; and the error summary's one
   * unlinked item, for a `422` from Submit.
   */
  notSubmitted: (cause: Cause): string => msg.notDone('submitted', cause, strings.note.draftIsSaved),
  /** Today's success banner after Submit (design.md §4.3), with the participant's full name, one phrase. */
  noteSubmitted: (name: string) => `Note for ${name} submitted`,
  /**
   * The Discard dialog (note-form.md component 15, confirm-dialog.md's dialog 2): its title and its confirming button,
   * each with the participant's full name; its body's line for a draft dated before today, with the note date in
   * `dateLong`, as the earlier-day notice on the form words it; and its line for someone else's draft, with the
   * author's display name as stored and the time the draft was started, as `stamp()` gives it.
   */
  discardTitle: (name: string) => `Discard the draft note for ${name}?`,
  discardDraftFor: (name: string) => `Discard draft for ${name}`,
  draftIsFor: (noteDate: string) => `This draft is for ${noteDate}.`,
  startedItAt: (authorDisplayName: string, startedAt: string) => `${authorDisplayName} started it at ${startedAt}.`,
  /**
   * The note form's notice on a draft from an earlier day (note-form.md *Kinds of note*), with the note date in
   * `dateLong`: the Discard dialog's line for such a draft, then what Submit does with that date.
   */
  earlierDayDraft: (noteDate: string) => `${msg.draftIsFor(noteDate)} Submitting it now keeps that date.`,
  /**
   * The note form's notice on a manager's past-day note (note-form.md *Kinds of note*), in the place of the earlier-day
   * notice: the note's date and the day it was written, both in `dateLong`.
   */
  pastDayNotice: (noteDate: string, writtenOn: string) => `Past-day note for ${noteDate}, written on ${writtenOn}.`,
  /**
   * A Discard that failed (note-form.md *States* › *Discard dialog*): in the dialog's alert line, or above Submit note
   * when a browser closed the dialog while its request was out.
   */
  notDiscarded: (cause: Cause): string => msg.notDone('discarded', cause),
  /**
   * The note form's caption in edit mode, inside its heading before the name (note-form.md *Kinds of note*): the number
   * is the version being edited.
   */
  editingSubmittedNote: (version: number) => `Editing submitted note (version ${version})`,
  /**
   * The version-conflict notice's heading (note-form.md component 6): who saved the newer version, which, and when,
   * with the time as `stamp()` gives it.
   */
  savedVersionWhileEditing: (displayName: string, version: number, at: string) =>
    `${displayName} saved version ${version} at ${at} while you were editing.`,
  /** The summary of the notice's newer version: "Version 3 by Sam Lee". */
  versionBy: (version: number, displayName: string) => `Version ${version} by ${displayName}`,
  /** A Save changes that failed, in the alert above the buttons (note-form.md *Edit-mode actions*). */
  changesNotSaved: (cause: Cause): string => `Changes not saved: ${strings.cause[cause]}. Try again.`,
  /** A Cancel that failed, in the alert above the buttons. */
  notCancelled: (cause: Cause): string => msg.notDone('cancelled', cause),
  /**
   * The read view's byline before the submitted time (participant-notes.md *Read view*), with the author's display
   * name as stored, as the daily report words it (design.md §11.3).
   */
  writtenBy: (displayName: string) => `Written by ${displayName}`,
  /**
   * The read view's Edited line after the Edited tag (status-tags.md item 4): who saved the current version, with the
   * comma that the time follows.
   */
  lastChangeBy: (displayName: string) => `last change by ${displayName},`,
  /**
   * The read view's flag line after the Flagged tag (design.md §4.4), and a To review row's line after its meta line,
   * there with the reason up to its first line break (flagged.md item 4): the reason, after the form's label for it.
   */
  flagReason: (reason: string) => `${strings.note.reason}: ${reason}`,
  /**
   * Who reviewed a note, after the Reviewed tag or word and before the date and time, from `format.dateTime()`
   * (design.md A74): "Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am", on the read view, the panel's record and the
   * Reviewed list alike.
   */
  reviewedBy: (displayName: string) => `by ${displayName} on`,
  /** The Flagged item's hidden text after its word and badge, so that the name reads "Flagged, 3 to review". */
  toReviewBadge: (count: number) => `, ${count} to review`,
  /** A review's comment, after its line (participant-notes.md component 4), as written. */
  reviewComment: (comment: string) => `Comment: ${comment}`,
  /** A Mark reviewed that failed, in the alert above its button: "Not marked reviewed: no connection. Try again." */
  notMarkedReviewed: (cause: Cause): string => msg.notDone('marked reviewed', cause),
  /** A version's number, as its row's link and its page's caption (version-history.md *Copy on this screen*). */
  version: (versionNumber: number) => `Version ${versionNumber}`,
  /** The link to the current version, in page B's line for an earlier version, with the full stop after it. */
  versionLink: (versionNumber: number) => `version ${versionNumber}`,
  /** Page B's loading line, and what its failure sentence says did not load, which is the version's caption. */
  loadingVersion: (versionNumber: number) => `Loading version ${versionNumber}…`,
  /** Page A's sentence under its heading, from the highest number in the list, the screen's only source for it. */
  currentVersionIs: (versionNumber: number) => `The current version is version ${versionNumber}.`,
  /** Page B's saved-by line for version 1, with the comma that the time follows and the name with any suffix. */
  submittedBy: (displayName: string) => `Submitted by ${displayName},`,
  /** Page B's saved-by line for a later version, with the comma that the time follows and the name with any suffix. */
  savedBy: (displayName: string) => `Saved by ${displayName},`,
  /**
   * The past-day line after its tag (status-tags.md, microcopy.md): who wrote the note, always a manager, as only a
   * manager can write a note for an earlier day, then the time of the note's start, from `format.dateTime()`.
   */
  pastDayWrittenBy: (displayName: string) => `written by ${displayName} (manager) on`,
  /** Past notes with no live note (design.md §4.4 *States*), with the participant's full name, as stored. */
  noNotesYet: (name: string) => `No notes yet for ${name}.`,
  /** A setup request's failure (sign-in.md *Setup*), above the button that sent it, which sends it again. */
  notSetUp: (cause: Cause): string => msg.notDone('set up', cause),
  /** A sign-out that failed, in the Account panel's status line (app-shell.md *States*). */
  notSignedOut: (cause: Cause): string => msg.notDone('signed out', cause),
  /** A Save that failed, in the alert above it, such as "Not saved: no connection. Try again." */
  notSaved: (cause: Cause): string => msg.notDone('saved', cause),
  /**
   * An Archive participant or a list row's Archive that failed, in the alert above it (participants.md *States*,
   * list-editor.md *Request errors*).
   */
  notArchived: (cause: Cause): string => msg.notDone('archived', cause),
  /**
   * A Restore that failed, in the alert above it: in the archived notice, or in an archived row of a list
   * (participants.md *States*, list-editor.md *Request errors*).
   */
  notRestored: (cause: Cause): string => msg.notDone('restored', cause),
  /** A list's Add goal or Add item, or Add group, that failed, in the alert above it (list-editor.md). */
  notAdded: (cause: Cause): string => msg.notDone('added', cause),
  /**
   * A list row's or a Common items group's Move up or Move down that failed, in the row's or the group's alert above
   * its buttons (list-editor.md, common-items.md *Failures*).
   */
  notMoved: (cause: Cause): string => msg.notDone('moved', cause),
  /**
   * A list row's move, in the page's status region once the list shows it (list-editor.md *Behaviour*): where the row
   * went, counted from 1 among the active rows, such as "Moved down to number 4 of 5", one phrase with no full stop.
   */
  moved: (direction: 'up' | 'down', position: number, total: number) =>
    `Moved ${direction} to number ${position} of ${total}`,
  /**
   * A group's move on Common items, in the page's status region once the sections show it (common-items.md *Group
   * actions*): "Group moved up" or "Group moved down", one phrase with no full stop, as a row's is, naming no place.
   */
  groupMoved: (direction: 'up' | 'down') => `Group moved ${direction}`,
  /**
   * A common item's Save that moved it to another group, in the page's status region once the list shows it at the end
   * of that group (common-items.md *Item actions*): "Item saved and moved to the end of Personal care", naming the
   * group as its Group radio does, one phrase with no full stop, as "Item saved" is.
   */
  itemSavedAndMoved: (group: string) => `Item saved and moved to the end of ${group}`,
  /**
   * After the visible label of a button that acts on a list's row, for screen readers alone, the row's text, so that
   * "Edit" is read as "Edit, Makes own breakfast" (list-editor.md *Repeated buttons in every row*): hidden text rather
   * than `aria-label`, which automatic translation misses, and after the label, so that the name starts with the words
   * on screen (SC 2.5.3).
   */
  rowContext: (text: string) => `, ${text}`,
  /**
   * After the visible label of a group's button on Common items, for screen readers alone, as `rowContext` is after a
   * row's, the group's name and "group", so that "Rename" is read as "Rename, Community outing group" (common-items.md
   * component 5).
   */
  groupContext: (name: string) => `, ${name} group`,
  /**
   * After a list's words that would be the same in every group's list on Common items, its group's name, so that each
   * is told from the others': after "New item" in the add field's label, as in "New item in Every note", and, for
   * screen readers alone, after "Archived items" in the archived section's summary (common-items.md component 5).
   */
  inList: (name: string) => ` in ${name}`,
  /**
   * A list row's Save refused as stale (`412`) that finds the row still active once the list is read again
   * (list-editor.md *Request errors*), in the edit form's alert, naming the row as its kind does and with its wording
   * now, as stored, given as its kind's screen gives it: a goal's as it is, a common item's `quoted`.
   */
  rowChangedWhileEditing: (row: string, wording: string) =>
    `Someone else changed this ${row} while you were editing. It now says: ${wording}. Save to replace it with your `
    + 'wording, or Cancel to keep it.',
  /**
   * Words that someone typed, in typographic quotes (microcopy.md §1, Quotes and apostrophes), as a common item's
   * wording is in its edit conflict: "It now says: ‘Meal prepared’." (common-items.md *States*).
   */
  quoted: (text: string) => `‘${text}’`,
  /**
   * The archived participant's notice (participants.md B3), under the `<h1>`, with their full name as stored, from
   * design.md §3.7.
   */
  participantArchived: (name: string) =>
    `${name} is archived, so this page is read-only. They are not on Today and no new notes can be written for them. `
    + 'Their past notes are kept.',
  /** Write past-day note's whole page for an archived participant (participants.md C4), with their full name. */
  noNewNotesArchived: (name: string) => `${name} is archived, so no new notes can be written for them.`,
  /**
   * A Send invite or a Resend invite that failed, above its button, and a refusal that names no field of Invite user's
   * (users.md *States*).
   */
  notSent: (cause: Cause): string => msg.notDone('sent', cause),
  /**
   * Resend invite's result under its button (users.md *States* › User detail), with the time the server sent the link,
   * such as "4:12 pm", or, when the answer said none, without "at …", as the device's clock is never the time shown.
   */
  setupLinkSent: (email: string, time: string | undefined) =>
    `Setup link sent to ${email}${time === undefined ? '' : ` at ${time}`}. The old link no longer works.`,
  /** A page action refused `412`, as someone changed the user first, once the page shows them as they are now. */
  userChanged: (displayName: string) =>
    `Someone else has just changed ${displayName}’s account. The page now shows the latest details.`,
  /**
   * The date of birth's message for the parts left empty when another is not (text-and-date-inputs.md), named in
   * their order, as in "Date of birth must include a day and month".
   */
  dateOfBirthMustInclude: (parts: readonly string[]) =>
    `${strings.dateOfBirth.legend} must include a ${parts.join(' and ')}`,
  /** A dialog's action refused `412`, which leaves Go back alone, behind which the page shows the user as they are. */
  userChangedGoBack: (displayName: string) =>
    `Someone else has just changed ${displayName}’s account. Go back to see the latest details.`,
  /** A Reset sign-in that failed, in its dialog (users.md *Dialogs*), such as "Not reset: no connection. Try again." */
  notReset: (cause: Cause): string => msg.notDone('reset', cause),
  /** A Deactivate that failed, in its dialog. */
  notDeactivated: (cause: Cause): string => msg.notDone('deactivated', cause),
  /** A Reactivate that failed, above the actions (users.md *States* › User detail). */
  notReactivated: (cause: Cause): string => msg.notDone('reactivated', cause),
  /**
   * The Reset sign-in dialog for someone else (users.md *Dialogs*): its title; its first line, from design.md §4.11's
   * "The manager confirms who is asking … by phone or in person"; its second, with the address the link goes to; and
   * its confirming button. Your own reset has its words in `strings.users.resetDialog` and `resetOwnWhatHappens`.
   */
  resetTitle: (displayName: string) => `Reset sign-in for ${displayName}?`,
  resetCheckWhoIsAsking: (displayName: string) =>
    `Only do this after you have checked, by phone or in person, that ${displayName} is the one asking.`,
  resetWhatHappens: (email: string) =>
    'Their current way of signing in will be removed, and they will be signed out everywhere. A new setup link will '
    + `be emailed to ${email}.`,
  resetConfirm: (displayName: string) => `Reset sign-in for ${displayName}`,
  /** Your own Reset sign-in dialog's one line, in place of the two for someone else. */
  resetOwnWhatHappens: (email: string) =>
    `You will be signed out everywhere now. A new setup link will be emailed to ${email}.`,
  /**
   * The Deactivate dialog for someone else (users.md *Dialogs*): its title; its body, design.md §4.11's word for word,
   * its contraction included, or, for an Invited person, what deactivating does to their link; and its confirming
   * button.
   */
  deactivateTitle: (displayName: string) => `Deactivate ${displayName}?`,
  deactivateBody: (displayName: string) =>
    `${displayName} will be signed out everywhere now and can’t sign in. Their notes stay.`,
  deactivateInvitedBody: (displayName: string) =>
    `${displayName} has not set up their account yet. Their setup link will stop working.`,
  deactivateConfirm: (displayName: string) => `Deactivate ${displayName}`,
  /** View F's hint, with `limits.passwordMinLength`, so that the words say the rule that the field checks. */
  passwordHint: (minLength: number) => `Use at least ${minLength} characters.`,
  /** View F's message for a password shorter than `limits.passwordMinLength`. */
  passwordTooShort: (minLength: number) => `Password must be ${minLength} characters or more`,
  /**
   * microcopy.md §4's Field error, too long, which names the field by its label, as in "Guide prompts must be 1,000
   * characters or less".
   */
  tooLong: (field: string, limit: number) => `${field} must be ${characters(limit)} or less`,
  /** microcopy.md §4's Character count hint, under a field's label: "You can enter up to 1,000 characters". */
  characterLimitHint: (limit: number) => `You can enter up to ${characters(limit)}`,
  /**
   * microcopy.md §4's Character count, under a field near or over its limit: "You have 100 characters remaining", down
   * to "You have 0 characters remaining" at the limit, then "You have 1 character too many".
   */
  characterCount: (length: number, limit: number) =>
    length <= limit
      ? `You have ${characters(limit - length)} remaining`
      : `You have ${characters(length - limit)} too many`,
} as const;
