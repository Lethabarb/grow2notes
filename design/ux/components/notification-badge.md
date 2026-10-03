# Count badge on the Flagged nav item

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `notification-badge`. Covers the number shown next to **Flagged** in the manager navigation (design.md 4.0, 4.6; API 6.2 and 6.8; A14). Nothing here adds a screen, setting, notification or data item. It describes how to build the badge the design already specifies.

---

## Where it's used

The badge is a single component with one source of data: `toReviewCount` from `GET /api/auth/me` (design.md 6.2). It lives in the manager navigation, which appears on every manager screen. Workers never see it, because their navigation has no Flagged item (4.0, section 2).

| Screen | Role of the badge there | What differs |
|---|---|---|
| **App shell (global), 4.0** | The manager nav is "Today, Flagged (with badge), Report, and Manage". The badge sits on the Flagged item on every manager screen and refreshes "when a page loads and when the window regains focus; there is no push" (4.6, 6.8). | This is the component's only real home. Its look and behaviour are the same everywhere. Only the nav item's *current page* state changes. |
| **Today, 4.2** (home for everyone; where a manager lands after sign-in) | This is the main place a manager notices new flags. The Flag flow in 4.13 is "submits → the managers' badge goes up → a manager opens Flagged". | Today has **no counts** of its own (D25, A17), so the badge is the only number on this screen, and it stays in the nav. Today's participant rows can also carry a "Flagged" *tag* (4.2), which is a different object: a word on one note, not a count of the queue. The two must not look alike. |
| **Flagged notes, 4.6** | The nav item is the current page. The screen's first tab repeats the same number as "**To review (n)**". | The nav link has `aria-current="page"`. The badge must agree with the tab's "(n)". When the last flag is reviewed, the badge disappears and the screen shows "No flagged notes to review." |

---

## Best practice

**What a badge is for**

- A badge is an ambient, passive indicator. It draws attention to a nav item and does not demand action. NN/g defines a badge as something that "indicates a notification … or an item count", and classes indicators as "passive". They should be used only where users would miss the information without them. **[Research]** (NN/g guidance articles, not a controlled study) https://www.nngroup.com/articles/ui-elements-glossary/ · https://www.nngroup.com/articles/indicators-validations-notifications/
- The UK Ministry of Justice (MoJ) Design System's Notification badge is official guidance, last updated October 2025. It is for "the number of items" that need attention, such as a new case or referral. It is "best used in a navigation link", and should not be used "to just display a 'count' if there's nothing for the user to do". The guidance says that in the navigation the badge "can be reliably detected by screen reader users" and "is most prominent for sighted users". **[Convention]** https://design-patterns.service.justice.gov.uk/components/notification-badge/
- The Australian Government's Agriculture Design System (AgDS) Notification badge is "for numeric values" and goes "next to associated content – for example, Messages (1)". **[Convention]** https://design-system.agriculture.gov.au/components/notification-badge

**Accessible text**

- Put the meaning of the count into the accessible name of the element that owns it, not into a bare number. MUI: "Label the element that owns the badge … use `aria-label="Inbox, 4 unread messages"`". Primer: always pair a counter "with adjacent text that provides supplementary information regarding what the count is for". **[Convention]** https://mui.com/material-ui/react-badge/ · https://primer.style/product/components/counter-label/accessibility/
- MoJ's markup hides the visible digit from assistive tech and adds visually hidden context inside the link: `Messages<span …><span aria-hidden="true">5</span><span class="govuk-visually-hidden">(5 unread)</span></span>`. This uses no `aria-label`. **[Convention]** https://design-patterns.service.justice.gov.uk/components/notification-badge/
- Prefer visually hidden text to `aria-label` for content. Adrian Roselli's browser testing (2019, updated 2025) found "you cannot rely on `aria-label` being auto-translated", and recommends visually hidden text instead. This matters for managers who use a browser's translate feature. **[Research]** (practitioner testing) https://adrianroselli.com/2019/11/aria-label-does-not-translate.html
- SC 2.5.3 Label in Name says the accessible name must contain the visible text, but "differences in capitalization and punctuation are not relevant". It calls putting the visible label "at the start of the name" "a best practice". So "Flagged, 3 to review" is a valid name for a link showing "Flagged 3", and voice-control users can still say "click Flagged". **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- The standard visually hidden technique is clip, 1 px and `position: absolute`, not `display: none`. **[Convention]** https://www.a11yproject.com/posts/how-to-hide-content/

