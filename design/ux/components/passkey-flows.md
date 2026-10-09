# Passkey setup and sign-in

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Passkey failures show in the `role="alert"` line above the pressed button, not the error summary (sign-in.md, form-validation.md).

Component key: `passkey-flows`. Covers the passkey parts of design.md §4.1 (Sign-in and account setup), and the passkey
paths in §4.0 Sessions, §8.1, §8.2, §8.5 and §8.6. It covers:

- the choice between **Use a passkey (recommended)** and **Use a password and authenticator app** at setup
- creating the passkey
- **Sign in with a passkey**
- every error, cancel and fallback state on those paths.

The password field, the authenticator QR code and setup key, and the 6-digit code field are separate components. This
file only covers the hand-off to them.

Nothing here adds a screen, a setting, a notification or stored data. Six small things go beyond the literal words of
design.md. Each is marked **[added]** where it appears, so the owner can drop any of them:

1. plain-English explanation copy
2. a hint line
3. the passkey icon
4. passkey autofill on the email field (no visible UI)
5. a "Passkeys do not work in this browser" notice
6. a one-line confirmation on Today after setup

---

## Where it's used

| # | Screen (design.md) | What this component does there | What differs |
|---|---|---|---|
| 1 | **Set up your account**, steps 2–3 (§4.1, §8.1 step 4) | Explains passkeys, offers the two methods, runs passkey creation, finishes setup. | Uses the enrolment cookie, not a session. Creation (`navigator.credentials.create`). Failures fall back to the password + authenticator path. |
| 2 | **Set up your account**, opened in a phone email app's in-app browser (§4.1 table) | Swaps the passkey option for the design's "Open this link in your browser" notice. The password + authenticator option stays. | Detected up front where possible. Otherwise the failure message carries the same advice. |
| 3 | **Sign in** (§4.1 "Signing in", §8.2) | **Sign in with a passkey** button, plus passkey autofill on the email field. | Authentication (`navigator.credentials.get`), discoverable, no email needed. Server rejections use the A25 generic message. |
| 4 | **Sign in in place** after the 30-minute idle sign-out (§4.0 Sessions, §8.5) | Same as 3. | On success the user goes back to the same route, and unsaved text stays in memory. There is no reload. |
| 5 | **Set up again** after a manager's Reset sign-in or an email change (§4.11, §8.6) | Same as 1. | The old passkey may still be on the person's phone (see Per-screen notes). |
| 6 | **Today**, first arrival after setup (§4.1 step 5) | Shows one confirmation line **[added]**. | Held in router state only. It is gone on reload. |

---

## Best practice

### Wording

- **[Convention]** "passkey" is a common noun, written in lowercase except at the start of a sentence. Use "a passkey",
  not "your Passkey". Don't tie it to a platform: write "passkey", not "Apple passkey" or "Google passkey". FIDO's
  example labels are "Create a passkey" and "Sign in with a passkey".
  https://fidoalliance.org/wp-content/uploads/2023/12/FIDO-Passkey_Icon_Usage_Guidelines-August2022.pdf ·
  https://passkeys.dev/docs/reference/terms/
- **[Research]** Microsoft found that "passkey" was sometimes unfamiliar, but "face, fingerprint, or PIN" was generally
  well understood. So they connect the two terms everywhere.
  https://www.microsoft.com/en-us/security/blog/2024/12/12/convincing-a-billion-users-to-love-passkeys-ux-design-insights-from-microsoft-to-boost-adoption-and-security/
  (vendor-reported telemetry and experiments, method not published).
- **[Convention]** Google: use the word "passkey" explicitly. Pair it with familiar ideas (fingerprint, face, screen
  lock). Avoid jargon such as WebAuthn and FIDO. Don't imply biometrics are required, because a PIN or screen lock also
  works. https://developers.google.com/identity/passkeys/ux/communicating-passkeys
- **[Convention]** FIDO content principle: "pair with familiar language", and keep explanations visible rather than
  behind tooltips or extra clicks ("persist information").
  https://www.passkeycentral.org/design-guidelines/principles
- **[Research]** GOV.UK One Login ran a survey of more than 2,500 people and three rounds of research over a year.
  Users worried that their biometric data was shared, and they did not need to understand how passkeys work in order to
  use them.
  https://gds.blog.gov.uk/2026/09/16/how-we-made-it-easier-for-millions-of-users-to-sign-into-government-services/
- **[Standard]** The relying party never receives biometric data. Recognition happens on the device, and only the
  "user verified" flag is sent. This backs up a plain "we never see your face or fingerprint" line.
  https://www.w3.org/TR/webauthn-3/#sctn-biometric-privacy (§14.3)
- **[Convention]** Apple: always name the method on the button. Only mention methods that are available in the
  current context ("don't reference Face ID on a device that doesn't offer it"). Avoid the word "passcode" for account
  sign-in. https://developer.apple.com/design/human-interface-guidelines/managing-accounts
- **[Convention]** GOV.UK style: say "sign in", not "log in". Avoid negative contractions ("cannot", not "can't")
  because many users misread them. This matters for readers who have English as a second language.
  https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

### Creating a passkey (setup)

- **[Convention]** Use "handshake" messaging: put an explanation on the page **before** the operating system's dialog,
  and a confirmation **after** it. FIDO's research found that jumping straight into the OS dialog "was shorter but
  disorienting for participants".
  https://www.passkeycentral.org/design-guidelines/optional-patterns/new-account-creation-with-a-passkey ·
  https://www.passkeycentral.org/design-guidelines/principles
- **[Convention]** Offer a choice. Let people set up without a passkey. FIDO principle 4: "Offer choice".
  **[Research]** NN/g: offer password and biometric options alongside passwordless ones, because some users prefer
  them. https://www.nngroup.com/articles/passwordless-accounts/
- **[Convention]** `user.name` should be something the user recognises, such as their email address. `user.displayName`
  should be a friendly name. https://web.dev/articles/passkey-registration
  **[Standard]** The user handle (`user.id`) must not contain an email address or other personal information.
  https://www.w3.org/TR/webauthn-3/#sctn-user-handle-privacy (§14.6.1)
