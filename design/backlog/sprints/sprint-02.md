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
| | | **Total** | | **14** |

These are the next four items in the backlog. S00.02.02 needs S00.02.01's resource groups and identities; S00.02.03
needs S00.02.02's database and web app; S00.02.06 adds `monitoring.bicep` to `main.bicep` and is checked in test after
S00.02.03's first deploy. Together they meet the Definition of Done's "Deployed to the test environment" line for this
and every later story. If time is left over, take the next story in backlog order (S00.03.02) once it is Ready.

## Definition of Ready check

README §8, used as a reminder, not a gate. All four stories are `ready`, and the five owner questions raised at
planning were answered on 4 October 2026 (below).

| Check | S00.02.01 | S00.02.02 | S00.02.03 | S00.02.06 |
|---|---|---|---|---|
| Type and title; an enabler says what it is for | Yes | Yes | Yes | Yes |
| Sources linked; what to build is clear | Yes | Yes | Yes | Yes |
| No open owner question changes it (ux/README.md open questions; screen files; releases.md owner questions) | None open. Q1 (a budget per group, D63), Q2 (no other role holders), Q3 (no paid Defender plan) and Q5 (secrets, D65) answered on 4 October; D60 and D61 settled the subscription and the policy and budget scope | None open. Q4 (the "Grow2Notes SQL admins" group, D64) and Q5 answered. D62 settled the test address, so no domain or certificate is needed | None open. Q5 answered: the deploy reads environment secrets (D65) | None open. Q2 answered: no one else holds a role on the subscription |
| Copy marked (P) | None used | None used | None used | None used |
| Acceptance criteria written and testable | Yes (budget criterion updated for D61 and D63) | Yes (HTTPS criterion updated for D62) | Yes | Yes |
| Sized at 1–5 points | 3 | 5 | 3 | 3 |
| `depends_on` done or earlier in this sprint | None | None (uses S00.02.01's groups and identities, earlier in this sprint) | S00.01.02: done; S00.02.02: earlier in this sprint | None |
| Made-up test data known (A35) | None needed | None needed | None needed | A made-up canary string |

## Owner questions

Also in releases.md's owner questions table, needed by Sprint 02, and in the affected stories' *Notes*. Q1 to Q5 were
raised at planning and answered on 4 October 2026. Q6 was raised on 7 October 2026 by the checks after S00.02.03's
first deploy (S00.02.02's *Notes*) and is open.

| | Question | Answer | Stories |
|---|---|---|---|
| Q1 | The budget's shape (D61): one AUD 100 budget on the subscription with a filter for the two resource groups (proposed), or a budget on each group, splitting the AUD 100? | **Answered:** one budget inside each resource group, AUD 30 a month for test and AUD 70 a month for prod (AUD 100 in total), each emailing the developer at an address passed in when bootstrap is run. The subscription-scope filtered budget is dropped (D63) | S00.02.01 |
| Q2 | Who else holds roles on the shared subscription (D60)? Any Reader, Contributor or Owner role there can read the Grow2Notes resources and query the telemetry workspace, against design.md §9.5's "read access limited to the developer": accept and record them, or change them? | **Answered** by inspecting the subscription: the only role holder at subscription scope is the developer's own account (Owner). No change needed | S00.02.01, S00.02.06 |
| Q3 | Is any paid Defender for Cloud plan on in the shared subscription? It would cover, and bill for, the Grow2Notes resources too, against design.md §10.1 and §10.2 (no paid Defender plan): accept that and its cost, or turn it off for the Grow2Notes resources where the plan allows? | **Answered** by inspecting the subscription: the App Service, SQL, Key Vault and all other workload plans are on the Free tier; only Foundational CSPM (free) and Discovery show Standard, and neither covers or bills the Grow2Notes resources. No change needed | S00.02.01 |
| Q4 | The SQL Entra admin group (design.md §9.4): a new security group in the operator's Entra tenant holding only the developer's account, or an existing group? Its name must not name the parent company (D42) | **Answered:** a new Entra security group, "Grow2Notes SQL admins", with the developer as its only member, used for both test and prod (D64) | S00.02.02 |
| Q5 | The repository's Actions logs are public, and GitHub prints environment variables in them but masks secrets. Keep the deployment identity's client ID, the tenant and subscription IDs, the SQL admin group and the alert email address as `test` environment secrets (recommended), or as variables as D60 says, accepting that they appear in the run logs? | **Answered:** environment secrets on the GitHub `test` environment (later `prod`), masked in the public logs; the workflows read `secrets.*`, not `vars.*` (D65, amending D60) | S00.02.01, S00.02.02, S00.02.03 |
| Q6 | S00.02.02's database checks (the app identity in `grow2notes_runtime` with no DDL right; the stored key wrapped by Key Vault and still the only key after a restart) need a SQL connection from outside the VNet. Approve a temporary firewall rule for the operator's address on `sql-grow2notes-test`, removed straight after (as for `grant-identities.sql`), to run `infra/sql/check-database.sql`, and one restart of `app-grow2notes-test` between two runs of it? | **Open** | S00.02.02 |

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

Done: __ points, __ stories. Not done (back to the backlog): …

Unplanned work: what it was, and roughly how many hours, so the next sprint's capacity allows for it.

## Review

Date, who came, what they tried. Feedback becomes new items (give their IDs) or questions for decisions.md.

## Retro

Keep: … Change: …
