# Participant details inputs

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The participant Details error summary sits above the `<h1>` (first in `<main>`), not under the name (participants.md B4).

Component key: `text-and-date-inputs`. This covers the three inputs in the **Details** section of participant detail
(design.md 4.8): **Given name**, **Family name** and **Date of birth** (A5), on both the "Add participant" and the
edit path, plus their read-only form on an archived participant. It also covers the one other date field on the same
screen, the "Write past-day note" date, because it is a different kind of date and needs a different control. Error
display and the error summary are owned by `form-validation.md`. The "Saved 4:12 pm" status is owned by
`status-messages.md`. The Save button's look and busy state are owned by `primary-actions.md`. This file follows all
three and does not restate them. It adds no screen, setting, notification or data item.

Evidence grades: **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA,
HTML spec · **[Convention]** established design systems · **[Opinion]** reasoned judgement with no direct evidence.

Copy conventions: text in "quotes" with no mark is word for word from design.md. Text marked **(proposed)** fills a gap
the design leaves and needs the owner's sign-off. Names in examples are made up.

---

## Where it's used

| Screen (design.md) | What the inputs do | What differs |
|---|---|---|
| **4.8 Participant detail, Details section** (existing participant) | Given name, family name and date of birth, filled with the current values, then **Save** (`PUT /api/admin/participants/{id}` with `If-Match`). | Managers only. A multi-field form on a page that also holds the Goals list and Actions, so it is one of several independent forms. A stale save returns `412`. Date of birth is "shown to managers only" (§5.3). |
| **4.8 Add participant** (from "Add participant" on the list screen) | The same three inputs, empty, then **Save** (`POST /api/admin/participants`). | Nothing exists yet, so there is no Goals section and no Actions until the first Save succeeds. After that, the page is the new participant's detail page. M1 "done when" includes setting up a participant with five goals on a phone without help (§14). |
| **4.8 Archived participant** | The same three values, shown as text. | "A read-only banner with Restore" (4.8). No inputs, so nothing to validate. |
| **4.8 Write past-day note** | One date: the day the forgotten note is for. "A date field allowing past dates only." | A date the manager **chooses** from the recent past, not a date anyone **remembers**. This needs a different control (see Best practice). It is a one-field form. If the date already has a note, that note opens. Otherwise the note form (4.3) opens with the past-day banner. |
| **4.8 Participants list** | Not used. The list's "Find a participant" box is `search-filter.md`. | — |
| **Duplicate warning** (same name and date of birth) | **Not specified in design.md, so not designed here.** 4.8 lists no such state, `POST /api/admin/participants` has no duplicate response (§6.6), and §6.9 has no such error code. See Open questions. | — |

Workers never see these inputs. `GET /api/participants/{id}` leaves out the date of birth for everyone (§6.3).

---

## Best practice

### Names

- **[Convention]** W3C Internationalization says to ask first whether separate given-name and family-name fields are
  really needed, because one full-name field fits the most name shapes. It also advises against the labels "first name"
  and "last name". https://www.w3.org/International/questions/qa-personal-names
- **[Convention]** GOV.UK: "Use single or multiple fields depending on your user's needs." For users from outside the
  UK it gives the labels "Given names" and "Family name". https://design-system.service.gov.uk/patterns/names/
  Grow2Notes needs two fields: every list is sorted by family name, then given name (A5), and the Today search matches
  either name (4.2).
- **[Convention]** W3C: allow hyphens, apostrophes, spaces and accented letters, and "Don't normalize the casing in
  names" (McNamara is its example). GOV.UK: "Support all the characters users may need to enter." (Same two URLs.)
- **[Convention]** W3C: "Make input fields long enough to enter long names", and leave room to show the whole name later.
  GOV.UK: "Fields must be long enough to accommodate the names of your users." (Same URLs.)
- **[Convention]** GOV.UK sets `spellcheck="false"` on name fields so names are not marked as misspelt. (Names URL.)
- **[Standard]** `autocorrect="off"` turns off automatic correction of spelling. MDN marks it Baseline 2026, available
  across current browsers since September 2026. https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocorrect
- **[Standard]** `autocapitalize` affects on-screen keyboards and voice input only, not physical keyboards. Chrome and
  Safari default to `sentences`. `words` capitalises the first letter of each word. MDN marks it "Limited availability"
  (not Baseline). https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocapitalize

### A remembered date (date of birth)

- **[Convention]** GOV.UK: "Ask for memorable dates, like dates of birth, using the Date input component", meaning three
  labelled text inputs for Day, Month and Year. Use a calendar control only for a date "in the near future or recent
  past", when the day of the week matters, or when dates need to be seen next to each other.
  https://design-system.service.gov.uk/patterns/dates/ · https://design-system.service.gov.uk/components/date-input/
- **[Convention]** NHS and the NSW Design System use the same three-field pattern for dates people already know. NSW
  says to order the fields "according to the regional format familiar to your users". Day, Month, Year is the Australian
  order. https://service-manual.nhs.uk/design-system/components/date-input · https://designsystem.nsw.gov.au/components/date-input/index.html
- **[Research]** NN/g (Angie Li, 2017): typing is often the most efficient way to enter a date, "especially when the
  date is further away in the past". Label and separate the day, month and year fields. Dropdown selects add clicks and
  scrolling. This is a guidelines article: the sample is not stated. https://www.nngroup.com/articles/date-input/
