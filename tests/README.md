# tests

Automated tests for Grow2Notes (design.md §7.3, §10.4).

- `Grow2Notes.Tests/`: xUnit v3 unit tests and API integration tests. The integration tests host the app with
  `WebApplicationFactory` against a real SQL Server in Testcontainers, which CI's runner provides.
- `e2e/`: Playwright smoke tests and end-to-end tests of the app shell, account setup, sign-out, the warning before
  the idle sign-out, signing in again in place, the Users and Participants screens, a participant's goals, Common
  items, Guide prompts, Today and its unfinished drafts, the note form and Discard, someone else's draft, and Flagged's
  To review list, and the Report screen's downloads (also in Firefox and WebKit), with axe accessibility checks, run
  against the published app.

The SPA's unit tests are not here: they sit next to the code they test, as `*.test.ts` or `*.test.tsx` files under
`src/grow2notes-spa/src`, and run in Vitest with Testing Library.

**Where the tests run.** Tests are written with the work, but none is run on a developer's machine: the owner's rule of
20 October 2026 (decisions.md D90, design.md A98) is that the only local checks are compile and lint. CI runs every
test (unit, integration and end-to-end) on every pull request and is the authority; a pull request merges only after
review and with CI green. The sections below say what each test run covers and how to write the tests; they are not
steps to run on a machine.

## The SPA checks

Each is a separate step of CI's SPA checks (design.md §10.4 step 2). On a machine, in `src/grow2notes-spa` after
`npm ci`, only the compile and lint checks run:

```shell
npm run typecheck           # tsc over the app, the tests, the config files and scripts/
npm run lint                # ESLint; a warning fails it too
npm run build               # vite build into src/Grow2Notes.Web/wwwroot
```

`npm run build` does not type-check, so a type error shows up only in `npm run typecheck`.

CI runs the other scripts:

```shell
npm test                    # Vitest, once
npm run test:webkit         # the format tests again, in Playwright's WebKit (below)
npm run check:no-analytics  # no Application Insights JavaScript SDK or third-party analytics (design.md §9.5)
```

`npm run test:webkit` runs `src/copy/format.test.ts` alone, headless in Playwright's WebKit, Safari's engine, through
Vitest's browser mode (`vitest.webkit.config.ts`), because Safari's time-zone data can differ from Node's
(microcopy.md §8). The browser's zone is set to America/Los_Angeles, behind UTC, whatever the machine's, so that a date
read in the device's own time, which there falls on the day before, fails it. CI installs the WebKit build that the
SPA's `playwright` package drives (`npx playwright install --with-deps webkit`).

`npm test` needs no browser: it runs every test, `format.test.ts` included, in jsdom.

`npm run check:no-analytics` reads every package in `package-lock.json` and every file of the build, so CI runs it after
`npm run build`; its denylist is at the top of `scripts/check-no-analytics.ts`.

`npm test` also runs, in CI, foundations.md's two checks on the stylesheets, which have no script or CI step of their
own:

- `src/styles/tokens.test.ts`, the contrast test, checks each colour token in `tokens.css` against each background it
  is used on, at 7:1, 4.5:1 or 3:1 (the pairs are listed at the top of the test), and fails on a colour token in no
  pair.
- `src/styles/breakpoints.test.ts`, the breakpoint check, reads every `.css` file under `src/` and fails, naming the
  file and the rule, on a media query on width or height other than `(min-width: 40rem)` and `(max-height: 30rem)`,
  range syntax included, and on any `@container` rule.

The build keeps those queries in `min-width` form only because `vite.config.ts` sets `build.cssTarget`: at Vite's
default targets the minifier rewrites them in range syntax. `SpaBuildTests` checks the built stylesheets for it.

`npm test` also runs, in CI, microcopy.md §8's wording check, `src/copy/copy.test.ts`, which has no script or CI step of
its own either. It reads every `.ts` and `.tsx` module in `src/copy` except its tests, and fails, naming the file and
the word, on §8's banned words, on "please" outside "Please check your ticks.", on §8's US spellings, and on a negative
contraction outside design.md's two "can't" strings. With it, `src/copy/wordsInJsx.test.ts` lints sample components with
`eslint.config.js` and checks which words in JSX its rule refuses, as no JSX in `src` holds one for `npm run lint` to
refuse.

## The .NET tests

The integration tests serve the real SPA build, so CI builds the SPA (`npm ci`, `npm run build`) before
`dotnet test Grow2Notes.slnx`. If `src/Grow2Notes.Web/wwwroot` has no build, the test project's build stops with an
error saying so. On a machine, only the .NET build with warnings as errors runs: for that compile check, run `npm ci`
and `npm run build` in `src/grow2notes-spa` first, and again after changing the SPA.

## The end-to-end tests

CI runs them against the published app, on a SQL Server container that the migrations bundle has migrated (the
end-to-end steps of `.github/workflows/ci.yml`): it builds the SPA, migrates the container, publishes the app, starts
it on `http://localhost:5000`, installs the browsers and runs `npm test` in `tests/e2e`. They are not run on a machine;
the run's output on the pull request, with the HTML report and a trace of each failed test, is the evidence.

The tests open `http://localhost:5000` unless `E2E_BASE_URL` names another address. The setup tests
(`e2e/setup.spec.ts`), the two sign-out tests that sign in through setup (`e2e/sign-out.spec.ts`), the session
warning's tests (`e2e/session-warning.spec.ts`), the Users test that meets the app's own list (`e2e/users.spec.ts`),
the two Invite user tests that meet the app's own invite (`e2e/invite-user.spec.ts`), the Resend invite test that meets
the app's own resend (`e2e/resend-invite.spec.ts`), the two tests of signing in again that end a real session
(`e2e/sign-in-again.spec.ts`), the Guide prompts test that saves to the app (`e2e/guide-prompts.spec.ts`), the
Participants tests that meet the app's own answers (`e2e/participants.spec.ts`), the Common items tests that meet the
app's own answers (`e2e/common-items.spec.ts`), the two tests of a user's actions that meet the app's own
(`e2e/user-actions.spec.ts`), the Edit user test that meets the app's own save (`e2e/edit-user.spec.ts`), the eleven
note form tests that meet the app's own Today, notes, saves, Submit and Discard (`e2e/note-form.spec.ts`) and the
Flagged test that meets the app's own Submit and To review list (`e2e/flagged.spec.ts`) get their setup links from the
published app's own `admin bootstrap` and `admin reset-signin` (the `invite`, `secondInvite` and `resetSignIn` fixtures,
below), so CI's end-to-end step gives them the published app's folder, in `E2E_APP_DIR`, and the app's connection
string and `App__Origin`, which must be the address that the tests open. Without one of the three, each test that
needs a link fails and names it, and each that gets one adds a made-up organisation and manager to the database. After
a failure, the HTML report that CI uploads holds a trace of each failed test.

## Writing end-to-end tests

Every end-to-end test imports `test` and `expect` from `e2e/fixtures.ts`, not from `@playwright/test`. The fixture's
`page` fails the test, after it ends, if the browser's console reported a Content Security Policy violation, so each
screen's test also shows that the screen works under design.md §9.7's policy (S06.01.04). Other console errors, such
as the one Chromium logs for a `401`, do not fail a test.

`e2e/fixtures.spec.ts` shows that the fixture fails a test whose page meets a violation. It is marked `test.fail()`, so
it passes by failing: the run marks it as failed, with a cross, and counts it as passed.

A test checks a screen against WCAG 2.2 AA with `expectAxeToPass(page)` from `e2e/axe.ts`, which first expects the
page not to scroll sideways at 320 px, then runs axe at a phone's width and at a laptop's (see *The 320 px check in
`expectAxeToPass`*).

Seven more fixtures are there for the tests that name them, and only those run them:

- `invite` runs the published app's `admin bootstrap` from `E2E_APP_DIR`, with the app's settings, as
  `ops/break-glass.md` runs it in the SSH console, for a made-up organisation and manager, Alex Sample, with a new
  `example.org` address (A35). It gives the manager's display name and address, and the setup link that the command
  printed.
- `secondInvite` runs it once more, for another made-up organisation and manager, Jordan Sample, for a test of two
  people on one browser.
- `resetSignIn(email)` runs the published app's `admin reset-signin` for that address in the same way, and gives the
  link that it printed: a new one for someone invited from the Users screen, whose emailed link the test cannot read,
  or for someone whose sign-in it resets.
- `authenticator` adds Chromium's virtual authenticator to the page, through the DevTools protocol, in place of a
  phone: one built into the device, which makes discoverable passkeys and verifies the person, and answers each prompt
  at once. `setUserVerified(false)` fails the person's verification at the prompts that follow, so no passkey is made,
  `credentials()` lists the passkeys that it holds, and `addPasskey(rpId)` adds one that no account holds (below).
  With no passkey for the domain that a prompt names, the prompt fails at once with `NotAllowedError`, as dismissing
  it does.
- `secondDevice` gives a second device: a `page` in a browser context of its own, with none of the test page's
  cookies, under the same options and the same check of the policy, and its own `authenticator`, for a second person
  signed in beside the first, or one person on two devices.
- `thirdDevice` gives a third such device, for a third person beside the other two, such as a second manager beside
  a worker and the first (`e2e/flagged.spec.ts`).
- `secondTab` gives a second tab: a `page` in the test page's own browser context, with its cookies, so the same
  person, under the same check of the policy, for one person's note in two tabs; the test may close it, as a person
  closes a tab, or crash it, as a phone kills a tab (below).

A test that sets up an account with a password gets each code as a person's authenticator app would, from the setup
key that view G shows: `authenticatorCode(key)` from `e2e/authenticatorCode.ts` computes, with `node:crypto`, RFC
6238's code for the key at the current 30-second step, as Identity's authenticator provider checks it, and
`authenticatorCode(key, time)` the code at another time. Identity accepts the codes of the steps either side of now, so
a wrong code is the right key's code for a step ten minutes away. `e2e/authenticatorCode.spec.ts` checks the helper
against RFC 6238's test values, and the password path's tests in `e2e/setup.spec.ts` are the example.

Every request that a test's browser sends says that it came over HTTPS: `e2e/fixtures.ts` gives every test, and
every `secondDevice` and `thirdDevice`, `extraHTTPHeaders` of `clientHeaders()`, which holds `X-Forwarded-Proto: https`,
as every signed-out page sends a change through the SPA's `api()` wrapper, sign-in asking for passkey options as it
shows. The antiforgery, enrolment and session cookies are `Secure`, so the app issues them only on a request that is
HTTPS, as the forwarded scheme makes a request from loopback, where CI's run serves `http://localhost`; Chromium keeps
a `Secure` cookie from `localhost`.

`clientHeaders()` also gives each browser context a random forwarded client address of its own, from `2001:db8::/32`,
which RFC 3849 reserves for documents, so that the app's rate limits (design.md §9.9) count each context as a client of
its own, as each person's phone is, and not every test and device as one, which would soon be refused. The sign-in and
setup limit is 30 requests a minute for each address, so a test that adds sign-ins to one context counts them: a
sign-in page view asks for passkey options once, and a password sign-in sends the password and the code. A test that
makes its own browser context, with `browser.newContext()`, passes `extraHTTPHeaders: clientHeaders()` to it.

### Answering `/api/auth/me`

The end-to-end run signs someone in only through setup and the passkey or password it makes (`e2e/setup.spec.ts`), so a
test of a signed-in screen answers `GET /api/auth/me` itself, in the browser. `serveMe(page, me)` from `e2e/me.ts`
routes the page's requests for it, through Playwright's `page.route`, to an answer of `me`, one of the made-up `worker`
and `manager` there (A35), in the JSON that `MeEndpointTests` asserts, so that the request never reaches the app. Call
it before `page.goto`. To change who is signed in, call it again with the other person, whose answer then wins, and load
the page again, so that the SPA starts with them. It answers Today's lists too, `GET /api/today` with no participants
on `me`'s day and `GET /api/me/drafts` with no unfinished drafts: Today, where the made-up person lands, asks for both,
and the app, which holds no session for them, would answer `401`, ending the session in place. Everything else the
page loads is the published app's, under its policy. A test that does not call it meets the app's real `401`, as a
signed-out visitor does (`e2e/smoke.spec.ts`); `e2e/shell.spec.ts` is the example of one that does. A test of signing
out answers it only until the page sends the logout: `e2e/sign-out.spec.ts` adds a route ahead of `serveMe`'s that lets
the request through to the app from then on, so the page that the logout loads meets the app's `401`.

```ts
await serveMe(page, manager);
await page.goto('/manage');
```

### Making `/api/auth/me` fail, wait or crash the page

Beside `serveMe`, `e2e/me.ts` answers `GET /api/auth/me` in the browser in three more ways, each through `page.route`
and each overriding the answer before it, as `serveMe` does. `e2e/start-up.spec.ts` is the example of all three.

- `failMe(page, 'no connection')` aborts each request, as a phone with no signal does, rather than
  `context.setOffline(true)`, which would stop the page itself loading; `failMe(page, 503)` answers each with that
  status and no body, for a server problem. The query client tries such a request once more, silently, 1 s later, so
  the start-up message shows about 1 s after the page loads, and the "still" sentence about 1 s after Try again. For
  the next Try again to bring the screen, the test then calls `serveMe`.
- `holdMe(page, me, ms)` answers each with `me`, `ms` after the page sent it, for the start-up's "Loading…", which
  shows from 1 s, and Today's list as `serveMe` does, at once.
- `answerMe(page, body)` answers each with JSON that is not a `Me`, so that the page throws as it renders. The worker
  with a number for `today`, which Today's heading cannot read, crashes the screen, which shows the crash page under
  the nav; `null`, which the header cannot read, crashes the shell, which shows its own crash page. React writes each
  crash to the console as an error, which does not fail the test (above).

```ts
await answerMe(page, { ...worker, today: 20261001 });
await page.goto('/');
```

To see `index.html`'s boot line, a test holds back the page's script with a route for `**/assets/*.js` that waits until
the test lets it through, and opens the page with `page.goto('/', { waitUntil: 'commit' })`, as `DOMContentLoaded` and
the load event wait for the script. With JavaScript off, through `test.use({ javaScriptEnabled: false })`, `getByText`
does not find the `<noscript>` line, but `page.locator('noscript > p')` does.

A test that counts the requests for `/api/auth/me` counts the `GET`s that `page.on('request')` reports. A request that
a step sent may be reported after the step, so a count that should not have grown is checked once the test has the
answer to a request of its own, `fetch('/healthz')` in `page.evaluate`: the browser reports a page's requests in the
order that it sends them. Time with nothing done runs on Playwright's clock, installed with `page.clock.install()`
before `page.goto` and moved on with `page.clock.runFor('30:00')`.

### Answering `/api/admin/users`

