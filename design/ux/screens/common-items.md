# Common items

Screen spec for design.md §4.9, with decisions D44 and D45 (common items in groups, the built-in Every note group).
Managers only. Route `/manage/common-items`, page title **Grow2Notes – Common items**.

It puts these researched component specs together for this one screen:
[group-picker](../components/group-picker.md) (Manager side), [list-editor](../components/list-editor.md),
[form-validation](../components/form-validation.md), [empty-loading-error](../components/empty-loading-error.md),
[foundations](../components/foundations.md) and [microcopy](../components/microcopy.md). It also uses the shared
button and status region from [primary-actions](../components/primary-actions.md), the character count from
[conditional-reveal](../components/conditional-reveal.md), the drawn radio (ChoiceRow) from
[checkbox-list](../components/checkbox-list.md) and users.md, and the shell from
[app-shell-nav](../components/app-shell-nav.md).

Copy marks: **(V)** design.md word for word · **(N)** design.md words with only punctuation normalised to the
microcopy rules · **(P)** proposed, needs the owner's sign-off. Evidence grades: **[Research]** · **[Standard]** ·
**[Convention]** · **[Opinion]**.

**Assumed defaults this screen builds on** (design.md §13; shared with every file that touches groups and stated in
full in group-picker.md). They are assumed defaults, not decisions: the owner can override any of them by adding a
decision.

- **A41** Groups are organisation-wide. Managers add, rename, reorder, archive and restore them on this screen;
  nothing is deleted. Groups bring no new screens, notifications or settings.
- **A42** Each common item belongs to exactly one group. A manager moves an item to another group from the item's
  Edit; it goes to the end of that group.
- **A43** **Every note** is built in: always first, cannot be renamed, moved or archived. With no items, it is not
  shown on notes.
- **A45** (part) A group with no active items is not offered on notes.
- **A2** (extended) A restored item goes to the end of its group; a restored group goes to the end of the groups.
- **A3** (extended) Notes already started or submitted keep their groups, names, wording and order.
- **A6** (extended) A group name is up to 200 characters, the same as an item.

This spec adds no feature. There is no drag and drop (between rows or between groups), delete, confirmation dialog,
undo toast, count, search, "Save order" step, separate Groups screen, per-participant setting (D11, D44: any group
can be picked on any note) or unsaved-changes warning. design.md asks for that warning only on Guide prompts (§4.10).

---

## Purpose and who uses it

- **Purpose (V):** "keep the organisation-wide common items (everyday activities or items that are ticked off but
  are not goals), organised into groups" (D9–D11, D44, D45).
- **Who:** managers only (§2), on a laptop or a phone. Workers can never reach it: the route guard shows "Page not
  found", so the screen's existence is not revealed (app-shell-nav, design §9.2).
- **How often:** rarely. It is used heavily during set-up before go-live (the groups first, then their items), then
  for the odd reword, reorder, move or archive. Each visit therefore has to be understood without practice. That is
  one more reason for visible, labelled buttons and no gestures to remember. [Opinion]
- **What a change does:** **Every note**'s items appear on **every** participant's note started from then on. Another
  group's items appear on a note only when the writer ticks that group under "Which of these happened?" on the note
  form (D44). Groups, and the items in each group, show on notes in the order set here (§3.1, §3.4 snapshot, §3.7,
  A2, A3). A group with no active items is not offered on notes (A45). Notes already started or submitted keep the
  groups, names, wording and order they had. By design, the help text is the only warning. No confirmation is used,
  because every change can be reversed (confirm-dialog.md: archiving goals or common items gets no dialog; the same
  holds for groups).
- **How managers get here:** nav **Manage** → **Common items** (app-shell-nav: the Manage page holds only the four
  links). On this screen **Manage** has `aria-current="true"` (current section).

---

## Layout - phone

375 px wide, with 16 px gutters (343 px of content). The example groups and items are illustrative. Shown top to
bottom.

```
+---------------------------------------+
| Grow2Notes                  Account ▾ |  app shell: never sticky (app-shell-nav)
| Today  Flagged 3  Report  Manage      |  Manage = current section (bold + 4 px bar)
+---------------------------------------+
| Common items                          |  <h1 tabindex=-1>, 28 px bold; focused on arrival
|                                       |
| Items in Every note show on every     |  help text <p id=items-help>, 18 px,
| note. For other groups, the writer    |  --colour-text-secondary
| ticks the ones that happened. Changes |
| apply to notes started from now on.   |
|                                       |
| (list-conflict message: only after    |  <p tabindex=-1>, 4 px bar; takes focus
|  a conflict)                          |
|                                       |
| Every note                            |  <section aria-labelledby> <h2>, 22 px bold
| Always shown first, on every note.    |  <p>, secondary text; NO group buttons
|                                       |
|  1. Medication prompted               |  <ol> per group; numbers restart per group
|     (row alert slot: empty, 0 height) |  role=alert, always rendered
|     [↑ Move up ] [↓ Move down ]       |  pair 1 (Move up on row 1 is unavailable:
|     [ Edit ] [ Archive ]              |  pair 2   grey fill, dashed border)
| ───────────────────────────────────── |  1 px --colour-divider
|  2. Meal prepared                     |
|     [↑ Move up ] [↓ Move down ]       |  Move down on the group's last row:
|     [ Edit ] [ Archive ]              |  unavailable
| ───────────────────────────────────── |
| New item in Every note                |  <label for="item-new-{groupId}">
| +-----------------------------------+ |
| |                                   | |  <textarea rows=3>, 18 px, no maxlength
| +-----------------------------------+ |
| You can enter up to 200 characters    |  character count, 16 px
| (form alert slot: empty)              |
| [             Add item              ] |  primary, full width
| ▸ Archived items                      |  this group's, only when it has archived items
|                                       |  --section-gap (32 px)
| Community outing                      |  <section aria-labelledby> <h2>
|   (group alert slot: empty)           |  role=alert, always rendered
| [↑ Move up ] [↓ Move down ]           |  group buttons, same pairs as item rows;
| [ Rename ] [ Archive ]                |  hidden ", Community outing group"
|                                       |  (Move up unavailable: first after Every note)
|  1. Travelled by bus or train         |
|     [↑ Move up ] [↓ Move down ]       |
|     [ Edit ] [ Archive ]              |
| ───────────────────────────────────── |
| New item in Community outing          |
| +-----------------------------------+ |
| +-----------------------------------+ |
| You can enter up to 200 characters    |
| [             Add item              ] |
|                                       |
| Personal care                         |
| [↑ Move up ] [↓ Move down ]           |  Move down unavailable: last group
| [ Rename ] [ Archive ]                |
| No items in this group. It does not   |  empty-group line (V), plain <p>
| show on notes until it has one.       |
| New item in Personal care             |
|  ...                                  |
|                                       |  --section-gap
| New group                             |  <label for="group-new-name">
| +-----------------------------------+ |
| |                                   | |  <input type="text">, 48 px, no maxlength
| +-----------------------------------+ |
| You can enter up to 200 characters    |
| (form alert slot: empty)              |
| [ Add group ]                         |  secondary, label width
|                                       |  --section-gap
| ▸ Archived groups                     |  <details>, closed on load; rendered only
|                                       |  when at least one group is archived
+---------------------------------------+
```

**An item in edit mode** replaces that row's text and buttons. The row keeps its number. The **Group** radios show
only when there are at least two active groups (Conflicts resolved, 18).

```
|  2. Edit item                         |  <label>
|     +-------------------------------+ |
|     | Meal prepared                 | |  textarea rows=3, current text, caret at the end
|     +-------------------------------+ |
|     You have 187 characters remaining |  character count
|     Group                             |  <fieldset><legend>, radios (ChoiceRow)
|     (•) Every note                    |  56 px rows, 40 px drawn circle;
|     ( ) Community outing              |  every active group in order,
|     ( ) Personal care                 |  the item's current group selected
|     (form alert slot: empty)          |
|     [ Save ]  [ Cancel ]              |  primary + secondary, label width, 16 px apart
```

**A group being renamed** (the group buttons give way to a one-field form under the heading; its items stay below,
unchanged):

