# Participant record export form

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. `color-scheme: light` only (foundations.md), not `light dark`. Success line "{file} is ready. Look in your downloads." (shared with the Daily report), not "Exported: {file}". A `403` shows "Page not found" through the role guard, with no message. The export passes the `api()` wrapper's `timeoutMs: 30_000`. Radios use users.md's drawn ChoiceRow, not `accent-color`. 48 px fields and buttons, 56 px rows.

Component key: `export-form`. This is the form a manager uses to download one participant's record as a PDF or Word file for an access or correction request (design.md §4.12, §11.6, A20, A30). It covers date range entry, the format choice, the "earlier versions" option, validation, and what happens after Export. It builds the screen as design.md specifies it. It adds no screens, settings, notifications or data.

Evidence grades: **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML spec, platform docs · **[Convention]** established design systems · **[Opinion]** reasoned judgement with no direct evidence.

It relies on three sibling specs and does not restate them: `form-validation.md` (error summary, inline errors, export error copy), `primary-actions.md` (the `Button` component, busy and error states, downloads) and `app-shell-nav.md` (page title, focus on arrival, role guard, signed-out flow). Where this file differs from them, it says so and gives the reason.

---

## Where it's used

| Screen (design.md) | How you get there | What differs |
|---|---|---|
| **4.12 Participant record export** (managers only) | "Export record" in the actions on **Participant detail** (4.8), and in the manager header actions on **Participant notes** (4.4) | The only screen that uses this form. Six parts, in the design's order: Participant (fixed) · From date · To date · Format (PDF or Word) · "Include earlier versions of edited notes" (off) · Export. Defaults: From is the participant's first note, To is today. States: no notes in the range ("No submitted notes for Jane Citizen between … and …"), and To date before From date ("an error next to the field"). |

Related but not this component. The **Daily report** (4.7) uses the same native date field and the same download mechanism. It differs in three ways: it has two download buttons instead of a format choice, it checks for notes before the press (`has-notes`), and it renders in under a second. The export has no pre-check, and one export can take up to 30 seconds (M5 "Done when").

Workers never see this screen. The route renders "Page not found" for them (app-shell-nav.md, role guard), and the API returns 403/404.

---

## Best practice

### Date range entry

- **Native `<input type="date">` always gives a `yyyy-mm-dd` value, but it shows the date in the browser's locale format.** MDN: "the displayed date is formatted based on the locale of the user's browser, but the parsed `value` is always formatted `yyyy-mm-dd`." A manager whose browser language is English (United States) sees mm/dd/yyyy. The page cannot change that. [Standard] https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- **AgDS (the Australian Government design system) says: "Prefer Australian date format 'dd/mm/yyyy'", "Always display the date format", and "Don't use international date formats such as USA mm/dd/yyyy".** Its Date range picker is a custom component with "From" and "To" labels, and it accepts typing or calendar picking. [Convention] https://design-system.agriculture.gov.au/components/date-range-picker
- **GOV.UK: "Use the date input component when you're asking users for a date they'll already know, or can look up without using a calendar."** That component is three text fields. GOV.UK says to use a calendar control only to "pick a date in the near future or recent past", and never one "that depends on JavaScript as the only input option". [Convention] https://design-system.service.gov.uk/components/date-input/ · https://design-system.service.gov.uk/patterns/dates/
- **NN/g: "Calendar pickers should be used for events close to the present time — within less than a year"; for dates further away, typing is "the most efficient" option** (Angie Li, 2017). [Convention, expert guidance] https://www.nngroup.com/articles/date-input/
- **Native date inputs have documented assistive-technology gaps.** Hassell Inclusion (Graham Armfield, February 2019) tested NVDA, JAWS, VoiceOver, TalkBack and Dragon. Dragon could not use the field at all: "It seems that Dragon has no support for `input type="date"`." Browser validation messages were not announced in Safari/VoiceOver on iOS or in Firefox/TalkBack. The verdict for accessible sites was "No". [Research, expert AT testing, 2019] https://hassellinclusion.com/blog/input-type-date-ready-for-use/ Parts of this are dated. Safari on macOS now supports the control (unverified here), and Grow2Notes does not use browser validation bubbles. a11ysupport.io still rates the element's screen-reader and voice-control support as partial. That page is rendered by script and could not be read directly, so the exact counts are **unverified**. https://a11ysupport.io/tech/html/input(type-date)_element
- **Choosing between them** depends on two things. First, the dates here are **pre-filled**, so the usual path involves no date entry at all. Second, design 4.7 already uses a native date field for the same managers. Native is the consistent choice. The gaps above are recorded under Tensions. [Opinion]

### Format: radios, not a select

- **Radios for one choice from a short visible list.** NN/g: "If possible, use radio buttons rather than drop-down menus. Radio buttons have lower cognitive load because they make all options permanently visible" (Nielsen, 2004). [Convention, expert guidance] https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/ GOV.UK: "The select component should only be used as a last resort … research shows that some users find selects very difficult to use." [Convention, citing unpublished user research] https://design-system.service.gov.uk/components/select/
- **Group the radios in `<fieldset>` + `<legend>`.** WCAG technique H71: "Grouping controls is most important for related radio buttons and checkboxes." [Standard] https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- **Pre-selection: the sources disagree.** GOV.UK: "Do not pre-select radio options as this makes it more likely that users will: not realise they've missed a question; submit the wrong answer." [Convention] https://design-system.service.gov.uk/components/radios/ NN/g (2004): "Always offer a default selection for radio button lists." [Convention] The hidden variable is whether one answer is knowably right for most people. Here it isn't: §11.6 says redaction happens "on the exported Word copy", and §11.5 says the PDF "is for printing and filing". Only the manager knows which this request needs. A wrong guess also leaves an unwanted file of health information on the device (§9.6). [Opinion]
- **Item hints are allowed but must be short.** GOV.UK: "Keep each hint to a single short sentence, without any full stops." **Order:** "Order radio options alphabetically by default", and PDF, Word is already alphabetical. **Layout:** inline only when there are two short options, and "on mobile devices, inline radios still stack". [Convention] https://design-system.service.gov.uk/components/radios/

