# Status labels and tags

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026: the tag **border** below replaces this file's earlier "no border, bold in forced
> colours" (today.md Conflicts #10). Every screen uses this one tag style.

Component key: `status-tags`. This file covers the words that tell someone the state of a note or a user: **Draft, Submitted, Flagged, Edited, Past-day note, To review, Reviewed, Invited, Active, Deactivated**.

Other components own these related pieces, and this file only sets the rules they share with it:

- the To review count badge on the Flagged menu item (navigation)
- the **To review (n) | Reviewed** tabs (tabs)
- the past-day and earlier-day banners on the note form (banners)
- the save indicator (note form)
- the "Note for Jane Citizen submitted" message after an action (notifications)

---

## Where it's used

| Screen | What is shown | What differs |
|---|---|---|
| **Today** (design.md 4.2) | One status line under a participant's name, but only when a note exists (A17): *Draft · You · saved 9:42 am* / *Draft · Alex P. · started 9:14 am* / *Submitted · Alex P. · 4:12 pm*, with a **Flagged** tag when the current version is flagged. | Rows with no note show nothing: no "Not started" and no "missing" marker (D25). The status line shows Flagged and nothing else. Edited, To review and Reviewed are not shown on Today. The "Your unfinished drafts" section shows the participant and note date. It needs no status word because its heading already says what the rows are. |
| **Participant notes history** (4.4), list | Each row shows the date, author, status (**Draft** or **Submitted**) and tags: **Edited** and **Flagged** for everyone. Managers also see **To review** or **Reviewed**. | This screen uses the most tags: up to three on one row. |
| **Read view** (4.4) | **Edited** label with the last editor and time. "**Past-day note**, written on … by …" where it applies. The flag and its reason. Managers also see the review status and comment. | Each tag here is followed by its detail on the same line, because the reader has asked for the full story. |
| **Version history** (4.5) | Rows like *Version 3 · Sam Lee (manager) · 1 Oct 2026, 5:03 pm*. Version 1 is labelled **Submitted**. | Only one row has a status word. "Not edited since submit" is an empty-state sentence, not a status. |
| **Flagged notes** (4.6) | The tab carries the state (To review or Reviewed). If a later edit removed the tick, the row says *Flag removed in a later edit*. Opening a row shows the read view. | Rows repeat no tag, because every row in a tab has the same state. |
| **Users** (4.11) | A status for every user: **Invited**, **Active** or **Deactivated**. | Every row has exactly one status, and it is never empty. Managers only. |

---

## Best practice

### Tag, badge or plain text

- **A tag shows a status. It is never a link or a button, and its name is an adjective, not a verb.** GOV.UK: "Do not make a tag interactive by making it into a link or button. Use adjectives… and not verbs". NHS says the same. [Convention] https://design-system.service.gov.uk/components/tag/, https://service-manual.nhs.uk/design-system/components/tag
- **Users try to click tags that look like buttons.** GOV.UK: "Research from multiple teams found that some users perceived [white text on a dark background] as buttons and tried to click on them." The fix was a light fill with dark text. Task-list feedback was similar: users tried to select statuses. GOV.UK responded by making statuses look less like buttons and **linking the whole row**. [Research] (qualitative findings from service teams; no sample sizes published) https://design-system.service.gov.uk/components/tag/, https://design-system.service.gov.uk/components/task-list/
- **Use plain text for the normal or finished state, and save the filled tag for states that need attention.** GOV.UK task list: "The 'Completed' task now uses black text with no background colour, which will draw more attention to tasks that require action." Their research found that once a few tasks were done, it was "harder for users to scan the page and spot incomplete tasks". [Research] (GOV.UK says the new component itself still needs user testing) https://design-system.service.gov.uk/components/task-list/, https://design-system.service.gov.uk/patterns/complete-multiple-tasks/
- **No tag can mean a status.** "Sometimes a single status is enough… if something does not have a tag, that means it's incomplete." This supports Today showing nothing when there is no note (A17). [Convention] https://design-system.service.gov.uk/components/tag/
- **A badge is a different thing: a small count or dot attached to another element,** such as a navigation item. Material 3 and Android describe a badge as a small element showing "status or a numeric value on another composable". AgDS has a separate *Notification badge* "for numeric values" and a *Status badge* for "an item's status". In Grow2Notes, **badge** therefore means only the To review count on the Flagged menu item. Everything in this file is a tag or plain status text. [Convention] https://developer.android.com/develop/ui/compose/components/badges, https://m3.material.io/components/badges/overview, https://design-system.agriculture.gov.au/components/status-badge, https://design-system.agriculture.gov.au/components/notification-badge
- **Keep the status next to the thing it describes.** NSW says to "place status labels as close to the element as possible". NN/g says indicators "should be shown in close proximity to that element" and are conditional, meaning they appear only when the condition holds. [Convention] https://designsystem.nsw.gov.au/components/status-labels/index.html, https://www.nngroup.com/articles/indicators-validations-notifications/

