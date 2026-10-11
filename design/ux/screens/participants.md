# Participants list and participant detail (goals, past-day note)

Screen key: `participants`. design.md section 4.8 ("Participants and goals (managers)"), with 3.2, 3.3, 3.7, 3.8,
3.9, A2, A5, A6, A11, A32 and the API in 6.3 and 6.6. Decisions D8, D20, D21, D22, D24, D25, D35 and D42 apply.

This file composes researched component specs into four views that managers reach through **Manage > Participants**:

| View | Route (illustrative; URLs hold only IDs and dates, design 4.0) | Page title |
|---|---|---|
| Participants list | `/manage/participants` and `/manage/participants?view=archived` | Grow2Notes – Participants |
| Add participant | `/manage/participants/new` | Grow2Notes – Add participant |
| Participant detail (Details, Goals, Actions) | `/manage/participants/{participantId}` | Grow2Notes – Participant |
| Write past-day note | `/manage/participants/{participantId}/past-day-note` | Grow2Notes – Write past-day note |

It adds no screen, field, setting, notification or data that design.md does not already have. "Add participant" and
"Write past-day note" are design.md's own actions; they get their own URLs because each is a form the manager moves
to and comes back from (`primary-actions.md`: "Write past-day note … go[es] to another URL").

**Copy marks** (from `../components/microcopy.md`): **(V)** word for word from design.md · **(N)** design.md wording
with only punctuation or a date format normalised · **(P)** proposed, not in design.md. Every (P) string here on
9 October 2026 was approved as written (D67, Open question 1); a (P) string added later still needs the owner's
sign-off.

**Evidence grades:** **[Research]** studies and usability or assistive-technology testing · **[Standard]** WCAG 2.2,
WAI-ARIA, HTML spec, framework docs · **[Convention]** established design systems · **[Opinion]** reasoned judgement.
Every grade below is carried over from the component file named next to it, where the full evidence and sources sit.

---

## Purpose and who uses it

**Purpose (design 4.8):** "keep the participant list and each participant's flat list of goals (D8)", and give
managers the per-participant actions: Past notes (4.4), Write past-day note (3.8), Export record (4.12), and Archive
participant or Restore (3.7).

**Who:** managers only (design 2, "Add, edit, reorder, archive or restore participants and their goals"). Workers
never see these views: every route here renders "Page not found" for a worker (`app-shell-nav.md` role guard), and
the API returns `403`/`404`.

**How managers use it** (design 4.0, 4.13, 14 M1):
- Mostly on a **laptop**, sometimes on a **phone**. The M1 "done when" test is phone-first: "A manager can set up a
  participant with five goals on a phone without help." Every layout below is built for that test.
- **Rare, deliberate tasks:** set up a new participant and their goals before go-live or on intake; fix a typo in a
  goal; reorder or archive goals as plans change; archive a participant who has left; write a forgotten note
  ("Forgotten note" flow: Manager → Participants → participant → Write past-day note → pick date → form → Submit).
- **What matters most:** the participant's full name is always in view (3.9 wrong-participant guard); every change
  is shown only after the server confirms it (ui-ux-design invariant I5); nothing about a participant is stored on
  the manager's personal device (D21, D22).

---

## Layout - phone

At about 375 px wide, one column, 16 px gutters (`foundations.md`). The app shell header and nav sit above every view
(`app-shell-nav.md`); "Manage" is the current section (`aria-current="true"`). Nothing on these views is sticky.

### Participants list (Active view, loaded)

```
+-------------------------------------+
| Grow2Notes               Account v  |  app shell (app-shell-nav.md)
| Today  Flagged 3  Report  Manage    |  Manage = current section
+-------------------------------------+
| Participants                        |  <h1>
|                                     |
| [         Add participant         ] |  <a> styled as primary button, full width, 48 px
|                                     |
| Find a participant                  |  <search>: visible <label>
| [(o)                    ] [ Clear ] |  type="text", 48 px; Clear only while text is typed
|                                     |
|  Active            Archived         |  <nav aria-labelledby=h1>: two links, equal halves
|  ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀                   |  current: bold + 4 px bar; other: link colour, underlined
| ----------------------------------- |
| Jane Citizen                      > |  whole row is one link, 56 px
| ----------------------------------- |
| Priya Kumar                       > |
| ----------------------------------- |
| Tom Ward                          > |
| ----------------------------------- |
+-------------------------------------+
```

No-match: the rows are replaced by `No participant matches ‘xyz’.` in plain body text. Archived view: the same
layout with "Archived" current and only archived names.

### Add participant (before the first Save)

```
+-------------------------------------+
| < Participants                      |  back link (shell's before-main bar slot), 44 px
+-------------------------------------+
| [error summary: only after a failed Save; first in <main>, above the <h1>]
| Add participant                     |  <h1>
|                                     |
| Details                             |  <h2>
| Given name                          |  <label>
| [                         ]         |  max 20.5em; full column on a phone
| Family name                         |
| [                         ]         |
| Date of birth                       |  <fieldset role="group"><legend>
| For example, 27 3 1987              |  hint
| Day    Month   Year                 |  three <label>s
| [    ] [    ]  [        ]           |  inputmode=numeric; 2.75em / 2.75em / 4.5em
|                                     |  <div role="alert"> (empty; request failures)
| [              Save               ] |  primary, full width
|                                     |  <p role="status"> (empty until saved)
+-------------------------------------+
```

After `201` the same page becomes the participant's detail page (below): the `<h1>` becomes the name, "Saved 4:12 pm"
appears next to Save, and the Goals and Actions sections appear under it.

### Participant detail (active participant)

```
+-------------------------------------+
| < Participants                      |  back link (shell's before-main bar slot)
+-------------------------------------+
| [error summary: only after a failed Details Save; above the <h1>]
| Jane Citizen                        |  <h1>, full name as entered, 28 px bold
|                                     |
| Details                             |  <h2>
| Given name                          |
| [Jane                     ]         |
| Family name                         |
| [Citizen                  ]         |
| Date of birth                       |
| For example, 27 3 1987              |
| Day    Month   Year                 |
| [7   ] [3   ]  [1987    ]           |
| [              Save               ] |
| Saved 4:12 pm                       |  role="status"; cleared on the next edit
|                                     |
| Goals                               |  <h2>
| Changes apply to notes started      |  help text (V)
| from now on. Notes already started  |
| or submitted keep the wording they  |
| were written with.                  |
|  1. Makes own breakfast             |  <ol> numbered rows
|     [^ Move up ] [v Move down ]     |  Move up on row 1: unavailable (dashed)
|     [ Edit ] [ Archive ]            |  every button >= 44 x 44 px, 8 px gaps
| ----------------------------------- |
|  2. Catches the bus to day program  |
|     and phones when she arrives     |
|     [^ Move up ] [v Move down ]     |
|     [ Edit ] [ Archive ]            |
| ----------------------------------- |
|  3. Phones her sister               |
|     [^ Move up ] [v Move down ]     |  Move down on the last row: unavailable
|     [ Edit ] [ Archive ]            |
|                                     |
| New goal                            |  <label>
| [                               ]   |  <textarea rows=3>, no maxlength
| [                               ]   |
| You can enter up to 200 characters  |  character count
| [            Add goal             ] |  primary, full width
|                                     |
| > Archived goals                    |  <details>, closed; only if any are archived
|                                     |
| Actions                             |  <h2>
| [           Past notes            ] |  links styled as secondary buttons
| [       Write past-day note       ] |
| [          Export record          ] |
| ----------------------------------- |  separating rule
| [       Archive participant       ] |  <button>, secondary (not red), no dialog
+-------------------------------------+
```

### Participant detail (archived participant)

```
+-------------------------------------+
| < Participants                      |  back link (to the Archived view)
|                                     |
| Jane Citizen                        |  <h1>
| +---------------------------------+ |
| |▌ i  Jane Citizen is archived,   | |  notice: neutral bar + "i", plain content
| |     so this page is read-only.  | |  focused only when it appears because of Archive
| |     They are not on Today and   | |
| |     no new notes can be written | |
| |     for them. Their past notes  | |
| |     are kept.                   | |
| |  [            Restore         ] | |  <button>, full width, 44 px+
| +---------------------------------+ |
| Details                             |  <h2>
| Given name                          |  <dl>: <dt> above <dd> on a phone
| Jane                                |
| Family name                         |
| Citizen                             |
| Date of birth                       |
| 7 March 1987                        |  datePlain
|                                     |
| Goals                               |  <h2>
|  1. Makes own breakfast             |  numbered texts, no buttons, no help text
|  2. Phones her sister               |
| > Archived goals                    |  texts only, no Restore (only if any)
|                                     |
| Actions                             |  <h2>
| [           Past notes            ] |
| [          Export record          ] |  exports are unchanged by archiving (3.7)
+-------------------------------------+
```

