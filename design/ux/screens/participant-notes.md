# Participant notes history and read view

Screen key: `participant-notes`. Covers design.md 4.4: the **Past notes** list for one participant and the **read
view** of one note. The read view is also what opens from a Submitted row on Today (4.2) and from a Flagged row (4.6).
**This file owns the read view and the manager's review panel for every route that shows them**: the page behaves
the same whichever list opened it, and only the back link's label differs (flagged.md adds nothing else). The note
URLs are defined once in note-form.md, Routes. One version in full (4.5) is a separate screen (`version-history.md`)
and is not covered.

Sources: design.md 2, 3.4–3.9, 4.0, 4.2, 4.4, 4.6, 6.1, 6.3, 6.4, 6.9, 11.3, M3, A3, A4, A5, A6, A9, A11, A13–A17,
A32, A33, A43; decisions D6, D7, D10, D15–D20, D22, D25, D26, D34, D35, D39, D42, D44–D47 (common items in groups;
the read view shows Every note and the picked groups only, [`group-picker.md`](../components/group-picker.md)).
Component specs composed here are linked in "Components, in order".

Evidence grades: **[Research]** studies and usability testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML ·
**[Convention]** established design systems · **[Opinion]** reasoned judgement. Copy marks: **(V)** verbatim from
design.md · **(N)** design.md wording normalised as `microcopy.md` directs · **(P)** proposed. Every (P) string here
on 9 October 2026 was approved as written (D67, Open question 4); a (P) string added later still needs owner sign-off.

---

## Purpose and who uses it

**Purpose (4.4):** read one participant's past notes (D20). The list answers "what has been written for Jane, and on
which days?". The read view shows one note exactly as written, in the same order and words as the daily report's note
block (11.3), so a note reads the same on screen and on paper.

| Who | Why they come here | What they can do here |
|---|---|---|
| **Support worker** (phone, often tired, possibly reading English as an additional language) | Check what happened on earlier days before writing today's note; re-read their own submitted note; fix their own note | Open any note for any participant (D20). **Edit** only their own submitted notes (D19). See Flagged and its reason on any submitted note. Never see To review, Reviewed, review comments or Version history (A13, A14; the API does not send them, 6.3). |
| **Manager** (laptop or phone) | Spot-check notes (D17, A16), act on a flag reached from anywhere, correct a note (3.9, 11.6), start a forgotten past-day note, export a record | Everything a worker can, plus **Edit** on any submitted note, **Version history**, review status and comment, **Mark reviewed** when the note is To review, a read-only view of someone else's draft with **Discard draft**, and the list's **Write past-day note**, **Export record** and **Edit participant**. |

**How people arrive (4.4):** the "Past notes" link in the note form and read view, Manage > Participants > participant
detail > Past notes, a Submitted row on Today (read view), a Flagged row (read view, 4.6).

**Primary action (4.4):** open a note. Nothing on these screens is primary-styled except **Mark reviewed** in the
manager's review panel (`primary-actions.md`).

---

## Layout - phone  (ASCII wireframe, top to bottom, at ~375 px)

Single column, 16 px side gutters, nothing sticky or fixed on either screen (see Conflicts resolved, 1). The app header
and nav come from `app-shell-nav.md` and scroll away with the page.

### A. Past notes (history list), manager

```
+-----------------------------------------+
| Grow2Notes                    Account v |  app header (app-shell-nav.md), static
| Today  Flagged 3  Report  Manage        |  workers see "Today" only
+-----------------------------------------+
| < Note                                  |  back link in the shell's bar slot, static, 44 px
+-----------------------------------------+
| Past notes                              |  <h1> caption: 18 px regular, secondary colour
| Jane Citizen                            |  <h1> name: 28 px bold, never truncated
|                                         |
| [         Write past-day note         ] |  managers only: secondary-styled links,
| [            Export record            ] |  full width, stacked, 8 px apart.
| [           Edit participant          ] |  Write past-day note hidden if archived
|-----------------------------------------|
| Thursday 1 October 2026                 |  row link: whole row, min 56 px
| Submitted · Alex P.                     |  meta: 16 px, secondary colour, status word bold
| [Flagged] [To review] [Edited]          |  tags wrap as whole words (managers see To review)
|-----------------------------------------|
| Wednesday 30 September 2026             |
| Draft · Sam Lee                         |  drafts: no tags, ever
|-----------------------------------------|
| Tuesday 29 September 2026               |
| Submitted · Priya Nair  [Edited]        |
|-----------------------------------------|
| ... up to 30 rows ...                   |
|-----------------------------------------|
| [              Show older             ] |  secondary <button>, full width, 48 px
| Loading older notes...                  |  <p role="status">: empty unless slow or failed
|                                         |
+-----------------------------------------+
```

Worker version: no manager actions; the first row follows the `<h1>` directly. No tags other than Flagged and Edited.

### B. Read view, manager, note To review (reached from Past notes)

```
+-----------------------------------------+
| Grow2Notes                    Account v |
| Today  Flagged 3  Report  Manage        |
+-----------------------------------------+
| < Past notes                            |  back link names where it goes
+-----------------------------------------+
| Sam Taylor                              |  <h1> line 1: name, 28 px bold
| Thursday 1 October 2026                 |  <h1> line 2: note date, 18 px regular, text colour
|                                         |
| Written by Priya Nair ·                 |  byline (16 px, secondary colour)
| Submitted 4:42 pm                       |
| [Edited] last change by Jo Smith,       |  one line per status, report order (11.3)
| Fri 2 Oct 2026, 9:01 am                 |
| [Flagged] Reason: Mentioned pain in     |  reason in text colour (user-written)
| his left knee after the walk.           |
| [To review]                             |  managers only
|                                         |
| [                 Edit                ] |  secondary-styled link (author or manager)
| Version history                         |  text link, 44 px tall (managers)
| Past notes                              |  text link, 44 px tall
|-----------------------------------------|  1 px rule
| 1. Goals                                |  <h2>
| [x] Catch the 903 bus to the library    |  TickListRead: icon + words, not controls
|     on his own                          |
| [ ] Make his own lunch                  |
|-----------------------------------------|
| 2. Common items                         |  <h2>
| Every note                              |  <h3>, only when it has items
| [x] Medication prompted                 |
| [ ] Meal prepared                       |
| Community outing                        |  <h3>: each group picked on this note,
| [x] Travelled by bus or train           |  in the manager's order (D47);
| [ ] Paid for own purchases              |  unpicked groups never appear
|-----------------------------------------|
| 3. Guided notes                         |  <h2>
| Sam was keen to go to the library       |  18 px, line-height 1.5, pre-wrap,
| today. We caught the 903 together...    |  never cut short or collapsed
|                                         |
|-----------------------------------------|
| Review                                  |  <h2>, managers, To review only
| Comment (optional)                      |  <label>
| Shown in the daily report and record    |  hint (P)
| exports, and cannot be changed later    |
| +-------------------------------------+ |
| |                                     | |  <textarea rows="4">, 18 px
| +-------------------------------------+ |
| You can enter up to 500 characters      |  GOV.UK character count
| [            Mark reviewed            ] |  primary, full width, 48 px
|                                         |
+-----------------------------------------+
```

### C. Read view variants (phone, same frame)

```
Worker, someone else's draft                 Manager, someone else's draft
+-----------------------------------+        +-----------------------------------+
| < Today                           |        | < Past notes                      |
+-----------------------------------+        +-----------------------------------+
| Jane Citizen                      |        | Jane Citizen                      |
| Thursday 1 October 2026           |        | Thursday 1 October 2026           |
|                                   |        |                                   |
| Alex P. started today's note for  |        | Alex P. started today's note for  |
| Jane Citizen at 9:14 am. Only     |        | Jane Citizen at 9:14 am. Only     |
| Alex P. can finish it.            |        | Alex P. can finish it.            |
|                                   |        |                                   |
| Past notes                        |        | [         Discard draft         ] |  secondary trigger
+-----------------------------------+        | Past notes                        |
                                             |-----------------------------------|
                                             | 1. Goals ... 2. Common items ...  |
                                             | 3. Guided notes                   |
                                             | Nothing written yet.              |  (P), only if empty
                                             |-----------------------------------|
                                             | [ ] Flag for manager              |  read-only row, never
                                             +-----------------------------------+  the Flagged tag
```

