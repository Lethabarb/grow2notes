# Session timeout warning

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. No open or close animation on the warning dialog (foundations.md: no motion). Its connection error is "No connection. Try again." (app-shell.md).

Component key: `session-timeout`. Sources of truth: design.md §4.0 (Sessions), §4.3 (Note form and submit confirmation), §5.6 (working copies and autosave), §6.2 (`POST /api/auth/ping`), §6.8 (session refresh), §8.4–8.5 (idle timeout), A24, A32. Decisions D21, D22 and D23 apply.

The component has two visible parts:

1. **The warning.** A modal dialog shown after 28 minutes without a request to the server: "You'll be signed out in 2 minutes" with **Stay signed in**.
2. **The signed-out state.** At 30 minutes, or on any `401`, the sign-in screen is shown in place of the page. After signing in, the person is back on the same screen with their unsaved text (§8.5).

It also has an invisible part, the **session clock**: a small module that predicts when the server will end the session. It never decides on its own; the server's answer always wins.

---

## Where it's used

| Screen | Where it is | What differs |
|---|---|---|
| **App shell, every signed-in screen** (§4.0): Today, read view and history, Flagged, Report, Manage | Mounted once by the shell, after `<main>` and outside the routes, so it survives navigation. It opens only while the shell shows a signed-in screen. Workers and managers get the same component. | "Activity" means a request the server accepted: page loads, `GET /api/auth/me` on focus and on becoming visible (§6.8), saves, and manager actions. Reading a long note or history without loading anything for 28 minutes brings up the warning. This is correct: the design says idle means the person is idle (§8.5). |
| **Note form** (§4.3), including a past-day note, an earlier-day draft and "Editing submitted note" | Same component | Typing keeps the session alive, because autosave runs about 2 seconds after typing stops (§5.6). The warning can only appear after the person has stopped changing the note. A change still waiting for its autosave is sent at 28 minutes instead of showing the warning. If the save indicator already says "Not saved: no connection…", the warning can still appear, and Stay signed in can fail for the same reason. |
| **Submit confirmation** and **Discard draft** confirmation (§4.3) | Same component | Before the warning opens, an open confirmation is closed as if **Go back** or Cancel was chosen. Nothing is submitted or discarded. After the warning, the person taps Submit note again and sees the participant's name again. As built (T12), focus then goes back to **Submit note** after the Submit confirmation alone; after the Discard dialog it goes to **More actions** on the form, as after its own Go back, and to **Discard draft** on a manager's read-only draft. |
| Not used: sign-in and account setup (§4.1) | No session exists | The setup page has its own 30-minute enrolment cookie and its own message: "Your setup session timed out. Open the link from your email again." That belongs to the sign-in component. |

---

## Best practice

**The rule**

- **[Standard]** WCAG 2.2 SC 2.2.1 Timing Adjustable (Level A). For a time limit set by the content, one option is to warn the user before time runs out and give at least 20 seconds to extend it with a simple action, extendable at least ten times. Time limits added for security are covered by the SC. Only things like time-limited two-factor codes are named as possibly "essential". Limits longer than 20 hours are exempt. Grow2Notes' 2-minute warning and one-button extension meet this. Within the 12-hour absolute limit the session can be extended more than 20 times, which is more than the 10 required. https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- **[Standard]** Technique SCR16, "Providing a script that warns the user a time limit is about to expire", is the sufficient technique for this SC. https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR16
- **[Standard]** SC 2.2.5 Re-authenticating (AAA): after re-authenticating, the user carries on without losing data. SC 2.2.6 Timeouts (AAA): users must be told how long inactivity can last before data is lost, *unless the data is kept for more than 20 hours*. Grow2Notes meets both because drafts and pending edits stay on the server and the person returns to the same note. So there is no need to warn about the timeout in advance. https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html · https://www.w3.org/WAI/WCAG22/Understanding/timeouts.html
- **[Convention]** UK government services warn **at least 2 minutes** before the timeout. HMRC shows its dialog 2 minutes before a 15-minute timeout. https://design-system.dwp.gov.uk/patterns/manage-a-session-timeout · https://design.tax.service.gov.uk/hmrc-design-patterns/service-timeout/ · https://design.homeoffice.gov.uk/design-system/patterns/help-users-to/manage-service-timing-out
- **[Research]** Advance notice backfires. DWP found that timeout information on start pages was read as a time limit for the whole service. Home Office found that warning users in advance caused confusion and anxiety for the same reason. Neither publishes a participant count for this finding. https://design-system.dwp.gov.uk/patterns/manage-a-session-timeout/user-research · Home Office URL above.

**Modal or page**

- **[Research]** Home Office ran an A/B test of a modal dialog against a warning page with 8 assistive-technology users (visually impaired or dyslexic). The modal alerted them sooner. On the page version, some screen readers read the standard page parts before the warning. Participants understood how to stay signed in and what would happen if they did nothing. Home Office URL above; also https://github.com/alphagov/govuk-design-system-backlog/issues/104
- **[Convention]** Home Office says to avoid modal dialogs *except* for timeouts. This is the one accepted case of a dialog the system opens on its own. Home Office URL above.
- **[Research]** HMRC tested with 5 users, and all understood the warning and stayed signed in. It was lab-tested with JAWS, ZoomText, NVDA and VoiceOver. The sample is small. HMRC URL above.

**Wording**

- **[Research]** Home Office: users know they have to act, so they often close the warning with the main button without reading it. The button label has to carry the meaning on its own. Users also hoped their input would be saved and expected to return to the page they were on. Home Office URL above.
- **[Convention]** HMRC uses a **Stay signed in** button, an optional **Sign out** link, and a signed-out page that explains it signed the user out for their security, with a **Sign in** button. Home Office: tell the user whether their progress will be saved. HMRC and Home Office URLs above.
- **[Convention]** GOV.UK style: use "sign in", not "log in". Avoid negative contractions ("can't", "don't"), because many readers misread them; positive ones like "you'll" are fine. Use numerals. https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