The Users screens list the organisation's users from `GET /api/admin/users`, which needs a manager's session, so a test
of them answers it in the browser, as it answers `/api/auth/me`. `e2e/users.ts`, beside `e2e/me.ts`, has users.md's
made-up people (A35), in the JSON that `UserListEndpointTests` asserts: Sam Lee, who is `me.ts`'s `manager`, with the
same user ID, Jo Smith, Alex Park, who is Invited, and Chris Ng, who is Deactivated, together in `people`, in no
particular order, as the API sorts nothing (design.md §6.6). `madeUpUser` makes others, such as a name with an accent or
an 80-character email. Each of its two helpers routes the page's requests through `page.route`, overriding the answer
before it, as `serveMe` does, so that the request never reaches the app. Call them before `page.goto`.

- `serveUsers(page, users)` answers each request with `users`.
- `failUsers(page)` aborts each, as `failMe(page, 'no connection')` does. The list's failure then shows about 1 s after
  the page loads, after the query client's silent retry, and the "still" sentence about 1 s after Try again. For the
  next Try again to bring the list, the test then calls `serveUsers`.

```ts
await serveMe(page, manager);
await serveUsers(page, people);
await page.goto('/manage/users');
```

A test of the region's loading line holds the list's request with `holdNext(page, '**/api/admin/users')` from
`e2e/requests.ts`, a route for the page's next request alone, which hands the test that request to answer or let
through, while Playwright's clock, paused before the test opens the list, runs the page on to each mark: nothing at
999 ms, and "Loading users…" at 1,000 ms. A test of the Tab stops reads each with `whereFocusIs(page)` from
`e2e/focus.ts`, which gives the focused element's text, "the page itself" for `<body>`, or "outside the page" once focus
has gone to the browser. `toHaveAccessibleDescription` gives a row's description as Chromium computes it, with a space
before each comma that only screen readers get, "alex.park@example.org , Invited , Worker", which they do not voice
(S00.05.01's Notes). The one test that meets the app's own list signs in for real, through the `invite` fixture and
setup with a passkey on the `authenticator` fixture, so that the organisation that `admin bootstrap` made lists its
first manager alone. `e2e/users.spec.ts` is the example of each, and `e2e/shell.spec.ts`'s walk, whose Users link opens
the list, answers it too.

Invite user sends `POST /api/admin/users` to the same address, so `e2e/users.ts` has two more helpers for it, each
routing the invite alone and passing every other request on, through `route.fallback()`, to the routes before it,
such as `serveUsers`'s. Call them after `serveUsers`, as Playwright tries the latest route first and `serveUsers`
answers every method.

- `answerInvites(page, answer)` answers each invite in the browser: `{ status, problem }` with that status and an RFC
  9457 problem holding `problem`'s members, such as `{ code: 'validation.failed', errors: { role: ['…'] } }`, or
  `'no connection'`, which aborts it.
- `holdNextInvite(page)` holds the next invite until the test answers it or lets it go on to the app, as `holdNext`
  holds a request, for the busy button's "Sending…" and a double tap.

```ts
await serveMe(page, manager);
await serveUsers(page, people);
await answerInvites(page, { status: 500 });
```

The tests that meet the app's own invite sign in for real, as above, and invite a made-up person with a new
`example.org` address (A35), whose email the app writes to its standard output, as CI's run sets
`Email__UseConsole` and so sends no real email (T01's Notes). `e2e/invite-user.spec.ts` is the example of each.

Resend invite on an Invited user's page sends `POST /api/admin/users/{id}/setup-link`, whose path `setupLinks` in
`e2e/users.ts` matches for any user. `answerSetupLinks(page, answer)` answers each resend in the browser:
`{ sentAt }`, the app's `204` with that `Date` header, such as `'Wed, 14 Oct 2026 05:12:00 GMT'`, from which the page
takes the time it shows, or as `answerInvites` answers an invite. `holdNext(page, setupLinks)` holds the next one, for
the busy button and its emptied line, until the test answers it with `route.fulfill`. A test of the device's clock and
time zone sets the zone with `test.use({ timezoneId })` and the day with `page.clock.install({ time })`, both before
the page loads. The test that meets the app's own resend invites a worker through Invite user first, and reads the
time from the app's own `Date` header. `e2e/resend-invite.spec.ts` is the example of each.

Reset sign-in sends the same `POST …/setup-link`, and Deactivate and Reactivate send theirs to the paths that
`deactivations` and `reactivations` match. `answerChanges(page, pattern, answer)` answers each change to `pattern`, one
of the three, as `answerSetupLinks` answers a resend, which it is with `setupLinks`; `holdNext(page, pattern)` holds
the next one, for a busy dialog. Beside users.md's four, `kim` is a second Active manager, Kim Tran, so that Sam Lee's
own page offers Reset sign-in and Deactivate, and `withChanged(users, user, changed)` gives the list as the app gives
it after a change to `user`, with a new stamp, which a test serves before the press, for the refetch that follows the
change. A test of your own reset or deactivation calls `failMe(page, 401)` before it confirms, as the change ends the
session, so that the page that it loads afresh meets the app's `401`. The two tests that meet the app's own changes
sign in for real and invite through Invite user: a worker, deactivated and then reactivated; and a second manager, who
sets up on the `secondDevice` from the link that `resetSignIn` gives, and whose sign-in the first then resets.
`e2e/user-actions.spec.ts` is the example of each, `expectNothingKept(page, ...values)` from `e2e/kept.ts` checks that
no name or address is kept where a page could keep it (users.md AC3), and `signInInPlaceAs(page, me)` from
`e2e/signedOut.ts` signs in again on view C, the password and code steps answered in the browser.

Edit user's Save sends `PUT /api/admin/users/{id}`, whose path `edits` in `e2e/users.ts` matches for any user.
`answerEdits(page, answer)` answers each save in the browser: `'saved'`, the app's `200` with no body and no `ETag`, or
as `answerInvites` answers an invite. `holdNext(page, edits)` holds the next one, for the busy button, or, in a test of
your own change, until the test has changed what `/api/auth/me` answers, which the page asks again on the entry that
it lands on: `serveMe` with the role Worker or the new name, or `failMe(page, 401)` after a new address, which ends
the session. `recordRequests(page)` from `e2e/requests.ts` records each request that the page sends, with its
`If-Match`, and `signInAndOpenUsers(page, invite, authenticator)` and `inviteThroughThePage(page, person, role)` from
`e2e/users.ts` sign in for real and invite through Invite user, for each test that meets the app's own Users screens.
The test that meets the app's own save renames a worker invited through Invite user and gives them a new address.
`e2e/edit-user.spec.ts` is the example of each.

### Answering `/api/admin/guide-prompts`

Guide prompts loads the organisation's prompts from `GET /api/admin/guide-prompts`, which needs a manager's session too,
so a test of it answers that in the browser in the same way. `e2e/guidePrompts.ts` has the design's sketch of the
prompts, with a blank line inside them, as `savedPrompts`, and `promptsAnswer(text, version)`, the `GET`'s answer: the
JSON `{text}` with a made-up row version in the `ETag`, in quotes, as the API writes it. Its two helpers route the
page's `GET`s through `page.route`, as the Users helpers route theirs, and pass any other request on to the routes
before them.

- `servePrompts(page, text)` answers each `GET` with `text`.
- `failPrompts(page)` aborts each, as `failUsers(page)` does.

A test of the loading line holds the next `GET` with `holdNext` and answers it with `promptsAnswer`, as the Users test
does. `e2e/guide-prompts.spec.ts` is the example, and `e2e/shell.spec.ts`'s walk, whose Guide prompts link opens the
editor, answers it too.

Save sends `PUT /api/admin/guide-prompts`, which a test meets in the browser too, so that no save reaches the app.
`savedAnswer(version, date)` is its `200`: no body, a made-up row version in the `ETag` and the server's time in the
`Date`, by default `savedAt`, 06:12 UTC on 1 October 2026, which the page shows as "Saved 4:12 pm". `conflictAnswer` is
its `412 precondition.failed`, as the API answers once another manager has saved.

- `serveSaves(page, answer)` meets each `PUT` with what `answer` does to its route, such as
  `route.fulfill(savedAnswer())` or `route.abort('internetdisconnected')`, and gives the list of the `PUT`s sent, each
  as its `text` and `If-Match`, which grows as the page sends them. `sentSave(route)` reads one, as from a `PUT` that
  `holdNext` held, which a test of the busy label or of typing during a save answers when it chooses.
- `promptsStore(text)` keeps the prompts as the API does, for the pages of two managers to load and save through: each
  `PUT` whose `If-Match` names the version it holds stores its text and moves the version on, and any other gets `412`.

The one test that meets the app's own endpoint signs in for real, through the `invite` fixture and setup with a passkey
on the `authenticator` fixture, as the Users test does, and saves to the organisation that `admin bootstrap` made.

`e2e/guide-prompts.spec.ts` extends the fixtures' `test` with one more, `deviceWrites`, which runs in each of its tests
without being named, as the screen's Definition of Done asks that nothing be kept on the device on any of its paths
(guide-prompts.md *Storage and naming*). It spies on `Storage.prototype.setItem` and `indexedDB.open` in each page of
the test's browser context as it loads, and fails the test, once it has ended, on any call; a test that names it reads
the calls so far, as the file's own test of the spies does. A second test, marked `test.fail()` as the one in
`e2e/fixtures.spec.ts` is, writes to `localStorage` without naming it, which shows that the fixture runs, and fails a
test, by itself.

### Answering `/api/admin/participants`

The Participants screens read the organisation's participants with `GET /api/admin/participants?includeArchived=true`,
their one read, which needs a manager's session too. `e2e/participants.ts`, beside `e2e/users.ts`, has made-up
participants (A35), in the JSON that `ParticipantListEndpointTests` asserts: Jane Citizen and Ravi de Silva, active, and
Ben Archer, archived, together in `participants`, in the API's order, by family name, then given name.
`madeUpParticipant` makes others, such as one with a long name, and a test changes one by spreading it with another
`status`, date of birth or `rowVersion`. Each of the two helpers routes the page's requests through `page.route`,
overriding the answer before it, as `serveUsers` does. Call them before `page.goto`.

- `serveParticipants(page, list)` answers each request with `list`.
- `failParticipants(page)` aborts each, as `failUsers` does. The failure then shows about 1 s after the page loads,
  after the query client's silent retry, and the "still" sentence about 1 s after Try again. For the next Try again to
  bring the list, the test then calls `serveParticipants`.

```ts
await serveMe(page, manager);
await serveParticipants(page, participants);
await page.goto('/manage/participants');
```

`e2e/shell.spec.ts`'s walk, whose Participants link opens the list, answers it too. A change to a participant, sent
after an antiforgery token that the app gives anyone, is answered in the browser as well, by `answerChanges` in
`e2e/participants.spec.ts`: Add participant's `POST`, a correction's `PUT`, and Archive participant's and Restore's
`POST`s, each with a status, a body if any, and a `Date` of 4:12 pm in Melbourne, from which "Saved 4:12 pm" reads, or
aborted, for no connection. The page reads the list again after each change, so the test first serves the list as the
change leaves it, calling `serveParticipants` again with the participant changed.

The tests that meet the app's own answers sign in for real, through the `invite` fixture and setup with a passkey on
the `authenticator` fixture, so that the organisation that `admin bootstrap` made starts with no participants: one adds,
corrects, archives and restores a participant at 375 px; one adds a participant and five goals at 375 px, then
rewords, moves, archives and restores one of them; and two more add, correct, archive and restore a participant, and
add two goals and archive one, with the keyboard alone, at 375 px and at a laptop's width.
`signInThroughSetup(page, invite, authenticator)` from `e2e/setupSignIn.ts` sets the account up, adding one passkey
to the authenticator, and ends on Today.
`expectStops(page, key, stops)` from `e2e/focus.ts` presses Tab or Shift+Tab, or an arrow key in a set of radios, and
reads each stop: the focused element by the text that shows, so that a button's busy label, hidden until it is busy, is
left out, a field or a radio by its label, and "outside the page" past the last stop, from where the next Shift+Tab
comes back to it; and its focus ring as it is drawn, on a row's link on the `::after` that covers the row, which it
expects to be `ring`, or the ring given, such as one in `Highlight` under forced colours. `expectFocusAt(page, stop)`
reads the stop that a script focused after a key, such as the list conflict, in the same way. A goal row's button
reads as its label, then its goal's hidden text after a space and a comma, "Move up , Makes own breakfast", as its
accessible name does too: the hidden text is a block of its own to both, and screen readers do not voice the space.

Every test in `e2e/participants.spec.ts` also watches the live regions, through `watchLiveRegions(page, path)` from
`e2e/liveRegions.ts`: an init script wraps `appendChild` and `insertBefore`, before the app's own script runs, and
reports, through `page.exposeFunction`, each element whose role is `status` or `alert`, or each in `<main>` with
`aria-live`, such as a character count's polite twin, that a screen at an address starting `path` puts into the
document with words already in it, which a screen reader may not announce. After the test, any region so reported
fails it, as does any whose `display` is `none`, which `hiddenLiveRegions(page)` lists. A test marked `test.fail()`, as
the one in `e2e/fixtures.spec.ts` is, puts a status region with words into the list's `<main>`, which shows that the
watcher sees it and fails a test.

### Answering a participant's goals

A participant's page reads their goals with them, with
`GET /api/admin/participants/{participantId}/goals?includeArchived=true`, and shows neither until both have loaded, so
a test that opens a participant's page with a made-up manager answers that request too, and each goal write after it.
`e2e/goals.ts`, beside `e2e/participants.ts`, has made-up goals (A35), in the JSON that `GoalListEndpointTests` asserts:
participants.md's three, Makes own breakfast, Catches the bus to day program and phones when she arrives, and Phones
her sister, active, and list-editor.md's two archived ones, Uses the washing machine and Walks to the shop, together in
`goals`, in the API's order. `madeUpGoal` makes others, such as one with a long word. Add participant needs none: after
its `201` the page puts the new participant's empty list in place itself, and sends no request for it.

`serveGoals(page, goals)`, beside `serveParticipants`, answers the goals read and each goal write in the browser,
through `page.route`, from a copy of `goals` that it keeps in memory as the API keeps them, for every participant, so
that no goal request reaches the app: the list in its order; an add at the end, `201`; a reword, an archive and a
restore, each with the goal's version as `If-Match`, `428` without one and `412` with an old one, the reword then `200`
with the goal's `ETag`, and the others `204`, a restored goal going to the end; and a reorder `204` when it names the
active goals, each once, and `422 config.order_mismatch` otherwise. A goal whose words or place change takes a new
version, as its `RowVersion` would, so that the page meets what the API would say to its next write; a change that
leaves it as it was saves nothing, as the API's does (D84). The list itself is a `HeldRows` from `e2e/rows.ts`, which
`serveCommonItems` keeps for each group's items, and for the groups other than Every note, too. Call it before
`page.goto`, with `[]` for no goals; a later call overrides it, as Playwright tries the latest route first.

It gives the list's `changeElsewhere(id, change)`, which rewords a goal, `{ text }`, or archives it,
`{ archived: true }`, with a new version, as another manager would: a Save then meets the edit conflict, and a move or
an Archive built from the list as the page last read it meets the list conflict. `goalWrites` names a move's and an
Archive's addresses, for a route added after `serveGoals`'s, which Playwright tries first:
`holdNext(page, goalWrites.reorder)` holds the next move, for its busy label, until the test lets it go on to
`serveGoals` with `route.fallback()`, and a route that aborts the next archive, with `{ times: 1 }`, gives a write with
no connection.

```ts
await serveMe(page, manager);
await serveParticipants(page, participants);
const store = await serveGoals(page, goals);
await page.goto(`/manage/participants/${jane.id}`);
```

The goal tests check every state with axe; at 375 px, each row's two pairs of buttons on a line each, and each row
button as wide as its label, as when sized to what it holds (`expectLabelWidth`, which sizes it so for a moment through
its `style`, which the policy allows); at 1280 px the buttons on the text's line, and under it at 200% text; nothing
scrolling sideways at 320 px with 200% text; and, under forced colours, the focus ring in `Highlight` and the
unavailable buttons in `GrayText`. `e2e/layout.ts` has the helpers for those: `boxOf`, `expectAtLeast`,
`expectLabelWidth`, `setTextTo200Percent`, `scrollsSideways`, and `systemColour(page, keyword)`, the colour that forced
colours give a system colour such as `Highlight`; and `contrastOf(locator, colour)`, the WCAG ratio of an element's
text, border or focus ring against the background next to it, for the 7:1 that a screen's checklist asks of its text
beyond axe's AA rules, and the 3:1 of borders and rings.

### Answering `/api/admin/common-item-groups`

Common items reads every group, with its items, active and archived, with
`GET /api/admin/common-item-groups?includeArchived=true`, its one read, and shows no group until it has loaded.
`e2e/commonItems.ts` has made-up groups and items (A35), in the JSON that `CommonItemGroupListEndpointTests` asserts and
as `CommonItemsPage.test.tsx` has them: `everyNote`, the built-in group as `admin bootstrap` leaves it, with no items;
and `threeGroups`, common-items.md's sketch, Every note with Medication prompted and Meal prepared, and Laundry done and
Bed made archived, Community outing with Travelled by bus or train, Paid for a ticket at the counter and Bought lunch,
Personal care with none, and Overnight respite, archived, which the page lists in Archived groups alone, in the API's
order. `madeUpItem` and `madeUpGroup` make others, such as an item of a long word or a group of a long name.

`serveCommonItems(page, groups)`, beside `serveGoals`, answers the read and each group and item write in the browser,
through `page.route`, from a copy of `groups` that it keeps in memory as the API keeps them, with a `HeldRows` for the
groups other than Every note, each held as a row whose text is the group's name, and one for each group's items, so
that no request reaches the app. For the groups: the list, Every note first; a group add at the end, `201` with the new
group's ID and no `Location`; a group's rename, archive and restore, each with its version as `If-Match`, `428` without
one, `412` with an old one and `422 config.every_note_fixed` for Every note, the rename then `200` with no body and no
`ETag`, and the others `204`, a restored group going to the end; and a reorder of the active groups other than Every
note `204`, or `422 config.order_mismatch`. For the items: an add at the end of the group, `201` with no body, or
`409 config.group_archived` for an archived group; an item's reword or move, archive and restore, each with its version
as `If-Match`, `428` and `412` as for a group, the reword or move then `200` with no body and no `ETag`, or `409` while
its group, or the group it is moved to, is archived, a moved item going to the end of its new group, and the others
`204`, a restored item going to the end of its group; and a reorder of a group's active items `204`, or
`422 config.order_mismatch`. A group or an item that it does not hold is `404`. It gives `changeElsewhere(id, change)`,
as `serveGoals` does, for an item, or for a group other than Every note, whose `{ text }` renames it.
`failCommonItems(page)` aborts each read, as `failParticipants` does; `listRead` is the read's address, for `holdNext`,
so that a test holds it for the loading line; `itemWrites` names an item move's and an item Archive's addresses, as
`goalWrites` does, and `groupWrites` a group Archive's. `e2e/shell.spec.ts`'s walk, whose Common items link opens the
screen, answers the read with `[everyNote]`.

```ts
await serveMe(page, manager);
const store = await serveCommonItems(page, threeGroups);
await page.goto('/manage/common-items');
```

The tests that meet the app's own answers, in `e2e/common-items.spec.ts`, sign in for real, through the `invite`
fixture and setup with a passkey on the `authenticator` fixture, so that the organisation that `admin bootstrap` made
has its Every note group, empty, as the bootstrap creates it with the organisation: one adds five items at 375 px, with
focus in New item in Every note after each, then rewords, moves, archives and restores one; one adds the groups
Community outing and Personal care at 375 px, with focus in each new group's New item field, renames one, adds items to
it, moves an item into it through Edit's Group radios, moves a group down, and archives and restores the renamed one;
and two more add three items with the keyboard alone, at 375 px and at a laptop's width, and reword, move, archive and
restore one. The integration tests' seeded organisations have their Every note group too (*The seeded organisations and
the tenant*, below). The other tests answer the list in the browser: two go through the group parts with the keyboard
alone, at both widths, adding a group, renaming it, moving it, moving an item into it with the arrow keys in its Group
radios, archiving and restoring it, and meeting the list conflict; and the others check each state with axe; the item
rows' and the group heading rows' layout at 375 px and at 1280 px, and at 200% text with no button over another or cut
off, with the targets' sizes and the space between them; nothing scrolling sideways at 320 px with 200% text; the text
at 7:1 and the borders, radio circles and ring at 3:1 (`contrastOf`); and, under forced colours, every stop's ring in
`Highlight`, the radios and the list conflict included, the unavailable buttons dashed in `GrayText`, the summary
markers, the radio dots and the row dividers. Every test watches the live regions, as `e2e/participants.spec.ts` does.
`expectNothingKept(page, ...values)` from `e2e/kept.ts` then checks that no group's name or item's words are kept where
the page could keep them: on a page that `page.goto` loaded, the history entry holds React Router's index alone.

### Answering Today and a participant's note

Today reads its list with `GET /api/today` and the caller's unfinished drafts with `GET /api/me/drafts`, and a
participant's note reads their header with `GET /api/participants/{participantId}`, the note with `GET {base}`, which
says which screen to show, and the form with `GET {base}/draft`, whose autosave is `PUT {base}/draft`, whose Submit is
`POST {base}/versions` and whose Discard draft, a manager's on someone else's draft too, is `POST {base}/discard`;
Past notes reads the header and `GET /api/participants/{participantId}/notes?before=` (design.md §6.3). Each needs a
session, so a test of Today, the note form, someone else's draft, the read view or Past notes with a made-up person
answers them in the browser, as it answers `/api/admin/participants`.

`e2e/today.ts`, beside `e2e/participants.ts`, has three helpers, each routing one of Today's reads through `page.route`
and overriding `serveMe`'s empty answer, as Playwright tries the latest route first, so call them after `serveMe`:

- `serveToday(page, date, participants, noteOf)` answers each `GET /api/today` with
  `todayList(date, participants, noteOf)`: the date, and the active ones of the made-up participants, such as
  `e2e/participants.ts`'s, as their IDs and names, in the order given, each with the note that `noteOf` gives for their
  ID, by default none, worked out afresh for each request. A store's `todayNotes(me)` (below) gives each participant's
  note today as `me` sees it, so that Today shows what the store holds as it asks; a test of the status lines alone
  gives notes of its own, in `TodayNote`'s shape.
- `serveMyDrafts(page, draftsOf)` answers each `GET /api/me/drafts` with what `draftsOf` gives then, each entry a
  `MyDraft`, `{participantId, givenName, familyName, noteDate, kind, savedAtUtc}`. A store's `myDrafts(me)` (below)
  gives `me`'s own drafts as it holds them, and their pending edits of submitted notes, a manager's of a past day's
  note included, as `kind: 'pendingEdit'` rows; a test of Today's rows alone gives rows of its own, beside the store's.
- `failToday(page, failure)` aborts each `GET /api/today`, for `'no connection'`, or answers it with a status, such as
  `503`. The failure shows about 1 s after the request, after the query client's silent retry.

`e2e/notes.ts` keeps participants' notes in memory as the API keeps them:
`notesStore({ today, participants, goals, commonItemGroups, prompts })` takes the server's date in Melbourne, the
made-up participants, each one's active goals by participant ID, such as `e2e/goals.ts`'s, the organisation's active
common item groups as `NoteGroup`s, `{id, name, isEveryNote?, items}`, Every note first, none by default, and its guide
prompts. Its `serve(page, me)` answers the page's requests for a header, a history and a note as `me`'s, through
`page.route`:

- the header, or `404` for a participant that it does not hold;
- the history, up to 30 live notes dated before `before`, by default tomorrow, newest first, with `hasMore`, each as
  `{noteDate, status, authorDisplayName, isFlagged}` and, on a manager's rows alone, `flagStatus`; or `404`, as for
  the header, and the framework's `400` for a `before` that is not a date;
- the note, `GET {base}`: a submitted note's current version, `{status: 'Submitted', author: {displayName},
  firstSubmittedAtUtc, current: {narrative, isFlagged, flagReason, goals: [{text, isTicked}], commonItemGroups:
  [{name, isEveryNote, items: [{text, isTicked}]}]}}`, the groups being Every note and the picked ones with items,
  every item with its tick; a draft's header, `{status: 'Draft', authorDisplayName, startedAtUtc}`, with, to a manager
  alone, its author's working copy in the same `current` shape, every goal of its snapshot in order with its tick; or
  else `404`, a discarded draft's day included;
- the form, a template with the goals unticked, `commonItemGroups` unpicked and unticked, no flag and the lists'
  version, which covers the goals and the groups with their items, the caller's own draft with its flag, picks and
  ticks, `409 note.in_progress_by_other` with the author's name and start time for another user's, `404` for a date
  after today and, to a worker, for one before yesterday (design.md A65); and, for a submitted note, `403
  note.not_editable` to a worker who did not write it, else the caller's own pending edit, saved, with its base
  version, or the current version as a template, at its base, with no `listsVersion` and nothing saved;
- each save, which keeps the flag, and the reason only while flagged, as the API does, and updates the caller's own
  draft with `200`, or creates it with `201` after the creating save's checks in design.md §5.5's order,
  `422 note.participant_archived`, `422 note.future_date`, `422 note.past_date_manager_only` for a worker,
  `409 note.lists_changed` for any lists' version but the current one's, and `422 validation.failed` for a goal not
  listed, a missing or wrong `pickedGroupIds` (Every note, a group with no items, or one outside the snapshot) and a
  missing or wrong `tickedCommonItemIds` (an item outside Every note and the picked groups); and
  `409 note.version_conflict` for a `baseVersion` that the note has no working copy at (a draft is 0). A save of a
  submitted note's `baseVersion` is its pending edit's own base, which keeps saving after another editor has saved a
  newer version, or the note's current version, which creates the pending edit, `200`, from that version, or, from one
  with a pending edit, is Start again and moves its base; any other is `409 note.version_conflict`, before the saves
  are numbered, and a worker who did not write the note gets `403 note.not_editable`, at any base. A draft, and a
  pending edit, keeps the `clientId` and `seq` of the save that it
  last applied, and numbers later saves as design.md §5.6 does, before their ticks are checked: from the
  same `clientId`, a `seq` at or below the one kept changes nothing and answers `200` with the time that the draft
  holds, and a higher one is applied; from another `clientId`, `seq` 1 is applied, taking the draft over, and any other
  gets `409 draft.taken_over`, writing nothing. So a creating save sent again after its answer was lost finds the
  draft with its `clientId`, and is taken as a later save;
- each Cancel, `DELETE {base}/draft`: `204` for the caller's own pending edit, which it deletes, and `404` for none,
  a draft's working copy included, which it never deletes, and for a participant that the store does not hold;
- and each Submit and each Save changes, with the checks of T09's and T14's Notes in their order: the framework's `400`
  for an `Idempotency-Key` that is missing or not a GUID; `422 validation.failed` under `baseVersion` for a negative
  number, under `narrative` for a blank
  text, U+0085 alone included, as .NET counts it, or one over 20,000 characters, and under `flagReason` for a reason
  over 200; `404` for no participant or no live note; `409 note.in_progress_by_other` for another user's draft; `403
  note.not_editable` for a submitted note that a worker who did not write it posts to; the retry's `200` with the
  version and the note's flag status as it is now for a key that this note's version has, and `422` under
  `idempotencyKey` for one that another note's has; `409 note.version_conflict` for a `baseVersion` that is not the
  note's current version, with `currentVersion`, `createdBy` and `createdAtUtc` of that version, none for a note with
  none; `422` under `flagReason` for a flagged body with a blank reason; `409 note.version_conflict` again for no
  working copy of the caller's at that base; `422` under `tickedGoalIds` for a goal not in the snapshot, and under
  `pickedGroupIds` or `tickedCommonItemIds` as a save does; and otherwise version `baseVersion + 1`: at 0, version 1,
  its goals from the snapshot with the ticks sent, its shown groups with the picks and ticks sent, the draft gone, and
  the note in To review, with version 1 as its flagged version, when it is flagged (design.md §5.8 step 5); at a later
  base, the note's current version replaced, last changed by the caller at the store's time, with the author kept, the
  flag status and the flagged version left as they are unless the version is flagged and its base version was not, or
  had another reason, which puts the note in To review with this version as its flagged version (design.md §5.8's
  flag rules), and the caller's pending edit gone. Each is answered
  `201 {versionNumber, createdAtUtc}` and, for a manager alone, a `flagStatus` (`ToReview`, `Reviewed` or `None`);
- and each Mark reviewed, `POST {base}/reviews`: `403` for a worker; `422` for a comment over 500 characters or a
  version below 1; `404` for no note; `409 review.not_current` for a note that is not To review or whose current version
  is not the one sent; and otherwise `201 {reviewedAtUtc}`, the note Reviewed and the review, with the comment trimmed,
  first in its `reviews`;
- and each Discard, with the checks of T12's Notes in their order: `404` for no participant or a day with no note;
  `409 note.not_a_draft` for a submitted note or a day whose draft is discarded already; a `403` problem with no `code`
  for a worker who did not start the draft; and otherwise `204`, the draft, its content kept, gone from every answer
  above, the history, `todayNotes` and `myDrafts` included, so that the day's next first save creates a new draft.

Each save and Submit answers with `savedAtUtc`, by default 06:12 UTC on 1 October 2026, which the page shows as "Saved
4:12 pm", and which a Submit makes the note's first submitted time; `serve(page, me, { latencyMs })` holds each answer
that long, as on a slow network. The store's `saves` lists the body of every save sent, `submits` the key and body
of every Submit and Save changes, and `cancels` every Cancel; `draft(participantId, noteDate)` gives a working copy,
`pendingEdit(participantId, noteDate, me)` `me`'s pending edit of a submitted note,
`submitted(participantId, noteDate)` a submitted note, `discarded(participantId, noteDate)` the drafts of that day
discarded, each with who discarded it, `todayNotes(me)` each participant's note today, for `serveToday`, and
`myDrafts(me)` `me`'s own drafts, whatever their dates and whether or not their participants are archived since, for
`serveMyDrafts`.
`addSubmitted(participantId, noteDate, note)` puts a submitted note in it, its author someone else unless given their
user ID, flagged when given a reason, and then in To review, as Submit leaves it, unless its `flagStatus` is set by
hand; with `firstFlagReason`, it is in To review on a flagged version 1 with that reason, under a
version 2 that Save changes saved, flagged with `flagReason` or not flagged without one, as it is, at version 2, with
a `lastEdit`; its goals may carry IDs, for its pending edits' forms;
`daysAfter(date, days)` gives the dates of a run of them. Eight more do what someone else does while a page is open:
`setToday(date)`, midnight in Melbourne; `setGoals(participantId, goals)`, a manager's change to the list;
`archive(participantId)`; `startElsewhere(participantId, noteDate, startedBy)`, another user's draft, such as
`alexStarted`, Alex P.'s at 9:14 am, or, given the author's user ID too, one started on another page of theirs, such as
the made-up worker's own draft from the day before; `submitElsewhere(participantId, noteDate)`, the draft submitted by
its author in another tab; `startEdit(participantId, noteDate, me, elsewhere)`, `me`'s pending edit of a submitted
note, from its current version, with what another page of theirs holds; `editElsewhere(participantId, noteDate, by,
elsewhere)`, a version saved over the current one by another editor named `by`, so that a pending edit at the older
version meets the conflict; and `loseNextSubmitAnswer()`, which keeps the version that the next Submit or Save changes
makes but aborts its answer, as a phone that loses its signal while the answer is on its way. One more,
`saveElsewhere(participantId, noteDate, elsewhere)`, saves the draft as its author does from another tab or device, with
that page's text, ticks, picks, flag and reason, the rest kept, and its time, by default the store's: a page's first
save under a `clientId` of its own, so that the next save of the page that saved before meets the takeover banner.
`failNoteReads(page)` aborts each read of a header, a history, a note or a form, as `failParticipants` does, until the
store serves the page again, and `failNoteReads(page, { olderOnly: true })` only each page of a history after the first,
which Show older asks for; `failDraftSaves(page)` aborts each save, `PUT {base}/draft`, until the store serves the page
again, so that autosave retries it and shows "Not saved: no connection…" about 1 s after the first failure;
`failEditRequests(page)` aborts each Save changes, `POST {base}/versions`, and each Cancel, `DELETE {base}/draft`,
in the same way, for the failure lines above the edit form's buttons. For a test
that watches the saves, of the store's page or of the app's own, `isDraftSaveRequest(request)` tells a save from the
page's other requests, and `draftRequests` is the glob of a working copy's address, its read's and its save's, for
`holdNext`.

```ts
await serveMe(page, worker);
const store = notesStore({ today: worker.today, participants, goals: { [jane.id]: [breakfast, bus] } });
await serveToday(page, worker.today, participants, store.todayNotes(worker));
await serveMyDrafts(page, store.myDrafts(worker));
await store.serve(page, worker);
await page.goto('/');
```

A test of a page that waits on one of those requests holds it with `holdNext`, which also takes a test of the URL, for
a request that a glob cannot tell apart by its query: `e2e/past-notes.spec.ts` holds Show older's next page, with
`before`, and not the first page's request, which has none.

The eleven tests that meet the app's own answers sign in for real through the `invite` fixture, each adding Jane Citizen
with two goals from Today's empty state, as a manager does, with `addJaneWithTwoGoals` from `e2e/addParticipant.ts`: a
manager finds her, starts her note and reopens the draft, then saves new guide prompts, which the emptied draft shows,
and then `""`, which leaves its box with no placeholder; a worker whom the manager invites, set up on `secondDevice`
from the link that `resetSignIn` gives, meets the in-progress sentence and Past notes at that note, the note's header
alone and no Discard draft, while the other organisation's manager, `secondInvite`, meets Page not found there; such a
worker, at a phone's width, starts a note, meets More actions once its first save is answered, goes back from the
Discard dialog to More actions, then discards it, landing on Today's `<h1>` with the row showing no status and Past
notes listing nothing, and starts the day's note again, whose first save creates it (design.md §14 M2), after which the
manager reads it, its header and working copy, as the read-only draft, with its ticks, text and flag row, discards it
and lands on Today, where neither sees a status on the row; and such a worker, at a phone's width, fills in a note,
ticks a goal, flags it with a reason and submits it through the confirmation, then finds it Submitted and Flagged on
Today, in the read view and in Past notes, as the manager does too, while the other organisation's manager meets Page
not found at the note and at its Past notes (design.md §14 M2 *Done when*). Seven more prove that autosave never loses
words (T10), at a phone's width: offline, through `context.setOffline`, "Not saved: no connection…" within about 1 s and
Submit unavailable, then "Saved" with no action once online, and the draft holding what was typed; a first save let
through to the app with `route.fetch()` and its answer then aborted, so that the retry, the page's next `seq`, saves
once with no banner; the same draft in `secondTab`, whose save takes it over, so that the first tab's next save meets
the takeover banner, Keep wins and Load then replaces the other tab's screen; `secondTab` killed in the middle of a
sentence typed at a known pace, the text kept but for its last few seconds; the form left and opened again in the same
page load, its next save applied; the back link and Past notes sending a change waiting, then leaving, and staying
offline; and a session ended in place mid-note, then the same person signing in again, one save going under the same
`clientId` and the next `seq`, and another person, set up first with a password, landing on Today with nothing sent.
`e2e/today.spec.ts`, `e2e/note-form.spec.ts` and `e2e/past-notes.spec.ts` are the examples. They watch the live regions
as `e2e/participants.spec.ts` does, but for the save indicator, which a reopened draft's form puts in anew with its
first words (autosave-status.md *Behaviour* 8). A test that opens a confirmation again by a key just after closing it
first waits for the closed one's `<dialog>`, `dialog[role="alertdialog"]`, to leave the page: the page takes it away
only once its close event has run, and a key pressed before then would meet the dialog going rather than open a new one.
Their checks of 200% text, sideways scroll and target sizes use `setTextTo200Percent`, `scrollsSideways` and
`expectAtLeast` from `e2e/layout.ts`, where `shownText` gives the words that show, without the visually hidden ones, and
the forced-colours ring, the tags' borders and the Discard dialog's warning button's border, in `ButtonText`, use
`systemColour` from there too. A row's description is checked with `toHaveAccessibleDescription`, and a To review row's
name with `toHaveAccessibleName`, whose reckoning in Chromium puts a space before each comma that a hidden `Pause` reads
for a dot or a new line, so their patterns allow one. Today's Waiting state holds the page's timers of 1 s with an init
script rather than on Playwright's clock, as axe settles each rule on a timer and never finishes under a paused clock;
the after-midnight refusal runs on `page.clock`. A check of what a screen does as a person comes back to the tab fires
the window's `focus` and a `visibilitychange` that bubbles, as a browser's own does, since TanStack Query listens for it
on the window, not the document, and shows on a screen that opts in to `refetchOnWindowFocus` that the same events reach
it (`comeBack` in `e2e/past-notes.spec.ts`). A headless page is never hidden, so the note form's page-hidden save is
checked by giving `document` a `visibilityState` of `'hidden'` and dispatching `visibilitychange` on it (`setHidden` in
`e2e/note-form.spec.ts`), on a paused `page.clock`, so that no timer of autosave's sends the save meanwhile; a wrapper
round the page's own `fetch` then shows that the save went with `keepalive`. A tab killed as a phone kills one in the
background is crashed with `goto('chrome://crash')`, as Playwright's own tests crash a Chromium page: the renderer stops
at once, unloading nothing, so that the page-hidden save that `page.close()` lets the page send does not go, and what
the note keeps comes from the saves made while typing alone. CDP's `Page.crash` is no stand-in: in a run on Windows the
page ran on for seconds after it, long enough to save once more.

### Answering `/api/reviews?status=toReview`

The Flagged screen reads its To review list with `GET /api/reviews?status=toReview`, which needs a manager's session,
so a test of it answers it in the browser, from the notes that a `notesStore` holds, as it answers Today. The store's
`toReview()` gives the list as design.md §6.4 has it, as the store stands when it is asked: each submitted note in To
review, an archived participant's too, as `{participantId, givenName, familyName, noteDate, authorDisplayName,
flagReason, flaggedAtUtc, currentVersionNumber, flagRemovedLater}`, the reason and time those of its flagged version,
and `flagRemovedLater` true when its current version is not flagged; the oldest flag first, then by participant and
date in place of the note's ID. `e2e/reviews.ts`, beside `e2e/notes.ts`, has two helpers, each routing the page's
requests through `page.route` and overriding the answer before it, so call them before `page.goto`:

- `serveReviews(page, store)` answers each with `store.toReview()`, worked out afresh for each request, so that a note
  flagged and submitted through the store's own Submit shows on the list's next request.
- `failReviews(page, failure)` aborts each, for `'no connection'`, or answers it with a status, such as `403`, which
  shows Page not found, as for a manager whose role has changed. The failure of a first load shows about 1 s after the
  request, after the query client's silent retry.

A test whose made-up manager only passes through Flagged, as the shell's walks and the count of `/api/auth/me`
requests do (`e2e/shell.spec.ts`, `e2e/start-up.spec.ts`), serves an empty store's list, as the app, which holds no
session for them, would answer `401`, ending the session in place.

```ts
await serveMe(page, manager);
await serveReviews(page, store);
await store.serve(page, manager);
await page.goto('/flagged');
```

The test that meets the app's own answers signs in for real through the `invite` fixture at a phone's width: the
manager adds a participant and invites a worker and a second manager, set up on `secondDevice` and `thirdDevice` from
the links that `resetSignIn` gives; the second manager opens Flagged and finds no flags; the worker flags and submits
the participant's note; the first manager finds it on Flagged, and the second on their next page load, over the empty
list that their page kept, which shows until the list asked for again answers (design.md §14 M3 *Done when*, the
list half); the first opens it and comes back by its back link, "Flagged"; and the worker meets Page not found at
`/flagged`. `e2e/flagged.spec.ts` is the example. A click on a row's line, such as its Reason, goes through
`page.mouse` at the line's place, as the link's `::after` covers the row and Playwright's own click on the line would
wait for the line itself to take it.

### Signing in with a passkey, end to end

A sign-in test names the `authenticator` fixture, so that Chromium answers the page's passkey prompts and shows no
dialog, as passkey-flows.md's *Tests* say.

- `addPasskey(rpId)` adds a passkey through `WebAuthn.addCredential`: a discoverable credential for the domain
  `rpId`, with a P-256 key, a credential ID and a user handle that the test makes, given to the protocol as base64.
  The sign-in options name no credentials, so the next prompt for that domain signs with it. Pass the host of the
  address that the test opened, `new URL(page.url()).hostname` once the page has loaded, as the options name
  `App:Origin`'s host and `App__Origin` names that address (above).
- No account holds a passkey added this way, so a sign-in with it fails, with the `401` that the API gives an unknown
  credential. One that succeeds needs a passkey that the app's setup made with the same authenticator, as in
  `e2e/setup.spec.ts`'s passkey path, which signs in again once its session is gone.

An assertion that something is absent passes at once, before the page has handled the prompt's answer, so a test
first waits for what the page does with it: its request to `POST /api/auth/passkey` (`page.waitForResponse`), or, for
a failure that sends nothing, the fresh options that the sign-in view asks for after any failure.
`e2e/sign-in.spec.ts` is the example.

### Signing in with a password, end to end

A password sign-in that succeeds needs an account set up on the password path, whose key gives the code:
`e2e/setup.spec.ts`'s password path makes one, and then, with its session gone, signs in a second time with the email,
the password and a code from the key that view G showed (`authenticatorCode`, above), ending on the page it opened.

A test of the code step's layout and refusals, sign-in's view B, needs no such account: it answers the password step
itself. Before `page.goto`, it routes the page's `POST /api/auth/login`, through `page.route`, to the
`200 {"next":"totp"}` that the app gives a right password, so that Continue, with any email and password, shows view B
in A's place. The pattern matches that path only, so the code then goes to the app's own
`POST /api/auth/login/totp`, which answers `401`, as no code step is under way there: Identity's two-factor cookie
holds at most the passkey ceremony of the options that view A asked for.

```ts
await page.route('**/api/auth/login', (route) => route.fulfill({ json: { next: 'totp' } }));
await page.goto('/');
```

Without that route, Continue meets the app's own `POST /api/auth/login`, which answers an address that no account
holds, such as a made-up `example.org` one (A35), with the `401` of any failed sign-in. A test that shows that a step
starts no passkey prompt counts the page's calls of `navigator.credentials.get`, through an init script that wraps it
before the app's own script runs (`countPasskeyPrompts`), and, so that the count is known to work, ends with a press of
the passkey button, which the `authenticator` fixture ends at once. `e2e/sign-in.spec.ts` is the example.

### Running 28 idle minutes on `page.clock`, over a real session

The warning before the idle sign-out opens after 28 minutes with no request that the app accepted, and its Stay signed
in sends `POST /api/auth/ping`, which needs a session that the app still holds. So a test of it signs in for real,
through setup with a passkey on the `authenticator` fixture, as `e2e/setup.spec.ts`'s passkey path does, and runs the
28 minutes on Playwright's clock. Only the page's timers and `Date` run on that clock: the app's own runs in real time,
so its 30 idle minutes have barely begun when the page warns. `e2e/session-warning.spec.ts` is the example.

```ts
await page.clock.install();
await page.goto(invite.setupLink);
// Set up with a passkey; once Today shows:
await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Today', exact: true }).click();
await page.clock.runFor('28:00'); // the warning opens
```

- Install the clock before `page.goto`, so that the app's script starts on its timers. It then runs as the real clock,
  as setup's requests and passkey prompt need.
- Once Today shows, pause it, a second ahead of the page's `Date.now()`, so that the page's time moves only as the test
  runs it on with `page.clock.runFor`, and each mark falls exactly where the test puts it. While it is paused, none of
  the page's timers fires until `runFor`, `api()`'s 10-second timeout and the 1 s before "Connecting…" included.
- The page's idle time counts from the send time of the newest request that the app accepted, which on the paused
  clock is where the clock stands. So the test sends one at a known time, through the nav's Today link, once setup's
  own refresh of `/api/auth/me` has its answer, and runs the clock on from there: `runFor('27:59')` leaves the warning
  closed, and one more second opens it. Each ping that the app answers starts the idle time again.
- axe runs its own timers on the page's clock, so resume it (`page.clock.resume()`) before `expectAxeToPass`; from then
  on, the marks fall where the real time since puts them.
- The page's first change after a sign-in fetches an antiforgery token for the person now signed in, itself a request
  that the app accepts, which closes the warning however its ping went. A test of a ping that fails, aborted with
  `page.route`, lets a first ping through, so that the page then holds a token.
- From the warning's one button, Tab and Shift+Tab take focus out of the page, to the browser, and the next brings it
  back. As it comes back, the window's `focus` refreshes `/api/auth/me`, which closes the warning as any accepted
  request does, so a test of the keys holds that request with `holdNext` (above) until it has pressed them.
- At 30:00 the signed-out-in-place view shows, and the app, whose session has not ended, answers the window's next
  refresh of `/api/auth/me` with `200`, which brings the screen back, as when another tab kept the session alive. The
  view sends nothing at 30:00: it asks for passkey options once the session has surely ended by the page's clock, 30:05
  after the last answer came (design.md A61), which, on the paused clock, is 30:05 after the request that it answered.
- The note form's test signs in on the clock, adds a participant and opens their note while the clock still runs as
  the real one, and only then pauses it. Its accepted request is the note's first save, which goes as the test runs
  the clock on by the 2 s after the last change; a change made at 27:59 then goes at 28:00 in place of the warning.

### Ending a session in place, and signing in again

A test of signing in again in place (S00.04.08) ends a real session, which it signed in through setup, by removing the
session cookie, `__Host-grow2notes`, with `page.context().clearCookies({ name })`, as the app's own 30 idle minutes
would. The window's `focus`, dispatched in the page, then refreshes `/api/auth/me`, whose `401` shows the
signed-out-in-place view at the same address; a change sent then, such as Send invite, meets the `401` too. The passkey
that setup made on the `authenticator` fixture signs the same person in again. To sign in as another person, a test
sets that person up first, with `secondInvite` on the password path, so that the virtual authenticator holds one
passkey only, and clears the browser's cookies before the first person's setup. `endTheSession(page)` and
`signInAgainWithPasskey(page)` from `e2e/signedOut.ts` end the session and sign the same person in again, and
`setUpWithPassword(page, person)` from `e2e/setupSignIn.ts` sets the other person up, giving the password and the
setup key; `e2e/sign-in-again.spec.ts` and the note form's session test use them.

```ts
await page.context().clearCookies({ name: '__Host-grow2notes' });
await page.evaluate(() => window.dispatchEvent(new Event('focus')));
```

A test that answers `/api/auth/me` in the browser shows the view with a change that it answers `401`, such as Resend
invite with `answerSetupLinks(page, { status: 401 })`, after `failMe(page, 401)`, so that the window's focus, which Tab
out of the page and back gives, finds no session and leaves the view up. It calls `serveMe` again before it signs in
again, as the new session's refreshes are then answered. `e2e/sign-in-again.spec.ts` is the example of each.

### Walking the Tab stops with `tabTo`

`tabTo(page, stop, key, expected)` from `e2e/focus.ts` moves focus to a stop by pressing the key, which is Tab unless
the test gives `'Shift+Tab'`, at least once and then until `stop` is the page's `document.activeElement`. `stop` is a
Locator, so Playwright's strictness fails one that finds more than one element, or `'outside the page'`, where Tab takes
focus past the page's last stop and Shift+Tab before its first. Every stop it passes, and `stop` itself, must show
`expected`'s focus ring, `ring` unless the test gives another, such as one in `Highlight` under forced colours, but for
a date field's calendar button, which the browser rings in its own style. It returns the names of the stops it passed,
without `stop`. It never wraps. Focus leaving the page before a Locator stop, or no stop after 200 presses, which is
more than any screen has, fails the test, which is how a trap, or a stop that the key never reaches, fails.

Use `expectStops` where a screen file names a Tab order and the test should fail on any other. Use `tabTo` where a test
crosses many stops and only the stop it is going to matters, as the key flows do.

```ts
await tabTo(page, page.getByRole('link', { name: 'Participants', exact: true }));
await page.keyboard.press('Enter');
```

### `foundations.ts`: what a browser can prove of foundations.md's manual test list

`e2e/foundations.ts` holds the checks of foundations.md's manual test list that need no person, so that each screen's
own spec calls one function in its fullest state rather than carrying a copy.

- `expectFoundationsToPass(page, { topBar })` runs, in this order: `<html lang>` is `en-AU`; `expectTextAt7To1`;
  `expectNothingToMove`; nothing pinned to the window, at 1280x800 and at 640x400 (see below); a focus ring on every Tab
  stop and no trap, forward and then backward with `tabTo`, in the page's colours and under forced colours, where the
  ring is in `Highlight`; and `expectToFit` at 320x800, 320x200 and 640x400, each with 100% text, 200% text, and 200%
  text with WCAG 1.4.12's spacing. Pass `topBar: true` on the note form and its forms, whose top bar, the first child
  of `#page-bar`, is the one element allowed to stay put, and only on a window taller than 30rem.
