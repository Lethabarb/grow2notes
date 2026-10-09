# Autosave, save status and conflicts

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. The note form's top bar now sits in the shell's before-main bar slot (app-shell.md 3a); the form mounts its own announcer pair, and its polite region doubles as the page status region (note-form.md).

Component key: `autosave-status`. This covers the autosave engine behind the note form, the save indicator in the form's top bar, the "Not saved" banner, the banners that stop autosave (changed on another device or tab, someone else started the note, after midnight), the "lists were just changed" notice, the version conflict on **Save changes**, and how **Submit note** waits for a save.

Sources of truth: design.md §3.3, §3.4, §3.5, §4.0, §4.2, §4.3, §5.6, §5.7, §5.8, §6.3, §6.8, §6.9, §8.5, §9.6, §9.8, §9.9 and A3, A8, A10, A24, A31, A33. Decisions D17, D19, D22, D35 apply.

Sibling component docs this one hands off to:
- `confirm-dialog.md` owns the submit confirmation (busy state, error copy, focus).
- `form-validation.md` owns field errors and the error summary, including the length-limit state that pauses autosave.
- `session-timeout.md` owns the 28-minute warning and signing in again in place. It calls this component's `flushNow()`, `pause()` and `resume()`.

Grades: **[Research]** studies or usability testing, **[Standard]** a spec (WCAG, WAI-ARIA, HTML, Fetch), **[Convention]** established design systems and platform guidance, **[Opinion]** reasoned judgement. "(V)" means copy taken word for word from design.md. "(P)" means proposed copy. Every (P) string here on 9 October 2026 was approved as written (D67); a (P) string added later still needs the owner's approval.

---

## Where it's used

| Screen / state (design.md) | What this component does | What differs |
|---|---|---|
| **Note form, new note** (4.3 "New"; worker today, manager today) | The indicator is blank until the first change. The first `PUT …/draft` creates the draft (A8). | Only the first save sends `listsVersion` and can get `409 note.lists_changed`, `409 note.in_progress_by_other`, the after-midnight `422 note.past_date_manager_only` or `422 note.participant_archived`. |
| **Note form, reopened own draft** (4.3 "Draft", "Draft from an earlier day"; reached from Today or "Your unfinished drafts", 4.2) | Shows "Draft · saved 9:42 am" until the next change. Then the normal cycle runs. | For a draft from an earlier day, the saved time also shows its date. The earlier-day banner belongs to the form header, not to this component. |
| **Note form after signing in again** (4.0 Sessions, 8.5) | Unsaved text stays in memory. Autosave is paused while signed out, then retries with the same `clientId` and the next `seq`. | No new UI. `session-timeout.md` drives it. |
| **Note form, manager's past-day note** (3.8, 4.3) | Same as a new note. | The past-day banner belongs to the header. The note date is the past date. |
| **Note form, editing a submitted note** (3.5, 4.3 "Editing submitted note (version 2)") | Autosaves the editor's own pending edit. **Save changes** waits for the save, just as Submit does. | The indicator never says "Draft". `409 note.version_conflict` can come back on Save changes. **Cancel** must stop autosave before it sends `DELETE …/draft`. |
| **Submit confirmation** (4.3) | Opens only after the latest change is saved **and** a check save has confirmed that this page still owns the draft. | Nothing autosaves inside the dialog. Busy and error states are in `confirm-dialog.md`. |
| Today (4.2) | Not shown. Today displays the server's "Draft · You · saved 9:42 am". | After leaving the form, refresh the `today` and `me/drafts` queries so that line matches. |
| Read view; a manager's read-only view of someone else's draft (4.4, 4.2) | Not present. | There is nothing to save, so no indicator appears. |

---

## Best practice

### When to save

- **Save automatically while people work and when they switch away. Do not make them press Save.** Apple's HIG says to keep people confident their work is kept, to save periodically during editing, and to save again when they close the file or switch apps. [Convention] https://developer.apple.com/design/human-interface-guidelines/file-management
- **The page becoming hidden is the last moment you can rely on.** Chrome's Page Lifecycle guidance calls `visibilitychange` to hidden the last reliable time to save user data. Browsers may later freeze or discard the page without firing anything else, and `unload` is unreliable, especially on mobile. [Convention: platform vendor guidance] https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- **`fetch(…, { keepalive: true })` lets a request outlive the page, but its body is capped at 64 KiB.** [Standard: Fetch, via MDN] https://developer.mozilla.org/en-US/docs/Web/API/RequestInit
- **Focus leaving a field is a good extra trigger** alongside a timer. [Convention] https://ui-patterns.com/patterns/autosave
- **A debounce needs a maximum wait.** A pure "2 s after the last change" timer never fires while someone types or dictates without a 2-second pause. The M2 acceptance test says closing the browser mid-sentence loses "no more than the last few seconds of typing" (§14). A maximum wait is what guarantees that. [Opinion: the logic of debouncing. The value is house style: the `ui-build` corpus records 1 s / 5 s as convention, not evidence.]

### Showing status

- **Status should always be visible and given within a reasonable time** (NN/g heuristic 1). [Convention: heuristic, not a study] https://www.nngroup.com/articles/visibility-system-status/
- **Indicators are passive and belong next to what they describe. A notification that needs action must be prominent.** [Convention] https://www.nngroup.com/articles/indicators-validations-notifications/
- **Put autosave status inline, near the content, not in a toast.** Use "Saving…" while a save runs. If autosave fails, show an inline warning and **keep it visible until the changes are saved**. [Convention: GitLab Pajamas] https://design.gitlab.com/patterns/saving-and-feedback/
- **"Saving…" and "Saved" belong in the same place each time.** [Convention: Carbon inline loading] https://carbondesignsystem.com/components/inline-loading/usage/
- **When autosave is on, do not use "unsaved" warning styling for ordinary saving.** Apple drops the unsaved-changes dot when autosave is on, because the dot suggests the person has to act to avoid losing work. So "Saving…" should look neutral, not like a warning. [Convention] https://developer.apple.com/design/human-interface-guidelines/file-management
- **Tell people whether their data is saved. Use plain words, not "offline" jargon. Do not blame the user. Do not rely on colour alone.** Distinguish "your connection" from "our server" where you can. [Convention] https://web.dev/articles/offline-ux-design-guidelines

### Screen readers