**Colour and text**

- A count in digits already carries the information, so colour is decoration (SC 1.4.1), and the pill shape is not a graphical object that needs 3:1 under SC 1.4.11. The digits must still meet 4.5:1 against the pill (SC 1.4.3). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- Red is the common badge colour. MoJ says "Red circles are commonly used to attract attention", and Material's Android `BadgeDrawable` uses "the theme's error color by default". Apple's HIG describes a red badge on tab bars, but that page could not be fetched, so its wording is **unverified**. **[Convention]** https://developer.android.com/reference/com/google/android/material/badge/BadgeDrawable
- Other systems keep red for errors. GOV.UK's red is defined as "Use this colour for error messages". AgDS's badge offers only **neutral** and **action** tones, with no red, and adapts to dark or light backgrounds while keeping 4.5:1 for text. **[Convention]** https://design-system.service.gov.uk/styles/colour/ · https://design-system.agriculture.gov.au/components/notification-badge
- In forced-colours mode (Windows contrast themes), `background-color` is overridden and `box-shadow` is forced to `none`. A pill drawn only with a fill or shadow loses its shape, so a real `border` is needed. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors

**Maximum count and zero**

- Caps are convention, not research. MoJ shows the number up to 98 and "99+" from 99. MUI caps at 99 by default. Material Android's default maximum is 4 characters, which displays up to "999+". No source cites a study. **[Convention]** (sources above)
- At zero, hide the badge. MoJ says not to display it when there is nothing to show, and MUI hides it at zero unless `showZero` is set. **[Convention]**

**Announcing changes (or not)**

- Do not put `aria-live` on a badge. SC 4.1.3 covers status messages about "the success or results of an action, … waiting state, … progress, … errors", and its intent is to inform users "in a way that doesn't unnecessarily interrupt their work". A count refreshed on page load or window focus is not the result of an action the user just took on that screen. **[Standard]** (interpretation) https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- MoJ: "The notification badge number will only update when the page loads. It's not 'dynamic'. If you want to change this, you'll need to consider accessibility." This is the same refresh model as design.md 4.6. **[Convention]** https://design-patterns.service.justice.gov.uk/components/notification-badge/
- Scott O'Hara warns that many live regions "all start barking at assistive technology users at the same time", and that fewer live regions, "none being the ideal", is better. **[Opinion]** (expert practitioner) https://www.scottohara.me/blog/2022/02/05/are-we-live.html
- Do not animate the count. A looping pulse is a WCAG 2.2.2 (Level A) failure when it auto-starts, lasts more than 5 seconds and sits beside other content. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html

**Navigation semantics and refresh mechanics**

- A nav destination is a link (`<a href>`), and the current one carries `aria-current="page"`. React Router's `NavLink` "automatically applies `aria-current="page"`". **[Standard]** https://www.w3.org/TR/wai-aria-1.2/#aria-current · https://reactrouter.com/api/components/NavLink
- TanStack Query v5 refetches on window focus using **only** `visibilitychange`: "The `visibilitychange` event is used exclusively now." Clicking back into a browser window that was visible beside another window does not fire `visibilitychange`. Meeting design.md 6.8 ("window focus" *and* `visibilitychange`) therefore needs a `focus` listener added. **[Standard]** (library documentation) https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5 · https://tanstack.com/query/v5/docs/framework/react/guides/window-focus-refetching

---

## Recommendation for Grow2Notes

### Anatomy

```
[ Flagged  (3) ]   ← one link; the pill is inside it
  │        │
  │        └ pill: digits only, aria-hidden; plus visually hidden ", 3 to review"
  └ visible label "Flagged" (design.md 4.0)
```

