# Note form and submit confirmation

Screen key: `note-form`. This file builds design.md §4.3 (with §3.3–3.6, §3.8, §3.9, §4.0, §5.5–5.8, §6.3, §6.9, §8.5 and
A3–A11, A24, A31–A33) and decisions D44–D47 (common items in groups, picked per note). It composes thirteen researched
component specs in `../components/` into one screen. It adds no feature, field, screen, setting, notification or data
beyond those decisions. Where two component specs disagreed, the choice and the reason are in **Conflicts resolved**
at the end.

**Common item groups: assumed defaults this file builds on.** They are shared by every file that touches groups, are
stated in full in [group-picker.md](../components/group-picker.md) and are recorded in design.md §13 as A41–A47 (with
A2, A3, A4 and A6 extended). They are assumed defaults, not decisions: the owner can override any of them by adding a
decision.

- **A41** Groups are organisation-wide; managers add, rename, reorder, archive and restore them; nothing is deleted;
  no new screens, notifications or settings.
- **A42** Each common item belongs to exactly one group.
- **A43** **Every note** (D45) is built in and always first. With no items, it is not shown on the note.
- **A44** Starting picks (D46) are copied from the participant's most recent **submitted** note, by any author (for a
  manager's past-day note: the most recent submitted note before that date). Archived groups are skipped. With no
  earlier note, nothing is picked. Only picks are copied, never item ticks.
- **A45** No group has to be picked to submit. Ticks stay optional; Guided notes is still required. A group with no
  active items is not offered on the note.
- **A46** Unpicking a group clears that group's ticks on this note, with no confirmation; picking it again shows its
  items unticked.
- **A47** When at least one group was copied, the new note and its draft show the copied line naming the source
  note's date. It is an addition, not part of D46; dropping it removes `picksCopiedFrom` and the copied-line rows.
- **A3** (extended) The snapshot holds Every note and every active group with their items' wording and order. The
  picks are part of the note: they autosave with the draft, and an edit after submit can change them (versioned like
  ticks).
- **A4** (extended) The read view, daily report and record export show Every note and the picked groups only (D47).

**Copy marks.** **(V)** word for word from design.md. **(N)** design.md's words with only the date, time or name format
normalised by `microcopy.md` §3 and §9. **(D)** derived from design.md wording for a case design.md does not cover.
**(P)** proposed, needs the owner's sign-off. The copy module uses typographic apostrophes (’); this file shows
straight quotes for readability (`microcopy.md` §1).

**Evidence grades.** **[Research]** studies or usability testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML, CSS ·
**[Convention]** established design systems · **[Opinion]** reasoned judgement. Each grade is backed by the cited
component file, which holds the full evidence and URLs; the key sources are repeated under **Sources**.

---

## Purpose and who uses it

**Purpose (design.md §4.3, D44):** write one participant's note for one date (goal ticks; common-item ticks for
Every note and for the groups the writer ticks as having happened that day; one guided text box; an optional flag with
a reason), then submit it after a check that names the participant.

| Who | How they arrive | What they do here | Usual device and context |
|---|---|---|---|
| **Worker** | Today row with no note yet, or with their own draft (4.2). "Your unfinished drafts" (an earlier-day draft or a pending edit). Read view → **Edit** on their own submitted note (4.4). | Write and submit today's note. Finish their own draft from an earlier day (3.4). Edit their own submitted note (3.5). Discard their own draft. | Their own phone (D21), often at the end of a long shift, tired; many write in English as a second language; some dictate with the phone keyboard (A31). |
| **Manager** | The same routes for today's notes (A7). Participant detail → **Write past-day note** → date (3.8, 4.8). Read view or Flagged → **Edit** on any submitted note. | Everything a worker does, plus past-day notes (D35) and editing anyone's submitted note (D19). | Laptop for past-day notes and edits (paste and desktop speech tools); phone for today's notes. |

What this screen must protect against, in design.md's words:

1. **A note on the wrong participant** (3.9): the full name in the header, in the confirmation and on the confirm
   button.
2. **Lost text** (3.4, D22): autosave to the server only; "The text on screen is never replaced without asking" (4.3).
3. **Submitting content the server has not saved** (5.8 step 1): Submit is unavailable until the latest change is saved.
4. **A second note for the same day** (3.2): enforced by the server; the form shows the refusal.
5. **A group left ticked from the last note that did not happen today** (D46): one line names the date the ticks were
   copied from, and each copied group's items sit unticked in the writer's path (group-picker.md).

Not on this screen: someone else's draft (workers see "Alex P. started today's note…", managers see the read view,
4.2), the read view (4.4), version history (4.5) and the session warning (shell-owned, `session-timeout.md`).

### Routes

This table is the one place that defines the note URLs; today.md, participant-notes.md, flagged.md and
participants.md link to it. URLs hold IDs and dates only, and no route pushes router state (app-shell.md).

| URL | What it renders, decided from fresh data on every visit | Page title |
|---|---|---|
| `/participants/{participantId}/notes/{noteDate}` (the shared note URL) | No note yet for that day, or the viewer's own draft → **this form** (new or draft). Someone else's draft → a worker sees the "Alex P. started … Only Alex P. can finish it." sentence; a manager sees the read-only draft with Discard draft (participant-notes.md, view C). A submitted note → **the read view** (participant-notes.md, view B), whichever list opened it. | Grow2Notes – Note |
| `/participants/{participantId}/notes/{noteDate}/edit` | **Edit mode** of this form for a submitted note: reopens the viewer's pending edit, or starts one at the first change. `403 note.not_editable` → the whole-page "You cannot edit this note" (title "Grow2Notes – Note", app-shell.md). A note that is not submitted → `replace` to the shared URL. | Grow2Notes – Note |

- **Into the form:** Today rows, "Your unfinished drafts" rows with `kind: "draft"`, and Write past-day note's
  Continue push the shared URL. Rows with `kind: "pendingEdit"` push `/edit`. The read view's **Edit** navigates to
  `/edit` with `replace`.
- **Out of edit mode:** Save changes and Cancel `replace` `/edit` with the shared URL, which then renders the read
  view. The shell copies the opened-from record across each replace, so the back link keeps its destination.
