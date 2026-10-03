# File download buttons (Word/PDF)

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. No spinner (foundations.md: no motion). Busy feedback after 400 ms, not 1 s: "Downloading…" on the pressed button and "Preparing the Word file… Keep this page open." (or PDF) in a status line **above** the buttons. Success line "{file} is ready. Look in your downloads." on both download screens. Downloads pass the `api()` wrapper's `timeoutMs: 30_000`.

Component key: `file-download`. This covers the controls that turn a manager's choice into a Word (.docx) or PDF file on their device: **Download Word** and **Download PDF** on the Daily report screen (design.md 4.7), and **Export** on the Participant record export screen (4.12). It applies the design as written (D26–D30, D43; §6.5, §7.1, §9.6, §11; A18–A20). It adds no screens, settings, notifications or data.

Evidence grades: **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA APG, HTML spec, MDN platform documentation, browser behaviour documented by the vendor · **[Convention]** established design systems and widely copied practice · **[Opinion]** reasoned judgement with no direct evidence.

---

## Where it's used

Both screens are for managers only (section 2). Workers never see either control: the Report item is not in their navigation, and both export endpoints return `403` to them.

| Screen (design.md) | Control | How the format is chosen | How "nothing to download" is known | Expected wait | File name (set by the server) |
|---|---|---|---|---|---|
| **4.7 Daily report** | Two buttons: **Download Word**, **Download PDF**, after Previous day / date field / Next day | One button per format | **Before** the click. `GET /api/reports/daily/{date}/has-notes` answers yes or no (A18). On "no", both buttons are disabled and the screen says "No submitted notes for Thursday 1 October 2026." | Usually well under 1 second ("Twenty notes render in well under a second", §7.1). Target: 20 notes in under 10 seconds (M4). | `daily-notes_2026-10-01.docx` / `.pdf` |
| **4.12 Participant record export** | One submit button, **Export**, at the end of a short form (participant fixed, From date, To date, Format, "Include earlier versions of edited notes") | A Format choice (PDF or Word) in the form | **After** the click. There is no pre-check endpoint, so "No submitted notes for Jane Citizen between … and …" is the result of pressing Export. The To-before-From check runs first (form-validation.md). | Can be long. Target: a one-year export with earlier versions in under 30 seconds (M5). | `participant-record_2026-01-01_to_2026-09-30.pdf` / `.docx` |

What is the same on both screens: a manager presses a button, the server builds the file fresh and in memory (§7.1, A19), every download is audited (§11.5, §11.6), the response is `Content-Disposition: attachment` with a dates-only file name (§6.5), exports are rate-limited to 10 a minute per user (§9.9), and the file is never shown inside the app (D43).

Not part of this component: the **Export record** entry on participant detail and history (4.4, 4.8). That moves the manager to the 4.12 screen, so it is an ordinary link (`<a href>`), not a download.

---

## Best practice

### Link or button

- **[Convention]** Adrian Roselli's widely cited rule: use `<a href>` if the user "is whisked to another URL", and a `<button>` if "the user is not moved from the page" but is presented with a new view or message. A download leaves the user on the same page, and the article does not single out downloads. https://adrianroselli.com/2016/01/links-buttons-submits-and-divs-oh-hell.html
- **[Standard]** MDN: "you should only use a hyperlink for navigation to a real URL". Fake links (`href="#"` plus a click handler) should be a `<button>` instead. https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a
- **[Standard]** The `download` attribute "only works for same-origin URLs, or the `blob:` and `data:` schemes". A `filename` in the `Content-Disposition` header "takes priority over a filename specified in the `download` attribute". Same page. The HTML spec says the same: the attribute's value "can be overridden by the `Content-Disposition` HTTP header's filename parameters". https://html.spec.whatwg.org/multipage/links.html#downloading-hyperlinks
- **[Convention]** Practice is split. A plain link to the file URL is the simplest build, and the browser's own download UI shows progress. But the page cannot see the response, so it cannot show its own "preparing" state or explain a failure in plain English. Fetching the file in script and saving it from memory gives the page both, at the cost of a little code and some browser quirks (below). Choose by what the screen must say during and after the wait. **[Opinion]**

### Telling people what they will get

- **[Convention]** NHS: links to documents carry the file type (and size) in the link text, for example "weight loss progress chart (PDF only, 545KB)". The PDF "should open in the same tab". "Avoid using links or buttons that open new tabs or windows." https://service-manual.nhs.uk/content/formatting
- **[Convention]** GOV.UK inline attachment links show "the file type and size … in brackets after the title". https://guidance.publishing.service.gov.uk/formatting-content/attachments
- Grow2Notes cannot know a file's size before the server builds it, so size cannot be shown honestly. The labels "Download Word" and "Download PDF" already name the type. **[Opinion]**

### Waiting

- **[Research]** Nielsen's limits (1993; expert judgement that has held up, not a measured threshold): under 0.1 s needs no feedback; up to 1 s "normally, no special feedback is necessary"; beyond 10 s users "should be given feedback indicating when the computer expects to be done". https://www.nngroup.com/articles/response-times-3-important-limits/
- **[Research]** NN/g (Sherwin, 2014): for anything under 1 second "it is distracting to use a looped animation". Looped indicators suit 2–10 seconds, and percent-done indicators suit 10 seconds or more. It cites a University of Nebraska–Lincoln study in which people who saw moving feedback "were willing to wait on average 3 times longer". https://www.nngroup.com/articles/progress-indicators/
- **[Convention]** GOV.UK buttons can prevent double clicks with `data-prevent-double-click`, and you "should also think about the issue server-side". https://design-system.service.gov.uk/components/button/
- **[Convention]** AgDS buttons have a `loading` state "to let users know their action is being processed", with a `loadingLabel` "to make it more contextual". https://design-system.agriculture.gov.au/components/button
- **[Standard]** A status message includes information "on the waiting state of an application, on the progress of a process, or on the existence of errors". It must be announced without moving focus (SC 4.1.3). https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