- **[Convention]** `InvalidStateError` means a passkey for this account already exists on the device. Don't treat it as
  a failure. `NotAllowedError` means the user cancelled. https://web.dev/articles/passkey-registration
- **[Convention]** Use the FIDO passkey icon next to a text label:
  - at least 24 × 24 px, in a single flat colour, with at least 3:1 contrast
  - centred vertically with the label
  - `aria-hidden="true"` when a text label is present
  - inside a touch target of at least 48 px.

  Service providers get the files by filling in a form on FIDO's site.
  https://fidoalliance.org/wp-content/uploads/2023/12/FIDO-Passkey_Icon_Usage_Guidelines-August2022.pdf ·
  https://fidoalliance.org/get-the-passkey-icon/

### Signing in

- **[Research]** In FIDO's research, autofill gave "the highest success for people to sign in with a passkey". FIDO also
  found that many people do not discover a dedicated passkey button. A button is still appropriate when several sign-in
  options sit side by side, which is Grow2Notes' case.
  https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey
- **[Standard]** Passkey autofill ("conditional UI") needs all three of these together:
  - a discoverable credential
  - an input with `autocomplete="username webauthn"` (`webauthn` must be the last token)
  - a `navigator.credentials.get({ mediation: "conditional" })` call made when the page loads.

  The pending request does not resolve if the user picks a password or ignores it. Use an `AbortController` to cancel
  it before you start another WebAuthn call. https://web.dev/articles/passkey-form-autofill ·
  https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#autofill
- **[Standard]** `PublicKeyCredential.getClientCapabilities()` has been Baseline since 2025. It reports:
  - `conditionalGet` (autofill)
  - `hybridTransport`
  - `passkeyPlatformAuthenticator`, meaning a passkey can be used "locally and/or via hybrid transport".

  A missing key means unknown, so assume nothing from it.
  https://developer.mozilla.org/en-US/docs/Web/API/PublicKeyCredential/getClientCapabilities_static ·
  https://www.w3.org/TR/webauthn-3/#enumdef-clientcapability (§5.8.7)
- **[Standard]** ASP.NET Core Identity 10 supports username-less and conditional-UI requests (it calls
  `MakePasskeyRequestOptionsAsync` with no user). It keeps the ceremony state in a protected cookie between the options
  request and the response. `AuthenticatorTimeout` defaults to 5 minutes.
  https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0
- **[Convention]** Cross-device sign-in (a phone passkey used on a laptop by scanning a QR code) is drawn by the
  browser and operating system, not the site. Don't explain it on the sign-in page; explain it in help or onboarding.
  https://www.passkeycentral.org/design-guidelines/optional-patterns/cross-device-sign-in

### Errors, cancellations and timeouts

- **[Standard]** A cancel, a timeout, and "no credential" all come back as `NotAllowedError`. The spec tells browsers to
  make these hard for the site to tell apart, for privacy reasons. So the app **cannot know** whether the person
  cancelled, the prompt timed out, or an in-app browser blocked it. Write one message that fits all three.
  https://www.w3.org/TR/webauthn-3/#sctn-make-credential-privacy (§14.5.1, §14.5.2) ·
  https://developer.mozilla.org/en-US/docs/Web/API/CredentialsContainer/create
- **[Standard]** Recommended ceremony timeout is 300,000–600,000 ms, with 5 minutes as the default. It should follow
  WCAG Guideline 2.2 Enough Time. https://www.w3.org/TR/webauthn-3/#sctn-timeout-recommended-range (§15.1)
- **[Standard]** WCAG 3.3.3 Error Suggestion requires a suggestion "unless it would jeopardize the security or purpose
  of the content". This covers A25's deliberately generic sign-in failure message.
  https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html
- **[Standard]** WCAG 3.3.8 Accessible Authentication (Minimum) says operating-system authentication such as Touch ID,
  Face ID and Windows Hello is "not a cognitive function test". The password path must allow paste and password
  managers. https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html
