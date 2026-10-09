# Sign-in form and account setup form

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. No `/sign-in` route; sign-in renders at the requested URL. Show/Hide only on "Create a password" and no Copy setup key button unless the owner approves them; request failures go in the alert above the pressed button (sign-in.md).

Component key: `sign-in-form`. Screens: design.md 4.1 (Sign-in and account setup), plus the "sign-in in place"
after a session ends (design.md 4.0 Sessions, 5.6, 8.5). Rules it must keep: D23, D42, A22–A25, §6.2, §8.1–8.6, §9.6–9.9.

Evidence tags: **[Research]** measured with users · **[Standard]** WCAG 2.2, NIST, HTML/WebAuthn specs ·
**[Convention]** established design systems and browser-vendor guidance · **[Opinion]** reasoned judgement, not tested.
"Proposed copy" means the string is not in design.md and needs the owner's OK (listed again in the summary).

---

## Where it's used

The component is a small kit, not one form: **EmailInput**, **PasswordInput** (with Show/Hide), **CodeInput**
(6 digits), **ErrorSummary**, and two **method buttons**. Each view below uses some of them.

| # | View | Where | What's on it | What differs |
|---|---|---|---|---|
| 1 | **Sign in** | `/sign-in` | "Sign in with a passkey" button, "or", email + password, Continue | Two sign-in methods on one page; `current-password` |
| 2 | **Enter your code** | Same route, step 2 (component state, not a new URL) | One 6-digit code field, Sign in, Back | Reached only after a correct password; Identity's two-factor cookie lasts 5 minutes |
| 3 | **Sign in again (in place)** | Any route, after a `401` | Views 1–2 plus a notice; the page behind stays mounted but hidden | Must not navigate, so unsaved note text survives in memory (5.6, 8.5) |
| 4 | **Set up your account** | `/setup` (from `/setup#u=…&t=…`) | Name and email (fixed, shown as text), two method buttons | No inputs; passkey button runs the device prompt directly |
| 5 | **Create a password** | `/setup/password` | One new-password field with Show/Hide, Continue | `new-password`, hidden username field, 12-character rule |
| 6 | **Set up your authenticator app** | `/setup/app` | Steps, QR code, setup key, 6-digit code, Finish setup | Code entry for enrolment, not sign-in; specific error allowed |
| 7 | **Setup messages** | `/setup*` | Heading + one sentence, no form | Link expired; setup session timed out; in-app browser; no link in memory |

---

## Best practice

### Page structure and "one thing per page"

- **[Convention]** Start with one thing per page: it helps people "focus on the specific question", "use the service on
  a mobile device" and "recover easily from form errors". GOV.UK gives no measured evidence for it.
  (GOV.UK Service Manual, Structuring forms)
- **[Convention]** When a page asks one question, make the `<label>` the `<h1>`. Put a Back link at the top of question
  pages. Label the button "Continue", not "Next". (GOV.UK question pages)
- **[Convention]** Keep the email and password in **one** `<form>`. Password managers fill and save most reliably when
  "each authentication process … is grouped together in a single `<form>`". If email and password are split across
  pages, the password page needs a hidden username field. (Chromium, Create amazing password forms; web.dev)
- **[Research, n=5]** After a client-side route change, moving focus to the new heading tested well at first. The final
  advice became a focusable skip link plus a live-region announcement. Small sample. (Sutton / Fable Tech Labs, 2019)

### Autocomplete, password managers and paste

- **[Standard]** Password managers and autofill **SHALL** be allowed; paste **SHOULD** be allowed; a "show password"
  option **SHOULD** be offered until submit. Minimum 8 characters when the password is part of MFA (Grow2Notes asks
  for 12); maximum of at least 64 **SHOULD** be allowed; no composition rules (**SHALL NOT**); a blocklist check is a
  **SHALL**. (NIST SP 800-63B-4 §3.1.1.2)
- **[Standard]** WCAG 3.3.8 Accessible Authentication (AA): a password step passes only if a mechanism helps (paste,
  password manager). For a second-factor code, "it must be possible for a user to at least paste the code". A
  6-digit code split into separate boxes is given as a failure. WebAuthn is a passing alternative. Email "is not
  considered" a cognitive function test.
- **[Standard]** Sign-in email field `autocomplete="username"`; sign-in password `current-password`; new password
  `new-password`; code `one-time-code` ("most commonly … SMS, email, or authenticator application"). Browsers ignore
  `autocomplete="off"` on login fields. (MDN; web.dev; Chromium)
- **[Convention]** Keep `id`/`name` stable across releases, so autofill keeps working. (web.dev)
- **[Convention]** In a single-page app, a password manager decides "it worked" when the request succeeds and the
  password form is removed from the page (or the URL changes). (Chromium)

### Show/hide password

- **[Convention]** Hide by default. A button whose visible text switches between "Show" and "Hide", with accessible
  names "Show password" / "Hide password", and an announcement "Your password is visible" / "Your password is hidden".
  Switch the field back to `type="password"` when the form submits. Set `spellcheck="false"` (stops "spell-jacking")
  and `autocapitalize="none"`. (GOV.UK Password input)
