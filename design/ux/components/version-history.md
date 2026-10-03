# Version history (managers)

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The `<h1>` holds caption, name and note date (note-identity-header.md); the query keys use the `['notes', p, d]` prefix.

Component key: `version-history`. This covers design.md **4.5 Version history**, which has two views:

- **A. The version list.** It shows the participant, the note date and the current version number, then every version, newest first. Version 1 is labelled "Submitted". If there is only one version, the list also says "Not edited since submit".
- **B. One version.** Tapping a version shows it in full and read-only. There is **no restore**: to bring old wording back, a manager edits the note (4.5).

Rules it follows: D15, D19, D47, A13 (managers only, the restricted record under HPP 6.7), 3.5, 3.8 ("Nobody can … edit an old version"), 5.8 (versions are insert-only) and 6.3 (`GET {base}/versions`, `GET {base}/versions/{versionNumber}`). It adds no screen, setting, notification, status or data. It describes how to build what 4.5 already specifies.

Other components own these pieces, and this file only uses them:

- the **"Version history" link** in the read view (4.4). The read view owns it.
- **row links** (stretched link, chevron, detail line): `participant-list-rows.md`
- the **Submitted** status word, **Flagged** and **Past-day note** tags, and the `·` separators: `status-tags.md`
- the **read-only goal and common-item lists** (`TickListRead`): `checkbox-list.md`
- **page titles**, focus on route change, the header, and the "Page not found" screen: `app-shell-nav.md`

---

## Where it's used

One screen, 4.5. It is reached only from the read view's "Version history" link (4.4), which only managers see (A13).

| View | What it shows | What differs |
|---|---|---|
| **A. Version list** (4.5 items 1–2) | Participant, note date, current version number. Then one row per version, newest first: *Version 3 · Sam Lee (manager) · 1 Oct 2026, 5:03 pm*. Version 1's row starts with **Submitted**. | This is a list of links. It is not a timeline with the content shown inline. Each row is brief: which version, who saved it, when. |
| **A, only one version** (4.5 States) | The same header, the sentence "Not edited since submit", then the single version 1 row. | It is the only empty-like state, because a submitted note always has version 1 (5.3 check constraint). |
| **B. One version** (4.5 item 3) | The whole version: who saved it and when, the flag and its reason if that version was flagged, every goal; for common items, Every note and the groups picked in this version (D47), each item ticked or not ticked; and the Guided notes text. A sentence says whether this is the current version. | Read-only and complete. There is no Edit, no Restore and no Mark reviewed here. Those actions live on the read view. |

Both views are **managers only**. Workers never see the link. A worker who types the URL gets `403` from the API (6.3).

---

## Best practice

### Presenting history plainly

- **Newest first is the usual order for history.** MOJ: "Show the most recent events first, unless user research suggests a different order is better." Wikipedia lists past changes "in reverse-chronological order". **[Convention]** https://design-patterns.service.justice.gov.uk/components/timeline/ · https://en.wikipedia.org/wiki/Help:Page_history
- **Some people expect oldest first, so make the order visible.** DWP's timeline notes say "there is evidence from research that some people initially expect the oldest entry first". DWP made the order more visible in response. **[Research]** (no sample size published) https://design-system.dwp.gov.uk/components/timeline/design-notes. In Grow2Notes, every row starts with its version number, so 3, 2, 1 down the page shows the order without any extra words.
- **Each entry needs three things: what, when, and who.** MOJ requires "a short title", "the date (or date and time)" and "who or what made the update". Wikipedia shows "the time and date that the edit was applied" and the contributor on every line. Design 4.5's "Version 3 · Sam Lee (manager) · 1 Oct 2026, 5:03 pm" already has all three. **[Convention]** (links above)
- **A history whose rows open full records is a list of links, not a timeline.** MOJ says not to use its timeline "as navigation, or to display large amounts of information". DWP says a timeline "should not be used to give in-depth information". Keep rows brief, and show each full version on its own page. **[Convention]** https://design-patterns.service.justice.gov.uk/components/timeline/ · https://design-system.dwp.gov.uk/components/timeline
- **Repeated link text must still be told apart.** DWP: "If you repeat link text for more than one event, make sure that screen reader users can tell links apart". "Version 3", "Version 2" and "Version 1" differ by their number, so each link name is unique. **[Convention]** https://design-system.dwp.gov.uk/components/timeline/accessibility
- **An audit record needs exact times, not relative ones.** Cloudscape: use absolute timestamps when "users need a specific date and time for when an event occurred". "2 hours ago" also goes stale on a page left open. **[Convention]** https://cloudscape.design/patterns/general/timestamps/ · **[Opinion]** on staleness
- **Use a 12-hour clock with no leading zero.** DWP: "Times in 24-hour format or with leading zeroes can be more difficult to read for users with dyslexia or dyscalculia." The Australian Government Style Manual writes "am" and "pm" in lower case, after a non-breaking space ("6:30 pm"). **[Convention]** https://design-system.dwp.gov.uk/components/timeline/accessibility · https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- **Never leave an area blank. Say what the state is.** NN/g: "Do not default to totally empty states", because users may wonder "if the system is still loading information or if errors have occurred". A brief system message is "a simple yet effective way to increase the visibility of system status". **[Research]** (NN/g guidance, not a controlled study; Kaplan, 2021) https://www.nngroup.com/articles/empty-state-interface-design/
- **Use a description list for the key facts.** GOV.UK: "Use a summary list to show information as a list of key facts" or to "display metadata". It is built on `<dl>`, `<dt>` and `<dd>`. **[Convention]** https://design-system.service.gov.uk/components/summary-list/

### Making clear which version is current