### Colour

- **Never show a status by colour alone.** WCAG 1.4.1: colour must not be "the only visual means of conveying information". The Understanding document notes that "many older users do not see color well". [Standard] https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- **The same tag has the same colour everywhere.** GOV.UK: "If you use the same tag in more than one place, make sure you keep the colour consistent." [Convention] https://design-system.service.gov.uk/components/tag/
- **Use as few statuses and colours as possible.** "The more you add, the harder it is for users to remember them." NN/g's hierarchy guidance for simple designs is to "limit your color use to 2 primary and 2 secondary colors". [Convention] (practitioner guidance, not a measured threshold) https://design-system.service.gov.uk/components/tag/, https://www.nngroup.com/articles/visual-hierarchy-ux-definition/
- **Red is only for errors.** GOV.UK: "Do not use the red background colour for any status text except errors." NSW and Carbon also use red for error or danger, and amber or yellow for "action is required" or a warning. Carbon uses grey for "drafts or unstarted jobs". [Convention] https://design-system.service.gov.uk/patterns/complete-multiple-tasks/, https://designsystem.nsw.gov.au/components/status-labels/index.html, https://carbondesignsystem.com/patterns/status-indicator-pattern/
- **Don't mix solid fills with tints.** Solid fills stand out more, so NHS advises using "one or the other". [Convention] https://service-manual.nhs.uk/design-system/components/tag
- **Tag text needs 4.5:1 contrast against the tag fill. The fill itself does not need 3:1 against the page.** SC 1.4.3 applies to the text. SC 1.4.11 does not apply when text conveys the same information as the graphic. NHS goes further: all its tag text meets AAA. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html, https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html; [Convention] NHS (link above)
- **Forced-colours (high contrast) mode removes background fills,** so a tag can look like plain text. GOV.UK makes tags bold in forced colours. It deliberately does not add an outline, because an outline made tags "indistinguishable from a button". NHS takes the other route and gives tags a border in the text colour. [Standard] https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors; [Convention] GOV.UK Frontend source https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/tag/_mixin.scss

### Words

- **Sentence case, not capitals.** GOV.UK dropped uppercase in tags because "uppercase text can be harder to read, particularly for longer tag text". NHS testing "found that it helped to use sentence case rather than block capitals for readability". [Research] (qualitative) https://design-system.service.gov.uk/components/tag/, https://service-manual.nhs.uk/design-system/components/tag
  - Counterpoint: an MIT AgeLab study, reported by NN/g, found uppercase faster for *isolated words read at a glance* in a driving context. That setting is not a list read at the end of a shift. [Research] https://www.nngroup.com/articles/glanceable-fonts/
- **One to three words, no truncation.** AgDS recommends "a maximum of three words". Tags are not focusable, so truncated text cannot be revealed. Atlassian truncates long lozenges at 200 px, and that is the case to avoid. [Convention] https://design-system.agriculture.gov.au/components/status-badge, https://atlassian.design/components/lozenge/usage
- **Keep a tag the same size as the text around it.** GOV.UK tags use the body size (19 px), not a smaller caption size. [Convention] GOV.UK Frontend source (link above)

### Accessibility