- **[Research, n=600 + 200 survey]** Hu, Alroomi, Sahin and Li (ACM CCS 2024) found masking had no significant effect on
  typing errors or entry time, and only a minority used the toggle. So Show/Hide is cheap and expected (NIST SHOULD),
  but it will not fix sign-in errors on its own.
- **[Opinion]** NN/g (Sherwin, 2015) suggests showing passwords by default on phones. GOV.UK hides by default. Support
  workers sign in in participants' homes and in public, so hidden by default is the safer default here.
- **[Convention]** No "confirm password" field: GOV.UK found "having a second field is not helpful for users",
  especially with Show/Hide available.

### 6-digit code

- **[Convention]** One `type="text"` field with `inputmode="numeric"` and `autocomplete="one-time-code"`, sized for the
  code. Accept spaces, hyphens and dashes. Error wording "The code must be 5 numbers" (GOV.UK pattern, 5-digit code).
  (GOV.UK Confirm a phone number)
- **[Standard]** Never `type="number"` for codes: leading zeros drop and the value can be silently emptied. (MDN, HTML)
- **[Standard]** A security time limit on a two-factor code "can be considered essential" under WCAG 2.2.1, so the
  5-minute two-factor step does not need a warning or extension. 3.3.7 and 3.3.8 still apply.

### Passkeys

- **[Research]** FIDO Alliance guidelines, from repeated rounds of 60–90-minute remote sessions by Blink UX (US adults
  18–70; blind and low-vision screen-reader users added in 2023): "autofill ensured the highest success" for passkey
  sign-in. When several methods sit together, label the button "Sign in with a passkey". Provide a "graceful fallback
  to other sign-in methods". Sample sizes per round are not published. (Passkey Central)
- **[Research]** GOV.UK One Login (GDS blog, 16 September 2026): "users did not need to understand how passkeys work to
  be able to use them with ease". Explanations can stay short. About 300,000 users (almost 10%) adopted passkeys in
  the first month. This is an adoption figure, not a usability measure.
- **[Convention]** Use the FIDO passkey icon consistently next to passkey actions. (Google passkeys UI design guidance)
- **[Standard]** Cancelling, timing out and "no passkey here" all return the same `NotAllowedError`. The page cannot
  tell them apart, so it must not claim to know which happened. (WebAuthn; SimpleWebAuthn error wrapping)
- **[Convention]** Embedded in-app browsers often cannot use passkeys. "WebAuthn is currently not directly supported in
  embedded WebViews on Android". On iOS an embedded WebView can only use passkeys for the host app's own domain.
  System browser views (Custom Tabs, ASWebAuthenticationSession) work. Which email apps use which view is
  **unverified**. (passkeys.dev)
- **[Convention, unverified detail]** Older iOS versions dropped the tap's "user activation" across an `await fetch()`
  before `navigator.credentials.create/get`, giving `NotAllowedError`. Vendor blogs say iOS 17.4 relaxed this. Keep the
  options fetch and the WebAuthn call in the same click handler, and let a second tap retry.

### Errors and lockout

- **[Convention]** On a failed sign-in, "do not reveal whether they got the username or password wrong". Allow 5–10
  attempts before locking. Clear the password field; keep the rest. (GOV.UK Passwords, Password input)
- **[Standard]** WCAG 3.3.3 requires correction suggestions "unless it would jeopardize the security" of the content, so
  a generic sign-in failure message is allowed. WCAG 3.3.7 Redundant Entry: do not make people retype the email.
  Clearing a rejected password falls under the security and "no longer valid" exceptions (our reading, [Opinion]).
- **[Convention]** Validate on submit, not on blur. Turn off browser validation (`novalidate`, no `required`). Show an
  error summary headed "There is a problem" at the top and move focus to it. If you don't know which field is wrong,
  "link to the first field". Put each field's message after the label and hint, with a visually hidden "Error:"
  prefix and a red border. Write "Enter your …"; don't use "valid"/"invalid". (GOV.UK Validation, Error summary,
  Error message)

### Phone keyboards and layout

- **[Convention]** `type="email"` gives the @ keyboard. Text below 16px in a field makes iOS Safari zoom in on focus.
  Keep tap targets at least 44 × 44 px. Put the main button where the keyboard won't hide it. Use `<form>`, `<label>` and
  `<button>`, never `div`s. (web.dev sign-in form best practices; Judis on iOS zoom)
- **[Convention]** Don't disable Continue while fields are empty. Do stop a second tap while a request is in flight.
  (web.dev)

---

## Recommendation for Grow2Notes

### Anatomy

**View 1–2: Sign in (phone, 360 px wide)**

```
Grow2Notes                                  ← app name only, no nav
Sign in                                     ← h1
Sign in the way you chose when you          ← hint (proposed)
set up your account.
[🔑 Sign in with a passkey             ]    ← primary button, full width
──────────────── or ────────────────
Email address                               ← label
[jane@example.com                     ]
Password
[••••••••••••••              ] [ Show ]     ← 44×44 toggle; wraps below at 320 px / 200% text
[ Continue                             ]    ← primary button, submit of the password form
Can't sign in? Ask a manager to reset       ← hint (proposed)
your sign-in.
```

