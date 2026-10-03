# Tabs and Active/Archived switches

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `tabs-segmented`. This covers the two places where a screen switches between two views of one list: **To review (n) | Reviewed** on Flagged notes (design.md 4.6) and the **Active / Archived** toggle on Manage > Participants (4.8). It also records why the participant detail screen (4.8) has no switch. Nothing here adds a screen, view, count, setting or data item. It describes how to build what the design already specifies.

Evidence grades: **[Research]** usability testing or studies (including NN/g and Baymard write-ups) · **[Standard]** WCAG 2.2, WAI-ARIA, HTML · **[Convention]** established design systems and library documentation · **[Opinion]** reasoned judgement with no direct evidence.

---

## Where it's used

| Screen (design.md) | What the switch does | What differs |
|---|---|---|
| **Flagged notes, 4.6** (managers, laptop and phone) | Switches between two lists: **To review (n)**, oldest first, returned whole; and **Reviewed**, newest first, 30 at a time with "Show older". Opening a row shows the read view with the review panel. | The first view's label carries a **count** "(n)". Each view comes from its **own API call** (`GET /api/reviews?status=toReview` and `?status=reviewed&before=`, 6.4), so the Reviewed view has to load when it is first shown. The manager comes back from the read view and expects to land on the same view. The nav item **Flagged** stays current on both views (notification-badge). |
| **Manage > Participants list, 4.8** (managers, mostly laptop) | Switches the list between **Active** and **Archived** participants. Search ("Find a participant") filters whichever view is shown and keeps its text when the view changes (search-filter). | **No count.** Both views come from one call (`GET /api/admin/participants?includeArchived=true`, 6.6), so switching is instant. "Add participant" stays on the screen in both views. Tapping an archived name opens the detail page with its read-only banner and **Restore** (4.8). |
| **Participant detail, 4.8** (goals, Write past-day note) | **Not used.** Details, Goals and Actions are stacked sections on one page. Archived goals are in a **collapsed section** with Restore (4.8), not behind a switch. | Listed here so that nobody turns the detail page into tabs, or swaps the collapsed archived-goals section for an Active / Archived switch. |

"Tabs" in 4.6 and "toggle" in 4.8 describe what the user sees: two named views and one current. Neither word means ARIA `role="tab"` or `role="switch"`. Both screens use **one component** (below).

---

## Best practice

### Tabs, sub-navigation, segmented control, switch: which is which

- **ARIA tabs are for layered panels on one page.** The APG definition is "a set of layered sections of content, known as tab panels, that display one panel of content at a time". A tab list is a single Tab stop and the arrow keys move between tabs. **[Standard]** https://www.w3.org/WAI/ARIA/apg/patterns/tabs/
- **The APG makes automatic activation depend on speed.** "It is recommended that tabs activate automatically when they receive focus as long as their associated tab panels are displayed without noticeable latency." The Reviewed view needs a network call, so ARIA tabs here would need manual activation, which means an extra key press for every switch. **[Standard]** https://www.w3.org/WAI/ARIA/apg/patterns/tabs/
- **Government design systems say not to use the tabs component to move between URLs.**
  - GOV.UK: "do not use the tabs component as a form of page navigation." **[Convention]** https://design-system.service.gov.uk/components/tabs/
  - NHS uses the same wording. **[Convention]** https://service-manual.nhs.uk/design-system/components/tabs
  - Australian Government AgDS: tabs "should not serve as links that navigate users to new URLs", and "don't use for page navigation - use the Sub nav component instead". **[Convention]** https://design-system.agriculture.gov.au/components/tabs
- **The tab-like pattern made of links is called sub-navigation.**
  - MoJ: "Use this component when you have a second level of navigation." Its markup is a `<nav aria-label>` holding a `<ul>` of plain links, and the current one has `aria-current="page"`. MoJ also allows a count on an item. **[Convention]** https://design-patterns.service.justice.gov.uk/components/sub-navigation/
  - AgDS Sub nav is "A horizontal list of links typically placed between the main navigation and page content". **[Convention]** https://design-system.agriculture.gov.au/components/sub-nav