- **A tag is plain text in the reading order.** It has no ARIA role and is not a live region. `role="status"` creates a live region with implicit `aria-live="polite"` and `aria-atomic="true"`. A list that gives every row's tag that role would read itself out on every refresh. [Standard] https://www.w3.org/TR/wai-aria-1.2/#status
- **Announce what an action did through the action's message, not the tag.** SC 4.1.3 covers messages about "the success or results of an action". The tag is the lasting record of the state, and the message is the announcement. [Standard] https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- **Tie the status to its row link with `aria-describedby`, and keep the link name short.** GOV.UK's task list gives each task link `aria-describedby="…-status"` and puts the status in a separate element. Its link name stays as the task name only. [Convention] https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/task-list/template.njk; [Standard] https://www.w3.org/WAI/ARIA/apg/practices/names-and-descriptions/
- **Status text must reflow and accept text spacing.** That means no fixed heights, no clipping at 200% zoom and no horizontal scrolling at 320 CSS px (SC 1.4.4, 1.4.10, 1.4.12). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html, https://www.w3.org/WAI/WCAG22/Understanding/reflow.html, https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- **Instructions must not depend on colour or position.** For example, never write "notes marked in yellow" (SC 1.3.3). [Standard] https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html

---

## Recommendation for Grow2Notes

### The rule in one line

**Every item has a lifecycle state, and that state is plain text. Only the exceptions some items carry get a tag. Tags come in two tones: amber for anything about a flag, grey for everything else.**

This gives the whole app one accent colour for statuses. Red stays reserved for errors, and green and blue are not used for status at all. [Opinion] built on the GOV.UK task-list research and the colour conventions above.

### Anatomy

**1. Status text (plain).** One word, in the normal text colour and weight, with no fill and no border. It is always the **first** item of the item's meta line.

**2. Tag.** One to three words in sentence case, set in a small rectangle with a light fill:

- **Fill.** Light tint with dark text, which avoids the "looks like a button" problem GOV.UK found.
- **Shape.** Corner radius 2 px, no shadow, and a **1 px border**: `--colour-attention-edge` (#b35900, 3.8:1 on the
  strong tint, foundations.md) on attention tags (Flagged, To review); `transparent` on neutral tags (Reviewed,
  Edited, Past-day note). In forced colours the browser draws every tag's border in the text colour, so every tag
  keeps its shape when fills disappear (the NHS route). The border keeps the attention tag visible in glare, where the
  1.3:1 fill alone disappears. [Convention] NHS tag; [Opinion] for glare; today.md Conflicts #10.
- **Text.** Same font size as the line it sits in; regular weight, in forced colours too.
- **Padding.** `0.125em 0.5em`.
- **Behaviour.** No pointer cursor and no hover change.

The tag must not share the button's look. Buttons are solid-filled and full-width or large. Tags are pale and inline.

**3. Meta line.** This is the line that holds the status, under the item's link:

```
[status text] · [who] · [when]  [tag] [tag]
```

- The `·` separators are hidden from screen readers. A visually hidden comma replaces each one to give a spoken pause.
- Tags come after the text in this fixed order: **Flagged → To review | Reviewed → Edited → Past-day note**. Flag-related tags come first. A fixed position means eyes learn where to look. [Opinion]
- Tags wrap to the next line as whole words. They are never truncated.

**4. Read-view status line (tag + detail).** In the read view, each tag starts its own line and its detail follows in plain text, completing the design's own sentence:

```
[Flagged] "Mentioned pain in his left knee after the walk."
[To review]
[Edited] last change by Sam Lee, 1 Oct 2026, 5:03 pm
[Past-day note] written on Sat 3 Oct 2026, 10:14 am by Jo Smith
```

A screen reader hears "Past-day note, written on…", which is the design.md string (3.8, 4.4). "Last change by" and "Reviewed by Jo Smith, Fri 2 Oct 9:30 am" reuse the daily-report strings (11.3), so the screen and the file use the same words. The read-view component owns the layout and the review comment. This file sets only the tag + detail pattern.

### The mapping (one source in code)

| Word (exact) | Treatment | Tone | Who sees it | Shown when | Where |
|---|---|---|---|---|---|
| Draft | Status text | none | Everyone | Live note in Draft. Detail is "You · saved {time}" for your own draft, "{name} · started {time}" for anyone else's. | Today, history list |
| Submitted | Status text | none | Everyone | Note is Submitted. On version history, only on the version 1 row. | Today, history list, version history |
| Flagged | Tag | **attention** | Everyone | `status = Submitted` **and** the current version is flagged. **Never on a draft**, because a flag on a draft is not seen (3.6). | Today, history list, read view |
| To review | Tag | **attention** | Managers | `flagStatus = ToReview` (stays even if a later edit removed the tick, A15) | History list, read view |
| Reviewed | Tag | neutral | Managers | `flagStatus = Reviewed` | History list, read view |
| Edited | Tag | neutral | Everyone | `isEdited` (more than one version) | History list, read view |
| Past-day note | Tag | neutral | Everyone | `isPastDayNote` | Read view |
| Invited / Active / Deactivated | Status text | none | Managers | Always one of the three | Users list (and user detail) |