**Countdown and screen-reader announcements**

- **[Research]** Home Office: counting down in minutes, then in 20-second steps during the last minute, "worked best for both sighted and non-sighted users". The same steps are used on screen and for screen-reader announcements, with `aria-live="polite"`. Home Office URL above.
- **[Convention]** The implementations differ. HMRC updates every minute, then in 20-second steps, and puts the countdown in a visually hidden `aria-live="assertive"` paragraph while the visible countdown is `aria-hidden`. MoJ announces every 15 seconds, then *every second* (assertive) below 20 seconds. https://raw.githubusercontent.com/hmrc/hmrc-frontend/main/src/components/timeout-dialog/timeout-dialog.js · https://design-patterns.service.justice.gov.uk/components/timeout-warning
- **[Opinion]** (practitioner report, not replicated) In the DWP design-system discussion (Aug 2023), a contributor reported that an `aria-hidden` visible countdown cannot be read by macOS Zoom's hover text. https://github.com/dwp/design-system/discussions/449
- **[Standard]** Do not use `role="timer"`. In WAI-ARIA it has an implicit `aria-live="off"`, so its changes are not announced. https://www.w3.org/TR/wai-aria-1.2/#timer

**Focus, keyboard and close requests**

- **[Standard]** HTML `showModal()` remembers the focused element, focuses the first focusable element (or an `autofocus` one), makes the rest of the page inert, and puts focus back when the dialog closes. https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element · https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog
- **[Standard]** WAI-ARIA APG modal dialog: Tab stays inside, Escape closes, the name comes from a visible title (`aria-labelledby`). Initial focus goes to the control most likely to be used, and focus returns when the dialog closes. https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- **[Convention]** HMRC and Home Office: **Escape closes the warning and keeps the user signed in**. Home Office also lets the browser back button close it, and puts focus back where it was. Home Office notes that mobile screen readers started on the main button, and this did not stop anyone. HMRC and Home Office URLs above.
- **[Standard]** Escape (desktop) and the Android back gesture send a *close request*, which fires `cancel` on a modal dialog. To stop back-button trapping, Chromium skips or ignores `cancel` when there has been no user activation since the dialog opened. A dialog opened by a timer can therefore close straight away with only a `close` event. https://developer.mozilla.org/en-US/docs/Web/API/HTMLDialogElement/cancel_event · https://github.com/WICG/close-watcher · https://issues.chromium.org/issues/41484805

**Timers on phones and in background tabs**

- **[Standard]** (browser vendor documentation) Chrome throttles timers in hidden pages to once per second, and to once per minute after 5 minutes hidden. Frozen pages run no timers at all. `visibilitychange` is the reliable signal that the page has come back. A countdown that counts its own ticks drifts or stops. https://developer.chrome.com/blog/timer-throttling-in-chrome-88 · https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- **[Opinion]** On a locked phone the warning is rarely seen: the person usually comes back after the screen locked and the page was frozen. What usually matters on phones is what happens when the page becomes visible again, not the dialog itself.

---

## Recommendation for Grow2Notes

### Anatomy

```
+-----------------------------------+
|                                   |   ::backdrop, dims the page
|  +-----------------------------+  |
|  | You'll be signed out in     |  |   <h2>, the dialog's name and
|  | 2 minutes                   |  |   the polite live region
|  |                             |  |
|  | (status line, empty)        |  |   <p role="status">: "Connecting…"
|  |                             |  |   or the connection error
|  | [      Stay signed in     ] |  |   <button type="button">, full width,
|  +-----------------------------+  |   48 px tall, focused on open
|                                   |
+-----------------------------------+
```

- One heading, one status line (empty unless needed), one button. **No close "X"** (it would be unclear whether X means "sign me out" or "keep me in"). **No Sign out link**, because the design doesn't include one and Sign out is already in the account menu.
- It uses the same native `<dialog>` styling as the Submit confirmation (centred card, same surface, border, radius and button style).

### Behaviour

**The session clock** (`src/api/session.ts`, beside the session-ended event: a plain TypeScript module, not React,
nothing stored on the device):

1. **Activity = a request the server accepted.** The shared `api()` fetch wrapper records the *send time* of every
   request whose answer comes and is not `401`, the antiforgery token's and a resent change's included. It counts
   once the answer's head has come, as the renewed session cookie comes in it. Using the send time means that a
   request which renewed the session can only make the client expire early, never late. Failed network requests do
   not count, because the server never saw them. Any other answer counts, a `5xx` included (step 6): the session
   check runs before the endpoint and renews the session even when the endpoint then fails. Two answers renewed
   nothing and count all the same, as the client cannot tell them from the rest: a `503` that App Service's front
   end gives during a restart, which never reached the app, and a `500` from a database failure inside the session
   check less than 15 minutes after the request that last renewed the cookie, before the cookie's own sliding renewal
   starts. After either, the clock can be late until the server's next `401` ends it (step 7; A57, *Tensions with
   decisions*). Mouse movement, scrolling and taps do **not** count, and nothing pings in the background (§6.8,
   §8.5).
