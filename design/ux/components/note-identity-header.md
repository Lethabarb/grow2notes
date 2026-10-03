# Participant identity header and back link

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> **Editorial pass, 1 October 2026: the app-wide rules this file now records** (they replace older values below):
> 1. **Identity heading:** on every page about one note (the note form, the read view, both version pages) the
>    `<h1>` holds up to three block lines: an optional caption ("Editing submitted note (version 2)", "Version 2",
>    "Version history"), the participant's name, then the note date as `<time>` (body size, weight 400, full text
>    colour). Literal spaces give one accessible name ("Jane Citizen Thursday 1 October 2026"), so Read → Edit changes
>    only the caption that is announced. Past notes (no single date) has caption + name only. Metadata under it
>    (byline, saved-by, status lines) is 16 px in `--colour-text-secondary` on every one of these pages.
> 2. **BackLink:** one shared component (app-shell.md component 3a), label = the destination's name, rendered in the
>    shell's **before-main bar slot**, popping when the previous entry is the destination, otherwise pushing.
> 3. **Where a screen was opened from:** the shell's **in-memory opened-from record** keyed by `location.key` and
>    copied across replaces (app-shell.md, route-change rule). **No router state**: `history.state` holds only React
>    Router's key and `inScreen`. After a reload the back link falls back to Today.
> 4. **Sticky:** only the note form's bar is sticky; the read view, version pages and Past notes use a static bar
>    with no compact name (participant-notes.md, version-history.md). Type sizes and the breakpoint come from
>    foundations.md (h1 28/32 px; `40rem`); the older 2rem/2.5rem and `48em` values have been replaced below.
> 5. **Leaving after a failed save:** no guard dialog (note-form.md Conflicts #5); the form stays and announces the
>    indicator text.

Component key: `note-identity-header`. This is the top of every screen about one participant. It has a one-row **top bar** with the back link on the left and the save indicator on the right (the save indicator only appears on the form). Under the bar is the **identity block**: the participant's full name in large type, then the note date. The same name and date appear again in the **submit confirmation**. Defined by design.md 3.3, 3.8, 3.9, 4.0, 4.3, 4.4 and A5, A11, A32.

This spec covers the bar's layout and the back link, the identity block, and how the identity is shown inside the confirmation. Three things are covered by other specs: what the save indicator says, the banners under the identity block, and how the confirmation dialog works.

Evidence grades: **[Research]** usability testing or studies · **[Standard]** WCAG 2.2, WAI-ARIA, HTML · **[Convention]** established design systems · **[Opinion]** reasoned judgement, no direct evidence.

---

## Where it's used

| Screen (design.md) | Back link goes to | Identity block | Top bar right side | What differs |
|---|---|---|---|---|
| **Note form, new or draft, today** (4.3) | **Today** (design: "back to Today") | Name + "Thursday 1 October 2026" | Save indicator | The base case. |
| **Note form, draft from an earlier day** (4.3, 3.4) | Today | Name + that draft's note date | Save indicator | The banner "This draft is for Wed 30 Sep. Submitting it now keeps that date." sits directly under the date. |
| **Note form, manager's past-day note** (4.3, 3.8, 4.8) | The screen it was opened from: Participant (detail) or Past notes. Falls back to Today. | Name + the past date | Save indicator | The banner "Past-day note for Mon 28 Sep 2026, written on 1 Oct." sits under the date. |
| **Note form, editing a submitted note** (4.3, 3.5) | Same place the read view's back link goes | The caption "Editing submitted note (version 2)" sits above the name, inside the heading | Save indicator | **Save changes** and **Cancel** take you back to the read view, so the back link is a way to leave the note altogether. |
| **Submit confirmation** (4.3) | None. **Go back** does that job. | "Submit today's note for" / name / date | n/a (modal) | Same identity, in a native `<dialog>`. The name also appears on the button: "Submit note for Jane Citizen". |
| **Read view of one note** (4.4; also opened from Today 4.2 and Flagged 4.6) | The list it was opened from: Today, Past notes or Flagged | Name + note date | Empty | Read-only. Author, submitted time, "Edited" and "Past-day note, written on … by …" come under the identity block and belong to the read-view spec. |
| **Participant notes (history list)** (4.4) | Where it was opened from: the note (form or read view) or Participant (detail) | The caption "Past notes" sits above the name. No date. | Empty | The manager actions (Write past-day note, Export record, Edit participant) sit under the identity block in `<main>`, not in the bar. |

---

## Best practice

### Preventing wrong-person entries

- **Wrong-person notes really happen, and readers spot them.** In a survey of 22,889 patients who read their own notes, 23 of the 356 very serious errors they reported (6.5%) were "notes on the wrong patient". **[Research]** [Bell et al. 2020, JAMA Netw Open](https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2766834)
- **Show who the record belongs to on every screen: large, in the same place every time, top left.** The US Patient Identification guide (SAFER), Recommended Practice 1.3, rated *Strong*: "Information required to accurately identify the patient is clearly displayed on all portions of the EHR user interface." Its implementation guidance asks for "large font sizes, distinct colors, minimal visual clutter, and consistent location across various EHR screens", and adds that this "is best displayed on the top-left of the screen". The 2026 update kept 1.3 at Strong. **[Convention]** (an expert consensus checklist built on a literature review) [ONC SAFER Patient Identification (Aug 2024 edition, 2025 set)](https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf) · [Weatherford et al. 2026, JAMIA Open](https://pmc.ncbi.nlm.nih.gov/articles/PMC12772641/)
- **The strongest protection is at the moment of commitment.** In an RCT over 901,776 ordering sessions:
  - a "verify" alert that showed the patient before ordering reduced wrong-patient orders (OR 0.84, 95% CI 0.72–0.98);
  - making the clinician re-type identifiers reduced them more (OR 0.60, 0.50–0.71).

  Grow2Notes's confirmation, which names the participant (3.9), is the verify-alert kind. **[Research]** [Adelman et al. 2013, JAMIA](https://pubmed.ncbi.nlm.nih.gov/22753810/)
- **What is shown alongside the name matters.** Adding a photograph to an EHR banner that already showed the patient's name went with fewer wrong-patient orders: 133 v 186 per 100,000, adjusted OR 0.57 (0.52–0.61), across 2,558,746 orders. This was a single emergency department and an observational study. **[Research]** [Salmasian et al. 2020, JAMA Netw Open](https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2772798) Photos are not in Grow2Notes; see Tensions.
- **Don't try to fix this by limiting open tabs.** An RCT of 3,356 clinicians and 4,486,631 order sessions found that allowing only one record open made no difference: 90.7 v 88.0 wrong-patient orders per 100,000 sessions, OR 1.03 (0.90–1.20). **[Research]** [Adelman et al. 2019, JAMA](https://pubmed.ncbi.nlm.nih.gov/31087021/) This supports the design's choice to warn about a second tab rather than block it (4.3).
- **A name alone is not a full identifier.** SAFER 1.3: "Patient names alone are not sufficient for identification." The Australian hospital standard asks for at least three approved identifiers (name, date of birth, record number). **[Convention]** [SAFER](https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf) · [ACSQHC, Correct identification and procedure matching](https://safetyandquality.gov.au/standards/nsqhs-standards/communicating-safety-standard/correct-identification-and-procedure-matching). That page timed out when fetched, so the detail comes from the search summary and is unverified. It is unverified whether this standard applies to NDIS providers. See Tensions.
- **Upper case for names is a deprecated NHS convention.** The NHS Common User Interface (CUI) patient banner and name display standards (ISB 1505, ISB 1506) put the family name in capitals, accepting slower reading as a way to make people actually read it. Those standards are now **Deprecated** ("out of date"). **[Convention]** [NHS Standards Directory, CUI standards](https://standards.nhs.uk/published-standards/common-user-interface-standards) · secondary summary: [G. Schmidt, uppercase family name debate](http://www.gregoryschmidt.ca/writing/patient-name-uppercase-family-name-debate). GOV.UK's style guide: "DO NOT USE BLOCK CAPITALS FOR LARGE AMOUNTS OF TEXT AS IT'S QUITE HARD TO READ." **[Convention]** [GOV.UK A to Z style guide](https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/)
- **Stop browser translation from rewriting names.** `translate="no"` "indicates that the element must not be translated", and Google Translate respects it. This matters to workers who read English as a second language and use the browser's translate feature. **[Standard]** [MDN, translate](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/translate)

### Sticky or static header on a long form

- **A sticky header costs screen space, so keep it small, solid and still.** NN/g (Laubheimer, 2021): sticky headers help people reach navigation without scrolling up. "When implemented poorly, sticky headers are annoying, distracting, and obstruct page content." Make the header as short as possible while keeping readable text and tap targets, use "an opaque color", and "it's best to not use animation at all". No study or threshold is given. **[Convention]** [NN/g, Sticky Headers](https://www.nngroup.com/articles/sticky-headers/) The ui-build corpus's cap of 10% of the viewport height is a decision rule taken from this, not a measurement. **[Opinion]**
- **The page must scroll so focused items are never fully hidden under the bar.** SC 2.4.11 (AA): a focused component must not be "entirely hidden due to author-created content". A sticky header that fully covers focus is failure F110, and CSS `scroll-padding` is sufficient technique C43. **[Standard]** [Understanding 2.4.11](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html)
- **Make the bar static when the screen is small or zoomed.** The Reflow Understanding document: "It is strongly suggested that at smaller viewport sizes that such components are modified to have static positioning". It explains that sticky content "significantly reduce[s] the available space for reading" at high zoom. **[Standard]** (advisory text in Understanding) [Understanding 1.4.10](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html)
- **A large title can shrink into the bar as you scroll.** Apple HIG: "By default, a large title transitions to a standard title as people begin scrolling the content, and transitions back to large when people scroll to the top, reminding them of their current location." **[Convention]** [Apple HIG, Toolbars](https://developer.apple.com/design/human-interface-guidelines/toolbars)

### Heading structure and focus on arrival

- **Use one `<h1>` per screen, and let it say what the screen is about.** SC 1.3.1 and 2.4.6 require headings that describe the topic. GOV.UK lets a caption sit inside the `<h1>` "if the caption should be considered part of the page heading". **[Standard]** [WCAG 2.2](https://www.w3.org/TR/WCAG22/) · **[Convention]** [GOV.UK Headings](https://design-system.service.gov.uk/styles/headings/)
- **In a single-page app, move focus to the new screen's heading.** Gatsby's user testing (Marcy Sutton, 2019; 5 sessions with NVDA, JAWS, ZoomText, Dragon and switch users) found that for screen reader users "focusing on a heading was found to be the best experience". Focus outlines on things that can't be operated confused the switch user. Gatsby itself shipped a skip-link approach in the end. The sample was small. **[Research]** [Gatsby, user testing accessible client routing](https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/)
- **Page titles may be generic in a web app.** Understanding 2.4.2: for a web application, "the name of the document or web application would be sufficient to describe the purpose of the page." So "Grow2Notes – Note" (4.0) passes. **[Standard]** [Understanding 2.4.2](https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html)

### Back link and the browser's Back button

- **Put the back link at the top left, before `<main>`, and make it return people to the previous page as they left it.** GOV.UK: "Always place back links at the top of a page, before the `<main>` element", so that "Skip to main content" skips it. Also: "Make sure the link takes users to the previous page they were on, in the state they last saw it." "Back" works for simple journeys; for complex ones, consider "Go back to [page]". NHS says the same, adding that the back link should sit "top left". **[Convention]** [GOV.UK Back link](https://design-system.service.gov.uk/components/back-link/) · [NHS Back link](https://service-manual.nhs.uk/design-system/components/back-link)
- **The in-app back link and the browser's Back button should agree.** Android: inside an app, "the Up and Back buttons behave identically", and Up never leaves the app. When the screen was opened directly by a link, Up follows a made-up back stack to the app's start screen. **[Convention]** [Android, Principles of navigation](https://developer.android.com/guide/navigation/principles)
- **People expect browser Back to go to what they *saw* as the previous page.** Baymard: "Users expect the 'Back' button to take them back to what they *perceived* to be their previous page", and breaking this caused people to give up in testing. 59% of benchmarked e-commerce sites got at least one case wrong (2020). **[Research]** (Baymard's usability testing and benchmark; that is e-commerce, not this sector) [Baymard, Back button expectations](https://baymard.com/blog/back-button-expectations)
- **The accessible name must contain the visible label, ideally at the start.** SC 2.5.3: "the name contains the text that is presented visually", and it is "a best practice to have the text of the label at the start of the name". **[Standard]** [Understanding 2.5.3](https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html)

### Identity inside the confirmation dialog

- **Choose where focus lands when the dialog opens.** WAI-ARIA APG: when a dialog opens with content above its buttons, "add `tabindex="-1"` to a static element at the top of the dialog, such as the dialog title … and initially focus that element". For actions that are hard to undo, "set focus on the least destructive action". When the dialog closes, focus goes back to the button that opened it. **[Standard]** [APG Dialog (Modal)](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/) · [MDN, `<dialog>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog)

---

## Recommendation for Grow2Notes

### Anatomy

```
<body>
  [skip link: "Skip to main content"]
  [app navigation, if the navigation spec shows it on this screen; scrolls away]
  ┌ top bar (sticky, one row) ───────────────────────────────┐
  │ ‹ Today        [Jane Citizen]*         [save indicator]  │   * only once the h1 has scrolled away
  └──────────────────────────────────────────────────────────┘
  <main id="main">
    [error summary, when Submit fails]           (error-summary spec)
    <h1>  [caption, when there is one]
          Jane Citizen                           (large)
          <time>Thursday 1 October 2026</time>   (body size; not on the history list)
    </h1>
    [banners: past-day / earlier-day draft / changed elsewhere / lists changed]  (banner spec)
    … the screen's content …
  </main>
```

1. **Top bar.** One row, before `<main>`, sticky (see Behaviour). Its parts:
   - **Back link** on the left: a chevron icon plus the name of the place it goes to.
   - **Compact name** in the middle: shown only after the large name has scrolled out of view.
   - **Save indicator** on the right: note form only, with content owned by the save-indicator spec.
2. **`<h1>`**: an optional caption on its own line in smaller type, then the participant's **given name and family name** in large type, in title case as stored. Never upper case. Never truncated. Never translated.
3. **Note date** (form, read view and version pages): `<time dateTime="2026-10-01">Thursday 1 October 2026</time>` as the **last line inside the `<h1>`** (editorial pass; it was a `<p>` under the heading).
4. **Banner slot** directly after the date. This spec only fixes the order.

### Behaviour

**Back link**

- It is a real link (`<Link>` from React Router, which renders `<a href>`). Its `href` is the destination path: IDs and dates only, never names (4.0).
- **Where it goes.** The screen that opened this one inside the app, from a fixed list (see the Copy section for the labels). If the screen was opened from outside the app (reload, a pasted URL, a new tab, sign-in in place), it goes to **Today**. **[Convention]** GOV.UK "previous page", Android Up.
- **How it gets there.**
  - If the previous history entry *is* the destination, tapping the link calls `navigate(-1)`. The browser's back stack stays a straight line, and Today comes back where it was.
  - Otherwise it follows its `href` (a push).
  - Ctrl, Cmd, Shift and middle-clicks always follow the `href`. **[Convention]** Baymard, Android.
- **Moving between the read view and the edit form of the same note uses `replace`, not push**, and keeps the same back destination. Edit → form replaces the read view. Save changes or Cancel → read view replaces the form. So the entry before is always the list the person came from, and browser Back never reopens a form they have finished with. **[Opinion]**
- **After Submit**, return to Today the same way:
  - pop if Today is the previous entry, otherwise replace;
  - pass "Note for Jane Citizen submitted" to Today **in memory**, never in router state, because history state is written into the browser's session history on the device (D22, 4.0).
- **Leaving while a save is pending.** The link always works and is never disabled.
  - If an autosave is waiting on its 2-second timer, the form sends it at once and then leaves. TanStack Query lets the request finish after the form unmounts.
  - If the latest save has **failed** (the "Not saved: no connection…" state), a native `<dialog>` asks first. The copy is proposed below and is not in design.md; see Open questions.
  - Use React Router `useBlocker` for this, so the browser Back button is caught too. Add `beforeunload` only while the note is unsaved, and remove it once the note saves. **[Opinion]**, following invariant I9 (never lose partial work). ui-ux-design 08-forms-validation: attach `beforeunload` only while dirty.

**Top bar**

- `position: sticky; top: 0` on the **note form only** (editorial pass: the read view and the history list use a static bar, participant-notes.md Conflicts #1).
  - It has one row, with a minimum height of 3.5rem (56 px, about 8% of a 667 px phone screen), an opaque background and a 1px bottom border.
  - It never animates, collapses or hides on scroll. **[Convention]** NN/g.
- **It becomes static** when the viewport is short: `@media (max-height: 30rem)`. That covers a landscape phone and roughly 200% zoom or more on a laptop. When static, the compact name is never shown. **[Standard]** (advisory) Understanding 1.4.10.
- **Compact name.**
  - When the `<h1>` has scrolled up behind the bar (an IntersectionObserver with the bar's height as a negative top margin), the bar shows the full name on one line, bold, at body size, between the back link and the save indicator.
  - This copy may end with "…" if it doesn't fit. If the space is under 6rem it is not shown at all (a container query).
  - It appears and disappears instantly, with no animation.
  - It is plain text: not a heading, not a link, not a live region. It is added to the DOM only while visible, so it is never read twice.
  - **[Convention]** Apple's large-title behaviour; SAFER 1.3 "all portions"; **[Opinion]** for the details.
- **Never stack two sticky bars.** If the app navigation is shown above this bar on these screens, it scrolls away normally. **[Opinion]**, following NN/g on screen space.
- `scroll-padding-block-start` on the root equals the bar's measured height plus 0.5rem, so Tab never puts a focused tick box under the bar. The measurement comes from a ResizeObserver, because the save indicator's text can make the bar taller. It is 0 when the bar is static. **[Standard]** 2.4.11, C43.

**Identity block**

- The name comes from `GET /api/participants/{participantId}` (`givenName`, `familyName`).
  - The block **renders nothing until both the participant and the note have loaded.** Form controls never render without the name above them.
  - Never show a placeholder name, a cached name from another participant, or a skeleton name. See the anti-pattern about leaking another participant's data.
- **Focus on arrival.**
  - After an in-app navigation, once the `<h1>` first renders, focus moves to it (`tabIndex={-1}`). A screen reader then announces the name, which acts as an identity check for people who can't see the screen.
  - This happens once per location, never on re-render, and never on the first full page load.
  - It doesn't steal focus if the person has already moved it, for example by tabbing to the back link while the page loaded.
  - **[Research]** (Gatsby, n=5) · **[Opinion]** for the details.
- Long names **wrap**, up to as many lines as they need (`overflow-wrap: break-word`, `text-wrap: balance`, `hyphens: manual` so names are never auto-hyphenated). **[Standard]** 1.4.10 (no loss of information at 320 px).

### States

| State | What shows |
|---|---|
| **Default (form)** | `‹ Today` · save indicator. Then **Jane Citizen** and "Thursday 1 October 2026". |
| **Scrolled** | The bar adds the compact **Jane Citizen** between the back link and the save indicator, if there is room. |
| **Short viewport or high zoom** | The bar scrolls away with the page and there is no compact name. |
| **Back link hover** (laptop) | Underline thickens to 3px. Pointer cursor. |
| **Back link focus** | 3px outline in the focus colour, 2px offset (`:focus-visible`). Always visible (A32). |
| **Back link active** | Link colour darkens. No movement. |
| **Back link visited** | Same as default (app chrome, not content). |
| **Back link disabled** | Never. There is always a way out. |
| **Leaving while saving** | Navigates at once. The pending autosave is sent. |
| **Leaving after a failed save** | Confirmation dialog (proposed copy below). "Stay on this page" has focus. |
| **`<h1>` focused (after arrival)** | The outline shows only under `:focus-visible` (keyboard arrival), never after a tap. |
| **Loading** | The top bar and back link render at once. The identity area keeps its height and is blank for up to 1 s, then the app's shared loading message appears in `<main>`, never in the `<h1>`. **[Convention]** ui-build: nothing under 1 s. |
| **Error: note or participant can't load** (network, `404`) | The top bar stays, so the person can leave. The shared load-error message shows in `<main>`. The design has no copy for this; see Open questions. No `<h1>` name. |
| **Signed out (`401`)** | Sign-in shows in place (8.5). Afterwards the same route loads and focus goes to the `<h1>`. |
| **Editing a submitted note** | The `<h1>` caption "Editing submitted note (version 2)" sits above the name. |
| **Past-day note / earlier-day draft** | The date line shows that note's date. The design's banner follows directly underneath. |
| **Read-only** (read view; a manager viewing someone else's draft) | The same header with no save indicator. Read-only status is shown by the read-view body. |
| **Archived participant** (a draft still being finished, 3.7) | No change. The design specifies no label here. |
| **Very long name** | The `<h1>` wraps over 2–3 lines. The compact name ends with "…". The confirmation wraps. |
| **Empty** | Not possible: a participant always has a given name and a family name (A5). |

### Phone vs laptop

- **Phone (below 40rem; foundations.md's breakpoint, editorial pass).**
  - The bar and content span the screen with 1rem side gutters.
  - The `<h1>` name is `--font-size-h1` (28 px) with line-height 1.2, bold. The date, the heading's last line, is body size (18 px) regular in the full text colour (foundations.md; editorial pass).
  - The back link's hit area is at least 44 × 44 px (A32), with padding on the right.
- **Laptop (40rem and up).**
  - The bar's background spans the window. Its contents line up with the content column (the same max-width as the form).
  - The name is 32 px (`--font-size-h1` from 40rem); the date stays at body size.
  - Hover styles apply.
- Sizes use rem steps at one breakpoint, never `vw` or `clamp()` with `vw`, so 200% text resize works (1.4.4). **[Standard]**

### Copy

All strings come from design.md unless marked *derived* or *proposed*.

| Where | Text |
|---|---|
| Back link (form default, design 4.3 sketch) | **Today** |
| Back link to the history list | **Past notes** *(derived: the design's own name for that link, 4.4)* |
| Back link to the flag review list | **Flagged** *(derived: the manager nav item, 4.0)* |
| Back link to participant detail (manager) | **Participant** *(derived)* |
| Back link from the history list to the note it was opened from | **Note** *(derived)* |
| `<h1>` name | **Jane Citizen**: given name, a space, family name, as stored |
| Date line | **Thursday 1 October 2026** (weekday, day, month, year; no commas) |
| `<h1>` caption, editing | **Editing submitted note (version 2)** |
| `<h1>` caption, history list | **Past notes** *(derived)* |
| Banners under the date (owned by the banner spec) | "Past-day note for Mon 28 Sep 2026, written on 1 Oct." · "This draft is for Wed 30 Sep. Submitting it now keeps that date." · "This note was changed on another device or tab." · "The goal or common-item list was just changed. Please check your ticks." |
| Page title, form and read view | **Grow2Notes – Note** (en dash with spaces) |
| Page title, history list | **Grow2Notes – Past notes** *(derived from the 4.0 pattern)* |
| Confirmation heading, today's note | **Submit today's note for** / **Jane Citizen** |
| Confirmation heading, any other date (earlier-day draft, past-day note) | **Submit the note for** / **Jane Citizen** *(derived: "today's" would be wrong)* |
| Confirmation lines | **Thursday 1 October 2026** · **Flagged for manager: No** (or **Yes**) |
| Confirmation buttons | **Submit note for Jane Citizen** · **Go back** |
| Leave guard (failed save only) | Heading **Your latest changes are not saved** · body **If you leave now, they will be lost.** · buttons **Stay on this page** / **Leave without saving** *(proposed; not in design.md)* |
| Loading (after 1 s) | **Loading…** *(derived; use the app's shared loading text if one exists)* |

### Accessibility

**Semantics**
- The top bar is a plain `<div>` before `<main>`. GOV.UK reports no user problems with a back link outside landmarks, so there is no `<nav>` for a single link.
- The back link is a native `<a href>`. The chevron is an inline SVG with `aria-hidden="true"` and `focusable="false"`.
- The identity block is the first heading in `<main>`, or the second item when the error summary shows.
- Section headings on the form and read view ("1. Goals", "2. Common items", "3. Guided notes") are `<h2>`.
- The confirmation's heading is an `<h2>` inside the `<dialog>`; the page `<h1>` behind it is inert.

**ARIA, only these**
- `tabIndex={-1}` on the `<h1>` and on the dialog `<h2>`, as focus targets.
- `aria-labelledby` on the `<dialog>`, pointing at its `<h2>`.
- `aria-describedby` on the `<dialog>`, pointing at the date and flag lines.
- Nothing else: no `aria-label` on the back link (its visible text is its name, which meets the 2.5.3 best practice), no live region on the name, and no role on the bar.

**Keyboard**
- Tab order: skip link → (app navigation, if shown) → back link → `<main>` content.
- The save indicator is not focusable.
- Enter follows the back link.
- In the dialog: focus starts on the heading, Tab moves to **Submit note for Jane Citizen** and then **Go back**, and Esc is the same as Go back.
- Focus starts on the heading for two reasons. A fast double Enter or a key repeat from **Submit note** can't submit without the person seeing the name. And screen readers read the name first. **[Standard]** APG, static element at the top. **[Opinion]** for using it here.
- When the dialog closes, browsers return focus to **Submit note**. Check this on iOS Safari.

**Screen reader announcements.** These are expected wording; it varies by screen reader and has not been tested.

| Moment | Expected announcement |
|---|---|
| Arrive at the form from Today | "Jane Citizen, heading level 1" |
| Arrive at the history list | "Past notes Jane Citizen, heading level 1" |
| Arrive at the edit form | "Editing submitted note (version 2) Jane Citizen, heading level 1" |
| Tab to the back link | "Today, link" |
| Compact name appears on scroll | Nothing (visual only, not a live region) |
| Confirmation opens | "Submit today's note for Jane Citizen, dialog, Thursday 1 October 2026, Flagged for manager: No", then "heading level 2" on the focused heading |

**WCAG 2.2 criteria met:**
- 1.3.1 Info and Relationships (`<h1>`, `<time>`, link before `<main>`)
- 1.3.2 Meaningful Sequence
- 1.4.1 Use of Color (no state carried by colour)
- 1.4.3 Contrast (Minimum): text 4.5:1
- 1.4.4 Resize Text (rem sizes)
- 1.4.10 Reflow (name wraps, bar goes static on short viewports)
- 1.4.11 Non-text Contrast (focus outline 3:1)
- 1.4.12 Text Spacing (minimum heights only, no fixed heights)
- 2.4.1 Bypass Blocks (the skip link passes the bar)
- 2.4.2 Page Titled
- 2.4.3 Focus Order
- 2.4.4 Link Purpose (In Context): the link names its destination
- 2.4.6 Headings and Labels
- 2.4.7 Focus Visible
- 2.4.11 Focus Not Obscured (Minimum), via `scroll-padding`
- 2.5.3 Label in Name
- 2.5.8 Target Size (Minimum): 44 px, which exceeds 24 px (A32)
- 3.2.3 Consistent Navigation (same place on every participant screen)
- 3.2.4 Consistent Identification
- 4.1.2 Name, Role, Value (native elements)

**Testing.**
- Run axe through Playwright on all three screens.
- Do manual passes with VoiceOver on iOS Safari, TalkBack on Android Chrome, and NVDA with Chrome.
- Check at 200% and 400% zoom, on a landscape phone, and in forced-colours mode.
- Test long names: 40 characters, hyphenated, and two-word family names.
- Turn on Chrome's page translation and check that names stay unchanged.
- **Wrong-person regression test (Playwright):** open participant A's form, go back, open participant B's form with the network throttled. Assert that A's name never appears in B's DOM at any point.
- Check the sticky bar on iOS Safari with the keyboard open on the Guided notes box. How sticky elements behave there is unverified.

### Implementation: React 19, native HTML, CSS Modules

No React Aria is needed: a link, a heading, `<time>` and `<dialog>` are all native.

**Back destination: the shell's in-memory opened-from record, never router state** (editorial pass; app-shell.md
route-change rule). The opener route declares `handle.backKey`; when its link pushes a new entry, the shell records
`{ entryKey, backKey, path }` on that entry (memory only) and copies it across a `replace` (Edit → form, Save changes
or Cancel → read view). Links push no state:

```tsx
// Today row, Past notes row, Flagged row, Participant detail action, "Past notes" link: no router state
<Link to={`/participants/${p.id}/notes/${date}`} data-return-key={p.id}>…</Link>
// Read view -> edit form, and Save changes / Cancel -> read view: same note, so replace; the shell copies the record
navigate(editPath, { replace: true });
```

```tsx
// src/components/BackLink/BackLink.tsx
import { Link, useLocation, useNavigate } from 'react-router';
import styles from './TopBar.module.css';

export type BackKey = 'today' | 'pastNotes' | 'flagged' | 'participant' | 'note';
// No NavState: nothing is read from or written to history.state (app-shell.md acceptance criteria).

const LABEL: Record<BackKey, string> = {
  today: 'Today', pastNotes: 'Past notes', flagged: 'Flagged', participant: 'Participant', note: 'Note',
};

export function BackLink({ fallback = { key: 'today' as const, path: '/' } }: { fallback?: { key: BackKey; path: string } }) {
  const location = useLocation();
  const navigate = useNavigate();
  const fromApp = openedFrom.get(location.key);           // the shell's in-memory record (app-shell.md); no history.state
  const back = fromApp ?? fallback;

  return (
    <Link
      to={back.path}
      className={styles.backLink}
      onClick={(e) => {
        if (!fromApp || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        e.preventDefault();
        navigate(-1);                                     // previous entry is the destination: pop, don't push
      }}
    >
      <svg aria-hidden="true" focusable="false" width="20" height="20" viewBox="0 0 20 20">
        <path d="M12.5 4 6.5 10l6 6" fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
      {LABEL[back.key]}
    </Link>
  );
}
```

**Top bar** (publishes its height for `scroll-padding`):

```tsx
// src/components/TopBar/TopBar.tsx
export function TopBar({ compactName, status }: { compactName?: string; status?: React.ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const el = ref.current!;
    const ro = new ResizeObserver(([e]) =>
      document.documentElement.style.setProperty('--topbar-h', `${e.borderBoxSize[0].blockSize}px`));
    ro.observe(el);
    return () => { ro.disconnect(); document.documentElement.style.removeProperty('--topbar-h'); };
  }, []);
  return (
    <div ref={ref} className={styles.bar}>
      <div className={styles.inner}>
        <BackLink />
        <div className={styles.nameSlot}>
          {compactName && <p className={styles.compactName} translate="no">{compactName}</p>}
        </div>
        {status /* <SaveIndicator/> on the form only */}
      </div>
    </div>
  );
}
```

```css
/* TopBar.module.css */
.bar { position: sticky; inset-block-start: 0; z-index: var(--z-sticky);
       background: var(--surface); border-block-end: 1px solid var(--border); }
.inner { display: flex; align-items: center; gap: 0.75rem; min-block-size: 3.5rem;
         max-inline-size: var(--content-max); margin-inline: auto; padding-inline: 1rem; }
.backLink { flex: none; display: inline-flex; align-items: center; gap: 0.25rem;
            min-block-size: 2.75rem; min-inline-size: 2.75rem; padding-inline-end: 0.5rem;
            font-weight: 600; color: var(--link); text-decoration: underline; text-underline-offset: 0.2em; }
.backLink:hover { text-decoration-thickness: 3px; }
.backLink:active { color: var(--link-active); }
.backLink:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
.nameSlot { flex: 1 1 0; min-inline-size: 0; container-type: inline-size; }
.compactName { margin: 0; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
@container (max-width: 6rem) { .compactName { display: none; } }

:global(:root) { scroll-padding-block-start: calc(var(--topbar-h, 0px) + 0.5rem); }
@media (max-height: 30rem) {
  .bar { position: static; }
  .compactName { display: none; }
  :global(:root) { scroll-padding-block-start: 0; }
}
@media (forced-colors: active) {
  .bar { border-block-end-color: CanvasText; }
  .backLink:focus-visible { outline-color: Highlight; }
}
```

**Identity block:**

```tsx
// src/components/IdentityHeader/IdentityHeader.tsx
type Props = { caption?: string; givenName: string; familyName: string; noteDate?: string;
               ref?: React.Ref<HTMLHeadingElement> };           // React 19: ref is a normal prop

export function IdentityHeader({ caption, givenName, familyName, noteDate, ref }: Props) {
  return (
    <div className={styles.identity}>
      <h1 ref={ref} tabIndex={-1} className={styles.heading}>
        {caption && <span className={styles.caption}>{caption} </span>}
        <span className={styles.name} translate="no">{`${givenName} ${familyName}`}</span>
      </h1>
      {noteDate && (
        <p className={styles.date}><time dateTime={noteDate}>{formatLongDate(noteDate)}</time></p>
      )}
    </div>
  );
}
```

```css
/* IdentityHeader.module.css */
.identity { padding-block: 1.25rem 1rem; }
/* Editorial pass: sizes and the breakpoint from foundations.md; the date is the heading's last line, in text colour. */
.heading { margin: 0; font-size: var(--font-size-h1); line-height: 1.2; font-weight: 700;   /* 28 px, 32 px from 40rem */
           overflow-wrap: break-word; hyphens: manual; text-wrap: balance; }
.heading:focus { outline: none; }
.heading:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }
.caption { display: block; font-size: var(--font-size-body); font-weight: 400; color: var(--colour-text-secondary);
           margin-block-end: 0.25rem; }
.name { display: block; }
.date { display: block; margin-block-start: 0.25rem; font-size: var(--font-size-body); font-weight: 400;
        color: var(--colour-text); }                     /* part of the right-day check, so not greyed */
```

**Focus on arrival and the compact-name trigger:**

```ts
export function useFocusOnArrival(ref: React.RefObject<HTMLElement | null>, ready: boolean) {
  const location = useLocation();
  const navType = useNavigationType();
  useEffect(() => {
    if (!ready) return;
    if (navType === 'POP' && location.key === 'default') return;      // first full page load
    const active = document.activeElement;
    if (active && active !== document.body) return;                    // person already moved focus
    ref.current?.focus();
  }, [location.key, ready]);                                           // once per location, not per render
}

export function useScrolledPast(ref: React.RefObject<Element | null>, ready: boolean) {
  const [past, setPast] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!ready || !el) return;
    const barH = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--topbar-h')) || 0;
    const io = new IntersectionObserver(
      ([entry]) => setPast(!entry.isIntersecting && entry.boundingClientRect.top < barH),
      { rootMargin: `-${Math.round(barH)}px 0px 0px 0px` });
    io.observe(el);
    return () => io.disconnect();
  }, [ref, ready]);
  return past;
}
```

**Page composition.** Key the page by note, so React never reuses one participant's state for another:

```tsx
function KeyedByNote({ children }: { children: React.ReactNode }) {
  const { participantId, noteDate } = useParams();
  return <Fragment key={`${participantId}/${noteDate}`}>{children}</Fragment>;
}

export function NoteFormPage() {
  const { participantId, noteDate } = useParams();
  const participant = useQuery({ queryKey: ['participant', participantId], queryFn: () => getParticipant(participantId!) });
  const draft = useQuery({ queryKey: ['noteDraft', participantId, noteDate], queryFn: () => getDraft(participantId!, noteDate!) });
  // No placeholderData / keepPreviousData on either query: a different participant must never flash on screen.
  const ready = participant.isSuccess && draft.isSuccess;
  const h1 = useRef<HTMLHeadingElement>(null);
  useFocusOnArrival(h1, ready);
  const scrolledPast = useScrolledPast(h1, ready);
  const name = ready ? `${participant.data.givenName} ${participant.data.familyName}` : undefined;
  return (
    <>
      <title>{'Grow2Notes – Note'}</title>           {/* React 19 hoists it; one string child; never a name */}
      <TopBar compactName={scrolledPast ? name : undefined} status={<SaveIndicator /* save-indicator spec */ />} />
      <main id="main">
        {ready ? (
          <>
            <IdentityHeader ref={h1} givenName={participant.data.givenName} familyName={participant.data.familyName}
                            noteDate={noteDate} caption={isEditing ? `Editing submitted note (version ${n})` : undefined} />
            {/* banners, then sections */}
          </>
        ) : <PageLoading /* shows nothing for 1 s */ />}
      </main>
    </>
  );
}
```

**Dates.** The note date is a calendar date (`"2026-10-01"`), not an instant, so format it in UTC from `Date.UTC`. Formatting in the device's time zone can show the wrong day.
- **Long form:** build it from `formatToParts`, so every browser gives "Thursday 1 October 2026" without commas.
- **Short forms** in the banners ("Mon 28 Sep 2026", "Wed 30 Sep", "1 Oct"): use fixed English abbreviations. Node 24's ICU formats `en-AU` short dates as "Mon, 28 Sept 2026" (checked on 1 October 2026), which doesn't match the design's copy.

```ts
const LONG = new Intl.DateTimeFormat('en-AU', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
const WD = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MON = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const toUtc = (iso: string) => { const [y, m, d] = iso.split('-').map(Number); return new Date(Date.UTC(y, m - 1, d)); };

export function formatLongDate(iso: string) {                     // "Thursday 1 October 2026"
  const p = Object.fromEntries(LONG.formatToParts(toUtc(iso)).map((x) => [x.type, x.value]));
  return `${p.weekday} ${p.day} ${p.month} ${p.year}`;
}
export function formatShortDate(iso: string, opts = { weekday: true, year: true }) {   // "Mon 28 Sep 2026"
  const d = toUtc(iso);
  return [opts.weekday && WD[d.getUTCDay()], d.getUTCDate(), MON[d.getUTCMonth()], opts.year && d.getUTCFullYear()]
    .filter(Boolean).join(' ');
}
```

**Confirmation identity** (the dialog's mechanics are in the dialog spec):

```tsx
<dialog ref={dlg} aria-labelledby="confirm-title" aria-describedby="confirm-date confirm-flag" className={styles.confirm}>
  <h2 id="confirm-title" ref={title} tabIndex={-1}>
    <span className={styles.lead}>{isToday ? "Submit today's note for" : 'Submit the note for'} </span>
    <span className={styles.name} translate="no">{fullName}</span>
  </h2>
  <p id="confirm-date"><time dateTime={noteDate}>{formatLongDate(noteDate)}</time></p>
  <p id="confirm-flag">{`Flagged for manager: ${isFlagged ? 'Yes' : 'No'}`}</p>
  <button type="button" onClick={submit}>{`Submit note for ${fullName}`}</button>
  <button type="button" onClick={() => dlg.current?.close()}>Go back</button>
</dialog>
// open: dlg.current.showModal(); title.current.focus();
```

**Scroll position on Today.** React Router's `<ScrollRestoration>` writes scroll positions to `sessionStorage` (key `react-router-scroll-positions`): numbers only, no names. Popping back with `navigate(-1)` and serving Today from the in-memory query cache usually lets the browser restore the scroll position without it. Whether to add `<ScrollRestoration>` belongs to the Today spec, checked against 9.6.

---

## Per-screen notes

**Note form, new or draft (4.3).**
- Focus lands on the name after the tap from Today. That spoken name is the screen reader user's equivalent of seeing the large name.
- Don't move focus anywhere else on arrival. Don't autofocus the first tick box: it hides the identity and starts nothing, because a draft only begins at the first change (3.3).

**Note form, earlier-day draft, opened from "Your unfinished drafts" (4.2).**
- The date line shows the draft's own date (for example "Wednesday 30 September 2026"), not today.
- The banner follows directly, so the date shows up twice in different forms. That is intended: the banner explains, the date line identifies.

**Note form, manager's past-day note (3.8, 4.8).**
- Opened from Participant detail → Write past-day note, so the back link reads **Participant**.
- Opened from the history list, it reads **Past notes**.
- The confirmation heading uses **Submit the note for**.

**Note form, editing (3.5).**
- The caption is part of the `<h1>`, so it is announced with the name.
- The number shown follows the design's example. Which version number appears is an open question.
- The back link leaves the pending edit autosaved. It reappears under "Your unfinished drafts" (4.2) when it isn't on today's list.

**Submit confirmation (4.3).**
- On a phone, the name is the same size as the page `<h1>` (2rem). It wraps and is never truncated, and the button text wraps too.
- The dialog shows no back link and no compact name. **Go back** closes it.

**Read view (4.4).**
- Opened from Today → **Today**. From the history list → **Past notes**. From Flagged → **Flagged**, with the review panel underneath (4.6).
- The "Past notes" link in the body and the back link may point to the same place. That's fine, because both use the same name (3.2.4).

**History list (4.4).**
- The caption "Past notes" separates this screen's `<h1>` from the read view's. Otherwise a screen reader user moving between them would hear the same "Jane Citizen" heading twice and couldn't tell whether anything had changed.
- A list heading and the "Show older" behaviour belong to the history-list spec.

**Hand-off to Today.** When the back link pops to Today, Today should put focus back on the row that was opened, and show "Note for Jane Citizen submitted" after a submit. Both are Today's spec. This component only guarantees that the return is a pop where possible.

---

## Anti-patterns to avoid

1. **Showing one participant's name or ticks while another's loads.** This can happen in two ways:
   - `placeholderData: keepPreviousData` (or a shared query key without the participant ID) shows A's name while B's data loads;
   - React Router reusing the page component between two participants carries over local state.

   Key the page by `participantId/noteDate`, and never use placeholder data for identity. This is the most likely way this build itself could cause a wrong-person note.
2. **Truncating the `<h1>` name** with an ellipsis or `line-clamp`. It loses exactly the part that tells similar names apart, and fails 1.4.10 at 320 px. Only the compact copy in the bar may end with "…".
3. **Upper-casing the name with CSS** to match the sketch's "JANE CITIZEN". It is harder to read for second-language readers, and doesn't match "Submit note for Jane Citizen" and "Note for Jane Citizen submitted". The sketch's capitals show emphasis, not a rule.
4. **Names anywhere persistent:** the page title, URL, router state (`history.state` is written to the browser's session history), `localStorage` or `sessionStorage`. Pass only IDs, dates and screen keys (4.0, D22).
5. **`<button onClick={() => navigate(-1)}>` as the back link,** or an unconditional `history.back()`. On a reloaded page or a new tab, it either leaves the app or does nothing. Use a real `href` with the pop as an enhancement.
6. **Pushing the destination when it is already the previous entry.** History becomes Today → Form → Today, and the browser's Back button reopens the form.
7. **Disabling the back link while saving.** There must always be a way out. Send the pending save, or warn only when the save has failed.
8. **A tall sticky header**, for example one holding the large name and date, or two stacked sticky bars. A sticky bar that animates, hides on scroll down, or stays sticky at 400% zoom is also wrong.
9. **A sticky bar with no `scroll-padding`** (F110). A related trap: `overflow: hidden` or `overflow: auto` on an ancestor quietly stops `position: sticky` from working.
10. **Moving focus to the `<h1>` on every render or refetch.** That steals focus from the text box while someone is typing. Focus once per location.
11. **Moving focus to `<body>` or the top of the app on navigation.** Gatsby's participants found resetting focus to the top "very overwhelming".
12. **A live region on the name or the compact name.** It would announce on scroll and duplicate the heading.
13. **Focusing "Submit note for Jane Citizen" when the confirmation opens.** A double tap or double Enter submits without the name being seen.
14. **Formatting the note date in the device's time zone**, or with `en-AU` short options as they come ("Sept", with a comma).
15. **Fluid `vw` font sizes on the name.** They stop text from scaling at 200% (1.4.4).
16. **Adding identity aids the design doesn't specify:** date of birth, a photo, a re-type-the-name step, or a "Wrong participant?" button. See Tensions; nothing is recommended.

---

## Tensions with decisions

1. **Name only, without a second identifier (A5; design.md 6.3: the participant header "Date of birth is left out").**
   - Health IT guidance says a name alone is not enough to identify someone:
     - SAFER 1.3, Strong: "Patient names alone are not sufficient for identification".
     - The Australian hospital standard uses at least three approved identifiers.
   - Grow2Notes shows the name only, by design, to keep date of birth from workers.
   - Context:
     - SAFER is US EHR guidance, and whether the Australian hospital standard applies to NDIS providers is unverified.
     - This is a provider with fewer than 20 workers who know their participants by name. The risk is two participants with the same full name; the history list and confirmation show nothing else to tell them apart.
   - No change recommended.
2. **No photograph (scope list: "attachments or photos"; the owner's simplicity rule).**
   - The one large study of a banner photo found lower odds of wrong-patient orders: aOR 0.57 (Salmasian 2020; single site, observational, emergency department orders, not progress notes).
   - Grow2Notes stores no participant photos.
   - No change recommended.
3. **A passive name check rather than re-entry at Submit (3.9, 4.3 confirmation).**
   - Adelman 2013: re-typing identifiers (OR 0.60) beat a verify-style alert (OR 0.84). The design's confirmation is the verify kind.
   - Re-entry adds a typing step on every note for tired workers on phones, and the study was of hospital ordering, not daily notes.
   - No change recommended.
4. **Generic page titles (4.0) against identity on "all portions" of the interface.**
   - With two tabs open, the tab strip can't show whose note each one is.
   - The in-page name and the "changed on another device or tab" banner (4.3) carry it instead.
   - Adelman 2019 found that allowing several open records did not raise wrong-patient orders.
   - Privacy wins here; the generic title passes 2.4.2.
   - No change recommended.

---

## Sources

- GOV.UK Design System, Back link: https://design-system.service.gov.uk/components/back-link/
- NHS digital service manual, Back link: https://service-manual.nhs.uk/design-system/components/back-link
- GOV.UK Design System, Headings (captions inside `<h1>`): https://design-system.service.gov.uk/styles/headings/
- GOV.UK A to Z style guide (block capitals): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- ONC/ASTP SAFER Guides, Patient Identification (August 2024 edition, 2025 set), Recommended Practices 1.2, 1.3, 1.6 and 2.5: https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf · index: https://healthit.gov/clinical-quality-and-safety/safer-guides/
- Weatherford E. et al., "Developing updated and new guidance to promote reliable patient identification", JAMIA Open, 2026: https://pmc.ncbi.nlm.nih.gov/articles/PMC12772641/
- Adelman J.S. et al., "Understanding and preventing wrong-patient electronic orders: a randomized controlled trial", JAMIA 2013 (abstract checked through Europe PMC): https://pubmed.ncbi.nlm.nih.gov/22753810/
- Adelman J.S. et al., "Effect of Restriction of the Number of Concurrently Open Records in an EHR on Wrong-Patient Order Errors", JAMA 2019: https://pubmed.ncbi.nlm.nih.gov/31087021/
- Salmasian H. et al., "Association of Display of Patient Photographs in the EHR With Wrong-Patient Order Entry Errors", JAMA Netw Open 2020: https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2772798
- Bell S.K. et al., "Frequency and Types of Patient-Reported Errors in EHR Ambulatory Care Notes", JAMA Netw Open 2020: https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2766834
- NHS Standards Directory, Common User Interface standards (Deprecated; ISB 1505 Patient Banner, ISB 1506 Patient Name): https://standards.nhs.uk/published-standards/common-user-interface-standards
- G. Schmidt, "Patient Name, uppercase family name debate" (secondary summary of the NHS CUI rationale): http://www.gregoryschmidt.ca/writing/patient-name-uppercase-family-name-debate
- Australian Commission on Safety and Quality in Health Care, Correct identification and procedure matching (page timed out when fetched; content from search summary, unverified): https://safetyandquality.gov.au/standards/nsqhs-standards/communicating-safety-standard/correct-identification-and-procedure-matching
- Nielsen Norman Group, P. Laubheimer, "Sticky Headers: 5 Ways to Make Them Better" (2021): https://www.nngroup.com/articles/sticky-headers/
- Apple Human Interface Guidelines, Toolbars (large titles): https://developer.apple.com/design/human-interface-guidelines/toolbars
- Android Developers, Principles of navigation (Up and Back): https://developer.android.com/guide/navigation/principles
- Baymard Institute, "4 Design Patterns That Violate 'Back' Button UX Expectations" (2020): https://baymard.com/blog/back-button-expectations
- Gatsby, M. Sutton, "What we learned from user testing of accessible client-side routing techniques" (2019): https://www.gatsbyjs.com/blog/2019-07-11-user-testing-accessible-client-routing/
- WCAG 2.2: https://www.w3.org/TR/WCAG22/
- Understanding 2.4.11 Focus Not Obscured (Minimum), including C43 and F110: https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- Understanding 2.4.2 Page Titled: https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html
- Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WAI-ARIA APG, Dialog (Modal) pattern: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- MDN, `<dialog>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog
- MDN, `translate` global attribute: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/translate
- React 19, `<title>`: https://react.dev/reference/react-dom/components/title
- React Router, ScrollRestoration (`sessionStorage`): https://reactrouter.com/api/components/ScrollRestoration
- Local check: Node.js 24.12 ICU output for `en-AU` date formats, run on 1 October 2026.
- Internal: ui-ux-design skill, sectors/healthcare-safety-critical.md (rules 7, 8, 9, 11) and references/16-accessibility.md (rule 9), 08-forms-validation.md (`beforeunload` only while dirty); ui-build skill, references/00-decision-tables.md (row 70 sticky header) and 04-proven-vs-failed-patterns.md (sticky header budget, loading thresholds).