- **One element:** a native `<a>` (React Router `NavLink`) inside the manager `<nav>`. The pill and the hidden text are children of the link, so the badge is never its own tab stop or tap target. design.md calls it the "Flagged menu item", which here means a nav link. It does **not** mean ARIA `role="menuitem"`.
- **Pill:** shows digits only, placed after the label on the same line with a small gap. There is no icon, so the pill does not overlap a corner.
- **Hidden text:** `, {n} to review`, which uses the design's own term "To review" (4.6). It needs no plural form.
- **Accessible name:** "Flagged, 3 to review". With no flags, it is just "Flagged".

### Behaviour

1. **Source:** `toReviewCount` from `GET /api/auth/me`, held only in TanStack Query's memory cache. There is no persister and no browser storage, not even for this number (D22).
2. **Refresh triggers, and nothing else:**
   - **Page load:** the first `me` call at sign-in or start-up, **and each time the manager opens a screen** in the single-page app (route change). In an SPA, a user who opens a screen experiences it as a page load, and without this the badge would stay stale for a whole visit. **[Opinion]**: this is how design.md 4.6/6.8 reads for an SPA; see Open questions in the summary.
   - **Window focus:** TanStack's default `visibilitychange` handling, plus a `focus` listener (see code).
   - **After this manager's own Mark reviewed succeeds:** invalidate `me` along with the reviews list, so the badge and the "To review (n)" tab never disagree because of the manager's own action. Mark reviewed is followed by a screen update, which is a page load in this sense.
   - **No** timers, no polling (6.8), no push, no WebSocket.
3. **Show exactly the number, with no "99+" cap.** **[Opinion]** Caps are convention only (see Best practice). A 3-digit number is no wider than "99+", and an organisation with fewer than 20 workers will not plausibly reach 1,000 open flags. The exact number also matches the "To review (n)" tab, so the same queue never shows two numbers. The pill grows to fit.
4. **Zero hides the pill and the hidden text.** The link reads "Flagged".
5. **Changes are silent:** no `aria-live`, no animation, no colour flash. The new number simply renders (zero-duration change).

### States

| State | Visual | Accessible name / announcement |
|---|---|---|
| **Default** (count ≥ 1) | "Flagged" + pill "3" | "Flagged, 3 to review, link" (wording varies by screen reader) |
| **Empty** (count 0) | "Flagged" only, no pill | "Flagged, link" |
| **Current page** (on Flagged notes) | The nav's current-item style, which must not rely on colour alone (for example bold and an underline bar; owned by the nav component). Pill unchanged. | "Flagged, 3 to review, current page, link" |
| **Hover** (laptop) | The link's hover style (for example a thicker underline). Pill unchanged. | n/a |
| **Focus** (keyboard) | One focus outline around the **whole link, including the pill**: 3 px solid, at least 3:1 against the background, 2 px offset. Never removed (A32). | Name read once on focus |
| **Active** (pressed) | The link's pressed style. Pill unchanged. | n/a |
| **Loading (first load)** | The app shell waits for `me` (it needs the role), so the nav and badge appear together. There is no pill placeholder, no "0" and no skeleton. | Nothing |
| **Refreshing** | The previous number stays until the new one arrives. No spinner, and no `aria-busy`. | Nothing |
| **Error on refresh** | Keep the last known number. No "!" or "?" in the pill. A `401` is handled by the app-wide sign-in path (8.5). The Flagged screen's list is authoritative. | Nothing |
| **Disabled / read-only** | Not applicable: nav links are never disabled. | n/a |
| **Worker** | No Flagged item at all (4.0). | n/a |

### Phone vs laptop

