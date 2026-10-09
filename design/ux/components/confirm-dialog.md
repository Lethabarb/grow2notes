# Confirmation dialogs

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Dialog busy text: a status line after 1 s, labels unchanged; page buttons swap labels after 400 ms (primary-actions.md two-tier rule). Exactly two warning (red) buttons in the app: Discard draft and Reset sign-in. Success after Discard, Save changes, Deactivate and similar: no banners (only Submit has one); focus goes to the `<h1>` or the changed state.

Component key: `confirm-dialog`. One shared component, `ConfirmDialog`, built on native `<dialog>` + `showModal()`.
It asks the user to confirm one action and names who the action affects. It is not used for errors, success messages
or information. Copy marked **(V)** is verbatim from design.md. Copy marked **(P)** is proposed here, because
design.md gives no string for it. Every (P) string here on 9 October 2026 was approved as written (D67); a (P) string
added later still needs the owner's approval.

---

## Where it's used

| # | Dialog | Screen (design.md) | Who sees it | Opened by | Can it be undone afterwards? | Confirm button style |
|---|---|---|---|---|---|---|
| 1 | **Submit confirmation** | 4.3 Note form | Workers and managers | **Submit note**, only after validation passes and the latest change is saved | Partly. The note can be edited, but every version is kept (3.5) and the note is in the report at once (3.4) | Standard primary |
| 2 | **Discard draft?** | 4.3 Note form (menu item "Discard draft"); also the read-only draft view managers reach from Today (4.2) and Past notes (4.4) | The draft's author; managers | "Discard draft" menu item / Discard button | No. The draft is hidden from every screen (3.4, A9) | Warning |
| 3 | **Deactivate user** | 4.11 Users, user detail | Managers | **Deactivate** | Partly. **Reactivate** restores sign-in, but the person's sessions have already ended (3.7, 8.6) | Standard primary |
| 4 | **Reset sign-in** | 4.11 Users, user detail | Managers | **Reset sign-in** (while Active) | No. The password, authenticator and passkeys are removed and the person must set up again (8.6) | Warning |
| 5 | **Archive participant** | 4.8 Participant detail | Managers | **Archive participant** | Yes. **Restore**, shown at once in the read-only banner (4.8) | **No dialog** (see Per-screen notes) |

What differs between them:
- **Dialog 1 is a check, not a warning.** Its job is to catch a note written on the wrong person (3.9), so the
  participant's name is the largest thing in it, and it is the only dialog that also shows the note date and the flag.
- **Dialogs 2 and 4 are the only actions the app cannot reverse,** so they are the only ones with the warning style.
- **Dialogs 3 and 4 act on another person**, who is not present, so the body says what happens to them.
- **Dialog 2 has a manager variant**: a manager discarding someone else's draft also sees who started it and when.

Not a confirmation dialog (no dialog at all): Restore, Reactivate, Resend invite, Archive or Restore a goal or common
item, Save changes, Cancel (editing a submitted note), Mark reviewed, Download, Invite user, Sign out.
The idle sign-out warning (4.0, 8.5) is a separate component, though it can reuse the same `<dialog>` shell and CSS.

---

## Best practice

**Is a confirmation worth it, or is undo better?**
- [Research] Warnings lose effect fast with repetition: an fMRI study found "a dramatic drop" in visual processing
  "after only the second exposure to a warning". Anderson, Kirwan, Jenkins, Eargle, Howard, Vance, CHI 2015.
  https://scholarsarchive.byu.edu/facpub/9306/
- [Research] In clinical decision support, acceptance of alerts fell about 30% with each extra alert in the same
  encounter (IRR 0.70; 112 clinicians). Ancker et al. 2017. https://pmc.ncbi.nlm.nih.gov/articles/PMC5387195/
- [Convention] Confirm only actions with serious consequences. If you "cry wolf too many times, people will stop
  paying attention". Prefer undo. Nielsen, NN/g, 18 Feb 2018 (the article gives guidance and cites no study).
  https://www.nngroup.com/articles/confirmation-dialog/
- [Convention] Apple HIG: "Avoid displaying alerts for common, undoable actions, even when they're destructive",
  but do show one for "an uncommon destructive action that they can't undo".
  https://developer.apple.com/design/human-interface-guidelines/alerts
- [Convention] Interrupt "whenever there is a chance that users' work be lost" or consequences are irreversible.
  Fessenden, NN/g, 23 Apr 2017. https://www.nngroup.com/articles/modal-nonmodal-dialog/
- [Standard] WCAG 2.2 SC 3.3.4 Error Prevention (Legal, Financial, Data), Level AA: where a page modifies or
  deletes user-controllable data in a data storage system, the action must be **reversible, checked or confirmed**.
  Confirmation is one of three equal ways to meet it.
  https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

**What goes in the dialog**
- [Convention] Restate what will happen and include identifying detail. Nielsen: "Without identifying details it's
  useless to ask users to confirm". Replace Yes/No with verbs, for example "Delete file".
  https://www.nngroup.com/articles/confirmation-dialog/
