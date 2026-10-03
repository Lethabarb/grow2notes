# Flag for manager with reason

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The flag tick box uses checkbox-list.md's 40 px drawn box, not a 24 px box with `accent-color` (note-form.md Conflicts #11).

Component key: `conditional-reveal`. One tick box, **Flag for manager**, which reveals one required text field, **Reason** (up to 200 characters), directly underneath it. Defined by D18, design.md 3.1, 3.4, 3.6, 4.3 and A6, A8, A15.

Evidence grades: **[Research]** usability testing or studies · **[Standard]** WCAG 2.2, WAI-ARIA, HTML · **[Convention]** established design systems · **[Opinion]** reasoned judgement, no direct evidence.

---

## Where it's used

| Screen (design.md) | What the component does there | What differs |
|---|---|---|
| **Note form, new note or draft** (4.3, worker or manager, today) | Tick box under the Guided notes section, Reason revealed when ticked, validated when **Submit note** is tapped | Starts unticked with an empty Reason. Ticking it counts as a change: it starts the draft (3.3) and autosaves like any other tick (3.4). |
| **Note form, manager's past-day note** (4.3, 3.8) | Same | No difference. The past-day banner is above the form, not on this component. |
| **Note form, draft from an earlier day** (4.3) | Same | Loads with the saved tick and reason. |
| **Note form, editing a submitted note** (4.3 "Editing submitted note (version 2)", 3.5) | Same, validated when **Save changes** is tapped | Loads ticked, with the reason, if the version being edited was flagged. Unticking it does **not** take the note out of To review (A15), and changing the reason puts it back in (A15). The form says nothing extra about this; the design gives no copy for it. |
| **Submit confirmation** (4.3) | One line of text: "Flagged for manager: No" or "Flagged for manager: Yes" | Read-only text. No tick box and no field. |
| *(Read-only cases: a manager looking at someone else's draft, or the read view, 4.2 and 4.4)* | Not this component. Show the flag and reason as text, as in the read view (4.4). | Out of scope here; covered by the read-view component. |

---

## Best practice

### The reveal pattern

- **Reveal only a question, and keep it to one simple field.** GOV.UK: "Keep it simple. If the related question is complicated or has more than one part, show it on the next page". Their 2021 work with assistive technology users found "All users we tested with had no problems completing the task when the conditional reveals were kept to a single input." One text field meets this. **[Research]** [GOV.UK accessibility blog, 2021](https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/) · [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) · NHS gives the same guidance: [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes)
- **There is a known accessibility weakness.** GOV.UK and NHS both say: "Users are not always notified when a conditionally revealed question is shown or hidden. This fails WCAG 2.2 success criterion 4.1.2 Name, Role, Value." They also report that screen reader users had no difficulty with simple reveals. **[Research]** [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/) · [NHS Checkboxes](https://service-manual.nhs.uk/design-system/components/checkboxes)
- **Put the revealed field directly after its tick box in the DOM, never anywhere else.** AgDS: place it "directly after the related set of questions", and do not nest reveals. DOM order is what really tells screen reader and keyboard users where the new field is. **[Convention]** [AgDS Conditional field container](https://design-system.agriculture.gov.au/components/conditional-field-container) · **[Standard]** WCAG 2.4.3 Focus Order, 1.3.2 Meaningful Sequence
- **Show the link between the two visually with an indented left bar.** GOV.UK and AgDS both indent the revealed field behind a vertical border. When the revealed field has an error, AgDS changes the bar to the error colour and keeps the indent. **[Convention]** [AgDS](https://design-system.agriculture.gov.au/components/conditional-field-container) · [GOV.UK Checkboxes](https://design-system.service.gov.uk/components/checkboxes/)

### Semantics and ARIA

- **`aria-expanded` is allowed on a checkbox.** ARIA 1.2 lists `checkbox` among the roles that use `aria-expanded` (radio is not on the list). MDN: "Avoid including it on elements that do not control the expanded state of other elements." This tick box does control one. **[Standard]** [MDN aria-expanded](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-expanded)
- **GOV.UK Frontend puts `aria-controls` and `aria-expanded` on the tick box.** It sets `aria-expanded` to the checked state and hides the revealed block with a class, so the field stays in the DOM. GOV.UK kept `aria-expanded` after research because "we think it's useful for screen reader users". **[Convention]** [govuk-frontend checkboxes.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/checkboxes.mjs) · [GOV.UK accessibility blog, 2021](https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/) · history in [govuk-frontend #1991](https://github.com/alphagov/govuk-frontend/issues/1991)
- **Don't rely on `aria-controls` doing anything.** Practitioners report that screen readers do little with it (historically only JAWS acted on it). It does no harm and costs nothing. **[Opinion]**, practitioner, 2016 and older: [Heydon Pickering, "Aria-controls is poop"](https://heydonworks.com/article/aria-controls-is-poop/)
- **It is a checkbox, not a disclosure.** The APG Disclosure pattern (a `<button aria-expanded>`) shows or hides content. Here the tick is itself recorded data (`IsFlagged`), so it must be a real form checkbox. The design has a Submit button, so the choice is not committed straight away, which means a checkbox and not a switch. **[Standard]** [APG Disclosure](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/) · **[Convention]** ui-build control table (binary, deferred commit → checkbox)

### Focus

- **Do not move focus when the box is ticked.** Ticking a checkbox is "changing the setting" of a component. WCAG's definition of a change of context includes changes of "focus", and SC 3.2.2 forbids that on input unless users are warned first. Focus stays on the tick box, and the next Tab goes to Reason because of DOM order. GOV.UK Frontend does not move focus either. **[Standard]** [Understanding 3.2.2 On Input](https://www.w3.org/WAI/WCAG22/Understanding/on-input.html) · **[Convention]** [govuk-frontend checkboxes.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/checkboxes.mjs)

### Keeping the value if unticked and then re-ticked

- **Keep what was typed.** GOV.UK hides the block with CSS and leaves the field's value alone. SC 3.3.7 Redundant Entry (Level A) needs previously entered information to be "auto-populated, or available for the user to select" within the same process, and its Understanding note says this "could be elsewhere on a page, including within a show/hide component". Re-showing the typed reason meets the intent of the rule. Whether 3.3.7 strictly applies within one step is debatable. **[Standard]** [Understanding 3.3.7](https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html) · **[Convention]** [govuk-frontend checkboxes.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/checkboxes.mjs)
- **Never send a hidden value as if it were an answer.** With GOV.UK's approach the hidden value still gets posted, and the server has to ignore it. The design already says "a flagged note has a reason of 1–200 characters, otherwise the reason is null" (design.md 5.8 step 4). **[Convention]** · design.md 5.8

### Character count

- **Use the GOV.UK character count behaviour.** Show the count message below the field. It updates visibly as the user types, and screen reader users "hear the count announcement when they stop typing". The field "does not restrict the user from entering information"; they are told they have "too many characters". It was tested with "17 users, including those with low digital skills and users with disabilities" (2017). **[Research]**, small sample · [GOV.UK Character count](https://design-system.service.gov.uk/components/character-count/)
- **Wording (GOV.UK defaults):** "You can enter up to 200 characters", "You have %{count} characters remaining", "You have %{count} characters too many". The count is `text.length`. **[Convention]** [govuk-frontend character-count.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/character-count/character-count.mjs)
- **How it reaches screen readers:** the visible count is `aria-hidden="true"`, and a separate visually hidden `aria-live="polite"` region is updated after a pause in typing. While the field has focus, GOV.UK also checks the value every 1000 ms ("to detect speech recognition changes") and announces once there has been no input for 500 ms. **[Convention]** [govuk-frontend character-count.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/character-count/character-count.mjs) · **[Standard]** WCAG 4.1.3 Status Messages
- **Don't use `maxlength` on free text.** It cuts off pasted and dictated text without telling the user. govuk-frontend actively removes the attribute. **[Convention]** [Adam Silver on maxlength](https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/) · [govuk-frontend character-count.mjs](https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/character-count/character-count.mjs)
- **Known limitation:** the count "counts some characters as multiple characters. For example, emojis and some non-Latin characters." **[Convention]** [GOV.UK Character count](https://design-system.service.gov.uk/components/character-count/)

### Errors

- **Empty-field wording:** "Enter [whatever it is]". **Too-long wording:** "[whatever it is] must be [number] characters or less". Put the error after the label, add a hidden "Error:" prefix, and show a red border. Avoid "please", "sorry", "valid", "invalid" and "you forgot". **[Convention]** [GOV.UK Error message](https://design-system.service.gov.uk/components/error-message/)
- **Validate empty fields only on submit, and clear an error as soon as it is fixed** ("reward early, punish late"). **[Research]**, moderate: the Etre/Wroblewski inline validation test found validating while typing raised errors. The rule comes via ui-ux-design 09-errors-recovery rules 9–10. The design already sets this timing: errors appear "when Submit is tapped" (4.3).
- **`aria-invalid="true"` only after submit, with the error tied to the field by `aria-describedby`.** MDN: do not set it "on empty required elements until after the user attempts to submit the form". **[Standard]** [MDN aria-invalid](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-invalid)
- **Never show an error by colour alone.** **[Standard]** WCAG 1.4.1 Use of Color

---

## Recommendation for Grow2Notes

### Anatomy

```
[x] Flag for manager                    <- native checkbox, full-width row, at least 44 px tall
 ┃  Reason                               <- <label>, bold
 ┃  Error: Enter a reason for flagging   <- only after Submit / Save changes is tapped
 ┃  this note
 ┃  +--------------------------------+
 ┃  |                                |   <- <textarea rows="4">, no maxlength
 ┃  +--------------------------------+
 ┃  You have 153 characters remaining   <- visible count (aria-hidden)
 ┃                                       <- hidden polite live region (empty until typing pauses)
 ^ 4 px bar under the centre of the tick box; the field lines up with the label text
```

1. **Tick-box row**: the same full-width tick-box row component as Goals and Common items, so the tick size, hit area and focus ring match. It sits in its own block below Guided notes and above **Submit note**, as in the 4.3 sketch, with no heading.
2. **Reveal block** (`hidden` when unticked). It contains, in order: the Reason label, a hidden hint, the error message (when present), the textarea, the visible count, and the hidden live region.

### Behaviour

| Action | Result |
|---|---|
| Tick | The block appears at once, with no animation. Focus stays on the tick box. `aria-expanded` changes to `true`. The reason keeps any earlier text. The change is autosaved on the normal schedule (3.4). |
| Untick | The block is hidden. Focus stays on the tick box. The typed reason **stays in memory** while the form is open. Autosave and submit send `flagReason: null`, so hidden text never reaches the server. Any reason error is cleared, in the field and in the error summary. |
| Re-tick | The earlier text comes back. No error is shown until the next Submit. |
| Type in Reason | The visible count updates on every input. The live region updates once typing pauses for 1 s. If an error is showing, it is re-checked on each change and clears as soon as the field is valid. |
| Tap **Submit note** / **Save changes** | Validation runs for the whole form (4.3). If flagged and the reason is blank or only spaces, show "Enter a reason for flagging this note". If it is over 200 characters, show "Reason must be 200 characters or less". The error shows at the field and in the summary at the top, which links to `#flag-reason`. If there are no errors, the confirmation opens (Submit) or the version saves (Save changes). |
| Load a saved draft or pending edit | The tick and reason come from `GET …/draft` (`isFlagged`, `flagReason`). If ticked, the block is visible from the first render. No count announcement. |
| "Keep the text on this screen" / "Load the other version" (4.3 conflict banner), list change (4.3), sign-in in place after `401` (5.6) | The flag and reason are part of the working copy and are handled with the rest of the form. Nothing special here. |

**Over-typing and autosave.** Over-typing is allowed and the screen keeps everything typed. The **autosave** payload is cut to the first 200 characters, because the draft column is `nvarchar(200)` and the same limit drives request validation (5.1). Without the cut, every autosave would be refused while the reason is too long, and Submit would stay disabled with no explanation. The full text on screen is what gets validated when Submit is tapped. The too-long error then makes the writer shorten it, so a submitted reason is never cut. **[Opinion]**. See Tensions.

### States

| State | Appearance and behaviour |
|---|---|
| **Default, unticked** | Unticked tick box, label "Flag for manager". Block hidden (`hidden` attribute). `aria-expanded="false"`. |
| **Default, ticked, empty** | Block visible. Count shows "You can enter up to 200 characters". No error, no `aria-invalid`. |
| **Hover** (laptop, `@media (hover: hover)` only) | Light background tint on the whole tick-box row, `cursor: pointer`. No hover style on touch. |
| **Focus** | The browser's native focus on the tick box and textarea, plus a solid outline at least 2 px thick with ≥3:1 contrast (`outline`, never only `box-shadow`, which disappears in forced-colours mode). The focus ring must never be hidden by a sticky bar or the on-screen keyboard (2.4.11). |
| **Active** (pressed) | Native checkbox behaviour. No custom pressed style. |
| **Typing, under limit** | "You have N characters remaining" ("1 character" in the singular). |
| **Over limit, before submit** | "You have N characters too many", in the error colour **and bold**, so it does not rely on colour. Not an error yet: no `aria-invalid`, no summary entry. |
| **Error** (after Submit / Save changes) | Error text after the label: hidden "Error: " plus the message. Error-coloured textarea border, and the bar turns the error colour (AgDS). `aria-invalid="true"`. `aria-describedby` includes the error id. The tick box stays ticked and the block stays open. |
| **Disabled** | None. The tick box and field are never disabled. When it is unticked the field is **hidden**, not greyed out. |
| **Loading** | None at component level. The form-level loading state covers it. Don't render the tick box until the draft has loaded, because an unticked box that flips to ticked is misleading, and tapping it would count as a "first change". |
| **Empty** | Same as "Default, ticked, empty". |
| **Read-only** | Not this component. Show it as text in the read view (4.4): "Flagged for manager: Yes" or "No" (copy from 4.3), then the reason with its line breaks. A disabled checkbox is skipped by keyboard and fails contrast checks. |
| **Confirmation dialog** | One line of text: "Flagged for manager: No" (4.3 sketch) or "Flagged for manager: Yes". Don't repeat the reason. The dialog's job is to confirm the participant (3.9), and the reason is on the form just behind it. |

### Phone vs laptop

- **Phone (designed first):** the tick-box row is full width. The textarea fills the indented column, about 250 px of field at 320 px width. Use a font size of at least 16 px (`max(1rem, 16px)`): iOS Safari zooms the page when focusing a field with smaller text. That behaviour is widely reported, but no Apple document was found (**[Convention]**, unverified). Don't scroll automatically on reveal. The block appears right under the user's thumb, and an automatic scroll would be a viewport change on input (3.2.2). **[Opinion]**, **[Standard]**
- **Laptop:** same structure. Cap the textarea at the form's text column (about 40rem). Hover tint on the row. Keep `resize: vertical`.
- **Both:** the layout survives 200% text and 320 px reflow (1.4.4, 1.4.10). The count and error wrap rather than truncate.

### Copy

| Element | Text | Source |
|---|---|---|
| Tick box label | Flag for manager | design.md 4.3 |
| Field label | Reason | design.md 4.3 |
| Hint (hidden, read with the field) and initial visible count | You can enter up to 200 characters | GOV.UK default; limit from A6 |
| Count, under limit | You have 153 characters remaining · You have 1 character remaining | GOV.UK default |
| Count, over limit | You have 12 characters too many · You have 1 character too many | GOV.UK default |
| Error, empty | Enter a reason for flagging this note | **Proposed.** design.md gives no error string. GOV.UK "Enter …" pattern |
| Error, too long | Reason must be 200 characters or less | **Proposed.** GOV.UK "must be … characters or less" pattern |
| Error summary link | Same text as the field error | GOV.UK Error summary convention |
| Confirmation line | Flagged for manager: No · Flagged for manager: Yes | design.md 4.3 sketch ("Yes" is the other value of the same line) |

Plain English, short words, no "please" or "invalid", Australian spelling.

### Accessibility

**Semantics.** Native `<input type="checkbox">` with a `<label>` wrapped around it, and native `<textarea>` with `<label for>`. There is no fieldset: one self-explanatory tick box doesn't need a group legend, and a legend repeating "Flag for manager" would be read twice. **[Opinion]**

**ARIA, only these:**
- On the tick box: `aria-controls="flag-reason-group"` and `aria-expanded={flagged}`. This follows GOV.UK Frontend; ARIA 1.2 allows it on checkbox.
- On the textarea: `aria-describedby` (the error id when present, then the hint id), and `aria-invalid="true"` only after a failed Submit.
- On the visible count: `aria-hidden="true"`. A separate visually hidden `aria-live="polite"` region holds the spoken count. It is **always in the DOM** and starts empty, never created when needed.
- No `aria-label` on either control. The visible labels are the accessible names, which keeps voice control working (2.5.3).

**Keyboard.** Tab reaches the tick box, Space toggles it, the next Tab goes to Reason (only when visible), and Enter inserts a new line in Reason. Line breaks are expected: the Flagged list shows "the first line of the flag reason" (4.6). No custom key handling. Never `preventDefault()` on keys in the textarea, because it breaks IME and dictation.

**Screen reader announcements.** These are approximate; the exact wording varies by screen reader and has not been tested.

| Moment | Expected announcement |
|---|---|
| Focus tick box | "Flag for manager, checkbox, not checked, collapsed" |
| Space | "checked, expanded" |
| Tab to field | "Reason, edit text, multi-line, required, You can enter up to 200 characters" |
| Pause in typing | "You have 140 characters remaining" (polite) |
| After failed Submit, following the summary link | "Reason, invalid entry, required, Error: Enter a reason for flagging this note, You can enter up to 200 characters" |

**WCAG 2.2 criteria this meets:** 1.3.1 Info and Relationships (labels, DOM order) · 1.3.2 Meaningful Sequence · 1.4.1 Use of Color (error has text and weight, over-limit count is bold) · 1.4.3 and 1.4.11 contrast (text 4.5:1, tick box and field borders and focus 3:1) · 1.4.4 Resize Text · 1.4.10 Reflow · 2.1.1 Keyboard · 2.4.3 Focus Order · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (the 44 px row exceeds 24 px; A32) · 3.2.2 On Input (no focus or viewport change on tick) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions (label plus limit shown before typing) · 3.3.3 Error Suggestion (the message says what to do) · 3.3.7 Redundant Entry (reason kept on re-tick) · 4.1.2 Name, Role, Value (native roles, with `aria-expanded` for the reveal; see Tensions for the known gap) · 4.1.3 Status Messages (polite count).

**Testing (adds nothing to scope, matches the design's test stack).** Run axe through Playwright with the box both ticked and unticked. Do manual passes with VoiceOver on iOS Safari, TalkBack on Android Chrome, and NVDA with Chrome. Use phone keyboard dictation into Reason (A31) to confirm React's `onChange` fires and the count updates. GOV.UK checks the value on a timer because some desktop speech recognition software does not fire input events; that is unverified for phone keyboards. Also check at 200% zoom and in forced-colours mode.

### Implementation: React 19, native HTML, CSS Modules

Native HTML is enough here, so no React Aria. State lives in the note form (no form library). The component is controlled and keeps no copy of the reason itself.

```tsx
// FlagForManager.tsx
import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import styles from './FlagForManager.module.css';

export const FLAG_REASON_MAX = 200; // same value as the server's A6 constant
export type FlagReasonError = 'empty' | 'tooLong' | null;

const ERROR_TEXT = {
  empty: 'Enter a reason for flagging this note',
  tooLong: `Reason must be ${FLAG_REASON_MAX} characters or less`,
} as const;

export function countMessage(length: number): string {
  if (length === 0) return `You can enter up to ${FLAG_REASON_MAX} characters`;
  const left = FLAG_REASON_MAX - length;
  const n = Math.abs(left);
  const word = n === 1 ? 'character' : 'characters';
  return left >= 0 ? `You have ${n} ${word} remaining` : `You have ${n} ${word} too many`;
}

type Props = {
  flagged: boolean;
  reason: string;          // kept by the parent even while unticked
  error: FlagReasonError;  // set only when Submit / Save changes is tapped
  onFlaggedChange: (flagged: boolean) => void;
  onReasonChange: (reason: string) => void;
};

export function FlagForManager({ flagged, reason, error, onFlaggedChange, onReasonChange }: Props) {
  const [spoken, setSpoken] = useState('');        // polite live region text
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  function handleReason(e: ChangeEvent<HTMLTextAreaElement>) {
    const next = e.currentTarget.value;
    onReasonChange(next);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setSpoken(countMessage(next.length)), 1000);
  }

  const over = reason.length > FLAG_REASON_MAX;
  const describedBy = error ? 'flag-reason-error flag-reason-hint' : 'flag-reason-hint';

  return (
    <div className={styles.flag}>
      <label className={styles.row} htmlFor="flag-for-manager">
        <input
          id="flag-for-manager"
          type="checkbox"
          className={styles.box}
          checked={flagged}
          aria-controls="flag-reason-group"
          aria-expanded={flagged}
          onChange={(e) => onFlaggedChange(e.currentTarget.checked)}
        />
        Flag for manager
      </label>

      {/* Always in the DOM: keeps the aria-controls target valid and the live region pre-existing */}
      <div id="flag-reason-group" className={styles.reveal}
           data-error={error ? '' : undefined} hidden={!flagged}>
        <label className={styles.label} htmlFor="flag-reason">Reason</label>
        <p id="flag-reason-hint" className="visually-hidden">
          You can enter up to {FLAG_REASON_MAX} characters
        </p>
        {error && (
          <p id="flag-reason-error" className={styles.error}>
            <span className="visually-hidden">Error: </span>{ERROR_TEXT[error]}
          </p>
        )}
        <textarea
          id="flag-reason"            /* fixed id: the error summary links to #flag-reason */
          name="flagReason"
          className={styles.field}
          rows={4}
          required                    /* exposes "required"; the <form> has noValidate */
          value={reason}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          onChange={handleReason}
        />
        <p className={styles.count} data-over={over ? '' : undefined} aria-hidden="true">
          {countMessage(reason.length)}
        </p>
        <div className="visually-hidden" aria-live="polite">{spoken}</div>
      </div>
    </div>
  );
}
```

In the note form (parent):

```ts
// Submit / Save changes: validate the full on-screen text
export function validateFlag(flagged: boolean, reason: string): FlagReasonError {
  if (!flagged) return null;
  if (reason.trim() === '') return 'empty';
  if (reason.length > FLAG_REASON_MAX) return 'tooLong';
  return null;
}

// Autosave (PUT …/draft): never send hidden text; never exceed the column
export function draftReason(flagged: boolean, reason: string): string | null {
  if (!flagged || reason.trim() === '') return null;
  let s = reason.slice(0, FLAG_REASON_MAX);
  if (/[\uD800-\uDBFF]$/.test(s)) s = s.slice(0, -1); // don't split an emoji's surrogate pair
  return s;
}

// POST …/versions after validateFlag() returned null:
//   { …, isFlagged: flagged, flagReason: flagged ? reason : null }
// onFlaggedChange(false) also clears the flag error (field + summary entry).
// While an error is showing, re-run validateFlag on each reason change ("reward early").
```

```css
/* FlagForManager.module.css: token names are placeholders for the app's own tokens */
.row {
  display: flex;
  align-items: center;
  gap: 12px;
  min-block-size: 44px;               /* A32; whole row is the hit area */
  padding-block: 8px;
  cursor: pointer;
}
@media (hover: hover) { .row:hover { background: var(--surface-hover); } }
.box { inline-size: 24px; block-size: 24px; margin: 0; flex: none; accent-color: var(--action); }
.box:focus-visible, .field:focus { outline: 3px solid var(--focus); outline-offset: 2px; }

.reveal {
  margin-block-start: 8px;
  margin-inline-start: 10px;          /* 4 px bar centred under the 24 px tick box */
  padding-inline-start: 22px;         /* field lines up with the label text (24 + 12) */
  border-inline-start: 4px solid var(--border-strong);
}
.reveal[hidden] { display: none; }    /* guard: never let a display rule override [hidden] */
.reveal[data-error] { border-inline-start-color: var(--error); }

.label { display: block; font-weight: 700; margin-block-end: 4px; }
.error { color: var(--error); font-weight: 700; margin-block: 0 4px; }
.field {
  display: block;
  inline-size: 100%;
  max-inline-size: 40rem;
  font: inherit;
  font-size: max(1rem, 16px);         /* avoids iOS focus zoom; grows with user text size */
  line-height: 1.4;
  padding: 8px;
  border: 2px solid var(--border-input); /* ≥3:1 against the page (1.4.11) */
  resize: vertical;
}
.field[aria-invalid='true'] { border-color: var(--error); }
.count { margin-block-start: 4px; color: var(--text-secondary); }
.count[data-over] { color: var(--error); font-weight: 700; }

@media (forced-colors: active) {
  .reveal[data-error] { border-inline-start-width: 8px; } /* error still differs with colours forced */
}
```

Notes:
- **`hidden`, not `{flagged && …}`.** Keeping the block in the DOM keeps the `aria-controls` target valid, keeps the live region in place before anything is written to it, and makes "keep the value" automatic. `hidden` also takes the field out of the tab order and the accessibility tree.
- **`<form noValidate>`** on the note form. Otherwise the browser's own `required` check on a hidden textarea can block submission with its own bubble.
- **No animation.** The reveal is instant, so there is no reduced-motion branch to maintain.
- **Count with `string.length`** (UTF-16 code units). That matches .NET `string.Length` and `nvarchar(200)`, so the client, API and database agree. An emoji counts as 2; GOV.UK has the same known limitation.
- **No `maxlength`, no `placeholder`, no `autocomplete` token.** Use the textarea's default spellcheck and sentence capitalisation, which help writers using English as a second language.
- Use fixed ids, not `useId()`. The error summary needs a stable `#flag-reason` anchor, and there is only one of these per page.
- Don't put the reason text in URLs, titles, console logs or client error reports (4.0, 9.5).
- The server's draft `PUT` must accept `isFlagged: true` with `flagReason: null`, the way a draft's Narrative "may be empty". The reason is required at submit (3.4, A8), not while drafting.

---

## Per-screen notes

**Note form, new or draft (worker, or manager writing today's or a past-day note).**
- Place it exactly as in the 4.3 sketch: its own block after **3. Guided notes**, before **Submit note**.
- Ticking it is a "first change" on a new note, so it starts the draft, the same as ticking a goal (3.3).
- A flag on a draft is not seen by managers (3.6). The form doesn't say so; the design gives no copy for it.
- In the error summary, the reason error comes after the Guided notes error, in page order.

**Note form, editing a submitted note.**
- If the version being edited was flagged, the box loads ticked with the reason in the field, and the block is visible from the first render.
- Validation runs on **Save changes**, with the same rules and the same errors.
- Unticking hides the field and saves `flagReason: null`. The To review status is server logic (A15, 5.8 flag rules) and the form adds no message about it.
- **Cancel** discards the pending edit, flag included (3.5).

**Submit confirmation.**
- Show the line "Flagged for manager: No" or "Flagged for manager: Yes" as plain text in the dialog's reading order, where the 4.3 sketch puts it.
- Base it on the values being submitted (`isFlagged`), not on what was saved last.
- Don't repeat the reason or ask a second question about the flag. Nothing to focus, nothing interactive.

---

## Anti-patterns to avoid

- **Moving focus into Reason when the box is ticked.** This is a change of context on input (3.2.2), and it opens the phone keyboard unasked.
- **Clearing the reason when the box is unticked,** or re-showing it empty on re-tick (3.3.7 intent).
- **Sending a hidden reason** to the server when unticked.
- **`maxlength="200"`**, which cuts off pasted or dictated text without warning.
- **Showing "Enter a reason" as soon as the box is ticked or on blur.** Empty fields are checked only when Submit is tapped (4.3).
- **A greyed-out (disabled) Reason field** shown while unticked. Hide it instead.
- **Using `<details>` or a toggle button** instead of a checkbox. The tick is recorded data, not just a show/hide control.
- **A switch (`role="switch"`)**, which suggests the change takes effect immediately.
- **Creating the live region only when it is needed** (for example `{typing && <div aria-live>}`), or announcing on every keystroke.
- **`aria-label` on the tick box or textarea** that overrides the visible text, which breaks voice control (2.5.3).
- **A placeholder in Reason as the instruction.** The design uses placeholders only for the Guided notes prompts (D12, D34).
- **Animating the height of the reveal.** It costs layout, adds nothing, and needs a reduced-motion branch.
- **Colour alone** for the over-limit count or the error.
- **Adding things the design doesn't specify:** hint text such as "This is not an incident report", example reasons, a reason picker or categories, an "urgent" option, a confirmation for unflagging, or a notice about the review status. D16 is covered by staff onboarding, not by on-screen copy.
- **Rendering the tick box before the draft has loaded,** which shows an unticked box that then flips to ticked.

---

## Tensions with decisions

1. **The character limit versus autosave (A6, design.md 5.1, 5.3 NoteDraft `FlagReason nvarchar(200)`, 5.6).**
   - Best practice is to let people type past the limit, keep every character, and only report the error at submit. Evidence: the GOV.UK Character count ("does not restrict the user from entering information", tested with 17 users) and Adam Silver on `maxlength`.
   - Grow2Notes autosaves the draft about every 2 seconds, into a column capped at 200, using the same limit in request validation. So over-typed text cannot be saved in full, and refused autosaves would keep Submit disabled (3.4).
   - The recommendation above works within the design: cut the **autosave** copy to 200, keep the full text on screen, and block Submit with the too-long error.
   - Remaining risk: if the page crashes while the reason is over the limit, the extra characters are lost. The writer would have had to delete them anyway.
   - No change recommended.
2. **Conditional reveal has a known WCAG 4.1.2 gap (design.md 4.3, "When ticked, a Reason field appears").**
   - GOV.UK and NHS list conditionally revealed questions as a known 4.1.2 failure: users "are not always notified" when one appears.
   - The same sources report that simple, single-field reveals caused no difficulty for screen reader users in testing.
   - This build reduces the gap with `aria-expanded` on the tick box, a single field, and DOM order. It may not close it fully.
   - No change recommended.

---

## Sources

- GOV.UK Design System, Checkboxes (conditional reveal, known issue): https://design-system.service.gov.uk/components/checkboxes/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- GOV.UK Design System, Error summary: https://design-system.service.gov.uk/components/error-summary/
- GOV.UK Accessibility blog, "An update on the accessibility of conditionally revealed questions" (21 Sep 2021): https://accessibility.blog.gov.uk/2021/09/21/an-update-on-the-accessibility-of-conditionally-revealed-questions/
- govuk-frontend source, checkboxes.mjs: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/checkboxes/checkboxes.mjs
- govuk-frontend source, character-count.mjs: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/character-count/character-count.mjs
- govuk-frontend issue #1991, "Improve the accessibility and usability of conditional reveals" (Oct 2020): https://github.com/alphagov/govuk-frontend/issues/1991
- NHS digital service manual, Checkboxes: https://service-manual.nhs.uk/design-system/components/checkboxes
- Agriculture Design System (AgDS), Conditional field container: https://design-system.agriculture.gov.au/components/conditional-field-container
- MDN, aria-expanded: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-expanded
- MDN, aria-invalid: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-invalid
- WAI-ARIA APG, Disclosure pattern: https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/
- WCAG 2.2 Understanding 3.2.2 On Input: https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- WCAG 2.2 Understanding 3.3.7 Redundant Entry: https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- WCAG 2.2 (all criteria cited): https://www.w3.org/TR/WCAG22/
- Heydon Pickering, "Aria-controls is poop": https://heydonworks.com/article/aria-controls-is-poop/
- Adam Silver, "Don't use the maxlength attribute to stop users from exceeding the limit": https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/
- Internal: ui-ux-design skill, references/09-errors-recovery.md (validation timing, rules 9–10); ui-build skill, references/11-forms-implementation.md (rules 13–17) and 00-decision-tables.md (checkbox vs switch, disclosure)
