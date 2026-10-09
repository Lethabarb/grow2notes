Where a screen spec differs, the screen spec wins.

# Common item group picker

Component key: `group-picker`. On the note form, the writer ticks which **groups** of common items happened that
day; only those groups' items are shown to tick. The built-in group **Every note** is always shown and is not in the
picker. Defined by D44–D47 (D9 and D11 as amended by D44), design.md 3.1, 3.4 (snapshot, A3), 3.5, 4.3, 4.9, 5.7,
6.3, 6.6, 11.3 and A3, A4, A6, A41–A47. It extends [checkbox-list](checkbox-list.md) (`TickListField`, `TickListRead`) and, on the manager side,
[list-editor](list-editor.md). Global rules are in the [UX README](../README.md).

Evidence grades: **[Research]** studies or usability testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML · **[Convention]**
established design systems · **[Opinion]** reasoned judgement, no direct evidence. Copy marks: **(V)** word for word
from design.md or decisions.md · **(D)** derived from existing design wording · **(P)** proposed. Every (P) string
here on 9 October 2026 was approved as written (D67); a (P) string added later still needs the owner's sign-off.

**Assumed defaults this file builds on** (shared by every file that touches groups, and recorded in design.md §13 as
A41–A47 with A2, A3, A4 and A6 extended; the owner can override any of them by adding a decision):

1. Groups are organisation-wide. Managers add, rename, reorder, archive and restore them; nothing is deleted.
2. Each common item belongs to exactly one group. A manager moves an item to another group from the item's Edit.
3. **Every note** is built in: always first, cannot be renamed, moved or archived. With no items, it is not shown on
   the note.
4. Starting picks (D46) are copied from the participant's most recent **submitted** note, by any author (for a
   manager's past-day note: the most recent submitted note before that date). Archived groups are skipped. With no
   earlier note, nothing is picked.
5. No group has to be picked to submit. Ticks stay optional; Guided notes is still required.
6. Unpicking a group clears that group's ticks on this note; picking it again shows its items unticked.
7. Snapshot (extends A3): when the draft is created, the note stores the Every note group and every active group,
   with their items' wording and order. The picks are part of the note: they autosave with the draft, and an edit
   after submit can change them (versioned like ticks). Old notes keep their groups and wording.
8. The read view, daily report and record export show Every note and the picked groups only, by group name, in the
   configured order, each item ticked or not ticked (D47). "Reports show every item" (A4) now applies within the
   shown groups.
9. No new screens, notifications or settings.

Three further defaults started in this file and are now in design.md §13: a group with no items is not offered in
the picker (A45), the group name limit is 200 characters (A6), and the copied line naming the source note's date
(A47). A47 is an addition, not part of D46: dropping it removes `picksCopiedFrom` and the cue rows below.

---

## Where it's used

| Screen (design.md) | Variant | What differs |
|---|---|---|
| **Note form, new note** (4.3; worker or manager, today) | Picker, editable | Groups from the live lists. Starting picks copied from the participant's most recent submitted note (D46), with the one-line cue naming that note's date. Nothing is saved by opening: the first tick or pick creates the draft with the picks on screen (3.3). |
| **Note form, manager's past-day note** (4.3, 3.8) | Picker, editable | Starting picks come from the most recent submitted note **before** the note date. Same cue. |
| **Note form, continuing a draft** (including "This draft is for Wednesday 30 September 2026…") | Picker, editable | The draft's snapshot groups and saved picks. The cue still shows if the starting picks were copied (the date is kept with the draft). |
| **Note form, lists changed before the first save** (4.3, 5.7) | Picker, editable | Groups reload; picks re-applied by group ID, ticks by item ID; the existing notice (V) "The goal or common-item list was just changed. Please check your ticks." |
| **Note form, editing a submitted note** (4.3, 3.5) | Picker, editable | The version's snapshot groups (including any archived since) and its picks. No cue: nothing is copied in edit mode. Empty text "No common items set" (D, 11.3). |
| **Note form, changed on another device or tab** (4.3, 5.6) | Picker, editable | **Load the other version** replaces picks with ticks and text. **Keep the text on this screen** re-sends the on-screen picks too. |
| **Submit confirmation** (4.3) | Not shown | The dialog shows only name, date and "Flagged for manager: No". |
| *Read view, manager's read-only draft, one version, flagged review (4.4, 4.2, 4.5, 4.6)* | Not the picker | `TickListRead` per shown group under a group-name heading: Every note, then the picked groups in configured order (D47). The question and unpicked groups never appear. |
| *Daily report and record export (11.3, 11.6)* | Not the picker | Same rule as the read view, in the file (D47). |
| **Common items screen** (4.9, managers) | Manager side | Sections per group, group actions, Add group, Add item into a group, moving an item via Edit. See **Manager side**. |

---

## Best practice

### Choosing the pattern: picks first, items after

Three simple native patterns were considered:

- **(a) A tick per group, its items revealed directly under it** (GOV.UK conditional reveal on each group).
- **(b) One list of group ticks, then the picked groups' items** as their own headed lists below it.
- **(c) A `<details>` disclosure per group**, opened to show the items.

Evidence:

- **GOV.UK and NHS keep conditional reveals to one simple input.** GOV.UK: "Keep it simple. If the related question
  is complicated or has more than one part, show it on the next page in the process instead." Its 2021 research with
  assistive technology users: "All users we tested with had no problems completing the task when the conditional
  reveals were kept to a single input", but revealing "multiple form fields complicated the relationship between the
  question and revealed content" (number of participants not published). A group's items are several tick boxes,
  which is the case GOV.UK warns about. This counts against (a). **[Research]**
  [GOV.UK accessibility blog, 2021](https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/)
  · **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) ·
  [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes)
