# Chronological lists with "Show older"

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026: the error strings below follow microcopy.md §9, one set for Past notes and
> Flagged > Reviewed; place-keeping goes through the shell's `handle.returnFocus` (app-shell.md), not a list-owned map.

Component key: `chronological-list`. This file covers the lists of notes, flags and versions ordered by date, and the **Show older** button that loads the next 30 rows.

Other components own these related pieces. This file only sets how they sit inside a list row:

- **Status text and tags** (Draft, Submitted, Flagged, Edited, To review, Reviewed) and the meta-line markup belong to `status-tags`. This file uses that component's `MetaLine` and its row rules: the whole row is the link, and the status is the link's description.
- The **To review (n) | Reviewed** tabs and the Flagged badge belong to the tabs and `notification-badge` components.
- The **read view**, the **review panel** and the "Note for Jane Citizen submitted" kind of message belong to their own components.
- The **page heading and the focus move on route change** belong to the app shell. This file defines one exception to that rule: going back to a list (see Behaviour).

---

## Where it's used

| Screen | Order | How rows arrive | Row says | What differs |
|---|---|---|---|---|
| **Participant notes history** (design.md 4.4) | Newest first, by note date | 30 at a time, then **Show older** (`GET /api/participants/{id}/notes?before=` returns `hasMore`) | Note date, author, status (Draft or Submitted), tags (Edited, Flagged; managers also see To review or Reviewed) | The only list where any user (worker or manager) can page back. Rows include drafts. The participant's name is the page heading, so rows don't repeat it. |
| **Version history** (4.5, managers) | Newest first, by version | Whole list in one response (`GET {base}/versions`) | "Version 3 · Sam Lee (manager) · 1 Oct 2026, 5:03 pm". Version 1 is labelled "Submitted". | No **Show older**. Usually 1 to 3 rows. Has a one-version state: "Not edited since submit". |
| **Flagged notes: To review** (4.6, managers) | **Oldest first**, by when flagged | Whole list in one response (`GET /api/reviews?status=toReview`) | Participant, note date, author, first line of the flag reason, when it was flagged; "Flag removed in a later edit" where it applies | The only oldest-first list: it is a queue to work through. Rows leave the list when reviewed. Empty state: "No flagged notes to review." |
| **Flagged notes: Reviewed** (4.6, managers) | Newest first, by review time | 30 at a time, then **Show older** (`GET /api/reviews?status=reviewed&before=`) | The same as To review, plus reviewer, review time and comment | A record, not a queue. Sits in the second tab of the same page as To review. |

One component handles all four. It has two switches: **paged** (with **Show older**) or **whole**, and the row renderer each screen passes in.

---

## Best practice

### Load more vs pagination vs infinite scroll

- **[Research] Long, endless lists are poor for finding a specific item.** NN/g (Loranger, 2014): "Endless scrolling is not recommended for goal-oriented finding tasks." Finding an item again on a very long page "is inefficient". Reading a participant's notes, or checking a past review, is goal-oriented finding. https://www.nngroup.com/articles/infinite-scrolling/
- **[Research] A Load More button fixes the worst problems of infinite scroll.** NN/g (Neusesser, 2022) advises against infinite scroll when users want to "find something specific" or "compare items". It says "Adding a Load More button reduces some of the usability issues". The button "stops the constant flow of new content", so users can reach what sits below the list. With infinite scroll, keyboard users "have to 'tab' through content link by link". https://www.nngroup.com/articles/infinite-scrolling-tips/
- **[Research] In Baymard's e-commerce testing, Load More beat both other patterns.** Baymard (Holst, 2016; usability studies of 50+ e-commerce sites, published in Smashing Magazine) reports:
  - With pagination, participants "browsed much less of the total product list".
  - With Load More, they browsed more products than with pagination, and looked at each one more closely than with infinite scroll.
  - Its suggested mobile batch is **15–30 items** before a Load More button. Grow2Notes' 30 sits at the top of that range.

  These are shopping lists, not care records, so treat it as indirect evidence. https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/
- **[Convention] GOV.UK says not to load more content automatically as the user scrolls.** Its pagination guidance: "Avoid using the 'infinite scroll' technique to automatically load content when the user approaches the bottom of the page. This causes problems for keyboard users." It also says to "only break up content onto separate pages if it improves the performance or usability of your service". https://design-system.service.gov.uk/components/pagination/
- **[Opinion] (expert practitioner) Auto-loading lists leave out more than keyboard users.** Deque (Peri, 2019) lists speech-recognition users as "completely left out" of infinite scroll. It calls the experience "extremely time consuming" for switch users and "very stressful" for some low-vision users. It recommends Load More or pagination links as the fallback. https://www.deque.com/blog/infinite-scrolling-rolefeed-accessibility-issues/
- **[Opinion] (expert practitioner) Roselli's checklist for incremental lists:** "Can the user hit 'back' and return to the exact same place?" and "Can a user easily jump ahead a few 'pages'". He favours a user-triggered button over auto-loading (2014, updated 2024). https://adrianroselli.com/2014/05/so-you-think-you-built-good-infinite.html
- **[Standard] ARIA's own infinite-scroll pattern (`role="feed"`) has its own keyboard model.** It moves between articles with Page Down and Page Up, and leaves with Ctrl+End and Ctrl+Home. It also needs `aria-busy`, `aria-setsize` and `aria-posinset`. The APG notes the pattern lacks established keyboard conventions. A button-driven list needs none of this. https://www.w3.org/WAI/ARIA/apg/patterns/feed/
- **[Convention] AgDS offers pagination "to separate large lists of content… into smaller lists", and gives no Load More guidance.** No design system checked here (GOV.UK, AgDS) ships a Load More component. The pattern rests on the research and practitioner sources above. https://design-system.agriculture.gov.au/components/pagination

### Focus after loading more

