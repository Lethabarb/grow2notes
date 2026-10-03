# Find a participant (filter as you type)

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.

Component key: `search-filter`. This is the "Find a participant" box on Today (design.md 4.2) and on Manage > Participants (4.8). It narrows a list the screen already holds, by given or family name, as the user types. It covers the label, the field, the Clear button, how names are matched, the "No participant matches 'xyz'" message and the screen-reader announcement. Nothing here adds a screen, setting, notification or data item. It describes how to build what the design already specifies.

---

## Where it's used

The two lists are already in memory when the box is used. `GET /api/today` and `GET /api/admin/participants` both return the whole list (design.md 6.1: "Lists are returned whole … a few dozen participants"). So this is a **client-side filter of loaded content**, not a server search. No request is sent per keystroke.

| Screen | What it filters | What differs |
|---|---|---|
| **Today, 4.2** (workers and managers, mostly on phones) | "Every active participant, sorted by family name then given name (A5)". The box sits third on the screen, after the header and "Your unfinished drafts" and before the list. | It filters **only** the participant list, not "Your unfinished drafts", which sits above the box. It matches names only, never the status line ("Draft · Alex P. · started 9:14 am"). If there are no participants at all, the screen shows "No participants yet." and the box is not shown (see States). A manager sees "Add participant" only in that no-participants state, not when a search finds nothing. |
| **Manage > Participants list, 4.8** (managers, mostly on laptops) | The participant list in the selected view. The screen holds "search, an Active / Archived toggle, names, and 'Add participant'". | The box filters whichever view is selected, Active or Archived. The typed text stays when the toggle changes. "Add participant" stays visible whatever is typed, because it is part of the screen, not the list. The label and the no-match message are the same as on Today. |
| **Participant detail, 4.8** (goals, past-day note) | Nothing. | **This component is not used here.** Goals are a short, ordered list that managers reorder with Move up and Move down, and hiding rows would break that. "Write past-day note" uses a date field, which is a different component. Participant history (4.4) pages with "Show older" and has no search either. |

---

## Best practice

**Filter, not search: what the box is**

- The HTML `<search>` element "represents a part of a document or application that contains a set of form controls or other content related to performing a search or **filtering** operation". It creates a `search` landmark. **[Standard]** https://html.spec.whatwg.org/multipage/grouping-content.html#the-search-element
- MDN says that when filtering runs only in JavaScript, "neither a `<form>` element nor a submit `<button>` is required". It also says `<search>` "is not for presenting search results"; the results belong in the main content. `<search>` has been Baseline "widely available" since October 2023. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/search
- AgDS has a separate **Search input** component for filtering "content that has already been loaded on the page". It has no submit button, and shows a search icon and a clear button by default. Its guidance says not to hide either one. AgDS keeps its **Search box** (with a submit button) for server-side search. **[Convention]** https://design-system.agriculture.gov.au/components/search-input
- NN/g calls this *interactive* filtering, where results update after each change. It suits one criterion with results in under a second. *Batch* filtering (choose, then Apply) is for several criteria or slow results. One name box over an in-memory list is the clear interactive case. NN/g also warns against jumping the page while people filter, because they "lose their place on the page". **[Research]** (Sherwin, 2016) https://www.nngroup.com/articles/applying-filters/
- The DWP Design System lists "batch filtering is preferable to live filtering" as a theory to test, for multi-control filter panels where "unintentional clicks lead to changing the results". This does not apply to a single text box, and DWP publishes it as a theory, not a finding. **[Convention]** https://design-system.dwp.gov.uk/research/filters/design-notes
- Apple's HIG: "If possible, start search immediately when a person types." **[Convention]** https://developer.apple.com/design/human-interface-guidelines/search-fields
- Updating the list as the user types is a change of **content**, not of **context**. WCAG says "A change of content is not always a change of context" unless it also moves focus or changes the viewport. So live filtering meets SC 3.2.2 On Input as long as focus stays in the box. **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/on-input.html

**Label, field type, keyboard**

