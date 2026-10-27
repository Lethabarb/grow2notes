# First use in test

One ordered checklist of every check by hand still outstanding in test,
`https://app-grow2notes-test.azurewebsites.net`, for the first time the owner signs in there. On 12 October 2026 the
owner decided that checks like these are nice-to-haves that do not hold a story open, and on 20 October 2026 that
nothing is checked against the deployed site after a deploy (backlog README §9, decisions.md D90, design.md A98): the
stories are done on CI, which runs every test on every pull request, and these steps are by hand, hold nothing open
and are not run by an agent. The criteria that only these steps can prove stay unticked, each with a Note pointing
here. Each step ends with what it confirms.

## Before you start

- Test holds made-up data only (A35): made-up names, such as `Sample Support Services` and `Alex Sample`, and a new
  `example.org` address for each account that `admin bootstrap` creates, such as `alex.sample.20261013@example.org`.
  An account invited from the Users screen gets its setup link by email instead, which an `example.org` address never
  receives, so it gets a new address at a mailbox kept only for testing; if the mailbox takes `+` tags, as Gmail and
  Outlook.com do, `name+20261015c@…` gives each account its own.
- Test sends that email through Resend's free plan (D92). Confirmed on Resend's pricing page on 25 October 2026 (a page
  read): 3,000 emails a month and 100 a day, for 3 sending domains in all, and 30 days of searchable logs; it gives no
  per-second figure for the free plan. Nothing here comes near 100 a day, but an invite or a resend over a limit says
  "Not sent: something went wrong. Try again." and changes nothing: wait and press again. Check the page again if a
  limit seems to have changed. The email is sent from the address in the `test` environment's `EMAIL_SENDER_ADDRESS`,
  and a missing key in `kv-grow2notes-test`, or a missing role, answers "Not sent" the same way (step 151).
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

### What the owner reads afterwards (read-only, by hand)

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
16. The telemetry, in the Azure portal's Log Analytics workspace: from step 8's time, the workspace holds its
    `POST /api/auth/login` `401` and one "Password sign-in with an unknown email" entry holding no address, and no
    `400`, `415` or `5xx` on `/api/auth/*`, and no exception. (S00.04.05.)

### More accounts, invited from Users

17. F00.04's feature check also asks for a worker on each setup path, and for a manager's passkey on a phone, since
    account A's is on the laptop, each setting up and signing in a second time on a phone and a laptop. As account A,
    invite from the Users screen (S00.06.02) two workers and a third manager (account C), each with a made-up name and
    a new address at the testing mailbox (*Before you start*). Open the first invite's email on the laptop, leaving its
    link for step 18. Look for: it arrives within 5 minutes (look in junk too, and say which folder it was in), from
    the address in the `test` environment's `EMAIL_SENDER_ADDRESS`, sent through Resend, with the subject "Set up your
    Grow2Notes sign-in"; its text names Grow2Notes
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
*Cut stories*); the section *Accessibility pass on the finished app* orders them with the other by-hand
accessibility steps. Besides the kit in *Before you start*, they need an Android phone with Chrome, Samsung Internet
and TalkBack, and Dragon on the laptop if you have it.

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
31. You read the audit rows as in step 15:

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

38. The database, as in step 26: run `infra/sql/check-database.sql` for test once T05 is in test. Look for PASS on
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

42. The database, as in step 26: run `infra/sql/check-database.sql` for test once T06 is in test. Look for PASS on
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

46. The database, as in step 26: run `infra/sql/check-database.sql` for test once T08 is in test. Look for PASS on
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

53. The database, as in step 26: run `infra/sql/check-database.sql` for test once T09 is in test. Look for PASS on
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
    S03.03.01.) Before T19 the Flagged link was the word alone and the screen had no view links. Once T19 is in test,
    the link reads "Flagged, 1 to review" on the next screen change, and the first view link reads "To review (1)"
    above the list; look for both too.
57. On the laptop as account A with NVDA and Chrome, then on the iPhone as account B with VoiceOver and Safari, then on
    the Android phone as account B with TalkBack and Chrome, choose Flagged while step 56's note is still there. Look
    for: the arrival read as "Flagged notes, heading level 1"; the row read as the participant's name and the date,
    then "link", then the worker's name and "flagged" with the time; the Reason line read when reading on; and, once
    the row is open, the back link read as "Flagged". (flagged.md *Visual and device*, the manual pass, for what T11
    builds.) Before T19 the arrival was the heading and the row alone. Once T19 is in test, the heading is followed by
    "Flagged notes, navigation" and the current view link, and step 112 holds those words.

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

67. The database, as in step 26: run `infra/sql/check-database.sql` for test once T13 is in test. Look for PASS on every
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

### Editing a submitted note, once T14 is in test

These steps hold T14's by-hand checks ([tasks.md](../design/backlog/tasks.md), T14's Notes). They need the manager and
the passkey worker of step 18, the kit of steps 26 to 28, and made-up participants with two goals each: a submitted
note by the worker for today for steps 70 and 73 to 76, and one for a past day for step 71, written by the worker. Every
word typed into a note is made up (A35). The editing is on the notes that steps 54 and 68 submitted, or new ones.

