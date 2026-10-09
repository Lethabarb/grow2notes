# Authenticator app setup and one-time code entry

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. No Copy setup key button unless the owner approves it (clipboard concern, sign-in.md); request failures go in the alert above Finish setup.

Component key: `totp-setup`. Covers two pieces that share one input: the **authenticator app setup block**
(QR code, setup key, first code) and the **one-time code field** used at setup and at sign-in.

Evidence tags: **[Research]** studies and usability testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML spec ·
**[Convention]** established design systems and production services · **[Opinion]** reasoned judgement, no
direct evidence. Statements about how ASP.NET Core Identity behaves were read from its source code (linked in
Sources) and are facts about the product, not graded evidence.

---

## Where it's used

| # | Screen (design.md) | What the component does | What differs from the other uses |
|---|---|---|---|
| 1 | **Account setup**, "Use a password and authenticator app" path (4.1 step 4, 8.1 step 4) | Shows the pre-generated key as a QR code and as a typed **setup key**, then asks for one **6-digit code**. Sent with the password in one `POST /api/auth/setup/password {password, totpCode}`. | The person is already identified by the setup link and the 30-minute enrolment cookie, so errors can say exactly what is wrong. If the code is checked with `UserManager.VerifyTwoFactorTokenAsync` (as Identity's own template does), a wrong code does **not** count towards lockout. Rate limit: 30 a minute per IP (9.9). Primary action **Finish setup**. |
| 2 | **Sign-in, code step** after the password (4.1 "Signing in", 8.2) | Code field only. `POST /api/auth/login/totp {code}` → `200 Me` or `401`. | Every failure shows the design's one generic message (A25). Each wrong code counts towards the 5-attempt lockout, shared with password failures (9.9). The pending two-factor session lasts **5 minutes** (Identity's default for the two-factor cookie). Rate limit: 10 a minute per account. Primary action **Sign in**. |
| 3 | **Sign-in shown in place** after the session ends (4.0 Sessions, 8.5) | Same as 2. | Rendered over the route the person was on, so the label is not the page's `<h1>`. On success it returns to the same route and retries the save; it does not go to Today. |

**Not used for:** passkey setup or sign-in. There is no "change authenticator" screen, no recovery codes and no
"remember this device" (A22). A lost phone or forgotten password goes through a manager's **Reset sign-in** (4.11, 8.6).

---

## Best practice

### The code field

- **Use one input, not six boxes.** WCAG's Understanding document for SC 3.3.8 says a service that needs a code
  copied by hand fails the criterion. People must be able to paste the code, and the browser or a password manager
  must be able to fill it in. It gives split fields as the failing example: pasting the whole code fills only the
  first box. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html
- GOV.UK One Login uses one input for both setting up an authenticator app and signing in with one. **[Convention]**
  https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/setup-authenticator-app/index.njk ·
  https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/enter-authenticator-app-code/index.njk
- USWDS says not to split numbers into several inputs: each needs its own screen-reader label, and labels for the
  pieces are "often not meaningful". web.dev also recommends one `<input>`. **[Convention]**
  https://designsystem.digital.gov/components/text-input/ · https://web.dev/articles/sms-otp-form
- **No published usability study comparing one input with split boxes was found.** The case for one input rests on the
  standard and on convention, not on a measured difference. **[Research: none found]**
- **Use `type="text" inputmode="numeric"`, never `type="number"`.** GDS found that `type="number"` cannot be dictated or
  selected with Dragon, that NVDA reads its spin buttons without labels, that a scroll wheel can change the value,
  and that letters typed into it disappear without any message. **[Research]**
  https://technology.blog.gov.uk/2020/02/24/why-the-gov-uk-design-system-team-changed-the-input-type-for-numbers/ ·
  The HTML spec says `type="number"` does not suit input that only happens to be made of digits. **[Standard]**
  https://html.spec.whatwg.org/multipage/interaction.html#attr-inputmode-keyword-numeric
