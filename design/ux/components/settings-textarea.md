# Guide prompts editor

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The shell's Sign out awaits `confirmLeave()` before any request, disarms the unload guard before reloading and re-arms it if logout fails (app-shell.md).

Component key: `settings-textarea`. The one multi-line field on **Manage > Guide prompts** (design.md 4.10): "one
multi-line field, 'Guide prompts', up to 1,000 characters, and Save", with the help text "Prompts are a guide only.
They disappear as soon as someone starts typing and are not saved into notes." Whatever is saved becomes the grey
placeholder in every empty Guided notes box straight away, including open drafts (D12, D34). An empty field means no
placeholder. Leaving with unsaved changes shows a warning.

This file covers how to build exactly that. It adds no preview pane, no Cancel or Reset button, no "restore default"
and no autosave.

**Copy conventions.** Text in "quotes" with no mark is copied word for word from design.md. Text marked
**(proposed)** is not in design.md. It fills a gap the design leaves, such as an error or the leave dialog, and the
owner should confirm it. Wherever a sibling component doc has already proposed a string for this screen
(`primary-actions.md`, `status-messages.md`, `form-validation.md`), this file reuses it word for word.

Evidence grades: **[Research]** usability testing or studies · **[Standard]** WCAG 2.2, WAI-ARIA APG, HTML spec,
browser-vendor platform documentation · **[Convention]** established design systems · **[Opinion]** reasoned
judgement.

---

## Where it's used

| Screen (design.md) | Who | What differs |
|---|---|---|
| **4.10 Guide prompts** (Manage > Guide prompts) | Managers only. A worker who opens the URL gets the shell's not-found page (`app-shell-nav.md`). | This is the only screen that uses the component. Two situations behave differently: |
| · First set-up (go-live, §14 M6: "its prompts become the guide prompts") | One manager, usually on a laptop | Usually a **paste** from the organisation's Word template. That is the most likely moment to go over 1,000 characters, and to bring in Word bullets, tabs and blank lines. The go-live checklist requires "entered by one manager and checked by another". |
| · Later edits | Any manager, on a laptop or a phone | Small rewordings. The change reaches every worker's empty box as soon as it is saved, so a half-edited version must never go live by accident. |
| *(Related, not this component)* 4.3 Note form, "3. Guided notes" | Workers and managers | Where the saved text shows as the grey placeholder. That is owned by `guided-notes-textarea.md`. This editor copies its box metrics so that what the manager types wraps the same way. |

There is no read-only or worker variant. The editor is either fully editable, or not rendered at all.

---

## Best practice

### Seeing how the prompts will look (the preview question)

- **A textarea placeholder keeps its line breaks.** "All U+000D CARRIAGE RETURN U+000A LINE FEED character pairs
  (CRLF) in the hint, as well as all other U+000D CARRIAGE RETURN (CR) and U+000A LINE FEED (LF) characters in the
  hint, must be treated as line breaks when rendering the hint." The spec's own textarea example uses a multi-line
  placeholder "to suggest the basic form to the user, without providing an explicit template". So a prompt typed on
  its own line in the editor shows on its own line in the note form. [Standard]
  https://html.spec.whatwg.org/multipage/form-elements.html#attr-textarea-placeholder
- **The spec still intends a placeholder to be "a short hint (a word or short phrase)",** shown "when the element's
  value is the empty string and the control is not focused". Placeholders that disappear add memory load, and people
  can mistake them for text that is already filled in. [Standard] same URL · [Research] NN/g, *Placeholders in Form
  Fields Are Harmful* (2014, reviewed 2018) https://www.nngroup.com/articles/form-design-placeholders/. D34 settles
  the design. These sources apply here only as **content** advice to the manager: short prompts, one per line.
- **None of the sources consulted describes a separate "preview" for placeholder text** (GOV.UK, NHS, AgDS, NN/g,
  Cloudscape). Whether any design system does is unverified. The cheapest faithful preview is to give the editor the
  same font, size, line height, padding and column width as the Guided notes box. A second, real check already
  exists in the design: opening any participant's note form creates nothing until the first change (3.3, A8), and
  opening a note is not audited (A28). [Opinion]
- **Do not show the editor's own text in placeholder grey.** Grey text in an editable field reads as a placeholder or
  as disabled. NN/g's failure mode (placeholder mistaken for filled-in text) also runs the other way. MDN warns that
  high-contrast placeholders can be confused with entered text. [Research + Convention]
  https://developer.mozilla.org/en-US/docs/Web/CSS/::placeholder

### Labels, hint and limit

- **When a page asks one question, make the label the page heading.** "When you're asking just one question on a
  page, you can make the question the page's heading", because a separate heading and label are read twice by screen
  readers. [Convention] GOV.UK https://design-system.service.gov.uk/get-started/labels-legends-headings/
- **State the limit, and only add a character count when a limit is really needed.** GOV.UK: "Only use the character
  count component when there is a good reason for limiting the number of characters users can enter." It can show the
  count only once a threshold is passed (`data-threshold`). It "does not restrict the user from entering
  information. The user can enter more than the character limit, but are told they've entered too many characters."
  It was tested in 2017 with 17 users, including people with low digital skills and disabilities. [Research +
  Convention] https://design-system.service.gov.uk/components/character-count/
- **Error wording:** "[whatever it is] must be [number] characters or less". [Convention] (same GOV.UK page)
- **Never `maxlength` on free text.** It silently cuts off a paste. [Convention] GOV.UK character count, Adam Silver
  https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/
- **Validate when the user presses Save, not on blur.** Once an error is showing, clear it the moment the input is
  fixed. [Convention] GOV.UK https://design-system.service.gov.uk/patterns/validation/ · [Research] Baymard
  (2024) https://baymard.com/blog/inline-form-validation

### Save confirmation

- **People need to know whether an action worked.** "Whenever users interact with a system, they need to know whether
  the interaction was successful." [Research, heuristic] NN/g, Harley, 2018
  https://www.nngroup.com/articles/visibility-system-status/