- Use a visible label, not placeholder text. Placeholders vanish when typing starts, which burdens memory, and their default grey "has poor color contrast". This matters more for people with cognitive impairments. **[Research]** (Sherwin, NN/g, 2014, reviewed 2018) https://www.nngroup.com/articles/form-design-placeholders/ GOV.UK: "Do not use placeholder text in place of a label." **[Convention]** https://design-system.service.gov.uk/components/text-input/
- `type="search"` adds little and costs some things. Adrian Roselli tested nine browsers with screen readers. The browser's own clear button "cannot be activated via keyboard and is not announced", and Escape (which clears the field in Chrome) is hard to discover and can be pressed by accident by NVDA and JAWS users leaving forms mode. The only real gain is a different label on the phone's Enter key. He recommends `type="text"`. **[Research]** (practitioner browser and screen-reader testing, 2019, updated December 2023) https://adrianroselli.com/2019/07/ignore-typesearch.html MDN confirms that in Chrome, Escape also clears the field. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/search
- An accessibility audit of the ONS Design System by the Digital Accessibility Centre (DAC) found that the native clear button "cannot receive keyboard focus" and rated it a Level A failure. The fix was to add "an additional button … that can be navigated onto by keyboard-only users". **[Standard]** (SC 2.1.1, as applied by the DAC audit) https://github.com/ONSdigital/design-system/issues/2237 A partner agency's audit raised the same issue with USWDS. **[Convention]** https://github.com/uswds/uswds/issues/5277
- React Aria's `useSearchField` uses `type="search"` by default. Its clear button is `excludeFromTabOrder: true` with `aria-label` "Clear search", and Escape clears the field. That is a defensible pattern, but it is the opposite of what the DAC audit asked for. **[Convention]** https://github.com/adobe/react-spectrum/blob/main/packages/react-aria/src/searchfield/useSearchField.ts
- Do not use `autofocus` on a screen that is not purely a search page. Screen readers "teleport" users to the field without warning, the page can scroll on load, and on touch devices the keyboard opens over the content. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autofocus
- iOS Safari zooms into any input whose font size is below 16px when it gets focus. "If the font-size of an `<input>` is 16px or larger, Safari on iOS will focus into the input normally." Apple does not document this, but it is widely observed. **[Convention]** https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/
- `autocorrect="off"` turns off spelling and punctuation correction. It is in the HTML Standard and became Baseline (newly available) in September 2026, so older phones may ignore it. `spellcheck` is a long-standing global attribute. Names are exactly what autocorrect gets wrong. **[Standard]** https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocorrect
- `autocomplete="off"` tells the browser the value is sensitive or never reused, and that the page "does not want the user agent to provide autocompletion values". MDN notes that browsers do not always honour it, so it is a request, not a guarantee. **[Standard]** https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-autocomplete-off · https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete
- A focus indicator is required (SC 2.4.7). Sticky headers and footers are named as "typical types of content that can overlap focused items" (SC 2.4.11). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- The accessible name of a control must contain its visible text, and "a best practice is to have the text of the label at the start of the name" (SC 2.5.3). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html

**Debounce and performance**

- React's `useDeferredValue` is the tool for slow rendering, and debouncing is for network requests: "If the work you're optimizing doesn't happen during rendering, debouncing and throttling are still useful … they can let you fire fewer network requests." Debouncing a render "merely postpone[s] the moment when rendering blocks the keystroke." **[Standard]** (framework documentation) https://react.dev/reference/react/useDeferredValue
- The common 300 ms search debounce has no located primary source. MDN's own illustration uses 10 ms. **[Convention]** (as recorded by the ui-build evidence audit) https://developer.mozilla.org/en-US/docs/Glossary/Debounce
- **Here, nothing needs debouncing except the screen-reader announcement.** No request is sent, and filtering a few dozen names takes well under a frame. **[Opinion]**

**Telling screen-reader users what happened**

- A message such as "No results returned" or "18 results returned" is a *status message* under SC 4.1.3 when it does not take focus. It must be programmatically determinable, for example with `role="status"` (technique ARIA22). **[Standard]** https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- GOV.UK's 2014 fix for live-filtered search followed Léonie Watson's advice to "update only the result count" in a visually hidden live region. **[Convention]** (Bartlett, GDS, 2014; no test data published) https://technology.blog.gov.uk/2014/08/14/improving-accessibility-on-gov-uk-search/
- GOV.UK's accessible-autocomplete delays its status message with `statusDebounceMillis = 1400`. Its regions are `role="status"`, `aria-live="polite"`, `aria-atomic="true"`. **[Convention]** https://github.com/alphagov/accessible-autocomplete/blob/main/src/status.js
- Sara Soueidan recommends a different approach for filter-as-you-type: an `aria-describedby` description such as "Results will filter as you type", no announcement while results exist, and an announcement when *no* results are found. She quotes Scott O'Hara: "we don't want to constantly interrupt people while typing, [but] we need to interrupt when things go afoul." **[Opinion]** (expert practitioner, January 2024) https://www.sarasoueidan.com/blog/accessible-notifications-with-aria-live-regions-part-2/
- A live region must be in the DOM before text is written into it. `{noMatch && <p role="status">…</p>}` mounts the region and its text together, so many screen readers say nothing. **[Standard]** (WAI-ARIA live region model; MDN) https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- Use `role="status"` (polite) by default. Use assertive `role="alert"` only when the user's action is blocked or data is at risk. **[Convention]** (ui-build feedback-channels rule 7; MDN) https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/status_role