- **A status message that does not take focus must be exposed through a role or property** (SC 4.1.3). "Saving…" and "Saved 9:42 am" fall under that definition, which covers the waiting state of an application and the result of an action. Leaving them as bare text fails F103. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html · https://www.w3.org/WAI/WCAG22/Techniques/failures/F103
- **Use `role="status"`, and have the container in the page before the message arrives.** [Standard: ARIA22] https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- **Polite is the default. Assertive is for urgent, time-sensitive messages only. Put live regions in the initial markup. Add a redundant `aria-live="polite"` to `role="status"`.** [Standard: MDN on WAI-ARIA] https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- **Alerts must not move focus. Frequent interruptions hurt people with visual and cognitive disabilities. Alerts must not disappear on their own.** [Standard: WAI-ARIA APG] https://www.w3.org/WAI/ARIA/apg/patterns/alert/
- **Live regions behave differently across screen readers.** Roselli's January 2026 tests found failures in polite regions (VoiceOver on macOS during read-all), `role="alert"` (Orca, Narrator) and dynamic `aria-describedby` changes (most readers). So the persistent visible text has to carry the meaning, and a live region only adds to it. [Research: practitioner cross-AT testing] https://adrianroselli.com/2026/01/live-region-support.html

### Connection loss and retry

- **`navigator.onLine` is a hint.** `true` does not mean the internet can be reached. Do not turn features off based on it. [Standard: MDN] https://developer.mozilla.org/en-US/docs/Web/API/Navigator/onLine
- **TanStack Query v5 assumes it is online and listens only for `online`/`offline` events.** In the default `networkMode: 'online'`, an `offline` event pauses retries until `online` fires. `scope.id` runs mutations one after another. [Convention: library docs] https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5 · https://tanstack.com/query/v5/docs/framework/react/guides/network-mode · https://tanstack.com/query/v5/docs/framework/react/guides/mutations
- **Offline is a lasting condition, so it gets a persistent banner, never a toast.** [Convention: GitLab above; web.dev above]

### Conflicts

- **Never silently overwrite. Keep the person's own text visible next to the other version.** MediaWiki's edit-conflict screen shows the other version, a diff and "your text" so nothing is lost. [Convention] https://en.wikipedia.org/wiki/Help:Edit_conflict
- **Changes to stored data should be reversible, checked or confirmed** (SC 3.3.4). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

### Never losing text

- **Making people re-enter data causes abandonment.** In Baymard's checkout testing, participants gave up when forced to retype data, and 34% of benchmarked sites did not keep card data after an error. [Research] https://baymard.com/research-articles/preserve-card-details-on-error
- **Keep the user's input after an error so they can correct it, not start again.** [Convention: NN/g guidelines] https://www.nngroup.com/articles/error-message-guidelines/
- **`beforeunload` cannot show custom text, needs prior user interaction, is unreliable on mobile, and stops Firefox using the back/forward cache. Add it only while there are unsaved changes and remove it straight after.** [Standard: HTML via MDN; Convention: Chrome guidance] https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- **React Router's `useBlocker` stops in-app navigation, but not reloads or cross-origin navigation, and it needs a data router.** [Convention: library docs] https://reactrouter.com/api/hooks/useBlocker

### A Submit that waits for a save

- **Avoid disabled buttons where possible.** They have poor contrast and can confuse people. [Convention: GOV.UK] https://design-system.service.gov.uk/components/button/
- **`aria-disabled="true"` keeps a button focusable and findable.** MDN gives a temporarily unavailable submit button as its example. The page must block activation in script. [Standard: MDN] https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-disabled
- **A disabled control with no reason leaves people guessing what is missing.** [Opinion: practitioner article] https://www.smashingmagazine.com/2021/08/frustrating-design-patterns-disabled-buttons/
- **People double-click submit buttons.** GOV.UK Notify found duplicate invitations caused by double-clicked send buttons. [Research: GOV.UK team finding, reported in the Button guidance above]

### Banners and sticky bars

- **One notification banner at a time. Put it at the top, before the `h1`. Not for validation errors. People often miss banners.** [Convention, with GOV.UK noting banner-blindness evidence] https://design-system.service.gov.uk/components/notification-banner/
- **A section alert goes right above the section it is about. Never hide it automatically. Error alerts are not dismissible until resolved.** [Convention: Australian Government Design System] https://design-system.agriculture.gov.au/components/section-alert · https://design-system.agriculture.gov.au/components/page-alert
- **A sticky header must not hide the focused element.** Use `scroll-padding` (technique C43). Failure F110 is a sticky header that fully covers focus. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html

---

## Recommendation for Grow2Notes

### Anatomy

```
Phone, normal                         Phone, Not saved (connection)        Phone, changed on another device
+---------------------------------+   +---------------------------------+  +---------------------------------+
| < Today          Saved 9:42 am  |   | < Today                         |  | < Today              Not saved  |
+---------------------------------+   | (!) Not saved: no connection.   |  | (!) This note was changed on    |
| Jane Citizen                    |   |     Keep this page open;        |  |     another device or tab.      |
| Thursday 1 October 2026         |   |     retrying.                   |  | [ Keep the text on this screen ]|
| ...                             |   +---------------------------------+  | [ Load the other version       ]|
                                      | Jane Citizen ...                |  +---------------------------------+
  ^ sticky top bar (one row)            ^ the same status element spans      ^ blocking banner inside the
                                          a second row of the sticky bar       sticky bar; status says "Not saved"
```

Parts:
1. **Sticky top bar**: `< Today` link, then the **save indicator**, one `<p role="status">` that is always in the DOM. In the failed state the same element spans a second, full-width row and is styled as a warning banner. That is the design's "banner" (§3.4) and its indicator string (4.3) in one element, so the text is never duplicated or announced twice.
2. **Blocking banner** (inside the sticky bar, under row 1), only one at a time: the takeover banner (two buttons), "someone else started this note" or the after-midnight message.
3. **Lists-changed notice**: in the page flow, directly above "1. Goals".
4. **Version-conflict notice** (editing a submitted note only): in the page flow at the top of the form, under the participant header.
5. **Submit note / Save changes**: `aria-disabled` while the latest change is not saved, with `aria-describedby="save-status"`.
6. **App-level announcer**: two visually hidden regions, `role="status"` and `role="alert"`, mounted once at app start. These are the same regions `form-validation.md` uses.

### Behaviour