---

## Layout - laptop  (what changes at wider widths)

One breakpoint: `@media (min-width: 40rem)` (`foundations.md`). Same markup, same order, same words.

- **Column.** The page container is centred at `--page-max` (60rem); the content column is `--measure` (40rem),
  left-aligned inside it, so it shares a left edge with the nav. No sidebar and no second column for metadata: a side
  column would split what the report keeps together and would make reading order differ from visual order
  (SC 1.3.2). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html
- **Line length.** 40rem at 18 px is about 70 characters per line, inside the 55–75 range the line-length studies and
  GOV.UK support. Count a real line of Guided notes in the system font during build; narrow the column if it runs past
  about 75. [Research] https://legible-typography.com/en/6-overview-of-research-typography · [Convention]
  https://design-system.service.gov.uk/styles/layout/
- **Type.** `<h1>` name 32 px; `<h2>` 24 px; body stays 18 px; meta lines stay 16 px.
- **Gutters and gaps.** 32 px gutters; 48 px between sections.
- **Buttons and actions.** Buttons are as wide as their labels (at least 8rem), left-aligned, in a wrapping row with
  1rem gaps: the three manager actions sit on one line; on the read view **Edit**, **Version history** and **Past
  notes** sit on one line. **Show older** and **Mark reviewed** are auto width, left-aligned.
- **Hover** (only under `@media (hover: hover)`): list rows tint `--colour-hover` and the row link's underline
  thickens; text links thicken their underline; buttons darken a step. Tags, tick rows and text have no hover.
- **Top bar.** Its contents line up with the content column; still static.

---

## Components, in order

Each entry names the component spec, then the settings for this screen only. Shared behaviour is not restated.

### History list (A), top to bottom

1. **App header and nav** → [`app-shell-nav.md`](../components/app-shell-nav.md)
   - No nav item is current on this screen ("Note, read view, history … none").
   - Page title: **Grow2Notes – Past notes** (page-name list in `app-shell-nav.md`). Never a name (4.0).
   - Route: `/participants/{participantId}/notes` (IDs only; see Cross-screen issues on route ownership).

2. **Back link** → the shared BackLink in the shell's before-main bar slot (app-shell.md component 3a),
   [`note-identity-header.md`](../components/note-identity-header.md)
   - **Static on this screen** (not sticky), no compact name (Conflicts resolved, 1).
   - Back destinations and labels, from the shell's in-memory opened-from record (no router state): opened from the
     note form or read view → **Note** (derived); from participant detail → **Participant** (derived); opened from
     outside the app → **Today** (V, fallback).
   - Pops (`navigate(-1)`) when the previous entry is the destination; otherwise follows its `href`.

3. **Identity block (`<h1>`)** → [`note-identity-header.md`](../components/note-identity-header.md)
   - `<h1 tabIndex={-1}>` = caption **Past notes** (derived) + the participant's given and family name as stored,
     `translate="no"`, never upper-cased or truncated. No date line on this screen.
   - Sizes from `foundations.md`: caption 18 px regular `--colour-text-secondary`; name `--font-size-h1` (28/32 px) 700.
   - Rendered only when both the participant and the first page of notes have loaded (no placeholder or cached name).

4. **Manager actions** → [`primary-actions.md`](../components/primary-actions.md)
   - Managers only; hidden (not disabled) for workers. Order: **Write past-day note** (V), **Export record** (V),
     **Edit participant** (V). Each is a React Router `<Link>` styled as a secondary button.
   - **Write past-day note** goes to the past-day page (`date-navigation.md`); hidden when the participant is
     archived (3.7: no new notes, including past-day notes). **Export record** goes to the export page
     (`export-form.md`). **Edit participant** goes to participant detail (4.8).
   - They render with the identity block, even when the list is empty (`empty-loading-error.md`).