On the Flagged list, *Flag removed in a later edit* is a plain-text line in the row, not a tag. It is a sentence, and tags are kept to one to three words. In the read view it follows **To review** as that tag's detail.

### Colour tokens

These are suggested values. Contrast was computed with the WCAG 2 formula on 1 October 2026. Any substitute must keep tag text at **≥ 7:1** against its fill (AAA, as NHS does). The legal floor is 4.5:1. The higher target allows for glare on phones and tired eyes. [Opinion]

| Token | Value | Check |
|---|---|---|
| `--tag-neutral-text` | `#1F2328` | 13.1:1 on `#E8EAED` |
| `--tag-neutral-bg` | `#E8EAED` | 1.2:1 against white. That is allowed, because the fill carries no meaning (SC 1.4.11). |
| `--tag-attention-text` | `#5C3A00` | 8.1:1 on `#FFE2A8` |
| `--tag-attention-bg` | `#FFE2A8` (amber tint) | 1.3:1 against white; allowed, same reason |
| Status text | the normal body text token | ≥ 4.5:1 (should already be met by the body text token) |

Both tones are tints, so solids and tints are never mixed. There is no dark-theme variant, because design.md specifies no dark theme.

### Behaviour

- **Not interactive.** Tags and status text are never links or buttons and never take focus. The **row** is the link and the 44 × 44 px target. A tap on a tag opens the row, which is exactly what GOV.UK's "whole row linked" research points to.
- **Status changes show without announcement or animation.** They switch instantly, with no fade and no pulse.
  - After the user's own action, the action's message speaks: "Note for Jane Citizen submitted" (4.3), or the Mark reviewed confirmation. The tag just shows the new state.
  - When a background refresh brings in someone else's change (Today and the badge refresh on page load and window focus, 6.8), nothing is announced.
- **No tooltips** (`title`) explaining tags. The words are the explanation.
- **No icons or emoji** in tags. GOV.UK and NHS tags are text-only. AgDS pairs an icon with text, which is a valid convention, but here it would add assets and alternative text for no new meaning. [Opinion]

### States

| State | Status text and tags |
|---|---|
| Default | As in the mapping above |
| Hover / active | None of their own. The row link owns hover and pressed styles. The tag fill must stay visible on the row's hover background. Text contrast is unaffected, because it is measured against the tag's own fill. |
| Focus | None of their own. The row link's focus outline (always visible, 4.0) surrounds the whole row, tags included. |
| Disabled | Not applicable. Never fade a tag or lower its opacity to mean "inactive". That applies to Deactivated users too: lower contrast fails SC 1.4.3 and makes the row look disabled. |
| Error | Not applicable. There is no error tag. Errors use the error message components, and red stays theirs. |
| Loading | Tags render with their row, never as a skeleton or placeholder. During a background refetch, the previous data and tags stay on screen (TanStack Query keeps cached data while refetching). Nothing blanks out. |
| Empty | No note means no status line at all. Never "Not started", "None", "No note" or "Missing" (D25, A17). Users always have a status. |
| Read-only | Always read-only. |
| Forced colours | Fills disappear; every tag keeps its 1 px border (drawn in the system text colour), so tags stay distinct from plain status text. Weight stays regular. |
| 200% text / 320 px width | The meta line wraps and tags drop whole onto the next line. No fixed heights; padding in `em`. |

### Phone and laptop

- **Phone.** Two lines per row: the name or date (link) on line 1, the meta line on line 2. Up to three tags (history, manager) wrap to a third line if needed.
- **Laptop.** Same markup. Keep the list column narrow enough (about 40 rem) that tags stay near the name. **Don't** push tags into a far-right column on a wide row (the proximity guidance above).
- **Users on laptop.** May be a table (name, email, role, status). Status is then a plain-text cell under a "Status" column header, which gives screen readers the context. On a phone, status goes on the meta line: `Worker · Invited`.

