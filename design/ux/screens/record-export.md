# Participant record export

Screen spec for design.md **§4.12**. The file's content is defined in **§11.6**, and the defaults come from **A20** and **A30**. Only managers can use this screen. The spec puts together the researched component files linked below and stays faithful to design.md and D1–D47. It adds no screens, fields, settings, notifications or data.

**Evidence grades:** **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML spec, MDN/framework docs · **[Convention]** established design systems · **[Opinion]** reasoned judgement with no direct evidence. The full evidence is in each linked component file. This file cites the deciding source for each choice that is specific to this screen.

**Copy marks** (as defined in [microcopy.md](../components/microcopy.md)):
- **(V)** is word for word from design.md.
- **(N)** is design.md wording with only the date format normalised by microcopy.md.
- **(P)** is proposed and not in design.md. Every (P) string here on 9 October 2026 was approved as written (D67, Open question 5); a (P) string added later still needs the owner's sign-off.

**Example data used throughout (made up):** `me.today` is Thursday 1 October 2026. The participant is Jane Citizen, and her earliest submitted note is dated Thursday 1 January 2026.

---

## Purpose and who uses it

- **Purpose (V, §4.12):** "answer a participant's (or their representative's) request to see their records (APP 12, HPP 6)". The manager chooses a date range and a format, presses **Export**, and gets one PDF or Word file of that participant's submitted notes (§11.6). The file is never shown on screen (D43 principle), and the app sends it nowhere (D28, D30).
- **Common items in the file (D47, §11.6).** Each note shows the Every note group (left out when it has no items) and the groups picked on that note, each under its group name in the configured order, with every item in those groups ticked or not ticked; groups not picked do not appear. With **Include earlier versions of edited notes** ticked, each earlier version is printed with the groups picked in that version. Nothing on this screen changes because of this: no group appears, is counted or is chosen here.
- **Who:** managers only (§2, D24).
  - They use laptops and phones (§4.0, D5). There is no usage data on which is more common.
  - This is an occasional task, tied to an access or correction request that has a legal deadline. That deadline is usually 30 days under APP 12, and at most 45 days under HRA s 34 (§12).
  - Because the task is rare, nothing on the screen relies on the manager remembering how it worked last time. [Opinion]
- **What happens next is outside the app.** The manager reads the copy. On the **Word** copy, they redact anything that must not be given (HRA ss 26–27, APP 12.3) and any other person's information. Then they hand it over (§4.13 "Access request", §11.6). The screen does not prompt for these steps. The two hints state the facts the manager needs when choosing (Components 7 and 8).
- **How managers get here:** the "Export record" link in the Actions on Participant detail (4.8), and in the manager actions on Past notes (4.4). Both are secondary links styled as buttons ([primary-actions.md](../components/primary-actions.md)). Browser Back returns to where the manager came from.
- **Workers never reach it.** No link is rendered for them. The route renders "Page not found" ([app-shell-nav.md](../components/app-shell-nav.md) role guard), and the export endpoint is manager-only (§6.5).
- **Archived participants can be exported.** "History, reports and exports are unchanged" (§3.7). The screen looks and behaves the same for them.
- **Deliberately not on this screen** ([export-form.md](../components/export-form.md) anti-patterns):
  - a preview of the record, or email and share buttons (D28, D30, D43)
  - a remembered format or date range (D22)
  - a note count for the range (D26), or a "first note was on …" hint
  - a confirmation dialog, a Cancel button, or a progress percentage

---

## Layout - phone

At about 375 px wide, with 16 px gutters, the content column is 343 px. Everything is in one column, top to bottom. This is the default state after loading:

```
┌───────────────────────────────────────────────┐
│ Grow2Notes                         Account    │  app shell header (app-shell-nav.md)
│ Today   Flagged 2   Report   Manage           │  Manage is current (aria-current="true")
├───────────────────────────────────────────────┤
│                                               │  <main>  (error summary goes here, above
│ Export record                                 │   the <h1>, only after a failed Export)
│                                               │  <h1> 28 px; focused on arrival
│ Participant                                   │  <dl><dt> 18 px bold
│ Jane Citizen                                  │  <dd> 22 px bold, the name's own case
│                                               │
│ From date                                     │  <label for="export-from"> 18 px bold
│ ┌───────────────────────────────────────────┐ │
│ │ 01/01/2026                              ▾ │ │  <input type="date">, full width, 48 px,
│ └───────────────────────────────────────────┘ │   shown in the device's own format
│                                               │
│ To date                                       │  <label for="export-to">
│ ┌───────────────────────────────────────────┐ │
│ │ 01/10/2026                              ▾ │ │  <input type="date">, 48 px
│ └───────────────────────────────────────────┘ │
│                                               │
│ Format                                        │  <fieldset><legend> 18 px bold
│ ┌───────────────────────────────────────────┐ │
│ │ ( )  PDF                                  │ │  whole row is the <label>, 56 px min
│ │      For printing and filing              │ │  item hint, 16 px, secondary text
│ ├───────────────────────────────────────────┤ │
│ │ ( )  Word                                 │ │  56 px min
│ │      Can be edited, so use it if you need │ │
│ │      to remove anything before handing it │ │
│ │      over                                 │ │
│ └───────────────────────────────────────────┘ │
│                                               │
│ ┌───────────────────────────────────────────┐ │
│ │ [ ]  Include earlier versions of edited   │ │  whole row is the <label>, 56 px min,
│ │      notes                                │ │   unticked (A20)
│ │      Adds each earlier version in full,   │ │  hint, 16 px, secondary text
│ │      which can include corrected wording  │ │
│ │      or another person's information      │ │
│ └───────────────────────────────────────────┘ │
│                                               │
│                                               │  role="alert"  (in the DOM, empty)
│                                               │  role="status" (in the DOM, empty)
│ ┌───────────────────────────────────────────┐ │
│ │                  Export                   │ │  primary <button type="submit">,
│ └───────────────────────────────────────────┘ │   full width, 48 px
│                                               │  64 px clear space below
└───────────────────────────────────────────────┘
```

The same screen in its other states. Only the parts that change are shown.

```
After Export with problems                 While exporting (after 400 ms)
┌─────────────────────────────────────┐    │ ┌─────────────────────────────────┐ │
│▌There is a problem                  │    │ │ Preparing the PDF file… Keep    │ │ status line,
│▌                                    │    │ │ this page open.                 │ │  secondary text
│▌To date must be the same as or      │    │ └─────────────────────────────────┘ │
│▌after 1 January 2026                │    │ ┌─────────────────────────────────┐ │
│▌Choose PDF or Word                  │    │ │           Exporting…            │ │ aria-disabled;
└─────────────────────────────────────┘    │ └─────────────────────────────────┘ │  same look, same width
 (4 px error border, focused)              │  fields stay editable

▌ To date                                  After the file is handed over
▌ To date must be the same as or after     │ participant-record_2026-01-01_to_ │ status line, wraps
▌ 1 January 2026                           │ 2026-10-01.pdf is ready. Look in  │  anywhere; stays
                                           │ your downloads.                   │
▌ ┌─────────────────────────────────────┐  │ [            Export             ] │  until the next press
▌ │ 31/12/2025                        ▾ │  │                                   │  or a date change
▌ └─────────────────────────────────────┘  No submitted notes in the range
 (3 px error border, aria-invalid)         │ No submitted notes for Jane       │ status line, body
                                           │ Citizen between 1 January 2026    │  text, NOT red
▌ Format                                   │ and 1 October 2026.               │
▌ Choose PDF or Word                       │ [            Export             ] │
▌ ( ) PDF  …                               Request failed
▌ ( ) Word …                               │ Not exported: no connection.      │ role="alert", bold,
                                           │ Try again.                        │  error colour
                                           │ [            Export             ] │
```

**Spacing** ([foundations.md](../components/foundations.md)):
- Label to field: 8 px (`--space-2`).
- Between field groups (From, To, Format, the earlier-versions row): 24 px (`--space-5`).
- 32 px (`--space-6`) above the alert, status and Export block.
- 64 px (`--space-8`) below Export.
- Heading to participant: 12 px (`--space-3`). Participant to From date: 24 px.

