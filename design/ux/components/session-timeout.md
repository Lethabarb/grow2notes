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
| **App shell, every signed-in screen** (§4.0): Today, read view and history, Flagged, Report, Manage | Mounted once in the signed-in layout, outside the routes, so it survives navigation. Workers and managers get the same component. | "Activity" means a request the server accepted: page loads, `GET /api/auth/me` on focus and on becoming visible (§6.8), saves, and manager actions. Reading a long note or history without loading anything for 28 minutes brings up the warning. This is correct: the design says idle means the person is idle (§8.5). |
| **Note form** (§4.3), including a past-day note, an earlier-day draft and "Editing submitted note" | Same component | Typing keeps the session alive, because autosave runs about 2 seconds after typing stops (§5.6). The warning can only appear after the person has stopped changing the note. A change still waiting for its autosave is sent at 28 minutes instead of showing the warning. If the save indicator already says "Not saved: no connection…", the warning can still appear, and Stay signed in can fail for the same reason. |
| **Submit confirmation** and **Discard draft** confirmation (§4.3) | Same component | Before the warning opens, an open confirmation is closed as if **Go back** or Cancel was chosen. Nothing is submitted or discarded. After the warning, the person taps Submit note again and sees the participant's name again. |
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

**The session clock** (a plain TypeScript module, not React, nothing stored on the device):

1. **Activity = a request the server accepted.** The shared `api()` fetch wrapper records the *send time* of every request whose response is not `401`. Using the send time means the client can only expire early, never late. Failed network requests do not count, because the server never saw them. Mouse movement, scrolling and taps do **not** count, and nothing pings in the background (§6.8, §8.5).
2. **Times are measured, not counted.** On every wake-up the clock works out `Date.now() − lastAccepted`. It sets one `setTimeout` for the next mark: 28:00, 29:00, 29:20, 29:40, 30:00.
3. **At 28:00**, if the note form has a change waiting for autosave, the clock asks autosave to send it now and waits. If the save is accepted, the clock resets and no warning appears ("a person typing is never timed out", §8.5). Otherwise the warning opens.
4. **While the page is hidden, the clock changes nothing.** Nobody can see a hidden tab's warning.
5. **When the page becomes visible**, the app already calls `GET /api/auth/me` (§6.8). The clock holds its current state until that call settles. `200` means the session is alive (perhaps kept alive by another tab): reset the clock, and close the warning or the signed-out view if either is showing. `401` means signed out. If the call fails for network reasons, fall back to the clock. Holding avoids the warning flashing up for a moment and then closing.
6. **Any non-`401` response at any time** resets the clock and closes the warning. The session really was extended. Focus goes back to where it was.
7. **At 30:00 on a visible page, or on any `401`**, the clock goes to *expired*. It never sends a request to "check" at 30:00, because a request would extend the session.

**The warning:**