### Accessibility

- **Semantics.** Tags are `<span>`s with text. Status text is plain text. Neither has an ARIA role.
- **Separators.** `<span aria-hidden="true"> · </span>` plus a visually hidden comma. Whether each screen reader speaks "·" is **unverified**. Hiding it is harmless and gives the same pause everywhere.
- **Linking status to the row.** The row link's accessible name is only the participant's name (Today) or the note date (history) or "Version n" (version history). The meta line has an `id`, and the link points to it with `aria-describedby`. A screen reader then hears "Jane Citizen, link, Submitted, Alex P., 4:12 pm, Flagged".
  - The meta line also stays in the reading order straight after the link. Users who swipe still reach it even if their screen reader doesn't speak descriptions (for example, VoiceOver with hints turned off; **unverified** per screen reader).
  - Lists of links (the rotor or elements list) show clean names.
- **Keyboard.** Nothing to add. The tag is not a tab stop. Tab moves row to row.
- **Screen-reader announcements.** None from the tag. Announcements come from the action's message in the live region the notifications component already has in the DOM.
- **WCAG 2.2 criteria met.**
  - 1.3.1 Info and Relationships: status is real text, tied to its row.
  - 1.3.3 Sensory Characteristics: no copy refers to colour.
  - 1.4.1 Use of Color: the word is always present.
  - 1.4.3 Contrast (Minimum): ≥ 7:1 tags, ≥ 4.5:1 text.
  - 1.4.4 Resize Text, 1.4.10 Reflow, 1.4.12 Text Spacing.
  - 1.4.11 Non-text Contrast: not applicable, because the fills carry no meaning.
  - 2.4.7 Focus Visible and 2.5.8 Target Size: on the row, which also meets the app's 44 px rule.
  - 4.1.3 Status Messages: met by the action messages, not by tags.

### Implementation (React 19, native HTML, CSS Modules)

Native HTML covers all of this, so no React Aria is needed. Put the words, tones and order in **one module**. Screens pass a *kind*, never free text or a colour, so the mapping cannot drift between screens.

```ts
// src/components/status/statusWords.ts — the only place these words and tones live
export const NOTE_STATUS = { Draft: 'Draft', Submitted: 'Submitted' } as const;
export const USER_STATUS = { Invited: 'Invited', Active: 'Active', Deactivated: 'Deactivated' } as const;

export const TAGS = {
  flagged:  { text: 'Flagged',       tone: 'attention' },
  toReview: { text: 'To review',     tone: 'attention' },
  reviewed: { text: 'Reviewed',      tone: 'neutral' },
  edited:   { text: 'Edited',        tone: 'neutral' },
  pastDay:  { text: 'Past-day note', tone: 'neutral' },
} as const;
export type TagKind = keyof typeof TAGS;

const ORDER: TagKind[] = ['flagged', 'toReview', 'reviewed', 'edited', 'pastDay'];
export const inOrder = (kinds: TagKind[]) => ORDER.filter((k) => kinds.includes(k));

// History row (4.4). The API sends flagStatus only to managers.
export function historyTags(n: { status: 'Draft' | 'Submitted'; isEdited: boolean;
  isFlagged: boolean; flagStatus?: 'None' | 'ToReview' | 'Reviewed' }): TagKind[] {
  const t: TagKind[] = [];
  if (n.status === 'Submitted' && n.isFlagged) t.push('flagged'); // never on a draft
  if (n.flagStatus === 'ToReview') t.push('toReview');
  if (n.flagStatus === 'Reviewed') t.push('reviewed');
  if (n.isEdited) t.push('edited');
  return t;
}
```

```tsx
// Tag.tsx
import styles from './Tag.module.css';
import { TAGS, type TagKind } from './statusWords';

export function Tag({ kind }: { kind: TagKind }) {
  const { text, tone } = TAGS[kind];
  return <span className={`${styles.tag} ${styles[tone]}`}>{text}</span>;
}

// MetaLine.tsx: "Submitted · Alex P. · 4:12 pm  [Flagged]"
import { Fragment } from 'react';
export function MetaLine({ id, parts, tags = [] }:
  { id?: string; parts: string[]; tags?: TagKind[] }) {
  return (
    <p id={id} className={styles.meta}>
      {parts.map((part, i) => (
        <Fragment key={i}>
          {i > 0 && (<><span aria-hidden="true"> · </span><span className="visually-hidden">, </span></>)}
          {part}
        </Fragment>
      ))}
      {inOrder(tags).map((k) => (<Fragment key={k}>{' '}<Tag kind={k} /></Fragment>))}
    </p>
  );
}
```

