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

### Edit user, on the laptop

These steps use the passkey worker that step 18 set up, Active again after steps 33 to 35, a new address for them at
the testing mailbox (*Before you start*), and accounts B and C, the other two managers.

36. As account A on the laptop in Chrome, as in step 9, choose Manage, then Users, open the passkey worker's page and
    press Edit. Look for the hint "Changing the email resets sign-in and sends a setup link to the new address." under
    Email address. Type the new address in its place and press Save. Look for the worker's page, with focus on its
    `<h1>`, the new address under Email address, the Status Invited and no banner. Within 5 minutes, look for one email
    to the new address in the testing mailbox (junk included), with the subject "Set up your Grow2Notes sign-in",
    naming Grow2Notes and no other organisation, product or person, and holding one link of the form
    `https://app-grow2notes-test.azurewebsites.net/setup#u=…&t=…`; and none to the old address. Follow the link on the
    iPhone in Safari, set up with Use a passkey (recommended), and look for Today. (S00.06.06's new address and its
    setup-link email in test; users.md AC36.)
37. As account A on the laptop in Chrome, with NVDA running, open the passkey worker's page again as in step 36 and
    press Edit. Tab through the form. Look for NVDA reading Name and Email address each with its label, value and
    hint, Email address's being step 36's, then "Role, group" and "Worker, radio button, checked, 1 of 2, Writes notes
    and reads past notes". Clear Name and press Save. Look for: the page titled "Error: Grow2Notes – Edit user"; NVDA
    saying "There is a problem, heading level 2", then "Enter the person's name" as a link, which moves focus to Name.
    Type the name again, put account B's address in place of the worker's, and press Save. Look for NVDA saying "There
    is a problem, heading level 2", then "Another account already uses this email address. …" as a link. Leave by the
    back link, which saves nothing. Then make accounts B and C workers: on each one's page press Edit, choose Worker,
    listening for "Worker, radio button, checked", and press Save. Account A is now the only active manager. Open your
    own page from Users and press Edit. Look for NVDA reading, after "Edit user, heading level 1", the notice "You are
    the only active manager, so you cannot change your email or make yourself a worker. First make someone else a
    manager.", and, with Email address, the hint "Changing your email resets your sign-in. …". Choose Worker and press
    Save. Look for: NVDA saying "There is a problem, heading level 2", then "You are the only active manager, so you
    cannot make yourself a worker. Make someone else a manager first." as a link; and no `PUT /api/admin/users/…` in
    the Network panel. Leave by the back link, then make accounts B and C managers again, as you made them workers,
    and look for each one's row on Users read with "Manager". (S00.06.06's screen-reader spot check, as backlog README
    §9 asks of a changed form; users.md's NVDA pass over Edit user: its hints, the Role radios, a failed Save and the
    last-manager notice.)

### Goals, once T05 is in test

These steps hold T05's by-hand checks ([tasks.md](../design/backlog/tasks.md), T05's Notes). They need account A and
the kit of steps 26 to 28. Every goal typed is made up (A35), such as `Makes own breakfast`.

38. The database, as in step 26: run `infra/sql/check-database.sql` for test after T05's deploy. Look for PASS on
    every row, among them D3 and D5, which now name the `DENY DELETE` on `dbo.Goal` for `grow2notes_runtime`, and B7
    and B8, which list the `Goal` table and the `AddGoal` migration. (S01.02.05's first criterion, in test.)
39. Ask a manager who has not used the Participants screens before to set up a made-up participant with five goals
    on their own phone, without help, while you watch and say nothing. Write down whether they finished, how long it
    took and where they stopped or asked. (design.md §14 M1's *Done when*; F01.02's feature check.)
40. On the iPhone, as account A, open a participant with a goal and press Edit on it. Look for the keyboard opening
    at once, with the cursor after the goal's words; then press Cancel. Do the same on the Android phone.
    (participants.md open question 6; list-editor.md *Implementation notes*, "test it".)
41. With NVDA and Chrome on the laptop, VoiceOver and Safari on the iPhone, and TalkBack and Chrome on the Android
    phone, as account A, open a participant, add three goals, then on row 2 press Move up, then press Move up on
    row 1, then Edit a goal and Save it, Archive a goal and Restore it, and open and close Archived goals. Look for
    each reading in list-editor.md's *Screen reader announcements* and participants.md's *Accessibility checklist*,
    *Screen reader*, heard once each: "Move up" and the goal's words, then "button", on row 2; "Moved up to number 1
    of 3" after the press; "dimmed" or "unavailable" on row 1's Move up, and "Already at the top" when it is pressed;
    on Edit, "Edit goal", the words, then "You can enter up to 200 characters" and the help text; "Goal saved", with
    focus on Edit; "Goal archived", with focus on Restore and the goal's words; "Goal restored to the end of the
    list", with focus on Archive; "Goal added to the end of the list" after each Add, with focus still in New goal;
    and "Archived goals" with "collapsed" or "expanded". (participants.md's screen-reader pass; list-editor.md
    *Implementation notes*, the manual pass.)

### Common items, once T06 is in test

These steps hold T06's by-hand checks ([tasks.md](../design/backlog/tasks.md), T06's Notes). They need account A and
the kit of steps 26 to 28. Every common item typed is made up (A35), such as `Medication prompted`.