```
‹ Back                                      ← button styled as a back link (returns to step 1)
Enter your 6-digit code                     ← h1 that is also the <label>
Open your authenticator app and find        ← hint
Grow2Notes. The code changes every 30 seconds.
[ 123 456 ]                                 ← ~9ch wide, numeric keyboard
[ Sign in                              ]
```

**View 4: Set up your account**

```
Set up your account                         ← h1 (design)
Name           Jane Citizen                 ← <dl>, read-only text, not inputs
Email address  jane@example.com
If these are wrong, ask a manager.          ← hint (proposed)
Choose how you'll sign in                   ← h2 (proposed)
[🔑 Use a passkey (recommended)         ]    ← primary
Uses your face, fingerprint or PIN, like unlocking your phone. Nothing to remember.
[ Use a password and authenticator app ]    ← secondary
You'll make a password and use an app that shows a 6-digit code each time you sign in.
```

**View 5: Create a password**: Back · h1/label "Create a password" · hint "Use at least 12 characters." · field +
Show/Hide · hidden `username` field holding the email · Continue.

**View 6: Set up your authenticator app**: Back · h1 · 3-step ordered list · QR code (200 × 200) · "Setup key" in
groups of 4 · label "Enter the 6-digit code from the app" + hint · code field · "Finish setup".

### Behaviour

1. **Two forms, two methods.** The passkey button is a `<button type="button">` outside the email/password `<form>`.
   So Enter in the password field never starts the passkey prompt, and the passkey never submits the form.
2. **Sign in with a passkey:** tap → `POST /api/auth/passkey/options` → `startAuthentication({ optionsJSON })` → `POST
   /api/auth/passkey` → `200 Me` → Today (or back to the route the person was on). On `NotAllowedError` (cancelled,
   timed out, no passkey on this device) show **nothing** and return focus to the button: the person chose to stop, or
   the device already explained. On `401`, show the generic failure.
3. **Email + password → Continue:** client checks run first (empty email, no "@", empty password). Then `POST
   /api/auth/login`. On `200 {next:"totp"}` the form is removed and the code step shows in the same place; focus moves
   to the code field. On `401` or `429` show the generic failure, **keep the email, clear the password** and focus the
   error summary.
