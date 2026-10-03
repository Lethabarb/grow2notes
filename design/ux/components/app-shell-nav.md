# App shell and navigation

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Overruled by app-shell.md (editorial pass, 1 October 2026): no `<ScrollRestoration>` (it writes `sessionStorage`; the shell keeps scroll in memory); the Reviewed view is `/flagged?view=reviewed`, not `/flagged/reviewed`; one route-change focus rule with `handle.returnFocus` and `handle.screenKey`; a before-main bar slot for back links; the page-name union and the URL → current-item table live in app-shell.md.

The frame around every signed-in screen: the skip link, the header (the Grow2Notes name, the navigation and the
account menu), the `<main>` region, page titles, and what happens to focus, scroll and the Flagged badge when the
screen changes. It follows design.md §4.0, §4.2, §4.6 (badge), §6.8 (refresh) and §8.5 (sign-out). It adds no
features. Where it needs copy that design.md does not give, the new strings are marked **(new copy)** so the
owner can approve them.

## Where it's used

| Screen | What differs |
|---|---|
| **App shell (global, design.md §4.0)**: every signed-in screen | Workers see **Today** and **Account** (Sign out). Managers also see **Flagged** with its count badge, **Report** and **Manage**. The header is the same on phone and laptop and is never fixed or sticky. Signed-out screens (sign-in, setup, "sign in again" after a timeout) show only the Grow2Notes name, as plain text. |
| **Today (design.md §4.2)**: the home route `/` | **Today** is marked as the current page. The page `<h1>` is "Today · Thursday 1 October". After a submit, the confirmation "Note for Jane Citizen submitted" gets focus instead of the `<h1>`. Managers see the Flagged badge here as they do on every manager screen. |
| Note form, read view, history, version history | The header stays. The note form's own top bar (back to Today, save indicator, §4.3) sits under it, inside `<main>`. No navigation item is marked current, because these screens can be reached from more than one section. |
| Flagged, Report | Their item is marked current. The Report link always points at today's date. |
| Manage and its four setup screens | **Manage** opens a page that holds only the four links (Participants, Common items, Guide prompts, Users). On that page Manage is the current page; on the setup screens below it, Manage is the current section. |

## Best practice

**Phone navigation: visible bar, bottom tab bar or menu?**

- **[Research, weak]** Hidden navigation was used less and was slower in NN/g's 2016 study: on mobile, people used it
  in 57% of cases against 86% for a visible-plus-hidden "combo", and were 15% slower (39% slower on desktop). This is
  one observational study: 179 people, six live commercial sites, and the navigation style was never manipulated, so
  it is confounded with which site was tested. The direction is plausible; the exact percentages are not solid.
  NN/g's 2025 follow-up found the hamburger icon is now widely recognised, but it did not re-measure use or task
  time. https://www.nngroup.com/articles/hamburger-menus/ ·
  https://www.nngroup.com/articles/hamburger-menu-icon-recognizability/
- **[Convention]** Government design systems collapse navigation into a menu button on phones because they carry
  many items. The GOV.UK service navigation collapses into "Menu" on mobile by default, with an option to turn that
  off. The NHS header moves items that do not fit into "More". The AgDS main nav "collapses down to a conventional
  open/close menu button". Grow2Notes has 1 item (workers) or 4 short items (managers), which fit at 360 px.
  https://design-system.service.gov.uk/components/service-navigation/ ·
  https://service-manual.nhs.uk/design-system/components/header ·
  https://design-system.agriculture.gov.au/components/main-nav
- **[Convention]** Bottom tab bars are a native-app convention. Apple: "Use a tab bar to support navigation, not to
  provide actions"; keep it visible; "Don't disable or hide tab bar buttons". Material 3's navigation bar holds 3–5
  destinations and becomes a navigation rail on wider windows. NN/g calls tab bars "specific to apps". GOV.UK, the
  NHS and AgDS all put web navigation at the top, and none of them ships a bottom bar.
  https://developer.apple.com/design/human-interface-guidelines/tab-bars ·
  https://m3.material.io/components/navigation-bar/guidelines ·
  https://www.nngroup.com/articles/mobile-navigation-patterns/
- **[Standard]** Fixed and sticky bars cost the most on exactly the screens we design for. The WCAG Understanding
  document for Reflow says: "It is strongly suggested that at smaller viewport sizes that such components are modified
  to have static positioning". Technique C34 un-fixes sticky headers and footers when space is short. SC 2.4.11 lists
  "sticky footers, sticky headers" as typical causes of a hidden focused item.
  https://www.w3.org/WAI/WCAG22/Understanding/reflow.html ·
  https://www.w3.org/WAI/WCAG22/Techniques/css/C34 ·
  https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **[Standard, browser vendor]** Since Chrome 108 on Android, the on-screen keyboard resizes only the visual
  viewport, so `position: fixed` content at the bottom can be covered by the keyboard. Safari on iOS behaves the
  same way. WebKit warns that a bottom bar beside the home indicator is "very hard to use" unless it is padded with
  `env(safe-area-inset-*)`. A worker types or dictates a long Guided notes entry on a phone, so the keyboard is open
  for most of the session. https://developer.chrome.com/blog/viewport-resize-behavior ·
  https://webkit.org/blog/7929/designing-websites-for-iphone-x/
