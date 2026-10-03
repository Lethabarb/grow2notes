# Sign-in and account setup

Screen spec for design.md **§4.1**, plus the "sign in again in place" view from §4.0 Sessions and §8.5. It puts
together the researched component files into one buildable screen. It adds no feature, field, screen, setting,
notification or stored data. Rules it keeps: D21–D23, D42, A22–A25, A32, §6.2, §8.1–8.6, §9.6–9.9.

**Evidence tags:** **[Research]** studies and usability testing · **[Standard]** WCAG 2.2, WAI-ARIA, HTML/WebAuthn
specs, NIST · **[Convention]** established design systems and browser-vendor guidance · **[Opinion]** reasoned
judgement. Behaviour of ASP.NET Core Identity is quoted from its source as read by the component files. It is a fact
about the product, not graded evidence.

**Copy marks:** **(V)** word for word from design.md · **(N)** design.md wording, split or punctuated per
[microcopy.md](../components/microcopy.md) · **(P)** proposed, not in design.md, needs the owner's OK (all listed under
Open questions). No string, title, alt text, hidden text, passkey name or authenticator label ever contains the parent
company's name (D42).

---

## Purpose and who uses it

**Purpose (design.md 4.1):** let invited people in, with MFA always on (D23). It has two jobs:

1. **Account setup**, used once per person (and again after a manager's Reset sign-in). The person arrives from the
   emailed setup link, almost always on **their own phone** (D21), and chooses one sign-in method for good (A22):
   **a passkey** (recommended) or **a password plus an authenticator app**.
2. **Sign in**, used at the start of every shift and after the 30-minute idle sign-out. Passkey users tap one button.
   Password users type email and password, then a 6-digit code.

**Who:** every worker and manager. Workers are often tired, at the end of a long shift, sometimes in a participant's
home or in public. Many write English as a second language, digital confidence is mixed, and this is where the
most unfamiliar words in the app cluster (passkey, authenticator app, setup key, QR code). Managers also sign in on
laptops, including shared ones (§8.2).

**What the screen never does** (A22, A23, §8.2): no "Forgot password", no "Remember me" or "remember this device", no
recovery codes, no self-service method change, no sign-up, no SSO. Recovery is always a manager's **Reset sign-in**.

**Routes.** Sign-in has **no URL of its own**: when there is no session, it renders at whatever URL was requested, and
that route renders after sign-in (no redirect, no `returnUrl`; §4.0 URLs hold IDs only). Setup uses `/setup` (from
the emailed `/setup#u=…&t=…`), `/setup/password` and `/setup/app`.

---

## Layout - phone (about 375 px)

One column, 16 px side gutters, nothing sticky or fixed. The header shows only the wordmark "Grow2Notes" as plain text
(no nav, no Account) on every view here ([app-shell-nav.md](../components/app-shell-nav.md)). An error summary appears
at the top of `<main>`, above the `<h1>`, only for field errors and the A25 "Sign-in failed" message. Request failures
(no connection, server error, a passkey that did not work) show in an always-present `role="alert"` line directly
above the button that was pressed, and focus stays on that button.

**A. Sign in** (step 1)

```
+-----------------------------------------+
| Grow2Notes                              |  <header>, wordmark as plain text
+-----------------------------------------+
| [There is a problem ...]                |  error summary, only after a failure
| Sign in                            (h1) |
| Sign in the way you chose when you set  |  hint (P)
| up your account.                        |
|  (empty)                                |  <p role="alert">: passkey request failures
| +-------------------------------------+ |
| |        Sign in with a passkey       | |  PRIMARY <button type=button>, 48 px,
| +-------------------------------------+ |  outside the form
|                    or                   |
| Email address                           |
| +-------------------------------------+ |  type=email, 48 px, 18 px text
| |                                     | |
| +-------------------------------------+ |
| Password                                |
| +-------------------------------------+ |  type=password, 48 px; no Show/Hide here
| |                                     | |  unless approved (Optional, not built)
| +-------------------------------------+ |
|  (empty)                                |  <p role="alert">: request failures
| +-------------------------------------+ |
| |               Continue              | |  SECONDARY, type=submit
| +-------------------------------------+ |
| If you cannot sign in, ask a manager to |  help line (P)
| reset your sign-in.                     |
+-----------------------------------------+
```

**B. Sign in, code step** (replaces A's content in the same place; same URL; no passkey button)

```
| Grow2Notes                              |
| [There is a problem ...]                |  only for "Sign-in failed" (A25)
| Enter the 6-digit code from your   (h1, |
| authenticator app               =label) |
| Open your authenticator app and find    |  hint
| Grow2Notes. If the code changes while   |
| you're typing, you can still use it.    |
| [Error: The code must be 6 digits]      |  inline, only for a format error
| +------------+                          |  ~10ch wide, 24 px bold digits
| |  123 456   |                          |  inputmode=numeric
| +------------+                          |
| +-------------------------------------+ |
| |               Sign in               | |  PRIMARY, type=submit, directly under
| +-------------------------------------+ |  the field (number pad may lack Return)
| If you cannot sign in, ask a manager to |
| reset your sign-in.                     |
```

**C. Signed out, in place** (after any `401`; the page behind stays mounted but `hidden`)

```
| Grow2Notes                              |
| You've been signed out             (h1) |  (P) focus lands here
| Sign in again to go back to where you   |  (P)
| were.                                   |
| ...then exactly A's content (hint,      |
| passkey button, or, email, password,    |
| Continue, help line), then B's code     |
| step with its label as a plain <label>  |
| styled as h2.                           |
```

**D. Setup link check and setup messages** (`/setup`)

```
| Grow2Notes                              |     | Grow2Notes                              |
| Set up your account                (h1) |     | This link has expired              (h1) |
| Checking your link…                     |     | Ask a manager to send a new one.        |
|   (role=status, after 1 s)              |     |   (no button: retrying cannot help)     |
```

If the check cannot reach the server, the status line becomes the failure sentence and a primary **Try again**
button appears under it (outside the status element).

**E. Set up your account** (`/setup`, after the link is accepted)

```
| Grow2Notes                              |
| Set up your account                (h1) |  (no summary: failures show in the alert
|                                         |  line above the passkey button)
| Name                                    |  <dl>; term and value stack at 375 px,
| Sam Lee                                 |  sit side by side from 40rem
| Email address                           |
| sam.lee@example.org                     |
| If these are wrong, ask a manager.      |  hint (P)
| Choose how you'll sign in          (h2) |
| +-------------------------------------+ |
| |     Use a passkey (recommended)     | |  PRIMARY <button>: starts the
| +-------------------------------------+ |  device's passkey prompt
| You'll sign in with your face,          |  hint (P)
| fingerprint or PIN, the same way you    |
| unlock your phone. Set it up on the     |
| phone you'll use for Grow2Notes.        |
|                    or                   |
| +-------------------------------------+ |
| |  Use a password and authenticator   | |  SECONDARY, router <Link> to
| |                app                  | |  /setup/password (label wraps)
| +-------------------------------------+ |
| You'll create a password and use a free |  hint (P)
| app that shows a 6-digit code each time |
| you sign in.                            |
```

If the browser has no WebAuthn at all, the passkey button and its hint are replaced by the notice **"Open this link in
your browser"** (V) with its body, and "Use a password and authenticator app" becomes the primary style.

**F. Create a password** (`/setup/password`)

```
| Grow2Notes                              |
| ‹ Set up your account                   |  BackLink in the shell's bar slot, to /setup
| Create a password           (h1=label)  |
| Use at least 12 characters.             |  hint
| [Error: ...]                            |  inline, only after Continue
| +---------------------------+ +-------+ |
| |                           | | Show  | |  autocomplete=new-password
| +---------------------------+ +-------+ |
|   (hidden username field = the email)   |  not visible, not focusable
| +-------------------------------------+ |
| |               Continue              | |  PRIMARY, type=submit (no request)
| +-------------------------------------+ |
```

**G. Set up your authenticator app** (`/setup/app`)

```
| Grow2Notes                              |
| ‹ Create a password                     |  BackLink in the shell's bar slot, to /setup/password
| Set up your authenticator app      (h1) |  (no summary: one field; failures inline or in the alert)
| An authenticator app is a free app on   |
| your phone. It shows a new 6-digit code |
| every 30 seconds. You enter the code    |
| after your password each time you sign  |
| in.                                     |
| 1. Get an authenticator app on your     |  <ol>
|    phone if you do not have one, for    |
|    example Google Authenticator or      |
|    Microsoft Authenticator. Any         |
|    authenticator app works.             |
| 2. Add Grow2Notes to the app. Scan the  |
|    QR code, or use the setup key.       |
|    +--------------------+               |
|    |                    |  200 x 200,   |  white panel with quiet zone,
|    |      QR code       |  <img alt>    |  color-scheme: only light
|    +--------------------+               |
|    Using this phone? Type the setup key |
|    into the app. If the app asks for a  |
|    name, type Grow2Notes.               |
|    Setup key                            |
|    ABCD EFGH IJKL MNOP                  |  monospace, groups never split,
|    QRST UVWX YZ23 4567                  |  selectable; copies with no spaces
|    Your app might call this a "key" or  |
|    "secret key". You do not need to     |
|    type the spaces.                     |
| 3. Come back to this page.              |
|    Enter the 6-digit code from your     |  <label>
|    authenticator app                    |
|    Find Grow2Notes in the app. If the   |  hint
|    code changes while you're typing,    |
|    you can still use it.                |
|    +------------+                       |
|    |            |                       |
|    +------------+                       |
|  (empty)                                |  <p role="alert">: request failures
| +-------------------------------------+ |
| |             Finish setup            | |  PRIMARY, type=submit
| +-------------------------------------+ |
```

---

## Layout - laptop

What changes at `@media (min-width: 40rem)` ([foundations.md](../components/foundations.md)): nothing is added,
hidden or reordered, so the DOM order equals the visual order on every device (SC 1.3.2, 2.4.3).

- **Column.** Content sits in one `--measure` (40rem) column, **left-aligned** inside the `--page-max` (60rem) page
  container, so it shares a left edge with the wordmark. These are not "setup screens that use the extra space"
  (that phrase in §4.0 means the Manage screens). [Convention: foundations.md single-breakpoint system]
- **Type and spacing.** `h1` grows from 28 px to 32 px; gutters and section gaps grow per the tokens. Body stays 18 px.
- **Buttons** become as wide as their label (minimum 8rem), left-aligned, still 48 px tall
  ([primary-actions.md](../components/primary-actions.md)). Each setup method button keeps its hint directly under it,
  so the two stay stacked blocks, not a side-by-side group.
- **Text inputs** fill the column. The code field stays about 10ch wide at every size, so it reads as "a short code".
  [Convention: GOV.UK fixed-width inputs for known-length content]
- **Show/Hide** (view F) sits beside the password field (it only wraps underneath at 320 px or 200% text).
- **QR code** is the natural route on a laptop (scan it with the phone). Same size and place; the setup key stays
  visible next to it in the same column.
- **The operating system's passkey dialog** may offer "use a phone or tablet" (QR code, Bluetooth on both devices) on a
  laptop without Windows Hello. The browser draws that; the page says nothing about it (onboarding covers it).
  [Convention: FIDO cross-device guidance, https://www.passkeycentral.org/design-guidelines/optional-patterns/cross-device-sign-in]
- Hover styles exist only under `@media (hover: hover) and (pointer: fine)`.

---

## Components, in order

Tokens throughout: [foundations.md](../components/foundations.md) (system font, 18 px body, `--colour-text #0b0c0c`,
`--colour-error #a4000f`, `--colour-action #00558b`, 2 px `--colour-border-control` on inputs, 3 px near-black focus
outline with 2 px offset on `:focus-visible`, targets `max(3rem, 48px)` for buttons and inputs). Words:
[microcopy.md](../components/microcopy.md) (glossary, no negative contractions in new copy, sentence case, `en-AU`).
Every visible string lives in `src/copy`.

| # | Component | Used on | Screen-specific settings |
|---|---|---|---|
| 1 | Signed-out shell ([app-shell-nav.md](../components/app-shell-nav.md)) | All views | Skip link, `<header>` with the wordmark "Grow2Notes" as plain text (not a link), `<main id="main-content" tabindex="-1">`. No nav, no Account. Page titles from the fixed list only: **"Grow2Notes – Sign in"** (views A, B, C) and **"Grow2Notes – Set up your account"** (D to G). After a failure, prefix "Error: ". `<html lang="en-AU">`. |
| 2 | ErrorSummary ([form-validation.md](../components/form-validation.md)) | A, B, C | First child of `<main>`. `tabIndex={-1}`, `<h2>` "There is a problem", `<ul>`. Field errors are links whose text equals the inline message. **The A25 "Sign-in failed" message is the one unlinked item**: it is a credential outcome, not a request failure, and marks no field invalid (form-validation.md's stated exception). Focus moves to it on every failed attempt that shows it. No `role="alert"` (focus does the announcing). Request failures never go here (row 2a). |
| 2a | Request-failure alert ([primary-actions.md](../components/primary-actions.md), [form-validation.md](../components/form-validation.md)) | A, B, C, E, G | An always-present, empty `<p role="alert">` directly above each button that sends a request: above **Sign in with a passkey** (A, C), above **Continue** (A, C), above **Sign in** (B, C), above **Use a passkey (recommended)** (E), above **Finish setup** (G). It holds no-connection and server failures, passkey ceremony errors and the setup `429`. Bold, `--colour-error`, words first. Focus stays on the pressed button, so pressing it again retries. Cleared on the next press. |
| 3 | Page heading and hint | A, C, E | A: `h1` "Sign in" + hint (P). C: `h1` "You've been signed out" (P) + "Sign in again to go back to where you were." (P), then A's hint. E: `h1` "Set up your account" (V). `h1` has `tabIndex={-1}` for route focus. |
| 4 | Passkey sign-in button ([passkey-flows.md](../components/passkey-flows.md), [primary-actions.md](../components/primary-actions.md)) | A, C | `<Button type="button" variant="primary">` "Sign in with a passkey" (V), **outside** the email/password `<form>`. No icon (see Conflicts). Busy label "Signing in…" (P). Options prefetched on render (Interactions 2). If `browserSupportsWebAuthn()` is false, render instead the static notice "Passkeys do not work in this browser. To use your passkey, open Grow2Notes in your phone's main browser, such as Safari or Chrome." (P) and drop the "or". |
| 5 | "or" divider | A, C, E | Plain `<p>` "or" (P), read by screen readers; any rule lines are CSS pseudo-elements. |
| 6 | EmailInput ([sign-in-form.md](../components/sign-in-form.md)) | A, C | `<label>` "Email address". `id`/`name="email"`, `type="email"`, `autoComplete="username"`, `spellCheck={false}`, `autoCapitalize="none"`, `autoCorrect="off"`. Trimmed before sending. Not pre-filled, no placeholder, no `autoFocus`. |
| 7 | PasswordInput ([sign-in-form.md](../components/sign-in-form.md)) | A, C (`current-password`); F (`new-password`) | One shared component. Label "Password" (A, C) or "Create a password" inside the `h1` (F). **Show/Hide on F only** (design 4.1: "set a password of at least 12 characters, with show/hide"): a 48 px "Show"/"Hide" button with accessible names "Show password"/"Hide password" (visible word first, SC 2.5.3), `aria-controls`, and a persistent visually hidden `aria-live="polite"` line saying "Your password is visible"/"Your password is hidden" (empty on mount); back to `type="password"` on form submit. On A and C the field has no toggle unless the owner approves it (Optional, not built). `spellCheck={false}`, `autoCapitalize="none"`, `autoCorrect="off"`. Never trimmed, no `maxLength`. F only: `minLength={12}` (guides password generators; `noValidate` stops it blocking) and a hidden `<input type="email" name="username" autoComplete="username" value={email} readOnly hidden>` inside the form. |
| 8 | Continue button ([primary-actions.md](../components/primary-actions.md)) | A, C (secondary, busy "Checking…" (P)); F (primary, no request, no busy) | `type="submit"`. Never `disabled`. |
| 9 | Help line ([microcopy.md](../components/microcopy.md) §9) | A, B, C | Plain `<p>`: "If you cannot sign in, ask a manager to reset your sign-in." (P). Not a link. |
| 10 | OneTimeCodeField ([totp-setup.md](../components/totp-setup.md)) | B (label inside `h1`); C (plain label styled as h2); G (plain label inside step 3) | One `<input>`: `type="text"`, `id`/`name="code"`, `inputMode="numeric"`, `autoComplete="one-time-code"`, `enterKeyHint="go"`, `spellCheck={false}`, `autoCapitalize="off"`, `autoCorrect="off"`. No `maxLength`, `pattern` or placeholder; never auto-submits. 24 px bold tabular digits, letter-spacing 0.15em, `inline-size: 10ch`, 48 px tall, `scroll-margin-block` so it is never under anything. Label "Enter the 6-digit code from your authenticator app" (P). Hints: B/C "Open your authenticator app and find Grow2Notes. If the code changes while you're typing, you can still use it." (P); G "Find Grow2Notes in the app. If the code changes while you're typing, you can still use it." (P). |
| 11 | Code-step submit ([primary-actions.md](../components/primary-actions.md)) | B, C | Primary "Sign in" (V), busy "Signing in…" (P). |
| 12 | Setup link check ([empty-loading-error.md](../components/empty-loading-error.md)) | D | One `<p role="status">` present from first render: "Checking your link…" (P) after 1 s, then the failure sentence. **Try again** (primary, busy "Checking…") sits outside it. Whole-page messages are an `h1` (`tabIndex={-1}`) and one sentence, with no button. |
| 13 | Account summary | E | `<dl>`: "Name" / display name, "Email address" / email, as text (not disabled or read-only inputs). Hint "If these are wrong, ask a manager." (P). |
| 14 | Method choice ([passkey-flows.md](../components/passkey-flows.md)) | E | `h2` "Choose how you'll sign in" (P, from design's "Choose"). Primary `<Button type="button">` "Use a passkey (recommended)" (V), busy "Setting up your account…" (P), `aria-describedby` its hint. Secondary router `<Link>` styled as a button, "Use a password and authenticator app" (V), `aria-describedby` its hint. Accessible names equal the visible labels (no `aria-label`). |
| 15 | Back link (the shared BackLink, [app-shell.md](app-shell.md) component 3a) | F, G | Labelled with the destination: "‹ Set up your account" on F (to `/setup`), "‹ Create a password" on G (to `/setup/password`; the password is still in memory and shown again) (P). Rendered into the shell's before-main bar slot (GOV.UK back-link position), so the error summary or the `h1` is the first thing in `<main>`. Pops when the previous entry is the destination, otherwise pushes. |
| 16 | Authenticator setup block ([totp-setup.md](../components/totp-setup.md)) | G | `h1` "Set up your authenticator app" (P), intro, `<ol>` of 3 steps (copy in States). QR `<img src="/api/auth/setup/authenticator/qr.png" width="200" height="200" alt="QR code for adding Grow2Notes to your authenticator app">` on a white panel with the 4-module quiet zone, container `color-scheme: only light`, `image-rendering: pixelated`. SetupKey: term "Setup key", key in capitals, `--font-family-code`, groups of 4 as separate `<span>`s (no space characters), `user-select: all`, `translate="no"`. No Copy button in the default build (design 4.1: "type the setup key"; see Optional, not built). |
| 18 | Page status region ([app-shell.md](app-shell.md) component 3) | All views | The shared visually hidden `<p role="status">` (`PageStatus`), present from the first render. The shared Button writes its busy labels here ("Checking…", "Signing in…", "Setting up your account…", "Finishing setup…"). There is no app-level region. |
| 17 | Finish setup ([primary-actions.md](../components/primary-actions.md)) | G | Primary "Finish setup" (V), busy "Finishing setup…" (P), `type="submit"` of the code form. |

React Aria is not needed anywhere on this screen: every control is a native `<button>`, `<a>`, `<input>` or `<form>`.

### Optional, not built unless approved

Each item below was proposed by a component file but is not in design.md 4.1. The default build leaves it out, and
nothing else depends on it. If the owner approves one, add it exactly as described.

- **(P) Show/Hide on the sign-in password (views A and C).** The same PasswordInput toggle as view F. Design 4.1 gives
  show/hide only when creating a password. Evidence for adding it: NIST SP 800-63B says verifiers SHOULD offer to show
  the secret; Understanding 3.3.8 says it "can improve the chance of success". If approved: the toggle follows the
  password field in Tab order (A and C: passkey → email → password → Show → Continue), and the AC "Show reveals the
  password…" applies to A and C too.
- **(P) Copy setup key button (view G).** A secondary button under the key that calls `navigator.clipboard.writeText`
  inside the click, with a persistent `role="status"` line: "Setup key copied." / "Could not copy. Press and hold the
  key to copy it, or type it." (P). Design 4.1 says "type the setup key". Reason it was proposed: on a phone the
  person cannot scan their own screen. **Clipboard concern:** the key is the authenticator's shared secret. On the
  clipboard it can be kept by Windows clipboard history or synced by iOS Universal Clipboard to the person's other
  devices, which sits badly with §9.6 and D22 ("nothing stored on the device"). Selecting and copying the key by hand
  does the same, but only by the person's own choice.

---

## States

Every state from design.md 4.1, plus loading, empty and error. Copy is exact. "Summary" means the ErrorSummary
(heading "There is a problem") at the top of `<main>`.

### Sign in (views A, B, C)

| State | Trigger | What shows (exact copy) | Focus | Announced |
|---|---|---|---|---|
| Default | No session (`/me` → `401` on start-up) | View A. No loading state: the screen loads no data. | Where the browser puts it on first load (no `autoFocus`: the keyboard would cover the passkey button) | Nothing |
| Already signed in | `/me` → `200` | The requested route, not sign-in | — | — |
| Passkeys unavailable | `browserSupportsWebAuthn()` false | Notice in place of the passkey button: "Passkeys do not work in this browser. To use your passkey, open Grow2Notes in your phone's main browser, such as Safari or Chrome." (P). Password form unchanged. | — | Read in normal order (static) |
| Empty fields on Continue | Client check | Inline under each label and linked in the summary: "Enter your email address" (P) · "Enter your password" (P) | Summary | Summary on focus |
| Email has no "@" with text both sides | Client check | "Enter an email address in the correct format, like name@example.com" (P, GOV.UK) | Summary | Summary on focus |
| Busy | Continue / passkey / Sign in pressed | Button `aria-disabled="true"`; after 400 ms its label becomes "Checking…" (Continue) or "Signing in…" (passkey, code step). No spinner. | Stays on the button | Busy label through the page status region (`PageStatus`) |
| **Sign-in failed** (design) | `401` or `429` from `/api/auth/login`, `/api/auth/login/totp` or `/api/auth/passkey` | Summary, unlinked: **"Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes."** (V). No field marked invalid. Password step: email kept, **password cleared**. Code step: code cleared, stays on step B. | Summary | Summary on focus |
| Code step expired | `401`/`429` on the code step **4 min 50 s or more** after the password was accepted | Back to step A with the email kept, password empty, and the same design message in the summary. Nothing says why. | Summary | Summary on focus |
| Code empty / wrong format (one-field form) | Client check after removing spaces, hyphens and dashes (NFKC) | Inline only: "Enter the 6-digit code from your authenticator app" (P) · "The code must be 6 digits" (P). `aria-invalid` on the field. Nothing is sent, so no lockout attempt is used. | The code field | Label, "invalid", hint, "Error: …" via `aria-describedby` |
| Passkey prompt dismissed | `NotAllowedError` (cancel, timeout or no passkey: the page cannot tell which, WebAuthn §14.5) | **Nothing.** Button back to default. | Stays on the button | Nothing |
| Passkey other client error | Any other WebAuthn error | Alert above the passkey button: "Not signed in: something went wrong. Try again." (P) | Stays on the passkey button | The alert (assertive) |
| No connection | `fetch` rejects or times out | Alert above the pressed button (passkey, Continue or Sign in): "Not signed in: no connection. Try again." (P). Typed email kept; password kept (it was never checked). Not in the summary. | Stays on the pressed button | The alert (assertive) |
| Server error | `5xx` | Alert above the pressed button: "Not signed in: something went wrong. Try again." (P). Not in the summary. | Stays on the pressed button | The alert (assertive) |
| Signed out in place | Any `401` from the app while signed in | View C: `h1` "You've been signed out" (P), "Sign in again to go back to where you were." (P), then view A's content. Route behind is mounted but `hidden`; autosave paused. Title "Grow2Notes – Sign in". | The `h1` | Heading on focus |
| Success, fresh load | `200 Me` | The requested route (Today when it was `/`), via `navigate(..., { replace: true })` | That route's `h1` (app shell rule) | Heading on focus |
| Success, in place, same person | `200 Me`, `userId` matches | Route unhidden, queries refetched, autosave retries the pending save | The page's `h1` (note form: the participant name heading) | Heading on focus |
| Success, in place, different person | `200 Me`, `userId` differs | `location.replace('/')`: full reload to Today, so nobody sees another person's unsaved text | Today's first-load default | — |

### Setup (views D, E, F, G)

| State | Trigger | What shows (exact copy) | Focus | Announced |
|---|---|---|---|---|
| Checking link | `POST /api/auth/setup/start` in flight | `h1` "Set up your account" (V); after 1 s, "Checking your link…" (P) | `h1` | Status text |
| Link check failed | Network failure or `5xx` | "Your setup link was not checked: no connection. Try again." or "Your setup link was not checked: something went wrong. Try again." (P), then **Try again** (primary). The token is still in memory, so retrying is safe. After a second failure the same sentence is cleared and rewritten so it is announced again. | Stays on Try again | Status text |
| **Link expired, used or replaced** (design) | `410 auth.setup_link_invalid` | Whole page: `h1` "This link has expired", then "Ask a manager to send a new one." (N, design sentence split). No button. | `h1` | Heading on focus |
| **Setup session timed out** (design) | `401` from any `/api/auth/setup/*` call (enrolment cookie gone after 30 minutes) | Whole page: `h1` "Your setup session timed out", then "Open the link from your email again." (N). No button. In-memory setup state cleared. | `h1` | Heading on focus |
| No link in memory | `/setup*` opened with no `#u=…&t=…` and nothing in memory (refresh, copied URL, an in-app browser's "Open in browser") | Whole page: `h1` "Set up your account", then "To set up your account, open the link from your email again." (P) | `h1` | Heading on focus |
| Choose a method | `setup/start` → `{email, displayName}` | View E | `h1` | Heading on focus |
| **In-app browser cannot create passkeys** (design) | `browserSupportsWebAuthn()` false | In place of the passkey button and its hint: heading (`h2`-styled `<p>` inside a bordered notice) **"Open this link in your browser"** (V), body "Passkeys cannot be set up here. Go back to the email and open the link in your phone's browser, such as Safari or Chrome." (P). "Use a password and authenticator app" becomes primary. | `h1` | Read in order (static) |
| Passkey busy | Tap "Use a passkey (recommended)" | Device prompt (Face ID, fingerprint, PIN, or "use a phone"); button `aria-disabled`, after 400 ms "Setting up your account…" (P) | Stays on the button | Busy label |
| Passkey not set up | `NotAllowedError`, an `AbortError` we did not start, `InvalidStateError`, any other WebAuthn error, or `400` from `/setup/passkey` | Alert above "Use a passkey (recommended)": "Your passkey was not set up. Try again, or open the link from your email in your phone's browser." (P). Both options stay usable; new options are prefetched. | Stays on the passkey button | The alert (assertive) |
| No connection / server error (any setup action) | `fetch` rejects / `5xx` | Alert above the pressed button: "Not set up: no connection. Try again." or "Not set up: something went wrong. Try again." (P) | Stays on the pressed button | The alert (assertive) |
| Create a password, empty | Continue with nothing typed | Inline: "Enter a password" (P) | The field | Via `aria-describedby` |
| Create a password, too short | Fewer than 12 characters (`value.length`, matching Identity's `RequiredLength`) | Inline: "Password must be 12 characters or more" (P) | The field | Via `aria-describedby` |
| Key loading | `GET /api/auth/setup/authenticator` in flight | After 1 s, a fixed-height "Loading…" line where the key goes; QR `<img>` reserves 200 × 200 | — | Status text |
| Key did not load | Network failure / `5xx` on the key | "The setup key did not load: no connection. Try again." (or "…: something went wrong. Try again.") (P) with **Try again** | Stays on Try again | Status text |
| QR image failed | `<img onError>` | Image hidden; "The QR code did not load. Use the setup key instead." (P) | — | Static |

| Setup code empty / wrong format | Client check | Inline: "Enter the 6-digit code from your authenticator app" (P) · "The code must be 6 digits" (P) | The field | Via `aria-describedby` |
| Setup code rejected | `422 validation.failed` with `errors.totpCode` (assumed shape, see Open questions) | Inline on the code field: "That code did not work. Enter the code the app shows for Grow2Notes now." (P). Code cleared, password kept in memory. | The field | Via `aria-describedby` |
| Password rejected by server | `422` with `errors.password` (only if client and server rules ever drift) | Navigate back to view F with that message inline | F's field | Via `aria-describedby` |
| Too many tries | `429` on a setup call | Alert above the pressed button: "Too many tries. Wait 1 minute, then try again." (P) | Stays on the pressed button | The alert (assertive) |
| Done | `200 Me` from `/setup/passkey` or `/setup/password` | Today (design step 5), `navigate('/', { replace: true })`; setup state cleared from memory | Today's `h1` | Heading on focus |

**Copy for view G, in order (all P except the button):** intro "An authenticator app is a free app on your phone. It
shows a new 6-digit code every 30 seconds. You enter the code after your password each time you sign in." · step 1
"Get an authenticator app on your phone if you do not have one, for example Google Authenticator or Microsoft
Authenticator. Any authenticator app works." · step 2 "Add Grow2Notes to the app. Scan the QR code, or use the setup
key." · "Using this phone? Type the setup key into the app. If the app asks for a name, type Grow2Notes." ·
"Setup key" · "Your app might call this a "key" or "secret key". You do not need to type the spaces." · step 3 "Come
back to this page." · field label and hint as in Components row 10 · button "Finish setup" (V).

**States that do not apply:** Disabled (no control on this screen is ever `disabled` or shown greyed out); Read-only
(name and email are plain text, not read-only inputs); Empty list (the screen shows no lists).

---

## Interactions and focus

### What happens on each action

1. **First load with no session.** The shell calls `GET /api/auth/me`; `401` renders view A at the requested URL. A
   `200` renders the route. No redirect, nothing stored.
2. **Sign in with a passkey.** On render (A or C), if WebAuthn exists, prefetch `POST /api/auth/passkey/options` and
   keep **one** options object, stamped with its time. On tap: guard re-entry, set busy, and if the options are under
   4 minutes old call `startAuthentication({ optionsJSON })` **straight from the tap with no `await` before it**;
   otherwise fetch, then call. Credential → `POST /api/auth/passkey {credentialJson}` → `200 Me` → success path. Mark
   the options spent once sent to the server, and prefetch fresh ones after any failure. Cancel any ceremony on
   unmount (StrictMode's double effect must stay silent). [Convention, vendor, unverified for current iOS: older
   WebKit dropped the tap's user activation across an awaited `fetch`,
   https://developers.yubico.com/WebAuthn/Concepts/Handle_WebKit_User_Gesture.html] [Standard: Identity keeps the
   ceremony state in one cookie and its `AuthenticatorTimeout` defaults to 5 minutes,
   https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0]
3. **Continue (email and password).** `preventDefault`; if busy, ignore. Trim the email; never trim the password. Run
   the client checks; if any fail, show inline errors plus the summary and send nothing. Otherwise
   `POST /api/auth/login {email, password}`. `200 {next:"totp"}` → record `passwordAcceptedAt = Date.now()` in memory,
   replace the form with step B (the form's removal also lets password managers offer to save). `401`/`429` → generic
   message, clear the password, keep the email.
4. **Sign in (code).** Normalise (NFKC, remove spaces, hyphens, dashes and minus signs), require exactly 6 ASCII
   digits, else inline error and nothing sent. `POST /api/auth/login/totp {code}` with `retry: false` semantics (never
   retried automatically: every attempt counts towards lockout). `200 Me` → success path. `401`/`429` → if
   `Date.now() − passwordAcceptedAt ≥ 4 min 50 s`, return to step A (email kept, password empty, generic message);
   otherwise stay, clear the code, generic message. [Product fact: Identity's two-factor cookie lasts 5 minutes and
   then every code fails without counting towards lockout,
   https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs]
5. **Sign-in success (any method).** Fetch a fresh antiforgery token (§9.8), `queryClient.setQueryData(['me'], me)`,
   then: fresh load → render the requested route with `replace: true`; in place → same `userId`: unhide the route,
   refetch, resume autosave (same `clientId`, next `seq`), restore the page title; different `userId`:
   `location.replace('/')`.
6. **Show / Hide (view F).** Toggles `type`; writes the announcement into the field's polite live line. Focus stays
   on the toggle. On submit the field returns to hidden.
7. **Setup arrival.** Read `u` and `t` from `location.hash` once (lazy `useState` initialiser, so StrictMode's second
   run still has them), then `history.replaceState(null, '', '/setup')` at once (§8.1), then
   `POST /api/auth/setup/start {userId, token}` with an `AbortController`. Keep the token in component memory only
   until the check succeeds (so Try again works), then drop it. `{email, displayName}` go into a setup context in React
   memory above the `/setup*` routes, never into storage, the URL or router `state`.
8. **Use a passkey (recommended).** When view E renders with passkeys offered, prefetch
   `POST /api/auth/setup/passkey/options` (one object, same 4-minute rule). Tap → `startRegistration` straight from
   the tap → `POST /api/auth/setup/passkey {credentialJson, name}` with a fixed `name` (no UI asks for one) →
   `200 Me` → Today. Never start the prompt on page load (SC 3.2.1, 3.2.2; FIDO found jumping straight into the OS
   dialog "disorienting", https://www.passkeycentral.org/design-guidelines/principles).
9. **Use a password and authenticator app.** A link to `/setup/password`. Start fetching the key
   (`GET /api/auth/setup/authenticator`: `staleTime: Infinity`, `gcTime: 0`, `refetchOnWindowFocus: false`,
   `retry: false`) so view G is ready.
10. **Continue (Create a password).** Client check only (no request): empty → "Enter a password"; under 12 → "Password
    must be 12 characters or more". Pass → store the password in the setup context (memory) and go to `/setup/app`.
11. **Setup key.** Shown as selectable text only; the default build has no Copy button (Optional, not built). If the
    owner approves the button: `navigator.clipboard.writeText(key)` called directly in the click with the key already
    in memory (Safari and Firefox require a user action; HTTPS only,
    https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText).
12. **Finish setup.** Code checked as in step 4 (inline errors, nothing sent). `POST /api/auth/setup/password
    {password, totpCode}` → `200 Me` → antiforgery refresh, `setQueryData`, clear the setup context, navigate to Today
    with `replace: true`. A wrong code clears the code only; the password is never cleared on this view.
13. **Leaving `/setup*`** for any reason clears the setup context (password, email, name) from memory.
14. **Back links** (F, G): the shared BackLink pops when the previous entry is the destination (so it is the same as
    browser Back), otherwise it pushes the destination.

### Focus order (Tab), matching the visual order

- **A / C:** skip link → (C: nothing in the heading area is focusable) → Sign in with a passkey → Email address →
  Password → Continue. The help line is text. After a failure the summary sits before all of these; its field
  links (if any) are reachable by Tab.
- **B:** code field → Sign in.
- **D (failure):** Try again.
- **E:** Use a passkey (recommended) → Use a password and authenticator app.
- **F:** back link "Set up your account" → password field → Show → Continue.
- **G:** back link "Create a password" → code field → Finish setup. The QR image and key text are not tab stops.

No positive `tabindex`; nothing traps focus; Enter in any single-line field submits its own form (implicit submission),
and never starts a passkey prompt because the passkey buttons are outside every form.

### Where focus goes after actions

| After | Focus goes to | Why |
|---|---|---|
| First load of A | Not moved (browser default) | App-shell first-load rule; no `autoFocus` (keyboard would hide the passkey button) [Opinion] |
| Any failure that shows the summary (field errors, A25) | The summary (`tabIndex={-1}`), on every attempt, even if the text is unchanged | [Convention: GOV.UK error summary] so a screen-reader user knows the press did something |
| A request failure (no connection, server, passkey error, setup `429`) | Stays on the pressed button; the alert above it is announced | form-validation.md and primary-actions.md app-wide rule: one more press retries, and the phone page does not scroll away from the button |
| A format error on a one-field view (B, F, G) | The field in error | [Convention: AgDS one-error rule, form-validation.md rule 6] |
| Password accepted (A → B) | The code field | The person pressed Continue; label is the `h1`, so the step's purpose is read with the field. iOS may not open the keyboard for focus set after a network response (unverified across versions); the field is directly under the hint, so one tap opens it. |
| Code step expired (B → A) | The summary | Same as any failure |
| Passkey dismissed | Stays on the passkey button | Nothing was removed; the person chose to stop |
| Busy | Stays on the pressed button (`aria-disabled`, never `disabled`, which would drop focus to `<body>`) | [Standard: HTML disabled controls swallow clicks, https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#enabling-and-disabling-form-controls:-the-disabled-attribute] |
| 401 anywhere in the app (→ C) | C's `h1` "You've been signed out" | The focused element is now hidden ([session-timeout.md](../components/session-timeout.md)) |
| Setup route change (D → E, E → F, F → G, Back) | The new view's `h1` (`tabIndex={-1}`), or the summary if it asks for focus | App-wide route rule ([app-shell-nav.md](../components/app-shell-nav.md)); [Research, n=5, Sutton 2019, https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/] |
| Whole-page message (expired, timed out, no link) | Its `h1` | [Convention: empty-loading-error.md] |
| Summary link selected | `preventDefault`, scroll the field's `<label>` into view, `focus({ preventScroll: true })`; URL and history unchanged | [Convention: govuk-frontend error-summary script] |
| Try again, Show/Hide (F) | Stays on the button | Result is announced by a status region |
| Success | Destination `h1` (or, in place, the page's own heading) | App-wide route rule |

### Live regions and announcements

All regions exist in the DOM from first render and are written into, never mounted on demand. [Standard: SC 4.1.3;
ui-build non-negotiable "a live region must exist before content is written into it"]

| Region | Politeness | Says |
|---|---|---|
| Page status region (`PageStatus`, [app-shell.md](app-shell.md) component 3; the shared Button writes into it) | Polite | Busy labels once shown: "Checking…", "Signing in…", "Setting up your account…", "Finishing setup…" |
| Request-failure alert above each sending button (Components row 2a) | Assertive (`role="alert"`) | No-connection, server and passkey failures; setup `429` |
| PasswordInput's visually hidden line (one per password field) | `aria-live="polite"` | "Your password is visible" / "Your password is hidden" (empty on mount, so nothing on load) |
| Setup link check `<p role="status">` | Polite | "Checking your link…" then the failure sentence; cleared and rewritten on a repeat failure |
| Key region `<p role="status">` (G) | Polite | "Loading…", then a key load failure if any |
| ErrorSummary | None (focus announces it) | "There is a problem", then the message(s) |

`role="alert"` only in the request-failure lines; no `aria-errormessage`, no `aria-busy`, no `aria-live` on inline
errors (it clips the next field's name: [Research] Roselli 2023, https://adrianroselli.com/2023/04/exposing-field-errors.html).

---

## Accessibility checklist

**Headings and landmarks**
- [ ] One `<h1>` per view: "Sign in" · "Enter the 6-digit code from your authenticator app" (B, the label inside it) ·
      "You've been signed out" (C) · "Set up your account" · "Create a password" (label inside it) · "Set up your
      authenticator app" · "This link has expired" · "Your setup session timed out".
- [ ] `h2` "Choose how you'll sign in" (E); `h2` "There is a problem" inside the summary.
- [ ] Landmarks: `<header>` (banner, wordmark only) and `<main id="main-content">`. No `<nav>` on these views. Skip
      link "Skip to main content" first in `<body>`.
- [ ] `<html lang="en-AU">`; page titles from the fixed list, "Error: " prefix while an error shows (SC 2.4.2).

**Labels and names**
- [ ] Every input has a visible `<label for>`; hints and errors are `<p id>` joined in `aria-describedby` (hint first,
      then error; the error id only while there is an error).
- [ ] No placeholder text anywhere on this screen.
- [ ] Every button's accessible name is its visible text; Show/Hide's name starts with the visible word ("Show
      password") (SC 2.5.3). The method buttons have no `aria-label`.
- [ ] `autocomplete`: `username`, `current-password`, `new-password` (SC 1.3.5) and `one-time-code` (for SC 3.3.8
      autofill; not on WCAG's input-purpose list).
- [ ] QR image has `alt="QR code for adding Grow2Notes to your authenticator app"`; the visible setup key is its text
      equivalent (SC 1.1.1).
- [ ] Name and email at setup are a `<dl>` of plain text.

**Keyboard**
- [ ] Everything works by keyboard alone; Tab order as listed under Interactions; visible 3 px focus outline on every
      stop, drawn with `outline` (survives forced colours).
- [ ] Enter submits the form the focus is in; it never triggers a passkey prompt.
- [ ] The OS passkey dialog returns focus to the button when it closes (check per browser on the device test).

**Screen reader**
- [ ] Failed attempts are announced by focusing the summary; format errors on one-field views by focusing the field.
- [ ] Busy states are announced politely; the password toggle state (F) is announced; request failures are announced
      from their alert line while focus stays on the button.
- [ ] A25 failures mark no field `aria-invalid`.
- [ ] Tested with NVDA + Chrome and VoiceOver on iOS before M0 closes (summary without `role="alert"` is [Opinion] in
      form-validation.md and must be confirmed).

**Visual**
- [ ] Text ≥ 4.5:1 (all text tokens reach 7:1 or more); input borders, the secondary button border, error borders
      and the focus ring ≥ 3:1 (SC 1.4.3, 1.4.11).
- [ ] Errors are shown by words, a hidden "Error:" prefix, a 4 px bar and a thicker border, not colour alone (SC 1.4.1).
- [ ] Targets: buttons, inputs and Show/Hide at least 48 px tall; nothing smaller than 44 × 44 px (A32, SC 2.5.8).
- [ ] Inputs at 16 px or more (they inherit 18 px), so iOS does not zoom.
- [ ] Reflow at 320 px and text at 200%: labels wrap, Show/Hide wraps under the field, key groups wrap between groups,
      nothing scrolls sideways (SC 1.4.4, 1.4.10, 1.4.12).
- [ ] No motion: no spinner, no transitions on focus or errors.
- [ ] QR code not inverted with Chrome or Samsung Internet page darkening on.

**WCAG 2.2 AA criteria this screen must meet:** 1.1.1, 1.3.1, 1.3.2, 1.3.5, 1.4.1, 1.4.3, 1.4.4, 1.4.10, 1.4.11,
1.4.12, 2.1.1, 2.1.2, 2.2.1 (the 5-minute two-factor step and the 30-minute enrolment window rely on WCAG's
"essential" reading for security time limits, https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html; the
messages tell people how to recover), 2.4.1, 2.4.2, 2.4.3, 2.4.6, 2.4.7, 2.4.11, 2.5.3, 2.5.8, 3.1.1, 3.2.1, 3.2.2
(no auto-submit, failure F36), 3.2.4, 3.3.1, 3.3.2, 3.3.3 (the A25 message uses the security exception), 3.3.7
(email kept; password and code cleared under the security and "no longer valid" exceptions), 3.3.8 (paste and
password managers allowed, one code field, passkeys as the non-cognitive route), 4.1.2, 4.1.3.

---

## Acceptance criteria

**Sign in**
- [ ] With no session, any URL shows view A at that URL; after signing in, that route renders. There is no
      `/sign-in` route and no `returnUrl`.
- [ ] Tab order on A is passkey button → email → password → Continue; nothing has focus on first load; the sign-in
      password has no Show/Hide toggle (Optional, not built).
- [ ] The passkey button is `type="button"` and outside the `<form>`; pressing Enter in the password field submits the
      password form and never opens a passkey prompt.
- [ ] Only "Sign in with a passkey" (A) and "Sign in" (B) use the primary style on the sign-in views.
- [ ] Empty email or password shows the inline messages and a summary with matching link text; no request is sent.
- [ ] A wrong password, a wrong code, a locked account, a deactivated account and a `429` all show exactly "Sign-in
      failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes." with no
      field marked invalid.
- [ ] After a failed password step the email is still filled in and the password field is empty.
- [ ] A correct password replaces the form with the code step at the same URL; focus is on the code field.
- [ ] Pasting "123 456", "123-456" or full-width digits into the code field signs in; "12345" or "abc" shows "The code
      must be 6 digits" and sends nothing (check the network log).
- [ ] The code is never submitted automatically after the 6th digit.
- [ ] A code `401` arriving 4 min 50 s or more after the password was accepted returns to step A with the email kept
      and the design message shown (test with a mocked clock and a server whose two-factor cookie is set to 5 minutes).
- [ ] Code requests are never retried automatically (one press = one request).
- [ ] Dismissing the passkey prompt shows no message and leaves focus on the button.
- [ ] With `window.PublicKeyCredential` removed, the passkey button is replaced by the "Passkeys do not work in this
      browser…" notice and the password form still works.
- [ ] Offline, Continue shows "Not signed in: no connection. Try again." in the alert line above Continue (not in the
      summary), focus stays on Continue, and both fields keep their values.
- [ ] A second tap while busy sends nothing extra; the button keeps focus and is never `disabled`.
- [ ] The busy label appears only if the request takes more than 400 ms, without changing the button's size.
- [ ] On view F, Show reveals the password, announces "Your password is visible", and the field is hidden again on
      submit.

**Sign in again, in place**
- [ ] A `401` during a note shows view C without changing the URL or reloading; the note route stays in the DOM with
      `hidden`, and its unsaved text is still in memory.
- [ ] Focus moves to "You've been signed out"; the title is "Grow2Notes – Sign in".
- [ ] Signing in as the same person returns to the same screen, focus on its heading, and the pending save succeeds.
- [ ] Signing in as a different person does a full reload to Today; none of the first person's text appears.

**Setup**
- [ ] Opening `/setup#u=…&t=…` removes the fragment from the address bar before the setup request completes, and the
      browser history never contains the token.
- [ ] `410` shows "This link has expired" / "Ask a manager to send a new one." with no button.
- [ ] A `401` from any setup call shows "Your setup session timed out" / "Open the link from your email again."
- [ ] Opening `/setup`, `/setup/password` or `/setup/app` with nothing in memory shows "To set up your account, open the
      link from your email again."
- [ ] Network failure on the link check shows the failure sentence and Try again; Try again succeeds without the
      fragment (token held in memory).
- [ ] View E shows name and email as text, then "Use a passkey (recommended)" (primary button) and "Use a password and
      authenticator app" (secondary link).
- [ ] With the CDP virtual authenticator (`hasResidentKey`, `hasUserVerification`, `isUserVerified: true`) the passkey
      path ends on Today; with `isUserVerified: false` it shows "Your passkey was not set up…" in the alert above the
      passkey button, focus stays on that button, and both options still work.
- [ ] With WebAuthn unavailable, view E shows "Open this link in your browser" and the password option is primary.
- [ ] Create a password rejects an empty value and 11 characters with the inline messages; 12 characters (any
      characters) continue; no confirm field, no strength meter, no rules list, no `maxLength`.
- [ ] The password form contains a hidden `username` field holding the email.
- [ ] View G shows the QR code and the setup key on every screen size; there is no Copy button; the key can be selected
      by long press and copies with no spaces.
- [ ] A wrong setup code shows "That code did not work. Enter the code the app shows for Grow2Notes now." on the code
      field, clears the code and keeps the password (Finish setup works with a new code without going back).
- [ ] Back on view G returns to Create a password with the password still filled in.
- [ ] Both setup paths end on Today, and Back from Today does not return to a setup or sign-in step.
- [ ] After setup, signing in a second time works for both paths (design §8.1 invariant test).

**Privacy and storage (D22, §9.6)**
- [ ] `localStorage`, `sessionStorage`, IndexedDB, cookies set by script, `history.state` and the URL never contain
      the email, password, code, setup token, setup key or method used (check in DevTools after each path).
- [ ] Credentials are sent by a direct API call, not through a TanStack `useMutation` (no password in the mutation
      cache); the setup key query is dropped from memory when view G unmounts.
- [ ] No `<form action={…}>` is used; fields are controlled and submitted with `onSubmit` + `preventDefault`.
- [ ] The D42 copy test (microcopy.md §8, case-insensitive match on the parent company's name) fails the build if
      that name appears in any string, title, alt text, passkey relying-party name or
      authenticator issuer (D42).

**Layout and accessibility**
- [ ] At 320 px and at 200% text nothing scrolls sideways; Show/Hide wraps under the field.
- [ ] axe reports no violations on views A–G; the keyboard pass and the NVDA/VoiceOver pass in the checklist are done.
- [ ] On a real iPhone and Android phone, setup from Gmail, Outlook and the built-in mail app is tried, and each
      in-app browser's passkey result is recorded (go-live device check).

---

## Conflicts resolved

Where the component files disagreed, this screen builds the choice below. "Cross-screen" items are reported to the
orchestrator so the other files can be aligned.

| # | Disagreement | Chosen | Why |
|---|---|---|---|
| 1 | **Password after a failed sign-in:** clear it (sign-in-form.md) vs keep it (form-validation.md, rule 5 and 4.1 note) | **Clear the password, keep the email** | GOV.UK, checked 1 Oct 2026: Password input "Clear any information entered into the password input." (https://design-system.service.gov.uk/components/password-input/) and Passwords pattern (https://design-system.service.gov.uk/patterns/passwords/) [Convention]. WCAG 3.3.7's security exception allows it [Standard]. |
| 2 | **Primary style on sign-in:** both the passkey button and the password button primary (sign-in-form.md) vs passkey primary, password button secondary (passkey-flows.md, primary-actions.md) | **Passkey primary; Continue secondary; "Sign in" on the code step primary** | One primary per page [Convention: GOV.UK, NHS button guidance]; two of three files; a password user is drawn by the fields, not the colour [Opinion, check in the pilot]. |
| 3 | **Password-step button label:** "Continue" (sign-in-form.md, primary-actions.md) vs "Sign in" (passkey-flows.md) | **"Continue"** | A code step follows [Convention: GOV.UK question pages]; "Sign in" stays on the step that signs in. |
| 4 | **Passkey dismissed at sign-in:** silent (sign-in-form.md, primary-actions.md) vs "You are not signed in…" message (passkey-flows.md) | **Silent at sign-in; a message at setup** | Cancel, timeout and "no passkey" are indistinguishable (WebAuthn §14.5) [Standard]; at sign-in the OS sheet has already explained, and missing WebAuthn is detected and explained by a notice. At setup, silence would strand in-app-browser users, so a message is shown. [Opinion] |
| 5 | **Where failures show:** error summary with focus (sign-in-form.md, form-validation.md, primary-actions.md 4.1 note) vs a `role="alert"` area above the buttons with focus staying (passkey-flows.md, primary-actions.md general rule) | **The app-wide rule (editorial pass, form-validation.md):** field errors and the A25 "Sign-in failed" message in the summary; every request failure (no connection, server, passkey errors, setup `429`) in the `role="alert"` above the pressed button, with focus kept on it | The A25 message is a credential outcome the person must read before retrying, so it keeps GOV.UK's summary; a dropped connection is fixed by one more press of the focused button, as on every other screen (SC 3.2.4). The alert lines exist from the first render, so nothing is conditionally mounted. |
| 6 | **Busy feedback:** label swap inside the button after 400 ms, announced by the app status region (primary-actions.md, microcopy.md) vs a separate status line after 1 s (sign-in-form.md, totp-setup.md) | **The shared Button: label swap after 400 ms**, announced through the page status region (`PageStatus`, app-shell.md) | One Button component app-wide (SC 3.2.4); microcopy.md puts busy labels on the button. There is no app-level region. Both delays are [Convention], not measured. |
| 7 | **Passkey busy label:** "Waiting for passkey…" (primary-actions.md) vs "Signing in…" (passkey-flows.md) | **"Signing in…"** (setup: "Setting up your account…") | Same words for both sign-in methods; literal. |
| 8 | **Setup structure:** three views `/setup`, `/setup/password`, `/setup/app` (sign-in-form.md, primary-actions.md "Continue" after the password) vs one page with password then authenticator block (totp-setup.md) | **Three views** | One thing per page [Convention: GOV.UK Structuring forms]; on a phone the person leaves for the authenticator app and returns to a short page whose only field is the code; each view has one kind of error. Password order (first) and the single `{password, totpCode}` request are unchanged. |
| 9 | **Errors on one-field views (code steps, Create a password):** inline + focus the field (form-validation.md) vs summary (totp-setup.md, sign-in-form.md) | **Field errors inline with focus on the field; non-field failures (A25, network, server) in the summary** | AgDS one-error rule [Convention]; the A25 message belongs to no field. |
| 10 | **`aria-invalid` on a rejected sign-in code:** yes (totp-setup.md) vs no field marked (form-validation.md) | **No** | Marking a field would say which part failed (A25; OWASP generic responses). |
| 11 | **Code label:** "Enter your 6-digit code" (sign-in-form.md) vs "Enter the 6-digit code from your authenticator app" (totp-setup.md) | **The longer label** | Names where the code comes from (helps readers with English as a second language); matches microcopy.md's canonical empty-field error. |
| 12 | **When the code step is treated as expired:** more than 5 min (sign-in-form.md) vs 4 min 50 s (totp-setup.md) | **4 min 50 s** | Leaves margin for a request sent just before the cookie lapses. [Opinion] |
| 13 | **Passkey options:** fetched inside the tap handler (sign-in-form.md) vs prefetched with a direct call from the tap (passkey-flows.md) | **Prefetch, one object, refetch after 4 minutes or after use** | Works on every WebKit version, including old iPhones on workers' own phones [Convention, vendor; unverified for iOS 17.4+]. |
| 14 | **Detecting passkey support at setup:** `getClientCapabilities` too (passkey-flows.md) vs no up-front check (primary-actions.md) vs `browserSupportsWebAuthn()` only (sign-in-form.md) | **`browserSupportsWebAuthn()` only** | Reliable and simple; anything it misses is covered by the "not set up" message. No user-agent sniffing. |
| 15 | **Passkey autofill** (`autocomplete="username webauthn"` + conditional request): added by passkey-flows.md; not built by sign-in-form.md | **Not built** | Design 4.1 specifies a button; the owner rejects unrequested additions; sharing one options object between autofill and the button adds risk with Identity's single ceremony cookie. Listed as an owner question. |
| 16 | **Extras marked [added] in passkey-flows.md:** passkey icon, privacy line, three-paragraph explanation, "Your account is set up" line on Today | **Left out**, except one hint per method (microcopy.md: "explained once, in one sentence") carrying the "set it up on the phone" tip | Simplicity rule; the icon needs FIDO's download form; the Today line would put a message on another screen and passkey-flows.md proposed router `state`, which app-shell-nav.md forbids. |
| 17 | **Failure and help wording:** "No connection. Check your internet (and/then) try again." (sign-in-form.md, totp-setup.md, passkey-flows.md), "Check your internet connection, then try again." (empty-loading-error.md), "Something went wrong (on our side)…", "Can't sign in?…", "Cannot use your authenticator app?…", "6 numbers" | **microcopy.md §9 canonical:** "Not signed in: no connection. Try again.", "Not [done]: something went wrong. Try again.", "If you cannot sign in, ask a manager to reset your sign-in.", "The code must be 6 digits" | The brief makes microcopy.md the wording authority. |
| 18 | **Signed-out line:** "Sign in to go back to where you were." (session-timeout.md) vs "…to carry on" (sign-in-form.md) vs "Sign in again to go back to where you were." (microcopy.md) | **microcopy.md's** | Canonical; "carry on" is an idiom. |
| 19 | **"Passkey not set up" message:** 2-sentence (sign-in-form.md) vs 4-sentence (passkey-flows.md) | **"Your passkey was not set up. Try again, or open the link from your email in your phone's browser."** | Worker strings at most 2 sentences (microcopy.md); keeps the in-app-browser advice; the password option is visible directly below. |
| 20 | **In-app notice body:** "Passkeys can't be set up here…" (sign-in-form.md) vs "Passkeys do not work here…" plus how-to (passkey-flows.md) | **"Passkeys cannot be set up here. Go back to the email and open the link in your phone's browser, such as Safari or Chrome."** under the design heading | No negative contraction; sends people back to the email because §8.1 strips the token, so the in-app "Open in browser" menu would lose it. |
| 21 | **No link in memory:** new sentence (sign-in-form.md) vs reuse the timed-out string (passkey-flows.md) | **New sentence** "To set up your account, open the link from your email again." | "Timed out" would be false for someone who opened the page seconds ago; the instruction reuses the design's own words. [Opinion] |
| 22 | **Page titles:** one per view (sign-in-form.md) vs the fixed list (microcopy.md, app-shell-nav.md) | **Fixed list:** "Sign in", "Set up your account" | App-wide typed list; both pass SC 2.4.2. |
| 23 | **Column:** 30rem centred (sign-in-form.md), 34rem centred (passkey-flows.md) vs `--measure` 40rem left-aligned (foundations.md) | **foundations.md** | One layout system; shared left edge with the wordmark. |
| 24 | **Button height:** 44 px (primary-actions.md CSS) vs 48 px (foundations.md, passkey-flows.md) | **48 px** | foundations.md is canonical for tokens; FIDO asks for 48 px around the passkey control. |
| 25 | **Laptop button width:** full column (passkey-flows.md) vs label width (primary-actions.md) | **Label width** | App-wide button rule. |
| 26 | **Sign-in URL:** `/sign-in` route (sign-in-form.md) vs sign-in rendered at the requested URL (app-shell-nav.md, session-timeout.md) | **Requested URL, no `/sign-in` route** | One mechanism for fresh load and in place; URLs hold only IDs (§4.0). |
| 27 | **Back controls:** Back on the code step and setup views (sign-in-form.md) vs none (primary-actions.md) | **Back links on F and G only**, as the shared BackLink labelled with the destination ("‹ Set up your account", "‹ Create a password") in the shell's bar slot | They are real routes [Convention: GOV.UK question pages]; the code step is short-lived and returns to step A by itself after 5 minutes. One back-link label pattern app-wide (SC 3.2.4), instead of a bare "Back". |
| 28 | **Pending state source:** `useMutation().isPending` (primary-actions.md) vs a direct API call (sign-in-form.md) | **Direct call with local `pending` state passed to `Button busy`** | The mutation cache would hold the password as `variables`. |
| 29 | **Code input styling:** ~9ch (sign-in-form.md) vs 10ch, 24 px bold (totp-setup.md); error border 4 px (totp-setup.md) vs 4 px bar + 3 px border (form-validation.md) | **totp-setup.md size; form-validation.md error styling** | Each file owns its part. |
| 30 | **Unrequested controls:** Show/Hide on the sign-in password and a Copy setup key button (sign-in-form.md, totp-setup.md) vs design 4.1 (show/hide only when creating a password; "type the setup key") | **Neither in the default build**; both described under "Optional, not built unless approved" | The default build matches design 4.1 exactly (owner's simplicity rule); the clipboard concern is recorded with the Copy question. |

### Tensions with decisions (recorded once; no change proposed)

1. **A25 vs the two-step flow (§6.2).** `POST /api/auth/login` returns `{next:"totp"}` only after a correct password, so
   reaching the code step shows the password was right. Wording stays generic; the code still protects the account.
2. **Passkey button vs passkey autofill (design 4.1).** FIDO's research found "autofill ensured the highest success"
   for passkey sign-in [Research, sample sizes unpublished,
   https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey]. Built as designed.
3. **One method, no passkey management (D23 via A22/A23).** FIDO lists managing passkeys in account settings as a
   required pattern; Windows does not sync passkeys out of the box (https://passkeys.dev/device-support/), so a passkey
   made only on a Windows laptop needs a Reset sign-in to move to a phone. Mitigated by the setup hint and §8.2.
4. **Method chooser at setup (D23).** Microsoft (vendor data) advises passkey-first with no chooser; FIDO's "Offer
   choice" and NN/g support the design's chooser.
5. **Generic code-step message (A25).** GOV.UK error guidance and GOV.UK One Login name a wrong code specifically;
   here the generic message gives a tired worker less to go on.
6. **30-minute setup session with no warning (§8, 4.1).** WCAG 2.2.1's "essential" reading is clear for two-factor
   codes, less so for a 30-minute window. Reopening the link shows the same key, so only the typed password is lost.
7. **No password blocklist (A22, §8.4).** NIST SP 800-63B-4 makes a compromised-password blocklist a SHALL
   (https://pages.nist.gov/800-63-4/sp800-63b.html).

### Open questions for the owner

1. **Approve or drop the proposed (P) strings**, in particular the optional ones: the sign-in hint "Sign in the way you
   chose when you set up your account.", the help line "If you cannot sign in, ask a manager to reset your sign-in.",
   "If these are wrong, ask a manager.", the two method hints, the authenticator steps (including naming Google
   Authenticator and Microsoft Authenticator as examples), and every error, busy and notice string.
2. **Show/Hide on the sign-in password (not built unless approved).** Design 4.1 mentions it only when setting a
   password, so the default build has it on view F only. Adding it to sign-in is supported by NIST (SHOULD) and WCAG
   3.3.8 Understanding ("can improve the chance of success"). Add it?
3. **Copy setup key button (not built unless approved).** Design says "type the setup key". On a phone (the main case)
   the person cannot scan their own screen, so a copy button was proposed. **Clipboard concern:** the key is the
   authenticator's shared secret; on the clipboard it can be kept by Windows clipboard history or synced by iOS
   Universal Clipboard, which sits badly with §9.6 and D22's "nothing stored on the device". Add it?
4. **Passkey autofill**, the passkey icon (needs FIDO's download form), a privacy line ("Your face, fingerprint and PIN
   stay on your device. Grow2Notes never sees them.") and a "Your account is set up" line on Today: all left out; say
   if any are wanted.
5. **Expired-link page:** may it carry a "Sign in" link? People who reuse an old invite email after setting up will land
   there and may ask for needless resets.
6. **Two-factor expiry:** set the two-factor cookie's lifetime explicitly (5 minutes) with an integration test, or have
   the API return a distinct response for an expired code step?
7. **API details not in §6.2:** the response for a wrong code at `POST /api/auth/setup/password` (assumed `422
   validation.failed` with `errors.totpCode`), and the fixed `name` to send with `POST /api/auth/setup/passkey`
   (suggested "Grow2Notes"; never shown).
8. **Lockout and passkeys:** does Identity's lockout block `PasskeySignInAsync`, and do passkey failures count? This
   decides whether the A25 sentence is accurate for passkey users (M0 test).
9. **Oldest phones:** which iOS and Android versions may workers' own phones run? (Drives the prefetch rule and the
   CSS baseline.)
10. **In-app browsers:** which email apps on staff phones can create passkeys (device check before go-live).

---

## Sources

- design.md §4.0, §4.1, §6.2, §6.9, §8.1–8.6, §9.6–9.9, §13 (A22–A25, A32); decisions.md D21–D23, D42.
- Component files: [sign-in-form.md](../components/sign-in-form.md), [passkey-flows.md](../components/passkey-flows.md),
  [totp-setup.md](../components/totp-setup.md), [form-validation.md](../components/form-validation.md),
  [primary-actions.md](../components/primary-actions.md), [empty-loading-error.md](../components/empty-loading-error.md),
  [foundations.md](../components/foundations.md), [microcopy.md](../components/microcopy.md), plus
  [session-timeout.md](../components/session-timeout.md) and [app-shell-nav.md](../components/app-shell-nav.md) for the
  in-place view and shell. Their source lists carry the full citations.
- GOV.UK Design System, Password input (checked 1 October 2026): https://design-system.service.gov.uk/components/password-input/
- GOV.UK Design System, Passwords pattern (checked 1 October 2026): https://design-system.service.gov.uk/patterns/passwords/
- GOV.UK Design System, Error summary: https://design-system.service.gov.uk/components/error-summary/ · Validation: https://design-system.service.gov.uk/patterns/validation/ · Button: https://design-system.service.gov.uk/components/button/
- AgDS, Accessible form validation and recovery: https://design-system.agriculture.gov.au/patterns/accessible-form-validation-and-recovery
- W3C, Understanding WCAG 2.2: 3.3.8 https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html · 3.3.7 https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html · 3.3.3 https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html · 2.2.1 https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html · 4.1.3 https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · F36 https://www.w3.org/WAI/WCAG22/Techniques/failures/F36
- W3C, Web Authentication Level 3 (§14.5 ceremony privacy, §15.1 timeouts): https://www.w3.org/TR/webauthn-3/
- NIST SP 800-63B-4: https://pages.nist.gov/800-63-4/sp800-63b.html
- FIDO Alliance design guidelines: https://www.passkeycentral.org/design-guidelines/principles · https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey
- passkeys.dev device support: https://passkeys.dev/device-support/
- Chromium, Create amazing password forms: https://www.chromium.org/developers/design-documents/create-amazing-password-forms/
- Reese et al., A Usability Study of Five Two-Factor Authentication Methods, SOUPS 2019: https://www.usenix.org/conference/soups2019/presentation/reese
- Roselli, Exposing Field Errors (2023): https://adrianroselli.com/2023/04/exposing-field-errors.html
- React, `<form>` (reset after a successful action): https://react.dev/reference/react-dom/components/form
- Microsoft Learn, Passkeys in ASP.NET Core (.NET 10): https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0
- ASP.NET Core source, two-factor cookie lifetime: https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityCookiesBuilderExtensions.cs
- Yubico, Handle WebKit user gesture: https://developers.yubico.com/WebAuthn/Concepts/Handle_WebKit_User_Gesture.html
- MDN, `Clipboard.writeText()`: https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText
- Sutton, User testing of accessible client-side routing (Gatsby, 2019, n = 5): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
