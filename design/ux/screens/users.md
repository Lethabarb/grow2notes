# Users

Screen spec for design.md **4.11 Users (managers)**. It puts together the researched component files in
`../components/` into one buildable screen. It adds no screen, field, setting, notification, status or data item: the
four pages below are the list, the invite form, the user detail and the edit form that 4.11 already describes.

Copy marks: **(V)** verbatim from design.md · **(S)** taken from a sibling component file · **(P)** proposed, needs the
owner's approval. Evidence marks: **[Research]**, **[Standard]**, **[Convention]**, **[Opinion]**. Example people:
**Sam Lee** is the signed-in manager; **Alex Park** is a worker; **Chris Ng** is deactivated.

---

## Purpose and who uses it

**Purpose (V, 4.11):** "control who can sign in and in which role" (D23, D24).

**Who:** managers only (§2). They use laptops and phones (brief). A worker who types a Users URL gets "Page not found"
(app-shell-nav.md role guard, matching the API's 404-not-403 stance, §9.2).

**What they come here to do**, roughly in order of frequency:

| Task | How often | Stakes |
|---|---|---|
| Invite a new worker (several in a row at go-live, A40) | Occasional | Low: a wrong role or email is fixed with Edit |
| Reset sign-in for someone with a lost phone, new phone or forgotten password (4.13 "Lost phone") | Occasional | High: removes their sign-in method and cannot be undone; the manager must check who is asking first (4.11, 8.6) |
| Resend an invite whose link expired ("Ask a manager to send a new one", 4.1) | Occasional | Low |
| Deactivate someone who has left | Rare | Medium: their sessions end at once; Reactivate undoes the access part |
| Change someone's name, email or role | Rare | Changing the email resets sign-in (V) |

**Rules this screen must show and respect (4.11, A26, A27):**
- One email address per account across the whole app (A27).
- The last active manager cannot be deactivated, made a worker or reset (A26). Because only an active manager can open
  this screen, the only active manager is **always the person viewing it**. Every last-manager message on this screen
  therefore talks to "you".
- Users are never deleted. There is no Delete anywhere.

**Routes and page titles** (IDs only in URLs, no names in titles, 4.0):

| Page | Route | Title |
|---|---|---|
| Users list | `/manage/users` | Grow2Notes – Users |
| Invite user | `/manage/users/new` | Grow2Notes – Invite user (in app-shell.md's page-name union) |
| User detail | `/manage/users/{userId}` | Grow2Notes – User |
| Edit user | `/manage/users/{userId}/edit` | Grow2Notes – Edit user (in app-shell.md's page-name union) |
| Deactivate / Reset sign-in confirmation | none (screen state, no history entry) | unchanged |

After a failed Send invite or Save, the title gets the prefix "Error: " (form-validation.md).

---

## Layout - phone  (ASCII wireframe, top to bottom, at ~375 px)

Content column = 375 px minus two 16 px gutters = 343 px. One column. Nothing is sticky or fixed.

### Users list (`/manage/users`)

```
+---------------------------------------+
| Grow2Notes                   Account  |  header + nav (app-shell-nav.md)
| Today  Flagged 3  Report  Manage      |  Manage: aria-current="true"
+---------------------------------------+
| Users                            <h1> |  takes focus on arrival (also after Send invite)
|                                       |
| [           Invite user             ] |  primary-styled LINK, full width, 48 px
|                                       |
| Loading users…                        |  <p role="status">, always in the DOM;
|                                       |  text appears only after 1 s
| ------------------------------------- |  <ul role="list">, 1 px dividers
| Alex Park                          >  |  link text = name only, bold
| alex.park@example.org                 |  detail <p id>, linked by aria-describedby;
| Invited · Worker                      |  status word first, then role
| ------------------------------------- |  row min-height 56 px; whole row is the target
| Jo Smith                           >  |
| jo.smith@example.org                  |
| Active · Worker                       |
| ------------------------------------- |
| Sam Lee                            >  |  the viewer's own row: no "(you)"
| sam.lee@example.org                   |
| Active · Manager                      |
| ------------------------------------- |
|                                       |
| Deactivated                      <h2> |  section only when someone is deactivated
| ------------------------------------- |
| Chris Ng                           >  |
| chris.ng@example.org                  |
| Deactivated · Worker                  |  plain text, never faded or greyed
| ------------------------------------- |
+---------------------------------------+
```

### Invite user (`/manage/users/new`)

```
+---------------------------------------+
| (header + nav as above)               |
+---------------------------------------+
| ‹ Users                               |  back link (shell's before-main bar slot)
+---------------------------------------+
| +-----------------------------------+ |  error summary: ONLY after a failed
| | There is a problem           <h2> | |  Send invite; takes focus
| | Enter an email address        <a> | |
| +-----------------------------------+ |
| Invite user                      <h1> |
|                                       |
| Name                                  |  <label for="displayName">
| Shown on the notes they write         |  hint
| [_________________________________]   |  text input, full width
|                                       |
| ▌Email address                        |  4 px error bar (only when in error)
| ▌Grow2Notes will email them a setup   |  hint
| ▌link. It works once and lasts 7 days.|
| ▌Error: Enter an email address        |  inline error ABOVE the input
| ▌[_________________________________]  |  type="email"
|                                       |
| Role                         <legend> |  <fieldset>
| (●) Worker                            |  whole row is the <label>, 56 px,
|     Writes notes and reads past notes |  40 px circle; hint by aria-describedby
| ( ) Manager                           |
|     Can also review flagged notes,    |
|     download reports, and set up      |
|     participants and users            |
|                                       |
|                                       |  <p role="alert">, always present, empty
| [           Send invite             ] |  primary, type="submit", 48 px
+---------------------------------------+
```

### User detail: Active worker (`/manage/users/{userId}`)

```
+---------------------------------------+
| ‹ Users                               |  back link (shell's before-main bar slot)
+---------------------------------------+
| Alex Park                        <h1> |  data heading (pending focus while loading)
| ------------------------------------- |  <dl> summary list, row dividers
| Email address                         |  <dt>
| alex.park@example.org                 |  <dd>, wraps anywhere
| ------------------------------------- |
| Role                                  |
| Worker                                |
| ------------------------------------- |
| Status                                |  this row takes focus after Deactivate,
| Active                                |  Reset sign-in or Reactivate (tabindex=-1)
| ------------------------------------- |
|                                       |  <p role="alert">, always present, empty
| [               Edit                ] |  secondary-styled LINK
|                                       |
| For a lost phone, a new phone or a    |  description, aria-describedby target
| forgotten password. Check who is      |
| asking first.                         |
| [           Reset sign-in           ] |  secondary button; opens ConfirmDialog
|                                       |
| ===================================== |  1 px rule, 24 px space above and below
| For someone who no longer works here. |
| Their notes stay.                     |
| [            Deactivate             ] |  secondary button; opens ConfirmDialog
+---------------------------------------+
```

### User detail: other statuses (only the actions area changes)

```
Invited                                   Deactivated
| [               Edit                ] | | [               Edit                ] |
|                                       | | ===================================== |
| Sends a new setup link. The old link  | | Lets them sign in again.              |
| stops working.                        | | [            Reactivate             ] |
| [           Resend invite           ] |
| Setup link sent to alex.park@example. |   <p role="status">, always present,
| org at 4:12 pm. The old link no       |   empty until a resend succeeds
| longer works.                         |
| ===================================== |
| For someone who no longer works here. |
| Their notes stay.                     |
| [            Deactivate             ] |
```

### User detail: your own page when you are the only active manager

```
| Sam Lee                          <h1> |
| Email address / sam.lee@example.org   |  (summary list as above)
| Role / Manager · Status / Active      |
|                                       |
| [               Edit                ] |
| ===================================== |
| +-----------------------------------+ |  notice: plain content, no role, no focus,
| |▌i You are the only active         | |  no dismiss (status-messages.md "notice")
| |   manager, so you cannot reset    | |
| |   your own sign-in, deactivate    | |  replaces Reset sign-in AND Deactivate;
| |   your account or make yourself a | |  nothing is disabled or greyed
| |   worker. To do any of these,     | |
| |   first make someone else a       | |
| |   manager.                        | |
| +-----------------------------------+ |
```

### Edit user (`/manage/users/{userId}/edit`)

```
| ‹ Alex Park                           |  back link to the detail page (bar slot)
| [ error summary, only after a failed Save; first in <main> ]
| Edit user                        <h1> |
| [ notice, ONLY on your own page when you are the only active manager:
|   "You are the only active manager, so you cannot change your email or
|    make yourself a worker. First make someone else a manager." ]
| Name                                  |
| Shown on the notes they write         |
| [Alex Park________________________]   |  prefilled
| Email address                         |
| Changing the email resets sign-in and |  hint (V)
| sends a setup link to the new address.|
| [alex.park@example.org____________]   |
| Role                                  |
| (●) Worker  + hint                    |  starts on the person's current role
| ( ) Manager + hint                    |
|                                       |  <p role="alert">
| [               Save                ] |  primary, type="submit"
```

### Confirmation dialogs (confirm-dialog.md owns the component)

```
+-------------------------------------+     +-------------------------------------+
| Deactivate Alex Park?          <h2> |     | Reset sign-in for Alex Park?   <h2> |
|   ^ focus lands here on open        |     |                                     |
| Alex Park will be signed out        |     | Only do this after you have         |
| everywhere now and can't sign in.   |     | checked, by phone or in person,     |
| Their notes stay.                   |     | that Alex Park is the one asking.   |
|                                     |     | Their current way of signing in     |
| (role="alert", empty)               |     | will be removed, and they will be   |
| [      Deactivate Alex Park       ] |     | signed out everywhere. A new setup  |
|   standard primary (blue)           |     | link will be emailed to             |
| [            Go back              ] |     | alex.park@example.org.              |
| (role="status": "Deactivating…"     |     | (role="alert", empty)               |
|  only after 1 s)                    |     | [   Reset sign-in for Alex Park   ] |
+-------------------------------------+     |   WARNING style (red)               |
  width: 343 px on a 375 px phone,          | [            Go back              ] |
  centred, scrolls inside at 200% text      +-------------------------------------+
```

---

## Layout - laptop  (what changes at wider widths)

One breakpoint only: `@media (min-width: 40rem)` (foundations.md). Below it, the phone layout above. From 40rem:

| Part | Change |
|---|---|
| Page | Gutters 32 px, section gaps 48 px, `<h1>` 32 px, `<h2>` 24 px (foundations.md). Content column capped at `--measure` (40rem), left-aligned inside the 60rem page. |
| List | **Same stacked three-line rows. Not a table.** The job is "find this person, then act", not "compare columns" [Research NN/g mobile tables https://www.nngroup.com/articles/mobile-tables/ ; Opinion Roselli https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html]. Rows get the hover tint under `@media (hover: hover)`. |
| Invite user (list) | Width of its label (`width: auto`, at least 8rem), left-aligned. |
| Text fields | Name and Email address inputs at least 30 characters wide (GOV.UK email pattern https://design-system.service.gov.uk/patterns/email-addresses/), up to the 40rem column. |
| Summary list | Key and value side by side: a 10rem key column and the value beside it. Same 40rem media query, **not** a container query (one-breakpoint rule; see Conflicts). |
| Action buttons | `width: auto`, left-aligned, still stacked one per line because each serious button has its own description above it. Same order as on the phone. |
| Send invite / Save | `width: auto`, left-aligned [Convention: primary-actions.md, magnifier users miss right-aligned buttons]. |
| Dialogs | 28rem wide, centred; buttons still stacked and full width inside the dialog (confirm-dialog.md). |
| Notice | Width of the content column, not the window. |

Nothing becomes sticky. At 200% text the layout falls back to the phone column (the breakpoint is in rem).

---

## Components, in order

Shared rules that apply to every part: body text 18 px, nothing under 16 px, two weights (400/700), tokens from
[foundations](../components/foundations.md); words from [microcopy](../components/microcopy.md); names and emails shown
exactly as stored, never truncated, wrapping with `overflow-wrap: anywhere`; no motion.

### A. Users list (`/manage/users`)

1. **App shell** — [app-shell-nav](../components/app-shell-nav.md), [app-shell](app-shell.md). Title "Grow2Notes –
   Users". Nav item Manage gets `aria-current="true"` (a child page of Manage). Route-change focus (app-shell.md's one
   rule): the `<h1>`; on a pop, the row the manager opened (`handle.returnFocus`, item 6).
2. **No success banner.** design.md gives a success message only for Submit, so arriving after Send invite focuses
   the `<h1>` like any arrival, and the new row ("Invited · Worker") is the result. A focused "Invite sent to {email}"
   banner was proposed; it is **not built unless the owner approves it** (Open questions).
3. **Page heading** — `<h1 tabindex="-1">Users</h1>` (V). Fixed text, rendered at once, so focus never waits for data.
4. **Invite user** — [primary-actions](../components/primary-actions.md) primary style on a React Router `<Link
   to="/manage/users/new">` (it changes the URL, so it is a link) (V). Min height 48 px.
5. **Region status line** — [empty-loading-error](../components/empty-loading-error.md). One `<p role="status">`,
   always rendered, empty while waiting; "Loading users…" after 1 s (S); "The user list did not load: [cause]. Try
   again." on failure (microcopy.md §9), with a secondary **Try again** button **outside** the status element.
6. **User rows** — [participant-list-rows](../components/participant-list-rows.md) row component, with
   [user-management](../components/user-management.md) content and [status-tags](../components/status-tags.md) words.
   - `<ul role="list">` (Safari drops list semantics under `list-style: none`
     [Convention] https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html).
   - Each `<li>` holds one `<Link>` whose text is the **name only** (`displayName`, weight 700), and one detail `<p
     id>` linked by `aria-describedby` [Convention: GOV.UK task list pattern, via participant-list-rows.md]. The
     link's `::after` stretches over the whole row: one action per row, nothing else inside is interactive.
   - Detail line 1: the email. Detail line 2: `{Status}<Sep/>{Role}` = "Invited · Worker", "Active · Manager",
     "Deactivated · Worker" (V words). Status first, so the odd ones out ("Invited", "Deactivated") are at the start of
     a line. `<Sep/>` is the middle dot with `aria-hidden="true"` plus a visually hidden ", " (microcopy.md).
   - Status is **plain text** in `--colour-text-secondary` (9.0:1). No tag, no fill, no colour coding, no fading,
     including Deactivated [Standard SC 1.4.1, 1.4.3; Convention status-tags.md].
   - Decorative chevron, `aria-hidden="true" focusable="false"`, at 3:1 or more.
   - Row `min-block-size: 3.5rem` (56 px), 1 px `--colour-divider` between rows. Focus: 3 px outline round the whole
     row on `.link::after`, offset −3 px.
   - Order: current users (Invited and Active together) sorted by `displayName` with `Intl.Collator('en-AU',
     { sensitivity: 'base' })`. If the API ever sorts, keep the server's order.
   - Each row link carries `data-return-key={userId}`; the route declares `handle.returnFocus` and
     `backKey: 'users'`, so the shell returns focus to the row on a pop (memory only, D22; app-shell.md).
7. **Deactivated section** — `<h2>Deactivated</h2>` (V word) then a second `<ul role="list">` of the same rows,
   same sort. Not rendered at all (heading included) when nobody is deactivated. It is a grouping, not a filter: there
   is no toggle, search, count or "last signed in" (none is in 4.11).

### B. Invite user (`/manage/users/new`)

1. **Back link** — the shared BackLink (app-shell.md component 3a): visible text "‹ Users" (P), `href`
   `/manage/users`, the "‹" chevron `aria-hidden`, so the name is "Users". In the shell's before-main bar slot, not
   in `<main>`. A plain click pops (`navigate(-1)`) when the previous entry is the list, so focus returns to the row;
   otherwise it pushes `/manage/users`.
2. **Error summary** — [form-validation](../components/form-validation.md). First in `<main>`, before the `<h1>`
   (GOV.UK placement; the back link is in the bar slot). Heading "There is a problem" (S). Only after a failed Send invite.
   `tabIndex={-1}`; takes focus on **every** failed attempt. Links use exactly the inline text; selecting one scrolls
   the label into view and focuses the field with `preventScroll`, without changing the URL.
3. **Page heading** — `<h1 tabindex="-1">Invite user</h1>` (V).
4. **Name** — text field (form-validation.md wiring; [text-and-date-inputs](../components/text-and-date-inputs.md)
   visuals). `id="displayName"` (equals the API key). Label "Name" (V word, microcopy.md). Hint "Shown on the notes
   they write" (P). `type="text" autoComplete="off" spellCheck={false} autoCapitalize="words"`. **No `maxLength`**
   (it silently cuts pasted text); limit 100 characters checked on submit (§5.3 `nvarchar(100)`).
5. **Email address** — `id="email"`. Label "Email address" (P, GOV.UK). Hint "Grow2Notes will email them a setup link.
   It works once and lasts 7 days." (P, from V "The email contains a setup link that works once and lasts 7 days.").
   `type="email" autoComplete="off" spellCheck={false} autoCapitalize="none" autoCorrect="off"`.
   - `autocomplete="off"` on both fields: SC 1.3.5 covers only information **about the user** filling the form; these
     describe another person, and `email`/`name` tokens would offer the manager's own details [Standard
     https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html].
   - No "confirm email" field [Convention GOV.UK: only if research shows it works]. A typo is fixed with Edit.
6. **Role** — native radios in `<fieldset id="role">` + `<legend>Role</legend>` (V) [Standard H71
   https://www.w3.org/WAI/WCAG22/Techniques/html/H71]. Two stacked options, **Worker** then **Manager** (V), each a
   whole-row `<label>` at least 56 px tall (foundations.md) with a 40 px circle drawn with `appearance: none` and a
   border-drawn dot so it survives forced colours (same technique and size as the tick box in
   [checkbox-list](../components/checkbox-list.md)). Item hints linked by `aria-describedby` on each radio:
   - Worker: "Writes notes and reads past notes" (P, from §2)
   - Manager: "Can also review flagged notes, download reports, and set up participants and users" (P, from §2)
   - **Worker is preselected** on Invite [Opinion, weighing Convention]: least privilege (a forgotten change gives
     less access, not more), most invites are workers (D2, D24), and the new row shows "Invited · Worker" at once.
     GOV.UK says don't preselect (https://design-system.service.gov.uk/components/radios/); AgDS allows a default
     where it makes sense (https://design-system.agriculture.gov.au/components/radio); NN/g recommends one
     (https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/).
   - Choosing a radio changes nothing until Send invite [Standard SC 3.2.2].
7. **Action error region** — `<p role="alert">`, always rendered, empty, directly above the button. Holds only
   network and server failures (field problems go to the summary).
8. **Send invite** — [primary-actions](../components/primary-actions.md) primary, `type="submit"` (V). Busy:
   `aria-disabled="true"` from the press (second taps ignored), label "Sending…" (S) after 400 ms in the same grid
   cell so the button never changes size; the page status region (`PageStatus`, app-shell.md component 3) announces
   "Sending…". Never the HTML `disabled` attribute. No Cancel button: the back link and browser Back are the way out.

### C. User detail (`/manage/users/{userId}`)

1. **Back link** — the shared BackLink "‹ Users" (P), to `/manage/users`, in the shell's bar slot; pops when the
   previous entry is the list.
2. **No success banner; focus goes to the changed state.** After Deactivate, Reset sign-in or Reactivate the pressed
   trigger has gone, so focus moves to the **Status row** of the summary list (its `<div>` gets `tabIndex={-1}`),
   heard as "Status, Deactivated" / "Status, Invited" / "Status, Active": the new state, in the place it is shown.
   After Save (arriving from Edit) the `<h1>` takes focus as on any arrival. design.md gives a success message only
   for Submit; the proposed banners are **not built unless the owner approves them** (Open questions).
3. **Page heading** — `<h1 tabindex="-1">{displayName}</h1>`. Data heading: not rendered while loading; the shell's
   in-memory "focus pending" flag lets it take focus when it mounts (empty-loading-error.md). Fallback `<h1>` "User"
   on a failed load. The name never goes into the title or URL.
4. **Summary list** — GOV.UK summary list pattern on `<dl>`: one `<div>` per row with `<dt>` and `<dd>`, row borders
   [Convention https://design-system.service.gov.uk/components/summary-list/: "Use a summary list to show information
   as a list of key facts"; "Borders help many users find and read information"]. Rows: **Email address**, **Role**,
   **Status** (keys P; values V words). No row actions inside the list (Edit covers all three fields). No invited,
   activated or deactivated dates: the API returns them but 4.11 does not list them.
5. **Action error region** — `<p role="alert">`, always rendered, empty, directly above group 1.
6. **Group 1 (routine)**, in this order, only the actions that apply now:
   - **Edit** (V) — secondary-styled `<Link to="edit">`. Every status.
   - **Resend invite** (V) — Invited only. Description above it (P): "Sends a new setup link. The old link stops
     working." linked by `aria-describedby`. Secondary `<button type="button">`. Busy "Sending…" (S). No dialog.
     Followed by an always-rendered `<p role="status">` for its result.
   - **Reset sign-in** (V) — Active only, and not when you are the only active manager. Description above it (P):
     "For a lost phone, a new phone or a forgotten password. Check who is asking first." Secondary trigger; opens the
     Reset sign-in dialog. The trigger is **not** red.
7. **Group 2 (serious)** — after a 1 px rule with 24 px space above and below [Research NN/g: "Avoid placing highly
   consequential actions directly next to options that are benign"
   https://www.nngroup.com/articles/proximity-consequential-options/]:
   - **Deactivate** (V) — Invited or Active, and not when you are the only active manager. Description (P): "For
     someone who no longer works here. Their notes stay." Secondary trigger; opens the Deactivate dialog.
   - **Reactivate** (V) — Deactivated only. Description (P): "Lets them sign in again." Secondary button. Busy
     "Reactivating…" (S). No dialog.
   - **Last-manager notice** — on your own page when you are the only active manager, in place of Reset sign-in and
     Deactivate. [status-messages](../components/status-messages.md) notice tone: plain content, no role, no focus, no
     dismiss. Copy in States. **Nothing is ever disabled or greyed out** [Convention GOV.UK: "Disabled buttons have
     poor contrast and can confuse some users, so avoid them if possible"
     https://design-system.service.gov.uk/components/button/ ; Opinion Smashing: explain why and how to re-enable
     https://www.smashingmagazine.com/2024/05/hidden-vs-disabled-ux/].
8. **ConfirmDialog** — [confirm-dialog](../components/confirm-dialog.md). Native `<dialog>` + `showModal()`,
   `role="alertdialog"`, `aria-modal="true"`, `aria-labelledby` = title `<h2>`, `aria-describedby` = body. Initial
   focus on the title (`tabindex="-1"`, set through a ref because React's `autoFocus` does nothing in `<dialog>`,
   react#23301). Exactly two stacked full-width buttons: confirm (verb + name) then **Go back**. No close X, no
   backdrop dismiss, no route or history entry. Used for exactly two actions here:
   - **Deactivate**: `tone="standard"` (Reactivate exists, so it is not "cannot be easily undone").
   - **Reset sign-in**: `tone="warning"` (cannot be undone: the person must set up again, 8.6).

   Which actions show, by status (4.11, 6.6, A26):

   | Status | Group 1 | Group 2 |
   |---|---|---|
   | Invited | Edit · Resend invite | Deactivate |
   | Active | Edit · Reset sign-in | Deactivate |
   | Deactivated | Edit | Reactivate |
   | Active, your own page, **only active manager** | Edit | Last-manager notice |

### D. Edit user (`/manage/users/{userId}/edit`)

1. **Back link** — the shared BackLink "‹ {displayName}" (P), to the detail page, in the shell's bar slot; pops when
   the previous entry is the detail page. (A name in link text is fine; it never enters the URL or title.)
2. **Error summary** — as Invite.
3. **Page heading** — `<h1 tabindex="-1">Edit user</h1>` (P). Fixed text, rendered at once; the fields render when
   the data is ready.
4. **Last-manager notice** — only on your own page when you are the only active manager, directly under the `<h1>`.
5. **Name, Email address, Role** — exactly as Invite, prefilled from the user, with these differences:
   - Email hint for someone else (V): "Changing the email resets sign-in and sends a setup link to the new address."
   - Email hint on your own page (P): "Changing your email resets your sign-in. You will be signed out, and a setup
     link will be sent to the new address."
   - Role starts on the person's current role.
   - Email and Role stay **editable** for the only active manager (a read-only look-alike field confuses more than it
     helps); Save checks them first (Interactions).
6. **Action error region** — `<p role="alert">`, always rendered, above Save.
7. **Save** — primary, `type="submit"`, busy "Saving…" (S). The word is "Save", as on every setup form
   (primary-actions.md). No Cancel button.

### E. Data and helpers (not visual)

- **One query** `['admin', 'users']` from `GET /api/admin/users` (lists are returned whole, 6.1). Detail and Edit
  `select` one user from it with `staleTime: 0` and **`refetchOnWindowFocus: true` set explicitly** (the app default
  is off; app-shell.md lists this query among the opt-ins), so they refetch on mount and on window focus (6.8). The
  Edit form copies the values once and is never overwritten by a refetch. No polling. There is no single-user
  endpoint and none is needed.
- **Query defaults** (empty-loading-error.md): `networkMode: 'always'`, the `api()` wrapper's own 10 s timeout (the
  query passes TanStack's `signal`, never `AbortSignal.timeout`, whose abort the wrapper would rethrow as it came, not
  as a timeout; empty-loading-error.md *Timing*), one silent retry for network, timeout or 5xx only. Use
  `isLoadingError` (never `isError`) so a failed background refetch never replaces data already on screen.
- **Mutations**: `retry: 0` (TanStack's mutation default is no retry
  [Convention https://tanstack.com/query/latest/docs/framework/react/guides/mutations]); the manager retries. Each
  action's `mutationFn` does the request **and then awaits `refetchQueries(['admin','users'])`**, so the button stays
  busy until the new ETag has arrived and a quick second press cannot hit a stale `If-Match`.
- **`If-Match`** on every `PUT` and state-changing `POST` on a user, from the user's `ConcurrencyStamp` (6.6). The list
  shape in 6.6 has no such field yet (open question). The Edit form captures the ETag **when it first fills the
  fields** and does not silently take a newer one from a background refetch; it takes the new one only after a `412`.
- **Helpers** in `src/manage/users/userRules.ts` (user-management.md): `byName`, `isOnlyActiveManager(users, viewerId)`
  (exactly one user with `role = Manager` and `status = Active`, and it is the viewer), `allowedActions(user,
  onlyManager)`, and one `validateUser()` shared by Invite and Edit. The server is still the gate (`409
  user.last_manager`).
- **No React Aria, no form library, no React 19 `<form action>`** (it resets uncontrolled fields after the action,
  react#29034). Controlled inputs, `<form noValidate onSubmit>` + `preventDefault()`, `useMutation`.
- **Strings** live in `src/copy` (microcopy.md); API error codes map through `apiErrors.ts`.

---

## States  (every state from design.md 4.11 plus loading/empty/error, each with exact copy)

Shared failure shapes (S, microcopy.md §9, used for loads and actions alike; a 10 s timeout counts as "no connection"):
- Load failed: "[Thing] did not load: no connection. Try again." / "…: something went wrong. Try again."; after a
  failed Try again, "[Thing] still did not load: …".
- Action failed: "Not [done]: no connection. Try again." / "Not [done]: something went wrong. Try again."

### Users list

| State | When | What shows (exact copy) |
|---|---|---|
| Waiting | Request in flight under 1 s | `<h1>` "Users" and **Invite user**. Status line empty. No rows, no "No users". |
| Loading | Still in flight after 1 s | "Loading users…" (S) in the status line. |
| Ready | Data arrived | Rows as in Layout. The status line is cleared. |
| Ready, nobody deactivated | No Deactivated users | No `<h2>` and no second list. |
| Empty | **Cannot happen**: the viewing manager is always listed. | No empty sentence is designed. If the list ever comes back empty (a bug), show the rows area empty; do not invent copy. |
| Load failed | Failed after one silent retry, nothing on screen | "The user list did not load: no connection. Try again." (or "…: something went wrong. Try again.") + **Try again** (secondary button). |
| Still failing | Try again failed | "The user list still did not load: no connection. Try again." Focus stays on **Try again**. |
| Try again busy | Pressed | Error text cleared at once; button `aria-disabled`, label "Loading…" after 400 ms. |
| Background refresh failed | Data already on screen | Nothing changes and nothing is announced. |
| After Send invite | Arrived from Invite (a replace) | The `<h1>` "Users" takes focus; the new row reads "Invited · Worker". No banner (owner question). |
| Worker visits the URL | Role guard | Whole page: `<h1>` "Page not found", "If you typed or pasted the web address, check it is correct.", link **Go to Today** (S). Title "Grow2Notes – Page not found". |

### Invite user

| State | What shows (exact copy) |
|---|---|
| Default | Empty Name and Email address; Role = **Worker** selected; no errors; no summary. |
| Name empty | "Enter the person's name" (S, form-validation.md) inline and in the summary. |
| Name over 100 | "Name must be 100 characters or less" (S). |
| Email empty | "Enter an email address" (S). |
| Email malformed (loose check: text, one "@", text) | "Enter an email address in the correct format, like name@example.com" (S, GOV.UK). |
| Role missing (only via a server `422`; Worker is preselected) | "Choose Worker or Manager" (S). |
| Email in use (`409 user.email_in_use`) | On Email: "Another account already uses this email address. Use a different one, or find the person in the Users list." (S, microcopy.md canonical). The likely cause is a returning worker under **Deactivated**, who needs Reactivate. |
| Server field errors (`422 validation.failed`) | Mapped by key (`displayName`, `email`, `role`) to the same fields and wording; unknown keys become unlinked summary items. |
| Busy | Send invite `aria-disabled`, "Sending…" after 400 ms. Fields stay editable and are never cleared. |
| No connection | Above the button: "Not sent: no connection. Try again." (S) |
| Server error | Above the button: "Not sent: something went wrong. Try again." (S) |
| Success (`201`) | Leaves the page; see the list's "After Send invite". |
| Page title with errors | "Error: Grow2Notes – Invite user"; the prefix goes when the last error is fixed. |

### User detail

| State | When | What shows (exact copy) |
|---|---|---|
| Waiting / Loading | The users query is in flight | Back link only; after 1 s "Loading user…" (S) in a `<p role="status">`. No `<h1>` yet. |
| Not found | ID not in the loaded list (bad, old or other-organisation ID) | Whole page "Page not found" (as above). |
| Load failed | Query failed, nothing cached | Fallback `<h1>` "User", then "This user did not load: no connection. Try again." (or the server cause) + **Try again**. "This user still did not load: …" on a repeat failure. |
| Invited | `status = Invited` | Summary Status "Invited". Edit · Resend invite (with description) · rule · Deactivate (with description). |
| Active | `status = Active` | Status "Active". Edit · Reset sign-in · rule · Deactivate. |
| Deactivated | `status = Deactivated` | Status "Deactivated" (plain text, not faded). Edit · rule · Reactivate. |
| Your own page, another active manager exists | Viewer = this user | As Active, with the "your own" dialog copy below. No "(you)" label. |
| Your own page, **only active manager** (A26) | Viewer = this user, the one active manager | Edit · rule · notice: "You are the only active manager, so you cannot reset your own sign-in, deactivate your account or make yourself a worker. To do any of these, first make someone else a manager." (P; rule V A26) |
| Resend invite busy | Pressed | Button `aria-disabled`, "Sending…" after 400 ms; the inline status and alert are cleared. |
| Resend invite sent | `204` | Inline `role="status"`: "Setup link sent to alex.park@example.org at 4:12 pm. The old link no longer works." (P). The time is the `204` response's `Date` header (the server clock) in Melbourne time through `time()`; if the header is missing, the sentence leaves out "at …". Never the device clock (microcopy.md). The line is cleared, then rewritten on the next frame, so a second send is always announced. The button stays. |
| Resend failed | Network / server | Alert: "Not sent: no connection. Try again." / "Not sent: something went wrong. Try again." (S) |
| Reactivate busy / failed | | "Reactivating…" (S) / alert "Not reactivated: no connection. Try again." or "…something went wrong. Try again." (S pattern) |
| Reactivated | `204` | Status "Active" (or whatever the API returns; see open questions); Deactivate and the matching group 1 action appear. Focus on the Status row. No banner. |
| Deactivated (just now) | Dialog `204` | Status "Deactivated"; Reactivate replaces Deactivate; Resend invite / Reset sign-in disappear. Focus on the Status row. No banner. |
| Sign-in reset (just now) | Dialog `204` | Status "Invited"; Resend invite replaces Reset sign-in. Focus on the Status row. No banner. (The dialog already said a new setup link will be emailed.) |
| Arrived from Edit | Save `200` | The detail page with the new values; focus on the `<h1>`. No banner. After an email change the Status row shows what the API returns. |
| Stale (`412`) on Resend or Reactivate | Someone changed this user first | Refetch; alert: "Someone else has just changed Alex Park's account. The page now shows the latest details." (P). The page re-renders from fresh data. |
| Last manager (`409 user.last_manager`) on a page action | Only possible in a race | Alert: the "you" form of the matching message (Dialogs table). |

### Dialogs

| Part | Deactivate (someone else) | Deactivate (an Invited person) | Deactivate (yourself) |
|---|---|---|---|
| Title `<h2>` | "Deactivate Alex Park?" (S) | same | "Deactivate your own account?" (P) |
| Body | "Alex Park will be signed out everywhere now and can't sign in. Their notes stay." (**V**, kept word for word including "can't") | "Alex Park has not set up their account yet. Their setup link will stop working." (P) | "You will be signed out everywhere now and cannot sign in again. Your notes stay." (P) |
| Confirm (standard style) | "Deactivate Alex Park" (S) | same | "Deactivate my account" (P) |
| Safe | "Go back" (S) | same | same |
| Busy (status line after 1 s) | "Deactivating…" (S) | same | same |

| Part | Reset sign-in (someone else) | Reset sign-in (yourself) |
|---|---|---|
| Title `<h2>` | "Reset sign-in for Alex Park?" (S) | "Reset your own sign-in?" (P) |
| Body line 1 | "Only do this after you have checked, by phone or in person, that Alex Park is the one asking." (S, from 4.11 "The manager confirms who is asking … by phone or in person") | "You will be signed out everywhere now. A new setup link will be emailed to sam.lee@example.org." (P) |
| Body line 2 | "Their current way of signing in will be removed, and they will be signed out everywhere. A new setup link will be emailed to alex.park@example.org." (S) | — |
| Confirm (**warning** style) | "Reset sign-in for Alex Park" (S) | "Reset my sign-in" (P) |
| Safe / Busy | "Go back" / "Resetting sign-in…" (S, microcopy canonical) | same |

| Dialog error | Copy | Then |
|---|---|---|
| No connection / server | "Not deactivated: no connection. Try again." · "Not deactivated: something went wrong. Try again." · "Not reset: no connection. Try again." · "Not reset: something went wrong. Try again." (S pattern) | Buttons active again; focus stays on the confirm button. |
| `412 precondition.failed` | "Someone else has just changed Alex Park's account. Go back to see the latest details." (S) | **No longer possible**: confirm button removed, focus to Go back; the users query refetches. |
| `409 user.last_manager` (race: the other manager changed their own role or status at the same moment) | Deactivate: "You are the only active manager, so you cannot deactivate your account. Make someone else a manager first." · Reset: "You are the only active manager, so you cannot reset your own sign-in. Make someone else a manager first." (P, "you" form of the microcopy.md pattern) | No longer possible. |
| `401` | No message | The dialog closes first, then sign-in-in-place runs (8.5). |

### Edit user

| State | What shows (exact copy) |
|---|---|
| Waiting / Loading | `<h1>` "Edit user" at once; "Loading user…" after 1 s; no fields yet. |
| Not found / Load failed | "Page not found" / "This user did not load: [cause]. Try again." + Try again (fallback `<h1>` stays "Edit user"). |
| Default | Fields prefilled; hint per "someone else" or "your own". |
| Only active manager (your own page) | Notice under the `<h1>`: "You are the only active manager, so you cannot change your email or make yourself a worker. First make someone else a manager." (P) |
| Field errors | Same as Invite. |
| You changed your own role to Worker while the only active manager | On Role: "You are the only active manager, so you cannot make yourself a worker. Make someone else a manager first." (P, "you" form of microcopy.md) |
| You changed your own email while the only active manager | On Email: "You are the only active manager, so you cannot change your email. Make someone else a manager first." (P) |
| `409 user.last_manager` from the server | Same two messages, on the field that changed (race only). |
| `409 user.email_in_use` | Same as Invite. |
| `412` (someone else saved this user first) | Alert above Save (not the summary; it belongs to no field): "Someone else changed this user while you were editing. Check the details, then save again." (S). Refetch for the new ETag; **typed values kept**; focus stays on Save. |
| Busy / No connection / Server error | "Saving…" / "Not saved: no connection. Try again." / "Not saved: something went wrong. Try again." (S) |
| Saved (someone else) | Detail page with the new values, focus on its `<h1>` (Detail table). No banner. |
| Saved, your own name | Detail page, focus on the `<h1>`; `/me` refetched so the Account panel's "Signed in as …" updates. |
| Saved, your own role → Worker | Today, focus on Today's `<h1>`; the nav rebuilds without Manage. No banner (today.md shows a banner only after Submit). |
| Saved, your own email | Full reload to sign-in (`window.location.replace('/')`): you have been signed out. |

---

## Interactions and focus  (what happens on each action; focus order; where focus goes after actions; live-region announcements)

### What each action does

| Action | Request | On success | Focus after | Announced |
|---|---|---|---|---|
| Open a row | none (navigate) | Detail page. Remember the user ID in memory. | `<h1>` (name) when it mounts | The heading |
| "‹ Users" back link (pops) / browser Back to the list | none | List | The row the manager opened, if still listed (shell's `handle.returnFocus`, memory only, D22); otherwise the `<h1>`. | The row's name and description |
| **Invite user** | none (navigate) | Invite page | `<h1>` "Invite user" | The heading |
| **Send invite**, checks fail | none | Inline errors + summary, title "Error: …" | Error summary (every attempt) | "There is a problem", then the list |
| **Send invite** | `POST /api/admin/users {displayName, email, role}` (trimmed; no `If-Match`, new row) | Refetch users; `navigate('/manage/users', { replace: true })` | The list's `<h1>` "Users" | The heading; the new row reads "Invited · Worker" |
| Send invite `409`/`422` | | Field errors + summary | Summary | Summary |
| Send invite network/server | | Alert above the button; nothing cleared | Stays on Send invite | Alert text (assertive) |
| **Edit** | none (navigate) | Edit page | `<h1>` "Edit user" | The heading |
| **Save**, client checks (including last-manager) fail | none | Errors + summary | Summary | Summary |
| **Save** | `PUT /api/admin/users/{id}` with `If-Match` | Someone else: refetch, `navigate(detail, { replace: true })`. Yourself: see the Edit states (reload, Today, or detail). | The destination's `<h1>` (or sign-in after a reload) | The heading |
| Save `412`, network or server failure | | Alert above Save; typed values kept | Stays on Save | Alert text (assertive) |
| **Resend invite** | `POST …/setup-link` with `If-Match` | Refetch; inline status sentence with the time from the response's `Date` header | **Stays on Resend invite** (the button remains) | Busy label via the page status region, then the inline `role="status"` sentence (polite), cleared and rewritten so a second send is announced again. |
| **Reset sign-in** | none | Opens the dialog | Dialog title `<h2>` | "Reset sign-in for Alex Park?, alert dialog" + body |
| Reset: confirm | `POST …/setup-link` with `If-Match` | Someone else: refetch; unmount the dialog; status → Invited, Resend invite replaces Reset sign-in. Yourself: `window.location.replace('/')`. | The Status row (the trigger is gone) | "Status, Invited", through focus |
| **Deactivate** | none | Opens the dialog | Dialog title | Title + body |
| Deactivate: confirm | `POST …/deactivate` with `If-Match` | Someone else: refetch; unmount the dialog; Reactivate replaces Deactivate. Yourself: `window.location.replace('/')`. | The Status row | "Status, Deactivated", through focus |
| Dialog: Go back / Escape / Android Back | none | Dialog closes, nothing changed | The trigger button | — |
| Dialog: confirm pressed | | Both buttons `aria-disabled`; Escape and Back blocked while busy | Stays | "Deactivating…" / "Resetting sign-in…" after 1 s (polite, inside the dialog) |
| **Reactivate** | `POST …/reactivate` with `If-Match` | Refetch; Deactivate replaces Reactivate | The Status row (the trigger is gone) | "Status, Active", through focus |
| **Try again** (load failure) | Refetch the failed query | Content replaces the message | Page `<h1>` (the button has gone) | The heading |
| Try again fails | | "… still did not load: …" | Stays on Try again | The new text (polite) |
| Any `401` | | Sign-in in place (8.5); dialogs close first; typed values stay in memory | Owned by session-timeout.md | Owned there |

Deactivating an **Invited** person is how a manager withdraws an invite: it rotates the security stamp, so the setup
link stops working (8.1, 8.6). No extra control is needed.

Nothing on this screen warns on leaving: Invite and Edit have three short fields, and design.md asks for an
unsaved-changes warning only on Guide prompts (4.10).

### Focus order (Tab), per page

Every page starts with the skip link "Skip to main content", the wordmark, the nav items and Account (app-shell-nav.md).
Then:

- **List:** Invite user → [Try again, only on failure] → each current-user row
  (one stop per row) → each Deactivated row. Enter follows a row. No arrow-key handling (not a composite widget).
- **Invite:** back link "Users" → [summary links] → Name → Email address → Role (one Tab stop; arrow keys move and select,
  natively) → Send invite. Enter in a text field submits the form (implicit submission); that is safe because it only
  sends what is on screen, after validation.
- **Detail:** back link "Users" → Edit → Resend invite **or** Reset sign-in → Deactivate **or** Reactivate. The notice
  and the Status row (a script-only focus target) add no stop.
- **Dialog:** title (focused, not a Tab stop) → confirm button → Go back. A native modal lets focus reach browser chrome
  after the last button; that is not a trap (SC 2.1.2).
- **Edit:** back link "{name}" → [summary links] → Name → Email address → Role → Save.

### Live regions and announcements

All of these exist in the DOM from the page's first render and are only ever filled in [Standard SC 4.1.3; Research
O'Hara https://www.scottohara.me/blog/2022/02/05/are-we-live.html]:

| Region | Page | Role | Carries |
|---|---|---|---|
| List / user status line | List, Detail, Edit | `role="status"` | "Loading users…", "Loading user…", load errors |
| Action error | Invite, Edit, Detail | `role="alert"` | Network and server failures, `412` on Edit and Detail, race `409` on the Detail page |
| Resend result | Detail (Invited) | `role="status"` | "Setup link sent to … at 4:12 pm. …" |
| Dialog error / busy | Dialog | `role="alert"` / `role="status"` | Owned by confirm-dialog.md |
| Page status region (`PageStatus`, visually hidden) | Every page (app-shell.md component 3) | `role="status"` | "Sending…", "Saving…", "Reactivating…", Try again's "Loading…" after 400 ms (the shared Button writes them here) |

Not live regions, by design: the error summary and the focused Status row (they take focus, so 4.1.3 does not apply
and a live role risks double reading); the last-manager notice (static content); status words in rows.

---

## Accessibility checklist  (headings, landmarks, labels, keyboard, screen reader, WCAG 2.2 AA criteria)

**Language and titles**
- [ ] `<html lang="en-AU">`.
- [ ] Titles from the fixed page-name list: "Grow2Notes – Users", "– Invite user", "– User", "– Edit user"; never a
  name or email; "Error: " prefix after a failed submit (SC 2.4.2).

**Landmarks and headings**
- [ ] One `<header>` (wordmark + `<nav>`), one `<main id="main-content">`; the skip link focuses `<main>` without
  changing the URL.
- [ ] Exactly one `<h1>` per page: "Users", "Invite user", the person's name (fallback "User"), "Edit user".
- [ ] List: `<h2>` "Deactivated" only when that section exists. Error summary heading and dialog titles are `<h2>`.
- [ ] Notices contain no heading element.

**Lists, summary and links**
- [ ] Both user lists are `<ul role="list">`; each row is one `<a href>` whose accessible name is the name only, with
  the email and "Status, Role" as its description (`aria-describedby`).
- [ ] Middle dots are `aria-hidden` with a visually hidden ", " after them. Chevrons are `aria-hidden`.
- [ ] Detail facts are a `<dl>` with `<div>` rows.
- [ ] Invite user, Edit and the back links are links; Send invite, Save, Resend invite, Reset sign-in, Deactivate,
  Reactivate, Try again and the dialog buttons are `<button>`s.

**Forms**
- [ ] Every input has a visible `<label for>`; hints and errors are in `aria-describedby`; `aria-invalid="true"` only
  while in error after a submit attempt.
- [ ] Role is a `<fieldset>` with `<legend>Role</legend>`; each radio's hint is in its own `aria-describedby`; the
  role error sits under the legend.
- [ ] `noValidate`, no `required`, no `maxLength`, no `:invalid` styling; `autocomplete="off"` on Name and Email
  (SC 1.3.5 does not apply: the data is about another person).
- [ ] Error messages sit **above** their input and start with a visually hidden "Error: ".

**Keyboard and focus**
- [ ] Everything works with Tab, Shift+Tab, Enter, Space, arrow keys (radios) and Escape (dialogs). No positive
  `tabindex`; programmatic focus targets use `tabindex="-1"`.
- [ ] Focus is visible on every stop: 3 px `--colour-focus` outline, 2 px offset (rows: on `::after`, −3 px).
  `outline`, never `box-shadow` alone (SC 2.4.7, 1.4.11).
- [ ] Focus never falls to `<body>` after an action removes its own button (the Status row or the `<h1>` takes it).
- [ ] Nothing sticky, so nothing hides the focused element (SC 2.4.11).
- [ ] No HTML `disabled` anywhere; busy buttons use `aria-disabled` and ignore presses.

**Screen reader** (expected; confirm with NVDA + Chrome, VoiceOver + iOS Safari, TalkBack + Chrome)
- [ ] Row: "Alex Park, link, alex.park@example.org, Invited, Worker".
- [ ] Radio: "Worker, radio button, checked, 1 of 2, Writes notes and reads past notes", inside "Role, group".
- [ ] After Send invite: "Users, heading level 1"; the new row reads "Alex Park, link, alex.park@example.org,
  Invited, Worker".
- [ ] After Resend invite: "Sending…" then "Setup link sent to … at 4:12 pm. The old link no longer works."; focus
  stays on the button.
- [ ] Dialog open: "Deactivate Alex Park?, alert dialog, Alex Park will be signed out everywhere now and can't sign in.
  Their notes stay."
- [ ] After Deactivate / Reset / Reactivate: "Status, Deactivated" (or "Invited", "Active") from the focused Status row.
- [ ] Failed submit: "There is a problem, heading level 2", then the linked errors.

**Visual**
- [ ] Status always in words, never by colour; Deactivated rows have normal contrast (SC 1.4.1, 1.4.3).
- [ ] Text 7:1 or better on the default tokens; field borders and radio circles 3:1 or better (SC 1.4.11).
- [ ] Warning (red) appears only on the final "Reset sign-in for …" button, and the words carry the warning.
- [ ] 320 px wide and 200% text: no sideways scrolling; long emails and names wrap (`overflow-wrap: anywhere`) and are
  never truncated (SC 1.4.4, 1.4.10, 1.4.12).
- [ ] Windows forced colours: radio circle and dot, row dividers, summary borders, notice edges, button borders and
  focus rings stay visible; `aria-disabled` buttons show `GrayText`.
- [ ] Tap targets: rows and radio rows 56 px; buttons and links 48 px; 8 px between separate targets (A32).
- [ ] No motion of any kind.

**WCAG 2.2 AA criteria this screen must meet:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.4.1 Use
of Color · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow · 1.4.11 Non-text Contrast · 1.4.12 Text
Spacing · 2.1.1 Keyboard · 2.1.2 No Keyboard Trap · 2.4.1 Bypass Blocks · 2.4.2 Page Titled · 2.4.3 Focus Order ·
2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus Not Obscured
(Minimum) · 2.5.3 Label in Name · 2.5.8 Target Size (Minimum) · 3.1.1 Language of Page · 3.2.2 On Input · 3.2.4
Consistent Identification · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 3.3.3 Error Suggestion · 3.3.4
Error Prevention (Reset sign-in and Deactivate confirmed; Invite and Edit checked; Reactivate reversible) · 3.3.7
Redundant Entry (values kept after any error; Edit prefilled) · 4.1.2 Name, Role, Value · 4.1.3 Status Messages.
1.3.5 Identify Input Purpose does not apply to these fields.

---

## Acceptance criteria  (testable bullets a developer and tester can check)

**Access**
- AC1. A worker opening `/manage/users`, `/manage/users/new` or `/manage/users/{id}` sees "Page not found" with
  "Go to Today", and the title "Grow2Notes – Page not found".
- AC2. A manager reaches the list from Manage > Users; Manage shows `aria-current="true"`.
- AC3. No page title, URL, query string, router `state`, `localStorage`, `sessionStorage` or IndexedDB entry contains a
  user's name or email at any point (check DevTools Application tab and `history.state` after every flow).
- AC4. The parent company's name appears nowhere on these pages or in the invite and reset emails (CI check).

**List**
- AC5. With Sam Lee (Active Manager, the viewer), Jo Smith (Active Worker), Alex Park (Invited Worker) and Chris Ng
  (Deactivated Worker), the list shows Alex Park, Jo Smith, Sam Lee in that order under "Users", and Chris Ng under an
  `<h2>` "Deactivated"; each row reads name / email / "{Status} · {Role}". Names with accents or different case sort
  as `Intl.Collator('en-AU', { sensitivity: 'base' })` sorts them.
- AC6. With nobody deactivated, there is no "Deactivated" heading.
- AC7. Tapping anywhere in a row (name, email, status, chevron, blank space) opens that user's detail page; each row is
  one Tab stop.
- AC8. Returning by the back link (which pops) or browser Back puts focus on the row just opened; opening the detail
  page from a pasted URL and pressing the back link pushes the list and focuses its `<h1>`.
- AC9. On a throttled connection, nothing shows for the first second, then "Loading users…"; there are no skeletons or
  spinners.
- AC10. Offline: "The user list did not load: no connection. Try again." and **Try again** appear within about 2 s;
  a failed retry shows "The user list still did not load: no connection. Try again."; a successful retry moves focus
  to the `<h1>`.
- AC11. At 320 px and 200% text, an 80-character email wraps inside its row; the page never scrolls sideways.
- AC12. No row is faded, greyed or coloured by status.

**Invite**
- AC13. The Invite page opens with Worker selected, both fields empty and no error showing.
- AC14. Pressing Send invite with both fields empty shows "Enter the person's name" and "Enter an email address"
  inline and in a focused "There is a problem" summary, sends no request, and sets the title to "Error: Grow2Notes –
  Invite user".
- AC15. Fixing a field removes its error and summary item as you type; the summary never takes focus while typing.
- AC16. "alex" gives "Enter an email address in the correct format, like name@example.com"; a 101-character name
  gives "Name must be 100 characters or less"; text pasted beyond 100 characters is not cut off.
- AC17. Neither field offers the manager's own saved name or email (manual check in Chrome, Safari and Edge; some
  browsers may still offer profile suggestions, which is acceptable only if nothing is filled in without a choice).
- AC18. A valid invite sends `POST /api/admin/users` once even on a double tap; "Sending…" appears only if the request
  takes over 400 ms; on `201` the list shows the new row "Invited · Worker" with focus on the `<h1>` "Users" and no
  banner; Back does not return to the filled form.
- AC19. An email that already has an account gives the email-in-use message on Email, linked from the summary, with all
  typed values kept.
- AC20. With the network cut, Send invite shows "Not sent: no connection. Try again." above the button, focus stays on
  the button, and nothing is cleared.

**Detail and actions**
- AC21. The detail page shows the name as `<h1>`, then Email address, Role and Status, and only the actions in the
  "Which actions show" table for that status. No button is ever rendered with `disabled` or greyed out.
- AC22. Logged in as the only active manager, your own detail page shows Edit and the last-manager notice, and no
  Reset sign-in or Deactivate. With a second active manager added, both buttons appear on your page.
- AC23. Resend invite (Invited) sends one `POST …/setup-link` with `If-Match`, keeps focus on the button and shows
  "Setup link sent to {email} at {Melbourne time}. The old link no longer works.", with the time taken from the
  response's `Date` header (set the device clock to another day and time zone: the sentence still shows the server
  time); pressing it again a minute later updates the time and is announced again.
- AC24. Reset sign-in opens a dialog titled "Reset sign-in for {name}?" with focus on the title; pressing Enter
  straight away does nothing; the confirm button is red and reads "Reset sign-in for {name}"; Go back, Escape and
  Android Back each close it with focus back on Reset sign-in and no request sent.
- AC25. Confirming the reset sends one request; on success the dialog closes, focus is on the Status row, which reads
  "Invited", and Resend invite has replaced Reset sign-in. No banner appears.
- AC26. Deactivate opens a dialog with the design's body text verbatim and a **blue** (standard) confirm button
  "Deactivate {name}"; on success focus is on the Status row, which reads "Deactivated", and Reactivate is the only
  serious action. No banner appears.
- AC27. Deactivating an Invited person makes their old setup link show "This link has expired. Ask a manager to send a
  new one." (4.1).
- AC28. Reactivate has no dialog; on success focus is on the Status row, which reads "Active", and Deactivate returns.
- AC29. Deactivating or resetting **yourself** (with another active manager present) uses the "your own" copy and, on
  success, reloads the app to the sign-in page.
- AC30. A `412` while confirming a dialog removes the confirm button, shows "Someone else has just changed {name}'s
  account. Go back to see the latest details.", moves focus to Go back, and the page shows fresh data after Go back.
- AC31. A busy dialog ignores further presses and Escape; after 1 s it shows "Deactivating…" or "Resetting sign-in…"
  under the buttons; the button labels do not change.
- AC32. A `401` during any action closes any open dialog before the sign-in-in-place screen appears; after signing in
  the manager is back on the same page.
- AC33. Every `POST` and `PUT` on an existing user carries `If-Match`; none is retried automatically.

**Edit**
- AC34. Edit opens with the current values; the email hint reads "Changing the email resets sign-in and sends a setup
  link to the new address." (your own page: the "your own" hint).
- AC35. As the only active manager, choosing Worker or changing your email and pressing Save shows the matching "You
  are the only active manager…" error inline and in a focused summary, and sends nothing.
- AC36. Saving someone else's changes lands on their detail page (a replace) with focus on its `<h1>` and no banner;
  Status reads "Invited" after an email change if the API sets it.
- AC37. A `412` on Save shows the stale-save sentence in the alert above Save (not the summary), keeps every typed
  value and focus on Save, and a second Save succeeds with the new ETag.
- AC38. Saving your own role as Worker lands on Today with focus on its `<h1>`, no banner, and a nav without Manage;
  saving your own new email reloads to sign-in.

**General**
- AC39. axe-core reports no violations on all four pages and both dialogs, in each state above.
- AC40. All copy matches the States tables exactly, uses Australian spelling, and contains no "delete", "log in",
  "click", "invalid", "oops", "sorry" or "please".

---

## Conflicts resolved  (where component recommendations disagreed, what you chose and why)

1. **Table or list.** status-tags.md says the laptop "may be a table" with role-first meta `Worker · Invited`;
   user-management.md says one stacked list everywhere, status first. **Chosen: stacked list on every width, status
   first ("Invited · Worker").** The task is find-then-act, not compare [Research NN/g mobile tables]; a restyled table
   loses its semantics [Opinion Roselli]; status-first matches Today's "Draft · …" lines so the same eye habit works
   across screens.
2. **Name field label.** user-management.md: "Full name" (GOV.UK names pattern); microcopy.md and design.md: "Name".
   **Chosen: "Name"** with the hint "Shown on the notes they write". It is the design's word, it matches the error
   "Name must be 100 characters or less", and "Full name" would push managers away from short forms like design.md's
   own "Alex P.", which is a decision the manager makes by what they type (microcopy.md).
3. **Deactivate confirm style.** primary-actions.md: warning (red); confirm-dialog.md and user-management.md: standard.
   **Chosen: standard.** Reactivate undoes it, and GOV.UK keeps warning buttons for consequences "that cannot be easily
   undone" (https://design-system.service.gov.uk/components/button/). Red stays meaningful for Reset sign-in and
   Discard draft only.
4. **Dialog initial focus.** primary-actions.md: Go back; confirm-dialog.md: the title. **Chosen: the title** (the
   dialog component owns it). It puts the person's name first for screen readers and leaves no default button, so a
   double Enter cannot confirm [Standard APG "static element at the start" https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/;
   Convention Apple and NN/g "no default answer"].
5. **Where Send invite lands.** primary-actions.md: the new user's detail page; user-management.md: the list.
   **Chosen: the list.** Managers invite several people in a row at go-live, Invite user is at the top of the list,
   and the `201` response does not need to return the new ID.
6. **Focus after Deactivate, Reset sign-in or Reactivate.** primary-actions.md: the status line; confirm-dialog.md:
   the `<h1>` plus a page status region; status-messages.md and user-management.md: a focused success banner.
   **Chosen (editorial pass): no banner; focus moves to the Status row**, which now shows the new state. The trigger
   is gone, so focus must move somewhere deliberate; the Status row says exactly what changed, in the place it is
   shown, with no new message. App-wide rule: no success banners except after Submit (design.md specifies no
   other), as note-form.md and participant-notes.md already applied. The banners are listed as owner questions.
7. **Resend invite confirmation.** form-validation.md calls it a confirmation dialog; confirm-dialog.md,
   primary-actions.md and user-management.md give it none. **Chosen: no dialog.** It is harmless and repeatable, and
   each needless dialog weakens the ones that matter [Research Anderson et al. CHI 2015
   https://scholarsarchive.byu.edu/facpub/9306/].
8. **Last-manager and email-in-use wording.** primary-actions.md ("There must always be at least one active manager.",
   "This email address already has an account."), form-validation.md and confirm-dialog.md (third person "[Name] is
   the only active manager…", with "can't"), microcopy.md (canonical pattern). **Chosen: microcopy.md's pattern and
   email-in-use sentence, written in the "you" form.** Only an active manager can call these endpoints, so a
   last-manager block can only ever apply to the viewer; "Sam Lee is the only active manager" said to Sam Lee is
   confusing. Negative contractions removed ("cannot") per GOV.UK style for readers of English as a second language.
9. **Load-failed copy.** user-management.md ("Couldn't load the users. Check your connection and try again."),
   microcopy.md ("[Thing] did not load: [cause]. Try again."), empty-loading-error.md ("Could not load {thing}." +
   cause sentence, then "Still could not load…"). **Chosen (editorial pass, app-wide): microcopy.md §9's words**
   ("The user list did not load: [cause]. Try again.", then "still did not load") with empty-loading-error.md's
   mechanism, which keeps repeat failures audible [Convention NHS App error pages].
10. **Action failure and success wording.** confirm-dialog.md ("Sam Lee was not deactivated. Check your connection…",
    "Sam Lee is deactivated.", "Sign-in reset. A new setup link has been emailed…") versus microcopy.md's shapes.
    **Chosen: microcopy.md** for the failure lines ("Not deactivated: no connection. Try again."), because microcopy.md
    §9 is the declared canonical table. Its success strings ("Alex Park deactivated", "Sign-in reset for Alex Park…")
    are not built (Conflicts 6; Open questions 7).
11. **Where a network failure shows on Invite and Edit.** form-validation.md: an unlinked item in the error summary
    (focus moves to the top); primary-actions.md and user-management.md: an alert above the button (focus stays).
    **Chosen: the alert above the button** for every request failure that belongs to no field (network, server,
    `412`); the summary keeps field errors (client checks, `422`, and the `409`s that map to a field). The fix is one
    more press of the focused button; moving focus to the top of a phone page scrolls the manager away from it. This
    is now the app-wide rule (form-validation.md, primary-actions.md).
12. **Radio row height.** user-management.md: 44 px rows; foundations.md: radios use the same 56 px rows as tick boxes.
    **Chosen: 56 px rows with the 40 px circle** (foundations.md is the token authority; checkbox-list.md's 40 px box).
13. **Summary list side-by-side switch.** user-management.md: `@container (min-width: 32rem)`; foundations.md: one
    breakpoint, `40rem`, enforced by a CI grep for other `min-width` values. **Chosen: the 40rem media query**, so the
    one-breakpoint rule and its CI check hold.
14. **Button height.** primary-actions.md and confirm-dialog.md: 44 px; foundations.md: 48 px for buttons and links.
    **Chosen: 48 px** (foundations.md); 44 px stays the floor (A32).
15. **Name weight.** participant-list-rows.md and user-management.md: 600; foundations.md: weights 400 and 700 only.
    **Chosen: 700.**
16. **Back link wording.** user-management.md: "‹ Users" / "‹ Alex Park"; an earlier draft of this file: "Back to
    Users" / "Back to {name}", always a push. **Chosen (editorial pass): the shared BackLink, "‹ Users" / "‹ Alex
    Park"**, in the shell's before-main bar slot, popping when the previous entry is the destination (app-shell.md
    3a). One label pattern for every back link in the app (SC 3.2.4: "‹ Today", "‹ Past notes", "‹ Note"), and a pop
    returns focus to the row the manager opened.
17. **Copy with contractions in user-management.md** ("You're the only active manager … can't", "hasn't set up",
    "You'll be signed out"). **Rewritten without contractions** per microcopy.md's rule for new copy. design.md's own
    Deactivate body keeps "can't" word for word (V).
18. **Busy feedback inside dialogs versus on page buttons.** primary-actions.md swaps the button label after 400 ms;
    confirm-dialog.md keeps labels fixed and shows a status line after 1 s. **Each component keeps its own rule here**
    (page buttons swap; dialogs use the status line), because the dialog's confirm button holds focus and its name
    should not change under the user. Flagged as a cross-component inconsistency.

---

## Tensions with decisions

No conflict with D1–D43.

Recorded only, against an assumed default rather than a decision: **A27 (one email per account across the whole app)
with D3 (SaaS-ready).** If a second organisation is ever added, "Another account already uses this email address"
would tell one organisation's manager that an address has an account in another. OWASP calls this user enumeration and
advises identical responses (https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html). Today
there is one organisation and only trusted managers send invites, and A27 already notes the multi-provider case. No
change recommended.

---

## Open questions for the owner

1. **Reset sign-in confirmation.** 4.11 says the manager confirms who is asking, but specifies no on-screen
   confirmation for an action that cannot be undone. This spec uses the shared dialog (SC 3.3.4 "Confirmed"). Approve?
2. **Changing an email resets sign-in, with no confirmation** (4.11). This spec relies on the always-visible hint (V)
   and the Status row on the detail page afterwards (arguably SC 3.3.4 "Checked"). Approve, or should Save reuse the
   Reset sign-in dialog when the email has changed?
3. **Email change on a Deactivated person.** If the reset sets `Status = Invited` (8.6), it would undo the deactivation
   without Reactivate. What should the API do? The UI follows the returned status.
4. **Reactivating someone who was deactivated while still Invited.** 6.6 says they "sign in with their existing
   methods", but they have none. Return to Invited (with Resend invite) or Active? The Reactivate description "Lets
   them sign in again." assumes the second.
5. **Does a case-only email change** (Alex@ to alex@) count as an email change that resets sign-in? Recommended: no
   (Identity compares the normalised email); the client compares case-insensitively.
6. **API gap:** `GET /api/admin/users` needs a per-user ETag (`ConcurrencyStamp`) for `If-Match` (6.6).
7. **Success banners (not built).** design.md gives a success message only for Submit, so none of these is built:
   "Invite sent to {email}" (on the list), "Changes saved" and "Changes saved. Sign-in reset. A new setup link was
   emailed to {email}." (after Edit), "Alex Park deactivated", "Alex Park reactivated", "Sign-in reset for Alex Park.
   A new setup link was emailed to {email}." and "Changes saved. You are now a worker." (on Today). Instead focus
   moves to the `<h1>` or to the Status row, which shows the new state. Approve any of them? If you do, today.md must
   accept messages other than "Note for … submitted".
8. **Two sections** (current, then Deactivated) is a layout choice. If the owner prefers one flat list, drop the
   `<h2>` and sort everyone by name; nothing else changes.
9. **All (P) strings** need approval: the Name hint, the email hints, role hints, action descriptions, the last-manager
   notices and field errors, the Invited and "your own" dialog variants, the Resend inline status, the `412` sentence,
   and the back-link labels ("Users", the user's name).
10. **Worker preselected on Invite.** GOV.UK says not to preselect radios; this form preselects Worker (least
    privilege; most invites are workers). It is the one stated exception to the app's no-preselection rule
    (form-validation.md). Keep it?
