# Guided notes text box

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `guided-notes-textarea`. Section 3 of the note form: one free-text box with a permanently visible
label, the organisation's guide prompts as grey placeholder text that disappears when typing starts (D12, D34),
growing as you type, up to 20,000 characters (A6), must not be blank to submit (A8), and usable with phone keyboard
dictation (A31). This document says how to build exactly that. It adds no features, settings or copy beyond the
three short messages the design implies but does not word (marked **new copy** below).

Evidence grades: **[Research]** usability testing or studies · **[Standard]** WCAG 2.2, WAI-ARIA, HTML/CSS specs,
HTML-AAM · **[Convention]** established design systems and platform guidance · **[Opinion]** reasoned judgement.

---

## Where it's used

| Screen (design.md) | Who | What differs for this box |
|---|---|---|
| **4.3 Note form: new note, today** | Worker or manager (A7) | Empty. Guide prompts show as placeholder. The first `input` event is the "first change" that creates the draft and fixes the note date (3.3, A8); focusing or tapping the box creates nothing. |
| **4.3 Note form: draft** (today, or the "This draft is for Wed 30 Sep" earlier-day banner) | Author | Pre-filled with the saved text, so no placeholder shows. Behaviour otherwise identical. The banner sits above the form, never inside the box. |
| **4.3 Note form: past-day note** ("Past-day note for Mon 28 Sep 2026, written on 1 Oct.") | Manager | Same as a new note. Usually on a laptop, so keyboard, paste and desktop speech tools (Dragon, Windows voice typing) matter more than phone dictation. |
| **4.3 Note form: "Editing submitted note (version 2)"** | Author or manager | Pre-filled with the current version's text; **Save changes** and **Cancel** replace Submit. Same validation (not blank, ≤ 20,000). On `note.version_conflict` the user's text stays in the box (3.5). A refused correction is typed at the end of the existing text (A21). |
| **4.3 Submit confirmation** | Whoever submits | The box is **not shown** in the confirmation. It sits behind the modal `<dialog>` (inert). Validation runs **before** the dialog opens, and the submit request reads the box's current value when the user confirms. |
| *(Related, not this component)* 4.4 read view, and a manager viewing someone else's draft read-only (4.2) | Everyone / managers | Show the narrative as text (`white-space: pre-wrap`), **not** as a read-only text box. Listed so nobody reuses this component for it. |

---

## Best practice

### Label and placeholder