- **[Standard] Content added by a user action must be reachable in a sensible order.** SC 2.4.3 requires focus order that "preserves meaning and operability". The Understanding document's example inserts new content "in the focus order immediately after the button". Failure F85 covers content that appears far from its trigger. https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html
- **[Opinion] (practitioners) Most sources move focus to the first new item. No study compares the options.**
  - Hovhannisyan (2021) focuses "the first newly inserted result" so keyboard focus "will visibly jump" to it. https://www.aleksandrhovhannisyan.com/blog/load-more-button-focus/
  - Barnett (2020) says "move `focus` to the first item in the new set of items" and also adds a polite status message. https://human-centred.nz/2020/04/22/infinite-scroll-and-accessibility/
  - Dissent, W3C WAI-IG list, August 2020. Steve Green (Test Partners) says the answer "is going to be context-dependent". For repeated loading of shop search results, he'd rather focus stayed on the button "so I could operate it repeatedly". Will Ringland (Epic) sets out the trade-off: moving focus backwards "can be confusing", while leaving it on the button means going back "through the loaded list which could be long". https://lists.w3.org/Archives/Public/w3c-wai-ig/2020JulSep/0120.html, https://lists.w3.org/Archives/Public/w3c-wai-ig/2020JulSep/0119.html
- **[Standard] Progress and failure messages that don't take focus must still reach screen readers.** SC 4.1.3 Status Messages covers them. The live region must already be in the page before text is written into it. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html, https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- **[Standard] `focus()` scrolls the element into view by default.** If `focusVisible` is not set, "a browser will provide visible indication if it determines that this would improve accessibility". In practice, keyboard users get a focus ring and tap users usually don't. That behaviour is a browser heuristic, so the exact result per browser is **unverified**. https://developer.mozilla.org/en-US/docs/Web/API/HTMLElement/focus
- **[Standard] A sticky header must not cover the focused row.** SC 2.4.11 lists "sticky headers" as the typical cause. "Using scroll padding so the banner does not overlap other content" passes (technique C43). https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **[Convention] Never use `disabled` on a button while its action runs.** It drops keyboard focus to the page body. Use `aria-disabled="true"` plus status text, and keep that text at full contrast. Source: the ui-build skill's control-states rules, which draw on MDN and the GOV.UK button guidance. https://design-system.service.gov.uk/components/button/
- **[Research] (expert judgement, 1993) Response-time limits.** 0.1 s feels instant. 1 s is "the limit for the user's flow of thought to stay uninterrupted". 10 s is the limit for attention. So show no loading text for loads under about 1 s. https://www.nngroup.com/articles/response-times-3-important-limits/

### Keeping the user's place

- **[Research] (benchmark, 2016) Losing the user's place is the most common failure.** Baymard found "over 90%" of benchmarked sites with Load More buttons did not return users to the same spot after they viewed an item. Baymard calls this a severe limitation. The figure is a 2016 shop benchmark, so it shows the problem is common, not a current rate. https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/
- **[Convention] React Router's `<ScrollRestoration>`** works only in data and framework modes. It stores scroll positions in **`sessionStorage`** (key `react-router-scroll-positions`, per `location.key`). https://reactrouter.com/api/components/ScrollRestoration
- **[Convention] TanStack Query 5 refetches every loaded page in order.** For infinite queries, "each group is fetched sequentially, starting from the first one" so "we're not using stale cursors". `hasNextPage` is false when `getNextPageParam` returns `undefined`. `isFetchNextPageError` reports a failed **Show older**. https://tanstack.com/query/v5/docs/framework/react/guides/infinite-queries
- **[Convention] TanStack's default network mode stops queries from running while the device is offline.** With no connection, a query sits in `fetchStatus: 'paused'` and shows no error. Retries default to 3, with a back-off delay between them. https://tanstack.com/query/v5/docs/framework/react/guides/network-mode, https://tanstack.com/query/v5/docs/framework/react/guides/query-retries

### Row anatomy, date grouping and date format

