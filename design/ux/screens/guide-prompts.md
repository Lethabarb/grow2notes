# Guide prompts

Screen spec for **Manage > Guide prompts** (design.md 4.10). It puts together the researched component files into
one buildable screen. It adds no field, setting, preview, notification or data. Wherever the component files
disagreed, the choice made is listed under "Conflicts resolved".

**Copy marks.** **(V)** is word for word from design.md. **(P)** is proposed: the design implies the state but gives
no words. Every (P) string here on 9 October 2026 was approved as written (D67); a (P) string added later still needs
the owner's approval. **(M)** is a design or component string changed only because `microcopy.md` says to; D67 does
not cover it, so the owner should still confirm it. Each one is listed under "Conflicts resolved".

**Evidence grades.** [Research] studies or usability testing · [Standard] WCAG 2.2, WAI-ARIA APG, HTML spec,
browser-vendor documentation · [Convention] established design systems · [Opinion] reasoned judgement.

Route `/manage/guide-prompts` (P, following the `/manage/users` pattern in `user-management.md`). Page title
"Grow2Notes – Guide prompts" (`app-shell-nav.md` fixed list).

---

## Purpose and who uses it

**Purpose (V):** "set the placeholder text shown in the empty Guided notes box (D12, D34)". What is saved here becomes
the grey prompt text in every empty Guided notes box, "straight away … including open drafts" (4.10). It is never
copied into a note (§5.3, §11.3).

| Who | When | Device | What they do |
|---|---|---|---|
| One manager, at go-live (§14 M6: the Word template's "prompts become the guide prompts") | Once | Usually a laptop | Pastes the prompts from the Word template. This is the most likely time to go over 1,000 characters, and to bring in Word bullets, tabs and blank lines. A second manager then checks them (go-live checklist). |
| Any manager, later | Rarely | Laptop or phone | Small rewordings. The change reaches every worker at once, so a half-edited version must never go live by accident. That is why the page has an explicit Save and no autosave. |
| Workers | Never | – | They cannot reach the page. Opening the URL shows the shell's "Page not found" (`app-shell-nav.md` role guard). The API returns `403` (§2, M1 test). |

Managers often reach the page tired or in a hurry, between other jobs. The screen therefore has one field, one
button and no choices to make.

---

## Layout - phone (about 375 px)

Single column, 16 px gutters (343 px content width). Nothing is sticky or fixed: not the header, and not the Save
button (`app-shell-nav.md`, `primary-actions.md`, `foundations.md`).

```
+---------------------------------------+
| Grow2Notes                  Account v |  <header> (static): wordmark, Account disclosure
| Today  Flagged [3]  Report  Manage    |  <nav aria-label="Main">, 48 px links
|                             ======    |  Manage: bold + 4 px bar, aria-current="true"
+---------------------------------------+
|                                       |  <main id="main-content">
| Guide prompts                         |  <h1 tabindex=-1><label for=…>  28 px bold
|                                       |    route focus lands here
| Prompts are a guide only. They        |  help text (V): body 18 px, --colour-text
| disappear as soon as someone starts   |
| typing and are not saved into notes.  |
|                                       |
| You can enter up to 1,000 characters  |  limit hint (M): 16 px, --colour-text-secondary
| +-----------------------------------+ |
| | Mood and wellbeing today?         | |  <textarea>: same box as Guided notes
| | What did you do together?         | |  (shared TextBox.module.css), 18 px / 1.5,
| | Anything to follow up?            | |  text in --colour-text (never grey),
| |                                   | |  no placeholder, min about 6 lines,
| |                                   | |  grows with the text; the page scrolls
| +-----------------------------------+ |
|                                       |  count: hidden until 900 characters
|                                       |  request-error alert: in the DOM, empty
| +-----------------------------------+ |
| |               Save                | |  primary button, full width, 48 px
| +-----------------------------------+ |
| Saved 4:12 pm                         |  <p role="status">: empty until a save
|                                       |
+---------------------------------------+
```

**Loading and load-failed** (before `GET /api/admin/guide-prompts` returns, no field and no Save are drawn):

```
| Guide prompts                         |  h1, help text and limit hint render at once
| Prompts are a guide only. …           |
| You can enter up to 1,000 characters  |
| Loading guide prompts…                |  <p role="status">, text only after 1 s
```
```
| The guide prompts did not load: no    |  same status element
| connection. Try again.                |
| +-----------------------------------+ |
| |            Try again              | |  secondary button, outside the status
| +-----------------------------------+ |
```

**Over the limit, after Save is pressed:**

```
| You can enter up to 1,000 characters  |
|▌Error: Guide prompts must be 1,000    |  bold, --colour-error; "Error:" visually hidden
|▌characters or less                    |  4 px red bar on the group's start edge
|▌+-----------------------------------+ |  3 px red border on the box
|▌| …pasted text…                     | |
|▌+-----------------------------------+ |
|▌You have 212 characters too many      |  bold, --colour-error
| [               Save                ] |
```

**Leave dialog** (in-app navigation or Sign out while there are unsaved changes):

```
+---------------------------------------+
|  Leave without saving?                |  <h2>, the dialog's name
|                                       |
|  Your changes to the guide prompts    |  body, the dialog's description
|  are not saved.                       |
|                                       |
| +-----------------------------------+ |
| |        Stay on this page          | |  primary, focused on open
| +-----------------------------------+ |
| +-----------------------------------+ |
| |       Leave without saving        | |  secondary, not red
| +-----------------------------------+ |
+---------------------------------------+
   page behind: dimmed by ::backdrop
```

---

## Layout - laptop (40rem and wider)

| | Phone (< 40rem) | Laptop (≥ 40rem) |
|---|---|---|
| Header | Two rows, wraps if it must | The same two rows, inside the 60rem page container |
| Content width | Full width inside 16 px gutters | **The 40rem `--measure` column**, left-aligned in the 60rem container. 4.10 is not one of the full-width setup screens (`foundations.md` lists 4.8, 4.9 and 4.11). The column must equal the note form's column, so that prompt lines wrap where they will wrap in a worker's Guided notes box on a laptop. The space to the right stays empty on purpose. |
| Gutters / gaps | 16 px / 32 px | 32 px / 48 px |
| h1 | 28 px | 32 px |
| Textarea | Full column width | 40rem wide, same font, padding and line height as on the phone |
| Save + status | Save full width; "Saved 4:12 pm" on its own line under it | Save `width: auto` (at least 8rem), left-aligned; the status sits inline to its right and wraps under it if space runs out |
| Leave dialog | Full width minus margins; buttons stacked, full width | Fixed width (`confirm-dialog.md`); buttons **still stacked and full width inside the dialog** |
| Typical input | Small edits, keyboard or dictation | Pasting from the Word template, keyboard |

```
+---------------------------------------------------------------------+
| Grow2Notes                                                Account v |
| Today   Flagged [3]   Report   Manage                               |
|                                ======                               |
+---------------------------------------------------------------------+
|   Guide prompts                                                     |
|                                                                     |
|   Prompts are a guide only. They disappear as soon as               |
|   someone starts typing and are not saved into notes.               |
|   You can enter up to 1,000 characters                              |
|   +-------------------------------------------+                     |
|   | Mood and wellbeing today?                 |   <- 40rem, same    |
|   | What did you do together?                 |      as the note    |
|   | Anything to follow up?                    |      form column    |
|   +-------------------------------------------+                     |
|   [  Save  ]   Saved 4:12 pm                                        |
+---------------------------------------------------------------------+
```

---

## Components, in order

DOM order equals visual order. Every token name below is from `foundations.md`.

### 1. App shell: header, nav and Account menu ([app-shell-nav](../components/app-shell-nav.md))

- **Manage** shows as the current *section*: `aria-current="true"`, bold, 4 px `--colour-action` bar. The page is a
  child of `/manage`.
- **Account > Sign out** must ask this page's leave guard **before** it posts `/api/auth/logout` (see Interactions,
  and Cross-screen issues). This is the one way this screen changes shell behaviour.
