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
  /** A setup request's failure (sign-in.md *Setup*), above the button that sent it, which sends it again. */
  notSetUp: (cause: Cause): string => msg.notDone('set up', cause),
  /** A sign-out that failed, in the Account panel's status line (app-shell.md *States*). */
  notSignedOut: (cause: Cause): string => msg.notDone('signed out', cause),
  /** A Save that failed, in the alert above it, such as "Not saved: no connection. Try again." */
  notSaved: (cause: Cause): string => msg.notDone('saved', cause),
  /** An Archive participant that failed, in the alert above it (participants.md *States*). */
  notArchived: (cause: Cause): string => msg.notDone('archived', cause),
  /** A Restore that failed, in the alert above it, in the archived notice (participants.md *States*). */
  notRestored: (cause: Cause): string => msg.notDone('restored', cause),
  /** A list's Add goal that failed, in the alert above it (list-editor.md *Request errors*). */
  notAdded: (cause: Cause): string => msg.notDone('added', cause),
  /**
   * The archived participant's notice (participants.md B3), under the `<h1>`, with their full name as stored, from
   * design.md §3.7.
   */
  participantArchived: (name: string) =>
    `${name} is archived, so this page is read-only. They are not on Today and no new notes can be written for them. `
    + 'Their past notes are kept.',
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