70. On the iPhone in Safari, at its usual text size and held upright, sign in as the passkey worker and open a note
    that the worker submitted: look for the byline, then Edit first in the row with Past notes, in a box at least as
    tall as a finger. Press Edit and look for "Editing submitted note (version 1)" above the participant's name and the
    date, the goals and groups ticked as they were, the text, and Save changes and Cancel under the flag, nothing cut
    short and nothing running past the screen's edge. Change a sentence: look for "Saving…", then "Saved" and the time.
    Press Save changes and look for the read view, with focus on the name and the date, the new text, and a new line,
    "Edited", with "last change by" and the worker's name. Press Past notes and look for "Edited" on the row. Press Edit
    again, change a tick, press Cancel and look for the read view as it was, with no change. (S02.06.01's first, second
    and fifth criteria on a real phone; note-form.md's layout and states for edit mode.)
71. As the manager on the laptop, open the Past notes of a participant and a note for a day before today that the
    worker submitted. Press Edit, change a word, and wait for "Saved". Go back to Today, without pressing Save
    changes or Cancel: look for "Your unfinished drafts" with the participant's name and that day's date.
    Press the row and look for the edit form with your word in it and "Saved" with the date, then press Save changes
    and look for the read view, written by the worker, with "last change by" and the manager's name. (S02.06.02's
    third criterion and S02.01.04's fifth, in test.)
72. With the worker on the iPhone and the manager on the laptop, both on the same submitted note: press Edit on both and
    change a different word on each, waiting for "Saved". Press Save changes on the laptop first. On the iPhone, press
    Save changes: look for focus moving to a notice that says who saved which version and when, "while you were
    editing", and "Your changes are still below.", the newer version open under "Version 2 by" and the manager's name,
    your own text unchanged in the form and "Not saved" at the top. Press "Start again from the latest version" and look
    for the notice's version closed, "Editing submitted note (version 2)" above the name, focus on the name and
    "Saved". Press Save changes and look for the read view with the worker's words and the worker's name on the Edited
    line; press Past notes and look for one "Edited" row. (S02.06.03's first and second criteria, on a real phone.)
73. Copy the address of a submitted note that the worker wrote, add `/edit` to its end, and paste it into a browser
    signed in as a second worker, who did not write it: look for "You cannot edit this note", "Only the person who
    wrote it, or a manager, can edit it." and the link "Read the note", with the navigation above and the tab titled
    "Grow2Notes – Note", and focus on the heading. Press "Read the note" and look for the read view, with no Edit among
    its links. (S02.06.01's last criterion; empty-loading-error.md's Edit refused row.)
74. On the laptop with NVDA and Chrome, as the worker on a submitted note of their own: look for Edit read as a link
    before Past notes. Press it and look for the heading read with "Editing submitted note (version 1)" first, then the
    name and the date, and for Save changes and Cancel read as buttons. Press Save changes: look for "Saving changes…"
    said if it takes more than a moment, then the read view's heading, with the Edited line read as "Edited", then "last
    change by", the name and the time. Make a conflict as in step 72 with the manager on a second window: look for the
    heading of the notice read on arrival, "…while you were editing", then "Your changes are still below.", the
    newer version's summary as a button that opens and closes it, and Start again from the latest version as a button;
    look for focus on the form's heading after it. (note-form.md's *Announcements* and *Focus order* for edit mode;
    autosave-status.md's *Behaviour* 10.)
75. On the iPhone with VoiceOver and Safari, repeat step 74's reading of the Edit link, the heading, the Edited line,
    the notice and its buttons, and swipe through the form: look for Save changes and Cancel as the last two stops, the
    notice's disclosure announced as expanded and then collapsed after Start again, and no stop on the closed
    version's text. With Voice Control or Voice Access, say "Tap Edit" and "Tap Save changes" and look for each acting.
    (note-form.md's manual pass for edit mode, on the platform that cannot be tested here.)
76. On the iPhone, open the edit form, change a word and wait for "Saved". Then, in this order: (a) turn on airplane
    mode and press Cancel: look for "Not cancelled: no connection. Try again." above the buttons, the text still in
    the box and no dialog. (b) Still offline, press Save changes: look for the top bar's "Not saved: no connection.
    Keep this page open; retrying.", nothing above the buttons, and no dialog. (c) Turn airplane mode off, wait for
    "Saved", and press Save changes: look for the read view with the new text and the Edited line. (d) For "Changes
    not saved: no connection. Try again.", open the edit form again, change a word, wait for "Saved", press Save
    changes and turn on airplane mode at once, so that the request fails or its answer is lost; this is timing-
    dependent and may take a few tries. Then turn airplane mode off and press Save changes again with nothing typed:
    look for the read view and, in the note's Version history, for exactly one new version, not two. (S02.06.01's
    second criterion and A69, with the retry's key.)

### Mark reviewed and re-flag, once T16 is in test

These steps hold T16's by-hand checks ([tasks.md](../design/backlog/tasks.md), T16's Notes). They need account A and
account B, both managers (step 13), the passkey worker of step 18, the kit of steps 26 to 28, and a made-up participant
with two goals. The worker submits two notes for today with Flag for manager ticked and a made-up reason, such as
`Asked about a new medication`, so that each manager has a note to review; every word typed is made up (A35). A note
can be flagged again after a review by a manager's edit, as step 84 does.

77. The database, as in step 26: run `infra/sql/check-database.sql` for test once T16 is in test. Look for PASS on every
    row, among them D3 and D5, which now name the `DENY UPDATE` and `DENY DELETE` on `dbo.NoteReview` for
    `grow2notes_runtime`, and B7 and B8, which list the `NoteReview` table and the `AddNoteReview` migration.
    (S03.02.01's migration criterion, in test.)
78. On the laptop with NVDA and Chrome, sign in as account A, choose Flagged, then a To review note. Look for the
    heading level 1 with the participant's name and the date, and for the headings "1. Goals", "2. Common items", "3.
    Guided notes" and "Review" in that order when listing headings. Move to the box and look for "Comment (optional)"
    read as its name, then the hint "Shown in the daily report and record exports, and cannot be changed later" and
    "You can enter up to 500 characters" as its description, and for the count read as you type. Paste 501 characters
    and press Mark reviewed: look for "Comment must be 500 characters or less" read as an alert, the box read as
    invalid with the message, and focus in the box; delete a character and look for the message gone. Press Enter in
    the box and look for a new line, not a press of the button. Press Mark reviewed with a short comment and look for
    "Marking reviewed…" said if it takes more than a moment, then focus moving to "Reviewed by" and the manager's name,
    "on" and the date and time, read as one sentence with "Comment:" and the words typed. (S03.02.01's first criterion;
    review-panel.md's *Announcements* and *Focus order*; flagged.md's Review view headings and Enter items, in a real
    screen reader.)
79. On the iPhone with VoiceOver and Safari, as account B, repeat step 78 on the second note: swipe to the "Review"
    heading and look for it read as a heading, then the box with its label, hint and count, the button read as "Mark
    reviewed, button", and, after a press, the record read with focus on it. Turn on airplane mode and press Mark
    reviewed: look for "Not marked reviewed: no connection. Try again." read as an alert, focus staying on the button
    and the comment still in the box; turn it off and press again. (S03.02.01 and S03.02.03, on the platform that
    cannot be tested here.)
80. On the Android phone with TalkBack and Chrome, repeat step 79 with a new flagged note: the heading, the box with
    its label, hint and count, the error for 501 characters, "Marking reviewed…" and the record with focus on it. With
    Voice Access, say "Tap Mark reviewed" and look for it acting. (S03.02.01 and S03.02.03 on the third platform; the
    visible words are the control's name, flagged.md's label-in-name item.)
81. On a real phone held upright with the browser at its usual text size, open a To review note as a manager at 375 px
    wide and look for no sideways scroll, the box and Mark reviewed as wide as the page's column, and the box's text
    not zooming the page when you tap into it on the iPhone. Press Enter in the box and look for a new line. Dictate a
    long comment, and look for the count including the dictated words, and the 501st character refused as in step 78.
    (participant-notes.md's phone layout; flagged.md's Enter item with dictation, which no automated test covers.)
82. With account A on the laptop and account B on the phone, open the same To review note, type a comment on each, and
    press Mark reviewed on the laptop first. On the phone press Mark reviewed: look for focus moving to "This note was
    marked reviewed while you had it open.", account A's record under it, then "Your comment was not saved:" and the
    words typed on the phone, with no Mark reviewed button. Then reload the note on the phone and look for the Reviewed
    line only, with no Review heading and no box. (S03.02.03 and A73; participant-notes.md's two-managers case.)
83. With the passkey worker on the laptop and account A on the phone, open the worker's flagged note on the phone and
    type a comment. On the laptop press Edit, change a word, and press Save changes. On the phone press Mark reviewed:
    look for the new text shown in the note, "This note was edited after you opened it. Read the latest version, then
    mark it reviewed." in the alert, the comment still in the box, and focus on the button. Press Mark reviewed again
    and look for the record. Open the note's Past notes list as the worker and look for no "To review" or "Reviewed"
    tag anywhere. (S03.02.03's first criterion and S03.02.02's first criterion, with real people.)
84. As account A, open a note that is Reviewed and press Edit, change the reason of the flag, and press Save changes.
    With account B already on Flagged in another window, and without reloading it, return to that tab and look for the
    note in To review. Open it as account A and look for the To review tag, the Review panel with an empty box and the
    earlier review no longer shown as the note's state. Review it again, then edit it, untick the flag and save: look
    for the note still in To review, "Flag removed in a later edit" and the earlier reason. Look too for the badge
    ("Flagged, 1 to review" in the menu) and "To review (1)" counting it, on the screen change after the edit and
    without a reload of the other window. (S03.01.02 end to end; the badge and the count are S03.03.03's, in test since
    T19. Before T19 this sentence began "Once T19 is in test, also look for…", as the menu showed no number.)
85. Open a note from Flagged and look for the back link "Flagged". Press Edit and look for the same label above the
    form; press Cancel and look for the read view with "Flagged" again; press Edit and Save changes, look for "Flagged"
    once more, and press it to return to the Flagged list. (flagged.md component 7's second bullet.)

### Version history and past-day notes, once T18 is in test

These steps hold T18's by-hand checks ([tasks.md](../design/backlog/tasks.md), T18's Notes), in two parts: Version
history, steps 86 to 94, and Write past-day note, steps 95 to 105. They need account A, a manager, the passkey worker
of step 18, the kit of steps 26 to 28, and a made-up participant with two goals, such as `Jane Citizen`, whose archived
state is changed in step 104. For the first part, the worker submits a note for today, and account A edits it twice,
changing a word and a tick each time and flagging it once. Every word typed is made up (A35).

86. The database, as in step 26: run `infra/sql/check-database.sql` for test once T18 is in test. Look for PASS on every
    row, with no new table or migration named, as T18 adds no table, column or write path.
87. On the laptop in Chrome, as account A, open the edited note and look for Version history between Edit and Past
    notes in the row, and no such link when you sign in as the worker. Press it and look for the `<h1>` "Version
    history Jane Citizen" with the note's date, "The current version is version 3.", and three rows, newest first, each
    a link "Version n" with who saved it and when, "(manager)" after account A's name and not after the worker's, and
    "Submitted" on version 1 alone. (S02.07.01 and version-history.md's page A, with real people.)
88. Open version 2 and look for its own picked groups and its own flag, and the line "This is an earlier version. The
    current version is version 3." with a working link to version 3, which says "This is the current version." Press
    the back link, and then, on a second visit, Back in the browser: look for the list at the place it was left, the
    row opened focused, and no Restore, Edit or Mark reviewed anywhere. (S02.07.02 and version-history.md's page B.)
89. With the list open in one tab, edit the note in another and save, then return to the first tab and look for the new
    version in the list without a reload. Then open a version in the first tab, edit and save in the other, and look,
    on returning to the list, for the same. (The list refetches on focus; a version never changes.)
90. On the iPhone with VoiceOver, open the list and a version: look for the heading read as "Version history", the
    name and the day, then "heading level 1"; each row read as a link, "Version 3", with its description; the commas
    pausing between the name and the time; and the way "Thu" and "Oct" are spoken, which the spec marks unverified.
    Repeat with NVDA and Chrome on the laptop. (version-history.md's screen-reader checklist; no automated test hears
    a speech engine.)
91. On the Android phone with TalkBack and Chrome, repeat step 90 with the list and version 2. Write down how the
    abbreviated day and month are spoken and whether the rows are easy to land on. (The third platform of the same
    checklist.)
92. On a real phone held upright at its usual text size, then with the text size at its largest, open the list and a
    version and look for no sideways scroll, each row at least a finger tall, and the Guided notes text wrapping with
    its line breaks and spaces kept. Then, on the laptop with a Windows contrast theme on, open the list and a version
    and look for the focus ring at each Tab stop, each row's edge, and every box and tick on the version still drawn.
    (version-history.md's layout items on a real device, and its forced-colours check.)
93. As the passkey worker, type the list's address and a version's address, taken from account A's browser, into the
    address bar: look for Page not found for both, and the same for a version number that is not a whole number, such
    as `0`. As account A, type a version number the note does not have and look for Page not found too. (S02.07.01 and
    S02.07.02's access criteria, with real sessions.)
94. As account A, open the list and a version of the past-day note that step 97 makes, and look for the line
    "Past-day note" on both, with "written by {name} (manager) on" and the date and time. With the screen reader of
    step 90, listen for it as one sentence: "Past-day note, written by …". (version-history.md's past-day line.)
95. On the laptop in Chrome, as account A, open Participants, then Jane Citizen, then Write past-day note (after Past
    notes in Actions). Look for the title "Grow2Notes – Write past-day note", the `<h1>` holding her name above the
    page's name and taking focus, and the field "Note date" empty, with "A date before today" under the label. Press
    Continue with it empty and look for "Enter the note date" above the field, the field focused and red-barred. Pick
    today and press Continue: "Note date must be in the past". Pick yesterday: the message goes at once. Then, with a
    Windows contrast theme on, Tab through the page and look for the focus ring on the back link, the field and
    Continue. (S02.08.01 and participants.md C.)
96. Press Continue with yesterday and look for the note's form with "Past-day note for {the date}, written on {today}."
    after the date and before Past notes, no "This draft is for" notice, and the back link "Participant". Press the
    back link and look for her participant page with no date page in between; press Write past-day note again,
    continue, and press Back in the browser: look for the same. Open Past notes, press Write past-day note there,
    continue, and look for the back link "Past notes". (S02.08.02, and note-form.md open question 4 as built.)
97. Type some words in the form of step 96, wait for "Saved", press Submit note and look for the confirmation titled
    "Submit the note for" with the date, then confirm. Open the note and look for the line "Past-day note" with your
    name and "(manager)", the day and time it was written, and then "Submitted" with its time, and no "Written by".
    Open its Version history and look for the same line. (S02.08.02's third and fifth criteria.)
98. On a date that already has a submitted note, press Continue and look for its read view with Edit; on a date with
    your own draft, the form; on a date with the worker's draft, the read-only draft with Discard draft. Then, on a
    past-day note you wrote, press Edit and look for the same "Past-day note for…" notice above the form and no "This
    draft is for" notice. (S02.08.01's second criterion with real notes.)
99. A very old date: in Chrome type `01011999` into the field, or pick 1 January 1999, and press Continue. Look for the
    form of that date opening, with "Past-day note for Friday 1 January 1999, written on {today}.", and nothing
    breaking in the heading or the notice. Leave without typing, so that no draft is made. Then do the same in Edge and
    on the iPhone. Then type a year of two digits, such as `0019`, and look for the form opening with nothing breaking;
    its weekday may be wrong, as the browser's date arithmetic reads a year from 0 to 99 as 1900 to 1999, which A75
    accepts. (A75: no earliest date, no warning.)
100. A typed year: in Chrome click into the year part of the field and type a fifth digit, such as `20261`, press
     Continue and look for "Note date must be in the past", not Page not found. Then type `2026` with a month and day
     after today, in Chrome, Edge and Firefox, and look for the same message. (Typed dates get past `max`, which is
     only a hint.)
101. On the iPhone in Safari, open the page and tap the field: look for the phone's own picker, which lets you pick
     today or a day after it despite `max`. Pick one, close the picker, press Continue and look for "Note date must be
     in the past" with the field focused. Look too for the text at least as large as the page's, the page not zooming
     in, and the value not centred oddly in the field. (date-navigation.md's iOS items.)
102. With NVDA and Chrome, VoiceOver and iOS Safari, and TalkBack and Chrome, tab to the field and look for "Note date",
     "A date before today" read as its description, and the type of control; press Continue empty and look for "Error:
     Enter the note date" read, with focus in the field. With Dragon or Voice Access, try to say a date into the field
     and write down what happens. (participants.md and date-navigation.md's checks; Dragon could not use date inputs
     in 2019.)
103. The daylight-saving and midnight check: Melbourne's clock decides "written on", so set the laptop's time zone to
     another, such as UTC, and write a past-day note at about 12:30 am Melbourne time, which is still the day before in
     UTC. Look for "written on" naming Melbourne's day in the notice, after the first save too, and the read view's
     line giving the same day with the Melbourne time. Repeat on the night of a change of clocks, the first Sunday of
     April or October (4 April 2027 is the next), once for a note started shortly before midnight and once shortly
     after it. (`format.melbourneDay` and the server's `DateOf`, with a real clock.)
104. Archive Jane Citizen: look for Write past-day note gone from her participant page and from her Past notes, with
     Past notes and Edit participant still there. Type the date page's address by hand and look for "Jane Citizen is
     archived, so no new notes can be written for them." with no field. Restore her and look for the link back. As the
     worker, look for no such link on Past notes, and Page not found at the typed address. (S01.02.04's Write past-day
     note check and S02.08.01's first criterion.)
105. On the laptop, reload the date page and look for its back link reading "Participant"; press Continue with
     yesterday and look for the note's back link reading "Today", as nothing in the app opened the date page. Then open
     the note's address from a pasted link and look for "Today" too. (note-form.md's back-link fallback.)

### Reviewed list and badge, once T19 is in test

These steps hold T19's by-hand checks ([tasks.md](../design/backlog/tasks.md), T19's Notes). None of them holds a story
open. They need account A and account B, both managers (step 13), the passkey worker of step 18, the kit of steps 26 to
28, and a made-up participant with two goals. T19 adds no table or migration, so the database check of step 26 needs no
new row. The worker submits several notes for today, on different made-up participants or days, each with Flag for
manager ticked and a made-up reason, so that the managers have three to review; every word typed is made up (A35).

106. With account A on the laptop in Chrome and account B on the Android phone in Chrome, both on Today, have the worker
     submit a flagged note on the iPhone. Without touching either screen, look for the Flagged link in the menu with no
     number on both; then choose Today on each, and look for "Flagged, 1 to review" in the menu on the next screen. Have
     the worker submit a second flagged note, put the laptop's window behind another, and bring it back: look for the
     number going to 2 on the focus, with no other change. Open Flagged on the phone and look for "To review (2)" and
     two rows. Leave both screens untouched for 10 minutes and look at the network list of the laptop's developer
     tools for no request. (S03.03.03's second criterion on real devices; app-shell.md's *Refresh*.)
107. With three notes to review, as account A, mark one reviewed with a comment of two lines. Look for the menu number
     going down by one on the screen after, "To review (2)" on Flagged, the row gone, and the note reading "Reviewed by"
     your name, "on" and the date and time. Choose Reviewed and look for the row with the participant, the date, the
     author, "reviewed by" your name "on" the date and time, "Reason:" with the first line of the reason and "Comment:"
     with both lines. As account B, look for the same on the phone, and for the menu reading "Flagged, 2 to review".
     Review the other two and look for the menu word alone, "To review (0)" and "No flagged notes to review.". (The
     F03.03 flow end to end in test; design.md §14 M3 *Done when* bullets 1 and 2.)
108. As account A, edit a reviewed note, change its reason and save. Look for the note back in To review with the new
     reason, the number up by one, and Reviewed still holding the earlier row with the earlier reason. Review it again
     and look for two rows for the note in Reviewed, the newer first, each with its own reason. (S03.03.02's third
     criterion and A15, with real data.)
109. On the Android phone, over a patchy connection (walk to a weak spot, or throttle it), with 31 or more reviews, open
     Reviewed and press Show older. Look for the button not repeating on a second press, "Loading older notes…" if it
     takes more than a second, and, with airplane mode on, the rows kept, focus on the button and the sentence that
     older notes did not load; turn it off, press again, and look for focus on the first new row and the button gone
     when no more are left. (S03.03.04 and flagged.md's Show older items, with a real connection.)
110. On the laptop, then on the phone, show 60 Reviewed rows with Show older, scroll to the 10th, open its note and
     press the browser's Back, then again with the back link "Flagged". Look for the 60 rows still there, the scroll
     where it was and focus on the 10th row's link. Reload and look for the newest 30 only. (S03.03.05; A77.)
111. With account A on Flagged and focus on a row (Tab to its link), have account B review that note on the phone.
     Return to the laptop's window and look for the row gone and focus on the row now in its place, or on the last row,
     or on the heading when the list is empty, and not at the page's start. (S03.03.05's criterion, with real people.)
112. On the laptop as account A with NVDA and Chrome, then on the iPhone as account B with VoiceOver and Safari, then on
     the Android phone as account B with TalkBack and Chrome, with three to review, move through the menu and look for
     "Flagged, 3 to review" read once, not "3" twice. Choose Flagged and look for "Flagged notes, heading level 1", then
     "Flagged notes, navigation", the two links with "To review (3)" as the current page, and "Reviewed". Choose
     Reviewed and look for it as the current page, then a row read as the name and date, "link", and the author and
     "reviewed by" description. (flagged.md *Accessibility checklist*, the Screen reader items, and *Visual and
     device*; app-shell.md's *Badge*.)
113. On a real phone, set the text to its largest and the page 320 px wide if the browser allows, and look at Flagged,
     both views, with a long name and a long reason: no sideways scroll, the two view links on two rows, and the badge
     in the menu not cut off. On a Windows laptop with a contrast theme on, look for the current view link's bar, the
     badge's outline, the focus ring and the Show older button all visible. (flagged.md *Visual and device*, on real
     hardware.)

### Daily report as Word and PDF, once T15 is in test

These steps hold T15's by-hand checks ([tasks.md](../design/backlog/tasks.md), T15's Notes). None of them holds a story
open. Steps 20 and 21 already cover the downloads on phones and the screen readers on the finished Report screen, so
these do not repeat them. They need a manager, the worker of step 18 and a day with three submitted notes on made-up
participants, one with an accented name such as `Zoë Sample`, and every word typed is made up (A35). In one note's
Guided notes type curly quotes, a dash, three dots, two lines with a blank line between them and a phone emoji; tick one
goal and leave one unticked. Open Report from the menu, which opens today's date, and download both files for that day.

114. Open the PDF in Microsoft Edge and then in Adobe Acrobat Reader. Look for the participants in family-name order,
     the ticks and empty boxes as drawn shapes, the curly quotes, the dash and the accents, the line breaks and the
     blank line kept, and a replacement mark in place of the phone emoji. In each, open Document Properties and look
     for DejaVu Sans listed as embedded. (S04.01.02; design.md §11.5, A59 and A81.)
115. Open the Word file in desktop Word on Windows. Open the Navigation pane and look for each participant as a heading.
     Look for the ticks and empty boxes showing, the goals as a list with no bullet, the Guided notes' line breaks and
     blank line kept, and the phone emoji as written. (S04.01.01; design.md §11.5.)
116. Open the Word file in Word on a phone. Look for the same words in the same order, the ticks showing and the line
     breaks kept. (S04.01.01.)
117. In each file, look through the text, then File > Info in Word and Document Properties in Edge or Acrobat, including
     the PDF's document information, for the parent company's name anywhere. Look for none. (D42, D72.)
118. On a phone at 375 px wide, open Report: look for the heading "Daily report" and the two buttons one above the
     other, no sideways scroll, and nothing cut off with the text set larger. With only the keyboard on the laptop, Tab
     to Download Word and then Download PDF, look for the focus ring on each, and press Enter on one and look for the
     file to save and focus to stay on the button. (T17 puts the date field and the two step links before the buttons;
     step 127 checks that order.) (daily-report.md *Visual and device* and
     *Accessibility checklist*, keyboard items.)
119. With NVDA and Chrome on the laptop, if you have it to hand, press Download Word on a day with notes, then press it
     on a day without any, and look for the buttons read by name, "Daily report, heading level 1" on arrival and, for
     the day without notes, "No submitted notes for" and that day read once. (daily-report.md *Accessibility
     checklist*, Screen reader items, for T15's part.)

Record the results in T15's Notes.

### Daily report content, pages and timings, once T20 is in test

These steps hold T20's by-hand checks ([tasks.md](../design/backlog/tasks.md), T20's Notes). None of them holds a story
open. They need a manager, a worker and a made-up day of 20 submitted notes (A35): each note has three goals, Every note
and two of three groups picked, and long Guided notes, about 2,000 characters. Make some notes flagged, one of them
reviewed with a comment and one with its flag removed in a later edit, some edited, one a past-day note written by the
manager and one submitted on the next day. Every word typed is made up. Download both files for that day.

120. Prepare the day above in test: 20 participants with a note each, and the cases listed. Look for the Report screen
     to offer both downloads for the day. (S04.01.06; design.md A35.)
121. On a laptop, time each format from pressing the button to the saved file. Look for each under 10 seconds. Write
     both times in T20's Notes. (S04.01.06; design.md §14 M4.)
122. Do the same on a phone. If either is over 10 seconds, T20 fixes the speed and the check is repeated. Write the
     times in T20's Notes. (S04.01.06.)
123. Open the PDF in a PDF viewer. Look for no header on page 1, the running header "Daily progress notes: {date}"
     from page 2, and on every page the footer with your name, page 1's "As at" time and a correct "Page n of N". Open
     Document Properties and look for only the title and the organisation, with no author. (S04.01.05; design.md
     §11.5, A85.)
124. Open the Word file in desktop Word and then in Word on a phone. Look for the same header and footer, correct page
     numbers on screen and when printed, each common item group as a Heading 3 in the Navigation pane, and File > Info
     showing only the Title and the Company. (S04.01.03 and S04.01.05; design.md §11.5, A85.)
125. Open the Word file in a phone preview that is not Word, such as iOS Files or Quick Look or an Android preview.
     Look for whether the page numbers show, and write down what you see. (S04.01.05.)
126. Read each marker and the common items in both files against the read view of the same notes: the past-day line,
     the next-day Submitted time, Edited, the flag line with "(flag removed in a later edit)", "To review", "Reviewed
     by … on …" with its "Comment: …", and the groups with their items. Search each file's text and properties for the
     parent company's name and look for none. (S04.01.03 and S04.01.04; design.md §11.3, A82 to A84, D72.)

Record the results in T20's Notes.

### Report screen: choose a day and know what happened, once T17 is in test

These steps hold T17's by-hand checks ([tasks.md](../design/backlog/tasks.md), T17's Notes). None of them holds a story
open. Steps 20 and 21 already cover saving the files on phones, the date pickers and the screen readers, so these do
not repeat them. They need a manager, the worker of step 18 and made-up participants (A35).

127. On a real phone at 375 px wide, open Report: look for the date field at the full width of the screen, Previous day
     and Next day side by side under it, and Next day with a dashed border on today. Set the text larger and look for
     nothing cut off and no sideways scroll. On the laptop with only the keyboard, Tab from the heading and look for
     the order the date field, Previous day, Next day, Download Word and Download PDF, with a visible focus ring on
     each. (S04.02.01 to S04.02.03; daily-report.md *Visual and device* and *Accessibility checklist*, keyboard items.)
128. As a manager in test, open Report on a day with no submitted notes and leave it open: look for "No submitted notes
     for" that day and both download buttons dashed. As the worker, on another device, submit a note for that day. Back
     in the manager's window, click into the window and look for the sentence to become the day's date and both
     buttons to become available, with no reload. Then download both formats and look in the audit log for one
     `report.downloaded` entry for each press, and none from opening the screen or stepping between days. Last, type a
     date a few weeks back, slowly, and record whether the pause of three quarters of a second before the page moves to
     it feels right (daily-report.md open question 3). Also type a date by its parts, clearing one part and moving to
     the next, and record whether "Enter a date" or an early history entry appears before leaving the field (Chromium
     blurs and focuses the field between the parts, and the field commits on every blur); record it in T17's Notes.
     (S04.02.03 and S04.02.04; design.md A87.)

Record the results in T17's Notes.

### Operator runbooks, once T23 is in test

These steps hold T23's by-hand checks ([tasks.md](../design/backlog/tasks.md), T23's Notes). The runbooks and their
scripts are tested in CI on made-up data, so none of these holds a story open. Step 129 needs the test environment to
hold made-up data (A35), including a made-up participant that you archive for the purpose.

129. As the SQL Entra admin on the test server, through a temporary rule for your own address deleted straight after,
     with no address written down, run every `ops/` script on made-up data with the runbooks' commands. That includes
     `retention-destroy.sql`, preview then destroy, on the made-up participant archived for the purpose. Look for exit
     code 0, Melbourne times with their offset, names beside their IDs, and the audit file after the `sed` step with
     its header, one line for each row and every Details value whole. Correct `audit-extraction.md` by pull request if
     the flags differ. (S05.04.01 criterion 6, S05.04.02 criterion 7.)
130. A manager reads `retention-and-destruction.md`, `audit-extraction.md` and `breach-response.md`. A comment on T23's
     pull request records "a manager" and the date; the name stays in the provider's records. (S05.04.01 criterion 11,
     S05.04.02 criterion 8, S06.06.01 criterion 9.)
131. The operator gives the provider its contact details in writing (go-live checklist line 3). The managers choose the
     place in their records storage for the yearly list, the HPP 4.3 log, the audit request record and the breach
     records, and confirm that the operator can reach it. (S06.06.01 criterion 8, S05.04.02 criterion 3; design.md A89,
     A92.)
132. At the first restore drill, before go-live ([go-live.md](go-live.md) F3), follow `restore-drill.md` step by step
     and fix any step that did not work in the same pull request as its restore-log.md row. (S06.03.02, S06.06.02.)

Record the results in T23's Notes.

### Export a participant's record, once T21 is in test

These steps hold T21's by-hand checks ([tasks.md](../design/backlog/tasks.md), T21's Notes). None of them holds a story
open. They need a manager and made-up participants (A35). Steps 20 and 21 already cover the Report screen on the same
phones and screen readers; these cover the Export record screen, which is a different page.

133. In test, choose the largest made-up record there: one participant with a year of submitted notes if one exists,
     otherwise the one with the most. Export a one-day range as Word once to warm up the database and the server. Then,
     on the laptop, tick Include earlier versions and export that whole record as Word and as PDF. Time each from
     pressing Export to the file being handed over. Record each time, the date range, how many notes the record has and
     how many were edited, and whether the "Exporting…" button and the preparing line held steady the whole time. This
     is the M5 timing for the deployed site; CI's timing test only shows the code is fast in one process. If a time is
     near 30 seconds, tell the developer: the fix is three batched reads, still in one request. If the test site holds
     less than a year of notes, write down how much it does hold, so the owner can decide whether a made-up year is
     needed. (S05.01.04; design.md §14 M5, A93, A94.)
134. On iPhone Safari, iPhone Chrome, Android Chrome and Samsung Internet, as a manager, export a record as Word and as
     PDF. Do it for a short range and for one that takes more than 10 seconds (the year of step 133, with earlier
     versions, if it takes that long). Look for each file saved or offered for saving, with its name
     `participant-record_{from}_to_{to}` and the format, and the line "{name} is ready. Look in your downloads." If a
     browser cannot save the slow one, tell the developer: the plain-link contingency in file-download.md is then built
     for this screen only. (S05.01.04; record-export.md *Real devices*.)
135. On the iPhone and on the Android phone, open each date field. Look for the system date picker to open and to set
     the value, and for the page not to zoom when a field takes focus. Look for a To date before the From date to show
     its error once you press Export, and for a To date after today to be sent. (S05.01.04; record-export.md *Real
     devices*.)
136. On each manager's laptop, open Export record and look at both dates: they should read day first, then month (the
     browser's language set to English (Australia)). Write down each manager, browser and what the dates showed. Do
     this again at go-live. (S05.01.04; record-export.md *Real devices*.)
137. With VoiceOver on the iPhone (Safari) and TalkBack on the Android phone (Chrome), go through the Export record
     screen: the heading, the two dates, the Format group, the box and its hint, and Export. Then hear each of these
     read once, as written: the error summary and its links, the preparing line during a slow export, the ready line,
     the "No submitted notes for … between … and …." line for an empty range, and the failure line when the phone is in
     airplane mode. (S05.01.04; record-export.md *Accessibility checklist*, *Real devices*.)
138. Open one PDF and one Word file by eye, each with earlier versions, for a participant who has an edited note, a
     flagged and reviewed note and a past-day note. Look for the title block in order (the title, the name, the date
     of birth, the range, the organisation, who exported it and when, and "Earlier versions: included"), a first-level
     heading for each date, the note blocks as in the daily report, and under each edited note "Earlier versions"
     with each "Version n" heading below it, newest first, each with its own flag, reviews and text. In Word, look at
     the Navigation pane for the dates, "Earlier versions" and the versions at three levels. Open the file properties
     and look for only the title and the organisation, with no participant's name and no author. Search the text and
     the properties of each file for the parent company's name and look for none. Export once without earlier versions
     and look for no "Earlier versions" anywhere. (S05.01.01 and S05.01.03; design.md §11.6, A93.)
139. Open the Report screen and the Export record screen on a phone, and download one file from each. Look at the
     sentence after each: "{name} is ready. Look in your downloads." Decide whether to approve it on both screens or
     to show nothing on both, and tell the developer: either is a change in the copy module. Both screens ship with
     the line until you decide. (record-export.md open question 6; file-download.md.)

Record the results in T21's Notes (S05.01.04).

### Security review and rate limits, once T25 is in test

140. In the repository's Settings, under Code security, turn on Dependabot alerts. Then open Insights, Dependency graph,
     Dependabot and look for the four entries of `.github/dependabot.yml`, NuGet at `/`, npm at `/src/grow2notes-spa`,
     npm at `/tests/e2e` and GitHub Actions at `/`, with no error beside any of them. After the first weekly run, look
     for at most one grouped `chore(deps)` pull request for each entry, and for any new major version as a pull request
     of its own. (S06.01.03; design.md §9.11.)
141. The ZAP baseline scan, on your own machine with your own copy of ZAP, against the test site only, with no
     credentials and no ZAP sign-in. Run it twice: once on the site root with the AJAX spider (`-j`), for the pages a
     signed-out visitor sees, and once on `/api/auth/me` signed out, for an `/api` response. If most of a report's
     requests were answered `429`, the scan is incomplete: wait a minute and run it again with the spider slowed. Keep
     each raw report yourself, because it can hold cookie or token values. In `ops/security-review.md`'s ZAP section
     write what was covered (the targets, the two passes, the number of URLs and the number of `429` answers), and each
     alert as fixed, accepted with a reason, or a false positive. Look for no High alert open; take any High to the
     developer as a bug. No alert about a missing Strict-Transport-Security, Content-Security-Policy or
     X-Content-Type-Options header, or about a cookie without Secure, confirms ASVS rows 3.3.1, 3.4.1, 3.4.3 and 3.4.4
     of `ops/security-review.md`; an alert on any of them is a finding to decide. Write down no address, ID, email,
     cookie or token. (S06.01.02; design.md §14 M6; the Server header and the cookie, cache and header alerts are
     decided here, as `ops/security-review.md` says.)
142. The rate limits key on the real client, with two devices on different networks: the laptop on the office network
     and a phone on mobile data, nobody signed in. First look at step 15's result: the audit `IpAddress` was your own
     address and not a private or `169.254.` one. If it was not, stop and tell the developer, because every person would
     then share one count. On the laptop, in Chrome with the Network panel open, open the setup page with a made-up
     user and token in its address, which sends one `POST /api/auth/setup/start` (`SetupPage.tsx` sends one for each
     load) and shows "This link has expired". Select that request and press R 29 times to replay it, which makes 30 in
     the minute. Then open the made-up link once more. Look for "Too many tries. Wait 1 minute, then try again.", and
     for the request answering `429` with `Retry-After: 60`. While that is showing, open the same made-up link on the
     phone on mobile data and look for "This link has expired", not "Too many tries". A minute later the laptop shows
     "This link has expired" again. People on one network share one count of 30 a minute, by design, so a shared office
     is the case to watch: about 3 requests for each password sign-in means about 10 sign-ins a minute from one office
     address (design.md A96). Write down no link. (S06.01.05; design.md §9.9, A95, A96.)

Record the results in T25's Notes (S06.01.01, S06.01.02, S06.01.03 and S06.01.05).

### Accessibility pass on the finished app, once T26 is in test

These steps hold the by-hand checks of S06.02.01 and S06.02.02 ([tasks.md](../design/backlog/tasks.md), T26's Notes).
CI's Playwright run proves what a browser can (design.md A98); these steps need a real device, a screen reader or a
person, and none of them holds a story open or go-live (A107). Run the by-hand accessibility steps in this order, once
T26 is in test:

1. Keyboard only, on the laptop: step 143.
2. Screen readers: steps 21 and 22, then the screen-reader steps of the screens, in the order of the screens: 4, 9, 10,
   11, 12 to 14, 17, 18, 27, 28, 32 to 34, 37, 41, 45, 47, 48, 52, 55, 57, 62, 65, 66, 69, 74, 75, 78 to 80, 90, 91, 94,
   102, 112, 119 and 137.
3. Guide prompts, with screen readers and the phones' back gesture: steps 24 and 25.
4. Real devices: step 20, then the device steps of the screens: 35, 40, 44, 49, 50, 54, 68, 70, 81, 92, 101, 113, 118,
   125, 127, 134 to 136 and 139, then the go-live device check, step 144.
5. Text size and zoom: step 145.
6. Contrast, page darkening and motion: step 146.
7. Outdoors: step 147.
8. Last, the five-worker words check: step 23, which the managers arrange.

Steps 20 to 25 keep their numbers because the stories' Notes cite them. Besides the kit in *Before you start*, these
steps need the Android phone, with Chrome, Samsung Internet and TalkBack. Everything typed is made up (A35).

143. Keyboard only, on the laptop. In test, in Chrome, put the mouse and the trackpad aside. As a manager and as a
     worker, do each flow of design.md §4.13 with made-up data, using only the keyboard: Forgotten note, Daily report,
     Access request, Wrong participant, Write today's note, Correct a note, Flag and Lost phone. Look for a focus ring
     at every stop, including inside banners and dialogs; for focus never stuck, so that Tab always moves on and
     Shift+Tab goes back; and for focus, after each screen change, on the banner, the error summary, the row returned
     to or the `<h1>`, as the screen's file says. Write down the result for each flow. (S06.02.01's keyboard
     criterion by hand; foundations.md manual test 8; design.md A98's cross-check of
     `tests/e2e/key-flows.spec.ts`.)
144. The go-live device check. On the iPhone, with Gmail, Outlook and Apple Mail, and on the Android phone, with Gmail,
     Outlook and the phone's own mail app, invite one made-up account for each app, to the testing mailbox, minding the
     free plan's 100 emails a day (*Before you start*). Open each setup link in the app that received it
     and try Use a passkey (recommended) in the app's own browser. Write down, for each phone and each app, whether a
     passkey was made, refused or not offered, and, if it was not made, whether opening the link in the phone's
     browser fixed it. (S06.02.02; sign-in.md *Layout and accessibility*, the go-live device check.)
145. Text size and zoom. On the laptop in Chrome, set the zoom to 200% and then to 400%, then back to 100% with the
     browser's font size at 32. On the iPhone, set Larger Text to its largest, and set Safari's page zoom to 200%. On
     the Android phone, set the font size and the display size to their largest. On each phone, look at each screen
     upright, and the note form in landscape. Go through Today, a note with Flag for manager ticked and a Reason, the
     Submit dialog, Past notes, Flagged, Report, Participants and a participant, Common items, Guide prompts, Users
     with a long address, and Export record. Look for nothing cut off, nothing overlapping and nothing scrolling
     sideways. On the note form, in the largest text, say "Tap" and a goal's words with Voice Control or Voice Access
     and look for its box ticking; tap a goal's box in Safari and look for the focus ring on it; and, in Chrome, use
     Translate on the note form and look for the participant's name unchanged. (foundations.md manual tests 1 to 3;
     note-form.md *Manual test passes (M6)*: Voice Control, zoom, landscape, page translation; S06.02.02.)
146. Contrast, page darkening and motion. On the laptop, turn on the Windows contrast theme Night sky, then Desert, and
     in Edge and in Chrome Tab through a note with ticks and a flag, the Submit dialog, Today with its banner and
     tags, Flagged with its tags and the badge in the menu, and the Report screen on a day with no notes. Look for
     every tick, tag border, badge, banner edge, unavailable button and focus ring. On the Android phone, turn on dark
     mode with page darkening, in Chrome and in Samsung Internet, and open Today, a note, Report and Users: look for
     pages that are usable and, on setup's authenticator view, the QR code not inverted. Reaching that view needs a
     fresh invite. On each device, with reduced motion on, look for nothing moving. (foundations.md manual tests 4 to
     6; status-tags.md, tag borders in forced colours; S06.02.02.)
147. Outdoors. On each real phone, outdoors, at its automatic brightness, open a note on the note form. Look for the
     placeholder, the status lines, the tick state and the focus ring: can each be seen? Write down, for each phone,
     what could not. (foundations.md manual test 7; note-form.md *Manual test passes (M6)*.)

Record the results in T26's Notes (S06.02.01, S06.02.02), for each item and each device.

### The email sender's settings, once T01 is in test

148. In the Azure portal, signed in with your own account, open the web app `app-grow2notes-test`, then Settings,
     Environment variables. Look at the names only. Look for: `Email__ApiKeySecretUri` and `Email__SenderAddress`
     among them and no other `Email__` name; and no setting whose value is a key or a connection string. The secret's
     address holds the vault's name and `resend-api-key`, and no key. The only connection-string names expected are
     `ConnectionStrings__Grow2Notes` and `APPLICATIONINSIGHTS_CONNECTION_STRING`. If a name looks wrong, open that one
     value. Do not write any value down. (S00.02.07's second criterion, its deployed half: its app half is
     `EmailSendingTests`, in CI. This is the read-only check, by hand, for the Azure side; D92 changed the names.)

### Sign-out on every device, once F-1's fix is in test

149. Sign in as account B with email, password and a 6-digit code on the laptop in Chrome, with the developer tools open
     on the Network panel, and on the iPhone in Safari, as in step 14. On the laptop, open today's note for a made-up
     participant whose note nobody has started today (A35), type a sentence and wait for "Saved" and the time. On the
     iPhone, sign out from Account: look for the sign-in page. Then on the laptop type a second sentence: look for
     "You've been signed out" in the form's place, at the same address. Sign in again with email, password and a
     6-digit code: look for the note form again with both sentences, the status "Saved" and the time, no "This note was
     changed on another device or tab.", and a `PUT …/draft` answered `200` in the Network panel. (design.md A99 and
     §8.5 *No work is lost*; ops/security-review.md F-1. CI proves in `LogoutEndpointTests` that a copy of the cookie
     stops working, so no cookie is copied by hand.)

### The bootstrap's GitHub subject, once F-25's fix is on `main`

150. In Git Bash, from the root of a clone of `main`, signed in to the Azure CLI as Owner of the subscription and to the
     GitHub CLI as an admin of the repository, follow `infra/README.md`'s *Running it again*: steps 1, 2 and 3 in one
     shell, with `budgetStartDate` read from the last run. Look for step 2 printing `repo:`, then the account and the
     repository, each followed by `@` and a number. Look for the what-if showing no create, modify or delete in either
     group, with the federated credentials `github-test` and `github-prod` unchanged. Do not run step 4: with no change,
     it has nothing to deploy. If the what-if shows a change to a federated credential's subject, stop: the prefix
     passed is not the one the credentials were made with, and deploys would stop signing in. Do not write down the
     prefix, an ID or an email address. (F-25 in `ops/security-review.md`, D89: the prefix now comes from GitHub when
     the bootstrap is run, not from a default in `bootstrap.bicep`, and nothing in Azure changes. The developer records
     the result in T25's Notes.)

### Email through Resend and the database lock, once T22 is in test

151. Before T22 merges (A104). First the owner: in Resend, verify one sending domain that belongs to Grow2Notes and is
     never the parent company's or a subdomain of it, add the DNS records Resend gives and a DMARC record, turn open and
     click tracking off, protect the account with multi-factor sign-in and the fewest members, and create a
     sending-only API key for test (`infra/README.md`, *Email through Resend*). Then the operator, in one Git Bash shell
     and signed in to the Azure CLI as Owner and to the GitHub CLI as an admin of the repository: store the key as
     `resend-api-key` in `kv-grow2notes-test` as that section says, set the `test` environment's secret
     `EMAIL_SENDER_ADDRESS` with `gh secret set --env test`, and run *Running it again* from a clone of the T22 branch,
     steps 1 to 4, passing the GitHub subject prefix at run time (D89) and the budgets' start date. Look for the
     what-if showing one new role assignment, `Key Vault Secrets User`, in each group and nothing else, then for the
     deployment to succeed. The old `Communication and Email Service Owner` assignment stays until step 153, so test
     keeps sending through the old sender until T22 deploys. Do not write down the key, the prefix, an ID or an address.
     (A104, D92, D89. Until `EMAIL_SENDER_ADDRESS` is set, every test deploy stops at its what-if, before any change,
     and test keeps its previous build: expected, not a defect. Step 150's record of "no change" is of the earlier
     bootstrap and stands.)
152. After T22's first test deploy, the cold-start check. Restart the web app `app-grow2notes-test` in the Azure portal
     and, within a minute of it starting, invite a new worker from Users to the testing mailbox. Look for the invite
     arriving within 5 minutes and no "Not sent" on screen. If it answers "Not sent", wait a minute and invite again: if
     only the first fails, the first send's 5 seconds were too short for the cold start, and the developer raises
     `SendTimeout` in `EmailSending.cs` to 8 seconds (T22's Notes). If every send fails, the key or the role is
     missing: look at step 151. (D92, the one 5-second deadline over the vault read and the post.)
153. After T22's first test deploy has sent an invite (step 152), delete the old email resources. In the Azure portal,
     delete `acs-grow2notes-test` and `ecs-grow2notes-test` from the test resource group; the production group should
     never have had any. Then delete the app identity's old `Communication and Email Service Owner` role assignment in
     both groups, as `infra/README.md` says. Look for neither group listing a Communication or Email resource, and each
     group's role assignments for the app identity being Key Vault Crypto Service Encryption User, Key Vault Secrets
     User and Monitoring Metrics Publisher. (D92; ops/security-review.md F-8.)
154. Run `infra/locks.bicep` for test as Owner, with `infra/README.md`'s *Locking the database* command, once the test
     database exists. Look for the deployment succeeding and `az lock list --resource-group rg-grow2notes-test-ause`
     showing `grow2notes-database` at level `CanNotDelete`. (S00.02.05; design.md §10.1.)
155. With the lock in place, try the two things it matters for, on test only. First `az sql server delete` for
     `sql-grow2notes-test` in its resource group. Look for the Azure CLI refusing with `ScopeLocked`. If it is not
     refused, the test server and database are gone: test holds made-up data only, so rebuild it by hand (a deploy of
     `main.bicep`, then `infra/sql/grant-identities.sql`, then Deploy), update design.md §10.1 to say that long-term
     backups are the protection, and tell the developer. Then `az sql db rename` of `sqldb-grow2notes` to a temporary
     name and straight back, while nobody is using test. Look for whether each rename is accepted or refused. Then
     `az lock list` for the group: look for `grow2notes-database` still on the database, and run step 154 again if it is
     gone. Write down the dates and the two results (no IDs): the developer records them in S00.02.05's Notes and in
     `restore-drill.md` step 4. This step holds nothing open. (S00.02.05's first criterion; design.md §14 M0, the last
     Done-when line; D90.)
156. In the next test deploy after the lock, open the run's log. Look for the step that adds the runner's firewall rule
     and the step that removes it both succeeding. (S00.02.05's second criterion.)
157. Production's checks are not here: they are in [go-live.md](go-live.md), which the operator works through before
     any real data goes in. (S00.02.04, S06.03.01, S06.04.01, S06.05.01.)

### The provider's organisation in production, once T27 is on main

158. Before anyone starts [go-live.md](go-live.md) D, steps 141 and 142 are done and their results recorded
     (`ops/security-review.md`'s ZAP section and T25's Notes). Its D1 does not pass without them, so no real personal
     information goes into production first. (S06.05.02's first criterion; design.md §14 M6; D1.) The production run,
     the first manager's setup and production's first email are go-live.md D, done by the operator, the owner and the
     first manager, and nothing about them is written here. (S06.05.02; S06.05.01's third and fourth criteria; A105.)
     This step holds nothing open and writes nothing down. (D90.)

### Staff onboarding in test, once T28 is on main

159. Before the managers' 30-minute onboarding ([go-live.md](go-live.md) E line 6), the operator readies test with
     made-up participants, goals, common items in their groups and guide prompts (those from earlier steps will do),
     and one made-up worker account, set up as earlier steps do. The managers present from a laptop signed in to that
     account, and the workers watch and may take turns on it. Nobody types a real participant's details or a real
     staff address, nobody sets up their own phone on test, and production is not used. The topics are design.md §14
     line 6's: writing and submitting today's note, using the flag, editing keeps the history, incidents stay on the
     existing incident form, and nothing needs saving on the device. Cross-device passkeys are explained only, because
     each worker makes their production passkey at their own setup (go-live line 5; passkey-flows.md). The dates held
     go on the provider's copy of go-live.md E, as line 6's evidence. This step holds nothing open, and any problem
     becomes a bug. (S06.06.03; A106, A107, A35, D90.)

### The compliance pack for the provider, once T29 is on main

160. Early in Release 2, the owner names the business AI account the operator's Support person will use: its AI
     provider and account type (A108). The operator writes them into its copy of
     [mcp-compliance-pack.md](../design/mcp-compliance-pack.md), kept outside the repository, with the Support account
     holders in `[Support account holders]`. On that copy it answers each of part 1's seven checks from that AI
     provider's current terms, with the date checked and a link, fills in `[AI providers]` and `[countries]` with its
     own AI provider for now, sends the copy to the provider and writes the date sent on it. The provider names its
     privacy contact on the copy it received, and its lawyer or privacy officer checks the wording. It confirms that
     the provider has the operator's check and the draft wording while the server is built. This checklist only
     points to the pack: nothing filled in is written here or anywhere in the repository. This step holds nothing
     open: the provider's approval, its decision, publishing and signing are switch-on lines 2 to 6 (mcp-server.md
     §11.1). (S07.05.06; S07.05.07's first and third criteria; A108, D57, D90.)

### Approved assistants and discovery in test, once T30 is on main

161. After the merge's deploy, the operator opens test's web app in the Azure portal and reads its settings
     (Settings, Environment variables) without changing any: `Mcp__Enabled` must read `True`, because
     `infra/test.bicepparam` sets it and every deploy from `main` replaces the app settings. Then open
     `https://app-grow2notes-test.azurewebsites.net/.well-known/oauth-authorization-server` and
     `https://app-grow2notes-test.azurewebsites.net/.well-known/openid-configuration` in a browser, not signed in. Both
     show the same JSON document, with `issuer` as the app's address followed by `/`, `authorization_endpoint`
     ending `/oauth/authorize` and `token_endpoint` ending `/oauth/token`, `grant_types_supported` of
     `authorization_code` and `refresh_token`, `response_types_supported` of `code`,
     `code_challenge_methods_supported` of `S256` alone, `scopes_supported` of `grow2notes.admin` and
     `offline_access`, `token_endpoint_auth_methods_supported` of `none` alone,
     `authorization_response_iss_parameter_supported` and `client_id_metadata_document_supported` both `true`, and
     no `registration_endpoint` and no `jwks_uri`. This step holds nothing open. (S07.01.04; MA2, MA11; D90.)
162. In test's SSH console, as [break-glass.md](break-glass.md) says, run `admin mcp-client add --metadata-url` with
     the client ID that one AI assistant publishes in its own documentation (mcp-server.md §11.2 gives examples), and
     a name, company and data location of your own. Compare the printed `Document client_name` and the redirect URIs
     with that documentation, then run `admin mcp-client list`: the assistant shows once, with the name, company and
     data location you typed and no token data. Leave it registered for step 163. If the assistant's address does not
     answer, or its document is refused, the refusal names why; that is not a bug in Grow2Notes, and you may use a
     different assistant. This step holds nothing open. (S07.01.05; MA1, MA2; A114.)
163. Open `https://app-grow2notes-test.azurewebsites.net/.well-known/oauth-protected-resource`. Until T32 is in
     test it answers `404` with a short problem-details answer, not the app's screens. From T32 it answers `200`
     with four fields: `resource` of `https://app-grow2notes-test.azurewebsites.net/mcp`, `authorization_servers`
     holding the one address `https://app-grow2notes-test.azurewebsites.net/`, `scopes_supported` holding
     `grow2notes.admin`, and `bearer_methods_supported` holding `header`.
     With the assistant that step 162 registered, open `/oauth/authorize` on the same address in a browser, with that
     assistant's client ID and one of its redirect URIs, `response_type=code`, any 43-character `code_challenge` of
     letters and digits with `code_challenge_method=S256`, `resource=https://app-grow2notes-test.azurewebsites.net/mcp`,
     `scope=grow2notes.admin` and any `state`. Until T31 is on main it answers `404`, with no redirect to the
     assistant and no code. From T31 it answers `302` to `/connect?request=` and a long handle, and still no code
     goes to the assistant, because the person has not chosen Allow (MA4); steps 166 to 170 cover what follows. This
     step holds nothing open, and a code that arrives at the assistant before Allow is a bug. (S07.01.04; MA4,
     MA11.)
164. In the same console, run `admin mcp-client add --client-id` with a made-up client ID such as
     `step-164-sample-client`, one `--redirect-uri` of `https://sample.example/callback` and one of
     `http://127.0.0.1/callback`, and a made-up name, company and data location. Run the same command again with
     another name (the other options as before): it prints `Updated`, not `Added`. Run `admin mcp-client list`
     (both assistants show), then `admin mcp-client remove step-164-sample-client` and `list` again (it is gone and
     the other is not). Last, run `add --metadata-url` with `http://assistant.example/client.json` and the same kind
     of options: it is refused, and `list` shows nothing new. Remove the assistant of step 162 too if the provider has
     not approved it. You read the audit rows as in step 15:

     ```sql
     SELECT EventType, OccurredAtUtc, ActorUserId, EntityType, OrganisationId, Details
     FROM dbo.AuditEvent
     WHERE EventType LIKE 'admin.mcp_client_%'
     ORDER BY OccurredAtUtc;
     ```

     Look for: from step 164, one `admin.mcp_client_added`, one `admin.mcp_client_updated` and one
     `admin.mcp_client_removed` row for each organisation in test (check the rows by `OrganisationId`: each ID
     shows all three), each with no actor and no `EntityType`, and a
     `Details` of the client ID and the name alone, and none from the refused run. This step holds nothing open.
     (S07.01.05; A111, A114, D90.)
165. In the Azure portal, read production's web app settings without changing any: `Mcp__Enabled` must read
     `False`, or be absent if no production deploy has run since T30. Then open
     `https://app-grow2notes-prod.azurewebsites.net/oauth/authorize`, `/mcp` and
     `/.well-known/oauth-authorization-server` on that address: each answers `404`. Production stays this way until
     T39 switches the access on at an agreed time (mcp-server.md §11.1). This step holds nothing open.
     (S07.01.04; MA11; D90.)

### Support accounts and the Connect screen in test, once T31 is on main

166. In test, as a manager, invite a made-up person with the role **Support (IT operator)** on the Users screen
     (Release 2 turns the access on in test, so the option is there; production ships the option before switch-on,
     A110). Open the setup link in a private window, set up with a passkey, or a password and an authenticator, and
     sign in. Look for: the one line "This account is only used to connect an AI assistant." with a **Sign out**
     button, and no navigation. Open `/`, `/manage/users` and `/no-such-page` on the same address: each shows the
     same page. Press **Sign out**, sign in again and use Account, then **Sign out**, to check both. As the manager,
     the new person shows as "Active · Support". This step holds nothing open. (S07.01.01, S07.01.02; A115.)
167. In test's SSH console, as [break-glass.md](break-glass.md) says, run `admin mcp-client add --client-id` with a
     made-up client ID such as `step-167-sample-client`, one `--redirect-uri` of `https://sample.example/callback`,
     and a made-up name, company and data location. Make a PKCE pair: a `code_verifier` of 43 letters and digits, and
     its `code_challenge`, the base64url of its SHA-256 without padding. With no session in the browser, open
     `/oauth/authorize` on test's address with that client ID, the redirect URI,
     `response_type=code`, the challenge with `code_challenge_method=S256`,
     `resource=https://app-grow2notes-test.azurewebsites.net/mcp`, `scope=grow2notes.admin offline_access` and a
     `state` of your own. Do this on Safari and Chrome, on a phone and a laptop. Look for: the sign-in screen, then,
     after a passkey or a password and 6-digit code, the same Connect screen; the made-up name, company and data
     location and the two hosts (`step-167-sample-client` as the client ID and `sample.example`), and no
     loopback warning. Press **Allow**. The browser goes to `https://sample.example/callback` (which does not
     answer) with `code`, your `state` and `iss` in the address. Copy the code and, with curl, `POST` to
     `/oauth/token` the form fields `grant_type=authorization_code`, `code`, `redirect_uri`, `client_id`,
     `code_verifier` and `resource`, and no secret. Look for: `token_type` of `Bearer`, `expires_in` of `600`, and a
     `refresh_token`. Repeat with the same code: `invalid_grant`. Then refresh with `grant_type=refresh_token`, the
     `refresh_token` and `client_id`: a new pair comes back. Read the audit rows as in step 15, with
     `EventType LIKE 'mcp.%'`: one `mcp.authorised`, with the client ID and `grow2notes.admin offline_access`. A
     real assistant's own connection is step 172. Remove the made-up assistant with
     `admin mcp-client remove`. This step holds nothing open. (S07.01.06, S07.01.08; MA2, MA3, MA4.)
168. As a worker in test, open an `/oauth/authorize` link as in step 167 (register the made-up assistant again). Look
     for: the line "Only managers can connect an AI assistant to Grow2Notes." and **Cancel** only. Press **Cancel**:
     the browser goes to the redirect URI with `error=access_denied`, your `state` and `iss`, and no code. The audit
     rows show one `mcp.authorisation_refused` with `why` of `not_allowed`. Do the same as a manager and press
     **Cancel**: `why` is `cancelled`. This step holds nothing open. (S07.01.07; MA4.)
169. Open an `/oauth/authorize` link with a client ID that is not registered, such as
     `https://unregistered.example/client.json`. Look for: "There is a problem with Grow2Notes", the browser never
     leaving test's address, and no redirect to the made-up address. In the application log (Log stream in the Azure
     portal, or the log search), find the warning "Refused an authorisation request from client" with that client ID.
     This step holds nothing open. (S07.01.06; MA2, MA4.)
170. With VoiceOver on a phone and a screen reader on a laptop, go through the Connect screen as a manager and the
     Support page: the heading is announced when the page opens, **Allow** and **Cancel** are reachable in that
     order, the buttons say what they do, and the loopback warning, when it shows, is read with the rest. Any
     failure becomes a bug. This step holds nothing open and does not hold go-live (A107). (S07.01.06, S07.01.07,
     S07.01.02; A107.)


### The /mcp endpoint in test, once T32 is on main

171. From a laptop, with curl and no sign-in, `POST` an empty JSON object to test's `/mcp` address with
     `curl -i -X POST -H "Content-Type: application/json" -d "{}"`. Look for: `401`; a `WWW-Authenticate` header that
     starts `Bearer`, with `resource_metadata` naming test's `/.well-known/oauth-protected-resource` address and
     `scope="grow2notes.admin"`, and no `error` parameter; `Cache-Control: no-store`; and no header that starts with
     `Access-Control-`. Repeat with `-H "Authorization: Bearer not-a-token"`: the same `401`, now with
     `error="invalid_token"`. Repeat with `-H "Origin: https://unapproved.example"` and no token: `403` with a short
     problem-details answer, not a `401`, because the Origin is checked first. Then sign in to the app in a browser
     as a manager and, in the same browser, open `/mcp` in a new tab: a `401` answer, not the app, because the
     session cookie is not accepted there. Last, open `/.well-known/oauth-protected-resource/mcp`: `404`. This step
     holds nothing open. (S07.02.01; MA10; A120.)
172. With a real approved assistant, or the MCP Inspector, registered as in step 162, add test's `/mcp` address as a
     remote server. Look for: it follows the `401` to the sign-in and the Connect screen of step 167 (a manager signs
     in, with MFA, and chooses **Allow**), connects, and lists two tools, `find_participants` and
     `get_participant` (later tasks add more). Write down which assistant it was, the protocol revision it reports
     (2025-11-25 or 2026-07-28) and, from the browser's Network panel when it runs in a browser, the `Origin` its
     calls carry, or that it sends none. A `403` on its calls means that its `Origin` is not the origin of a redirect
     URI of its registration: correct the registration (`admin mcp-client add` again) and note it. Read the audit
     rows as in step 15, with `EventType LIKE 'mcp.%'`: one `mcp.authorised` and no `mcp.read`, because listing
     tools is not audited. This step holds nothing open. (S07.02.01; MA10; A119.)
173. Connect an assistant as in step 172 for a made-up manager of the test organisation, and make one `/mcp` call.
     Then end that person's access, using a new made-up manager and a new connection for each way: the person signs
     out of the app in a browser (Account, then **Sign out**); another manager deactivates the person on the Users
     screen; another manager resets the person's sign-in (the link is sent, not used); another manager changes the
     person's role to Worker. Look for: the assistant's next call fails with `401`, and it can only reconnect by
     running the Connect screen again, with MFA (a reactivated or reset person reconnects that way; a worker is
     refused with `access_denied`). Changing a manager to Support does not end the connection. Read the audit rows
     as in step 15, with `EventType IN ('auth.signed_out', 'user.deactivated', 'user.setup_link_sent', 'user.updated')`:
     one row for each change you made. Then set `Mcp__Enabled` to `False` on test's web app in the Azure portal, wait
     for the restart, and sign out, deactivate and reset another made-up person: each works exactly as before, and
     `/mcp` answers `404`. Set `Mcp__Enabled` back to `True`. This step holds nothing open. (S07.02.02; A118.)

### The participant look-ups in test, once T33 is on main

174. Connect an assistant as in step 172, in a test organisation with made-up participants, and ask it to find a
     made-up participant by name, then a name that matches nobody, then to get a participant by a made-up ID. Read
     the audit rows as in step 15, with `EventType IN ('mcp.read', 'mcp.refused')`. Look for: `mcp.read` for the
     first, with `via` of `mcp`, the assistant's client ID, the tool's name, `participantIds` holding the one ID, a
     `count` of `1` and no name or date of birth, and `ParticipantId` set; `mcp.read` for the second, with
     `participantIds` empty and a `count` of `0`, and no `mcp.refused`, because finding nobody is not an error; and
     `mcp.refused` with `code` of `not_found` for the third, with the same `via`, `client` and `tool`. The
     assistant's reply shows the participant's name, and the audit row does not. This step holds nothing open.
     (S07.02.03; S07.02.04; MA8.)
175. In the same test organisation, with a participant who has a date of birth and both active and archived goals,
     ask the assistant to describe that participant. Look for: it gives the name, the status and the goals, the
     active ones first and then the archived ones, and no date of birth and nothing about notes. Add enough
     participants that more than 10 match a search of two letters or more such as `an` (or give them a shared family
     name and search for it), then ask the assistant to find them: it shows 10 and the line "More matches: narrow
     the search." Ask it to find `J` on its own (one letter): a real client
     must show the refusal "Search must be at least 2 characters" and let the assistant correct itself, not a client
     error. This is the interoperability check on error results: if a real client rejects them because their
     structured content does not match the tool's output schema, the one place to fix is `ToolError.ToResult`, with
     a change to mcp-server.md §5.5. Last, connect an assistant for a manager of a second test organisation and ask
     it to find the first organisation's participant by name and to get that participant by its ID: it finds
     nobody and gets `not_found`. This step holds nothing open. (S07.02.04; MA5; A124 to A126.)