4. **Code → Sign in:** strip spaces, hyphens and dashes, then require exactly 6 digits. `POST /api/auth/login/totp`. On
   `401` show the generic failure, clear the code and stay on the step. **If more than 5 minutes have passed since the
   password step** (Identity's two-factor cookie lifetime), go back to step 1 instead, email kept, with the same generic
   message. Otherwise a tired worker could fail forever on a code step that can no longer succeed. [Opinion]
5. **No auto-submit** when the 6th digit arrives. Dictation, paste and slow typing all produce partial values, and
   submitting on input changes context without warning (3.2.2 risk). [Standard/Opinion]
6. **Setup start:** read `u` and `t` from `location.hash` once, `history.replaceState` to `/setup` straight away (8.1),
   then `POST /api/auth/setup/start`. While waiting, show "Checking your link…" (after 1 s). `410` → link expired
   message. Success → view 4.
7. **Passkey setup:** "Use a passkey (recommended)" → creation options → `startRegistration` → `POST
   /api/auth/setup/passkey` → Today. `NotAllowedError` → inline message "Your passkey was not set up…". No WebAuthn, or
   any other WebAuthn error → the in-app browser message (below), with the authenticator option still offered.
8. **Password setup:** view 5 checks length on Continue (client only; there is no server call), keeps the password in
   memory, and goes to view 6. View 6 sends `{password, totpCode}` on "Finish setup" → Today.
9. **Enrolment cookie expired (`401` on any `/setup/*` call):** "Your setup session timed out…" message. Any setup
   sub-route opened with nothing in memory (for example after a refresh, or "Open in browser" from an in-app
   browser, which no longer has the token) shows "To set up your account, open the link from your email again."
10. **Sign in again (in place):** on a `401` from any API call, the app shell gets the `hidden` attribute (still mounted,
    so in-memory note text survives) and views 1–2 render with the notice. On success: if `Me.userId` is the same,
    unhide and retry the pending save. If it is a **different** person, do a full reload to `/`, so one person's unsaved
    text is never shown to another. [Opinion, privacy]
11. **After any sign-in or setup success:** set the `me` query data and navigate with `replace: true`, so Back does not
    return to a sign-in step. The screen fetches no antiforgery token: the `api()` wrapper drops its own on the
    `200 Me`, so its next change fetches a fresh one, bound to the person now signed in (9.8).
12. **Repeat taps:** while a request is in flight, the button gets `aria-disabled="true"` and further presses are
    ignored. It is never `disabled`, which would drop keyboard focus to `<body>`. Show the status text only if the wait
    passes 1 s. [Convention]

### States

| Part | Default | Hover (`@media (hover:hover)` only) | Focus | Active | Error | Loading | Disabled / read-only |
|---|---|---|---|---|---|---|---|
| Text field | 2px border at ≥3:1, white field, ≥16px text | No change | 3px focus outline (`outline`, not `box-shadow`) | n/a | Error border, 4px error bar on the group, message above the field with hidden "Error:" | n/a | Never disabled. Name and email on view 4 are text in a `<dl>`, not read-only inputs |
| Primary button | Filled, full column width, min 48px tall | Darker fill | Same outline token, 2px offset | Darker fill, no motion | n/a | `aria-disabled`, label unchanged, "Signing in…" in the status line after 1s | Not used: validation runs on submit |
| Show/Hide | Text "Show", bordered, 44×44 minimum | Darker | Outline | n/a | n/a | n/a | n/a |
| Passkey button | Primary, FIDO icon (decorative) + text | Darker fill | Outline | n/a | Generic failure in the summary (server `401` only) | `aria-disabled` while the device prompt is open | No WebAuthn on sign-in: replaced by a notice |
| Code field | ~9ch wide, letter-spaced, tabular figures | No change | Outline | n/a | Same as text field | n/a | n/a |
| Error summary | Hidden | n/a | Receives focus (`tabIndex={-1}`) after a failed submit | n/a | "There is a problem" + message(s) linked to field(s) | n/a | n/a |
| Empty | Fields start empty. No placeholders | | | | | | |

### Copy

| Where | Text | Source |
|---|---|---|
| Page titles | "Grow2Notes – Sign in" · "Grow2Notes – Enter your code" · "Grow2Notes – Set up your account" · "Grow2Notes – Create a password" · "Grow2Notes – Set up your authenticator app" | Pattern from 4.0; wording proposed |
| Sign in h1 | "Sign in" | design (primary action) |
| Sign in hint | "Sign in the way you chose when you set up your account." | proposed |
| Passkey button | "Sign in with a passkey" | design |
| Divider | "or" | proposed |
| Labels | "Email address" · "Password" | proposed (GOV.UK wording) |
| Toggle | Visible "Show" / "Hide"; accessible name "Show password" / "Hide password"; announced "Your password is visible" / "Your password is hidden" | GOV.UK |
| Password-step button | "Continue" | proposed (a code step follows) |
| Recovery hint | "Can't sign in? Ask a manager to reset your sign-in." | proposed; restates A23, not a link |
| Code h1/label | "Enter your 6-digit code" | proposed |
| Code hint | "Open your authenticator app and find Grow2Notes. The code changes every 30 seconds." | proposed |
| Code-step button | "Sign in" | design |
| **Generic failure** | "Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes." | **design, exact** |
| Summary heading | "There is a problem" | GOV.UK |
| Field errors | "Enter your email address" · "Enter an email address in the correct format, like name@example.com" · "Enter your password" · "Enter the 6-digit code" · "The code must be 6 numbers" | proposed (GOV.UK patterns) |
| Waiting (>1 s) | "Signing in…" · "Checking your link…" · "Finishing setup…" | proposed |
| No connection | "No connection. Check your internet and try again." | proposed (matches the "no connection" wording in 4.3) |
| Server error | "Something went wrong. Try again in a few minutes." | proposed: align with the app-wide error copy |
| In-place notice | "You've been signed out. Sign in again to carry on." | proposed |
| Passkey unsupported (sign-in) | "Passkeys don't work in this browser. To use your passkey, open Grow2Notes in your phone's main browser, such as Safari or Chrome." | proposed |
| Setup h1 | "Set up your account" | design |
| Setup hints | "If these are wrong, ask a manager." · h2 "Choose how you'll sign in" | proposed |
| Method buttons | "Use a passkey (recommended)" · "Use a password and authenticator app" | design |
| Method hints | "Uses your face, fingerprint or PIN, like unlocking your phone. Nothing to remember." · "You'll make a password and use an app that shows a 6-digit code each time you sign in." | proposed |
| Passkey not created | "Your passkey was not set up. Try again, or use a password and authenticator app." | proposed |
| In-app browser | Heading: **"Open this link in your browser"** (design). Body: "Passkeys can't be set up here. Go back to the email and open the link in your phone's browser, such as Safari or Chrome. Or use a password and authenticator app." | design + proposed body |
| Link expired | h1 "This link has expired" · "Ask a manager to send a new one." | design (split into heading + sentence) |
| Setup timed out | h1 "Your setup session timed out" · "Open the link from your email again." | design (split) |
| No link in memory | h1 "Set up your account" · "To set up your account, open the link from your email again." | proposed |
| Create password | h1/label "Create a password" · hint "Use at least 12 characters." · errors "Enter a password" · "Password must be 12 characters or more" | GOV.UK wording; 12 from A22 |
| Authenticator steps | 1 "Open your authenticator app and add an account." 2 "Scan the QR code, or enter the setup key. If the app asks for an account name, use Grow2Notes." 3 "Enter the 6-digit code the app shows." | proposed |
| QR alt | "QR code for your authenticator app" | proposed |
| Key label | "Setup key" | design ("setup key") |
| Setup code label/hint | "Enter the 6-digit code from the app" · "The code changes every 30 seconds." | proposed |
| Setup code wrong | "That code didn't work. Enter the code the app shows now." | proposed: setup may say which field, because the enrolment cookie already proves the link |
| Finish | "Finish setup" | design |

No string, alt text, title, hidden text, passkey name or file name contains the parent company's name (D42).

### Phone vs laptop

- **Phone (the main case):** one column, 16px side gutters, inputs and buttons full width, minimum height 48px. The
  code field stays narrow (about 9ch) so it reads as "a short code". Show/Hide sits beside the password field and wraps
  below it when there is no room (320px wide, or text at 200%). No `autofocus` on view 1: it would open the keyboard and
  hide the passkey button. On the code steps, focus goes to the code field, so the numeric keyboard is ready.
- **Laptop:** the same single column, centred, `max-inline-size: 30rem`, same order and sizes. No second column, no
  illustration. The QR code matters most here (scanned with the phone). On a phone it is still shown, because the
  design lists it, but the setup key is what a phone user will copy.
- Text in fields is at least 16px (iOS zoom). Hover styles only apply under `@media (hover: hover)`, so they don't stick
  on touch.

### Accessibility

**Semantics.** `<main>` → `<h1>` → a `<form noValidate>` per method. Real `<label for>` on every field. Hints and
errors are `<p id>` joined in `aria-describedby` (hint first, then error). `aria-invalid="true"` only on fields in error.
The setup name and email are a `<dl>`. The QR code is an `<img>` with alt text. The setup key is plain text in `<code>`.
The Back control on the code step is a `<button type="button">`, because it changes state, not the URL. On setup views
it is a router `<Link>`, because it changes the URL. The passkey icon is `aria-hidden="true"`.

**ARIA only where native falls short:** `aria-describedby`, `aria-invalid`, `aria-disabled` on pending buttons,
`aria-controls` + a changing `aria-label` on Show/Hide, one `role="status"` line per form, one visually hidden
`aria-live="polite"` line per password field. No `role="alert"` created on demand. No React Aria needed.

**Keyboard.** Tab order matches the visual order: passkey button → email → password → Show/Hide → Continue. Enter in any
field submits its own form. Space/Enter work on every button. Nothing traps focus. After a failed submit, focus goes to
the error summary; its links move focus to the field.

**Screen-reader announcements**

| Event | What is heard | How |
|---|---|---|
| Failed submit (client or server) | "There is a problem, Sign-in failed. Check your details…" | Focus moves to the summary (`tabIndex={-1}`) |
| Password step OK | "Enter your 6-digit code, edit text, Open your authenticator app…" | Focus moves to the code field |
| Show/Hide | "Your password is visible" / "Your password is hidden" | Persistent polite live line |
| Wait over 1 s | "Signing in…" | Persistent `role="status"` line, written after 1 s |
| New setup view | The view's h1 | Focus to h1 (`tabIndex={-1}`) on route change, following the app-wide route rule |
| Passkey cancelled at sign-in | Nothing | Focus stays on the button |

**WCAG 2.2 criteria this meets:** 1.3.1, 1.3.2, **1.3.5** (`username`, `current-password`, `new-password`; whether
`one-time-code` is on WCAG's own list is unverified, but it is used anyway for autofill), 1.4.1 (errors in words and a
border, not colour alone), 1.4.3, 1.4.4, 1.4.10 (320px, toggle wraps), 1.4.11 (field and button borders ≥3:1),
1.4.12, 2.1.1, 2.2.1 (the 5-minute two-factor step falls under the security "essential" reading; the 30-minute setup
session relies on the same reading, and its message tells people how to recover), 2.4.2, 2.4.3, 2.4.6, 2.4.7,
2.4.11, 2.5.3 (the visible "Show" is inside the name "Show password"), 2.5.8 (44px beats 24px), 3.2.2 (no auto-submit),
3.3.1, 3.3.2, 3.3.3 (security exception for the generic message), **3.3.7** (email kept; password cleared under the
security exception), **3.3.8** (paste and password managers allowed, a single code field, and passkeys as the
non-cognitive alternative), 4.1.2, 4.1.3.

**Forced colours.** Focus uses `outline`. Every state carried by a background or colour also has a border, so it
survives `forced-colors: active`.

### Implementation notes (React 19 + native HTML + CSS Modules)

- **Files:** `features/auth/SignIn.tsx` (steps 1–2 as component state; used by `/sign-in` and by the in-place gate),
  `features/auth/setup/*.tsx` (one route per view), `components/form/PasswordInput.tsx`, `CodeInput.tsx`,
  `EmailInput.tsx`, `ErrorSummary.tsx`, each with a `.module.css`.
- **Memory only.** Email, password and setup state live in a React context above the routes. Never in
  `localStorage`/`sessionStorage`, the URL, or React Router `state` (which is written to `history.state` and can be
  saved by session restore). Clear the setup context on finish or when leaving `/setup*`.
- **Don't use `<form action={fn}>` here.** React 19 resets uncontrolled fields "after the `action` function succeeds".
  An action that catches a `401` and returns an error counts as succeeding, so the email would be wiped (3.3.7). Use
  `onSubmit` + `preventDefault()` + controlled inputs.
- **Don't route credentials through TanStack Query mutations.** The mutation cache keeps `variables`, which here would
  be the password, in memory after the call. Call the shared API client directly from the handler, then
  `queryClient.setQueryData(['me'], me)`.
- **Passkeys:** `@simplewebauthn/browser` (already chosen in 7.5): `browserSupportsWebAuthn()`,
  `startAuthentication({ optionsJSON })`, `startRegistration({ optionsJSON })`. Errors arrive with `name` (for example
  `NotAllowedError`). Send `credentialJson` as `JSON.stringify(result)`. The setup call's `name` field: send a fixed
  value, with no UI.
- **Strict Mode:** read the setup hash in a lazy `useState(() => parse(location.hash))` initialiser, then
  `replaceState` and `POST /setup/start` in an effect with an `AbortController`. Otherwise the dev double-run sees an
  empty hash.
- **`<title>`** can be rendered inside each view; React 19 moves it into `<head>`.
- **Setup key:** render groups of 4 as `<span>`s spaced by CSS (no space characters), with `user-select: all`, so a copy
  gives the bare key. In a dark theme, put the QR code on a white panel with a quiet margin.
- **Fields:** email `type="email" autoComplete="username" spellCheck={false} autoCapitalize="none" autoCorrect="off"`,
  trimmed before sending. Passwords: never trimmed, no `maxLength`. New password `minLength={12}`, which guides
  password generators; `noValidate` stops it from blocking. Code: `type="text" inputMode="numeric"
  autoComplete="one-time-code"`, no `maxLength` (a pasted "123 456" is 7 characters).
- **Hidden username** on view 5: `<input type="email" name="username" autoComplete="username" value={email} readOnly
  hidden />` inside the password form (Chromium guidance).

```tsx
// components/form/PasswordInput.tsx
export function PasswordInput({ id, label, hint, error, autoComplete, value, onChange, asHeading }: Props) {
  const [shown, setShown] = useState(false);
  const [said, setSaid] = useState('');               // empty until first toggle: nothing announced on mount
  const ref = useRef<HTMLInputElement>(null);

  useEffect(() => {                                   // back to dots whenever the form submits (GOV.UK)
    const form = ref.current?.form;
    const hide = () => setShown(false);
    form?.addEventListener('submit', hide);
    return () => form?.removeEventListener('submit', hide);
  }, []);

  const describedBy = [hint && `${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ') || undefined;
  const labelEl = <label htmlFor={id} className={styles.label}>{label}</label>;
  return (
    <div className={error ? `${styles.group} ${styles.groupError}` : styles.group}>
      {asHeading ? <h1 tabIndex={-1} className={styles.headingLabel}>{labelEl}</h1> : labelEl}
      {hint && <p id={`${id}-hint`} className={styles.hint}>{hint}</p>}
      {error && <p id={`${id}-error`} className={styles.error}><span className="visually-hidden">Error: </span>{error}</p>}
      <div className={styles.row}>
        <input ref={ref} id={id} name={id} className={styles.input}
          type={shown ? 'text' : 'password'} autoComplete={autoComplete}
          spellCheck={false} autoCapitalize="none" autoCorrect="off"
          aria-describedby={describedBy} aria-invalid={error ? true : undefined}
          value={value} onChange={e => onChange(e.currentTarget.value)} />
        <button type="button" className={styles.toggle} aria-controls={id}
          aria-label={shown ? 'Hide password' : 'Show password'}
          onClick={() => { setSaid(shown ? 'Your password is hidden' : 'Your password is visible'); setShown(!shown); }}>
          {shown ? 'Hide' : 'Show'}
        </button>
      </div>
      <p className="visually-hidden" aria-live="polite">{said}</p>
    </div>
  );
}
```

```tsx
// features/auth/SignIn.tsx: password step (passkey handler follows the same shape)
async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
  e.preventDefault();
  if (pending) return;                                  // aria-disabled button: ignore repeat taps
  const email = flow.email.trim();
  const fieldErrors = checkEmailAndPassword(email, password);   // empty / no "@" / empty
  setFieldErrors(fieldErrors);
  setFormError(null);
  if (hasAny(fieldErrors)) return setAttempt(n => n + 1); // effect on `attempt` focuses the summary
  setPending(true);
  try {
    const res = await api.post('/api/auth/login', { email, password });
    if (res.ok) { flow.passwordStepAt = Date.now(); setStep('code'); return; }
    setPassword('');                                     // keep email (3.3.7); clear password (GOV.UK)
    setFormError(res.status >= 500 ? COPY.serverError : COPY.signInFailed);  // 401 and 429 → generic
  } catch {
    setFormError(COPY.noConnection);                     // fetch rejected: offline
  } finally {
    setPending(false);
  }
  setAttempt(n => n + 1);
}

