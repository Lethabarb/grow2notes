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
  /** The Account panel's Sign out button (component 2d). */
  signOut: 'Sign out',
  /** Sign out's busy label. */
  signingOut: 'Signing out…',
  /** The warning before the idle sign-out (app-shell.md component 6). */
  sessionWarning: {
    /** Its heading at each step of the countdown, from 28:00, 29:00, 29:20 and 29:40 with no request accepted. */
    twoMinutes: 'You’ll be signed out in 2 minutes',
    oneMinute: 'You’ll be signed out in 1 minute',
    fortySeconds: 'You’ll be signed out in 40 seconds',
    twentySeconds: 'You’ll be signed out in 20 seconds',
    staySignedIn: 'Stay signed in',
    /** Its status line, once Stay signed in's request has taken 1 s. */
    connecting: 'Connecting…',
    /** Its status line when that request got no answer: the dialog has one button, so the cause and the next step. */
    noConnection: 'No connection. Try again.',
  },
  /** The start-up message's `<h1>` for no connection or a timeout (app-shell.md component 4). */
  couldNotConnect: 'Could not connect to Grow2Notes',
  /**
   * The `<h1>` of the start-up message for any other failure but a `401`, and of the crash pages (app-shell.md
   * component 4).
   */
  thereIsAProblem: 'There is a problem with Grow2Notes',
  /** Page not found's words under its heading (app-shell.md component 4). */
  pageNotFound: {
    checkAddress: 'If you typed or pasted the web address, check it is correct.',
  },
  /** The crash pages' words under their heading (app-shell.md component 4). */
  savedIsKept: 'Anything already saved is kept.',
  /** The link to Today on Page not found and the crash pages (app-shell.md component 4). */
  goToToday: 'Go to Today',
  /**
   * Sign-in's views A and B (sign-in.md): A's `<h1>` is its page name, "Sign in", and B's, the code step's, is the code
   * field's label, `oneTimeCode.label`.
   */
  signIn: {
    /** Under A's `<h1>`. */
    hint: 'Sign in the way you chose when you set up your account.',
    passkey: 'Sign in with a passkey',
    /** The busy label of the passkey button and of the code step's Sign in. */
    busy: 'Signing in…',
    /** The code field's hint on the code step (sign-in.md component 10); setup's view G has a hint of its own. */
    codeHint:
      'Open your authenticator app and find Grow2Notes. If the code changes while you’re typing, you can still '
      + 'use it.',
    /** The code step's button, which sends the code (sign-in.md component 11). */
    signIn: 'Sign in',
    /** The password field's label (the email field's is `emailAddress`). */
    password: 'Password',
    /** The email and password form's messages, inline and linked in the error summary (form-validation.md). */
    enterEmail: 'Enter your email address',
    emailFormat: 'Enter an email address in the correct format, like name@example.com',
    enterPassword: 'Enter your password',
    /** Every sign-in that the server refuses, in the error summary, which says nothing of why (A25). */
    failed:
      'Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.',
    /** What a sign-in that never got its answer did not do, as in "Not signed in: no connection. Try again." */
    notDone: 'signed in',
    /** In place of the passkey button, in a browser with no WebAuthn (sign-in.md component 4). */
    passkeysUnavailable:
      'Passkeys do not work in this browser. To use your passkey, open Grow2Notes in your phone’s main browser, '
      + 'such as Safari or Chrome.',
    /** The help line (microcopy.md §9), plain text, not a link: recovery is always a manager's Reset sign-in. */
    help: 'If you cannot sign in, ask a manager to reset your sign-in.',
  },
  tryAgain: 'Try again',
  /** Try again's busy label, and the start-up's loading line, as `index.html` has it before the script runs. */
  loading: 'Loading…',
  /** The busy label of sign-in's Continue and of the setup link check's Try again (sign-in.md components 8 and 12). */
  checking: 'Checking…',
  /** The button of a step that leads to the next (sign-in.md component 8). */
  continue: 'Continue',
  /** Between two ways of doing one thing, as a word that a screen reader reads (sign-in.md component 5). */
  or: 'or',
  /** A person's email address, as a field's label or a summary's term (sign-in.md components 6 and 13). */
  emailAddress: 'Email address',
  /**
   * The Show/Hide button of a password field that has one (sign-in.md component 7, GOV.UK's words): its visible word,
   * its name, which starts with that word, and what its polite line then says.
   */
  passwordToggle: {
    show: 'Show',
    hide: 'Hide',
    showName: 'Show password',
    hideName: 'Hide password',
    visible: 'Your password is visible',
    hidden: 'Your password is hidden',
  },
  /**
   * The one-time code field (sign-in.md component 10), on setup's view G and sign-in's code step: its label, which is
   * also its message when nothing is left once spaces and dashes are taken out (microcopy.md §9), and its message for
   * a code that is not then 6 digits. Each view's hint is in that view's own group.
   */
  oneTimeCode: {
    label: 'Enter the 6-digit code from your authenticator app',
    wrongFormat: 'The code must be 6 digits',
  },
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
    /** View F's `<h1>`, which is its field's label. */
    createPassword: 'Create a password',
    /** View F's message for a password that is empty or only spaces (its hint and length message are in `msg`). */
    enterPassword: 'Enter a password',
    /** View G's setup block (sign-in.md *Copy for view G*), in the order it shows them. */
    authenticatorApp: {
      heading: 'Set up your authenticator app',
      intro:
        'An authenticator app is a free app on your phone. It shows a new 6-digit code every 30 seconds. You enter '
        + 'the code after your password each time you sign in.',
      getApp:
        'Get an authenticator app on your phone if you do not have one, for example Google Authenticator or '
        + 'Microsoft Authenticator. Any authenticator app works.',
      addGrow2Notes: 'Add Grow2Notes to the app. Scan the QR code, or use the setup key.',
      /** The QR code's `alt`, which says what it is for; the setup key under it is its text equivalent. */
      qrAlt: 'QR code for adding Grow2Notes to your authenticator app',
      /** In place of the QR code, when it did not load. */
      qrNotLoaded: 'The QR code did not load. Use the setup key instead.',
      thisPhone: 'Using this phone? Type the setup key into the app. If the app asks for a name, type Grow2Notes.',
      /** The key's term, in front of the key. */
      setupKey: 'Setup key',
      /** The key as its failure sentence names it, "The setup key did not load: …". */
      theSetupKey: 'The setup key',
      setupKeyNote: 'Your app might call this a ‘key’ or ‘secret key’. You do not need to type the spaces.',
      comeBack: 'Come back to this page.',
      /** The code field's hint on view G; sign-in's code step has a hint of its own. */
      codeHint: 'Find Grow2Notes in the app. If the code changes while you’re typing, you can still use it.',
      /** On the code field, for a code that the server did not take (`422` with `errors.totpCode`, D70). */
      codeRejected: 'That code did not work. Enter the code the app shows for Grow2Notes now.',
      finishSetup: 'Finish setup',
      /** Finish setup's busy label. */
      finishingSetup: 'Finishing setup…',
    },
  },
  /**
   * The Users screens (users.md), whose `<h1>` and Invite user are page names: the list's loading line, and what did
   * not load, as its failure sentence starts (empty-loading-error.md).
   */
  users: {
    listLoading: 'Loading users…',
    theList: 'The user list',
  },
  /**
   * A user's status, by its name in the API (status-tags.md): plain words, never a tag. "Deactivated" also heads the
   * Users list's second section (users.md component A).
   */
  userStatus: { Invited: 'Invited', Active: 'Active', Deactivated: 'Deactivated' },
  /** A user's role, by its name in the API (design.md §2). */
  role: { Worker: 'Worker', Manager: 'Manager' },
  /** The error summary's heading (form-validation.md). */
  errorSummaryHeading: 'There is a problem',
  /**
   * With a space after it, in front of the page title while a form shows its errors, and, hidden, in front of each
   * field's error message.
   */
  errorPrefix: 'Error:',
} as const;

export type Cause = keyof typeof strings.cause;