- **When an old version is open, say so at the top and point to the current one.** Wikipedia's notice reads: "This is an old revision of this page, as edited by [user] at [time] … which may differ significantly from the current revision." Confluence puts a header at the top of an older version with a "current version" link. **[Convention]** https://en.wikipedia.org/wiki/MediaWiki:Revision-info · https://confluence.atlassian.com/doc/page-history-and-page-comparison-views-139379.html
- **Don't show it by position or colour alone.** "The top one" or a green dot fails people who can't see the layout or the colour. **[Standard]** SC 1.3.3 https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html · SC 1.4.1 https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- **Put it where people will see it.** GOV.UK: "Some users do not notice inset text if it's used on complex pages or near to other visually prominent elements." So the "earlier version" statement goes directly under the `<h1>`, in bold body text. It is not a pale aside. **[Research]** (GOV.UK, qualitative) https://design-system.service.gov.uk/components/inset-text/
- **Keep the "current" cue visible, not ARIA-only.** `aria-current` marks "the current item within a container or set of related elements", such as a page in a breadcrumb. On a version row it would give screen-reader users a cue that sighted users don't get. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current · **[Opinion]** on applying it here

### The read-only viewer

- **A full record belongs on its own page, not in a dialog.** Carbon: "For complex flows with complex choices, consider using a full page instead of a modal". It also says: "If the large modal height is still not enough space then a full page might be needed instead." NN/g: "if it requires multiple steps to begin with, it probably justifies dedicating a full page to it." A note can hold 20,000 characters (A6). **[Convention]** https://carbondesignsystem.com/components/modal/usage/ · https://www.nngroup.com/articles/modal-nonmodal-dialog/
- **Show stored values as text, not as disabled or read-only form controls.** HTML's `readonly` "does not apply to" checkboxes. Disabled controls are exempt from contrast rules. Roselli advises showing values "as plain text instead of form fields". **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly · **[Research]** (practitioner AT testing) https://adrianroselli.com/2024/11/avoid-read-only-controls.html
- **Keep the old text selectable.** Pickering and Roselli both note that a stretched-link overlay makes the text under it hard to select. **[Opinion]** (expert practitioners) https://inclusive-components.design/cards/ · https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html. Design 4.5 says old wording comes back by editing the note. In practice that means copying text out of an old version, so that text must never sit under a link.
- **The back link goes at the top.** GOV.UK: "Always place back links at the top of a page". Use "Go back to [page]" where plain "Back" might be unclear. **[Convention]** https://design-system.service.gov.uk/components/back-link/
- **Keep lines readable.** WCAG 1.4.8 (AAA, advisory here) caps text blocks at 80 characters wide. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/visual-presentation.html

### Markup and single-page-app behaviour

- **A version list is an ordered list.** WHATWG: `ol` "represents a list of items, where the items have been intentionally ordered". `reversed` "indicates that the list is a descending list (..., 3, 2, 1)", which is exactly what the version numbers are. **[Standard]** https://html.spec.whatwg.org/multipage/grouping-content.html#the-ol-element
- **Safari drops list semantics under `list-style: none`** unless the list has `role="list"`. **[Convention]** https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- **Opening a version changes the URL, so each row is an `<a href>`.** Its purpose can come from the text in the same list item. **[Standard]** SC 2.4.4 https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html
- **Mark up dates and times with `<time datetime>`.** It is "for presenting dates and times in a machine-readable format". **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/time
- **On a screen change, focus the heading.** In Gatsby and Fable's test with 5 assistive-technology users, focusing the heading "was found to be the best experience, as it would save time and make it clear what happened". `app-shell-nav.md` already does this app-wide. **[Research]** (n = 5) https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- **TanStack Query treats cached data as stale by default** and refetches it "when the window is refocused". Inactive queries are garbage-collected after 5 minutes. `staleTime: 'static'` will "never trigger a refetch". Versions can never change (5.8), so they can be static. **[Convention]** (library documentation) https://tanstack.com/query/v5/docs/framework/react/guides/important-defaults

---

## Recommendation for Grow2Notes

### In one line

**Two plain pages. A brief list of links, newest first, under a short facts block that names the current version. Then a full read-only page for each version, which says in its first line whether it is the current version.** No dialog, no inline expanding, no diff, no restore, no edit.

### Routes and data

URLs hold only IDs, dates and version numbers (4.0):

| View | Route (illustrative) | Data |
|---|---|---|
| A. Version list | `/participants/{participantId}/notes/{noteDate}/versions` | `GET /api/participants/{participantId}` (name) · `GET {base}` (author, `isPastDayNote`, written-on time; usually cached from the read view) · `GET {base}/versions` |
| B. One version | `/participants/{participantId}/notes/{noteDate}/versions/{n}` | The same three, plus `GET {base}/versions/{n}` |

The **current version number** is the highest `versionNumber` in `GET {base}/versions`. Both views read it from that one query, so they always agree. Page title for both: **"Grow2Notes – Version history"**, from the fixed page-name list in `app-shell-nav.md`. No name is ever put in the title, the URL or router state (4.0).

### Anatomy: A. Version list

```
Phone, 360 px, default text size
+-----------------------------------+
| (app header and navigation)       |
+-----------------------------------+
| < Back to note                    |  back link, top of <main>
|                                   |
| Version history                   |  <h1> (focused on arrival)
|                                   |
| Participant                       |  <dl> key facts (stacked on phone)
| Jane Citizen                      |
| Note date                         |
| Thursday 1 October 2026           |
| Current version                   |
| Version 3                         |
|                                   |
| [Past-day note] written on Sat 3 Oct 2026, 10:14 am by Jo Smith
|                                   |  ^ only when isPastDayNote (A11)
+-----------------------------------+
| Version 3                       > |  row link text = "Version 3"
| Sam Lee (manager) · 1 Oct 2026,   |  detail line (aria-describedby)
| 5:03 pm                           |
+-----------------------------------+
| Version 2                       > |
| Priya Nair · 1 Oct 2026, 4:40 pm  |
+-----------------------------------+
| Version 1                       > |
| Submitted · Priya Nair ·          |  "Submitted" only on version 1
| 1 Oct 2026, 4:12 pm               |
+-----------------------------------+
```

