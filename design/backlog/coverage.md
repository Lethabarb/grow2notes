# Grow2Notes coverage matrix

This is the 100% check for the backlog ([README §2.3](README.md#23-how-the-backlog-maps-onto-a-wbs-and-the-100-rule)).
Every scope row appears once, with exactly one owner: a feature, or **DoD** for a cross-cutting group that is checked
on every story ([README §9](README.md#9-definition-of-done)). Other items may deliver part of a row or check it again.
They are listed under *Also*, and they do not own the row.

Rows are quoted only as far as needed to find them. The sources of truth stay
[design.md §14](../design.md#14-delivery-plan), the [UX screen files](../ux/README.md) and
[mcp-server.md](../mcp-server.md). Coverage is scope, not status, so this file stays live after any import to a board
(README §11).

Last checked: 4 October 2026, at release planning, when all eight epics had been written, and again after the review
the same day.

## Check result

| Check | Result |
|---|---|
| Rows with no owner | **None** |
| Rows with two owners | **None**. Six rows were named by two epics' drafts; each now has one owner (see *Hand-offs resolved*) |
| Features that own no row | None. Each of the 38 features owns at least one row |
| Rows in total | **228**: R1 has 186 (170 owned by a feature, 16 marked DoD); R2 has 42 (all owned by a feature) |

### Hand-offs resolved at merge

Each of these rows was mentioned by two epics. The owner below is the feature that builds the work; the other epic
only mentions the row. Confirm at refinement.

| Row | Owner | Mentioned by | Why |
|---|---|---|---|
| `participants.md › Write past-day note` | F02.08 | E01 | Past-day notes are design.md §14 M2 scope |
| `guide-prompts.md › Effect on the note form (cross-check at M2)` | F02.02 | E01 | It is the note form's Guided notes box |
| `app-shell.md › Badge` | F03.03 | E00 | The To review count is M3 scope; E00 builds `/api/auth/me` without it |
| M3 *Scope*: "the required flag reason" | F03.01 | E02 (S02.03.02) | S02.03.02 builds the Reason field and the 1–200 character rule (M2 *Scope*, "Flag for manager with reason"); F03.01 checks the rule end to end and makes a submitted flag take effect |
| `participant-notes.md › Read view` | F02.04 | E03 (S03.02.01–S03.02.03) | E03 delivers the review panel bullets (24, 25, 26, 28b–28d, and the review parts of 20 and 21) |
| `participant-notes.md › History list` | F02.04 | E03 (S03.02.02), E05 (S05.01.01) | Bullet 10 (To review and Reviewed tags) needs E03's data; bullet 11's Export record link is built by S05.01.01 |

### Rows to watch

These rows have one owner, but part of the row is delivered or can only be checked by a story forecast later than
the owner's own stories. The owner is the feature that starts the row, in both releases (*How to read the tables*).
Under README §2.3 rule 4 the owning feature, and so its epic, is not done until the whole row passes in test; the
epics' real closing dates are in [releases.md](releases.md) (R1 *Epics*). The sprint numbers are the forecast in
releases.md.

| Row | Owner (own stories forecast) | Last contributing story (forecast) |
|---|---|---|
| M2 *Scope*: "The Today list…, 'Your unfinished drafts', and participant history" | F02.01 (Sprints 14–15) | S02.04.02 Past notes (Sprint 18) |
| M2 *Scope*: "The note form: Goals, Common items…, Guided notes…, Flag…, and Submit" | F02.03 (Sprints 15–17) | S02.05.04 (Sprint 18) |
| `note-form.md › Edit mode, discard, leaving, session` | F02.02 (Sprints 13–16) | S02.06.03 (Sprint 19) |
| `participant-notes.md › Read view` and `› History list` | F02.04 (Sprints 17–18) | S03.02.01–S03.02.03 (Sprints 21–22) for the review bullets; S05.01.01 (Sprint 25) for the Export record link |
| M3 *Done when*: "A reviewed note… shows 'Reviewed by … on …' on the note and in the report" | F03.03 (Sprints 21–22) | S04.01.04 (Sprint 24) for the report part, so E03 cannot close at the end of M3 |
| `participants.md › Archive and Restore` | F01.02 (Sprints 10–11) | The Today, Past notes and Export record bullets: S02.01.01 (Sprint 14), S02.04.02 (Sprint 18), S05.01.01 (Sprint 25) |
| `app-shell.md › Screen changes, focus and scroll`, `› Account and Sign out`, `› Session` | F00.05 and F00.04 (Sprints 3–8) | Bullets that name Guide prompts, autosave, Submit, Discard or Today rows: S01.04.02 (Sprint 13) and E02's stories up to S02.02.05 and S02.02.08 (Sprint 16) |
| mcp-server.md §1.2 *Users* | F07.02 (Sprints 33–34) | S07.04.03–S07.04.05, invite, reset and deactivate (Sprint 37) |
| mcp-server.md §1.2 *Participants* | F07.02 (Sprints 33–34) | S07.03.01, add and correct (Sprint 35); S07.04.06, archive and restore (Sprint 38) |
| mcp-server.md §10 automated item 7, *Rules through MCP* | F07.03 (Sprints 35–36) | S07.04.04 and S07.04.05, the last-manager rule and repeated deactivation (Sprint 37) |

**Decision for the owner:** either keep the method's rule (the owning feature stays open until the last bullet passes,
which is what this forecast assumes), or let a feature close with the named bullets recorded as deferred to the named
later story. Neither choice moves a story's forecast sprint, because the bullets are checked inside the later
stories; the first choice is what the *Can close* column in releases.md (R1 *Epics*) shows.

### Other points for the owner

1. **Paragraphs split into clauses.** The M1, M3 and M4 *Scope* entries in design.md §14 are single paragraphs, not
   bullets. They are split here into one row per clause, so that each clause has one owner.
2. **Go-live and switch-on checklist lines are rows with no story.** Lines that the provider or the owner does carry
   no points (README §9 *Short variants*). F06.06 and F07.05 hold them as checklists, each with an owner and evidence
   column. Only the operator's developer work on a line is a pointed story.
3. **Two rows that are not bullets are included** so the 100% check sees them: the design.md §14 preamble "For every
   milestone" (four DoD rows) and the M6 *Migration from Word* paragraph (owned by F06.06).

## How to read the tables

- **Row:** the scope bullet, checklist line or acceptance criteria group, quoted or shortened.
- **Owner:** the one feature whose stories deliver the row, and which is not done until the row passes in test; or
  **DoD**, meaning the group is a rule checked on every story under README §9. When more than one feature delivers a
  row, the owner is the feature that starts it (for a screen file's group, usually the screen's own feature), in R1
  and R2 alike. The later stories that finish the row are under *Also*, and the row is listed in *Rows to watch*.
- **Also:** stories or features that deliver part of the row or check it again. They do not own it.
- Rows are numbered within each table only, so they can be referred to in conversation. The numbers are not IDs.

---

## R1 The app

### All milestones

design.md §14, "For every milestone". These are rules for every story, so they are marked DoD.

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | It ends with a working build deployed to the test environment in Azure Australia Southeast | DoD | S00.02.03 builds the deploy | The DoD line "Deployed to the test environment" |
| 2 | Automated tests check worker-versus-manager permissions and organisation scoping, and grow with each milestone | DoD | S00.03.02, S00.03.04 build the fixture and the endpoint matrix | |
| 3 | Audit entries are added as each feature is built | DoD | S00.03.03 builds the writer; S05.02.01 checks every event type | |
| 4 | Only made-up data is used before go-live (A35) | DoD | | |

### E00 Skeleton, hosting and sign-in (M0)

**design.md §14 M0 Scope**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | ASP.NET Core on .NET 10 LTS serving the React (Vite, TypeScript) build from the same origin; unknown `/api` paths return 404 | F00.01 | | |
| 2 | EF Core and Azure SQL in Australia Southeast, Key Vault, the user-assigned identity, Bicep (`bootstrap`, `main`, `locks`) and GitHub Actions deploying to test and prod | F00.02 | S00.03.01 (the EF Core context), S00.01.02 (CI) | |
| 3 | The Organisation table, `OrganisationId` on every `ITenantOwned` entity with the named query filter, and the `grow2notes_runtime` database role (D3) | F00.03 | | |
| 4 | Accounts: invite and setup (passkey, or password + TOTP), sign-in and sign-out, cookie sessions validated against the security stamp on every request, 30-minute idle timeout with warning, 12-hour limit | F00.04 | S00.06.02 (sending the invite) | |
| 5 | Manager screen: invite, edit, reset sign-in, deactivate and reactivate users | F00.06 | | |
| 6 | The audit table, starting with sign-in and user events. The operator commands | F00.03 | F00.04 and F00.06 write the sign-in and user events | |
| 7 | Telemetry in Australia Southeast that never records request bodies or SQL error text | F00.02 | | |

**design.md §14 M0 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | A manager and a worker can each accept an invite and sign in with MFA on a phone and a laptop, in test, and sign in again a second time (both setup paths) | F00.04 | S00.06.02 (the worker's invite) | Manual check in test |
| 2 | After deactivation, the user's next request is rejected (tested) | F00.06 | S00.04.01 (the stamp check); S05.03.02 checks it end to end at M5 | |
| 3 | A test fails the build if any `ITenantOwned` entity lacks the organisation filter; the named exceptions are listed in the test | F00.03 | | |
| 4 | The 12-hour absolute limit and the `org_id` claim survive the per-request principal rebuild (tested) | F00.04 | | |
| 5 | Successful and failed sign-ins appear in the audit log | F00.04 | S00.03.03 (the writer) | |
| 6 | HTTPS only with HSTS; the cookie is HttpOnly, Secure and SameSite=Strict; API responses carry `Cache-Control: no-store` | F00.04 | S00.01.01 (`no-store`), S00.02.02 (HTTPS only) | |
| 7 | Deleting the test SQL server is refused while its database is locked (or the fallback in §10.1 is recorded) | F00.02 | | |

**ux/screens/sign-in.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `sign-in.md › Sign in` | F00.04 | | |
| 2 | `sign-in.md › Sign in again, in place` | F00.04 | | |
| 3 | `sign-in.md › Setup` | F00.04 | | |
| 4 | `sign-in.md › Privacy and storage (D22, §9.6)` | DoD | | |
| 5 | `sign-in.md › Layout and accessibility` | DoD | S06.02.02 (the go-live device check) | Checked on every story, except the go-live device check of passkey setup from phone email apps, which S06.02.02 does once |

**ux/screens/app-shell.md** (`app-shell.md › Badge` is under E03)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `app-shell.md › Structure, privacy and naming` | F00.05 | S00.01.05 (the D42 check), S00.01.01 (document head) | |
| 2 | `app-shell.md › Navigation` | F00.05 | | |
| 3 | `app-shell.md › Refresh` | F00.05 | S03.03.03 adds `toReviewCount` | |
| 4 | `app-shell.md › Account and Sign out` | F00.04 | S01.04.02 (Guide prompts bullet), S02.02.05 (autosave bullet) | See *Rows to watch* |
| 5 | `app-shell.md › Screen changes, focus and scroll` | F00.05 | S02.01.01, S02.02.08, S02.03.01 (the Today row, Discard and Submit bullets) | See *Rows to watch* |
| 6 | `app-shell.md › Start-up and whole-page messages` | F00.05 | | |
| 7 | `app-shell.md › Session` | F00.04 | S02.02.05 (autosave and Submit confirmation bullets) | "After timing out mid-note" is M2 *Done when* row 3 (F02.02) |
| 8 | `app-shell.md › Visual and settings` | DoD | | |

**ux/screens/users.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `users.md › Access` | F00.06 | | |
| 2 | `users.md › List` | F00.06 | | |
| 3 | `users.md › Invite` | F00.06 | | |
| 4 | `users.md › Detail and actions` | F00.06 | S00.03.06 reuses the Reset sign-in handler | |
| 5 | `users.md › Edit` | F00.06 | | |
| 6 | `users.md › General` | DoD | | |

### E01 Participants, goals, common items and guide prompts (M1)

**design.md §14 M1 Scope** (one paragraph, split into its clauses)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Manager screens for participants (add, edit, archive, restore) | F01.02 | | |
| 2 | Each participant's goals | F01.02 | | |
| 3 | The organisation-wide common items in their groups (groups: add, rename, reorder, archive, restore; items: add, reword, reorder, move to another group, archive, restore; the built-in Every note group created with the organisation, D44, D45) | F01.03 | | |
| 4 | The guide prompts | F01.04 | | |
| 5 | Workers cannot reach any of these | F01.01 | Every configuration story's `403` test | |
| 6 | Every change is audited | F01.01 | Every configuration story writes its event | |

**design.md §14 M1 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | A manager can set up a participant with five goals on a phone without help | F01.02 | | Manual check in test, by a manager new to the screen |
| 2 | A worker calling any configuration endpoint gets `403` (automated test) | F01.01 | | |
| 3 | There is no delete endpoint for any of these | F01.01 | | |
| 4 | The Every note group cannot be renamed, moved or archived, and a second one cannot be created (API and database tests) | F01.03 | | |
| 5 | Every change writes an audit entry with old and new values | F01.01 | | |

**ux/screens/participants.md** (`participants.md › Write past-day note` is under E02)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `participants.md › Access and privacy` | F01.02 | | |
| 2 | `participants.md › Participants list` | F01.02 | | |
| 3 | `participants.md › Add participant and Details` | F01.02 | | |
| 4 | `participants.md › Goals` | F01.02 | | |
| 5 | `participants.md › Archive and Restore` | F01.02 | S02.01.01, S02.04.02, S05.01.01; F05.03 checks archiving end to end | See *Rows to watch* |
| 6 | `participants.md › Cross-cutting` | DoD | | |

**ux/screens/common-items.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `common-items.md › Content and copy` | F01.03 | | |
| 2 | `common-items.md › Layout` | F01.03 | | |
| 3 | `common-items.md › Loading, empty, errors` | F01.03 | | |
| 4 | `common-items.md › Reordering` | F01.03 | | |
| 5 | `common-items.md › Add, edit, rename, move` | F01.03 | | |
| 6 | `common-items.md › Archive and restore` | F01.03 | | |
| 7 | `common-items.md › Failures and conflicts` | F01.03 | | |
| 8 | `common-items.md › Accessibility runs` | DoD | | |

**ux/screens/guide-prompts.md** (`guide-prompts.md › Effect on the note form` is under E02)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `guide-prompts.md › Access and structure` | F01.04 | | |
| 2 | `guide-prompts.md › Loading` | F01.04 | | |
| 3 | `guide-prompts.md › Field` | F01.04 | | |
| 4 | `guide-prompts.md › Limit and count` | F01.04 | | |
| 5 | `guide-prompts.md › Save` | F01.04 | | |
| 6 | `guide-prompts.md › Leaving` | F01.04 | | |
| 7 | `guide-prompts.md › Storage and naming` | DoD | | |
| 8 | `guide-prompts.md › Accessibility runs` | DoD | | |

### E02 Note form, autosave, submit and edit history (M2)

**design.md §14 M2 Scope**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | The Today list (all participants, D20), "Your unfinished drafts", and participant history | F02.01 | F02.04 (participant history) | See *Rows to watch* |
| 2 | The note form: Goals, Common items with the Every note group and the group picker (D44, D45), Guided notes with placeholder prompts, Flag for manager with reason, and Submit | F02.03 | F02.02 (Goals, Guided notes), F02.05 (Common items, group picker) | See *Rows to watch* |
| 3 | Starting picks copied from the participant's most recent submitted note (D46, A44), with the line naming its date (A47) | F02.05 | | A47 may be dropped by the owner (S02.05.03 Notes) |
| 4 | Autosave with `clientId` and `seq`; snapshot (goals, groups and items) at draft creation with the `listsVersion` check; picks autosaved and versioned with the ticks; discard as a status change | F02.02 | F02.05 (picks and the groups snapshot) | |
| 5 | One live note per participant per day, enforced by the filtered unique index | F02.02 | | |
| 6 | Submit creates version 1; Save changes by the author or a manager creates the next version; pending edits and Cancel; version history for managers | F02.06 | F02.03 (version 1), F02.07 (version history) | |
| 7 | Past-day notes for managers. Melbourne "today" through `TimeProvider` | F02.08 | S00.01.01 (the clock), S02.02.01 (its daylight-saving tests) | |

**design.md §14 M2 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | A worker can fill in and submit a note on a 375 px-wide phone | F02.03 | | |
| 2 | Closing the browser mid-sentence and reopening on another device loses no more than the last few seconds of typing; a retried save whose response was lost raises no conflict | F02.02 | | |
| 3 | After an idle timeout and signing back in, the draft is intact | F02.02 | S00.04.08 (sign in again in place) | |
| 4 | Browser developer tools show no note content in localStorage, sessionStorage, IndexedDB or Cache Storage | F02.02 | DoD (nothing stored on the device, D22) | |
| 5 | The server refuses a worker's note for any date other than today in Melbourne; the daylight-saving tests pass with a fake clock, under `TZ=UTC` and on Windows | F02.02 | S00.01.01 (the clock), S02.02.01 (the daylight-saving tests) | |
| 6 | A second live note for the same participant and day cannot be created (database test); after a discard, a new note can | F02.02 | | |
| 7 | Each Save changes adds exactly one version, and earlier versions are byte-for-byte unchanged | F02.06 | | |
| 8 | A goal reworded, or a group renamed, after a draft was created does not change that draft or its submitted version | F02.05 | S02.02.03 (the goals snapshot) | |
| 9 | A new note starts with the last submitted note's picks, skipping archived groups, and no item ticks; with no earlier note, nothing is picked; unpicking clears ticks; a picks change in an edit creates a new version (automated tests) | F02.05 | S02.06.01 (the edit) | |

**ux/screens/today.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `today.md › Content and copy` | F02.01 | | |
| 2 | `today.md › Order and layout` | F02.01 | | |
| 3 | `today.md › Loading, empty and error` | F02.01 | | |
| 4 | `today.md › Search` | F02.01 | | |
| 5 | `today.md › Focus and navigation` | F02.01 | | |
| 6 | `today.md › Refresh` | F02.01 | | |
| 7 | `today.md › Privacy (D22, §9.6)` | DoD | | |
| 8 | `today.md › Automated and manual checks` | F02.01 | | |

**ux/screens/note-form.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `note-form.md › Identity and wrong-participant protection` | F02.03 | | |
| 2 | `note-form.md › Autosave` | F02.02 | | |
| 3 | `note-form.md › Conflicts and refusals` | F02.02 | S02.06.03 (two editors) | |
| 4 | `note-form.md › Validation and Submit` | F02.03 | | |
| 5 | `note-form.md › Common items and groups (D44–D47)` | F02.05 | | |
| 6 | `note-form.md › Edit mode, discard, leaving, session` | F02.02 | F02.06 (edit mode) | See *Rows to watch* |
| 7 | `note-form.md › Layout and accessibility` | DoD | S06.02.03 (VoiceOver and TalkBack at M6) | |

**ux/screens/participant-notes.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `participant-notes.md › History list` | F02.04 | S03.02.02 (bullet 10), S05.01.01 (bullet 11's Export record link) | See *Rows to watch* |
| 2 | `participant-notes.md › Read view` | F02.04 | S03.02.01–S03.02.03 (review panel bullets) | See *Rows to watch* |
| 3 | `participant-notes.md › Both screens` | F02.04 | | |

**ux/screens/version-history.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `version-history.md › Access and privacy` | F02.07 | | |
| 2 | `version-history.md › Page A` | F02.07 | | |
| 3 | `version-history.md › Page B` | F02.07 | | |
| 4 | `version-history.md › Not found and errors` | F02.07 | | |
| 5 | `version-history.md › Navigation and focus` | F02.07 | | |
| 6 | `version-history.md › Layout and display` | F02.07 | | |

**Groups from M1 screen files that are M2 work**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `participants.md › Write past-day note` | F02.08 | E01 mentions it | Hand-off resolved |
| 2 | `guide-prompts.md › Effect on the note form (cross-check at M2)` | F02.02 | E01 mentions it | Hand-off resolved |

### E03 Flag review (M3)

**design.md §14 M3 Scope** (one paragraph, split into its clauses)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | The required flag reason | F03.01 | S02.03.02 (builds the field and the rule) | Hand-off resolved |
| 2 | The To review list (oldest first) and badge | F03.03 | | "Oldest first" sort key to confirm (S03.03.01 Notes) |
| 3 | The Reviewed tab | F03.03 | | |
| 4 | Mark reviewed with an optional comment | F03.02 | | |
| 5 | The re-review rule (A15) | F03.01 | | |
| 6 | Audit of every review | F03.02 | | |

**design.md §14 M3 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | A flagged note shows in the list and badge for every manager on their next page load | F03.03 | | |
| 2 | A reviewed note leaves the list and shows "Reviewed by … on …" on the note and in the report | F03.03 | S03.02.02 (on the note), S04.01.04 (in the report) | See *Rows to watch* |
| 3 | A flag removed by a later edit stays in To review until reviewed | F03.01 | | |
| 4 | Workers get `403` on the review endpoints | F03.03 | F03.02 (the review endpoint's own test) | |
| 5 | The codebase has no email, SMS or push dependency apart from setup-link email | F03.03 | | |

**ux/screens/flagged.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `flagged.md › Access and navigation` | F03.03 | | |
| 2 | `flagged.md › View switch and count` | F03.03 | | |
| 3 | `flagged.md › To review list` | F03.03 | | |
| 4 | `flagged.md › Reviewed list` | F03.03 | | |
| 5 | `flagged.md › Loading and errors` | F03.03 | | |
| 6 | `flagged.md › Back and place` | F03.03 | | |
| 7 | `flagged.md › The note` | F03.03 | | |
| 8 | `flagged.md › Visual and device` | DoD | | |

**Group from an M0 screen file that is M3 work**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `app-shell.md › Badge` | F03.03 | E00 mentions it | Hand-off resolved; built by S03.03.03 |

### E04 Daily report, PDF and Word (M4)

**design.md §14 M4 Scope** (one sentence, split into its parts)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | The Report screen (4.7) | F04.02 | S04.01.01 (the first Report screen) | |
| 2 | Everything in §11.1–11.5: the shared report model | F04.01 | | |
| 3 | The PDFsharp-MigraDoc renderer | F04.01 | | |
| 4 | The Open XML SDK renderer | F04.01 | | |
| 5 | Audit entries for report downloads | F04.01 | | |

**design.md §14 M4 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | An automated test confirms the PDF text and the Word text both match the stored notes | F04.01 | | |
| 2 | Every character in a rendered PDF exists in the embedded font, including ☐ and ☑ (automated test) | F04.01 | | Open question on characters the font lacks (S04.01.02 Notes) |
| 3 | A day with 20 notes downloads in under 10 seconds | F04.01 | | |
| 4 | The PDF opens in Edge and Acrobat; the Word file opens in desktop and mobile Word, with participant headings in the desktop Navigation pane | F04.01 | | Manual checks |
| 5 | Edited, flagged, reviewed, submitted-next-day and past-day notes each show the right marker | F04.01 | | |
| 6 | Common items show the Every note group and the picked groups only, by group name in the configured order, each item ticked or not ticked; groups not picked do not appear (D47) | F04.01 | | |
| 7 | No drafts and no counts appear in either file | F04.01 | | |
| 8 | A day with no submitted notes, including a future date, returns `404 report.no_notes` and no file | F04.01 | | |
| 9 | Workers get `403` | F04.02 | F04.01 (the export endpoint's test) | |

**ux/screens/daily-report.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `daily-report.md › Routing and access` | F04.02 | | |
| 2 | `daily-report.md › Date field and stepping` | F04.02 | | |
| 3 | `daily-report.md › Has-notes and the empty day` | F04.02 | | |
| 4 | `daily-report.md › Downloads` | F04.02 | S04.01.01 (the Word half) | |
| 5 | `daily-report.md › Copy and layout` | DoD | | |
| 6 | `daily-report.md › Real devices, test environment with the production CSP (M4)` | F04.02 | S06.02.04 (the M6 re-run) | |

### E05 Participant export, audit completeness and retention (M5)

**design.md §14 M5 Scope**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | The participant record export (date range, earlier-versions option, PDF or Word) | F05.01 | | |
| 2 | Audit entries for every event in §5.4 | F05.02 | Every earlier story writes its own event (DoD) | |
| 3 | Append-only enforcement through `grow2notes_runtime` (§5.8) | F05.02 | S00.03.01, S00.03.03 and E02's migrations add the denies | |
| 4 | Archive and deactivate flows checked end to end | F05.03 | | |
| 5 | Written runbooks: retention and destruction (with the HPP 4.3 log and the backup note), and audit extraction for the operator (A28) | F05.04 | | Where the log is kept is an owner question (S05.04.01 Notes) |

**design.md §14 M5 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | A one-year export with earlier versions for one participant is generated in under 30 seconds and matches the app | F05.01 | | |
| 2 | A test triggers each audit event type and finds its entry | F05.02 | | |
| 3 | A test running as a member of `grow2notes_runtime` gets error 229 for `UPDATE` and `DELETE` on the append-only tables and for `DELETE` on `Note`, while deleting a `NoteDraft` still works | F05.02 | | |
| 4 | Both runbooks are written and read by a manager | F05.04 | | |

**ux/screens/record-export.md**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | `record-export.md › Arrival and defaults` | F05.01 | | |
| 2 | `record-export.md › Validation` | F05.01 | | |
| 3 | `record-export.md › Export request and results` | F05.01 | | |
| 4 | `record-export.md › Accessibility and layout` | F05.01 | | |
| 5 | `record-export.md › Real devices` | F05.01 | | |

### E06 Hardening and go-live (M6)

**design.md §14 M6 Scope**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Security: self-review against OWASP ASVS level 2; an OWASP ZAP baseline scan; dependency scanning (NuGet Audit, npm audit, Dependabot); CSP and the other security headers; rate limiting and lockout | F06.01 | S00.01.02 (NuGet Audit, npm audit and pinned actions in CI), S00.04.05 (Identity lockout) | S06.01.04 (the headers) is placed before the first screen (releases.md) |
| 2 | Accessibility: axe checks, keyboard-only use, and VoiceOver and TalkBack on the note form and the Report screen | F06.02 | | |
| 3 | Backups: 35-day point-in-time restore and monthly long-term backups for 12 months, all in Australian regions (A36) | F06.03 | | |
| 4 | Monitoring: uptime and error alerts to the operator | F06.04 | | |
| 5 | Set-up: production configuration entered from the current Word template (prompts become the guide prompts; standard tick items become the common items, sorted into groups) | F06.05 | Go-live checklist line 7 (the managers enter it) | |
| 6 | Cut-over on one set date (A40) | F06.06 | | |
| 7 | *Migration from Word* (paragraph): historical Word documents are not imported; they are gathered, checked and removed from laptops; Grow2Notes starts with no notes on the switch-over day | F06.06 | | Not a bullet; included for the 100% check |

**design.md §14 M6 Done when**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | No high or critical findings are open | F06.01 | | |
| 2 | A restore test has been done and recorded | F06.06 | S06.03.02 (the runbook) | When production first holds a note is open (F06.06) |
| 3 | The go-live checklist is complete and the switch-over date is recorded | F06.06 | | The date goes in [releases.md](releases.md) |

**design.md §14 Go-live checklist** (short names; F06.06 holds the checklist with an owner and evidence for each line)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Privacy policy updated (APP 1, HPP 5, HPP 9 wording, access and correction contact and timeframes) | F06.06 | | Checklist owner: the provider |
| 2 | Collection notice and consent wording checked (APP 5, HPP 1.4, Practice Standards) | F06.06 | | Checklist owner: the provider |
| 3 | Data breach response plan names Grow2Notes and the operator's contact | F06.06 | S06.06.01 (the runbook) | Checklist owner: the provider |
| 4 | Written hosting agreement between the provider and the developer's business signed (D40) | F06.06 | | Checklist owner: the owner and the provider, with a lawyer |
| 5 | Every staff member has accepted their invite and set up MFA on their own phone; at least two managers are active | F06.06 | S06.05.02 (the first manager) | Checklist owner: the provider's managers |
| 6 | Staff onboarding (about 30 minutes) | F06.06 | | Checklist owner: the provider |
| 7 | Participants, goals, common items (in their groups) and guide prompts entered by one manager and checked by another | F06.06 | S06.05.02 (the organisation in production) | Checklist owner: the provider's managers |
| 8 | Restore test of the latest production backup, recorded, copy deleted | F06.06 | S06.06.02 | Checklist owner: the operator |
| 9 | Every historical Word note gathered into organisation-controlled storage (D41), checked, then removed from personal devices (A39) | F06.06 | | Checklist owner: the provider |
| 10 | Where the gathered Word notes are kept is written down, and the Word template stops being used from the switch-over date | F06.06 | S06.06.03 (the switch-over date) | Checklist owner: the provider |

---

## R2 Admin MCP server

### E07 Admin MCP server

**mcp-server.md §1.2 In scope**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Users: invite (Support: workers only, D58); deactivate; reset sign-in or resend an invite; look-up: list users | F07.02 | S07.04.03–S07.04.05 (invite, reset, deactivate) | Started by S07.02.05 (list users); see *Rows to watch* |
| 2 | Participants: add; edit given name, family name or date of birth; archive; restore; look-ups: find by name, one participant with goals | F07.02 | S07.03.01 (add, edit), S07.04.06 (archive, restore) | Started by S07.02.04 (look-ups); see *Rows to watch* |
| 3 | Goals: add, reword, reorder, archive, restore (returned with the participant) | F07.03 | S07.02.04 | |
| 4 | Common item groups: add, rename, reorder, archive, restore (never the Every note group, A43); list groups with items | F07.03 | S07.02.05 | |
| 5 | Common items: add, reword, move to another group, reorder, archive, restore | F07.03 | | |
| 6 | Guide prompts: replace the text; read the current text | F07.03 | S07.02.05 | |

**mcp-server.md §10 Testing, automated**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Item 1: tool matrix (every tool by every caller and token case; the named Support cases; a tool with no row fails the build) | F07.02 | Every later tool adds its row (DoD) | |
| 2 | Item 2: the no-note-content tests (architecture rule, contract allow-list and deny-list, canary over every tool, `tools/list` snapshot) | F07.02 | Every later tool joins them (DoD) | |
| 3 | Item 3: separation of sign-in (cookie rejected at `/mcp`, bearer ignored at `/api`, the Origin rule, no CORS headers) | F07.02 | | |
| 4 | Item 4: OAuth (unknown client refused with no outbound request; PKCE, redirect and resource refusals; `iss`; code replay; refresh reuse, 30 minutes, 12 hours; no role claim; auth method `none` only; discovery contents) | F07.01 | | |
| 5 | Item 5: ending access (deactivation, reset, role change to worker, `admin signout-all`, registration removal: next call `401` and authorisations revoked) | F07.02 | | |
| 6 | Item 6: consent (worker refused with `access_denied`; registered details and hosts shown; loopback warning; Allow under the production CSP; `Mcp:Enabled = false` gives `404`) | F07.01 | | |
| 7 | Item 7: rules through MCP (last-manager rule, Every note rules, `config.group_archived`, `config.order_mismatch`, stale `expectedVersion`, `changed: false` on repeats) | F07.03 | S07.04.04, S07.04.05 (the last-manager rule, repeated deactivation) | See *Rows to watch* |
| 8 | Item 8: confirmation for each of the four tools (preview-then-confirm path and confirmation-form path) | F07.04 | | |
| 9 | Item 9: audit (`"via": "mcp"` and `client` on every change event; `mcp.read` with the returned IDs; the Support reason) | F07.02 | | |
| 10 | Item 10: rate limits (61st request in a minute gets `429`; 11th confirmed user change in an hour gets `rate_limited`) | F07.05 | | |
| 11 | Item 11: telemetry canary extended to MCP calls (names, emails and goal wording never in logs or telemetry) | F07.02 | | |

**mcp-server.md §10 Testing, manual**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Each approved assistant: at least two different assistants, phone and laptop, one task per tool area, the six behaviours recorded | F07.05 | | |
| 2 | Prompt-injection exploratory test, as a manager and as Support | F07.05 | | |
| 3 | MCP Inspector check of the protected resource metadata and the authorisation flow | F07.05 | | |

**mcp-server.md §11.1 Switch-on checklist** (F07.05 holds the checklist with an owner and evidence for each line)

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | Every automated test in §10 passes, and the manual tests are recorded | F07.05 | | Checklist owner: the operator |
| 2 | The provider has approved the AI assistants (and the operator's for Support, D57) after the §7.1 check, each through a business account with a data processing agreement | F07.05 | S07.05.06 (the §7.1 record for the operator's AI provider) | Checklist owner: the provider; the operator for its own account and agreement |
| 3 | Privacy policy and collection notice updated, naming the AI providers and their countries | F07.05 | S07.05.07 (draft wording), S07.05.10 (named providers) | Checklist owner: the provider |
| 4 | Staff told (staff privacy notice or handbook sentence) | F07.05 | S07.05.07 (draft wording), S07.05.10 (named providers) | Checklist owner: the provider |
| 5 | The provider's one-page decision (C5) written and checked by its lawyer or privacy officer | F07.05 | | Checklist owner: the provider |
| 6 | Hosting agreement amended (§7.5) | F07.05 | S07.05.07 (draft clauses), S07.05.10 (named providers) | Checklist owner: the owner and the provider |
| 7 | The operator has registered each approved assistant and no other; `admin mcp-client list` matches the written approval | F07.05 | S07.05.09 | Checklist owner: the operator |
| 8 | Account settings outside the app turned on, and what each account cannot do written down (C7) | F07.05 | S07.05.06 (what the operator's account offers) | Checklist owner: the provider; the operator for its own |
| 9 | Managers and the Support person have had the 10-minute briefing | F07.05 | S07.05.08 | Checklist owner: the operator |
| 10 | `Mcp:Enabled` set to `true` at an agreed time, and the switch-on date recorded | F07.05 | S07.05.09 | The date goes in [releases.md](releases.md) |

**mcp-server.md §12 Defaults MA1–MA12**

| # | Row | Owner | Also | Notes |
|---|---|---|---|---|
| 1 | MA1: only assistants the provider has approved can connect; the operator registers at written request and removes within one business day | F07.01 | | |
| 2 | MA2: Client ID Metadata Document URLs as client IDs, accepted only once registered; read once by the operator command; pre-registration fallback; public clients with PKCE; no Dynamic Client Registration | F07.01 | | |
| 3 | MA3: 10-minute access tokens with no role; rotating refresh tokens, 30 minutes sliding, refused 12 hours after sign-in | F07.01 | | |
| 4 | MA4: the Connect an AI assistant screen on every connection, with the registered name, company, data location and hosts; consent not remembered | F07.01 | | |
| 5 | MA5: no date of birth in any result | F07.02 | | |
| 6 | MA6: invite, reset sign-in, deactivate and archive participant always need confirmation (form where declared, otherwise preview then confirm) | F07.04 | | Owner question Q4 (mcp-server.md §12) blocks Ready |
| 7 | MA7: Support limits (workers only for invite, deactivate and reset; a reason on every user change; no app screens beyond sign-in and consent; granted in the app) | F07.04 | S07.01.01, S07.01.02 (granting Support, its screens) | |
| 8 | MA8: every MCP call audited, look-ups included, naming the assistant that received the data | F07.02 | | |
| 9 | MA9: rate limits, 60 requests a minute per user and 10 confirmed user changes an hour per user | F07.05 | | |
| 10 | MA10: no IP restriction; an `Origin` header must match an approved assistant's HTTPS redirect origin; no CORS headers | F07.02 | | |
| 11 | MA11: MCP off by default (`Mcp:Enabled = false`) until switch-on; turned off within one business day of the provider asking | F07.01 | S07.05.09 (switch-on) | |
| 12 | MA12: no edits to a user's name, email or role, and no reactivation, through MCP | F07.04 | | |
