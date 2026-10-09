# Form validation and error summary

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> The editorial pass of 1 October 2026 settled these app-wide rules here (they replace older values below):
> - **Error summary position:** first in `<main>`, above the `<h1>` (back links live in the shell's before-main
>   bar slot, so nothing in `<main>` comes before the summary). Field errors only.
> - **Request failures that belong to no field** (no connection, server error, `412`, a rate limit) never go in the
>   summary. They go in the always-present `role="alert"` directly above the submit button, and focus stays on the
>   button. The one exception is sign-in's A25 "Sign-in failed" message: a credential outcome, it stays an unlinked
>   summary item (sign-in.md).
> - **Mutations use `retry: 0`** (TanStack's default). Nothing is retried automatically unless repeating it is safe
>   (autosave `PUT`s; a change refused before any handler ran, which the `api()` wrapper sends once more with a fresh
>   antiforgery token; the note-version `POST` with its `Idempotency-Key`, retried only when the person presses again).
> - **Radio pre-selection:** no radio group is pre-selected (GOV.UK), so a "Choose …" error can occur. The one stated
>   exception is users.md's Role, which starts on Worker (least privilege) and is listed as an owner question. Every
>   radio uses the shared ChoiceRow: a 40 px circle drawn with `appearance: none` in a 56 px row (users.md), never
>   `accent-color`.
> - **Passwords after a failed sign-in are cleared** (GOV.UK password input; sign-in.md Conflicts #1), not kept as
>   rule 5 below says.
> - **"Not saved: fix the error below." is not used** (note-form.md Conflicts #1): over-limit text keeps autosaving a
>   capped copy and errors only on Submit.

Component key: `form-validation`. This covers how Grow2Notes checks what people type, how it tells them what to fix, and where focus goes. It applies the design as written (design.md §3, §4, §6.9, §13). It adds no screens, settings or data.

Evidence grades: **[Research]** studies, usability or assistive-technology testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML spec, NIST · **[Convention]** established design systems · **[Opinion]** reasoned judgement with no direct evidence.

---

## Where it's used

| Screen (design.md) | Form | What can go wrong | What differs |
|---|---|---|---|
| **4.3 Note form** (submit and Save changes) | Guided notes, Flag for manager + Reason | Guided notes empty; flag ticked with no reason; text over its length limit (Guided notes 20,000, Reason 200, A6) | Design requires "an error next to the field, plus a summary at the top when Submit is tapped". Checks run **before** the submit confirmation dialog opens. Autosave keeps running, so a length error blocks saving as well as submitting. Submit is disabled while the latest change is unsaved (§3.4). |
| **4.1 Sign-in** | Email + password, then the 6-digit code | Empty fields; wrong details, locked or deactivated account | Server failures always show **one generic message** that never says which part was wrong (A25). The code step is a one-field form. |
| **4.1 Account setup** (password path) | Password + 6-digit code, sent together | Empty fields; password under 12 characters; wrong code; enrolment cookie expired | The person is already identified by the setup link, so a wrong code can be named specifically. The passkey path has no fields. |
| **4.8 Participant detail** | Details (given name, family name, date of birth); Add goal / Edit goal; Write past-day note (a date) | Empty, too long, impossible or future date of birth; past-day date not in the past | Several independent forms on one page. Details has three fields. Goal and past-day forms have one field each. |
| **4.9 Common items** | Add item / Edit item (text, plus the Group radios); New group / Group name (rename) | Empty; over 200 characters | One-field pattern only. The item Edit form's Group radios can't be in error (one is always selected), so only its text is validated. |
| **4.11 Users** | Invite user; Edit user (name, email, role) | Empty, too long, malformed email; email already used (`409 user.email_in_use`); last active manager (`409 user.last_manager`); stale edit (`412`) | Most of the server-only rules in the app live here. |
| **4.12 Participant record export** | From date, To date, Format, Include earlier versions | Empty dates; To date before From date | A cross-field rule. Design: "an error next to the field". "No submitted notes…" is a result, not an error. |

Read-only values are never validated and never show an error state: the email at setup ("fixed"), the participant on the export form ("fixed"), and an archived participant's details ("read-only banner").

---

## Best practice

### When to validate

- **[Research]** In two peer-reviewed studies (n = 77 and n = 90), showing the fields in error **after the whole form was completed** worked best. Immediate feedback did worst, because people "often simply ignored the messages" while they were still filling in the form ("Completion Mode"). Bargas-Avila et al., *Interacting with Computers* 19(3), 2007. https://academic.oup.com/iwc/article-abstract/19/3/330/693000
- **[Research]** Wroblewski with Etre (n = 22, one form, 2009). Inline validation on **blur** beat submit-only. Validating **while typing** was slower, and showing errors "before and while" typing gave the most errors and the lowest satisfaction. Only 30–50% of participants noticed messages on easy fields such as name and address, against 80–100% on hard fields such as username and password. Etre called the figures "indicative". https://alistapart.com/article/inline-validation-in-web-forms/
- **[Convention]** GOV.UK says to validate when people try to move on ("usually by clicking the 'continue' or 'submit' button"), not on blur. https://design-system.service.gov.uk/patterns/validation/ · AgDS says "A form should validate when a user attempts to submit the form." https://design-system.agriculture.gov.au/patterns/accessible-form-validation-and-recovery
- **[Convention]** NN/g prefers inline validation but says "Don't Validate Fields Before Input is Complete". https://www.nngroup.com/articles/errors-forms-design-guidelines/ The sources disagree because of one hidden variable: whether the user already knows the right answer. Every Grow2Notes field holds something the user knows (their notes, a name, a date), so the validate-on-submit sources fit this app better.
- **[Research]** Clear an error the moment it is fixed. Baymard watched a user whose password error stayed on screen after they had fixed it, so they could not tell that they had succeeded (qualitative). https://baymard.com/blog/inline-form-validation

### Error summary

- **[Convention]** GOV.UK and NHS: always show a summary when there is a validation error, "even if there's only one". Put it at the top of the main container, above the `<h1>`, with the heading "There is a problem". Each item links to its field, with link text that "must" match the inline message exactly. Prefix the page `<title>` with "Error: ". https://design-system.service.gov.uk/components/error-summary/ · https://service-manual.nhs.uk/design-system/components/error-summary
- **[Convention]** AgDS: put the summary at the top of the form and focus it "immediately after a submission attempt". If there is **only one** error, show no summary and focus the field instead. https://design-system.agriculture.gov.au/patterns/accessible-form-validation-and-recovery · Use `tabIndex={-1}` for programmatic focus. https://design-system.agriculture.gov.au/components/page-alert
- **[Convention]** NN/g: "Don't Use Validation Summaries as the Only Indication of an Error". Keep messages next to their fields. https://www.nngroup.com/articles/errors-forms-design-guidelines/
- **[Standard]** W3C WAI forms tutorial: list errors at the top with in-page links, connect fields to messages with `aria-describedby`, and update the `<title>`. https://www.w3.org/WAI/tutorials/forms/notifications/
- **[Convention]** In govuk-frontend's summary script, a link click calls `preventDefault()`, scrolls the field's **label or legend** into view, then calls `focus({ preventScroll: true })` on the input. It does not change the URL hash. https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/error-summary/error-summary.mjs

### Inline error messages and ARIA

- **[Standard]** WCAG 3.3.1 Error Identification (A): identify the item in error and describe the error in text. WCAG 3.3.3 Error Suggestion (AA): say how to fix it. WCAG 1.4.1 Use of Color (A): colour must not be the only signal. https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html · https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html · https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- **[Convention]** GOV.UK puts the message after the label and hint and **before** the input, with a red left border tying it to the field, a visually hidden "Error:" prefix, and `aria-describedby` from the input to the message. https://design-system.service.gov.uk/components/error-message/ NSW puts the alert "immediately after the field". https://designsystem.nsw.gov.au/components/form/index.html No controlled study of placement was found (unverified absence).
- **[Research]** Adrian Roselli's screen-reader testing (2023) found that `aria-describedby` errors were "consistently exposed when navigating by fields". `aria-errormessage` "is generally not exposed when navigating through fields". When a live region is treated as assertive, "the name of the subsequent field that just received focus is clipped or lost". https://adrianroselli.com/2023/04/exposing-field-errors.html · a11ysupport.io reports partial support for `aria-errormessage`. https://a11ysupport.io/tech/aria/aria-errormessage_attribute
- **[Standard]** WAI-ARIA: "if the user has not attempted to submit the form, authors SHOULD NOT set the `aria-invalid` attribute on required widgets". https://w3c.github.io/aria/#aria-invalid
- **[Convention]** GOV.UK turns off HTML5 validation with `novalidate` and leaves `required` off inputs, because native bubbles "cannot be made consistent". It notes it has no research on how omitting `required` affects screen readers. AgDS also uses `noValidate`. (Same validation URLs as above.)

### Wording

- **[Convention]** GOV.UK: messages should be "clear and concise", say what happened and how to fix it, and "make sense out of context". Use the same text inline and in the summary. Avoid "please", "sorry", "valid/invalid", "oops", jargon and blame ("you forgot"). Templates: "Enter [whatever]"; "[Whatever] must be [n] characters or less"; "[Whatever] must be the same as or after [date]"; "[Whatever] must be in the past"; "[Whatever] must be a real date"; "Enter an email address in the correct format, like name@example.com". https://design-system.service.gov.uk/components/error-message/ · https://design-system.service.gov.uk/components/date-input/ · https://design-system.service.gov.uk/components/character-count/ · https://design-system.service.gov.uk/patterns/email-addresses/
- **[Research]** NN/g on placeholders: "Users may mistake a placeholder for data that was automatically filled in", and some "skip the field completely". https://www.nngroup.com/articles/form-design-placeholders/ This matters because the Guided notes prompts are placeholders (D12, D34).
- **[Convention]** OWASP: sign-in must respond "in a generic manner" whether the user ID is wrong, the password is wrong, or the account does not exist, is locked or is disabled. This supports A25. https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html

### Keeping input, server checks, authentication

- **[Standard]** WCAG 3.3.7 Redundant Entry (A): don't make people re-enter what they already gave. The exceptions are re-entry that is essential, needed for security, or for information that is no longer valid. https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- **[Convention]** GOV.UK: "You'll always need to carry out server side validation", and re-show the form "as the user filled them in". AgDS says the same. (Validation URLs above.)
- **[Standard]** React 19: "After the `action` function succeeds, all uncontrolled field elements in the form are reset." An action that *returns* validation errors still counts as succeeding, so the fields empty themselves (react#29034). https://react.dev/reference/react-dom/components/form · https://github.com/react/react/issues/29034
- **[Convention]** GOV.UK's character count deliberately does not block input past the limit, so people can paste or type a full answer and then trim it. `maxlength` would truncate silently. https://design-system.service.gov.uk/components/character-count/
- **[Standard]** WCAG 3.3.8 Accessible Authentication (Minimum) (AA): allow paste and password managers. https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html · NIST SP 800-63B: permit paste, offer show-password, never truncate. https://pages.nist.gov/800-63-4/sp800-63b.html
- **[Standard]** WCAG 1.3.5 Identify Input Purpose covers fields that collect information **about the user**. The participant and invitee fields describe someone else. https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html
- **[Convention]** GOV.UK on disabled buttons: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible." https://design-system.service.gov.uk/components/button/
- **[Standard]** WCAG 2.4.11 Focus Not Obscured (Minimum) (AA): the focused field must not be fully hidden, for example by a sticky top bar. https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html

---

## Recommendation for Grow2Notes

### Rules in one place

1. **Validate on submit only.** Submit means Submit note, Save changes, Save, Add, Send invite, Export, Sign in or Finish setup. Nothing turns red on load, on focus, on blur or during first typing. [Research + Convention]
2. **Once a field shows an error, re-check it on every input and clear the error the moment it is fixed** ("reward early"). The error never moves from one field to another while the user types. [Research, qualitative]
3. **Exception: length limits on the note form are checked while typing.** Autosave cannot store over-limit text (the draft `PUT` returns `422`), and Submit is disabled until the save succeeds (§3.4). The user cannot see that the limit has been crossed unless the app says so. This is the "only the system knows" case in which both bodies of evidence support early feedback. [Opinion, grounded in the Research above]
4. **The server is the gate.** The client repeats the server's rules only to give faster, better-worded feedback. A server `422` is mapped onto the same fields and shown the same way. [Convention]
5. **Never clear what was typed**, with two exceptions: a rejected 6-digit code (time-limited and no longer valid), and the password after a failed sign-in, which sign-in.md clears following GOV.UK's password input guidance (both are WCAG 3.3.7 exceptions). [Standard] [Convention]
6. **Multi-field forms** (note form, sign-in password step, setup, participant Details, invite/edit user, export) show **inline errors and a summary**, even with one error. This matches design 4.3 and GOV.UK/NHS. **One-field forms** (sign-in code step, add/edit goal, add/edit common item, past-day note date) show the inline error only and move focus to the field. This matches AgDS. [Convention]
7. **All forms use `noValidate`.** No `required` attribute and no asterisks: every text field in these forms is needed, and the only optional inputs are tick boxes. [Convention]

### Anatomy

```
Error summary (multi-field forms only)          Field in error
+--------------------------------------+        |  Reason                       <label>
| There is a problem              <h2> |        |  Up to 200 characters         hint
|                                      |        |  Enter a reason for the flag  message (bold, error colour)
|  Write your notes in the Guided      |        |  +------------------------+   input (thicker error-colour border)
|  notes box. The grey text is only    |        |  |                        |
|  a guide.                    <a>     |        |  +------------------------+
|  Enter a reason for the flag <a>     |        ^ 4px error-colour bar on the left of the whole group
+--------------------------------------+
4px error-colour border, tabindex="-1"
```

- **Summary:** the first thing in `<main>`, above the `<h1>`. A container with `tabIndex={-1}`, an `<h2>` "There is a problem", and a `<ul>` with one `<a href="#fieldId">` per error, in page order. Field errors only: a request failure that belongs to no field goes in the `role="alert"` above the submit button instead (Request failures, below). The one unlinked item in the app is sign-in's A25 message.
- **Field group:** label, then hint (if any), then message, then input. The message is `<p id="{fieldId}-error">` with a visually hidden "Error: " prefix. The input carries `aria-describedby="{hintId} {errorId}"` and `aria-invalid="true"`. The group gets a 4px error-colour left border. The message sits **above** the input so the phone keyboard, which covers the lower half of the screen, never hides it.
- **Groups of inputs** (radio sets, a three-part date) put the message under the `<legend>` inside the `<fieldset>`, and the summary links to the first input that is in error.

### Behaviour

**Submit with errors (multi-field form):**
1. Run the client checks on trimmed values. Never trim a password.
2. Render the inline errors and the summary in the same update.
3. Move focus to the summary. Do this on **every** failed attempt, even if the errors are unchanged, so a screen-reader user knows the tap did something.
4. Prefix `document.title` with "Error: ", for example "Error: Grow2Notes – Note". Remove the prefix when the last error clears.
5. Nothing is sent. On the note form, the confirmation dialog does not open.

**Selecting a summary link:** call `preventDefault()`, scroll the field's `<label>` (or `<legend>`) into view, then `focus({ preventScroll: true })` the input. The URL does not change: no hash and no history entry (design 4.0 keeps URLs to IDs and dates). On iOS this happens inside the tap, so the keyboard opens ready to type.

**Fixing:** each input in error re-validates on `input`. When it passes, its message, `aria-invalid` and summary item are removed silently. When the summary is empty it disappears and the title prefix goes. The summary never re-orders or takes focus while the user types.

**Submit with errors (one-field form):** show the inline error, set `aria-invalid`, and focus the input. Its `aria-describedby` makes the screen reader read the label, "invalid", and "Error: …".

**Server responses after a submit that passed the client checks:**

| Response | Where it shows | Focus |
|---|---|---|
| `422 validation.failed` with `errors{}` | Mapped to the matching fields, exactly like a client error. A key with no visible field is a client bug: show the "something went wrong" alert line above the button. | Summary, or the field on a one-field form |
| Known `409` codes tied to a field (`user.email_in_use`, `user.last_manager`) | The field it concerns (table below) | Summary |
| `412 precondition.failed` (config forms) | The `role="alert"` above the submit button, not the summary (it belongs to no field). Re-fetch the record for a fresh ETag, but **keep the typed values** on screen. | Stays on the button |
| `401` at sign-in or the code step, and `429` there | Summary box with the design's generic sign-in message, no link (A25; the one unlinked summary item) | Summary |
| `401` elsewhere (session ended) | Not a validation error. The session flow in §8.5 applies, and typed values stay in memory. | n/a |
| Network failure or `5xx` | The `role="alert"` above the submit button, in microcopy.md §9's words: "Not [done]: no connection. Try again." or "Not [done]: something went wrong. Try again." Not the summary: the fix is one more press of the focused button, and moving focus to the top of a phone page scrolls away from it. | Stays on the button |
| Note-form conflicts (`note.version_conflict`, `draft.taken_over`, `note.lists_changed`, the after-midnight `422`) | **Not** in the error summary. They use the banners and copy already in design.md §3.3, §4.3 and §5.6. | Per the banner component |

**Retries (editorial pass):** mutations use `retry: 0`, set as the `QueryClient`'s mutation default (TanStack's own
default is no retry). No create or state-changing `POST` in design §6.6 has an idempotency key except the
note-version `POST`, so a silent retry after a lost response could create a second participant, goal or common item.
The person retries by pressing the focused button again. The one change sent again with no press is one answered `400`
with no `code`, as the `/api` filter refuses a missing or stale antiforgery token before any handler runs: the `api()`
wrapper sends it once more with a fresh token, so nothing can be done twice (empty-loading-error.md *Timing*). (This
file first proposed `retry: (n, e) => isRetryable(e) && n < 3`; participants.md, common-items.md and users.md showed
why that is unsafe.)

### States

| State | Summary | Field |
|---|---|---|
| **Default** | Not rendered | No error styling, no `aria-invalid`, hint (if any) only |
| **Hover** | Links: underline thickens (`text-decoration-thickness: 3px`); colour unchanged | Unchanged. Errors are not hover-revealed. |
| **Focus** | A programmatically focused summary shows the app's focus outline (≥ 3:1, `outline`, never only `box-shadow`). Links use the standard link focus style. | Focus outline **plus** the error border, both visible |
| **Active** | Links: standard pressed style | — |
| **Disabled** | Errors never appear on disabled controls. The note form's Submit while unsaved is `aria-disabled` (see implementation notes) and runs no checks. | — |
| **Error** | Heading, linked list, 4px error-colour border | Message above input, 4px left bar, thicker error border on the input, `aria-invalid="true"` |
| **Loading** (request in flight) | The previous attempt's summary stays until the response arrives, then is replaced or removed | Values stay editable and are never cleared |
| **Empty** | No errors means no summary and no title prefix | — |
| **Read-only** | Never lists read-only values | Never styled as an error |

### Phone vs laptop

- **Phone (single column):** the summary is full width at the top of `<main>`. Each link is a block with at least 44 px of tappable height (design 4.0). Field groups get `scroll-margin-top` equal to the sticky top bar's height plus spacing, so a scrolled-to label is never under the bar (WCAG 2.4.11).
- **Laptop:** same order and behaviour. The summary is as wide as the form column, not the page, so the line length stays readable. Nothing moves into a side panel or a tooltip.

### Exact copy

The summary heading is always **"There is a problem"**. Every summary link has exactly the same text as its inline message. Copy marked *proposed* is not in design.md: it follows the GOV.UK templates above and is graded [Convention] unless marked [Opinion].

**Note form (4.3)**

| Field (`id` / API key) | Rule | When | Message |
|---|---|---|---|
| Guided notes (`narrative`) | Not blank after trimming (server: "not blank", §5.8) | Submit, Save changes | *Proposed:* "Write your notes in the Guided notes box. The grey text is only a guide." Add the second sentence only when guide prompts exist. [Opinion: it names the placeholder confusion that NN/g documents] |
| Guided notes | ≤ 20,000 characters (A6) | While typing, and Submit | *Proposed:* "Guided notes must be 20,000 characters or less. You have [n] characters too many." |
| Reason (`flagReason`, shown only when Flag is ticked) | Not blank after trimming | Submit, Save changes | *Proposed:* "Enter a reason for the flag" |
| Reason | ≤ 200 characters (A6) | While typing, and Submit | *Proposed:* "Reason must be 200 characters or less. You have [n] characters too many." |
| Reason hint | — | Always | *Proposed:* "Up to 200 characters" (the limit stated in design 4.3) |
| Save indicator when a length error blocks autosave | — | — | **Not used** (note-form.md Conflicts #1): autosave keeps saving a capped copy, and the error shows only on Submit. |
| Submit request failed (network or 5xx) inside the confirmation dialog | — | After "Submit note for Jane Citizen" | *Proposed:* "Not submitted: no connection. Your draft is saved. Try again." This is true because Submit requires a saved draft (§5.8). [Opinion] |

Unticking Flag for manager removes the Reason field and its error together.

**Sign-in (4.1)**

| Field | Rule | Message |
|---|---|---|
| Email | Not blank | *Proposed:* "Enter your email address" |
| Password | Not blank (not trimmed) | *Proposed:* "Enter your password" |
| Any server failure (`401`, `429`) | — | **design.md:** "Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes." Shown as an unlinked summary item. No field is marked invalid, because marking one would say which part was wrong (A25). |
| 6-digit code (one-field form) | 6 digits after removing spaces | *Proposed:* "Enter the 6-digit code from your authenticator app" |
| Code rejected (`401`) | — | design.md generic message in the summary box. Clear the code field; keep everything else. |

Empty-field messages reveal nothing about the account, so they are compatible with A25. [Opinion]

**Account setup, password path (4.1)**

| Field | Rule | Message |
|---|---|---|
| Password | Not blank | *Proposed:* "Enter a password" |
| Password | ≥ 12 characters. Count with `value.length` to match ASP.NET Identity's `RequiredLength` (§8.4). No other rules. | *Proposed:* "Password must be 12 characters or more" |
| Code | 6 digits after removing spaces | *Proposed:* "Enter the 6-digit code from your authenticator app" |
| Code rejected by the server | — | *Proposed:* "That code did not work. Enter the new 6-digit code from your authenticator app." The password stays filled in and the code is cleared. [Opinion] |
| Enrolment cookie expired (`401`) | — | **design.md:** "Your setup session timed out. Open the link from your email again." This is a page state, not a form error. |
| Link expired, used or replaced (`410`) | — | **design.md:** "This link has expired. Ask a manager to send a new one." Page state. |

**Participant detail (4.8)**

| Field | Rule | Message |
|---|---|---|
| Given name | Not blank | *Proposed:* "Enter the given name" |
| Given name | ≤ 100 | *Proposed:* "Given name must be 100 characters or less" |
| Family name | Not blank / ≤ 100 | *Proposed:* "Enter the family name" / "Family name must be 100 characters or less" |
| Date of birth | Present and complete | *Proposed:* "Enter the date of birth" (if the control has three parts and one is missing: "Date of birth must include a [day/month/year]") |
| Date of birth | A real date | *Proposed:* "Date of birth must be a real date" |
| Date of birth | Before today (Melbourne) | *Proposed:* "Date of birth must be in the past" |
| Stale save (`412`) | — | *Proposed:* "Someone else changed these details while you were editing. Check the details below, then save again." [Opinion] |
| Goal (Add or Edit, one field) | Not blank / ≤ 200 | *Proposed:* "Enter the goal" / "Goal must be 200 characters or less". Hint "Up to 200 characters" (design: "takes up to 200 characters"). |
| Write past-day note date (one field) | Present and complete; before today | *Proposed:* "Enter the note date" / "Note date must be in the past" (design: "allowing past dates only") |

**Common items (4.9)**

| Field | Rule | Message |
|---|---|---|
| Item (Add or Edit, one field) | Not blank / ≤ 200 | *Proposed:* "Enter the common item" / "Common item must be 200 characters or less". Hint "Up to 200 characters". |
| New group (one field) | Not blank / ≤ 200 (A6) | "Enter the group name" / "Group name must be 200 characters or less" (design §4.9) |
| Group name (rename, one field) | Not blank / ≤ 200 (A6) | "Enter the group name" / "Group name must be 200 characters or less" (design §4.9) |

**Users (4.11)**

| Field | Rule | Message |
|---|---|---|
| Name | Not blank / ≤ 100 | *Proposed:* "Enter the person's name" / "Name must be 100 characters or less" |
| Email | Not blank | *Proposed:* "Enter an email address" |
| Email | Text, one "@", text (loose check; server decides) | **GOV.UK:** "Enter an email address in the correct format, like name@example.com" |
| Email | `409 user.email_in_use` | *Proposed:* "Another account already uses this email address. Use a different one, or find the person in the users list." |
| Role | One chosen (users.md pre-selects Worker, the one stated exception to the no-preselection rule, so this is reached only through a server `422`) | *Proposed:* "Choose Worker or Manager" |
| Role (changed to Worker) | `409 user.last_manager` | *Proposed:* "[Name] is the only active manager. Make someone else a manager first." |
| Email (changed on the last manager: an email change resets sign-in, which A26 forbids) | `409 user.last_manager` | *Proposed:* "[Name] is the only active manager, so their email can't be changed here. Make someone else a manager first." |
| Stale save (`412`) | — | *Proposed:* "Someone else changed this user while you were editing. Check the details, then save again." In the alert above Save, not the summary. |

**Participant record export (4.12)**

| Field | Rule | Message |
|---|---|---|
| From date | Present and complete | *Proposed:* "Enter the From date" |
| To date | Present and complete | *Proposed:* "Enter the To date" |
| To date | Same as or after From | *Proposed (GOV.UK template):* "To date must be the same as or after [From date]", for example "…after Thursday 1 October 2026". The error goes on **To date**, as design 4.12 says. |
| Format | One chosen (only if neither starts selected) | *Proposed:* "Choose PDF or Word" |
| No notes in range | — | **design.md:** "No submitted notes for Jane Citizen between … and …". A **status** message near Export, not an error: no red, no summary, no focus move. It is announced through a persistent `role="status"` region. |

### Accessibility

**Semantics.** Native `<form noValidate>`, `<label for>`, `<fieldset>`/`<legend>` for groups, `<h2>` in the summary, and `<ul>`/`<a>` for its items. ARIA is used only for `aria-describedby` (hint and error) and `aria-invalid="true"` on fields in error after an attempt. No `aria-errormessage`, which has partial support. No `aria-live` on inline errors, which clips the next field's name (Roselli).

**Summary announcement.** Moving focus does the announcing: the container has `tabIndex={-1}`, and VoiceOver, NVDA and JAWS read the focused container's heading and list. GOV.UK also nests a `role="alert"`. Its pages are server-rendered, so the alert is there at load and focus does the work. In this SPA the summary is inserted and focused in the same moment, and an alert as well risks the content being read twice. It is left out. **[Opinion: verify with NVDA + Chrome and VoiceOver on iOS before M2 closes.]**

**Keyboard.** Tab reaches every summary link. Enter on a link focuses its field. Enter in a single-line input submits its form (implicit submission). On the note form that only opens the confirmation dialog, which names the participant, so nothing is sent by accident. The submit handler checks the "latest change saved" rule itself, because `aria-disabled` does not stop implicit submission.

**Announcements (expected; exact wording varies by screen reader):**
- A failed submit on a multi-field form reads "There is a problem, heading level 2", the list, then each link's text.
- Following a summary link reads "Reason, edit, invalid entry, Up to 200 characters, Error: Enter a reason for the flag".
- A failed submit on a one-field form reads the same as following a link.
- Crossing a length limit while typing (note form only) writes the message **once** into the app's persistent, visually hidden `role="status"` region, which must exist in the DOM from page load. Falling back under the limit is silent; the save indicator then returns to "Saving…" / "Saved".
- The export "No submitted notes…" result goes through the same persistent `role="status"` region.

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships · 1.4.1 Use of Color (text plus a position cue as well as colour) · 1.4.3 Contrast (error text ≥ 4.5:1) · 1.4.11 Non-text Contrast (error borders and focus ≥ 3:1) · 2.1.1 Keyboard · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.8 Target Size (Minimum), exceeded at 44 px · 3.2.2 On Input (no auto-submit) · 3.3.1 Error Identification · 3.3.2 Labels or Instructions (limit hints, "6-digit code") · 3.3.3 Error Suggestion · 3.3.4 Error Prevention (submit is checked, then confirmed) · 3.3.7 Redundant Entry · 3.3.8 Accessible Authentication (paste allowed, `autocomplete="one-time-code"`, passkeys) · 4.1.2 Name, Role, Value (`aria-invalid`) · 4.1.3 Status Messages (length and export status).

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No form library and no `<form action>`.** React 19 resets uncontrolled fields after a form action "succeeds", and that includes an action that returned errors. Use `<form noValidate onSubmit>` with `event.preventDefault()`. Keep values in state (the note form is controlled already because of autosave), and call the TanStack Query mutation from the handler. [Standard: React docs]
- **One `limits.ts`** mirrors A6 (200, 200, 100, 20,000, 12). A unit test compares it with the API's OpenAPI `maxLength` values, so the client cannot drift from the server's constants class (§5.1). [Opinion]
- **Field IDs equal API keys,** with a prefix per form where a page has several forms (`details-givenName`, `goal-new-text`). Mapping a `422` is then a lookup. ASP.NET Core 10's built-in validation must be configured so that (a) `errors{}` keys are the camelCase JSON names and (b) the messages are the plain-English strings above, never "The GivenName field is required." **(Unverified: check both in M0.)**
- **No `maxLength`** on Reason, Guided notes, goals, items, names or the code field. It silently truncates pasted and dictated text, and turns a pasted "123 456" into "123 45". Check lengths in code instead. [Convention: GOV.UK character count]
- **6-digit code:** `type="text" inputMode="numeric" autoComplete="one-time-code"`. Strip spaces before checking or sending. Never `type="number"`, which drops leading zeros.
- **Fields about other people** (participant names, date of birth, invitee name and email) use `autoComplete="off"`, so the browser never fills in the manager's own details. 1.3.5 does not apply to them. [Opinion]
- **Note form Submit while unsaved** (design: disabled). Render `aria-disabled="true"` instead of `disabled`, so the button keeps focus and stays findable. Point its `aria-describedby` at the save indicator, so a screen-reader user hears "Saving…" or "Not saved…". Guard `onSubmit` too.
- **Do not style `:invalid` or `:user-invalid`.** With no `required` attribute and `noValidate` they add nothing, and `:invalid` can match before any interaction. Style from `[aria-invalid="true"]` and a `data-invalid` attribute on the group, so the visual state and the announced state are always the same state.
- **Forced colours:** the error borders are real borders, so they survive. Keep the error border width difference (4px bar, 3px input border) so the error is visible as **shape** when colour is gone.

```tsx
// ErrorSummary.tsx
import { useEffect, useRef, type MouseEvent } from 'react';
import s from './ErrorSummary.module.css';

export type FormError = { fieldId?: string; message: string };

function jumpTo(e: MouseEvent<HTMLAnchorElement>, id: string) {
  const field = document.getElementById(id);
  if (!field) return;
  e.preventDefault();                                   // no hash, no history entry
  const cue = document.querySelector(`label[for="${id}"]`) ?? field.closest('fieldset')?.querySelector('legend');
  (cue ?? field).scrollIntoView({ block: 'start' });    // honours scroll-margin-top
  field.focus({ preventScroll: true });
}

/** `attempt` increments on every submit; focus moves only then, never while typing. */
export function ErrorSummary({ errors, attempt }: { errors: FormError[]; attempt: number }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => { if (errors.length) ref.current?.focus(); }, [attempt]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    const base = document.title.replace(/^Error: /, '');
    document.title = errors.length ? `Error: ${base}` : base;
  }, [errors.length]);
  if (!errors.length) return null;
  return (
    <div ref={ref} tabIndex={-1} className={s.summary}>
      <h2 className={s.title}>There is a problem</h2>
      <ul className={s.list}>
        {errors.map((x) => (
          <li key={x.fieldId ?? x.message}>
            {x.fieldId ? <a href={`#${x.fieldId}`} onClick={(e) => jumpTo(e, x.fieldId!)}>{x.message}</a> : x.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
```

```tsx
// TextField.tsx (the text-field component owns the visuals; this shows only the error wiring)
export function TextField({ id, label, hint, error, ...input }: Props) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  return (
    <div className={s.group} data-invalid={error ? '' : undefined}>
      <label htmlFor={id} className={s.label}>{label}</label>
      {hint && <p id={hintId} className={s.hint}>{hint}</p>}
      {error && <p id={errorId} className={s.message}><span className="visually-hidden">Error: </span>{error}</p>}
      <input id={id} {...input}
        aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
        aria-invalid={error ? true : undefined} />
    </div>
  );
}
```

```tsx
// NoteForm submit (sketch)
function onSubmit(e: React.FormEvent) {
  e.preventDefault();
  if (!isLatestSaved) return;                    // design 3.4: Submit inactive until saved
  const found = validateNote(form, { hasPrompts: guidePrompts.trim() !== '' });
  setErrors(found);
  setAttempt((n) => n + 1);
  if (found.length === 0) confirmRef.current?.showModal();   // confirmation only when valid
}
// on a 422 from POST {base}/versions: confirmRef.current?.close(); setErrors(fromProblem(err)); setAttempt(n => n + 1);
```

```css
/* ErrorSummary.module.css (tokens come from the app's theme) */
.summary { border: 4px solid var(--colour-error); padding: var(--space-4); margin-block-end: var(--space-6); max-inline-size: var(--form-max-width); }
.summary:focus-visible, .summary:focus { outline: 3px solid var(--colour-focus); outline-offset: 2px; }
.title { margin: 0 0 var(--space-3); font-size: var(--text-lg); }
.list { margin: 0; padding: 0; list-style: none; }
.list a { display: block; min-block-size: 44px; padding-block: var(--space-2); color: var(--colour-error-text); font-weight: 700; }
.list a:hover { text-decoration-thickness: 3px; }
/* TextField.module.css */
.group { scroll-margin-top: calc(var(--top-bar-height) + var(--space-4)); }
.group[data-invalid] { border-inline-start: 4px solid var(--colour-error); padding-inline-start: var(--space-3); }
.message { margin: 0 0 var(--space-2); color: var(--colour-error-text); font-weight: 700; }
.group[data-invalid] :is(input, textarea)[aria-invalid="true"] { border: 3px solid var(--colour-error); }
```

---

## Per-screen notes

**4.3 Note form.**
- Order of checks: Guided notes, then Reason, which is also page order.
- The summary is the first child of `<main>`, above the participant's name. The name stays directly below it, so the "right person" context is never lost.
- Checks run on Submit note and on Save changes (editing a submitted note). The confirmation dialog opens only when there are no errors.
- A `422` from `POST …/versions` closes the dialog and shows the errors on the form. A network failure keeps the dialog open with the proposed "Not submitted…" message focused, and Idempotency-Key makes the retry safe.
- Autosave never produces validation messages, except the length rule. A draft may legitimately be empty.
- ~~While a field is over its limit, the client does not send the autosave.~~ Superseded by note-form.md Conflicts #1: autosave keeps saving a copy capped at the limit, the full text stays on screen, and the error shows only on Submit. "Not saved: fix the error below." is not used.
- Whitespace-only Guided notes count as empty, matching the server's "not blank".

**4.1 Sign-in and setup.**
- The password step has two fields, so it gets a summary. The code step is one field, so it gets an inline error and focus.
- A credential failure never marks a field invalid. The generic A25 message goes in the summary box as an unlinked item (OWASP). Keep the email; clear the password (sign-in.md Conflicts #1). Network and server failures go in the alert above the pressed button, not the summary (sign-in.md).
- Setup sends password and code together. A wrong code keeps the password filled in and clears the code. Expired-link and timed-out-session messages are page states with their design.md copy, not form errors.
- The passkey path has nothing to validate. A cancelled passkey prompt is not a form error.

**4.8 Participant detail.**
- Three independent forms on one page. Details has a summary at the top of `<main>`, above the participant's name (the app-wide position; participants.md B4), with field errors only; its request failures go in the alert above Save. Add goal, Edit goal and the past-day date are one-field forms: inline error, focus the field, no page-top summary that would scroll the manager away from the goal list.
- If date of birth is built as three inputs (the GOV.UK convention for a remembered date; the control is the date component's decision), the summary links to the first part in error, and only the parts in error get `aria-invalid`.
- An archived participant's read-only view has no validation.

**4.9 Common items.**
- One-field pattern only: no summary. The same pattern and wording as goals, for Add item, Edit item, New group and the group rename.
- The item Edit form also holds the **Group** radios. They can't be in error, because one is always selected, so the one-field pattern still applies to its text.

**4.11 Users.**
- The invite form gets a summary. If the form is built in a native `<dialog>`, the summary goes at the top of the dialog body and focus moves there; the rest is unchanged.
- The `409` codes map to Email or Role as in the copy table.
- Deactivate, Reset sign-in and Resend invite are confirmation dialogs, not forms. Their `409 user.last_manager` uses the same wording, shown inside the dialog.

**4.12 Participant record export.**
- To-before-From is checked on Export, never on change, because the manager may be about to change From. Once To date is in error, changing **either** date re-checks it.
- With native date inputs, an incomplete date returns `""`, so "incomplete" and "empty" share the "Enter the … date" message.
- "No submitted notes…" is a status, not an error.

---

## Anti-patterns to avoid

- Errors on load, on focus, on blur or during first typing ("before and while" did worst in the Etre test).
- `aria-invalid="true"` on every required field before an attempt (ARIA says SHOULD NOT).
- Native HTML5 bubbles. Use `noValidate`; bubbles can't be styled, worded or kept on screen.
- `maxlength` on text that people paste or dictate. It truncates silently.
- Clearing fields after an error, including through React 19's `<form action>` auto-reset. The only exception is a rejected 6-digit code.
- Showing a field-specific error at sign-in ("wrong password", "no account with that email"), which breaks A25 and OWASP.
- A summary with no inline messages, or inline messages with no summary on a multi-field form (design 4.3 requires both). Summary text that differs from the inline text.
- Raw server or framework text: "The GivenName field is required.", "validation.failed", "422". Also "Please", "Sorry", "Oops", "Invalid", "This field is required".
- Error toasts, or auto-dismissing messages (SC 2.2.1; a toast is gone before a tired user reads it).
- `aria-errormessage` on its own, or `aria-live="assertive"` on inline errors.
- Relying on a conditionally rendered `role="alert"` for the announcement instead of moving focus.
- The `disabled` attribute on Submit, which removes it from the focus order with no explanation. Also: forgetting that Enter still submits a form whose button is only `aria-disabled`.
- Summary links that change the hash, add history entries, or scroll the input under the sticky top bar.
- An error message below a tall textarea, where the phone keyboard hides it.
- Opening the submit confirmation dialog when there are errors.
- Retrying a `422`. `type="number"` for the code. Trimming passwords. Letting autofill put the manager's own name or birthday into a participant's record.
- Adding what the design did not ask for: green "success" ticks, live counters on every field, validation settings.

---

## Tensions with decisions

1. **Submit disabled while unsaved** (design.md §3.4 and §4.3). GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible" [Convention]. Adam Silver documents the same failures (not focusable, no feedback about why) [Convention, practitioner]. https://adamsilver.io/blog/the-problem-with-disabled-buttons-and-what-to-do-instead/ The design's reason is sound (submit must carry saved content, §5.8). The implementation above keeps the behaviour and limits the cost with `aria-disabled` and a description pointing at the save indicator. No change is proposed.
2. **Guide prompts as placeholder text** (D12, D34; contrast raised to 4.5:1 in 4.10). NN/g: users "may mistake a placeholder for data that was automatically filled in" and "skip the field completely" [Research, eye-tracking and usability observation]. Raising the contrast makes the prompts look even more like real text. For this component the risk is a worker thinking Guided notes is already filled. The proposed empty-box message ("…The grey text is only a guide.") answers exactly that misunderstanding. No change is proposed.

---

## Sources

- Bargas-Avila et al. (2007), *Interacting with Computers* 19(3):330–341: https://academic.oup.com/iwc/article-abstract/19/3/330/693000
- Wroblewski and Etre (2009), "Inline Validation in Web Forms", A List Apart: https://alistapart.com/article/inline-validation-in-web-forms/
- Baymard Institute, "Usability Testing of Inline Form Validation": https://baymard.com/blog/inline-form-validation
- GOV.UK Design System, Recover from validation errors: https://design-system.service.gov.uk/patterns/validation/
- GOV.UK Design System, Error summary: https://design-system.service.gov.uk/components/error-summary/
- GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- GOV.UK Design System, Date input: https://design-system.service.gov.uk/components/date-input/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- GOV.UK Design System, Email addresses: https://design-system.service.gov.uk/patterns/email-addresses/
- GOV.UK Design System, Button (disabled buttons): https://design-system.service.gov.uk/components/button/
- govuk-frontend error-summary script: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/error-summary/error-summary.mjs
- NHS digital service manual, Error summary: https://service-manual.nhs.uk/design-system/components/error-summary
- AgDS, Accessible form validation and error recovery: https://design-system.agriculture.gov.au/patterns/accessible-form-validation-and-recovery (design-system.gov.au was unreachable during research)
- AgDS, Page alert: https://design-system.agriculture.gov.au/components/page-alert
- NSW Design System, Form: https://designsystem.nsw.gov.au/components/form/index.html
- NN/g, "10 Design Guidelines for Reporting Errors in Forms" (Krause, 2019, reviewed 2024): https://www.nngroup.com/articles/errors-forms-design-guidelines/
- NN/g, "Placeholders in Form Fields Are Harmful" (Sherwin, 2014, reviewed 2018): https://www.nngroup.com/articles/form-design-placeholders/
- Adrian Roselli, "Exposing Field Errors" (2023): https://adrianroselli.com/2023/04/exposing-field-errors.html
- a11ysupport.io, `aria-errormessage`: https://a11ysupport.io/tech/aria/aria-errormessage_attribute
- WAI-ARIA, `aria-invalid`: https://w3c.github.io/aria/#aria-invalid
- W3C WAI Forms tutorial, User notifications: https://www.w3.org/WAI/tutorials/forms/notifications/
- WCAG 2.2 Understanding: 3.3.1 https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html · 3.3.3 https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html · 1.4.1 https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · 3.3.7 https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html · 3.3.8 https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html · 1.3.5 https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html · 2.4.11 https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- NIST SP 800-63B-4: https://pages.nist.gov/800-63-4/sp800-63b.html
- OWASP Authentication Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- React docs, `<form>`: https://react.dev/reference/react-dom/components/form · react#29034: https://github.com/react/react/issues/29034
- Adam Silver, "The problem with disabled buttons": https://adamsilver.io/blog/the-problem-with-disabled-buttons-and-what-to-do-instead/