- **[Research, observational]** Hoober watched 1,333 people in 2013 (780 of them touching the screen): 49% used one
  hand. He suggests larger targets in the corners (about 12 mm) than in the centre (about 7 mm). This is the real
  argument for bottom navigation. The "thumb-zone" heat map built on his data was never measured, so treat it as a
  placement heuristic only.
  https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php
- **[Research, unquantified]** GOV.UK One Login asks services to show its header "at the top of every page in your
  service when a user is signed in", and reports "no evidence that the header distracts users in a service journey".
  AgDS hides its app sidebar in multi-page forms ("focus mode") [Convention]. That rule does not apply here: the note
  is a single page, and design.md §4.6 puts the badge on every manager screen.
  https://www.sign-in.service.gov.uk/documentation/design-recommendations/let-users-navigate-sign-out ·
  https://design-system.agriculture.gov.au/components/app-layout

**Laptop**

- **[Research, weak]** Hidden navigation on desktop did worst in the same NN/g study (27% use against 48% for
  visible). Never use a hamburger on a laptop. https://www.nngroup.com/articles/hamburger-menus/
- **[Convention]** The NHS header and the GOV.UK One Login header use two rows: a top row with the service name and
  the account or sign-out controls, and a navigation row underneath. One Login reports that this layout "clearly
  shows these are 2 different spaces". AgDS's sidebar app layout suits apps with many sections; four items do not
  need it.

**Showing where you are**

- **[Research, expert review]** "Navigation should not only show where you can go but also where you are now", and
  "a signal that may seem distractingly obvious to the designer is often not even noticed". NN/g, Farrell, 2015.
  https://www.nngroup.com/articles/navigation-you-are-here/
- **[Standard]** Use `aria-current="page"` for the current page and `"true"` for the current item in a set, and mark
  only one element in a set. https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current
- **[Convention]** GOV.UK's template sets `aria-current="page"` on the current page and `"true"` on the active section
  when the user is on a page inside it, and wraps the active item in `<strong>` as a fallback.
  https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/service-navigation/template.njk
- **[Standard]** SC 1.4.1 Use of Color: the current item cannot be shown by colour alone.
- **[Convention, library behaviour]** React Router's `NavLink` always writes `aria-current="page"`, and by default it
  also matches child routes. Left as it is, `/manage/users` would announce "Manage, current page", which is wrong.
  https://reactrouter.com/api/components/NavLink

**Home link**

- **[Research]** Use both a clickable logo and an explicit text link home. "Less technical audiences" often do not
  know the logo is a link, and NN/g reports that people are 6 times more likely to reach the homepage in one click
  when the logo is left-aligned than when it is centred. NN/g, Loranger, 2017, reviewed 2024.
  https://www.nngroup.com/articles/homepage-links/

**Skip link, landmarks and headings**

- **[Standard]** SC 2.4.1 Bypass Blocks is met by a skip link (G1), landmarks (ARIA11) or headings (H69).
  https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html
- **[Convention]** GOV.UK skip link: place it straight after `<body>`, not inside the header or nav. The text is
  "Skip to main content". It stays hidden until it receives keyboard focus, and JavaScript moves focus to the target.
  https://design-system.service.gov.uk/components/skip-link/
- **[Research]** WebAIM Screen Reader Survey #10 (December 2023 to January 2024, n = 1,539): 71.6% of respondents
  move through headings first on a long page, and only 3.7% start with landmarks. Still, 31.8% use landmarks often
  or whenever they are available. Get one clear `<h1>` per screen right first; landmarks are still worth having.
  https://webaim.org/projects/screenreadersurvey10/
- **[Standard]** Put all content inside landmarks, use one `main`, give a unique label to any landmark type used more
  than once, and do not put the role name in the label.
  https://www.w3.org/WAI/ARIA/apg/practices/landmark-regions/
- **[Standard]** SC 3.2.3 Consistent Navigation: "Items are considered to be in the same relative order even if
  other items are inserted or removed". Workers having fewer items than managers is fine.
  https://www.w3.org/WAI/WCAG22/Understanding/consistent-navigation.html

**Changing screens in a single-page app**

- **[Research, n = 5]** Gatsby and Fable Tech Labs tested five people (NVDA, JAWS, ZoomText, Dragon, switch).
  Screen-reader users found focusing the heading "the best experience, as it would save time and make it clear what
  happened". Magnifier users had trouble when a full-width wrapper received focus: mobile Chrome scrolled to its
  middle and cut off the text. The final recommendation was to focus a skip link and announce the route in a live
  region. https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- **[Convention]** GOV.UK success banner: `role="alert"`, focus moves to it when the page loads, it sits just before
  the `<h1>`, and it is removed when the user moves to a new page.
  https://design-system.service.gov.uk/components/notification-banner/

**Page titles and privacy**

- **[Standard]** SC 2.4.2: in single-page apps "the title of the page should also be changed dynamically to reflect
  the content or topic of the current view". https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html
- **[Convention]** HMRC's page-title pattern puts the `<h1>` text first and then the service name. It adds "Error: "
  to the start after a failed submit, and when the `<h1>` holds personal information it uses a generic title.
  https://design.tax.service.gov.uk/hmrc-design-patterns/page-title
- **[Standard]** "Some browsers save `state` objects to the user's disk". So router `state` must never carry a
  participant's name or note text (D22). https://developer.mozilla.org/en-US/docs/Web/API/History/pushState