- [Convention] Apple HIG: button titles are verbs that "describe the result"; avoid OK except in purely
  informational alerts; avoid alerts that scroll; keep titles short.
  https://developer.apple.com/design/human-interface-guidelines/alerts
- [Convention] GOV.UK style guide: "Avoid negative contractions like can't and don't. Many users find them harder to
  read, or misread them as the opposite of what they say." This matters for readers with English as a second language.
  https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

**Native `<dialog>` and its semantics**
- [Standard] `showModal()` puts the dialog in the top layer, makes the rest of the page inert and gives it implicit
  `aria-modal="true"`. Escape closes it. Do not put `tabindex` on `<dialog>`.
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog ·
  https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element
- [Standard] A modal dialog's name comes from `aria-labelledby` pointing at its visible title; `aria-describedby`
  points at the message. https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- [Standard] An alert dialog is a modal dialog for "action confirmation prompts"; it needs `aria-modal="true"`, a
  label, and `aria-describedby` pointing at the message. https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- [Standard] ARIA in HTML allows `role="alertdialog"` on `<dialog>`; it is the only recommended override.
  https://www.w3.org/TR/html-aria/
- [Convention] Scott O'Hara (accessibility engineer) recommends the native element over custom modals, citing recent
  spec fixes to how it handles focus.
  https://www.scottohara.me/blog/2023/01/26/use-the-dialog-element.html

**Initial focus**
- [Standard] APG modal dialog: focus moves into the dialog on open. It may go to a static element at the start
  (with `tabindex="-1"`), or, for an irreversible final step, to "the least destructive action".
  https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- [Standard] HTML spec: autofocus the element the user is expected to use at once; "If there is no such element",
  autofocus the dialog itself. Without autofocus, `showModal()` focuses the first focusable element, which here
  would be the confirm button.
  https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element
- [Convention] Nielsen: the best default for a dangerous action is "no default answer at all".
  https://www.nngroup.com/articles/confirmation-dialog/ · Apple: "If you want to encourage people to read an alert
  and not just automatically press Return to dismiss it, avoid making any button the default button."
  https://developer.apple.com/design/human-interface-guidelines/alerts
- [Convention] The APG's own alert dialog example focuses its "No" button (the least destructive).
  https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/examples/alertdialog/
- [Opinion] React's `autoFocus` prop does not work inside `<dialog>`. React calls `focus()` at mount, while the dialog
  is still closed, and does not write the native `autofocus` attribute. The issue is still open:
  https://github.com/react/react/issues/23301 . Set focus yourself.

**Closing, Escape and the phone's Back gesture**
- [Standard] Escape closes the dialog (fires `cancel`, then `close`). When several modals are open, Escape closes only
  the last one. On close, focus returns to the element that was focused before. If that element no longer exists,
  focus goes to "another element that provides logical work flow".
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog ·
  https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- [Standard] Since Chrome 120 (2023), the Android Back gesture or button is a "close request": it closes an open
  `showModal()` dialog before it navigates history. https://developer.chrome.com/blog/chrome-120-beta
- [Standard] Browsers limit how often `cancel` can be blocked. Calling `preventDefault()` on it is only honoured when
  the page has had a user activation since the last close request, so a second Back or Escape can close the dialog
  anyway. https://github.com/WICG/close-watcher
- [Standard] `closedby` defaults to `"closerequest"` for `showModal()`: Escape or Back closes it, a tap on the
  backdrop does not. https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog

**Button order, style and labels**
- [Convention] Apple HIG: put the button people are most likely to choose "at the top in a stack"; Cancel buttons
  are "at the bottom of a stack". https://developer.apple.com/design/human-interface-guidelines/alerts
- [Convention] UK Ministry of Justice modal dialog: primary action ("Yes, delete it") first, then **"Go back"**;
  focus defaults to the dialog itself. https://design-patterns.service.justice.gov.uk/components/modal-dialog
- [Convention] On the web, button order matters less than consistency: "Deviate from the standard, and you'll easily
  cost users several minutes". Nielsen, NN/g, 2008. https://www.nngroup.com/articles/ok-cancel-or-cancel-ok/
  Platforms disagree: Windows puts the "do it" button first, Apple puts it last in a row. Neither cites research.
- [Convention] GOV.UK and NHS warning buttons: for "serious destructive consequences" that cannot easily be undone;
  "only effective if used very sparingly"; confirm with an extra step whose final button is the warning button.
  NHS: "Do not rely on the red colour of a warning button". https://design-system.service.gov.uk/components/button/ ·
  https://service-manual.nhs.uk/design-system/components/buttons
- [Convention] Apple, the other way: use the destructive style only for a destructive action "people didn't
  deliberately choose". https://developer.apple.com/design/human-interface-guidelines/alerts
- [Convention] AgDS Modal: use "sparingly"; at most two actions plus dismiss; it asks for a Close button in the top
  right. Apple alerts have no close button. https://design-system.agriculture.gov.au/components/modal

**Busy and error states**
- [Convention] GOV.UK and NHS: avoid disabled buttons ("poor contrast and can confuse some users"); prevent double
  clicks on the client **and** guard on the server. https://design-system.service.gov.uk/components/button/ ·
  https://service-manual.nhs.uk/design-system/components/buttons