**1. When a save is sent** (design §3.4, §5.6)
- Every change to a tick box, the Guided notes box, the flag or the reason counts as one change. A save is due **2 s after the last change** (V). There is a **5 s maximum wait** from the first unsaved change, so continuous typing or dictation still saves every 5 s. [Opinion]
- **On blur** of any field, the save is sent now (V).
- **When the page becomes hidden**, the latest state is sent now, using `keepalive` when the body is under 60,000 bytes measured with `TextEncoder` (V, §5.6). This save does not wait for one already in flight, because the higher `seq` wins on the server.
- **When the page becomes visible again** and a change is still unsaved, the save is sent now. [Opinion]
- **Nothing is sent** while a field is over its length limit (`form-validation.md`), while autosave is paused for sign-in (`session-timeout.md`), or while a blocking banner is showing.

**2. One save at a time, always the newest content** (V, §5.6)
- Only one regular save is in flight. When it settles, if changes arrived meanwhile, the next save goes at once with the newest state. Saves are combined, never queued one by one.
- The request body is read **when the request is sent**, including on every retry, so a retry never sends old text.
- **Acknowledgement is by change number.** Each change increases a counter. A save records the counter value it carried. The latest change counts as saved only when the server has acknowledged a save whose counter value is at least the newest one. "Saved" never appears before the server confirms (I5, no optimistic success).

**3. Identity of this page's saves** (V, §5.6, with one refinement)
- Keep `{ clientId, seq }` **per note, per page load, in a module-level `Map` in memory**. Create it at this page's first save of that note, with `seq` starting at 1.
  - Not per component mount. If `seq` started again at 1 when someone left the form and came back in the same page load, the server would ignore those saves as stale and still return `200`, so the page would say "Saved" while nothing was stored.
  - Not one `clientId` for the whole page. "Keep the text on this screen" replaces the `clientId` for one note, which must not break the saves of another note.
- **Keep the text on this screen** and **Load the other version** both delete that note's entry, so the next save takes over with a new `clientId` and `seq = 1` (V).
- A full reload starts a new map. That only happens on Sign out (`session-timeout.md`).