- **[Standard, library behaviour]** React 19 moves a `<title>` rendered anywhere into `<head>`. If two are rendered,
  "the behavior of browsers and search engines is undefined", and the children must be one string.
  https://react.dev/reference/react-dom/components/title

**Account menu**

- **[Standard]** For site navigation, use the APG disclosure pattern: a `<button aria-expanded aria-controls>` and a
  plain list. Escape closes it and returns focus to the button. The APG example "does not use the menu role".
  https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/
- **[Convention]** The NHS header shows user information in its account area and puts "log out" last. AgDS shows the
  user's "name and avatar" top right with a dropdown. GOV.UK One Login shows Sign out on every page.

**Count badge**

- **[Convention]** Apple: "Use badges to indicate critical information is available" and save them for critical
  information. AgDS: place the badge "next to associated content – for example, Messages (1)", at 4.5:1 text
  contrast. https://design-system.agriculture.gov.au/components/notification-badge
- **[Convention, library behaviour]** TanStack Query 5 refetches on `visibilitychange` only by default. To also
  refetch on window `focus`, as design.md §6.8 asks, use `focusManager.setEventListener`.
  https://tanstack.com/query/v5/docs/framework/react/guides/window-focus-refetching

**Targets**

- **[Standard]** SC 2.5.8 Target Size (Minimum) asks for 24 × 24 CSS px. design.md §4.0 asks for 44 × 44. Apple uses
  44 pt and Material 48 dp. https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

## Recommendation for Grow2Notes

### Decision in one line

Use one **static (not sticky) top header**, the same on phone and laptop. All navigation links are visible: no
hamburger, no bottom tab bar, no "More". Workers get one row. Managers get two rows: the name and Account on top,
the four links underneath.

Why: workers have one destination and managers have four short ones. Everything fits visibly, so a hidden menu adds
nothing. A fixed bottom bar on the mobile web brings problems with the keyboard, the safe area, 200% text and
2.4.11, on the very screen where workers spend their time (the note, with the keyboard open). The bottom bar's one
real advantage, thumb reach, matters little for navigation that is used once per screen change. 48 px targets offset
it. [Standard] + [Convention] + [Opinion]

### Anatomy

```
Worker, phone and laptop (one row)
+-----------------------------------------+
| Grow2Notes   Today            Account v |
|              =====                      |
+-----------------------------------------+

Manager, phone (two rows; the nav row wraps if it must)
+-----------------------------------------+
| Grow2Notes                    Account v |
| Today  Flagged [3]  Report  Manage      |
| =====                                   |
+-----------------------------------------+

Manager, laptop (same two rows, inside the page's max-width container)
+---------------------------------------------------------------+
| Grow2Notes                                          Account v |
| Today   Flagged [3]   Report   Manage                         |
|                       ======                                  |
+---------------------------------------------------------------+

Account open (an in-flow panel under the top row, pushing the page down)
+-----------------------------------------+
| Grow2Notes                    Account ^ |
|             Signed in as Alex Pham      |
|                          [ Sign out ]   |
| Today  Flagged [3]  Report  Manage      |
+-----------------------------------------+
```

In DOM order:

1. **Skip link**, the first element in `<body>`: "Skip to main content".
2. **`<header>`** (banner landmark), with an inner container whose max-width and side padding match the page
   container, so the left edges line up.
   - **Wordmark**: the text link "Grow2Notes" to `/` (Today). It is text, not an image.
   - Workers: **nav** (Today), then the **Account** button. Managers: the **Account** button, then the **nav** row.
     In both cases the DOM order equals the visual order.
   - **Account panel**, straight after the Account button's row, `hidden` until opened.
3. **`<nav aria-label="Main">`** holding a `<ul>` of links. Screen readers say "Main navigation".
4. **`<main id="main-content" tabindex="-1">`**: the route outlet. It has no footer, because the design specifies
   none.
5. The **session time-out `<dialog>`** (A24) is mounted once by the shell. Its content belongs to its own component
   spec.

### Copy

| Element | Text | Source |
|---|---|---|
| Wordmark | Grow2Notes | D42 |
| Nav items | Today · Flagged · Report · Manage | design.md §4.0 |
| Badge | the number, plus visually hidden " to review" (screen readers hear "Flagged 3 to review") | §4.6 "To review (n)" |
| Account button | Account | **(new copy)**: §4.0 says "account menu" |
| Account panel | Signed in as {displayName} | **(new copy)**: [Convention], NHS and AgDS. Helps on shared org devices (D21) |
| Sign-out button | Sign out / while working: Signing out… | §4.0; "Signing out…" **(new copy)** |
| Sign-out errors | Not signed out. Your note isn't saved yet: no connection. Keep this page open; retrying. · Not signed out: no connection. Try again. | **(new copy)**, modelled on §4.3 "Not saved: no connection. Keep this page open; retrying." |
| Skip link | Skip to main content | GOV.UK |
| Manage page | `<h1>` Manage; links: Participants · Common items · Guide prompts · Users | §4.0 |
| Shell loading (only after 1 s) | Loading… | **(new copy)** |
| Shell error | Can't connect to Grow2Notes. Check your internet connection, then try again. [Try again] | **(new copy)** |
| Unknown route | `<h1>` Page not found · link "Go to Today" | **(new copy)** |
| Page titles | `Grow2Notes – {Page name}`, with an en dash and spaces, exactly as in §4.0 | §4.0 |