2. **Times are measured, not counted.** On every wake-up the clock works out `Date.now() − lastAccepted`. It sets one `setTimeout` for the next mark: 28:00, 29:00, 29:20, 29:40, 30:00.
3. **At 28:00**, if the note form has a change waiting for autosave, the clock asks autosave to send it now and waits,
   holding its phase on the save (`holdWhile`). If the save is accepted, the clock resets and no warning appears ("a
   person typing is never timed out", §8.5). Otherwise the warning opens.
4. **While the page is hidden, the clock changes nothing.** Nobody can see a hidden tab's warning.
5. **When the page becomes visible**, the clock's one `visibilitychange` listener calls `GET /api/auth/me` through the
   shell's `refreshMe()` (§6.8) and holds its current state until that call settles, so one listener sends the
   request and the clock holds on that very promise (app-shell.md *Conflicts resolved* 6). `200` means the session is
   alive (perhaps kept alive by another tab): reset the clock, and close the warning or the signed-out view if either
   is showing. `401` means signed out. If the call fails for network reasons, fall back to the clock. Holding avoids
   the warning flashing up for a moment and then closing.
6. **Any non-`401` response at any time** resets the clock and closes the warning. The session really was extended. Focus goes back to where it was.
7. **At 30:00 on a visible page, or on any `401` that ends the session** (not a failed sign-in's, an ended setup's or
   logout's), the clock goes to *expired*. It never sends a request to "check" at 30:00, because a request would
   extend the session. Nor does the signed-out view that then shows: it asks for passkey options only once the server
   has surely ended the session (sign-in.md *Interactions* 2, design.md A61).

**Holds, and the clock's life.** `api()` holds the clock (`holdWhile`) while each call is out, as its answer may
reset the clock or end it ("If a request is in flight, wait for it", app-shell.md *28:00 idle on a visible page*).
Holds are counted, so two at once, such as the visible page's `/me` and a save, both settle before the clock decides.
While held, the phase neither opens the warning, nor steps, nor ends; then it takes the step that the time gives. So
Stay signed in's ping keeps the dialog open until it is answered. `startClock(checkSession)` starts the clock, active
from that moment, and returns the function that stops it. The shell starts it once, for its whole life, signed in or
not, with `() => refreshMe(queryClient)` as `checkSession`; a sign-in is an accepted request, which resets it. Its
state (the phase, the newest accepted send time, whether a `401` ended it, the count of holds and the timer) belongs
to one start and is kept only while it is started: the stop removes the listener and the timer and drops that state,
so a hold that settles afterwards changes nothing. While it is stopped, `getPhase()` gives `active`, and
`requestAccepted` and `holdWhile` do nothing.

**The warning** (`src/routes/SessionTimeoutWarning.tsx`, mounted once by the shell after `<main>`). It opens only
while the shell shows a signed-in screen: `/me` has answered and the page is not a setup page. So it never opens over
the start-up message, sign-in or setup, whose requests still move the clock.

1. When opening, the header first closes the Account panel and, if focus was in the panel, moves it to Account, so
   the warning gives focus back to Account and never to a hidden Sign out. Then close any other open `<dialog>` with
   a plain `close()`. Every Grow2Notes confirmation treats a plain close as Go back or Cancel. Also close any open
   menu (the note form's "Discard draft" menu). Then call `showModal()`.
   As built (T12): the note form's menu is More actions' disclosure (note-form.md component 13). Its panel closes
   as the warning opens, whether or not the Discard dialog is open, focus that is in it moving to More actions first,
   so that the warning never gives focus back to a hidden Discard draft; the Discard dialog closes as Go back, focus
   going to More actions, where the warning gives it back.
2. Focus lands on **Stay signed in**, the only focusable element.
3. **Stay signed in** → `POST /api/auth/ping` (with the `X-XSRF-TOKEN` header, §6.1). The press first empties the
   status line, so a failure that repeats is announced again. The button is `aria-disabled` until the answer, and the
   dialog **stays open until the server answers**:
   - `204`, or any other answer but a `401`: the clock resets, the dialog closes, and focus returns to the element
     focused before.
   - `401` (the session had already ended, or the 12-hour limit was reached): the clock ends, the dialog closes, and
     the signed-out state follows.
   - Network failure or a 10-second timeout: keep the dialog open, show the connection error ("No connection. Try
     again."), and leave the button ready. The countdown keeps running.
4. **Escape or the Android back gesture = Stay signed in.** Handle `cancel` with `preventDefault()` and run the same
   ping. Chromium may skip `cancel` and close the dialog straight away, so `close` is handled too. If the warning
   closes while still in a warning phase, send the ping. If that ping fails, reopen the warning with the connection
   error. Chromium fires `close` a frame or more after the dialog closes, so a `close` that finds the dialog open
   again belongs to an earlier closing and is ignored.
5. Clicking the backdrop does nothing (`closedby` stays at its `showModal()` default, which ignores clicks outside).
6. The countdown updates **only at 2 minutes → 1 minute → 40 seconds → 20 seconds**, on screen and in the announcement at the same time. It never ticks every second.

**The signed-out state** (§8.5, "shows sign-in in place"):

1. The URL does not change (IDs only, §4.0). The page is **not** reloaded. A reload would lose the unsaved text and create a new autosave `clientId`, and the retried save would then look like another tab (`409 draft.taken_over`, §5.6).
2. The signed-in layout keeps the current route **mounted but `hidden`** (removed from display and from the accessibility tree), and shows the §4.1 sign-in screen in its place. Unsaved text, ticks, the flag reason, a half-typed review comment or a half-filled Manage form all stay in memory and nothing is shown. Nothing is written to device storage (D22).
3. Autosave pauses while signed out, so it does not keep sending requests that will get `401`.
4. Focus moves to the signed-out heading, because the element that had focus is now hidden. The page title becomes "Grow2Notes – Sign in".
5. **After signing in:**
   - **Same person** (`/me.userId` matches the remembered user ID): unhide the route, refetch the queries, and resume autosave, which retries the save with the same `clientId` and next `seq`. Focus goes to the page's `<h1>`, or for the note form, the participant's name heading.
   - **A different person** (a shared laptop, §8.2): do a full reload to Today (`location.replace('/')`). The previous person's unsaved text and cached data are dropped and never saved under the new person's name.
6. Autosave is the only thing retried automatically. **Submit note, Save changes, Discard and Mark reviewed are never replayed.** If one of them got a `401`, the person taps it again after signing in.

### States

| State | What shows | Notes |
|---|---|---|
| **Hidden** (default) | Nothing | The clock is running. |
| **Warning: open** | Heading "You'll be signed out in 2 minutes", Stay signed in focused | Modal, page inert, backdrop dims. |
| **Warning: countdown** | Heading text changes to 1 minute, then 40 seconds, then 20 seconds | Polite announcement at each change. |
| **Hover** (laptop) | Button uses the shared primary hover colour | Only under `@media (hover: hover)`, so taps don't leave a sticky hover. |
| **Focus** | Focus ring on the button: 3 px outline, 2 px offset, at least 3:1 against the dialog surface | Always visible (A32). Uses `outline`, never only `box-shadow`. foundations.md's ring (app-shell.md *Conflicts resolved* 17). |
| **Active** (pressed) | Shared primary pressed colour | No movement. |
| **Loading** (ping sent) | Up to 1 s: no change (repeat taps ignored). After 1 s: status line "Connecting…" | `aria-disabled="true"` on the button, **never `disabled`**, which would push focus out of the dialog. |
| **Error** (network) | Status line "No connection. Try again." The button is ready again. | Error text in words (A32). Countdown continues. |
| **Disabled** | Never | |
| **Expired / signed out** | §4.1 sign-in screen in place of the page, with the signed-out heading and line above it | Route kept mounted and hidden. |
| **Empty / read-only** | Not applicable | |

### Phone and laptop

| | Phone | Laptop |
|---|---|---|
| Size | Width = screen minus 16 px each side; button full width, at least 48 px tall | Width up to 28rem, centred; same full-width button |
| When it's seen | Mostly when the screen stays on (reading, or left open on a table). After the phone was locked, the person usually comes back to the signed-out state or straight to the app (step 5 above). | A hidden tab shows nothing until it is visible again; then `/me` decides. A second tab left visible but unused can warn even though another tab kept the session alive. Pressing Stay signed in is harmless, and focusing that window corrects it through `/me`. |
| Keyboard | If the on-screen keyboard was open (the Guided notes box had focus), it closes when focus moves to the button. After Stay signed in, focus returns to the box; whether the keyboard reopens depends on the browser. | Tab stays on the one button; Enter or Space activates; Escape = Stay signed in. |
| Zoom | Text set in rem; the dialog wraps and scrolls inside (`max-block-size: calc(100dvh − 2rem)`) at 200% text size and at 320 px width | Same |

### Copy

| Moment | Text | Source |
|---|---|---|
| Warning opens (28:00) | Heading: **You'll be signed out in 2 minutes**. Button: **Stay signed in** | design.md §4.0, §8.5 (exact) |
| 29:00 | You'll be signed out in 1 minute | Same sentence as design, Home Office steps |
| 29:20 | You'll be signed out in 40 seconds | Same |
| 29:40 | You'll be signed out in 20 seconds | Same |
| Ping slower than 1 s | Connecting… | Proposed, **not in design.md** |
| Ping failed (network) | No connection. Try again. | Proposed, **not in design.md** (app-shell.md *Conflicts resolved* 15) |
| Signed out (30:00 or any `401`) | Heading **You've been signed out**, then "Sign in to go back to where you were.", then the §4.1 sign-in controls | Proposed, **not in design.md** |
| Page title while signed out | Grow2Notes – Sign in | Follows the §4.0 title pattern |

All proposed strings use sentence case and no negative contractions, so they suit readers with English as a second language. "Sign in" and "signed out" match the rest of the app.

### Accessibility

- **Semantics.** Use the native `<dialog>` opened with `showModal()`, which gives `role="dialog"` and `aria-modal="true"` implicitly. Add `aria-labelledby` pointing at the `<h2>`. No `aria-describedby`: the heading *is* the message. No `role="alertdialog"`. Focus moves into the dialog, which already announces it, and alertdialog would add an ARIA override for no tested benefit ([Opinion]). Use a real `<button type="button">`.
- **Live region.** Put `aria-live="polite"` and `aria-atomic="true"` on the **visible** `<h2>`. Do not `aria-hide` it, because of the macOS Zoom hover-text report. The `<dialog>` is always in the DOM (closed), so the live region exists before its text changes. When the dialog opens, focus reads "You'll be signed out in 2 minutes, dialog, Stay signed in, button". At each step the polite region announces the new sentence. The status line is a `<p role="status">` that is always present. The visible text change is the main channel; the announcement is an extra, because live-region support varies between screen readers ([Opinion]).
- **Keyboard.** Tab and Shift+Tab stay on the button; Enter and Space activate it; Escape = Stay signed in. There is no keyboard trap beyond the modal's intended one, and Escape always gets out.
- **Focus.** Opening the dialog focuses the button. Closing it puts focus back on the element focused before, natively. On sign-out, focus moves to the signed-out heading (`tabindex="-1"`). After signing in again, focus goes to the page's heading.
- **Voice control.** The visible label "Stay signed in" is the accessible name, so "click Stay signed in" works (SC 2.5.3).
- **WCAG 2.2 criteria met:**
  - 2.2.1 Timing Adjustable (A)
  - 2.2.5 Re-authenticating (AAA, met anyway)
  - 2.2.6 Timeouts (AAA, met by server-side drafts)
  - 1.3.1 Info and Relationships
  - 1.4.3 Contrast (Minimum)
  - 1.4.4 Resize Text
  - 1.4.10 Reflow
  - 1.4.11 Non-text Contrast (1 px border at least 3:1; a shadow alone does not count)
  - 1.4.12 Text Spacing
  - 2.1.1 Keyboard
  - 2.1.2 No Keyboard Trap
  - 2.4.2 Page Titled (signed-out view)
  - 2.4.3 Focus Order
  - 2.4.7 Focus Visible
  - 2.4.11 Focus Not Obscured (Minimum), because the dialog is in the top layer
  - 2.5.3 Label in Name
  - 2.5.8 Target Size (Minimum); 48 px is above the 44 px required by A32
  - 3.3.1 Error Identification (connection error in words)
  - 4.1.2 Name, Role, Value
  - 4.1.3 Status Messages

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria needed.** Native `<dialog>` covers focus, inertness, Escape and the top layer.
- Render the `<dialog>` **always**, and open and close it with `showModal()` and `close()` in an effect. Never use `{open && <dialog>}`, which loses the live region and the focus return. Don't rely on React's `autoFocus` prop for dialog contents. The button is the first focusable element, so `showModal()` focuses it natively.
- Read the clock with `useSyncExternalStore`. The phase is a string, so the snapshot stays stable between renders.
- The shared `api()` wrapper (also used by TanStack Query's `queryFn`s and mutations) is the only place that calls
  `requestAccepted(sentAt)` and `sessionEnded()`, and it holds the clock (`holdWhile`) while each call is out. Turn
  off TanStack retries for `401`.
- Keep the client limits as constants beside a comment pointing to §8.4 (`ExpireTimeSpan = 30 min`). Nothing is
  shortened for tests: the clock's tests use fake timers and a fake system time, and the end-to-end test runs the
  28 minutes on Playwright's `page.clock` over a real session (tests/README.md). Nothing here is a user setting.
- Unit-test the clock with fake timers and a fake system time: 28:00 opens, the countdown steps are right, 30:00 expires, a visible-page `/me` returning `200` clears an expiry, and a pending autosave holds the warning. The design's M2 acceptance test applies: "After an idle timeout and signing back in, the draft is intact" (§14).

```ts
// src/api/session.ts, beside the session-ended event: no React, nothing stored on the device.
// Mirrors the server: 30 min idle (design.md §8.4), warning at 28 (A24).
const WARN = 28 * 60_000, END = 30 * 60_000;
const MARKS = [WARN, END - 60_000, END - 40_000, END - 20_000, END];
const PHASES = ['active', 'warn-2m', 'warn-1m', 'warn-40s', 'warn-20s', 'expired'] as const;  // after 0 to 5 marks
export type Phase = (typeof PHASES)[number];
export type WarningPhase = Exclude<Phase, 'active' | 'expired'>;
export const isWarning = (p: Phase): p is WarningPhase => p !== 'active' && p !== 'expired';

// One start's state, kept only while it is started: its stop drops it.
type Clock = {
  phase: Phase;
  lastAccepted: number;                      // send time of the newest request the server accepted, or the start's
  ended: boolean;                            // a 401 has ended the session since
  holds: number;                             // promises holding the phase as it is
  timer?: ReturnType<typeof setTimeout>;
};
let clock: Clock | undefined;
const listeners = new Set<() => void>();
const tell = () => listeners.forEach((l) => l());

function update(c: Clock) {
  if (clock !== c) return;                   // stopped since: a late hold changes nothing
  clearTimeout(c.timer);
  if (c.holds > 0 || document.visibilityState === 'hidden') return;  // waiting for an answer, or nobody can see it
  const idle = Date.now() - c.lastAccepted;
  const passed = c.ended ? MARKS.length : MARKS.filter((m) => m <= idle).length;
  if (PHASES[passed] !== c.phase) { c.phase = PHASES[passed]; tell(); }
  if (passed < MARKS.length) c.timer = setTimeout(() => update(c), MARKS[passed] - idle);  // the next mark only
}

/** api() only: a request sent at sentAt got an answer that was not a 401. */
export function requestAccepted(sentAt: number) {
  if (!clock) return;
  clock.ended = false;
  clock.lastAccepted = Math.max(clock.lastAccepted, sentAt);
  update(clock);
}
/** api() only, on a 401 that ends the session: ends the clock, then raises the session-ended event. */
export function sessionEnded() {
  if (clock) { clock.ended = true; update(clock); }
  // …then calls the event's listeners (onSessionEnded)
}
/** Holds the phase until p settles: every api() call while it is out, the /me on becoming visible. Counted. */
export function holdWhile(p: Promise<unknown>) {
  const c = clock;
  if (!c) return;
  c.holds += 1;
  const release = () => { c.holds -= 1; update(c); };
  void p.then(release, release);
}
export const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l); }; };
export const getPhase = (): Phase => clock?.phase ?? 'active';  // active while stopped

/** Starts the clock, active from now, and returns its stop. The shell starts it once, for its whole life, with
 *  () => refreshMe(queryClient), so the request is shared, not doubled (app-shell.md Conflicts resolved 6). */
export function startClock(checkSession: () => Promise<unknown>): () => void {
  const c: Clock = { phase: 'active', lastAccepted: Date.now(), ended: false, holds: 0 };
  clock = c;
  const onVisibilityChange = () => {
    if (document.visibilityState === 'visible') holdWhile(checkSession()); else clearTimeout(c.timer);
  };
  document.addEventListener('visibilitychange', onVisibilityChange);
  update(c);
  return () => {
    document.removeEventListener('visibilitychange', onVisibilityChange);
    clearTimeout(c.timer);
    clock = undefined;
    if (c.phase !== 'active') tell();       // getPhase() gives active from now on
  };
}
```

```ts
// src/api/api.ts: the clock's part of the shared wrapper.
export function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const call = request<T>(path, options);
  holdWhile(call);                           // its answer may reset the clock or end it
  return call;
}
// For every request it sends, the antiforgery token's and a resent change's included:
const sentAt = Date.now();
const response = await fetch(path, { ...init, signal: controller.signal });
if (response.status !== 401) requestAccepted(sentAt);  // once the head has come, whatever the body then does
// And a 401 that ends the session, but not a failed sign-in's, an ended setup's or logout's:
if (response.status === 401 && endsSession(path)) sessionEnded();

// src/routes/useRefreshMe.ts, in the shell: the clock's visibilitychange listener is the only one.
useEffect(() => {
  const onFocus = () => { refreshMe(queryClient).catch(() => {}); };
  window.addEventListener('focus', onFocus);
  const stopClock = startClock(() => refreshMe(queryClient));
  return () => { window.removeEventListener('focus', onFocus); stopClock(); };
}, [queryClient]);
```

```tsx
// src/routes/SessionTimeoutWarning.tsx: rendered once by AppShell, after <main>, always in the DOM.
const countdown: Readonly<Record<WarningPhase, string>> = {
  'warn-2m': strings.sessionWarning.twoMinutes,     // "You’ll be signed out in 2 minutes", in src/copy
  'warn-1m': strings.sessionWarning.oneMinute,
  'warn-40s': strings.sessionWarning.fortySeconds,
  'warn-20s': strings.sessionWarning.twentySeconds,
};

/** signedIn: the shell shows a signed-in screen (/me has answered, and it is not a setup page). */
export function SessionTimeoutWarning({ signedIn }: { signedIn: boolean }) {
  const phase = useSyncExternalStore(subscribe, getPhase);
  const open = signedIn && isWarning(phase);
  const ref = useRef<HTMLDialogElement>(null);
  const [pinging, setPinging] = useState(false);
  const [failed, setFailed] = useState(false);
  const connecting = useDelayed(pinging, 1_000);    // "Connecting…" from 1 s
  const [wasOpen, setWasOpen] = useState(open);
  if (wasOpen !== open) { setWasOpen(open); setFailed(false); }  // each warning opens with an empty line

  useEffect(() => {
    const dialog = ref.current!;
    // The header has already closed the Account panel, moving focus to Account if it was in it. Closing any other
    // open <dialog> with a plain close() (= Go back) and the note form's menu comes with the first of them.
    if (open && !dialog.open) dialog.showModal();   // focuses the only button
    else if (!open && dialog.open) dialog.close();  // focus returns to where it was
  }, [open]);

  async function staySignedIn() {
    if (pinging) return;
    setFailed(false);                               // a failure that repeats is announced again
    setPinging(true);
    try {
      await api('/api/auth/ping', { method: 'POST' });  // api() holds the clock: the dialog stays open until answered
    } catch (e) {
      if (classify(e) === 'offline') {              // a 401 has ended the clock, which closes the dialog
        setFailed(true);
        const dialog = ref.current!;
        if (!dialog.open && isWarning(getPhase())) dialog.showModal();  // the browser closed it with no cancel
      }
    } finally {
      setPinging(false);
    }
  }

  return (
    <dialog
      ref={ref}
      className={s.dialog}
      aria-labelledby="session-timeout-heading"
      onCancel={(e) => { e.preventDefault(); void staySignedIn(); }}            // Escape / Android back
      onClose={(e) => { if (open && !e.currentTarget.open) void staySignedIn(); }}  // Chromium may skip cancel
    >
      {/* Closed, it holds the first step's sentence, so opening it writes nothing into its live region. */}
      <h2 id="session-timeout-heading" className={s.heading} aria-live="polite" aria-atomic="true">
        {countdown[isWarning(phase) ? phase : 'warn-2m']}
      </h2>
      <p role="status" className={s.statusLine}>
        {connecting ? strings.sessionWarning.connecting : failed ? strings.sessionWarning.noConnection : ''}
      </p>
      {/* The shared Button: aria-disabled while busy, never disabled. */}
      <Button variant="primary" busy={pinging} className={s.staySignedIn} onClick={() => void staySignedIn()}>
        {strings.sessionWarning.staySignedIn}
      </Button>
    </dialog>
  );
}
```

```tsx
// src/routes/Header.tsx, the signed-in header: the Account panel closes as the phase becomes a warning.
const warning = useSyncExternalStore(subscribe, () => isWarning(getPhase()));
const [warned, setWarned] = useState(warning);
if (warned !== warning) { setWarned(warning); if (warning) setOpen(false); }  // during render, as a location change
useLayoutEffect(() => {                             // runs before the warning's effect opens it
  if (warning && panelRef.current?.contains(document.activeElement)) accountRef.current?.focus();
}, [warning]);

// src/routes/AppShell.tsx: the warning after the frame, so after <main>, outside anything made hidden or inert.
useRefreshMe();                                     // starts the clock, for the shell's whole life
const frameMe = signedOutFrame ? undefined : me;    // a signed-in screen: /me has answered, not a setup page
return (
  <>
    <ShellFrame me={frameMe} page={page}>{/* the route's screen, sign-in or the start-up message */}</ShellFrame>
    <SessionTimeoutWarning signedIn={frameMe !== undefined} />
  </>
);
// The signed-out view (app-shell.md component 7) goes inside <main>, with the route kept mounted and hidden beside
// it, never by hiding the whole shell (app-shell.md Conflicts resolved 10).
```

```css
/* SessionTimeoutWarning.module.css: foundations.md's dialog, with no shadow and no open or close animation
   (app-shell.md Conflicts resolved 17). The focus ring is base.css's: 3 px --colour-focus, 2 px outside. */
.dialog {
  inline-size: min(100% - 2rem, 28rem);
  max-inline-size: none;                            /* the browser's own cap would leave less than 16 px a side */
  max-block-size: calc(100dvh - 2rem);
  overflow-y: auto;                                 /* scrolls inside itself at 200% text */
  overscroll-behavior: contain;
  padding: var(--space-5) var(--space-4);           /* confirm-dialog.md's padding */
  border: var(--border-divider) solid var(--colour-text);  /* 1 px, ≥3:1 boundary (SC 1.4.11) */
  border-radius: var(--radius-m);
  background: var(--colour-surface);
  color: var(--colour-text);
}
@media (min-width: 40rem) { .dialog { padding: var(--space-6); } }
/* tokens.css declares its tokens on ::backdrop as well as :root: older browsers' backdrops inherit nothing. */
.dialog::backdrop { background: var(--colour-backdrop); }
.heading { margin: 0; }                             /* an <h2>: --font-size-h2, bold, from base.css */
.statusLine { margin: 0; }                          /* never display:none: it is a live region */
.statusLine:not(:empty) { margin-block-start: var(--space-3); }
/* The shared primary Button: 48 px, weight 700, its hover and pressed colours; full width at every size here. */
.staySignedIn { inline-size: 100%; margin-block-start: var(--space-5); }
@media (forced-colors: active) { .dialog { border: 2px solid CanvasText; } }
```

---

## Per-screen notes

**App shell (§4.0), all signed-in screens**
- Mount it once, in the shell after `<main>` and outside any element that is made `hidden` or `inert`. It opens only
  over a signed-in screen, never over the start-up message, sign-in or setup.
- Workers and managers get the same dialog, copy and timing.
- The manager's Flagged badge and `today` are refreshed by the same `/me` call that decides the session when a page becomes visible. One request does both jobs, and there is no new polling.
- Manage screens and the review panel's comment box have no autosave. Text typed there survives a sign-out because the route stays mounted and hidden, and is lost only if a different person signs in.
- The 12-hour absolute limit shows up only as a `401`, so it goes straight to the signed-out state. Never show the warning for it: Stay signed in cannot extend it, so the warning would mislead.

**Note form (§4.3)**
- At 28 minutes, send a waiting autosave instead of warning. Only warn if nothing is accepted.
- The warning appears above the save indicator and any "Not saved: no connection" banner. Both messages are correct together. If the connection is down, Stay signed in shows the connection error. At 30 minutes the person is signed out with their text kept in memory. When they sign in again (once back online), autosave retries and Submit becomes enabled once the save succeeds (§3.4).
- After Stay signed in, focus returns to the field the person was in, such as the Guided notes box or the flag Reason.
- After signing in again as the same person, the form reappears exactly as it was. The retried save uses the page's existing `clientId` and next `seq`, so the "This note was changed on another device or tab" banner does **not** appear because of the timeout.
- This works the same for a past-day note (the banner stays), an earlier-day draft, and "Editing submitted note (version N)", whose pending edit autosaves in the same way (§3.5).
- As built (T10): a mounted note form registers, in `session.ts` beside `holdWhile`, whether a change is unsaved and
  its `flushNow()` (`registerSaveFlush()`). The clock asks as the phase would leave `active`, once in each idle period
  by its newest accepted send time, only while a change is unsaved, and never once a `401` has ended the session, and
  holds on the answer: a save that gets no answer lets the warning open as the hold settles, and any answer, a `422` or
  a `5xx` included, starts the clock afresh, as any accepted request does (A57). The shell tells its screen whether the
  signed-out view hides it (`HiddenBySignInContext`), as the clock's phase cannot, since view C's passkey options and a
  different person's sign-in are accepted requests too: autosave pauses while it does, its timers and retries cleared,
  and, as the same person's form shows again, sends at once under the same `clientId` and the next `seq`. Autosave's
  retries stop 28 minutes after the person last acted on the form, as a failing server's `5xx` answers, which renew the
  session (A57), would otherwise keep an unattended page's session alive with no warning: the clock then warns 28
  minutes after the last retry, as on any idle page (A66).

**Submit confirmation (§4.3)**
- If the warning is due while the confirmation is open, close the confirmation first (= Go back), then open the warning. Focus then returns to **Submit note** on the form, not to "Submit note for Jane Citizen" inside the confirmation. This avoids a quick double tap or a second Enter going from Stay signed in straight onto the commit button ([Opinion]: the two buttons sit in the same place on a phone).
- The person taps Submit note again and sees the participant's name again. That matches the design's wrong-participant safeguard (§3.9).
- If Submit itself gets a `401`, sign-in appears in place, and Submit is **not** retried automatically. The person submits again after signing in.
- The **Discard draft** confirmation is handled the same way (closed as Cancel).
- As built (T12): after the Discard dialog, focus returns to **More actions**, as after its own Go back, and on a
  manager's read-only draft to **Discard draft**: **Submit note** is the Submit confirmation's alone.

---

## Anti-patterns to avoid

- **Counting ticks with `setInterval`.** It drifts and stops in hidden or frozen pages. Always work from `Date.now() − lastAccepted`.
- **A countdown that ticks every second or is announced every second** (MoJ does this assertively below 20 s). Use minutes, then 20-second steps (Home Office research).
- **`aria-hidden` on the visible countdown** with a separate hidden copy. It breaks Zoom hover text, and it isn't needed when the visible text changes only four times.
- **`role="timer"`** (it is never announced) or **`role="alert"` on the countdown**.
- **Background keep-alive**: pinging on mouse movement, scroll or a timer. It breaks "idle means idle" (§6.8, §8.5) and makes the timeout meaningless.
- **Sending a request at 30:00 to "check" the session.** Any request extends it.
- **Calling `/api/auth/logout` or reloading on timeout.** Logout sends `Clear-Site-Data` and reloads, which throws away the unsaved text and the autosave `clientId`. That path is only for the person's own Sign out.
- **Redirecting to a `/sign-in?returnUrl=…` page.** It isn't needed (sign in happens in place, on the same URL), and URLs must hold IDs only (§4.0).
- **Unmounting the route on sign-out**, which loses unsaved text in forms without autosave. Hide it instead.
- **Resuming the previous person's unsaved text after a *different* person signs in** on a shared laptop.
- **Replaying Submit, Save changes, Discard or Mark reviewed** automatically after signing in again.
- **Closing the dialog before the ping succeeds** and then failing silently. The person would be signed out 2 minutes later with no warning.
- **`disabled` on the button while the ping is in flight.** Focus falls out of the dialog.
- **A toast or banner instead of a modal.** It is easily missed and slower for assistive-technology users (Home Office research).
- **`{open && <dialog>}`**, a `position: fixed` div, or a hand-made focus trap.
- **`window.confirm()`** (SCR16's example). It is enough for WCAG, but it cannot update the countdown, cannot show the `401` or connection outcomes, and blocks the page's scripts.
- **A close "X", a "Sign out" link, or extra explanation** in the dialog. None of them is in the design.
- **Telling people in advance** ("You'll be signed out after 30 minutes") or **showing a session timer** in the header. People read this as a deadline for finishing the note (DWP, Home Office research).
- **Showing the warning for the 12-hour limit**, which Stay signed in cannot extend.
- **Stacking the warning on top of the Submit confirmation** and returning focus to "Submit note for Jane Citizen".

---

## Tensions with decisions

- **The 12-hour absolute limit (A24) has no warning and cannot be extended.** Under the Understanding document for SC 2.2.1, a security time limit is still a time limit. Only things like time-limited two-factor codes are named as possibly "essential", and the exemption applies only to limits over 20 hours. So a strict reading treats an unwarned 12-hour sign-out as a 2.2.1 issue. In practice, the design means nothing is lost: drafts are on the server and the person returns to the same note (SC 2.2.5). The limit can only be reached by someone continuously active for 12 hours. Recorded for awareness, not as a request to change.
- **The warning text does not say the work is saved.** Home Office guidance says to tell users whether their progress will be saved, and its research found users hoped their input would be kept. HMRC's dialog also says what happens to answers. The design's copy is the countdown sentence and the button only. "Drafts are already saved on the server" appears in §4.0 as a design fact, not as dialog text. It would not always be true either: when the save indicator says "Not saved: no connection", the latest change is not yet saved. Recorded only.
- **The setup page's 30-minute enrolment limit (§4.1, §8.1)** is also a time limit under SC 2.2.1 with no warning. It is outside this component (there is no session to extend) and belongs to the sign-in component. Recorded only.
- **A `5xx` that renewed nothing still counts as activity (A57).** *The session clock* step 1 counts every answer but
  a `401`, as the session check runs before the endpoint. Two answers renewed nothing, and the client cannot tell them
  from an endpoint's failure: a `503` from App Service's front end during a restart, which never reached the app, and
  a `500` from a database failure inside the session check less than 15 minutes after the request that last renewed
  the cookie (later, the cookie's own sliding renewal comes with the `500`). After either, the clock can be late by up
  to the time since that request, so the server can end the session before the warning that SC 2.2.1 asks for. The
  person's next request then gets the `401` that ends the clock and shows sign-in in place, with nothing lost, as at
  the 12-hour limit. Both come only while the app is failing, and not counting a `5xx` would make the clock early
  after any endpoint's failure and leave Stay signed in's `5xx` needing words of its own. Recorded for awareness; A57
  accepts it.

---

## Sources

- W3C, Understanding SC 2.2.1 Timing Adjustable: https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- W3C, Understanding SC 2.2.5 Re-authenticating: https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html
- W3C, Understanding SC 2.2.6 Timeouts: https://www.w3.org/WAI/WCAG22/Understanding/timeouts.html
- W3C, Technique SCR16: https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR16
- W3C, WAI-ARIA APG modal dialog pattern: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- W3C, WAI-ARIA APG alert dialog pattern: https://www.w3.org/WAI/ARIA/apg/patterns/alertdialog/
- W3C, WAI-ARIA 1.2, `timer` role: https://www.w3.org/TR/wai-aria-1.2/#timer
- WHATWG HTML, the dialog element: https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element
- MDN, `<dialog>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog
- MDN, HTMLDialogElement `cancel` event: https://developer.mozilla.org/en-US/docs/Web/API/HTMLDialogElement/cancel_event
- WICG, Close watcher explainer (user-activation rules): https://github.com/WICG/close-watcher
- Chromium issue 41484805, dialog `cancel` not dispatched on Escape without user interaction: https://issues.chromium.org/issues/41484805
- Chrome for Developers, Timer throttling in Chrome 88: https://developer.chrome.com/blog/timer-throttling-in-chrome-88
- Chrome for Developers, Page Lifecycle API: https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- Home Office Design System, Manage a service timing out (pattern and research): https://design.homeoffice.gov.uk/design-system/patterns/help-users-to/manage-service-timing-out
- HMRC Design Patterns, Service timeout: https://design.tax.service.gov.uk/hmrc-design-patterns/service-timeout/
- HMRC, hmrc-frontend timeout dialog source: https://raw.githubusercontent.com/hmrc/hmrc-frontend/main/src/components/timeout-dialog/timeout-dialog.js
- DWP Design System, Manage a session timeout: https://design-system.dwp.gov.uk/patterns/manage-a-session-timeout
- DWP Design System, Manage a session timeout, user research: https://design-system.dwp.gov.uk/patterns/manage-a-session-timeout/user-research
- DWP design-system discussion #449 (Zoom hover text report): https://github.com/dwp/design-system/discussions/449
- MoJ Design Patterns, Timeout warning: https://design-patterns.service.justice.gov.uk/components/timeout-warning
- GOV.UK Design System backlog #104, Timeout warning: https://github.com/alphagov/govuk-design-system-backlog/issues/104
- GOV.UK A to Z style guide (contractions, numbers, sign in): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- React, `useSyncExternalStore`: https://react.dev/reference/react/useSyncExternalStore