### Disabled state

- **[Convention]** GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible." https://design-system.service.gov.uk/components/button/
- **[Convention]** AgDS: "Avoid using disabled buttons". They "don't tell users why the content is unavailable", "are not keyboard accessible", and "can be hard for users with a visual impairment to see". https://design-system.agriculture.gov.au/components/button
- **[Standard]** WAI-ARIA APG: native `disabled` removes a control from the Tab order. "Screen reader users are far less likely to discover disabled elements that are not focusable". Where a disabled element "does need to remain discoverable", use `aria-disabled="true"` so that it stays focusable. https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- **[Convention]** Sandrina Pereira (CSS-Tricks, 2021): `aria-disabled` keeps the button focusable and still announced as disabled, but it does **not** block clicks, so the handler must check for it. https://css-tricks.com/making-disabled-buttons-more-inclusive/
- **[Standard]** SC 1.4.3 exempts text that is "part of an inactive user interface component" from contrast requirements. This is an exemption, not advice to make disabled text faint. https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html

### File names

- **[Standard]** Send both `filename` and `filename*`. `filename*` "is preferred over `filename` when both are understood". Avoid percent escapes in `filename`, because Safari does not decode them. https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Disposition
- **[Convention]** GOV.UK attachment file names should be "written entirely in lowercase", "use hyphens or underscores instead of spaces" and "make sense out of context", meaning when found later in a downloads folder. Leave out a date "unless the date is part of the document title". For a daily report, the date is what identifies the report. https://guidance.publishing.service.gov.uk/formatting-content/attachments/choose-attachment-type-name/

### Saving a file from script