**Empty state**

- A no-results message should "clearly explain" that nothing matched and leave the query in the box so it can be edited. Do not make jokes about it. **[Research]** (Whitenton, NN/g, 2014) https://www.nngroup.com/articles/search-no-results-serp/
- Australian Government style uses single quotation marks. "Double quotation marks aren't Australian Government style." **[Convention]** https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/punctuation/quotation-marks

---

## Recommendation for Grow2Notes

### Anatomy

```
<search>                                     search landmark, no <form>
  Find a participant                         <label>, visible, bold, above the field
  [(icon) jan                    ] [ Clear ] text input + Clear button (button only while the field has text)
  (visually hidden) The list of participants below changes as you type.   aria-describedby
  (visually hidden, role="status") …         announcement region, always in the DOM
</search>
<ul> participant rows </ul>                   the screen's list, outside <search>
  or, if nothing matches:
No participant matches ‘xyz’                 visible message where the list would be
```

1. **Landmark:** `<search>` with no `<form>` inside, so there is nothing to submit and Enter cannot reload the page.
2. **Label:** a visible `<label>` reading **Find a participant** (4.2), on its own line above the field. The same label is used on both screens: one term per concept.
3. **Field:** `<input type="text">`, full width on phones and at most about 32rem wide on laptops. It has a decorative magnifying-glass icon at the left inside the field (`aria-hidden="true"`), and **no placeholder**.
4. **Clear button:** a real `<button type="button">` to the right of the field, with visible text **Clear** and a visually hidden " search", so its accessible name is "Clear search". It shows only while the field holds any text.
5. **Description:** visually hidden text, "The list of participants below changes as you type.", tied to the field with `aria-describedby`. Screen-reader users hear it once when they enter the field, so they know to check the list (Soueidan). Sighted users do not need it, because they see the list change.
6. **Announcement region:** a visually hidden `<p role="status" aria-live="polite">` that is always rendered and starts empty.
7. **No-match message:** the visible text **No participant matches ‘xyz’**, in place of the list. It is not part of `<search>`, because results are not search controls (MDN).

### Behaviour

- **Filter on every keystroke, synchronously, with no debounce and no `useDeferredValue`.** The list is in memory and a few dozen rows long. The field always shows exactly what was typed. **[Opinion]**, based on React's guidance that debounce is for network work.
- **Matching rule** (one rule on both screens):
  - Normalise both the typed text and the name: Unicode NFKD, remove accent marks ("Zoë" → "zoe"), lower-case in `en-AU`, **remove apostrophes** ("O’Brien" → "obrien"), and turn every other non-letter, non-digit character into a space (hyphens, commas, and the full stop dictation sometimes adds). Collapse spaces and trim.
  - Split the typed text into words. A participant matches when **every typed word is the start of some word** in "given name + family name". So "jan", "cit", "jane cit", "cit jane", "citizen, jane" and "Jane." all find Jane Citizen.
  - If the typed text is empty after normalising (spaces only, or "."), show the whole list and no message.
  - Match names only: never date of birth, status text or author names.
  - **Keep the A5 order** (family name, then given name) in the filtered list. Do not re-rank by how well a name matched. **[Opinion]**: predictable order matters more than match quality across a few dozen names.
  - No fuzzy matching, "did you mean", suggestions or recent searches. The design does not specify them, they make results less predictable, and recent searches would store names. **[Opinion]**