- The 30-minute idle warning dialog and sign-in-in-place come from the shell ([session-timeout](../components/session-timeout.md)).

### 2. Page heading, which is also the field's label ([settings-textarea](../components/settings-textarea.md), [foundations](../components/foundations.md))

- `<h1 tabindex="-1" class="pageHeading"><label for="guidePrompts-text">Guide prompts</label></h1>` **(V)**.
  When a page asks one question, the label is the page heading, so a screen reader does not hear it twice
  [Convention] https://design-system.service.gov.uk/get-started/labels-legends-headings/.
- **Rendered at once, outside the data-dependent form.** It is a fixed `<h1>`, so the shell's route focus can land
  on it straight away (`empty-loading-error.md` rule 1). Until the field mounts, the `for` points at nothing, which
  does no harm.
- `--font-size-h1`, weight 700, sentence case, `width: fit-content` so the focus box hugs the text.

### 3. Help text ([foundations](../components/foundations.md), [microcopy](../components/microcopy.md))

- `<p id="guidePrompts-help">Prompts are a guide only. They disappear as soon as someone starts typing and are not
  saved into notes.</p>` **(V)**.
- **Body size (18 px) in `--colour-text`**, not hint styling. It is the one thing the manager must understand before
  editing (`foundations.md`, 4.10 note). It is ordinary page text: no banner, no box, no icon (`status-messages.md`).

### 4. Limit hint ([microcopy](../components/microcopy.md), [settings-textarea](../components/settings-textarea.md))

- `<p id="guidePrompts-limit" class="hint">You can enter up to 1,000 characters</p>` **(M)**: the GOV.UK character
  count hint [Convention] https://design-system.service.gov.uk/components/character-count/, built from the design's
  "up to 1,000 characters" (A6).
- `--font-size-small` (16 px), `--colour-text-secondary` (9.0:1). Always visible: it states the limit before anyone
  hits it.

### 5. Load status and Try again ([empty-loading-error](../components/empty-loading-error.md), [primary-actions](../components/primary-actions.md))

- `<p role="status" class="loadStatus">`, rendered on every pass and empty by default, so it exists before text is
  written into it [Standard] https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22.
- Nothing for the first second, then "Loading guide prompts…" (P). Below 1 s an indicator only flickers [Research]
  https://www.nngroup.com/articles/progress-indicators/; the exact 1 s is [Opinion], matching the rest of the app.
- Load failed (after one silent retry, `networkMode: 'always'`): "The guide prompts did not load: no connection. Try
  again." or "The guide prompts did not load: something went wrong. Try again." (P, microcopy pattern). After a
  failed **Try again**: "The guide prompts still did not load: …" (P). The changed words make screen readers
  announce it again.
- **Try again**: a secondary `<button type="button">`, *outside* the status element so it is not read as part of the
  message. Busy: `aria-disabled`, label "Loading…" after 400 ms.
- Empty once the data has arrived.

### 6. The form: guide prompts editor ([settings-textarea](../components/settings-textarea.md))

**Mounted only after `GET /api/admin/guide-prompts` has returned.** An empty, editable box shown during loading
could be saved as "no prompts" and wipe the placeholder for every worker at once [Opinion].