42. The database, as in step 26: run `infra/sql/check-database.sql` for test after T06's deploy. Look for PASS on
    every row, among them D3 and D5, which now name the `DENY DELETE` on `dbo.CommonItemGroup` and `dbo.CommonItem`
    for `grow2notes_runtime`, and B7 and B8, which list the `CommonItemGroup` and `CommonItem` tables and the
    `AddCommonItems` migration. (S01.03.01's first criterion, in test.)
43. As account A on the laptop, in Chrome, choose Manage, then Common items. Look for one group, Every note, with
    "Always shown first, on every note." under its heading. Write down whether step 3 ran before T06's deploy or
    after it: before, this shows that the migration gave the organisation that it bootstrapped its Every note group;
    after, that `admin bootstrap` made the group with the organisation. (S01.03.01's fourth criterion, or its third,
    in test.)
44. On the iPhone, as account A, open Common items, add an item to Every note if it has none, and press Edit on it.
    Look for the keyboard opening at once, with the cursor after the item's words; then press Cancel. Do the same on
    the Android phone. (common-items.md *Add, edit, rename, move*, "keyboard open on iOS Safari and Android Chrome";
    open question 7.)
45. With NVDA and Chrome on the laptop, and VoiceOver and Safari on the iPhone, as account A, open Common items, add
    three items to Every note, then on row 2 press Move up, then press Move up on row 1, then Edit an item and Save
    it, Archive an item and Restore it, and open and close Archived items. Look for each reading in common-items.md's
    *What a screen-reader user hears* and *Accessibility checklist*, *Screen reader*, heard once each: "Every note,
    heading level 2", and Every note as a region, when moving by heading and by region; "Move up" and the item's
    words, then "button", on row 2; "Moved up to number 1 of 3" after the press; "dimmed" or "unavailable" on row 1's
    Move up, and "Already at the top" when it is pressed; on Edit, "Edit item", the words, then "You can enter up to
    200 characters" and the help text; "Item saved", with focus on Edit; "Item archived", with focus on Restore and
    the item's words; "Item restored to the end of the list", with focus on Archive; "Item added to the end of the
    list" after each Add, with focus still in New item in Every note; and "Archived items in Every note" with
    "collapsed" or "expanded". T07 adds the group parts of the pass. (common-items.md *Accessibility runs*, the manual
    pass.)

### Today and the note form, once T08 is in test

These steps hold T08's by-hand checks ([tasks.md](../design/backlog/tasks.md), T08's Notes). They need account A, the
kit of steps 26 to 28, and made-up participants with two goals each, such as `Jane Citizen` with `Makes own breakfast`
and `Phones her sister`, whose notes for today nobody has started: three for each screen reader in the steps below,
nine in all. Every word typed into a note is made up (A35).

46. The database, as in step 26: run `infra/sql/check-database.sql` for test after T08's deploy. Look for PASS on
    every row, among them D3 and D5, which now name the `DENY DELETE` on `dbo.Note` for `grow2notes_runtime`, with
    none on `dbo.NoteDraft`, and B7 and B8, which list the `Note` and `NoteDraft` tables and the `AddNote` migration.
    (S02.02.02's first criterion and S02.02.07's database test, in test.)