- **[Standard]** `download` support: Safari on iOS 13 and later, desktop Safari 10.1+, Firefox 20+, Chrome and Edge. https://caniuse.com/download
- **[Standard]** Call `URL.revokeObjectURL()` to release an object URL. https://developer.mozilla.org/en-US/docs/Web/API/URL/createObjectURL_static
- **[Convention]** FileSaver.js, the long-standing library for this job, revokes the object URL after 40 seconds rather than at once. It sets `rel="noopener"`. It also has a special path for Chrome on iOS (`CriOS`). That code dates from about 2020, and whether current Chrome on iOS still needs it is **unverified**. https://github.com/eligrey/FileSaver.js/blob/master/src/FileSaver.js
- **[Standard]** WebKit bug 190351 (fixed 2018): a `blob:` download link with `target="_blank"` was *loaded* instead of downloaded ("WebKit tries to *load* the blob instead of *downloading* it"). Never put `target="_blank"` on a download anchor. https://bugs.webkit.org/show_bug.cgi?id=190351
- **[Standard]** (platform behaviour, from Chromium's source code, not a spec) Each tab may start one download freely. After that, Chrome asks "allow multiple downloads?" unless the user has since clicked, or pressed Enter or Space. So one download per button press never triggers the prompt. Two downloads from one press, or an automatic retry, can trigger it. https://chromium.googlesource.com/chromium/+/HEAD/chrome/browser/download/download_request_limiter.h
- **[Standard]** User activation lasts "at most a few seconds" (HTML spec). A file that takes 10–30 seconds to build is saved *after* the click's activation has expired. Chromium's limiter above does not depend on it. Whether iOS Safari saves a `blob:` file that late is **unverified**: test it on a device. https://html.spec.whatwg.org/multipage/interaction.html#transient-activation-duration
- **[Standard]** `showSaveFilePicker()` is "Experimental" and "not Baseline because it does not work in some of the most widely-used browsers". https://developer.mozilla.org/en-US/docs/Web/API/Window/showSaveFilePicker

### What phones do with the file

- **[Convention]** iPhone Safari (iOS 13 and later): tapping a file shows a prompt asking whether to download it. For files Safari can display, such as PDFs, the prompt also offers **View**. Downloads go to the Downloads folder in the Files app. By default that folder is **in iCloud Drive**. Settings › Apps › Safari › Downloads can change this to "On My iPhone". Sources: How-To Geek (2019); Apple support pages https://support.apple.com/en-lamr/102440 and https://support.apple.com/en-am/guide/iphone/iphb3100d149/ios. The settings page's wording was read from search results because the page did not render, so the current iOS default is **unverified**. https://www.howtogeek.com/440633/how-to-download-files-using-safari-on-your-iphone-or-ipad/
- **[Standard]** Chrome on Android saves to "your default download location". Files are found in the Files app or under Chrome's menu › Downloads. https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DAndroid
- **[Standard]** Chrome on iPhone saves to the Files app and also lets people save files "directly … to Google Drive". https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DiOS

### Errors and wording

- **[Convention]** GOV.UK problem pages: say "Sorry, there is a problem with the service" and "Try again later". Do not use "Technical jargon like 500 or bad request". https://design-system.service.gov.uk/patterns/problem-with-the-service-pages/
- **[Convention]** GOV.UK style: "Avoid negative contractions like can't and don't. Many users find them harder to read, or misread them as the opposite of what they say." This matters for managers who read English as a second language. https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

### React 19 specifics

- **[Standard]** For a `<form action={fn}>`, React says: "After the `action` function succeeds, all uncontrolled field elements in the form are reset." On the export form, this would wipe the dates and choices the manager just used. react.dev's own pending example also uses `disabled={pending}` on the submit button, which moves focus off the button while it waits. https://react.dev/reference/react-dom/components/form

---

## Recommendation for Grow2Notes

### Decision in one line

Each control is a native **`<button>`**. When pressed, it fetches the file through the app's shared `api()` wrapper (the one session-timeout.md defines). It then saves the file from memory with a temporary `<a download>` pointing at a same-origin `blob:` URL, and reports the result in one persistent `role="status"` line. **[Opinion, built on the sources above]**

Why a button and a fetch, and not a plain link to the export URL:
1. **Errors stay in the app.** With a link, a `401`, `429`, `500` or dropped connection is handled by the browser. It might show raw `problem+json` or a "Failed" entry in its downloads list. The design requires errors in plain English (I10; A32).
2. **Waits of up to 30 seconds** (M5) need a visible "preparing" state. Otherwise a tired manager taps again, creating a second audited export and moving toward the 10-a-minute limit (§9.9).
3. **"Disabled with a reason"** (4.7, A18) is a button state. A link without `href` is not a control at all.
4. **The participant export already needs script** for its To-before-From check (4.12) before anything is requested.

**Contingency.** If the M4 device test finds a browser that cannot save a `blob:` file, swap that screen's fetch for a plain same-origin link to the same export URL. The API already sends `Content-Disposition: attachment`, so the download works. The cost is that browser-native error handling replaces the plain-English messages below. Do not build both paths up front.

### Anatomy

```
Daily report (4.7)                               Participant record export (4.12)

[ Previous day ] [ 2026-10-01 ] [ Next day ]     ... From date / To date / Format / Include earlier versions
                                                 
[        Download Word        ]  <- button       [            Export            ]  <- submit button
[        Download PDF         ]  <- button       
                                                 
Status line (role="status", always in the DOM): Status line (role="status", always in the DOM)
  empty-day reason / Preparing… / ready / error  Preparing… / ready / no notes in range / error
```

- **Button.** A text label only, with no download icon (the word "Download" carries it, and the label names the format). ~~It has a reserved slot at the end for a small spinner~~ (struck: no spinner, foundations.md); the busy label "Downloading…" shares the label's grid cell, so the width never changes.
- **Status line.** One `<p role="status">` directly **above** the button(s), after the failure alert (editorial pass: the same position on both download screens; it was under them). It is always rendered, often empty, and its text changes in place. It is the only place this component writes words.
- No toast, no dialog, no file size, no note count. D26 forbids counts, which is why `has-notes` returns yes or no only.

### Behaviour

1. **Press.** The click handler checks a ref first (`inFlight`). If a request is already running, or the button is `aria-disabled`, it does nothing. This stops double taps before React re-renders, which `aria-disabled` alone cannot do, because it does not block clicks.
2. **Busy at once, visible after 400 ms** (editorial pass; was 1 s with a spinner). Both download buttons on the screen (or Export) get `aria-disabled="true"` immediately, so only one file is made at a time. If the response has not arrived after **400 ms**, the pressed button reads "Downloading…" (no spinner) and the status line reads "Preparing the Word file… Keep this page open." (or PDF). Most daily reports finish before then, so nothing flashes. **[Research: Nielsen 1 s; NN/g "under 1 second … distracting"]**
3. **Success.** Read the `Blob`, take the name from `Content-Disposition` (falling back to the name built from the same pattern), create an object URL, click a temporary hidden `<a download rel="noopener">` (no `target`), remove it, and revoke the URL after 60 seconds. The status line says the file is ready (copy below). The buttons are live again. Focus never moves; it stays on the button that was pressed.
4. **Nothing to download.** Daily report: the buttons are disabled with the reason before any press (below). If the export still answers `404 report.no_notes`, for example from a typed URL, write `{hasNotes: false}` into the query cache, and the empty state appears. Participant export: show the 4.12 "No submitted notes for …" sentence in the status line. It is a result, not an error (form-validation.md).
5. **Failure.** Show one plain-English sentence in the status line (copy below) and make the buttons live again. **Never retry automatically.** A retry would be a second audited export, could hit `429`, and in Chrome would be a second download without a click, which brings up the "multiple downloads" prompt.
6. **Signed out (`401`).** The shared wrapper's sign-in-in-place flow takes over (session-timeout.md). After signing in, the manager is back on the same screen, which keeps its date and form values in memory, and presses the button again. Like Submit note, a download is never replayed.
7. **Leaving or changing the date.** The daily-report download block is rendered with `key={date}`, so a new date starts clean. Unmounting aborts any request in flight (`AbortController`), and an old "ready" message never sits under a different date.
8. **Has-notes check (4.7 only).** The buttons are disabled only on a definite `hasNotes: false`. While the check is loading, or if it fails (for example, no connection), the buttons stay enabled, because the export endpoint has the final say and answers `404` when the day is empty. This avoids an enabled → disabled → enabled flicker on every Previous day / Next day press, since most days have notes. TanStack Query's default refetch on window focus keeps it current, for example after a worker submits today's first note. The SPA does no background polling (§6.8).

### States

| State | Daily report (4.7) | Participant export (4.12) | Semantics |
|---|---|---|---|
| **Default** | Two equal buttons, same style, Word first (design order) | One primary button, **Export** | `<button type="button">` / `<button type="submit">` |
| **Hover** (pointer devices only, `@media (hover: hover)`) | Background one step darker | Same | Hover is not shown on touch, so a tapped button does not stay hovered |
| **Focus** | 3 px solid outline, 2 px offset, at least 3:1 against the background, `:focus-visible` | Same | Native focus; never `box-shadow` alone |
| **Active** (pressed) | Background two steps darker while pressed | Same | No movement or scaling |
| **Disabled: day has no submitted notes** | Both buttons `aria-disabled="true"`: muted fill, **dashed** border, label text still at least 4.5:1. The status line reads the design string. Both buttons point at it with `aria-describedby`. | Not used. Export is never disabled for "no notes", because that is only known afterwards. | Focusable, announced as dimmed or unavailable, with the reason read on focus |
| **Loading: preparing the file** | The pressed button looks normal and shows a spinner after 1 s; the other button is `aria-disabled`. Status line: "Preparing the Word file…" / "Preparing the PDF file…" | Export is `aria-disabled` with the spinner after 1 s. Status line: "Preparing the Word file… Keep this page open." | `aria-disabled`, **never** `disabled` (keeps focus); text in the status line |
| **Ready** | Status line names the file | Same | Polite announcement; stays until the next press or date change; not timed |
| **Empty result** | (handled by Disabled) | Status line: the design's "No submitted notes for Jane Citizen between … and …" | Status, not error: no red, no focus move |
| **Error** | Status line in error colour with a warning icon *and* words | Same | Same status line; focus stays put |
| **Validation error** | – | To date before From date, empty dates: see form-validation.md. No request is sent. | Error summary and inline errors per that spec |
| **Read-only** | – (workers never reach the screen) | Participant is "fixed" (shown, not editable) | – |

### Exact copy

Strings from design.md are marked **design.md**. Everything else is **Proposed** and is not in design.md, and the owner may reword it. Proposed copy avoids negative contractions (GOV.UK style). Dates use the app's long form, "Thursday 1 October 2026".

| Where | String | Source |
|---|---|---|
| 4.7 buttons | **Download Word** · **Download PDF** | design.md 4.7 |
| 4.7 empty day | "No submitted notes for Thursday 1 October 2026." | design.md 4.7 |
| 4.12 button | **Export** | design.md 4.12 |
| 4.12 empty range | "No submitted notes for Jane Citizen between Thursday 1 January 2026 and Wednesday 30 September 2026." | design.md 4.12 (dates filled in) |
| Preparing (shown after 1 s) | "Preparing the Word file…" · "Preparing the PDF file…" | Proposed |
| Preparing, 4.12 only | Add a second sentence: "Keep this page open." | Proposed, reusing the design's "Keep this page open" from the save indicator (4.3) |
| Ready | "daily-notes_2026-10-01.docx is ready. Look in your downloads." (always the real file name) | Proposed |
| No connection | "The file was not downloaded: no connection. Check your connection, then try again." | Proposed, echoing "Not saved: no connection." (4.3) |
| `429` | "Too many downloads in a short time. Wait a minute, then try again." | Proposed |
| `403` (role changed mid-session) | "Only managers can download these files." | Proposed |
| `5xx` or anything else | "Sorry, there was a problem making the file. Try again." | Proposed, after GOV.UK |
| `401` | No message here; the sign-in-in-place flow shows its own (session-timeout.md) | – |

Formatting the dates: `Intl.DateTimeFormat('en-AU', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })` puts a comma after the weekday ("Thursday, 1 October 2026"). Build the string from `formatToParts()` to match the design's "Thursday 1 October 2026". Format a `YYYY-MM-DD` calendar date with `timeZone: 'UTC'` from `Date.UTC(y, m - 1, d)`, so no device time zone can shift it by a day.

### Phone vs laptop

- **Phone (single column, 4.0).** Buttons are full width, stacked Word then PDF, with a 12 px gap. Each is at least 48 px tall, above the 44 px floor (A32) and well above SC 2.5.8's 24 px. The status line sits under them, full width. Long file names wrap with `overflow-wrap: anywhere`. Otherwise `participant-record_2026-01-01_to_2026-09-30.docx`, which has no spaces, would force sideways scrolling at 320 px or 200% text (SC 1.4.10).
- **Laptop.** Buttons sit side by side at their natural width, with a shared minimum width so they line up. Export keeps its natural width at the end of the form.
- **What happens next is the browser's job, and it differs.** iPhone Safari shows its download prompt (with **View** for PDF). Android Chrome saves to Downloads. Desktop browsers show their download panel. The "ready" message is worded so that it is true on all of them.

### Accessibility

**Semantics.** Native `<button>`s with visible text as their only name. The only ARIA is `aria-disabled` (empty day; busy) and `aria-describedby` from both 4.7 buttons to the status line while the day is empty. The status line is a `<p role="status">` (implicitly `aria-live="polite"`) that exists from first render. Its text is changed, never mounted on demand, because a live region created at the moment of writing is often not announced.

**Keyboard.** The Tab order follows the visual order: Previous day → date field → Next day → Download Word → Download PDF. Enter and Space activate (native). On 4.12, Enter in a date field submits the form (implicit submission). The submit handler checks `inFlight` itself, because `aria-disabled` does not stop implicit submission. Focus is never moved by this component.

**Screen reader (expected; wording varies by reader):**
- Tabbing to a button on an empty day: "Download Word, dimmed, button. No submitted notes for Thursday 1 October 2026." (VoiceOver says "dimmed"; NVDA says "unavailable".)
- Changing to an empty day with Previous day: the status line announces "No submitted notes for Wednesday 30 September 2026." Focus stays on Previous day.
- Pressing Download Word on a slow request: "Preparing the Word file…", then "daily-notes_2026-10-01.docx is ready. Look in your downloads." On iPhone, VoiceOver then reads Safari's own download prompt.
- A repeated identical message (for example, two connection failures in a row) may not be re-read. Clear the line, then write the new text on the next animation frame.

**WCAG 2.2 criteria met.** 1.3.1 Info and Relationships · 1.4.1 Use of Color (disabled = dashed border + words; error = icon + words) · 1.4.3 Contrast (kept at 4.5:1 even for disabled labels, beyond the exemption) · 1.4.10 Reflow (wrapping file names) · 1.4.11 Non-text Contrast (button borders and focus at 3:1) · 2.1.1 Keyboard · 2.2.1 Timing Adjustable (no message times out) · 2.4.3 Focus Order · 2.4.6 Headings and Labels ("Download Word" says what and in which format) · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) (the header is not sticky, per app-shell-nav.md) · 2.5.3 Label in Name (name = visible text, so "Tap Download Word" works in Voice Control) · 2.5.8 Target Size (Minimum), exceeded at 48 px · 3.3.1 Error Identification (errors in words) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

