# Participant list rows with note status

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Return focus goes through the shell's `handle.returnFocus` (rows carry `data-return-key`), not a list-owned module variable; rows push no router state (app-shell.md).

Component key: `participant-list-rows`. This covers the rows on **Today** (design.md 4.2): the main list of every active participant with today's note status, and the rows in the **Your unfinished drafts** section above it. It uses data from `GET /api/today` and `GET /api/me/drafts` (6.3) and follows A5, A9, A17, A32, D13, D20 and D25. Nothing here adds a screen, setting, notification, status or data item. It describes how to build the rows the design already specifies.

---

## Where it's used

One screen, Today (4.2). The same row is used in two lists on that screen. Both are the same for workers and managers. Only the destination changes by role, and the row never shows that difference.

| Where | What the row shows | What tapping does | What differs |
|---|---|---|---|
| **Today, main list** (4.2 item 4) | The participant's name. Under it, a status line, but only when a note exists for today (A17): `Draft · You · saved 9:42 am`, `Draft · Alex P. · started 9:14 am` or `Submitted · Alex P. · 4:12 pm`, plus a `Flagged` tag when the current version is flagged. | Opens the note route for (participant, today). That page decides what to show: an empty form, your own draft, the blocked message (workers) or the read-only draft with Discard (managers), or the read view. | This is the main variant. Every active participant is listed, sorted by family name then given name (A5). The search box above filters it. There are no counts, "missing" markers or "not started" labels (D25). |
| **Today, "Your unfinished drafts"** (4.2 item 2) | The participant's name, and the note date under it. | "Tap to continue": opens the caller's working copy, which is the draft form or the edit form for a pending edit. | Shown only when the caller has a draft or pending edit that is **not** on today's list (one from an earlier day, or for a participant archived since). It comes before the search box, and search does not filter it. It has no status line and no tag. |

The rows look the same to a **worker and a manager**. If someone else has a draft, the row says so in words (`Draft · Alex P. · started 9:14 am`), whatever the role. The difference is only on the next page: a worker sees "Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex can finish it.", and a manager sees the draft read-only, with Discard (4.2).

**Data each row uses** (6.3):

| Field | Used for |
|---|---|
| `givenName`, `familyName` | The row title, "Jane Citizen". The order comes from the server and is never re-sorted on the client. |
| `note === null` | No status line at all |
| `note.status` | `Draft` or `Submitted` |
| `note.isMine`, `note.authorDisplayName` | `You` or `Alex P.` |
| `note.savedAtUtc` | "saved 9:42 am". Present only on the caller's own draft. |
| `note.startedAtUtc` | "started 9:14 am" on someone else's draft |
| `note.firstSubmittedAtUtc` | "4:12 pm" on a submitted note |
| `note.isFlagged` | The `Flagged` tag, shown **only when `status` is Submitted** |
| `today.date` | The date part of every main-list link (server Melbourne date, never the device clock; A33) |
| drafts: `noteDate`, `kind` | The drafts-row date line, and the drafts-row destination |

---

## Best practice

**Making the whole row the target, and how to mark it up**

- In GOV.UK's cross-government research on the task list (a list of rows, each with a name and a status), teams kept seeing users try to click the status instead of the link text. The fix was to make the whole row a link. The task name stays the only link text, and the status is tied to it with `aria-describedby`. GOV.UK says this still needs testing to confirm that "the benefits of linking the whole task row outweigh the risks of accidental clicking". **[Research]** (qualitative synthesis with no numbers) + **[Convention]** https://designnotes.blog.gov.uk/2023/12/15/working-as-a-community-to-iterate-the-task-list-pattern/ · https://design-system.service.gov.uk/components/task-list/
- Wrapping a whole block in one `<a>` makes all of its text the link's accessible name. That makes it verbose in screen-reader link lists, and you can no longer select the text. Adrian Roselli recommends linking only the title and stretching the clickable area over the block with a pseudo-element. He applies the same pattern to clickable table rows, because otherwise "everything in the row announces as clickable". **[Opinion]** (expert accessibility practitioner) https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html
- Heydon Pickering describes the same pseudo-element technique and names its drawback: the overlay makes text inside the block hard to select. He uses `:focus-within` to style the whole block when its link has focus. **[Opinion]** (expert practitioner) https://inclusive-components.design/cards/
- An `<a>` may contain flow content but **no interactive descendants**. A row link therefore cannot hold a button, tick box or second link. **[Standard]** https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-a-element
- Activating a row changes the URL, so the row is an `<a href>`, not a `<button>` and not a `listbox` option. The APG listbox pattern is for *choosing* options, not for navigating. Bad ARIA is worse than no ARIA. **[Standard]** https://www.w3.org/WAI/ARIA/apg/patterns/listbox/ · https://www.w3.org/WAI/ARIA/apg/practices/read-me-first/
- Link purpose can come from the link text together with its enclosing list item (technique H77). A name-only link inside a `<li>` that holds the status meets SC 2.4.4. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html
- For speech-input users, "a best practice is to have the text of the label at the start of the name" (SC 2.5.3). A link whose name is just "Jane Citizen" matches exactly what a speech user says. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- **Target size.** WCAG 2.2 SC 2.5.8 sets a 24 × 24 CSS px minimum. Undersized targets stacked with small gaps fail, because their 24 px spacing circles overlap. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html. NN/g recommends at least 1 cm × 1 cm. **[Research]** https://www.nngroup.com/articles/touch-target-size/. Material 3 list items are 56 / 72 / 88 dp tall for one, two and three lines. **[Convention]** https://m3.material.io/components/lists/overview (the page did not render as text during this research, so the figures come from a secondary summary and are unverified). Grow2Notes sets 44 × 44 px (A32).
- **Focus not obscured.** A focused item must not be entirely hidden by author content such as a sticky header. `scroll-padding` is a recognised way to pass (SC 2.4.11). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html