- **Put feedback next to what it is about. A confirmation that needs no action should be passive.** [Research,
  practitioner synthesis] NN/g, Flaherty, *Indicators, Validations, and Notifications* (17 January 2024)
  https://www.nngroup.com/articles/indicators-validations-notifications/
- **A result shown without a change of context must be announced without moving focus** (SC 4.1.3, AA). The
  Understanding doc's own example is a save: "Saved in 'Wedding' album", "which is also read by a screen reader".
  [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **GOV.UK's green success banner** takes `role="alert"` and focus "on page load". It is designed for the page that
  loads *after* an action. [Convention] https://design-system.service.gov.uk/components/notification-banner/ The user
  stays on this page, so an inline status fits better (as `status-messages.md` already decided).
- **Never report success before the server confirms it.** [Opinion, ui-ux-design invariant I5]
- **Saving stored data must be reversible, checked or confirmed** (SC 3.3.4, AA, which covers pages that "modify …
  user-controllable data in data storage systems"). [Standard]
  https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

### Unsaved-changes warning: in-app navigation

- **React Router's `useBlocker`** "allow[s] the application to block navigations within the SPA and present the user
  a confirmation dialog… Mostly used to avoid using half-filled form data. This does not handle hard-reloads or
  cross-origin navigations." It works in Framework and Data mode only, **not** Declarative mode (so it needs
  `createBrowserRouter`, which `app-shell-nav.md` already uses). States are `unblocked`, `blocked` and `proceeding`,
  with `proceed()` and `reset()`. The `shouldBlock` function receives `currentLocation`, `nextLocation` and
  `historyAction`. [Standard: library docs] https://reactrouter.com/api/hooks/useBlocker ·
  https://reactrouter.com/how-to/navigation-blocking
- **Browser Back inside the app (a POP navigation) is less reliable.** A 2024 report (v6.23.1) says the URL changes
  before the dialog shows. It was closed as "not planned". Whether current v7 behaves the same is **unverified**.
  https://github.com/remix-run/react-router/issues/11589
- **Only warn when something would actually be lost.** "Don't launch a confirmation modal when there is no risk of
  data loss. For example, when no change has been made on the page." Never offer "don't show this again". Use the
  in-page modal for in-app navigation and the browser's own dialog for "closing tabs, reloading, quitting the browser,
  using browser history, or modifying the URL". Cloudscape titles its modal "Leave page" and labels the buttons
  "Cancel" / "Leave". [Convention] AWS Cloudscape https://cloudscape.design/patterns/general/unsaved-changes/
- **In a dialog that guards against loss, put initial focus on the least destructive action.** Escape closes the
  dialog, and focus goes back to whatever opened it. [Standard] WAI-ARIA APG
  https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ · alert dialog
  https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- **Buttons say what will happen, not "OK" or "Yes". Warnings lose force with repetition.** [Research] NN/g
  https://www.nngroup.com/articles/confirmation-dialog/

### Unsaved-changes warning: reload, close, typed URL (`beforeunload`)

- **Trigger the dialog with `preventDefault()`, and set `returnValue` for older browsers.** Browsers "only show a
  generic browser-specified string… This cannot be controlled by the webpage code", and they "require sticky
  activation", so the page must have been interacted with. [Standard] MDN
  https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- **It is unreliable on phones.** "It is not reliably fired, especially on mobile platforms". MDN's example is a
  user who switches app and then closes the browser from the app manager: no event fires at all. [Standard] (MDN,
  same URL)
- **Add the listener only while there are unsaved changes, and remove it once they are saved.** Chrome: "Only add
  `beforeunload` listeners when a user has unsaved changes and then remove them immediately after they are saved."
  Firefox will not put a page with a `beforeunload` listener into the back/forward cache. [Standard: platform
  documentation] https://developer.chrome.com/docs/web-platform/page-lifecycle-api ·
  https://web.dev/articles/bfcache · MDN (above)

---

## Recommendation for Grow2Notes

### Anatomy (phone, top to bottom)

```
Guide prompts                                 <h1><label for="guidePrompts-text">  (one question, one heading)
Prompts are a guide only. They disappear as   hint paragraph 1 (design.md 4.10)
soon as someone starts typing and are not
saved into notes.
Up to 1,000 characters                        hint paragraph 2 (proposed; design says "up to 1,000 characters")
[Error: Guide prompts must be 1,000 …]        field error, only after a failed Save
+----------------------------------------+
| Mood and wellbeing today?              |    <textarea>: same metrics as the Guided notes box,
| What did you do together?              |    text in the normal text colour, grows with content,
| Anything to follow up?                 |    never shorter than about 6 lines
+----------------------------------------+
You have 80 characters remaining              count, only from 900 characters (aria-hidden; polite twin below)
(visually hidden polite live region)          always in the DOM
[ request error: role="alert", always in the DOM, empty until needed ]
[ Save ]   Saved 4:12 pm                      primary button + <p role="status"> (status-messages.md SaveStatus)
```

- **Heading and label are one element:** `<h1 class="pageHeading"><label htmlFor="guidePrompts-text">Guide
  prompts</label></h1>`. The page title is "Grow2Notes – Guide prompts" (`app-shell-nav.md`). Tapping the heading
  focuses the field. [Convention: GOV.UK]
- **Hint:** both paragraphs sit between the label and the field, and the field's `aria-describedby` points at both.
  Design says the help text is ordinary page text, not a banner (`status-messages.md`).
- **The field copies the Guided notes box exactly:** `font: inherit`, `font-size: max(16px, 1rem)`,
  `line-height: 1.5`, padding `0.75rem`, 2 px border at ≥ 3:1, 4 px radius, `min-block-size: 11em`, no max height,
  `resize: none`, auto-grow. Its laptop width is capped at the note form's column width (about 40rem), so a line
  wraps where it will wrap in a worker's box on a laptop. **Put both components on one shared CSS module**
  (`TextBox.module.css`) so the metrics cannot drift apart. [Opinion]
- **The only visual difference from the note form is colour:** here the prompts are *content*, in the normal text
  colour (≥ 4.5:1, e.g. `#0b0c0c`). They are not shown in placeholder grey. The editor has **no placeholder of its
  own**.
- **Save** is the app's primary button (`primary-actions.md`), left-aligned under the field, full width on a phone.
  The `SaveStatus` line (`status-messages.md`) sits to its right on a laptop and wraps beneath it on a phone.

### Behaviour

1. **Load, then show.** `GET /api/admin/guide-prompts` returns `{text}` and the `ETag` (§6.6, §6.7). Do **not**
   render an editable field until it arrives. An empty field during loading could be saved by mistake as "no
   prompts", which would wipe the placeholder for every worker at once.
2. **Read once, never re-sync.** The field is an uncontrolled `<textarea defaultValue>`, mounted once with the loaded
   text, the same model as `guided-notes-textarea.md`. Its base `ETag` and base text sit in refs. Later refetches of
   the query (for example the same person signing in again in place) **never** change the field or the base `ETag`.
   A silent refetch must not turn a stale edit into an overwrite. [Opinion]
3. **Dirty** means `field.value.trim() !== savedText`. It is checked on every `input`, and read straight from the DOM
   when a navigation or Save is attempted. That second check also catches speech tools that change the value without
   firing events (the issue `guided-notes-textarea.md` notes; unverified for current Dragon).
4. **Typing:** on each `input`, clear the "Saved 4:12 pm" status, update the count (shown from 900 characters), and
   re-check an error that is already showing (clear it when fixed). Nothing is sent while typing. **No autosave:**
   every save goes live at once in workers' empty boxes, so a half-edited version must never be published by
   accident. The design's explicit Save is the right model for this field. [Opinion]
5. **Save** (`<form noValidate onSubmit>`, `preventDefault()`):
   - Read `text = field.value.trim()`. Leading and trailing blank lines and spaces would only push the prompts down
     or leave a blank placeholder, and whitespace-only text should mean "no prompts". Inner line breaks are kept
     exactly. The field itself is not rewritten. [Opinion]
   - If `text.length > 1000`: show the field error, set `aria-invalid`, move focus to the field, and send nothing.
     This is a one-field form, so there is inline error only and no summary (`form-validation.md` rule 6).
   - Otherwise send `PUT /api/admin/guide-prompts {text}` with `If-Match: <base ETag>`. The button goes busy
     ("Saving…" after 400 ms, `aria-disabled`, from `primary-actions.md`). The field stays editable, never `disabled`.
   - **Save is never unavailable**, even with no changes. Sending the same text is harmless (`primary-actions.md`).
   - **No automatic retry** (`retry: false`). A retried `PUT` whose first response was lost would come back `412`,
     because the `ETag` has moved on, and show a false conflict. The manager presses Save again. [Opinion]
6. **On `200`:** base text = `text`, base `ETag` = the response `ETag`. If the response carries no `ETag` header,
   refetch to get one. Recompute dirty: it stays dirty if the manager typed during the save. Show "Saved 4:12 pm" in
   the status line. Focus stays on Save. Take the time from the response's `Date` header, which is the server
   clock and readable on a same-origin response, and format it for Australia/Melbourne with the shared formatter.
   Fall back to the device clock only if the header is missing. The PUT body returns no `savedAtUtc`. [Opinion]
7. **On `422 validation.failed`** (`errors.text`): show the same field error, focus the field.
8. **On `412 precondition.failed`** (another manager saved first): refetch, then set the base text and `ETag` from the
   fresh copy. **Keep the manager's typed text in the field.** Show the conflict message in the alert above Save.
   Focus stays on Save. The manager's next Save replaces the other version knowingly. Both versions are kept in
   the audit log, which records old and new values for every change (§6.6). design.md 6.7 says "the client reloads".
   That is read here as "reload the record and `ETag`", not "replace what was typed". This matches
   `form-validation.md` ("Re-fetch the record for a fresh ETag, but keep the typed values on screen") and invariant
   I9. [Opinion]
9. **On `401`:** no message here. The sign-in-in-place flow takes over and keeps this route mounted but hidden, so
   the typed text survives (`session-timeout.md`). **Save is not replayed** after sign-in ("Autosave is the only
   thing retried automatically"). The field is still dirty, so the warnings still protect it.
10. **On a network error or `5xx`:** show the error in the alert above Save. The text and dirty state stay as they
    are.
11. **Leaving with unsaved changes** (design 4.10). There are three routes out, and each gets one guard:

    | How the manager leaves | Guard | What they see |
    |---|---|---|
    | A link or button inside the app (Today, Flagged, Report, Manage, back link), or browser Back/Forward within the app | `useBlocker`, only when dirty and the pathname changes | The app's dialog (below) |
    | Reload, closing the tab or window, typing a URL, Back out of the app | `beforeunload` listener, **added only while dirty**, removed on save and on unmount | The browser's own generic dialog. Its wording can't be changed. On phones it may not appear at all (MDN) |
    | **Sign out** in the account menu | The shell's Sign out asks the page's guard first, **before** it posts `/api/auth/logout` | The app's dialog. "Leave without saving" then continues the sign-out. "Stay on this page" cancels it |

    Without the third row, Sign out would post the logout *and then* hit the browser dialog during
    `location.replace('/')`. That leaves a signed-out page behind a "Stay" choice. Any deliberate full reload, such
    as Sign out after confirming, or the session flow's "different person signed in" reload (`session-timeout.md`),
    first **disarms** the `beforeunload` guard. Otherwise the next person is asked about the previous person's
    unsaved text. [Opinion]
12. **The leave dialog** uses the shared `ConfirmDialog` shell (`confirm-dialog.md`): native `<dialog>` +
    `showModal()`, `role="alertdialog"`, title `<h2>`, one body sentence, two stacked buttons. **"Stay on this page"**
    is primary, first, and **focused on open**. It is the least destructive choice (APG). **"Leave without saving"**
    is secondary and **not red**: a few lines of prompt text are easy to retype, and red is kept for the
    irreversible actions (`primary-actions.md`). Escape, the Android back gesture and `cancel` all mean Stay
    (`blocker.reset()`). Tapping the backdrop does nothing. Focus returns to whatever started the navigation. Never
    use `window.confirm()` or React Router's `unstable_usePrompt` (which wraps it): they block the main thread and
    can't follow the app's dialog copy or focus rules. [Opinion]
13. **Keyboard:** Tab in and out. Enter inserts a new line (a textarea never submits on Enter). No shortcuts.
    No autofocus on page load: the manager should read the help text first, and on a phone autofocus would open the
    keyboard over it. [Opinion]

### States

| State | What shows | Semantics and behaviour |
|---|---|---|
| **Loading** | Heading and hint render at once. Where the field will go, nothing for the first 400 ms, then "Loading…" (proposed). No empty, editable box. | A `role="status"` region that exists from the first render receives "Loading…" |
| **Load failed** | "Couldn't load the guide prompts. Check your connection and try again." (proposed) and a secondary **Try again** button, which refetches | Same status region. Try again sits outside it, so it isn't read twice (`participant-list-rows.md` pattern) |
| **Default, prompts saved** | Field holds the saved text in the normal text colour, at least as tall as the text | Native textarea, named by the `<h1><label>`, described by both hint paragraphs |
| **Empty, no prompts saved** | An empty, roughly 6-line box. **No placeholder** in the editor. Nothing else changes: the hint already explains what prompts are | Same. Saving an empty field is valid: it means "the box shows no placeholder" (4.10) |
| **Hover** (pointer devices) | No change. Text cursor only. Hover never carries information | — |
| **Focus** | Border unchanged, plus the app focus outline: `outline: 3px solid var(--focus-ring); outline-offset: 2px`. No transition | `:focus-visible`. `scroll-margin-block-start` if the shell ever gets a sticky bar (it has none today) |
| **Active / editing (dirty)** | Text as typed. "Saved 4:12 pm" cleared on the first keystroke. **No "unsaved" badge**: the design specifies none, and the warning covers the risk | Leave guards armed (`useBlocker` + `beforeunload`) |
| **Near / over the limit** | From 900 characters: "You have 100 characters remaining" in hint style under the box. Over 1,000: "You have 12 characters too many" in bold error colour, with the box border in the error colour | Visible count `aria-hidden`. A polite live region, always in the DOM, repeats it 1 s after typing stops. Not `aria-invalid` until Save fails |
| **Error (field)** | "Error:" (visually hidden) + "Guide prompts must be 1,000 characters or less" in bold error colour between the hint and the box. Thicker error border. 4 px error bar on the group's inline-start edge (`form-validation.md` anatomy) | `aria-invalid="true"`. The error id is added to `aria-describedby`. Focus moves to the field. Clears silently once the length is ≤ 1,000 |
| **Error (request)** | The message in the alert slot directly above Save. The button returns to default | Container with `role="alert"`, **already in the DOM** and empty until needed. Focus stays on Save |
| **Conflict (`412`)** | Conflict message in the same alert slot. Typed text unchanged | As request error |
| **Busy (saving)** | Save shows "Saving…" after 400 ms. The field stays editable | Save `aria-disabled="true"`, so a second press is ignored. Never `disabled` |
| **Saved** | "Saved 4:12 pm" beside Save, until the next keystroke or until the manager leaves | `<p role="status">` (polite), always rendered. Focus stays on Save |
| **Leaving while dirty** | In-app: the leave dialog. Browser-level: the browser's dialog | See Behaviour 11–12 |
| **Signed out mid-edit** | Sign-in shows in place. The page is hidden but still mounted | Text survives in memory only (D22). Save is not replayed |
| **Disabled** | **Never.** A disabled field can't be focused or copied, and its text is exempt from contrast | — |
| **Read-only** | **Never.** Managers can always edit. Workers never see the page | — |

### Phone vs laptop

| | Phone | Laptop |
|---|---|---|
| Field width | Full content column inside the 16 px gutters. That is the same width a worker's Guided notes box gets on the same phone | Capped at the note form's column width (about 40rem), left-aligned, so line wrapping matches the worker's laptop view. The rest of the width stays empty on purpose |
| Text size | `max(16px, 1rem)`, so iOS doesn't zoom on focus and larger user settings are respected | Same |
| Height | Grows with the text. The page scrolls; the box never scrolls inside itself | Same |
| Save + status | Save full width. "Saved 4:12 pm" on its own line below it | Save `width: auto`, status inline to its right |
| Typical input | Small edits. Keyboard and dictation | Paste from the Word template at go-live. Keyboard |
| Leave warnings | In-app dialog works. `beforeunload` may not fire on tab or app close (MDN). That is accepted: the text is short and easy to retype | Both work |

### Exact copy

| Where | Text | Source |
|---|---|---|
| Page heading = label | "Guide prompts" | design.md 4.10 |
| Hint 1 | "Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes." | design.md 4.10 |
| Hint 2 | "Up to 1,000 characters" | **(proposed)**, from design.md 4.10 "up to 1,000 characters". Same form as "Up to 200 characters" in `form-validation.md` |
| Save button / busy label | "Save" / "Saving…" | design.md / `primary-actions.md` (proposed) |
| Saved status | "Saved 4:12 pm" | `status-messages.md` (proposed). Same wording as the note form's "Saved 9:42 am" |
| Count, 900 to 999 | "You have 100 characters remaining" (singular: "1 character") | GOV.UK character count wording, as in `guided-notes-textarea.md` |
| Count, exactly 1,000 | "You have 0 characters remaining" | GOV.UK |
| Count, over 1,000 | "You have 12 characters too many" (singular: "1 character") | GOV.UK |
| Field error (Save, or `422`) | "Guide prompts must be 1,000 characters or less" | **(proposed)**, GOV.UK template; "or less" as in `form-validation.md` |
| Hidden error prefix | "Error: " | GOV.UK |
| Network error | "Not saved: no connection. Try again." | `primary-actions.md` (proposed) |
| Server error | "Not saved: something went wrong. Try again." | `primary-actions.md` (proposed) |
| Conflict (`412`) | "Another manager saved the guide prompts while you were editing. Your text is still here. Select Save to replace their version with yours." | **(proposed)** [Opinion]. "Select" is device-neutral |
| Loading | "Loading…" | **(proposed)** |
| Load failed | "Couldn't load the guide prompts. Check your connection and try again." + "Try again" | **(proposed)**, same pattern as `participant-list-rows.md` |
| Leave dialog title | "Leave without saving?" | `primary-actions.md` (proposed) |
| Leave dialog body | "Your changes to the guide prompts have not been saved." | **(proposed)** |
| Leave dialog buttons | "Stay on this page" (primary, focused) / "Leave without saving" (secondary) | `primary-actions.md` (proposed) |
| Browser-level warning | The browser's own text. It cannot be set (MDN) | — |

Numbers use `Intl.NumberFormat('en-AU')` ("1,000"). The time uses the shared Melbourne formatter ("4:12 pm").

### Accessibility

**Semantics.** Native `<form>`, `<h1><label for>`, `<textarea>` and `<button>`. ARIA is used only where native HTML
has no equivalent:
- `aria-describedby` on the field: `"guidePrompts-hint guidePrompts-limit"`, plus `guidePrompts-error` while an
  error shows.
- `aria-invalid="true"` only after a failed Save.
- `role="status"` for loading and Saved, `role="alert"` for request errors, one visually hidden `aria-live="polite"`
  for the count, and `role="alertdialog"` on the leave dialog. Every one of these containers is rendered from the
  first paint and only ever filled in.

No `aria-required`: empty is valid here. **React Aria is not needed.** Native elements and `<dialog>` cover
everything.

**Keyboard.** Fully native. Tab order: field → (Try again, when shown) → Save. In the dialog: Tab moves between the
two buttons, Enter or Space activates, Escape = Stay on this page.

**What a screen reader announces** (wording varies by reader):

| Moment | Heard | Mechanism |
|---|---|---|
| Page opens | "Guide prompts, heading level 1" (route focus on `h1`, from `app-shell-nav.md`) | Shell focus rule |
| Focus on the field | "Guide prompts, edit text, multi-line", the current text, then the two hint sentences | Label, content, `aria-describedby` |
| Typing below 900 | Nothing extra | — |
| Pause past 900 / over 1,000 | "You have 100 characters remaining" / "You have 12 characters too many" | Polite live region, 1 s after the last input |
| Save pressed, slow | "Saving…" (after 400 ms) | App status region (`primary-actions.md`) |
| Saved | "Saved 4:12 pm" | `role="status"` beside Save |
| Too long on Save | Focus moves to the field: "Guide prompts, invalid entry, …, Error: Guide prompts must be 1,000 characters or less" | Focus + `aria-invalid` + `aria-describedby` |
| Network, server or conflict error | The message, read once. Focus stays on Save | `role="alert"` |
| In-app navigation while dirty | "Leave without saving?, alert dialog. Your changes to the guide prompts have not been saved." Focus on "Stay on this page, button" | `alertdialog`, `aria-labelledby`, `aria-describedby` |

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships (label, hints and error linked) · 1.4.1 Use of Color (errors
and limits in words, plus the error bar's shape) · 1.4.3 Contrast (Minimum) (all text ≥ 4.5:1; the editor never uses
placeholder grey) · 1.4.4 Resize Text and 1.4.10 Reflow (rem sizes, no fixed height, wraps at 320 px) · 1.4.11
Non-text Contrast (border, focus ring ≥ 3:1) · 1.4.12 Text Spacing (grows, never clips) · 2.1.1 Keyboard · 2.1.2 No
Keyboard Trap (native modal) · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not
Obscured (Minimum) (nothing sticky) · 2.5.3 Label in Name ("Guide prompts", "Save" are the names) · 2.5.8 Target Size
(Minimum) (44 px button; the box far larger) · 3.2.2 On Input (nothing is sent while typing) · 3.3.1 Error
Identification · 3.3.2 Labels or Instructions (help text and limit) · 3.3.3 Error Suggestion (the count says how
much to cut) · 3.3.4 Error Prevention (Data) (checked before saving; reversible by editing again) · 4.1.2 Name, Role,
Value · 4.1.3 Status Messages (Saved, count, errors without moving focus).

**Forced colours.** Real borders on the box, the error bar and the button keep their shape. Use
`.box:focus-visible { outline-color: Highlight; }`. `aria-disabled` Save uses `GrayText` (`primary-actions.md`).

### Implementation notes (React 19 + React Router data router + TanStack Query 5 + CSS Modules)

- **No form library and no `<form action>`.** React 19 resets uncontrolled fields after a form action succeeds, which
  would empty this box. Use `onSubmit` + `preventDefault()` (react.dev `<form>`).
- **One `limits.ts`** holds `GUIDE_PROMPTS_MAX = 1000` next to the other A6 limits. It is checked against the API's
  OpenAPI `maxLength` in a unit test (`form-validation.md`). `value.length` counts UTF-16 code units, which matches
  .NET `string.Length` and `nvarchar(1000)`.
- **Share the count helper with Guided notes:** `countMessage(len, limit)`, plus a threshold of 90%.
- **Query:** `useQuery({ queryKey: ['admin', 'guidePrompts'], refetchOnWindowFocus: false })`. The editor is mounted
  only once the query has data, and it reads that data once.
- **`useBlocker` needs the data router** (`createBrowserRouter`, already chosen in `app-shell-nav.md`). Use the
  function form and read the DOM, so the decision is made at navigation time. Block only when the pathname changes.
- **`beforeunload`:** add it in an effect keyed on `dirty`, and remove it in that effect's cleanup. Never attach it
  permanently (Chrome, MDN). Call `preventDefault()` and set `returnValue = true`.
- **Leave guard for Sign out:** one tiny module (memory only) lets the page register "ask before leaving", and lets
  the shell ask before Sign out and disarm before any deliberate full reload. This file defines the contract.
  `app-shell-nav.md` and `session-timeout.md` should call it.
- **Testing** (Playwright + axe at M1): dirty → click Today → dialog, focus on Stay, Escape stays. Dirty → reload →
  browser dialog (Chromium). Clean → no dialog anywhere. Save → "Saved 4:12 pm" announced and cleared on the next
  keystroke. Two managers → `412` keeps typed text. Paste 1,200 characters → count goes red, Save shows the error,
  nothing is sent. By hand: Android back gesture and iOS swipe-back inside the app (POP blocking is **unverified**,
  issue #11589), and VoiceOver/TalkBack announcements.

```ts
// leaveGuard.ts: in-memory only (D22). The page registers; the shell asks.
let ask: (() => Promise<boolean>) | null = null;
let unloadArmed = true;
export const setLeaveGuard = (fn: typeof ask) => { ask = fn; };
export const confirmLeave = () => (ask ? ask() : Promise.resolve(true)); // Sign out: await before logout
export const disarmUnloadGuard = () => { unloadArmed = false; };          // before location.replace(...)
export const rearmUnloadGuard = () => { unloadArmed = true; };            // the shell's logout request failed: text still on screen
export const unloadGuardArmed = () => unloadArmed;
// Shell order on Sign out (app-shell.md): confirmLeave() → flushNow() → disarmUnloadGuard() → POST logout →
// location.replace('/'); on a failed logout, rearmUnloadGuard(). The "different person signed in" reload also
// calls disarmUnloadGuard() first.
```

```tsx
// GuidePromptsForm.tsx: mounted only after GET /api/admin/guide-prompts has data.
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useBlocker } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { GUIDE_PROMPTS_MAX } from '../limits';
import { countMessage } from '../ui/countMessage';
import { Button } from '../ui/Button';
import { SaveStatus } from '../ui/SaveStatus';
import { LeaveDialog } from './LeaveDialog';           // ConfirmDialog shell, alertdialog
import { setLeaveGuard, unloadGuardArmed } from '../leaveGuard';
import { putGuidePrompts, getGuidePrompts, isStatus, isNetwork } from '../api';
import box from '../ui/TextBox.module.css';            // shared with Guided notes

const TOO_LONG = 'Guide prompts must be 1,000 characters or less';
const CONFLICT = 'Another manager saved the guide prompts while you were editing. ' +
  'Your text is still here. Select Save to replace their version with yours.';

export function GuidePromptsForm({ initial }: { initial: { text: string; etag: string } }) {
  const field = useRef<HTMLTextAreaElement>(null);
  const savedText = useRef(initial.text);   // base text: what the server holds, as far as we know
  const etag = useRef(initial.etag);        // If-Match: changes only on our save or a 412 refetch
  const [dirty, setDirty] = useState(false);
  const [len, setLen] = useState(initial.text.trim().length);
  const [fieldError, setFieldError] = useState(false);
  const [requestError, setRequestError] = useState('');
  const [savedAtUtc, setSavedAtUtc] = useState<string | null>(null);
  const isDirty = () => field.current!.value.trim() !== savedText.current;

  // In-app navigation (links, in-app Back/Forward)
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    currentLocation.pathname !== nextLocation.pathname && isDirty());

  // Reload / close / typed URL: only while dirty, removed when clean or unmounted
  useEffect(() => {
    if (!dirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (unloadGuardArmed()) { e.preventDefault(); e.returnValue = true; }
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [dirty]);

  // Sign out asks this page first (LeaveDialog resolves true = leave, false = stay)
  const askRef = useRef<() => Promise<boolean>>(null);
  useEffect(() => {
    setLeaveGuard(() => (isDirty() && askRef.current ? askRef.current() : Promise.resolve(true)));
    return () => setLeaveGuard(null);
  }, []);

  const save = useMutation({ mutationFn: (text: string) => putGuidePrompts(text, etag.current), retry: false });

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (save.isPending) return;
    const text = field.current!.value.trim();
    if (text.length > GUIDE_PROMPTS_MAX) { setFieldError(true); field.current!.focus(); return; }
    setRequestError('');
    try {
      const res = await save.mutateAsync(text);            // { etag?, serverDateUtc? } from headers
      etag.current = res.etag ?? (await getGuidePrompts()).etag;
      savedText.current = text;
      setDirty(isDirty());                                  // still dirty if typed during the save
      setSavedAtUtc(res.serverDateUtc ?? new Date().toISOString());
    } catch (err) {
      if (isStatus(err, 412)) {
        const fresh = await getGuidePrompts();              // new base; typed text untouched
        etag.current = fresh.etag; savedText.current = fresh.text;
        setDirty(isDirty()); setRequestError(CONFLICT);
      } else if (isStatus(err, 422)) { setFieldError(true); field.current!.focus(); }
      else if (isStatus(err, 401)) { /* sign-in in place takes over; Save is not replayed */ }
      else setRequestError(isNetwork(err) ? 'Not saved: no connection. Try again.'
                                          : 'Not saved: something went wrong. Try again.');
    }
  }

  const showCount = len >= 900;
  return (
    <form noValidate onSubmit={onSubmit} className={box.formColumn}>
      <h1 className="pageHeading"><label htmlFor="guidePrompts-text">Guide prompts</label></h1>
      <p id="guidePrompts-hint" className="hint">
        Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.
      </p>
      <p id="guidePrompts-limit" className="hint">Up to 1,000 characters</p>
      <div className={fieldError ? box.groupError : box.group}>
        {fieldError && (
          <p id="guidePrompts-error" className="errorMessage">
            <span className="visually-hidden">Error: </span>{TOO_LONG}
          </p>
        )}
        <div className={box.grow}>
          <textarea
            ref={field} id="guidePrompts-text" name="text" className={box.box}
            defaultValue={initial.text}
            aria-describedby={`guidePrompts-hint guidePrompts-limit${fieldError ? ' guidePrompts-error' : ''}`}
            aria-invalid={fieldError || undefined}
            spellCheck autoCorrect="on" autoCapitalize="sentences" autoComplete="off"
            onInput={(e) => {
              const n = e.currentTarget.value.trim().length;
              setLen(n); setDirty(isDirty()); setSavedAtUtc(null);
              if (fieldError && n <= GUIDE_PROMPTS_MAX) setFieldError(false);   // clear on fix
              // + copy the value into the .grow wrapper's data-value for the non-field-sizing fallback,
              //   exactly as GuidedNotes does (omitted here for brevity)
            }}
          />
        </div>
        <p aria-hidden="true" hidden={!showCount}
           className={len > GUIDE_PROMPTS_MAX ? box.countOver : box.count}>
          {showCount ? countMessage(len, GUIDE_PROMPTS_MAX) : ''}
        </p>
        <PoliteCount len={len} limit={GUIDE_PROMPTS_MAX} from={900} /> {/* always in the DOM, 1 s debounce */}
      </div>
      <div role="alert" className="actionError">{requestError}</div>
      <div className="saveRow">
        <Button type="submit" variant="primary" busy={save.isPending} busyLabel="Saving…">Save</Button>
        <SaveStatus savedAtUtc={savedAtUtc} />
      </div>
      <LeaveDialog
        blocker={blocker}                                   // opens on blocker.state === 'blocked'
        registerAsk={(fn) => { askRef.current = fn; }}     // same dialog for Sign out
        title="Leave without saving?"
        body="Your changes to the guide prompts have not been saved."
        stayLabel="Stay on this page"                       // primary, focused on open, Escape = stay
        leaveLabel="Leave without saving"                   // secondary, not red
      />
    </form>
  );
}
```

`writingsuggestions="false"` is set with `setAttribute` in a layout effect, as in `guided-notes-textarea.md`, because
React's DOM types don't include it yet. Browser inline completions don't belong in text that every worker sees on
every note (D31 spirit). [Opinion]

```css
/* TextBox.module.css: shared by GuidedNotes and the Guide prompts editor, so the "preview" can't drift */
.formColumn { max-inline-size: 40rem; }     /* the note form's column width */
.grow { display: grid; }
.grow > .box, .grow::after {
  grid-area: 1 / 1; box-sizing: border-box; inline-size: 100%;
  padding: 0.75rem; border: 2px solid transparent;
  font: inherit; font-size: max(16px, 1rem); line-height: 1.5;
  white-space: pre-wrap; overflow-wrap: anywhere;
}
.grow::after { content: attr(data-value) " "; visibility: hidden; }  /* fallback growth, set from onInput */
.box {
  min-block-size: 11em; resize: none; overflow-y: auto;
  border-color: var(--colour-input-border); border-radius: 4px;
  background: var(--colour-input-bg); color: var(--colour-text);    /* never placeholder grey here */
}
.box:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; transition: none; }
.box[aria-invalid="true"] { border-color: var(--colour-error); border-width: 3px; }
@supports (field-sizing: content) { .box { field-sizing: content; } .grow::after { content: none; } }
@media (forced-colors: active) { .box:focus-visible { outline-color: Highlight; } }
```

---

## Per-screen notes

**4.10 Guide prompts: first set-up at go-live (manager, laptop, paste from Word)**
- Expect a paste over 1,000 characters. Nothing is cut off. The count turns red and Save shows the field error, so
  the manager trims with the live count to guide them.
- Word bullets usually paste as a symbol plus a tab. How browsers draw tabs and repeated spaces inside a placeholder
  is **unverified**. After saving, the checking manager (go-live checklist: "entered by one manager and checked by
  another") opens any participant's note form from Today and looks at the Guided notes box **without tapping
  anything**. Nothing is created until the first change (3.3, A8), and opening a note is not audited (A28). This is
  the real preview, at no cost. If a draft does get started by mistake, discard it (A9).
- Content advice for whoever enters the prompts (not shown on screen) [Opinion]:
  - One short question per line. The spec's model is "a short hint", and the box shows the prompts only until the
    first character.
  - Nothing that must stay visible while writing: it disappears (D34).
  - No participant names or personal details: every worker sees the prompts on every note.
  - Shorter is kinder to screen-reader users, whose readers may read the whole placeholder each time the box is
    focused (behaviour varies; see `guided-notes-textarea.md`).

**4.10 Guide prompts: later edits (any manager, phone or laptop)**
- A saved change reaches open, empty Guided notes boxes straight away (4.10). The editor says only "Saved 4:12 pm".
  It doesn't promise when another device will show the change, which depends on when that device next fetches
  (`guided-notes-textarea.md`, Behaviour 3).
- On a phone, the Save button sits under the field. Close the keyboard or scroll to reach it. Nothing is sticky
  (`primary-actions.md`).
- If the manager is signed out by the 30-minute idle timeout mid-edit, the text survives in memory. After signing in
  again they press Save themselves.

---

## Anti-patterns to avoid

- **A separate live-preview pane, a "Preview" button or a mock note box** on the page. The design doesn't specify one.
  The editor's matched metrics and a real note form already do the job.
- **Showing the editor's text in placeholder grey,** or giving the editor a placeholder of its own (for example
  example prompts). Grey editable text reads as empty or disabled, and an example could be mistaken for saved
  prompts.
- **Rendering an empty editable field while loading.** One Save would wipe every worker's prompts.
- **Re-syncing the field from refetched query data,** or updating the `If-Match` `ETag` from a background refetch.
  The first overwrites typing. The second turns a conflict into a silent overwrite.
- **Autosave on this field.** Every save goes live at once, so half-typed prompts would show in workers' boxes.
- **Clearing or replacing the typed text on `412`,** a network error or a `401` (invariant I9).
- **`maxlength="1000"`.** It silently cuts off the Word paste.
- **An always-on "0 / 1,000" counter,** or a count announced on every keystroke, or `aria-live="assertive"`.
- **Disabling Save** when nothing has changed or while saving (`disabled` drops focus to `<body>`). Use
  `aria-disabled` only for the busy state.
- **A toast that fades out** for "Saved", or a success banner that steals focus while the manager stays on the page.
- **Cancel, Reset or "Restore default prompts" buttons.** Not in the design. NN/g advises against Reset and Cancel
  on web forms unless people fear they've committed to something.
- **Warning when nothing has changed,** a "don't show this again" option (Cloudscape), or a red "Leave" button.
- **`window.confirm()` / `unstable_usePrompt`** for the in-app warning.
- **A permanent `beforeunload` listener,** or trying to set custom text in it (browsers ignore it). On a phone, don't
  count on it: it may not fire.
- **Running the logout request before asking** about unsaved changes. Also, forgetting to disarm the unload guard
  before a deliberate `location.replace`.
- **Keeping a "backup" of the unsaved text** in `localStorage`, `sessionStorage` or IndexedDB (D22). Memory only.
- **Rich text, Markdown or a `contenteditable` editor.** A placeholder renders plain text and line breaks only.
- **Trimming or reflowing inner lines.** Only leading and trailing whitespace is trimmed, at Save.
- **`<form action={fn}>`** (React 19 resets uncontrolled fields after success).

---

## Tensions with decisions

**D12 / D34 with A6: up to 1,000 characters of placeholder text.** The HTML spec describes a placeholder as "a short
hint (a word or short phrase)". GOV.UK, NHS and NN/g (2014) advise against putting guidance in placeholders that
vanish. This screen is where that length is set. Against this, the same spec's own textarea example uses a
multi-line placeholder, and the design already keeps a visible label and 4.5:1 placeholder contrast (4.10). This
tension is recorded in full in `guided-notes-textarea.md` and is noted here once only. **No change is recommended.**
The editor builds the decision as specified and keeps the length advice off-screen, as content advice to managers.

---

## Sources

**[Standard]**
- WHATWG HTML, `textarea` `placeholder` (short hint; shown when empty and unfocused; CR/LF are line breaks; multi-line
  example): https://html.spec.whatwg.org/multipage/form-elements.html#attr-textarea-placeholder
- MDN, `Window: beforeunload` event: https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- Chrome for Developers, Page Lifecycle API: https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- web.dev, Back/forward cache: https://web.dev/articles/bfcache
- React Router, `useBlocker`: https://reactrouter.com/api/hooks/useBlocker
- React Router, Navigation blocking how-to: https://reactrouter.com/how-to/navigation-blocking
- React Router issue #11589 (Back button and `useBlocker`, v6.23.1, closed as not planned):
  https://github.com/remix-run/react-router/issues/11589
- React, `<form>` (uncontrolled fields reset after an action): https://react.dev/reference/react-dom/components/form
- React, `<textarea>`: https://react.dev/reference/react-dom/components/textarea
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 3.3.4 Error Prevention (Legal, Financial, Data):
  https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum):
  https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WAI-ARIA APG, Dialog (Modal): https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- WAI-ARIA APG, Alert dialog: https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- MDN, `::placeholder`: https://developer.mozilla.org/en-US/docs/Web/CSS/::placeholder
- MDN, `field-sizing`: https://developer.mozilla.org/en-US/docs/Web/CSS/field-sizing

**[Research]**
- NN/g, Harley, *Visibility of System Status* (3 June 2018): https://www.nngroup.com/articles/visibility-system-status/
- NN/g, Flaherty, *Indicators, Validations, and Notifications* (17 January 2024):
  https://www.nngroup.com/articles/indicators-validations-notifications/
- NN/g, *Placeholders in Form Fields Are Harmful* (2014, reviewed 2018):
  https://www.nngroup.com/articles/form-design-placeholders/
- NN/g, *Confirmation Dialogs Can Prevent User Errors (If Not Overused)*:
  https://www.nngroup.com/articles/confirmation-dialog/
- NN/g, Nielsen, *Reset and Cancel Buttons* (2000): https://www.nngroup.com/articles/reset-and-cancel-buttons/
- Baymard Institute, *Usability Testing of Inline Form Validation* (2024): https://baymard.com/blog/inline-form-validation

**[Convention]**
- GOV.UK Design System, Making labels and legends headings:
  https://design-system.service.gov.uk/get-started/labels-legends-headings/
- GOV.UK Design System, Character count (2017 research, threshold, over-limit behaviour):
  https://design-system.service.gov.uk/components/character-count/
- GOV.UK Design System, Notification banner: https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK Design System, Validation pattern: https://design-system.service.gov.uk/patterns/validation/
- GOV.UK Design System, Textarea: https://design-system.service.gov.uk/components/textarea/
- AWS Cloudscape Design System, Unsaved changes pattern: https://cloudscape.design/patterns/general/unsaved-changes/
- Adam Silver, *Don't use the maxlength attribute*:
  https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/

**Internal**
- design.md §3.3, 3.4, 4.0, 4.3, 4.10, 5.1, 6.6, 6.7, 6.9, 7.5, 13 (A6, A8, A9, A28), 14 (M1, M6, go-live checklist);
  decisions D12, D22, D31, D34
- Sibling component docs: `guided-notes-textarea.md`, `primary-actions.md`, `status-messages.md`,
  `form-validation.md`, `confirm-dialog.md`, `app-shell-nav.md`, `session-timeout.md`, `participant-list-rows.md`
- ui-ux-design and ui-build skill corpora (invariants I5, I9, I10; live regions in the initial DOM; never `disabled`
  on a busy button; 400 ms delayed busy label, marked as convention)
