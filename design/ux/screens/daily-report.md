# Daily report

Screen spec for design.md §4.7 ("Daily report (managers)"). It composes the researched component specs in
`../components/` into one screen and settles where they disagree. It adds no feature, field, setting, notification,
count or data item: everything on this screen comes from design.md §4.7, §3.3, §6.5, §6.9, §7.1, §11.4 and A18 (the
file's content follows §11.3 and D47).

**Copy marks used in this file.** **(V)** design.md's words, verbatim. **(N)** design.md's words with only the date
format or punctuation rule from `microcopy.md` applied. **(P)** proposed copy for a state design.md implies but does not
word; the owner should approve it (see Open questions).

**Evidence grades.** [Research] studies, usability or assistive-technology testing · [Standard] WCAG 2.2, WAI-ARIA,
HTML, MDN, framework documentation · [Convention] established design systems · [Opinion] reasoned judgement. The full
evidence sits in each component file; only the load-bearing citations are repeated here.

---

## Purpose and who uses it

**Purpose (design.md §4.7, V):** "pick a day and download that day's notes as one Word (.docx) or PDF file". "The
report is never shown on screen" (D43). **Primary action:** Download Word / Download PDF.

| Who | Device and context | What they need from this screen |
|---|---|---|
| **Managers only** (a few, equal, D24; §2: "Download the daily report (Word or PDF)" is manager-only) | Laptops and their own phones (D5, D21). Typical uses: the day's notes at the end of the day or the next morning, spot-checks (D17, A16), filing, catching up on a missed week. | Be sure which day is chosen, move a day at a time or pick a date, and get the file in the format they want, first time. Know plainly when a day has nothing to download. |
| **Workers** | Never. There is no Report item in their navigation (§4.0); the route shows "Page not found" (app-shell-nav.md role guard) and the API returns `403`. | – |

What the screen deliberately does **not** do: show any part of the report, a preview or a "view" option (D43); show a
count of notes, drafts or anything else (D26, A18); email or share (D28, D30); offer a date range or a week view (D27);
add a "Today" button, a remembered last date, a calendar that marks days with notes (D25), file sizes, or hints about
which format to choose. The file name and the file's content are set by the server (§6.5, §11). For common items,
each note in the file shows the Every note group (left out when it has no items) and the groups picked on that note,
each under its group name (a Heading 3 in Word) in the configured order, with every item in those groups ticked or
not ticked; groups not picked do not appear, and "No common items set" stands in when no group is shown (D47, §11.3,
A4). Nothing on this screen changes because of this: no group appears, is counted or is chosen here.

---

## Layout - phone

At about 375 px, default text size, manager header (two rows). The day shown has notes and is not today. The empty
lines marked "(empty)" are live regions that are always in the DOM but take no space until they hold text.

```
+---------------------------------------+
| Grow2Notes                  Account v |  <header>: static, never sticky (app-shell-nav.md)
| Today  Flagged [3]  Report  Manage    |  "Report" is current: bold + 4 px bar + aria-current
|                     ======            |
+---------------------------------------+
|                                       |  <main>, 16 px side gutters
| Daily report                          |  <h1>, 28 px bold; takes focus on arrival
|                                       |  24 px
| Date                                  |  <label for="report-date">, 18 px bold
| +-----------------------------------+ |  8 px
| | 30 Sep 2026                     v | |  <input type="date" max={today}>, full width,
| +-----------------------------------+ |  48 px, 18 px text; shows the DEVICE's format
| +----------------+ +----------------+ |  12 px
| | < Previous day | |   Next day >   | |  two <a> styled as secondary buttons, 48 px;
| +----------------+ +----------------+ |  side by side when both fit, else stacked
| Wednesday 30 September 2026           |  <p role="status"> date status line
|                                       |  32 px section gap
| (empty)                               |  <div role="alert"> download failure message
| (empty)                               |  <p role="status"> download status line
| +-----------------------------------+ |  (alert, then status, then buttons: the
| |           Download Word           | |  same order as Export record)
| +-----------------------------------+ |  primary <button>, full width, 48 px; 16 px gap
| +-----------------------------------+ |
| |           Download PDF            | |  primary <button>, full width, 48 px
| +-----------------------------------+ |
|                                       |  64 px to the bottom of the page
+---------------------------------------+
```

The same screen on **today, with no submitted notes yet** (design.md §4.7 empty state):

```
| Daily report                          |
|                                       |
| Date                                  |
| +-----------------------------------+ |
| | 1 Oct 2026                      v | |
| +-----------------------------------+ |
| +----------------+ +- - - - - - - - + |  Next day: same <a>, no href, dashed border,
| | < Previous day | |   Next day >   | |  muted fill, label still >= 4.5:1 (8.1:1)
| +----------------+ +- - - - - - - - + |
| No submitted notes for Thursday       |  date status line, (V), wraps; the
| 1 October 2026.                       |  non-breaking space keeps "1 October" together
|                                       |
| +- - - - - - - - - - - - - - - - - -+ |  both downloads: aria-disabled, focusable,
| |           Download Word           | |  dashed border, muted fill, label 8.1:1,
| +- - - - - - - - - - - - - - - - - -+ |  aria-describedby -> the sentence above
| +- - - - - - - - - - - - - - - - - -+ |
| |           Download PDF            | |
| +- - - - - - - - - - - - - - - - - -+ |
```

**Field in error** (for example an iPhone picker that let a future date through, WebKit bug 225639):

```
| Date                                  |
|# Date must be today or in the past    |  message above the input, bold, --colour-error,
|# +---------------------------------+  |  hidden "Error: " prefix; 4 px red bar on the group
|# | 2 Oct 2026                    v |  |  3 px red border, aria-invalid="true"
|# +---------------------------------+  |
| +----------------+ +----------------+ |  both still work, stepping from the URL date
| | < Previous day | |   Next day >   | |
| +----------------+ +----------------+ |
|                                       |  date status line: visible text empty, holds a
|                                       |  visually hidden copy of the error
```

**Sizes and spacing** (tokens from foundations.md):
- `<h1>` `--font-size-h1` (28 px phone / 32 px laptop). Everything else is body size (18 px); this screen has no small
  text. The date status line is body text in `--colour-text`, not grey, because it is the right-day check.
- Gaps: `<h1>` → "Date" label `--space-5` (24 px); label → field `--space-2` (8 px); field → step links and between
  the two links `--space-3` (12 px, above the 8 px target-gap minimum); step links → date status line `--space-3`;
  date status line → download group `--section-gap` (32 px phone / 48 px laptop); download status line → buttons
  `--space-3`; between the two download buttons the shared `ButtonGroup` gap (1rem). Empty live regions take no space
  (`:empty { margin: 0 }`, never `display: none`).
- Tap targets: field, step links and download buttons all `--target-button`, `max(3rem, 48px)`.
- **Step links fit test:** each link is `flex: 1 1 auto`, so at 375 px "‹ Previous day" and "Next day ›" share a row,
  and at 320 px or 200% text they stack full width, still in DOM order. A label never wraps inside a half-width button
  and nothing scrolls sideways (SC 1.4.10). Their inline padding is `--space-3` (12 px), not the shared 20 px, so the
  pair fits on one row at 375 px [Opinion: arithmetic at 18 px bold; check on a device].

---

## Layout - laptop

From `40rem` (foundations.md's one breakpoint). Same DOM order; only widths, the `<h1>` size and gaps change. The
content sits in the `--measure` (40rem) column, left-aligned inside the 60rem page container, like every non-setup
screen.

```
+--------------------------------------------------------------------------------------+
| Grow2Notes                                                                Account v  |
| Today   Flagged [3]   Report   Manage                                                |
+--------------------------------------------------------------------------------------+
|                                                                                      |
| Daily report                                          <h1>, 32 px                    |
|                                                                                      |
| Date                                                                                 |
| +--------------------+  +----------------+  +--------------+                         |
| | 30/09/2026   [cal] |  | < Previous day |  |  Next day >  |   one row (flex-wrap)    |
| +--------------------+  +----------------+  +--------------+                         |
| Wednesday 30 September 2026                                                          |
|                                                                                      |
| daily-notes_2026-09-30.docx is ready. Look in your downloads.                        |
| +------------------+  +------------------+                                           |
| |  Download Word   |  |  Download PDF    |   side by side, left-aligned, same width   |
| +------------------+  +------------------+                                           |
+--------------------------------------------------------------------------------------+
```

What changes from the phone:
- **Date row:** the field (`flex: 0 1 16rem`) and the two step links sit on one row when they fit; flex-wrap breaks
  the row at 200% or 400% zoom with no media query of its own. The browser draws its own segments (day, month, year in
  the device locale's order; an en-US laptop shows 09/30/2026) and a calendar button. Typing is allowed, so the same
  checks as the phone apply [Standard] MDN `<input type="date">`.
- **Downloads:** side by side, Word first, both at least `12rem` wide so they are the same size; left-aligned (AgDS:
  right-aligned buttons are missed by magnifier users) [Convention] https://design-system.agriculture.gov.au/components/button.
- **Hover** styles appear (pointer devices only): primary buttons go to `--colour-action-hover`; step links get the
  `--colour-hover` fill; nothing changes on the unavailable controls.
- Nothing else changes: same words, same order, no extra columns, no side panel, no preview.

---

## Components, in order

Each entry links to its component spec and gives only what is specific to this screen.

### 1. App shell, page title and heading

Specs: [app-shell-nav.md](../components/app-shell-nav.md), [foundations.md](../components/foundations.md),
[empty-loading-error.md](../components/empty-loading-error.md).

- **Route:** `/reports/daily/:date`, a `YYYY-MM-DD` Melbourne date (design.md §4.7: "a date only, so back, forward and
  bookmarks work and no personal data is in the URL").
- **Nav:** "Report" (V) is the current item (`aria-current="page"`) for every date. Its `href` is
  `/reports/daily/${me.today}`, so the screen opens on today without a redirect entry (app-shell-nav.md).
- **Page title:** "Grow2Notes – Daily report" (V pattern, fixed page-name list). No date in the title.
- **`<h1>`:** "Daily report" (P; microcopy.md and date-navigation.md). Fixed text, rendered at once, `tabindex="-1"`,
  focused by the shell on arrival. The date is **not** repeated in the heading; the date status line is the one place
  it appears in words (date-navigation.md).
- **Arrival rules** (date-navigation.md, empty-loading-error.md):
  - `/reports/daily` with no date → replace with `/reports/daily/{me.today}`.
  - A well-formed date after `me.today` → replace with today, so a future date is never on screen or in history.
  - Anything that is not a real `YYYY-MM-DD` (`2026-13-45`, `2026-02-29`) → "Page not found".
  - Workers → "Page not found" (role guard).
- **Loading:** none of its own. The shell shows the wordmark (and "Loading…" after 1 s) until `/api/auth/me` returns;
  this screen then renders fully at once because its `<h1>`, field and links need no data.

### 2. Date field

Spec: [date-navigation.md](../components/date-navigation.md) (with form-validation.md's error look).

| Setting | Value on this screen |
|---|---|
| Element | Native `<input type="date" id="report-date">` with a visible `<label for>`. No custom calendar, no React Aria DatePicker [Convention] NN/g and MOJ support a picker for a chosen date near today, https://www.nngroup.com/articles/date-input/ · https://design-patterns.service.justice.gov.uk/components/date-picker |
| Label | Date (P) |
| Value | The URL date on arrival and after every step, Back or Forward |
| `max` | `me.today` (server, Melbourne, §3.3, A33); never the device clock. Updates by itself when `/me` refreshes after midnight |
| `min` | None (design.md specifies none; see Open questions) |
| `aria-describedby` | `report-date-status` normally; `report-date-error` only while in error (so the error is not read twice) |
| Commit | On Enter, on leaving the field, or 750 ms after the last change while focused (needed for Android's picker, which returns focus without a blur). The 750 ms commit is silent and happens only for a real date, not after today, with a year of 1900 or later, so half-typed years never reach the URL [Research, dated] w3c/html#194, https://github.com/w3c/html/issues/194 |
| Checks | In code on every commit, because iOS ignores `max` (WebKit bug 225639, open, last modified February 2026, https://bugs.webkit.org/show_bug.cgi?id=225639) and desktop typing gets past it [Standard] MDN https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/date. The server stays the gate (`404 report.no_notes` for any future date) |
| Errors | Empty or incomplete: "Enter a date" (P). After today: "Date must be today or in the past" (P). GOV.UK templates. Shown on Enter, blur, or a download press, never while typing; cleared the moment the value becomes valid |
| Style | 2 px `--colour-border-control` border, 18 px text (iOS does not zoom at 16 px or more), `min-block-size: 3rem`, `inline-size: 100%` on the phone, `appearance` left alone so the calendar button stays. Error: 4 px `--colour-error` bar on the group, 3 px red border, message above the input in bold `--colour-error` with a visually hidden "Error: " prefix |
| States | Default · Focus (3 px near-black outline, 2 px offset) · Active (native picker or segment) · Error · never disabled · no read-only state (manager-only screen) |

### 3. Previous day and Next day

Specs: [date-navigation.md](../components/date-navigation.md), [primary-actions.md](../components/primary-actions.md).

| Setting | Value on this screen |
|---|---|
| Element | `<a>` elements styled with the shared **secondary** button class, because each one changes the URL [Convention] Roselli, https://adrianroselli.com/2016/01/links-buttons-submits-and-divs-oh-hell.html · GOV.UK pagination uses links, https://design-system.service.gov.uk/components/pagination/ |
| Labels | Previous day · Next day (V). A `‹` / `›` glyph sits beside each with `aria-hidden="true"`, so the accessible name is exactly the visible words (SC 2.5.3) |
| Targets | Previous day: URL date − 1. Next day: URL date + 1. Calendar arithmetic on `YYYY-MM-DD` strings in UTC (`addDays` in `src/lib/dates.ts`), never ±86,400,000 ms, never `Date.now()`: adding a day's milliseconds goes wrong on 5 April 2026, and daylight saving starts on Sunday 4 October 2026 [Opinion, tested in Node 24 in date-navigation.md] |
| Behaviour | Client-side push (`useLinkClickHandler` with `preventScrollReset`), so each step adds one Back entry [Research] Baymard 2020, https://baymard.com/blog/back-button-expectations. Focus stays on the link; nothing scrolls; the route is never keyed by date |
| Previous day | Always available (there is no earliest date) |
| Next day on today (design: "disabled") | The **same `<a>` element**: no `href`, `role="link"`, `aria-disabled="true"`, `tabIndex={0}`, no click handler. Unavailable look: `--colour-surface-muted` fill, `--colour-text-secondary` label (8.1:1), 2 px **dashed** border, `cursor: not-allowed`; `GrayText` in forced colours. Enter or a click does nothing. Swapping the element or dropping `href` without `tabIndex` would drop a keyboard user's focus to `<body>` when they step onto today [Standard] HTML focus fixup, https://html.spec.whatwg.org/multipage/interaction.html · React state reset on a different element type, https://react.dev/learn/preserving-and-resetting-state · [Convention] O'Hara, https://www.scottohara.me/blog/2021/05/28/disabled-links.html |
| Reason text for Next day | None added. The field shows the chosen date and the status line names it; design.md gives no reason string, and the meaning ("no later day") is plain from the position [Opinion] |
| States | Default (secondary: 2 px `--colour-action` border, `--colour-action` label) · Hover (laptop: `--colour-hover` fill) · Focus (3 px outline, 2 px offset) · Active (`--colour-action-tint-pressed`, instant) · Unavailable (Next day on today only) |

### 4. Date status line

Specs: [date-navigation.md](../components/date-navigation.md), [empty-loading-error.md](../components/empty-loading-error.md),
[microcopy.md](../components/microcopy.md).

- `<p id="report-date-status" role="status">`, in the DOM from the first render, directly under the step links, above
  the downloads. Body text, `--colour-text`, never grey or italic.
- **Text, by state** (exact strings in States, below): the URL date in words ("Thursday 1 October 2026", `dateLong`
  from `src/copy/format.ts`, with a non-breaking space in "1 October"); or, on a definite "no notes", the design's
  sentence "No submitted notes for Thursday 1 October 2026." (V); or empty while the field holds an uncommitted or
  invalid value, with a visually hidden copy of the field error when there is one.
- The date is wrapped in `<time dateTime="2026-10-01">` (microcopy.md).
- Both download buttons always point at this line with `aria-describedby`, so a screen-reader user hears which day a
  download is for, and the reason when the day is empty.
- It is keyed to the URL date: it never shows the previous day's answer while the new one loads (no `placeholderData`
  on the check) [Standard] TanStack placeholder data, https://tanstack.com/query/v5/docs/framework/react/guides/placeholder-query-data
  · [Research] NN/g on misleading status, https://www.nngroup.com/articles/empty-state-interface-design/.

### 5. Download failure message

Specs: [primary-actions.md](../components/primary-actions.md), [microcopy.md](../components/microcopy.md).

- `<div id="report-download-alert" role="alert">`, always in the DOM, above the download status line and the
  download buttons (the shared order for action failures, as on Export record: alert, status, buttons). Empty and
  spaceless until a download fails.
- Text: one sentence in the microcopy.md "Action failed" shape, bold, `--colour-error` (8.1:1); no icon, no box. The
  words ("Not downloaded: …") carry the meaning, so colour is never the only cue (SC 1.4.1).
- Cleared at the next press and whenever the URL date changes. A repeat of the same failure clears the text and writes
  it again on the next animation frame, so it is announced again.

### 6. Download Word and Download PDF

Specs: [file-download.md](../components/file-download.md), [primary-actions.md](../components/primary-actions.md).

| Setting | Value on this screen |
|---|---|
| Element | Two native `<button type="button">`s from the shared `Button` component. Not links: they must be unavailable with a reason, keep `401`, network and `429` failures inside the app, and avoid double audited downloads [Convention] Roselli; [Standard] APG on focusable disabled controls, https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/ |
| Labels | Download Word · Download PDF (V), Word first (design order). No icon |
| Style | Both **primary**, same width. Deliberately breaks "one primary per page": they are one action in two formats, and §11.5 makes Word the accessible version and PDF the filing version, so neither is secondary [Opinion, primary-actions.md] |
| `aria-describedby` | Always `report-date-status` (the day in words, or the reason) |
| Request | `GET /api/reports/daily/{date}/export?format=docx|pdf` through the shared `api()` wrapper's raw-`Response` form, with the screen's `AbortController` and the wrapper's per-call option `timeoutMs: 30_000` instead of the default 10 s (empty-loading-error.md; M4 allows up to 10 s for 20 notes). Export record passes the same option [Opinion] |
| Save | Blob → same-origin `blob:` object URL → a temporary hidden `<a download rel="noopener">` with **no** `target` → click → remove → revoke after 60 s. File name from `Content-Disposition` (`daily-notes_2026-10-01.docx`), falling back to the same pattern [Standard] MDN `<a download>`, https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a · WebKit bug 190351 (never `target="_blank"`), https://bugs.webkit.org/show_bug.cgi?id=190351 |
| Available | When the day has notes, **while the has-notes check is still running**, and when the check failed. The export endpoint has the final say (`404 report.no_notes`) |
| Unavailable (design: "disabled") | Only for a definite reason: the check said `hasNotes: false` for the URL date, or the field is in error. `aria-disabled="true"`, never `disabled`; still in the Tab order; `--colour-surface-muted` fill, `--colour-text-secondary` label (8.1:1), 2 px dashed border, `cursor: not-allowed`; `GrayText` in forced colours |
| Busy | From the press: an `inFlight` ref plus `aria-disabled` on **both** buttons, so a second tap does nothing and only one file is made at a time. The pressed button shows "Downloading…" (P) after 400 ms in the same grid cell as its label (no size change). The other button keeps its normal look. No spinner, no motion (foundations.md). `announceBusy={false}`: the download status line says the wait instead, so the page status region does not read it twice (app-shell.md component 3) |
| Results | Ready text in the download status line; failures in the failure message; `404 report.no_notes` turns the screen into the empty state. Focus never moves. Nothing is retried automatically: a retry would be a second audited download, counts toward the 10-a-minute limit (§9.9), and in Chrome a second download without a click brings up the "multiple downloads" prompt [Standard] Chromium `download_request_limiter.h`, https://chromium.googlesource.com/chromium/+/HEAD/chrome/browser/download/download_request_limiter.h |
| States | Default · Hover (laptop, `--colour-action-hover`) · Focus (3 px near-black outline, 2 px offset; the offset is what makes the ring pass against the blue fill) · Active (`--colour-action-pressed`, instant) · Unavailable · Busy · no read-only state |

### 7. Download status line

Spec: [file-download.md](../components/file-download.md).

- `<p id="report-download-status" role="status">`, always in the DOM, directly **above** the buttons, after the
  failure alert: the same position as on Export record, so the line is in view whenever the buttons are.
- Text, word for word the same shapes as Export record: "Preparing the Word file… Keep this page open." or
  "Preparing the PDF file… Keep this page open." (P; the format of the pressed button) while a download has taken
  400 ms or more; then "daily-notes_2026-10-01.docx is ready. Look in your downloads." (P) with the real file name.
  Stays until the next press or date change; never times out (SC 2.2.1). Body text, `--colour-text` (the preparing
  line in `--colour-text-secondary`), `overflow-wrap: anywhere` so the file name cannot cause sideways scrolling at
  320 px or 200% text.

### 8. Data: the has-notes check (not visible)

Specs: [date-navigation.md](../components/date-navigation.md), [file-download.md](../components/file-download.md),
[empty-loading-error.md](../components/empty-loading-error.md).

- `useQuery(['reports', 'daily', date, 'has-notes'])` → `GET /api/reports/daily/{date}/has-notes` → `{date, hasNotes}`.
  Yes or no only; no count, no content (D26, A18).
- Runs for the committed (URL) date only. No `placeholderData`, no `useSuspenseQuery` (a suspended render would
  unmount the links and drop focus).
- App defaults from empty-loading-error.md: `networkMode: 'always'`, 10 s timeout, one silent retry for network, timeout
  or `5xx`. This query **opts in** to `refetchOnWindowFocus: true` (the app default is off, app-shell-nav.md), because
  a manager may leave the screen open while a worker submits. No polling (§6.8).
- A failed background refetch keeps the last answer (`isLoadingError`, not `isError`).
- A `404 report.no_notes` from a download writes `{date, hasNotes: false}` into this query's cache.

### Wiring sketch (React 19, React Router, TanStack Query 5, CSS Modules)

Composition only; each hook is specified in its component file.

```tsx
// features/reports/DailyReportScreen.tsx — route /reports/daily/:date
export function DailyReportScreen() {
  const { date } = useParams();
  const { today } = useMe();                                         // server's Melbourne date (§3.3)
  if (!date) return <Navigate to={`/reports/daily/${today}`} replace />;
  if (!isIsoDate(date)) return <NotFound />;
  if (date > today) return <Navigate to={`/reports/daily/${today}`} replace />;
  return <DailyReport date={date} today={today} />;                  // never key={date}: the DOM must persist
}

function DailyReport({ date, today }: { date: IsoDate; today: IsoDate }) {
  usePageTitle('Daily report');
  const field = useReportDateField(date, today);  // date-navigation.md: draft, error, inputValue(), commit(), focus()
  const check = useQuery({ ...hasNotesQuery(date), refetchOnWindowFocus: true });   // no placeholderData
  const dl = useReportDownload(date);             // file-download.md: inFlight ref, busy, format, statusText, failure;
                                                  // aborts and clears when the URL date changes to a different day
  const settled = field.draft === date && !field.error;
  const empty = settled && check.data?.hasNotes === false;

  function press(format: 'docx' | 'pdf') {
    if (dl.busy) return;                           // aria-disabled does not block clicks
    let target = date;
    const typed = field.inputValue();              // read the field itself: browsers differ on blur/click order
    if (typed !== date) {                          // picked or typed, not yet committed
      const committed = field.commit(typed, true); // same checks as Enter; shows the field error if any
      if (!committed) { field.focus(); return; }
      target = committed;                          // URL moves to it; the download is for the date in the field
    } else if (empty) return;                      // reason already shown, described and announced
    dl.start(target, format);
  }

  return (
    <>
      <PageHeading>Daily report</PageHeading>
      <div className={s.dateRow}>
        <DateField ref={field.ref} id="report-date" label={copy.date} max={today} {...field.inputProps}
          describedBy={field.error ? 'report-date-error' : 'report-date-status'} />
        <div className={s.steps}>
          <StepLink to={addDays(date, -1)}>{copy.previousDay}</StepLink>
          <StepLink to={date < today ? addDays(date, 1) : null}>{copy.nextDay}</StepLink>
        </div>
      </div>
      <p id="report-date-status" role="status" className={s.dateStatus}>
        {settled && (empty ? msg.noSubmittedNotes(date) : <time dateTime={date}>{dateLong(date)}</time>)}
        {field.error && <span className="visually-hidden">{field.error}</span>}
      </p>
      <div id="report-download-alert" role="alert" className={s.alert}>{dl.failure}</div>
      <p id="report-download-status" role="status" className={s.downloadStatus}>{dl.statusText}</p>
      <ButtonGroup className={s.downloads}>
        {(['docx', 'pdf'] as const).map((f) => (
          <Button key={f} variant="primary"
            unavailable={empty || !!field.error} onUnavailablePress={() => press(f)}
            blocked={dl.busy}                        // both ignore presses, neither changes look
            busy={dl.busy && dl.format === f} busyLabel={copy.downloading} announceBusy={false} announceBusy={false}
            describedBy="report-date-status"         // always: the day in words, or the reason
            onClick={() => press(f)}>
            {f === 'docx' ? copy.downloadWord : copy.downloadPdf}
          </Button>
        ))}
      </ButtonGroup>
    </>
  );
}
```

`StepLink` is date-navigation.md's single-`<a>` component. `Button` needs two additions to primary-actions.md's sketch
(see Conflicts resolved 11): a `blocked` prop (ignore presses with no visual change) and a `describedBy` that is
merged, not replaced, when unavailable. All strings come from `src/copy` (microcopy.md).

---

## States

Every state from design.md §4.7, plus loading, empty and error. "URL date" is the committed date in the address bar.
Exact copy is in the cells; dates in the examples are Thursday 1 October 2026 (today) or Wednesday 30 September 2026.

### Screen states

| # | State | When | Field and step links | Date status line (exact) | Downloads | Failure message / download status (exact) |
|---|---|---|---|---|---|---|
| S1 | **Ready, day has notes** | `hasNotes: true` | Field shows the URL date. Both links available | Wednesday 30 September 2026 (P: the date alone, in design.md's long format) | Available | Empty / empty |
| S2 | **Today** (design: "Next day is disabled on today") | URL date = `me.today` | `max` = today. **Next day unavailable** (same `<a>`, no `href`, dashed) | As S1, S3, S4 or S5 | Per the check | – |
| S3 | **Checking** (has-notes in flight, any length) | After arrival or a date change | Unchanged | The URL date in words, at once | **Available**, no change of look, no "Loading" text | – |
| S4 | **Empty day** (design.md §4.7, A18, §11.4) | `hasNotes: false` | Unchanged | No submitted notes for Thursday 1 October 2026. (V) | **Unavailable** (dashed), focusable, `aria-describedby` → this sentence. A press does nothing and sends nothing | Empty / empty |
| S5 | **Check failed** | has-notes failed after one silent retry (no connection, timeout, `5xx`) | Unchanged | The URL date in words; **no error text** | Available; the download reports its own result | – |
| S6 | **Changing the date** | Field value differs from the URL date and is not yet committed (picker just closed, or typing) | Shows the new value; links still step from the URL date | Empty | Look available. A press commits the field first (same checks as Enter), then downloads that date | – |
| S7 | **Field error: empty or incomplete** | Enter, blur or a download press with an incomplete value | "Enter a date" (P) above the input; red bar and border; `aria-invalid="true"`. Links still work and clear the error | Visible text empty; hidden copy: Enter a date | Unavailable. A press moves focus to the field | – |
| S8 | **Field error: future date** | Enter, blur or a download press with a date after today (iOS picker; desktop typing) | "Date must be today or in the past" (P) | Hidden copy: Date must be today or in the past | Unavailable. A press moves focus to the field | – |
| S9 | **Preparing** | A download has run for 400 ms or more | Unchanged; links still work (a step cancels the download) | Unchanged | Pressed button reads "Downloading…" (P); both `aria-disabled`; the other looks normal | – / Preparing the Word file… Keep this page open. (or "PDF", the pressed format) (P) |
| S10 | **Ready** | The file was handed to the browser | Unchanged | Unchanged | Available | – / daily-notes_2026-09-30.docx is ready. Look in your downloads. (P; real file name) |
| S11 | **Failed: no connection or timeout** | `fetch` rejected, connection dropped mid-file, or 30 s timeout | Unchanged | Unchanged | Available; focus stays on the pressed button | Not downloaded: no connection. Try again. (P) / empty |
| S12 | **Failed: server** | `5xx` or anything unexpected | Unchanged | Unchanged | Available | Not downloaded: something went wrong. Try again. (P) / empty |
| S13 | **Failed: too many** | `429` (10 exports a minute per user, §9.9; for example a week caught up in both formats) | Unchanged | Unchanged | Available | Not downloaded: too many downloads in the last minute. Wait a minute, then try again. (P) / empty |
| S14 | **Export says no notes** | `404 report.no_notes` from a press (the day was empty and the check had not answered or had failed) | Unchanged | Becomes S4's sentence (announced) | Become S4 | Both cleared; no error |
| S15 | **Signed out** | Any `401` (check or download) | session-timeout.md's sign-in in place; the URL keeps the date | – | The download is **not** replayed; after sign-in the manager presses again | – |
| S16 | **Not a manager** | A worker opens the route, or a `403` from either request after a role change | Whole page: `<h1>` "Page not found", "If you typed or pasted the web address, check it is correct.", link "Go to Today" (P, empty-loading-error.md) | – | – | – |
| S17 | **Bad URL date** | Not a real `YYYY-MM-DD` | As S16 | – | – | – |
| S18 | **Missing or future URL date** | `/reports/daily`, or a date after `me.today` | Replaced by `/reports/daily/{me.today}` with no history entry | – | – | – |
| S19 | **Crash** | A render error caught by the route error boundary | `<h1>` "There is a problem with Grow2Notes", "Anything already saved is kept.", link "Go to Today" (full page load) (P) | – | – | – |
| S20 | **After midnight, screen left open** | `/me` refreshes on focus, visibility or navigation (§6.8) | `max` moves to the new today; Next day becomes available on what was today. The URL date does not change | Unchanged | Unchanged | Unchanged |

There is **no loading message** on this screen (empty-loading-error.md: "None on screen"): the has-notes request is
tiny, and the screen's own parts need no data. The shell owns the start-up wait.

### Interactive states per control (summary)

| Control | Default | Hover (laptop) | Focus | Active | Unavailable | Busy | Error |
|---|---|---|---|---|---|---|---|
| Date field | 2 px near-black border | none | 3 px outline, 2 px offset | Native picker / segment | Never | – | Message above, bar, 3 px red border, `aria-invalid` |
| Previous day | Secondary | Action tint | Ring | Pressed tint | Never | – | – |
| Next day | Secondary | Action tint | Ring (also when unavailable) | Pressed tint | On today: dashed, muted, no `href` | – | – |
| Download Word / PDF | Primary | Darker blue | Ring | Darker blue | Empty day or field error: dashed, muted | "Downloading…" after 400 ms (pressed one) | Message in the failure line |

---

## Interactions and focus

### Arrival focus

| How the manager arrived | Focus | Scroll | Owner |
|---|---|---|---|
| From another screen (nav "Report", a link, Back from another screen) | The `<h1>` "Daily report" | Top on push; restored on Back | app-shell-nav.md |
| Very first page load (reload, bookmark, typed URL) | Where the browser puts it | Browser default | app-shell-nav.md |
| **From this screen to this screen** (Previous day, Next day, a committed date, browser Back or Forward between report dates, or nav "Report" while already here) | **Unchanged** | **Unchanged** | This spec (see Conflicts resolved 7) |

The route declares `handle: { screenKey: 'dailyReport' }`. The shell's route-change rule (app-shell.md) treats two
locations that share a `screenKey` as the same screen, for push, replace and pop, so it skips the `<h1>` focus and the
scroll reset, including browser Back to the first report entry (which the nav link created without any state). The
shell's in-memory scroll map keys this route by the `screenKey`, so all report dates share one scroll position.
React Router's `<ScrollRestoration>` is **not** used: it writes to `sessionStorage` (D22, §9.6). Changing the date
is a change of content, not of context, so SC 3.2.2 is met [Standard] https://www.w3.org/WAI/WCAG22/Understanding/on-input.html.

### Actions

| Action | What happens | Focus after | Announced |
|---|---|---|---|
| **Previous day** (tap, click, Enter) | Push `/reports/daily/{URL date − 1}`. Field resets to the new date; any field error, failure text and download status clear; any download in flight is aborted; the check runs for the new date | Stays on Previous day | Date status: "Tuesday 29 September 2026", then, if empty, "No submitted notes for Tuesday 29 September 2026." (some screen readers say only the second) |
| **Next day** (available) | Push URL date + 1. Same as above | Stays on Next day, **including when the new date is today** and the link becomes unavailable (same element) | As above; on today the link now reads "Next day, link, unavailable" (NVDA) or "dimmed" (VoiceOver) |
| **Next day** (unavailable, on today) | Nothing (no `href`, no handler) | Stays | Nothing new |
| **Pick a date** (phone picker) | iOS: calendar popover, ignores `max`. Android: calendar dialog, greys out dates after `max`, returns focus without a blur. The value commits 750 ms after the last change, on Enter, or on leaving the field. The first commit in one visit to the field **pushes** a history entry; later commits in the same visit **replace** it | Stays in the field | Date status: the new date in words (and the empty sentence if empty). A future date: on leaving the field, the hidden error copy "Date must be today or in the past" |
| **Type a date** (laptop segments) | Nothing happens per keystroke; a half-typed year never reaches the URL. Commit as above | Stays | As above |
| **Enter in the field** | Commit with checks; shows the error if any. Enter never submits anything (there is no form) | Stays | Error: through the field's `aria-describedby` when focus returns; hidden copy in the date status |
| **Leave the field** (Tab, tap elsewhere) | Commit with checks | Moves where the user moved it | Error: hidden copy in the date status line |
| **Browser Back / Forward** | Steps through the report dates viewed (one entry per step or per picking visit), then back to the previous screen | Unchanged within the screen | Date status: the date in words |
| **Download Word / Download PDF** (available, field matches URL) | `inFlight` set; both `aria-disabled`; request sent. After 400 ms: "Downloading…" on the pressed button and "Preparing the Word file… Keep this page open." / "Preparing the PDF file… Keep this page open." in the download status. Success: the browser saves or prompts (iPhone Safari may offer View or Download; that is the browser's own UI) | **Stays on the pressed button** | Download status: "Preparing the Word file… Keep this page open." / "Preparing the PDF file… Keep this page open." (only if slow), then "daily-notes_2026-10-01.docx is ready. Look in your downloads." |
| Download press **while the field holds an uncommitted date** | Commit it with Enter's checks. Valid: the URL moves to it (push or replace per the visit rule) and that date downloads. Invalid: the field error shows, nothing is sent | Valid: stays on the button. Invalid: **moves to the field** | Valid: the new date in words, then the download messages. Invalid: the field reads its label, "invalid entry", and the error |
| Download press **on an empty day** | Nothing; no request | Stays | Nothing new: the reason was announced on the date change and is read with the button (`aria-describedby`) |
| Download press **while the field is in error** | Nothing is sent | **Moves to the field** | The field's label, invalid state and error |
| Second press while busy (either button) | Ignored by the `inFlight` ref | Stays | Nothing |
| Download fails | One sentence in the failure message; buttons available again | Stays on the pressed button | `role="alert"`: the failure sentence |
| Download returns `404 report.no_notes` | The check's cache becomes `hasNotes: false`; S4 | Stays (the button is now unavailable, still focusable) | Date status: "No submitted notes for …" |
| Session ended (`401`) | session-timeout.md's sign-in in place; date kept in the URL; no replay | session-timeout.md | session-timeout.md |
| Window regains focus / tab becomes visible | `/me` refreshes (badge, `today`, `max`, Next day); the check refetches for the URL date. Field value, failure text and download status unchanged | Unchanged | Only if the check's answer changes the date status text (for example a first note was just submitted: the empty sentence becomes the date) |
| Leave the screen during a download | The request is aborted; no file appears later. The server may already have written the `report.downloaded` audit row | Next screen's rule | Next screen |

### Tab order

Skip link → Grow2Notes → Account → Today → Flagged → Report → Manage → (Account panel contents, when open) → *(`<h1>`:
focus target only, not a Tab stop)* → **Date** field → **Previous day** → **Next day** (a Tab stop in both states) →
**Download Word** → **Download PDF**. DOM order is visual order at every width (no CSS `order`, no `row-reverse`). No
positive `tabindex`, no custom keys.

### Focus is never lost

- The route is not keyed by date and has no Suspense boundary, so every date change keeps the same DOM nodes.
- Next day is the same `<a>` in both states and stays focusable (`tabIndex={0}`).
- Buttons use `aria-disabled`, never `disabled`, for busy and unavailable.
- When a press must explain a field error, focus moves to the field on purpose (the user started it; GOV.UK one-field
  validation pattern).

### Live regions on this screen (all in the DOM before any text is written)

| Region | Role | Carries | Never carries |
|---|---|---|---|
| `#report-date-status` | `role="status"` (polite) | The URL date in words · "No submitted notes for …" · hidden field-error copy | Download progress or results |
| `#report-download-alert` | `role="alert"` | The three "Not downloaded: …" sentences | Success, busy, empty-day text |
| `#report-download-status` | `role="status"` (polite) | "Preparing the Word file… Keep this page open." (or PDF) · "… is ready. Look in your downloads." | Errors |
| Page status region (`PageStatus`, visually hidden, app-shell.md component 3) | `role="status"` (polite) | Nothing on this screen in normal use: the downloads opt out (`announceBusy={false}`) because `#report-download-status` says the wait | Download text (never twice) |

They never change at the same moment: a date change clears the two download regions silently (clearing is not
announced) and writes only the date status. Text present at first render is not announced; the focused `<h1>` announces
the screen. On arrival on an empty day the date status changes once the check answers, so "No submitted notes for …"
is heard just after the heading; that is intended (empty-loading-error.md: the one announced empty state)
[Research] O'Hara, https://www.scottohara.me/blog/2022/02/05/are-we-live.html · [Standard] SC 4.1.3,
https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html.

---

## Accessibility checklist

**Headings and landmarks**
- [ ] One `<h1>` "Daily report", `tabindex="-1"`, focused on arrival from another screen; no other headings.
- [ ] Landmarks come only from the shell: `<header>`, the "Main" `<nav>`, `<main id="main-content">`, skip link. No
      `<nav>` around the step links, no extra regions.
- [ ] Page title "Grow2Notes – Daily report" for every date (SC 2.4.2); no names anywhere in the title or URL.
- [ ] `<html lang="en-AU">`.

**Labels and names**
- [ ] The date field has the visible label "Date" via `<label for>`; no placeholder; no `aria-label` (SC 1.3.1, 3.3.2).
- [ ] Every accessible name equals its visible words: "Previous day", "Next day", "Download Word", "Download PDF"; the
      `‹` `›` glyphs are `aria-hidden` (SC 2.5.3, for voice control: "click Previous day").
- [ ] Both download buttons have `aria-describedby="report-date-status"` at all times.
- [ ] The field has `aria-describedby="report-date-status"`, or `report-date-error` while in error, never both.
- [ ] Every displayed date sits in `<time dateTime>`; dates use words, never numbers only (the device may read 01/10 as
      10 January).

**States and ARIA**
- [ ] Unavailable Next day: no `href`, `role="link"`, `aria-disabled="true"`, `tabIndex={0}` (SC 4.1.2).
- [ ] Unavailable and busy buttons: `aria-disabled="true"`; the `disabled` attribute appears nowhere on this screen.
- [ ] Field in error: `aria-invalid="true"`; message with a visually hidden "Error: " prefix; error not shown while
      typing.
- [ ] The three live regions exist from the first render and are never mounted on demand or toggled with
      `display: none`.
- [ ] No `aria-busy`, no `aria-live` on buttons, no `role="menu"`, no tooltip or `title` carrying information.

**Keyboard**
- [ ] Tab order as listed; every control reachable and operable with the keyboard alone (SC 2.1.1, 2.4.3).
- [ ] Enter in the field commits; Enter on a link follows it; Enter or Space on a button presses it; Enter on the
      unavailable Next day does nothing and keeps focus.
- [ ] Focus never lands on `<body>`: step onto today with Next day; press a download; let a download fail; press a
      download on an empty day; use browser Back with focus on a download button.

**Screen reader** (expected; the native field's wording varies)
- [ ] Field: "Date", the control type, the value, then "Wednesday 30 September 2026".
- [ ] A step announces the new date, and the empty sentence when empty.
- [ ] A button on an empty day: "Download Word, dimmed/unavailable, button, No submitted notes for Thursday 1 October
      2026."
- [ ] A slow download: "Preparing the Word file… Keep this page open." (or PDF), then "… is ready. Look in your
      downloads."
- [ ] A failure is announced once per attempt, including two identical failures in a row.

**Visual (foundations.md)**
- [ ] All text uses foundations.md colours at 7:1 or better; unavailable labels stay at 8.1:1 (SC 1.4.3, beyond the
      exemption for inactive controls).
- [ ] Field border, secondary borders, dashed unavailable borders and focus ring ≥ 3:1 (SC 1.4.11); the focus ring
      keeps its 2 px offset on the blue buttons.
- [ ] Unavailable is shown by a dashed border as well as colour; errors by words, a bar and a border (SC 1.4.1).
- [ ] Targets ≥ 48 × 48 px; ≥ 8 px between separate targets (SC 2.5.8; A32's 44 px).
- [ ] 200% text and 320 px width: everything wraps, step links stack, the file name wraps, no sideways scroll, nothing
      clipped (SC 1.4.4, 1.4.10); text-spacing overrides cause no clipping (SC 1.4.12).
- [ ] Nothing sticky or fixed (SC 2.4.11); no motion of any kind (SC 2.2.2, 2.3.3).
- [ ] Forced colours: real borders on every control; `GrayText` for unavailable; `Highlight` focus ring.

**WCAG 2.2 AA criteria this screen must meet:** 1.3.1 · 1.3.2 · 1.3.4 · 1.4.1 · 1.4.3 · 1.4.4 · 1.4.10 · 1.4.11 ·
1.4.12 · 2.1.1 · 2.2.1 (no message times out) · 2.2.2 · 2.4.2 · 2.4.3 · 2.4.6 · 2.4.7 · 2.4.11 · 2.5.3 · 2.5.8 ·
3.1.1 · 3.2.2 · 3.2.4 (same words as the nav, export and other screens) · 3.3.1 · 3.3.2 · 3.3.3 · 3.3.7 (a typed date
is never cleared by the app) · 4.1.2 · 4.1.3. 3.3.4 does not apply: nothing is changed or deleted.

---

## Acceptance criteria

**Routing and access**
- [ ] The nav "Report" link opens `/reports/daily/{me.today}`; the field shows that date and `max` equals it.
- [ ] `/reports/daily` and any date after `me.today` are replaced by today's URL; Back does not return to them.
- [ ] `/reports/daily/2026-13-45` and `/reports/daily/2026-02-29` show "Page not found" with the title
      "Grow2Notes – Page not found".
- [ ] A worker opening any `/reports/daily/...` URL sees "Page not found" and no Report nav item; `GET .../has-notes`
      and `GET .../export` return `403` for workers (M4).
- [ ] With the device clock set to another time zone or a wrong date, the screen still opens on the server's Melbourne
      today.

**Date field and stepping**
- [ ] Previous day from 2026-10-01 goes to 2026-09-30; Next day from 2026-10-03 goes to 2026-10-04 (daylight saving
      starts that day); from 2026-04-05 to 2026-04-06; from 2028-02-28 to 2028-02-29. Unit tests run with
      `TZ=Australia/Melbourne`.
- [ ] On today, Next day has no `href`, has `role="link"`, `aria-disabled="true"`, a dashed border, and is a Tab stop;
      pressing it changes nothing. Stepping onto today with the keyboard leaves focus on Next day.
- [ ] Each Previous day / Next day press adds one history entry; Back steps through the days viewed, then leaves the
      screen. One visit to the field that passes through several values adds one entry.
- [ ] A date change keeps focus where it was and does not scroll; the `<h1>` is not focused.
- [ ] While a year is half typed (for example "0202"), the URL does not change and nothing is announced.
- [ ] A picked or typed future date shows "Date must be today or in the past" on Enter, on leaving the field, or on a
      download press; no request is sent; Previous day still steps from the URL date and clears the error.
- [ ] An emptied field shows "Enter a date" on Enter or blur.
- [ ] The date status line shows the URL date as "Wednesday 30 September 2026" (non-breaking space between the day
      number and month) and is empty while the field holds a different, uncommitted value.

**Has-notes and the empty day**
- [ ] While the check is in flight the buttons look and behave available and no loading text appears.
- [ ] On `hasNotes: false` the date status reads exactly "No submitted notes for Thursday 1 October 2026." and both
      buttons have `aria-disabled="true"`, a dashed border, remain Tab stops, and send no request when pressed.
- [ ] The previous date's answer is never shown under a new date (no `placeholderData`).
- [ ] If the check fails (offline, `503`), both buttons stay available and no message appears; pressing one reports its
      own result.
- [ ] Leaving the window and coming back refetches the check; after a worker submits the day's first note, the empty
      sentence becomes the date and the buttons become available without a reload.
- [ ] A download answered `404 report.no_notes` turns the screen into the empty state without an error message.

**Downloads**
- [ ] Download Word saves `daily-notes_{date}.docx`; Download PDF saves `daily-notes_{date}.pdf` (name from
      `Content-Disposition`), in Chromium, Firefox and WebKit (Playwright `waitForEvent('download')`).
- [ ] A double click or double tap sends exactly one request and writes one `report.downloaded` audit event.
- [ ] While one format is downloading, the other button sends nothing when pressed.
- [ ] A response under 400 ms shows no busy label and no "Preparing…" line; a slower one shows "Downloading…" on the
      pressed button (same button size) and "Preparing the Word file… Keep this page open." (or "PDF") in the status
      line above the buttons.
- [ ] Success shows "{file name} is ready. Look in your downloads." in the status line above the buttons and keeps
      focus on the pressed button.
- [ ] A download mocked to take 25 s succeeds (the 30 s `timeoutMs`), with "Downloading…" and the preparing line
      shown throughout.
- [ ] The shell treats report dates as one screen through `handle.screenKey`: browser Back from the second report date
      to the first keeps focus where it was and does not scroll; `sessionStorage` stays empty.
- [ ] Offline: "Not downloaded: no connection. Try again." `500`: "Not downloaded: something went wrong. Try again."
      `429`: "Not downloaded: too many downloads in the last minute. Wait a minute, then try again." No automatic retry
      in any case (one request per press in the network log).
- [ ] A `401` hands over to sign-in in place; after signing in the same date is on screen and nothing downloads until
      the manager presses again.
- [ ] Pressing Previous day during a download aborts it; no file appears afterwards and no stale message remains.
- [ ] A press with an uncommitted valid date in the field downloads that date and moves the URL to it.
- [ ] No part of the report, no preview, no count, no file size and no drafts list appears anywhere on screen (D26,
      D43, A18); no new tab or window opens.
- [ ] The object URL is revoked within 60 s; nothing is written to `localStorage`, `sessionStorage`, IndexedDB or Cache
      Storage by this screen (D22).

**Copy and layout**
- [ ] Every visible string matches this spec and comes from `src/copy`; microcopy.md's parent-company-name check (D42),
      banned-word check and US-spelling check pass.
- [ ] At 375 px the step links share a row; at 320 px they stack; no horizontal scroll at 320 px or 200% text.
- [ ] axe (Playwright) reports no violations in S1, S2, S4, S7, S10 and S11.

**Real devices, test environment with the production CSP (M4)**
- [ ] iPhone Safari, iPhone Chrome, Android Chrome and Samsung Internet each save the file from the `blob:` URL. If any
      cannot, switch this screen to a plain same-origin link for that case, as file-download.md's contingency says.
- [ ] iPhone: a future date chosen in the picker is caught on leaving the field.
- [ ] Android: a date picked in the dialog commits within about 1 s without leaving the field.
- [ ] NVDA + Chrome, VoiceOver on iOS and macOS, and TalkBack hear the announcements in the checklist above.

---

## Conflicts resolved

| # | Where the component specs disagreed | Chosen for this screen | Why |
|---|---|---|---|
| 1 | **Order of the date controls.** design.md §4.7 lists "Previous day, a native date field and Next day"; file-download.md's tab order and foundations.md's laptop note follow that order. date-navigation.md puts the field first, then Previous day and Next day. | Field first, then Previous day, Next day, at every width. | One DOM order must hold at every width (SC 1.3.2, 2.4.3). With the design's order, a 375 px phone has no room for three controls on a row, so they stack as three full-width rows with Next day separated from Previous day. Field first gives the field full width on the phone, keeps the pair together (previous left, next right, the GOV.UK pagination idiom), and lets a screen-reader user hear the chosen date before the stepping links. All three controls are kept; only their sequence differs from the design's sentence [Opinion]. |
| 2 | **Downloads while has-notes is loading.** date-navigation.md, primary-actions.md and empty-loading-error.md: unavailable, no message. file-download.md: available. | Available while loading and when the check fails; unavailable only on a definite `hasNotes: false` or a field error. | Unavailable-while-loading flashes the dashed style on every step, mostly on days that do have notes ([Research] NN/g: indicators under about 1 s distract, https://www.nngroup.com/articles/progress-indicators/), and gives a press during that window no reason. The export endpoint is the gate (`404 report.no_notes`, §6.9), so an early press on an empty day ends in the correct empty state. The design's rule ("no submitted notes … both download buttons are disabled") is still met exactly. |
| 3 | **Future date in the URL.** date-navigation.md: replace with today. file-download.md and empty-loading-error.md: show the empty state ("the server says there are no notes"). | Replace with today. | design.md: "Future dates cannot be picked". Showing a future date would also put the field in an invalid state (value above `max`) with Next day unavailable on a day that is not today. |
| 4 | **Busy feedback.** file-download.md: spinner and "Preparing the Word file…" after 1 s, label unchanged. primary-actions.md: busy label "Downloading…" after 400 ms, no spinner. foundations.md and empty-loading-error.md: no spinners anywhere. export-form.md: label after 400 ms plus "Preparing the Word file… Keep this page open." / "Preparing the PDF file… Keep this page open." | No spinner. "Downloading…" on the pressed button after 400 ms, and "Preparing the Word file… Keep this page open." (or PDF) in the download status line at the same moment. | One pattern for one mechanism on both download screens (SC 3.2.4). An earlier draft said this matched Export record "word for word" when it did not ("Preparing the file." here, "Preparing the PDF file…" there); the editorial pass aligned both on the line that names the format, which confirms the button the manager pressed. Keeps motion out of the app (foundations.md) and still announces the wait to screen-reader users, who may not hear a focused button's name change. |
| 5 | **Success message.** primary-actions.md: none ("the browser's own download UI confirms them"). file-download.md: "{file} is ready. Look in your downloads." export-form.md: "Exported: {file}". | "{file} is ready. Look in your downloads.", **the same line on Export record** (editorial pass) | The browser's download UI is not reliably announced to screen readers (unverified, as export-form.md notes), and without a line a fast download ends in silence. "Is ready" stays true when iPhone Safari asks first or offers View; "Downloaded" would not. "Look in your downloads" tells a low-confidence user where the file went. The file name helps the manager find the right day among several downloads. It is a status line, not a notification, and it adds no data. |
| 6 | **Failure placement and wording.** file-download.md: in the status line under the buttons, red with an icon; "The file was not downloaded: no connection…", "Sorry, there was a problem making the file…". primary-actions.md: a `role="alert"` above the buttons; "Didn't download: no connection. Try again.", "…something went wrong…". empty-loading-error.md: "Did not download…". microcopy.md (the wording owner): "Not downloaded: no connection. Try again." | A `role="alert"` container above the buttons (primary-actions.md, as on Export record), bold `--colour-error`, no icon. Wording from microcopy.md's "Action failed" pattern; 429 from export-form.md's shape. | microcopy.md owns canonical wording and bans "sorry" and negative contractions in new copy ([Convention] GOV.UK style, https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/). The placement keeps one app-wide rule for action failures and matches the other download screen. |
| 7 | **In-screen navigation and the shell.** date-navigation.md: tell the shell with `state: { inScreen: true }`. app-shell-nav.md: focus the `<h1>` and reset or restore scroll on every screen change. An earlier draft of this file: compare matched routes and use `<ScrollRestoration getKey>`. | The route declares `handle: { screenKey: 'dailyReport' }`; app-shell.md's rule treats a shared `screenKey` as one screen (push, replace or pop) and keys its in-memory scroll map by it. No `<ScrollRestoration>`. | The state flag misses browser Back to the first report entry, which the nav link created without the flag. `<ScrollRestoration>` writes to `sessionStorage`, which D22 and app-shell.md forbid. The `screenKey` covers every case with one shell rule and stores nothing on the device. |
| 8 | **Re-keying the downloads.** file-download.md renders the download block with `key={date}`. date-navigation.md: never key anything on this screen by date. | No key. The download hook clears its messages and aborts any request when the URL date changes to a different day. | A keyed block remounts the buttons, which drops focus if a keyboard user presses browser Back (or Alt+Left) while on a download button. |
| 9 | **A typed or picked date that is not yet committed.** date-navigation.md: downloads unavailable until the field matches the URL. primary-actions.md: a press on an unavailable control must never simply do nothing. | Buttons keep their look; a press commits the field with Enter's checks, then downloads that date (or shows the field error and focuses the field). | Avoids a 750 ms flash of the unavailable style after every pick, and a dead press when the order of blur, re-render and click differs between browsers (iOS does not focus buttons on tap; unverified detail). The rule "a press never downloads a day other than the one in the field" still holds, which is what date-navigation.md was protecting. |
| 10 | **`aria-describedby` on the downloads.** file-download.md and primary-actions.md: only while unavailable. date-navigation.md's sketch: always. | Always, to the date status line. | On a normal day the screen reader then says which day the button downloads, a cheap guard against the wrong day; on an empty day it gives the reason. |
| 11 | **Shared Button behaviour.** primary-actions.md's `Button` has no way to block presses without a busy label or the unavailable look (with `busy` and no `busyLabel`, its CSS hides the label), and it replaces `aria-describedby` with the reason id. | Add a `blocked` prop and merge `describedBy`. | Needed for "both buttons ignore presses during a download, only the pressed one says Downloading…" and for the permanent date description. No visible change elsewhere. |
| 12 | **403.** file-download.md: "Only managers can download these files." empty-loading-error.md: any `403` from a manager-only request shows "Page not found". | "Page not found" via the role guard (`/me` is refreshed, the nav rebuilds). | One rule app-wide, matching the API's 404-not-403 stance (§9.2); the case only arises after a role change mid-session. |
| 13 | **Sizes and type.** primary-actions.md's Button CSS: `2.75rem` (44 px), breakpoint `40em`. file-download.md: 48 px, weight 600, `min-inline-size: 12rem`. foundations.md: buttons `max(3rem, 48px)`, weights 400/700, breakpoint `40rem` written literally. | 48 px, weight 700, `40rem`, both download buttons at least `12rem` wide on a laptop. | foundations.md owns tokens; 600 renders as Semibold on iPhone and Bold on Android ([Standard] CSS font matching); equal widths show the two formats as equals. |
| 14 | **Date formatters.** date-navigation.md: `formatLong` in `src/lib/dates.ts` (no non-breaking space). foundations.md: `src/lib/formatDate.ts`. microcopy.md: `dateLong` in `src/copy/format.ts` (non-breaking space). | Display: microcopy.md's `dateLong`. Arithmetic and validation: date-navigation.md's `isIsoDate` and `addDays`. | One formatter for every displayed date, with the Style Manual's non-breaking space; one place for calendar arithmetic. |
| 15 | **Reason for the unavailable Next day.** microcopy.md: every disabled control has a visible reason next to it. primary-actions.md: none needed. | No new text. | design.md gives no string, the field and status line show the chosen day, and "Next day" being unavailable on the latest possible day explains itself. Adding a sentence would be new copy for little gain [Opinion]. |
| 16 | **Download timeout.** empty-loading-error.md's `api()` applies `AbortSignal.timeout(10_000)` to every request. file-download.md uses the wrapper with only an abort signal. | Downloads pass the wrapper's per-call `timeoutMs: 30_000` (now in empty-loading-error.md's `api()` sketch), plus the screen's abort signal. | M4 accepts up to 10 s for 20 notes; a 10 s cut-off would report "no connection" for a slow but working download. 30 s matches M5's export target, and Export record now passes the same option, so both download screens share one value [Opinion]. |
| 17 | **Status line position.** file-download.md: under the buttons (this file's earlier draft). export-form.md, primary-actions.md and Export record: above the button. | Above the buttons: alert, then status line, then the downloads. | One position on both download screens (SC 3.2.4); the line stays in view with the buttons at the bottom of a phone screen. |

---

## Tensions with decisions

Recorded once, with evidence; no change recommended. Each is built as design.md says, with the mitigations above.

- **A native date field (design.md §4.7).** GOV.UK and NHS avoid `type="date"` [Convention]
  https://design-system.service.gov.uk/components/date-input/. Hassell Inclusion's 2019 testing found Dragon could not
  use it and VoiceOver on iOS did not read its errors [Research, dated]
  https://hassellinclusion.com/blog/input-type-date-ready-for-use/; Roselli calls it a "problem for voice users"
  [Research, practitioner] https://adrianroselli.com/2019/07/maybe-you-dont-need-a-date-picker.html; iOS still ignores
  `max` (WebKit 225639). NN/g and MOJ support a picker for chosen dates near today, which is this use. Mitigations: the
  step links (a voice user can say "click Previous day"), the date in words, code and server checks.
- **"Both download buttons are disabled" and "Next day is disabled on today" (§4.7, A18, §11.4).** GOV.UK: "avoid them
  if possible"; AgDS: "Avoid using disabled buttons" because they hide the reason, drop out of keyboard reach and are
  hard to see [Convention] https://design-system.service.gov.uk/components/button/ ·
  https://design-system.agriculture.gov.au/components/button; GOV.UK pagination hides an unusable next link
  https://design-system.service.gov.uk/components/pagination/. Mitigations: `aria-disabled` keeps them focusable, the
  reason is visible and linked, labels stay at 8.1:1, a dashed border marks the state.
- **Report files on personal phones (D21, D22, §9.6, §12).** On an iPhone, Safari's download folder is reported to be
  in iCloud Drive by default (How-To Geek 2019; current default unverified), and Chrome on iPhone offers saving to
  Google Drive (https://support.google.com/chrome/answer/95759?hl=en&co=GENIE.Platform%3DiOS). A downloaded daily report
  can then sit in a personal cloud account. The app cannot see or control this; §12 already places exported files under
  the provider's records policy.

---

## Open questions

For the owner unless marked otherwise. None of them adds a feature.

1. Approve the proposed copy: heading "Daily report"; label "Date"; the chosen date on its own in words as the status
   line ("Wednesday 30 September 2026"); "Enter a date"; "Date must be today or in the
   past"; "Downloading…"; "Preparing the Word file… Keep this page open." (and PDF); "{file name} is ready. Look in your downloads.";
   "Not downloaded: no connection. Try again."; "Not downloaded: something went wrong. Try again."; "Not downloaded:
   too many downloads in the last minute. Wait a minute, then try again."
2. Should the report date have an earliest allowed date (for example go-live)? design.md sets none, so none is set;
   earlier days simply show "No submitted notes for …". Dates before go-live are covered by the Word records (A39).
3. For testing (not the owner), in M4 on real devices under the production CSP: whether iPhone Safari, iPhone Chrome,
   Android Chrome and Samsung Internet save a `blob:` download (decides file-download.md's plain-link contingency);
   whether iPhone Safari's download prompt appears for a scripted click; the date field's `change` timing while typing
   in Chrome, Edge, Firefox and Safari; how NVDA, VoiceOver and TalkBack read the native field and the
   "dimmed/unavailable" link; and whether the 750 ms commit pause feels right to managers.