5. **Chronological list, paged** → [`chronological-list.md`](../components/chronological-list.md)
   - Mode: **paged**, 30 per page, newest first by note date, `useInfiniteQuery` on
     `GET /api/participants/{id}/notes?before=`, cursor = last row's `noteDate`, `hasMore` only (6.3).
   - Row link text: the note date as `dateLong` in `<time dateTime="YYYY-MM-DD">` ("Thursday 1 October 2026").
     Link styling: body size, weight 700 (foundations replaces the list spec's 600), `--colour-action`, underlined.
   - Row: `min-block-size: 3.5rem` (56 px, `foundations.md`), 1 px `--colour-divider` between rows, `::after` overlay
     makes the whole row the target. No date-group headings, no relative dates, no actions inside rows.
   - Every row links to `/participants/{id}/notes/{noteDate}`; the note route decides what the viewer gets (own draft
     → form; someone else's draft → C; submitted → read view), as note-form.md's Routes table defines. The history API has no `isMine`, so the list cannot
     and does not decide.
   - Rows carry `data-return-key={noteDate}` and push **no router state**; the shell records that Past notes opened
     the note (app-shell.md route-change rule), so the note's back link says "Past notes" and pops back here. The
     route declares `handle: { returnFocus: {} }`.
   - Keys: `noteDate`.
   - Query key `['pastNotes', participantId]` (note-form.md invalidates it after Save changes and Cancel). Query
     options: `networkMode: 'always'`, `gcTime: 30 min`, no `maxPages`, and **`refetchOnWindowFocus: true` set
     explicitly** (the app default is off; app-shell.md lists this query among the opt-ins), so every loaded page
     refetches in order, silently, when the window regains focus. Retry: one silent retry on network errors,
     timeouts and `5xx` only (Conflicts resolved, 12).

6. **Meta line and tags (per row)** → [`status-tags.md`](../components/status-tags.md)
   - `MetaLine` parts: status word, then `authorDisplayName`: **Submitted · Alex P.** / **Draft · Alex P.** (V).
     The status word is bold; the line is `--font-size-small`, `--colour-text-secondary` (foundations, as on Today).
   - Tags via `historyTags()`: **Flagged** (Submitted and current version flagged; never on a draft), **To review** or
     **Reviewed** (managers only; from `flagStatus`), **Edited** (`isEdited`). Fixed order: Flagged → To review |
     Reviewed → Edited. Amber for Flagged and To review, neutral grey for Reviewed and Edited. One tag style app-wide
     (`status-tags.md`): attention tags carry the 1 px `--colour-attention-edge` border, the same Flagged tag as Today,
     and every tag keeps its border in forced colours.
   - No "Past-day note" tag on rows (the history API returns no `isPastDayNote`; Open questions).
   - The row link has `aria-describedby` pointing at the meta line; separators are `aria-hidden` with a visually
     hidden comma.

7. **Show older and its status line** → [`chronological-list.md`](../components/chronological-list.md)
   - **Show older** (V): secondary `<button type="button">`, full width on phone, auto on laptop, rendered only while
     `hasNextPage`.
   - Busy: `aria-disabled="true"` from the press; the label stays **Show older**; the status line under it says
     **Loading older notes…** (P) only after 1 s.
   - The status line is a `<p role="status">` rendered with the list, empty until needed.

8. **Empty, loading and error states** → [`empty-loading-error.md`](../components/empty-loading-error.md)
   - One page-level `<p role="status">` in `<main>` from the first render, for first-load loading and failure text.
   - Copy follows `microcopy.md`'s load-failed pattern (States section).

### Read view (B, C), top to bottom

1. **App header and nav** → [`app-shell-nav.md`](../components/app-shell-nav.md)
   - Page title: **Grow2Notes – Note** (V, 4.0). Route: `/participants/{participantId}/notes/{noteDate}`.

2. **Back link** → the shared BackLink in the shell's before-main bar slot (app-shell.md component 3a),
   [`note-identity-header.md`](../components/note-identity-header.md)
   - Static, no compact name. Labels from the shell's opened-from record: **Today** (from Today; default), **Past
     notes** (from the list), **Flagged** (from either Flagged tab, 4.6). This label is the only thing that differs
     between the routes that open the read view.

3. **Title (`<h1>`)** → [`note-read-view.md`](../components/note-read-view.md), sizes from `foundations.md`
   - One `<h1 tabIndex={-1}>` with two block lines: the name (`translate="no"`, `--font-size-h1`, 700) and the note date
     as `dateLong` in `<time dateTime="2026-10-01">` (body size, 400, `--colour-text`). A literal space between them
     gives the accessible name "Sam Taylor Thursday 1 October 2026". (Conflicts resolved, 2.)

4. **Byline and status lines** → [`note-read-view.md`](../components/note-read-view.md) +
   [`status-tags.md`](../components/status-tags.md) + [`microcopy.md`](../components/microcopy.md)
   - Order = the report block (11.3): byline (or past-day line, then Submitted) → Edited → Flagged + Reason →
     To review / Reviewed (+ Comment). Lines that do not apply are not rendered: no "Not flagged", "Not edited".
   - `--font-size-small` (16 px), `--colour-text-secondary` (9.0:1); tags same size as the line; the user-written
     reason and comment in `--colour-text` with `white-space: pre-wrap; overflow-wrap: break-word`.
   - Times use `microcopy.md`'s `stamp` with the **note date** as the reference day: time alone on the note date,
     otherwise "Fri 2 Oct 2026, 8:05 am". Exact strings are in States.

5. **Action row** → [`primary-actions.md`](../components/primary-actions.md)
   - **Edit** (V): `<Link>` styled as a secondary button; only when the note is Submitted and the viewer is the
     author (`author.id === me.userId`) or a manager. Navigates with `replace` to
     `/participants/{id}/notes/{date}/edit` (note-form.md, Routes). No router state: the shell copies the opened-from
     record across the replace, so the form's back link keeps the same destination.
   - **Version history** (V): plain text link, managers, every submitted note (even version 1).
   - **Past notes** (V): plain text link to the list (a new visit, top of the list).
   - Hidden, never disabled, when not allowed. Each target at least 44 px tall.

6. **Goals and Common items** → [`checkbox-list.md`](../components/checkbox-list.md) (`TickListRead`) and
   [`group-picker.md`](../components/group-picker.md) (Per-screen notes)
   - Headings `<h2>` **1. Goals** and **2. Common items** (V). Goals: every snapshot goal in snapshot order, icon plus
     words; screen readers hear "{item}, ticked" / "{item}, not ticked".
   - **Common items (D47):** under the `<h2>`, `<h3>` **Every note** (V) and its `TickListRead` (only when its
     snapshot has items, A43), then `<h3>` with the group's name as stored and its `TickListRead` for **each group
     picked on the version shown**, in configured (snapshot) order. Every item in a shown group appears, ticked or not
     ticked (A4). Groups not picked, the "Which of these happened?" question and the copied-picks line never appear.
     `TickListRead` takes `headingLevel={3}` for these (checkbox-list.md).
   - Empty: **No goals set** / **No common items set** (V, 11.3); the second when no group is shown at all. No hints,
     no form controls, keyed by index (groups too: the read API gives names, not IDs).
   - The same rendering serves the manager's read-only view of someone else's draft (the draft's Every note and the
     groups picked so far) and note-form.md's version-conflict notice.

7. **Guided notes** → [`note-read-view.md`](../components/note-read-view.md)
   - `<h2>` **3. Guided notes** (V) and one `<div>` of escaped React text: `white-space: pre-wrap`,
     `overflow-wrap: break-word`, body size, line height 1.5. Never truncated, collapsed, linkified or rendered as HTML.
     No guide prompts (D34).

8. **Review panel** (managers) → [`review-panel.md`](../components/review-panel.md). This file is its owner for
   every route; flagged.md only links here.
   - **When it renders:** a manager, a Submitted note, and `flagStatus = ToReview` when the page first renders.
     **Once rendered it stays mounted for the rest of the visit**, even after the note refetches as Reviewed, so its
     success record, a conflict message and the focus on them are never lost. Never for workers, drafts, never-flagged
     notes, or a note that was already Reviewed when the page opened.
   - **Placement:** the last block of the read view, after "3. Guided notes", in the same column; not sticky, not a
     sidebar. `<section>` with `<h2>` **Review** (P) and no accessible name (not a landmark).
   - `<form noValidate onSubmit>`: `<textarea>` labelled **Comment (optional)**, hint (P), GOV.UK count (500, A6),
     an always-present `role="alert"` slot above the button, primary **Mark reviewed** (V) `<button type="submit">`.
     Request `POST {base}/reviews` with `{ versionNumber: <version on screen>, comment }`; comment trimmed, blank
     sent as `null`, inner line breaks kept.
   - **Busy:** `aria-disabled` from the press; after **400 ms** the label reads **Marking reviewed…** (P, the button's
     own verb; microcopy.md glossary), also written into the page status region (app-shell.md component 3).
   - **Not built unless the owner approves** (Open questions 1 and 2): an "Earlier reviews" block for a note flagged
     again after a review (A15), and a "Go to flagged notes" link after a successful review. The default build has
     neither, on every route.
   - **An unsent comment** (one rule for every route): when the read view unmounts with text in the comment box (Edit,
     a back link, signing out in place), the text is kept in a module-level `Map` keyed by `participantId/noteDate`
     (memory only, D22) and put back when the same note's read view mounts again in the same page load. It is
     forgotten after a successful Mark reviewed and lost on reload. Mark reviewed then sends the version on screen.
     [Opinion; ui-ux-design invariant I9, spirit of SC 3.3.7]

9. **Discard draft** (manager, someone else's draft) → [`confirm-dialog.md`](../components/confirm-dialog.md) +
   [`primary-actions.md`](../components/primary-actions.md)
   - Trigger **Discard draft** (V) is a secondary `<button>` in the action row. The dialog's final button is the
     warning style. Copy in States.

10. **Empty, loading and error states** → [`empty-loading-error.md`](../components/empty-loading-error.md)
    - Page-level `<p role="status">` in `<main>` from first render.

**Data for the read view (one render, not in pieces):** `GET /api/participants/{id}` (name, status) and `GET {base}`
(note). For a manager on a note that is To review but whose current version is not flagged, also the To review list
(`GET /api/reviews?status=toReview`, manager-only, small, often cached) to show the flagged version's reason
(`note-read-view.md`, Per-screen notes). Query key **`['notes', participantId, noteDate]`**, the one prefix shared with
`version-history.md` (which appends `'versions'`) and with every route that opens the read view, so note-form.md's
invalidation after Save changes or Cancel refreshes all of them. `refetchOnWindowFocus: false`, no `placeholderData`.
Page component keyed by `participantId/noteDate`.
Common items come in the note response as `current.commonItemGroups: [{name, isEveryNote, items: [{text,
isTicked}]}]`, which holds only Every note (left out when it has no items) and the groups picked in that version, in
order (design.md §6.3, D47). The screen renders what it is given and never filters groups itself.

---

## States

Every state from 4.4, plus the loading, empty and error states. Copy is exact. Dates: `dateLong` uses a non-breaking
space between day and month; times use a non-breaking space before am/pm; exactly 12:00 shows "midday"
(`microcopy.md`).

### History list (A)

| State | What shows | Copy |
|---|---|---|
| **Waiting** (under 1 s) | Top bar and back link only; `<main>` empty apart from the empty status `<p>`. | – |
| **Loading** (after 1 s) | One line in the page status region. No spinner, no skeleton rows. | Loading notes… (P) |
| **Default, worker** | `<h1>`, then the list, then Show older when `hasMore`. Tags: Flagged, Edited. | Rows: "Thursday 1 October 2026" / "Submitted · Alex P." (V) [Flagged] [Edited] |
| **Default, manager** | As worker, plus the three manager actions and To review / Reviewed tags. | Write past-day note · Export record · Edit participant (V) |
| **Draft rows** | Status word Draft, author, no tags. Same link as any row. | Draft · Sam Lee (V) |
| **Archived participant** (reached from Manage) | Same as default; **Write past-day note** is not rendered. No banner (design specifies none here). | – |
| **Empty** (no live notes) | The sentence as a plain `<p>`; no list, no Show older. Managers still see their actions. | No notes yet for Jane Citizen. (V) |
| **Show older, loading** (after 1 s) | Rows unchanged; button `aria-disabled`, label unchanged; status line text. | Loading older notes… (P) |
| **Show older, failed** | Rows unchanged; button available again; focus stays on it. Clears when the next attempt starts. | Older notes did not load: no connection. Try again. · Older notes did not load: something went wrong. Try again. (P, `microcopy.md` §9; the same strings as Flagged > Reviewed) |
| **Show older, failed again** | Second failure in a row; the changed text is announced again | Older notes still did not load: no connection. Try again. (or the server cause) (P) |
| **End of list** | Show older is removed. No "end of list" message. | – |
| **First load failed** | Fallback `<h1>` "Past notes", the message in the page status region, then a **Try again** secondary button outside it. | Past notes did not load: no connection. Try again. · Past notes did not load: something went wrong. Try again. (P) + Try again |
| **Try again failed again** | The text changes so screen readers announce it again; focus stays on Try again. | Past notes still did not load: no connection. Try again. (P) |
| **Participant not found** (`404`, another organisation's ID, bad URL) | Whole-page message (`empty-loading-error.md`). | `<h1>` Page not found · If you typed or pasted the web address, check it is correct. · Go to Today (P) |
| **Signed out** (`401`) | Sign-in in place (8.5, `session-timeout.md`); same route afterwards. | Owned there |
| **Background refresh failed** | Nothing changes and nothing is announced. | – |
| **Forced colours / 200% text / 320 px** | Every tag keeps its border (`status-tags.md`); dividers and focus ring stay; everything wraps; no horizontal scroll. | – |

### Read view (B, C)

**Status lines** (all shown only when they apply):

| Situation | Who | Line(s) | Source |
|---|---|---|---|
| Byline, submitted on the note date | Everyone | Written by Priya Nair · Submitted 4:42 pm | (V) 4.4, 11.3 |
| Byline, submitted on a later day | Everyone | Written by Priya Nair · Submitted Fri 2 Oct 2026, 8:05 am | (V words, N format) 4.4 "with its date when it was a later day" |
| Past-day note (`isPastDayNote`) — replaces "Written by" | Everyone | [Past-day note] written by Jo Smith (manager) on Sat 3 Oct 2026, 10:14 am · next line: Submitted Sat 3 Oct 2026, 10:20 am | (N) `microcopy.md`: one wording on screen and in files, the 11.3 order. Screen reader: "Past-day note, written by…" |
| Edited (`isEdited`) | Everyone | [Edited] last change by Jo Smith, Fri 2 Oct 2026, 9:01 am (or "…by Jo Smith, 5:03 pm" on the note date) | (V words 3.5, 11.3; N format) |
| Current version flagged | Everyone | [Flagged] Reason: Mentioned pain in his left knee after the walk. | (V) "Reason" is the form label (4.3) |
| To review | Managers | [To review] | (V) |
| To review, flag removed by a later edit | Managers | [To review] Flag removed in a later edit · next line: Reason: {reason from the flagged version, via the To review list}. If that lookup fails, the Reason line is left out. | (V) 4.6, 11.3 |
| Reviewed (latest review only) | Managers | [Reviewed] by Jo Smith on Fri 2 Oct 2026, 9:30 am · next line, if any: Comment: Called Sam's mum. GP booked for Monday. | (V) M3 "Reviewed by … on …"; date always shown so "on" reads correctly |

**Page states:**

| State | What shows | Copy |
|---|---|---|
| **Waiting / Loading** | Top bar only. After 1 s the page status line. Nothing of the note (no name, no empty ticks, no empty text box) until the participant, the note and any needed To review lookup have loaded; then it renders at once. | Loading note… (P) |
| **Submitted, worker, not author** | B without Edit, Version history, To review/Reviewed or the review panel. | – |
| **Submitted, author (worker)** | B with **Edit**; no Version history, no review data. | Edit (V) |
| **Submitted, manager** | B with Edit and Version history; review status lines when flagged. | Edit · Version history · Past notes (V) |
| **To review, manager** | Review panel at the bottom. | See Review panel below |
| **Reviewed, manager** | Reviewed line and comment at the top; no panel. | – |
| **Empty goals / common items** | Under the section heading. "No common items set" when no common-item group is shown (Every note empty and no group picked). | No goals set · No common items set (V, 11.3) |
| **Common items shown** (D47) | Every note (if it has items), then each picked group under its name, in the manager's order; each item ticked or not ticked. A note with no group picked shows Every note only. | Group headings: Every note (V) and the group names as stored |
| **Someone else's draft, worker** (4.2, 4.4) | `<h1>` name and date, one sentence, the **Past notes** link. No sections, no tags. | Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it. (V, with the staff-name rule: N) · earlier-day draft: Alex P. started the note for Jane Citizen at 9:14 am. Only Alex P. can finish it. (P) |
| **Someone else's draft, manager** (4.2, 4.4, A9) | The same sentence, then **Discard draft**, **Past notes**, then Goals, Common items and Guided notes from the draft, then a read-only tick row for the flag (and its reason if ticked). Never the Flagged tag on a draft (3.6). | Discard draft (V) · Flag for manager, ticked / not ticked (V words) · Reason: … · empty text: Nothing written yet. (P) |
| **Discard dialog** (manager) | `confirm-dialog.md` dialog 2, manager variant. | Title: Discard the draft note for Jane Citizen? (V) · Body: Alex P. started it at 9:14 am. (P) · Nobody will be able to open this draft again. (P) · Confirm (warning): Discard draft for Jane Citizen (P, `microcopy.md`) · Go back · Busy: Discarding… (P) · Failed: Not discarded: no connection. Try again. (P, `microcopy.md` pattern) · `409`: This draft has already been submitted or discarded. (P) |
| **Note not found** (participant loaded, note `404`: for example a draft discarded since the list loaded, and the viewer cannot start a note for that date) | `<h1>` name and date, one sentence, a **Past notes** link. | There is no note for this day. (P, `note-read-view.md`) |
| **Participant not found** (`404`) | Whole-page "Page not found" (as for the list). | As above |
| **Load failed** | Fallback `<h1>` "Note", message in the page status region, **Try again** outside it. | This note did not load: no connection. Try again. · This note did not load: something went wrong. Try again. (P) · then: This note still did not load: … (P) |
| **Signed out** (`401`) | Sign-in in place; back to the same note afterwards. | Owned by `session-timeout.md` |
| **Forced colours / 200% / 320 px** | Tags keep their border (`status-tags.md`); tick icons follow the text colour; rules are borders; everything wraps, including long unbroken strings. | – |

**Review panel states** (managers, To review; behaviour owned by `review-panel.md`):

| State | Copy |
|---|---|
| Default | Review (P) · Comment (optional) (V + GOV.UK convention) · hint: Shown in the daily report and record exports, and cannot be changed later (P; "can't" changed to "cannot" per `microcopy.md` voice rules) · You can enter up to 500 characters · Mark reviewed (V) |
| Typing / over limit (not yet an error) | You have 120 characters remaining · You have 12 characters too many (GOV.UK) |
| Busy (after 400 ms) | Button label: Marking reviewed… (P, the button's own verb) |
| Too long, on press | Comment must be 500 characters or less (P, `microcopy.md` pattern) |
| No connection / server error | Not marked reviewed: no connection. Try again. · Not marked reviewed: something went wrong. Try again. (P, `microcopy.md` "Not [done]" pattern) |
| `409`, reviewed by someone else meanwhile | This note was marked reviewed while you had it open. (P) · then the other review's lines · then, if a comment was typed: Your comment was not saved: {comment as plain text} (P) |
| `409`, edited since it was opened | This note was edited after you opened it. Read the latest version, then mark it reviewed. (P) |
| `409`, and the refetch also failed | This note has changed or was already reviewed. Check the latest version before marking it reviewed. (P) |
| Success | The form is replaced in place by: Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am · Comment: {comment} (omitted if none). The panel stays mounted although the note now refetches as Reviewed. |
| Unsent comment restored | Back on the note after Edit (then Save changes or Cancel), or after signing in again: the typed comment is in the box again (memory only; gone after a reload) |

---

## Interactions and focus

### Focus order (Tab), no custom keys anywhere

- **History:** skip link → header and nav (`app-shell-nav.md`) → back link → [managers: Write past-day note → Export
  record → Edit participant] → row 1 … row 30 (one stop per row) → Show older. Try again sits after the error text
  when shown.
- **Read view:** skip link → header and nav → back link → Edit → Version history → Past notes → [manager on To review:
  Comment → Mark reviewed]. On a manager's draft view: back link → Discard draft → Past notes. Text, tags and tick rows
  are never tab stops.

### What happens on each action

| Action | Result | Focus after | Announced |
|---|---|---|---|
| **Arrive on either screen** by an in-app link (push) | Scroll to top. Nothing renders in `<main>` until the data is ready, then everything at once. | The `<h1>` (`tabIndex={-1}`), once, when it mounts (the shell's "focus pending" flag). Not on the first full page load, not on re-render or refetch, not if the person already moved focus. Outline only under `:focus-visible`. [Research] Gatsby, n=5: https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/ | History: "Past notes Jane Citizen, heading level 1". Read view: "Sam Taylor Thursday 1 October 2026, heading level 1". |
| **Back to the list** (back link or browser Back, a POP) | Rows come from the in-memory cache (all pages loaded before); scroll position restored from the shell's in-memory map keyed by `location.key`. | The link of the row the person opened, with `preventScroll`, through the shell's `handle.returnFocus` (app-shell.md route-change rule). If that row is gone or the cache expired: newest 30 at the top and focus on the `<h1>`. | The restored row: "Thursday 1 October 2026, link, Submitted, Alex P., Edited". [Research] Baymard: over 90% of load-more sites lost the place (2016): https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/ |
| **Back to the read view** (POP, for example from Version history) | Scroll restored from the same in-memory map. | The `<h1>`, `preventScroll`. | The heading. |
| **Tap a row** | Push to `/participants/{id}/notes/{noteDate}` with no router state; the shell remembers the opener, the scroll position and the row key in memory. | Per the destination screen. | Destination heading. |
| **Show older** | `aria-disabled` at once (repeat taps ignored). Next 30 appended below; existing rows never move. | The link of the first new row (found by the previously last row's key, not an index). If `hasMore` is now false, the button is removed (focus is already safe). If nothing new arrived and the button went, focus the last row's link. [Standard] SC 2.4.3: https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html | The newly focused row (no "30 loaded" message). Slow (>1 s): "Loading older notes…" politely. |
| **Show older fails** | Rows unchanged; button available. | Stays on Show older. | The failure line, politely (`role="status"`). |
| **Try again** (first-load failure) | Old text cleared at once; button `aria-disabled`, label "Loading…" after 400 ms (`empty-loading-error.md`). | Success: the `<h1>`. Failure: stays on Try again. | Success: the heading. Failure: the "still did not load" line. |
| **Back link** | Pops when the previous entry is the destination; otherwise follows its `href`. Never disabled. | Per destination. | – |
| **Edit** | Replaces the read view with the edit form at `…/edit` (same back destination; an unsent review comment is kept in memory). | The edit form's `<h1>`. | "Editing submitted note (version 2) Sam Taylor Thursday 1 October 2026, heading level 1" |
| **Save changes / Cancel** (from the form) | Replaces the form with the read view. note-form.md invalidates `['notes', p, d]`, `['pastNotes', p]` and `['reviews']` (and `['me']` after Save changes), so the read view shows the new version and the Edited line, and the list and Flagged agree. No success banner. | The read view's `<h1>`. | The heading. |
| **Version history / Past notes link** | Push. Past notes from here is a new visit: list from the top. | Destination `<h1>`. | – |
| **Mark reviewed, comment over 500** | Nothing sent; inline error; `aria-invalid`. | The textarea. | Via focus: label, "Error: Comment must be 500 characters or less". |
| **Mark reviewed** | Button `aria-disabled` at once; textarea `readOnly`; "Marking reviewed…" after 400 ms. Sends `{versionNumber: <version on screen>, comment}` (trimmed; blank → `null`). | Stays on the button while busy. | "Marking reviewed…" via the page status region (app-shell.md component 3). |
| **Mark reviewed succeeds** (`201`) | The form is replaced in place by the review record; the panel stays mounted; the top status line changes silently from To review to Reviewed. Invalidate `['notes', p, d]`, `['reviews']` (both lists), `['me']` (exact; badge) and `['pastNotes', p]`. Forget the unsent comment. | The "Reviewed by …" line (`tabIndex={-1}`), because the pressed button has gone. | Via focus: "Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am". No extra live message. |
| **Mark reviewed fails** (network, `5xx`) | Message in the alert slot above the button; comment kept; nothing retried automatically. | Stays on the button. | The message, assertively (`role="alert"`, present from first render). |
| **`409 review.not_current`** | Refetch the note, then the three cases in States. If our own earlier press had landed (latest review is ours), treat as success. | Reviewed by someone else: the message (`tabIndex={-1}`). Edited since opened: stays on the button. | Via focus, or the alert slot. |
| **Discard draft** (manager) | Opens the native `<dialog>` (`showModal()`). | The dialog title (`tabIndex={-1}`, `confirm-dialog.md`); Go back returns focus to Discard draft. | "Discard the draft note for Jane Citizen?, dialog, Alex P. started it at 9:14 am. …" |
| **Discard confirmed** | `POST {base}/discard`; on success go to Today. | Per Today's spec. | Per Today's spec (Cross-screen issues). |
| **Switch apps and come back** (window focus) | List: refetched silently, rows keyed, focus kept. Read view: **not** refetched, so the text never changes under a reader; it refetches when reopened. | Unchanged. | Nothing. |

### Live regions (all in the DOM before anything is written into them)

| Screen | Region | Carries |
|---|---|---|
| Both | Page `<p role="status">` at the top of `<main>` | Loading line after 1 s; first-load failure text |
| History | `<p role="status">` after Show older | "Loading older notes…"; Show older failure |
| Both | Page status region (`PageStatus`, visually hidden, app-shell.md component 3) | "Marking reviewed…" and Try again's "Loading…" busy labels (the shared Button writes them here) |
| Read view | Review panel `role="alert"` slot above the button | Request errors and the edited-since-opened conflict |
| Read view | Visually hidden `aria-live="polite"` count (`conditional-reveal.md`) | "You have N characters remaining" after a pause |

No live role on the list, rows, tags, names or the review record. Background data changes are never announced.

---

## Accessibility checklist

**Headings**
- [ ] History: exactly one `<h1>` = "Past notes" caption + full name (accessible name "Past notes Jane Citizen"). No
      other headings needed.
- [ ] Read view: one `<h1>` = name + note date. `<h2>`s: "1. Goals", "2. Common items", "3. Guided notes", and
      "Review" (managers, To review). `<h3>`s under "2. Common items": "Every note" and each picked group's name.
      71.6% of screen-reader users move through long pages by headings first.
      [Research] WebAIM #10, n=1,539: https://webaim.org/projects/screenreadersurvey10/
- [ ] Fallback `<h1>` ("Past notes", "Note") on a failed load; "Page not found" on a 404.

**Landmarks and structure**
- [ ] `header` (banner), `nav aria-label="Main"`, `main id="main-content" tabindex="-1"` from the shell. The back link
      sits in the shell's before-main bar slot (app-shell.md 3a), outside landmarks, so "Skip to main content" passes
      it, and it is hidden with the route when signed out. [Convention] GOV.UK back link:
      https://design-system.service.gov.uk/components/back-link/
- [ ] No `<section>` with an accessible name, no `<article>`, no `<dl>`, no `role="feed"`.
- [ ] History list is `<ol role="list">` (`role="list"` keeps semantics in Safari/VoiceOver once bullets are removed).
      Tick lists are `<ul role="list">`.
- [ ] Every date and time is in `<time dateTime>`.
- [ ] `<html lang="en-AU">`; participant names carry `translate="no"`.

**Labels and names**
- [ ] Row link name is the date only; status and tags are its description (`aria-describedby`) and also follow it in
      reading order. [Convention] GOV.UK task list: https://design-system.service.gov.uk/components/task-list/
- [ ] No `aria-label` replaces visible text anywhere (SC 2.5.3; dictation users say what they see).
- [ ] Back link name = its visible label ("Past notes", "Note", "Today", "Flagged", "Participant"); chevron SVG is
      `aria-hidden="true" focusable="false"`.
- [ ] Review textarea has a visible `<label for>`; hint, count and error linked by `aria-describedby`;
      `aria-invalid` only after a failed check.
- [ ] Tick state in words for screen readers (", ticked" / ", not ticked"), shape plus words visually.

**Keyboard**
- [ ] Every action reachable with Tab in visual order; Enter follows links; Enter/Space presses buttons.
- [ ] No positive `tabindex`; programmatic targets use `tabIndex={-1}`.
- [ ] No `disabled` attribute anywhere on these screens; busy buttons use `aria-disabled` and keep focus.
- [ ] Focus never falls to `<body>` after Show older, Try again, Mark reviewed or a dialog closing.

**Screen reader**
- [ ] On arrival the heading is read once; on Back to the list the restored row is read.
- [ ] Separators: `<span aria-hidden="true"> · </span>` plus a visually hidden comma (unverified per screen reader;
      harmless either way).
- [ ] Manual passes: NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Android Chrome.

**Visual**
- [ ] Text contrast 7:1 or better (`foundations.md` tokens: text 19.6:1, secondary 9.0:1, tags 8.1:1 and 13.1:1).
- [ ] Focus ring: 3 px near-black outline, 2 px offset, `:focus-visible`, `Highlight` in forced colours; rows draw it
      on the link's `::after` inset 3 px around the whole row.
- [ ] No status by colour alone; tags keep their border in forced colours (`status-tags.md`).
- [ ] Nothing sticky or fixed; nothing animates; no spinners or skeletons.
- [ ] Viewport meta does not block zoom; `<meta name="format-detection" content="telephone=no">` (set in `index.html`
      by app-shell.md §8) so numbers in notes are not turned into call links on iOS.
- [ ] Text selection and the context menu are not blocked.

**WCAG 2.2 AA criteria this screen must meet:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.3.3
Sensory Characteristics · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11
Non-text Contrast · 1.4.12 Text Spacing · 1.4.13 Content on Hover or Focus (nothing on hover) · 2.1.1 Keyboard · 2.2.1
Timing Adjustable (nothing timed) · 2.2.2 Pause, Stop, Hide (no motion) · 2.4.1 Bypass Blocks · 2.4.2 Page Titled ·
2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not
Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (44 px floor, 48 px buttons, 56 px rows, A32) ·
3.1.1 Language of Page · 3.2.1 On Focus · 3.2.2 On Input · 3.2.3 Consistent Navigation · 3.2.4 Consistent
Identification · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 4.1.2 Name, Role,
Value · 4.1.3 Status Messages. https://www.w3.org/TR/WCAG22/

---

## Acceptance criteria

**History list**
1. Rows appear newest first by note date, in API order, never re-sorted; the first load requests no `before` and shows
   at most 30 rows.
2. **Show older** shows only while the last response had `hasMore: true`; pressing it requests `before=<last row's
   noteDate>` and appends up to 30 rows below without moving existing rows.
3. Scrolling to the bottom without pressing Show older makes no request (checked in the network log).
4. No count, total, page number or "end of list" text appears anywhere on the screen.
5. After Show older succeeds, `document.activeElement` is the link of the first new row; when no older notes remain the
   button is no longer in the DOM and focus is not on `<body>`.
6. Pressing Show older twice quickly sends one request; the button never gets the `disabled` attribute.
7. With the network offline, Show older shows "Older notes did not load: no connection. Try again." within about 2
   seconds, the rows already shown remain, and focus stays on the button; a second failure shows "Older notes still
   did not load: no connection. Try again.".
8. A slow first load (throttled over 1 s) shows nothing for the first second, then "Loading notes…"; no spinner or
   skeleton appears at any point.
9. Every row is at least 56 px tall and a tap anywhere in it opens that note; the row link's accessible name is the
   date only and its description contains the status, author and tags.
10. A worker never sees To review or Reviewed tags; a manager sees them where `flagStatus` says so; a Draft row never
    shows any tag.
11. Workers see no Write past-day note, Export record or Edit participant; managers see all three, except Write
    past-day note for an archived participant.
12. With no notes the screen shows "No notes yet for Jane Citizen." with no list and no Show older.
13. After loading 90 rows, opening the 75th and pressing Back (browser or back link), all 90 rows show at once from
    memory (a silent background refetch may run, and never blanks or moves them), the scroll position matches, and
    focus is on the 75th row's link.
14. Arriving from a "Past notes" link always starts at the newest 30, scrolled to the top, focus on the `<h1>`.

**Read view**
15. Nothing from the note renders until the participant and the note have loaded; the first render contains the name,
    date, all ticks and the Guided notes text together.
16. After an in-app navigation, focus is on the `<h1>`, whose accessible name is the full name followed by the long
    note date; on a full page load focus is left where the browser puts it.
17. Every goal in the snapshot appears in snapshot order with ticked or not ticked, as text, not form controls; a
    screen reader reads "{item}, ticked" or "{item}, not ticked".
17a. Common items show Every note (when it has items) and only the groups picked on the version shown, each under an
    `<h3>` with its name, in configured order, with every item in those groups ticked or not ticked. A fixture with
    an unpicked group shows no trace of it; the question "Which of these happened?" and the copied-picks line never
    appear. With no group shown, the section reads "No common items set".
18. A 20,000-character note with blank lines, long unbroken strings and the text `<b>hi</b>` shows in full, keeps every
    line break, shows the tags literally and causes no horizontal scroll at 320 px.
19. "Submitted" shows the time alone when the Melbourne date of `firstSubmittedAtUtc` equals the note date, otherwise
    "Fri 2 Oct 2026, 8:05 am" style.
20. A past-day note shows the past-day line in place of "Written by"; Edited, Flagged, To review and Reviewed lines
    appear only when they apply, in the 11.3 order.
21. A worker's DOM contains no To review, Reviewed, review comment, Version history link or review panel.
22. **Edit** shows only for the author and managers on a Submitted note and is never shown disabled; **Version
    history** shows only for managers.
23. The note text does not change when the window regains focus (no request on focus); it is refetched when the page
    is opened again.
24. A manager on a To review note sees the review panel at the bottom; pressing Mark reviewed with a 501-character
    comment sends nothing, shows "Comment must be 500 characters or less" and focuses the textarea.
25. A successful Mark reviewed replaces the form with "Reviewed by {me} on {date, time}" (and the comment), focuses that
    line, decrements the Flagged badge, and the participant's history row then shows Reviewed.
26. Two managers on the same To review note: when the second presses Mark reviewed, they see "This note was marked
    reviewed while you had it open." and their typed comment under "Your comment was not saved:".
27. A worker opening someone else's draft sees only the `<h1>`, the sentence ending "Only Alex P. can finish it." and
    the Past notes link; a manager also sees the draft content, a read-only "Flag for manager" row and **Discard
    draft**, and never the Flagged tag.
28. Discard draft opens a modal dialog with focus on its title; Go back or Escape returns focus to Discard draft.
28a. The read view behaves identically when opened from Today, Past notes or Flagged; only the back link's label
    differs. No "Earlier reviews" block and no "Go to flagged notes" link appear in the default build.
28b. After a successful Mark reviewed, the note refetches as Reviewed and the panel (with the record and its focus)
    is still in the DOM.
28c. Type a comment, press Edit, then Cancel: the comment is back in the box. Reload: it is gone (memory only).
28d. A busy Mark reviewed reads "Marking reviewed…" after 400 ms, never "Saving…".

**Both screens**
29. Nothing is `position: sticky` or `fixed` on either screen; the top bar scrolls away.
30. Page titles are exactly "Grow2Notes – Past notes" and "Grow2Notes – Note"; URLs contain only IDs and dates;
    `history.state` holds only React Router's key (no back object, no names, no note text).
31. After using both screens, `localStorage`, `sessionStorage`, IndexedDB and Cache Storage hold no note data, scroll
    positions or names (React Router `<ScrollRestoration>` is not used).
32. Wrong-person test (Playwright, throttled): open participant A's list and a note, go back to Today, open
    participant B's; A's name never appears in B's DOM at any moment.
33. At 320 px wide with 200% text, and in Windows forced colours, nothing is clipped, truncated or scrolls sideways;
    tags keep their border and focus rings are visible in forced colours.
34. Formats: "Thursday 1 October 2026" contains U+00A0 between "1" and "October"; times read "4:42 pm" with U+00A0;
    12:00 shows "midday". No "Sept", no comma after the weekday, no relative times.
35. axe-core (Playwright) reports no violations for every state in States; no visible string contains the parent
    company's name (D42), "delete", "log in" or a US spelling (`microcopy.md` copy test).

---

## Conflicts resolved

Where the component specs disagreed, what this screen uses and why.

1. **Sticky top bar.** `note-identity-header.md` makes the bar sticky on the read view and history list, with a
   compact name once the `<h1>` scrolls away. `note-read-view.md` ("nothing sticky over the text") and
   `foundations.md` ("nothing is fixed or sticky except the note form's top bar") disagree. **Chosen: static on both
   screens, no compact name.** These are reading screens with no save indicator to keep in view; a sticky bar costs
   reading height at 200% zoom and is the textbook cause of 2.4.11 failures. [Standard]
   https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html · [Convention] NN/g sticky headers:
   https://www.nngroup.com/articles/sticky-headers/
2. **Where the note date sits.** `note-identity-header.md` puts it in a `<p>` under an `<h1>` that holds only the
   name; `note-read-view.md` puts it inside the `<h1>`. **Chosen: inside the `<h1>` on the read view.** The focused
   heading then confirms which day's note opened ("Sam Taylor Thursday 1 October 2026"), which matters after tapping
   a date in the list and keeps the read view's heading distinct from the list's ("Past notes Jane Citizen"). Visual
   result is the same. [Opinion]
3. **Heading sizes.** `note-identity-header.md`: 2rem / 2.5rem name, 1.125rem secondary date. `foundations.md`: h1
   28/32 px; the note date in body size and text colour "because it is part of the right-day check". **Chosen:
   foundations** (the app-wide type scale; four sizes in use).
4. **Breakpoint.** `note-identity-header.md` uses 48em; `foundations.md`, `chronological-list.md` and
   `primary-actions.md` use 40rem/40em. **Chosen: 40rem.**
5. **Body size.** `note-read-view.md` recommends 19 px (GOV.UK); `foundations.md` sets 18 px app-wide. **Chosen: 18
   px**, already above the critical print size, and consistent with the form the note was written in.
6. **Metadata size and colour.** `note-read-view.md`: metadata at body size in body colour. `foundations.md` (4.4
   per-screen note): author, times and past-day line at 16 px in `--colour-text-secondary` (9.0:1, not light grey).
   **Chosen: foundations for metadata, with user-written text (flag reason, review comment) in `--colour-text`.**
   Both concerns are met: nothing is below 16 px or 7:1, and what the writer wrote reads as content.
7. **Date and time formats.** `note-read-view.md` spells stamps out ("Friday 2 October 2026, 9:01 am"),
   `status-tags.md` and `status-messages.md` use "Fri 2 Oct 9:01 am", `chronological-list.md` uses "1 Oct 2026, 5:03
   pm". **Chosen: `microcopy.md`'s tokens**: `dateLong` for headings, link text and sentences; `stamp` (time alone on
   the note date, otherwise "Fri 2 Oct 2026, 9:01 am") next to times. One formatter, built from parts.
8. **Past-day line.** `status-tags.md`, `note-read-view.md` and `status-messages.md` keep 3.8's order ("written on …
   by …"); `microcopy.md` normalises to 11.3's order. **Chosen: "Past-day note, written by Jo Smith (manager) on Sat 3
   Oct 2026, 10:14 am"**, because the brief allows wording changes where the microcopy research directs, and screen and
   file then say the same thing. The past-day line replaces "Written by" so the author is not named twice
   (`note-read-view.md`).
9. **Read-view line order.** `status-tags.md` fixes a tag order for list rows (Flagged → To review | Reviewed → Edited
   → Past-day note); `note-read-view.md`'s anatomy follows the report. **Chosen: the report order (11.3) on the read
   view; the status-tags order in list rows.**
10. **Load-failure wording.** `chronological-list.md` ("This list did not load. Check your connection, then select Try
    again."), `empty-loading-error.md` ("Could not load past notes." + cause sentence), `note-read-view.md` ("The note
    didn't load…") and `microcopy.md` §9 ("[Thing] did not load: [cause]. Try again."). **Chosen: `microcopy.md`'s
    canonical pattern** (it mirrors design.md's own "Not saved: no connection."), plus `empty-loading-error.md`'s
    "still" variant so a repeat failure is re-announced.
11. **Show older while loading.** `primary-actions.md` and `empty-loading-error.md` change the label to "Loading…"
    after 400 ms; `chronological-list.md` keeps the label and uses the status line after 1 s. **Chosen:
    `chronological-list.md`**: a focused control's name stays stable (name changes mid-focus are announced
    inconsistently), and the status line under the button is already needed for its failure message. Try again keeps
    `empty-loading-error.md`'s behaviour.
12. **Retries.** `chronological-list.md` keeps TanStack's default (3 with back-off); `empty-loading-error.md` sets one
    silent retry for network, timeout and `5xx` only. **Chosen: one retry**, so an offline phone fails in about a
    second instead of sitting on "Loading…". [Convention] https://tanstack.com/query/v5/docs/framework/react/guides/query-retries
13. **A missing note (404).** `note-read-view.md`: "There is no note for this day." with Past notes;
    `empty-loading-error.md`: whole-page "Page not found". **Chosen: split by cause.** Participant loaded but note
    gone → the specific sentence under the name and date (the URL was valid; "check the web address" would be wrong).
    Participant itself 404 → "Page not found" (also hides other organisations' IDs, 9.2).
14. **After Mark reviewed.** `note-read-view.md`: focus the `<h1>` and announce "Note for X marked reviewed.";
    `review-panel.md` and `primary-actions.md`: replace the form with the review record and focus it. **Chosen: the
    record in place**: focus moves the shortest distance from the removed button, the record states exactly what was
    stored, and focus itself announces it, so no extra live message is needed. [Standard] SC 2.4.3, 4.1.3.
15. **Mark reviewed busy delay.** `note-read-view.md` 1 s; `review-panel.md` and `primary-actions.md` 400 ms.
    **Chosen: 400 ms**, the app-wide button rule.
16. **Mark reviewed failure wording and busy label.** `note-read-view.md`: "Not marked reviewed: no connection. Check
    your connection and try again."; `review-panel.md`: "Not saved: no connection. Try again." **Chosen: "Not marked
    reviewed: no connection. Try again."**, and the busy label **"Marking reviewed…"** (an earlier draft of this file
    said "Saving…"). A label and a failure line use the button's own verb (microcopy.md glossary), and the pattern
    ends "Try again."
17. **409 messages.** `note-read-view.md` has two cases; `review-panel.md` has three plus "our own earlier press
    landed"; `primary-actions.md`/`microcopy.md` give one general string. **Chosen: `review-panel.md`'s cases**, with
    the general string only when the refetch fails.
18. **Where the flag reason sits.** `review-panel.md` assumes it follows the Guided notes (just above the panel);
    `note-read-view.md` puts it at the top, as the report does. **Chosen: top**: the read view owns the layout, a
    manager hears why they are here before the content, and screen and report match.
19. **Earlier reviews, and "Go to flagged notes".** `review-panel.md` (P) and flagged.md showed an "Earlier reviews"
    block when a note was re-flagged and a "Go to flagged notes" link after a review; `note-read-view.md` shows the
    latest review only, as the report does, and no link. **Chosen: neither in the default build, on every route**;
    both are owner questions (Open questions 1, 2). Earlier reviews remain in the Reviewed tab; the back link "Flagged"
    already returns to the list. One read view, so the same URL never behaves differently by where it was opened.
20. **Reviewed record placement.** `review-panel.md` puts the record at the bottom in the Reviewed state;
    `note-read-view.md` puts it in the top status lines. **Chosen: top status lines** (report order); the bottom
    record appears only straight after this manager's own review, replacing the form.
21. **Reviewed wording.** `status-tags.md` ("by Jo Smith, Fri 2 Oct 9:30 am", from 11.3) vs `review-panel.md` ("by
    Jo Smith on Fri 2 Oct 2026, 9:30 am"). **Chosen: "by … on …" with the date always shown**: it is design.md's own
    M3 wording ("Reviewed by … on …") and reads correctly whatever the day.
22. **Manager's view of someone else's draft.** `note-read-view.md`: status line "Draft · Alex P. · started 9:14 am";
    `microcopy.md`: the same sentence a worker sees. **Chosen: the sentence** (4.2's own words), then the read-only
    content. One message for one situation.
23. **Discard draft trigger.** `note-read-view.md`: warning style in the action row; `primary-actions.md` and
    `confirm-dialog.md`: the trigger is secondary, red only on the dialog's final button. **Chosen: secondary
    trigger**, which keeps red rare and meaningful.
24. **Discard dialog initial focus.** `primary-actions.md`: "Go back"; `confirm-dialog.md` and
    `note-identity-header.md`: the title. **Chosen: the title** (dialog owner; the APG's "static element at the top"
    option, and a double Enter cannot confirm). https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
25. **After Save changes.** `primary-actions.md` proposes a "Changes saved" banner; `status-messages.md` adds none.
    **Chosen: none**, focus to the `<h1>`: design.md specifies a message only for Submit, and the Edited line shows
    the result.
26. **Row size and weight.** `chronological-list.md`: 44 px rows, weight 600. `foundations.md`: 56 px for history
    rows, weights 400/700 only. **Chosen: foundations.**
27. **Going back to the list.** `app-shell-nav.md`: `<ScrollRestoration>` and focus the `<h1>` on POP;
    `chronological-list.md` and `participant-list-rows.md`: in-memory place, focus the opened row, no
    `<ScrollRestoration>` (it writes to `sessionStorage`). **Chosen: in-memory place and the opened row** (D22, 9.6;
    Baymard place-keeping).
28. **An extra list heading.** `chronological-list.md` suggests an `<h2>` such as "Notes" before the list;
    `note-identity-header.md` uses only the captioned `<h1>`. **Chosen: no extra heading** (new copy the design does
    not need; the list follows the `<h1>` and the manager actions directly).
29. **Flag line wording.** `microcopy.md` 4.4 says "Flagged for manager" with the reason; `status-tags.md` fixes the
    tag word "Flagged". **Chosen: tag "Flagged" + "Reason: …"** so the tag word is identical on Today, the list and
    here (SC 3.2.4), and "Reason" is the form's own label.
30. **Comment hint contraction.** `review-panel.md`'s hint says "can't"; `microcopy.md` bans negative contractions in
    new copy. **Chosen: "cannot".**
31. **How the screen knows where it was opened from.** This file and today.md passed `{ back: { key, path } }` in
    router state; app-shell.md forbids anything in `history.state` but its key and `inScreen` (some browsers write it
    to disk). **Chosen: the shell's in-memory opened-from record**, keyed by `location.key` and copied across replaces
    (app-shell.md). After a reload the back link falls back to Today.
32. **Note query key.** This file and flagged.md used `['note', p, d]`; version-history.md used `['notes', p, d]`.
    **Chosen: `['notes', participantId, noteDate]`** everywhere, so one prefix invalidation refreshes the read view,
    the version list and each version.
33. **Who owns the read view.** flagged.md and this file both specified it and disagreed on the review block, the
    "Go to flagged notes" link, the busy label, the line order, the panel's lifetime and the unsent comment.
    **Chosen: this file owns it for every route**; the line order is the report's (11.3); the panel stays mounted
    once rendered; one unsent-comment rule (Components, item 8).

---

## Tensions with decisions

Recorded once; no change recommended.

- **D20 (every worker reads every note, including flag reasons) and need-to-know access.** The OAIC guide advises
  limiting internal access "on a 'need to know' basis". D20 follows from D13/D14; A13 already restricts earlier
  versions to managers. [Standard] https://www.oaic.gov.au/privacy/privacy-guidance-for-organisations-and-government-agencies/handling-personal-information/guide-to-securing-personal-information
- **D7, D10, A4 (one optional tick per item).** In the read view "not ticked" cannot be told apart from "missed"
  (`checkbox-list.md`). The read view shows the design's "ticked / not ticked" as the report does.
- **D47 (groups not picked do not appear).** A group missing from the read view cannot show "did not happen" apart
  from "forgot to tick it", which extends the tension above (`group-picker.md`).
- **A5 / 6.3 (name only, no second identifier) and D-scope (no photo).** SAFER 1.3 says names alone are not sufficient;
  a photo in the banner was associated with fewer wrong-patient orders (aOR 0.57, single site, observational). On a
  reading screen the risk is lower than at Submit. [Convention] https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf
  · [Research] https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2772798
- **4.4 "30 at a time, then Show older" (design text, not a decision).** NN/g finds long incremental lists weak for
  goal-oriented finding; a note six months back takes about six presses. Managers have date routes (Report, Export
  record). [Research] https://www.nngroup.com/articles/infinite-scrolling-tips/

---

## Open questions for the owner

1. **"Earlier reviews" on a note flagged again after a review (A15).** review-panel.md proposed an `<h3>` "Earlier
   reviews" block above the comment box listing each earlier review. Not built unless approved; today the read view
   shows the latest review only, as the report does, and every review stays in Flagged > Reviewed.
2. **"Go to flagged notes" link after Mark reviewed.** review-panel.md proposed a link under the review record that
   returns to the Flagged list. Not built unless approved; the back link "Flagged" (from Flagged) or the nav item
   already does this.
3. **Past-day note on Past notes rows.** A11 says the past-day line shows "everywhere it appears", but the history
   API (6.3) returns no `isPastDayNote`, so list rows show no Past-day note tag; the read view shows the line. Keep it
   that way, or add the field to the history response?
4. **Proposed strings:** "Review", the comment hint, "Marking reviewed…", the 409 messages, "Your comment was not
   saved:", "Nothing written yet.", "There is no note for this day.", "Loading notes…", "Loading note…", the load and
   Show older failure lines.
   **Answered 9 October 2026 (D67):** approved as written; any of them can still be changed later in the copy module.
   A (P) string added after that date still needs the owner's approval.