**4. Retry** (V: "the page keeps retrying")
- Retry indefinitely while the form is open for **network errors, timeouts (10 s per request, the `api()` wrapper's own, `empty-loading-error.md` *Timing*; the mutation passes no `AbortSignal.timeout`, whose abort the wrapper would rethrow as it came, not as a timeout), `408`, `429` (honour `Retry-After`), `500`, `502`, `503` and `504`**. Back off 1 s, 2 s, 4 s, 8 s, then every 10 s. Set `networkMode: 'online'` on autosave's own mutation, as the app's query client makes `'always'` the default for every other one (`empty-loading-error.md` *Timing*), so an `offline` event pauses retries and `online` restarts them. When the page becomes visible, a save is sent at once. [Opinion on the numbers. 300 requests a minute per user, §9.9, is never approached.]
- **Show "Not saved" only after the first retry has also failed** (about 1 s), or at once if the mutation is paused because the browser reported offline. A single dropped packet on a moving train should not flash a warning. Until then the indicator truthfully says "Saving…". [Opinion]
- Never retry `401`, `403`, `409`, `412` or `422`. Each one has its own handling below.

**5. Responses that stop autosave**

| Response | What happens | Copy |
|---|---|---|
| `401` | Pause autosave. Keep everything in memory. `session-timeout.md` shows sign-in in place, then resumes, which retries with the same `clientId` and the next `seq`. | (owned by session-timeout) |
| `409 draft.taken_over` | Pause autosave. Show the **takeover banner** in the sticky bar. The indicator shows "Not saved". Typing keeps working and is not lost. The next choice sends or replaces it. | (V) "This note was changed on another device or tab." · **Keep the text on this screen** · **Load the other version** |
| `409 note.lists_changed` (first save only) | `GET …/draft` for the fresh template. Re-apply picks by group ID and ticks by item ID, keeping ticks only for Every note and picked groups (a tick on an item now in an unpicked group is dropped; design.md 5.7). Keep the text, flag and reason. Show the **lists-changed notice** above Goals. Save again with the new `listsVersion`. The indicator stays "Saving…" throughout. | (V) "The goal or common-item list was just changed. Please check your ticks." |
| `409 note.in_progress_by_other` (first save only) | Stop autosave. Keep the text on screen, still selectable so it can be copied. Show a blocking banner. Submit stays `aria-disabled`. | (V, from 4.2) "Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex can finish it." |
| `422 note.past_date_manager_only` after midnight (first save only) | As above. | (V, §3.3) "It's now after midnight, so this note can't be started for yesterday. Ask a manager to record it." |
| `422 note.participant_archived` (first save only) | As above. | **No copy in design.md** (open question). |
| `422 validation.failed` (over a length limit) | `form-validation.md` stops this happening by not sending. If it happens anyway, show that doc's field error. | (P, `form-validation.md`) "Not saved: fix the error below." |

**6. Submit note and Save changes** (V: "Submit is disabled while the latest change is not saved")
- The button has `aria-disabled="true"` while the indicator is **Saving…**, **Not saved…** or blocked, and `aria-describedby="save-status"` at all times. It stays in the tab order and keeps focus.
- On tap or Enter:
  1. Run the form's checks first (`form-validation.md`). Errors show at once and nothing waits.
  2. If the indicator says **Not saved** or a blocking banner is showing: do nothing more. The sticky bar already explains why, and the button's description repeats it.
  3. Otherwise (**Saving…**, **Saved**, or a just-reopened draft) run a **check save**: one `PUT` now, even if nothing has changed. It flushes any change still waiting **and** detects whether another tab or device took over since this page last saved (a `409 draft.taken_over`). Open the confirmation (Submit) or send `POST …/versions` (Save changes) only when that save succeeds. While it runs, the indicator says "Saving…". Ignore repeat taps.
  4. If the check save fails or is blocked, nothing opens. The indicator or banner explains, and the next tap tries again. A tap is never remembered and acted on later, so the dialog never pops up unexpectedly half a minute afterwards.
- Why the check save: `POST …/versions` carries no `clientId` (§6.3). Without the check, a phone tab that had sat untouched could submit older text over a newer draft typed on a laptop, and the laptop's working copy would be deleted (§5.8 step 7). [Opinion]
- **Before sending `POST …/versions`, `POST …/discard` or `DELETE …/draft` (Cancel): stop autosave.** Clear the timer, stop page-hidden saves, and wait for the save in flight to settle. Otherwise a late autosave can land after the action and recreate a working copy. If the person backs out of a Discard or Cancel confirmation, resume. After a successful Submit or Save changes, the form unmounts with autosave stopped.

**7. Leaving the form**
- While a change is unsaved (**Saving…** or **Not saved…**) or the takeover banner is showing, add a `beforeunload` listener. Remove it as soon as the change is saved. The browser shows its own generic prompt. It works on laptops but cannot be relied on on phones, which is why the page-hidden save exists. [Standard + Convention, MDN/Chrome above] The design already accepts this kind of warning: the guide-prompts screen says "Leaving with unsaved changes shows a warning" (4.10).
- **In-app navigation** (`< Today`, browser Back within the app, a manager's nav links): use `useBlocker`. When it blocks, send the save now:
  - If it succeeds, `proceed()` happens automatically. The person sees only "Saving…" for a moment.
  - If it fails, `reset()` keeps them on the form. The sticky bar already shows "Not saved: no connection. Keep this page open; retrying." Announce that text politely, because a tapped link that did nothing would otherwise be silent.
  - While the takeover banner is showing, `reset()` immediately, because the two buttons are the way out.
  - No new dialog. [Opinion]
- After the person is blocked by **someone else started this note**, **after midnight** or **participant archived**, do **not** block navigation. The text can never be saved, and blocking would trap them. `beforeunload` still warns on close.

**8. The form never reloads itself over the person's text**
- The `GET …/draft` query that fills the form uses `staleTime: Infinity`, `refetchOnWindowFocus: false` and `refetchOnReconnect: false`. The form copies the data into its own state **once**. §6.8's refetch on focus is for Today and the badge, not this query. A background refetch replacing on-screen text would break "the text on screen is never replaced without asking" (4.3). [Opinion, enforcing a V rule]
- Render the form, including the indicator, only after that query resolves. The indicator's first text ("Draft · saved 9:42 am") is then present at mount and is not announced as a change.

**9. Conflict choices** (V)
- **Keep the text on this screen**: start a new `clientId` and send everything on screen now, including anything typed while the banner was up and the on-screen group picks (`pickedGroupIds`). Remove the banner. Put focus back on the element that had it when the conflict appeared, or the participant name heading if that element has gone. They were usually typing, so the keyboard reopening is expected.
- **Load the other version**: `GET …/draft` and replace the picks, ticks, text, flag and reason, start a new `clientId`, and remove the banner. The indicator shows "Saved [the other version's time]". Focus goes to the participant name heading (`h1`, `tabindex="-1"`) so they review from the top, without the phone keyboard popping up.
- "Keep" is the first button and uses primary styling, because it is the choice that cannot lose anything on this screen. "Load" uses secondary styling.

**10. Version conflict on Save changes** (V §3.5, §6.3; copy partly P)
- This is the result of the editor's own tap, so focus moves to the notice (`section`, `tabindex="-1"`), which is scrolled into view.
- Contents:
  - one line naming who saved what and when, from `{currentVersion, createdBy, createdAtUtc}`;
  - the newer version shown read-only, reusing the 4.4 read-view rendering, inside `<details open>` ("you are shown the newer version", V);
  - the button **Start again from the latest version** (V).
- That button sends the `PUT` with the new `baseVersion`. The editor's text, ticks and flag stay on screen (V). The heading becomes "Editing submitted note (version 3)". The `<details>` closes but stays available for reference until Save changes succeeds. Focus goes to the form heading.

### States

| State | Indicator text (`#save-status`) | Banner / notice | Submit / Save changes | Announced | Focus |
|---|---|---|---|---|---|
| **Empty**: new note, nothing changed | *(empty; the element stays in the DOM)* | none | Active. A tap runs the checks, so empty Guided notes shows its error (4.3). | nothing | unchanged |
| **Loaded**: reopened draft, no change yet | (V) "Draft · saved 9:42 am". Earlier day (P): "Draft · saved Wed 30 Sep, 9:42 am". Pending edit: "Saved 9:42 am". | Earlier-day banner (header, V) | Active (the tap still runs the check save) | nothing (present at mount) | unchanged |
| **Loading** (saving) | (V) "Saving…", from the first unsaved change until it is acknowledged | none | `aria-disabled` | polite, through the indicator | unchanged |
| **Saved** | (V) "Saved 9:42 am" (server time, Melbourne) | none | Active | polite, through the indicator | unchanged |
| **Error**: connection or server, after one failed retry | (V) "Not saved: no connection. Keep this page open; retrying." with a warning icon, full-width row in the sticky bar | the indicator *is* the banner | `aria-disabled` | polite, through the indicator, once per failure episode. The text does not change between retries, so nothing repeats. | unchanged |
| **Error → recovered** | (V) "Saved 9:44 am" | gone | Active | polite | unchanged |
| **Blocked: taken over** | (P) "Not saved" (the opening words of the V string) | (V) takeover banner with two buttons | `aria-disabled` | **assertive** (app announcer): the banner sentence. Saving has stopped and on-screen text is at risk. | unchanged (background event) |
| **Blocked: started by someone else / after midnight / archived** | "Not saved" | banner with the V sentence (archived: open question) | `aria-disabled` | assertive, the banner sentence | unchanged |
| **Error: over a length limit** (`form-validation.md`) | (P) "Not saved: fix the error below." | field error | `aria-disabled`; checks run on tap | the field's message, once (`form-validation.md`) | unchanged |
| **Lists changed** | "Saving…" then "Saved …" | (V) notice above Goals, persistent while on the form, not dismissible | as for saving/saved | polite (app announcer): the notice sentence | unchanged. If the focused tick box was removed, focus moves to the notice (`tabindex="-1"`). |
| **Version conflict** (Save changes) | "Not saved" until Start again succeeds | notice at the top of the form (P line + V button) | Save changes `aria-disabled` until Start again succeeds | via focus (heading read on arrival) | moves to the notice (user-caused) |
| **Paused for sign-in** | unchanged (hidden behind sign-in) | — | — | — | `session-timeout.md` |
| **Read-only** (read view, manager viewing a draft) | not rendered | — | — | — | — |
| **Hover / active** | The indicator is not interactive. Banner buttons use the shared button styles: hover only under `@media (hover: hover)`, plus a pressed style. | | | | |
| **Focus** | The banner buttons and `< Today` show a 3 px `outline` with offset, at least 3:1 contrast, never `box-shadow` alone. | | | | |

### Exact copy

| Where | Copy | Source |
|---|---|---|
| Indicator, saving | Saving… | (V) 4.3 |
| Indicator, saved | Saved 9:42 am | (V) 4.3 |
| Indicator, reopened draft | Draft · saved 9:42 am | (V) 4.3 "Draft" state |
| Indicator, saved on an earlier Melbourne day | Saved Wed 30 Sep, 9:42 am · Draft · saved Wed 30 Sep, 9:42 am | (P) uses the 4.3 date style "Wed 30 Sep" |
| Indicator and banner, connection or server failure | Not saved: no connection. Keep this page open; retrying. | (V) 4.3 |
| Indicator while a blocking banner shows | Not saved | (P) the opening words of the V string |
| Takeover banner | This note was changed on another device or tab. | (V) 4.3 |
| Takeover buttons | Keep the text on this screen · Load the other version | (V) 4.3 |
| Lists changed | The goal or common-item list was just changed. Please check your ticks. | (V) 4.3 |
| Someone else started it | Alex P. started today's note for Jane Citizen at 9:14 am. Only Alex can finish it. | (V) 4.2 |
| After midnight | It's now after midnight, so this note can't be started for yesterday. Ask a manager to record it. | (V) 3.3 |
| Version conflict line | Sam Lee saved version 3 at 5:03 pm while you were editing. Your changes are still below. | (P) built from `{createdBy, currentVersion, createdAtUtc}` |
| Version conflict button | Start again from the latest version | (V) §6.3 |
| Over a length limit | Not saved: fix the error below. | (P) `form-validation.md` |
| Submit busy and failure | see `confirm-dialog.md` ("Submitting…", "The note was not submitted. Check your connection, then try again. Your draft is saved.") | (P) owned there |

Formatting:
- Times come from the server's `savedAtUtc`, never the device clock. They are formatted for `Australia/Melbourne` (A33).
- Whether to add the date is decided against the server's Melbourne "today" already loaded for the app, not the device date.
- `Intl.DateTimeFormat('en-AU')` produces "9:42 am" in Node 24's ICU. Its short date form is "Wed, 30 Sept" and its numeric day is zero-padded ("01"). So build "Wed 30 Sep" from `formatToParts`, with `Number(day)` and a fixed three-letter month table (verified locally; check Safari in Playwright WebKit).
- The middle dot in "Draft · saved" is decorative. Render it as `<span aria-hidden="true"> · </span>` so screen readers do not say "dot".

### Phone vs laptop

- **Phone (≤ 40rem):**
  - The top bar is one row: `< Today` on the left, the indicator right-aligned.
  - In the failed state the indicator wraps to a full-width second row.
  - Takeover buttons are stacked, full width, at least 44 px high.
  - The lists-changed and version-conflict notices are full width.
- **Laptop:**
  - The same structure inside the form's content column.
  - Takeover buttons sit side by side and wrap when the text is large.
  - The indicator stays in the top bar, not in a corner of the window, so it stays near the content (GitLab, NN/g above).
- **Short viewports** (`@media (max-height: 30rem)`: landscape phones, and laptops zoomed to 200% or more): the top bar is **not** sticky, so it cannot cover half the screen (SC 1.4.10).
- Everywhere: the indicator text is at least 1rem. It wraps, never truncates or uses ellipsis. There is no spinner and no animation: "Saving…" appears every few seconds, and movement in the corner of the eye would pull attention away from writing. [Opinion] So no reduced-motion branch is needed.

### Accessibility

**Semantics**
- Indicator:

  ```html
  <p id="save-status" role="status" aria-live="polite" aria-atomic="true">
  ```

  It is rendered for the whole life of the form, empty when there is nothing to say. The warning icon is an inline SVG with `aria-hidden="true"`. The words carry the meaning (SC 1.4.1).
- Takeover and blocking banners: `<section aria-labelledby="…">` with the sentence as its label. The section is a named region, so screen-reader users can jump to it by landmark. Its buttons are native `<button type="button">`. The banner itself is **not** a live region, because it holds controls. Its sentence is announced once through the app's assertive announcer.
- Lists-changed notice: `<div>` with a `<p>`. It is announced once through the app's polite announcer.
- Version-conflict notice: `<section tabindex="-1" aria-labelledby>` with an `h2`, the read-only version inside `<details open><summary>`, and a native button.
- Submit and Save changes: `aria-disabled="true"` while unsaved, never `disabled`. `aria-describedby="save-status"`. Guard the handler, because `aria-disabled` stops neither clicks nor Enter-to-submit (`form-validation.md`).

**ARIA only where needed.** `role="status"` is required (SC 4.1.3). `aria-disabled` is required to honour the design's "disabled" without losing focus. Nothing else is needed: the banners are plain HTML.

**Keyboard**
- Tab order: `< Today` → banner buttons (when present) → form fields → Submit.
- Enter and Space activate the banner buttons. There are no shortcuts.
- Escape does nothing on banners. They are resolved, not dismissed.
- The sticky bar sets `scroll-padding-block-start` to its measured height, so a focused field is never hidden under it (SC 2.4.11, technique C43).

**What a screen reader hears** (wording varies by reader)
- While typing: "Saving…" once at the start of a burst. This is often cut off by key echo, which is harmless. Then "Saved 9:42 am" during the pause.
- On failure: "Not saved: no connection. Keep this page open; retrying." once.
- On recovery: "Saved 9:44 am".
- Takeover: "This note was changed on another device or tab." straight away (assertive).
- Focus on Submit while unsaved: "Submit note for Jane Citizen, dimmed (or unavailable), button, Saving…".
- These announcements are extra. The visible text in the sticky bar is the reliable channel (Roselli 2026).

**WCAG 2.2 criteria met**

| Criterion | How it is met |
|---|---|
| 1.3.1 Info and Relationships | Submit is described by the status |
| 1.4.1 Use of Color | words plus icon |
| 1.4.3 Contrast (Minimum) | text at least 4.5:1, including the neutral "Saved" grey |
| 1.4.4 Resize Text and 1.4.10 Reflow | wraps; not sticky on short viewports |
| 1.4.11 Non-text Contrast | banner border and icon at least 3:1 |
| 1.4.12 Text Spacing | no fixed heights |
| 2.1.1 Keyboard | native buttons |
| 2.2.1 Timing Adjustable | nothing auto-dismisses; retries have no deadline |
| 2.4.3 Focus Order | |
| 2.4.7 Focus Visible | |
| 2.4.11 Focus Not Obscured (Minimum) | scroll-padding |
| 2.5.8 Target Size (Minimum) | 44 px, beyond the 24 px minimum |
| 3.2.2 On Input | autosave and a list reload never change context or move focus |
| 3.3.1 Error Identification | |
| 3.3.4 Error Prevention | Submit is checked and confirmed; edits are versioned |
| 3.3.7 Redundant Entry | text is never cleared, so it never has to be typed again |
| 4.1.2 Name, Role, Value | `aria-disabled` is exposed |
| 4.1.3 Status Messages | ARIA22 |

### Implementation notes (React 19 + native HTML + CSS Modules)

- **No React Aria needed.** Native buttons, `<details>`, `<section>` and a `role="status"` paragraph cover everything.
- The engine is a hook, `useAutosave`, that uses TanStack Query's `useMutation` for transport, retry and back-off (§7.5), plus refs for the change counters.
- Its public API: `markChanged()`, `flush()`, `checkSave(): Promise<boolean>`, `flushNow(): Promise<boolean>` (used by `session-timeout.md` at 28:00 and by the navigation blocker), `pause()` and `resume()` (sign-in), `stop(): Promise<void>` and `resume()` (Submit, Discard, Cancel), and `status`, `savedAt` and `blocked`.
- **Shared fetch wrapper:** reuse the app's `api()` wrapper. It adds `X-XSRF-TOKEN` (§9.8) and records accepted requests for the session clock (`session-timeout.md`).
- **Router:** `useBlocker` needs a data router (`createBrowserRouter`). Confirm the app uses one.
- **Tests:**
  - Vitest with fake timers: the 2 s debounce, the 5 s maximum wait, combining saves, acknowledgement by change number, never sending while blocked or paused.
  - Playwright `context.setOffline(true/false)`: the indicator text, Submit's `aria-disabled`, recovery.
  - Two pages on one note: the takeover banner appears, and both choices keep or replace text as labelled.
  - Lost response (route the request through, then abort the response): the retry shows "Saved" and raises no conflict (the M2 acceptance criterion).
  - axe on each banner state.
  - Assert nothing appears in Web Storage (M2).

```ts
// saveIds.ts: memory only (D22). One entry per note per page load; never per component mount.
const ids = new Map<string, { clientId: string; seq: number }>();
export function nextSaveIds(noteKey: string) {
  let e = ids.get(noteKey);
  if (!e) ids.set(noteKey, (e = { clientId: crypto.randomUUID(), seq: 0 }));
  e.seq += 1;                                   // first save of this note in this page load is seq 1
  return { clientId: e.clientId, seq: e.seq };
}
export const forgetSaveIds = (noteKey: string) => ids.delete(noteKey); // Keep / Load: take over with seq 1
```

```tsx
// useAutosave.ts (sketch: error mapping, flushNow/checkSave promises and pause/stop omitted for brevity)
const DEBOUNCE_MS = 2_000;   // design.md 5.6 (V)
const MAX_WAIT_MS = 5_000;   // [Opinion] continuous typing or dictation still saves
// No timeout of its own: api()'s 10 s one makes a hung request a retry (empty-loading-error.md *Timing*).

export function useAutosave(o: {
  noteKey: string; url: string; baseVersion: number;
  getContent: () => DraftContent;            // reads the form's latest state from a ref
  initialSavedAt: string | null;             // from GET {base}/draft
  onBlockingError: (e: ApiError) => Promise<Blocked>; // lists_changed reload, 401 sign-in, 409/422 banners
}) {
  const v = useRef({ local: 0, saved: 0, firstUnsavedAt: 0, inFlight: false, halted: false });
  const timer = useRef<number>();
  const [savedAt, setSavedAt] = useState(o.initialSavedAt);
  const [blocked, setBlocked] = useState<Blocked>(null);
  const [, rerender] = useReducer((n: number) => n + 1, 0);

  const save = useMutation({
    mutationFn: async () => {
      const version = v.current.local;                       // read at send time, so a retry
      const body = { ...o.getContent(), baseVersion: o.baseVersion, ...nextSaveIds(o.noteKey) };
      const res = await putDraft(o.url, body);               // always sends the newest text
      return { version, savedAtUtc: res.savedAtUtc };
    },
    networkMode: 'online',                                     // the client's default, 'always', never pauses
    retry: (_n, e) => isTransient(e),                          // network, timeout, 408, 429, 5xx
    retryDelay: (n, e) => retryAfterMs(e) ?? Math.min(1_000 * 2 ** n, 10_000),
    onMutate: () => { v.current.inFlight = true; },
    onSuccess: ({ version, savedAtUtc }) => {
      v.current.saved = Math.max(v.current.saved, version);
      if (v.current.saved >= v.current.local) v.current.firstUnsavedAt = 0;
      setSavedAt(savedAtUtc);
    },
    onError: async (e) => { const b = await o.onBlockingError(e); setBlocked(b); v.current.halted = b !== null; },
    onSettled: () => { v.current.inFlight = false; rerender(); if (v.current.local > v.current.saved) schedule(0); },
  });

  function schedule(delay: number) {
    const s = v.current;
    if (s.halted || s.inFlight) return;                       // onSettled sends the latest
    window.clearTimeout(timer.current);
    const left = MAX_WAIT_MS - (Date.now() - s.firstUnsavedAt);
    timer.current = window.setTimeout(() => save.mutate(), Math.max(0, Math.min(delay, left)));
  }
  const markChanged = () => { const s = v.current; s.local += 1; s.firstUnsavedAt ||= Date.now(); rerender(); schedule(DEBOUNCE_MS); };
  const flush = () => { if (v.current.local > v.current.saved) schedule(0); };

  useEffect(() => {                                            // page hidden: last reliable moment (Page Lifecycle)
    const onVisibility = () => {
      const s = v.current;
      if (s.halted || s.local === s.saved) return;
      if (document.visibilityState === 'visible') return schedule(0);
      window.clearTimeout(timer.current);
      const version = s.local;
      const json = JSON.stringify({ ...o.getContent(), baseVersion: o.baseVersion, ...nextSaveIds(o.noteKey) });
      void api.raw(o.url, { method: 'PUT', body: json,
                            keepalive: new TextEncoder().encode(json).length < 60_000 }) // 64 KiB cap
        .then((r) => r.ok && r.json())
        .then((r) => { if (r) { s.saved = Math.max(s.saved, version); setSavedAt(r.savedAtUtc); } })
        .catch(() => {});                                      // the regular retry loop owns failures
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
  }, []);

  const s = v.current;
  const status: SaveStatus =
    blocked ? 'blocked'
    : s.local > s.saved ? (save.isPaused || save.failureCount >= 2 ? 'failed' : 'saving')
    : s.local === 0 ? (savedAt ? 'loaded' : 'empty')
    : 'saved';

  useEffect(() => {                                            // only while something is at risk
    if (!(status === 'saving' || status === 'failed' || blocked === 'takenOver')) return;
    const warn = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = true; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [status, blocked]);

  return { status, savedAt, blocked, markChanged, flush /* , checkSave, flushNow, pause, resume, stop */ };
}
```

```tsx
// SaveStatus.tsx
export function SaveStatus({ status, savedAt, kind, today }: Props) {
  return (
    <p id="save-status" className={styles.status} data-state={status}
       role="status" aria-live="polite" aria-atomic="true">
      {status === 'failed' && <WarningIcon className={styles.icon} aria-hidden="true" />}
      {statusText(status, savedAt, kind, today)}
    </p>
  );
}
function statusText(s: SaveStatus, savedAt: string | null, kind: 'draft' | 'edit', today: string) {
  const when = savedAt ? formatSaved(savedAt, today) : '';
  switch (s) {
    case 'empty':   return '';
    case 'loaded':  return kind === 'draft' ? <>Draft<span aria-hidden="true"> · </span>saved {when}</> : `Saved ${when}`;
    case 'saving':  return 'Saving…';
    case 'saved':   return `Saved ${when}`;
    case 'failed':  return 'Not saved: no connection. Keep this page open; retrying.';
    case 'blocked': return 'Not saved';
  }
}
```

```css
/* SaveBar.module.css */
.bar {
  position: sticky; inset-block-start: 0; z-index: 10;
  display: grid; grid-template-columns: auto 1fr; align-items: center;
  gap: 0.5rem 1rem; padding: 0.5rem 1rem;
  background: var(--surface); border-block-end: 1px solid var(--border);
}
@media (max-height: 30rem) { .bar { position: static; } }   /* landscape phone, 200%+ zoom (SC 1.4.10) */

.status { margin: 0; justify-self: end; text-align: end; font-size: 1rem; color: var(--text-secondary); } /* ≥ 4.5:1 */
.status[data-state='blocked'] { font-weight: 600; color: var(--warning-text); }
.status[data-state='failed'],
.banner {
  grid-column: 1 / -1; justify-self: stretch; text-align: start;
  padding: 0.75rem; color: var(--warning-text); background: var(--warning-surface);
  border: 2px solid var(--warning-border);                   /* a real border survives forced colours */
}
.status[data-state='failed'] { display: flex; gap: 0.5rem; align-items: flex-start; }
.actions { display: flex; flex-direction: column; gap: 0.5rem; margin-block-start: 0.75rem; }
@media (min-width: 40rem) { .actions { flex-direction: row; flex-wrap: wrap; } }
.actions > button { min-block-size: 2.75rem; }
@media (forced-colors: active) { .status[data-state='failed'], .banner { border-color: CanvasText; } }
```

Set `--bar-height` from a `ResizeObserver` on `.bar`, and use `html { scroll-padding-block-start: var(--bar-height, 0px); }`. Set it to `0px` when the media query makes the bar static. Scroll anchoring keeps the caret steady when the bar grows. Check on iOS Safari.

---

## Per-screen notes

**Note form: new note (4.3).**
- The indicator is empty until the first change.
- The first save carries `listsVersion`. Its possible refusals (lists changed, started by someone else, after midnight, archived) are the only places a brand-new note shows a banner.
- A mis-tap creates nothing (A8), so a tick that is immediately un-ticked within 2 s still sends one save, which creates a draft. That matches "a draft is created at the first change" and is not worth special handling.

**Note form: reopened draft (4.3, 4.2 "Your unfinished drafts").**
- Shows "Draft · saved 9:42 am". If the save was on an earlier Melbourne day, the date is included.
- The earlier-day banner ("This draft is for Wed 30 Sep. Submitting it now keeps that date.", V) sits in the header, separate from the sticky bar, so the bar shows at most one warning.
- Tapping Submit still runs the check save. This is the case where a stale tab is most likely.

**Note form: after an idle sign-out (8.5).**
- `session-timeout.md` pauses this engine, keeps the route mounted but hidden, then resumes.
- Because `{clientId, seq}` live in the module map, the retried save continues the same sequence and does not trigger a takeover.

**Note form: manager's past-day note (3.8).** Identical behaviour. The after-midnight refusal does not apply to managers.

**Note form: editing a submitted note (3.5).**
- The indicator never shows "Draft".
- Save changes follows the Submit rules (check save first) and then posts directly. There is no confirmation dialog in the design.
- Cancel stops autosave and waits for the save in flight before `DELETE …/draft`.
- A `409 note.version_conflict` gives the version-conflict notice. A `409 draft.taken_over` on the pending edit (the same editor on two devices) gives the takeover banner, exactly as for a draft.

**Submit confirmation (4.3).**
- Opens only after a successful check save, so "Submit is disabled while the latest change is not saved" also covers a takeover the page did not yet know about.
- Inside the dialog, `confirm-dialog.md` handles the `Idempotency-Key`, the busy text after 1 s, errors and focus.
- Autosave is stopped while the version `POST` is in flight and resumed if the person taps Go back.

**Today (4.2), consumer only.** On leaving the form, invalidate the `today` and `me/drafts` queries so "Draft · You · saved 9:42 am" and "Your unfinished drafts" match the last acknowledged save.

---

## Anti-patterns to avoid

- **A toast for "Saved" or "Not saved".** It disappears while the problem persists. Use the indicator and a persistent banner.
- **"Saved" before the server acknowledges**, or "Saved" after a save that carried an older change number than the newest change.
- **A debounce with no maximum wait.** Non-stop dictation never saves.
- **Retrying with the original request body.** Read the content when the request is sent.
- **A new `clientId` per retry, per component mount, or per tab focus.** Each causes silent takeovers. A `seq` that starts again at 1 within the same page load gets saves ignored as stale while the page shows "Saved".
- **Letting the draft query refetch on focus or reconnect and overwrite the form.**
- **Disabling or making read-only the text box or tick boxes** during a save, an outage or a conflict. Typing never stops.
- **`disabled` on Submit.** It loses focus and gives no reason. Also: forgetting that `aria-disabled` does not stop the click or Enter handler.
- **Remembering a Submit tap and opening the dialog whenever the save finally succeeds.**
- **`role="alert"` or `aria-live="assertive"` on the routine indicator.** Assertive is only for the takeover and the other blocking banners.
- **A conditionally rendered live region** (`{msg && <p role="status">}`). Silent in NVDA and Orca. Keep the element mounted and change its text.
- **Moving focus to the indicator or a banner after a background save.** Focus moves only after the person's own tap (version conflict).
- **A spinner animating every few seconds** next to the participant's name.
- **A permanent `beforeunload` listener, or any `unload` listener.**
- **`localStorage`, `sessionStorage`, IndexedDB or a service worker as a backup copy** (D22, §9.6).
- **Relative times ("saved 2 minutes ago")**, which need a ticking timer that re-announces and goes stale. Use the clock time (V).
- **Sending an autosave after Discard, Cancel or Submit has started.** It can recreate a working copy.
- **Showing two warnings in the sticky bar at once.** Blocking banners replace the connection banner.

---

## Tensions with decisions

1. **D22 (online only, nothing stored on the device) vs never losing text.**
   - Evidence: browsers may freeze or discard hidden pages without warning (Chrome Page Lifecycle), and `beforeunload` is unreliable on phones (MDN).
   - Text typed during an outage lives only in page memory. If the phone discards the tab while the person is in another app, or the battery dies before the connection returns, that text is gone.
   - Many autosave systems keep a local copy for exactly this reason.
   - Within D22 the mitigations are the page-hidden `keepalive` save and the design's "Keep this page open" wording. Recorded only. No change proposed.
2. **design.md 4.3 "Submit is disabled" vs GOV.UK's advice to avoid disabled buttons** (Convention, above).
   - The reason in the design holds: a version must carry saved content (§5.8).
   - This doc keeps the rule and reduces the cost with `aria-disabled`, a description and the check save. No change proposed.
3. **design.md 4.3 "Load the other version" is a choice made without seeing the other version.** MediaWiki's convention shows both before the person decides (Convention, above). Recorded only.
4. **One failure string for every cause.** web.dev advises telling a server problem apart from the person's own connection. "Not saved: no connection" will also show during a Grow2Notes outage (a `5xx`). The instruction "Keep this page open; retrying" is right either way. Recorded only.
5. **No manual "Try again" in the Not saved banner.** GitLab Pajamas offers a manual retry, while the design retries automatically. Automatic retry on the `online` event and when the page becomes visible covers the same need. Recorded only.

---

## Sources

- W3C, Understanding SC 4.1.3 Status Messages — https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- W3C, Technique ARIA22, using role=status — https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- W3C, Failure F103 — https://www.w3.org/WAI/WCAG22/Techniques/failures/F103
- W3C, Understanding SC 2.4.11 Focus Not Obscured (Minimum) — https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- W3C, Understanding SC 3.3.4 Error Prevention (Legal, Financial, Data) — https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- W3C, Understanding SC 1.4.1, 1.4.10, 2.2.1, 2.5.8, 3.2.2, 3.3.7 — https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · https://www.w3.org/WAI/WCAG22/Understanding/reflow.html · https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html · https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html · https://www.w3.org/WAI/WCAG22/Understanding/on-input.html · https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- WAI-ARIA APG, Alert pattern — https://www.w3.org/WAI/ARIA/apg/patterns/alert/
- MDN, ARIA live regions — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- MDN, aria-disabled — https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-disabled
- MDN, beforeunload event — https://developer.mozilla.org/en-US/docs/Web/API/Window/beforeunload_event
- MDN, Navigator.onLine — https://developer.mozilla.org/en-US/docs/Web/API/Navigator/onLine
- MDN, RequestInit (keepalive) — https://developer.mozilla.org/en-US/docs/Web/API/RequestInit
- Chrome for Developers, Page Lifecycle API — https://developer.chrome.com/docs/web-platform/page-lifecycle-api
- TanStack Query v5, Network mode — https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
- TanStack Query v5, Mutations — https://tanstack.com/query/v5/docs/framework/react/guides/mutations
- TanStack Query v5, Migrating to v5 (onlineManager) — https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5
- React Router, useBlocker — https://reactrouter.com/api/hooks/useBlocker
- Adrian Roselli, Live Region Support (January 2026) — https://adrianroselli.com/2026/01/live-region-support.html
- Nielsen Norman Group, Visibility of System Status — https://www.nngroup.com/articles/visibility-system-status/
- Nielsen Norman Group, Indicators, Validations, and Notifications — https://www.nngroup.com/articles/indicators-validations-notifications/
- Nielsen Norman Group, Error-Message Guidelines — https://www.nngroup.com/articles/error-message-guidelines/
- Baymard Institute, Retain card data after validation errors — https://baymard.com/research-articles/preserve-card-details-on-error
- GOV.UK Design System, Button — https://design-system.service.gov.uk/components/button/
- GOV.UK Design System, Notification banner — https://design-system.service.gov.uk/components/notification-banner/
- Australian Government Design System, Page alert — https://design-system.agriculture.gov.au/components/page-alert
- Australian Government Design System, Section alert — https://design-system.agriculture.gov.au/components/section-alert
- GitLab Pajamas, Saving and feedback — https://design.gitlab.com/patterns/saving-and-feedback/
- Carbon Design System, Inline loading — https://carbondesignsystem.com/components/inline-loading/usage/
- Apple Human Interface Guidelines, File management — https://developer.apple.com/design/human-interface-guidelines/file-management
- Wikipedia, Help:Edit conflict — https://en.wikipedia.org/wiki/Help:Edit_conflict
- web.dev, Offline UX design guidelines — https://web.dev/articles/offline-ux-design-guidelines
- UI-Patterns, Autosave — https://ui-patterns.com/patterns/autosave
- Smashing Magazine, Usability Pitfalls of Disabled Buttons — https://www.smashingmagazine.com/2021/08/frustrating-design-patterns-disabled-buttons/
- Sibling docs: `design/ux/components/confirm-dialog.md`, `form-validation.md`, `session-timeout.md`
