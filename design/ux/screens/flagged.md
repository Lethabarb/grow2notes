# Flagged notes

Screen spec for design.md section 4.6 (managers only). It composes the researched component specs in
`../components/` into one screen and settles the points where they disagree. It adds no feature, field, setting,
notification, screen or data. Every view, list, count and action here is already in design.md 4.6, 4.4, 6.4 and 6.8.

**The note itself** (read view and review panel) is owned by `participant-notes.md` for every route that opens it.
This file adds only the back link's label, "Flagged", and the list behaviour around it.

Copy marks: **(V)** word for word from design.md. **(N)** design.md wording with only the date, time or punctuation
normalised to `microcopy.md`. **(P)** proposed, because design.md has no string for that state. Every (P) string here
on 9 October 2026 was approved as written (D67); a (P) string added later still needs the owner's approval.
Evidence grades: **[Research]**, **[Standard]**, **[Convention]**, **[Opinion]**, as in the component files.

---

## Purpose and who uses it

**Purpose (V, 4.6):** "make sure every flag is seen and dealt with" (D17, D18). A support worker ticks **Flag for
manager** with a reason; once the note is submitted it joins the **To review** queue and the count badge. A manager
reads the note, edits it if needed, and presses **Mark reviewed** with an optional comment. One review is enough,
because managers are equal (D24, A14).

**Who:** managers only (section 2). Workers have no Flagged nav item, and a worker who types the URL gets
"Page not found" from the role guard (`app-shell-nav.md`; the API returns 403/404, 9.2).

**Context of use:** a few managers, on laptops and phones, usually in short sessions between other work. The queue is
small (under 20 workers, at most one note per participant per day), so the To review list comes back whole. A flag
is a request to read, not an incident (D16), so nothing on this screen uses red or alarm styling.

**The screen has two views on one route, plus the note it opens:**

| View | URL (IDs, dates and one fixed keyword only, 4.0) | Contents |
|---|---|---|
| To review (default) | `/flagged` | Oldest first, whole list (6.4) |
| Reviewed | `/flagged?view=reviewed` | Newest first, 30 at a time, **Show older** (6.4) |
| Review a note | `/participants/{participantId}/notes/{noteDate}` (the shared note URL, note-form.md Routes) | The read view (4.4) with the review panel underneath (4.6 item 3), both owned by participant-notes.md |

Page titles: "Grow2Notes – Flagged notes" for both list views; "Grow2Notes – Note" for the note (4.0, fixed
page-name list in `app-shell-nav.md`). No title, URL or router state ever holds a name or note text.

**Primary action (V):** Mark reviewed.

---

## Layout - phone (about 375 px)

Everything is one column with 16 px side gutters (`foundations.md`). Nothing is sticky. The header scrolls away with
the page.

### List view: To review (default)

```
+-------------------------------------+
| Grow2Notes                Account v |  <header>, not sticky
| Today  Flagged [3]  Report  Manage  |  <nav aria-label="Main">; Flagged is current:
|        ==========                   |  bold + 4 px bar; pill [3] is the badge
+-------------------------------------+
| Flagged notes                       |  <h1>, static, focused on arrival
|                                     |
|   To review (3)   |    Reviewed     |  ViewLinks: <nav aria-labelledby=h1> <ul> 2 links
|   =============   |    --------     |  current: bold, body colour, 4 px action-blue bar
|-------------------------------------|  other: link colour, underlined
|                                     |  <p role="status"> (empty unless slow or failed)
| Sam Taylor                          |  row link: name + note date (dateLong)
| Tuesday 29 September 2026           |    whole row is the target (min 56 px)
| Priya Nair · flagged 4:42 pm        |  meta line (aria-describedby target)
| Reason: Seemed upset about moving   |  reason, up to the first line break,
| house.                              |    wraps, never clipped
|-------------------------------------|
| Jane Citizen                        |
| Wednesday 30 September 2026         |
| Alex P. · flagged Thu 1 Oct 2026,   |  date shown because flagged on a later
| 9:01 am                             |    day than the note date
| Reason: Did not want to get out of  |
| bed. Said her back hurt.            |
|-------------------------------------|
| Lee Wong                            |
| Thursday 1 October 2026             |
| Alex P. · flagged 11:20 am          |
| Reason: Missed lunch.               |
| Flag removed in a later edit        |  (V) plain text line, not a tag
|-------------------------------------|
```

### List view: Reviewed

```
| Flagged notes                       |
|                                     |
|   To review (3)   |    Reviewed     |  count still shown: the To review query
|   -------------   |    ========     |    runs on both views
|-------------------------------------|
| Jane Citizen                        |
| Monday 28 September 2026            |
| Alex P. · reviewed by Jo Smith on   |
| Tue 29 Sep 2026, 9:30 am            |
| Reason: Fell over in the kitchen.   |
| Comment: Called her sister. OT      |  full comment, line breaks kept
| visit booked.                       |
|-------------------------------------|
| ... up to 30 rows                   |
|                                     |
| [            Show older           ] |  secondary button, full width, 48 px
|                                     |  <p role="status"> (Show older messages)
+-------------------------------------+
```

