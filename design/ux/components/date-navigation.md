# Date navigation and date fields

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The Daily report tells the shell it is one screen through `handle: { screenKey: 'dailyReport' }`, not `state: { inScreen: true }` (app-shell.md, daily-report.md).

Component key: `date-navigation`. This covers every place a person picks a calendar date in Grow2Notes: the Daily report's **Previous day / date field / Next day** (design.md 4.7), the manager's **Write past-day note** date (4.8, also linked from 4.4), the **From date / To date** of the participant record export (4.12), and the participant's **date of birth** (4.8 Details, A5), which form-validation.md left to this component. It also sets the one shared way the app works out "today" and writes dates. It adds no screens, settings or data.

Evidence grades: **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML spec, MDN platform docs, framework docs · **[Convention]** established design systems · **[Opinion]** reasoned judgement with no direct evidence.

---

## Where it's used

| Screen (design.md) | Control | Allowed dates | What differs |
|---|---|---|---|
| **4.7 Daily report** | **Previous day**, a native date field, **Next day**; then Download Word / Download PDF | Any date up to today (Melbourne). "Future dates cannot be picked, so Next day is disabled on today." | No submit button: the date *is* the screen's state and lives in the URL (`/reports/daily/2026-10-01`). Changing it only re-asks `has-notes` (6.5) to turn the downloads on or off. Opens on today. |
| **4.8 Participant detail → Write past-day note** (the same link also sits on 4.4 Participant notes) | One native date field and **Continue** | Past dates only (up to yesterday) | A one-field form that leads to the note for that participant and date. "If that date already has a note, it opens; otherwise the note form (4.3) opens for that date with the past-day banner." Managers only. Not offered for an archived participant (3.7: "No new notes, including past-day notes"). |
| **4.12 Participant record export** | **From date**, **To date** (native date fields) inside the export form | Pre-filled: "the participant's first note to today" | Part of a multi-field form. The cross-field rule ("To date before From date: an error next to the field") and its copy belong to form-validation.md. No navigation. |
| **4.8 Participant detail → Details** | **Date of birth** | A remembered date in the past | Not a calendar choice. Three text boxes (Day, Month, Year), see Recommendation. form-validation.md: "the control is the date component's decision". |
| **Not used:** the note form (4.3) | — | — | "Workers can only write a note dated today, so the worker's form has no date picker" (3.3, D35). The note date is fixed by the server at the first change (A8). |
| **Shared by every screen** | The date formatter and `today` | — | "Today · Thursday 1 October" (4.2), "Thursday 1 October 2026" (4.3 header, 4.7 message), "Mon 28 Sep 2026" (4.3 banners) all come from one formatter fed by the server's `today`. |

---

## Best practice

### Native date input, custom picker, or text boxes

- **[Convention]** GOV.UK and NHS use three text boxes (day, month, year) "when you're asking users for a date they'll already know, or can look up without using a calendar", and say not to use it "if users are unlikely to know the exact date". GOV.UK's testing found "hundreds of users were inputting months using full or abbreviated month names", so it now accepts "jan" or "january". https://design-system.service.gov.uk/components/date-input/ · https://service-manual.nhs.uk/design-system/components/date-input
- **[Convention]** The MOJ date picker is for "a relative date or one they need to look up", never for "a memorable date, such as a user's date of birth". It keeps a typed text input next to the calendar and warns that pickers "can be slow for keyboard-only and screen reader users". https://design-patterns.service.justice.gov.uk/components/date-picker
- **[Convention]** NN/g (Li, 2017, expert guidance with no study cited): use calendar pickers for dates "within less than a year" of now; always allow typing; spell out month names for mixed audiences; grey out impossible dates. https://www.nngroup.com/articles/date-input/
- **[Research]** Hassell Inclusion tested `type="date"` on 9 browser and assistive-technology combinations (Armfield, February 2019, now-old browser versions). Dragon: "It seems impossible to successfully interact with date inputs". VoiceOver on iOS never voiced the browser's error messages. TalkBack users had to open the calendar to hear the chosen date. Verdict: not ready. Whether this is still true in 2026 is **unverified**. https://hassellinclusion.com/blog/input-type-date-ready-for-use/
- **[Research]** (practitioner audit) Roselli found 8 WCAG failures in the popular ReactJS Datepicker, calls the native input a "problem for voice users", and argues that "Users generally do not want a complex date picker every time you ask for any date." https://adrianroselli.com/2019/07/maybe-you-dont-need-a-date-picker.html
- **[Convention]** AgDS (the Australian Government design system) prefers "dd/mm/yyyy" and warns against "USA mm/dd/yyyy". In its picker, a typed date outside the allowed range is replaced by "the closest valid date". https://design-system.agriculture.gov.au/components/date-picker
- **[Standard]** React Aria's DatePicker draws its own segmented field and calendar popover on every device rather than the phone's own picker, and needs `@internationalized/date`. https://react-aria.adobe.com/DatePicker

Together: a calendar suits a **chosen date near today** (the report, a forgotten note from last week); text boxes suit a **remembered date** (a birthday). Nobody recommends a home-made calendar widget.

### What the native date input really does on each platform

