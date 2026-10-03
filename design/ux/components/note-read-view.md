# Submitted note read view

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Body text is 18 px (foundations.md), not 19 px. The note query key is `['notes', participantId, noteDate]`, not `['note', …]`. After Mark reviewed the record replaces the form in place and takes focus (no "marked reviewed" status message). participant-notes.md owns the read view for every route.

Component key: `note-read-view`. The read-only page for one note: every goal ticked or not ticked; for common items, Every note and the groups picked on that note (D47), each item ticked or not ticked; the Guided notes text, the flag and its reason, the author, the submitted time, "Past-day note, written on … by …", the "Edited" label and, for managers, the review status and comment, the **Version history** link and the review panel. Actions: **Edit** (author or manager) and **Mark reviewed** (manager, when the note is in To review). Defined by D7–D12, D15, D17–D20, D34, D35, D39, D44–D47, design.md 3.4–3.9, 4.2, 4.4, 4.5, 4.6, 6.3, 6.4, 11.3 and A3, A4, A11, A13–A15.

This component reuses three sibling components and does not restate them:

- **`TickListRead`** from `checkbox-list.md`: the read-only Goals and Common items lists ("Makes own breakfast, ticked").
- **`Tag` and `MetaLine`** from `status-tags.md`: the words Flagged, To review, Reviewed, Edited, Past-day note, and the "a · b · c" meta line.
- **The Discard dialog** from `confirm-dialog.md`, and the character count from `conditional-reveal.md`.

It owns the page layout, the order of everything on it, the Guided notes text block, the flag and review lines, where the actions sit, and the review panel's place and outcome.

---

## Where it's used

