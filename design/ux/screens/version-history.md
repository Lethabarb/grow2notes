# Version history

Screen spec for design.md **4.5 Version history (managers)**. It puts together these component files, and where they disagree it picks one answer (see Conflicts resolved): [version-history](../components/version-history.md), [chronological-list](../components/chronological-list.md), [note-read-view](../components/note-read-view.md), [status-tags](../components/status-tags.md), [foundations](../components/foundations.md) and [microcopy](../components/microcopy.md). It also uses the shared pieces from [note-identity-header](../components/note-identity-header.md), [app-shell-nav](../components/app-shell-nav.md), [empty-loading-error](../components/empty-loading-error.md) and [checkbox-list](../components/checkbox-list.md), so that this screen looks and works like the read view the manager has just left.

Evidence grades: **[Research]** studies and usability write-ups, **[Standard]** WCAG, WAI-ARIA and HTML, **[Convention]** established design systems, **[Opinion]** reasoned judgement. Copy marks: **(V)** word for word from design.md, **(N)** design.md wording with only the format normalised under microcopy.md, **(P)** proposed (not in design.md). Every (P) string here on 9 October 2026 was approved as written (D67); a (P) string added later still needs the owner's approval.

---

## Purpose and who uses it

**Purpose (4.5):** "show every version of a submitted note, with who saved it and when" (D15, D19). **Primary action:** open a version. There is no restore. To bring old wording back, a manager edits the note (4.5, 3.5). A version shows "the groups picked in that version" (4.5, D44–D47): which common-item groups were picked is part of each version, like the ticks.

**Who:** managers only (A13). Earlier versions are the restricted record under HPP 6.7, because they can hold content that a correction replaced, such as a note first written on the wrong participant (3.9, D39). Workers never see the way in, and a worker who types the URL gets **Page not found**.

**Why managers come here (all from the design, none new):**
- to see who changed a submitted note and when (D19, 3.5)
- to read an earlier version in full, for example after a wrong-participant correction (3.9) or a correction request (11.6)
- to copy old wording out of an earlier version and paste it into an edit, since there is no restore (4.5)

**How they get here:** only through the **Version history** link in the read view (4.4, owned by note-read-view.md). Managers see that link on every submitted note, including one with a single version.