### Write past-day note

```
+-------------------------------------+
| < Participant                       |  back link (or "< Past notes" if opened from 4.4)
|                                     |
| Jane Citizen                        |  caption, inside the <h1>
| Write past-day note                 |  <h1>
|                                     |
| Note date                           |  <label>
| A date before today                 |  hint
| [                            [cal] ]|  <input type="date" max="2026-09-30">, empty
|                                     |
| [            Continue             ] |  primary submit, full width
+-------------------------------------+
```

---

## Layout - laptop

One breakpoint, `@media (min-width: 40rem)` (`foundations.md`). The DOM order never changes with width, so focus
order and reading order are the same on every device.

| Part | What changes at 40rem and wider |
|---|---|
| Page | Gutters 32 px, section gaps 48 px, h1 32 px, h2 24 px. The setup screens may use `--page-max` (60rem) (design 4.0: "the setup screens use the extra space"). |
| Participants list | Content column capped at `--measure` (40rem), as on Today (`participant-list-rows.md`): a name and its chevron far apart on a wide screen help nobody. **Add participant** sizes to its label. The search field stops at about 32rem with Clear beside it. The Active / Archived links are a fixed 12em each, left-aligned (`tabs-segmented.md`). |
| Details form | Stays in a 40rem column. Name inputs stop at 20.5em. **Given name and Family name stay stacked**, never side by side (`text-and-date-inputs.md`, [Opinion]: same order and tab order as the phone). Save sizes to its label, with "Saved 4:12 pm" on the same line. |
| Goals | The list uses the wider page. Each row is a **wrapping flex line** (the same CSS as common-items.md, `list-editor.md`): text `flex: 1 1 16rem` on the left, the four buttons at their natural width on one line at the right when they fit, in the same order. When the text column would fall under 16rem (200% text, a narrow window) the buttons drop under the text by themselves. No container query and no breakpoint of its own. **Add goal** sizes to its label. |
| Archived notice | Restore follows the text in a wrapping flex line, so it sits after the text when there is room and drops under it otherwise. No container query. |
| Read-only Details | `<dl>` as two columns: label left, value right. |
| Actions | The three links sit side by side and wrap; the rule, then **Archive participant**, sized to its label. |
| Write past-day note | Field `max-inline-size: 12em`; Continue sized to its label. |
| Hover | Only on `@media (hover: hover)`: row background tint and underline on list rows, button tints, underline thickening on links (`foundations.md`). |

---

## Components, in order

Each entry links to the component spec that holds the evidence. Only the settings specific to this screen are
listed here.

### A. Participants list

**A1. Page heading.** `<h1 id="page-heading" tabIndex={-1}>` "Participants" (the Manage page's own link text,
`app-shell-nav.md`). It is fixed text, so the shell focuses it on arrival. Its `id` names the Active / Archived
`<nav>`. [Research: Gatsby/Fable client-routing test, n = 5, heading focus was the best experience,
https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/]

**A2. Add participant** → [`primary-actions.md`](../components/primary-actions.md)
- Text "Add participant" **(V)**. A React Router `<Link to="/manage/participants/new">` with the primary button class:
  it goes to another URL, so it is a link (announced as a link, Back works). [Convention: primary-actions.md]
- **Placed directly under the `<h1>`, before the search box.** design.md lists "search, an Active / Archived toggle,
  names, and 'Add participant'". `search-filter.md` requires that "Add participant stays visible and in the same place
  whatever is typed". Below a list that the filter shortens, it would move; above the search, it never moves.
  [Opinion]
- Shown in every loaded state, including "No participants yet." (no second copy of the link there:
  `empty-loading-error.md`).