47. As account A on the laptop, with NVDA and Chrome, choose Today. Look for each row read as the participant's name
    and "link" alone, with nothing after it, and the list's item count as you move into it, such as "list, 3 items".
    Type a name that matches nobody in Find a participant and stop typing: look for "No participant matches" and the
    name read once, after a short pause. Clear it, open the first participant's row and look for: the `<h1>` read as
    "Jane Citizen Thursday 1 October 2026, heading level 1", with today's date; "1. Goals, grouping, Ticked means
    reached." and the first goal as you Tab into Goals; and, as you tick a goal and type a sentence, "Saving…" once
    for each burst, then "Saved" and the time. Then open the second participant's note in a second tab and change
    nothing there; in the first tab, add a goal on that participant's page; back in the second tab, tick a goal and
    look for "The goal or common-item list was just changed. Please check your ticks." read once. Last, open the third
    participant's note in the second tab, again changing nothing; archive them in the first tab; type into Guided
    notes in the second tab and look for "… is archived, so this note cannot be started. Ask a manager." read once;
    then Restore them in the first tab. (today.md's *Screen reader*; note-form.md's *Announcements*, for what T08
    builds.)
48. Do all of the step above again on the iPhone with VoiceOver and Safari, then on the Android phone with TalkBack
    and Chrome, each time with three participants of its own as the first, second and third. The phone, signed in as
    account A, does what that step does on the laptop, the second tab's part included, while the laptop, as account A
    in Chrome, makes the changes that it makes in the first tab: the goal added, the archive and the Restore. Look
    for the same readings, the lists-changed and archived sentences each read once included. (note-form.md's
    *Announcements* and today.md's manual pass, on the phones.)
49. On each phone, with the screen reader off: in Find a participant, type a name and press Clear, and look for the
    keyboard staying open with the cursor still in the field; open a participant's note whose box is empty, tap into
    Guided notes and look for the guide prompts showing while the box has focus, and the top bar, with Today and the
    save status, staying in view above the keyboard; then dictate a sentence with the keyboard's microphone and look
    for it in the box and the status ending in "Saved" and the time. (A31; note-form.md open question 11.)

### Common item groups, once T07 is in test

These steps hold T07's by-hand checks ([tasks.md](../design/backlog/tasks.md), T07's Notes). They need account A and
the kit of steps 26 to 28. Every group and item typed is made up (A35), such as `Community outing`.

50. On the iPhone, as account A, open Common items, add a group if there is none besides Every note, and press Rename
    on it. Look for the keyboard opening at once, with the cursor after the group's name; then press Cancel. Do the
    same on the Android phone. (common-items.md *Add, edit, rename, move*, "Rename shows the Group name field…"; open
    question 7.)
51. As account A on the laptop, in Chrome at its usual window size and text size, open Common items with two groups
    besides Every note, each with an item. Look for each group's heading with its four buttons on one line. Press Move
    down on the first of the two, then press Edit on one of its items, choose the other group and press Save. Look for
    the two sections swapping, with focus still on that group's Move down, then the item last in the other group,
    scrolled into view, with focus on its Edit. (common-items.md *Layout*, its laptop bullet; open question 7.)
52. With NVDA and Chrome on the laptop, and VoiceOver and Safari on the iPhone, as account A, open Common items and
    Archive any group there besides Every note (those of the two steps above, and of the other pass), so that each pass
    starts with Every note alone and no group name repeats. Then add the groups `Community outing` and
    `Personal care`, rename one and rename it back, press Move up on the first of them, then Move down on it, Edit an
    item of Every note and move it into Personal care, Archive Community outing and Restore it, and open and close
    Archived groups. Look for each reading in common-items.md's *What a screen-reader user hears* and *Accessibility
    checklist*, *Screen reader*, for the groups, heard once each: "Community outing, heading level 2", and the group as
    a region, when moving by heading and by region; after Add group, focus in "New item in Community outing", then
    "Group added at the end of the list"; "Move up, Community outing group, button"; "dimmed" or "unavailable" on the
    first group's Move up, and "Already at the top" when it is pressed; on Rename, "Group name", the name, then "You
    can enter up to 200 characters"; after Save, focus on Rename, then "Group renamed"; "Group moved down" after Move
    down; on the Group radios, "Group", then "Every note, radio button, checked, 1 of 3"; after Save with another
    group, focus on the item's Edit, then "Item saved and moved to the end of Personal care"; "Group archived", with
    focus on Restore and the group's name; "Group restored to the end of the list", with focus on Archive; and
    "Archived groups" with "collapsed" or "expanded". (common-items.md *Accessibility runs*, the manual pass, its
    group parts.)