**Page names**, a fixed list typed as a TypeScript union so no name can ever reach a title: Sign in ·
Set up your account · Today · Note · Past notes · Version history · Flagged notes · Daily report · Manage ·
Participants · Participant · Common items · Guide prompts · Users · User · Export record · Page not found. After a
failed submit, the form adds `Error: ` in front ("Error: Grow2Notes – Note") [Convention, GOV.UK/HMRC].

### Behaviour

- **Current item** (one per set; it takes [Standard] `aria-current` plus two non-colour cues: bold and a 4 px bar
  under the item):

  | URL | Item | `aria-current` |
  |---|---|---|
  | `/` | Today | `page` |
  | `/flagged`, `/flagged?view=reviewed` (the screen and its view; editorial pass) | Flagged | `page` |
  | `/reports/daily/{date}` (any date) | Report | `page` |
  | `/manage` | Manage | `page` |
  | `/manage/...` (Participants, Common items, Guide prompts, Users and their detail pages) | Manage | `true` |
  | Note, read view, history, version history, export, not found | none | – |

  Note screens mark nothing, because the same note can be opened from Today, Flagged or Manage, and a guess would
  be wrong some of the time. The URL scheme for notes is shared with the note-form spec.
- **Report link** goes to `/reports/daily/${me.today}`, so the screen opens on today without a redirect entry in
  history (§4.7). `me.today` is refreshed whenever `/api/auth/me` is (below), so it is right after midnight.
- **Manage** links to `/manage`, a page with only the `<h1>` "Manage" and the four links as full-width rows of at
  least 48 px. It has no descriptions and no counts. This is how the "Manage (…)" group in §4.0 is reached on a
  phone and a laptop with one mechanism: no dropdown positioning, no extra keyboard model, and the back button works.
- **Badge** (managers): shown when `toReviewCount > 0`. Nothing is shown at 0 (no "0" and no dot); the Flagged link
  itself always stays, because Apple says not to hide tabs. The badge is never a live region, because a count
  refreshing on focus is not the result of an action and would talk over the page. The count is part of the link's
  name, so screen readers hear it when they reach the link. It is never shown by colour or a dot alone. Use the dark
  brand accent, not red, which this app keeps for errors such as "Not saved" [Opinion].
- **When `/api/auth/me` is refreshed**, which refreshes the badge, `today`, the role and the name:
  - on full page load;
  - on every client-side screen change (the single-page-app meaning of "when a page loads", §4.6);
  - on window `focus` and on `visibilitychange` to visible (§6.8);
  - after a manager marks a note reviewed;
  - after any submit or save that returns a `flagStatus`.

  There is no polling (§8.5).
- **Account menu**: an APG disclosure. Tapping Account toggles the panel. Escape, from the button or from inside the
  panel, closes it and returns focus to Account. Moving to another screen closes it. It needs no outside-click
  handling, because the panel is in the page flow rather than floating over it. It never opens on hover.
- **Sign out**:
  1. Set `aria-disabled="true"` and show "Signing out…". Never use `disabled`, which drops focus to `<body>`.
  2. Wait for any pending autosave to finish (`flushPendingSaves()`, owned by the note-form spec), so nothing
     typed is lost (I9).
  3. `POST /api/auth/logout`. A `401` counts as success.
  4. `window.location.replace('/')`. The full reload clears memory (§8.5), and `replace` keeps the signed-in screen
     out of Back.

  If step 2 or 3 fails, show the error text in the panel's `role="status"` line and put the button back. There is
  no "Are you sure?" dialog: drafts are already on the server, so signing out loses nothing [Convention].
- **Screen change (client-side)**:
  - New screen (push or replace): scroll to the top.
  - Back or forward (pop): restore the scroll position from the shell's in-memory map (app-shell.md). ~~React Router
    `<ScrollRestoration>`~~ is not used: it writes to `sessionStorage` (editorial pass).
  - Then move focus once, to the first of these that exists:
    1. a confirmation or error summary that asks for focus;
    2. the page `<h1>` (`tabindex="-1"`, `width: fit-content` so the focus box hugs the text rather than spanning
       the screen).

    On pop, use `focus({ preventScroll: true })` so the restored scroll position is kept. On the very first page
    load, leave focus where the browser puts it.
  - This follows the n = 5 finding that screen-reader users did best with heading focus. It avoids the full-width
    wrapper problem the magnifier users hit. It also avoids the extra skip link plus live region from the study's
    final compromise, because the focused heading already announces the new screen. [Research] + [Opinion]
- **Skip link**: it is an `<a href="#main-content">`. Its click handler calls `preventDefault()` and focuses
  `<main>`, so `#main-content` never enters the URL or history and React Router does not see a fake navigation.
- **Router state and privacy**: never put names or note text in `navigate(..., { state })`, query strings or titles.
  The "Note for Jane Citizen submitted" message is handed to Today through in-memory React state (or the TanStack
  cache) and cleared on the next screen change.
- **Role guard**: manager routes render "Page not found" for workers. This hides that they exist, matching the API's
  404-not-403 stance (§9.2). If `/me` comes back with a new role, the nav rebuilds at once.
- **Signed out mid-session (401)**: the shell drops the nav and Account, shows the wordmark as plain text, and
  renders sign-in in `<main>` (§8.5). After signing in, it returns to the same route and title.

### States