- **Same component and same proportions on both.** All sizes are in `em`, so the pill scales with the user's text size (200%, A32).
- **Phone:** the whole link is at least 44 × 44 px (A32; WCAG 2.5.8 needs 24 × 24). The pill sits inline after "Flagged", never overlapping a neighbour. Flagged must be **visible without opening anything** at 320 px width, because 4.6 says the count "shows on the Flagged menu item on every manager screen". If the nav component ever collapses Flagged behind a menu button, that button must carry the same count text. That is a dependency on the navigation component, not an addition.
- **Laptop:** identical, plus the hover state.

### Colour

- Use a **strong neutral pill, not red** **[Opinion]**, based on GOV.UK and AgDS (above) and on invariant I11 ("do not spend an attention channel already carrying a safety or data signal"). In Grow2Notes, red will carry errors, including the "not saved" banner (3.4) and validation messages. A flag is a request to read, not an error. The badge is on screen all day on every manager screen, so a red pill would make red routine and weaken it for errors. The number gives the signal, not the colour (SC 1.4.1).
- Tokens: `--badge-bg` and `--badge-fg`. On a light nav, use near-black fill with white digits. On a dark nav, use a white fill with near-black digits (as AgDS adapts). The digits must reach **at least 4.5:1** against the fill (SC 1.4.3; the digits are small bold text, not "large text"). Aim for the pill to reach 3:1 against the nav background, although SC 1.4.11 does not require it.
- The pill must look different from the Today and history "Flagged" *tag*: the tag is a word on one note, the badge is a number in the nav. Do not reuse the tag component or its colour for the badge.

### Exact copy

| Where | Text |
|---|---|
| Visible label | `Flagged` (design.md 4.0) |
| Visible pill | digits only, for example `3`, `12`, `104` |
| Visually hidden, inside the link | `, 3 to review` (the term from 4.6 "To review") |
| Flagged screen tab (owned by the tabs component, must agree) | `To review (3)` |
| Empty Flagged screen (owned by 4.6) | `No flagged notes to review.` |

### Accessibility summary

- **Semantics:** a native `<a href>` in `<nav aria-label="Main">` (label owned by the nav component). The digit is `aria-hidden="true"`, and the visually hidden text supplies the meaning. The only ARIA is `aria-current="page"` (added by `NavLink`) and `aria-hidden` on the digit. No `aria-label`, no `aria-live`, no `role="status"`, no `title` tooltip.
- **Keyboard:** one Tab stop, the link. Enter follows it. The focus outline wraps label and pill together.
- **Screen readers:** the count is read when the user reaches the nav link. Nothing is announced when it changes. After Mark reviewed, the review panel's own confirmation is what gets announced (owned by that component), not the badge.
- **WCAG 2.2 criteria met:** 1.3.1 Info and Relationships (the count sits inside the link it describes); 1.4.1 Use of Color; 1.4.3 Contrast (Minimum); 1.4.4 Resize Text; 1.4.10 Reflow; 1.4.11 Non-text Contrast (focus indicator); 1.4.12 Text Spacing (no fixed heights); 2.1.1 Keyboard; 2.2.2 Pause, Stop, Hide (no animation); 2.3.1 Three Flashes; 2.4.4 Link Purpose; 2.4.7 Focus Visible; 2.5.3 Label in Name; 2.5.8 Target Size (Minimum); 3.2.3 Consistent Navigation and 3.2.4 Consistent Identification (same place, same look on every manager screen); 4.1.2 Name, Role, Value. 4.1.3 Status Messages is not triggered, by design.

### Implementation notes (React 19 + native HTML + CSS Modules)

No React Aria is needed. A link, a span and a visually hidden span are all native.

```tsx
// src/shell/FlaggedNavLink.tsx
import { NavLink } from 'react-router';
import styles from './FlaggedNavLink.module.css';
import a11y from '../styles/a11y.module.css';

export function FlaggedNavLink({ toReviewCount }: { toReviewCount: number }) {
  return (
    <NavLink to="/flagged" className={styles.link}>
      Flagged
      {toReviewCount > 0 && (
        <>
          <span className={styles.badge} aria-hidden="true">{toReviewCount}</span>
          <span className={a11y.visuallyHidden}>{`, ${toReviewCount} to review`}</span>
        </>
      )}
    </NavLink>
  );
}
```