### Submit, the read view and Past notes, once T09 is in test

These steps hold T09's by-hand checks ([tasks.md](../design/backlog/tasks.md), T09's Notes). They need account A,
account B, the passkey worker of step 18, the kit of steps 26 to 28, and made-up participants with two goals each,
such as `Jane Citizen` with `Makes own breakfast` and `Phones her sister`, whose notes for today nobody has started:
one for step 54 and two for each screen reader in step 55, seven in all. Every word typed into a note is made up
(A35).

53. The database, as in step 26: run `infra/sql/check-database.sql` for test after T09's deploy. Look for PASS on
    every row, among them D3 and D5, which now name the `DENY UPDATE` and `DENY DELETE` on `dbo.NoteVersion` and
    `dbo.NoteVersionItem` for `grow2notes_runtime`, and B7 and B8, which list the `NoteVersion` and `NoteVersionItem`
    tables and the `AddNoteVersion` migration. (S02.03.01's first criterion, in test.)
54. On the iPhone in Safari, at its usual text size and held upright, sign in as the passkey worker and choose Today.
    Open a participant's note, tick a goal, type a sentence or two into Guided notes, tick Flag for manager and type a
    reason, and look for "Saving…" then "Saved" and the time. Press Submit note. Look for the confirmation with "Submit
    today's note for", the participant's full name on a line of its own, today's date and "Flagged for manager: Yes",
    with nothing cut short; press "Submit note for" and the name. Look for Today with "Note for" the name and
    "submitted" at the top, and the participant's row reading "Submitted · You ·" and the time, then Flagged. Open the
    row and look for the note as written, its goal ticked and the flag line with the reason; then press Past notes and
    look for the note's date, "Submitted", the worker's name and Flagged. (S02.03.01's manual check, design.md §14 M2
    *Done when*: a worker fills in and submits a note on a phone 375 px wide; note-form.md item 38.)
55. On the laptop as account A with NVDA and Chrome, then on the iPhone as the passkey worker with VoiceOver and
    Safari, then on the Android phone as account B with TalkBack and Chrome, each with two participants of its own:
    open the first participant's note, type a sentence and tick Flag for manager. Look for "checked, expanded", then
    Reason as the next stop, and its count, "You can enter up to 200 characters", after a pause. Leave Reason empty and
    press Submit note: look for "There is a problem" and "Enter a reason for the flag" read as the summary takes focus.
    Type a reason and press Submit note again. On the laptop, first set the Network panel's throttling to 3G, so that
    the request is slow. Look for the confirmation read as "Submit today's note for", the name, "alert dialog", today's
    date and "Flagged for manager: Yes", then its heading; press "Submit note for" and the name, and, on the laptop,
    look for "Submitting…" read after a moment (then set the throttling back to No throttling). Look for "Note for",
    the name and "submitted" read once on Today, and the row's status, "Submitted, You", the time and "Flagged", read
    after its name as its description. Open the row and look for the `<h1>`, the name and the date, read as the page
    opens, and each goal read with "ticked" or "not ticked". Press Past notes and look for its rows read as the date,
    then the status, the author and Flagged. Then submit the second participant's note unflagged and look for "Flagged
    for manager: No" in the confirmation and no Flagged on its row. Show older shows only once a participant has more
    than 30 notes: when one does, press it and look for focus on the first new row, read as its date. (note-form.md's
    *Announcements* for the flag, the summary and the confirmation; today.md's and participant-notes.md's manual
    passes, for what T09 builds.)

### Flagged's To review list, once T11 is in test

These steps hold T11's by-hand checks ([tasks.md](../design/backlog/tasks.md), T11's Notes). They need account A,
account B, the passkey worker of step 18, the kit of steps 26 to 28, and a made-up participant whose note for today
nobody has started. Every word typed into a note is made up (A35). A note flagged before T11's deploy is not in To
review, as T09 left every flag status as it was, so these steps flag a note of their own.