| Part | Default | Hover (laptop) | Focus | Active (pressed) | Current | Disabled | Loading | Error | Empty |
|---|---|---|---|---|---|---|---|---|---|
| Wordmark | Bold text, no underline | Underline | 3 px outline, 2 px offset | Pressed tint | – | Never | Plain text until `/me` resolves | – | Plain text when signed out |
| Nav link | Text, no underline, min-height 48 px | Underline 2 px | Same outline | Pressed tint | Bold + 4 px bar + `aria-current` | **Never**: items are not disabled or hidden by state (Apple) | Not rendered until the role is known | – | – |
| Badge | Number in a pill, 4.5:1 text | – | (inside the link) | – | – | – | Not shown until `/me` resolves (no fake 0) | Refresh failed: keep the last known count, no error shown | Count 0: not shown |
| Account button | "Account" + a decorative chevron (`aria-hidden`) | Underline | Same outline | Pressed tint | – | Never | Not rendered until `/me` resolves | – | – |
| Sign out | Secondary button, min 48 px | Darker border | Same outline | Pressed tint | – | `aria-disabled` while working | "Signing out…" | Error line in the panel (`role="status"`) | – |
| Skip link | Visually hidden | – | Shown top-left with outline | – | – | – | – | – | – |
| Shell | Header + `<main>` | – | – | – | – | – | Wordmark only; "Loading…" after 1 s (`role="status"`) | Message + Try again | Unknown route: "Page not found" |

Read-only does not apply to navigation.

### Phone and laptop

- The same markup and structure at every width. The header layout needs no breakpoint: rows use
  `flex-wrap: wrap`, so at 320 px or 200% text the nav row wraps onto a second line instead of scrolling sideways
  (SC 1.4.10).
- Laptop: the header's inner container uses the same max-width as the widest page, and narrower screens
  left-align inside it, so the name, nav and content share one left edge [Convention, GOV.UK width container].
- No `position: fixed` or `sticky` anywhere in the shell. Leave out `viewport-fit=cover`, so no safe-area padding is
  needed.
- `<meta name="viewport" content="width=device-width, initial-scale=1">`. Never `user-scalable=no` or
  `maximum-scale` (SC 1.4.4).
- `<html lang="en-AU">`, so screen readers and spell-check use Australian English (SC 3.1.1).

### Accessibility summary

- **Semantics**: `header`, `nav aria-label="Main"`, `main`, a `ul` of `a href` links, and one `h1` per screen. ARIA is
  used only for `aria-current`, `aria-expanded` with `aria-controls` on Account, and `aria-disabled` on the busy Sign
  out button.
- **Never**: `role="menu"` or `menubar`, `<div onClick>`, or `<a href="#">`.
- **Keyboard**: Tab order is skip link → Grow2Notes → (workers: Today → Account) or (managers: Account → Today,
  Flagged, Report, Manage) → panel contents when open → main. Enter or Space toggles Account; Escape closes it. No
  arrow-key model, because these are plain links.
- **Screen reader**:
  - "Today, current page, link".
  - "Flagged 3 to review, link".
  - "Account, button, collapsed" or "expanded".
  - A new screen announces its `<h1>`.
  - Sign-out errors are announced through the panel's status line, which is in the DOM before any text is written
    to it.
- **Voice control**: every accessible name starts with its visible label (SC 2.5.3). "Tap Flagged" works.
- **Forced colours**:
  - The current bar is a real `border-bottom`, set only on the current item: transparent borders turn visible in
    forced colours.
  - The badge has a `1px solid transparent` border, so its outline appears.
  - Focus uses `outline`, not `box-shadow`.