- **[Convention]** GOV.UK: prevent double clicks, and avoid disabled buttons ("poor contrast and can confuse some
  users"). Use one primary button per page and secondary buttons for other actions.
  https://design-system.service.gov.uk/components/button/

### In-app browsers and fallback

- **[Convention]** FIDO: "gracefully fall back to other methods".
  https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey
- **[Convention, vendor]** Embedded WebViews give unreliable WebAuthn, and passkey autofill does not work in them at
  all. On iOS, a WKWebView needs an entitlement owned by the host app. SFSafariViewController and Android Custom Tabs use
  the real browser and do support passkeys. The vendor recommends:
  - detecting in-app browsers early
  - leading with a method that works there
  - prompting the person to open the page in the real browser.

  https://www.corbado.com/blog/passkeys-in-app-browsers (passkey vendor; not independently verified). **Unverified:**
  which in-app browser Gmail, Outlook or other email apps use on workers' phones today. Test it (see Per-screen notes).
- **[Convention, vendor]** Older WebKit (iOS 14 to about 17.3) required every WebAuthn call to come straight from a tap.
  An awaited `fetch` before the call could lose that gesture. A vendor says iOS 17.4 replaced the rule with rate
  limiting; this is **unverified** against WebKit release notes. The safe pattern is to fetch the options before the
  tap, then call WebAuthn directly from the tap handler.
  https://developers.yubico.com/WebAuthn/Concepts/Handle_WebKit_User_Gesture.html ·
  https://www.corbado.com/blog/safari-webauthn-user-activated-events

### Laptops and cross-device

- **[Standard]** passkeys.dev's support matrix (updated 21 September 2026) shows:
  - synced passkeys out of the box on Android 9+, ChromeOS, iOS 16+ and macOS 13+, but **not Windows**
  - Windows 23H2+ able to use a passkey from a phone (cross-device)
  - autofill on iOS/iPadOS Safari 16.1+, Chrome 108+ (Android, macOS, Windows), Edge 122+ and Firefox 122+.

  https://passkeys.dev/device-support/ So a passkey made in Windows Hello may only work on that laptop. A passkey made
  on a phone can sign in on a laptop by QR code. Cross-device sign-in needs Bluetooth on both devices
  (https://passkeys.dev/docs/reference/terms/).
- **[Research]** In FIDO's research, participants liked phone-to-laptop sign-in. Some found getting their phone out
  inconvenient, and some felt the extra step was more secure.
  https://www.passkeycentral.org/design-guidelines/optional-patterns/cross-device-sign-in

---

## Recommendation for Grow2Notes

### Anatomy

**Setup, passkey part (screen 1).** The `h1` and the name/email summary belong to the setup screen spec.

```
Set up your account                                   h1 (screen spec)
Name   Sam Lee            Email  sam@example.org      read-only (screen spec)

Choose how you'll sign in                             h2
A passkey lets you sign in with your face,            p   [added]
fingerprint or PIN – the same way you unlock your
phone or computer. There is no password to remember.
Your face, fingerprint and PIN stay on your device.   p   [added]
Grow2Notes never sees them.
Set it up on the phone you'll use for Grow2Notes.     p   [added]
That phone can also sign you in on a computer.

[message area – role="alert", empty until needed]
[ (passkey icon) Use a passkey (recommended) ]        primary button, full width
                       or                             p
[ Use a password and authenticator app ]              secondary button, full width
[status line – role="status", empty until busy]
```

**Sign-in, passkey part (screens 3 and 4).**

```
Sign in                                               h1
[message area – role="alert"; shared with the password path]
[ (passkey icon) Sign in with a passkey ]             primary button, full width
Uses your face, fingerprint or PIN.                   hint, aria-describedby   [added]
[status line – role="status"]
                       or
Email     [ ................. ]   autocomplete="username webauthn"   (password component)
Password  [ ........ ] [Show]                                         (password component)
[ Sign in ]                                           secondary style (password component)
```

- **Order.** The passkey option comes first because it is the recommended method (§8.1), and on phones most staff will
  use it. Password users see their familiar fields straight underneath. **[Opinion]**
- **One primary button per screen.** On sign-in, "Sign in with a passkey" is primary and the password form's "Sign in"
  is secondary. GOV.UK says to avoid more than one primary button
  ([button guidance](https://design-system.service.gov.uk/components/button/)). Each person only ever has one method
  (A22), so whoever uses the password form is drawn to it by its fields, not by the button colour. **[Convention]**
  plus **[Opinion]**. Check this during the pilot (A40).
- **The passkey button is `type="button"` and sits outside the password `<form>`.** Pressing Enter in the password
  field must submit the password form, never start a passkey. **[Standard]** (HTML implicit submission).
- **The passkey icon is [added], decorative and `aria-hidden`.** The text label carries the meaning. If the owner skips
  the FIDO icon download form, ship the label alone. **[Convention]**

### Behaviour

**Setup (screen 1)**

1. **Before showing the step,** run the capability check alongside `POST /api/auth/setup/start`, so the screen never
   flashes the wrong option:
   - `window.PublicKeyCredential` missing → passkeys are unavailable
   - else `getClientCapabilities()` returns `passkeyPlatformAuthenticator === false` → unavailable
   - else (true, missing, or the method itself missing) → offer the passkey.

   Laptops without Windows Hello still pass, because the capability includes cross-device. Do not sniff user-agent
   strings. **[Standard]** for the checks; **[Opinion]** against user-agent lists, because they need ongoing upkeep
   and the failure copy below covers anything the checks miss.
2. When the step renders with passkeys offered, **prefetch the creation options** (`POST
   /api/auth/setup/passkey/options`). Keep exactly one options object at a time. Identity stores the ceremony state in
   one cookie, so a second fetch replaces the first. **[Standard]** for the cookie; check the replacement behaviour in
   the M0 tests.
3. **Tap "Use a passkey (recommended)":**
   - If the cached options are under 4 minutes old (Identity's default ceremony timeout is 5 minutes), call
     `startRegistration` straight from the handler, with no `await` before it.
   - Otherwise fetch, then call.

   The button gets `aria-disabled="true"`, a re-entry guard, and the status line "Setting up your account…".
4. **The operating system's dialog shows** (Face ID, fingerprint, PIN, or "use a phone" on a laptop). The app draws
   nothing on top of it.
5. **When a credential comes back:**
   - mark the options as used
   - `POST /api/auth/setup/passkey` with `{credentialJson, name}`, where `name` is the fixed value "Grow2Notes" (D70);
     never ask the person to name the passkey, because no screen ever shows it (A22)
   - on `200 Me`, go to Today with the confirmation line.
6. **On any failure,** map it to one state (see [States](#states)). Keep both buttons usable. If the server consumed
   the options, prefetch new ones, so the next tap still comes straight from a gesture.

**Sign-in (screens 3 and 4)**

1. On mount, if `window.PublicKeyCredential` exists:
   - prefetch request options (`POST /api/auth/passkey/options`)
   - if `browserSupportsWebAuthnAutofill()` is true, start the conditional request using **the same options**. The
     email field carries `autocomplete="username webauthn"`. **[added]**, with no visible UI until the person taps the
     email field.
2. **Tap "Sign in with a passkey":**
   - Start a modal `startAuthentication` with the same cached options, straight from the tap. SimpleWebAuthn's
     `WebAuthnAbortService` cancels the pending autofill request automatically, and the abort is ignored.
   - The status line shows "Signing in…".
3. **Picking the passkey from autofill** uses the same finish path: "Signing in…", then `POST /api/auth/passkey`.
4. **Submitting the password form** calls `WebAuthnAbortService.cancelCeremony()` first.
5. **On success,** navigate with no reload:
   - screen 3: go to Today
   - screen 4: go back to the route the person was on, with in-memory unsaved text intact (§8.5).
6. **After any failure,** show the message and restart autofill. If the server consumed the options, fetch new ones
   first.
7. **On unmount,** cancel any ceremony. React 19 StrictMode runs effects twice in development, so the first run's abort
   must stay silent.

**Never** start a modal WebAuthn call on page load. The OS dialog only ever appears after a tap on a passkey button or
on the person's passkey in autofill. **[Standard]** (WCAG 3.2.1/3.2.2: no change of context without a user request)
and **[Convention]** (FIDO's "disorienting" finding).

### States

**Setup: passkey part**

| State | When | What shows | Focus | Screen reader hears |
|---|---|---|---|---|
| Loading | `setup/start` and capability check in flight | The setup screen's own loading state | — | Screen spec |
| Default | Passkeys possible | Explanation, then primary "Use a passkey (recommended)", "or", secondary "Use a password and authenticator app" | `h1` on load (screen spec) | — |
| Hover (pointer only) | Mouse over a button | Background one step darker. No movement. | — | — |
| Focus | Keyboard | 3 px solid outline, 2 px offset, at least 3:1 against both neighbouring colours | — | "Use a passkey (recommended), button" |
| Active | Pressed | Background two steps darker. No movement. | — | — |
| Busy | Tap until the server answers | Button keeps its look, with `aria-disabled="true"` and `cursor: progress`. Repeat taps are ignored. Status line: "Setting up your account…" | Stays on the button | Status text (polite) |
| Not set up | `NotAllowedError`, `AbortError` not started by us, or a `400` from completion | Message above the buttons (see [Copy](#copy)). Both buttons usable. | Stays on the button | Message (assertive, `role="alert"`) |
| Already on this device | `InvalidStateError` | Should not happen, because Reset sign-in removes server passkeys (§8.6). If it does, treat it as **Not set up**. | — | — |
| Passkeys unavailable | Capability check fails | A notice replaces the explanation and the passkey button. "Use a password and authenticator app" becomes the **primary** button, because it is now the only action. | `h1` on load | Notice is read in normal order (static, not live) |
| No connection | `fetch` throws `TypeError` | Message: "No connection…". Both buttons usable. | Stays on the button | Alert |
| Session timed out | `401` from options or completion | The method choice is replaced by the design string. The buttons are removed. | Moves to the message, which has `tabindex="-1"`, so focus is never lost | Read on focus |
| Link replaced | `410 auth.setup_link_invalid` | Same, with the expired-link design string | Moves to the message | Read on focus |
| Done | `200 Me` | Go to Today, showing the confirmation line | Today's `h1` (Today spec) | Confirmation line (`role="status"`, filled after mount) |
| Disabled | Never. No state uses a greyed-out button. | | | |

**Sign-in: passkey part**

| State | When | What shows | Focus | Screen reader hears |
|---|---|---|---|---|
| Default | `PublicKeyCredential` exists | Button with hint. Autofill request waits silently. | `h1` on load (screen spec) | "Sign in with a passkey, button, Uses your face, fingerprint or PIN." |
| Autofill offered | Person focuses the email field | The browser's own list or keyboard bar shows "Grow2Notes" passkeys. We draw nothing. | Email field | Browser-provided |
| Hover / Focus / Active | As for setup | As for setup | — | — |
| Busy | Tap, or autofill pick, until the server answers | `aria-disabled="true"`, status line "Signing in…" | Stays where it was | Status (polite) |
| Not signed in | Client error (`NotAllowedError` and similar) after a **button** ceremony | Message in the shared message area | Stays on the button | Alert |
| Autofill dismissed | Client error from the autofill request | **Nothing.** The person just chose not to use it. | — | — |
| Sign-in failed | `401` or `429` from `/api/auth/passkey` | The A25 string, in the same message area the password path uses | Stays put | Alert |
| No connection | `fetch` throws | "No connection…" | Stays put | Alert |
| Passkeys unavailable | `PublicKeyCredential` missing | A one-line notice **[added]** replaces the button and hint. The password form is unchanged. | — | Static text |
| Done | `200 Me` | Screen 3: Today. Screen 4: back to the same route. | Destination `h1` | Route spec |

To make a repeated identical message announce again, clear the alert region, then set the text on the next animation
frame. Empty, read-only and disabled states do not apply to this component.

### Phone vs laptop

- **Phone (main case).** Single column. Full-width buttons, at least 48 px tall (FIDO's 48 px, above A32's 44 px), with
  at least 8 px between them. The OS sheet covers the bottom of the page, so the message area sits **just above** the
  buttons on setup and **just above** the button on sign-in: where the person is looking when the sheet closes.
- **Laptop.** Same layout, in a centred column with `max-inline-size: 34rem`. Sign-in and account setup do not use the
  extra width; that note in §4.0 is about the Manage screens. Buttons stay full column width, so the pair reads as one
  choice. **[Opinion]** The browser draws its own passkey dialog. On a laptop without Windows Hello it offers "use a
  phone or tablet" (a QR code). Grow2Notes says nothing about that on screen, and onboarding covers it.
- **Text at 200% and reflow at 320 px.** Labels wrap. Buttons use `min-block-size`, never a fixed `block-size`. The icon
  stays centred against the label block. **[Standard]** (WCAG 1.4.4, 1.4.10)

### Copy

Source key:

- **design.md** — the exact string from design.md
- **added** — proposed here, written in the same voice

New strings avoid negative contractions (GOV.UK style). Australian spelling. "passkey" is lowercase except at the
start of a sentence.

| Where | Text | Source |
|---|---|---|
| Setup `h2` | Choose how you'll sign in | added (design: "Choose …") |
| Setup explanation 1 | A passkey lets you sign in with your face, fingerprint or PIN – the same way you unlock your phone or computer. There is no password to remember. | added |
| Setup explanation 2 | Your face, fingerprint and PIN stay on your device. Grow2Notes never sees them. | added (WebAuthn §14.3; GDS finding) |
| Setup tip | Set it up on the phone you'll use for Grow2Notes. That phone can also sign you in on a computer. | added (Windows does not sync passkeys out of the box) |
| Setup primary button | Use a passkey (recommended) | design.md |
| Separator | or | added |
| Setup secondary button | Use a password and authenticator app | design.md |
| Setup busy | Setting up your account… | added |
| Setup: not set up | **Your passkey was not set up.** Try again. If you were not asked for your face, fingerprint or PIN, open this link in your browser, or use a password and authenticator app. To open it in your browser, go back to the email, press and hold the link, and open it in Safari or Chrome. | added, built around the design.md string "Open this link in your browser" |
| Setup: passkeys unavailable (notice) | **Passkeys do not work here.** Open this link in your browser, or use a password and authenticator app. To open it in your browser, go back to the email, press and hold the link, and open it in Safari or Chrome. | design.md row "in-app email browser", plus added how-to |
| Setup: no connection | No connection. Check your internet, then try again. | added (matches "Not saved: no connection" in §4.3) |
| Setup: session timed out | Your setup session timed out. Open the link from your email again. | design.md |
| Setup: link expired, used or replaced | This link has expired. Ask a manager to send a new one. | design.md |
| Today, after setup | Your account is set up. Next time, choose **Sign in with a passkey**. | added (FIDO handshake after the OS dialog) |
| Sign-in button | Sign in with a passkey | design.md |
| Sign-in hint | Uses your face, fingerprint or PIN. | added |
| Sign-in busy | Signing in… | added |
| Sign-in: not signed in (client side) | **You are not signed in.** Try again. If you have lost the phone with your passkey, ask a manager to reset your sign-in. | added ("reset your sign-in" matches the manager's **Reset sign-in** button, §4.11) |
| Sign-in: server rejected (`401`, `429`) | Sign-in failed. Check your details and try again. After 5 failed attempts, sign-in pauses for 15 minutes. | design.md (A25) |
| Sign-in: passkeys unavailable | Passkeys do not work in this browser. To use your passkey, open Grow2Notes in Safari or Chrome. | added |
| Sign-in: no connection | No connection. Check your internet, then try again. | added |

Why "Safari or Chrome": these are the default browsers on the two phone platforms. Other real browsers also work. The
message names a browser to open, not a passkey platform, so FIDO's "don't tie passkeys to a platform" rule is kept.
**[Opinion]**

### Accessibility

- **Semantics.**
  - Native `<button type="button">` for both passkey actions, and `<h2>` for "Choose how you'll sign in".
  - The hint is a `<p>` linked by `aria-describedby`.
  - The icon is an inline SVG with `aria-hidden="true"` and `focusable="false"`, filled with `currentColor`.
  - **No ARIA beyond:**
    - `aria-describedby` (hint)
    - `aria-disabled` (busy)
    - `role="status"` (busy line)
    - `role="alert"` (message area).
  - Both live regions are always in the DOM, empty until used.
- **Accessible name = visible label.** No `aria-label` on these buttons, so "(recommended)" is spoken as shown
  (WCAG 2.5.3 Label in Name).
- **Keyboard.**
  - Tab order follows the visual order: passkey button, then the email field, then the password field, Show, and Sign
    in.
  - Enter and Space start the passkey. The OS or browser dialog owns its own keyboard and returns focus to the button
    when it closes. Confirm this on each browser in the device check.
  - Autofill on the email field is opened with Down arrow (browser-provided).
- **Focus is never dropped.**
  - Busy uses `aria-disabled`, never `disabled`.
  - When content is removed (timed out, link expired), focus moves to the message that replaces it.
- **Announcements.**
  - Busy: polite, through the status line.
  - Failure: assertive, through `role="alert"`, because the person just acted and is waiting.
  - Static notices: no live region.
  - Success: handled by the destination route.
- **Forced colours.** Buttons get a `border` in `ButtonText` and the focus ring uses `outline`, never `box-shadow`. The
  icon follows `currentColor`.
- **Motion.** None. Busy is shown in words, with no spinner, so there is nothing to change for
  `prefers-reduced-motion`.
- **WCAG 2.2 criteria met:**
  - 1.1.1 Non-text Content
  - 1.3.1 Info and Relationships
  - 1.4.1 Use of Color (every message has words and a bold lead, not colour alone)
  - 1.4.3 Contrast (Minimum)
  - 1.4.4 Resize Text
  - 1.4.10 Reflow
  - 1.4.11 Non-text Contrast (icon and secondary-button border at least 3:1)
  - 2.1.1 Keyboard
  - 2.2.1 Timing Adjustable (ceremony timeout at least 5 minutes)
  - 2.4.3 Focus Order
  - 2.4.6 Headings and Labels
  - 2.4.7 Focus Visible
  - 2.4.11 Focus Not Obscured (Minimum)
  - 2.5.3 Label in Name
  - 2.5.8 Target Size (Minimum)
  - 3.2.2 On Input
  - 3.3.1 Error Identification
  - 3.3.3 Error Suggestion (with its security exception for A25)
  - 3.3.8 Accessible Authentication (Minimum) (the passkey path has no cognitive test)
  - 4.1.2 Name, Role, Value
  - 4.1.3 Status Messages.

### Implementation notes (React 19, native HTML, CSS Modules)

- **Libraries.**
  - `@simplewebauthn/browser` (design §7.5): `startRegistration`, `startAuthentication({ useBrowserAutofill })`,
    `browserSupportsWebAuthn`, `browserSupportsWebAuthnAutofill`, `WebAuthnAbortService`, and `WebAuthnError` (code
    `ERROR_CEREMONY_ABORTED` when we abort). https://simplewebauthn.dev/docs/packages/browser
  - No React Aria. Native buttons cover everything here.
  - Don't use SimpleWebAuthn's `useAutoRegister`. It creates passkeys silently after a password sign-in, which A22
    rules out.
- **Server settings this UI depends on:**
  - Keep `IdentityPasskeyOptions.AuthenticatorTimeout` at **5 minutes or more**. Don't copy the 3-minute value in
    Microsoft's sample (WebAuthn §15.1; WCAG 2.2.1).
  - `PasskeyUserEntity`:
    - `Name` = the email
    - `DisplayName` = the person's display name
    - `Id` = the user ID.

    No organisation name and no other company name goes into the passkey (D42). The relying party is the Grow2Notes
    domain (A38).
- **Permissions-Policy.** §9.7 already allows `publickey-credentials-create=(self)` and `publickey-credentials-get=(self)`.
  No CSP changes are needed. All calls are same-origin `fetch`.
- **No device storage.** Don't remember "last method used" or "has a passkey" in `localStorage` or cookies (D22, A22).
  The passkey itself lives in the person's own password manager, outside the app. Sign-out's `Clear-Site-Data` does not
  touch it, and should not.
- **In-app browsers and the setup token.** §8.1 strips the setup token from the address bar with `history.replaceState`.
  So the in-app browser's own "Open in browser" menu would open `/setup` **without** the token, and its cookie jar is
  separate. That is why the copy sends people **back to the email link** rather than to the browser menu.
- **Tests.**
  - Playwright on Chromium, with a CDP virtual authenticator (`WebAuthn.addVirtualAuthenticator`, `hasResidentKey:
    true`, `hasUserVerification: true`, `isUserVerified: true`), covering:
    - setup → Today
    - sign-in by button
    - sign-in by autofill
    - the cancel path (`isUserVerified: false` → `NotAllowedError` → message).
  - axe cannot see OS dialogs, so add a manual device check. See [Per-screen notes](#per-screen-notes).

Short sketch: the shared helpers and the sign-in button. Setup uses the same pattern with `startRegistration`.

```tsx
// auth/passkey.ts
import {
  startAuthentication, browserSupportsWebAuthn, browserSupportsWebAuthnAutofill,
  WebAuthnAbortService, WebAuthnError,
} from '@simplewebauthn/browser';

export async function canCreatePasskeyHere(): Promise<boolean> {
  if (!browserSupportsWebAuthn()) return false;
  const caps = await PublicKeyCredential.getClientCapabilities?.().catch(() => undefined);
  return caps?.passkeyPlatformAuthenticator !== false;   // missing = unknown = offer it
}
export const isOurAbort = (e: unknown) =>
  e instanceof WebAuthnError && e.code === 'ERROR_CEREMONY_ABORTED';
export const cancelPasskey = () => WebAuthnAbortService.cancelCeremony();

const MAX_AGE_MS = 4 * 60_000;                           // inside Identity's 5-minute timeout
export function useOneOptions<T>(fetchOptions: () => Promise<T>) {
  const ref = useRef<{ value: T; at: number } | null>(null);
  const prefetch = async () => { const value = await fetchOptions(); ref.current = { value, at: Date.now() }; return value; };
  const take = () => (ref.current && Date.now() - ref.current.at < MAX_AGE_MS ? ref.current.value : null); // sync
  const spent = () => { ref.current = null; };          // server consumed the state cookie
  return { prefetch, take, spent };
}

// auth/PasskeySignIn.tsx
export function PasskeySignIn({ onSignedIn, setMessage }: Props) {
  const [busy, setBusy] = useState(false);
  const opts = useOneOptions(fetchRequestOptions);       // POST /api/auth/passkey/options
  const supported = browserSupportsWebAuthn();

  async function finish(credential: AuthenticationResponseJSON) {
    setBusy(true); opts.spent();
    const res = await postPasskey(credential);           // POST /api/auth/passkey
    if (res.ok) return onSignedIn(res.me);               // navigate; never reload
    setBusy(false);
    setMessage(res.offline ? COPY.noConnection : COPY.signInFailed);   // A25 for 401/429
    void listenForAutofill();
  }
  async function listenForAutofill() {
    if (!(await browserSupportsWebAuthnAutofill())) return;
    try {
      const optionsJSON = opts.take() ?? (await opts.prefetch());
      await finish(await startAuthentication({ optionsJSON, useBrowserAutofill: true }));
    } catch { /* dismissed, or aborted by us: stay silent */ }
  }
  useEffect(() => {
    if (!supported) return;
    void opts.prefetch().then(listenForAutofill);
    return cancelPasskey;                                // unmount and StrictMode re-run
  }, []);

  function onPress() {
    if (busy) return;
    const cached = opts.take();
    const ceremony = cached                              // straight from the tap when we can
      ? startAuthentication({ optionsJSON: cached })
      : opts.prefetch().then((o) => startAuthentication({ optionsJSON: o }));
    setBusy(true); setMessage(null);
    ceremony.then(finish, (e) => {
      setBusy(false);
      if (!isOurAbort(e)) setMessage(COPY.notSignedIn);
      void listenForAutofill();
    });
  }

  if (!supported) return <p className={styles.notice}>{COPY.passkeysUnavailableBrowser}</p>;
  return (
    <div className={styles.passkey}>
      <button type="button" className={styles.primary} onClick={onPress}
              aria-disabled={busy || undefined} aria-describedby="passkey-hint">
        <PasskeyIcon className={styles.icon} aria-hidden="true" focusable="false" />
        Sign in with a passkey
      </button>
      <p id="passkey-hint" className={styles.hint}>Uses your face, fingerprint or PIN.</p>
      <p role="status" className={styles.status}>{busy ? 'Signing in…' : ''}</p>
    </div>
  );
}
```

```css
/* PasskeySignIn.module.css (tokens come from the shared button styles) */
.primary {
  display: flex; align-items: center; justify-content: center; gap: 0.5rem;
  inline-size: 100%; min-block-size: 3rem; padding: 0.75rem 1rem;
  font: inherit; font-weight: 600; color: var(--btn-primary-text);
  background: var(--btn-primary-bg); border: 2px solid transparent; border-radius: 0.375rem;
}
.primary:hover  { background: var(--btn-primary-bg-hover); }
.primary:active { background: var(--btn-primary-bg-active); }
.primary:focus-visible { outline: 3px solid var(--focus-ring); outline-offset: 2px; }
.primary[aria-disabled='true'] { cursor: progress; }
.icon { inline-size: 1.5rem; block-size: 1.5rem; flex: none; fill: currentColor; }
@media (forced-colors: active) { .primary { border-color: ButtonText; } }
```

Check during M0: SimpleWebAuthn's non-autofill path should reach `navigator.credentials.get`/`create` without its own
`await` first. This only matters for iPhones still on iOS 16–17.3. **Unverified.**

---

## Per-screen notes

**1. Set up your account, on a phone (main path).**

- The capability check and `setup/start` run together, then the step renders in its final form.
- The tip line steers people to make the passkey on the phone they'll carry. On Windows, passkeys are not synced out of
  the box, and A22 allows only one sign-in method, with no way to add a second passkey later.
- After `200 Me`, go to Today and fill the confirmation line from router state (`navigate('/', { state: { setUp:
  'passkey' } })`). Nothing is written to storage.
- Nothing the person typed is lost on this path, because there is nothing to type.

**2. Set up your account, on a laptop.**

- Same screen, narrower than the window.
- On Windows with Hello, the passkey lands on that laptop only. The tip line already advises the phone.
- On a laptop without Hello, the browser offers "use a phone or tablet" (a QR code). The phone then holds the passkey
  and the laptop holds nothing. That is the best outcome for a shared laptop.

**3. Set up, opened inside an email app's browser.**

- If the capability check fails, the notice replaces the passkey option, and "Use a password and authenticator app"
  becomes primary.
- If the check passes but creation fails, which is likely in a WKWebView, the "Your passkey was not set up" message
  carries the same advice.
- Opening `/setup` in a real browser **without** the token and **without** an enrolment cookie is a state design.md
  does not define. Don't invent new copy for it; reuse "Your setup session timed out. Open the link from your email
  again." (flagged in open questions).
- **Device check before go-live:** open a real setup link (test environment) on an iPhone and an Android phone from
  Gmail, Outlook and the phone's built-in mail app. Note which ones can create a passkey. This turns the "Unverified"
  above into fact for the actual staff phones.

**4. Sign in, on a phone.**

- Most passkey users will just tap the button. Anyone who taps the email field out of habit gets their passkey offered
  by autofill.
- Passkey-only accounts have no password. Without autofill, they would type a password, fail with the A25 message and
  count towards the A25 lockout. Autofill and the hint line are the two cues that stop this.
- Whether a lockout also blocks passkey sign-in in Identity is **unverified** (open question).

**5. Sign in, on a laptop or shared laptop (§8.2).**

- The browser dialog offers the phone (QR code, with Bluetooth on both devices). Nothing is registered on the laptop
  and nothing is stored there (D21, D22).
- Explain this in the 30-minute staff onboarding (go-live checklist), not on screen (FIDO cross-device guidance).
- Managers who mostly use a Windows laptop without Bluetooth should set up with the authenticator option, or check that
  QR sign-in works on their laptop during onboarding.

**6. Sign in in place, after the idle sign-out (§4.0, §8.5).**

- Same component, rendered in place by the 401 handler.
- On success, call the same `onSignedIn` and go back to the stored route. Don't do a full reload, because §8.5 keeps
  unsaved text in memory and retries the save.
- The autofill request starts again when this view mounts, and is cancelled when it unmounts.

**7. Setting up again after Reset sign-in or an email change (§4.11, §8.6).**

- Same as screen 1.
- The person's phone may still hold the old Grow2Notes passkey. A new one for the same user ID usually replaces it in
  iCloud Keychain and Google Password Manager (**unverified** across all password managers).
- If the person re-sets up with the authenticator instead, the old passkey still appears in the phone's list and fails
  with the A25 message. The manager doing the reset can tell them to ignore or delete it. Don't add
  `signalUnknownCredential()` in v1; it is not needed for this rare case. **[Opinion]**

**8. Today, first arrival.**

- One line, in a `role="status"` region at the top of Today's content: "Your account is set up. Next time, choose
  **Sign in with a passkey**."
- Use the GOV.UK success-banner idea: it confirms "something they're expecting to happen has happened" and goes away
  on the next page (https://design-system.service.gov.uk/components/notification-banner/).
- The password path's component should mirror this with its own wording.
- If the owner wants no addition at all, drop it. Today's own heading still shows the person is in.

---

## Anti-patterns to avoid

- **Jargon:** WebAuthn, FIDO, credential, authenticator (for a passkey), "encrypted digital key", "biometric login".
  Also "Face ID" or "Windows Hello" in our copy, because the web cannot tell which one the device has (Apple HIG).
- **"Passkey" capitalised mid-sentence**, or tied to a platform ("your Google passkey").
- **Starting the OS passkey prompt on page load,** or straight after "Continue" with no explanation on screen first.
- **Hiding the explanation** behind "What is a passkey?", a tooltip or a "Learn more" link. There are no help pages,
  and FIDO's "persist information" principle says keep it visible.
- **A greyed-out or `disabled` passkey button** when passkeys are not available. Replace it with the notice instead.
  The same goes for `disabled` while busy (it drops keyboard focus to `<body>`).
- **Silence after a failed button ceremony,** which leaves in-app browser users stuck. The opposite mistake is a red
  "You cancelled" message: it blames the person, and may not be true, because cancel and failure look the same.
- **Showing an error when the person dismisses autofill.** That was a choice, not a failure.
- **Messages that reveal account state:** "No passkey for this account", "Account locked", "This account uses a
  password". A25 applies to every server rejection.
- **Asking for the email before the passkey** (identifier-first). It adds a step, reveals the method, and defeats
  discoverable credentials (§6.2).
- **Lists of in-app browser user-agent strings** as the only detection. They go stale. Use capability checks plus
  failure copy.
- **Putting the passkey button inside the password `<form>`** as a submit button.
- **Two primary buttons** on the sign-in screen.
- **Nudging password users to "upgrade to a passkey"** after sign-in, or silently creating one (`useAutoRegister`).
  A22 says one method, chosen at setup.
- **Asking the person to name their passkey.** No screen shows it.
- **Storing anything on the device** to remember the method or pre-fill the email (D22).
- **A ceremony timeout under 5 minutes**, such as the 3-minute value in Microsoft's sample.
- **`mediation: "conditional"` without `autocomplete="username webauthn"`** on the field. Autofill silently never
  appears.
- **Two WebAuthn option requests in flight at once.** Identity's single state cookie means the older one fails
  verification.
- **Explaining QR-code cross-device sign-in on the sign-in page.**
- **Any company name other than Grow2Notes** in the relying party, `user.displayName` or copy (D42).

---

## Tensions with decisions

- **One sign-in method, no self-service passkey management (D23 as specified by A22; recovery by A23).** FIDO lists
  "Create, view, and manage passkeys in Account Settings" as a **required** pattern, and its copy guidance uses "a
  passkey" to allow for several passkeys per account
  (https://www.passkeycentral.org/design-guidelines/required-patterns/create-view-and-manage-passkeys-in-account-settings;
  FIDO icon guidelines). Microsoft reports that about 99% of its passkey enrolments came from prompts at key moments,
  not from settings (vendor data, 2024).
  - **The practical effect here:** someone whose only passkey is on a Windows laptop cannot add their phone later
    without a manager's Reset sign-in (Windows does not sync passkeys out of the box, according to passkeys.dev).
  - **Mitigated within the design by:**
    - the setup tip line
    - cross-device sign-in (§8.2)
    - Reset sign-in (§8.6).

  Recorded for information. No change proposed.
- **A method chooser at setup (D23).** Microsoft's guidance is to avoid authentication-method choosers and go
  passkey-first (vendor data). FIDO's "Offer choice" principle and NN/g's advice to keep password options support the
  design's chooser. Passkey autofill on the sign-in email field gives passkey-first behaviour without a chooser. No
  change proposed.

---

## Sources

Standards and specifications
- W3C, Web Authentication Level 3: §5.8.7 client capabilities, §14.3 biometric privacy, §14.5 ceremony privacy, §14.6.1
  user handle, §15.1 timeouts. https://www.w3.org/TR/webauthn-3/
- WHATWG HTML, autofill (`webauthn` token). https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#autofill
- WCAG 2.2 Understanding 3.3.8 Accessible Authentication (Minimum). https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html
- WCAG 2.2 Understanding 3.3.3 Error Suggestion. https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html
- WCAG 2.2 Understanding 4.1.3 Status Messages. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum). https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 2.2.1 Timing Adjustable. https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- MDN, `PublicKeyCredential.getClientCapabilities()`. https://developer.mozilla.org/en-US/docs/Web/API/PublicKeyCredential/getClientCapabilities_static
- MDN, `CredentialsContainer.create()`. https://developer.mozilla.org/en-US/docs/Web/API/CredentialsContainer/create

Research
- GDS blog, "How we made it easier for millions of users to sign into government services" (16 September 2026). https://gds.blog.gov.uk/2026/09/16/how-we-made-it-easier-for-millions-of-users-to-sign-into-government-services/
- Microsoft Security blog, "Convincing a billion users to love passkeys" (12 December 2024; vendor telemetry). https://www.microsoft.com/en-us/security/blog/2024/12/12/convincing-a-billion-users-to-love-passkeys-ux-design-insights-from-microsoft-to-boost-adoption-and-security/
- NN/g, "Passwordless Accounts: One-Time Passwords (OTPs) and Passkeys". https://www.nngroup.com/articles/passwordless-accounts/
- FIDO Design Guidelines (research-based patterns):
  - Principles: https://www.passkeycentral.org/design-guidelines/principles
  - Sign in with a passkey: https://www.passkeycentral.org/design-guidelines/required-patterns/sign-in-with-a-passkey
  - Create, view and manage passkeys: https://www.passkeycentral.org/design-guidelines/required-patterns/create-view-and-manage-passkeys-in-account-settings
  - New account creation with a passkey: https://www.passkeycentral.org/design-guidelines/optional-patterns/new-account-creation-with-a-passkey
  - Cross-device sign-in: https://www.passkeycentral.org/design-guidelines/optional-patterns/cross-device-sign-in

Conventions and platform guidance
- FIDO Alliance, Passkey Icon Usage Guidelines (August 2022). https://fidoalliance.org/wp-content/uploads/2023/12/FIDO-Passkey_Icon_Usage_Guidelines-August2022.pdf
- FIDO Alliance, get the passkey icon. https://fidoalliance.org/get-the-passkey-icon/
- Google for Developers, Communicating passkeys to users. https://developers.google.com/identity/passkeys/ux/communicating-passkeys
- web.dev, Sign in with a passkey through form autofill. https://web.dev/articles/passkey-form-autofill
- web.dev, Create a passkey for passwordless logins. https://web.dev/articles/passkey-registration
- passkeys.dev, Terms. https://passkeys.dev/docs/reference/terms/
- passkeys.dev, Device support (updated 21 September 2026). https://passkeys.dev/device-support/
- Apple Human Interface Guidelines, Managing accounts. https://developer.apple.com/design/human-interface-guidelines/managing-accounts
- GOV.UK Design System, Button. https://design-system.service.gov.uk/components/button/
- GOV.UK Design System, Notification banner. https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK A to Z style guide (contractions; sign in). https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- Microsoft Learn, Enable WebAuthn passkeys in ASP.NET Core (.NET 10). https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0
- SimpleWebAuthn, @simplewebauthn/browser. https://simplewebauthn.dev/docs/packages/browser
- Yubico, Handle WebKit user gesture. https://developers.yubico.com/WebAuthn/Concepts/Handle_WebKit_User_Gesture.html
- Corbado (passkey vendor; treat as unverified), Passkeys in in-app browsers. https://www.corbado.com/blog/passkeys-in-app-browsers
- Corbado (passkey vendor; treat as unverified), Safari WebAuthn user gesture. https://www.corbado.com/blog/safari-webauthn-user-activated-events
