---
sprint: 2
release: R1
start: 2026-10-05
end: 2026-10-18
capacity_hours: 29
planned_points: 14
done_points:
done_stories:
---
# Sprint 02

**Goal:** Every accepted change runs in the test environment in Melbourne with no manual step, and the operator can see
its health with no personal details.

The dates were the forecast's ([releases.md](../releases.md)). Sprint 01 finished on 4 October 2026, before its planned
start, so work on this sprint may start earlier than 26 October. If it does, the sprint still lasts two weeks and is
never extended (D53): change `start` to the real start and `end` to two weeks later in the front matter, and record
the real start under *Result*. releases.md's dates are moved at the sprint review's re-forecast.

## Capacity

| | Hours |
|---|---|
| Build time: 2 weeks × about 20 hours (D53, D54) | 40 |
| Less sprint events: planning 1, review 1, retro 0.5, refinement 1.5 | −4 |
| Less a 20% buffer for the unknown | −7 |
| Less leave and known commitments | 0 (none known on 4 October; check when the sprint starts) |
| **Focused story time** | **29** |

Planned by capacity (README §7.2): the four Ready stories next in backlog order were taken, and their tasks were given
rough hour guesses that came to about 29 hours, so no fifth story was taken. As the method says, the guesses are not
kept and will never be compared with actuals. Sprint 01's 13 points took one session, so the hours are probably
generous, but much of this sprint waits on Azure deployments and on the operator's steps by hand. Sprint 01's retro
asks for a re-forecast from measured throughput after this sprint; until then the rates in releases.md stand.

## Plan

| Order | Story | Title | Type | Points |
|---|---|---|---|---|
| 1 | S00.02.01 | Bootstrap Azure for both environments | enabler | 3 |
| 2 | S00.02.02 | Test environment resources | enabler | 5 |
| 3 | S00.02.03 | Deploy to test from main | enabler | 3 |
| 4 | S00.02.06 | Server telemetry with IDs only | enabler | 3 |
| | | **Total at planning** | | **14** |
| 5 | S00.03.02 | Tenant isolation and its tests (pulled in on 8 October 2026) | enabler | 3 |
| 6 | S00.03.03 | Audit writer (pulled in on 8 October 2026) | enabler | 2 |
| 7 | S00.03.04 | Deny by default, CSRF and the endpoint matrix (pulled in on 9 October 2026) | enabler | 5 |
| 8 | S00.04.01 | Session checked on every request (pulled in on 9 October 2026) | enabler | 3 |
| 9 | S06.01.04 | Security headers and CSP on every response (pulled in on 9 October 2026) | enabler | 2 |
| 10 | S00.03.05 | Bootstrap the organisation and first manager (pulled in on 9 October 2026) | enabler | 3 |
| 11 | S00.01.03 | Shared look, buttons and page status (pulled in on 9 October 2026) | enabler | 3 |
| 12 | S00.01.04 | Shared loading, failure and form-error handling (pulled in on 9 October 2026) | enabler | 3 |
| 13 | S00.01.05 | Copy module, date formats and wording checks (pulled in on 9 October 2026) | enabler | 3 |
| 14 | S00.04.02 | Set up my account with a passkey (pulled in on 12 October 2026) | story | 5 |
| 15 | S00.04.04 | Sign in with a passkey (pulled in on 12 October 2026) | story | 5 |
| 16 | S00.05.01 | Navigation for my role (pulled in on 12 October 2026) | story | 3 |
| 17 | S00.04.03 | Set up my account with a password and authenticator app (pulled in on 12 October 2026) | story | 5 |
| 18 | S00.04.05 | Sign in with email, password and code (pulled in on 12 October 2026) | story | 5 |
| 19 | S00.04.06 | Sign out (pulled in on 12 October 2026) | story | 2 |

These are the next four items in the backlog. S00.02.02 needs S00.02.01's resource groups and identities; S00.02.03
needs S00.02.02's database and web app; S00.02.06 adds `monitoring.bicep` to `main.bicep` and is checked in test after
S00.02.03's first deploy. Together they meet the Definition of Done's "Deployed to the test environment" line for this
and every later story. If time is left over, take the next story in backlog order (S00.03.02) once it is Ready.