- **Primer (GitHub) splits the same look into two components, and the dividing line is the URL.** UnderlineNav "provides navigation links to let users switch between 2 or more related views without leaving their current context". UnderlinePanels says: "If you want to use this pattern for tabs that change the URL when activated, use the UnderlineNav component instead." **[Convention]** https://primer.style/product/components/underline-nav/ · https://primer.style/product/components/underline-panels/
- **NN/g separates in-page tabs from navigation tabs, and warns against mixing them.** "Mixing in-page and navigation tabs within one tab control will disorient users." **[Research]** (NN/g guidance article, Sunwall 2024, updating Nielsen 2007; not a controlled study) https://www.nngroup.com/articles/tabs-used-right/
- **ARIA tabs have had real usability problems in testing.** In Simply Accessible's testing ("Danger! ARIA tabs", Jeff Smith, April 2016):
  - Sighted keyboard users were not aware of the arrow-key model and were frustrated that Tab no longer reached each tab.
  - Links given `role="tab"` dropped out of screen readers' links lists.

  **[Research]** (small practitioner test; the original page could not be fetched, so the participant numbers are **unverified**. Findings are taken from search excerpts and from the response below.) https://simplyaccessible.com/article/danger-aria-tabs/
- **Experts dispute the remedy, not the findings.** Alastair Campbell accepts that "people are currently having issues with ARIA tabs" but argues against dropping them everywhere. He traces the problem to "over a decade of no differentiation between navigation widgets and tabs, so there is a very real expectation-gap". **[Opinion]** (expert practitioner) https://alastairc.uk/2016/05/aria-tabs-ui-problems-and-standards/
- **A segmented control is a set of buttons that select a state or view.**
  - Apple: "a linear set of two or more segments, each of which functions as a button". Use it "to provide closely related choices that affect an object, state, or view". "In general, keep segment size consistent". "A segmented control that displays text labels doesn't need introductory text." **[Convention]** https://developer.apple.com/design/human-interface-guidelines/segmented-controls
  - Material's segmented buttons "help people select options, switch views, or sort elements", for 2 to 5 options, and mark the selected segment with a checkmark. **[Convention]** (as documented in Flutter's Material library) https://api.flutter.dev/flutter/material/SegmentedButton-class.html
  - Neither system ties the control to a URL.
- **A toggle switch is for a setting, not a view.** NN/g: "Toggle switches are best used for changing the state of system functionalities and preferences." The article shows how two-sided toggles leave users unsure which state is current. **[Research]** (NN/g guidance article, Kendrick 2018) https://www.nngroup.com/articles/toggle-switch-guidelines/
- **React Aria can render tabs as links.** `Tab` takes `href` and renders an `<a>`, and `keyboardActivation` can be `"manual"`. It keeps the tab keyboard model, so it is the "mixing" case above. **[Convention]** https://react-aria.adobe.com/Tabs

### When two views behind tabs are acceptable at all

- GOV.UK: tabs suit content where "the first section is more relevant than the others for most users", and "can work well for people who use a service regularly, for example, users of a caseworking system". NHS says the same about "staff using a patient record system". Both fit here:
  - To review and Active are the main tasks.
  - Reviewed and Archived are for occasional look-ups.
  - Managers use the app every day.

  **[Convention]** https://design-system.service.gov.uk/components/tabs/ · https://service-manual.nhs.uk/design-system/components/tabs
- GOV.UK warns: "Tabs hide content from users and not everyone will notice them or understand how they work." GOV.UK and NHS both say more research is needed, including on small screens. The hidden view should therefore be the secondary one, and its label must look clickable. **[Convention]** (same sources)
- Avoid tabs where users must compare across them or read everything in order (GOV.UK, NHS; NSW "Don't … use for content where users are likely to want to compare information across multiple tabs"). **[Convention]** https://designsystem.nsw.gov.au/components/tabs/index.html

### URL state and the Back button

- "Users expect the Back button to take them back to what they perceived to be their previous page." Baymard recommends "use the history.pushState() to create a new entry in the user's browser history for any view that the user will perceive as a new page". Filtering and sorting were one of the four failure areas, with 27% of benchmarked sites failing. **[Research]** (Baymard large-scale usability testing, 2020) https://baymard.com/blog/back-button-expectations
- GOV.UK's tabs write the current tab into the URL fragment ("the URL gets updated with a fragment"), so a tab survives reload and can be linked to. **[Convention]** https://design-system.service.gov.uk/components/tabs/
- React Router documents the exact mechanism with a tab example: `<Link to="?tab=one" preventScrollReset />`. `preventScrollReset` "Prevents the scroll position from being reset to the top of the window when the link is clicked". AgDS Sub nav likewise says to "Keep users in the same scroll position when navigating between items". **[Convention]** https://reactrouter.com/api/components/Link · https://design-system.agriculture.gov.au/components/sub-nav
- React Router's `NavLink` "automatically applies `aria-current="page"`". Its documented matching is on the **pathname** (`end` controls how much). Query strings are not described as part of the match, so two links that differ only in `?view=` cannot both rely on `NavLink`'s current state. **[Convention]** https://reactrouter.com/api/components/NavLink