**Nothing is sticky or fixed** on this screen (foundations.md: only the note form's top bar sticks). [Convention]

**Reflow.** At 320 px wide, at 200% text, or at 400% zoom, every line wraps and nothing scrolls sideways (SC 1.4.4, 1.4.10). The file name in the status line has no spaces, so the line uses `overflow-wrap: anywhere` ([file-download.md](../components/file-download.md)). [Standard]

---

## Layout - laptop

What changes from 40rem (640 px) up, the app's one breakpoint (foundations.md):

| | Phone (< 40rem) | Laptop (≥ 40rem) |
|---|---|---|
| Column | Full width minus 16 px gutters | The `--measure` column (40rem), left-aligned inside the 60rem page container. This is not a setup screen, so it does not use the full page width (foundations.md, §4.0) |
| Gutters / section gaps | 16 / 32 px | 32 / 48 px |
| `<h1>` | 28 px | 32 px |
| Participant `<dd>` | 22 px | 24 px (`--font-size-h2` steps with the breakpoint) |
| Date fields | Full width, 48 px tall | `max-inline-size: 14rem` (fits the date and the browser's calendar button), 48 px tall. **Still stacked**, not side by side: an error above To date would push its input out of line with From date, and stacking reflows at 400% with no extra work (export-form.md) [Opinion] |
| Date entry | Tapping opens the phone's own picker. No keyboard opens | Typing segment by segment, or the browser's calendar button. NN/g calls typing "the most efficient" option for dates more than a year away [Convention] https://www.nngroup.com/articles/date-input/ |
| Format radios | Stacked, with hints | Stacked as well. GOV.UK keeps radios inline only for two **short** options, and the hints rule that out [Convention] https://design-system.service.gov.uk/components/radios/ |
| Rows (radios, checkbox) | 56 px min, full column width | Same. Hover tint `--colour-hover` across the whole row, only under `@media (hover: hover)` |
| Export | Full width | Natural width, left-aligned ([primary-actions.md](../components/primary-actions.md)) |
| Alert and status lines | Directly above Export | Same |

```
Grow2Notes          Today   Flagged 2   Report   Manage                  Account
─────────────────────────────────────────────────────────────────────────────────
  Export record
  Participant
  Jane Citizen

  From date
  [ 01/01/2026   📅 ]
  To date
  [ 01/10/2026   📅 ]

  Format
  ( ) PDF
      For printing and filing
  ( ) Word
      Can be edited, so use it if you need to remove anything before handing it over

  [ ] Include earlier versions of edited notes
      Adds each earlier version in full, which can include corrected wording or another
      person's information

  [ Export ]
```

---

## Components, in order

DOM order is the visual order at every width. There is no `order` and no `row-reverse`.

### 1. Page frame: app shell, loading and failure

Uses [app-shell-nav.md](../components/app-shell-nav.md) and [empty-loading-error.md](../components/empty-loading-error.md).

| Setting | Value |
|---|---|
| Route | `/manage/participants/{participantId}/export` (app-shell.md's URL table). There are no names and no dates in the path (§4.0) [Standard: design §4.0] |
| Page title | **Grow2Notes – Export record** (fixed list in app-shell.md). After a failed Export: **Error: Grow2Notes – Export record** |
| Nav "current" item | **Manage**, with `aria-current="true"`: the route is under `/manage/…`, so app-shell.md's URL table gives Manage, the same as the past-day page at `/manage/participants/{id}/past-day-note`. (An earlier draft said "none", citing app-shell-nav.md; app-shell.md's table is the one the shell builds.) |
| Role guard | Workers get the shared "Page not found" page |
| Data needed before the form renders | (a) the participant's given and family name (`GET /api/participants/{id}`, §6.3); (b) the date of the participant's **earliest submitted note**. No API in §6 returns (b) yet (see Open questions). It is written `firstNoteDate` below, and it is `null` when there are no submitted notes |
| Today | `me.today` from `GET /api/auth/me`. This is the server's Melbourne date, never the device clock (§3.3, A33) [Standard: design] |
| Loading | Nothing for 1 s, then **"Loading participant…"** **(P)**, the same line as Participant detail in empty-loading-error.md. It sits in that region's own `role="status"` |
| Load failed | **"The export form did not load: no connection. Try again."** or **"The export form did not load: something went wrong. Try again."** **(P)**, using microcopy.md's canonical "[Thing] did not load: [cause]. Try again.", plus a **Try again** button. The `<h1>` "Export record" shows as the fallback heading |
| Participant not found (404) | The shared "Page not found" page |
| Session ended (401) | The sign-in-in-place flow ([session-timeout.md](../components/session-timeout.md)). Form values stay in memory |

The `<h1>` renders with the form, once both values have arrived. That way the error summary can sit above it (form-validation.md). While data loads, arrival focus waits for the heading (empty-loading-error.md `PageHeading`).

### 2. Error summary

Uses [form-validation.md](../components/form-validation.md). This is a multi-field form, so the summary appears with the inline errors, even when there is only one error. [Convention: GOV.UK, NHS]

| Setting | Value |
|---|---|
| When | Only after Export is pressed with a problem, or after a `422` from the export request |
| Position | First child of `<main>`, above the `<h1>` |
| Container | `<div tabIndex={-1}>` with a 4 px `--colour-error` border. Focused on **every** failed attempt. No `role="alert"`: the focus move does the announcing (form-validation.md, Opinion to verify with NVDA and VoiceOver) |
| Heading | `<h2>` **There is a problem** [Convention] |
| Items | One `<a href="#{id}">` per error, in page order (From, To, Format). The link text is exactly the inline message. Selecting a link scrolls the label or legend into view, then calls `focus({ preventScroll: true })` on the input. The URL does not change |
| Format link target | `export-format` (the PDF radio) |
| Clears | Each item disappears as soon as its field is fixed. When the last item goes, the summary and the `Error: ` title prefix go too |

### 3. Page heading

| Setting | Value |
|---|---|
| Element | `<h1 id="page-heading" tabIndex={-1}>` through the shared `PageHeading` (focused on arrival) |
| Text | **Export record** **(P)**. It matches the "Export record" link text (4.4, 4.8) and the page name. Glossary: "Export" here, "Download" on the Daily report (microcopy.md) |

### 4. Participant (fixed)

Design: "Participant (fixed)".

| Setting | Value |
|---|---|
| Element | `<dl><dt>Participant</dt><dd>Jane Citizen</dd></dl>`. **Never** an `<input disabled>`: disabled text has no contrast requirement (SC 1.4.3) and is skipped in the tab order. GOV.UK shows fixed answers as a summary list [Standard + Convention] https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html · https://design-system.service.gov.uk/components/summary-list/ |
| Text | `dt` "Participant" **(V)**, bold, body size. `dd` is the full name, given then family, exactly as stored, never re-cased or truncated. It uses `--font-size-h2`, bold, `--colour-text` (microcopy.md names rule; foundations.md) |
| Why it is prominent | The full name is the app's wrong-participant guard (§3.9). Here it guards against exporting the wrong person's health record [Opinion, by analogy with §3.9] |
| Validation | None. Read-only values are never validated (form-validation.md) |
| Privacy | The name is on the page only. It is not in the title, the URL, router state or the file name (§4.0, §6.5, A19) |

### 5. From date

Uses [date-navigation.md](../components/date-navigation.md), [export-form.md](../components/export-form.md), [form-validation.md](../components/form-validation.md) and [foundations.md](../components/foundations.md).

| Setting | Value |
|---|---|
| Element | `<label for="export-from">From date</label>` + `<input type="date" id="export-from" name="from">`, inside the shared `DateField` wrapper (label, then error, then input) |
| Label | **From date** **(V)** |
| Default | `firstNoteDate`. If the participant has no submitted notes, `me.today` (export-form.md). The default is set **once**, when the form first renders |
| `max` | `me.today`. This is a picker hint only: iOS ignores `min` and `max` (WebKit bug 225639, open), and desktop typing gets past them [Research] https://bugs.webkit.org/show_bug.cgi?id=225639 |
| `min` | None (design sets no lower bound) |
| Size | Full width on a phone. `max-inline-size: 14rem` from 40rem. `min-block-size: max(3rem, 48px)`. Text inherits 18 px, which is never below 16 px, so iOS does not zoom (foundations.md) |
| Border / focus | 2 px `--colour-border-control`. Focus is a 3 px `--colour-focus` outline with a 2 px offset, on `:focus-visible`, never animated |
| Value handling | `YYYY-MM-DD` strings end to end. An incomplete date reads as `""`. Never `valueAsDate`, never `new Date(string)`, never the device clock (date-navigation.md `dates.ts`) [Opinion, tested there] |
| Validation (on Export only) | Empty or incomplete: **"Enter the From date"** **(P)** |
| Error look | The message sits between the label and the input, in bold `--colour-error` with a visually hidden "Error: " prefix. The group gets a 4 px error bar, the input a 3 px error border, and `aria-invalid="true"` and `aria-describedby="export-from-error"` (form-validation.md) |
| Changing it | Never changes To date automatically (export-form.md). It clears an old result line (Interactions). If a date error is showing, both date fields are re-checked |

### 6. To date

The same component and settings as From date, except:

| Setting | Value |
|---|---|
| Element | `id="export-to"`, `name="to"` |
| Label | **To date** **(V)** |
| Default | `me.today` |
| `min` | The From value, when From is a complete date. This is a picker hint only |
| `max` | `me.today`. A typed future To date is **not** an error: the design defines no such error, and no note can exist after today (date-navigation.md). What the server does with it is an Open question |
| Validation (on Export only) | Empty or incomplete: **"Enter the To date"** **(P)**. Before From: **"To date must be the same as or after 1 January 2026"** **(N)**. The slot is the From date as entered, written with microcopy.md's `datePlain`. The error goes on **To date**, because design §4.12 says "an error next to the field" [Convention: GOV.UK date template] https://design-system.service.gov.uk/components/date-input/ |
| Re-check | Once To date shows an error, changing **either** date re-checks it. The message updates if From changes, and the error clears the moment the range is valid (form-validation.md "reward early") [Research, qualitative: Baymard] |

**Why a native date field.** Design §4.12 does not name a control, and §4.7 uses "a native date field" for the same managers.
- These are chosen dates, usually within the last year, and they are pre-filled. In the usual path the manager enters no date at all. NN/g supports a calendar for dates near the present. [Convention] https://www.nngroup.com/articles/date-input/
- Known gaps, recorded and not hidden:
  - In Hassell Inclusion's 2019 testing, Dragon could not use `type="date"`, and VoiceOver on iOS did not voice browser validation messages. [Research, dated] https://hassellinclusion.com/blog/input-type-date-ready-for-use/
  - The field shows the browser locale's format, which is mm/dd on a US-English browser. [Standard] https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date
- Mitigations:
  - The defaults are pre-filled.
  - The app writes its own error messages; the browser's bubbles are never used (`noValidate`).
  - Every message spells the date out in words.
  - The file name confirms the range in ISO dates.
- No custom calendar and no React Aria `DatePicker` (date-navigation.md, export-form.md). [Convention + Opinion]

### 7. Format

Uses [export-form.md](../components/export-form.md) and [form-validation.md](../components/form-validation.md). The row layout comes from [checkbox-list.md](../components/checkbox-list.md) and the size from foundations.md.

| Setting | Value |
|---|---|
| Element | `<fieldset>` + `<legend>Format</legend>`, with two native `<input type="radio" name="format">`. Radios, not a select: NN/g says radios have "lower cognitive load because they make all options permanently visible" [Convention] https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/ · fieldset and legend [Standard: H71] https://www.w3.org/WAI/WCAG22/Techniques/html/H71 |
| Legend | **Format** **(V)** |
| Options, in order | **PDF** (`id="export-format"`, `value="pdf"`), then **Word** (`id="export-format-docx"`, `value="docx"`). This is the design's order ("PDF or Word"), which is also alphabetical. The Daily report lists Word first because §4.7 does; each screen keeps its own design order |
| Pre-selection | **None.** GOV.UK: "Do not pre-select radio options as this makes it more likely that users will not realise they've missed a question" (checked 1 October 2026) [Convention] https://design-system.service.gov.uk/components/radios/. Neither format is right for most requests: redaction needs Word (§11.6), and filing needs PDF (§11.5) [Opinion] |
| Item hints | PDF: **"For printing and filing"** **(P, from §11.5)**. Word: **"Can be edited, so use it if you need to remove anything before handing it over"** **(P, from §11.6)**. Each is one sentence with no full stop, in `--font-size-small` and `--colour-text-secondary`, linked to its radio with `aria-describedby`. GOV.UK: "Keep each hint to a single short sentence, without any full stops" [Convention] |
| Rows | The shared ChoiceRow radio, the same as users.md's Role: the whole row is the `<label>`, `min-block-size: 3.5rem` (56 px), with a **40 px circle drawn with `appearance: none` and a border-drawn dot** (the same technique and size as the tick box in checkbox-list.md), so it survives forced colours. Never `display: none`, **no `accent-color`**: the mark is not legible on Chrome Android or older Safari (note-form.md Conflicts #11) |
| Validation (on Export only) | Nothing chosen: **"Choose PDF or Word"** **(P)**. The message sits under the legend, inside the fieldset, and the `<fieldset>` gets `aria-describedby="export-format-error"`. The radios get no `aria-invalid`, as in GOV.UK. Choosing either radio clears the error at once |
| Keyboard | One tab stop for the group. Arrow keys move between the radios and select them (native). Selecting does nothing else (SC 3.2.2) |

### 8. Include earlier versions of edited notes

Uses [export-form.md](../components/export-form.md).

| Setting | Value |
|---|---|
| Element | One native `<input type="checkbox" id="export-includeHistory" name="includeHistory">` in a 56 px label row. A single checkbox needs no fieldset. It is a checkbox and not a switch, because its value takes effect only when Export is pressed [Convention: ui-build control table] |
| Label | **Include earlier versions of edited notes** **(V)** |
| Default | Unticked (A20). It is never remembered between visits (D22) |
| Hint | **"Adds each earlier version in full, which can include corrected wording or another person's information"** **(P, from §11.6 "each printed in full" and §3.9)**. Linked with `aria-describedby="export-includeHistory-hint"` [Convention: GOV.UK hint text] |
| Validation | None |
| Sent as | `includeHistory=true` or `includeHistory=false` |

### 9. Request-failure alert

Uses [primary-actions.md](../components/primary-actions.md), [export-form.md](../components/export-form.md) and [microcopy.md](../components/microcopy.md).

| Setting | Value |
|---|---|
| Element | `<div role="alert" id="export-alert">`, always in the DOM and empty until needed. It sits directly above the status line and the Export button. The live region must exist before anything is written into it [Standard: MDN live regions] https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions |
| Look | Body size, bold, `--colour-error`. The words "Not exported" carry the meaning, so colour is not the only cue (SC 1.4.1) |
| Copy | Uses microcopy.md's "Not [done]: [cause]. Try again." See States for every string |
| Cleared | When Export is pressed again. It is never auto-dismissed (SC 2.2.1) |
| Spacing | Margins only when not empty (`:not(:empty)`). The element is never `display: none` |

### 10. Result status line

Uses [export-form.md](../components/export-form.md) and [file-download.md](../components/file-download.md).

| Setting | Value |
|---|---|
| Element | `<p role="status" id="export-status">`, always in the DOM and empty until needed. It sits between the alert and Export |
| Carries | The preparing line (busy for 400 ms or more), the success line, and the design's no-notes sentence. These are status messages under SC 4.1.3: no focus move, polite announcement [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html |
| Look | The preparing line uses `--colour-text-secondary`. The success and no-notes lines use `--colour-text`, at body size and upright, never red. `overflow-wrap: anywhere` |
| Dates | Each date sits in `<time dateTime="2026-01-01">1 January 2026</time>` (microcopy.md) |
| Cleared | When Export is pressed. When either date changes while **no** export is running, because the old sentence named the old dates. It is never auto-dismissed. Format and checkbox changes leave it alone |

### 11. Export button

Uses [file-download.md](../components/file-download.md), [export-form.md](../components/export-form.md) and the shared `Button` from [primary-actions.md](../components/primary-actions.md).

| Setting | Value |
|---|---|
| Element | `<button type="submit">`, primary variant, the form's only submit button. A button and not a link, because it fetches a file and writes an audit row (file-download.md) [Convention: Roselli; Standard: MDN] |
| Label | **Export** **(V)**. Busy label **"Exporting…"** **(P)**, kept in the same grid cell so the width never changes |
| Size | `min-block-size: max(3rem, 48px)`. Full width on a phone, natural width from 40rem |
| Unavailable | **Never.** Export is never disabled. Checks run when it is pressed. GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible" [Convention] https://design-system.service.gov.uk/components/button/ |
| Busy | `aria-disabled="true"` from the moment of the press, **never** `disabled`, which drops focus to `<body>`. After 400 ms the label becomes "Exporting…" with `cursor: progress`. No spinner (foundations.md: no motion in v1). Colours stay the same, because busy is not unavailable |
| Double press | An `inFlight` ref guard ignores a second press or Enter, before React re-renders. `aria-disabled` alone does not stop clicks or implicit submission (file-download.md) |
| Announcement | The Button's busy announcement into the page status region (`PageStatus`, app-shell.md component 3) is **turned off** here (`announceBusy={false}`, see Conflicts resolved). The local status line says it instead |
| Request | `GET /api/reports/participants/{id}/export` through the shared `api()` wrapper's raw-`Response` form, passing the wrapper's per-call option **`timeoutMs: 30_000`** (empty-loading-error.md), the same value as the Daily report downloads, and the screen's `AbortController` signal for leaving the page. M5 allows up to 30 s, so the default 10 s would report a slow but working export as "no connection" |
| Focus | Stays on Export after success, no notes, and every request failure, so pressing it again retries |

### 12. Shared formats and copy

Uses [microcopy.md](../components/microcopy.md) and [foundations.md](../components/foundations.md).

- **Dates in messages use `datePlain` ("1 January 2026").** microcopy.md uses it for From/To ranges, and its section 9 makes it canonical for the range error. A non-breaking space goes between the day and the month. The text is built from numeric parts, never `Intl` `dateStyle`.
- **Strings live in `src/copy`.** This screen uses the shared `filePreparing(format)` and `fileReady(fileName)` (one pair for both download screens), and adds `noNotesInRange(name, from, to)`, `notExported(cause)` and the four field errors. ESLint `react/jsx-no-literals` keeps literals out of JSX.
- **The action is "Export", never "download".** The glossary uses "Export" here and "Download" on the Daily report (microcopy.md). The one exception is the ready line's "Look in your downloads.", which names the browser's Downloads folder, not the action; it is shared word for word with the Daily report.
- **`<html lang="en-AU">`.** Light theme only: `color-scheme: light`, so the native date picker stays light (foundations.md).

### Assembly sketch (React 19 + TypeScript + CSS Modules)

This sketch composes the shared parts and applies the choices in "Conflicts resolved". `DateField`, `ChoiceRow`, `ErrorSummary`, `PageHeading`, `Button` and `saveResponseAsFile` come from the component files. Every string comes from `src/copy`.

```tsx
// features/export/ExportRecordForm.tsx (sketch)
type Format = 'pdf' | 'docx';
type Field = 'from' | 'to' | 'format';
const IDS: Record<Field, string> = { from: 'export-from', to: 'export-to', format: 'export-format' };

export function ExportRecordForm({ participant, firstNoteDate, today }: {
  participant: { id: string; fullName: string };   // "Jane Citizen", exactly as stored
  firstNoteDate: string | null;                    // earliest SUBMITTED note (API gap, see Open questions)
  today: string;                                   // me.today: server, Melbourne
}) {
  const [from, setFrom] = useState(firstNoteDate ?? today);   // defaults set once
  const [to, setTo] = useState(today);
  const [format, setFormat] = useState<Format | null>(null);  // nothing pre-selected
  const [includeHistory, setIncludeHistory] = useState(false); // A20
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [attempt, setAttempt] = useState(0);
  const [status, setStatus] = useState('');                  // role="status"
  const [alertText, setAlertText] = useState('');            // role="alert"
  const [busy, setBusy] = useState(false);
  const inFlight = useRef<AbortController | null>(null);
  useEffect(() => () => inFlight.current?.abort(), []);      // leaving the page aborts; no late file

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (inFlight.current) return;                            // double tap, or Enter while busy
    const found = validateExport(from, to, format);          // copy.exportErrors; datePlain(from) in the To message
    setErrors(found); setAttempt((n) => n + 1);              // ErrorSummary focuses itself on attempt change
    if (Object.values(found).some(Boolean)) return;          // nothing is sent, nothing is audited
    setStatus(''); setAlertText('');
    const q = { from, to, format: format!, includeHistory };
    const ac = (inFlight.current = new AbortController());
    setBusy(true);
    const slow = window.setTimeout(() => setStatus(copy.filePreparing(q.format)), 400);
    try {
      const r = await saveResponseAsFile(exportUrl(participant.id, q), `participant-record_${q.from}_to_${q.to}.${q.format}`,
        { signal: ac.signal, timeoutMs: 30_000 });              // api() per-call timeout (empty-loading-error.md)
      window.clearTimeout(slow);
      setStatus('');
      await nextFrame();                                      // a repeated sentence is announced again
      switch (r.kind) {
        case 'saved':     setStatus(copy.fileReady(r.fileName)); break;   // "{file} is ready. Look in your downloads."
        case 'noNotes':   setStatus(copy.noNotesInRange(participant.fullName, q.from, q.to)); break;
        case 'invalid':   setErrors(mapFieldErrors(r.errors, IDS)); setAttempt((n) => n + 1); break;  // 422
        case 'failed':    setAlertText(copy.notExported(r.cause)); break; // 'offline' | 'server' | 'tooMany'
        case 'notManager': refreshMe(); break;                    // 403: the role guard shows "Page not found"; no message
        case 'signedOut': break;                              // the shell's sign-in-in-place flow takes over
      }
    } catch { /* aborted: the page is gone, say nothing */ }
    finally { window.clearTimeout(slow); inFlight.current = null; setBusy(false); }
  }

  function changeDate(which: 'from' | 'to', value: string) {
    const f = which === 'from' ? value : from, t = which === 'to' ? value : to;
    (which === 'from' ? setFrom : setTo)(value);
    if (!inFlight.current) setStatus('');                     // keep "Preparing…" while an export runs
    if (errors.from || errors.to) {                           // re-check only errors already shown
      const again = validateExport(f, t, format);
      setErrors((x) => ({ ...x, from: x.from && again.from, to: x.to && again.to }));
    }
  }

  // Render order: <ErrorSummary/> <PageHeading>Export record</PageHeading> <dl/> <form noValidate onSubmit>
  //   <DateField export-from/> <DateField export-to min={isIsoDate(from) ? from : undefined}/> <fieldset Format/>
  //   <ChoiceRow checkbox/> <div role="alert"/> <p role="status" data-busy={busy || undefined}/> <Button type="submit" variant="primary"
  //   busy={busy} busyLabel="Exporting…" announceBusy={false}>Export</Button> </form>
}
```

```css
/* ExportRecordForm.module.css: tokens from foundations.md only */
.form    { display: grid; gap: var(--space-5); max-inline-size: var(--measure); }
.fixed dt { font-weight: 700; }
.fixed dd { margin: 0; font-size: var(--font-size-h2); font-weight: 700; }
.date    { inline-size: 100%; min-block-size: var(--target-button); }
@media (min-width: 40rem) { .date { max-inline-size: 14rem; } }
.actions { margin-block-start: var(--space-3); padding-block-end: var(--space-8); }
.alert, .status { margin: 0; overflow-wrap: anywhere; }
.alert:not(:empty)  { margin-block-end: var(--space-4); color: var(--colour-error); font-weight: 700; }
.status:not(:empty) { margin-block-end: var(--space-4); }
.status[data-busy]  { color: var(--colour-text-secondary); }
```

---

## States

The page title is "Grow2Notes – Export record" in every state except validation errors.

| State | When | What shows (exact copy) | Focus | Announced |
|---|---|---|---|---|
| **Loading** | Waiting for the participant or `firstNoteDate` | Nothing for 1 s, then "Loading participant…" **(P)** | Waits for the `<h1>` | "Loading participant…" (that region's status) |
| **Load failed** | Network or `5xx` after the shared single silent retry (empty-loading-error.md) | `<h1>` "Export record"; "The export form did not load: no connection. Try again." or "The export form did not load: something went wrong. Try again." **(P)**; button **Try again** | The fallback `<h1>` | The load region's status line |
| **Not found** | Unknown participant ID, or another organisation's (`404`, §9.2) | Shared "Page not found" page | Its `<h1>` | – |
| **Not permitted** | A worker opens the route | Shared "Page not found" page (role guard) | Its `<h1>` | – |
| **Default** | Data loaded | Participant name. From = earliest submitted note, To = today. No format chosen, box unticked. Export. Alert and status empty | `<h1>` "Export record" | "Export record, heading level 1" |
| **No submitted notes at all** | `firstNoteDate` is `null` | Same as Default, with From = To = today. Nothing extra is shown. Export then returns the no-notes result | `<h1>` | – |
| **Archived participant** | Participant is archived | Identical to Default (§3.7) | `<h1>` | – |
| **Validation error: From** | Export pressed with From empty or incomplete | Summary "There is a problem" with **"Enter the From date"** **(P)**, the same text above the From input | The summary | Summary heading and links (focus move) |
| **Validation error: To empty** | To empty or incomplete | **"Enter the To date"** **(P)** on To date and in the summary | The summary | As above |
| **Validation error: To before From** | Both complete and To < From | **"To date must be the same as or after 1 January 2026"** **(N)** on **To date** and in the summary | The summary | As above |
| **Validation error: Format** | No radio chosen | **"Choose PDF or Word"** **(P)** under the Format legend and in the summary | The summary | As above |
| **Several errors** | Any mix | One summary item per error, in page order (From, To, Format) | The summary | As above |
| **Busy, under 400 ms** | Request in flight | No visible change apart from the pressed state. Export is `aria-disabled` | Stays on Export | Nothing |
| **Busy, 400 ms or more** | Still in flight | Button **"Exporting…"** **(P)**. Status line **"Preparing the PDF file… Keep this page open."** or **"Preparing the Word file… Keep this page open."** **(P)**. Fields stay editable | Stays on Export | The preparing line (polite) |
| **Exported** | `200`, file handed to the browser | Status line **"participant-record_2026-01-01_to_2026-10-01.pdf is ready. Look in your downloads."** **(P)**, the same shape as the Daily report, using the server's file name from `Content-Disposition`. Form values unchanged. Next comes the browser's own download UI or prompt (iPhone Safari asks first, and for PDFs offers View) | Stays on Export | "Exported: participant-record…pdf" (polite) |
| **No notes in range** | `404 report.no_notes` (assumed code, see Open questions) | Status line **"No submitted notes for Jane Citizen between 1 January 2026 and 1 October 2026."** **(N: design wording, `datePlain` dates)**. Not red, not in the summary. Dates are those **sent**, not the current field values | Stays on Export | The sentence (polite) |
| **Failed: no connection** | `fetch` rejects, or the connection drops mid-file | Alert **"Not exported: no connection. Try again."** **(P)** | Stays on Export | The alert (assertive) |
| **Failed: server** | `5xx`, an unexpected `4xx`, or a `404` with no `report.no_notes` code | Alert **"Not exported: something went wrong. Try again."** **(P)** | Stays on Export | The alert |
| **Failed: rate limit** | `429` (10 exports a minute per user, shared with Daily report downloads, §9.9) | Alert **"Not exported: too many exports in the last minute. Wait a minute, then try again."** **(P)** | Stays on Export | The alert |
| **No longer a manager** | `403` (role changed mid-session, §6.6 "apply on the user's next request") | **No message.** `me` is refetched, and the role guard replaces the screen with the whole-page "Page not found", exactly as on the Daily report (one `403` rule app-wide, empty-loading-error.md) | Its `<h1>` | Through focus on "Page not found" |
| **Server field errors** | `422 validation.failed` | Mapped to fields by API key (`from`, `to`, `format`, `includeHistory`) and shown exactly like client errors | The summary | Summary |
| **Session ended** | `401` | No message here. The sign-in-in-place flow takes over. Values stay in memory. The export is **not** replayed after sign-in | Per session-timeout.md | Per session-timeout.md |
| **Left mid-export** | Back, a nav link, or closing the tab while busy | The request is aborted and no file appears later. The server may already have written the `participant.exported` audit row (export-form.md), which errs on the cautious side | – | – |
| **Date changed after a result** | Either date edited while not busy | The status line empties silently. The alert stays until the next press | Unchanged | Nothing |

---

## Interactions and focus

**Focus order** (Tab), at every width:
1. Skip link and app-shell header (app-shell-nav.md).
2. Error summary links, when shown.
3. From date. The browser handles moving inside its segments and opening its calendar; don't override it.
4. To date.
5. Format group: one tab stop, with arrow keys between PDF and Word.
6. Include earlier versions of edited notes.
7. Export.

The `<h1>` and the summary container are focus targets (`tabIndex={-1}`), not tab stops.

**On each action:**

| Action | What happens | Focus after |
|---|---|---|
| Arrive (from 4.4 or 4.8) | Data loads. The form renders with defaults. Nothing is validated and nothing is red. The defaults never change afterwards, even if `me.today` changes at midnight; only `max` follows `me.today` | `<h1>` "Export record" |
| Type or pick a date | Value stored as `YYYY-MM-DD`. No validation, unless that date group already shows an error. The status line clears if no export is running (Components 10) | Unchanged |
| Choose a format | Clears the Format error and its summary item, if shown. Nothing else (SC 3.2.2) | Unchanged |
| Tick or untick the box | Nothing else | Unchanged |
| Press Export, or Enter in a date field (implicit submission) | 1. If an export is running, ignore it. 2. Validate From, To, Format. On any error: inline messages and summary in one update, the `Error: ` title prefix, focus on the summary, nothing sent. 3. Clear the alert and the status. 4. `GET /api/reports/participants/{id}/export?from=&to=&includeHistory=&format=pdf\|docx` through the shared `api()` wrapper, with an `AbortSignal`. 5. Export goes `aria-disabled` at once. At 400 ms: "Exporting…" and the preparing line. 6. Handle the outcome (States) | Summary on a validation error. Otherwise Export |
| Select a summary link | Scroll the field's label or legend into view, then `focus({ preventScroll: true })` the input. No hash and no history entry | The field |
| Press Export again after a result | The same steps. Each press is a new audited export. **Nothing is ever retried automatically**: a retry is a second audit row, counts toward the `429` limit, and in Chrome a second download without a click triggers the "multiple downloads" prompt (file-download.md) [Standard: Chromium source] | Export |
| Leave while busy | `AbortController.abort()` on unmount. No file, no message | – |
| Back after an export | Returns to 4.4 or 4.8. Coming back to this screen starts fresh with defaults. Nothing is remembered (D22) | Per the destination |

**Saving the file:**
- `fetch`, then `blob()`, then `URL.createObjectURL()`.
- Click a temporary hidden `<a download="{name}" rel="noopener">` with **no** `target`. With `target="_blank"`, WebKit loads a blob instead of downloading it (WebKit bug 190351).
- Remove the anchor, and revoke the object URL after 60 s, not at once.
- The name comes from `Content-Disposition`, preferring `filename*`. If there is none, fall back to `participant-record_{from}_to_{to}.{pdf|docx}`.
- The Blob lives in JS memory only until it is handed to the browser. There is no IndexedDB, Cache Storage, service worker or `showSaveFilePicker()` (D22, §9.6). [Standard: MDN, HTML spec; Convention: FileSaver.js] (file-download.md)

**Live-region announcements:**

| Moment | Region | Politeness | Text |
|---|---|---|---|
| Arrival | Focus on `<h1>` | – | "Export record, heading level 1" |
| Slow load (over 1 s) | Load region `role="status"` | polite | "Loading participant…" |
| Failed Export (validation or `422`) | Focus on the summary | – | "There is a problem, heading level 2", then each link |
| Busy for 400 ms or more | `#export-status` | polite | "Preparing the PDF file… Keep this page open." |
| Exported | `#export-status` | polite | "participant-record_2026-01-01_to_2026-10-01.pdf is ready. Look in your downloads." |
| No notes | `#export-status` | polite | "No submitted notes for Jane Citizen between 1 January 2026 and 1 October 2026." |
| Request failed | `#export-alert` | assertive | "Not exported: …" |
| Result cleared by a date change | `#export-status` emptied | – | Nothing (an empty region is not announced) |

- Each result is written after the region has been emptied and one animation frame has passed. A repeated sentence, such as "no connection" twice, is then announced again (file-download.md).
- The shared Button does **not** also announce "Exporting…" in the page status region (`PageStatus`, app-shell.md component 3; `announceBusy={false}`), so nothing is read twice. The page status region is still rendered from the first render and carries the load region's Try again "Loading…" label.
- On an iPhone, VoiceOver then reads Safari's own download prompt. This is browser behaviour, and its exact wording is unverified.

---

## Accessibility checklist

**Headings and landmarks**
- [ ] One `<h1>` "Export record". The error summary's `<h2>` "There is a problem" appears only after a failed attempt. There are no other headings (SC 1.3.1, 2.4.6).
- [ ] Landmarks come from the shell only: header, nav, `<main>` and the skip link "Skip to main content". The `<form>` has no accessible name, so it is not an extra landmark.
- [ ] `<html lang="en-AU">` (SC 3.1.1).

**Labels and descriptions**
- [ ] Each date input has a visible `<label for>`: "From date", "To date" (SC 1.3.1, 2.4.6, 3.3.2).
- [ ] The radios are in `<fieldset>` + `<legend>Format</legend>`. Each radio's visible label is its whole accessible name ("PDF", "Word"), and its hint is linked with `aria-describedby` (SC 2.5.3).
- [ ] The checkbox label is exactly "Include earlier versions of edited notes", and its hint is linked with `aria-describedby`.
- [ ] The participant is a `<dl>` with text, not a disabled input.
- [ ] No `aria-label` replaces visible text anywhere. No `title` attributes, tooltips or placeholders (SC 2.5.3, 1.4.13).

**Errors**
- [ ] Errors appear only after Export, never on load, focus, blur or typing (SC 3.3.1). Each message says how to fix the problem (SC 3.3.3).
- [ ] Inline message above the input, with a visually hidden "Error: " prefix. `aria-invalid="true"` and `aria-describedby` on date inputs in error. The Format error is linked from the `<fieldset>`. No `aria-errormessage`, and no live region on inline errors (Roselli 2023) [Research] https://adrianroselli.com/2023/04/exposing-field-errors.html
- [ ] Error state shown by text, the 4 px bar and the border width, as well as colour (SC 1.4.1).
- [ ] Values are never cleared after an error, a result or a failure (SC 3.3.7).

**Keyboard**
- [ ] Every control is reachable and operable by keyboard, with no traps (SC 2.1.1, 2.1.2). Focus order matches the visual order (SC 2.4.3).
- [ ] The focus ring is visible on every stop: 3 px near-black outline, 2 px offset, `Highlight` in forced colours (SC 2.4.7; also meets 2.4.13 AAA).
- [ ] Nothing sticky can cover a focused element (SC 2.4.11).
- [ ] Focus never drops to `<body>`: busy uses `aria-disabled`, never `disabled`.

**Screen readers and status**
- [ ] `#export-alert` (`role="alert"`) and `#export-status` (`role="status"`) are in the DOM from first render. They are never conditionally mounted and never `display: none` (SC 4.1.3).
- [ ] No-notes, preparing and exported are status messages: no focus move, not red (SC 4.1.3).
- [ ] No message auto-dismisses (SC 2.2.1).

**Visual and input**
- [ ] Text contrast is at least 7:1 from foundations tokens. Control borders and the focus ring are at least 3:1 (SC 1.4.3, 1.4.11).
- [ ] Targets: date fields and Export at least 48 px, radio and checkbox rows at least 56 px, at least 8 px between separate targets (SC 2.5.8; A32).
- [ ] Reflow at 320 px, 200% text and 400% zoom with no sideways scroll. Long file names wrap. No fixed heights (SC 1.4.4, 1.4.10, 1.4.12). Both orientations work (SC 1.3.4).
- [ ] Forced colours: real borders on inputs, rows and the button. `GrayText` for `[aria-disabled="true"]`. The focus ring is an `outline`.
- [ ] No motion at all, so there is no spinner. The reduced-motion safety reset is in base CSS (foundations.md).

**Does not apply** (noted so nobody adds controls for them)
- **SC 3.3.4 Error Prevention.** Exporting does not "modify or delete user-controllable data", so no confirmation is needed [Standard] https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- **SC 1.3.5 Identify Input Purpose.** The dates are not information about the user.
- **SC 2.5.7 Dragging Movements.** Nothing is dragged.
- **SC 3.3.8 Accessible Authentication.** There is no sign-in on this screen.

**WCAG 2.2 AA criteria this screen meets:** 1.3.1, 1.3.2, 1.3.4, 1.4.1, 1.4.3, 1.4.4, 1.4.10, 1.4.11, 1.4.12, 1.4.13, 2.1.1, 2.1.2, 2.2.1, 2.4.2, 2.4.3, 2.4.6, 2.4.7, 2.4.11, 2.5.2, 2.5.3, 2.5.8, 3.1.1, 3.2.2, 3.2.4, 3.3.1, 3.3.2, 3.3.3, 3.3.7, 4.1.2, 4.1.3.

---

## Acceptance criteria

**Arrival and defaults**
- [ ] With `me.today = 2026-10-01` and an earliest submitted note on 2026-01-01, the form opens with From `2026-01-01`, To `2026-10-01`, no format selected and the box unticked.
- [ ] A participant whose only note is a draft (no submitted notes) opens with From = To = `2026-10-01`.
- [ ] Defaults are identical with the device clock and time zone set to anything else (for example `TZ=America/Los_Angeles` in Playwright). Nothing reads `new Date()` for a default.
- [ ] Both date inputs have `max="2026-10-01"`. To date has `min` equal to a complete From value.
- [ ] Focus lands on the `<h1>` "Export record". The document title is exactly "Grow2Notes – Export record". The URL contains no name.
- [ ] A worker who opens the route sees "Page not found". An archived participant's export screen is identical to an active one's, and its export succeeds.
- [ ] After leaving and returning, the form shows defaults again. Nothing is read from or written to `localStorage`, `sessionStorage` or IndexedDB (spy in a unit test).

**Validation**
- [ ] Pressing Export with no format chosen shows "Choose PDF or Word" under the legend and in a "There is a problem" summary. Focus moves to the summary. The title becomes "Error: Grow2Notes – Export record". **No network request is made.**
- [ ] With From `2026-01-01` and To `2025-12-31`, the To date error reads exactly "To date must be the same as or after 1 January 2026" (U+00A0 between "1" and "January"). The From field shows no error.
- [ ] Clearing either date gives "Enter the From date" or "Enter the To date". An incomplete date (value `""`) gives the same message.
- [ ] No error appears before the first press, on blur, or while typing.
- [ ] Fixing an error removes its message, its `aria-invalid` and its summary item at once. The summary and the title prefix go when the last error is fixed.
- [ ] Pressing Export again with the same errors moves focus to the summary again.
- [ ] Each summary link focuses its field: the first radio for Format. The URL hash does not change.
- [ ] A future To date with a valid range raises no client error. The request is sent.

**Export request and results**
- [ ] A valid press sends exactly one `GET /api/reports/participants/{id}/export` with `from`, `to`, `format` (`pdf` or `docx`) and `includeHistory` (`true` or `false`) matching the form.
- [ ] A double click, or Enter pressed twice, sends one request.
- [ ] Export never has the `disabled` attribute. While busy it has `aria-disabled="true"` and keeps focus.
- [ ] With a delayed response (mocked to 2 s): no label change before 400 ms. From 400 ms the button reads "Exporting…" without changing width, and the status line reads "Preparing the PDF file… Keep this page open." (or "Word").
- [ ] On `200`, the saved file's name equals the `Content-Disposition` file name. The status line reads "{that name} is ready. Look in your downloads." (the Daily report's shape). Focus is still on Export. From, To, Format and the box keep their values.
- [ ] An export mocked to take 25 s succeeds (the wrapper's `timeoutMs: 30_000`), showing "Exporting…" and the preparing line throughout, with no "no connection" alert.
- [ ] The download anchor has `rel="noopener"` and no `target`. The object URL is revoked about 60 s later.
- [ ] On `404` with `code: "report.no_notes"`, the status line reads "No submitted notes for Jane Citizen between 1 January 2026 and 1 October 2026." using the dates sent. It is not in error colour and not in the summary, and focus does not move.
- [ ] On a network failure, `5xx` and `429`, `#export-alert` shows the matching string from States. Focus stays on Export, and **exactly one** request was made (no automatic retry).
- [ ] On `403`, no alert appears: `me` is refetched and the screen becomes "Page not found".
- [ ] The Manage nav item has `aria-current="true"` on this screen.
- [ ] The Format radios are 40 px drawn circles in 56 px rows (the users.md ChoiceRow), visible in forced colours, with no `accent-color`.
- [ ] On `422` with `errors.to`, the message appears on To date and in the summary.
- [ ] On `401`, no export message appears and the sign-in-in-place flow starts. After signing in, the form still holds its values and no export request is replayed.
- [ ] Unmounting during a request aborts it. No file is saved and nothing is written to the status or alert line.
- [ ] Changing a date after a result empties the status line. Changing a date during a request keeps the preparing line.
- [ ] Two identical failures in a row are both announced (the region is emptied and re-written).

**Accessibility and layout**
- [ ] `#export-status` and `#export-alert` exist in the DOM on first render.
- [ ] Axe (`@axe-core/playwright`) reports no violations in the default, validation-error, exported and failed states.
- [ ] A keyboard-only run reaches every control in the order in "Interactions and focus", with a visible ring on each.
- [ ] At 320 px wide, and at 200% text, there is no horizontal scroll, including with a `.docx` file name in the status line.
- [ ] Measured target heights: date inputs and Export are at least 48 px, and radio and checkbox rows are at least 56 px.
- [ ] Forced-colours emulation: field borders, radio and checkbox states, the error bar and the focus ring are all visible.
- [ ] A copy test finds no "download" (except the shared ready line's "Look in your downloads."), "please", "sorry" or "invalid", and not the parent company's name (D42), in this screen's strings (microcopy.md `copy.test.ts`).

**Real devices** (test environment with the production CSP, before M5 sign-off; file-download.md, export-form.md)
- [ ] iPhone Safari, iPhone Chrome, Android Chrome and Samsung Internet each save (or offer to save) the file for a fast export **and** for one taking more than 10 s, after the click's user activation has expired. If one cannot, apply file-download.md's plain-link contingency to this screen only.
- [ ] The native date pickers open and set values on iOS and Android. Text is at least 16 px, so there is no zoom on focus.
- [ ] Each manager's laptop browser shows dates in day-month order (English (Australia) language). Record the result at go-live.
- [ ] A one-year export with earlier versions completes in under 30 s (M5), and the busy state holds steady the whole time.
- [ ] VoiceOver (iOS) and TalkBack read the preparing, exported, no-notes and failure lines.

---

## Conflicts resolved

Where component files disagreed, this is what the screen uses and why.

| # | Topic | Disagreement | Chosen for this screen | Why |
|---|---|---|---|---|
| 1 | When busy feedback appears | export-form.md and primary-actions.md say 400 ms. file-download.md says 1 s | **400 ms** for both the "Exporting…" label and the preparing line | The shared `Button` already uses 400 ms, so one timing app-wide. Both numbers are convention, not measurement (ui-build corpus notes 300 vs 400 ms are both unsourced). [Convention] |
| 2 | Spinner | file-download.md adds a spinner after 1 s. foundations.md and primary-actions.md say no spinners in v1 | **No spinner.** Words only | foundations.md owns motion ("no spinners, skeletons…"). Words carry the meaning (invariant I1), and there is no reduced-motion variant to build. [Convention + Opinion] |
| 3 | Preparing copy | export-form.md: "Preparing the file. Keep this page open." file-download.md: "Preparing the Word file… Keep this page open." | **"Preparing the PDF file… Keep this page open."** / **"…Word file…"** **(P)** | Naming the format confirms the manager's radio choice during a long wait. The ellipsis is allowed for something happening now (microcopy.md). "Keep this page open" reuses design §4.3 wording. [Opinion] |
| 4 | Success message | primary-actions.md: downloads get no in-page success. file-download.md: "{file} is ready. Look in your downloads." export-form.md: "Exported: {file}" | **"{file name} is ready. Look in your downloads."** **(P)**, the Daily report's line, word for word (editorial pass; this screen first chose "Exported: {file}") | Waits can reach 30 s (M5). Silence after that is ambiguous, and the browser's download UI is not reliably announced (unverified). It is one phrase with no full stop, as microcopy.md's success pattern asks. It avoids "download" (glossary) and "is ready", which is not true while iPhone Safari is still asking. [Opinion] |
| 5 | Where request failures go | export-form.md and primary-actions.md: an always-present `role="alert"` above the button. file-download.md: the single status line. form-validation.md: an unlinked item in the error summary | **`role="alert"` above Export** | It is not a field problem, so the summary (and its focus jump to the top) is wrong for it. Keeping failures out of the status line means a failure is announced assertively and never mistaken for a result. Focus stays on Export, so pressing again retries. [Convention + Standard 4.1.3] |
| 6 | Failure wording | file-download.md: "The file was not downloaded: no connection. Check your connection, then try again." / "Sorry, there was a problem making the file. Try again." / "Too many downloads in a short time…" | **"Not exported: no connection. Try again."**, **"Not exported: something went wrong. Try again."**, **"Not exported: too many exports in the last minute. Wait a minute, then try again."** **(P)** | microcopy.md's canonical "Not [done]: [cause]. Try again." "Sorry" is banned in new copy. "Download" is the Daily report's word. "Exports" matches §9.9's own name for the shared limit. [Convention: GOV.UK, microcopy.md §9] |
| 7 | Date format inside messages | export-form.md and form-validation.md examples use the weekday form ("Thursday 1 January 2026"). microcopy.md uses `datePlain` for ranges and the range error | **`datePlain`**: "between 1 January 2026 and 1 October 2026"; "…same as or after 1 January 2026" **(N)** | microcopy.md §9 settles this. It is shorter for a two-date sentence and still spells out the month, so a device showing mm/dd cannot mislead. [Convention: Style Manual] |
| 8 | Same-day range wording | export-form.md proposes "No submitted notes for Jane Citizen on {date}." | **Not built.** The design sentence is used for every range, including From = To | The brief allows wording changes only where microcopy.md asks for them, and it does not ask for this one. It is listed as an Open question for the owner. |
| 9 | Future To date | export-form.md asks whether to add "To date must be today or in the past". date-navigation.md says it is not an error | **No client error.** `max` is a picker hint only | The design specifies only the To-before-From error, and no note can exist after today. Server behaviour is an Open question. |
| 10 | Control sizes | export-form.md: date fields and radio/checkbox rows at 44 px. foundations.md and date-navigation.md: date fields 48 px, rows 56 px | **48 px date fields and button; 56 px rows** | foundations.md owns target sizes and names this screen's radios and checkbox explicitly ("same 56 px label rows as tick boxes"). [Research: Parhi 2006, via NN/g; Convention: Android 48 dp] |
| 11 | `color-scheme` | export-form.md: `color-scheme: light dark`. foundations.md: light only | **`color-scheme: light`** (global, without `only`) | foundations.md owns theming. It keeps the native date picker light and consistent with the page. [Research: NN/g dark-mode review; Opinion] |
| 12 | Busy announcement read twice | primary-actions.md: the shared `Button` announces its busy label in the page status region (`PageStatus`, app-shell.md; an earlier draft called it app-level). export-form.md and file-download.md: a local status line | **The local status line only.** The `Button` gets an `announceBusy={false}` option, as on the Daily report downloads and Guide prompts' Save | The local line also says "Keep this page open", which matters for a wait of up to 30 s. Two polite regions would read busy twice. [Standard 4.1.3 + Opinion] |
| 13 | Double-press guard | export-form.md: `mutation.isPending`. file-download.md: an `inFlight` ref | **`inFlight` ref**, with the busy state still driving `aria-disabled` | `isPending` updates only after a re-render, so a fast second tap or Enter can slip through. A ref blocks it at once. [Opinion, React rendering model] |
| 14 | Clearing the result on date change | export-form.md clears the status line on any date change, including while busy | **Cleared only when no export is running** | Clearing during a long export would remove the "Keep this page open" reassurance mid-wait. [Opinion] |
| 15 | Status line position | file-download.md: under the button(s). export-form.md and primary-actions.md: above the button | **Above Export** (alert, then status, then Export); the Daily report now uses the same order (editorial pass: an earlier draft claimed it already did, when its line sat under the buttons) | The line is in view whenever the button is, at the bottom of a long phone form. This matches date-navigation.md's Daily report, where the status line sits above the downloads. [Opinion] |
| 16 | Load-failed wording | empty-loading-error.md: "Could not load …" + a cause sentence. microcopy.md §9: "[Thing] did not load: [cause]. Try again." | **"The export form did not load: … Try again."** **(P)** | microcopy.md §9 is the wording authority for every screen and for the shared `LoadRegion` (settled app-wide in the editorial pass). |
| 17 | `403` handling | file-download.md: "Only managers can download these files." This file's earlier draft: "Not exported: only managers can export records." empty-loading-error.md and the Daily report: "Page not found" through the role guard | **No message: refetch `me`; the role guard shows "Page not found"** (editorial pass) | One `403` rule on both download screens and app-wide (empty-loading-error.md), matching the API's 404-not-403 stance (§9.2); the case only arises after a role change mid-session. |
| 18 | Token names | export-form.md uses `--focus-ring`, `--colour-error-text`, `--input-border`, `--text-lg` and others | **foundations.md names** via its alias table (`--colour-focus`, `--colour-error`, `--colour-border-control`, `--font-size-h2`) | One token set keeps the contrast test meaningful. |
| 19 | Format pre-selection | form-validation.md left it open. export-form.md: none | **None**, so "Choose PDF or Word" can occur | GOV.UK radios guidance (checked 1 October 2026). Neither format is right for most requests (§11.5, §11.6). |
| 20 | Request timeout | empty-loading-error.md's `api()` applies 10 s to every request; this file set none of its own | **`timeoutMs: 30_000`** through the wrapper's per-call option, the same as the Daily report | M5 allows exports of up to 30 s; at 10 s a slow but working export would read "Not exported: no connection." |
| 21 | Radio style | export-form.md: native radios with `accent-color`. users.md: a 40 px drawn circle that survives forced colours. note-form.md Conflicts #11: `accent-color` is not legible on Chrome Android or older Safari | **users.md's ChoiceRow radio** (40 px drawn circle, 56 px row) | One radio look app-wide (SC 3.2.4); the pre-selection rule (none here) is recorded in form-validation.md | Uses the failure pattern and the glossary word, then lets app-shell-nav.md's role guard take over. |

---

## Tensions with decisions

Each is recorded once. No change is proposed.

1. **Long synchronous export with no measure of progress** (§7.1: no background workers, synchronous generation; M5: a one-year export with earlier versions in under 30 s).
   - Nielsen: beyond 10 s, give "feedback indicating when the computer expects to be done". NN/g recommends percent-done indicators for waits of "10 or more seconds". [Convention, expert synthesis] https://www.nngroup.com/articles/response-times-3-important-limits/ · https://www.nngroup.com/articles/progress-indicators/
   - A single synchronous request cannot report progress.
   - Mitigated with the "Exporting…" label, the "Keep this page open." line and a persistent result line.
2. **Exported files on personal phones** (D21, §9.6, §12 APP 11.1 row).
   - On an iPhone, Safari's Downloads folder is reported to default to iCloud Drive (How-To Geek 2019; current default **unverified**). Chrome on iPhone offers saving straight to Google Drive (Google Chrome Help).
   - A participant's whole record could therefore land in a personal cloud account. The app cannot see or control this. §12 already puts exported files under the provider's records policy.
   - Sources: https://www.howtogeek.com/440633/how-to-download-files-using-safari-on-your-iphone-or-ipad/ · https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DiOS
3. **Not a decision conflict, recorded for completeness:** §4.12 does not name the date control. The native field is chosen to match the decided native field in §4.7. Its known assistive-technology gaps (Dragon, iOS VoiceOver, locale display) are listed under Component 6, with their mitigations.

---

## Open questions

1. **API gap: the From default.** Design §4.12 defaults From to "the participant's first note", but no endpoint in §6 returns that date. `GET /api/participants/{id}` returns `{id, givenName, familyName, status}`, and history pages newest-first, 30 at a time. Options for the owner and developer, none chosen here:
   - add the earliest **submitted** note date to an existing response;
   - restate the default;
   - page through the history endpoint, which is wasteful: about 13 requests for a year of notes.
2. **API gap: the no-notes response.** §6.5 and §6.9 define no response for a participant export with no submitted notes in the range. This spec assumes `404 report.no_notes`, the daily report's code. Confirm it in M5.
3. **Future To date.** The server's behaviour is undefined: reject with `422`, clamp to today, or accept. If it accepts, the file's title block prints a future end date.
4. **Same-day wording.** For From = To, should "No submitted notes for Jane Citizen between 1 October 2026 and 1 October 2026." become "…on Thursday 1 October 2026."? This is the owner's call. It is not built.
5. **Owner sign-off on every (P) string:** the busy label, the preparing and exported lines, the four failure lines, the load lines, the field errors, and the three hints.
   **Answered 9 October 2026 (D67):** approved as written; any of them can still be changed later in the copy module. A (P) string added after that date still needs the owner's approval.
6. **Ready line on both download screens.** Both screens now show "{file name} is ready. Look in your downloads." after a file is handed to the browser (primary-actions.md said downloads get no in-page success). Approve the line, or show nothing on both.
7. **Unverified on devices** (M5):
   - blob saves after user activation has expired (10–30 s) on iOS Safari and Chrome on iOS;
   - the production CSP's effect on `blob:` downloads;
   - managers' browser languages (day-month order);
   - how VoiceOver and TalkBack read the file name and Safari's download prompt.

---

## Sources

- design.md §2, §3.7, §3.9, §4.0, §4.4, §4.7, §4.8, §4.12, §4.13, §5.4, §6.3, §6.5, §6.9, §7.1, §9.2, §9.6, §9.9, §11.5, §11.6, §12, §13 (A19, A20, A30, A32, A33), §14 M5. decisions.md D5, D21, D22, D24, D26, D28, D30, D42, D43, D47.
- Component files: [export-form.md](../components/export-form.md) · [file-download.md](../components/file-download.md) · [date-navigation.md](../components/date-navigation.md) · [form-validation.md](../components/form-validation.md) · [foundations.md](../components/foundations.md) · [microcopy.md](../components/microcopy.md) · [primary-actions.md](../components/primary-actions.md) · [app-shell-nav.md](../components/app-shell-nav.md) · [empty-loading-error.md](../components/empty-loading-error.md) · [checkbox-list.md](../components/checkbox-list.md) · [session-timeout.md](../components/session-timeout.md)
- GOV.UK Design System, Radios (pre-selection, item hints, stacking on mobile; fetched 1 October 2026): https://design-system.service.gov.uk/components/radios/
- GOV.UK Design System, Date input: https://design-system.service.gov.uk/components/date-input/ · Button: https://design-system.service.gov.uk/components/button/ · Summary list: https://design-system.service.gov.uk/components/summary-list/ · Error summary: https://design-system.service.gov.uk/components/error-summary/
- NN/g: Date-input form fields https://www.nngroup.com/articles/date-input/ · Checkboxes vs radio buttons https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/ · Response times https://www.nngroup.com/articles/response-times-3-important-limits/ · Progress indicators https://www.nngroup.com/articles/progress-indicators/
- Hassell Inclusion, input type="date" AT testing (2019): https://hassellinclusion.com/blog/input-type-date-ready-for-use/
- WebKit bug 225639 (iOS ignores min/max): https://bugs.webkit.org/show_bug.cgi?id=225639 · WebKit bug 190351 (blob download with target=_blank): https://bugs.webkit.org/show_bug.cgi?id=190351
- Adrian Roselli, Exposing field errors (2023): https://adrianroselli.com/2023/04/exposing-field-errors.html
- MDN: `<input type="date">` https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date · `<a download>` https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a · Live regions https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions · Content-Disposition https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Disposition
- WCAG 2.2 Understanding: 4.1.3 https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · 3.3.4 https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html · 1.4.3 https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html · Technique H71 https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- Chromium `download_request_limiter.h` (multiple-downloads prompt): https://chromium.googlesource.com/chromium/+/HEAD/chrome/browser/download/download_request_limiter.h
- React `<form>` (action reset): https://react.dev/reference/react-dom/components/form · TanStack Query 5 mutations: https://tanstack.com/query/v5/docs/framework/react/guides/mutations