**Forced colours.** `[aria-disabled="true"]` uses `GrayText` for text and border. The focus outline is a real `outline`, so it survives. The spinner uses `currentColor` borders, not a background.

**Reduced motion.** The spinner's rotation becomes a slow opacity pulse under `prefers-reduced-motion: reduce`. The words in the status line carry the meaning in both cases.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria.** A native `<button>` covers this. Nothing here needs a custom widget.
- **No form action on 4.12.** Use `<form noValidate onSubmit={…}>` with `event.preventDefault()`, not `<form action={fn}>`. React 19 resets uncontrolled fields after a successful action, which would clear From, To and Format after every export. Don't copy react.dev's `disabled={pending}` either.
- **One helper, two screens.** `saveResponseAsFile()` below does the fetch, the error mapping and the save. The Daily report and the Export form only build the URL and the fallback name.
- **Use the shared `api()` wrapper**, not bare `fetch`. It already sends cookies (same origin), records the request for the idle clock, and turns `401` into the sign-in-in-place flow (session-timeout.md). The export is a `GET`, so no antiforgery header is needed (§9.8). With openapi-fetch, use `parseAs: 'blob'`, or call the wrapper's raw-`Response` form.
- **Nothing is stored on the device by the app.** The file exists in JS memory as a `Blob` only until it is handed to the browser's download, and the object URL is revoked after 60 seconds. No IndexedDB, no Cache Storage, no service worker, no `showSaveFilePicker()` (D22, §9.6). `Cache-Control: no-store` on `/api` (§6.1) keeps the browser from caching the response.
- **CSP.** The production policy (§9.7) does not apply under the Vite dev server. Test the `blob:` save in the **test environment**, where the real CSP is sent. Never fall back to a `data:` URL: top-level `data:` navigation is blocked by browsers, and `assetsInlineLimit: 0` exists precisely to keep `data:` out (§7.2).
- **The server owns the file name.** Use ASP.NET Core's `Results.File(stream, contentType, fileDownloadName)`, which sends `filename` and `filename*` (MDN). The client reads the header (same origin, so no `Access-Control-Expose-Headers` is needed) and falls back to the same pattern.

