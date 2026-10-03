# Goal and common-item tick lists

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `checkbox-list`. These are the tick-box lists on the note form: **1. Goals** (the participant's own goals, ticked = reached) and, inside **2. Common items** (ticked = done), one list for the built-in **Every note** group and one for each common-item group the writer ticks as having happened (D44, D45). There is also a read-only version used wherever a note is shown and not edited. The lists are defined by D7–D11 (D9 and D11 as amended by D44), D44–D47, design.md 3.1, 3.4, 3.5, 4.3, 4.4, 5.7, 11.3, and A3, A4, A6 and A43–A46.

**Common item groups (D44–D47).** Section 2 is composed by [group-picker.md](group-picker.md) (`CommonItemsField`): Every note first, then the question "Which of these happened?" with one tick per group, then each picked group's items. That file owns the picker, the copied picks (D46) and the rule for which groups are shown. This file supplies the rows: `TickListField` for Every note and each picked group (with `headingLevel={3}` and a shared hint), and `TickListRead` for the shown groups in read-only views. The group tick rows in the picker use this file's row and `.box` styles.

Evidence grades: **[Research]** usability testing or studies · **[Standard]** WCAG 2.2, WAI-ARIA, HTML, CSS, Unicode, browser compatibility data · **[Convention]** established design systems · **[Opinion]** reasoned judgement, no direct evidence.

---

## Where it's used

| Screen (design.md) | Variant | What differs |
|---|---|---|
| **Note form, new note** (4.3; a worker or manager, today) | Editable | The lists are the participant's current active goals, Every note's items, and the items of each group the writer ticks (group-picker.md), all unticked (5.7). Groups may start ticked from the last note (D46), but items never do. The first change of any kind counts as the "first change": it creates the draft and the snapshot (3.3, A3). |
| **Note form, manager's past-day note** (4.3, 3.8) | Editable | Same as a new note. The past-day banner sits above the form, not in this component. |
| **Note form, continuing a draft** (incl. "This draft is for Wed 30 Sep…") | Editable | Loads the draft's snapshot and saved ticks. The wording may differ from today's lists, which is correct (A3). |
| **Note form, "Lists changed before the first save"** (4.3, 5.7) | Editable | The lists reload, group picks are re-applied by group ID and ticks by item ID (kept only for Every note and picked groups), and the message "The goal or common-item list was just changed. Please check your ticks." is shown. |
| **Note form, "Editing submitted note (version 2)"** (4.3, 3.5) | Editable | The list is the snapshot of the version being edited, including any common-item group archived since. It is fixed, so nothing a manager adds now will appear (3.5); the group picks and ticks can change. The empty-list wording changes (see below). |
| **Note form, "This note was changed on another device or tab."** (4.3, 5.6) | Editable | **Load the other version** replaces the group picks and ticks along with the text. **Keep the text on this screen** keeps the picks and ticks on screen too. |
| **Submit confirmation** (4.3) | **Not shown** | The dialog shows only the name, the date and "Flagged for manager: No". The tick lists stay on the form behind the modal dialog, which makes them inert, so they can't change between Submit and confirm. |
| *Read view (4.4), a manager reading someone else's draft (4.2), one version (4.5), flagged-note review (4.6)* | Read-only | Every goal from the note's snapshot, and for common items the Every note group and the groups picked on that note or version, by name, each item "shown as ticked or not ticked" (4.4, D47). It isn't a form control (see Recommendation, read-only). These screens embed the read-only list; the rest of each screen is outside this component. |

---

## Best practice

### Choosing the control

- **Use real checkboxes, not switches, chips or toggle buttons.** Changes take effect only on **Submit note** or **Save changes**. Microsoft: "if the user must click a 'submit' or 'next' button to apply changes, use a check box". **[Convention]** [Microsoft, Toggle switches](https://learn.microsoft.com/en-us/windows/apps/design/controls/toggles) · same rule in the ui-build control table (binary, deferred commit → `<input type="checkbox">`).
- **Use the native `<input type="checkbox">` element.** The First Rule of ARIA says use native HTML when it does the job. A native checkbox gives you the role, the state, Space to toggle, label activation, form participation and forced-colours support for free. APG's whole keyboard model for a checkbox is: "When the checkbox has focus, pressing the Space key changes the state of the checkbox." **[Standard]** [APG Checkbox pattern](https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/)
- **Never pre-tick.** GOV.UK and NHS both say pre-selection makes it more likely users "will not realise they've missed a question" or submit a wrong answer. The design starts every goal and item unticked (5.7). The one exception is D46: a new note starts with the last note's **group** picks (never its item ticks); that decision and its tension are recorded in group-picker.md. **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) · [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes)

### Size and hit area

- **Make the visible box large, because people aim at the box and not the label.** GDS lab research found users "predominantly clicked" the small control rather than its clickable label, out of caution learnt on sites where labels don't work. Grey label backgrounds and hover states did not change this. What worked was larger custom controls, about the height of a text field, which tested well: "people of all confidence levels are clicking these controls quickly and easily". The number of participants isn't published. **[Research]** [GOV.UK design notes, Tim Paul, 30 Nov 2016](https://designnotes.blog.gov.uk/2016/11/30/weve-updated-the-radios-and-checkboxes-on-gov-uk/)
- **The tested size is a 40 px box inside a 44 px touch area.** In GOV.UK Frontend the box is `$govuk-checkboxes-size: 40px`, the touch target is that plus a 4 px gutter (44 px), and the whole label is clickable as well. There is a 24 px "small" variant, but it is only for dense screens such as filters. **[Convention]** [govuk-frontend checkboxes `_mixin.scss`](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/_mixin.scss) · [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)
- **Clicking or tapping the label must toggle the box.** MDN: "That increased hit area for focusing the input provides an advantage to anyone trying to activate it — including those using a touch-screen device." NN/g says the same (2004): select "by clicking on either the button/box itself or its label". **[Standard]** [MDN `<label>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/label) · **[Convention]** [NN/g, Checkboxes vs. Radio Buttons](https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/) (expert guidelines, no study cited)
- **The WCAG minimum is 24 × 24 CSS px. Our own floor (A32) is 44 × 44.** SC 2.5.8 requires "at least 24 by 24 CSS pixels" unless spacing or another exception applies. The Understanding document doesn't say whether a label counts as part of the target. Counting the label is our interpretation: HTML gives a label activation behaviour, so the label is "a region of the display that will accept a pointer action". Even without the label, a 40 px box passes 2.5.8. **[Standard]** [Understanding 2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) · interpretation **[Opinion]**
- **Size alone isn't enough; separate the targets.** NN/g recommends at least 1 cm × 1 cm, based on Parhi, Karlson and Bederson (MobileHCI 2006), and says targets must be "big enough, and then also spaced well enough". **[Research]** [NN/g, Touch Targets on Touchscreens (2019)](https://www.nngroup.com/articles/touch-target-size/)

### Grouping

- **Wrap each list in `<fieldset>` with a `<legend>`.** WCAG H71 is a sufficient technique for 1.3.1: "Grouping controls is most important for related radio buttons and checkboxes." AgDS, GOV.UK and NHS all do this. **[Standard]** [H71](https://www.w3.org/WAI/WCAG22/Techniques/html/H71) · [Understanding 1.3.1](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html) · **[Convention]** [Agriculture Design System, Checkbox accessibility](https://design-system.agriculture.gov.au/components/checkbox/accessibility) (passed accessibility testing in 2022 and 2024)
- **Keep the legend short.** Some screen readers repeat it on every checkbox: in 2013 testing, JAWS spoke the legend "before every 'label for' text" in forms mode, and VoiceOver spoke it after the label. NVDA spoke it once on entering the group. This is old evidence, but the risk hasn't gone away. **[Research, dated]** [Roger Hudson, usability.com.au, 2013](https://usability.com.au/2013/04/accessible-forms-1-labels-and-identification/)
- **A heading may go inside the legend.** In HTML, the content model of `<legend>` is "Phrasing content, optionally intermixed with heading content". GOV.UK puts the page `<h1>` inside the legend for single-question pages. MDN's warning about headings applies to `<label>`, not `<legend>`. **[Standard]** [HTML, the legend element](https://html.spec.whatwg.org/multipage/form-elements.html#the-legend-element) · **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)
- **Never render an empty fieldset.** PowerMapper (December 2025, 40 screen reader and browser combinations) found an empty fieldset announced as an "empty group" in NVDA 2025.3, skipped by older JAWS, and confusing in VoiceOver. Their advice is to avoid the pattern. **[Research]** [PowerMapper, fieldset containing no controls](https://www.powermapper.com/tests/screen-readers/labelling/fieldset-no-controls/)
- **Don't nest fieldsets.** H71: "Authors should avoid nesting fieldsets unnecessarily." **[Standard]** [H71](https://www.w3.org/WAI/WCAG22/Techniques/html/H71)
- **Lay the list out vertically, one item per line.** AgDS: "use a vertical list of options". NN/g: "Lay out your lists vertically, with one choice per line". **[Convention]** [Agriculture Design System, Checkbox](https://design-system.agriculture.gov.au/components/checkbox) · [NN/g](https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/)
- **Give a short hint under the legend that explains what a tick means.** GOV.UK and NHS use hint text to explain the selection rule ("Select all that apply"). **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) · [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes)

### Labels, including long ones

- **Associate the label explicitly as well as wrapping the input.** MDN: implicit association (nesting) is supported by common browser and screen reader pairs, but "not all assistive technologies do", so use `for`. Do both: wrapping gives the whole row as the hit area, and `for`/`id` gives a robust association. **[Standard]** [MDN `<label>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/label)
- **Put nothing interactive inside a label.** MDN: "Don't place additional interactive elements such as anchors or buttons inside a `<label>`." **[Standard]** [MDN `<label>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/label)
- **The accessible name must be the visible text.** SC 2.5.3: "the name contains the text that is presented visually". Speech-input users say the visible label, for example "tap Makes own breakfast". **[Standard]** [Understanding 2.5.3](https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html)
- **Box on the left, label on the right, box pinned to the first line.** GOV.UK and NHS put checkboxes "to the left of their labels". GOV.UK positions the box absolutely at the top of the item, so a label that wraps keeps its box on the first line. **[Convention]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) · [govuk-frontend `_mixin.scss`](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/_mixin.scss)
- **Let long labels wrap, and stop a fieldset from forcing the page wider.** The HTML user-agent stylesheet gives `fieldset` the rule `min-inline-size: min-content`. One long unbroken string in a 200-character goal (A6) can therefore make the page scroll sideways at 320 px, which fails SC 1.4.10 Reflow. **[Standard]** [HTML rendering, fieldset and legend](https://html.spec.whatwg.org/multipage/rendering.html#the-fieldset-and-legend-elements) · [Understanding 1.4.10](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html)

### Visual state and contrast

- **The box border and the tick each need 3:1 contrast.** The 1.4.11 Understanding document uses a checkbox as its example: the border shows that the control exists, and "the black tick shape indicates the state of checked". Once the box is styled by the author, the exemption for the user agent's default look no longer applies. **[Standard]** [Understanding 1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)
- **`accent-color` alone isn't reliable for the tick's contrast.** MDN marks `accent-color` as "Limited availability". Browser-compat-data says Chrome for Android "does not maintain minimum contrast for legibility of the control", and Safari before 26.2 has the same note. **[Standard]** [MDN accent-color](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/accent-color) · [mdn/browser-compat-data accent-color.json](https://github.com/mdn/browser-compat-data/blob/main/css/properties/accent-color.json)
- **Restyling the native input with `appearance: none` keeps it a native checkbox.** The element keeps its role, state, keyboard support and form participation. `appearance` is Baseline widely available (Chrome 84, Firefox 80, Safari 15.4). Pseudo-elements on an `appearance: none` checkbox have worked in Chrome, Safari and Firefox since about 2017. **[Standard]** [web-features: appearance](https://github.com/web-platform-dx/web-features/blob/main/features/appearance.yml) · **[Convention]** [Stephanie Eckles, Pure CSS Custom Checkbox Style](https://moderncss.dev/pure-css-custom-checkbox-style/)
- **Never `display: none` the real input.** It removes the checkbox from the accessibility tree. **[Standard/Convention]** ui-build non-negotiables · [Adrian Roselli, Under-Engineered Custom Radio Buttons and Checkboxen](https://adrianroselli.com/2017/05/under-engineered-custom-radio-buttons-and-checkboxen.html)
- **In forced-colours mode, borders survive and backgrounds and box-shadows don't.** CSS Color Adjust: a colour that is not a system colour "is instead forced to a system color". `background-color` is forced but keeps its alpha channel, and `box-shadow` is dropped. A tick drawn with **borders** (as GOV.UK draws it) survives with no extra code. A tick drawn with a background colour needs a `forced-colors` block. **[Standard]** [CSS Color Adjust 1, properties affected by forced colours](https://drafts.csswg.org/css-color-adjust-1/#forced-colors-properties)
- **Printing drops backgrounds by default; borders print.** The same border-drawn tick prints correctly if someone prints the screen. This follows from browsers' default "print backgrounds off" setting. **[Opinion]**, not separately verified for every browser.

### Read-only display of ticked and not ticked

- **Don't use `disabled` or "read-only" checkboxes to show a record.** HTML's `readonly` "does not apply to" checkboxes (MDN). `disabled` controls "cannot receive focus" and are exempt from contrast rules, so they can legally be faint. Roselli's 2022 tests found `aria-readonly` on a checkbox announced only by NVDA with Firefox, out of 6 screen reader and browser pairs: "you cannot rely on it". His 2024 advice is to show values "as plain text instead of form fields". He also found TalkBack announced read-only fields as "disabled". **[Standard]** [MDN readonly](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly) · **[Research]** (practitioner AT testing) [Roselli, aria-readonly support, Nov 2022](https://adrianroselli.com/2022/11/brief-note-on-aria-readonly-support-html.html) · [Roselli, Avoid Read-only Controls, Nov 2024](https://adrianroselli.com/2024/11/avoid-read-only-controls.html)
- **Give the state in text for screen readers, and in shape (not colour) on screen.** SC 1.1.1 (text alternative for the icon), SC 1.4.1 (not colour alone), and the design's own rule: "Status is always given in words, never by colour alone" (4.0). **[Standard]** [Understanding 1.4.1](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html)
- **Don't use the ☑ and ☐ characters on screen.** In Unicode emoji-data, U+2611 ☑ has the `Emoji` property but U+2610 ☐ does not. On phones the two can therefore come from different fonts, and ☑ can appear as a colour emoji next to a plain-text ☐. The report embeds a font that contains both (11.5), so it doesn't have this problem. Screens should use one inline SVG drawn the same way for both states. **[Standard]** [Unicode emoji-data.txt](https://www.unicode.org/Public/16.0.0/ucd/emoji/emoji-data.txt) · rendering consequence **[Opinion]**, unverified per device
- **If a list is styled with `list-style: none`, add `role="list"`.** Otherwise Safari/VoiceOver drops the list semantics (outside `<nav>`). **[Convention]** [Scott O'Hara, "Fixing" Lists](https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html)

---

## Recommendation for Grow2Notes

Build two small presentational components that share one stylesheet:

- **`TickListField`**: the editable list on the note form, 4.3.
- **`TickListRead`**: the read-only list used by the read view, a manager's read-only draft, a version and flagged-note review.

Neither one fetches data or saves. The note form owns the tick state and the autosave.

### Anatomy: `TickListField` (note form)

```
<fieldset>                                  no visible border; min-inline-size: 0
  <legend><h2>1. Goals</h2></legend>        section heading inside the legend
  <p id="goals-hint">Ticked means reached.</p>    hint, linked by aria-describedby on the fieldset
  <label for=…> [ box ]  Makes own breakfast </label>        one full-width row per item
  <label for=…> [ box ]  Catches bus to day program </label>
  …
</fieldset>
```

| Part | Spec |
|---|---|
| Section | `<fieldset>` with `border: 0; margin: 0; padding: 0; min-inline-size: 0`. One per list; never nested. |
| Legend | `<legend>` containing a heading: `<h2>` "1. Goals" (the form's numbered section, 4.3), or `<h3>` "Every note" or the group's name inside **2. Common items** (`headingLevel={3}`; the `<h2>` "2. Common items" sits outside, group-picker.md). Nothing else goes in the legend, so screen readers that repeat it stay short. |
| Hint | Goals: a `<p>` directly under the legend, "Ticked means reached." (design.md 4.3), linked with `aria-describedby` on the `<fieldset>` (GOV.UK's pattern). Common items: the one section hint "Ticked means done." is rendered once under "2. Common items" by `CommonItemsField`, and each group's fieldset points to it through `hintId`. Body text colour or a secondary colour with at least 4.5:1 contrast. |
| Row | A `<label>` that wraps the input and also has `for`. `display: flex; align-items: flex-start; gap: 0.75rem`, full width, `min-block-size: 3.5rem`, `padding-block: 0.5rem`, a 1 px divider between rows, `cursor: pointer`. The whole row is the tap target: full width and at least 56 px tall at default text size (A32's 44 × 44 is met). |
| Box | The native `<input type="checkbox">` with `appearance: none`: 2.5 rem (40 px) square, `border: 2px solid currentColor`, a surface-coloured background, `flex: none`. It scales with the user's text size because it is sized in rem. |
| Tick | The input's `::before`: an L shape drawn with **borders** in `currentColor`, rotated −45°, hidden when unticked and visible when `:checked`. Because it is made of borders, it survives forced-colours mode and printing. Same idea as GOV.UK. |
| Text | A `<span>` with the item's wording exactly as in the snapshot, as plain text. `overflow-wrap: anywhere`. The first line is centred on the box with `padding-block-start: max(0px, (2.5rem - 1lh) / 2)` (`lh` is Baseline widely available). Text weight and colour never change when the box is ticked. |

**Why a 40 px box?** It is the size GOV.UK tested with users of all confidence levels, and users aim at the box. 24 px (GOV.UK "small") is for dense screens, which this form is not.

### Behaviour

- **Tapping or clicking anywhere on the row toggles the tick.** This is native label activation; there is no JavaScript click handler. Keyboard: **Tab** moves to the next checkbox (every checkbox is its own tab stop) and **Space** toggles. Don't add Enter-to-toggle or arrow-key roving (APG: Space only).
- **The tick appears immediately, from local React state.** Don't wait for the server. Each toggle updates the form's state and starts the existing autosave timer ("about 2 seconds after the last change", 3.4). Don't rely on blur from a checkbox to trigger a save. Safari doesn't focus buttons when they are clicked, by design ([MDN](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/button)), and may do the same for checkboxes (unverified). Saves should follow the change event.
- **If a save fails, the ticks stay on screen.** The save indicator's "Not saved: no connection. Keep this page open; retrying." covers it. Never untick something because a save failed.
- **The very first tick on a new note creates the draft** (3.3). The component doesn't know this; the form's save logic does.
- **No animation.** The tick appears instantly. This is simplest, needs no reduced-motion branch, and ui-build allows 0–100 ms for a checkbox flip anyway. **[Opinion]**
- **Order is the snapshot order** (the manager's `SortOrder`). Never sort it, and never move ticked items to the top.
- **There is no "tick all", "clear all", collapse or search.** None of these is in the design.

### States

| State | Applies to | Spec |
|---|---|---|
| **Default (unticked)** | Field | An empty box with a 2 px `currentColor` border on the surface colour. With body text at 4.5:1 or more, the border easily clears the 3:1 needed for 1.4.11. |
| **Ticked** | Field | The tick is visible inside the box. The shape is the state (SC 1.4.1). No row tint, no strike-through, no bold, no colour change to the text. |
| **Hover** (laptop) | Field | Only under `@media (hover: hover)`: a subtle background tint on the **whole row**, so a mouse user can see which row will toggle when the pointer is over the text far from the box. GOV.UK adds a hover state for the same reason. Text on the tint keeps 4.5:1 contrast. |
| **Focus** | Field | `.box:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }`, at least 3:1 against the background. The ring appears instantly and is never removed. `:focus-visible` keeps it off for mouse and touch. |
| **Active / pressed** | Field | No separate style. The tick appearing on release is the feedback. |
| **Disabled** | — | **Not used.** Ticks are never disabled on the form. A note the user can't edit is shown with `TickListRead`, not with disabled boxes. AgDS: don't "provide disabled options unless unavoidable". |
| **Error** | — | **None.** Ticks are optional (A4, A8), so there is never a "Select at least one" message. The submit error summary covers only Guided notes and the flag reason (4.3). |
| **Loading** | Field | Render no rows until `GET …/draft` has returned. Never show placeholder unticked boxes: they would show the wrong state and could be tapped, which would start a draft with wrong ticks. The page-level loading message covers this wait. |
| **Lists changed** (409 `note.lists_changed`) | Field | The form reloads the lists and re-applies ticks by item ID. Keep `key={item.id}` on rows, so React keeps the DOM node, and therefore focus, for every item that still exists. Show "The goal or common-item list was just changed. Please check your ticks." just above **1. Goals**, in a `role="status"` container that is already in the DOM (empty) when the form mounts. It stays until the user leaves the form, and never times out. If the focused checkbox was removed and focus fell to `<body>`, move focus to that message (`tabindex="-1"`). The user's own tap caused this, so moving focus is acceptable. Otherwise don't move focus. |
| **Empty: no goals** | Field | No fieldset (an empty fieldset confuses screen readers). Render the `<h2>` "1. Goals" and a paragraph: new note or draft, **"No goals set up for Jane yet. A manager can add them."** (4.3, with the participant's given name). When editing a submitted note, use the report's string **"No goals set"** (11.3), because adding goals now can't change this note (3.5). |
| **Empty: no common items** | Field | `<h2>` "2. Common items" and **"No common items set up."** (4.3). When editing a submitted note: **"No common items set"** (11.3). An empty Every note, or a group with no items, renders no `TickListField` at all (group-picker.md, A43, A45). |
| **Read-only** | Read | See the next section. |

### Read-only variant: `TickListRead`

For the read view (4.4), a manager reading someone else's draft (4.2), one version (4.5) and flagged-note review (4.6).

```
<h2>1. Goals</h2>                      same heading text as the form
<ul role="list">
  <li> [tick icon]  Makes own breakfast  <span class="visually-hidden">, ticked</span></li>
  <li> [empty box]  Catches bus to day program  <span class="visually-hidden">, not ticked</span></li>
</ul>
```

- It is **plain text, not form controls.** Every item from the snapshot is shown in order, ticked or not ticked (4.4, A4).
- **Icon:** one inline `<svg aria-hidden="true" focusable="false">`, 1.5 rem. A square outline in `currentColor`, plus a tick path only when ticked. This is the same visual language as the form box and the report's ☑ / ☐, but smaller, with no row dividers, no hover and no pointer cursor, so it doesn't look tappable. The SVG uses `currentColor`, and the CSS Color Adjust UA rule `svg { forced-color-adjust: preserve-parent-color }` makes it follow the forced text colour. Inline SVG elements are not affected by the `img-src 'self'` CSP (9.7); `data:` URIs would be blocked.
- **Screen reader text:** visually hidden ", ticked" or ", not ticked" after the wording. This gives "Makes own breakfast, ticked" (name then state, the same order a native checkbox is read in) and uses the design's own words ("ticked or not ticked", 4.4, 11.3).
- **Unticked items keep full text colour.** Greying them would read as "unavailable" and could drop below 4.5:1.
- **Empty lists:** "No goals set" / "No common items set" (the 11.3 strings), as a paragraph under the heading. For common items, "No common items set" shows once under "2. Common items" when no group is shown.
- **Common items (D47):** one `TickListRead` per shown group, with `headingLevel={3}`: Every note (when it has items), then each group picked on that note or version, in configured order, under its name. Groups not picked are not rendered at all.
- **Keys:** the read API returns `{text, isTicked}` with no ID (6.3), so key by index. The snapshot order is fixed, so this is safe. Groups are keyed by index for the same reason.

### Phone and laptop

- **Phone (first):** single column with the 16 px page gutter; rows run the full column width. At 320 px wide, a 40 px box and 12 px gap leave about 236 px for text. A 200-character goal wraps to several lines, which is fine. Nothing scrolls sideways.
- **Laptop:** the same component, with the row hover tint added. The rows fill the form's column. A comfortable line length on wide screens comes from the page's maximum content width, which the page decides, not this component.
- **Text enlarged to 200%:** everything is in rem and rows use `min-block-size`, never a fixed height, so text spacing overrides (1.4.12) and enlarged text don't clip.

### Copy (all from design.md)

| Where | Text |
|---|---|
| Goals legend / heading | 1. Goals |
| Goals hint | Ticked means reached. |
| Common items heading (outside any legend) | 2. Common items |
| Common items hint | Ticked means done. |
| Built-in group legend / heading | Every note (D45) |
| Other group legends / headings | The group's name, as stored in the snapshot |
| Group picker question, hint and copied line | See group-picker.md |
| Empty goals (new note or draft) | No goals set up for Jane yet. A manager can add them. |
| Empty common items (new note or draft) | No common items set up. |
| Empty goals / common items (editing a submitted note; read-only) | No goals set / No common items set |
| Lists changed | The goal or common-item list was just changed. Please check your ticks. |
| Read-only state (screen reader only) | ticked / not ticked |

### Accessibility

- **Semantics:** native `fieldset`/`legend`, a native `input type="checkbox"` and `label` with both `for` and wrapping. The only ARIA needed is `aria-describedby` on the fieldset for the hint, and `aria-hidden` plus `role="list"` in the read-only list. No `role="checkbox"`, no `aria-checked`, no `aria-readonly`, no `aria-live` on rows.
- **Keyboard:** Tab and Shift+Tab move through the boxes in visual order; Space toggles.
- **Screen reader announcements:** the native state change is spoken automatically when a box is toggled. Don't add a live-region message for each tick. The save indicator is a separate component and shouldn't announce "Saving…" on every tick either. Roughly what to expect (exact wording varies by screen reader and language and is not verified here):
  - Tabbing into Goals with NVDA and Chrome: "1. Goals, grouping, Ticked means reached. Makes own breakfast, check box, not checked". Then Space: "checked".
  - JAWS may repeat "1. Goals" before each box (Hudson 2013), which is why the legend stays short.
  - Read view: "list, 3 items. Makes own breakfast, ticked".
- **Voice control:** "Tap Makes own breakfast" works because the name equals the visible wording (2.5.3).
- **WCAG 2.2 criteria this meets:** 1.1.1 (hidden text for the read-only icon), 1.3.1 (fieldset/legend, label association, real list), 1.3.2 (DOM order = visual order), 1.4.1 (tick shape plus text, not colour), 1.4.3 (label and hint text), 1.4.4 and 1.4.10 (rem sizing, `min-inline-size: 0`, wrapping), 1.4.11 (box border, tick, icon and focus ring at 3:1 or more), 1.4.12 (no fixed heights), 2.1.1 (native keyboard), 2.4.3 (focus order), 2.4.6 (headings and labels), 2.4.7 (visible focus), 2.4.11 (see the next point), 2.5.3 (label in name), 2.5.8 (row target ≥ 44 px, box 40 px), 3.2.2 (ticking never changes context; autosave isn't a change of context), 3.3.2 (legend plus hint), 4.1.2 (native name, role and value).
- **2.4.11 Focus Not Obscured:** if the page has a sticky top bar (back to Today and the save indicator) or a sticky Submit bar, set `scroll-padding-block` on the scroll container to their heights, so a focused row is never hidden under them (technique [C43](https://www.w3.org/WAI/WCAG22/Techniques/css/C43)).
- **Test before release:** NVDA with Chrome (laptop); VoiceOver with Safari on iPhone and TalkBack with Chrome on Android (workers' phones); a Windows contrast theme; 200% text; 320 px width; and Voice Control "Tap <goal wording>".

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria for this component.** Native HTML covers everything. No form library (7.5).
- The checkboxes are **controlled** (`checked` plus `onChange`). The form keeps `tickedGoalIds`, `tickedCommonItemIds` and `pickedGroupIds` as `Set<string>` in state and sends them as arrays in the autosave `PUT` and the submit `POST` (6.3). `tickedCommonItemIds` is sent filtered to Every note and the picked groups' items (group-picker.md).
- IDs: `goal-${item.id}` and `common-${item.id}`. Server item IDs are unique and stable, which also keeps the right rows mounted after a lists reload. `name`/`value` are set for clear semantics and tests; the form is never natively submitted.
- Item wording is rendered as React text children, never with `dangerouslySetInnerHTML`, never auto-linked, and never truncated.
- The Flag for manager tick box (`conditional-reveal.md`) should reuse the same `.box` class, so every tick box in the app looks and behaves the same.

```tsx
// TickListField.tsx
import s from './TickList.module.css';

type Item = { id: string; text: string };

export function TickListField({ idPrefix, heading, headingLevel = 2, hint, hintId, emptyText, items, ticked, onToggle }: {
  idPrefix: 'goal' | 'common';
  heading: string;            // "1. Goals" | "Every note" | a group's name
  headingLevel?: 2 | 3;       // 2 for Goals; 3 inside "2. Common items" (group-picker.md)
  hint?: string;              // "Ticked means reached." (Goals); omitted when hintId points to a shared hint
  hintId?: string;            // "common-hint": the one section hint rendered by CommonItemsField
  emptyText?: string;         // Goals only; empty common-item groups are not rendered (A43, A45)
  items: Item[];
  ticked: ReadonlySet<string>;
  onToggle: (itemId: string, isTicked: boolean) => void;
}) {
  const H = headingLevel === 3 ? 'h3' : 'h2';
  if (items.length === 0) {
    return (
      <div className={s.group}>
        <H className={s.heading}>{heading}</H>
        <p className={s.hint}>{emptyText}</p>
      </div>
    );
  }
  const ownHintId = `${idPrefix}-hint`;
  return (
    <fieldset className={s.group} aria-describedby={hintId ?? ownHintId}>
      <legend className={s.legend}><H className={s.heading}>{heading}</H></legend>
      {!hintId && <p id={ownHintId} className={s.hint}>{hint}</p>}
      {items.map((item) => {
        const id = `${idPrefix}-${item.id}`;
        return (
          <label key={item.id} htmlFor={id} className={s.row}>
            <input id={id} type="checkbox" className={s.box} name={idPrefix} value={item.id}
              checked={ticked.has(item.id)}
              onChange={(e) => onToggle(item.id, e.currentTarget.checked)} />
            <span className={s.text}>{item.text}</span>
          </label>
        );
      })}
    </fieldset>
  );
}
```

```css
/* TickList.module.css (shared by TickListField and TickListRead) */
.group  { border: 0; margin: 0; padding: 0; min-inline-size: 0; }
.legend { padding: 0; }
.heading { margin: 0 0 0.25rem; }
.hint   { margin: 0 0 0.5rem; }

.row {
  display: flex; align-items: flex-start; gap: 0.75rem;
  min-block-size: 3.5rem; padding-block: 0.5rem;
  border-block-start: 1px solid var(--divider);
  cursor: pointer; touch-action: manipulation;   /* GOV.UK does the same */
}
.row:last-of-type { border-block-end: 1px solid var(--divider); }
@media (hover: hover) { .row:hover { background: var(--row-hover); } }

.box {
  appearance: none; margin: 0; flex: none; box-sizing: border-box;
  inline-size: 2.5rem; block-size: 2.5rem;
  border: 2px solid currentColor; border-radius: 0;
  background: var(--surface); color: inherit; cursor: pointer;
  display: grid; place-content: center;
}
.box::before {                       /* the tick, drawn with borders */
  content: ""; box-sizing: border-box;
  inline-size: 1.45rem; block-size: 0.8rem;
  border: solid currentColor; border-width: 0 0 0.3rem 0.3rem;
  transform: translateY(-0.15rem) rotate(-45deg);
  visibility: hidden;
}
.box:checked::before { visibility: visible; }
.box:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }

.text { padding-block-start: max(0px, (2.5rem - 1lh) / 2); overflow-wrap: anywhere; }

/* Read-only list */
.readList { list-style: none; margin: 0; padding: 0; }
.readItem { display: flex; align-items: flex-start; gap: 0.5rem; padding-block: 0.25rem; overflow-wrap: anywhere; }
.icon { flex: none; inline-size: 1.5rem; block-size: 1.5rem; margin-block-start: max(0px, (1lh - 1.5rem) / 2); }
```

```tsx
// TickListRead.tsx (the icon is inline SVG, so the CSP img-src rule is not involved)
export function TickListRead({ heading, headingLevel = 2, emptyText, items }: {
  heading: string; headingLevel?: 2 | 3; emptyText?: string; items: { text: string; isTicked: boolean }[];
}) {
  const H = headingLevel === 3 ? 'h3' : 'h2';   // 3 for Every note and each picked group (D47)
  return (
    <div className={s.group}>
      <H className={s.heading}>{heading}</H>
      {items.length === 0 ? <p>{emptyText}</p> : (
        <ul role="list" className={s.readList}>
          {items.map((item, i) => (
            <li key={i} className={s.readItem}>
              <svg className={s.icon} viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <rect x="2" y="2" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" />
                {item.isTicked && <path d="M6 12.5l4 4 8-9" fill="none" stroke="currentColor" strokeWidth="3" />}
              </svg>
              <span>{item.text}</span>
              <span className="visually-hidden">{item.isTicked ? ', ticked' : ', not ticked'}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
```

Check on real devices that the border-drawn tick lines up inside the box at 100% and 200% text, and in a Windows contrast theme. The values above are a starting point, not tested pixels.

---

## Per-screen notes

**Note form: new note, past-day note, continued draft (4.3).** `TickListField` for Goals, then `CommonItemsField` (group-picker.md), which uses `TickListField` with `headingLevel={3}` and `hintId="common-hint"` for Every note and for each picked group, between the participant header and **3. Guided notes**. A new note shows the live lists, all unticked (groups may start picked from the last note, D46; items never do); a draft shows its saved snapshot, picks and ticks. The past-day and earlier-day banners are above the form and don't touch the lists. The first tick may be the change that creates the draft, so the "It's now after midnight…" refusal (3.3) can follow a tick. The ticks stay on screen like the text does.

**Note form, lists changed (4.3, 5.7).** The only state where the rows themselves change under the user. Re-apply group picks by group ID and ticks by item ID (keeping ticks only for Every note and picked groups), keep focus on surviving rows (stable keys), and show the status message above **1. Goals**. Don't try to highlight which items changed: the design asks the writer to check, and nothing more.

**Note form, editing a submitted note (4.3, 3.5).** The same components, loaded from the version's snapshot, including a group archived since. Only the picks and ticks change; the wording, names and order never do. **Cancel** throws the pending edit away, so the read view shows the version's ticks again. Use the "No goals set" / "No common items set" empty strings here.

**Note form, changed on another device or tab (4.3, 5.6).** Choosing **Load the other version** replaces both the ticks and the text. Choosing **Keep the text on this screen** re-sends the whole working copy, picks and ticks included (the `PUT` is a full replacement). The ticks on screen are never replaced without asking, the same as the text.

**Submit confirmation (4.3).** The tick lists don't appear in the dialog, and nothing should be added. The dialog's job is to stop a note going on the wrong person, and the ticks are on the same page directly behind it. The `<dialog>` is opened with `showModal()`, so the form is inert: the ticks sent with **Submit note for Jane Citizen** are the ticks the user saw. After **Go back**, focus returns to **Submit note** and the ticks are unchanged.

**Read view, manager's read-only draft, version view, flagged review (4.4, 4.2, 4.5, 4.6).** `TickListRead` with the snapshot's goals, then under `<h2>2. Common items</h2>` one `TickListRead` with `headingLevel={3}` per shown group: Every note (when it has items) and each group picked on that note or version, by name, in configured order (D47). Use the same headings as the form ("1. Goals", "2. Common items", "Every note", the group names) so readers see the structure the writer filled in. **[Opinion]**: the downloaded report uses unnumbered "Goals"/"Common items" (11.3), and either is acceptable. Never render disabled checkboxes here.

---

## Anti-patterns to avoid

- `disabled` or `aria-readonly` checkboxes to show a past note. They are faint, skipped by Tab, announced as "disabled" by TalkBack, and `aria-readonly` is barely supported.
- `display: none` or `visibility: hidden` on the real input with a fake box drawn next to it.
- `<div role="checkbox">` or a click handler on a `<div>` row. The native input already does everything.
- Switches or toggle styling: they promise an immediate effect, but nothing is saved as a record until Submit.
- Only the small box being tappable, or a 16–20 px browser-default box on a phone.
- An empty `<fieldset>` and `<legend>` when there are no goals or no common items.
- Hint text, the empty-state message or the item wording put inside the `<legend>` (long legends get repeated).
- Truncating item wording with an ellipsis, or a "show more" toggle. The wording is part of the record (A3).
- Greying, striking through or colour-coding items (green for ticked, red for unticked). It reads as judgement, can fail contrast, and uses colour as meaning.
- Moving ticked items to the top, sorting them, or adding "Tick all" / "Clear all".
- Unicode ☑ / ☐ on screen (mixed emoji and text rendering on phones). They are fine in the report, which embeds its own font.
- `data:` URI tick images in CSS. The CSP's `img-src 'self'` blocks them (9.7). Use borders or inline SVG.
- Waiting for the server before showing a tick, or unticking on a failed save.
- A live-region announcement on every tick.
- Pre-ticking any goal or item, for example from yesterday's note. D46 copies only which common-item groups were picked, never item ticks (group-picker.md); copying a note forward is out of scope (§1).
- Links or buttons inside the row label (for example "Edit goal").

---

## Tensions with decisions

- **Unticked can mean "not reached" or "forgot to tick" (D7, D10, A4).** The decisions make every item a single optional checkbox, with unticked meaning not reached or not done. Form-design evidence says a blank checkbox can't tell "no" apart from "didn't answer". GOV.UK added an explicit "none" checkbox after research across government found users were unsure whether leaving everything blank was allowed, wanted to "give a clear answer", and sometimes skipped questions by accident. **[Research]** (method not detailed) [GOV.UK design notes, Frankie Roberto, 15 Nov 2021](https://designnotes.blog.gov.uk/2021/11/15/letting-users-tick-a-none-checkbox/). Practitioners make the same point about yes/no answers: "an unchecked checkbox could simply mean that the user missed the question". **[Opinion]** [Sara Soueidan, 2020](https://www.sarasoueidan.com/blog/one-checkbox-or-two-radio-buttons/). Noted only. No change is recommended; the hints "Ticked means reached." and "Ticked means done." are the design's own way of making unticked deliberate.
- **D47 extends this to common-item groups.** A group that was not picked does not appear in the read view or the files, so "this activity did not happen" cannot be told apart from "the writer forgot to tick the group". Recorded in group-picker.md; no change recommended.

---

## Sources

Standards and specifications
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 2.5.5 Target Size (Enhanced): https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html
- WCAG 2.2 Understanding 1.4.11 Non-text Contrast: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 Understanding 1.3.1 Info and Relationships: https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG Technique H71 (fieldset and legend): https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- WCAG Technique C43 (scroll-padding): https://www.w3.org/WAI/WCAG22/Techniques/css/C43
- WAI-ARIA APG Checkbox pattern: https://www.w3.org/WAI/ARIA/apg/patterns/checkbox/
- HTML Standard, the legend element: https://html.spec.whatwg.org/multipage/form-elements.html#the-legend-element
- HTML Standard, rendering of fieldset and legend: https://html.spec.whatwg.org/multipage/rendering.html#the-fieldset-and-legend-elements
- CSS Color Adjustment Module Level 1: https://drafts.csswg.org/css-color-adjust-1/
- Unicode emoji-data.txt (16.0): https://www.unicode.org/Public/16.0.0/ucd/emoji/emoji-data.txt
- MDN `<label>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/label
- MDN `readonly`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly
- MDN `<button>` (clicking and focus): https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/button
- MDN `accent-color`: https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/accent-color
- MDN browser-compat-data, accent-color: https://github.com/mdn/browser-compat-data/blob/main/css/properties/accent-color.json
- web-features (Baseline) for `appearance`, `:has`, `lh`: https://github.com/web-platform-dx/web-features

Research
- GOV.UK design notes, "We've updated the radios and checkboxes on GOV.UK" (Tim Paul, 2016): https://designnotes.blog.gov.uk/2016/11/30/weve-updated-the-radios-and-checkboxes-on-gov-uk/
- GOV.UK design notes, "Letting users tick a 'none' checkbox" (Frankie Roberto, 2021): https://designnotes.blog.gov.uk/2021/11/15/letting-users-tick-a-none-checkbox/
- NN/g, Touch Targets on Touchscreens (Aurora Harley, 2019): https://www.nngroup.com/articles/touch-target-size/
- PowerMapper, fieldset containing no controls (Dec 2025): https://www.powermapper.com/tests/screen-readers/labelling/fieldset-no-controls/
- Adrian Roselli, Brief Note on aria-readonly Support (2022): https://adrianroselli.com/2022/11/brief-note-on-aria-readonly-support-html.html
- Adrian Roselli, Avoid Read-only Controls (2024): https://adrianroselli.com/2024/11/avoid-read-only-controls.html
- Roger Hudson, Accessible forms 1: Labels and identification (2013): https://usability.com.au/2013/04/accessible-forms-1-labels-and-identification/

Design systems and conventions
- GOV.UK Design System, Checkboxes: https://design-system.service.gov.uk/components/checkboxes/
- GOV.UK Frontend checkbox styles: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/_mixin.scss
- NHS digital service manual, Checkboxes: https://service-manual.nhs.uk/design-system/components/checkboxes
- Agriculture Design System (Australian Government), Checkbox: https://design-system.agriculture.gov.au/components/checkbox
- Agriculture Design System, Checkbox accessibility: https://design-system.agriculture.gov.au/components/checkbox/accessibility
- Microsoft, Toggle switches (checkbox vs switch): https://learn.microsoft.com/en-us/windows/apps/design/controls/toggles
- NN/g, Checkboxes vs. Radio Buttons (Jakob Nielsen, 2004): https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/
- Stephanie Eckles, Pure CSS Custom Checkbox Style: https://moderncss.dev/pure-css-custom-checkbox-style/
- Adrian Roselli, Under-Engineered Custom Radio Buttons and Checkboxen: https://adrianroselli.com/2017/05/under-engineered-custom-radio-buttons-and-checkboxen.html
- Scott O'Hara, "Fixing" Lists: https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html

Opinion
- Sara Soueidan, "Yes or No?" One Checkbox vs Two Radio Buttons (2020): https://www.sarasoueidan.com/blog/one-checkbox-or-two-radio-buttons/