**Status: words, tags and icons**

- Status must not rely on colour alone (SC 1.4.1). Grow2Notes already requires "Status is always given in words, never by colour alone" (A32, 4.0). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- "A text label must be present alongside an icon to clarify its meaning". Apart from a few such as home, print and search, universal icons are rare. **[Research]** (NN/g usability testing, 2014) https://www.nngroup.com/articles/icon-usability/
- GOV.UK Tag: use the "smallest number of statuses", and it can be correct for something with no tag to have a meaning ("if something does not have a tag, that means it's incomplete"). Do not make a tag a link or button. Use adjectives. Keep colours consistent and never use colour alone. Tags moved from uppercase to sentence case because "uppercase text can be harder to read". **[Convention]** + **[Research]** (GOV.UK research, not quantified) https://design-system.service.gov.uk/components/tag/
- GOV.UK task list: show "Completed" as plain text with no background, so that more attention goes to the rows that still need action. **[Convention]** informed by research (link above)
- NHS Tag: the same rules ("Do not add links", adjectives, fewest statuses). In NHS research, tags helped users "save time as they could see the information they needed quickly". **[Research]** (NHS, not quantified) + **[Convention]** https://service-manual.nhs.uk/design-system/components/tag
- AgDS Status badge: at most three words, and not interactive. **[Convention]** https://design-system.agriculture.gov.au/components/status-badge
- NN/g defines indicators as contextual, conditional and passive. They should sit next to the thing they describe, and each one should earn its place because indicators add clutter. **[Research]** (NN/g guidance, not a controlled study) https://www.nngroup.com/articles/indicators-validations-notifications/

**Scannability**

- People fixate on the start of lines. In the "bypassing" pattern, they skip the opening words when several lines in a list begin the same way. "If users see only the first 2 words, they should still get the gist." **[Research]** (NN/g eyetracking) https://www.nngroup.com/articles/f-shaped-pattern-reading-web-content/ · https://www.nngroup.com/articles/text-scanning-patterns-eyetracking/
- UIs with weak clickability signifiers needed more user effort than UIs with strong ones. **[Research]** (NN/g eyetracking, 2017) https://www.nngroup.com/articles/flat-ui-less-attention-cause-uncertainty/
- On iOS, a trailing chevron (the "disclosure indicator") tells people that tapping the row opens the next level. **[Convention]** https://developer.apple.com/design/human-interface-guidelines/lists-and-tables (the page did not render as text, so the wording is paraphrased)
- Australian Government Style Manual: write "am" and "pm" in lower case with a non-breaking space after the number ("6:30 pm"), and write noon and midnight in words. **[Convention]** (Australian Government style standard) https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time

**Long names**

- Leave room to display long names, and do not assume a name order or a sort order that works for every culture. **[Convention]** (W3C Internationalization guidance, non-normative) https://www.w3.org/International/questions/qa-personal-names
- Text must stay readable and fully visible at 200% text size (SC 1.4.4), at 320 CSS px width (SC 1.4.10) and with increased text spacing (SC 1.4.12). A fixed row height or a one-line ellipsis breaks all three. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/reflow.html · https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- iOS Contacts sorts by one part of the name and shows that part in bold, so the sort order is visible. **[Convention]** (documented by third parties, not by Apple in this research) https://www.howtogeek.com/704346/how-to-change-contact-name-order-on-iphone-or-ipad/

**List semantics and structure**