- `expectTextAt7To1(page, state)` runs axe's `color-contrast-enhanced` rule and names each target below 7:1. That rule
  holds large text to 4.5:1, and cannot reach a placeholder or the pressed secondary label, which tokens.test.ts holds
  to 4.5:1. An element painted with one of those two pairs is left out by selector, with a comment, and a token is never
  changed to pass.
- `expectNothingToMove(page)` expects no element, nor its `::before` or `::after`, to have an animation, a transition
  lasting longer than 0, or smooth scrolling, with reduced motion off, as base.css's reduce rule would hide a
  transition.
- `spaceTextOut(page)` sets WCAG 1.4.12's text spacing on every element, through a constructed stylesheet, which the
  Content Security Policy allows.
- `expectToFit(page, state)` expects no sideways scroll; no element, and no word of an element's own text, past the
  window's sides or its element's; no element cutting off what it holds, but for a box that scrolls up and down, such as
  the dialog; and a field showing all its text. Words hidden on purpose by a clip are left out. A filled single-line
  field is a field that cuts off what it holds when its text is longer than the field is wide at 320 px and 200% text,
  so a test that calls it at that size empties such a field first, as Edit user's does; it is not excluded, since a
  worker or manager who cannot see their own address in full has met the failure it looks for.

The zoom sizes are real geometry. 1280x800 at 400% zoom is 320x200 CSS pixels, and at 200% it is 640x400, both under
the `(max-height: 30rem)` layout, which is what a zoomed laptop and a landscape phone get; 320x800 is the narrowest
phone upright, not zoom.