56. On the laptop, as account A in Chrome, and on the Android phone, as account B in Chrome, choose Flagged and leave
    it open. On the iPhone in Safari, sign in as the passkey worker, open the participant's note for today, type a
    sentence or two, tick Flag for manager, type a reason and submit the note. Then, on the laptop and on the Android
    phone, choose Today and then Flagged. Look for the row on each: the participant's full name, today's date under it,
    the worker's name, "flagged" and the time of the submit, and "Reason:" with the reason. Open the row on each and
    look for the note as written, the flag line with the reason, and "Flagged" as the back link; press it and look for
    the list again, with the row still there. (design.md §14 M3 *Done when* bullet 1, the list half, on real devices;
    S03.03.01.)
57. On the laptop as account A with NVDA and Chrome, then on the iPhone as account B with VoiceOver and Safari, then on
    the Android phone as account B with TalkBack and Chrome, choose Flagged while step 56's note is still there. Look
    for: the arrival read as "Flagged notes, heading level 1"; the row read as the participant's name and the date,
    then "link", then the worker's name and "flagged" with the time; the Reason line read when reading on; and, once
    the row is open, the back link read as "Flagged". (flagged.md *Visual and device*, the manual pass, for what T11
    builds.)

### Autosave through a lost signal, another device and the session's end, once T10 is in test

These steps hold T10's by-hand checks ([tasks.md](../design/backlog/tasks.md), T10's Notes). They need account A,
signed in on the laptop in Chrome and on each phone as in step 48, and made-up participants with two goals each, such
as `Jane Citizen` with `Makes own breakfast` and `Phones her sister`, whose notes for today nobody has started: two for
step 58, one each for steps 59 and 60, and two for each screen reader in step 62, ten in all. Every word typed into a
note is made up (A35).

58. On the iPhone in Safari, as account A, open the first participant's note, type a sentence into Guided notes and,
    before the status says "Saved", switch straight to another app. On the laptop, open the same note: look for the
    sentence, whole. Do the same on the Android phone in Chrome with the second participant. (The page-hidden save on
    a real phone: design.md §5.6 *Client behaviour*; note-form.md item 9.)
59. On the iPhone in Safari, as account A, open the third participant's note and type a sentence, then a second one,
    and close the browser in the middle of the second: swipe up and swipe Safari away. On the laptop, open the same
    note: look for the text there apart from at most the last few seconds of typing. (S02.02.06's manual check: type
    on a phone, close the browser mid-sentence, open the same note on a laptop; the text is there apart from at most
    the last few seconds. design.md §14 M2 *Done when*.)
60. On the laptop, in Chrome with the developer tools open on the Network panel, open the fourth participant's note,
    type a sentence, then at once set the Network panel's throttling to Offline. Look for "Not saved: no connection.
    Keep this page open; retrying." in the bar under Today. Leave the page untouched for 31 minutes, in front and with
    the screen kept awake: look for the warning at 28 minutes, and at 30 "You've been signed out" in the form's place,
    at the same address. Set the throttling back to No throttling and press Sign in with a passkey. Look for the note
    form again with the sentence, the status "Saved" and the time, no "This note was changed on another device or
    tab.", and one `PUT …/draft` answered `200` in the Network panel. (The app's own 30 idle minutes, then the same
    person signing in again with the text intact and no takeover banner: S02.02.05's second criterion in test;
    design.md §14 M2 *Done when*.)
61. After steps 58 to 60, on the laptop, in Chrome's developer tools, open Application and look under Storage at Local
    storage, Session storage, IndexedDB and Cache storage for the test site: look for nothing in any of them, so no
    note text, ticks or names. Then connect the Android phone to the laptop by USB, with USB debugging on, open
    `chrome://inspect` in the laptop's Chrome, inspect the phone's tab on the test site, and look in the same four
    places. (S02.02.05's manual check: after writing notes on a phone and on a laptop, local storage, session storage,
    IndexedDB and Cache Storage hold no note text, ticks or names. design.md §14 M2 *Done when*; note-form.md item 14.)