```ts
// src/api/saveResponseAsFile.ts
import { apiFetch } from './api'; // shared wrapper: activity clock + 401 -> sign-in in place

export type SaveResult =
  | { kind: 'saved'; fileName: string }
  | { kind: 'noNotes' }
  | { kind: 'signedOut' }
  | { kind: 'error'; message: string };

const COPY = {
  offline: 'The file was not downloaded: no connection. Check your connection, then try again.',
  tooMany: 'Too many downloads in a short time. Wait a minute, then try again.',
  notManager: 'Only managers can download these files.',
  problem: 'Sorry, there was a problem making the file. Try again.',
} as const;

export async function saveResponseAsFile(
  url: string, fallbackName: string, signal: AbortSignal,
): Promise<SaveResult> {
  let blob: Blob;
  let fileName: string;
  try {
    const res = await apiFetch(url, { signal });
    if (res.status === 401) return { kind: 'signedOut' };
    if (res.status === 404) {
      const p = await res.json().catch(() => null);
      return p?.code === 'report.no_notes' ? { kind: 'noNotes' } : { kind: 'error', message: COPY.problem };
    }
    if (res.status === 429) return { kind: 'error', message: COPY.tooMany };
    if (res.status === 403) return { kind: 'error', message: COPY.notManager };
    if (!res.ok) return { kind: 'error', message: COPY.problem };
    fileName = nameFrom(res.headers.get('Content-Disposition')) ?? fallbackName;
    blob = await res.blob();                   // a connection drop mid-file lands in catch too
  } catch (e) {
    if (signal.aborted) throw e;               // left the screen: say nothing
    return { kind: 'error', message: COPY.offline };
  }
  const href = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = href;
  a.download = fileName;
  a.rel = 'noopener';                          // never target="_blank" (WebKit 190351)
  a.hidden = true;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(href), 60_000);
  return { kind: 'saved', fileName };
}

function nameFrom(cd: string | null): string | null {
  if (!cd) return null;
  const star = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(cd);
  if (star) return decodeURIComponent(star[1]);
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(cd);
  return plain?.[1] ?? null;
}
```

