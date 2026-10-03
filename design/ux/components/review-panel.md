# Review panel on a flagged note

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. participant-notes.md owns the panel for every route. The Reviewed view is `/flagged?view=reviewed`. "Earlier reviews" and "Go to flagged notes" are **not built unless the owner approves them** (participant-notes.md Open questions). Busy label "Marking reviewed…" after 400 ms. Once rendered, the panel stays mounted for the visit. The note key is `['notes', p, d]`.

Component key: `review-panel`. One component, `ReviewPanel`. It is the last block of a note's read view for a
manager when the note has been flagged. While the note is **To review** it holds an optional comment and the
**Mark reviewed** button. Once the note is **Reviewed** it shows who reviewed it, when, and their comment. It adds no
features, screens, settings or data. Copy marked **(V)** is verbatim from design.md. Copy marked **(P)** is proposed
here because design.md gives no string, and needs the owner's approval.

It reuses, and does not redefine:
- the **Mark reviewed** button's styles, busy state and error slot from `primary-actions.md` (`ActionButton`);
- the GOV.UK character-count behaviour from `conditional-reveal.md`;
- the one-field validation rule from `form-validation.md` (inline error and focus, no summary);
- the **To review** and **Reviewed** tags at the top of the read view from `status-tags.md`;
- the screen-change focus rules and route names (`/flagged`, `/flagged?view=reviewed`) from `app-shell.md`;
- the sign-out-in-place behaviour from `session-timeout.md`.

---

## Where it's used

| Screen (design.md) | How the manager gets there | Panel state | What differs |
|---|---|---|---|
| **Flagged notes, To review tab (4.6)** | Opens a row in **To review (n)** | **Form:** "Comment (optional)" + **Mark reviewed** | This is the main use, and Mark reviewed is the screen's primary action (4.6). The manager is working through a queue, oldest first, so getting back to the list matters. |
| **Flagged notes, Reviewed tab (4.6)** | Opens a row in **Reviewed** | **Read-only record:** "Reviewed by … on …" and the comment | No form, no button, no link. Nothing to do. |
| **Read view from Past notes or Today (4.4)** | A manager opens a flagged note anywhere else | Same as above, decided by the note's flag status | 4.4 lists "Mark reviewed (manager, when the note is in To review)" as a read-view action, so it is the same component with no variation. |
| **Reviewed tab rows (4.6 item 4)** | — | Not this component | The row "also shows the reviewer, the time and the comment". The list owns the row layout; it should reuse this component's `ReviewRecord` wording so the list and the note say the same thing. |
| **Never shown** | Workers; drafts (a flag on a draft is not seen, 3.6); notes that were never flagged; one old version (4.5); the edit form (4.3) | — | Not rendered at all, not rendered as unavailable. Workers can never review (section 2), and the API returns no review data to them. |

Two variants come from the re-review rule (A15), and both stay inside the same component:
- **"Flag removed in a later edit"** (4.6): the panel is unchanged. The note stays in To review until a manager
  reviews it (3.6). The read view and the list carry that sentence, not the panel.
- **Flagged again after an earlier review** (a later edit added a flag or changed the reason, A15): the earlier
  review records are shown above the comment field, under "Earlier reviews" (P), so the second manager can see what
  was already done.

---

## Best practice

### Placement: where the action sits

- **[Standard]** WCAG 2.2 SC 1.3.2 Meaningful Sequence and SC 2.4.3 Focus Order: the reading order and the focus
  order must keep the meaning. Putting the panel after the note means keyboard, screen-reader and phone users reach
  the action only after the content it acts on.
  https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html ·
  https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html
