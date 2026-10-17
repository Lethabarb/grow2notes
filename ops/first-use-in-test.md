# First use in test

One ordered checklist of every check by hand still outstanding in test,
`https://app-grow2notes-test.azurewebsites.net`, for the first time the owner signs in there. On 12 October 2026 the
owner decided that checks like these are nice-to-haves that do not hold a story open (backlog README §9): the stories
are done on their checks in CI and their read-only checks after the deploy, and the criteria that only these steps can
prove stay unticked, each with a Note pointing here. Each step ends with what it confirms.

## Before you start

- Test holds made-up data only (A35): made-up names, such as `Sample Support Services` and `Alex Sample`, and a new
  `example.org` address for each account that `admin bootstrap` creates, such as `alex.sample.20261013@example.org`.
  An account invited from the Users screen gets its setup link by email instead, which an `example.org` address never
  receives, so it gets a new address at a mailbox kept only for testing; if the mailbox takes `+` tags, as Gmail and
  Outlook.com do, `name+20261015c@…` gives each account its own.
- Test sends that email from an Azure-managed domain, which allows 5 emails a minute and 10 an hour for the whole
  subscription, with no way to raise either. An invite or a resend over that says "Not sent: something went wrong. Try
  again." and changes nothing: wait a minute, or until the hour is up, and press again. Steps 12 and 17 send five
  between them.
- Have ready: a laptop with Chrome, Edge and NVDA, an iPhone with VoiceOver and an authenticator app (such as Google
  Authenticator or Microsoft Authenticator), the testing mailbox open on both, and the Azure portal signed in with
  your own account. Let each browser keep the name and email address it already offers on forms of your own, as
  steps 12, 13 and 18 check that Invite user offers neither.
- In Chrome, open the developer tools on the Network panel, with **Preserve log** on, before each step that names a
  request.
- Write down each step's time and result (passed, or what you saw) for the developer, who records them in the
  stories' Notes. Never write down a setup link, a token, a password, a code, an IP address or an email address.
- If a step says "no connection" soon after a quiet hour, the test database is waking up: wait a minute and try again.

## Steps

### The first account: a passkey, on the laptop

1. Open `/healthz/ready` until it answers `200`, which wakes the test database.
2. Open the web app's SSH console and run its check of the six settings by name, as
   [break-glass.md](break-glass.md) *The SSH console* says. All six must say `set`. If any says `MISSING`, or the way
   into the console fails, stop, run nothing and tell the developer.
3. Run `admin bootstrap` as [break-glass.md](break-glass.md) says, for a made-up organisation and first manager
   (account A). Look for `Exit code: 0` after one printed line, a link of the form
   `https://app-grow2notes-test.azurewebsites.net/setup#u=…&t=…`. Leave the console open for step 5. (S00.03.05's first
   criterion.)
4. Copy the link straight into Chrome's address bar on the laptop, with NVDA running, and choose Use a passkey
   (recommended). Look for: setup ends on Today, at `/`; the `Set-Cookie` of the `200` that
   `POST /api/auth/setup/passkey` answers is `__Host-grow2notes` with `Secure`, `HttpOnly`, `SameSite=Strict` and
   `Path=/`, and no `Domain`, `Expires` or `Max-Age`; and NVDA reads views D and E as sign-in.md's *Accessibility
   checklist* says. (S00.04.01's `Set-Cookie` criterion; S00.04.02; sign-in.md's screen-reader pass.)
5. Copy the link from the console into a new tab once more. Look for "This link has expired". Then type `exit` in the
   console and close its tab, so the link is kept nowhere. (S00.04.02.)
