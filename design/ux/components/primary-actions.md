# Buttons and action hierarchy

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> The editorial pass of 1 October 2026 settled these app-wide rules here (they replace older values below):
> - **Two-tier busy timing:** a page button swaps to its busy label (the button's own verb + "…") in the same grid
>   cell after **400 ms**; a confirmation dialog keeps its labels and shows a status line after **1 s**
>   (confirm-dialog.md). One Button, one timing on every screen.
> - **Where busy labels are announced:** each page renders one visually hidden `<p role="status">` (`PageStatus`,
>   app-shell.md component 3). The shared Button finds the nearest one through React context and writes its busy
>   label there. A screen with its own status line opts out per button with `announceBusy={false}` (Export record,
>   Daily report downloads, Guide prompts' Save). There is no app-level region.
> - **Request failures:** a failure that belongs to no field (no connection, server error, `412`, a rate limit)
>   goes in the always-present `role="alert"` directly above the pressed button, and focus stays on that button.
>   The error summary holds field errors only (form-validation.md), plus sign-in's A25 credential message.
> - **Mutations default to `retry: 0`** (TanStack's own default, set explicitly). Nothing is retried automatically
>   unless repeating it is safe: autosave `PUT`s (`clientId` + `seq`) and the note-version `POST`, whose
>   `Idempotency-Key` is reused when the person presses again.
> - **Two warning (red) buttons in the whole app:** the final "Discard draft for [name]" and "Reset sign-in for
>   [name]". Deactivate's confirm is the standard style (Reactivate undoes it).
> - **Sizes:** buttons `min-block-size: max(3rem, 48px)` (`--target-button`, foundations.md); breakpoint `40rem`.
> - **No success banners** except "Note for [name] submitted" after Submit; elsewhere focus moves to the `<h1>` or to
>   the changed state (5.4).

Covers every button and button-like link on the screens below: what each one looks like, where it sits, what it says, how it behaves while unavailable or busy, and how it is built. It builds what design.md section 4 already specifies. It adds no actions, screens or settings.

**Copy conventions in this file.** Text in "quotes" with no mark is copied word for word from design.md. Text marked **(proposed)** is not in design.md. It fills a gap the design leaves, such as a busy label or an error after a failed request, and the owner should confirm it (see "Open questions" at the end of Tensions). Participant and user names in examples are made up.

---

## Where it's used

| Screen | Actions | What differs on this screen |
|---|---|---|
| **4.1 Sign-in and account setup** | "Sign in with a passkey"; email and password, then the 6-digit code ("Sign in"); setup: "Use a passkey (recommended)", "Use a password and authenticator app", "Finish setup" | Two sign-in paths on one screen, and each person uses only one (A22). A passkey button starts a device prompt, not a form post. One generic failure message (A25). No back-out actions. |
| **4.3 Note form and submit confirmation** | "Submit note"; a menu holding "Discard draft"; the confirmation's "Submit note for Jane Citizen" and "Go back"; edit mode "Save changes" and "Cancel"; banner actions "Keep the text on this screen", "Load the other version", "Start again from the latest version" (6.3) | The only screen where the design **disables** a primary button for a reason the user can't see directly (an unsaved change). It has the most-used commit action in the app, a two-step commit (button, then confirmation naming the person), and the only destructive action a worker can take (Discard draft). Used on phones by tired workers. |
| **4.4 Participant notes and read view** (and the 4.6 review panel) | "Edit"; "Mark reviewed" (with optional comment); "Show older"; managers: "Write past-day note", "Export record", "Edit participant", "Version history"; manager on someone else's draft: Discard | Mostly reading, so most actions are low-emphasis. What shows depends on role and authorship: hide actions this person can never take. Mark reviewed removes itself after it succeeds. |
| **4.7 Daily report** | "Previous day", "Next day", "Download Word", "Download PDF" | Two equal primary actions (one file in two formats). Downloads, not page changes. The design disables both downloads on an empty day, and Next day on today. |
| **4.10 Guide prompts** | "Save"; the "leaving with unsaved changes" warning | One field and one Save. Needs a saved confirmation and an unsaved-changes guard. |
| **4.11 Users** | "Invite user", "Send invite", "Edit", "Resend invite", "Reset sign-in", "Deactivate", "Reactivate" | The serious manager-only actions. Deactivate has a specified confirmation; Reset sign-in can't be undone. Routine and serious actions sit on the same detail page. |

---

## Best practice

### Hierarchy and styling

- **One filled "primary" style for the main action, an outlined "secondary" style for the rest, and a red "warning" style kept for serious destructive actions.** GOV.UK: "Use a default button for the main call to action on a page" and "Avoid using multiple default buttons on a single page." NHS: "Use only 1 primary button on a page." AgDS describes primary, secondary and tertiary as steps down in prominence. [Convention] https://design-system.service.gov.uk/components/button/ · https://service-manual.nhs.uk/design-system/components/buttons · https://design-system.agriculture.gov.au/components/button
- **A Cancel next to a Submit must look clearly less important.** NN/g: "make sure that the Cancel button has significantly less visual prominence than the Submit button, to avoid accidental clicks." [Research] https://www.nngroup.com/articles/web-form-design/
- **Keep red for actions that are serious and hard to undo, and never rely on the red alone.** GOV.UK: "Only use warning buttons for actions with serious destructive consequences that cannot be easily undone by a user", and "Do not only rely on the red colour of a warning button to communicate the serious nature of the action." [Convention] https://design-system.service.gov.uk/components/button/ (WCAG 1.4.1 Use of Color [Standard] https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html)
- **"One primary per page" is a convention, not a law.** The ui-ux-design corpus lists it among rules that dashboards and healthcare tools refute. Where two actions really are equal (Download Word / Download PDF), giving them equal weight is defensible. [Opinion]

### Placement: sticky bottom bar or inline

- **Sticky bars cost space, and the cost is worst on phones.** NN/g: "Sticky headers inherently take up space on the screen that could be used for content", and when done badly they "obstruct page content." [Research] https://www.nngroup.com/articles/sticky-headers/
- **On today's phone browsers, a fixed bottom bar can end up under the on-screen keyboard.** Chrome on Android 108+ matches iOS Safari: "Elements that use `position: fixed` remain in place and can be obscured by the OSK." [Standard: platform documentation] https://developer.chrome.com/blog/viewport-resize-behavior
- **Sticky footers are the classic way to fail WCAG 2.2 SC 2.4.11 Focus Not Obscured (Minimum).** The Understanding doc names "sticky footers" and gives `scroll-padding` (technique C43) as the fix. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **The evidence for sticky buttons comes from shopping pages, where the action doesn't depend on finishing what's above it.** One e-commerce A/B test (supplement store product pages, about 3,000 conversions per variant on mobile) found that a sticky button that scrolled to the cart area made **no significant difference**. Only a sticky button that opened a slide-up drawer won (+5.2% orders). [Research, vendor-published, e-commerce] https://growthrock.co/sticky-add-to-cart-button-example/
- **Government form patterns put the submit button inline, after the last question, and validate when it is pressed.** "Wait until they try to move to the next part of the service - usually by clicking the 'continue' or 'submit' button at the bottom of the page." [Convention, used across live services] https://design-system.service.gov.uk/patterns/validation/
- **Line buttons up on the left, from most to least important.** AgDS: "right aligned buttons can be missed, especially on large screens or for screen magnifier users." On mobile, "buttons will be stacked on top of one another and span the full width of a screen." GOV.UK Frontend makes buttons `width: 100%` below its 641 px tablet breakpoint and `width: auto` above it. [Convention] https://design-system.agriculture.gov.au/components/button · https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/button/_mixin.scss
- **Put the primary action first.** GOV.UK button groups lead with the primary button. NN/g's web advice is "OK first, Cancel last" (2008, following the Windows convention). [Convention] https://design-system.service.gov.uk/components/button/ · https://www.nngroup.com/articles/ok-cancel-or-cancel-ok/

### Disabled buttons, or enabled with errors

- **Every government system consulted advises against disabled buttons.** GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible." NHS says the same. AgDS says they "don't tell users why the content is unavailable, are not keyboard accessible, can be hard for users with a visual impairment to see." [Convention] https://design-system.service.gov.uk/components/button/ · https://service-manual.nhs.uk/design-system/components/buttons · https://design-system.agriculture.gov.au/components/button
- **Observed behaviour: people who hit a disabled submit button go back through the form looking for the problem.** Friedman reports users clicking again and again, re-entering data and reloading. He recommends validating on submit, and using `aria-disabled` with an explanation when disabling can't be avoided. [Research, weak: practitioner observation, no published method] https://www.smashingmagazine.com/2021/08/frustrating-design-patterns-disabled-buttons/ · [Opinion] https://adrianroselli.com/2024/02/dont-disable-form-controls.html
- **A natively disabled button silently swallows the tap.** HTML: "A form control that is disabled must prevent any click events that are queued on the user interaction task source from being dispatched on the element." Browsers also take `disabled` controls out of the tab order. The APG supports keeping a control focusable with `aria-disabled="true"` "when discoverability is essential." [Standard] https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#enabling-and-disabling-form-controls:-the-disabled-attribute · https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- **Hide what this person can never do; show as unavailable what will become available.** "Disable if you want the user to know a feature exists but is unavailable, and hide if the value shown is currently irrelevant and can't be used." [Opinion, practitioner] https://www.smashingmagazine.com/2024/05/hidden-vs-disabled-ux/
- **To "disable" a link,** remove `href` and add `role="link"` and `aria-disabled="true"`. Screen readers then still announce it as a link, marked dimmed or unavailable. [Convention] https://www.scottohara.me/blog/2021/05/28/disabled-links.html

### Loading (busy) state

- **Timing limits:** about 0.1 s feels instant, 1 s keeps the flow of thought, and 10 s is the limit of attention (Nielsen 1993, partly expert judgement). [Research] https://www.nngroup.com/articles/response-times-3-important-limits/
- **Don't flash an indicator for a fast request.** Show it after a delay, about 400 ms. The exact number is a convention, not a measurement. [Convention] (ui-build corpus, 07-feedback-channels)
- **A busy button should stay focusable, ignore further presses, and announce its state.** React Aria's `isPending` "disables press and hover events while retaining focusability, and announces the pending state to screen readers." AgDS's loading button has a `loadingLabel` that should be made "more contextual." Never use `disabled` for this: it drops keyboard focus to `<body>`. [Convention] https://react-aria.adobe.com/Button · https://design-system.agriculture.gov.au/components/button
- **Stop double submits on the client and make them safe on the server.** GOV.UK (`data-prevent-double-click`) and NHS (`preventDoubleClick`) both block a second click, and both say the server must cope as well. [Convention] https://design-system.service.gov.uk/components/button/ · https://service-manual.nhs.uk/design-system/components/buttons
- **Status changes must be announced without moving focus** (SC 4.1.3 Status Messages, AA). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

### Labels

- **Labels are verbs, in sentence case, and name the thing acted on.** GOV.UK: sentence case, with examples "Sign in", "Continue", "Save and continue". AgDS: "a verb and a noun that makes the action obvious". NHS gives the same kind of examples. [Convention] https://design-system.service.gov.uk/components/button/ · https://design-system.agriculture.gov.au/components/button
- **In a confirmation dialog, the buttons should say what will happen,** not "Yes" or "OK": for example "Delete file" and "Keep file". Confirm only actions with serious consequences, because "if you cry wolf too many times, people will stop paying attention." [Research] https://www.nngroup.com/articles/confirmation-dialog/
- **The same function gets the same label everywhere** (SC 3.2.4 Consistent Identification). The visible label must start the accessible name (SC 2.5.3 Label in Name), so voice-control users can say what they see. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html · https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- **Use a button for an action and a link for going somewhere.** NSW: buttons when "there is an action performed", and text links to navigate. GOV.UK uses a link styled as a button for start buttons. [Convention] https://designsystem.nsw.gov.au/components/button/index.html

### Destructive actions and confirmation

- **Actions that delete or change stored data must be reversible, checked or confirmed** (SC 3.3.4 Error Prevention: Legal, Financial, Data, AA). One sufficient technique is "Requesting confirmation to continue with selected action." [Standard] https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- **Prefer reversible actions to confirmations,** and confirm only when an action is irreversible, costly and rare. [Research synthesis] (ui-ux-design corpus, 09-errors-recovery) · https://www.nngroup.com/articles/confirmation-dialog/
- **When a dialog is the last step of something hard to undo,** "it may be advisable to set focus on the least destructive action." Escape closes it, and focus goes back to whatever opened it, or to a sensible place if that is gone. [Standard] https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- **Avoid Cancel and Reset buttons on web forms unless people may fear they have committed to something.** "Offer a Cancel button when users may fear that they have committed to something they want to avoid." [Research, 2000] https://www.nngroup.com/articles/reset-and-cancel-buttons/

---

## Recommendation for Grow2Notes

### 1. The whole set: three styles and one rule for links

| Style | Looks like | Used for | Never used for |
|---|---|---|---|
| **Primary** | Solid fill, bold label | The action this screen exists for: Submit note, the confirmation's Submit note for [name], Save changes, Mark reviewed, Download Word / Download PDF, Save, Send invite, Invite user, Sign in with a passkey, Sign in, Finish setup | Anything destructive; Cancel; Go back |
| **Secondary** | Outline (2 px border), same height and font | Everything else: Cancel, Go back, Edit, Show older, Previous day, Next day, Resend invite, Reactivate, and the triggers for Discard draft, Deactivate and Reset sign-in | — |
| **Warning** | Solid red fill, white bold label, and a label that names the consequence | **Only** the final commit button inside **two** confirmation dialogs: "Discard draft for [name]" and "Reset sign-in for [name]" (proposed). Deactivate's confirm uses the standard style, because Reactivate undoes it (users.md, confirm-dialog.md) | Triggers on the page; Deactivate; Archive (it can be restored); Cancel; Go back; the Guide prompts leave dialog |

- **Links that look like buttons.** Edit, Invite user, Previous day, Next day, Write past-day note, Export record and Edit participant each go to another URL. Each is a React Router `<Link>` with the button class, so it is announced as a link, can open in a new tab, and has a working Back button. Everything that changes data, downloads a file or opens a dialog is a `<button>`.
- **No icon-only buttons and no tertiary (text-only) style.** Users have mixed digital confidence and many have English as a second language. Words beat symbols, and three styles are enough. [Opinion]
- **Download Word and Download PDF are both primary,** the same width, with Word first as design.md lists them. They are one action in two formats. Making one secondary would suggest the app prefers a format, which the design doesn't say. This knowingly breaks the "one primary" convention. [Opinion]

### 2. Anatomy

```
┌──────────────────────────────────────┐  ← 2 px border (transparent on primary/warning,
│        Submit note for Jane Citizen  │     visible on secondary). Min height 48 px.
└──────────────────────────────────────┘     Label: body size, bold, sentence case,
   ↑ focus ring: 3 px outline, 2 px offset     wraps onto 2+ lines, never truncated.
```

- **Box:** `min-block-size: var(--target-button)` (`max(3rem, 48px)`, foundations.md; 44 px is A32's floor) and the same `min-inline-size`. Padding is about 0.625rem by 1.25rem, and the radius is small. There is no fixed height, so labels can wrap at 200% text and under the SC 1.4.12 text-spacing overrides.
- **Label:** the body font at its normal size or bigger, weight 700, `line-height: 1.25`, centred. No uppercase transform. Don't uppercase participant names in buttons or dialogs either: show them large and bold instead. Some screen readers read capitals oddly. [Opinion]
- **Busy label:** a second label sits in the same grid cell, hidden. The button is always as wide as the longer of the two labels, so switching between them never moves anything (ui-build rule 14).
- **No spinner.** The busy label already says what is happening in words (invariant I1). Leaving out the spinner also leaves out motion and the reduced-motion work that comes with it. [Opinion]

### 3. Layout: phone and laptop

| | Phone (below 40rem) | Laptop (40rem and up) |
|---|---|---|
| Width | Full width of the content column | As wide as the label (`width: auto`), at least 8rem |
| Group | Stacked, primary on top, 1rem gap | In a row, left-aligned, primary on the left, 1rem gap, wrapping when space runs out |
| Position | **Inline, at the end of the content it acts on.** Nothing sticky or fixed, on any screen, including inside dialogs | Same |
| Destructive triggers | In their own group, separated from routine actions by 1.5rem and a heading or rule | Same |

Why nothing is sticky: the note form's Submit is meant to come after goals, items, notes and the flag, and validation runs when it is pressed. A fixed bar would sit under the keyboard while the worker types in Guided notes (Chrome and iOS behaviour above). It would also take a large share of the screen at 200% text, and could hide focused fields (SC 2.4.11). The only sticky-button evidence comes from shopping pages, and even there the plain scroll variant showed no effect. Dialogs scroll as a whole, and their buttons scroll with their content.

### 4. States

| State | When | Visual | Semantics and behaviour |
|---|---|---|---|
| **Default** | Available | Style per variant. Label ≥ 4.5:1 on its fill. Primary and warning fills ≥ 3:1 against the page; the secondary border ≥ 3:1 (SC 1.4.11) | Native `<button>` or `<Link>` |
| **Hover** | Pointer devices only: `@media (hover: hover) and (pointer: fine)` | Fill a step darker; secondary gets a faint tint | None. Hover never carries information |
| **Focus** | `:focus-visible` | `outline: 3px solid var(--focus-ring); outline-offset: 2px`, plus a contrasting inner band if one colour can't reach 3:1 against both the page and the fill. Appears instantly, with no transition | Never removed. `outline`, not `box-shadow`, because forced-colours mode removes box shadows |
| **Active (pressed)** | Pointer down or key held | Fill two steps darker, appearing instantly. This is the "we got your tap" signal within 100 ms | Native button activation fires on release (SC 2.5.2) |
| **Unavailable** (design.md: "disabled") | Submit or Save changes with an unsaved change; downloads on an empty day; Next day on today | Neutral grey fill, label still ≥ 4.5:1, **dashed border** as a second cue besides colour, `cursor: not-allowed` | `aria-disabled="true"`, **not** `disabled`. Stays in the tab order. `aria-describedby` points at the visible reason. A press does not do the action, but it explains or fixes the reason (see 5.1). For a link: no `href`, plus `role="link"` and `aria-disabled="true"` |
| **Busy** | From the press until the request settles | Page buttons: no change for the first 400 ms apart from the pressed state; after that, the busy label (the button's own verb + "…", for example "Saving changes…") and `cursor: progress`. Dialog buttons keep their labels; the dialog's status line shows the busy text after 1 s (confirm-dialog.md). Colours stay the same, because busy is not unavailable | `aria-disabled="true"` from the moment of pressing, so a second press is ignored. The Button writes the busy label into the page status region (`PageStatus`, found through context) once it shows, unless `announceBusy={false}` |
| **Error** | The request failed | The button returns to default. A message in words sits directly above the button group (or at the top of the dialog) | The message goes into a `role="alert"` container that is **already in the DOM**, never into the error summary (that holds field errors only). Focus stays on the button, so the user can simply press again |
| **Empty** | Nothing to act on (no notes that day; new note with nothing written) | Unavailable, with the design's empty-state sentence as the reason where one exists | As unavailable |
| **Read-only / not permitted** | This role can never take this action (a worker on someone else's note, a worker on Report) | **Not rendered** | Hidden, not shown as unavailable: it will never become available to this person |

### 5. Behaviour rules

**5.1 Pressing an unavailable button.** The press never does the action, and it never does nothing at all:
- **Submit or Save changes with an unsaved change:** save now instead of waiting for the 2-second timer. Blur usually triggers this already (3.4). If the indicator shows "Not saved: no connection. Keep this page open; retrying.", say that text again in the status region. The button becomes available as soon as the indicator shows "Saved 9:42 am".
- **Submit on a new note with nothing written** (no draft yet, so it is unavailable under 5.8): run the normal submit validation. The one thing that can be wrong is the empty Guided notes box, so the user sees the design's field error and the error summary. That is the real reason, and nothing is sent.
- **Download Word or Download PDF on an empty day:** nothing happens. The reason, "No submitted notes for Thursday 1 October 2026.", is linked by `aria-describedby` and was already announced when the date changed.

**5.2 Double activation.** A button goes busy (`aria-disabled`) as soon as it is pressed, so a second tap is ignored. This is a usability fix only. What makes double submits safe is the `Idempotency-Key` on `POST {base}/versions` (6.3). Create the key when the submit confirmation opens and **reuse it for every retry from that dialog**. A retry after a lost response then returns the existing version (`200`) instead of creating a second one.

**5.3 Only report success after the server confirms it.** Never show "submitted" or "saved" before the response arrives (invariant I5).

**5.4 Focus after an action** (allowed because the user started it):

| After | Focus goes to |
|---|---|
| Submit succeeds | Today, on the success banner "Note for Jane Citizen submitted" (`tabindex="-1"`). This is GOV.UK's success notification-banner pattern, and the **only** success banner in the app |
| Save changes succeeds | The read view's `<h1>` (no banner; the Edited line shows the result) |
| Cancel (edit) | The read view's `<h1>` |
| Discard draft succeeds | Today's `<h1>` (no banner; the row simply loses its status) |
| Mark reviewed succeeds | The review status that replaces the panel (`tabindex="-1"`). The button is gone, so focus must not fall back to `<body>` |
| Deactivate, Reset sign-in or Reactivate succeeds | The Status row of the user's detail page (`tabindex="-1"`), which shows the new state. The trigger has been replaced. No banner |
| Restore (participant) succeeds | The participant's `<h1>`. No banner |
| Show older | The first newly loaded note link |
| Save, Download, Resend invite | Stays on the button. The result goes to the status region |
| A confirmation dialog closes with Go back or Escape | The button that opened it |

**5.5 Confirmation dialogs** (native `<dialog>` + `showModal()`):
- The heading names the action and the person. The commit button repeats the verb and the person. The way out is always **"Go back"**, the design's own label, used the same way in every dialog.
- **Initial focus:** the dialog's title (`<h2 tabindex="-1">`) in every commit confirmation (Submit, Discard draft, Deactivate, Reset sign-in), as confirm-dialog.md and every screen chose: the person's name is heard first and there is no default button, so a double Enter cannot commit. The Guide prompts leave dialog focuses its safe button, "Stay on this page" (guide-prompts.md). (This file first proposed "Go back".)
- While the commit request is running, the commit button is busy, "Go back" is unavailable, and the dialog's `cancel` event is prevented. The dialog never closes on an unknown result. If Chromium closes it on a repeated Escape anyway (its anti-abuse rule for repeated Escape; unverified), the result handler still runs and reports on the page.
- No closing by tapping the backdrop (the default for `showModal()`).

### 6. Copy

Proposed text is marked. Every busy label is proposed.

| Screen | Action | Label | Style | Busy label (proposed) | Confirmation |
|---|---|---|---|---|---|
| 4.1 | Passkey sign-in | "Sign in with a passkey" | Primary | "Waiting for passkey…" | — |
| 4.1 | Password step | "Continue" (proposed) | Secondary | "Checking…" | — |
| 4.1 | Code step | "Sign in" | Primary | "Signing in…" | — |
| 4.1 | Choose a method (setup) | "Use a passkey (recommended)" / "Use a password and authenticator app" | Primary / Secondary | "Waiting for passkey…" / — | — |
| 4.1 | After the password is set (setup) | "Continue" (proposed) | Primary | — | — |
| 4.1 | Finish (password path) | "Finish setup" | Primary | "Finishing setup…" | — |
| 4.3 | Submit | "Submit note" | Primary | — (opens a dialog, no request) | Heading "Submit today's note for" + name + date + "Flagged for manager: No"/"Yes" |
| 4.3 | Confirm submit | "Submit note for Jane Citizen" / "Go back" | Primary / Secondary | "Submitting…" | — |
| 4.3 | Draft menu trigger | "More actions" (proposed) | Secondary | — | — |
| 4.3 | Discard (in menu) | "Discard draft" | Secondary | — | "Discard the draft note for Jane Citizen?" |
| 4.3 | Confirm discard | "Discard draft" / "Go back" | **Warning** / Secondary | "Discarding…" | — |
| 4.3 | Edit mode | "Save changes" / "Cancel" | Primary / Secondary | "Saving…" / "Cancelling…" | None specified (see Tensions 3) |
| 4.3 | Device conflict banner | "Keep the text on this screen" / "Load the other version" | Secondary / Secondary | — | — |
| 4.3 | Version conflict | "Start again from the latest version" | Secondary | "Loading…" | — |
| 4.4 | Edit a note | "Edit" | Secondary (link) | — | — |
| 4.4 / 4.6 | Review | "Mark reviewed" | Primary | "Saving…" | — |
| 4.4 | More history | "Show older" | Secondary | "Loading…" | — |
| 4.4 | Manager on someone else's draft | "Discard draft" | Secondary | — | As 4.3 |
| 4.7 | Day stepping | "Previous day" / "Next day" | Secondary (links) | — | — |
| 4.7 | Download | "Download Word" / "Download PDF" | Primary / Primary | "Downloading…" | — |
| 4.10 | Save prompts | "Save" | Primary | "Saving…" | — |
| 4.10 | Unsaved-changes warning | "Stay on this page" / "Leave without saving" (both proposed) | Primary / Secondary | — | Heading "Leave without saving?" (proposed) |
| 4.11 | List | "Invite user" | Primary (link) | — | — |
| 4.11 | Invite form | "Send invite" | Primary | "Sending…" | — |
| 4.11 | User detail | "Edit" | Secondary (link) | — | — |
| 4.11 | User detail | "Resend invite" | Secondary | "Sending…" | — |
| 4.11 | User detail | "Reset sign-in" | Secondary (trigger) | — | Proposed; see Open questions |
| 4.11 | Confirm reset | "Reset sign-in for Alex Park" / "Go back" (proposed) | **Warning** / Secondary | "Resetting…" | — |
| 4.11 | User detail | "Deactivate" | Secondary (trigger) | — | Heading "Deactivate Alex Park?" (proposed); body "Alex Park will be signed out everywhere now and can't sign in. Their notes stay." |
| 4.11 | Confirm deactivate | "Deactivate Alex Park" / "Go back" (proposed) | **Warning** / Secondary | "Deactivating…" | — |
| 4.11 | User detail | "Reactivate" | Secondary | "Reactivating…" | — |

**Error messages after a failed request (all proposed).** They follow the pattern of the design's own "Not saved: no connection." Each one says what happened and what to do next (invariant I10):
- No connection: "Not submitted: no connection. Try again." / "Not saved: no connection. Try again." / "Not sent: no connection. Try again." / "Didn't download: no connection. Try again."
- Server error: "Not submitted: something went wrong. Try again." (same shape for each verb).
- `409 review.not_current`: "This note has changed or was already reviewed. Check the latest version before marking it reviewed."
- `409 user.last_manager`: "There must always be at least one active manager."
- `409 user.email_in_use`: shown next to the email field: "This email address already has an account."
- `401` (session ended): no message on the button. The app's sign-in-in-place flow takes over (5.6), and the user's input stays in memory.

**Success messages (proposed, except the first).** "Note for Jane Citizen submitted" (design). "Changes saved". "Draft for Jane Citizen discarded". "Saved 4:12 pm" for guide prompts, matching the note form's "Saved 9:42 am". "Invite sent to alex.park@example.org". Downloads get none: the browser's own download UI confirms them.

### 7. Accessibility

- **Semantics:** `<button type="button">` for actions, `type="submit"` only for the one button that submits its `<form>`, and `<a href>` (React Router `<Link>`) for navigation. Never `<div onClick>` and never `<a href="#">`.
- **ARIA, only where needed:** `aria-disabled="true"` for the unavailable and busy states, and `aria-describedby` linking an unavailable button to its visible reason. `aria-expanded` and `aria-controls` go on the "More actions" disclosure trigger. Don't use `aria-busy` on buttons, `aria-label` on buttons with visible text, or `role="menu"`.
- **Repeated buttons in lists** (for example Edit on each user, or Archive on each goal) get context **after** the visible text: `Edit<span class="visually-hidden"> Alex Park</span>`. The accessible name then starts with the visible label (SC 2.5.3).
- **Keyboard:** native. Enter and Space activate. Tab order follows the visual order (primary first). Enter in a single-line field submits the form through its first submit button (HTML implicit submission). That only opens the confirmation on the note form, and the confirmation's focus starts on "Go back".
- **Screen reader announcements:** one visually hidden `role="status"` region **per page** (`PageStatus`, app-shell.md component 3), rendered empty with the page, for busy labels and results that do not move focus; the Button reaches it through React context. Screens with their own status line ("Saved 4:12 pm", the download lines) use that line and opt the Button out. A `role="alert"` container that is always rendered sits above each button group and inside each dialog, for request errors. The one success after navigation (Submit) uses a focused banner, not a live region.
- **Forced colours:** `aria-disabled` doesn't get the browser's disabled styling, so add `@media (forced-colors: active) { [aria-disabled="true"] { color: GrayText; border-color: GrayText; } }`. Keep a real `border` on every variant so the shape still shows when fills are removed.
- **WCAG 2.2 criteria this meets:** 1.3.1, 1.4.1 (warning and unavailable states use words and a dashed border, not colour alone), 1.4.3, 1.4.4 and 1.4.10 (wrapping labels, no fixed heights, nothing fixed or sticky), 1.4.11, 1.4.12, 2.1.1, 2.4.3, 2.4.7, 2.4.11 (nothing sticky), 2.5.2, 2.5.3, 2.5.8 (44 px against the 24 px minimum), 3.2.2 (nothing submits on input, including the 6-digit code), 3.2.4, 3.3.1 (validation on press), 3.3.4 (submit confirmed; discard and deactivate confirmed; archive reversible), 4.1.2, 4.1.3.

### 8. Implementation notes (React 19 + native HTML + CSS Modules)

- **One `Button` component and one `buttonClass()` helper** for links. Native `<button>` covers everything here, so React Aria isn't needed. Its `isPending` is the same pattern this file builds by hand.
- **Mutations use `retry: 0`**, set as the `QueryClient`'s mutation default (TanStack's own default is no retry; form-validation.md). No create or state-changing `POST` in design §6.6 has an idempotency key except the note-version `POST`, so a silent retry after a lost response could add a participant, goal or item twice. The only automatic retries in the app are autosave's (`clientId` + `seq`, autosave-status.md) and one resend by the `api()` wrapper: a change answered `400` with no `code`, as the `/api` filter refuses a missing or stale antiforgery token before any handler runs, is sent once more with a fresh token, so nothing can be done twice (empty-loading-error.md *Timing*).
- **Busy state comes from TanStack Query** (`useMutation().isPending`). Don't use React 19 `<form action>` or `useFormStatus` for these forms. That would add a second pending-state system, and "When a `<form>` Action succeeds, React will automatically reset the form for uncontrolled components." Resetting is the wrong default for the guide-prompts or invite fields. Use plain `onSubmit` with `preventDefault()`.
- **React 19 passes `ref` as an ordinary prop,** so `Button` needs no `forwardRef`.
- **Dialog focus:** React's `autoFocus` calls `focus()` at mount instead of writing the `autofocus` attribute. In a `<dialog>` that is still closed, that call does nothing (facebook/react#23301; a fix has been proposed). Call `goBackRef.current?.focus()` straight after `showModal()`.
- **Downloads are buttons, not links:** `fetch` → `blob()` → object URL → a temporary `<a download>` click, using the file name from `Content-Disposition` (`daily-notes_2026-10-01.pdf`). Why: the design needs both buttons to be unavailable with a reason; a `401` has to go to the sign-in-in-place flow; a network failure needs an in-app message; and every download writes an audit event (A28), which the busy guard keeps from being doubled. Revoke the object URL on a short timer after the click. Test on iOS Safari and Android Chrome under the production CSP before M4 sign-off (unverified on those two).
- **Unsaved-changes guard (4.10):** React Router's `useBlocker` for moves inside the app (it needs a data router), showing the dialog above. Use `beforeunload` for reload and tab close; the browser's wording there can't be changed.
- **"More actions" (4.3)** follows the APG **disclosure** pattern: `<button aria-expanded aria-controls>` showing an inline panel that holds "Discard draft". It is not an APG menu: one item doesn't justify the arrow-key model, and `role="menu"` is often broken. Show it only while the note is a Draft. A new note has nothing to discard, and edit mode has Cancel instead.

```tsx
// Button.tsx — one component for every button in the app
import { createContext, useContext, useEffect, useState, type ComponentPropsWithRef, type MouseEvent } from 'react';
import styles from './Button.module.css';

type Variant = 'primary' | 'secondary' | 'warning';

type Props = Omit<ComponentPropsWithRef<'button'>, 'disabled'> & {
  variant?: Variant;
  unavailable?: boolean;            // design.md "disabled": stays focusable
  unavailableReasonId?: string;     // id of the visible reason text
  onUnavailablePress?: () => void;  // explain or fix the reason (5.1)
  busy?: boolean;                   // request in flight (from useMutation().isPending)
  busyLabel?: string;               // the button's own verb + "…", e.g. "Saving changes…"
  announceBusy?: boolean;           // default true: write busyLabel into the page status region
};

// PageStatus (app-shell.md component 3) provides this context; announce() clears, then writes on the next frame.
export const PageStatusContext = createContext<{ announce: (text: string) => void } | null>(null);

export function buttonClass(variant: Variant = 'secondary') {
  return `${styles.button} ${styles[variant]}`;
}

export function Button({
  variant = 'secondary', unavailable = false, unavailableReasonId, onUnavailablePress,
  busy = false, busyLabel, announceBusy = true, type = 'button', className, onClick, children, ...rest
}: Props) {
  const [showBusy, setShowBusy] = useState(false);
  const pageStatus = useContext(PageStatusContext);
  useEffect(() => {                       // no flash for fast requests: page buttons wait 400 ms
    if (!busy) { setShowBusy(false); return; }
    const t = window.setTimeout(() => {
      setShowBusy(true);
      if (announceBusy && busyLabel) pageStatus?.announce(busyLabel);
    }, 400);
    return () => window.clearTimeout(t);
  }, [busy, announceBusy, busyLabel, pageStatus]);

  const blocked = unavailable || busy;

  function handleClick(e: MouseEvent<HTMLButtonElement>) {
    if (blocked) {
      e.preventDefault();                 // also stops form submission and implicit submission
      if (unavailable && !busy) onUnavailablePress?.();
      return;
    }
    onClick?.(e);
  }

  return (
    <button
      {...rest}
      type={type}
      className={`${buttonClass(variant)} ${className ?? ''}`}
      aria-disabled={blocked || undefined}
      aria-describedby={unavailable && !busy ? unavailableReasonId : undefined}
      data-unavailable={(unavailable && !busy) || undefined}
      data-busy={showBusy || undefined}
      onClick={handleClick}
    >
      <span className={styles.label}>{children}</span>
      {busyLabel && <span className={styles.busyLabel}>{busyLabel}</span>}
    </button>
  );
}
```

```css
/* Button.module.css — colours come from the theme tokens, not set here */
.button {
  display: inline-grid;
  place-items: center;
  inline-size: 100%;                         /* phone: full width */
  min-block-size: var(--target-button);      /* max(3rem, 48px), foundations.md; 44 px stays the A32 floor */
  min-inline-size: var(--target-button);
  padding: 0.625rem 1.25rem;
  border: 2px solid transparent;
  border-radius: 0.375rem;
  font: inherit;
  font-weight: 700;
  line-height: 1.25;
  text-align: center;
  text-decoration: none;                     /* for <Link> using buttonClass() */
  cursor: pointer;
}
@media (min-width: 40rem) {                  /* the one breakpoint, written as foundations.md's CI check expects */
  .button { inline-size: auto; min-inline-size: 8rem; }
}
.label, .busyLabel { grid-area: 1 / 1; }     /* same cell: width = longer label */
.busyLabel { visibility: hidden; }           /* visibility also removes it from the accessible name */
.button[data-busy] .label { visibility: hidden; }
.button[data-busy] .busyLabel { visibility: visible; }
.button[data-busy] { cursor: progress; }

.primary   { background: var(--action-primary-bg);  color: var(--action-primary-fg); }
.secondary { background: var(--surface);           color: var(--action-secondary-fg);
             border-color: var(--action-secondary-border); }
.warning   { background: var(--action-warning-bg);  color: var(--action-warning-fg); }

@media (hover: hover) and (pointer: fine) {
  .primary:hover:not([aria-disabled])   { background: var(--action-primary-bg-hover); }
  .secondary:hover:not([aria-disabled]) { background: var(--action-secondary-bg-hover); }
  .warning:hover:not([aria-disabled])   { background: var(--action-warning-bg-hover); }
}
.primary:active:not([aria-disabled])   { background: var(--action-primary-bg-active); }
.secondary:active:not([aria-disabled]) { background: var(--action-secondary-bg-active); }
.warning:active:not([aria-disabled])   { background: var(--action-warning-bg-active); }

.button:focus-visible {
  outline: 3px solid var(--focus-ring);
  outline-offset: 2px;
}

.button[data-unavailable] {
  background: var(--action-unavailable-bg);
  color: var(--action-unavailable-fg);        /* still >= 4.5:1 */
  border: 2px dashed var(--action-unavailable-border);
  cursor: not-allowed;
}

@media (forced-colors: active) {
  .button { border-color: ButtonText; }
  .button[aria-disabled="true"] { color: GrayText; border-color: GrayText; }
  .button:focus-visible { outline-color: Highlight; }
}

/* ButtonGroup.module.css */
.group { display: flex; flex-direction: column; gap: 1rem; align-items: stretch; }
@media (min-width: 40rem) {
  .group { flex-direction: row; flex-wrap: wrap; align-items: center; }
}
```

How the note form uses it (outline):

```tsx
// Inside NoteForm. save = the autosave hook's state; validate() = the form's own checks.
const unsaved = !save.isLatestSaved;                 // pending, in flight, failed, or no draft yet

<form noValidate onSubmit={(e) => {
  e.preventDefault();
  const errors = validate(note);                     // empty Guided notes, missing flag reason
  if (errors.length) return showErrorSummary(errors); // summary at top takes focus
  submitKey.current = crypto.randomUUID();           // one key per confirmation, reused on retry
  openConfirm();
}}>
  {/* … goals, common items, guided notes, flag … */}
  <div role="alert" className={styles.actionError}>{actionError}</div>
  <ButtonGroup>
    <Button type="submit" variant="primary"
      unavailable={unsaved}
      unavailableReasonId={save.hasMessage ? 'save-indicator' : undefined}
      onUnavailablePress={() => {
        if (!save.hasDraft) return showErrorSummary(validate(note)); // new note: say why
        save.flushNow();                                              // don't wait for the timer
        if (save.failed) announce(save.message);                      // repeat the indicator's text
      }}>
      Submit note
    </Button>
  </ButtonGroup>
</form>
```

---

## Per-screen notes

### 4.1 Sign-in and account setup
- **The sign-in screen has two separate forms.** "Sign in with a passkey" (primary, at the top) is a `type="button"` **outside** the email-and-password `<form>`. Pressing Enter in the password field then submits the password form, and never starts a passkey prompt. Below an "or" divider comes email, password and **"Continue" (proposed)**. This is secondary, because the passkey is the recommended method (design 4.1). [Opinion] Then a separate code page with "Sign in" (primary).
- **Code step:** never submit automatically when the sixth digit goes in (SC 3.2.2). The person presses "Sign in".
- **Failure:** the design's one message, "Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes.", goes in an error summary at the top, which takes focus. The button goes back to default and is never disabled during a lockout: the message already explains it.
- **Passkey cancelled:** if the person dismisses the device prompt (`NotAllowedError`), put the button back quietly with no error. They know they cancelled. [Opinion]
- **Setup:** "Use a passkey (recommended)" starts the device prompt straight away (design step 3). If passkeys can't be made in this browser, show "Open this link in your browser" under that button. The other button, "Use a password and authenticator app", stays as the alternative. Leave the passkey button available and show the message when it fails. Don't try to detect support up front, because in-app browsers report it unreliably (unverified).
- No Cancel or Back buttons are needed in setup. Each step is a page, and the browser's Back works. [Research, 2000: NN/g Cancel]

### 4.3 Note form and submit confirmation
- **Submit note** is the last item in the form, inline and full width on the phone, after the Flag box (as in the design sketch). It is never sticky.
- **Submit is available only when the latest change is saved** (3.4, 4.3, 5.8). Build "disabled" as `aria-disabled` with the save indicator as its `aria-describedby` reason, and handle presses as in 5.1. Change the look with no transition, so the button doesn't flicker visibly while autosave cycles.
- **Validation is not a reason to disable.** An empty Guided notes box or a missing flag reason shows the error next to the field and the summary at the top when Submit is pressed (design 4.3). This matches the GOV.UK validation pattern.
- **Confirmation dialog:** design copy and layout. The commit button "Submit note for Jane Citizen" wraps onto two lines for long names and is never truncated, because naming the person is the whole point of the dialog (3.9). Focus starts on the dialog title (confirm-dialog.md). The commit keeps its label; "Submitting…" shows in the dialog's status line after 1 s. On success, go to Today with focus on "Note for Jane Citizen submitted". On `422`, close the dialog and show the field errors. On a network error, keep the dialog open with "Not submitted: no connection. Try again." and the same `Idempotency-Key`.
- **Discard draft:** "More actions" (proposed label) is a secondary disclosure trigger 1.5rem below Submit, shown only on a Draft. Inside it, "Discard draft" (secondary) opens "Discard the draft note for Jane Citizen?" with **"Discard draft for Jane Citizen"** (warning) and **"Go back"**, focus on the title. Discard can't be undone in the app, which is why this is one of only **two** warning buttons (with Reset sign-in).
- **Edit mode** ("Editing submitted note (version 2)"): "Save changes" (primary), then "Cancel" (secondary), in the same place Submit sits. Cancel is plainly lower in emphasis and 1rem away (NN/g). Cancel is not red: it leaves the submitted note unchanged. Save changes follows Submit's availability rule (see Open questions).
- **Banner actions** ("Keep the text on this screen", "Load the other version", "Start again from the latest version") are secondary buttons inside the banner. "Keep the text on this screen" comes first because it is the choice that loses nothing. No banner button is primary: the page's primary is still Submit or Save changes.

### 4.4 Participant notes and read view (and the 4.6 review panel)
- **Workers** see "Edit" only on notes they wrote. Otherwise the button is not rendered (hidden, not unavailable). Managers always see Edit on submitted notes.
- **Edit** is a secondary link to the edit route. Reading is the purpose of this page, so editing a submitted note is offered but not pushed. [Opinion]
- **Mark reviewed** (primary) appears only while the note is in To review. When it succeeds, the panel becomes the review status, and focus moves there. On `409 review.not_current`, show the proposed message and refresh the note. The comment the manager typed must stay in the field (invariant I9).
- **Show older** (secondary) loads the next 30 notes. Focus moves to the first new note, and when no older notes remain the button is no longer rendered.
- **Manager header actions** (Write past-day note, Export record, Edit participant) are secondary links in one group. None is primary: on this page, the primary action is opening a note.
- **Manager viewing someone else's draft (read-only):** a single secondary "Discard draft" with the same confirmation as 4.3. No Submit or Edit.

### 4.7 Daily report
- **Previous day / Next day** are secondary `<Link>`s to `/reports/daily/{date}`. On today, Next day is rendered as `<a role="link" aria-disabled="true">Next day</a>` with no `href`, styled as unavailable (design 4.7). No reason text is needed, because "today" is shown in the date field.
- **Download Word / Download PDF:** both primary, side by side on a laptop and stacked on a phone, Word first. While `has-notes` is loading they are unavailable, with no message (usually well under a second). When the answer is "no", they stay unavailable, and the status region announces "No submitted notes for Thursday 1 October 2026.", which their `aria-describedby` also points to. If the `has-notes` check itself fails, leave both buttons available and let the download report its own error. Don't block the manager because of a failed pre-check.
- Pressing one: it goes busy ("Downloading…" after 400 ms), the file is fetched, and the save is handed to the browser. Focus stays on the button. Superseded by daily-report.md: the status line above the buttons says "Preparing the Word file… Keep this page open." and then "{file} is ready. Look in your downloads." (the same lines as Export record).

### 4.10 Guide prompts
- **"Save"** (primary) under the field, left-aligned, and never unavailable, even with no changes (saving unchanged text does no harm). It goes busy as "Saving…". On success, the status line next to it shows "Saved 4:12 pm" (proposed). On failure, an alert above it, with the typed text kept.
- **Leaving with unsaved changes** (design 4.10): the in-app dialog "Leave without saving?" (proposed) with "Stay on this page" (primary, focused first) and "Leave without saving" (secondary). Not red: losing a few lines of prompt text is easily redone, and red is kept for the two irreversible actions (Discard draft, Reset sign-in).

### 4.11 Users
- **List:** "Invite user" (primary link to the invite page). **Invite page:** name, email and role, then "Send invite" (primary). No Cancel button: the back link and browser Back are the way out (NN/g 2000; GOV.UK has no Cancel buttons on question pages). Success: superseded by users.md: back to the list, focus on its `<h1>`, the new row "Invited · Worker", no banner.
- **Detail page, in two groups:**
  1. Routine: "Edit" (secondary link), "Resend invite" (while Invited) or "Reset sign-in" (while Active).
  2. Under a separating rule: "Deactivate" (while Active or Invited) or "Reactivate" (while Deactivated).
  Show only the actions valid for the current status. Hide the rest rather than showing them unavailable.
- **Deactivate** → dialog. Heading "Deactivate Alex Park?" (proposed), the design's body text, then **"Deactivate Alex Park"** (standard primary style, not warning: Reactivate undoes it; users.md) and **"Go back"**, focus on the title. On `409 user.last_manager`, the proposed message appears in the dialog, and the commit button returns to default.
- **Reset sign-in** removes the password, authenticator and passkeys and ends every session. It can't be undone, and SC 3.3.4 calls for a confirmation (see Open questions). Recommended dialog, all proposed: heading "Reset sign-in for Alex Park?"; body "Alex Park will be signed out everywhere now. Their passkey, password and authenticator app will stop working, and a new setup link will be emailed to alex.park@example.org. Only do this after confirming who is asking."; buttons **"Reset sign-in for Alex Park"** (warning) and **"Go back"**.
- **Edit user:** the form's button is "Save" (same word as the other set-up forms, SC 3.2.4). Show the design's sentence "Changing the email resets sign-in and sends a setup link to the new address." as hint text under the email field, so the consequence is visible before Save is pressed.
- **Reactivate** and **Resend invite** need no confirmation: both are easily reversed or harmless.

---

## Anti-patterns to avoid

- **`disabled` on a button that is unavailable or busy.** The tap disappears silently, keyboard focus drops to `<body>`, and the control drops out of the tab order. Use `aria-disabled` plus a handler.
- **Disabling Submit until the form is valid.** Validate when Submit is pressed. The only "disabled" rule here is the design's unsaved-change rule.
- **A sticky or fixed bottom action bar** on the note form, or pinned buttons in a dialog.
- **Red on anything reversible** (Archive, Cancel, Go back, Leave without saving), or red on the page trigger *and* the dialog button. Red loses its meaning when it is everywhere.
- **Generic or icon-only labels:** "OK", "Yes", "Confirm", "Submit" with no object, "⋯", a bin icon, a download icon.
- **A spinner on its own, a spinner that flashes on a 150 ms request, or a label swap that resizes the button** and moves its neighbours under a finger.
- **Showing success before the server confirms it,** for example a "Submitted" toast fired as the request starts (invariant I5).
- **A new `Idempotency-Key` on each retry** of the same submit. A lost response then becomes two versions.
- **`<div onClick>`, `<a href="#" onClick>`,** or a `<button>` that only navigates.
- **`role="menu"` for the "More actions" holder,** or a custom menu without the full APG keyboard model.
- **Submitting the 6-digit code automatically** when the last digit is typed.
- **Truncating "Submit note for Jane Citizen"** with an ellipsis, or uppercasing the participant name.
- **Right-aligned button groups on a laptop.** Screen-magnifier users miss them.
- **Focus rings drawn with `box-shadow` only,** or removed. A focus ring that fades in.
- **`pointer-events: none` as a way to disable.** Keyboard users can still activate the control.
- **The only explanation in a tooltip or on hover.** Phones don't hover, so the reason must be visible text.
- **Removing a button the user just pressed** (Mark reviewed, Deactivate) without moving focus somewhere deliberate.
- **Confirmation dialogs on routine or reversible actions** (Save, Archive, Restore, Reactivate, Resend invite). They teach people to click through the ones that matter.
- **Two words for one action:** "Delete" for Discard, or "Cancel" in a dialog where the app says "Go back". Keep "Cancel" for leaving edit mode only.
- **React 19 `<form action>` on these forms,** because of the automatic reset of uncontrolled fields after success.

---

## Tensions with decisions

These are recorded once, with the evidence. None is a push to change the design. Each is built as design.md says, with the mitigations above.

1. **Submit (and Save changes) disabled while the latest change is unsaved** (design 3.4, 4.3, 5.8). GOV.UK, NHS and AgDS all advise against disabled buttons. There is also a timing problem: autosave waits about 2 seconds after the last keystroke, so a worker who finishes a sentence (or a dictation) and taps Submit straight away will usually tap an unavailable button. With native `disabled`, HTML throws that click away with no feedback. **Mitigation used:** `aria-disabled`, the save indicator as the stated reason, and a press that saves straight away. The cost that remains is a second tap after "Saved 9:42 am" appears. An alternative exists (open the confirmation automatically once the save lands). It is not recommended here, because the design says disabled.
2. **Download buttons disabled on an empty day, and Next day disabled on today** (4.7, A18, 11.4). The same design-system guidance applies. The mitigation is the same: `aria-disabled`, with the design's "No submitted notes for …" sentence as the reason. GOV.UK's pagination component hides an unusable previous or next link instead: "Do not show the previous page link on the first page – and do not show the next page link on the last page." [Convention] https://design-system.service.gov.uk/components/pagination/ The design's choice is kept.
3. **Cancel in edit mode throws away the autosaved pending edit with no confirmation** (3.5, A10). The pending edit is stored on the server, so SC 3.3.4 arguably applies (deleting stored data the user can see). NN/g advises a Cancel button only where people may fear they have committed. **Mitigation used:** Cancel is secondary, comes after Save changes, and sits 1rem away. The submitted note itself is never changed by Cancel. No confirmation is added.

### Open questions for the owner (gaps, not conflicts)

- **Reset sign-in confirmation (4.11).** The design specifies a confirmation for Deactivate but says nothing for Reset sign-in, which can't be undone. SC 3.3.4 (A32's AA target) is met by a confirmation. Recommended: reuse the Deactivate dialog with the proposed copy in Per-screen notes. Please confirm.
- **Confirmation heading for notes that aren't today's.** "Submit today's note for" is wrong for a manager's past-day note and for an earlier-day draft. Proposed: "Submit the note for" + name + the note's date in those cases.
- **Save changes when nothing has changed, or before the first autosave of the pending edit.** 5.8 needs a saved working copy for every version. Proposed: Save changes follows Submit's availability rule, with no change being "nothing to save". The owner should decide whether pressing it then simply returns to the read view.
- **Every string marked (proposed):** busy labels, error and success messages, "Continue" on the password steps, "More actions", the unsaved-changes dialog, and the Deactivate and Reset sign-in button labels.

---

## Sources

**Standards**
- WCAG 2.2 Understanding: 3.3.4 Error Prevention (Legal, Financial, Data) https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- WCAG 2.2 Understanding: 2.4.11 Focus Not Obscured (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding: 4.1.3 Status Messages https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding: 2.5.3 Label in Name https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding: 3.2.4 Consistent Identification https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html
- WCAG 2.2 Understanding: 1.4.1 Use of Color https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding: 1.4.11 Non-text Contrast https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 Understanding: 2.5.8 Target Size (Minimum) https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WAI-ARIA APG: Dialog (Modal) pattern https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- WAI-ARIA APG: Keyboard interface, focusability of disabled controls https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- WAI-ARIA APG: Disclosure pattern https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/
- WAI-ARIA APG: Button pattern https://www.w3.org/WAI/ARIA/apg/patterns/button/
- HTML Living Standard: disabled form controls and click events https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#enabling-and-disabling-form-controls:-the-disabled-attribute
- HTML Living Standard: implicit submission https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#implicit-submission
- Chrome for Developers: viewport resize behaviour and the on-screen keyboard https://developer.chrome.com/blog/viewport-resize-behavior
- MDN: `<dialog>` https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog

**Design systems (Convention)**
- GOV.UK Design System: Button https://design-system.service.gov.uk/components/button/
- GOV.UK Frontend: button styles (`width: 100%` below the tablet breakpoint) https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/button/_mixin.scss
- GOV.UK Design System: Validation pattern https://design-system.service.gov.uk/patterns/validation/
- GOV.UK Design System: Error summary https://design-system.service.gov.uk/components/error-summary/
- GOV.UK Design System: Notification banner https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK Design System: Pagination https://design-system.service.gov.uk/components/pagination/
- NHS digital service manual: Buttons https://service-manual.nhs.uk/design-system/components/buttons
- AgDS (Agriculture Design System, Australian Government DAFF): Button https://design-system.agriculture.gov.au/components/button
- NSW Design System: Button https://designsystem.nsw.gov.au/components/button/index.html
- React Aria: Button (`isPending`) https://react-aria.adobe.com/Button

**Research**
- NN/g, Confirmation Dialogs Can Prevent User Errors (If Not Overused), 2018 https://www.nngroup.com/articles/confirmation-dialog/
- NN/g, Website Forms Usability: Top 10 Recommendations (Whitenton, 2016) https://www.nngroup.com/articles/web-form-design/
- NN/g, Reset and Cancel Buttons (Nielsen, 2000) https://www.nngroup.com/articles/reset-and-cancel-buttons/
- NN/g, OK–Cancel or Cancel–OK? (Nielsen, 2008) https://www.nngroup.com/articles/ok-cancel-or-cancel-ok/
- NN/g, Sticky Headers: 5 Ways to Make Them Better https://www.nngroup.com/articles/sticky-headers/
- NN/g, Response Times: The 3 Important Limits (Nielsen, 1993) https://www.nngroup.com/articles/response-times-3-important-limits/
- NN/g, Button States: Communicate Interaction https://www.nngroup.com/articles/button-states-communicate-interaction/
- GrowthRock, sticky add-to-cart A/B test (e-commerce, vendor-published) https://growthrock.co/sticky-add-to-cart-button-example/
- Smashing Magazine, Friedman, Frustrating Design Patterns: Disabled Buttons (2021; practitioner observation) https://www.smashingmagazine.com/2021/08/frustrating-design-patterns-disabled-buttons/

**Opinion / practitioner**
- Smashing Magazine, Friedman, Hidden vs. Disabled In UX (2024) https://www.smashingmagazine.com/2024/05/hidden-vs-disabled-ux/
- Adrian Roselli, Don't Disable Form Controls (2024, updated 2026) https://adrianroselli.com/2024/02/dont-disable-form-controls.html
- Scott O'Hara, Disabled links (2021, updated 2022) https://www.scottohara.me/blog/2021/05/28/disabled-links.html

**Framework**
- React 19 release notes (`ref` as a prop; form Actions reset uncontrolled fields) https://react.dev/blog/2024/12/05/react-19
- React issue: `autoFocus` inside `<dialog>` https://github.com/facebook/react/issues/23301

**Internal**
- design.md sections 3.4, 3.5, 3.9, 4.0–4.11, 5.6, 5.8, 6.3, 6.9, 7.5, 13 (A10, A18, A22–A26, A28, A32)
- ui-ux-design and ui-build skill corpora (invariants I1, I5, I9, I10; control-states rules 9, 12, 14–16; feedback-channels 400 ms delayed-show, marked as convention)