`<form noValidate onSubmit>` (no `<form action>`: React 19 resets uncontrolled fields after a form action succeeds,
which would empty the box [Standard] https://react.dev/reference/react-dom/components/form). Inside it, in order:

**6a. Field error** ([form-validation](../components/form-validation.md), one-field pattern: inline error only, no error
summary, focus moves to the field)

- Rendered only after a failed Save. `<p id="guidePrompts-error" class="errorMessage"><span class="visually-hidden">Error:
  </span>Guide prompts must be 1,000 characters or less</p>` (P, microcopy "too long" pattern; GOV.UK template
  [Convention] https://design-system.service.gov.uk/components/character-count/).
- Group styling: 4 px `--colour-error` bar on the inline-start edge, bold error text, 3 px error border on the box.
  The border alone is not enough, because black to dark red is only a 2.4:1 change (`foundations.md`).

**6b. Textarea**

| Setting | Value | Why |
|---|---|---|
| Element | `<textarea id="guidePrompts-text" name="text">`, uncontrolled, `defaultValue` = loaded text, read once | A refetch must never overwrite typing (settings-textarea Behaviour 2) |
| Name | From the `<h1><label>`: "Guide prompts" | SC 2.5.3: visible label = accessible name |
| Description | `aria-describedby="guidePrompts-help guidePrompts-limit"`, plus `guidePrompts-error` while an error shows | SC 1.3.1, 3.3.2 |
| `aria-invalid` | `"true"` only after a failed Save; removed once fixed | GOV.UK validation pattern |
| `placeholder` | **None** | An example here could be mistaken for saved prompts |
| Text colour | `--colour-text` (`#0b0c0c`), **never** `--colour-placeholder` | Grey editable text reads as empty or disabled ([Research] https://www.nngroup.com/articles/form-design-placeholders/; [Convention] https://developer.mozilla.org/en-US/docs/Web/CSS/::placeholder) |
| Box metrics | From the **shared `TextBox.module.css`** that the Guided notes box also uses: `font: inherit`, `font-size: max(var(--font-size-body), 16px)` (18 px), `line-height: 1.5`, padding 0.75rem, 2 px `--colour-border-control` border, `--radius-s`, `min-block-size: 11em` (about 6 lines), no maximum height, `resize: none`, `white-space: pre-wrap` | The only "preview" the design allows: the same box, so the lines wrap the same way. Text of 16 px or more stops iOS zooming on focus [Convention] https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/ |
| Width | Phone: the full column. Laptop: `--measure` (40rem), the note form's column | Same wrapping as the worker's box |
| Growth | `field-sizing: content` behind `@supports`, with the grid `::after` mirror as a fallback | `field-sizing` became Baseline 2026, newly available since June 2026, so older phones need the fallback [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/field-sizing |
| Limits | **No `maxlength`.** The limit is checked when Save is pressed | `maxlength` silently cuts off a paste [Convention] https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/ |
| Input aids | `spellCheck`, `autoCapitalize="sentences"`, `autoComplete="off"`; `writingsuggestions="false"` set with `setAttribute` | Typos would show on every worker's note. Browser AI completions are left out, in the spirit of D31 [Opinion] |
| Focus | 3 px `--colour-focus` outline, 2 px offset, `:focus-visible`, no transition; `Highlight` in forced colours | `foundations.md` |
| Never | `disabled`, `readonly`, autofocus on page load | On a phone, autofocus would open the keyboard over the help text |

**6c. Character count** ([settings-textarea](../components/settings-textarea.md), [microcopy](../components/microcopy.md))

- Counts `value.trim().length` (UTF-16 code units: the same as .NET `string.Length` and `nvarchar(1000)`), because
  the trimmed text is what is checked and saved.
- **Hidden below 900.** From 900: "You have 100 characters remaining" in hint style. At 1,000: "You have 0 characters
  remaining". Over 1,000: "You have 212 characters too many" in bold `--colour-error`. Singular: "1 character". All
  GOV.UK strings, with numbers from `Intl.NumberFormat('en-AU')` [Convention, GOV.UK 2017 testing with 17 users]
  https://design-system.service.gov.uk/components/character-count/.
- The visible count is `aria-hidden="true"`. A visually hidden `aria-live="polite"` twin, always in the DOM, repeats
  it 1 s after typing stops. It never announces on every keystroke.
- Over the limit before Save: count in red and the border in the error colour, but **no** `aria-invalid` and no
  error message until Save is pressed.

**6d. Request-error alert** ([primary-actions](../components/primary-actions.md))

- `<div role="alert" class="actionError">`, **always in the DOM** and empty, directly above Save. Errors written into
  a region created at the same moment can go unannounced [Opinion, AT testing]
  https://www.scottohara.me/blog/2022/02/05/are-we-live.html.
- Holds the network, server and conflict messages (States). Cleared when Save is pressed again.

**6e. Save button** ([primary-actions](../components/primary-actions.md))

- `<Button type="submit" variant="primary" busy busyLabel="Saving…" announceBusy={false}>Save</Button>` **(V label)**.
  `announceBusy={false}` because `SaveStatus` (6f) already says "Saving…"; the page status region would read it
  twice (app-shell.md component 3). 48 px tall, full width
  on a phone, label-width on a laptop, left-aligned.
- **Never unavailable**: not while there are no changes, and not while the text is over the limit (validation never
  disables). It is `aria-disabled="true"` only while busy, from the moment it is pressed, so a second press is
  ignored. The busy label "Saving…" (P) shows only after 400 ms, in the same grid cell, so the button never changes
  size. No spinner. Busy buttons that use `disabled` drop focus to `<body>` [Convention]
  https://react-aria.adobe.com/Button.
- Inline after the field, never sticky. A fixed bar can sit under the phone keyboard [Standard]
  https://developer.chrome.com/blog/viewport-resize-behavior and can hide focus (SC 2.4.11).

**6f. Save status** ([status-messages](../components/status-messages.md) `SaveStatus`)

- `<p role="status" class="saveStatus">`, **always rendered** and never `display: none`. It sits beside Save on a
  laptop and under it on a phone. `--font-size-small`, `--colour-text-secondary`, `tabular-nums`.
- Cleared when Save is pressed. After 400 ms of waiting it holds a visually hidden "Saving…" (the same moment the
  button label changes), so the waiting state is announced (SC 4.1.3). On success it reads "Saved 4:12 pm" (P, the same
  words as the note form's "Saved 9:42 am"). It is cleared on the next keystroke, so it never claims "Saved" while
  there are unsaved edits.
- The time comes from the PUT response's `Date` header (the server clock), formatted with the shared `time` token
  (`microcopy.md`: Melbourne, "4:12 pm", non-breaking space). If the header is missing, show "Saved" with no time.
  The device clock is never used (microcopy formats rule).

### 7. Leave dialog ([confirm-dialog](../components/confirm-dialog.md) shell, copy from [primary-actions](../components/primary-actions.md), behaviour from [settings-textarea](../components/settings-textarea.md))

- Native `<dialog role="alertdialog" aria-modal="true" aria-labelledby aria-describedby>` opened with `showModal()`.
- Title `<h2>` "Leave without saving?" (P). Body "Your changes to the guide prompts are not saved." (P, (M) wording).
- Buttons, stacked and full width: **"Stay on this page"** (P), primary, first, **focused on open**; **"Leave without
  saving"** (P), secondary, **not red**. A few lines of prompt text are easy to type again, and red is kept for the
  **two** actions that cannot be undone: Discard draft and Reset sign-in (`primary-actions.md`, `confirm-dialog.md`).
- Escape, the Android back gesture and `cancel` all mean Stay. Tapping the backdrop does nothing (`closedby`
  default for `showModal()`). No close ×.
- A `close` event with an empty `returnValue` also means **Stay**: the shell closes any open dialog with a plain
  `close()` before the 28-minute session warning (app-shell.md, "every confirmation treats that as Go back"). The
  dialog's `close` handler calls `blocker.reset()` (or resolves `confirmLeave()` with `false`) whenever
  `returnValue` is not `'leave'`, so `useBlocker` is never left in the blocked state. Only **Leave without saving**
  closes with `returnValue = 'leave'`.
- Focus is set with `.focus()` straight after `showModal()`, because React's `autoFocus` does nothing inside a
  dialog that is still closed [Standard: library issue] https://github.com/facebook/react/issues/23301.
- No busy state and no error region: the dialog sends no request.
- Never `window.confirm()` or `unstable_usePrompt`.

### 8. Browser-level warning (no component of ours)

The browser's own "Leave site?" dialog for reload, closing the tab and typed URLs. Its wording cannot be set
[Standard] https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event.

---

## States

| State | When | What shows (exact copy) | Semantics |
|---|---|---|---|
| **Waiting** | GET in flight, under 1 s | h1, help text, limit hint. No field, no Save | Route focus on the h1 |
| **Loading** | GET still in flight after 1 s | "Loading guide prompts…" (P) | Polite `role="status"` |
| **Load failed** | GET failed after one silent retry | "The guide prompts did not load: no connection. Try again." or "The guide prompts did not load: something went wrong. Try again." (P) + **Try again** | Polite status; Try again outside it |
| **Still failing** | Try again failed | "The guide prompts still did not load: no connection. Try again." (or "…something went wrong…") (P) | New words, so it is announced again. Focus stays on Try again |
| **Default, prompts saved** | GET returned text | The field holds the saved text exactly, line breaks kept, in the normal text colour | `aria-describedby` help + limit |
| **No prompt text** (4.10) | GET returned `""` | An empty, roughly 6-line box with **no placeholder**. Nothing else changes: the help text already explains. Saving an empty field is valid and means "the box shows no placeholder" (V) | Same |
| **Editing (dirty)** | Trimmed field text differs from the last saved text | Text as typed. "Saved 4:12 pm" cleared on the first keystroke. **No "unsaved" badge** (the design specifies none) | Leave guards armed |
| **Near the limit** | 900 to 1,000 characters | "You have 100 characters remaining" … "You have 0 characters remaining" | Visible count `aria-hidden`; polite twin after 1 s |
| **Over the limit, before Save** | More than 1,000 characters | "You have 212 characters too many" in bold red; box border red | Not `aria-invalid` yet |
| **Too long, after Save** (or `422 validation.failed`) | Save pressed while over 1,000 | "Error: Guide prompts must be 1,000 characters or less" ("Error:" visually hidden) above the box, red bar, 3 px red border. Nothing is sent | `aria-invalid="true"`, error id added to `aria-describedby`, **focus moves to the field**. Cleared silently once the length is 1,000 or less |
| **Busy** | PUT in flight | Save looks pressed; after 400 ms its label reads "Saving…" (P). The field stays editable | Save `aria-disabled="true"`; status holds hidden "Saving…" after 400 ms |
| **Saved** | PUT returned `200` | "Saved 4:12 pm" (P) beside or under Save, until the next keystroke or until the manager leaves | Polite `role="status"`; focus stays on Save |
| **Not saved: no connection** | PUT failed at the network or timed out | "Not saved: no connection. Try again." (P) above Save. Text kept, still dirty | `role="alert"`; focus stays on Save. No automatic retry |
| **Not saved: server** | `5xx`, `428`, or anything unexpected | "Not saved: something went wrong. Try again." (P) | As above |
| **Conflict** (`412 precondition.failed`) | Another manager saved first | "Another manager saved the guide prompts while you were editing. Your text is still here. Select Save to replace their version with yours." (P). Typed text unchanged | `role="alert"`; focus stays on Save. The base text and ETag are refetched; the field is not touched |
| **Leaving while dirty, in-app** | A link, the Account menu's Sign out, or in-app Back/Forward | Leave dialog: "Leave without saving?" / "Your changes to the guide prompts are not saved." / "Stay on this page" / "Leave without saving" (P) | `alertdialog`; focus on Stay |
| **Leaving while dirty, browser** | Reload, closing the tab, a typed URL, Back out of the app | The browser's own dialog. It may not appear at all on phones (MDN) | `beforeunload` listener only while dirty |
| **Idle warning** | 28 minutes with no request. Typing here sends nothing, so a long edit can reach it | The shell's "You'll be signed out in 2 minutes" with **Stay signed in** (V) | `session-timeout.md` |
| **Signed out mid-edit** | Any `401`, or the 30-minute idle timeout | Sign-in in place. This route stays mounted but `hidden`, so the text survives in memory only. After the same person signs in, the page is back as it was, still dirty. **Save is not replayed** | `session-timeout.md` |
| **A different person signs in** | Shared laptop | Full reload to Today. The unload guard is disarmed first, so no browser dialog appears. The previous person's unsaved text is dropped | `session-timeout.md` |
| **Role removed** | PUT or GET returns `403` (a manager was made a worker) | The shell refreshes `/me`; its role guard then shows "Page not found" | `app-shell-nav.md`, `empty-loading-error.md` |
| **Worker opens the URL** | – | "Page not found" | Shell role guard |
| **Hover** (laptop) | – | Field: no change, text cursor only. Save: `--colour-action-hover`. Hover never carries information | `@media (hover: hover)` |
| **Disabled / read-only** | **Never** | – | – |

---

## Interactions and focus

### Focus order (Tab)

Skip link → wordmark → Account → Today → Flagged → Report → Manage → (Try again, only while load failed) → **Guide
prompts** field → **Save**. In the leave dialog: Stay on this page → Leave without saving, trapped by the native
modal.

The `<h1>` (`tabindex="-1"`), help text, limit hint, count and status lines are not in the Tab order.

### What happens on each action

| Action | What happens | Focus afterwards | Announced |
|---|---|---|---|
| **Arrive on the page** | Shell scrolls to the top; GET starts | The `<h1>` (shell route rule) | "Guide prompts, heading level 1" |
| **Data arrives** | Field, alert, Save and status mount. The field is never focused automatically | Unchanged (on the h1) | Nothing |
| **Try again** (load failed) | Refetch; the old message is cleared at once; busy after 400 ms | Success: the h1, because the Try again button has gone. Failure: stays on Try again | Failure: the "still did not load" text |
| **Type or paste** | Status cleared; count updated (shown from 900); an error already showing clears once fixed. **Nothing is sent** (SC 3.2.2) | In the field | Count only, 1 s after typing stops, from 900 characters |
| **Enter in the field** | Inserts a new line. A textarea never submits on Enter | In the field | – |
| **Save, too long** | `text = field.value.trim()`; over 1,000, so the error shows and nothing is sent | **The field** | "Guide prompts, invalid entry, …, Error: Guide prompts must be 1,000 characters or less" (wording varies by reader) |
| **Save, valid** | Status and alert cleared. `PUT /api/admin/guide-prompts {text}` with `If-Match: <base ETag>`. Busy from the press | Stays on Save | "Saving…" only if slower than 400 ms |
| **→ `200`** | Base text = sent text; base ETag = response `ETag` (or refetch if the header is absent); dirty recomputed (still dirty if typed during the save); unload listener removed when clean | Stays on Save | "Saved 4:12 pm" |
| **→ `412`** | Refetch (bypassing cache) for the new base text and ETag; field untouched | Stays on Save | The conflict message (alert) |
| **→ `422`** | Same as Save, too long | The field | As Save, too long |
| **→ network / `5xx`** | Nothing changes; the manager presses Save again (`retry: false`, because a retried PUT whose first response was lost would return a false `412`) | Stays on Save | The error message (alert) |
| **→ `401`** | Sign-in in place takes over; nothing shown here | Shell-managed | Shell-managed |
| **In-app link, or in-app Back/Forward, while dirty** | `useBlocker` blocks only when the pathname changes **and** the field (read from the DOM at that moment) differs from the base. The dialog opens | "Stay on this page" | "Leave without saving?, alert dialog, Your changes to the guide prompts are not saved., Stay on this page, button" |
| **Stay on this page / Escape / Android back / a plain `close()` by the shell** | `blocker.reset()` (any `close` whose `returnValue` is not `'leave'`); nothing changes | Back on the element that started it (the link); after the session warning, the shell's own rule | – |
| **Leave without saving** | `blocker.proceed()`; the page unmounts and its unload listener goes with it | The new page's `<h1>` | The new page's heading |
| **Account > Sign out while dirty** | The shell awaits `confirmLeave()` **before** any logout request. Stay: the sign-out is cancelled. Leave: the shell calls `disarmUnloadGuard()`, then runs its normal sign-out (`POST /api/auth/logout`, `location.replace('/')`). If the sign-out then fails, it calls `rearmUnloadGuard()`; the text is still on screen | Stay: the Sign out button. Leave: the sign-in page | As the dialog above |
| **Reload, close or typed URL while dirty** | The browser's own dialog (Chromium and Firefox on laptops; phones may show nothing) | Browser-managed | Browser-managed |
| **Any navigation while clean** | No dialog of any kind | – | – |

### Live regions on this screen

All are rendered from the first paint of their container and only ever filled in. Nothing is `assertive` except the
request-error alert.

| Region | Role | Carries |
|---|---|---|
| Load status | `role="status"` (polite) | "Loading guide prompts…", load-failed and still-failing text |
| Count twin | `aria-live="polite"`, visually hidden | The count text, 1 s after typing stops, from 900 characters |
| Request error | `role="alert"` | Not saved (network, server) and the conflict message |
| Save status | `role="status"` (polite) | Hidden "Saving…" (after 400 ms), then "Saved 4:12 pm" |
| Page status region (`PageStatus`, visually hidden, app-shell.md component 3) | `role="status"` (polite) | Try again's "Loading…" busy label only; Save opts out (`announceBusy={false}`) because Save status carries "Saving…" |

No toast, no banner and no focus move for success: the manager stays on the page, so an inline status is the right
form [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html; [Research]
https://www.nngroup.com/articles/indicators-validations-notifications/.

---

## Accessibility checklist

**Headings and page**
- [ ] One `<h1>`, "Guide prompts", which also labels the field. The dialog's title is an `<h2>`.
- [ ] Page title "Grow2Notes – Guide prompts" (SC 2.4.2); no personal data in the title or the URL.
- [ ] `<html lang="en-AU">` (SC 3.1.1).

**Landmarks**
- [ ] Skip link first; `<header>` (banner), `<nav aria-label="Main">`, one `<main id="main-content">`. No other
  landmarks on this page.

**Labels and descriptions**
- [ ] The field is named "Guide prompts" by `<label for>` inside the h1. No `aria-label` (SC 2.5.3, 4.1.2).
- [ ] `aria-describedby` = help + limit (+ error while showing) (SC 1.3.1, 3.3.2).
- [ ] No `aria-required`: empty is valid here.
- [ ] Repeated Save buttons: none. "Save" is the whole name.

**Keyboard**
- [ ] Every control reachable and operable by keyboard alone; DOM order = visual order (SC 2.1.1, 2.4.3).
- [ ] Focus visible on every stop: 3 px near-black ring, 2 px offset (SC 2.4.7; also 2.4.13 AAA).
- [ ] Nothing sticky, so focus is never hidden (SC 2.4.11).
- [ ] Dialog: focus trapped by `showModal()`, Escape = Stay, focus returns to the trigger (SC 2.1.2, APG
  https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/).
- [ ] No keyboard shortcuts; Enter in the field inserts a line break.

**Screen reader**
- [ ] Arrival: heading announced once (route focus).
- [ ] Field: name, content, then the help and limit descriptions.
- [ ] Waiting, Saved, the count and errors are announced without moving focus (SC 4.1.3), except the too-long error,
  which moves focus because the person must act on the field.
- [ ] Saving twice with no change in between is announced twice (the status is cleared on press).
- [ ] The visible count is `aria-hidden`; only its polite twin speaks.

**Visual**
- [ ] All text ≥ 7:1 except the placeholder token, which is not used here (SC 1.4.3, with glare margin).
- [ ] Box border, Save fill, focus ring ≥ 3:1 (SC 1.4.11).
- [ ] Errors use words, a bar and a border, never colour alone (SC 1.4.1).
- [ ] No fixed heights; reflows at 320 px; survives 200% text and SC 1.4.12 spacing (SC 1.4.4, 1.4.10, 1.4.12).
- [ ] Targets: Save and Try again 48 px; the box far larger (SC 2.5.8; A32 44 px).
- [ ] Forced colours: real borders on the box, the error bar and the buttons; `Highlight` focus; `GrayText` for the
  busy `aria-disabled` Save.
- [ ] No motion of any kind (SC 2.3.3, 2.2.2).

**Errors and data**
- [ ] SC 3.3.1 and 3.3.3: the error names the field and the fix; the count says how much to cut.
- [ ] SC 3.3.4: checked before saving, and reversible by editing and saving again; the audit log keeps old and new
  values (§6.6). https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- [ ] SC 3.2.2: nothing is sent while typing.

---

## Acceptance criteria

**Access and structure**
- [ ] A manager on `/manage/guide-prompts` sees the h1 "Guide prompts", the help text (V) and "You can enter up to
  1,000 characters". The Manage nav item has `aria-current="true"`; the page title is "Grow2Notes – Guide prompts".
- [ ] A worker opening the URL sees "Page not found". The API returns `403` for GET and PUT as a worker (M1 test).
- [ ] After a client-side navigation to the page, `document.activeElement` is the h1. The field is never focused
  automatically.

**Loading**
- [ ] With GET delayed 3 s: no field and no Save button exist at any time before the response; the status is empty
  at 999 ms and reads "Loading guide prompts…" at 1,000 ms.
- [ ] Offline: the load-failed text and Try again appear. A second failure reads "…still did not load…". Success
  after Try again mounts the field and moves focus to the h1.
- [ ] Revisiting the page in the same session always starts from a fresh GET (the query uses `gcTime: 0`), so the
  editor never mounts from a cached copy.

**Field**
- [ ] The saved text shows exactly, line breaks included, in `#0b0c0c`. The textarea has no `placeholder`,
  `maxlength`, `disabled` or `readonly` attribute.
- [ ] The editor and the note form's Guided notes box use the same `TextBox.module.css` class. At 375 px and 1280 px
  widths their computed `font-size`, `line-height`, `padding` and `border-width` are equal, and both are 40rem wide on
  the laptop.
- [ ] An empty saved value shows an empty box with no placeholder.
- [ ] Typing, pasting and deleting send no network request.

**Limit and count**
- [ ] No count below 900 trimmed characters. 900 → "You have 100 characters remaining"; 999 → "You have 1 character
  remaining"; 1,000 → "You have 0 characters remaining"; 1,001 → "You have 1 character too many".
- [ ] Pasting 1,200 characters keeps all 1,200. Pressing Save shows "Guide prompts must be 1,000 characters or
  less", sets `aria-invalid="true"`, focuses the field and sends nothing. Deleting down to 1,000 clears the error.
- [ ] A text of exactly 1,000 characters including 10 line breaks saves successfully (client and server count line
  breaks the same way; the client sends `.value`, which uses LF).

**Save**
- [ ] Save sends `PUT /api/admin/guide-prompts` with `If-Match` equal to the GET's `ETag` and `{text}` trimmed of
  leading and trailing whitespace only. Inner blank lines are kept. Whitespace-only text is sent as `""`.
- [ ] Save is never given the `disabled` attribute. Two quick presses send exactly one PUT.
- [ ] With the PUT delayed 2 s: the label changes to "Saving…" at 400 ms, the button's size does not change, and the
  field stays editable.
- [ ] On `200`: "Saved 4:12 pm" shows, using the response `Date` header in Melbourne time (test with a mocked `Date`
  header of 06:12 UTC on 1 October 2026 → "Saved 4:12 pm"). Focus stays on Save. The next keystroke clears it.
- [ ] Two saves in a row with no change are both announced (the status empties on press).
- [ ] Typing during a slow save leaves the page dirty after the `200`.
- [ ] Network failure → "Not saved: no connection. Try again."; `5xx` → "Not saved: something went wrong. Try
  again." Exactly one PUT is sent per press. The text is unchanged.
- [ ] Two managers: A loads, B saves, A saves → A sees the conflict message, A's text is unchanged, and A's next Save
  succeeds with the new ETag. B's version is in the audit log.
- [ ] Every successful save writes one `guide_prompts.updated` audit event with old and new values (§6.6, M1).

**Leaving**
- [ ] Dirty + select Today → the dialog opens with focus on "Stay on this page". Escape → still on the page, URL
  unchanged, text unchanged, focus back on the Today link. "Leave without saving" → Today, focus on its h1.
- [ ] Clean (including typing then deleting back to the saved text) → no dialog for links, Back, reload or Sign out.
- [ ] Dirty + reload (Chromium) → the browser's dialog. After a successful Save → no dialog. No `beforeunload`
  listener is attached while the page is clean (spy on `addEventListener`).
- [ ] Dirty + Account > Sign out → the dialog opens and **no** request to `/api/auth/logout` has been made. Stay → no
  logout request. Leave → logout runs and no browser dialog appears.
- [ ] A `401` mid-edit, then the same person signs in → the text is still there and still dirty; no PUT is sent until
  Save is pressed again.
- [ ] A different person signs in → full reload to Today, no browser dialog, the old text is gone.

**Storage and naming**
- [ ] Nothing is written to `localStorage`, `sessionStorage` or IndexedDB on any path (spy on `Storage.prototype.setItem`
  and `indexedDB.open`).
- [ ] The copy test passes: no banned words, Australian spelling, no trace of the parent company's name (D42).

**Effect on the note form** (cross-check at M2)
- [ ] After a save, a newly opened note form's empty Guided notes box shows the prompts as its placeholder with the
  same line breaks, and nothing is created by opening it (A8).
- [ ] The placeholder token passes the contrast test at ≥ 4.5:1 (4.10), and the box keeps its visible "3. Guided
  notes" label.

**Accessibility runs**
- [ ] axe-core: no violations in Default, Loading, Load failed, Too long, Saved, Conflict and the open dialog.
- [ ] 320 px, 200% text, 400% zoom: no sideways scrolling, nothing clipped.
- [ ] Windows forced colours: box border, error bar, focus ring and both buttons visible.
- [ ] NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Chrome: the announcements in "Interactions and focus" are
  heard once each.
- [ ] By hand on real phones: the Android back gesture and iOS swipe-back while dirty (blocking a POP is
  unverified, react-router issue #11589), and whether `beforeunload` shows anything.

---

## Conflicts resolved

| # | Where the component files disagreed | Chosen | Why |
|---|---|---|---|
| 1 | Text size in the box: `settings-textarea.md` (copying `guided-notes-textarea.md`) says `max(16px, 1rem)` = 16 px; `foundations.md` says textareas use body text, 18 px | **18 px**: `max(var(--font-size-body), 16px)` in the shared `TextBox.module.css` | `foundations.md` owns sizes ("no other font sizes" in components), and 18 px keeps text above the critical print size out to about 44 cm [Research, via foundations.md]. Both boxes share one module, so they change together and the "preview" holds. |
| 2 | Limit hint: "Up to 1,000 characters" (`settings-textarea.md`) or "You can enter up to 1,000 characters" (`microcopy.md`) | **"You can enter up to 1,000 characters"** (M) | microcopy's character-count pattern, the GOV.UK string. A full sentence reads better for readers with English as an additional language. |
| 3 | Help-text style: hint class (`settings-textarea.md`) or body size in `--colour-text` (`foundations.md` per-screen note) | **Body size, `--colour-text`** for the help text; hint style for the limit line only | The help text explains a consequence that every worker will feel, so it must be full strength. |
| 4 | Loading: nothing for 400 ms then "Loading…" (`settings-textarea.md`) or nothing for 1 s then "Loading {things}…" (`microcopy.md`, `empty-loading-error.md`, `app-shell-nav.md`) | **1 s, "Loading guide prompts…"** | Three files agree; one rule app-wide. 400 ms stays for the Save busy label (`primary-actions.md`), which is a different thing. |
| 5 | Load failed: "Couldn't load the guide prompts. Check your connection and try again." (`settings-textarea.md`), "[Thing] did not load: [cause]. Try again." (`microcopy.md` §9, canonical) | **"The guide prompts did not load: no connection. Try again."** (P); the repeat failure adds "still" | microcopy §9 settles this and bans new negative contractions. "Still" is borrowed from `empty-loading-error.md` so the repeated message is new text and gets announced again. |
| 6 | Where "Saving…" is announced: one app-level `role="status"` (`primary-actions.md`) or no global live region (`status-messages.md`, `app-shell-nav.md`) | **The page's own `SaveStatus` `<p role="status">`** holds a visually hidden "Saving…", then the visible "Saved 4:12 pm" | One region next to the button, so it needs no shell plumbing and does not duplicate "Saving…" on screen. The waiting state is still announced (SC 4.1.3). |
| 7 | Time for "Saved": the response `Date` header with a device-clock fallback (`settings-textarea.md`), or never the device clock (`microcopy.md`) | **`Date` header only;** if it is missing, "Saved" with no time | microcopy's rule is product-wide (D37, A33). A missing `Date` header should not occur from Kestrel, but that is unverified through Azure's front end. |
| 8 | Safe button label: "every dialog's safe button is Go back" (`microcopy.md` §2, `confirm-dialog.md`, `primary-actions.md` §5.5), yet the same files propose "Stay on this page" for this dialog | **"Stay on this page"** | The dialog often appears *because* the manager pressed the browser's Back. "Go back" would then name the very action being blocked. This dialog is not a commit confirmation, so the Go back rule's reason (one word for "keep things as they are") is better served by a literal label. |
| 9 | Dialog initial focus: the title (`confirm-dialog.md`) or the least destructive button (`settings-textarea.md`, `primary-actions.md`) | **"Stay on this page"** | APG: for steps that are hard to undo, focus "the least destructive action" [Standard]. Here the safe button is also the expected choice, so Enter keeps the work. Nothing is committed by a stray Enter. |
| 10 | Dialog buttons on a laptop: label-width in a row (`primary-actions.md` layout) or stacked full width on every size (`confirm-dialog.md`) | **Stacked, full width, inside the dialog**; the page's Save follows `primary-actions.md` | `confirm-dialog.md` owns dialogs; managers using both devices find the buttons in the same place. |
| 11 | Leave dialog body: "Your changes to the guide prompts have not been saved." (`settings-textarea.md`) | **"Your changes to the guide prompts are not saved."** (M) | microcopy voice rules: present tense, plain verbs. Matches microcopy's canonical "…your note is not saved yet". |
| 12 | Sign out: "no 'Are you sure?' dialog: drafts are already on the server" (`app-shell-nav.md`) or ask the page's guard first (`settings-textarea.md`) | **Ask first, on this page** | 4.10 says "Leaving with unsaved changes shows a warning". The shell's reason holds for notes (autosaved) but not here: guide prompts have no autosave, so Sign out would lose the text. |
| 13 | Count basis: GOV.UK counts the raw value; `settings-textarea.md` counts the trimmed value | **Trimmed** | The count must agree with the error, and Save checks the trimmed text. Trailing blank lines from a Word paste then do not count. |
| 14 | Token names: `--colour-input-border`, `--colour-input-bg` (`settings-textarea.md`), `--focus-ring`, `--action-*` (`primary-actions.md`) | **`foundations.md` names** via its alias table (`--colour-border-control`, `--colour-surface`, `--colour-focus`, `--colour-action`…) | One token set, so the Vitest contrast test protects every value. |
| 15 | Breakpoint unit: `40em` (`primary-actions.md`) or `40rem` (`foundations.md`) | **`40rem`** | Identical inside media queries; `foundations.md`'s CI check greps for `40rem`. |
| 16 | Page structure: `settings-textarea.md`'s sample puts the h1 and hint inside the data-dependent form, but its own States table says they render at once | **h1, help text and limit hint render at once, outside the form;** the form mounts with the data | Route focus needs a fixed `<h1>` at once (`empty-loading-error.md` rule 1). |

---

## Tensions with decisions

**D12 / D34 with A6: up to 1,000 characters of text that vanishes on the first keystroke.** The HTML spec describes
a placeholder as "a short hint (a word or short phrase)" [Standard]
https://html.spec.whatwg.org/multipage/form-elements.html#attr-textarea-placeholder, and NN/g reports placeholders
strain short-term memory and can be "mistaken for prefilled data" [Research]
https://www.nngroup.com/articles/form-design-placeholders/. This screen is where that length is set. Against that, the
spec's own textarea example uses a multi-line placeholder, and the design keeps a visible label and 4.5:1 contrast.
This is already recorded in `guided-notes-textarea.md`, `settings-textarea.md` and `microcopy.md`; it is noted here
once. **No change is recommended.** The screen builds the decision as specified. The following content advice stays
off-screen, for whoever enters the prompts (`microcopy.md` 4.10) [Opinion]:

- One short question per line, everyday words, about 5 lines at most so they fit an empty phone box. The design's
  sketch is the model: "Mood and wellbeing today?", "What did you do together?", "Anything to follow up?".
- Nothing that must stay visible while writing; it disappears (D34).
- No names or details of a real participant: every worker sees the prompts on every note.
- To check the result, open any participant's note form from Today and look at the box **without tapping
  anything**. Nothing is created until the first change (3.3, A8), and opening a note is not audited (A28). If a
  draft is started by mistake, discard it (A9).

---

## Open questions

- **Strings to sign off (all P or M):** "You can enter up to 1,000 characters"; "Guide prompts must be 1,000 characters
  or less"; "Loading guide prompts…"; "The guide prompts did not load: … Try again." and the "still" form; "Not
  saved: no connection. Try again."; "Not saved: something went wrong. Try again."; "Saving…"; "Saved 4:12 pm"; the
  conflict message; "Leave without saving?", "Your changes to the guide prompts are not saved.", "Stay on this page",
  "Leave without saving".
  **Partly answered 9 October 2026 (D67):** the (P) strings are approved as written; any of them can still be changed
  later in the copy module, and a (P) string added after that date still needs the owner's approval. D67 covers (P)
  only, so the (M) string "You can enter up to 1,000 characters" is still open.
- **API at M1:** should the PUT `200` carry the new `ETag` header? (Otherwise the client refetches after every
  save.) Should saving unchanged text still write a `guide_prompts.updated` audit event? Save is deliberately never
  unavailable.
- **The conflict message** lets the manager overwrite another manager's version without seeing it on this page. The
  design gives no way to show it, and none is added. The other version can be seen by opening any note form in
  another tab. Is that acceptable to the owner?
- **Unverified, to test by hand at M1/M6:** `useBlocker` with the Android back gesture and iOS swipe-back
  (react-router issue #11589 reports the URL changing first; https://github.com/remix-run/react-router/issues/11589);
  whether `beforeunload` shows anything on Android Chrome and iOS Safari; whether the `Date` header survives Azure's
  front end; how browsers draw tabs and repeated spaces from Word-pasted bullets inside a placeholder; whether an
  empty Guided notes box with `field-sizing: content` grows to fit a long placeholder (MDN describes this for
  `<input>` only), which decides how many prompt lines a worker sees on a phone without scrolling the box.

---

## Sources

Component files: [settings-textarea](../components/settings-textarea.md) · [primary-actions](../components/primary-actions.md) ·
[status-messages](../components/status-messages.md) · [foundations](../components/foundations.md) ·
[microcopy](../components/microcopy.md) · [app-shell-nav](../components/app-shell-nav.md) ·
[confirm-dialog](../components/confirm-dialog.md) · [empty-loading-error](../components/empty-loading-error.md) ·
[form-validation](../components/form-validation.md) · [session-timeout](../components/session-timeout.md) ·
[guided-notes-textarea](../components/guided-notes-textarea.md). Design: design.md 3.3, 4.0, 4.10, 5.3, 6.1, 6.6, 6.7,
6.9, 13 (A6, A8, A9, A24, A28, A32, A33), 14 (M1, M6); decisions D12, D22, D31, D34, D37, D42.

- [Standard] WHATWG HTML, textarea placeholder: https://html.spec.whatwg.org/multipage/form-elements.html#attr-textarea-placeholder
- [Standard] WCAG 2.2 Understanding 4.1.3: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- [Standard] WCAG 2.2 Understanding 3.3.4: https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- [Standard] WCAG 2.2 Understanding 2.4.11: https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- [Standard] WCAG technique ARIA22: https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- [Standard] WAI-ARIA APG dialog (modal) and alert dialog: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ · https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- [Standard] MDN `beforeunload`: https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- [Standard] MDN `field-sizing` (Baseline 2026, newly available since June 2026; checked 1 October 2026): https://developer.mozilla.org/en-US/docs/Web/CSS/field-sizing
- [Standard] Chrome, Page Lifecycle (add `beforeunload` only while there are unsaved changes): https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- [Standard] Chrome, viewport resize and the on-screen keyboard: https://developer.chrome.com/blog/viewport-resize-behavior
- [Standard: library docs] React Router `useBlocker`: https://reactrouter.com/api/hooks/useBlocker · issue #11589: https://github.com/remix-run/react-router/issues/11589
- [Standard: library docs] React `<form>` (uncontrolled fields reset after an action): https://react.dev/reference/react-dom/components/form · `autoFocus` in `<dialog>`: https://github.com/facebook/react/issues/23301
- [Convention] GOV.UK labels as headings: https://design-system.service.gov.uk/get-started/labels-legends-headings/
- [Convention + Research] GOV.UK character count (tested 2017, 17 users): https://design-system.service.gov.uk/components/character-count/
- [Convention] GOV.UK validation pattern: https://design-system.service.gov.uk/patterns/validation/
- [Convention] Adam Silver, do not use `maxlength`: https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/
- [Convention] AWS Cloudscape, unsaved changes: https://cloudscape.design/patterns/general/unsaved-changes/
- [Convention] React Aria Button `isPending`: https://react-aria.adobe.com/Button
- [Convention] 16 px inputs stop iOS zoom: https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/
- [Research] NN/g, placeholders in form fields: https://www.nngroup.com/articles/form-design-placeholders/
- [Research] NN/g, indicators, validations and notifications (2024): https://www.nngroup.com/articles/indicators-validations-notifications/
- [Research] NN/g, progress indicators (nothing under about 1 s): https://www.nngroup.com/articles/progress-indicators/
- [Opinion, from AT testing] Scott O'Hara, live regions: https://www.scottohara.me/blog/2022/02/05/are-we-live.html