- **[Convention] The MOJ timeline is the closest published pattern to these lists.** It is for "events in date order" and "updates from different people". Each item has a title, a byline ("by Joe Bloggs") and a date in a `<time datetime>` element. It advises showing "the most recent events first, unless user research suggests a different order". It warns against using the timeline for large amounts of information. Where links repeat, it adds visually hidden text so each link is unique. The timeline does **not** group items under date headings. https://design-patterns.service.justice.gov.uk/components/timeline/
- **[Convention] The whole row is one link, and the status describes it.** GOV.UK's task list links the whole row and ties the status to the link with `aria-describedby`. `status-tags` adopted this for Grow2Notes. https://design-system.service.gov.uk/components/task-list/
- **[Standard] Ordered content uses `<ol>`.** The HTML spec: "The ol element represents a list of items, where the items have been intentionally ordered, such that changing the order would change the meaning of the document." Dates go in `<time datetime>`. https://html.spec.whatwg.org/multipage/grouping-content.html#the-ol-element, https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-time-element
- **[Convention] Safari and VoiceOver drop list semantics when the bullets are removed with CSS.** Adding `role="list"` restores them; lists inside `<nav>` were exempted in 2023 (O'Hara, 2019, updated 2023). https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- **[Convention] Australian date and time style.** NSW: "Write dates in day-month-year order… the month in full". "If space is limited, use a 3-letter month abbreviation" ("2 Jul 2018"). Times use the 12-hour clock with a colon and a non-breaking space before am or pm ("9:30 am"). The Australian Government Style Manual also puts a non-breaking space before lower-case am and pm, and prefers "noon" and "midnight" to "12 pm" and "12 am". https://www.digital.nsw.gov.au/delivery/digital-service-toolkit/resources/writing-content/content-style-guide/numbers-dates-and-times, https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- **[Standard] Link names must make sense in context, and the visible label must be in the name.** SC 2.4.4 and SC 2.5.3. https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html, https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- **Date grouping (headings such as "October 2026"): no usability evidence found either way.** This is **unverified**. Material 3 lists allow subheaders [Convention]. The MOJ timeline puts the date on each item instead [Convention].

---

## Recommendation for Grow2Notes

### The rule in one line

**A plain `<ol>` of whole-row links, one row per note, flag or version, each with its full date. Paged lists end in a "Show older" button. When it loads, focus moves to the first new row. Nothing loads on scroll, nothing is counted, and nothing is stored on the device.**

### Anatomy

```
<h2>Notes</h2>                                  heading owned by the screen

┌─────────────────────────────────────────┐
│ Thursday 1 October 2026                 │  1. row link (the date, underlined)
│ Submitted · Alex P.  [Flagged] [Edited] │  2. meta line (status-tags MetaLine)
├─────────────────────────────────────────┤
│ Wednesday 30 September 2026             │
│ Draft · Alex P.                         │
├─────────────────────────────────────────┤
│ …28 more rows                           │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│               Show older                │  3. Show older button (paged lists only)
└─────────────────────────────────────────┘
  Loading older notes…                         4. status line (always in the DOM, usually empty)
```

1. **Row link.** It is the row's identity, and it is unique within the list:
   - **History:** the note date, "Thursday 1 October 2026". There is one note per participant per day (D6), so dates never repeat.
   - **Flagged (both tabs):** the participant's name on one line and the note date on the next, both inside the link. Its name is "Jane Citizen, Thursday 1 October 2026". Name + date is unique for the same reason (D6). It is the MOJ "make repeated links unique" rule, met with visible text instead of hidden text.
   - **Versions:** "Version 3".
   - Styling: body size, semibold, link colour, underlined. A `::after` overlay makes the whole row the tap target, as in `status-tags`.
2. **Meta line** (`status-tags` `MetaLine`): `who · when` plus tags, with `·` separators hidden from screen readers. The row link points to it with `aria-describedby`.
3. **Extra lines** (Flagged only), in plain text in reading order:
   - "Reason: …"
   - "Flag removed in a later edit" (To review, when it applies)
   - "Comment: …" (Reviewed, when there is a comment)
4. **Show older** (paged lists only): a secondary button directly after the list.
5. **Status line**: a `<p role="status">` directly after the button. It is always rendered and is empty unless loading is slow or has failed.
6. **Row divider**: a 1 px line between rows, decorative only. No zebra stripes, no cards, no chevrons, no icons.

**No date-group headings.** Every row already carries its full date. In history the date *is* the link. A month heading would repeat it, add a heading level, and split awkwardly when **Show older** adds rows to a month that is already on screen. [Opinion]

**No relative dates** such as "Today", "Yesterday" or "3 days ago". [Opinion], grounded in the design:
- "Today" comes from the server's Melbourne clock and never from the device (3.3, A33). A relative label would either use the device clock or go stale on a page left open past midnight.
- The date on screen must match the date in the daily report and the export file, which are records.

### Behaviour

**Loading and order**
- **First load.** Paged lists ask for the first 30 rows. Whole lists ask for everything. Rows render in the order the API returns them. The client never re-sorts.
- **Show older** asks for the next 30 rows, using the last row on screen as the `before` cursor. New rows are appended below the existing ones. Rows already on screen do not move.
- **Never load on scroll.** No `IntersectionObserver` and no "load when near the bottom". The button is the only trigger (GOV.UK, NN/g, Deque).
- **No counts.** There is no "Showing 60 notes", no "30 more" and no page numbers. The API returns only `hasMore` (6.1), and the app shows no totals (D26, A17).

**Focus after Show older**
- **Focus moves to the link of the first new row** once it is in the DOM. Keyboard and screen-reader users land on the new content, and Tab then continues down through it to **Show older** again. This follows the practitioner majority and SC 2.4.3. The trade-off is that loading another batch means tabbing through 30 rows. That is accepted, because the task is reading notes, not loading pages. [Opinion]
- Call `focus()` with no `focusVisible` option, so the browser decides whether to draw a ring. A tap user's view barely moves, because the first new row appears where the button was. [Standard] MDN; per-browser ring behaviour **unverified**.
- **When the last batch arrives** (`hasMore` is false), the button is removed. Focus is already on the first new row, so it is never lost.
- **When nothing new arrives** (a rare race, for example a draft discarded meanwhile) and the button disappears: move focus to the last row's link, so focus never falls to `<body>`.

**Repeat taps**
- While a load is running, the button has `aria-disabled="true"` and taps are ignored. It is never `disabled`, which would throw keyboard focus to the page body (ui-build).

**Background refresh**
- TanStack refetches on window focus (7.5). Every loaded page is refetched in order. Rows are keyed by a stable ID, so React keeps the DOM nodes and focus.
- Nothing blanks out, nothing animates, and nothing is announced.
- A failed background refresh is silent: the rows already on screen stay.

**Going back to a list**
- When the user opens a row and then goes back (browser Back, or the in-app back that uses history), two things happen:
  - The list shows the same rows it had, from the in-memory cache.
  - The page returns to the same scroll position, and **focus goes to the row the user opened**.
- This is the one exception to the app shell's "focus the page heading on navigation" rule: on a back navigation to a list that restored a row, the list's focus wins. Keyboard and screen-reader users then carry on from where they were. [Opinion] built on Baymard's and Roselli's place-keeping finding.
- After a reload, or after the cache has expired, the list starts again at the newest 30 rows at the top. The place is kept in memory exactly as long as the rows themselves.

**Read-only rows**
- Rows never carry their own buttons: no Mark reviewed, no Edit, no swipe actions. Mark reviewed lives on the read view, under the note it reviews (4.6). The API also requires the current version number, so a review happens after reading the current note.

### States

| State | List rows | Show older button | Status line |
|---|---|---|---|
| **Default** | As in Anatomy | Shown when `hasMore` | Empty |
| **Hover** (pointer devices only) | Row background tint; link underline thickens | Shared secondary-button hover | — |
| **Focus** | 3 px outline around the whole row (on the link's `::after`), ≥ 3:1 against the background, always visible (4.0) | Shared button focus ring | — |
| **Active** (pressed) | Darker row tint while pressed | Shared button pressed style | — |
| **Loading, first page** | Rows not yet rendered. Under 1 s, nothing is shown. After 1 s: "Loading notes…" in the list's place, written into a `<p role="status">` that mounts empty with the page. No skeleton rows and no spinner. | Not shown | — |
| **Loading, Show older** | Existing rows unchanged | `aria-disabled="true"`, label unchanged ("Show older"), `cursor: progress` | Empty for the first 1 s, then "Loading older notes…" |
| **Error, first page** | Each screen's microcopy.md §9 line ("Past notes did not load: [cause]. Try again.", "Flagged notes did not load: …") in the same status paragraph, and "… still did not load: …" after a failed Try again, followed (outside it) by a **Try again** button that calls `refetch`. If the app shell already has a shared load-error message, use that instead. | Not shown | — |
| **Error, Show older** | Existing rows unchanged; focus stays on the button | Enabled again, still "Show older" | "Older notes did not load: no connection. Try again." or "Older notes did not load: something went wrong. Try again."; a second failure in a row: "Older notes still did not load: [cause]. Try again." (changed text, so it is announced again). It clears when the next attempt starts. |
| **Empty** | The screen's empty sentence, as a plain `<p>` with no list and no button. History: "No notes yet for Jane Citizen." To review: "No flagged notes to review." Reviewed: "No reviewed notes yet." (proposed; not in design.md) | Not shown | — |
| **End of list** | Last row is the oldest | Removed | Empty. No "end of list" message: the missing button is the signal. [Opinion] |
| **Disabled** | Never. Rows are never greyed out or made unopenable. A draft row the viewer can't edit still opens the note route, which shows what that viewer may see. | Only `aria-disabled` while loading | — |
| **Read-only** | Always read-only (see Behaviour) | — | — |
| **Forced colours** | Hover and pressed tints disappear (harmless). Dividers and the focus outline stay, in system colours. | Shared button forced-colours style | Plain text |
| **200% text / 320 px** | Single column, text wraps, no fixed heights, no truncation | Full width, height grows with text | Wraps |

**Copy notes**
- "Loading…" text appears only after 1 s, so fast loads cause no flicker (NN/g response-time limits).
- Error strings avoid negative contractions and say what to do next. "Select" is device-neutral.
- First-load errors name the list (microcopy.md §9). One Show older string set serves both paged lists ("Older notes did not load: …" and its "still" form), because the Reviewed tab's rows are notes too.

### Phone and laptop

| | Phone (single column, 320–599 px) | Laptop |
|---|---|---|
| List width | Full width with page margins | Same single column, `max-inline-size: 40rem`. No table and no far-right columns (`status-tags`: keep tags near the item). |
| Row | Two lines minimum (link, meta), plus Reason and Comment lines on Flagged. `min-block-size: 44px`, padding `0.75rem` top and bottom. | Same markup and line breaks. Hover tint only on `(hover: hover)` devices. |
| Show older | Full width, ≥ 44 px tall. [Convention] GOV.UK Frontend's button is full width below its tablet breakpoint (current version **unverified**). | Auto width, left-aligned under the list |
| Sticky top bar (if the app has one) | `scroll-padding-block-start` on `html` equal to its height, so a focused row is never hidden under it (SC 2.4.11, C43) | Same |

### Exact copy

| Where | Copy | Source |
|---|---|---|
| Paged-list button | Show older | design.md 4.4 |
| History row link | Thursday 1 October 2026 | date format from the brief |
| History meta | Submitted · Alex P. / Draft · Alex P., then tags | 4.4, `status-tags` |
| Flagged row link | Jane Citizen / Thursday 1 October 2026 | 4.6 (participant, note date) |
| To review meta | Alex P. · flagged 1 Oct 2026, 4:12 pm | 4.6 (author, when flagged); "who · verb time" pattern from 4.2 ("started 9:14 am") |
| To review extra lines | Reason: Mentioned pain in his left knee after the walk. / Flag removed in a later edit | 4.6; "Reason" is the form field's label (4.3) |
| Reviewed meta | Alex P. · reviewed by Sam Lee, 2 Oct 2026, 9:30 am | 4.6 (reviewer, time); "Reviewed by …" from 11.3 |
| Reviewed extra lines | Reason: … / Comment: … | 4.6 |
| Version row link | Version 3 | 4.5 |
| Version meta | Sam Lee (manager) · 1 Oct 2026, 5:03 pm; version 1: Submitted · Alex P. · 1 Oct 2026, 4:12 pm | 4.5, `status-tags` |
| Empty: history | No notes yet for Jane Citizen. | 4.4 |
| Empty: To review | No flagged notes to review. | 4.6 |
| Empty: Reviewed | No reviewed notes yet. | **proposed**: design.md has no string |
| One version | Not edited since submit | 4.5 |
| Slow first load | Loading notes… | proposed |
| First-load error | [Thing] did not load: [cause]. Try again. + **Try again**; repeat: [Thing] still did not load: [cause]. Try again. | microcopy.md §9 |
| Slow Show older | Loading older notes… | proposed |
| Show older error | Older notes did not load: [cause]. Try again. · repeat: Older notes still did not load: [cause]. Try again. | microcopy.md §9 (participant-notes.md, flagged.md) |

### Accessibility

**Semantics**
- `<ol role="list">`: an ordered list, because the order is the meaning. `role="list"` keeps list semantics in Safari and VoiceOver after `list-style: none`.
- `<li>` per row, `<a href>` row link, `<time dateTime="2026-10-01">` for dates and `<time dateTime="2026-10-01T06:12:00Z">` for timestamps.
- `<button type="button">` for Show older.
- `<p role="status">` for the status line.

**ARIA used, and only this**
- `aria-describedby` on the row link, pointing at the meta line (`status-tags`).
- `aria-disabled="true"` on Show older while loading.
- `role="list"` (Safari fix).
- `role="status"` on the status line.

**ARIA not used**
- No `role="feed"`, `aria-busy`, `aria-setsize` or `aria-posinset`. The native list count is enough.
- No `aria-live` on the `<ol>`, which would read the whole list on every refresh.
- No `aria-label` on rows. The visible text is the name (SC 2.5.3).

**Keyboard**
- Tab moves row link to row link, then reaches Show older.
- Enter on a row opens it. Enter or Space on Show older loads more and moves focus to the first new row.
- There are no arrow-key or custom keys. Each row is one tab stop.

**Screen reader**
- A history row reads as "Thursday 1 October 2026, link, Submitted, Alex P., Flagged, Edited" (description timing varies by screen reader; **unverified** per AT).
- After Show older, the screen reader announces the newly focused row. That focus move *is* the announcement, so no extra "30 notes loaded" message is spoken (fewer live regions; see `notification-badge`).
- A slow load says "Loading older notes…" politely. A failure is announced politely while focus stays on the button.
- On going back, the restored row is announced.

**WCAG 2.2 criteria met**
- 1.3.1 Info and Relationships (`ol`, `time`, headings from the screen, meta tied to the link)
- 1.3.2 Meaningful Sequence (DOM order = visual order, new rows after old)
- 1.4.1 Use of Color (statuses in words)
- 1.4.3 Contrast (text ≥ 4.5:1, including the `aria-disabled` button label)
- 1.4.4 Resize Text, 1.4.10 Reflow (one column at 320 px), 1.4.12 Text Spacing (no fixed heights or clamps)
- 1.4.11 Non-text Contrast (focus outline ≥ 3:1; button boundary ≥ 3:1)
- 2.1.1 Keyboard (native link and button)
- 2.4.3 Focus Order (focus lands on the first new row; never lost on end of list or error)
- 2.4.4 Link Purpose in Context (unique, meaningful link text)
- 2.4.7 Focus Visible
- 2.4.11 Focus Not Obscured (Minimum) (scroll padding under a sticky bar)
- 2.5.3 Label in Name
- 2.5.8 Target Size (Minimum) (whole row and button ≥ 44 px, above the 24 px floor)
- 3.2.1 On Focus (nothing loads or navigates on focus or scroll)
- 4.1.2 Name, Role, Value
- 4.1.3 Status Messages (loading and error text through `role="status"` without moving focus)

### Implementation notes (React 19, native HTML, CSS Modules, TanStack Query 5)

Native HTML covers everything here, so **no React Aria is needed**. One generic component, `ChronoList`, renders the `<ol>`, the empty sentence, the button and the status line. Screens pass a row renderer and, for paged lists, the infinite-query result.

**Data: one infinite query per paged list, a plain query per whole list.**

```ts
// src/features/history/useParticipantHistory.ts
import { useInfiniteQuery } from '@tanstack/react-query';

export function useParticipantHistory(participantId: string) {
  return useInfiniteQuery({
    queryKey: ['participants', participantId, 'notes'],
    queryFn: async ({ pageParam, signal }) => {
      const { data, error } = await api.GET('/api/participants/{participantId}/notes', {
        params: { path: { participantId }, query: pageParam ? { before: pageParam } : {} },
        signal,
      });
      if (error) throw error;
      return data; // { notes: [...], hasMore }
    },
    initialPageParam: undefined as string | undefined, // server default: tomorrow
    getNextPageParam: (last) => (last.hasMore ? last.notes.at(-1)?.noteDate : undefined),
    select: (d) => d.pages.flatMap((p) => p.notes),   // rows for ChronoList
    networkMode: 'always', // offline => fail and show the error, never sit "paused" silently
    gcTime: 30 * 60_000,   // keep loaded pages in memory while the user reads a note (idle limit is 30 min)
  });
}
// Reviewed tab: same shape on /api/reviews?status=reviewed, cursor = last row's reviewedAtUtc (see open points).
// To review and versions: plain useQuery; no cursor, no button.
```

Why these options:
- **`networkMode: 'always'`.** In the default `online` mode, a Show older tapped offline would sit in `paused` with no error. The user would see nothing happen.
- **`gcTime`.** The default of 5 minutes would drop the loaded pages while a manager reads a long note. Then back would land at the top. Memory only, so D22 still holds.
- **No `maxPages`.** It would drop rows above the user and break focus and back navigation. A participant gets at most about 12 pages a year, so refetching every page in order on focus is cheap.
- **Retries.** Keep TanStack's default retries. During retries the status line shows "Loading older notes…". The error appears only after the last retry fails.

**The list component**

```tsx
// src/components/chrono-list/ChronoList.tsx
import { useEffect, useRef, type ReactNode } from 'react';
import { useDelayedFlag } from '../../lib/useDelayedFlag'; // true once `value` has been true for `ms`
import styles from './ChronoList.module.css';

type Older = {
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  fetchNextPage: () => Promise<{ isError: boolean }>;
};

type Props<T> = {
  items: T[];
  getKey: (item: T) => string;          // stable: noteDate, participantId+noteDate, versionNumber
  renderRow: (item: T) => ReactNode;     // must contain exactly one <a href>: the row link
  emptyText: string;
  older?: Older;                         // omit for whole lists
};

export function ChronoList<T>({ items, getKey, renderRow, emptyText, older }: Props<T>) {
  const listRef = useRef<HTMLOListElement>(null);
  const focusAfterKey = useRef<string | null>(null);
  const slow = useDelayedFlag(older?.isFetchingNextPage ?? false, 1000);

  // After "Show older": focus the row that follows the previously last row, once it exists.
  useEffect(() => {
    const key = focusAfterKey.current;
    if (key === null) return;
    const i = items.findIndex((it) => getKey(it) === key);
    if (i === -1 || i === items.length - 1) return;      // new rows not rendered yet
    focusAfterKey.current = null;
    listRef.current?.children[i + 1]?.querySelector<HTMLElement>('a[href]')?.focus();
  }, [items]); // eslint-disable-line react-hooks/exhaustive-deps

  async function showOlder() {
    if (!older || older.isFetchingNextPage) return;      // aria-disabled: ignore repeat taps
    focusAfterKey.current = getKey(items[items.length - 1]);
    const result = await older.fetchNextPage();
    if (result.isError) focusAfterKey.current = null;     // focus stays on the button
    // If no rows came back and the button is now gone, focus the last row's link instead.
  }

  if (items.length === 0) return <p className={styles.empty}>{emptyText}</p>;

  return (
    <>
      <ol role="list" ref={listRef} className={styles.list}>
        {items.map((it) => (
          <li key={getKey(it)} className={styles.row}>{renderRow(it)}</li>
        ))}
      </ol>
      {older && (
        <div className={styles.more}>
          {older.hasNextPage && (
            <button type="button" className={styles.olderButton}
              aria-disabled={older.isFetchingNextPage || undefined} onClick={showOlder}>
              Show older
            </button>
          )}
          <p role="status" className={styles.status}>
            {older.isFetchNextPageError && !older.isFetchingNextPage
              ? 'Older notes did not load. Check your connection, then select Show older again.'
              : slow ? 'Loading older notes…' : ''}
          </p>
        </div>
      )}
    </>
  );
}
```

**A history row and a To review row** (the row renderers each screen passes in)

```tsx
function HistoryRow({ n, participantId }: { n: HistoryNote; participantId: string }) {
  const metaId = useId();
  return (
    <>
      <Link to={`/participants/${participantId}/notes/${n.noteDate}`}
            className={styles.rowLink} aria-describedby={metaId}
            onClick={() => rememberPlace(n.noteDate)}>
        <time dateTime={n.noteDate}>{formatNoteDate(n.noteDate)}</time>
      </Link>
      <MetaLine id={metaId} parts={[n.status, n.authorDisplayName]} tags={historyTags(n)} />
    </>
  );
}

function ToReviewRow({ r }: { r: ToReviewItem }) {
  const metaId = useId();
  return (
    <>
      <Link to={`/participants/${r.participantId}/notes/${r.noteDate}`}
            className={styles.rowLink} aria-describedby={metaId}
            onClick={() => rememberPlace(`${r.participantId}/${r.noteDate}`)}>
        <span className={styles.name}>{r.givenName} {r.familyName}</span>
        <span className="visually-hidden">, </span>
        <time className={styles.line} dateTime={r.noteDate}>{formatNoteDate(r.noteDate)}</time>
      </Link>
      <MetaLine id={metaId} parts={[r.authorDisplayName, `flagged ${formatDateTime(r.flaggedAtUtc)}`]} />
      <p className={styles.extra}>Reason: {firstLine(r.flagReason)}</p>
      {r.flagRemovedLater && <p className={styles.extra}>Flag removed in a later edit</p>}
    </>
  );
}
// firstLine = text up to the first line break, shown in full (never clipped with an ellipsis).
```

**Keeping the place on back, in memory only**

```ts
// src/lib/listPlace.ts: lives exactly as long as the in-memory query cache (D22: nothing on the device)
const places = new Map<string, { y: number; rowKey: string }>(); // keyed by location.key
if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

// rememberPlace(rowKey) runs on row-link click: places.set(location.key, { y: scrollY, rowKey })
// In the list page: when useNavigationType() === 'POP' and the rows are rendered,
//   useLayoutEffect -> scrollTo(0, place.y); then focus the row link whose key === place.rowKey
//   with { preventScroll: true }. If that row isn't rendered (cache gone), do nothing: newest 30 at top.
```

Don't use React Router's `<ScrollRestoration>` here. It writes to `sessionStorage`. The stored values are only scroll offsets, which 9.6 technically allows (they are not note data), but keeping the place in memory removes the question entirely. It also works whichever router mode is used.

**CSS Module**

```css
/* ChronoList.module.css */
.list { list-style: none; margin: 0; padding: 0; max-inline-size: 40rem; }
.row {
  position: relative;                  /* anchor for the row link's ::after */
  min-block-size: 44px;
  padding-block: 0.75rem;
  border-block-end: 1px solid var(--divider);   /* decorative */
}
.rowLink { font-weight: 600; color: var(--link); text-decoration: underline; text-underline-offset: 0.15em; }
.rowLink::after { content: ""; position: absolute; inset: 0; }   /* whole row is the target */
.rowLink:focus-visible { outline: none; }
.rowLink:focus-visible::after { outline: 3px solid var(--focus); outline-offset: -3px; }
.name, .line { display: block; }
.extra { margin: 0.25rem 0 0; overflow-wrap: anywhere; }          /* wrap, never clamp */
@media (hover: hover) {
  .row:hover { background: var(--row-hover-bg); }
  .row:hover .rowLink { text-decoration-thickness: 2px; }
}
.row:has(.rowLink:active) { background: var(--row-active-bg); }
.more { margin-block-start: 1rem; max-inline-size: 40rem; }
.olderButton { inline-size: 100%; min-block-size: 44px; }           /* shared secondary button styles */
.olderButton[aria-disabled="true"] { cursor: progress; }            /* label keeps full contrast */
@media (min-width: 40em) { .olderButton { inline-size: auto; } }
.status:empty { margin: 0; }
/* No transitions or animations on rows: new rows appear instantly (and under reduced motion). */
```

Other notes:
- **Dates.** Format `noteDate` as a calendar date, never through the device's time zone. `Intl.DateTimeFormat('en-AU', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })` on `Date.UTC(y, m - 1, d)` gives "Thursday 1 October 2026". Timestamps use `timeZone: 'Australia/Melbourne'`. Both were checked in Node 24 (ICU 77) on 1 October 2026; test in target browsers.
- **Short months.** Don't use `month: 'short'` for the "1 Oct 2026, 5:03 pm" pattern. In the same check, en-AU returned "Sept", "June" and "July", not "Sep", "Jun" and "Jul". With `weekday: 'short'` it added a comma ("Mon, 28 Sept 2026"). Build that pattern from parts with a fixed 3-letter month table (NSW: "use a 3-letter month abbreviation").
- **Keys.** Use `noteDate` (history), `participantId + noteDate` (Flagged) and `versionNumber` (versions). Never use the array index.
- **After Mark reviewed,** invalidate `['reviews']` so To review drops the row and Reviewed gains it on next view.

---

## Per-screen notes

**Participant notes history (4.4): paged, newest first**
- Rows are dates. The meta line and tags come from `status-tags`: `Submitted · Alex P.` plus `[Flagged] [To review] [Edited]`. A draft row reads `Draft · Alex P.` with no tags.
- **Every row is a link to the note route.** The note route decides what the viewer gets:
  - the author's own draft opens the form;
  - a manager gets the read-only draft;
  - another worker gets the status-only view;
  - a submitted note opens the read view (4.2, 4.4).

  The history API returns no author ID or `isMine`, so the list can't make this decision itself, and shouldn't.
- The screen owns the heading above the list (participant name as `h1`; a short `h2` such as "Notes" before the list) and the manager actions (Write past-day note, Export record, Edit participant). They are not part of this component.
- Arriving from a "Past notes" link is a new visit: the list shows from the top. Only Back restores the place.
- Workers and managers use the same list. Only the tags differ, because the API sends `flagStatus` only to managers.

**Version history (4.5): whole, newest first, managers**
- Row link "Version 3"; meta `Sam Lee (manager) · 1 Oct 2026, 5:03 pm`. The version 1 row's meta starts with "Submitted", matching `status-tags`.
- Don't add a "Current" marker. The page header already gives the current version number (4.5 item 1).
- **One version:** show "Not edited since submit" as a sentence above a list holding the single Version 1 row, so the screen's primary action ("open a version") still works. [Opinion]
- No Show older. Back from a version restores the place like the other lists, which matters little at 1–3 rows.

**Flagged notes (4.6): two variants on one page, managers**
- **To review: whole, oldest first.** It is a queue. Oldest first means the longest-waiting flag is at the top, which is the design's order. Rows: name + note date (link), `Alex P. · flagged 1 Oct 2026, 4:12 pm`, `Reason: …`, and "Flag removed in a later edit" where `flagRemovedLater`. No tags (the tab says the state). No Show older.
- **Reviewed: paged, newest first.** Rows: the same link, `Alex P. · reviewed by Sam Lee, 2 Oct 2026, 9:30 am`, `Reason: …`, `Comment: …` (omitted when there is no comment). Show older as for history.
- Switching tabs keeps each tab's loaded rows, from the in-memory cache. Going back to the Reviewed tab after reading a note restores its place.
- After Mark reviewed, the row leaves To review on the next fetch. Where the manager lands and what is announced belongs to the review panel and notification components. This list just re-renders, with focus handled by the app shell.

### Points the design leaves open

These are for the owner or the named component. None adds a feature.

1. **Reviewed cursor.** 6.4 shows `before=` but doesn't name the field. Default: the last row's `reviewedAtUtc`. Two reviews with the same timestamp could straddle a page boundary and one could be skipped. The API owner may want a tie-breaker in the cursor. **Unverified** whether SQL timestamp precision makes this possible in practice.
2. **"First line of the flag reason" (4.6).** Read here as the text up to the first line break, shown in full with no ellipsis. If the owner meant one *visual* line, CSS clamping would hide text that can't be revealed in the row (it is in the read view).
3. **Reviewed empty state.** design.md gives only "No flagged notes to review." Proposed: "No reviewed notes yet."
4. **"(manager)" on version rows.** The 4.5 example shows the saver's role, but `GET {base}/versions` returns `createdBy` with no role field. Either the API adds it or the row drops it.
5. **Loading and error copy.** design.md has none for lists. The four strings above are proposals in the app's existing voice ("Not saved: no connection…").
6. **Shared date formatter.** design.md mixes "1 Oct 2026, 5:03 pm", "Fri 2 Oct 9:30 am" and "Mon 28 Sep 2026". The ICU short-month behaviour above means the formatter must build these by hand. Whether to show "12 pm" or "noon" (Style Manual) is the formatter owner's call.

---

## Anti-patterns to avoid

1. **Infinite scroll or auto-loading near the bottom** (`IntersectionObserver`, scroll listeners). Keyboard, speech and switch users can't drive it, and finding a note gets harder (GOV.UK, NN/g, Deque).
2. **Leaving focus on Show older after new rows appear,** or letting focus drop to `<body>` when the button disappears.
3. **`disabled` on Show older while loading** (focus is lost), or changing its label to "Loading…" (a name change mid-focus is announced inconsistently). Use `aria-disabled` and the status line.
4. **`aria-live` or `role="status"` on the list itself.** Every refresh would read the rows aloud. One empty status line only.
5. **Counts and page numbers:** "Showing 60 of 230", "30 more", "Page 2 of 8". The API has no total, and the app shows no totals (D26, A17).
6. **Spinners, skeleton rows or blanking the list** during a background refetch or a Show older. Rows on screen stay put.
7. **Animating rows in** (fade, slide, highlight flash). New rows appear instantly.
8. **Losing the user's place on Back:** returning to the top with only 30 rows after the user had loaded 90 (Baymard).
9. **Storing the list, cursor or scroll position in `localStorage` or `sessionStorage`,** including via React Router's `<ScrollRestoration>` default (D22, 9.6).
10. **`maxPages`** or any trimming of earlier rows. Rows vanish above the user, and focus and back break.
11. **Relative dates** ("Today", "Yesterday", "3 days ago"). They depend on a clock the design forbids (3.3) and don't match the report.
12. **Date-group headings or sticky date headers.** Not in the design, and they duplicate the per-row date.
13. **Actions inside rows:** Mark reviewed, Edit or Discard buttons on rows, or swipe-to-review. Reviews happen on the read view after reading the current version (4.6, 409 `review.not_current`).
14. **`<div onClick>` rows, or nesting buttons and links inside the row link.** One `<a href>` per row; the `::after` overlay gives the full-row target.
15. **Truncating reasons or comments with an ellipsis** or a fixed row height. It clips at 200% text and hides what was written.
16. **A table on phones** with horizontal scrolling (SC 1.4.10), or right-aligned columns on laptop that separate tags from their row.
17. **Sort, filter or search controls on these lists.** Not specified (search exists only on Today and Participants).
18. **Colour-only distinctions,** such as amber backgrounds on To review rows (SC 1.4.1; `status-tags`).

---

## Tensions with decisions

- **None with D1–D43.**
  - "30 at a time, then Show older" sits within Baymard's suggested mobile range (15–30).
  - A button-triggered load is what GOV.UK, NN/g and Deque recommend.
  - The no-counts rule (D26, A17) costs nothing here, because the API has no total.
  - D22 is met by keeping both the rows and the user's place in memory.
- **One observation about design.md 4.4 (not a decision), with no change proposed.** NN/g says long incremental lists are weak for "goal-oriented finding", and Roselli asks whether users "can jump ahead". With one note per day, each Show older reaches back about a month, so a note from six months ago takes about six taps. The design already gives managers date-based routes (Daily report by date, 4.7; Export record by date range, 4.12). For workers, the friction is accepted as specified.

---

## Sources

**Standards and specifications**
- WCAG 2.2 Understanding 2.4.3 Focus Order (and failure F85): https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum), technique C43: https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 2.4.4 Link Purpose (In Context): https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WAI-ARIA APG, Feed pattern: https://www.w3.org/WAI/ARIA/apg/patterns/feed/
- HTML Living Standard, the `ol` element: https://html.spec.whatwg.org/multipage/grouping-content.html#the-ol-element
- HTML Living Standard, the `time` element: https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-time-element
- MDN, `HTMLElement.focus()` (`preventScroll`, `focusVisible`): https://developer.mozilla.org/en-US/docs/Web/API/HTMLElement/focus
- MDN, ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions

**Research**
- NN/g, Loranger, "Infinite Scrolling Is Not for Every Website" (2014): https://www.nngroup.com/articles/infinite-scrolling/
- NN/g, Neusesser, "Infinite Scrolling: When to Use It, When to Avoid It" (2022): https://www.nngroup.com/articles/infinite-scrolling-tips/
- Baymard Institute (Holst), "Infinite Scrolling, Pagination Or 'Load More' Buttons? Usability Findings In eCommerce", Smashing Magazine (2016): https://www.smashingmagazine.com/2016/03/pagination-infinite-scrolling-load-more-buttons/
- NN/g, Nielsen, "Response Times: The 3 Important Limits" (1993 limits): https://www.nngroup.com/articles/response-times-3-important-limits/

**Conventions (design systems, style guides, libraries)**
- GOV.UK Design System, Pagination: https://design-system.service.gov.uk/components/pagination/
- GOV.UK Design System, Task list (whole-row link, status via `aria-describedby`): https://design-system.service.gov.uk/components/task-list/
- GOV.UK Design System, Button: https://design-system.service.gov.uk/components/button/
- Ministry of Justice Design System, Timeline: https://design-patterns.service.justice.gov.uk/components/timeline/
- Australian Government Design System (AgDS), Pagination: https://design-system.agriculture.gov.au/components/pagination
- NSW Digital Service Toolkit, Numbers, dates and times: https://www.digital.nsw.gov.au/delivery/digital-service-toolkit/resources/writing-content/content-style-guide/numbers-dates-and-times
- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- TanStack Query 5, Infinite queries: https://tanstack.com/query/v5/docs/framework/react/guides/infinite-queries
- TanStack Query 5, Network mode: https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
- TanStack Query 5, Query retries: https://tanstack.com/query/v5/docs/framework/react/guides/query-retries
- React Router, `ScrollRestoration`: https://reactrouter.com/api/components/ScrollRestoration
- Scott O'Hara, "'Fixing' Lists" (Safari list semantics, 2019, updated 2023): https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html

**Expert practitioner opinion**
- Adrian Roselli, "So You Think You've Built a Good Infinite Scroll" (2014, updated 2024): https://adrianroselli.com/2014/05/so-you-think-you-built-good-infinite.html
- Deque (Peri), "Infinite Scrolling & Role=Feed Accessibility Issues" (2019): https://www.deque.com/blog/infinite-scrolling-rolefeed-accessibility-issues/
- Aleksandr Hovhannisyan, "Managing Keyboard Focus for Load-More Buttons" (2021, updated 2022): https://www.aleksandrhovhannisyan.com/blog/load-more-button-focus/
- Steve Barnett, "Infinite scroll accessibility and usability" (2020): https://human-centred.nz/2020/04/22/infinite-scroll-and-accessibility/
- W3C WAI-IG mailing list, "Focus order and Load More functions" (Green; Ringland, August 2020): https://lists.w3.org/Archives/Public/w3c-wai-ig/2020JulSep/0120.html, https://lists.w3.org/Archives/Public/w3c-wai-ig/2020JulSep/0119.html

**Checked locally (1 October 2026):** Node 24.12 / ICU 77.1. `Intl.DateTimeFormat('en-AU')` gives "Thursday 1 October 2026" and "4:12 pm" (plain space), and short months "Sept", "June" and "July".