### Current state, counts and labels

- `aria-current="page"` "Represents the current page within a set of pages". MDN: "Don't use `aria-current` as a substitute for `aria-selected` in … `tab`". Links use `aria-current`, and tabs use `aria-selected`. Never mix them. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current · https://www.w3.org/TR/wai-aria-1.2/#aria-current
- The `<nav>` element is "a section with navigation links". "Not all groups of links on a page need to be in a `nav` element". It is meant for major navigation blocks. **[Standard]** https://html.spec.whatwg.org/multipage/sections.html#the-nav-element
- Safari and VoiceOver remove list semantics from `list-style: none` lists, **except** inside `<nav>`: "if a list is a descendant of a `<nav>` element, then even if the list styles are removed, Safari/VoiceOver will expose this as a list". **[Research]** (practitioner testing, updated January 2023) https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- The selected state must be visible without colour: SC 1.4.1. The visual cue that identifies it needs 3:1 against adjacent colours: "visual information necessary to indicate state, such as whether a component is selected or focused must also ensure … 3:1". **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- NN/g: "Prominently highlight the selected tab". "Unselected tabs should be visible to remind users of the additional options". "Tab labels should usually be 1-2 words". **[Research]** (NN/g guidance) https://www.nngroup.com/articles/tabs-used-right/
- Counts in tab or sub-nav labels are a convention. Examples are Primer counters on UnderlineNav items, MoJ badges in sub-navigation, and AgDS "Messages (1)". Primer advises waiting until the count is ready, "so you can avoid multiple layout shifts". **[Convention]** https://primer.style/product/components/underline-nav/ · https://design-system.agriculture.gov.au/components/notification-badge
- Do not disable a view. AgDS: "don't disable tab buttons". NSW: "Don't … display disabled tabs". GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible." **[Convention]** https://design-system.agriculture.gov.au/components/tabs · https://designsystem.nsw.gov.au/components/tabs/index.html · https://design-system.service.gov.uk/components/button/

### Focus and announcements

- Changing what a page shows is not by itself a change of context. "Changes in content, such as an expanding outline, dynamic menu, or a tab control do not necessarily change the context, unless they also change one of the above (e.g., focus)." **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/change-on-request.html
- The best-known client-side routing study (Gatsby, 2019, five sessions with NVDA, JAWS, magnification, Dragon and switch users) covers moving focus on full **route** changes. It does not cover switching views inside one screen. **[Research]** https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/

---

## Recommendation for Grow2Notes

**One component, `ViewLinks`, used on both screens: two native links in a `<nav>`, styled like tabs, with the view held in the URL.** It is **not** ARIA tabs, not a segmented button group and not a switch. **[Opinion]**, based on the evidence above:

- Each view is its own list from its own query.
- Each view must survive Back, reload and returning from a note.
- GOV.UK, NHS, AgDS, MoJ and Primer all point to links for views that change the URL.
- Links need no keyboard model to build or test, and every manager already knows how to use one.

### Anatomy

```
┌───────────────────────────────────────────────┐
│  To review (3)          Reviewed              │  ← <nav aria-labelledby="h1"> <ul> two <li><a>
│ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀                               │  ← current: bold, body-text colour, 4px bar
└───────────────────────────────────────────────┘     other: link colour, underlined
──────────────────────────────────────────────────    1px strip line under both
  list for the current view …
```

- **Container:** `<nav aria-labelledby={h1Id}>`, which takes its name from the screen's visible `<h1>` ("Flagged notes", "Participants"). Screen readers announce, for example, "Flagged notes, navigation". **[Convention]** MoJ and Primer use a named nav landmark.
- **List:** `<ul>` with two `<li>`, so screen readers say "list, 2 items" (the job "tab 1 of 2" would otherwise do). It is inside `<nav>`, so Safari keeps the list semantics without `role="list"`.
- **Items:** native `<a href>` (React Router `<Link>`), **default view first**. The current one has `aria-current="page"`. That attribute is the only ARIA used.
- **No** `role="tablist"`, `tab`, `tabpanel`, `switch`, `radiogroup` or `aria-selected`, and no `aria-pressed` buttons.

### Behaviour

1. **The view is in the URL as a fixed keyword.**

   | Screen | Default (no parameter) | Other view |
   |---|---|---|
   | Flagged notes | `/flagged` = To review | `/flagged?view=reviewed` |
   | Participants | `/manage/participants` = Active | `/manage/participants?view=archived` |

   The paths are illustrative; the router defines them. `view` only ever holds `reviewed` or `archived`, never typed text or a name (4.0). Any other value shows the default view, and the URL is corrected with `replace`. **[Opinion]**
