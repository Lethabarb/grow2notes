# Success messages and page banners

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. No success banners except "Note for [name] submitted" after Submit (editorial pass): Restore, Deactivate, Reset sign-in, Reactivate, Invite and Save move focus to the `<h1>` or the changed state instead. The message store is memory only.

Component key: `status-messages`. This covers the one success message design.md specifies ("Note for Jane Citizen submitted"). It also covers the static page banners and labels that tell people what kind of note or record they are looking at: the past-day note banner, the earlier-day draft banner, the archived-participant read-only banner, the "Past-day note, written on … by …" line and the "Edited" label. It adds no screen, setting, notification or data. It describes how to build what design.md already specifies.

**Not covered here** (owned by other components, which can reuse the banner shell below): the save indicator and its "Not saved" banner (4.3), the "changed on another device or tab" conflict banner (4.3), the lists-changed message (4.3), validation errors and the error summary, the session-timeout dialog (4.0), the submit confirmation dialog itself (4.3), and the Flagged badge (see `notification-badge.md`).

---

## Where it's used

The component comes in three kinds. They behave differently, so they are named separately throughout this document.

- **Success banner:** the result of something the user just did, shown on the screen they land on.
- **Notice:** a static banner that is part of the page from the moment it loads.
- **Inline status:** a short result shown next to the button that caused it, on a screen the user stays on.