```css
/* FlaggedNavLink.module.css: badge parts only; the nav component owns the link's layout */
.link {
  display: inline-flex;
  align-items: center;
  gap: 0.5em;
  min-block-size: 44px;          /* A32 */
  min-inline-size: 44px;
  white-space: nowrap;           /* keep "Flagged" and the pill together */
}
.link:focus-visible {
  outline: 3px solid var(--colour-focus);
  outline-offset: 2px;
}
.badge {
  display: inline-block;         /* inline-block: the link's underline is not drawn through the pill */
  min-inline-size: 1.6em;        /* a single digit is a circle */
  padding-inline: 0.45em;
  border-radius: 999px;
  background: var(--badge-bg);
  color: var(--badge-fg);
  font-size: 0.875em;
  font-weight: 700;
  line-height: 1.6;
  text-align: center;
  font-variant-numeric: tabular-nums;
  /* no transition, no animation */
}
@media (forced-colors: active) {
  .badge { border: 1px solid currentColor; }  /* fill is overridden; keep the shape */
}
```

```css
/* styles/a11y.module.css */
.visuallyHidden {
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
  block-size: 1px;
  inline-size: 1px;
  overflow: hidden;
  position: absolute;
  white-space: nowrap;
}
```

```ts
// src/session/useSessionRefresh.ts: called once in the manager and worker app shell
import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';

const ME = ['me'] as const;

export function useSessionRefresh() {
  const queryClient = useQueryClient();
  const { pathname } = useLocation();
  const refresh = () =>
    void queryClient.invalidateQueries({ queryKey: ME }, { cancelRefetch: false }); // reuse an in-flight request

  // "when a page loads": each screen opened in the SPA
  useEffect(refresh, [pathname]);

  // "when the window regains focus": TanStack already handles visibilitychange; add focus
  useEffect(() => {
    window.addEventListener('focus', refresh);
    return () => window.removeEventListener('focus', refresh);
  }, []);
}

// In the Mark reviewed mutation (owned by the review panel):
// onSuccess: () => {
//   queryClient.invalidateQueries({ queryKey: ['reviews'] });
//   queryClient.invalidateQueries({ queryKey: ME });
// }
```

- Keep `refetchOnWindowFocus` at its default (`true`) and `staleTime` at `0` for the `me` query. Do not add `refetchInterval`.
- The route path `/flagged` is illustrative. Use whatever the router defines. Do not pass `end` if the Reviewed tab is a child route, so the item stays current on both tabs.
- **Test:** with NVDA + Chrome and VoiceOver + Safari (iOS), check that the link reads "Flagged, n to review" as one item and is read only once. Check that the count is the same as "To review (n)" after Mark reviewed. Check that it disappears at 0. In Windows forced colours, the pill outline should show. At 320 px and 200% text, there should be no overlap or horizontal scroll.

---

## Per-screen notes

**App shell (4.0)**
- Render `FlaggedNavLink` only when `me.role === 'Manager'`. A role change applies on the next request (6.6), so the next `me` refetch removes or adds the item.
- Same position in the nav on every manager screen (SC 3.2.3). The badge is never duplicated elsewhere in the shell: not in the account menu, the page title or the favicon.

**Today (4.2)**
- This is where a manager first sees new flags after signing in, so the badge must be visible at 320 px without opening anything.
- Today has "no counts" (D25, A17). The badge stays in the nav. Do not echo it in the Today header, as a banner ("3 flagged notes") or on participant rows.
- Keep the badge visually distinct from the row-level "Flagged" tag (see Colour).

**Flagged notes (4.6)**
- The nav item is the current page (`aria-current="page"`). Keep showing the badge so the nav looks the same on every screen.
- Two places show the same queue size: the badge (from `me`) and the tab "To review (n)" (from the list). Both refresh when this screen opens, and both are invalidated after Mark reviewed. If they differ for a moment, the list is authoritative. Do not compute one from the other.
- The tab's "(n)" stays plain text in brackets, as written in 4.6. Do not render it as a second pill. MoJ advises reserving the badge for navigation.
- When the last flag is reviewed, the badge disappears silently and the screen shows "No flagged notes to review."

