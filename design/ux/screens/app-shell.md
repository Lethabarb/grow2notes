# App shell (global)

Screen spec for design.md §4.0 "Navigation and shared behaviour". It also covers the parts of §4.6 (the Flagged badge),
§6.8 (session and badge refresh), §8.5 (idle timeout, sign-out) and §9.6 (personal devices) that live in the frame
around every screen. It composes these component specs and adds nothing to them:

- [app-shell-nav.md](../components/app-shell-nav.md) (header, navigation, account menu, titles, route focus)
- [notification-badge.md](../components/notification-badge.md) (the Flagged count)
- [session-timeout.md](../components/session-timeout.md) (the 28-minute warning and signing in again in place)
- [status-messages.md](../components/status-messages.md) (the in-memory message store and the route-focus rule)
- [empty-loading-error.md](../components/empty-loading-error.md) (start-up, loading and whole-page messages)
- [foundations.md](../components/foundations.md) (tokens, sizes, focus ring, breakpoint, motion)
- [microcopy.md](../components/microcopy.md) (glossary, voice, page names, canonical wording)

Copy marks: **(V)** word for word from design.md · **(N)** design.md wording with format normalised per microcopy.md ·
**(P)** proposed, not in design.md, needs the owner's approval. Evidence grades: [Research] · [Standard] ·
[Convention] · [Opinion]. Where two component specs disagreed, the choice made here is listed under "Conflicts
resolved".

---

## Purpose and who uses it

**Purpose.** Give every signed-in screen the same frame: a way to skip to the content, the app name, the navigation
for the person's role, Sign out, the managers' Flagged count, a generic page title, and the session rules (warning at
28 minutes, signed out at 30, back to the same screen after signing in). The shell also handles what happens before
any screen can show: starting up, no connection, a broken link and a crash. It adds no screen content of its own,
except the four-link Manage page (see Open questions).

**Who uses it.**

| Person | Device | What the shell gives them |
|---|---|---|
| Support worker | Own phone, often tired at the end of a shift, often English as a second language, sometimes dictating | One row: Grow2Notes, **Today**, **Account** (Sign out). Nothing else to learn. The header never covers the note or the keyboard. |
| Manager | Laptop and phone | Two rows: Grow2Notes and **Account**, then **Today**, **Flagged** with the To review count, **Report**, **Manage**. Everything visible at 320 px, nothing behind a menu. |
| Anyone not signed in | Any | The name "Grow2Notes" as plain text and the §4.1 sign-in screen, shown at the address they opened. |

**Non-negotiables from the design** (each is tested in Acceptance criteria):

- No participant name, note text or other personal data in page titles, URLs, query strings, router `state`, browser
  storage or the message store's persistence (§4.0, §9.6, D22). Titles are "Grow2Notes – {page name}" (V pattern).
- No polling and no push. The badge and `today` refresh only on page load, screen change, window focus and the page
  becoming visible (§4.6, §6.8).
