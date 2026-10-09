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

## Definition of Ready check

README §8, used as a reminder, not a gate. All four stories are `ready`, and the five owner questions raised at
planning were answered on 4 October 2026 (below). S00.03.02 and S00.03.03 were checked on 8 October 2026, and
S00.03.04, S00.04.01 and S06.01.04 on 9 October 2026, when they were pulled in.

| Check | S00.02.01 | S00.02.02 | S00.02.03 | S00.02.06 | S00.03.02 (8 October) | S00.03.03 (8 October) | S00.03.04 (9 October) | S00.04.01 (9 October) | S06.01.04 (9 October) |
|---|---|---|---|---|---|---|---|---|---|
| Type and title; an enabler says what it is for | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| Sources linked; what to build is clear | Yes | Yes | Yes | Yes | Yes, with its Notes from refinement | Yes, with its Notes from refinement | Yes, with its Notes from refinement (the choices where design.md is silent, the test-only sign-in among them) | Yes, with its Notes from refinement (the choices where design.md is silent, the test-only cookie sign-in and HSTS through ASP.NET Core's middleware among them) | Yes, with its Notes from refinement (the choices where design.md is silent, keeping S00.04.01's HSTS and accepting none on the exception handler's responses among them) |
| No open owner question changes it (ux/README.md open questions; screen files; releases.md owner questions) | None open. Q1 (a budget per group, D63), Q2 (no other role holders), Q3 (no paid Defender plan) and Q5 (secrets, D65) answered on 4 October; D60 and D61 settled the subscription and the policy and budget scope | None open. Q4 (the "Grow2Notes SQL admins" group, D64) and Q5 answered. D62 settled the test address, so no domain or certificate is needed | None open. Q5 answered: the deploy reads environment secrets (D65) | None open. Q2 answered: no one else holds a role on the subscription | None open. No open question in ux/README.md or releases.md concerns tenancy; Q6 changes only S00.02.02 | None open. No open question in ux/README.md or releases.md concerns the audit log or forwarded headers; Q6 changes only S00.02.02 | None open. ux/README.md question 10 (per-row ETags for `If-Match` on participants and users) changes the endpoints that send them (E01, F00.06), not the helper; Q6 changes only S00.02.02 | None open. No open question in ux/README.md, sign-in.md, app-shell.md or releases.md concerns the session check: sign-in.md questions 6 and 7 change S00.04.02, S00.04.03 and S00.04.05, not it; Q6 changes only S00.02.02 | None open. No open question in ux/README.md, the screen files or releases.md concerns the headers or the CSP, and the ux/ specs already keep to the strict policy (foundations.md); the `blob:` download checks are S04.02.05's and S05.01.04's; Q6 changes only S00.02.02 |
| Copy marked (P) | None used | None used | None used | None used | None used | None used | None used | None used | None used |
| Acceptance criteria written and testable | Yes (budget criterion updated for D61 and D63) | Yes (HTTPS criterion updated for D62) | Yes | Yes | Yes (linked §5.9 and §9.2 groups narrowed to what it builds) | Yes (linked §5.8 group narrowed to what it builds; mcp-server.md §3.5 convention 3 linked) | Yes (linked §2, §9.1, §9.2 and §9.8 groups narrowed to what it builds) | Yes (linked §14 M0, §8.4 and §9.3 groups narrowed to what it builds; §7.2 and §9.8 item 1 linked; a criterion added for a changed security stamp, coverage.md's M0 *Done when* row 2) | Yes (linked §9.7 and §7.2 groups narrowed to what it builds; the first criterion's list brought up to date with the kinds S00.03.04 and S00.04.01 added) |
| Sized at 1–5 points | 3 | 5 | 3 | 3 | 3 | 2 | 5 (re-sized from 3; its Notes say why) | 3 (stays at 3; its Notes say why) | 2 (stays at 2; its Notes say why) |
| `depends_on` done or earlier in this sprint | None | None (uses S00.02.01's groups and identities, earlier in this sprint) | S00.01.02: done; S00.02.02: earlier in this sprint | None | None (extends S00.03.01's context, done in Sprint 01) | S00.03.02: earlier in this sprint (in review, PR #21) | S00.03.02: done (closed in PR #24); S00.03.03: earlier in this sprint (in review, PR #24) | S00.03.02: done; S00.03.04: earlier in this sprint (in review, PR #26), whose code its tasks build on | S00.01.02: done; S00.03.04: done (PR #26, its deploy recorded in PR #27); S00.04.01: earlier in this sprint (merged in PR #28, its after-deploy check to come), whose HSTS, session cookie and `/api/auth/me` its tasks build on |
| Made-up test data known (A35) | None needed | None needed | None needed | A made-up canary string | Two made-up organisations, each with a manager and a worker (`example.org` addresses) | The seeded organisations (S00.03.02), and client addresses from the documentation ranges (RFC 5737) | The seeded organisations' users (S00.03.02), signed in by the test-only scheme; nothing of its own | The seeded organisations' users (S00.03.02), and users each test adds to organisation A for the role and stamp changes (`example.org` addresses) | The seeded organisations' users (S00.03.02), signed in by the test-only scheme for the JSON `200` and the `403`; nothing of its own |

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

Done: __ points, __ stories. Not done (back to the backlog): …

Unplanned work: what it was, and roughly how many hours, so the next sprint's capacity allows for it.

## Review

Date, who came, what they tried. Feedback becomes new items (give their IDs) or questions for decisions.md.

## Retro

Keep: … Change: …
