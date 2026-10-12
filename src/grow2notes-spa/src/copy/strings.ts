/** Words that have no slots (microcopy.md §8), each written here once. */
export const strings = {
  appName: 'Grow2Notes',
  /** The shell's skip link (app-shell.md component 1), GOV.UK's words. */
  skipToMainContent: 'Skip to main content',
  /** The main navigation's name, which screen readers say as "Main navigation" (app-shell.md component 2b). */
  mainNavigation: 'Main',
  /** The main navigation's items (app-shell.md component 2b), in design.md §4.0's words. */
  navItem: { today: 'Today', flagged: 'Flagged', report: 'Report', manage: 'Manage' },
  /** The header's Account button (app-shell.md component 2d). */
  account: 'Account',
  tryAgain: 'Try again',
  /** Try again's busy label. */
  loading: 'Loading…',
  /** Why a request failed, as the failure sentences put it (microcopy.md §9). */
  cause: {
    /** `fetch` rejected, or no whole answer came in time. */
    offline: 'no connection',
    /** Any other failure but a `401`, which the session flow has. */
    server: 'something went wrong',
  },
  /** The error summary's heading (form-validation.md). */
  errorSummaryHeading: 'There is a problem',
  /**
   * With a space after it, in front of the page title while a form shows its errors, and, hidden, in front of each
   * field's error message.
   */
  errorPrefix: 'Error:',
} as const;

export type Cause = keyof typeof strings.cause;