- **A placeholder must never be the label; keep a persistent visible label bound with `<label for>`.** Placeholders vanish on input, are not reliably announced, and their default styling fails contrast. [Convention] GOV.UK Text input; [Convention] NHS Textarea; [Standard] HTML spec says the placeholder "should not be used as an alternative to a label". The design already does this (D34 plus 4.10: "the box keeps its visible 'Guided notes' label").
- **Placeholder text that disappears adds memory load and makes errors harder to fix.** NN/g lists seven failure modes from user testing and eyetracking, including memory strain and placeholders being read as pre-filled content (11 May 2014, reviewed 2018; no sample size published). [Research] Baymard's mobile usability testing found inline labels cause "loss of context" (4 June 2013; n not stated). [Research]
- **Platform guidance accepts a placeholder hint when a separate label is also shown.** Apple HIG: placeholder text "disappears when people start typing", so "include a separate label describing the field". [Convention] This is the shape D34 already has.
- **The HTML spec intends a placeholder to be "a short hint (a word or short phrase)"** and says user agents should show it "when the element's value is the empty string and the control is not focused". CR and LF in a textarea placeholder "must be treated as line breaks". [Standard] Multi-line prompts therefore render as written. Whether the hint stays visible while the box is focused but still empty is left to each browser. Current Chrome, Safari and Firefox are generally reported to keep it until the first character (**unverified per version**), so check it on the target phones.
- **Placeholder text must meet 4.5:1.** WCAG 1.4.3 "applies to text in the page, including placeholder text". [Standard] Browser defaults fail. Chrome uses `darkgray` (#a9a9a9, 2.35:1 on white). Firefox uses the text colour at 54% opacity (4.19:1 for #0b0c0c text) (MDN `::placeholder`; ratios computed for this document). [Standard/Convention]
- **A high-contrast placeholder can be mistaken for entered text**, so keep it clearly lighter than typed text while still passing 4.5:1 (MDN `::placeholder`, Usability section). [Convention]
- **Screen readers do not use the placeholder as the label when a label exists.** HTML-AAM maps `placeholder` to the platform's placeholder property and uses it as the accessible name only when there is no label or `title`. It was removed from the description computation in 2016. [Standard] Announcement therefore varies by screen reader. GOV.UK: "not all screen readers read it out" [Convention]. An older test (David MacDonald, Chrome 39 / Firefox 43, about 2015) found that when a field also had `aria-describedby`, JAWS and NVDA usually dropped the placeholder: "Aria-describedby wins". [Research, dated] Re-test before relying on either behaviour.
- **Avoid italics for readability.** The BDA Dyslexia Style Guide 2023 says to avoid italics and underlining because they make text appear to run together. [Convention]

### Auto-growing box

- **Size the box to the text you expect, and let it grow.** GOV.UK and NHS: "Make the height of a textarea proportional to the amount of text you expect users to enter" (default 5 rows). [Convention]
- **`field-sizing: content` makes a textarea grow with its content natively.** It is Baseline "newly available since June 2026". A placeholder makes the control large enough to show it. `rows`/`cols` have no effect, and a fixed `height` defeats it; use min/max sizes instead (MDN). [Standard] Support: Chrome/Edge 123+, Firefox 152+, Safari and iOS Safari 26.2+, Chrome Android 154+ (caniuse, checked 1 October 2026). [Standard] Workers use their own phones (D21), and iPhones that cannot run iOS 26 will not get it, so a fallback is still needed.
- **The established fallback is the "grid replica" technique.** A hidden copy of the text in the same grid cell sets the height, so the box re-wraps on width or zoom changes without any measuring JavaScript. [Convention] (CSS-Tricks, Stephen Shaw).
- **Use `min-height`, never a fixed `height`, on anything holding text.** A fixed height clips at 200% text size (1.4.4) and under text-spacing overrides (1.4.12). [Standard]

### Character limit feedback

- **Do not enforce limits with `maxlength` on free text.** It silently truncates typing, pastes and dictation, and the user may never know text was cut. GOV.UK's character count deliberately removes any `maxlength` and lets users over-type with feedback. [Convention] (govuk-frontend source; Adam Silver)
- **Set the limit "higher than most users will need"** and, for high limits, show the count only past a threshold (GOV.UK `data-threshold`, for example 75%). The component was tested with 17 users including people with low digital skills and disabilities (2017), and updated for accessibility in 2022. [Research/Convention]
- **Wording:** "You have %{count} characters remaining", "You have 0 characters remaining", "You have %{count} characters too many", with singular forms. Errors: "Enter [whatever it is]" and "[Whatever it is] must be [number] characters or less". [Convention] (GOV.UK, NHS)
- **Announce the count to screen readers only when the user pauses.** GOV.UK keeps the visible counter `aria-hidden` and updates a separate visually hidden `aria-live="polite"` region once typing has stopped. Polite regions speak "whenever the user is idle". [Convention] [Standard] (MDN)
- **A live region must already be in the DOM before text is written into it.** "The most reliable way to ensure that live regions are registered is to include them in the initial markup." [Standard] (MDN)
- **Count the way the server counts.** The textarea's API value normalises line breaks to LF, and `value.length` counts UTF-16 code units (HTML spec, MDN). .NET `string.Length` also counts UTF-16 code units, so the two match as long as the JSON body carries `value` unchanged. [Standard] GOV.UK notes emoji and some non-Latin characters can count as more than one. [Convention]

### Validation and errors

- **Validate on submit, not as people move between fields.** GOV.UK: "Do not validate when the user moves away from a field. Wait until they try to move to the next part." Show an error summary at the top, move focus to it, and put an error message next to the field. [Convention]
- **Remove the error as soon as the field becomes valid**, at keystroke level, so users know the fix registered (Baymard usability testing, 9 January 2024). [Research]
- **The error message goes after the label and hint, before the field.** It has a visually hidden "Error:" prefix and a red border on the field. Never clear what the user typed. [Convention] (GOV.UK Error message)
- **Identify and describe the error in text**, not by colour alone. [Standard] WCAG 3.3.1, 1.4.1, failure F81.
- **Set `aria-invalid="true"` only after a submit attempt**, and link the message with `aria-describedby`. MDN: "Do not set `aria-invalid="true"` on empty required elements until after the user attempts to submit the form." [Standard]
- **When the summary link is followed, scroll the field's label into view, then focus the field with `preventScroll`.** The user then sees the label, not a bare box. [Convention] (govuk-frontend error-summary source)
- **Mark required fields with `aria-required` and optional ones with "(optional)".** [Convention] (AgDS Textarea)

### Spellcheck, autocorrect, capitalisation, suggestions

- **`autocapitalize` affects virtual keyboards and voice input, not physical keyboards.** Chrome and Safari default to `sentences`, Firefox to `none`. [Standard] (MDN)
- **`autocorrect` is Baseline 2026 (newly available September 2026).** A textarea defaults to `on`, or inherits from its form. [Standard] (MDN)
- **The browsers' default spellcheckers run locally.** Chrome's "Enhanced spell check" sends typed text to Google, but it is opt-in; Microsoft Editor was an add-on (BleepingComputer on otto-js research, 17 September 2022). [Research, press report] Turning spellcheck off is the mitigation for passwords, not for prose.
- **`writingsuggestions="false"` turns off browser-provided inline writing suggestions** (grey completions after the caret). In Edge it turns off "text prediction and inline Compose" (Microsoft Edge blog, 23 April 2024). Compose sends the text to Microsoft when the user asks it to rewrite or generate (Edge privacy whitepaper). Support: Chrome/Edge 124+, Safari and iOS 18+, Chrome Android 154+, not Firefox (caniuse). [Standard/Convention]

### Dictation and speech input

- **Phone dictation works in any native text field.** Gboard: "Tap an area where you can enter text… tap Microphone", with spoken "New line" and "New paragraph" commands. [Convention] (Google Gboard Help) The box must keep line breaks exactly as entered.
- **Never intercept printable keys.** Do not `preventDefault()` on keydown in text fields. Listen to `input`, not `keydown`, because key capture breaks IME composition, dictation and autocorrect. [Standard] (WAI-ARIA APG combobox note)
- **Some speech software changes the value without firing events.** govuk-frontend: "Speech recognition software such as Dragon NaturallySpeaking will modify the fields by directly changing its `value`. These changes don't trigger events", so it polls the value every second while focused. [Convention] Whether current Dragon and Windows voice typing still behave this way in Chrome and Edge is **unverified**.
- **Speech users address a field by its visible label**, so the accessible name must contain the visible label text. [Standard] WCAG 2.5.3 Label in Name.

### React 19 specifics

- **A controlled textarea must update its state synchronously in `onChange`**, and React writes `value` back to the DOM when it re-renders. [Standard] (react.dev `<textarea>`) A value changed outside React's events, for example by speech software, would be overwritten by stale state on the next render.
- **React 19 resets uncontrolled fields after a `<form action={fn}>` succeeds:** "After the `action` function succeeds, all uncontrolled field elements in the form are reset." `onSubmit` does not do this. [Standard] (react.dev `<form>`)

### Platform and layout

- **Inputs below 16px make iOS Safari zoom the page on focus**, and the fix is a font size of at least 16px rather than disabling zoom. [Convention] (Defensive CSS; Rick Strahl 2023)
- **A focused control must not be entirely hidden by author content** such as a sticky header; `scroll-padding` and `scroll-margin` are the documented fix. [Standard] WCAG 2.4.11.
- **Never disable copy and paste** in a textarea. [Convention] (GOV.UK Textarea)

---

## Recommendation for Grow2Notes

### Anatomy (top to bottom)

```
3. Guided notes                          <h2> wrapping the <label for="guided-notes">
[Error: Write your notes in the …]       error message, only after a failed Submit
+-------------------------------------+
| Mood and wellbeing today?           |  <textarea>; prompts are the placeholder
| What did you do together?           |  grows with the text (or the prompts);
| Anything to follow up?              |  never shorter than about 6 lines
+-------------------------------------+
You have 1,950 characters remaining      visible counter, only at 18,000+ characters
(visually hidden polite live region)     always in the DOM
```

- **Label = section heading.** `<h2 class="sectionHeading"><label htmlFor="guided-notes">3. Guided notes</label></h2>`, matching the "1. Goals" and "2. Common items" headings. The heading text and the accessible name are then the same string, and tapping the heading focuses the box. [Convention] This follows GOV.UK's question-page pattern of a heading wrapping the label; the accessible name "3. Guided notes" contains the visible text (2.5.3).
- **No hint text and no permanent limit text.** The design specifies none. A permanent "up to 20,000 characters" description would be noise for a limit almost nobody reaches, and an always-on `aria-describedby` may suppress the placeholder in some screen readers (MacDonald). [Opinion]
- **Box:** full width of the form column; 2px border at ≥ 3:1 against the page (1.4.11); 4px radius (or the app's input radius); padding 0.75rem; `font: inherit` (browser default for textarea is monospace); `font-size: max(16px, 1rem)`; `line-height: 1.5`; minimum height about 6 lines (`min-block-size: 11em`); no maximum height, so the page scrolls rather than the box; `resize: none`.
- **Placeholder:** the organisation's guide prompts, exactly as saved (line breaks kept), in a grey of about 6:1 on the box background, e.g. `#626262` = 6.10:1 on `#ffffff`. That clears 4.5:1 with margin for glare, and stays clearly lighter than typed text (`#0b0c0c` is 19.6:1). Use the app's secondary-text token if it is ≥ 4.5:1. Set `opacity: 1` to override Firefox's 54% default. Upright, not italic. [Standard] for 4.5:1; [Opinion] for the 6:1 target.
- **No prompts saved** (4.10 help text, §11.3): the placeholder is never copied into the value.

### Behaviour

1. **Uncontrolled, read once.** The box is an uncontrolled `<textarea defaultValue>` whose initial text is captured once when the draft loads, e.g. `useState(() => draft.narrative)` in the form. The DOM holds the live text. Autosave and Submit read `ref.current.value`. React never writes the value back, so a TanStack Query refetch (on window focus), a re-render, or speech software that skips `input` events cannot overwrite what is on screen ("The text on screen is never replaced without asking", 4.3). [Opinion, grounded in react.dev and the govuk-frontend Dragon note]
2. **The only programmatic replacement** is the user choosing **Load the other version** (4.3, 5.6). That sets `value` deliberately and refreshes the counter and height.
3. **Prompts may update live.** `placeholder` comes from the latest `guidePrompts` the client has fetched, so a manager's change reaches open, empty boxes on the next refetch (4.10). The text value never follows refetches.
4. **The box grows with the content**, and never gets shorter than the prompts. The box keeps at least the height the prompts needed, so the page does not jump when the first character removes them. It also never gets shorter than about 6 lines (A31: room to proofread dictated text). It has no inner scrolling on any screen, and no manual resize handle (auto-growth makes it unnecessary, and a dragged height would stop the growth). [Opinion]
5. **Typing and autosave.** On every `input`: update the counter and the fallback replica, and tell the form something changed. The form's autosave hook saves about 2 s after the last change, on blur, and when the page is hidden (3.4, 5.6). The first `input` on a new note creates the draft (A8). The text is never trimmed, collapsed or reformatted, because "the text as written" is the record (3.1, §11.3).
6. **Speech software without events (laptop).** While the box has focus, check `value` every 1 s (GOV.UK's pattern). If it changed since the last `input`, treat it as an `input`. Blur and page-hidden saves read the DOM value anyway, so nothing is lost even without the poll. Keep the poll only if M6 testing shows Dragon or Windows voice typing still skip `input` events. [Convention; current need unverified]
7. **Keyboard.** Tab in, Tab out (no Tab trapping or indenting). Enter inserts a new line and never submits. No shortcuts are added.
8. **No autofocus.** The form opens with the participant name and Goals in view. Autofocusing the box would open the phone keyboard over the tick boxes. [Opinion]
9. **Validation on Submit only.** Submit, or Save changes, runs these checks in order:
   - `value.trim() === ''` → "Write your notes in the Guided notes box"
   - `value.length > 20000` → "Guided notes must be 20,000 characters or fewer"

   On failure the form shows the error summary at the top (form-level component) with the same text, linking to `#guided-notes`, and does **not** open the confirmation dialog. The summary link scrolls the "3. Guided notes" heading into view, then focuses the box with `preventScroll` (GOV.UK).
10. **Error clears on fix.** As soon as the box becomes non-blank (empty error), or drops back to ≤ 20,000 (length error), remove the inline message and `aria-invalid` on that keystroke, silently (Baymard 2024). No new errors appear until the next Submit.
11. **Over-typing is allowed.** No `maxlength`; nothing is ever truncated, including pastes and dictation.

### Copy (exact strings)

| Where | Text | Source |
|---|---|---|
| Label / section heading | `3. Guided notes` | design.md 4.3 |
| Placeholder | The organisation's guide prompts as saved in Manage > Guide prompts; none if empty | 4.10 |
| Empty error (inline + summary) | `Write your notes in the Guided notes box` | **new copy**: GOV.UK "Enter [whatever it is]" pattern, with the verb workers use and the box named in the label's words [Opinion] |
| Too-long error (inline + summary) | `Guided notes must be 20,000 characters or fewer` | **new copy**: GOV.UK "must be [number] characters or less", with "fewer" because characters are counted [Opinion] |
| Counter, 18,000 to 19,999 characters | `You have 1,950 characters remaining` (singular `1 character`) | GOV.UK strings; numbers formatted with `Intl.NumberFormat('en-AU')` |
| Counter, exactly 20,000 | `You have 0 characters remaining` | GOV.UK |
| Counter, over 20,000 | `You have 312 characters too many` (singular `1 character`) | GOV.UK |
| Hidden error prefix | `Error: ` | GOV.UK |

The counter threshold is **18,000 characters** (90%), following GOV.UK's threshold mechanism. 20,000 characters is about 3,000 words, so most workers will never see it. [Opinion on the number]

### States

| State | Appearance | Semantics and behaviour |
|---|---|---|
| **Default (empty)** | White box, 2px dark border; prompts in grey; box at least as tall as the prompts | Placeholder exposed natively; `aria-required="true"` |
| **Default (no prompts set up)** | Empty white box, about 6 lines, label only (4.10 "no placeholder") | Same |
| **Hover** | No change; text cursor only. A text box is not a button, and hover does not exist on phones. [Opinion] | — |
| **Focus** | Border unchanged plus the app's focus outline: 3px solid focus colour, 2px offset, ≥ 3:1 against both the box and the page. `transition: none`. Prompts stay until the first character (browser behaviour; verify). | `:focus-visible` matches text fields on every focus. `scroll-margin-block-start` is set if the top bar is sticky (2.4.11). |
| **Active / typing** | Typed text in primary text colour; box grows; counter only at 18,000+ | No announcements while typing below the threshold |
| **Approaching / over limit** | Counter below the box: grey hint style at 18,000–20,000; red bold with red box border when over | Visible counter `aria-hidden="true"`; the polite live region announces the same text after a 1 s pause. Not `aria-invalid` until a Submit fails. |
| **Error (after Submit)** | Message in red bold between label and box, with the "Error:" prefix hidden; red box border; a 5px red bar on the section's inline-start edge (GOV.UK form-group error). Border width stays 2px so the box does not shift. | `aria-invalid="true"`, `aria-describedby="guided-notes-error"`; summary focused first |
| **Loading** | Box not rendered until `GET {base}/draft` returns. The form's loading state covers the section. An editable empty box that could later be overwritten is never shown. | — |
| **Saving / not saved** | No change to the box. Status lives in the top-bar save indicator (4.3). The text stays editable and is never cleared. | — |
| **Read-only** | Only when the note can no longer be saved from this screen, e.g. the after-midnight refusal in 3.3 ("The text stays on screen with the message…"). Text at full contrast on a light grey background (`#f3f2f1`; text 17.5:1, placeholder 5.46:1); no placeholder. | `readOnly`, never `disabled`: it stays focusable, selectable and copyable, and is announced as "read only". [Opinion] |
| **Disabled** | **Never.** Disabled text is exempt from contrast (so usually illegible), unfocusable, and cannot be copied. | — |

### Phone vs laptop

| | Phone (375 px first, D5) | Laptop |
|---|---|---|
| Width | Full column width inside the 16px page gutters | Fills the form column. The note-form layout caps the column at a readable width (about 40rem). |
| Text size | `max(16px, 1rem)`: stops the iOS focus zoom, respects larger user settings | Same |
| Height | At least about 6 lines or the prompt height; grows; the page scrolls. The browser keeps the caret above the on-screen keyboard. | Same |
| Input | Typing with autocorrect and predictions; keyboard microphone dictation; paste | Typing; paste (often from elsewhere for past-day notes); Dragon or Windows voice typing |
| Counter | Below the box. It may sit under the keyboard while typing at the end of a very long note, but the screen-reader announcement and the Submit error still cover it. | Below the box |

### Attributes

```html
<textarea id="guided-notes" name="narrative"
  placeholder="…guide prompts…"
  aria-required="true"
  spellcheck="true" autocorrect="on" autocapitalize="sentences"
  autocomplete="off" writingsuggestions="false"></textarea>
```

| Attribute | Value | Why |
|---|---|---|
| `spellcheck` | `true` | Explicit because the default differs by browser. Many workers write in a second language, and default checkers run locally. [Standard + Opinion] |
| `autocorrect` | `on` | Explicit; Baseline Sept 2026. [Standard] |
| `autocapitalize` | `sentences` | Firefox defaults to `none`; it also shapes dictated text. [Standard] |
| `autocomplete` | `off` | Low-cost guard so form history never keeps note text (D22). Its effect on textareas is **unverified**; harmless if none. [Opinion] |
| `writingsuggestions` | `false` | Removes browser inline completions and Edge's inline Compose from the record box. This keeps generative AI out of the notes (D31) and avoids accepted predictions putting words in that weren't observed. Keyboard suggestion bars and autocorrect are untouched (**unverified** on every platform). [Opinion] |
| `aria-required` | `true` | AgDS convention. Not the `required` attribute, which triggers constraint validation, `:invalid` styling and browser bubbles. [Convention] |
| `maxlength`, `rows`, `inputmode`, `enterkeyhint`, `wrap` | **not set** | No silent truncation; `rows` is ignored with `field-sizing`; the defaults give the text keyboard with a microphone and an Enter key that inserts a new line. |
| Page | `<html lang="en-AU">` | WCAG 3.1.1. Whether browsers choose an Australian dictionary from it is **unverified**. |
| Form | `<form noValidate onSubmit>` | Own error messages; `onSubmit`, not `action={fn}`, so React 19 never resets the uncontrolled box. |

### Accessibility

**Semantics.** Native `<textarea>` (implicit role `textbox`, multi-line), named by the wrapping `<label>`. No ARIA except `aria-required`, `aria-invalid` (after a failed submit only) and `aria-describedby` (error id only, while an error is shown). React Aria is **not** needed: native HTML covers every requirement.

**Screen reader announcements.**

| Moment | Heard (wording varies by screen reader) | Mechanism |
|---|---|---|
| Focus, empty | "3. Guided notes, edit text, multi-line, required", then in most screen readers the prompts | Label, `aria-required`, native placeholder mapping (HTML-AAM) |
| Focus, with text | Label and role, then the text | Native |
| Typing below 18,000 | Nothing extra | — |
| Pause after passing 18,000 | "You have 1,950 characters remaining" | Polite live region, always in the DOM, updated 1 s after the last input |
| Pause while over 20,000 | "You have 312 characters too many" | Same region |
| Submit with errors | Error summary (focused), then on reaching the box: "invalid entry", "Error: Write your notes in the Guided notes box" | Summary focus; `aria-invalid`; `aria-describedby` |
| Error fixed | Nothing; the message is removed | — |
| Read-only | "read only" | Native `readonly` |

In the empty-error state the box has both an `aria-describedby` and a placeholder. Some Windows screen readers have dropped the placeholder in that combination (MacDonald). Accept this, since the error tells the user what to do, and include it in the M6 VoiceOver and TalkBack pass (§14).

**WCAG 2.2 criteria this meets:** 1.3.1 Info and Relationships (label and error linked) · 1.4.1 Use of Color (error in text, not just a red border) · 1.4.3 Contrast (Minimum) (typed text, placeholder 6.1:1, error text) · 1.4.4 Resize Text (rem and px floor, no fixed height) · 1.4.10 Reflow (no fixed widths at 320 px) · 1.4.11 Non-text Contrast (border and focus ring ≥ 3:1) · 1.4.12 Text Spacing (grows; the fallback scrolls rather than clips) · 2.1.1 Keyboard (native) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) (scroll margin under a sticky bar) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (whole box, well over 44 × 44 px) · 3.2.2 On Input (autosave changes no context) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 4.1.2 Name, Role, Value · 4.1.3 Status Messages (count via polite live region). Test the placeholder contrast by hand; do not assume an automated scan checks `::placeholder` (**unverified** for axe).

### Implementation sketch (React 19 + CSS Modules)

```tsx
// GuidedNotes.tsx
import { useImperativeHandle, useLayoutEffect, useRef, useState, type Ref } from 'react';
import styles from './GuidedNotes.module.css';

export const NARRATIVE_LIMIT = 20_000;         // same value as the server's Limits class (A6)
const SHOW_COUNT_FROM = 18_000;                // GOV.UK-style threshold, 90%
const fmt = new Intl.NumberFormat('en-AU');
const nativeGrow = CSS.supports('field-sizing', 'content');

export function countMessage(len: number) {
  const left = NARRATIVE_LIMIT - len;
  const n = Math.abs(left);
  const word = n === 1 ? 'character' : 'characters';
  return left >= 0 ? `You have ${fmt.format(n)} ${word} remaining`
                   : `You have ${fmt.format(n)} ${word} too many`;
}

export type GuidedNotesHandle = { value(): string; replaceText(t: string): void; focus(): void };

export function GuidedNotes({ ref, initialText, prompts, error, readOnly, onEdit }: {
  ref?: Ref<GuidedNotesHandle>;  // React 19: ref is a plain prop
  initialText: string;           // captured once by the form; never re-synced
  prompts: string;               // guide prompts; may change at any time
  error?: string;                // set by the form only when Submit fails
  readOnly?: boolean;
  onEdit(text: string): void;    // autosave + "first change" + clearing the error
}) {
  const box = useRef<HTMLTextAreaElement>(null);
  const grow = useRef<HTMLDivElement>(null);
  const [len, setLen] = useState(initialText.length);
  const [srCount, setSrCount] = useState('');
  const srTimer = useRef<number | undefined>(undefined);

  function sync(text: string) {
    if (!nativeGrow && grow.current) grow.current.dataset.value = text;  // fallback auto-grow
    setLen(text.length);
    clearTimeout(srTimer.current);
    srTimer.current = window.setTimeout(
      () => setSrCount(text.length >= SHOW_COUNT_FROM ? countMessage(text.length) : ''), 1000);
  }

  useLayoutEffect(() => {
    box.current!.setAttribute('writingsuggestions', 'false'); // not in React's DOM types yet
    if (!nativeGrow) grow.current!.dataset.value = initialText;
    return () => clearTimeout(srTimer.current);
  }, []);

  useImperativeHandle(ref, () => ({
    value: () => box.current!.value,
    replaceText: (t) => { box.current!.value = t; sync(t); }, // only for "Load the other version"
    focus: () => box.current!.focus(),
  }));

  const showCount = len >= SHOW_COUNT_FROM;
  return (
    <section className={error ? styles.sectionError : styles.section}>
      <h2 className={styles.heading}><label htmlFor="guided-notes">3. Guided notes</label></h2>
      {error && (
        <p id="guided-notes-error" className={styles.error}>
          <span className="visually-hidden">Error: </span>{error}
        </p>
      )}
      <div ref={grow} className={styles.grow} data-prompts={prompts}>
        <textarea
          ref={box} id="guided-notes" name="narrative" className={styles.box}
          defaultValue={initialText} placeholder={prompts || undefined}
          aria-required="true" aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'guided-notes-error' : undefined}
          readOnly={readOnly} spellCheck autoCorrect="on" autoCapitalize="sentences" autoComplete="off"
          onInput={(e) => { const t = e.currentTarget.value; sync(t); onEdit(t); }}
        />
      </div>
      <p className={len > NARRATIVE_LIMIT ? styles.countOver : styles.count} aria-hidden="true"
         hidden={!showCount}>{showCount ? countMessage(len) : ''}</p>
      <div className="visually-hidden" aria-live="polite">{srCount}</div>
    </section>
  );
}
```

The Dragon poll from Behaviour item 6 adds an `onFocus` that starts a 1 s interval comparing `box.current.value` with the last synced value, and an `onBlur` that clears it. Add it only if M6 testing needs it.

```css
/* GuidedNotes.module.css: token names are placeholders for the app's colour system */
.grow { display: grid; }
.grow > .box, .grow::before, .grow::after {
  grid-area: 1 / 1;
  box-sizing: border-box;
  inline-size: 100%;
  padding: 0.75rem;
  border-width: 2px;                       /* identical box metrics in all three */
  border-style: solid;
  font: inherit;                           /* textarea defaults to monospace */
  font-size: max(16px, 1rem);              /* no iOS focus zoom; honours larger settings */
  line-height: 1.5;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
.grow::before, .grow::after { border-color: transparent; visibility: hidden; }
.grow::before { content: attr(data-prompts) " "; } /* never shorter than the prompts */
.grow::after  { content: attr(data-value) " "; }   /* fallback growth */

.box {
  min-block-size: 11em;                    /* about 6 lines */
  border-color: var(--colour-input-border);/* >= 3:1 against the page */
  border-radius: 4px;
  background: var(--colour-input-bg);      /* #ffffff */
  color: var(--colour-text);               /* e.g. #0b0c0c */
  resize: none;
  overflow-y: auto;                        /* if the fallback under-measures (e.g. text-spacing
                                              overrides miss ::after), scroll; never clip */
}
.box::placeholder { color: var(--colour-placeholder); opacity: 1; } /* e.g. #626262, 6.10:1 */
.box:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; transition: none; }
.box[aria-invalid="true"] { border-color: var(--colour-error); }
.box[readonly] { background: var(--colour-surface-muted); }          /* e.g. #f3f2f1 */

@supports (field-sizing: content) {
  .box { field-sizing: content; }
  .grow::after { content: none; }          /* native growth; delete the fallback once the support floor allows */
}
@media (forced-colors: active) {
  .box::placeholder { color: GrayText; }
  .box:focus-visible { outline-color: Highlight; }
}
```

**Form-side notes** (for the note-form owner):
- `onSubmit` with `e.preventDefault()`; read `notes.current.value()` at the moment of the check and again when the confirmation's submit button is pressed. The `POST {base}/versions` body "repeats the latest content" (6.3).
- Capture `initialText` once and key the component by `participantId + noteDate`, so a different note remounts it cleanly.
- If the session ends and sign-in appears "in place" (4.0, 5.6), keep the form mounted beneath it, or copy `value()` into memory before unmounting and pass it back as `initialText`. Otherwise the uncontrolled box loses the unsaved last seconds. Memory only, never device storage (D22).
- Share `NARRATIVE_LIMIT` with the blank and length checks the server runs on submit (5.8 step 4), so client and server agree.

---

## Per-screen notes

**4.3 Note form: new note (today, worker or manager)**
- The prompts are the only on-screen guidance, so they must render in full. The box sizes itself to them, and line breaks in the prompts show as line breaks.
- Nothing is created until the first `input`. Focusing the box, scrolling past it, or opening and closing the keyboard must not trigger an autosave.
- If the first change happens after midnight (3.3), the server refuses. The text stays, the form shows the design's banner, and the box becomes `readOnly` so the worker cannot keep typing into a note that cannot be saved.

**4.3 Note form: draft (today or an earlier day)**
- The box opens with the saved text, so the placeholder is hidden. If the worker deletes everything, the prompts reappear, which is consistent with D34.
- Don't autofocus or move the caret. On "Lists changed before the first save" and "Changed on another device or tab", the text stays exactly as on screen. Only **Load the other version** replaces it.

**4.3 Note form: past-day note (manager)**
- Same component and copy. Expect paste and desktop speech input. Pasting more than 20,000 characters is allowed, shows the red counter, and is blocked only at Submit.

**4.3 Note form: Editing submitted note (version n)**
- Same validation on **Save changes**. On `409 note.version_conflict`, the editor's text stays in the box while the newer version is shown separately (3.5). The box is never pre-emptively overwritten.
- For a refused correction (A21) the manager types the statement at the end. Do not insert any template text into the box. The heading the manager writes is their own typing.

**4.3 Submit confirmation**
- Validate first. A blank or over-limit box shows the inline error and summary and does **not** open the dialog.
- While the dialog is open (`showModal()`), the box is inert. **Go back** returns focus to the Submit button (native `<dialog>` behaviour), and the text is unchanged.
- If the server still returns `422 validation.failed` for the narrative (for example whitespace the client did not catch), close the dialog and show the same inline error and summary. Never clear the text.
- After success the box unmounts with the form. Removing that note's draft query from the in-memory cache is tidy but optional [Opinion]; nothing is ever in device storage.
- Hand-off to the Submit button doc: tapping Submit straight after typing blurs the box, which starts a save. While that save is in flight, Submit is in its "not saved" state (4.3), so the first tap can land on it. That doc should handle the timing.

---

## Anti-patterns to avoid

- **Placeholder as the only label,** or `aria-label` copied from the prompts. The visible "3. Guided notes" label is the name.
- **Browser-default placeholder colour** (Chrome 2.35:1; Firefox about 4.2:1 with near-black text), or an italic placeholder.
- **Prompts inserted as the default value,** so the worker must delete them and they risk being saved into the record (NN/g failure 7; 4.10 "not saved into notes").
- **Re-showing the prompts after typing starts** (tooltip, hint, side panel). That reverses D34 and is listed out of scope in §1.
- **`maxlength="20000"`**: silent truncation of pastes and dictation.
- **An always-visible "0 / 20,000" counter,** a counter announced on every keystroke, or `aria-live="assertive"` for the count.
- **A fixed `height`, a `max-height` with inner scrolling, or `rows` alone:** clipped text at 200% zoom, and a scroll trap inside a scrolling page on phones.
- **Font size under 16px:** iOS zooms the page on focus.
- **A controlled textarea fed from query data,** or `<form action={fn}>`: refetches and React 19's automatic form reset can replace the worker's text.
- **Keydown handlers, input masks, auto-trim or auto-formatting while typing:** these break IME composition, dictation and autocorrect.
- **Enter to submit, or keyboard shortcuts** that the design doesn't specify.
- **Turning off spellcheck, autocorrect or autocapitalisation** "for privacy": this hurts second-language writers and dictation to guard against opt-in browser settings.
- **`disabled` on the box** while saving, submitting or after an error.
- **A rich-text or `contenteditable` editor:** more inconsistent with dictation, autocorrect and screen readers, and the record is plain text.
- **Validating on blur or while typing** before the first Submit.
- **Keeping a "backup" copy of the text in localStorage, sessionStorage or IndexedDB** (D22). Memory only.
- **A read-only `<textarea>` for the read view:** it scrolls inside itself and hides text. Use plain text with `white-space: pre-wrap`.

---

## Tensions with decisions

**D12 / D34: guide prompts as placeholder text that disappears when typing starts.** The weight of guidance prefers persistent hint text outside the field. GOV.UK: "Do not use placeholder text in place of a label, or for hints or examples… it vanishes when the user starts typing, which can cause problems for users with memory conditions… not all screen readers read it out". NHS: "Do not use placeholder text for a label". NN/g (2014) lists memory strain and placeholders mistaken for entered text. Baymard (2013) found "loss of context" in mobile testing. The HTML spec intends a placeholder for "a short hint (a word or short phrase)", while these prompts can be up to 1,000 characters. Against that, Apple HIG accepts placeholder hints alongside a separate label. The design already removes the two most-cited failures, a missing label and low contrast, by keeping a permanent visible label and requiring 4.5:1 (4.10). Recorded for completeness only; **no change recommended**. The decision stands, and this document makes the placeholder as good as it can be.

---

## Sources

**[Standard]**
- WHATWG HTML, the `textarea` element (placeholder, API value, maxlength): https://html.spec.whatwg.org/multipage/form-elements.html#the-textarea-element
- HTML-AAM, accessible name computation for `textarea`; `placeholder` mapping: https://w3c.github.io/html-aam/
- WCAG 2.2 Understanding 1.4.3 Contrast (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 3.3.1 Error Identification: https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 1.4.12 Text Spacing: https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- WAI-ARIA APG, Combobox pattern (do not capture key events in text inputs): https://www.w3.org/WAI/ARIA/apg/patterns/combobox/
- MDN `<textarea>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/textarea
- MDN `::placeholder`: https://developer.mozilla.org/en-US/docs/Web/CSS/::placeholder
- MDN `field-sizing`: https://developer.mozilla.org/en-US/docs/Web/CSS/field-sizing
- MDN `autocorrect`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocorrect
- MDN `autocapitalize`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocapitalize
- MDN `writingsuggestions`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/writingsuggestions
- MDN ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- MDN `aria-invalid`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-invalid
- caniuse, `field-sizing`: https://caniuse.com/mdn-css_properties_field-sizing
- caniuse, `writingsuggestions`: https://caniuse.com/mdn-html_global_attributes_writingsuggestions
- React, `<textarea>`: https://react.dev/reference/react-dom/components/textarea
- React, `<form>` (uncontrolled fields reset after an action): https://react.dev/reference/react-dom/components/form

**[Research]**
- Nielsen Norman Group, "Placeholders in Form Fields Are Harmful" (2014, reviewed 2018): https://www.nngroup.com/articles/form-design-placeholders/
- Baymard Institute, "Mobile Form Usability: Avoid Using Inline Labels" (2013): https://baymard.com/blog/mobile-forms-avoid-inline-labels
- Baymard Institute, "Usability Testing of Inline Form Validation" (2024): https://baymard.com/blog/inline-form-validation
- W3C Low Vision Accessibility Task Force, Placeholder Research (literature summary): https://www.w3.org/WAI/GL/low-vision-a11y-tf/wiki/Placeholder_Research
- David MacDonald, "Does Placeholder Text AND aria-describedby Work on a Form Field" (undated; Chrome 39 / Firefox 43 era): http://www.davidmacd.com/blog/test-placeholder-text-aria-describedby.html
- BleepingComputer on otto-js spellcheck research (17 September 2022): https://www.bleepingcomputer.com/news/security/google-microsoft-can-get-your-passwords-via-web-browsers-spellcheck/

**[Convention]**
- GOV.UK Design System, Textarea: https://design-system.service.gov.uk/components/textarea/
- GOV.UK Design System, Character count (incl. 2017 research, threshold): https://design-system.service.gov.uk/components/character-count/
- govuk-frontend character count source (strings, live region, Dragon polling): https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/character-count/character-count.mjs
- govuk-frontend error summary source (label scroll, then focus): https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/error-summary/error-summary.mjs
- GOV.UK Design System, Text input (placeholder guidance): https://design-system.service.gov.uk/components/text-input/
- GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- GOV.UK Design System, Validation pattern: https://design-system.service.gov.uk/patterns/validation/
- NHS digital service manual, Textarea: https://service-manual.nhs.uk/design-system/components/textarea
- NHS digital service manual, Character count: https://service-manual.nhs.uk/design-system/components/character-count
- Australian Government Design System (AgDS), Textarea: https://design-system.agriculture.gov.au/components/textarea
- Apple Human Interface Guidelines, Text fields: https://developer.apple.com/design/human-interface-guidelines/text-fields
- Google Gboard Help, Type with your voice: https://support.google.com/gboard/answer/2781851
- Microsoft Edge blog, "Improving text editing on the web, one feature at a time" (23 April 2024): https://blogs.windows.com/msedgedev/2024/04/23/improving-text-editing-on-the-web/
- Microsoft Edge privacy whitepaper, Compose: https://learn.microsoft.com/microsoft-edge/privacy-whitepaper/#compose
- Defensive CSS, "Input zoom on iOS Safari": https://defensivecss.dev/tip/input-zoom-safari/
- Rick Strahl, "Preventing iOS Textbox Auto Zooming and ViewPort Sizing" (2023): https://weblog.west-wind.com/posts/2023/Apr/17/Preventing-iOS-Textbox-Auto-Zooming-and-ViewPort-Sizing
- CSS-Tricks, "The Cleanest Trick for Autogrowing Textareas" (grid replica): https://css-tricks.com/the-cleanest-trick-for-autogrowing-textareas/
- Adam Silver, "Don't use the maxlength attribute to stop users from exceeding the limit": https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/
- British Dyslexia Association, Dyslexia Style Guide 2023: https://cdn.bdadyslexia.org.uk/uploads/documents/Advice/style-guide/BDA-Style-Guide-2023.pdf