**Two pages, one screen.** Both pages have the title "Grow2Notes – Version history" (app-shell-nav.md's fixed list). URLs hold only IDs, dates and version numbers (4.0).

| Page | Route | Shows |
|---|---|---|
| **A. Version list** | `/participants/{participantId}/notes/{noteDate}/versions` | Participant, note date, current version number, then every version, newest first (4.5 items 1–2) |
| **B. One version** | `/participants/{participantId}/notes/{noteDate}/versions/{n}` | Version n in full, read-only (4.5 item 3) |

**Not on this screen:** compare or diff, restore, edit, previous and next, mark reviewed, export, print. The design specifies none of them. version-history.md records the compare and restore convention once under its "Tensions with decisions" and recommends no change.

---

## Layout - phone

375 px wide, default text size. The app header and nav come from app-shell-nav.md, and no nav item is marked current on note screens. The back link sits in the shell's before-main bar slot (app-shell.md component 3a), static.

**A. Version list (default: 3 versions)**

```
+-------------------------------------+
| Grow2Notes               Account v  |  app header (app-shell-nav.md)
| Today  Flagged 3  Report  Manage    |
+-------------------------------------+
| ‹ Note                              |  back link, before <main>, static, ≥ 44 px tall
+-------------------------------------+
| Version history                     |  <h1> caption line (body size, secondary colour)
| Jane Citizen                        |  <h1> name line (28 px, bold), translate="no"
| Thursday 1 October 2026             |  <h1> date line, <time>, body size, regular
|                                     |
| The current version is version 3.   |  body text
|                                     |
|                                     |  <p role="status"> (in the DOM, empty, no height)
+-------------------------------------+
| Version 3                           |  row link: bold, underlined, action blue
| Sam Lee (manager) · Thu 1 Oct 2026, |  meta line (16 px, secondary colour)
| 5:03 pm                             |  whole row is the link, ≥ 56 px tall
+-------------------------------------+
| Version 2                           |
| Priya Nair · Thu 1 Oct 2026,        |
| 4:40 pm                             |
+-------------------------------------+
| Version 1                           |
| Submitted · Priya Nair ·            |  "Submitted" only on version 1
| Thu 1 Oct 2026, 4:12 pm             |
+-------------------------------------+
```

**A, only one version**

```
| Version history                     |
| Jane Citizen                        |
| Thursday 1 October 2026             |
|                                     |
| The current version is version 1.   |
| Not edited since submit             |  4.5 state string, word for word
+-------------------------------------+
| Version 1                           |  the single row stays, so "open a version" still works
| Submitted · Priya Nair ·            |
| Thu 1 Oct 2026, 4:12 pm             |
+-------------------------------------+
```

**A, past-day note (variant):** one line is added after the current-version sentence. In this example the note date line reads "Monday 28 September 2026", and Jo Smith wrote the note on 1 October:

```
| [Past-day note] written by Jo Smith |  neutral tag + detail (A11, microcopy wording)
| (manager) on Thu 1 Oct 2026,        |
| 10:14 am                            |
```

**A, loading after 1 s, and load failed**

```
| ‹ Note                              |      | ‹ Note                              |
+-------------------------------------+      +-------------------------------------+
| Loading version history…            |      | Version history                     |  generic <h1> on failure
|                                     |      | Version history did not load: no    |  <p role="status">
|                                     |      | connection. Try again.              |
|                                     |      | [          Try again          ]     |  secondary button, full width
```

**B. One version (an earlier version, flagged)**

```
+-------------------------------------+
| ‹ Version history                   |  back link, before <main>
+-------------------------------------+
| Version 2                           |  <h1> caption line
| Jane Citizen                        |  <h1> name line
| Thursday 1 October 2026             |  <h1> date line
|                                     |
| This is an earlier version. The     |  bold body text, straight after the heading
| current version is version 3.       |  "version 3" is a link to that version
|                                     |
| Saved by Priya Nair,                |  16 px, secondary colour (as the read view's
| Thu 1 Oct 2026, 4:40 pm             |  byline); version 1 says "Submitted by"
| [Flagged] Reason: Mentioned pain in |  only when THIS version is flagged
| her left knee after the walk.       |
|-------------------------------------|  1 px rule
| 1. Goals                            |  <h2>, TickListRead (checkbox-list.md)
| [v] Makes own breakfast             |
| [ ] Catches bus to day program      |
|-------------------------------------|
| 2. Common items                     |  <h2>
| Every note                          |  <h3>, only when it has items
| [v] Medication prompted             |
| Community outing                    |  <h3>: each group picked in THIS
| [v] Travelled by bus or train       |  version, in the manager's order;
| [ ] Paid for own purchases          |  unpicked groups never appear (D47)
|-------------------------------------|
| 3. Guided notes                     |  <h2>
| Jane was keen to go to the library  |  plain, selectable text; line breaks kept
| today. She caught the bus with ...  |  never cut short, never under a link
+-------------------------------------+
```

**B, current version:** the bold line reads "This is the current version." and has no link. Everything else is the same. On a past-day note, the past-day line sits between "Saved by …" and the Flagged line.

---

## Layout - laptop

- **Same single column, same order, same words** as the phone (4.0: only the setup screens use the extra width). The content column is capped at `--measure` (40rem) and left-aligned inside the 60rem page container, so the back link, heading and rows share one left edge with the header. [Convention] foundations.md.
- **No list-and-detail split pane, and no table** (Version | By | Date). A split pane would hide B's URL and Back behaviour, and a table separates who and when from the version and invites sorting nobody asked for. [Opinion] version-history.md, participant-list-rows.md.
- At 40rem and wider: h1 name 32 px, page gutter 32 px, section gaps 48 px (foundations.md tokens). Body text stays 18 px.
- **Rows** keep the stacked two-line layout. Hover tint (`--colour-hover`) and the thicker underline apply only inside `@media (hover: hover)`.
- **B's text column** at 40rem with 18 px text is about 70 characters a line. That is inside Baymard's 50–80 range and under WCAG 1.4.8's 80 (AAA, advisory). [Research/Standard] foundations.md, note-read-view.md.
- **Try again** is `width: auto` (at least 8rem) instead of full width.

---

## Components, in order

### Page A: version list

| # | Component | Settings on this screen |
|---|---|---|
| 1 | **App header and navigation**: [app-shell-nav](../components/app-shell-nav.md) | No nav item has `aria-current`, because note screens can be reached from several sections. The badge behaves as on every manager screen. Page title "Grow2Notes – Version history". |
| 2 | **Back link**: the shared BackLink (app-shell.md component 3a), [note-identity-header](../components/note-identity-header.md) | Label **Note** with a decorative `‹` SVG (`aria-hidden="true"`, `focusable="false"`). `href` = `/participants/{id}/notes/{date}` (the read view). If the previous history entry is that read view (the shell's opened-from record), tapping calls `navigate(-1)`. Otherwise it follows the `href`. Modifier clicks always follow the `href`. Rendered into the shell's **before-main bar slot**, ≥ 44 px tall, **static, not sticky** (see Conflicts resolved 7), and hidden with the route when signed out. |
| 3 | **Identity heading**: [note-identity-header](../components/note-identity-header.md) | `<h1 tabIndex={-1}>` with three block lines (the app-wide identity-heading rule): caption **Version history** (body size, `--colour-text-secondary`), the participant's full name (`--font-size-h1`, 700, `translate="no"`, never truncated or re-cased), and the note date (item 4). Literal spaces between them, so the name is "Version history Jane Citizen Thursday 1 October 2026". Not rendered until the data is ready. |
| 4 | **Note date line**, inside the `<h1>`: [note-identity-header](../components/note-identity-header.md), [microcopy](../components/microcopy.md) | `<time dateTime="2026-10-01">Thursday 1 October 2026</time>`, the `dateLong` token, formatted with no time-zone conversion; body size, weight 400, text colour (as on the read view and the form). |
| 5 | **Current version sentence**: [version-history](../components/version-history.md) | "The current version is version 3." **(P)**, covering 4.5 item 1. The number is the highest `versionNumber` from the version list query, the screen's only source for "current". Plain text, no link. |
| 6 | **Past-day line** (only when `isPastDayNote`): [status-tags](../components/status-tags.md), [microcopy](../components/microcopy.md) | `<p>` holding a `Tag kind="pastDay"` (neutral), a visually hidden comma, then "written by Jo Smith (manager) on Thu 1 Oct 2026, 10:14 am" **(N)**. The name is the note's author and the time is `startedAtUtc` from `GET {base}`. It follows A11 / 3.8, "everywhere it appears". |
| 7 | **One-version sentence** (only when there is exactly one version): [version-history](../components/version-history.md), [chronological-list](../components/chronological-list.md) | "Not edited since submit" **(V)**, as a plain `<p>` in body text and text colour, with no full stop. The list still renders below it. |
| 8 | **Status line**: [empty-loading-error](../components/empty-loading-error.md), [microcopy](../components/microcopy.md) | One `<p role="status">` in `<main>` from the first render. Empty and `margin: 0` when idle, and never `display: none`. It holds only the loading and load-failed text (see States). |
| 9 | **Version list**: [chronological-list](../components/chronological-list.md), *whole* mode, with the row content from [version-history](../components/version-history.md) and `MetaLine` from [status-tags](../components/status-tags.md) | `<ol reversed role="list">`, newest first, sorted on the client by `versionNumber` whatever order the API returns. No **Show older**, no counts. **Row link**: text exactly "Version 3", bold (700), `--colour-action`, underlined, `::after` overlay so the whole `<li>` is the target. **Meta line** (`id`, pointed to by the link's `aria-describedby`), 16 px, `--colour-text-secondary`: `[Submitted ·] {saver}[ (manager)] · {dateTime}`. "Submitted" appears on version 1 only. **"(manager)"** shows when `createdBy.id !== note.author.id`, never from anyone's current role. **No** chevron, Current, Latest, Flagged or Edited tags. Row `min-block-size: 3.5rem` (56 px), `padding-block: 0.75rem`, 1 px `--colour-divider` between rows. Key: `versionNumber`. |
| 10 | **Try again** (load failed only): [empty-loading-error](../components/empty-loading-error.md) | Shared secondary `<button type="button">`, after the status line and outside it. It refetches only the failed queries. Its "Loading…" busy label goes to the page status region (item 11). |
| 11 | **Page status region**: [app-shell](app-shell.md) component 3 | The shared visually hidden `<p role="status">` (`PageStatus`), rendered from the first render. On this screen it carries only Try again's busy label. |

### Page B: one version

| # | Component | Settings on this screen |
|---|---|---|
| 1 | **App header and navigation** | As on A. |
| 2 | **Back link** | The shared BackLink in the shell's bar slot. Label **Version history**, `href` = `…/versions`. If the previous history entry is page A for this note, it calls `navigate(-1)`, so A restores its place. |
| 3 | **Identity heading with its date line** | As on A, with the caption **Version {n}** taken from the URL ("Version 2 Jane Citizen Thursday 1 October 2026"). Rendered once, with all the data, and never changed after it has focus. |
| 4 | **Currency line**: [version-history](../components/version-history.md) | `<p>`, bold (700), body size, text colour, directly after the `<h1>`. Current: "This is the current version." **(P)**. Earlier: "This is an earlier version. The current version is [version 3]." **(P)**, where "version 3" is a `<Link>` to `…/versions/3`. The link is `display: inline-block; padding-block: 0.5625rem; margin-block: -0.5625rem;`. That makes its tap area about 44 px tall without changing line spacing. [Opinion]: an inline link is exempt under SC 2.5.8, but A32 asks for 44 px. No inset box and no tint: GOV.UK found inset text gets missed. [Research] https://design-system.service.gov.uk/components/inset-text/ |
| 5 | **Saved-by line**: [version-history](../components/version-history.md) | `<p>` at `--font-size-small` (16 px) in `--colour-text-secondary` (9.0:1), the same size and colour as the read view's byline and status lines (participant-notes.md, Conflicts 6), so one note's metadata looks the same on both pages; the reason on the Flagged line stays in `--colour-text` because it is user-written. Version 1: "Submitted by Priya Nair, Thu 1 Oct 2026, 4:12 pm". Later versions: "Saved by Sam Lee (manager), Thu 1 Oct 2026, 5:03 pm". The words come from the 11.6 export ("submitted by", "saved by"), with `dateTime`. The name and suffix use the same rule as the row the manager tapped. |
| 6 | **Past-day line** (only when `isPastDayNote`) | Same component and words as on A. |
| 7 | **Flagged line** (only when *this version* has `isFlagged`): [status-tags](../components/status-tags.md), [note-read-view](../components/note-read-view.md) | `<p>`: `Tag kind="flagged"` (amber), a visually hidden comma, then "Reason: " and the reason as a text node with `white-space: pre-wrap`. There are **no** Edited, To review or Reviewed lines, because they describe the note, not this version, and live on the read view. |
| 8 | **Status line and Try again** | As on A. |
| 9 | **Goals and Common items**: [checkbox-list](../components/checkbox-list.md) `TickListRead`, [group-picker](../components/group-picker.md) (Per-screen notes) | Headings `<h2>` "1. Goals" and "2. Common items" **(V)**. Goals: every snapshot goal in snapshot order, with an icon plus visually hidden ", ticked" / ", not ticked". **Common items (D47):** `<h3>` "Every note" **(V)** with its items (only when it has items), then `<h3>` with the name of **each group picked in this version**, as stored in the version's snapshot, in configured order, with every item in it ticked or not ticked (`TickListRead` with `headingLevel={3}`). Groups not picked in this version do not appear, so a change of picks between versions shows as different groups on the two version pages. Nothing is added to the version list (no "groups changed" tag, no compare). Empty lists: "No goals set" / "No common items set" **(V, 11.3)**, the second when no group is shown. Keyed by index (the API gives no IDs). |
| 10 | **Guided notes**: [note-read-view](../components/note-read-view.md) | `<h2>` "3. Guided notes" **(V)**, then one `<div>` holding the stored text as a React text node. `white-space: pre-wrap; overflow-wrap: break-word; line-height: 1.5`. Never trimmed, split into paragraphs, cut short, linkified or rendered as HTML. Guide prompts never appear (D34). |
| — | **Actions** | **None.** No Edit (3.8: nobody edits an old version, and the current note's Edit is on the read view), no Restore (4.5), no Mark reviewed, no Previous/Next, no Past notes link. |

Sections 9 and 10 are separated by a 1 px `--colour-divider` rule and `--section-gap`, as in the read view. There are no cards, shadows or tinted panels.

### Copy on this screen

| Where | Text | Mark |
|---|---|---|
| Page title (A and B) | Grow2Notes – Version history | (V pattern, 4.0) |
| A back link / B back link | Note / Version history | (P), labelled with the destination as note-identity-header.md does |
| A `<h1>` | Version history / Jane Citizen / Thursday 1 October 2026 | (V) "Version history"; name from 4.5 item 1 |
| B `<h1>` | Version 2 / Jane Citizen / Thursday 1 October 2026 | (V) "Version 2" |
| Note date (third line of the `<h1>`) | Thursday 1 October 2026 | (V format, `dateLong`) |
| Current version (A) | The current version is version 3. | (P) |
| One version | Not edited since submit | (V) |
| Row link | Version 3 | (V) |
| Row meta, version 2 and later | Sam Lee (manager) · Thu 1 Oct 2026, 5:03 pm | (N) weekday added by `dateTime` (microcopy.md §4.5) |
| Row meta, version 1 | Submitted · Priya Nair · Thu 1 Oct 2026, 4:12 pm | (V) label "Submitted", (N) date |
| B currency, current | This is the current version. | (P) |
| B currency, earlier | This is an earlier version. The current version is version 3. | (P), "version 3" is the link |
| B saved-by | Saved by Sam Lee (manager), Thu 1 Oct 2026, 5:03 pm · Submitted by Priya Nair, Thu 1 Oct 2026, 4:12 pm | (V words from 11.6), (N) date |
| Past-day line | [Past-day note] written by Jo Smith (manager) on Thu 1 Oct 2026, 10:14 am (note date Monday 28 September 2026) | (N) microcopy.md's single wording for screen and files |
| Flagged line | [Flagged] Reason: Mentioned pain in her left knee after the walk. | (V) "Reason" label from 4.3 |
| Section headings / empty lists | 1. Goals · 2. Common items · 3. Guided notes / No goals set · No common items set | (V) |
| Common-item group headings (B) | Every note · each picked group's name as stored in the version | (V) "Every note"; names are data |
| Loading (after 1 s) | Loading version history… · Loading version 2… | (P) |
| Load failed | Version history did not load: no connection. Try again. · Version history did not load: something went wrong. Try again. · (B) Version 2 did not load: … | (P) microcopy.md "Load failed" pattern |
| Load failed again | Version history still did not load: no connection. Try again. (and the same for the other variants) | (P) |
| Try again button / busy label | Try again / Loading… | (P) empty-loading-error.md |
| Not found | Page not found · If you typed or pasted the web address, check it is correct. · Go to Today | app-shell-nav.md, empty-loading-error.md |

Formats (microcopy.md §3): times are Melbourne time, like "4:12 pm", with a non-breaking space before am/pm, "midday" and "midnight" at exactly 12:00, and no leading zero. `dateTime` is "Thu 1 Oct 2026, 4:12 pm", with a non-breaking space between day and month and three-letter months with no full stops ("Sep", not "Sept"). Every date and time is wrapped in `<time dateTime>`. Times are never relative.

### Data and queries

All queries are memory-only, with no persister and no browser storage (D22, 9.6). They share the retry and network settings in empty-loading-error.md.

| Query | Key | Options | Used for |
|---|---|---|---|
| `GET /api/participants/{id}` | `['participants', id]` | defaults | name in the `<h1>` |
| `GET {base}` | `['notes', id, date]` | `refetchOnWindowFocus: false` (same as the read view) | `status` (Draft → not found), `author.id`, `isPastDayNote`, `startedAtUtc` |
| `GET {base}/versions` | `['notes', id, date, 'versions']` | defaults (refetches on window focus; no polling, 6.8) | rows, current version number |
| `GET {base}/versions/{n}` (B only) | `['notes', id, date, 'versions', n]` | `staleTime: 'static'` (fall back to `Infinity` if the pinned v5 lacks it) | the version's content, including `commonItemGroups: [{name, isEveryNote, items: [{text, isTicked}]}]`: only Every note and the groups picked in that version (design.md §6.3, D47) |

- Shared settings: `networkMode: 'always'` (offline fails in about 1 s instead of sitting "paused"), a 10 s timeout, **one** silent retry for network errors and 5xx only, and no retry for 401/403/404. [Convention] https://tanstack.com/query/v5/docs/framework/react/guides/network-mode; [Opinion] empty-loading-error.md.
- `staleTime: 'static'` is right because `NoteVersion` is insert-only (5.8). TanStack's docs say `invalidateQueries()` "has no effect on `staleTime: 'static'`" (checked 1 October 2026), so a prefix invalidation after Save changes refetches the note and the version list but leaves version entries alone. [Convention] https://tanstack.com/query/v5/docs/framework/react/guides/important-defaults
- note-form.md invalidates the prefix `['notes', id, date]` after Save changes and Cancel; the read view (participant-notes.md) and flagged.md use the same prefix, so one invalidation refreshes the note, this list and each version.
- **Current version** = `versions.data[0].versionNumber` after the sort. It is never stored and never taken from `GET {base}`, so A and B always agree.
- **Not found is decided by the server**, not by the cached list. It applies when the URL number is not `^[1-9][0-9]*$`, `GET {base}` or `GET {base}/versions/{n}` returns 404, the note's `status` is Draft, or the version list is empty. If version n loads but the cached list's highest number is lower (another manager has just saved), refetch the list and keep the page in its loading state until the list includes n.

```tsx
// Routes. No <ScrollRestoration>: it writes to sessionStorage.
{ path: 'participants/:participantId/notes/:noteDate', children: [
  { index: true, element: <NoteReadPage /> },
  { element: <ManagerOnly />, children: [      // worker -> Page not found, no request sent
    { path: 'versions', element: <VersionHistoryPage /> },
    { path: 'versions/:versionNumber', element: <VersionPage /> },
  ]},
]}

// VersionRow.tsx: all visible strings come from src/copy (jsx-no-literals, microcopy.md)
function VersionRow({ v, authorId }: { v: VersionSummary; authorId: string }) {
  const metaId = useId();
  const saver = v.createdBy.displayName + (v.createdBy.id !== authorId ? copy.managerSuffix : '');
  return (
    <>
      <Link to={String(v.versionNumber)} className={rows.rowLink} aria-describedby={metaId}
            onClick={() => rememberPlace(String(v.versionNumber))}>{copy.version(v.versionNumber)}</Link>
      <MetaLine id={metaId} parts={[
        ...(v.versionNumber === 1 ? [copy.submitted] : []),
        saver,
        <time dateTime={v.createdAtUtc}>{dateTime(v.createdAtUtc)}</time>,
      ]} />
    </>
  );
}
```

`dateTime(utcIso)` is added to `src/copy/format.ts` as `dateShort + ', ' + time`, built from `formatToParts` numeric parts and fixed English tables (microcopy.md). `rememberPlace` is the in-memory place map from chronological-list.md.

---

## States

| State | When | A. Version list | B. One version |
|---|---|---|---|
| **Waiting** | Requests in flight, under 1 s | Back link only. `<main>` holds the empty status line. No `<h1>`, no placeholder rows. | Same. |
| **Loading** | Still in flight after 1 s | Status line: "Loading version history…". No spinner, no skeleton. | "Loading version 2…" |
| **Ready (default)** | Participant, note and versions loaded (and version n on B) | Everything renders in one pass: `<h1>`, date, current-version sentence, past-day line if any, list. The status line is cleared. | `<h1>`, date, currency line, saved-by line, past-day and Flagged lines if any, Goals, Common items, Guided notes. |
| **Only one version** (4.5 state) | `versions.length === 1` | "The current version is version 1.", then "Not edited since submit", then the single Version 1 row. | Version 1 says "This is the current version." and "Submitted by …". |
| **Current version** | n = current | — | "This is the current version." (no link) |
| **Earlier version** | n < current | — | "This is an earlier version. The current version is version 3." with the link |
| **Flagged version** | that version's `isFlagged` | No tag on rows. | "[Flagged] Reason: …" |
| **Past-day note** | `isPastDayNote` | Past-day line under the current-version sentence. | Past-day line under the saved-by line. |
| **Empty goals / items** | snapshot had none, or no common-item group is shown | — | "No goals set" / "No common items set" under the heading. |
| **Common items** | always | — | Every note (when it has items), then the groups picked in this version, by name, each item ticked or not ticked (D47). |
| **Load failed** | Any first-load request fails after one silent retry, with nothing on screen | Generic `<h1>` "Version history". Status line: "Version history did not load: no connection. Try again." (offline or timeout) or "…: something went wrong. Try again." (5xx or unexpected). Then **Try again**. No red, no icon, no code. | Generic `<h1>` "Version history". "Version 2 did not load: no connection. Try again." / "…: something went wrong. Try again." Then **Try again**. |
| **Try again running** | After pressing Try again | Error text cleared at once. The button gets `aria-disabled="true"`, extra presses are ignored, and after 400 ms its label becomes "Loading…". | Same. |
| **Load failed again** | Try again failed | "Version history still did not load: no connection. Try again." The changed words get announced again. Focus stays on Try again. | "Version 2 still did not load: …" |
| **Not found** | Bad version number, 404 on the note or version, a Draft, an empty version list | Whole-page **Page not found** (app-shell-nav.md, empty-loading-error.md): `<h1>` "Page not found", "If you typed or pasted the web address, check it is correct.", link **Go to Today**. The URL is kept. | Same. |
| **Not a manager** | Worker, or a manager whose role changed (`/me` refresh) | **Page not found** from `ManagerOnly`, with no request sent. A `403` from the API is shown the same way. | Same. |
| **Signed out** (401) | Session ended | Sign-in in place (session-timeout.md). Afterwards, the same URL loads and the `<h1>` takes focus. | Same. |
| **Background refresh** | Window focus refetches the version list | A newly saved version appears at the top and the sentence updates ("…is version 4."). Nothing blanks, nothing animates, nothing is announced, and focus stays where it is (stable keys). | The currency line updates from the same query. For example, "This is the current version." becomes "This is an earlier version. The current version is version 4." Version content never changes. Nothing is announced. |
| **Background refresh failed** | Refetch fails with data on screen | Nothing changes and nothing is announced. | Same. |
| **Hover** (`hover: hover` only) | Pointer over a row | Row background `--colour-hover`; link underline thickens to 3 px. | Links only: `--colour-action-hover`, 3 px underline. Text, tags and ticks have no hover. |
| **Focus** | Keyboard focus | Row: 3 px `--colour-focus` outline on the link's `::after`, inset 3 px, around the whole row. Back link: 3 px outline, 2 px offset. `<h1>`: outline only under `:focus-visible`. | Back link, "version 3" link and Try again: 3 px outline, 2 px offset. |
| **Active / pressed** | Press on a row | Row `--colour-pressed`, applied instantly. | Normal link pressed colour. |
| **Visited** | — | Same as default. | Same as default. |
| **Disabled** | — | Never. Every version opens. Try again uses `aria-disabled`, never `disabled`. | No controls to disable. |
| **Read-only** | Always | Rows are links, not controls. | Values are plain text, never disabled or read-only form controls. |
| **Forced colours** | Windows contrast themes | Dividers and the focus ring stay (ring `Highlight`). Hover and pressed tints go, which is harmless. | Tags keep their border (status-tags.md). Tick icons follow the text colour. Rules are borders. |
| **200% text, 320 px** | — | One column, everything wraps, no horizontal scroll. Each date and time stays together, but a line may break between them. | Same. Long names, reasons and unbroken strings wrap (`overflow-wrap: break-word`). |

---

## Interactions and focus

**Arriving (push navigation)**
1. From the read view's **Version history** link, the page scrolls to the top. While data loads there is no `<h1>`, so the shell's "focus pending" flag waits. When the `<h1>` mounts (real or generic), it takes focus through a callback ref, unless the person has already moved focus (for example by tabbing to the back link). That happens once per location and never on re-render. [Research] Gatsby routing tests, n = 5, https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/; [Convention] empty-loading-error.md.
2. Screen readers announce "Version history Jane Citizen Thursday 1 October 2026, heading level 1" (A) or "Version 2 Jane Citizen Thursday 1 October 2026, heading level 1" (B). The name and day are heard straight away, which acts as an identity check (3.9). [Convention] SAFER 1.3, via note-identity-header.md.

**Opening a version (A → B)**
- Tap, click or Enter on a row pushes `…/versions/{n}`. Each row link carries `data-return-key={versionNumber}`, and the shell stores the scroll position and the row key in its in-memory map keyed by `location.key` (app-shell.md route-change rule; the route declares `handle.returnFocus`). Nothing is written to device storage or router state.
- B arrives like any push: top of page, focus on the `<h1>` when it mounts.

**Going back from B to A**
- **Back link "Version history"** or the **browser Back button**: if the previous entry is A, this is a pop. A renders from the memory cache, the shell restores `scrollY`, then focuses the link of the row just opened with `focus({ preventScroll: true })` (`handle.returnFocus`, app-shell.md). [Research] Baymard: over 90% of benchmarked sites lost the user's place (2016), https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/
- If B was opened directly (reload, pasted URL, sign-in in place), the back link pushes A: top of page, focus on the `<h1>`.
- If the remembered row is gone or the cache has expired, A shows from the top and focuses the `<h1>`.

**Going back from A to the read view**
- **Back link "Note"** pops when the read view is the previous entry. Otherwise it pushes the read view's URL. The read view then follows its own rules: on a pop it restores scroll and focuses its `<h1>` with `preventScroll`.

**Moving between versions on B**
- The "version 3" link in the currency line pushes `…/versions/3`: top of page, focus on the `<h1>` "Version 3 Jane Citizen Thursday 1 October 2026". Back pops to version 2: its scroll position is restored from the in-memory map and its `<h1>` is focused with `preventScroll: true`.

**Try again**
- It refetches only the failed queries. The status text clears at once. The button is `aria-disabled="true"` and ignores repeat presses, and its label is "Loading…" after 400 ms.
- **Success:** the content replaces the message and the button unmounts, so focus moves to the real `<h1>` (it never falls to `<body>`).
- **Failure:** the "still did not load" text is written into the status line, and focus stays on Try again.

**Copying old wording (the design's way to "restore")**
- On B, the person selects the Guided notes text (or a reason) and copies it, taps **Version history** then **Note**, taps **Edit**, and pastes. The text is never under a link or overlay. The page sets no `user-select: none` and does not block the context menu. [Opinion] Roselli and Pickering on block links, https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html

**Live regions and announcements**
- Each page has two polite live regions, both present from the first render: the load status line `<p role="status">` in `<main>` ("Loading …" after 1 s and the load-failed text) and the shared page status region (`PageStatus`, app-shell.md component 3), which carries only Try again's "Loading…" busy label. [Standard] SC 4.1.3, https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- No announcement on success (the focused `<h1>` speaks), on background refresh, on a new version appearing, or on the currency line changing.
- No `aria-live` on the list, no `role="alert"`, and no toast.

**Focus order**
- A: skip link → header and nav → back link "Note" → (`<main>`) row "Version 3" → row "Version 2" → row "Version 1". When loading fails: back link → Try again.
- B: skip link → header and nav → back link "Version history" → "version 3" link (earlier versions only) → nothing else. The content has no tab stops. Arrow keys and screen-reader reading keys move through it.
- No positive `tabindex`, no roving tabindex, no arrow-key handlers: this is a list of links, not a composite widget.

---

## Accessibility checklist

**Headings**
- [ ] A: exactly one `<h1>` ("Version history" caption + name) and no `<h2>`. One list follows the facts, so a "Versions" heading would only repeat the `<h1>`.
- [ ] B: one `<h1>` ("Version n" caption + name), then `<h2>` "1. Goals", "2. Common items", "3. Guided notes", with `<h3>` "Every note" and each picked group's name under "2. Common items". 71.6% of screen-reader users move through long pages by headings. [Research] WebAIM survey #10, n = 1,539, https://webaim.org/projects/screenreadersurvey10/
- [ ] `<h1>` has `tabIndex={-1}`, `width: fit-content`, and an outline only on `:focus-visible`.

**Landmarks**
- [ ] `<header>` (banner), `<nav aria-label="Main">`, `<main id="main-content">`. The back link is in the shell's before-main bar slot (app-shell.md 3a), before `<main>`, and is skipped by "Skip to main content". [Convention] GOV.UK back link, https://design-system.service.gov.uk/components/back-link/
- [ ] No `<section aria-label>` regions, no `<article>`.

**Names and labels**
- [ ] Row link name is exactly the visible "Version n". The meta line is the description, through `aria-describedby`. No `aria-label` anywhere on this screen. [Standard] SC 2.5.3, https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html; [Convention] GOV.UK task list, https://design-system.service.gov.uk/components/task-list/
- [ ] Back links' names are their visible text ("Note", "Version history"). The chevron is `aria-hidden`.
- [ ] Middle-dot separators are `aria-hidden="true"`, each followed by a visually hidden ", ".
- [ ] Tags are plain `<span>` text with no role and no focus. The state is always in words (A32, SC 1.4.1).
- [ ] Ticks read "{item}, ticked" / "{item}, not ticked". Never use disabled or `aria-readonly` checkboxes. [Research] Roselli AT testing, https://adrianroselli.com/2024/11/avoid-read-only-controls.html
- [ ] `<html lang="en-AU">`. Participant names have `translate="no"`.

**Semantics**
- [ ] `<ol reversed role="list">`: ordered, descending, with `role="list"` restoring semantics in Safari after `list-style: none`. [Standard] https://html.spec.whatwg.org/multipage/grouping-content.html#the-ol-element; [Convention] https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- [ ] Every date and time is in `<time dateTime>`.
- [ ] Guided notes is escaped text (never `dangerouslySetInnerHTML`).

**Keyboard**
- [ ] Every row, link and button can be reached with Tab and opened with Enter, in visual order.
- [ ] Focus is never lost: after Try again succeeds it goes to the `<h1>`, and after a pop to A it goes to the opened row.
- [ ] Nothing is sticky, so focus is never hidden (SC 2.4.11).

**Screen reader (expected; confirm in testing)**
- [ ] A row: "Version 3, link", then "Sam Lee (manager), Thu 1 Oct 2026, 5:03 pm". Version 1: "Version 1, link … Submitted, Priya Nair, Thu 1 Oct 2026, 4:12 pm".
- [ ] B: "Version 2 Jane Citizen, heading level 1" → "Thursday 1 October 2026" → "This is an earlier version. The current version is, version 3, link" → "Saved by Priya Nair, Thu 1 Oct 2026, 4:40 pm" → "Flagged, Reason: …" → "1. Goals, heading level 2, list, 2 items, Makes own breakfast, ticked …".
- [ ] How "Thu" and "Oct" are spoken, and whether the hidden commas pause, is **unverified**. Test with NVDA + Chrome, VoiceOver + iOS Safari and TalkBack + Chrome.

**WCAG 2.2 AA criteria this screen must meet**
1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.3.3 Sensory Characteristics (the current version is named in words, never "the top one") · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) (text ≥ 7:1 by the foundations tokens; secondary 9.0:1) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast (focus ring, tick icons) · 1.4.12 Text Spacing · 1.4.13 Content on Hover or Focus (none) · 2.1.1 Keyboard · 2.1.2 No Keyboard Trap · 2.4.1 Bypass Blocks · 2.4.2 Page Titled · 2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (rows 56 px, back link and button ≥ 44 px) · 3.1.1 Language of Page · 3.2.3 Consistent Navigation · 3.2.4 Consistent Identification (same back link, heading, tag and row patterns as the read view and Past notes) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages. Also met at no cost (AAA, not claimed): 1.4.6, 2.4.13 and 2.5.5 on the back link, rows and buttons.

---

## Acceptance criteria

**Access and privacy**
- [ ] A worker opening either URL sees **Page not found** and the network log shows no `/versions` request.
- [ ] A manager whose role becomes Worker mid-session sees Page not found on the next screen change. A `403` from the API also shows Page not found.
- [ ] `document.title` is "Grow2Notes – Version history" on A, B, loading and failure. The URL and `history.state` contain no names or note text.
- [ ] After using both pages, `localStorage`, `sessionStorage` and IndexedDB hold nothing written by the app (Playwright check). React Router `<ScrollRestoration>` is not mounted.
- [ ] No string on this screen contains the parent company's name (the app-wide D42 check in microcopy.md's `copy.test.ts`).

**Page A**
- [ ] The `<h1>` text is "Version history Jane Citizen Thursday 1 October 2026" (U+00A0 between "1" and "October"; the date is the heading's third line).
- [ ] The current-version sentence shows the highest `versionNumber` from `GET {base}/versions`.
- [ ] Rows are in descending `versionNumber` order even when the API returns them ascending or shuffled.
- [ ] Each row link's text is exactly "Version {n}". Its `aria-describedby` points to the meta line, and the meta line text (with separators hidden) matches the copy table.
- [ ] "Submitted" appears only in version 1's meta line. "(manager)" appears exactly when `createdBy.id` differs from the note's `author.id`. A fixture where the author is a manager shows no suffix on the author's own versions.
- [ ] Times are "5:03 pm" (U+00A0 before "pm"); 02:00Z on 1 October 2026 shows "midday"; a September date shows "Sep"; there is no comma after the weekday.
- [ ] With one version: "Not edited since submit" is shown (no full stop) and one row is listed.
- [ ] With `isPastDayNote`: the past-day line reads "Past-day note, written by {author} (manager) on {dateTime}" to a screen reader. Without it, there is no line.
- [ ] No row shows a Flagged, Edited, Current or Latest tag or a chevron, and no row contains a second link or button.
- [ ] Each row's box is at least 56 px tall at default text size. Clicking anywhere in the row (including the meta line) opens that version.

**Page B**
- [ ] The `<h1>` is "Version {n} Jane Citizen Thursday 1 October 2026" and does not change after it receives focus.
- [ ] The saved-by line is 16 px in the secondary text colour, as the read view's byline.
- [ ] n = current → "This is the current version." with no link. n < current → "This is an earlier version. The current version is version {current}." with "version {current}" linking to `…/versions/{current}`.
- [ ] Version 1 shows "Submitted by …". Others show "Saved by …", with the same name, suffix and time as their row on A.
- [ ] The Flagged line appears only when that version's `isFlagged` is true, with its own `flagReason`. Edited, To review and Reviewed never appear.
- [ ] Goals list every snapshot goal in API order, with the visually hidden ", ticked" / ", not ticked". Empty lists show "No goals set" / "No common items set".
- [ ] Common items show Every note (when it has items) and only the groups picked in **that** version, each under an `<h3>` with its name, in configured order, every item ticked or not ticked. With a fixture where version 1 picked Community outing and version 2 picked Personal care instead, version 1's page shows only Community outing and version 2's only Personal care. No "Which of these happened?" question or copied-picks line appears.
- [ ] The Guided notes `textContent` equals the stored narrative exactly, including leading and trailing spaces, blank lines and a 20,000-character fixture. Markup characters (`<b>`) show as text.
- [ ] `document.elementFromPoint` over the Guided notes text never returns an `<a>`, and the text can be selected and copied.
- [ ] There is no Edit, Restore, Mark reviewed, Previous, Next or Past notes control.
- [ ] Opening the same version twice in a session sends one `GET …/versions/{n}`. After Save changes on the note, revisiting A refetches the list but not the cached versions.

**Not found and errors**
- [ ] `…/versions/0`, `/-1`, `/1.5`, `/02`, `/abc`, a number above the current version (API 404), a Draft note and a discarded-note URL all show Page not found, with the URL unchanged and focus on its `<h1>`.
- [ ] Throttled to 3 s: nothing appears for the first second, then "Loading version history…" (or "Loading version 2…") appears in a `role="status"` element that was in the DOM before the text arrived.
- [ ] Offline: within about 2 s the generic `<h1>` "Version history" and "… did not load: no connection. Try again." appear, with a Try again button. A 500 response gives "…: something went wrong. Try again." after one retry.
- [ ] Try again: `aria-disabled="true"` while running, label "Loading…" after 400 ms, never the `disabled` attribute. On success, focus is on the `<h1>`. On failure, the text starts "… still did not load" and focus stays on the button.
- [ ] A failed background refetch leaves the screen unchanged and announces nothing.

**Navigation and focus**
- [ ] From the read view: arrive at A with focus on the `<h1>`.
- [ ] A → row "Version 2" → B → back link: A is shown at the same scroll position with focus on the "Version 2" row link (not the `<h1>`). The same happens with browser Back.
- [ ] Open B by pasting its URL, then press the back link: A loads at the top with focus on its `<h1>`.
- [ ] On A, "‹ Note" returns to the read view. When the read view was the previous entry, `history.length` does not grow.
- [ ] While A is open, another manager saves version 4. On window refocus, "Version 4" is the top row, the sentence says version 4, the focused row keeps focus, and nothing is announced.

**Layout and display**
- [ ] At 320 px wide with 200% text: no horizontal scroll on A or B. A 100-character participant name and a 100-character display name wrap with no ellipsis.
- [ ] At 1280 px: one column, content no wider than 40rem, no table, no side panel.
- [ ] Hover tints appear only on devices that can hover. Nothing animates (check transition and animation durations are 0).
- [ ] Windows forced colours: the focus ring is visible on rows and links, tags keep their border, and ticked and not-ticked icons are visible.
- [ ] axe (Playwright) reports no violations on: A default, A one version, A past-day, A loading, A failed, B current, B earlier and flagged, B failed, Page not found.
- [ ] A manual keyboard-only pass and screen-reader passes (NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Chrome) confirm the announcements listed in the checklist.

---

## Conflicts resolved

1. **B's heading.**
   - The options:
     - version-history.md: `<h1>` "Version 2" with the participant in a `<dl>`.
     - note-read-view.md: `<h1>` name + date, then "Version 2 of 3".
     - note-identity-header.md: a caption inside the `<h1>`, then the name.
   - **Chosen:** caption "Version 2" + name + note date, all inside the `<h1>` (the app-wide identity-heading rule recorded in note-identity-header.md in the editorial pass, the same on the read view and the form), then version-history.md's plain-English currency sentence instead of "Version 2 of 3".
   - Why: the name stays large, top left and in the same place as on the read view, form and Past notes. [Convention] SAFER 1.3 "clearly displayed on all portions", https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf; GOV.UK caption-in-heading, https://design-system.service.gov.uk/styles/headings/
   - Earlier versions are exactly where wrong-participant content lives (3.9), and "This is an earlier version…" says more than "2 of 3". [Convention] Wikipedia revision notice, https://en.wikipedia.org/wiki/MediaWiki:Revision-info
2. **A's facts block.** version-history.md uses a `<dl>` (Participant / Note date / Current version). **Chosen:** the same identity heading and date line as everywhere else, plus one sentence, "The current version is version 3." Why: it covers all three facts in 4.5 item 1, keeps one identity pattern app-wide (SC 3.2.4), and shares wording with B's currency line. [Opinion]
3. **Back link wording and place.**
   - version-history.md: "Back to note" / "Back to version history" at the top of `<main>`, always a push.
   - note-identity-header.md: the destination name with a chevron, before `<main>`, popping when the previous entry is the destination.
   - **Chosen:** "Note" / "Version history", following the app's pattern ("Today", "Past notes", "Flagged"), in the shared BackLink in the shell's before-main bar slot (app-shell.md 3a). [Convention] GOV.UK "before the `<main>` element", https://design-system.service.gov.uk/components/back-link/; SC 3.2.4.
4. **Focus on return to A.**
   - version-history.md: focus the opened row after the back link *or* browser Back.
   - chronological-list.md: only on a pop.
   - **Resolved by item 3:** the back link pops when A is the previous entry, so both routes are pops and the row gets focus. A push (direct link) focuses the `<h1>`.
5. **Scroll restoration.**
   - version-history.md's route note and app-shell-nav.md rely on React Router `<ScrollRestoration>`, which writes to `sessionStorage`.
   - **Chosen:** chronological-list.md's in-memory place map. Why: it removes any question about D22 and 9.6. [Convention] https://reactrouter.com/api/components/ScrollRestoration
6. **Row anatomy.**
   - version-history.md (via participant-list-rows.md): chevron and 56 px rows.
   - chronological-list.md: no chevron and 44 px rows.
   - foundations.md: history and flagged rows at 56 px.
   - **Chosen:** the shared ChronoList row (underlined bold link, no chevron) at **56 px**, so this list looks like Past notes and Flagged. 56 px is the foundations size for most-tapped rows (about 9 mm, near Parhi's 9.2 mm). [Research/Convention] foundations.md
7. **Sticky top bar.**
   - note-identity-header.md: the bar is sticky on read and list screens.
   - foundations.md and note-read-view.md: nothing sticky except the note form's bar.
   - **Chosen:** static. Why: SC 2.4.11 risk and lost reading height on phones. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
8. **Font weight.** version-history.md and chronological-list.md use 600. **Chosen:** 700 (foundations.md: 600 renders as Semibold on iPhone but Bold on Android). [Standard] CSS font matching.
9. **Date-time format.**
   - "1 Oct 2026, 5:03 pm": design.md 4.5, version-history.md, status-tags.md, foundations.md.
   - Long "Friday 2 October 2026, 9:01 am": note-read-view.md.
   - **Chosen:** microcopy.md's `dateTime` token, "Thu 1 Oct 2026, 5:03 pm" **(N)**, on every row and on B's saved-by line. Why: microcopy.md is the app's single format owner and lists version rows under `dateTime`.
   - This also settles version-history.md's open "Sep or Sept" question: the Style Manual allows "Sep", as quoted in microcopy.md. [Convention] https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
10. **Past-day wording order.**
    - status-tags.md, version-history.md and note-read-view.md follow 3.8: "written on … by …".
    - microcopy.md sets one wording for screen and files: "written by Jo Smith (manager) on …".
    - **Chosen:** microcopy.md **(N)**. The words are unchanged; only the order follows §11.3.
11. **Flagged line wording.**
    - status-tags.md and version-history.md: `[Flagged] "reason"`.
    - note-read-view.md: `[Flagged] Reason: …`.
    - **Chosen:** "Reason: …". Why: it reuses the form's own label (4.3), matches the read view the manager just left, and avoids quote marks around text that can hold line breaks. [Opinion]
12. **Order of B's metadata lines.**
    - status-tags.md tag order puts Flagged first and Past-day last.
    - note-read-view.md puts the byline / past-day line first.
    - **Chosen:** note-read-view.md's order (saved-by, past-day, Flagged), so B reads like the read view. [Opinion]
13. **Note-level tags on B.**
    - note-read-view.md (variant c): no note-level tags.
    - version-history.md: shows the past-day line.
    - **Chosen:** show the past-day line only (3.8 / A11 "everywhere it appears"). Edited, To review and Reviewed stay on the read view.
14. **A 403 or a worker on the route.**
    - version-history.md: "Only managers can see version history." with Go to Today.
    - app-shell-nav.md and empty-loading-error.md: Page not found.
    - **Chosen:** Page not found. Why: it is the single app-wide role-guard rule, and workers have no link here. [Convention] GOV.UK page-not-found pattern, https://design-system.service.gov.uk/patterns/page-not-found-pages/
15. **Load-failed copy.** Four versions existed:
    - version-history.md: "Couldn't load…"
    - chronological-list.md: "This list did not load. Check your connection, then select Try again."
    - empty-loading-error.md: "Could not load …" + cause sentence, "Still could not load…"
    - microcopy.md: "[Thing] did not load: [cause]. Try again."
    - **Chosen:** microcopy.md's pattern, which the brief names as the wording authority. To it I added "still" on a repeat failure, taken from empty-loading-error.md (NHS App), so the changed text is announced again. [Convention] https://design-system.nhsapp.service.nhs.uk/patterns/error-page/
16. **"Not edited since submit" punctuation.** version-history.md adds a full stop. **Chosen:** word for word with no full stop. Why: microcopy.md says empty states use the design.md string verbatim, and chronological-list.md and status-tags.md agree.
17. **Empty status container.**
    - version-history.md: `display: none` when empty.
    - empty-loading-error.md and chronological-list.md: never `display: none`.
    - **Chosen:** `margin: 0`, always rendered. Why: a live region toggled from `display: none` is the least reliable. [Standard] SC 4.1.3; [Opinion] empty-loading-error.md.
18. **Guided notes markup.** version-history.md splits blank-line paragraphs into `<p>`; note-read-view.md uses one `pre-wrap` block, unchanged. **Chosen:** one block. Why: it keeps the text byte-for-byte as stored, and the report keeps line breaks the same way (11.3). [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/white-space
19. **Metadata size and colour.**
    - foundations.md: status lines are 16 px secondary grey.
    - note-read-view.md: record metadata is body size and text colour.
    - **Chosen:** A's row meta lines use 16 px secondary (a list status line, like Today). B's saved-by, past-day and Flagged lines use body size and text colour (they are part of the record, as on the read view).
20. **Body text size.** note-read-view.md suggests 19 px; foundations.md sets 18 px app-wide. **Chosen:** 18 px. Why: one app-wide size; 18 px keeps text above the critical print size out to about 44 cm. [Research] Legge & Bigelow 2011, via foundations.md.
21. **Retries.** chronological-list.md keeps TanStack's default 3 retries; empty-loading-error.md uses one silent retry and no 4xx retries. **Chosen:** empty-loading-error.md. Why: the failure shows sooner, and a 404 never "loads" for seconds.
22. **Not-found detection.** version-history.md decides "n above current" from the cached list. **Chosen:** decide only from the server's 404 (plus the URL format check), and refetch the list when it is behind. Why: another manager's fresh save must never show as Page not found. [Opinion]
23. **Query keys.** note-read-view.md uses `['note', p, d]`; version-history.md uses `['notes', p, d, …]`. **Chosen:** the `['notes', p, d]` prefix for the note, the list and versions, so one invalidation after Save changes refreshes all of them. [Convention] TanStack prefix invalidation.
24. **Inline "version 3" link size.**
    - version-history.md leaves it as plain inline text.
    - foundations.md asks to keep links out of sentences where possible.
    - **Chosen:** keep the Wikipedia/Confluence-style inline link and give it a 44 px tap area with padding and negative margin, so A32 holds and line spacing does not change. [Standard] SC 2.5.8 inline exception, https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html; [Opinion] the technique.