1. **Back link** "Back to note", which goes to the read view of this note (4.4). It is a real `href` to the parent route, not `history.back()`.
2. **`<h1>` "Version history".** Its text is fixed, so it renders straight away and can take focus before the data arrives.
3. **Key facts**, a `<dl>` holding exactly the three facts 4.5 lists:
   - **Participant:** the full name.
   - **Note date:** "Thursday 1 October 2026".
   - **Current version:** "Version 3".
4. **Past-day line**, only when `isPastDayNote`. It follows A11 ("everywhere it appears") and reuses the read view's tag + detail line from `status-tags.md`.
5. **Only one version:** the sentence "Not edited since submit." sits here, above the list.
6. **The list**: `<ol reversed role="list">`, one row per version, newest first. Each row is built like `participant-list-rows.md`:
   - **Link text:** "Version 3". It is the only link text.
   - **Detail line:** who and when. Version 1 begins with the plain status word **Submitted** (`status-tags.md`). Other rows have no status word and no tag. There is no "Current", no "Latest" and no Flagged tag on rows (`status-tags.md` points 1–2).
   - **Chevron**, decorative.

**"(manager)" after a name.** Show it when the person who saved that version is **not the note's author**. Only the author or a manager can save a version (3.5), so this is always true at the time of saving, and it never changes if someone's role changes later. The note's author never gets the suffix, even if they are a manager. (See Points the design leaves open, 1.)

### Anatomy: B. One version

```
Phone, 360 px
+-----------------------------------+
| < Back to version history         |
|                                   |
| Version 2                         |  <h1> (from the URL, renders at once)
|                                   |
| This is an earlier version.       |  bold, directly under the h1
| The current version is version 3. |  "version 3" is a link
|                                   |
| Participant   Jane Citizen        |  <dl> (two columns from ~30rem)
| Note date     Thursday 1 October 2026
| Saved by      Priya Nair,         |  version 1: "Submitted by"
|               1 Oct 2026, 4:40 pm |
| [Past-day note] written on … by … |  only when isPastDayNote
| [Flagged] "Mentioned pain in his left knee after the walk."
|                                   |  ^ only when THIS version is flagged
+-----------------------------------+
| 1. Goals                          |  TickListRead (checkbox-list.md)
| [v] Makes own breakfast           |
| [ ] Catches bus to day program    |
| 2. Common items                   |
| Every note                        |  <h3>, only when it has items
| [v] Medication prompted           |
| Community outing                  |  <h3>: each group picked in THIS version
| [ ] Travelled by bus or train     |  (D47); unpicked groups never appear
| 3. Guided notes                   |
| Jane was keen to go to the        |  plain selectable text, line breaks kept
| library today ...                 |
+-----------------------------------+
```

1. **Back link** "Back to version history".
2. **`<h1>` "Version 2"**, taken from the URL so it is ready at once. It is never changed after render, because focus has already landed on it.
3. **Currency line**, the first thing after the `<h1>`, in bold body text:
   - current version: "This is the current version."
   - earlier version: "This is an earlier version. The current version is version 3." The "version 3" part links to that version's page, following the Wikipedia and Confluence convention.
4. **Facts**, a `<dl>`:
   - **Participant:** the full name.
   - **Note date:** "Thursday 1 October 2026".
   - **Saved by:** "Sam Lee (manager), 1 Oct 2026, 5:03 pm". For version 1 the label is **Submitted by**. Both match the export's strings (11.6): "saved by" and "submitted by".
5. **Past-day line** (A11), and the **Flagged** tag with the reason when *this version* is flagged. Both use the `status-tags.md` read-view pattern. There is no review status here: reviews belong to the note, and the read view shows them.
6. **Content**, through the same note-content renderer the read view uses:
   - `TickListRead` for "1. Goals". Empty: "No goals set".
   - `<h2>` "2. Common items", then from `v.commonItemGroups`, in order: `<h3>` "Every note" and its `TickListRead headingLevel={3}` (only when it has items), then an `<h3>` with the group's name as stored and its `TickListRead headingLevel={3}` for each group picked in this version (D47). The API sends only those groups (design.md 6.3, `GET {base}/versions/{n}`); the renderer never filters them. Groups not picked in this version, the question "Which of these happened?" and the copied-picks line never appear. Empty array: "No common items set". Keyed by index.
   - "3. Guided notes" as plain text, with line breaks kept. Guide prompts never appear (D34).
7. **No action buttons.** There is no Edit, because nobody can edit an old version (3.8), and Edit for the current note is on the read view. There is no Restore (4.5) and no Mark reviewed.

### Behaviour

