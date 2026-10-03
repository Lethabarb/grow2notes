# Visual foundations

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `foundations`. This file sets the shared visual rules that every screen and every other component file builds on: typeface, type sizes, line length, colour and contrast, spacing, tap-target size, the one layout breakpoint, the focus indicator, light-only theming, motion, and the user settings the app must survive (text size, zoom, forced colours, reduced motion).

It does **not** design any one control. Buttons, tick boxes, tags, banners, the save indicator, dialogs and navigation each have their own file in this folder. Where those files invented their own token names, the alias table in "Implementation notes" maps them onto the names here, so there is one set of values for the whole app.

Contrast ratios in this file were computed with the WCAG 2 formula on 1 October 2026. "px" means CSS pixels.

---

## Where it's used

Everywhere. The foundations are the same on every screen; what differs is which parts each screen leans on.

| Screen (design.md) | What it leans on | What differs |
|---|---|---|
| **Sign-in and account setup** (4.1) | Body text, text inputs, primary buttons, error colour, focus ring | One narrow column on every device. The setup key needs a monospace face and the QR code must stay dark-on-light even when a browser darkens pages. |
| **Session warning dialog** (4.0) | Dialog surface, backdrop, button sizes | Appears over any screen. No animation in or out. |
| **App shell and navigation** (4.0) | Wordmark as plain text, nav link size, the "current page" marker, the count badge | The only element that spans the full page width on a laptop. |
| **Today** (4.2) | h1 with the date, search input, 56 px list rows, secondary text for status lines, the attention tag | Status lines are the most-read small text in the app, so they use the darker secondary grey, not a light grey. |
| **Note form** (4.3) | The largest heading (participant name), section headings, 56 px tick rows, the textarea, the placeholder grey, notice/warning/error colours, the sticky top bar | The busiest screen: it uses nearly every token. It is used at the end of a shift, often outdoors or in a car, so contrast margins matter most here. |
| **Submit confirmation** (4.3) | Dialog surface, the name in large bold type | The name must be readable at a glance, because it is the wrong-person check (3.9). |
| **Participant notes and read view** (4.4) | Reading measure for the Guided notes text, ticked and not-ticked icons with words, tags | The only screen with long-form reading, so line length and line height matter most here. |
| **Version history** (4.5) | List rows, secondary text | Same as the read view. |
| **Flagged notes** (4.6) | Tabs with a "current" marker, rows, the review textarea | The current tab must be shown by weight and a bar, not colour alone. |
| **Daily report** (4.7) | Native date field, buttons in the "unavailable" state | The only screen where buttons are routinely unavailable (empty day, Next day on today). |
| **Participants and goals, Common items, Users** (4.8, 4.9, 4.11) | Wider page on a laptop, rows with several action buttons, collapsed sections | "The setup screens use the extra space" (4.0): these use the full page width on a laptop; everything else stays in the reading column. |
| **Guide prompts** (4.10) | Textarea, help text | The help text explains the placeholder, so it must be full-strength body text. |
| **Participant record export** (4.12) | Date fields, radio buttons, a tick box | Native controls that are not tick-box rows need the same size rules. |

---

## Best practice

### Typeface