2. **Switching is an ordinary link navigation.** It pushes a history entry, so **Back** returns to the previous view (Baymard), and so does coming back from a note's read view or a participant's detail page. **[Research]** It does not use `replace`, which would make Back skip the view the user was on.
3. **Scroll stays put.** Pass `preventScrollReset` so the switch does not jump if the app uses `<ScrollRestoration>`. Without it, React Router does not reset scroll anyway. **[Convention]** (React Router; AgDS Sub nav)
4. **Focus stays on the link the user activated.** The links are the same DOM nodes before and after, so React keeps focus on them. The user moves on with Tab, or a screen-reader user reads on, into the new list. Nothing moves focus and nothing is announced (see Accessibility). **[Opinion]**: the change is the direct, expected result of the user's own activation, and WCAG does not treat it as a change of context. The app shell's route-change focus handling (owned by navigation) must react to **`pathname` only**, not to `search`, so that switching views does not throw focus to the top of the page. **[Opinion]**
5. **Search text is kept** when the view changes on Participants, because the screen stays mounted (search-filter). It never goes into the URL.
6. **Data:**
   - **Flagged:** the To review query runs in **both** views, because it supplies the count. The Reviewed query runs only when the Reviewed view is shown, then stays in TanStack Query's memory cache. Do **not** use `placeholderData: keepPreviousData` across the two views, because it would show To review rows under the "Reviewed" label while loading.
   - **Participants:** one query, `includeArchived=true`, filtered on `status` in the browser. Switching is then instant and never shows a loading state. **[Opinion]**: the API returns the whole list either way (6.1, 6.6).
   - Nothing is stored on the device: no remembered view in `localStorage` or `sessionStorage` (D22). The URL is the only memory.

### States

| State | Visual | Assistive tech |
|---|---|---|
| **Default / current item** | Body-text colour, **bold**, no underline, 4px bar under it in the text colour (≥ 3:1 against the background). The weight and the bar are non-colour cues (SC 1.4.1, 1.4.11). | "To review (3), current page, link" (wording varies by screen reader) |
| **Other item** | Link colour, underlined, regular weight. It looks clickable because it is a link. **[Opinion]**, after GOV.UK tabs, where unselected tabs are underlined links. | "Reviewed, link" |
| **Hover** (laptop, other item) | Underline thickens to 3px, as GOV.UK links do. The current item does not change on hover. | n/a |
| **Focus** (keyboard) | 3px solid outline in the focus colour (≥ 3:1), 2px offset, never removed (A32). No ancestor has `overflow: hidden`, so the outline is never clipped. | Name read on focus |
| **Active** (pressed) | Pressed background token on the item. No animation. | n/a |
| **Disabled** | **Never.** An empty view is still reachable and shows its empty message (AgDS, NSW, GOV.UK). | n/a |
| **Loading, count unknown** (Flagged, first load) | "To review" with no brackets. Never "(0)", never a spinner or skeleton in the label. The items have equal widths, so adding "(3)" later moves nothing. | Name without the count |
| **Loading, view content** | The switch is already fully rendered. The list area below shows the list's own loading state (owned by the list). Participants never reach this state on a switch. | List's own handling |
| **Count refreshing** | The last number stays until the new one arrives (TanStack keeps `data` during a refetch). | Nothing announced |
| **Error** | The switch never shows an error and stays usable. If the To review list fails and has never loaded, the label is "To review" with no count. If it loaded before, the last known count stays. The error message belongs to the list area. | List's own error handling |
| **Empty view** | The switch is unchanged. The list area shows the empty message (Exact copy). | Read when the user reaches it |
| **Zero to review** | "To review (0)". The design writes "(n)" with no exception, and the zero confirms the empty message. **[Opinion]** The nav badge is hidden at zero, but that is a different component with a different job. | "To review (0), current page, link" |
| **Read-only** | Not applicable. Workers never see either screen. | n/a |

### Phone vs laptop