- **Tap or click a row** to go to view B for that version. The whole row is the target, through the stretched link. One row has one action.
- **Going back.** The back link always goes to the parent route, so it works the same after a direct link or after signing in again (4.0 Sessions). The browser Back button works as normal.
- **Returning to the list** (back link or browser Back): focus goes to the link of the version just viewed. This follows the Today rows' rule in `participant-list-rows.md`, and the remembered row is held in memory only (D22).
- **Freshness.** The version list query uses TanStack's defaults, so it refetches when the window regains focus (6.8: no polling). If another manager saves version 4 meanwhile, it simply appears on top and the facts block says "Version 4". A version page that was current then says "This is an earlier version…" when the data refreshes, because it is worked out from the list query, never stored. Nothing is announced; this is a passive change.
- **Single versions never change**, so `GET {base}/versions/{n}` is cached with `staleTime: 'static'`. Opening the same version twice makes no second request until the cache entry is garbage-collected. Everything stays in memory only (7.5, D22).
- **After Save changes elsewhere** (the note form), the form invalidates `['notes', participantId, noteDate]`. The version list refetches, and static single-version entries are untouched, which is correct.
- **No motion.** Route changes are instant. Nothing animates.

### States

| State | A. Version list | B. One version |
|---|---|---|
| **Default** | Back link, `<h1>`, facts, past-day line if any, list. | Back link, `<h1>`, currency line, facts, tags if any, content. |
| **Hover** (`@media (hover: hover)` only) | Row background `--colour-row-hover`, and the link text underlined. Same as Today rows. | Links (back link, "version 3") get the normal link hover. Content has no hover. |
| **Focus** (`:focus-visible`) | 3 px `--colour-focus` outline around the **whole row** on `.link::after`, offset −3 px. At least 3:1 against both row backgrounds. | The normal link focus outline on the back link and the "version 3" link. The `<h1>` focus box hugs its text (`width: fit-content`, `app-shell-nav.md`). |
| **Active** (pressed) | Row background `--colour-row-active`. No ripple or scale. | Normal link active state. |
| **Visited** | Same as default. A visited colour would mean nothing in an audit list. | n/a |
| **Disabled** | **Never.** Every version can be opened. | There are no controls to disable. |
| **Loading** | The back link and `<h1>` render at once. The list area is blank for the first second, then shows "Loading version history…" in a `role="status"` container that is in the DOM from the first render. No skeleton rows. | Back link and `<h1>` at once. After 1 s: "Loading version 2…" in the same kind of container. Render the rest in one pass when every query has succeeded, so nothing shifts. |
| **Load failed** (network or 5xx) | "Couldn't load the version history. Check your connection and try again." and a **Try again** button, which refetches every failed query. The text goes in the status container. The button sits outside it, so it isn't read out twice. | "Couldn't load version 2. Check your connection and try again." and **Try again**. |
| **Not allowed** (`403`: a worker, or a manager whose role changed mid-session) | "Only managers can see version history." and a **Go to Today** link. No data is shown. | Same. |
| **Not found** (`404`, a Draft with no versions, a version number that is not a positive whole number, or one above the current version) | The app's **Page not found** screen (`app-shell-nav.md`). | Same. |
| **Session ended** (`401`) | Global sign-in handling (session-timeout component). Afterwards the user comes back to the same URL. | Same. |
| **Empty: only one version** | "Not edited since submit." above the list, then the single version 1 row. | n/a. Version 1 then says "This is the current version." |
| **Read-only** | The whole page. Rows are links, not controls. | The whole page. Values are text, never disabled form controls. |
| **Background refresh failed** | Nothing changes. The rows already shown stay. | Nothing changes. |

### Phone vs laptop

- **Phone (first, 4.0):**
  - One column with the 16 px page gutter.
  - Rows are full-bleed with at least 56 px height, so the tap target runs edge to edge, above the 44 px of A32.
  - The facts `<dl>` stacks each label above its value.
  - Names and times wrap; nothing is truncated. "1 Oct 2026," and "5:03 pm" are each kept on one line (non-breaking space before "pm"), but a line may break between them, so 200% text at 320 px never scrolls sideways (SC 1.4.10).
- **Laptop:**
  - The same single column. 4.0 gives the extra space only to the setup screens, so there is no list-and-detail split pane.
  - The list is capped at `max-inline-size: 40rem`. The content of view B is capped at about 40rem as well, roughly 70–75 characters of body text, under the 80-character AAA guide.
  - The facts `<dl>` becomes two columns, labels then values, from about 30rem wide.
  - Hover appears only on devices that can hover.
- **Never** turn the list into a table with columns (Version | By | Date) on wide screens. That splits who and when away from the version, and invites sorting and filtering nobody asked for. **[Opinion]**, the same call as `participant-list-rows.md`.

### Copy