- **Enter does nothing.** With no `<form>` there is no submit. Do not move focus into the list, open the only match, or blur the field. Opening a participant takes a deliberate tap, which supports the wrong-participant protection in 3.9. **[Opinion]**, and SC 3.2.2.
- **Escape does nothing special.** Do not add an Escape-to-clear handler, because NVDA and JAWS users press Escape to leave forms mode (Roselli). Clear is the only shortcut.
- **Clear:** empties the field, puts focus back in the field, and restores the full list. On a phone the keyboard stays open, because `onMouseDown` calls `preventDefault()` and focus never leaves the field. A keyboard user tabs to Clear and presses Enter or Space. Focus returns to the field, which matters because the Clear button then disappears.
- **Where the typed text lives:** in component state only. **It never goes into the URL, the page title, `history.state`, TanStack Query keys, localStorage, sessionStorage, logs or telemetry.** It is a participant's name. This follows D22, 4.0 ("URLs hold only IDs and dates") and 9.5. It is never sent to the server.
- **Leaving the screen clears the text.** When the user opens a participant and comes back, or returns to Today after Submit (flow 4.13, "back to Today, showing Submitted"), the full list is shown. **[Opinion]**: the list is short, and a leftover filter could hide the note the user just submitted.
- **Background refetch** (TanStack Query refetches on window focus): the field stays mounted, keeps its text and focus, and the filter applies to the new list. A refetch never clears or disables the field.
- **No scrolling on keystroke.** The page does not jump while typing (NN/g). The field sits near the top of Today in the 4.2 order, so on a phone the first matches and the no-match message show above the keyboard.

### Screen-reader announcements