62. On the laptop with NVDA and Chrome, then on the iPhone with VoiceOver and Safari, then on the Android phone with
    TalkBack and Chrome, each as account A with two participants of its own: open the first participant's note, type a
    sentence and wait for "Saved" and the time. Turn the connection off, in the Network panel on the laptop and with
    aeroplane mode on a phone, and type another sentence: look for "Not saved: no connection. Keep this page open;
    retrying." read once, and not again while the connection stays off. Press Submit note: look for the same sentence
    read again, and nothing opening. Open Account and press Sign out: look for "Not signed out: your note is not saved
    yet. Keep this page open; retrying." read, and Today and Account still there once you close the panel. Turn the
    connection on and press nothing: look for "Saved" and the time read. Then open the second participant's note,
    type a word and wait for "Saved"; open the same note in a second tab, type a word there and wait for "Saved"; go
    back to the first tab and type a word there: look for "This note was changed on another device or tab." read once,
    with the cursor still in Guided notes, and Keep the text on this screen, then Load the other version, as the next
    two stops after Today, the back link. Press Load the other version and look for the participant's name and the
    date read as focus moves to the heading. (The first tab saves before the second opens, as a tab's first save of a
    note takes it over with no banner: design.md §5.6. note-form.md's *Announcements* for what T10 builds;
    app-shell.md's Sign out with a note not saved.)

### Discard and unfinished drafts, once T12 is in test

These steps hold T12's by-hand checks ([tasks.md](../design/backlog/tasks.md), T12's Notes). They need account A,
account B, the passkey worker of step 18, the kit of steps 26 to 28, and made-up participants with two goals each, such
as `Jane Citizen` with `Makes own breakfast` and `Phones her sister`, whose notes for today nobody has started: one each
for steps 63 and 64, three for step 65 and two for each screen reader in step 66, eleven in all. Step 65 runs over two
days. Every word typed into a note is made up (A35).

63. On the iPhone in Safari, as the passkey worker, open the first participant's note, type a sentence and wait for
    "Saved" and the time. Press More actions, then Discard draft. Look for the dialog asking "Discard the draft note
    for" and the participant's full name, then "Nobody will be able to open this draft again.", a red "Discard draft
    for" and the name, and Go back under it. Press Go back and look for the dialog gone and More actions still there.
    Open the dialog again and press the red button. Look for Today with no message at the top and the participant's
    row with no status under the name. Open the row again: look for an empty note; type a sentence and look for "Saved"
    and the time. (S02.02.08 on a real phone; design.md §14 M2 *Done when*: after a discard, a new note can.)
64. On the iPhone in Safari, as the passkey worker, open the second participant's note, tick a goal, type a sentence,
    tick Flag for manager, type a reason and wait for "Saved". Then, on the laptop as account A in Chrome, choose Today
    and open that participant's row. Look for the worker's name, "started today's note for", the participant's name,
    the time and "Only", the worker's name, "can finish it."; then Discard draft and Past notes; then the goal ticked
    and the other not, the sentence as typed, and Flag for manager ticked with "Reason:" and the reason under it, and
    no Flagged tag. Press Discard draft: look for the worker's name, "started it at" and the time in the dialog, then
    press the red button. Look for Today with the row showing no status. Then on the iPhone choose Today, and look for
    the row with no status there too. (S02.04.03 on real devices: a manager reads a worker's draft and discards it.)
65. On the first day, as account A on the laptop in Chrome, as the passkey worker on the iPhone in Safari and as
    account B on the Android phone in Chrome, each opens one of the three participants' notes, types a sentence and
    waits for "Saved"; leave each unsubmitted. On the next day, with NVDA, VoiceOver and TalkBack on in turn, choose
    Today on each. Look for "Your unfinished drafts, heading level 2" above the list, then the participant's row read
    as the name, "link", then yesterday's date in full, such as "Wednesday 30 September 2026", with no status. Open
    the row: look for "This draft is for", yesterday's date, "Submitting it now keeps that date." under the heading,
    and "Draft · saved" with yesterday's date and the time. Press Submit note: look for the dialog read as "Submit the
    note for", the name and "alert dialog", never "today's"; submit it, and look for "Note for", the name and
    "submitted" read on Today, with Your unfinished drafts gone. (S02.01.04 and S02.03.03 on real devices across a
    real midnight in Melbourne; today.md's *Screen reader* items for the section.)
66. On the laptop with NVDA and Chrome as account A, then on the iPhone with VoiceOver and Safari as the passkey
    worker, then on the Android phone with TalkBack and Chrome as account B, each with two participants of its own:
    open the first participant's note, type a sentence and wait for "Saved". Tab past Submit note: look for "More
    actions, button, collapsed", then "expanded" as you press it, and Discard draft as the next stop. Press Discard
    draft: look for "Discard the draft note for", the name and "alert dialog", then "Nobody will be able to open this
    draft again.". Press Go back and look for focus on More actions, read as it lands there. Open the dialog again,
    press "Discard draft for" and the name, and look for Today's heading read as it opens. Then have someone else
    start the second participant's note on another device, ticking Flag for manager with a reason: the passkey worker
    on the iPhone for the laptop's and the Android phone's passes, and account A on the laptop for the iPhone's. Open
    that participant's row. On the iPhone, as a worker, look for the sentence ending "can finish it." and Past notes,
    and nothing more, no Discard draft among it. On the laptop and the Android phone, as managers, look for the same
    sentence, then Discard draft, Past notes, each goal read with "ticked" or "not ticked", and "Flag for manager,
    ticked" with the reason after it; press Discard draft and look for the dialog's "started it at" line read with the
    other person's name and the time, then Go back, and focus back on Discard draft. (note-form.md's *Announcements*
    for More actions and the Discard dialog; participant-notes.md's manual pass for someone else's draft.)

### Common items and group picks, once T13 is in test

These steps hold T13's by-hand checks ([tasks.md](../design/backlog/tasks.md), T13's Notes). Unfinished drafts saved
before this release hold no common items, as their snapshot predates them: discard them before these steps rather than
finish them. The steps need the passkey worker of step 18, the kit of steps 26 to 28, and made-up participants with two
goals each, whose notes for today nobody has started: one for step 68 and one for each screen reader in step 69, four in
all. Every word typed into a note is made up (A35).