### Explaining the "earlier versions" option

- **Don't pre-tick a checkbox.** GOV.UK: "Do not pre-select checkbox options." A20 makes this option off by default, which also keeps the restricted record (HPP 6.7, A13) out of the file unless the manager chooses it. [Convention] https://design-system.service.gov.uk/components/checkboxes/
- **Explain it in a hint, not in the label or a tooltip.** GOV.UK: "Use hint text for help that's relevant to the majority of users", "Keep hint text to a single short sentence, without any full stops", and "Do not include links within hint text". Link the hint with `aria-describedby`. [Convention] https://design-system.service.gov.uk/components/text-input/
- **A checkbox, not a switch.** Its value takes effect only when Export is pressed (a deferred commit). That makes it a native checkbox. `role="switch"` is for settings that apply immediately. [Convention: ui-build control table]
- **What the hint must say comes from the design:** earlier versions are printed "in full" (§11.6), and they can hold another person's information from a wrong-participant note, which "the manager removes" before handing the copy over (§3.9, §11.6).

### Validation

- **Validate when Export is pressed, not on change or blur.** Two peer-reviewed studies (n = 77 and n = 90) found that showing errors after the whole form was completed worked best. [Research] https://academic.oup.com/iwc/article-abstract/19/3/330/693000 · GOV.UK says the same. [Convention] https://design-system.service.gov.uk/patterns/validation/ Full rules and copy are in `form-validation.md`.
- **Date-range error wording.** GOV.UK templates: "must be the same as or after [date]", "must be between [date] and [date]". [Convention] https://design-system.service.gov.uk/components/date-input/
- **"No notes in the range" is a result, not an error.** WCAG 4.1.3 counts "No results returned" as a status message when it "does not take focus". [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **Don't disable Export to prevent errors.** GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible." [Convention] https://design-system.service.gov.uk/components/button/
- **No confirmation dialog is needed.** WCAG 3.3.4 covers pages that "modify or delete user-controllable data". Exporting a copy does neither. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

### What happens after Export

- **Response-time limits.** "1.0 second is about the limit for the user's flow of thought to stay uninterrupted"; "10 seconds is about the limit for keeping the user's attention focused"; beyond that, give "feedback indicating when the computer expects to be done" (Nielsen, 1993, updated 2014). [Convention, expert synthesis] https://www.nngroup.com/articles/response-times-3-important-limits/ NN/g: looped indicators for 2–10 seconds, percent-done for "10 or more seconds". [Convention, citing Nah 2004 for tolerable waits] https://www.nngroup.com/articles/progress-indicators/
- **Busy and result messages are status messages.** WCAG 4.1.3 examples: "an icon symbolizing 'busy' appears … The screen reader announces 'application busy'", and text added after submit that "the screen reader announces". [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **The live region has to exist before you write to it.** MDN: "Establish the live region before updating its content … The most reliable way … is to include them in the initial markup." `role="status"` is polite and `role="alert"` is assertive. [Standard] https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- **Saving a blob.** `download` "only works for same-origin URLs, or the `blob:` and `data:` schemes". For a `blob:` URL the server's `Content-Disposition` is not visible to the link, so the page has to pass on the file name itself. The browser then decides whether to prompt, save or open the file: "How browsers treat downloads varies by browser, user settings, and other factors." [Standard] https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a
- **TanStack Query 5:** "By default, TanStack Query will not retry a mutation on error", and callbacks passed to `mutate()` "won't run if your component unmounts before the mutation finishes". [Standard: library docs] https://tanstack.com/query/v5/docs/framework/react/guides/mutations

### Fixed values

- **Show the fixed participant as text, not as a disabled input.** WCAG 1.4.3 gives text in "an inactive user interface component" no contrast requirement, so disabled fields are often unreadable. Disabled fields are also skipped in tab order. GOV.UK shows answers that can't be changed as a summary list (`<dl>`). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html · [Convention] https://design-system.service.gov.uk/components/summary-list/

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **Two native `<input type="date">` fields**, labelled "From date" and "To date", stacked. **From** defaults to the participant's earliest **submitted** note date, and **To** to `today` from `/api/auth/me` (Melbourne, A33; never the device clock). Both get `max={today}`. **To** gets `min={from}` once From is a full date, so the picker greys out earlier days. Nothing is ever changed automatically when the other date changes. [Opinion, consistent with 4.7]
2. **Format is two stacked radios, "PDF" then "Word", with nothing pre-selected.** Each has a one-line hint taken from §11.5 and §11.6. [Convention: GOV.UK]
3. **"Include earlier versions of edited notes" is a native checkbox, unticked**, with a one-line hint. [A20 + Convention]
4. **Export is never disabled.** Checks run when it is pressed (`form-validation.md`). [Research + Convention]
5. **Export fetches the file and hands it to the browser** (`fetch` → `blob` → object URL → `<a download>`), as `primary-actions.md` sets out for downloads. The form keeps every value afterwards. [Convention, sibling spec]
6. **One visible `role="status"` line above Export** carries the busy reassurance, the success line and the no-notes result. One `role="alert"` container carries request failures. Both are always in the DOM. [Standard]

### Anatomy

```
[Error summary — only after a failed Export]          (form-validation.md)

Export record                                          <h1>, focused on arrival (app shell)
Participant                                            <dl><dt>
Jane Citizen                                           <dd>  plain text, never an input

From date                                              <label>
[ 02/02/2026   (calendar) ]                             <input type="date" max=today>
To date
[ 01/10/2026   (calendar) ]                             <input type="date" min=From max=today>

Format                                                 <fieldset><legend>
( ) PDF                                                radio
    For printing and filing                            item hint
( ) Word                                               radio
    Can be edited, so use it if you need to remove     item hint
    anything before handing it over

[ ] Include earlier versions of edited notes           checkbox (unticked)
    Adds each earlier version in full, which can       hint
    include corrected wording or another person's
    information

<role="alert">  request failure, empty until needed
<role="status"> busy / exported / no notes, empty until needed
[ Export ]                                             primary <button type="submit">
```

Everything sits in one `<form noValidate>` in one column. The error summary is the first thing in `<main>`, above the `<h1>`, as `form-validation.md` sets out. The participant row follows the `<h1>` so the name stays in view.

### Behaviour

**On arrival.** The page loads the participant's name and the first-note date (see Open questions: §6 has no field for the date yet). Until both arrive, the shell's loading pattern shows. The form appears with the defaults filled in, no format chosen, and the box unticked. If the participant has **no submitted notes**, From also defaults to today, and Export will then return the no-notes message. That is honest, and it needs no extra state. Archived participants can be exported: "History, reports and exports are unchanged" (§3.7).

**Editing the dates.** Native typing (desktop: segmented fields), or the platform picker (iOS compact calendar or wheels, Android calendar dialog). An incomplete date gives `value === ""`. Changing either date **clears the status line** without an announcement, because the old sentence named the old dates. If a date field already shows an error, changing either date re-checks it ("reward early", `form-validation.md`).

**Pressing Export** (or Enter in a date field, which is implicit submission):
1. If an export is already running, ignore the press. `aria-disabled` alone does not stop Enter.
2. Validate in page order: From, To (empty first, then before From), Format. On any error: inline messages, the summary, focus on the summary, `Error: ` added to the title. Nothing is sent.
3. Clear the status and alert lines. Send `GET /api/reports/participants/{id}/export?from=&to=&includeHistory=&format=pdf|docx` (§6.5). The query holds only dates, a flag and a format, which is allowed in URLs (§4.0).
4. The button goes busy at once (`aria-disabled`). After **400 ms** its label changes to "Exporting…", and the status line says **"Preparing the file. Keep this page open."** The 400 ms delay means a fast export shows no flash.
5. **Success:** save the blob under the server's file name, for example `participant-record_2026-01-01_to_2026-09-30.pdf`. The status line becomes **"Exported: participant-record_2026-01-01_to_2026-09-30.pdf"**, and focus stays on Export.
6. **No submitted notes in the range:** the status line shows the design sentence. No red, no summary, no focus move.
7. **Failure:** the alert container shows the message (copy below), and focus stays on Export, so pressing it again retries. Nothing retries automatically: each attempt rebuilds the file and writes a `participant.exported` audit row.
8. **401 (session ended):** the app's sign-in-in-place flow takes over (`app-shell-nav.md`), and the form keeps its values in memory.

**Leaving the page** while it is busy aborts the request (`AbortController`), and no file appears later. The server may still have written the audit row, because it writes it before sending the response (§5.4). That makes the log record a request the manager never received. This is conservative and needs no change.

**The success line differs from `primary-actions.md` on purpose.** That file gives downloads no in-page success message, because the daily report finishes in under a second and the browser's download UI confirms it. An export can take up to 30 seconds. After a long busy period, silence is ambiguous, and the browser's download notification is not reliably announced to screen readers (unverified). The line also names the file, so the manager can find it among other exports. [Opinion]

### States

| State | When | Visual | Semantics |
|---|---|---|---|
| **Default** | Page ready | Dates pre-filled, no format chosen, box unticked, Export primary | Native controls with labels. The participant is plain text |
| **Hover** | `(hover: hover) and (pointer: fine)` only | Date fields: border one step darker. Radio and checkbox rows: faint tint across the whole 44 px row (as `checkbox-list.md`). Button as `primary-actions.md` | Hover never carries information |
| **Focus** | `:focus-visible` | `outline: 3px solid var(--focus-ring); outline-offset: 2px` on the date field, on the radio or checkbox box, and on Export. No transition | Never removed. `outline`, not `box-shadow` |
| **Active** | Pointer down | Native for inputs. Button pressed fill (`primary-actions.md`) | Button activates on release (SC 2.5.2) |
| **Disabled** | **None on this form.** Export is never unavailable, because the design gives no rule and there is no pre-check. Fields are never disabled, even while busy | — | — |
| **Busy (loading)** | Export request in flight | Button keeps its look. After 400 ms: "Exporting…", `cursor: progress`, and the status line "Preparing the file. Keep this page open." Fields stay editable | `aria-disabled="true"` on the button. The status text is announced politely |
| **Error: validation** | Export pressed with a problem | Summary at top. 4px error bar on the field group. Message above the input. 3px error border on a date input | Focus on the summary. `aria-invalid` and `aria-describedby` on date inputs. The Format error is linked from the `<fieldset>` |
| **Error: request** | Network, 5xx, 429 | Bold error-colour text directly above Export | Written into the always-present `role="alert"`. Focus stays on Export |
| **Empty** | Server: no submitted notes in the range | Plain text in the status line, no error colour | Polite announcement. No focus move |
| **Success** | File handed to the browser | "Exported: {file name}" in the status line until a date changes or Export is pressed again | Polite announcement. Never auto-dismissed |
| **Read-only** | The participant | `<dt>` "Participant" and `<dd>` name, larger text | Plain text, not an `<input disabled>` |
| **Page loading / failed / not permitted** | Data not yet loaded, failed, or a worker | App shell patterns: "Loading…" after 1 s, the shell error with Try again, "Page not found" | As `app-shell-nav.md` |

### Phone vs laptop

| | Phone (below 40em) | Laptop (40em and up) |
|---|---|---|
| Column | Single column, 16 px gutters | Same order, form column `max-inline-size: 40rem` |
| Date fields | Full width, at least 44 px tall, font size at least 16 px | `max-inline-size: 14rem` (fits the date plus the calendar button) |
| Date entry | Tapping opens the platform picker; no keyboard opens | Typing in segments is fastest (NN/g, for dates more than a year away); the calendar button stays available |
| Format | Stacked radios with hints | Stacked as well. The hints rule out inline radios |
| Export | Full width, inline after the checkbox, never sticky | `width: auto`, left-aligned (`primary-actions.md`) |
| Status and alert lines | Directly above Export, so they show where the user just tapped | Same |

The two date fields stay stacked on a laptop too. Side by side, an error above To date would push its input out of line with From date. Stacking also reflows with no extra work at 400% zoom (SC 1.4.10).

### Exact copy

**design.md** marks design strings. *Proposed* marks new copy. Validation messages are from `form-validation.md`. Dates in messages use the app's long format, "Thursday 1 October 2026".

| Element | Text | Source |
|---|---|---|
| Page title | Grow2Notes – Export record | `app-shell-nav.md` page names |
| `<h1>` | Export record | *Proposed*: matches the "Export record" link (4.4, 4.8) |
| Fixed value | Participant / Jane Citizen | design.md 4.12 "Participant (fixed)" |
| Date labels | From date · To date | design.md 4.12 |
| Format legend | Format | design.md 4.12 |
| Radio labels | PDF · Word | design.md 4.12 ("PDF or Word"), in its order |
| PDF hint | For printing and filing | *Proposed*: §11.5 "The PDF is for printing and filing" |
| Word hint | Can be edited, so use it if you need to remove anything before handing it over | *Proposed*: §11.6, redaction "on the exported Word copy" |
| Checkbox label | Include earlier versions of edited notes | design.md 4.12 |
| Checkbox hint | Adds each earlier version in full, which can include corrected wording or another person's information | *Proposed*: §11.6 "each printed in full"; §3.9 wrong-participant content stays in the history |
| Button | Export | design.md 4.12 |
| Busy label | Exporting… | *Proposed*: matches "Downloading…" (`primary-actions.md`) |
| Busy status line | Preparing the file. Keep this page open. | *Proposed* [Opinion] |
| Success | Exported: participant-record_2026-01-01_to_2026-09-30.pdf | *Proposed*. The file name is the server's (§6.5) |
| No notes | No submitted notes for Jane Citizen between Thursday 1 January 2026 and Wednesday 30 September 2026. | design.md 4.12 |
| No notes, same day | No submitted notes for Jane Citizen on Thursday 1 October 2026. | *Proposed*: matches 4.7's wording, and avoids "between … and" the same date |
| From empty or incomplete | Enter the From date | `form-validation.md` |
| To empty or incomplete | Enter the To date | `form-validation.md` |
| To before From | To date must be the same as or after Thursday 1 January 2026 (the From date as entered) | `form-validation.md`, GOV.UK template. Shown on **To date** (design) |
| No format chosen | Choose PDF or Word | `form-validation.md` |
| Network failure | Not exported: no connection. Try again. | *Proposed*: the pattern of `primary-actions.md` |
| Server error | Not exported: something went wrong. Try again. | *Proposed*: same pattern |
| Rate limited (`429`, 10 per minute per user, §9.9) | Not exported: too many exports in the last minute. Wait a minute, then try again. | *Proposed* |
| Summary heading | There is a problem | `form-validation.md` |

### Accessibility

**Semantics.** `<form noValidate>`. `<label for>` on each date input. `<fieldset><legend>Format</legend>` around the radios. A plain `<input type="checkbox">` with its `<label>`: one option needs no fieldset. `<dl>` for the fixed participant. A `<button type="submit">` for Export, never a link, because it fetches a file and writes an audit row.

**ARIA, only where needed.**
- `aria-describedby` from each radio and from the checkbox to its hint.
- `aria-describedby` from a date input to its error message.
- `aria-describedby` from the `<fieldset>` to the Format error, as GOV.UK does.
- `aria-invalid="true"` on date inputs in error. None on radios: GOV.UK links the error from the fieldset instead, and the ARIA 1.3 draft lists `aria-invalid` for `radiogroup`, not `radio` (unverified).
- `aria-disabled` on Export while busy.
- One `role="status"` and one `role="alert"`, both rendered empty on load.
- No `aria-live` on inline errors, no `aria-busy`, and no `aria-label` on anything that has visible text.

**Keyboard.** Everything is native. Tab order: summary links (when shown), From date, To date, PDF, Word (arrow keys move between radios, which form one tab stop), the checkbox, then Export. How you move inside a date field and open its calendar varies by browser. Don't override it. Enter in a date field submits the form, which is safe because pressing Export only exports after checks.

**Screen reader announcements (expected; wording varies by screen reader):**
- On arrival: "Export record, heading level 1" (focus set by the app shell).
- "Format, group, PDF, radio button, not checked, 1 of 2, For printing and filing."
- "Include earlier versions of edited notes, checkbox, not checked, Adds each earlier version in full, …"
- After Export: "Preparing the file. Keep this page open." (only if it takes over 400 ms), then "Exported: participant-record_…pdf" **or** the no-notes sentence, all polite. A failure is assertive.
- Clear the status line when Export is pressed. If the same sentence comes back (for example, no notes twice), it is then a real change and is announced again.
- A failed check moves focus to the summary, which reads its heading and links (`form-validation.md`).

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships (labels, fieldset/legend, `dl`) · 1.3.2 Meaningful Sequence · 1.4.1 Use of Color (errors in words, plus the bar and border width) · 1.4.3 Contrast (Minimum) (the participant is real text, not a disabled field) · 1.4.4 Resize Text · 1.4.10 Reflow (stacked, no fixed widths below 40em) · 1.4.11 Non-text Contrast (input borders, radio and checkbox, focus at 3:1 or more) · 1.4.12 Text Spacing (no fixed heights) · 2.1.1 Keyboard · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) (`scroll-margin-top`, nothing sticky) · 2.5.2 Pointer Cancellation · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (44 px rows and fields) · 3.2.2 On Input (no field changes another field or starts an export) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions (hints) · 3.3.3 Error Suggestion · 3.3.7 Redundant Entry (defaults pre-filled; values kept after every outcome) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages (busy, exported, no notes). 3.3.4 does not apply, because nothing is modified or deleted.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria.** Native date, radio, checkbox and button cover every need here. Reconsider React Aria's `DatePicker` with `I18nProvider locale="en-AU"` **only** if go-live testing shows a manager's browser showing mm/dd/yyyy and causing mistakes. It is the one tool that would force the dd/mm/yyyy display. [Opinion]
- **Controlled inputs, plain `onSubmit`, no `<form action>`.** React 19 resets uncontrolled fields after an action "succeeds" (https://react.dev/reference/react-dom/components/form), and that would wipe the dates.
- **Compare dates as `yyyy-mm-dd` strings** (`to < from`). They sort correctly, and there is no time-zone arithmetic. Format them for messages with the app's shared long-date helper. Build it from `Intl.DateTimeFormat('en-AU', …).formatToParts` with `timeZone: 'UTC'` on `Date.UTC(y, m - 1, d)`, so a calendar date never shifts and no CLDR comma sneaks in (whether en-AU adds one is unverified).
- **IDs follow `form-validation.md`:** `export-from`, `export-to`, `export-format` (the first radio, so the summary link lands on it), `export-format-docx`, `export-includeHistory`. A `422` maps onto these by API key.
- **`Content-Disposition` from ASP.NET Core** usually looks like `attachment; filename=participant-record_….pdf; filename*=UTF-8''…`, often without quotes, so parse with `/filename="?([^";]+)"?/`. The response is same-origin, so the header is readable.
- **Abort on unmount,** and do the save in the per-call `mutate(…, { onSuccess })` callback, which "won't run if your component unmounts". Leave TanStack's default of no retries for this mutation.
- **Never `display: none` the status or alert line,** even when empty (MDN, live regions). Collapse the spacing with `:not(:empty)` margins instead.
- ~~`color-scheme: light dark`~~ **`color-scheme: light`** on `:root` (global, without `only`), as foundations.md sets for the whole app, so the native date picker stays light (record-export.md Conflicts #11).
- Test on iOS Safari and Android Chrome under the production CSP before M5 sign-off. Check the blob download prompt, the picker, and the 16 px font that prevents zoom on focus (all unverified on those two).

```tsx
// ExportRecordForm.tsx — sketch. Button/ButtonGroup: primary-actions.md. ErrorSummary: form-validation.md.
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import s from './ExportRecordForm.module.css';

type Format = 'pdf' | 'docx';
type Field = 'from' | 'to' | 'format';
type Errors = Partial<Record<Field, string>>;
type ExportQuery = { from: string; to: string; format: Format; includeHistory: boolean };
const ORDER: Field[] = ['from', 'to', 'format'];
const IDS: Record<Field, string> = { from: 'export-from', to: 'export-to', format: 'export-format' };

function validate(from: string, to: string, format: Format | null): Errors {
  const e: Errors = {};
  if (!from) e.from = 'Enter the From date';                       // '' = empty or incomplete
  if (!to) e.to = 'Enter the To date';
  else if (from && to < from) e.to = `To date must be the same as or after ${longDate(from)}`;
  if (!format) e.format = 'Choose PDF or Word';
  return e;
}

export function ExportRecordForm({ participant, firstNoteDate, today }: {
  participant: { id: string; fullName: string };
  firstNoteDate: string | null;   // earliest SUBMITTED note (see Open questions)
  today: string;                  // /api/auth/me — Melbourne, never the device clock
}) {
  const [from, setFrom] = useState(firstNoteDate ?? today);
  const [to, setTo] = useState(today);
  const [format, setFormat] = useState<Format | null>(null);   // nothing pre-selected
  const [includeHistory, setIncludeHistory] = useState(false); // A20
  const [errors, setErrors] = useState<Errors>({});
  const [attempt, setAttempt] = useState(0);
  const [status, setStatus] = useState('');    // role="status"
  const [failure, setFailure] = useState('');  // role="alert"
  const abort = useRef<AbortController | null>(null);
  useEffect(() => () => abort.current?.abort(), []);

  const exportFile = useMutation({
    mutationFn: (q: ExportQuery) => {
      abort.current = new AbortController();
      return fetchExport(participant.id, q, abort.current.signal);
    },
  });

  function changeDate(which: 'from' | 'to', value: string) {
    const f = which === 'from' ? value : from;
    const t = which === 'to' ? value : to;
    (which === 'from' ? setFrom : setTo)(value);
    setStatus('');                                    // the old sentence named the old dates
    if (errors.from || errors.to) {                   // re-check only what is already shown
      const again = validate(f, t, format);
      setErrors((e) => ({ ...e, from: e.from && again.from, to: e.to && again.to }));
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (exportFile.isPending) return;                 // Enter still submits an aria-disabled button's form
    const found = validate(from, to, format);
    setErrors(found);
    setAttempt((n) => n + 1);
    if (ORDER.some((k) => found[k])) return;
    setStatus(''); setFailure('');
    const slow = window.setTimeout(() => setStatus('Preparing the file. Keep this page open.'), 400);
    const q = { from, to, format: format!, includeHistory };
    exportFile.mutate(q, {                            // these callbacks don't run after unmount
      onSuccess: (file) => { saveFile(file); setStatus(`Exported: ${file.name}`); },
      onError: (err) => {
        if (isAbort(err) || isUnauthorised(err)) return;   // 401: the shell's sign-in flow takes over
        if (isNoNotes(err)) return setStatus(noNotesMessage(participant.fullName, q.from, q.to));
        if (isValidation(err)) { setErrors(fieldErrors(err)); setAttempt((n) => n + 1); return; }
        setFailure(isRateLimited(err) ? 'Not exported: too many exports in the last minute. Wait a minute, then try again.'
          : isOffline(err) ? 'Not exported: no connection. Try again.'
          : 'Not exported: something went wrong. Try again.');
      },
      onSettled: () => window.clearTimeout(slow),
    });
  }

  const list = ORDER.filter((k) => errors[k]).map((k) => ({ fieldId: IDS[k], message: errors[k]! }));

  return (
    <>
      <ErrorSummary errors={list} attempt={attempt} />
      <h1 tabIndex={-1} className={s.h1}>Export record</h1>
      <dl className={s.fixed}><dt>Participant</dt><dd>{participant.fullName}</dd></dl>

      <form noValidate onSubmit={onSubmit} className={s.form}>
        <DateField id={IDS.from} label="From date" value={from} max={today}
          error={errors.from} onChange={(v) => changeDate('from', v)} />
        <DateField id={IDS.to} label="To date" value={to} min={from || undefined} max={today}
          error={errors.to} onChange={(v) => changeDate('to', v)} />

        <fieldset className={s.group} data-invalid={errors.format ? '' : undefined}
          aria-describedby={errors.format ? 'export-format-error' : undefined}>
          <legend className={s.label}>Format</legend>
          {errors.format && <p id="export-format-error" className={s.message}>
            <span className="visually-hidden">Error: </span>{errors.format}</p>}
          <Choice type="radio" name="format" id="export-format" label="PDF" hint="For printing and filing"
            checked={format === 'pdf'} onChange={() => { setFormat('pdf'); setErrors((e) => ({ ...e, format: undefined })); }} />
          <Choice type="radio" name="format" id="export-format-docx" label="Word"
            hint="Can be edited, so use it if you need to remove anything before handing it over"
            checked={format === 'docx'} onChange={() => { setFormat('docx'); setErrors((e) => ({ ...e, format: undefined })); }} />
        </fieldset>

        <Choice type="checkbox" id="export-includeHistory" label="Include earlier versions of edited notes"
          hint="Adds each earlier version in full, which can include corrected wording or another person's information"
          checked={includeHistory} onChange={(e) => setIncludeHistory(e.currentTarget.checked)} />

        <div className={s.actions}>
          <div role="alert" className={s.failure}>{failure}</div>
          <p role="status" className={s.status}>{status}</p>
          <ButtonGroup>
            <Button type="submit" variant="primary" busy={exportFile.isPending} busyLabel="Exporting…">Export</Button>
          </ButtonGroup>
        </div>
      </form>
    </>
  );
}

function DateField({ id, label, value, min, max, error, onChange }: {
  id: string; label: string; value: string; min?: string; max: string; error?: string; onChange: (v: string) => void;
}) {
  const errorId = `${id}-error`;
  return (
    <div className={s.group} data-invalid={error ? '' : undefined}>
      <label htmlFor={id} className={s.label}>{label}</label>
      {error && <p id={errorId} className={s.message}><span className="visually-hidden">Error: </span>{error}</p>}
      <input id={id} type="date" className={s.date} value={value} min={min} max={max}
        aria-invalid={error ? true : undefined} aria-describedby={error ? errorId : undefined}
        onChange={(e) => onChange(e.currentTarget.value)} />
    </div>
  );
}

async function fetchExport(participantId: string, q: ExportQuery, signal: AbortSignal) {
  const params = new URLSearchParams({ from: q.from, to: q.to, format: q.format, includeHistory: String(q.includeHistory) });
  const res = await fetch(`/api/reports/participants/${participantId}/export?${params}`, { signal });
  if (!res.ok) throw await problemFrom(res);                  // RFC 9457 → { status, code, errors }
  const header = res.headers.get('Content-Disposition') ?? '';
  const name = /filename="?([^";]+)"?/.exec(header)?.[1] ?? `participant-record_${q.from}_to_${q.to}.${q.format}`;
  return { blob: await res.blob(), name };
}

function saveFile({ blob, name }: { blob: Blob; name: string }) {
  const url = URL.createObjectURL(blob);
  const a = Object.assign(document.createElement('a'), { href: url, download: name });
  document.body.append(a); a.click(); a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);  // slow mobile download managers
}

function noNotesMessage(name: string, from: string, to: string) {
  return from === to
    ? `No submitted notes for ${name} on ${longDate(from)}.`
    : `No submitted notes for ${name} between ${longDate(from)} and ${longDate(to)}.`;
}
```

```css
/* ExportRecordForm.module.css — tokens come from the theme */
.form { display: grid; gap: var(--space-6); max-inline-size: 40rem; }
.fixed { margin: 0 0 var(--space-6); }
.fixed dt { font-weight: 700; }
.fixed dd { margin: 0; font-size: var(--text-lg); }

.group { margin: 0; padding: 0; border: 0; min-inline-size: 0;
         scroll-margin-top: calc(var(--top-bar-height) + var(--space-4)); }
.group[data-invalid] { border-inline-start: 4px solid var(--colour-error); padding-inline-start: var(--space-3); }
.label { display: block; padding: 0; margin-block-end: var(--space-2); font-weight: 700; }
.message { margin: 0 0 var(--space-2); color: var(--colour-error-text); font-weight: 700; }

.date {
  box-sizing: border-box;
  inline-size: 100%;
  min-block-size: 2.75rem;                 /* 44 px (A32) */
  padding: 0.5rem 0.75rem;
  font: inherit;
  font-size: max(1rem, 16px);              /* iOS zooms on focus below 16 px (unverified) */
  color: var(--text);
  background: var(--surface);
  border: 2px solid var(--input-border);   /* >= 3:1 against the page */
  border-radius: 0.25rem;
}
.date::-webkit-date-and-time-value { text-align: start; }   /* iOS centres the value (unverified) */
@media (min-width: 40em) { .date { max-inline-size: 14rem; } }
@media (hover: hover) and (pointer: fine) { .date:hover { border-color: var(--input-border-hover); } }
.date:focus-visible { outline: 3px solid var(--focus-ring); outline-offset: 2px; }
.date[aria-invalid="true"] { border: 3px solid var(--colour-error); }

/* Live regions stay in the DOM; collapse only their spacing when empty */
.failure, .status { margin: 0; }
.failure:not(:empty) { margin-block-end: var(--space-4); color: var(--colour-error-text); font-weight: 700; }
.status:not(:empty) { margin-block-end: var(--space-4); }

@media (forced-colors: active) {
  .date { border-color: CanvasText; }
  .date:focus-visible { outline-color: Highlight; }
}
```

`Choice` is the app's shared native radio or checkbox row from `checkbox-list.md`: a large visible box, the whole row as the label, at least 44 px, `accent-color` or the clip technique, never `display: none`. The only addition here is an optional `hint` linked by `aria-describedby`.

---

## Per-screen notes

**4.12 Participant record export (the only screen).**
- **Entry points.** "Export record" is a secondary `<Link>` styled as a button (`primary-actions.md`) in the actions on Participant detail (4.8) and Participant notes (4.4). Use an ID-only URL, with no dates or names in the path. Browser Back returns to where the manager came from.
- **Archived participants.** The link stays under 4.8's read-only banner, because exports are "unchanged" by archiving (§3.7). Nothing on the export screen marks the participant as archived, because the file is the same either way.
- **Defaults are the main path.** Most access requests are for "everything" (APP 12, HPP 6), and the defaults already cover the full record. The usual flow is: choose a format, press Export. Tick the box only if the request needs the history.
- **The form never remembers anything between visits.** No last-used format, and no stored dates (D22; no settings).
- **After the file is saved,** the design's next steps (read it, redact on the Word copy, hand it over) happen outside the app (§4.13, §11.6). The form doesn't prompt for them. The Word hint and the checkbox hint state the facts the manager needs when choosing.
- **Compared with the Daily report (4.7):**
  - Same native date field, same `fetch`-then-save mechanism, same busy timing.
  - Different: one Export with a format choice, as the design specifies, and no pre-check, so the empty result appears after the press.
  - The order also differs: 4.7 lists Word first, while 4.12 lists PDF first, which is also alphabetical. Each screen keeps the design's own order.

---

## Anti-patterns to avoid

- A `<select>` for Format, or a toggle or segmented control: two options belong in radios.
- Pre-selecting a format, or pre-ticking "Include earlier versions" (A20).
- A custom JavaScript calendar, or React Aria date components, when the native field works (see the one exception above). Also, replacing the native field with a text box that has a `pattern` but no picker.
- Trying to force dd/mm/yyyy by hiding the native field or overlaying text on it.
- Changing To date automatically when From date changes, or "fixing" a reversed range silently.
- Disabling Export until the form is valid, or until the dates have notes.
- Validating on change or blur, or showing "To date must be…" while the manager is part-way through changing From.
- Showing "No submitted notes…" in red, in the error summary, or by moving focus to it.
- A confirmation dialog before Export (3.3.4 doesn't apply; it would add a step).
- A fake percentage bar or a time estimate the server can't back up. Also a Cancel button the design doesn't specify: leaving the page cancels.
- Navigating the browser to the API URL (`window.location`, `<a href>`, `<form method="get">`). A 401, 404 or 429 would then show raw JSON, and the busy and result states would be lost.
- Retrying the export automatically: each attempt is a full rebuild and another audit row.
- Creating the status or alert element only when there is something to say, or hiding it with `display: none` while empty.
- Auto-dismissing the success, no-notes or failure line.
- Clearing the dates, format or checkbox after an export or an error.
- Showing the participant as a greyed-out `<input disabled>`.
- Putting the participant's name in the URL, the page title or the file name (§4.0, §6.5).
- Adding things the design doesn't specify: an on-screen preview of the record (the D43 principle), email or share buttons (D28, D30), a remembered default format, a note count for the range (D26), or a "first note was on …" hint.

---

## Tensions with decisions

1. **Long synchronous export with no progress measure (design §7.1, "generation is synchronous", with no background workers; M5: "A one-year export with earlier versions … in under 30 seconds").**
   - **Evidence:** Nielsen says waits over 10 seconds need "feedback indicating when the computer expects to be done", and NN/g recommends percent-done indicators for "10 or more seconds". [Convention, expert synthesis] https://www.nngroup.com/articles/response-times-3-important-limits/ · https://www.nngroup.com/articles/progress-indicators/
   - **Why it can't be met:** a single synchronous request can't report progress.
   - **Mitigation:** the busy label, the "Preparing the file. Keep this page open." line, and a persistent result line. No change is proposed.
2. **The native date field (design 4.7 "a native date field"; this form follows it for consistency).**
   - **Evidence:** expert AT testing found Dragon could not use `input type="date"`, and VoiceOver on iOS did not announce browser validation messages. [Research, Hassell Inclusion 2019] https://hassellinclusion.com/blog/input-type-date-ready-for-use/ a11ysupport.io rates support as partial (counts unverified). GOV.UK recommends its three-field date input for dates people already know. [Convention] https://design-system.service.gov.uk/components/date-input/ AgDS asks for a visible dd/mm/yyyy format, which the native field can't guarantee. [Convention]
   - **Mitigation:** the dates are pre-filled, errors are the app's own and not browser bubbles, and every message spells out the date in words. No change is proposed.

---

## Sources

- design.md §3.7, §3.9, §4.0, §4.4, §4.7, §4.8, §4.12, §5.4, §6.5, §6.9, §7.1, §9.6, §9.9, §11.5, §11.6, §13 (A13, A19, A20, A30, A33), §14 M5; decisions.md D22, D26, D28, D30, D43
- Sibling specs: `form-validation.md`, `primary-actions.md`, `app-shell-nav.md`, `checkbox-list.md`
- GOV.UK Design System, Date input: https://design-system.service.gov.uk/components/date-input/
- GOV.UK Design System, Dates pattern: https://design-system.service.gov.uk/patterns/dates/
- GOV.UK Design System, Radios: https://design-system.service.gov.uk/components/radios/
- GOV.UK Design System, Checkboxes: https://design-system.service.gov.uk/components/checkboxes/
- GOV.UK Design System, Select: https://design-system.service.gov.uk/components/select/
- GOV.UK Design System, Text input (hint text): https://design-system.service.gov.uk/components/text-input/
- GOV.UK Design System, Validation pattern: https://design-system.service.gov.uk/patterns/validation/
- GOV.UK Design System, Button (disabled buttons): https://design-system.service.gov.uk/components/button/
- GOV.UK Design System, Summary list: https://design-system.service.gov.uk/components/summary-list/
- Agriculture Design System (AgDS), Date range picker: https://design-system.agriculture.gov.au/components/date-range-picker
- NN/g, Date-Input Form Fields (Angie Li, 2017): https://www.nngroup.com/articles/date-input/
- NN/g, Checkboxes vs. Radio Buttons (Jakob Nielsen, 2004): https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/
- NN/g, Response Times: The 3 Important Limits (Jakob Nielsen, 1993, updated 2014): https://www.nngroup.com/articles/response-times-3-important-limits/
- NN/g, Progress Indicators: https://www.nngroup.com/articles/progress-indicators/
- Hassell Inclusion, Is input type="date" ready for use in accessible websites? (Graham Armfield, 12 Feb 2019): https://hassellinclusion.com/blog/input-type-date-ready-for-use/
- a11ysupport.io, input type=date (counts not machine-readable; unverified): https://a11ysupport.io/tech/html/input(type-date)_element
- Bargas-Avila et al. (2007), *Interacting with Computers* 19(3): https://academic.oup.com/iwc/article-abstract/19/3/330/693000
- MDN, `<input type="date">`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- MDN, `<a>` and the `download` attribute: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a
- MDN, ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 3.3.4 Error Prevention (Legal, Financial, Data): https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- WCAG 2.2 Understanding 1.4.3 Contrast (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- WCAG technique H71 (fieldset and legend): https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- React, `<form>` (action reset behaviour): https://react.dev/reference/react-dom/components/form
- TanStack Query 5, Mutations: https://tanstack.com/query/v5/docs/framework/react/guides/mutations