- **[Research]** GOV.UK's date input page reports that many users of the Apply for teacher training service typed month
  names ("jan", "january") and got errors. Accepting month names "dramatically" reduced errors, so GOV.UK says to accept
  full and abbreviated month names. Its dates pattern also says more research is needed. (Date input and dates URLs.)
- **[Convention]** Hint example: "For example, 31 3 1980" (GOV.UK), "For example, 15 3 1984" (NHS). GOV.UK: use a day of
  13 or more and a month of 9 or less, which shows the order and that leading zeros are not needed. NSW instead shows
  "07 11 2022", with leading zeros. (Dates pattern URL; NSW URL.)
- **[Convention]** GOV.UK: "Never automatically tab users between the fields of the date input." NSW says the same:
  auto-advance makes it hard for keyboard users to correct mistakes. (Date input URL; NSW URL.)
- **[Convention]** Inputs are `type="text" inputmode="numeric"`, never `type="number"`. Widths: Day and Month
  `govuk-input--width-2` (max-width 2.75em), Year `govuk-input--width-4` (4.5em). govuk-frontend sets no `pattern`
  by default. https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/date-input/template.njk
  · https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/input/_mixin.scss
- **[Convention]** Errors, from GOV.UK, in this order: nothing entered "Enter [it]"; incomplete "[It] must include a
  [missing part]" (examples include "Year must include 4 numbers"); impossible "[It] must be a real date"; future when
  past is needed "[It] must be in the past". Highlight the whole date when the error is about the whole date, and only the
  parts in error when it is about a part. (Date input URL.)