- A list of items must be marked up as a list so that the structure reaches assistive technology (SC 1.3.1). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html
- Safari and VoiceOver remove list semantics from a `<ul>` styled with `list-style: none`, unless the list is inside `<nav>`. The fix is `role="list"`. This still applied at the last update in January 2023. **[Convention]** (documented browser behaviour) https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- 71.6% of screen-reader users navigate a long page by its headings (WebAIM Screen Reader User Survey #10, 1,539 respondents, December 2023 to January 2024). Only 4.8% start with the links list. **[Research]** https://webaim.org/projects/screenreadersurvey10/
- A status message that appears without focus moving must be exposed through a live region (SC 4.1.3). Passive changes to a list are not status messages. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

**Stability**

- Content that moves after it has loaded causes mis-clicks, which is why Cumulative Layout Shift (CLS) exists as a metric. Reserve space, or render in one pass. **[Convention]** (Google web.dev) https://web.dev/articles/cls

---

## Recommendation for Grow2Notes

### Anatomy

```
Phone, 360 px wide, default text size

  Today · Thursday 1 October                      <h1> (header component)

  Your unfinished drafts                          <h2>, only when needed
 +-----------------------------------------------+
 | Sam Nguyen                                  > |  name = the link text
 | Wednesday 30 September 2026                   |  note date
 +-----------------------------------------------+

  Find a participant  [_______________________]   search component
                                                  <h2 class=visually-hidden>Participants</h2>
 +-----------------------------------------------+
 | Jane Citizen                                > |  link text (600 weight)
 | Draft · You · saved 9:42 am                   |  status line, aria-describedby target
 +-----------------------------------------------+
 | Priya Kumar                                 > |  no note: name only
 +-----------------------------------------------+
 | Lee Oakes                                   > |
 | Draft · Alex P. · started 9:14 am             |
 +-----------------------------------------------+
 | Tom Ward                                    > |
 | Submitted · Alex P. · 4:12 pm  [Flagged]      |  tag: words + border, not a count
 +-----------------------------------------------+
   ^ the whole row, edge to edge, is one link target
```

Each row has three parts, in DOM order:

1. **Name**, the only link text: `Jane Citizen` (given name then family name, as in every other place the app shows a name: 3.9, 4.2, 4.3).
2. **Detail line**, which is not part of the link text and is linked to the link by `aria-describedby`:
   - main list: the status line, only when a note exists, followed by the `Flagged` tag when it applies;
   - drafts section: the note date.
3. **Chevron**, a decorative SVG with `aria-hidden="true"`, vertically centred at the trailing edge.

### Behaviour

- **The whole row is one link.** The link's `::after` pseudo-element covers the whole `<li>`, so a tap on the name, the status, the tag, the chevron or empty space goes to the same place. This follows GOV.UK's research on users clicking statuses. There is only one action per row, so nothing can be tapped by mistake inside it.
- **Main-list destination:** the note route for `(participantId, today.date)`, for example `/participants/{id}/notes/2026-10-01`. The route names are illustrative, but URLs must hold only IDs and dates (4.0). That page fetches the note and picks the form, the blocked message, the manager's read-only draft or the read view. **The row never decides the destination from its cached status.** The status may be a few minutes old (Today refreshes only on load and focus, 6.8), and the destination's fresh fetch is the authority.
- **Drafts-section destination:** straight to the caller's working copy for `(participantId, noteDate)`. For a draft that is the form. For a pending edit it is the edit form, "Editing submitted note (version n)" (4.3).
- **Order:** the server's order (family name, then given name, A5). The client only filters, for search, and never re-sorts. A status change never moves a row.
- **Drafts-section order:** family name, then given name, then note date with the oldest first. This applies A5 "everywhere". The client builds the list from `/api/me/drafts` and drops entries where `noteDate === today.date` and the participant is on today's list (6.3).
- **Search** (owned by the search component) filters only the main list. The drafts section sits above the search box and is not filtered.
- **One render pass.** The drafts section and the main list render only when both `/api/today` and `/api/me/drafts` have loaded, so the drafts section can never appear above rows that are already showing and push them down under a finger.
- **Refresh:** TanStack Query refetches on mount and on window focus (6.8). New data replaces the rows silently: no animation, no announcement, and no change of scroll position or focus.
- **After Submit** (4.3): the form drops the cached `today` and `me/drafts` queries (`removeQueries`), then navigates to Today, so the row can never show "Draft" for a moment and then change. Today loads fresh, usually in under a second, so no loading text appears. The row then shows Submitted, as 4.3 requires.
- **Returning to Today** by "< Today", the browser Back button or after Submit: focus moves to the link of the row the user opened, if it is still on the list. Calling `focus()` scrolls it into view. The row key is held **in memory only** (a module variable, never storage; D22, 9.6). If the row has gone, for example an earlier-day draft that was just submitted, focus is left alone. If the success-message component ("Note for Jane Citizen submitted") moves focus itself, that rule wins.
- **No hover-only content**, tooltips, long-press, swipe actions or inline buttons. A row has one action, and any other action belongs on the destination page.

### States

| State | What it looks like | Notes |
|---|---|---|
| **Default, no note** | Name and chevron, with no detail line. | The absence of a status means "no note yet" (A17, D25), as GOV.UK's Tag guidance allows. Never write "Not started". |
| **Default, with note** | Name, status line and chevron. The status word ("Draft" or "Submitted") is semibold, and the rest is regular. | The status word comes first, so a scanning eye picks it up even in the bypassing pattern. |
| **Flagged** (Submitted, current version flagged) | `Flagged` tag after the time: sentence case, 1 px border, pale amber fill, dark text. | It never appears on a Draft row, because a flag on a draft is not seen (3.6) and other workers see status only (A9). It must not look like the manager nav count badge (see notification-badge.md). |
| **Hover** (pointer devices only, `@media (hover: hover)`) | Row background `--colour-row-hover`, and the name underlined. | Not on touch, where hover gets stuck after a tap. |
| **Focus** (`:focus-visible`) | A 3 px solid `--colour-focus` outline around the **whole row**, drawn on the link's `::after` and inset by 3 px so the next row cannot clip it. | The outline is moved from the name to the row, not removed. It needs at least 3:1 against both the row and hover backgrounds (SC 1.4.11). |
| **Active** (pressed) | Row background `--colour-row-active`, a step darker than hover. | No scale, no ripple, no animation. |
| **Visited** | The same as default. | All rows look alike. A visited colour would mean nothing. |
| **Disabled** | **Never.** | Every row opens something, including someone else's draft, which opens the explanation (4.2). A disabled row cannot explain why it is disabled. |
| **Read-only** | Not a row state. | Read-only happens on the destination page (the manager's view of a draft, or the read view). |
| **Error** (row) | Not a row state. | The row cannot fail. Load errors are list states (below). |
| **Loading** (first load) | The header and search show at once. The list area shows nothing for the first second, then `Loading participants…` as plain text. | The text goes into a `role="status"` container that is in the DOM from the first render. Below 1 s, show no indicator. No skeleton rows, which would also add layout shift. *This copy is not in design.md; see Open questions.* |
| **Load failed** | `Couldn't load the participant list. Check your connection and try again.` and a **Try again** button, which refetches. | Use the same status container. The button sits outside it so that it is not re-announced. 401s go through the global sign-in handling. *This copy is not in design.md.* |
| **Background refresh failed** | Nothing changes. The rows that are already shown stay. | Each destination fetches its own fresh data, so a stale row cannot cause a wrong action. |
| **Empty: no participants** | `No participants yet.` Managers also see an **Add participant** link to Manage > Participants. | 4.2. A worker sees only the sentence. |
| **Empty: no search match** | `No participant matches 'xyz'`, with the user's text shown as plain text. | 4.2. The search component owns its live region. The drafts section stays visible. |
| **Drafts section empty** | The whole section, including its heading, is not rendered. | 4.2: "shown only when…" |

### Phone vs laptop

- **Phone (single column, 4.0):** the rows are full-bleed. The list extends into the page's side gutter and each row pads its text back to the gutter, so the tap target runs from screen edge to screen edge while the text lines up with the rest of the page. The name is on line 1 and the detail on line 2. Long names and status lines wrap. They are **never truncated**.
- **Laptop:** the same stacked two-line row. The list column is capped at `max-inline-size: 40rem` (about 640 px) and aligned with the page content, so the chevron and status stay near the name, and the hover state appears. Do not spread the row into columns (Name | Status | Author | Time) across a wide screen. That separates a status from its name and invites table features nobody asked for. **[Opinion]**
- **200% text and 320 px width:** the row grows taller and wraps. Each status part ("Draft", "Alex P.", "started 9:14 am") is kept together, so lines break only at the separators. The tag drops to its own line if it has to. Nothing scrolls sideways (SC 1.4.10).

### Visual specification

All sizes are in `rem`, so they follow the user's text size.

| Part | Specification |
|---|---|
| Row | `min-block-size: 3.5rem` (56 px at default text size, above the 44 px of A32); `padding-block: 0.75rem`; 1 px divider between rows, plus one above the first and below the last |
| Name | `1.125rem`, weight 600, line-height 1.35, `--colour-text`. No underline at rest. `overflow-wrap: anywhere` (only breaks a long unbroken name), `hyphens: manual` (never auto-hyphenate names) |
| Status line | `1rem`, weight 400, line-height 1.4, `--colour-text-secondary` at **4.5:1 or better** (not light grey; tired readers and readers of English as a second language). Status word weight 600. |
| Separator | ` · ` in `--colour-text-secondary`, with a non-breaking space before the dot so a line never starts with "·" |
| Flagged tag | `1rem` (not smaller), weight 600, `padding: 0 0.375rem`, `border: 1px solid --colour-tag-flag-border`, `border-radius: 0.25rem`, background `--colour-tag-flag-bg` (pale amber), text `--colour-text` at 4.5:1 or better. **Not red**: red is kept for errors and the "Not saved" banner. **Not** a filled pill, so it cannot be confused with the count badge. |
| Chevron | `1.25rem` square, 2 px stroke, `currentColor` = `--colour-icon` at **3:1 or better** against the row background. It is the main signifier that the row can be tapped, so treat it as required for identifying the component (SC 1.4.11). |
| Focus | `outline: 3px solid var(--colour-focus); outline-offset: -3px` on `.link::after` |

### Exact copy

| Where | Text | Source |
|---|---|---|
| Name | `Jane Citizen` | 3.9, 4.2 |
| Own draft | `Draft · You · saved 9:42 am` | 4.2 |
| Someone else's draft | `Draft · Alex P. · started 9:14 am` | 4.2 |
| Submitted | `Submitted · Alex P. · 4:12 pm` | 4.2 |
| Submitted by the caller | `Submitted · You · 4:12 pm` (recommended; design.md shows only another author's example; see Open questions) | follows the 4.2 "You" pattern |
| Tag | `Flagged` | 4.2 |
| Drafts heading | `Your unfinished drafts` | 4.2 |
| Drafts-row detail | `Wednesday 30 September 2026` (note date) | 4.2 "participant and note date" |
| Hidden heading for the main list | `Participants` (visually hidden) | the Manage menu's term (4.0) |
| No participants | `No participants yet.` and, for managers, `Add participant` | 4.2 |
| No match | `No participant matches 'xyz'` | 4.2 |
| Loading | `Loading participants…` | **proposed, not in design.md** |
| Load failed | `Couldn't load the participant list. Check your connection and try again.` and `Try again` | **proposed, not in design.md** |

Times are Melbourne time, in the form `9:42 am`, lower case, with a non-breaking space before am/pm (Style Manual). Dates use the form `Wednesday 30 September 2026`, with no comma. A time of exactly 12:00 is shown as `12:00 pm` to match the rest of the app. The Style Manual prefers "noon", and that is left to the shared time formatter.

### Accessibility

**Semantics**

- The heading structure is `<h1>` Today · date, then `<h2>Your unfinished drafts</h2>` (when shown), then a **visually hidden** `<h2>Participants</h2>` before the search box. Without the hidden heading, a screen-reader user moving by headings would hear the participant list as part of "Your unfinished drafts" (SC 1.3.1, 2.4.6). It adds nothing to the screen.
- Each list is a `<ul role="list">`. The `role` is needed only because Safari drops list semantics under `list-style: none`. Each row is an `<li>` holding one `<a href>`, rendered by React Router's `<Link>`.
- The link text is the name only. The detail line is a `<p>` with an `id`, referenced by the link's `aria-describedby`, as in the GOV.UK task list. Inside it, each "·" is `aria-hidden="true"` and is followed by a visually hidden `", "`, so screen readers pause between parts instead of reading "middle dot" or running the words together. How each reader voices "·" by default is unverified, so remove the guesswork.
- The chevron SVG has `aria-hidden="true"` and `focusable="false"`.
- Times use `<time dateTime="2026-09-30T23:42:00Z">9:42&nbsp;am</time>`. This is harmless and machine-readable.
- **ARIA used:** `aria-describedby`, `aria-hidden`, and `role="list"` (the Safari fix). There is no `aria-label` (it would break SC 2.5.3 matching), no `aria-live` on the list, and no `listbox`, `grid` or `menu` roles.

**Keyboard**

- Each row is one Tab stop, in visual order: drafts rows, then the search box, then participant rows. Enter follows the link. Space scrolls the page, which is normal for links, so do not add a Space handler.
- This is not a composite widget, so there is no roving tabindex and there are no arrow-key handlers. That is correct for a list of links. With roughly 20 to 80 participants, the search box is the shortcut.
- The focus outline covers the whole row. If the top bar or search is ever made sticky, set `html { scroll-padding-block-start: <its height> }` so a focused row is never hidden behind it (SC 2.4.11).

**What screen readers say** (expected; confirm in testing)

- NVDA or JAWS with Chrome, tabbing to a row: "Jane Citizen, link", then the description "Draft, You, saved 9:42 am".
- A Flagged row: "Tom Ward, link … Submitted, Alex P., 4:12 pm, Flagged".
- A no-note row: "Priya Kumar, link", with no description.
- VoiceOver on iOS, swiping: "Jane Citizen, link", then the description as a hint after a pause if hints are on. The next swipe reads the status text. Some users will hear the status twice, which GOV.UK's task list also accepts.
- Entering the list, readers say something like "list, 24 items". That is the number of participants, not a note count, so it does not conflict with D25 or D26. Do not remove list semantics to avoid it.
- **Nothing is announced** when a background refresh changes a row's status. Only Loading, Load failed and No match are announced, through status containers that exist from the first render (SC 4.1.3).

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships; 1.3.2 Meaningful Sequence (name, then status, in the DOM); 1.4.1 Use of Color; 1.4.3 Contrast (Minimum); 1.4.4 Resize Text; 1.4.10 Reflow; 1.4.11 Non-text Contrast (focus, chevron); 1.4.12 Text Spacing; 1.4.13 Content on Hover or Focus (none added); 2.1.1 Keyboard; 2.4.3 Focus Order; 2.4.4 Link Purpose (In Context) (H77 plus the description); 2.4.6 Headings and Labels; 2.4.7 Focus Visible; 2.4.11 Focus Not Obscured (Minimum); 2.5.3 Label in Name; 2.5.8 Target Size (Minimum) (whole row, at least 56 px tall); 3.2.4 Consistent Identification; 4.1.2 Name, Role, Value; 4.1.3 Status Messages.

**Test:** run Playwright with `@axe-core/playwright` on all list states. Then test by hand with NVDA and Chrome, VoiceOver and Safari on iOS, and TalkBack and Chrome. Check a 320 px viewport, 200% text, Windows contrast themes (forced colours), and keyboard-only use.

### Implementation notes (React 19 + native HTML + CSS Modules)

No React Aria is needed. A link, a list, a paragraph and an SVG are all native.

```tsx
// src/today/ParticipantRow.tsx
import { useId, type ReactNode, type Ref } from 'react';
import { Link } from 'react-router';
import styles from './ParticipantRow.module.css';

type Props = {
  to: string;                 // IDs and dates only (4.0)
  name: string;               // "Jane Citizen"
  detail?: ReactNode;         // status line or note date; omitted when there is no note
  onOpen?: () => void;        // remembers the row for focus on return (memory only)
  linkRef?: Ref<HTMLAnchorElement>;
};

export function ParticipantRow({ to, name, detail, onOpen, linkRef }: Props) {
  const detailId = useId();
  return (
    <li className={styles.row}>
      <div className={styles.text}>
        <Link
          ref={linkRef}
          to={to}
          className={styles.link}
          onClick={onOpen}
          aria-describedby={detail ? detailId : undefined}
        >
          {name}
        </Link>
        {detail && <p id={detailId} className={styles.detail}>{detail}</p>}
      </div>
      <svg className={styles.chevron} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <path d="M7.5 4.5 13 10l-5.5 5.5" fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
    </li>
  );
}
```

```tsx
// src/today/NoteStatus.tsx  (main-list detail line)
import a11y from '../styles/a11y.module.css';
import styles from './ParticipantRow.module.css';
import { melbourneTime } from '../lib/time';
import type { TodayNote } from '../api/types';

const Sep = () => (
  <>
    <span className={styles.sep} aria-hidden="true">{' ·'}</span>
    <span className={a11y.visuallyHidden}>,</span>{' '}
  </>
);

export function NoteStatus({ note }: { note: TodayNote }) {
  const isDraft = note.status === 'Draft';
  const who = note.isMine ? 'You' : note.authorDisplayName;
  const iso = isDraft
    ? (note.isMine ? note.savedAtUtc ?? note.startedAtUtc : note.startedAtUtc)
    : note.firstSubmittedAtUtc!;
  const verb = isDraft ? (note.isMine ? 'saved ' : 'started ') : '';
  return (
    <>
      <span className={`${styles.part} ${styles.state}`}>{isDraft ? 'Draft' : 'Submitted'}</span><Sep />
      <span className={styles.part}>{who}</span><Sep />
      <span className={styles.part}>{verb}<time dateTime={iso}>{melbourneTime(iso)}</time></span>
      {!isDraft && note.isFlagged && (
        <>
          <span className={a11y.visuallyHidden}>,</span>{' '}
          <span className={styles.tag}>Flagged</span>
        </>
      )}
    </>
  );
}
```

```css
/* ParticipantRow.module.css */
.list {
  list-style: none;
  margin: 0;
  padding: 0;
  margin-inline: calc(-1 * var(--page-gutter));   /* phone: full-bleed rows */
  border-block-start: 1px solid var(--colour-divider);
}
@media (min-width: 48rem) {
  .list { margin-inline: 0; max-inline-size: 40rem; }
}
.row {
  position: relative;                 /* containing block for .link::after */
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-block-size: 3.5rem;
  padding-block: 0.75rem;
  padding-inline: var(--page-gutter);
  border-block-end: 1px solid var(--colour-divider);
}
.text { flex: 1; min-inline-size: 0; }
.link {
  font-size: 1.125rem;
  font-weight: 600;
  line-height: 1.35;
  color: var(--colour-text);
  text-decoration: none;
  overflow-wrap: anywhere;
  hyphens: manual;
}
.link:visited { color: var(--colour-text); }
.link::after {                        /* stretches the target over the whole row */
  content: "";
  position: absolute;
  inset: 0;
}
.link:focus-visible { outline: none; }            /* moved, not removed: */
.link:focus-visible::after {
  outline: 3px solid var(--colour-focus);
  outline-offset: -3px;
}
.detail {
  margin: 0.125rem 0 0;
  font-size: 1rem;
  line-height: 1.4;
  color: var(--colour-text-secondary);
}
.part { white-space: nowrap; }        /* wrap only at separators */
.state { font-weight: 600; color: var(--colour-text); }
.tag {
  display: inline-block;
  padding: 0 0.375rem;
  border: 1px solid var(--colour-tag-flag-border);
  border-radius: 0.25rem;
  background: var(--colour-tag-flag-bg);
  color: var(--colour-text);
  font-weight: 600;
  line-height: 1.4;
}
.chevron { flex: none; inline-size: 1.25rem; block-size: 1.25rem; color: var(--colour-icon); }

@media (hover: hover) {
  .row:has(.link:hover) { background: var(--colour-row-hover); }
  .row:has(.link:hover) .link { text-decoration: underline; text-underline-offset: 0.15em; }
}
.row:has(.link:active) { background: var(--colour-row-active); }

@media (forced-colors: active) {
  .tag { border-color: CanvasText; }   /* the fill is dropped; the border keeps it a tag */
}
```

`:has()` has been Baseline (widely available) since December 2023 (MDN). If a browser lacks it, it loses only the hover and active tint: the target, focus and status still work.

**Time and date formatting.** This belongs in a shared helper, so every screen says "9:42 am" the same way:

```ts
// src/lib/time.ts
const timeFmt = new Intl.DateTimeFormat('en-AU', {
  timeZone: 'Australia/Melbourne', hour: 'numeric', minute: '2-digit', hour12: true,
});
export function melbourneTime(isoUtc: string): string {
  const p = Object.fromEntries(timeFmt.formatToParts(new Date(isoUtc)).map(x => [x.type, x.value]));
  return `${p.hour}:${p.minute} ${String(p.dayPeriod).toLowerCase().replace(/\./g, '')}`;
}
// noteDate is a date-only "YYYY-MM-DD": format it in UTC so it can never shift a day
const dateFmt = new Intl.DateTimeFormat('en-AU', {
  timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
});
export function longDate(noteDate: string): string {
  const p = Object.fromEntries(dateFmt.formatToParts(new Date(`${noteDate}T00:00:00Z`)).map(x => [x.type, x.value]));
  return `${p.weekday} ${p.day} ${p.month} ${p.year}`;   // "Wednesday 30 September 2026", no comma
}
```

The string is built from parts because ICU output varies between engines and versions: the space before am/pm, the case, and whether a comma follows the weekday (unverified per engine). Building it guarantees the design's exact form.

**Loading the two lists together:**

```tsx
const today  = useQuery({ queryKey: ['today'], queryFn: getToday });
const drafts = useQuery({ queryKey: ['me', 'drafts'], queryFn: getMyDrafts });
const ready  = today.isSuccess && drafts.isSuccess;      // one render pass, no layout shift
const failed = today.isError || drafts.isError;           // either failing = "Load failed"
```

If either query fails, treat the whole load as failed. Showing the list without the drafts section would hide unfinished work without saying so.

**Focus on return (memory only):**

```ts
// src/today/lastOpened.ts: module scope; never localStorage or sessionStorage (D22, 9.6)
let lastOpened: string | null = null;            // `${participantId}/${noteDate}`
export const rememberOpened = (key: string) => { lastOpened = key; };
export const takeLastOpened = () => { const k = lastOpened; lastOpened = null; return k; };
```

In `Today`, after `ready`, call `takeLastOpened()` and `focus()` the matching link through a `Map` of refs. Do **not** use React Router's `<ScrollRestoration>`. It works only in data or framework mode, and it stores scroll positions in `sessionStorage`. That is not note data, but 9.6 and the delivery check keep that storage empty. Focusing the row scrolls it into view, which is enough.

**Other notes**

- Key rows by participant ID (main list) and by `participantId/noteDate` (drafts). Render every row: no virtualisation, no paging, no "show more". The list is small, and virtualisation breaks find-in-page, list counts and scroll position.
- Build hrefs from `today.date` (server), never `new Date()` on the device (A33).
- In the drafts section, choose the destination from `kind` (`draft` or `pendingEdit`). Do not add a visible word for it unless the owner agrees (see Open questions).
- Page titles stay generic ("Grow2Notes – Today"), and hrefs carry no names (4.0).

---

## Per-screen notes

**Today, main list (4.2 item 4)**

- This is the only place the status line appears. It shows four strings in total, `Draft · You · saved …`, `Draft · Alex P. · started …`, `Submitted · Alex P. · …` and the `Flagged` tag. A row with no note has no line.
- `Flagged` follows the **current version** (4.2). If a later edit removes the tick, the tag goes, even though the note stays in managers' To review list (A15). That difference is correct, and the Flagged screen shows it ("Flag removed in a later edit", 4.6).
- A manager sees exactly the same row as a worker, even for a draft the manager can read. The extra powers are shown on the destination page, so the list stays one simple pattern.
- Names show given name first, while the sort is by family name (A5). Do not bold the family name or add A–Z letter headings: they add a visual rule people have to learn, and the search box covers finding someone. **[Opinion]** (see Tensions)

**Today, "Your unfinished drafts" (4.2 item 2)**

- The same row component, with the note date as the detail line and no status line, tag or time.
- For a participant archived since the draft started, the row stays, because their draft can still be submitted (3.7). Nothing marks the archive.
- After an earlier-day draft is submitted, the row disappears from this section on return, and if it was the last one, so does the whole section. Focus is not forced anywhere in that case. The success message does the announcing.

**Coordination with neighbouring components**

- **Search box:** owns the filter, its label "Find a participant", and the live region for "No participant matches 'xyz'". This component renders the empty-state text inside the list area.
- **Success message after Submit:** owns "Note for Jane Citizen submitted" and its announcement. This component only returns focus to the row, and gives way if that component moves focus itself.
- **Notification badge:** the `Flagged` tag must stay visually distinct from the count pill (notification-badge.md, Colour).

---

## Anti-patterns to avoid

- `<div onClick>` or `<li onClick>` rows, or a `<button>` that navigates with `navigate()`. These lose the link role, opening in a new tab, the URL preview, and keyboard and voice access.
- Wrapping the whole row's contents in the `<a>`, so the accessible name becomes "Jane Citizen Submitted · Alex P. · 4:12 pm Flagged". This is verbose in link lists and weak for speech input.
- Any interactive element inside the row (Discard, Edit, a tick box, a second link). This is invalid inside `<a>`, and it is not in the design.
- Swipe actions, long-press menus or hover tooltips. These are hidden, gesture-only, and not in the design.
- **Disabling** rows for someone else's draft, or greying them out. The design opens an explanation instead (4.2).
- Status as a coloured dot, an icon on its own (a lock, a tick, a pencil), or colour-coded pills for Draft and Submitted. Use words, and use a tag only for `Flagged`.
- Uppercase status text or tags.
- Showing `Flagged` on a **Draft** row. This leaks draft content to other workers (A9), and managers do not act on draft flags (3.6).
- Adding "Not started", "Missing", "Overdue", "3 of 12 done", progress bars or any count. These are excluded by D25 and A17.
- Re-sorting by status, pinning your own drafts to the top, or grouping by Draft/Submitted. The order is family name (A5), and only the separate drafts section sits above.
- Listing today's own drafts in "Your unfinished drafts" as well as in the main list (4.2 says only drafts that are *not* on today's list).
- Relative times ("5 minutes ago"). The design specifies clock times, and the list refreshes only on load and focus, so relative times would go stale on screen.
- Truncating names or statuses with an ellipsis or `line-clamp`, or using fixed row heights.
- `aria-live` on the list, or announcing background status changes.
- `role="listbox"`, `grid`, `menu` or `treegrid` on the list, or a roving tabindex.
- Removing `role="list"` and relying on `<ul>` alone under `list-style: none`.
- Rendering the drafts section after the main list has loaded, which pushes the rows down.
- Choosing the destination from the row's cached status.
- Storing the last-opened row, scroll position or search text in `localStorage` or `sessionStorage`, including through `<ScrollRestoration>`.
- Using the device clock for the link date.
- Virtualised or infinitely scrolled lists.
- Hover styles without `@media (hover: hover)`, which stick on phones after a tap.
- Removing the link's focus outline without drawing one around the row.

---

## Tensions with decisions

These are recorded once, with the evidence. There is no push to change either.

1. **"Your unfinished drafts" also lists pending edits (4.2, A17).** Elsewhere the design separates a *draft* (3.4) from a *pending edit* on a submitted note (3.5, A10), and the drafts-section rows show only a name and a date. NN/g's "Consistency and standards" heuristic says users "should not have to wonder whether different words, situations, or actions mean the same thing" **[Research]** (heuristic, not a study) https://www.nngroup.com/articles/consistency-and-standards/. The impact is small: the destination's header ("Editing submitted note (version 2)", 4.3) makes it clear on arrival. This document builds the heading and rows exactly as written.
2. **Sorted by family name, shown given name first (A5 and the app-wide "Jane Citizen" form).** Nothing on the row shows the sort key, so a person scanning for "Jane" in a family-name-sorted list may not see the order. iOS Contacts handles this by bolding the sort-key part of the name **[Convention]** (third-party documentation). No research was found on whether this matters for lists of under about 100 names with a search box. The search box (4.2) already covers finding someone, and this document keeps A5 and the name form as they are.

---

## Sources

- GOV.UK Design System, Task list: https://design-system.service.gov.uk/components/task-list/
- GOV.UK Design Notes, "Working as a community to iterate the task list pattern" (15 Dec 2023): https://designnotes.blog.gov.uk/2023/12/15/working-as-a-community-to-iterate-the-task-list-pattern/
- GOV.UK Design System, Tag: https://design-system.service.gov.uk/components/tag/
- NHS digital service manual, Tag: https://service-manual.nhs.uk/design-system/components/tag
- Australian Government Agriculture Design System (AgDS), Status badge: https://design-system.agriculture.gov.au/components/status-badge
- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- Adrian Roselli, "Block Links, Cards, Clickable Regions, Rows, Etc." (2020): https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html
- Heydon Pickering, Inclusive Components, "Cards": https://inclusive-components.design/cards/
- Scott O'Hara, "Fixing lists" / lists and Safari (2019, updated 2023): https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- HTML Living Standard, the `a` element: https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-a-element
- WAI-ARIA APG, Listbox pattern: https://www.w3.org/WAI/ARIA/apg/patterns/listbox/
- WAI-ARIA APG, Read Me First: https://www.w3.org/WAI/ARIA/apg/practices/read-me-first/
- WCAG 2.2 Understanding 1.3.1 Info and Relationships: https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Understanding 1.4.12 Text Spacing: https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- WCAG 2.2 Understanding 2.4.4 Link Purpose (In Context): https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WebAIM Screen Reader User Survey #10 (2024): https://webaim.org/projects/screenreadersurvey10/
- NN/g, Aurora Harley, "Icon Usability" (2014): https://www.nngroup.com/articles/icon-usability/
- NN/g, Aurora Harley, "Touch Targets on Touchscreens" (2019): https://www.nngroup.com/articles/touch-target-size/
- NN/g, Kara Pernice, "F-Shaped Pattern of Reading on the Web" (2017): https://www.nngroup.com/articles/f-shaped-pattern-reading-web-content/
- NN/g, Kara Pernice, "Text Scanning Patterns: Eyetracking Evidence" (2019): https://www.nngroup.com/articles/text-scanning-patterns-eyetracking/
- NN/g, "Flat UI Elements Attract Less Attention and Cause Uncertainty" (2017): https://www.nngroup.com/articles/flat-ui-less-attention-cause-uncertainty/
- NN/g, Kim Flaherty, "Indicators, Validations, and Notifications" (2024 update): https://www.nngroup.com/articles/indicators-validations-notifications/
- NN/g, "Consistency and Standards" (heuristic 4): https://www.nngroup.com/articles/consistency-and-standards/
- W3C Internationalization, "Personal names around the world": https://www.w3.org/International/questions/qa-personal-names
- Apple Human Interface Guidelines, Lists and tables: https://developer.apple.com/design/human-interface-guidelines/lists-and-tables
- Material Design 3, Lists: https://m3.material.io/components/lists/overview
- How-To Geek, iPhone contact name order (iOS Contacts sort-key display): https://www.howtogeek.com/704346/how-to-change-contact-name-order-on-iphone-or-ipad/
- web.dev, Cumulative Layout Shift: https://web.dev/articles/cls
- MDN, `:has()`: https://developer.mozilla.org/en-US/docs/Web/CSS/:has
- React Router, ScrollRestoration: https://reactrouter.com/api/components/ScrollRestoration
