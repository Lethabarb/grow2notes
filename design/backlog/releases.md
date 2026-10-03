# Grow2Notes releases

The backlog order, and for each release its goal, epics, size, reference stories, velocity, forecast and burn-up
([README §4](README.md#4-id-scheme), [§7](README.md#7-estimation)). Release 1 is the app (design.md M0–M6), ending
at go-live; Release 2 is the admin MCP server, built after go-live (D55).

Status, points and sprint live only in each story's fields, in its feature file. A story's `sprint` field stays empty
until the sprint planning that takes the story, so this file's forecast tables are the only record of where the
unplanned stories are expected to fall. This file holds IDs and titles, the forecast and the burn-up. Scope coverage
is in [coverage.md](coverage.md).

Release planning: 4 October 2026, revised the same day after review. Re-forecast at every sprint review (README §7.3).

## Backlog order

The Product Backlog order: every item not yet done, R1 then R2. The owner reorders it at refinement (README §4).

The order respects every `depends_on`. Otherwise it follows the milestones (M0 to M6, then R2) and, within each epic,
groups the stories into sprint-sized vertical slices, mostly in the order they were written. The notable changes:

- **S06.01.04** (security headers and CSP) comes in Sprint 03, straight after the session check (S00.04.01) and
  before the first screen, so every response carries the design.md §9.7 headers from then on, as the Definition of
  Done says. It stays in F06.01, which owns M6's "CSP and the other security headers"; only its place in the order
  changes. The real-device checks under the production CSP (S04.02.05, S05.01.04) depend on it.
- **S00.01.03–S00.01.05** (the shared look, loading and error handling, and the copy module) come just before the
  first screen, S00.04.02, in Sprint 04, with the operator's bootstrap (S00.03.05) that the first screen needs.
- **S00.05.02** (keeping your place between screens) comes after S00.06.01, because it is checked with the Users list,
  the user detail page and the back link.
- **S00.02.05** (the delete lock on the test database) comes after the user stories, in Sprint 09; only S00.02.04
  depends on it.
- **S00.02.04** (production) comes last in E00, so the owner has until Sprint 09 to fix the production domain (A38).
- **In E06**, the runbooks and the security stories come first: Dependabot (S06.01.03), the rate limits (S06.01.05),
  the ZAP scan (S06.01.02) and the ASVS self-review (S06.01.01). Then production is checked, backed up and watched by
  alerts (S06.05.01, S06.03.01, S06.04.01), and only then is the provider's organisation created (S06.05.02), so no
  real personal information reaches production before the M6 security work is done and no high or critical finding is
  open (D1). The accessibility checks follow, in the test environment, while the provider works in production. At the
  likely rate this leaves the provider about three weeks of production before go-live for the go-live checklist lines
  that need it (lines 3, 5, 6 and 7); a later switch-over date gives more (owner questions, Sprint 27).
- **In R2**, the spike S07.01.03 comes first, and so do S07.05.06 and S07.05.07 (the check of the operator's own AI
  provider; the privacy and hosting wording, with placeholders for the AI providers), so the provider's approvals and
  legal checks run alongside the build (mcp-server.md §11.1 lines 2–6). S07.03.02 comes before S07.03.01, because it
  adds the archived-participant guard that S07.03.01 uses. S07.05.10 (naming the approved AI providers) comes after
  the manual tests, because the provider approves its assistants after its §7.1 check, which uses their results.

### R1

1. S00.01.01 Serve the SPA and the API from one origin
2. S00.03.01 Database context, conventions and first migration
3. S00.01.02 Build and test every pull request
4. S00.02.01 Bootstrap Azure for both environments
5. S00.02.02 Test environment resources
6. S00.02.03 Deploy to test from main
7. S00.02.06 Server telemetry with IDs only
8. S00.03.02 Tenant isolation and its tests
9. S00.03.03 Audit writer
10. S00.03.04 Deny by default, CSRF and the endpoint matrix
11. S00.04.01 Session checked on every request
12. S06.01.04 Security headers and CSP on every response
13. S00.03.05 Bootstrap the organisation and first manager
14. S00.01.03 Shared look, buttons and page status
15. S00.01.04 Shared loading, failure and form-error handling
16. S00.01.05 Copy module, date formats and wording checks
17. S00.04.02 Set up my account with a passkey
18. S00.04.03 Set up my account with a password and authenticator app
19. S00.04.04 Sign in with a passkey
20. S00.04.05 Sign in with email, password and code
21. S00.05.01 Navigation for my role
22. S00.05.03 Start-up, refresh and whole-page messages
23. S00.04.06 Sign out
24. S00.04.07 Warning before the idle sign-out
25. S00.02.07 Setup-link email
26. S00.06.01 See who can sign in
27. S00.05.02 Keep my place when I move between screens
28. S00.06.02 Invite a user
29. S00.06.03 Resend an invite
30. S00.04.08 Sign in again where I was
31. S00.06.04 Reset someone's sign-in
32. S00.06.05 Deactivate and reactivate a user
33. S00.06.06 Change a user's name, email or role
34. S00.03.06 Break-glass reset and sign-out of everyone
35. S00.02.05 Database delete lock
36. S00.02.04 Production environment and manual deploy
37. S01.01.01 Shared configuration handling
38. S01.02.01 See and find participants
39. S01.02.02 Add a participant
40. S01.02.03 Correct a participant's details
41. S01.02.04 Archive and restore a participant
42. S01.02.05 Add goals for a participant
43. S01.02.06 Reword a goal
44. S01.02.07 Reorder goals
45. S01.02.08 Archive and restore goals
46. S01.03.01 Every note group, built in
47. S01.03.02 Add and reword common items
48. S01.03.03 Reorder common items in a group
49. S01.03.04 Archive and restore common items
50. S01.03.05 Add and rename groups
51. S01.03.06 Reorder groups
52. S01.03.07 Archive and restore groups
53. S01.03.08 Move a common item to another group
54. S01.04.01 Set the guide prompts
55. S01.04.02 Warning before leaving unsaved prompts
56. S02.02.01 Melbourne date tests across daylight saving
57. S02.01.01 See every active participant on Today
58. S02.01.02 Find a participant
59. S02.02.02 Start a note and autosave the Guided notes
60. S02.02.03 Tick the participant's goals
61. S02.03.01 Submit a note after a name check
62. S02.01.03 See today's note status on each row
63. S02.01.04 Continue an unfinished draft from Today
64. S02.02.04 Keep autosaving when the connection drops
65. S02.02.05 Keep the draft through sign-out and leaving the form
66. S02.02.08 Discard a draft
67. S02.02.06 Carry on in another tab or on another device
68. S02.02.07 One note per participant per day
69. S02.03.02 Flag a note for a manager
70. S02.03.03 Submit a draft from an earlier day
71. S02.04.01 Read a submitted note
72. S02.05.01 Tick the Every note items
73. S02.05.02 Tick which groups happened and their items
74. S02.05.03 Start with the last note's group picks
75. S02.05.04 Re-check ticks when groups or items change before the first save
76. S02.04.02 Browse a participant's past notes
77. S02.04.03 See someone else's draft, and discard it as a manager
78. S02.06.01 Edit my own submitted note
79. S02.06.02 Edit any submitted note as a manager
80. S02.06.03 Handle two people editing the same note
81. S02.07.01 See a note's versions
82. S02.07.02 Read one version in full
83. S02.08.01 Choose the date for a past-day note
84. S02.08.02 Write and submit a forgotten past-day note
85. S03.01.01 Send a submitted flag to To review
86. S03.01.02 Return a re-flagged note to To review
87. S03.02.01 Mark a flagged note reviewed
88. S03.03.01 See the flagged notes to review
89. S03.02.02 See whether a flag has been reviewed
90. S03.02.03 Handle a review conflict without losing the comment
91. S03.03.02 Switch to the reviewed notes
92. S03.03.03 See the To review count on the Flagged menu
93. S03.03.04 Show older reviewed notes
94. S03.03.05 Keep my place in the Flagged lists
95. S04.01.01 Download a day's notes as a Word file
96. S04.01.02 Download the day's notes as a PDF
97. S04.01.03 Show common items by picked group
98. S04.01.04 Show each note's markers
99. S04.01.05 Mark every page as confidential
100. S04.02.01 Move a day at a time
101. S04.01.06 Download a full day in under 10 seconds
102. S04.02.02 Pick a date in the date field
103. S04.02.03 See when a day has no submitted notes
104. S04.02.04 Know whether a download worked
105. S04.02.05 Save the report on a phone
106. S05.01.01 Export a participant's record
107. S05.01.02 Export form checks and failures
108. S05.01.03 Include earlier versions in the export
109. S05.01.04 One-year export in under 30 seconds on real devices
110. S05.02.01 Every audit event type is written and found
111. S05.02.02 Append-only permissions tested in the database
112. S05.03.01 Archiving keeps every past record unchanged
113. S05.03.02 A deactivated person is signed out everywhere and their notes stay
114. S05.04.01 Retention and destruction runbook
115. S05.04.02 Audit extraction runbook
116. S06.03.02 Restore drill runbook
117. S06.06.01 Breach response runbook
118. S06.01.03 Dependabot alerts and weekly updates
119. S06.01.05 Rate limits
120. S06.01.02 OWASP ZAP baseline scan
121. S06.01.01 OWASP ASVS level 2 self-review
122. S06.05.01 Production environment ready and checked
123. S06.03.01 Backup retention set and checked in production
124. S06.04.01 Uptime and error alerts reach the operator
125. S06.05.02 Provider's organisation and first manager in production
126. S06.02.01 axe and keyboard-only pass on every screen
127. S06.02.02 Zoom, text size and contrast checks
128. S06.02.03 VoiceOver and TalkBack on the note form
129. S06.02.04 VoiceOver and TalkBack on the Report screen
130. S06.02.05 Button words checked with five workers
131. S06.06.02 Restore test of the production backup
132. S06.06.03 Cut-over on the set date

### R2

1. S07.01.03 Spike: OpenIddict settings for approved assistants
2. S07.01.01 Invite a Support account
3. S07.01.02 Support sign-in and landing page
4. S07.05.06 The operator's own AI provider checked
5. S07.05.07 Privacy and hosting agreement wording
6. S07.01.04 Authorisation server, off switch and discovery
7. S07.01.05 Register an approved assistant
8. S07.01.06 Connect an AI assistant
9. S07.01.07 Refuse workers and warn about desktop assistants
10. S07.01.08 Stay connected within the app's session limits
11. S07.02.01 MCP endpoint with checks on every request
12. S07.02.02 Access ends on the next call
13. S07.02.03 Audit every MCP call
14. S07.02.04 Look up participants and their goals
15. S07.02.05 Look up users, common items and guide prompts
16. S07.02.06 Architecture, contract and snapshot rules
17. S07.02.07 Canary tests over every tool
18. S07.03.02 Add and reword goals
19. S07.03.01 Add and correct participants
20. S07.03.03 Reorder, archive and restore goals
21. S07.03.04 Add and rename common item groups
22. S07.03.05 Reorder, archive and restore common item groups
23. S07.03.06 Add, reword and move common items
24. S07.03.07 Reorder, archive and restore common items
25. S07.03.08 Replace the guide prompts
26. S07.04.01 Preview, then confirm
27. S07.05.04 MCP sections in the breach and audit runbooks
28. S07.04.02 Confirmation forms where the assistant supports them
29. S07.04.03 Invite a user
30. S07.04.04 Reset a user's sign-in or resend an invite
31. S07.04.05 Deactivate a user
32. S07.04.06 Archive and restore a participant
33. S07.05.01 Rate limits for assistants
34. S07.05.02 Security controls checked against the design
35. S07.05.05 Monthly and quarterly MCP checks
36. S07.05.03 Manual tests with two approved assistants
37. S07.05.10 Name the approved AI providers in the wording
38. S07.05.08 Briefing for managers and the Support person
39. S07.05.09 Switch on in production

---

## R1 The app

**Goal:** the provider's managers and workers write, submit, flag, review and download participant notes in
Grow2Notes instead of the Word template, from one switch-over date, with managers in control of users and
configuration (D55, A40, design.md §1, §14).

**Done when:** README §9 *Release* R1: every epic below is done, the go-live checklist in F06.06 is complete with an
owner and evidence for every line, and the build that passed in test is deployed to production.

**Switch-over date:** not set yet. S06.06.03 records it here.

### Epics

A coverage.md row that more than one feature delivers is owned by the feature that starts it, in both releases
(coverage.md, *How to read the tables*), and an epic is done only when all its features are done (README §2.3 rule 4).
So some epics can close only after their own last forecast sprint, when a later story finishes one of their rows
(coverage.md, *Rows to watch*). The last column shows when.

| Epic | Title | Milestone | Stories | Points | Forecast sprints | Last forecast sprint ends | Can close (the story that finishes its last row) |
|---|---|---|---|---|---|---|---|
| E00 | Skeleton, hosting and sign-in | M0 | 35 | 112 | 01–09 | 14 Feb 2027 | 23 May 2027 (S02.02.08, Sprint 16) |
| E01 | Participants, goals, common items and guide prompts | M1 | 19 | 53 | 09–13 | 11 Apr 2027 | 26 Sep 2027 (S05.01.01, Sprint 25) |
| E02 | Note form, autosave, submit and edit history | M2 | 29 | 85 | 13–20 | 18 Jul 2027 | 26 Sep 2027 (S05.01.01, Sprint 25) |
| E03 | Flag review | M3 | 10 | 30 | 20–22 | 15 Aug 2027 | 12 Sep 2027 (S04.01.04, Sprint 24) |
| E04 | Daily report, PDF and Word | M4 | 11 | 35 | 23–25 | 26 Sep 2027 | 26 Sep 2027 |
| E05 | Participant export, audit completeness and retention | M5 | 10 | 29 | 25–27 | 24 Oct 2027 | 24 Oct 2027 |
| E06 | Hardening and go-live | M6 | 18 | 40 | 03, 27–30 | 5 Dec 2027 | 5 Dec 2027 |
| **Total** | | | **132** | **384** | **01–30** | | |

R1's 132 stories are 89 user stories and 43 enablers; there are no spikes.

### Reference stories

Sized against, from now on (README §7.1). Both are full vertical slices: screen, API, data, audit and tests.

| Points | Story | Why it is typical |
|---|---|---|
| **2** | S01.02.06 Reword a goal | One inline edit on a screen, one `PUT` with `If-Match` and a `412`, one audit event with the old and new value, and its tests. Most 2-point setup stories in E01 have this shape. |
| **5** | S00.04.05 Sign in with email, password and code | Two steps on one screen, two endpoints over Identity, four audit events, lockout and timing tests with a fake clock. The larger flows (S02.03.01 Submit, S03.02.01 Mark reviewed, S04.01.01 the Word report) are of this size. |

The epics were sized by their authors before these were recorded. At review on 4 October 2026, four stories were
re-sized after the work they repeated from E00 was taken out: S02.02.01 (2 to 1), S06.01.03 (2 to 1), S06.03.01
(2 to 1) and S06.05.01 (3 to 2). At each refinement, compare the next two sprints' stories with these two and re-size
only a story that is clearly a different size.

### Velocity

- **Sprint 01 is planned by hours** (README §7.2): about 29 focused hours (40 hours of build time, less 4 for sprint
  events and a 20% buffer of 7). The three chosen stories' tasks fill those hours, and their **13 points are the sprint
  1 forecast, not a velocity** (README §7.2 step 3). See [sprint-01.md](sprints/sprint-01.md).
- **Until three sprints are done**, this forecast uses 13 points a sprint as the *likely* rate and **10.4** as the
  *cautious* rate, which is 20% lower: velocity typically moves by about 20% either way from one sprint to the next
  (README §7.2).
- **From Sprint 04 planning onwards**, the likely rate is the average of the last three done sprints and the cautious
  rate is the slowest of them (README §7.3). Use them only for this forecast, never to fill a sprint.

### Forecast, sprint by sprint

At the likely rate of about 13 points. Each sprint runs from a Monday to the second Sunday after it (D53). Sprint sizes
vary from 11 to 14 points so each sprint stays one coherent slice; the last sprint is small because it is the cut-over.
This table is the only record of the forecast. Each sprint planning fills in the chosen stories' `sprint` fields and
moves the rest of the table on.

| Sprint | Dates | Goal | Stories, in order | Points |
|---|---|---|---|---|
| 01 | 12 Oct – 25 Oct 2026 | Every change to Grow2Notes is built and tested automatically before it is accepted, starting with the empty app and its database. | S00.01.01, S00.03.01, S00.01.02 | 13 |
| 02 | 26 Oct – 8 Nov 2026 | Every accepted change runs in the test environment in Melbourne with no manual step, and the operator can see its health with no personal details. | S00.02.01, S00.02.02, S00.02.03, S00.02.06 | 14 |
| 03 | 9 Nov – 22 Nov 2026 | Each organisation's data is kept apart, every request is checked for its session and role, changes can be audited, and every response carries the security headers and the strict content security policy. | S00.03.02, S00.03.03, S00.03.04, S00.04.01, S06.01.04 | 13 |
| 04 | 23 Nov – 6 Dec 2026 | The operator can create the organisation and its first manager, and every screen to come has one look, one way to load and report problems, and checked wording. | S00.03.05, S00.01.03, S00.01.04, S00.01.05 | 12 |
| 05 | 7 Dec – 20 Dec 2026 | The first manager can set up their account from the setup link with a passkey, or with a password and authenticator app, and sign in with a passkey, on a phone or a laptop. | S00.04.02, S00.04.03, S00.04.04 | 13 |
| 06 | 21 Dec 2026 – 3 Jan 2027 | Staff can sign in with email, password and code, see the navigation for their role, get plain messages when the app cannot start, and sign out. | S00.04.05, S00.05.01, S00.05.03, S00.04.06 | 13 |
| 07 | 4 Jan – 17 Jan 2027 | Staff are warned before an idle sign-out, setup links can be emailed, and a manager can see who can sign in and keep their place between the list and a person's details. | S00.04.07, S00.02.07, S00.06.01, S00.05.02 | 12 |
| 08 | 18 Jan – 31 Jan 2027 | A manager can invite a worker or manager, resend an invite, reset someone's sign-in, and deactivate or reactivate a user; someone signed out mid-task signs in again where they were. | S00.06.02, S00.06.03, S00.04.08, S00.06.04, S00.06.05 | 14 |
| 09 | 1 Feb – 14 Feb 2027 | A manager can change a user's name, email or role; the operator can break glass; the test database is locked against deletion; production is ready for manual deploys; and the configuration screens share one set of rules. | S00.06.06, S00.03.06, S00.02.05, S00.02.04, S01.01.01 | 13 |
| 10 | 15 Feb – 28 Feb 2027 | A manager can see, find, add, correct, archive and restore participants, and add their goals. | S01.02.01, S01.02.02, S01.02.03, S01.02.04, S01.02.05 | 14 |
| 11 | 1 Mar – 14 Mar 2027 | A manager can reword, reorder and archive goals, so a participant can be set up with five goals on a phone, and keeps the common items in the built-in Every note group. | S01.02.06, S01.02.07, S01.02.08, S01.03.01 | 12 |
| 12 | 15 Mar – 28 Mar 2027 | A manager can add, reword, reorder and archive common items, and add, rename and reorder groups. | S01.03.02, S01.03.03, S01.03.04, S01.03.05, S01.03.06 | 12 |
| 13 | 29 Mar – 11 Apr 2027 | A manager can archive and restore groups, move items between them and set the guide prompts, and the Melbourne date is proven across daylight saving. | S01.03.07, S01.03.08, S01.04.01, S01.04.02, S02.02.01 | 13 |
| 14 | 12 Apr – 25 Apr 2027 | Everyone sees every active participant on Today and can find one, and a worker can start today's note, tick goals and have the Guided notes save as they type. | S02.01.01, S02.01.02, S02.02.02, S02.02.03 | 13 |
| 15 | 26 Apr – 9 May 2027 | A worker can submit a note after a name check, Today shows each note's status and any unfinished drafts, and autosave keeps going through a dropped connection. | S02.03.01, S02.01.03, S02.01.04, S02.02.04 | 13 |
| 16 | 10 May – 23 May 2027 | A draft survives a sign-out or leaving the form, can be discarded and carries on in another tab or on another device; one note per participant per day holds; and a note can be flagged for a manager. | S02.02.05, S02.02.08, S02.02.06, S02.02.07, S02.03.02 | 13 |
| 17 | 24 May – 6 Jun 2027 | A draft from an earlier day can be submitted, a submitted note can be read, and a worker ticks the Every note items and the groups that happened. | S02.03.03, S02.04.01, S02.05.01, S02.05.02 | 13 |
| 18 | 7 Jun – 20 Jun 2027 | A new note starts with the last note's group picks, ticks are re-checked when groups or items change, and staff browse a participant's past notes, with managers able to discard someone else's draft. | S02.05.03, S02.05.04, S02.04.02, S02.04.03 | 11 |
| 19 | 21 Jun – 4 Jul 2027 | The author or a manager can edit a submitted note as a new version, two editors cannot overwrite each other, and managers see a note's versions. | S02.06.01, S02.06.02, S02.06.03, S02.07.01 | 13 |
| 20 | 5 Jul – 18 Jul 2027 | Managers can read any version in full and write a forgotten past-day note, and a flagged note goes to To review, also when it is flagged again. | S02.07.02, S02.08.01, S02.08.02, S03.01.01, S03.01.02 | 12 |
| 21 | 19 Jul – 1 Aug 2027 | Managers see the flagged notes to review, oldest first, mark one reviewed with a comment, and see whether a flag has been reviewed. | S03.02.01, S03.03.01, S03.02.02 | 13 |
| 22 | 2 Aug – 15 Aug 2027 | A review conflict loses no comment, and managers switch to the reviewed notes, see the To review count on the menu, show older reviewed notes and keep their place. | S03.02.03, S03.03.02, S03.03.03, S03.03.04, S03.03.05 | 13 |
| 23 | 16 Aug – 29 Aug 2027 | A manager can download a day's notes as a Word or PDF file, with common items shown by picked group. | S04.01.01, S04.01.02, S04.01.03 | 13 |
| 24 | 30 Aug – 12 Sep 2027 | The daily report shows each note's markers, is marked confidential on every page and downloads a full day in under 10 seconds, and managers step a day at a time or pick a date. | S04.01.04, S04.01.05, S04.02.01, S04.01.06, S04.02.02 | 14 |
| 25 | 13 Sep – 26 Sep 2027 | Managers see when a day has no notes, know whether a download worked, can save the report on a phone, and can export one participant's record. | S04.02.03, S04.02.04, S04.02.05, S05.01.01 | 13 |
| 26 | 27 Sep – 10 Oct 2027 | The record export checks its form, includes earlier versions when asked and exports a year in under 30 seconds; every audit event is written and found, and the database refuses changes to history. | S05.01.02, S05.01.03, S05.01.04, S05.02.01, S05.02.02 | 14 |
| 27 | 11 Oct – 24 Oct 2027 | Archiving and deactivating keep every record, and the retention, audit extraction and restore drill runbooks are written. | S05.03.01, S05.03.02, S05.04.01, S05.04.02, S06.03.02 | 13 |
| 28 | 25 Oct – 7 Nov 2027 | The breach response runbook is written, and Dependabot, the rate limits, a ZAP scan and the OWASP ASVS level 2 self-review are done, with no high or critical finding open. | S06.06.01, S06.01.03, S06.01.05, S06.01.02, S06.01.01 | 13 |
| 29 | 8 Nov – 21 Nov 2027 | Production is checked, backed up and watched by alerts, the provider's organisation and first manager are created, and every screen passes the axe, keyboard, zoom, text size and contrast checks. | S06.05.01, S06.03.01, S06.04.01, S06.05.02, S06.02.01, S06.02.02 | 12 |
| 30 | 22 Nov – 5 Dec 2027 | The note form and Report screen pass VoiceOver and TalkBack, five workers check the button words, the production backup is shown to restore, and every staff member switches to Grow2Notes on the set date. | S06.02.03, S06.02.04, S06.02.05, S06.06.02, S06.06.03 | 10 |

### Forecast range for go-live

| | Rate | Sprints for 384 points | R1 done and go-live by |
|---|---|---|---|
| **Likely** | 13 a sprint | 30 | the end of Sprint 30, **Sunday 5 December 2027** |
| **Cautious** | 10.4 a sprint | 37 | the end of Sprint 37, **Sunday 12 March 2028** |

The switch-over date itself is agreed by the owner and the managers (A40). The likely date falls just before
Christmas; the owner may prefer an earlier December date, if the work allows, or a date after the January holidays.

### Burn-up

One row per sprint, filled in at each sprint review (README §7.3). *Scope* is R1's total points at the end of that
sprint; a rise shows new scope, not slow work.

| Sprint | Ends | Points done | Stories done | Total points done | Scope (points) |
|---|---|---|---|---|---|
| Start | 12 Oct 2026 | | | 0 | 384 |
| 01 | 25 Oct 2026 | | | | |
| 02 | 8 Nov 2026 | | | | |
| 03 | 22 Nov 2026 | | | | |
| 04 | 6 Dec 2026 | | | | |
| 05 | 20 Dec 2026 | | | | |
| 06 | 3 Jan 2027 | | | | |
| 07 | 17 Jan 2027 | | | | |
| 08 | 31 Jan 2027 | | | | |
| 09 | 14 Feb 2027 | | | | |
| 10 | 28 Feb 2027 | | | | |
| 11 | 14 Mar 2027 | | | | |
| 12 | 28 Mar 2027 | | | | |
| 13 | 11 Apr 2027 | | | | |
| 14 | 25 Apr 2027 | | | | |
| 15 | 9 May 2027 | | | | |
| 16 | 23 May 2027 | | | | |
| 17 | 6 Jun 2027 | | | | |
| 18 | 20 Jun 2027 | | | | |
| 19 | 4 Jul 2027 | | | | |
| 20 | 18 Jul 2027 | | | | |
| 21 | 1 Aug 2027 | | | | |
| 22 | 15 Aug 2027 | | | | |
| 23 | 29 Aug 2027 | | | | |
| 24 | 12 Sep 2027 | | | | |
| 25 | 26 Sep 2027 | | | | |
| 26 | 10 Oct 2027 | | | | |
| 27 | 24 Oct 2027 | | | | |
| 28 | 7 Nov 2027 | | | | |
| 29 | 21 Nov 2027 | | | | |
| 30 | 5 Dec 2027 | | | | |

---

## R2 Admin MCP server

**Goal:** managers, and the operator's Support person, make the same admin changes as the Manage screens through an AI
assistant the provider has approved, signing in with their own Grow2Notes account and MFA, under the same rules and
audit trail, with no note content ever returned (D48–D51, D55–D59, mcp-server.md).

**Done when:** README §9 *Release* R2: E07 is done, the mcp-server.md §11.1 switch-on checklist in F07.05 is
complete, `Mcp:Enabled` is on in production, and the date is recorded below.

**Switch-on date:** not set yet. S07.05.09 records it here.

### Epics

| Epic | Title | Stories | Points | Forecast sprints | Last forecast sprint ends |
|---|---|---|---|---|---|
| E07 | Admin MCP server | 39 | 110 | 31–39 | 9 Apr 2028 |
| **Total** | | **39** | **110** | | |

R2's 39 stories are 20 user stories, 18 enablers and 1 spike (S07.01.03).

### Reference stories

| Points | Story | Why it is typical |
|---|---|---|
| **2** | S07.03.04 Add and rename common item groups | Two tools that call Release 1's existing handlers, each with its tool matrix row, `tools/list` snapshot update, audit entry with `"via": "mcp"`, and the no-note-content test. Most setup tools have this shape. (S07.03.02 was the reference until review, when it took on the archived-participant guard and became a 3.) |
| **5** | S07.02.04 Look up participants and their goals | The first look-up tools: the contract allow-list, `mcp.read` audit with the returned IDs, no date of birth, organisation scoping and the canary. Later tools reuse what it builds. |

At review on 4 October 2026: S07.03.02 went from 2 to 3 points (the archived-participant guard), S07.05.06 from 2 to 1
(only the written §7.1 record is pointed work), and S07.05.10 (1 point) was split from S07.05.07.

### Velocity

R2 starts with about 30 sprints of R1 history, so its forecast will use real velocity (README §7.3). Until then it
uses the same 13 (likely) and 10.4 (cautious). After go-live, routine operations (design.md §10.9) and support come
out of the same 20 hours (README §7.2), so the cautious rate is the more realistic one for R2 until history shows
otherwise.

### Assumptions for R2, until the owner answers

Both questions are in the owner questions table, needed by Release 2 planning (Sprint 31 planning, 6 Dec 2027).

- **Q4 keeps the MA6 default** (mcp-server.md §12): confirmation by the assistant, through its confirmation form where
  it has one, otherwise preview then confirm. A confirmation page in Grow2Notes would add a screen and stored pending
  changes through decisions.md and re-size S07.04.01–S07.04.06, so the dates below would move.
- **The Support role ships before switch-on.** `Mcp:Enabled` covers only `/mcp`, `/oauth/*`, the consent screen and
  discovery (mcp-server.md §3.1). The Support role on the production Users screen and the changed sign-in, setup and
  session policies (S07.01.01, S07.01.02) are not behind it, so the monthly production deploys after go-live
  (design.md §10.9) ship them from Sprint 31: before switch-on, the amended hosting agreement (mcp-server.md §7.5,
  switch-on line 6) and S07.05.02's security check. The forecast assumes the owner accepts that. Hiding the Support
  option while `Mcp:Enabled` is false would need a decisions.md entry and then a criterion in S07.01.01.

### Forecast, sprint by sprint

At the likely rate, starting straight after the likely go-live, on the two assumptions above.

| Sprint | Dates | Goal | Stories, in order | Points |
|---|---|---|---|---|
| 31 | 6 Dec – 19 Dec 2027 | A manager can invite a Support account that signs in to its own page, the OpenIddict settings are decided, and the provider has the operator's AI provider check and the draft privacy and hosting wording to start its checks. | S07.01.03, S07.01.01, S07.01.02, S07.05.06, S07.05.07 | 11 |
| 32 | 20 Dec 2027 – 2 Jan 2028 | A manager or the Support person can connect an approved AI assistant through the Connect an AI assistant screen; workers are refused. | S07.01.04, S07.01.05, S07.01.06, S07.01.07 | 13 |
| 33 | 3 Jan – 16 Jan 2028 | A connected assistant reaches the MCP endpoint within the app's session limits, every call is checked and audited, and access ends on the next call. | S07.01.08, S07.02.01, S07.02.02, S07.02.03 | 13 |
| 34 | 17 Jan – 30 Jan 2028 | An assistant can look up participants, goals, users, common items and guide prompts, and tests prove no tool returns note content. | S07.02.04, S07.02.05, S07.02.06, S07.02.07 | 14 |
| 35 | 31 Jan – 13 Feb 2028 | Through an assistant, a manager adds and corrects participants and keeps their goals and the common item groups, and nothing changes for an archived participant. | S07.03.02, S07.03.01, S07.03.03, S07.03.04, S07.03.05 | 14 |
| 36 | 14 Feb – 27 Feb 2028 | Through an assistant, a manager keeps the common items and replaces the guide prompts; changes that need confirmation show a preview first; the runbooks cover MCP. | S07.03.06, S07.03.07, S07.03.08, S07.04.01, S07.05.04 | 13 |
| 37 | 28 Feb – 12 Mar 2028 | Invite, reset sign-in and deactivate work through an assistant, always after a confirmation. | S07.04.02, S07.04.03, S07.04.04, S07.04.05 | 13 |
| 38 | 13 Mar – 26 Mar 2028 | Archiving a participant needs confirmation, assistants are rate-limited, the security controls are checked, and two approved assistants have been tried by hand. | S07.04.06, S07.05.01, S07.05.02, S07.05.05, S07.05.03 | 14 |
| 39 | 27 Mar – 9 Apr 2028 | The approved AI providers are named in the privacy and hosting wording, managers and the Support person are briefed, and MCP is switched on in production. | S07.05.10, S07.05.08, S07.05.09 | 5 |

### Forecast range for switch-on

| | Rate | Sprints for 110 points | Starts | R2 done and switch-on by |
|---|---|---|---|---|
| **Likely** | 13 a sprint | 9 (Sprints 31–39) | 6 Dec 2027 | the end of Sprint 39, **Sunday 9 April 2028** |
| **Cautious** | 10.4 a sprint | 11 (Sprints 38–48) | 13 Mar 2028, after the cautious go-live | the end of Sprint 48, **Sunday 13 August 2028** |

Switch-on also needs the provider's approvals, policy updates and the amended hosting agreement (mcp-server.md §11.1
lines 2–6), which carry no points. They can start as soon as S07.05.07 hands over the draft wording (Sprint 31);
S07.05.10 names the approved AI providers in it once the provider has approved them (Sprint 39).

### Burn-up

| Sprint | Ends | Points done | Stories done | Total points done | Scope (points) |
|---|---|---|---|---|---|
| Start | 6 Dec 2027 (forecast) | | | 0 | 110 |
| 31 | 19 Dec 2027 | | | | |
| 32 | 2 Jan 2028 | | | | |
| 33 | 16 Jan 2028 | | | | |
| 34 | 30 Jan 2028 | | | | |
| 35 | 13 Feb 2028 | | | | |
| 36 | 27 Feb 2028 | | | | |
| 37 | 12 Mar 2028 | | | | |
| 38 | 26 Mar 2028 | | | | |
| 39 | 9 Apr 2028 | | | | |

---

## Assumptions

1. **Start and cadence.** Sprint 01 starts on Monday 12 October 2026; the method sets no start date. Sprints are back
   to back, two weeks each, Monday to Sunday, and are never extended (D53, README §10).
2. **Capacity.** One developer, about 20 hours a week (D54), which gives about 29 focused hours a sprint (README §7.2).
   The forecast deducts **no leave and no public holidays**; each sprint planning takes them off. Sprint 06
   (21 December 2026 – 3 January 2027) and Sprint 32 (20 December 2027 – 2 January 2028) span Christmas and New Year.
   Each sprint of time off moves every later date by two weeks.
3. **Rate.** Sprint 01's 13 points, planned by hours, stand in for velocity until three sprints are done; the cautious
   rate is 20% lower (README §7.2, §7.3).
4. **Owner questions are answered in time.** The forecast assumes each open question is settled before the sprint
   planning that takes its story (table below). A late answer moves that story, not the release, if another Ready story
   can take its place.
5. **Bootstrap exception in E00.** Until S00.02.03 deploys `main` to test (Sprint 02), stories are checked locally and
   in CI, and that first deploy re-checks them in test, including the two Sprint 01 criteria parts that need Azure
   (S00.01.02's Bicep artifact and S00.03.01's run on Azure SQL). Until S00.01.02's workflow exists (Sprint 01), the
   stories before it count as done once its first run on `main` is green ([sprint-01.md](sprints/sprint-01.md)). The
   stories before S06.01.04 (Sprint 03) have no screen, and its header test covers the response kinds they add, so the
   Definition of Done's headers bullet holds from the first screen on.
6. **GitHub plan.** Whether the repository is public or private, and if private whether GitHub Pro is taken, is an
   owner question needed at Sprint 01 planning (table below). The forecast assumes `main` can be protected from Sprint
   01: on a private repository that needs GitHub Pro, which design.md §10.2 already budgets for the production
   environment's branch rule (S00.01.02's first criterion, S00.02.04).