| Screen | What appears | Kind | What differs |
|---|---|---|---|
| **App shell (global), 4.0** | Nothing visible. The shell holds the in-memory message store and the route-change focus rule that the success banner depends on. | Plumbing | No global toast area and no global live region. Pages render their own messages. |
| **Today, 4.2** | "Note for Jane Citizen submitted" after Submit (4.3: "After submitting, the user returns to Today with…") | Success banner | The only success banner design.md specifies. It arrives with a navigation, so it takes focus and needs no live region. |
| **Note form and submit confirmation, 4.3** | "Past-day note for Mon 28 Sep 2026, written on 1 Oct." (manager's past-day note). "This draft is for Wed 30 Sep. Submitting it now keeps that date." (draft from an earlier day). | Notice | Present when the form opens, sitting under the name and date. Never announced as a live update and never takes focus. If both apply, only one shows (see Per-screen notes). The confirmation dialog shows no success message. Success appears only on Today, after the server confirms. |
| **Participant notes (history) and read view, 4.4** | The "Edited" label with the last editor and time. "Past-day note, written on … by …". The "Edited" tag on history rows. | Static label (metadata line) | Plain text in the note's header block. Not a banner, not a live region, not interactive. |
| **Participants list and participant detail, 4.8** | Archived participant: "a read-only banner with Restore". Write past-day note opens the form with the past-day notice. Details **Save** result. | Notice with one action, plus inline status | The archived notice is the only banner holding a control. It is focused when it appears because of Archive. After Restore it is replaced by a success banner. Save on Details shows an inline status. |
| **Guide prompts, 4.10** | The result of **Save**. | Inline status | The manager stays on the page, so focus stays on Save and a polite `role="status"` announces the result. The help text is ordinary page text, not a banner. |

---

## Best practice

**Toasts or inline banners**

- Toasts are missed. Practitioners who test with assistive technology report that screen-magnifier users may never see a toast drawn outside the magnified area. Virtual-cursor screen-reader navigation fires no focus events, so "pause on focus" timers never pause. People with motor impairments may not reach a toast before it disappears. **[Opinion]** (expert practitioner, from AT testing) https://www.scottohara.me/blog/2019/07/08/a-toast-to-a11y-toasts.html
- Adrian Roselli: a toast on its own is not enough for messages that matter, because users miss them through distraction or magnification. He recommends `role="status"`, no interactive content, and either a persistent message area or "do nothing". **[Opinion]** https://adrianroselli.com/2020/01/defining-toast-messages.html
- NN/g (Flaherty, January 2024) reports a user who missed a brief toast error and "spent 5 minutes waiting for content to load". The article says persistent passive notifications avoid being missed entirely. **[Research]** (NN/g usability observation, qualitative) https://www.nngroup.com/articles/indicators-validations-notifications/
- Adam Silver (2020) lists the problems with toasts (they disappear, are hard to reach by keyboard, cover content, are easy to miss with a magnifier and sit somewhere other than system messages). He recommends showing "a prominent message at the top of the page", kept until the user navigates away. **[Opinion]** https://adamsilver.io/blog/the-problem-with-toast-messages-and-what-to-do-instead/
- Neither the GOV.UK Design System nor the NHS digital service manual offers a toast. Both offer a success notification banner at the top of the page after an action. AgDS offers a Page alert (success, info, warning, error) near the top of the page and no toast. **[Convention]** https://design-system.service.gov.uk/components/notification-banner/ · https://service-manual.nhs.uk/design-system/components/notification-banners · https://design-system.agriculture.gov.au/components/page-alert
- Carbon allows toasts to auto-dismiss after about 5 seconds, but only when they hold no action. Carbon's inline notifications do not auto-dismiss. Material Components for Android uses fixed snackbar durations of `SHORT_DURATION_MS = 1500` and `LONG_DURATION_MS = 2750`. Neither system cites a study for its timing. **[Convention]** https://carbondesignsystem.com/components/notification/usage/ · https://github.com/material-components/material-components-android/blob/master/lib/java/com/google/android/material/snackbar/SnackbarManager.java
- WCAG 2.2.1 Timing Adjustable lets a self-dismissing message pass only when "there is an alternative that does not rely on a timer". Its example is an email toast that is backed by the inbox. If the timed message is the only way to get the information, it must be adjustable. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- React Aria's Toast is still `UNSTABLE_`. It is built as an F6-navigable landmark and advises auto-dismissing "only … when the information is not critical, or may be found elsewhere." **[Convention]** https://react-aria.adobe.com/Toast

**`role="status"`, `role="alert"` and WCAG 4.1.3**

- SC 4.1.3 defines a status message as "a change in content that is not a change of context" that reports success, results, waiting, progress or errors. The SC covers content that does **not** take focus. "Changes of context … are already surfaced by assistive technologies", and a dialog "takes focus" and so "does not meet the definition of a status message". **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- The listed failure is "Using `role="alert"` or `aria-live="assertive"` on content which is not important and time-sensitive." **[Standard]** (same page)
- ARIA22 (role=status) requires the container to have `role="status"` *before* the message is written into it. **[Standard]** https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- `status` implies `aria-live="polite"` and `aria-atomic="true"`. MDN: "Do not give focus to the status when its content updates", and "If a situation requires that focus needs to be moved, then using a `status`, or other live region, are likely not appropriate." **[Standard]** (MDN documentation of WAI-ARIA 1.2) https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/status_role
- WAI-ARIA APG Alert: "Screen readers do not inform users of alerts that are present on the page before page load completes", and "Frequent interruptions inhibit usability for people with visual and cognitive disabilities." **[Standard]** https://www.w3.org/WAI/ARIA/apg/patterns/alert/
- An empty live region that already exists in the DOM is "the most robust way" to get an announcement. Injecting a region, or switching one from `display: none`, is effectively the same as creating it with its content, and that is unreliable. O'Hara also says fewer live regions is better, "none being the ideal". **[Opinion]** (expert practitioner, from AT testing) https://www.scottohara.me/blog/2022/02/05/are-we-live.html

**Success after a page change: placement, focus and persistence**

- GOV.UK and NHS place the success banner "immediately before the page `h1`", at the same width as the content. For the success type they set `role="alert"`, and "JavaScript moves the keyboard focus to the notification banner when the page loads". GOV.UK's code comment says this is "to help some assistive technologies prioritise announcing it". Its `setFocus` adds `tabindex="-1"` and removes it on blur. **[Convention]** https://design-system.service.gov.uk/components/notification-banner/ · https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/notification-banner/notification-banner.mjs · https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/common/index.mjs
- GOV.UK and NHS: "Avoid showing more than one notification banner on the same page." Do not use a notification banner for validation errors or next to an error summary. Both admit research gaps on dismissal and on how often people miss banners. GOV.UK says the banner needs "more research". **[Convention]** (same URLs)
- NHS: "Use a success banner to confirm an action on a previous page was successful". "Don't use headings for single-line notifications." NHS (page updated June 2026) and GOV.UK give the banner a title ("Success" or "Important") so meaning does not rest on colour alone. **[Convention]** https://service-manual.nhs.uk/design-system/components/notification-banners
- AgDS places Page alerts "under the H1" and offers focus-on-mount or manual `tabIndex={-1}` focus. That differs from GOV.UK and NHS, which place it before the h1. **[Convention]** https://design-system.agriculture.gov.au/components/page-alert
- In a single-page app, a route change does not reset focus by itself. In Marcy Sutton and Fable's user testing (2019, n=5: NVDA, JAWS, ZoomText, Dragon and switch users), "focusing on a heading was found to be the best experience" for screen-reader users. **[Research]** (small qualitative study) https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- React Router's `navigate(…, { state })` stores state in `history.state`. MDN: "Some browsers save `state` objects to the user's disk so they can be restored after the user restarts the browser." **[Standard]** https://reactrouter.com/api/hooks/useNavigate · https://developer.mozilla.org/en-US/docs/Web/API/History/pushState

**Static notices present on load**

- GOV.UK keeps notification banners for information "not directly related to the page content". It also warns that "some users do not notice inset text" on busy pages, and offers Warning text, with a visually hidden "Warning" prefix, for consequences. **[Convention]** https://design-system.service.gov.uk/components/notification-banner/ · https://design-system.service.gov.uk/components/inset-text/ · https://design-system.service.gov.uk/components/warning-text/
- Carbon's Callout is the static variant: it is present on page load, sits near the related content, cannot be dismissed and has no live region. Carbon notes callouts "are not automatically announced by screen readers". **[Convention]** https://carbondesignsystem.com/components/notification/usage/ · https://carbondesignsystem.com/components/notification/accessibility/
- NHS warning callouts need "a short, clearly worded heading". In NHS testing, users "noticed the yellow callouts and understood them as a warning". The NHS has not tested them in forms or transactional content. **[Research]** (NHS service-manual testing notes) https://service-manual.nhs.uk/design-system/components/warning-callout

**Dates in messages**

- Australian Government Style Manual: "Only use abbreviations if space is limited – for example, in tables …". It also advises avoiding abbreviated dates in body text, because "Words written in full are usually easier to read and understand." **[Convention]** https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- GOV.UK style guide: truncated months only "when space is an issue". **[Convention]** https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/

**General**

- Never show success before the server confirms it. The ui-ux-design corpus invariant I5 says: "State at the moment of commitment is unambiguous and named. Optimistic success is a hazard wherever a ledger or third party is the authority." A submitted note is a record. **[Opinion]** (synthesis)
- Use red only for errors. Do not spend an attention channel that already carries a safety or data signal (invariant I11). **[Opinion]**

---

## Recommendation for Grow2Notes

### Decisions in one line each

1. **No toasts anywhere.** Use the success banner at the top of the destination page, an inline status next to the button, or a static notice.
2. **The success banner takes focus and has no live role.** Because focus moves to it, it is not a 4.1.3 status message, and a live role would risk it being read twice.
3. **Inline status uses `role="status"`** on a container that is always rendered.
4. **Notices are plain page content.** No role, no focus, no announcement, no dismiss.
5. **Nothing is timed and nothing is dismissible.** Messages last until the user leaves the screen.
6. **Messages live in memory only.** Never in `history.state`, the URL or browser storage (D22, 4.0).

### Anatomy

```
Success banner (Today)                     Notice (note form, participant detail)
+--------------------------------------+   +--------------------------------------+
|▌ ✓  Note for Jane Citizen submitted  |   |▌ i  Past-day note for Mon 28 Sep    |
+--------------------------------------+   |     2026, written on 1 Oct.          |
 ▲    ▲   ▲                                 +--------------------------------------+
 │    │   └ one sentence, bold, design.md copy
 │    └ icon, aria-hidden (the words carry the meaning, SC 1.4.1)
 └ 6 px inline-start bar + 1 px border in the tone colour (survives forced colours)

Notice with an action (archived participant, 4.8)
+--------------------------------------+
|▌ i  Jane Citizen is archived, so     |
|     this page is read-only. …        |
|     [ Restore ]                      |   ← native <button>, 44 px tall
+--------------------------------------+

Inline status (Guide prompts, participant Details)
[ Save ]  Saved 4:12 pm                    ← <p role="status">, always in the DOM
```

- **Two tones only.** **Success** is a green bar with a tick. **Notice** is a neutral or blue bar with an "i". Red stays for errors and amber for warnings, both owned by other components. Text is the normal body colour on a pale tint, never coloured text.
- **No title row** ("Success" / "Important"). Every message here is one or two short sentences, and NHS says not to use headings for single-line notifications. The words "submitted", "Past-day note", "archived" and "read-only" already say what kind of message it is, so colour is not needed (SC 1.4.1). **[Opinion]** This differs from GOV.UK and NHS, which add a title; see Anti-patterns for why not.
- **No close button** and no auto-dismiss. design.md specifies neither.
- **No heading element** inside any banner, so the page's heading outline is unchanged.

### Behaviour

**Success banner (Today after Submit)**

1. The confirmation dialog's "Submit note for Jane Citizen" calls `POST {base}/versions`. Nothing says "submitted" until the server returns `201`, or `200` on an idempotent retry. While waiting, the dialog button shows its pending state (owned by the button and dialog components). Errors stay in the dialog (owned by the error component).
2. On success: write the text into the in-memory message store, update the Today cache so the row reads Submitted, then navigate to Today.
3. Today renders the banner **immediately before the `h1`** ("Today · Thursday 1 October"). It renders outside any loading boundary, so it is there even while the list refetches.
4. The shell's route-change focus rule focuses the banner instead of the `h1` (the banner carries `data-route-focus`). A screen reader reads "Note for Jane Citizen submitted". The next swipe reads the `h1`, so the user knows they are on Today. The next Tab goes to the first control after the banner. The page is scrolled to the top.
5. **Persistence:** the banner stays while the user is on Today, including across list refetches and window focus. It is gone when they leave Today, reload the page or sign out. A second submit replaces it, so there is only ever one success banner.
6. The Today row's "Submitted · … · 4:12 pm" status is the permanent record. The banner only speeds up noticing it. This is the "alternative that does not rely on a timer" of SC 2.2.1, although the banner is not timed anyway.

**Notices (past-day, earlier-day draft, archived)**

- Rendered from data when the page renders (`isPastDayNote`, `noteDate` against `me.today`, participant `status`). They are not live regions, never take focus on page load and are never dismissible.
- In reading order they sit directly after the participant name and date (4.3 item 2), or directly after the participant name on participant detail (4.8). After the route-change focus lands on the `h1`, the next swipe or arrow reads the notice.
- **One exception:** when the archived notice appears *because the manager just pressed Archive*, the Archive button has disappeared. Focus moves to the notice (`tabIndex={-1}`), so the outcome is read and the Restore button inside it is the next Tab stop.

**Inline status (Save on Guide prompts and participant Details)**

- `<p role="status">` is always rendered next to the Save button. It is empty until a save succeeds, then it reads "Saved 4:12 pm". Focus stays on Save. The text is announced politely.
- It is cleared as soon as the field changes again, so it never claims "Saved" while there are unsaved edits. It also makes the next save a real text change, so it is announced again.

### States

| State | Success banner | Notice | Inline status |
|---|---|---|---|
| **Default** | Green bar, tick, bold sentence | Neutral bar, "i", sentence | "Saved 4:12 pm" in secondary text, same size as body |
| **Empty** | Not rendered | Not rendered when it does not apply | The `<p role="status">` is rendered but empty, with no border or padding. Never `display: none` (it would break the live region). |
| **Loading** | No loading state. It appears only after `201`. | No loading state. It renders with the page data. | None. The Save button owns its pending state. |
| **Focus** | When focused by the route rule: a 3 px focus outline with 2 px offset, ≥3:1, via `:focus-visible` (the browser decides whether to draw it after programmatic focus). `scroll-margin-block-start` keeps it clear of a sticky top bar (SC 2.4.11). | Not focusable, except the archived notice just after Archive (same outline). The Restore button has the standard button focus. | Not focusable |
| **Hover / active** | None (not interactive) | None on the box. Restore has standard button hover and active styles. | None |
| **Disabled** | n/a | n/a. Restore is never disabled. While restoring it uses `aria-disabled` and a pending label (button component). | n/a |
| **Error** | n/a. A failed submit never shows this banner. | n/a. A failed Restore shows an error next to the button (error component), and the notice stays. | A failed save shows an error (error component). The status stays empty. |
| **Read-only** | n/a | The archived notice *is* the read-only signal for 4.8. Fields show as text, not greyed-out inputs (form components). | n/a |

### Phone vs laptop

- **Same component and same proportions.** Sizes are in `rem`/`em`, there are no fixed heights, and text wraps at 200% and at 320 px (SC 1.4.4, 1.4.10, 1.4.12).
- **Phone:** full width of the content column, 16 px horizontal padding, icon top-aligned with the first line. Restore sits on its own line under the text, full width, at least 44 × 44 px (A32).
- **Laptop:** the banner is the width of the content column, not the window. Restore sits after the text, at the inline end, when there is room (a container query at about 32 rem).

### Exact copy

| Where | Text | Source |
|---|---|---|
| Today, after Submit | `Note for Jane Citizen submitted` | design.md 4.3 (verbatim) |
| Note form, manager's past-day note | `Past-day note for Mon 28 Sep 2026, written on 1 Oct.` | design.md 4.3 (verbatim; see Tensions on abbreviations) |
| Note form, draft from an earlier day | `This draft is for Wed 30 Sep. Submitting it now keeps that date.` | design.md 4.3 (verbatim) |
| Read view, past-day note | `Past-day note, written on 1 Oct 2026, 4:12 pm by Sam Lee` | design.md 3.8 and 4.4 template; date and time format from 4.5 ("1 Oct 2026, 5:03 pm") |
| Read view, edited note | `Edited · last change by Jo Smith, Fri 2 Oct 9:01 am` | design.md 3.5 ("with the last editor and time"); wording mirrors the report marker in 11.3 without the version count, which is manager-only (A13) |
| History row tag | `Edited` | design.md 4.4 |
| Participant detail, archived | `Jane Citizen is archived, so this page is read-only. They are not on Today and no new notes can be written for them. Their past notes are kept.` + button `Restore` | **Proposed.** design.md 4.8 gives no string; content taken from 3.7. Needs owner sign-off. |
| Participant detail, after Restore | `Jane Citizen restored. They are back on Today.` | **Proposed.** design.md 3.7 says "Back on Today". Needs owner sign-off. |
| Guide prompts and participant Details, after Save | `Saved 4:12 pm` | Reuses the note form's save-indicator wording (4.3, "Saved 9:42 am"), so one term covers one concept |

Times use the shared Melbourne formatter (`Intl.DateTimeFormat('en-AU', { timeZone: 'Australia/Melbourne', hour: 'numeric', minute: '2-digit' })`, giving "4:12 pm"). Dates use one shared date formatter, so a later copy change happens in one place. Names are the participant's full name, as in the form header (3.9).

### Accessibility

- **Semantics.** Success banner: `<div tabindex="-1" data-route-focus>` containing an `aria-hidden` icon and a `<p>`. No role. Notice: `<div>` containing an `aria-hidden` icon, a `<p>` and an optional `<button type="button">`. No role and no landmark. Inline status: `<p role="status">`. No `aria-live` attribute is added anywhere by hand, there is no `role="alert"`, and there is no `aria-label` on the boxes.
- **Why the success banner has no `role="alert"`, unlike GOV.UK.** GOV.UK and NHS serve full pages, and the APG says alerts present before page load completes are not announced. They focus the banner to make sure it is read. In this single-page app the banner is inserted after load. An `alert` role *plus* a focus move risks the message being read twice, and MDN says a live region is "likely not appropriate" when focus moves. Focus on its own is enough. **[Opinion]**, to be confirmed by the test below. **Fallback:** if a tested screen reader does not read the focused banner, add `role="alert"`, which is GOV.UK's exact markup.
- **Keyboard.** Banners add no Tab stops, except Restore. Programmatic focus uses `tabIndex={-1}`, never a positive tabindex.
- **Announcements.** On Today after Submit: "Note for Jane Citizen submitted", read once, through focus. Notices are read in normal reading order with no interruption. After Save on Guide prompts or Details: "Saved 4:12 pm", read politely. After Archive: the notice text, through focus. After Restore: "Jane Citizen restored. They are back on Today.", through focus.
- **Test.** Use NVDA + Chrome, VoiceOver + iOS Safari and TalkBack + Chrome. Check: the submitted message is read exactly once on arrival at Today, then the `h1` is next. "Saved 4:12 pm" is read once per save. Nothing is read on page load for notices. In Windows forced colours the bar and border stay visible. At 320 px and 200% text there is no overlap or horizontal scroll.
- **WCAG 2.2 criteria met:** 1.3.1 Info and Relationships; 1.3.2 Meaningful Sequence (notices sit next to what they describe); 1.4.1 Use of Color (words carry the meaning); 1.4.3 Contrast (Minimum) (body-colour text on a pale tint, ≥4.5:1); 1.4.4 Resize Text; 1.4.10 Reflow; 1.4.11 Non-text Contrast (focus outline ≥3:1); 1.4.12 Text Spacing; 2.1.1 Keyboard; 2.2.1 Timing Adjustable (nothing is timed); 2.2.2 Pause, Stop, Hide (no motion); 2.4.3 Focus Order; 2.4.7 Focus Visible; 2.4.11 Focus Not Obscured (Minimum); 2.5.8 Target Size (Minimum) (Restore, 44 px); 3.2.4 Consistent Identification (one banner look product-wide); 4.1.2 Name, Role, Value (Restore); 4.1.3 Status Messages (inline Save status through `role="status"`; the success banner and the post-Archive notice take focus, so they are outside 4.1.3 by its own definition).

### Implementation notes (React 19 + native HTML + CSS Modules)

No React Aria is needed: a `div`, a `p`, a native `button` and one `role="status"` are enough. Do not use React Aria's `UNSTABLE_Toast`.

```ts
// src/shell/messageStore.ts: memory only (D22). Never history.state, the URL or browser storage.
let pending: string | null = null;
export const messageStore = {
  set(text: string) { pending = text; },
  peek(): string | null { return pending; },
  clear() { pending = null; },
};
```

```tsx
// src/ui/Banner.tsx
import type { ReactNode } from 'react';
import styles from './Banner.module.css';

type Props = {
  tone: 'success' | 'notice';
  children: ReactNode;          // the sentence
  action?: ReactNode;           // e.g. <button type="button">Restore</button>
  takeFocus?: boolean;          // success banner, or archived notice right after Archive
};

export function Banner({ tone, children, action, takeFocus = false }: Props) {
  return (
    <div
      className={`${styles.banner} ${styles[tone]}`}
      tabIndex={takeFocus ? -1 : undefined}
      data-route-focus={takeFocus || undefined}
    >
      <span className={styles.icon} aria-hidden="true">{tone === 'success' ? '✓' : 'i'}</span>
      <div className={styles.body}>
        <p className={styles.text}>{children}</p>
        {action}
      </div>
    </div>
  );
}
```

```tsx
// In the submit mutation (owned by the note form):
// onSuccess: (res) => {
//   queryClient.setQueryData(['today'], markRowSubmitted(participantId, res)); // row and banner agree
//   void queryClient.invalidateQueries({ queryKey: ['today'] });
//   messageStore.set(`Note for ${fullName} submitted`);
//   navigate('/');   // no `state`: history.state can be written to disk
// }

// src/today/TodayPage.tsx (excerpt)
const [submitted] = useState(messageStore.peek);   // pure read; StrictMode-safe
useEffect(() => { messageStore.clear(); }, []);     // consumed once; gone on reload or revisit
return (
  <>
    {submitted && <Banner tone="success" takeFocus>{submitted}</Banner>}
    <h1 tabIndex={-1}>Today · {formatTodayHeader(me.today)}</h1>
    {/* drafts, search, list: may suspend or load; the banner and h1 must not */}
  </>
);
```

```ts
// Route-change focus rule (owned by the app shell; this component only needs the first selector):
// after each navigation, focus  main [data-route-focus]  if present, otherwise  main h1.
// One owner for route focus means the banner and the h1 never race.
```

```tsx
// src/ui/SaveStatus.tsx: Guide prompts (4.10) and participant Details (4.8)
export function SaveStatus({ savedAtUtc }: { savedAtUtc: string | null }) {
  // Always rendered; the parent sets savedAtUtc on 200 and back to null on the next edit.
  return (
    <p role="status" className={styles.saveStatus}>
      {savedAtUtc ? `Saved ${formatTime(savedAtUtc)}` : ''}
    </p>
  );
}
```

```css
/* Banner.module.css */
.banner {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
  padding: 0.75rem 1rem;
  border: 1px solid var(--banner-edge);
  border-inline-start-width: 6px;            /* the bar: a border, so forced colours keep it */
  border-radius: 4px;
  background: var(--banner-tint);
  color: var(--colour-text);
  margin-block-end: 1rem;
  scroll-margin-block-start: var(--top-bar-height, 4rem);  /* SC 2.4.11 */
  container-type: inline-size;
}
.success { --banner-edge: var(--colour-success-edge); --banner-tint: var(--colour-success-tint); }
.notice  { --banner-edge: var(--colour-notice-edge);  --banner-tint: var(--colour-notice-tint); }
.success .text { font-weight: 700; }
.text { margin: 0; }
.icon { flex: none; inline-size: 1.5em; text-align: center; font-weight: 700; color: var(--banner-edge); }
.body { display: flex; flex-direction: column; gap: 0.75rem; flex: 1; }
.body button { min-block-size: 44px; inline-size: 100%; }
@container (min-width: 32rem) {
  .body { flex-direction: row; align-items: center; justify-content: space-between; }
  .body button { inline-size: auto; }
}
.banner:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }
/* No transition or animation: messages appear at once (zero duration for status). */
@media (forced-colors: active) {
  .banner { border-color: CanvasText; }
  .icon { color: CanvasText; }
}

.saveStatus { margin: 0; min-block-size: 0; color: var(--colour-text-secondary); }  /* never display:none */
```

- Tokens `--colour-success-edge`, `--colour-success-tint`, `--colour-notice-edge` and `--colour-notice-tint` each need light and dark values. Body text on each tint must reach at least 4.5:1. Aim for 3:1 between the edge and the page background. SC 1.4.11 does not require it, because the words carry the meaning.
- Replace the `✓` and `i` characters with the icon set's SVGs when one exists. Keep `aria-hidden="true"`.
- Close the native `<dialog>` *before* calling `navigate`, so the dialog's own focus return does not compete with the route focus rule.

---

## Per-screen notes

**App shell (4.0)**
- Owns `messageStore` (memory only) and the route focus rule: `[data-route-focus]` first, then `h1`. It has no toast container, no global `aria-live` region and no message queue.
- On sign-out the app reloads with `Clear-Site-Data` (6.2), which empties the store. Nothing about a message is written to `history.state`, the URL (4.0: URLs hold only IDs and dates) or storage.

**Today (4.2)**
- One success banner, before the `h1`, focused on arrival, and kept until the user leaves Today.
- Update the row to "Submitted" from the `201` response before the banner is seen. Otherwise "submitted" could sit next to a row still reading "Draft · You · saved 9:42 am".
- No other messages on Today. Not for discard, and not for drafts in "Your unfinished drafts". Do not repeat the message on the row as a highlight or flash. The row status is enough.
- The success banner is the only banner on Today. The "Flagged" row tag and the nav badge are separate components.

**Note form and submit confirmation (4.3)**
- **Past-day notice:** shown when `isPastDayNote`, or for a new past-day form before the first save. It sits under the name and date, and stays in edit mode too, because 3.8 says the past-day line shows "everywhere it appears". "written on" is the date of `Note.CreatedAtUtc`, or `me.today` before the draft exists.
- **Earlier-day draft notice:** shown when `noteDate < me.today` and it is not a past-day note. If the date rolls over while the form is open (after `me` refetches on focus), the notice just appears. It is not announced, because it is not the result of the user's action.
- **Only one notice.** If both could apply (a manager continues a past-day draft on a later day), show only the past-day notice. It already names the note date. This follows GOV.UK and NHS: "Avoid showing more than one notification banner".
- Error and warning banners from other components (Not saved, changed on another device) go **above** the notice, because they need action. Notices never use red or amber.
- **Confirmation dialog:** no success state inside it. On `201`, close the dialog and go to Today. On error, stay in the dialog (error component).

**Participant notes (history) and read view (4.4)**
- "Past-day note, written on 1 Oct 2026, 4:12 pm by Sam Lee" and "Edited · last change by Jo Smith, Fri 2 Oct 9:01 am" are lines in the note's header block, in body or secondary text. They are not banners, have no icon and no live region, and are not links. Managers reach Version history through its own link (4.4).
- The history row tag reads "Edited" as a word. The tag component owns its look, and it must not rely on colour alone.
- design.md specifies no banner here for archived participants, so none is added.

**Participants list and participant detail (4.8)**
- **Archived notice** at the top of participant detail, under the name, with Restore. It is static when the page opens, and focused when it appears because of Archive.
- **Restore succeeds:** replace the notice with a success banner in the same place ("Jane Citizen restored. They are back on Today.") and focus it. The Restore button has gone, so focus needs somewhere meaningful to land. It stays until the manager leaves the screen.
- **Details Save:** `SaveStatus` next to Save. Focus stays on Save.
- **Write past-day note:** opens the form, which shows the past-day notice (4.3). The list screen has no banners.
- Goal add, edit, move and archive announcements belong to the goals-list component. If it needs them, it should use the same always-rendered `role="status"` pattern, not a banner.

**Guide prompts (4.10)**
- `SaveStatus` next to Save ("Saved 4:12 pm"), cleared on the next keystroke. No success banner, because the manager has not left the page.
- The help text ("Prompts are a guide only. …") is ordinary paragraph text, not a notice box.
- The unsaved-changes warning (dialog) and a `412` reload belong to other components.

---

## Anti-patterns to avoid

- Toasts or snackbars for anything, including "Note submitted". They are missed by magnifier users, tired users and anyone who looks away.
- Auto-dismissing any message, or adding a timer "for tidiness".
- `role="alert"` or `aria-live="assertive"` on success messages or notices. WCAG lists this as a failure for content that is not important and time-sensitive.
- Creating a live region together with its text, for example `{saved && <p role="status">Saved</p>}`, or toggling it from `display: none`. The container must be in the DOM first.
- Live roles on notices that are present on load. They are not announced and only add noise.
- Moving focus to a notice on page load. Only the success banner, and the archived notice right after Archive, take focus.
- Both a live role and a focus move on the same message. It may be read twice.
- Passing the message through `navigate(…, { state })`, a query string (`?submitted=Jane`), `sessionStorage` or `localStorage`. These write participant names to the device or the URL (D22, 4.0), and a reload would replay the message.
- Showing "submitted" before the server's `201` (optimistic success).
- A "Success" or "Important" title row. It adds reading for tired, ESL readers and repeats what the sentence already says. **[Opinion]**
- Stacking notices. Use one notice, plus any error banner from its own component.
- Colour as the only signal: a green bar with no words, or red for a notice.
- Close (×) buttons. design.md specifies none, and a dismiss adds state to remember.
- Slide-in, fade or bounce animation on banners.
- A notice box around help text, or around the "Edited" and past-day lines in the read view.
- Copying the message into `document.title` (titles stay generic, 4.0).

---

## Tensions with decisions

- **Abbreviated dates in design.md banner copy.** This is design.md copy, not a D-numbered decision. "Mon 28 Sep 2026", "written on 1 Oct" and "Wed 30 Sep" are abbreviated. The Australian Government Style Manual says "Only use abbreviations if space is limited" and that "Words written in full are usually easier to read and understand". GOV.UK allows truncated months only "when space is an issue". The brief for this work also gives full dates as the house style ("Thursday 1 October 2026"), and the Today header and form sketch already use full dates. Many users write in English as a second language. Recorded only. This document uses design.md's strings verbatim and builds them through one shared date formatter, so the owner can change them in one place if they ever choose to.

No D-decision conflicts with best practice for this component. No toasts, no notifications and download-only reports all agree with the evidence.

---

## Sources

- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Technique ARIA22, Using role=status: https://www.w3.org/WAI/WCAG22/Techniques/aria/ARIA22
- WCAG 2.2 Understanding 2.2.1 Timing Adjustable: https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- WAI-ARIA Authoring Practices, Alert pattern: https://www.w3.org/WAI/ARIA/apg/patterns/alert/
- MDN, ARIA `status` role: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/status_role
- MDN, `History.pushState()`: https://developer.mozilla.org/en-US/docs/Web/API/History/pushState
- GOV.UK Design System, Notification banner: https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK Design System, Inset text: https://design-system.service.gov.uk/components/inset-text/
- GOV.UK Design System, Warning text: https://design-system.service.gov.uk/components/warning-text/
- GOV.UK Frontend source, notification banner JS and `setFocus`: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/notification-banner/notification-banner.mjs · https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/common/index.mjs
- GOV.UK style guide, A to Z (Dates, Times): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- NHS digital service manual, Notification banners (updated June 2026): https://service-manual.nhs.uk/design-system/components/notification-banners
- NHS digital service manual, Warning callout: https://service-manual.nhs.uk/design-system/components/warning-callout
- Agriculture Design System (Australian Government), Page alert: https://design-system.agriculture.gov.au/components/page-alert
- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- Carbon Design System, Notification usage and accessibility: https://carbondesignsystem.com/components/notification/usage/ · https://carbondesignsystem.com/components/notification/accessibility/
- Material Components for Android, `SnackbarManager.java`: https://github.com/material-components/material-components-android/blob/master/lib/java/com/google/android/material/snackbar/SnackbarManager.java
- React Aria, Toast (UNSTABLE): https://react-aria.adobe.com/Toast
- React Router, `useNavigate`: https://reactrouter.com/api/hooks/useNavigate
- NN/g, Flaherty (January 2024), Indicators, Validations, and Notifications: https://www.nngroup.com/articles/indicators-validations-notifications/
- Scott O'Hara, A toast to an accessible toast (2019): https://www.scottohara.me/blog/2019/07/08/a-toast-to-a11y-toasts.html
- Scott O'Hara, Are we live? (2022): https://www.scottohara.me/blog/2022/02/05/are-we-live.html
- Adrian Roselli, Defining 'Toast' Messages (2020): https://adrianroselli.com/2020/01/defining-toast-messages.html
- Adam Silver, The problem with toast messages and what to do instead (2020): https://adamsilver.io/blog/the-problem-with-toast-messages-and-what-to-do-instead/
- Marcy Sutton (Gatsby, with Fable Tech Labs), What we learned from user testing of accessible client-side routing techniques (2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