1. When opening, first close any other open `<dialog>` with a plain `close()`. Every Grow2Notes confirmation treats a plain close as Go back or Cancel. Also close any open menu (the note form's "Discard draft" menu). Then call `showModal()`.
2. Focus lands on **Stay signed in**, the only focusable element.
3. **Stay signed in** → `POST /api/auth/ping` (with the `X-XSRF-TOKEN` header, §6.1). The dialog **stays open until the server answers**:
   - `204`: the clock resets, the dialog closes, and focus returns to the element focused before.
   - `401` (the session had already ended, or the 12-hour limit was reached): go to the signed-out state.
   - Network failure or a 10-second timeout: keep the dialog open, show the connection error, and leave the button ready. The countdown keeps running.
4. **Escape or the Android back gesture = Stay signed in.** Handle `cancel` with `preventDefault()` and run the same ping. Chromium may skip `cancel` and close the dialog straight away, so `close` is handled too. If the warning closes while still in a warning phase, send the ping. If that ping fails, reopen the warning with the connection error.
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
| **Focus** | Focus ring on the button: 3 px outline, 3 px offset, at least 3:1 against the dialog surface | Always visible (A32). Uses `outline`, never only `box-shadow`. |
| **Active** (pressed) | Shared primary pressed colour | No movement. |
| **Loading** (ping sent) | Up to 1 s: no change (repeat taps ignored). After 1 s: status line "Connecting…" | `aria-disabled="true"` on the button, **never `disabled`**, which would push focus out of the dialog. |
| **Error** (network) | Status line "No connection. Check your internet connection and try again." The button is ready again. | Error text in words (A32). Countdown continues. |
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
| Ping failed (network) | No connection. Check your internet connection and try again. | Proposed, **not in design.md** |
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
- The shared `api()` wrapper (also used by TanStack Query's `queryFn`s and mutations) is the only place that calls `requestAccepted(sentAt)` and `sessionEnded()`. Turn off TanStack retries for `401`.
- Keep the client limits as constants beside a comment pointing to §8.4 (`ExpireTimeSpan = 30 min`). For testing, shorten the server's idle timeout and the client constants together in the test environment only. Nothing here is a user setting.
- Unit-test the clock with fake timers and a fake system time: 28:00 opens, the countdown steps are right, 30:00 expires, a visible-page `/me` returning `200` clears an expiry, and a pending autosave holds the warning. The design's M2 acceptance test applies: "After an idle timeout and signing back in, the draft is intact" (§14).

```ts
// src/session/sessionClock.ts: no React, nothing stored on the device.
// Mirrors the server: 30 min idle (design.md §8.4), warning at 28 (A24).
const WARN = 28 * 60_000, END = 30 * 60_000;
export type Phase = 'active' | 'warn-2m' | 'warn-1m' | 'warn-40s' | 'warn-20s' | 'expired';

let lastAccepted = Date.now();  // send time of the newest request the server accepted
let expired = false, held = false, phase: Phase = 'active';
let timer: ReturnType<typeof setTimeout> | undefined;
const listeners = new Set<() => void>();

function compute(): Phase {
  if (held) return phase;                    // waiting for the server's answer
  if (expired) return 'expired';
  const left = END - (Date.now() - lastAccepted);
  if (left <= 0) { expired = true; return 'expired'; }
  if (left > END - WARN) return 'active';
  return left > 60_000 ? 'warn-2m' : left > 40_000 ? 'warn-1m' : left > 20_000 ? 'warn-40s' : 'warn-20s';
}

function update() {
  clearTimeout(timer);
  if (document.visibilityState === 'hidden') return;   // nobody can see it; /me decides on return
  const next = compute();
  if (next !== phase) { phase = next; listeners.forEach((l) => l()); }
  if (phase === 'expired' || held) return;
  const idle = Date.now() - lastAccepted;
  const mark = [WARN, END - 60_000, END - 40_000, END - 20_000, END].find((m) => m > idle)!;
  timer = setTimeout(update, mark - idle + 50);
}

export function requestAccepted(sentAt: number) { expired = false; lastAccepted = Math.max(lastAccepted, sentAt); update(); }
export function sessionEnded() { expired = true; update(); }
/** Hold the phase while a request that will decide it is in flight (the /me on becoming visible, a flushed autosave). */
export function holdWhile(p: Promise<unknown>) { held = true; void p.finally(() => { held = false; update(); }); }
export const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l); }; };
export const getPhase = () => phase;

/** Call once after sign-in. `checkSession` is the app's existing /me refetch (§6.8), e.g.
 *  () => queryClient.fetchQuery({ queryKey: ['me'], ... }), so the request is shared, not doubled. */
export function startClock(checkSession: () => Promise<unknown>) {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') holdWhile(checkSession()); else clearTimeout(timer);
  });
  update();
}
```

```tsx
// src/session/SessionTimeoutWarning.tsx: rendered once in the signed-in layout.
const LEFT = { 'warn-2m': '2 minutes', 'warn-1m': '1 minute', 'warn-40s': '40 seconds', 'warn-20s': '20 seconds' } as const;
const isWarning = (p: string): p is keyof typeof LEFT => p in LEFT;

export function SessionTimeoutWarning() {
  const phase = useSyncExternalStore(subscribe, getPhase);
  const ref = useRef<HTMLDialogElement>(null);
  const [ping, setPing] = useState<'ready' | 'busy' | 'slow' | 'failed'>('ready');
  const warning = isWarning(phase);

  useEffect(() => {
    const dialog = ref.current!;
    if (warning && !dialog.open) {
      // Every app dialog treats a plain close() as Go back / Cancel, so nothing is committed.
      document.querySelectorAll<HTMLDialogElement>('dialog[open]').forEach((d) => d.close());
      setPing('ready');
      dialog.showModal();                    // focuses the only button
    } else if (!warning && dialog.open) {
      dialog.close();                         // focus returns to where it was
    }
  }, [warning]);

  async function stay() {
    if (ping === 'busy' || ping === 'slow') return;
    setPing('busy');
    const slow = setTimeout(() => setPing('slow'), 1000);
    try {
      await api('/api/auth/ping', { method: 'POST' }); // api()'s own 10 s timeout (empty-loading-error.md)
      setPing('ready');                       // api() → requestAccepted() → phase 'active' → effect closes
    } catch (e) {
      if (!isUnauthorised(e)) {               // a 401 → api() → sessionEnded() → sign-in in place
        setPing('failed');
        if (!ref.current!.open) ref.current!.showModal();
      }
    } finally { clearTimeout(slow); }
  }

  return (
    <dialog
      ref={ref}
      className={s.dialog}
      aria-labelledby="session-timeout-heading"
      onCancel={(e) => { e.preventDefault(); void stay(); }}       // Escape / Android back
      onClose={() => { if (isWarning(getPhase())) void stay(); }}  // Chromium may skip cancel
    >
      <h2 id="session-timeout-heading" className={s.heading} aria-live="polite" aria-atomic="true">
        You'll be signed out in {isWarning(phase) ? LEFT[phase] : LEFT['warn-2m']}
      </h2>
      <p role="status" className={s.status}>
        {ping === 'slow' ? 'Connecting…' : ping === 'failed' ? 'No connection. Check your internet connection and try again.' : ''}
      </p>
      <button type="button" className={s.button} onClick={stay}
        aria-disabled={ping === 'busy' || ping === 'slow' || undefined}>
        Stay signed in
      </button>
    </dialog>
  );
}
```

```tsx
// In the signed-in layout: the route stays mounted (unsaved text survives) but hidden.
const expired = useSyncExternalStore(subscribe, getPhase) === 'expired';
return (
  <>
    {expired && <SignedOutInPlace />}  {/* §4.1 sign-in + heading; focuses heading; same/different-user check */}
    <div hidden={expired}><AppShell><Outlet /></AppShell></div>
    <SessionTimeoutWarning />
  </>
);
```

```css
/* SessionTimeoutWarning.module.css */
.dialog {
  inline-size: min(100% - 2rem, 28rem);
  max-block-size: calc(100dvh - 2rem);
  overflow-y: auto;
  padding: 1.5rem;
  border: 1px solid var(--color-border-strong);  /* ≥3:1 boundary (SC 1.4.11) */
  border-radius: var(--radius-m);
  background: var(--color-surface);
  color: var(--color-text);
}
/* No open animation (foundations.md: no motion; app-shell.md Conflicts #17). The earlier fade-in is struck. */
.dialog::backdrop { background: rgb(0 0 0 / 0.55); }
.heading { margin: 0; font-size: var(--font-size-h2); line-height: 1.3; }
.status { margin: 0; }                                /* never display:none: it is a live region */
.status:not(:empty) { margin-block-start: 0.75rem; }
.button {
  margin-block-start: 1.5rem;
  inline-size: 100%;
  min-block-size: 3rem;                               /* 48 px: above A32's 44 px */
  font: inherit;
  font-weight: 600;
  /* colours from the shared primary button tokens */
}
.button:focus-visible { outline: 3px solid var(--color-focus); outline-offset: 3px; }
.button[aria-disabled='true'] { cursor: progress; }
@media (hover: hover) { .button:hover { background: var(--color-primary-hover); } }
.button:active { background: var(--color-primary-active); }
@media (forced-colors: active) { .dialog { border: 2px solid CanvasText; } }
```

---

## Per-screen notes

**App shell (§4.0), all signed-in screens**
- Mount it once, inside the signed-in layout and outside any element that is made `hidden` or `inert`.
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

**Submit confirmation (§4.3)**
- If the warning is due while the confirmation is open, close the confirmation first (= Go back), then open the warning. Focus then returns to **Submit note** on the form, not to "Submit note for Jane Citizen" inside the confirmation. This avoids a quick double tap or a second Enter going from Stay signed in straight onto the commit button ([Opinion]: the two buttons sit in the same place on a phone).
- The person taps Submit note again and sees the participant's name again. That matches the design's wrong-participant safeguard (§3.9).
- If Submit itself gets a `401`, sign-in appears in place, and Submit is **not** retried automatically. The person submits again after signing in.
- The **Discard draft** confirmation is handled the same way (closed as Cancel).

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
