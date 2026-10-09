import { strings, type Cause } from './strings.ts';

/** Words with slots (microcopy.md §8), each shape written here once. */
export const msg = {
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
} as const;