- **Above a small threshold size, typeface choice makes little measurable difference to reading for people with normal vision.** Reviews of 50+ and 72 studies found no performance difference between serif and sans-serif faces; differences show up only near the threshold of legibility. [Research] (Poole's review, via the ui-ux-design research corpus: https://alexpoole.info/blog/which-are-more-legible-serif-or-sans-serif-typefaces/)
- **No single font is fastest for everyone, and people's preferred font is not their fastest.** Wallace et al. (2022, ACM TOCHI) found readers were 35% faster in their fastest font than their slowest, with no loss of comprehension, but the fastest font differed by person and "one font does not fit all". [Research] https://dl.acm.org/doi/10.1145/3502222, summary: https://readabilitymatters.org/articles/towards-individuated-reading-experiences
- **Atkinson Hyperlegible was designed for low-vision readers and makes easily confused letters distinct** (0/O, 1/I/l, c/e). It is free under the SIL Open Font License, and Atkinson Hyperlegible Next (February 2025) adds seven weights and a variable version. [Convention] https://www.brailleinstitute.org/freefont/, https://en.wikipedia.org/wiki/Atkinson_Hyperlegible
- **No published, independent study showing that Atkinson Hyperlegible improves reading speed or accuracy was found in this pass.** The Braille Institute's font page lists awards and adoption, not studies, and the Wikipedia article cites no outcome research. Development testing with Braille Institute clients is reported in secondary sources only. Treat its benefit as plausible for low-vision readers and unverified for everyone else. [Opinion, based on the absence of evidence]
- **Australian government practice uses the device's own fonts.** The Agriculture Design System (AgDS) uses `-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif`, "to decrease file size and increase page speed". NHS uses Frutiger with an Arial fallback. GOV.UK's own face (GDS Transport) is restricted to GOV.UK services. [Convention] https://design-system.agriculture.gov.au/foundations/tokens/typography, https://service-manual.nhs.uk/design-system/styles/typography, https://design-system.service.gov.uk/styles/typeface/
- **A downloaded font that swaps in after the page has drawn moves the text unless the fallback is metric-matched.** `font-display: swap` causes a layout shift; `size-adjust` and the ascent/descent overrides on the fallback remove it. [Convention] (ui-ux-design corpus, 03-typography; https://developer.chrome.com/blog/font-fallbacks)
- **Weights the device does not have get substituted.** CSS font matching picks the nearest available face. Asking for weight 600 gets Semibold on iPhone, but on Android's Roboto (Regular, Medium, Bold and others, no Semibold) it resolves to Bold. [Standard] https://www.w3.org/TR/css-fonts-4/#font-matching-algorithm

### Type size and scale

- **Reading speed is flat above a "critical print size" and falls sharply below it.** For normal vision this is an x-height of about 0.2° of visual angle, or 1.4 mm at 40 cm. [Research] Legge & Bigelow (2011), *Journal of Vision*: https://pmc.ncbi.nlm.nih.gov/articles/PMC3428264/
- **People hold phones closer than 40 cm on average, but the range is wide.** Bababekova et al. (2011, n = 129 and 100) measured mean distances of 36.2 cm for text messages (range 17.5–58 cm) and 32.2 cm for web pages (range 19–60 cm). [Research] https://europepmc.org/article/MED/21499163
- **What that means in pixels.** One CSS px on a phone is about 0.16 mm, and the system faces have an x-height of about 0.53 em. 16 px text therefore has an x-height of about 1.35 mm, right at the critical size at 40 cm. 18 px gives about 1.52 mm, which keeps text above the critical size out to about 44 cm. [Opinion: this document's arithmetic from the two studies above]
- **Public-sector systems have moved body text up on phones.** GOV.UK Frontend v6 sets body text at 19 px / 25 px line height on small and large screens, saying the scale was "tested and iterated for readability on different devices". NHS uses 16 px on mobile and 19 px on desktop. AgDS's type tokens run 14, 16, 20, 24 px and up, and its token page does not say which one is body text. [Convention] https://design-system.service.gov.uk/styles/type-scale/, https://service-manual.nhs.uk/design-system/styles/typography, https://design-system.agriculture.gov.au/foundations/tokens/typography
- **iOS Safari zooms the page when a form field's text is smaller than 16 px.** The page jumps and the user loses their place. [Convention] https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/
- **WCAG sets no minimum font size.** It requires text to resize to 200% without loss of content or function (1.4.4), and layouts to survive user text-spacing overrides (1.4.12). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html, https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- **Fewer heading levels that differ in fewer ways are easier to rank.** Size is the cue readers treat as strongest for hierarchy. [Research] Williams & Spyridakis (1992), via the ui-ux-design corpus (03-typography).
- **Avoid all-capitals for anything longer than a few words; it reads more slowly than mixed case.** [Research] (ui-ux-design corpus, 03-typography: Woodworth, Tinker)

### Line length and line height

- **Comprehension is best around 55 characters per line; speed rises with longer lines; preference falls.** The 45–75 "rule" is a typographer's judgement, not a finding. [Research] Dyson & Haselgrove (2001) and others, via https://legible-typography.com/en/6-overview-of-research-typography
- **Baymard recommends 50–80 characters per line,** combining Emil Ruder's 50–60, the web convention of up to 75, and WCAG 1.4.8 (AAA)'s 80. [Convention] https://baymard.com/blog/line-length-readability
- **On a phone the measure is fixed by the screen:** about 32–40 characters at 18 px on a 320–375 px screen. There is nothing to tune there. [Opinion: arithmetic]
- **Leading helps scanning and the return to the next line more than raw reading speed;** 1.4–1.5 suits the line lengths used here. WCAG 1.4.12's 1.5 is a value the layout must *survive* a user setting, not a required design value. [Research/Standard] (ui-ux-design corpus, 03-typography), https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html

### Colour and contrast, including outdoor use

- **Text needs 4.5:1 (AA) or 7:1 (AAA); large text needs 3:1; control boundaries and focus indicators need 3:1.** Placeholder text counts as text. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html, https://www.w3.org/WAI/WCAG22/Understanding/contrast-enhanced.html, https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- **Colour must never be the only way information is shown.** [Standard] https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- **4.5:1 is a floor, derived from a 3:1 figure for normal vision multiplied by 1.5 to allow for 20/40 vision.** It does not allow for glare. [Standard] (derivation in the Understanding document above)
- **Glare adds reflected light to both the text and the background, which squashes the ratio.** A simple model that adds the same reflected luminance to both shows low-contrast pairs failing first:

  | Text on white | Normal | Moderate glare (25% of white) | Strong glare (50%) |
  |---|---|---|---|
  | `#0b0c0c` near-black | 19.6:1 | 4.3:1 | 2.8:1 |
  | `#484949` dark grey | 9.0:1 | 3.6:1 | 2.5:1 |
  | `#626262` mid grey | 6.1:1 | 3.1:1 | 2.3:1 |
  | `#767676` (just passes AA) | 4.5:1 | 2.7:1 | 2.1:1 |

  [Opinion: illustrative arithmetic, not a measurement of any screen.] The direction is the point: the more contrast you start with, the more survives in sunlight.
- **In daylight, dark-on-light versus light-on-dark makes no measurable difference; contrast magnitude does.** Dobres et al. (2017) found no polarity effect in daytime conditions. [Research] via https://www.nngroup.com/articles/dark-mode/
- **Government systems keep text near-black and secondary text dark.** GOV.UK: text `#0b0c0c`, secondary text `#484949`, input border `#0b0c0c`. AgDS: text `#313131`, muted `#626262`, action `#00558b`, error `#d10000`, success `#00754e`. [Convention] https://design-system.service.gov.uk/styles/colour/, https://design-system.agriculture.gov.au/foundations/tokens/colour
- **Red/green is the worst pairing for colour-vision deficiency,** and deuteranomaly is the most common form. Status is carried by words here anyway. [Research] (ui-ux-design corpus, 02-colour)

### Dark mode

- **For normal vision, dark text on a light background performs better in most conditions.** Piepenbrock et al. (2013) found light mode better for younger and older adults; Dobres et al. (2017) found light mode better at night and no difference in daytime. [Research] https://www.nngroup.com/articles/dark-mode/
- **The documented exception is cloudy ocular media (for example cataracts),** where light-on-dark reads faster (Legge et al. 1985). [Research] same source.
- **NN/g's recommendation is to default to light mode and offer dark mode as a choice, especially for long-form reading apps.** [Convention] same source. Grow2Notes is short form-filling, not long-form reading.
- **Browsers can darken a light page themselves.** Chrome's Auto Dark Theme "applies an automatically generated dark theme to light themed sites, when the user has opted into dark themes". A page opts out with `color-scheme: only light`, page-wide or per element. Chrome recommends a curated dark theme instead of opting out. [Convention] https://developer.chrome.com/blog/auto-dark-theme. The CSS spec says `only` "forbids the user agent from overriding the color scheme for the element". [Standard] https://www.w3.org/TR/css-color-adjust-1/
- **AgDS has a dark palette, but applies it per section of a page (for example a dark header), not as a user setting.** [Convention] https://design-system.agriculture.gov.au/foundations/tokens/colour
- **Windows contrast themes (forced colours) replace author colours with system colours** and force `box-shadow` to `none`. Anything drawn only with a background or shadow disappears. [Standard] https://www.w3.org/TR/css-color-adjust-1/, https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors

### Spacing

- **Grouping is read from the ratio between gaps, not their absolute size.** Make the gap between groups at least 1.5×, and preferably 2×, the gap inside a group. A heading's space below must be under half its space above, or it reads as belonging to the wrong block. [Research] Oyama (1961) and Kubovy & Wagemans (1995), via the ui-ux-design corpus (04-whitespace-layout)
- **The base unit is arbitrary.** GOV.UK uses 5 px steps, others 4 or 8. What matters is one scale used consistently, with steps far enough apart to tell apart. [Convention] https://design-system.service.gov.uk/styles/spacing/
- **Shrink large spacing on small screens; keep small spacing the same.** GOV.UK keeps units 0–3 (0–15 px) fixed and reduces units 4–9 below 640 px. [Convention] same source.

### Tap targets

- **WCAG 2.5.8 (AA) requires 24 × 24 px,** with exceptions for spacing, an equivalent control, inline links in text, browser-drawn controls and essential presentation. **2.5.5 (AAA) requires 44 × 44 px.** [Standard] https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html, https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html
- **The research behind these numbers recommends larger targets.** Parhi, Karlson & Bederson (2006) recommend about 9.2 mm for single taps; NN/g recommends at least 1 cm × 1 cm. Fingertips are 1.6–2 cm wide (MIT Touch Lab). [Research] https://www.nngroup.com/articles/touch-target-size/
- **Platform guidance:** Android asks for 48 × 48 dp (about 9 mm) with 8 dp between targets. Apple's guidance is 44 × 44 pt. [Convention] https://support.google.com/accessibility/android/answer/7101858. Apple: https://developer.apple.com/design/human-interface-guidelines/accessibility (script-rendered; the 44 pt figure could not be fetched in this pass and is quoted from the ui-ux-design corpus).
- **On a phone, 44 px is about 7 mm and 56 px is about 9 mm.** So 44 px meets A32 and the AAA criterion but sits below the research figure. The most-tapped targets should be bigger. [Opinion: arithmetic, about 0.16 mm per CSS px on phones]

### Breakpoints and layout

- **Set breakpoints where the content breaks, not at device widths;** device widths go out of date. Breakpoints in `rem` follow the user's text size, so someone with large text keeps the one-column layout longer. [Convention] (ui-build corpus, 06-responsiveness-layout)
- **Reflow at 320 px with no sideways scrolling** (the width of a 1280 px laptop at 400% zoom), and **do not lock orientation**. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/reflow.html, https://www.w3.org/WAI/WCAG22/Understanding/orientation.html
- **Published breakpoints disagree.** GOV.UK's responsive spacing changes at one breakpoint, 640 px ("tablet"). AgDS has 576, 768, 992, 1200 and 1600 px and says to start designs at 320 px and add breakpoints "only when necessary". [Convention] https://design-system.service.gov.uk/styles/spacing/, https://design-system.agriculture.gov.au/foundations/tokens/breakpoints
- **Never disable zoom** (`user-scalable=no`, or `maximum-scale` below 5). [Standard] (1.4.4; ui-build corpus, 06-responsiveness-layout)

### Focus indicator

- **2.4.7 (AA): keyboard focus must be visible.** **2.4.11 (AA): the focused element must not be entirely hidden by author content** such as a sticky bar. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html, https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- **2.4.13 (AAA): the indicator must be at least as large as a 2 px perimeter of the component, and change contrast by at least 3:1 between focused and unfocused states.** "A solid outline around the component" of 2 px is "the easiest and most common way" to meet it. An offset is not required but "can help make indicators more visible". [Standard] https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance.html
- **The ring must also reach 3:1 against the colours next to it** (1.4.11). Measure against whatever shows in the offset gap. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- **Use `outline`, not `box-shadow`,** because forced-colours mode removes shadows. **Use `:focus-visible`** so the ring shows for keyboard use but not after a mouse click on a button. **Never fade the ring in.** [Standard/Convention] https://developer.mozilla.org/en-US/docs/Web/CSS/:focus-visible; ui-build corpus, 10-control-states
- **GOV.UK uses yellow (`#ffdd00`) with black**: the yellow contrasts with dark backgrounds and the black with light ones. AgDS uses a purple ring (`#9263de`). [Convention] https://design-system.service.gov.uk/get-started/focus-states/, https://design-system.agriculture.gov.au/foundations/tokens/colour

### Motion and reduced motion

- **`prefers-reduced-motion` reports a device setting** (iOS: Settings > Accessibility > Motion; Android: Remove animations; Windows: Animation effects). MDN says to "remove, reduce, or replace" motion when it is set. [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion
- **2.3.3 (AAA): motion triggered by interaction must be possible to turn off,** unless it is essential. **2.2.2 (A): anything that moves automatically for more than 5 seconds needs a pause control.** [Standard] https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html, https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html
- **Motion must never be the only way something is communicated,** and focus rings and validation messages should appear with no animation at all. [Convention] (ui-build corpus, motion rules)
- **Do not reset reduced motion with `animation: none`;** it stops `animationend` firing. A near-zero duration is safer. [Convention] (ui-build corpus, non-negotiables)

### Platform mechanics that affect every screen

- **`-webkit-text-size-adjust: 100%`** stops iOS enlarging text on its own when the phone is turned sideways. It does not block the user's own zoom. [Convention] https://github.com/necolas/normalize.css/blob/master/normalize.css
- **Safari drops list semantics when `list-style: none` is set.** Add `role="list"` to any styled list where the count matters to screen-reader users. [Convention] https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- **Hover styles should apply only on devices that can hover,** or they "stick" after a tap on a phone. [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/@media/hover

---

## Recommendation for Grow2Notes

### Decisions in one line each

1. **Typeface:** the device's own font, using the AgDS system stack. No downloaded fonts and no Atkinson Hyperlegible.
2. **Body text 18 px** (`1.125rem`) with a 1.5 line height, on phone and laptop. **Nothing smaller than 16 px anywhere**, including tags, hints and timestamps.
3. **Four type sizes in use, two weights (400 and 700), sentence case, no italics.**
4. **Reading measure `40rem`** (about 70 characters at 18 px) for forms and the read view. **Page width `60rem`** for the shell and setup screens.
5. **Palette:** near-black text, a dark secondary grey, one blue action colour, and red, amber and green only for status. **Every text colour reaches 7:1** on the surfaces where it is used, except the placeholder (6.1:1). That margin is for glare.
6. **Light only.** No dark theme, no theme setting, and no `only light` opt-out of browser darkening, apart from the QR code.
7. **Spacing:** one 4 px-based scale in `rem`. Larger gutters and section gaps from 40 rem up.
8. **Tap targets:** at least 44 × 44 px everywhere (A32). 48 px for buttons and nav links; 56 px for the rows tapped most (tick rows, participant rows). At least 8 px between separate targets.
9. **One layout breakpoint at `40rem` (640 px).**
10. **Focus:** a 3 px solid near-black `outline` with a 2 px offset, on `:focus-visible`, never animated. `Highlight` in forced colours.
11. **No motion in v1.** State changes are instant. A reduced-motion safety reset still ships in the base CSS.
12. **Forced colours:** every shape that carries meaning has a real border, so it survives Windows contrast themes.

### Typography

**Font stack** (AgDS, plus the monospace stack only for codes):

```css
--font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
--font-family-code: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
```

Why the device font and not Atkinson Hyperlegible:

- The evidence does not separate good typefaces at these sizes for normal vision (Poole; Wallace et al.). No independent outcome study of Atkinson was found.
- The system faces (San Francisco on iPhone, Roboto on Android, Segoe UI on Windows) are tuned for their screens and familiar to each worker on their own phone. They also cover every script and accent a participant's name might use.
- There is nothing to download, license, host, preload or metric-match, and so no layout shift. This fits design.md §9.5 ("no third-party… fonts") and the strict `font-src 'self'` policy with no extra files.
- Glyph confusion matters in one place, the authenticator setup key. That uses the monospace stack with grouping, which the TOTP setup file owns. Base32 setup keys contain no 0, 1 or lower-case l, so the confusable set is already small.

**Type scale** (all in `rem`, so it follows the user's text size):

| Token | Phone | ≥ 40rem | Weight | Line height | Used for |
|---|---|---|---|---|---|
| `--font-size-h1` | 1.75rem (28 px) | 2rem (32 px) | 700 | 1.25 | One per screen: "Today · Thursday 1 October", the participant's name on the note form and read view, setup screen titles |
| `--font-size-h2` | 1.375rem (22 px) | 1.5rem (24 px) | 700 | 1.25 | Section headings: "1. Goals", "2. Common items", "3. Guided notes", "Your unfinished drafts", "Details", "Goals", "Actions" |
| `--font-size-body` | 1.125rem (18 px) | same | 400; 700 for emphasis and h3 | 1.5 | All body text, labels, tick-row text, inputs, textarea, buttons |
| `--font-size-small` | 1rem (16 px) | same | 400; 700 for the status word | 1.5 | Status lines, hints, help text, the save indicator, tags, timestamps, error-message text under a field |

- **h3** (rare, for example "Archived goals") is body size in bold. That keeps four sizes in use.
- **Headings step at the breakpoint; they do not use `clamp()`.** Two fixed sizes are easier to test and cannot break 200% resize.
- **Weights: 400 and 700 only.** Other files that ask for 600 should use 700, because 600 renders as Semibold on iPhone and Bold on Android.
- **Sentence case everywhere.** No `text-transform: uppercase`. Show participant names exactly as entered (see Per-screen notes).
- **No italics** for prompts, empty states or anything else. Use bold for emphasis, sparingly.
- **Figures:** `font-variant-numeric: tabular-nums` on the save indicator and the count badge only, so "Saved 9:42 am" does not shift sideways when the minute changes.
- **Measure:** `--measure: 40rem`. At 18 px that is about 70 characters, within Baymard's 50–80 and close to the comprehension optimum. On phones the screen sets the measure (32–40 characters) and nothing more is needed.

### Colour

One theme, light. Tokens are named by job, not by hue. Components use these names and nothing else.

| Token | Value | Use | Contrast (WCAG 2) |
|---|---|---|---|
| `--colour-text` | `#0b0c0c` | All body text, headings, labels, values, empty-state sentences | 19.6:1 on white; 17.5:1 on `--colour-surface-muted`; ≥ 15:1 on every tint below |
| `--colour-text-secondary` | `#484949` | Status lines, hints, help text, timestamps, the save indicator, chevrons and other icons | 9.0:1 on white; 8.1:1 on surface-muted; 7.1:1 on pressed |
| `--colour-placeholder` | `#626262` | Guide prompts in the empty Guided notes box only (D34, 4.10) | 6.1:1 on white; 5.5:1 on surface-muted |
| `--colour-page` | `#ffffff` | Page background | — |
| `--colour-surface` | `#ffffff` | Inputs, dialogs, the top bar | — |
| `--colour-surface-muted` | `#f3f2f1` | Read-only fields, the "unavailable" button fill | — |
| `--colour-hover` | `#f3f2f1` | Row and secondary-button hover (laptop) | Text stays ≥ 7:1 |
| `--colour-pressed` | `#e5e4e2` | Row pressed state | Text 15.4:1; secondary 7.1:1 |
| `--colour-border-control` | `#0b0c0c` | 2 px borders of text inputs, textareas, date fields, tick boxes and radios | 19.6:1 (needs 3:1) |
| `--colour-divider` | `#b1b4b6` | 1 px rules between list rows and sections. Decorative only: spacing does the grouping | 2.1:1 (not required) |
| `--colour-action` | `#00558b` | Links, primary button fill, secondary button text and border, the current-page and current-tab bar, the notice banner edge | 7.9:1 on white; 7.0:1 on surface-muted |
| `--colour-action-hover` | `#003e66` | Primary hover, link hover | 11.2:1 |
| `--colour-action-pressed` | `#002d4d` | Primary pressed | 14.2:1 |
| `--colour-on-action` | `#ffffff` | Text on action and error fills | 7.9:1 on action; 8.1:1 on error |
| `--colour-action-tint` | `#e8f1f8` | Secondary button hover; notice banner fill | Action text 6.9:1; body text 17.1:1 |
| `--colour-action-tint-pressed` | `#d2e2ef` | Secondary button pressed | Action text 5.9:1 |
| `--colour-focus` | `#0b0c0c` | Focus ring | 19.6:1 against white and ≥ 14:1 against every tint |
| `--colour-error` | `#a4000f` | Error text, error border, the error-summary border, the warning (destructive) button fill | 8.1:1 on white; 7.1:1 on its tint |
| `--colour-error-hover` / `-pressed` | `#8a000d` / `#6e000a` | Warning button hover and pressed | White text 10.1:1 / 12.6:1 |
| `--colour-error-tint` | `#fdecea` | Not a default fill. Available if a component needs it | Body 17.1:1 |
| `--colour-success` | `#00703c` | Success banner edge and tick icon (non-text) | 6.2:1 on white; 5.5:1 on its tint |
| `--colour-success-tint` | `#e7f4ec` | Success banner fill | Body 17.3:1 |
| `--colour-attention-edge` | `#b35900` | Warning banner edge ("Not saved"), attention tag border | 4.8:1 on white; 3.8:1 on the strong tint (needs 3:1) |
| `--colour-attention-tint` | `#fff4d6` | Warning banner fill | Body 17.9:1 |
| `--colour-attention-tint-strong` | `#ffe2a8` | Fill of the **Flagged** and **To review** tags | — |
| `--colour-attention-text` | `#5c3a00` | Text on the strong tint | 8.1:1 |
| `--colour-neutral-tint` | `#e8eaed` | Fill of neutral tags (Edited, Reviewed, Past-day note) | — |
| `--colour-neutral-text` | `#1f2328` | Text on the neutral tint | 13.1:1 |
| `--colour-badge` / `--colour-on-badge` | `#0b0c0c` / `#ffffff` | The To review count pill | 19.6:1 |
| `--colour-backdrop` | `rgb(11 12 12 / 0.6)` | `<dialog>::backdrop` | — |

Rules that go with the palette:

- **Text at 7:1 or better.** Every text colour above clears 7:1 (WCAG AAA 1.4.6) on the surfaces listed, except the placeholder. The placeholder sits at 6.1:1 so it is clearly lighter than typed text (D34's grey) while still well above the 4.5:1 that design.md 4.10 requires. The extra margin is for glare and tired eyes, not for compliance.
- **No light grey text anywhere.** Not for hints, timestamps, "No participants yet." or disabled labels. Empty-state and loading sentences use `--colour-text` or `--colour-text-secondary`, never anything lighter.
- **Red means error or destructive, amber means "needs attention", green means "done".** Blue means "you can act on this" and is also used for neutral notices. Never introduce another status hue.
- **The words always carry the meaning** (A32, 1.4.1). Colour is a second channel: "There is a problem", "Not saved", "Flagged", "Submitted", "Note for Jane Citizen submitted".
- **An error border alone is too weak a signal.** Black to dark red is only a 2.4:1 change, so an error is shown by the message text, the red bar beside the field group and the red border together (form-validation file).
- **The focus ring is near-black and needs the 2 px offset.** Near-black against the blue button fill is only 2.5:1, so the gap of page colour between the button and the ring is what makes the ring pass 1.4.11. Never set `outline-offset` to 0 on a filled button.
- **No brand colour.** The wordmark "Grow2Notes" is plain text in `--colour-text`. Nothing from the parent company (name, colours, fonts) appears in tokens, file names or comments (D42).

### Theme: light only, no dark mode

**Recommendation: ship light only, with no toggle and no automatic dark theme.**

- **The evidence favours light for these users.** Light mode performed as well or better in daytime and better at night for normal vision (Piepenbrock 2013; Dobres 2017). Outdoors, contrast magnitude matters, not polarity.
- **The group dark mode helps (cloudy ocular media) is still served,** by tools the user already controls: Windows contrast themes (supported below), iOS Smart Invert, Android colour inversion, and browser page darkening (left switched on, see below).
- **NN/g recommends offering dark mode for long-form reading.** Grow2Notes sessions are short: tick some boxes, write one note. That recommendation does not fit this use.
- **It keeps the build small.** A dark theme doubles the colour table and every state of every component must be checked twice. A user setting would also need storing (a server-side preference, because nothing may be stored on the device, D22) and adds a setting nobody asked for.

How to declare it:

- `:root { color-scheme: light; }` and `<meta name="color-scheme" content="light">`. Native controls (date picker, scrollbars, `<dialog>`) then stay light even when the phone is in dark mode, so the page is consistent.
- **Do not add `only`.** A worker who has asked their browser to darken websites gets that (Chrome's Auto Dark Theme; Samsung Internet has a similar page-darkening mode, unverified detail). WCAG conformance is judged on the author's colours, and the user's own choice is respected.
- **One exception:** put `color-scheme: only light` on the **QR code container** in account setup, so automatic darkening never inverts the code an authenticator app has to scan (Chrome's documented per-element opt-out). Verify on a phone with dark mode and page darkening switched on.
- **Test with page darkening switched on** (see the test list). If darkening hides a tick, the focus ring or the placeholder, fix that element's CSS (borders and `currentColor` survive darkening better than background images). Do not ship a dark theme as the fix.

### Spacing and layout

**Scale** (4 px base, in `rem` so spacing grows with text size):

| Token | Value | Typical use |
|---|---|---|
| `--space-1` | 0.25rem (4 px) | Between a tag and its neighbour; icon to text inside a tag |
| `--space-2` | 0.5rem (8 px) | Label to field; between stacked buttons; minimum gap between separate targets |
| `--space-3` | 0.75rem (12 px) | Tick-box to its text; inside banners; heading to its content |
| `--space-4` | 1rem (16 px) | Phone page gutter; between fields in a group; paragraph spacing |
| `--space-5` | 1.5rem (24 px) | Between field groups |
| `--space-6` | 2rem (32 px) | Between page sections on a phone; laptop gutter |
| `--space-7` | 3rem (48 px) | Between page sections from 40 rem up |
| `--space-8` | 4rem (64 px) | Bottom of the page, below the last button |

**Layout tokens:**

| Token | Phone | ≥ 40rem |
|---|---|---|
| `--page-gutter` | `--space-4` (16 px) | `--space-6` (32 px) |
| `--section-gap` | `--space-6` (32 px) | `--space-7` (48 px) |
| `--measure` | 40rem (fills the screen on a phone) | 40rem |
| `--page-max` | — | 60rem (960 px at default text size) |

- **The heading rule:** space above a section heading is `--section-gap`; space below it is `--space-3`. That is at least 2.6:1 above to below, so each heading clearly belongs to the block under it.
- **Inside a group, gaps are at most half the gap between groups.** For example: label to field 8 px, field to field 16 px, group to group 24 px, section to section 32 or 48 px.
- **Laptop layout:** the header and the page container are centred at `--page-max`. The content column (`--measure`) is left-aligned inside it, so the navigation and the content share a left edge. Setup screens (4.8, 4.9, 4.11) use the full `--page-max` width. Everything else stays in the `--measure` column.
- **Radii:** `--radius-s: 0.25rem` (inputs, buttons, tags); `--radius-m: 0.5rem` (dialogs, banners). No pills, except the count badge.
- **Borders:** `--border-control: 2px`, `--border-divider: 1px`. Borders stay in `px`; they do not need to grow with text.
- **No shadows.** Dialogs get a 1 px `--colour-text` border and the backdrop. Shadows add nothing on a flat, light UI and vanish in forced colours anyway.
- **Nothing is fixed or sticky** except the note form's top bar, which the save-indicator file owns along with its 2.4.11 `scroll-margin` handling. No sticky Submit bar, no fixed footer, no floating buttons.

### Tap targets

| Target | Minimum size | Why |
|---|---|---|
| Every interactive target (A32) | `min-block-size: max(2.75rem, 44px)`, and at least 44 px wide | `max()` keeps 44 px even if a user sets a small default font. Meets 2.5.8 AA and 2.5.5 AAA. |
| Buttons, nav links, tabs, the search input, date fields | `max(3rem, 48px)` | Matches Android's 48 dp (about 9 mm). These are thumb targets. |
| Tick-box rows, participant rows, history and flagged rows | `3.5rem` (56 px) | The most-tapped targets; about 9 mm, close to the 9.2 mm research figure. Owned by the checkbox-list and list-row files. |
| Gap between separate targets | `--space-2` (8 px) | Android 8 dp. Full-width rows separated by a divider count as separate targets with no gap needed, because each row is already well above 24 px. |
| Links inside a sentence | Exempt (2.5.8 inline exception) | Still keep them out of sentences where possible: "Past notes", "Version history" and "Show older" are standalone links with a 44 px box. |

The whole row or label is the target, never just the 20 px box inside it. Visual size and hit area can differ: a small chevron sits inside a 56 px row.

### Breakpoint

**One breakpoint: `@media (min-width: 40rem)`** (640 px at default text size).

- **Below 40rem:** one column, 16 px gutters, full-width primary buttons and tick rows, stacked action buttons on setup rows.
- **From 40rem:** larger gutters and section gaps, larger h1 and h2, buttons sized to their label and placed side by side, dialogs at a fixed width (confirm-dialog file), setup rows with their actions on the same line.
- **Why 40rem:** design.md 4.0 asks for one phone-first column that "gets wider" on a laptop. At about 40 rem the reading column reaches its full measure, and from there only the margins change. It also matches GOV.UK's 640 px. It is in `rem`, so a user with large default text keeps the phone layout until there is really room for more.
- **`min-width`, not range syntax.** It costs nothing and works on older iPhones that some workers may still use.
- **Other files that used `48rem` should use `40rem`.** The participant-list file's `48rem` only caps the list width, so behaviour is unchanged.
- **Short screens:** the save-indicator file's `@media (max-height: 30rem)` (landscape phone, or a laptop at 200% zoom), which un-sticks the top bar, is the only other media query on size.

### Focus indicator

```css
:where(a, button, input, select, textarea, summary, [tabindex]):focus-visible {
  outline: var(--focus-width) solid var(--colour-focus);   /* 3px, #0b0c0c */
  outline-offset: var(--focus-offset);                      /* 2px */
}
```

- **Near-black, 3 px, 2 px offset, on every focusable element,** with no transition.
- **Why near-black rather than GOV.UK's yellow-and-black or AgDS's purple:** there is only one, light theme. Near-black gives the largest state change on every surface the app uses (19.6:1 against white, at least 14:1 against every tint), holds up best in glare, uses no extra hue, and is one declaration. The yellow in GOV.UK's pattern exists for dark backgrounds, which Grow2Notes does not have.
- **It meets 2.4.7 (AA) and 1.4.11 (AA),** and also **2.4.13 (AAA):** a 3 px ring around the whole perimeter changes the same pixels from white to near-black.
- **Inset rings** (the participant-row file draws the ring on `::after` inset by 3 px) are fine at 3 px. 2.4.13 needs inset rings to be thicker than 2 px.
- **Text inputs show the ring when tapped too.** That is browser behaviour and helps: it marks the field being typed in.
- **Forced colours:** `outline-color: Highlight`. Never replace the outline with `box-shadow`.
- **Programmatic focus targets** (page headings focused after navigation, the error summary) follow the app-shell and form-validation files.

### Motion

- **No animation in v1.** No transitions on hover or press, no animated reveals (the flag reason, archived sections and banners appear instantly), no spinners, no skeletons, no page transitions, and no smooth scrolling (`scroll-behavior: auto`). Busy and loading states are words ("Saving…", "Submitting…", "Loading…"), as the save-indicator and primary-actions files already specify.
- **Why:** nothing on these screens needs motion to be understood. Instant changes are the fastest feedback, need no reduced-motion variant, and trivially meet 2.3.3 and 2.2.2.
- **Safety net:** the base CSS still includes a `prefers-reduced-motion` reset with a near-zero duration (not `animation: none`), so anything a library adds later is neutralised.
- **Native behaviour stays.** `<details>` and `<dialog>` open instantly by default; do not add open or close animations to them.

### States: foundation-level rules every component follows

| State | Visual rule | Never |
|---|---|---|
| **Default** | Tokens above. Controls have a 2 px `--colour-border-control` border or a solid fill. | Borderless inputs, or controls identified only by a background tint |
| **Hover** (laptop only) | Inside `@media (hover: hover)`: rows and secondary buttons go to `--colour-hover`; primary to `--colour-action-hover`; links to `--colour-action-hover` with a 3 px underline | Hover-only information; hover styles that stick on touch |
| **Focus** | The 3 px near-black ring, 2 px offset | `outline: none` without a replacement; `box-shadow` rings; transitions |
| **Active (pressed)** | Rows `--colour-pressed`; primary `--colour-action-pressed`; secondary `--colour-action-tint-pressed`; applied instantly. Because every control has a pressed style, `-webkit-tap-highlight-color: transparent` is set globally. | Pressed feedback that waits for the network |
| **Unavailable** (design.md's "disabled") | `--colour-surface-muted` fill, `--colour-text-secondary` label (8.1:1), 2 px **dashed** border, `cursor: not-allowed` (primary-actions file) | Faded or low-contrast labels; colour as the only cue |
| **Error** | `--colour-error` message text in bold beside the field, a 4 px red bar on the field group, a red field border, and the error summary (form-validation file) | Red border alone; red text without the word for the problem |
| **Loading / busy** | Words in `--colour-text-secondary` (or the button's own label). No placeholder shapes. | Spinners, skeletons, shimmer |
| **Empty** | The design.md sentence in `--colour-text`, body size, upright | Grey or italic empty-state text; illustrations |
| **Read-only** | Values as plain text in `--colour-text`. A read-only field keeps its border on `--colour-surface-muted` (guided-notes file). | `disabled` for read-only content (it cannot be selected or copied) |
| **Current** (nav item, tab) | Bold plus a 4 px `--colour-action` bar (app-shell and tabs) | Colour change alone |

### Phone and laptop

| | Phone (< 40rem) | Laptop (≥ 40rem) |
|---|---|---|
| Text | 18 px body, 16 px small, h1 28 px, h2 22 px | Same body; h1 32 px, h2 24 px |
| Gutters / section gaps | 16 px / 32 px | 32 px / 48 px |
| Content width | Full width minus gutters | `--measure` (40 rem) column; setup screens up to `--page-max` (60 rem) |
| Targets | 44 / 48 / 56 px as above | Same. Mouse users get no smaller targets; there is no density mode. |
| Hover styles | None (`hover: none` devices) | Yes |
| Orientation | Portrait and landscape both work. Never locked (1.3.4). | — |

### Copy, dates and times

Foundations owns the shared text formats, because every screen uses them.

- **Times:** Melbourne time, "4:12 pm": hour without a leading zero, lower-case am/pm, and a **non-breaking space** before am/pm so "pm" never wraps onto a line of its own.
- **Dates:** "Thursday 1 October 2026" in full; "Mon 28 Sep 2026" and "Wed 30 Sep" in banners; "1 Oct" for written-on dates; "1 Oct 2026, 5:03 pm" in version history. **No commas between the weekday and the day,** as in design.md.
- **Build them from `Intl.DateTimeFormat('en-AU', { timeZone: 'Australia/Melbourne', … }).formatToParts()`,** not from `format()`. `format()` output differs between browsers' ICU versions; for example, it gives "Thu, 1 Oct 2026" with a comma for the short weekday form. One `formatDate.ts` module with unit tests covers every format above.
- **Sentence case** for every heading, label, button and tab ("Submit note", "Flag for manager", "Download Word"). Australian spelling.
- **App name:** "Grow2Notes", exactly that, in the wordmark and page titles. Never the parent company's name (D42).

### Accessibility

- **Semantics:** `<html lang="en-AU">` (3.1.1), so screen readers use Australian English pronunciation and spell-check dictionaries can follow it. The base CSS keeps real headings, lists and landmarks; if a reset removes list bullets, the component adds `role="list"`.
- **ARIA:** none at foundation level. A `.visually-hidden` utility class is provided for the hidden text other files use (the badge's ", 3 to review", "Error:" prefixes).
- **Keyboard:** every focusable element shows the ring; DOM order is visual order (no `order` or `row-reverse`).
- **Screen reader announcements:** none at foundation level.
- **User settings the app must survive** (test each):
  - 200% text size (browser default font size at 32 px), and 200% and 400% page zoom on a laptop. `rem` sizing and the `rem` breakpoint handle this. No fixed heights; use `min-block-size`.
  - 1.4.12 text-spacing overrides: no truncation and no ellipsis on names, goal wording or buttons. Long unbroken strings (emails, names) wrap: `overflow-wrap: break-word` globally, and `overflow-wrap: anywhere` plus `min-inline-size: 0` on text inside flex or grid rows.
  - Windows contrast themes: borders on every meaningful shape; `Highlight` focus; `GrayText` for unavailable controls; SVG icons drawn in `currentColor`; no meaning in background images.
  - Reduced motion: nothing moves anyway.
  - iOS and Android at their largest text setting, and Safari's page zoom at 200%.
- **WCAG 2.2 criteria these foundations meet or enable:** 1.3.4 Orientation; 1.4.1 Use of Color; 1.4.3 Contrast (Minimum); 1.4.4 Resize Text; 1.4.10 Reflow; 1.4.11 Non-text Contrast; 1.4.12 Text Spacing; 2.4.7 Focus Visible; 2.4.11 Focus Not Obscured (Minimum), with the sticky-bar handling in the save-indicator file; 2.5.8 Target Size (Minimum); 3.1.1 Language of Page. They also meet these AAA criteria at no extra cost: 1.4.6 Contrast (Enhanced) for all text except the placeholder; 2.3.3 Animation from Interactions; 2.4.13 Focus Appearance; 2.5.5 Target Size (Enhanced).

### Implementation notes (React 19, native HTML, CSS Modules)

**Files**

```
src/styles/tokens.css     custom properties on :root (global, imported once)
src/styles/base.css       element defaults, focus, forced colours, reduced motion, utilities
src/main.tsx              import './styles/tokens.css'; import './styles/base.css'; before <App/>
src/lib/formatDate.ts     the date and time formats above, with unit tests
src/**/X.module.css       components: var(--…) only; no raw hex values, no other font sizes
```

- **No CSS framework, CSS-in-JS library, icon font or web font.** CSS-in-JS libraries inject `<style>` elements, which the production CSP (`style-src 'self'`, §9.7) blocks. Vite extracts CSS Modules into files, which the CSP allows. In development, Vite injects `<style>` tags, but the CSP is not applied there.
- **Avoid the React `style` prop.** CSSOM writes are not blocked by CSP, but keeping all styles in files keeps every value in one place. Verify in the CSP integration test with a real browser if a library sets inline styles.
- **Icons:** inline `<svg aria-hidden="true" focusable="false">` with `fill`/`stroke="currentColor"`, sized in `em`. No `data:` URIs: `img-src 'self'` blocks them, and `assetsInlineLimit: 0` is already set.
- **Media queries cannot read custom properties.** Write `40rem` literally. A one-line CI check keeps it to one breakpoint: it looks **only inside `@media` rules** and fails on any `min-width:` other than `40rem` or any `max-height:` other than `30rem`. Layouts that must adapt to their own width use wrapping flex rows instead of a second breakpoint (the ListEditor rows, the Daily report date row, the archived-participant notice). The CI check also fails on any `@container` rule, with one allowed exception: the note form's compact-name slot, and only if the owner approves that feature (note-form.md). (Editorial pass: participants.md's two container queries were replaced by wrapping flex rows.)

**`index.html` head**

```html
<html lang="en-AU">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light">
```

No `maximum-scale`, no `user-scalable=no`, and no `viewport-fit=cover`, so there are no safe-area insets to handle.

**`tokens.css` (sketch)**

```css
:root {
  color-scheme: light;
  accent-color: var(--colour-action);        /* any native radio/checkbox not custom-drawn */

  --font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
  --font-family-code: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
  --font-size-small: 1rem;
  --font-size-body: 1.125rem;
  --font-size-h2: 1.375rem;
  --font-size-h1: 1.75rem;
  --line-height-body: 1.5;
  --line-height-heading: 1.25;

  --colour-text: #0b0c0c;            /* 19.6:1 on white */
  --colour-text-secondary: #484949;  /* 9.0:1 */
  --colour-placeholder: #626262;     /* 6.1:1, D34 grey */
  --colour-page: #ffffff;
  --colour-surface: #ffffff;
  --colour-surface-muted: #f3f2f1;
  --colour-hover: #f3f2f1;
  --colour-pressed: #e5e4e2;
  --colour-border-control: #0b0c0c;
  --colour-divider: #b1b4b6;
  --colour-action: #00558b;          /* 7.9:1 */
  --colour-action-hover: #003e66;
  --colour-action-pressed: #002d4d;
  --colour-on-action: #ffffff;
  --colour-action-tint: #e8f1f8;
  --colour-action-tint-pressed: #d2e2ef;
  --colour-focus: #0b0c0c;
  --colour-error: #a4000f;           /* 8.1:1 */
  --colour-error-hover: #8a000d;
  --colour-error-pressed: #6e000a;
  --colour-error-tint: #fdecea;
  --colour-success: #00703c;
  --colour-success-tint: #e7f4ec;
  --colour-attention-edge: #b35900;
  --colour-attention-tint: #fff4d6;
  --colour-attention-tint-strong: #ffe2a8;
  --colour-attention-text: #5c3a00;  /* 8.1:1 on the strong tint */
  --colour-neutral-tint: #e8eaed;
  --colour-neutral-text: #1f2328;
  --colour-badge: #0b0c0c;
  --colour-on-badge: #ffffff;
  --colour-backdrop: rgb(11 12 12 / 0.6);

  --space-1: 0.25rem; --space-2: 0.5rem; --space-3: 0.75rem; --space-4: 1rem;
  --space-5: 1.5rem;  --space-6: 2rem;   --space-7: 3rem;    --space-8: 4rem;
  --page-gutter: var(--space-4);
  --section-gap: var(--space-6);
  --measure: 40rem;
  --page-max: 60rem;

  --target-min: max(2.75rem, 44px);
  --target-button: max(3rem, 48px);
  --target-row: 3.5rem;

  --radius-s: 0.25rem;
  --radius-m: 0.5rem;
  --border-control: 2px;
  --border-divider: 1px;
  --focus-width: 3px;
  --focus-offset: 2px;
}

@media (min-width: 40rem) {
  :root {
    --font-size-h1: 2rem;
    --font-size-h2: 1.5rem;
    --page-gutter: var(--space-6);
    --section-gap: var(--space-7);
  }
}
```

**`base.css` (sketch)**

```css
*, *::before, *::after { box-sizing: border-box; }

html { -webkit-text-size-adjust: 100%; text-size-adjust: 100%; }

body {
  margin: 0;
  font-family: var(--font-family);
  font-size: var(--font-size-body);
  line-height: var(--line-height-body);
  color: var(--colour-text);
  background: var(--colour-page);
  overflow-wrap: break-word;
  -webkit-tap-highlight-color: transparent;   /* every control has its own pressed style */
}

h1, h2, h3 { line-height: var(--line-height-heading); margin: 0 0 var(--space-3); }
h1 { font-size: var(--font-size-h1); }
h2 { font-size: var(--font-size-h2); }
h3 { font-size: var(--font-size-body); }
p  { margin: 0 0 var(--space-4); }

button, input, select, textarea { font: inherit; color: inherit; }
input, select, textarea { font-size: max(1em, 16px); }      /* never triggers iOS zoom */
::placeholder { color: var(--colour-placeholder); opacity: 1; }   /* Firefox fades it otherwise */

a {
  color: var(--colour-action);
  text-decoration-thickness: max(1px, 0.0625em);
  text-underline-offset: 0.15em;
}
@media (hover: hover) {
  a:hover { color: var(--colour-action-hover); text-decoration-thickness: max(3px, 0.1875em); }
}

:where(a, button, input, select, textarea, summary, [tabindex]):focus-visible {
  outline: var(--focus-width) solid var(--colour-focus);
  outline-offset: var(--focus-offset);
}

.visually-hidden {
  position: absolute !important; inline-size: 1px; block-size: 1px;
  margin: -1px; padding: 0; overflow: hidden; clip-path: inset(50%);
  white-space: nowrap; border: 0;
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}

@media (forced-colors: active) {
  :where(a, button, input, select, textarea, summary, [tabindex]):focus-visible {
    outline-color: Highlight;
  }
  [aria-disabled="true"] { color: GrayText; border-color: GrayText; }
}
```

**Contrast test** (Vitest; cheap, and stops a later token edit from quietly breaking contrast):

```ts
// src/styles/tokens.test.ts
import css from './tokens.css?raw';

const hex = (name: string) => {
  const m = css.match(new RegExp(`--${name}:\\s*(#[0-9a-f]{6})`, 'i'));
  if (!m) throw new Error(`missing --${name}`);
  return m[1];
};
const lum = (h: string) => {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255)
    .map(c => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const ratio = (a: string, b: string) => {
  const [hi, lo] = [lum(hex(a)), lum(hex(b))].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};

test.each([
  ['colour-text', 'colour-page', 7],
  ['colour-text-secondary', 'colour-page', 7],
  ['colour-text-secondary', 'colour-surface-muted', 7],
  ['colour-text-secondary', 'colour-pressed', 7],
  ['colour-placeholder', 'colour-surface', 4.5],
  ['colour-action', 'colour-page', 7],
  ['colour-on-action', 'colour-action', 7],
  ['colour-on-action', 'colour-error', 7],
  ['colour-error', 'colour-page', 7],
  ['colour-attention-text', 'colour-attention-tint-strong', 7],
  ['colour-neutral-text', 'colour-neutral-tint', 7],
  ['colour-border-control', 'colour-page', 3],
  ['colour-attention-edge', 'colour-attention-tint-strong', 3],
  ['colour-focus', 'colour-attention-tint', 3],
] as const)('%s on %s ≥ %s:1', (fg, bg, min) => {
  expect(ratio(fg, bg)).toBeGreaterThanOrEqual(min);
});
```

**Alias table: names used in the other component files → the names here**

| Used in other files | Use instead |
|---|---|
| `--colour-text`, `--color-text`, `--text` | `--colour-text` |
| `--colour-text-secondary`, `--text-secondary`, `--colour-icon` | `--colour-text-secondary` |
| `--colour-surface`, `--color-surface`, `--surface`, `--colour-input-bg`, `--color-field` | `--colour-surface` |
| `--colour-surface-muted`, `--action-unavailable-bg` | `--colour-surface-muted` |
| `--colour-input-border`, `--color-input-border`, `--border-input`, `--border-strong`, `--color-border-strong`, `--border-hover` | `--colour-border-control` (no hover change on input borders) |
| `--colour-border`, `--border`, `--colour-divider`, `--divider` | `--colour-divider` |
| `--colour-row-hover`, `--row-hover`, `--surface-hover`, `--color-surface-hover`, `--action-secondary-bg-hover` | `--colour-hover` (secondary buttons: `--colour-action-tint`) |
| `--colour-row-active`, `--colour-pressed`, `--pressed` | `--colour-pressed` |
| `--colour-focus`, `--color-focus`, `--focus`, `--focus-ring` | `--colour-focus` |
| `--colour-error`, `--color-error`, `--error`, `--colour-error-text`, `--color-error-text`, `--action-warning-bg` | `--colour-error` |
| `--action-warning-bg-hover` / `-active` | `--colour-error-hover` / `--colour-error-pressed` |
| `--action`, `--link`, `--colour-brand`, `--action-primary-bg`, `--btn-primary-bg`, `--action-secondary-fg`, `--action-secondary-border`, `--colour-notice-edge` | `--colour-action` |
| `--action-primary-bg-hover`, `--btn-primary-bg-hover`, `--color-primary-hover` | `--colour-action-hover` |
| `--action-primary-bg-active`, `--btn-primary-bg-active`, `--color-primary-active` | `--colour-action-pressed` |
| `--action-primary-fg`, `--btn-primary-text`, `--action-warning-fg` | `--colour-on-action` |
| `--action-secondary-bg-active` | `--colour-action-tint-pressed` |
| `--colour-notice-tint` | `--colour-action-tint` |
| `--action-unavailable-fg`, `--action-unavailable-border` | `--colour-text-secondary` |
| `--colour-success-edge`, `--colour-success-tint` | `--colour-success`, `--colour-success-tint` |
| `--warning-surface`, `--warning-border`, `--warning-text` | `--colour-attention-tint`, `--colour-attention-edge`, `--colour-text` |
| `--tag-attention-bg`, `--colour-tag-flag-bg`, `--tag-attention-text`, `--colour-tag-flag-border` | `--colour-attention-tint-strong`, `--colour-attention-tint-strong`, `--colour-attention-text`, `--colour-attention-edge` |
| `--tag-neutral-bg`, `--tag-neutral-text` | `--colour-neutral-tint`, `--colour-neutral-text` |
| `--badge-bg`, `--colour-badge-bg`, `--badge-fg`, `--colour-badge-text` | `--colour-badge`, `--colour-on-badge` |
| `--text-lg`, `--font-size-h2` | `--font-size-h2` |
| `--form-max-width` | `--measure` |
| `--page-max`, `--page-gutter`, `--radius-m`, `--space-2/3/4/6` | Same names (values as above) |
| `--banner-edge`, `--banner-tint`, `--top-bar-height`, `--bar-height` | Component-local; they stay in their own files |

The status-messages file asks for "light and dark values" for its tokens. Only light values are needed.

**Manual test list (add to M6's accessibility pass)**

1. 320 px wide: no sideways scrolling on any screen, including Users with long email addresses.
2. Laptop at 200% and 400% zoom; browser default text size at 200%.
3. iPhone at the largest text setting and Safari page zoom at 200%; Android at the largest font and display size.
4. Windows contrast theme (or Edge or Chrome forced-colours emulation): every tick, tag, badge, banner edge, unavailable button and focus ring is still visible.
5. Phone in dark mode with page darkening on (Chrome and Samsung Internet): the page is usable, and the QR code is not inverted.
6. Reduced motion on: nothing moves (nothing should move anyway).
7. **Outdoors:** the note form on a real phone in daylight at the phone's automatic brightness. Can the placeholder, status lines, tick state and focus ring be seen?
8. Keyboard only on a laptop: the ring is visible on every stop, including inside banners and the dialog.

---

## Per-screen notes

- **Sign-in and account setup (4.1).** One `--measure` column on every device, left-aligned inside the page container on a laptop. Inputs inherit 18 px, so iOS never zooms (the email, password, 6-digit code and setup key fields matter most). The QR code is shown on `--colour-surface` with its quiet zone, inside a container with `color-scheme: only light`. The setup key uses `--font-family-code`; its grouping and letter spacing belong to the TOTP setup file. The fixed sign-in failure message uses `--colour-error`, with words (A25).
- **Session warning (4.0).** `<dialog>` on `--colour-surface` with a 1 px `--colour-text` border, `--radius-m`, and `--colour-backdrop`. "You'll be signed out in 2 minutes" in body size; **Stay signed in** at 48 px. No animation.
- **App shell (4.0).** The wordmark is the text "Grow2Notes" in `--colour-text`, bold, at body size or slightly larger; no logo image. Nav links are 48 px tall. The current item is bold with a 4 px `--colour-action` bar. The count badge uses `--colour-badge` with tabular figures. The header spans `--page-max` on a laptop.
- **Today (4.2).** h1 "Today · Thursday 1 October" at `--font-size-h1`; it wraps to two lines at 320 px, which is fine. The search input is 48 px. Rows are 56 px: name at body size in bold, status line at `--font-size-small` in `--colour-text-secondary` with the status word in bold, and the **Flagged** tag in the attention colours. "No participants yet." and "No participant matches 'xyz'" are in `--colour-text`. "Your unfinished drafts" is an h2.
- **Note form (4.3).** The participant's name is the h1, the largest text on the screen (28/32 px), **in the case it was entered**. The ASCII sketch's capitals mean "large type"; capitalising would turn "McDonald" or "de Silva" into something else and reads more slowly. The note date sits directly under it in `--colour-text` at body size (not grey, because it is part of the right-day check). Section headings "1. Goals", "2. Common items", "3. Guided notes" are h2s with `--section-gap` above. Tick rows are 56 px. The textarea is 18 px with a 1.5 line height and `--colour-placeholder` prompts. Banners: past-day and earlier-day drafts use the notice colours (action edge and tint); "Not saved" uses the attention colours; validation uses error. The save indicator is `--font-size-small`, `--colour-text-secondary`, tabular figures. The sticky top bar is the only sticky element in the app. **Submit note** is full width on a phone.
- **Submit confirmation (4.3).** The name at `--font-size-h1`, bold. "Flagged for manager: No" at body size. Buttons at 48 px, stacked on a phone.
- **Participant notes and read view (4.4).** The Guided notes text is shown in the `--measure` column at body size and 1.5 line height, with `white-space: pre-wrap` so the writer's line breaks survive. Ticked and not-ticked items use an icon plus words (checkbox-list file). The author, times and "Past-day note, written on … by …" are `--font-size-small` in `--colour-text-secondary`. Tags use the attention and neutral tints.
- **Version history (4.5).** Rows at 56 px. "Version 3 · Sam Lee (manager) · 1 Oct 2026, 5:03 pm" at body size; "Not edited since submit" in `--colour-text`.
- **Flagged notes (4.6).** Tabs "To review (n)" and "Reviewed" are 48 px; the current tab is bold with a 4 px action bar. The review comment textarea uses the same field rules as Guided notes, without a placeholder.
- **Daily report (4.7).** The native date field gets the 2 px control border, 18 px text and 48 px height; `color-scheme: light` keeps its picker light. **Previous day** and **Next day** sit either side on a laptop and wrap on a phone. On an empty day both download buttons use the unavailable style next to "No submitted notes for Thursday 1 October 2026." in `--colour-text`.
- **Participants and goals (4.8) and Common items (4.9).** These use `--page-max` on a laptop. Each goal row puts the wording first (body size, wraps freely) and then **Edit**, **Move up**, **Move down** and **Archive**. On a phone the four buttons wrap onto their own line under the wording, each at least 44 px with 8 px gaps; from 40 rem they sit on the same line. Help text is body size in `--colour-text-secondary`. The archived sections are native `<details>` with the default marker, opening instantly.
- **Guide prompts (4.10).** The help text "Prompts are a guide only…" is body size in `--colour-text`, because the manager must read it. The field is a normal textarea with no placeholder.
- **Users (4.11).** A table on a laptop (`--page-max`); on a phone, stacked rows with name, email, role and status, so nothing scrolls sideways. Email addresses use `overflow-wrap: anywhere`. Status is a word (Invited, Active, Deactivated), not a coloured dot.
- **Participant record export (4.12).** The date fields follow the report's date-field rules. The PDF/Word radios and "Include earlier versions of edited notes" use the same 56 px label rows as tick boxes, not tiny native controls.

---

## Anti-patterns to avoid

- **Light grey text** for hints, timestamps, status lines or empty states. It is the most common contrast failure on the web, and the first thing glare erases.
- **Text below 16 px,** including "small print" on tags, the badge and the save indicator.
- **Font sizes in `px`,** or in `vw` alone. `vw` cannot be zoomed and fails 1.4.4.
- **Form fields below 16 px,** which makes iOS zoom the page on every tap.
- **A downloaded web font, an icon font, or Google Fonts.** The CSP blocks third-party fonts, and they add size and layout shift for no measured gain.
- **"Dyslexia fonts" or a font-switching setting.** The evidence is negative, and it is a feature nobody asked for.
- **All capitals for names or headings,** and italics for prompts or empty states.
- **A dark theme or a theme toggle,** and storing a theme choice in `localStorage` (D22).
- **`color-scheme: only light` on the whole page.** It overrides a user's request to darken pages. Use it only on the QR code.
- **Removing focus outlines,** `box-shadow`-only rings, rings that fade in, `outline-offset: 0` on filled buttons, or a focus colour close to the action blue.
- **Status shown by colour alone:** green and red dots, coloured row backgrounds, or a red border with no message.
- **Shadows, gradients and decorative illustrations.** They do not survive forced colours and add nothing here.
- **Animation of any kind:** spinners, skeletons, slide-in banners, animated `<details>`, smooth scrolling.
- **Several breakpoints named after devices** (`$tablet`, `$iphone`). Use one: 40 rem.
- **Sticky or fixed bars beyond the note form's top bar.** Each one costs vertical space at 200% zoom and risks hiding the focused element (2.4.11).
- **`user-scalable=no` or `maximum-scale`** in the viewport tag.
- **Raw hex values or one-off sizes inside component CSS.** Every value comes from the tokens, or the contrast test cannot protect it.
- **Any trace of the parent company's name** in tokens, class names, file names or comments (D42).

---

## Tensions with decisions

- **None with D1–D43.** The decisions and assumed defaults agree with best practice for the foundations. A32's 44 × 44 px is stricter than WCAG AA (24 px) and equal to AAA. D22 rules out storing a theme choice, which supports light-only. §9.5 and §9.7 rule out third-party fonts, which supports the system font stack.
- **The research figure for tap targets (about 9.2 mm, around 58 px on a phone) is larger than A32's 44 px.** A32 is a minimum, so this is handled by using 48 px and 56 px for the most-tapped targets, not by changing A32.
- **Not a tension, just a clarification of the 4.3 sketch.** The sketch shows the participant's name in capitals ("JANE CITIZEN"); the text says "in large type". This file follows the text: large, bold, in the name's own case.
- **The grey guide prompts (D12, D34)** are discussed in the guided-notes-textarea file. Foundations only supplies their colour: 6.1:1, lighter than typed text and above the 4.5:1 in 4.10.

---

## Sources

**Research**
- Legge, G. E. & Bigelow, C. A. (2011). Does print size matter for reading? *Journal of Vision* 11(5). https://pmc.ncbi.nlm.nih.gov/articles/PMC3428264/
- Bababekova, Y., Rosenfield, M., Hue, J. E. & Huang, R. R. (2011). Font size and viewing distance of handheld smart phones. *Optometry and Vision Science*. https://europepmc.org/article/MED/21499163
- Wallace, S., Bylinskii, Z., Dobres, J. et al. (2022). Towards individuated reading experiences: different fonts increase reading speed for different individuals. *ACM TOCHI*. https://dl.acm.org/doi/10.1145/3502222 (summary: https://readabilitymatters.org/articles/towards-individuated-reading-experiences)
- Budiu, R. (2020). Dark mode vs. light mode: which is better? NN/g. Covers Piepenbrock et al. 2013, Dobres et al. 2017, Legge et al. 1985 and Aleman et al. 2018. https://www.nngroup.com/articles/dark-mode/
- Harley, A. (2019). Touch targets on touchscreens. NN/g. Covers Parhi, Karlson & Bederson 2006 and MIT Touch Lab. https://www.nngroup.com/articles/touch-target-size/
- Dyson, M. C. Legible Typography: overview of research on typography (line length and leading studies). https://legible-typography.com/en/6-overview-of-research-typography (via the ui-ux-design corpus)
- Poole, A. Which are more legible: serif or sans serif typefaces? https://alexpoole.info/blog/which-are-more-legible-serif-or-sans-serif-typefaces/ (via the ui-ux-design corpus)
- ui-ux-design skill corpus: 02-colour (palette, CVD, dark mode), 03-typography (measure, leading, caps, font loading), 04-whitespace-layout (proximity ratios, target sizes in mm), 06-motion (reduced motion). ui-build skill corpus: 06-responsiveness-layout, 10-control-states (focus rings, forced colours).

**Standards**
- WCAG 2.2 Understanding documents: 1.3.4 https://www.w3.org/WAI/WCAG22/Understanding/orientation.html · 1.4.1 https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html · 1.4.3 https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html · 1.4.4 https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html · 1.4.6 https://www.w3.org/WAI/WCAG22/Understanding/contrast-enhanced.html · 1.4.10 https://www.w3.org/WAI/WCAG22/Understanding/reflow.html · 1.4.11 https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html · 1.4.12 https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html · 2.2.2 https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html · 2.3.3 https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html · 2.4.7 https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html · 2.4.11 https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html · 2.4.13 https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance.html · 2.5.5 https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html · 2.5.8 https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- CSS Color Adjustment Module Level 1 (`color-scheme`, `only`, forced colours). https://www.w3.org/TR/css-color-adjust-1/
- CSS Fonts Module Level 4, font matching algorithm. https://www.w3.org/TR/css-fonts-4/#font-matching-algorithm
- MDN: `prefers-reduced-motion` https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion · `forced-colors` https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors · `:focus-visible` https://developer.mozilla.org/en-US/docs/Web/CSS/:focus-visible · `hover` https://developer.mozilla.org/en-US/docs/Web/CSS/@media/hover

**Conventions**
- GOV.UK Design System: type scale https://design-system.service.gov.uk/styles/type-scale/ · typeface https://design-system.service.gov.uk/styles/typeface/ · colour https://design-system.service.gov.uk/styles/colour/ · spacing https://design-system.service.gov.uk/styles/spacing/ · focus states https://design-system.service.gov.uk/get-started/focus-states/
- NHS digital service manual, typography. https://service-manual.nhs.uk/design-system/styles/typography
- Agriculture Design System (AgDS, Australian Government): typography tokens https://design-system.agriculture.gov.au/foundations/tokens/typography · colour tokens https://design-system.agriculture.gov.au/foundations/tokens/colour · breakpoints https://design-system.agriculture.gov.au/foundations/tokens/breakpoints
- Android accessibility help, touch target size. https://support.google.com/accessibility/android/answer/7101858
- Apple Human Interface Guidelines, accessibility (44 × 44 pt; script-rendered, not fetched in this pass). https://developer.apple.com/design/human-interface-guidelines/accessibility
- Baymard Institute (2022), readability: the optimal line length. https://baymard.com/blog/line-length-readability
- Chrome for Developers, Auto Dark Theme. https://developer.chrome.com/blog/auto-dark-theme
- Chrome for Developers, improved font fallbacks. https://developer.chrome.com/blog/font-fallbacks
- Braille Institute, Atkinson Hyperlegible font page. https://www.brailleinstitute.org/freefont/ · Wikipedia, Atkinson Hyperlegible (licence and history). https://en.wikipedia.org/wiki/Atkinson_Hyperlegible
- CSS-Tricks, 16px or larger text prevents iOS form zoom. https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/
- normalize.css (`-webkit-text-size-adjust`). https://github.com/necolas/normalize.css/blob/master/normalize.css
- Scott O'Hara, "Fixing" lists (Safari and `list-style: none`). https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html

**Unverified in this pass** (flagged where used): Samsung Internet's page-darkening behaviour; whether Chrome's Auto Dark Theme is on by default for all Android users today (the blog documents an origin trial from Chrome 96); Apple's 44 pt figure on its live page; any published outcome study for Atkinson Hyperlegible.