---

## Anti-patterns to avoid

- `aria-live`, `role="status"` or `role="alert"` on the badge or its wrapper. Each refresh would interrupt managers for no benefit.
- A bare number as the accessible name, for example "Flagged 3, link", with no context. Also avoid an `aria-label` that holds the content, because it does not reliably translate.
- The digit announced twice ("3, 3 to review"). Keep `aria-hidden="true"` on the visible digit.
- The badge as its own focusable or clickable element, or placed outside the link.
- ARIA `role="menu"`/`menuitem` for the nav, because design.md says "menu item". Use plain links.
- Red or error-colour fill. Colour as the only cue for "current page".
- Pulse, bounce, count-up or flash animation when the number changes.
- Showing "0", or a dot with no number.
- Placeholders while loading: "0", skeleton, spinner. Also "!" or "?" on a failed refresh.
- `setInterval` polling, `refetchInterval`, Server-Sent Events, WebSockets or Web Push (6.8: "never polls"; 4.6: "no push").
- Caching the count in `localStorage` or `sessionStorage` to show it faster (D22: nothing on the device).
- Copying the count into `document.title`, the favicon or the app-icon badge (`navigator.setAppBadge`). Titles are generic (4.0), and these are features nobody asked for.
- A `title` tooltip explaining the badge. Touch users never see it.
- `display: none` on the hidden text. Use the clip technique.
- Hiding Flagged behind a menu button on phones without carrying the count.

---

## Tensions with decisions

None found. Best practice agrees with the design here: a nav-only count refreshed on page load (MoJ "It's not 'dynamic'"), no push, no announcement, and hidden at zero. Colour, count cap and accessible text are build choices the design leaves open, so they are recommended above rather than raised as tensions.

---

## Sources

- NN/g, UI elements glossary (Badge): https://www.nngroup.com/articles/ui-elements-glossary/
- NN/g, Flaherty (January 2024), Indicators, Validations, and Notifications: https://www.nngroup.com/articles/indicators-validations-notifications/
- Ministry of Justice Design System, Notification badge (official, updated October 2025): https://design-patterns.service.justice.gov.uk/components/notification-badge/
- Agriculture Design System (Australian Government), Notification badge: https://design-system.agriculture.gov.au/components/notification-badge
- GOV.UK Design System, Colour: https://design-system.service.gov.uk/styles/colour/
- MUI, Badge (accessibility, max, zero): https://mui.com/material-ui/react-badge/
- Material Components for Android, `BadgeDrawable`: https://developer.android.com/reference/com/google/android/material/badge/BadgeDrawable
- Primer, CounterLabel and its accessibility page: https://primer.style/product/components/counter-label/ · https://primer.style/product/components/counter-label/accessibility/
- Apple HIG, Tab bars (could not be fetched; badge wording unverified): https://developer.apple.com/design/human-interface-guidelines/tab-bars
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.11 Non-text Contrast: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 2.2.2 Pause, Stop, Hide: https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html
- WAI-ARIA 1.2, `aria-current`: https://www.w3.org/TR/wai-aria-1.2/#aria-current
- MDN, `forced-colors`: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors
- A11Y Project, How to hide content: https://www.a11yproject.com/posts/how-to-hide-content/
- Adrian Roselli, aria-label Does Not Translate (2019, updated 2025): https://adrianroselli.com/2019/11/aria-label-does-not-translate.html
- Scott O'Hara, Are we live? (2022): https://www.scottohara.me/blog/2022/02/05/are-we-live.html
- React Router, NavLink: https://reactrouter.com/api/components/NavLink
- TanStack Query v5, Window focus refetching and Migrating to v5: https://tanstack.com/query/v5/docs/framework/react/guides/window-focus-refetching · https://tanstack.com/query/v5/docs/framework/react/guides/migrating-to-v5