Call `expectFoundationsToPass` last in a test, in a state where nothing has yet changed the text size or its spacing:
neither can be undone on a page, and it leaves the page at 640x400 with 200% spaced text.

### The 320 px check in `expectAxeToPass`

`expectAxeToPass(page)` from `e2e/axe.ts` first sets the window to 320x800 and expects the page not to scroll
sideways (`scrollsSideways`, WCAG 1.4.10), with the message `sideways scroll at 320 px wide (WCAG 1.4.10)`, then
puts the window and the scroll back as the test left them, runs axe at 375 px and at 1280 px, and leaves the page at
1280x800. It puts them back because, without that, two states of the note form (a group picked, and the dead end after
midnight) failed axe's `target-size` rule at 375 px or 1280 px, the reflow having moved what the rule saw. So
every state that a test checks with axe also reflows at the narrowest phone's width, and a test that adds a state with
`expectAxeToPass` gets the check without asking for it. The check measures the page's own width only;
`expectToFit` in `expectFoundationsToPass` also looks inside the page, and fits the real zoom sizes. A test that
asserts nothing but 320 px sideways scroll is no longer written.

### `key-flows.spec.ts`: the eight flows of design.md section 4.13, by keyboard alone

`e2e/key-flows.spec.ts` is the one spec that signs in for real and answers nothing in the browser. It holds two tests,
each with one `test.step` for each flow, named as design.md section 4.13 names it, so that the report has a result for
each flow:

- `a manager, keyboard only`: Forgotten note, Daily report and Access request.
- `a worker and a manager, keyboard only`: Wrong participant, Write today's note, Correct a note, Flag and Lost phone.

Each runs at 1280x800 with `test.setTimeout(6 * 60_000)`. The made-up people, goals and common items that a flow needs
are made with the clicks the other specs use (`signInThroughSetup`, `addJaneWithTwoGoals`, `addParticipantWithGoals`,
`inviteThroughThePage`); inside a flow step only the keyboard is used, but for a native date field, which is reached by
Tab and given its date with `fill('YYYY-MM-DD')`. After each change of screen the test asserts where focus landed,
with `expectFocusAt` or `tabTo`'s ring, before it runs axe, and axe runs at each screen, result and dialog, not at
every key. Yesterday comes from the server's today, which the nav's Report link holds, never from the device's clock
(A33). In Lost phone the manager's Reset sign-in is pressed, and `resetSignIn` stands for the emailed link, as in
`user-actions.spec.ts`. A failing flow step names the flow in the CI report.

## Writing integration tests

- Put the test class in `[Collection<SqlServerCollection>]` and take `Grow2NotesFactory` as a class fixture. Every class
  in that collection shares one SQL Server container for the run, and xUnit runs their tests one at a time.
- `SqlServerFixture` applies the app's migrations to the container's `Grow2Notes` database before the first test, as a
  deploy runs them before the new code starts; the app never migrates itself (design.md §10.6). It then seeds the two
  organisations described below. Every test in the collection shares that database, so a test finds its rows by their
  keys and never assumes a table is empty. A test that must leave nothing behind runs in a transaction that it rolls
  back; one that must know every row in a table takes a migrated database of its own, with nothing seeded, from
  `SqlServerFixture.CreateDatabaseAsync`. A test-only context gets a database of its own from
  `SqlServerFixture.ConnectionStringFor` and creates it from its model with `EnsureCreated`;
  `Grow2Notes.Tests/Data/TenantIsolationOnNewEntitiesTests.cs` is the example.
- The app under test connects as `sa`, to which the denies of `grow2notes_runtime` do not apply. A test of what the
  database refuses the app runs its statements as a member of that role, as the app's identity is in Azure. A new
  append-only table is added to its `AppendOnly` list and a new never-deleted table to `NeverDeleted`, the two places a
  table is named there: the error-229 theories and the exact list of denies are derived from them, and a guard fails
  while an `IAppendOnly` entity's table is missing from `AppendOnly`. `Grow2Notes.Tests/Data/RuntimeRoleTests.cs` is
  the example.
- A test of a script in `ops/` runs the file itself through `OpsScripts.RunAsync`, which substitutes its `$(Name)`
  variables as `sqlcmd` does, starts the session with `sqlcmd`'s `QUOTED_IDENTIFIER OFF`, and returns every result set.
  It refuses a variable that the script uses and the test did not give, one that the test gave and the script does not
  use, and a value with anything but letters, digits and hyphens. `OpsScripts.OnDatabaseOfItsOwnAsync` gives the test a
  migrated database of its own, with the app on it to add rows through; its name is unique in the collection.
  `Grow2Notes.Tests/Ops/FindIdsScriptTests.cs` is the example.
- A test that stops its database (`SqlServerFixture.StopAsync`) declares a collection of its own with
  `ICollectionFixture<SqlServerFixture>`, so it gets its own container and cannot break the shared one. It starts the
  app (`factory.CreateClient()`) before stopping the database, because a stopped container has no port to build the
  connection string from. `Grow2Notes.Tests/Platform/HealthEndpointsWithDatabaseStoppedTests.cs` is the example.
- The factory runs the app in the `Testing` environment, so `appsettings.Development.json` is not loaded, and its
  connection string overrides any on the machine.
- A test that only reads the app's services or settings, and sends no request that needs the database, can host the
  app without one in `AppWithoutDatabase`, so it runs without Docker. `Grow2Notes.Tests/Platform/LoggingTests.cs` is
  the example, and `Grow2Notes.Tests/Data/ModelTenancyTests.cs` reads the app's model that way. It hosts the app
  without the test-only sign-in (below), as the app is deployed, so `Grow2Notes.Tests/SignedOutTests.cs` uses it to
  show what a signed-out caller gets. In Development it has the app's console email sender, from
  `appsettings.Development.json`, as local development has (below), and otherwise the not-configured sender, which
  fails every send.
- The rate limits (design.md §9.9) are lifted in both hosts, `Grow2NotesFactory` and `AppWithoutDatabase`: every test
  of a suite calls one host, and all its clients have the same null address, so with the design's numbers the suite
  would soon be refused. `TestRateLimits.LiftRateLimits()` replaces the `RateLimitPermits` service, which is the only
  place the app takes its limits from, so no setting changes them in production, with permits that no test reaches. It
  does the same for `AssistantRateLimitPermits`, the numbers of the AI-assistant access (mcp-server.md §5.6).
  A test of a limit builds a host of its own with `factory.WithWebHostBuilder`, puts `RateLimitPermits.Design` back
  with `services.AddSingleton(RateLimitPermits.Design)`, and calls from an address it chooses with
  `services.AddClientAddressHeader()`, which takes the remote address from an `X-Test-Client-Address` header.
  `Grow2Notes.Tests/Platform/RateLimitsTests.cs` is the example. Its counts are the design's numbers written out, never
  read from the permits. A test of an assistant limit starts its flow with
  `AssistantFlow.StartAsync(..., limited: true)`, which puts back both `RateLimitPermits.Design` and
  `AssistantRateLimitPermits.Design`; `Grow2Notes.Tests/Features/Mcp/AssistantRateLimitsTests.cs` is the example.
- A test of what archiving a participant, goal, common item or group, or deactivating a user, leaves of the past
  adds an organisation of its own and changes no seeded row, and runs on a fake clock that stands still, so that the
  report's "As at" line and the export's "Exported by" line are equal before and after and whole texts can be
  compared. `Grow2Notes.Tests/Platform/PastRecordsTests.cs` is the example: one fact for each of a participant, a
  goal with an item and a group, and a worker.
- A test that reads what is written to standard output puts a writer of its own in its place with `Console.SetOut`,
  and the old one back in a `finally`, in `[Collection<StandardOutputCollection>]`, which xUnit runs on its own, after
  every collection that runs in parallel, so nothing else writes to it meanwhile. It hosts its app with
  `logging.ClearProviders()` and a provider of its own, so the console logger writes nothing there either.
  `Grow2Notes.Tests/Platform/Email/ConsoleEmailSenderTests.cs` is the example.
- A test-only endpoint that a test maps is denied by default, like any endpoint of the app's (design.md §2), so mark
  it `AllowAnonymous` unless the test is about who may call it. A signed-out request to one that is not gets the
  session cookie's `401`, as problem details, as in the deployed app: in `AppWithoutDatabase`, where the cookie is the
  default scheme (`Grow2Notes.Tests/SignedOutTests.cs`), and in `Grow2NotesFactory`, whose default scheme sends a
  request without the test-only sign-in's headers to the cookie (below).