| Screen | Who | What it shows | What differs |
|---|---|---|---|
| **Read view** (4.4), reached from Today (a Submitted row, 4.2), Past notes (4.4) or a link | Everyone | The **current version** of a submitted note (6.3 `current`). Header, status lines, Goals, Common items, Guided notes. **Past notes** link. | **Edit** only for the author and managers. Managers also see To review / Reviewed with the comment, **Version history**, and the review panel when the note is To review (4.4 "Mark reviewed (manager, when the note is in To review)"). Workers never see review data (the API doesn't send it). |
| **Someone else's draft** (4.4 state, 4.2) | Worker | Status only, no content: "Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex can finish it." | No sections, no actions except navigation. |
| **Someone else's draft** (4.4 state, 4.2) | Manager | The draft's content, read-only, with "Draft · Alex P. · started 9:14 am" and **Discard draft**. | No Edit, no tags, no review. The flag shows as a ticked row, not the **Flagged** tag, because a flag on a draft alerts nobody (3.6). Guided notes may be empty. |
| **One version** (4.5), reached from Version history | Manager | One version in full, read-only: "Version 2 of 3", who saved it and when, its own flag and reason, its Goals, Common items (Every note and the groups picked in that version, D47) and Guided notes. | No actions at all: there is no restore and an old version can't be edited (3.8, 4.5). No note-level tags (Edited, To review, Reviewed, Past-day note): they describe the note, not this version. |
| **Flagged note review** (4.6), reached from **To review** | Manager | The read view, with the **review panel** underneath: "Comment (optional)", up to 500 characters, and **Mark reviewed**. | The flag reason is the reason the manager is here, so it sits at the top. If a later edit removed the tick, the top says "Flag removed in a later edit" and still shows the reason that was flagged. |
| **Flagged note review** (4.6), reached from **Reviewed** | Manager | The read view with "Reviewed by Jo Smith, …" and the comment. | No review panel. |

The same page component serves 4.4 and 4.6. Only the back link and where the flagged reason comes from differ (see Per-screen notes).

---

## Best practice

### Presenting a record that was once a form

- **Show a finished record as text, not as a form with its controls switched off.** HTML `readonly` doesn't apply to checkboxes, disabled controls are skipped by Tab and are exempt from contrast rules, and Roselli's tests found `aria-readonly` reliable in only 1 of 6 screen reader and browser pairs. His advice is to show values "as plain text instead of form fields". [Standard] https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly · [Research] (practitioner AT testing) https://adrianroselli.com/2024/11/avoid-read-only-controls.html (the full case is in `checkbox-list.md`)
- **Keep the form's structure so the reader recognises what the writer filled in.** GOV.UK's check-answers pattern groups answers under the same section names the user met, and its research says this lets users "clearly see that they have completed all the sections and that their data has been captured". [Research] (GOV.UK service teams; no sample size published) https://design-system.service.gov.uk/patterns/check-answers/
- **Key–value metadata belongs in a summary list (`<dl>`); sentences and lists don't.** GOV.UK and NHS use the summary list "as key-value pairs, such as metadata", and say not to use it for "tabular data or simple lists". The HTML spec defines `<dl>` as "name-value groups". [Convention] https://design-system.service.gov.uk/components/summary-list/ · https://service-manual.nhs.uk/design-system/components/summary-list · [Standard] https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element
  - Grow2Notes' metadata is already written as sentences in the daily report (11.3: "Written by Priya Nair · Submitted …", "Flagged for manager: …", "Reviewed by Jo Smith, …"). Using the same sentences on screen means the screen and the file say the same thing in the same words. This file therefore uses short lines, not a `<dl>`. [Opinion]
- **Actions that apply to the whole record sit with its title, not inside the content.** GOV.UK summary cards put "card actions" that apply to the whole card in the card's header. [Convention] https://design-system.service.gov.uk/components/summary-list/
- **Hide actions a person can never take; don't disable them.** HashiCorp Helios: "When a user does not have permissions, hide the related actions". Disabled controls can't be focused or explained. [Convention] https://helios.hashicorp.design/patterns/disabled-patterns
- **The final, deliberate step goes at the bottom, after the content it depends on.** GOV.UK check answers puts the send button at the end, after every answer. Design 4.6 puts the review panel "underneath" the note, and the API only accepts a review of the current version "so the manager has seen what is current" (6.4). [Convention] https://design-system.service.gov.uk/patterns/check-answers/

### Long text

- **Line length: aim for about 55–75 characters per line on a laptop.** Screen studies show reading speed rises with longer lines but comprehension peaks around 55 characters per line (Dyson & Haselgrove 2001). Shaikh & Chaparro (2005, n=20) found 95 characters fastest with no comprehension difference. A manager reading a flagged note needs to understand it, not skim it. [Research] (small samples, desktop monitors, 2001–2005) https://legible-typography.com/en/6-overview-of-research-typography
  - GOV.UK caps lines at "no more than 75 characters per line". [Convention] https://design-system.service.gov.uk/styles/layout/
  - WCAG 1.4.8 (AAA) caps blocks of text at 80 characters, because people with some reading or vision disabilities "have trouble keeping their place". [Standard] https://www.w3.org/WAI/WCAG22/Understanding/visual-presentation.html
  - CSS `ch` is the width of the zero, not an average letter, so `65ch` renders about 75–90 real characters. Count a real line. [Standard] (CSS Values 4, via the ui-ux-design corpus)
- **Line spacing at least 1.5 in paragraphs; text left-aligned, never justified.** WCAG 1.4.8 (AAA). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/visual-presentation.html
- **Body text size: GOV.UK uses 19 px with 25 px line height on both small and large screens.** It is the size a population-wide service settled on for mixed literacy and eyesight. [Convention] https://design-system.service.gov.uk/styles/type-scale/
- **Long text reads as well on a phone as on a computer.** NN/g (Moran, 2016; 276 participants) found "no practical differences in the comprehension scores" between phone and computer. Readers slowed down on hard text on phones to keep comprehension up. So don't shorten, collapse or summarise the note on phones. [Research] https://www.nngroup.com/articles/mobile-content/
- **Keep the writer's line breaks without HTML.** `white-space: pre-wrap` preserves spaces and newlines and still wraps long lines. It is Baseline widely available (since 2015). [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/white-space
- **Never render user text as HTML.** React escapes text in JSX. `dangerouslySetInnerHTML` with user content is "trivial" to turn into an XSS hole. [Standard] https://react.dev/reference/react-dom/components/common

### Dates and times

- **Use absolute dates and times for a record.** Cloudscape: absolute timestamps when "users need a specific date and time for when an event occurred". Relative times ("2 hours ago") go stale on a record that is kept for years. [Convention] https://cloudscape.design/patterns/general/timestamps/
- **Spell out months; truncate only where space is tight, like tables.** GOV.UK style: "4 June 2017", and "you can use truncated months like Jan, Feb" in tables. Full words are easier for people reading English as a second language. [Convention] https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- **`<time datetime>` gives the machine-readable value.** The spec: the element "represents its contents, along with a machine-readable form". It costs nothing and doesn't change what is read aloud. [Standard] https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-time-element

### Screen reader order and navigation

- **Headings are how most screen reader users move through a long page.** WebAIM Screen Reader Survey #10 (1,539 responses, Dec 2023–Jan 2024): 71.6% navigate a long page by headings first, and 88.8% find heading levels useful. [Research] https://webaim.org/projects/screenreadersurvey10/
- **DOM order must be the reading order** (SC 1.3.2). A two-column laptop layout reads one column top to bottom, then the next. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html
- **After a route change in a single-page app, focus a heading.** Gatsby's tests (Marcy Sutton, 2019; 5 sessions with screen reader, magnification, voice and switch users) found "Focusing on a heading was found to be the best experience" for screen reader users, and a visible focus outline helped voice and keyboard users. [Research] (n=5) https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- **Don't turn every section into a landmark.** A `<section>` becomes a `region` landmark only when it has an accessible name, and ARIA defines a region as content "sufficiently important that users will likely want to be able to navigate to the section easily". Headings already give section navigation. [Standard] https://www.w3.org/TR/wai-aria-1.2/#region · https://www.w3.org/WAI/ARIA/apg/practices/landmark-regions/
- **Move focus only after the user's own action, and only when the focused control disappears.** Otherwise announce through a status region that already exists in the DOM (SC 4.1.3, SC 2.4.3). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

### Print-like clarity on a phone

- **Let the user zoom.** A viewport meta with `user-scalable=no` or `maximum-scale` below 2 fails SC 1.4.4 (ACT rule "Meta viewport allows for zoom"). [Standard] https://www.w3.org/WAI/standards-guidelines/act/rules/b4f0c3/
- **Nothing sticky over the text.** Sticky headers and footers are the typical cause of focus being hidden (SC 2.4.11) and they take reading height on a phone. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **iOS Safari turns anything that looks like a phone number into a call link** unless the page sets `<meta name="format-detection" content="telephone=no">` (an Apple extension). [Standard] (Apple, Safari HTML Reference, archived) https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariHTMLRef/Articles/MetaTags.html
- **Shape, not colour, for ticked and not ticked; borders and inline SVG survive forced colours and printing.** (Covered in `checkbox-list.md`.) [Standard] https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html

---

## Recommendation for Grow2Notes

### The rule in one line

**The read view is the daily report's note block on a screen, in the same order and the same words, with three additions: the actions, the Version history link for managers, and the review panel at the bottom.** [Opinion] The design already defines a note "as written" once (11.3), so the screen follows it.

### Anatomy (submitted note, current version)

```
+-----------------------------------+
| < Flagged          (top bar: nav) |   back link names its destination (navigation component)
|                                   |
| Sam Taylor                        |  1  <h1> line 1: participant's full name, large
| Thursday 1 October 2026           |     <h1> line 2: note date
|                                   |
| Written by Priya Nair ·           |  2  byline (MetaLine)
| Submitted 4:42 pm                 |
| [Edited] last change by Jo Smith, |  3  status lines: tag + detail, one per line
|   Friday 2 October 2026, 9:01 am  |
| [Flagged] Reason: Mentioned pain  |
|   in his left knee after the walk.|
| [To review]                       |     (managers only)
|                                   |
| [ Edit ]  Version history         |  4  actions (Edit: author or manager;
|           Past notes              |     Version history: managers)
|-----------------------------------|
| 1. Goals                          |  5  TickListRead
| [x] Catch the 903 bus to the      |
|     library on his own            |
| [ ] Make his own lunch            |
|-----------------------------------|
| 2. Common items                   |  6  <h2>, then an <h3> + TickListRead
| Every note                        |     per shown group: Every note (when
| [x] Medication prompted           |     it has items), then each group
| Community outing                  |     picked on this note (D47)
| [ ] Travelled by bus or train     |
|-----------------------------------|
| 3. Guided notes                   |  7  the text, exactly as written
| Sam was keen to go to the library |
| today ...                         |
|-----------------------------------|
| Review                            |  8  review panel (managers, To review only)
| Comment (optional)                |
| [                               ] |
| You can enter up to 500 characters|
| [         Mark reviewed         ] |
+-----------------------------------+
```

| # | Part | Spec |
|---|---|---|
| 1 | **Title** | One `<h1>` holding two lines: the participant's full name (large, as on the form, 3.9) and the note date in the long form, wrapped in `<time dateTime="2026-10-01">`. A literal space between the two spans keeps the accessible name "Sam Taylor Thursday 1 October 2026" (each span is `display: block`). `tabIndex={-1}` so the app can focus it on arrival. |
| 2 | **Byline** | `MetaLine` with two parts: "Written by {author}" and "Submitted {time}". The time alone when the Melbourne date of `firstSubmittedAtUtc` equals the note date; otherwise date and time: "Submitted Friday 2 October 2026, 8:05 am" (4.4 "with its date when it was a later day"). For a **past-day note** the first part is replaced by the past-day line (row 3), as in the report (11.3), so the author's name isn't said twice. |
| 3 | **Status lines** | One `<p>` per line, in the fixed tag order from `status-tags.md`. Each line is a `Tag`, a visually hidden comma, then the detail in plain text. The lines are listed under Exact copy below. Lines that don't apply are not rendered: no "Not edited", no "Not flagged". |
| 4 | **Actions** | One row, wrapping. **Edit** is a link styled as a secondary button (it goes to the edit form's URL, so it is `<Link>`, not `<button>`). **Version history** and **Past notes** are plain text links. Each has at least 44 px of tappable height. No disabled actions: what a person can't do isn't shown. |
| 5–6 | **Goals, Common items** | **Goals:** `TickListRead` with heading "1. Goals" (the form's numbered heading), every snapshot goal in snapshot order, ticked or not (A3, A4); empty text "No goals set" (11.3). **Common items (D47):** `<h2>2. Common items</h2>`, then `<h3>Every note</h3>` and its `TickListRead headingLevel={3}` (only when it has items), then an `<h3>` with the group's name as stored and its `TickListRead headingLevel={3}` for each picked group, in order. These come from `v.commonItemGroups`, which already holds only Every note (left out when empty) and the groups picked in the version shown (design.md 6.3); the component renders the array in order and never filters groups itself. Every item in a shown group appears, ticked or not. "No common items set" (11.3) when the array is empty. Never the question "Which of these happened?" or the copied-picks line. Keyed by index (the read API gives names, not IDs). |
| 7 | **Guided notes** | `<h2>3. Guided notes</h2>` and one `<div>` holding the text with `white-space: pre-wrap`. No prompts (D34), no truncation, no "Read more", no auto-linking. |
| 8 | **Review panel** | Managers, `flagStatus = ToReview` only. `<h2>Review</h2>`, a `<form noValidate>` with a labelled `<textarea>` "Comment (optional)", the GOV.UK character count (500, A6), and a primary **Mark reviewed** `<button type="submit">`. |

Sections 5–8 are separated by a 1 px rule and space, like the thin rule between notes in the report. No cards, shadows, tinted boxes or zebra rows. Black-on-white text with no background behind it reads and prints like a page. [Opinion]

### Behaviour

1. **Arrival.** The page loads the note with `GET {base}` (6.3). Nothing renders in `<main>` until it has loaded: a half-drawn record (header with no ticks yet) could be misread as "nothing ticked". The app's page-level loading message covers the wait. Once loaded, the whole page renders at once, scrolled to the top, and the app's route-change rule focuses the `<h1>` (Gatsby research above).
2. **Reading.** Nothing on the page moves or refreshes by itself while someone reads. The note query does **not** refetch on window focus (`refetchOnWindowFocus: false`). Someone who switches apps to take a call and comes back must find the same text where they left it, not a silently replaced version. [Opinion] It refetches when the page is opened again, and after the user's own Save changes or Mark reviewed. The server's version check catches a review of an out-of-date version (6.4), so staleness can't cause a wrong review.
3. **Edit.** Shown when the note is Submitted and the viewer is its author (`author.id === me.userId`) or a manager (D19, 3.5). It opens the note form in edit mode (4.3, "Editing submitted note (version 2)"). Save changes there invalidates this note's query, so the read view shows the new version and the **Edited** line on return. Cancel returns here with nothing changed.
4. **Version history** (managers, every submitted note) opens 4.5. It is shown even when the note has one version; 4.5 then says "Not edited since submit".
5. **Past notes** opens the participant's history (4.4 "the 'Past notes' link in the note form and read view").
6. **Mark reviewed** (managers, To review only). No confirmation dialog (`confirm-dialog.md`: Mark reviewed has none). It sends `{versionNumber: current.versionNumber, comment}` (6.4).
   - **While sending:** the button gets `aria-disabled="true"` (never `disabled`, which drops focus) and ignores taps. After 1 second, "Saving…" appears next to it in the panel's status region.
   - **Success:** stay on the note. Invalidate the note, the reviews lists and `me` (the badge, `notification-badge.md`). The review panel goes. The status line changes from **To review** to **Reviewed** by {you}, {date}, {time}, with the comment. Focus moves to the `<h1>` and the page's status region announces "Note for Sam Taylor marked reviewed." The button that had focus has gone, so focus must move; the `<h1>` puts the manager at the top, next to the updated status and the back link to Flagged. This matches the design's "Note for Jane Citizen submitted" and the Deactivate pattern in `confirm-dialog.md`. [Opinion]
   - **Comment too long** (client check on tap, same rule as the flag reason): inline error "Comment must be 500 characters or less", focus to the field. One-field form, so no error summary (`form-validation.md`, AgDS).
   - **No connection or server error:** "Not marked reviewed: no connection. Check your connection and try again." in the panel's alert region above the button. Focus stays on the button. The comment stays.
   - **`409 review.not_current`:** refetch the note, keep the comment text.
     - If the note is no longer To review (another manager reviewed it): the panel goes, focus moves to the `<h1>`, and the status region says "This note has already been marked reviewed." The status lines show who reviewed it.
     - If a newer version exists: the page shows the new version, the panel stays with its comment, and the alert region says "This note was changed by {name} at {time}. Read the latest version above, then mark it reviewed." Focus stays on the button.
7. **Discard draft** (manager, someone else's draft) uses the Discard dialog in `confirm-dialog.md`. On success the manager lands on Today.
8. **Text selection and copying are allowed.** Nothing blocks selection or the context menu.

### States

| State | What shows |
|---|---|
| **Default** | As in the anatomy, with only the lines that apply. |
| **Hover** (laptop, `@media (hover: hover)`) | Links: underline thickens. Edit: the secondary button's hover fill. Tick rows, tags and text: no hover, because they aren't interactive. |
| **Focus** | Links and buttons: `outline: 3px solid var(--focus)` with `outline-offset: 2px`, at least 3:1 against the page, never `box-shadow` alone. The `<h1>` shows the same outline under `:focus-visible` only, so a keyboard user sees where focus landed and a touch user sees no box around the title. |
| **Active / pressed** | Edit and Mark reviewed: the button's pressed style. Nothing else. |
| **Disabled** | Not used. Actions the viewer can't take are hidden. Mark reviewed while sending uses `aria-disabled`, not `disabled`. |
| **Loading** | The app's page-level loading message; nothing of the note until all of it is ready. Never placeholder tick boxes or an empty Guided notes box. |
| **Error: note not found** (`404`) | "There is no note for this day." and a **Past notes** link. *Proposed.* (Most likely a draft discarded between listing and tapping.) |
| **Error: didn't load** (network, `5xx`) | "The note didn't load. Check your connection, then try again." and a **Try again** button that refetches. *Proposed.* |
| **Error: signed out** (`401`) | The app's sign-in flow, then back to this note (4.0). |
| **Empty: no goals / no common items** | "No goals set" / "No common items set" under the heading (11.3); the second when no common-item group is shown (Every note empty and no group picked). |
| **Empty: Guided notes** | Can't happen on a submitted note (3.4). On a manager's read-only **draft**: "Nothing written yet." *Proposed.* |
| **Not flagged / not edited / not past-day** | The line isn't shown. No "No" values. Absence of a tag means the state doesn't apply (GOV.UK tag guidance, `status-tags.md`). |
| **Read-only** | The whole component is read-only. The only inputs on the page are the review comment and the buttons. |
| **Forced colours** | Tags turn bold (`status-tags.md`); tick icons follow the text colour (`checkbox-list.md`); rules are borders, so they show. |
| **200% text, 320 px wide** | Everything wraps; nothing is clipped or truncated; no horizontal scroll. Long names, reasons and unbroken strings wrap with `overflow-wrap: break-word`. |

### Phone and laptop

- **Phone (first).** One column with the 16 px page gutter. The `<h1>` name is the largest text on the page. The byline and status lines wrap under themselves. The action row wraps: Edit, then the links. The review panel's textarea and **Mark reviewed** are full width. Nothing is sticky. Expect about 30–40 characters per line, which the screen decides.
- **Laptop.** The same single column and the same order. Cap the column at **40 rem** and left-align it with the app's content. Then count a real line of Guided notes in the chosen font. If it runs over about 75 characters, narrow the column (with a 16 px system font that is about 34–36 rem; with 19 px text, 40 rem is about right). Don't move the metadata into a side column: that splits what the report keeps together, and a side column reads after or before the note depending on DOM order (SC 1.3.2). The extra laptop width belongs to the setup screens (4.0).
- **Type.** The read view uses the app's body size and never anything smaller for metadata. This file recommended 19 px (1.1875 rem) body text (**overruled: 18 px app-wide, foundations.md and participant-notes.md Conflicts #5**) with a 1.5 line height, GOV.UK's size, for tired readers and people reading English as a second language. [Convention] At least 16 px in any case. Metadata uses the body text colour. Hierarchy comes from size, order and the tags, not from grey text.

### Exact copy

Design strings are used as written. Dates use the long form from the brief and the form header ("Thursday 1 October 2026"); times use "4:12 pm". *Proposed* marks strings that design.md doesn't have, which this component can't work without.

| Where | Copy | Source |
|---|---|---|
| Title | {Given} {Family} / {Weekday D Month YYYY} | 4.3 header, 3.9 |
| Byline | Written by {author} · Submitted {time} | 4.4, 11.3 |
| Byline, submitted on a later day | Written by {author} · Submitted {Weekday D Month YYYY}, {time} | 4.4, 11.3 |
| Past-day note | **[Past-day note]** written on {Weekday D Month YYYY}, {time} by {author} | 3.8, 4.4 (A11) |
| Edited | **[Edited]** last change by {name}, {Weekday D Month YYYY}, {time} | 3.5, 11.3, `status-tags.md` |
| Flag (current version flagged) | **[Flagged]** Reason: {reason, line breaks kept} | 4.3 "Reason" label, 4.4 "the flag and its reason" |
| To review (managers) | **[To review]** | 3.6, 4.6 |
| To review, flag removed by a later edit (managers) | **[To review]** Flag removed in a later edit · then a line: Reason: {reason from the flagged version} | 4.6, 11.3 |
| Reviewed (managers) | **[Reviewed]** by {name}, {Weekday D Month YYYY}, {time} · then, if any: Comment: {comment, line breaks kept} | 11.3 |
| Actions | Edit · Version history · Past notes | 4.4 |
| Section headings | 1. Goals · 2. Common items · 3. Guided notes | 4.3 |
| Empty lists | No goals set · No common items set | 11.3 |
| Common-item group headings (`<h3>`) | Every note · each picked group's name as stored | D45, D47 |
| Tick state (screen reader only) | , ticked · , not ticked | `checkbox-list.md` |
| Review panel heading | Review | *Proposed* |
| Review comment label | Comment (optional) | 4.6 "an optional comment"; "(optional)" is the GOV.UK convention |
| Review comment count | You can enter up to 500 characters · You have {n} characters remaining · You have {n} characters too many | GOV.UK defaults, as in `conditional-reveal.md` |
| Review comment error | Comment must be 500 characters or less | *Proposed*, GOV.UK pattern |
| Review button | Mark reviewed | 4.6 |
| Busy | Saving… | 4.3 save indicator word |
| Mark reviewed success (status region) | Note for {Given} {Family} marked reviewed. | *Proposed*, modelled on "Note for Jane Citizen submitted" (4.3) |
| Mark reviewed, no connection | Not marked reviewed: no connection. Check your connection and try again. | *Proposed*, modelled on "Not saved: no connection" (4.3) |
| Mark reviewed, already reviewed | This note has already been marked reviewed. | *Proposed* |
| Mark reviewed, newer version | This note was changed by {name} at {time}. Read the latest version above, then mark it reviewed. | *Proposed* |
| Version view (4.5) | Version {n} of {current} · then the same meta line as the version history row that was tapped: "Submitted · {author} · {date}, {time}" (version 1) or "{name} (manager) · {date}, {time}" (later versions) | 4.5 rows, `status-tags.md` |
| Manager, someone else's draft | Draft · {author} · started {time} · button **Discard draft** | 4.2, 4.3 |
| Manager draft, flag | [tick icon] Flag for manager, ticked / not ticked · if ticked: Reason: {reason} | 4.3 labels |
| Manager draft, empty Guided notes | Nothing written yet. | *Proposed* |
| Worker, someone else's draft | {author} started today's note for {Given} {Family} at {time}. Only {author} can finish it. | 4.2 (see note on names below) |
| Not found | There is no note for this day. | *Proposed* |
| Load failed | The note didn't load. Check your connection, then try again. · button **Try again** | *Proposed* |

Names: the API gives one `displayName` per user. "Only Alex can finish it" (4.2) needs a given name, which the app doesn't store separately. Splitting a display name at the first space fails for many names. Use the display name in both places: "Only Alex P. can finish it." [Opinion]

### Accessibility

**Semantics (native HTML only).**
- `<main>` → `<h1>` (name and date) → status lines as `<p>` → action row (`<a>` links) → `<h2>`s for the sections → Goals and Common items as `<ul role="list">` (`TickListRead`), with an `<h3>` per shown common-item group → Guided notes as a `<div>` of text → the review panel's `<h2>` and `<form>`.
- No `<section>` with an accessible name (no region landmarks), no `<article>` wrapper and no `<dl>`. ARIA appears only where `TickListRead` and `MetaLine` already use it (`role="list"`, `aria-hidden` on icons and separators).
- Dates in `<time dateTime>`: `YYYY-MM-DD` for the note date, the ISO UTC string for timestamps.
- Tags are text (`status-tags.md`). Every state is in words, never colour alone (4.0, SC 1.4.1).

**Reading order (what NVDA or VoiceOver says, manager on 4.6):**

> "Sam Taylor Thursday 1 October 2026, heading level 1. Written by Priya Nair, Submitted 4:42 pm. Edited, last change by Jo Smith, Friday 2 October 2026, 9:01 am. Flagged, Reason: Mentioned pain in his left knee after the walk. To review. Edit, link. Version history, link. Past notes, link. 1. Goals, heading level 2. List, 2 items. Catch the 903 bus to the library on his own, ticked. Make his own lunch, not ticked. … 3. Guided notes, heading level 2. Sam was keen … Review, heading level 2. Comment (optional), edit text, multi-line, You can enter up to 500 characters. Mark reviewed, button."

The flag reason comes straight after the title, so a manager hears why they are there before the content. Headings let anyone jump to "3. Guided notes" in one keystroke (WebAIM survey). Whether each screen reader pauses at the visually hidden commas is **unverified**; they are harmless either way.

**Keyboard.** Tab order is the DOM order: Edit → Version history → Past notes → Comment → Mark reviewed. There are no custom keys and no tab stops on text, tags or tick rows.

**Announcements.**
- On arrival: the focused `<h1>`.
- After Mark reviewed, or when it is no longer possible: focus to the `<h1>` plus one message in the page's status region (`role="status"`, in the DOM from first render, empty until used). The `<h1>` read on focus and the polite message don't collide, because polite messages wait.
- Errors in the panel: the panel's alert container (`role="alert"`, in the DOM from first render, empty until used).
- Nothing else is announced. Background data changes are silent.

**WCAG 2.2 criteria met.** 1.3.1 Info and Relationships (headings, lists, labels) · 1.3.2 Meaningful Sequence (one column, DOM order = visual order) · 1.4.1 Use of Color · 1.4.3 Contrast (Minimum) (body colour for all text) · 1.4.4 Resize Text (rem units; zoom never blocked) · 1.4.10 Reflow · 1.4.11 Non-text Contrast (tick icons, focus) · 1.4.12 Text Spacing (no fixed heights) · 2.1.1 Keyboard · 2.4.2 Page Titled ("Grow2Notes – Note" describes the page's purpose, 4.0) · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) (nothing sticky) · 2.5.3 Label in Name (no `aria-label` overrides) · 2.5.8 Target Size (Minimum) (44 px, A32) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 4.1.2 Name, Role, Value · 4.1.3 Status Messages. The laptop layout also meets the line-length, alignment and line-spacing parts of 1.4.8 (AAA, not required).

**Test (no new scope).** axe through Playwright on each variant. Manual passes with NVDA + Chrome, VoiceOver on iOS Safari and TalkBack on Android Chrome, at 200% text and in Windows forced colours. Use a 20,000-character note (A6) with long unbroken strings and many blank lines.

### Implementation (React 19, native HTML, CSS Modules)

Native HTML covers everything here, so **no React Aria**. The component is presentational. The page component fetches, decides permissions and passes the review panel in as children.

```ts
// src/lib/melbourneTime.ts — one formatter for the whole app (dates spelled out, Melbourne time)
const ZONE = 'Australia/Melbourne';
const long = (timeZone: string) => new Intl.DateTimeFormat('en-AU',
  { timeZone, weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
const noteDateFmt = long('UTC');  // a note date is a calendar date: format it in UTC so it never shifts
const stampDateFmt = long(ZONE);
const timeFmt = new Intl.DateTimeFormat('en-AU', { timeZone: ZONE, hour: 'numeric', minute: '2-digit' });
const dayFmt = new Intl.DateTimeFormat('en-CA', { timeZone: ZONE, year: 'numeric', month: '2-digit', day: '2-digit' });

export const formatNoteDate = (d: string) => noteDateFmt.format(new Date(`${d}T00:00:00Z`)); // Thursday 1 October 2026
export const formatTime = (utc: string) => timeFmt.format(new Date(utc));                     // 4:42 pm
export const formatStamp = (utc: string) =>                                                    // Friday 2 October 2026, 8:05 am
  `${stampDateFmt.format(new Date(utc))}, ${formatTime(utc)}`; // join it ourselves: ICU's combined format says "at"
export const melbourneDay = (utc: string) => dayFmt.format(new Date(utc));                    // 2026-10-02
```

Checked in Node 24 (ICU there gives "Thursday 1 October 2026", "10:14 am", and "Saturday 3 October 2026 at 10:14 am" for a combined format, hence the manual join). Browser ICU versions can differ, so unit-test the strings.

```tsx
// src/features/notes/read/NoteReadView.tsx
import { type ReactNode, type Ref } from 'react';
import { Link } from 'react-router';
import { TickListRead } from '../../../components/ticks/TickListRead';
import { Tag } from '../../../components/status/Tag';
import { MetaLine } from '../../../components/status/MetaLine';
import { formatNoteDate, formatStamp, formatTime, melbourneDay } from '../../../lib/melbourneTime';
import s from './NoteReadView.module.css';

const Pause = () => <span className="visually-hidden">,</span>;

export function NoteReadView({ name, note, titleRef, canEdit, isManager, hrefs, flaggedReason, children }: {
  name: string;
  note: SubmittedNote;              // GET {base} response (6.3)
  titleRef?: Ref<HTMLHeadingElement>;
  canEdit: boolean;                 // author or manager, decided by the page
  isManager: boolean;
  hrefs: { edit: string; versions: string; pastNotes: string };
  flaggedReason?: string;           // only when a later edit removed the flag (see Per-screen notes)
  children?: ReactNode;             // the review panel, or nothing
}) {
  const v = note.current;
  const first = note.firstSubmittedAtUtc;
  const submitted = melbourneDay(first) === note.noteDate ? formatTime(first) : formatStamp(first);
  const review = note.reviews                // latest review only, as in the report (11.3)
    ?.toSorted((a, b) => a.reviewedAtUtc.localeCompare(b.reviewedAtUtc)).at(-1);

  return (
    <div className={s.note}>
      <h1 ref={titleRef} tabIndex={-1} className={s.title}>
        <span className={s.name}>{name}</span>{' '}
        <time className={s.date} dateTime={note.noteDate}>{formatNoteDate(note.noteDate)}</time>
      </h1>

      <div className={s.meta}>
        {note.isPastDayNote ? (
          <>
            <p><Tag kind="pastDay" /><Pause /> written on {formatStamp(note.startedAtUtc)} by {note.author.displayName}</p>
            <MetaLine parts={[`Submitted ${submitted}`]} />
          </>
        ) : (
          <MetaLine parts={[`Written by ${note.author.displayName}`, `Submitted ${submitted}`]} />
        )}
        {note.isEdited && note.lastEdit && (
          <p><Tag kind="edited" /><Pause /> last change by {note.lastEdit.by}, {formatStamp(note.lastEdit.atUtc)}</p>
        )}
        {v.isFlagged && (
          <p><Tag kind="flagged" /><Pause /> Reason: <span className={s.userText}>{v.flagReason}</span></p>
        )}
        {isManager && note.flagStatus === 'ToReview' && (
          <>
            <p><Tag kind="toReview" />{!v.isFlagged && <><Pause /> Flag removed in a later edit</>}</p>
            {!v.isFlagged && flaggedReason && <p>Reason: <span className={s.userText}>{flaggedReason}</span></p>}
          </>
        )}
        {isManager && note.flagStatus === 'Reviewed' && review && (
          <>
            <p><Tag kind="reviewed" /><Pause /> by {review.reviewedBy}, {formatStamp(review.reviewedAtUtc)}</p>
            {review.comment && <p>Comment: <span className={s.userText}>{review.comment}</span></p>}
          </>
        )}
      </div>

      <p className={s.actions}>
        {canEdit && <Link to={hrefs.edit} className={s.secondaryButton}>Edit</Link>}
        {isManager && <Link to={hrefs.versions}>Version history</Link>}
        <Link to={hrefs.pastNotes}>Past notes</Link>
      </p>

      <div className={s.section}>
        <TickListRead heading="1. Goals" emptyText="No goals set" items={v.goals} />
      </div>
      <div className={s.section}>
        <h2>2. Common items</h2>
        {v.commonItemGroups.length === 0 ? (
          <p>No common items set</p>
        ) : (
          // Every note (left out by the API when empty), then the groups picked in this version (D47)
          v.commonItemGroups.map((g, i) => (
            <TickListRead key={i} headingLevel={3} heading={g.name} items={g.items} />
          ))
        )}
      </div>
      <div className={s.section}>
        <h2>3. Guided notes</h2>
        <div className={s.narrative}>{v.narrative}</div>  {/* text node: React escapes it; never innerHTML */}
      </div>

      {children}
    </div>
  );
}
```

```css
/* NoteReadView.module.css */
.note { max-inline-size: 40rem; }                 /* then count a real line: ≤ ~75 characters */
.title { margin: 0; line-height: 1.2; overflow-wrap: break-word; }
.name { display: block; font-size: var(--text-xl); font-weight: 700; }
.date { display: block; font-size: var(--text-m); font-weight: 400; margin-block-start: 0.25rem; }
.title:focus { outline: none; }                   /* programmatic focus after a tap: no box */
.title:focus-visible { outline: 3px solid var(--focus); outline-offset: 4px; } /* keyboard: always visible */

.meta { margin-block-start: 1rem; }
.meta p { margin: 0.25rem 0 0; line-height: 1.5; }
.userText, .narrative { white-space: pre-wrap; overflow-wrap: break-word; }
.narrative { line-height: 1.5; margin-block-start: 0.5rem; }

.actions { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem 1.5rem; margin-block: 1rem 0; }
.actions a { display: inline-flex; align-items: center; min-block-size: 44px; }
.section { border-block-start: 1px solid var(--rule); margin-block-start: 1.5rem; padding-block-start: 1rem; }
```

```tsx
// The page (4.4 and 4.6 share it). The review panel is passed in only when it applies.
const { data: note, status, refetch } = useQuery({
  queryKey: ['notes', participantId, noteDate],     // one prefix with version-history.md (editorial pass)
  queryFn: () => getNote(participantId, noteDate),
  refetchOnWindowFocus: false,   // the text must not change under someone reading it
});                              // memory cache only: no persister, no browser storage (D22)
const canEdit = note?.status === 'Submitted' && (me.role === 'Manager' || note.author.id === me.userId);
const showPanel = me.role === 'Manager' && note?.flagStatus === 'ToReview';
```

- **Escaping.** `{v.narrative}`, `{v.flagReason}` and `{review.comment}` are React text, so they are escaped. Never pass them through `dangerouslySetInnerHTML`, a Markdown renderer or a linkifier.
- **Line breaks.** Render the stored text unchanged. Don't trim, split into paragraphs or collapse blank lines: the report keeps them (11.3) and the screen should match.
- **Keys and order.** `TickListRead` keys by index; the snapshot order is fixed (`checkbox-list.md`).
- **Head.** The app's `index.html` must not block zoom (`width=device-width, initial-scale=1`, nothing else). Add `<meta name="format-detection" content="telephone=no">` so iOS Safari leaves numbers in notes as plain text, the way they were written. [Opinion] It is one global tag and adds nothing to the product.
- **Mark reviewed mutation.** `onSuccess`: invalidate `['notes', …]` (superseded: the record replaces the form in place, participant-notes.md), invalidate `['reviews']` and `me`, then focus the `<h1>` through `titleRef` in an effect and write the message into the page status region. Keep the comment in component state until success, so an error never clears it.

---

## Per-screen notes

**Read view (4.4), worker**
- The author sees **Edit**; other workers don't. Workers never see To review, Reviewed, review comments or Version history: the API doesn't send them (6.3), and the component doesn't ask for them.
- A worker sees **Flagged** and the reason on any flagged submitted note (4.4 lists "the flag and its reason" for everyone).
- Reached from Today by tapping a Submitted row (4.2). The back link says where it goes ("Today", "Past notes").

**Read view (4.4), manager**
- Edit and Version history on every submitted note.
- When the note is To review, the review panel is at the bottom, the same panel as on 4.6 (4.4 "Mark reviewed (manager, when the note is in To review)"). One component, one behaviour, wherever the manager opened the note.
- When the note was reviewed more than once (flagged again after a review), show the **latest** review only, as the report does (11.3). The Reviewed tab still lists every review.

**Someone else's draft (4.4 state, 4.2)**
- **Worker:** the `<h1>` (name and date) and one paragraph: "Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex P. can finish it." For a draft from an earlier day, replace "today's note" with "the note" (the `<h1>` gives the date). No sections, no actions.
- **Manager:** the `<h1>`, the status line "Draft · Alex P. · started 9:14 am" (with the date when it was started on a later day, which only happens on a past-day draft), **Discard draft** in the action row (warning style, `confirm-dialog.md`), then Goals, Common items and Guided notes from the draft. Common items render exactly as on a submitted note: Every note (when it has items) and the groups picked so far in the working copy, as `v.commonItemGroups` gives them (design.md 6.3), never the question or the unpicked groups. Show the flag as the form showed it: a read-only tick row "Flag for manager" and, if ticked, "Reason: …". **Never** the Flagged tag on a draft: a flag on a draft alerts nobody (3.6), and the tag says it did. Empty Guided notes: "Nothing written yet." Use the same no-refetch-on-focus rule; the author may still be typing, and the manager sees the draft as at the moment they opened it.

**One version (4.5)**
- The `<h1>` (name and date), then "Version 2 of 3" (this version's number and the current version number from 4.5's header), then the same meta line as the row the manager tapped: "Submitted · Priya Nair · Thursday 1 October 2026, 4:42 pm" for version 1, "Sam Lee (manager) · Friday 2 October 2026, 9:01 am" for later ones.
- This version's own flag: **[Flagged]** Reason: … when `isFlagged` is true in that version. No other tags.
- No Edit, no restore, no review panel, no Past notes link. Back goes to Version history.
- Don't highlight differences between versions. The design shows each version in full (4.5) and asks for nothing more.
- An old version can hold another participant's information (3.9). It is shown as stored; the restriction is that only managers reach this screen (A13).

**Flagged note review (4.6)**
- Mount the same page under the Flagged route, so the back link says "Flagged" and returns to the tab the manager came from.
- **To review, current version flagged:** Flagged + Reason, then To review, at the top; the panel at the bottom.
- **To review, flag removed in a later edit:** the top reads "[To review] Flag removed in a later edit" and then "Reason: {the flagged version's reason}". The read endpoint returns only the current version's flag, so take the reason (and `flagRemovedLater`) from the To review list (`GET /api/reviews?status=toReview`, 6.4). That list is small, is returned whole and is already fetched for the Flagged screen and its count. Match the row on participant and note date. This needs no API change. On the 4.4 route the same lookup works for managers, because the list is manager-only and small.
- **Reviewed tab:** no panel. The top shows "[Reviewed] by Jo Smith, Friday 2 October 2026, 9:30 am" and "Comment: …".
- **Edit, then review:** after Save changes the manager returns to this view with the new version. If the edit added a flag or changed the reason, the note is still To review (A15), so the panel is still there.

---

## Anti-patterns to avoid

1. **Disabled or `aria-readonly` checkboxes or text fields to show a submitted note.** Faint, skipped by Tab, announced as "disabled" by TalkBack (`checkbox-list.md`).
2. **Truncating the Guided notes** with "Read more", a fixed height, a fade-out or a collapsed section, on phones or anywhere. The note is the record, and NN/g found phones don't need shortened text.
3. **Rendering user text as HTML** (`dangerouslySetInnerHTML`, Markdown, auto-linking URLs or phone numbers). XSS risk, and the record would no longer be shown as written.
4. **Collapsing the writer's line breaks** (default `white-space`) or re-flowing them into new paragraphs.
5. **Grey or small "metadata" text** below 4.5:1 or below body size. The author, times and flag are part of the record.
6. **Edit shown but disabled for workers who aren't the author**, or a 403 after tapping it. Hide it.
7. **A Print button, a "Download this note" button, prev/next note arrows, a version diff, a "Copy" button, or a "share" action.** None is in the design. The daily report and the participant export are the paper and file routes (D43, A30).
8. **Relative times** ("2 days ago", "yesterday") or times without the date when they fall on a different day from the note.
9. **A sticky header or a sticky Mark reviewed bar.** It takes reading height on a phone and can hide focus (SC 2.4.11).
10. **Two-column laptop layout with the metadata in a sidebar.** Splits the record and confuses reading order.
11. **Showing the Flagged tag on a draft**, or To review / Reviewed / review comments to workers.
12. **A confirmation dialog before Mark reviewed.** The design has none, and the version check already guards against reviewing the wrong thing.
13. **Moving focus while someone is reading** (for example, after a background refresh), or refetching on window focus so the text changes under them.
14. **Placeholder tick boxes or an empty text area while loading.** They show a false record.
15. **`user-select: none`, blocking the context menu, or `maximum-scale=1`/`user-scalable=no`.** They stop people selecting text (for example to look up a word) and stop them zooming (SC 1.4.4).
16. **Cards, shadows, tinted panels or zebra striping** around sections. They add visual noise and disappear in forced colours anyway.
17. **New words for existing things:** "Amended", "Updated", "Late entry", "Backdated", "Approved", "Signed off", "Checked". Use the design's words only.
18. **Region landmarks for every section** (`<section aria-labelledby>`), which fills the landmarks list with noise.

---

## Tensions with decisions

- **D20 (every worker can read every note) and need-to-know access.** The OAIC's guide to securing personal information says entities should, "when possible, limit internal access to personal information to those who require access to do their job (ie provide access on a 'need to know' basis)". In the read view this means every worker sees every participant's Guided notes and flag reasons, which may hold sensitive health information. [Standard] (regulator guidance) https://www.oaic.gov.au/privacy/privacy-guidance-for-organisations-and-government-agencies/handling-personal-information/guide-to-securing-personal-information. Noted only. D20 follows from having no rostering (D14) and one worker per participant per day chosen on the day (D13), and the design already limits the most sensitive layer (earlier versions) to managers (A13, HPP 6.7).
- **D7, D10, A4 (one optional checkbox per item).** In the read view, "not ticked" can't be told apart from "missed". The evidence (GOV.UK "none" checkbox research) is set out once in `checkbox-list.md`. Noted only; the hints "Ticked means reached." and "Ticked means done." are the design's answer, and the read view shows every item as "ticked" or "not ticked", as the report does.

---

## Sources

**Standards and specifications**
- WCAG 2.2 Understanding 1.3.1 Info and Relationships: https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html
- WCAG 2.2 Understanding 1.3.2 Meaningful Sequence: https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.4 Resize Text: https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html
- WCAG 2.2 Understanding 1.4.8 Visual Presentation (AAA): https://www.w3.org/WAI/WCAG22/Understanding/visual-presentation.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Understanding 1.4.12 Text Spacing: https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- WCAG 2.2 Understanding 2.4.6 Headings and Labels: https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- W3C ACT rule, Meta viewport allows for zoom: https://www.w3.org/WAI/standards-guidelines/act/rules/b4f0c3/
- WAI-ARIA 1.2, region role: https://www.w3.org/TR/wai-aria-1.2/#region
- WAI-ARIA APG, Landmark Regions: https://www.w3.org/WAI/ARIA/apg/practices/landmark-regions/
- HTML Standard, the dl element: https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element
- HTML Standard, the time element: https://html.spec.whatwg.org/multipage/text-level-semantics.html#the-time-element
- MDN, white-space: https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/white-space
- MDN, readonly: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly
- React, common components (`dangerouslySetInnerHTML`): https://react.dev/reference/react-dom/components/common
- TanStack Query v5, Window Focus Refetching: https://tanstack.com/query/v5/docs/framework/react/guides/window-focus-refetching
- Apple, Safari HTML Reference: Supported Meta Tags (format-detection, archived): https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariHTMLRef/Articles/MetaTags.html
- OAIC, Guide to securing personal information: https://www.oaic.gov.au/privacy/privacy-guidance-for-organisations-and-government-agencies/handling-personal-information/guide-to-securing-personal-information

**Research**
- WebAIM, Screen Reader User Survey #10 (1,539 responses, 2023–24): https://webaim.org/projects/screenreadersurvey10/
- Gatsby / Marcy Sutton, What we learned from user testing of accessible client-side routing techniques (2019, 5 sessions): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- NN/g, Reading Content on Mobile Devices (Moran, 2016, 276 participants): https://www.nngroup.com/articles/mobile-content/
- Mary C. Dyson, Legible Typography, overview of research (line length studies incl. Dyson & Haselgrove 2001, Shaikh & Chaparro 2005): https://legible-typography.com/en/6-overview-of-research-typography
- GOV.UK Design System, Check answers pattern (research notes): https://design-system.service.gov.uk/patterns/check-answers/
- Adrian Roselli, Avoid Read-only Controls (2024): https://adrianroselli.com/2024/11/avoid-read-only-controls.html

**Conventions**
- GOV.UK Design System, Summary list (and summary cards): https://design-system.service.gov.uk/components/summary-list/
- NHS digital service manual, Summary list: https://service-manual.nhs.uk/design-system/components/summary-list
- GOV.UK Design System, Layout (75 characters per line): https://design-system.service.gov.uk/styles/layout/
- GOV.UK Design System, Type scale: https://design-system.service.gov.uk/styles/type-scale/
- GOV.UK Design System, Headings: https://design-system.service.gov.uk/styles/headings/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- GOV.UK style guide A to Z (dates and times): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- Cloudscape, Timestamps: https://cloudscape.design/patterns/general/timestamps/
- HashiCorp Helios, Show, hide, and disable: https://helios.hashicorp.design/patterns/disabled-patterns
- Sibling components: `checkbox-list.md`, `status-tags.md`, `conditional-reveal.md`, `confirm-dialog.md`, `form-validation.md`, `notification-badge.md`