- **Phone (from 320px):** the strip spans the content width, and the two items share it equally (`flex: 1 1 0`). Each item is far wider than 44px and at least 44px tall (A32; SC 2.5.8 needs 24px). The labels are short and never truncate. At 200% text the items **wrap onto two rows** rather than scroll sideways. Each item keeps its own bar and weight, so the current one is still clear (SC 1.4.10, 1.4.4). **[Opinion]**
- **Laptop:** the same component, left-aligned, with each item a fixed equal width (about 12em) rather than stretched across a wide screen. NN/g: put the tab list directly above what it controls. **[Research]** (NN/g guidance)
- **Equal widths everywhere** (Apple: "keep segment size consistent"). This also means the late-arriving count and the bold current item never shift the other link. **[Convention]**
- **Not sticky**, with no horizontal-scroll strip and no "More" overflow menu. Two items do not need any of these, and a sticky bar can hide focused rows (SC 2.4.11).

### Exact copy

| Where | Text | Source |
|---|---|---|
| Flagged, first view | `To review (3)`: plain text, number in round brackets, no pill | 4.6 |
| Flagged, first view before the count is known | `To review` | this document |
| Flagged, second view | `Reviewed`, no count | 4.6 |
| Participants, first view | `Active`, no count | 4.8 |
| Participants, second view | `Archived`, no count | 4.8 |
| Nav landmark name | The screen's `<h1>` through `aria-labelledby`, for example "Flagged notes" or "Participants". No separate string. | this document |
| Introductory text before the links ("Show:", "View:") | **None.** Apple: text labels need no introductory text. | this document |
| Empty, To review | `No flagged notes to review.` | 4.6 |
| Empty, Reviewed | **Not in design.md.** Proposed: `No flagged notes have been reviewed yet.` (owner to confirm) | gap |
| Empty, no participants at all (either view) | `No participants yet.` (managers also see "Add participant") | 4.2 / 4.8, as applied in search-filter |
| Empty, Archived (when active ones exist) | **Not in design.md.** Proposed: `No archived participants.` (owner to confirm) | gap |
| Empty, Active (when every participant is archived) | **Not in design.md.** Proposed: `No active participants.` (owner to confirm) | gap |

The labels follow one word per concept. "To review" and "Reviewed" are also the status words on note rows for managers (4.4) and the API terms. "Archived" is used only for participants, goals and common items, never for users (status-tags).

### Accessibility

- **Semantics:** native `<nav>`, `<ul>`, `<li>` and `<a href>`. The only ARIA is `aria-current="page"` on the current link and `aria-labelledby` on the nav. There are no `role` attributes, no `aria-live`, no `aria-label` text to translate, and no `title` tooltips.
- **Keyboard:** two ordinary Tab stops in visual order, followed by the list. Enter follows the link (native). There are **no arrow-key handlers** and no roving `tabindex`. This avoids the arrow-key discoverability problem found in ARIA tab testing. **[Research]** (Simply Accessible, details unverified)
- **Screen readers:** on arrival, "Flagged notes, navigation, list, 2 items, To review (3), current page, link". After the user activates "Reviewed", focus stays on it and the list below has changed. There is **no live announcement**. SC 4.1.3 is not triggered, because this is new content the user asked for, not a status message. The count changes silently, as the badge does.
- **Voice control:** "click Reviewed" and "click To review" work, because the accessible name is exactly the visible text (SC 2.5.3).
- **WCAG 2.2 criteria met:**
  - 1.3.1 Info and Relationships (landmark, list, current state)
  - 1.3.2 Meaningful Sequence (switch before the list it controls)
  - 1.4.1 Use of Color (weight and bar)
  - 1.4.3 Contrast (Minimum)
  - 1.4.4 Resize Text
  - 1.4.10 Reflow (wraps, no sideways scroll at 320px)
  - 1.4.11 Non-text Contrast (bar and focus outline ≥ 3:1)
  - 1.4.12 Text Spacing (no fixed heights)
  - 2.1.1 Keyboard
  - 2.4.3 Focus Order
  - 2.4.4 Link Purpose (In Context)
  - 2.4.6 Headings and Labels
  - 2.4.7 Focus Visible
  - 2.4.11 Focus Not Obscured (Minimum) (not sticky)
  - 2.5.3 Label in Name
  - 2.5.8 Target Size (Minimum) (44px)
  - 3.2.1 On Focus (focus alone does nothing)
  - 3.2.3 Consistent Navigation and 3.2.4 Consistent Identification (same component and look on both screens)
  - 4.1.2 Name, Role, Value

### Implementation notes (React 19 + native HTML + CSS Modules)

No React Aria is needed. Its `Tabs` and `ToggleButtonGroup` would add exactly the widget semantics this design avoids.