6. Restart `app-grow2notes-test` from the portal. Once `/healthz` answers `200` again, reload the Today tab. Look for:
   Today again, with no sign-in asked, and `GET /api/auth/me` answering `200`. (S00.02.02's restart criterion.)
7. Sign out from Account. Look for: `POST /api/auth/logout` answering `204` with
   `Clear-Site-Data: "cache", "cookies", "storage"`, then the sign-in page, and `GET /api/auth/me` answering `401`.
   (S00.04.06; F00.04's feature check.)
8. On the sign-in page, press nothing at first. Look for `GET /api/auth/antiforgery` answering `204`, and
   `POST /api/auth/passkey/options` answering `200` JSON with `Cache-Control: no-store` and the test host as `rpId`.
   Then type a made-up `example.org` address that no account holds and any password, and press Continue. Look for
   "Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes." in the
   summary, and `POST /api/auth/login` answering `401` problem+json with `no-store`. (S00.04.04; S00.04.05.)
9. Sign in as account A with Sign in with a passkey. Look for Today, and NVDA reading the sign-in page as sign-in.md's
   *Accessibility checklist* says. (S00.04.04; F00.04's feature check, the second sign-in on the passkey path.)
10. Choose Manage, then Users, with NVDA running. Look for: `GET /api/admin/users` answering `200`; the page titled
    "Grow2Notes – Users", with Manage marked; one row, account A's own, with its name, its address and "Active ·
    Manager", and no "Deactivated" heading; and NVDA, tabbing to the row, reading it as users.md's *Accessibility
    checklist* says, the name, "link", then the address, "Active" and "Manager", with no middle dot. Press the row. Look
    for its detail page, titled "Grow2Notes – User", with the name as its heading, then Email address, Role "Manager",
    Status "Active" and Edit, and neither the name nor the address in the title or the address bar. Then choose Today.
    (S00.06.01, the first real list in test; users.md's screen-reader pass over the list.)
11. Leave Today untouched for 28 minutes, in front and with the screen kept awake. Look for: the warning opens with
    focus on Stay signed in; NVDA says "You'll be signed out in 2 minutes", "dialog" and Stay signed in; left open, its
    sentence changes at 29:00, 29:20 and 29:40. Before 30:00 press Stay signed in. Look for `POST /api/auth/ping`
    answering `204`, the warning closing and focus back where it was. Then choose Manage, Users and Invite user, type a
    made-up name and address, and leave the page untouched for 31 minutes. Look for: at 30:00, "You've been signed
    out" in the form's place, at the same address, with no nav or Account, and NVDA saying "You've been signed out,
    heading level 1"; then, after Sign in with a passkey, Invite user again, with what you typed, NVDA on its heading
    and the address unchanged. Sign out as in step 7. (S00.04.07 and S00.04.08; their screen-reader spot checks, and
    the session that the app itself ends after 30 idle minutes.)

### The second account: a password and an authenticator app

12. Sign in as account A with Sign in with a passkey, then choose Manage, Users and Invite user, with NVDA running.
    Look for: the page titled "Grow2Notes – Invite user", with Worker chosen; clicking into Name and then Email
    address fills in nothing by itself and offers none of your own names or addresses, a list of suggestions being
    acceptable only if nothing is filled in until you choose from it; and NVDA reading each field's label and hint,
    then "Role, group" and "Worker, radio button, checked, 1 of 2, Writes notes and reads past notes". Press Send
    invite with both fields empty, and look for NVDA saying "There is a problem, heading level 2", then the two
    messages as links. Then invite account B, a manager, with a made-up name and a new address at the testing mailbox
    (*Before you start*), and look for: the Users list with NVDA saying "Users, heading level 1", no banner, and B's
    row read, as you tab to it, as its name, "link", its address, "Invited" and "Manager". Sign out as in step 7.
    (S00.06.02, the first invite in test; users.md AC17 in Chrome, and its screen-reader pass over Invite user.)
13. Open account B's email in the testing mailbox and follow its link in Chrome on the laptop, with NVDA running.
    Choose Use a password and authenticator app, create a password, then scan the QR code with the phone's
    authenticator app and enter its 6-digit code. Look for: setup ends on Today, and NVDA reads views D to G as
    sign-in.md's *Accessibility checklist* says. Follow the email's link once more and look for "This link has
    expired", then sign out as in step 7. Then, in Edge, sign in as account B with email, password and a 6-digit code,
    choose Manage, Users and Invite user, and click into Name and then Email address: look for the same as in step 12,
    nothing filled in by itself. Sign out. (S00.04.03's and S00.04.05's screen-reader spot checks, handed to F00.04's
    feature check; users.md AC17 in Edge.)
14. On the iPhone, with VoiceOver on, sign in as account B with email, password and a 6-digit code. Look for Today,
    and VoiceOver reading the sign-in page and the code step as sign-in.md's *Accessibility checklist* says. Sign out
    with Sign out. (S00.04.05; F00.04's feature check, the phone, and the second sign-in on the password path.)

### What the developer reads afterwards (read-only)

15. The audit rows, as the Entra admin, through a temporary firewall rule for your own address on
    `sql-grow2notes-test`, deleted straight after, as `infra/sql/check-database.sql` describes:

    ```sql
    SELECT EventType, OccurredAtUtc, ActorUserId, EntityType, IpAddress
    FROM dbo.AuditEvent
    WHERE EventType IN ('admin.bootstrap', 'auth.setup_completed', 'auth.signed_out')
    ORDER BY OccurredAtUtc;
    ```

    Look for: one `admin.bootstrap` row with no actor for each run; each `auth.setup_completed` row's `IpAddress` is
    your own address, the one the firewall rule names, not a private or `169.254.` address of App Service's front end;
    and one `auth.signed_out` row for each sign-out, with the person as actor. Do not write the addresses down.
    (S00.03.03's `IpAddress` criterion; S00.03.05's first criterion; S00.04.06.)
16. The telemetry, which an agent can query: from step 8's time, the workspace holds its `POST /api/auth/login` `401`
    and one "Password sign-in with an unknown email" entry holding no address, and no `400`, `415` or `5xx` on
    `/api/auth/*`, and no exception. (S00.04.05.)

### More accounts, invited from Users

17. F00.04's feature check also asks for a worker on each setup path, and for a manager's passkey on a phone, since
    account A's is on the laptop, each setting up and signing in a second time on a phone and a laptop. As account A,
    invite from the Users screen (S00.06.02) two workers and a third manager (account C), each with a made-up name and
    a new address at the testing mailbox (*Before you start*). Open the first invite's email on the laptop, leaving its
    link for step 18. Look for: it arrives within 5 minutes (look in junk too, and say which folder it was in), from a
    `DoNotReply@…azurecomm.net` address, with the subject "Set up your Grow2Notes sign-in"; its text names Grow2Notes
    and no other organisation, product or person, and holds one link, of the form
    `https://app-grow2notes-test.azurewebsites.net/setup#u=…&t=…`; and its headers (Show original in Gmail, View
    message source in Outlook.com) show `spf=pass`, `dkim=pass` and `dmarc=pass` in `Authentication-Results`.
    (S00.02.07's third criterion, and its first seen in test.) Then, with NVDA running, open the second worker's page
    from Users and press Resend invite. Look for: NVDA saying "Setup link sent to", the worker's address, the time
    and "The old link no longer works.", with focus left on Resend invite; and a second email to that worker within 5
    minutes, whose link step 18 follows in place of the first one's. (S00.06.03, the first resend in test; users.md's
    screen-reader pass over Resend invite.)
18. On the iPhone, with VoiceOver on, open each of the three invites' emails in the testing mailbox and follow its
    setup link. Set up account C and one worker with Use a passkey (recommended), and the other worker with Use a
    password and authenticator app, typing the setup key into the phone's authenticator app. Look for: each setup
    ends on Today, and VoiceOver reads views D to G as sign-in.md's *Accessibility checklist* says. Then, as account C
    in Safari, choose Manage, Users and Invite user, and look for the same as in step 12: nothing filled in by itself
    in Name or Email address, and VoiceOver reading the fields, the Role radios and, after Send invite with both
    fields empty, the summary as step 12 lists them. Sign each out with Sign out. (F00.04's feature check: setup on
    the phone, and its VoiceOver pass over views D to G; users.md AC17 in Safari, and its VoiceOver pass over Invite
    user.)
19. Sign each of the three in a second time on the laptop: the passkey ones with Sign in with a passkey, choosing the
    iPhone when Chrome offers it, and the password worker with email, password and a 6-digit code. Look for Today each
    time, then sign out as in step 7, and repeat step 15's query for their `auth.setup_completed` and
    `auth.signed_out` rows. (F00.04's feature check: the second sign-ins, on the laptop.)

### Later, once the note form and the Report screen are in test

These steps hold the by-hand checks of four stories cut on 14 October 2026 (D87, [tasks.md](../design/backlog/tasks.md)
*Cut stories*); T26 orders them with the other by-hand accessibility steps. Besides the kit in *Before you start*,
they need an Android phone with Chrome, Samsung Internet and TalkBack, and Dragon on the laptop if you have it.

20. As a manager, on the Report screen, download a day's Word file and PDF on each of iPhone Safari, iPhone Chrome,
    Android Chrome and Samsung Internet. Look for each file saved. On the iPhone, choose a future date in the picker
    and look for the error once you leave the field; on Android, pick a date in the dialog and look for it to take
    effect within about a second, without leaving the field. If a browser cannot save a file, tell the developer: the
    plain-link contingency in file-download.md is then built for that case. (S04.02.05; daily-report.md's *Real
    devices* criteria.)
21. With VoiceOver on the iPhone (Safari), TalkBack on the Android phone (Chrome) and NVDA on the laptop (Chrome), step
    through the days on the Report screen, download on a day with notes and press a download on a day without. Look
    for each item of daily-report.md's *Accessibility checklist*, *Screen reader*, heard as written. (S04.02.05,
    S06.02.04.)
22. With the same three screen readers, write, check and submit a note, and discard a draft, going through the items
    of note-form.md's *Accessibility checklist*, *Manual test passes (M6)*, and group-picker.md's *Test (M6)*. Look
    for each item heard as written, the Submit and Discard dialog titles read once, and the loading, error and retry
    states announced. On the laptop, dictate into Guided notes with Windows voice typing (Windows key + H) and with
    Dragon, and look for the note's save status saying it saved while the cursor stays in the box. If it does not,
    tell the developer: guided-notes-textarea.md's 1-second check (*Behaviour* item 6) is then added. (S06.02.03.)
23. The managers arrange five of the provider's workers, including some who write in English as an additional
    language. In test, with made-up data (A35), each says what every primary button will do before pressing it.
    Record, for each button, how many understood it; a button that fewer than 4 of the 5 understood goes to the owner
    as a wording question, and no wording changes without the owner's approval. This step is also on the go-live run
    sheet that T22 writes. (S06.02.05; microcopy.md §8 *Testing the words*.)

### Guide prompts, on the laptop and both phones

These steps need the Android phone, with Chrome and TalkBack, as well as the kit in *Before you start*. Type only
made-up prompts: every worker in the organisation sees them in every empty note.

24. On the laptop, sign in as account A as in step 9, with NVDA running, and choose Manage, then Guide prompts. Do the
    same as account B on the iPhone, with VoiceOver in Safari, and on the Android phone, with TalkBack in Chrome,
    signing in with email, password and a 6-digit code as in step 14. With each screen reader, once, do the following
    in order, and look for each announcement of guide-prompts.md's *Interactions and focus*, heard once each:
    - Arrive on the page. Look for "Guide prompts, heading level 1", though skip this one until
      [tasks.md](../design/backlog/tasks.md) T02, *Keep my place across screens*, is in test, as nothing moves focus to
      the heading on arrival before then.
    - Move into the box. Look for its name, its text, then the help text and "You can enter up to 1,000 characters".
    - Type or paste until the box holds 900 characters or more, then stop. Look for the count about a second after
      typing stops, never at each key.
    - Paste in more than 1,000 and press Save. Look for the error, with focus moved to the box.
    - Cut it back to 1,000 or fewer and press Save, then Save again with nothing changed. Look for "Saved" and the time
      after each of the two Saves.
    - Open Guide prompts in a second tab, type a word there, press Save and close that tab. Back in the first tab, type
      a word and press Save. Look for "Another manager saved the guide prompts while you were editing…", with focus
      still on Save. The second tab is the other manager: accounts A and B are in different organisations, so neither
      can cause the other's conflict.
    - Turn the connection off: on the laptop, choose Offline in the Network panel's throttling menu; on a phone, turn
      on Airplane Mode with Wi-Fi off. Press Save. Look for "Not saved: no connection. Try again.", with focus still on
      Save. Then turn the connection back on.
    - Choose Today, and in the dialog Stay on this page. Then choose Account, then Sign out, and in the dialog Stay on
      this page. Look for, as the dialog opens each time, "Leave without saving?", that it is a dialog, "Your changes
      to the guide prompts are not saved." and "Stay on this page"; and, after Stay, focus back on Today, then on Sign
      out.
    - Choose Today again, and in the dialog Leave without saving. Look for Today's heading, though skip this one until
      T02 is in test, as for the heading on arrival.
    - Turn the connection off as before, then choose Manage, then Guide prompts. Look for "The guide prompts did not
      load: no connection. Try again.", which "Loading guide prompts…" may come just before. Press Try again. Look for
      "Loading…" while it waits, then "The guide prompts still did not load: no connection. Try again.", with focus
      still on Try again. Turn the connection back on and press Try again. Look for focus on the heading, with the box
      below it.
    - On the laptop alone, add a profile with a latency of 3,000 ms to the Network panel's throttling menu, and choose
      it. Choose Manage, then Guide prompts. Look for "Loading guide prompts…" about a second after you arrive. Type a
      word and press Save. Look for "Saving…", then "Saved" and the time. Then choose No throttling. Only the laptop
      can slow the connection, so these two are heard with NVDA alone: with VoiceOver and TalkBack, the load-failed
      sentence and "Saved", which the same two regions carry, stand for them.

    (S01.04.01; S01.04.02; guide-prompts.md's *Accessibility checklist*, its five *Screen reader* items, and
    *Accessibility runs*, its screen-reader bullet.)
25. On each phone, signed in as in step 24 with the screen reader off, choose Manage, then Guide prompts, and type a
    word, so that the text is not saved. Go back with the phone's own gesture: on the iPhone, swipe in from the left
    edge of Safari; on the Android phone, the back gesture, or Back. Look for the leave dialog, with focus on Stay on
    this page, and after Stay, the page, its address and your text as they were. Go back the same way again and
    choose Leave without saving, and look for Manage. Then type a word again, reload the page from the browser's
    menu, and then close the tab. Write down, for each phone, whether the gesture brought the dialog or left the page
    without it, and whether the browser showed a warning of its own for the reload and for the close; a phone may
    show none (MDN), which the developer records rather than fixes. (S01.04.02's Note; guide-prompts.md *Open
    questions* and *Accessibility runs*.)

### Participants, once T02 is in test

These steps hold T02's by-hand checks ([tasks.md](../design/backlog/tasks.md), T02's Notes). They need account A,
a manager, and, besides the kit in *Before you start*, an Android phone with Chrome and TalkBack. Each participant
added is made up (A35), such as `Siobhan O'Brien`, with an apostrophe in the family name, born on a made-up date.

26. The database, as the Entra admin, through a temporary firewall rule for your own address on `sql-grow2notes-test`,
    deleted straight after, as `infra/sql/check-database.sql` describes: run the script for test as its header says.
    Look for PASS on every row, among them D3 and D5, which now name the `DENY DELETE` on `dbo.Participant` for
    `grow2notes_runtime`, and B7 and B8, which list the `Participant` table and the `AddParticipant` migration.
    (S01.02.01's first criterion, in test.)
27. As account A on the laptop, with NVDA and Chrome, choose Manage, then Participants. Add a participant, correct the
    date of birth, go back to the list with the back link, open their row, then press Archive participant and then
    Restore. Look for NVDA reading each item of participants.md's *Accessibility checklist*, *Screen reader*: the
    `<h1>` on each screen change, the name as "heading level 1" on the participant's page; their row again after the
    back link and after Back; "Saved" and the time once for each Save; the date of birth's group and hint on the Day
    field; the archived notice read exactly once, as it takes focus after Archive, then "Restore, button" at the next
    Tab; and the views as "Participants, navigation, list, 2 items, Active, current page, link". Then type a name that
    matches nobody in Find a participant and stop typing: look for "No participant matches" and the name read once,
    after a short pause, and not again while you type. (participants.md's screen-reader pass; search-filter.md's
    500 ms pause.)
28. Do step 27 again on the iPhone with VoiceOver and Safari, and on the Android phone with TalkBack and Chrome, with a
    second made-up participant on each. On each phone, also look for: Clear in Find a participant leaving the keyboard
    open, with the cursor still in the field; and no autofill, saved name or saved date offered on Given name, Family
    name, Day, Month and Year, nor on Find a participant. (participants.md open question 6.)

### Break-glass, from the SSH console

These steps run the two break-glass commands in test as [break-glass.md](break-glass.md) says, on accounts set up in
the steps above. Step 30 signs out everyone in test, so run these when no one else is using it.

29. On the laptop, sign in as account C (step 18) in Chrome, and on the iPhone, in Safari, so that C has two sessions,
    and leave Today open on each. You are the organisation here, and you know who account C is, which in production
    the organisation confirms before the command is run. Open the web app's SSH console, check its six settings as in
    step 2, and run `admin reset-signin` for account C's address as [break-glass.md](break-glass.md) says. Look for
    `Exit code: 0` after one printed line, a link of the form
    `https://app-grow2notes-test.azurewebsites.net/setup#u=…&t=…`. On each device, choose Manage, then Users: look
    for "You've been signed out". Copy the link into a new tab in Chrome on the laptop and choose Use a passkey
    (recommended): look for setup ending on Today. Wait 5 minutes and look for no new email to account C in the
    testing mailbox, junk included. Then paste the block again with a made-up `example.org` address that no account
    holds, and look for `Refused: No account has that address. Nothing was written.` and `Exit code: 1`. Leave the
    console open for step 30. (S00.03.06's first criterion, in test.)
30. Sign in as account A on the laptop in Chrome, as in step 9, and as account B on the iPhone, as in step 14, and
    leave Today open on each. Run `admin signout-all` as [break-glass.md](break-glass.md) says. Look for
    `Exit code: 0` with nothing printed before it. On each device, choose Manage, then Users: look for "You've been
    signed out". Then sign each in again, A with Sign in with a passkey and B with email, password and a 6-digit code,
    and look for Today each time. Type `exit` in the console and close its tab, so step 29's link is kept nowhere.
    (S00.03.06's second criterion, in test.)
31. The developer reads the audit rows as in step 15:

    ```sql
    SELECT EventType, OccurredAtUtc, ActorUserId, EntityType, OrganisationId, IpAddress
    FROM dbo.AuditEvent
    WHERE EventType IN ('admin.signin_reset', 'admin.signout_all')
    ORDER BY OccurredAtUtc;
    ```

    Look for: one `admin.signin_reset` row from step 29, with no actor, the `EntityType` `User` and no `IpAddress`,
    and none from its refused run; and, from step 30, one `admin.signout_all` row for each organisation in test, each
    with no actor and the `EntityType` `Organisation`. (S00.03.06, the audit events in test.)

### Reset sign-in, Deactivate and Reactivate, on the laptop and both phones

These steps use the two workers that step 18 set up, the password one and the passkey one, and the Android phone,
with Chrome and TalkBack, as well as the kit in *Before you start*. The worker whose sign-in is reset sets up again
from the email, so keep the testing mailbox open.

32. Sign the password worker in on the laptop in Edge and on the iPhone in Safari, with email, password and a 6-digit
    code, and leave Today open on each. On the laptop in Chrome, sign in as account A as in step 9, with NVDA running,
    choose Manage, then Users, and open the password worker's page. Press Reset sign-in. Look for NVDA saying "Reset
    sign-in for", the worker's name, "alert dialog", then both lines of the dialog, the second ending with the worker's
    address, and the heading once, with no ring drawn round it. Press Go back: look for focus back on Reset sign-in, and
    the Status still Active. Press Reset sign-in again, Tab to the red "Reset sign-in for …" and press it. Look for:
    NVDA saying "Status, Invited", with focus on the Status row; Resend invite in place of Reset sign-in; and no banner.
    On each of the worker's devices, reload the page: look for the sign-in page. Within 5 minutes, look for one email to
    the worker in the testing mailbox (junk included), with the subject "Set up your Grow2Notes sign-in", naming
    Grow2Notes and no other organisation, product or person, and holding one link. Follow it on the iPhone, set up with
    Use a password and authenticator app, and sign in once more with a code from the app. (S00.06.04's two devices and
    its email in test; users.md AC4 for the reset email, AC24 and AC25, and its NVDA pass over the Reset sign-in
    dialog.)
33. Sign the passkey worker in on the iPhone in Safari, with the passkey, and on the laptop in Chrome, choosing the
    iPhone when Chrome offers it, and leave Today open on each. On the iPhone, in Chrome, which keeps its own cookies
    apart from Safari's, sign in as account B with email, password and a 6-digit code, with VoiceOver on, choose Manage,
    then Users, and open the passkey worker's page. Press Deactivate. Look for VoiceOver saying "Deactivate", the name,
    then design.md's words, "… will be signed out everywhere now and can't sign in. Their notes stay.", and the blue
    "Deactivate …". Confirm. Look for VoiceOver saying "Status, Deactivated", and Reactivate as the only action under
    the rule. On the laptop, reload the worker's tab: look for the sign-in page; in the worker's Safari tab, reload:
    look for the sign-in page, and that signing in with the passkey fails. Back as account B, press Reactivate. Look for
    "Reactivating…" if it takes more than a moment, then VoiceOver saying "Status, Active", with Reset sign-in and
    Deactivate back. Sign the worker in again with the passkey, and look for Today. Wait 5 minutes and look for no email
    to the worker from either change. (S00.06.05's two devices in test; users.md AC26, AC28, and its VoiceOver pass over
    the Deactivate dialog and Reactivate.)
34. On the Android phone, with TalkBack on, sign in as account B in Chrome, as in step 24, choose Manage, then Users,
    and open the passkey worker's page. Press Reset sign-in, listen to the dialog as in step 32, and press Go back;
    then press Deactivate, listen as in step 33, and press Go back. Look for TalkBack reading each dialog's title,
    "alert dialog" and its words, and focus back on the button that opened it each time, with the Status still Active.
    (users.md's TalkBack pass over both dialogs.)
35. On the Android phone, with TalkBack off, open the passkey worker's page as in step 34. Press Reset sign-in, then go
    back with the phone's back gesture, or Back. Look for: the dialog closes, the page and its address stay, the Status
    is still Active, and no email reaches the worker. Do the same with Deactivate. To see where focus went, connect the
    phone to the laptop by USB with USB debugging on, open `chrome://inspect` in Chrome on the laptop, inspect the tab,
    and run `document.activeElement.textContent` in its console after each close: look for "Reset sign-in", then
    "Deactivate". Then, in the inspected tab's Network panel, choose a throttling profile with a latency of 3,000 ms,
    adding it as in step 24 if it is not there; keep it well under 10 seconds, after which the app gives up on a
    request that the server may still carry out. Press Deactivate and its "Deactivate …", then the back gesture at
    once. Look for: the dialog stays open with both buttons unchanged, and "Deactivating…" under them after a second;
    once it ends, the Status reads Deactivated. Choose No throttling and press Reactivate. Do the same with Reset
    sign-in on the password worker, whose sign-in it resets: then follow the new email's link on the iPhone and set the
    worker up again as in step 32. Write down, for each dialog, whether the back gesture closed it and whether it did
    nothing while busy.
    (users.md AC24 and AC31, the Android back gesture, which Playwright's Escape stands in for in CI.)