```
| Community outing                      |  the <h2> stays, so the region keeps its name
| Group name                            |  <label for="group-{id}-name">
| +-----------------------------------+ |
| | Community outing                  | |  <input type="text">, current name, caret at end
| +-----------------------------------+ |
| You have 184 characters remaining     |
| (form alert slot: empty)              |
| [ Save ]  [ Cancel ]                  |
|                                       |
|  1. Travelled by bus or train         |
```

**A field error after Add item** (a one-field form, so there is no error summary):

```
| ▌New item in Community outing         |  4 px --colour-error bar on the field group
| ▌Enter the common item                |  bold error text ABOVE the field, so the phone
| ▌+-----------------------------------+|  keyboard cannot hide it
| ▌|                                   ||  3 px error border; aria-invalid="true"
| ▌+-----------------------------------+|
| ▌You can enter up to 200 characters   |
| [             Add item              ] |
```

The New group and Group name fields show their errors the same way: **Enter the group name** or **Group name must be
200 characters or less** (V).

**The archived sections, open:**

```
| ▾ Archived items                      |  per group; plain-text <summary>, default marker
|   • Laundry done                      |  <ul> > <li>, most recently archived first
|     [ Restore ]                       |
|                                       |
| ▾ Archived groups                     |  page level, after New group
|   • Overnight respite                 |  <ul> > <li>, most recently archived first
|     [ Restore ]                       |  name and Restore only
```

At 320 px, or with 200% text, each pair may break into single buttons. Nothing scrolls sideways (SC 1.4.10).

---

## Layout - laptop

Same parts, same order, same words and the same behaviour. Only these things change:

| What | Phone (< 40rem) | Laptop (≥ 40rem, the one breakpoint in foundations) |
|---|---|---|
| Page width | Full width minus 16 px gutters | Up to `--page-max` (60rem), left-aligned in the page container. design.md §4.0 says "the setup screens use the extra space". |
| Gutters, section gaps, h1, h2 | 16 px, 32 px, 28 px, 22 px | 32 px, 48 px, 32 px, 24 px (foundations tokens) |
| Help text, textareas, text inputs | Full width | Capped at `--measure` (40rem), so lines stay readable |
| Group heading row | Heading on top, then the group buttons in two pairs | Heading and the four group buttons **side by side when the row has room**: the same wrapping flex line as the item rows (CSS below), so the buttons drop under the heading by themselves at 200% text or in a narrow window |
| Item rows | Text on top. Actions under it in two pairs | Text and actions **side by side when the row has room**: text on the left, all four buttons on one line on the right, aligned to the top of the text. No extra breakpoint: the row is a wrapping flex line, so the actions drop under the text by themselves when the text column would be narrower than 16rem (for example at 200% text or in a narrow window). See CSS below. |
| Add item, Try again | Full width | Label width, at least 8rem (primary-actions) |
| Row and group buttons (Move up, Move down, Edit, Rename, Archive, Restore, Save, Cancel, Add group) | Label width | Label width. They do not take the shared 8rem minimum (see Conflicts resolved, 6) |
| Edit and rename forms | Full row width | Full row width, fields capped at 40rem |
| Hover | None (`hover: none`) | Button hover fills (foundations, primary-actions) |

```
| Common items                                                                          |  h1 32 px
| Items in Every note show on every note. For other groups, the writer ticks the ones   |  help, ≤ 40rem
| that happened. Changes apply to notes started from now on.                            |
|                                                                                       |
| Every note                                                                            |  h2, no buttons
| Always shown first, on every note.                                                    |
|  1. Medication prompted                   [↑ Move up] [↓ Move down]  [Edit] [Archive] |
|  ───────────────────────────────────────────────────────────────────────────────────  |
|  2. Meal prepared                         [↑ Move up] [↓ Move down]  [Edit] [Archive] |
| New item in Every note                                                                |
| +--------------------------------------------------+                                  |  ≤ 40rem
| +--------------------------------------------------+                                  |
| You can enter up to 200 characters                                                    |
| [ Add item ]                                                                          |
|                                                                                       |
| Community outing                          [↑ Move up] [↓ Move down] [Rename] [Archive]|  h2 + group buttons
|  1. Travelled by bus or train             [↑ Move up] [↓ Move down]  [Edit] [Archive] |
| New item in Community outing                                                          |
|  ...                                                                                  |
|                                                                                       |
| New group                                                                             |
| [__________________________________________________]                                  |  ≤ 40rem
| You can enter up to 200 characters                                                    |
| [ Add group ]                                                                         |
|                                                                                       |
| ▸ Archived groups                                                                     |
```

Arithmetic, [Opinion], to be checked in the browser: at 18 px bold with 1.25rem side padding, the four row buttons
take about 33rem. With the 16rem text minimum, the 1rem gap and the 2.25rem number column, rows sit side by side
from a list width of about 52rem. That is reached on a normal laptop window at the 60rem page width. No number is
hard-coded, so wrong estimates cannot cause overflow. The row just stacks.

```css
/* ListEditor.module.css, row layout. Replaces list-editor.md's @container rule (Conflicts resolved, 5) */
.row      { padding-block: var(--space-3); border-block-end: var(--border-divider) solid var(--colour-divider); } /* li stays display:list-item */
.rowInner { display: flex; flex-wrap: wrap; align-items: flex-start; gap: var(--space-2) var(--space-4); }
.text     { flex: 1 1 16rem; min-inline-size: 0; margin: 0; overflow-wrap: anywhere; }
.side     { flex: 0 1 auto; min-inline-size: 0; }            /* holds the row alert slot, then the actions */
.actions  { display: flex; flex-wrap: wrap; gap: var(--space-2) var(--space-4); } /* 16 px between the pairs */
.pair     { display: flex; flex-wrap: wrap; gap: var(--space-2); }                 /* 8 px inside a pair */
.actions .rowButton { inline-size: auto; min-inline-size: var(--target-min); }    /* not full width, no 8rem minimum */
.list     { margin: 0; padding-inline-start: 2.25rem; }
.list > li::marker { font-weight: 700; font-variant-numeric: tabular-nums; }

/* CommonItemsPage.module.css: the group heading row reuses the same wrapping technique */
.groupHead { display: flex; flex-wrap: wrap; align-items: flex-start; gap: var(--space-2) var(--space-4); }
.groupHead > h2 { flex: 1 1 16rem; min-inline-size: 0; margin: 0; overflow-wrap: anywhere; }
.group + .group { margin-block-start: var(--section-gap); }
```

[Standard/Convention] A flex item with a `flex-basis` wraps onto a new line "once there is not enough space to place
another … item in a row" (MDN, Mastering wrapping of flex items). The DOM order is the visual order in both layouts
(no `order`, no `row-reverse`; SC 1.3.2, 2.4.3).

---

## Components, in order

DOM order is reading order is visual order.

### 0. App shell: [app-shell-nav](../components/app-shell-nav.md)
- Skip link "Skip to main content", header, nav, then `<main id="main-content">`. Nothing is sticky or fixed.
- Page name "Common items" from the fixed list. Title **Grow2Notes – Common items** (V pattern). No names in the
  title or the URL (§4.0).
- Role guard: a worker (or a manager demoted mid-session, `403` on the list request) sees **Page not found**
  (empty-loading-error).

### 1. Page heading
- `<h1 id="page-heading" tabIndex={-1}>Common items</h1>` **(V)**, using the shared `PageHeading`. It is a fixed
  heading, so it renders at once and takes focus when the screen opens (app-shell-nav, empty-loading-error rule 1).
- `--font-size-h1`, sentence case, `width: fit-content` so the focus ring hugs the text.

### 2. Help text
- **"Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes apply
  to notes started from now on."** **(V, design.md 4.9)**. It replaces the earlier 4.9 string ("Shown on every
  participant's note, in this order…"), which stopped being true with D44.
- `<p id="items-help">`, body size (18 px), `--colour-text-secondary` (9.0:1), `max-inline-size: var(--measure)`.
  Shown at once, before the list loads, because it is static.
- Linked to every **New item in [group name]** and **Edit item** textarea through `aria-describedby`, because it
  states the consequence of adding or rewording (list-editor). Not linked to the group name fields: three sentences
  heard on every group field would bury the count [Opinion].