```tsx
// src/components/ViewLinks.tsx
import { Link, useLocation } from 'react-router';
import styles from './ViewLinks.module.css';

export type ViewOption<V extends string> = { value: V | null; label: string }; // null = default view

export function ViewLinks<V extends string>({ labelledBy, options, current }: {
  labelledBy: string;                    // id of the screen's <h1>
  options: readonly [ViewOption<V>, ViewOption<V>];
  current: V | null;
}) {
  const { pathname } = useLocation();
  return (
    <nav aria-labelledby={labelledBy} className={styles.nav}>
      <ul className={styles.list}>
        {options.map(({ value, label }) => (
          <li key={value ?? 'default'} className={styles.item}>
            <Link
              to={{ pathname, search: value ? `?view=${value}` : '' }}
              preventScrollReset
              aria-current={value === current ? 'page' : undefined}
              className={styles.link}
            >
              {label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
```

```ts
// src/components/useView.ts: reads ?view=, falls back to the default, fixes unknown values
import { useEffect } from 'react';
import { useSearchParams } from 'react-router';

export function useView<V extends string>(allowed: readonly V[]): V | null {
  const [params, setParams] = useSearchParams();
  const raw = params.get('view');
  const valid = raw !== null && (allowed as readonly string[]).includes(raw);
  useEffect(() => {
    if (raw !== null && !valid) setParams({}, { replace: true }); // no other params exist on these screens
  }, [raw, valid, setParams]);
  return valid ? (raw as V) : null;
}
```

```tsx
// Flagged notes screen (sketch)
const h1Id = useId();
const view = useView(['reviewed'] as const);                       // null = To review
const toReview = useQuery({ queryKey: ['reviews', 'toReview'], queryFn: fetchToReview }); // both views: the count
const reviewed = useInfiniteQuery({ /* ['reviews','reviewed'], before cursor */ enabled: view === 'reviewed' });
const n = toReview.data?.length;

<h1 id={h1Id}>Flagged notes</h1>
<ViewLinks labelledBy={h1Id} current={view} options={[
  { value: null, label: n === undefined ? 'To review' : `To review (${n})` },
  { value: 'reviewed', label: 'Reviewed' },
]} />
{view === 'reviewed' ? <ReviewedList query={reviewed} /> : <ToReviewList query={toReview} />}

// Participants list (sketch): one query, filtered in memory
const view = useView(['archived'] as const);                      // null = Active
const all = useQuery({ queryKey: ['admin', 'participants', 'all'], queryFn: () => fetchParticipants({ includeArchived: true }) });
const shown = all.data?.filter(p => (view === 'archived') === (p.status === 'Archived'));
```

```css
/* ViewLinks.module.css */
.list {
  display: flex;
  flex-wrap: wrap;                       /* 200% text: wraps, never scrolls sideways */
  margin: 0;
  padding: 0;
  list-style: none;                      /* list semantics kept: it is inside <nav> */
  border-block-end: 1px solid var(--colour-border);
}
.item { flex: 1 1 0; min-inline-size: 8em; }           /* phone: two equal halves */
@media (min-width: 40em) { .item { flex: 0 0 12em; } }  /* laptop: equal, left-aligned */

.link {
  display: flex;
  align-items: center;
  justify-content: center;
  min-block-size: 44px;                  /* A32 */
  padding: 0.625em 1em;
  margin-block-end: -1px;                /* bar sits on the strip line */
  border-block-end: 4px solid transparent;
  color: var(--colour-link);
  text-decoration: underline;
  text-underline-offset: 0.2em;
  text-align: center;
}
.link:hover { text-decoration-thickness: 3px; }
.link:active { background: var(--colour-pressed); }
.link:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }

.link[aria-current='page'] {             /* style from the ARIA state, so the look and AT never disagree */
  color: var(--colour-text);
  font-weight: 700;
  text-decoration: none;
  border-block-end-color: currentColor;
}
.link[aria-current='page']:hover { text-decoration: none; }

@media (forced-colors: active) {
  .link[aria-current='page'] { border-block-end-color: CanvasText; } /* the bar is a border, so it survives */
}
/* No transition or animation: the bar does not slide (zero-duration change). */
```