- **Motion**: none. The panel opens instantly.
- **WCAG 2.2 AA criteria met**:
  - 1.3.1 Info and Relationships
  - 1.3.2 Meaningful Sequence
  - 1.4.1 Use of Color
  - 1.4.3 Contrast (Minimum)
  - 1.4.4 Resize Text
  - 1.4.10 Reflow
  - 1.4.11 Non-text Contrast (focus outline and current bar at 3:1 or better)
  - 1.4.12 Text Spacing (no fixed heights)
  - 2.1.1 Keyboard
  - 2.4.1 Bypass Blocks
  - 2.4.2 Page Titled
  - 2.4.3 Focus Order
  - 2.4.4 Link Purpose
  - 2.4.6 Headings and Labels
  - 2.4.7 Focus Visible
  - 2.4.11 Focus Not Obscured (nothing is sticky)
  - 2.5.3 Label in Name
  - 2.5.8 Target Size (48 px, above design.md's 44 px)
  - 3.1.1 Language of Page
  - 3.2.3 Consistent Navigation
  - 3.2.4 Consistent Identification
  - 4.1.2 Name, Role, Value
  - 4.1.3 Status Messages

### Implementation notes (React 19, React Router data router, TanStack Query 5, CSS Modules)

- **Routing.** Use `createBrowserRouter` with one root layout route, `<AppShell>`, plus an `errorElement` that serves
  as "Page not found". Wrap manager routes in a small `<ManagerOnly>` element that renders the not-found page for
  workers.
- **Do not use `NavLink`.** It always writes `"page"` and matches child routes. Use a 10-line `NavItem` that maps the
  URL to `'page' | 'true' | undefined` using the table above.
- **Titles.** Keep `<title>Grow2Notes</title>` in `index.html` for the moment before JavaScript runs, and set
  `document.title` from a `usePageTitle(name: PageName, hasError?)` hook. If React 19's `<title>` component were
  used as well, there would be two `<title>` elements, which React says gives undefined behaviour. Use one approach
  only.
- **Refresh.**
  - Call `focusManager.setEventListener` once, listening to both `focus` and `visibilitychange`.
  - Because that makes every stale query refetch on focus, set `refetchOnWindowFocus: false` as the
    `QueryClient` default and turn it on only for `['me']` and for the list queries whose specs ask for it. A draft
    query must never refetch over the text on screen.
  - Call `invalidateQueries({ queryKey: ['me'] })` on each `location.key` change.
- **ScrollRestoration (struck in the editorial pass).** It stores scroll offsets in `sessionStorage` under
  `react-router-scroll-positions`. app-shell.md replaces it with an in-memory `Map` keyed by `location.key` (or by a
  route's `handle.screenKey`), so nothing is stored on the device and Back still lands where the person was.
- **Hidden panel.** `.panel { display: flex }` overrides the `hidden` attribute, so add `.panel[hidden] { display: none; }`.
- **Wordmark.** Text only, no logo file. Nothing in `index.html`, meta tags, the favicon, file names or the bundle
  carries the parent company's name (D42).

```tsx
// AppShell.tsx — sketch
export function AppShell() {
  const me = useQuery(meQuery);               // ['me'], refetchOnWindowFocus: true
  useMeRefreshOnRouteChange();                // invalidate ['me'] on location.key
  useArrivalFocusTracker();                   // marks "focus pending" for each client-side navigation
  return (
    <>
      <a href="#main-content" className={s.skipLink} onClick={skipToMain}>Skip to main content</a>
      <Header me={me.data} />
      <div id="page-bar" />                    {/* before-main bar slot (app-shell.md 3a); hidden with the route when signed out */}
      <main id="main-content" tabIndex={-1} className={s.main}>
        <Outlet />
      </main>
      {/* No <ScrollRestoration />: it writes sessionStorage. Scroll lives in the shell's in-memory map (app-shell.md). */}
      <SessionTimeoutDialog />
    </>
  );
}

function skipToMain(e: React.MouseEvent) {
  e.preventDefault();                          // keep #main-content out of the URL and history
  document.getElementById('main-content')?.focus();
}

// Header.tsx — sketch
export function Header({ me }: { me?: Me }) {
  const [open, setOpen] = useState(false);
  const btn = useRef<HTMLButtonElement>(null);
  const panelId = useId();
  const { pathname } = useLocation();
  useEffect(() => setOpen(false), [pathname]);

  const brand = <Link to="/" className={s.brand}>Grow2Notes</Link>;
  if (!me) return <header className={s.header}><div className={s.inner}><span className={s.brand}>Grow2Notes</span></div></header>;

  const isManager = me.role === 'manager';
  const nav = (
    <nav aria-label="Main">
      <ul className={s.navList}>
        <NavItem to="/" current={pathname === '/' ? 'page' : undefined}>Today</NavItem>
        {isManager && <>
          <NavItem to="/flagged" current={pathname.startsWith('/flagged') ? 'page' : undefined}>
            Flagged{me.toReviewCount ? (
              <span className={s.badge}> {me.toReviewCount}<span className="visually-hidden"> to review</span></span>
            ) : null}
          </NavItem>
          <NavItem to={`/reports/daily/${me.today}`} current={pathname.startsWith('/reports/') ? 'page' : undefined}>Report</NavItem>
          <NavItem to="/manage" current={pathname === '/manage' ? 'page' : pathname.startsWith('/manage/') ? 'true' : undefined}>Manage</NavItem>
        </>}
      </ul>
    </nav>
  );
  const account = (
    <button ref={btn} type="button" className={s.accountButton}
            aria-expanded={open} aria-controls={panelId} onClick={() => setOpen(o => !o)}>
      Account<Chevron aria-hidden="true" />
    </button>
  );
  const panel = <AccountPanel id={panelId} hidden={!open} displayName={me.displayName} />;

  return (
    <header className={s.header}
            onKeyDown={e => { if (e.key === 'Escape' && open) { setOpen(false); btn.current?.focus(); } }}>
      <div className={s.inner}>
        {isManager
          ? <><div className={s.row}>{brand}{account}</div>{panel}{nav}</>
          : <><div className={s.row}>{brand}{nav}{account}</div>{panel}</>}
      </div>
    </header>
  );
}

function NavItem({ to, current, children }: { to: string; current?: 'page' | 'true'; children: React.ReactNode }) {
  return <li><Link to={to} aria-current={current} className={s.navLink}>{children}</Link></li>;
}

// PageHeading.tsx — every screen's <h1>
export function PageHeading({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  const claim = useClaimArrivalFocus();        // true once per client-side navigation, false if a banner took it
  const navType = useNavigationType();
  useEffect(() => { if (claim()) ref.current?.focus({ preventScroll: navType === 'POP' }); }, []);
  return <h1 ref={ref} tabIndex={-1} className={s.pageHeading}>{children}</h1>;
}
```

```css
/* AppShell.module.css — sketch; tokens come from the shared token file */
.skipLink:not(:focus) { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
.skipLink:focus { position: absolute; top: 8px; left: 16px; z-index: 1; padding: 12px 16px; background: var(--colour-surface); outline: 3px solid var(--colour-focus); outline-offset: 2px; }
.main:focus { outline: none; }                       /* programmatic skip-link target only */

.header { background: var(--colour-surface); border-bottom: 1px solid var(--colour-border); }
.inner  { max-width: var(--page-max); margin-inline: auto; padding-inline: 16px; }
.row    { display: flex; flex-wrap: wrap; align-items: center; gap: 0 8px; }
.brand  { display: inline-flex; align-items: center; min-height: 48px; font-weight: 700; font-size: 1.125rem; color: var(--colour-text); text-decoration: none; }
.accountButton { margin-inline-start: auto; min-height: 48px; padding-inline: 12px; font: inherit; }

.navList { display: flex; flex-wrap: wrap; gap: 0 4px; margin: 0; padding: 0; list-style: none; }
.navLink { display: inline-flex; align-items: center; gap: 6px; min-height: 48px; padding: 0 8px 4px; color: var(--colour-text); text-decoration: none; }
.navLink[aria-current] { font-weight: 700; padding-bottom: 0; border-bottom: 4px solid var(--colour-brand); }
.navLink:hover, .brand:hover, .accountButton:hover { text-decoration: underline; text-decoration-thickness: 2px; text-underline-offset: 4px; }
.navLink:active, .brand:active, .accountButton:active { background: var(--colour-pressed); }
.navLink:focus-visible, .brand:focus-visible, .accountButton:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }

.badge { display: inline-block; min-width: 1.5em; padding: 0 0.4em; border: 1px solid transparent; border-radius: 999px;
         background: var(--colour-badge-bg); color: var(--colour-badge-text); font-size: 0.875rem; font-weight: 700; line-height: 1.5; text-align: center; }

.panel { display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: center; gap: 8px 16px; padding-block: 8px 12px; }
.panel[hidden] { display: none; }

.pageHeading { width: fit-content; max-width: 100%; }
.pageHeading:focus:not(:focus-visible) { outline: none; }
```

## Per-screen notes

### App shell (global, design.md §4.0)

- One header on every signed-in screen, never sticky or fixed. Workers get one row, managers two. There are no
  breakpoints in the header's structure, only wrapping.
- The badge is on every manager screen (§4.6). It refreshes on page load, on screen change, on focus or visibility,
  and after a review or a flagged submit. It never polls.
- Titles come only from the fixed `PageName` list. URLs hold only IDs and dates. Router state holds no names.
- The session time-out dialog and "sign in again in place" are hosted here. Their content belongs to their own specs.
- Sign-out waits for any pending autosave, then posts, then does a full reload with `replace`.

### Today (design.md §4.2)

- Route `/`, title "Grow2Notes – Today", nav item **Today** with `aria-current="page"`. The wordmark also leads
  here.
- `<h1>` via `PageHeading`: "Today · Thursday 1 October", built from `me.today`, the server's Melbourne date, never
  the device clock (§3.3). When `/me` refreshes on focus after midnight, the Today spec should refetch its list if
  `today` has changed.
- Arrival focus:

  | How the user arrived | Focus goes to | Scroll |
  |---|---|---|
  | Tapping Today or the wordmark | The `<h1>` | Top |
  | Back from a note | The `<h1>` with `preventScroll` | Restored to where the user was in the list |
  | After Submit | "Note for Jane Citizen submitted" (`tabindex="-1"`, placed before the `<h1>`, removed on the next screen change, GOV.UK pattern) | Top |

  That confirmation text includes the participant's name. It lives in memory only, never in router state, the URL
  or the title.
- Today is a long list, so the header scrolls away. That is accepted: navigation is needed only at the start or end
  of a task. Workers come back to Today through the note form's own "back to Today".
- Managers' "Add participant" in the "No participants yet." state (§4.2) is page content in `<main>`, not a nav item.
- The search box "Find a participant" sits in `<main>`. If its spec wraps it in a `<search>` landmark, that landmark
  is the only other one on the page, which is fine (APG).

### Other signed-in screens (for consistency only; their own specs own the content)

| Screen | Title | Current item | Note |
|---|---|---|---|
| Note form / read view (§4.3, §4.4) | Grow2Notes – Note | none | The header stays above the note's own top bar, as on every manager screen (§4.6). Nothing is sticky, so the keyboard and 200% text keep the full height. |
| Past notes, Version history | Grow2Notes – Past notes / Version history | none | |
| Flagged (§4.6) | Grow2Notes – Flagged notes | Flagged (`page`) | The badge and the "To review (n)" tab read from the same refreshed data. |
| Report (§4.7) | Grow2Notes – Daily report | Report (`page`) | The link targets `me.today`. |
| Manage | Grow2Notes – Manage | Manage (`page`) | Four links only. |
| Participants, Common items, Guide prompts, Users and details | Grow2Notes – {name from list} | Manage (`true`) | |
| Sign in / setup / signed out (§4.1) | Grow2Notes – Sign in / Set up your account | – | Wordmark as plain text; no nav or Account. |

## Anti-patterns to avoid

**Navigation layout**

- A hamburger or "More" menu for 4 or fewer items, on any width. Never on a laptop.
- A fixed bottom tab bar on the mobile web. It is covered by the keyboard, sits too close to the home indicator,
  takes height at 200% text, and can hide focused items (SC 2.4.11).
- A sticky or fixed header on phones (Understanding 1.4.10, C34).
- `role="menu"` or `menubar` for navigation. A dropdown that opens on hover.
- Hiding the header on the note form. The badge must be on every manager screen (§4.6), and One Login's research
  found no distraction from keeping it.
- Disabling nav items, or showing workers greyed-out manager items. Workers simply don't get them.
- Icon-only navigation. An icon without a visible text label.
- CSS `order` or grid placement that makes the visual order differ from the tab order.

**Current item and badge**

- React Router `NavLink`'s default `aria-current="page"` on child pages. Marking two items current. Showing the
  current item by colour alone.
- A badge shown as a red dot or colour alone; a "0" badge; a badge as a live region; polling for the count.

**Focus and screen changes**

- Moving focus to a full-width wrapper on a screen change (magnifier users lost the text).
- Moving focus on the first page load.
- Removing focus outlines, or using a `box-shadow`-only focus ring, which disappears in forced colours.
- `<div onClick>`; `<a href="#" onClick>`; `disabled` on the busy Sign out button.

**Privacy and storage**

- Participant names in `document.title`, URLs, query strings or `navigate(..., { state })`, all of which can reach
  browser history on disk.
- Using `localStorage` to remember whether the Account panel is open, or anything else.
- The parent company's name anywhere in the shell, in metadata or in assets (D42).

**Other**

- `user-scalable=no` or `maximum-scale=1`.
- A confirmation dialog on Sign out.

## Tensions with decisions

- **Title order.** design.md §4.0 gives the format "Grow2Notes – Note", with the app name first. HMRC's page-title
  pattern [Convention] and the titles in W3C's own examples in the Understanding document for 2.4.2 put the
  page-specific part first, then the service name. That way screen readers announce the distinguishing words
  first, and phone tabs and history entries are less likely to cut off to the same "Grow2Notes – …" prefix.
  Evidence: https://design.tax.service.gov.uk/hmrc-design-patterns/page-title ·
  https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html. This spec follows design.md exactly.

## Sources

- NN/g, Hamburger Menus and Hidden Navigation Hurt UX Metrics (2016): https://www.nngroup.com/articles/hamburger-menus/
- NN/g, The Hamburger-Menu Icon Today: Is it Recognizable? (2025): https://www.nngroup.com/articles/hamburger-menu-icon-recognizability/
- NN/g, Budiu, Basic Patterns for Mobile Navigation: A Primer (2015): https://www.nngroup.com/articles/mobile-navigation-patterns/
- NN/g, Loranger, Homepage Links Remain a Necessity (2017, reviewed 2024): https://www.nngroup.com/articles/homepage-links/
- NN/g, Farrell, Navigation: You Are Here (2015): https://www.nngroup.com/articles/navigation-you-are-here/
- Hoober, How Do Users Really Hold Mobile Devices? (UXmatters, 2013): https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php
- WebAIM, Screen Reader User Survey #10 (2024): https://webaim.org/projects/screenreadersurvey10/
- Gatsby and Fable Tech Labs, user testing of accessible client-side routing (2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- GOV.UK Design System, Service navigation: https://design-system.service.gov.uk/components/service-navigation/
- GOV.UK Frontend, service navigation template: https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/service-navigation/template.njk
- GOV.UK Design System, Help users navigate a service: https://design-system.service.gov.uk/patterns/navigate-a-service/
- GOV.UK Design System, Skip link: https://design-system.service.gov.uk/components/skip-link/
- GOV.UK Design System, Notification banner: https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK One Login, Let users navigate and sign out: https://www.sign-in.service.gov.uk/documentation/design-recommendations/let-users-navigate-sign-out
- HMRC Design Patterns, Page title: https://design.tax.service.gov.uk/hmrc-design-patterns/page-title
- NHS digital service manual, Header: https://service-manual.nhs.uk/design-system/components/header
- Agriculture Design System (AgDS), App layout: https://design-system.agriculture.gov.au/components/app-layout
- AgDS, Main nav: https://design-system.agriculture.gov.au/components/main-nav
- AgDS, Notification badge: https://design-system.agriculture.gov.au/components/notification-badge
- Apple Human Interface Guidelines, Tab bars: https://developer.apple.com/design/human-interface-guidelines/tab-bars
- Material 3, Navigation bar: https://m3.material.io/components/navigation-bar/guidelines
- WCAG 2.2 Understanding, Bypass Blocks: https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html
- WCAG 2.2 Understanding, Page Titled: https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html
- WCAG 2.2 Understanding, Consistent Navigation: https://www.w3.org/WAI/WCAG22/Understanding/consistent-navigation.html
- WCAG 2.2 Understanding, Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding, Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Technique C34: https://www.w3.org/WAI/WCAG22/Techniques/css/C34
- WCAG 2.2 Understanding, Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WAI-ARIA APG, Disclosure navigation menu example: https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/
- WAI-ARIA APG, Landmark regions: https://www.w3.org/WAI/ARIA/apg/practices/landmark-regions/
- MDN, aria-current: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-current
- MDN, History.pushState(): https://developer.mozilla.org/en-US/docs/Web/API/History/pushState
- React, `<title>`: https://react.dev/reference/react-dom/components/title
- React Router, NavLink: https://reactrouter.com/api/components/NavLink
- React Router, ScrollRestoration: https://reactrouter.com/api/components/ScrollRestoration
- TanStack Query 5, Window focus refetching: https://tanstack.com/query/v5/docs/framework/react/guides/window-focus-refetching
- Chrome for Developers, viewport resize behaviour on Android (2022): https://developer.chrome.com/blog/viewport-resize-behavior
- WebKit, Designing websites for iPhone X (2017): https://webkit.org/blog/7929/designing-websites-for-iphone-x/