67. The database, as in step 26: run `infra/sql/check-database.sql` for test after T13's deploy. Look for PASS on every
    row, among them D3 and D5, which now name the `DENY UPDATE` and `DENY DELETE` on `dbo.NoteVersionGroup` for
    `grow2notes_runtime`, and B7 and B8, which list the `NoteVersionGroup` table and the `AddNoteVersionGroup`
    migration. (S02.05.01's first criterion, in test.)
68. On the iPhone in Safari, at its usual text size and held upright, repeat step 54 with common items. A manager has
    first set up Every note with an item and two groups with an item or two each, such as `Community outing`. Sign in
    as the passkey worker and open a participant's note: tick a goal, an Every note item and one group, tick an item in
    that group, type a sentence or two into Guided notes, and look for "Saving…" then "Saved" and the time. Press
    Submit note and look for the confirmation with nothing cut short, and nothing in it about groups; press "Submit
    note for" and the name. Open the participant's row: look for the note as written with "2. Common items" between
    the goals and Guided notes, Every note and the ticked group each with its items ticked or not, and the other group
    nowhere. Nothing in the form or the read view runs past the screen's edge. (F02.03's step-54 check repeated once
    F02.05 is done; S02.05.02 and S02.05.04 on a real phone.)
69. On the laptop with NVDA and Chrome, then on the iPhone with VoiceOver and Safari, then on the Android phone with
    TalkBack and Chrome, each as a worker with its own participant whose note for today nobody has started: open the
    note and move into "2. Common items". Look for the picker's legend, "Which of these happened?", and its hint, "Tick
    all that happened.", read on entering it. Tick a group: look for "expanded" read for it, and "collapsed" again as
    you untick it (TalkBack's support is unverified: note what it reads). With a group unticked, Tab or swipe through
    the form: look for the group's items skipped. Tick it again and look for its items, unticked. With Voice Control
    or Voice Access, say "Tap" and a group's name and look for its box ticking. Then repeat on the laptop at a window
    320 px wide and at 200% text size, looking for nothing cut off or hidden, and with a Windows contrast theme on,
    looking for every box and tick still drawn. (group-picker.md's *Test (M6)*; S02.05.02's manual check.)

Two checks of earlier steps now cover more, and are not repeated. Step 62's Load the other version also replaces the
group ticks with the other version's. Step 64's manager, who reads a worker's draft, now also finds "2. Common items"
between the goals and Guided notes, with the groups the worker has ticked and their items ticked or not (S02.04.03).