- **Do not use `NavLink` for the two view links.** They share a pathname, and `NavLink` matches on pathname, so both would look current. The main nav's **Flagged** item stays a `NavLink` to `/flagged`, and that is exactly why it remains current on both views (notification-badge).
- **Keep `ViewLinks` outside any Suspense boundary or conditional that unmounts while data loads.** Remounting would drop focus to `<body>`. Use the TanStack `isPending` state in the list area, not `useSuspenseQuery` around the whole screen.
- **Key items by view value** (as above), so React reuses the same `<a>` nodes when the view changes.
- **Page title:** one per screen, for example "Grow2Notes – Flagged notes" for both views. It is generic and holds no names (4.0). **[Opinion]**
- **Tests:**
  - Playwright + axe: exactly one `aria-current="page"` inside each `ViewLinks`.
  - Tab reaches both links, and Enter switches view with focus kept on the link.
  - Back returns to the previous view, and reload keeps the view.
  - `?view=nonsense` falls back to the default view and the URL is corrected.
  - No horizontal scroll at 320px with 200% text.
  - The bar is visible in Windows forced colours.
  - Manual pass with NVDA + Chrome and VoiceOver + Safari on iOS: the landmark name, "current page", and that nothing is read twice.

---

## Per-screen notes

**Flagged notes (4.6)**
- Order: `<h1>` "Flagged notes", then `ViewLinks`, then the list. To review is the default and comes first (GOV.UK: the most relevant section first).
- The "(n)" comes from the **To review list**, which is authoritative, not from `me.toReviewCount`. After Mark reviewed, the review panel invalidates `['reviews']` and `me` (notification-badge), so the tab and the badge agree when the manager comes back. If they differ for a moment, the list wins.
- Rows in either view carry no "To review" or "Reviewed" tag, because the current link already names the state (status-tags).
- Coming back from a note's read view with Back lands on the same view, because it is in the URL. On the Reviewed view, the pages already loaded come back from the memory cache. "Show older" is **not** written to the URL. A reload starts again at the newest 30. **[Opinion]**: simple, and the design specifies nothing more.
- On a phone, both labels fit side by side at 320px. At 200% text they wrap onto two rows.
- No count on Reviewed, and no third view (for example "All").

**Manage > Participants list (4.8)**
- Order, as in the design and search-filter: `<h1>` "Participants", "Find a participant", then `ViewLinks` (Active, Archived), then the names, with "Add participant" staying in the same place in both views.
- Switching filters the list already in memory, so there is no loading state and no request. The search text and its filtering carry over. The no-match message follows search-filter's rules.
- Names in the Archived view carry no "Archived" tag, because the view names it. On the detail page, the read-only banner with **Restore** says it (4.8).
- After a manager archives or restores someone and comes back, the participant appears in the other view (the query is invalidated by that action, owned by the detail screen). The switch does not jump to follow them.
- Not a switch (`role="switch"`), and not a "Show archived" checkbox. The design asks for two separate views, not one list with archived participants mixed in.

**Participant detail (4.8): goals and Write past-day note**
- **No tabs and no view switch here.** Details, Goals and Actions stay as stacked sections on one page. GOV.UK says to try headings before tabs, and a manager setting up "a participant with five goals on a phone" (M1) needs to see details, goals and help text together. **[Convention]** / **[Opinion]**
- **Archived goals stay in the collapsed section with Restore**, as 4.8 specifies: a native `<details>`/`<summary>`. Do not replace it with an Active / Archived switch for goals. The active goals are what the manager works on, and the archived ones are a short, rarely opened list on the same page.
- Past notes is a link to 4.4. Write past-day note is a date field. Neither is a tab.

---

## Anti-patterns to avoid

1. **`role="tablist"`, `role="tab"` or `aria-selected` on links that change the URL.** Mixing navigation and tab semantics disorients users (NN/g) and goes against GOV.UK, NHS, AgDS and Primer.
2. **Building full ARIA tabs** (roving `tabindex`, arrow keys, `tabpanel`) for these two screens. This adds a keyboard model that testing found people do not discover, it needs manual activation because of network latency, and it loses Back and reload unless rebuilt by hand.
3. **`aria-current` and `aria-selected` together**, or `aria-current` on a `role="tab"`.
4. **A `role="switch"`, toggle switch or "Show archived" checkbox** for Active / Archived. It is a setting control used for a view (NN/g), and it makes the user work out which state is current.
5. **A segmented control made of `<button aria-pressed>` pairs.** It has no URL, Back does not work, and with two options it is hard to tell which one is on.
6. **A `<select>` with two options** ("Show: Active ▾"). It hides the other view behind a tap.
7. **The current view shown by colour alone**, or by a pale fill that is under 3:1 against the background.
8. **Disabling a view** because it is empty ("Reviewed" or "Archived" greyed out).
9. **Counts on Reviewed, Active or Archived**, or "(n)" drawn as a pill or badge. The design gives a count only on To review, and the badge belongs to the nav.
10. **"To review (0)" as a loading placeholder**, or a spinner or skeleton inside the label.
11. **`replace` navigation** for switching, which breaks Back (Baymard). Equally, **no URL state** at all, so that returning from a note always lands on To review or Active.
12. **Search text, names or IDs in the view parameter**, or any personal data in the URL (4.0).
13. **Remembering the last view in `localStorage` or `sessionStorage`.** Nothing is stored on the device (D22), and the URL already does this job.
14. **Moving focus** to the list, the `<h1>` or the top of the page when the view changes, or a live region announcing "Showing reviewed notes".
15. **`placeholderData: keepPreviousData` across views**, which shows To review rows under the Reviewed label.
16. **A sliding or animated underline, swipe-between-views gestures**, sticky tab bars, horizontal scrolling strips or "More" menus. None is specified, swipes are hidden and fight vertical scrolling, and a sticky bar can hide focused rows (SC 2.4.11).
17. **`NavLink` for the two view links**, because both would be marked current.
18. **Turning the participant detail page into tabs**, or replacing the collapsed archived-goals section with a switch.
19. **Introductory labels** ("Show:", "Filter by status:") or helper text explaining the switch.

