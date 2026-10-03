# Grow2Notes backlog: method

How the Grow2Notes work is broken down, written, sized and planned into sprints. This file is only the method. The
backlog itself is the other files in this folder. The method follows four decisions: D52 (Markdown in the repo,
importable later), D53 (2-week sprints), D54 (about 20 hours a week, one developer) and D55 (R1 is the app, M0–M6;
R2 is the admin MCP server, after go-live). It adds no feature. A decision in [decisions.md](../decisions.md) can
override anything here.

Evidence grades: **[Standard]** a published standard; **[Vendor]** the tool maker's own documentation;
**[Convention]** widely used practitioner practice; **[Opinion]** one expert's view, or this method's own choice.
Numbers in brackets point to [Sources](#12-sources).

## 1. Purpose

- **One place for all the work.** The backlog holds all the work for R1 and R2 and nothing else (the WBS 100% rule,
  §2.3), in an order the developer can build from, one sprint at a time.
- **Link, don't copy.** [design.md](../design.md), [decisions.md](../decisions.md) and [ux/](../ux/README.md) stay the
  sources of truth. A backlog item says which part of them it delivers. It adds only what they lack: story-specific
  acceptance criteria, tasks, a size and a sprint.
- **New scope enters through decisions.md first.** An item with no source is either a missing decision or extra scope.
- **Light enough for one developer.** You need only Git and a text editor until the owner picks a board (§11).

## 2. Hierarchy

### 2.1 Epic › Feature › Story › Task, with the release on each epic

| Level | What it is | Deliverable or work | Typical size | Written when |
|---|---|---|---|---|
| **Release** | Not an item of its own: a field on each epic. A version handed to the provider. **R1** is the app (design.md M0–M6) and **R2** is the admin MCP server (D48–D51, D55), designed in [mcp-server.md](../mcp-server.md). | Deliverable | Many sprints | Already decided |
| **Epic** | A large deliverable within a release. R1's epics match the milestones in design.md §14 one to one. | Deliverable | 2–6 sprints | At the start of the release |
| **Feature** | One capability that a person uses or the operator relies on. Usually one screen, one file type or one piece of platform. UX screen specs attach here. | Deliverable | 2–8 stories | At the start of the release |
| **Story** | The smallest slice that is useful and testable on its own. It cuts through every layer: screen, API, data, audit and tests. Its type is `story`, `enabler`, `spike` or `bug` (§3.2). It is the **work package**: the level that gets a size, goes into a sprint and meets the Definition of Done. | Deliverable (work package) | 1–5 points; 3–8 fit in a sprint | 1–2 sprints ahead |
| **Task** | One step towards finishing a story, such as "write the integration tests" or "update the runbook". | Work (an activity) | One sitting, up to about 4 hours | At sprint planning, or while working |

Name releases, epics and features as nouns ("Flag review"). Give stories a short outcome title ("Invite a user"). Start
tasks with a verb ("Add the invite tool").

### 2.2 Why this hierarchy

- **Scrum leaves the structure to us.** The Scrum Guide has only a Product Backlog of items. Refinement breaks those
  items into smaller ones, and the Guide calls Scrum "purposefully incomplete". It names no epics, features or stories
  [1, Standard]. So we pick the smallest hierarchy that meets this project's needs.
- **It matches what's already asked for.** D52 names epics, features, user stories and tasks. The owner asked for
  "user stories → features → tasks".
- **It imports into Azure DevOps without renaming.** It is the Azure DevOps Agile process's own hierarchy: Epic ›
  Feature › User Story › Task, with a Story Points field [5, 7, Vendor]. The Scrum process uses "Product Backlog Item"
  and "Effort" instead. The Basic process has only Epic › Issue › Task [5].
- **It fits GitHub.** GitHub sub-issues allow eight levels and 100 children per parent [8, Vendor].
- **Release is not a work item** in either tool. On import it becomes a label or tag, and the parent of its sprints
  (§11).
- **SAFe's extras don't help one developer.** SAFe adds layers, such as Capability, to coordinate many teams
  [unverified: from search summaries, SAFe's glossary not opened]. We borrow only its idea of an **enabler** story
  [12] (§3.2).
- **Epic versus feature is set by position in the tree**, not by size. Cohn advises against arguing over the labels
  and suggests settling on what the team understands [22, Opinion].

### 2.3 How the backlog maps onto a WBS, and the 100% rule

These are the PMI work breakdown structure (WBS) practices we follow [3, 4, Standard, read through a secondary source]:

- WBS elements are **deliverables**, named for what is produced, not for what is done.
- Under the **100% rule**, the children of each element hold all of the parent's work and nothing outside it, at
  every level.
- Elements don't overlap.
- The **work package** is the lowest level that is estimated and managed.

The third edition (2019) applies the WBS to agile life cycles [3]. Agile teams commonly map epics, features and
stories onto WBS levels [30, Convention].

- Release, epic, feature and story are WBS elements. **The story is the work package.**
- **Tasks are activities**, so they sit below the WBS. They are left out of the 100% check and are written only when
  the work is near.
- **The 100% rule counts all project work**, not only code. The go-live checklist in design.md §14 is scope, so it is
  covered by E06's *Go-live readiness* feature. Lines that the provider or the owner does carry no points (§9). Sprint
  events are paid for from capacity (§7.2), not written as backlog items.

**When to run the 100% check:** when an epic's features are first written, before an epic is closed, and whenever a
decision changes scope.

1. **Nothing missing.** One coverage matrix, [coverage.md](coverage.md), lists every row below exactly once, each
   assigned to one epic and one feature, even where a screen file spans milestones. Epic files link to their part of
   the matrix and keep no table of their own. The rows depend on the release:
   - **R1:** every bullet in each milestone's *Scope* and *Done when* in design.md §14, every go-live checklist line,
     and every acceptance criteria group in the UX screen files. A group is a bold sub-heading under *Acceptance
     criteria*, written like `users.md › Invite`.
   - **R2:** each row of mcp-server.md §1.2, each automated test group and manual test in mcp-server.md §10, each
     §11.1 switch-on checklist line, and the defaults MA1–MA12.

   Cross-cutting groups, such as `note-form.md › Layout and accessibility` or `today.md › Privacy (D22, §9.6)`, are
   marked **DoD** instead of a feature and are checked on every story (rule 5). A row with no owner is a gap, so add a
   feature or story.
2. **Nothing extra.** Every feature and story lists at least one source in `sources`: a decision, an assumed default,
   a design.md or mcp-server.md section, or a UX file. An item with no source is one of two things:
   - a missing decision: the owner makes it and it goes in decisions.md, then the item gets its source;
   - unrequested scope: set it to `dropped`. This half of the rule also keeps unrequested features out.
3. **No overlap.** Each coverage row has exactly one owning feature, or is marked DoD. Other items may mention the row
   but don't own it.
4. **Children add up to the parent.**
   - A feature is done only when all its stories are done and the rows it owns pass in the test environment.
   - An epic is done only when all its features are done and its milestone's *Done when* list passes in test.
5. **Rules that apply everywhere are not owned by a feature.** Examples are nothing stored on the device, Melbourne
   time, audit entries and security headers. In coverage.md they are marked DoD. They live in the Definition of Done
   (§9) and are checked on every story. The first build of their shared mechanism is an enabler story, such as the
   audit writer in E00 (its sources include design.md §5.4 and mcp-server.md §3.5).

### 2.4 Example

This is for illustration only. The real R2 items are written in their own files, and the points are made up.

```text
R2  Admin MCP server                                          release   D48–D51, D55, mcp-server.md
└─ E07  Admin MCP server                                      epic      mcp-server.md §1.4
   ├─ F07.01  Support role, sign-in and Connect an AI assistant   feature   slice 1
   │  ├─ S07.01.01  Add the Support role                     story · 3
   │  └─ S07.01.02  Connect an AI assistant with a Grow2Notes account and MFA   story · 5
   ├─ F07.02  MCP endpoint, look-ups and audit                feature   slice 2
   ├─ F07.03  Setup tools                                     feature   slice 3
   ├─ F07.04  User tools with preview and confirm             feature   slice 4
   │  ├─ S07.04.01  Invite a user                             story · 3
   │  │    Add the invite tool, reusing the app's invite handler
   │  │    Tests: preview, then confirm; Support needs a reason and may invite workers only; other organisation refused
   │  │    Check the audit entry matches an invite from the Users screen, plus "via": "mcp"
   │  ├─ S07.04.02  Deactivate a user                         story · 2
   │  └─ S07.04.03  Reset a user's sign-in                    story · 2
   └─ F07.05  Hardening, manual tests and switch-on           feature   slice 5
```

## 3. Writing items

### 3.1 Stories

- **Format:** "As a [role], I want [capability], so that [benefit]". This is the Connextra template from 2001 [16,
  Convention]. Roles matter here because what someone may do depends on their role, so the role in the story is also
  the permission to test. The roles are:
  - **worker** and **manager** (D24);
  - **support**: the operator's people using the MCP server, R2 only (D50);
  - **operator**: the developer's business (D40);
  - **the provider**, for compliance duties.
- **Job stories** fit when the situation matters more than who acts. Their form is "When [situation], I want to
  [motivation], so I can [outcome]", for example "When I start today's note for a participant, I want the groups from
  their last note already ticked…" (D46). Mix them with user stories as needed [17, Opinion].
- **A story starts a conversation; it is not a contract.** Jeffries describes three parts: the card, the conversation
  and the confirmation [15, Convention]. Put what refinement settles in the story's *Notes*.
- **Check every story against INVEST:** independent, negotiable, valuable, estimable, small and testable [14,
  Convention]. Here, "valuable" includes value to the operator or for compliance, such as an audit entry or a restore
  test.

### 3.2 Story-level types, and where technical work goes

| Type | Use it for | Example |
|---|---|---|
| `story` | Something a worker, manager or support person can do or see. | Invite a user |
| `enabler` | Technical, infrastructure, compliance or documentation work with no direct user, which several stories need or which is a deliverable itself [12, Convention]. Its first line says "Enabler: [what]. So that [what it makes possible]." | Deploy to test from `main`; the audit writer; the restore runbook |
| `spike` | Time-boxed research when a story can't be sized. It produces a decision, a design note or split stories, not production code. Try the other splits first [18, Opinion]. | Find out which MCP protocol revision approved assistants negotiate (mcp-server.md §3.6 leaves it to the manual tests) |
| `bug` | A defect found **after** its story was done. Before its release, file it under the feature it affects. After its release, file it under a *Fixes* feature in the current release's epic, naming the affected feature in `sources`, so finished epics stay closed. A defect found while a story is in progress is fixed as part of that story. | Wrong date on a past-day note |

**Where technical work goes:**

- **Needed by one story only:** make it a **task** in that story.
- **Needed by several stories, or a deliverable in its own right** (hosting, CI/CD, the tenancy filter, the audit
  writer, runbooks): make it an **enabler** under the feature it serves. Size it and count it like any story.
- **A rule every story must follow** (accessibility, no participant data in logs, Melbourne time): put it in the
  **Definition of Done**, not in a story. Cohn treats these as constraints [21, Opinion]. Only building the shared
  mechanism, once, is an enabler.
- **Tests:** never write them as separate stories. They are part of done.

### 3.3 Splitting

Aim for 3–8 stories a sprint. Split any story you expect to take more than about two work sessions (roughly 8 focused
hours). Keep stories at 1–5 points; an 8 means split. The Humanizing Work guide's rule of thumb is 6–10 stories a
sprint, but it calls this a ratio that scales with sprint length, team size and velocity [19, Convention]. At about
29 focused hours for one developer, and with fixed Definition of Done costs on every story, 3–8 suits this project
[Opinion]. Always slice **vertically**: each slice goes through screen, API, database, audit and tests. Never make one
story for the API and another for the screen [14, 19, Convention].

Try these patterns in order. They come from SPIDR [18] and the Humanizing Work patterns [19].

1. **Workflow steps, simplest end to end first.** For example, "write a note" becomes: a draft with autosave, then
   submit, then flag for a manager, then editing with versions.
2. **Operations.** "Manage participants" becomes add, edit, archive and restore. There is never a delete story (design.md
   §3.7).
3. **Business rules, happy path first.** For example, a worker's note for today only first, then a manager's past-day
   note (D35).
4. **Data variations.** Goals first, then common items, then common item groups with picks copied from the last note
   (D44–D46).
5. **Interfaces.** Passkey setup first, then password plus 6-digit code (D23). Don't split by phone and laptop: every
   story must work on both (D5, §9).
6. **Defer performance.** Build the report first. If needed, meet "20 notes in under 10 seconds" (M4) in a second story.
7. **Spike**, only when none of the above gives a story you can size.

### 3.4 How far ahead to write

| Level | When to write it |
|---|---|
| Releases, epics, features | For the whole release, up front. R1's scope is final in design.md, and R2's in mcp-server.md. |
| Stories | Draft them feature by feature. Refine them (criteria, points) about two sprints ahead. |
| Tasks | At sprint planning, or as you find them. |

R2's design is [mcp-server.md](../mcp-server.md). R2 features follow its §1.4 slices one to one (F07.01–F07.05), just
as R1 epics follow M0–M6.

### 3.5 Acceptance criteria

- **Link first.** Most criteria already exist in two places: the *Done when* lists in design.md §14, and the
  *Acceptance criteria* section of each UX screen file, grouped under bold sub-headings.
  - A story names the groups it delivers, for example `ux/screens/users.md › Acceptance criteria › Invite`, and copies
    nothing.
  - If a story delivers only part of a group, list the bullets it covers by their first few words.
- **Add only what is missing**, as checklist items, which is the style the UX specs use.
- **Use Given / When / Then only where the state before the action matters**: permissions, dates and time zones,
  conflicting edits [20, Convention]. For example: "Given a token for a manager since made a worker, when it calls the
  invite tool, then the call returns 401 and no invite is created."
- **Every criterion needs a check**: either an automated test or a named manual check in the test environment.

## 4. ID scheme

| Item | Format | Example | Rule |
|---|---|---|---|
| Release | `R` + number | `R1` | D55 |
| Epic | `E` + 2 digits | `E03` | Numbered across the whole backlog. R1 uses E00–E06, matching M0–M6. Later epics take the next free number, so R2 starts at E07. |
| Feature | `F` + epic digits + `.` + 2 digits | `F03.02` | Numbered within its epic, in the order written |
| Story (any type) | `S` + feature digits + `.` + 2 digits | `S03.02.01` | Numbered within its feature, in the order written |
| Sprint | `Sprint` + 2 digits | `Sprint 07` | Counted from the first sprint, across both releases |

Tasks have no ID. Each is a plain checklist line in its story (§6).

R1 epics: **E00** Skeleton, hosting and sign-in (M0) · **E01** Participants, goals, common items and guide prompts (M1)
· **E02** Note form, autosave, submit and edit history (M2) · **E03** Flag review (M3) · **E04** Daily report, PDF and
Word (M4) · **E05** Participant export, audit completeness and retention (M5) · **E06** Hardening and go-live (M6).

- **IDs never change and are never reused.** A dropped item keeps its file, with `status: dropped`.
- **Moving an item:** set the old one to `dropped`, give it the note "moved to …", and create the new one under its new
  parent. That way an ID always shows its parent.
- **Backlog order is held separately from IDs.** Near the top of releases.md is one ordered list of the IDs not yet
  done: the Product Backlog order. The owner reorders it at refinement, because the Product Owner is accountable for
  ordering the backlog [1, Standard]. IDs stay stable; add `depends_on` only where the order really matters. Don't
  renumber.
- **Use the story ID in every pull request description.** Branch names follow the git rules: before import,
  `feat/invite-user`; after import to GitHub, `feat/123-invite-user`, using the issue number.

## 5. File layout

The epic, feature and story names in this tree are only illustrations.

```text
design/backlog/
├─ README.md                                  this method
├─ releases.md                                the backlog order; per release: goal, epics, reference stories,
│                                             velocity, burn-up
├─ coverage.md                                the coverage matrix: every scope row and its owner (§2.3)
├─ E00-skeleton-hosting-sign-in/              one folder per epic
│  ├─ E00-skeleton-hosting-sign-in.md         the epic: goal, features, link to its coverage rows
│  ├─ F00.01-deployment.md                    a feature: outcome, and each of its stories as a section
│  └─ …
├─ E07-admin-mcp-server/
└─ sprints/
   ├─ sprint-01.md                            goal, capacity, plan, result, review, retro
   └─ …
```

- **File names:** `<ID>-<slug>.md` for epic and feature files. The slug is lowercase kebab-case of six words or
  fewer. Lowercase matters because Windows file names ignore case.
- **Front matter:** every epic and feature file starts with YAML front matter, and every story's fields block holds
  the same kind of fields. Status lives only there. Lists elsewhere hold just IDs and titles, so nothing gets out of
  step.
- **Nothing is stored twice.** A feature's or story's parent and release follow from its ID and its epic, so they are
  not fields; the import script derives them.
- **This project's layout: one file per feature.** Each story is a section of its feature's file: a `## <ID> <title>`
  heading, then the story's own fields in a fenced `yaml` block, then its sections from the story template (§6) as
  `###` headings. Each epic folder also holds one epic file. That means about five times fewer files than one file
  per story, and a script can still import it: it reads the feature file's front matter and each story's fields block.
  Wherever this method says "front matter" for a story, it means that fields block. The other option, one file per
  story named `<ID>-<slug>.md`, is not used.

**Front matter fields**

| Field | On | Values |
|---|---|---|
| `id` | all | As in §4 |
| `type` | all | `epic`, `feature`, `story`, `enabler`, `spike`, `bug` |
| `title` | all | Short title, without the ID |
| `release` | epics | `R1`, `R2` |
| `milestone` | R1 epics | `M0` to `M6` |
| `status` | stories; on epics and features only when `dropped` | See the status table below |
| `sources` | all | A list, for example `D48`, `A35`, `"design.md §6.6"`, `"ux/screens/users.md"` |
| `rough_points` | features | A rough size until its stories are written; delete it after that |
| `points` | stories | 1, 2, 3 or 5. An 8 or more means split it before it is Ready |
| `sprint` | stories | Sprint number, filled in at the sprint planning that takes the story; empty until then. The forecast is kept only in releases.md |
| `depends_on` | stories | IDs that must be done first. Keep these rare (INVEST: independent) |
| `github`, `azure` | all | Issue number or work item ID, filled in on import |

**Status values**

| Status | Meaning | GitHub | Azure DevOps (Agile) |
|---|---|---|---|
| `new` | Written down, not yet ready | Open | New |
| `ready` | Meets the Definition of Ready (§8) | Open | New |
| `doing` | Started in the current sprint | Open | Active |
| `done` | Meets the Definition of Done (§9) | Closed as completed | Closed |
| `dropped` | Out of scope; file kept for the record | Closed as not planned | Removed |

An epic's or feature's status is derived, not stored (unless it is `dropped`): it is `doing` while any child is doing,
and `done` under §2.3 rule 4.

## 6. Templates

**Epic** (`E07-admin-mcp-server/E07-admin-mcp-server.md`)

```markdown
---
id: E07
type: epic
title: Admin MCP server
release: R2
milestone:            # M0–M6 for R1 epics; empty for R2
sources: [D48, D49, D50, D51, D55, "mcp-server.md"]
github:
azure:
---
# E07 Admin MCP server

**Goal:** one sentence saying what is true when this epic is done.

**Done when:** link to design.md §14 Mx *Done when* (R1), or to mcp-server.md §10 (R2).

## Features
- F07.01 Support role, sign-in and Connect an AI assistant
- F07.02 MCP endpoint, look-ups and audit
- F07.03 Setup tools
- F07.04 User tools with preview and confirm
- F07.05 Hardening, manual tests and switch-on

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E07.
```

**Feature** (`F07.04-user-tools.md`)

```markdown
---
id: F07.04
type: feature
title: User tools with preview and confirm
sources: [D48, D50, MA6, MA7, "mcp-server.md §1.4 slice 4", "mcp-server.md §5.3"]
rough_points: 8       # delete once the stories are written
github:
azure:
---
# F07.04 User tools with preview and confirm

**Outcome:** a manager or support person can invite, deactivate or reset a user by asking an approved AI assistant,
limited to what their role allows (D50, mcp-server.md §2.1).

## Stories
- S07.04.01 Invite a user
- S07.04.02 Deactivate a user
- S07.04.03 Reset a user's sign-in

## Feature checks
This feature's rows in coverage.md, and any check that spans its stories. Link them; don't copy them.
```

**Story** (a section of `F07.04-user-tools.md`, under the one-file-per-feature layout in §5). Enablers, spikes and
bugs use the same shape with a different `type`. In the feature file the section starts with the heading
`## S07.04.01 Invite a user`, the fields below go in a fenced `yaml` block instead of front matter, and the
sub-headings become `###`.

```markdown
---
id: S07.04.01
type: story           # story | enabler | spike | bug
title: Invite a user
status: new           # new | ready | doing | done | dropped
points:               # 1, 2, 3 or 5
sprint:
depends_on: [S07.01.02]
sources: [D48, D49, D50, MA6, MA7, "mcp-server.md §2.1", "mcp-server.md §5.3", "design.md §4.11", "design.md §8.1"]
github:
azure:
---
# S07.04.01 Invite a user

As a manager, I want to invite a new worker by asking my AI assistant,
so that I can add staff without opening Grow2Notes.

<!-- Enabler: "Enabler: [what]. So that [what it makes possible]."
     Spike:   "Question: … Time-box: N hours. Output: decision / design note / split stories."
     Bug:     "Found in: [ID]. Expected: … Actual: … Steps: …" -->

## Acceptance criteria
Already specified elsewhere (linked, not copied):
- design.md §8.1: the invite and MFA enrolment work as in the app.
- mcp-server.md §5.3: preview, then confirm, with the Invite preview wording (P).

Specific to this story:
- [ ] The invite is the same as one made on the Users screen: same checks, same setup email, same audit entry, plus
      `"via": "mcp"` (mcp-server.md §9).
- [ ] Called without `confirm: true`, the tool returns a preview and creates no invite; called again with
      `confirm: true`, it invites (MA6).
- [ ] Given a Support account, when it invites without a `reason`, then it is refused with `reason.required` and no
      invite is created.
- [ ] Given a Support account, when it invites a manager, then it is refused with `role.not_allowed` (workers only).
- [ ] Given a token for a manager since made a worker, when it calls the invite tool, then the call returns 401.
- [ ] The tool's reply contains no note content (D49).

## Notes
What refinement settled, answers from the owner, links to pull requests.

## Tasks
- [ ] Add the invite tool, reusing the app's invite handler
- [ ] Add the tool's row to the tool matrix test and update the `tools/list` snapshot
- [ ] Write integration tests for the criteria above
```

**Task:** a single checklist line in its story: `- [ ] Verb-first description`. Tasks have no ID, no estimate and no
file. Each is specific, can be finished in one sitting, and is ticked when done [14, 31].

**Sprint** (`sprints/sprint-07.md`)

```markdown
---
sprint: 7
release: R1
start: YYYY-MM-DD
end: YYYY-MM-DD
capacity_hours:       # focused story hours: about 29, less leave and known commitments (§7.2)
planned_points:
done_points:
done_stories:
---
# Sprint 07

**Goal:** one sentence a manager would understand.

## Plan
| Story | Title | Points |
|---|---|---|

## Result
Done: __ points, __ stories. Not done (back to the backlog): …
Unplanned work: what it was, and roughly how many hours, so the next sprint's capacity allows for it.

## Review
Date, who came, what they tried. Feedback becomes new items (give their IDs) or questions for decisions.md.

## Retro
Keep: … Change: … (one of each)
```

## 7. Estimation

### 7.1 Story points, on stories only

**Recommendation:** give every story-level item relative **story points** on the scale **1, 2, 3, 5**. An **8** means
"split it before it is Ready". Tasks get no points. Hours are used only to size the sprint at planning; estimates are
never compared with actuals [Opinion].

- **Reference stories.** Once the first features are drafted, pick one typical small story as a **2** and one typical
  larger story as a **5**. Record both in releases.md and size everything else against them.
- **Features** get `rough_points` (a quick guess against the reference stories) until their stories are written. That
  is enough for an early R1 forecast.

Why points, and not something else:

- **Not hours on stories.** With one developer, hours look simpler. But hour estimates invite comparing estimates with
  actuals, which tends to turn into pressure instead of delivery [Opinion]. Points stay valid as the developer gets
  faster or a week is interrupted. Azure DevOps' velocity and forecast charts also read the Story Points field [7,
  Vendor].
- **Not "no estimates" yet (counting finished stories).** Jeffries, who may have coined story points, now prefers no
  estimates: slice small and count finished stories [25, Opinion]. Duarte reports that counting forecasts about as
  well as points [26, secondary summaries; book not read]. We start with points only because there is no history.
  Sprint files record both points and story count, and after about 5 sprints the owner drops points if counting
  forecasts as well.
- **Not SAFe's normalised points.** SAFe starts every full-time developer at 8 points per two-week iteration, one per
  ideal day [13]. At 20 hours a week that gives about 4 points a sprint, which is too coarse for 3–8 stories.

### 7.2 Capacity and velocity

| | Hours per sprint |
|---|---|
| Build time: 2 weeks × about 20 hours (D53, D54) | 40 |
| Less sprint events: planning 1, review 1, retro 0.5, refinement 1.5 | −4 |
| Less a 20% buffer for the unknown, taken from the rest | −7 |
| **Focused story time to plan for** | **about 29** |

**Why a 20% buffer:**

- Velocity typically moves by about 20% either way from one sprint to the next [23, Opinion].
- Setting up the stack, CI and Azure will throw up surprises.
- Dependency updates (design.md §9.11) arrive without warning.
- After go-live, routine operations (design.md §10.9) and support also come out of the 20 hours, unless the owner
  decides otherwise. Log them, with rough hours, under *Unplanned work*, so the next sprint's capacity allows for
  them.

**Plan every sprint by capacity** [23, Opinion]:

1. **Start from about 29 focused hours**, and take off leave and known commitments.
2. **Take Ready stories in backlog order** (releases.md). Write each one's tasks with a rough hour guess, and stop
   when the hours are used. The guesses only size the sprint: don't keep them, and never compare them with actuals.
3. **In sprint 1**, the points of the chosen stories are the **sprint 1 forecast**, not a velocity.
4. **Count points only for finished stories.** An unfinished story goes back to the Product Backlog and earns nothing
   in that sprint [1, Standard]. Re-size it only if what is left is clearly a different size.

**Velocity** is the points of Done stories, or the number of Done stories, averaged over the last three sprints. Use
it only for the release forecast range in §7.3, never to fill a sprint (so not as "yesterday's weather" [27,
Convention]). With one developer, one slipped story moves a sprint's velocity a lot, and Cohn calls velocity "useful
in the long term, not the short term" [23, Opinion].

### 7.3 Forecasting a release

- **Keep a burn-up in releases.md**, with one row per sprint: points done, running total done, and the release's
  total points (its scope). A burn-up shows scope growth as its own line, separate from progress, so new work doesn't
  look like slow work [29, Convention].
- **Forecast as a range, not a single date** [24, Opinion]. Divide the remaining points by the average of the last
  three sprints (likely), and by the slowest of the last three (cautious). This is a simplified stand-in for Cohn's
  statistical range [Opinion].
- **Handle unknowns with the range and the visible scope line**, not by padding estimates.

## 8. Definition of Ready

A story is ready to plan into a sprint when all of these are true:

- [ ] It has its type and title (its parent and release follow from its ID). A user story uses the "As a…" form; an
  enabler or spike says what it is for.
- [ ] `sources` are linked, and what to build is clear from them plus the story's notes.
- [ ] No open owner question changes the story: check *Open questions* in ux/README.md and the screen file's
  questions.
- [ ] **R2:** mcp-server.md §12 Q4 answered if the story depends on it.
- [ ] Any copy it uses that is marked **(P)** is either approved, or the story notes that it ships with the proposed
  wording.
- [ ] Acceptance criteria are written: linked groups plus story-specific checks, all testable.
- [ ] It is sized at 1–5 points.
- [ ] Everything in `depends_on` is done, or planned earlier in the same sprint.
- [ ] The made-up test data it needs is known (A35).
- [ ] **R2 only:** it states which roles may use the tool, and that the tool returns no note content (D49).

Use this list as a reminder, not a sign-off gate. The Scrum Guide's only readiness test is that an item can be Done
within one sprint [1]. Scrum.org warns that a readiness checklist used as a gate tends to replace conversation [28,
Opinion].

## 9. Definition of Done

**Story.** A story is done when all of these are true:

- [ ] **Merged** to `main` through a pull request: squash merge, Conventional Commit title, and the story ID in the
  description. Use one story per pull request where practical.
- [ ] **CI is green** (design.md §10.4): build with warnings as errors, unit and integration tests, NuGet and npm
  audit, type-check, lint, Vitest, Playwright smoke tests with axe, and no pending model changes.
- [ ] **Migrations:** the `migrate.sql` from the CI artifact has been read in the pull request; the change is
  expand-only (design.md §10.6); any new append-only table has its `DENY` in `grow2notes_runtime` and is added to the
  M5 permission test; data fixes are migrations, never ad-hoc SQL.
- [ ] **Automated tests** cover the acceptance criteria. For every new endpoint or tool, that includes worker versus
  manager permissions (and support in R2) and organisation scoping (design.md §14, D3).
- [ ] **R2 tools:** the tool has a row in the tool matrix test (mcp-server.md §10 item 1) and the `tools/list`
  snapshot is updated.
- [ ] **R1 admin stories** (E00 users, E01 configuration): follows mcp-server.md §3.5: handler class with acting user
  and channel; explicit enum values; audit `Details` accepts extra keys.
- [ ] **Rules everywhere:** dates through `TimeProvider` in Australia/Melbourne, stored as UTC (A33); no delete
  endpoint or hard delete, archive instead (A29); new responses carry the design.md §9.7 headers, and API responses
  carry `Cache-Control: no-store` (design.md §6.1); new tenant-owned entities implement `ITenantOwned`; no
  `IgnoreQueryFilters` outside the operator commands (design.md §5.9).
- [ ] **Accessibility:** the changed parts meet WCAG 2.2 AA (A32). Automated checks find only part of WCAG (about 57%
  of issues by volume in Deque's own study [37, Vendor]), so the evidence is:
  - axe in Playwright shows no issues on changed screens;
  - a keyboard-only check passes;
  - the screen spec's accessibility checklist items for the changed parts are ticked;
  - it works at 375 px wide and on a laptop (D5);
  - a screen reader spot check has been done if a form or dialog changed.

  M6's VoiceOver and TalkBack runs on the note form and the Report screen stay a coverage row in E06.
- [ ] **Audit:** every state change writes its audit event from design.md §5.4, or mcp-server.md §9 for R2 (with
  `"via": "mcp"`), and a test finds it.
- [ ] **Privacy:**
  - no participant data or note content in logs or telemetry (design.md §9.5);
  - nothing stored on the device (D22);
  - the parent company's name appears nowhere (D42), checked by hand on screens, page titles, emails and files;
  - **R2:** no note content in any tool reply (D49), with a test.
- [ ] **Words on screen** match the UX spec and the microcopy glossary.
- [ ] **Docs:**
  - design.md or the UX spec is updated if behaviour changed, with a decision in decisions.md;
  - the `ops/` runbooks are updated if operations changed.
- [ ] **Deployed to the test environment** (this happens automatically on merge), and the acceptance criteria checked
  there with made-up data (A35).
- [ ] **Story section:** tasks ticked, `status: done` and `sprint` set in its fields block.

**Short variants.** Some items can't produce tested code, so they use a shorter list:

- **Spike:** the time-box was kept, and its output (a decision in decisions.md, a design note, or split stories) is
  merged by pull request.
- **Document enabler** (a runbook or other document): merged by pull request, and read by the person its coverage row
  names (M5 says "read by a manager").
- **Checklist lines that the provider or the owner does** (design.md §14 go-live checklist, mcp-server.md §11.1): no
  story and no points. They stay a checklist, with owner and evidence columns, in one feature: *Go-live readiness* in
  E06, and F07.05 for the mcp-server.md §11.1 lines. The Release check below ticks them. Only developer work on those
  lists, such as the restore test, becomes a pointed story.

**Feature.** Done when all its stories are done, and the coverage.md rows it owns are checked in test.

**Epic.**

- **R1:** all its features are done, and its milestone's *Done when* list in design.md §14 passes in test.
- **R2:** all its features are done, and the tests in mcp-server.md §10 pass in test.

**Release.**

- **R1:** all epics are done, the *Go-live readiness* checklist is complete with an owner and evidence for every line,
  and the build that passed in test is deployed to production (design.md §10.4, §14).
- **R2:** its epics are done, the mcp-server.md §11.1 switch-on checklist is complete, `Mcp:Enabled` is turned on in
  production, and the date is recorded.

## 10. Sprint cadence

Sprints are a fixed two weeks (D53) and are never extended. If the sprint goal is at risk, cut scope, not quality.

| Event | When | Timebox | What comes out |
|---|---|---|---|
| Sprint planning | First work session of the sprint | 1 h | Sprint goal, chosen stories, tasks, the sprint file |
| Daily check (instead of the Daily Scrum) | Start of each work session | 5 min | Story statuses updated; anything blocking noted |
| Refinement | Spread through the sprint, mostly in week 2 | 1.5 h in total | Next sprint's stories Ready |
| Sprint review | Last work session, with one or more of the provider's managers | 30–45 min | Feedback, which becomes new items or questions for a decision |
| Retrospective | Straight after the review | 15–30 min | One thing to keep and one to change, in the sprint file |

- **Roles.** The owner is the Product Owner: they order the backlog and, at the review, decide what goes to
  production. The developer builds, and also holds the Scrum Master accountability; the retro looks after the process.
  A story is done when it meets the Definition of Done (§9) [1, Standard]. If one person holds both roles, the managers
  at the review are the outside check.
- **Timeboxes.** For a one-month sprint the Scrum Guide's limits are 8 hours for planning, 4 for review and 3 for the
  retro, and shorter sprints usually need less [1, Standard]. The 2017 guide put refinement at no more than 10% of
  capacity [2]. This table scales those limits down for one part-time developer [Opinion].
- **Sprint goal.** One sentence a manager would understand. For example: "A manager can set up a participant with five
  goals on a phone" (from M1's *Done when*).
- **Review.** The Scrum Guide calls the review a working session, not a presentation [1]. Managers try the increment
  themselves, with their own test accounts in the test environment, using made-up data only (A35). If a sprint has
  nothing they can see, as with much of E00, send a three-line written update instead.
- **Feedback that adds scope** goes to the owner as a decision question first (§1). It doesn't go straight into the
  backlog.

## 11. Importing into a board later

Once you import into a board, that board becomes the source of truth. Don't keep the Markdown and a board both live,
because keeping them in step costs more than it saves. After import, write each new number into `github` or `azure`,
then stop editing these files and keep them as the record. The coverage matrix (coverage.md) is scope, not status, so
it stays live in the repo after import, like design.md.

**Bodies:** relative links such as `../design.md` work only in files shown in the repository [33, Vendor], not in an
imported issue or work item. So the import script rewrites each relative link to an absolute
`https://github.com/<owner>/<repo>/blob/main/design/...` URL, and adds a line `Sources: …` (the item's `sources` and
its coverage.md rows) to each body.

### GitHub Issues

- **CLI version.** The `--parent`, `--type` and `--blocked-by` options on `gh issue create` need GitHub CLI 2.94.0 or
  later [10, 11, Vendor].
- **Issue types** need a repository owned by an organisation. Sub-issues and dependencies work in any repository [9,
  10]. In a personal repository, use the labels `epic`, `feature`, `story`, `enabler`, `spike` and `bug` instead.
- **No CSV import.** GitHub doesn't document a built-in CSV import for issues; third-party importers exist
  [unverified]. A short script that calls `gh` does the job.

| Backlog field | GitHub |
|---|---|
| Item | One issue, titled `<ID> <title>` |
| Body | The file without its front matter, with links made absolute and the `Sources: …` line added |
| Parent (from the ID) | `--parent` |
| `release` (from the epic) | Label `R1` or `R2` |
| `points` | Label `points:3`, or a number field in Projects |
| `sprint` | Milestone `Sprint 07` (create the milestones first) |
| `depends_on` | `--blocked-by` |
| Tasks | Stay as the checklist in the story's body; GitHub renders it |
| `done` / `dropped` | Closed as completed / closed as not planned, in a second step (below) |

- **Create the labels first.** `gh issue create` stops with "could not add label: … not found" when a label doesn't
  exist [36, Convention; the gh manual doesn't say]. Run `gh label create R1 --force` [34, Vendor], and the same for
  `R2`, `epic`, `feature`, `story`, `enabler`, `spike`, `bug`, `points:1`, `points:2`, `points:3` and `points:5`.
- **Create in order:** epics, then features, then stories, so each parent's number exists before its children.
- **Close finished items afterwards.** `gh issue create` has no option to create a closed issue [11]. Run
  `gh issue close <n> --reason completed` for done items and `gh issue close <n> --reason "not planned"` for dropped
  ones [35, Vendor].
- **Make re-runs safe:** write each new issue number back into `github:`, so running the script again skips it.
- **Limits:** 100 sub-issues per parent and eight levels [8], which is well within this backlog.

```powershell
# One story under feature issue #42, blocked by issue #40 (numbers are illustrative)
gh issue create --title "S07.04.01 Invite a user" --body-file out/S07.04.01.md `
  --parent 42 --blocked-by 40 --label story --label R2 --label "points:3" --milestone "Sprint 14"
```

### Azure DevOps Boards

1. **Choose Agile when creating the project.** Its Epic › Feature › User Story › Task hierarchy and Story Points
   field match this method. Microsoft's pages disagree on whether the process can be changed later [5, 32, Vendor], and a
   change needs a migration.
2. **Create the iterations first**, such as `Grow2Notes\R1\Sprint 01` and `Grow2Notes\R1\Sprint 02` (the form
   `<Project>\R1\Sprint 01`), so each release is the parent of its sprints. If the import file doesn't give an
   iteration, items default to the top level [6].
3. **Build the CSV** and import it from Boards › Queries › Import work items [6]:
   - Leave out the `ID` column for new items. Don't include `State`, `Assigned To`, `Created By` or `Changed Date`.
     Every item imports as New.
   - Show the hierarchy with indented title columns: `Title 1` for the epic, `Title 2` for the feature, `Title 3` for
     the story and `Title 4` for the task. Microsoft's example shows only two levels. Deeper levels should follow the
     same pattern, but test with a five-row file first [unverified].
   - Map the types: `story`, `enabler` and `spike` become User Story, with a tag `enabler` or `spike` where it applies.
     `bug` becomes Bug.
   - Map the fields:

     | Backlog | Azure DevOps |
     |---|---|
     | `release` (from the epic) | Tag `R1` or `R2` |
     | `points` | `Story Points` |
     | `sprint` | `Iteration Path` |
     | Body, with links made absolute and the `Sources: …` line added | `Description`, with the Markdown converted to HTML [6] |
     | Acceptance criteria | `Acceptance Criteria`, as HTML: wrap each line in `<p>…</p>` |

   - Put no more than 1,000 items in one file [6].
4. **Set the states** after import: export, edit the State column, then import again [6].
5. **Add `depends_on` links by hand** as predecessor links, because CSV import creates only parent-child links [6].

```csv
Work Item Type,Title 1,Title 2,Title 3,Title 4,Description,Story Points,Iteration Path,Tags
Epic,E07 Admin MCP server,,,,"<p>Sources: D48, D49, D50, D51, D55</p>",,,R2
Feature,,F07.04 User tools with preview and confirm,,,"<p>Sources: D48, D50, MA6, MA7</p>",,,R2
User Story,,,S07.04.01 Invite a user,,"<p>As a manager, I want to invite a new worker by asking my AI assistant…</p><p>Sources: D48, D49, D50, MA6, MA7</p>",3,Grow2Notes\R2\Sprint 14,R2
Task,,,,Add the invite tool,,,Grow2Notes\R2\Sprint 14,R2
```

## 12. Sources

Checked on 4 October 2026. "Snippet only" means the page could not be opened, so the claim relies on the search
result text.

**Standards**

1. Schwaber and Sutherland, *The Scrum Guide* (November 2020). <https://scrumguides.org/scrum-guide.html> [Standard]
2. Schwaber and Sutherland, *The Scrum Guide* (2017; replaced by the 2020 guide).
   <https://scrumguides.org/docs/scrumguide/v2017/2017-Scrum-Guide-US.pdf> [Standard; snippet only]
3. PMI, *Practice Standard for Work Breakdown Structures*, third edition (2019).
   <https://www.pmi.org/standards/work-breakdown-structures-third-edition> [Standard; the page returned 403. The
   claim that the third edition covers agile life cycles is snippet only. The 100% rule and the work package
   definition are taken from source 4.]
4. Wikipedia, "Work breakdown structure". It cites PMI's second edition (2006) and Haugan (2001).
   <https://en.wikipedia.org/wiki/Work_breakdown_structure> [secondary]

**Vendor documentation**

5. Microsoft Learn, "Default processes and process templates" (updated 2026).
   <https://learn.microsoft.com/en-us/azure/devops/boards/work-items/guidance/choose-process?view=azure-devops> [Vendor]
6. Microsoft Learn, "Export, update, and import bulk work items with CSV files" (updated 2026).
   <https://learn.microsoft.com/en-us/azure/devops/boards/queries/import-work-items-from-csv?view=azure-devops> [Vendor]
7. Microsoft Learn, "Agile workflow in Azure Boards" (updated 2026).
   <https://learn.microsoft.com/en-us/azure/devops/boards/work-items/guidance/agile-process-workflow?view=azure-devops>
   [Vendor]
8. GitHub Docs, "Adding sub-issues".
   <https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/adding-sub-issues> [Vendor]
9. GitHub Docs, "Managing issue types in an organization".
   <https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/managing-issue-types-in-an-organization>
   [Vendor]
10. GitHub Changelog, "Manage sub-issues, types, and dependencies from GitHub CLI" (10 June 2026; CLI 2.94.0).
    <https://github.blog/changelog/2026-06-10-manage-sub-issues-types-and-dependencies-from-github-cli/> [Vendor]
11. GitHub CLI manual, `gh issue create`. <https://cli.github.com/manual/gh_issue_create> [Vendor]

**Practitioner writing**

12. Scaled Agile, "Story" (user and enabler stories). <https://framework.scaledagile.com/story/> [Convention]
13. wibas, "A tricky slide about story points and capacity in SAFe" (on SAFe's starting velocity of 8 points per
    developer). <https://www.wibas.com/blog/a-tricky-slide-about-story-points-and-capacity-in-safe-and-how-to-get-it-right/>
    [secondary]
14. Wake, "INVEST in Good Stories, and SMART Tasks" (2003).
    <https://xp123.com/articles/invest-in-good-stories-and-smart-tasks/> [Convention]
15. Jeffries, "Essential XP: Card, Conversation, Confirmation" (2001).
    <https://ronjeffries.com/xprog/articles/expcardconversationconfirmation/> [Convention]
16. Agile Alliance, "User story template" (Connextra, 2001). <https://agilealliance.org/glossary/user-story-template/>
    [Convention]
17. Cohn, "Job stories offer a viable alternative to user stories" (2024).
    <https://www.mountaingoatsoftware.com/blog/job-stories-offer-a-viable-alternative-to-user-stories> [Opinion]
18. Cohn, "SPIDR: five simple but powerful ways to split user stories" (updated 2026).
    <https://www.mountaingoatsoftware.com/agile/five-simple-but-powerful-ways-to-split-user-stories> [Convention]
19. Lawrence and Green, "The Humanizing Work guide to splitting user stories".
    <https://www.humanizingwork.com/the-humanizing-work-guide-to-splitting-user-stories/> [Convention]
20. Fowler, "GivenWhenThen" (2013). <https://martinfowler.com/bliki/GivenWhenThen.html> [Convention]
21. Cohn, "Non-functional requirements as user stories" (2023).
    <https://www.mountaingoatsoftware.com/agile/non-functional-requirements-as-user-stories> [Opinion]
22. Cohn, "Epics, features and user stories". <https://mountaingoatsoftware.com/blog/stories-epics-and-themes> [Opinion]
23. Cohn, "Why I prefer capacity-driven sprint planning" (2024).
    <https://www.mountaingoatsoftware.com/blog/why-i-prefer-capacity-driven-sprint-planning> [Opinion]
24. Cohn, "AMC Plan Visualizer tool: agile forecasting for accurate plans" (2025; velocity as a range).
    <https://www.mountaingoatsoftware.com/blog/plan-visualizer-tool-agile-forecasting-for-accurate-plans> [Opinion]
25. Jeffries, "Story points revisited" (2019). <https://ronjeffries.com/articles/019-01ff/story-points/Index.html>
    [Opinion]
26. Duarte, *NoEstimates* (2016). <https://www.goodreads.com/en/book/show/30650836> [Opinion; secondary summaries only]
27. Fowler, "YesterdaysWeather" (2004). <https://martinfowler.com/bliki/YesterdaysWeather.html> [Convention]
28. Scrum.org, "Why isn't the Definition of Ready described in the Scrum Guide?"
    <https://www.scrum.org/resources/blog/why-isnt-definition-ready-described-scrum-guide> [Opinion; snippet only]
29. Atlassian, "Burn up chart". <https://www.atlassian.com/agile/project-management/burn-up-chart> [Convention; snippet
    only]
30. Kanga, "WBS in the age of agile", Zalando PMO blog (2024).
    <https://pmo.zalando.com/posts/2024/11/wbs-in-the-age-of-agile.html> [Convention]
31. Cohn, "The difference between a story and a task".
    <https://www.mountaingoatsoftware.com/agile/the-difference-between-a-story-and-a-task> [Convention; snippet only]

**Added in review (read directly, 4 October 2026)**

32. Microsoft Learn, "Create and manage an inherited process" (updated 2026; documents changing a project from Agile
    to Scrum and from Basic to Agile, which source 5 says can't be done).
    <https://learn.microsoft.com/en-us/azure/devops/organizations/settings/work/manage-process?view=azure-devops>
    [Vendor]
33. GitHub Docs, "Basic writing and formatting syntax" (relative links in rendered files).
    <https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax>
    [Vendor]
34. GitHub CLI manual, `gh label create`. <https://cli.github.com/manual/gh_label_create> [Vendor]
35. GitHub CLI manual, `gh issue close`. <https://cli.github.com/manual/gh_issue_close> [Vendor]
36. GitHub Community, Discussion #35377, "`gh issue create --label ...` but getting error `could not add label: not
    found`". <https://github.com/orgs/community/discussions/35377> [Convention]
37. Deque, "Automated testing study identifies 57 percent of digital accessibility issues".
    <https://www.deque.com/blog/automated-testing-study-identifies-57-percent-of-digital-accessibility-issues/> [Vendor]

The method rests on standards, vendor documentation and practitioner writing. It relies on no independent research
study; the one study it cites, on automated accessibility testing, is a vendor's own [37].