- Do **not** add design.md §3.7's "Rewording is for typos…" sentence. That belongs in manager training, not on
  screen (list-editor).

### 3. Load region: [empty-loading-error](../components/empty-loading-error.md)
- One `LoadRegion` around everything that depends on the data (the conflict slot, every group section, the New group
  form and the archived groups). Its `<p role="status">` is always in the DOM.
- `loading` = **"Loading common items…"** (P), shown only after 1 s. `what` = **"common items"**. No
  `fallbackHeading`, because the `<h1>` is fixed.
- Failure text (P, microcopy.md §9): **"Common items did not load: no connection. Try again."** (no connection or
  timeout) · **"Common items did not load: something went wrong. Try again."** (5xx, anything unexpected). After a
  failed **Try again**: **"Common items still did not load: …"** with the same cause.
- **Try again**: secondary button outside the status `<p>`. While it runs: `aria-disabled`, and "Loading…" after
  400 ms. Never `disabled`.
- No add form (item or group) is rendered until the data has loaded. A field that appears before the list could add
  an item to a list the manager cannot see. [Opinion, list-editor]
- **One query for the whole page:** `GET /api/admin/common-item-groups?includeArchived=true` (design.md §6.6),
  `queryKey: ['admin', 'common-item-groups']`, returning every group (Every note first, active and archived) with its
  active and archived items, each with `sortOrder` and `archivedAtUtc`, plus a `rowVersion` per group and per item for `If-Match`
  (design.md §6.6). Each group's list editor reads its own rows from this query (`select`), so one re-read refreshes
  every section. `networkMode: 'always'`, one silent retry for network failures or 5xx only, the `api()` wrapper's own
  10 s timeout (the query passes TanStack's `signal`, never `AbortSignal.timeout`, whose abort the wrapper would
  rethrow as it came, not as a timeout; empty-loading-error.md *Timing*), `refetchOnWindowFocus: false`. Gate on
  `isLoadingError`, never `isError`: a failed background re-read leaves the page on screen.

### 4. List-conflict message slot
- Rendered only after a conflict (see Interactions). It is a `<p tabIndex={-1}>` above the first group section, with
  body text and a 4 px `--colour-error` left bar. It takes focus.
- **"Someone else changed this list just now. Check it, then try again."** (P, list-editor). One message for item and
  group conflicts.
- It stays until the next successful change on this screen.

### 5. Group sections: [group-picker](../components/group-picker.md) Manager side
- One `<section aria-labelledby="group-{id}-heading">` per **active** group, in configured order, **Every note
  first**. Each has an `<h2 id="group-{id}-heading">` with the group's name as stored, `overflow-wrap: anywhere`,
  never truncated. A manager benefits from a region per group here, and there are few of them [Opinion,
  group-picker.md]. Sections are `--section-gap` apart.
- **Every note:** heading **Every note** **(V)**, then `<p>` **"Always shown first, on every note."** **(V)** in
  `--colour-text-secondary`. **No group buttons at all**, not even unavailable ones: it can never be renamed, moved or
  archived (A43), so there is nothing to explain by pressing, and its line says why.
- **Every other group, heading row** (`.groupHead`): the `<h2>`, then `.side` holding the group alert slot (`<div
  role="alert">`, always rendered) and the four group buttons in two pairs: **Move up**, **Move down** (pair 1),
  **Rename**, **Archive** (pair 2) **(V labels; order: Conflicts resolved, 15)**. Secondary `<button type="button">`
  through the shared `ActionButton`, label width, with hidden context after each visible label:
  `Rename<span class="visually-hidden">, Community outing group</span>`. Never `aria-label` (SC 2.5.3).
- **Unavailable group moves:** Move up on the first group after Every note and Move down on the last group (both,
  when there is one group besides Every note) use `aria-disabled="true"`, stay in place and focusable, with the
  dashed border. Pressing one announces "Already at the top" / "Already at the bottom". Nothing ever moves above
  Every note.
- **Rename form** (replaces the group buttons while open; the `<h2>` stays above it, so the region keeps its name and
  the manager sees what is being renamed): `<form noValidate>` with label **Group name** **(V)**, `<input type="text"
  id="group-{id}-name">` with the current name and the caret at the end, `autoComplete="off"`, no `maxLength`, the
  character count (200, A6), the form alert slot, then **Save** (primary, `type="submit"`) and **Cancel**
  (secondary). The items below stay as they are. group-picker.md has the whole heading row become the form; keeping
  the heading is simpler and needs no change of the region's name [Opinion].
- **Items:** the existing list editor (`kind: 'item'`, scoped to this group), unchanged: numbered `<ol>` that
  restarts at 1 in each group (so "number 2 of 5" stays true), row buttons **Move up, Move down, Edit, Archive**, the
  row alert slot, busy labels and hidden context `, [item text]` (list-editor; Conflicts resolved, 4 and 14). Items
  move up and down **within their group only**; Move up on a group's first row and Move down on its last row are
  unavailable. Moving to another group is done through Edit (component 6).
- **Empty group** (no active items): instead of the `<ol>`, a plain `<p>` **"No items in this group. It does not show
  on notes until it has one."** **(V)** in `--colour-text`, not a live region, shown only after the data arrives.
  The same line serves Every note (an empty Every note is not shown on notes either, A43).
- **Add item form** (list-editor `AddForm`, form-validation one-field pattern): label **New item in [group name]**
  **(V)** (for example "New item in Every note"), field id `item-new-{groupId}`, `<textarea rows={3}>`, no
  `maxLength`, no `required`, font size at least 16 px (iOS zoom). Character count as before: hidden hint **"You can
  enter up to 200 characters"**, visible count `aria-hidden`, polite count region after a 1 s pause; over the limit is
  bold error colour but **not** an error until Add item is pressed (GOV.UK character count). `aria-describedby`: the
  error id (only after a failed attempt), the count hint id, then `items-help` (Conflicts resolved, 12). Form alert
  slot above the button. **Add item** **(V)**: primary, `type="submit"`, in every section (Conflicts resolved, 17).
  Busy label **Adding…** (P). The new item goes to the end of **this** group.