- **[Convention, with tester evidence]** govuk-frontend puts the hint and error IDs in `aria-describedby` **on the
  fieldset** and overrides its role to `group`. The template's comment says JAWS otherwise does not announce the
  description. When the attribute was challenged as redundant (issue #1590, 2019), Hanna Laakso's testing found no
  meaningful difference in later JAWS versions or in NVDA, and the role was kept.
  https://github.com/alphagov/govuk-frontend/issues/1590
- **[Standard]** `<fieldset>` and `<legend>` give a group of controls a name. https://www.w3.org/WAI/tutorials/forms/grouping/
- **[Research]** Hassell Inclusion (Graham Armfield, February 2019) tested `<input type="date">` across browsers, NVDA,
  JAWS, VoiceOver, TalkBack and Dragon. Dragon could not use it at all, some errors went unannounced, and Safari on macOS
  had no support. The verdict was not ready for accessible sites. This is seven years old. Safari on macOS has supported
  it since version 14.1, and current Dragon behaviour is **unverified**.
  https://hassellinclusion.com/blog/input-type-date-ready-for-use/ · a11ysupport.io rates the basic test as partially
  supported: https://a11ysupport.io/tech/html/input(type-date)_element
- **[Standard]** For `<input type="date">`, "the displayed date is formatted based on the locale of the user's browser",
  while `value` is always `yyyy-mm-dd`, or empty when incomplete.
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date

### A chosen recent date (Write past-day note)

- **[Convention]** GOV.UK limits calendar controls to dates "in the near future or recent past", or when people need the
  day of the week. A forgotten note is from the recent past, and a manager often thinks in weekdays ("Tuesday's note").
  (Dates pattern URL.)
- **[Research]** NN/g: calendar pickers suit dates close to the present, within about a year. (NN/g URL.)
- **[Standard]** `max` stops the picker offering later dates, but with `novalidate` the app must check the range itself.
  An incomplete date gives `value === ""`. (MDN date URL.)

### Data about someone else: autocomplete

- **[Standard]** WCAG 1.3.5 Identify Input Purpose "is specifically scoped to inputs collecting information about the
  user". A participant's name and birthday are about someone else, so the `given-name`, `family-name` and `bday-*` tokens
  are not required. https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html GOV.UK and NHS recommend the
  `bday-*` tokens for the **user's own** date of birth, citing 1.3.5. (Date input URLs.)
- **[Standard]** MDN: `autocomplete="off"` tells the browser not to save what was typed for later autocompletion, and stops
  it caching form data in session history.
  https://developer.mozilla.org/en-US/docs/Web/Security/Practical_implementation_guides/Turning_off_form_autocompletion
  That matches D21 and D22: nothing about a participant stays on a manager's personal phone.
- **[Convention, practitioner]** Chrome has long ignored `autocomplete="off"` for its own Autofill (addresses and names).
  The Chrome team announced this in November 2014
  (https://lists.w3.org/Archives/Public/public-whatwg-archive/2014Nov/0092.html), and Adam Silver reported it still
  happening on name and address fields in 2021 (https://adamsilver.io/blog/stopping-chrome-from-ignoring-autocomplete-off/).
  His workaround is a made-up `autocomplete` value. Chrome's current behaviour on these fields is **unverified**.
- **[Standard]** A form control with no `name` attribute is left out when a form is submitted natively. A `<form>` with no
  `method` submits with GET, which puts the values in the URL. https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set

### Widths and layout

- **[Research]** Baymard (2010, checkout usability testing, sample not stated): when a field was too long or too short,
  test participants wondered whether they had misread the label. Fixed-length fields such as a birth year should match
  the input's length, and variable fields such as names should share one sensible default width.
  https://baymard.com/blog/form-field-usability-matching-user-expectations
- **[Convention]** GOV.UK: use fixed-width inputs for content of a known length. Its name-sized width is
  `govuk-input--width-20` (20.5em). (Text input and mixin URLs.)
  https://design-system.service.gov.uk/components/text-input/
- **[Research]** Baymard (2023, checkout testing): avoid multi-column form layouts. Two or three inputs on one line did not
  cause problems "when they logically belonged to the same single entity", such as a card expiry date.
  https://baymard.com/blog/avoid-multi-column-forms So the three date parts on one line are fine, and the two name fields
  stay stacked.
- **[Convention]** GOV.UK: labels sit above the input, in sentence case, with no colon. Never use a placeholder instead of
  a label, hint or example. (Text input URL.)
- **[Convention, unverified mechanism]** iOS Safari zooms the page when it focuses an input whose text is smaller than
  16 px. Apple does not document this. It is widely reported, for example
  https://github.com/heroui-inc/heroui/issues/5326. The fix is input text of at least 16 px, never `user-scalable=no`,
  which breaks WCAG 1.4.4.
- **[Standard]** 1.4.11 Non-text Contrast: an input's border must reach 3:1 where it is what shows the field is there.
  https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html · 1.4.10 Reflow: no sideways scrolling at 320 CSS px.
  https://www.w3.org/WAI/WCAG22/Understanding/reflow.html

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **Two name fields, labelled "Given name" and "Family name",** in that order, stacked on every screen size. These are
   design.md's words (A5, 4.2) and the international labels from W3C and GOV.UK. [Convention]
2. **Date of birth is three text inputs: Day, Month, Year,** in a `<fieldset>` with the legend "Date of birth". Not
   `<input type="date">`, not dropdowns, not a calendar. [Convention + Research]
3. **Write past-day note is one native `<input type="date">`** with `max` set to yesterday in Melbourne. This is the
   same control the Report screen uses (4.7, "a native date field"). [Convention + Opinion]
4. **No autofill, no device memory:** `autoComplete="off"` on every input here, and **no `name` attributes.** If anything
   ever submits natively, nothing is sent and nothing lands in a URL. [Standard + Opinion]
5. **Accept what people type.** Trim spaces. Accept leading zeros and month names. Never change the case of a name, never
   reject a character, and never truncate. [Convention]
6. **Validate on Save only,** using `form-validation.md`'s summary and inline pattern. Use GOV.UK's date messages.
7. **Read-only means text, not disabled inputs.** [Convention]

### Anatomy

```
Details                                              <h2>
                                                     (error summary goes at the top of <main>: form-validation.md)
Given name                                           <label for="details-givenName">
+--------------------------------+                   <input type="text">, max 20.5em wide, at least 48 px tall
| Jane                           |
+--------------------------------+

Family name                                          <label for="details-familyName">
+--------------------------------+
| Citizen                        |
+--------------------------------+

Date of birth                                        <fieldset role="group"> <legend>
For example, 27 3 1987                               hint <p id="details-dateOfBirth-hint">
[Date of birth must include a month]                 error <p id="details-dateOfBirth-error"> (only when in error)
Day     Month   Year                                 three <label>s above three inputs
+----+  +----+  +-------+                            Day and Month 2.75em (at least 44 px), Year 4.5em
| 7  |  |    |  | 1987  |                            on one line, wrapping when text is enlarged
+----+  +----+  +-------+

[ Save ]  Saved 4:12 pm                              primary button; <p role="status"> (status-messages.md)
```

**Archived (read-only):**

```
Details                         <h2>
Given name      Jane            <dl>: <dt> label, <dd> value
Family name     Citizen
Date of birth   7 March 1987
```

**Write past-day note (one-field form):**

```
Note date                       <label for="pastday-noteDate">
A date before today             hint (proposed)
+------------------+
| 28/09/2026    [] |            <input type="date" max="{yesterday}">, browser's own picker
+------------------+
[ Continue ]                    (proposed)
```

### Field specification

| Input | Element and attributes | Width | Notes |
|---|---|---|---|
| Given name | `<input type="text" id="details-givenName" autoComplete="off" autoCorrect="off" spellCheck={false} autoCapitalize="words">` | 100% of the column, `max-inline-size: 20.5em` | No `maxLength` (it truncates pasted or dictated text silently, see `form-validation.md`). Limit 100 (§5.1), checked on Save. |
| Family name | The same, `id="details-familyName"` | The same | The same. |
| Date of birth group | `<fieldset role="group" aria-describedby="details-dateOfBirth-hint [details-dateOfBirth-error]">` + `<legend>Date of birth</legend>` | — | `role="group"` follows govuk-frontend's JAWS fix. It is the only ARIA role in this component. |
| Day / Month / Year | `<input type="text" inputMode="numeric" id="details-dateOfBirth-day" autoComplete="off" spellCheck={false}>` (and `-month`, `-year`) | Day and Month `2.75em` (min 44 px), Year `4.5em` | No `pattern`, no `maxLength`, no auto-tab, no `type="number"`. A numeric keypad on phones. Month names typed on a laptop are still accepted. |
| Note date (past-day) | `<input type="date" id="pastday-noteDate" max={yesterday} autoComplete="off">` | `max-inline-size: 12em` | `yesterday` comes from `me.today` (server, Melbourne, §6.8), never the device clock. No default value. |

`autoCapitalize="words"` is **[Opinion]**: most names start each part with a capital, and a manager can still type a
lower-case "van" or "de". Whatever is typed is stored exactly, after trimming the ends. Nothing is re-cased.

### Behaviour

**Loading the form.** Render the inputs only once the participant's record has arrived, with the values from it. Copy
them into local state **once**. A background refetch, such as TanStack Query's refetch on window focus, must never
overwrite what the manager is typing. Show the date of birth without leading zeros (`1987-03-07` becomes Day 7, Month 3,
Year 1987), the same way it is typed.

**Typing.** No formatting, masking, auto-advance or live checking. Errors appear only after Save. Once a field shows an
error, it is re-checked on every input and cleared silently when fixed (`form-validation.md` rules 1 and 2).

**Save (client checks, in page order).**

| Field | Check (on trimmed text) | Message | Parts marked invalid |
|---|---|---|---|
| Given name | Not blank | "Enter the given name" | — |
| Given name | 100 characters or fewer | "Given name must be 100 characters or less" | — |
| Family name | Not blank | "Enter the family name" | — |
| Family name | 100 characters or fewer | "Family name must be 100 characters or less" | — |
| Date of birth | Not all three empty | "Enter the date of birth" | Day, Month, Year |
| Date of birth | No part empty | "Date of birth must include a day" (or "a month", "a year", "a day and month", "a day and year", "a month and year") | The empty parts |
| Date of birth | Year is digits but not 4 of them | "Year must include 4 numbers" | Year |
| Date of birth | Day 1 to the last day of that month; month 1 to 12 or a month name; year 4 digits | "Date of birth must be a real date" | The part that is wrong (Day for 31 April, Month for 13) |
| Date of birth | Earlier than `me.today` | "Date of birth must be in the past" | Day, Month, Year |

Only the first failing check per field is shown. The date's message goes under the legend. The summary links to the
first part marked invalid. All of this follows `form-validation.md` (summary, focus, `aria-invalid`, title prefix).

**Month names accepted:** `jan`…`dec`, the full English names, and `sept`, in any case, with an optional final full stop.
Spaces around a value are trimmed and leading zeros are accepted ("07"). A value with a space inside it ("0 7") counts as
not real. A two-digit year is never guessed into a century: "87" gives "Year must include 4 numbers". (The sketch below
was run against these cases, including 29 February in 2023 and 2024.)

**Save (request).** Send `{givenName, familyName, dateOfBirth: "1987-03-07"}`. On an edit, send `If-Match` with the row's
ETag. The Save button follows `primary-actions.md` ("Saving…", never `disabled`). The inputs stay editable and are never
cleared.

| Response | What happens |
|---|---|
| `200` (edit) | "Saved 4:12 pm" next to Save, through the always-rendered `<p role="status">` (`status-messages.md`). Focus stays on Save. The page `<h1>` (the participant's full name) updates. Store the new ETag. Update the participants list query in memory. |
| `201` (add) | Replace the URL with the new participant's detail route (`navigate(…, { replace: true })`, IDs only, design 4.0), so Back returns to the list rather than to an empty form. The Goals section ("Notes will show an empty Goals section.") and Actions appear. Show "Saved 4:12 pm" next to Save and keep focus on Save, so the manager can go straight on to Add goal. |
| `422 validation.failed` | `errors.givenName`, `errors.familyName` and `errors.dateOfBirth` map onto the same fields and messages. `dateOfBirth` marks all three parts. |
| `412 precondition.failed` | Unlinked summary item: "Someone else changed these details while you were editing. Check the details below, then save again." Keep the typed values. Refetch to get the fresh ETag only (`form-validation.md`). |
| Network or `5xx` | `form-validation.md`'s "Not saved: …" messages. Values kept. |

**Write past-day note.** On **Continue (proposed)**: an empty or incomplete value gives "Enter the note date". A value of
`me.today` or later gives "Note date must be in the past". Both are inline, and focus goes to the field (a one-field form,
`form-validation.md`). Otherwise go to the note route for (participant, date). That route opens the existing note, or the
empty form with the past-day banner (4.8, 4.3). Nothing is created until the first change (3.3).

### States

| State | Name inputs | Date of birth | Past-day date |
|---|---|---|---|
| **Default** | Label above, 2 px border at least 3:1 against the background, white or theme input background | Legend, hint, three labelled inputs | Label, hint, native control |
| **Hover** | No change. GOV.UK inputs have no hover style, and a hover change on a text box suggests nothing. [Convention] | Same | Same |
| **Focus** | `outline: 3px solid var(--colour-focus)`, `outline-offset: 0`, always visible, never only a `box-shadow` (which forced colours remove). Not hidden by the sticky top bar (`scroll-margin-top`, WCAG 2.4.11). | Focus is on one part at a time. The others keep their default or error look. | Same as names. The browser draws the picker. |
| **Active** | Not applicable to text inputs | — | The browser's picker is open |
| **Disabled** | **Never used.** Inputs are not disabled while saving. | Same | Same |
| **Error** | `form-validation.md`: message above the input, 4 px bar on the group, 3 px error border on the input, `aria-invalid="true"` | Message under the legend. Only the parts named in the table get `aria-invalid` and the error border. The bar runs down the whole fieldset. | Message above the input, error border, `aria-invalid="true"` |
| **Loading** (record on its way) | Inputs not rendered yet. The page's own loading state shows. Never render empty inputs that fill in later. | Same | Rendered at once. Needs only `me.today`. |
| **Saving** | Still editable. The Save button shows "Saving…". | Same | — |
| **Empty** (Add participant) | Empty, no placeholder | Empty, hint visible | Empty, no default date |
| **Read-only** (archived) | `<dl>` text. Not inputs with `readonly` or `disabled`, which look editable or fail contrast. | "7 March 1987" (proposed format, see Exact copy) | Not shown. Archived participants get no new notes (3.7). |

### Phone vs laptop

- **Phone (most of M1's "on a phone without help" test):** one column. Name inputs fill the column width. The three date
  parts sit on one line (about 13em with gaps) and wrap onto two lines at 200% text or at 320 px with large text, with
  no sideways scrolling (1.4.10). The keypad opens for date parts. The Save button and status sit directly under the
  date, and nothing in this section is sticky.
- **Laptop:** same order and stacking. Name inputs stop at 20.5em so their width hints at a name, not a paragraph
  (Baymard). Date parts keep their fixed widths. The form column is no wider than `--form-max-width`, which
  `form-validation.md` also uses for the summary. Given name and family name are **not** placed side by side, so tab
  order, reading order and the phone layout stay the same. [Opinion: Baymard would allow it, but nothing is gained.]
- **Text size:** input text is `max(1rem, 16px)`, so iOS does not zoom on focus. Every width is in `em`, so inputs grow
  with the user's text size (1.4.4).

### Exact copy

| Element | Text | Source |
|---|---|---|
| Section heading | "Details" | design.md 4.8 |
| Labels | "Given name" · "Family name" · "Date of birth" (legend) | design.md A5 and 4.8 (sentence case) |
| Date part labels | "Day" · "Month" · "Year" | GOV.UK, NHS, NSW |
| Date of birth hint | "For example, 27 3 1987" | **(proposed)**, GOV.UK pattern: day of 13 or more, month of 9 or less, no leading zeros |
| Button | "Save" | design.md 4.8 |
| Busy label | "Saving…" | `primary-actions.md` |
| Saved status | "Saved 4:12 pm" | `status-messages.md` (from the note form's "Saved 9:42 am") |
| Add page heading | "Add participant" | design.md 4.8 (the list's link text) |
| Errors | As in the Save table above | `form-validation.md` and GOV.UK date input templates. "Year must include 4 numbers" is GOV.UK's own example. |
| Stale save | "Someone else changed these details while you were editing. Check the details below, then save again." | `form-validation.md` (proposed there) |
| Read-only date of birth | "7 March 1987" | **(proposed)**: no weekday, no leading zero, month in full. The app's "Thursday 1 October 2026" style is for note dates, and a weekday adds nothing to a birthday. |
| Past-day label / hint / button | "Note date" / "A date before today" / "Continue" | **(proposed)**. Error copy is `form-validation.md`'s "Enter the note date" and "Note date must be in the past". |
| Page title | "Grow2Notes – Participant", "Error: Grow2Notes – Participant" after a failed Save | design.md 4.0 (no names in titles) and `form-validation.md` |

### Accessibility

**Semantics.** Native `<form noValidate>`, `<label htmlFor>` on every input, `<fieldset>` and `<legend>` for the date,
`<dl>` for read-only values. ARIA is used only for `role="group"` on the date fieldset (the govuk-frontend JAWS fix),
`aria-describedby` for hints and errors, and `aria-invalid="true"` after a failed Save. Nothing else.

**Keyboard.** Tab order: Given name → Family name → Day → Month → Year → Save. Focus never moves on its own while typing.
Enter in any input submits the Details form (implicit submission), which runs the checks. Shift+Tab goes back to a part
without anything being cleared or reformatted.

**Expected screen-reader output (verify with NVDA + Chrome and VoiceOver + iOS Safari in M1):**
- Tabbing into Day: "Date of birth, grouping, For example, 27 3 1987, Day, edit, 7".
- After a failed Save, following the summary link "Date of birth must include a month": "Month, edit, invalid entry,
  blank", with the group's description now including "Error: Date of birth must include a month".
- After Save succeeds: "Saved 4:12 pm", read politely once. Focus stays on Save.
- Names: "Given name, edit, Jane". No hint is attached, so nothing extra is read.

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships (labels, fieldset, `dl`) · 1.3.5 Identify Input Purpose (not
applicable: the data is about someone else, so `off` is compliant) · 1.4.1 Use of Color (error text and bar, not colour
alone) · 1.4.3 Contrast (text and hint at least 4.5:1) · 1.4.4 Resize Text (em widths, no zoom lock) · 1.4.10 Reflow (date
parts wrap) · 1.4.11 Non-text Contrast (borders and focus at least 3:1) · 1.4.12 Text Spacing (em widths leave room) ·
2.1.1 Keyboard · 2.4.3 Focus Order (no auto-advance) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not
Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum), exceeded: inputs at least 48 px tall and Day and
Month at least 44 px wide · 3.2.2 On Input (nothing happens on change) · 3.3.1 Error Identification · 3.3.2 Labels or
Instructions (the date hint) · 3.3.3 Error Suggestion · 3.3.7 Redundant Entry (values kept after any error) · 4.1.2 Name,
Role, Value · 4.1.3 Status Messages ("Saved 4:12 pm").

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria here.** Native inputs, `fieldset` and `<input type="date">` cover every need. React Aria's DateField
  builds a segmented spin-button field, which is the opposite of the GOV.UK pattern. [Opinion]
- **Controlled inputs, no form library, no `<form action>`** (React 19 resets uncontrolled fields after an action, per
  `form-validation.md`). Keep `{givenName, familyName, dob: {day, month, year}}` as strings in `useState`, initialised
  once from the query data, in a child component keyed by participant ID.
- **One `parseDob` function**, pure and unit-tested, returns either an ISO date or `{message, parts}`. The server repeats
  the rules (`DateOnly`, before Melbourne today) and the client mirrors them only for wording.
- **Dates are strings.** Compare `YYYY-MM-DD` strings with `me.today`. Never build a `Date` from the device clock. When
  formatting the read-only date of birth, pass `timeZone: 'UTC'` with a `…T00:00:00Z` date, so a browser behind UTC never
  shows the day before. Checked: Node 24 `en-AU` gives "7 March 1987".
- **Yesterday for `max`:** do the date arithmetic on `me.today` in UTC (`Date.UTC(y, m - 1, d - 1)`), then format as ISO.
- **TanStack Query:** memory cache only (design 7). After Save, `setQueryData` on the participant and the list. Do not
  persist the cache. `retry` only on network errors and 5xx (`form-validation.md`).
- **ETag:** an edit needs the row's ETag (§5.1, §6.6). §6.6's list response shape shows no ETag field. See Open
  questions.
- **`handleSubmit` calls `event.preventDefault()` first.** With no `name` attributes, even a native submission sends
  nothing, but the handler should not rely on that.

```ts
// participants/dob.ts
export type DobParts = { day: string; month: string; year: string };
export type DobPart = keyof DobParts;
export type DobResult = { ok: true; iso: string } | { ok: false; message: string; parts: DobPart[] };

const MONTHS = ['january','february','march','april','may','june','july','august','september','october','november','december'];
const ORDER: DobPart[] = ['day', 'month', 'year'];

function monthNumber(raw: string): number | null {
  if (/^\d{1,2}$/.test(raw)) return Number(raw);
  const t = raw.toLowerCase().replace(/\.$/, '');
  if (t === 'sept') return 9;
  const i = MONTHS.findIndex((m) => m === t || m.slice(0, 3) === t);
  return i === -1 ? null : i + 1;
}

export function parseDob(p: DobParts, today: string): DobResult {
  const v = { day: p.day.trim(), month: p.month.trim(), year: p.year.trim() };
  const missing = ORDER.filter((k) => v[k] === '');
  if (missing.length === 3) return { ok: false, message: 'Enter the date of birth', parts: ORDER };
  if (missing.length) return { ok: false, message: `Date of birth must include a ${missing.join(' and ')}`, parts: missing };
  if (!/^\d{4}$/.test(v.year)) {
    return { ok: false, message: /^\d+$/.test(v.year) ? 'Year must include 4 numbers' : 'Date of birth must be a real date', parts: ['year'] };
  }
  const month = monthNumber(v.month);
  if (month === null || month < 1 || month > 12) return { ok: false, message: 'Date of birth must be a real date', parts: ['month'] };
  const day = /^\d{1,2}$/.test(v.day) ? Number(v.day) : NaN;
  const lastDay = new Date(Date.UTC(Number(v.year), month, 0)).getUTCDate();   // day 0 of next month
  if (!(day >= 1 && day <= lastDay)) return { ok: false, message: 'Date of birth must be a real date', parts: ['day'] };
  const iso = `${v.year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  if (iso >= today) return { ok: false, message: 'Date of birth must be in the past', parts: ORDER };
  return { ok: true, iso };
}
```

```tsx
// participants/DateOfBirthField.tsx (error look and summary wiring come from form-validation.md)
import s from './DateOfBirthField.module.css';
import type { DobParts, DobPart } from './dob';

const PARTS: { key: DobPart; label: string }[] = [
  { key: 'day', label: 'Day' }, { key: 'month', label: 'Month' }, { key: 'year', label: 'Year' },
];

type Props = { value: DobParts; onChange: (v: DobParts) => void; error?: string; invalid: DobPart[] };

export function DateOfBirthField({ value, onChange, error, invalid }: Props) {
  const base = 'details-dateOfBirth';
  return (
    <fieldset role="group" className={s.group} data-invalid={error ? '' : undefined}
      aria-describedby={error ? `${base}-hint ${base}-error` : `${base}-hint`}>
      <legend className={s.legend}>Date of birth</legend>
      <p id={`${base}-hint`} className={s.hint}>For example, 27 3 1987</p>
      {error && <p id={`${base}-error`} className={s.message}><span className="visually-hidden">Error: </span>{error}</p>}
      <div className={s.parts}>
        {PARTS.map(({ key, label }) => (
          <div key={key} className={s.part}>
            <label htmlFor={`${base}-${key}`} className={s.partLabel}>{label}</label>
            <input id={`${base}-${key}`} type="text" inputMode="numeric" autoComplete="off" spellCheck={false}
              className={`${s.input} ${key === 'year' ? s.year : s.short}`}
              value={value[key]} onChange={(e) => onChange({ ...value, [key]: e.target.value })}
              aria-invalid={invalid.includes(key) || undefined} />
          </div>
        ))}
      </div>
    </fieldset>
  );
}
```

```css
/* DateOfBirthField.module.css (shared .input tokens also used by the name TextFields) */
.group { border: 0; margin: 0 0 var(--space-6); padding: 0; min-inline-size: 0;
         scroll-margin-top: calc(var(--top-bar-height) + var(--space-4)); }
.legend { font-weight: 700; padding: 0; margin-block-end: var(--space-1); }
.hint { margin: 0 0 var(--space-2); color: var(--colour-text-secondary); }      /* at least 4.5:1 */
.parts { display: flex; flex-wrap: wrap; gap: var(--space-4); }               /* wraps at 200% text */
.part { display: flex; flex-direction: column; gap: var(--space-1); }
.input { font: inherit; font-size: max(1rem, 16px); box-sizing: border-box; min-block-size: 48px;
         padding: 0.25em 0.5em; border: 2px solid var(--colour-input-border); border-radius: 0;
         background: var(--colour-input-bg); color: var(--colour-text); appearance: none; }
.input:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 0; }
.short { inline-size: 2.75em; min-inline-size: 44px; }
.year  { inline-size: 4.5em; }
.group[data-invalid] { border-inline-start: 4px solid var(--colour-error); padding-inline-start: var(--space-3); }
.input[aria-invalid="true"] { border: 3px solid var(--colour-error); }
/* names: .name { inline-size: 100%; max-inline-size: 20.5em; } */
```

---

## Per-screen notes

**4.8 Participant detail: Details (edit).**
- Details is the first section, so the error summary at the top of `<main>` sits directly above it, under the
  participant's name (`form-validation.md`). Goals and Write past-day note are separate forms with their own inline
  errors. A failed Details save never marks a goal field.
- The `<h1>` shows the saved full name, never the half-typed one. It changes only after a `200`.
- Date of birth is never shown outside this screen and the archived read-only view (§5.3 "shown to managers only").

**4.8 Add participant.**
- The same component in "new" mode under the `<h1>` "Add participant". There are no Goals or Actions until the first
  Save, because goals need a participant ID (`POST …/participants/{participantId}/goals`).
- After `201`, replace the history entry so Back goes to the list. Keep focus on Save with "Saved 4:12 pm". The next Tab
  reaches the new Goals section ("Notes will show an empty Goals section." and Add goal), which suits the M1 test of
  adding five goals on a phone.
- The new participant appears on Today straight away (3.7, Active). No extra message is added.

**4.8 Archived participant.**
- The archived notice and Restore belong to `status-messages.md`. Details renders as a `<dl>` with the three values, and
  there is no Save. After Restore, the page returns to the editable form with the same values.

**4.8 Write past-day note.**
- One native date input, `max` = yesterday, no default, with "Continue (proposed)". `primary-actions.md` treats "Write
  past-day note" as a link to its own route, so this field is the main content of that small page, under the
  participant's full name (3.9: keep the person's name in view).
- The browser shows the date in its own locale. A manager whose phone is set to US English would see mm/dd/yyyy. The
  picker's calendar shows month names and weekdays, which removes the ambiguity when a date is picked. When it is typed,
  it is not removed. This is accepted for consistency with 4.7.
- Do not use the three-field pattern here. That pattern is for dates people remember. This date is chosen relative to
  today, and the calendar's weekdays help ("last Tuesday").

**4.8 Participants list.** No inputs from this component. "Find a participant" is `search-filter.md`.

---

## Anti-patterns to avoid

- `<input type="date">` for date of birth: the year is far back, iOS and Android pickers make the manager scroll or page
  through decades, and the display format follows the browser's locale.
- Three `<select>` dropdowns for day, month and year, or a single masked "dd/mm/yyyy" box that inserts slashes as you
  type. Both add interaction cost, and masks fight dictation and paste.
- Auto-advancing from Day to Month after two digits (GOV.UK: never).
- `type="number"` on date parts: the scroll wheel changes values, and `.value` returns `""` for "07a".
- `maxLength` on names (silent truncation), or `pattern`, or any character filter that rejects apostrophes, hyphens,
  spaces or accents ("O'Brien", "Nguyễn", "Mary-Jane").
- Re-casing names ("MCNAMARA" to "Mcnamara", "de Silva" to "De Silva") on save or display.
- `autocomplete="given-name"`, `"family-name"` or `"bday-*"` on a participant's details. Chrome would offer the manager's
  own name and birthday, and the browser would store participant data on a personal device.
- Made-up `autocomplete` values or renamed fields to defeat Chrome's autofill, unless M1 testing shows Chrome offering
  the manager's own details despite `off`. Even then, record the reason in code.
- `name` attributes, or a `<form>` that could submit with GET and put "givenName=Jane&…" in the URL and browser history
  (design 4.0: URLs hold IDs and dates only).
- Placeholder text as the label or the example. The hint sits above the inputs and stays visible.
- Disabling inputs during Save, or showing a disabled input as the read-only view.
- Live validation as the manager types, or red borders before the first Save.
- Re-filling the inputs from a refetch while the manager is typing.
- `user-scalable=no` or `maximum-scale=1` to stop iOS zooming. Use 16 px input text.
- Building `Date` objects from the device clock to decide "in the past" or "yesterday". Use `me.today`.
- Adding a duplicate-participant check, an NDIS number, a preferred-name field, a title, a middle-name field or an
  age display. None is in the design (A5).

---

## Tensions with decisions

1. **Family name is required** (design.md A5; `FamilyName nvarchar(100) NOT NULL`, §5.3). W3C Internationalization
   advises: "Don't require that people supply a family name." Some people, for example in parts of Southern India,
   Malaysia and Indonesia, have a given name only. Forcing a family name leads to junk entries such as "." just to get
   past the form. **[Convention, W3C guidance]**
   https://www.w3.org/International/questions/qa-personal-names The design needs the family name to sort every list
   (A5), and this is a small provider whose participants are known in person. No change is proposed. This file keeps
   "Enter the family name" and adds no workaround.

---

## Open questions

- **Duplicate warning (same name and date of birth).** The brief for this component mentions one, but design.md does
  not: 4.8 has no such state, §6.6 has no duplicate response, and §6.9 has no error code for it. Nothing is designed
  here. If the owner wants it, it needs a decision first.
- **Earliest allowed date of birth.** design.md only implies "before today". A year typo such as 1087 passes as a real
  date. Should there be a lower bound (for example "Date of birth must be the same as or after 1 1 1900", GOV.UK's
  template)? Not added without a decision.
- **Where the edit ETag comes from.** §6.6 says every `PUT` needs `If-Match` from the row's `RowVersion`, but the
  `GET /api/admin/participants` response shape lists no ETag. Confirm in M1 how the client receives it (for example, a
  field per row).
- **Unsaved changes on Details.** design.md asks for a leaving-with-unsaved-changes warning only on Guide prompts
  (4.10). None is added for the three Details fields.
- **Proposed strings:** the date hint "For example, 27 3 1987", the read-only date format "7 March 1987", and the
  past-day "Note date", "A date before today" and "Continue".
- **Chrome autofill:** check in M1 whether Chrome on Android and desktop still offers the signed-in manager's own name
  in Given name and Family name despite `autocomplete="off"`.

---

## Sources

- GOV.UK Design System, Date input: https://design-system.service.gov.uk/components/date-input/
- GOV.UK Design System, Dates pattern: https://design-system.service.gov.uk/patterns/dates/
- GOV.UK Design System, Names pattern: https://design-system.service.gov.uk/patterns/names/
- GOV.UK Design System, Text input: https://design-system.service.gov.uk/components/text-input/
- govuk-frontend date input template: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/date-input/template.njk
- govuk-frontend input styles (width classes, no hover style): https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/input/_mixin.scss
- govuk-frontend issue #1590, `role="group"` on the date fieldset: https://github.com/alphagov/govuk-frontend/issues/1590
- NHS digital service manual, Date input: https://service-manual.nhs.uk/design-system/components/date-input
- NSW Design System, Date input: https://designsystem.nsw.gov.au/components/date-input/index.html
- Australian Government Design System (AgDS), Date picker (Australian "dd/mm/yyyy" format): https://design-system.agriculture.gov.au/components/date-picker
- W3C Internationalization, Personal names around the world: https://www.w3.org/International/questions/qa-personal-names
- W3C WAI Forms tutorial, Grouping controls: https://www.w3.org/WAI/tutorials/forms/grouping/
- WCAG 2.2 Understanding: 1.3.5 https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html · 1.4.10
  https://www.w3.org/WAI/WCAG22/Understanding/reflow.html · 1.4.11 https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
  · 1.4.4 https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html · 2.4.11 https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
  · 2.5.8 https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html · 3.3.2 https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html
- HTML Standard, constructing the entry list (unnamed controls are not submitted): https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set
- MDN, `<input type="date">`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- MDN, Turning off form autocompletion: https://developer.mozilla.org/en-US/docs/Web/Security/Practical_implementation_guides/Turning_off_form_autocompletion
- MDN, `autocorrect`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocorrect
- MDN, `autocapitalize`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocapitalize
- NN/g, "Date-Input Form Fields: UX Design Guidelines" (Angie Li, 2017): https://www.nngroup.com/articles/date-input/
- Baymard Institute, "Form Field Usability: Matching User Expectations" (2010): https://baymard.com/blog/form-field-usability-matching-user-expectations
- Baymard Institute, "Avoid Extensive Multicolumn Layouts" (2023): https://baymard.com/blog/avoid-multi-column-forms
- Hassell Inclusion, "Is input type=date ready for use in accessible websites?" (Graham Armfield, 2019): https://hassellinclusion.com/blog/input-type-date-ready-for-use/
- a11ysupport.io, `input type=date`: https://a11ysupport.io/tech/html/input(type-date)_element
- WHATWG list, "PSA: Chrome ignoring autocomplete=off for Autofill data" (2014): https://lists.w3.org/Archives/Public/public-whatwg-archive/2014Nov/0092.html
- Adam Silver, "Stopping Chrome from ignoring autocomplete=off" (2021): https://adamsilver.io/blog/stopping-chrome-from-ignoring-autocomplete-off/
- Example report of iOS input zoom below 16 px: https://github.com/heroui-inc/heroui/issues/5326
- Sibling specs this file follows: `form-validation.md`, `status-messages.md`, `primary-actions.md`, `search-filter.md`
