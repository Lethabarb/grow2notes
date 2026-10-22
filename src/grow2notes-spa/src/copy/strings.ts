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
  /**
   * The Account panel's status line when a note form's change waiting for its save could not be saved first, so that
   * no logout was sent (app-shell.md *States*, Sign-out blocked: note not saved; D67).
   */
  notSignedOutNoteNotSaved: 'Not signed out: your note is not saved yet. Keep this page open; retrying.',
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
  /** The busy label of a list's Add goal and of Common items' Add group (list-editor.md *Exact copy*). */
  adding: 'Adding…',
  /** The button of a step that leads to the next (sign-in.md component 8). */
  continue: 'Continue',
  /**
   * The button of a page or form that saves what it shows and stays, such as Guide prompts, a participant's Details or
   * a list's edit form (primary-actions.md, list-editor.md).
   */
  save: 'Save',
  /** Beside a form's Save, the button that closes the form and drops what was typed (list-editor.md). */
  cancel: 'Cancel',
  /** Save's busy label (primary-actions.md), and the save status's hidden words while a save waits. */
  saving: 'Saving…',
  /** The save status once a save is done, then the time of its answer, if it gave one: "Saved 4:12 pm". */
  saved: 'Saved',
  /**
   * The busy label of Archive participant and of a list row's or a common item group's Archive (participants.md,
   * list-editor.md, common-items.md).
   */
  archiving: 'Archiving…',
  /**
   * The button that brings back what is archived: a participant, in their archived notice, an archived row of a list
   * and an archived common item group (participants.md B3, list-editor.md, common-items.md component 8).
   */
  restore: 'Restore',
  /** Restore's busy label. */
  restoring: 'Restoring…',
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
   * view with none; and the names of its two views, Active and Archived (tabs-segmented.md). Today's list loads,
   * fails and is empty in the list's words (today.md components 4 and 9).
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
   * Today (today.md): the `<h2>` of the caller's drafts and pending edits that the day's list does not show (design.md
   * §4.2 item 2, component 5); and the visually hidden `<h2>` before its list of participants (component 6), so that a
   * screen reader moving by headings hears where the list starts.
   */
  today: { unfinishedDraftsHeading: 'Your unfinished drafts', participantsHeading: 'Participants' },
  /**
   * A note's status, by its name in the API (status-tags.md *The mapping*): plain words, never a tag, first on a note's
   * status line, as on Today's rows (today.md component 8) and in the save indicator of a draft reopened.
   */
  noteStatus: { Draft: 'Draft', Submitted: 'Submitted' },
  /**
   * A note's status line after its status word (design.md §4.2 item 4): the signed-in user, in place of their name
   * (microcopy.md §3, *Names*), as in "Draft · You · saved 9:42 am"; and the word before a draft's time, the time of
   * its author's last save, as the save indicator of a draft reopened has it too, "Draft · saved 9:42 am", or, on
   * anyone else's draft, of its start, as in "Draft · Alex P. · started 9:14 am".
   */
  statusLine: { you: 'You', saved: 'saved', started: 'started' },
  /** Each kind of `Tag`, by its kind (status-tags.md *The mapping*). */
  tag: { flagged: 'Flagged', edited: 'Edited' },
  /**
   * The note form (note-form.md): its loading line and what did not load, as the failure sentence starts
   * (empty-loading-error.md); then Goals' heading, in its legend, and hint (design.md §4.3), the Common items' heading
   * and hint, the question over the picker with its hint, and the sentence for a form with no common items
   * (group-picker.md *Exact copy*), and Guided notes' heading, which is its box's label, each numbered as design.md
   * numbers the form's sections, whichever of them it shows; the flag's tick box and the label of the Reason it reveals
   * (design.md §4.3 item 6, conditional-reveal.md *Copy*); and the save indicator once a failed save is being retried,
   * as the top bar's second row, and while a dead end or the takeover banner in the top bar says why the note cannot be
   * saved, then the takeover banner's two choices (autosave-status.md *Exact copy*). Then Submit note (component 12)
   * and Submit's checks (*Validation*), inline and in the error summary: Guided notes left blank, with the second
   * sentence only while guide prompts are set, as they are the grey text; the name that Guided notes' too-long message
   * starts with, as Reason's starts with `reason`; and Reason left blank while the flag is ticked. Then the
   * confirmation (component 14): its title's lead, for a note dated `me.today` and for any other, before the name on
   * its own line; its flag line, from the values that Submit sends; its status line once the request has taken 1 s; and
   * what a failure says is safe, as in "Not submitted: no connection. Your draft is saved. Try again." (microcopy.md
   * §9). Then More actions, the disclosure under Submit note, and Discard draft in its panel (component 13), as on a
   * manager's read-only draft (participant-notes.md component 9); and the Discard dialog's line that always ends its
   * body, and its status line once the request has taken 1 s (component 15, confirm-dialog.md's dialog 2).
   */
  note: {
    loading: 'Loading note…',
    theNote: 'The note',
    goals: '1. Goals',
    goalsHint: 'Ticked means reached.',
    commonItems: '2. Common items',
    commonItemsHint: 'Ticked means done.',
    whichHappened: 'Which of these happened?',
    whichHappenedHint: 'Tick all that happened. Their items show below.',
    noCommonItemsSetUp: 'No common items set up.',
    guidedNotes: '3. Guided notes',
    flagForManager: 'Flag for manager',
    reason: 'Reason',
    notSavedRetrying: 'Not saved: no connection. Keep this page open; retrying.',
    notSaved: 'Not saved',
    keepTheText: 'Keep the text on this screen',
    loadTheOtherVersion: 'Load the other version',
    submit: 'Submit note',
    writeYourNotes: 'Write your notes in the Guided notes box',
    writeYourNotesNotTheGuide: 'Write your notes in the Guided notes box. The grey text is only a guide.',
    guidedNotesName: 'Guided notes',
    enterAReason: 'Enter a reason for the flag',
    submitTodaysNoteFor: 'Submit today’s note for',
    submitTheNoteFor: 'Submit the note for',
    flaggedYes: 'Flagged for manager: Yes',
    flaggedNo: 'Flagged for manager: No',
    submitting: 'Submitting…',
    draftIsSaved: 'Your draft is saved.',
    moreActions: 'More actions',
    discardDraft: 'Discard draft',
    nobodyCanOpenThisDraft: 'Nobody will be able to open this draft again.',
    discarding: 'Discarding…',
    saveChanges: 'Save changes',
    savingChanges: 'Saving changes…',
    cancelling: 'Cancelling…',
    yourChangesAreStillBelow: 'Your changes are still below.',
    startAgain: 'Start again from the latest version',
  },
  /**
   * The read view of a submitted note (participant-notes.md *Read view*): the word before its submitted time in the
   * byline, as in "Written by Priya Nair · Submitted 4:42 pm", and, under "1. Goals" and "2. Common items", the words
   * for a version with none, each as the daily report has them (design.md §11.3); and, under "3. Guided notes", the
   * words for a draft with no text yet, which a manager reads (*Someone else's draft, manager*).
   */
  readView: {
    submitted: 'Submitted',
    noGoals: 'No goals set',
    noCommonItems: 'No common items set',
    nothingWrittenYet: 'Nothing written yet.',
    edit: 'Edit',
  },
  /**
   * The whole page for a refused edit (empty-loading-error.md *Whole-page messages*): `403 note.not_editable`, reached
   * by an old link or a stale screen, as the note exists and the person may read it.
   */
  cannotEditNote: {
    heading: 'You cannot edit this note',
    body: 'Only the person who wrote it, or a manager, can edit it.',
    readTheNote: 'Read the note',
  },
  /**
   * Past notes (participant-notes.md *History list*), whose caption and failure sentence are its page name: the
   * region's loading line, and Edit participant, the manager's link to the participant's page (design.md §4.4 item 1).
   */
  pastNotes: { loading: 'Loading notes…', editParticipant: 'Edit participant' },
  /**
   * The Flagged screen's To review list (flagged.md item 4 and *States*), whose failure sentence starts with the page
   * name, "Flagged notes did not load: …": the region's loading line (L2); the word before a row's flag time, as in
   * "Priya Nair · flagged 4:42 pm"; a row's line when a later edit removed the flag (L5); and the sentence in place of
   * an empty list (L6), design.md §4.6's.
   */
  flagged: {
    loading: 'Loading flagged notes…',
    flagged: 'flagged',
    flagRemovedLater: 'Flag removed in a later edit',
    noneToReview: 'No flagged notes to review.',
  },
  /**
   * A paged list's Show older (chronological-list.md *Exact copy*), one set for every paged list of notes: the button;
   * the status line once it has taken 1 s; and what did not load, as its failure sentence starts, "Older notes did not
   * load: no connection. Try again.".
   */
  showOlder: { button: 'Show older', loading: 'Loading older notes…', olderNotes: 'Older notes' },
  /**
   * A read-only tick list's state after each item's wording, for screen readers alone (checkbox-list.md
   * `TickListRead`), after a pause, as in "Makes own breakfast, ticked".
   */
  tickState: { ticked: 'ticked', notTicked: 'not ticked' },
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
   * Manage > Common items (common-items.md), whose fixed `<h1>` is its page name: the region's loading line and what
   * did not load, as its failure sentence starts; and the line under Every note's `<h2>` (component 5). Then the New
   * group form (component 7): its field's label, its button, whose busy label is `adding`, and, in the page's status
   * region, what an add did; and a group's name as its messages name it, the one for no name and, with `msg.tooLong`,
   * the one for too much (design.md §4.9 *States*), which is the rename form's label too. Then each other group's
   * Rename, beside the Move up, Move down and Archive of `listRows`, and, in the page's status region, what a rename's
   * Save did, and what a group's Archive did (component 5). Then the archived groups' section's summary, each row's
   * Restore being `restore`, and, in the page's status region, what a Restore did (component 8). Then the legend of an
   * item's Group radios in its edit form (component 6), whose Save that moves the item says `msg.itemSavedAndMoved`.
   * Each group's list's own words are `listEditor.item`.
   */
  commonItems: {
    loading: 'Loading common items…',
    items: 'Common items',
    everyNote: 'Always shown first, on every note.',
    newGroup: 'New group',
    addGroup: 'Add group',
    groupAdded: 'Group added at the end of the list',
    enterGroupName: 'Enter the group name',
    groupName: 'Group name',
    rename: 'Rename',
    groupRenamed: 'Group renamed',
    groupArchived: 'Group archived',
    archivedGroups: 'Archived groups',
    groupRestored: 'Group restored to the end of the list',
    group: 'Group',
  },
  /**
   * The words of each kind of list that `ListEditor` edits (list-editor.md *Exact copy*), a participant's goals
   * (design.md §4.8) and a group's common items (§4.9): the help text above the lists, unless they are read-only; the
   * sentence in place of a list when no row is active; and the summary of the archived rows' section. Then the add
   * form: its field's label, which a group's list follows with " in [group name]", its button, and, in the page's
   * status region, what an add did; then a row's edit form: its field's label, and what a Save did; then, in the
   * page's status region too, what an Archive and a Restore did; and a row as `msg.rowChangedWhileEditing` names it,
   * and its text as its messages name it, the one for no text and, with `msg.tooLong`, the one for too much
   * (form-validation.md).
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
      editLabel: 'Edit goal',
      saved: 'Goal saved',
      archived: 'Goal archived',
      restored: 'Goal restored to the end of the list',
      row: 'goal',
      enter: 'Enter the goal',
      text: 'Goal',
    },
    item: {
      help:
        'Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes '
        + 'apply to notes started from now on.',
      empty: 'No items in this group. It does not show on notes until it has one.',
      archivedSummary: 'Archived items',
      newLabel: 'New item',
      add: 'Add item',
      added: 'Item added to the end of the list',
      editLabel: 'Edit item',
      saved: 'Item saved',
      archived: 'Item archived',
      restored: 'Item restored to the end of the list',
      row: 'item',
      enter: 'Enter the common item',
      text: 'Common item',
    },
  },
  /**
   * What every kind of list that `ListEditor` edits shares (list-editor.md *Exact copy*): each active row's Move up and
   * Move down, with their busy label and, in the page's status region, what a press of one that is unavailable at the
   * row's end says; its Edit and Archive, whose busy label is `archiving`, as an archived row's Restore is `restore`;
   * and, above the list, focused, a write that found the list changed by someone else. Common items' group buttons
   * share the move and archive words (common-items.md component 5).
   */
  listRows: {
    moveUp: 'Move up',
    moveDown: 'Move down',
    moving: 'Moving…',
    atTop: 'Already at the top',
    atBottom: 'Already at the bottom',
    edit: 'Edit',
    archive: 'Archive',
    listChanged: 'Someone else changed this list just now. Check it, then try again.',
  },
  /**
   * Archiving and restoring a participant (participants.md B3 and B7): the page's Actions section's `<h2>` and Archive
   * participant in it, whose busy label is `archiving`, as the archived notice's Restore is `restore`; then, in the
   * alert above either, a press refused as stale (`412`) that finds the participant still as they were.
   */
  participantActions: {
    heading: 'Actions',
    archive: 'Archive participant',
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
