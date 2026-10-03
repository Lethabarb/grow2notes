# Editable ordered list (goals and common items)

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026: Goals (participants.md) and Common items (common-items.md) now build this one
> ListEditor the same way, and the values below are updated to match: a **wrapping flex row** (no `@container`
> query), **one verb per failed action** ("Not moved / added / saved / archived / restored: [cause]. Try again."),
> **no full stop** on one-phrase announcements ("Moved up to number 2 of 5"), announcements written into the
> **page status region** (`PageStatus`, app-shell.md component 3; there is no app-level region), and `retry: 0` on
> every write.
> Update, 3 October 2026 (D44, D45): common items are now in **groups**. On 4.9 this component is used **once per
> group**, unchanged inside, under the group's `<h2>`; the group actions (Rename, Move up, Move down, Archive, Add
> group, Archived groups) and moving an item between groups belong to the page (common-items.md, group-picker.md).

Component key: `list-editor`. This is the manager's list of a participant's **goals** (design.md 4.8) and of the
items in one organisation-wide **common-item group** (4.9, one list per group, D44). Each active row has **Edit**,
**Move up**, **Move down** and **Archive**.
There is an **Add goal** / **Add item** field of up to 200 characters, a collapsed section of archived rows with
**Restore**, and the help text that says changes apply only to notes started from now on. Sources: D7–D11, design.md
3.4 (snapshot), 3.7 (archiving), 4.8, 4.9, 5.3 (Goal, CommonItem), 6.6, 6.7, A2, A3, A6.

This spec covers how to build what the design already lists. It adds no screen, setting, notification or data. There
is no drag and drop, no delete, no "Save order" step and no separate edit page. None of these is in the design, and
none is needed to meet WCAG 2.2 AA.

---

## Where it's used

