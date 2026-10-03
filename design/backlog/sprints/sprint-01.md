---
sprint: 1
release: R1
start: 2026-10-12
end: 2026-10-25
capacity_hours: 29
planned_points: 13
done_points:
done_stories:
---
# Sprint 01

**Goal:** Every change to Grow2Notes is built and tested automatically before it is accepted, starting with the empty
app and its database.

## Capacity

| | Hours |
|---|---|
| Build time: 2 weeks × about 20 hours (D53, D54) | 40 |
| Less sprint events: planning 1, review 1, retro 0.5, refinement 1.5 | −4 |
| Less a 20% buffer for the unknown | −7 |
| Less leave and known commitments | 0 (none known on 4 October; check at planning on 12 October) |
| **Focused story time** | **29** |

Planned by hours, as README §7.2 says for the first sprint: Ready stories were taken in backlog order
([releases.md](../releases.md)) and each one's tasks were given a rough hour guess. The guesses reached about 29 hours
after the third story, so no fourth story was taken. As the method says, the guesses are not kept and will never be
compared with actuals. The 13 points are the sprint 1 forecast, not a velocity.

## Plan

| Order | Story | Title | Type | Points |
|---|---|---|---|---|
| 1 | S00.01.01 | Serve the SPA and the API from one origin | enabler | 3 |
| 2 | S00.03.01 | Database context, conventions and first migration | enabler | 5 |
| 3 | S00.01.02 | Build and test every pull request | enabler | 5 |
| | | **Total** | | **13** |

These are the top three items in the backlog. Together they give every later story one deployable app, a database
model whose rules the database also enforces, and the automatic "CI is green" check in the Definition of Done.
S00.01.02 comes last: its `efbundle`, `migrate.sql` and pending-model-changes steps need S00.03.01's first migration.
If time is left over, take the next Ready story in backlog order (S00.02.01).

## Definition of Ready check

README §8, used as a reminder. All three stories are `ready`. One owner question is still open for S00.01.02 and is
settled at planning on 12 October (releases.md, owner questions).

| Check | S00.01.01 | S00.03.01 | S00.01.02 |
|---|---|---|---|
| Type and title; an enabler says what it is for | Yes | Yes | Yes |
| Sources linked; what to build is clear | Yes | Yes | Yes |
| No open owner question changes it (ux/README.md open questions; screen files; releases.md owner questions) | None applies | None applies | Open: is the repository public or private, and if private, is GitHub Pro taken (design.md §10.2)? GitHub Free cannot protect `main` on a private repository |
| Copy marked (P) | None used | None used | None used |
| Acceptance criteria written and testable | Yes | Yes | Yes |
| Sized at 1–5 points | 3 | 5 | 5 |
| `depends_on` done or earlier in this sprint | None | S00.01.01: earlier in this sprint | S00.01.01, S00.03.01: earlier in this sprint |
| Made-up test data known (A35) | None needed | Made-up email addresses | None needed |

## Tasks

Each story's tasks are written once, in its own section, and ticked there (README §6):

1. [S00.01.01 Serve the SPA and the API from one origin](../E00-skeleton-hosting-sign-in/F00.01-skeleton-and-continuous-integration.md#s000101-serve-the-spa-and-the-api-from-one-origin),
   in F00.01.
2. [S00.03.01 Database context, conventions and first migration](../E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000301-database-context-conventions-and-first-migration),
   in F00.03.
3. [S00.01.02 Build and test every pull request](../E00-skeleton-hosting-sign-in/F00.01-skeleton-and-continuous-integration.md#s000102-build-and-test-every-pull-request),
   in F00.01.

## Settled at planning

These follow from this being the first sprint. They are recorded in the stories' *Notes* and in
[releases.md](../releases.md) (assumptions 5 and 6).

- **No test environment yet.** The Definition of Done line "Deployed to the test environment" cannot be met until
  S00.02.03 (Sprint 02). These stories are done when every other line is met; S00.02.03's first deploy carries them
  to test and their criteria are checked there.
- **No CI for the first two stories.** S00.01.01 and S00.03.01 are merged by pull request before `ci.yml` exists. They
  count as done once S00.01.02's first run on `main` is green.
- **Two criterion parts need Azure.** S00.03.01's run on Azure SQL and S00.01.02's Bicep artifact are checked by
  S00.02.03's first deploy in Sprint 02.
- **Repository plan.** The owner decides at planning whether the repository is public or private. If it is private,
  GitHub Pro is needed now to protect `main` (S00.01.02's first criterion); design.md §10.2 already budgets it.

## Result

Done: __ points, __ stories. Not done (back to the backlog): …

Unplanned work: what it was, and roughly how many hours, so the next sprint's capacity allows for it.

## Review

Nothing in this sprint can be tried by the managers, so send the provider's managers a three-line written update
instead (README §10). Date sent: …

## Retro

Keep: … Change: …