| Where | Text | Source |
|---|---|---|
| Page title (both) | Grow2Notes – Version history | 4.0 pattern; `app-shell-nav.md` page names |
| A `<h1>` | Version history | 4.4 link / 4.5 title |
| A back link | Back to note | **proposed** (GOV.UK "Go back to [page]" convention) |
| Fact labels | Participant · Note date · Current version | 4.5 item 1 |
| Fact values | Jane Citizen · Thursday 1 October 2026 · Version 3 | 4.5, date format as the note form (4.3) |
| Row link | Version 3 | 4.5 |
| Row detail (versions 2+) | Sam Lee (manager) · 1 Oct 2026, 5:03 pm | 4.5 |
| Row detail (version 1) | Submitted · Priya Nair · 1 Oct 2026, 4:12 pm | 4.5 ("Version 1 is labelled 'Submitted'") + `status-tags.md` |
| Only one version | Not edited since submit. | 4.5 (full stop added, as on the app's other empty-state sentences) |
| B `<h1>` | Version 2 | 4.5 |
| B back link | Back to version history | **proposed** |
| B, current | This is the current version. | **proposed** (Wikipedia/Confluence convention) |
| B, earlier | This is an earlier version. The current version is version 3. | **proposed**; "version 3" is the link |
| B saved-by label | Saved by · Submitted by (version 1) | 11.6 export strings |
| B saved-by value | Sam Lee (manager), 1 Oct 2026, 5:03 pm | 4.5 / 11.6 |
| Past-day line | [Past-day note] written on Sat 3 Oct 2026, 10:14 am by Jo Smith | 3.8, `status-tags.md` |
| Flag | [Flagged] "Mentioned pain in his left knee after the walk." | `status-tags.md` read-view pattern |
| Section headings | 1. Goals · 2. Common items · 3. Guided notes | 4.3, `checkbox-list.md` |
| Empty sections | No goals set · No common items set | 11.3 |
| Loading | Loading version history… · Loading version 2… | **proposed** (same pattern as `participant-list-rows.md`) |
| Load failed | Couldn't load the version history. Check your connection and try again. · Couldn't load version 2. Check your connection and try again. · Try again | **proposed** (same pattern) |
| Not allowed | Only managers can see version history. · Go to Today | **proposed** |
| Not found | Page not found · Go to Today | `app-shell-nav.md` |

Times are Melbourne time (A33), like "5:03 pm": lower case, no leading zero, with a non-breaking space before am/pm. Short dates follow design.md: "1 Oct 2026". Long dates: "Thursday 1 October 2026", with no comma.

### Accessibility

**Semantics**

- **View A, in DOM order:**
  - the back link `<a href>`
  - `<h1>`
  - `<dl>` (each `<dt>`/`<dd>` pair wrapped in a `<div>`, which HTML allows)
  - the past-day line
  - the "Not edited since submit." `<p>`
  - `<ol reversed role="list">`, where each `<li>` holds **one** `<a href>` (React Router `<Link>`) and a detail `<p id>`.
- The list needs no heading of its own. It follows the facts directly, and the `<h1>` names the page. **[Opinion]**: there is only one list on the page, so an extra `<h2>` "Versions" would only repeat the `<h1>`.
- **Detail line:**
  - The link points to it with `aria-describedby`, as in the GOV.UK task list.
  - Each `·` is `aria-hidden="true"`, followed by a visually hidden comma, so screen readers pause between parts (`status-tags.md`).
  - Times are `<time dateTime="2026-10-01T07:03:00Z">`.
- **View B, in DOM order:**
  - the back link
  - `<h1>`
  - the currency `<p>`
  - the facts `<dl>`
  - the tags lines
  - `<h2>` "1. Goals", "2. Common items" and "3. Guided notes", each with its content; under "2. Common items", an `<h3>` per shown group (Every note, then the groups picked in this version).
- **Guided notes text** is ordinary text. Split it on blank lines into `<p>` elements, and use `white-space: pre-wrap` for single line breaks. It is never placed under a link or overlay, so it can be selected and copied.
- **ARIA used:** only `aria-describedby` (row → detail), `aria-hidden` (separators and chevron) and `role="list"` (the Safari fix). Not used:
  - `aria-current` on rows
  - `aria-live` on the list
  - `listbox`, `grid`, `menu` or `feed`
  - `aria-readonly` or `disabled` controls in view B

**Keyboard**

- **Tab order in view A:** the skip link and header, then the back link, then one stop per version row in visual order. Enter follows the link. Space scrolls the page, which is normal for links. There are no arrow-key handlers and no roving tabindex: this is a list of links, not a composite widget.
- **Tab order in view B:** the back link, then the "version 3" link when the page is an earlier version. The content has no tab stops. Arrow keys, Page Down and screen-reader reading keys move through it.
- The `<h1>` gets `tabindex="-1"` and receives focus on arrival (`app-shell-nav.md`). On Back or Forward it is focused with `preventScroll: true`, unless focus is returned to the row just viewed (view A).
- If any header is ever made sticky, set `scroll-padding-block-start` so a focused row is never hidden under it (SC 2.4.11).

**What screen readers say** (expected; confirm in testing)

- Arriving at A: "Version history, heading level 1". Then, reading on: "Participant, Jane Citizen. Note date, Thursday 1 October 2026. Current version, Version 3." Then "list, 3 items".
- Tabbing to a row: "Version 3, link", then the description "Sam Lee (manager), 1 Oct 2026, 5:03 pm". Version 1: "Version 1, link … Submitted, Priya Nair, 1 Oct 2026, 4:12 pm".
- Arriving at B: "Version 2, heading level 1". The next item is "This is an earlier version. The current version is version 3", with "version 3" as a link.
- Loading, load-failed and not-allowed text is announced through status containers that exist from the first render (SC 4.1.3). A background refresh announces nothing.

**WCAG 2.2 criteria met**

1.3.1 Info and Relationships (ordered list, description list, headings, `aria-describedby`) · 1.3.2 Meaningful Sequence · 1.3.3 Sensory Characteristics (the current version is named in words, never as "the top one") · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast (focus ring, chevron, tick icons) · 1.4.12 Text Spacing · 2.1.1 Keyboard · 2.4.2 Page Titled · 2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name (the visible "Version 3" is the whole link name) · 2.5.8 Target Size (Minimum) (rows ≥ 56 px tall) · 3.2.3 Consistent Navigation · 3.2.4 Consistent Identification (same row and tag patterns as the rest of the app) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

**Test**

- Playwright with `@axe-core/playwright` on: default (3 versions), only one version, loading, load failed, not allowed, an earlier version and the current version.
- By hand: NVDA with Chrome, VoiceOver with Safari on iOS, TalkBack with Chrome; 320 px width at 200% text; forced colours; keyboard only.

### Implementation notes (React 19 + native HTML + CSS Modules)

Everything here is native HTML. React Aria isn't needed: there is no composite widget, popover or dialog.

**Files:** `src/features/notes/versions/`, containing `VersionHistoryPage.tsx`, `VersionPage.tsx`, `VersionRow.tsx`, `versionQueries.ts` and `Versions.module.css`. Reuse these rather than copying them:

- the shared link-row styles from `participant-list-rows.md`
- `MetaSeparator` and `Tag` from `status-tags.md`
- `TickListRead` from `checkbox-list.md`
- the read view's note-content renderer

**Routes** (data router, so `<ScrollRestoration>` works):

```tsx
{
  path: 'participants/:participantId/notes/:noteDate',
  children: [
    { index: true, element: <NoteReadPage /> },
    {
      element: <RequireManager />,          // me.role !== 'Manager' -> the "Only managers…" message, no fetch
      children: [
        { path: 'versions', element: <VersionHistoryPage /> },
        { path: 'versions/:versionNumber', element: <VersionPage /> },
      ],
    },
  ],
}
```

**Queries** (memory only, no persister, D22):

```ts
// versionQueries.ts
import { queryOptions } from '@tanstack/react-query';

const noteKey = (p: string, d: string) => ['notes', p, d] as const;

export const versionsQuery = (p: string, d: string) =>
  queryOptions({
    queryKey: [...noteKey(p, d), 'versions'],
    queryFn: async () => {
      const list = await getVersions(p, d);                 // openapi-fetch wrapper; throws ApiError(status)
      return [...list].sort((a, b) => b.versionNumber - a.versionNumber);  // newest first, by number
    },
  });

export const versionQuery = (p: string, d: string, n: number) =>
  queryOptions({
    queryKey: [...noteKey(p, d), 'versions', n],
    queryFn: () => getVersion(p, d, n),
    staleTime: 'static',   // NoteVersion rows are never updated (5.8). If the pinned v5 lacks 'static', use Infinity.
  });
```

**Version row:**

```tsx
// VersionRow.tsx
export function VersionRow({ v, authorId }: { v: VersionSummary; authorId: string }) {
  const detailId = useId();
  const by = v.createdBy.displayName + (v.createdBy.id !== authorId ? ' (manager)' : '');
  const at = melbourneDateTime(v.createdAtUtc);            // { date: '1 Oct 2026', time: '5:03 pm' }
  return (
    <li className={rows.row}>
      <div className={rows.text}>
        <Link to={String(v.versionNumber)} className={rows.link} aria-describedby={detailId}>
          Version {v.versionNumber}
        </Link>
        <p id={detailId} className={rows.detail}>
          {v.versionNumber === 1 && (<><span className={rows.state}>Submitted</span><MetaSeparator /></>)}
          <span>{by}</span><MetaSeparator />
          <time dateTime={v.createdAtUtc}>
            <span className={rows.nowrap}>{at.date},</span> <span className={rows.nowrap}>{at.time}</span>
          </time>
        </p>
      </div>
      <Chevron className={rows.chevron} />   {/* aria-hidden, focusable=false */}
    </li>
  );
}
```

**Version page, the parts that matter:**

```tsx
// VersionPage.tsx (abridged)
const n = Number(versionNumber);
const valid = Number.isInteger(n) && n >= 1;

// Hooks first, every render (Rules of Hooks); an invalid number fetches nothing
const versions = useQuery({ ...versionsQuery(participantId, noteDate), enabled: valid });
const version  = useQuery({ ...versionQuery(participantId, noteDate, n), enabled: valid });
// participantQuery and noteQuery as on the list page

const current = versions.data?.[0]?.versionNumber;        // newest first
if (!valid || (versions.isSuccess && n > current!) || is404(version.error)) return <NotFound />;

return (
  <>
    <Link to=".." relative="path" className={styles.back}>Back to version history</Link>
    <h1 ref={headingRef} tabIndex={-1} className={styles.h1}>Version {n}</h1>
    <div role="status" className={styles.status}>{statusText /* delayed loading or error, else '' */}</div>
    {ready && (
      <>
        <p className={styles.currency}>
          {n === current
            ? 'This is the current version.'
            : <>This is an earlier version. The current version is <Link to={`../${current}`} relative="path">version {current}</Link>.</>}
        </p>
        <dl className={styles.facts}>
          <div><dt>Participant</dt><dd>{fullName}</dd></div>
          <div><dt>Note date</dt><dd><time dateTime={noteDate}>{longDate(noteDate)}</time></dd></div>
          <div><dt>{n === 1 ? 'Submitted by' : 'Saved by'}</dt><dd>{by}, <time dateTime={v.createdAtUtc}>{at.date}, {at.time}</time></dd></div>
        </dl>
        {/* PastDayLine (A11) and Flagged tag + reason from status-tags.md, when they apply */}
        <NoteContent goals={v.goals} commonItemGroups={v.commonItemGroups} narrative={v.narrative} />
      </>
    )}
    {failed && <button type="button" onClick={retryAll}>Try again</button>}
  </>
);
```

**Dates and times.** A shared helper, built from parts so the output matches design.md exactly:

```ts
// src/lib/time.ts (shared with the other components)
const MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
const fmt = new Intl.DateTimeFormat('en-AU', {
  timeZone: 'Australia/Melbourne', year: 'numeric', month: 'numeric', day: 'numeric',
  hour: 'numeric', minute: '2-digit', hour12: true,
});
export function melbourneDateTime(isoUtc: string) {
  const p = Object.fromEntries(fmt.formatToParts(new Date(isoUtc)).map(x => [x.type, x.value]));
  return {
    date: `${Number(p.day)} ${MONTHS[Number(p.month) - 1]} ${p.year}`,           // "1 Oct 2026"
    time: `${p.hour}:${p.minute} ${String(p.dayPeriod).toLowerCase().replace(/\./g, '')}`, // "5:03 pm"
  };
}
```

Both workarounds were checked in Node 24.12 on 1 October 2026:

- **Day.** With `month: 'numeric'`, en-AU gives the day as "01", so `Number()` is required.
- **Month.** With `month: 'short'`, en-AU and en-GB give "30 **Sept** 2026", but design.md writes "Sep" ("Mon 28 Sep 2026"). The fixed month table avoids that. Browser ICU versions can differ, so unit-test this helper.

The Daylight Saving Time change (4 October 2026) was checked too: 15:30 UTC that day shows as 2:30 am on 5 Oct.

**CSS (additions; the rows reuse the Today row styles):**

```css
/* Versions.module.css */
.back { display: inline-block; padding-block: 0.75rem; min-block-size: 2.75rem; } /* 44 px target */
.h1 { width: fit-content; }
.currency { font-weight: 600; margin-block: 0.5rem 1.25rem; max-inline-size: 40rem; }
.facts { margin: 0 0 1.5rem; max-inline-size: 40rem; }
.facts > div { padding-block: 0.25rem; }
.facts dt { font-weight: 600; }
.facts dd { margin: 0; overflow-wrap: anywhere; }
@media (min-width: 30rem) {
  .facts > div { display: grid; grid-template-columns: 9rem 1fr; column-gap: 1rem; }
}
.content { max-inline-size: 40rem; }
.narrative p { margin-block: 0 0.75rem; white-space: pre-wrap; overflow-wrap: anywhere; }
.status:empty { display: none; }   /* stays in the DOM, so it works as a live region */
```

`display: none` on an empty status container is safe: it is still in the DOM, and the next text written into it is announced. **[Opinion]**: some screen reader and browser pairs may miss text written into a container that was hidden a moment before, which is unverified. If testing shows misses, use a visually hidden class instead of `display: none`.

---

## Per-screen notes

**4.4 Read view → Version history link.** The read view owns this link. The link text "Version history" matches the `<h1>` of view A exactly, so speech users can say it and screen-reader users hear the same words when they arrive. The link stays even when there is only one version: 4.4 doesn't make it conditional, and view A then says "Not edited since submit."

**4.5 View A, the version list**

- Show exactly the three facts 4.5 names: participant, note date and current version. Add the past-day line only because A11 asks for it "everywhere it appears".
- **Current version is stated in the facts block, not tagged on a row.** This agrees with `status-tags.md` ("Don't add 'Current' or 'Latest' tags"). Wikipedia and Confluence do mark the current revision in their history lists. If managers testing it ever hesitate over which row is current, the conventional fallback is plain text "Current version" at the start of the top row's detail line, like "Submitted" on version 1. No tag, no colour. **[Opinion]**
- Don't show a Flagged tag on version rows, even though `GET {base}/versions` returns `isFlagged`. 4.5 doesn't list it, and `status-tags.md` point 2 has the same default. The flag is visible inside each version (view B).
- Rows never show draft autosaves or pending edits. They are not versions (3.5). The manager's own unsaved pending edit is invisible here, as it is everywhere except to its editor.

**4.5 View B, one version**

- The first line under the `<h1>` always says which version this is: the current version or an earlier one. This is the main safeguard against reading a replaced version, for example the content of a note first written on the wrong participant (3.9, D39), as if it were the record.
- It is a full page, never a dialog, bottom sheet or inline expansion. A version can hold up to 20,000 characters of text (A6). A page also gives it a URL, so Back works.
- There is no Edit, Restore, Previous or Next. To bring old wording back, the manager selects and copies text here, goes back to the note and taps Edit (4.5, 3.5). That is why the text must be selectable.
- The content uses the read view's renderer, so a version looks exactly like the note did when it was current. That means the same headings, tick icons and line breaks.

**Daily report and participant export (11.3, 11.6).** View B's "Saved by" and "Submitted by" words are taken from the export's "Version 2: saved by …" and "Version 1: submitted by …". Then a manager reading earlier versions on screen and in an exported record sees the same words.

### Points the design leaves open

1. **How "(manager)" is decided.** `NoteVersion` stores only `CreatedByUserId` (5.3), not the role at the time of saving. This file recommends showing "(manager)" when the saver is not the note's author. That is always true at the time of saving (3.5) and never changes with later role changes. It needs `createdBy` to carry the user's ID, not only a display name; 6.3 doesn't give its shape. Using the user's *current* role would rewrite history whenever a role changed.
2. **The order of `GET {base}/versions`.** 6.3 doesn't specify it. Recommend the server returns newest first. The client sorts by `versionNumber` anyway, which is cheap and has no locale issues.
3. **A Draft's `/versions` URL.** A draft has no versions. Whether the API returns `[]` or `404` isn't stated. Either way, the client shows Page not found.
4. **Proposed copy** for the owner to approve:
   - the back-link texts
   - the two currency sentences
   - loading, failure and not-allowed messages
   - the "Saved by" and "Submitted by" labels, which come from the export wording
5. **"Sep" or "Sept".** design.md writes "Sep". `Intl` en-AU now produces "Sept". This file follows design.md through a fixed month table. Whether the Australian Government Style Manual prefers "Sept" could not be checked, because its pages timed out during this research, so it is **unverified**.

---

## Anti-patterns to avoid

- **A Restore or Revert button**, or "Make this the current version". 4.5 rules it out, and an old version can't be edited (3.8).
- **An Edit button on a version page**, even the current one. It suggests that old versions are editable. Edit lives on the read view.
- **Diff highlighting, side-by-side compare, or "what changed" summaries.** They are not in the design (see Tensions).
- **Showing a version in a `<dialog>`, a bottom sheet or an accordion** inside the list. Long content gets trapped, there is no URL or Back, and phones handle it badly.
- **Showing old ticks as disabled or read-only checkboxes**, or old text in a disabled `<textarea>`. Use plain text and `TickListRead`.
- **Relative times** ("2 hours ago", "Yesterday") in an audit list, or 24-hour or leading-zero times ("17:03", "05:03 pm").
- **Marking the current version by position, colour, bold or an icon alone**, or only through `aria-current`. Say it in words.
- **Putting names or note text into the URL, page title, router `state`, `localStorage` or `sessionStorage`.** Version content is the restricted record (A13, HPP 6.7). It lives in TanStack Query's memory cache only.
- **Listing draft autosaves or pending edits as versions**, or numbering versions on the client. The number comes from the server's `VersionNumber`.
- **Truncating Guided notes text with "Read more"** in view B. "Shows it in full" (4.5).
- **Showing the guide prompts** in a version. They were placeholders and were never saved (D34).
- **A table with sortable columns** on laptop. Also filters, search, pagination or "load more": a note has a handful of versions, and the list is returned whole.
- **Spinners or skeleton rows for sub-second loads.** Show nothing under 1 s, then text.
- **Changing the focused `<h1>` text after load**, for example adding "(earlier version)" to it later. Put the state in the next line instead.
- **Making the whole content area of view B a link**, or wrapping it in a clickable card. The text must stay selectable so it can be copied into an edit.

---

## Tensions with decisions

- **Compare and restore are the convention in version histories; design 4.5 has neither.**
  - Wikipedia's history links each revision to a diff ("(cur) takes you to a diff page, showing the difference between that edit and the current revision").
  - Confluence offers "Compare with Current", "Restore this Version" and "Previous and Next" on an older version.
  - Google Docs offers "Restore this version".
  - All of this is **[Convention]**. This research found no study showing that managers in a small provider need a compare view to do the review 4.5 supports.
  - Confluence's restore creates a *new* version that copies the old one. That is the same append-only result the design gets when a manager edits the note (5.8), with the copying done by hand.
  - **No change recommended.** The full read-only version, with selectable text, covers the design's stated purpose: "show every version of a submitted note, with who saved it and when".
  - Sources: https://en.wikipedia.org/wiki/Help:Page_history · https://confluence.atlassian.com/doc/page-history-and-page-comparison-views-139379.html · https://support.google.com/docs/answer/190843

---

## Sources

**Standards and specifications**

- WHATWG HTML, the `ol` element and `reversed`: https://html.spec.whatwg.org/multipage/grouping-content.html#the-ol-element
- MDN, `<time>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/time
- MDN, `readonly`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly
- MDN, `aria-current`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current
- WCAG 2.2 Understanding:
  - 1.3.3 Sensory Characteristics: https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html
  - 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
  - 1.4.8 Visual Presentation: https://www.w3.org/WAI/WCAG22/Understanding/visual-presentation.html
  - 2.4.4 Link Purpose (In Context): https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html

**Research**

- Gatsby and Fable Tech Labs, accessible client-side routing user testing (n = 5, 2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- DWP Design System, Timeline design notes ("some people initially expect the oldest entry first"): https://design-system.dwp.gov.uk/components/timeline/design-notes
- GOV.UK Design System, Inset text ("Some users do not notice inset text…"): https://design-system.service.gov.uk/components/inset-text/
- NN/g, Kate Kaplan, "Designing Empty States in Complex Applications: 3 Guidelines" (2021): https://www.nngroup.com/articles/empty-state-interface-design/
- Adrian Roselli, "Avoid Read-only Controls" (2024): https://adrianroselli.com/2024/11/avoid-read-only-controls.html

**Conventions (design systems and established products)**

- MOJ Design System, Timeline: https://design-patterns.service.justice.gov.uk/components/timeline/
- DWP Design System, Timeline: https://design-system.dwp.gov.uk/components/timeline
- DWP Design System, Timeline accessibility: https://design-system.dwp.gov.uk/components/timeline/accessibility
- GOV.UK Design System, Back link: https://design-system.service.gov.uk/components/back-link/
- GOV.UK Design System, Summary list: https://design-system.service.gov.uk/components/summary-list/
- Carbon Design System, Modal usage: https://carbondesignsystem.com/components/modal/usage/
- NN/g, Therese Fessenden, "Modal & Nonmodal Dialogs: When (& When Not) to Use Them" (2017): https://www.nngroup.com/articles/modal-nonmodal-dialog/
- Cloudscape, Timestamps: https://cloudscape.design/patterns/general/timestamps/
- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- Wikipedia, Help:Page history: https://en.wikipedia.org/wiki/Help:Page_history
- Wikipedia, MediaWiki:Revision-info (old-revision notice): https://en.wikipedia.org/wiki/MediaWiki:Revision-info
- Atlassian, Confluence page history and page comparison views: https://confluence.atlassian.com/doc/page-history-and-page-comparison-views-139379.html
- Google Docs Editors Help, Find what's changed in a file: https://support.google.com/docs/answer/190843
- Scott O'Hara, "Fixing" Lists: https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- Heydon Pickering, Inclusive Components, Cards: https://inclusive-components.design/cards/
- Adrian Roselli, Block Links, Cards, Clickable Regions, Etc.: https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html
- TanStack Query v5, Important Defaults: https://tanstack.com/query/v5/docs/framework/react/guides/important-defaults

**Grow2Notes sources**

- design.md: 3.5, 3.8, 3.9, 4.0, 4.4, 4.5, 5.3, 5.8, 6.3, 11.3, 11.6, A6, A11, A13, A33
- decisions.md: D15, D19, D22, D34, D39
- Sibling component files: `status-tags.md`, `participant-list-rows.md`, `checkbox-list.md`, `app-shell-nav.md`