```tsx
// Today row (4.2). The whole row is the link; status is its description.
function TodayRow({ p, href }: { p: TodayParticipant; href: string }) {
  const statusId = useId();
  const n = p.note;
  return (
    <li className={styles.row}>
      <Link to={href} className={styles.rowLink} aria-describedby={n ? statusId : undefined}>
        {p.givenName} {p.familyName}
      </Link>
      {n && (
        <MetaLine
          id={statusId}
          parts={
            n.status === 'Draft'
              ? n.isMine
                ? ['Draft', 'You', `saved ${formatTime(n.savedAtUtc!)}`]
                : ['Draft', n.authorDisplayName, `started ${formatTime(n.startedAtUtc)}`]
              : ['Submitted', n.authorDisplayName, formatTime(n.firstSubmittedAtUtc!)]
          }
          tags={n.status === 'Submitted' && n.isFlagged ? ['flagged'] : []}
        />
      )}
    </li>
  );
}
```

```css
/* Tag.module.css */
.tag {
  display: inline-block;
  max-width: 100%;
  padding: 0.125em 0.5em;
  border-radius: 0.125rem;
  border: 1px solid transparent; /* forced colours draw it in CanvasText, so every tag keeps its shape */
  font: inherit;             /* same size as the meta line, never smaller */
  font-weight: 400;
  overflow-wrap: break-word; /* wrap, never ellipsis */
}
.neutral   { color: var(--tag-neutral-text);   background-color: var(--tag-neutral-bg); }
.attention { color: var(--colour-attention-text); background-color: var(--colour-attention-tint-strong);
             border-color: var(--colour-attention-edge); } /* foundations.md tokens; 8.1:1 text */
/* No forced-colors override: the border is the forced-colours cue (editorial pass; replaces the bold rule). */
.meta { margin: 0; line-height: 1.5; } /* room for wrapped tags; no fixed height */

/* Row (shown for context; the list components own it) */
.row { position: relative; min-block-size: 44px; }
.rowLink::after { content: ""; position: absolute; inset: 0; } /* whole row is the target */
.rowLink:focus-visible { outline: none; }
.rowLink:focus-visible::after { outline: 3px solid var(--focus); outline-offset: -3px; }
```

- **Times and dates.** `formatTime` is the shared Melbourne-time helper: `Intl.DateTimeFormat('en-AU', { timeZone: 'Australia/Melbourne', hour: 'numeric', minute: '2-digit' })` gives "4:12 pm" (checked in Node 24; browser ICU versions may differ, so test it).
- **Ids.** `useId()` gives each row a stable id for `aria-describedby`.
- **Refetching.** Don't blank tags during a TanStack Query refetch. The default already keeps cached data on screen.

---

## Per-screen notes

**Today (4.2)**
- Status lines are word-for-word from design.md: *Draft · You · saved 9:42 am*, *Draft · Alex P. · started 9:14 am*, *Submitted · Alex P. · 4:12 pm*.
- **Flagged** is the only tag on Today. Show it only for Submitted notes whose current version is flagged.
- Don't add Edited, To review or any "not started" marker here.
- "Your unfinished drafts" rows have no status word; the heading says it.
- Managers see the same Today as workers. The To review signal lives in the menu badge, not on Today rows.

**Participant notes history, list (4.4)**
- Meta line: `Submitted · Alex P.` then the tags in order, for example `[Flagged] [To review] [Edited]`. A Draft row shows `Draft · Alex P.` and never a tag.
- Workers never get To review or Reviewed, because the API doesn't send `flagStatus` to them.
- **Show older** loads more rows below. New rows use the same component and need no announcement beyond the button's own behaviour.

**Read view (4.4)**
- Use the tag + detail lines above, in the same order as the list.
- **Reviewed** detail: "by Jo Smith, Fri 2 Oct 9:30 am", from the report string (11.3). The comment goes underneath and the read-view component owns it.
- When a later edit removed the flag, show `[To review] Flag removed in a later edit`. There is no **Flagged** tag in that case, because the current version is not flagged.
- On a manager's read-only view of someone else's draft: status text **Draft**, no tags.