async function onPasskey() {
  if (pending) return;
  setPending(true); setFormError(null);
  try {
    const optionsJSON = await (await api.post('/api/auth/passkey/options')).json();
    const result = await startAuthentication({ optionsJSON });   // same click handler: keeps user activation
    const res = await api.post('/api/auth/passkey', { credentialJson: JSON.stringify(result) });
    if (res.ok) return onSignedIn(await res.json());
    setFormError(COPY.signInFailed); setAttempt(n => n + 1);
  } catch (err) {
    if ((err as Error).name !== 'NotAllowedError') { setFormError(COPY.signInFailed); setAttempt(n => n + 1); }
  } finally { setPending(false); }
}
```

```css
/* PasswordInput.module.css (shared tokens come from the app's :root) */
.input { font: inherit; font-size: max(1rem, 16px); min-block-size: 3rem; inline-size: 100%;
  padding: 0.5rem 0.75rem; border: 2px solid var(--color-input-border); background: var(--color-field); color: var(--color-text); }
.input:focus { outline: 3px solid var(--color-focus); outline-offset: 0; }
.row { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.row > .input { flex: 1 1 12rem; }
.toggle { min-inline-size: 4.5rem; min-block-size: 3rem; font: inherit; border: 2px solid var(--color-input-border); background: var(--color-surface); }
.toggle:focus-visible { outline: 3px solid var(--color-focus); outline-offset: 2px; }
.groupError { border-inline-start: 4px solid var(--color-error); padding-inline-start: 0.75rem; }
.groupError .input { border-color: var(--color-error); }
.error { color: var(--color-error); font-weight: 700; margin: 0 0 0.5rem; }
@media (hover: hover) { .toggle:hover { background: var(--color-surface-hover); } }
```

---

## Per-screen notes

- **View 1, Sign in.** Passkey first, because the design lists it first and recommends it. Both buttons use the primary
  style: each is the only action for the person using that method [Opinion]. If `browserSupportsWebAuthn()` is false,
  replace the passkey button with the "Passkeys don't work in this browser…" notice and keep the email form. Don't just
  hide it: a passkey-only user would have no way in and no explanation. If someone already signed in opens `/sign-in`,
  send them to Today.
- **View 2, Enter your code.** The same generic message as step 1 on any failure (A25). The 5-minute rule in Behaviour 4
  stops an endless loop. No countdown timer: authenticator apps already show one.
- **View 3, In place.** Same component; only the notice is added. The note behind keeps its in-memory text because it is
  hidden, not unmounted. Nothing new is stored on the device. The email is not pre-filled (the app does not hold it, and
  someone else may be signing in); password managers fill it.
- **View 4, Set up your account.** The name and email are shown as text, not disabled fields: disabled fields are low
  contrast, skipped by Tab, and look editable. Let the passkey button start the device prompt from its own click
  (user activation). On a laptop the browser may offer "use a phone": that is fine, nothing to build.
- **In-app email browsers.** Because 8.1 removes the token from the address bar, an in-app browser's "Open in browser"
  menu opens `/setup` with **no** token. That is why the copy says "Go back to the email and open the link…", and why
  the "no link in memory" message exists. Show this message when WebAuthn is missing, or when `startRegistration` fails
  with anything other than `NotAllowedError`.
- **View 5, Create a password.** One field, no confirm field, no strength meter, no rules list beyond "at least 12
  characters" (A22). Hidden by default. Phone keychains will offer a strong password because of `new-password` plus the
  hidden username. The password manager may offer to save on Continue, before setup finishes. That is acceptable: the
  password is the one the person chose.
- **View 6, Set up your authenticator app.** Password first, code last, so the code is fresh when "Finish setup" sends
  both (§6.2). On a phone the QR code can't be scanned by the same phone, so step 2 names the setup key, and the key is
  easy to copy. A wrong code here may say so plainly; the generic-message rule (A25) is about signing in.
- **View 7, Messages.** Each is a heading plus one sentence, with no form and no dead-end button. The expired-link page
  is where people land if they reuse an old invite email after setting up, so a "Sign in" link there would stop needless
  resets. It is listed as an open question, because it is not in the design.

---

## Anti-patterns to avoid

- Six one-digit boxes for the code; auto-advance; auto-submit on the 6th digit (3.3.8 failure; 3.2.2 risk).
- `type="number"` for the code; `maxLength={6}` (rejects a pasted "123 456").
- Blocking paste, `autocomplete="off"`, or made-up tokens such as `autocomplete="email-address"`.
- Placeholders as labels, or example values inside fields.
- Messages that name the problem at sign-in: "Wrong password", "No account with that email", "Account locked",
  "Account deactivated", or a lockout countdown (A25).
- Clearing the email after a failure (3.3.7). Using `<form action>`, which does this silently in React 19.
- Showing the password by default, or using an eye icon with no text or name.
- A "Confirm password" field, composition rules, a strength meter, or a `maxLength` below 64.
- A "Forgot password?" link, "Remember me", "Keep me signed in", or "Trust this device" (A22, 8.2).
- `disabled` on a pending button; a full-screen spinner over the form; errors in toasts or modal dialogs.
- Live regions created at the moment of the error (`{err && <div role="alert">}`); moving focus without a user action.
- `autofocus` on the sign-in email field (the keyboard hides the passkey option).
- Email, password, setup token or key in `localStorage`, `sessionStorage`, router state, URL query strings or logs.
- Hiding the passkey option without saying why; treating `NotAllowedError` as a failed sign-in.
- The parent company's name anywhere: titles, alt text, hidden text, passkey or authenticator labels (D42).

---

## Tensions with decisions

1. **Passkey autofill vs a passkey button (design.md 4.1).** FIDO's research (Blink UX, repeated rounds) found "autofill
   ensured the highest success" for passkey sign-in. web.dev describes it as `autocomplete="username webauthn"` plus a
   pending `navigator.credentials.get({ mediation: 'conditional' })`. Design 4.1 specifies a "Sign in with a passkey"
   button, which FIDO also supports when several methods share a page. This file builds the button as designed.
2. **"It never says which part was wrong" (A25) vs the two-step flow (§6.2).** `POST /api/auth/login` returns
   `{next:"totp"}` only after a correct password, so reaching the code step shows the password was right. The wording
   stays generic everywhere. The authenticator code still protects the account, and this is how ASP.NET Core Identity's
   two-step sign-in works. Recorded so the claim is not read as absolute.

---

## Sources

- NIST SP 800-63B-4 §3.1.1.2, Password verifiers. https://pages.nist.gov/800-63-4/sp800-63b.html
- W3C, Understanding SC 3.3.8 Accessible Authentication (Minimum). https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html
- W3C, Understanding SC 3.3.7 Redundant Entry. https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- W3C, Understanding SC 3.3.3 Error Suggestion. https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html
- W3C, Understanding SC 2.2.1 Timing Adjustable. https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- W3C, Understanding SC 1.3.5 Identify Input Purpose. https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html
- MDN, HTML `autocomplete` attribute. https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete
- GOV.UK Design System, Password input. https://design-system.service.gov.uk/components/password-input/
- GOV.UK Design System, Passwords pattern. https://design-system.service.gov.uk/patterns/passwords/
- GOV.UK Design System, Create accounts. https://design-system.service.gov.uk/patterns/create-accounts/
- GOV.UK Design System, Validation. https://design-system.service.gov.uk/patterns/validation/
- GOV.UK Design System, Error summary. https://design-system.service.gov.uk/components/error-summary/
- GOV.UK Design System, Error message. https://design-system.service.gov.uk/components/error-message/
- GOV.UK Design System, Question pages. https://design-system.service.gov.uk/patterns/question-pages/
- GOV.UK Design System, Confirm a phone number (security code). https://design-system.service.gov.uk/patterns/confirm-a-phone-number/
- GOV.UK Service Manual, Structuring forms. https://www.gov.uk/service-manual/design/form-structure
- GDS blog, "How we made it easier for millions of users to sign into government services", 16 September 2026. https://gds.blog.gov.uk/2026/09/16/how-we-made-it-easier-for-millions-of-users-to-sign-into-government-services/
- FIDO Alliance / Passkey Central, Design guidelines (research method). https://www.passkeycentral.org/design-guidelines/
- Passkey Central, Sign in with a passkey. https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey
- Google for Developers, Passkeys user interface design. https://developers.google.com/identity/passkeys/ux/user-interface-design
- web.dev, Sign-in form best practices. https://web.dev/articles/sign-in-form-best-practices
- web.dev, Sign in with a passkey through form autofill. https://web.dev/articles/passkey-form-autofill
- Chromium, Create amazing password forms. https://www.chromium.org/developers/design-documents/create-amazing-password-forms/
- Chromium, Password form styles that Chromium understands. https://www.chromium.org/developers/design-documents/form-styles-that-chromium-understands/
- passkeys.dev, Android reference (WebViews, Custom Tabs). https://passkeys.dev/docs/reference/android/
- passkeys.dev, iOS reference (WKWebView, ASWebAuthenticationSession). https://passkeys.dev/docs/reference/ios/
- SimpleWebAuthn, `@simplewebauthn/browser`. https://simplewebauthn.dev/docs/packages/browser
- React, `<form>` reference (automatic reset after a successful action). https://react.dev/reference/react-dom/components/form
- ASP.NET Core source, `IdentityCookiesBuilderExtensions.cs` (two-factor cookie `ExpireTimeSpan` 5 minutes). https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs
- Hu, Alroomi, Sahin, Li, "Unmasking the Security and Usability of Password Masking", ACM CCS 2024. https://doi.org/10.1145/3658644.3690333
- Sherwin, "Password Creation: 3 Ways To Make It Easier", NN/g, 2015. https://www.nngroup.com/articles/password-creation/
- Sutton, "What we learned from user testing of accessible client-side routing techniques", Gatsby, 2019. https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- Judis, "Mobile Safari doesn't zoom into form inputs with minimum 16px" (practitioner note). https://www.stefanjudis.com/notes/mobile-safari-doesnt-zoom-into-form-inputs-with-minimum-16px/
- Not reachable in this pass: the Australian Government Design System site (DNS failure), so AgDS guidance is not cited.