- Nothing is stored on the device. Everything the shell remembers (scroll positions, the return row, where each
  screen was opened from, the success message, the arrival-focus flag, the signed-in user's ID) lives in memory and is
  gone on reload or Sign out (D22, §9.6).
- The parent company's name never appears in the shell, its metadata, file names, tokens or comments (D42).

---

## Layout - phone

At about 375 px wide, default text size. `(3)` is the count pill (digits only). `=====` is the current-item marker
(bold text plus a 4 px bar). `[ ]` is a button. Everything below the header belongs to the screen: an optional
before-main bar (the back link, or the note form's sticky bar; component 3a), then `<main>`.

**Worker, signed in** (one row):

```
+-------------------------------------+
| Grow2Notes  Today        [Account v]|  <header>: wordmark link, <nav> (1 link), Account button
|             =====                   |
+-------------------------------------+
| Today · Thursday 1 October          |  <main>: the screen's <h1> comes first (owned by Today)
| ...                                 |
```

**Manager, signed in** (two rows; the nav row wraps onto a third line at 320 px or 200% text):

```
+-------------------------------------+
| Grow2Notes               [Account v]|  row 1: wordmark link, Account button
| Today  Flagged (3)  Report  Manage  |  row 2: <nav aria-label="Main">
|                     ======          |  (here the Report screen is open)
+-------------------------------------+
| Daily report                        |  <main>
```

**Account open** (an in-flow panel straight under the Account button's row; it pushes the page down, nothing floats):

```
+-------------------------------------+
| Grow2Notes               [Account ^]|
| Signed in as Alex Pham              |  small secondary text
| [             Sign out            ] |  secondary button, full width, 48 px
| (status line, empty until needed)   |  <p role="status">
| Today  Flagged (3)  Report  Manage  |  managers only; workers have no second row
+-------------------------------------+
```

**Starting up** (before `/api/auth/me` answers; the line appears only after 1 second):

```
+-------------------------------------+
| Grow2Notes                          |  plain text, not a link; no nav, no Account
+-------------------------------------+
| Loading…                            |  <p role="status">
```

**Could not connect** (start-up failed; same layout for "There is a problem with Grow2Notes"):

```
+-------------------------------------+
| Grow2Notes                          |
+-------------------------------------+
| Could not connect to Grow2Notes     |  <h1>
|                                     |
| Grow2Notes did not load: no         |  <p role="status">
| connection. Try again.              |
|                                     |
| [            Try again            ] |  secondary button, full width
+-------------------------------------+
```

**Page not found** (signed in, so the header and nav stay usable):

```
+-------------------------------------+
| Grow2Notes  Today        [Account v]|  no item marked current
+-------------------------------------+
| Page not found                      |  <h1 tabindex="-1">
|                                     |
| If you typed or pasted the web      |
| address, check it is correct.       |
|                                     |
| Go to Today                         |  link
+-------------------------------------+
```

**Session warning at 28 minutes** (native `<dialog>` in the top layer; the page behind is inert and dimmed):

```
+-------------------------------------+
|.....................................|  ::backdrop
|. +-------------------------------+ .|
|. | You'll be signed out in       | .|  <h2>, the dialog's name and a polite live region
|. | 2 minutes                     | .|
|. | (status line, empty)          | .|  <p role="status">: "Connecting…" or the error
|. | [       Stay signed in      ] | .|  primary button, full width, 48 px, focused
|. +-------------------------------+ .|
|.....................................|
```

**Signed out in place** (30 minutes idle, or any `401`; same URL, the screen behind is kept in memory but hidden):

```
+-------------------------------------+
| Grow2Notes                          |  plain text; no nav, no Account
+-------------------------------------+
| You've been signed out              |  <h1 tabindex="-1">, takes focus
| Sign in again to go back to where   |
| you were.                           |
|                                     |
| (the §4.1 sign-in controls, with    |  owned by sign-in-form.md; its
|  their headings as <h2>)            |  headings drop one level here
+-------------------------------------+
```

---

## Layout - laptop

The same markup and the same rows at every width. The header has **no structural breakpoint**: its rows use
`flex-wrap`, so they only wrap when space runs out (SC 1.4.10). What changes from `40rem` (640 px) up, all from
foundations.md:

```
+------------------------------------------------------------------------------+
|      Grow2Notes                                                [Account v]   |
|      Today   Flagged (3)   Report   Manage                                   |
|      =====                                                                   |
+------------------------------------------------------------------------------+
|      Today · Thursday 1 October          <- content starts on the same left  |
|      ...                                    edge as the wordmark             |
|      |<------ 40rem reading column ------>|                                  |
|      |<---------------------- 60rem page container (setup screens) ------->| |
+------------------------------------------------------------------------------+
```

- The header's inner container and `<main>`'s container are both centred at `--page-max` (60rem) with
  `--page-gutter` (32 px from 40rem up), so the wordmark, the nav and the content share one left edge [Convention,
  GOV.UK width container]. Screens use the `--measure` (40rem) column, left-aligned; the setup screens (4.8, 4.9,
  4.11) use the full 60rem (§4.0 "the setup screens use the extra space").
- Hover styles appear (inside `@media (hover: hover)` only).
- The account panel stays in the flow under row 1, with its contents aligned to the inline end.
- Headings in `<main>` step up (h1 32 px, h2 24 px); the header's own text sizes do not change.
- Nothing becomes fixed, sticky or a sidebar. No hamburger at any width [Research, weak: NN/g 2016 found hidden
  navigation used least on desktop, 27% against 48% visible; one confounded observational study,
  https://www.nngroup.com/articles/hamburger-menus/].

---

## Components, in order

DOM order equals visual order everywhere. Sizes and colours are foundations.md tokens; no raw values.

### 1. Skip link → [app-shell-nav.md](../components/app-shell-nav.md)

- **Element:** `<a href="#main-content">`, the first element in `<body>`, before the header.
- **Copy:** "Skip to main content" [Convention, GOV.UK, https://design-system.service.gov.uk/components/skip-link/].
- **States:** visually hidden until focused; on focus, shown top-left over the header (`position: absolute`, not
  fixed) on `--colour-surface` with the standard focus ring. Its click handler calls `preventDefault()` and focuses
  `<main>`, so `#main-content` never enters the URL or history and the router sees no navigation.
- Present on signed-out views too (it still leads to `<main>`).

### 2. Header → [app-shell-nav.md](../components/app-shell-nav.md), [foundations.md](../components/foundations.md)

- **Element:** `<header>` (banner landmark), static. **Never `position: fixed` or `sticky`** [Standard: Understanding
  1.4.10 says it is "strongly suggested" that such bars are static at small viewports,
  https://www.w3.org/WAI/WCAG22/Understanding/reflow.html; technique C34; SC 2.4.11 names sticky headers as a cause of
  hidden focus]. A worker types or dictates with the keyboard open for most of a note, and since Chrome 108 the
  on-screen keyboard covers fixed bottom content [Standard, vendor,
  https://developer.chrome.com/blog/viewport-resize-behavior]. So: no bottom tab bar either [Convention: tab bars are
  a native-app pattern; GOV.UK, NHS and AgDS put web navigation at the top].
- Background `--colour-surface`, a 1 px `--colour-divider` rule underneath (decorative).
- Visible on every signed-in screen, including the note form, where the note's own sticky top bar (owned by
  autosave-status.md and note-form.md) sits below it in the before-main bar slot (component 3a). The header is not
  hidden on the note form: §4.6 puts the badge
  on every manager screen [Research, unquantified: GOV.UK One Login reports "no evidence that the header distracts
  users in a service journey"].

**2a. Wordmark**

- **Copy:** "Grow2Notes" (D42). Text only, no logo file, bold, `--font-size-body`, `--colour-text`.
- **Signed in:** a link to `/` (Today). It sits beside an explicit Today link: less technical users often do not
  know a logo is a link [Research, NN/g, https://www.nngroup.com/articles/homepage-links/].
- **Starting up, signed out, or shell crashed:** plain text, not a link.
- Target at least 48 px tall.

**2b. Main navigation** (`<nav aria-label="Main">` holding a `<ul>` of links)

| Role | Items, in this order | Source |
|---|---|---|
| Worker | Today | §4.0 (V) |
| Manager | Today · Flagged (with badge) · Report · Manage | §4.0 (V) |

- Plain `<a href>` links in `<li>`s. **No `role="menu"`, no dropdown, no "More", no hamburger** at any width; one or
  four short items fit at 360 px [Convention + Opinion]. Items are never disabled or greyed; workers simply do not get
  manager items (SC 3.2.3 allows inserted or removed items).
- **Not rendered until `/me` has answered**, because the role decides the items. No placeholder items.
- **Link targets:** Today → `/` · Flagged → `/flagged` · Report → `/reports/daily/{me.today}` (always today's
  Melbourne date from the server, so it opens on today with no redirect, §4.7) · Manage → `/manage`.
- **Current item:** exactly one item per set carries `aria-current`, plus two non-colour cues: **bold** and a 4 px
  `--colour-action` `border-bottom` set **only on the current item** (so it survives forced colours) [Standard:
  SC 1.4.1; MDN aria-current].

  | URL | Current item | `aria-current` |
  |---|---|---|
  | `/` | Today | `page` |
  | `/flagged` (both views, `?view=reviewed` included) | Flagged | `page` |
  | `/reports/daily/{date}` (any date) | Report | `page` |
  | `/manage` | Manage | `page` |
  | `/manage/…` (Participants, Common items, Guide prompts, Users and their detail pages, Write past-day note at `/manage/participants/{id}/past-day-note`, Export record at `/manage/participants/{id}/export`) | Manage | `true` |
  | Note form, read view, Past notes, Version history, Page not found, any other | none | none |

  Note screens mark nothing, because the same note is reached from Today, Flagged and Manage [Opinion,
  app-shell-nav.md]. Built with a small `NavItem` that maps the URL to `'page' | 'true' | undefined`, **not** React
  Router's `NavLink`, which always writes `"page"` and matches child routes
  (https://reactrouter.com/api/components/NavLink).
- **Look:** link text in `--colour-text`, `--font-size-body`, no underline at rest; `min-block-size: 48px`
  (`--target-button`); `--space-1` between items. Hover (laptop): underline, thickness `max(3px, 0.1875em)`. Pressed:
  `--colour-pressed` fill, instant.

**2c. Flagged count badge** (managers) → [notification-badge.md](../components/notification-badge.md)

- **Markup inside the Flagged link:** the text "Flagged", then a pill `<span aria-hidden="true">3</span>`, then a
  visually hidden `<span>, 3 to review</span>`. **Accessible name: "Flagged, 3 to review"** [Convention: Ministry of
  Justice badge markup, https://design-patterns.service.justice.gov.uk/components/notification-badge/; Standard:
  SC 2.5.3, the visible label starts the name]. No `aria-label` (it does not reliably translate [Research,
  practitioner testing, https://adrianroselli.com/2019/11/aria-label-does-not-translate.html]).
- **Source:** `toReviewCount` from `GET /api/auth/me`, memory only.
- **Show the exact number**, no "99+" cap (caps are convention only, and the number must match the Flagged screen's
  "To review (n)" tab) [Opinion].
- **At 0:** no pill and no hidden text; the link reads "Flagged". Never "0", never a dot.
- **Not a live region, no animation.** Changes are silent; the count is read when the person reaches the link
  [Standard: SC 4.1.3 covers results of the user's own action; Convention: MoJ "It's not 'dynamic'"].
- **Refresh failed:** keep the last known number; show nothing extra.
- **Look:** pill in `--colour-badge` (#0b0c0c) with `--colour-on-badge` (#fff) digits, 19.6:1; `--font-size-small`
  (16 px, the app's minimum), bold, `font-variant-numeric: tabular-nums`; `min-inline-size: 1.6em`, `border-radius:
  999px`, sizes in `em`; `border: 1px solid transparent` so the shape shows in forced colours. **Not red**: red is kept
  for errors such as "Not saved" (invariant I11), and the pill must look different from the row-level "Flagged" tag
  (amber). One focus ring wraps the label and the pill together.
- Never echoed in the title, favicon, app badge, Today header or account panel.

**2d. Account button and panel** → [app-shell-nav.md](../components/app-shell-nav.md)

- **Button:** `<button type="button" aria-expanded aria-controls="account-panel">`. **Copy: "Account"** (P) plus a
  decorative chevron (`aria-hidden="true"`, `currentColor` SVG). Secondary button look (2 px `--colour-action`
  border, `--colour-action` text), at least 48 px tall, pushed to the inline end of row 1. Never opens on hover.
- **Panel:** `<div id="account-panel" hidden>` placed straight after row 1, in the flow (APG disclosure pattern,
  **not** `role="menu"` [Standard, https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/]).
  Contents, in order:
  1. "Signed in as {displayName}" (P), `--font-size-small`, `--colour-text-secondary`; `overflow-wrap: anywhere`
     (display names are up to 100 characters). Helps on shared organisation devices (D21) [Convention, NHS and AgDS
     headers show the user].
  2. **Sign out** (V), secondary button, full width on a phone, label width from 40rem, 48 px tall.
  3. `<p role="status">`, always rendered while the panel is open, empty until a sign-out problem.
- `.panel[hidden] { display: none; }` so a `display: flex` rule cannot override `hidden`.
- No confirmation dialog on Sign out: drafts are already on the server, so signing out loses nothing that was saved
  [Opinion].

### 3. Main region and route outlet → [app-shell-nav.md](../components/app-shell-nav.md), [status-messages.md](../components/status-messages.md), [empty-loading-error.md](../components/empty-loading-error.md)

- **Element:** exactly one visible `<main id="main-content" tabindex="-1">` holding the route outlet. No footer (the
  design specifies none). Inner container `--page-max`, `--page-gutter` sides, `--space-5` above the content and
  `--space-8` below it.
- **Each screen renders one `<h1>`** through the shared `PageHeading` (`tabindex="-1"`, `width: fit-content`,
  `max-inline-size: 100%`). The shell owns arrival focus; screens only render headings and banners.
- **Page title:** "Grow2Notes – {page name}" with an en dash (U+2013) and spaces (V pattern, §4.0). The page name
  comes from a fixed TypeScript union, so no name or data can reach a title. Each route declares its name once
  (`handle: { pageName }`); the shell sets `document.title`; whole-page messages and the signed-out view override it
  while shown; a form may prefix "Error: " after a failed submit [Convention, GOV.UK/HMRC]. `index.html` keeps
  `<title>Grow2Notes</title>` for the moment before JavaScript runs. Do **not** also render React 19's `<title>`
  component: two titles give undefined behaviour (https://react.dev/reference/react-dom/components/title).
- **Page names** (the complete union, rebuilt from every screen's route table; a screen that needs another adds it
  here first): Sign in · Set up your account · Today · Note · Past notes · Version history · Flagged notes ·
  Daily report · Manage · Participants · Add participant · Participant · Write past-day note · Common items ·
  Guide prompts · Users · Invite user · User · Edit user · Export record · Page not found · There is a problem ·
  Could not connect. "Note" covers the note form (new, draft and edit), the read view and the whole-page "You
  cannot edit this note" message. sign-in.md uses only "Sign in" and "Set up your account" for all its views.
- **Message store** (status-messages.md): a module-level variable holding at most one success message (for example
  "Note for Jane Citizen submitted"). Memory only. Never `navigate(…, { state })` (some browsers save history state
  to disk [Standard, https://developer.mozilla.org/en-US/docs/Web/API/History/pushState]), never the URL, never
  storage. Today reads it once and clears it; a reload or Sign out empties it.
- **No global live region, no route announcer, no toast container.** The focused heading or banner announces each
  new screen.
- **Page status region (one per page, owned by the page).** Every screen renders one visually hidden
  `<p role="status">` through the shared `PageStatus`, from its first render and never conditionally mounted (the
  pattern participants.md B8 first set out). `PageStatus` provides a React context. The shared `Button`
  (primary-actions.md) writes its busy label into the nearest page region; pages write their own results (for example
  the list-editor announcements) through the same context's `announce()`, which clears the text and writes it again
  on the next frame so a repeated message is spoken again. A button whose screen already has its own status line opts
  out with `announceBusy={false}`: Export record, the Daily report downloads and Guide prompts' Save (`SaveStatus`).
  Dialogs use their own status line (confirm-dialog.md). Because the region belongs to the page, the shell still has
  no global live region. [Standard] SC 4.1.3; [Research] O'Hara, https://www.scottohara.me/blog/2022/02/05/are-we-live.html

### 3a. Before-main bar slot and the shared BackLink → [note-identity-header.md](../components/note-identity-header.md)

- **Slot.** The shell renders `<div id="page-bar">` between `<header>` and `<main>`. A route fills it through the
  shared `PageBar` component (a portal into the slot); with nothing in it the slot takes no space
  (`#page-bar:empty { display: none }`). It is not a landmark: GOV.UK puts the back link before `<main>` so "Skip to
  main content" passes it [Convention, https://design-system.service.gov.uk/components/back-link/]. axe's
  best-practice `region` rule may flag it; that is not a WCAG failure.
- **Hidden with the route.** In the signed-out-in-place state the slot gets `hidden` together with the route outlet,
  so a bar ("‹ Today", "Saved 9:42 am") is never visible or usable over the sign-in view. The route stays mounted, so
  its portal content stays mounted too, hidden, and comes back unchanged after signing in as the same person.
- **What goes in it, and nothing else:** every back link in the app, and on the note form its sticky top bar (back
  link, save indicator, failed-save and blocking banners; note-form.md). The bar is static on every screen except the
  note form (foundations.md: the note form's top bar is the only sticky element).
- **BackLink, one component for every screen** [Standard SC 3.2.4; Convention GOV.UK back link, Android Up]:
  - React Router `<Link>` (a real `<a href>`, IDs and dates only), a decorative chevron SVG (`aria-hidden="true"
    focusable="false"`), hit area at least 44 × 44 px, never disabled.
  - **Label = the destination's name**, nothing more: "Today", "Past notes", "Note", "Version history", "Flagged",
    "Participant", "Participants", "Users", the user's display name ("Alex Park"), "Set up your account", "Create a
    password". The accessible name is the visible label (SC 2.5.3). No "Back to …" prefix and no bare "Back".
  - **Pop or push:** a plain click calls `navigate(-1)` when the previous history entry is the destination (the
    shell's opened-from memory, below, says so); otherwise it follows its `href` (a push). Modified clicks always
    follow the `href`. So the back link and browser Back land the same way, with the list's return point.
  - Where a screen can be opened from several places (note screens, the past-day page), the label and `href` come
    from the opened-from memory; after a reload, a pasted URL or a new tab, the route's default (Today for note
    screens).

### 4. Shell loading and whole-page messages → [empty-loading-error.md](../components/empty-loading-error.md)

| Situation | Shows | Page title |
|---|---|---|
| Before JavaScript runs | `index.html`: `<p class="boot">Loading…</p>`, made visible after 1 s by a CSS animation delay in the bundled stylesheet (CSP forbids inline styles, §9.7) | Grow2Notes |
| JavaScript turned off | `<noscript>`: "Grow2Notes needs JavaScript. Turn it on in your browser settings, then reload this page." (P) | Grow2Notes |
| `/api/auth/me` pending | Header with the wordmark as text; in `<main>` an always-rendered `<p role="status">` that says "Loading…" (P) only after 1 s | Grow2Notes |
| `/me` → no connection or 10 s timeout | `<h1>` "Could not connect to Grow2Notes" (P), status line "Grow2Notes did not load: no connection. Try again." (P, microcopy.md §9), **Try again** | Grow2Notes – Could not connect |
| `/me` → `5xx`, `429` or anything unexpected | `<h1>` "There is a problem with Grow2Notes" (P), status line "Grow2Notes did not load: something went wrong. Try again." (P, microcopy.md §9), **Try again** | Grow2Notes – There is a problem |
| `/me` → `401` | The §4.1 sign-in screen in `<main>`, at the URL that was opened (not an error, no redirect) | Grow2Notes – Sign in |
| Unknown route; `404` on a screen's main request; a worker on a manager route; a `403` from a manager-only request | `<h1>` "Page not found" (P), "If you typed or pasted the web address, check it is correct." (P), link **Go to Today** | Grow2Notes – Page not found |
| `403 note.not_editable` on the note form's edit route (an old link or a stale screen) | In `<main>`, nav usable: `<h1>` "You cannot edit this note" (P), "Only the person who wrote it, or a manager, can edit it." (P), link **Read the note** to the read view (empty-loading-error.md) | Grow2Notes – Note |
| A screen crashes while rendering | In `<main>`, nav still usable: `<h1>` "There is a problem with Grow2Notes" (P), "Anything already saved is kept." (P), link **Go to Today** as a **full page load** (`<a href="/">`) | Grow2Notes – There is a problem |
| The shell itself crashes | The same page with the wordmark as text and no nav | Grow2Notes – There is a problem |

- One silent retry for network failures, timeouts and `5xx`, then the message; never retry a `4xx`. Queries use
  `networkMode: 'always'`, so an offline phone reaches the message in about a second instead of sitting on
  "Loading…" for ever [Convention, TanStack, https://tanstack.com/query/v5/docs/framework/react/guides/network-mode].
- No spinner, no skeleton, no illustration, no status code, no red, no "oops" or "sorry". The cause words "no
  connection" and "something went wrong" appear only inside microcopy.md §9's shapes ("… did not load: [cause]. Try
  again." and "Not [done]: [cause]. Try again."), so every failure names what failed and the next step
  [Research: NN/g no indicator under about 1 s, https://www.nngroup.com/articles/response-times-3-important-limits/;
  Convention: GOV.UK page-not-found and problem pages; NHS App error pages,
  https://design-system.nhsapp.service.nhs.uk/patterns/error-page/].
- Page not found also covers the role guard on purpose: it matches the API's 404-not-403 stance (§9.2) and never
  reveals that a manager screen exists.

### 5. Manage page → [app-shell-nav.md](../components/app-shell-nav.md)

- Route `/manage`, title "Grow2Notes – Manage". `<h1>` "Manage", then a `<ul>` of four links, each a full-width row
  at least 48 px tall, in the design's order: **Participants · Common items · Guide prompts · Users** (V). No
  descriptions, no counts, no icons.
- This is how the "Manage (…)" group in §4.0 is reached on both phone and laptop with one mechanism: no dropdown, no
  second keyboard model, and Back works. See Open questions: it could be read as a new screen.

### 6. Session timeout warning → [session-timeout.md](../components/session-timeout.md)

- **Element:** one native `<dialog aria-labelledby="session-timeout-heading">`, always in the DOM, mounted once by
  the shell **after** `<main>` and outside anything that can be `hidden` or `inert`. Opened with `showModal()`.
- **Contents:** `<h2 id="session-timeout-heading" aria-live="polite" aria-atomic="true">` with the countdown
  sentence; `<p role="status">` (empty unless needed); one **Stay signed in** button (V), primary, full width, 48 px.
  No close X, no Sign out link, no extra explanation.
- **Countdown copy** (changes only at these marks, on screen and in the announcement together):

  | Idle time | Heading |
  |---|---|
  | 28:00 | You'll be signed out in 2 minutes (V) |
  | 29:00 | You'll be signed out in 1 minute (P, same sentence) |
  | 29:20 | You'll be signed out in 40 seconds (P) |
  | 29:40 | You'll be signed out in 20 seconds (P) |

  [Research: Home Office found minutes, then 20-second steps, "worked best for both sighted and non-sighted users";
  https://design.homeoffice.gov.uk/design-system/patterns/help-users-to/manage-service-timing-out]
- **Status line copy:** after 1 s waiting, "Connecting…" (P); on a network failure, "No connection. Try again." (P,
  microcopy.md §9: the dialog has one action, so the cause and the next step are enough).
- **Look:** `--colour-surface`, 1 px `--colour-text` border, `--radius-m`, `--colour-backdrop`;
  `inline-size: min(100% - 2rem, 28rem)`; `max-block-size: calc(100dvh - 2rem)` with internal scroll; heading at
  `--font-size-h2`; **no open or close animation**.

### 7. Signed-out-in-place view → [session-timeout.md](../components/session-timeout.md) (sign-in controls from sign-in-form.md)

- Shown at 30:00 idle on a visible page, or on any `401` from the shared `api()` wrapper (except `/api/auth/login*`,
  `/api/auth/setup/*` and `/api/auth/logout`).
- **Structure (one `<main>`):** the header drops the nav and Account and shows the wordmark as text. The before-main
  bar slot gets `hidden` (component 3a). Inside `<main>`:
  the signed-out block, then `<div hidden>` wrapping the route outlet, **still mounted**, so unsaved note text,
  ticks, a half-typed review comment or a half-filled Manage form survive in memory. The HTML spec allows only one
  `<main>` without `hidden` (https://html.spec.whatwg.org/multipage/grouping-content.html#the-main-element), so the
  route is hidden inside the same `<main>`, not beside it.
- **Copy:** `<h1>` "You've been signed out" (P), then "Sign in again to go back to where you were." (P, microcopy.md
  canonical), then the §4.1 sign-in controls with their own headings as `<h2>`. Title "Grow2Notes – Sign in".
- **Same URL, no reload.** A reload would lose the unsaved text and the autosave `clientId` (§5.6).
- On a fresh page load with no session, the same view is used **without** the "You've been signed out" heading and
  sentence: the sign-in screen's own `<h1>` "Sign in" is the heading. No redirect to `/sign-in`, so a deep link still
  opens the right screen after signing in.

### 8. Document head (index.html)

- `<html lang="en-AU">` (SC 3.1.1). `<meta name="viewport" content="width=device-width, initial-scale=1">`: never
  `user-scalable=no` or `maximum-scale` (SC 1.4.4); no `viewport-fit=cover`, so no safe-area insets.
  `<meta name="color-scheme" content="light">` and `:root { color-scheme: light; }`, without `only`, so a person's
  browser page-darkening still works (foundations.md). `<title>Grow2Notes</title>`.
- `<meta name="format-detection" content="telephone=no">`, so iOS does not turn numbers in note text, times or
  dates into call links (participant-notes.md asked for it; the shell owns `index.html`).
- No favicon, meta tag, file name or bundle string carries the parent company's name (D42).

---

## States

Every state the shell can be in. Screen states (empty lists, banners) belong to each screen's own spec.

| State | Trigger | What shows | Exact copy | Announced |
|---|---|---|---|---|
| **Before JavaScript** | Page requested | `index.html` line after 1 s | "Loading…" (P) | No (plain text) |
| **JavaScript off** | Scripts blocked | `<noscript>` paragraph | "Grow2Notes needs JavaScript. Turn it on in your browser settings, then reload this page." (P) | Read in order |
| **Starting up** | `/me` pending | Wordmark as text; status line after 1 s | "Loading…" (P) | Polite, once |
| **Could not connect** | `/me` network failure or timeout (after one silent retry) | Whole-page message + Try again | `<h1>` "Could not connect to Grow2Notes" · "Grow2Notes did not load: no connection. Try again." · button "Try again" (P) | Read in order (first load: focus not moved) |
| **Still could not connect** | Try again failed the same way | Status line text changes; focus stays on Try again | "Grow2Notes still did not load: no connection. Try again." (P) | Polite |
| **Server problem** | `/me` `5xx`, `429` or unexpected | Whole-page message + Try again | `<h1>` "There is a problem with Grow2Notes" · "Grow2Notes did not load: something went wrong. Try again." · "Try again" (P) | Read in order |
| **Still a server problem** | Try again failed the same way | Status line text changes | "Grow2Notes still did not load: something went wrong. Try again." (P) | Polite |
| **Not signed in** | `/me` `401` on start-up | §4.1 sign-in at the opened URL | Owned by sign-in-form.md ("Sign in", (V) primary action) | Read in order |
| **Signed in: worker** | `/me` `200`, role Worker | One row: wordmark link, Today, Account | "Grow2Notes" · "Today" · "Account" | — |
| **Signed in: manager, nothing to review** | Role Manager, `toReviewCount` 0 | Two rows, Flagged with no pill | "Today" · "Flagged" · "Report" · "Manage" | — |
| **Signed in: manager, flags waiting** | `toReviewCount` ≥ 1 | Pill with the exact number | Visible "Flagged 3"; name "Flagged, 3 to review" | No (read on reaching the link) |
| **Badge refreshing / refresh failed** | Any refresh trigger | Previous number stays | — | No |
| **Current item** | URL table in component 2b | Bold + 4 px bar on one item | "Today, current page, link" (wording varies by screen reader) | On reaching the link |
| **Account closed** | Default | Button with chevron down | "Account" · "Account, button, collapsed" | — |
| **Account open** | Account pressed | In-flow panel | "Signed in as Alex Pham" (P) · "Sign out" (V) | "expanded" |
| **Signing out** | Sign out pressed | Button `aria-disabled="true"`; label changes after 400 ms | "Signing out…" (P) | — |
| **Sign-out blocked: note not saved** | `flushNow()` failed (no connection) | Error line in the panel; button ready again | "Not signed out: your note is not saved yet. Keep this page open; retrying." (P, microcopy.md canonical) | Polite (panel status line) |
| **Sign-out failed: no connection** | Logout request failed or timed out | Same place | "Not signed out: no connection. Try again." (P) | Polite |
| **Sign-out failed: server** | Logout `5xx` | Same place | "Not signed out: something went wrong. Try again." (P, microcopy.md §9) | Polite |
| **Sign-out stopped by a page** | Guide prompts has unsaved text and its leave dialog was answered **Stay** | Nothing sent; panel and page unchanged | Owned by guide-prompts.md | Focus back on Sign out |
| **Signed out (own choice)** | Logout `204` or `401` | Full reload of `/`, then "Not signed in" | — | — |
| **Page not found** | Unknown route, `404`, role guard, manager `403` | Whole-page message in `<main>`, nav usable | `<h1>` "Page not found" · "If you typed or pasted the web address, check it is correct." · link "Go to Today" (P) | Through focus on the `<h1>` |
| **Crashed (screen)** | Route error boundary | Whole-page message in `<main>`, nav usable | `<h1>` "There is a problem with Grow2Notes" · "Anything already saved is kept." · link "Go to Today" (P) | Through focus on the `<h1>` |
| **Crashed (shell)** | Root error boundary | Same, wordmark as text, no nav | Same | Read in order |
| **Session warning** | 28:00 since the last accepted request, page visible, nothing in flight | Modal dialog | "You'll be signed out in 2 minutes" · "Stay signed in" (V) | Dialog name on focus |
| **Session countdown** | 29:00, 29:20, 29:40 | Heading text changes | "…in 1 minute" / "…in 40 seconds" / "…in 20 seconds" (P) | Polite, at each step |
| **Staying signed in** | Stay signed in pressed | Button `aria-disabled`; after 1 s status line | "Connecting…" (P) | Polite |
| **Stay signed in failed** | Ping network failure or 10 s timeout | Dialog stays open, button ready, countdown continues | "No connection. Try again." (P) | Polite |
| **Signed out in place** | 30:00 visible, or any `401` | Signed-out block, route hidden but mounted | "You've been signed out" · "Sign in again to go back to where you were." (P) | Through focus on the `<h1>` |
| **Back after signing in (same person)** | `me.userId` matches | Route unhidden exactly as it was; autosave resumes | — | Through focus on the page `<h1>` |
| **Different person signed in** | `me.userId` differs (shared laptop, §8.2) | Full reload to Today | — | — |
| **Disabled** | Never | Nav links, the wordmark, Account and Try again are never disabled. Busy buttons use `aria-disabled`, never `disabled`. | — | — |
| **Empty** | Not applicable | The shell has no list. The worker's single-item nav is not an empty state. | — | — |

---

## Interactions and focus

### Tab order

- **Worker:** Skip to main content → Grow2Notes → Today → Account → (panel open: Sign out) → `<main>` content.
- **Manager:** Skip to main content → Grow2Notes → Account → (panel open: Sign out) → Today → Flagged → Report →
  Manage → `<main>` content.
- **Signed out / starting up:** Skip to main content → `<main>` content (the wordmark is text).
- **Session warning open:** Stay signed in only (the page is inert).
- No positive `tabindex`; no arrow-key model (these are plain links and buttons); no CSS `order`.

### What happens on each action

| Action | What happens | Focus afterwards | Announced |
|---|---|---|---|
| Tab on a fresh page | Skip link appears top-left | Skip link | "Skip to main content, link" |
| Activate the skip link | `preventDefault()`; nothing added to the URL | `<main>` (no visible ring on `<main>`) | Start of the main content |
| Tap Grow2Notes, Today, Flagged, Report or Manage | New screen (push). Scroll to top. `/me` refreshed. Account panel closes. | The new screen's `[data-route-focus]` element if it has one (success banner, error summary), otherwise its `<h1>` | The focused heading or banner |
| Open any other screen from content (a row, "Past notes", "Go to Today") | Same as above; the opener is recorded on the new entry (route-change rule) | Same | Same |
| Back or Forward to a different screen (browser, or a BackLink that pops) | Scroll restored from the shell's in-memory map, best effort | `[data-route-focus]` if present; otherwise the screen's return point if it declares one (lists: the row the person opened, or the list's fallback); otherwise the `<h1>` with `preventScroll` | The focused element |
| Switch Flagged or Participants view (`?view=`), change the Report date (same `handle.screenKey`), or the Add participant `201` replace (`state.inScreen`) | Same screen: no scroll change. `/me` refreshed (keeps `today` fresh). | **Unchanged** (stays on the tab link, date field, day link or Save) | Owned by those screens' status lines |
| First load of any URL | Nothing scrolls or moves | Where the browser puts it | — |
| Press Account | Panel shows or hides; `aria-expanded` flips | Stays on Account | "expanded" / "collapsed" |
| Escape in the header while the panel is open | Panel closes | Account | "collapsed" |
| Leave the screen while the panel is open | Panel closes | Per the screen-change rule | — |
| Press Sign out | 0. `if (!(await leaveGuard.confirmLeave())) return;` (settings-textarea.md): it resolves `true` at once when no page has registered a guard; on Guide prompts with unsaved text it opens that page's leave dialog, and **Stay** (`false`) cancels the sign-out with nothing sent. 1. `aria-disabled="true"`; label "Signing out…" after 400 ms; repeat presses ignored. 2. If a note form is mounted, `await autosave.flushNow()`. 3. `disarmUnloadGuard()`, so no browser leave prompt fires on the reload. 4. `POST /api/auth/logout` (antiforgery header); `204` or `401` = success. 5. `window.location.replace('/')`: full reload drops all memory and keeps the signed-in screen out of Back; the server's `Clear-Site-Data` clears cache, cookies and storage. | Stays on Sign out while working; after Stay, back on Sign out | Errors through the panel's status line |
| Sign out fails (flush or logout request) | `rearmUnloadGuard()` (the unsaved Guide prompts text is still on screen); error line shown; button ready again | Sign out | The error, politely |
| Window regains focus, or the page becomes visible | `refreshMe()` (one shared request). Badge, `today` and role update silently. A new role rebuilds the nav at once. `401` → signed out in place. Network failure → keep everything as it was. | Unchanged | Nothing |
| A manager marks a note reviewed | The review panel calls `refreshMe()` with its own list refresh, so the badge and "To review (n)" agree | Owned by review-panel.md | Owned by review-panel.md |
| Try again (start-up message) | Clear the status line at once; `aria-disabled`; after 400 ms label "Loading…" (P) | Success: the screen's `<h1>` (the button has gone). Failure: stays on Try again | Failure: the "still did not load" sentence, politely |
| Go to Today (crash page) | Full page load of `/` | Fresh page | — |
| 28:00 idle on a visible page | If a note change is waiting, autosave sends it now instead; if accepted, no warning. If a request is in flight, wait for it. Otherwise: close the account panel, close any open `<dialog>` with a plain `close()` (every confirmation treats that as **Go back**), close the note form's menu, then `showModal()`. | **Stay signed in** | "You'll be signed out in 2 minutes, dialog, Stay signed in, button" |
| 29:00 / 29:20 / 29:40 | Heading text changes | Unchanged | The new sentence, politely |
| Press Stay signed in | `POST /api/auth/ping`; the dialog stays open until the server answers. `204`: close. `401`: signed out in place. Network failure: error line, button ready. | `204`: back to the element focused before the dialog (native). After a closed confirmation: **Submit note** on the form, not the confirm button. | "Connecting…" after 1 s; the error if it fails |
| Escape or Android back on the warning | Treated as Stay signed in (`cancel` with `preventDefault()`, and `close` handled too, because Chromium may skip `cancel` without user activation) | As above | As above |
| Click the backdrop | Nothing | Unchanged | — |
| 30:00 idle on a visible page, or any `401` | Close the warning if open. Pause autosave. Header drops nav and Account. Route hidden, still mounted. Signed-out block shown. Title "Grow2Notes – Sign in". No request is sent at 30:00 to "check" (it would extend the session). | `<h1>` "You've been signed out" | The heading |
| Page hidden | The session clock does nothing | — | — |
| Page visible again | `sessionClock.holdWhile(refreshMe())`: `200` resets the clock and undoes a false "signed out"; `401` → signed out in place; network failure → the clock decides | Unchanged (or the signed-out heading) | — |
| Sign in again, same person | Fresh antiforgery token. Unhide the route. Invalidate the current screen's queries. Resume autosave (same `clientId`, next `seq`). Restore nav, Account and title. Never replay Submit, Save changes, Discard or Mark reviewed. | The page `<h1>` (on the note form, the participant's name) | The heading |
| Sign in again, a different person | `disarmUnloadGuard()`, then `location.replace('/')`: the previous person's unsaved text and cached data are dropped, never saved under the new name, and no leave prompt appears | Fresh page | — |

### The route-change rule (one owner)

The shell is the only code that moves focus or resets scroll on a screen change. Every screen follows this one rule;
no screen keeps its own return-focus variable.

**What counts as a new screen.** A location change is a new screen **unless** one of these holds, checked the same
way for push, replace and pop:
- only the search changed (same `pathname`: the Flagged and Participants `?view=` switches);
- `location.state?.inScreen === true` (participants.md's `/new` → `/{id}` replace after Add participant);
- the previous and next matched routes declare the same `handle.screenKey` (for example
  `handle: { screenKey: 'dailyReport' }` on `/reports/daily/:date`). This also covers browser Back to the first
  report entry, which the nav link created without any state.

**On a new screen:**
1. **First load:** do nothing.
2. **Scroll:** push or replace → top. Pop → the position saved for that entry, best effort.
3. **Arrival focus, first match wins:**
   a. **`[data-route-focus]` wins on every arrival** (push, replace or pop): the success banner after Submit or an
      error summary. It is focused without `preventScroll`, so it scrolls into view at the top of `<main>`. So
      Submit's pop back to Today focuses the banner, not the row and not the old scroll position.
   b. **On pop only, the route's `handle.returnFocus`.** List screens declare it, with an optional fallback. Their
      row links carry `data-return-key={stable key}`; when one is activated, the shell records that key and the
      row's index on the list's history entry. On pop back to that entry, once the list says it is ready
      (`useReturnFocusReady(isReady)`), the shell focuses `main [data-return-key="…"]` with `preventScroll`. If that
      row has gone, it applies the fallback: `'h1'` (the default) or `'sameIndex'` (Flagged: the row now at the same
      index, else the last row, else the `<h1>`). If the list fails to load, the (fallback) `<h1>` takes focus.
   c. **Otherwise the `<h1>`** through `PageHeading` (`preventScroll` on pop). Data headings (a participant's name)
      mount later and take the pending focus when they appear; a failed load's fallback `<h1>` takes it instead
      (empty-loading-error.md).

   A return point is honoured only on pop, so a replace never focuses an old row: Discard on the note form (a replace
   to Today) focuses Today's `<h1>`. Every in-app back link to a list pops when the previous entry is that list
   (BackLink, component 3a), so the back link and browser Back always behave the same.
4. **Not a new screen:** no focus move, no scroll change.

**Memory per history entry (memory only).** One module-level `Map` keyed by `location.key` holds, for each entry:
the scroll position, the return row (key and index), and where the screen was opened from. For routes that declare
a `handle.screenKey`, the scroll position is keyed by the `screenKey` instead, so every report date shares one
position. `history.scrollRestoration = 'manual'`. React Router's `<ScrollRestoration>` is **not** used (it writes to
`sessionStorage`). Nothing here goes into router state, the URL or storage; a reload empties it.

**Where a screen was opened from.** When an in-app link pushes a new entry, the shell records the opener on the new
entry: `{ entryKey, backKey, path }`, where `backKey` comes from the opener route's `handle.backKey` (`'today'`,
`'pastNotes'`, `'flagged'`, `'participant'`, `'note'`, `'versionHistory'`, `'participants'`, `'users'`, `'user'`)
and `path` holds IDs and dates only. A replace (Edit → form, Save changes or Cancel → read view) copies the record to
the new entry. BackLink reads it for its label and `href`, and pops when the record's `entryKey` is the previous
entry. With no record (reload, pasted URL, new tab) BackLink uses the route's default. This replaces every
router-state `back` object the component specs proposed (note-identity-header.md, today.md, participant-notes.md):
`history.state` holds only React Router's key and the boolean `inScreen`.

### Refresh of `/api/auth/me` (badge, `today`, role, name)

One module owns every refresh: `refreshMe()` calls `queryClient.fetchQuery({ queryKey: ['me'], staleTime: 0 })`,
which shares a request already in flight. It runs on: start-up; every `location.key` change; window `focus`;
`visibilitychange` to visible (through the session clock's hold); and after Mark reviewed. The `['me']` observer uses
`staleTime: Infinity` and `refetchOnWindowFocus: false` so nothing else triggers it. **No timer, no
`refetchInterval`, no push** (§6.8). The `QueryClient` default is `refetchOnWindowFocus: false`, so a draft is never
refetched over the text on screen.

**Queries that opt in to `refetchOnWindowFocus: true`** (the complete list; a screen that needs it adds itself here):
Today's `['today', date]` and `['me', 'drafts']` (today.md); Flagged's `['reviews', 'toReview']` and
`['reviews', 'reviewed']` (flagged.md); the Daily report has-notes check (daily-report.md); a participant's Past notes
list (participant-notes.md); `['admin', 'users']` (users.md). Note content (`['notes', participantId, noteDate]`),
drafts and every setup form's data stay off, so nothing a person is reading or editing changes under them.

### Live regions (complete list)

| Region | Exists from | Says |
|---|---|---|
| Start-up status line in `<main>` | First render | "Loading…" after 1 s; the start-up error sentences |
| Account panel status line | When the panel opens (before Sign out can be pressed) | Sign-out errors only |
| Session dialog `<h2>` (`aria-live="polite"`, `aria-atomic="true"`) | Always (the dialog is always in the DOM) | The countdown steps |
| Session dialog status line | Always | "Connecting…", the connection error |

Nothing else in the shell is live: not the badge, not route changes, not notices, not the signed-out view (it takes
focus instead). No `role="alert"` anywhere in the shell. Each screen adds its own page status region (component 3)
and lists it in its own Live regions table.

---

## Accessibility checklist

**Headings**
- [ ] Exactly one `<h1>` per screen, the first thing in `<main>` after any success banner; rendered by `PageHeading`.
- [ ] The header has no heading; the wordmark is a link or plain text, not an `<h1>`.
- [ ] The session dialog's heading is an `<h2>`; the signed-out block's heading is the page `<h1>`; the sign-in
  controls' headings drop to `<h2>` in place.

**Landmarks**
- [ ] `header` (banner), `nav aria-label="Main"`, one visible `main`; no content outside landmarks except the skip
  link, the before-main bar (back link, note form bar) and the dialog [Standard, APG landmark regions].
- [ ] Any second `nav` on a screen (the Flagged tabs) carries its own unique label.

**Names and labels**
- [ ] Every accessible name starts with its visible words (SC 2.5.3): "Flagged, 3 to review"; "Account"; "Sign out";
  "Stay signed in". No `aria-label` replaces visible text.
- [ ] Badge digits are `aria-hidden`; the hidden ", 3 to review" uses the clip technique, never `display: none`.
- [ ] Chevron icons are `aria-hidden="true"` with `focusable="false"`.

**Keyboard**
- [ ] Every control reachable by Tab in the orders above; Enter follows links; Enter or Space presses buttons.
- [ ] Escape closes the account panel and returns focus to Account; Escape on the warning = Stay signed in.
- [ ] The modal keeps focus inside (native `showModal()`); nothing else traps focus.
- [ ] Focus is never dropped to `<body>`: busy buttons use `aria-disabled`; a Try again that disappears hands focus to
  the `<h1>`.

**Screen reader**
- [ ] A new screen is announced by its focused `<h1>` (or success banner), once.
- [ ] Nothing is announced when the badge changes or when the window regains focus.
- [ ] Countdown steps are announced politely at 29:00, 29:20 and 29:40, never every second; no `role="timer"`.
- [ ] Sign-out and start-up errors are announced through status lines that existed before their text.

**Visual**
- [ ] Focus ring: 3 px `--colour-focus` outline with 2 px offset on every focusable element, `outline` (never only
  `box-shadow`), `Highlight` in forced colours.
- [ ] Current item: bold + 4 px bar + `aria-current`, never colour alone (SC 1.4.1).
- [ ] All text at least 16 px and at least 7:1 (foundations.md); badge digits 19.6:1.
- [ ] Targets: nav links, Account, Sign out, Try again, Stay signed in at least 48 px tall; the wordmark at least
  44 px; 8 px between separate targets.
- [ ] No motion anywhere in the shell (panel, dialog, badge and banners appear instantly).

**WCAG 2.2 AA criteria this screen must meet**
1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.3.4 Orientation · 1.4.1 Use of Color · 1.4.3 Contrast
(Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast · 1.4.12 Text Spacing · 2.1.1 Keyboard ·
2.1.2 No Keyboard Trap · 2.2.1 Timing Adjustable · 2.2.2 Pause, Stop, Hide · 2.4.1 Bypass Blocks · 2.4.2 Page Titled ·
2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus
Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) · 3.1.1 Language of Page · 3.2.3
Consistent Navigation · 3.2.4 Consistent Identification · 3.3.1 Error Identification · 4.1.2 Name, Role, Value ·
4.1.3 Status Messages. Also met at no extra cost: 2.2.5 Re-authenticating, 2.2.6 Timeouts, 2.4.13 Focus Appearance,
2.5.5 Target Size (Enhanced), 1.4.6 Contrast (Enhanced) for all shell text.

---

## Acceptance criteria

**Structure, privacy and naming**
- [ ] At 320, 375 and 1280 px, the DOM order is skip link, `header`, before-main bar (when a screen fills it), `main`,
  `dialog`, and there is exactly one `<main>` without `hidden`.
- [ ] In the signed-out-in-place view the before-main bar is `hidden` with the route: no back link or save indicator
  can be seen or reached by Tab.
- [ ] `document.title` always matches `^(Error: )?Grow2Notes – (one PageName)$` or is exactly "Grow2Notes"; a test
  walks every route and fails if a participant or user name appears in any title.
- [ ] No route pushes `history.state` containing anything other than React Router's own key and the boolean
  `inScreen`; after a full manager and worker session, `localStorage`, `sessionStorage` and IndexedDB are empty.
- [ ] A Vitest/CI check fails if the parent company's name appears in `index.html`, the bundle, CSS tokens, file names
  or copy modules (D42).
- [ ] `<html lang="en-AU">`, the viewport tag has no `maximum-scale` or `user-scalable`, and `color-scheme` is
  `light` without `only`.

**Navigation**
- [ ] A worker sees exactly: Grow2Notes, Today, Account. A manager sees exactly: Grow2Notes, Account, Today, Flagged,
  Report, Manage, in that order, at every width, with no menu button.
- [ ] Before `/me` answers, no nav item or Account button is in the DOM.
- [ ] `aria-current` matches the URL table in component 2b on every route; only one element in the nav has it;
  `/manage/users` gives Manage `aria-current="true"`; note routes give none.
- [ ] The Report link's `href` is `/reports/daily/{me.today}` and updates after `/me` reports a new date (test with a
  fake clock across midnight Melbourne time).
- [ ] At 320 px and at 200% text, the header wraps without horizontal scrolling and nothing overlaps.
- [ ] The header computes to `position: static`; no element in the shell is `fixed` or `sticky`.
- [ ] A worker opening any manager URL gets "Page not found" with title "Grow2Notes – Page not found".

**Badge**
- [ ] With `toReviewCount` 3, the Flagged link's accessible name is "Flagged, 3 to review" (checked with Testing
  Library `getByRole('link', { name: 'Flagged, 3 to review' })`), and the digit is not read twice.
- [ ] With 0, the link's name is "Flagged" and no pill is rendered.
- [ ] With 104, the pill shows "104" (no cap).
- [ ] The badge element has no `aria-live`, `role`, `title`, animation or transition.
- [ ] After Mark reviewed succeeds, the badge and the "To review (n)" tab show the same number without a reload.
- [ ] A failed `/me` refresh leaves the previous number on screen.

**Refresh**
- [ ] One `GET /api/auth/me` is sent per trigger: start-up, each `location.key` change, window `focus`, page becoming
  visible (focus and visibility firing together still send one request).
- [ ] With the page left open and untouched for 30 minutes, no request is sent by the shell (network log empty
  apart from the user's own actions).

**Account and Sign out**
- [ ] Account toggles `aria-expanded` and the panel's `hidden`; Escape closes it and focuses Account; navigating
  closes it.
- [ ] Sign out with a waiting autosave sends the `PUT …/draft` before `POST /api/auth/logout`, then reloads `/`; Back
  afterwards does not show the signed-in screen.
- [ ] Sign out offline shows "Not signed out: no connection. Try again." in the panel, announced politely, focus
  still on Sign out, and the person is still signed in.
- [ ] A `401` from logout is treated as success and does not flash the signed-out view.
- [ ] There is no confirmation dialog on Sign out, except a page's own leave guard.
- [ ] On Guide prompts with unsaved text, Sign out opens that page's leave dialog **before** any request; Stay sends
  nothing and returns focus to Sign out; Leave signs out with no browser leave prompt; a failed logout re-arms the
  page's unload guard.

**Screen changes, focus and scroll**
- [ ] Following any nav link focuses the new screen's `<h1>` and scrolls to the top; the `<h1>`'s focus box hugs the
  text.
- [ ] After Submit, arriving on Today focuses "Note for Jane Citizen submitted", not the `<h1>`; reloading Today does
  not show the message again.
- [ ] Back from a note to Today restores the list position and focuses the row that was opened (when the list
  declares a return point), with no jump to the top. The back link and browser Back give the same result.
- [ ] After Submit, the pop back to Today focuses the banner (not the opened row) and shows the top of the page.
- [ ] After Discard (a replace to Today), Today's `<h1>` is focused, not the row.
- [ ] No screen keeps its own return-row variable; every return point uses `handle.returnFocus`.
- [ ] Switching `?view=reviewed` or changing the Report date keeps focus on the activated control and does not
  scroll.
- [ ] On first load, focus is not moved.
- [ ] The skip link is the first Tab stop, becomes visible on focus, moves focus to `<main>` and does not change the
  URL.

**Start-up and whole-page messages**
- [ ] With the network stubbed to delay `/me` by 900 ms, no "Loading…" ever appears; at 1,000 ms it appears.
- [ ] Offline start-up shows "Could not connect to Grow2Notes" within about 2 seconds; Try again offline changes the
  status line to "Grow2Notes still did not load: no connection. Try again." and keeps focus on Try again; Try again
  online renders the screen and focuses its `<h1>`.
- [ ] A stubbed `503` shows "There is a problem with Grow2Notes" with "Grow2Notes did not load: something went wrong.
  Try again."
- [ ] An unknown URL shows "Page not found" with the nav still usable; a thrown render error in a screen shows
  "There is a problem with Grow2Notes" with the nav still usable, and its "Go to Today" does a full page load.
- [ ] No message in the shell is red, shows a status code, or contains "oops", "sorry", "error", "invalid", "can't"
  or "couldn't". "something went wrong" appears only inside microcopy.md §9's "did not load: …" and "Not [done]: …"
  shapes, never alone.

**Session**
- [ ] With fake timers: at 27:59 nothing shows; at 28:00 the dialog opens with focus on Stay signed in; the heading
  changes at 29:00, 29:20 and 29:40 only; at 30:00 the signed-out view appears with focus on its `<h1>`.
- [ ] A waiting autosave at 28:00 is sent instead of warning, and no warning appears if it is accepted.
- [ ] An open Submit confirmation is closed as Go back before the warning opens, and after Stay signed in focus lands
  on **Submit note**, not "Submit note for Jane Citizen".
- [ ] Stay signed in keeps the dialog open until `204`; offline it shows the connection error and the countdown
  continues; Escape and Android back act as Stay signed in; a backdrop click does nothing.
- [ ] Hidden for 31 minutes, then visible: no warning flashes; the `/me` result decides (`401` → signed out; `200` →
  stays).
- [ ] After timing out mid-note and signing in again as the same person, the typed text, ticks and flag reason are
  intact, the autosave retry succeeds without the "changed on another device or tab" banner, and the URL never
  changed (design §14 M2 test).
- [ ] Signing in as a different person after a timeout reloads to Today and the previous person's unsaved text is
  never sent to the server.
- [ ] A `401` on Submit, Save changes, Discard or Mark reviewed is never replayed automatically after signing in.

**Visual and settings**
- [ ] Forced colours (Edge emulation): the current-item bar, badge outline, buttons, dialog border and focus ring
  are all visible.
- [ ] Browser text size 200% and laptop zoom 400%: no clipping, no horizontal scroll, the dialog scrolls inside
  itself.
- [ ] `prefers-reduced-motion: reduce`: nothing changes (nothing moves anyway).
- [ ] axe (Playwright) passes on: worker header, manager header, account open, start-up error, Page not found,
  session warning, signed-out view.
- [ ] Manual: NVDA + Chrome, VoiceOver + iOS Safari and TalkBack + Chrome hear "Flagged, 3 to review" once, the
  screen `<h1>` on each navigation, the success banner once on arrival at Today, and the countdown steps.

---

## Conflicts resolved

| # | Disagreement | Chosen | Why |
|---|---|---|---|
| 1 | Badge markup: app-shell-nav.md (visible digit + " to review", name "Flagged 3 to review") vs notification-badge.md (digit `aria-hidden` + ", 3 to review") | notification-badge.md: "Flagged, 3 to review" | It is the MoJ design system's published markup, the comma gives a spoken pause, and microcopy.md uses the same form. SC 2.5.3 ignores punctuation, so "say Flagged" still works. |
| 2 | Nav links: `NavLink` (notification-badge.md) vs a custom `NavItem` (app-shell-nav.md) | `NavItem` | `NavLink` always writes `aria-current="page"` and would announce "Manage, current page" on `/manage/users`, which is wrong; the URL table needs `"true"` for sections. |
| 3 | Badge text size: 0.875rem/em in both component specs vs foundations.md's 16 px minimum | `--font-size-small` (1rem) | foundations.md bans text under 16 px, "including … the badge", for glare and tired eyes; 0.875em of 18 px is under 16. |
| 4 | Badge colour: "dark brand accent" (app-shell-nav.md) vs `--badge-bg` neutral (notification-badge.md) vs foundations.md (no brand colour; `--colour-badge` near-black) | `--colour-badge` / `--colour-on-badge` | foundations.md owns colour; near-black is the neutral, non-red pill notification-badge.md asked for. |
| 5 | Current-item bar: `--colour-brand` (app-shell-nav.md) vs `--colour-action` (foundations.md) | `--colour-action` | foundations.md's alias table maps `--colour-brand` to `--colour-action`; there is no brand colour. |
| 6 | Badge refresh mechanics: TanStack `focusManager` listening to `focus` + `visibilitychange` (app-shell-nav.md) vs default `refetchOnWindowFocus` plus an extra `focus` listener (notification-badge.md) vs the session clock's own `visibilitychange` listener (session-timeout.md) | One `refreshMe()` module with explicit triggers; `['me']` observer `staleTime: Infinity`, no TanStack focus refetch | One owner and one request per trigger. The session clock must hold on the very same `/me` promise when the page becomes visible; three listeners would send duplicate requests or race. |
| 7 | Scroll restoration: React Router `<ScrollRestoration>` (app-shell-nav.md) vs participant-list-rows.md, chronological-list.md and note-identity-header.md (all reject it because it writes `sessionStorage`) | An in-memory `Map` keyed by `location.key` in the shell; lists own only their focus return point | Keeps "nothing stored on the device" (D22, §9.6) without asking the owner, keeps Back landing where the person was, and gives scroll one owner. It also removes the unverified CSP question app-shell-nav.md raised. |
| 8 | What counts as a screen change: every `location.key` (app-shell-nav.md) vs `pathname` only (tabs-segmented.md) vs `state.inScreen` (date-navigation.md) vs a route handle for Report (empty-loading-error.md, daily-report.md) | A new screen unless the search alone changed, `state.inScreen` is `true`, or both routes share a `handle.screenKey`; checked for push, replace and pop. `/me` refreshes on every `location.key`. Routes with a `screenKey` share one scroll position. | `inScreen` alone missed browser Back to the first report entry (daily-report.md); a `screenKey` covers it without `<ScrollRestoration getKey>`, which writes to `sessionStorage` (D22). `inScreen` is a boolean, so router state still holds no personal data. |
| 9 | Arrival focus: `[data-route-focus]` then `<h1>` (status-messages.md) vs a focus-pending flag for data headings (empty-loading-error.md) vs "focus the last opened row" on Today (participant-list-rows.md, chronological-list.md), with five different mechanisms across the screen specs | One rule for every screen: `[data-route-focus]` wins on every arrival (push, replace or pop); otherwise, on pop only, `handle.returnFocus` (row by stable key, optional `'sameIndex'` fallback); otherwise the `<h1>` (pending until it mounts). Back links pop when the previous entry is the list. | One owner, no per-screen module variables. A return point on a push or replace would focus an old row out of context (Discard), so it is honoured only on pop; the banner after Submit must win even on a pop. |
| 10 | Signed-out structure: hide the whole `AppShell` and render sign-in beside it (session-timeout.md, sign-in-form.md) vs keep the header with the wordmark as text and render sign-in in `<main>` (app-shell-nav.md) | Keep one header (wordmark as text) and one `<main>`; hide only the route inside `<main>` | The HTML spec allows only one `<main>` without `hidden`; the wordmark keeps the person oriented; the route still stays mounted, which is what session-timeout.md needs. |
| 11 | Signed-out copy: "Sign in to go back to where you were." (session-timeout.md) vs "You've been signed out. Sign in again to carry on." (sign-in-form.md) vs microcopy.md | microcopy.md canonical: `<h1>` "You've been signed out" + "Sign in again to go back to where you were." | microcopy.md settles it; "carry on" is an idiom (COGA 4.4.4). |
| 12 | Load-failure wording: "[Thing] did not load: [cause]. Try again." (microcopy.md) vs "Could not load [thing]. [cause sentence]" with a "Still…" retry (empty-loading-error.md); "Can't connect…" (app-shell-nav.md) | microcopy.md §9 for every status line: "Grow2Notes did not load: no connection. Try again." and the "still did not load" repeat. The start-up page keeps its `<h1>` "Could not connect to Grow2Notes" / "There is a problem with Grow2Notes". | Settled once app-wide (editorial pass): microcopy.md §9 owns the words, empty-loading-error.md owns the mechanism (1 s delay, one silent retry, Try again outside the status line, the "still" re-announcement). A tired worker sees the same words for the same failure on every screen (SC 3.2.4). |
| 13 | Server-failure wording: "something went wrong" allowed with a next step (microcopy.md) vs banned (empty-loading-error.md) | "something went wrong", only inside microcopy.md §9's shapes, which always end "Try again.": "Not signed out: something went wrong. Try again." | Eight of twelve screens already used it, and the shape always names what failed and the next step, which answers the NN/g objection to a bare "something went wrong". "There is a problem with Grow2Notes" stays as the whole-page `<h1>`. |
| 14 | Sign-out error wording: "Your note isn't saved yet" (app-shell-nav.md) vs microcopy.md canonical | "Not signed out: your note is not saved yet. Keep this page open; retrying." | No negative contractions in new copy; follows design's own "Not saved: …" shape. |
| 15 | Stay-signed-in connection error: "…and try again." (session-timeout.md) vs "…, then try again." (empty-loading-error.md) | "No connection. Try again." | microcopy.md §9's cause word and next step; the dialog has one button, so naming the action again adds nothing. |
| 16 | Autosave flush API: `flushPendingSaves()` (app-shell-nav.md) vs `flushNow()` (autosave-status.md) | `flushNow()` | autosave-status.md owns the engine and already exposes it to session-timeout.md. |
| 17 | Dialog styling: 150 ms fade, 3 px focus offset, weight 600 (session-timeout.md) vs no motion, 2 px offset, weights 400/700 (foundations.md) | foundations.md | foundations.md owns motion, focus and type; 600 renders differently on iPhone and Android. Heading stays at `--font-size-h2` because it is an `<h2>` (foundations.md's per-screen note said body size; the dialog's one sentence needs the prominence). |
| 18 | Account button look: text with a hover underline (app-shell-nav.md) vs foundations.md ("controls have a 2 px border or a solid fill") | Secondary button style | A visible boundary tells low-confidence users it can be pressed; it also meets 1.4.11 without relying on hover, which phones do not have. |
| 19 | Target size: 44 px (notification-badge.md, A32) vs 48 px (app-shell-nav.md, foundations.md) | 48 px for nav links, buttons; 44 px floor elsewhere | foundations.md sets 48 px for nav links and buttons; A32's 44 px stays the minimum. |
| 20 | Title of the start-up failure page: "Grow2Notes" (empty-loading-error.md) | "Grow2Notes – Could not connect" / "Grow2Notes – There is a problem" | SC 2.4.2 asks the title to describe the page's topic; both names are generic and carry no data. |
| 21 | Page-name list (app-shell-nav.md, microcopy.md) lacked "There is a problem", "Add participant", "Write past-day note", "Invite user" and "Edit user", and listed sign-in step names sign-in.md does not use | Union rebuilt from the screens' route tables (component 3) | Every title must come from the one typed list, or it will not compile. |
| 22 | Where sign-in shows on a fresh load with no session: a `/sign-in` route (sign-in-form.md) vs in place at any URL (session-timeout.md, §8.5) | In place at the opened URL. **There is no `/sign-in` route** (sign-in.md #26): a signed-in person who types it gets "Page not found", like any unknown URL | One mechanism for a fresh load and for signing in again in place; a deep link survives sign-in without a `returnUrl` query string (URLs hold only IDs and dates, §4.0). The app never links to `/sign-in`. |
| 23 | Where the before-main bar lives: inside `<main>` (this file) vs between the header and `<main>` (note-form.md, participant-notes.md, version-history.md, flagged.md, sign-in.md) | A named before-main bar slot owned by the shell, filled by the route through a portal and hidden with the route when signed out (component 3a) | Keeps GOV.UK's back-link position (the skip link passes it) and gives the route-rendered bar a defined place in the DOM order, so it can never show over the signed-out view. |
| 24 | Where busy labels are announced: an app-level region (primary-actions.md, common-items.md, users.md, sign-in.md) vs no global live region (this file, status-messages.md) | One page status region per page, found by the shared `Button` through context (component 3) | Keeps the shell free of a global live region while every screen gets one pre-existing region for busy labels and results. |

---

## Tensions with decisions

Recorded once each, with evidence. No change is recommended.

1. **Page title order** (design.md §4.0 copy, "Grow2Notes – Note"). HMRC's page-title pattern and the W3C examples for
   SC 2.4.2 put the page-specific words first, so screen readers announce them first and truncated phone tabs stay
   distinguishable [Convention, https://design.tax.service.gov.uk/hmrc-design-patterns/page-title; Standard,
   https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html]. Both orders pass 2.4.2; this spec follows design.md.
2. **A24, 12-hour absolute limit with no warning.** The Understanding document for SC 2.2.1 treats security time
   limits as time limits and exempts only limits over 20 hours
   (https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html). Nothing is lost (drafts are on the server,
   SC 2.2.5), and the warning must not be shown for this limit because Stay signed in cannot extend it.
3. **The warning text does not say work is saved** (design.md §4.0, §8.5 copy). Home Office guidance says to tell
   users whether progress will be saved, and its research found users hoped it was. It would not always be true here:
   while "Not saved: no connection" shows, the latest change is not saved.
4. **D22, nothing stored on the device.** Mobile guidance often caches last-known content for offline use [Convention,
   https://www.smashingmagazine.com/2016/09/how-to-design-error-states-for-mobile-apps/]. A first start-up with no
   connection can only show "Could not connect to Grow2Notes".

---

## Open questions for the owner

1. **The Manage page.** §4.0 lists "Manage (Participants, Common items, Guide prompts, Users)". This spec reaches it
   through `/manage`, a page holding only the heading and the four links, because it needs no dropdown and Back works.
   It could be read as an extra screen. The alternative with no extra page is a "Manage" disclosure button in the nav
   that shows the four links in the flow underneath (APG disclosure navigation). Which do you prefer?
2. **New wording to approve:** "Account", "Signed in as {name}", "Signing out…", the three "Not signed out…" messages,
   "Loading…", "Could not connect to Grow2Notes" and "There is a problem with Grow2Notes" (start-up headings) with
   "Grow2Notes did not load: no connection. Try again." / "…: something went wrong. Try again." and their "still did
   not load" forms, "Page not found" / "If you typed or pasted the web address, check it is correct." / "Go to Today",
   "You cannot edit this note" / "Read the note", "Anything already saved is kept.", "You've been signed out" / "Sign
   in again to go back to where you were.", "Connecting…", "No connection. Try again.", the 1-minute, 40-second and
   20-second warning steps, and the `<noscript>` line.
3. **"When a page loads"** (§4.6, §6.8) is read as every screen opened inside the app, not only a browser reload,
   so the badge and `today` stay fresh during a visit. Confirm.
4. **Shared laptop:** if a different person signs in on the "You've been signed out" view, the app reloads to Today
   and the previous person's unsaved, in-memory text is dropped rather than saved under the new person's name.
   Confirm.
5. **Warning over a confirmation:** the 28-minute warning closes an open Submit or Discard confirmation as "Go back"
   before it opens, so the person sees the participant's name again when they resubmit. Confirm.
