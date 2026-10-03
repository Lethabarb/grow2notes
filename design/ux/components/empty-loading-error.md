# Empty, loading and error states

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> **Wording:** every word of a loading or failure message comes from [microcopy.md](microcopy.md) §9 ("[Thing] did
> not load: [cause]. Try again.", the "still did not load" repeat, and the cause words "no connection" / "something
> went wrong"). This file owns the **mechanism** only: the 1 s delay, one silent retry, Try again outside the status
> line, the changed "still" text on a repeat failure, and the whole-page messages. The examples below follow
> microcopy.md §9 since the editorial pass of 1 October 2026.

Component key: `empty-loading-error`. Sources of truth: design.md §3.4 (autosave banner), §4.0 (shared behaviour, accessibility, page titles), §4.1, §4.2, §4.4, §4.6, §4.7, §4.8, §4.9, §4.11 (each screen's **States**), §6.1 and §6.9 (error shapes and codes), §6.8 (refresh), §7.2 (401/403 instead of redirects; unknown `/api` paths give 404), §8.5 (signed out keeps the text), §9.2 (another organisation's record gives 404, not 403), §9.6 (nothing on the device), A17, A18, A32, A33. Decisions D20, D22, D25, D35 and D43 apply.

This spec owns **what a screen shows while its data is loading, when the data is empty, and when loading fails**. It is the one place that sets the timing, wording and wiring for those states, so every screen behaves the same way.

It does **not** own these, which have their own specs. This document only lines its copy and wiring up with them:

| Not owned here | Owner |
|---|---|
| The note form's save indicator and the "Not saved: no connection. Keep this page open; retrying." banner | `autosave-status.md` |
| Field errors and the error summary after Save, Submit, Invite and so on | `form-validation.md` |
| A button's busy state and the error after an action fails ("Not submitted: no connection…") | `primary-actions.md`, `confirm-dialog.md` |
| The timeout warning and the signed-out state after any `401` | `session-timeout.md` |
| The "No participant matches 'xyz'" live region | `search-filter.md` |
| Sign-in failure, passkey and authenticator messages | `sign-in-form.md`, `passkey-flows.md`, `totp-setup.md` |
| The Flagged badge when its refresh fails | `notification-badge.md` |

**The short version**

1. **Under 1 second, show nothing.** After 1 second, show one line of plain text, such as "Loading participants…". No spinner and no skeleton.
2. **An empty list says so in one sentence**, using design.md's own words. Add an action only where the design names one.
3. **A failed load says what did not load and why**, in microcopy.md §9's words ("The participant list did not load: no connection. Try again."), with a **Try again** button. If Try again fails too, the message says "still did not load", so it is new text and is announced again.
4. **Two whole-page messages**: "Page not found" and "There is a problem with Grow2Notes". Neither shows a status code, an apology joke or red text.
5. **Never show "empty" before the data has arrived.** A failed background refresh never replaces data that is already on screen.

---

## Where it's used

| Screen (design.md) | Loading | Empty | Errors | What differs |
|---|---|---|---|---|
| **App shell, global** (§4.0) | Before `GET /api/auth/me` returns: the wordmark only, then "Loading…" after 1 s. Before JavaScript runs: the same "Loading…" from `index.html`. | – | Cannot reach the server; server error; unknown route; a screen crashes; a worker opens a manager route; any `401` | The only place that shows **whole-page** messages. A `401` hands over to `session-timeout.md`. The nav stays usable around any screen-level message. |
| **Sign-in and account setup** (§4.1) | "Checking your link…" after 1 s while the setup link is checked | – | Link expired (`410`), setup timed out (`401` on `/setup/*`), no connection, server error | The design's own sentences for expired and timed-out links. These are whole-page messages with no Try again, because trying again cannot help. |
| **Today** (§4.2) | Two requests (`/api/today`, `/api/me/drafts`) shown as one region: "Loading participants…" | "No participants yet." Managers also see **Add participant**. No search match: "No participant matches 'xyz'". The drafts section is left out when empty. | List did not load | The only empty state with an action in the design. The search box appears with the list, never before it. D25: no "not started" or "missing" wording anywhere. |
| **Past notes and the read view** (§4.4) | "Loading notes…" / "Loading note…". The `<h1>` is the participant's name, so it appears with the data. | "No notes yet for Jane Citizen." | List or note did not load; participant or note not found (`404`); **Show older** failed; worker opens Edit on someone else's note (`403 note.not_editable`) | The `<h1>` depends on the data, so focus waits for it (see Behaviour). Older notes that failed to load do not remove the 30 already shown. |
| **Flagged notes** (§4.6) | Per tab: "Loading flagged notes…" / "Loading reviewed notes…" | To review: "No flagged notes to review." Reviewed: "No reviewed notes yet." (proposed) | Tab list did not load; **Show older** on Reviewed failed | Each tab panel is its own region. The tab label shows no "(n)" until the list has loaded. |
| **Daily report** (§4.7) | None on screen. While `has-notes` is pending, the download buttons are unavailable without a message (`primary-actions.md`). | "No submitted notes for Thursday 1 October 2026." Both buttons disabled. | `has-notes` failed (buttons stay available); download failed (`primary-actions.md`); `404 report.no_notes` from the download; badly formed date in the URL | The **only empty state that is announced**, because it follows the manager's own date change while focus stays on the date controls. No report is ever shown on screen (D43). |
| **Participants list and participant detail** (§4.8) | "Loading participants…" / "Loading participant…" | Active: "No participants yet." Archived: "No archived participants." (proposed). No match: "No participant matches 'xyz'". Goals: "No goals yet. Notes will show an empty Goals section." | List or participant did not load; participant not found | **Add participant** and **Add goal** are already on the page, so the empty states add no second button. The archived-goals section is left out when there are none. |
| **Common items** (§4.9) | "Loading common items…" | Never empty of groups (Every note is built in). A group with no items shows "No items in this group. It does not show on notes until it has one." | List did not load | **Add item** is already in every group. The archived sections are left out when there are none. |
| **Users** (§4.11) | "Loading users…" / "Loading user…" | Never empty: the manager looking at it is always on the list. | List or user did not load; user not found | No empty state is needed. |
| *Also applies, outside this list* | Note form "Loading note…" (§4.3); version history (§4.5) | §4.3 "No goals set up for Jane yet. A manager can add them." and "No common items set up."; §4.5 "Not edited since submit"; §4.12 "No submitted notes for Jane Citizen between … and …" | Same patterns | Listed so the same component is used. Their owners decide the details. |

---

## Best practice

### Loading and perceived performance

- **[Research, expert estimate]** The three response-time limits: about 0.1 s feels instant, about 1 s keeps the user's flow of thought, about 10 s is the limit of attention. They come from Miller (1968), who called his figures "the best calculated guesses by the author", and were restated by Nielsen. Treat them as sensible estimates, not measured thresholds. https://www.nngroup.com/articles/response-times-3-important-limits/ · https://yusufarslan.net/sites/yusufarslan.net/files/upload/content/Miller1968.pdf
- **[Research]** NN/g: no progress indicator for waits under about 1 s, because a flashing indicator distracts; a looped indicator for roughly 2–10 s; percent-done for 10 s or more. https://www.nngroup.com/articles/progress-indicators/
- **[Research]** Viget (2017), n = 136, between groups (skeleton 39, spinner 39, blank 58), the same wait shown to everyone: perceived wait was skeleton 2.82 s, spinner 2.41 s, blank 2.29 s. "Loaded quickly" agreement was skeleton 59 %, spinner 74 %, blank 66 %. Later task time was also slowest after the skeleton (10.54 s against about 9.5 s). No significance tests were reported, so the fair reading is "no shown advantage for skeletons", not "spinners win". The authors suggest skeletons may only help on familiar screens or very short waits. https://www.viget.com/articles/a-bone-to-pick-with-skeleton-screens/
- **[Research]** Mejtoft, Långström and Söderström (ECCE 2018) report that a page with skeleton screens "scored higher on average" on perceived speed and ease of navigation than one with spinners. The sample size and whether the difference was significant are **unverified** here (the paper is paywalled). So the two studies point in opposite directions. https://doi.org/10.1145/3232078.3232086
- **[Convention]** NN/g's skeleton guidance (Tankala 2023, citing Mejtoft): skeletons for full-page loads under 10 s, spinners for single modules, nothing under 1 s; avoid "frame-display" skeletons that show only a header and footer; animated skeletons "can potentially be distracting, annoying, or even create accessibility problems". https://www.nngroup.com/articles/skeleton-screens/
- **[Convention]** AgDS: size a skeleton to "the shape of the final user interface", and do not use skeletons on compound components such as cards, tables or accordions. https://design-system.agriculture.gov.au/components/skeleton
- **[Convention]** Google's Cumulative Layout Shift counts a shift only when "a visible element changes its position". New content added below everything else does not count. Good is 0.1 or less. So content that replaces a one-line loading message at the end of the page causes no layout shift. https://web.dev/articles/cls
- **[Opinion, practitioner reports]** GOV.UK's loading-spinner backlog issue: one team found that words explaining what is happening reassured users, "even if the loading screen wasn't on long enough for the content to be read"; another found a static waiting page made users feel "something had broken" after a while; GOV.UK's own position was to "lead with 'try to avoid this'" by making services fast. GOV.UK has published no loading component. https://github.com/alphagov/govuk-design-system-backlog/issues/28
- **[Standard]** WCAG 2.2.2 Pause, Stop, Hide: an animation in a preload phase "can be considered essential if interaction cannot occur during that phase". A spinner next to a usable nav would not clearly meet that. Text with no motion avoids the question. https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html

### Empty states

- **[Research]** NN/g (Kaplan 2021): an empty state should communicate system status, give a learning cue, and give a direct path to fill it. "Misleading system-status messages" are "particularly harmful": showing "No records" and then replacing it with content moments later erodes trust. "Do not default to totally empty states", because users cannot tell loading from error from empty. https://www.nngroup.com/articles/empty-state-interface-design/
- **[Convention]** AgDS: an empty state appears when data **loaded successfully** but there is nothing to show. Give "clear messaging and a helpful action". https://design-system.agriculture.gov.au/patterns/loading-error-empty-states

### Error pages and wording

- **[Convention]** GOV.UK "Page not found": `<h1>` "Page not found"; "If you typed the web address, check it is correct"; no "404", no "oops", no red text, do not blame the user. https://design-system.service.gov.uk/patterns/page-not-found-pages/
- **[Convention]** GOV.UK "There is a problem with the service": say "Try again later", and say what happened to the user's answers; avoid "We are experiencing technical difficulties", codes such as "500" and red text. https://design-system.service.gov.uk/patterns/problem-with-the-service-pages/ · https://design-system.service.gov.uk/patterns/service-unavailable-pages/
- **[Convention + Research]** NHS App error pages: headings start "There is a problem…", and a repeat failure says "There is still a problem…". Offer **Try again** only when the user attempted something, the problem looks temporary and retrying is likely to work. No blame, no "500", no red text or exclamation marks, no "oops". The NHS App team's research: users skip the detailed text and go straight to buttons and links. https://design-system.nhsapp.service.nhs.uk/patterns/error-page/
- **[Research]** NN/g error-message guidelines: say what happened, why, and what to do next, in the user's words, without blame. https://www.nngroup.com/articles/error-message-guidelines/
- **[Convention]** AgDS error states: be specific, turn jargon into plain language, say how and when it can be resolved, do not blame, and "include the error code in the message if possible". It also uses `role="alert"` on error **and** empty containers. https://design-system.agriculture.gov.au/patterns/loading-error-empty-states
- **[Convention]** GOV.UK style guide: "Avoid negative contractions like can't and don't. Many users find them harder to read, or misread them as the opposite of what they say." This matters for readers with English as a second language. https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

### Retrying

- **[Convention]** TanStack Query 5 retries a failed query **3 times** by default on the client, with a delay of `Math.min(1000 * 2 ** attemptIndex, 30000)`. `retry` can be a function of the failure count and the error. https://tanstack.com/query/v5/docs/framework/react/guides/query-retries
- **[Convention]** In the default `networkMode: 'online'`, a query started while the browser reports offline is **paused**: its status stays `pending` and its `fetchStatus` is `paused`, so "it might not be enough to check for `pending` state to show a loading spinner". In `'always'` mode, queries are never paused and failures reach the `error` state. https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
- **[Convention]** TanStack Query 5 no longer reads `navigator.onLine` (unreliable in Chromium); it "always start[s] with `online: true`" and only listens to `online` and `offline` events. https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5
- **[Convention]** TanStack Query 5 exposes `isLoadingError` ("failed while fetching for the first time") separately from `isRefetchError` ("failed while refetching"). https://github.com/TanStack/query/blob/main/docs/framework/react/reference/interfaces/QueryObserverSuccessResult.md

### Accessibility

- **[Standard]** WCAG 2.2 SC 4.1.3 Status Messages (AA): a status message gives information on "the waiting state of an application" or "the existence of errors" without a change of context, and must be announced without moving focus. Changes of context that move focus are already announced and are out of scope. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **[Standard]** WAI-ARIA 1.2: `role="status"` is a polite, atomic live region; `role="alert"` is assertive and interrupts. The APG alert pattern: alerts must not move focus, and alerts present before the page finishes loading are not announced. https://www.w3.org/TR/wai-aria-1.2/#status · https://www.w3.org/WAI/ARIA/apg/patterns/alert/
- **[Research, practitioner screen-reader testing]** Scott O'Hara: an empty live region that already exists in the DOM, with text injected later, is the most reliable; injecting the region with its text, or toggling it from `display: none`, is the least reliable. Use as few live regions as possible. https://www.scottohara.me/blog/2022/02/05/are-we-live.html
- **[Standard]** SC 1.4.1 Use of Color (meaning never by colour alone), SC 2.4.2 Page Titled, SC 2.4.3 Focus Order. https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html · https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html

### Platform facts

- **[Convention]** React 19 error boundaries are still class-only ("There is currently no way to write an Error Boundary as a function component"). They do not catch errors in event handlers or asynchronous code. https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary
- **[Convention]** React Router renders the closest route error boundary when a route throws; `useRouteError()` and `isRouteErrorResponse()` tell thrown responses from crashes. https://reactrouter.com/how-to/error-boundary
- **[Convention]** Vite fires `vite:preloadError` when a lazily loaded chunk fails, typically after a new deployment removes old files; its example handler reloads the page. https://vite.dev/guide/build#load-error-handling

---

## Recommendation for Grow2Notes

### The states

Every data region on every screen is always in exactly one of these. The words are different per screen; the rules are not.

| State | When | What shows | Announced |
|---|---|---|---|
| **Waiting** | Request in flight for under 1 s | Nothing new. The `<h1>` and any static parts of the page show. | No |
| **Loading** | Still in flight after 1 s | One line: "Loading {things}…" | Yes, polite |
| **Ready** | Data arrived | The content. The loading line is cleared. | No (the user reads on) |
| **Empty** | Data arrived and has nothing in it | The design's one sentence, plus the design's action if it has one | No on first load; yes after the user's own change in place (Report date, search) |
| **Load failed** | The request failed after one silent retry, and no data is on screen | "{Thing} did not load: {cause}. Try again." (microcopy.md §9) + **Try again** | Yes, polite |
| **Still failing** | Try again failed | "{Thing} still did not load: {cause}. Try again." | Yes, polite (the text is new, so it is announced again) |
| **Not found** | `404`, a `403` from a manager-only request, or an unknown route | Whole-page "Page not found" | Through focus on its `<h1>` |
| **Crashed** | A render error caught by the route error boundary | Whole-page "There is a problem with Grow2Notes" | Through focus on its `<h1>` |
| **Signed out** | Any `401` | `session-timeout.md`'s signed-out state | Owned there |

**Background refresh.** When data is already on screen and a refetch fails, **nothing changes** and nothing is announced (`participant-list-rows.md`, `notification-badge.md`, `status-tags.md` agree). The next page visit or the next action fetches fresh data.

### Anatomy

**Loading** (phone, Today, after 1 s):

```
+-----------------------------------+
| Grow2Notes             Account v  |
| Today                             |
+-----------------------------------+
| Today · Thursday 1 October        |  <h1>, shown at once
|                                   |
| Loading participants…             |  <p role="status">, plain body text
|                                   |
+-----------------------------------+
```

**Load failed** (same place; header, nav and `<h1>` stay usable):

```
| Today · Thursday 1 October        |
|                                   |
| The participant list did not      |  <p role="status"> (same element)
| load: no connection. Try again.   |
|                                   |
|                                   |
| [          Try again          ]   |  secondary <button>, outside the status
```

**Empty** (Today, manager):

```
| Today · Thursday 1 October        |
|                                   |
| No participants yet.              |  <p>, body text
|                                   |
| Add participant                   |  <a href>, managers only
```

**Whole-page message** (Page not found):

```
+-----------------------------------+
| Grow2Notes             Account v  |
| Today   Flagged 3   Report  Manage|
+-----------------------------------+
| Page not found                    |  <h1 tabindex="-1">, takes focus
|                                   |
| If you typed or pasted the web    |
| address, check it is correct.     |
|                                   |
| Go to Today                       |  <a href>
+-----------------------------------+
```

There is no illustration, icon, coloured box, status code or red text in any of these states.

### Behaviour

**Timing**

- **No indicator under 1 s.** A 1 s timer starts with the request. If the data lands first, nothing was shown. This matches `participant-list-rows.md` and `app-shell-nav.md`. [Research] NN/g; [Opinion] for the exact 1 s.
- **No minimum display time.** If "Loading…" appears at 1 s and the data lands at 1.1 s, the line simply goes. It is one line of text, so the flash is small, and holding content back to avoid it would make every slow load slower. [Opinion]
- **One request timeout of 10 s**, the same as autosave and the session ping (`autosave-status.md`, `session-timeout.md`), via `AbortSignal.timeout(10_000)`. A call may pass its own `timeoutMs`: the two file downloads (Daily report, Export record) pass `30_000`, because M4 and M5 allow slow but working requests that a 10 s cut-off would report as "no connection".
- **One silent retry** for network failures, timeouts and `5xx`, then the Load failed state. Never retry `401`, `403`, `404`, `409`, `410`, `412` or `422`: retrying cannot change the answer. The worst case is about 21 s of "Loading…" (10 s, a 1 s pause, 10 s) on a server that answers nothing; an offline phone fails in about 1 s because `fetch` rejects at once. [Opinion], within TanStack's documented `retry` options.
- **Queries use `networkMode: 'always'`.** In the default `'online'` mode a query started while the phone reports offline sits in `pending` + `paused` with no error, so the screen would say "Loading…" for ever. With `'always'`, it fails in about a second and says "Check your internet connection". Mutations keep their own settings (`autosave-status.md` relies on the default pause). [Convention] TanStack docs; [Opinion] the choice.

**Load failed and Try again**

- The message names **what** did not load and **why**, then "Try again." (microcopy.md §9). Two causes are told apart ("no connection", "something went wrong") because the person's next step differs: fix the connection, or wait. [Research] NN/g; [Convention] GOV.UK, NHS App.
- **Try again** refetches only the failed requests. While it runs, the button gets `aria-disabled="true"`, extra presses are ignored, and after 400 ms its label becomes "Loading…" (the shared busy pattern in `primary-actions.md`). The old error text is cleared at once, so the screen visibly reacts to the tap.
- **If it works:** the content replaces the message and focus moves to the page `<h1>`, because the focused button has gone. Without this, focus would fall to `<body>`.
- **If it fails:** the text comes back as "{Thing} **still** did not load: {cause}. Try again." and focus stays on Try again. The changed words mean screen readers announce it again; identical text in a live region is often not re-announced. [Convention] NHS App "There is still a problem".
- **Try again stays available.** The NHS App shows it once and then points elsewhere, but Grow2Notes has no other channel: phone connections drop and come back, and the Word template is retired (§14). [Opinion, deliberate departure from NHS App]
- **No error codes on screen.** The server logs request IDs (§9.5) and the operator gets error alerts (§14 M6), so a code would only add something a tired worker has to copy. [Opinion]; AgDS and the NHS App suggest codes where a support desk exists. Grow2Notes has none in the app.

**Empty**

- Show the empty sentence **only after the data has arrived** (`isSuccess`). Never before, never from a cache that might be out of date on first load, never while the request is failing. [Research] NN/g "misleading system-status messages".
- Use design.md's sentence word for word. Add the design's action only where it names one (managers' **Add participant** on Today). On the setup screens the adding control (Add participant, Add goal, Add item) is already on the page, so a second copy of it would be noise. [Opinion]
- A section that the design says is "shown only when…" (Today's drafts section, the archived goals and archived items sections) is **left out entirely** when empty, heading and all. It is not an empty state.
- D25: no empty-state wording may suggest a missing note ("No note yet today", "Not started"). Rows with no note simply show no status (A17).

**Focus on screen change (with `app-shell-nav.md`)**

`app-shell-nav.md` moves focus to the page `<h1>` after each screen change. Some `<h1>`s are data (the participant's name on Past notes, the read view and participant detail; the user's name on user detail), so they do not exist while loading. The rule:

1. Screens with a fixed `<h1>` (Today, Flagged notes, Daily report, Participants, Common items, Users) render it at once, and focus moves as normal.
2. Screens with a data `<h1>` render no `<h1>` while loading. The shell sets a "focus pending" flag in memory; the `<h1>` takes focus when it mounts (callback ref), whether it is the real heading, "Page not found" or the fallback heading on a failed load.
3. A failed load on a data-`<h1>` screen shows the page's generic name as its `<h1>` ("Past notes", "Note", "Participant", "User", the same names as the page titles), followed by the error and Try again.
4. While loading, focus may sit on `<body>` for up to a second or two. The polite "Loading notes…" covers longer waits for screen-reader users. [Opinion]

**Whole-page messages**

- **Page not found** for: an unknown route; a `404` on a screen's main request (a bad or old ID, another organisation's record per §9.2, a note discarded since the link was made); a worker on a manager route (`app-shell-nav.md` role guard); a `403` from a manager-only request after a role change. The URL is kept. [Convention] GOV.UK; matches the API's 404-not-403 stance (§9.2).
- **There is a problem with Grow2Notes** for a render error caught by the route error boundary. Its "Go to Today" link is a **full page load** (`<a href="/">`), which throws away the broken in-memory state. Drafts are on the server (§3.4), so the page can truthfully say "Anything already saved is kept." [Convention] GOV.UK "say what happened to their answers".
- **Shell cannot start**: if `GET /api/auth/me` fails with no connection or a server error, the shell shows a whole-page message with **Try again** (copy below). A `401` is not an error: it shows sign-in (§4.1).
- **Edit refused** (`403 note.not_editable`): reachable only by an old link or a stale screen, because Edit is not offered to other workers (§2). Show "You cannot edit this note" with a link to read it. It is not "Page not found", because the note exists and the person may read it (D20).
- No separate "service unavailable" page. If the whole app is down, `index.html` cannot load and the browser or Azure shows its own page, which the app cannot change (unverified what Azure shows). If the API is down while the app is open, each region's server message covers it.

**Offline**

- No app-wide offline banner. The design specifies an offline message only on the note form, where it matters (`autosave-status.md`). Everywhere else, a failed load already says "… did not load: no connection. Try again.", and data already loaded in this session stays on screen from the in-memory cache. [Opinion; keeps to the design]

### States of the parts

| Part | Default | Hover (`@media (hover: hover)`) | Focus | Active | Disabled | Busy / loading | Error | Empty | Read-only |
|---|---|---|---|---|---|---|---|---|---|
| Loading line | Empty, takes no space | – | Not focusable | – | – | "Loading {things}…" after 1 s | Replaced by the error text (same element) | Replaced by the content | – |
| Error message | – | – | Not focusable (it is a status) | – | – | Cleared while Try again runs | "… did not load: …" / "… still did not load: …" | – | – |
| Try again | Shared secondary button (`primary-actions.md`) | Darker border | 3 px outline, 2 px offset | Pressed tint | **Never `disabled`** | `aria-disabled="true"`, "Loading…" after 400 ms, same width | Back to "Try again", keeps focus | – | – |
| Empty sentence | Body text, normal text colour | – | Not focusable | – | – | – | – | Shown | – |
| Empty-state link (Today, managers) | Link style | Underline thickens | 3 px outline | Pressed tint | Never | – | – | Shown | – |
| Whole-page `<h1>` | Page heading style | – | `tabindex="-1"`; takes focus on mount; outline on `:focus-visible` only | – | – | – | – | – | – |
| Whole-page link ("Go to Today", "Read the note") | Link style | Underline thickens | 3 px outline | Pressed tint | Never | – | – | – | – |

Read-only does not apply: these states hold no data the person could change. The read-only screens (read view, a manager's view of a draft, an archived participant) use these same states while they load.

### Phone vs laptop

- **Same markup, same words** at every width. The message sits where the content will appear, left-aligned in the content column (capped at about 40rem on a laptop, as in `participant-list-rows.md`). Nothing is centred in the viewport, overlaid or fixed.
- **Try again** follows `primary-actions.md`: full width of the content column on a phone, `width: auto` (at least 8rem) on a laptop, at least 44 × 44 px (A32).
- **At 200 % text and 320 px width** the messages wrap; nothing scrolls sideways (SC 1.4.10). No message is truncated.

### Visual specification

All sizes in `rem`, so they follow the user's text size.

| Part | Specification |
|---|---|
| Loading line, error text, empty sentence | `1rem` body text, `line-height: 1.5`, `--colour-text` (the normal text colour, not a light grey: tired readers and readers of English as a second language). No italics. `max-inline-size: 40rem`. `margin-block-start: 1rem` below the `<h1>` or search box. |
| Empty status line when hidden | The `role="status"` `<p>` stays in the DOM with no text and `margin: 0`, so it takes no space and causes no shift. Never `display: none` (live regions toggled from `display: none` are the least reliable). |
| Error block | Error text, then Try again with `margin-block-start: 0.75rem`. **No red, no icon, no border, no background.** Red stays for field errors, the error summary and the "Not saved" banner, where the person can act on a specific thing. [Convention] GOV.UK and NHS App: no red text on these pages. |
| Whole-page message | Same `<h1>` style as other screens. Paragraphs at body size, `1rem` apart. One link. No illustration. |
| Motion | None. No spinner, no shimmer, no fade. So there is nothing to change under `prefers-reduced-motion`, and nothing for SC 2.2.2. |
| Forced colours | Nothing extra: no state depends on a background or shadow. The button keeps its 1 px border (shared button spec). |

### Exact copy

New strings use sentence case, no negative contractions ([Convention] GOV.UK style), and no "please", "sorry" or "oops". "(design)" means design.md's exact words; "(proposed)" means new copy for a state the design implies but does not word. **The words below are microcopy.md §9's; if the two files ever differ, microcopy.md wins.**

**Cause words, used for loads and actions alike** (microcopy.md §9):

| Cause | Words | Shape |
|---|---|---|
| No connection or timeout | no connection | "{Thing} did not load: no connection. Try again." |
| Server error (`5xx`, `429` with no specific message, anything unexpected) | something went wrong | "{Thing} did not load: something went wrong. Try again." |

**Loading and load-failed lines** (all proposed unless marked; each screen spec holds the exact string):

| Screen | Loading (after 1 s) | Load failed (then "still did not load" on a repeat) |
|---|---|---|
| Shell, before JavaScript and before `/me` | Loading… | Grow2Notes did not load: … (under the whole-page `<h1>`, app-shell.md) |
| Setup link check (§4.1) | Checking your link… (`sign-in-form.md`) | Your setup link was not checked: … (sign-in.md) |
| Today | Loading participants… (`participant-list-rows.md`) | The participant list did not load: … |
| Past notes | Loading notes… | Past notes did not load: … |
| Show older (Past notes, Reviewed) | status line: Loading older notes… (`chronological-list.md`) | Older notes did not load: … |
| Read view, note form | Loading note… | This note did not load: … / The note did not load: … |
| Flagged, To review | Loading flagged notes… | Flagged notes did not load: … |
| Flagged, Reviewed | Loading reviewed notes… | Reviewed notes did not load: … |
| Participants (Manage) | Loading participants… | The participant list did not load: … |
| Participant detail | Loading participant… | This participant did not load: … |
| Common items | Loading common items… | Common items did not load: … |
| Users | Loading users… | The user list did not load: … |
| User detail | Loading user… | This user did not load: … |

After a failed Try again, "did not load" becomes "**still did not load**", for example: "The participant list still did not load: no connection. Try again." Button: **Try again**.

**Empty states:**

| Screen | Text | Action | Source |
|---|---|---|---|
| Today, no participants | No participants yet. | Managers: **Add participant** (link) | design §4.2 |
| Today, no search match | No participant matches 'xyz' | – | design §4.2 (owned by `search-filter.md`) |
| Past notes | No notes yet for Jane Citizen. | – | design §4.4 |
| Flagged, To review | No flagged notes to review. | – | design §4.6 |
| Flagged, Reviewed | No reviewed notes yet. | – | proposed |
| Daily report | No submitted notes for Thursday 1 October 2026. | Both downloads disabled | design §4.7 |
| Participants (Manage), Active | No participants yet. | (Add participant is already on the page) | design §4.2 wording reused |
| Participants (Manage), Archived | No archived participants. | – | proposed |
| Participants (Manage), no search match | No participant matches 'xyz' | – | design §4.2 wording reused |
| Participant detail, Goals | No goals yet. Notes will show an empty Goals section. | (Add goal is already on the page) | design §4.8; "No goals yet." is the design's name for the state, shown so the sentence states the status first |
| Common items, a group with no items | No items in this group. It does not show on notes until it has one. | (Add item is already in the group) | design §4.9 (the page is never empty of groups) |

**Whole-page messages:**

| Situation | `<h1>` | Body | Action | Page title | Source |
|---|---|---|---|---|---|
| Not found / role guard / `404` | Page not found | If you typed or pasted the web address, check it is correct. | **Go to Today** (link) | Grow2Notes – Page not found | proposed, after GOV.UK; `<h1>` matches `app-shell-nav.md` |
| Render crash | There is a problem with Grow2Notes | Anything already saved is kept. | **Go to Today** (link, full page load) | Grow2Notes – There is a problem | proposed, after GOV.UK and NHS |
| Shell: no connection | Could not connect to Grow2Notes | Grow2Notes did not load: no connection. Try again. | **Try again** (button) | Grow2Notes – Could not connect (app-shell.md) | proposed (replaces `app-shell-nav.md`'s "Can't connect…") |
| Shell: server error | There is a problem with Grow2Notes | Grow2Notes did not load: something went wrong. Try again. | **Try again** (button) | Grow2Notes – There is a problem (app-shell.md) | proposed |
| Edit refused (`403 note.not_editable`) | You cannot edit this note | Only the person who wrote it, or a manager, can edit it. | **Read the note** (link) | Grow2Notes – Note | proposed; rule from design §2 |
| Setup link expired, used or replaced (`410`) | This link has expired | Ask a manager to send a new one. | – | Grow2Notes – Set up your account | design §4.1, split as in `sign-in-form.md` |
| Setup session timed out | Your setup session timed out | Open the link from your email again. | – | Grow2Notes – Set up your account | design §4.1, split as in `sign-in-form.md` |
| JavaScript turned off (`<noscript>`) | – | Grow2Notes needs JavaScript. Turn it on in your browser settings, then reload this page. | – | Grow2Notes | proposed |

The page title never contains a name (§4.0). "There is a problem" needs adding to `app-shell-nav.md`'s fixed list of page names.

### Accessibility

**Semantics**

- Each data region has **one** `<p role="status">`, rendered with the region from the first render and never conditionally mounted. It carries "Loading…" and then the error text. [Standard] SC 4.1.3; [Research] O'Hara.
- **Try again sits outside** the status element, so its label is not read out as part of the message and pressing it does not re-announce the message.
- The empty sentence is an ordinary `<p>`, not a live region, on first load: the person reaches it straight after the `<h1>`. Two exceptions announce it because it changes in place after the person's own action: search no-match (`search-filter.md`) and the Report screen's empty sentence (`primary-actions.md`).
- `role="status"` (polite), never `role="alert"`, for load messages. Nothing else is being read at that moment, and assertive interruptions add nothing. This differs from AgDS, which uses `role="alert"`. [Standard] WAI-ARIA 1.2; [Opinion] the choice.
- No `aria-busy`. Nothing renders in pieces: the content appears in one go when every request for the region has finished, so there is no half-built region to hide. A forgotten `aria-busy="true"` would also silence the region for good. [Opinion]
- Whole-page messages are real pages: an `<h1>` with `tabindex="-1"`, a page title, and links or buttons. No live region; focus on the `<h1>` announces them. [Standard] SC 2.4.2, 2.4.3; 4.1.3 places focus changes outside "status messages".
- The pre-JavaScript "Loading…" in `index.html` is plain text, not a live region. The APG notes that alerts present before the page finishes loading are not announced, so a live region there would add nothing.

**What a screen-reader user hears**

| Moment | Heard |
|---|---|
| Opens Today, fast connection | "Today · Thursday 1 October, heading level 1", then reads on into the list |
| Opens Today, slow connection | The heading, then after 1 s "Loading participants…", then silence; the list is there when they read on |
| The load fails | "The participant list did not load: no connection. Try again." |
| Taps Try again, it fails | "The participant list still did not load: no connection. Try again." (focus stays on Try again) |
| Taps Try again, it works | Focus moves to the heading: "Today · Thursday 1 October, heading level 1" |
| Opens a bad link | "Page not found, heading level 1" |

**Keyboard**

- Native `<button>` and `<a href>` only: Tab to reach, Enter or Space to press a button, Enter to follow a link. No custom keys.
- Focus never lands on `<body>` after a press: Try again keeps focus while busy (`aria-disabled`, not `disabled`) and hands it to the `<h1>` when it disappears.

**WCAG 2.2 criteria met**

1.3.1 Info and Relationships · 1.4.1 Use of Color (every state is words) · 1.4.3 Contrast (Minimum) (normal text colour) · 1.4.10 Reflow · 1.4.11 Non-text Contrast (button border, focus outline) · 1.4.12 Text Spacing · 2.1.1 Keyboard · 2.2.2 Pause, Stop, Hide (no motion) · 2.4.2 Page Titled · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (nothing fixed) · 2.5.3 Label in Name · 2.5.8 Target Size (44 px, A32) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria** is needed: a `<p role="status">`, a `<button>`, links and headings are all native.
- **Use `useQuery`, not `useSuspenseQuery` or `<Suspense>`,** so every screen uses one visible pattern and errors stay as data, not thrown exceptions. [Opinion]
- **Tell a first-load failure from a background one with `isLoadingError`, never `isError`.** In TanStack Query 5 a failed background refetch also sets `status: 'error'` while the old data is kept, so testing `isError` would replace good data with an error.
- **One `api()` wrapper** (already shared with `session-timeout.md`) turns a rejected `fetch` or a timeout into an `ApiError` with `status: 0`, and any non-2xx into an `ApiError` with the status and the problem-details `code`. On a `401` it tells the session layer first, then throws, and the region shows nothing for it. Classify by **status**, not by `TypeError`, because a bug inside a query function also throws `TypeError`.
- **No route-level code splitting.** The bundle is small, and with one bundle a deploy cannot leave an open tab asking for a chunk that no longer exists. If a lazy import is ever added, handle `vite:preloadError` by showing the "There is a problem" page, **not** by reloading automatically as Vite's example does: a reload drops unsaved text held in memory (§8.5).
- **Route error boundary** (`errorElement` / `ErrorBoundary` on the root layout route and on the signed-in layout): crashes inside a screen keep the shell and nav; a crash in the shell itself gets the bare page. Unknown routes use the same boundary's not-found branch (`app-shell-nav.md`).
- **After signing in again** (`session-timeout.md`), invalidate the queries of the current screen, so a region that failed or was pending on the `401` loads again.
- **Report screen:** key the empty sentence to the date in the URL, and clear it the moment the date changes, so "No submitted notes for Wednesday…" never shows under Thursday.
- **The 1 s delay in `index.html`** uses a CSS animation delay in the bundled stylesheet (CSP `style-src 'self'` forbids inline styles, §9.7). If a global reduced-motion rule turns animations off, the text simply shows at once.

```ts
// api/errors.ts
export class ApiError extends Error {
  constructor(public status: number, public code?: string) { super(code ?? `HTTP ${status}`); }
}
export type Failure = 'offline' | 'server' | 'notFound' | 'notEditable' | 'signedOut';

export function classify(e: unknown): Failure {
  if (!(e instanceof ApiError)) return 'server';                 // a bug, not the network
  if (e.status === 0) return 'offline';                          // fetch rejected or 10 s timeout
  if (e.status === 401) return 'signedOut';                      // session-timeout.md shows sign-in
  if (e.status === 403 && e.code === 'note.not_editable') return 'notEditable';
  if (e.status === 403 || e.status === 404) return 'notFound';   // §9.2: never reveal existence
  return 'server';                                               // 5xx, 429, anything unexpected
}
export const isTransient = (e: unknown) =>
  e instanceof ApiError && (e.status === 0 || e.status >= 500);

// api/client.ts (excerpt): 401 never reaches a screen
// timeoutMs: per-call override of the 10 s default; the file downloads pass 30_000 (daily-report.md, record-export.md).
// signal: the caller's own AbortSignal (leaving the page); it is combined with the timeout.
export async function api<T>(
  path: string,
  init: RequestInit & { timeoutMs?: number } = {},
): Promise<T> {
  const { timeoutMs = 10_000, signal, ...rest } = init;
  const timeout = AbortSignal.timeout(timeoutMs);
  let res: Response;
  try {
    res = await fetch(path, { ...rest, signal: signal ? AbortSignal.any([signal, timeout]) : timeout });
  } catch (e) {
    if (signal?.aborted) throw e;                                // the caller left the page: say nothing
    throw new ApiError(0);                                       // offline, DNS, timeout
  }
  if (res.status === 401) { sessionEnded(); throw new ApiError(401); }  // session-timeout.md
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new ApiError(res.status, body.code);                   // RFC 9457 `code` (§6.1)
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

// queryClient.ts
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      networkMode: 'always',                       // never sit "pending + paused" with no message
      retry: (count, e) => count < 1 && isTransient(e),  // one silent retry; default 1 s delay
      refetchOnWindowFocus: false,                 // per app-shell-nav.md
    },
  },
});
```

```tsx
// ui/useDelayed.ts
export function useDelayed(active: boolean, ms: number): boolean {
  const [late, setLate] = useState(false);
  useEffect(() => {
    if (!active) { setLate(false); return; }
    const t = window.setTimeout(() => setLate(true), ms);
    return () => window.clearTimeout(t);
  }, [active, ms]);
  return active && late;
}

// ui/LoadRegion.tsx: words from src/copy (microcopy.md §9)
const CAUSE = { offline: 'no connection', server: 'something went wrong' } as const;

type Props = {
  queries: UseQueryResult<unknown>[];   // e.g. [today, drafts]: shown together or not at all
  loading: string;                      // "Loading participants…"
  what: string;                         // "The participant list" (sentence start)
  fallbackHeading?: string;             // data-<h1> screens: "Past notes"
  children: () => ReactNode;            // renders only when every query has succeeded
};

export function LoadRegion({ queries, loading, what, fallbackHeading, children }: Props) {
  const errored = queries.find(q => q.isLoadingError);          // never isError (keeps good data)
  const kind = errored ? classify(errored.error) : null;
  const failed = kind !== null && kind !== 'signedOut';          // a 401 waits for sign-in, silently
  const fetching = queries.some(q => q.fetchStatus === 'fetching');
  // A failed *background* refetch has status 'error' but keeps its data: still ready.
  const ready = queries.every(q => q.isSuccess || q.isRefetchError);
  const slow = useDelayed(!ready && !failed && fetching, 1000);
  const busy = useDelayed(failed && fetching, 400);
  const [tries, setTries] = useState(0);
  const focusWhenReady = useRef(false);

  useEffect(() => {                                              // runs after the new <h1> is in the DOM
    if (ready && focusWhenReady.current) { focusWhenReady.current = false; focusPageHeading(); }
  }, [ready]);

  if (kind === 'notFound') return <NotFound />;                  // its own <h1>, takes focus
  if (kind === 'notEditable') return <EditRefused />;

  const text =
    failed && !fetching
      ? `${what} ${tries > 0 ? 'still did not' : 'did not'} load: ${CAUSE[kind === 'offline' ? 'offline' : 'server']}. Try again.`
      : slow ? loading : '';

  async function retry() {
    if (fetching) return;                                        // aria-disabled: ignore presses
    focusWhenReady.current = true;                               // set before the data can land
    const results = await Promise.all(queries.filter(q => q.isLoadingError).map(q => q.refetch()));
    if (results.every(r => r.isSuccess)) setTries(0);
    else { focusWhenReady.current = false; setTries(n => n + 1); }   // focus stays on Try again
  }

  return (
    <>
      {failed && fallbackHeading && <PageHeading>{fallbackHeading}</PageHeading>}
      <p role="status" className={s.status}>{text}</p>
      {failed && (
        <button type="button" className={btn.secondary} aria-disabled={fetching || undefined} onClick={retry}>
          {busy ? 'Loading…' : 'Try again'}
        </button>
      )}
      {ready && children()}
    </>
  );
}
```

The `<p role="status">` is rendered on every pass, so it exists before any text is written into it. On a data-`<h1>` screen whose load failed, the fallback `<PageHeading>` takes the pending focus. When a retry succeeds, the effect runs after React has committed the new content, so `focusPageHeading()` finds the real heading (static, or the data heading inside `children()`) rather than the fallback heading that is about to unmount. A `401` shows nothing here: `api()` has already handed over to the signed-out state, and the region's queries are invalidated after sign-in.

```tsx
// ui/PageHeading.tsx: focus waits for the heading, in memory only (D22)
let focusPending = false;
export const requestHeadingFocus = () => { focusPending = true; };  // called by the shell on navigation
export function focusPageHeading() {
  const h = document.getElementById('page-heading');
  if (h) h.focus(); else focusPending = true;
}

export function PageHeading({ children }: { children: ReactNode }) {
  const ref = useCallback((el: HTMLHeadingElement | null) => {
    if (el && focusPending) { focusPending = false; el.focus({ preventScroll: true }); }
  }, []);
  return <h1 id="page-heading" tabIndex={-1} ref={ref} className={s.h1}>{children}</h1>;
}

// ui/ProblemPage.tsx
export function NotFound() {
  usePageTitle('Page not found');
  return (
    <>
      <PageHeading>Page not found</PageHeading>
      <p>If you typed or pasted the web address, check it is correct.</p>
      <p><Link to="/">Go to Today</Link></p>
    </>
  );
}

export function Crashed() {             // used by the route error boundary
  usePageTitle('There is a problem');
  return (
    <>
      <PageHeading>There is a problem with Grow2Notes</PageHeading>
      <p>Anything already saved is kept.</p>
      <p><a href="/">Go to Today</a></p>   {/* full page load: drops the broken state */}
    </>
  );
}
```

```css
/* LoadRegion.module.css */
.status { margin: 1rem 0 0; max-inline-size: 40rem; color: var(--colour-text); line-height: 1.5; }
.status:empty { margin: 0; }            /* in the DOM, takes no space, never display:none */

/* global.css (bundled, linked from index.html): the pre-JavaScript line, shown after 1 s */
.boot { animation: boot-show 0s linear 1s both; }
@keyframes boot-show { from { visibility: hidden; } to { visibility: visible; } }
```

```html
<!-- index.html: React replaces #root's contents when it mounts -->
<div id="root"><p class="boot">Loading…</p></div>
<noscript><p>Grow2Notes needs JavaScript. Turn it on in your browser settings, then reload this page.</p></noscript>
```

**Tests**

- Vitest + Testing Library, with a fake timer: nothing at 999 ms, "Loading participants…" at 1,000 ms; a failed then successful refetch moves focus to the `<h1>`; a background refetch error leaves the list on screen; an empty response shows the empty sentence only after success.
- Playwright: offline (`context.setOffline(true)`) shows the connection message within about 2 s on every screen; a stubbed `503` shows the server message; a bad participant ID shows "Page not found" with that page title; axe passes in each state.
- Manual: VoiceOver and TalkBack hear "Loading…", the error, the "still did not load" repeat, and the heading after a successful Try again (§14 M6 already lists VoiceOver and TalkBack).

---

## Per-screen notes

### App shell, global (§4.0)

- **Start-up:** `index.html` shows "Loading…" after 1 s while JavaScript loads. The shell then calls `GET /api/auth/me`: `200` shows the nav and the screen; `401` shows sign-in (§4.1), which is not an error; no connection or a server error shows the shell's whole-page message with **Try again**. The antiforgery token request (§9.8) is part of start-up and fails the same way.
- **Wordmark only while `/me` is pending** (`app-shell-nav.md`): no nav, no badge, no fake "0", because the role decides the nav.
- **Background `/me` refresh** on focus or visibility (§6.8) never shows an error: the last known badge and nav stay (`notification-badge.md`).
- **Crashes** inside a screen show "There is a problem with Grow2Notes" in `<main>` with the nav still usable. A crash in the shell itself shows the same page without the nav.
- **Any `401`** goes to `session-timeout.md`'s signed-out state ("You've been signed out" / "Sign in to go back to where you were."). No region shows a load error for a `401`.

### Sign-in and account setup (§4.1)

- The sign-in screen loads no data, so it has no loading or empty state of its own.
- The setup link check shows "Checking your link…" after 1 s (`sign-in-form.md`). If the check cannot reach the server, show "Your setup link was not checked: {cause}. Try again." (sign-in.md) and **Try again**; the link is still valid (§8.1 step 3), so retrying is safe.
- "This link has expired" and "Your setup session timed out" are whole-page messages with **no Try again**: nothing the person can do on this page will fix them (NHS App rule). They tell the person what to do instead, in the design's words.
- Sign-in and setup **action** errors (wrong code, passkey cancelled, no connection on Sign in) belong to `sign-in-form.md`, `passkey-flows.md` and `totp-setup.md`. Their no-connection line uses microcopy.md §9's "Not signed in: no connection. Try again." (see Consistency below).

### Today (§4.2)

- **One region for both requests** (`/api/today` and `/api/me/drafts`), shown together or not at all, so unfinished drafts are never hidden without a word (`participant-list-rows.md`).
- **The `<h1>` "Today · Thursday 1 October" shows at once**: its date comes from `/me`, which the shell already has.
- **The search box appears with the list**, not before (`search-filter.md`). A box above an unloaded list invites typing that would produce a false "No participant matches".
- **Empty:** "No participants yet." A manager also sees **Add participant**, a link to the add-participant page in Manage > Participants. A worker sees only the sentence; there is nothing a worker can do about it in the app.
- **After Submit**, the form clears the cached Today data (`participant-list-rows.md`), so Today loads fresh and may briefly show "Loading participants…". The "Note for Jane Citizen submitted" message has its own place above the list and is not affected by the loading line.
- **Someone else's draft** ("Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex can finish it.") is information, not an error: no error styling.

### Past notes and the read view (§4.4)

- **Past notes** loads the participant (`GET /api/participants/{id}`) and the first 30 notes together. The `<h1>` is the participant's name, so it mounts with the data and takes the pending focus. A failed load shows the fallback `<h1>` "Past notes".
- **Empty:** "No notes yet for Jane Citizen." Managers still see Write past-day note, Export record and Edit participant above it, because they come from the participant request, not the notes list.
- **Show older:** the button keeps its label and a status line under it says "Loading older notes…" after 1 s (`chronological-list.md`, as the screen specs chose). If it fails, the 30 notes already shown stay, and the status line says "Older notes did not load: {cause}. Try again.", then "Older notes still did not load: …" on a repeat. The **Show older** button itself is the retry; no second button. On success, focus moves to the first new note (`primary-actions.md`).
- **Read view:** "Loading note…". A `404` (for example, a draft discarded since the link was made) shows "Page not found". Manager-only parts (review status, comment, Version history link) come in the same response, so they never load separately.
- **Edit refused** (`403 note.not_editable`): "You cannot edit this note" with **Read the note**.

### Flagged notes (§4.6)

- **Each tab panel is its own region** with its own status line. Switching tabs does not re-announce the other tab's message.
- **Tab label:** "To review" with no number until the list has loaded, then "To review (n)" (design). Never a placeholder "(0)". If the list fails, the label stays without a number.
- **Empty:** "No flagged notes to review." and, on Reviewed, "No reviewed notes yet." (proposed). These are good news and are written plainly, with no celebration.
- **Reviewed paging:** the same Show older behaviour as Past notes.
- Opening a flagged note uses the read view's states; the review panel's errors belong to `primary-actions.md`.

### Daily report (§4.7)

- **No loading message.** `has-notes` is a tiny request; while it runs, both downloads are unavailable without a message (`primary-actions.md`).
- **Empty:** "No submitted notes for Thursday 1 October 2026." (design), with both download buttons disabled and pointing to the sentence with `aria-describedby` (`primary-actions.md`). It **is** announced through the screen's status line, because it follows the manager's own date change and focus stays on the date controls. (The shell should therefore not move focus to the `<h1>` when only the report date changes. Raised for `app-shell-nav.md`.)
- **`has-notes` failed:** leave both buttons available and show nothing; the download reports its own failure (`primary-actions.md`: "Don't block the manager because of a failed pre-check"). If the download comes back `404 report.no_notes`, show the empty sentence and make both buttons unavailable.
- **URL with a badly formed date** (for example `/reports/daily/2026-13-45`): "Page not found". A well-formed future date needs nothing special: the server says there are no notes (§6.5), so the empty sentence shows.
- Nothing about the report's content is ever shown on screen (D43), so there is no "report loading" state.

### Participants list and participant detail (§4.8)

- **List:** "Loading participants…". Active and Archived come from one request (`includeArchived`), so switching the toggle never loads. Empty Active: "No participants yet." (no second Add participant: it is already on the page). Empty Archived: "No archived participants." (proposed). No search match: "No participant matches 'xyz'" (`search-filter.md`).
- **Detail:** the details and the goals load together; "Loading participant…". The `<h1>` is the participant's name (pending focus); a failed load shows the fallback `<h1>` "Participant". A `404` shows "Page not found".
- **No goals:** "No goals yet. Notes will show an empty Goals section." The **Add goal** field is right there.
- **Archived goals section:** left out when there are none.
- **Archived participant:** the design's read-only banner with Restore (§4.8) is information, not an error: neutral styling, no red.
- **Write past-day note:** opening the chosen date is an action, so its busy state and failure belong to `primary-actions.md`; the failure line follows microcopy.md §9.
- Stale saves (`412`) and reorder mismatches (`422 config.order_mismatch`) reload the list; their wording belongs to `form-validation.md` ("Someone else changed…").

### Common items (§4.9)

- "Loading common items…". The page is never empty of groups, because Every note is built in. A group with no items shows "No items in this group. It does not show on notes until it has one." with **Add item** already in that group; archived sections left out when empty.
- An empty group is normal before go-live set-up, not an error. No warning styling.

### Users (§4.11)

- "Loading users…"; there is no empty state, because the signed-in manager is always listed.
- User detail: "Loading user…", the user's name as `<h1>` (pending focus), fallback `<h1>` "User", and "Page not found" for a `404`.
- Errors from Invite, Edit, Resend, Reset sign-in, Deactivate and Reactivate belong to `form-validation.md` and `confirm-dialog.md`.

### Consistency with other component specs

These sibling specs proposed wording or behaviour before this cross-cutting spec existed. Bringing them in line removes several versions of the same message. None of this adds a feature.

| Spec | Now says | Change to | Why |
|---|---|---|---|
| `participant-list-rows.md` | "Couldn't load the participant list. Check your connection and try again." | "The participant list did not load: no connection. Try again." (microcopy.md §9) | No negative contractions (GOV.UK); one wording app-wide |
| `participant-list-rows.md` | "The header and search show at once." | Header at once; search box with the list | Agrees with `search-filter.md`; avoids a false "No participant matches" |
| `app-shell-nav.md` | "Can't connect to Grow2Notes. Check your internet connection, then try again." | `<h1>` "Could not connect to Grow2Notes", status line "Grow2Notes did not load: no connection. Try again." | Same reasons |
| `app-shell-nav.md` | Page-name list | Use app-shell.md's rebuilt union | Title for the crash page and the screens' own pages |
| `primary-actions.md` | "Didn't download: no connection. Try again." | "Not downloaded: no connection. Try again." (microcopy.md §9) | No negative contractions; one action-failed shape |
| `sign-in-form.md`, `totp-setup.md`, `passkey-flows.md` | "No connection. Check your internet and try again." and three close variants | "Not signed in: no connection. Try again." / "Not set up: no connection. Try again." (microcopy.md §9) | One shape for one situation |
| `session-timeout.md` | "No connection. Check your internet and try again." | "No connection. Try again." (app-shell.md) | The dialog has one action |
| `sign-in-form.md`, `totp-setup.md` | "Something went wrong (on our side). Try again in a few minutes." | "Not signed in: something went wrong. Try again." (microcopy.md §9) | One server phrase, always inside the "Not [done]:" shape |

The design's own strings that contain negative contractions (for example "can't be started for yesterday", "can't sign in") are left exactly as written.

---

## Anti-patterns to avoid

- **A spinner or skeleton for waits under 1 s.** It reads as a glitch, and NN/g advises no indicator at that speed.
- **Skeleton rows on Today or any list.** Two studies disagree on whether skeletons feel faster at all (Viget against Mejtoft), and a skeleton must match the final layout (AgDS), but these rows change height with status lines, tags and wrapping at 200 % text, so no fixed placeholder can match them. A shimmer would also add motion to a screen tired people use at the end of a shift.
- **A full-screen overlay or blanket** while one region loads. The nav and header should stay usable (AgDS: no full-screen blanket for partly loaded pages).
- **"No participants yet." (or any empty sentence) before the data has arrived**, or from a failed request. This is NN/g's "misleading system-status" failure, and on Today it would tell a worker that a participant does not exist.
- **Testing `isError` instead of `isLoadingError`**, which swaps a perfectly good list for an error when a background refresh fails.
- **Default `networkMode: 'online'` for screen queries**, which leaves an offline phone saying "Loading…" for ever.
- **Retrying `4xx` responses**, or retrying three times with a 30 s cap (TanStack's default), which keeps a tired worker waiting with no news.
- **`{error && <div role="alert">…</div>}`**: a live region created with its text is often not announced. Keep the status element mounted and change its text.
- **Identical text after a second failure.** Many screen readers do not re-announce unchanged text; use "… still did not load: …".
- **`disabled` on Try again while it runs**, which throws focus to `<body>`.
- **Toasts for load errors.** A message that disappears takes the only route to Try again with it (SC 2.2.1).
- **Red text, warning icons, exclamation marks, "Oops", "Sorry", a bare "Something went wrong" with no subject or next step, "Error 500", "Forbidden", "Invalid".** GOV.UK and the NHS App ban these on error pages, and they frighten or confuse readers of English as a second language. "something went wrong" is allowed only inside microcopy.md §9's shapes, which always name what failed and end "Try again."
- **Showing "Page not found" with a `403` wording such as "You don't have permission"** for manager routes. It reveals that the page exists (§9.2) and adds a second message for the same situation.
- **"Not started", "Missing" or "No note yet" anywhere**, including empty states (D25, A17).
- **Encouraging copy in empty states** ("All caught up!", "Get started by…"). The owner asked for plain screens, and celebration on "No flagged notes to review." trivialises flags.
- **Help text or tips added to empty states** beyond the design's sentence. It adds content nobody asked for.
- **Reloading automatically** on an error or on `vite:preloadError`: it throws away text held in memory (§8.5).
- **Saving anything about failed loads in `localStorage` or `sessionStorage`** (retry counts, last URL). Keep it in memory (D22, §9.6).
- **Names in a "Page not found" URL, title or message.** The URL holds only IDs and dates, and titles stay generic (§4.0).

---

## Tensions with decisions

- **D22 (online only, nothing stored on the device) against "show last-known content when offline".** [Convention] Mobile guidance commonly recommends caching the last-known state so an offline user sees something instead of an error (for example Babich, Smashing Magazine 2016, https://www.smashingmagazine.com/2016/09/how-to-design-error-states-for-mobile-apps/). Under D22 and §9.6 there is no service worker or device storage, so a screen opened for the first time without a connection can only show "… did not load: no connection. Try again." Within a session, data already loaded stays visible from the in-memory cache, which limits the effect to first visits. This spec follows D22 and does not recommend a change.

---

## Sources

**Research**
- Nielsen Norman Group, *Response Times: The 3 Important Limits*. https://www.nngroup.com/articles/response-times-3-important-limits/
- Miller, R. B. (1968), *Response time in man-computer conversational transactions*, AFIPS FJCC. https://yusufarslan.net/sites/yusufarslan.net/files/upload/content/Miller1968.pdf
- Nielsen Norman Group, *Progress Indicators Make a Slow System Less Insufferable*. https://www.nngroup.com/articles/progress-indicators/
- Viget (2017), *A Bone to Pick with Skeleton Screens*. https://www.viget.com/articles/a-bone-to-pick-with-skeleton-screens/
- Mejtoft, Långström, Söderström (2018), *The effect of skeleton screens: Users' perception of speed and ease of navigation*, ECCE '18 (sample and significance not verified). https://doi.org/10.1145/3232078.3232086
- Kaplan, K. (2021), NN/g, empty-state design guidelines. https://www.nngroup.com/articles/empty-state-interface-design/
- Nielsen Norman Group, *Error-Message Guidelines*. https://www.nngroup.com/articles/error-message-guidelines/
- NHS App design system, *Error pages* (includes the team's research note). https://design-system.nhsapp.service.nhs.uk/patterns/error-page/
- O'Hara, S. (2022), *Are we live?* https://www.scottohara.me/blog/2022/02/05/are-we-live.html

**Standards**
- W3C, *Understanding SC 4.1.3 Status Messages*. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- W3C, *Understanding SC 2.2.2 Pause, Stop, Hide*. https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html
- W3C, *Understanding SC 1.4.1 Use of Color*. https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- W3C, *Understanding SC 2.4.2 Page Titled*. https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html
- W3C, *Understanding SC 2.4.3 Focus Order*. https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html
- W3C, *WAI-ARIA 1.2* (`status`, `alert`). https://www.w3.org/TR/wai-aria-1.2/#status
- W3C WAI-ARIA APG, *Alert pattern*. https://www.w3.org/WAI/ARIA/apg/patterns/alert/

**Conventions**
- GOV.UK Design System, *Page not found pages*. https://design-system.service.gov.uk/patterns/page-not-found-pages/
- GOV.UK Design System, *There is a problem with the service pages*. https://design-system.service.gov.uk/patterns/problem-with-the-service-pages/
- GOV.UK Design System, *Service unavailable pages*. https://design-system.service.gov.uk/patterns/service-unavailable-pages/
- GOV.UK, *A to Z style guide* (contractions). https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- GOV.UK Design System backlog, *Loading spinner* issue #28 and comments. https://github.com/alphagov/govuk-design-system-backlog/issues/28
- Agriculture Design System (AgDS), *Loading, empty and error states*. https://design-system.agriculture.gov.au/patterns/loading-error-empty-states
- AgDS, *Skeleton*. https://design-system.agriculture.gov.au/components/skeleton
- AgDS, *Loading*. https://design-system.agriculture.gov.au/components/loading
- Nielsen Norman Group (Tankala 2023), *Skeleton Screens 101*. https://www.nngroup.com/articles/skeleton-screens/
- Google, *Cumulative Layout Shift (CLS)*. https://web.dev/articles/cls
- TanStack Query 5, *Query Retries*. https://tanstack.com/query/v5/docs/framework/react/guides/query-retries
- TanStack Query 5, *Network Mode*. https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
- TanStack Query 5, *Migrating to v5* (onlineManager, `isPending`, `throwOnError`). https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5
- TanStack Query 5, `QueryObserverSuccessResult` reference (`isLoadingError`, `isRefetchError`). https://github.com/TanStack/query/blob/main/docs/framework/react/reference/interfaces/QueryObserverSuccessResult.md
- React, *Component: catching rendering errors with an error boundary*. https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary
- React Router, *Error Boundaries*. https://reactrouter.com/how-to/error-boundary
- Vite, *Load error handling*. https://vite.dev/guide/build#load-error-handling
- Babich, N. (2016), *How To Design Error States For Mobile Apps*, Smashing Magazine. https://www.smashingmagazine.com/2016/09/how-to-design-error-states-for-mobile-apps/

**Checked and not used:** Apple HIG *Loading* (page body did not render for the fetch tool), Material 3 empty-states page (404), the GOV.UK spinner's linked draft guidance (Google Doc, not opened), and the full texts of Mejtoft 2018 and its follow-up (paywalled, 403).