- A test that calls `GET /api/auth/antiforgery` or an endpoint in the `/api` group calls `https://localhost`, with a
  client from `CreateHttpsClient()` (`Grow2Notes.Tests/Fixtures/AntiforgeryTokens.cs`): the antiforgery cookie is
  `Secure` always, so antiforgery throws on a request that is not HTTPS, and the app answers `500`. A request that
  changes something sends the token that `FetchTokenAsync()` fetched with the same client, which keeps the cookie, in
  its `X-XSRF-TOKEN` header, as the SPA does (design.md §9.8), and sends any body as `application/json`, as
  `PostAsJsonAsync` and `JsonContent` do: the group answers `415` to a request with any other `Content-Type`, or with
  a body and none. `PostWithTokenAsync` and `PutWithTokenAsync` send a change so, each with a row's ETag, if given,
  as `If-Match`. A token is issued to the user a request is signed in as, so fetch it after signing the client in.
  In `AppWithoutDatabase`, Data Protection, which protects the tokens, has no key ring until `KeepKeysInMemory()` gives
  it one. A test-only endpoint mapped in the group, as `TestOnlyApiEndpoint` maps one, gets the group's filter; one
  mapped outside it, as most tests map theirs, does not.
- A test of `Strict-Transport-Security` calls a host name other than `localhost` over HTTPS, as
  `Grow2Notes.Tests/Platform/SecurityHeadersTests.cs` calls `https://grow2notes.example`: the HSTS middleware never
  sends the header to `localhost`, `127.0.0.1` or `[::1]`, nor over plain HTTP, which `Grow2Notes.Tests/HstsTests.cs`
  shows.
- Every response carries design.md §9.7's security headers, the exception handler's without
  `Strict-Transport-Security` (S06.01.04's Notes). `Grow2Notes.Tests/Platform/SecurityHeadersTests.cs` asks
  `https://grow2notes.example` for each kind of response that the app gives and checks that it carries all six, once
  each, with the values that it reads from §9.7 itself. `Grow2Notes.Tests/Platform/DatabaseFailureHandlerTests.cs`
  checks the exception handler's `500`, `400` and `413` the same way for the other five. A story that adds a kind of
  response, such as a file download or an image, adds it to `SecurityHeadersTests`, as the Definition of Done's
  headers bullet asks.
- A run filtered to unit tests starts no container.

### The seeded organisations and the tenant

After migrating the shared database, `SqlServerFixture` adds two made-up organisations (A35) for the two-organisation
tests (design.md §5.9 item 7), each with an Active manager and an Active worker with an `example.org` address, an
active participant, and its Every note group, as `admin bootstrap` adds it. A test takes `SqlServerFixture` in its
constructor, beside `Grow2NotesFactory`, and finds their IDs in `sqlServer.Seeded`: `Seeded.A` and `Seeded.B` each
hold the `OrganisationId`, `ManagerId`, `WorkerId`, `ParticipantId` and `EveryNoteGroupId`. A two-organisation test
acts in A and checks that nothing of B's reaches it.
`Grow2Notes.Tests/Fixtures/SeededOrganisationsTests.cs` shows what is seeded.

- Other tests rely on the seeded IDs, and on each seeded user's email address, role, Active status, security stamp
  and lockout (Identity's count of failed sign-ins and its lockout end), so no test changes them. One call does: the
  endpoint matrix's `POST /api/auth/logout` gives each seeded caller a new security stamp (design.md A99). No other
  test sees it: the test-only sign-in reads the row on each request and checks no stamp, a cookie sign-in takes the
  stamp of that moment, and no test keeps a seeded user's cookie from one test to the next. An endpoint call
  commits, and may change other columns of a seeded row, such as Identity's `ConcurrencyStamp`. A test may add rows
  to a seeded organisation, users included, so no test assumes the seeded users are its only ones. A test that
  deactivates or resets a user, changes a user's role or email, or fails a sign-in on purpose does it to a user it
  added itself, and one that invites through the API invites an address of its own; or it works in a migrated
  database of its own from `SqlServerFixture.CreateDatabaseAsync`, as a test that runs `admin signout-all` must,
  since it gives every user a new security stamp, the seeded users' too
  (`Grow2Notes.Tests/Platform/OperatorCommands/SignOutAllCommandTests.cs`). Likewise no test archives, restores or
  changes a seeded participant, or starts a note for one, whose form the endpoint matrix opens: it adds one of its own,
  Jane Citizen, with `services.AddParticipantAsync(organisation)` (`Grow2Notes.Tests/Fixtures/TestParticipants.cs`),
  active, or archived with `archived: true`, which gives their ID and their ETag, the `If-Match` of a change to them,
  as `Grow2Notes.Tests/Features/Participants/UpdateParticipantEndpointTests.cs` does. A test that needs a draft of a
  note adds it, as a first save leaves one, with `services.AddDraftAsync(organisation, new TestDraft(…))`
  (`Grow2Notes.Tests/Fixtures/TestNotes.cs`), and one that needs another author or reader than the seeded users adds
  an Active user with `services.AddUserAsync(organisation, role, displayName)`
  (`Grow2Notes.Tests/Fixtures/TestUsers.cs`), as `Grow2Notes.Tests/Features/Notes/NoteDraftEndpointsTests.cs` does.
- No test changes a seeded Every note group either, which the groups list's tests and its matrix row read as
  bootstrap leaves it (*The endpoint matrix*). A test adds groups and items of its own straight through the app's
  context with `TestCommonItems` (`Grow2Notes.Tests/Fixtures/TestCommonItems.cs`), which writes no audit event and
  keeps every value given, so that a test can set up gaps, ties and archived rows that no endpoint would leave:
  `services.AddGroupsAsync(organisationId, groups)` adds groups other than Every note, and
  `services.AddItemsAsync(organisationId, groupId, items)` adds items to a group, each with its `SortOrder`, its
  `CreatedAtUtc` and ID when the test needs them, such as fixed IDs that SQL Server orders the other way round from
  their times, and its archive time, if any, and gives their IDs in the order given; and
  `services.AddItemAsync(organisationId, groupId, item)` adds one and gives its ID and its ETag, the `If-Match` of a
  change to it, as `Grow2Notes.Tests/Features/CommonItems/UpdateCommonItemEndpointTests.cs` does. A reorder of the
  groups sends every active group of the organisation but Every note, and an add through
  `POST /api/admin/common-item-groups` or a restore puts a group after them all, so a test that reorders them, or
  finds where an added or restored group lands, works in an organisation of its own, as
  `Grow2Notes.Tests/Features/CommonItems/AddCommonItemGroupEndpointTests.cs`,
  `Grow2Notes.Tests/Features/CommonItems/ReorderCommonItemGroupsEndpointTests.cs` and
  `Grow2Notes.Tests/Features/CommonItems/ArchiveCommonItemGroupEndpointsTests.cs` do, or, among other tests' groups in
  A, as the reorder's matrix row does, reads their order with `services.ActiveGroupIdsAsync(organisationId)`. An add
  through the endpoint also writes `common_item_group.created`, which `TestCommonItems` never does, so a test that
  adds a group to A that way reads the events of that group alone, by its ID.
- The users are created through `UserManager`, as the app creates accounts, and are as setup leaves them except that
  they have no passkey or password, so a test calls as them with the test-only sign-in.
- A test that adds a participant gives it the organisation's ID itself, under a `Use` block for that organisation
  (below), as `Grow2Notes.Tests/Features/Participants/ParticipantListEndpointTests.cs` does: EF Core gives a
  participant added with no `OrganisationId` a new GUID, as the column is in its alternate key and its foreign key to
  `Organisation`, and the save then refuses it as another organisation's (`Participant.OrganisationId`).

Tenant-owned rows are read and written under a tenant (design.md §5.9). In a request, the app takes it from the
signed-in user's session. A test that uses the app's context outside a request sets it as sign-in and the operator
commands do: it takes `ITenantContext` from the same scope as the context and opens a `Use` block.

```csharp
using var scope = factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

using (tenant.Use(sqlServer.Seeded.A.OrganisationId))
{
    // Queries return only A's rows; a save stamps A's ID on added rows and throws for another organisation's.
}
```

With no `Use` block open, a query or save of a tenant-owned row throws. Open the block in the test method itself,
around its awaits: a tenant set inside an awaited helper method does not flow back to its caller. `Organisation` and
`AspNetUsers` need no tenant: the first is the tenant, and the second is not filtered, so a query of users names the
organisation itself, as `SeededOrganisationsTests` does.

### Calling as a seeded user

`Grow2NotesFactory` keeps a test-only authentication scheme beside the session cookie
(`Grow2Notes.Tests/Fixtures/TestSignIn.cs`); the app itself has no such scheme. The factory's default scheme is a
policy scheme that sends a request with the `X-Test-User` or `X-Test-Role` header to the test-only sign-in, and any
other request to the session cookie, as in the deployed app, so a request with neither header and no session cookie
is signed out. A request that names a user's ID in its `X-Test-User` header is signed in as that user, with the
principal that the app's claims factory builds from the user's row (design.md §8.4), as the session check rebuilds it
on every request: Identity's claims (the user's ID, user name, email and security stamp), `org_id`, which makes the
user's organisation the request's tenant, the role by its `UserRole` name, and the display name.
`Grow2Notes.Tests/Fixtures/TestSignInTests.cs` shows it. The session checks run in the cookie only, so a request
signed in this way skips them: the security stamp, the idle limit and the 12-hour limit do not apply to it, and its
principal has no `auth_time`, which only a sign-in adds; a test of them signs in with the cookie (below). `SignInAs`
adds the header to every request a client sends:

```csharp
using var client = factory.CreateClient().SignInAs(sqlServer.Seeded.A.WorkerId);
```

`SignInAs(caller, sqlServer.Seeded)` signs in as one of the endpoint matrix's callers (below), or, for
`Caller.SignedOut`, adds nothing.

A request that the fallback policy or the `Manager` policy refuses then answers as problem details: `401` signed
out, from the session cookie, and `403` signed in, from the test-only sign-in, which forbids as the cookie does. To
test a role that `UserRole` does not have, such as `Admin`, a request also sends the `X-Test-Role` header,
whose value replaces the role claim's, and no other claim changes; the value `TestSignIn.NoRole` leaves the user with
no role claim (an empty value would not: the test server drops a header whose value is empty). The header needs
`X-Test-User`: a request with `X-Test-Role` alone fails to sign in, rather than going to the cookie, which would
ignore the header. `Grow2Notes.Tests/Platform/PoliciesOnSqlServerTests.cs` is the example.

### Signing in with the session cookie, on a fake clock

A test of the session itself signs in with the real session cookie, `__Host-grow2notes`, so its later requests meet
the app's own session checks: the security stamp, the idle limit and the 12-hour limit (design.md §8.4, §8.5). The
seeded users have no passkey or password to sign in with (a test of a sign-in endpoint signs in with a passkey, below),
so the test maps a test-only sign-in with `MapCookieSignIn()` (`Grow2Notes.Tests/Fixtures/CookieSignIn.cs`), which
signs in the user whose ID it is given through the app's `SignInManager.SignInAsync(user, isPersistent: false)`, as
setup does when it completes (§8.1 step 5). So the cookie is the one a real sign-in issues, `auth_time` included. That
call does not ask `CanSignInAsync`, so sign in Active users only.

The cookie is `Secure`, so the client calls `https://localhost`: `SignInWithCookieAsync` signs in a client from
`CreateHttpsClient()`, which keeps the cookie and sends it with every later request, and returns the cookie that the
sign-in set. A later request goes to the cookie as long as it has neither of the test-only sign-in's headers.

`UseClock` (`Grow2Notes.Tests/Fixtures/TestClock.cs`) hosts the app on a `FakeTimeProvider` in place of the system
clock, which then drives the sign-in time, the cookie's expiry, the stamp check, the 12-hour limit and `MelbourneClock`
alike; Data Protection keeps the real clock, whose keys do not expire within a run. Start the clock on a whole second,
such as `TestClock.Start` (2026-10-09T13:30:00Z): the cookie and `auth_time` keep times to the second, and the session
limits are tested to the second. Move the clock forward at least a second before each request: the stamp check runs
only once more time than its interval of zero has passed since the cookie was issued or last renewed, so on a clock
that has not moved, a request skips it, and a changed security stamp or role goes unseen.

```csharp
private readonly FakeTimeProvider clock = new(TestClock.Start);

// In the test:
await using var app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
using var client = app.CreateHttpsClient();
await client.SignInWithCookieAsync(sqlServer.Seeded.A.WorkerId);

clock.Advance(TimeSpan.FromSeconds(1));
using var response = await client.GetAsync("/api/...", TestContext.Current.CancellationToken);
```

Signing in changes no row, so a test may sign in as a seeded user; one that then changes the user, such as rotating
their security stamp, signing them out or changing their role, adds the user itself (the seeded-rows rule above).
`Grow2Notes.Tests/Fixtures/CookieSignInTests.cs` is the example.

### Ending a user's sessions on every device

A change to a user reaches each session of theirs on its next request (design.md §8.4, §8.5, §8.6): a reset, a
deactivation, a sign-out on another device or `admin signout-all` rotates the security stamp, which ends each with
`401`, and a change to Worker leaves the stamp, so a manager's request then gets `403`. `SignedInDevices`
(`Grow2Notes.Tests/Fixtures/SignedInDevices.cs`) shows it on several devices at once. Reset sign-in's tests use it
(`Grow2Notes.Tests/Features/Users/SignInResetTests.cs`, the example), and so does every later test of a change that
ends sessions or a role: deactivation, a change of role or email, Sign out
(`Grow2Notes.Tests/Features/Auth/LogoutEndpointTests.cs`), and the operator's `admin reset-signin` and
`admin signout-all`; T24's, that a deactivated person is signed out everywhere (S05.03.02, the third fact of
`Grow2Notes.Tests/Platform/PastRecordsTests.cs`). Such a change ends a connected assistant's access too, in the same
transaction: `AccessEndsTests` (`Grow2Notes.Tests/Features/Mcp/AccessEndsTests.cs`) does each event through its real
endpoint or command, shows every authorisation of the person revoked before any refresh or `/mcp` call, then the
refusal of both, and the person's own session by `SignedInDevices`. T36 added the through-MCP deactivation, and T37 adds
those of its tools, as rows of its `Events` list, each an `AccessEnd` with the tool call as its trigger.

- Host the app with `MapCookieSignIn()` and on a fake clock with `UseClock`, as above, and add the user the test
  changes (the seeded-rows rule above).
