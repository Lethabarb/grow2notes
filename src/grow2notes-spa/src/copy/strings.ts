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
  /** Page not found's words under its heading (app-shell.md component 4). */
  pageNotFound: {
    checkAddress: 'If you typed or pasted the web address, check it is correct.',
    goToToday: 'Go to Today',
  },
  tryAgain: 'Try again',
  /** Try again's busy label. */
  loading: 'Loading…',
  /** Between two ways of doing one thing, as a word that a screen reader reads (sign-in.md component 5). */
  or: 'or',
  /** A person's email address, as a field's label or a summary's term (sign-in.md components 6 and 13). */
  emailAddress: 'Email address',
  /** Why a request failed, as the failure sentences put it (microcopy.md §9). */
  cause: {
    /** `fetch` rejected, or no whole answer came in time. */
    offline: 'no connection',
    /** Any other failure but a `401`, which the session flow has. */
    server: 'something went wrong',
  },
  /** Account setup (sign-in.md views D to G), whose `<h1>` is its page name, "Set up your account". */
  setup: {
    /** The link check's status line, once the check has taken 1 s. */
    checkingLink: 'Checking your link…',
    /** The link check's Try again, busy. */
    checking: 'Checking…',
    /** A `429` on a setup call, where that call's failures show. */
    tooManyTries: 'Too many tries. Wait 1 minute, then try again.',
    /** The whole-page message for a `401` from a setup call: the enrolment cookie has ended. */
    timedOut: { heading: 'Your setup session timed out', body: 'Open the link from your email again.' },
    /** Under the `<h1>` "Set up your account", on a setup page opened with no link and nothing in memory. */
    noLink: 'To set up your account, open the link from your email again.',
    /** View E's account summary (sign-in.md component 13): the term for the person's name, and the hint under it. */
    name: 'Name',
    accountHint: 'If these are wrong, ask a manager.',
    /** View E's method choice (sign-in.md component 14): its `<h2>`, then each method with the hint under it. */
    chooseMethod: 'Choose how you’ll sign in',
    passkey: 'Use a passkey (recommended)',
    passkeyHint:
      'You’ll sign in with your face, fingerprint or PIN, the same way you unlock your phone. Set it up on the phone '
      + 'you’ll use for Grow2Notes.',
    /** The passkey button, busy. */
    settingUp: 'Setting up your account…',
    password: 'Use a password and authenticator app',
    passwordHint: 'You’ll create a password and use a free app that shows a 6-digit code each time you sign in.',
    /** Above the passkey button, when the browser made no passkey or the server refused the one it made. */
    passkeyNotSetUp:
      'Your passkey was not set up. Try again, or open the link from your email in your phone’s browser.',
    /** In place of the passkey button and its hint, in a browser with no WebAuthn, as an email app's own may be. */
    inAppBrowser: {
      heading: 'Open this link in your browser',
      body:
        'Passkeys cannot be set up here. Go back to the email and open the link in your phone’s browser, such as '
        + 'Safari or Chrome.',
    },
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