- **[Convention]** GOV.UK "Check answers" puts the commit button **after** the summary, under its own heading ("Now
  send your application"). The button text "clearly shows the action it performs".
  https://design-system.service.gov.uk/patterns/check-answers/
- **[Convention]** GOV.UK Button: "Align the primary action button to the left edge of your form."
  https://design-system.service.gov.uk/components/button/
- **[Standard]** WCAG 2.2 SC 2.4.11 Focus Not Obscured (Minimum): a focused control must not be fully hidden by
  author content, for example a sticky bar. `primary-actions.md` already rules out sticky or fixed buttons
  app-wide, because on a phone they sit under the keyboard and take up a large share of the screen at 200% text.
  https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **[Research, not measured]** NN/g (Flaherty, 17 January 2024): indicators belong "in close proximity to" the
  element they describe. The article gives guidance and reports no study data.
  https://www.nngroup.com/articles/indicators-validations-notifications/

### Labelling the optional comment

- **[Convention]** GOV.UK Question pages: "in most contexts, add '(optional)' to the labels of optional fields", and
  "Never mark mandatory fields with asterisks." https://design-system.service.gov.uk/patterns/question-pages/
- **[Convention]** NHS digital service manual: the same "(optional)" rule. It also says asterisks on essential fields
  "can make users anxious and less likely to complete the form", citing research it does not link (unverified).
  https://service-manual.nhs.uk/content/how-to-write-good-questions-for-forms/make-sure-you-need-each-question
- **[Convention]** AgDS (Australian Government Design System) adds "(optional)" to optional labels by default. Its
  setting to hide it "should be reserved for inputs that filter data in a table or chart, and should never be used
  in standard forms". https://design-system.agriculture.gov.au/components/text-input
- **[Research]** Baymard (2 October 2018, large-scale checkout usability testing, sample size not stated in the
  article): mark optional fields with "(Optional)" next to the label, not as placeholder text. When sites marked
  only optional fields, "32% of users during testing had a validation error" on an unmarked required field. That
  number comes from multi-field checkout forms. It does not transfer to this panel, which has **no** required field.
  The transferable part is the placement of "(optional)" in the label. https://baymard.com/blog/required-optional-form-fields
- **[Research, not measured]** NN/g (Budiu, 16 June 2019): marking optional fields "does lighten the user's cognitive
  load". https://www.nngroup.com/articles/required-fields/
- **[Convention]** GOV.UK Text input: "Use hint text for help that's relevant to the majority of users, like how
  their information will be used". Keep it "to a single short sentence, without any full stops". No links in hint
  text. AgDS says the same. https://design-system.service.gov.uk/components/text-input/ ·
  https://design-system.agriculture.gov.au/components/text-input
- **[Convention]** GOV.UK Textarea: "Make the height of a textarea proportional to the amount of text you expect";
  a placeholder "is not a suitable substitute for a label". https://design-system.service.gov.uk/components/textarea/
- **[Research, small sample]** GOV.UK Character count was tested with 17 participants in 2017, including people with
  low digital skills and disabilities. It does not stop people typing past the limit; it tells them how many
  characters are "too many". https://design-system.service.gov.uk/components/character-count/
- **[Convention]** Don't use `maxlength` on free text: it cuts off pasted and dictated text without telling anyone.
  https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/

### Confirming that it worked

- **[Standard]** WCAG 2.2 SC 4.1.3 Status Messages: a result shown without a change of context must be announced
  without moving focus. A change that moves focus is out of scope, because moving focus is itself announced. Either
  route is compliant: focus the result, or announce it through a live region.
  https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **[Convention]** GOV.UK Notification banner: use the green success banner "to confirm that something they're
  expecting to happen has happened". It takes focus on page load and is removed "when the user moves to a new page".
  GOV.UK lists as an open research question "how common it is for users to miss important information in
  notification banners". https://design-system.service.gov.uk/components/notification-banner/
- **[Convention]** GitHub's Primer design system: "Toasts pose significant accessibility concerns and are not
  recommended for use." It points to banners, dialogs and inline messages instead. Secondary sources say this
  followed moderated testing with disabled users; Primer's page itself doesn't publish that research (unverified).
  https://primer.style/accessibility/toasts
- **[Opinion, expert]** Adrian Roselli: a toast that goes away after a few seconds fails SC 2.2.1 Timing Adjustable;
  prefer a static message that stays. Adam Silver: toasts can't be made to "work well for everyone".
  https://adrianroselli.com/2020/01/defining-toast-messages.html ·
  https://adamsilver.io/blog/can-you-make-toast-messages-accessible/ ·
  https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- **[Convention]** Only report success after the server confirms it. Never optimistically, wherever the server is the
  record (ui-ux-design invariant I5; `primary-actions.md` 5.3).

### Returning to the list

- **[Convention]** GOV.UK Back link exists because "not all users are aware of the back button", and some avoid it
  because "some sites break when you use it". It goes "at the top of a page, before the `<main>` element".
  https://design-system.service.gov.uk/components/back-link/
- **[Research, n = 5]** Gatsby with Fable Tech Labs (11 July 2019), five 30-minute sessions with screen-reader,
  magnifier, voice and switch users: after a client-side screen change, focusing a heading "was found to be the best
  experience, as it would save time and make it clear what happened". It was a desktop-only test.
  https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- **[Convention]** GOV.UK "Complete multiple tasks": when the user returns to the list, the finished item shows its
  new status. https://design-system.service.gov.uk/patterns/complete-multiple-tasks/

### Busy and error states

- **[Convention]** GOV.UK: prevent accidental double clicks, and still guard on the server; "Disabled buttons have
  poor contrast and can confuse some users". https://design-system.service.gov.uk/components/button/
- **[Convention]** React Aria `isPending` "disables press and hover events while retaining focusability, and
  announces the pending state to screen readers". `primary-actions.md` builds the same behaviour natively with
  `aria-disabled`. https://react-aria.adobe.com/Button
- **[Standard]** WCAG 2.2 SC 3.3.1 Error Identification and SC 3.3.3 Error Suggestion: describe the error in text and
  say how to fix it. https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html
- **[Standard]** WCAG 2.2 SC 3.3.4 Error Prevention (Legal, Financial, Data) is met when the action is reversible,
  **checked** or confirmed. Checking the comment's length, with a chance to correct it, is one of the three ways.
  https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **Placement:** the last block of the read view, directly under the note's flag and reason, in the same single
   column as the note. Not at the top, not in a sidebar, not sticky.
2. **Optional field:** the label says "Comment (optional)". There is no asterisk anywhere and no `required`. One short
   hint says where the comment goes and that it is permanent.
3. **Confirmation:** **stay on the note**. The form is replaced in place by the review record ("Reviewed by Jo Smith on
   Fri 2 Oct 2026, 9:30 am" and the comment), and focus moves to it. No toast, no green banner, no dialog. The record
   uses the design's own words and shows exactly what was stored, so it is the confirmation.
4. **Returning to the list:** one link, "Go to flagged notes" (P), under the record, shown only right after this
   manager's own review. The To review list reloads when it opens: the row is gone and the badge has gone down.
   Nothing navigates on its own.
5. **No confirmation dialog** for Mark reviewed (as `confirm-dialog.md` also decides). The protection is that the
   button comes after the whole note, is never the default focus, and the result names the note right away.

### Anatomy

**To review** (phone width; laptop is the same column, capped at the read view's text width):

```
  … read view above: goals, common items, Guided notes …
  Flagged for manager: Yes                       read view's flag block (not this component),
  "Mentioned pain in his left knee after         so the reason sits right above the panel
   the walk."
 ─────────────────────────────────────────────   1 px rule + 2rem space: start of the panel
  Review                                         <h2>                                   (P)
  ┌ Earlier reviews ────────────────────────┐    <h3> + records, only after a re-flag   (P)
  │ Reviewed by Jo Smith on Fri 2 Oct 2026, │    (A15). Same ReviewRecord as below.
  │ 9:30 am                                 │
  └─────────────────────────────────────────┘
  Comment (optional)                             <label for>
  Shown in the daily report and record           hint <p id>                            (P)
  exports, and can't be changed later
  Error: Comment must be 500 characters or less  <p id>, only after a failed check     (P)
  ┌─────────────────────────────────────────┐
  │                                         │    <textarea rows="4">, no maxlength,
  │                                         │    no placeholder
  └─────────────────────────────────────────┘
  You can enter up to 500 characters             visible count (aria-hidden) + hidden polite
                                                 live region (conditional-reveal.md)
  [request error: empty until needed]            <div role="alert">, always in the DOM
  ┌─────────────────────────────────────────┐
  │              Mark reviewed              │    ActionButton, primary, type="submit"   (V)
  └─────────────────────────────────────────┘
```

**Reviewed** (opened from the Reviewed tab, or straight after this manager's own review):

```
 ─────────────────────────────────────────────
  Review                                         <h2>
  Reviewed by Jo Smith on Fri 2 Oct 2026,        <p tabindex="-1">: the focus target after
  9:30 am                                        this manager's own review
  Comment                                        small bold label (no colon)
  Called Sam's mum. GP booked for Monday.        <p>, white-space: pre-wrap; omitted when
                                                 there is no comment
  Go to flagged notes                            <Link to="/flagged">, only right after this
                                                 manager's own review                   (P)
  [Earlier reviews, if any]                      <h3> + older records, newest first
```

- The panel's `<h2>` sits under the read view's `<h1>`, next to the note's own section headings.
- No icon, no colour fill, no green "success" styling. The words carry the state (invariant I1), and the
  **To review → Reviewed** tag at the top of the read view changes silently (`status-tags.md`).
- The panel never repeats the flag reason. The read view shows "the flag and its reason" after the Guided notes text
  (4.4 order), so it is already right above the comment box.

### Behaviour

1. **When it renders.** Render it only when `me.role === 'Manager'`, the note is Submitted, and `flagStatus` is
   `ToReview` or `Reviewed`. Wait until the note query has loaded. Never render the form and then swap it for a
   record (or the other way round) as data arrives.
2. **Typing.** The count updates as the manager types and is announced politely after a pause (GOV.UK behaviour, via
   `conditional-reveal.md`). Going over 500 shows "You have N characters too many" in bold and the error colour, but
   it is **not** an error until Mark reviewed is pressed (`form-validation.md` rule 1). Enter makes a new line.
3. **Pressing Mark reviewed.**
   - If the comment is over 500 characters: show the inline error, set `aria-invalid`, move focus to the textarea,
     and send nothing. This is a one-field form, so there is no error summary (`form-validation.md` rule 6). Once
     the error shows, re-check on every input and clear it the moment the text fits ("reward early").
   - Otherwise send `POST {base}/reviews` with `{ versionNumber, comment }`. `versionNumber` is the version **on the
     screen**, never one fetched later that the manager hasn't seen (6.4: "so the manager has seen what is current").
     `comment` is the text with leading and trailing spaces trimmed; blank or spaces only is sent as `null`. Line
     breaks inside the comment are kept.
   - The button goes busy at once (`aria-disabled="true"`, so a second tap does nothing). After 400 ms it shows
     "Saving…" and the page status region announces it (`primary-actions.md`). The textarea becomes `readOnly`
     while the request runs, so the comment saved is the comment on screen. `readOnly` keeps it focusable and
     readable, unlike `disabled`.
4. **Success (`201 {reviewedAtUtc}`).** Only after the server answers:
   - Replace the form with the record, built from the response time, the manager's own `displayName` and the comment
     that was sent. Move focus to the "Reviewed by …" line (`tabindex="-1"`). The button that was pressed has gone,
     so focus must not fall back to `<body>` (`primary-actions.md` 5.4).
   - Show "Go to flagged notes" under the record.
   - Invalidate the note, both review lists, `me` (the badge) and the participant's history, so every place that
     shows To review or Reviewed agrees (`notification-badge.md`).
   - Forget the unsent comment (see 8).
5. **`409 review.not_current`.** Refetch the note, then:
   - **Already reviewed by someone else** (`flagStatus` is now `Reviewed`): show the other manager's record in place of
     the form, with the message "This note was marked reviewed while you had it open." (P) above it. If the manager
     had typed a comment, show it under "Your comment was not saved:" (P) as plain text, so they can copy it.
     Nothing they typed disappears without a trace. Move focus to the message (the button has gone).
   - **Our own earlier press landed** (the previous attempt failed with no response, and the latest review is this
     manager's): treat it as success (step 4). A lost response must not look like a conflict.
   - **Edited since it was opened** (still `ToReview`, but the current version is newer): the read view now shows the
     new version. Keep the comment in the field. Put "This note was edited after you opened it. Read the latest
     version, then mark it reviewed." (P) in the alert slot above the button, and leave focus on the button.
   - **The refetch itself fails:** use `primary-actions.md`'s general text, "This note has changed or was already
     reviewed. Check the latest version before marking it reviewed."
6. **Other failures.** No connection: "Not saved: no connection. Try again." (P). Server error: "Not saved: something
   went wrong. Try again." (P). Both go in the `role="alert"` slot above the button. Focus stays on the button and
   the comment stays. Nothing is retried automatically. `422`: map `errors.comment` to the field error. `401`: the
   sign-in-in-place flow takes over, the comment stays in memory, and Mark reviewed is **not** replayed after
   signing in (`session-timeout.md`). `403` (role changed to Worker): refetch `me`; the app shell's role guard takes
   over.
7. **Going back to the list.** "Go to flagged notes" is a real link to `/flagged`, the To review tab. It is shown
   only in the just-reviewed state, because that is the moment the manager wants the next flag. The read view's own
   back link (top of the page, owned by the read view), the Flagged nav item and the browser's Back button all work as
   well. The list refetches on mount, so the reviewed row has gone. When the list is empty it says "No flagged notes
   to review." (V). The app shell moves focus to the page `<h1>` on arrival (`app-shell-nav.md`).
8. **An unsent comment survives moving around the app.** A manager may type a comment, tap **Edit**, save the change
   and come back. Keep the unsent text in a module-level `Map` keyed by note ID, in memory only. It is cleared on
   success, on reload and on sign-out (which does a full reload, `app-shell-nav.md`). Never `localStorage`,
   `sessionStorage`, router `state`, the URL or the title (D22, 4.0).
9. **Nothing else.** No confirmation dialog, no undo, no automatic navigation, no "next flag" button, no
   unsaved-changes warning (design.md specifies that warning only for Guide prompts, 4.10).

### States

| State | What the manager sees | Semantics and behaviour |
|---|---|---|
| **Default** (To review, empty) | "Review" heading, label, hint, empty textarea, "You can enter up to 500 characters", **Mark reviewed** | Button available. No `aria-invalid`. |
| **Typing** | "You have N characters remaining" | Polite live count after about 1 s with no typing. |
| **Over limit, before press** | "You have N characters too many", bold, error colour | Not an error yet: no `aria-invalid`. |
| **Hover** (laptop, `@media (hover: hover)`) | Button: one step darker (`primary-actions.md`). Textarea: none | Hover carries no meaning. |
| **Focus** | `outline: 3px solid var(--focus); outline-offset: 2px` on the textarea and button, appearing instantly | `outline`, never only `box-shadow` (removed in forced colours). Never covered by a sticky bar (none exist). |
| **Active** (pressed) | Button fill two steps darker, instantly | Native activation on release. |
| **Disabled** | **Never.** The comment is optional, so Mark reviewed is always available. | No `disabled` attribute ever. `aria-disabled` only while busy. |
| **Loading** (note not loaded) | Panel not rendered; the read view's loading state covers it | Prevents a form-to-record flash. |
| **Busy** | Same button; "Saving…" after 400 ms | `aria-disabled="true"`, textarea `readOnly`, status region says "Saving…". |
| **Error: too long** | Message above the textarea, error-colour border and left bar, text kept | `aria-invalid="true"`; `aria-describedby` = error, hint, count hint; focus on the textarea. |
| **Error: request** | Message directly above the button, comment kept | `role="alert"` slot already in the DOM; focus stays on the button. |
| **Conflict: edited since opened** | New version above; message above the button; comment kept | As request error. |
| **Conflict: reviewed by someone else** | Message, the other manager's record, "Your comment was not saved:" + text | Focus moves to the message (`tabindex="-1"`). |
| **Success** (just reviewed) | Record, comment, "Go to flagged notes" | Focus on the record line. Badge and lists invalidated. |
| **Read-only** (Reviewed) | Record and comment | No focus move on load (the app shell focuses the `<h1>`). |
| **Empty comment** | The record has no "Comment" part | `comment: null` sent. Not an error. |
| **Re-flagged after a review** (A15) | "Earlier reviews" with the old record(s), then the form | Records are plain text, not interactive. |

### Phone and laptop

- **Phone (designed first):** a single column, full width. Textarea text at least 16 px (`max(1rem, 16px)`), because
  iOS Safari zooms into smaller fields (widely reported; no Apple document found, so unverified). The button is full
  width (`primary-actions.md` layout). Nothing is sticky. When the keyboard opens, let the browser scroll the
  textarea into view and don't script any scrolling.
- **Laptop:** the same order and column, capped at the read view's text width (about 40rem), so the comment box
  lines up with the note it answers. The button is as wide as its label and left-aligned (GOV.UK). No side-by-side
  "review sidebar": it would split the reading order from the visual order and is not in the design.
- **Both:** works at 200% text and 320 px reflow (SC 1.4.4, 1.4.10). The record and comment wrap with
  `overflow-wrap: anywhere`, so a long pasted link can't cause sideways scrolling. Nothing animates: the switch from
  form to record is instant, so there is no reduced-motion branch to build.

### Copy

| Element | Text | Source |
|---|---|---|
| Panel heading | Review | (P) |
| Earlier records heading | Earlier reviews | (P) |
| Field label | Comment (optional) | (V) "optional comment", 4.6. "(optional)" in the label is GOV.UK/NHS/AgDS convention |
| Hint | Shown in the daily report and record exports, and can't be changed later | (P). Facts from 11.3 (the report shows the comment), A20 (exports include review comments) and the append-only NoteReview (5.3) |
| Count, empty | You can enter up to 500 characters | GOV.UK default; limit from A6 |
| Count, typing | You have 120 characters remaining · You have 1 character remaining | GOV.UK default |
| Count, over | You have 12 characters too many · You have 1 character too many | GOV.UK default |
| Button | Mark reviewed | (V) 3.6, 4.6 |
| Busy label | Saving… | (P), as `primary-actions.md` |
| Record line | Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am | (V) words from M3 ("Reviewed by … on …"); Melbourne time; date style as the read view's other lines (`status-tags.md`). See Open questions. |
| Comment label in the record | Comment | (P) |
| Link after own review | Go to flagged notes | (P) |
| Error, too long | Comment must be 500 characters or less | (P), GOV.UK pattern, as `form-validation.md` |
| No connection | Not saved: no connection. Try again. | (P), as `primary-actions.md` |
| Server error | Not saved: something went wrong. Try again. | (P), as `primary-actions.md` |
| Reviewed by someone else | This note was marked reviewed while you had it open. | (P) |
| The comment that was not sent | Your comment was not saved: | (P) |
| Edited since opened | This note was edited after you opened it. Read the latest version, then mark it reviewed. | (P) |
| Conflict, fallback | This note has changed or was already reviewed. Check the latest version before marking it reviewed. | (P), from `primary-actions.md` |
| Empty list after the last review | No flagged notes to review. | (V) 4.6 (owned by the list) |

Plain English, Australian spelling, no "please", "sorry", "successfully", "invalid" or "Are you sure?". Never
"Acknowledge", "Resolve", "Close" or "Done" for this action: one word per concept (SC 3.2.4).

### Accessibility

**Semantics.**
- A `<section>` with an `<h2>`. The section gets **no** accessible name, so it stays a plain container and does not
  add a landmark. The heading alone makes the panel reachable by heading navigation (SC 1.3.1, 2.4.6).
  [Standard] https://www.w3.org/TR/html-aam-1.0/
- A `<form noValidate>` with no accessible name, so it is not a form landmark either (HTML-AAM: "If a form has no
  accessible name, do not expose the element as a landmark").
- A native `<textarea>` with a visible `<label for>`. A native `<button type="submit">`. A React Router `<Link>` for
  "Go to flagged notes". The record lines are plain `<p>`s; earlier reviews are a `<ul>` when there is more than one.

**ARIA, only these:**
- Textarea: `aria-describedby` = error id (only when shown), hint id, the visually hidden count hint id;
  `aria-invalid="true"` only after a failed check.
- The visible count is `aria-hidden="true"`; a separate visually hidden `aria-live="polite"` region, **always in the
  DOM**, carries the spoken count.
- The request-error slot is a `<div role="alert">` rendered empty from the start, directly above the button.
- The button: `aria-disabled="true"` while busy, from `ActionButton`. No `aria-label`: the visible "Mark reviewed" is
  the accessible name (SC 2.5.3).
- No `aria-live` on the record: focus moves to it, which already announces it (SC 4.1.3 does not apply when focus
  moves).

**Keyboard.** Tab reaches the textarea, then Mark reviewed. Enter in the textarea makes a new line (a textarea never
submits a form). Enter or Space on the button submits. No shortcuts and no custom key handling. Never call
`preventDefault()` on keys in the textarea, because it breaks IME and dictation.

**Screen reader announcements** (approximate; wording varies by screen reader and has not been tested):

| Moment | Expected announcement |
|---|---|
| Heading navigation reaches the panel | "Review, heading level 2" |
| Focus on the textarea | "Comment (optional), edit text, multi-line, Shown in the daily report and record exports, and can't be changed later, You can enter up to 500 characters" |
| Pause in typing | "You have 380 characters remaining" (polite) |
| Busy for more than 400 ms | "Saving…" (polite, page status region) |
| Success | Focus lands on "Reviewed by Jo Smith on Fri 2 Oct 2026, 9:30 am" |
| Too long | Focus on the field: "Comment (optional), invalid entry, Error: Comment must be 500 characters or less, …" |
| Request error | "Not saved: no connection. Try again." (assertive), focus unchanged |
| Reviewed by someone else | Focus lands on "This note was marked reviewed while you had it open." |

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence (panel after the note) · 1.4.1
Use of Color (all states in words; the over-limit count is bold too) · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text ·
1.4.10 Reflow · 1.4.11 Non-text Contrast (textarea border and focus ring at least 3:1) · 1.4.12 Text Spacing (no fixed
heights) · 2.1.1 Keyboard · 2.2.1 Timing Adjustable (no message disappears on a timer) · 2.4.3 Focus Order · 2.4.6
Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.8 Target
Size (Minimum) (44 px button, A32) · 3.2.2 On Input (typing changes nothing else) · 3.3.1 Error Identification · 3.3.2
Labels or Instructions ("(optional)" and the limit are shown before typing) · 3.3.3 Error Suggestion · 3.3.4 Error
Prevention (checked) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages (count and busy state announced without moving
focus).

**Testing (within the existing M3 and M6 scope).** Run axe through Playwright in both panel states. Do manual passes
with VoiceOver on iOS Safari, TalkBack on Android Chrome and NVDA with Chrome: the label and hint are read on focus,
focus lands on the record after success, and nothing is announced twice. Test phone keyboard dictation into the
comment, 200% text, 320 px width and Windows forced colours. Test the two-manager race: two browsers open the same
flagged note, the first marks it reviewed, and the second presses Mark reviewed with a comment typed.

### Implementation notes (React 19, native HTML, CSS Modules)

- **Native HTML covers everything here; no React Aria.** The textarea, button and link are native, and the busy
  button is the shared `ActionButton`.
- **No form library, and no React 19 `<form action>`.** A form action resets uncontrolled fields after it "succeeds",
  including when it returns errors, which would wipe the comment. Use `onSubmit` with `preventDefault()` and a
  TanStack Query `useMutation` (as `primary-actions.md` says). https://react.dev/reference/react-dom/components/form
- **The version sent is the version shown.** The read view's note query should not silently swap in a newer version
  while the manager is reading it (keep `refetchOnWindowFocus: false` on that one query). A newer version then shows
  up as the `409` "edited" message, which tells the manager to re-read, instead of changing under them unnoticed.
  The badge's `me` query keeps its focus refetch (`notification-badge.md`).
- **One length constant.** Read 500 from the shared `limits.ts` that mirrors A6 (`form-validation.md`). Count with
  `value.length`, which is UTF-16 code units, the same as the server's .NET string length for `nvarchar(500)`.
- **Extract the count.** Move `countMessage(length, max)` and its live region out of `FlagForManager` into a shared
  `CharacterCount`, so Reason and Comment behave identically (SC 3.2.4).
- **Times.** Format `reviewedAtUtc` in `Australia/Melbourne` with the app's shared formatter. Assert the exact output
  ("Fri 2 Oct 2026, 9:30 am") in a unit test: `Intl` output for `en-AU` differs slightly between engines (unverified
  in detail).
- **Assumed API shape.** design.md 6.3 says managers get `reviews` but doesn't list its fields. This sketch assumes
  `{ reviewedBy: { id, displayName }, reviewedAtUtc, comment, versionNumber }`, newest first. Align it with the API.

```tsx
// ReviewPanel.tsx
import { useEffect, useId, useRef, useState, type FormEvent, type Ref } from 'react';
import { Link } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ActionButton } from '../ui/ActionButton';       // primary-actions.md
import { CharacterCount } from '../ui/CharacterCount';   // extracted from conditional-reveal.md
import { LIMITS } from '../lib/limits';                  // reviewComment: 500 (A6)
import { formatStamp } from '../lib/melbourneTime';      // "Fri 2 Oct 2026, 9:30 am"
import { api, ApiError, noteQuery, type NoteKey, type Review } from '../api';
import s from './ReviewPanel.module.css';

const unsent = new Map<string, string>();   // memory only (D22); gone on reload or sign-out

const MSG = {
  tooLong: `Comment must be ${LIMITS.reviewComment} characters or less`,
  offline: 'Not saved: no connection. Try again.',
  server: 'Not saved: something went wrong. Try again.',
  edited: 'This note was edited after you opened it. Read the latest version, then mark it reviewed.',
  fallback: 'This note has changed or was already reviewed. Check the latest version before marking it reviewed.',
} as const;

type Props = {
  noteKey: NoteKey; noteId: string;
  shownVersion: number;                      // the version on screen
  flagStatus: 'ToReview' | 'Reviewed';
  reviews: Review[];                         // newest first
  me: { userId: string; displayName: string };
};
type Outcome = { kind: 'open' } | { kind: 'done'; review: Review } | { kind: 'taken'; lost: string };

export function ReviewPanel(p: Props) {
  const qc = useQueryClient();
  const id = useId();
  const [comment, setComment] = useState(() => unsent.get(p.noteId) ?? '');
  const [tooLong, setTooLong] = useState(false);
  const [alertText, setAlertText] = useState('');
  const [outcome, setOutcome] = useState<Outcome>({ kind: 'open' });
  const lastAttemptLost = useRef(false);
  const fieldRef = useRef<HTMLTextAreaElement>(null);
  const focusRef = useRef<HTMLParagraphElement>(null);   // record line or "taken" message

  useEffect(() => { if (outcome.kind !== 'open') focusRef.current?.focus(); }, [outcome.kind]);

  const refresh = () => ['note', 'reviews', 'me', 'history']
    .forEach((k) => qc.invalidateQueries({ queryKey: [k] }));

  const finish = (review: Review) => { unsent.delete(p.noteId); setOutcome({ kind: 'done', review }); refresh(); };

  const mark = useMutation({
    mutationFn: (c: string | null) =>
      api.post<{ reviewedAtUtc: string }>(`${p.noteKey.base}/reviews`, { versionNumber: p.shownVersion, comment: c }),
    onSuccess: ({ reviewedAtUtc }, c) =>
      finish({ reviewedBy: { id: p.me.userId, displayName: p.me.displayName }, reviewedAtUtc, comment: c }),
    onError: async (err) => {
      if (err instanceof ApiError && err.code === 'review.not_current') {
        const fresh = await qc.fetchQuery({ ...noteQuery(p.noteKey), staleTime: 0 }).catch(() => null);
        if (!fresh) return setAlertText(MSG.fallback);
        const latest = fresh.reviews?.[0];
        if (fresh.flagStatus === 'Reviewed') {
          if (lastAttemptLost.current && latest?.reviewedBy.id === p.me.userId) return finish(latest);
          setOutcome({ kind: 'taken', lost: comment.trim() }); unsent.delete(p.noteId); return refresh();
        }
        return setAlertText(MSG.edited);         // read view now shows the new version; comment kept
      }
      if (err instanceof ApiError && err.status === 422) { setTooLong(true); return fieldRef.current?.focus(); }
      // 401 and 403 are handled by the api layer (session-timeout.md, app-shell-nav.md)
      lastAttemptLost.current = !(err instanceof ApiError);     // no response: it may have landed
      setAlertText(err instanceof ApiError ? MSG.server : MSG.offline);
    },
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (mark.isPending) return;
    if (comment.length > LIMITS.reviewComment) { setTooLong(true); fieldRef.current?.focus(); return; }
    setAlertText('');
    const c = comment.trim();
    mark.mutate(c === '' ? null : c);
  }

  function onChange(next: string) {
    setComment(next); unsent.set(p.noteId, next);
    if (tooLong && next.length <= LIMITS.reviewComment) setTooLong(false);   // reward early
  }

  const showForm = outcome.kind === 'open' && p.flagStatus === 'ToReview';
  const current = outcome.kind === 'done' ? outcome.review : showForm ? undefined : p.reviews[0];
  const earlier = current ? p.reviews.filter((r) => r.reviewedAtUtc !== current.reviewedAtUtc) : p.reviews;

  return (
    <section className={s.panel}>
      <h2>Review</h2>
      {outcome.kind === 'taken' && (
        <div className={s.notice}>
          <p ref={focusRef} tabIndex={-1}>This note was marked reviewed while you had it open.</p>
          {outcome.lost && <><p className={s.label}>Your comment was not saved:</p><p className={s.text}>{outcome.lost}</p></>}
        </div>
      )}
      {current && <ReviewRecord review={current} lineRef={outcome.kind === 'done' ? focusRef : undefined} />}
      {outcome.kind === 'done' && <p><Link to="/flagged">Go to flagged notes</Link></p>}
      {earlier.length > 0 && (
        <>
          <h3>Earlier reviews</h3>
          <ul className={s.records}>{earlier.map((r) => <li key={r.reviewedAtUtc}><ReviewRecord review={r} /></li>)}</ul>
        </>
      )}
      {showForm && (
        <form noValidate onSubmit={onSubmit}>
          <label htmlFor={`${id}-c`} className={s.label}>Comment (optional)</label>
          <p id={`${id}-h`} className={s.hint}>Shown in the daily report and record exports, and can't be changed later</p>
          {tooLong && <p id={`${id}-e`} className={s.error}><span className="visually-hidden">Error: </span>{MSG.tooLong}</p>}
          <textarea
            ref={fieldRef} id={`${id}-c`} name="comment" rows={4} className={s.field}
            value={comment} readOnly={mark.isPending} onChange={(e) => onChange(e.currentTarget.value)}
            aria-invalid={tooLong || undefined}
            aria-describedby={[tooLong && `${id}-e`, `${id}-h`, `${id}-n`].filter(Boolean).join(' ')}
          />
          <CharacterCount value={comment} max={LIMITS.reviewComment} hintId={`${id}-n`} />
          <div role="alert" className={s.alert}>{alertText}</div>
          <ActionButton type="submit" variant="primary" busy={mark.isPending} busyLabel="Saving…">
            Mark reviewed
          </ActionButton>
        </form>
      )}
    </section>
  );
}

export function ReviewRecord({ review, lineRef }: { review: Review; lineRef?: Ref<HTMLParagraphElement> }) {
  return (
    <div className={s.record}>
      <p ref={lineRef} tabIndex={lineRef ? -1 : undefined} className={s.line}>
        Reviewed by {review.reviewedBy.displayName} on {formatStamp(review.reviewedAtUtc)}
      </p>
      {review.comment && <><p className={s.label}>Comment</p><p className={s.text}>{review.comment}</p></>}
    </div>
  );
}
```

```css
/* ReviewPanel.module.css: token names are placeholders for the app's own tokens */
.panel  { margin-block-start: 2rem; padding-block-start: 1.5rem; border-block-start: 1px solid var(--rule);
          max-inline-size: 40rem; }
.label  { font-weight: 700; margin-block-end: 0.25rem; }
.hint   { color: var(--text-secondary); margin-block: 0 0.5rem; }        /* ≥ 4.5:1 */
.error  { color: var(--error-text); font-weight: 700; margin-block: 0 0.5rem; }
.field  { display: block; inline-size: 100%; font: inherit; font-size: max(1rem, 16px); line-height: 1.5;
          padding: 0.5rem; border: 2px solid var(--input-border); border-radius: 0; resize: vertical; }
.field[aria-invalid="true"] { border-color: var(--error-border); border-width: 4px; }
.field:focus-visible, .line:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
.alert  { font-weight: 700; color: var(--error-text); }   /* never display:none: it must stay a live region */
.alert:not(:empty) { margin-block: 1rem 0.5rem; }         /* empty = zero height, still in the a11y tree */
.text   { white-space: pre-wrap; overflow-wrap: anywhere; margin-block-start: 0; }
.line   { inline-size: fit-content; }   /* focus box hugs the text, as the app shell does for <h1> */
.records { list-style: none; padding: 0; }
@media (forced-colors: active) { .field { border-color: CanvasText; } .field:focus-visible { outline-color: Highlight; } }
```

Notes on the sketch: in the real code, narrow the invalidation keys to this note and this participant rather than
every `['note']` and `['history']`, and reset `lastAttemptLost` after a success.

---

## Per-screen notes

**Flagged notes, To review tab (4.6)**
- This is where the panel does its main work. The manager reads the note, optionally edits it (Edit is the read
  view's secondary link, not near this button), types a comment if they want, and presses Mark reviewed.
- After success, "Go to flagged notes" returns to `/flagged`. The row has gone, the "To review (n)" tab and the badge
  have both dropped by one, and focus is on the page `<h1>` (`app-shell-nav.md`). If it was the last flag, the list
  shows "No flagged notes to review." There is no auto-advance to the next flag: that would be a new feature, and
  it would skip the moment where the manager sees the result.
- "Flag removed in a later edit" notes behave the same. The reason the manager is reviewing must still be visible in
  the read view (see Open questions).

**Flagged notes, Reviewed tab (4.6)**
- The opened note shows the panel read-only: the record and comment, with no form and no link.
- The Reviewed list rows should use `ReviewRecord`'s exact words so the list, the note and the report agree.

**Read view from Past notes or Today (4.4)**
- Identical behaviour. After success the manager stays on the note and can go back with the read view's back link or
  the browser's Back. "Go to flagged notes" still appears, because the queue is the natural next stop for a
  reviewer. It is a link and does nothing until tapped.

**Version history and one old version (4.5)**
- Never shows the panel. The API accepts a review only for the current version (6.4).

---

## Anti-patterns to avoid

- **Mark reviewed at the top of the note**, in a header action bar, or repeated at both ends. It invites acting before
  reading, and it breaks the "manager has seen what is current" rule (6.4).
- **A sticky or floating Mark reviewed bar**, or a side panel on laptop. It covers the note and focused fields
  (SC 2.4.11) and breaks reading order.
- **An asterisk, "Required", or `required`** on the comment, or **no "(optional)"**, which leaves managers
  wondering whether they must write something.
- **Placeholder text as the label or the hint** ("Add a comment…"). It disappears on typing and fails contrast.
- **`maxlength="500"`**, which silently cuts off pasted or dictated text.
- **A toast or snackbar** ("Marked as reviewed") that disappears on a timer (SC 2.2.1), sits away from the
  action, or is missed by magnifier users.
- **Navigating away automatically** after success, or auto-opening the next flag. The manager never sees the result.
- **Optimistic success:** showing "Reviewed by…" or dropping the row before the server answers.
- **`disabled` on the busy button**, which throws keyboard focus to `<body>`. Or removing the button on success
  without moving focus somewhere deliberate.
- **Clearing the typed comment** on any error, conflict or sign-out, or silently discarding it when another manager
  got there first.
- **A confirmation dialog** ("Are you sure you want to mark this reviewed?"). It is routine, frequent work; every
  extra dialog teaches people to click through the ones that matter (`confirm-dialog.md`).
- **Inventing review data:** outcome categories, "action taken" pickers, severity, follow-up dates, an "Unreview" or
  "Reopen" button, editing a saved comment, or notifying the worker. None is in the design.
- **Showing the review to workers.** Review comments are manager-only on screen (section 2).
- **Putting the comment or the participant's name in the URL, router `state`, the page title or browser storage**
  (4.0, D22).
- **Green styling or an icon as the only sign** that the note is reviewed. The words say it.

---

## Tensions with decisions

1. **D22 (nothing stored on the device) means an unsent review comment cannot survive a reload or a closed tab.**
   General guidance is to keep partial work across interruptions (ui-ux-design invariant I9; WCAG SC 2.2.5
   Re-authenticating, Level AAA, asks that data survives re-authentication, which `session-timeout.md` already
   handles within the tab). The in-memory map in this design covers moving around the app and signing in again, not a
   reload. The comment is optional and at most 500 characters, so the cost is small. No change is recommended.
   https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html

No other real tension. One review is enough (D24, A14), there is no approval step (D17), and no alerts other than
the in-app list and badge (D18). All of these fit this panel as specified.

### Open questions (gaps in design.md, not tensions)

1. **Which "Reviewed by" string?** M3 says the note and the report show "Reviewed by … on …". The report layout in
   11.3 shows "Reviewed by Jo Smith, Fri 2 Oct 9:30 am". This spec uses "Reviewed by Jo Smith on Fri 2 Oct 2026,
   9:30 am" on screen. Pick one string for the screen, the Reviewed list and the report.
2. **The flag reason when the tick was removed.** For "Flag removed in a later edit", the current version has no
   reason. `GET {base}` returns only `current.flagReason`, so the read view above the panel may not be able to show
   what the manager is reviewing. Confirm that `flagStatus` (or `reviews`) carries the reason from the flagged version.
3. **The fields in `reviews[]`** are not listed in 6.3. Confirm the shape this sketch assumes.
4. **Proposed copy:** "Review", "Earlier reviews", the hint, "Comment", "Go to flagged notes", the error and conflict
   messages. "Go to flagged notes" and the hint can each be dropped without changing anything else.
5. **Earlier reviews after a re-flag (A15):** this spec shows them. The alternative is showing only the latest review.

---

## Sources

- design.md §2, §3.6, §4.4, §4.5, §4.6, §5.3 (NoteReview), §6.3, §6.4, §6.7, §6.9, §11.3, §13 (A6, A14, A15, A20), §14 M3; decisions.md D17, D18, D22, D24.
- Sibling specs: `primary-actions.md`, `conditional-reveal.md`, `form-validation.md`, `status-tags.md`, `notification-badge.md`, `app-shell-nav.md`, `session-timeout.md`, `confirm-dialog.md`.
- GOV.UK Design System, Question pages: https://design-system.service.gov.uk/patterns/question-pages/
- GOV.UK Design System, Check answers: https://design-system.service.gov.uk/patterns/check-answers/
- GOV.UK Design System, Complete multiple tasks: https://design-system.service.gov.uk/patterns/complete-multiple-tasks/
- GOV.UK Design System, Button: https://design-system.service.gov.uk/components/button/
- GOV.UK Design System, Text input (hint text): https://design-system.service.gov.uk/components/text-input/
- GOV.UK Design System, Textarea: https://design-system.service.gov.uk/components/textarea/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- GOV.UK Design System, Notification banner: https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK Design System, Back link: https://design-system.service.gov.uk/components/back-link/
- NHS digital service manual, Make sure you need each question: https://service-manual.nhs.uk/content/how-to-write-good-questions-for-forms/make-sure-you-need-each-question
- Australian Government Design System (AgDS), Text input: https://design-system.agriculture.gov.au/components/text-input
- Baymard Institute, "E-Commerce Checkouts Need to Mark Both Required Fields and Optional Fields Explicitly" (2 Oct 2018): https://baymard.com/blog/required-optional-form-fields
- Budiu, R., "Marking Required Fields in Forms", NN/g (16 Jun 2019): https://www.nngroup.com/articles/required-fields/
- Flaherty, K., "Indicators, Validations, and Notifications", NN/g (17 Jan 2024): https://www.nngroup.com/articles/indicators-validations-notifications/
- GitHub Primer, Toasts: https://primer.style/accessibility/toasts
- Roselli, A., "Defining 'Toast' Messages" (2020, updated): https://adrianroselli.com/2020/01/defining-toast-messages.html
- Silver, A., "Can you make toast messages accessible?": https://adamsilver.io/blog/can-you-make-toast-messages-accessible/
- Silver, A., "Don't use the maxlength attribute to stop users from exceeding the limit": https://adamsilver.io/blog/dont-use-the-maxlength-attribute-to-stop-users-from-exceeding-the-limit/
- Sutton, M., Gatsby and Fable Tech Labs, "What we learned from user testing of accessible client-side routing techniques" (11 Jul 2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- React Aria, Button (`isPending`): https://react-aria.adobe.com/Button
- React, `<form>` (form action resets uncontrolled fields): https://react.dev/reference/react-dom/components/form
- W3C, HTML Accessibility API Mappings (form and section landmark mapping): https://www.w3.org/TR/html-aam-1.0/
- WCAG 2.2 Understanding: 1.3.2 Meaningful Sequence https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html · 2.2.1 Timing Adjustable https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html · 2.2.5 Re-authenticating https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html · 2.4.3 Focus Order https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html · 2.4.11 Focus Not Obscured (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html · 3.3.1 Error Identification https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html · 3.3.4 Error Prevention (Legal, Financial, Data) https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html · 4.1.3 Status Messages https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