```tsx
// src/features/reports/DailyReportDownloads.tsx  (rendered as <DailyReportDownloads key={date} date={date} />)
import { useEffect, useId, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { saveResponseAsFile } from '../../api/saveResponseAsFile';
import { getHasNotes } from '../../api/reports';
import { longDate } from '../../lib/dates';       // "Thursday 1 October 2026"
import { WarningIcon } from '../../components/icons'; // inline SVG, decorative
import styles from './DownloadButtons.module.css';

type Format = 'docx' | 'pdf';
const NAME: Record<Format, string> = { docx: 'Word', pdf: 'PDF' };
type Status =
  | { kind: 'idle' }
  | { kind: 'busy'; format: Format; slow: boolean }
  | { kind: 'ready'; fileName: string }
  | { kind: 'error'; message: string };

export function DailyReportDownloads({ date }: { date: string }) {
  const statusId = useId();
  const qc = useQueryClient();
  const key = ['reports', 'daily', date, 'has-notes'];
  const { data } = useQuery({ queryKey: key, queryFn: ({ signal }) => getHasNotes(date, signal) });
  const empty = data?.hasNotes === false;     // enabled while loading or if the check fails
  const [status, setStatus] = useState<Status>({ kind: 'idle' });
  const inFlight = useRef<AbortController | null>(null);
  useEffect(() => () => inFlight.current?.abort(), []);

  async function download(format: Format) {
    if (empty || inFlight.current) return;    // aria-disabled does not block clicks
    const ac = (inFlight.current = new AbortController());
    setStatus({ kind: 'busy', format, slow: false });
    const t = setTimeout(() => setStatus({ kind: 'busy', format, slow: true }), 400);   // the app's page-button delay
    try {
      const r = await saveResponseAsFile(
        `/api/reports/daily/${date}/export?format=${format}`, `daily-notes_${date}.${format}`,
        { signal: ac.signal, timeoutMs: 30_000 });   // api() per-call timeout (empty-loading-error.md)
      if (r.kind === 'saved') setStatus({ kind: 'ready', fileName: r.fileName });
      else if (r.kind === 'error') setStatus({ kind: 'error', message: r.message });
      else {
        if (r.kind === 'noNotes') qc.setQueryData(key, { date, hasNotes: false });
        setStatus({ kind: 'idle' });
      }
    } catch { /* aborted */ } finally {
      clearTimeout(t);
      inFlight.current = null;
    }
  }

  const busy = status.kind === 'busy';
  const message = empty ? `No submitted notes for ${longDate(date)}.`
    : status.kind === 'busy' && status.slow ? `Preparing the ${NAME[status.format]} file…`
    : status.kind === 'ready' ? `${status.fileName} is ready. Look in your downloads.`
    : status.kind === 'error' ? status.message : '';

  return (
    <div className={styles.block}>
      <div className={styles.buttons}>
        {(['docx', 'pdf'] as const).map((f) => {
          const mine = busy && status.format === f;
          return (
            <button key={f} type="button" className={styles.button}
              aria-disabled={empty || busy || undefined}
              data-busy={mine || undefined}
              aria-describedby={empty ? statusId : undefined}
              onClick={() => download(f)}>
              Download {NAME[f]}
              {/* no spinner (editorial pass): the shared Button shows "Downloading…" after 400 ms */}
            </button>
          );
        })}
      </div>
      <p id={statusId} role="status" className={styles.status}
         data-tone={status.kind === 'error' && !empty ? 'error' : undefined}>
        {status.kind === 'error' && !empty && <WarningIcon aria-hidden="true" />}
        {message}
      </p>
    </div>
  );
}
```

```css
/* DownloadButtons.module.css (token names are placeholders for the app's own) */
.buttons { display: flex; flex-direction: column; gap: 12px; }
@media (min-width: 40rem) { .buttons { flex-direction: row; } .button { min-inline-size: 12rem; } }

.button {
  font: inherit; font-weight: 600; min-block-size: 48px; padding-inline: 1rem;
  display: inline-flex; align-items: center; justify-content: center; gap: .5rem;
  color: var(--on-primary); background: var(--primary); border: 2px solid var(--primary); border-radius: 6px;
}
@media (hover: hover) { .button:hover:not([aria-disabled="true"]) { background: var(--primary-hover); } }
.button:active:not([aria-disabled="true"]) { background: var(--primary-active); }
.button:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
/* Dimmed, but the label stays >= 4.5:1; dashed border is the non-colour cue. Busy button keeps its normal look. */
.button[aria-disabled="true"]:not([data-busy]) {
  color: var(--text-muted-strong); background: var(--surface-muted); border-style: dashed; border-color: var(--border-strong);
}

/* Spinner rules struck in the editorial pass: no motion in the app (foundations.md). The shared Button's busy label
   "Downloading…" replaces it (primary-actions.md). */

.status { margin-block: .75rem 0; min-block-size: 1.5em; overflow-wrap: anywhere; }
.status[data-tone="error"] { color: var(--error-text); font-weight: 600; }
/* The warning icon is an inline <svg aria-hidden="true"> rendered before the text when tone is error.
   Not CSS ::before content: generated text such as "!" is read out by screen readers. */

@media (forced-colors: active) {
  .button[aria-disabled="true"]:not([data-busy]) { color: GrayText; border-color: GrayText; }
}
```