- [Standard] WCAG 2.2 SC 4.1.3 Status Messages: progress and results must be announced without moving focus.
  https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- [Standard] WCAG 2.2 SC 2.5.3 Label in Name: the accessible name contains the visible label, so voice-control users
  can say what they see. https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- [Standard] WCAG 2.2 SC 2.5.8 Target Size (Minimum) is 24 × 24 CSS px. Grow2Notes sets 44 × 44 px (A32), which
  matches Apple's 44 pt. https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

**Privacy (D22, 4.0)**
- [Standard] MDN on `history.pushState`: "Some browsers save `state` objects to the user's disk so they can be restored
  after the user restarts the browser". A participant's name in router state could therefore end up stored on the
  phone. https://developer.mozilla.org/en-US/docs/Web/API/History/pushState

---

## Recommendation for Grow2Notes

### Anatomy

```
+-------------------------------------------+   <dialog role="alertdialog" aria-modal="true"
| Title (h2) — names the person             |       aria-labelledby=title aria-describedby=body>
|   [Submit dialog only: name on its own    |   <h2 id=title tabindex=-1>  initial focus here
|    line, large and bold]                  |
| Body — consequence / date / flag          |   <div id=body> 1–3 short <p>
| Error message (empty until needed)        |   <p role="alert">   always rendered, empty
| [ Confirm button — verb + person's name ] |   <button type=button> primary or warning
| [ Go back ]                               |   <button type=button> secondary
| Busy text (empty until needed)            |   <p role="status">  always rendered, empty
+-------------------------------------------+
```

- **Exactly two buttons.** The confirm button comes first (on top) and the safe button, always labelled
  **"Go back"**, comes second. No close X: "Go back", Escape and Android Back already close it, and a third, small
  target at the top of a phone screen adds nothing. [Opinion; AgDS differs]
- **"Go back", never "Cancel".** It is the design's own label (4.3). Using it everywhere also avoids a clash: on the
  note form, **Cancel** means "throw away my pending edit" (3.5), which is the opposite of "keep things as they are".
  One word should mean one thing across the product. [Convention: MOJ uses "Go back"; Apple prefers "Cancel"]
- **Buttons are stacked and full width, on every screen size,** so managers who use both phone and laptop find them
  in the same place. A long name such as "Submit note for Mohammed Al-Hashimi" wraps instead of squeezing a
  side-by-side pair. [Convention: Apple stack order; Opinion: same layout on both devices]
- **No icon, no illustration, no "Are you sure?".** The title says what will happen.

### Behaviour

1. **Opening.** The user taps the trigger. For Submit, the form first runs its own checks (Guided notes not empty,
   flag reason given) and shows its errors and summary instead of the dialog if they fail (4.3). Submit is already
   disabled while the latest change is not saved (4.3), so the dialog never opens over unsaved work.
2. **Focus on open goes to the title** (`h2`, `tabindex="-1"`). A screen reader therefore hears the participant's or
   user's name first. Enter, a held key or a repeated tap cannot confirm by accident, because no button is the
   default. This follows the APG's "static element at the start" option, Nielsen's "no default answer", and the
   intent of the HTML spec's "autofocus the dialog itself" advice. One Tab reaches the confirm button; two reach
   Go back.
3. **Confirming.** Tapping the confirm button starts the request. Both buttons become `aria-disabled="true"` and further
   taps are ignored. If the request takes more than 1 second, the busy text appears ("Submitting…"). Nothing else
   moves.
