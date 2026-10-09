# Today

Screen spec for design.md §4.2 ("Today (home for everyone)"). It composes the researched component specs in
`../components/` into one screen and settles where they disagree. It adds no feature, field, setting, notification,
count or data item: everything on this screen is in design.md §4.2, §4.0, §4.3 (the post-submit message) and §6.3.

**Copy marks used in this file.** **(V)** design.md's words, verbatim. **(N)** design.md's words with the punctuation
or name rule from `microcopy.md` applied. **(P)** proposed copy for a state design.md implies but does not word; the
owner should approve it (see Open questions).

**Evidence grades.** [Research] studies and usability-research write-ups · [Standard] WCAG 2.2, WAI-ARIA, HTML ·
[Convention] established design systems · [Opinion] reasoned judgement. The full evidence sits in each component file;
only the load-bearing citations are repeated here.

---

## Purpose and who uses it

**Purpose (design.md §4.2):** start or continue today's note for any participant, and see what has been done today.
It is the home screen for everyone and where every user lands after signing in (§4.1) and after submitting a note
(§4.3). **Primary action:** tap a participant.

| Who | Device and context | What they need from this screen |
|---|---|---|
| **Support workers** (fewer than 20, D2) | Their own phones (D21), often at the end of a long shift, tired; many write English as a second language; mixed digital confidence; some dictate. | Find one participant fast, see at a glance whether today's note exists and whether it is a draft or submitted (a row with no note simply has no status), and tap into it. Continue an unfinished earlier-day draft. |
| **Managers** (a few, equal, D24) | Laptops and phones. | The same list as workers (A7, they also write notes), plus the Flagged badge in the navigation, and "Add participant" when the organisation has no participants yet. |

What the screen deliberately does **not** do (D25, A17, D26): no counts, no "not started" or "missing" labels, no
progress, no sorting or grouping by status, no per-worker list (D20: every worker sees every participant).