The 4.12 Export button uses the same CSS and helper. Its URL is `/api/reports/participants/{participantId}/export?from=…&to=…&includeHistory=…&format=…`, its fallback name is `participant-record_${from}_to_${to}.${format}`, and a `noNotes` result writes the design's "No submitted notes for Jane Citizen between … and …" into its status line.

**Tests to add** (they fit M4, M5 and M6 as written):
- Vitest + Testing Library: a double click sends one request; `404 report.no_notes` gives the empty state; `429`, `500` and a network error give the right sentence; an abort on unmount writes nothing; the status `<p>` exists before any message.
- Playwright: Chromium, Firefox and WebKit save a file whose name matches the header (`page.waitForEvent('download')`).
- **On real devices, in the test environment (real CSP):** iPhone Safari, iPhone Chrome, Android Chrome and Samsung Internet. Check a fast daily report, and a participant export that takes more than 10 seconds (the late-save case). This is the unverified part of this spec, and it decides whether the contingency link is needed. VoiceOver and TalkBack on the Report screen are already in M6.

---

## Per-screen notes

### 4.7 Daily report

- **Order:** Download Word, then Download PDF, as the design lists them. Both use the same style. They are alternatives, not a primary and a secondary. Word is the accessible version (§11.5).
- **Empty day:** both buttons `aria-disabled`, with the design's sentence under them, linked by `aria-describedby`. This applies to future dates too (only reachable by typing a URL, because Next day is disabled on today, 4.7): `has-notes` returns `false` and the export returns `404 report.no_notes` (§6.9, M4).
- **Re-downloading** the same day is normal. The file is rebuilt with a new "As at" stamp (§11.2). The browser adds its own suffix, such as "(1)", to a repeated name. Nothing in the app tracks or warns about earlier downloads.
- **Rate limit.** A manager catching up on a week (7 days × 2 formats = 14 files) can reach the 10-a-minute limit (§9.9). The `429` sentence handles it, and no automatic retry is made.
- **No count, no preview.** Do not show "12 notes" or any summary next to the buttons (D26, A18), and do not render any part of the report on screen (D43). iPhone Safari's own **View** option in its download prompt is the browser, not the app.
- The date controls (Previous day, date field, Next day disabled on today) belong to the date-navigation spec. This component only re-keys on `date`.

### 4.12 Participant record export

- **Export is a submit button** in a `<form noValidate>`. It runs the form-validation.md checks (empty dates, To before From) **before** any request. If they fail, no file is requested and no audit entry is written.
- **Not disabled before submit**, because there is no pre-check endpoint. The empty result appears in the status line after Export.
- **Longer waits:** show the 1-second "Preparing the Word file… Keep this page open." A percent-done bar is not possible, because the server builds the file in one synchronous request (§7.1). Leaving the page aborts the request. No cancel button is added; the design does not have one.
- **The form keeps its values** after an export (no React form action), so a manager who picked PDF by mistake changes the format and presses Export again.
- **Archived participants** can still be exported (3.7: "History, reports and exports are unchanged").
- **Same file name for different people.** `participant-record_2026-01-01_to_2026-09-30.pdf` is identical for two participants with the same range (dates only, by design, A19). The browser adds "(1)". The title block names the participant (§11.6), and the manager reads the copy before handing it over (§11.6).
- **Format choice:** keep the design's control (PDF or Word, as radios in a fieldset). Pre-selection and its "Choose PDF or Word" message are owned by form-validation.md. Note for the manager's workflow: redaction is done "on the exported Word copy" (§11.6).

---

## Anti-patterns to avoid

- **`<a href="#" onClick>`** or a `<div>` as a download control. Use a `<button>`.
- **`disabled` on a busy button.** It drops keyboard focus to `<body>`. Use `aria-disabled` plus the `inFlight` guard. react.dev's `disabled={pending}` example does exactly this.
- **Greyed-out buttons with no reason**, or hiding the buttons on an empty day. The design wants them visible, disabled, and explained.
- **A spinner of any kind** (editorial pass: no motion in the app; words carry the wait).
- **`{msg && <div role="status">}`**: a live region mounted at the moment of writing is often not announced. Keep it in the DOM.
- **Auto-retrying a failed download**, or starting both formats from one press. Each one is a second audited export, counts toward `429`, and triggers Chrome's "multiple downloads" prompt.
- **`target="_blank"`** on the download anchor, or opening the file in a new tab (WebKit 190351; NHS "avoid … new tabs"; D43).
- **Revoking the object URL in the same tick as `click()`.** Keep FileSaver.js's delayed revoke.
- **`window.location = exportUrl`, a hidden `<iframe>`, or a `data:` URL** as the main path. Errors land outside the app, and `data:` conflicts with the CSP's intent (§7.2, §9.7).
- **`showSaveFilePicker()`, a service worker, IndexedDB or Cache Storage** to hold or "resume" files (D22, §9.6).
- **React 19 `<form action>`** on the export form, which resets the fields after success.
- **Names in URLs or file names**, such as `?participant=Jane` or `jane-citizen.pdf` (4.0, §6.1, A19).
- **Toasts that disappear** with the result or the error (SC 2.2.1). The status line stays until the next press.
- **Counts, file sizes or estimates** ("about 2 MB", "12 notes"). Counts are out (D26), and sizes are unknown until the file is built.
- **Background polling** of `has-notes` (§6.8). Refetch on load and focus only.