4. **Success.** The dialog does not show a success state. The screen behind it does:
   - Submit: go to Today, which announces **"Note for Jane Citizen submitted"** (V) and shows the row as Submitted.
   - Discard: go to Today, which announces "Draft for Jane Citizen discarded." (P). The row loses its status.
   - Deactivate / Reset sign-in: stay on the user detail. Status changes to Deactivated or Invited. Focus moves to
     the page heading (the user's name). The page's status region announces the result (copy below).
5. **Error.** The message appears in the dialog's alert region, above the buttons, and is announced. Focus stays on
   the confirm button, so the user can try again or go back. The user's note text is never cleared. If the action is
   **no longer possible** (already submitted or discarded elsewhere, last manager, details changed by someone else),
   the confirm button is removed, focus moves to **Go back**, and going back reloads the screen's data.
6. **Going back.** "Go back", Escape and Android Back all close the dialog with nothing changed. Focus returns to the
   trigger. Where the trigger has gone, focus goes to its owner: Discard was chosen from a menu that is now closed,
   so focus returns to the **menu button**.
7. **While busy,** Escape and Back are blocked (`cancel` → `preventDefault()`). Browsers can override that on a
   repeated request (see Best practice). If the dialog does close mid-request, the request still finishes, and its
   result is shown on the screen in the normal way.
8. **Session ended (401).** Close the dialog first, then let the app's sign-in-in-place flow run (8.5). A sign-in form
   shown behind an open modal would be inert and unreachable. After signing in, the user is back on the same screen
   and can confirm again.
9. **Stacking.** If the idle sign-out warning (8.5) opens while a confirmation is open, it sits on top. Escape closes
   only the top one (MDN).
10. **No URL change and no history entry.** The open dialog is screen state, not a route. Back therefore closes it
    (Chrome 120+) instead of navigating, and no name ever reaches a URL (4.0).

### States

| State | What the user sees | Notes |
|---|---|---|
| Default | Title, body, confirm button, Go back. Backdrop dims the page. | Title has focus; no visible ring on the title (it is not a control). |
| Hover (laptop) | Button hover styles from the shared button component | Only under `@media (hover: hover)`. |
| Focus | 3 px outline with offset on the focused button, ≥ 3:1 against the dialog surface | `outline`, never `box-shadow` alone (removed in forced-colours mode). |
| Active | Pressed style from the button component | No dialog-specific behaviour. |
| Disabled | **Not used.** HTML `disabled` is never set on dialog buttons. | `disabled` drops focus to `<body>` and fails contrast; GOV.UK and NHS advise against it. |
| Busy (loading) | Both buttons `aria-disabled="true"`, same labels and size. After 1 s: busy text under the buttons. | Taps ignored in the handler; Escape/Back blocked where the browser allows. Labels do not change, so the focused button's name stays stable. |
| Error (can retry) | Bold message in the alert region, above the buttons; buttons active again | The message is text, not colour alone (SC 1.4.1). |
| Error (no longer possible) | Message; confirm button removed; Go back focused | Go back reloads the screen's data. |
| Empty | Not applicable. The dialog always has a title, body and buttons. | |
| Read-only | Not applicable. A manager on a read-only draft view still gets the Discard dialog. | |
| Offline | Submit cannot open while the note is "Not saved: no connection" (4.3). A drop after opening gives the connection error. | |

### Exact copy

Dates use the long form "Thursday 1 October 2026"; times use "9:14 am" (Melbourne). Names are the full name as stored,
in normal case. The sketch's capitals (JANE CITIZEN) mean "large type", not uppercase text.

**1. Submit confirmation (4.3)**

| Part | Copy |
|---|---|
| Title | (V) "Submit today's note for" + name on its own line: "Jane Citizen" |
| Title when the note date is not today (manager's past-day note, or a worker's draft from an earlier day) | (P) "Submit the note for" + "Jane Citizen". "Today's" would be false. |
| Body line 1 | (V) The note date: "Thursday 1 October 2026" |
| Body line 2 | (V) "Flagged for manager: No" · (P) "Flagged for manager: Yes" |
| Confirm | (V) "Submit note for Jane Citizen" |
| Safe | (V) "Go back" |
| Busy | (P) "Submitting…" |
| Success (on Today) | (V) "Note for Jane Citizen submitted" |
| Error: connection or server | (P) "The note was not submitted. Check your connection, then try again. Your draft is saved." |
| Error: already submitted elsewhere (`409 note.version_conflict`) | (V, reused from 4.3) "This note was changed on another device or tab." No longer possible: Go back only. |
| Error: field errors (`422`) | Close the dialog. The form shows its own field errors and summary (4.3). |

**2. Discard draft (4.3; manager variant from 4.2 / 4.4)**

| Part | Copy |
|---|---|
| Title | (V) "Discard the draft note for Jane Citizen?" |
| Body, draft from an earlier day | (P) "This draft is for Wednesday 30 September 2026." (same wording as the 4.3 banner) |
| Body, manager discarding someone else's draft | (P) "Alex P. started it at 9:14 am." |
| Body, always last | (P) "Nobody will be able to open this draft again." |
| Confirm (warning style) | (P) "Discard draft for Jane Citizen" |
| Safe | "Go back" |
| Busy | (P) "Discarding…" |
| Success (on Today) | (P) "Draft for Jane Citizen discarded." |
| Error: connection or server | (P) "The draft was not discarded. Check your connection, then try again." |
| Error: `409 note.not_a_draft` | (P) "This draft has already been submitted or discarded." No longer possible. |

**3. Deactivate user (4.11)**

| Part | Copy |
|---|---|
| Title | (P) "Deactivate Sam Lee?" |
| Body | (V) "Sam Lee will be signed out everywhere now and can't sign in. Their notes stay." |
| Confirm (standard style) | (P) "Deactivate Sam Lee" |
| Safe | "Go back" |
| Busy | (P) "Deactivating…" |
| Success (page status) | (P) "Sam Lee is deactivated." |
| Error: `409 user.last_manager` | (P) "Sam Lee is the only active manager, so they cannot be deactivated." No longer possible. |
| Error: `412 precondition.failed` | (P) "Someone else has just changed Sam Lee's account. Go back to see the latest details." No longer possible. |
| Error: connection or server | (P) "Sam Lee was not deactivated. Check your connection, then try again." |

**4. Reset sign-in (4.11, 8.6)**

| Part | Copy |
|---|---|
| Title | (P) "Reset sign-in for Sam Lee?" |
| Body line 1 | (P) "Only do this after you have checked, by phone or in person, that Sam Lee is the one asking." (from 4.11: "The manager confirms who is asking … by phone or in person.") |
| Body line 2 | (P) "Their current way of signing in will be removed, and they will be signed out everywhere. A new setup link will be emailed to sam.lee@example.org." (Each person has one method, a passkey or a password with an authenticator app (A22), so the copy does not list methods they may not have.) |
| Confirm (warning style) | (P) "Reset sign-in for Sam Lee" |
| Safe | "Go back" |
| Busy | (P) "Resetting sign-in…" |
| Success (page status) | (P) "Sign-in reset. A new setup link has been emailed to sam.lee@example.org." |
| Error: `409 user.last_manager` | (P) "Sam Lee is the only active manager, so their sign-in cannot be reset here." No longer possible. |
| Error: `412` / connection | As for Deactivate, with "Sam Lee's sign-in was not reset." |

Proposed copy avoids negative contractions ("cannot", "was not") for readers with English as a second language. The
verbatim Deactivate body keeps design.md's "can't".

### Phone vs laptop

| | Phone (≤ 40 rem) | Laptop |
|---|---|---|
| Width | Screen width minus a 16 px gutter each side (343 px on a 375 px phone) | 28 rem (448 px at default text size), centred |
| Position | Centred vertically (the browser default for a modal). Not a bottom sheet. | Centred |
| Height | At most the visible height minus 2 rem (`100dvh`, so it fits when the keyboard closes); scrolls inside itself at 200% text | Same |
| Buttons | Stacked, full width, at least 44 px tall | Same |
| Dismiss | Go back; Android Back (Chrome 120+); Escape on an attached keyboard | Go back; Escape |

Tapping **Submit note** closes the phone keyboard, so the dialog opens on a full-height screen. The stacked order puts
**Go back** lower, nearer where the Submit note button was. An impatient double tap therefore lands on Go back or the
backdrop, not on the confirm button. [Opinion]

### Accessibility

**Semantics**
- `<dialog role="alertdialog" aria-modal="true" aria-labelledby="{title}" aria-describedby="{body}">`. The APG puts
  confirmation prompts under alert dialog, and ARIA in HTML permits the role on `<dialog>`. `aria-modal` is implicit
  with `showModal()`; it is stated anyway because the role is overridden and the APG alert-dialog pattern requires it.
  [Standard; Opinion on the belt-and-braces `aria-modal`]
- Title is a real `<h2>` (the page has the `<h1>`). The body is plain `<p>` text: no lists or tables, so
  `aria-describedby` reads well (APG advises against it only when the body has structure).
- Buttons are `<button type="button">`. **No `aria-label` on them**: the visible text is the accessible name
  (SC 2.5.3).
- The alert and status regions are rendered empty from the moment the dialog opens and are only ever filled in. They
  are never mounted with text already in them, and never hidden with `display: none` while empty.

**Keyboard**
- Tab and Shift+Tab move between the two buttons. A native modal lets focus pass to the browser's own controls after
  the last button; that is correct and is not a keyboard trap (SC 2.1.2).
- Enter or Space activates the focused button. Escape = Go back, except while busy.
- Nothing has a keyboard shortcut.

**What a screen reader announces** (exact wording varies by reader)
- On open (Submit): "Submit today's note for Jane Citizen, alert dialog. Thursday 1 October 2026. Flagged for
  manager: No." Then the focused title: "Submit today's note for Jane Citizen, heading level 2."
- On open (Deactivate): "Deactivate Sam Lee?, alert dialog. Sam Lee will be signed out everywhere now and can't sign
  in. Their notes stay."
- After 1 s busy: "Submitting…" (polite).
- On error: the message (assertive, `role="alert"`), without moving focus.
- On success: the destination screen's own status message, for example "Note for Jane Citizen submitted".

**WCAG 2.2 criteria this meets**
1.3.1 Info and Relationships · 1.4.1 Use of Color (the warning is in the words, not only the red) · 1.4.3 Contrast
(text 4.5:1) · 1.4.4 Resize Text and 1.4.10 Reflow (dialog scrolls internally at 200% and 320 px) · 1.4.11 Non-text
Contrast (1 px dialog border and button boundaries ≥ 3:1) · 2.1.1 Keyboard · 2.1.2 No Keyboard Trap · 2.4.3 Focus
Order · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (the top layer sits above sticky bars) · 2.5.3 Label in Name ·
2.5.8 Target Size (44 px) · 3.3.1 Error Identification · 3.3.4 Error Prevention (Discard, Deactivate, Reset sign-in
and Submit are confirmed) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.

Verify in the M6 pass (14, M6) with VoiceOver on iOS Safari, TalkBack on Android Chrome and NVDA with Chrome, and
add these dialogs to the axe checks. In particular, check that VoiceOver reads the title once focus lands on it.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **Native `<dialog>` only.** No React Aria: native covers focus, inertness, Escape, Back and the top layer, and
  React Aria's `Modal` would replace them with script. No portal: the top layer already escapes every stacking context.
- **Mount on open, unmount on close.** The parent renders `{open && <ConfirmDialog …/>}`. A layout effect calls
  `showModal()` once, guarded by `if (!dialog.open)` so StrictMode's double effect is harmless.
- **Focus the title yourself.** Set the native `autofocus` attribute on the `h2` through its ref, because React does not
  write `autoFocus` as an attribute (react#23301). After `showModal()`, call `heading.focus()` if it did not take focus.
- **Close only through `dialog.close()`** (Go back) or a close request (Escape, Back), so the browser returns focus.
  On success, navigate or update the screen and let the parent unmount the dialog.
- **React's `onClose` and `onCancel` bubble** (react.dev), unlike the DOM events. Check
  `e.target === e.currentTarget`.
- **No `command`/`commandfor` on the trigger.** Submit runs validation first, so it needs a normal click handler.
- **Mutations:** TanStack Query `useMutation`, `retry: 0` (the user retries). For Submit, create the
  `Idempotency-Key` once when the dialog opens and reuse it on every retry in that dialog. A retry after a lost
  response then returns `200` rather than a second version (6.3). User actions send `If-Match` (6.6). Map the
  problem `code` to the copy above.
- **Success messages travel in memory** (a small React context or module store read once by Today), never in React
  Router `state` or the URL: browsers may save history state to disk (MDN), and D22 forbids note data on the device.
- **No animation.** The dialog appears at once. That is simpler and needs no reduced-motion branch.
- **Scroll:** no `overflow: hidden` on `body`. The dialog has `overscroll-behavior: contain` and scrolls itself.

```tsx
// ConfirmDialog.tsx
import { useId, useLayoutEffect, useRef, type ReactNode } from 'react';
import { useDelayedFlag } from '../useDelayedFlag'; // true once `pending` has lasted 1 s
import styles from './ConfirmDialog.module.css';

type Props = {
  title: ReactNode;          // the dialog's name; must name the person
  children: ReactNode;       // the description: 1–3 short <p>
  confirmLabel: string;      // verb + name, e.g. "Deactivate Sam Lee"
  tone?: 'standard' | 'warning';
  pending: boolean;
  pendingText: string;       // "Submitting…"
  error: string | null;
  canConfirm?: boolean;      // false after a "no longer possible" error
  onConfirm: () => void;
  onClose: () => void;       // Go back, Escape, Android Back
};

export function ConfirmDialog({ title, children, confirmLabel, tone = 'standard', pending,
  pendingText, error, canConfirm = true, onConfirm, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const goBackRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const bodyId = useId();
  const slow = useDelayedFlag(pending, 1000);

  useLayoutEffect(() => {
    const dialog = dialogRef.current!;
    const heading = headingRef.current!;
    heading.setAttribute('autofocus', '');          // React won't write it (react#23301)
    if (!dialog.open) dialog.showModal();
    if (document.activeElement !== heading) heading.focus();
  }, []);

  useLayoutEffect(() => {
    if (!canConfirm) goBackRef.current?.focus();      // confirm button was removed
  }, [canConfirm]);

  return (
    <dialog
      ref={dialogRef}
      role="alertdialog"
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
      className={styles.dialog}
      onCancel={(e) => { if (pending) e.preventDefault(); }}
      onClose={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <h2 id={titleId} ref={headingRef} tabIndex={-1} className={styles.title}>{title}</h2>
      <div id={bodyId} className={styles.body}>{children}</div>
      <p role="alert" className={styles.error}>{error}</p>
      <div className={styles.actions}>
        {canConfirm && (
          <button type="button"
            className={tone === 'warning' ? styles.warning : styles.primary}
            aria-disabled={pending || undefined}
            onClick={() => { if (!pending) onConfirm(); }}>
            {confirmLabel}
          </button>
        )}
        <button ref={goBackRef} type="button" className={styles.secondary}
          aria-disabled={pending || undefined}
          onClick={() => { if (!pending) dialogRef.current!.close(); }}>
          Go back
        </button>
      </div>
      <p role="status" className={styles.status}>{slow ? pendingText : ''}</p>
    </dialog>
  );
}
```

```tsx
// In the note form: Submit (sketch)
const name = `${p.givenName} ${p.familyName}`;
<ConfirmDialog
  title={<>{isToday ? "Submit today's note for" : 'Submit the note for'}{' '}
           <span className={styles.name}>{name}</span></>}
  confirmLabel={`Submit note for ${name}`}
  pending={submit.isPending}
  pendingText="Submitting…"
  error={submitError}
  canConfirm={!noLongerPossible}
  onConfirm={() => submit.mutate()}
  onClose={() => setConfirming(false)}
>
  <p>{formatLongDate(noteDate)}</p>
  <p>Flagged for manager: {isFlagged ? 'Yes' : 'No'}</p>
</ConfirmDialog>
```

```css
/* ConfirmDialog.module.css — colour tokens come from the app's :root */
.dialog {
  box-sizing: border-box;
  width: min(100vw - 2rem, 28rem);
  max-width: none;
  max-height: calc(100dvh - 2rem);
  overflow-y: auto;
  overscroll-behavior: contain;
  padding: 1.5rem 1rem;
  border: 1px solid var(--color-border-strong);   /* ≥ 3:1; survives forced colours */
  border-radius: 0.5rem;
  background: var(--color-surface);
  color: var(--color-text);
}
.dialog::backdrop { background: rgb(0 0 0 / 0.6); }
.title { margin: 0; font-size: 1.25rem; line-height: 1.3; }
.title:focus { outline: none; }                     /* focus target, not a control */
.name { display: block; margin-top: 0.25rem; font-size: 1.75rem; font-weight: 700;
        overflow-wrap: anywhere; }                  /* normal case, no text-transform */
.body > p { margin: 0.75rem 0 0; }
.error { margin: 1rem 0 0; font-weight: 700; color: var(--color-error-text); }
.actions { display: grid; gap: 0.75rem; margin-top: 1.5rem; }
.actions > button { width: 100%; min-height: 44px; }
.status { margin: 0.75rem 0 0; }
@media (min-width: 40rem) { .dialog { padding: 2rem; } }
/* .primary / .secondary / .warning compose the shared button styles (hover, focus, active, aria-disabled, forced colours) */
```

---

## Per-screen notes

**4.3 Note form: Submit confirmation**
- Opens only after the form's checks pass and the note is saved. The dialog never shows field errors.
- The name is the biggest text in the dialog and appears twice: in the title and in the button (3.9). The person's
  name changes on every note, so the dialog looks different each time. That is the best defence against the
  habituation described in Best practice. Keep it that way: no generic title, no "Don't show this again".
- Manager past-day note and worker earlier-day draft: title "Submit the note for", and the date line shows the note
  date, which is the date that matters (3.3, 3.8).
- **Editing a submitted note** (Save changes / Cancel) gets no confirmation. design.md specifies none.
- After success, Today puts focus on its heading and announces "Note for Jane Citizen submitted" (Today's spec).

**4.3 Note form: Discard draft**
- Reached from the menu next to Submit (4.3). Only offered once a draft exists (a "New" form has nothing to discard).
- Warning style, because the app cannot bring a discarded draft back (A9). This, with Reset sign-in, is one of only
  two warning buttons in the product, which keeps red meaningful (GOV.UK, NHS).
- **Go back** returns focus to the menu button, not to the closed menu's item.

**4.2 Today / 4.4 Past notes: manager discarding someone else's draft**
- Same dialog. The body adds "Alex P. started it at 9:14 am." so the manager can see whose work goes. After success,
  the manager lands on Today.

**4.8 Participants: Archive participant (no dialog)**
- Recommendation: **no confirmation**. design.md 4.8 specifies none. Archiving is rare and fully reversible, and the
  page at once shows the read-only banner with **Restore**, which works as an immediate, permanent undo. This is
  the pattern Apple, Raskin and NN/g recommend for undoable actions. Moving focus to that banner after archiving, so
  screen-reader users hear the result, belongs to the participant detail spec.
- Restore, archiving a goal and Restore on a goal get no dialog either.

**4.11 Users: Deactivate and Reset sign-in**
- Deactivate: standard style, because Reactivate exists. It still gets a dialog (design-specified) because the effect
  lands on someone who is not present and cannot be taken back: their sessions end at once.
- Reset sign-in: warning style. The body's first line reminds the manager to check who is asking (4.11, 8.6), because
  a reset is how someone would take over an account. The email address is shown so the manager can check that the
  link goes to the right place.
- If the server reports `user.last_manager` (A26), the dialog explains it and offers only Go back. Whether the buttons
  appear at all for the last manager belongs to the user detail spec.
- After success, the Deactivate or Reset button is replaced (by Reactivate or Resend invite), so the trigger is gone.
  Focus goes to the page heading, and the page's status region announces the result.
- Not covered by design.md: changing a user's **email** also resets sign-in (4.11) but has no confirmation. See
  open questions.

---

## Anti-patterns to avoid

- **`window.confirm()`.** Its buttons are OK/Cancel and cannot be relabelled, it cannot name the person in the
  button, and it shows the site's address as its title.
- **A `<div>` with `position: fixed` and `role="dialog"`.** It has no top layer, no inert background, no Escape or Back
  handling and no focus return.
- **"Are you sure?" with Yes/No or OK/Cancel.** Every button must say the verb, and the confirm button must name
  the person.
- **Focusing the confirm button on open,** or letting the browser do it by leaving out focus handling. The confirm
  button is first in the DOM, so `showModal()` would focus it, and Enter would confirm.
- **Relying on React's `autoFocus` inside `<dialog>`.** It does nothing (react#23301).
- **HTML `disabled` on the buttons while busy.** Focus falls to `<body>` and the keyboard user is lost. Use
  `aria-disabled` and ignore taps.
- **Changing the confirm label to a spinner or "Please wait".** The name of the focused button changes under the user.
  Use the separate status line.
- **Mounting the alert region with its message,** or hiding the empty region with `display: none`. Both stop the
  announcement in some screen readers.
- **Light dismiss (`closedby="any"` or a backdrop click handler).** A stray tap on a phone would cancel. It also
  does not work on iOS Safari.
- **A close X in the corner as well as Go back.** It is a third, small target that does the same thing.
- **Red or uppercase as the only signal.** Red must come with words. All-caps names are harder to read and may be
  spelt out letter by letter by some screen readers. [Opinion]
- **A "Don't ask me again" tick box.** It adds a setting, and remembering it would need device storage (D22).
- **The participant's name in the URL, the query string, router `state` or the page title** while the dialog is open
  or after it closes (4.0, D22).
- **A confirmation for reversible, routine actions** (Restore, Reactivate, archiving goals or common items, Mark
  reviewed). Each extra dialog teaches people to click through the ones that matter.
- **Using this dialog for errors, success or information.** Errors stay next to the field or in the banner (4.3); success
  is announced on the destination screen.
- **Unmounting an open dialog on the "Go back" path** instead of calling `close()`. Focus is not returned.

---

## Tensions with decisions

1. **A confirmation on every Submit (design.md 3.9 and 4.3, supporting D39).** Workers submit several notes a day, so
   this dialog will be seen far more often than any other. The evidence says repeated warnings lose effect from the
   second exposure (Anderson et al., CHI 2015, fMRI), and clinical alert acceptance falls about 30% per extra alert
   (Ancker et al. 2017). Apple advises against alerts for common actions, and Nielsen warns against "crying wolf".
   The design limits this as far as a dialog can: it is a name check rather than a warning, and the name changes
   every time. No change is recommended; the owner should simply know that its protection will weaken with use.
2. **Cancel on "Editing submitted note" throws away the pending edit with no confirmation (design.md 3.5, A10),
   against the WCAG 2.2 AA target (A32).** The pending edit is stored on the server (DELETE `{base}/draft`), and
   SC 3.3.4 requires deleting user-controllable data in a data storage system to be reversible, checked or
   confirmed. Whether a private working copy counts as "user-controllable data" is a matter of interpretation.
   Flagged for the owner; no change recommended here.

---

## Sources

- Anderson, Kirwan, Jenkins, Eargle, Howard, Vance, "How Polymorphic Warnings Reduce Habituation in the Brain", CHI 2015 — https://scholarsarchive.byu.edu/facpub/9306/
- Ancker et al., "Effects of workload, work complexity, and repeated alerts on alert fatigue", BMC Med Inform Decis Mak 2017 — https://pmc.ncbi.nlm.nih.gov/articles/PMC5387195/
- Nielsen, "Confirmation Dialogs Can Prevent User Errors (If Not Overused)", NN/g, 2018 — https://www.nngroup.com/articles/confirmation-dialog/
- Fessenden, "Modal & Nonmodal Dialogs: When (& When Not) to Use Them", NN/g, 2017 — https://www.nngroup.com/articles/modal-nonmodal-dialog/
- Nielsen, "OK–Cancel or Cancel–OK? The Trouble With Buttons", NN/g, 2008 — https://www.nngroup.com/articles/ok-cancel-or-cancel-ok/
- W3C WAI-ARIA APG, Dialog (Modal) Pattern — https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- W3C WAI-ARIA APG, Alert and Message Dialogs Pattern — https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- W3C WAI-ARIA APG, Alert Dialog Example — https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/examples/alertdialog/
- W3C, ARIA in HTML — https://www.w3.org/TR/html-aria/
- WHATWG HTML Standard, the dialog element — https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element
- MDN, `<dialog>` — https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog
- MDN, HTMLDialogElement `cancel` event — https://developer.mozilla.org/en-US/docs/Web/API/HTMLDialogElement/cancel_event
- MDN, `History.pushState()` — https://developer.mozilla.org/en-US/docs/Web/API/History/pushState
- WICG, CloseWatcher explainer — https://github.com/WICG/close-watcher
- Chrome for Developers, Chrome 120 beta (close requests, Android Back) — https://developer.chrome.com/blog/chrome-120-beta
- React, common components (`onCancel`, `onClose` on `<dialog>`) — https://react.dev/reference/react-dom/components/common
- React issue #23301, "autoFocus broken inside `<dialog />`" — https://github.com/react/react/issues/23301
- Scott O'Hara, "Use the dialog element (reasonably)", 2023 — https://www.scottohara.me/blog/2023/01/26/use-the-dialog-element.html
- Apple Human Interface Guidelines, Alerts — https://developer.apple.com/design/human-interface-guidelines/alerts
- GOV.UK Design System, Button — https://design-system.service.gov.uk/components/button/
- GOV.UK style guide, A to Z (Contractions) — https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- NHS digital service manual, Buttons — https://service-manual.nhs.uk/design-system/components/buttons
- Ministry of Justice Design System, Modal dialog — https://design-patterns.service.justice.gov.uk/components/modal-dialog
- Australian Government Design System, Modal — https://design-system.agriculture.gov.au/components/modal
- WCAG 2.2 Understanding: 3.3.4 Error Prevention — https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- WCAG 2.2 Understanding: 4.1.3 Status Messages — https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding: 2.5.3 Label in Name — https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding: 2.5.8 Target Size (Minimum) — https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