- **For checkboxes, AgDS puts the dependent content after the whole set, not after each box.** "If the same input can
  create multiple conditional revealed fields or content (such as a checkbox input), use one conditional field
  container component and conditional render the children within", positioned "directly after the related set of
  questions". It also says do not "nest conditional field container components". That is pattern (b). **[Convention]**
  [Agriculture Design System, Conditional field container](https://design-system.agriculture.gov.au/components/conditional-field-container)
- **A tick box with indented tick boxes under it already means "tick all" to many users.** In the APG mixed-state
  example, checking the parent checks every child, and the parent shows "partially checked" when only some children are
  ticked. Material's checkbox does the same: "The first checkbox (the parent) will be selected if all children are
  selected". Under (a), a group tick sitting above its own item ticks looks like this widely used pattern, so a tired
  writer may read ticking **Community outing** as ticking everything in it, or expect it to tick itself when every item
  is ticked. **[Standard]** [APG Checkbox (Mixed-State) example](https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/examples/checkbox-mixed/)
  · **[Convention]** [Material Components, Checkbox](https://github.com/material-components/material-components-android/blob/master/docs/components/Checkbox.md)
- **Asking the deciding question first, then showing only what applies, is GOV.UK's branching model.** The Service
  Manual starts forms with "one question they have to answer" per page and uses "'branching' questions so people only
  have to answer questions that are relevant to them". Grow2Notes cannot add a page (no new screens), so (b) keeps the
  same order on one page: the question, then the follow-up lists. **[Convention]**
  [GOV.UK Service Manual, Structuring forms](https://www.gov.uk/service-manual/design/form-structure)
- **The counter-evidence: changes far from the control can go unseen.** NN/g: change blindness is "people's tendency
  to ignore changes in a scene when they occur in a region that is far away from their focus of attention", and it
  advises grouping what changes together. Under (b), a picked group's items appear below the picker, possibly off a
  phone screen. This counts for (a). **[Research]** (NN/g, synthesis of lab work)
  [Budiu, Change Blindness, 2018](https://www.nngroup.com/articles/change-blindness-definition/)
- **Picks are recorded data, so they must be form controls, not disclosures.** The APG disclosure pattern shows or
  hides content; its open state is not a value. The picks are saved with the note and versioned (default 7), and
  D47 prints them. This rules out (c). The same reasoning made **Flag for manager** a checkbox, not a disclosure
  (`conditional-reveal.md`). **[Standard]** [APG Disclosure](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/)
- **Use checkboxes, not switches.** The choice is committed by Submit, not straight away (ui-build control table:
  binary, deferred commit → `<input type="checkbox">`). **[Convention]**

### Labels, order and structure

- **Group each set of tick boxes in a `<fieldset>` with a `<legend>`, and don't nest fieldsets.** H71: "Grouping
  controls is most important for related radio buttons and checkboxes"; "Authors should avoid nesting fieldsets
  unnecessarily." **[Standard]** [H71](https://www.w3.org/WAI/WCAG22/Techniques/html/H71)
- **Headings let screen reader users jump between groups.** 71.6% of 1,539 respondents first "navigate through the
  headings" on a long page. **[Research]** [WebAIM Screen Reader User Survey #10, 2024](https://webaim.org/projects/screenreadersurvey10/)
- **Say how many can be ticked.** GOV.UK: "add a hint explaining this, for example, 'Select all that apply'."
  **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)
- **Order options on purpose.** GOV.UK: alphabetical by default, but "In some cases, it can be helpful to order them
  from most-to-least common options." The manager's configured order allows the most common first. **[Convention]**
  [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)
- **Revealing content is not a change of context; moving focus would be.** WCAG: "Changes in content, such as an
  expanding outline, dynamic menu, or a tab control do not necessarily change the context, unless they also change one
  of the above (e.g., focus)." **[Standard]** [WCAG 2.2, change of context](https://www.w3.org/TR/WCAG22/#dfn-changes-of-context) ·
  [Understanding 3.2.2](https://www.w3.org/WAI/WCAG22/Understanding/on-input.html)
- **`aria-expanded` is allowed on a checkbox, and the controlled content need not be next to it.** MDN lists
  `checkbox` among its roles and says to use `aria-controls` "if the expandable container is not owned by the element
  with `aria-expanded`, but is controlled by it instead". GOV.UK and NHS still list conditional reveals as a known
  4.1.2 gap: "Users are not always notified when a conditionally revealed question is shown or hidden." **[Standard]**
  [MDN aria-expanded](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-expanded) ·
  **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)

### Starting picks copied from the last note (D46)

- **Defaults stick.** In an online experiment (161 respondents), 42% chose to be organ donors when the default was
  "no" and 82% when it was "yes". **[Research]** [Johnson and Goldstein, Do Defaults Save Lives?, Science 2003](https://www.dangoldstein.com/papers/DefaultsScience.pdf).
  Nielsen: "Users click the top hit not because it's any better, but simply because it's first"; defaults should
  reflect a frequent value. **[Research, secondary]** [NN/g, The Power of Defaults, 2005](https://www.nngroup.com/articles/the-power-of-defaults/)
- **GOV.UK and NHS say not to pre-select checkboxes.** Doing so "makes it more likely that users will: not realise
  they've missed a question [or] submit the wrong answer". **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) ·
  [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes). D46 is a decision, so this is
  recorded under **Tensions with decisions**, not acted on.
- **When content is carried forward, label it and name its source.** The Partnership for Health IT Patient Safety
  (systematic review of 51 publications), as adopted by The Joint Commission: "Provide a mechanism to make
  copy-and-paste material easily identifiable" and "Ensure that the provenance of copy-and-paste material is readily
  available. Having the source, context, author, time, and date…" It also warns that copying promotes "error
  propagation". The setting differs (clinical notes, copied text), but the risk is the same: yesterday's activity left
  in today's record. **[Research]** (systematic review) [Tsou et al., Applied Clinical Informatics 2017](https://pure.johnshopkins.edu/en/publications/safe-practices-for-copy-and-paste-in-the-ehr-systematic-review-re) ·
  **[Convention]** [The Joint Commission, Quick Safety Issue 10 (2015, updated 2021)](https://digitalassets.jointcommission.org/api/public/content/9c4646fca14f4cbea2b98a1f0366a496?v=82c80221)
- **A repeated warning fades.** Warnings lose effect from the second exposure (Anderson et al., CHI 2015, already in
  `confirm-dialog.md`). A daily cue will be skimmed, so it cannot be the only safeguard. **[Research]**
  [Anderson et al. 2015](https://scholarsarchive.byu.edu/facpub/9306/)

### Unpicking a group that has ticks

- **Prefer reversibility to confirmation; confirm only when an action is irreversible, costly and rare.** NN/g: "Do
  not use confirmation dialogs for routine actions… the confirmation dialog will lose its power to prevent errors."
  **[Convention]** [NN/g, Confirmation Dialogs](https://www.nngroup.com/articles/confirmation-dialog/) ·
  ui-ux-design cross-cutting rules.
- **SC 3.3.7 Redundant Entry covers information "required to be entered again in the same process".** Ticks are
  optional (A4), so re-ticking after an unpick is not strictly covered. Its intent still favours keeping input; see
  **Tensions**. **[Standard]** [Understanding 3.3.7](https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html)

---

## Recommendation for Grow2Notes

**Pattern (b): Every note first, then one list of group ticks under a plain question, then each picked group's items
as its own headed list.** No nested reveals, no disclosure widgets, no new dialog. Every control is a native tick box
with the same 40 px box and 56 px row as the rest of the form.

Why (b), in one line each:

- **Phones:** the picks sit together in one short list, so a carried-over pick (D46) can be checked at a glance next
  to the cue. Under (a) the picks would be spread down the page between item lists.
- **Screen readers:** one question fieldset, then one fieldset per group with a heading. No nested fieldsets (H71),
  no multi-field reveal (GOV.UK 2021), and each group can be reached by heading.
- **Tired readers, and readers with English as a second language:** a question, then lists, the same shape as
  **1. Goals**. No parent tick box that looks like "tick all" (APG, Material).
- **The cost (change blindness)** is small here: every writer scrolls through the picked groups to reach Guided notes
  and **Submit note**, so the items are met in order even if their appearance was not seen. The hint "Their items show
  below." sets the expectation.

### Anatomy (phone, about 375 px)

```
| 2. Common items                       |  <h2>, no longer inside a legend
| Ticked means done.                    |  <p id="common-hint">, the section's hint (V)
|                                       |
| Every note                            |  <fieldset aria-describedby="common-hint"><legend><h3>
|---------------------------------------|  (h3 = 18 px bold, foundations)
| [x]  Medication prompted              |  TickListField rows: 56 px, 40 px box
|---------------------------------------|
| [ ]  Meal prepared                    |
|---------------------------------------|
|                                       |  24 px (--space-5)
| Which of these happened?              |  <fieldset aria-describedby="picker-hint picker-copied">
| Tick all that happened. Their items   |    <legend><h3> · hint <p id="picker-hint">, 16 px secondary
| show below.                           |
| These ticks are copied from the note  |  <p id="picker-copied">, 18 px, full text colour;
| for Wednesday 30 September 2026.      |  only when at least one group was pre-picked (D46)
| Untick any that did not happen.       |
|---------------------------------------|
| [x]  Community outing                 |  same rows; aria-controls + aria-expanded
|---------------------------------------|
| [ ]  In-home support                  |
|---------------------------------------|
| [x]  Personal care                    |
|---------------------------------------|
|                                       |  24 px
| Community outing                      |  <div id="group-{id}" hidden={!picked}>
|---------------------------------------|    <fieldset aria-describedby="common-hint"><legend><h3>
| [ ]  Travelled by bus or train        |
|---------------------------------------|
| [ ]  Paid for own purchases           |
|---------------------------------------|
|                                       |
| Personal care                         |
|---------------------------------------|
| [ ]  Showered                         |
|---------------------------------------|
|                                       |  32 px section gap
| 3. Guided notes                       |
```

| Part | Spec |
|---|---|
| Section | A plain `<div>` holding `<h2>2. Common items</h2>` (V) and `<p id="common-hint">Ticked means done.</p>` (V). Not a `<section>` with a name (that would add a region landmark), and not a fieldset (it now holds several). |
| Every note | `TickListField` with `headingLevel={3}`, heading **Every note** (V, D45), `aria-describedby="common-hint"`. First, always. Not rendered when the snapshot's Every note group has no items. |
| Picker | One `<fieldset>`, `border: 0; padding: 0; margin: 0; min-inline-size: 0` (checkbox-list rule). `<legend><h3>Which of these happened?</h3></legend>` (V). Hint `<p id="picker-hint">` **Tick all that happened. Their items show below.** (V), in the checkbox-list hint style. Then the cue (below), then one tick row per group with at least one item, in configured order. |
| Cue (D46, A47) | `<p id="picker-copied">` **These ticks are copied from the note for Wednesday 30 September 2026. Untick any that did not happen.** (V). The date is the source note's date, `dateLong` in a `<time dateTime>`. Body size, `--colour-text` (not the secondary grey), no icon, no bar, no dismiss. Included in the fieldset's `aria-describedby`. |
| Group tick row | The checkbox-list row: `<label for>` wrapping `<input type="checkbox" id="pick-{groupId}">` with the shared `.box` class, the group name as a text child, `overflow-wrap: anywhere`, never truncated. Plus `aria-controls="group-{groupId}"` and `aria-expanded={picked}` (the Flag for manager pattern). |
| Picked group | A wrapper `<div id="group-{groupId}">`, **always in the DOM**, `hidden` when not picked. Inside it, `TickListField` with `headingLevel={3}`, the group name as heading, `aria-describedby="common-hint"`, item ids `common-{itemId}`. All wrappers follow the picker, in configured order, never in pick order. |
| Spacing | `--space-5` (24 px) between Every note, the picker and each picked group; `--section-gap` before and after the section, as today. |

### Behaviour

| Action | What happens | Focus | Heard (native) |
|---|---|---|---|
| **Open a new note** | Picks copied per default 4; their groups' items show, **all unticked** (items are never copied). The cue shows if at least one group was pre-picked. Nothing is saved (3.3). | `<h1>` (form rule) | — |
| **Pick a group** | The tick shows at once. Its wrapper loses `hidden`: heading and unticked items appear below the picker. `markChanged()`; the first change creates the draft with all on-screen picks, pre-picks included. No scroll. | Stays on the tick box (3.2.2) | "checked, expanded" |
| **Unpick a group** | The tick clears; the wrapper gets `hidden`; that group's item ticks are removed from form state at once (default 6). `markChanged()`. **No confirmation, no undo, no message.** | Stays on the tick box | "not checked, collapsed" |
| **Pick it again** | Its items show again, unticked. | Stays | "checked, expanded" |
| **Tick an item** | As checkbox-list. | Stays | "checked" |
| **Tab** | Every note items → picker boxes → each picked group's items (DOM order = visual order) → Guided notes. | — | "Which of these happened?, grouping, Tick all that happened. Their items show below. These ticks are copied from the note for Wednesday 30 September 2026. Untick any that did not happen. Community outing, check box, checked, expanded" |
| **Submit note / Save changes** | Picks and ticks are sent with the version. Nothing about groups is validated. The confirmation dialog shows nothing about groups. | As the form | — |

**Why no confirmation when unpicking a group with ticks.** The loss is small and bounded (one group's ticks, re-ticked
in a few taps), it shows straight away where the writer is looking when the group's list is in view, and unpicking
is routine: correcting a carried-over pick is exactly what the cue asks for. A dialog here would fire on ordinary use
and train people to dismiss it (NN/g; Anderson 2015), and the app's dialog list is closed (README: Submit, Discard
draft, Deactivate, Reset sign-in, Guide prompts' leave warning, session warning). Clearing also keeps one rule true:
what is on screen is what is saved. Hidden ticks are never kept or sent, like the hidden flag reason (`flagReason:
null`). **[Opinion]**, on **[Convention]** NN/g.

**Why one line for D46, and not a dialog, tags or "Same as last time?".** The Joint Commission's two asks are
identifiable copied material and visible provenance. The cue does both in one line: it says the ticks are copied and
names the source note's date, so a writer can see a two-week-old pick for what it is. Per-row tags would repeat it on
every row; a dialog would add a screen element and habituate (README dialog list). The real safeguard is structural:
each pre-picked group puts its items in the writer's path, unticked, where a group that did not happen looks wrong.
**[Opinion]**, on **[Convention]** Joint Commission and **[Research]** Tsou 2017.

**Cue rules.**
- Shown on a new note, and on a draft, whenever at least one group was pre-picked (A47). The server works out the
  source date itself and keeps it with the draft (`picksCopiedFrom`, a date or null; the client never sends it), so
  the cue survives the first save and a reload. The worker may
  tick Every note items first and reach the picker later.
- It stays after the writer changes picks: "copied from" stays true, and the second sentence covers the change.
- Never shown in edit mode, the read view, the report or the export.
- No pre-picks (no earlier submitted note, the earlier note picked nothing, or every copied group is archived): no cue.

**Data.**
- Form state: `pickedGroupIds: Set<string>` beside `tickedCommonItemIds: Set<string>`.
- The draft `PUT` and the version `POST` send both. `tickedCommonItemIds` is filtered to Every note and picked
  groups' items, as a guard.
- Picks are part of the working copy everywhere ticks are: autosave, takeover (Keep / Load), lists-changed reload,
  sign-in in place, Cancel in edit mode.

### States

| State | What shows |
|---|---|
| **New note, picks copied** | Every note; picker with the copied groups ticked; the cue; those groups' items below, unticked. |
| **New note, nothing copied** | Every note; picker with nothing ticked; no cue; no group lists. |
| **Draft reopened** | The draft's snapshot groups and saved picks; the cue if `picksCopiedFrom` is set. |
| **Editing a submitted note** | The version's snapshot groups (including any archived since; A3), its picks, no cue. |
| **Every note has no items** | Every note not rendered; the picker comes first in the section. |
| **Only Every note has items** | No picker; Every note only, with its heading (it matches the report, D47). |
| **No items anywhere** | `<h2>2. Common items</h2>` and **No common items set up.** (V); edit mode **No common items set** (D, 11.3). No fieldset. |
| **A group with no items** | Not offered in the picker: there is nothing to tick, and it would print an empty heading. **[Opinion]**; it also avoids an empty fieldset (PowerMapper 2025, in checkbox-list). |
| **Many groups** | All shown as a vertical list, in configured order. No cap, no "Show more", no search (ui-ux-design: never cap a list on memory grounds). |
| **Lists changed** (`409 note.lists_changed`) | Groups reload; picks re-applied by group ID; ticks re-applied by item ID and kept only for Every note and picked groups (an item moved into an unpicked group loses its tick); the existing notice above **1. Goals**. Focus as today: if the focused box was removed, focus goes to the notice. |
| **Changed on another device or tab** | **Load the other version** replaces picks, ticks, text, flag. **Keep** re-sends all on-screen picks. |
| **Save failed / dead-end refusal** | Picks and ticks stay on screen; nothing is unticked or unpicked. |
| **Loading** | Not rendered until `GET …/draft` returns (form rule): no placeholder rows that could be tapped. |
| **Hover / focus / active** | As checkbox-list rows: row tint under `@media (hover: hover)`, 3 px outline on `.box:focus-visible`, no pressed style. |
| **Disabled / error** | None. Every note is never a ticked, disabled row in the picker; it is simply not in it. Picks have no validation. |
| **Read-only** | Not this component. `TickListRead` per shown group (see Per-screen notes). |

### Exact copy (note form)

| Where | Text | Mark |
|---|---|---|
| Section heading | 2. Common items | (V) |
| Section hint | Ticked means done. | (V) |
| Built-in group heading | Every note | (V) D45 |
| Picker legend (`<h3>`) | Which of these happened? | (V) 4.3 |
| Picker hint | Tick all that happened. Their items show below. | (V) 4.3 |
| Cue (only when groups were pre-picked) | These ticks are copied from the note for Wednesday 30 September 2026. Untick any that did not happen. | (V) 4.3, A47 |
| Picked group heading | The group's name, exactly as in the snapshot | — |
| No items anywhere (new note, draft) | No common items set up. | (V) |
| No items (edit mode; read view, report) | No common items set | (D) 11.3 |
| Lists changed | The goal or common-item list was just changed. Please check your ticks. | (V) |

Copy notes:
- **Date-neutral on purpose.** No "today" in the legend or cue: a manager's past-day note and an earlier-day draft are
  not about today. The note date is in the `<h1>`.
- **No name in the cue.** The page is about one participant, the `<h1>` names them, and leaving the name out keeps
  the sentence at 12 words (microcopy: about 15 words, at most 2 sentences, for worker strings).
- **"Tick", not "pick" or "select", on screen.** Tick is the app's word for a tick box (glossary; "select" is banned).
  "Pick" stays the design's word for the concept. Workers never see the word "group": it is not needed to answer the
  question, and in NDIS work "group" also means group supports (D36).
- **"Untick any that did not happen."** The carry-over risk is a stale pick, not a missing one, so the cue names that
  action. It is literal (COGA 4.4.4) and has no negative contraction.

### Accessibility

- **Semantics:** native fieldsets with heading legends, native checkboxes with `<label for>` that also wraps. ARIA
  only: `aria-describedby` on each fieldset, `aria-controls` and `aria-expanded` on each group tick box. No
  `role="checkbox"`, no tri-state, no `aria-live` on rows. `hidden` takes unpicked groups out of the accessibility
  tree and the tab order. [Standard]
- **No announcement per pick.** The native "checked, expanded" or "not checked, collapsed" is enough. No live region
  is added (form rule: never an announcement per tick). The 4.1.2 notification gap is reduced by the hint and
  `aria-expanded`, not closed (see Tensions). [Standard] ARIA22 not needed: nothing is written into a region.
- **Focus never moves** on pick or unpick, and nothing scrolls (3.2.2). The focused element can't be removed by a
  pick: unpicking needs focus on the picker box. Voice Control "Tap Community outing" moves focus to that box first.
- **Headings:** h3 for Every note, the question and each group name, under the h2. Heading navigation reaches every
  group (WebAIM: 71.6%).
- **Voice control:** accessible names equal the visible group names (2.5.3). A group and an item with the same words
  (for example "Personal care") make Voice Control show numbers, which is its normal behaviour.
- **Size and reflow:** rows 56 px, box 40 px, rem sizing, wrapping text, `min-inline-size: 0` on every fieldset
  (1.4.4, 1.4.10, 1.4.12, 2.5.8). Forced colours: the border-drawn tick survives; no new colour meaning (1.4.1).
- **WCAG 2.2 criteria:** 1.3.1, 1.3.2, 1.4.1, 1.4.3, 1.4.4, 1.4.10, 1.4.11, 1.4.12, 2.1.1, 2.4.3, 2.4.6, 2.4.7, 2.4.11
  (the form's `scroll-padding`), 2.5.3, 2.5.8, 3.2.2, 3.3.2, 4.1.2 (known gap).
- **Test (M6):** NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Android Chrome: the spoken legend, hint and cue on
  entering the picker; "expanded/collapsed" on a checkbox (TalkBack support unverified); hidden groups skipped by Tab
  and by swipe. Also 320 px, 200% text, a Windows contrast theme, and "Tap [group name]".

### React and native sketch (React 19, CSS Modules, no React Aria, no form library)

`TickListField` gains two props: `headingLevel` (2 for Goals, 3 inside Common items) and `hintId` (the id its
fieldset is described by). Nothing else in it changes.

```tsx
// CommonItemsField.tsx: section 2 of the note form
import s from './TickList.module.css';
import { TickListField } from './TickListField';
import { dateLong } from '../copy/format';

type Item = { id: string; text: string };
type Group = { id: string; name: string; isEveryNote: boolean; items: Item[] }; // snapshot order

export function CommonItemsField({ groups, picked, ticked, copiedFrom, editMode, onPick, onToggle }: {
  groups: Group[];
  picked: ReadonlySet<string>;
  ticked: ReadonlySet<string>;
  copiedFrom: string | null;          // 'yyyy-mm-dd' of the source note, or null
  editMode: boolean;
  onPick: (group: Group, isPicked: boolean) => void;
  onToggle: (itemId: string, isTicked: boolean) => void;
}) {
  const everyNote = groups.find((g) => g.isEveryNote && g.items.length > 0);
  const others = groups.filter((g) => !g.isEveryNote && g.items.length > 0);

  if (!everyNote && others.length === 0) {
    return (
      <div className={s.section}>
        <h2 className={s.heading}>2. Common items</h2>
        <p>{editMode ? 'No common items set' : 'No common items set up.'}</p>
      </div>
    );
  }

  const showCue = !editMode && copiedFrom !== null;
  return (
    <div className={s.section}>
      <h2 className={s.heading}>2. Common items</h2>
      <p id="common-hint" className={s.hint}>Ticked means done.</p>

      {everyNote && (
        <TickListField headingLevel={3} idPrefix="common" heading="Every note" hintId="common-hint"
          items={everyNote.items} ticked={ticked} onToggle={onToggle} />
      )}

      {others.length > 0 && (
        <fieldset className={s.group} aria-describedby={showCue ? 'picker-hint picker-copied' : 'picker-hint'}>
          <legend className={s.legend}><h3 className={s.subheading}>Which of these happened?</h3></legend>
          <p id="picker-hint" className={s.hint}>Tick all that happened. Their items show below.</p>
          {showCue && (
            <p id="picker-copied" className={s.copied}>
              These ticks are copied from the note for{' '}
              <time dateTime={copiedFrom!}>{dateLong(copiedFrom!)}</time>. Untick any that did not happen.
            </p>
          )}
          {others.map((g) => {
            const id = `pick-${g.id}`;
            const isPicked = picked.has(g.id);
            return (
              <label key={g.id} htmlFor={id} className={s.row}>
                <input id={id} type="checkbox" className={s.box} name="pickedGroup" value={g.id}
                  checked={isPicked} aria-controls={`group-${g.id}`} aria-expanded={isPicked}
                  onChange={(e) => onPick(g, e.currentTarget.checked)} />
                <span className={s.text}>{g.name}</span>
              </label>
            );
          })}
        </fieldset>
      )}

      {others.map((g) => (
        // Always rendered: keeps aria-controls valid; `hidden` removes it from view, tab order and AT
        <div key={g.id} id={`group-${g.id}`} hidden={!picked.has(g.id)}>
          <TickListField headingLevel={3} idPrefix="common" heading={g.name} hintId="common-hint"
            items={g.items} ticked={ticked} onToggle={onToggle} />
        </div>
      ))}
    </div>
  );
}
```

```ts
// In the note form (owner of the state and autosave)
function pickGroup(group: Group, isPicked: boolean) {
  setPicked((prev) => {
    const next = new Set(prev);
    if (isPicked) next.add(group.id); else next.delete(group.id);
    return next;
  });
  if (!isPicked) {
    setTicked((prev) => {                       // default 6: unpicking clears that group's ticks
      const next = new Set(prev);
      for (const item of group.items) next.delete(item.id);
      return next;
    });
  }
  markChanged();                                // same autosave engine; the first change creates the draft
}
// Payload (PUT …/draft and POST …/versions): { …, pickedGroupIds: [...picked],
//   tickedCommonItemIds: [...ticked].filter(isInEveryNoteOrPickedGroup) }
```

```css
/* Additions to TickList.module.css */
.section > * + * { margin-block-start: var(--space-5); }        /* 24 px between the sub-blocks */
.subheading { font-size: var(--font-size-body); font-weight: 700; margin: 0 0 var(--space-1); }
.copied { color: var(--colour-text); margin: 0 0 var(--space-2); max-inline-size: var(--measure); }
[hidden] { display: none !important; }                          /* global base stylesheet, not the module: never let a display rule beat hidden */
```

**API (design.md §5.7, §6.3, §6.6; no gaps left):** the draft response carries the whole snapshot as
`commonItemGroups: [{id, name, isEveryNote, isPicked, items}]` (Every note first, then every group in order, picked or
not); on a new note `isPicked` holds the copied picks, with archived or empty groups skipped. It also carries
`picksCopiedFrom`, which the server works out and stores itself. The `PUT` and the version `POST` send
`pickedGroupIds`. The read API returns only the shown groups as `commonItemGroups: [{name, isEveryNote, items: [{text,
isTicked}]}]`. `listsVersion` covers the common items and their groups, so a group rename, archive, add or item move
before the first save gives `409 note.lists_changed`.

### Per-screen notes

- **Note form (4.3).** Section 2 is `CommonItemsField`, between **1. Goals** and **3. Guided notes**. The note-form
  screen spec's component 9, phone sketch, focus order and announcements gain the picker. Its Conflicts table wins
  over this file.
- **Read view, read-only draft, one version, flagged review (4.4, 4.2, 4.5, 4.6).** `<h2>2. Common items</h2>`, then
  `<h3>Every note</h3>` + `TickListRead`, then `<h3>[group name]</h3>` + `TickListRead` for each picked group in
  configured order. No question, no unpicked groups, no cue. If nothing is shown: **No common items set** (D, 11.3).
- **Daily report and record export (11.3, 11.6).** Under "Common items", each shown group's name as a sub-heading,
  then its items with ☑/☐, in the same order (D47).
- **Version history.** A version shows the groups picked in that version, so a change of picks between versions is
  visible in the version view. Nothing is added to the version list.

---

## Manager side (Common items screen grouping)

The Common items screen (design.md 4.9, `screens/common-items.md`) becomes one section per group. Each section
reuses the existing list editor (`list-editor.md`, `kind: 'item'`) for its items, unchanged: numbered `<ol>`, row
buttons **Move up, Move down, Edit, Archive**, the add form, and the group's own **Archived items**. Group actions
use the same button styles, hidden-context pattern, busy labels, failure lines, conflict handling and one mutation
scope. Nothing is dragged, deleted or confirmed.

### Anatomy (phone)

```
| Common items                          |  <h1>, focused on arrival
| Items in Every note show on every     |  help text (V), design.md 4.9
| note. For other groups, the writer    |
| ticks the ones that happened. Changes |
| apply to notes started from now on.   |
|                                       |
| Every note                            |  <section aria-labelledby> <h2>
| Always shown first, on every note.    |  <p>, secondary text (V); no group actions
|  1. Medication prompted               |  list editor, unchanged
|     [↑ Move up ] [↓ Move down ]       |
|     [ Edit ] [ Archive ]              |
| New item in Every note                |  <label> (V), textarea, character count
| [             Add item              ] |
| ▸ Archived items                      |  per group, only when it has archived items
|                                       |  --section-gap
| Community outing                      |  <h2>
| [↑ Move up ] [↓ Move down ]           |  group actions: 4 secondary buttons, in the
| [ Rename ] [ Archive ]                |  item rows' order; hidden context
|                                       |  ", Community outing group"
|  1. Travelled by bus or train         |
|     [↑ Move up ] [↓ Move down ]       |
|     [ Edit ] [ Archive ]              |
| New item in Community outing          |
| [             Add item              ] |
|                                       |
| ...one section per active group...    |
|                                       |
| New group                             |  <label for="group-new-name"> (V)
| [_________________________________]   |  <input type="text">, no maxlength
| You can enter up to 200 characters    |
| [ Add group ]                         |  secondary (V)
|                                       |
| ▸ Archived groups                     |  <details>, only when a group is archived (P)
```

On a laptop, each group's heading and its four group actions sit on one wrapping flex line, the same technique as the
item rows (`common-items.md` CSS), with no new breakpoint.

### Behaviour

| Action | What happens | Focus afterwards | Announced (polite, page status) |
|---|---|---|---|
| **Rename** (group) | The group buttons give way to a one-field form under the heading; the `<h2>` stays above it, so the region keeps its name and the manager sees what is being renamed. Label **Group name** (V), `<input type="text">` with the current name, character count, **Save** (primary), **Cancel**. Every note has no Rename. | The input, caret at the end | — |
| **Save** (rename) | Validate; unchanged text closes as Cancel. Then `PUT …/groups/{id}` with `If-Match`, re-read. | That group's **Rename** | **Group renamed** (P) |
| **Move up / Move down** (group) | Swap with the neighbouring group; Every note never moves and nothing moves above it. `PUT …/groups/order`, re-read. | The same button of the moved group | **Group moved up** / **Group moved down** (P) |
| **Move up on the first group after Every note / Move down on the last** | `aria-disabled`, no request | Stays | **Already at the top** / **Already at the bottom** (existing) |
| **Archive** (group) | No dialog (reversible, as for items). The section leaves the page with its items, which stop showing on notes started from now on. **Archived groups** appears if needed, opens, and lists it first. | Its **Restore** | **Group archived** (P) |
| **Restore** (group) | Back at the **end** of the groups, with its active items (A2 rule). | Its **Archive** | **Group restored to the end of the list** (P) |
| **Add group** | Validate; `POST …/groups`, re-read. The new, empty section appears last, above the New group field; the field is cleared. | The new group's **New item in [name]** field: the next useful step, because an empty group does not show on notes | **Group added at the end of the list** (P) |
| **Add item** (in a group) | The existing list-editor Add, scoped to that group: the item goes to the end of that group. | Stays in that field | **Item added to the end of the list** (existing) |
| **Edit item → change group** | The item's edit form gains a radio group, legend **Group** (V), listing every active group in order with the current one ticked (the drawn radio from checkbox-list). Save with text and group unchanged closes as Cancel. Otherwise one `PUT …/common-items/{id}` with `{text, groupId}` and `If-Match`; a moved item goes to the **end** of its new group. | That item's **Edit**, now in its new section | **Item saved** (existing), or **Item saved and moved to the end of Personal care** (P) |

- **Radios, not a select, for the group.** GOV.UK: "The select component should only be used as a last resort…
  research shows that some users find selects very difficult to use"; radios keep every group visible.
  **[Convention]** [GOV.UK Select](https://design-system.service.gov.uk/components/select/). Ticking the current
  group is the item's current value, not a guessed pre-selection.
- **Moving through Edit, not a fifth row button.** It keeps the existing four buttons per row (phone pairs) and puts
  the rare action where the item's other properties change. **[Opinion]**
- **Validation** (rename, add group): one-field pattern, on press only: **Enter the group name** (V) and **Group name
  must be 200 characters or less** (V), above the field, focus to the field, no summary.
- **Request failures:** the row or form alert slot: **Not saved: …**, **Not moved: …**, **Not archived: …**, **Not
  restored: …**, **Not added: …**, with cause "no connection" or "something went wrong" (microcopy §4). Conflicts
  (`412`, `422 config.order_mismatch`, `409 config.group_archived`): re-read, then the existing list-conflict message
  above the groups takes focus.
- **Busy labels** after 400 ms: Saving…, Moving…, Archiving…, Restoring…, Adding… (existing pattern).
- **No writes are retried automatically** (no idempotency key on the add endpoints, as today).

### States and copy (manager side)

| State | Text | Mark |
|---|---|---|
| Help text | Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes apply to notes started from now on. | (V) 4.9 |
| Every note line | Always shown first, on every note. | (V) 4.9 |
| A group with no active items | No items in this group. It does not show on notes until it has one. | (V) 4.9 |
| New item label (per group) | New item in [group name] | (V) 4.9 |
| Rename label | Group name | (V) 4.9 |
| New group label / button | New group · Add group | (V) 4.9 |
| Group buttons | Move up · Move down · Rename · Archive · Restore, each with hidden ", [group name] group" | (V) labels, 4.9; the hidden context is (P) |
| Archived sections | Archived items (with hidden " in [group name]") · Archived groups | (P) |
| Edit item radios legend | Group | (V) 4.9 |
| Field errors (rename, add group) | Enter the group name · Group name must be 200 characters or less | (V) 4.9 |

- The old page-level empty string "No common items yet." is gone: design.md 4.9 now uses the per-group line, because
  the page is never empty of groups (Every note is built in).
- **Every note** has no group actions at all (not unavailable buttons): it can never be renamed, moved or archived,
  so there is nothing to explain by pressing. Its one line says why.
- **Add item** stays primary in every section (the same control as today, SC 3.2.4; equal actions may share weight,
  `primary-actions.md`). **Add group** is secondary: rare, and it should not compete with the item fields. **[Opinion]**
- **Accessibility:** each group is a `<section aria-labelledby>` on its `<h2>`. Unlike the note form, a manager
  benefits from a region per group here, and there are few of them **[Opinion]**. The item `<ol>` restarts per group,
  so "number 2 of 5" stays true. Focus never drops to `<body>` (the list-editor rule). The `<details>` summaries are
  plain text with hidden context.
- **API (closed, design.md §6.6):** group endpoints (list with items, archived included; add; rename; order; archive;
  restore), `groupId` on the item `PUT`, item order per group, `rowVersion` per group and per item for `If-Match`, and
  `409 config.group_archived` for an add into, or a move into, a group archived meanwhile.

---

## Anti-patterns

- **Each group's items nested under its own tick box** (pattern a). It reads as a parent "tick all" box (APG mixed
  state, Material), and it is a multi-field reveal (GOV.UK 2021).
- **A tri-state group box,** or ticking a group that ticks its items, or a group that ticks itself when all its items
  are ticked.
- **`<details>`, accordion or disclosure buttons for groups.** Picks are recorded and printed (D47); an open state is
  not data.
- **Switches** for groups (the change is not immediate).
- **Showing every group's items with no picker,** or showing unpicked groups greyed or `disabled`.
- **Every note as a ticked, disabled row in the picker.** No disabled controls; it is simply always there.
- **Pre-ticking items** because they were ticked last time. Only groups are copied (D46); items always start unticked.
- **A dialog, banner, toast or per-row tag for copied picks,** a "Same as last time?" question, or a "Copy last note"
  button.
- **A confirmation or undo toast when unpicking.**
- **Keeping hidden ticks** for an unpicked group and sending them, or remembering picks on the device (D22).
- **Moving focus or scrolling** when a group is picked; announcing each pick in a live region; animating the reveal.
- **Ordering picked groups by tap order,** or sorting the picker alphabetically against the manager's order.
- **"Select all", "None" or search** in the picker; a cap or "Show more" on a long group list.
- **An empty group in the picker,** or an empty fieldset for a group with no items.
- **The words "select", "check", "group" or "today"** in the worker's picker copy.
- **Manager side:** drag and drop between groups, a separate Groups screen, deleting a group, a confirmation on
  Archive, Rename/Move/Archive buttons on Every note, a `<select>` for an item's group.

---

## Tensions with decisions

Recorded with evidence only; **no change is recommended**. The build follows the decisions as written.

1. **D46 (start with the last note's picks) vs "do not pre-select".** GOV.UK and NHS: pre-selection "makes it more
   likely that users will: not realise they've missed a question [or] submit the wrong answer" **[Convention]**.
   Defaults stick (Johnson and Goldstein 2003: 42% vs 82%, n=161) **[Research]**. The risk is a stale pick that stays:
   its items print unticked under its name, which reads as "the outing happened and nothing was done". The cue
   follows the copy-forward safe practice (identifiable, with provenance; Tsou 2017, Joint Commission) and the items
   sit in the writer's path, but a daily cue fades (Anderson 2015).
2. **D44 on one page vs "show complicated follow-ups on the next page".** GOV.UK and NHS say multi-part follow-ups
   belong on a later page, and that conditional reveals are a known WCAG 4.1.2 gap **[Convention]**. Multi-field
   reveals "complicated the relationship between the question and revealed content" (GOV.UK 2021) **[Research]**.
   No new screens is a firm rule here. Placing the lists after the whole set (AgDS), with headings, a hint and
   `aria-expanded`, narrows the gap without closing it.
3. **Assumed default 6 (unpicking clears ticks) vs the flag reason precedent.** The Reason text is kept in memory and
   comes back on re-tick, following SC 3.3.7's intent (`conditional-reveal.md`). Group ticks do not come back. Ticks
   are optional, so 3.3.7 does not strictly apply. This is an assumed default, not a decision, recorded for the owner.
4. **D47 (unpicked groups do not appear).** A missing group cannot show "did not happen" apart from "forgot to tick
   it". This extends the existing "unticked can mean no or missed" tension (D7, D10, A4; README tension 2).

---

## Sources

Standards and specifications
- WCAG 2.2, definition of change of context: https://www.w3.org/TR/WCAG22/#dfn-changes-of-context
- WCAG 2.2 Understanding 3.2.2 On Input: https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- WCAG 2.2 Understanding 3.3.7 Redundant Entry: https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- WCAG 2.2 Understanding 4.1.2 Name, Role, Value: https://www.w3.org/WAI/WCAG22/Understanding/name-role-value.html
- WCAG Technique H71 (fieldset and legend): https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- WAI-ARIA APG, Checkbox (Mixed-State) example: https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/examples/checkbox-mixed/
- WAI-ARIA APG, Disclosure pattern: https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/
- MDN, aria-expanded: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-expanded
- HTML Standard, the hidden attribute: https://html.spec.whatwg.org/multipage/interaction.html#the-hidden-attribute

Research
- GOV.UK accessibility blog, "An update on the accessibility of conditionally revealed questions" (2021): https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/
- WebAIM Screen Reader User Survey #10 (2024, 1,539 responses): https://webaim.org/projects/screenreadersurvey10/
- Johnson and Goldstein, "Do Defaults Save Lives?", Science 2003: https://www.dangoldstein.com/papers/DefaultsScience.pdf
- Tsou et al., "Safe Practices for Copy and Paste in the EHR", Applied Clinical Informatics 2017: https://pure.johnshopkins.edu/en/publications/safe-practices-for-copy-and-paste-in-the-ehr-systematic-review-re
- NN/g, Change blindness (Raluca Budiu, 2018): https://www.nngroup.com/articles/change-blindness-definition/
- NN/g, The Power of Defaults (Jakob Nielsen, 2005): https://www.nngroup.com/articles/the-power-of-defaults/
- Anderson et al., warning habituation, CHI 2015: https://scholarsarchive.byu.edu/facpub/9306/

Design systems and conventions
- GOV.UK Design System, Checkboxes: https://design-system.service.gov.uk/components/checkboxes/
- GOV.UK Design System, Select: https://design-system.service.gov.uk/components/select/
- GOV.UK Service Manual, Structuring forms: https://www.gov.uk/service-manual/design/form-structure
- NHS digital service manual, Checkboxes: https://service-manual.nhs.uk/design-system/components/checkboxes
- Agriculture Design System, Conditional field container: https://design-system.agriculture.gov.au/components/conditional-field-container
- Material Components for Android, Checkbox (parent and child checkboxes): https://github.com/material-components/material-components-android/blob/master/docs/components/Checkbox.md
- NN/g, Confirmation dialogs: https://www.nngroup.com/articles/confirmation-dialog/
- The Joint Commission, Quick Safety Issue 10, "Preventing copy-and-paste errors in EHRs" (2015, updated 2021): https://digitalassets.jointcommission.org/api/public/content/9c4646fca14f4cbea2b98a1f0366a496?v=82c80221

Internal: `checkbox-list.md`, `conditional-reveal.md`, `list-editor.md`, `microcopy.md` (glossary, voice rules,
message patterns), `primary-actions.md`, `foundations.md`, `screens/note-form.md`, `screens/common-items.md`, the
ui-ux-design skill (confirmation vs reversibility, list caps) and the ui-build skill (control table).