---

## Tensions with decisions

- **A18 / 4.7 disable both buttons on an empty day.** GOV.UK says to "avoid them if possible", and AgDS says "Avoid using disabled buttons" because they hide the reason, drop out of keyboard reach and are hard to see. The build above answers all three: the reason is printed and linked, the buttons stay focusable (`aria-disabled`), and the labels stay at 4.5:1. The design's choice stands.
- **Long synchronous exports (§7.1, M5 "under 30 seconds") vs percent-done feedback.** NN/g recommends percent-done indicators for 10 seconds or more, and Nielsen asks for feedback on "when the computer expects to be done". A single synchronous request cannot report progress, so the screen can only say "Preparing…" with a looping indicator. Accepted as designed.
- **Files on personal phones (D21, D22, §9.6, §12).** The design accepts that exported files land on managers' devices, and it puts them under the provider's records policy, outside the app (§12, APP 11.1 row). On an iPhone, Safari's default download folder is reported to be **in iCloud Drive** (How-To Geek 2019; current default unverified). Chrome on iPhone offers saving straight to Google Drive. Either moves a copy of health information into a personal cloud account. The app cannot see or control this. No app change is suggested; this is for the provider's own policy.

---

## Sources

- Adrian Roselli, "Links, Buttons, Submits, and Divs, Oh Hell" (2016): https://adrianroselli.com/2016/01/links-buttons-submits-and-divs-oh-hell.html
- MDN, `<a>` element (download attribute, fake buttons): https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a
- MDN, Content-Disposition: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Disposition
- MDN, `URL.createObjectURL()`: https://developer.mozilla.org/en-US/docs/Web/API/URL/createObjectURL_static
- MDN, `showSaveFilePicker()`: https://developer.mozilla.org/en-US/docs/Web/API/Window/showSaveFilePicker
- HTML Standard, downloading resources: https://html.spec.whatwg.org/multipage/links.html#downloading-hyperlinks
- HTML Standard, transient activation duration: https://html.spec.whatwg.org/multipage/interaction.html#transient-activation-duration
- Can I use, download attribute: https://caniuse.com/download
- WebKit bug 190351 (blob download with `target=_blank`): https://bugs.webkit.org/show_bug.cgi?id=190351
- Chromium, `download_request_limiter.h`: https://chromium.googlesource.com/chromium/+/HEAD/chrome/browser/download/download_request_limiter.h
- FileSaver.js source: https://github.com/eligrey/FileSaver.js/blob/master/src/FileSaver.js
- GOV.UK Design System, Button: https://design-system.service.gov.uk/components/button/
- GOV.UK Design System, Radios: https://design-system.service.gov.uk/components/radios/
- GOV.UK Design System, Problem with the service pages: https://design-system.service.gov.uk/patterns/problem-with-the-service-pages/
- GOV.UK publishing guidance, Attachments: https://guidance.publishing.service.gov.uk/formatting-content/attachments
- GOV.UK publishing guidance, Choose attachment type and name: https://guidance.publishing.service.gov.uk/formatting-content/attachments/choose-attachment-type-name/
- GOV.UK A to Z style guide (contractions): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- NHS digital service manual, Formatting (links, PDFs, new tabs): https://service-manual.nhs.uk/content/formatting
- NHS digital service manual, PDFs: https://service-manual.nhs.uk/content/pdfs
- Australian Government Design System (AgDS), Button: https://design-system.agriculture.gov.au/components/button
- WAI-ARIA APG, Keyboard interface: focusability of disabled controls: https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- Sandrina Pereira, "Making Disabled Buttons More Inclusive", CSS-Tricks (2021): https://css-tricks.com/making-disabled-buttons-more-inclusive/
- WCAG 2.2 Understanding: 1.4.3 Contrast (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html · 4.1.3 Status Messages https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · 2.5.8 Target Size (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html · 2.5.3 Label in Name https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html · 1.4.10 Reflow https://www.w3.org/WAI/WCAG22/Understanding/reflow.html · 2.2.1 Timing Adjustable https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- Jakob Nielsen, "Response Times: The 3 Important Limits" (1993): https://www.nngroup.com/articles/response-times-3-important-limits/
- Katie Sherwin, "Progress Indicators Make a Slow System Less Insufferable", NN/g (2014): https://www.nngroup.com/articles/progress-indicators/
- React, `<form>` reference (action reset, `useFormStatus`): https://react.dev/reference/react-dom/components/form
- Apple Support, where to find downloads on iPhone or iPad: https://support.apple.com/en-lamr/102440 · Customise Safari settings on iPhone: https://support.apple.com/en-am/guide/iphone/iphb3100d149/ios
- How-To Geek, "How to Download Files Using Safari on Your iPhone or iPad" (2019): https://www.howtogeek.com/440633/how-to-download-files-using-safari-on-your-iphone-or-ipad/
- Google Chrome Help, Download a file (Android): https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DAndroid · (iPhone and iPad): https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DiOS
- Sibling specs in this folder: `form-validation.md` (4.12 checks and status line), `session-timeout.md` (shared `api()` wrapper, `401` handling), `app-shell-nav.md` (Report link and non-sticky header), `confirm-dialog.md` (Download has no confirmation)