- `SignedInDevices.SignInAsync(app, clock, userId, count)` signs the user in on `count` devices, two by default, each a
  client from `CreateHttpsClient()` with the session cookie; fetches each device's antiforgery token, while the session
  still stands, for a `POST` later; and checks that each device's next request is let in.
- After the change, `AssertNextRequestsAsync(status, requests)` moves the clock a second before each device's next
  request, so the session check runs on it, and asserts its status: one request for each device, in order, such as
  `SignedInDevices.Me` (`GET /api/auth/me`, sent to each when none is given) or `SignedInDevices.Ping`
  (`POST /api/auth/ping`, with the device's token), or `NextRequest.Get(path)` and `NextRequest.Post(path)` for
  others. A `401` must also be problem details that clear the session cookie, as the session check answers once it
  ends a session.
- `AssertNextAnswersAsync(status, answer, requests)` does the same for requests answered with JSON, and asserts
  `answer` of each device's body too, as Edit user's tests read the role that `GET /api/auth/me` gives each session
  after a change to Worker (`Grow2Notes.Tests/Features/Users/UserEditEndpointTests.cs`).

```csharp
await using var app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
using var devices = await SignedInDevices.SignInAsync(app, clock, userId);

// The change, such as a manager's reset of the user's sign-in.

await devices.AssertNextRequestsAsync(HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);
```

### Calling /mcp in a test

A test of the AI-assistant access calls `/mcp` as an approved assistant does, with an access token that the real flow
issued, so that what it proves is what the endpoint's own validation does with a real token (mcp-server.md §4.5).
`AssistantFlow` (`Grow2Notes.Tests/Features/Mcp/AssistantFlow.cs`) is the fixture: the app with `Mcp:Enabled` on, on a
database and a fake clock of its own, with the approved assistant registered. `AccessTokenTests` and `McpEndpointTests`
are the examples.

- `AssistantFlow.StartAsync(factory, sqlServer, name)` starts the app, and `AddUserAsync(role)` adds a person to its
  organisation, or to another with `AddOrganisationAsync()` and `AddUserAsync(role, organisationId)`. `SignInAsync`
  signs the person in with the real session cookie, and `ConnectAsync(browser)` has them press Allow and the assistant
  exchange the code: the tokens of a connection. `RefreshEveryAsync` keeps a connection going on the fake clock.
- `PostMcpAsync(accessToken, origin, method)` makes one call as `AssistantFlow.McpRequest` builds it (a `tools/list`
  `POST` that the stateless server answers on its own), and `AssertChallengeAsync(response, error)` asserts the 401 of
  mcp-server.md §4.2: the document and the scope it names, and the `error` it carries, or none for a call with no token.
  `McpClientAsync(accessToken, revision)` is the SDK's own client for `2025-11-25` (the initialize handshake) or
  `2026-07-28` (details with each request); dispose it.
- `ProbeTools` are tools that only the tests have, to see a call as the server runs it and to do what no real tool
  does. They are in the app, beside the app's own tools, unless `StartAsync` is called with `probes: false`, which is
  the app as production has it. `probe_whoami` answers who the call is for, every claim of the principal it runs as,
  the organisation it runs in and the participants that its database context sees; `ProbeTools.ReadWhoAmI` reads the
  answer. A new tool of the tests belongs in `ProbeTools`. A test of a real tool, or of what every tool must do, uses
  `probes: false`, so that the tools listed are the ones a model gets.
- `AssistantFlow.ToolCall(name, arguments)` is the JSON-RPC body of a `tools/call`, which `PostMcpAsync` and
  `McpRequest` send as their `body` for a raw call, such as one with a token that must be refused. A call whose result
  is read goes through `McpClientAsync`, and `ToolScene.CallAsync` takes its arguments as a `JsonObject`.
- A tool that confirms a change (`ToolConfirmation`, mcp-server.md §5.3) answers by what the request's own `_meta`
  declares, which the SDK's client does not let a test choose per call. `PostToolCallAsync(accessToken, name,
  arguments, capabilities, requestState, inputResponses)` makes a raw `2026-07-28` `tools/call` that declares the
  capabilities it is given (`{}`, `{"elicitation":{"form":{}}}`, a blank `{"elicitation":{}}`, or URL only) and, to
  answer a form, retries with the `requestState` it returned and the answer by input request key. It returns the
  JSON-RPC `result`, from a JSON or an event stream body, so a test sees `input_required` as the wire has it.
  `McpClientAsync(..., elicitation)` takes a handler, then declares form elicitation and answers forms with it, to
  prove the SDK's own client. `probe_confirm` is the tool to try it on before a real tool uses it;
  `ToolConfirmationTests` shows how.
- `ToolScene.CreateAsync(flow)` is the world that the tests of the tools share: two organisations (made with
  `TestOrganisations`, so each has a manager and a Support person), a participant with names and goals that are unique to
  the scene, an archived participant and a date of birth. `ToolMatrix.Rows` holds one row for each tool the app lists
  (valid arguments, error cases with their codes, and what a manager of another organisation gets), and
  `ToolMatrixTests` holds every tool to its row; a new tool adds a row there. `McpAuditRows` reads back and asserts the
  audit rows of a call.
- `Features/Mcp/tools-list.json` is the reviewed surface of the production tools: for each tool its name, title,
  description, input and output schemas and annotations, sorted by name, which
  `McpEndpointTests.The_production_setup_lists_the_snapshot_tools_on_either_revision` compares with what `tools/list`
  answers on both protocol revisions. A change to a tool changes that file in the same PR. When the test fails it prints
  the whole file as it would be: copy it from `gh run view <id> --log-failed`, and review the diff.
  `OnboardingSkillTests` holds the onboarding skill in `.claude/skills/onboard-provider/` to this file: every tool
  name the skill writes must be one of its tools.
- `flow.TokenEdits.Queue(edit)` changes the next access token that the app issues (`TokenEdit`: another audience, no
  stamp hash, another stamp hash, another organisation), so that a real token can be one the endpoint must refuse. It
  applies once, so give each case its own flow, which xUnit's one class instance for each test already does.
- `flow.DeactivateAsync`, `ResetSignInAsync` and `ChangeRoleAsync` change the person's row and revoke nothing, so they
  are for the checks made on each call. Use the app's own endpoint or command, or `SignOutOfTheAppAsync`, wherever the
  test is about what ends a connection and when.
- `flow.Logs` and `flow.Outbound` hold what the app logged, at its own levels, and every outbound call it tried, which
  fails the test.
- A token lasts 10 minutes and the session 12 hours from sign-in, both on the fake clock: `flow.Clock.Advance`. To
  check the 12 hours, refresh up to a minute before, so that the access token is still valid after and only the check on
  the session can refuse it.
- `/mcp` takes bearer tokens only: the session cookie is refused there (a 401 challenge), and a bearer token is
  ignored under `/api`.

### Setting up an account, with a test-only passkey handler or a computed code

A test of the setup endpoints (design.md §8.1) calls them as the SPA does, for a user of its own:
`factory.InviteAsync(organisation)` (`Grow2Notes.Tests/Fixtures/SetupLinks.cs`) invites a worker through the invite core
and returns the link's user ID and token, read from its fragment as the SPA reads them, and
`client.StartSetupAsync(link)` fetches an antiforgery token, starts setup with it and returns it, for the later setup
steps to send with `PostWithTokenAsync`. Identity's passkey handler verifies a real WebAuthn attestation, which a .NET
test cannot easily make, so a test that completes setup with a passkey hosts the app with `UseTestPasskeyHandler()`
(`Grow2Notes.Tests/Fixtures/TestPasskeyHandler.cs`) in its place. Identity's sign-in manager still keeps the ceremony in
its two-factor cookie and ends it on the attempt, and the handler accepts any credential but
`TestPasskeyHandler.RefusedCredential`, as a made-up passkey for the user whose options the client asked for.

The test server gives a request no client address, so a test of an audit event's `IpAddress` gives each request one
in a startup filter, ahead of the app's middleware. `Grow2Notes.Tests/Features/Auth/SetupPasskeyEndpointTests.cs` is
the example of both.

Identity's authenticator provider checks codes but makes none, so a test that completes setup with a password gets
each code as a person's authenticator app would, from the key that the invite stored, which
`UserManager.GetAuthenticatorKeyAsync` reads: `AuthenticatorCodes.Now(key)`
(`Grow2Notes.Tests/Fixtures/AuthenticatorCodes.cs`) computes RFC 6238's code for the key at the current 30-second
step, and `AuthenticatorCodes.At(key, time)` the code at another time, such as ten minutes away for a wrong one.
Identity checks a code against the system clock, not the app's `TimeProvider`, so a test computes its codes from the
real time even when it hosts the app on `UseClock`'s fake clock.
`Grow2Notes.Tests/Features/Auth/SetupPasswordEndpointTests.cs` is the example.

### Signing in with a passkey

A test of a sign-in endpoint signs in as the SPA does, with a passkey (design.md §6.2, §8.2). The .NET tests run no
browser, so `TestAuthenticator` (`Grow2Notes.Tests/Fixtures/TestAuthenticator.cs`) stands in for the browser's
passkey prompt. It holds one passkey, an ES256 key pair that it makes, and signs assertions as a browser at the
factory's `App:Origin`, `https://grow2notes.example`, would: for that origin's host, the domain that the passkey
options name, with the person verified, and with its signature counter one higher each time, as Identity requires.
Identity's own passkey handler verifies them, as it verifies a real one.

- `TestAuthenticator.RegisterAsync(app.Services, userId)` makes an authenticator and stores its passkey for the user,
  through the app's `UserManager.AddOrUpdatePasskeyAsync`, as setup stores one. The seeded users have no passkey, so
  register it for a user the test adds itself (the seeded-rows rule above). A `new TestAuthenticator()` holds a
  passkey that no account holds, as the endpoint matrix's row for `POST /api/auth/passkey` sends. For a sign-in that
  fails, `GetAssertion(challenge, userVerified: false)` leaves the person unverified,
  `GetAssertion(challenge, origin: …)` is asked for by a page at another origin, such as a subdomain's, and
  `WithAnotherKey()` gives an authenticator that signs for the same passkey with a key of its own.
- `SignInWithPasskeyAsync(authenticator)` (`Grow2Notes.Tests/Fixtures/PasskeySignIn.cs`) signs in a client from
  `CreateHttpsClient()` as the SPA does: it fetches an antiforgery token, asks `POST /api/auth/passkey/options` for
  the options, which set the ceremony's cookie, and sends the authenticator's assertion over their challenge to
  `POST /api/auth/passkey`. It returns that response; the client keeps the session cookie it sets, and sends it with
  every later request. The request carries the `Origin` header that a browser sends with every `POST`; the app takes
  only an assertion asked for by a page at `App:Origin` itself (design.md A54).
  `SignInWithPasskeyAsync(challenge => …)` sends the credential that the function gives for the challenge instead,
  such as a failing assertion.
- `FetchPasskeyChallengeAsync(token)` and `SendPasskeyCredentialAsync(credentialJson, token)` are its two requests,
  for a test that sends a credential with no options asked for, after other options, after the clock has moved, or,
  with an `origin`, from a page at another origin. `PasskeySignIn.Request(credentialJson)` is the second request
  alone; the test adds the token.

Host the app on a fake clock with `UseClock`, and move the clock a second before each later request of the session,
as for the cookie sign-in above.

```csharp
private readonly FakeTimeProvider clock = new(TestClock.Start);

// In the test, for a user that it added:
await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
using var client = app.CreateHttpsClient();
using var signIn = await client.SignInWithPasskeyAsync(authenticator);

clock.Advance(TimeSpan.FromSeconds(1));
using var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
```

Each sign-in, and each failed one on a known account, commits an audit event about its user, so a test reads only the
events of a user it added, by the user's ID. `Grow2Notes.Tests/Features/Auth/PasskeySignInEndpointTests.cs` is the
example.

### Signing in with a password

A test of the password sign-in signs in as the SPA does, with an email, a password and the code from an
authenticator app (design.md §6.2, §8.2), for a user that it adds itself (the seeded-rows rule above), as its failures
count towards the user's lockout.

- `app.AddPasswordUserAsync(organisation)` (`Grow2Notes.Tests/Fixtures/PasswordSignIn.cs`) adds a worker through the
  app's `UserManager`, as setup's password path leaves one: with `PasswordSignIn.Password`, an authenticator key and
  two-factor on. It returns a `PasswordUser`, with the user's ID, address and key. `status` gives the user another
  status, and `twoFactorEnabled: false` leaves two-factor off, as setup never does.
- `AuthenticatorCodes.Now(key)` (`Grow2Notes.Tests/Fixtures/AuthenticatorCodes.cs`) computes the code that an
  authenticator app holding the key shows now: RFC 6238's, as Identity's authenticator provider checks it, which
  `AuthenticatorCodesTests` checks against the RFC's own test values. Identity checks a code against the system
  clock, not the app's `TimeProvider`, so a test computes its codes from the real time, even when it hosts the app on
  a fake clock. Identity accepts the codes of the steps either side of now, so a wrong code is the right key's code
  for a step well away from them: `AuthenticatorCodes.At(key, DateTimeOffset.UtcNow.AddMinutes(10))`.
- `SignInWithPasswordAsync(email, password, code)` signs in a client from `CreateHttpsClient()` as the SPA does: it
  sends the email and the password to `POST /api/auth/login`, checks that the answer asks for the code, and sends the
  code to `POST /api/auth/login/totp`, each with an antiforgery token that it fetches first. It returns the code
  step's response. Between the two, the client keeps Identity's two-factor cookie, which holds the code step, and
  then the session cookie, which it sends with every later request. `SendPasswordAsync(email, password)` and
  `SendCodeAsync(code)` are its two requests, for a test that does something in between, such as changing the user
  or asking for passkey options, which replace the code step.
- The code step lasts `TwoFactorCookie.Lifetime` on the app's clock, D69's 5 minutes, which Program.cs sets on
  Identity's two-factor cookie, the one that holds a passkey ceremony too. A test of the code step's window, or of a
  ceremony's, moves the fake clock by that constant, never by a number of its own, so the windows cannot drift apart.
  Identity times the lockout on the system clock, so a test reads a lockout's end against `DateTimeOffset.UtcNow`.
  `Grow2Notes.Tests/Features/Auth/PasswordSignInLimitsTests.cs` is the example of both.

Host the app on a fake clock with `UseClock`, and move the clock a second before each later request of the session,
as for the cookie sign-in above.

```csharp
private readonly FakeTimeProvider clock = new(TestClock.Start);

// In the test:
await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
var user = await app.AddPasswordUserAsync(sqlServer.Seeded.A);
using var client = app.CreateHttpsClient();
using var signIn = await client.SignInWithPasswordAsync(
    user.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(user.AuthenticatorKey));

clock.Advance(TimeSpan.FromSeconds(1));
using var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
```

As with a passkey, a test reads only the audit events of a user it added, by the user's ID.
`Grow2Notes.Tests/Features/Auth/TotpSignInEndpointTests.cs` is the example.

