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
  /** A setup request's failure (sign-in.md *Setup*), above the button that sent it, which sends it again. */
  notSetUp: (cause: Cause): string => msg.notDone('set up', cause),
  /** A sign-out that failed, in the Account panel's status line (app-shell.md *States*). */
  notSignedOut: (cause: Cause): string => msg.notDone('signed out', cause),
  /** View F's hint, with `limits.passwordMinLength`, so that the words say the rule that the field checks. */
  passwordHint: (minLength: number) => `Use at least ${minLength} characters.`,
  /** View F's message for a password shorter than `limits.passwordMinLength`. */
  passwordTooShort: (minLength: number) => `Password must be ${minLength} characters or more`,
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