| Screen | What the list holds | What differs |
|---|---|---|
| **4.8 Participant detail, "Goals" section** (managers; phone and laptop) | One participant's flat list of active goals, in note order (D8). Archived goals are in a collapsed section. | It is one section of a page that also has Details (a three-field form) and Actions. The section heading is an `<h2>` "Goals". Help text has two sentences. Empty copy is "Notes will show an empty Goals section." The add control is "Add goal". Design 4.8 names "Save / Add goal" as the page's primary actions. If the participant is archived, the whole page is read-only (4.8 "a read-only banner with Restore"), so the list shows **no controls** at all. |
| **4.9 Common items** (managers) | One list per common-item group, Every note first (D44, D45), each in the order it shows on notes. Each group's archived items are in its own collapsed section. | One `ListEditor` per active group, inside that group's `<section>` under its `<h2>` (the group's name); the page `<h1>` "Common items" and the help text come once at the top (common-items.md). Empty copy per group: "No items in this group. It does not show on notes until it has one." The add label is "New item in [group name]" and the button "Add item" (primary in every group). The edit form gains a **Group** radio set to move the item (A42). Moves stay within the group. There is no read-only variant. |
| **4.8 Participants list** (Active / Archived toggle, names, Add participant) | Not this component. | Participants are sorted by family name, then given name (A5), and are never reordered by hand. The list and its search are covered by `participant-list-rows.md` and `search-filter.md`. |
| **4.8 Write past-day note** | Not this component. | A one-field date form (`form-validation.md`). |
| **4.3 Note form, 4.4 read view, 11 daily report** | Where the result shows, not where it is edited. | Goals and common items appear as tick rows in exactly the order and wording set here, frozen per note by the snapshot (3.4, 5.7). Every note's items show on every note; another group's items show only when the writer ticks that group (group-picker.md), and only picked groups appear in the read view and files (D47). `checkbox-list.md` owns those rows. |

---

## Best practice

### Reordering: buttons, not drag

- **WCAG 2.2 requires a non-drag way to reorder, and its own example is up/down controls.** SC 2.5.7 Dragging
  Movements (AA): "A sortable list of elements may, after tapping or clicking on a list element, provide adjacent
  controls for moving the element up or down." A list reordered **only** with buttons has no dragging to replace, so
  it meets 2.5.7 outright. **[Standard]**
  https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html
- **Reorder the DOM, not just the picture, and put focus back on the control that was used.** Technique SCR27 (a
  sufficient technique for SC 2.4.3 Focus Order) puts move controls on each item, says items "must be reordered in
  the actual DOM structure", and says to "set focus back on the menu item which launched the whole thing".
  **[Standard]** https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR27
- **Drag is weak on touch screens.** NN/g: drag and drop "can be hard to implement on touchscreens because they lack
  hover states", and its signifier must say both "grabbable" and what dragging does. The article also notes that
  mobile Gmail swapped dragging for a menu. **[Research]**, qualitative expert review with no study numbers.
  https://www.nngroup.com/articles/drag-drop/
- **A large design system says to always offer a non-drag alternative, with the item's name in the button.**
  Atlassian (Pragmatic drag and drop): "Always provide alternatives to dragging". It prefers move actions to
  arrow-key dragging because "Directional arrow movements require JAWS screen reader users to change screen reader
  mode". Accessible names should "include the name of the element being acted on". No user research is cited.
  **[Convention]** https://atlassian.design/components/pragmatic-drag-and-drop/accessibility-guidelines
- **GOV.UK's publishing tools use Up/Down buttons and turn drag off on phones.** The "Reorderable list" turns drag
  off "on small viewports … to prevent being triggered when scrolling on touch devices". Its acceptance criteria say
  the buttons must "inform the user about which item they operate on" and "preserve focus after interacting with
  them". **[Convention]** https://components.publishing.service.gov.uk/component-guide/reorderable_list
- **Accessible drag does exist, but it is a separate mode that people have to learn.** React Aria's drag and drop
  lets a keyboard user press Enter to start a drag, Tab between drop targets and Enter to drop. Touch screen reader
  users double-tap, swipe and double-tap. **[Convention]** https://react-aria.adobe.com/dnd. Move up and Move down
  need no mode at all, so React Aria isn't needed here. (Stack rule: use it only where native HTML falls short.)

### Move up on the first row, Move down on the last

- **`disabled` takes a control out of the tab order; `aria-disabled="true"` keeps it focusable.** The APG removes
  focus only "when users can reasonably infer the presence of a disabled element from nearby focusable elements".
  Its example is a toolbar's Up button when the first item is selected. In that example focus is on the list item,
  not on the button. **[Standard]** https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- **Focus is lost when the focused control disappears.** APG: if a removed or hidden active element isn't managed,
  "browsers move focus to the body element, effectively causing a loss of focus". **[Standard]** (same URL).
  Disabling the focused button has the same effect, because a `disabled` button can no longer hold focus. The HTML
  standard calls this the "focus fixup" behaviour
  (https://html.spec.whatwg.org/multipage/interaction.html#focus-fixup-rule; wording not re-checked in this pass).
  In this component, the button a manager has just pressed is exactly the one that becomes unavailable when the
  item reaches the end.
- **GOV.UK publishing hides the end buttons instead.** Its CSS sets `display: none` on Up for the first item and
  Down for the last. Its script moves focus to the other button when an item reaches an end (`e.target
  .nextElementSibling.focus()`). **[Convention]**
  https://github.com/alphagov/govuk_publishing_components/blob/main/app/assets/javascripts/govuk_publishing_components/components/reorderable-list.js
  This has two side effects **[Opinion]**. First, the remaining button slides into the gap, so the next tap or click
  in the same spot does the **opposite** move. Second, a keyboard user who presses Enter again, now on Down, undoes
  the move. Keeping the button in place and unavailable avoids both.

### Where focus goes after an action

- **Back to the control that was used. If it no longer exists, choose deliberately.** Atlassian: "Focus should
  move to the original trigger element whenever possible … If the element no longer exists after the operation,
  choose a new element using your best judgement." **[Convention]** (URL above). SCR27 says the same for moves.
  **[Standard]**
- **After deleting, Inclusive Components focuses the list's heading (`tabindex="-1"`) instead of a neighbour,** so
  the user hears "here's the list again". After adding, focus stays in the add field. Each result is announced as
  "[item] added" or "[item] deleted" through `role="status"`. **[Convention]**, expert practitioner.
  https://inclusive-components.design/a-todo-list/
- **Moving focus is itself the announcement.** In WCAG 4.1.3, content that takes focus is not a "status message".
  Anything that does **not** take focus must be announced through a live region. **[Standard]**
  https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

### Edit in the row, or on a separate page

- **GOV.UK and MOJ edit on a separate page.** A summary list row has a "Change" link that goes to the question page.
  MOJ's "Add to a list" (usability-tested and audited by DAC) works the same way. **[Convention]**
  https://design-system.service.gov.uk/components/summary-list/ ·
  https://design-patterns.service.justice.gov.uk/patterns/add-to-a-list/. Those patterns are built for citizen
  services that ask one thing per page.
- **Admin design systems edit in the row when the row holds everything that can be edited.** PatternFly: use inline
  edit when "All editable elements can be viewed within the row". It uses an explicit edit toggle with explicit save
  and cancel, not click-to-edit text. Avoid it when "editing is the primary function of the view". **[Convention]**
  https://www.patternfly.org/components/inline-edit/design-guidelines
- **[Opinion]** A goal is one field of up to 200 characters, in a list of about five. Editing it inside the row
  keeps its position and neighbours in view, and it adds no route or screen. The design specifies none.

### The archived section

- **`<details>` suits information that only some users need.** GOV.UK: "Do not use the details component to hide
  information that the majority of your users will need." Known issues: "Some users avoid clicking the link …
  as they think it will take them away from the page", and some voice-control users cannot operate it.
  **[Convention]** https://design-system.service.gov.uk/components/details/
- **Keep `<summary>` plain text and keep its default marker.** How a summary is announced varies by browser and
  screen reader (sometimes "button", sometimes "disclosure triangle"). Headings inside `<summary>` are "not
  consistently exposed". Removing the default triangle breaks the announced state in Firefox, VoiceOver, JAWS and
  NVDA. **[Convention]**, expert practitioner testing (2022).
  https://www.scottohara.me/blog/2022/09/12/details-summary.html
- **`open` is a boolean attribute and `toggle` fires on change,** so script can open the section. **[Standard]**
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/details

### Announcing changes

- **Put the live region in the first render and add text to it later.** MDN: "The most reliable way to ensure
  that live regions are registered is to include them in the initial markup." `role="status"` implies
  `aria-live="polite"` and `aria-atomic="true"`. **[Standard]**
  https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- **Live-region support is uneven, so the change the user can see has to carry the meaning.** Roselli's January 2026
  cross-reader tests found failures with polite regions (VoiceOver on macOS) and with `role="alert"` (Orca, among
  others). **[Research]**, practitioner testing. https://adrianroselli.com/2026/01/live-region-support.html
- **Say what moved and where it went.** Atlassian's example: "Task 'Clean dishes' moved to list 'Doing' from
  'Todo'." **[Convention]** (URL above)

### Repeated buttons in every row

- **Add the row's name as visually hidden text after the visible label.** GOV.UK summary lists ("Change name") and
  MOJ "Add another" both do this, so a screen reader user knows which item each button acts on. **[Convention]**
  https://design-system.service.gov.uk/components/summary-list/ ·
  https://design-patterns.service.justice.gov.uk/components/add-another/
- **Hidden text, not `aria-label`.** Automatic translation services still translate `aria-label` inconsistently.
  Text that is actually in the page gets translated. **[Research]**, practitioner testing, updated July 2025.
  https://adrianroselli.com/2019/11/aria-label-does-not-translate.html. This matters for managers who use a
  browser's translate feature.
- **The visible label must be part of the accessible name** (SC 2.5.3 Label in Name). "Move up, Makes own
  breakfast" starts with "Move up", so "click Move up" works in voice control. **[Standard]**

### Saving each change

- **Mutations that share a TanStack Query `scope.id` "will run in serial".** One scope per list therefore makes
  every write on that list wait for the one before it. **[Convention]**
  https://tanstack.com/query/v5/docs/framework/react/guides/mutations
- **Optimistic updates under concurrent mutations need extra handling.** You have to skip invalidation while
  sibling mutations are running, or the screen jumps back to stale data. **[Convention]**
  https://tkdodo.eu/blog/concurrent-optimistic-updates-in-react-query
- **[Opinion]** The ui-ux-design invariant I5 says state at commitment must be unambiguous. Showing the server's
  order after it confirms the change costs about one round trip. The app is hosted in Melbourne and its users are
  in Victoria, so that is well under the one-second point at which an indicator is needed. This is simpler than
  optimistic reordering with rollback.

### The text field

- **Use a textarea for text that may run longer than one line.** GOV.UK: "Use the textarea component when you need
  to let users enter an amount of text that's longer than a single line." Make its height "proportional to the
  amount of text you expect". 200 characters is five or six lines on a phone. **[Convention]**
  https://design-system.service.gov.uk/components/textarea/
- **Use a character count, not `maxlength`.** This matches the app's Reason field (`conditional-reveal.md`, after
  the GOV.UK Character count, tested with 17 users in 2017). `maxlength` silently cuts pasted and dictated text.
  **[Research]**, small sample, and **[Convention]**.
  https://design-system.service.gov.uk/components/character-count/

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **Buttons only.** Every active row has Edit, Move up, Move down and Archive, as the design says. There is no
   drag, no drag handle, no keyboard shortcut and no "reorder mode". This meets 2.5.7 because there is no dragging.
2. **Edit happens in the row.** Edit turns the row into a one-field form (textarea, character count, **Save**,
   **Cancel**). The URL doesn't change and no screen is added, so Edit is a `<button>` here. On other screens Edit
   is a link, because it changes the URL (`primary-actions.md`).
3. **Every change is saved straight away and shown only after the server confirms it.** This covers each move,
   add, edit, archive and restore. All writes on one list run one at a time (one mutation scope per list). Each
   write sends, then re-reads the list before the next one starts. Nothing is optimistic.
4. **At the ends, the move buttons stay and become unavailable.** Move up on the first row and Move down on the
   last row get `aria-disabled="true"`. They are never hidden and never `disabled`. With one row, both are
   unavailable.
5. **Focus follows the item.** After a move, focus stays on the same button of the moved item. After Archive, focus
   goes to that item's **Restore** in the archived section, which opens. After Restore, focus goes to that item's
   **Archive** in the active list. After Add, focus stays in the add field. After Save or Cancel, focus goes back
   to the row's **Edit**.
6. **Everything is said in words twice.** The change can be seen on screen (the row moves, appears or changes), and
   a short polite message goes to the page's status region (`PageStatus`, app-shell.md component 3;
   `primary-actions.md`).
7. **The active list is a numbered `<ol>`; the archived list is a `<ul>` inside `<details>`.** The visible numbers
   match the "number 2 of 5" announcements, and the order is what the list is about.
8. **Request errors appear in the row, conflicts above the list.** A failed request shows its message in the row it
   came from, and focus stays where it was. If someone else has changed the list, the list is re-read and a message
   above the list takes focus, because the row the manager pressed may have moved or gone.

### Anatomy

**Phone (one column):**