- **Archived items** (per group): rendered only when this group has archived items. Native, **uncontrolled**
  `<details>`, closed on load, `<summary>` plain text **"Archived items"** (P, from design "the group's archived
  items, collapsed") plus visually hidden **" in Community outing"**, so the several summaries on the page have
  distinct names. Bold, body size, at least 44 px tall, no heading inside. `<ul>` most recently archived first; each
  `<li>`: the text, the row alert slot, then **Restore** **(V)** with hidden `, [item text]`. No Edit, no moves.
- Mutation scope `{ id: 'common-items' }` for **every** write on this screen, items and groups alike, so a group move
  and an item add never interleave. All writes run one at a time, and each one sends, then re-reads the one query.

### 6. Item edit form: list-editor edit mode, plus the Group radios
- Inside the `<li>`: `<form noValidate>` with label **Edit item** (P), `<textarea rows={3}>` with the current text
  and the caret at the end, the character count, then the **Group** radios, the form alert slot, then **Save** (P;
  primary, `type="submit"`) and **Cancel** (secondary). Field id `item-{id}-text`. Several rows can be in edit mode at
  once.
- **Group radios** **(V: "Group" set of radio buttons, design.md 4.9)**: `<fieldset>` with `<legend>Group</legend>`,
  one native `<input type="radio" name="item-{id}-group">` per **active** group in configured order (Every note
  first), each in the shared ChoiceRow (56 px row, 40 px circle drawn with `appearance: none`, the checkbox-list
  technique), the group name as the label. The item's current group is selected: that is its current value, not a
  guessed pre-selection. **Rendered only when there are at least two active groups** (Conflicts resolved, 18).
  Radios, not a `<select>`: GOV.UK, "some users find selects very difficult to use" [Convention].
- **Save:** validate the text as before. If neither the text nor the group changed, close as Cancel and send nothing.
  Otherwise one `PUT` with `{text, groupId}` and `If-Match`. A moved item goes to the **end** of its new group (A42).

### 7. New group form: form-validation one-field pattern
- After the last group section, `--section-gap` below it. `<form noValidate>`, label **New group** **(V)**, `<input
  type="text" id="group-new-name">`, `autoComplete="off"`, no `maxLength`, no `required`, 48 px tall, capped at
  `--measure`. Character count (200, A6) as for items.
- Form alert slot above the button. **Add group** **(V)**: **secondary**, label width (Conflicts resolved, 17). Busy
  label **Adding…** (P).
- The new group is added at the end, empty (design.md 4.9).

### 8. Archived groups
- Rendered only when at least one group is archived, `--section-gap` below the New group form.
- Native, **uncontrolled** `<details>`, closed on load. `<summary>` plain text **"Archived groups"** (P, from design
  "Archived groups, collapsed"). Same style as the item summaries.
- `<ul>` of archived groups, most recently archived first. Each `<li>`: the group name, the row alert slot, then
  **Restore** **(V)** with hidden `, [group name] group`. No items are listed and nothing else can be done to an
  archived group: Restore brings it back with its active items (A2).

### 9. Page status region: [app-shell](app-shell.md) component 3, [primary-actions](../components/primary-actions.md)
- This page's own visually hidden `<p role="status">` (`PageStatus`), rendered empty with the page from its first
  render and never conditionally mounted. The shared Button writes its busy labels into it, and the page writes the
  results that do not move focus (see the announcements table). A repeated identical message (for example "Already
  at the top" twice) must still be spoken, so the shared `announce()` clears the region before writing. There is no
  app-level region (app-shell.md). [Opinion; identical live-region text is often not re-announced, empty-loading-error]

---

## States

| State | When | What the manager sees (exact copy) | Notes |
|---|---|---|---|
| **Waiting** | Request in flight, under 1 s | `<h1>` Common items, help text. Nothing else. | No spinner and no skeleton (NN/g: no indicator under 1 s) |
| **Loading** | Still in flight after 1 s | "Loading common items…" | Polite status. No forms yet |
| **Load failed** | First load failed after one silent retry | "Common items did not load: no connection. Try again." or "Common items did not load: something went wrong. Try again." + **Try again** | No red, no icon, no code |
| **Still failing** | Try again failed | "Common items still did not load: …" (same cause) + **Try again** | Changed words, so it is announced again |
| **Ready** | Data loaded | Every note, then each active group with its buttons, items and add form, then New group, then **Archived groups** if any | Default |
| **Only Every note** (no other active group) | Normal before set-up | Every note section, then **New group** | No group buttons anywhere |
| **Every note with no items, nothing else** | First visit | Every note with "No items in this group. It does not show on notes until it has one." **(V)**, its add form, then New group | Normal before set-up; no warning styling |
| **A group with no items** | Newly added, or every item archived or moved | Its heading and buttons, "No items in this group. It does not show on notes until it has one." **(V)**, its add form, and its **Archived items** if any | Not offered on notes until it has an item (A45) |
| **One group besides Every note** | — | Both of its Move up and Move down unavailable | Pressing either explains why |
| **One item in a group** | — | Both of that item's Move up and Move down unavailable | As before |
| **Archived items / groups absent** | Nothing archived in that group / no archived group | Not rendered at all | — |
| **Archived section closed / open** | Something archived | "▸ Archived items" / "▾ Archived items" (per group), "▸ Archived groups" / "▾ Archived groups" with Restore rows | Native toggle. It opens by itself after the matching Archive |
| **Row or group busy** | A write on that row or group is running or queued | That button shows its busy label after 400 ms: Moving… · Saving… · Archiving… · Restoring… · Adding… | Other buttons stay usable; their writes queue |
| **Unavailable pressed** | An end Move up / Move down (item or group) | Nothing visible changes | Announced: "Already at the top" / "Already at the bottom" |
| **Editing an item** | Edit pressed | Edit item field with the text, count, **Group** radios (two or more active groups), Save, Cancel | The row keeps its number |
| **Renaming a group** | Rename pressed | Group name field with the name, count, Save, Cancel, under the heading in place of the group buttons | Items stay below |
| **Over the limit while typing** | More than 200 characters typed in any field | Count: "You have 3 characters too many" in bold error colour | Not an error yet; no `aria-invalid` |
| **Field error: empty** | Add item or item Save with blank text; Add group or rename Save with a blank name | "Enter the common item" (P) · "Enter the group name" **(V)** above the field | Focus moves to the field |
| **Field error: too long** | Over 200 characters on press | "Common item must be 200 characters or less" (P) · "Group name must be 200 characters or less" **(V)** | Same |
| **Request failed** | Network failure or 5xx on a write | In that row's, group's or form's alert slot: "Not moved: no connection. Try again." · "Not added: …" · "Not saved: …" · "Not archived: …" · "Not restored: …" with cause "no connection" or "something went wrong" (P) | Text kept; focus stays on the button |
| **Edit conflict** | `412` on an item Save, the item still active | In the edit form's alert slot: "Someone else changed this item while you were editing. It now says: ‘[their text]’. Save to replace it with your wording, or Cancel to keep it." (P) | The typed text and the chosen group are kept |
| **List conflict** | `412` on any group write or on an item Archive or Restore, `422 config.order_mismatch` on any move, `412` on an item Save when the item was archived meanwhile, or `409 config.group_archived` on Add item or an item Save (the item's group or the chosen group was archived meanwhile) | "Someone else changed this list just now. Check it, then try again." above the groups (P) | The page re-reads first; the message takes focus. An open rename form keeps the typed name |
| **Signed out** | Any `401` | session-timeout.md's sign-in-in-place state | Typed text stays in memory only |
| **Not found** | Worker, or `403`/`404` on the list request | Whole-page "Page not found" (empty-loading-error) | — |
| **Background re-read failed** | A refetch fails with the page on screen | Nothing changes, nothing is announced | `isRefetchError` counts as ready |
| **Read-only** | — | Does not exist on this screen. Workers cannot reach it, and there is no archived-list read-only mode. | — |

---

## Interactions and focus

### On arrival
The shell scrolls to the top and focuses the `<h1>` **Common items** (app-shell-nav). The data loads into the region
below. When it arrives, focus does not move.

### Item actions (inside a group)

| Action | While it runs | On success | Focus afterwards | Announced (polite, page status region) |
|---|---|---|---|---|
| **Move up / Move down** | The button is busy at once; "Moving…" after 400 ms. The list does not change yet. | `PUT /api/admin/common-item-groups/{groupId}/items/order` with `{commonItemIds}`: **that group's** full active list with this item swapped, built from the server's last order **when the write runs**. Then re-read; the group's rows re-render in the new order. | Stays on the **same button of the moved item**. If React moved that DOM node (Move down on row 1 is the case that breaks), focus is restored explicitly after render. | "Moved up to number 2 of 5" · "Moved down to number 4 of 5" (P), counted within the group |
| **Move up on a group's row 1 / Move down on its last row** | No request | Nothing changes | Stays | "Already at the top" · "Already at the bottom" (P) |
| **Edit** | — | The row becomes the edit form, filled with the current text and group | The textarea, caret at the end (focused inside the tap so phones open the keyboard) | — (the label, value and descriptions are read) |
| **Save** (edit), same group | Save busy, "Saving…" after 400 ms; textarea `readOnly` | Validate. Trim, and turn line breaks into spaces. If text and group are unchanged, close as Cancel does and send nothing. Otherwise `PUT /api/admin/common-items/{id}` with `{text, groupId}` and `If-Match`, then re-read. | That row's **Edit** | "Item saved" (P) |
| **Save** (edit), another group chosen | As above | The same `PUT`; the item leaves this group (its numbers close up) and appears at the **end** of the chosen group (A42) | That item's **Edit**, now in its new section (scrolled into view by the focus) | "Item saved and moved to the end of Personal care" (P) |
| **Cancel** (edit) | — | The row returns to normal; the typed text and any group choice are thrown away | That row's **Edit** | — |
| **Add item** | Add busy, "Adding…" after 400 ms | Validate. `POST /api/admin/common-item-groups/{groupId}/items` `{text}` (added at the end of that group), then re-read. The new row appears at the bottom of that group's list; the field is cleared. | Stays in that group's **New item in …** field, ready for the next one | "Item added to the end of the list" (P) |
| **Archive** (item) | Archive busy, "Archiving…" after 400 ms | `POST /api/admin/common-items/{id}/archive` with `If-Match`, then re-read. The row leaves the list and the numbers close up. That group's **Archived items** appears if it was absent, **opens**, and lists this item first. If it was the group's last active item, the empty-group line shows. | That item's **Restore** | "Item archived" (P) |
| **Restore** (item) | Restore busy, "Restoring…" after 400 ms | `POST /api/admin/common-items/{id}/restore` with `If-Match`, then re-read. The item goes to the **end** of its group (A2). If nothing archived is left in that group, its section is removed. | That item's **Archive**, now in the group's last row | "Item restored to the end of the list" (P) |

### Group actions (not on Every note)

| Action | While it runs | On success | Focus afterwards | Announced (polite, page status region) |
|---|---|---|---|---|
| **Rename** | — | The group buttons give way to the rename form, filled with the current name | The **Group name** input, caret at the end (inside the tap) | — |
| **Save** (rename) | Save busy, "Saving…" after 400 ms; input `readOnly` | Validate (trimmed). If unchanged, close as Cancel and send nothing. Otherwise `PUT /api/admin/common-item-groups/{id}` `{name}` with `If-Match`, then re-read. The heading shows the new name; new item labels follow it. | That group's **Rename** | "Group renamed" (P) |
| **Cancel** (rename) | — | The group buttons return; the typed name is thrown away | That group's **Rename** | — |
| **Move up / Move down** | Busy at once; "Moving…" after 400 ms | `PUT /api/admin/common-item-groups/order` with every active group ID except Every note, this group swapped, built from the server's last order when the write runs. Then re-read; the sections re-render in the new order. | The **same button of the moved group**, restored explicitly after render (whole sections move in the DOM) | "Group moved up" · "Group moved down" (P) |
| **Move up on the first group after Every note / Move down on the last group** | No request | Nothing changes | Stays | "Already at the top" · "Already at the bottom" (P, existing) |
| **Archive** (group) | Busy at once; "Archiving…" after 400 ms | No dialog (reversible, as for items). `POST …/common-item-groups/{id}/archive` with `If-Match`, then re-read. The section leaves the page with its items; they stop showing on notes started from now on. **Archived groups** appears if needed, **opens**, and lists it first. | Its **Restore** in Archived groups | "Group archived" (P) |
| **Restore** (group) | Busy at once; "Restoring…" after 400 ms | `POST …/common-item-groups/{id}/restore` with `If-Match`, then re-read. The section returns at the **end** of the groups with its active items (A2). If no archived group is left, Archived groups is removed. | That group's **Archive**, in the restored section | "Group restored to the end of the list" (P) |
| **Add group** | Busy at once; "Adding…" after 400 ms | Validate (trimmed). `POST /api/admin/common-item-groups` `{name}`, then re-read. The new, empty section appears last, just above New group; the field is cleared. | The new group's **New item in [name]** field: the next useful step, because an empty group does not show on notes | "Group added at the end of the list" (P) |
| **Open / close an archived section** | — | Native `<details>` toggle | Stays on the summary | Native "expanded" / "collapsed" |
| **Try again** (load failed) | `aria-disabled`; "Loading…" after 400 ms; the old error text clears | The page renders | The `<h1>` (the button has gone) | — |

All paths are design.md §6.6's. The server refuses renaming, moving or archiving Every note (`422
config.every_note_fixed`); the screen never offers those actions, so that response is handled as "something went
wrong".

**Validation** (Add item, item Save, Add group, rename Save; form-validation's one-field pattern). It runs only on
press, on the trimmed text; whitespace only counts as empty. On failure, the message appears **above** the field with
a hidden "Error: " prefix. The field gets `aria-invalid="true"` and the group gets the 4 px bar. **Focus moves to the
field**. There is no error summary and no "Error: " title prefix, because both belong to multi-field forms. Once in
error, the field is re-checked on every input, and the error clears silently the moment the text is fixed. A server
`422` with `errors.text` or `errors.name` is shown the same way. Typed text is never cleared (SC 3.3.7). Nothing
checks for two groups with the same name; the design has no such rule.

**Queueing.** A press on another row or group while a write runs is accepted. Its own button shows busy, and it runs
when its turn comes, working from the data the server last returned. A queued move whose item or group is already at
the end sends nothing.

**Focus is restored only if the manager has not moved on.** When a queued write finishes, focus moves only if it is
on `<body>` or still on the button that started that write. It never pulls focus away from where the manager has
since gone. [Opinion; SC 3.2.1/3.2.2 spirit: no unexpected focus change]

### Failures

| Response | What happens | Focus |
|---|---|---|
| Network failure or timeout on any write | The button returns to normal. The row's, group's or form's alert slot shows "Not [moved / added / saved / archived / restored]: no connection. Try again." | Stays on the button |
| `5xx` on any write | Same slot: "Not [done]: something went wrong. Try again." | Stays on the button |
| `412` on an item **Save**, item still active | Re-read. The edit form stays open with the typed text and chosen group and shows the edit-conflict message with the other person's wording. The next Save carries the new version. | Stays on **Save** |
| `412` on an item Save when the item was archived meanwhile; `409 config.group_archived` on Add item or an item Save (its group or the chosen group was archived meanwhile, design §6.6); `412` on any group write or item Archive or Restore; `422 config.order_mismatch` on any move | Re-read, then show the list-conflict message above the groups. An open rename form stays open with the typed name and the new version. | The list-conflict message |
| `422 validation.failed` on Add, Save or rename | Shown as the field error, as above | The field |
| `428 precondition.required`, `422 config.every_note_fixed` | A client bug (a missing `If-Match`, or an action on Every note the screen never offers). Show the "something went wrong" line. | Stays on the button |
| `401` | The sign-in-in-place flow (session-timeout.md). Typed text stays in memory. After signing in, the query is invalidated and re-read. | Owned there |

**No automatic retry for any write** (`retry: false`). The add endpoints have no idempotency key in design §6.6, so a
silent retry after a lost response could add an item or group twice. A retried archive would get a false `412`
caused by our own first attempt. (Conflicts resolved, 8.) Mutations use `networkMode: 'always'`, so an offline write
fails at once with "no connection" instead of sitting paused with a busy label.

### Focus order (Tab)

Skip link → shell (Account, Today, Flagged, Report, Manage) → `<main>`. Then, for each group section in order: the
group buttons **Move up → Move down → Rename → Archive** (none on Every note; in rename mode: **Group name** field →
**Save** → **Cancel**); then, for each item row: **Move up → Move down → Edit → Archive** (in edit mode: **Edit item**
field → each **Group** radio as one Tab stop, arrows between them → **Save** → **Cancel**); then that group's **New
item in …** field → **Add item** → its **Archived items** summary → each **Restore** (when open). After the last
group: the **New group** field → **Add group** → the **Archived groups** summary → each **Restore** (when open). The
`<h1>`, the list-conflict message and the status lines are focusable only by script (`tabIndex={-1}`) or not at all.
There are no shortcuts (SC 2.1.4). Enter in a textarea makes a new line and is never intercepted, so IME and
dictation work; Enter in a group name input submits its own form (native one-field form behaviour).

### What a screen-reader user hears

Expected wording, not verified. It varies by screen reader.

| Moment | Heard |
|---|---|
| Screen opens | "Common items, heading level 1" |
| Slow load | "Loading common items…" after 1 s |
| Heading navigation | "Every note, heading level 2" … "Community outing, heading level 2" (each also a region named by its heading) |
| Tab to a group's Move up | "Move up, Community outing group, button" |
| Tab to row 2 Move up in a group | "Move up, Meal prepared, button" |
| Press it | Focus stays. "Moved up to number 1 of 3" |
| Tab to an end Move up | "Move up, …, button, dimmed / unavailable"; pressing: "Already at the top" |
| Rename | "Group name, edit text, Community outing, You can enter up to 200 characters" |
| Save rename | Focus: "Rename, Outings, group, button". Then "Group renamed" |
| Edit item | "Edit item, edit text, multi-line, Meal prepared, You can enter up to 200 characters, Items in Every note show on every note…" |
| Tab to the Group radios | "Group, grouping, Every note, radio button, checked, 1 of 3" |
| Save with another group | Focus: "Edit, Meal prepared, button" (in its new section). Then "Item saved and moved to the end of Personal care" |
| Add item with nothing typed | Focus: "New item in Community outing, edit text, invalid entry, Error: Enter the common item, You can enter up to 200 characters, Items in Every note…" |
| Add item succeeds | Focus stays in the empty field. "Item added to the end of the list" |
| Add group succeeds | Focus: "New item in Respite, edit text, …". Then "Group added at the end of the list" |
| Archive a group | Focus: "Restore, Community outing group, button". "Group archived" |
| Restore a group | Focus: "Archive, Community outing group, button". "Group restored to the end of the list" |
| Network failure | Assertive (alert slot): "Not archived: no connection. Try again." |
| List conflict | Focus: "Someone else changed this list just now. Check it, then try again." |

---

## Accessibility checklist

**Headings and landmarks**
- [ ] One `<h1>` "Common items", focused on arrival. One `<h2>` per active group, Every note first, so heading
      navigation reaches every group (71.6% of screen-reader users navigate long pages by headings first; WebAIM #10).
      The archived sections are `<summary>`s, not headings (headings inside `<summary>` are not reliably exposed;
      O'Hara 2022).
- [ ] Each active group is a `<section aria-labelledby>` on its `<h2>` (which stays while renaming), so it is a named
      region. Landmarks otherwise come from the shell: banner, navigation, `<main
      id="main-content">`. Forms have no accessible name, so they do not create extra landmarks.

**Labels and names**
- [ ] **New item in [group name]**, **Edit item**, **Group name** and **New group** are visible `<label for>`s. Their
      limit is stated before typing (character count hint). Item fields: `aria-describedby` = error (when present),
      count hint, help text. Group name fields: error, count hint.
- [ ] The Group radios are a `<fieldset>` with `<legend>Group</legend>`; each radio's name is the visible group name.
- [ ] Every row button's accessible name starts with its visible word, followed by visually hidden ", [item text]";
      every group button's by ", [group name] group"; each archived-items summary by " in [group name]". No
      `aria-label` anywhere on this screen (SC 2.5.3).
- [ ] Arrows are `aria-hidden` SVG. No button is icon-only.

**Keyboard**
- [ ] Every action works with Tab plus Enter or Space (arrows inside the radio set). Nothing needs dragging (SC
      2.5.7) or a shortcut (SC 2.1.4).
- [ ] Focus is never dropped on `<body>` after any move (item or group), Edit, Rename, Save, Cancel, Add, Archive,
      Restore or Try again (SC 2.4.3).
- [ ] Unavailable and busy buttons use `aria-disabled`, never `disabled`, so they stay focusable.
- [ ] The focus ring (3 px near-black, 2 px offset, `:focus-visible`, `Highlight` in forced colours) is on every
      stop, including summaries, radios and the script-focused conflict message (SC 2.4.7, 1.4.11). Nothing is
      sticky, so the focused item is never covered (SC 2.4.11).

**Screen reader**
- [ ] Live regions exist before text is written into them: the page status region, the load `<p role="status">`,
      every row, group and form `role="alert"` slot, and each count's polite region. None is conditionally mounted
      or toggled from `display: none`.
- [ ] Routine results are polite (`role="status"`). Only request failures use the always-rendered `role="alert"`
      slots (primary-actions). Inline field errors are not live regions; focus does the announcing.
- [ ] Each group's active items are an `<ol>` with native numbering that restarts per group, so "number 2 of 5"
      matches what is heard ("list, 5 items").

**Visual**
- [ ] Text at 7:1 or better (help text and the Every note line 9.0:1, unavailable labels 8.1:1). Button borders,
      radio circles and focus at least 3:1.
- [ ] Unavailable is shown with a dashed border as well as grey. Errors are shown with words, a bar and a border.
      The selected radio is a drawn dot, not colour alone (SC 1.4.1).
- [ ] Targets: buttons and the group name input 48 px tall (`--target-button`), at least 44 px wide; radio rows 56
      px; summaries at least 44 px. At least 8 px between separate targets (A32; SC 2.5.8, also 2.5.5).
- [ ] Works at 320 px, 200% text and 400% zoom with no sideways scroll; long words and long group names wrap (SC
      1.4.4, 1.4.10, 1.4.12).
- [ ] No motion: the archived sections open instantly; no spinners (SC 2.2.2, 2.3.3).

**WCAG 2.2 AA criteria this screen must meet:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.4.1
Use of Color · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast · 1.4.12 Text
Spacing · 2.1.1 Keyboard · 2.1.4 Character Key Shortcuts (none used) · 2.2.2 Pause, Stop, Hide · 2.4.1 Bypass Blocks
(headings and regions per group) · 2.4.2 Page Titled · 2.4.3 Focus Order · 2.4.6 Headings and Labels · 2.4.7 Focus
Visible · 2.4.11 Focus Not Obscured (Minimum) · 2.5.3 Label in Name · 2.5.7 Dragging Movements · 2.5.8 Target Size
(Minimum) · 3.1.1 Language of Page (`en-AU`) · 3.2.4 Consistent Identification · 3.3.1 Error Identification · 3.3.2
Labels or Instructions · 3.3.3 Error Suggestion · 3.3.7 Redundant Entry · 4.1.2 Name, Role, Value · 4.1.3 Status
Messages.

---

## Acceptance criteria

**Content and copy**
- [ ] The page shows, in order: `<h1>` "Common items", the help text "Items in Every note show on every note. For
      other groups, the writer ticks the ones that happened. Changes apply to notes started from now on.", the Every
      note section, each active group's section in configured order, the **New group** field with **Add group**,
      then **Archived groups** (only when a group is archived).
- [ ] Every note is always first, shows "Always shown first, on every note." and has no Rename, Move up, Move down or
      Archive buttons in the DOM.
- [ ] Each other group shows its name as an `<h2>` and Move up, Move down, Rename, Archive in that DOM and visual
      order; each item row shows its number (restarting at 1 per group), its full text and Move up, Move down, Edit,
      Archive. Archived rows show only their text (or group name) and Restore.
- [ ] A group with no active items shows "No items in this group. It does not show on notes until it has one."
      only after the data arrives. The old page-level "No common items yet." never appears.
- [ ] The page title is "Grow2Notes – Common items". No participant or staff name appears in the title or URL.
- [ ] No string on the screen contains "delete", "please", "sorry", "oops", "invalid", "click", a negative
      contraction, or US spelling (microcopy `copy.test.ts`).

**Layout**
- [ ] At 375 px, each item row's and each group's Move up and Move down share one line and the other pair shares the
      next. Add item is full width; Add group is label width. Nothing scrolls sideways at 320 px.
- [ ] On a laptop at the 60rem page width and default text size, each item row's text and its four buttons sit on
      one line, and each group heading and its four buttons sit on one line. At 200% text the same rows stack, with
      no overlap or clipping.
- [ ] Help text, textareas and text inputs are no wider than 40rem.

**Loading, empty, errors**
- [ ] With a stubbed 2 s response: nothing extra at 999 ms, "Loading common items…" at 1,000 ms, and no add form
      (item or group) until the data arrives.
- [ ] Offline on first load: "Common items did not load: no connection. Try again." appears within about 2 s, with
      **Try again**. A second failure shows "Common items still did not load: no connection. Try again.". Success
      moves focus to the `<h1>`.
- [ ] A stubbed `503` shows "Common items did not load: something went wrong. Try again."
- [ ] A failed background re-read leaves the page on screen unchanged.
- [ ] A worker opening `/manage/common-items` sees "Page not found".

**Reordering**
- [ ] Move up on row 2 of a group sends one order request containing that group's active item IDs with rows 1 and 2
      swapped, and nothing about other groups. After the re-read, focus is on the moved item's Move up.
- [ ] Move down on row 1 (item or group): focus ends on the moved item's or group's Move down, never on `<body>`
      (Vitest + Testing Library on `document.activeElement`).
- [ ] End buttons (Move up on a group's first row or on the first group after Every note; Move down on a last row or
      the last group) are `aria-disabled="true"`, focusable, dashed, send no request, and announce "Already at the
      top" / "Already at the bottom". No request can move a group above Every note.
- [ ] Moving a group sends every active group ID except Every note, swapped, and re-renders the sections in the new
      order with focus on the same button of the moved group.
- [ ] Two quick presses on different rows or groups send two requests one after the other (never in parallel), each
      built from the order the server last returned.
- [ ] The screen never shows a new order before the server confirms it.

**Add, edit, rename, move**
- [ ] Add item in a group with an empty or spaces-only field shows "Enter the common item" above the field, sets
      `aria-invalid`, moves focus to the field and sends nothing. Over 200 characters shows "Common item must be 200
      characters or less". Both clear as soon as the text is fixed.
- [ ] A successful Add item appends the item at the end of **that** group, clears the field, keeps focus in it, and
      announces "Item added to the end of the list". Five items can be added in a row without leaving the field.
- [ ] Edit opens the row form with the current text (caret at the end, keyboard open on iOS Safari and Android
      Chrome) and, when there are two or more active groups, the Group radios with the item's group selected. Save
      with text and group unchanged sends nothing. Save and Cancel both return focus to that row's Edit.
- [ ] Choosing another group and pressing Save sends one `PUT` with `{text, groupId}` and `If-Match`; after the
      re-read the item is the last row of the new group, focus is on its Edit there, and "Item saved and moved to the
      end of Personal care" is announced.
- [ ] Rename shows the Group name field with the current name; an empty name gives "Enter the group name", over 200
      gives "Group name must be 200 characters or less"; Save with a changed name sends one `PUT` with `If-Match`,
      returns focus to that group's Rename and announces "Group renamed". The New item label follows the new name.
- [ ] Add group with a valid name adds an empty section at the end, clears the field, moves focus to the new group's
      New item field and announces "Group added at the end of the list".
- [ ] Line breaks typed or pasted into an item field are saved as single spaces, and the server applies the same
      rule. Typing 210 characters in any field is allowed and nothing is cut off (no `maxLength`).

**Archive and restore**
- [ ] Archiving an item removes the row, opens that group's Archived items (creating it if needed), lists the item
      first, focuses its Restore, and announces "Item archived". Restoring puts it at the end of its group.
- [ ] Archiving a group removes its section, opens Archived groups (creating it if needed), lists it first, focuses
      its Restore and announces "Group archived". No confirmation dialog appears. Restoring returns the section at the
      end of the groups with its active items, focuses its Archive and announces "Group restored to the end of the
      list".
- [ ] Every note can never be renamed, moved or archived: no button exists, and the API refuses it (`422
      config.every_note_fixed`).
- [ ] Each archived `<details>` keeps any open or closed state the manager sets between actions (uncontrolled).

**Failures and conflicts**
- [ ] With the network cut, each write shows its own "Not [done]: no connection. Try again." in that row, group or
      form, keeps any typed text, and keeps focus on the button. No write is retried automatically.
- [ ] A `412` on an item Save keeps the typed text and chosen group and shows "Someone else changed this item while
      you were editing. It now says: ‘…’. …". The next Save succeeds with the fresh version.
- [ ] A `412` on any group write or on an item Archive or Restore, or a `422 config.order_mismatch`, re-reads the
      page and focuses "Someone else changed this list just now. Check it, then try again."
- [ ] A `401` during any write hands over to sign-in in place; after signing in, the page reloads its data.

**Accessibility runs**
- [ ] axe (Playwright) passes in these states: loading, load failed, only Every note, ready with three groups,
      editing an item (with radios), renaming a group, field error, archived items open, archived groups open, list
      conflict.
- [ ] Manual pass with NVDA + Chrome and VoiceOver on iOS Safari: names, "dimmed/unavailable", heading and region
      navigation by group, every announcement in the table above, and focus after each action.
- [ ] Keyboard-only pass on a laptop, and a Windows forced-colours pass (dashed unavailable borders, focus ring,
      summary markers, radio dots, row dividers visible).

---

## Conflicts resolved

1. **Load-state wording.** list-editor: "Loading…" and "Common items couldn't be loaded. Check your connection, then
   try again." microcopy §9: "[Thing] did not load: [cause]. Try again." empty-loading-error: "Loading common
   items…", "Could not load common items." plus a cause sentence, and "Still could not…" on a repeat.
   **Chosen (editorial pass, app-wide): microcopy §9's words** ("Common items did not load: [cause]. Try again.", and
   "still did not load" on a repeat) with **empty-loading-error's mechanism**. One wording for the same failure on
   every screen; the "still" text keeps repeat failures audible; no negative contraction (GOV.UK).
2. **Request-failure wording.** list-editor uses "Not saved: no connection. Try again." for every action, and
   "…something went wrong in Grow2Notes…". microcopy's message pattern is "Not [done]: [cause]. Try again.", with
   the cause "no connection" or "something went wrong". **Chosen: microcopy, with a verb per action** (Not moved /
   added / saved / archived / restored). "Not saved" after a failed Move is not literally true, and literal words
   help tired readers and readers with English as a second language (COGA 4.4.4).
3. **Limit hint.** form-validation: hint "Up to 200 characters". list-editor, microcopy and conditional-reveal: the
   GOV.UK character-count strings, starting "You can enter up to 200 characters". **Chosen: the GOV.UK strings**,
   so the hint and the count are one element. They are the same strings the Reason field and Guide prompts use
   (SC 3.2.4). The group name fields use them too.
4. **Button order.** design.md §4.9 and foundations list "Edit, Move up, Move down, Archive". list-editor builds
   "Move up, Move down, Edit, Archive". **Chosen: list-editor's order, in two pairs.** The design lists what each row
   has, not the order. At 375 px with 18 px bold labels, Edit-first splits the Move pair across two lines (about
   72 + 142 + 163 px plus gaps against 343 px). Move-first keeps the pair on one line and Archive furthest from the
   text. [Opinion: arithmetic]
5. **Laptop row layout.** list-editor: text and actions side by side through `@container (min-width: 44rem)`.
   foundations: one breakpoint, `@media (min-width: 40rem)`, with a CI grep that flags any other `min-width`.
   **Chosen: neither number.** The row is a wrapping flex line (text `flex: 1 1 16rem`, actions at their natural
   width), so it changes layout with no breakpoint. With primary-actions' real button sizes, a 44rem container
   would leave the item text about 4rem wide, and a 40rem viewport switch would be worse. Wrapping honours the
   one-breakpoint rule and design §4.0's "setup screens use the extra space". Group heading rows use the same
   technique.
6. **Row button width.** primary-actions: buttons are full width on phones, and at least 8rem wide from 40em.
   **Chosen: row and group buttons (Move up, Move down, Edit, Rename, Archive, Restore, Save, Cancel) and Add group
   take their label width, with a 44 px minimum width and a 48 px height.** This is needed for the pairs on phones,
   and it gives the item text about 4rem more room on laptops. **Add item** and **Try again** keep the shared rule.
   [Opinion]
7. **Announcement punctuation.** list-editor ends its announcements with full stops ("Item archived."). microcopy:
   one-phrase status and success messages have no full stop. **Chosen: microcopy** ("Item archived", "Group
   archived", "Moved up to number 2 of 5"). Message sentences ("Not moved: no connection. Try again.") keep their
   full stops.
8. **Retrying writes.** form-validation: mutations retry network failures and 5xx up to 3 times. list-editor: list
   writes are never retried automatically. **Chosen: no automatic retry on this screen.** design §6.6 gives the add
   endpoints no idempotency key, so a retry after a lost response could add a duplicate item or group. A retried
   archive would hit `412` from its own first attempt.
9. **Archived section heading.** foundations names "Archived goals" as an example of a bold h3. list-editor (after
   O'Hara's testing): a plain-text `<summary>`, no heading inside. **Chosen: plain-text summary**, styled bold at
   body size, which keeps the look foundations wants without unreliable semantics. The same for "Archived groups".
10. **Help-text size.** The foundations type-scale table puts help text at the 16 px small size. Its per-screen note
    for 4.8/4.9 says body size, `--colour-text-secondary`. **Chosen: the per-screen note** (more specific), at
    18 px. It is the only warning that a change reaches notes.
11. **Button height.** primary-actions' CSS: `min-block-size: 2.75rem` (44 px). foundations: buttons at
    `--target-button`, `max(3rem, 48px)`. **Chosen: 48 px** (foundations, the canonical token set). It is still
    A32-compliant.
12. **`aria-describedby` order.** form-validation's `TextField`: hint, then error. conditional-reveal: error, then
    hint. **Chosen here: error, count hint, then help text** (item fields). The help text is three sentences, so an
    error placed after it would be heard about 25 words late. [Opinion]
13. **Token names.** list-editor uses `--focus-ring`, `--error` and `--border-subtle`. **Chosen:** the foundations
    names `--colour-focus`, `--colour-error` and `--colour-divider` (foundations alias table).
14. **Hidden context format.** list-editor: `Edit<span class="visually-hidden">, [text]</span>`. microcopy's example:
    `Edit<span …> goal: [text]</span>`. primary-actions: `Edit<span …> Alex Park</span>`. **Chosen: list-editor's
    comma form.** The comma gives a spoken pause between the action and the item. It matches the hidden-comma
    pattern already used in checkbox-list, chronological-list and status-tags. Group buttons add " group" after the
    name (group-picker.md), so "Archive, Personal care group" cannot be confused with an item called "Personal care".
15. **Group button order.** group-picker.md and design.md 4.9 listed "Rename, Move up, Move down, Archive".
    **Chosen: Move up, Move down, Rename, Archive**, the item rows' order with Rename in Edit's place. The same reason
    as 4 (the Move pair stays on one line at 375 px), and the same position for the same action on every row (SC
    3.2.4). group-picker.md and design.md 4.9 now list the same order. [Opinion]
16. **Help text and empty sentence.** design.md 4.9 before D44: help "Shown on every participant's note, in this
    order…" and empty "No common items yet.". group-picker.md and design.md 4.9 now: the new help text and a
    per-group empty line. **Chosen: the new strings (V).** The old help text is false since D44, and the page is
    never empty of groups (Every note is built in), so the page-level empty sentence is replaced by the per-group
    line.
17. **Primary actions.** The earlier spec had one primary, **Add item**. Now there is one **Add item** per group.
    **Chosen (group-picker.md): Add item stays primary in every section; Add group is secondary.** Add item is the
    same control as before in each section (SC 3.2.4; equal actions may share weight, primary-actions.md), and
    design.md 4.9 keeps "Primary action: Add item". Adding a group is rare and should not compete. [Opinion]
18. **Group radios with one active group.** design.md 4.9: the Edit form has a "Group" set of radio buttons listing
    every active group. **Chosen: render the radios only when there are at least two active groups.** With only
    Every note there is nowhere to move an item, and a one-option radio set is noise for screen-reader users.
    [Opinion]
19. **Regions on this screen.** The note form keeps Common items in a plain `<div>` (no region). group-picker.md:
    each group here is a `<section aria-labelledby>`. **Chosen: regions here**, as group-picker.md says: managers
    move between groups to edit them, and there are few groups. [Opinion]

---

## Tensions with decisions

None found with D1–D47 or design.md §4.9. The tensions group-picker.md records for D44, D46 and D47 concern the note
form and the files (note-form.md, README). The API point under Open questions (now closed) was a gap in the §6.6
contract, not a conflict with a decision.

---

## Open questions

1. **API (closed).** design.md §6.6 now lists the group endpoints, the grouped list with a `rowVersion` per group and
   per item for `If-Match`, item add and reorder per group, `{text, groupId}` on the item `PUT`,
   `config.every_note_fixed` and `409 config.group_archived`. Nothing further is needed from this screen.
2. **Proposed copy needs the owner's sign-off:** "Edit item", "Save", "Archived items" (with hidden " in [group
   name]"), "Archived groups", "Loading common items…", the load-failure lines, the item field errors, the busy
   labels, the announcements (including "Group renamed", "Group moved up", "Group moved down", "Group archived",
   "Group restored to the end of the list", "Group added at the end of the list" and "Item saved and moved to the end
   of [group name]"), the per-action "Not [done]: …" lines and both conflict messages.
3. **Assumed defaults:** confirm or override A41, A42, A43 and the 200-character group name (A6), as listed at the top.
   The group name limit is set in one place (design.md A6) so every spec uses the same number.
4. **Group button order and the radios rule** (Conflicts resolved 15 and 18) are this file's own choices. Confirm.
5. **Lost response on Add.** If the network drops after the server has added an item or group, the manager sees "Not
   added: no connection" while it exists, and may add it twice. It shows up on the next re-read and can be archived.
   Accept this (rare, reversible), or give the add endpoints an idempotency key as the note-version endpoint has?
   Recorded only; no change recommended here.
6. **Typed wording lost in one edge case.** If another manager archives an item (or its group) while it is being
   reworded, Save gets `412`, the item leaves the page and its edit form closes, so the typed text (at most 200
   characters) is lost. Accept this as rare and short?
7. **Device checks not yet done:** the iOS keyboard opening when Edit or Rename focuses its field inside the tap
   (`flushSync`), restoring focus after React moves a row or a whole section, the side-by-side switch point on a real
   laptop, and the exact screen-reader wording for regions and radios.
8. **Pilot check, not a change.** Do managers on a laptop press Enter expecting it to add the item? In a textarea it
   makes a line break, which is saved as a space. Watch for this in the M1 "five items without help" check.

---

## Sources

Component specs (all carry their own full source lists): [group-picker](../components/group-picker.md) ·
[list-editor](../components/list-editor.md) · [form-validation](../components/form-validation.md) ·
[empty-loading-error](../components/empty-loading-error.md) · [foundations](../components/foundations.md) ·
[microcopy](../components/microcopy.md) · [primary-actions](../components/primary-actions.md) ·
[conditional-reveal](../components/conditional-reveal.md) · [checkbox-list](../components/checkbox-list.md) ·
[app-shell-nav](../components/app-shell-nav.md) · [confirm-dialog](../components/confirm-dialog.md).

Cited directly above:
- [Standard] W3C, Understanding SC 2.5.7 Dragging Movements: https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html
- [Standard] W3C, Technique SCR27 (reorder the DOM, return focus): https://www.w3.org/WAI/WCAG22/Techniques/client-side-script/SCR27
- [Standard] W3C, Understanding SC 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- [Standard] W3C, Understanding SC 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- [Standard] W3C, Understanding SC 3.3.7 Redundant Entry: https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html
- [Standard] WAI-ARIA APG, Developing a Keyboard Interface (disabled controls, focus loss): https://www.w3.org/WAI/ARIA/apg/practices/keyboard-interface/
- [Standard/Convention] MDN, Mastering wrapping of flex items: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_flexible_box_layout/Mastering_wrapping_of_flex_items
- [Standard] MDN, ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Guides/Live_regions
- [Research] WebAIM Screen Reader User Survey #10 (2024, 1,539 responses): https://webaim.org/projects/screenreadersurvey10/
- [Research] NN/g, Progress indicators (no indicator under about 1 s): https://www.nngroup.com/articles/progress-indicators/
- [Research] NN/g, Error-message guidelines: https://www.nngroup.com/articles/error-message-guidelines/
- [Research] NN/g, Empty states in application design: https://www.nngroup.com/articles/empty-state-interface-design/
- [Research] A. Roselli, aria-label does not translate: https://adrianroselli.com/2019/11/aria-label-does-not-translate.html
- [Convention] S. O'Hara, The details and summary elements, again: https://www.scottohara.me/blog/2022/09/12/details-summary.html
- [Convention] GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- [Convention] GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- [Convention] GOV.UK Design System, Select (radios preferred): https://design-system.service.gov.uk/components/select/
- [Convention] GOV.UK style guide A to Z (negative contractions): https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- [Convention] AgDS, Accessible form validation (one-field forms focus the field): https://design-system.agriculture.gov.au/patterns/accessible-form-validation-and-recovery
- [Convention] W3C COGA, Making Content Usable (4.4.4 Use Literal Language): https://www.w3.org/TR/coga-usable/
- [Convention] TanStack Query 5, Mutations (scope): https://tanstack.com/query/v5/docs/framework/react/guides/mutations
- [Convention] TanStack Query 5, Network mode: https://tanstack.com/query/v5/docs/framework/react/guides/network-mode