**A3. Find a participant** → [`search-filter.md`](../components/search-filter.md)
- `<search>` landmark with no `<form>`; visible `<label>` "Find a participant" **(V)**; `<input type="text">` with no
  placeholder, `autoComplete="off"`, `autoCorrect="off"`, `spellCheck={false}`, no `autofocus`; at least 48 px tall;
  text at 18 px (`foundations.md`; anything at or over 16 px stops iOS zoom). [Research: Roselli on `type="search"`,
  https://adrianroselli.com/2019/07/ignore-typesearch.html; Standard: HTML `<search>`,
  https://html.spec.whatwg.org/multipage/grouping-content.html#the-search-element]
- **Clear** button: visible "Clear" plus hidden " search", at least 44 × 44 px, shown only while the field has text;
  returns focus to the field.
- Hidden description: "The list of participants below changes as you type." (P, search-filter.md)
- Filters **whichever view is shown**, on every keystroke, with no debounce. Matching: every typed word starts a word
  in "given name + family name", ignoring accents, case, apostrophes and punctuation (`matchesName`). Names only.
- The typed text **survives a switch between Active and Archived** (same mounted screen) and **is cleared when the
  manager leaves the screen**. It never goes into the URL, title, router state, query keys or storage (D22, 4.0).
- **Hidden** while the list is loading, after a failed load, and when there are no participants at all (Active and
  Archived both empty). A view that is empty only because of the switch keeps the box.

**A4. Active / Archived** → [`tabs-segmented.md`](../components/tabs-segmented.md)
- `ViewLinks`: `<nav aria-labelledby="page-heading">` holding a `<ul>` of two `<Link>`s: "Active" **(V)** to
  `/manage/participants`, "Archived" **(V)** to `/manage/participants?view=archived`. `aria-current="page"` on the
  current one. No ARIA tabs, no switch, no "Show archived" checkbox, no count. [Convention: GOV.UK, NHS, AgDS and
  MoJ say views that change the URL are links, https://design-patterns.service.justice.gov.uk/components/sub-navigation/;
  Research: Baymard back-button expectations, https://baymard.com/blog/back-button-expectations]
- An ordinary pushed navigation with `preventScrollReset`; focus stays on the link; nothing is announced. Any other
  `view` value falls back to Active with `replace`.
- Both views come from **one** query (`GET /api/admin/participants?includeArchived=true`), filtered on `status` in
  memory, so switching never loads.
- Placed after the search box (design order "search, an Active / Archived toggle, names").

**A5. Participant rows** → row pattern from [`participant-list-rows.md`](../components/participant-list-rows.md)
(built for Today; reused here with no status line)
- `<ul>`; each `<li>` holds one `<Link to="/manage/participants/{id}">` whose `::after` covers the whole row
  (56 px minimum). Link text: given name then family name, exactly as stored ("Jane Citizen"), never truncated,
  `overflow-wrap: anywhere`. Decorative chevron `aria-hidden="true"`. No status line, no tag: the current view already
  names Active or Archived (`tabs-segmented.md`).
- **Order:** family name, then given name (A5), as the server returns it. The client filters but never re-sorts.
- Each row link carries `data-return-key={participantId}`; the list route declares `handle.returnFocus` (and
  `backKey: 'participants'`), so a pop back from a participant focuses the row through the shell's one rule
  (app-shell.md). No module variable, no router state.
- Name weight 700, not 600 (`foundations.md`: two weights only).

**A6. List load region** → [`empty-loading-error.md`](../components/empty-loading-error.md)
- One `LoadRegion` with one always-rendered `<p role="status">` for the participants query. The search box, the
  Active / Archived links and the list render together when the data is ready, so nothing shifts under a finger.
- Loading line after 1 s: "Loading participants…" (P). Failed load: see States.
- Empty sentences appear only after the data has arrived (`isSuccess`), never from a failed request.

### B. Participant detail and Add participant

One route component serves both `/manage/participants/new` and `/manage/participants/{id}`, so that the `201` from Add
participant turns the page into the detail page **without remounting it** (see Interactions).

**B1. Back link** → the shared BackLink (app-shell.md component 3a), [`note-identity-header.md`](../components/note-identity-header.md)
- "Participants", with a decorative chevron, in the shell's **before-main bar slot** (not inside `<main>`), so the skip
  link passes it and the error summary is the first thing in `<main>`. A real `href` to the parent list:
  `/manage/participants` for an active participant and for Add participant, `/manage/participants?view=archived` for
  an archived one, so the manager returns to the view the person is in. At least 44 × 44 px. [Convention: GOV.UK back
  link, https://design-system.service.gov.uk/components/back-link/]
- **Pops** (`navigate(-1)`) when the previous history entry is that list (the shell's opened-from record); otherwise
  pushes the `href`. So the back link and browser Back both return focus to the row the manager opened, through the
  shell's `handle.returnFocus` (rows carry no `data-route-focus`).

**B2. Page heading.** `<h1 id="page-heading" tabIndex={-1}>`:
- Add participant: "Add participant" **(V, the action's name)**. Fixed, so focus moves to it on arrival.
- Detail: the participant's full name as stored, "Jane Citizen" (3.9; never re-cased, never truncated). It comes from
  data, so it takes the pending route focus when it mounts (`empty-loading-error.md`). It changes only after a
  successful Save, never while the manager is typing (`text-and-date-inputs.md`).
- Failed load: fallback `<h1>` "Participant" (P, the page name from `app-shell-nav.md`).

**B3. Archived notice and Restore** → [`status-messages.md`](../components/status-messages.md)
- Shown only when the participant is archived, directly under the `<h1>`. Neutral notice tone (blue bar, "i" icon
  `aria-hidden`), body-colour text on the notice tint, no title row, no close button, no role.
- Text (P): "Jane Citizen is archived, so this page is read-only. They are not on Today and no new notes can be written
  for them. Their past notes are kept." Content from design 3.7.
- **Restore** (V): native `<button type="button">`, secondary style, full width on a phone, at least 44 px. This is the
  only Restore for the participant on the page (see Conflicts resolved). An always-rendered, empty
  `<div role="alert">` sits above it for request errors (`primary-actions.md`).
- Static when the page opens (not announced, not focused). **Focused** (`tabIndex={-1}`) only when it appears because
  the manager pressed Archive participant, because the Archive button has gone.
- After a successful Restore the notice is **removed** and the page becomes editable again; because the pressed
  Restore button has gone, focus moves to the `<h1>` (the participant's name). No success banner: design.md gives a
  success message only for Submit (owner simplicity rule). A banner "Jane Citizen restored. They are back on Today."
  (P) is **not built unless the owner approves it** (Open questions).

**B4. Error summary (Details form only)** → [`form-validation.md`](../components/form-validation.md)
- Rendered only after a failed Details Save, for **field errors only**, as the first thing in `<main>`: above the
  `<h1>` (the back link is in the bar slot before `<main>`), the app-wide position (form-validation.md).
  `tabIndex={-1}`, `<h2>` "There is a problem", one link per error with exactly the inline text, no `role="alert"`.
  Focused on every failed attempt. The page title becomes "Error: Grow2Notes – Participant" (or "Error: Grow2Notes –
  Add participant") until the last error clears. [Convention: GOV.UK error summary,
  https://design-system.service.gov.uk/components/error-summary/]
- Request failures that belong to no field (no connection, server error, `412`) never go in the summary: they go in the
  `role="alert"` above Save, and focus stays on Save (B5; form-validation.md).
- Goal forms and the past-day form are one-field forms: they never use this summary.

**B5. Details form** → [`text-and-date-inputs.md`](../components/text-and-date-inputs.md),
[`form-validation.md`](../components/form-validation.md), [`status-messages.md`](../components/status-messages.md),
[`primary-actions.md`](../components/primary-actions.md)
- `<section aria-labelledby>` with `<h2>` "Details" **(V)**, then `<form noValidate onSubmit>` (never React 19
  `<form action>`, which resets fields even when it returns errors: https://github.com/react/react/issues/29034).
  Inputs are controlled, copied **once** from the query data, and never overwritten by a refetch.
- **Given name** **(V, A5)**, **Family name** **(V, A5)**: `<input type="text">`, `autoComplete="off"`,
  `autoCorrect="off"`, `spellCheck={false}`, `autoCapitalize="words"` ([Opinion]), **no `name` attribute**, no
  `maxLength`, no `pattern`. Limit 100 characters each (5.3), checked on Save. Accept every character; trim the ends
  only; never re-case. [Convention: W3C personal names, https://www.w3.org/International/questions/qa-personal-names]
- **Date of birth** **(V, A5)**: `<fieldset role="group">` + `<legend>` "Date of birth", hint "For example, 27 3 1987"
  (P), then three labelled inputs "Day", "Month", "Year" (P, GOV.UK), each `type="text" inputMode="numeric"
  autoComplete="off" spellCheck={false}`, no `pattern`, no `maxLength`, **no auto-advance**. Widths 2.75em (at least
  44 px), 2.75em, 4.5em. Accepts leading zeros and month names ("jan", "sept", "March"). One pure, unit-tested
  `parseDob(parts, me.today)`. [Convention: GOV.UK date input, https://design-system.service.gov.uk/components/date-input/;
  Research: GOV.UK month-name finding, same page; Research: NN/g date input, https://www.nngroup.com/articles/date-input/]
- `autoComplete="off"` and no `name` attributes: this is someone else's data, so SC 1.3.5 does not apply, and the
  browser is asked not to keep participant names and birthdays on a manager's own phone (D21, D22). [Standard:
  https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html,
  https://developer.mozilla.org/en-US/docs/Web/Security/Practical_implementation_guides/Turning_off_form_autocompletion]
- An always-rendered, empty `<div role="alert">` directly above Save, for request failures that belong to no field
  (no connection, server error, `412`). Focus stays on Save, so pressing it again retries (form-validation.md,
  primary-actions.md).
- **Save** **(V)**: primary `type="submit"`, never `disabled`; busy (`aria-disabled`, "Saving…" (P) after 400 ms).
  The mutation uses `retry: 0`: `POST /api/admin/participants` has no idempotency key (6.6), so a silent retry after
  a lost response could create the participant twice.
  Next to it, an always-rendered `<p role="status">` (`SaveStatus`): empty, then "Saved 4:12 pm" after a `200` or
  `201`, cleared on the next edit. The time is wrapped in `<time dateTime>` and is never read from the device clock
  (see Open questions).
- Validation on Save only, in page order; messages under States.

**B6. Goals** → [`list-editor.md`](../components/list-editor.md) (`ListEditor kind="goal"`)
- `<section aria-labelledby>` with `<h2>` "Goals" **(V)**. Rendered only once the participant exists (goals need a
  participant ID).
- Help text **(V)**: "Changes apply to notes started from now on. Notes already started or submitted keep the wording
  they were written with." Linked by `aria-describedby` to the New goal and Edit goal textareas.
- Active goals: numbered native `<ol>` (each `<li>` stays `display: list-item`). Each row: the goal text, an
  always-rendered empty `role="alert"` slot, then **Move up**, **Move down**, **Edit**, **Archive** **(V)**, in that
  order, all secondary `<button type="button">`s with words (decorative arrows `aria-hidden`). Each button carries the
  goal text as visually hidden text after its label ("Move up, Makes own breakfast"), never `aria-label`.
  [Standard: SC 2.5.3; Research: Roselli, https://adrianroselli.com/2019/11/aria-label-does-not-translate.html]
- **Buttons only for reordering**: no drag, no handle, no reorder mode (meets SC 2.5.7 outright). [Standard:
  https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html, SCR27
  https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR27]
- At the ends, Move up (row 1) and Move down (last row) **stay in place**, `aria-disabled="true"`, dashed border; never
  `disabled`, never hidden. Pressing one announces "Already at the top." / "Already at the bottom." (P).
- **Edit** turns the row into a one-field form in place: label "Edit goal" (P), `<textarea rows={3}>` with the current
  text and the caret at the end, the shared character count, **Save** and **Cancel**. Opened with `flushSync` then
  `focus()` inside the tap, so phones open the keyboard (unverified on current iOS: test).
- **Add**: label "New goal" (P), `<textarea rows={3}>`, character count "You can enter up to 200 characters"
  (GOV.UK; microcopy.md), **Add goal** **(V)**, primary, full width on a phone. No `maxLength`: 200 characters (A6) is
  checked on Add. [Convention + Research (n = 17): GOV.UK character count,
  https://design-system.service.gov.uk/components/character-count/]
- Archived goals: `<details>` (uncontrolled, closed on load, default marker kept) with a plain-text `<summary>`
  "Archived goals" (P), a `<ul>` newest archived first, each with **Restore** **(V)**. Left out entirely when no goal
  is archived. [Convention: O'Hara on details/summary, https://www.scottohara.me/blog/2022/09/12/details-summary.html]
- Every write (move, add, edit, archive, restore) runs in one TanStack mutation scope `goals:<participantId>`, one at
  a time, and re-reads the list before the next starts. Nothing is optimistic. Every goal write uses **`retry: 0`**
  (the app-wide mutation default, form-validation.md): `POST …/goals` has no idempotency key, so a silent retry after
  a lost response could add the goal twice. [Convention: TanStack scope,
  https://tanstack.com/query/v5/docs/framework/react/guides/mutations; Opinion grounded in invariant I5]
- **Row layout:** the wrapping flex row from common-items.md and `list-editor.md` (text `flex: 1 1 16rem`, the
  actions at their natural width), never a container query.
- **Read-only variant** (archived participant): numbered goal texts only; no help text, no buttons, no add form;
  archived goals as text with no Restore.

**B7. Actions** → [`primary-actions.md`](../components/primary-actions.md),
[`confirm-dialog.md`](../components/confirm-dialog.md), [`date-navigation.md`](../components/date-navigation.md),
[`export-form.md`](../components/export-form.md)
- `<section aria-labelledby>` with `<h2>` "Actions" **(V)**.
- Group 1, in design order, each a `<Link>` with the secondary button class, at least 48 px:
  - **Past notes** **(V)** → the participant's history (4.4), `/participants/{id}/notes`.
  - **Write past-day note** **(V)** → `/manage/participants/{id}/past-day-note`. **Not rendered** for an archived
    participant (3.7: "No new notes, including past-day notes"; primary-actions.md hides actions that are never
    available in this state).
  - **Export record** **(V)** → the export screen (4.12, owned by export-form.md). Shown for archived participants
    too (3.7: "exports are unchanged").
- A 1 px divider, then group 2: **Archive participant** **(V)**, a secondary `<button type="button">`, **not** the red
  warning style (archiving is reversible), with an always-rendered empty `<div role="alert">` above it. **No
  confirmation dialog**: design 4.8 specifies none, and the read-only notice with Restore appears at once and works
  as an immediate, permanent undo. [Research: warnings lose effect from the second exposure, Anderson et al. CHI 2015,
  https://scholarsarchive.byu.edu/facpub/9306/; Convention: confirm-dialog.md] **Not rendered** while archived
  (Restore is in the notice).

**B8. Page status region.** One visually hidden `<p role="status">` (the shared `PageStatus`, app-shell.md component
3), rendered with the page from the first render and never conditionally mounted. It carries the goal list's
announcements and the busy labels of the Save, goal, Archive and Restore buttons: the shared Button finds it through
context (`list-editor.md`, `primary-actions.md`). It is owned by this page, not the app shell (`status-messages.md`:
no global live region). This is the pattern every screen now uses. [Standard: SC 4.1.3,
https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html; Research: O'Hara, "Are we live?",
https://www.scottohara.me/blog/2022/02/05/are-we-live.html]

**B9. Detail load region** → [`empty-loading-error.md`](../components/empty-loading-error.md)
- One `LoadRegion` over two queries, shown together or not at all: the participant (from the admin participants list,
  the only endpoint that returns the date of birth) and the goals
  (`GET /api/admin/participants/{id}/goals?includeArchived=true`). Loading line after 1 s: "Loading participant…" (P).
- The participant ID not in the list, or a `404`/`403`: the whole-page "Page not found".

### C. Write past-day note

**C1. Back link** → the shared BackLink (app-shell.md component 3a) in the shell's bar slot: names the screen it was
opened from, "Participant" (from participant detail) or "Past notes" (from 4.4), from the shell's in-memory
opened-from record; falls back to "Participant". Pops when the previous entry is the destination. 44 px target.

**C2. Heading.** `<h1 id="page-heading" tabIndex={-1}>` holding a caption and the title:
`<span class="caption">Jane Citizen</span><span class="visually-hidden">, </span>Write past-day note`. The caption is the
participant's full name (3.9), shown above the title in secondary weight. It is **inside** the `<h1>` so that the shell's
heading focus reads the name too (the same pattern note-identity-header.md uses for "Editing submitted note"). It holds
data, so it takes the pending route focus when it mounts. Failed load: fallback `<h1>` "Write past-day note".
Data: `GET /api/participants/{participantId}` (`{id, givenName, familyName, status}`, design 6.3).

**C3. Note date** → [`date-navigation.md`](../components/date-navigation.md),
[`text-and-date-inputs.md`](../components/text-and-date-inputs.md), [`form-validation.md`](../components/form-validation.md)
- A one-field `<form noValidate onSubmit>`. Label "Note date" (P), hint "A date before today" (P), one native
  `<input type="date" max={yesterday}>`, where `yesterday` is `me.today` minus one day using UTC string arithmetic in
  `dates.ts` (never the device clock). No `min`, **no default value**, `autoComplete="off"`, text at 18 px, at least
  48 px tall, `max-inline-size: 12em`. [Convention: GOV.UK dates pattern, a calendar suits a chosen recent date,
  https://design-system.service.gov.uk/patterns/dates/; Research: NN/g, https://www.nngroup.com/articles/date-input/]
- `max` is only a hint for the picker: iOS ignores it (WebKit bug 225639, open) and desktop typing gets past it, so
  the date is checked in code on Continue and the server stays the gate (`422 note.future_date`). [Research:
  https://bugs.webkit.org/show_bug.cgi?id=225639]
- **Continue** (P): primary `type="submit"`. It only navigates; it creates nothing (3.3: a draft starts at the first
  change).

**C4. Archived variant.** For an archived participant the page renders no field and no Continue, only the notice (P):
"Jane Citizen is archived, so no new notes can be written for them." in the notice style, with no Restore (Restore lives
on the participant's page). Reached only through an old link or Back, because the link is hidden for archived
participants.

### Shared foundations and formats

- Tokens, type sizes, colours, focus ring (3 px near-black `outline`, 2 px offset, on every focusable element) and
  forced-colours rules from [`foundations.md`](../components/foundations.md). Body text 18 px; nothing under 16 px.
- Words, glossary and date formats from [`microcopy.md`](../components/microcopy.md): "participant" (never client),
  "archive / Restore" (never delete or remove), "Past notes" (never history). Date of birth read-only as `datePlain`
  ("7 March 1987", non-breaking space between day and month). Times as `time` ("4:12 pm", non-breaking space before
  "pm"; "midday" / "midnight" at exactly 12:00). `<html lang="en-AU">`.
- Empty, loading and error mechanics from [`empty-loading-error.md`](../components/empty-loading-error.md); their
  wording from microcopy.md (see Conflicts resolved).

---

## States

Every string is exact. Placeholders: `Jane Citizen` = the participant's full name; `xyz` = the trimmed search text as
typed; `4:12 pm` = the save time in Melbourne.

### Participants list

| State | What shows | Copy |
|---|---|---|
| **Waiting** (under 1 s) | `<h1>`, Add participant. Nothing else. | — |
| **Loading** (1 s and over) | `<h1>`, Add participant, the status line | `Loading participants…` (P) |
| **Load failed** | `<h1>`, Add participant, the message in the status line, **Try again** (secondary button outside the status line, never `disabled`, "Loading…" after 400 ms while it runs) | `The participant list did not load: no connection. Try again.` (P) · server error: `The participant list did not load: something went wrong. Try again.` (P) |
| **Still failing** (Try again failed) | Same; focus stays on Try again | `The participant list still did not load: no connection. Try again.` (P) (or `…: something went wrong. Try again.`) |
| **Default, Active** | Search, Active current, active participants A–Z by family name | — |
| **Default, Archived** | Search, Archived current, archived participants | — |
| **No participants at all** | No search box. The Active / Archived links stay. | `No participants yet.` (V) |
| **Active empty, archived exist** | Search box stays | `No active participants.` (P) |
| **Archived empty** | Search box stays | `No archived participants.` (P) |
| **No search match** | Text stays in the field, Clear stays, the sentence replaces the list (plain body text, not red) | `No participant matches ‘xyz’.` (N: typographic quotes and a full stop, microcopy.md) |
| **Background refresh failed** | Nothing changes | — |
| **Worker opens the URL** | Whole-page not found | `<h1>` `Page not found` · `If you typed or pasted the web address, check it is correct.` · link `Go to Today` (P, empty-loading-error.md) |

### Add participant

| State | What shows | Copy |
|---|---|---|
| **Default** | Empty fields, no placeholders, the date hint, Save | Hint `For example, 27 3 1987` (P) |
| **Saving** | Fields stay editable; Save busy after 400 ms | `Saving…` (P) |
| **Validation failed** | Summary above the `<h1>`, inline messages above the inputs (table below), title `Error: Grow2Notes – Add participant` | `There is a problem` |
| **Network or server failure** | Alert above Save (not the summary); typed values kept; focus stays on Save | `Not saved: no connection. Try again.` · `Not saved: something went wrong. Try again.` (P, microcopy.md) |
| **Saved (`201`)** | Becomes the detail page: `<h1>` is the name, status next to Save, Goals (empty) and Actions appear | `Saved 4:12 pm` (P, status-messages.md) · `No goals yet. Notes will show an empty Goals section.` (see Goals) |

**Details validation messages** (same on Add participant and detail; checked on Save in page order; the first failing
check per field is shown; inline and summary text are identical; all (P) unless marked, following GOV.UK templates via
form-validation.md and text-and-date-inputs.md):

| Field | Check (trimmed) | Message | Parts marked invalid |
|---|---|---|---|
| Given name | not blank | `Enter the given name` | — |
| Given name | 100 characters or fewer | `Given name must be 100 characters or less` | — |
| Family name | not blank | `Enter the family name` | — |
| Family name | 100 characters or fewer | `Family name must be 100 characters or less` | — |
| Date of birth | not all three empty | `Enter the date of birth` | Day, Month, Year |
| Date of birth | no part empty | `Date of birth must include a day` (or `a month`, `a year`, `a day and month`, `a day and year`, `a month and year`) | the empty parts |
| Date of birth | year is digits but not 4 of them | `Year must include 4 numbers` | Year |
| Date of birth | a real date | `Date of birth must be a real date` | the wrong part (Day for 31 April, Month for 13) |
| Date of birth | before `me.today` | `Date of birth must be in the past` | Day, Month, Year |

### Participant detail

| State | What shows | Copy |
|---|---|---|
| **Waiting** (under 1 s) | Back link only (no `<h1>` yet: it is data) | — |
| **Loading** (1 s and over) | Back link, status line | `Loading participant…` (P) |
| **Load failed** | Back link, fallback `<h1>`, message, **Try again** | `<h1>` `Participant` · `This participant did not load: no connection. Try again.` (P) · server: `This participant did not load: something went wrong. Try again.` (P) |
| **Still failing** | Same; focus stays on Try again | `This participant still did not load: no connection. Try again.` (P) |
| **Not found** (unknown ID, `404`, `403`) | Whole-page not found (see list table) | `Page not found` |
| **Active, default** | Details form, Goals, Actions | Goals help text (V, above) |
| **No goals** | The sentence in place of the `<ol>`; the add form is still there; Archived goals only if any | `No goals yet. Notes will show an empty Goals section.` (second sentence V; first sentence P, the design's own name for the state) |
| **No archived goals** | The Archived goals section is left out entirely | — |
| **Details saving** | Fields editable; Save busy | `Saving…` (P) |
| **Details saved** | Status next to Save; focus stays on Save; `<h1>` shows the saved name | `Saved 4:12 pm` (P) |
| **Details validation failed** | Summary above the `<h1>`, inline messages (table above), title prefix | `There is a problem` · title `Error: Grow2Notes – Participant` |
| **Details stale (`412`)** | Alert above Save; typed values kept; fresh version fetched; focus stays on Save | `Someone else changed these details while you were editing. Check the details, then save again.` (P, form-validation.md) |
| **Details network or server failure** | Alert above Save; typed values kept; focus stays on Save | `Not saved: no connection. Try again.` · `Not saved: something went wrong. Try again.` (P) |
| **Goal states** | As in list-editor.md: busy, unavailable, editing, field error, request error, edit conflict, list conflict | See Goals copy below |
| **Archiving** | Archive participant busy after 400 ms (`aria-disabled`) | `Archiving…` (P) |
| **Archived** (on load, or after Archive) | Notice with Restore under the `<h1>`; Details as `<dl>`; goals read-only; Actions: Past notes, Export record | `Jane Citizen is archived, so this page is read-only. They are not on Today and no new notes can be written for them. Their past notes are kept.` (P) |
| **Archive failed** | Message in the alert slot above the button; button back to normal; focus stays | `Not archived: no connection. Try again.` · `Not archived: something went wrong. Try again.` (P) |
| **Archive or Restore conflict (`412`)** | Page re-reads; if the status already changed, the page simply shows the new state; otherwise the message in the alert slot | `Someone else changed this participant just now. Check the page, then try again.` (P) |
| **Restoring** | Restore busy after 400 ms | `Restoring…` (P) |
| **Restored** | The notice is removed; focus moves to the `<h1>`; page editable again. No banner (owner question) | — |
| **Restore failed** | Message in the alert slot above Restore; the notice stays | `Not restored: no connection. Try again.` · `Not restored: something went wrong. Try again.` (P) |

**Goals copy** (from list-editor.md and microcopy.md; (P) unless marked):

| Where | Text |
|---|---|
| Row buttons | `Move up` · `Move down` · `Edit` · `Archive` (V), each followed by hidden `, [goal text]` |
| Archived row button | `Restore` (V) + hidden `, [goal text]` |
| Add | label `New goal` · count `You can enter up to 200 characters` / `You have 142 characters remaining` / `You have 3 characters too many` · button `Add goal` (V) |
| Edit | label `Edit goal` · buttons `Save` · `Cancel` |
| Archived section | `Archived goals` |
| Busy labels (after 400 ms) | `Moving…` · `Saving…` · `Adding…` · `Archiving…` · `Restoring…` |
| Field errors (on Add or Save) | `Enter the goal` · `Goal must be 200 characters or less` |
| Announcements (page status region; one phrase, no full stop, microcopy.md) | `Moved up to number 2 of 5` · `Moved down to number 4 of 5` · `Already at the top` · `Already at the bottom` · `Goal saved` · `Goal added to the end of the list` · `Goal archived` · `Goal restored to the end of the list` |
| Request error (row or form alert slot; one verb per action, as common-items.md) | `Not moved: no connection. Try again.` · `Not added: …` · `Not saved: …` · `Not archived: …` · `Not restored: …`, each with the cause `no connection` or `something went wrong` |
| Edit conflict (`412` on Save, row still active) | `Someone else changed this goal while you were editing. It now says: [their text]. Save to replace it with your wording, or Cancel to keep it.` |
| List conflict (`412` on Archive or Restore, `422 config.order_mismatch`) | `Someone else changed this list just now. Check it, then try again.` (focused, above the list) |

Do not show design 3.7's "Rewording is for typos and clarifications…" on screen; it is manager training material
(`list-editor.md`, [Opinion]).

### Write past-day note

| State | What shows | Copy |
|---|---|---|
| **Waiting / Loading** | Back link; after 1 s the status line | `Loading participant…` (P) |
| **Load failed** | Fallback `<h1>`, message, Try again | `<h1>` `Write past-day note` · `This participant did not load: no connection. Try again.` (P) |
| **Default** | Caption + `<h1>`, label, hint, empty date field, Continue | `Note date` · `A date before today` · `Continue` (P) |
| **Empty or incomplete on Continue** | Inline error above the field, `aria-invalid`, focus on the field, 4 px bar | `Enter the note date` (P) |
| **Today or later on Continue** (iOS lets a future date be picked) | Same | `Note date must be in the past` (P) |
| **Archived participant** | No field, no Continue; the notice | `Jane Citizen is archived, so no new notes can be written for them.` (P) |
| **Valid** | Navigates to the note route; the note form shows its own loading and banner | — |

---

## Interactions and focus

### Arriving

| Arrival | Focus | Scroll |
|---|---|---|
| Participants list, by the nav or a link (push) | `<h1>` "Participants" (shell rule) | Top |
| Participants list, back from a participant (browser Back or the "Participants" back link, which pops) | The row link of the participant opened, if it is still in the shown view, through the shell's `handle.returnFocus` (rows carry `data-return-key={participantId}`; the shell remembers the key in memory only, D22). Otherwise the `<h1>`. Rows never carry `data-route-focus`. | Pop: restored from the shell's in-memory map. |
| Participant detail or Add participant | `<h1>` (detail: when it mounts with the data) | Top |
| Write past-day note | `<h1>` with the caption (when it mounts) | Top |

### Participants list

| Action | Result | Focus | Announced |
|---|---|---|---|
| Type in Find a participant | The shown view filters on every keystroke; the order never changes | Stays in the field | Nothing while matches exist. On no match, after a 500 ms pause: `No participant matches ‘xyz’.` (polite, the search's own hidden region; 500 ms is [Opinion], test with NVDA and VoiceOver) |
| Clear | Field emptied, full view back; phone keyboard stays open (`onMouseDown` `preventDefault`) | Back in the field | Nothing (the field's label and empty value are read) |
| Enter or Escape in the field | Nothing | Stays | — |
| Active / Archived | Pushed navigation; view swaps instantly; search text kept and applied | Stays on the activated link | Nothing (the user asked for new content: not a status message) |
| Add participant | Navigates to `/manage/participants/new` | `<h1>` "Add participant" | Via focus |
| A row | Navigates to that participant's detail | `<h1>` (the name) | Via focus |
| Leave the screen | Search text is discarded | — | — |

### Add participant and participant detail

| Action | While running | On success | Focus | Announced |
|---|---|---|---|---|
| **Save (Add participant)** | Save busy (`aria-disabled`, "Saving…" after 400 ms); fields editable, never cleared | `POST /api/admin/participants` → `201`. Then `navigate('/manage/participants/{newId}', { replace: true, state: { inScreen: true } })`: Back returns to the list, not to an empty form; the shell does not move focus or scroll; the **same component instance** stays mounted. The `<h1>` becomes the name, the title becomes "Grow2Notes – Participant", Goals and Actions appear below. Invalidate the participants and Today queries. | **Stays on Save.** The next Tab goes into Goals (the New goal field comes after the help text and the empty sentence), which serves the M1 five-goals test. | `Saved 4:12 pm` (polite, SaveStatus) |
| **Save (detail)** | As above | `PUT /api/admin/participants/{id}` with `If-Match`, then re-read the participants list (fresh ETag, `<h1>`, list screen). Invalidate Today (names show there). | Stays on Save | `Saved 4:12 pm` (polite). Cleared on the next edit, so a later save is announced again. |
| **Save with errors** (client or `422`) | — | Nothing sent (client) or nothing changed (server) | The error summary (every attempt) | Through focus: "There is a problem", then the list |
| **Error summary link** | — | Scroll the field's `<label>` or `<legend>` into view, then `focus({ preventScroll: true })` the input; no hash, no history entry | The field (date: the first part in error) | Label, "invalid", hint, "Error: …" |
| **Fix a field in error** | — | Re-checked on every input; message, `aria-invalid` and summary item removed silently when it passes | Unchanged | Nothing |
| **Goal: Move up / Move down** | Busy; the list does not change until the server confirms | `PUT …/goals/order` with the full active ID list read **when the write runs**, then re-read | Stays on the same button of the moved goal (re-focused explicitly in `useLayoutEffect`: Move down on row 1 is the case where React moves the focused node) | `Moved up to number 2 of 5` |
| **Goal: unavailable end button** | No request | — | Stays | `Already at the top` / `Already at the bottom` |
| **Goal: Edit** | — | Row becomes the edit form | The textarea, caret at the end, inside the tap | The label, value, help text and count are read |
| **Goal: Save** | Save busy; textarea `readOnly` | Text trimmed, line breaks become spaces. Unchanged text closes like Cancel and sends nothing. Otherwise `PUT /api/admin/goals/{id}` with `If-Match`, then re-read. | The row's Edit | `Goal saved` |
| **Goal: Cancel** | — | Row back to normal; typed text discarded | The row's Edit | — |
| **Add goal** | Add busy | `POST …/goals` (adds at the end; `retry: 0`), re-read; field cleared | Stays in the New goal field, ready for the next goal | `Goal added to the end of the list` |
| **Goal: Archive** | Busy | `POST /api/admin/goals/{id}/archive` with `If-Match`, re-read; Archived goals appears if hidden, **opens**, lists it first | That goal's Restore | `Goal archived` |
| **Goal: Restore** | Busy | `POST …/restore`, re-read; goal goes to the end of the active list (A2); empty Archived goals section disappears | That goal's Archive in the active list | `Goal restored to the end of the list` |
| **Past notes / Write past-day note / Export record** | — | Ordinary navigation (push) | The next screen's `<h1>` | Via focus |
| **Archive participant** | Busy; nothing else changes yet | `POST /api/admin/participants/{id}/archive` with `If-Match` → `204`, then re-read the participant. The page turns read-only: notice under the `<h1>`, Details as `<dl>`, goal controls gone, Write past-day note and Archive participant gone. Invalidate the participants list and Today. | **The archived notice** (`tabIndex={-1}`), which scrolls the page up to it; Restore is the next Tab stop | Through focus: the notice text |
| **Restore** (participant) | Restore busy | `POST …/restore` with `If-Match` → `204`, re-read. The notice is removed; the form, goal controls and actions return. Invalidate the participants list and Today. | **The `<h1>`** (the pressed button has gone) | Through focus: "Jane Citizen, heading level 1"; the page is editable again |
| **Back link "Participants"** | — | Pops to the list when it is the previous entry, otherwise pushes it (the Archived view for an archived participant) | The row just opened (on a pop), else the `<h1>` | Via focus |

Notes on these interactions:
- **One route component for new and existing participants.** React Router would remount a different route element
  and drop focus to `<body>` after the `201`. Match both paths to the same element, never key it by the URL ID, and
  key the Details form state by a form-session ID that the `201` does not change. Cover it with a test.
  [Opinion, from how React reconciles elements]
- **Archive while something is unsaved.** Archive does not send the Details form or an open goal edit. Typed but
  unsaved values in either are dropped when the page turns read-only. design.md asks for no unsaved-changes warning
  here (only on Guide prompts, 4.10), and Archive is a deliberate act at the bottom of the page. [Opinion; see Open
  questions]
- **Someone else archived the participant while this page was open.** No polling (6.8). The next write on this page
  gets `412`; the page re-reads and shows the archived state.
- **No confirmation dialog** for Archive participant, Restore, or any goal action (`confirm-dialog.md`).
- **Nothing is optimistic.** Every result on screen is the server's answer (invariant I5).

### Write past-day note

| Action | Result | Focus | Announced |
|---|---|---|---|
| Pick or type a date | Nothing is checked yet; no change event acts on half-typed dates | Stays | Native field output only |
| **Continue** with an empty, incomplete, today or future date | Inline error, `aria-invalid`, 4 px bar | The field | Label, "invalid", "Error: Enter the note date" (or "…must be in the past") |
| Fix the value | Error cleared the moment the value is valid (re-checked on each change once in error) | Stays | Nothing |
| **Continue** with a valid past date | `navigate('/participants/{id}/notes/2026-09-28')` (push). That route fetches and decides: the existing note (read view, own draft, or a read-only draft with Discard) or a new past-day form with the past-day banner (design 3.8, 4.3). Nothing is created until the first change. | The note screen's heading | Via focus |
| Back from the note form | Returns to an empty past-day page | `<h1>` | Via focus |

### Live regions on these views

| Region | Where | Always rendered? | What it says |
|---|---|---|---|
| LoadRegion `<p role="status">` | List; detail; past-day page | Yes, from the first render | Loading line, then the load-failed text |
| Search's hidden `<p role="status">` | List, inside `<search>` | Yes | The no-match sentence after a 500 ms pause |
| SaveStatus `<p role="status">` | Next to Details Save | Yes, empty, never `display: none` | `Saved 4:12 pm` |
| Page status region (visually hidden `<p role="status">`, `PageStatus`, B8) | List, detail, past-day page | Yes | Goal announcements; busy labels (the shared Button writes them here) |
| Row and form `role="alert"` slots | Above Details Save, each goal row, the add form, above Archive participant, inside the notice above Restore | Yes, empty | Request failures only (no connection, server error, `412`) |

No `role="alert"` on notices, the error summary or load messages. No live region on the archived notice: it is
announced by taking focus (SC 4.1.3 excludes content that takes focus). The page status region (B8) is on the
list, the detail and the past-day page alike.

---

## Accessibility checklist

**Headings** (one `<h1>` per view; no heading inside a banner or a `<summary>`):
- Participants list: `h1` Participants.
- Add participant: `h2` There is a problem (only after a failed Save, first in `<main>`, above the `<h1>`, as GOV.UK
  places it) > `h1` Add participant > `h2` Details.
- Detail: `h2` There is a problem (only after a failed Save, above the `<h1>`) > `h1` Jane Citizen > `h2` Details >
  `h2` Goals > `h2` Actions. "Archived goals" is plain `<summary>` text, not a heading.
- Write past-day note: `h1` "Jane Citizen, Write past-day note" (caption inside).

**Landmarks:** the shell's header and main nav; `<main id="main-content">`; on the list, a `<search>` landmark and a
second `<nav>` named by the `<h1>` ("Participants, navigation") for Active / Archived. No other landmarks; sections use
`<section aria-labelledby>` only to group Details, Goals and Actions under their headings.

**Labels and names:**
- Every input has a visible `<label>`; date of birth uses `<fieldset>` + `<legend>` with `role="group"` and
  `aria-describedby` (hint, then error). No placeholders anywhere on these views.
- Repeated buttons start with their visible words, then hidden context: "Move up, Makes own breakfast",
  "Clear search". No `aria-label` replaces visible words (SC 2.5.3; dictation users say what they see).
- Links say where they go: Past notes, Write past-day note, Export record, Participants (back), the participant's name.
- Errors: inline `<p id="…-error">` with a hidden "Error: " prefix, referenced by `aria-describedby`; `aria-invalid`
  only after an attempt. No `aria-errormessage`.

**Keyboard** (Tab order is the visual order at every width; no custom shortcuts; Enter and Escape are never
intercepted in text fields):
- List: (shell skip link and nav) → Add participant → Find a participant → Clear (when shown) → Active → Archived →
  rows.
- Detail, active: Participants (back) → [summary links, after a failed Save] → Given name → Family name → Day → Month
  → Year → Save → for each goal row: Move up, Move down, Edit, Archive → New goal → Add goal → Archived goals summary
  → each Restore (when open) → Past notes → Write past-day note → Export record → Archive participant.
- Detail, archived: Participants (back) → Restore → Archived goals summary (when any) → Past notes → Export record.
- Write past-day note: back link → date field (the browser's own keys) → Continue. Enter in the field submits.
- Focus is never dropped to `<body>`: no `disabled` on anything that can hold focus; replaced buttons hand focus to the
  notice, the banner, the moved goal's button, the goal's Restore or Archive, or Edit.

**Screen reader** (expected; verify with NVDA + Chrome and VoiceOver + iOS Safari in M1, and TalkBack + Chrome):
- Detail arrival: "Jane Citizen, heading level 1".
- Day field: "Date of birth, grouping, For example, 27 3 1987, Day, edit, 7".
- Move up on row 2: "Move up, Catches the bus to day program…, button"; after pressing, "Moved up to number 1 of 3"
- Move up on row 1: "…button, dimmed" (VoiceOver) or "unavailable" (NVDA); pressing it: "Already at the top"
- Archive participant: focus lands on the notice and its text is read; next Tab: "Restore, button".
- List switch: "Participants, navigation, list, 2 items, Active, current page, link".

**Visual:** text ≥ 18 px body, ≥ 16 px small; every text colour on these views ≥ 7:1 (the placeholder grey is not
used here); input borders and the focus ring ≥ 3:1; meaning never by colour alone (unavailable = dashed border; current view = bold + bar;
errors = words + bar + border; notice and banner = words); nothing sticky; no animation; works at 320 px and 200% text
with no sideways scrolling; Windows forced colours keep the bars (borders), the focus ring (`Highlight`) and the
unavailable state (`GrayText`).

**WCAG 2.2 AA criteria these views meet:**
1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.3.4 Orientation · 1.3.5 Identify Input Purpose (not
applicable: data about someone else; `off` is compliant) · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) · 1.4.4
Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast · 1.4.12 Text Spacing · 1.4.13 Content on Hover or Focus (none)
· 2.1.1 Keyboard · 2.1.2 No Keyboard Trap · 2.1.4 Character Key Shortcuts (none) · 2.2.1 Timing Adjustable (nothing
timed) · 2.2.2 Pause, Stop, Hide (no motion) · 2.4.1 Bypass Blocks (shell skip link) · 2.4.2 Page Titled (fixed,
nameless titles) · 2.4.3 Focus Order · 2.4.4 Link Purpose · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11
Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.7 Dragging Movements (no dragging) · 2.5.8 Target Size
(Minimum) (44 px and up) · 3.1.1 Language of Page · 3.2.1 On Focus · 3.2.2 On Input (filtering and date picking change
content, not context) · 3.2.3 Consistent Navigation · 3.2.4 Consistent Identification · 3.3.1 Error Identification ·
3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 3.3.7 Redundant Entry (typed values kept after every error) ·
4.1.2 Name, Role, Value · 4.1.3 Status Messages.

---

## Acceptance criteria

**Access and privacy**
- [ ] A worker opening any route on these views sees "Page not found" with the title "Grow2Notes – Page not found";
      every configuration endpoint returns `403` to a worker (automated test, M1).
- [ ] No participant name, date of birth or search text appears in any URL, page title, `history.state`, query key,
      `localStorage`, `sessionStorage`, IndexedDB, log or telemetry (Playwright check of `location`, `document.title`,
      `history.state` and storage after each flow).
- [ ] Page titles are exactly "Grow2Notes – Participants", "Grow2Notes – Add participant", "Grow2Notes – Participant"
      and "Grow2Notes – Write past-day note", with "Error: " in front only while a Details error is shown.
- [ ] Every name and date input on these views has `autocomplete="off"`, no `name` attribute and no `maxlength`.
- [ ] The parent company's name appears nowhere on these views (microcopy.md copy test, D42).

**Participants list**
- [ ] Rows are sorted by family name, then given name, in both views; filtering never re-orders them.
- [ ] "Add participant" is in the same place with or without search text, in both views, and in the empty state.
- [ ] Typing "cit jane", "Jane.", "zoe" (for Zoë) and "obr" (for O'Brien) each finds the right person; typing only
      spaces or "." shows the whole view and no message (`matchesName` unit tests).
- [ ] Switching Active / Archived keeps the search text, changes the URL to `?view=archived` and back, works with
      Back and reload, and leaves focus on the activated link; `?view=other` shows Active and the URL is corrected.
- [ ] Exactly one `aria-current="page"` inside the Active / Archived nav.
- [ ] With no participants at all, only "No participants yet." and Add participant show (no search box).
- [ ] The no-match sentence reads `No participant matches ‘xyz’.` and is announced once after a pause.
- [ ] Clear returns focus to the field and the phone keyboard stays open.
- [ ] Back from a participant focuses that participant's row.
- [ ] Nothing shows for loads under 1 s; "Loading participants…" shows at 1,000 ms (fake timers).

**Add participant and Details**
- [ ] No error shows before Save is pressed; errors clear the moment the field is fixed.
- [ ] A failed Save focuses the summary every time, the summary links match the inline messages exactly, and a link
      focuses its field without changing the URL.
- [ ] `parseDob` unit tests pass: "7 / 3 / 1987", "07 / 03 / 1987", "7 / mar / 1987", "7 / Sept. / 1987" are valid;
      "31 / 4 / 1987" marks Day; "13" as month marks Month; "87" gives "Year must include 4 numbers"; 29 February 2023
      is not real and 29 February 2024 is; `me.today` itself gives "…must be in the past".
- [ ] Names are stored and shown exactly as typed after trimming the ends ("de Silva", "O'Brien", "Nguyễn",
      "Smith-Jones"); nothing is re-cased.
- [ ] After `201`: the URL is replaced (Back goes to the list), focus is still on Save, "Saved 4:12 pm" is announced,
      the `<h1>` shows the name, and the next Tab reaches the Goals section.
- [ ] A refetch never overwrites typed values; a `412` keeps them and shows "Someone else changed…" in the alert
      above Save, with focus still on Save. A network or server failure does the same with "Not saved: …".
- [ ] A Details Save or any goal write whose response is lost is sent exactly once (no automatic retry).
- [ ] "Saved 4:12 pm" uses a server time (not the device clock) in Melbourne time with a non-breaking space.

**Goals**
- [ ] A manager can add five goals on a phone without help: focus stays in the New goal field after each Add (M1).
- [ ] After Move down on row 1, `document.activeElement` is the moved goal's Move down (Vitest + Testing Library).
- [ ] Move up on row 1 and Move down on the last row are focusable, `aria-disabled`, dashed, send nothing, and
      announce "Already at the top" / "Already at the bottom".
- [ ] Goal rows use the wrapping flex layout: at 1280 px the four buttons sit on the text's line when they fit; at
      200% text they drop under the text; the CSS has no `@container` rule.
- [ ] Archive opens Archived goals and focuses that goal's Restore; Restore puts it at the end and focuses its Archive.
- [ ] Save and Cancel return focus to the row's Edit; Edit focuses the textarea with the caret at the end.
- [ ] Two quick presses on different rows run one after the other (one mutation scope) and never send a stale order.
- [ ] Typing past 200 characters is allowed; the count says "too many"; Add or Save then shows the error.
- [ ] No drag handle, no `draggable`, no `display: none` on the end buttons, no `aria-label` on row buttons.

**Archive and Restore**
- [ ] Archive participant shows no dialog; after `204` the page is read-only, the notice is focused, Restore is the
      next Tab stop, Write past-day note and Archive participant are gone, and the person is gone from Today.
- [ ] Restore removes the notice and focuses the `<h1>`; the page is editable again; the person is back on Today and
      in the Active view. No success banner appears.
- [ ] An archived participant shows Details as text ("7 March 1987"), goals as numbered text with no buttons, and the
      actions Past notes and Export record only.

**Write past-day note**
- [ ] The field starts empty, `max` is yesterday from `me.today` (2026-09-30 on 1 October 2026), and Continue with an
      empty, today or future value shows the error and focuses the field (including a future date picked on iOS).
- [ ] Continue with a past date navigates to `/participants/{id}/notes/{date}` and creates nothing.
- [ ] For an archived participant the page shows the archived sentence and no field.
- [ ] The `<h1>` is read as "Jane Citizen, Write past-day note" when focused on arrival.

**Cross-cutting**
- [ ] axe (Playwright) passes on every state above; no horizontal scroll at 320 px with 200% text.
- [ ] Every interactive target is at least 44 × 44 px; buttons and the search field at least 48 px; list rows 56 px.
- [ ] Every live region is in the DOM before text is written into it; none uses `display: none`.
- [ ] Focus ring visible on every focusable element, and in Windows forced colours.
- [ ] Manual pass with NVDA + Chrome and VoiceOver + iOS Safari: the announcements in the Accessibility checklist.

---

## Conflicts resolved

| # | Disagreement | Chosen | Why |
|---|---|---|---|
| 1 | Load-failure wording: empty-loading-error.md "Could not load {thing}. Check your internet connection, then try again." and its ban on "something went wrong", against microcopy.md's canonical "[Thing] did not load: [cause]. Try again." with cause "no connection" / "something went wrong". | **microcopy.md's words** ("The participant list did not load: no connection. Try again."), plus **empty-loading-error.md's mechanism** (1 s delay, one silent retry, Try again outside the status line, changed text on a repeat failure, written as "still did not load"). | The brief makes microcopy the wording authority, and its section 9 exists to settle such clashes. The "Still" re-announcement from empty-loading-error.md is kept because identical live-region text is often not re-announced. |
| 2 | Request failures: list-editor.md "…something went wrong in Grow2Notes. Try again.", form-validation.md "Not saved: no connection. Check your connection and try again." | microcopy.md's shape with **one verb per action**, as common-items.md: `Not moved / added / saved / archived / restored: [cause]. Try again.` | One shape per message kind (microcopy.md section 4), modelled on design's "Not saved: no connection."; "Not saved" after a failed Move is not literally true (editorial pass aligned the two list editors). |
| 3 | No-match text: design and search-filter.md `No participant matches ‘xyz’` (no full stop); empty-loading-error.md and participant-list-rows.md with straight quotes. | `No participant matches ‘xyz’.` | microcopy.md (N): typographic quotes and a full stop for sentences. |
| 4 | Goal row button order: foundations.md "Edit, Move up, Move down, Archive"; list-editor.md "Move up, Move down, Edit, Archive". | **list-editor.md** order. | The component owner's reasoning: the move pair stays together and Archive sits furthest from the text. Matches design 4.8's list of four. |
| 5 | Empty goals: microcopy.md (V) "Notes will show an empty Goals section." only; list-editor.md and empty-loading-error.md add "No goals yet." first. | `No goals yet. Notes will show an empty Goals section.` | "No goals yet" is the design's own name for the state; microcopy.md's own empty-state pattern is "what is empty, then the path". Flagged for sign-off. |
| 6 | Goal hint: form-validation.md "Up to 200 characters"; list-editor.md and microcopy.md "You can enter up to 200 characters". | `You can enter up to 200 characters` (the character count). | Two of three, the GOV.UK default, and the same count component as the flag Reason. |
| 7 | Active view empty when every participant is archived: empty-loading-error.md "No participants yet."; tabs-segmented.md "No active participants." | `No active participants.` (P); "No participants yet." only when there are none at all. | "No participants yet." would be untrue when archived participants exist (NN/g: misleading system status is harmful, https://www.nngroup.com/articles/empty-state-interface-design/). |
| 8 | Where goal announcements go: list-editor.md "the app's single role=status region"; status-messages.md "no global live region; pages render their own"; primary-actions.md "a page-level role=status region". | One **page-level** hidden status region on participant detail. | Satisfies all three: a single region for the page, owned by the page, present from the first render. |
| 9 | Error summary position: GOV.UK and form-validation.md's note form put it above the `<h1>`; form-validation.md and text-and-date-inputs.md put it "under the participant's name" on this screen. | **Above the `<h1>`, first in `<main>`** (the app-wide rule set in the editorial pass, form-validation.md); field errors only. Request failures go in the alert above Save. | One position on every screen (SC 3.2.4). The summary appears only after the manager pressed Save, so the name is still the next thing read after it. |
| 10 | Date of birth hint and widths: text-and-date-inputs.md "For example, 27 3 1987", widths 2.75em/4.5em; date-navigation.md "27 3 1985", widths in `ch`. | **text-and-date-inputs.md** (the Details owner; date-navigation.md asks for confirmation from the participant-detail spec). | Same pattern either way; em widths keep Day and Month at least 44 px. |
| 11 | Past-day page: date-navigation.md has no hint and puts the name in a caption above the `<h1>`; text-and-date-inputs.md adds the hint "A date before today". | Keep the hint; put the caption **inside** the `<h1>`. | The hint states the rule before an error, which matters because iOS ignores `max`. The caption inside the heading is read when the shell focuses the heading (note-identity-header.md uses the same pattern). |
| 12 | Restore for an archived participant: design 4.8 lists "Archive participant or Restore" under Actions **and** "a read-only banner with Restore". | **One** Restore, in the notice under the `<h1>`. Actions shows Past notes and Export record only. | One control per action; the notice is the first thing seen and is where focus lands after Archive (status-messages.md). Design's wording is met: Restore replaces Archive participant, in the banner. |
| 13 | Load timing on detail: list-editor.md shows the goals heading and help text at once; empty-loading-error.md loads details and goals as one region. | **One region** ("Loading participant…"). | The `<h1>` is data, so nothing meaningful can show before the participant arrives, and one region avoids parts appearing at different times. |
| 14 | Label weight and input size: search-filter.md and participant-list-rows.md use weight 600 and `max(1rem, 16px)`; text-and-date-inputs.md uses `outline-offset: 0` on inputs. | foundations.md: weights 400/700, inputs at 18 px body size, focus ring with a 2 px offset everywhere. | One set of tokens product-wide. |
| 15 | Goal row layout switch: foundations.md "one breakpoint at 40rem" (with a CI check on `min-width`); list-editor.md `@container list-editor (min-width: 44rem)`. | **The wrapping flex row from common-items.md** (no container query), also for the archived notice's Restore. | One ListEditor for Goals and Common items. common-items.md showed that a 44rem container leaves the text about 4rem wide; a wrapping flex line changes layout at the real point with no number, and 200% text falls back by itself. foundations.md's one-breakpoint CI check then holds with no exception. |
| 17 | Success after Restore: status-messages.md (a focused banner "Jane Citizen restored…") vs note-form.md and participant-notes.md (no success banners except Submit). | **No banner**; focus to the `<h1>`. The banner is an owner question. | design.md gives a success message only for Submit, and the owner rejects unrequested notifications; the editable page is itself the result. |
| 18 | Retrying writes: form-validation.md (mutations retry network failures and `5xx` up to 3 times) vs common-items.md and users.md (`retry: 0`). | **`retry: 0`** for Details Save and every goal write. | `POST /api/admin/participants` and `POST …/goals` have no idempotency key (6.6); a silent retry after a lost response would create a duplicate participant or goal. TanStack's own mutation default is no retry. |
| 16 | Where "Add participant" sits: design lists it last; search-filter.md requires it to stay in the same place whatever is typed. | Directly under the `<h1>`. | A position after a filtered list moves; this one never does. |

---

## Tensions with decisions

- **Family name required** (design A5 and §5.3 `FamilyName NOT NULL`; an assumed default, not a D-decision). W3C
  Internationalization advises against requiring a family name, because some people have only one name and are forced
  into entries such as ".". [Convention, https://www.w3.org/International/questions/qa-personal-names] The design needs
  it for family-name sorting. No change recommended; no workaround added.
- **A native date field for Write past-day note** (design 4.8 "a date field allowing past dates only"; the same control
  as 4.7). GOV.UK and NHS avoid `type="date"`; Hassell's 2019 testing found Dragon could not use it and VoiceOver on
  iOS did not read its errors [Research, dated, https://hassellinclusion.com/blog/input-type-date-ready-for-use/]; iOS
  ignores `max`. The mitigations are in this spec (hint, code check, server gate, the date shown in words on the note
  form header and banner). No change recommended.

---

## Open questions

1. **Copy sign-off** for every (P) string above, especially: the archived notice, the
   past-day archived sentence, "No goals yet.", "No active participants.", "No archived participants.", the date of
   birth hint "For example, 27 3 1987", "Note date" / "A date before today" / "Continue", and the load-failure lines.
   **Answered 9 October 2026 (D67):** approved as written; any of them can still be changed later in the copy module.
   A (P) string added after that date still needs the owner's approval.
2. **API gaps to close in M1** (gaps, not decision changes):
   - `GET /api/admin/participants` shows no per-row `RowVersion`/ETag, but every `PUT` and state-changing `POST`
     needs `If-Match` (6.6). Each row needs, for example, `rowVersion` as base64. (The goals list now has it.)
   - There is no single-participant admin GET; the detail view reads the date of birth from the whole admin list
     (fine for a few dozen participants). Confirm.
   - `POST /api/admin/participants` → `201` does not say how the new ID is returned (body or `Location`).
   - The admin participant list's sort order is not stated in 6.6; A5 says family then given name everywhere.
   - None of the admin `PUT`s return a time. This spec proposes reading the response's `Date` header (same origin, so
     readable) to show "Saved 4:12 pm" in Melbourne time without the device clock. [Opinion: verify in M1]
3. **Unsaved values when archiving** are dropped (no warning, as design asks for none outside Guide prompts). Confirm.
4. **Duplicate participant warning** and an **earliest date of birth**: neither is in design.md, so neither is built.
5. **Success banner after Restore.** A focused banner "Jane Citizen restored. They are back on Today." was proposed.
   design.md gives a success message only for Submit, so it is not built: after Restore the notice goes and focus
   moves to the name heading. Add the banner?
6. **Device checks in M1:** Chrome's autofill on `autocomplete="off"` name fields; iOS keyboard opening on Edit goal;
   the 500 ms no-match announcement; the focused notice and banner being read exactly once.