```
Goals                                                      <h2> "Goals" (4.8) or <h2> [group name] (4.9, one per group)
Changes apply to notes started from now on. Notes          help text <p id=help>
already started or submitted keep the wording they
were written with.
                                                           [conflict message slot, only when needed]
 1. Makes own breakfast                                    <ol> > <li>: number + text
    [ ↑ Move up ] [ ↓ Move down ]                          actions wrap; every button ≥ 44 × 44 px
    [ Edit ] [ Archive ]                                   row alert slot (role=alert, empty) sits above them
 ───────────────────────────────────────────
 2. Catches the bus to day program and
    phones when she arrives
    [ ↑ Move up ] [ ↓ Move down ]
    [ Edit ] [ Archive ]
 ───────────────────────────────────────────
 3. Phones her sister
    [ ↑ Move up ] [ ↓ Move down ]                          ← Move down: unavailable (dashed border)
    [ Edit ] [ Archive ]

New goal                                                   <label>
+-----------------------------------------+                <textarea rows=3>
|                                         |
+-----------------------------------------+
You can enter up to 200 characters                         shared CharacterCount
[               Add goal                ]                  primary, full width

▸ Archived goals                                           <details><summary> (closed on load,
                                                            shown only if something is archived)
```

**A row in edit mode** (it replaces the row's text and buttons, and stays numbered):

```
 2. Edit goal                                              <label>
    +-------------------------------------+                <textarea rows=3>, current text, caret at end
    | Catches the bus to day program and  |
    | phones when she arrives             |
    +-------------------------------------+
    You have 142 characters remaining
    [ Save ] [ Cancel ]                                    primary + secondary
```

**Laptop (when the row has room):** the text and the actions sit side by side, and the actions stay in the same
order. The row is a wrapping flex line, so the actions drop under the text by themselves when the text column would
be narrower than 16rem (common-items.md):

```
 1. Makes own breakfast                          [ ↑ Move up ] [ ↓ Move down ] [ Edit ] [ Archive ]
```

**Archived section, open:**

```
▾ Archived goals
  • Uses the washing machine                                 [ Restore ]
  • Walks to the shop                                        [ Restore ]
```

Order of parts: help text, then the conflict slot, then the active list (or the empty message), then the add form,
then the archived section. This follows design 4.8 and 4.9: list, then "Add goal" / "Add item", then the archived
rows.

Button order in each row, in the DOM and on screen: **Move up, Move down, Edit, Archive**. Move up and Move down
stay together as a pair. Archive comes last, after Edit, so it is the furthest from the text. Every button is in the
secondary style. Archive is not red, because it can be undone (`primary-actions.md`). The arrows are decorative
inline SVG (`aria-hidden="true"`, `fill: currentColor`). Each label is in words, so the buttons are never
icon-only.

### Behaviour

| Action | While the request runs | On success | Focus | Announced (polite) |
|---|---|---|---|---|
| **Move up / Move down** | The button is busy at once (`aria-disabled`, so a second tap is ignored, as in `primary-actions.md` 5.2). "Moving…" shows only if it takes over 400 ms. The list doesn't change yet. | `PUT …/order` with the full active ID list, built from the latest server order **when the write runs**. Then the list is re-read and re-renders in the new order. | Stays on the same button of the moved item. Re-focus it explicitly after the render, because React may have moved that DOM node and focus is lost when that happens (see Implementation notes). | "Moved up to number 2 of 5." / "Moved down to number 4 of 5." |
| **Move up on the first row / Move down on the last row** (unavailable) | No request | Nothing changes | Stays | "Already at the top." / "Already at the bottom." (`primary-actions.md` 5.1: pressing an unavailable button explains why) |
| **Edit** | — | The row becomes the edit form, prefilled with the current text | The textarea, with the caret at the end | — (the focused field's label and value are read) |
| **Save** (edit) | Save busy, "Saving…" after 400 ms. The textarea is `readOnly`. | Trims the text and replaces line breaks with spaces. If the text (and, on 4.9, the group) is **unchanged**, it closes like Cancel and sends nothing (an unchanged save would only add an empty audit entry). Otherwise `PUT /api/admin/goals/{id}` (or the common item `PUT` with `{text, groupId}`) with `If-Match`, then re-read. The row goes back to normal with the new text; an item moved to another group leaves this list and appears at the end of that group's list (A42). | That row's **Edit** (in its new group's list after a move) | "Goal saved." / "Item saved." / "Item saved and moved to the end of [group name]" |
| **Cancel** (edit) | — | The row goes back to normal, and the typed text is thrown away | That row's **Edit** | — |
| **Add goal / Add item** | Add busy, "Adding…" after 400 ms | Validate first (see below). Then `POST`, which adds at the end, and re-read. The new row appears at the bottom of the list, directly above the field. The field is cleared. | Stays in the field, ready for the next one (needed for the M1 test: "five goals on a phone without help") | "Goal added to the end of the list." / "Item added to the end of the list." |
| **Archive** | Archive busy, "Archiving…" after 400 ms | `POST …/archive` with `If-Match`, then re-read. The row leaves the active list and the numbers close up. The archived section appears if it was hidden, **opens**, and lists the item first (newest archived first). | That item's **Restore** | "Goal archived." / "Item archived." |
| **Restore** | Restore busy, "Restoring…" after 400 ms | `POST …/restore` with `If-Match`, then re-read. The item goes to the **end** of the active list (A2). If the archived section is now empty, it disappears. | That item's **Archive** in the active list | "Goal restored to the end of the list." / "Item restored to the end of the list." |
| **Open or close "Archived goals"** | — | Native `<details>` behaviour | Stays on the summary | Native "expanded" or "collapsed" |

**Validation** for Add and Save follows `form-validation.md` for one-field forms. It runs only when Add or Save is
pressed. The error sits above the field, focus goes to the field, and the field gets `aria-invalid="true"`. There is
no error summary at the top of the page. Typing past 200 characters is allowed: the count says "too many" and the
error appears on Add or Save. Whitespace-only text counts as empty.

**Queueing.** Pressing a button on another row while a write is running is allowed. That write waits its turn, and
its own button shows busy. Each queued write works out its body from the list as the server last returned it. A
queued move whose item has already reached the end sends nothing.

**Request errors.** Writes are never retried automatically: every mutation uses `retry: 0` (form-validation.md), because the add endpoints have no idempotency key and a silent retry after a lost response would add the row twice.

| Response | What happens |
|---|---|
| Network failure or `5xx` | The button returns to normal. The row's alert slot (always rendered, above that row's buttons, or above Save or Add in a form) shows the message. Focus stays on the button. Typed text is kept. |
| `412 precondition.failed` on **Save** (someone else reworded or reordered the row first) | Re-read the list. If the row is still active, keep the typed text and show, in the form: "Someone else changed this goal while you were editing. It now says: [their text]. Save to replace it with your wording, or Cancel to keep it." The next Save carries the new version. If the row has been archived, use the conflict row below instead. |
| `412` on Archive or Restore, or `422 config.order_mismatch` on a move | Re-read the list. Show the list-level message above the list and focus it: "Someone else changed this list just now. Check it, then try again." It stays until the next successful change on this list. |
| `401` | Not handled here. The app's sign-in-in-place flow takes over (`session-timeout.md`), and the typed text stays in memory. |

**Leaving with an open edit form.** There is no "unsaved changes" warning. The design asks for one only on Guide
prompts (4.10), and an edit here is at most 200 characters. Rows in edit mode are independent: a manager can have
two open at once, and moving other rows doesn't close or reset them. **[Opinion]**

### States

| State | What the manager sees | Semantics |
|---|---|---|
| **Default** | Numbered rows, each with its text and four secondary buttons. A 1px divider between rows. | `<ol>` > `<li>`. Buttons are native `<button type="button">`. |
| **Hover** (pointer only) | Buttons only, as in `primary-actions.md`. Rows have no hover style, because a row is not a control. | — |
| **Focus** | `outline: 3px solid var(--focus-ring); outline-offset: 2px` on the focused button, field or summary. It appears instantly. | `:focus-visible`. Never `box-shadow` alone. |
| **Active (pressed)** | The pressed fill appears instantly (`primary-actions.md`) | Activation on release |
| **Unavailable** (Move up on row 1, Move down on the last row, both when there is one row) | Same place and size. Grey fill, **dashed border**, label still at least 4.5:1. | `aria-disabled="true"`, still focusable. Pressing it announces why. |
| **Busy** | The pressed button, then its busy label after 400 ms. Other buttons stay normal. | `aria-disabled="true"` while busy. The busy label also goes to the status region (`primary-actions.md`). |
| **Editing** | The row's text and buttons are replaced by label, textarea, count, Save and Cancel | A `<form noValidate>` inside the `<li>` |
| **Error (field)** | The message above the field, a 4px left bar and a thicker border | `aria-invalid`, `aria-describedby` (`form-validation.md`) |
| **Error (request)** | The message in the row's alert slot | Always-rendered `role="alert"` container |
| **Conflict** | A message above the list with a 4px left bar | `<p tabIndex={-1}>`, focused |
| **Loading** (first load) | Heading and help text at once. Nothing else for 1 s, then "Loading…". No add form until the list arrives. | App-level loading pattern (`app-shell-nav.md`) |
| **Load failed** | "Goals couldn't be loaded. Check your connection, then try again." and a **Try again** button | Message plus button. Try again refetches. |
| **Empty** (no active rows) | 4.8: "No goals yet. Notes will show an empty Goals section." 4.9, per group: "No items in this group. It does not show on notes until it has one." The add form is still shown, and so is the archived section if anything is archived. | A `<p>`, not an empty `<ol>` |
| **Read-only** (4.8, archived participant) | Numbered goal texts only. No buttons, no help text, no add form. The archived section (if any) lists texts with no Restore. | Same lists, no controls |

### Phone and laptop

| | Phone | Laptop |
|---|---|---|
| Row layout | Number and text on top, full width. The actions wrap underneath (`flex-wrap`, 0.5rem gaps). At 375 px the move pair fits on one line, with Edit and Archive on the next. | A **wrapping flex line** (text `flex: 1 1 16rem`, actions at their natural width): text on the left and the actions in one line on the right when they fit, aligned to the top of the text; at 200% text or in a narrow window the actions drop underneath by themselves. No container query and no breakpoint of its own (common-items.md showed a 44rem container leaves the text about 4rem wide). |
| Buttons | At least 44 × 44 px (A32), at least 0.5rem apart | Same |
| Add button | Full width (`primary-actions.md`) | Width of its label |
| Edit form | Full row width | Full row width, so the textarea is not squeezed into the text column |
| Archived section | Below the add form, 2rem above it | Same |
| Positioning | Nothing sticky or fixed (`app-shell-nav.md`), so focused rows are never covered (SC 2.4.11) | Same |

### Exact copy

| Where | Text | Source |
|---|---|---|
| Help text, 4.8 | Changes apply to notes started from now on. Notes already started or submitted keep the wording they were written with. | design.md 4.8 |
| Help text, 4.9 (once, at the top of the page) | Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes apply to notes started from now on. | design.md 4.9 (replaces the pre-D44 string) |
| Row buttons | Move up · Move down · Edit · Archive | design.md 4.8, 4.9 |
| Hidden context on each row button | `, [item text]`, for example "Move up, Makes own breakfast" | Proposed (GOV.UK hidden-text pattern) |
| Archived row button | Restore (plus hidden `, [item text]`) | design.md 4.8, 4.9 |
| Add field label | New goal · New item in [group name] | **Proposed** (4.8) · design.md 4.9 |
| Add button | Add goal · Add item | design.md 4.8, 4.9 |
| Edit field label | Edit goal · Edit item | **Proposed** |
| Edit form group radios (4.9 only, two or more active groups) | Legend "Group"; one radio per active group, current one selected | design.md 4.9 |
| Edit buttons | Save · Cancel | design.md wording used elsewhere (4.3, 4.8) |
| Character count | You can enter up to 200 characters · You have 142 characters remaining · You have 3 characters too many | GOV.UK defaults, as in `conditional-reveal.md` |
| Field errors | Enter the goal · Goal must be 200 characters or less · Enter the common item · Common item must be 200 characters or less | `form-validation.md` (proposed there) |
| Empty, 4.8 | No goals yet. Notes will show an empty Goals section. | Second sentence: design.md 4.8. "No goals yet." is the design's own name for the state, **proposed** as visible text so the sentence makes sense alone. |
| Empty, 4.9 (per group) | No items in this group. It does not show on notes until it has one. | design.md 4.9 (replaces "No common items yet.") |
| Archived summary | Archived goals · Archived items (plus hidden " in [group name]" on 4.9) | **Proposed**, from design.md 4.8 ("Archived goals sit in a collapsed section") and 4.9 ("the group's archived items, collapsed") |
| Busy labels | Moving… · Saving… · Adding… · Archiving… · Restoring… | **Proposed** (`primary-actions.md` busy pattern) |
| Announcements | Moved up to number 2 of 5 · Moved down to number 4 of 5 · Already at the top · Already at the bottom · Goal saved · Goal added to the end of the list · Goal archived · Goal restored to the end of the list (and "Item …" on 4.9, counted within the group, plus "Item saved and moved to the end of [group name]"). One phrase each, no full stop (microcopy.md). | **Proposed** |
| Request errors | One verb per action: Not moved · Not added · Not saved · Not archived · Not restored, then ": no connection. Try again." or ": something went wrong. Try again." | microcopy.md §9 (editorial pass; "Not saved" after a failed Move is not literally true) |
| Edit conflict | Someone else changed this goal while you were editing. It now says: [text]. Save to replace it with your wording, or Cancel to keep it. ("this item" on 4.9) | **Proposed** |
| List conflict | Someone else changed this list just now. Check it, then try again. | **Proposed** |
| Load failure | Superseded by microcopy.md §9: "This participant did not load: [cause]. Try again." (Goals load with the participant, participants.md) · "Common items did not load: [cause]. Try again." · Button: Try again | microcopy.md §9 |

Do **not** add design.md 3.7's rule ("Rewording is for typos and clarifications; for a genuinely different goal,
archive the old one and add a new one") to the screen. It is not on-screen copy in 4.8 or 4.9. It belongs in
manager training. **[Opinion]**

### Accessibility

**Semantics.**
- The active list is `<ol>`, and each row is `<li>` holding the text in a `<p>`. Keep the `<li>` as
  `display: list-item`, so the native number and the list semantics both stay. Put any grid layout on an inner
  `<div>`. Don't use `list-style: none`: WebKit then drops list semantics unless `role="list"` is added back.
- Archived rows are a `<ul>` inside `<details>`, with a plain-text `<summary>` and its **default marker** kept.
- Every button is `<button type="button">`, except Add and Save, which are `type="submit"` in their own
  `<form noValidate>`. Each textarea has a `<label for>`.
- The help text is linked to the Add and Edit textareas with `aria-describedby` (with the count hint), because it
  is what a manager most needs to know while rewording.

**ARIA only where needed.** `aria-disabled="true"` for unavailable and busy buttons. `aria-invalid` and
`aria-describedby` on fields in error. `tabIndex={-1}` on the conflict message so it can take focus. Use no
`role="listbox"`, `grid`, `toolbar` or `menu`, no `aria-label` (hidden text instead), and no `aria-expanded` on Edit
(it replaces the row; it doesn't disclose anything).

**Keyboard.** Tab and Shift+Tab move through rows in reading order: Move up, Move down, Edit, Archive, then the next
row, then the add field, Add, the summary, and the Restore buttons. Enter or Space activates. Enter or Space on the
summary opens and closes it. There are no arrow-key or modifier shortcuts (SC 2.1.4, and the simplicity rule).
Enter in a textarea makes a new line, as normal. It is never intercepted, so IME and dictation work. Line breaks
become spaces on save.

**Screen reader announcements** (expected; exact wording varies by screen reader and is not verified):

| Moment | Expected |
|---|---|
| Tab onto row 2's Move up | "Move up, Catches the bus to day program…, button" |
| Press it (row 2 becomes row 1) | Focus stays. Polite: "Moved up to number 1 of 3" Some readers also repeat the button name, because focus is set again. |
| Tab onto Move up on row 1 | "…button, dimmed" (VoiceOver) or "unavailable" (NVDA) |
| Press it | Polite: "Already at the top." |
| Press Archive | Focus: "Restore, Phones her sister, button". Polite: "Goal archived." |
| Press Restore | Focus: "Archive, Phones her sister, button". Polite: "Goal restored to the end of the list." |
| Press Edit | Focus: "Edit goal, edit text, multi-line, [current text], Changes apply to notes…, You can enter up to 200 characters" |
| Save | Focus: "Edit, [new text], button". Polite: "Goal saved." |
| Add | Focus stays in the now-empty field. Polite: "Goal added to the end of the list." |
| Summary | "Archived goals, collapsed" plus "button" or "disclosure triangle", depending on the reader |

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships (`<ol>`, labels) · 1.3.2 Meaningful Sequence (the DOM
order is the visual order, SCR27) · 1.4.1 Use of Color (unavailable uses a dashed border as well as colour, errors
use text and a bar) · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text and 1.4.10 Reflow (wrapping flex rows,
no fixed heights) · 1.4.11 Non-text Contrast (borders and focus at least 3:1) · 1.4.12 Text Spacing · 2.1.1
Keyboard · 2.4.3 Focus Order (focus follows the item; never dropped on `<body>`) · 2.4.6 Headings and Labels · 2.4.7
Focus Visible · 2.4.11 Focus Not Obscured (Minimum) (nothing sticky) · 2.5.3 Label in Name (hidden text after the
visible label) · 2.5.7 Dragging Movements (no dragging) · 2.5.8 Target Size (Minimum) (44 px against 24 px) · 3.3.1
Error Identification · 3.3.2 Labels or Instructions (limit and help text) · 3.3.3 Error Suggestion · 4.1.2 Name,
Role, Value · 4.1.3 Status Messages (results that don't move focus go to the status region).

### Implementation notes (React 19 + native HTML + CSS Modules)

- **One component for both lists:** `ListEditor` with `kind: 'goal' | 'item'`, an API adapter and `readOnly`. The
  page renders the heading (`<h2>Goals</h2>` on 4.8; on 4.9 the page `<h1>Common items</h1>` once and an `<h2>` per
  group). Native elements cover everything, so there is no React Aria.
- **Groups (4.9):** one `ListEditor` per active group. Its adapter carries the `groupId` (add and reorder are scoped
  to it; the order request holds only that group's active IDs), its `query` reads the group's rows from the page's
  one grouped query `['admin', 'common-item-groups']` with `select`, and its `scopeId` is `'common-items'` for every group,
  so item and group writes on the page run one at a time. The edit form renders the Group radios from the page's
  list of active groups when there are two or more; a Save that changes the group sends `{text, groupId}` and
  focuses the item's **Edit** in its new group's list after the re-read (the focus map is shared by the page).
- **Reuse the shared pieces:** the button with `unavailable`, `onUnavailablePress`, `busy` and `busyLabel`
  (`primary-actions.md`; imported as `ActionButton` in `review-panel.md`), `CharacterCount`
  (`conditional-reveal.md`), the page status region (`PageStatus`, app-shell.md; `primary-actions.md`), `limits.ts` (200 for both,
  A6), and the field and error markup from `form-validation.md`. No `maxLength` on the textareas.
- **Writes: one scope, send then re-read.** Every `useMutation` on a list shares `scope: { id: 'goals:<participantId>' }`
  (or `'common-items'`), so writes run one at a time. Each `mutationFn` reads the cache **when it runs**, sends, then
  `await queryClient.refetchQueries({ queryKey, exact: true })`. The re-read is needed because a reorder rewrites
  `SortOrder` on the active rows (design.md 5.3) and so changes their `RowVersion`. The next Edit, Archive or Restore
  would otherwise send a stale `If-Match` and get a `412` caused by our own reorder.
- **API gaps (closed):** rewording, archiving and restoring need `If-Match` from each row's `RowVersion` (6.6, 6.7).
  design.md 6.6 now returns `rowVersion` (base64) on each goal, each common item and each common item group. Archived
  common items are in the list too: `GET /api/admin/common-item-groups?includeArchived=` returns them.
- **Never rely on React to keep focus on a reordered row.** When a keyed list changes order, React moves some DOM
  nodes with `insertBefore`. Moving the focused node drops focus to `<body>`. For a swap of rows 1 and 2, React moves
  the row that was first, so **Move down on the first row** is the case that loses focus. **[Opinion]**, from how
  React reconciles keyed lists; cover it with a test. Register each button in a `Map` by `"<id>:<action>"` (React 19
  ref callbacks can return a cleanup). In `onSuccess`, set a focus request in state, and focus the button in a
  `useLayoutEffect`, so it happens after the DOM is in its new order and before paint.
- **Edit opens with `flushSync`** so the textarea exists before `focus()` runs, inside the tap. React's docs use
  `flushSync` for exactly this ("force React to update ('flush') the DOM synchronously")
  (https://react.dev/learn/manipulating-the-dom-with-refs). Focusing inside the tap is what lets iOS open the
  keyboard (unverified on current iOS; test it). Put the caret at the end with
  `setSelectionRange(len, len)`.
- **Opening the archived section:** keep `<details>` **uncontrolled** (no `open` prop, so React never fights the
  user's toggling). After a successful Archive, set `detailsRef.current.open = true` in the same layout effect,
  just before focusing Restore. It also covers the case where the section has only just appeared.
- **Busy per row with queued writes:** `useMutation().variables` only knows the latest call. Use `useMutationState`
  filtered by `mutationKey` and `status: 'pending'` to tell which rows have a write waiting or running.
- **Don't hide an empty live region with `display: none`.** A region that isn't in the accessibility tree when its
  text arrives may never be announced. An empty `<div>` already takes no height.
- **Long text:** `overflow-wrap: anywhere` on row text, so a long word or pasted link can't push the page sideways
  at 320 px (SC 1.4.10).
- **Textarea height:** `rows={3}`. Optionally add `field-sizing: content` with `min-block-size` and
  `max-block-size` as an enhancement. Browser support wasn't checked in this pass, so 3 fixed rows is the baseline.
- **Normalise text on the server too.** Trim it, replace line breaks with single spaces, then check 1–200
  characters, so the server and the client agree (`form-validation.md`: the server is the gate).
- **Tests:**
  - Vitest and Testing Library: after Move down on row 1, `document.activeElement` is the moved item's Move down.
  - Pressing an unavailable Move up sends nothing and announces once.
  - Archive opens `<details>` and focuses Restore. Restore focuses Archive on the last row.
  - Add keeps focus in the field and clears it. Save and Cancel return focus to Edit.
  - A `412` keeps the typed text.
  - Playwright with axe on both screens. A manual pass with NVDA + Chrome and VoiceOver on iOS.
  - The M1 check that a manager sets up five goals on a phone without help.

```tsx
// ListEditor.tsx: Goals (design.md 4.8) and Common items (4.9)
import { useId, useLayoutEffect, useRef, useState, type FormEvent } from 'react';
import { flushSync } from 'react-dom';
import { useMutation, useMutationState, useQuery, useQueryClient } from '@tanstack/react-query';
import { ActionButton } from '../ui/ActionButton';     // primary-actions.md
import { CharacterCount } from '../ui/CharacterCount'; // conditional-reveal.md
import { useAnnounce } from '../ui/PageStatus';        // this page's visually hidden role="status" (app-shell.md)
import { LIMITS } from '../lib/limits';
import { isConflict, requestErrorText, type ListApi, type Row } from '../api';
import { COPY, type Kind } from './listEditorCopy';
import s from './ListEditor.module.css';

type Dir = 'up' | 'down';
const clean = (t: string) => t.replace(/\s*[\r\n]+\s*/g, ' ').trim();
const activeOf = (rows: Row[]) => rows.filter((r) => !r.archivedAtUtc).sort((a, b) => a.sortOrder - b.sortOrder);

export function ListEditor({ kind, api, readOnly = false }: { kind: Kind; api: ListApi; readOnly?: boolean }) {
  const c = COPY[kind];
  const qc = useQueryClient();
  const announce = useAnnounce();
  const helpId = useId();
  const { data, status, refetch } = useQuery(api.query); // rows: {id, text, sortOrder, archivedAtUtc, rowVersion}

  const rows = data ?? [];
  const active = activeOf(rows);
  const archived = rows.filter((r) => r.archivedAtUtc)
    .sort((a, b) => b.archivedAtUtc!.localeCompare(a.archivedAtUtc!));         // newest archived first

  // ── focus that follows the item ──────────────────────────────────────────
  const els = useRef(new Map<string, HTMLElement>());
  const reg = (key: string) => (el: HTMLElement | null) => {
    if (!el) return;
    els.current.set(key, el);
    return () => { els.current.delete(key); };                                 // React 19 ref cleanup
  };
  const detailsRef = useRef<HTMLDetailsElement>(null);
  const [focusReq, setFocusReq] = useState<{ key: string; openArchived?: boolean; n: number } | null>(null);
  const focusAfterRender = (key: string, openArchived = false) =>
    setFocusReq((f) => ({ key, openArchived, n: (f?.n ?? 0) + 1 }));
  useLayoutEffect(() => {
    if (!focusReq) return;
    if (focusReq.openArchived && detailsRef.current) detailsRef.current.open = true;
    els.current.get(focusReq.key)?.focus();
  }, [focusReq]);

  // ── writes: one at a time, send then re-read ────────────────────────────
  const scope = { id: api.scopeId };                    // 'goals:<participantId>' or 'common-items'
  const latest = () => qc.getQueryData<Row[]>(api.query.queryKey) ?? [];
  const reread = () => qc.refetchQueries({ queryKey: api.query.queryKey, exact: true });
  const [rowError, setRowError] = useState<{ id: string; text: string } | null>(null);
  const [conflict, setConflict] = useState(false);
  const fail = async (id: string, e: unknown) => {
    if (isConflict(e)) {                                 // 412 precondition.failed, 422 config.order_mismatch
      await reread();
      setConflict(true);
      focusAfterRender('conflict');
    } else {
      setRowError({ id, text: requestErrorText(e, 'moved') });   // "Not moved: no connection. Try again."
    }
  };
  const begin = (id: string) => { setRowError(null); setConflict(false); return id; };

  const move = useMutation({
    mutationKey: [...api.query.queryKey, 'move'], scope,
    mutationFn: async ({ id, dir }: { id: string; dir: Dir }) => {
      const ids = activeOf(latest()).map((r) => r.id);  // the server's order, read when this write runs
      const i = ids.indexOf(id);
      const j = dir === 'up' ? i - 1 : i + 1;
      if (i < 0 || j < 0 || j >= ids.length) return null; // an earlier queued press already got it there
      [ids[i], ids[j]] = [ids[j], ids[i]];
      await api.reorder(ids);                            // PUT …/order → 204
      await reread();                                    // new order and new row versions
      return { n: j + 1, of: ids.length };
    },
    onSuccess: (r, { id, dir }) => {
      focusAfterRender(`${id}:${dir}`);
      if (r) announce(`Moved ${dir} to number ${r.n} of ${r.of}`);   // one phrase, no full stop (microcopy.md)
    },
    onError: (e, { id }) => fail(id, e),
  });

  const archive = useMutation({
    mutationKey: [...api.query.queryKey, 'archive'], scope,
    mutationFn: async (id: string) => {
      const row = latest().find((r) => r.id === id && !r.archivedAtUtc);
      if (!row) throw api.conflictError();
      await api.archive(id, row.rowVersion);             // If-Match
      await reread();
    },
    onSuccess: (_, id) => { focusAfterRender(`${id}:restore`, true); announce(c.archived); },
    onError: (e, id) => fail(id, e),
  });

  const restore = useMutation({
    mutationKey: [...api.query.queryKey, 'restore'], scope,
    mutationFn: async (id: string) => {
      const row = latest().find((r) => r.id === id && r.archivedAtUtc);
      if (!row) throw api.conflictError();
      await api.restore(id, row.rowVersion);
      await reread();
    },
    onSuccess: (_, id) => { focusAfterRender(`${id}:archive`); announce(c.restored); },
    onError: (e, id) => fail(id, e),
  });

  // Which rows have a write waiting or running (queued writes included).
  const pending = useMutationState({
    filters: { mutationKey: api.query.queryKey, status: 'pending' },
    select: (m) => `${String(m.options.mutationKey?.at(-1))}:${JSON.stringify(m.state.variables)}`,
  });
  const isBusy = (action: string, v: unknown) => pending.includes(`${action}:${JSON.stringify(v)}`);

  if (status === 'pending') return <DelayedLoading />;   // "Loading…" after 1 s (app-shell-nav.md)
  if (status === 'error') {
    return (
      <div>
        <p>{c.loadFailed}</p>
        <ActionButton type="button" variant="secondary" onClick={() => refetch()}>Try again</ActionButton>
      </div>
    );
  }

  const say = (id: string) => (rowError?.id === id ? rowError.text : '');

  return (
    <div className={s.editor}>
      {!readOnly && <p id={helpId} className={s.help}>{c.help}</p>}
      {conflict && <p ref={reg('conflict')} tabIndex={-1} className={s.conflict}>{c.listConflict}</p>}

      {active.length === 0 ? (
        <p>{c.empty}</p>
      ) : (
        <ol className={s.list}>
          {active.map((row, i) =>
            readOnly ? (
              <li key={row.id} className={s.row}><p className={s.text}>{row.text}</p></li>
            ) : (
              <EditableRow
                key={row.id} row={row} c={c} api={api} scope={scope} helpId={helpId}
                isFirst={i === 0} isLast={i === active.length - 1}
                reg={reg} error={say(row.id)} reread={reread} latest={latest}
                onSaved={() => { focusAfterRender(`${row.id}:edit`); announce(c.saved); }}
                onClosed={() => focusAfterRender(`${row.id}:edit`)}
                onConflict={() => { setConflict(true); focusAfterRender('conflict'); }}
                busy={{
                  up: isBusy('move', { id: row.id, dir: 'up' }),
                  down: isBusy('move', { id: row.id, dir: 'down' }),
                  archive: isBusy('archive', row.id),
                }}
                onMove={(dir) => move.mutate({ id: begin(row.id), dir })}
                onAtEnd={(dir) => announce(dir === 'up' ? c.atTop : c.atBottom)}
                onArchive={() => archive.mutate(begin(row.id))}
              />
            ),
          )}
        </ol>
      )}

      {!readOnly && (
        <AddForm c={c} api={api} scope={scope} helpId={helpId} reread={reread} onAdded={() => announce(c.added)} />
      )}

      {archived.length > 0 && (
        <details ref={detailsRef} className={s.archived}>
          <summary className={s.summary}>{c.archivedSummary}</summary>
          <ul className={s.archivedList}>
            {archived.map((row) => (
              <li key={row.id} className={s.archivedRow}>
                <p className={s.text}>{row.text}</p>
                {!readOnly && (
                  <>
                    <div role="alert" className={s.alert}>{say(row.id)}</div>
                    <ActionButton
                      ref={reg(`${row.id}:restore`)} type="button" variant="secondary"
                      busy={isBusy('restore', row.id)} busyLabel="Restoring…"
                      onClick={() => restore.mutate(begin(row.id))}
                    >
                      Restore<span className="visually-hidden">, {row.text}</span>
                    </ActionButton>
                  </>
                )}
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  );
}

// EditableRow: the four buttons, or the one-field edit form in their place.
// AddForm: <form noValidate> with label "New goal", <textarea rows={3}>, CharacterCount, an always-rendered
//   role="alert" div and ActionButton type="submit" "Add goal". On success it clears the text, focuses its own
//   textarea and calls onAdded().
// Both validate on submit only (form-validation.md one-field pattern), send clean(text) and use the same scope.
```

```tsx
// Inside EditableRow (abridged): opening the edit form so the keyboard opens on phones
const [editing, setEditing] = useState(false);
const fieldRef = useRef<HTMLTextAreaElement>(null);

function startEdit() {
  flushSync(() => setEditing(true));            // the textarea exists now…
  const el = fieldRef.current;
  if (el) { el.focus(); el.setSelectionRange(el.value.length, el.value.length); } // …so focus lands inside the tap
}

// Normal mode: the buttons, in this order.
<div role="alert" className={s.alert}>{error}</div>
<div className={s.actions}>
  <ActionButton ref={reg(`${row.id}:up`)} type="button" variant="secondary"
    unavailable={isFirst} onUnavailablePress={() => onAtEnd('up')}
    busy={busy.up} busyLabel="Moving…" onClick={() => onMove('up')}>
    <ArrowUp /> Move up<span className="visually-hidden">, {row.text}</span>
  </ActionButton>
  <ActionButton ref={reg(`${row.id}:down`)} type="button" variant="secondary"
    unavailable={isLast} onUnavailablePress={() => onAtEnd('down')}
    busy={busy.down} busyLabel="Moving…" onClick={() => onMove('down')}>
    <ArrowDown /> Move down<span className="visually-hidden">, {row.text}</span>
  </ActionButton>
  <ActionButton ref={reg(`${row.id}:edit`)} type="button" variant="secondary" onClick={startEdit}>
    Edit<span className="visually-hidden">, {row.text}</span>
  </ActionButton>
  <ActionButton ref={reg(`${row.id}:archive`)} type="button" variant="secondary"
    busy={busy.archive} busyLabel="Archiving…" onClick={onArchive}>
    Archive<span className="visually-hidden">, {row.text}</span>
  </ActionButton>
</div>

// Save: if clean(text) === row.text → setEditing(false); onClosed(). Otherwise mutate in the same scope:
//   PUT with the row's current rowVersion (read from latest() when the write runs), then reread().
//   onSuccess → setEditing(false); onSaved()   (both state updates render together; focus goes to Edit)
//   412 → await reread(); if the row is still active, show the edit-conflict text in this form and keep the
//         typed text; otherwise onConflict().
// Cancel: setEditing(false); onClosed().
```

```css
/* ListEditor.module.css */
/* Editorial pass: the wrapping flex row from common-items.md replaces the @container 44rem rules. */
.help { margin-block: 0 1rem; max-inline-size: var(--measure); }

.list { margin: 0; padding-inline-start: 2.25rem; }        /* the native numbers stay */
.list > li::marker { font-weight: 700; font-variant-numeric: tabular-nums; }
.row { padding-block: var(--space-3); border-block-end: var(--border-divider) solid var(--colour-divider); } /* li stays display: list-item */
.rowInner { display: flex; flex-wrap: wrap; align-items: flex-start; gap: var(--space-2) var(--space-4); }
.text { flex: 1 1 16rem; min-inline-size: 0; margin: 0; overflow-wrap: anywhere; }
.side { flex: 0 1 auto; min-inline-size: 0; }               /* the row alert slot (empty = no height), then the actions */
.actions { display: flex; flex-wrap: wrap; gap: var(--space-2) var(--space-4); }
.pair { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.icon { inline-size: 1em; block-size: 1em; flex: none; }    /* fill: currentColor, so it survives forced colours */

.conflict { margin-block: 0 1rem; padding: 0.5rem 0.75rem; border-inline-start: 4px solid var(--error); }
.archived { margin-block-start: 2rem; }
.summary { min-block-size: 2.75rem; padding-block: 0.625rem; cursor: pointer; }  /* keep the default marker */
.archivedList { margin: 0.5rem 0 0; padding-inline-start: 1.25rem; }
.archivedRow { display: flex; flex-wrap: wrap; align-items: flex-start; gap: var(--space-2) var(--space-4);
               padding-block: var(--space-3); border-block-end: var(--border-divider) solid var(--colour-divider); }
```

Focus rings, unavailable, busy, hover and forced-colours styles all come from the shared button
(`primary-actions.md`), so this module adds none.

---

## Per-screen notes

### 4.8 Participant detail: Goals

- The section is an `<h2>` "Goals" between Details and Actions (design 4.8 order). Details has its own Save and its
  own error summary (`form-validation.md`). The goal forms are one-field forms with no page-top summary, so a goal
  error never scrolls the manager up to Details.
- Mutation scope `goals:<participantId>`. Endpoints from design 6.6: `GET …/participants/{id}/goals?includeArchived=true`,
  `POST …/goals`, `PUT /api/admin/goals/{goalId}`, `POST …/goals/{goalId}/archive` and `/restore`, and
  `PUT …/participants/{id}/goals/order`.
- **Add goal** is a primary button (design: "Primary action: Save / Add goal"). The page then has two primary
  buttons, one for each form. Each is the main action of its own form, so this is expected.
- The list needs a participant ID. Render it only on a saved participant's detail page.
- **Archived participant:** the read-only variant. It shows numbered goal texts, no help text, no add form, no
  buttons, and the archived goals (if any) as text with no Restore. The page's read-only banner with **Restore**
  (participant) belongs to the participant detail spec, not to this component.
- **Write past-day note** and **Past notes** in the Actions section are not part of this component.

### 4.8 Participants list

- Not this component. The participants list is alphabetical with an Active / Archived toggle. It has no Move up
  or Move down, and archiving a participant is done from its detail page.

### 4.9 Common items

- The page (common-items.md): `<h1>Common items</h1>` and the 4.9 help text once, then one `<section>` per active
  group, **Every note first**, each with its `<h2>`, the group's buttons (none on Every note), this component for the
  group's items with a "New item in [group name]" field and **Add item** (primary in every group), and the group's
  "Archived items". Then "New group" with **Add group** and "Archived groups". Mutation scope `common-items` for
  every write on the page.
- Endpoints from 6.6: `GET /api/admin/common-item-groups?includeArchived=true` (every group with its items),
  `POST /api/admin/common-item-groups/{groupId}/items` (add at the end of that group), `PUT /api/admin/common-items/{id}`
  (`{text, groupId}`), `POST /api/admin/common-items/{id}/archive` and `/restore`, and
  `PUT /api/admin/common-item-groups/{groupId}/items/order` (one group's IDs). The group endpoints themselves are
  used by the page (common-items.md).
- Items move up and down within their group only; Move up on a group's first row and Move down on its last row are
  unavailable. Moving to another group is done through Edit, and the item goes to the end of that group (A42).
- Announcements and errors say "Item …" to match the design's "Add item" and "Archived items". Field errors keep
  `form-validation.md`'s "Enter the common item" wording.
- Workers can never reach this screen, so it has no read-only variant.
- One change here reaches every participant's next note that shows that group. The help text is the only warning,
  and that is enough. No confirmation dialog: archive, reorder and moves are reversible (`confirm-dialog.md`).

### Where the result shows: note form (4.3)

- The note form lists goals and then common items in exactly this order and wording, frozen per note by the
  snapshot (3.4, 5.7): Every note's items always, and each other group's items once the writer ticks that group
  (group-picker.md). If a manager changes a list while a worker has a note form open but hasn't changed anything
  yet, the worker sees "The goal or common-item list was just changed. Please check your ticks." (4.3). That is
  the note form's job. This component needs nothing extra for it beyond the help text.

---

## Anti-patterns to avoid

- **Drag and drop or drag handles.** They aren't in the design. They need a non-drag alternative anyway (2.5.7),
  clash with scrolling on phones (GOV.UK publishing turns drag off there), and need a learned mode for keyboard and
  screen reader users.
- **Reordering with CSS** (`order`, grid placement or `flex-direction: column-reverse`). The screen and the DOM
  then disagree, which fails 1.3.2 and 2.4.3 (SCR27: reorder the DOM).
- **Letting focus fall to `<body>`** after a move, archive, restore or save. Watch especially for Move down on the
  first row, where React moves the focused node.
- **`disabled` on the end buttons,** or **hiding them** with `display: none`. Either loses focus, and hiding shifts
  the remaining button under the next tap.
- **Icon-only arrow buttons, or `aria-label` instead of hidden text.** Icons alone are unclear to many users, and
  `aria-label` isn't reliably translated.
- **Click-to-edit text, `contenteditable`, or saving on blur.** These hide the fact that the text can be changed,
  and they commit by accident. Use an explicit Edit with explicit Save and Cancel.
- **A separate edit page or a modal for one 200-character field.** That adds a screen the design doesn't have, and
  it takes the order out of view.
- **Optimistic reordering without a reachable rollback,** or announcing "saved" before the server confirms (I5;
  `primary-actions.md` 5.3).
- **A confirmation dialog for Archive or Restore** (`confirm-dialog.md`), or a timed "Undo" toast. A toast whose
  action disappears breaks SC 2.2.1, and Restore is already the permanent undo.
- **A "Delete" button.** There is no hard delete (3.7, A29).
- **Archived rows mixed into the active list as greyed-out rows.** Then it is unclear which rows count towards the
  order and the numbering.
- **A heading or a custom marker inside `<summary>`,** or a JavaScript accordion in place of `<details>`.
- **Assertive (`role="alert"`) announcements for routine results,** live regions created only when needed, or an
  announcement for every keystroke.
- **`maxlength` on the textareas** (it silently cuts pasted and dictated text), or validating while the manager is
  still typing.
- **"Save order" buttons, reorder modes, or "Move to top" and "Move to bottom".** These are features the design
  doesn't list. Lists of about five rows don't need them.

---

## Tensions with decisions

No conflict was found with D1–D47 or with design.md section 4. The API point under Implementation notes (a version
per row in the list responses) is now in design.md 6.6. It doesn't change any decision.

---

## Sources

- W3C, Understanding SC 2.5.7 Dragging Movements (WCAG 2.2): https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html
- W3C, Technique SCR27 "Reordering page sections using the Document Object Model": https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR27
- W3C, Understanding SC 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- W3C WAI-ARIA APG, Developing a Keyboard Interface (focusability of disabled controls; loss of focus): https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- W3C WAI-ARIA APG, Listbox example with rearrangeable options: https://www.w3.org/WAI/ARIA/apg/patterns/listbox/examples/listbox-rearrangeable/
- WHATWG HTML, focus fixup (anchor; wording not re-checked in this pass): https://html.spec.whatwg.org/multipage/interaction.html#focus-fixup-rule
- MDN, ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- MDN, `<details>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/details
- Nielsen Norman Group, P. Laubheimer, "Drag–and–Drop: How to Design for Ease of Use" (February 2020): https://www.nngroup.com/articles/drag-drop/
- Atlassian Design System, Pragmatic drag and drop accessibility guidelines: https://atlassian.design/components/pragmatic-drag-and-drop/accessibility-guidelines
- GOV.UK publishing components, Reorderable list: https://components.publishing.service.gov.uk/component-guide/reorderable_list
- govuk_publishing_components source, reorderable-list.js: https://github.com/alphagov/govuk_publishing_components/blob/main/app/assets/javascripts/govuk_publishing_components/components/reorderable-list.js
- govuk_publishing_components source, _reorderable-list.scss: https://github.com/alphagov/govuk_publishing_components/blob/main/app/assets/stylesheets/govuk_publishing_components/components/_reorderable-list.scss
- GOV.UK Design System, Summary list: https://design-system.service.gov.uk/components/summary-list/
- GOV.UK Design System, Details: https://design-system.service.gov.uk/components/details/
- GOV.UK Design System, Textarea: https://design-system.service.gov.uk/components/textarea/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- MOJ Design System, Add another: https://design-patterns.service.justice.gov.uk/components/add-another/
- MOJ Design System, Add to a list: https://design-patterns.service.justice.gov.uk/patterns/add-to-a-list/
- PatternFly, Inline edit design guidelines: https://www.patternfly.org/components/inline-edit/design-guidelines
- H. Pickering, Inclusive Components, "A Todo List": https://inclusive-components.design/a-todo-list/
- S. O'Hara, "The details and summary elements, again" (12 September 2022): https://www.scottohara.me/blog/2022/09/12/details-summary.html
- A. Roselli, "aria-label Does Not Translate" (2019, updated July 2025): https://adrianroselli.com/2019/11/aria-label-does-not-translate.html
- A. Roselli, "Live Region Support" (14 January 2026): https://adrianroselli.com/2026/01/live-region-support.html
- React Aria, Drag and drop: https://react-aria.adobe.com/dnd
- React docs, Manipulating the DOM with refs (flushSync): https://react.dev/learn/manipulating-the-dom-with-refs
- React docs, flushSync: https://react.dev/reference/react-dom/flushSync
- TanStack Query v5, Mutations (scope): https://tanstack.com/query/v5/docs/framework/react/guides/mutations
- TkDodo, "Concurrent Optimistic Updates in React Query": https://tkdodo.eu/blog/concurrent-optimistic-updates-in-react-query
- Sibling specs in this folder: `primary-actions.md`, `form-validation.md`, `conditional-reveal.md`, `confirm-dialog.md`, `review-panel.md`, `app-shell-nav.md`, `checkbox-list.md`, `session-timeout.md`