**Version history (4.5)**
- Row link: "Version 3".
- Meta line for later versions: `Sam Lee (manager) · 1 Oct 2026, 5:03 pm`.
- Version 1's meta line starts with the status text: `Submitted · Alex P. · 1 Oct 2026, 4:12 pm`. That matches Today's submitted line exactly.
- Don't add "Current" or "Latest" tags. The header already gives the current version number.
- "Not edited since submit" is a plain sentence.

**Flagged notes (4.6)**
- The tab label carries the state, so rows have no tag.
- *Flag removed in a later edit* is a plain-text line in the row.
- Keep the badge and the To review tab count in the **attention** tone tokens if they are coloured, so amber always means "flag". The navigation and tabs components own them, and the count must be in the item's text, not only visual.

**Users (4.11)**
- **Invited / Active / Deactivated** as plain status text. Never as tags, never greyed out, never coloured.
- Keep "Deactivated" for users. "Archived" belongs to participants, goals and common items (3.7). They are different words for different things, and neither is ever a synonym for the other.

### Points the design leaves open

These are for the owner or the named component. They are not recommendations to add anything.

1. **Past-day note in list rows.** A11 says the past-day line shows "everywhere it appears". But the 4.4 list spec names only Edited, Flagged, To review and Reviewed, and the history API (6.3) returns no `isPastDayNote`. Default here: read view only, following 4.4 as written.
2. **Flagged on version rows.** `GET {base}/versions` returns `isFlagged` per version, but 4.5 doesn't show a flag on version rows. Default: no tag.
3. **Pending edits in "Your unfinished drafts".** `/api/me/drafts` returns `kind: draft | pendingEdit`, but 4.2 shows only the participant and date. If the owner wants the two told apart, the existing string "Editing submitted note" could be reused. Default: nothing added.
4. **Date and time formats** differ between design.md strings: "1 Oct 2026, 5:03 pm", "Fri 2 Oct 9:30 am", "Sat 3 Oct 2026, 10:14 am", and the brief's "Thursday 1 October 2026". The shared date formatter should settle one pattern for lines with a date and time. This file uses design.md's strings as written.
5. **Past-day wording order** differs. 3.8 says "written on [date, time] by [manager]", while the report (11.3) says "written by Jo Smith (manager) on …". The screen follows 3.8.

---

## Anti-patterns to avoid

1. **Colour-only status.** Dots, tinted rows or coloured left borders without the word (SC 1.4.1).
2. **Tags that look or act like buttons.** White text on a dark fill, pointer cursor, hover effect, `tabindex="0"`, or the tag as a link (GOV.UK research).
3. **Red for Flagged** (red is for errors; and D16: a flag is not an incident). **Green for Submitted or Active** (an extra colour with no extra meaning).
4. **A filled tag on every row's normal state** (Submitted, Active). Nothing stands out any more (GOV.UK task-list research).
5. **`role="status"` or `aria-live` on tags.** It creates one live region per row and reads the list aloud on every refresh.
6. **Status words inside the link name.** For example "Jane Citizen Submitted Alex P. 4:12 pm Flagged" as the link text clutters links lists. Use `aria-describedby`.
7. **"Not started", "Missing", "No note", "Overdue" or "Late"** on Today (D25, A17).
8. **Flagged on a draft.** It suggests a manager has been alerted when they haven't (3.6, A14).
9. **To review or Reviewed shown to workers** (2, A14).
10. **Synonyms.** "Pending", "Needs review", "Unreviewed", "Complete", "Done", "Sent", "Amended", "Updated", "Changed", "Backdated", "Late entry", "Inactive", "Disabled", "Suspended", or "Archived" for users. One word per concept, exactly as design.md spells it.
11. **ALL CAPS, letter-spacing, or tag text smaller than the line around it.**
12. **Ellipsis truncation or a fixed tag height** that clips at 200% zoom or with text spacing.
13. **Icons or emoji instead of words** (🚩, ✓). Also tooltips (`title`) used to explain a tag.
14. **Fading Deactivated users or drafts with opacity.** It fails contrast and reads as disabled.
15. **Pulsing, fading or sliding a tag in to draw attention.** State changes are instant.
16. **Help text that refers to colour,** such as "yellow notes need review" (SC 1.3.3).
17. **Inventing statuses the design doesn't have,** such as "Current", "Latest", "New", a count inside a tag, or a "Spot-checked" state (A16).
18. **Tags pushed to the far right of a wide laptop row,** away from the item they describe.

