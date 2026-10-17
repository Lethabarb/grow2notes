# Grow2Notes tasks

The plan for the rest of the work. On 14 October 2026 the owner decided (D87) that the 145 stories not yet done are
delivered as the 39 tasks below: 28 for Release 1 (T01 to T28) and 11 for Release 2, the admin MCP server (T29 to
T39). Each task is one pull request that a person can see working end to end. The tasks absorb 138 stories, and 7 are
cut ([Cut stories](#cut-stories)).

- **The stories are the acceptance criteria.** An absorbed story keeps its section in its epic folder: its criteria
  and Notes are the task's acceptance criteria, and the task adds no new spec. Each task links its stories under
  *Absorbs*.
- **One branch and one pull request per task**, titled as a Conventional Commit, with the task's ID and its stories'
  IDs in the description. The Definition of Done ([README §9](README.md#9-definition-of-done)) applies to each task
  as it did to each story: one review, green CI, merge, the deploy to test and one after-deploy check.
- **A task is planned briefly when its lane starts it**: its *Steps* turn the absorbed criteria into a short
  checklist, shared parts first. There is no per-story refinement, no pull-in row in a sprint file and no per-story
  close-out. When the task is done, its `status` here and its stories' `status` fields become `done` together.
- **By-hand checks** (real devices, screen readers, a person signing in to test, the operator's Azure steps) go on
  [ops/first-use-in-test.md](../../ops/first-use-in-test.md) and never hold a task open (owner, 12 October 2026).
- **Go-live and switch-on gates stay mandatory**, on their own run sheets: `ops/go-live.md`, which T22 writes, and
  `ops/mcp-switch-on.md`, which T38 writes. Only T28 and T39 wait for them.
- **This file's order supersedes the numbered backlog order in [releases.md](releases.md)** from 14 October 2026.
  The numbers are a valid order for building one task at a time. A task starts once every task in its `depends_on`
  has merged and every owner question it names (Q2, Q5 and so on, [below](#open-owner-questions)) is answered;
  branch it from the newest `origin/main`.
- **Fields:** `status` is `new`, `doing` or `done`; `size` is S (up to 5 points), M (6 to 11) or L (12 to 16), from
  the absorbed stories' points; `lane` is the lane that builds it.

## Lanes and shared files

Up to four lanes run at once, each one task at a time:

- **A:** T01, T04, T11, T16, T19, T24 for Release 1, then T30 to T39, one after another, because each MCP tool edits
  the shared `tools/list` snapshot and tool matrix.
- **B:** T02, T05, T06, T07, T15, T17, T21, T25, then T29 beside T30.
- **C:** T03, T08, T09, T10, T12, T13, T14, T18, T20, T26. The note form is one hot spot, so its tasks run back to
  back.
- **D:** T22 (once the production domain is set) and T23, alongside the note and report work, then T27 and T28.

Some pairs run one after the other on purpose: T12, T13 and T14 all change the note form, the draft save and Today;
T16 and T18 both change the read view and the version-saving code; and T15 holds both download buttons, so T17 and
T20 come after it and do not touch the same files.

**Shared pieces are built once**, by the task whose Notes say so, and later tasks reuse them from `main`. At ship time
there is one correct version of any shared code.

**Files every lane edits:** the EF model snapshot (`Data/Migrations/Grow2NotesDbContextModelSnapshot.cs`),
`routes.tsx` and `src/copy`, `AuditEventTypes.cs`, `EndpointMatrix.cs`, `Limits.cs` and `limits.ts`, and the
audit-events test. The lane that merges second rebases on `main` and re-creates its migration and the snapshot on
top; it never merges the snapshot by hand. It also adds its own audit-test and endpoint-matrix rows.

---

## T01 Invite people, resend their link, and sign in again in place

```yaml
id: T01
status: new
release: R1
lane: A
size: M
depends_on: []
stories: [S00.02.07, S00.06.02, S00.06.03, S00.04.08]
```

### Outcome

A manager invites a worker or manager from Users, and the person gets the Grow2Notes setup-link email. An expired invite
can be resent, and the old link stops working. If a session ends mid-task, the person signs in again on the same screen
with their typing kept (checked with a half-filled Invite form), and a different person gets a fresh Today.

### Absorbs

- [S00.02.07][] Setup-link email
- [S00.06.02][] Invite a user
- [S00.06.03][] Resend an invite
- [S00.04.08][] Sign in again where I was

### Cuts

- Success banners after Invite and Resend invite (ux/README.md open question 2): not built unless the owner approves
  them.

### Notes

- Builds once: the setup-link step (key, token and link), the email sender, and If-Match for user changes. T04 reuses
  all three; nothing else sends email (A23).
- In test the email goes from Azure Communication Services' free Azure-managed domain (azurecomm.net), with SPF, DKIM
  and DMARC set by Azure (design.md A58, answering the plan's question 1); the product-domain sender comes with
  production in T22. First confirm that the Email service offers Australia as its data location (design.md §10). Azure
  resources come only through `main.bicep` in the normal deploy, at resource-group scope.
- Tests use a fake sender; CI never sends real email. A real email arriving in test, with its SPF, DKIM and DMARC
  headers, is a by-hand check on ops/first-use-in-test.md.
- Whichever of T01 and T03 merges second adds `disarmUnloadGuard()` before the different-person reload.
- From T03 (15 October 2026, its review): if T03 merges first, T01 also proves on Manage > Guide prompts, in
  Playwright, guide-prompts.md › Leaving's last two bullets, which need its sign-in in place: after a `401` mid-edit
  the same person signs in, and the text is still there and still not saved, with no `PUT` until Save is pressed
  again; and a different person signs in, and the page reloads in full to Today, with no browser dialog and the text
  gone (S00.04.08's Note from T03). S01.04.02 keeps them open until then.

### Steps

Planned when the lane starts it.

---

## T02 Keep my place across screens, and participants

```yaml
id: T02
status: new
release: R1
lane: B
size: L
depends_on: []
stories: [S00.05.02, S01.02.01, S01.02.02, S01.02.03, S01.02.04]
```

### Outcome

Every screen change puts focus on the new heading, and Back returns to the row you opened, with scroll memory kept in
memory only: checked first on the Users list, a user's page and back, then on Participants. A manager can then search
Manage > Participants, add a participant, correct their details, and archive or restore them; a worker gets Page not
found and `403`.

### Absorbs

- [S00.05.02][] Keep my place when I move between screens
- [S01.02.01][] See and find participants
- [S01.02.02][] Add a participant
- [S01.02.03][] Correct a participant's details
- [S01.02.04][] Archive and restore a participant

### Cuts

- A duplicate-participant warning, an earliest date of birth, and the "restored" success banner (ux/README.md open
  question 2): not built unless the owner approves them.
- The server refusing changes to an archived participant: Release 2 adds it, in T34 (D86).

### Notes

- Builds once: the focus handling and the in-memory scroll map that later lists reuse (T19), the Find-a-participant box
  and the date-of-birth fields that Today reuses (T08), and the shared audit-events test that later tasks add rows to
  (T24 completes it).
- A worker's `403` on each new endpoint is an endpoint-matrix row, not a test of its own.
- From T03 (15 October 2026, its review): if T03 merges first, T02 also checks its route focus on Manage > Guide
  prompts, in Vitest and in `guide-prompts.spec.ts`: focus on the `<h1>` after a client-side arrival there
  (guide-prompts.md › Access and structure, the third bullet), and on Today's `<h1>` after Leave without saving
  (› Leaving, the first bullet), which S01.04.01 and S01.04.02 keep open until then (S00.05.02's Note from T03). And
  the one `SaveStatus` that ships, which Details' Save uses too, is at `--font-size-small` with `tabular-nums`, as
  foundations.md, which owns sizes, has status lines and the save indicator, where status-messages.md's *States*
  table says body size (guide-prompts.md *Conflicts resolved* 17; S01.02.03's Note from T03).

### Steps

Planned when the lane starts it.

---

## T03 Guide prompts, with a warning before leaving unsaved

```yaml
id: T03
status: doing
release: R1
lane: C
size: M
depends_on: []
stories: [S01.04.01, S01.04.02]
```

### Outcome

A manager sets the guide prompts shown in every empty Guided notes box, and is warned before leaving with unsaved
changes by a link, Back, reload or Sign out.

### Absorbs

- [S01.04.01][] Set the guide prompts
- [S01.04.02][] Warning before leaving unsaved prompts

### Notes

- Builds once, for later tasks to reuse: the shared confirm-dialog shell, the text-box styles, the `count()` helper in
  `src/copy/format.ts` and the leave guard. T04 adds the red warning style to the dialog shell; T05 uses `count()`.
- Also builds once the `SaveStatus` line and `apiWithHeaders()`, which gives an answer's `ETag` and `Date`, as T02's
  participant Details Save needs both too (status-messages.md, D79): whichever of T02 and T03 merges second reuses the
  first one's, so that one copy ships (S01.02.03's Notes), at the small size (the review Note below).
- Whichever of T01 and T03 merges second adds `disarmUnloadGuard()` before the different-person reload.
- guide-prompts.md's other open questions (the (M) string, the conflict message) are built as the spec words them unless
  the owner says otherwise.
- Lane C is then free until T05 merges, as T08 waits for it.
- Planned on 14 October 2026 from `origin/main` at `b618f57`. No owner question holds it. Where the design is silent,
  the simplest option is taken:
  - The `PUT` stores the text as sent and checks only its length: the screen trims it (guide-prompts.md *Save*), and
    design.md §6.6 gives the server no trimming of its own.
  - The request's `text` is `[Required(AllowEmptyStrings = true)]` and `[MaxLength(Limits.GuidePrompts)]`: `""` is
    valid (design.md §4.10 *States*, guide-prompts.md *Save*), where the app's other requests' plain `[Required]`
    refuses it, and a body with no `text` or a `null` one gets `422 validation.failed` on `text` from the validation
    filter rather than reaching the non-null column.
  - `apiWithHeaders()` beside `api()`, sharing its request, gives the answer's body with its `ETag`'s token, without
    the quotes, and its `Date` as a UTC time, each `undefined` when the answer has none, as Guide prompts is the first
    screen that reads them; the token goes back through `api()`'s existing `ifMatch`.
  - The `ConfirmDialog` shell holds what the leave dialog uses. The first dialog that sends a request, T04's, adds
    confirm-dialog.md's focus on the title, its alert line and its busy line along with the red style, so that no part
    ships before a dialog uses it (T04's Notes).
  - `ConfigurationChangesOnSqlServerTests` keeps its name when its tests move onto `/api/admin/guide-prompts`, as
    F01.02's and F01.03's Notes and tests/README.md cite its race test.
  - No migration: `Organisation.GuidePrompts` and the row's `RowVersion` are already in the schema.
  - Save's `PUT` is a direct `apiWithHeaders()` call with the form's own busy state, as Sign out's and Finish setup's
    are, not the TanStack mutation that primary-actions.md §8 suggests, whose `isPending` reaches the page a task
    after the press, so that a second quick press could send a second `PUT`. Nothing sends it again by itself, which
    is step 5's `retry: 0` (settings-textarea.md *Behaviour* 5). (Step 5, 14 October 2026.)
  - After a `412`, Save stays busy until the refetch of the base text and `ETag` has answered, so that the next press
    sends the new `ETag`; a refetch that fails leaves the base as it was, and that press meets the `412` again.
    (Step 5, 14 October 2026.)
  - The leave dialog answers the navigation that it holds, or Sign out, in its `close` event, which the browser fires a
    task after Escape or Stay on this page has hidden it. A Back pressed within that one task is held, its address
    put back and nothing lost, with no dialog; that is accepted rather than tracked apart from the event, and the
    tests wait for the dialog to leave the page before they go on. (Step 6, 15 October 2026.)
  - The storage spies run in every test of `guide-prompts.spec.ts`, as a fixture of that file, so that every path
    the tests take is covered. The checklist items that axe does not check are shown beside its runs: its AAA
    contrast rule for the 7:1 text, WCAG's text spacing on top of the 200% text at 320 px for SC 1.4.12, and no
    animation or transition for no motion. The by-hand checks are steps 24 and 25 of ops/first-use-in-test.md, in a
    section of their own after step 23, so that no step that a Note cites is renumbered; T26 orders them with the
    rest. The checklist's five screen-reader items stay unticked until step 24 has been done. (Step 7, 15 October
    2026.)
- Hand-offs, so that every criterion has one owner. Each is also written, dated 14 October 2026, where the receiving
  task reads it. Until that task proves it, the matching S01.04.01 or S01.04.02 criterion stays unticked with a dated
  Note naming the task:
  - design.md §4.10's lines about the Guided notes box (the prompts reaching open drafts straight away, the 4.5:1
    placeholder, the kept label, no placeholder when empty) and *Field*'s second bullet, comparing the two boxes'
    computed metrics, need the note form's Guided notes box, so T08 proves them (S02.02.02's Notes, T08's Notes).
    coverage.md gives *Field* to F01.04, unlike *Effect on the note form*, which is F02.02's; T03 proves the editor's
    own metrics.
  - Focus on the `<h1>` after a client-side navigation (arriving here, and on Today after Leave without saving) is the
    shell's route focus, which T02 builds for every screen (S00.05.02's Notes). If T02 is on `main` when this branch
    rebases, T03's tests assert it here; otherwise T02 asserts it on Guide prompts.
  - *Leaving*'s last two bullets (a `401` mid-edit, then the same or a different person signing in) need T01's sign-in
    in place (S00.04.08's Notes). Whichever of T01 and T03 merges second proves both on Guide prompts in Playwright,
    beside the `disarmUnloadGuard()` above.
  - confirm-dialog.md's focus on the title, alert line, busy line and red style, and the 28-minute warning closing an
    open dialog with a plain `close()`, are T04's (T04's Notes). No S01.04.01 or S01.04.02 criterion waits on them,
    and the leave dialog already treats that `close()` as Stay.
  - By-hand checks (screen readers, real phones) go on ops/first-use-in-test.md in step 7.
- 15 October 2026, review: 7 findings were confirmed and are fixed here:
  - `guide-prompts.spec.ts`'s test of two saves in a row checked the save status empty with the page's clock running,
    so a runner that stalled for 400 ms would meet its hidden "Saving…" and fail; it now pauses the clock for that
    check, as the test of the held save does.
  - No test showed that a refetch that fails after a `412` still shows the conflict and leaves the base as it was
    (step 5's Note above). A Vitest case now does, and fails, with an unhandled rejection, once that refetch's
    `.catch` is removed.
  - No test showed that the storage spies run in, and fail, a test that does not name them. A test marked
    `test.fail()` now writes to `localStorage` without naming them, as `fixtures.spec.ts` does for the Content
    Security Policy, so that a fixture no longer automatic, or no longer failing on a write, fails the run.
  - `SaveStatus` is at `--font-size-small` with `tabular-nums`, as guide-prompts.md 6f has it and foundations.md,
    which owns sizes, gives status lines and the save indicator, where status-messages.md's *States* table has "Saved
    4:12 pm" at body size. The choice is now recorded, as guide-prompts.md *Conflicts resolved* 17, in S01.02.03's
    Note from T03 and in T02's Notes, so that the one copy that ships keeps that size, whichever of T02 and T03 merges
    second.
  - S01.04.01 and S01.04.02 had no dated Notes for the criteria handed to T01, T02 and T08 and to the by-hand steps,
    though F00.04, F00.05 and F02.02 said they had; each now has one.
  - The hand-offs to T01 and T02 were only in story Notes, which a rebase does not bring to those lanes; T01's and
    T02's Notes here now hold them, as T04's and T08's do.
  - ops/first-use-in-test.md step 24 listened for the heading as the page arrives, which needs T02's route focus, with
    no gate; that check now waits until T02 is in test.
- 17 October 2026, review: 2 findings were confirmed and are fixed here:
  - No test showed that Save empties the save status at the press (guide-prompts.md *Interactions and focus*, "Status
    and alert cleared"), so a save that failed after one that worked could show "Saved" and its time beside the alert;
    the tests of two saves in a row pass without it, as the status is empty while any save is out. A Vitest case now
    saves, then meets no connection, and a `412`, and fails once that reset is removed; `guide-prompts.spec.ts`'s axe
    run checks the status empty at the conflict that follows a save; and the two-saves tests' titles no longer claim
    the reset.
  - ops/first-use-in-test.md step 24 left out the load status, Try again's "Loading…" and failure, the hidden
    "Saving…", the alerts for no connection and the conflict, Sign out's dialog and the heading after Leave without
    saving, so it could not close the checklist's item on waiting and errors or *Accessibility runs*' screen-reader
    bullet. It now causes each row of *Interactions and focus* that a person can: the conflict from a second tab, as
    accounts A and B are in different organisations; no connection with the device offline; and, on the laptop alone,
    whose developer tools can slow the connection, "Loading guide prompts…" and "Saving…", for which VoiceOver and
    TalkBack hear the load-failed sentence and "Saved" in the same two regions. A server error shows in the same alert
    as no connection, a `422` as the too-long error and a `401` is the shell's, so none needs a person. Step 24 is what
    closes the checklist's five screen-reader items and that bullet.

### Steps

- [x] Serve the guide prompts (S01.04.01's first criterion; guide-prompts.md *Access and structure*'s `403`, *Save*'s
      two-managers and audit bullets and the server half of *Limit and count*; design.md §5.4, §6.6, D84): a handler
      class in `Features/GuidePrompts` that takes the `Actor` (mcp-server.md §3.5), with `GET` and
      `PUT /api/admin/guide-prompts` mapped in the `/api/admin` group from `Program.cs`. The `GET` gives `{text}` with
      `RowVersionToken.ETag(organisation)`. The `PUT`'s request holds `text` as required but possibly empty, and at
      most `Limits.GuidePrompts` long (Notes above), so that the validation filter answers `422 validation.failed` on
      `text` before the handler runs; the handler checks `IfMatch.Check` and answers its code's `Problem()`, saves
      through `ConfigurationChanges.SaveAsync` as `guide_prompts.updated` on `Organisation` with `text` old and new,
      and answers `200` with the new `ETag`, the same one when nothing changed, or `412` when the save races. Delete
      `ConfigurationChangesOnSqlServerTests`' test-only endpoints and move its tests onto the real path, the race test
      through the handler. SQL Server tests add: 1,000 characters holding 10 line breaks save; 1,001 answer `422` with
      `errors.text`, changing nothing and writing no event; a `PUT` of `""` answers `200`, stores `""` and writes one
      event with the old and new text; a body with no `text`, and one with a `null` `text`, each answer `422` with
      `errors.text`, changing nothing and writing no event; A loads, B saves, A's `PUT` gets `412`, A's next `PUT`
      with the fresh `ETag` saves, and both events hold the old and new text; and, with a second organisation added
      that holds other prompts, each organisation's manager's `GET` gives their own organisation's text and `ETag`,
      and a `PUT` by one organisation's manager leaves the other organisation's row, `RowVersion` and audit events
      unchanged (tests/README.md *Adding an admin endpoint*; design.md §5.9 item 7). Matrix rows for both: `401`
      signed out, `403` for A's worker, and for both managers `200` from the `GET` and `428` from a `PUT` with a body
      and no `If-Match`, which changes nothing; `AdminGuardTests` pass; and, if T02's audit-events test is on `main`,
      its `guide_prompts.updated` row
- [x] Build the shared pieces that later tasks reuse (Notes above): `count()` in `src/copy/format.ts` (microcopy.md
      §3: `Intl.NumberFormat` and `Intl.PluralRules` in `en-AU`), with the character-count words in `messages.ts`
      (microcopy.md §4: "You can enter up to 1,000 characters", "You have 100 characters remaining", "You have 1
      character too many"); `TextBox.module.css` in `src/components` (guide-prompts.md 6b: 18 px text, line height
      1.5, 0.75rem padding, 2 px border, `min-block-size: 11em`, the 40rem column, `field-sizing: content` with the
      grid mirror as fallback, the error border and the forced-colours focus); `leaveGuard.ts` beside
      `fullPageLoad.ts` (settings-textarea.md's contract: `setLeaveGuard`, `confirmLeave`, `disarmUnloadGuard`,
      `rearmUnloadGuard`, `unloadGuardArmed`, in memory only); the `ConfirmDialog` shell (confirm-dialog.md):
      a native `<dialog>` opened with `showModal()` on mount, `role="alertdialog"` and `aria-modal`, named by its
      `<h2>` and described by its body, two stacked full-width buttons, primary and secondary in the order the dialog
      gives, focus on the button the dialog names, Escape, Android back and any close but its confirming button's
      meaning the safe choice, no light dismiss, and focus back on the trigger; `SaveStatus.tsx` in `src/components`
      (status-messages.md `SaveStatus`, guide-prompts.md 6f, participants.md's Details Save): an always-rendered
      `<p role="status">`, never `display: none`, in `--font-size-small`, `--colour-text-secondary` and
      `tabular-nums`, that is empty, holds a visually hidden "Saving…" when its page asks for it, or reads "Saved"
      followed by the time from `time()` in a `<time dateTime>`, or "Saved" alone when the answer had no `Date`; and
      `apiWithHeaders()` in `src/api/api.ts` (Notes above). Vitest shows each: `count()`'s singular and grouping, and
      the words at 900, 999, 1,000 and 1,001; `confirmLeave()` resolving `true` at once with no guard registered
      (S01.04.02's own criterion: no other screen warns) and asking the one registered; the dialog's role, name,
      description and initial focus, and its `cancel` event (Escape) and a plain `close()` as the safe choice (jsdom
      has no backdrop or real `showModal()`, so step 6 checks those in Chromium); `SaveStatus`'s four states, the region
      in the DOM while empty, and a time of 06:12 UTC on 1 October 2026 shown as "Saved 4:12 pm"; and
      `apiWithHeaders()` giving the body, the `ETag`'s token without its quotes and the `Date` as UTC, `undefined` for
      either header when it is missing, and failing as `api()` does
- [x] Make Sign out ask the leave guard (app-shell.md *Account and Sign out*'s Guide prompts bullet, in the order of
      *Press Sign out* and *Sign out fails*): in `AccountPanel`'s `signOut()`, `await confirmLeave()` before any
      request and before the button goes busy, so that Stay sends nothing; `disarmUnloadGuard()` just before the logout
      request; `rearmUnloadGuard()` after a failed logout; the comment over `signOut()` drops this part; and, if T01's
      different-person reload is on `main` by then, `disarmUnloadGuard()` before its `fullPageLoad()`.
      `Header.test.tsx` shows, with a stand-in guard: none registered, one logout as before; Stay, no request, Sign out
      not busy and focused; Leave, the guard disarmed before the `POST`; a failed logout, the guard armed again and the
      failure line shown; and the different-person reload disarming first, if it is built
- [x] Show Manage > Guide prompts (guide-prompts.md *Access and structure*, *Loading* and *Field*; design.md §4.10's
      *On screen*, *Help text* and no-prompt-text lines): `/manage/guide-prompts` under `ManagerOnly`, named
      `pageName.guidePrompts`, in `src/features/guidePrompts`; the `<h1>` holding the field's `<label>`, the help text
      and the limit hint, rendered at once; `LoadRegion` with "Loading guide prompts…" and "The guide prompts"; the
      `['admin', 'guidePrompts']` query through `apiWithHeaders()`, giving the text and the `ETag`'s token, with
      `gcTime: 0` and no refetch on focus; the form mounted only with the data, its uncontrolled textarea on
      `TextBox.module.css`, the loaded text read once, described by the help and limit, with `spellCheck`,
      `autoCapitalize="sentences"`, `autoComplete="off"` and `writingsuggestions="false"`, and no `placeholder`,
      `maxlength`, `disabled`, `readonly` or autofocus; and `guidePrompts: 1_000` in `limits.ts`
      (`SpaLimitsTests`). Vitest and Playwright, answering the API in the browser, show every bullet of the three
      groups: the heading, help, hint, title and Manage's `aria-current="true"`; a worker's Page not found; the field
      never focused by itself, and focus on the `<h1>` after a client-side arrival if T02 is on `main` (Notes above);
      with the `GET` held 3 s, no field or Save before its answer, the status empty at 999 ms and "Loading guide
      prompts…" at 1,000 ms; offline, the load-failed words, then "…still did not load…", then Try again's success
      mounting the field with focus on the `<h1>`; a second visit sending a fresh `GET`; the saved text exact, line
      breaks kept, in `#0b0c0c`; an empty value as an empty box; no request while typing, pasting and deleting; and, at
      375 and 1280 px, the box's computed 18 px font, 27 px line height, 12 px padding and 2 px border, 40rem wide on
      the laptop
- [x] Count and save (guide-prompts.md *Limit and count* and *Save*; design.md §4.10's Save): the count of the trimmed
      text, hidden below 900, `aria-hidden` beside a polite twin that speaks 1 s after typing stops, and red over 1,000
      with the box's border but no `aria-invalid`; Save through `onSubmit` with no form action, sending the trimmed
      text, or, over 1,000, showing "Guide prompts must be 1,000 characters or less" with the error bar,
      `aria-invalid` and focus on the field, cleared once fixed; the `PUT` through `apiWithHeaders()` with `If-Match`
      from the last `ETag`; the shared `Button` busy, "Saving…" after 400 ms, with `announceBusy={false}`; step 2's
      `SaveStatus`, cleared on the press, holding its hidden "Saving…" from 400 ms, then "Saved 4:12 pm" from the
      answer's `Date` ("Saved" without one), cleared on the next keystroke; `RequestAlert` with "Not saved: no
      connection. Try again.", "Not saved: something went wrong. Try again." or the conflict message; `retry: 0`; a
      `412` refetching the base text and `ETag` with the field untouched; a `422` shown as too long; a `401` left to the
      session flow; and a `403` refreshing `/me`, so that the role guard shows Page not found. Vitest and Playwright
      show every bullet of the two groups: the counts at 900, 999, 1,000 and 1,001; a 1,200-character paste kept
      whole, the error, focus and nothing sent; 1,000 characters with 10 line breaks saved; the body trimmed at its
      ends only, and `""` for whitespace; Save never `disabled`, and two quick presses sending one `PUT`; with the `PUT`
      held 2 s, "Saving…" at 400 ms, the button's size unchanged and the field editable; a `Date` of 06:12 UTC on
      1 October 2026 shown as "Saved 4:12 pm", focus on Save, cleared by a keystroke; two saves both announced; typing
      during a slow save leaving the page dirty; one `PUT` per press on failure, the text unchanged; A loads, B saves,
      A sees the conflict with the text unchanged and A's next Save succeeds; and one test signed in for real (a
      bootstrapped manager with a passkey on the virtual authenticator, as `users.spec.ts` does) saving prompts with
      line breaks and reading them back after a reload
- [x] Warn before leaving unsaved (S01.04.02: design.md §4.10's warning, guide-prompts.md *Leaving*, app-shell.md's
      Guide prompts bullet and its own criterion): dirty is the field's trimmed value, read from the DOM, against the
      base text; `useBlocker` blocks only when the pathname changes while dirty; the leave dialog on the `ConfirmDialog`
      shell says "Leave without saving?" and "Your changes to the guide prompts are not saved.", with "Stay on this
      page" (primary, focused) and "Leave without saving" (secondary, not red), any close but Leave resetting the
      blocker and Leave proceeding; a `beforeunload` listener only while dirty, which prevents the unload while
      `unloadGuardArmed()`; and the page registers the same dialog with `setLeaveGuard` for Sign out, removing it on
      unmount. Playwright shows every *Leaving* bullet: dirty and Today, the dialog with focus on Stay; Escape, the URL,
      the text and focus on the Today link unchanged; Leave, on Today, with focus on its `<h1>` if T02 is on `main`
      (Notes above); clean (typed, then deleted back) no dialog for a link, Back, reload or Sign out; arrived from
      Manage, dirty, and the browser's Back, the dialog with focus on Stay; Escape, and Stay after a second Back, each
      leaving the URL at `/manage/guide-prompts` and the text unchanged; and Leave after a third going to `/manage`
      (blocking a POP is the risky path, react-router issue #11589, and Chromium can prove it; only the phone gestures
      need a person); a click on the dialog's backdrop leaving it open and the URL unchanged; dirty and reload,
      Chromium's dialog, and none after a Save; no `beforeunload` listener while clean (a spy on `addEventListener`);
      dirty and Sign out, the dialog and no logout request; Stay, still none and focus on Sign out; Leave, the logout
      and no browser dialog; a failed logout leaving the page guarded; a plain `close()` of the dialog as Stay; no
      warning on Today, Manage or Users for a link, reload or Sign out; and, if T01 is on `main`, the two
      sign-in-again bullets (Notes above)
- [x] Meet the Definition of Done's rows for this screen (guide-prompts.md *Storage and naming* and *Accessibility
      runs*; README §9): Playwright shows nothing written to `localStorage`, `sessionStorage` or IndexedDB on any of
      the paths above (spies on `Storage.prototype.setItem` and `indexedDB.open`); axe passing in Default, Loading,
      Load failed, Too long, Saved, Conflict and with the dialog open; at 320 px with text at 200%, which also stands
      for 400% zoom at 1280 px, no sideways scroll and nothing clipped; with forced colours emulated, real borders on
      the box, the error bar, the focus ring and both buttons; and a keyboard-only pass (README §9's accessibility row;
      guide-prompts.md *Focus order* and *Accessibility checklist* › Keyboard), with Tab as `users.spec.ts` presses it:
      from Manage, Tab reaches the field and then Save, with Try again after Manage only while the load has failed;
      Enter in the field adds a line break and sends no `PUT`; in the leave dialog Tab moves from Stay on this page to
      Leave without saving; and the 3 px focus ring with its 2 px offset shows on each stop. Then tick the items of
      guide-prompts.md's *Accessibility checklist* that these runs prove; the screen-reader ones wait for the by-hand
      step below. Every new string is in `src/copy` and the copy test passes; the diff holds no trace of the parent
      company's name; and ops/first-use-in-test.md gains a Guide prompts step for the by-hand checks: NVDA with Chrome,
      VoiceOver with iOS Safari and TalkBack with Chrome hearing *Interactions and focus* once each, and, on real
      phones, the Android back gesture and iOS swipe-back while dirty, and whether `beforeunload` shows anything there
      (S01.04.02's Note; guide-prompts.md *Open questions*)
- [ ] After the deploy to test, check with requests and a headless browser that change nothing and sign no one in:
      the CI and Deploy runs passed every step, the deploy applied no migration and its smoke test passed; `/healthz`
      and `/healthz/ready` answer `200`; a signed-out `GET /api/admin/guide-prompts` answers `401` problem+json with
      `Cache-Control: no-store` and a `Date` header through App Service's front end, the header that "Saved 4:12 pm"
      reads (guide-prompts.md *Open questions*); and headless Chromium at `/manage/guide-prompts`, signed out, shows
      sign-in with no Content Security Policy error in its console. Record the run IDs in Notes, and that the criteria
      are proven in CI. Then set T03's `status` here and S01.04.01's and S01.04.02's to `done` together, ticking their
      criteria and leaving unticked, each with its dated Note, any criterion handed off to a task not yet merged (Notes
      above)

---

## T04 Reset, deactivate, reactivate and edit users; break-glass

```yaml
id: T04
status: new
release: R1
lane: A
size: M
depends_on: [T01, T03]
stories: [S00.06.04, S00.06.05, S00.06.06, S00.03.06]
```

### Outcome

From a user's page a manager resets a sign-in after a confirm step, deactivates or reactivates someone, or changes a
name, email or role. Each change ends that person's sessions on their next request, and the last active manager is
protected. From the console the operator can reset the last manager's sign-in or sign everyone out.

### Absorbs

- [S00.06.04][] Reset someone's sign-in
- [S00.06.05][] Deactivate and reactivate a user
- [S00.06.06][] Change a user's name, email or role
- [S00.03.06][] Break-glass reset and sign-out of everyone

### Cuts

- Success banners after edit, reset, deactivate and reactivate (ux/README.md open question 2): not built unless the
  owner approves them.

### Notes

- Reuses T01's setup-link step, email sender and If-Match for users, and T03's dialog shell, to which it adds the red
  warning style and the rule that the 28-minute warning closes an open dialog as Go back.
- From T03 (14 October 2026, its plan): T03's `ConfirmDialog` shell holds only what its leave dialog uses, so T04, the
  first dialog that sends a request, also adds to the shell, for every dialog after it, confirm-dialog.md's focus on
  the title (the `<h2 tabindex="-1">` with no visible ring, for a dialog that names it as its first focus), the
  always-rendered `<p role="alert">` error line, and the busy state with its always-rendered `<p role="status">` busy
  line after 1 s, beside the red style. The leave dialog keeps its focus on "Stay on this page" (guide-prompts.md
  *Conflicts resolved* 9). No S01.04.01 or S01.04.02 criterion waits on these.
- Builds the session-revocation test helper that T24 and T32 reuse. D74 to D77 settle the dialogs and the edge cases.
- The operator's break-glass run and the two-device sign-out are by-hand checks on ops/first-use-in-test.md.

### Steps

Planned when the lane starts it.

---

## T05 Goals for a participant

```yaml
id: T05
status: new
release: R1
lane: B
size: M
depends_on: [T02, T03]
stories: [S01.02.05, S01.02.06, S01.02.07, S01.02.08]
```

### Outcome

On a participant's page a manager adds goals one after another (five on a phone without leaving the field), and rewords,
moves, archives and restores them. Changes apply to notes started from then on.

### Absorbs

- [S01.02.05][] Add goals for a participant
- [S01.02.06][] Reword a goal
- [S01.02.07][] Reorder goals
- [S01.02.08][] Archive and restore goals

### Notes

- Builds the shared buttons-only list editor that T06 and T07 reuse for common items, and uses T03's `count()` helper.

### Steps

Planned when the lane starts it.

---

## T06 Common items in the built-in Every note group

```yaml
id: T06
status: new
release: R1
lane: B
size: L
depends_on: [T05]
stories: [S01.03.01, S01.03.02, S01.03.03, S01.03.04]
```

### Outcome

Every organisation has one fixed Every note group: bootstrap creates it, and a migration adds it to organisations that
already exist (D82). A manager adds, rewords, reorders, archives and restores its items on the Common items screen.

### Absorbs

- [S01.03.01][] Every note group, built in
- [S01.03.02][] Add and reword common items
- [S01.03.03][] Reorder common items in a group
- [S01.03.04][] Archive and restore common items

### Notes

- Creates the `CommonItemGroup` and `CommonItem` tables that note versions point at, which is why T09 waits for it.
  Reuses T05's list editor.

### Steps

Planned when the lane starts it.

---

## T07 Common item groups, and moving items between groups

```yaml
id: T07
status: new
release: R1
lane: B
size: M
depends_on: [T06]
stories: [S01.03.05, S01.03.06, S01.03.07, S01.03.08]
```

### Outcome

A manager adds, renames, reorders, archives and restores groups such as Community outing, and moves an item to another
group; Every note stays first and fixed.

### Absorbs

- [S01.03.05][] Add and rename groups
- [S01.03.06][] Reorder groups
- [S01.03.07][] Archive and restore groups
- [S01.03.08][] Move a common item to another group

### Notes

- Reuses T05's list editor. The shared audit-events test then covers all 20 configuration events with old and new
  values. A41 to A46 are confirmed as written (D82).

### Steps

Planned when the lane starts it.

---

## T08 Today list and starting today's note

```yaml
id: T08
status: new
release: R1
lane: C
size: L
depends_on: [T02, T03, T05]
stories: [S02.01.01, S02.01.02, S02.02.02, S02.02.03, S02.02.07]
```

### Outcome

Everyone lands on Today, sees every active participant and can filter by name. They open today's note, tick goals and
write Guided notes that save to the server as they type. The server allows one live note per participant per day, and
lets workers write only today's note, using the Melbourne date.

### Absorbs

- [S02.01.01][] See every active participant on Today
- [S02.01.02][] Find a participant
- [S02.02.02][] Start a note and autosave the Guided notes
- [S02.02.03][] Tick the participant's goals
- [S02.02.07][] One note per participant per day

### Keeps

- The server-side test that a worker's note for any other date is refused. The daylight-saving clock tests are already
  in CI (S02.02.01, cut).

### Cuts

- S02.01.01's interim step of rows with no status: T09 adds the status.
- The compact name in the note's sticky top bar (note-form.md open question 1): not built unless the owner approves it.

### Notes

- Builds once: the whole `Note` and `NoteDraft` schema (with no `PicksCopiedFrom`, D83) and the lists-changed reload.
  Reuses T02's Find-a-participant box and T03's leave guard and `count()`.
- Starts lane C's note-form run: T08, T09, T10, T12, T13 and T14 run back to back, because the note form is one hot
  spot.
- From T03 (14 October 2026, its plan): also proves the parts of S01.04.01 that need the note form's Guided notes box,
  which T03 left unticked naming T08: guide-prompts.md › Field's comparison of the two boxes' computed metrics at
  375 and 1280 px, and design.md §4.10's "straight away … including open drafts" and empty prompts showing no
  placeholder (S02.02.02's Notes).

### Steps

Planned when the lane starts it.

---

## T09 Submit, flag and read notes back

```yaml
id: T09
status: new
release: R1
lane: C
size: L
depends_on: [T04, T06, T08]
stories: [S02.03.01, S02.03.02, S02.01.03, S02.04.01, S02.04.02]
```

### Outcome

A worker submits a note after a confirm that shows the participant's name and date, optionally flagged for a manager
with a reason. Today shows each row's status, and anyone can open a submitted note and page back through a participant's
past notes.

### Absorbs

- [S02.03.01][] Submit a note after a name check
- [S02.03.02][] Flag a note for a manager
- [S02.01.03][] See today's note status on each row
- [S02.04.01][] Read a submitted note
- [S02.04.02][] Browse a participant's past notes

### Notes

- The version tables are append-only, with their denies in `grow2notes_runtime`, and a Playwright run at 375 px fills in
  and submits a note. The Submit confirm uses T03's dialog shell.
- Waits for T06 because note versions point at common items.

### Steps

Planned when the lane starts it.

---

## T10 Autosave that never loses words

```yaml
id: T10
status: new
release: R1
lane: C
size: M
depends_on: [T01, T09]
stories: [S02.02.04, S02.02.05, S02.02.06]
```

### Outcome

A note keeps its typing through a dropped signal, an idle sign-out, Sign out, and a move to another tab or device. An
older page is told the note has moved on, and nothing is overwritten without asking.

### Absorbs

- [S02.02.04][] Keep autosaving when the connection drops
- [S02.02.05][] Keep the draft through sign-out and leaving the form
- [S02.02.06][] Carry on in another tab or on another device

### Notes

- Reuses T01's sign-in again in place. The storage and phone-to-laptop checks are Playwright tests; the real-device runs
  go on ops/first-use-in-test.md.

### Steps

Planned when the lane starts it.

---

## T11 Flags reach every manager's To review list

```yaml
id: T11
status: new
release: R1
lane: A
size: M
depends_on: [T09]
stories: [S03.01.01, S03.03.01]
```

### Outcome

When a worker submits a flagged note, every manager sees it in the Flagged screen's To review list, oldest first, with
the reason and when it was flagged. Only submitted flags count.

### Absorbs

- [S03.01.01][] Send a submitted flag to To review
- [S03.03.01][] See the flagged notes to review

### Notes

- Starts the one flag-rules table test that T16 adds to. "Oldest first" means the flag time. The menu badge comes with
  T19.

### Steps

Planned when the lane starts it.

---

## T12 Unfinished drafts: continue, discard, other people's drafts

```yaml
id: T12
status: new
release: R1
lane: C
size: M
depends_on: [T10]
stories: [S02.01.04, S02.03.03, S02.02.08, S02.04.03]
```

### Outcome

Today lists your own older drafts so you can finish and submit them on their original date. The writer or a manager can
discard a draft to free the day. A worker opening someone else's draft sees only who started it and when, while a
manager can read it.

### Absorbs

- [S02.01.04][] Continue an unfinished draft from Today
- [S02.03.03][] Submit a draft from an earlier day
- [S02.02.08][] Discard a draft
- [S02.04.03][] See someone else's draft, and discard it as a manager

### Notes

- One discard endpoint and one dialog, on T03's dialog shell, serve both the writer and the manager.
- T12, T13 and T14 run one after another: all three change the note form, the draft save and Today.

### Steps

Planned when the lane starts it.

---

## T13 Common items and group picks on the note

```yaml
id: T13
status: new
release: R1
lane: C
size: L
depends_on: [T07, T12]
stories: [S02.05.01, S02.05.02, S02.05.03, S02.05.04]
```

### Outcome

The note shows the Every note items and asks which other groups happened, starting from the last submitted note's picks.
Only picked groups' items can be ticked, and picks are saved and versioned with the ticks.

### Absorbs

- [S02.05.01][] Tick the Every note items
- [S02.05.02][] Tick which groups happened and their items
- [S02.05.03][] Start with the last note's group picks
- [S02.05.04][] Re-check ticks when groups or items change before the first save

### Keeps

- Picks are still copied from the last submitted note (D46, A44).

### Cuts

- S02.05.01's temporary `422`.
- The copied-picks line, dropped by D83.

### Notes

- Runs after T12 because both change the note form, the draft save and Today.

### Steps

Planned when the lane starts it.

---

## T14 Edit a submitted note

```yaml
id: T14
status: new
release: R1
lane: C
size: M
depends_on: [T12, T13]
stories: [S02.06.01, S02.06.02, S02.06.03]
```

### Outcome

The author or any manager corrects a submitted note through a private pending edit. Save changes adds exactly one
version, Cancel changes nothing, and everyone sees the Edited label. A second person editing at the same time is stopped
and shown the newer version, so nothing is silently overwritten.

### Absorbs

- [S02.06.01][] Edit my own submitted note
- [S02.06.02][] Edit any submitted note as a manager
- [S02.06.03][] Handle two people editing the same note

### Notes

- Runs after T13: it changes the same note form and draft save.

### Steps

Planned when the lane starts it.

---

## T15 Daily report as Word and PDF

```yaml
id: T15
status: new
release: R1
lane: B
size: M
depends_on: [T12]
stories: [S04.01.01, S04.01.02]
```

### Outcome

A manager downloads a day's submitted notes as Word or PDF from a first Report screen with both buttons. Under a title
block, each note shows its participant, author, submitted time, goals with ticks and Guided notes exactly as stored,
with no drafts, discarded drafts or counts. The PDF embeds its font, so every character shows.

### Absorbs

- [S04.01.01][] Download a day's notes as a Word file
- [S04.01.02][] Download the day's notes as a PDF

### Notes

- Builds once: the one report model that both renderers read, and the note block that T21's export reuses. T20 adds
  groups, markers and the confidential pages to both files.
- Holds both download buttons, so T17 and T20 do not touch the same files.
- A character DejaVu Sans cannot draw, such as an emoji, shows as U+FFFD in the PDF only, and the Word file keeps it
  (design.md A59, answering the plan's question 3).
- Opening the files in Edge, Acrobat and Word is a by-hand check on ops/first-use-in-test.md.

### Steps

Planned when the lane starts it.

---

## T16 Mark reviewed, and re-flag after an edit

```yaml
id: T16
status: new
release: R1
lane: A
size: L
depends_on: [T11, T14]
stories: [S03.02.01, S03.02.02, S03.02.03, S03.01.02]
```

### Outcome

A manager marks a flagged note reviewed with an optional comment; it leaves To review and shows "Reviewed by … on …". If
another manager got there first or the note was edited, they are told and keep their comment. An edit that adds a flag
or changes its reason sends the note back to To review; removing the tick never takes it out.

### Absorbs

- [S03.02.01][] Mark a flagged note reviewed
- [S03.02.02][] See whether a flag has been reviewed
- [S03.02.03][] Handle a review conflict without losing the comment
- [S03.01.02][] Return a re-flagged note to To review

### Cuts

- The "Earlier reviews" block and the "Go to flagged notes" link (ux/README.md open question 3): not built unless the
  owner approves them.

### Notes

- Adds its rows to T11's flag-rules table test.
- Runs before T18, because both change the read view and the version-saving code.

### Steps

Planned when the lane starts it.

---

## T17 Report screen: choose a day and know what happened

```yaml
id: T17
status: new
release: R1
lane: B
size: L
depends_on: [T15]
stories: [S04.02.01, S04.02.02, S04.02.03, S04.02.04]
```

### Outcome

A manager steps or types to any day up to today, sees plainly when a day has no submitted notes, and is told whether
each download is preparing, ready or failed.

### Absorbs

- [S04.02.01][] Move a day at a time
- [S04.02.02][] Pick a date in the date field
- [S04.02.03][] See when a day has no submitted notes
- [S04.02.04][] Know whether a download worked

### Keeps

- The Report screen's automated axe and keyboard checks (also run in T26).

### Cuts

- The plain-link download fallback: built only if a phone cannot save the file (S04.02.05, cut).

### Notes

- Builds the download hook that T21's export reuses, and adds the date controls to T15's Report screen.
- Saving the file on real phones is a by-hand check on ops/first-use-in-test.md (from S04.02.05).

### Steps

Planned when the lane starts it.

---

## T18 Version history and past-day notes

```yaml
id: T18
status: new
release: R1
lane: C
size: M
depends_on: [T14, T16]
stories: [S02.07.01, S02.07.02, S02.08.01, S02.08.02]
```

### Outcome

A manager sees every version of a note with who saved it and when, and reads any version in full. A manager can also
write and submit a forgotten note for a past date, which shows when and by whom it was written.

### Absorbs

- [S02.07.01][] See a note's versions
- [S02.07.02][] Read one version in full
- [S02.08.01][] Choose the date for a past-day note
- [S02.08.02][] Write and submit a forgotten past-day note

### Notes

- Runs after T16, because both change the read view and the version-saving code.

### Steps

Planned when the lane starts it.

---

## T19 Reviewed list, the Flagged badge, and keeping your place

```yaml
id: T19
status: new
release: R1
lane: A
size: M
depends_on: [T16]
stories: [S03.03.02, S03.03.03, S03.03.04, S03.03.05]
```

### Outcome

A manager switches Flagged to Reviewed and sees who reviewed each flag, when and with what comment, 30 at a time with
Show older, and Back returns to the same row in either list. The Flagged menu shows the To review count, refreshed on
each screen change or window focus with no polling, and the badge and the (n) both drop by one after Mark reviewed.

### Absorbs

- [S03.03.02][] Switch to the reviewed notes
- [S03.03.03][] See the To review count on the Flagged menu
- [S03.03.04][] Show older reviewed notes
- [S03.03.05][] Keep my place in the Flagged lists

### Cuts

- Re-tests of the shared `LoadRegion`, the paged list and the scroll map, which are tested where built.

### Notes

- A build check fails on any SMS or push package, or any email sender other than the setup link. The F03.03 end-to-end
  flow runs in CI.
- Reuses T02's focus and scroll map.

### Steps

Planned when the lane starts it.

---

## T20 Daily report content complete, in both files

```yaml
id: T20
status: new
release: R1
lane: C
size: M
depends_on: [T15, T18]
stories: [S04.01.03, S04.01.04, S04.01.05, S04.01.06]
```

### Outcome

Word and PDF both show common items under each note's picked groups only, with unpicked groups leaving no trace (D47),
and every marker: past-day, submitted on a later day, the flag with its reason or "(flag removed in a later edit)", To
review or Reviewed by, and Edited with the version count, in Melbourne time. Both have a running header from page 2, a
footer with "Page n of N" on every page, and file properties holding only the title and organisation (A19).

### Absorbs

- [S04.01.03][] Show common items by picked group
- [S04.01.04][] Show each note's markers
- [S04.01.05][] Mark every page as confidential
- [S04.01.06][] Download a full day in under 10 seconds

### Notes

- Owns the one text-match test day that checks both files against the stored notes, with no drafts or discarded drafts,
  and the automated test that a 20-note day downloads in under 10 seconds in each format. Timings on real devices go on
  ops/first-use-in-test.md.
- Settles the report's "Reviewed by" wording as the specs give it. Builds on T15's report model.

### Steps

Planned when the lane starts it.

---

## T21 Export a participant's record

```yaml
id: T21
status: new
release: R1
lane: B
size: L
depends_on: [T17, T20]
stories: [S05.01.01, S05.01.02, S05.01.03, S05.01.04]
```

### Outcome

A manager exports one participant's submitted notes for a date range as PDF or Word, with earlier versions if they
choose, to answer an access or correction request. The form says what to fix and why an export failed.

### Absorbs

- [S05.01.01][] Export a participant's record
- [S05.01.02][] Export form checks and failures
- [S05.01.03][] Include earlier versions in the export
- [S05.01.04][] One-year export in under 30 seconds on real devices

### Cuts

- The same-day export wording (record-export.md open question 4): not built unless the owner approves it.

### Notes

- Reuses T15's note block and T17's download hook. A CI test shows that a one-year export with versions takes under 30
  seconds; real-device timing goes on ops/first-use-in-test.md.
- A To date after today is accepted as sent (design.md A60, answering the plan's question 4).

### Steps

Planned when the lane starts it.

---

## T22 Production environment: built, locked, backed up and watched

```yaml
id: T22
status: new
release: R1
lane: D
size: M
depends_on: [T01, Q2]
stories: [S00.02.04, S00.02.05, S06.03.01, S06.04.01, S06.05.01]
```

### Outcome

The operator deploys, by hand, exactly the build that passed in test to production on the product domain. Production has
its own identities, database delete locks, 35-day and 12-month backups in Australia, email alerts to the operator, the
product-domain email sender, and a read-only production check.

### Absorbs

- [S00.02.04][] Production environment and manual deploy
- [S00.02.05][] Database delete lock
- [S06.03.01][] Backup retention set and checked in production
- [S06.04.01][] Uptime and error alerts reach the operator
- [S06.05.01][] Production environment ready and checked

### Keeps

- The go-live gates stay mandatory. They are kept on their own run sheet, apart from the nice-to-have checklist.

### Notes

- Writes `ops/go-live.md`, the ordered run sheet for the mandatory production and go-live steps: the ten design.md §14
  go-live lines, the operator's production Owner steps (grant-identities, locks, the main-only deploy rule), and the
  five-worker words check (S06.02.05, cut) for the managers to arrange.
- Its product-domain sender replaces, in production, the test environment's azurecomm.net sender (A58).
- Waits for owner question Q2, the production domain (A38).

### Steps

Planned when the lane starts it.

---

## T23 Operator runbooks: audit, retention, restore and breach

```yaml
id: T23
status: new
release: R1
lane: D
size: M
depends_on: [T04, T09, Q5, Q7]
stories: [S05.04.01, S05.04.02, S06.03.02, S06.06.01]
```

### Outcome

The operator has four runbooks whose SQL runs in CI on made-up data: audit extraction for a manager's request, the
yearly retention list and destruction with the HPP 4.3 log, the restore drill with `restore-check.sql` and the restore
log, and breach response, which links to the audit runbook rather than repeating it.

### Absorbs

- [S05.04.01][] Retention and destruction runbook
- [S05.04.02][] Audit extraction runbook
- [S06.03.02][] Restore drill runbook
- [S06.06.01][] Breach response runbook

### Notes

- Owner question Q5 holds one line (where the destruction log and the retention list are kept), and Q7 shapes the
  restore drill runbook.

### Steps

Planned when the lane starts it.

---

## T24 Audit and history proof

```yaml
id: T24
status: new
release: R1
lane: A
size: M
depends_on: [T19, T21]
stories: [S05.02.01, S05.02.02, S05.03.01, S05.03.02]
```

### Outcome

CI proves that every audit event type is written exactly once with no note text, that the database refuses UPDATE and
DELETE on history as the runtime user, and that archiving or deactivating changes no past note, read view, version,
report or export.

### Absorbs

- [S05.02.01][] Every audit event type is written and found
- [S05.02.02][] Append-only permissions tested in the database
- [S05.03.01][] Archiving keeps every past record unchanged
- [S05.03.02][] A deactivated person is signed out everywhere and their notes stay

### Cuts

- Most of F05.03's archive and deactivate re-checks, now one cross-app test.
- Re-tests of "a refused change writes no audit row" and of the unknown-email sign-in, already tested where they were
  built.
- The app identity's role check in test, done on 9 October 2026 (S00.02.02).

### Notes

- Completes the shared audit-events test that T02 started, and adds a guard that fails the build if an event type has no
  test row or an append-only table lacks its denies. Reuses T04's session-revocation helper.

### Steps

Planned when the lane starts it.

---

## T25 Security hardening and review

```yaml
id: T25
status: new
release: R1
lane: B
size: M
depends_on: [T19, T21]
stories: [S06.01.01, S06.01.02, S06.01.03, S06.01.05]
```

### Outcome

The four design rate limits answer `429` with `Retry-After`, Dependabot raises alerts and grouped weekly updates, and
the finished app has a ZAP baseline scan of test and a committed ASVS level 2 review, with no high or critical finding
open.

### Absorbs

- [S06.01.01][] OWASP ASVS level 2 self-review
- [S06.01.02][] OWASP ZAP baseline scan
- [S06.01.03][] Dependabot alerts and weekly updates
- [S06.01.05][] Rate limits

### Cuts

- CodeQL and secret scanning (optional in design.md §9.11), and a ZAP job in CI: not built unless the owner approves
  them.

### Notes

- Runs on the finished app once T19 and T21 are in, beside T24 and T26.

### Steps

Planned when the lane starts it.

---

## T26 Accessibility pass on the finished app

```yaml
id: T26
status: new
release: R1
lane: C
size: M
depends_on: [T19, T21]
stories: [S06.02.01, S06.02.02]
```

### Outcome

Every key flow in design.md §4.13 runs keyboard-only in Playwright with axe clean at each step, and no screen scrolls
sideways at 320 px.

### Absorbs

- [S06.02.01][] axe and keyboard-only pass on every screen
- [S06.02.02][] Zoom, text size and contrast checks

### Keeps

- The readability check on worker-facing strings, from S06.02.05 (cut).
- The Report screen's axe and keyboard checks, from S06.02.04 (cut).

### Cuts

- The Dragon 1-second save: added only if the by-hand run shows it is needed (S06.02.03, cut).

### Notes

- Orders the by-hand accessibility steps on ops/first-use-in-test.md into one sequence (screen readers, real devices,
  zoom, contrast and forced colours, the five-worker words check), starting from the steps the cut stories moved there.
  Whether they hold go-live is owner question Q12.

### Steps

Planned when the lane starts it.

---

## T27 Provider's organisation and first manager in production

```yaml
id: T27
status: new
release: R1
lane: D
size: S
depends_on: [T22, T25, Q8]
stories: [S06.05.02]
```

### Outcome

Once the security review has no high or critical finding open and production has passed its check, the operator runs
`admin bootstrap` with the provider's name, and the first manager sets up with MFA. The provider then has about three
weeks in production to invite staff and enter its configuration before cut-over.

### Absorbs

- [S06.05.02][] Provider's organisation and first manager in production

### Notes

- Waits for owner question Q8, how the first manager gets the printed setup link.

### Steps

Planned when the lane starts it.

---

## T28 Restore test and cut-over (Release 1)

```yaml
id: T28
status: new
release: R1
lane: D
size: S
depends_on: [T23, T24, T26, T27, Q6, Q7, Q12]
stories: [S06.06.02, S06.06.03]
```

### Outcome

The latest production backup is restored and checked, then every staff member switches from the Word template on the set
date, on the build that passed in test.

### Absorbs

- [S06.06.02][] Restore test of the production backup
- [S06.06.03][] Cut-over on the set date

### Keeps

- Cut-over waits until every line of the design.md §14 go-live checklist has an owner and evidence: privacy policy,
  collection notice, breach plan naming the operator, signed hosting agreement, all staff set up with MFA and at least
  two active managers, onboarding, configuration checked by a second manager, the restore test, and Word notes gathered,
  removed from laptops and their location recorded.

### Cuts

- A pilot: not run unless the owner asks for one (A40, Q6).

### Notes

- One of the two tasks that wait for a run sheet: `ops/go-live.md`, which T22 writes.

### Steps

Planned when the lane starts it.

---

## T29 Compliance pack for the provider

```yaml
id: T29
status: new
release: R2
lane: B
size: S
depends_on: [T28, Q11]
stories: [S07.05.06, S07.05.07]
```

### Outcome

The provider gets the operator's written check of its own AI provider, and draft wording for its privacy policy,
collection notice, staff notice and hosting agreement, so its lawyer can start while the server is built.

### Absorbs

- [S07.05.06][] The operator's own AI provider checked
- [S07.05.07][] Privacy and hosting agreement wording

### Notes

- A documents-only pull request. Sending the pack is a step on `ops/mcp-switch-on.md`, which T38 writes. Runs beside
  T30.

### Steps

Planned when the lane starts it.

---

## T30 Approved assistants and the authorisation server

```yaml
id: T30
status: new
release: R2
lane: A
size: M
depends_on: [T28]
stories: [S07.01.03, S07.01.04, S07.01.05]
```

### Outcome

The operator registers, lists and removes the assistants the provider approved, and in test an assistant reads the
discovery documents. Wherever `Mcp:Enabled` is off (the default, and production) every MCP and OAuth path returns `404`.

### Absorbs

- [S07.01.03][] Spike: OpenIddict settings for approved assistants
- [S07.01.04][] Authorisation server, off switch and discovery
- [S07.01.05][] Register an approved assistant

### Cuts

- The OpenIddict spike (S07.01.03) as a pull request of its own: its eight questions are answered by tests that stay in
  the suite.

### Notes

- From here Release 2 runs one task after another in lane A, because each MCP tool edits the shared `tools/list`
  snapshot and tool matrix.

### Steps

Planned when the lane starts it.

---

## T31 Support accounts and Connect an AI assistant

```yaml
id: T31
status: new
release: R2
lane: A
size: L
depends_on: [T30, Q10]
stories: [S07.01.01, S07.01.02, S07.01.06, S07.01.07, S07.01.08]
```

### Outcome

A manager invites a Support (IT operator) account, which signs in with MFA and sees only the connect page. A manager or
Support person connects an approved assistant on the Connect screen and stays connected within the app's limits (30
minutes unused, 12 hours at most). Workers are refused, and a refresh after deactivation, reset or a change to worker is
refused.

### Absorbs

- [S07.01.01][] Invite a Support account
- [S07.01.02][] Support sign-in and landing page
- [S07.01.06][] Connect an AI assistant
- [S07.01.07][] Refuse workers and warn about desktop assistants
- [S07.01.08][] Stay connected within the app's session limits

### Notes

- Waits for owner question Q10, the Support option shipping before switch-on.

### Steps

Planned when the lane starts it.

---

## T32 MCP endpoint, audit, and access that ends on the next call

```yaml
id: T32
status: new
release: R2
lane: A
size: M
depends_on: [T31]
stories: [S07.02.01, S07.02.03, S07.02.02]
```

### Outcome

A connected assistant reaches `/mcp`, where every call is checked against the person's current role, status and
organisation, and audited with the assistant that got the data. Deactivate, reset, `admin signout-all`, a change to
worker or removing the assistant's registration revokes its authorisations in the same transaction, and its next `/mcp`
call gets `401`.

### Absorbs

- [S07.02.01][] MCP endpoint with checks on every request
- [S07.02.03][] Audit every MCP call
- [S07.02.02][] Access ends on the next call

### Notes

- Builds the access-ends test that T36 and T37 add their through-MCP rows to. Reuses T04's session-revocation helper.

### Steps

Planned when the lane starts it.

---

## T33 Participant look-ups, with the note-free rules

```yaml
id: T33
status: new
release: R2
lane: A
size: M
depends_on: [T32]
stories: [S07.02.04, S07.02.06, S07.02.07]
```

### Outcome

An assistant can find a participant by name and read their goals, never a date of birth. The build fails if any tool
could return note content.

### Absorbs

- [S07.02.04][] Look up participants and their goals
- [S07.02.06][] Architecture, contract and snapshot rules
- [S07.02.07][] Canary tests over every tool

### Notes

- Builds once: the tool matrix, the contract allow-list, the `tools/list` snapshot and the note canary. Every later tool
  joins them.

### Steps

Planned when the lane starts it.

---

## T34 Participants and goals through an assistant

```yaml
id: T34
status: new
release: R2
lane: A
size: M
depends_on: [T33]
stories: [S07.03.01, S07.03.02, S07.03.03]
```

### Outcome

A manager or Support person asks an approved assistant to add or correct a participant, and to add, reword, reorder,
archive or restore goals, under the same handlers, version checks and audit as Manage.

### Absorbs

- [S07.03.01][] Add and correct participants
- [S07.03.02][] Add and reword goals
- [S07.03.03][] Reorder, archive and restore goals

### Notes

- Adds the archived-participant refusal once, in the shared handlers for both channels, and its code to design.md §6.9
  (D86).

### Steps

Planned when the lane starts it.

---

## T35 Common items, guide prompts and the user list through an assistant

```yaml
id: T35
status: new
release: R2
lane: A
size: L
depends_on: [T34]
stories: [S07.02.05, S07.03.04, S07.03.05, S07.03.06, S07.03.07, S07.03.08]
```

### Outcome

Through an assistant, a manager or Support person lists users, groups, items and the guide prompts, changes groups and
items, and replaces the guide prompts, all under the app's own rules. Every note stays fixed.

### Absorbs

- [S07.02.05][] Look up users, common items and guide prompts
- [S07.03.04][] Add and rename common item groups
- [S07.03.05][] Reorder, archive and restore common item groups
- [S07.03.06][] Add, reword and move common items
- [S07.03.07][] Reorder, archive and restore common items
- [S07.03.08][] Replace the guide prompts

### Notes

- Each tool joins T33's tool matrix, contracts, snapshot and canary.

### Steps

Planned when the lane starts it.

---

## T36 Confirmation before access changes, shipped with Deactivate

```yaml
id: T36
status: new
release: R2
lane: A
size: M
depends_on: [T35, Q9]
stories: [S07.04.01, S07.04.02, S07.04.05]
```

### Outcome

Through an assistant, deactivating a user never acts on the first call: the person confirms in the assistant's own form
where it has one, otherwise after a preview, and the server keeps no state. Support may act only on workers and always
gives a reason.

### Absorbs

- [S07.04.01][] Preview, then confirm
- [S07.04.02][] Confirmation forms where the assistant supports them
- [S07.04.05][] Deactivate a user

### Notes

- Builds the MCP confirmation code that T37 reuses, and adds the through-MCP deactivate row to T32's access-ends test.
  Waits for owner question Q9 (MA6).

### Steps

Planned when the lane starts it.

---

## T37 Invite, reset or resend, and archive or restore a participant through an assistant

```yaml
id: T37
status: new
release: R2
lane: A
size: M
depends_on: [T36]
stories: [S07.04.03, S07.04.04, S07.04.06]
```

### Outcome

Using T36's confirmation, an assistant can invite a user, reset a sign-in or resend an invite, and archive (always
confirmed) or restore a participant, with Support limited to workers.

### Absorbs

- [S07.04.03][] Invite a user
- [S07.04.04][] Reset a user's sign-in or resend an invite
- [S07.04.06][] Archive and restore a participant

### Notes

- Adds the through-MCP reset row to T32's access-ends test.

### Steps

Planned when the lane starts it.

---

## T38 Assistant rate limits, MCP runbooks and the switch-on checklist

```yaml
id: T38
status: new
release: R2
lane: A
size: M
depends_on: [T37]
stories: [S07.05.01, S07.05.02, S07.05.04, S07.05.05]
```

### Outcome

Assistants are rate-limited (60 calls a minute per user, 10 confirmed access changes an hour, and the token endpoint
limits). The breach and audit runbooks gain MCP sections and saved monthly queries, including who invited whom through
an assistant, and every mcp-server.md §8 control is matched to its test.

### Absorbs

- [S07.05.01][] Rate limits for assistants
- [S07.05.02][] Security controls checked against the design
- [S07.05.04][] MCP sections in the breach and audit runbooks
- [S07.05.05][] Monthly and quarterly MCP checks

### Keeps

- The manual tests with two approved assistants (S07.05.03, cut) and the briefing (S07.05.08, cut), as switch-on steps
  that T39 waits for.

### Notes

- Writes `ops/mcp-switch-on.md`, the ordered list of every by-hand and outside step before switch-on, from the
  mcp-server.md §11.1 lines.

### Steps

Planned when the lane starts it.

---

## T39 Switch on Release 2 in production

```yaml
id: T39
status: new
release: R2
lane: A
size: S
depends_on: [T29, T38]
stories: [S07.05.09, S07.05.10]
```

### Outcome

Once every mcp-server.md §11.1 switch-on line has an owner and evidence (the manual tests with two assistants, the
provider's approval, the briefing, the amended hosting agreement), the approved AI providers are named in the provider's
texts, the tested build is deployed, only approved assistants are registered, `Mcp:Enabled` is turned on and the date is
recorded.

### Absorbs

- [S07.05.09][] Switch on in production
- [S07.05.10][] Name the approved AI providers in the wording

### Notes

- One of the two tasks that wait for a run sheet: `ops/mcp-switch-on.md`, which T38 writes.

### Steps

Planned when the lane starts it.

---

## Cut stories

Cut on 14 October 2026 (D87), 15 points in all. Each keeps its section with `status: cut` and a dated Note.

- [S02.02.01][] Melbourne date tests across daylight saving: already built.
  `tests/Grow2Notes.Tests/Platform/MelbourneClockTests.cs` holds the three daylight-saving cases from design.md §14
  M2, CI runs with `TZ: UTC`, and `TestClock.UseClock` (S00.04.01) lets the integration host set the clock. T08 tests
  that the server refuses a worker's note for any other date.
- [S04.02.05][] Save the report on a phone: by-hand checks only (four phone browsers saving the file under the
  production CSP, the iPhone and Android date pickers, and screen readers), now on ops/first-use-in-test.md. The
  plain-link fallback is built only if a phone fails (file-download.md). Its screen-reader part repeats S06.02.04.
- [S06.02.03][] VoiceOver and TalkBack on the note form: by-hand runs only (VoiceOver, TalkBack, NVDA, Voice Control,
  Dragon and Windows voice typing), now on ops/first-use-in-test.md, which T26 orders. WCAG 2.2 AA still applies and
  any failure becomes a bug. The Dragon 1-second save is added only if that run shows it is needed
  (guided-notes-textarea.md, *Behaviour* item 6). Whether go-live waits for it is owner question Q12.
- [S06.02.04][] VoiceOver and TalkBack on the Report screen: by-hand runs only, now on ops/first-use-in-test.md. The
  Report screen's automated axe and keyboard checks stay in T17 and T26. Whether go-live waits for it is owner
  question Q12.
- [S06.02.05][] Button words checked with five workers: it needs the provider's people, which no build lane can
  arrange, so it would hold a task open for ever. It is on ops/first-use-in-test.md, and because microcopy.md asks
  for it before go-live, T22 also lists it on `ops/go-live.md` for the managers to arrange; whether go-live waits for
  it is owner question Q12. Its readability check stays in T26. No wording changes without the owner's approval.
- [S07.05.03][] Manual tests with two approved assistants: all by hand (real approved assistants on a phone and a
  laptop, the prompt-injection test and the MCP Inspector). It becomes steps on `ops/mcp-switch-on.md`, which T38
  writes. It holds no task open, but switch-on (T39) still waits for it, as mcp-server.md §11.1 line 1 requires, and
  it feeds the provider's §7.1 check. Defects it finds become bugs.
- [S07.05.08][] Briefing for managers and the Support person: a meeting, not build work. It becomes a line on
  `ops/mcp-switch-on.md`, given from mcp-server.md §11.4 and the manual test record; its date and attendees are
  recorded there, and switch-on (T39) waits for it.

## Open owner questions

Numbered as in the condensed plan the owner agreed on 14 October 2026; Q1, Q3 and Q4 are answered (below). Each holds
only the tasks named. Everything else still open in the specs is built as they say unless the owner says otherwise.

- **Q2. The production domain (A38).** Blocks T22, and so T27 and T28. Suggested: pick a Grow2Notes domain now. It
  cannot be the parent company's domain or a subdomain of it, and it cannot change after the first real passkey.
- **Q5. Where the HPP 4.3 destruction log and the yearly retention list are kept.** Blocks one line of T23.
  Suggested: the provider's own records storage, named in the runbook and never in the public repository.
- **Q6. The switch-over date, who runs staff onboarding, and whether to pilot.** Blocks T28. Suggested: the owner
  and the managers set the date, the managers run the 30-minute onboarding, and there is no pilot.
- **Q7. The restore test while production holds no notes before go-live.** Shapes T23's restore drill runbook and
  blocks T28. Suggested: before go-live, check the configuration rows, `restore-check.sql` and `DBCC CHECKDB`; open a
  known note and its history at the first drill after go-live.
- **Q8. How the first manager gets their setup link.** Bootstrap prints the link and sends no email. Blocks T27.
  Suggested: the operator gives the printed link to the first manager in person or by phone, with no code change.
- **Q9. Is confirming inside the assistant enough for the four access tools?** (mcp-server.md §12 Q4, MA6.) Blocks
  T36 and T37. Suggested: yes, keep MA6: the assistant's own confirmation form where it has one, and preview then
  confirm otherwise.
- **Q10. The Support option shipping before switch-on.** Blocks T31. Suggested: accept it; no code is needed.
- **Q11. The operator's AI provider and account type, and the provider's privacy contact.** Blocks T29. Suggested:
  the owner names the business AI account the operator will use, and the provider names its privacy contact.
- **Q12. Do the by-hand accessibility checks hold go-live?** design.md §14 M6 names VoiceOver and TalkBack, and
  microcopy.md asks for the five-worker words check before go-live. Affects T28 only. Suggested: no, under the
  owner's rule of 12 October 2026: do them in test when possible, and turn any failure into a bug.

**Answered on 14 October 2026.** The developer took the plan's suggested answers to three questions as assumed
defaults, which the owner may overturn:

- **Q1. The test email sender** (T01): design.md A58, the free Azure-managed azurecomm.net domain in test.
- **Q3. PDF characters the font lacks** (T15): design.md A59, the replacement mark U+FFFD in the PDF only.
- **Q4. A record export's To date after today** (T21): design.md A60, accepted as sent.

[S00.02.04]: E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000204-production-environment-and-manual-deploy
[S00.02.05]: E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000205-database-delete-lock
[S00.02.07]: E00-skeleton-hosting-sign-in/F00.02-azure-hosting-and-deployment.md#s000207-setup-link-email
[S00.03.06]: E00-skeleton-hosting-sign-in/F00.03-tenancy-audit-operator-commands.md#s000306-break-glass-reset-and-sign-out-of-everyone
[S00.04.08]: E00-skeleton-hosting-sign-in/F00.04-sign-in-setup-sessions.md#s000408-sign-in-again-where-i-was
[S00.05.02]: E00-skeleton-hosting-sign-in/F00.05-app-shell.md#s000502-keep-my-place-when-i-move-between-screens
[S00.06.02]: E00-skeleton-hosting-sign-in/F00.06-users.md#s000602-invite-a-user
[S00.06.03]: E00-skeleton-hosting-sign-in/F00.06-users.md#s000603-resend-an-invite
[S00.06.04]: E00-skeleton-hosting-sign-in/F00.06-users.md#s000604-reset-someones-sign-in
[S00.06.05]: E00-skeleton-hosting-sign-in/F00.06-users.md#s000605-deactivate-and-reactivate-a-user
[S00.06.06]: E00-skeleton-hosting-sign-in/F00.06-users.md#s000606-change-a-users-name-email-or-role
[S01.02.01]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010201-see-and-find-participants
[S01.02.02]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010202-add-a-participant
[S01.02.03]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010203-correct-a-participants-details
[S01.02.04]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010204-archive-and-restore-a-participant
[S01.02.05]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010205-add-goals-for-a-participant
[S01.02.06]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010206-reword-a-goal
[S01.02.07]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010207-reorder-goals
[S01.02.08]: E01-participants-goals-common-items-guide-prompts/F01.02-participants-and-goals.md#s010208-archive-and-restore-goals
[S01.03.01]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010301-every-note-group-built-in
[S01.03.02]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010302-add-and-reword-common-items
[S01.03.03]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010303-reorder-common-items-in-a-group
[S01.03.04]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010304-archive-and-restore-common-items
[S01.03.05]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010305-add-and-rename-groups
[S01.03.06]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010306-reorder-groups
[S01.03.07]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010307-archive-and-restore-groups
[S01.03.08]: E01-participants-goals-common-items-guide-prompts/F01.03-common-items-and-groups.md#s010308-move-a-common-item-to-another-group
[S01.04.01]: E01-participants-goals-common-items-guide-prompts/F01.04-guide-prompts.md#s010401-set-the-guide-prompts
[S01.04.02]: E01-participants-goals-common-items-guide-prompts/F01.04-guide-prompts.md#s010402-warning-before-leaving-unsaved-prompts
[S02.01.01]: E02-note-form-autosave-submit-edit-history/F02.01-today-list.md#s020101-see-every-active-participant-on-today
[S02.01.02]: E02-note-form-autosave-submit-edit-history/F02.01-today-list.md#s020102-find-a-participant
[S02.01.03]: E02-note-form-autosave-submit-edit-history/F02.01-today-list.md#s020103-see-todays-note-status-on-each-row
[S02.01.04]: E02-note-form-autosave-submit-edit-history/F02.01-today-list.md#s020104-continue-an-unfinished-draft-from-today
[S02.02.01]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020201-melbourne-date-tests-across-daylight-saving
[S02.02.02]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020202-start-a-note-and-autosave-the-guided-notes
[S02.02.03]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020203-tick-the-participants-goals
[S02.02.04]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020204-keep-autosaving-when-the-connection-drops
[S02.02.05]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020205-keep-the-draft-through-sign-out-and-leaving-the-form
[S02.02.06]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020206-carry-on-in-another-tab-or-on-another-device
[S02.02.07]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020207-one-note-per-participant-per-day
[S02.02.08]: E02-note-form-autosave-submit-edit-history/F02.02-note-form-and-autosave.md#s020208-discard-a-draft
[S02.03.01]: E02-note-form-autosave-submit-edit-history/F02.03-submit-and-flag.md#s020301-submit-a-note-after-a-name-check
[S02.03.02]: E02-note-form-autosave-submit-edit-history/F02.03-submit-and-flag.md#s020302-flag-a-note-for-a-manager
[S02.03.03]: E02-note-form-autosave-submit-edit-history/F02.03-submit-and-flag.md#s020303-submit-a-draft-from-an-earlier-day
[S02.04.01]: E02-note-form-autosave-submit-edit-history/F02.04-past-notes-and-read-view.md#s020401-read-a-submitted-note
[S02.04.02]: E02-note-form-autosave-submit-edit-history/F02.04-past-notes-and-read-view.md#s020402-browse-a-participants-past-notes
[S02.04.03]: E02-note-form-autosave-submit-edit-history/F02.04-past-notes-and-read-view.md#s020403-see-someone-elses-draft-and-discard-it-as-a-manager
[S02.05.01]: E02-note-form-autosave-submit-edit-history/F02.05-common-items-and-group-picks.md#s020501-tick-the-every-note-items
[S02.05.02]: E02-note-form-autosave-submit-edit-history/F02.05-common-items-and-group-picks.md#s020502-tick-which-groups-happened-and-their-items
[S02.05.03]: E02-note-form-autosave-submit-edit-history/F02.05-common-items-and-group-picks.md#s020503-start-with-the-last-notes-group-picks
[S02.05.04]: E02-note-form-autosave-submit-edit-history/F02.05-common-items-and-group-picks.md#s020504-re-check-ticks-when-groups-or-items-change-before-the-first-save
[S02.06.01]: E02-note-form-autosave-submit-edit-history/F02.06-edit-a-submitted-note.md#s020601-edit-my-own-submitted-note
[S02.06.02]: E02-note-form-autosave-submit-edit-history/F02.06-edit-a-submitted-note.md#s020602-edit-any-submitted-note-as-a-manager
[S02.06.03]: E02-note-form-autosave-submit-edit-history/F02.06-edit-a-submitted-note.md#s020603-handle-two-people-editing-the-same-note
[S02.07.01]: E02-note-form-autosave-submit-edit-history/F02.07-version-history.md#s020701-see-a-notes-versions
[S02.07.02]: E02-note-form-autosave-submit-edit-history/F02.07-version-history.md#s020702-read-one-version-in-full
[S02.08.01]: E02-note-form-autosave-submit-edit-history/F02.08-past-day-notes.md#s020801-choose-the-date-for-a-past-day-note
[S02.08.02]: E02-note-form-autosave-submit-edit-history/F02.08-past-day-notes.md#s020802-write-and-submit-a-forgotten-past-day-note
[S03.01.01]: E03-flag-review/F03.01-flag-for-manager.md#s030101-send-a-submitted-flag-to-to-review
[S03.01.02]: E03-flag-review/F03.01-flag-for-manager.md#s030102-return-a-re-flagged-note-to-to-review
[S03.02.01]: E03-flag-review/F03.02-mark-reviewed.md#s030201-mark-a-flagged-note-reviewed
[S03.02.02]: E03-flag-review/F03.02-mark-reviewed.md#s030202-see-whether-a-flag-has-been-reviewed
[S03.02.03]: E03-flag-review/F03.02-mark-reviewed.md#s030203-handle-a-review-conflict-without-losing-the-comment
[S03.03.01]: E03-flag-review/F03.03-flagged-notes-lists-and-badge.md#s030301-see-the-flagged-notes-to-review
[S03.03.02]: E03-flag-review/F03.03-flagged-notes-lists-and-badge.md#s030302-switch-to-the-reviewed-notes
[S03.03.03]: E03-flag-review/F03.03-flagged-notes-lists-and-badge.md#s030303-see-the-to-review-count-on-the-flagged-menu
[S03.03.04]: E03-flag-review/F03.03-flagged-notes-lists-and-badge.md#s030304-show-older-reviewed-notes
[S03.03.05]: E03-flag-review/F03.03-flagged-notes-lists-and-badge.md#s030305-keep-my-place-in-the-flagged-lists
[S04.01.01]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040101-download-a-days-notes-as-a-word-file
[S04.01.02]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040102-download-the-days-notes-as-a-pdf
[S04.01.03]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040103-show-common-items-by-picked-group
[S04.01.04]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040104-show-each-notes-markers
[S04.01.05]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040105-mark-every-page-as-confidential
[S04.01.06]: E04-daily-report-pdf-word/F04.01-daily-report-files.md#s040106-download-a-full-day-in-under-10-seconds
[S04.02.01]: E04-daily-report-pdf-word/F04.02-report-screen.md#s040201-move-a-day-at-a-time
[S04.02.02]: E04-daily-report-pdf-word/F04.02-report-screen.md#s040202-pick-a-date-in-the-date-field
[S04.02.03]: E04-daily-report-pdf-word/F04.02-report-screen.md#s040203-see-when-a-day-has-no-submitted-notes
[S04.02.04]: E04-daily-report-pdf-word/F04.02-report-screen.md#s040204-know-whether-a-download-worked
[S04.02.05]: E04-daily-report-pdf-word/F04.02-report-screen.md#s040205-save-the-report-on-a-phone
[S05.01.01]: E05-participant-export-audit-completeness-retention/F05.01-participant-record-export.md#s050101-export-a-participants-record
[S05.01.02]: E05-participant-export-audit-completeness-retention/F05.01-participant-record-export.md#s050102-export-form-checks-and-failures
[S05.01.03]: E05-participant-export-audit-completeness-retention/F05.01-participant-record-export.md#s050103-include-earlier-versions-in-the-export
[S05.01.04]: E05-participant-export-audit-completeness-retention/F05.01-participant-record-export.md#s050104-one-year-export-in-under-30-seconds-on-real-devices
[S05.02.01]: E05-participant-export-audit-completeness-retention/F05.02-audit-log-append-only-records.md#s050201-every-audit-event-type-is-written-and-found
[S05.02.02]: E05-participant-export-audit-completeness-retention/F05.02-audit-log-append-only-records.md#s050202-append-only-permissions-tested-in-the-database
[S05.03.01]: E05-participant-export-audit-completeness-retention/F05.03-archive-deactivate-end-to-end.md#s050301-archiving-keeps-every-past-record-unchanged
[S05.03.02]: E05-participant-export-audit-completeness-retention/F05.03-archive-deactivate-end-to-end.md#s050302-a-deactivated-person-is-signed-out-everywhere-and-their-notes-stay
[S05.04.01]: E05-participant-export-audit-completeness-retention/F05.04-retention-audit-extraction-runbooks.md#s050401-retention-and-destruction-runbook
[S05.04.02]: E05-participant-export-audit-completeness-retention/F05.04-retention-audit-extraction-runbooks.md#s050402-audit-extraction-runbook
[S06.01.01]: E06-hardening-and-go-live/F06.01-security-review-hardening.md#s060101-owasp-asvs-level-2-self-review
[S06.01.02]: E06-hardening-and-go-live/F06.01-security-review-hardening.md#s060102-owasp-zap-baseline-scan
[S06.01.03]: E06-hardening-and-go-live/F06.01-security-review-hardening.md#s060103-dependabot-alerts-and-weekly-updates
[S06.01.05]: E06-hardening-and-go-live/F06.01-security-review-hardening.md#s060105-rate-limits
[S06.02.01]: E06-hardening-and-go-live/F06.02-accessibility-checks.md#s060201-axe-and-keyboard-only-pass-on-every-screen
[S06.02.02]: E06-hardening-and-go-live/F06.02-accessibility-checks.md#s060202-zoom-text-size-and-contrast-checks
[S06.02.03]: E06-hardening-and-go-live/F06.02-accessibility-checks.md#s060203-voiceover-and-talkback-on-the-note-form
[S06.02.04]: E06-hardening-and-go-live/F06.02-accessibility-checks.md#s060204-voiceover-and-talkback-on-the-report-screen
[S06.02.05]: E06-hardening-and-go-live/F06.02-accessibility-checks.md#s060205-button-words-checked-with-five-workers
[S06.03.01]: E06-hardening-and-go-live/F06.03-backups-restore-drill.md#s060301-backup-retention-set-and-checked-in-production
[S06.03.02]: E06-hardening-and-go-live/F06.03-backups-restore-drill.md#s060302-restore-drill-runbook
[S06.04.01]: E06-hardening-and-go-live/F06.04-monitoring-alerts.md#s060401-uptime-and-error-alerts-reach-the-operator
[S06.05.01]: E06-hardening-and-go-live/F06.05-production-set-up.md#s060501-production-environment-ready-and-checked
[S06.05.02]: E06-hardening-and-go-live/F06.05-production-set-up.md#s060502-providers-organisation-and-first-manager-in-production
[S06.06.01]: E06-hardening-and-go-live/F06.06-go-live-readiness-cut-over.md#s060601-breach-response-runbook
[S06.06.02]: E06-hardening-and-go-live/F06.06-go-live-readiness-cut-over.md#s060602-restore-test-of-the-production-backup
[S06.06.03]: E06-hardening-and-go-live/F06.06-go-live-readiness-cut-over.md#s060603-cut-over-on-the-set-date
[S07.01.01]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070101-invite-a-support-account
[S07.01.02]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070102-support-sign-in-and-landing-page
[S07.01.03]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070103-spike-openiddict-settings-for-approved-assistants
[S07.01.04]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070104-authorisation-server-off-switch-and-discovery
[S07.01.05]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070105-register-an-approved-assistant
[S07.01.06]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070106-connect-an-ai-assistant
[S07.01.07]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070107-refuse-workers-and-warn-about-desktop-assistants
[S07.01.08]: E07-admin-mcp-server/F07.01-support-role-sign-in-connect-assistant.md#s070108-stay-connected-within-the-apps-session-limits
[S07.02.01]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070201-mcp-endpoint-with-checks-on-every-request
[S07.02.02]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070202-access-ends-on-the-next-call
[S07.02.03]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070203-audit-every-mcp-call
[S07.02.04]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070204-look-up-participants-and-their-goals
[S07.02.05]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070205-look-up-users-common-items-and-guide-prompts
[S07.02.06]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070206-architecture-contract-and-snapshot-rules
[S07.02.07]: E07-admin-mcp-server/F07.02-mcp-endpoint-look-ups-audit.md#s070207-canary-tests-over-every-tool
[S07.03.01]: E07-admin-mcp-server/F07.03-setup-tools.md#s070301-add-and-correct-participants
[S07.03.02]: E07-admin-mcp-server/F07.03-setup-tools.md#s070302-add-and-reword-goals
[S07.03.03]: E07-admin-mcp-server/F07.03-setup-tools.md#s070303-reorder-archive-and-restore-goals
[S07.03.04]: E07-admin-mcp-server/F07.03-setup-tools.md#s070304-add-and-rename-common-item-groups
[S07.03.05]: E07-admin-mcp-server/F07.03-setup-tools.md#s070305-reorder-archive-and-restore-common-item-groups
[S07.03.06]: E07-admin-mcp-server/F07.03-setup-tools.md#s070306-add-reword-and-move-common-items
[S07.03.07]: E07-admin-mcp-server/F07.03-setup-tools.md#s070307-reorder-archive-and-restore-common-items
[S07.03.08]: E07-admin-mcp-server/F07.03-setup-tools.md#s070308-replace-the-guide-prompts
[S07.04.01]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070401-preview-then-confirm
[S07.04.02]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070402-confirmation-forms-where-the-assistant-supports-them
[S07.04.03]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070403-invite-a-user
[S07.04.04]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070404-reset-a-users-sign-in-or-resend-an-invite
[S07.04.05]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070405-deactivate-a-user
[S07.04.06]: E07-admin-mcp-server/F07.04-user-tools-with-confirmation.md#s070406-archive-and-restore-a-participant
[S07.05.01]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070501-rate-limits-for-assistants
[S07.05.02]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070502-security-controls-checked-against-the-design
[S07.05.03]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070503-manual-tests-with-two-approved-assistants
[S07.05.04]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070504-mcp-sections-in-the-breach-and-audit-runbooks
[S07.05.05]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070505-monthly-and-quarterly-mcp-checks
[S07.05.06]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070506-the-operators-own-ai-provider-checked
[S07.05.07]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070507-privacy-and-hosting-agreement-wording
[S07.05.08]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070508-briefing-for-managers-and-the-support-person
[S07.05.09]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070509-switch-on-in-production
[S07.05.10]: E07-admin-mcp-server/F07.05-hardening-manual-tests-switch-on.md#s070510-name-the-approved-ai-providers-in-the-wording