### Reading the emails the app sent

No test sends real email (design.md A58). `Grow2NotesFactory` puts a `RecordingEmailSender`
(`Grow2Notes.Tests/Fixtures/RecordingEmailSender.cs`) in place of the app's email sender, and it records each message
that the app sends, as an `EmailMessage` with its `To`, `Subject` and `Text`, in the order sent, and sends nothing. A
test reads them from the app it called:

```csharp
var sent = app.Services.GetRequiredService<RecordingEmailSender>().Sent;
var email = Assert.Single(sent, message => message.To == address);
```

Each app has a recorder of its own. One made with `WithWebHostBuilder` starts with none recorded, so a test that shows
that nothing was sent, or exactly what was, uses such an app, as
`Grow2Notes.Tests/Platform/OperatorCommands/BootstrapCommandTests.cs` shows that `admin bootstrap` sends nothing. The
class fixture's app is shared by the class's tests, so a test that calls it finds its own messages by the address it
used, which no other test uses (the seeded-rows rule above). `AppWithoutDatabase` has the app's console sender in
Development, from `Email:UseConsole` in `appsettings.Development.json`, which writes each message to standard output;
the app sends through Resend only where `Email:ApiKeySecretUri` and `Email:SenderAddress` are set, which no test sets
but `Grow2Notes.Tests/Platform/Email/EmailSendingTests.cs`, whose apps send nothing.

`SetupLinks.FromEmail(email)` (`Grow2Notes.Tests/Fixtures/SetupLinks.cs`) reads the link in a setup-link email, and
`SetupLinks.Read(link, email)` one that an operator command printed, as `SetupLinks.InviteAsync` reads the invite
core's, for a test that opens it, such as with `StartSetupAsync`, or shows that it has ended with
`client.AssertSetupLinkEndedAsync(link)`, which expects `410 auth.setup_link_invalid`. A test of an email that cannot be
sent hosts its app with `ConfigureTestServices(FailingEmailSender.ReplaceAppSender)`: its sender sends nothing and
throws an exception that holds the address and the whole text, link and all, so the test shows that neither reaches the
logs. `Grow2Notes.Tests/Features/Users/InviteUserEndpointTests.cs` is the example of both. A test that looks for a setup
token in the logs (`RecordedLog.Holds`) looks for it escaped too, as a link holds it, since its base64 almost always has
a `+`, `/` or `=` in it.

### The Reviewed list, the To review count and the messaging build check

- `GET /api/reviews?status=reviewed` answers `{items, hasMore}`: at most 30 reviews, newest first (`ReviewedAtUtc`, then
  the review's `Id`, both descending, the `Id` in SQL Server's `uniqueidentifier` order), one item per `NoteReview` row.
  An item holds `reviewId`, `participantId`, `givenName`, `familyName`, `noteDate`, `authorDisplayName`, `flagReason`
  (the whole reason of the flag the review cleared: the latest flagged version at or below the reviewed one),
  `reviewedBy` (the reviewer's display name, a string), `reviewedAtUtc` and `comment`.
  `Grow2Notes.Tests/Features/Reviews/ReviewListEndpointTests.cs` shows each.
- The next page is asked for with `&before={reviewedAtUtc}&beforeId={reviewId}` of the last item, both or neither
  (one alone is a `400`). The cursor is exclusive, so no review is skipped or repeated, even among reviews of one
  millisecond. `before` is bound as a `DateTimeOffset`, so a test sends the server's own value, which ends in `Z`: a
  value typed by hand without the `Z` binds as the host's local time. A test that needs every page walks them as the
  screen does (`ReviewedPagesAsync`), and a test that needs to know every review of an organisation, such as the
  paging tests' 61 and 33, adds an organisation of its own with `services.AddOrganisationAsync()`
  (`Grow2Notes.Tests/Fixtures/TestOrganisations.cs`) and its reviews with `services.AddReviewAsync(…)`
  (`TestNotes.cs`), because the seeded organisations' reviews are shared with other tests. A test never assumes .NET's
  `Guid` order for reviews of one millisecond: SQL Server's differs.
- Who may call it is not a row of the endpoint matrix, which keeps one row per endpoint (`GET /api/reviews` is still
  called with `status=toReview`). `ReviewListEndpointTests` has the same four callers as a theory instead: signed out
  `401`, A's worker `403`, A's manager `200` with A's review, B's manager `200` with none of A's.
- `GET /api/auth/me`, and the answers of the passkey and TOTP sign-ins and of setup completion, which all go through
  `MeEndpoint.OfUserAsync`, carry `toReviewCount` for a manager, `0` included, and leave the key out for a worker. It
  is the number of the organisation's notes with `FlagStatus` To review, so it equals the length of the To review
  list. A test of the count adds an organisation of its own, as above, because other tests flag notes in A.
- `Grow2Notes.Tests/Platform/MessagingOutOfScopeTests.cs` is the build check for the scope line on messaging. It reads
  the repository as text: no package ID in `Directory.Packages.props`, a `.csproj` or the two `package.json` files names
  SMS, push, SMTP, mail or another messaging service (Resend is called over HTTP, with no package); the `.cs` files of
  the web project that name `IEmailSender` or a mail library are exactly the listed set; the mail libraries are named
  only in `Platform/Email`; and the three handlers that issue a setup link use the sender only as the first argument of
  `SetupLinkEmail.SendAsync`. A change that adds a sender, or a legitimate use of a mail library, changes the list in
  that test in the same change. Matching by name is coarse by nature.

### The endpoint matrix

`Grow2Notes.Tests/EndpointMatrixTests.cs` takes every endpoint from the app's `EndpointDataSource` and calls it as each
`Caller`: signed out, A's worker, A's manager, B's manager and A's Support person (design.md §2, §9.2). Each must
answer with the status in the endpoint's row in `Grow2Notes.Tests/EndpointMatrix.cs`; the matrix checks the status, and
the answer only where the row has a check of it (below). An endpoint with no row fails the run, and so does a row with
no endpoint, so a story that maps, changes or removes an endpoint changes its row in the same change. The app is hosted
there with no test-only endpoints, as in production, so an endpoint that a test maps for itself needs no row.

- A row finds its endpoint by the methods it is mapped with and its route pattern as mapped, the `/api` group's prefix
  included: `["GET"]` for `MapGet`, and `AnyMethod` for an endpoint mapped with `Map` or `MapFallback`, which names no
  method and is called with `GET`, `POST`, `PUT` and `DELETE`. One row stands for every file of the SPA build
  (`StaticAssets`), whose names change with each build.
- The four statuses follow design.md §2 and §9.1: `401` signed out at an endpoint that is not `AllowAnonymous`, `403`
  for A's worker at one with the `Manager` policy, and, from the first endpoint that loads one of an organisation's
  records, `404` for B's manager at A's (§9.2).
- By default the matrix calls an endpoint at its route pattern. A row whose route has parameters builds its own
  request, such as `At("/api/no-such-path")`, and one whose endpoint reads a JSON body gives it one with `WithJson`,
  as `POST /api/auth/setup/start`'s gives a made-up link; a row's builder is asked again for each call. A call
  commits, so a row whose calls change something builds each call's request after adding the record that call acts
  on, in organisation A, as the seeded-rows rule above says, with the app's services and the seeded organisations
  that `EndpointCall` carries: `POST /api/admin/users/{id}/setup-link`'s row, the first, invites a user into A
  through the invite core and sends their ETag, so A's manager gets `204` and B's manager `404`;
  `PUT /api/admin/participants/{id:guid}`'s adds a participant to A for each call with `AddParticipantAsync`
  (above); and the archive's and restore's rows add an active and an archived one.
- A row of an endpoint that lists an organisation's rows checks the answer too, so that the matrix shows that no
  caller's list holds another organisation's (§5.9 item 7). Its `Answer` is given the response to a call that got the
  row's status, when that is a success, with the caller and the seeded organisations, and lists how the answer differs
  from what it expects. Tests add rows to the seeded organisations, so a check looks for the seeded rows, never for a
  whole list: `GET /api/admin/users`' row, the first, checks that each caller's list holds both of their own
  organisation's seeded users and neither of the other's, and `GET /api/admin/participants`' row their own
  organisation's seeded participant and not the other's. A list that no seeded row is on looks for the row its own
  call adds: `GET /api/reviews`' builder flags a note in A for each call and names its participant in the request's
  `EndpointMatrix.FlaggedInA` option, which the check reads from the response's `RequestMessage`, so that A's
  manager's To review list holds that note and B's manager's does not.
- The matrix calls `https://localhost`, as the SPA's antiforgery token needs (above), and fetches each caller's own
  token first. A call that changes something (`EndpointCall.ChangesState`: a method other than `GET`, `HEAD`, `OPTIONS`
  or `TRACE`, at an endpoint that names its methods) sends that token, and the matrix makes it twice more: without one,
  expecting `400` from the `/api` group's filter, and with it but with a form-encoded body in place of the row's,
  expecting `415` (§9.8 item 3; at an endpoint that reads JSON, binding answers the form body's `415` first, the same
  status). Of the statuses a row gives, only authorization's `401` and `403` come before the filter (binding and
  validation run first too, but a row's requests are valid), so at an endpoint that is not `AllowAnonymous`, the
  matrix expects the row's `401` or `403` to both calls too. At one that is, such as `POST /api/auth/login`, whose
  `401` to bad credentials is its own (design.md §6.2), it expects `400` and `415`. So a row's statuses are those of a
  call with the token, and an endpoint that changes something outside the group fails the run, even one that answers
  `401` to every caller. A `403` that the handler of an endpoint that is not `AllowAnonymous` gives, such as
  `note.not_editable` (§6.3), comes after the filter, yet the matrix expects it to both calls too; the first such row
  will need to say what they get. An endpoint that names no method, such as a catch-all, is called with `POST`, `PUT`
  and `DELETE` too, but changes nothing, and gets no token.
- `EndpointMatrix.CallsTo` lists every call the matrix makes, and `EndpointCall.RequestAsync` builds the request as
  the endpoint's row does, so a test of every endpoint, such as `PoliciesOnSqlServerTests`' role test, uses them. An
  endpoint with no row, such as one the test maps for itself, gets a request to its route pattern.

### Adding an admin endpoint

Every endpoint that manages users or configuration (design.md §6.6) is under `/api/admin`, which only managers may
call, and none of them deletes anything (§2, §14 M1, A29). Two tests that need no Docker hold each one to that, so a
story adds one as follows.

- Map it in the `/api/admin` group, which `ApiGroup.MapAdminGroup` maps inside the `/api` group, so the group's
  `Manager` policy applies to it, as do the `/api` group's antiforgery and JSON checks; never give it a policy of its
  own. The first admin endpoint maps the group in `Program.cs`, and the others are mapped in the same group. Name its
  methods with `MapGet`, `MapPost` or `MapPut`: never `MapDelete`, nor `Map`, which names none and so answers
  `DELETE` too; and never mark it `AllowAnonymous`. `Grow2Notes.Tests/Platform/AdminGuardTests.cs` reads every
  endpoint under `/api/admin` from the app's `EndpointDataSource` and fails, naming it, if it names no method or names
  `DELETE`, lacks the `Manager` policy, has a policy of its own beside the group's (one given in the group, even the
  same policy), or is `AllowAnonymous`. That is all it checks: it reads each endpoint's metadata, which does not say
  where the endpoint was mapped, so one mapped outside the group with a `Manager` policy of its own passes it, and
  that the endpoint is in the group is for review to see. One that changes something outside the `/api` group still
  fails the endpoint matrix, whose calls without the token and with a form body expect that group's `400` and `415`
  (above).
- Add its row to the endpoint matrix (above) in the same change, with `401` signed out and `403` for A's worker, as
  the group's policy answers them: `Grow2Notes.Tests/Platform/AdminMatrixRowsTests.cs` fails, naming the row, if a
  row under `/api/admin` expects anything else, and the matrix then checks that the endpoint answers as its row says.
  A's manager's status is the endpoint's own. Where the endpoint loads one of A's records, such as
  `PUT /api/admin/goals/{goalId}`, the row's request names one of A's, and B's manager gets `404`, as for any record
  in another organisation (§9.2); at one that loads none, such as a list or an add, B's manager acts in B and gets
  what A's manager gets.
- Give its handler its own tests on the SQL Server container: what it changes and the audit event it writes, each
  §6.9 code it answers, and that it reaches nothing of B's (§5.9 item 7). The matrix checks the status only, and the
  two tests above only how the endpoint is mapped and what its row expects.
  `Grow2Notes.Tests/Platform/Configuration/ConfigurationChangesOnSqlServerTests.cs` is the example for a change sent
  with `If-Match`: in an organisation the test adds with two managers, the `200` with its new `ETag` and audit event,
  the second manager's `412`, the `428`, and a save of the stored values that writes nothing.
  `Grow2Notes.Tests/Features/Users/UserSetupLinkEndpointTests.cs` is the example for a change to a user, which the
  handler makes through `UserChanges`: B's manager's `404` for A's user, with or without their ETag, the `428`, a
  stale ETag's `412`, and the `412` of a save that finds the user changed after the handler loaded them, which
  `HoldsTheFirstSaveOfTheUser` (`Grow2Notes.Tests/Fixtures/HoldsTheFirstSaveOfTheUser.cs`), a `SaveChangesInterceptor`,
  holds back while another request changes the user. A change under the last-manager rule (A26) is tested in an
  organisation the test adds, so that it alone decides who is an Active manager there; with the same interceptor,
  `Grow2Notes.Tests/Features/Users/SignInResetTests.cs` shows two managers resetting each other at once.
- Give each event type that it writes a row in the shared audit-events test,
  `Grow2Notes.Tests/Platform/Audit/AuditEventsTests.cs` (design.md §5.4, §14 M5): a function that triggers the event
  through the real endpoint or operator command, in seeded organisation A, adding the rows it acts on, or, for a change
  to an organisation's own row, in an organisation that it adds, as `guide_prompts.updated`'s row does, and returns the
  entry that it must write, with its organisation, actor, entity, participant and `Details`. The test finds exactly one
  new entry, and compares the `Details` as JSON. `participant.created`'s row is the example. An event type with more
  than one trigger gets a row for each, named by the event type, " by " and the trigger, as `user.setup_link_sent`'s
  Resend invite and Reset sign-in are. A trigger that changes every organisation, as `admin signout-all` does, writes
  one entry for each, so its row runs on a migrated database of its own, which `DatabasesOfTheirOwn` names, in an
  organisation that it adds there. The rows are one for each event type, plus one for each trigger that §5.4 names:
  `AuditTypesTests` fails while the event types of the rows and the `AuditEventTypes` constants differ, naming each
  one that is missing, so a new event type cannot be added without a row. The other triggers of the sign-in events (the
  passkey, the code step and passkey setup) are proved by the exact entries in their endpoint tests.