- **Add `autocomplete="one-time-code"`.** The HTML spec defines this token, and MDN describes it as a one-time code
  "received via ... SMS, email, or authenticator application". **[Standard]**
  https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-autocomplete-one-time-code ·
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete
  GOV.UK One Login and web.dev both use it. **[Convention]** On iPhone, codes saved in the Passwords app show above the
  keyboard on a code prompt (secondary source: https://www.macrumors.com/how-to/generate-verification-codes-passwords-app-ios/).
  Which attribute each platform and password manager actually looks for is **unverified**.
  Note: `one-time-code` is **not** in WCAG 2.2's list of input purposes, so SC 1.3.5 does not require it. SC 3.3.8's
  "let user agents fill" is the reason to add it. **[Standard]** https://www.w3.org/TR/WCAG22/#input-purposes
- **Accept spaces and dashes, and remove them before checking.** GOV.UK: "Allow additional spaces, hyphens and dashes."
  **[Convention]** https://design-system.service.gov.uk/patterns/confirm-a-phone-number/ · One Login strips spaces
  on the server before checking the code. **[Convention]**
  https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/common/verify-code/verify-code-validation.ts
  ASP.NET Core's own template removes spaces and hyphens too. Identity's `AuthenticatorTokenProvider` reads the code
  with `int.TryParse`, so a code like "123 456" fails unless the API removes the space first.
- **Never submit automatically when the 6th digit is typed.** WCAG failure F36 covers a form that submits itself
  when its last field gets a value (SC 3.2.2). **[Standard]** https://www.w3.org/WAI/WCAG22/Techniques/failures/F36
- **Don't use placeholder text.** It can be mistaken for a value already filled in, and it disappears once the person
  starts typing. **[Research]** https://www.nngroup.com/articles/form-design-placeholders/ (NN/g). ASP.NET Core's
  template uses `autocomplete="off"` and a placeholder. Don't copy either.
- **Make the input as wide as the code.** "Use fixed width inputs for content that has a specific, known length."
  **[Convention]** https://design-system.service.gov.uk/components/text-input/

### The setup key and QR code

- **Offer both a QR code and a key the person can type or paste.** GOV.UK One Login, ASP.NET Core Identity's template
  and Apple's Passwords app all support both. **[Convention]** (One Login template above; Apple:
  https://support.apple.com/guide/iphone/automatically-fill-in-verification-codes-ipha6173c19f/ios)
- **Show the key in groups of four characters.** One Login splits the key into 4-character pieces, and Identity's
  template does the same with spaces. **[Convention]** The key uses only A–Z and 2–7 (RFC 4648 base32), so there is
  no 0 or 1 to mistake for O or I. **[Standard]** https://www.rfc-editor.org/rfc/rfc4648#section-6
- **Tell people their app may call the key something else.** One Login notes that some apps use another word for it.
  **[Convention]** (translation file in Sources)
- **A copy button must write to the clipboard straight from the click.** Safari and Firefox only allow a clipboard
  write during a user action, and `writeText` is only available on HTTPS pages. **[Standard]**
  https://developer.mozilla.org/en-US/docs/Web/API/Clipboard_API · https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText
- **Leave a margin around the QR code.** QR codes need a quiet zone at least four modules wide on every side.
  **[Standard]** (from Denso Wave, the format's creator) https://www.qrcode.com/en/howto/code.html
- **Give the QR image a text alternative.** The visible setup key is the equivalent content, and the `alt` text names
  what the image is for (SC 1.1.1). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/non-text-content.html

### Explaining "authenticator app"

- **Say in a sentence or two what the app is, that it's free, and give examples.** The UK NCSC names apps "such as those
  provided by Microsoft or Google". One Login says any authenticator app works and that you get one from your app
  store. **[Convention]** https://www.ncsc.gov.uk/collection/top-tips-for-staying-secure-online/activate-2-step-verification-on-your-email
- **Number the steps.** One Login sets out its setup page as an ordered list. **[Convention]**
- **Setup is the hard part. Daily use is easy.** In a lab study (n = 30), authenticator app setup was the slowest of
  five methods (median 84 s), had the lowest ease score (mean 4.5 out of 7) and failed twice. In the same paper's
  two-week field study (n = 72), the authenticator app had the highest usability score of the second factors
  (median SUS 88.8). **[Research]** Reese et al., SOUPS 2019,
  https://www.usenix.org/conference/soups2019/presentation/reese. So the instructions should go into setup, and the
  daily sign-in step should stay bare.
- **Avoid negative contractions.** "Many users find them harder to read, or misread them as the opposite." This matters
  for readers who speak English as a second language. **[Convention]**
  https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

### Errors, timing and announcements

- **Put the message after the label and hint, and start it with a hidden "Error:".** Say what happened and how to fix
  it, and use the same wording in the error summary and next to the field. **[Convention]**
  https://design-system.service.gov.uk/components/error-message/ · SC 3.3.1 and 3.3.3. **[Standard]**
- **Clearing the field: keep the entry on format errors, clear it once the server rejects the code.** GOV.UK's general
  rule is not to clear fields. SC 3.3.7, though, lets you ask again "when the previously entered information is no
  longer valid". A rejected or expired code is no longer valid. **[Standard]**
  https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html · One Login shows an empty code field after a
  rejected code (its template sets no value). **[Convention]**
- **Don't add a countdown.** WCAG says time limits on time-based two-factor codes "can be considered essential" under
  SC 2.2.1. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html · Time pressure is
  still real: 8 of 12 people using an authenticator app said they struggled to enter the code before it changed.
  **[Research]** Reese et al. 2019. Identity accepts a code for up to 2 time steps (60 seconds) either side of the
  current one, so a code that changes while someone is typing still works.
- **Announce status messages without moving focus, through a live region that is already on the page** (SC 4.1.3).
  **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

---

## Recommendation for Grow2Notes

### Decisions in one place

1. **One input** for the code: `type="text"`, `inputmode="numeric"`, `autocomplete="one-time-code"`,
   `spellcheck={false}`, `autoCapitalize="off"`, `autoCorrect="off"`. No `maxLength`, no `pattern`, no placeholder,
   and no automatic submit.
2. **Remove spaces and dashes when checking, never while the person is typing.** Accept full-width digits as well
   (NFKC normalisation), for people whose phone keyboard is set to another script. **[Opinion]** The API does the same.
3. **Setup shows the QR code and the setup key, both visible, on every screen size.** The key is not hidden behind a
   "Can't scan?" disclosure. Workers set up on their own phones (D21), and you can't scan a QR code with the phone it's
   on, so on a phone the key is the main route. **[Opinion]**
4. **The key has a Copy setup key button.** The key is shown in capitals, in a monospace font, in groups of four. The
   copy has no spaces.
5. **Errors use the shared error summary and an inline message** (the GOV.UK pattern), on both setup and sign-in. Sign-in
   uses the design's generic message word for word.
6. **The code step never shows a countdown or a timer.**

### Anatomy

**Setup block** (inside the setup screen, after the password, in one `<form>` that ends with **Finish setup**):

```
h2  Set up your authenticator app
p   An authenticator app is a free app on your phone. It shows a new 6-digit code every
    30 seconds. You enter the code after your password each time you sign in.
ol
 1. Get an authenticator app on your phone if you do not have one, for example Google
    Authenticator or Microsoft Authenticator. Any authenticator app works.
 2. Add Grow2Notes to the app. Scan the QR code, or use the setup key.
      [ QR code image, 200 × 200, white quiet zone ]
      Using this phone? Copy the setup key and paste it into the app.
      If the app asks for a name, type Grow2Notes.
      Setup key
      ABCD EFGH IJKL MNOP QRST UVWX YZ23 4567        (monospace, wraps by group)
      [ Copy setup key ]
      (status) Setup key copied.
      Your app might call this a "key" or "secret key". You do not need to type the spaces.
 3. Come back to this page.
      label  Enter the 6-digit code from your authenticator app
      hint   Find Grow2Notes in the app. If the code changes while you're typing, you can still use it.
      [ ______ ]                                     (fixed width, large digits)
[ Finish setup ]
```

**Sign-in code step** (replaces the email and password step in the same sign-in screen):

```
h1 > label  Enter the 6-digit code from your authenticator app
hint        Open the app on your phone and find Grow2Notes. If the code changes while you're
            typing, you can still use it.
[ ______ ]
[ Sign in ]
p           Cannot use your authenticator app? Ask a manager to reset your sign-in.
```

The last line is plain text, not a link. It points to the only recovery route there is (A23, 8.6). **[Opinion]** It
saves a tired worker from using up their 5 attempts on a phone they no longer have.

### Behaviour

- **Arriving at the code step (sign-in):** once the password is accepted, render the code step and move focus to the
  code input. The person pressed Continue, so moving focus is allowed. Because the label is the `<h1>`, a screen reader
  reads the step's purpose and the field together.
- **Arriving at the setup block:** don't move focus. The person reads from the top. Fetch the key with
  `GET /api/auth/setup/authenticator` as soon as "Use a password and authenticator app" is chosen.
- **Typing and pasting:** the value is whatever the person typed. Never reformat it, never block keys, and never call
  `preventDefault` on key presses, because that breaks dictation and IMEs. Pasting "123 456" or "123-456" works.
- **Submitting (both):** check the code in the browser first: remove spaces and dashes, then require exactly 6 ASCII
  digits. If the field is empty or the format is wrong, show the error and don't send anything, so a typo never uses up
  a lockout attempt. While the request is pending, set the button to `aria-disabled="true"`, ignore further presses,
  and keep the field editable. If the request takes longer than 1 second, write "Signing in…" or
  "Finishing setup…" into the status region next to the button.
- **Sign-in rejected (`401`, or `429` from the rate limiter):** show the generic message, clear the field, set
  `aria-invalid`, and focus the error summary. The person stays on the code step.
- **Pending two-factor session expired:** Identity's two-factor cookie lasts 5 minutes. After that, every code fails
  with `401` and doesn't count towards lockout, so the person would be stuck on the code step. The SPA notes when the
  password was accepted. If a `401` or `429` arrives 4 minutes 50 seconds or more after that, it goes back to the email
  and password step. It keeps the email, clears the password, and shows the same generic message. Nothing reveals why.
  **[Opinion]** Set the two-factor cookie's lifetime explicitly in the server configuration so the SPA's constant and
  the server agree, and cover both with an integration test.
- **Setup rejected (`422 validation.failed` with a `totpCode` error):** clear the code, keep the password, and show the
  setup wrong-code message.
- **Setup timed out (`401` from any setup call):** show the design's message. Reopening the link shows the **same**
  key, because it was generated at invite (8.1), so a Grow2Notes entry the person already added to their app still works.
- **Copy setup key:** call `navigator.clipboard.writeText(key)` directly in the click handler, with the key already in
  memory. Write "Setup key copied." to a status region that is already on the page. The button text stays the same. If
  the write fails, show the "Could not copy" text. The key text has `user-select: all`, so a long press or click selects
  the whole key, and because the groups are separate spans with no space characters, a manual copy has no spaces either.
- **Success:** setup goes to Today (4.1 step 5). Sign-in goes to Today, or back to the previous route when shown in
  place (8.5). Focus moves according to the router's usual rule for a new page.

### States

| Element | State | Treatment |
|---|---|---|
| Code input | Default | 2 px border at ≥ 3:1 against the page; text at least 1.5rem (24 px) and bold; tabular digits; slightly wider letter spacing; at least 44 px tall. |
| | Hover | No change. Inputs don't need a hover state, and adding one gives nothing extra. **[Opinion]** |
| | Focus | The app's standard focus outline (`outline`, not `box-shadow`), at least 3:1 against both the input and the page. Always visible (A32, SC 2.4.7). |
| | Active / typing | Same as focus. |
| | Error | Thicker border in the error colour, plus the inline message with a hidden "Error:" first, plus `aria-invalid="true"`. Shown by text and border thickness as well as colour (SC 1.4.1). |
| | Disabled | **Never.** The input stays usable even while a request is pending. |
| | Read-only | Never. |
| | Empty | The default state. No placeholder. |
| Submit button (Sign in / Finish setup) | Default, hover, focus, active | The app's primary button. **Never `disabled`** before 6 digits are typed: check on submit instead. |
| | Loading | `aria-disabled="true"`, extra presses ignored, same width so it doesn't jump. "Signing in…" / "Finishing setup…" in a `role="status"` region after 1 s. |
| Copy setup key | Default, hover, focus, active | The app's secondary button, at least 44 × 44 px. |
| | After copy | Button unchanged. Status text "Setup key copied." stays until the key is copied again or the page is left. |
| | Copy failed | Status text "Could not copy. Press and hold the key to copy it, or type it." |
| Setup key | Loading | A fixed-height "Loading…" line where the key will go, so nothing jumps. The Copy button only appears once there's a key. |
| | Failed (`401`) | The whole screen shows the design's timed-out message. |
| QR image | Loading | A 200 × 200 box with `width` and `height` set on the `<img>`, so nothing jumps. |
| | Failed (`onError`) | Hide the image and show "The QR code did not load. Use the setup key instead." |
| | Dark mode | Keep it dark on light, on a white panel that includes the quiet zone. Never invert it. |

### Phone and laptop

- **Phone (single column, from 320 px):** the QR code (200 px) and then the key. The key wraps between groups, never
  inside one, and still fits at 320 px with text at 200% (SC 1.4.10). The code input keeps its fixed width.
  **Finish setup** and **Sign in** follow the app's button rule for phones. The submit button sits straight under the
  code field. The number pad doesn't always have a Return key (the iOS number pad is often reported without one,
  **unverified** across versions), so the button has to be visible once the keyboard is open. Set `scroll-margin` so
  the field and its error message aren't covered by the keyboard or any sticky header (SC 2.4.11).
- **Laptop:** the same single column, at most about 34rem wide. Scanning the QR code with a phone is the obvious route
  here. Nothing is hidden or reordered by screen size, so the DOM order matches the visual order on every device
  (SC 1.3.2, 2.4.3).

### Exact copy

Strings marked **design** are word for word from design.md. The rest are proposed, written in plain words without
negative contractions, and they use the design's terms "6-digit code", "setup key", "QR code" and "Reset sign-in".

| Where | Element | Copy | Source |
|---|---|---|---|
| Setup choice | Option | Use a password and authenticator app | design |
| Setup | Heading | Set up your authenticator app | proposed |
| Setup | Intro | An authenticator app is a free app on your phone. It shows a new 6-digit code every 30 seconds. You enter the code after your password each time you sign in. | proposed |
| Setup | Step 1 | Get an authenticator app on your phone if you do not have one, for example Google Authenticator or Microsoft Authenticator. Any authenticator app works. | proposed |
| Setup | Step 2 | Add Grow2Notes to the app. Scan the QR code, or use the setup key. | proposed (design: "Scan the QR code, or type the setup key") |
| Setup | QR `alt` | QR code for adding Grow2Notes to your authenticator app | proposed |
| Setup | Key lead-in | Using this phone? Copy the setup key and paste it into the app. If the app asks for a name, type Grow2Notes. | proposed |
| Setup | Key label | Setup key | design term |
| Setup | Button | Copy setup key | proposed |
| Setup | Status | Setup key copied. | proposed |
| Setup | Copy failed | Could not copy. Press and hold the key to copy it, or type it. | proposed |
| Setup | Key note | Your app might call this a "key" or "secret key". You do not need to type the spaces. | proposed |
| Setup | QR failed | The QR code did not load. Use the setup key instead. | proposed |
| Setup | Step 3 | Come back to this page. | proposed |
| Both | Field label | Enter the 6-digit code from your authenticator app | proposed |
| Setup | Hint | Find Grow2Notes in the app. If the code changes while you're typing, you can still use it. | proposed |
| Sign-in | Hint | Open the app on your phone and find Grow2Notes. If the code changes while you're typing, you can still use it. | proposed |
| Both | Empty | Enter the 6-digit code from your authenticator app | proposed |
| Both | Wrong format | The code must be 6 digits | proposed |
| Setup | Code rejected | That code did not work. Enter the code the app shows for Grow2Notes now. If the app lists Grow2Notes more than once, use the newest one. | proposed |
| Setup | Session ended (`401`) | Your setup session timed out. Open the link from your email again. | design |
| Setup | Too many tries (`429`) | Too many tries. Wait 1 minute, then try again. | proposed |
| Sign-in | Any rejection (`401`, `429`) | Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes. | design (A25) |
| Sign-in | Help line | Cannot use your authenticator app? Ask a manager to reset your sign-in. | proposed |
| Both | No connection | No connection. Check your internet and try again. | proposed (matches the note form's "no connection") |
| Both | Server error (5xx) | Something went wrong on our side. Try again in a few minutes. | proposed |
| Both | Pending > 1 s | Signing in… / Finishing setup… | proposed |
| Both | Buttons | Sign in · Finish setup | design |

The "more than once" sentence covers a Reset sign-in after a forgotten password. The old phone still has the old
Grow2Notes entry, and both entries carry the same label (`Grow2Notes:<email>`, 8.1). **[Opinion]**

### Accessibility

**Semantics**
- One `<form noValidate>` with a real `<button type="submit">`. The input has `id` and `name="code"` inside that form.
  Autofill needs all three. **[Standard]**
- `<label for>` on the input. On the sign-in step the label sits inside the `<h1>` (GOV.UK's "label as page heading");
  in the in-place sign-in it's a plain label.
- Hint and error are `<p>` elements linked with `aria-describedby`. Only include the error's id while there's an error:
  `aria-describedby` reads hidden content too.
- Setup steps are an `<ol>`. The QR code is an `<img>` with `alt`. The key is in a `<p>` that starts with a visible
  "Setup key" term. **Copy setup key** is a `<button type="button">`.
- Status regions (`role="status"`) for "Setup key copied." and the pending text are on the page from the start, empty.
- **ARIA only where native HTML can't do the job:** `aria-invalid`, `aria-describedby`, `aria-disabled` on the pending
  button, and `role="status"`. Nothing else. React Aria isn't needed here.

**Keyboard**
- Tab order on setup: Copy setup key → code input → Finish setup. The QR image and key text aren't tab stops. Sign-in:
  code input → Sign in. Enter in the input submits (implicit submission). Space or Enter activates Copy. No positive
  `tabindex`.

**Screen reader announcements**

| Event | What is heard | How |
|---|---|---|
| Code step appears | "Enter the 6-digit code from your authenticator app, heading/edit text", then the hint | Focus moves to the input |
| Format error, rejection | Error summary heading and the message | Focus moves to the error summary (it has `tabindex="-1"`). Its link takes the person to the input, where the inline error is read as the description. |
| Copy | "Setup key copied." | Text written into the existing `role="status"` region |
| Slow request | "Signing in…" | Text written into the existing `role="status"` region |

**WCAG 2.2 AA criteria met:** 1.1.1 (QR `alt` plus the text key), 1.3.1 (label, list, form structure), 1.3.2 / 2.4.3
(one DOM order), 1.4.1 (error not shown by colour alone), 1.4.3 / 1.4.11 (text and border contrast), 1.4.4 / 1.4.10
(200% text, reflow at 320 px), 2.1.1 (all keyboard), 2.2.1 (no time limit added by the UI; the code's own window
counts as essential), 2.4.6 (label describes the purpose), 2.4.7 (visible focus), 2.4.11 (focus not hidden by the
keyboard or sticky UI), 2.5.8 (targets at least 24 px; we use 44 px, A32), 3.2.2 (no automatic submit, F36),
3.3.1 / 3.3.3 (errors named, with a fix), 3.3.7 (password kept; a rejected code cleared under the "no longer valid"
exception), 3.3.8 (paste and autofill work; no copying by hand needed), 4.1.2 (native controls), 4.1.3 (status
regions).

### Implementation notes (React 19, native HTML, CSS Modules)

- **Files:** `src/auth/code.ts` (normalise and check), `src/auth/OneTimeCodeField.tsx` + `.module.css`,
  `src/auth/SetupKey.tsx` + `.module.css`, `src/auth/AuthenticatorSetup.tsx`.
- **Use `onSubmit` with controlled state, not `<form action>`.** React resets uncontrolled fields after an `action`
  succeeds ([docs](https://react.dev/reference/react-dom/components/form)), and that would wipe the password when a
  setup code is wrong.
- **TanStack Query:**
  - Setup key query: `staleTime: Infinity`, `gcTime: 0`, `refetchOnWindowFocus: false`, `retry: false`. The key never
    changes during setup. The person goes to their authenticator app and back, and that focus change mustn't refetch.
    `gcTime: 0` drops the secret from memory once the component unmounts.
  - Code mutations: `retry: false`, set explicitly, because every retry would count towards lockout. The `api()`
    wrapper leaves `/api/auth/login*`, `/api/auth/passkey*`, `/api/auth/setup/*` and `/api/auth/logout` out of the
    global "session ended → sign in again" `401` handler (app-shell.md component 7).
- **Server (mirror of the client):** NFKC, strip spaces and dashes, require `^[0-9]{6}$` before calling Identity.
  Badly formed codes don't reach `TwoFactorAuthenticatorSignInAsync`, so they don't count as attempts. The QR PNG and
  key responses are under `/api`, so they already get `Cache-Control: no-store` (6.1). Never log the code or the key.
  QRCoder: set pixels per module so the image is at least 200 px natively, and keep its quiet zone on.
- **Keep the secret out of** URLs, `document.title`, telemetry and the DOM (except the visible key). Don't render
  `authenticatorUri`. Never use a third-party QR service: it would leak the secret, and CSP `img-src 'self'` blocks it.
- **No web font for the key:** CSP `font-src 'self'`, so use a system monospace stack.
- **`translate="no"` on the key,** so a browser's page translation (which some readers use) can never change it.
  **[Opinion]**

```ts
// src/auth/code.ts
const SEPARATORS = /[\s\-‐-―−]/g;              // spaces, hyphens, dashes, minus
export const normaliseCode = (raw: string) => raw.normalize('NFKC').replace(SEPARATORS, '');

export function codeFormatError(raw: string): string | null {
  const code = normaliseCode(raw);
  if (code === '') return 'Enter the 6-digit code from your authenticator app';
  if (!/^[0-9]{6}$/.test(code)) return 'The code must be 6 digits';
  return null;
}
```

```tsx
// src/auth/OneTimeCodeField.tsx
type Props = {
  id: string; label: string; hint: string; error?: string; asPageHeading?: boolean;
  value: string; onChange: (v: string) => void; ref?: React.Ref<HTMLInputElement>;  // React 19: ref as a prop
};

export function OneTimeCodeField({ id, label, hint, error, asPageHeading, value, onChange, ref }: Props) {
  const describedBy = [`${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ');
  const labelEl = <label htmlFor={id} className={styles.label}>{label}</label>;
  return (
    <div className={error ? `${styles.field} ${styles.hasError}` : styles.field}>
      {asPageHeading ? <h1 className={styles.heading}>{labelEl}</h1> : labelEl}
      <p id={`${id}-hint`} className={styles.hint}>{hint}</p>
      {error && (
        <p id={`${id}-error`} className={styles.error}>
          <span className="visually-hidden">Error: </span>{error}
        </p>
      )}
      <input
        ref={ref} id={id} name="code" type="text"
        inputMode="numeric" autoComplete="one-time-code" enterKeyHint="go"
        spellCheck={false} autoCapitalize="off" autoCorrect="off"
        aria-invalid={error ? true : undefined} aria-describedby={describedBy}
        value={value} onChange={(e) => onChange(e.target.value)}
        className={styles.input}
      />
    </div>
  );
}
```

```tsx
// src/auth/SetupKey.tsx: key shown in groups, copied without spaces
export function SetupKey({ sharedKey }: { sharedKey: string }) {
  const key = sharedKey.replace(/\s/g, '').toUpperCase();
  const groups = key.match(/.{1,4}/g) ?? [];
  const [status, setStatus] = useState('');
  const copy = () => {
    navigator.clipboard.writeText(key)                     // called directly in the click: needed by Safari/Firefox
      .then(() => setStatus('Setup key copied.'))
      .catch(() => setStatus('Could not copy. Press and hold the key to copy it, or type it.'));
  };
  return (
    <div>
      <p className={styles.term}>Setup key</p>
      <p className={styles.key} translate="no">
        {groups.map((g, i) => <span key={i} className={styles.group}>{g}</span>)}
      </p>
      <button type="button" className={styles.copy} onClick={copy}>Copy setup key</button>
      <p role="status" className={styles.status}>{status}</p>
    </div>
  );
}
```

```css
/* OneTimeCodeField.module.css (tokens come from the app's theme) */
.input {
  font: inherit;
  font-size: 1.5rem;                 /* 16 px or more also stops iOS zooming on focus */
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  letter-spacing: 0.15em;
  inline-size: 10ch;                 /* fits "123 456" with room; scales with text size */
  min-block-size: 2.75rem;           /* 44 px */
  padding: 0.25rem 0.5rem;
  border: 2px solid var(--color-input-border);
  border-radius: 0;
  scroll-margin-block: 6rem;         /* keep the field and its error clear of sticky UI */
}
.input:focus { outline: 3px solid var(--color-focus); outline-offset: 0; }
.hasError .input { border-width: 4px; border-color: var(--color-error); }

/* SetupKey.module.css */
.key {
  display: flex; flex-wrap: wrap; gap: 0.25rem 0.75rem;   /* visual gaps, no space characters */
  font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
  font-size: 1.25rem; user-select: all;
}
.group { white-space: nowrap; }
.copy { min-block-size: 2.75rem; min-inline-size: 2.75rem; }
```

QR image in `AuthenticatorSetup.tsx`:

```tsx
<div className={styles.qrPanel}>  {/* white background in both themes */}
  <img src="/api/auth/setup/authenticator/qr.png" width={200} height={200}
       alt="QR code for adding Grow2Notes to your authenticator app"
       className={styles.qr} onError={() => setQrFailed(true)} />
</div>
/* .qr { image-rendering: pixelated; } keeps the modules sharp when scaled */
```

---

## Per-screen notes

### 4.1 Account setup (password and authenticator app)

- **Order on the page:** the password, then the setup block, then **Finish setup**. This follows the order in 4.1
  step 4. One request sends both (`{password, totpCode}`), and the password is only held in component state in
  memory (D22).
- **A wrong code never clears the password.** The password is only re-asked for if the person reopens the link.
- **The key stays the same until setup finishes** (8.1). Reopening the link after a timeout shows the same key, so the
  person doesn't need to add Grow2Notes to their app a second time. Mobile browsers sometimes reload a tab that has
  been in the background while the person was in their authenticator app. Once the fragment token has been removed
  (8.1 step 2), whether the SPA can resume from the enrolment cookie is a question for the setup screen design. At
  worst the person sees "Open the link from your email again", and their app entry still works.
- **Don't autofocus the code field.** It's at the bottom, and focusing it would scroll past the instructions.
- **Grow2Notes is the only name used.** The copy, the `alt` text and the QR label (`Grow2Notes:<email>`, issuer
  `Grow2Notes`) all say Grow2Notes (D42).
- **Left out on purpose:** an "open in authenticator app" (`otpauth://`) link. The design specifies a QR code or a
  setup key, and how such a link behaves across apps is unverified.

### 4.1 Sign-in, code step

- The password step's wording belongs to the password field component. This component starts when the API replies
  `{next: "totp"}`.
- The design's generic message is used word for word, for every `401` and `429` on this step (A25).
- Lockout counts both password and code failures (9.9). Checking the format in the browser keeps typos from using up
  attempts.
- **No "remember this device", no recovery-code link, no "try another way"** (A22). The single help line points to
  a manager.

### 4.0 / 8.5 Sign-in shown in place after the session ends

- Use the same component with `asPageHeading={false}` if the sign-in sits inside a `<dialog>` or under the note's
  `<h1>`. Focus stays inside that container.
- The note text the person hadn't saved stays in memory. Success returns to the same route and retries the save (8.5).
  The code step doesn't touch the note form's state.

---

## Anti-patterns to avoid

- Six separate boxes, focus jumping between boxes, or a single input made to look like boxes but with `maxLength={6}`.
- `type="number"`, `maxLength={6}` (it cuts a pasted "123 456" down to "123 45"), a `pattern` that rejects spaces,
  blocking paste, `autocomplete="off"`.
- Submitting automatically on the 6th digit (F36).
- Placeholder text such as "123456" or "Please enter the code" (both appear in ASP.NET Core's template).
- A countdown timer, progress ring or "code expires in…" text in Grow2Notes.
- Hiding the setup key behind "Cannot scan the QR code?", or hiding the QR code on phones by screen width or user agent.
- Showing the key in lower case and ungrouped, or putting spaces in the copied key.
- A "Copied!" label swap or a toast that fades out as the only feedback.
- At sign-in, saying which part failed ("Wrong code"), or showing a different message for lockout (A25).
- Retrying the code request automatically, or letting the global `401` handler treat a code-step `401` as "session
  ended".
- Clearing the password when the setup code is wrong. Keeping a rejected code in the field.
- `disabled` on the submit button before 6 digits are typed or while a request is pending.
- The key or code in a URL, the page title, logs or telemetry. Generating the QR code with a third-party service or a
  client-side library that needs a CSP exception.
- Inverting the QR code in dark mode, or cropping its quiet zone.
- `<form action={…}>` in React 19 for this form (it resets uncontrolled fields).
- Adding things the design doesn't specify: recovery codes, "remember this device", a "choose another method" link,
  an `otpauth://` button, or a confirmation screen after setup.

---

## Tensions with decisions

These are recorded for the owner. None of them is a request to change anything.

1. **Generic message on the code step (design.md 4.1, A25; not a D-decision).** GOV.UK error-message guidance asks
   for specific messages, and GOV.UK One Login tells people when the code is wrong ("The code you entered is not
   correct…"). In Grow2Notes the code step only appears after a correct password (`200 {next: "totp"}`), so the generic
   wording hides less than it might seem to, and it gives a tired worker less to go on. The design's message is used
   word for word.
2. **The 30-minute setup session has no warning (design.md 4.1 and §8 table; not a D-decision).** WCAG's Understanding
   document for SC 2.2.1 counts time limits on time-based two-factor codes as possibly essential. It is less clear for
   a 30-minute window to finish setup. Under the design, reopening the link still works and shows the same key, so
   little is lost apart from the typed password.

---

## Sources

**Standards**
- WCAG 2.2 Understanding SC 3.3.8 Accessible Authentication (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html
- WCAG 2.2 Understanding SC 3.3.7 Redundant Entry: https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- WCAG 2.2 Understanding SC 2.2.1 Timing Adjustable: https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- WCAG 2.2 Understanding SC 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding SC 1.1.1 Non-text Content: https://www.w3.org/WAI/WCAG22/Understanding/non-text-content.html
- WCAG 2.2, Input Purposes list (no `one-time-code`): https://www.w3.org/TR/WCAG22/#input-purposes
- WCAG failure F36 (automatic submit, SC 3.2.2): https://www.w3.org/WAI/WCAG22/Techniques/failures/F36
- HTML spec, `one-time-code` autofill token: https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-autocomplete-one-time-code
- HTML spec, `inputmode="numeric"`: https://html.spec.whatwg.org/multipage/interaction.html#attr-inputmode-keyword-numeric
- MDN, `autocomplete`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete
- MDN, Clipboard API security: https://developer.mozilla.org/en-US/docs/Web/API/Clipboard_API · `writeText`: https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText
- RFC 4648 base32 alphabet: https://www.rfc-editor.org/rfc/rfc4648#section-6
- Denso Wave, QR quiet zone: https://www.qrcode.com/en/howto/code.html

**Research**
- Reese, Smith, Dutson, Armknecht, Cameron, Seamons, "A Usability Study of Five Two-Factor Authentication Methods", SOUPS 2019: https://www.usenix.org/conference/soups2019/presentation/reese (PDF: https://www.usenix.org/system/files/soups2019-reese.pdf)
- GDS, "Why the GOV.UK Design System team changed the input type for numbers" (2020): https://technology.blog.gov.uk/2020/02/24/why-the-gov-uk-design-system-team-changed-the-input-type-for-numbers/
- NN/g, "Placeholders in Form Fields Are Harmful": https://www.nngroup.com/articles/form-design-placeholders/

**Conventions**
- GOV.UK Design System, Text input: https://design-system.service.gov.uk/components/text-input/
- GOV.UK Design System, Confirm a phone number: https://design-system.service.gov.uk/patterns/confirm-a-phone-number/
- GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- GOV.UK style guide, Contractions: https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- GOV.UK One Login authentication frontend (public source):
  - Set up an authenticator app: https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/setup-authenticator-app/index.njk
  - Enter authenticator app code: https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/enter-authenticator-app-code/index.njk
  - Code checks (whitespace removed): https://github.com/govuk-one-login/authentication-frontend/blob/main/src/components/common/verify-code/verify-code-validation.ts
  - Wording: https://github.com/govuk-one-login/authentication-frontend/blob/main/src/locales/en/translation.json
- USWDS, Text input: https://designsystem.digital.gov/components/text-input/
- web.dev, SMS OTP form best practices: https://web.dev/articles/sms-otp-form
- NCSC, Activate 2-step verification: https://www.ncsc.gov.uk/collection/top-tips-for-staying-secure-online/activate-2-step-verification-on-your-email
- Apple Support, verification codes on iPhone: https://support.apple.com/guide/iphone/automatically-fill-in-verification-codes-ipha6173c19f/ios · secondary write-up: https://www.macrumors.com/how-to/generate-verification-codes-passwords-app-ios/
- React, `<form>` (reset after action): https://react.dev/reference/react-dom/components/form

**Product behaviour (ASP.NET Core source, read 1 October 2026)**
- `AuthenticatorTokenProvider` (±2 time steps, `int.TryParse`): https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/AuthenticatorTokenProvider.cs
- Two-factor cookie lasts 5 minutes: https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs
- `TwoFactorAuthenticatorSignInAsync` counts failures towards lockout and fails without counting when the two-factor cookie has gone: https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/SignInManager.cs
- Key is 20 bytes as 32 base32 characters: https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/Base32.cs
- Identity UI template (removes spaces and hyphens, groups the key in fours, uses `autocomplete="off"` and a placeholder): https://github.com/dotnet/aspnetcore/blob/main/src/Identity/UI/src/Areas/Identity/Pages/V5/Account/Manage/EnableAuthenticator.cshtml.cs