- **[Standard]** MDN: "the displayed date is formatted *based on the locale of the user's browser*, but the parsed `value` is always formatted `yyyy-mm-dd`." A manager whose laptop is set to US English sees 10/01/2026 for 1 October. https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- **[Standard]** MDN: with `min` and `max` "the form control disables invalid dates" in its picker. This holds on Android and desktop browsers.
- **[Research]** (bug reports) **iPhone and iPad ignore `min` and `max`.** WebKit bug 225639, "[iOS] HTML datepicker's min-max attribute not working", has been open (status NEW) since May 2021 and was last modified in February 2026. Every iOS browser uses WebKit. Jeremy Keith confirmed it in April 2024, and MDN's compatibility data was challenged for showing full support. A manager on an iPhone can pick tomorrow. https://bugs.webkit.org/show_bug.cgi?id=225639 · https://adactio.com/journal/21050 · https://github.com/mdn/browser-compat-data/issues/26656
- **[Standard]** On desktop the date can be **typed** segment by segment, and a typed value is not clamped to `max`; it only fails constraint validation (MDN, same page).
- **[Research]** (bug report) Chrome was reported to fire `change` "on every date-part" while typing, where Firefox waited for the field to lose focus (w3c/html#194, 2016). Current behaviour is **unverified**. Any design that acts on each change can act on half-typed dates. https://github.com/w3c/html/issues/194
- **[Convention]** iOS Safari zooms the page when a field's text is under 16px. https://www.stefanjudis.com/notes/mobile-safari-doesnt-zoom-into-form-inputs-with-minimum-16px/ (already applied in sign-in-form.md)

### Previous / next stepping

- **[Convention]** GOV.UK pagination makes Previous and Next **links**, in a `<nav>`, and says: "Do not show the previous page link on the first page – and do not show the next page link on the last page." https://design-system.service.gov.uk/components/pagination/
- **[Convention]** To disable a link: remove `href`, add `role="link"` and `aria-disabled="true"`. Without `href` it "will not be keyboard focused". Scott O'Hara. https://www.scottohara.me/blog/2021/05/28/disabled-links.html
- **[Standard]** If the focused element stops being focusable, the browser moves focus to the document body (HTML "focus fixup"). https://html.spec.whatwg.org/multipage/interaction.html · React: "When you render a different component in the same position, it resets the state of its entire subtree", which means a new DOM node, and focus is lost. https://react.dev/learn/preserving-and-resetting-state So a Next day that turns from a `<Link>` into something else, or loses `href`, while it has focus, drops a keyboard user onto `<body>`.
- **[Standard]** WCAG 2.5.3 Label in Name: the accessible name contains the visible words. 2.5.8 Target Size (Minimum): 24 × 24 px floor (the app uses 44, design 4.0). https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html · https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

### URL, Back button and focus

- **[Research]** Baymard (2020, large e-commerce usability studies): users "perceive each discrete iteration of their filtering as a new view", and "Tapping 'Back' after applying filters should begin removing the applied filters". Use `history.pushState()` for changes users see as a new view. https://baymard.com/blog/back-button-expectations
- **[Standard]** WCAG 3.2.2 On Input: changing a control must not cause a change of context. "Moving focus to a different component" or "going to a new page (including anything that would look to a user as if they had moved to a new page)" counts. A change of content does not always count. Updating the date, a line of text and two buttons while focus stays put is a change of content. https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- **[Standard]** React Router's `useLinkClickHandler(to, { replace, state, preventScrollReset })` gives a plain `<a>` the same client-side behaviour as `<Link>`. https://reactrouter.com/api/hooks/useLinkClickHandler
- **[Standard]** TanStack Query's `placeholderData` / `keepPreviousData` keeps "displaying 'old' data" from the previous query key while the new one loads. https://tanstack.com/query/v5/docs/framework/react/guides/placeholder-query-data
- **[Standard]** WCAG 4.1.3 Status Messages: a result that doesn't take focus must still be announced, through a status role. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

### "Today" and date arithmetic

- **[Standard]** (design) "'Today' is the current date in Australia/Melbourne, worked out on the server, never from the device clock" (3.3, D37, A33). `GET /api/auth/me` returns `today`, which "drives every 'today' in the UI" (6.2). Dates in the API are `YYYY-MM-DD` Melbourne calendar dates (6.1).
- **[Opinion, tested]** In Node 24 with `TZ=Australia/Melbourne` (run while writing this): `new Date(2026, 9, 1).toISOString()` gives `2026-09-30T14:00:00.000Z`. Local midnight on 1 October becomes **30 September**. Adding 86,400,000 ms to local midnight on 5 April 2026, when daylight saving ends, gives `Sun Apr 05 2026 23:00`, the **same day**. Daylight saving starts on Sunday 4 October 2026, three days after go-live. Calendar dates must stay strings and be stepped in UTC.
- **[Opinion, tested]** `Intl.DateTimeFormat('en-AU')` gave "Thursday 1 October 2026" for the long form, but "Thu, 1 Oct 2026" (with a comma) for the short form, where the design writes "Mon 28 Sep 2026". Output can also differ between ICU versions in different browsers. A fixed English formatter is safer for a single-language app.
- **[Convention]** The Australian Government Style Manual advises against abbreviated dates in body text, because "Words written in full are usually easier to read and understand" (as quoted in status-messages.md; the page timed out when re-checked for this file). https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **Native `<input type="date">`** for the report, the past-day note and the export dates. These are chosen dates near today, on phones and laptops; the phone's own picker is familiar, and the design asks for a native field (4.7). **No custom calendar and no React Aria DatePicker.** [Convention + Standard]
2. **Date of birth uses three text boxes** (Day, Month, Year), the GOV.UK pattern for a remembered date. Both phone pickers open on the current month, and a birthday is decades away. [Convention]
3. **`today` always comes from `me.today`** (server, Melbourne). `max`, Next day, "in the past" and the report's opening date all use it. Nothing reads the device clock. [Standard: design 3.3]
4. **`min` and `max` are only hints.** iOS ignores them and desktop typing gets past them. The app checks every date in code, and the server stays the gate (422 `note.future_date`; 404 `report.no_notes`). [Research: WebKit 225639]
5. **Dates in words next to every native field that drives an action.** The report always shows the chosen date as "Thursday 1 October 2026", because the field itself shows whatever format the device uses. [Standard: MDN + Convention: AgDS, Style Manual]
6. **Previous day and Next day are links** (they change the URL), styled as secondary buttons (primary-actions.md). On today, Next day stays the **same `<a>` element**, made unavailable with `aria-disabled` and kept focusable, so focus is never lost. [Standard + Convention]
7. **Report date changes are in-screen navigations.** Each one adds one history entry, so Back steps back through the days the manager looked at. Focus stays where it is, the page doesn't scroll, and the new date is announced through a status line. [Research: Baymard + Standard: 3.2.2, 4.1.3]
8. **A typed date is committed only when it is complete**: on Enter, on leaving the field, or after a short pause. Half-typed years never reach the URL, the history or a screen reader. [Opinion, grounded in w3c/html#194]

### Anatomy

**Daily report, phone (one column):**
```
Daily report                                   <h1> (app shell focuses it on arrival)
Date                                           <label for="report-date">
[ 1 Oct 2026                         ▾ ]       <input type="date" max={today}>  (device's own format)
[ ‹ Previous day ]  [   Next day ›   ]         two <a> styled as secondary buttons, 48px tall, half width each
Thursday 1 October 2026                        <p role="status"> date in words, or the design's empty sentence
[          Download Word          ]            primary-actions.md
[          Download PDF           ]
```

**Daily report, laptop (same DOM order, one row when it fits):**
```
Daily report
Date
[ 01/10/2026  📅 ]   [ ‹ Previous day ]   [ Next day › ]
Thursday 1 October 2026
[ Download Word ]  [ Download PDF ]
```

**Report field in error:**
```
┃ Date
┃ Date must be today or in the past            message above the input (form-validation.md pattern)
┃ [ 2 Oct 2026                        ▾ ]     3px error border, aria-invalid="true"
[ ‹ Previous day ]  [   Next day ›   ]
                                               status line empty (a hidden copy of the error is announced)
```

**Write past-day note (its own page, reached by the "Write past-day note" link):**
```
Jane Citizen                                   caption (participant name, not in the title or URL)
Write past-day note                            <h1>
Note date                                      <label>
[                                    ▾ ]       <input type="date" max={yesterday}>, starts empty
[ Continue ]                                   primary submit button
```

**Export dates** sit inside the export form as two ordinary fields, "From date" and "To date", each with its own label, pre-filled.

**Date of birth:**
```
Date of birth                                  <legend> of <fieldset>
For example, 27 3 1985                         hint
Day      Month    Year                         <label>s
[    ]   [    ]   [        ]                   type="text" inputmode="numeric", widths 2ch / 2ch / 4ch (+ padding)
```

The layout is the same for every role that can see it. Workers never see any of these controls (Report and Manage are manager-only, 4.0).

### Behaviour

**Daily report (4.7)**

1. **Arrival.** The route is `/reports/daily/:date`. The Report nav link already points at `me.today` (app-shell-nav.md). If `:date` is missing, go to today with `replace`. If it is not a real `YYYY-MM-DD` date, show the app's not-found screen. If it is after `me.today`, go to today with `replace`, so a future date is never on screen or in history. The field starts at the URL date.
2. **Two values.** The **URL date** is the committed date: downloads, `has-notes`, Previous/Next and the status line all use it. The **field value** is a draft until it is committed.
3. **Committing the field.** A change is committed:
   - on **Enter** in the field;
   - on **leaving the field** (blur);
   - **750 ms after the last change** while the field still has focus. This is needed because Android's picker dialog returns focus to the field without a blur. A commit after the pause is silent: it goes ahead only if the value is a real date, not after today, and has a year of 1900 or later. Otherwise it waits.

   Enter and blur commits check the value and show an error if needed (States, below). The 750 ms value is [Opinion]: tune it after watching managers use it.
4. **History.** The first commit after the field gains focus **pushes** a history entry. Later commits during the same visit to the field **replace** it, so one picking session adds one Back step however many values passed through. Previous day and Next day always push (they are links). A commit of the date already shown does nothing.
5. **No jump.** Every date change keeps focus where it is (the field or the link), keeps the scroll position (`preventScrollReset`), and tells the app shell it is not a new screen (`state: { inScreen: true }`), so the shell neither scrolls to the top nor focuses the `<h1>`. The report route is **not** keyed by date, so the same DOM stays in place.
6. **Previous day** is always available (there is no earliest date). **Next day** goes to the URL date + 1 and is unavailable when the URL date is `me.today`. The arithmetic uses the UTC helpers below, never `Date.now()`.
7. **Midnight.** `me.today` is refreshed on page load, screen change, focus and visibility (6.8). If the report stays open past midnight, the field's `max` and Next day update by themselves. The URL date does not change.
8. **The has-notes check** (`GET /api/reports/daily/{date}/has-notes`) runs for the committed date only. It never shows the previous day's answer while the new one loads (no `placeholderData`). It refetches when the window regains focus, because a manager may leave the screen open while a worker submits.
9. **Downloads are available only when** the field value equals the URL date, there is no field error, **and** `hasNotes` is `true` **or** the check failed (then the export endpoint is the gate, as primary-actions.md says). The download handler reads the URL date when pressed, never a value captured earlier. So a press can never fetch a different day from the one shown.
10. **Status line** (`role="status"`, in the DOM from the first render):
    - the URL date in words, for example "Thursday 1 October 2026", while checking and when there are notes;
    - "No submitted notes for Thursday 1 October 2026." when `hasNotes` is `false` (design 4.7);
    - empty while the field holds an uncommitted or invalid value, so it never names a date that isn't the one in the field. In error it holds a visually hidden copy of the error, so a blur error is still announced.

**Write past-day note (4.8, also linked from 4.4)**

1. Reached by the "Write past-day note" link (a link to its own URL, per primary-actions.md). Managers only. The route renders no field for an archived participant and shows the participant's archived read-only banner instead (3.7). The banner component owns that copy.
2. The field starts **empty**. `max` = the day before `me.today`. No `min` (see Open questions).
3. **Continue** validates (form-validation.md one-field pattern): show the inline error, set `aria-invalid`, and focus the field. Nothing turns red before Continue is pressed.
4. If valid: navigate (push) to the note route for that participant and date (for example `/participants/{id}/notes/2026-09-28`). That route fetches the note and decides what opens: the existing note (read view, own draft, or a manager's read-only view of someone else's draft) or a new past-day form with the banner "Past-day note for Mon 28 Sep 2026, written on 1 Oct." (4.3). This page never decides from cached data.
5. The server still refuses a future date (`422 note.future_date`). The note form owns that message.

**Participant record export (4.12)**

1. "From date" and "To date" are pre-filled: From = the participant's first note date (see Open questions), To = `me.today`. Both have `max={me.today}`, which helps the picker but is not a rule: the design defines no "future To date" error, and no note can exist after today, so a typed future To date is not an error.
2. Checked on **Export** only, using form-validation.md's rules and copy (empty dates; To before From, with the error on To date).
3. The dates go to the API as `YYYY-MM-DD` query values. Dates are allowed in URLs (4.0).

**Date of birth (4.8 Details)**

1. Three `type="text" inputMode="numeric"` boxes, each with a visible label, inside `<fieldset>` + `<legend>`. Accept leading zeros, spaces, and month names ("jan", "january"), as GOV.UK does after its research. Don't use `type="number"`.
2. `autoComplete="off"` on all three, never `bday-*`. This is someone else's birthday, and the browser would fill in the manager's own (form-validation.md). SC 1.3.5 does not apply.
3. Check on Save with form-validation.md's rules. Build `YYYY-MM-DD` only when all three parts form a real date.
4. Archived participant (read-only): show the date as text, "27 March 1985", not as disabled inputs.

### States

**Daily report**

| State | Date field | Previous day / Next day | Status line | Downloads |
|---|---|---|---|---|
| **Default** | URL date. 2px border ≥ 3:1. Text ≥ 16px. | Secondary-button links, both available (or Next unavailable on today) | Date in words | Per `has-notes` |
| **Hover** (`@media (hover: hover)` only) | Native | Underline thickens to 3px. Unavailable: no change | — | primary-actions.md |
| **Focus** | 3px `outline` at ≥ 3:1, 2px offset, never only `box-shadow` | Same outline, also on the unavailable Next day | — | — |
| **Active** | Native picker opens (phone); segment or calendar active (laptop) | Pressed style from primary-actions.md | — | — |
| **Disabled / unavailable** | Never disabled | **Next day on today:** same `<a>`, no `href`, `role="link"`, `aria-disabled="true"`, `tabIndex={0}`. Unavailable style from primary-actions.md: neutral fill, label still ≥ 4.5:1, **dashed border** as the non-colour cue. Enter or click does nothing. | — | — |
| **Error** (Enter or blur with an empty, incomplete or future value) | Message above the input, 4px error bar on the group, 3px error border, `aria-invalid="true"`. Cleared the moment the value becomes valid (form-validation.md "reward early"). | Still work. They step from the **URL date** and clear the error. | Empty (hidden error copy for screen readers) | Unavailable |
| **Loading** (`has-notes` in flight) | Unchanged | Unchanged | Date in words, no spinner (primary-actions.md: "usually well under a second") | Unavailable, no message |
| **Empty** (no submitted notes) | Unchanged | Unchanged | "No submitted notes for Thursday 1 October 2026." | Unavailable, `aria-describedby` → status line |
| **Check failed** (network, 5xx) | Unchanged | Unchanged | Date in words | **Available.** The download reports its own error, including `404 report.no_notes` |
| **Read-only** | Not applicable: the screen is manager-only, and workers get the app's not-found screen (app-shell-nav.md) | | | |

**Write past-day note:** default (empty, no error) · focus (as above) · error ("Enter the note date" / "Note date must be in the past", focus on the field) · archived participant (no field; banner) · no loading state (Continue only navigates; the note route shows its own loading).

**Export dates:** default (pre-filled) · error (form-validation.md) · "No submitted notes for Jane Citizen between … and …" is a status, not an error (form-validation.md).

**Date of birth:** default (empty for a new participant, filled when editing) · error (only the parts in error get `aria-invalid`; the message sits under the legend) · read-only (text).

### Phone vs laptop

- **Phone.** The field takes the full width. Previous day and Next day share the next row, half width each, at least 48px tall. Tapping the field opens the phone's own picker: a calendar popover on iOS, which **ignores `max`** (hence the client check and the error state), and a calendar dialog on Android, which greys out dates after `max`. The field's text is at least 16px so iOS doesn't zoom. Set an explicit `min-block-size` on the field. iOS is reported to collapse an empty date input and to centre its value (**unverified**: check on a device in M4; the CSS below is harmless either way).
- **Laptop.** Same DOM order. With room, the field and the two links sit on one row (flex-wrap does this; there are no breakpoints to maintain). The date can be typed (segments: day, month and year in the browser's locale order) or chosen from the browser's calendar button. Typed dates can exceed `max`, so the same checks apply.
- **200% text and 320px wide.** Everything wraps. At large sizes Previous day and Next day stack, still in DOM order. No fixed heights, no `white-space: nowrap`, no horizontal scrolling (SC 1.4.4, 1.4.10).

### Exact copy

Strings marked *design* are verbatim from design.md. *Proposed* strings follow the GOV.UK date templates ([Convention]) unless marked [Opinion].

| Where | Text | Source |
|---|---|---|
| Report `<h1>` | Daily report | *Proposed*; matches the page title "Grow2Notes – Daily report" in app-shell-nav.md |
| Report field label | Date | *Proposed* |
| Step links | Previous day · Next day (a ‹ or › glyph beside each, `aria-hidden="true"`, so the names are exactly the visible words) | *design* 4.7 |
| Status line (has notes, or checking) | Thursday 1 October 2026 | *Proposed* [Opinion]: the date in words, nothing more |
| Status line (no notes) | No submitted notes for Thursday 1 October 2026. | *design* 4.7 |
| Report field error: empty or incomplete | Enter a date | *Proposed* (GOV.UK "Enter [whatever it is]") |
| Report field error: after today | Date must be today or in the past | *Proposed* (GOV.UK template) |
| Downloads | Download Word · Download PDF | *design* 4.7 |
| Past-day page `<h1>` | Write past-day note | *design* 4.8 (the action's name) |
| Past-day caption | Jane Citizen (participant's full name) | design 3.9: the full name prevents notes on the wrong person |
| Past-day label | Note date | *Proposed* (form-validation.md) |
| Past-day button | Continue | *Proposed* (GOV.UK's button for moving to the next step) |
| Past-day errors | Enter the note date · Note date must be in the past | *Proposed* in form-validation.md, kept identical |
| Export labels | From date · To date | *design* 4.12 |
| Export errors | Enter the From date · Enter the To date · To date must be the same as or after Thursday 1 October 2026 | form-validation.md (date written long, from the From value) |
| Date of birth legend / hint | Date of birth · For example, 27 3 1985 | *design* A5 / *Proposed* (GOV.UK hint pattern) |
| Date of birth part labels | Day · Month · Year | *Proposed* (GOV.UK) |
| Date of birth errors | Enter the date of birth · Date of birth must include a day / month / year · Year must include 4 numbers · Date of birth must be a real date · Date of birth must be in the past | form-validation.md, plus GOV.UK's "Year must include 4 numbers" |
| Long date everywhere | Thursday 1 October 2026 | *design* 4.3, 4.7 |
| Short date in banners | Mon 28 Sep 2026 | *design* 4.3 |

Never write a date as numbers only (01/10/2026) in app text. The reader's device may read it as 10 January.

### Accessibility

**Semantics.** `<label for>` + `<input type="date">`; `<a>` for Previous day and Next day; `<p role="status">` for the status line; `<fieldset>`/`<legend>` for date of birth. ARIA is used only for: `aria-disabled` + `role="link"` on the unavailable Next day; `aria-invalid` and `aria-describedby` (error, then the status line) on a field in error; `aria-describedby` from the downloads to the status line (primary-actions.md). No `<nav>` landmark around two links and a field: the app shell's "Main" nav is the only nav [Opinion]. No `rel="prev"`/`"next"`: it does nothing in a single-page app.

**Keyboard.** Tab order = visual order at every width: field → Previous day → Next day → Download Word → Download PDF. The field uses the browser's own keys (typing digits, arrow keys per segment, the browser's key for its calendar). Enter in the report field commits. Enter on a link follows it; Enter on the unavailable Next day does nothing. On the past-day page, Enter in the field submits the form (it has a submit button). No custom shortcuts.

**Screen-reader announcements (expected; the native field's own wording varies by browser and screen reader):**
- Tab to the report field: "Date", the control type, the value in the device's format, then the description "Thursday 1 October 2026". The words matter: the 2019 Hassell testing found TalkBack users had to open the calendar to hear the chosen date.
- Activate Previous day: focus stays on the link. The status line then reads "Wednesday 30 September 2026", and on an empty day "No submitted notes for Wednesday 30 September 2026." (some screen readers say only the second).
- Step forward onto today: focus stays on Next day, which now reads as "Next day, link, unavailable" (NVDA) or "dimmed" (VoiceOver).
- Leave the field with a future date: the status line's hidden copy says "Date must be today or in the past". Returning to the field reads the label, "invalid entry", and "Error: Date must be today or in the past".
- Past-day Continue with a problem: focus moves to the field, which reads its label, "invalid", and the error.

**Voice control.** The Previous day and Next day links give voice users ("click Previous day") a way to reach recent days without the picker, which is where native date inputs failed with Dragon in 2019. The past-day and export fields have no such path. That is accepted under Tensions.

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence (DOM order = visual order) · 1.4.1 Use of Color (unavailable = dashed border; error = bar, border width and text) · 1.4.3 Contrast (Minimum) (labels, unavailable label still ≥ 4.5:1) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast (field and link borders, focus ≥ 3:1) · 1.4.12 Text Spacing · 2.1.1 Keyboard · 2.4.3 Focus Order (focus never dropped to `<body>`) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) (48px) · 3.2.2 On Input (no focus move, scroll or new page on change) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 3.3.7 Redundant Entry (typed values are never cleared) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **One `dates.ts` for the whole app.** Calendar dates are `YYYY-MM-DD` strings end to end: from the API, in the URL, in `<input type="date">` values, back to the API. Compare them as strings (`a <= b` is correct for four-digit years). Never `new Date(string)` for display, never local getters, never `valueAsDate`, never ±86,400,000 ms. [Opinion, tested above]
- **No date library.** Four helpers cover every need here. Temporal or `@internationalized/date` would add a dependency to solve problems this app doesn't have.
- **Same `<a>` for both states of a step link** (sketch below). Swapping `<Link>` for a `<span>`, `<button>` or a different component remounts the node and drops focus. So does removing `href` without keeping `tabIndex={0}`.
- **No `useSuspenseQuery` and no Suspense boundary around the report body.** A suspended render unmounts the links and drops focus. Use `useQuery`.
- **No `placeholderData` / `keepPreviousData` for `has-notes`.** It would show yesterday's "has notes" while today's answer loads, and a fast press would download nothing (`404`).
- **App shell coordination.** The arrival-focus tracker and scroll reset in app-shell-nav.md must ignore navigations whose `location.state?.inScreen` is `true`. The flag holds no personal data (4.0). Invalidating `['me']` on each `location.key` (app-shell-nav.md) still happens, which keeps `today` fresh.
- **`noValidate` on the past-day and export forms; no `required`; don't style `:invalid`** (form-validation.md). `max` stays on the inputs for the pickers, but with `noValidate` the browser never shows its own bubble.
- **iOS styling of the date input.** `font-size: max(1rem, 16px)`, explicit `min-block-size` and `inline-size: 100%`, `text-align: start` on `::-webkit-date-and-time-value` (**unverified** need; harmless). Keep `appearance` as it is: removing it hides the calendar icon on desktop.
- **Forced colours.** The unavailable dashed border and the error borders are real borders, so they survive. Give the unavailable link `color: GrayText`.

```ts
// src/lib/dates.ts — calendar dates are 'YYYY-MM-DD'. "Today" is me.today from /api/auth/me (server, Melbourne, §3.3).
export type IsoDate = string;
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const split = (s: IsoDate) => s.split('-').map(Number) as [number, number, number];
// setUTCFullYear avoids Date.UTC's 0–99 → 19xx quirk; UTC has no daylight saving.
const utc = (y: number, m: number, d: number) => { const t = new Date(0); t.setUTCFullYear(y, m - 1, d); return t; };

export function isIsoDate(s?: string | null): s is IsoDate {
  if (!s || !/^\d{4}-\d{2}-\d{2}$/.test(s)) return false;
  const [y, m, d] = split(s); const t = utc(y, m, d);
  return t.getUTCFullYear() === y && t.getUTCMonth() === m - 1 && t.getUTCDate() === d;
}
export const addDays = (s: IsoDate, n: number): IsoDate => { const [y, m, d] = split(s); return utc(y, m, d + n).toISOString().slice(0, 10); };
/** "Thursday 1 October 2026" */
export const formatLong = (s: IsoDate) => { const [y, m, d] = split(s); return `${DAYS[utc(y, m, d).getUTCDay()]} ${d} ${MONTHS[m - 1]} ${y}`; };
/** "Mon 28 Sep 2026" */
export const formatShort = (s: IsoDate) => { const [y, m, d] = split(s); return `${DAYS[utc(y, m, d).getUTCDay()].slice(0, 3)} ${d} ${MONTHS[m - 1].slice(0, 3)} ${y}`; };
// Checked in Node 24, TZ=Australia/Melbourne: addDays('2026-10-03', 1) = '2026-10-04', addDays('2026-04-05', 1) = '2026-04-06',
// addDays('2028-02-28', 1) = '2028-02-29', isIsoDate('2026-02-29') = false.
```

```tsx
// DailyReport.tsx — route /reports/daily/:date (sketch)
const IN_SCREEN = { inScreen: true } as const;           // app shell: no scroll reset, no arrival focus

export function DailyReportScreen() {
  const { date } = useParams();
  const { today } = useMe();                             // server's Melbourne date
  if (!date) return <Navigate to={`/reports/daily/${today}`} replace />;
  if (!isIsoDate(date)) return <NotFound />;
  if (date > today) return <Navigate to={`/reports/daily/${today}`} replace />;
  return <DailyReport date={date} today={today} />;      // never key={date}: the DOM must persist
}

function DailyReport({ date, today }: { date: IsoDate; today: IsoDate }) {
  const navigate = useNavigate();
  const [draft, setDraft] = useState(date);
  const [error, setError] = useState<string | null>(null);
  const [shownFor, setShownFor] = useState(date);
  if (shownFor !== date) { setShownFor(date); setDraft(date); setError(null); }   // Prev/Next, Back, Forward
  const pushedThisVisit = useRef(false);
  const timer = useRef<number | undefined>(undefined);
  const check = useQuery({ ...hasNotesQuery(date), refetchOnWindowFocus: true });  // no placeholderData

  function commit(value: string, explicit: boolean) {
    window.clearTimeout(timer.current);
    const problem = !isIsoDate(value) ? 'Enter a date' : value > today ? 'Date must be today or in the past' : null;
    if (problem) { if (explicit) setError(problem); return; }
    if (!explicit && Number(value.slice(0, 4)) < 1900) return;     // half-typed year: wait
    setError(null);
    if (value === date) return;
    navigate(`/reports/daily/${value}`, { replace: pushedThisVisit.current, preventScrollReset: true, state: IN_SCREEN });
    pushedThisVisit.current = true;
  }

  const settled = draft === date && !error;
  const status = !settled ? '' : check.data?.hasNotes === false ? `No submitted notes for ${formatLong(date)}.` : formatLong(date);
  const canDownload = settled && (check.data?.hasNotes === true || check.isError);

  return (
    <>
      <PageHeading>Daily report</PageHeading>
      <div className={s.group}>
        <div className={s.field} data-invalid={error ? '' : undefined}>
          <label htmlFor="report-date" className={s.label}>Date</label>
          {error && <p id="report-date-error" className={s.message}><span className="visually-hidden">Error: </span>{error}</p>}
          <input id="report-date" type="date" className={s.input} max={today} value={draft}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? 'report-date-error report-date-status' : 'report-date-status'}
            onFocus={() => { pushedThisVisit.current = false; }}
            onChange={(e) => {
              const v = e.currentTarget.value;
              setDraft(v);
              if (error && isIsoDate(v) && v <= today) setError(null);       // clear the moment it's fixed
              window.clearTimeout(timer.current);
              timer.current = window.setTimeout(() => commit(v, false), 750);
            }}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); commit(e.currentTarget.value, true); } }}
            onBlur={(e) => commit(e.currentTarget.value, true)} />
        </div>
        <div className={s.steps}>
          <StepLink to={addDays(date, -1)}><span aria-hidden="true">‹ </span>Previous day</StepLink>
          <StepLink to={date < today ? addDays(date, 1) : null}>Next day<span aria-hidden="true"> ›</span></StepLink>
        </div>
      </div>
      <p id="report-date-status" role="status" className={s.status}>
        {status}{error && <span className="visually-hidden">{error}</span>}
      </p>
      <ReportDownloads date={date} available={canDownload} describedBy="report-date-status" />  {/* primary-actions.md */}
    </>
  );
}

/** One <a> in both states, so React keeps the same DOM node and focus survives reaching today. */
function StepLink({ to, children }: { to: IsoDate | null; children: React.ReactNode }) {
  const path = `/reports/daily/${to ?? ''}`;
  const href = useHref(path);
  const follow = useLinkClickHandler(path, { preventScrollReset: true, state: IN_SCREEN });
  return (
    <a className={to ? s.step : `${s.step} ${btn.unavailable}`} href={to ? href : undefined} onClick={to ? follow : undefined}
       role={to ? undefined : 'link'} aria-disabled={to ? undefined : true} tabIndex={to ? undefined : 0}>
      {children}
    </a>
  );
}
```

```css
/* DailyReport.module.css — tokens from the app theme; .step composes the shared secondary-button styles */
.group  { display: flex; flex-wrap: wrap; align-items: flex-end; gap: var(--space-3); }
.field  { flex: 1 1 14rem; }
.field[data-invalid] { border-inline-start: 4px solid var(--colour-error); padding-inline-start: var(--space-3); }
.label  { display: block; font-weight: 700; margin-block-end: var(--space-2); }
.message { margin: 0 0 var(--space-2); color: var(--colour-error-text); font-weight: 700; }
.input  { font: inherit; font-size: max(1rem, 16px); box-sizing: border-box; inline-size: 100%; min-block-size: 3rem;
          padding-inline: var(--space-3); border: 2px solid var(--colour-input-border); border-radius: 4px;
          background: var(--colour-surface); color: var(--colour-text); }
.input::-webkit-date-and-time-value { text-align: start; }            /* iOS centres the value (unverified) */
.input:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }
.input[aria-invalid="true"] { border: 3px solid var(--colour-error); }
.steps  { flex: 1 1 18rem; display: flex; flex-wrap: wrap; gap: var(--space-3); }
.step   { composes: secondary from '../ui/Button.module.css'; flex: 1 1 8rem; min-block-size: 3rem; }
/* Unavailable look (dashed border, label ≥ 4.5:1) comes from btn.unavailable, added in JSX: CSS Modules
   only allow `composes` in a single-class selector, so it can't hang off [aria-disabled]. */
.status { min-block-size: 1.5em; margin-block: var(--space-3) var(--space-4); }
@media (forced-colors: active) { .step[aria-disabled="true"] { color: GrayText; border-color: GrayText; } }
```

```tsx
// WritePastDayNote.tsx — one-field form (sketch). DateField is the shared label/hint/error wrapper from form-validation.md.
function WritePastDayNote({ participant }: { participant: { id: string; givenName: string; familyName: string } }) {
  const { today } = useMe();
  const navigate = useNavigate();
  const [value, setValue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const field = useRef<HTMLInputElement>(null);
  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const problem = !isIsoDate(value) ? 'Enter the note date' : value >= today ? 'Note date must be in the past' : null;
    setError(problem);
    if (problem) { field.current?.focus(); return; }
    navigate(`/participants/${participant.id}/notes/${value}`);      // that route decides: open the note, or a new past-day form
  }
  return (
    <form noValidate onSubmit={onSubmit}>
      <p className={s.caption}>{participant.givenName} {participant.familyName}</p>
      <PageHeading>Write past-day note</PageHeading>
      <DateField ref={field} id="past-day-date" label="Note date" max={addDays(today, -1)} value={value} error={error}
        onChange={(v) => { setValue(v); if (error && isIsoDate(v) && v < today) setError(null); }} />
      <button type="submit" className={btn.primary}>Continue</button>
    </form>
  );
}
```

---

## Per-screen notes

**4.7 Daily report.**
- Opens on `me.today` through the nav link. A future or missing URL date is replaced by today. Any other bad value shows not-found.
- The status line is the **only** place the chosen date appears in words. Don't add a second copy in the heading.
- Back after Previous day → the day you were on. Back after a calendar pick → the date shown before the pick. Back from the first report date → the screen you came from.
- iPhone managers can pick a future date (WebKit 225639). They get "Date must be today or in the past" when the picker closes, and Previous day still works from the last good date.
- The downloaded file name already carries the date (`daily-notes_2026-10-01.pdf`, §11.5). Nothing else is needed to confirm which day was downloaded.

**4.8 Participant detail and 4.4 Participant notes: Write past-day note.**
- Both pages link to the same past-day page. Neither shows the field inline (primary-actions.md lists "Write past-day note" as a link).
- Don't pre-fill yesterday. A pre-filled date can be accepted without being read, and this action writes a record. The picker opens on the current month anyway, a tap or two from any recent day. [Opinion]
- After Continue, the 4.3 banner ("Past-day note for Mon 28 Sep 2026, written on 1 Oct."), the header date and the confirmation ("Submit the note for" + name + date, confirm-dialog.md) all show the chosen date in words. A slip on the picker is caught three times before anything is submitted.
- Back from the note form returns to an empty past-day page. The manager is going back to pick a different date, so nothing needs remembering.

**4.8 Participant detail: Details (date of birth).**
- Three boxes, GOV.UK pattern, `autoComplete="off"`. Day and month boxes are about 2 characters wide and the year about 4, set in `ch` so they grow with the text.
- Only the parts in error get `aria-invalid`. The error summary links to the first part in error (form-validation.md).

**4.12 Participant record export.**
- Two native fields, pre-filled, `max={me.today}`. Nothing is checked until Export.
- The "To date must be the same as or after …" message writes the From date long ("Thursday 1 October 2026"), from `formatLong`.
- The participant on this page is fixed text, not an input (design: "Participant (fixed)").

**Everywhere dates are shown.** Today's header, the note form header and banners, read views, version history and the flagged list format note dates with `formatLong` / `formatShort` (add a `formatDayMonth` for "Thursday 1 October" on Today). Times stay on the shared Melbourne time formatter in status-messages.md.

---

## Anti-patterns to avoid

- Reading "today" from the device: `new Date()`, `Date.now()`, `toISOString()` on a local date (gives 30 September for 1 October in Melbourne), local getters, or `valueAsDate`.
- Stepping days by adding 86,400,000 ms (wrong on the day daylight saving ends).
- Trusting `max` to stop future dates. iOS ignores it and desktop typing gets past it.
- Acting on every `input`/`change` event of the report field: half-typed years in the URL, in history and read aloud.
- Moving focus to the `<h1>`, or scrolling to the top, when the report date changes (SC 3.2.2). Keying the report route by date, or a Suspense fallback, which remount it and drop focus.
- Rendering Next day as a different element when it becomes unavailable, or removing `href` without keeping it focusable (focus falls to `<body>`). Using the `disabled` attribute.
- Hiding Next day on today. That conflicts with design 4.7, and the controls would shift under the manager's finger.
- `placeholderData` / `keepPreviousData` on the has-notes check; a download handler holding a stale date.
- A home-made calendar widget or a third-party date picker; a calendar of the month that marks which days have notes (D25 rules out "missing" markers; D26 rules out counts and statistics).
- Extras nobody asked for: a "Today" button, keyboard shortcuts, week views, remembered last-used dates, a date range on the daily report (D27).
- Numeric-only dates in app text ("01/10/2026"); a placeholder such as "dd/mm/yyyy" used as the only label.
- Silently changing a picked date (AgDS clamps to the nearest valid date). Grow2Notes says what is wrong instead, especially on the past-day field, where a silent change would write a note for the wrong day.
- Pre-filling the past-day date.
- `type="number"`, `<select>` dropdowns, `maxLength` or `bday-*` autocomplete on the date-of-birth boxes.
- Putting names in the URL, the router state or the page title. Only IDs, dates and the `inScreen` flag go there (4.0).

---

## Tensions with decisions

1. **A native date field on the Daily report** (design 4.7, also used for the past-day and export dates). GOV.UK and NHS avoid `type="date"` altogether [Convention]. Hassell's 2019 testing found Dragon could not use it and VoiceOver on iOS didn't read its errors [Research, dated]. Roselli calls it a "problem for voice users" [Research, practitioner]. iOS still ignores `min`/`max` (WebKit 225639, open) [Research]. On the other side, NN/g and MOJ guidance support a calendar for chosen dates near today [Convention], which is exactly this use. The mitigations above (links for stepping, the date in words, code checks, server checks) cover the known gaps. No change is proposed.
2. **"Next day is disabled on today"** (design 4.7). GOV.UK pagination says "do not show the next page link on the last page" [Convention], and GOV.UK and Roselli both advise against disabled controls (cited in primary-actions.md). Keeping it visible avoids a layout shift and tells the manager plainly that tomorrow isn't available. The implementation keeps it focusable and announced as unavailable. No change is proposed.

---

## Open questions

1. **Export "From" default.** Design 4.12 pre-fills From with "the participant's first note", but no endpoint in §6 returns that date. One of the existing responses would need a `firstNoteDate`, or the default needs restating. Not decided here.
2. **Earliest date.** Neither the report nor the past-day field has a `min`. Dates before go-live are covered by the Word records (A39). The owner may want a lower bound, but the design specifies none, so none is set.
3. **Date of birth control.** Decided here as three text boxes because form-validation.md deferred it. Confirm it fits the participant-detail form spec.
4. **To verify on real devices before M4 closes:** current Chrome/Edge/Firefox/Safari `change` timing while typing; iOS empty-field collapse and centred value; whether iOS sets a value as soon as its picker opens on an empty field (the past-day page would then show "Note date must be in the past" for today, which is still correct); native field announcements with NVDA + Chrome, JAWS + Chrome, VoiceOver on iOS and macOS, and TalkBack; Dragon with the past-day field.
5. **App-shell hook.** app-shell-nav.md's arrival-focus tracker needs the `inScreen` exception described above.

---

## Sources

- GOV.UK Design System, Date input: https://design-system.service.gov.uk/components/date-input/
- GOV.UK Design System, Pagination: https://design-system.service.gov.uk/components/pagination/
- NHS digital service manual, Date input: https://service-manual.nhs.uk/design-system/components/date-input
- Ministry of Justice Design System, Date picker: https://design-patterns.service.justice.gov.uk/components/date-picker
- Australian Government Design System (AgDS), Date picker: https://design-system.agriculture.gov.au/components/date-picker
- Australian Government Style Manual, Dates and time (quoted via status-messages.md; timed out when re-checked): https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- NN/g, "Date-Input Form Fields: UX Design Guidelines" (Li, 2017): https://www.nngroup.com/articles/date-input/
- Baymard Institute, "4 Design Patterns That Violate 'Back' Button UX Expectations" (2020): https://baymard.com/blog/back-button-expectations
- Hassell Inclusion, "Is input type="date" ready for use in accessible websites?" (Armfield, 2019): https://hassellinclusion.com/blog/input-type-date-ready-for-use/
- Adrian Roselli, "Maybe You Don't Need a Date Picker" (2019, with later updates): https://adrianroselli.com/2019/07/maybe-you-dont-need-a-date-picker.html
- Scott O'Hara, "Disabling a link" (2021): https://www.scottohara.me/blog/2021/05/28/disabled-links.html
- WebKit bug 225639, "[iOS] HTML datepicker's min-max attribute not working": https://bugs.webkit.org/show_bug.cgi?id=225639
- Jeremy Keith, "Pickin' dates on iOS" (April 2024): https://adactio.com/journal/21050
- mdn/browser-compat-data issue 26656 (iOS min/max): https://github.com/mdn/browser-compat-data/issues/26656
- w3c/html issue 194, date input `change` timing (2016): https://github.com/w3c/html/issues/194
- MDN, `<input type="date">`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- HTML Standard, User interaction (focus fixup): https://html.spec.whatwg.org/multipage/interaction.html
- WCAG 2.2 Understanding: 3.2.2 On Input https://www.w3.org/WAI/WCAG22/Understanding/on-input.html · 4.1.3 Status Messages https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · 2.5.3 Label in Name https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html · 2.5.8 Target Size (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- React, "Preserving and Resetting State": https://react.dev/learn/preserving-and-resetting-state
- React Router, `useLinkClickHandler`: https://reactrouter.com/api/hooks/useLinkClickHandler
- TanStack Query 5, Placeholder query data: https://tanstack.com/query/v5/docs/framework/react/guides/placeholder-query-data
- React Aria, DatePicker: https://react-aria.adobe.com/DatePicker
- Stefan Judis, "Mobile Safari doesn't zoom into form inputs with minimum 16px": https://www.stefanjudis.com/notes/mobile-safari-doesnt-zoom-into-form-inputs-with-minimum-16px/
- Grow2Notes component specs this file stays consistent with: form-validation.md, primary-actions.md, app-shell-nav.md, status-messages.md, confirm-dialog.md