7. **No pilot.** A40 allows a short pilot before the set date; it is not in the backlog unless the owner asks
   (S06.06.03 Notes).
8. **Not in the forecast:** the .NET 10 to next-LTS upgrade, due by November 2028 (design.md §10.9), is routine
   operations. It falls after both likely dates but close to the cautious R2 date.

### Owner questions, by when they are needed

Taken from the stories' Notes and the screen files' open questions. "Needed by" is the planning of the first sprint
that takes an affected story.

| Needed by | Question | Stories |
|---|---|---|
| Sprint 01 (12 Oct 2026) | Is the repository public or private, and if private, is GitHub Pro taken (design.md §10.2)? On a private repository, GitHub Free cannot enforce "`main` accepts changes only through a pull request" | S00.01.02, S00.02.04 |
| Sprint 05 (7 Dec 2026) | sign-in.md Q7: the fixed passkey name, and the wrong setup code response | S00.04.02, S00.04.03 |
| Sprint 06 (21 Dec 2026) | sign-in.md Q6: a 5-minute two-factor cookie, or a distinct expired-code response; ux/README.md Q1: the `/manage` page, or a Manage disclosure in the nav | S00.04.05, S00.05.01 |
| Sprint 07 (4 Jan 2027) | ux/README.md Q10 and users.md Q6: `ConcurrencyStamp` in `GET /api/admin/users` (a design.md change); the setup-link email wording (P) | S00.06.01, S00.02.07 |
| Sprint 08 (18 Jan 2027) | ux/README.md Q8: Worker preselected on Invite; ux/README.md Q6 (the Reset sign-in dialog, and email changes), Q7 (status after the edge cases) | S00.06.02, S00.06.04, S00.06.05, S00.06.06 |
| Sprint 09 (1 Feb 2027) | The production domain (A38); users.md Q5 (case-only email changes) | S00.02.04, S00.06.06 |
| Sprint 09 review (14 Feb 2027) | coverage.md *Rows to watch*: close a feature with named bullets deferred, or keep it open | F00.04, F00.05 and later |
| Sprint 10 (15 Feb 2027) | participants.md Q2 and ux/README.md Q10 (`rowVersion` and sort order on participants; how `POST` returns the new ID) | S01.02.01, S01.02.02 |
| Sprint 10 (15 Feb 2027) | Should Release 1's server refuse detail and goal changes on an archived participant (its page is read-only on screen only), or does Release 2 add the refusal, as the backlog now assumes (S07.03.02; mcp-server.md §4.8, §5.4)? Either way the code goes into design.md §6.9 through decisions.md | S01.02.04, S07.03.02 |
| Sprint 11 (1 Mar 2027) | ux/README.md Q11: confirm A41–A43, A45 and the 200-character group name (A6), and the Common items choices (common-items.md Q2–Q4); confirm S01.03.01's data migration for the organisation already in test | F01.03 |
| Sprint 13 (29 Mar 2027) | guide-prompts.md open questions: the new ETag on the `PUT` response, and whether an unchanged save writes `guide_prompts.updated` | S01.04.01 |
| Sprint 14 (12 Apr 2027) | note-form.md Q1 (compact name in the sticky bar), Q2 ("Submit the note for"), Q10 (Submit after a submit in another tab) | S02.02.02, S02.03.01 |
| Sprint 15 (26 Apr 2027) | today.md Q2 and Q3 (pending edits on Today) | S02.01.03, S02.01.04 |
| Sprint 18 (7 Jun 2027) | ux/README.md Q11 and note-form.md Q13: keep or drop the A47 copied-picks line; participant-notes.md Q3 (no past-day tag on Past notes rows) | S02.05.03, S02.04.02 |
| Sprint 19 (21 Jun 2027) | note-form.md Q3 (the version number in the edit caption) | S02.06.01 |
| Sprint 20 (5 Jul 2027) | note-form.md Q4 (back-link labels for past-day notes) | S02.08.02 |
| Sprint 21 (19 Jul 2027) | The sort key for "oldest first"; ux/README.md Q3 and Q9 | S03.03.01, S03.03.03 |
| Sprint 23 (16 Aug 2027) | What the PDF shows for a character the embedded font lacks, such as an emoji | S04.01.02 |
| Sprint 25 (13 Sep 2027) | record-export.md Q1 (the earliest note date for the From default), Q2 (the no-notes response), Q3 (a To date after today) | S05.01.01, S05.01.02 |
| Sprint 27 (11 Oct 2027) | Where the HPP 4.3 destruction log and the yearly retention list are kept (not in the repository) | S05.04.01 |
| Sprint 27 (11 Oct 2027) | The switch-over date (A40), so staff onboarding and the gathering of Word notes can be planned (the likely forecast leaves the provider about three weeks of production before go-live; a later date gives more); who runs staff onboarding; whether to have a pilot | F06.06, S06.05.02, S06.06.03 |
| Sprint 27 (11 Oct 2027) | When the restore test runs, given production holds no note until the switch-over day: may production hold a known note before then? The answer shapes how S06.03.02's runbook is written and whether go-live line 8 can be complete before go-live | S06.03.02, S06.06.02 |
| Sprint 31 planning (6 Dec 2027), at Release 2 planning | mcp-server.md §12 Q4: is confirmation by the assistant enough for the four access-related tools? The R2 forecast assumes it is (MA6) | S07.04.01–S07.04.06 |
| Sprint 31 planning (6 Dec 2027), at Release 2 planning | The Support role and its sign-in, setup and session policies are not behind `Mcp:Enabled`, so the monthly production deploys would ship them before switch-on: accept that, or hide the Support option while `Mcp:Enabled` is false (a decisions.md entry, then a criterion in S07.01.01)? The R2 forecast assumes they ship | S07.01.01, S07.01.02 |

Copy marked (P) in the screen files ships with the proposed wording unless the owner approves other words
(README §8); it is not listed here.
