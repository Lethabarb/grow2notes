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
    /**
     * The email and password form's messages, inline and linked in the error summary (form-validation.md), with the
     * shared `emailFormat`.
     */
    enterEmail: 'Enter your email address',
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
  /**
   * Sign-in's view C, signed out in place (sign-in.md; app-shell.md component 7): its `<h1>` in place of A's, and the
   * sentence under it (microcopy.md's), over A's hint and controls.
   */
  signedOut: {
    heading: 'You’ve been signed out',
    sentence: 'Sign in again to go back to where you were.',
  },
  tryAgain: 'Try again',
  /** Try again's busy label, and the start-up's loading line, as `index.html` has it before the script runs. */
  loading: 'Loading…',
  /** The busy label of sign-in's Continue and of the setup link check's Try again (sign-in.md components 8 and 12). */
  checking: 'Checking…',
  /** The busy label of Send invite and Resend invite (users.md component B item 8). */
  sending: 'Sending…',
  /** The busy label of a list's Add goal (list-editor.md *Exact copy*). */
  adding: 'Adding…',
  /** The button of a step that leads to the next (sign-in.md component 8). */
  continue: 'Continue',
  /**
   * The button of a page or form that saves what it shows and stays, such as Guide prompts or a participant's Details
   * (primary-actions.md).
   */
  save: 'Save',
  /** Save's busy label (primary-actions.md), and the save status's hidden words while a save waits. */
  saving: 'Saving…',
  /** The save status once a save is done, then the time of its answer, if it gave one: "Saved 4:12 pm". */
  saved: 'Saved',
  /** Between two ways of doing one thing, as a word that a screen reader reads (sign-in.md component 5). */
  or: 'or',
  /** The safe button of a dialog that confirms an action, under the one that confirms it (confirm-dialog.md). */
  goBack: 'Go back',
  /**
   * A person's name and email address, each as a field's label or a summary's term (sign-in.md components 6 and 13,
   * users.md component B).
   */
  name: 'Name',
  emailAddress: 'Email address',
  /**
   * An email address's message when it is not text, one "@" and text, form-validation.md's loose check, or the server
   * refuses it (GOV.UK's words).
   */
  emailFormat: 'Enter an email address in the correct format, like name@example.com',
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
    /** The hint under view E's account summary (sign-in.md component 13), whose terms are `name` and `emailAddress`. */
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
   * Manage > Guide prompts (guide-prompts.md), whose `<h1>` is its page name, which labels the field: the help text
   * under it (design.md §4.10), the region's loading line and what did not load, as its failure sentence starts, then
   * the alert above Save for a `412`, when another manager saved first; and the leave dialog (component 7).
   */
  guidePrompts: {
    help: 'Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.',
    loading: 'Loading guide prompts…',
    thePrompts: 'The guide prompts',
    conflict:
      'Another manager saved the guide prompts while you were editing. Your text is still here. Select Save to '
      + 'replace their version with yours.',
    /** The leave dialog's title and body, then its safe button, on top, and the button that leaves. */
    leave: {
      title: 'Leave without saving?',
      body: 'Your changes to the guide prompts are not saved.',
      stay: 'Stay on this page',
      leave: 'Leave without saving',
    },
  },
  /**
   * The Users screens (users.md), whose fixed `<h1>`s and Invite user are page names: the list's and the user
   * detail's loading lines, and what did not load, as each failure sentence starts (empty-loading-error.md), which
   * Edit user shares with the user detail; then the user detail's, Invite user's and Edit user's own words.
   */
  users: {
    listLoading: 'Loading users…',
    theList: 'The user list',
    userLoading: 'Loading user…',
    thisUser: 'This user',
    /**
     * The terms of the user detail's summary list, after `emailAddress` (users.md component C item 4); `role` is also
     * the Role radios' legend (component B item 6).
     */
    role: 'Role',
    status: 'Status',
    /** The user detail's link to the Edit user page. */
    edit: 'Edit',
    /** Invite user's hints (users.md component B items 4 and 5), under the labels `name` and `emailAddress`. */
    nameHint: 'Shown on the notes they write',
    inviteEmailHint: 'Grow2Notes will email them a setup link. It works once and lasts 7 days.',
    /** The hint under each of the Role radios, by the role's name in the API (users.md component B item 6). */
    roleHint: {
      Worker: 'Writes notes and reads past notes',
      Manager: 'Can also review flagged notes, download reports, and set up participants and users',
    },
    /**
     * Edit user's hints under Email address (users.md component D item 5), for someone else, then on your own page;
     * Name's is `nameHint`, as on Invite user.
     */
    editEmailHint: 'Changing the email resets sign-in and sends a setup link to the new address.',
    ownEmailHint:
      'Changing your email resets your sign-in. You will be signed out, and a setup link will be sent to the new '
      + 'address.',
    /** Under Edit user's `<h1>`, on your own page as the only active manager (A26). */
    editLastManagerNotice:
      'You are the only active manager, so you cannot change your email or make yourself a worker. First make '
      + 'someone else a manager.',
    /** Invite user's button (users.md component B item 8), whose busy label is `sending`. */
    sendInvite: 'Send invite',
    /**
     * The messages of a user's fields, inline and linked in the error summary (users.md *States* › Invite user), with
     * the shared `emailFormat` and `msg.tooLong`. Role's is met only through a server `422`, as Worker starts
     * selected.
     */
    enterName: 'Enter the person’s name',
    enterEmail: 'Enter an email address',
    chooseRole: 'Choose Worker or Manager',
    /**
     * Role's and Email address's messages on your own Edit user page as the only active manager (users.md *States* ›
     * Edit user), for Worker and for a new address, from Save's own check or the server's `409 user.last_manager`.
     */
    lastManagerRole:
      'You are the only active manager, so you cannot make yourself a worker. Make someone else a manager first.',
    lastManagerEmail:
      'You are the only active manager, so you cannot change your email. Make someone else a manager first.',
    /** In the alert above Edit user's Save, for a save refused as stale (`412`), the typed values kept. */
    changedWhileEditing: 'Someone else changed this user while you were editing. Check the details, then save again.',
    /**
     * The user detail's Resend invite, for an Invited user (users.md component C item 6), with the description above
     * it; its busy label is `sending`, and its result `msg.setupLinkSent`.
     */
    resendInvite: 'Resend invite',
    resendInviteDescription: 'Sends a new setup link. The old link stops working.',
    /** Reset sign-in, for an Active user, with the description above it; it opens `resetDialog`. */
    resetSignIn: 'Reset sign-in',
    resetSignInDescription: 'For a lost phone, a new phone or a forgotten password. Check who is asking first.',
    /** Group 2's Deactivate, for an Invited or Active user, with its description; it opens `deactivateDialog`. */
    deactivate: 'Deactivate',
    deactivateDescription: 'For someone who no longer works here. Their notes stay.',
    /** Group 2's Reactivate, for a Deactivated user, with the description above it and its busy label. */
    reactivate: 'Reactivate',
    reactivateDescription: 'Lets them sign in again.',
    reactivating: 'Reactivating…',
    /** In group 2, in place of Reset sign-in and Deactivate, on your own page as the only active manager (A26). */
    lastManagerNotice:
      'You are the only active manager, so you cannot reset your own sign-in, deactivate your account or make '
      + 'yourself a worker. To do any of these, first make someone else a manager.',
    /**
     * The Reset sign-in dialog's words for your own account, its busy line, and its refusal when you are the only
     * active manager after all (users.md *Dialogs*); its words for someone else name them, in `msg`.
     */
    resetDialog: {
      ownTitle: 'Reset your own sign-in?',
      ownConfirm: 'Reset my sign-in',
      busy: 'Resetting sign-in…',
      lastManager:
        'You are the only active manager, so you cannot reset your own sign-in. Make someone else a manager first.',
    },
    /** The Deactivate dialog's words, as `resetDialog`'s are. */
    deactivateDialog: {
      ownTitle: 'Deactivate your own account?',
      ownBody: 'You will be signed out everywhere now and cannot sign in again. Your notes stay.',
      ownConfirm: 'Deactivate my account',
      busy: 'Deactivating…',
      lastManager:
        'You are the only active manager, so you cannot deactivate your account. Make someone else a manager first.',
    },
  },
  /**
   * The Participants screens (participants.md), whose fixed `<h1>`s and Add participant are page names: the list's and
   * the participant page's loading lines, and what did not load, as each failure sentence starts
   * (empty-loading-error.md); the list's empty sentences, in place of its rows, for no participants at all and for a
   * view with none; and the names of its two views, Active and Archived (tabs-segmented.md).
   */
  participants: {
    listLoading: 'Loading participants…',
    theList: 'The participant list',
    participantLoading: 'Loading participant…',
    thisParticipant: 'This participant',
    noneYet: 'No participants yet.',
    noneActive: 'No active participants.',
    noneArchived: 'No archived participants.',
    active: 'Active',
    archived: 'Archived',
  },
  /**
   * A participant's Details (participants.md B5; text-and-date-inputs.md): the section's `<h2>`, the names' labels,
   * with which their too-long messages start, and each name's message when it is left empty; then, in the alert above
   * Save, a save refused as stale (`412`), the typed details kept.
   */
  participantDetails: {
    heading: 'Details',
    givenName: 'Given name',
    familyName: 'Family name',
    enterGivenName: 'Enter the given name',
    enterFamilyName: 'Enter the family name',
    changedMeanwhile:
      'Someone else changed these details while you were editing. Check the details, then save again.',
  },
  /** A participant's Goals section (participants.md B6): its `<h2>`. The list's own words are `listEditor.goal`. */
  participantGoals: { heading: 'Goals' },
  /**
   * The words of each kind of list that `ListEditor` edits (list-editor.md *Exact copy*), a participant's goals
   * (design.md §4.8): the help text above the list, unless it is read-only; the sentence in place of the list when no
   * row is active; and the summary of the archived rows' section. Then the add form: its field's label, its button,
   * and, in the page's status region, what an add did; and a row's text as its messages name it, the one for no text
   * and, with `msg.tooLong`, the one for too much (form-validation.md).
   */
  listEditor: {
    goal: {
      help:
        'Changes apply to notes started from now on. Notes already started or submitted keep the wording they were '
        + 'written with.',
      empty: 'No goals yet. Notes will show an empty Goals section.',
      archivedSummary: 'Archived goals',
      newLabel: 'New goal',
      add: 'Add goal',
      added: 'Goal added to the end of the list',
      enter: 'Enter the goal',
      text: 'Goal',
    },
  },
  /**
   * Archiving and restoring a participant (participants.md B3 and B7): the page's Actions section's `<h2>`, Archive
   * participant in it and Restore in the archived notice, each with its busy label; then, in the alert above either, a
   * press refused as stale (`412`) that finds the participant still as they were.
   */
  participantActions: {
    heading: 'Actions',
    archive: 'Archive participant',
    archiving: 'Archiving…',
    restore: 'Restore',
    restoring: 'Restoring…',
    changedMeanwhile: 'Someone else changed this participant just now. Check the page, then try again.',
  },
  /**
   * The date of birth's three fields (text-and-date-inputs.md): the group's legend, which also starts its messages,
   * its hint and each part's label; each part as a message names it, as in "a day and month"; then the messages, in
   * the order the checks run.
   */
  dateOfBirth: {
    legend: 'Date of birth',
    hint: 'For example, 27 3 1987',
    day: 'Day',
    month: 'Month',
    year: 'Year',
    partNames: { day: 'day', month: 'month', year: 'year' },
    enter: 'Enter the date of birth',
    yearFourNumbers: 'Year must include 4 numbers',
    notReal: 'Date of birth must be a real date',
    inThePast: 'Date of birth must be in the past',
  },
  /**
   * The Find a participant box (search-filter.md), on Today and Manage > Participants: its label; the description that
   * screen readers read once as the field takes focus, as the list changing shows itself on screen; and Clear, with
   * the word only screen readers get after it, so that the button is named "Clear search".
   */
  findParticipant: {
    label: 'Find a participant',
    description: 'The list of participants below changes as you type.',
    clear: 'Clear',
    clearHidden: 'search',
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