- **Groups add nothing to the URLs** (D44–D47). Picks live in form state and on the server only: never in the URL,
  router state or device storage (D22). They change the payloads, not the routes or caches:
  - `GET {base}/draft` returns the whole snapshot as `commonItemGroups: [{id, name, isEveryNote, isPicked, items}]`
    (Every note first, then every group in order, picked or not; on a new note `isPicked` holds the copied picks, with
    archived or empty groups left out) and `picksCopiedFrom` (the source note's date, or `null`), kept with the draft
    so the copied line survives a reload (design.md §5.7, §6.3).
  - The first autosave `PUT {base}/draft` of a new note sends `listsVersion` back (the server works out and stores
    `picksCopiedFrom` itself; the client never sends it); every `PUT`
    and the `POST {base}/versions` send `pickedGroupIds[]` beside `tickedCommonItemIds[]`. The server refuses a pick
    of Every note or of an empty group, and a tick outside Every note and the picked groups (`422`).
  - No new query keys and no new invalidations: the draft query keeps `staleTime: Infinity`, and the existing
    `['notes', participantId, noteDate]` prefix invalidation after Save changes or Cancel already refreshes the read
    view and version pages, which show the picked groups (participant-notes.md, version-history.md).

---

## Layout - phone (about 375 px)

One column, 16 px side gutters, nothing sideways at 320 px. Sizes are from `foundations.md` (body 18 px, h1 28 px,
h2 22 px, small 16 px; 56 px tick rows; 48 px buttons). Only the top bar is sticky.

**Default: a draft, scrolled to the top**

```
 375 px
+---------------------------------------+
| Grow2Notes                  Account v |  app header: static, scrolls away
| Today                                 |  (managers: Today  Flagged [3]  Report  Manage)
+=======================================+
| < Today                 Saved 9:42 am |  TOP BAR: sticky, one row, min 56 px, opaque,
+=======================================+  1 px bottom border, no animation (shell's bar slot)
| Jane Citizen                          |  <h1> line 1: name, 28 px bold, as stored, never truncated
| Thursday 1 October 2026               |  <h1> line 2: <time>, 18 px regular, full text colour
|                                       |
| Past notes                            |  text link, 44 px target
|                                       |
| 1. Goals                              |  <fieldset><legend><h2>
| Ticked means reached.                 |  hint, 16 px secondary text
|---------------------------------------|
| [x]  Makes own breakfast              |  whole row is the <label>: min 56 px,
|---------------------------------------|  40 px box, tick drawn with borders
| [ ]  Catches bus to day program       |
|---------------------------------------|
| [x]  Phones her sister                |
|---------------------------------------|
|                                       |  32 px section gap
| 2. Common items                       |  <h2>, not in a legend (it holds several fieldsets)
| Ticked means done.                    |  section hint
|                                       |
| Every note                            |  <fieldset><legend><h3>; always first (D45)
|---------------------------------------|
| [x]  Medication prompted              |
|---------------------------------------|
|                                       |  24 px
| Which of these happened?              |  <fieldset><legend><h3> (V): the group picker
| Tick all that happened. Their items   |  hint (V), 16 px secondary text
| show below.                           |
|---------------------------------------|
| [x]  Community outing                 |  one 56 px tick row per group with items,
|---------------------------------------|  in the manager's order
| [ ]  Personal care                    |
|---------------------------------------|
|                                       |  24 px
| Community outing                      |  picked group: <fieldset><legend><h3> name
|---------------------------------------|  (unpicked groups are `hidden`)
| [ ]  Travelled by bus or train        |
|---------------------------------------|
|  ...                                  |
|                                       |
| 3. Guided notes                       |  <h2><label for>
| +-----------------------------------+ |
| | Mood and wellbeing today?         | |  guide prompts as placeholder, #626262
| | What did you do together?         | |  (6.1:1), upright; box grows with the text,
| | Anything to follow up?            | |  at least about 6 lines, no inner scroll
| |                                   | |
| +-----------------------------------+ |
|                                       |
| [ ]  Flag for manager                 |  same 56 px tick row
|                                       |
| [            Submit note            ] |  primary, full width, 48 px, inline (never sticky)
|                                       |
| [           More actions            ] |  secondary disclosure; only once a draft exists
+---------------------------------------+
```

**Flag ticked** (the block appears instantly, focus stays on the tick box)

```
| [x]  Flag for manager                 |
|     | Reason                          |  4 px bar under the box centre; <label> bold
|     | +-----------------------------+ |  <textarea rows=4>, no maxlength
|     | |                             | |
|     | +-----------------------------+ |
|     | You can enter up to 200         |  visible count (aria-hidden)
|     | characters                      |
```

**New note, groups copied from the last note** (D46; section 2 only)

```
| 2. Common items                       |
| Ticked means done.                    |
|                                       |
| Every note                            |
|---------------------------------------|
| [ ]  Medication prompted              |  items never copied: all unticked
|---------------------------------------|
|                                       |
| Which of these happened?              |
| Tick all that happened. Their items   |
| show below.                           |
| These ticks are copied from the note  |  copied line (V): 18 px, full text colour,
| for Wednesday 30 September 2026.      |  no icon, no bar, no dismiss; only when at
| Untick any that did not happen.       |  least one group was pre-picked
|---------------------------------------|
| [x]  Community outing                 |  copied pick
|---------------------------------------|
| [ ]  In-home support                  |
|---------------------------------------|
|                                       |
| Community outing                      |  its items show below, unticked
|---------------------------------------|
| [ ]  Travelled by bus or train        |
|---------------------------------------|
| [ ]  Paid for own purchases           |
|---------------------------------------|
```

**Only Every note has items** (no other group set up): Every note and its rows only, with its heading; no picker.
**No common items at all:** `2. Common items` and the sentence **No common items set up.** (V); no fieldset.

**Top bar after scrolling** (the large name is behind the bar)

```
+=======================================+
| < Today   Jane Citizen  Saved 9:42 am |  compact name: one line, bold, body size,
+=======================================+  ends in "..." if short of room, hidden under 6rem
```

**Save failed** (the indicator itself becomes the banner)

```
+=======================================+
| < Today                               |
| (!) Not saved: no connection. Keep    |  same <p role="status">, now a full-width second
|     this page open; retrying.         |  row in attention colours; icon aria-hidden
+=======================================+
```

**Changed on another device or tab** (blocking banner; one at a time)

```
+=======================================+
| < Today                     Not saved |
| (!) This note was changed on another  |  <section>, attention colours
|     device or tab.                    |
| [  Keep the text on this screen   ]   |  first, primary style
| [  Load the other version         ]   |  secondary style
+=======================================+
```

**After a failed Submit**

```
+=======================================+
| < Today                 Saved 9:42 am |
+=======================================+
| +-----------------------------------+ |
| | There is a problem                | |  first child of <main>, focused, 4 px error border
| | Write your notes in the Guided    | |  link (same text as the inline message)
| | notes box. The grey text is only  | |
| | a guide.                          | |
| | Enter a reason for the flag       | |  link
| +-----------------------------------+ |
| Jane Citizen                          |
|  ...                                  |
|| 3. Guided notes                      |  4 px error bar on the section
|| Error: Write your notes in the       |  "Error: " is visually hidden; bold, error colour,
|| Guided notes box. The grey text is   |  ABOVE the box so the keyboard cannot cover it
|| only a guide.                        |
|| +----------------------------------+ |  3 px error-colour border
```

**Editing a submitted note**

```
| Editing submitted note (version 2)    |  caption inside the <h1>, 18 px regular
| Jane Citizen                          |
| Thursday 1 October 2026               |
|  ... same sections ...                |
| [           Save changes            ] |  primary
|                                       |  16 px gap
| [              Cancel               ] |  secondary; no More actions in edit mode
```

**Submit confirmation** (native `<dialog>`, `showModal()`)

```
+---------------------------------------+
|  backdrop: page dimmed and inert      |
| +-----------------------------------+ |
| | Submit today's note for           | |  <h2 tabindex=-1>: focused on open
| | Jane Citizen                      | |  28 px bold, wraps, never truncated
| |                                   | |
| | Thursday 1 October 2026           | |  body, read by aria-describedby
| | Flagged for manager: No           | |
| |                                   | |  <p role="alert">, empty until an error
| | [  Submit note for Jane Citizen ] | |  primary, full width, wraps for long names
| | [            Go back            ] | |  secondary, full width
| |                                   | |  <p role="status">: "Submitting..." after 1 s
| +-----------------------------------+ |
+---------------------------------------+
   width: screen minus 16 px each side
```

**Discard confirmation**

```
| +-----------------------------------+ |
| | Discard the draft note for Jane   | |  <h2 tabindex=-1>, focused on open
| | Citizen?                          | |
| | Nobody will be able to open this  | |  (P)
| | draft again.                      | |
| | [ Discard draft for Jane Citizen ]| |  WARNING style (red fill, white bold text)
| | [            Go back            ] | |
| +-----------------------------------+ |
```

---

## Layout - laptop

At `@media (min-width: 40rem)`, the one breakpoint in the app (`foundations.md`). Same order, same components, same
words (`microcopy.md` §6: never a different label per device).

```
+------------------------------------------------------------------------------+
| Grow2Notes                                                         Account v |
| Today   Flagged [3]   Report   Manage                                        |
+==============================================================================+  bar background spans the window
|     < Today          Jane Citizen*                     Saved 9:42 am         |  bar contents = the 40rem column
+==============================================================================+  (* compact name, only when scrolled)
|     Jane Citizen                                  (32 px)                    |
|     Thursday 1 October 2026                                                  |
|     Past notes                                                               |
|     1. Goals ...                                                             |
|     |<------------- 40rem reading column, left-aligned ------------->|       |
|     [ Submit note ]                       (width of its label, min 8rem)     |
|     [ More actions ]                                                         |
+------------------------------------------------------------------------------+
```

What changes from the phone:

| Part | Laptop |
|---|---|
| Content column | `--measure` 40rem (about 70 characters at 18 px), left-aligned inside the 60rem page container, so the header nav and the form share a left edge. |
| Gutters and gaps | 32 px gutters; 48 px between sections. |
| Type | h1 (name) 32 px; h2 24 px; body stays 18 px. |
| Top bar | The background spans the window; its contents line up with the 40rem column, so the save indicator sits at the column's right edge, near the content, not in the window corner (`autosave-status.md`). |
| Buttons | As wide as their label (min 8rem), left-aligned. Edit mode: **Save changes** then **Cancel** in one row with a 16 px gap, wrapping when text is large. Takeover banner buttons side by side, wrapping. |
| Hover | Tick rows and the flag row get the hover tint under `@media (hover: hover)`; links thicken their underline; buttons darken. Hover never carries information. |
| Dialogs | 28rem wide, centred. Buttons **stay stacked and full width** inside the dialog (`confirm-dialog.md`). |
| Guided notes | Fills the 40rem column. Paste and desktop speech tools (Dragon, Windows voice typing) are the common inputs for past-day notes. |
| Short viewport | `@media (max-height: 30rem)` (landscape phone, or 200%+ zoom on a laptop): the top bar becomes static and the compact name is never shown (SC 1.4.10 advisory). |

---

## Components, in order

Top to bottom in DOM order (which is also visual and focus order). Each item links to the component spec and gives the
screen-specific settings. Nothing here uses React Aria: every part is native HTML (design.md §7.5 rule).

### 0. App header and skip link: [app-shell-nav.md](../components/app-shell-nav.md)

- Static, never sticky. "Skip to main content" first in `<body>`, targeting `<main id="main-content" tabindex="-1">`.
- No nav item is marked current on this screen (it is reached from several places). Page title **Grow2Notes – Note**
  (V pattern, 4.0); **Error: Grow2Notes – Note** while the error summary shows (`form-validation.md`). Never a name.
  [Standard] SC 2.4.2: an app name is sufficient for a web application.

### 1. Top bar: [note-identity-header.md](../components/note-identity-header.md) and [autosave-status.md](../components/autosave-status.md)

Rendered through `PageBar` into the shell's **before-main bar slot** (app-shell.md component 3a), between the app
header and `<main>`, so the skip link passes it (GOV.UK back-link placement) [Convention], and it is `hidden` with the
route in the signed-out-in-place view. `position: sticky; inset-block-start: 0`, opaque surface, 1 px bottom border,
minimum 3.5rem, one row.
Static under `@media (max-height: 30rem)` [Standard, advisory text in Understanding 1.4.10]. A `ResizeObserver` writes
its height to `--topbar-h`; `:root { scroll-padding-block-start: calc(var(--topbar-h) + 0.5rem) }`, 0 when static
[Standard] SC 2.4.11, technique C43. It is the only sticky element in the app (`foundations.md`).

| Part | Settings |
|---|---|
| **Back link** | The shared **BackLink** (app-shell.md component 3a): a real `<a href>` (IDs and dates only), decorative chevron, hit area at least 44 × 44 px, never disabled. Label = destination **(D)**, read from the shell's in-memory opened-from record (no router state): **Today** by default (V: "back to Today"); **Participant** when a manager opened a past-day note from Participant detail; **Past notes** when opened from the history list; in edit mode, the same destination as the read view's back link (Today, Past notes or Flagged). Falls back to **Today** after a reload, pasted URL or new tab. Pops (`navigate(-1)`) when the previous history entry is the destination; otherwise follows the `href`; modified clicks follow the `href`. [Convention] GOV.UK/NHS back link, Android Up; [Research] Baymard back-button expectations. |
| **Compact name** | **Pending owner sign-off (Open questions).** Once the `<h1>` has scrolled behind the bar (IntersectionObserver), show "Jane Citizen" on one line, bold, body size, between the back link and the indicator, `translate="no"`. May end in "…"; hidden when its slot is under 6rem (container query) and whenever the bar is static. Plain text, not a heading, link or live region; in the DOM only while shown. Instant, no animation. [Convention] Apple large-title behaviour; SAFER 1.3 "all portions" [Convention]. If the owner declines it, drop it; nothing else depends on it. |
| **Save indicator** | `<p id="save-status" role="status" aria-live="polite" aria-atomic="true">`, always mounted from the moment the form renders, right-aligned, 16 px, `--colour-text-secondary`, `tabular-nums`, wraps, never truncated. No spinner, no animation. Text per **States**. [Standard] SC 4.1.3 via ARIA22. |
| **Row 2: failed save** | In the failed state the same `#save-status` element spans the full width as the banner (attention tint, 2 px attention border, `aria-hidden` warning icon). The text is never shown or announced twice. |
| **Row 2: blocking banner** | One at a time, replacing the failed-save row: takeover (with two buttons), started by someone else, after midnight, or participant archived. `<section aria-labelledby>` whose label is the sentence; not a live region (it holds controls); the sentence is announced once through the form's assertive announcer (item 16). Buttons are native, stacked full width at 44 px+ on a phone. |

### 2. Error summary: [form-validation.md](../components/form-validation.md)

- First child of `<main>`, above the `<h1>`, only after a failed **Submit note** or **Save changes**. `tabIndex={-1}`,
  4 px error-colour border, `<h2>` **There is a problem**, a `<ul>` of links in page order (Guided notes first, then
  Reason). Link text is exactly the inline message. Unlinked items only for problems with no field.
- A link click: `preventDefault()`, scroll the field's label into view (`3. Guided notes` heading, or `Reason`), then
  `focus({ preventScroll: true })`. No hash change, no history entry. [Convention] govuk-frontend error summary.
- No `role="alert"` on it: focus does the announcing in this single-page app [Opinion; verify with NVDA and VoiceOver].
- Each link block is at least 44 px tall.

### 3. Identity block: [note-identity-header.md](../components/note-identity-header.md), sizes from [foundations.md](../components/foundations.md)

- One `<h1 tabIndex={-1}>` holding up to three block lines, in this order (the app-wide identity-heading rule in
  `note-identity-header.md`, the same on the read view and the version pages):
  1. optional caption (edit mode: **Editing submitted note (version 2)** (V)), 18 px regular;
  2. `<span translate="no">` with given name + space + family name, **as stored**: never upper-cased (the sketch's
     capitals mean large type), never truncated, `overflow-wrap: break-word; hyphens: manual; text-wrap: balance`.
     28 px phone, 32 px laptop, bold;
  3. `<time dateTime="2026-10-01">Thursday 1 October 2026</time>`: the note date, `dateLong` from
     `src/copy/format.ts`, built from numeric parts with no time-zone conversion (a calendar date). Body size,
     weight 400, in the **full text colour**, because it is part of the right-day check.
  Literal spaces between the lines give the accessible name "Jane Citizen Thursday 1 October 2026" (edit:
  "Editing submitted note (version 2) Jane Citizen Thursday 1 October 2026"), so moving from the read view to Edit
  changes only the caption that is announced, and the focused heading confirms the day as well as the person.
- Renders **nothing until both** `GET /api/participants/{id}` and `GET {base}/draft` have succeeded. No placeholder,
  cached or previous name; no `placeholderData`/`keepPreviousData` on either query; the page is keyed by
  `participantId/noteDate` so React never reuses one participant's state for another. [Opinion] This is the most
  likely way the build itself could put a note on the wrong person.

### 4. Notice (past-day or earlier-day draft): [status-messages.md](../components/status-messages.md)

- Directly after the date. Plain page content: no role, no focus, no dismiss, no timer. Notice tone: 6 px
  `--colour-action` inline-start bar plus 1 px border on `--colour-action-tint`, `aria-hidden` "i" icon, body-colour
  text. Real borders, so forced colours keep it.
- **Only one.** A past-day note wins over the earlier-day message (the past-day sentence already names the date).
- Shown in edit mode too for a past-day note (3.8: "everywhere it appears").
- Copy in **States**; dates normalised to `dateLong` (N) because they sit in sentences (`microcopy.md` §9;
  Style Manual "Only use abbreviations if space is limited") [Convention].

### 5. Past notes link: design.md §4.4 ("the 'Past notes' link in the note form and read view")

- A plain text link **Past notes** (V) on its own line, with a 44 px tappable box, to the participant's history. Same
  place relative to the header as on the read view (`note-read-view.md`), so it is found in the same spot on both
  (SC 3.2.3) [Standard]. Leaving through it follows the same rules as the back link (Interactions, "Leaving").

### 6. Version-conflict notice (edit mode only): [autosave-status.md](../components/autosave-status.md)

- Appears only after **Save changes** returns `409 note.version_conflict`. `<section tabIndex={-1} aria-labelledby>` in
  page flow, error tone not used (it is a notice with an action): attention bar and tint.
- `<h2>` (P) **Sam Lee saved version 3 at 5:03 pm while you were editing.** then `<p>` (P) **Your changes are still
  below.** The time uses `stamp` (time alone if the same Melbourne day as today, otherwise "Thu 1 Oct 2026, 4:12 pm").
- `<details open><summary>` (P) **Version 3 by Sam Lee** holding the newer version read-only, reusing the read-view
  rendering (`TickListRead` for Goals, then Every note and the groups picked in that version, by name (D47); the text
  with `white-space: pre-wrap`; the flag line).
- Button **Start again from the latest version** (V, §6.3), secondary style.

### 7. Lists-changed notice: [checkbox-list.md](../components/checkbox-list.md)

- A `<div role="status">` directly above **1. Goals**, **always in the DOM from mount** (empty), so the polite
  announcement is reliable [Standard] ARIA22. Filled with (V) **The goal or common-item list was just changed. Please
  check your ticks.** Notice tone. Stays until the person leaves the form; never times out. A change to the groups (a
  group added, renamed, archived or restored, or an item moved between groups) counts as a list change
  (`listsVersion` covers groups, group-picker.md).

### 8. 1. Goals: [checkbox-list.md](../components/checkbox-list.md) (`TickListField`)

- `<fieldset aria-describedby="goal-hint">` with `border:0; padding:0; margin:0; min-inline-size:0` (stops a long goal
  forcing sideways scroll at 320 px) [Standard] HTML UA stylesheet, SC 1.4.10. `<legend><h2>1. Goals</h2></legend>`
  (V); hint `<p id="goal-hint">` **Ticked means reached.** (V, 4.3).
- One `<label for>` row per goal that also wraps its input: full width, min 56 px, 1 px divider, `cursor: pointer`,
  `touch-action: manipulation`. Native `<input type="checkbox" id="goal-{id}">` with `appearance: none`, 2.5rem (40 px)
  box, 2 px `currentColor` border, tick drawn with borders in `::before` (survives forced colours and printing; not
  `accent-color`) [Research] GDS 2016 (users aim at the box; large controls tested well) · [Standard] CSS Color Adjust.
- Text exactly as in the snapshot, React text child, `overflow-wrap: anywhere`, never truncated; first line centred
  on the box. Snapshot order; never re-sorted; no "tick all".
- Controlled: `checked` from a `Set<string>` of ticked IDs in form state; `onChange` updates it and calls the autosave
  `markChanged()`. Rows keyed by item ID.
- **No goals:** no fieldset (an empty fieldset confuses screen readers [Research] PowerMapper 2025); `<h2>1. Goals</h2>`
  and a `<p>`: new note or draft (V) **No goals set up for Jane yet. A manager can add them.** (given name only, as
  design.md writes it); edit mode **No goals set** (D, the 11.3 report string, because a fixed snapshot can no longer
  change).
- `TickListField` with `headingLevel={2}` (its default) and its own hint.

### 9. 2. Common items: [group-picker.md](../components/group-picker.md) (`CommonItemsField`), rows from [checkbox-list.md](../components/checkbox-list.md) (`TickListField`)

Pattern (b) of group-picker.md, D44–D47: Every note first, then one question with a tick per group, then each picked
group's items as its own headed list. Every control is the same native 40 px box in a 56 px row as Goals. No nested
fieldsets, no disclosure widgets, no dialog, no React Aria.

- **Section:** a plain `<div>` (not a fieldset: it now holds several; not a named `<section>`: that would add a region)
  holding `<h2>2. Common items</h2>` (V) and `<p id="common-hint">` **Ticked means done.** (V).
- **Every note** (D45): `TickListField` with `headingLevel={3}`, heading **Every note** (V), `hintId="common-hint"`,
  ids `common-{itemId}`. Always first. Not rendered when the snapshot's Every note group has no items.
- **Group picker:** one `<fieldset>` (`border: 0; padding: 0; margin: 0; min-inline-size: 0`) with
  `<legend><h3>Which of these happened?</h3></legend>` (V), hint `<p id="picker-hint">` **Tick all that happened. Their
  items show below.** (V), then the copied line (below), then one tick row per group that has at least one item, in
  the manager's order. Each row is the checkbox-list row: `<label for>` wrapping `<input type="checkbox"
  id="pick-{groupId}">` with the shared `.box` class, the group name as a text child (never truncated), plus
  `aria-controls="group-{groupId}"` and `aria-expanded={picked}`, the same pattern as Flag for manager. Fieldset
  `aria-describedby="picker-hint picker-copied"` (the second id only while the copied line shows). Not rendered when
  no group other than Every note has items. Every note is never a row here (not even a ticked, disabled one).
- **Copied line** (D46): `<p id="picker-copied">` **These ticks are copied from the note for Wednesday 30 September
  2026. Untick any that did not happen.** (V). The date is the source note's date, `dateLong` in `<time dateTime>`.
  Body size, `--colour-text`, no icon, no bar, no dismiss. Shown on a new note or a draft whenever at least one group
  was pre-picked (`picksCopiedFrom` is set), including after the writer changes picks and after a reload. Never in
  edit mode.
- **Picked groups:** one wrapper `<div id="group-{groupId}">` per offered group, **always in the DOM** (keeps
  `aria-controls` valid), `hidden` when not picked. Inside it, `TickListField` with `headingLevel={3}`, the group's
  name as stored in the snapshot as the heading, `hintId="common-hint"`, ids `common-{itemId}`. All wrappers follow
  the picker in the manager's order, never in the order they were ticked. Global base CSS keeps
  `[hidden] { display: none !important; }` so no display rule can show an unpicked group.
- **Spacing:** `--space-5` (24 px) between Every note, the picker and each picked group; `--section-gap` before and
  after the section.
- **State:** `pickedGroupIds: Set<string>` (initialised from the groups' `isPicked` in the draft response) beside
  `tickedCommonItemIds: Set<string>`, both owned by the form. A pick or unpick calls `markChanged()`; unpicking also
  removes that group's item ticks from form state at once (A46). The payload filters `tickedCommonItemIds` to Every
  note and picked groups' items, as a guard.
- **No items anywhere:** no fieldset; `<h2>2. Common items</h2>` and (V) **No common items set up.**; edit mode
  **No common items set** (D, 11.3).
- Workers never see the word "group" on this screen: the question and the group names are enough, and in NDIS work
  "group" also means group supports (D36). The screen says "tick", never "pick" or "select" (microcopy.md glossary).

### 10. 3. Guided notes: [guided-notes-textarea.md](../components/guided-notes-textarea.md)

- `<h2><label for="guided-notes">3. Guided notes</label></h2>` (V): the heading is the label, so the accessible name
  equals the visible words (SC 2.5.3) and dictation users can say it.
- **Uncontrolled** `<textarea id="guided-notes" defaultValue>`: initial text captured once when the draft loads;
  autosave and Submit read `ref.current.value`; React never writes the value back, so no refetch or re-render can
  replace on-screen text. The only programmatic replacement is **Load the other version**. [Standard] react.dev; [Opinion]
  enforcing design 4.3.
- **Placeholder** = the guide prompts from `GET {base}/draft` (`guidePrompts`), line breaks kept; `::placeholder {
  color: #626262; opacity: 1 }` (6.1:1 on white), upright. No placeholder when no prompts are set. Never copied into the
  value. [Standard] SC 1.4.3 applies to placeholder text.
- **Size:** `field-sizing: content` behind `@supports`, with the grid-replica fallback for older phones; never shorter
  than the prompts (a hidden copy of the prompts holds the height) or than about 6 lines (`min-block-size: 11em`); no
  max height, no inner scroll, `resize: none`. Font size inherits 18 px (never under 16 px, which would trigger the iOS
  focus zoom).
- **Attributes:** `spellCheck`, `autoCorrect="on"`, `autoCapitalize="sentences"`, `autoComplete="off"`,
  `writingsuggestions="false"` (set via ref), `aria-required="true"` (not `required`). Not set: `maxlength`, `rows`,
  `inputmode`, `enterkeyhint`, `wrap` (keeps the keyboard microphone and Enter = new line).
- **Counter:** hidden until 18,000 characters. Visible line under the box (`aria-hidden`), plus an always-present
  visually hidden `aria-live="polite"` region updated 1 s after typing stops. Count = `value.length` (UTF-16 units,
  matching .NET `string.Length`) [Convention] GOV.UK character count, tested with 17 users.
- No hint text, no autofocus, no keydown handlers, no trimming or reformatting of the text.

### 11. Flag for manager and Reason: [conditional-reveal.md](../components/conditional-reveal.md)

- Its own block after Guided notes, no heading (as in the 4.3 sketch).
- **Tick row:** the same 56 px row and 40 px `.box` class as Goals (`checkbox-list.md`), label **Flag for manager**
  (V), `id="flag-for-manager"`, `aria-controls="flag-reason-group"`, `aria-expanded={flagged}` [Standard] ARIA 1.2
  allows `aria-expanded` on checkbox; [Convention] GOV.UK Frontend.
- **Reveal block** `#flag-reason-group`: always in the DOM, toggled with the `hidden` attribute; a 4 px bar under the
  centre of the 40 px box (error colour when Reason has an error), the field lined up with the label text. Contents:
  `<label for="flag-reason">Reason</label>` (V, bold); visually hidden hint `<p id="flag-reason-hint">You can enter up
  to 200 characters</p>`; the error (when present); `<textarea id="flag-reason" rows=4>`, controlled, `resize:
  vertical`, `aria-required="true"`, no `maxlength`, no placeholder; the visible count (`aria-hidden`); a visually
  hidden polite live region, always present, updated 1 s after typing stops.
- **Count copy:** **You can enter up to 200 characters** (empty), **You have 153 characters remaining**, **You have 12
  characters too many** (bold, error colour, so not colour alone). Singular "1 character". [Convention] GOV.UK.
- The typed reason is kept in form state while unticked and comes back on re-tick (intent of SC 3.3.7).

### 12. Actions: [primary-actions.md](../components/primary-actions.md) and [autosave-status.md](../components/autosave-status.md)

- Inline after the flag block, **never sticky or fixed** (a fixed bar sits under the phone keyboard and is the classic
  2.4.11 failure) [Standard] Chrome viewport docs, Understanding 2.4.11.
- An always-rendered, empty `<div role="alert">` directly above the buttons, for request failures only.
- **Submit note** (V): `<button type="submit">`, primary, full width on a phone. When the latest change is not saved,
  it is **unavailable**: `aria-disabled="true"` (never `disabled`), dashed border, label still ≥ 4.5:1,
  `aria-describedby="save-status"` (plus the blocking banner's sentence id while one shows). The handler guards every
  press, including Enter's implicit submission.
- **Edit mode:** **Save changes** (V, primary) then **Cancel** (V, secondary) in the same place; no More actions.
- Busy state: `aria-disabled` from the press, repeat presses ignored, busy label in the same grid cell after
  **400 ms** (the app-wide page-button rule, primary-actions.md) so the button never changes size. The label is also
  written into this page's status region (component 16's polite region is this page's `PageStatus`, app-shell.md).
  Only the dialogs keep the 1 s status line (confirm-dialog.md).

### 13. More actions → Discard draft: [primary-actions.md](../components/primary-actions.md)

- Shown only when a **saved draft** exists (not on a new note, not in edit mode). 1.5rem below Submit.
- APG **disclosure**, not a menu: `<button type="button" aria-expanded aria-controls>` **More actions** (P), revealing
  an inline panel with one secondary button **Discard draft** (V). No `role="menu"`. Escape closes the panel and
  returns focus to More actions. [Standard] APG Disclosure.

### 14. Submit confirmation: [confirm-dialog.md](../components/confirm-dialog.md) and [note-identity-header.md](../components/note-identity-header.md)

- Shared `ConfirmDialog`: native `<dialog role="alertdialog" aria-modal="true" aria-labelledby={title}
  aria-describedby={body}>`, opened with `showModal()` (top layer, page inert, implicit Escape and Android Back), mounted
  on open, no portal, no animation, no close X, no backdrop dismiss. [Standard] HTML dialog, APG alertdialog.
- Title `<h2 tabIndex={-1}>`: lead **Submit today's note for** (V), or **Submit the note for** (P/D) when the note date
  is not the server's Melbourne today; then the name on its own line at h1 size, bold, `translate="no"`.
- Body: the note date in `dateLong` (V) and **Flagged for manager: No** (V) or **Yes** (D), from the values being
  submitted. Nothing else: no tick summary, no picked groups, no reason.
- Buttons stacked full width on every screen size: **Submit note for Jane Citizen** (V, primary, wraps, never
  truncated), then **Go back** (V, secondary).
- `<p role="alert">` (empty) above the buttons for errors; `<p role="status">` (empty) below them for the busy line.
- Width `min(100vw - 2rem, 28rem)`, `max-height: calc(100dvh - 2rem)`, scrolls inside itself at 200% text.

### 15. Discard confirmation: [confirm-dialog.md](../components/confirm-dialog.md)

- Same component. Title (V) **Discard the draft note for Jane Citizen?** Body (P): for an earlier-day draft **This
  draft is for Wednesday 30 September 2026.**, then always **Nobody will be able to open this draft again.**
- Confirm **Discard draft for Jane Citizen** (P, canonical in `microcopy.md` §9), **warning** style (red fill, white
  bold text; one of only two warning buttons in the product). Then **Go back**.

### 16. Form-level announcer: [autosave-status.md](../components/autosave-status.md)

- Two visually hidden regions mounted with the form and never removed: `<div role="status">` (polite) and
  `<div role="alert">` (assertive). Used only to (a) announce a blocking banner's sentence once (assertive), (b)
  repeat "Not saved: no connection. Keep this page open; retrying." when a press did nothing because the save has
  failed (polite), and (c) carry the page buttons' busy labels ("Saving changes…", "Cancelling…"): the polite region
  is this page's `PageStatus` (app-shell.md component 3), so the shared Button finds it through context. Writing the
  same text twice: clear, then set on the next frame, so it is announced again.

### 17. Session warning: [session-timeout.md](../components/session-timeout.md)

- Mounted once by the signed-in layout, not by this screen. Screen-specific rules are under Interactions, "Session".

---

## States

"Submit" below means **Submit note**, or **Save changes** in edit mode. "Available" means a press runs the flow in
Interactions; "unavailable" means `aria-disabled` with the reason in `#save-status`.

### Loading and load errors

| State | What shows | Submit | Focus and announcements |
|---|---|---|---|
| **Loading** (participant or draft not yet loaded) | Top bar and back link at once; indicator empty. `<main>` holds one `<p role="status">` that is empty for 1 s, then **Loading note…** (P, `empty-loading-error.md`). No `<h1>`, no tick rows, no text box, no flag box: an empty box or unticked rows could be tapped and start a draft with the wrong state. | Not rendered | "Focus pending": the `<h1>` takes focus when it mounts. |
| **Did not load** (network, timeout, 5xx after one silent retry) | Fallback `<h1>` **Note** (the page name), then **The note did not load: no connection. Try again.** or **The note did not load: something went wrong. Try again.** (P, `microcopy.md` §4/§9 pattern) and a secondary **Try again** button that refetches only the failed request. Back link stays. | Not rendered | Focus to the fallback `<h1>`; the status region carries the message. |
| **Not found** (`404`: bad or old participant ID, another organisation) | Whole-page **Page not found** (`empty-loading-error.md`). | — | Focus to its `<h1>`. |
| **Not allowed to edit** (`403 note.not_editable`, stale link) | Whole-page **You cannot edit this note** · "Only the person who wrote it, or a manager, can edit it." · link **Read the note** (P, `empty-loading-error.md`). | — | Focus to its `<h1>`. |
| **Someone else's draft** (`409 note.in_progress_by_other` on load, from a stale link) | Identity block, then the sentence as a notice: **Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex P. can finish it.** (N; for a non-today date: "started the note for", D). No form. | — | Focus to `<h1>`. |
| **Signed out** (`401` at any point) | `session-timeout.md` shows sign-in in place: the route stays mounted but `hidden`, all on-screen text stays in memory, autosave pauses. | — | See Interactions, "Session". |

### Kinds of note (set when the form loads)

| State | Identity and notice | Indicator | Other differences |
|---|---|---|---|
| **New** (no note yet, design "New: nothing saved yet") | Name, date. | Empty (element present). | Lists from the live template, all unticked. Groups picked as on the participant's most recent submitted note (D46), with the copied line; nothing picked if there is none. No More actions. First change creates the draft (3.3) with every on-screen pick, pre-picks included. |
| **Draft** (own, today) | Name, date. | **Draft · saved 9:42 am** (V) until the next change. | The draft's snapshot groups and saved picks; the copied line if `picksCopiedFrom` is set. More actions shown. |
| **Draft from an earlier day** | Notice (N): **This draft is for Wednesday 30 September 2026. Submitting it now keeps that date.** Date line shows the draft's own date. | **Draft · saved Wed 30 Sep 2026, 9:42 am** (N, `stamp`) when saved on another Melbourne day. | As Draft. Confirmation title **Submit the note for** (P/D). |
| **Manager's past-day note** (new or draft) | Notice (N): **Past-day note for Monday 28 September 2026, written on Thursday 1 October 2026.** ("written on" is the Melbourne date of `Note.CreatedAtUtc`, or server today before the first save.) | As New or Draft. | Starting picks from the most recent submitted note **before** the note date. Back link **Participant** or **Past notes** (D). Confirmation title **Submit the note for**. |
| **Editing a submitted note** | Caption **Editing submitted note (version 2)** (V) inside the `<h1>` (the number is the version being edited; open question). Past-day notice still shown for a past-day note. | Never says "Draft". Reopened pending edit: **Saved 9:42 am**. | **Save changes** / **Cancel**; no More actions; the version's snapshot groups (including any archived since, A3) and its picks; no copied line; empty lists use "No goals set" / "No common items set". |

### Common items and groups (D44–D47; behaviour owned by group-picker.md)

| State | What shows | Focus and announcements |
|---|---|---|
| **Picks copied from the last note** (new note or draft) | Every note; the picker with the copied groups ticked; the copied line naming the source note's date; those groups' items below, **all unticked**. | Nothing announced on load. Tabbing into the picker reads the legend, hint and copied line once. |
| **Nothing to copy** (no earlier submitted note, it picked nothing, or none of its groups is still offered: archived or without items) | Every note; the picker with nothing ticked; no copied line; no group lists. | — |
| **A group picked** | Its heading and unticked items appear below the picker, in the manager's order. No scroll. | Focus stays on the tick box; native "checked, expanded". |
| **A group unpicked that had ticks** | Its list is hidden and **its ticks are cleared**: no confirmation, no undo, no message (A46; hidden ticks are never kept or sent, like the hidden flag reason). Picking it again shows its items unticked. | Focus stays; native "not checked, collapsed". |
| **No group picked at Submit** | Allowed. Nothing about groups is validated; Guided notes is still required (A8, A45). | — |
| **Only Every note has items** (no other group set up, or none with items) | Every note with its heading; no picker. | — |
| **Every note has no items** | Every note not rendered; the picker comes first in the section. | — |
| **A group with no items** | Not offered in the picker (A45): nothing to tick, and it would print an empty heading. | — |
| **No common items at all** | `<h2>2. Common items</h2>` and **No common items set up.** (V); edit mode **No common items set** (D, 11.3). No fieldset. | — |
| **Many groups** | All listed vertically in the manager's order. No cap, "Show more", search, "Select all" or "None". | — |
| **Lists changed before the first save** (`409 note.lists_changed`) | Groups reload; picks re-applied by group ID (an archived group drops out); ticks re-applied by item ID and kept only for Every note and picked groups, so an item moved into an unpicked group loses its tick; the lists-changed notice above **1. Goals**. | As for any lists change: focus stays, unless the focused box was removed, then the notice. |
| **Editing a submitted note** | The version's snapshot groups and picks. Picks can be changed; Save changes records them in the new version. No copied line. | — |
| **Save failed / dead-end refusal** | Picks and ticks stay on screen; nothing is unticked or unpicked. | As for the form. |
| **Loading** | Nothing rendered until `GET {base}/draft` returns: no placeholder rows that could be tapped. | — |

### Saving

| State | Indicator (`#save-status`) | Banner | Submit | Announced |
|---|---|---|---|---|
| **Empty** (new, no change yet) | (empty) | — | Available (a press runs the checks, so empty Guided notes shows its error) | — |
| **Loaded** (reopened draft or pending edit) | **Draft · saved 9:42 am** / **Saved 9:42 am** | — | Available | Nothing (present at mount) |
| **Saving** (an unsaved change exists, or a save is in flight) | **Saving…** (V) | — | Unavailable | Polite, once per burst |
| **Saved** | **Saved 9:42 am** (V; server `savedAtUtc` in Melbourne time; `stamp` adds the date only if not Melbourne today) | — | Available | Polite |
| **Not saved** (after the first retry also fails, about 1 s, or at once if the browser reports offline) | **Not saved: no connection. Keep this page open; retrying.** (V), full-width row with icon | The indicator is the banner | Unavailable | Polite, once per failure episode |
| **Recovered** | **Saved 9:44 am** | Gone | Available | Polite |

The middle dot in "Draft · saved" is `<span aria-hidden="true"> · </span>` plus a visually hidden comma
(`microcopy.md` §7). Times have a non-breaking space before am/pm; exactly 12:00 shows "midday" or "midnight".

### Conflicts and refusals (autosave stops)

| State | Indicator | Banner (row 2 of the top bar, one at a time) | Submit | Announced / focus |
|---|---|---|---|---|
| **Changed on another device or tab** (`409 draft.taken_over`) | **Not saved** (P) | (V) **This note was changed on another device or tab.** with **Keep the text on this screen** (first, primary style) and **Load the other version** (secondary) | Unavailable | Sentence announced once, assertive. Focus does not move. Typing still works and is kept. |
| **Started by someone else** (first save, `409 note.in_progress_by_other`) | **Not saved** | (N) **Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex P. can finish it.** (D for non-today dates: "started the note for") | Unavailable | Assertive, once. Text stays on screen, selectable and copyable. Leaving is not blocked. |
| **After midnight** (first save, worker, `422 note.past_date_manager_only`) | **Not saved** | (V) **It's now after midnight, so this note can't be started for yesterday. Ask a manager to record it.** | Unavailable | As above. |
| **Participant archived** (first save, `422 note.participant_archived`) | **Not saved** | (P) **Jane Citizen is archived, so this note cannot be started. Ask a manager.** | Unavailable | As above. |
| **Lists changed before the first save** (`409 note.lists_changed`, including a group change) | **Saving…** then **Saved …** (the form re-saves with the new `listsVersion`, its picks and its surviving ticks) | Lists-changed notice above Goals (V) **The goal or common-item list was just changed. Please check your ticks.** Picks re-applied by group ID, ticks by item ID (see Common items and groups). | As for Saving / Saved | Polite through the notice's own `role="status"`. Focus stays, unless the focused tick box was removed: then focus moves to the notice (`tabindex="-1"`). |
| **Version conflict** (edit mode, Save changes, `409 note.version_conflict`) | **Not saved** until Start again succeeds | Version-conflict notice in page flow (component 6) | Save changes unavailable until Start again succeeds | Focus moves to the notice (the person's own press caused it). |

### Validation (only after Submit or Save changes is pressed)

| State | Inline (between label and field) | Summary link | Field |
|---|---|---|---|
| **Guided notes empty or only spaces**, prompts exist | **Write your notes in the Guided notes box. The grey text is only a guide.** (P) | Same text | `aria-invalid="true"`, `aria-describedby="guided-notes-error"`, 3 px error border, 4 px error bar on the section |
| **Guided notes empty**, no prompts set | **Write your notes in the Guided notes box** (P) | Same | Same |
| **Guided notes over 20,000** | **Guided notes must be 20,000 characters or less** (P) | Same | Same; the counter already reads "You have 312 characters too many" |
| **Flag ticked, Reason empty or only spaces** | **Enter a reason for the flag** (P) | Same | `aria-invalid`, `aria-describedby="flag-reason-error flag-reason-hint"`, bar turns error colour |
| **Reason over 200** | **Reason must be 200 characters or less** (P) | Same | Same; the count already reads "You have 12 characters too many" |

All have a visually hidden **Error: ** prefix and no "please", "sorry" or "invalid". An error clears on the keystroke
that fixes it (message, `aria-invalid`, summary item; the summary and the title prefix go when empty). Unticking the
flag removes the Reason error everywhere. Ticks never have an error (A4).

### Over the limit while typing (no error yet)

| Field | What shows | Autosave |
|---|---|---|
| Guided notes ≥ 18,000 | Counter **You have 1,950 characters remaining**; over 20,000 **You have 312 characters too many** in bold error colour with an error-colour box border. | Keeps saving; the saved copy is capped at 20,000 characters (see Conflicts resolved). |
| Reason > 200 | **You have 12 characters too many**, bold, error colour. | Keeps saving; the saved copy is capped at 200 characters. |

Nothing is ever cut on screen; the error (and the Submit block) arrives only when Submit is pressed.

### Flag

| State | What shows |
|---|---|
| Unticked | Block `hidden`; `aria-expanded="false"`; autosave sends `isFlagged: false, flagReason: null`. |
| Ticked, empty | Block visible, count **You can enter up to 200 characters**, no error. |
| Ticked, typing | Count updates on every input; spoken count 1 s after typing stops. |
| Re-ticked | Earlier text returns; no error until the next Submit. |
| Loaded flagged (draft or edit) | Block visible from the first render; no count announcement. |

### Confirmation dialog

| State | What shows | Focus |
|---|---|---|
| **Open** | Title, date, flag line, two buttons | Title `<h2>` |
| **Busy** (POST sent) | Both buttons `aria-disabled`, labels unchanged; after 1 s the status line **Submitting…** (P); Escape and Back blocked where the browser allows | Unchanged |
| **Failed, can retry** (network, timeout, 5xx) | Alert region: **Not submitted: no connection. Your draft is saved. Try again.** or **Not submitted: something went wrong. Your draft is saved. Try again.** (P, canonical `microcopy.md` §9) | Stays on the confirm button |
| **No longer possible** (`409 note.version_conflict`: submitted from another device or tab) | Alert region (V reused): **This note was changed on another device or tab.** Confirm button removed. | **Go back** |
| **Field errors** (`422`) | Dialog closes; the form shows the inline errors and summary | Summary |
| **Session ended** (`401`) | Dialog closes first, then sign-in in place | Sign-in heading |

### Discard dialog

| State | What shows |
|---|---|
| Busy | **Discarding…** (P) after 1 s |
| Failed | **Not discarded: no connection. Try again.** / **Not discarded: something went wrong. Try again.** (P, pattern) |
| No longer possible (`409 note.not_a_draft`) | **This draft has already been submitted or discarded.** (P); confirm removed; focus to Go back; Go back goes to Today |

### Edit-mode actions

| State | What shows |
|---|---|
| Save changes busy | **Saving changes…** (P) in the button after 400 ms |
| Save changes failed | Alert above the buttons: **Changes not saved: no connection. Try again.** / **Changes not saved: something went wrong. Try again.** (P) |
| Cancel busy / failed | **Cancelling…** (P) after 400 ms / **Not cancelled: no connection. Try again.** (P) |

### Not used on this screen

- **Disabled** (`disabled` attribute): never, on any control. **Read-only** fields: never on the form (read-only notes
  use the read view). **Toasts**: none. **Success message on this screen**: none; success shows on Today.

---

## Interactions and focus

### Arrival

1. Tap from Today (or another in-app link). The page renders nothing in the identity area until both queries succeed,
   then the whole form renders at once, scrolled to the top.
2. The shell's route rule focuses the `<h1>` once per navigation (`tabIndex={-1}`, ring only under `:focus-visible`), so
   a screen reader says "Jane Citizen, heading level 1": the spoken identity check. Not on a first full page load, not
   on re-render or refetch, and not if the person has already moved focus. [Research] Gatsby/Fable 2019, n=5.
3. No autofocus on a tick box or the text box: it would hide the name and open the keyboard over the lists. Nothing is
   saved by opening, scrolling, focusing or tapping into the box (3.3).

### Ticking a goal, common item or the flag

- The tick shows at once from local state; the change event calls `markChanged()`. No animation, no per-tick
  announcement beyond the native "checked". A failed save never unticks anything.
- Ticking **Flag for manager** shows Reason instantly; **focus stays on the tick box** (moving it would be a change of
  context on input, SC 3.2.2); Reason is the next Tab stop. No automatic scrolling.

### Ticking a group in "Which of these happened?" (group-picker.md)

- **Tick a group:** the tick shows at once; that group's wrapper loses `hidden`, so its heading and unticked items
  appear below the picker, in the manager's order. `markChanged()`; on a new note the first change creates the draft
  with every on-screen pick, pre-picks included (3.3). **Focus stays on the tick box and nothing scrolls** (SC 3.2.2;
  revealing content is not a change of context). Heard natively: "checked, expanded". No live-region message.
- **Untick a group:** its list gets `hidden` and its item ticks are removed from form state at once (A46). **No
  confirmation, no undo, no message**: the loss is one group's ticks, re-ticked in a few taps, and correcting a copied
  pick is routine; a dialog here would be dismissed by habit (group-picker.md). Heard: "not checked, collapsed".
- **Tick it again:** its items show again, unticked.
- **Copied picks (D46, A44):** opening a new note saves nothing. The copied picks and the copied line are shown but
  not saved until the first change creates the draft, which then stores the picks and `picksCopiedFrom`. Item ticks
  are never copied.

### Typing and dictation

- Listen to `input` only; never `preventDefault()` on keys (IME, dictation and autocorrect depend on it). Enter adds a
  new line in both boxes and never submits.
- The prompts disappear at the first character (D34) and return if the box is emptied.
- Desktop speech tools: if M6 testing shows Dragon or Windows voice typing still change the value without `input`
  events, add GOV.UK's 1 s value poll while the box has focus. Blur and page-hidden saves read the DOM value anyway.

### Autosave (one engine, `useAutosave`)

| Trigger | What happens |
|---|---|
| Any change | Save 2 s after the last change (V), with a 5 s maximum wait from the first unsaved change, so non-stop typing or dictation still saves (§14 M2: lose "no more than the last few seconds") [Opinion on 5 s]. |
| Blur of any field | Save now (V). |
| Page becomes hidden | Send the latest state now; `fetch(..., { keepalive: true })` when the body is under 60,000 bytes (V, §5.6). |
| Page becomes visible with an unsaved change | Save now. |
| Paused | While signed out, while a blocking banner shows, and from the moment Submit's POST, Discard or Cancel starts. |

- One regular save in flight; later changes are combined into the next save, whose body is read when it is sent
  (retries never carry old text). "Saved" shows only when the server acknowledged a save carrying the newest change
  number (invariant I5).
- `{clientId, seq}` live in a module-level `Map` per note per page load, never per component mount (otherwise the
  server ignores restarted `seq` values as stale and still returns 200).
- Retry forever while the form is open for network errors, a 10 s timeout, 408, 429 (honour `Retry-After`) and 5xx:
  1, 2, 4, 8 s, then every 10 s; TanStack `networkMode: 'online'` pauses on `offline`. Never retry 401, 403, 409, 412,
  422.
- The payload sends `flagReason: null` when unticked or blank, and caps `narrative` at 20,000 and `flagReason` at 200
  characters (never splitting a surrogate pair). The screen always keeps the full text.
- The payload also sends `pickedGroupIds` (every on-screen pick) and `tickedCommonItemIds` filtered to Every note and
  the picked groups' items. Picks are part of the working copy everywhere ticks are: autosave, page-hidden saves,
  takeover (Keep / Load), the lists-changed reload, signing in again in place, and Cancel in edit mode.
- The draft query uses `staleTime: Infinity`, `refetchOnWindowFocus: false`, `refetchOnReconnect: false`; the form
  copies it into its own state once.

### Pressing Submit note (or Save changes)

1. **Checks first** (client, on trimmed values): Guided notes not blank and ≤ 20,000; if flagged, Reason not blank and
   ≤ 200. On failure: render inline errors and the summary in one update, prefix the title with "Error: ", move focus
   to the summary (on **every** failed press), open nothing, send nothing.
2. **If the indicator shows Not saved or a blocking banner shows:** do nothing more; write the indicator or banner text
   into the form's announcer (polite) so the press is not silent.
3. **Otherwise** (Empty with valid content cannot happen; Loaded, Saving or Saved): the button goes busy and a **check
   save** runs: one `PUT` now, even if nothing changed, after any save in flight settles. It flushes the last change and
   detects a takeover by another tab or device (`POST …/versions` carries no `clientId`). The indicator shows
   "Saving…". Repeat presses are ignored.
4. **Check save succeeds:** Submit opens the confirmation; Save changes sends `POST {base}/versions` at once (design
   specifies no confirmation for it). **Check save fails or is blocked:** nothing opens; the indicator or banner says
   why; the press is never remembered and acted on later.

### In the confirmation

- On open: create one `Idempotency-Key` (`crypto.randomUUID()`), call `showModal()`, set `autofocus` on the title via
  its ref and call `focus()` (React's `autoFocus` does nothing in a closed `<dialog>`, facebook/react#23301). Focus is
  on the title, so a fast double tap or held Enter cannot confirm before the name is seen; Tab reaches the confirm
  button, then Go back. [Standard] APG dialog "static element at the top"; [Convention] Nielsen "no default answer".
- **Submit note for Jane Citizen:** stop autosave (clear the timer, stop page-hidden saves, wait for any save in flight),
  then `POST {base}/versions` with the key. Busy as in States. Errors keep the dialog open with the same key for
  retries. "Submitted" is never shown before `201` (or `200` on an idempotent retry).
- **Go back**, Escape or Android Back (Chrome 120+ close request): close; focus returns natively to **Submit note**;
  autosave resumes; nothing changed. Blocked while busy (`cancel` → `preventDefault()`); if a browser closes it anyway,
  the request still finishes and its result is handled on the page.
- No URL change and no history entry while the dialog is open.

### After a successful submit

1. Write **Note for Jane Citizen submitted** (V) into the in-memory message store (never router state, URL or
   storage: `history.state` may be written to disk, D22).
2. Invalidate, then leave: `queryClient.removeQueries({ queryKey: ['today'] })` and
   `queryClient.removeQueries({ queryKey: ['me', 'drafts'], exact: true })` (no client-built row: Today shows only
   what the server returns, today.md Conflicts #2); when the `201` carries a `flagStatus`, also
   `invalidateQueries({ queryKey: ['me'], exact: true })` so the Flagged badge updates. Close the dialog, then navigate
   to Today: pop if Today is the previous entry, otherwise replace (so browser Back never reopens the finished form).
3. Today renders the success banner before its `<h1>` with `data-route-focus`; the shell focuses it on every arrival,
   including this pop (app-shell.md route-change rule); it has no live role (focus does the announcing)
   (`status-messages.md`).

### Edit mode: Save changes and Cancel

- **Save changes:** flow above, then `POST …/versions` (one `Idempotency-Key` per attempt, reused for retries while the
  content is unchanged; any edit after a failed attempt makes a new key). On `201`: invalidate (below), then replace
  the route with the read view; the shell focuses its `<h1>`; the read view shows the new version and the Edited line.
  No success banner.
- **Cancel:** no confirmation (design 3.5, A10). Stop autosave, wait for the save in flight, `DELETE {base}/draft`,
  then invalidate (below), replace the route with the read view and focus its `<h1>`. On failure, stay, show the alert,
  resume autosave.
- **Invalidations after Save changes or Cancel** (one prefix for every note query, participant-notes.md):
  `['notes', participantId, noteDate]` (prefix: the note, its version list and each version), the participant's
  Past notes list `['pastNotes', participantId]` (its Edited tag), and `['reviews']` (prefix: both Flagged lists,
  because an edit can re-queue or unflag a note, A15). After **Save changes** also `['me']` (exact), so the badge
  follows a re-queue or unflag. Nothing is updated by hand from the response.
- **Version conflict:** focus moves to the notice (scrolled into view). **Start again from the latest version** sends
  the `PUT` with the new `baseVersion`; the editor's picks, ticks, text and flag stay on screen; the caption becomes
  "Editing submitted note (version 3)"; the `<details>` closes but stays until Save changes succeeds; focus goes to the
  `<h1>`.

### Discard draft

1. **More actions** toggles the panel (`aria-expanded`). **Discard draft** opens the Discard dialog with focus on its
   title.
2. Confirm: stop autosave and wait for any save in flight, then `POST {base}/discard`. On `204`: close, replace the
   route with Today; because it is a replace, the shell focuses Today's `<h1>`, never the old row (app-shell.md route-change
   rule; no success banner; the row simply loses its status). Go back, Escape or
   Back: close, focus returns to **More actions** (the panel is closed), autosave resumes.

### Changed on another device or tab

- **Keep the text on this screen:** forget this note's `{clientId, seq}`, send everything on screen now (including
  anything typed while the banner showed) with a new `clientId` and `seq = 1`, remove the banner, return focus to the
  element that had it when the conflict appeared (or the `<h1>` if it has gone).
- **Load the other version:** `GET {base}/draft`, replace picks, ticks, text (`replaceText`), flag and reason; new
  `clientId`; remove the banner; indicator **Saved [the other version's time]**; focus to the `<h1>`, so the person
  reviews from the top without the keyboard popping up.

### Leaving the form

| How | What happens |
|---|---|
| Back link, Past notes, app nav, browser Back (in-app) | React Router `useBlocker` (needs a data router). If nothing is unsaved: leave. If a save is waiting or in flight: send it now and leave when it is acknowledged (the person sees "Saving…" for a moment). If the save has failed: stay, and announce the indicator text politely. While the takeover banner shows: stay (its two buttons are the way out). **No new dialog.** |
| After a dead end (started by someone else, after midnight, archived) | Never blocked: the text can never be saved here. |
| Reload or close the tab | `beforeunload` is attached only while a change is unsaved or the takeover banner shows, and removed as soon as the change saves. The browser shows its own wording. The page-hidden save is the real protection on phones. |
| Sign out from the Account menu while unsaved | Owned by `app-shell-nav.md`: sign-out waits for the save; if it fails, **Not signed out: your note is not saved yet. Keep this page open; retrying.** (P, canonical `microcopy.md` §9). |

After leaving, invalidate the `today` and `me/drafts` queries so Today's "Draft · You · saved 9:42 am" matches.

### Session

- At 28:00 with a change waiting, the session clock asks autosave to send it now instead of warning; the warning opens
  only if nothing is accepted.
- If the warning is due while the Submit or Discard dialog is open, that dialog is closed as Go back (nothing
  committed) and the More actions panel is closed; after **Stay signed in**, focus returns to **Submit note** (not the
  confirm button), so the person sees the name again on the next press.
- Signed out (30:00 or any `401`): the route stays mounted and `hidden`, and so does the top bar in the shell's bar
  slot (no "‹ Today" or "Saved 9:42 am" over the sign-in view); autosave pauses; after the **same** person signs
  in, the form is unhidden, autosave retries with the same `clientId` and next `seq` (no false takeover banner) and
  focus goes to the `<h1>`. A **different** person: full reload to Today; the previous person's unsaved text is
  dropped. Submit, Save changes, Discard and Cancel are never replayed.

### Focus order (Tab)

Skip link → app header (Account, nav links) → back link → *(compact name and indicator are not focusable)* → banner
buttons (Keep, Load) when shown → `<main>`: error summary links (when shown) → **Past notes** → version-conflict
**Start again** (edit, when shown) → each goal box → each Every note box → each group box under "Which of these
happened?" → each box in each picked group (the manager's order; unpicked groups are `hidden`, so Tab skips them) →
Guided notes → Flag for manager → Reason
(only when ticked) → **Submit note** (or **Save changes**, **Cancel**) → **More actions** → **Discard draft** (when the
panel is open). Programmatic-only targets (`tabIndex={-1}`): the `<h1>`, the error summary, the lists-changed notice,
the version-conflict notice. No positive `tabindex`. DOM order equals visual order.

### Where focus goes

| After | Focus |
|---|---|
| Arrival (in-app) | `<h1>` (name and note date) |
| Failed Submit / Save changes (validation) | Error summary |
| Summary link | The field, after its label is scrolled into view |
| Submit pressed, checks and check save pass | Confirmation title |
| Confirmation Go back / Escape / Back | **Submit note** |
| Confirmation success | Today's success banner |
| Confirmation "no longer possible" | **Go back** |
| Discard Go back | **More actions** |
| Discard success | Today's `<h1>` |
| Save changes / Cancel success | Read view `<h1>` |
| Version conflict | The notice; after Start again, the `<h1>` |
| Keep the text on this screen | The element focused before the conflict (else `<h1>`) |
| Load the other version | `<h1>` |
| Tick or untick a group under "Which of these happened?" | Stays on that tick box; nothing scrolls |
| Focused tick box removed by a lists reload | The lists-changed notice |
| Session warning closed | The element focused before it |
| Signed back in (same person) | `<h1>` |

### Announcements

| Moment | Heard (wording varies by screen reader; untested) | Mechanism |
|---|---|---|
| Arrive | "Jane Citizen Thursday 1 October 2026, heading level 1" (edit: "Editing submitted note (version 2) Jane Citizen Thursday 1 October 2026, heading level 1") | Focus |
| Tab into Goals | "1. Goals, grouping, Ticked means reached. Makes own breakfast, check box, not checked" | Native |
| Tab into Every note | "Every note, grouping, Ticked means done. Medication prompted, check box, not checked" | Native |
| Tab into the group picker | "Which of these happened?, grouping, Tick all that happened. Their items show below. These ticks are copied from the note for Wednesday 30 September 2026. Untick any that did not happen. Community outing, check box, checked, expanded" (the copied line only when it shows) | Native + `aria-describedby`, `aria-expanded` |
| Tick / untick a group | "checked, expanded" / "not checked, collapsed" | Native + `aria-expanded`; no live region |
| Typing | "Saving…" once per burst, then "Saved 9:42 am" | `#save-status`, polite |
| Save changes or Cancel slower than 400 ms | "Saving changes…" / "Cancelling…" | Page status region (component 16, polite) |
| Save fails | "Not saved: no connection. Keep this page open; retrying." once | `#save-status`, polite |
| Takeover / dead end | The banner sentence, once | Form announcer, assertive |
| Lists changed | The notice sentence | Its own `role="status"`, polite |
| Flag ticked | "checked, expanded" | Native + `aria-expanded` |
| Count passes the threshold / pause in Reason | "You have 1,950 characters remaining" | Field's polite region, 1 s after typing stops |
| Failed Submit | "There is a problem, heading level 2", the list | Focus on the summary |
| Press on an unavailable Submit | "Submit note, dimmed, button, Saving…" (description), and the Not saved text if the save failed | `aria-describedby`; form announcer |
| Confirmation opens | "Submit today's note for Jane Citizen, alert dialog, Thursday 1 October 2026, Flagged for manager: No", then the heading | Dialog name, description, focus |
| Submitting slowly | "Submitting…" | Dialog `role="status"` |
| Submit failed | The error sentence | Dialog `role="alert"` |
| Back on Today | "Note for Jane Citizen submitted" | Focus on the banner |

Never: a live region on the name or compact name, an announcement per tick, `role="alert"` on routine saves, or a
conditionally mounted live region (`{x && <p role="status">}` is silent in NVDA and Orca).

---

## Accessibility checklist

**Structure**
- [ ] One `<h1>` (caption + name + note date). `<h2>`: "1. Goals" (inside its legend), "2. Common items" (a plain
  heading, not in a legend), "3. Guided notes" (wrapping the label), "There is a problem" (summary), the
  version-conflict line, and the dialog titles. `<h3>` inside legends under "2. Common items": "Every note", "Which of
  these happened?" and each picked group's name, so heading navigation reaches every group. No heading inside
  notices. SC 1.3.1, 2.4.6.
- [ ] Group picker: no nested fieldsets (H71); each group tick box has `aria-controls` and `aria-expanded`; unpicked
  groups are `hidden` (out of view, Tab order and the accessibility tree); the copied line is in the picker's
  `aria-describedby`; no live region per pick (group-picker.md).
- [ ] Landmarks: `<header>` (banner), `<nav aria-label="Main">`, `<main id="main-content" tabindex="-1">`. The top bar
  sits in the shell's before-main bar slot, between the header and main (GOV.UK back-link placement; app-shell.md
  3a); axe's best-practice `region` rule may flag it, which is not a WCAG failure.
- [ ] `<html lang="en-AU">` (SC 3.1.1). Names carry `translate="no"`.
- [ ] Every date and time in `<time dateTime>`.

**Labels and names**
- [ ] Every control has a visible label that starts its accessible name; no `aria-label` replacing visible words
  (SC 2.5.3): goal wording, "Flag for manager", "Reason", "3. Guided notes", "Submit note for Jane Citizen".
- [ ] Tick lists in `<fieldset>`/`<legend>` with the hint linked by `aria-describedby` (H71); no empty fieldsets.
- [ ] Reason hint linked; errors linked by `aria-describedby`; `aria-invalid` only after a failed press.
- [ ] `aria-required="true"` on Guided notes and Reason; no `required`; the form is `noValidate`.

**Keyboard and focus**
- [ ] All controls native; Tab order as listed; Space toggles boxes; Enter never submits from a text box.
- [ ] Focus ring: 3 px near-black outline, 2 px offset, `:focus-visible`, no transition; `Highlight` in forced colours
  (SC 2.4.7, 1.4.11, also 2.4.13 AAA).
- [ ] `scroll-padding-block-start` keeps every focused element clear of the sticky bar; the bar is static on short
  viewports (SC 2.4.11, C43, F110).
- [ ] Dialogs: focus to title on open, returns on close, Escape = Go back (except while busy), no keyboard trap beyond
  the modal's own (SC 2.1.2).
- [ ] No `disabled` anywhere; unavailable and busy use `aria-disabled` with guarded handlers.

**Visual**
- [ ] Text ≥ 4.5:1 (the palette gives ≥ 7:1 except the placeholder at 6.1:1); box borders, tick, field borders, focus
  ring ≥ 3:1 (SC 1.4.3, 1.4.11).
- [ ] Nothing by colour alone: errors have words, a bar and a border; over-limit counts are bold; unavailable buttons are
  dashed; Flagged is "Yes"/"No" in words (SC 1.4.1).
- [ ] 200% text and 320 px width: everything wraps, nothing truncated except the compact name, no sideways scroll
  (SC 1.4.4, 1.4.10); no fixed heights (SC 1.4.12).
- [ ] Targets: tick rows 56 px, buttons 48 px, links and back link ≥ 44 × 44 px (A32; SC 2.5.8).
- [ ] No motion; base CSS keeps the reduced-motion safety reset (near-zero durations, not `animation: none`).
- [ ] Forced colours: tick (borders), bars and banner edges (borders), unavailable (`GrayText`), focus (`Highlight`)
  all visible.

**Timing and status**
- [ ] Nothing auto-dismisses; retries have no deadline (SC 2.2.1). Session warning per `session-timeout.md`.
- [ ] Status messages in live regions that exist before their text changes (SC 4.1.3, ARIA22).
- [ ] Ticking, typing and autosave never move focus or change context (SC 3.2.2).

**Errors**
- [ ] Errors identified and described in text with a fix (SC 3.3.1, 3.3.3); same text inline and in the summary.
- [ ] Typed text is never cleared, by errors, conflicts, sign-out or React form resets (SC 3.3.7).
- [ ] Submit is checked and confirmed; edits are versioned (SC 3.3.4).

**WCAG 2.2 AA criteria this screen must pass:** 1.1.1, 1.3.1, 1.3.2, 1.3.4, 1.4.1, 1.4.3, 1.4.4, 1.4.10, 1.4.11,
1.4.12, 1.4.13, 2.1.1, 2.1.2, 2.2.1, 2.2.2, 2.4.1, 2.4.2, 2.4.3, 2.4.4, 2.4.6, 2.4.7, 2.4.11, 2.5.3, 2.5.8, 3.1.1, 3.2.2,
3.2.3, 3.2.4, 3.3.1, 3.3.2, 3.3.3, 3.3.4, 3.3.7, 4.1.2, 4.1.3. Known gap: conditional reveals (Flag for manager's
Reason and each picked group's items) are listed by GOV.UK and NHS as a 4.1.2 risk (see Tensions).

**Manual test passes (M6):** NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Android Chrome; Voice Control "Tap
Makes own breakfast" and "Tap Community outing"; the picker's legend, hint and copied line spoken on entry;
"expanded/collapsed" on a group tick box (TalkBack support unverified); unpicked groups skipped by Tab and by swipe;
Windows contrast theme; 200% and 400% zoom; landscape phone; outdoor daylight check; the sticky bar with the iOS
keyboard open on Guided notes; Chrome page translation leaves names unchanged.

---

## Acceptance criteria

**Identity and wrong-participant protection**
1. With the network throttled, opening participant A's form, going back, then opening participant B's: A's name never
   appears in B's DOM at any point (Playwright).
2. No `<h1>`, tick box, text box or flag box is in the DOM until both the participant and the draft have loaded.
3. The name appears as stored (no CSS upper-casing), wraps at 320 px and 200% text, and is never truncated in the
   `<h1>`, the dialog title or the confirm button.
4. The confirmation shows the full name in the title and on the confirm button, the note date as "Thursday 1 October
   2026", and "Flagged for manager: Yes/No" matching the values being submitted.
5. The title reads "Submit today's note for" only when the note date equals the server's Melbourne today; otherwise
   "Submit the note for".
6. Page title is "Grow2Notes – Note" (or "Error: Grow2Notes – Note"); no URL, title, router state or storage ever holds
   a name or note text, and `history.state` holds no back object (the back destination is the shell's in-memory
   opened-from record).

**Autosave**
7. Nothing is sent on opening, scrolling or focusing; the first tick or keystroke sends a `PUT` within about 2 s.
8. Continuous typing for 12 s sends at least two saves (5 s maximum wait).
9. Switching apps (page hidden) sends the latest state immediately with `keepalive` when the body is under 60,000
   bytes.
10. "Saved 9:42 am" appears only after the server acknowledges the save carrying the newest change; the time is the
    server's `savedAtUtc` in Melbourne time.
11. With the network offline (Playwright `setOffline`), the indicator shows the exact V string within about 1 s, Submit
    is `aria-disabled`, and when back online it recovers to "Saved …" without any action.
12. A save whose response is aborted and retried shows "Saved" and raises no takeover banner (M2).
13. Leaving the form and returning in the same page load does not restart `seq` at 1 (no stale-but-200 saves).
14. Browser developer tools show no note content in localStorage, sessionStorage, IndexedDB or Cache Storage (M2).

**Conflicts and refusals**
15. Two pages on one draft: the older page's next save shows the takeover banner with both buttons; **Keep** re-sends
    the on-screen picks, ticks and text and wins; **Load** replaces picks, ticks, text, flag and reason and focuses
    the `<h1>`.
16. Nothing on screen is ever replaced except by **Load the other version**.
17. A lists change before the first save (including a group added, renamed, archived or restored, or an item moved
    between groups) reloads the lists, keeps the text, re-applies surviving picks by group ID and surviving ticks by
    item ID (dropping a tick on an item now in an unpicked group), and shows the V sentence above Goals until the
    person leaves.
18. The after-midnight, started-by-someone-else and archived refusals each show their sentence, keep all text on
    screen and copyable, keep Submit unavailable, and do not block leaving.
19. Save changes after another editor's version shows the conflict notice with the newer version read-only and focuses
    it; Start again keeps the editor's ticks, text and flag.

**Validation and Submit**
20. Pressing Submit with an empty (or spaces-only) Guided notes box shows the inline error above the box, the summary
    focused at the top, and "Error: " in the title; no dialog opens.
21. Every failed press re-focuses the summary; fixing a field clears its error on that keystroke.
22. Typing 20,050 characters (or pasting them) keeps all 20,050 on screen, shows "You have 50 characters too many",
    keeps autosaving, and blocks Submit with "Guided notes must be 20,000 characters or less".
23. Flag ticked with no reason gives "Enter a reason for the flag"; unticking removes the error from the field and the
    summary; re-ticking restores the typed reason.
24. While unticked, autosave and submit send `flagReason: null`.
25. Submit is never `disabled`; while unsaved it is `aria-disabled`, focusable, and described by the save indicator.
    Pressing it during "Saving…" opens the confirmation once the check save succeeds; pressing it during "Not saved"
    opens nothing and announces the indicator text.
26. Enter in any field never submits; implicit submission is guarded by the same handler.
27. Opening the confirmation focuses the title; pressing Enter twice quickly after Submit does not submit the note.
28. A network failure in the dialog shows the canonical failure text, keeps the dialog open and reuses the same
    `Idempotency-Key` on retry; a lost response retried returns `200` and creates no second version.
29. A `422` from the POST closes the dialog and shows the field errors and summary.
30. After `201`, Today shows "Note for Jane Citizen submitted" before its `<h1>`, focused (also when the return was a
    pop), and the row reads Submitted once the fresh list arrives, never from a client-built row; reloading Today does
    not show the message again.
30a. After Save changes, the read view, the participant's Past notes row (Edited tag), both Flagged lists and the
    badge all show the new state without a reload (the invalidations under "Edit mode").
30b. Save changes and Cancel show their busy label only when the request takes more than 400 ms.

**Common items and groups (D44–D47)**
30c. Section 2 shows, in DOM and visual order: `<h2>` "2. Common items", "Ticked means done.", Every note (when it
    has items), the "Which of these happened?" picker (when any other group has items), then each picked group's list
    under its name, in the manager's order whatever order the groups were ticked in.
30d. Every note is never a row in the picker; a group with no items is never offered; with only Every note's items
    there is no picker; with no items at all the section is the heading and "No common items set up." (edit mode "No
    common items set") with no fieldset.
30e. A new note for a participant whose most recent submitted note picked Community outing and an archived group
    opens with Community outing ticked, the archived group absent, every item unticked, and the copied line naming
    that note's date in `dateLong`. A manager's past-day note copies from the most recent submitted note before its
    note date. With no earlier submitted note, nothing is ticked and there is no copied line.
30f. Opening a new note with copied picks sends nothing; the first change of any kind sends a `PUT` whose
    `pickedGroupIds` includes the copied picks and which stores `picksCopiedFrom`. Reloading the draft shows the same
    picks and the copied line; edit mode never shows the copied line.
30g. Ticking a group keeps focus on its tick box, scrolls nothing, sets `aria-expanded="true"` and shows its items
    unticked below the picker. Unticking a group that has ticked items opens no dialog, hides its items, and the next
    `PUT` contains neither the group's ID nor any of its item IDs; ticking it again shows its items unticked.
30h. Submit with no group ticked opens the confirmation (Guided notes filled in); the confirmation shows nothing about
    groups.
30i. In edit mode the form shows the version's groups (including one archived since) and its picks; changing picks and
    pressing Save changes creates a version whose read view shows the new picked groups.
30j. The picker strings contain no "group", "select", "check" or "today" (microcopy.md copy test).

**Edit mode, discard, leaving, session**
31. Edit mode shows the caption, "Save changes" and "Cancel", no More actions, and "Saved …" (never "Draft").
32. Cancel deletes the pending edit with no autosave landing afterwards, and lands on the read view with focus on its
    `<h1>`.
33. More actions appears only once a draft is saved; Discard asks "Discard the draft note for Jane Citizen?" with
    focus on the title; Go back returns focus to More actions; confirming lands on Today with the row's status gone.
34. Tapping the back link while a save is pending sends it and then navigates; while the save has failed it stays and
    announces "Not saved: no connection. Keep this page open; retrying."
35. `beforeunload` is attached only while a change is unsaved (or the takeover banner shows).
36. After an idle sign-out and signing back in as the same person, the draft and any unsaved text are intact and no
    takeover banner appears (M2); signing in as a different person reloads to Today.
37. A session warning due while the confirmation is open closes the confirmation first; nothing is submitted.

**Layout and accessibility**
38. At 375 px a worker can fill in and submit a note (M2); at 320 px and 200% text nothing scrolls sideways.
39. Every tick row is at least 56 px tall and toggles when the text is tapped; every other target is at least 44 px.
40. The top bar is sticky above 30rem viewport height and static below it; a focused tick row is never hidden under it.
41. axe (Playwright) reports no WCAG violations in each state: default, flag ticked, errors, Not saved, takeover,
    lists changed, edit mode, both dialogs, picks copied with the copied line, a group ticked, only Every note, no
    common items.
42. The placeholder colour is #626262 (or the token) with `opacity: 1`, checked by hand (axe may not check
    `::placeholder`).
43. The Vitest copy check (`microcopy.md` §8) finds no trace of the parent company's name (D42), no banned words and
    no US spellings in this screen's strings.

---

## Conflicts resolved

| # | Where the components disagreed | Chosen | Why |
|---|---|---|---|
| 1 | **Over-length text and autosave.** `form-validation.md`: check length while typing, do not autosave, indicator "Not saved: fix the error below." `autosave-status.md`: send nothing while over a limit. `conditional-reveal.md`: cap the autosave copy of Reason at 200 and block only at Submit. `guided-notes-textarea.md`: counter only, validate on Submit. | Cap **both** autosave payloads (Reason 200, narrative 20,000), keep autosaving, keep the full text on screen, show the GOV.UK counter while typing, and error only on Submit. "Not saved: fix the error below." is not used. | Keeps one validation timing (on Submit) [Research] Bargas-Avila 2007; keeps Submit's availability tied only to saving, as design 3.4 says; adds no new indicator state or string; nothing on screen is lost. The draft column is `nvarchar(200)` for Reason. |
| 2 | **Pressing Submit while "Saving…".** `primary-actions.md`: the press only flushes the save; tap again once saved. `autosave-status.md`: run a check save, open the confirmation when it succeeds. | `autosave-status.md`'s check save. | It closes a real data-loss gap (a stale tab submitting older text, because `POST …/versions` has no `clientId`), honours "disabled while unsaved" (nothing opens until the save is acknowledged), and avoids a confusing dead first tap for tired workers. A tap is never remembered past one check save. |
| 3 | **Dialog initial focus.** `primary-actions.md`: Go back. `confirm-dialog.md` and `note-identity-header.md`: the title. | The title (`<h2 tabIndex={-1}>`). | Both are APG options; the title makes the name the first thing heard and leaves no default button, so neither Enter nor a double tap commits [Standard] APG; [Convention] Nielsen, Apple. `confirm-dialog.md` owns dialogs. |
| 4 | **Busy delay.** `primary-actions.md`: 400 ms label swap. `confirm-dialog.md`: 1 s status line; labels unchanged. | The app's two-tier rule (primary-actions.md): page buttons (Save changes, Cancel) swap their label in-cell after **400 ms**; the dialogs keep their labels and show a status line after **1 s**. | One shared Button with one timing on every screen (SC 3.2.4); the dialog rule keeps the focused confirm button's name stable. Both numbers are convention, not measurement. (Editorial pass: this screen used 1 s for page buttons too.) |
| 5 | **Leaving after a failed save.** `note-identity-header.md`: a new guard dialog ("Your latest changes are not saved" / "Stay on this page" / "Leave without saving"). `autosave-status.md`: no dialog; stay and announce. | No new dialog. | Matches the design's own instruction "Keep this page open; retrying."; adds no screen element or copy (owner's simplicity rule). Reload and tab close still get the browser's `beforeunload` prompt. |
| 6 | **Name and date sizes.** `note-identity-header.md`: name 2rem/2.5rem, date in secondary grey. `foundations.md`: h1 28/32 px, date in full text colour. | `foundations.md`. | One type scale for the app; the date is part of the right-day check, so it is not greyed. 28 px is still the largest text on the screen. |
| 7 | **Breakpoint.** `note-identity-header.md`: 48em. Others: 40rem. | 40rem. | `foundations.md` sets one breakpoint for the app. |
| 8 | **Takeover banner placement.** `note-identity-header.md` and `status-messages.md`: banners in page flow under the date. `autosave-status.md`: in the sticky bar. | Blocking and failed-save banners in the sticky bar (row 2); past-day and earlier-day notices in page flow under the date. | The bar's owner is `autosave-status.md`; a blocking banner must be visible wherever the person is typing. Notices are static context and belong with the name and date. The bar is static on short viewports. |
| 9 | **Lists-changed announcement.** `autosave-status.md`: plain `<div>` plus the app-level polite announcer. `checkbox-list.md`: the notice itself is a `role="status"` present from mount. | `checkbox-list.md`. | One element, so the text is never duplicated in a hidden region; it meets ARIA22 directly. |
| 10 | **App-level announcer.** `autosave-status.md`: two app-level regions. `status-messages.md`: no global live region in the shell. | The note form mounts its own pair (component 16). | Satisfies both: the shell stays free of live regions, and the form still has pre-existing regions for banners and repeated failures. |
| 11 | **Flag tick-box size.** `conditional-reveal.md` CSS: 24 px box with `accent-color`. `checkbox-list.md`: 40 px `appearance: none` box (and `conditional-reveal.md`'s own text says reuse the same row). | The 40 px `.box` and 56 px row; the reveal bar is centred under the 40 px box. | Every tick box looks and behaves the same (SC 3.2.4); `accent-color` does not keep the tick legible on Chrome Android or older Safari [Standard] MDN compat data. |
| 12 | **`required` on Reason.** `conditional-reveal.md`: `required` (form is `noValidate`). `form-validation.md` and `guided-notes-textarea.md`: no `required`, use `aria-required`. | `aria-required="true"` on both text fields. | No browser bubbles or `:invalid` styling to fight; consistent with the form rules. |
| 13 | **Controlled vs uncontrolled Guided notes.** `form-validation.md`: "the note form is controlled". `guided-notes-textarea.md`: uncontrolled, read by ref. | Uncontrolled Guided notes; controlled ticks, flag and Reason. | A refetch, re-render or speech tool that skips `input` can never overwrite the main text; Reason must be kept by the parent while hidden. |
| 14 | **Live guide prompts.** `guided-notes-textarea.md`: prompts may update from later refetches. `autosave-status.md`: the draft query never refetches. | Prompts come from the draft response and do not change while the form is open. | Avoids an extra request and keeps the one draft query rule; a prompt edit reaches the next form opened. |
| 15 | **Empty Guided notes copy.** "Write your notes in the Guided notes box" (`guided-notes-textarea.md`) vs "…The grey text is only a guide." (`form-validation.md`). | Canonical `microcopy.md` §9: add the second sentence only when prompts exist. | Directly answers the placeholder-as-filled confusion [Research] NN/g placeholders. |
| 16 | **Flag reason empty copy.** "Enter a reason for flagging this note" vs "Enter a reason for the flag". | "Enter a reason for the flag" (`microcopy.md` §9). | Shorter; GOV.UK "Enter…" pattern. |
| 17 | **Too-long copy.** "or fewer" vs "or less"; a second sentence with the count. | "[Thing] must be [n] characters or less", one sentence (`microcopy.md` §9). | GOV.UK template; the counter already shows how many too many. |
| 18 | **Submit failure copy.** `confirm-dialog.md`: "The note was not submitted. Check your connection, then try again. Your draft is saved." `form-validation.md` / `primary-actions.md`: "Not submitted: no connection…". | "Not submitted: no connection. Your draft is saved. Try again." (`microcopy.md` §9). | Same shape as design's "Not saved: no connection."; true because Submit requires a saved draft (5.8). |
| 19 | **Banner dates.** `status-messages.md`: keep design.md's abbreviated dates verbatim. `microcopy.md`: normalise sentences to `dateLong`. | `dateLong` in both notices (N). | The microcopy research says so (Style Manual: abbreviate only when space is limited); the brief's house style; ESL readers. Words unchanged. |
| 20 | **"Only Alex can finish it."** design.md vs `microcopy.md`. | "Only Alex P. can finish it." (N). | Never derive a first name from `DisplayName` [Convention] W3C personal names. |
| 21 | **Discard confirm label.** `primary-actions.md`: "Discard draft". `confirm-dialog.md` / `microcopy.md`: "Discard draft for Jane Citizen". | "Discard draft for Jane Citizen". | Names the person on the irreversible button, like the Submit confirmation. |
| 22 | **Success after Discard, Save changes, Cancel.** `confirm-dialog.md` / `primary-actions.md`: "Draft for Jane Citizen discarded", "Changes saved". `status-messages.md`: none. | None; focus goes to the destination's `<h1>`. | design.md specifies a success message only for Submit; no new notification (hard rule). Listed as an open question. |
| 23 | **Dead-end refusals.** `guided-notes-textarea.md`: make the box `readOnly` after the after-midnight refusal. `autosave-status.md`: never make fields read-only; keep text selectable. | No read-only state; fields stay as they are, autosave stops, the banner explains. | design.md says only "The text stays on screen"; fewer special states; a checkbox cannot be read-only anyway, so readOnly text beside live ticks would be inconsistent. |
| 24 | **Load-failure copy.** `empty-loading-error.md`: "Could not load this note. Check your internet connection, then try again." `microcopy.md` §9 canonical: "[Thing] did not load: [cause]. Try again." | `microcopy.md` canonical. | Settled app-wide in the editorial pass: microcopy.md §9 owns the words, empty-loading-error.md the mechanism. |
| 27 | **Where the top bar lives and how the back link knows its destination.** This file: a plain `<div>` between the header and `<main>`; the back destination reached it through router state set by Today and Past notes rows (today.md, participant-notes.md). app-shell.md: bars inside `<main>`, router state holds only `inScreen`. | The shell's before-main bar slot (app-shell.md 3a), hidden with the route when signed out; the destination from the shell's in-memory opened-from record; the shared BackLink. | The bar keeps GOV.UK's position without ever showing over the signed-out view, and `history.state` (which some browsers write to disk) holds no navigation data. |
| 28 | **Note date in or out of the `<h1>`.** This file and `note-identity-header.md`: date in a `<p>`. `note-read-view.md` and participant-notes.md: date inside the `<h1>`. | Inside the `<h1>` on every single-note page (form, read view, version pages), as `note-identity-header.md` now records. | Read → Edit then changes only the caption a screen reader announces, and the focused heading confirms the day as well as the person (3.9). Visual result unchanged. |
| 25 | **Earlier-day indicator format.** `autosave-status.md`: "Saved Wed 30 Sep, 9:42 am". `microcopy.md`: `stamp` "Wed 30 Sep 2026, 9:42 am". | `microcopy.md` `stamp`. | One shared formatter, one `dateTime` token. |
| 26 | **Dialog buttons on a laptop.** `primary-actions.md`: side by side, label width. `confirm-dialog.md`: stacked full width on every size. | Stacked full width. | Same place on phone and laptop for managers who use both; long names wrap instead of squeezing a pair. `confirm-dialog.md` owns dialogs. |
| 29 | **Section 2 markup.** `checkbox-list.md`: one `<fieldset>` whose legend is `<h2>2. Common items</h2>`. `group-picker.md`: a plain `<div>` with the `<h2>` and hint, then separate fieldsets with `<h3>` legends for Every note, the question and each picked group. | `group-picker.md`. `TickListField` gains `headingLevel` and `hintId` (checkbox-list.md). | One fieldset cannot hold several groups without nesting fieldsets (H71); heading legends let screen-reader users jump between groups (WebAIM #10). Goals is unchanged. |
| 30 | **Pre-ticking.** `checkbox-list.md`: never pre-tick (GOV.UK, NHS). `group-picker.md` and D46: start with the last note's group picks. | D46 for group picks only; items are never pre-ticked. | D46 is a decision. The copied line names the source date (Joint Commission copy-forward practice), and the tension is recorded below. |
| 31 | **Hidden input when a reveal closes.** `conditional-reveal.md`: the typed flag reason is kept in memory and comes back on re-tick. `group-picker.md`: an unpicked group's ticks are cleared. | Clear the group's ticks (A46). | What is on screen is what is saved; hidden ticks are never sent (as `flagReason: null` is sent while unticked). Re-ticking a few boxes is quick. Recorded as a tension for the owner. |

---

## Tensions with decisions (recorded only; no change recommended)

Each is recorded once, with the evidence in the cited component file.

1. **D12/D34, guide prompts as disappearing placeholder text.** GOV.UK and NHS advise against placeholders for hints;
   NN/g lists memory strain and placeholders mistaken for entered text [Research]; the HTML spec intends "a short hint".
   Mitigated by the permanent label, 6.1:1 grey, and the empty-box error's "The grey text is only a guide."
   (`guided-notes-textarea.md`, `form-validation.md`, `microcopy.md`).
2. **D7/D10/A4, one optional tick per item.** A blank checkbox cannot tell "no" from "missed it"; GOV.UK added an
   explicit "none" option after cross-government research (`checkbox-list.md`). The hints "Ticked means reached." and
   "Ticked means done." are the design's mitigation.
3. **Submit disabled while unsaved (3.4).** GOV.UK, NHS and AgDS advise against disabled buttons [Convention]. Built
   with `aria-disabled`, a described reason and the check save (`primary-actions.md`, `autosave-status.md`).
4. **A confirmation on every submit (3.9).** Warnings lose effect from the second exposure (Anderson et al., CHI 2015)
   [Research]; a passive check is weaker than re-entering the name (Adelman 2013: OR 0.84 vs 0.60) [Research]. The
   changing name in the title is the best available defence against habituation (`confirm-dialog.md`,
   `note-identity-header.md`).
5. **Name only, no second identifier or photo (A5, scope).** SAFER 1.3: names alone are not sufficient [Convention];
   a banner photo lowered wrong-patient orders in one observational study (Salmasian 2020) [Research]
   (`note-identity-header.md`).
6. **Generic page titles (4.0).** Two tabs cannot be told apart in the tab strip; mitigated by the in-page name and the
   takeover banner; passes SC 2.4.2 (`note-identity-header.md`).
7. **D22, nothing on the device.** Text typed during an outage lives only in page memory and is lost if the phone
   discards the tab; mitigated by the page-hidden `keepalive` save and "Keep this page open" (`autosave-status.md`).
8. **Cancel discards the pending edit with no confirmation (3.5, A10).** SC 3.3.4 arguably applies to server-stored
   working copies (`confirm-dialog.md`, `primary-actions.md`).
9. **Conditional reveal (4.3 "a Reason field appears").** GOV.UK and NHS list it as a known 4.1.2 risk; single-field
   reveals tested fine (`conditional-reveal.md`).
10. **A6 Reason `nvarchar(200)` vs the GOV.UK character count.** Over-typed reason text cannot be autosaved in full;
    handled by capping only the saved copy (`conditional-reveal.md`).
11. **D18, "flag" is figurative.** COGA and the Style Manual prefer literal words; "Reason" and "Flagged for manager:
    Yes" make it concrete (`microcopy.md`).
12. **design.md copy with "can't" and "Please".** GOV.UK advises against negative contractions and "please"; kept word
    for word; new strings avoid both (`microcopy.md`).
13. **D46, the last note's group picks copied onto a new note.** GOV.UK and NHS say not to pre-select checkboxes, and
    defaults tend to stay (Johnson and Goldstein 2003) [Research]. A stale pick prints its group's items unticked
    under its name. Mitigated by the copied line with the source date and by putting the items, unticked, in the
    writer's path; a daily line will fade (Anderson 2015) (`group-picker.md`).
14. **D44, groups and their items on one page.** GOV.UK and NHS put multi-part follow-ups on a later page, and
    multi-field reveals "complicated the relationship between the question and revealed content" (GOV.UK 2021).
    Placing the lists after the whole question (AgDS), with headings, a hint and `aria-expanded`, narrows the 4.1.2 gap
    without closing it (`group-picker.md`).
15. **D47, unpicked groups do not appear in the record.** A missing group cannot show "did not happen" apart from
    "forgot to tick it", extending tension 2 (`group-picker.md`).
16. **A46 (unpicking clears ticks), an assumed default, not a decision.** It differs from the flag reason, which comes back
    on re-tick (intent of SC 3.3.7; ticks are optional, so 3.3.7 does not strictly apply) (`group-picker.md`).

---

## Open questions for the owner

1. **Compact name in the sticky top bar** once the large name scrolls away: keep or drop? It is the one behaviour not
   in the 4.3 sketch.
2. **"Submit the note for"** (and "started the note for") when the note date is not today: approve the derived wording.
3. **"Editing submitted note (version 2)"**: is the number the version being edited (assumed here) or the version Save
   changes will create?
4. **Derived back-link labels** "Participant" and "Past notes" for past-day notes; edit mode returns to the read view's
   back destination.
5. **Proposed strings:** the four field errors; the dialog busy and failure lines; "Not saved" while a banner blocks;
   the archived-participant refusal; the version-conflict line and summary; the Discard dialog body lines;
   "More actions"; "Saving changes…", "Cancelling…", "Changes not saved: …", "Not cancelled: …", "Not discarded: …";
   "Loading note…" and "The note did not load: …".
6. **No success message after Discard, Save changes or Cancel** (only focus on the destination heading). Confirm.
7. **Leaving after a failed save** stays on the form with no dialog. Confirm.
8. **Pressing Submit during "Saving…"** opens the confirmation once the check save lands. Confirm this reading of
   "Submit is disabled while the latest change is not saved".
9. **API:** does `PUT {base}/draft` validate narrative length (this screen caps it at 20,000 either way)? Does it accept
   `isFlagged: true` with `flagReason: null`? Is a spaces-only reason treated as blank on the server? What does a check
   save return when the note was submitted from another tab, and should `POST …/versions` also check `clientId`? Should
   a `PUT` after a discard be refused so a late autosave can never recreate a draft?
10. **Submit after the note was submitted from another tab** (`409 note.version_conflict` with `baseVersion = 0`): the
    dialog shows the takeover sentence and Go back only; afterwards the text stays on screen with Submit unavailable.
    design.md does not cover this case.
11. **Device checks (M6):** sticky bar with the iOS keyboard open; placeholder visibility while focused but empty;
    VoiceOver/TalkBack wording for `aria-expanded` on a checkbox and for the focused dialog title; whether Dragon and
    Windows voice typing fire `input` events; Safari focus on tapped checkboxes.
12. **Router:** `useBlocker` needs `createBrowserRouter` (a data router). Confirm at M0.
13. **Common item groups, assumed defaults:** confirm or override A41–A47 as listed at the top of this file (in
    particular A44, where picks are copied from; A45, nothing has to be picked; A46, unpicking clears ticks; and A47,
    the copied line, which can be dropped on its own without changing where picks are copied from). The
    picker's three strings are now in design.md 4.3, so they are marked (V) here.
14. **API (closed):** the group fields group-picker.md listed as gaps (`commonItemGroups` and `picksCopiedFrom` on
    the draft `GET`, `pickedGroupIds[]` on the draft `PUT` and the version `POST`, `listsVersion` covering groups, the read API's shown
    groups) are now in design.md §5.7 and §6.3. Nothing further is needed from this screen.

---

## Sources

Component specs composed here (each holds its full evidence): `../components/note-identity-header.md`,
`checkbox-list.md`, `group-picker.md`, `guided-notes-textarea.md`, `autosave-status.md`, `conditional-reveal.md`, `form-validation.md`,
`primary-actions.md`, `confirm-dialog.md`, `status-messages.md`, `session-timeout.md`, `foundations.md`,
`microcopy.md`; also consulted `app-shell-nav.md`, `empty-loading-error.md`, `note-read-view.md`.

Key primary sources repeated from those files:

- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (C43, F110): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Understanding 4.1.3 Status Messages and ARIA22: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- WCAG 2.2 Understanding 3.2.2 On Input: https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- WCAG 2.2 Understanding 3.3.7 Redundant Entry: https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.4.2 Page Titled: https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html
- WCAG Technique H71: https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- WAI-ARIA APG Dialog (Modal): https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ · Alert dialog: https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/ · Checkbox: https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/ · Disclosure: https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/
- HTML Standard, the dialog element: https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element
- MDN `<dialog>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog · `aria-expanded`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-expanded · `aria-disabled`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-disabled · `field-sizing`: https://developer.mozilla.org/en-US/docs/Web/CSS/field-sizing · `History.pushState()`: https://developer.mozilla.org/en-US/docs/Web/API/History/pushState · `beforeunload`: https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- Chrome Page Lifecycle API: https://developer.chrome.com/docs/web-platform/page-lifecycle-api · Viewport resize behaviour: https://developer.chrome.com/blog/viewport-resize-behavior
- React `<form>` (uncontrolled reset after an action): https://react.dev/reference/react-dom/components/form · React issue 23301: https://github.com/facebook/react/issues/23301
- React Router `useBlocker`: https://reactrouter.com/api/hooks/useBlocker
- GOV.UK Design System: Error summary https://design-system.service.gov.uk/components/error-summary/ · Error message https://design-system.service.gov.uk/components/error-message/ · Validation https://design-system.service.gov.uk/patterns/validation/ · Character count https://design-system.service.gov.uk/components/character-count/ · Checkboxes https://design-system.service.gov.uk/components/checkboxes/ · Back link https://design-system.service.gov.uk/components/back-link/ · Button https://design-system.service.gov.uk/components/button/ · Notification banner https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK design notes, radios and checkboxes (2016): https://designnotes.blog.gov.uk/2016/11/30/weve-updated-the-radios-and-checkboxes-on-gov-uk/
- GOV.UK accessibility blog, conditionally revealed questions (2021): https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/
- NHS digital service manual, Checkboxes: https://service-manual.nhs.uk/design-system/components/checkboxes
- Agriculture Design System, Conditional field container: https://design-system.agriculture.gov.au/components/conditional-field-container
- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- Bargas-Avila et al. 2007, validation timing: https://academic.oup.com/iwc/article-abstract/19/3/330/693000
- Baymard, inline form validation (2024): https://baymard.com/blog/inline-form-validation
- NN/g, placeholders in form fields: https://www.nngroup.com/articles/form-design-placeholders/ · Sticky headers: https://www.nngroup.com/articles/sticky-headers/ · Confirmation dialogs: https://www.nngroup.com/articles/confirmation-dialog/
- Gatsby/Fable, accessible client-side routing testing (2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- Adelman et al. 2013, wrong-patient orders RCT: https://pubmed.ncbi.nlm.nih.gov/22753810/
- ONC SAFER Patient Identification guide: https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf
- Anderson et al. CHI 2015, warning habituation: https://scholarsarchive.byu.edu/facpub/9306/
- PowerMapper, fieldset with no controls (2025): https://www.powermapper.com/tests/screen-readers/labelling/fieldset-no-controls/
- Adrian Roselli, live region support (2026): https://adrianroselli.com/2026/01/live-region-support.html
- Group picker (full list in `group-picker.md`): WebAIM Screen Reader User Survey #10 (2024): https://webaim.org/projects/screenreadersurvey10/ ·
  Johnson and Goldstein, Do Defaults Save Lives? (2003): https://www.dangoldstein.com/papers/DefaultsScience.pdf ·
  The Joint Commission, Quick Safety Issue 10 (copy and paste): https://digitalassets.jointcommission.org/api/public/content/9c4646fca14f4cbea2b98a1f0366a496?v=82c80221 ·
  WAI-ARIA APG Checkbox (Mixed-State) example: https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/examples/checkbox-mixed/