---

## Tensions with decisions

- **None with D1–D43.** The decisions and assumed defaults match best practice for this component. A17 (no status when there is no note) is the GOV.UK "single status" approach, and design 4.0 already requires status "in words, never by colour alone".
- **Not a decision, recorded for the chosen tag style.** GOV.UK deliberately gives tags no outline in forced colours, because an outline made tags "indistinguishable from a button" in its research. This app follows NHS's border instead. Tags here are never interactive and never sit beside buttons in a row, which lowers that risk; it should be watched in the M6 device tests.
- **One soft point about a design.md string, with no change proposed.** GOV.UK, NHS and NSW advise tag names that are adjectives, not verbs, so a tag doesn't read as an action. "To review" is a verb phrase.
  - The guidance is a stated rationale, not a test of this phrase.
  - AgDS's own examples include "To be updated" and "To be added".
  - "To review" is also the tab name and the API term.
  - The risk is handled by the non-button styling and the whole-row link, so the string stays as designed.

---

## Sources

**Standards**
- WCAG 2.2 Understanding 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html
- WCAG 2.2 Understanding 1.4.3 Contrast (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- WCAG 2.2 Understanding 1.4.11 Non-text Contrast: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 Understanding 1.3.1 Info and Relationships: https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html
- WCAG 2.2 Understanding 1.3.3 Sensory Characteristics: https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html
- WCAG 2.2 Understanding 1.4.4 Resize Text: https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Understanding 1.4.12 Text Spacing: https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- WCAG 2.2 Understanding 2.5.8 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WAI-ARIA 1.2, role `status`: https://www.w3.org/TR/wai-aria-1.2/#status
- WAI-ARIA APG, Providing Accessible Names and Descriptions: https://www.w3.org/WAI/ARIA/apg/practices/names-and-descriptions/
- MDN, `aria-describedby`: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-describedby
- MDN, `forced-colors`: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/forced-colors

**Research write-ups (qualitative, from service teams)**
- GOV.UK Design System, Tag (research on case and button-like tags): https://design-system.service.gov.uk/components/tag/
- GOV.UK Design System, Task list (research on statuses and whole-row links): https://design-system.service.gov.uk/components/task-list/
- GOV.UK Design System, Complete multiple tasks pattern: https://design-system.service.gov.uk/patterns/complete-multiple-tasks/
- NHS digital service manual, Tag (tested in three NHS services): https://service-manual.nhs.uk/design-system/components/tag
- NN/g, Typography for Glanceable Reading (Laubheimer, 2017; MIT AgeLab study): https://www.nngroup.com/articles/glanceable-fonts/

**Conventions**
- GOV.UK Frontend tag styles (font size, forced-colours handling): https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/tag/_mixin.scss
- GOV.UK Frontend task list template (`aria-describedby` to status): https://github.com/alphagov/govuk-frontend/blob/main/packages/govuk-frontend/src/govuk/components/task-list/template.njk
- Australian Government Design System, Status badge: https://design-system.agriculture.gov.au/components/status-badge
- Australian Government Design System, Notification badge: https://design-system.agriculture.gov.au/components/notification-badge
- NSW Design System, Status labels: https://designsystem.nsw.gov.au/components/status-labels/index.html
- Carbon, Status indicator pattern: https://carbondesignsystem.com/patterns/status-indicator-pattern/
- Atlassian, Lozenge usage: https://atlassian.design/components/lozenge/usage
- Material 3, Badges: https://m3.material.io/components/badges/overview
- Android Developers, Badges: https://developer.android.com/develop/ui/compose/components/badges
- NN/g, Indicators, Validations, and Notifications (Flaherty, 2024): https://www.nngroup.com/articles/indicators-validations-notifications/
- NN/g, Visual Hierarchy in UX (Gordon, 2021): https://www.nngroup.com/articles/visual-hierarchy-ux-definition/
- React, `useId`: https://react.dev/reference/react/useId