Data (§6.3): `GET /api/today` (active participants in family-name order with today's live note or `null`) and
`GET /api/me/drafts` (the caller's drafts and pending edits). The header date comes from `GET /api/auth/me` →
`today` (server Melbourne date, never the device clock; §3.3, A33).

---

## Layout - phone

At about 375 px, default text size. Manager shown (two header rows); a worker's header is one row:
`Grow2Notes   Today   Account v`. This sketch shows the state just after a submit, with one unfinished draft, so
every part appears at once. Normally the banner and the drafts section are absent.

```
+---------------------------------------+
| Grow2Notes                  Account v |  <header>: static, never sticky; scrolls away
| Today  Flagged [3]  Report  Manage    |  managers only: 2nd row; [3] = count badge
| =====                                 |  current item: bold + 4 px bar + aria-current
+---------------------------------------+
|                                       |  <main>, 16 px side gutters
| +-----------------------------------+ |
| |# (tick) Note for Jane Citizen     | |  success banner: ONLY after Submit;
| |#        submitted                 | |  green edge, bold text, takes focus
| +-----------------------------------+ |
|                                       |
| Today · Thursday                      |  <h1>, 28 px bold; wraps at this width
| 1 October                             |  (non-breaking space keeps "1 October")
|                                       |
| Your unfinished drafts                |  <h2>, only when the user has one
|---------------------------------------|  rows run edge to edge (full-bleed)
| Sam Nguyen                          > |  whole row = one link, min 56 px
| Wednesday 30 September 2026           |  note date (16 px, dark grey)
|---------------------------------------|
|                                       |  32 px section gap
|  (visually hidden <h2> Participants)  |
| Find a participant                    |  <label>, bold, above the field
| +-----------------------------------+ |
| | (magnifier)                       | |  48 px text field, 18 px text, no placeholder
| +-----------------------------------+ |  [ Clear ] appears beside it only while it has text
|                                       |
|---------------------------------------|
| Jane Citizen                        > |  name: 18 px bold (the link text)
| Submitted · You · 4:12 pm             |  status line: 16 px; status word bold
|---------------------------------------|
| Priya Kumar                         > |  no note today: name only, no status line
|---------------------------------------|
| Lee Oakes                           > |
| Draft · Alex P. · started 9:14 am     |
|---------------------------------------|
| Tom Ward                            > |
| Submitted · Alex P. · 4:12 pm         |
| [Flagged]                             |  pale-amber tag; drops to its own line
|---------------------------------------|  when it does not fit
| Mei Zhang                           > |
| Draft · You · saved 9:42 am           |
|---------------------------------------|
  ... every active participant, family-name order (A5); no paging, no "show more"
```

The other region states replace everything below the `<h1>` (the banner and `<h1>` always stay):

```
Waiting (< 1 s)           Loading (>= 1 s)          Load failed                 No participants (manager)
| Today · Thursday     |  | Today · Thursday     |  | Today · Thursday       |  | Today · Thursday     |
| 1 October            |  | 1 October            |  | 1 October              |  | 1 October            |
|                      |  |                      |  |                        |  |                      |
| (nothing)            |  | Loading participants…|  | The participant list   |  | No participants yet. |
|                      |  |                      |  | did not load: no       |  |                      |
|                      |  |                      |  | connection. Try again. |  | Add participant      |
|                      |  |                      |  |                        |  |  (link; managers     |
|                      |  |                      |  |                        |  |   only)              |
|                      |  |                      |  | [      Try again     ] |  |                      |

No search match (search box stays, with the typed text and Clear)
| Find a participant                    |
| +-------------------------+ +-------+ |
| | (magnifier)  xyz        | | Clear | |
| +-------------------------+ +-------+ |
|                                       |
| No participant matches ‘xyz’.         |  plain body text where the list was; not red
```

Spacing (foundations tokens): banner → `<h1>` `--space-4`; `<h1>` → first section `--space-3`; drafts section →
search `--section-gap` (32 px); search label → field `--space-2`; field → list `--space-4`; bottom of page
`--space-8`. Rows have a 1 px `--colour-divider` rule above the first and below every row.

---

## Layout - laptop

One breakpoint, `@media (min-width: 40rem)` (foundations.md). From there:

| Part | Phone (< 40rem) | Laptop (>= 40rem) |
|---|---|---|
| Header | Same markup; nav row wraps if needed | Inner container `--page-max` (60rem), centred; same two rows for managers; hover underline on links |
| Content column | Full width minus 16 px gutters | `--measure` (40rem), **left-aligned** inside the 60rem container so it shares the nav's left edge; 32 px gutters |
| `<h1>` | 28 px, may wrap to two lines | 32 px, one line |
| Section gap | 32 px | 48 px |
| Rows | Full-bleed (list pulls into the gutter, rows pad back), 56 px min | Not full-bleed; list capped at 40rem; same stacked two-line row; hover tint `--colour-hover` and underlined name under `@media (hover: hover)` |
| Search | Field full width; Clear wraps below at 320 px / 200 % text | Field and Clear in one row, max 32rem |
| Banner | Full content width | Content-column width (not window width) |

Nothing else changes. No columns (Name | Status | Author | Time), no table, no side panel, nothing sticky, no
density mode: the same stacked rows keep each status next to its name (participant-list-rows.md, status-tags.md
[Convention] NSW "place status labels as close to the element as possible").

---

## Components, in order

DOM order equals visual order equals Tab order. Each item links to its component spec; only Today's settings are
listed here.

### 1. Skip link, header, navigation and badge

[app-shell-nav](../components/app-shell-nav.md) · [notification-badge](../components/notification-badge.md)

- Skip link "Skip to main content" first in `<body>`; click handler `preventDefault()` and focuses `<main>`.
- Page title **Grow2Notes – Today** (V pattern, §4.0). Never a name, a count or the search text.
- Nav item **Today** has `aria-current="page"`; the wordmark "Grow2Notes" also links to `/`.
- Managers: **Flagged** link with the count pill when `toReviewCount > 0`; accessible name "Flagged, 3 to review"
  (digits `aria-hidden`, visually hidden ", 3 to review"; notification-badge.md). Pill is neutral near-black
  (`--colour-badge`), never red, never a live region, hidden at 0. This is the only number on Today (D25).
- Header is static, never sticky or fixed ([Standard] Understanding 1.4.10 "strongly suggested … static
  positioning", https://www.w3.org/WAI/WCAG22/Understanding/reflow.html; SC 2.4.11).

### 2. Success banner (only after Submit)

[status-messages](../components/status-messages.md)

- Text: **Note for Jane Citizen submitted** (V, §4.3). Full participant name (3.9). No full stop (one-phrase success,
  microcopy.md). No title row, no close button, no timer.
- Placed **immediately before the `<h1>`** inside `<main>` ([Convention] GOV.UK and NHS notification banner,
  https://design-system.service.gov.uk/components/notification-banner/).
- Markup: `<div tabindex="-1" data-route-focus>` + `aria-hidden` tick icon + `<p>`; **no role**, no `aria-live`.
  It takes focus on arrival, which puts it outside SC 4.1.3 ([Standard]
  https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html).
- Tone: success (`--colour-success` 6 px inline-start border + 1 px border, `--colour-success-tint` fill, text
  `--colour-text` bold). The edge is a real border so it survives forced colours.
- The message is passed in an in-memory store (`messageStore`), never router state, URL or browser storage (D22,
  §4.0; [Standard] MDN: some browsers save history state to disk,
  https://developer.mozilla.org/en-US/docs/Web/API/History/pushState). Read once on mount, cleared at once, so a
  reload never replays it. It stays while the user is on Today (across refetches and window focus) and is gone when
  they leave.
- Shown only after the server returned `201` (or `200` on an idempotent retry): never optimistic (invariant I5).

### 3. Page heading

[app-shell-nav](../components/app-shell-nav.md) (`PageHeading`) · [microcopy](../components/microcopy.md) (`dateToday`) · [foundations](../components/foundations.md)

- `<h1 tabindex="-1">` **Today · Thursday 1 October** (V). Date from `me.today` through `dateToday()` (no year, as
  designed); non-breaking space between "1" and "October".
- The " · " is `<span aria-hidden="true"> · </span>` + visually hidden ", " (microcopy.md separator rule), so it is
  read "Today, Thursday 1 October, heading level 1".
- `--font-size-h1` (28/32 px), weight 700, `width: fit-content` so a focus box hugs the text. Renders at once (the
  shell already has `me`), before the list loads.

### 4. Load region (loading and load-failed)

[empty-loading-error](../components/empty-loading-error.md)

- One region for **both** requests (`/api/today` and `/api/me/drafts`): the drafts section and the list appear in
  one render pass or not at all, so the drafts section never pushes rows down under a finger and unfinished work is
  never hidden without a word ([Convention] CLS, https://web.dev/articles/cls; participant-list-rows.md).
- One `<p role="status">` rendered from the first render and never conditionally mounted; empty and `margin: 0`
  when idle (never `display: none`). **Try again** sits outside it.
- Timing: nothing under 1 s; then **Loading participants…** (P). No spinner, no skeleton ([Research] NN/g progress
  indicators, https://www.nngroup.com/articles/progress-indicators/; Viget n=136 found no advantage for skeletons,
  https://www.viget.com/articles/a-bone-to-pick-with-skeleton-screens/).
- Load failed (first load only, `isLoadingError`), microcopy.md §9 (P): no connection or timeout → **The participant
  list did not load: no connection. Try again.**; server error → **The participant list did not load: something went
  wrong. Try again.**
- Try again: native secondary `<button type="button">`, 48 px, full width on a phone, `aria-disabled` (never
  `disabled`) while running, label **Loading…** (P) after 400 ms; a repeat failure reads **The participant list still
  did not load: …** (P) so screen readers announce it again ([Convention] NHS App "There is still a problem",
  https://design-system.nhsapp.service.nhs.uk/patterns/error-page/).
- Query settings: `networkMode: 'always'`, one silent retry for network/timeout/5xx only, no retry of any 4xx, and
  the `api()` wrapper's own 10 s timeout: each query passes TanStack's `signal`, never `AbortSignal.timeout`, whose
  abort the wrapper would rethrow as it came, not as a timeout (empty-loading-error.md *Timing*). A `401` shows
  nothing here: it hands over to session-timeout.md, and the region's queries are invalidated after signing in again.

### 5. Your unfinished drafts (only when needed)

[participant-list-rows](../components/participant-list-rows.md) (drafts variant) · [microcopy](../components/microcopy.md)

- Rendered **only** when the caller has a draft or pending edit that is not on today's list (V, §4.2, A17): build it
  from `/api/me/drafts`, dropping entries whose `noteDate` equals the list's `date` **and** whose participant is on
  today's list. When nothing remains, the heading and list are not rendered at all (it is not an empty state).
- Heading `<h2>` **Your unfinished drafts** (V), `--font-size-h2`.
- `<ul role="list">` of the same row component as the main list. Link text = participant name ("Sam Nguyen", given
  then family, never truncated). Detail line = the note date in `dateLong`, **Wednesday 30 September 2026**, in
  `<time dateTime="2026-09-30">`, tied to the link with `aria-describedby`. No status word, no time, no tag.
- Order: family name, then given name (A5 "everywhere"), then note date oldest first (`Intl.Collator('en-AU')`).
- Destination: straight to the caller's working copy for `(participantId, noteDate)`: the draft form for
  `kind: "draft"` at `/participants/{id}/notes/{noteDate}`, the edit form for `kind: "pendingEdit"` at
  `/participants/{id}/notes/{noteDate}/edit` (note-form.md, Routes). URLs hold only IDs and dates.
- Rows for a participant archived since the draft started stay (3.7: the draft can still be submitted). Nothing marks
  the archive.
- Search never filters this section.

### 6. Hidden "Participants" heading

[participant-list-rows](../components/participant-list-rows.md)

- `<h2 class="visually-hidden">Participants</h2>` (P), before the search box, always present once the region is
  ready. Without it, a screen-reader user moving by headings hears the main list as part of "Your unfinished drafts"
  ([Research] WebAIM Screen Reader Survey #10: 71.6 % navigate by headings, https://webaim.org/projects/screenreadersurvey10/).
  It changes nothing on screen.

### 7. Find a participant

[search-filter](../components/search-filter.md)

- `<search>` landmark, **no `<form>`**, no submit button. Visible bold `<label>` **Find a participant** (V) above an
  `<input type="text">` with **no placeholder**, a decorative magnifier (`aria-hidden`), `autocomplete="off"`,
  `autocorrect="off"`, `spellcheck={false}`, default `autocapitalize`, no `enterkeyhint`, **no `autofocus`**.
  Font `max(1em, 16px)` (18 px here) so iOS does not zoom; min height 48 px.
- Visually hidden description, tied by `aria-describedby`: **The list of participants below changes as you type.** (P)
- **Clear** button (P): `<button type="button">`, visible word "Clear" + hidden " search", min 44 × 48 px, shown only
  while the field has text. `onMouseDown` `preventDefault()` keeps the phone keyboard open; click empties the field
  and returns focus to it.
- Filtering: synchronous on every keystroke, no debounce, no network request ([Standard] React docs:
  debounce is for network work, https://react.dev/reference/react/useDeferredValue). Matching is the shared
  `matchesName()` (accents, case and punctuation ignored, apostrophes dropped; every typed word must start a word of
  "given family"). Names only: never the status line or an author's name. Filtered rows keep A5 order.
- Enter does nothing. Escape does nothing special (no Escape-to-clear: NVDA/JAWS users press it to leave forms mode;
  [Research] Roselli, https://adrianroselli.com/2019/07/ignore-typesearch.html).
- Rendered only when the list has loaded and has at least one participant. Never during the first load, after a
  failed load, or in the "No participants yet." state. Never unmounted or disabled during a background refetch.
- The typed text lives in Today's component state only: never the URL, title, `history.state`, query keys, storage,
  logs or telemetry (D22, §4.0, §9.5). It resets when the user leaves Today.
- Not sticky (SC 2.4.11, and the keyboard would cover the list).

### 8. Participant rows (main list)

[participant-list-rows](../components/participant-list-rows.md) · [status-tags](../components/status-tags.md) · [foundations](../components/foundations.md)

- `<ul role="list">` (Safari drops list semantics under `list-style: none`; [Convention]
  https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html). Every active participant in the server's order
  (family, then given; A5). Never re-sorted, grouped, paged or virtualised. Keyed by participant ID.
- **The whole row is one `<a href>`** (React Router `<Link>`): the link text is the name only; a `::after` overlay
  stretches the target over the `<li>` ([Research]+[Convention] GOV.UK task list: users kept clicking the status, so
  the whole row became the link,
  https://designnotes.blog.gov.uk/2023/12/15/working-as-a-community-to-iterate-the-task-list-pattern/). No button,
  tick box or second link inside a row.
- Status line: a `<p id>` referenced by the link's `aria-describedby`; shown **only when a note exists** (A17):

  | `note` | Status line | Source |
  |---|---|---|
  | `null` | *(no line at all)* | (V) A17, D25 |
  | Draft, `isMine` | **Draft** · You · saved 9:42 am (`savedAtUtc`) | (V) |
  | Draft, someone else | **Draft** · Alex P. · started 9:14 am (`startedAtUtc`) | (V) |
  | Submitted, someone else | **Submitted** · Alex P. · 4:12 pm (`firstSubmittedAtUtc`) | (V) |
  | Submitted, `isMine` | **Submitted** · You · 4:12 pm | (P) follows "Draft · You"; see Open questions |
  | Submitted and `isFlagged` | the line above, then the **Flagged** tag | (V) |

  `Flagged` appears **only** when `status` is Submitted (a draft flag is not seen, 3.6, A9). It follows the current
  version, so it disappears if a later edit removed the tick, even while the note stays To review for managers (A15).
- Times: the shared `time()` formatter (`src/copy/format.ts`): Melbourne time, "9:42 am", non-breaking space before
  am/pm, exactly 12:00 shown as **midday** (microcopy.md, Style Manual). Wrapped in `<time dateTime>` with the UTC
  instant. Author: `authorDisplayName` exactly as stored (never shortened or re-derived).
- Separators: `<span aria-hidden="true"> · </span>` + visually hidden ", "; each part `white-space: nowrap` so lines
  break only at separators; non-breaking space before each dot so no line starts with "·".
- Typography: name `--font-size-body` (18 px), weight 700, `--colour-text`, no underline at rest,
  `overflow-wrap: anywhere`, `hyphens: manual`, never truncated. Status line `--font-size-small` (16 px),
  `--colour-text-secondary` (9.0:1), with the status word ("Draft" or "Submitted") in `--colour-text`, weight 700.
- Flagged tag: `--font-size-small`, weight 400, `--colour-attention-text` (#5c3a00) on
  `--colour-attention-tint-strong` (#ffe2a8) = 8.1:1, 1 px `--colour-attention-edge` border, `--radius-s`,
  `padding: 0.125em 0.5em`. Sentence case, no icon, not focusable, no role, no tooltip. It must not look like the
  near-black count pill.
- Chevron: decorative inline SVG, `aria-hidden="true" focusable="false"`, `currentColor` = `--colour-text-secondary`
  (well above 3:1), trailing edge, vertically centred.
- Row: `min-block-size: 3.5rem` (56 px; A32 asks for 44), `padding-block: 0.75rem`, 1 px divider. Focus: the link's
  outline is moved, not removed: 3 px `--colour-focus` outline on `.link::after`, `outline-offset: -3px`, around the
  whole row. Pressed: `--colour-pressed`. Hover only under `@media (hover: hover)`. Never disabled, never faded.
- Each link carries `data-return-key={participantId}` and **no router state**. The shell records that Today opened
  the note (its opened-from memory, app-shell.md route-change rule), so the note's back link says "Today" and pops
  back here; the shell also uses the key to return focus to this row.
- Destination: the note route `/participants/{id}/notes/{list.date}` (note-form.md, Routes). That page fetches fresh
  data and decides what to show; the row never routes from its cached status (Today refreshes only on load and
  focus, §6.8).

### 9. Empty states

[empty-loading-error](../components/empty-loading-error.md) · [search-filter](../components/search-filter.md)

- **No participants:** `<p>` **No participants yet.** (V) in `--colour-text`, body size. Managers also get
  `<a href>` **Add participant** (V) to the same "Add participant" page that Manage > Participants links to (§4.8).
  Workers see only the sentence. Shown only after the data has arrived ([Research] NN/g: an empty message shown
  before the data arrives is a harmful misleading status, https://www.nngroup.com/articles/empty-state-interface-design/).
- **No search match:** **No participant matches ‘xyz’.** (N: design's words with typographic quotes and a full stop,
  microcopy.md) shown at once where the list was; `xyz` is the trimmed text as typed; `overflow-wrap: anywhere`;
  plain body text, not red, no icon, no "Add participant", no suggestions, no count.

### Cross-cutting

- [foundations](../components/foundations.md): system font stack; 18 px body, 16 px minimum; weights 400/700; the
  `--colour-*` tokens above; 3 px near-black focus ring with 2 px offset (`Highlight` in forced colours); no motion;
  light only.
- [microcopy](../components/microcopy.md): every visible string comes from `src/copy` (`react/jsx-no-literals`);
  `<html lang="en-AU">`; no negative contractions in new copy; glossary terms only (participant, draft, submitted,
  Flagged).

---

## States

Every state the screen can be in. The `<h1>` (and the success banner, when present) show in all of them.

### Region states

| State | When | What shows (exact copy) | Announced |
|---|---|---|---|
| **Waiting** | Either request in flight, under 1 s | Nothing below the `<h1>`. No search box. | No |
| **Loading** | Still in flight at 1 s | **Loading participants…** (P) | Polite, via the region's `role="status"` |
| **Ready** | Both requests succeeded | Drafts section (if any), hidden "Participants" heading, search, list | No |
| **Ready, no participants** | `participants` is empty | **No participants yet.** (V) + managers: **Add participant** (V). No search box. Drafts section still shows if the user has one. | No (read in order after the `<h1>`) |
| **Load failed, no connection** | First load failed after one silent retry; `fetch` rejected or 10 s timeout | **The participant list did not load: no connection. Try again.** (P) + **Try again** | Polite |
| **Load failed, server** | `5xx`, `429` or anything unexpected | **The participant list did not load: something went wrong. Try again.** (P) + **Try again** | Polite |
| **Retrying** | Try again pressed | Error text cleared at once; button `aria-disabled`, label **Loading…** (P) after 400 ms | No |
| **Still failing** | Try again failed | **The participant list still did not load: no connection. Try again.** (or the server cause) (P); focus stays on Try again | Polite (new text, so re-announced) |
| **Background refresh failed** | A focus or mount refetch fails while data is shown | No change. Rows stay. No message. | No |
| **Signed out** (`401`) | Any request | Nothing in this region; session-timeout.md's signed-out state takes over; after signing in, Today's queries are invalidated | Owned there |
| **Crashed** | A render error | The shell's route error boundary page: `<h1>` **There is a problem with Grow2Notes**, **Anything already saved is kept.**, link **Go to Today** (P, empty-loading-error.md) | Via focus on its `<h1>` |
| **Midnight rollover** | `me.today` changes (refetched on focus or screen change) | `<h1>` shows the new date; the list for the old date is dropped and the new one loads (Waiting/Loading) | Only "Loading participants…" if slow |

### Row and section states (design.md §4.2)

| State | What shows |
|---|---|
| No note today | Name and chevron only. Never "Not started", "Missing", "No note" (D25, A17). |
| Own draft | **Draft · You · saved 9:42 am** (V) |
| Someone else's draft | **Draft · Alex P. · started 9:14 am** (V). The same for workers and managers; the difference is on the next page. |
| Submitted | **Submitted · Alex P. · 4:12 pm** (V); own note **Submitted · You · 4:12 pm** (P) |
| Submitted and flagged | Same line + **Flagged** tag (V) |
| Event at exactly 12:00 | **midday** (for example "Submitted · Alex P. · midday"); 00:00 is **midnight** (microcopy.md) |
| Unfinished drafts present | **Your unfinished drafts** (V) heading and rows: name + **Wednesday 30 September 2026** |
| No unfinished drafts | Section not rendered at all |
| Search: empty field | Full list; no Clear button |
| Search: typing, matches | Filtered list in A5 order; Clear visible |
| Search: no match | Field keeps the text; Clear visible; **No participant matches ‘xyz’.** (N) in place of the list; drafts section unaffected |
| After Submit | Success banner **Note for Jane Citizen submitted** (V) before the `<h1>`; the row shows Submitted once the fresh list arrives |
| Hover (laptop) | Row `--colour-hover`, name underlined |
| Focus | 3 px ring around the whole row |
| Pressed | Row `--colour-pressed`, instant |
| Disabled / read-only / error per row | Never. Every row opens something; read-only happens on the destination. |

### What tapping a row leads to (destination screens own these; listed so testers know what to expect)

| Row's note | Destination (decided by the note page from a fresh fetch) |
|---|---|
| None | Empty note form (§4.3); no draft exists until the first change |
| Own draft | The form |
| Someone else's draft, worker | **Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it.** (N: microcopy.md uses the full `DisplayName` in both places; design.md says "Only Alex can finish it.") |
| Someone else's draft, manager | The draft read-only, with Discard |
| Submitted | The read view (§4.4); Edit offered to the author and managers |

---

## Interactions and focus

### Arrival focus (one owner, one move)

The shell's route-change focus rule (app-shell-nav.md) runs once per client-side navigation. On Today it resolves in
this order, first match wins:

| How the user arrived | Focus goes to | Scroll | Why |
|---|---|---|---|
| **After Submit** (banner present) | The success banner (`data-route-focus`), focused **without** `preventScroll` | Top of page (the banner is above the `<h1>`) | status-messages.md, app-shell-nav.md; [Convention] GOV.UK banner focus |
| **Back (a pop) from the note opened from a Today row** ("‹ Today", which pops, or browser Back) and that row is still listed | That row's link (`data-return-key`), with `preventScroll` | Restored from the shell's in-memory map | app-shell.md route-change rule (`handle.returnFocus`); participant-list-rows.md; keyboard and screen-reader users keep their place in a list of up to ~80 rows; [Standard] by analogy with the APG rule that focus "returns to the element that invoked the dialog unless … the invoking element no longer exists", https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/; [Opinion] for applying it here |
| **Anything else** (nav "Today", wordmark, after sign-in, after Discard, which is a replace, a returned-to row that is gone) | The `<h1>` (`tabindex="-1"`) | Top on a push or replace; kept on Back/Forward (`preventScroll`) | app-shell.md; [Research] Gatsby/Fable n=5 found heading focus best on route change, https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/ |
| **Very first page load** (reload, typed URL) | Left where the browser puts it | Browser default | app-shell-nav.md |

How the row case works: Today owns no return-focus code. Its route declares `handle: { returnFocus: {} }` (fallback
`'h1'`) and each row link carries `data-return-key={participantId}`. The shell records the activated row on Today's
history entry (memory only; never storage, D22, §9.6) and, **on a pop only**, focuses that row once Today calls
`useReturnFocusReady(regionReady)` (app-shell.md route-change rule). If the list has to be fetched again (TanStack
Query drops unused cache after 5 minutes by default, so this is normal after writing a note; [Convention]
https://tanstack.com/query/v5/docs/framework/react/guides/caching), focus waits for the data; if the row is not in the
list or the load fails, the `<h1>` takes focus instead. A push or replace to Today (nav, Discard) never focuses a row,
and the banner after Submit always wins. Today does **not** use React Router `<ScrollRestoration>` (which writes to
`sessionStorage`).

### Actions

| Action | What happens | Focus after | Announced |
|---|---|---|---|
| Tap / Enter on a participant row | The shell records the row (`data-return-key`) and that Today opened the note (memory). Navigate to `/participants/{id}/notes/{list.date}`; no router state. | The note screen's own rule | The note screen's `<h1>` |
| Tap / Enter on an unfinished-draft row | Same, to the working copy for `(participantId, noteDate)` (`…/notes/{noteDate}` or `…/notes/{noteDate}/edit` by `kind`, note-form.md Routes) | Note screen | Note screen |
| Space on a row | Scrolls the page (normal link behaviour; no handler added) | Unchanged | No |
| Type in "Find a participant" | List filters synchronously; Clear appears; no scroll jump; focus stays in the field (SC 3.2.2) | Field | Nothing while there are matches; **No participant matches ‘xyz’.** about 500 ms after typing stops, via the search's own `role="status"` |
| Clear | Field emptied, full list back, keyboard stays open on phones | Field | No (the field's label and empty value are read) |
| Enter or Escape in the field | Nothing | Field | No |
| Try again (load failed) | Refetch only the failed request(s) | Success: the `<h1>`. Failure: stays on Try again | Failure: "The participant list still did not load: …" (polite) |
| Add participant (manager, empty state) | Navigate to the Add participant page (§4.8) | That page's `<h1>` | That page |
| Window regains focus / tab becomes visible | `GET /api/auth/me` refetches (badge, `today`); the Today and drafts queries refetch silently. Rows update in place; no animation; scroll, focus and search text unchanged | Unchanged | No |
| Submit on the note form (from this screen) | The form removes the cached `['today']` and drafts queries, invalidates `['me']` when the response has a `flagStatus`, writes the message to `messageStore`, closes the dialog, then returns to Today (pop if Today is the previous entry, otherwise replace; note-identity-header.md) | Success banner | Through focus: "Note for Jane Citizen submitted" |
| Flagged / Report / Manage / Account / Sign out | Shell behaviour (app-shell-nav.md) | Shell | Shell |

### Tab order

Skip link → Grow2Notes → workers: Today → Account / managers: Account → Today → Flagged → Report → Manage →
(Account panel contents, when open) → *(banner and `<h1>` are focus targets but not Tab stops)* → Try again (only when
failed) → unfinished-draft rows → Find a participant field → Clear (only with text) → participant rows, or Add
participant (manager, empty state). No positive `tabindex`, no roving tabindex, no arrow-key handlers.

### Live regions on this screen (all in the DOM before any text is written)

| Region | Role | Carries |
|---|---|---|
| Load region status `<p>` | `role="status"` (polite) | Loading participants… · The participant list did not load: … · …still did not load: … |
| Search status `<p>` (inside `<search>`) | `role="status"` + `aria-live="polite"` | No participant matches ‘xyz’. (after a 500 ms pause; cleared when there are matches) |
| Page status region (`PageStatus`, visually hidden, app-shell.md component 3) | `role="status"` (polite) | Try again's "Loading…" busy label (the shared Button writes it here) |

Nothing else on Today is live: not the list, not the rows, not the tag, not the badge, not the banner (focus instead).
No `role="alert"` anywhere on this screen.

---

## Accessibility checklist

**Headings**
- [ ] One `<h1>`: "Today · Thursday 1 October", read as "Today, Thursday 1 October".
- [ ] `<h2>` "Your unfinished drafts" only when that section shows.
- [ ] Visually hidden `<h2>` "Participants" before the search box whenever the region is ready.
- [ ] No headings inside the banner or rows.

**Landmarks**
- [ ] `<header>` (banner), `<nav aria-label="Main">`, `<main id="main-content" tabindex="-1">`, `<search>`. All
  content is inside a landmark. No other landmarks, no `<section>` landmarks.

**Labels and names**
- [ ] Search field labelled by its visible `<label for>` "Find a participant"; description by `aria-describedby`.
- [ ] Clear button name "Clear search" (starts with the visible word; SC 2.5.3).
- [ ] Each row link's name is exactly the participant's name; its description is the status line (or the note date in
  the drafts section).
- [ ] Flagged nav link name starts with "Flagged". No `aria-label` replaces visible words anywhere on the screen.

**Keyboard**
- [ ] Everything reachable with Tab, in visual order; Enter follows links; Enter/Space press buttons.
- [ ] Focus ring visible on every stop, including the whole-row ring and the banner when focused.
- [ ] No keyboard trap, no character-key shortcuts, no Escape or Enter handlers in the search field.

**Screen reader** (expected; confirm with NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Chrome)
- [ ] Row: "Jane Citizen, link" then "Draft, You, saved 9:42 am" (no "dot").
- [ ] Flagged row: "… Submitted, Alex P., 4:12 pm, Flagged".
- [ ] No-note row: name and "link" only.
- [ ] Entering the list announces the item count ("list, 24 items"). Accepted: it is the participant count, not a note
  count, so it does not conflict with D25.
- [ ] After Submit: "Note for Jane Citizen submitted" read **once**; the next swipe reads the `<h1>`. If a tested
  screen reader does not read the focused banner, fall back to GOV.UK's `role="alert"` (status-messages.md).
- [ ] Background refreshes and badge changes announce nothing.

**WCAG 2.2 AA criteria this screen must meet**
1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.3.4 Orientation · 1.4.1 Use of Color · 1.4.3 Contrast
(Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast · 1.4.12 Text Spacing · 1.4.13 Content on
Hover or Focus (none added) · 2.1.1 Keyboard · 2.1.2 No Keyboard Trap · 2.1.4 Character Key Shortcuts (none) · 2.2.1
Timing Adjustable (nothing timed) · 2.2.2 Pause, Stop, Hide (no motion) · 2.4.1 Bypass Blocks · 2.4.2 Page Titled ·
2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus
Not Obscured (Minimum) (nothing sticky) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (44/48/56 px, A32) ·
3.1.1 Language of Page (`en-AU`) · 3.2.1 On Focus · 3.2.2 On Input · 3.2.3 Consistent Navigation · 3.2.4 Consistent
Identification · 3.3.2 Labels or Instructions · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

**User settings to test:** 320 px width; 200 % text and 400 % zoom; iOS and Android largest text; Windows contrast
theme (row ring, tag border, badge border, banner edge all visible); phone dark mode with browser page-darkening on;
outdoors in daylight at automatic brightness (status lines and tag readable).

---

## Acceptance criteria

**Content and copy**
- [ ] Title is "Grow2Notes – Today" on every visit, in every state; it never contains a name, a count or search text.
- [ ] `<h1>` reads "Today · Thursday 1 October" using the server's `today` (set the device clock to another date and
  time zone: the header and every row link still use the server date).
- [ ] Rows show exactly the four status forms in §States, built from `src/copy`; a row with no note has no second line.
- [ ] "Flagged" appears only on Submitted rows whose current version is flagged; never on a Draft row.
- [ ] Times are Melbourne time, lower-case am/pm with a non-breaking space; 12:00 shows "midday" (test 02:00Z on
  1 October 2026), and a time just after the daylight-saving change on 4 October 2026 is correct.
- [ ] Nowhere on the screen: a count of notes, "Not started", "Missing", "No note", "Overdue", progress, or the parent
  company's name (the CI copy test from microcopy.md fails the build if it appears).
- [ ] Long names (100 characters, unbroken strings, "O’Brien-Smith", "Zoë") wrap and are never truncated.

**Order and layout**
- [ ] DOM order: (banner) → `<h1>` → status/Try again → drafts section → hidden "Participants" → search → list or
  empty/no-match text.
- [ ] Rows follow the server order (family, then given); filtering and refetching never reorder them.
- [ ] Drafts section lists only drafts/pending edits not on today's list, sorted family, given, then note date oldest
  first; hidden entirely when there are none.
- [ ] At 320 px and at 200 % text, nothing scrolls sideways; the Clear button wraps below the field; the tag drops to
  its own line.
- [ ] Every target is at least 44 × 44 px; rows at least 56 px tall; search field and Try again at least 48 px.
- [ ] Nothing on the screen is `position: fixed` or `sticky`.

**Loading, empty and error**
- [ ] With a stubbed 999 ms response, no loading text ever appears; at 1,000 ms "Loading participants…" appears in the
  pre-existing `role="status"` element.
- [ ] The drafts section and list appear in the same render; no layout shift when the drafts section is present.
- [ ] The search box is not in the DOM while waiting, loading, after a failed load, or when there are no participants.
- [ ] Offline (`context.setOffline(true)`): the connection message appears within about 2 s, with Try again.
- [ ] A stubbed `503` shows the server cause sentence. No status code, red text or icon in either message.
- [ ] Try again: `aria-disabled` while running (never `disabled`); a second failure shows "The participant list still
  did not load: …" and focus stays on the button; success moves focus to the `<h1>`.
- [ ] A failed background refetch leaves the rows on screen unchanged (test with `isLoadingError`, not `isError`).
- [ ] "No participants yet." shows only after a successful empty response; managers also get an "Add participant"
  link, workers do not.
- [ ] A `401` shows no load error; the signed-out state appears, and after signing in the list loads.

**Search**
- [ ] "jan", "JANE", "cit jane", "citizen, jane", "Jane." find Jane Citizen; "zoe" finds Zoë; "obr" finds O’Brien;
  "Alex" does not match a row only because Alex P. wrote its note.
- [ ] No request is sent while typing; the filter updates on every keystroke with no debounce.
- [ ] No match: the visible message appears at once; the status region receives the same text about 500 ms after
  the last keystroke and is emptied when matches return.
- [ ] Clear empties the field, restores the list and leaves focus in the field (Playwright); on a phone the keyboard
  stays open.
- [ ] Enter and Escape in the field change nothing; the page never reloads.
- [ ] The typed text never appears in the URL, `history.state`, the title, query keys, `localStorage`,
  `sessionStorage` or IndexedDB, and is gone after leaving Today and coming back.

**Focus and navigation**
- [ ] After Submit: the banner is before the `<h1>`, receives focus, has no role and no live attribute, is read once,
  and is gone after leaving Today or reloading. The row shows Submitted with no flash of "Draft".
- [ ] Back (or "‹ Today", which pops) from a note opened from a row focuses that row's link and scrolls it into view,
  also when the list had to be refetched; if the row is no longer listed, the `<h1>` gets focus.
- [ ] Arriving by the nav or wordmark, or after Discard (a replace), focuses the `<h1>`.
- [ ] Row links carry only IDs and dates in `href` and push no router state: `history.state` holds only React
  Router's key (app-shell.md).
- [ ] Opening a row whose cached status is stale (another device changed it) shows the destination's fresh state.

**Refresh**
- [ ] Window focus and `visibilitychange` refetch `me` (badge) and the Today and drafts queries; nothing polls; the
  search text, scroll position and focus are unchanged after a refresh.
- [ ] When `me.today` changes, the list for the new date loads; links use the list's `date`.

**Privacy (D22, §9.6)**
- [ ] After a full session on Today, browser storage (local, session, IndexedDB, Cache Storage) holds no participant
  name, note data, message or search text.

**Automated and manual checks**
- [ ] `@axe-core/playwright` passes in Waiting, Loading, Ready (with and without drafts), No participants (worker and
  manager), No match, Load failed and After Submit.
- [ ] Manual pass with NVDA + Chrome, VoiceOver + iOS Safari and TalkBack + Chrome covering the screen-reader items in
  the checklist, the 500 ms announcement and the banner-read-once check.

### Implementation sketch (React 19, TanStack Query 5, CSS Modules)

```tsx
// src/today/TodayPage.tsx: composition only; each piece is specified in its component file
export function TodayPage() {
  usePageTitle('Today');                                      // "Grow2Notes – Today"
  const me = useMe();                                         // shell's ['me'] query, already loaded
  const [submitted] = useState(messageStore.peek);            // status-messages.md (memory only)
  useEffect(() => { messageStore.clear(); }, []);
  // No return-row code here: the route declares handle: { returnFocus: {} } and rows carry data-return-key.

  const today = useQuery({ ...todayQuery(me.today), refetchOnWindowFocus: true });   // key ['today', me.today]
  const drafts = useQuery({ ...myDraftsQuery(), refetchOnWindowFocus: true });       // key ['me', 'drafts']
  const [query, setQuery] = useState('');                     // component state only

  return (
    <>
      {submitted && <Banner tone="success" takeFocus>{submitted}</Banner>}
      <PageHeading>{copy.today}<Sep />{dateToday(parseDateOnly(me.today))}</PageHeading>
      <LoadRegion queries={[today, drafts]} loading={copy.loadingParticipants} what={copy.theParticipantList}>
        {() => (
          <TodayLists list={today.data!} drafts={drafts.data!} isManager={me.role === 'Manager'}
                      query={query} onQuery={setQuery} />
        )}
      </LoadRegion>
    </>
  );
}
```

- `TodayLists` renders the drafts section, the hidden heading, `FindParticipant` (only when
  `participants.length > 0`), then the `ParticipantRow` list, the no-match text, or the empty state. It calls
  `useReturnFocusReady(true)` once the lists render, so the shell can return focus to the opened row on a pop.
- The page also renders the shared `<PageStatus>` (app-shell.md component 3) for Try again's busy label.
- If `today.data.date !== me.today`, invalidate `['me']` (exact) once, so the header and the links agree.
- Invalidate `['me']` with `exact: true`: with prefix matching, the shell's `['me']` invalidation on every screen change
  also invalidates `['me', 'drafts']`.

---

## Conflicts resolved

| # | Where the component specs disagreed | Chosen for Today | Why |
|---|---|---|---|
| 1 | **Return focus.** app-shell-nav.md: Back to Today focuses the `<h1>` and relies on `<ScrollRestoration>` (sessionStorage). participant-list-rows.md: focus the row the user opened, memory only, no ScrollRestoration. | Banner, then (on a pop only) the returned-to row, then the `<h1>`, all through the shell's one route-change rule (`handle.returnFocus`, app-shell.md). The earlier module-variable `useReturnRow` is removed, so a replace (Discard) focuses the `<h1>`. | Keeps a keyboard or screen-reader user's place in a long list without browser storage (D22, §9.6). APG's return-to-invoker rule supports it by analogy [Standard/Opinion]; the n=5 heading-focus finding is about arriving at a new screen and still applies to every other arrival [Research]. |
| 2 | **After-submit cache.** status-messages.md: `setQueryData` to mark the row Submitted, then invalidate. participant-list-rows.md and empty-loading-error.md: `removeQueries` for Today and drafts, then navigate. | `removeQueries`, then navigate. | The server stays the only author of the row (invariant I5); no client-built row can disagree with the server; the row can never show "Draft" for a moment. The banner appears at once regardless. |
| 3 | **Search box while loading.** participant-list-rows.md: header and search show at once. search-filter.md and empty-loading-error.md: the box appears with the list. | With the list. | Prevents typing into an unloaded list and a false "No participant matches" ([Research] NN/g misleading system status). |
| 4 | **Load-failed wording.** participant-list-rows.md: "Couldn't load the participant list. Check your connection and try again." microcopy.md: "The participant list did not load: no connection. Try again." empty-loading-error.md: "Could not load the participant list." + a cause sentence, and "Still could not…" on repeat. | microcopy.md §9: "The participant list did not load: [cause]. Try again." and "…still did not load…" on a repeat. | Settled app-wide in the editorial pass (microcopy.md §9): one wording for the same failure on every screen. empty-loading-error.md keeps the mechanism, including the changed "still" text that makes repeat failures audible. |
| 5 | **No-match punctuation.** design.md and search-filter.md: "No participant matches 'xyz'" (no full stop). microcopy.md: typographic quotes and a full stop. | "No participant matches ‘xyz’." | microcopy.md's rule that sentence messages end with a full stop and Style Manual quotes; the words are unchanged (N). |
| 6 | **12:00.** participant-list-rows.md's formatter: "12:00 pm". microcopy.md: "midday" / "midnight". | midday / midnight, via the one shared `time()` in `src/copy/format.ts`. | [Convention] Style Manual and GOV.UK; one formatter for the whole app. |
| 7 | **Own submitted note.** participant-list-rows.md and microcopy.md: "Submitted · You · 4:12 pm". status-tags.md: the author's display name. | "Submitted · You · 4:12 pm" (P). | Follows design.md's own "Draft · You" pattern and the API's `isMine`; flagged for owner sign-off. |
| 8 | **Pending-edit rows in "Your unfinished drafts".** microcopy.md proposes adding "Editing submitted note". participant-list-rows.md and status-tags.md: name and date only. | Name and note date only, exactly as §4.2 says. | Adds nothing the design did not specify; the destination's heading names the edit (4.3). Left as an open question. |
| 9 | **Status word weight.** status-tags.md: normal weight. foundations.md and participant-list-rows.md: bold. | Bold (700), `--colour-text`; rest of the line `--colour-text-secondary`. | foundations.md's type scale sets "700 for the status word"; front-loading the state word helps scanning ([Research] NN/g text-scanning, https://www.nngroup.com/articles/text-scanning-patterns-eyetracking/). |
| 10 | **Flagged tag look.** status-tags.md: no border, regular weight, bold in forced colours. participant-list-rows.md: 1 px border, semibold, text in `--colour-text`. foundations.md: `--colour-attention-edge` is the "attention tag border" and is contrast-tested against the tag fill. | 1 px `--colour-attention-edge` border, weight 400, `--colour-attention-text` on `--colour-attention-tint-strong`. Forced colours keep the border. | Two of three specs, including the token owner. The border keeps the tag's shape in glare, where the 1.3:1 fill disappears [Opinion]. GOV.UK's "outline looks like a button" concern matters less here because the tag sits inside a whole-row link, so tapping it does what the user expects. Regular weight keeps bold for the name and the status word. status-tags.md now records this bordered tag as the one tag style app-wide (editorial pass), so Past notes, Flagged and Version history match it (SC 3.2.4). |
| 11 | **Weights and breakpoint.** participant-list-rows.md: weight 600 and a 48rem list cap. foundations.md: weights 400/700 only, one 40rem breakpoint. | 700 and 40rem. | 600 renders differently on iPhone and Android ([Standard] CSS font matching); one breakpoint app-wide. |
| 12 | **Refetch on focus.** app-shell-nav.md sets `refetchOnWindowFocus: false` as the default. participant-list-rows.md: Today refetches on focus. | Today's two queries opt in (`refetchOnWindowFocus: true`). | autosave-status.md confirms §6.8's focus refresh is "for Today and the badge"; Today has no editable server data that a refetch could overwrite. |
| 13 | **Error test.** participant-list-rows.md: `isError`. empty-loading-error.md: `isLoadingError`. | `isLoadingError`. | `isError` also fires on a failed background refetch and would replace good rows with an error. |
| 14 | **Header date vs list date.** The `<h1>` uses `me.today`; rows use `/api/today`'s `date`. | Query key `['today', me.today]`; if the response date differs, invalidate `['me']`. | They can never show two different days for long, including at midnight. |
| 15 | **Separator in the `<h1>`.** Not covered by any spec. | The same hidden-dot plus spoken-comma separator as the status lines. | One rule for "·" across the app (microcopy.md). |

---

## Tensions with decisions

Recorded once, with evidence; no change recommended.

- **"Your unfinished drafts" also holds pending edits (§4.2, A17)**, which the rest of the design treats as a different
  thing from drafts (3.4, 3.5). [Research] NN/g heuristic 4: users "should not have to wonder whether different
  words… mean the same thing", https://www.nngroup.com/articles/consistency-and-standards/. Impact is small because the
  destination's heading names the edit.
- **Family-name sort with given-name-first display (A5).** Nothing on the row shows the sort key; iOS Contacts bolds
  it [Convention, third-party documentation]. No research found on whether this matters for a list under about 100
  names that has a search box. Kept as designed.
- **Page title order (§4.0).** "Grow2Notes – Today" puts the app name first; HMRC's pattern and W3C's 2.4.2 examples
  put the page name first, https://design.tax.service.gov.uk/hmrc-design-patterns/page-title. Both pass SC 2.4.2.
- **D22 (online only).** A first visit to Today without a connection can only show "The participant list did not
  load: no connection…". Mobile guidance often suggests caching last-known content ([Convention] Babich, Smashing Magazine 2016,
  https://www.smashingmagazine.com/2016/09/how-to-design-error-states-for-mobile-apps/). Within a session, the
  in-memory cache softens it.

---

## Open questions

For the owner unless marked otherwise. None of them adds a feature.

1. Approve the proposed copy: "Submitted · You · 4:12 pm"; "Loading participants…"; "The participant list did not
   load: no connection. Try again." and the server and "still" forms; "Try again" / "Loading…";
   "Clear"; the hidden "Participants" heading and the hidden search description.
2. Should pending edits in "Your unfinished drafts" say "Editing submitted note" (design.md's own words), or stay as
   name and date only (current spec)?
3. A pending edit on **today's** submitted note shows only "Submitted · …" on its row and is excluded from the drafts
   section, so nothing on Today shows it. Intended?
4. Two active participants with the same full name show identical rows; workers get no date of birth. Accepted for v1?
5. Workers on an empty Today see only "No participants yet." with no next step (the design gives managers the action).
   Keep as is?
6. After submitting an earlier-day draft (or a manager's past-day note), Today says "Note for Jane Citizen submitted"
   without the note's date, and Jane's row may show a different note or none. Keep as designed?
7. Confirm "midday" in status lines ("saved midday", "Submitted · Alex P. · midday").
8. For testing (not the owner): is the focused banner read exactly once; is the 500 ms no-match delay right with NVDA
   and VoiceOver; and do screen-reader users prefer row focus to heading focus when coming back to Today?