**Pulled in on 8 October 2026.** S00.02.01 and S00.02.03 are done, S00.02.06 is in review (PR #19), and S00.02.02's
last checks wait on the owner's answer to Q6, so time was left over. As the paragraph above allows, S00.03.02 was
refined to Ready that day, its tasks were written in its own section, and it was taken into the sprint. Its 3 points
are not added to `planned_points`, which stays at the 14 planned on 4 October. They count in `done_points` only if it
is done by 18 October; otherwise it goes back to the backlog (README §7.2 step 4).

**S00.03.03, pulled in on 8 October 2026.** S00.03.02 was pulled in earlier that day and is in review (PR #21),
S00.02.06 is done, and S00.02.02's last checks still wait on the owner's answer to Q6, so time was left over again.
S00.03.03, next in backlog order, was refined to Ready the same day, its tasks were written in its own section, and it
was taken into the sprint. Its 2 points are not added to `planned_points` either, which stays at 14. They count in
`done_points` only if it is done by 18 October; otherwise it goes back to the backlog (README §7.2 step 4).

**S00.03.04, pulled in on 9 October 2026.** S00.03.02 is done, S00.03.03 was pulled in on 8 October and is in review
(PR #24), and S00.02.02's last checks still wait on the owner's answer to Q6, so time was left over again. S00.03.04,
next in backlog order, was refined to Ready that day and re-sized from 3 points to 5 (its *Notes* say why), its tasks
were written in its own section, and it was taken into the sprint. Its 5 points are not added to `planned_points`
either, which stays at 14. They count in `done_points` only if it is done by 18 October; otherwise it goes back to the
backlog (README §7.2 step 4). It brings `/api/auth/antiforgery` and its smoke check into this sprint, not Sprint 03
(*Settled at planning*, the deploy smoke test).

**S00.04.01, pulled in on 9 October 2026.** S00.03.03 is done (PR #24, its deploy recorded in PR #25), S00.03.04 was
pulled in earlier that day and is in review (PR #26), and S00.02.02's last checks still wait on the owner's answer to
Q6, so time was left over again. S00.04.01, next in backlog order, was refined to Ready the same day, its tasks were
written in its own section, and it was taken into the sprint at 3 points (its *Notes* say why they stand). They are not
added to `planned_points` either, which stays at 14, and count in `done_points` only if it is done by 18 October;
otherwise it goes back to the backlog (README §7.2 step 4). Its tasks build on S00.03.04's policies, `/api` group,
endpoint matrix and test-only sign-in, so they are built on `main` once PR #26 has merged.

**S06.01.04, pulled in on 9 October 2026.** S00.03.04 is done (PR #26, its deploy recorded in PR #27), S00.04.01 was
pulled in earlier that day and has merged (PR #28) with its after-deploy check to come, and S00.02.02's last checks,
run after the owner's answer to Q6, are in review (PR #29), so time was left over again. S06.01.04, next in backlog
order (releases.md places it straight after S00.04.01, before the first screen), was refined to Ready the same day, its
tasks were written in its own section in F06.01, and it was taken into the sprint at 2 points (its *Notes* say why they
stand). They are not added to `planned_points` either, which stays at 14, and count in `done_points` only if it is done
by 18 October; otherwise it goes back to the backlog (README §7.2 step 4). Its tasks build on S00.04.01's HSTS,
session cookie and `/api/auth/me`, so they are built on `main` from PR #28's merge. With it, all five stories that
releases.md forecast for Sprint 03 are in this sprint.

**S00.03.05, pulled in on 9 October 2026.** S06.01.04 was pulled in earlier that day and has merged (PR #31), with
its deploy to test recorded in review; S00.04.01 is done (PR #28, its deploy recorded in PR #30), and so is S00.02.02
(its Q6 checks recorded in PR #29), so time was left over again. S00.03.05, next in backlog order (releases.md
forecast it for Sprint 04, just before the first screen, S00.04.02), was refined to Ready the same day, its tasks were
written in its own section, and it was taken into the sprint at 3 points (its *Notes* say why they stand). They are
not added to `planned_points` either, which stays at 14, and count in `done_points` only if it is done by 18 October;
otherwise it goes back to the backlog (README §7.2 step 4). Its tasks build on S00.03.02's tenant `Use` block and
S00.03.03's audit writer, both done, and add one app setting through `main.bicep`: the address that its setup links
start with. One task is the operator's, by hand, in the test web app's SSH console, and changes nothing there; the
command's first real run in test is handed to S00.04.02, the first screen to open the link it prints (its *Notes* say
why).

**S00.01.03, pulled in on 9 October 2026.** S06.01.04 is done (PR #31, its deploy recorded in PR #32), S00.03.05 was
pulled in earlier that day, and every other story in the sprint is done, so time was left over again. S00.01.03, next
in backlog order (releases.md forecast it for Sprint 04 with S00.03.05, just before the first screen, S00.04.02), was
refined to Ready the same day, its tasks were written in its own section, and it was taken into the sprint at 3
points (its *Notes* say why they stand). They are not added to `planned_points` either, which stays at 14, and count
in `done_points` only if it is done by 18 October; otherwise it goes back to the backlog (README §7.2 step 4). Its
tasks build on S00.01.01's SPA project, S00.01.02's SPA checks in CI and S06.01.04's strict policy and Playwright
fixture, all done. They change the SPA, its tests and two documents, and no server code, so it can be built beside
S00.03.05; none of them needs the operator.

**S00.01.04, pulled in on 9 October 2026.** S00.03.05 (PR #34) and S00.01.03 (PR #35), both pulled in earlier that
day, have merged, with their checks after the deploy to come, and every other story in the sprint is done, so time was
left over again. S00.01.04, next in backlog order (releases.md forecast it for Sprint 04 with S00.03.05 and S00.01.03,
just before the first screen, S00.04.02), was refined to Ready the same day, its tasks were written in its own section,
and it was taken into the sprint at 3 points (its *Notes* say why they stand). They are not added to `planned_points`
either, which stays at 14, and count in `done_points` only if it is done by 18 October; otherwise it goes back to the
backlog (README §7.2 step 4). Its tasks build on S00.01.03's `Button`, `ButtonGroup`, `PageStatus` and tokens, merged
in PR #35, and on S00.03.04's antiforgery endpoint, problem details and `If-Match` format, done, and add TanStack Query
to the SPA. They change the SPA, its tests, five UX files and two stories' Notes, and no server code; none of them
needs the operator.

**S00.01.05, pulled in on 9 October 2026.** S00.01.04, pulled in earlier that day, has merged (PR #36) and been
deployed to test, with its check after the deploy to come, as have S00.03.05 (PR #34) and S00.01.03 (PR #35), and every
other story in the sprint is done, so time was left over again. S00.01.05, next in backlog order (releases.md forecast
it for Sprint 04 with S00.03.05, S00.01.03 and S00.01.04, just before the first screen, S00.04.02), was refined to
Ready the same day, its tasks were written in its own section, and it was taken into the sprint at 3 points (its
*Notes* say why they stand). They are not added to `planned_points` either, which stays at 14, and count in
`done_points` only if it is done by 18 October; otherwise it goes back to the backlog (README §7.2 step 4). Its tasks
build on S00.01.04's `src/copy` modules, merged in PR #36, and on S00.01.02's CI, done. They change the SPA, its lint
and tests, CI's Test step and two new CI steps, the .NET tests, four UX files and four stories' Notes, and no server
code. One task is the operator's, by hand: the repository secret `FORBIDDEN_NAME`, which holds the parent company's
name, since the name may appear nowhere in the repository (D42). On 12 October 2026 the owner dropped the automated
check that read it, so that task is gone, and review keeps the name out (D72).

**S00.04.02, pulled in on 12 October 2026.** S00.01.05, pulled in on 9 October, has merged (PR #37) and been deployed
to test, with its check after the deploy to come; S00.03.05 is done (closed in PR #40), as are S00.01.03 and
S00.01.04, and every other story in the sprint is done, so time was left over again. S00.04.02, next in backlog order
(releases.md forecast it for Sprint 05, as the first screen), was refined to Ready that day, its tasks were written in
its own section, and it was taken into the sprint at 5 points (its *Notes* say why they stand). They are not added to
`planned_points` either, which stays at 14, and count in `done_points` only if it is done by 18 October; otherwise it
goes back to the backlog (README §7.2 step 4). Its tasks build on S00.03.05's setup token, invite core and
`admin bootstrap`, S00.04.01's sign-in manager, session cookie and `Me`, and S00.01.03 to S00.01.05's shared parts,
`api()` wrapper and copy module, and add SimpleWebAuthn to the SPA and the setup routes to S00.05.01's router and
shell, which merged first (its *Notes* say how setup stays outside the wait for `/me`). One task is the operator's, by
hand, after the deploy: the first real `admin bootstrap` in the test web app's SSH console, as `ops/break-glass.md`
says, and the first setup with its link, which also makes the checks in test that S00.02.02, S00.03.03, S00.03.05 and
S00.04.01 handed to this story. Nothing else is run in the console, as the owner dropped S00.03.05's rehearsal there
on 12 October 2026, and nothing checks for the parent company's name (D72).

**S00.05.01, pulled in on 12 October 2026.** S00.01.05 has merged (PR #37) and been deployed to test, with its check
after the deploy to come; S00.03.05 is done (PR #40), as are S00.01.03 and S00.01.04 (PR #38) and every other story in
the sprint, so time was left over again. S00.05.01 comes after the setup and sign-in stories (S00.04.02 to S00.04.05)
in the backlog order (releases.md forecast it for Sprint 06), but depends on none of them, so it was refined to Ready
that day, its tasks were written in its own section, and it was taken into the sprint at 3 points (its *Notes* say
why they stand), to be built beside them on its own branch. Its 3 points are not added to `planned_points` either,
which stays at 14, and count in `done_points` only if it is done by 18 October; otherwise it goes back to the backlog
(README §7.2 step 4). Its tasks build on S00.04.01's `/api/auth/me` and on S00.01.03 to S00.01.05's shared parts and
copy module, and add React Router to the SPA. As built, with its branch review, they change the SPA and its tests, the
end-to-end tests and tests/README.md, five UX files (app-shell-nav.md, empty-loading-error.md, microcopy.md, today.md
and daily-report.md), coverage.md, the Notes of S00.01.04, S00.01.05, S00.06.01 and the two setup stories not yet built
(S00.04.02 and S00.04.03), and the criteria and Notes of S00.04.07 and S00.04.08, and no server code; none of them needs
the operator. The parts that the setup and sign-in stories need too (the route table, the page-name list, the page-title
hook, the `['me']` query and the signed-out frame) are built once, by whichever branch merges first, and reused by the
other (its *Notes*). Whether both setup paths end on its `/` is checked by whichever of it and each setup story merges
second. It is 16 in the *Plan* and in the list below, as S00.04.02 and S00.04.04, pulled in the same day on their own
branches, are 14 and 15; at each of the three pull requests the numbers are made to follow what `main` has by then.

**S00.04.04, pulled in on 12 October 2026.** S00.01.05 has merged (PR #37), with its check after the deploy to come,
and every other story in the sprint is done, S00.03.05 the last of them (PR #40), so time was left over again.
S00.04.04 follows S00.04.02 and S00.04.03 in backlog order (releases.md forecast all three for Sprint 05), but builds
on neither, so it can be built beside them (its *Notes* say why). It was refined to Ready that day and re-sized from
3 points to 5 (its *Notes* say why), its tasks were written in its own section, and it was taken into the sprint. Its
5 points are not added to `planned_points` either, which stays at 14. They count in `done_points` only if it is done
by 18 October; otherwise it goes back to the backlog (README §7.2 step 4). Its tasks build on S00.04.01's sign-in
manager, `Me` and session cookie, S00.03.03's audit writer, S00.01.03's `Button` and `PageStatus` and S00.01.04's
`api()` wrapper and form parts, all done. They add two anonymous endpoints, the start-up that shows sign-in and the
sign-in view's passkey part, and add `@simplewebauthn/browser` to the SPA. None of them needs the operator: signing
in with a passkey on a real phone and a laptop in test is F00.04's feature-level check, once S00.04.02's setup has
made an account there.

**S00.04.06, pulled in on 12 October 2026.** S00.05.01, pulled in earlier that day, has merged (PR #41) and is being
deployed to test, with its check after the deploy to come; S00.01.05 (PR #37) awaits its check after the deploy too,
and every other story in the sprint but the setup and sign-in stories being built on their own branches is done, so
time was left over again. S00.04.06 comes after the setup and sign-in stories (S00.04.02 to S00.04.05) and S00.05.03
in the backlog order (releases.md forecast it for Sprint 06), but depends on none of them, so it was refined to Ready
that day, its tasks were written in its own section, and it was taken into the sprint at 2 points (its *Notes* say why
they stand), to be built on its own branch beside them. Its 2 points are not added to `planned_points` either, which
stays at 14, and count in `done_points` only if it is done by 18 October; otherwise it goes back to the backlog (README
§7.2 step 4). Its tasks build on S00.04.01's sign-in manager, session cookie and test-only cookie sign-in, S00.03.03's
audit writer, S00.03.04's `/api` group and endpoint matrix, S00.01.04's `api()` wrapper and S00.05.01's Account panel.
They add `POST /api/auth/logout` and Sign out in the Account panel, with their tests and an end-to-end test, and change
app-shell-nav.md and the Notes of S00.04.08, whose reload for a different person uses the same module, and of
S01.04.02 and S02.02.05, which add Sign out's leave-guard and autosave steps; none of them needs the operator. HTTPS for
CI's end-to-end run, which its sign-out needs for the antiforgery token, is the `X-Forwarded-Proto: https` header sent
through Playwright's `extraHTTPHeaders`, with no change to CI, set in its spec unless `main`'s Playwright config already
sets it for every test (its *Notes*). It is 19 in the *Plan* and in the list below, as 17 and 18 are left for S00.04.03
and S00.04.05, the setup and sign-in stories that follow S00.04.02 and S00.04.04 on their own branches; at each pull
request the numbers are made to follow what `main` has by then.

**S00.04.03, pulled in on 12 October 2026.** S00.04.02, pulled in earlier that day, has merged (PR #43) and been
deployed to test, with its checks after the deploy to come, as has S00.05.01 (PR #41); S00.01.05 (PR #37) awaits its
check after the deploy too, S00.04.04 is in review on its own branch (PR #42), and every other story in the sprint is
done, so time was left over again. S00.04.03, next in backlog order after S00.04.02 (releases.md forecast both for
Sprint 05), was refined to Ready that day, its tasks were written in its own section, and it was taken into the sprint
at 5 points (its *Notes* say why they stand). They are not added to `planned_points` either, which stays at 14, and
count in `done_points` only if it is done by 18 October; otherwise it goes back to the backlog (README §7.2 step 4).
Its tasks build on S00.04.02's enrolment cookie, completion, setup layout and end-to-end fixtures, S00.05.01's shell
and `['me']` query, S00.03.05's invite core, which stores the authenticator key, and S00.01.04 and S00.01.05's form
parts, `api()` wrapper and copy module. They add three setup endpoints and QRCoder to the server, views F and G, the
shell's `PageBar` and `BackLink`, which S00.05.01 left to their first user, and the SPA's `limits.ts` with its .NET
test; and they change sign-in.md, totp-setup.md, sign-in-form.md, form-validation.md, five UX sketches' limit names
and the Notes of S00.04.04, S00.04.05, S00.05.01 and S00.05.02. None of them needs the operator: a password setup in
test on a real phone and a laptop, with a real authenticator app, is F00.04's feature check, with sign-in.md's NVDA
and VoiceOver pass, and nothing checks for the parent company's name (D72). It is 17 in the *Plan* and in the list
below, as 15 is S00.04.04's, pulled in the same day on its own branch; at each pull request the numbers are made to
follow what `main` has by then.

**S00.04.05, pulled in on 12 October 2026.** S00.04.04, pulled in earlier that day, has merged (PR #42) and been
deployed to test, with its checks after the deploy to come, as have S00.04.02 (PR #43) and S00.05.01 (PR #41); S00.01.05
(PR #37) awaits its check after the deploy too, S00.04.03 and S00.04.06 were pulled in the same day on their own
branches, and every other story in the sprint is done, so time was left over again. S00.04.05, next in backlog order
after S00.04.04 (releases.md forecast it for Sprint 06), was refined to Ready that day, its tasks were written in its
own section, and it was taken into the sprint at 5 points (its *Notes* say why they stand). They are not added to
`planned_points` either, which stays at 14, and count in `done_points` only if it is done by 18 October; otherwise it
goes back to the backlog (README §7.2 step 4). Its tasks build on S00.04.04's sign-in view, start-up and sign-in
manager, S00.04.02's antiforgery test fixture, setup tests and `useErrorTitle`, S00.04.01's session cookie and `Me`,
S00.03.03's audit writer, S00.05.01's shell and `['me']` query, and S00.01.03 to S00.01.05's shared parts, `api()`
wrapper and copy module. It is built beside S00.04.03, with which it shares the password and code fields, the code check
on both sides and the tests' code helpers, each built once by whichever branch is first, and whichever merges second
adds the password path's second sign-in to the end-to-end setup test (its *Notes*). Its tasks add two anonymous
endpoints, Identity's lockout, its token providers where `main` lacks them and its two-factor cookie's 5 minutes, a
guard in the sign-in manager against a password alone, and the email and password form and the code step to view A. They
make S00.04.02's and S00.04.04's endpoint tests read those 5 minutes from one constant, and change tests/README.md,
sign-in.md and, if this story builds the shared fields and code check before S00.04.03 does, the *Files* lines of
sign-in-form.md and totp-setup.md; if S00.04.03's password setup is on `main` by the last rebase, they extend
`tests/e2e/setup.spec.ts` with its second sign-in and tick S00.04.03's invariant criterion with a Note there, and
otherwise they add a Note to S00.04.03. One step is the operator's, by hand, after the deploy: one sign-in in a desktop
browser with an address that no account holds, which changes nothing; signing in with a password on a real phone and a
laptop in test is F00.04's feature check, with sign-in.md's NVDA and VoiceOver pass. It is 18 in the *Plan* and in the
list below, as 17 is S00.04.03's, pulled in the same day on its own branch; at each pull request the numbers are made to
follow what `main` has by then.

## Definition of Ready check

README §8, used as a reminder, not a gate. All four stories are `ready`, and the five owner questions raised at
planning were answered on 4 October 2026 (below). S00.03.02 and S00.03.03 were checked on 8 October 2026,
S00.03.04, S00.04.01, S06.01.04, S00.03.05, S00.01.03, S00.01.04 and S00.01.05 on 9 October 2026, and S00.04.02,
S00.05.01, S00.04.04, S00.04.06, S00.04.03 and S00.04.05 on 12 October 2026, when they were pulled in.

| Check | S00.02.01 | S00.02.02 | S00.02.03 | S00.02.06 | S00.03.02 (8 October) | S00.03.03 (8 October) | S00.03.04 (9 October) | S00.04.01 (9 October) | S06.01.04 (9 October) | S00.03.05 (9 October) | S00.01.03 (9 October) | S00.01.04 (9 October) | S00.01.05 (9 October) | S00.05.01 (12 October) | S00.04.02 (12 October) | S00.04.04 (12 October) | S00.04.06 (12 October) | S00.04.03 (12 October) | S00.04.05 (12 October) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Type and title; an enabler says what it is for | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| Sources linked; what to build is clear | Yes | Yes | Yes | Yes | Yes, with its Notes from refinement | Yes, with its Notes from refinement | Yes, with its Notes from refinement (the choices where design.md is silent, the test-only sign-in among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the test-only cookie sign-in and HSTS through ASP.NET Core's middleware among them) | Yes, with its Notes from refinement (the choices where design.md is silent, keeping S00.04.01's HSTS and accepting none on the exception handler's responses among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the command's options, the `App:Origin` setting for the link's address and handing the command's first run in test to S00.04.02 among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the secondary button's hover fill, what the contrast and breakpoint checks cover and Vitest processing CSS among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the token fetched with the first change and dropped after sign-in, mutations that never wait while offline, `LoadRegion` keeping its failure through Try again, `PageHeading` built here and no end-to-end test of its own among them) | Yes, with its Notes from refinement (the choices where design.md is silent, `dateTime` exported, ESLint's own `no-restricted-syntax` in place of `react/jsx-no-literals`, which cannot be installed beside ESLint 10, negative contractions in `copy.test.ts`, the D42 check as a .NET test over the repository and the build (withdrawn 12 October 2026, D72), and WebKit through Vitest's browser mode among them) | Yes, with its Notes from refinement (the choices where design.md is silent, React Router 8 in data mode, the signed-out frame until S00.05.03 and S00.04.04 build start-up's messages and sign-in, Page not found built here and marking no nav item, the Account panel's first line, `Sep.tsx` built here, the end-to-end tests answering `/api/auth/me` through `page.route`, and the parts shared with the setup and sign-in stories built once among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the enrolment cookie as a scheme of its own that the setup endpoints read themselves, a refused passkey answered `422 validation.failed` as a wrong setup code is (D70), `apiErrors.ts` starting with this story's one code, only the frame and routes that setup needs, and a test-only passkey handler in the integration tests beside a real passkey end to end among them) | Yes, with its Notes from refinement (the choices where design.md is silent, finding the account by its credential ID, the telemetry count as one log entry, a missing or expired ceremony as a failed sign-in, `PreSignInCheck` on every runtime, the start-up built here and no dependency on S00.04.02 among them) | Yes, with its Notes from refinement (the choices where design.md is silent, signing out this browser only with the security stamp left alone, the event written before the sign-out so that a failed write leaves the session, a `401` counted as done with nothing written, Sign out's leave-guard and autosave steps left to S01.04.02 and S02.02.05, the full reload through a module the Vitest tests replace, HTTPS for CI's end-to-end run through an `X-Forwarded-Proto` header, and the end-to-end tests meeting the app's real `401` among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the key and its QR code from one URI built in one place, the code's check shared with sign-in's code step on both sides, completion shared with the passkey path through one helper, a password of only spaces refused on both sides, the key fetched as view G shows, the password path ending on one history entry, `PageBar` and `BackLink` built here as their first user, and `limits.ts`'s one form among them) | Yes, with its Notes from refinement (the choices where design.md is silent, finding the account by its email, one audit event per attempt from Identity's result, the code step's account read from the two-factor cookie, a badly formed code refused before Identity, the sign-in manager refusing a password alone, Continue waiting for passkey options in flight, view B reached through `page.route` in the end-to-end tests and no dependency on S00.04.03 among them) |
| No open owner question changes it (ux/README.md open questions; screen files; releases.md owner questions) | None open. Q1 (a budget per group, D63), Q2 (no other role holders), Q3 (no paid Defender plan) and Q5 (secrets, D65) answered on 4 October; D60 and D61 settled the subscription and the policy and budget scope | None open. Q4 (the "Grow2Notes SQL admins" group, D64) and Q5 answered. D62 settled the test address, so no domain or certificate is needed | None open. Q5 answered: the deploy reads environment secrets (D65) | None open. Q2 answered: no one else holds a role on the subscription | None open. No open question in ux/README.md or releases.md concerns tenancy; Q6 changes only S00.02.02 | None open. No open question in ux/README.md or releases.md concerns the audit log or forwarded headers; Q6 changes only S00.02.02 | None open. ux/README.md question 10 (per-row ETags for `If-Match` on participants and users) changes the endpoints that send them (E01, F00.06), not the helper; Q6 changes only S00.02.02 | None open. No open question in ux/README.md, sign-in.md, app-shell.md or releases.md concerns the session check: sign-in.md questions 6 and 7 change S00.04.02, S00.04.03 and S00.04.05, not it; Q6 changes only S00.02.02 | None open. No open question in ux/README.md, the screen files or releases.md concerns the headers or the CSP, and the ux/ specs already keep to the strict policy (foundations.md); the `blob:` download checks are S04.02.05's and S05.01.04's; Q6 changes only S00.02.02 | None open. No open question in ux/README.md, sign-in.md, users.md or releases.md concerns the command or the invite core: sign-in.md question 7 changes S00.04.02 and S00.04.03, and ux/README.md questions 6 to 8 change S00.06.02 and S00.06.04 to S00.06.06; the production domain (A38) is needed only in production (S00.02.04), and the link in test uses its default address (D62) | None open. No open question in ux/README.md, app-shell.md, primary-actions.md or releases.md concerns the tokens, `Button` or `PageStatus`: they concern screens, dialogs and their (P) wording. ux/README.md question 5 decides only whether the note form's compact name gets the breakpoint check's one `@container` exception, which the story that builds it adds; sign-in.md question 7 changes S00.04.02 and S00.04.03 | None open. No open question in ux/README.md, app-shell.md, sign-in.md or releases.md concerns the wrapper, the query client or the shared messages: app-shell.md question 2 asks only for its (P) wording (next row); ux/README.md question 10 (per-row ETags) changes the endpoints that send `If-Match`, not the wrapper; sign-in.md questions 6 and 7 change S00.04.02, S00.04.03 and S00.04.05. design.md §7.5's generated API types have no story yet, raised in releases.md's owner questions (its Notes), which changes none of its criteria | None open. No open question in ux/README.md, app-shell.md, microcopy.md or releases.md concerns the formats, the copy checks or the D42 check: app-shell.md question 2 asks only for its (P) wording, which this story does not ship, and microcopy.md records tensions only. The secret that holds the name is the operator's step, not a question (the check and the secret withdrawn 12 October 2026, D72) | None open. app-shell.md question 1, which is ux/README.md question 1, was answered on 12 October 2026: the `/manage` page (D73). app-shell.md questions 3 to 5 change S00.05.03 (when `/me` is refreshed), S00.04.08 (a different person signing in after a timeout) and S00.04.07 with E02 (the warning over a confirmation), not it, and ux/README.md question 9 names only app-shell.md question 3 of the shell's; today.md's questions concern its list, rows and banner (E02), not its heading | None open. sign-in.md questions 1, 6 and 7 are answered (D67, D69, D70); question 5 (a Sign in link on the expired-link page) asks only whether to add a link to a view specified without one, which this story builds as specified, the owner's simplest option; questions 4, 8, 9 and 10 change none of its criteria (autofill and the extras stay out, lockout is S00.04.04's, and the oldest phones and the in-app browsers are the go-live device check, S06.02.02). passkey-flows.md asks nothing beyond those, ux/README.md's open questions concern other screens, and releases.md's questions on sign-in.md Q7 and §7.5 are answered (D70, D71) | None open. sign-in.md question 8 is this story's own test, whose result goes to the owner; questions 4 (passkey autofill and the other extras, left out by default), 5 (the expired-link page, S00.04.02's), 9 (the oldest phones: the prefetch rule works on every WebKit version either way) and 10 (S06.02.02's go-live device check) change nothing it builds; no open question in ux/README.md, app-shell.md or releases.md concerns it | None open. app-shell.md questions 1 and 2 are answered (D73, D67); questions 3 to 5 change S00.05.03 (when `/me` is refreshed), S00.04.08 (a different person signing in after a timeout) and S00.04.07 with E02 (the warning over a confirmation), not it. app-shell-nav.md asks nothing, ux/README.md's open questions concern other screens, and releases.md's questions are answered or concern later stories | None open. sign-in.md questions 1, 2, 3, 6 and 7 are answered (D67 to D70); question 4 asks only whether to add extras that it leaves out, as specified, and question 5 concerns the expired-link page (S00.04.02); questions 8, 9 and 10 change none of its criteria (passkey lockout is S00.04.04's, and the oldest phones and the in-app browsers are the go-live device check, S06.02.02). totp-setup.md and sign-in-form.md ask nothing beyond those, ux/README.md's open questions concern other screens (its question 4 answered by D68), and releases.md's questions on sign-in.md Q7 and §7.5 are answered (D70, D71) | None open. sign-in.md questions 1, 2, 3 and 6 are answered (D67, D68, D69), and question 7 too (D70), which concerns setup only; question 8 was answered for the owner by S00.04.04's test, and what is left of it, whether a failed passkey shows the A25 sentence, changes nothing here; questions 4 (passkey autofill and the other extras, left out by default), 5 (the expired-link page, setup's), 9 (the oldest phones) and 10 (S06.02.02's go-live device check) change nothing it builds; app-shell.md questions 3 to 5 change S00.05.03 (when `/me` is refreshed), S00.04.08 (a different person signing in after a timeout) and S00.04.07 (the warning over a confirmation), not it; ux/README.md question 4 is answered (D68) and its other questions concern other screens; releases.md's question for it, sign-in.md Q6, is answered (D69) |
| Copy marked (P) | None used | None used | None used | None used | None used | None used | None used | None used | None used | None used (the command's output and `ops/break-glass.md` are for the operator) | None used (`Button` and `PageStatus` have no words of their own; primary-actions.md's proposed busy labels ship with their screens) | Ships with the proposed wording: "Try again", "Loading…", the cause words in microcopy.md §9's shapes, "There is a problem" and the "Error: " prefixes; PR #33 records the owner's approval of every (P) string in the UX specs on 9 October 2026. Each screen gives its own words for what loads or fails | None used: `strings.appName` is D42's name, and the formats, with their "midday" and "midnight", are microcopy.md §3's. PR #33, in review, records D67's approval of the (P) strings in the UX specs | Ships with app-shell.md's (P) wording, approved as written by D67: "Account", "Signed in as {name}", "Page not found", "If you typed or pasted the web address, check it is correct." and "Go to Today". The nav items, the Manage links and the Today header are (V), and "Skip to main content" is GOV.UK's | Ships with sign-in.md's proposed wording for views D and E and the setup messages, in the UX specs on 9 October 2026 and approved as written (D67); it adds no (P) string | Ships with the approved wording (D67): the hint, the help line, "Signing in…", the notice for a browser without passkeys and the "Not signed in: …" failures are sign-in.md's (P) strings of 9 October 2026; "Sign in", "Sign in with a passkey" and the A25 message are design.md's | Ships with app-shell.md's (P) wording, approved as written by D67: "Signing out…", "Not signed out: no connection. Try again." and "Not signed out: something went wrong. Try again."; "Sign out" is (V). The third "Not signed out…" sentence, for a note not yet saved, ships with S02.02.05 | Ships with sign-in.md's proposed wording for views F and G, the authenticator steps that name Google Authenticator and Microsoft Authenticator among it, in the UX specs on 9 October 2026 and approved as written (D67); it adds no (P) string, and no Copy setup key button (D68) | Ships with the approved wording (D67): the form's and the code step's words are sign-in.md's of 9 October 2026, its (P) strings among them (the empty-field and email-format messages, "Checking…", the code step's label, hint and format message, "Signing in…", the help line and the "Not signed in: …" failures), with the labels and "Continue" that it took from sign-in-form.md and microcopy.md; "Sign in" and the A25 message are design.md's; it adds no (P) string |
| Acceptance criteria written and testable | Yes (budget criterion updated for D61 and D63) | Yes (HTTPS criterion updated for D62) | Yes | Yes | Yes (linked §5.9 and §9.2 groups narrowed to what it builds) | Yes (linked §5.8 group narrowed to what it builds; mcp-server.md §3.5 convention 3 linked) | Yes (linked §2, §9.1, §9.2 and §9.8 groups narrowed to what it builds) | Yes (linked §14 M0, §8.4 and §9.3 groups narrowed to what it builds; §7.2 and §9.8 item 1 linked; a criterion added for a changed security stamp, coverage.md's M0 *Done when* row 2) | Yes (linked §9.7 and §7.2 groups narrowed to what it builds; the first criterion's list brought up to date with the kinds S00.03.04 and S00.04.01 added) | Yes (linked §7.4 and §8.1 groups narrowed to what it builds; §8.4's `RequireUniqueEmail` and setup token provider linked; the first and third criteria's checks in test handed to S00.04.02) | Yes (linked ux/README.md rows and foundations.md and primary-actions.md groups narrowed to what it builds; app-shell.md component 3 linked; the first criterion names foundations.md's two lower ratios, and the second its whole breakpoint check) | Yes (linked ux/README.md rows and empty-loading-error.md and form-validation.md groups narrowed to what it builds; primary-actions.md's alert and microcopy.md's failure wording linked; the first criterion adds that nothing waits while the browser reports offline, the third leaves the summary's place in `<main>` to each form and adds the item with no link, the fourth names the fresh token after sign-in and after a `400`, the fifth adds `/api/auth/passkey*`, and a sixth covers `If-Match`) | Yes (linked microcopy.md §3 and §8 groups and ux/README.md rows narrowed to what it builds; the first criterion names what CI searches and its failure on a missing secret, the second how the email template, passkey domain and issuer are covered now (both withdrawn 12 October 2026, D72), and a fourth adds negative contractions to `copy.test.ts`) | Yes (linked design.md §4.0 navigation, app-shell.md Structure group and components 2, 3, 3a and 4 narrowed to what it builds; today.md component 3 linked; the first criterion names the links' addresses and all four links, the second says Page not found marks no nav item, the third adds the spoken comma and the title, the fourth says who checks the setup paths' landing, the fifth where the list lives, and a sixth types `role`, as S00.04.01's Note asks) | Yes (the linked §4.1, §8.1 and sign-in.md *Setup* bullets kept; `/setup/password` and `/setup/app` built here for the no-link bullet only, their views being S00.04.03's; the passkey path's second sign-in, which needs S00.04.04's endpoint, handed to it unless that is on `main` first) | Yes (linked sign-in.md Sign in bullets narrowed to what it builds, the three that need the password form now S00.04.05's; sign-in.md's components, states and steps for the passkey button, §6.2's two rows and §8.4's passkey options linked; the first criterion names the environment's domain, the third says no session starts, and a fourth covers a credential sent with no ceremony underway) | Yes (§9.6 narrowed to the sign-out half of its fourth bullet; the Account and Sign out bullets' autosave and Guide prompts parts left to S02.02.05 and S01.04.02; component 2d's panel contents and the *States* and *What happens on each action* rows for Sign out linked; the criterion split in two, the first saying what ending the session means, what a `401` gets and what the event holds, the second the full reload with `replace`) | Yes (the linked §4.1, §8.1 and sign-in.md *Setup* and *Sign in* bullets kept; the *Setup* bullets on a `401` and on a page with nothing in memory, the *Privacy and storage* and *Layout and accessibility* bullets for views F and G, and app-shell.md component 3a added; the password path's second sign-in, which needs S00.04.05's endpoints, handed to it unless they are on `main` first, and the invariant criterion saying who ticks it) | Yes (linked sign-in.md Sign in bullets listed: every one not in S00.04.04 or S00.04.03, with the password halves of four of S00.04.04's; sign-in.md's components, states and steps for the form and the code step, and its Privacy and storage and Layout and accessibility bullets for views A and B, linked; §6.2's two rows, §8's code step and lockout rows with §8.4's settings, and §9.9's lockout sentence linked; the first criterion names the account found by its email and the one `401`, the third counts passwords and codes together, and two added: the code step's 5 minutes (D69) and no password alone (§8.1)) |
| Sized at 1–5 points | 3 | 5 | 3 | 3 | 3 | 2 | 5 (re-sized from 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 2 (stays at 2; its Notes say why) | 3 (stays at 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 5 (stays at 5; its Notes say why) | 5 (re-sized from 3; its Notes say why) | 2 (stays at 2; its Notes say why) | 5 (stays at 5; its Notes say why) | 5 (stays at 5, the 5-point reference itself; its Notes say why) |
| `depends_on` done or earlier in this sprint | None | None (uses S00.02.01's groups and identities, earlier in this sprint) | S00.01.02: done; S00.02.02: earlier in this sprint | None | None (extends S00.03.01's context, done in Sprint 01) | S00.03.02: earlier in this sprint (in review, PR #21) | S00.03.02: done (closed in PR #24); S00.03.03: earlier in this sprint (in review, PR #24) | S00.03.02: done; S00.03.04: earlier in this sprint (in review, PR #26), whose code its tasks build on | S00.01.02: done; S00.03.04: done (PR #26, its deploy recorded in PR #27); S00.04.01: earlier in this sprint (merged in PR #28, its after-deploy check to come), whose HSTS, session cookie and `/api/auth/me` its tasks build on | S00.03.01: done (Sprint 01); S00.03.02 and S00.03.03: done, whose tenant `Use` block and audit writer its tasks build on | S00.01.01, S00.01.02 and S06.01.04: done, whose SPA project, CI's SPA checks, strict policy, Playwright fixture and `SpaBuildTests` its tasks build on | S00.01.01, S00.01.02 and S00.03.04: done; S00.01.03: earlier in this sprint (merged in PR #35, its after-deploy check to come), whose `Button`, `ButtonGroup`, `PageStatus` and tokens its tasks build on | S00.01.01 and S00.01.02: done; S00.01.04: earlier in this sprint (merged in PR #36, its after-deploy check to come), whose `src/copy` modules its checks read | S00.01.03, S00.01.04 and S00.04.01: done, whose `PageStatus`, `Button`, tokens, `api()`, query client, `PageHeading` and `/api/auth/me` its tasks build on; S00.01.05: earlier in this sprint (merged in PR #37, its after-deploy check to come), whose `src/copy`, `dateToday` and lint rule they use | S00.03.05, S00.04.01 and S00.01.04: done; S00.01.05: earlier in this sprint (merged in PR #37, its after-deploy check to come), whose setup token, invite core, `admin bootstrap`, sign-in manager, session cookie, `Me`, `api()` wrapper, shared parts and copy module its tasks build on | S00.04.01, S00.03.03, S00.01.03 and S00.01.04: done, whose sign-in manager, `Me`, session cookie, audit writer, `Button`, `PageStatus` and `api()` wrapper its tasks build on; it needs neither S00.04.02 nor S00.05.01, and reuses what either has added by then (its Notes) | S00.01.03, S00.01.04, S00.03.03, S00.03.04 and S00.04.01: done, whose `Button`, `api()`, audit writer, `/api` group, endpoint matrix, sign-in manager, session cookie and test-only cookie sign-in its tasks build on; S00.01.05 and S00.05.01: earlier in this sprint (merged in PRs #37 and #41, their after-deploy checks to come), whose `src/copy` and Account panel they use | S00.03.05, S00.04.01 and S00.01.04: done; S00.04.02, S00.05.01 and S00.01.05: earlier in this sprint (merged in PRs #43, #41 and #37, their checks after the deploy to come), whose enrolment cookie, completion, setup layout, shell, `['me']` query and copy module, with S00.03.05's key stored at the invite and S00.01.04's `api()` wrapper and form parts, its tasks build on | S00.04.01, S00.03.03, S00.01.03 and S00.01.04: done; S00.04.02, S00.04.04, S00.05.01 and S00.01.05: earlier in this sprint (merged in PRs #43, #42, #41 and #37, their checks after the deploy to come), whose antiforgery test fixture, setup tests, `useErrorTitle`, sign-in view, start-up, sign-in manager, `Me` helper, audit writer, shell, `['me']` query, form parts, `api()` wrapper and copy module its tasks build on; it does not need S00.04.03, built beside it, and reuses what that has added by then (its Notes) |
| Made-up test data known (A35) | None needed | None needed | None needed | A made-up canary string | Two made-up organisations, each with a manager and a worker (`example.org` addresses) | The seeded organisations (S00.03.02), and client addresses from the documentation ranges (RFC 5737) | The seeded organisations' users (S00.03.02), signed in by the test-only scheme; nothing of its own | The seeded organisations' users (S00.03.02), and users each test adds to organisation A for the role and stamp changes (`example.org` addresses) | The seeded organisations' users (S00.03.02), signed in by the test-only scheme for the JSON `200` and the `403`; nothing of its own | Organisations, first managers and users that each test adds, with made-up names and an `example.org` address of its own (A35); no seeded row changes. Nothing in test: the command's first run there is S00.04.02's | None needed: made-up button labels in its tests; nothing in test | None needed: made-up things, fields and labels, and stubbed answers, in its tests; nothing in test | A made-up company name in the D42 check's planted samples, and made-up dates and times; the real name only in the CI secret (the check and the secret withdrawn 12 October 2026, D72); nothing in test | A made-up worker and manager, with a made-up organisation, in the answers of `/api/auth/me` that its Vitest tests and its end-to-end tests' `page.route` give (A35); nothing in test | Users that each test invites into organisation A through the invite core, with made-up names and an `example.org` address of its own (A35), and B's manager for the tenant test; in the end-to-end run, an organisation and first manager that each test bootstraps, with a new `example.org` address; in test, the operator's one made-up organisation and first manager, with an `example.org` address, which stay there for good | Users each test adds to organisation A, and one to B, with `example.org` addresses and passkeys from the tests' own keys, so no seeded user is failed or locked (tests/README.md); in the end-to-end test, a passkey made up through CDP that no account holds; nothing in test | Users that each integration test adds to organisation A (`example.org` addresses), so that each finds its own `auth.signed_out`, and the seeded users in the matrix; the made-up worker and manager of `tests/e2e/me.ts` (A35), and, for a real sign-in in the end-to-end run, the organisation and manager that its fixture bootstraps; nothing in test | Users that each test invites into organisation A through the invite core, with made-up names, an `example.org` address and a made-up password of their own, and codes computed from each user's stored key (A35); B's manager for the tenant test; in the end-to-end run, an organisation and first manager that each test bootstraps, with a new `example.org` address; nothing in test, where the check after the deploy changes nothing | Users each test adds to organisation A, and one to B, with `example.org` addresses, a made-up password and an authenticator key that Identity makes, and codes computed from that key, so no seeded user is failed or locked (tests/README.md); in the end-to-end test, a made-up `example.org` address that no account holds, and the password step answered through `page.route`; in test, the operator's one made-up address that no account holds, which changes nothing |

## Owner questions

Also in releases.md's owner questions table, needed by Sprint 02, and in the affected stories' *Notes*. Q1 to Q5 were
raised at planning and answered on 4 October 2026. Q6 was raised on 7 October 2026 by the checks after S00.02.03's
first deploy (S00.02.02's *Notes*) and answered on 9 October 2026.

| | Question | Answer | Stories |
|---|---|---|---|
| Q1 | The budget's shape (D61): one AUD 100 budget on the subscription with a filter for the two resource groups (proposed), or a budget on each group, splitting the AUD 100? | **Answered:** one budget inside each resource group, AUD 30 a month for test and AUD 70 a month for prod (AUD 100 in total), each emailing the developer at an address passed in when bootstrap is run. The subscription-scope filtered budget is dropped (D63) | S00.02.01 |
| Q2 | Who else holds roles on the shared subscription (D60)? Any Reader, Contributor or Owner role there can read the Grow2Notes resources and query the telemetry workspace, against design.md §9.5's "read access limited to the developer": accept and record them, or change them? | **Answered** by inspecting the subscription: the only role holder at subscription scope is the developer's own account (Owner). No change needed | S00.02.01, S00.02.06 |
| Q3 | Is any paid Defender for Cloud plan on in the shared subscription? It would cover, and bill for, the Grow2Notes resources too, against design.md §10.1 and §10.2 (no paid Defender plan): accept that and its cost, or turn it off for the Grow2Notes resources where the plan allows? | **Answered** by inspecting the subscription: the App Service, SQL, Key Vault and all other workload plans are on the Free tier; only Foundational CSPM (free) and Discovery show Standard, and neither covers or bills the Grow2Notes resources. No change needed | S00.02.01 |
| Q4 | The SQL Entra admin group (design.md §9.4): a new security group in the operator's Entra tenant holding only the developer's account, or an existing group? Its name must not name the parent company (D42) | **Answered:** a new Entra security group, "Grow2Notes SQL admins", with the developer as its only member, used for both test and prod (D64) | S00.02.02 |
| Q5 | The repository's Actions logs are public, and GitHub prints environment variables in them but masks secrets. Keep the deployment identity's client ID, the tenant and subscription IDs, the SQL admin group and the alert email address as `test` environment secrets (recommended), or as variables as D60 says, accepting that they appear in the run logs? | **Answered:** environment secrets on the GitHub `test` environment (later `prod`), masked in the public logs; the workflows read `secrets.*`, not `vars.*` (D65, amending D60) | S00.02.01, S00.02.02, S00.02.03 |
| Q6 | S00.02.02's database checks (the app identity in `grow2notes_runtime` with no DDL right; the stored key wrapped by Key Vault and still the only key after a restart) need a SQL connection from outside the VNet. Approve a temporary firewall rule for the operator's address on `sql-grow2notes-test`, removed straight after (as for `grant-identities.sql`), to run `infra/sql/check-database.sql`, and one restart of `app-grow2notes-test` between two runs of it? | **Answered 9 October 2026:** approved; the checks ran before and after one restart (three runs, as the restart took effect only after the second), through temporary firewall rules for the operator's address, removed straight after: 34 PASS and 0 FAIL each time, and the key ring kept its one key, wrapped by Key Vault (S00.02.02's *Notes*) | S00.02.02 |

## Tasks

Each story's tasks are written once, in its own section, and ticked there (README §6). Tasks marked
**(operator, by hand)** need Owner or Entra admin rights and are done by the operator, not by a workflow.

1. [S00.02.01 Bootstrap Azure for both environments](../E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000201-bootstrap-azure-for-both-environments),
   in F00.02.
2. [S00.02.02 Test environment resources](../E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000202-test-environment-resources),
   in F00.02.
3. [S00.02.03 Deploy to test from main](../E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000203-deploy-to-test-from-main),
   in F00.02.
4. [S00.02.06 Server telemetry with IDs only](../E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000206-server-telemetry-with-ids-only),
   in F00.02.
5. [S00.03.02 Tenant isolation and its tests](../E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000302-tenant-isolation-and-its-tests),
   in F00.03, pulled in on 8 October 2026.
6. [S00.03.03 Audit writer](../E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000303-audit-writer),
   in F00.03, pulled in on 8 October 2026 after S00.03.02.
7. [S00.03.04 Deny by default, CSRF and the endpoint matrix](../E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000304-deny-by-default-csrf-and-the-endpoint-matrix),
   in F00.03, pulled in on 9 October 2026 after S00.03.03.
8. [S00.04.01 Session checked on every request](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000401-session-checked-on-every-request),
   in F00.04, pulled in on 9 October 2026 after S00.03.04.
9. [S06.01.04 Security headers and CSP on every response](../E06-hardening-and-go-live/F06.01-security-review-hardening.md#s060104-security-headers-and-csp-on-every-response),
   in F06.01, pulled in on 9 October 2026 after S00.04.01.
10. [S00.03.05 Bootstrap the organisation and first manager](../E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000305-bootstrap-the-organisation-and-first-manager),
    in F00.03, pulled in on 9 October 2026 after S06.01.04.
11. [S00.01.03 Shared look, buttons and page status](../E00-skeleton-hosting-sign-in/F00.01-skeleton-and-continuous-integration.md#s000103-shared-look-buttons-and-page-status),
    in F00.01, pulled in on 9 October 2026 after S00.03.05.
12. [S00.01.04 Shared loading, failure and form-error handling](../E00-skeleton-hosting-sign-in/F00.01-skeleton-and-continuous-integration.md#s000104-shared-loading-failure-and-form-error-handling),
    in F00.01, pulled in on 9 October 2026 after S00.01.03.
13. [S00.01.05 Copy module, date formats and wording checks](../E00-skeleton-hosting-sign-in/F00.01-skeleton-and-continuous-integration.md#s000105-copy-module-date-formats-and-wording-checks),
    in F00.01, pulled in on 9 October 2026 after S00.01.04.
14. [S00.04.02 Set up my account with a passkey](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000402-set-up-my-account-with-a-passkey),
    in F00.04, pulled in on 12 October 2026 after S00.01.05.
15. [S00.04.04 Sign in with a passkey](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000404-sign-in-with-a-passkey),
    in F00.04, pulled in on 12 October 2026 after S00.01.05.
16. [S00.05.01 Navigation for my role](../E00-skeleton-hosting-sign-in/F00.05-app-shell.md#s000501-navigation-for-my-role),
    in F00.05, pulled in on 12 October 2026 after S00.01.05.
17. [S00.04.03 Set up my account with a password and authenticator app](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000403-set-up-my-account-with-a-password-and-authenticator-app),
    in F00.04, pulled in on 12 October 2026 after S00.04.02.
18. [S00.04.05 Sign in with email, password and code](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000405-sign-in-with-email-password-and-code),
    in F00.04, pulled in on 12 October 2026 after S00.04.04.
19. [S00.04.06 Sign out](../E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000406-sign-out),
    in F00.04, pulled in on 12 October 2026 after S00.05.01.

## Settled at planning

Recorded in [decisions.md](../../decisions.md) (D60–D65), in design.md (its opening sections, §8.4, §9.3, §9.4, §10.1,
§10.2, §10.3, §10.5 and §10.6), and in the stories' *Notes*.

- **Hosting in the operator's subscription (D60).** Test and production go into the operator's existing Azure
  subscription, which also runs its other workloads. The subscription and tenant IDs, and the deployment identity's
  client ID, live only in the GitHub `test` environment (later `prod`), never in a file: the repository is public.
  Values that could name the parent company, such as alert email addresses, stay out of it too (D42). The Actions
  logs are public as well, and GitHub masks secrets in them, so these values are kept as environment secrets, not
  variables, and the workflows read them as `secrets.*` (Q5, D65, amending D60).
- **Policy and budget on the two resource groups (D61, D63).** *Allowed locations* is assigned to each Grow2Notes
  resource group, not the subscription. Each group has its own budget alert inside it, AUD 30 a month for test and
  AUD 70 a month for prod (AUD 100 in total), emailing the developer (Q1, D63). A budget created inside a resource
  group covers only that group, so neither counts the operator's other workloads.
- **No other role holders, and no paid Defender plan (Q2, Q3).** Inspecting the subscription on 4 October found that
  the developer's own account (Owner) is the only role holder at subscription scope, and that the App Service, SQL,
  Key Vault and all other workload Defender plans are on the Free tier; only Foundational CSPM (free) and Discovery
  show Standard, and neither covers or bills the Grow2Notes resources. Nothing changes; S00.02.01's check task
  confirms it.
- **SQL admin group (D64).** A new Entra security group, "Grow2Notes SQL admins", with the developer as its only
  member, is the SQL Entra admin for both test and prod (Q4).
- **Test address (D62).** Test uses its default `*.azurewebsites.net` address with App Service's built-in HTTPS. There
  is no custom test domain or managed certificate for now. Test passkeys are bound to that address (design.md §8.4), so
  if a custom test domain is added later, test accounts enrol their passkeys again.
- **Costs.** From S00.02.02's first deploy, test costs about AUD 20 a month ex GST (design.md §10.2: the B1 plan about
  AUD 19.27, the free-offer database AUD 0 within its monthly allowance, and Key Vault, email and telemetry within free
  allowances or under a dollar). The production resource group holds only S00.02.01's free identities and assignments,
  and costs nothing, until S00.02.04 (Sprint 09). The test group's AUD 30 a month budget alert (D63) watches test's
  cost with room to spare. The Azure SQL free offer is limited per subscription and the subscription is shared, so
  S00.02.02 checks that a free database is still available before the AUD 0 is relied on.
- **Owner-only steps are done by hand.** The operator runs `bootstrap.bicep` as Owner of the subscription, creates the
  GitHub `test` environment and its values as secrets (D65), creates the "Grow2Notes SQL admins" group (D64), and runs
  `grant-identities.sql` as the Entra admin. Each is marked "(operator, by hand)" in the tasks. The pipeline's
  identities never get these rights (design.md §10.5).
- **The bootstrap exception continues** (releases.md, assumption 5). S00.02.01 and S00.02.02 are merged before
  `deploy.yml` exists; their checks in test follow S00.02.03's first deploy, and they are done then. That deploy also
  completes Sprint 01's two Azure parts (S00.03.01's run on Azure SQL and S00.01.02's built Bicep artifact) and checks
  Sprint 01's stories in test.
- **Deploy smoke test.** `/api/auth/antiforgery` (design.md §10.4 step 8) arrives with S00.03.04 in Sprint 03, so
  S00.02.03 smoke-tests `/healthz` and S00.03.04 adds the antiforgery check (noted in both stories).

## Result

Real start: Monday 5 October 2026, the first Monday after planning on Sunday 4 October (PR #13, merged that evening),
keeping releases.md assumption 1's Monday-to-Sunday sprints; the sprint's first story was merged that day (S00.02.01,
PR #15). So the sprint ends on Sunday 18 October 2026 (D53) instead of the forecast's 26 October to 8 November.
Sprint 01 keeps its forecast dates in sprint-01.md, and both sprints' dates are settled at Sprint 02's review
re-forecast.

Pulled in: S00.03.02 (3 points) on 8 October 2026 (Plan).
Pulled in: S00.03.03 (2 points) on 8 October 2026 (Plan).
Pulled in: S00.03.04 (5 points) on 9 October 2026 (Plan).
Pulled in: S00.04.01 (3 points) on 9 October 2026 (Plan).
Pulled in: S06.01.04 (2 points) on 9 October 2026 (Plan).
Pulled in: S00.03.05 (3 points) on 9 October 2026 (Plan).
Pulled in: S00.01.03 (3 points) on 9 October 2026 (Plan).
Pulled in: S00.01.04 (3 points) on 9 October 2026 (Plan).
Pulled in: S00.01.05 (3 points) on 9 October 2026 (Plan).
Pulled in: S00.04.02 (5 points) on 12 October 2026 (Plan).
Pulled in: S00.05.01 (3 points) on 12 October 2026 (Plan).
Pulled in: S00.04.04 (5 points) on 12 October 2026 (Plan).
Pulled in: S00.04.06 (2 points) on 12 October 2026 (Plan).
Pulled in: S00.04.03 (5 points) on 12 October 2026 (Plan).
Pulled in: S00.04.05 (5 points) on 12 October 2026 (Plan).

Done: __ points, __ stories. Not done (back to the backlog): …

Unplanned work: what it was, and roughly how many hours, so the next sprint's capacity allows for it.

## Review

Date, who came, what they tried. Feedback becomes new items (give their IDs) or questions for decisions.md.

## Retro

Keep: … Change: …