---

## Tensions with decisions

None found. design.md's "Tabs" (4.6) and "toggle" (4.8) describe what the user sees, and the recommended links styled as tabs deliver exactly that. Every view, label and count comes from the design, and nothing is added. The research caveat that tabs hide content (GOV.UK) does not apply strongly here: the hidden views (Reviewed, Archived) are the secondary ones, which is the condition GOV.UK gives for using tabs.

---

## Sources

- WAI-ARIA Authoring Practices, Tabs pattern: https://www.w3.org/WAI/ARIA/apg/patterns/tabs/
- WAI-ARIA 1.2, `aria-current`: https://www.w3.org/TR/wai-aria-1.2/#aria-current
- MDN, `aria-current`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current
- HTML Living Standard, the `nav` element: https://html.spec.whatwg.org/multipage/sections.html#the-nav-element
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.11 Non-text Contrast: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 Understanding 3.2.5 Change on Request (definition of change of context): https://www.w3.org/WAI/WCAG22/Understanding/change-on-request.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- GOV.UK Design System, Tabs: https://design-system.service.gov.uk/components/tabs/
- GOV.UK Design System, Button (disabled buttons): https://design-system.service.gov.uk/components/button/
- NHS digital service manual, Tabs: https://service-manual.nhs.uk/design-system/components/tabs
- Agriculture Design System (Australian Government), Tabs: https://design-system.agriculture.gov.au/components/tabs
- Agriculture Design System, Sub nav: https://design-system.agriculture.gov.au/components/sub-nav
- Agriculture Design System, Notification badge ("Messages (1)"): https://design-system.agriculture.gov.au/components/notification-badge
- NSW Design System, Tabs: https://designsystem.nsw.gov.au/components/tabs/index.html
- Ministry of Justice Design System, Sub navigation: https://design-patterns.service.justice.gov.uk/components/sub-navigation/
- Primer, UnderlineNav: https://primer.style/product/components/underline-nav/
- Primer, UnderlinePanels: https://primer.style/product/components/underline-panels/
- Apple Human Interface Guidelines, Segmented controls: https://developer.apple.com/design/human-interface-guidelines/segmented-controls
- Flutter Material library, SegmentedButton (Material 3 guidance as implemented): https://api.flutter.dev/flutter/material/SegmentedButton-class.html
- NN/g, Sunwall (2024, updating Nielsen 2007), Tabs, Used Right: https://www.nngroup.com/articles/tabs-used-right/
- NN/g, Kendrick (2018), Toggle-Switch Guidelines: https://www.nngroup.com/articles/toggle-switch-guidelines/
- Baymard Institute (2020), Back button expectations: https://baymard.com/blog/back-button-expectations
- Simply Accessible, Jeff Smith (2016), Danger! ARIA tabs. The site could not be fetched, so details are unverified: https://simplyaccessible.com/article/danger-aria-tabs/
- Alastair Campbell (2016), ARIA tabs, UI problems and standards: https://alastairc.uk/2016/05/aria-tabs-ui-problems-and-standards/
- Scott O'Hara, "Fixing" Lists (Safari list semantics; January 2023 update on `<nav>`): https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- Gatsby (2019), What we learned from user testing of accessible client-side routing techniques: https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- React Router, Link (`preventScrollReset`, `replace`): https://reactrouter.com/api/components/Link
- React Router, NavLink: https://reactrouter.com/api/components/NavLink
- React Router, useSearchParams: https://reactrouter.com/api/hooks/useSearchParams
- React Aria, Tabs (links, `keyboardActivation`): https://react-aria.adobe.com/Tabs