- **Only one thing is announced: the no-match message.** About **500 ms** after the user stops typing, if nothing matches, the hidden status region receives "No participant matches ‘xyz’", the same words as the visible message. When there are matches, or the field is empty, the region is set to empty.
  - This follows Soueidan and O'Hara: describe the behaviour up front, stay quiet while results exist, and speak up when the query has gone wrong. The visible no-match message is a status message, so SC 4.1.3 requires it to be exposed. The design shows no match count, and Today has "no counts" (4.2), so none is invented. **[Standard]** + **[Opinion]**
  - Why 500 ms: without a delay, every extra keystroke after the first miss changes the text ("…‘xy’", "…‘xyz’") and queues another announcement on top of the screen reader's typing echo. GOV.UK's accessible-autocomplete waits 1,400 ms for its count **[Convention]**, but a no-match message should not wait that long (O'Hara). 500 ms is **[Opinion]**; test it with NVDA and with VoiceOver on iOS, and adjust if needed.
  - Use polite, not assertive. Because the announcement waits for a pause in typing, there is no speech to interrupt, so polite is heard straight away without breaking the rule that assertive is for blocked actions and data at risk.
- The visible message appears **immediately** (not delayed) and is **not** `aria-hidden`. A screen-reader user reading the page line by line may hear the message twice (once from the hidden region, once in the list area). GOV.UK's autocomplete accepts the same duplication. It costs less than the alternatives of hiding visible text or delaying it.
- Clearing is not announced. Focus returns to the field, and the screen reader reads its label and empty value.

### States

| State | What shows | Notes |
|---|---|---|
| **Default (empty)** | Label, empty field with icon, no Clear button, full list. | No placeholder. |
| **Typing, matches** | The text as typed, Clear button, filtered list in A5 order. | Hidden region stays empty. |
| **Empty (no match)** | The text stays in the field, Clear stays visible, and **No participant matches ‘xyz’** shows where the list was. | The typed text is trimmed and shown as typed, not normalised, so a dictation mistake is visible ("‘Jean’"). Long text wraps (`overflow-wrap: anywhere`). Plain body text: not red and no icon, because it is not an error. Announced after a 500 ms pause. |
| **No participants at all** | The **box is not rendered.** The screen shows "No participants yet." (managers also see "Add participant"). | **[Opinion]**: an empty box over an empty list invites a pointless action. On Manage > Participants this applies to the Active and Archived lists together. A view that is empty only because of the toggle keeps the box. |
| **Loading (first load)** | The box is not rendered until the list has loaded. The screen's own loading state shows instead. | The request is small (6.1), and this rules out typing before the data exists or a false "No participant matches" message. |
| **Refetching** | No change. The field stays mounted and focused, and keeps its text. | Never unmount or disable the field on `isFetching`, only on the first `isPending`. |
| **List failed to load** | The box is not rendered. The screen shows its load error. | There is nothing to filter. |
| **Hover** | The field border darkens one step. The Clear button gets an underline or a tint. | Both states keep at least 3:1 against the page (SC 1.4.11). |
| **Focus** | The field and Clear show a 3px solid `outline` with a 2px offset, at least 3:1 against the surroundings. | Use `outline`, not `box-shadow` alone, which disappears in forced-colours mode. `:focus-visible` always matches a focused text field, so the ring shows for touch focus too. |
| **Active (pressed)** | The Clear button darkens while pressed. | |
| **Disabled** | Never. | A disabled field drops focus to `<body>` and hides the reason. |
| **Error** | None. | A filter cannot be wrong; no match is a status, not an error. |
| **Read-only** | Not applicable. | |

### Phone and laptop

- **Phone (most Today use):** the field is full width with a minimum height of 48px (the design's minimum is 44 × 44). The Clear button is at least 44 × 44. Text is at least 16px, so iOS does not zoom. The box is **not sticky**: on a phone with the keyboard up and text at 200%, a sticky bar takes up much of the screen and can cover focused rows (SC 2.4.11). No `autofocus`, so the keyboard does not open on arrival. Keep the phone's default Enter key and do not set `enterkeyhint`, because Enter has no action. `autocorrect="off"` and `spellcheck="false"` stop iOS and Android "correcting" names such as "Nguyen" or "Thi". Keep `autocapitalize` at its default, because matching ignores case. Keyboard dictation works because the field is a plain text input, and normalising removes the capital letters and full stops that dictation adds.
- **Laptop (most Manage use):** the field is at most about 32rem wide, with the Clear button beside it. GOV.UK: inputs should be "the right size for the content". Tab order is label, field, Clear (when shown), then the next control (the Active / Archived toggle on 4.8, the first participant on 4.2). There are no shortcuts such as "/" to focus: SC 2.1.4 and the simplicity rule.
- **Reflow:** the field and Clear sit in a flex row with `flex-wrap: wrap`. At 320 CSS px or 200% text, Clear drops below the field instead of squeezing it. Nothing scrolls sideways (SC 1.4.10).

### Exact copy

| Where | Text | Source |
|---|---|---|
| Label | Find a participant | design.md 4.2 |
| No-match message (visible and announced) | No participant matches ‘xyz’ | design.md 4.2, with typographic single quotes (Style Manual). No full stop, as written in the design. `xyz` is the trimmed text as typed. |
| No participants | No participants yet. | design.md 4.2 (the box is hidden in this state) |
| Clear button | Clear (accessible name "Clear search") | New; visible word plus visually hidden " search" |
| Field description (screen readers only) | The list of participants below changes as you type. | New; hidden |

### Accessibility summary

- **Semantics:** `<search>` (landmark), `<label for>`, `<input type="text">` (role textbox), `<button type="button">`, `<p role="status" aria-live="polite">`. The list stays a plain `<ul>` of links. **Do not use** `role="combobox"`, `listbox` or `aria-activedescendant`: the results are page content, not a popup, and those roles would take over the arrow keys in the list. `aria-controls` is not needed.
- **ARIA used, and why:** `aria-describedby` (the description), `role="status"` with `aria-live="polite"` (SC 4.1.3; MDN recommends the redundant `aria-live`), `aria-hidden="true"` on the decorative icon only. Nothing else.
- **Keyboard:** Tab and Shift+Tab only. Enter and Escape are not intercepted. `preventDefault()` is never called on printable keys, so IME composition and dictation keep working.
- **WCAG 2.2 criteria this meets:** 1.3.1 Info and Relationships (label, landmark), 1.4.3 Contrast (text 4.5:1), 1.4.4 Resize Text and 1.4.10 Reflow (wrapping row), 1.4.11 Non-text Contrast (field border and focus ring at 3:1), 1.4.12 Text Spacing, 2.1.1 Keyboard (a focusable Clear instead of the native ×), 2.1.4 Character Key Shortcuts (none added), 2.4.3 Focus Order, 2.4.6 Headings and Labels, 2.4.7 Focus Visible, 2.4.11 Focus Not Obscured (not sticky), 2.5.3 Label in Name ("Clear search" starts with "Clear"), 2.5.8 Target Size (44px and up), 3.2.1 On Focus (no autofocus or jump), 3.2.2 On Input (content change only), 3.3.2 Labels or Instructions, 4.1.2 Name, Role, Value, 4.1.3 Status Messages.

### Implementation notes (React 19, native HTML, CSS Modules)

- **No React Aria.** Native elements cover everything here. React Aria's `SearchField` would also bring `type="search"`, Escape-to-clear and a clear button that is out of the tab order, all of which this design avoids.
- **Matching** lives in one pure function, unit-tested with Vitest:

```ts
// src/features/participants/matchesName.ts
const normalise = (s: string): string =>
  s.normalize('NFKD')
    .replace(/\p{M}/gu, '')                 // accents: "Zoë" -> "zoe"
    .toLocaleLowerCase('en-AU')
    .replace(/['‘’ʼ]/g, '')  // apostrophes: "O’Brien" -> "obrien"
    .replace(/[^\p{L}\p{N}]+/gu, ' ')       // hyphens, commas, dictation full stops
    .trim();

export function matchesName(
  p: { givenName: string; familyName: string },
  query: string,
): boolean {
  const q = normalise(query);
  if (q === '') return true;
  const words = normalise(`${p.givenName} ${p.familyName}`).split(' ');
  return q.split(' ').every((part) => words.some((w) => w.startsWith(part)));
}
```

Test cases: "jan", "JANE", "cit jane", "citizen, jane", "Jane.", "zoe" against "Zoë", "o'b" and "obr" against "O’Brien", "smith-j" against "Smith-Jones", "   " (matches all), "xyz" (matches none).

- **The component:**

```tsx
// src/features/participants/FindParticipant.tsx
import { useEffect, useId, useRef, useState } from 'react';
import styles from './FindParticipant.module.css';

const ANNOUNCE_DELAY_MS = 500; // [Opinion]; see design/ux/components/search-filter.md

type Props = { value: string; onChange: (v: string) => void; noMatch: boolean };

export function FindParticipant({ value, onChange, noMatch }: Props) {
  const inputId = useId();
  const hintId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [announcement, setAnnouncement] = useState('');
  const shown = value.trim();

  useEffect(() => {
    const message = noMatch ? `No participant matches ‘${shown}’` : '';
    const t = setTimeout(() => setAnnouncement(message), ANNOUNCE_DELAY_MS);
    return () => clearTimeout(t);
  }, [noMatch, shown]);

  return (
    <search className={styles.search}>
      <label htmlFor={inputId} className={styles.label}>Find a participant</label>
      <p id={hintId} className="visually-hidden">
        The list of participants below changes as you type.
      </p>
      <div className={styles.row}>
        <span className={styles.field}>
          <SearchIcon className={styles.icon} aria-hidden="true" focusable="false" />
          <input
            ref={inputRef}
            id={inputId}
            className={styles.input}
            type="text"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            aria-describedby={hintId}
            autoComplete="off"
            autoCorrect="off"
            spellCheck={false}
          />
        </span>
        {value !== '' && (
          <button
            type="button"
            className={styles.clear}
            onMouseDown={(e) => e.preventDefault()} /* keep focus, and the phone keyboard, in the field */
            onClick={() => { onChange(''); inputRef.current?.focus(); }}
          >
            Clear<span className="visually-hidden"> search</span>
          </button>
        )}
      </div>
      {/* Always rendered so screen readers register it before text arrives */}
      <p role="status" aria-live="polite" className="visually-hidden">{announcement}</p>
    </search>
  );
}
```

- **Using it on Today** (Manage > Participants is the same, with `participants` set to the selected view):

```tsx
const [query, setQuery] = useState('');               // component state only
const shown = participants.filter((p) => matchesName(p, query));
const noMatch = participants.length > 0 && shown.length === 0;

{participants.length > 0 && (
  <FindParticipant value={query} onChange={setQuery} noMatch={noMatch} />
)}
{noMatch
  ? <p className={styles.noMatch}>No participant matches ‘{query.trim()}’</p>
  : <ul className={styles.list}>{shown.map(/* rows */)}</ul>}
```

- **CSS Module outline:**

```css
/* FindParticipant.module.css */
.search { display: block; }                 /* older browsers treat <search> as an unknown inline element */
.label  { display: block; font-weight: 600; margin-block-end: 0.25rem; }
.row    { display: flex; flex-wrap: wrap; gap: 0.5rem; max-inline-size: 32rem; }
.field  { position: relative; flex: 1 1 14rem; }
.icon   { position: absolute; inset-inline-start: 0.75rem; inset-block-start: 50%;
          translate: 0 -50%; inline-size: 1.25rem; block-size: 1.25rem; pointer-events: none; }
.input  { appearance: none; box-sizing: border-box; inline-size: 100%; min-block-size: 3rem;
          padding-block: 0.5rem; padding-inline: 2.75rem 0.75rem;
          font: inherit; font-size: max(1rem, 16px);           /* iOS zooms below 16px */
          color: var(--text); background: var(--surface);
          border: 2px solid var(--border-strong); border-radius: 0.25rem; }
.input:hover { border-color: var(--border-hover); }
.clear  { min-inline-size: 44px; min-block-size: 3rem; padding-inline: 1rem; font: inherit;
          color: var(--link); background: transparent; border: 2px solid transparent; }
.clear:hover  { text-decoration: underline; }
.clear:active { background: var(--pressed); }
.input:focus-visible, .clear:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
@media (forced-colors: active) { .input { border-color: CanvasText; } .clear { border-color: ButtonText; } }
```

The visible no-match paragraph, on the screen's own module: `overflow-wrap: anywhere;`, body size, normal text colour.

- **TypeScript:** recent `@types/react` versions include `<search>` among the intrinsic elements. This is **unverified** for the version CI pins at M0. If the build rejects it, add a one-line JSX intrinsic-element augmentation. Do not fall back to `<div role="search">` unless that fails.
- **Never** use `dangerouslySetInnerHTML` for the message. React escapes the typed text.
- **Checks:** Playwright and axe on both screens (label present, status region in the initial DOM, no horizontal scroll at 320px). A Playwright test that Clear returns focus to the field. A manual pass with NVDA and Chrome on a laptop and with VoiceOver on an iPhone to confirm the no-match announcement and the 500 ms timing (axe cannot test announcements).

---

## Per-screen notes

**Today (4.2)**
- Order follows the design: header, "Your unfinished drafts" (when shown), **Find a participant**, then the list. The filter never hides or changes the drafts section.
- Rows show status text such as "Submitted · Alex P. · 4:12 pm". Typing "Alex" must not match Jane's row because of her author's name. Match on participant names only.
- No-match state: only "No participant matches ‘xyz’". Do not show "Add participant" here (the design shows it only for "No participants yet."), do not show a count, and do not show suggestions.
- The text resets when the user leaves Today, so after Submit the user returns to the full list and can see the new "Submitted" status (4.13).
- This is the screen tired workers use on phones. The 16px text, the 44px Clear target, autocorrect off and the punctuation-tolerant matching matter most here.

**Manage > Participants list (4.8)**
- Same component, label and messages. In DOM order the box comes before the Active / Archived toggle, as the design lists "search, an Active / Archived toggle, names". On a wide laptop screen they may share one row (flex-wrap). **[Opinion]**
- The typed text persists when the toggle changes, and the filter applies to the selected view. If the new view has no match, the no-match message shows and is announced after the pause. If the same no-match text was already in the region, it is not repeated. This is an accepted edge case.
- "Add participant" stays visible and in the same place whatever is typed.
- Managers mostly use laptops here: keep the field narrow (about 32rem), not full width.

**Participant detail (4.8)**
- No search box. Do not add one to the goals list (reorder controls, short list) or to Past notes (4.4).

---

## Anti-patterns to avoid

- **A placeholder as the label** ("Search…" inside the field). Use the visible label "Find a participant" and no placeholder.
- **`type="search"` with the browser's ×.** It cannot be reached by keyboard, is not announced, is smaller than 44px and differs between browsers. Use `type="text"` plus a real Clear button.
- **Escape-to-clear,** or any key handler on printable keys. It catches screen-reader users leaving forms mode, and `preventDefault()` on typing breaks IME and dictation.
- **A combobox or listbox pattern** (`role="combobox"`, `aria-activedescendant`, arrow keys moving through results). This is a page filter, not a popup.
- **`autofocus` on Today or Manage.** It opens the keyboard over the list and moves screen-reader users without warning.
- **Debouncing the field or the filtering.** The list is in memory, so a delay only adds lag. Delay the announcement only.
- **Announcing on every keystroke,** using `role="alert"`, or **mounting the live region together with its text** (`{noMatch && <p role="status">…}`).
- **Typed text in the URL** (`?q=jane`), the page title, `history.state` or browser storage. That puts participant names into browser history on personal phones (4.0, 9.6, D22). The same applies to logging or telemetry of the query (9.5).
- **A `<form>` with submit,** or a "Search" button. Enter would reload or do nothing visible, and the design is filter-as-you-type.
- **Opening the only match on Enter,** or moving focus into the list when results change. This takes context away (SC 3.2.2), and on this app it risks a note on the wrong participant (3.9).
- **Disabling or unmounting the field during a refetch.** Focus falls to `<body>` and the typed text is lost.
- **A sticky search bar on phones.** With the keyboard up and large text it covers focused rows (SC 2.4.11).
- **A field font below 16px.** iOS zooms the page and leaves it zoomed.
- **A visible count** ("3 of 24 participants") or a "Showing all" line. Today has no counts (4.2, D25), and the design asks for none.
- **Highlighting matched letters** with `<mark>` or bold spans. It is visual noise on a short list, and splitting a name into several inline elements can make some screen readers read it in pieces. **[Opinion]**, unverified per screen reader.
- **Fuzzy matching, "did you mean", suggestions or recent searches.** Not specified, less predictable, and recent searches would store names.
- **Re-ranking results** by match quality, which breaks the family-name order (A5).
- **A Clear button that disappears and leaves focus on `<body>`.** Always return focus to the field.
- **Red text or an error icon on the no-match message.** It is a status, not an error.

---

## Tensions with decisions

None found.

One choice was made because of the design rather than against it. The pattern most often recommended for live filters announces a result count (GOV.UK 2014, the WCAG 4.1.3 example, accessible-autocomplete). Today states "There are no counts" (4.2, D25), and the design shows only a no-match message. This recommendation follows the equally well-sourced Soueidan and O'Hara approach: a description up front, and an announcement only when nothing matches. So no count is needed anywhere, and no decision is affected.

---

## Sources

Standards and specifications
- WHATWG HTML, the `search` element: https://html.spec.whatwg.org/multipage/grouping-content.html#the-search-element
- WHATWG HTML, `autocomplete="off"`: https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-autocomplete-off
- MDN, `<search>`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/search
- MDN, `<input type="search">`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/search
- MDN, `autocorrect`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autocorrect
- MDN, `autocomplete`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete
- MDN, `autofocus`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes/autofocus
- MDN, ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- MDN, `status` role: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/status_role
- MDN, Debounce glossary: https://developer.mozilla.org/en-US/docs/Glossary/Debounce
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WCAG 2.2 Understanding 3.2.2 On Input: https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- WCAG 2.2 Understanding 2.4.11 Focus Not Obscured (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- React, `useDeferredValue`: https://react.dev/reference/react/useDeferredValue

Research
- Sherwin, K. "User Intent Affects Filter Design", NN/g, 2016: https://www.nngroup.com/articles/applying-filters/
- Sherwin, K. "Placeholders in Form Fields Are Harmful", NN/g, 2014 (reviewed 2018): https://www.nngroup.com/articles/form-design-placeholders/
- Whitenton, K. "3 Guidelines for Search Engine 'No Results' Pages", NN/g, 2014: https://www.nngroup.com/articles/search-no-results-serp/
- Roselli, A. "Maybe Ignore type=search", 2019, updated 2023 (practitioner testing): https://adrianroselli.com/2019/07/ignore-typesearch.html

Design systems and conventions
- AgDS, Search input: https://design-system.agriculture.gov.au/components/search-input
- GOV.UK Design System, Text input: https://design-system.service.gov.uk/components/text-input/
- GOV.UK accessible-autocomplete, `status.js`: https://github.com/alphagov/accessible-autocomplete/blob/main/src/status.js
- GDS Technology blog, "Improving accessibility on GOV.UK search", 2014: https://technology.blog.gov.uk/2014/08/14/improving-accessibility-on-gov-uk-search/
- DWP Design System, Filters design notes: https://design-system.dwp.gov.uk/research/filters/design-notes
- ONS Design System issue #2237 (DAC audit, clear button): https://github.com/ONSdigital/design-system/issues/2237
- USWDS issue #5277 (search clear button): https://github.com/uswds/uswds/issues/5277
- React Aria `useSearchField` source: https://github.com/adobe/react-spectrum/blob/main/packages/react-aria/src/searchfield/useSearchField.ts
- Apple HIG, Search fields: https://developer.apple.com/design/human-interface-guidelines/search-fields
- Australian Government Style Manual, Quotation marks: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/punctuation/quotation-marks
- Coyier, C. "16px or Larger Text Prevents iOS Form Zoom", CSS-Tricks, 2021: https://css-tricks.com/16px-or-larger-text-prevents-ios-form-zoom/

Expert opinion
- Soueidan, S. "Accessible notifications with ARIA Live Regions (Part 2)", January 2024 (quotes Scott O'Hara): https://www.sarasoueidan.com/blog/accessible-notifications-with-aria-live-regions-part-2/
