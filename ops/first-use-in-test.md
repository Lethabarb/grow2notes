# First use in test

One ordered checklist of every check by hand still outstanding in test,
`https://app-grow2notes-test.azurewebsites.net`, for the first time the owner signs in there. On 12 October 2026 the
owner decided that checks like these are nice-to-haves that do not hold a story open (backlog README §9): the stories
are done on their checks in CI and their read-only checks after the deploy, and the criteria that only these steps can
prove stay unticked, each with a Note pointing here. Each step ends with what it confirms.

## Before you start

- Test holds made-up data only (A35): made-up names, such as `Sample Support Services` and `Alex Sample`, and a new
  `example.org` address for each account, such as `alex.sample.20261013@example.org`.
- Have ready: a laptop with Chrome and NVDA, an iPhone with VoiceOver and an authenticator app (such as Google
  Authenticator or Microsoft Authenticator), and the Azure portal signed in with your own account.
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
10. Leave Today untouched for 28 minutes, in front and with the screen kept awake. Look for: the warning opens with
    focus on Stay signed in; NVDA says "You'll be signed out in 2 minutes", "dialog" and Stay signed in; left open, its
    sentence changes at 29:00, 29:20 and 29:40. Before 30:00 press Stay signed in. Look for `POST /api/auth/ping`
    answering `204`, the warning closing and focus back where it was. Then sign out as in step 7. (S00.04.07; its
    screen-reader spot check.)

### The second account: a password and an authenticator app

11. Until Invite user is in test (S00.06.02), run `admin bootstrap` once more, as in steps 1 to 3, for a second
    made-up organisation and manager (account B), with a new `example.org` address, and leave the console open. The
    organisation stays in test for good, as every test organisation does.
12. Open account B's link in Chrome on the laptop, with NVDA running. Choose Use a password and authenticator app,
    create a password, then scan the QR code with the phone's authenticator app and enter its 6-digit code. Look for:
    setup ends on Today, and NVDA reads views D to G as sign-in.md's *Accessibility checklist* says. Reopen the link and
    look for "This link has expired", then close the console as in step 5, and sign out as in step 7. (S00.04.03's and
    S00.04.05's screen-reader spot checks, handed to F00.04's feature check.)
13. On the iPhone, with VoiceOver on, sign in as account B with email, password and a 6-digit code. Look for Today,
    and VoiceOver reading the sign-in page and the code step as sign-in.md's *Accessibility checklist* says. Sign out
    with Sign out. (S00.04.05; F00.04's feature check, the phone, and the second sign-in on the password path.)

### What the developer reads afterwards (read-only)

14. The audit rows, as the Entra admin, through a temporary firewall rule for your own address on
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
15. The telemetry, which an agent can query: from step 8's time, the workspace holds its `POST /api/auth/login` `401`
    and one "Password sign-in with an unknown email" entry holding no address, and no `400`, `415` or `5xx` on
    `/api/auth/*`, and no exception. (S00.04.05.)

### Later, once Invite user is in test

16. F00.04's feature check also asks for a worker on each setup path, and for a manager's passkey on a phone, since
    account A's is on the laptop, each setting up and signing in a second time on a phone and a laptop. As account A,
    invite from the Users screen (S00.06.02) two workers and a third manager (account C), each with a new made-up
    `example.org` address, with the setup link as S00.02.07 and S00.06.02 make it reach a made-up address in test.
17. On the iPhone, with VoiceOver on, set up account C and one worker with Use a passkey (recommended), and the other
    worker with Use a password and authenticator app, typing the setup key into the phone's authenticator app. Look
    for: each setup ends on Today, and VoiceOver reads views D to G as sign-in.md's *Accessibility checklist* says.
    Sign each out with Sign out. (F00.04's feature check: setup on the phone, and its VoiceOver pass over views D to G.)
18. Sign each of the three in a second time on the laptop: the passkey ones with Sign in with a passkey, choosing the
    iPhone when Chrome offers it, and the password worker with email, password and a 6-digit code. Look for Today each
    time, then sign out as in step 7, and repeat step 14's query for their `auth.setup_completed` and
    `auth.signed_out` rows. (F00.04's feature check: the second sign-ins, on the laptop.)