### Review a note (opened from a To review row)

The shared read view with the review panel at the bottom, exactly as `participant-notes.md` layout B draws it. The
only difference on this route is the back link, which reads **‹ Flagged** (in the shell's before-main bar slot). After
Mark reviewed the form is replaced in place by the review record (participant-notes.md). No "Earlier reviews" block
and no "Go to flagged notes" link in the default build (owner questions in participant-notes.md).

---

## Layout - laptop

The markup, order and words are identical at every width (`microcopy.md` 6). From the one breakpoint,
`@media (min-width: 40rem)` (`foundations.md`), only these change:

- **Container.** The header and page container are centred at `--page-max` (60rem). The content column is
  `--measure` (40rem), left-aligned, so the nav and the content share a left edge. This screen never uses the full
  60rem: the extra width belongs to the setup screens (4.0). Lists, the read view and the panel all sit in the
  40rem column, so tags, reasons and comments stay next to the row they describe (`status-tags.md`).
- **Type and spacing.** h1 32 px, h2 24 px, body stays 18 px; gutters 32 px, section gaps 48 px.
- **ViewLinks.** Each of the two items is a fixed, equal width of about 12em, left-aligned under the h1, not
  stretched across the column (`tabs-segmented.md`).
- **Rows.** Same two-to-four-line rows. Hover (only under `@media (hover: hover)`) tints the whole row with
  `--colour-hover` and thickens the link underline. No table and no right-hand columns.
- **Buttons.** Show older, Try again and Mark reviewed are as wide as their label (Try again at least 8rem) and
  left-aligned (GOV.UK button guidance).
- **Review view.** As participant-notes.md: one column, metadata above the note, panel below it. No sidebar "review
  pane": it would split reading order from visual order (SC 1.3.2) and is not in the design.

---

## Components, in order

Each entry links to its component spec. Only the settings specific to this screen are stated here; everything else
follows the component spec as written, except where "Conflicts resolved" says otherwise.

### 1. App shell header and the Flagged nav item with its badge

[`../components/app-shell-nav.md`](../components/app-shell-nav.md),
[`../components/notification-badge.md`](../components/notification-badge.md)

- **Flagged** is the current item on both list views (`aria-current="page"`, bold plus a 4 px bar). Match on
  `pathname === '/flagged'`; the `?view=` keyword does not change it. On the note screen no nav item is current.
- Badge: digits only in a near-black pill (`--colour-badge`), `aria-hidden="true"`, plus visually hidden
  ", 3 to review". Accessible name "Flagged, 3 to review". Hidden at 0. Exact number, no "99+". No live region and
  no animation. [Convention] MoJ notification badge,
  https://design-patterns.service.justice.gov.uk/components/notification-badge/ ; [Research, practitioner] Roselli,
  aria-label does not reliably translate, https://adrianroselli.com/2019/11/aria-label-does-not-translate.html
- Source: `me.toReviewCount` (6.2). Refreshed on every pathname change, on window `focus` and `visibilitychange`
  (6.8), and after Mark reviewed succeeds. Never polled.

### 2. Page heading

[`../components/app-shell-nav.md`](../components/app-shell-nav.md) (`PageHeading`)

- `<h1 id="page-heading" tabindex="-1">Flagged notes</h1>` **(V page name)**. Static, so it renders at once and
  takes focus on arrival.
- It names the ViewLinks landmark (item 3).

### 3. View switch: To review (n) | Reviewed

[`../components/tabs-segmented.md`](../components/tabs-segmented.md)

- `ViewLinks`: `<nav aria-labelledby="page-heading">` holding a `<ul>` of two React Router `<Link>`s. Not ARIA tabs,
  not a segmented button group, not a switch. [Convention] GOV.UK "do not use the tabs component as a form of page
  navigation", https://design-system.service.gov.uk/components/tabs/ ; MoJ sub-navigation,
  https://design-patterns.service.justice.gov.uk/components/sub-navigation/ ; [Research] NN/g on mixing navigation
  and in-page tabs, https://www.nngroup.com/articles/tabs-used-right/
- Targets: `/flagged` (default, no parameter) and `/flagged?view=reviewed`. Pushed with `preventScrollReset`, so
  Back, reload and returning from a note keep the view. Any other `view` value shows To review and the URL is
  corrected with `replace`. [Research] Baymard back-button expectations,
  https://baymard.com/blog/back-button-expectations
- Labels: **"To review (3)"** (V) once the To review list has loaded, including **"To review (0)"**; plain
  **"To review"** before the number is first known or if the first load failed. **"Reviewed"** (V), never a count.
  The number is plain text in round brackets, never a pill.
- The "(n)" comes from the length of the To review list (authoritative), not from `me.toReviewCount`. Both are
  invalidated together after Mark reviewed; if they differ for a moment, the list wins.
- Current item: body-text colour, bold, no underline, 4 px `--colour-action` bar drawn as a `border-block-end` (so it
  survives forced colours). Other item: link colour, underlined. Equal widths. Never disabled, never sticky, no
  animation. 48 px tall. At 200% text the two items wrap onto two rows rather than scroll sideways.
- Focus stays on the activated link after switching. No live announcement.

### 4. To review list

[`../components/chronological-list.md`](../components/chronological-list.md) (`ChronoList`, **whole** mode),
[`../components/status-tags.md`](../components/status-tags.md) (`MetaLine`)

- Data: `GET /api/reviews?status=toReview` (6.4), oldest first, rendered in API order. Query key
  `['reviews', 'toReview']`. Runs on **both** views because it supplies the count.
- `<ol role="list">`, one `<li>` per flagged note. React key: `participantId + '/' + noteDate` (unique in this list,
  D6). Each row link carries `data-return-key` with the same key and pushes no router state; the route declares
  `handle: { returnFocus: { fallback: 'sameIndex' }, backKey: 'flagged' }` (app-shell.md route-change rule).
- **Row link** (one `<a href>` per row, `::after` overlay makes the whole row the target, min 56 px):
  participant's full name on line 1, note date in `dateLong` on line 2, both inside the link, separated by a visually
  hidden comma. Accessible name: "Sam Taylor, Tuesday 29 September 2026". Goes to the shared note URL. [Convention]
  GOV.UK task list (whole-row link, status by `aria-describedby`),
  https://design-system.service.gov.uk/components/task-list/
- **Meta line** (`MetaLine`, its `id` referenced by the link's `aria-describedby`):
  `{authorDisplayName} · flagged {stamp(flaggedAtUtc, noteDate)}`. The time alone when the Melbourne date of the
  flag equals the note date ("flagged 4:42 pm"), otherwise `dateTime` ("flagged Thu 1 Oct 2026, 9:01 am")
  (`microcopy.md` reference-day rule). The middle dot is `aria-hidden` with a visually hidden ", ".
- **Extra lines**, plain `<p>` in reading order, not part of the description:
  - "Reason: {text up to the first line break}" (V "first line of the flag reason"). Shown in full, wrapping, never
    clamped or ellipsised.
  - "Flag removed in a later edit" (V) when `flagRemovedLater` is true.
- **No tags** on these rows: the current view link already names the state (`status-tags.md`). No actions inside
  rows, no swipe, no colour-only marking.
- No Show older: the list is returned whole (6.1).

### 5. Reviewed list

[`../components/chronological-list.md`](../components/chronological-list.md) (`ChronoList`, **paged** mode)

- Data: `GET /api/reviews?status=reviewed&before=` (6.4), newest first, 30 at a time, `hasMore`.
  `useInfiniteQuery` with key `['reviews', 'reviewed']`, cursor = last row's `reviewedAtUtc`. Enabled only when the
  Reviewed view is shown; then kept in memory.
- React key and place-keeping key (`data-return-key`): `participantId + '/' + noteDate + '/' + reviewedAtUtc`. A
  note reviewed twice (A15) appears twice in this list, so participant and date alone are not unique.
- **Row link:** the same as To review (name, `dateLong` note date). Two rows for the same note link to the same
  place, which is fine under SC 2.4.4.
- **Meta line:** `{authorDisplayName} · reviewed by {reviewedBy} on {dateTime(reviewedAtUtc)}`, always with the
  date ("Alex P. · reviewed by Jo Smith on Tue 29 Sep 2026, 9:30 am"). Same words as the review record (item 9).
- **Extra lines:** "Reason: {first line}" and, when there is a comment, "Comment: {comment}" in full, line breaks
  kept (`white-space: pre-wrap`), wrapping. No "flagged" time: the Reviewed API returns none.
- **Show older** (V): a secondary `<button type="button">` after the list, full width on phones. Only the button
  loads more; never on scroll; no counts or page numbers. [Research] NN/g, Load More reduces infinite-scroll
  problems, https://www.nngroup.com/articles/infinite-scrolling-tips/ ; [Research] Baymard (2016) load-more findings
  and 15 to 30 per batch on mobile,
  https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/ ; [Convention] GOV.UK
  pagination, https://design-system.service.gov.uk/components/pagination/
  - While loading: `aria-disabled="true"`, repeat presses ignored, label unchanged, `cursor: progress`. After 1 s
    the status line under it says "Loading older notes…" (P).
  - On success: focus moves to the link of the first new row. [Standard] SC 2.4.3,
    https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html ; [Opinion] practitioner majority
    (Hovhannisyan, https://www.aleksandrhovhannisyan.com/blog/load-more-button-focus/).
  - When `hasMore` becomes false the button is removed (focus is already on the first new row). If the button
    disappears with no new rows, focus moves to the last row's link.
  - On failure: rows already shown stay, focus stays on the button, status line says the failure (see States).

### 6. Loading, empty and error region (one per view)

[`../components/empty-loading-error.md`](../components/empty-loading-error.md) (`LoadRegion`),
[`../components/microcopy.md`](../components/microcopy.md) (wording)

- Each view's list area has one `<p role="status">` rendered from the first render and never conditionally
  mounted. It holds the loading line after 1 s, then any first-load error. Nothing shows under 1 s. No spinner, no
  skeleton. [Research, expert estimate] NN/g response-time limits,
  https://www.nngroup.com/articles/response-times-3-important-limits/ ; [Research] Viget skeleton study (n = 136,
  no significance tests), https://www.viget.com/articles/a-bone-to-pick-with-skeleton-screens/ ; [Research,
  practitioner AT testing] O'Hara, live regions must already exist,
  https://www.scottohara.me/blog/2022/02/05/are-we-live.html
- **Try again** is a secondary `<button>` outside the status element, never `disabled`; `aria-disabled` while
  running, label "Loading…" after 400 ms.
- The empty sentence shows only after the data has arrived (`isSuccess`), as a plain `<p>`. [Research] NN/g on
  misleading empty states, https://www.nngroup.com/articles/empty-state-interface-design/
- Queries: `networkMode: 'always'`, one silent retry for network, timeout or 5xx only, 10 s timeout, and
  `isLoadingError` (never `isError`) decides the error state, so a failed background refresh never replaces rows
  already shown. [Convention] TanStack network mode,
  https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
- **Screen-specific override:** the To review list query keeps `refetchOnWindowFocus: true` and refetches on mount
  (`staleTime: 0`), so the "(n)" and the badge refresh on the same triggers (4.6, 6.8). The Reviewed query does the
  same; TanStack refetches its loaded pages in order and stable keys keep the DOM nodes.

### 7. Back link (review view)

[`app-shell.md`](app-shell.md) component 3a (shared BackLink), [`../components/note-identity-header.md`](../components/note-identity-header.md)

- The shared BackLink in the shell's before-main bar slot, label **"Flagged"**, chevron `aria-hidden`. The shell's
  in-memory opened-from record says the note was opened from a Flagged list (no router state). If the previous history
  entry is that list, it calls `navigate(-1)` (so the view, scroll and focus are restored); otherwise it pushes
  `/flagged`. Opened from outside the app (reload, pasted URL), it reads "Today" and goes to Today.
- Edit replaces the read view with the form, and Save changes or Cancel replace it back, so the entry before is still
  the Flagged list and the label stays "Flagged" (the shell copies the record across each replace).

### 8. Read view and 9. Review panel

Owned by [`participant-notes.md`](participant-notes.md) (Components, read view items 1 to 10, including the review
panel in item 8) for every route. Nothing on this route differs except the back label above. In particular, as
settled there: the note query key is `['notes', participantId, noteDate]`; status lines follow the report's order
(11.3); the busy label is "Marking reviewed…" after 400 ms; the panel stays mounted for the visit once rendered; one
unsent-comment rule; no "Earlier reviews" block and no "Go to flagged notes" link unless the owner approves them.

### Cross-cutting

- [`../components/foundations.md`](../components/foundations.md): system font, 18 px body, 16 px small text for meta
  lines (`--colour-text-secondary`, 9.0:1), focus ring 3 px near-black with 2 px offset, 44/48/56 px targets, one
  breakpoint at 40rem, light only, no motion, borders that survive forced colours.
- [`../components/microcopy.md`](../components/microcopy.md): glossary (To review, Reviewed, Mark reviewed, Comment,
  flagged; never "resolved", "closed", "actioned", "approved"), date tokens (`dateLong`, `time`, `dateTime` with
  non-breaking spaces, "midday"/"midnight"), no negative contractions in new copy, all strings in `src/copy`.

---

## States

### List view states

| # | State | When | What shows (exact copy) |
|---|---|---|---|
| L1 | Waiting | Request in flight under 1 s | h1 and ViewLinks only. Label "To review" (no count yet). Status line empty. |
| L2 | Loading, To review | Still in flight after 1 s | "Loading flagged notes…" (P) in the status line. |
| L3 | Loading, Reviewed (first visit to the view) | After 1 s | "Loading reviewed notes…" (P). ViewLinks fully usable. |
| L4 | To review, with rows | Loaded, 1 or more | Rows as in item 4, oldest first. Label "To review (3)". |
| L5 | To review row, flag removed | `flagRemovedLater` | Extra line "Flag removed in a later edit" (V). |
| L6 | To review, empty | Loaded, 0 rows | "No flagged notes to review." (V). Label "To review (0)". Badge hidden. No celebration. |
| L7 | Reviewed, with rows, more exist | `hasMore` true | Rows; **Show older** (V). |
| L8 | Show older, slow | Loading more than 1 s | Button `aria-disabled`, label unchanged; status line under it: "Loading older notes…" (P). |
| L9 | Show older failed | Next page failed | Rows kept; focus stays on the button; status line: "Older notes did not load: no connection. Try again." or "Older notes did not load: something went wrong. Try again." (P, `microcopy.md` load-failed pattern). Cleared when the next attempt starts. |
| L10 | Show older failed again | Second failure in a row | "Older notes still did not load: no connection. Try again." (P; changed text so it is announced again). |
| L11 | Reviewed, end of list | `hasMore` false | Button removed. No "end of list" message. |
| L12 | Reviewed, empty | Loaded, 0 rows | "No reviewed notes yet." (P) |
| L13 | First load failed, To review | After one silent retry | "Flagged notes did not load: no connection. Try again." or "Flagged notes did not load: something went wrong. Try again." (P) + **Try again** button. Label stays "To review" (no count). |
| L14 | First load failed, Reviewed | Same | "Reviewed notes did not load: no connection. Try again." / "…: something went wrong. Try again." (P) + **Try again**. |
| L15 | Try again running | Pressed | Old message cleared at once; button `aria-disabled`, label "Loading…" after 400 ms. |
| L16 | Try again failed | Retry failed | "Flagged notes still did not load: no connection. Try again." (or the Reviewed / server variant) (P). Focus stays on Try again. |
| L17 | Background refresh (focus, mount) | Data already shown | Rows and count stay; nothing blanks, moves or is announced. A failed refresh is silent. |
| L18 | Count refreshing | Refetch in flight | Last number stays until the new one arrives. |
| L19 | Unknown view keyword | `?view=anything-else` | To review view; URL corrected with `replace`. |
| L20 | Worker or stale role | Worker opens `/flagged`, or `403`/`404` from the reviews API after a role change | Whole page "Page not found", body "If you typed or pasted the web address, check it is correct.", link **Go to Today** (`empty-loading-error.md`). |
| L21 | Signed out | Any `401` | `session-timeout.md` sign-in in place; afterwards the same URL, including the view. |
| L22 | Forced colours, 200% text, 320 px | User settings | Current view keeps its bar (border) and bold; tags keep their border (`status-tags.md`); rows and labels wrap; no sideways scroll. |

### Review view states

Owned by [`participant-notes.md`](participant-notes.md) (States, read view and review panel). The only state specific
to this route is the back link's label, "Flagged" (or "Today" when the note was opened from outside the app).

---

## Interactions and focus

### Focus order (DOM order equals visual order)

**List view:** skip link → wordmark → Account → Today → Flagged → Report → Manage → (main) h1 is not a tab stop →
"To review (n)" → "Reviewed" → row link 1 … row link n → Show older (Reviewed only) → Try again (only on a failed
load).

**Review view:** as participant-notes.md, with the back link reading "Flagged": skip link → header and nav → back
link "Flagged" → Edit → Version history → Past notes → Comment textarea → Mark reviewed.

### What happens on each action

| Action | Result | Focus goes to | Announced |
|---|---|---|---|
| Open Flagged from the nav (push) | Scroll to top; `me` and the To review list refetch; Reviewed loads only if that view is in the URL | `<h1>` "Flagged notes" (app shell rule) | The h1, by focus |
| Activate "Reviewed" or "To review" | URL pushed (`/flagged?view=reviewed` or `/flagged`), `preventScrollReset`; list area swaps; first visit to Reviewed may show the loading line after 1 s | **Stays on the activated link** | Nothing (new content the user asked for; SC 4.1.3 not triggered). Loading or error text if slow or failed |
| Browser Back or Forward between the two views | View follows the URL (search-only change) | Left where it is; the shell does not move focus or scroll for a search-only change | Nothing |
| Open a row | Push the note URL with no router state; the shell remembers the opener, the scroll position and the row key in memory (app-shell.md) | Note `<h1>` once loaded (pending-focus rule) | The h1, by focus |
| Back to the list (back link or browser Back: a POP) | Same view from the memory cache; list refetches silently; scroll restored | **The row link the manager opened**, with `preventScroll`, through the shell's `handle.returnFocus`. If that row has gone (it was just reviewed), this list's declared fallback `'sameIndex'`: the row now in its place; if none, the last row; if the list is empty, the h1 | The row, by focus |
| Show older | Next 30 rows appended below | First new row link (or last row if nothing new arrived and the button is gone); on failure, stays on Show older | New row by focus; "Loading older notes…" after 1 s and failures through the status line (polite) |
| Try again | Refetch of the failed list only | On success the h1; on failure stays on Try again | "Loading…" label after 400 ms; result text (polite) |
| Anything on the note (Edit, Mark reviewed and its outcomes) | As participant-notes.md | As participant-notes.md | As participant-notes.md |
| Window focus or tab becomes visible | `me` and the lists refetch silently | Unchanged. If a refresh removes the row that has focus (another manager reviewed it), focus moves to the row now in its place, so it never falls to `<body>` | Nothing |

### Live regions (all present in the DOM from first render, all empty until used)

| Region | Where | Type | Carries |
|---|---|---|---|
| List status line, one per view | In the list area, before the rows | `<p role="status">` (polite) | Loading line, first-load errors |
| Show older status line | After the Show older button | `<p role="status">` (polite) | "Loading older notes…", Show older errors |
| Page status region | Each list view and the note (`PageStatus`, visually hidden, app-shell.md component 3) | `<p role="status">` (polite) | Try again's "Loading…" busy label; on the note, "Marking reviewed…" |
| The note's own regions | Read view and panel | – | Owned by participant-notes.md (count announcer, panel alert slot) |

Never live: the badge, the "(n)" in the tab label, tags, rows.

---

## Accessibility checklist

**Headings**
- [ ] List view: exactly one `<h1>` "Flagged notes"; no other headings are needed above the rows.
- [ ] Review view: as participant-notes.md (one `<h1>` with name and date; `<h2>` "1. Goals", "2. Common items",
      "3. Guided notes", "Review"). [Research] WebAIM Screen Reader Survey #10 (n = 1,539): 71.6% navigate long pages
      by headings, https://webaim.org/projects/screenreadersurvey10/

**Landmarks**
- [ ] `header` (banner), `nav aria-label="Main"`, `nav aria-labelledby="page-heading"` (ViewLinks, read as "Flagged
      notes, navigation"), `main`. No `section` or `form` with an accessible name, so no extra region or form
      landmarks.

**Names and labels**
- [ ] Badge link name "Flagged, 3 to review" (visually hidden text, no `aria-label`); "Flagged" at 0.
- [ ] ViewLinks names are exactly the visible text ("To review (3)", "Reviewed"); `aria-current="page"` on one only.
- [ ] Row link name is only the participant name and note date; the meta line is its `aria-describedby`.
- [ ] Every accessible name starts with its visible words (SC 2.5.3), so "click Mark reviewed", "click Reviewed",
      "click Show older" work with voice control.

**Keyboard**
- [ ] Every control is a native `<a href>` or `<button>`; no `role="tab"`, no arrow-key model, no roving tabindex,
      no positive tabindex.
- [ ] Enter in the comment makes a new line; no key handlers on the textarea (IME and dictation safe).
- [ ] Focus never falls to `<body>`: after Show older, Try again, Back to the list, and when a refresh removes the
      focused row (the note's own cases are in participant-notes.md).
- [ ] No button is ever `disabled`; busy uses `aria-disabled`.

**Screen reader**
- [ ] Arrival: "Flagged notes, heading level 1"; then "Flagged notes, navigation, list, 2 items, To review (3),
      current page, link".
- [ ] A To review row reads as "Sam Taylor, Tuesday 29 September 2026, link, Priya Nair, flagged 4:42 pm", then the
      Reason line when reading on (description timing varies by screen reader; unverified per AT).
- [ ] Middle dots are `aria-hidden` with a visually hidden ", " in their place.
- [ ] Every date and time is in `<time dateTime>` (`YYYY-MM-DD` for note dates, ISO UTC for stamps).
- [ ] `<html lang="en-AU">`.

**Visual**
- [ ] Every state is in words; colour is never the only cue (tags carry words; current view has bold and a bar).
- [ ] Text at 7:1 or better; tag text 8.1:1 (amber) and 13.1:1 (grey); badge digits 19.6:1.
- [ ] Focus ring 3 px near-black, 2 px offset, on every focusable element; `Highlight` in forced colours; row focus
      drawn on the row link's `::after` inset 3 px.
- [ ] No sticky or fixed elements on this screen.

**WCAG 2.2 AA criteria this screen must meet:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence ·
1.3.3 Sensory Characteristics · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow
(320 px, no sideways scroll) · 1.4.11 Non-text Contrast (current-view bar, focus ring, textarea border) ·
1.4.12 Text Spacing · 1.4.13 Content on Hover or Focus (no tooltips) · 2.1.1 Keyboard · 2.2.1 Timing Adjustable (no
auto-dismissing messages) · 2.2.2 Pause, Stop, Hide (no motion) · 2.4.2 Page Titled · 2.4.3 Focus Order ·
2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured
(Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (44 px floor, A32) · 3.1.1 Language of Page ·
3.2.1 On Focus · 3.2.2 On Input · 3.2.3 Consistent Navigation · 3.2.4 Consistent Identification · 3.3.1 Error
Identification · 3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 3.3.4 Error Prevention (the comment length
is checked before sending) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

---

## Acceptance criteria

**Access and navigation**
- [ ] A worker has no Flagged nav item; a worker who opens `/flagged` or `/flagged?view=reviewed` sees "Page not
      found" with the title "Grow2Notes – Page not found".
- [ ] On both list views the Flagged nav item has `aria-current="page"`; on the note screen no nav item does.
- [ ] Page title is "Grow2Notes – Flagged notes" on both views and "Grow2Notes – Note" on the note. No title, URL,
      router state, `localStorage`, `sessionStorage` or IndexedDB entry contains a name, reason or comment.

**View switch and count**
- [ ] `/flagged` shows To review; `/flagged?view=reviewed` shows Reviewed; `/flagged?view=xyz` shows To review and
      the address bar becomes `/flagged` without a new history entry.
- [ ] Switching view pushes one history entry; Back returns to the previous view; reload keeps the view.
- [ ] After switching, focus is still on the link that was activated and the page has not scrolled.
- [ ] Exactly one `aria-current="page"` inside the ViewLinks nav.
- [ ] Before the To review list first loads, the label is "To review" with no number; after it loads with 3 rows it
      is "To review (3)"; with 0 rows "To review (0)"; if the first load failed it stays "To review".
- [ ] "Reviewed" never shows a count.
- [ ] The "(n)" equals the number of To review rows, and after Mark reviewed the badge and the "(n)" both drop by 1
      when the list next shows.

**To review list**
- [ ] Rows are in the API's order (oldest first); the client does not re-sort.
- [ ] Each row shows the full participant name, the note date as "Tuesday 29 September 2026", "{author} · flagged
      {time}" (date and time when the flag was on a later day than the note date, for example "flagged Thu 1 Oct
      2026, 9:01 am"), and "Reason: " with the reason up to its first line break, not truncated.
- [ ] A row with `flagRemovedLater` shows "Flag removed in a later edit".
- [ ] Tapping or clicking anywhere on a row opens that note; each row is at least 56 px tall.
- [ ] Rows show no tags and contain no buttons.
- [ ] With no flags: "No flagged notes to review." and no list element; the badge is not shown.

**Reviewed list**
- [ ] Shows at most 30 rows, newest review first; each shows author, "reviewed by {name} on {Ddd D Mmm YYYY,
      h:mm am}", the reason's first line and, when present, "Comment: " with the full comment and its line breaks.
- [ ] A note reviewed twice appears as two rows without React key warnings.
- [ ] Show older appears only when `hasMore` is true; pressing it appends the next rows and moves focus to the first
      new row's link; pressing it again while loading does nothing; when no more remain the button is gone.
- [ ] With Show older offline: the rows already shown stay, focus stays on the button, and "Older notes did not
      load: no connection. Try again." appears; a second failure says "Older notes still did not load: no
      connection. Try again.".
- [ ] No content ever loads on scroll; no count, "Showing…" or page number appears anywhere.
- [ ] With no reviews: "No reviewed notes yet." (once approved).

**Loading and errors**
- [ ] With a response under 1 s, no loading text ever appears; at 1 s "Loading flagged notes…" (or "Loading reviewed
      notes…") appears in the status line, which existed empty before.
- [ ] Offline on first load: within about 2 s the failure sentence and a Try again button show; a failed Try again
      changes the sentence to the "still" form and keeps focus on Try again; a successful Try again moves focus to
      the h1.
- [ ] A failed background refresh (window focus while offline) leaves the rows and count on screen and announces
      nothing.
- [ ] No spinner, skeleton, toast, shimmer or animation appears in any state.

**Back and place**
- [ ] Open the 10th Reviewed row after Show older, then press Back (browser or the "Flagged" back link): the
      Reviewed view shows the same 60 rows at the same scroll position, and focus is on the 10th row's link.
- [ ] Review the first To review row, press Back: the reviewed row is gone and focus is on the row now first.
- [ ] Reload the list: it starts again at the top with the newest 30 Reviewed rows (nothing was stored on the device).

**The note**
- [ ] Opened from either Flagged list, the note's back link reads "Flagged" and pops back to the list; everything else
      on the note meets participant-notes.md's acceptance criteria, unchanged by this route.
- [ ] `history.state` holds no back object; reloading the note shows "‹ Today".

**Visual and device**
- [ ] At 320 px wide with 200% text: no horizontal scroll; ViewLinks wrap to two rows; names, reasons and comments
      wrap; nothing is truncated.
- [ ] In Windows forced colours: the current view's bar, the badge outline, the focus ring and the tags' borders are
      all visible.
- [ ] axe (Playwright) reports no violations on: To review with rows, To review empty, Reviewed with Show older,
      Show older error, first-load error (the note's states are tested in participant-notes.md).
- [ ] Manual pass with NVDA + Chrome, VoiceOver + iOS Safari and TalkBack + Android Chrome confirms the
      announcements in "Interactions and focus" and that nothing is read twice.
- [ ] A copy test finds no parent company's name (CI secret, microcopy.md §8), no negative contractions in new strings, and no "resolved", "closed",
      "actioned", "please", "sorry" or "click".

---

## Conflicts resolved

Where component specs disagreed, the choice made for this screen and why. Items marked "app-wide" need the same
choice on other screens (also listed in the structured summary).

1. **URL for the Reviewed view: `/flagged?view=reviewed` (tabs-segmented), not `/flagged/reviewed`
   (app-shell-nav, review-panel).** One `ViewLinks` behaviour for Flagged and Participants (SC 3.2.4), and a simple
   shell rule: route-change focus and scroll react to `pathname` only, so switching views keeps focus on the link.
   With a path segment the shell would move focus to the h1 and scroll on every switch. The keyword holds no personal
   data. (App-wide: app-shell-nav's current-item table and focus rule need updating.)
2. **The note's URL is the shared `/participants/{id}/notes/{date}` (app-shell-nav, version-history,
   chronological-list), not a copy mounted under the Flagged route (note-read-view).** The back link label "Flagged"
   comes from the shell's in-memory opened-from record (app-shell.md), and no nav item is current on a note, as
   app-shell-nav says. The read view and the review panel are participant-notes.md's (editorial pass): this route
   changes only the back label.
3. **Back to the list focuses the row that was opened (chronological-list), not the h1 (app-shell-nav).** Keeps the
   place for keyboard and screen-reader users while working through the queue [Research: Baymard, over 90% of
   load-more sites lost the place, 2016]. Added rule [Opinion]: if that row has gone because it was just reviewed,
   focus the row now in its place. This gives "the next flag" without any auto-advance feature.
4. **Scroll and place kept in an in-memory map (chronological-list), not React Router `<ScrollRestoration>`
   (app-shell-nav), which writes to `sessionStorage`.** Keeps D22's "nothing on the device" without argument.
   (App-wide.)
5.–12. **Read view and review panel choices** (after Mark reviewed, busy label, error wording, 409 handling, where
   the reason sits, which reviews show where, the record wording, "Comment:" on one line). **Moved to
   participant-notes.md** in the editorial pass, which now owns the read view for every route. Two choices made here
   were not kept there: the "Earlier reviews" block and the "Go to flagged notes" link are owner questions, not built
   by default, so the same URL behaves the same whichever list opened it. The rest (record in place with focus,
   "Marking reviewed…" at 400 ms, "Not marked reviewed: …", review-panel's three 409 cases, the reason at the top,
   "Reviewed by … on …" with the date, the panel staying mounted, the restored unsent comment) carried over.
13. **Load-error wording follows microcopy.md ("Flagged notes did not load: no connection. Try again."), not
    empty-loading-error.md ("Could not load flagged notes. Check your internet connection, then try again.").**
    The brief says wording follows the microcopy research, and microcopy.md section 9 names this as the canonical
    form. empty-loading-error.md's behaviour is kept: one silent retry, the "still" wording on a repeat failure so it
    is announced again, Try again outside the status element, focus to the h1 on success. (App-wide.)
14. **Loading lines "Loading flagged notes…" and "Loading reviewed notes…" (empty-loading-error), not "Loading
    notes…" (chronological-list).** More specific, and both fit microcopy.md's loading pattern.
15. **Show older while loading keeps its label and uses the status line after 1 s (chronological-list), not a
    "Loading…" label after 400 ms (primary-actions, empty-loading-error).** It is a read, not a write; changing the
    focused control's name mid-press is announced unevenly; the status line gives a specific message ("Loading older
    notes…"). Try again keeps the shared "Loading…" label (empty-loading-error), since it replaces an error rather
    than adding to a list. (App-wide: Past notes 4.4 must match.)
16. **Reviewed empty sentence "No reviewed notes yet." (chronological-list, empty-loading-error), not "No flagged
    notes have been reviewed yet." (tabs-segmented).** Two specs against one; shorter.
17. **Window-focus refetch stays on for the To review list (design 7.5, chronological-list, tabs-segmented), against
    empty-loading-error's app-wide default of `refetchOnWindowFocus: false`.** The badge refreshes on focus (4.6), so
    the "(n)" must too, or the two numbers disagree. The note query keeps it off (note-read-view, review-panel).
18. **Current-view bar colour: `--colour-action` (foundations, "Current (nav item, tab)"), not the text colour
    (tabs-segmented).** Both pass 3:1; foundations owns tokens and the nav uses the same bar.
19.–20. **Body size and the comment hint.** Moved to participant-notes.md with the read view (18 px body;
    "cannot", not "can't").
21. **Reviewed row keys include `reviewedAtUtc` (fix to chronological-list's `participantId + noteDate`).** A15 means
    a note can have two reviews, so two rows.
22. **Date and time tokens from microcopy.md** (`dateLong` for note dates, `time` or `dateTime` "Thu 1 Oct 2026, 9:01
    am" for stamps, "midday"/"midnight"), not foundations.md's "1 Oct 2026, 5:03 pm" or chronological-list's
    "1 Oct 2026, 4:12 pm". (App-wide: one formatter owner is needed.)

---

## Tensions with decisions

Noted once, with no change recommended.

- **D22 (nothing stored on the device).** An unsent review comment survives moving around the app and signing in
  again (in-memory map), but not a reload or a closed tab. General guidance is to keep partial work across
  interruptions (ui-ux-design invariant I9; in spirit WCAG 2.2.5 Re-authenticating, AAA,
  https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html). The comment is optional and at most 500
  characters, so the cost is small.
- **D18, the word "flag".** COGA 4.4.4 (Use Literal Language, https://www.w3.org/TR/coga-usable/) points towards
  literal words; "flag" is figurative. The evidence is convention, not testing, and "Reason" and "Flagged for
  manager" make it concrete (`microcopy.md`).
