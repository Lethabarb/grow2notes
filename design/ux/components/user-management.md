# Users list, invite form and user actions

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> Editorial pass, 1 October 2026. Back links read "‹ Users" / "‹ Alex Park" (the shared BackLink); no success banners: focus moves to the Status row or the `<h1>` (users.md).

Component key: `user-management`. This file covers everything on the managers' **Users** screen (design.md 4.11): the
list of users, the **Invite user** form, the user detail page with its actions (**Edit**, **Resend invite**, **Reset
sign-in**, **Deactivate**, **Reactivate**), the **Edit** form, and the last-manager rule (A26). It uses
`GET/POST/PUT /api/admin/users…` (6.6) and follows D23, D24, A22–A24, A26–A28 and A32.

It adds no screen, setting, notification, status or data item. It says how to build what 4.11 already lists.

Shared components it uses, and does not redefine: `ConfirmDialog` (confirm-dialog.md) for Deactivate and Reset sign-in,
the button styles (primary-actions.md), the error summary and inline errors (form-validation.md), the success banner,
notice and inline status (status-messages.md), plain status words (status-tags.md) and the whole-row link
(participant-list-rows.md).

Copy marks: **(V)** verbatim from design.md · **(S)** already proposed in a sibling component file · **(P)** proposed
here, needs the owner's approval. Example people: **Sam Lee** is the signed-in manager; **Alex Park** is a worker.

---

## Where it's used

One screen, Users (4.11), reached from **Manage > Users** (4.0). Managers only. Workers who type a Users URL get
"Page not found" (app-shell-nav.md).

| Part of 4.11 | Route (IDs only, 4.0) | Page title (4.0) | What it holds | What differs |
|---|---|---|---|---|
| **Users list** | `/manage/users` | Grow2Notes – Users | Name, email, role, status for every user (V), and **Invite user** (V) | The only list. Fewer than 20 users now (D2), but users are never deleted (4.11), so Deactivated people pile up over the years. |
| **Invite user** | `/manage/users/new` | Grow2Notes – Invite user (P) | Name, email, role (Worker or Manager), **Send invite** (V) | A three-field form about **another person**, not the person typing. That changes the autocomplete rules (Best practice). |
| **User detail** | `/manage/users/{userId}` | Grow2Notes – User | The person's details and the actions that apply to their status (V) | The serious actions live here. Which buttons show depends on status, on whether this is the only active manager (A26), and on whether it is the manager's own account. |
| **Edit user** | `/manage/users/{userId}/edit` | Grow2Notes – Edit user (P) | Name, email, role, **Save** | Same fields as Invite, filled in. An email change resets sign-in (V), so the form must say so before Save. |
| **Deactivate / Reset sign-in dialogs** | none (screen state) | unchanged | `ConfirmDialog`, owned by confirm-dialog.md | This file adds only the variants that confirm-dialog.md does not cover: an Invited person, and the manager's own account. |

**Data used** (6.6): `id`, `email`, `displayName`, `role` (Worker, Manager), `status` (Invited, Active, Deactivated).
`invitedAtUtc`, `activatedAtUtc` and `deactivatedAtUtc` are returned but **not shown**: 4.11 does not list them.
"Today" and "is this me" come from `GET /api/auth/me` (`userId`).

---

## Best practice

### Table or list on phones (and laptops)

- **[Research]** NN/g separates two jobs a table does. Some users only need to *find one item*; for them, "they can
  narrow the choice of data down to the specific information they need before displaying". Others need to *compare*
  items across columns. "The smaller the screen, the more likely that we'll get into trouble." NN/g guidance, with no
  controlled study cited. https://www.nngroup.com/articles/mobile-tables/
- **[Convention]** GOV.UK: "Use the table component to let users compare information in rows and columns." Tables need a
  `<caption>` and `scope` on header cells. https://design-system.service.gov.uk/components/table/
- **[Convention]** NHS: "Use tables with caution. They may not be the best way to present information, especially for
  the public and on mobile screens." Its responsive table "stacks vertically" at 768 px and below.
  https://service-manual.nhs.uk/design-system/components/table
- **[Opinion]** (expert practitioner) Adrian Roselli: once a table's `display` is changed (for example to `block`, to
  stack it on a phone), screen readers may stop treating it as a table and its header cells become meaningless.
  https://adrianroselli.com/2017/11/a-responsive-accessible-table.html
- **[Opinion]** Roselli also warns that making a whole table row clickable means "everything in the row announces as
  clickable". He links only the title and stretches its hit area over the block.
  https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html
- **[Standard]** SC 1.4.10 Reflow exempts data tables, but everything else must work at 320 CSS px without sideways
  scrolling. Long unbroken strings (URLs, and in practice email addresses) need wrapping: technique C33 uses
  `overflow-wrap: break-word`. https://www.w3.org/WAI/WCAG22/Understanding/reflow.html ·
  https://www.w3.org/WAI/WCAG22/Techniques/css/C33
- **[Convention]** GOV.UK summary list: "Use a summary list to show information as a list of key facts", built on
  `<dl>`. "Do not use it for tabular data." "Borders help many users find and read information that's laid out in rows,
  especially users who zoom in." https://design-system.service.gov.uk/components/summary-list/
- **[Convention]** Safari with VoiceOver drops list semantics from a `<ul>` styled with `list-style: none`. The fix is
  `role="list"`. https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html

### Role radios

- **[Standard]** Group radios in a `<fieldset>` with a `<legend>` (technique H71). The APG radio group keyboard model:
  Tab enters the group on the checked radio, and the arrow keys move and select. Native `<input type="radio">` already
  behaves this way. https://www.w3.org/WAI/WCAG22/Techniques/html/H71 · https://www.w3.org/WAI/ARIA/apg/patterns/radio/
- **[Convention]** GOV.UK: "Do not pre-select radio options as this makes it more likely that users will not realise
  they've missed a question" (or submit the wrong answer). "Only use inline radios when: the question only has two
  options [and] both options are short." Item hints: "Keep each hint to a single short sentence, without any full
  stops." https://design-system.service.gov.uk/components/radios/
- **[Research]** NN/g takes the opposite view: "radio buttons always have exactly one option selected, and you therefore
  shouldn't display them without a default selection". It also says lay options out "vertically, with one choice per
  line". NN/g guidance, not a controlled study. https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/
- **[Convention]** AgDS (Australian Government) sits between them: "There will often be cases where it makes sense to have
  an option selected by default and others where it's more suitable not to select a default option." It also says use a
  vertical list. https://design-system.agriculture.gov.au/components/radio
- **[Standard]** SC 3.2.2 On Input: choosing a radio must not change the context by itself. The role is saved only when
  the form is submitted. https://www.w3.org/WAI/WCAG22/Understanding/on-input.html

### Name and email fields about someone else

- **[Standard]** SC 1.3.5 Identify Input Purpose "is specifically scoped to inputs collecting *information about the
  user*". "An input field for information that is not *about the user* does not need to programmatically expose its
  purpose." A manager typing a new worker's email is in that second case.
  https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html
- **[Convention]** GOV.UK email pattern: label "Email address", `type="email"`, `spellcheck="false"`, allow paste, wide
  enough for at least 30 characters, and the error "Enter an email address in the correct format, like
  name@example.com". Asking people to type the address twice: "Only do this if your user research shows it to be
  effective." https://design-system.service.gov.uk/patterns/email-addresses/
- **[Convention]** GOV.UK names pattern: "A single name field can accommodate the broadest range of name types." Label it
  "Full name" and set `spellcheck` to `false`. https://design-system.service.gov.uk/patterns/names/
- **[Standard]** HTML: the value of an `<input type="email">` is sanitised by stripping newlines and leading and trailing
  whitespace, so a pasted address with a trailing space is cleaned by the browser.
  https://html.spec.whatwg.org/multipage/input.html#email-state-(type=email) · MDN adds that the browser check "must not"
  be relied on, and the server must validate.
  https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/email

### Explaining the consequences of admin actions

- **[Research]** NN/g: "Use a confirmation dialog before committing to actions with serious consequences." "Be specific
  and inform users about the consequence of their action. Do not ask Are you sure you want to do this?" Use labels that
  "summarize what will happen", and "Do not use confirmation dialogs for routine actions." (2018, reviewed 2026)
  https://www.nngroup.com/articles/confirmation-dialog/
- **[Research]** Warnings lose effect quickly: an fMRI study found "a dramatic drop" in visual processing "after only the
  second exposure to a warning" (Anderson et al., CHI 2015). https://scholarsarchive.byu.edu/facpub/9306/
- **[Convention]** GOV.UK warning buttons: "Only use warning buttons for actions with serious destructive consequences
  that cannot be easily undone by a user." Use another style for the first button, and the warning style only for the
  final confirmation. "Do not only rely on the red colour." https://design-system.service.gov.uk/components/button/
- **[Convention]** GitLab Pajamas grades destructive actions by severity: high severity gets a confirmation with the
  danger button, and easily undone actions "may use the default button variant".
  https://design.gitlab.com/usability/destructive-actions
- **[Convention]** GOV.UK warning text is for consequences "of an action, or lack of action", with a visually hidden
  "Warning" prefix. https://design-system.service.gov.uk/components/warning-text/
- **[Opinion]** Smashing Magazine (Ponamariov, 2024): say exactly what will happen and to whom, and name the object in
  the button ("Delete Project X", not "Confirm").
  https://www.smashingmagazine.com/2024/09/how-manage-dangerous-actions-user-interfaces/
- **[Standard]** SC 3.3.4 Error Prevention covers actions that modify or delete "user-controllable data". One of these
  must apply: Reversible, Checked or Confirmed. Its intent is "to prevent mass loss of data such as deleting a file or
  record", not routine edits. https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html

### Unavailable actions, and saying why (the last-manager rule)

- **[Convention]** GOV.UK: "Disabled buttons have poor contrast and can confuse some users, so avoid them if possible."
  https://design-system.service.gov.uk/components/button/
- **[Opinion]** Smashing (Friedman, 2024): "disable if you want the user to know a feature exists but is unavailable.
  Hide if the value shown is currently irrelevant and can't be used." Either way, "explain why a feature is disabled
  and also how to re-enable it". No research cited.
  https://www.smashingmagazine.com/2024/05/hidden-vs-disabled-ux/
- **[Opinion]** Pereira (CSS-Tricks, 2021): the `disabled` attribute takes a button out of the tab order, so keyboard
  and screen-reader users never meet it. Prefer `aria-disabled`, or better, no disabled button at all.
  https://css-tricks.com/making-disabled-buttons-more-inclusive/

### Keeping routine and serious actions apart

- **[Research]** NN/g (Laubheimer, 2021): "Avoid placing highly consequential actions directly next to options that are
  benign." Its basis is slips during automatic, repeated work. https://www.nngroup.com/articles/proximity-consequential-options/
- **[Convention]** Primer's ActionList example puts the danger item last, after a divider.
  https://primer.style/components/action-list/
- **[Research]** NN/g (Fessenden, 2017): "Modal dialogs should be used for short, direct dialogs with the user."
  Avoid them for forms and other complex tasks. https://www.nngroup.com/articles/modal-nonmodal-dialog/
- **[Convention]** GOV.UK question pages: "Some users do not trust browser back buttons when they're entering data.
  Always include a Back link." https://design-system.service.gov.uk/patterns/question-pages/ ·
  https://design-system.service.gov.uk/components/back-link/

### Telling people the result

- **[Convention]** GOV.UK success banner: use it "to confirm that something they're expecting to happen has happened",
  move focus to it, and "Remove a green notification banner when the user moves to a new page."
  https://design-system.service.gov.uk/components/notification-banner/
- **[Standard]** SC 4.1.3: a status message is a change "that is not a change of context". A message that takes focus is
  outside 4.1.3, because focus already announces it. https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html

---

## Recommendation for Grow2Notes

### Decisions in one line each

1. **One stacked list of row links on every screen size. No table.** About 20 rows, and the job is "find this person,
   then act", not "compare columns". [Research + Opinion]
2. **Two sections:** current users (Invited and Active) first, then **Deactivated** under its own heading. Each is
   sorted by name. Every row still states its status in words. [Opinion]
3. **Invite and Edit are full pages, not dialogs.** On a phone, the keyboard and a three-field form don't fit well in a
   modal (NN/g), and the Back link and browser Back both work. [Research + Convention]
4. **Role is two stacked radios, Worker then Manager, with a one-line hint each. Invite starts with Worker selected.**
   [Opinion; GOV.UK differs, AgDS and NN/g allow it; reasons below]
5. **The detail page is a summary list plus actions in two groups.** Routine actions (Edit, Resend invite or Reset
   sign-in) come first. Deactivate or Reactivate sits below a divider. Each serious action has a one-line "what this is
   for" description above it. [Research + Convention]
6. **Only actions that apply now are shown.** For the only active manager, Reset sign-in and Deactivate are replaced by
   a notice that says why and what to do. **Nothing is disabled.** [Convention + Opinion]
7. **Deactivate and Reset sign-in use `ConfirmDialog`. Resend invite, Reactivate, Edit and Invite don't.** [Research]
8. **Results:** an action that removes its own button (Deactivate, Reset sign-in, Reactivate) shows a focused success
   banner. Resend invite keeps its button, so it shows an inline status. Invite and Save show a success banner on the
   page they lead to. (status-messages.md)

### Anatomy

**Users list (phone, 360 px)**

```
+-----------------------------------+
| Users                         <h1>|
| [          Invite user          ] |  primary, link to /manage/users/new
|                                   |
| +-------------------------------+ |  <ul role="list">
| | Alex Park                   > | |  link text = name only
| | alex.park@example.org         | |  detail (aria-describedby target)
| | Invited · Worker              | |  status word first, then role
| +-------------------------------+ |
| | Jo Smith                    > | |
| | jo.smith@example.org          | |
| | Active · Worker               | |
| +-------------------------------+ |
| | Sam Lee                     > | |
| | sam.lee@example.org           | |
| | Active · Manager              | |
| +-------------------------------+ |
|                                   |
| Deactivated                   <h2>|  only when anyone is deactivated
| +-------------------------------+ |
| | Chris Ng                    > | |
| | chris.ng@example.org          | |
| | Deactivated · Worker          | |
| +-------------------------------+ |
+-----------------------------------+
```

- **Row:** reuse the participant row (participant-list-rows.md): one `<li>`, one `<a>` whose text is the name, a detail
  block tied to it by `aria-describedby`, a decorative chevron, and the link's `::after` covering the whole row. The
  minimum height is 3.5 rem. The detail block has two lines: the email address, then `{Status} · {Role}`.
- **Status first** on its line, so a scanning eye picks out the odd ones ("Invited", "Deactivated") at the start of a
  line (status-tags.md). Plain text: no tag, no colour, never faded, including for Deactivated people.
- **Email wraps anywhere** (`overflow-wrap: anywhere`), so `firstname.longfamilyname@organisation.example.org` never
  causes sideways scrolling at 320 px or 200% text (SC 1.4.10, C33).

**Invite user**

```
< Users                                   back link (before <main> content)
Invite user                         <h1>
[ There is a problem … ]                  error summary, only after a failed Send
Full name                                 <label>
Shown on the notes they write             hint
[_______________________________]
Email address                             <label>
Grow2Notes will email them a setup        hint
link. It works once and lasts 7 days.
[_______________________________]
Role                                      <fieldset><legend>
(•) Worker                                whole row is the <label>, 44 px or taller
    Writes notes and reads past notes     item hint
( ) Manager
    Can also review flagged notes,
    download reports, and set up
    participants and users
[ Send invite ]                           primary, type="submit"
```

**User detail (Active worker)**

```
< Users
[ Sign-in reset. A new setup link has been emailed to alex.park@example.org. ]   success banner, only after an action
Alex Park                                 <h1>
Email address   alex.park@example.org     <dl> summary list (stacks on a phone)
Role            Worker
Status          Active
[ Edit ]                                  secondary, link to …/edit
For a lost phone, a new phone or a forgotten password. Check who is asking first.
[ Reset sign-in ]                         secondary, opens ConfirmDialog
----------------------------------------  divider, 1.5 rem space above and below
For someone who no longer works here. Their notes stay.
[ Deactivate ]                            secondary, opens ConfirmDialog
```

Which actions show (4.11; 6.6):

| Status | Group 1 | Group 2 |
|---|---|---|
| Invited | Edit · Resend invite | Deactivate |
| Active | Edit · Reset sign-in | Deactivate |
| Deactivated | Edit | Reactivate |
| Active, and the **only active manager** (always the viewer: see Behaviour) | Edit | The last-manager notice instead of Reset sign-in and Deactivate |

**Edit user**

Same three fields as Invite, filled in. The back link reads "< Alex Park". The `<h1>` is "Edit user". The email hint
is the design's sentence. The button is **Save**.

### Behaviour

**List**
- Opening a row goes to that user's detail page. A row has one action, and nothing inside it is interactive.
- **Order:** by `displayName`, compared with `Intl.Collator('en-AU', { sensitivity: 'base' })`. 6.6 does not fix an
  order; if the API later sorts, the client keeps the server's order. A status change moves a row only between the two
  sections, never inside one.
- **Refresh:** TanStack Query refetches on mount and on window focus (6.8). No polling. New data replaces rows silently.
- **Returning** from a detail page by the back link or browser Back: focus goes to the row the manager opened, if it is
  still there. The key is held in memory only (D22), as on Today.

**Invite**
- **Validate when Send invite is pressed** (form-validation.md rule 1). Trim the name and email, then check: name not
  blank and 100 characters or fewer, email not blank and roughly `text@text`, and a role chosen. On failure: inline
  errors, the summary, focus on the summary, and "Error: " added to the title.
- **Send:** `POST /api/admin/users` `{displayName, email, role}`. The button goes busy (`aria-disabled`, "Sending…"
  after 400 ms). A second tap is ignored.
- **Success (`201`):** put "Invite sent to alex.park@example.org" in the in-memory message store and
  `navigate('/manage/users', { replace: true })`. The list shows the new row as "Invited · Worker", and the banner takes
  focus. The list is the right place to land because a manager at go-live will invite several people in a row, and
  **Invite user** is at the top of it.
- **`409 user.email_in_use`:** the error goes on Email. The copy points to the list, because the most likely cause is
  someone who already has an account, often a returning worker under **Deactivated**, who needs **Reactivate**.
- **Network or `5xx`:** an alert above the button says "Not sent: no connection. Try again." Nothing typed is cleared
  (SC 3.3.7). A retry after a lost `201` comes back as `409 user.email_in_use`, and that message sends the manager to
  the list, where the person already shows as Invited. That is acceptable. The API is not idempotent, and no key is
  added.
- **Leaving** by the back link: nothing is saved and there is no warning. Three short fields are cheap to retype, and
  design.md asks for a warning only on Guide prompts (4.10).

**Detail page actions**
- **Edit** (all statuses): a link to the edit page.
- **Resend invite** (Invited): `POST …/setup-link` with `If-Match`. No dialog: it is harmless, and repeating it only
  replaces the link. Busy "Sending…". On success the button stays, focus stays on it, and the inline status beside it
  reads "Setup link sent to alex.park@example.org at 4:12 pm. The old link no longer works." The time makes a second
  send a real text change, so it is announced again.
- **Reset sign-in** (Active, not the only manager): opens `ConfirmDialog` (warning tone; copy in confirm-dialog.md).
  On success the status becomes Invited, so **Resend invite** replaces **Reset sign-in**. The success banner appears
  above the `<h1>` and takes focus.
- **Deactivate** (Invited or Active, not the only manager): opens `ConfirmDialog` (standard tone; copy in
  confirm-dialog.md, with the Invited variant below). On success the status becomes Deactivated: **Reactivate**
  replaces Deactivate, and Resend invite or Reset sign-in disappears. A focused success banner shows. Deactivating an Invited
  person is how a manager withdraws an invite: it rotates the security stamp, so the setup link stops working (8.1, 8.6).
- **Reactivate** (Deactivated): `POST …/reactivate` with `If-Match`. No dialog: it gives access back and can be undone
  with Deactivate. Busy "Reactivating…". On success, a focused success banner shows.
- **After any action,** invalidate the users query. If the person is the signed-in manager, also refetch `/me`.
- **`412 precondition.failed`** (someone else changed this user first): refetch, show the message in the action area's
  alert (or in the dialog, as confirm-dialog.md does), and keep the page as it is now.
- **`401`:** the app's sign-in-in-place flow (8.5). Dialogs close first (confirm-dialog.md).

**The last-manager rule (A26)**
- The client works out "only active manager" from the users list: exactly one user with `role = Manager` and
  `status = Active`, and it is this user. **Only an active manager can open this screen, so that one person is always
  the viewer.** The notice therefore talks to "you".
- On that page, **Reset sign-in** and **Deactivate** are not rendered. In their place, under the divider, is a notice
  (status-messages.md "notice": plain content, no role, no focus) that names the blocked actions and the way out.
- On that person's **Edit** page the same notice sits above the fields. Email and Role stay editable (a read-only
  look-alike field confuses more than it helps), and **Save** checks them on the client before sending: a changed email
  or Role = Worker gives the inline error and summary from form-validation.md. The server's `409 user.last_manager`
  maps to the same errors if two managers race.
- Why hide rather than disable: GOV.UK advises against disabled buttons, and Smashing's rule asks to "explain why …
  and also how to re-enable it". The notice does both in words, with no low-contrast dead control.

**The manager's own account**
- Allowed by 4.11 (except under the last-manager rule), so it is not blocked. Copy switches to "you" (below).
- **Deactivate or Reset sign-in on yourself:** after `204`, call `window.location.replace('/')`. The security stamp has
  changed, so the next request is `401` and the sign-in page shows. Don't show a banner the person can't keep.
- **Saving your own changed email:** the same full reload, because the email change reset your sign-in.
- **Saving your own role as Worker:** refetch `/me`. The nav rebuilds without Manage. Navigate to Today with the success
  banner "Changes saved. You are now a worker." (P)

**Edit**
- Same validation, busy ("Saving…") and error rules as Invite. Send `PUT /api/admin/users/{id}` with `If-Match`.
- **Success:** navigate to the detail page with the banner "Changes saved", or, when the email changed, "Changes saved.
  Sign-in has been reset and a setup link has been emailed to alex.p@example.org." The status line now reads Invited.
- **`412`:** an unlinked summary item, a refetch for the new ETag, and the typed values **kept** (form-validation.md).

### States

| Part | Default | Hover (laptop) | Focus | Active | Disabled | Loading / busy | Error | Empty | Read-only |
|---|---|---|---|---|---|---|---|---|---|
| **List row** | Name (600 weight), email, status · role, chevron | Row tint, name underlined (`@media (hover: hover)`) | 3 px outline round the whole row, on `::after` (participant-list-rows.md) | Row a step darker | **Never.** Every row opens a detail page. | n/a | n/a (list states below) | n/a | Rows are links, nothing to edit |
| **List** | Current users, then Deactivated | – | – | – | – | Nothing for 1 s, then "Loading users…" in an always-present `role="status"` | "Couldn't load the users. Check your connection and try again." + **Try again** | The main list can't be empty, because the viewer is in it. No Deactivated section when nobody is deactivated. | – |
| **Text fields** | Label, hint, input with a 2 px border at 3:1 or more | – | 3 px outline, 2 px offset | – | Not used | Stay editable while sending | Message above the input, 4 px left bar, `aria-invalid` (form-validation.md) | Invite starts empty; Edit starts filled | Not used |
| **Role radios** | 40 px circle, label, hint. Invite: Worker selected. | Row tint | Outline on the circle | – | **Not used**, even for the last manager | – | Message under the legend, 4 px bar on the fieldset | Never empty on Invite (Worker preselected) | – |
| **Send invite / Save** | Primary | Darker fill | Outline | Pressed fill | Never `disabled` | `aria-disabled`, "Sending…" / "Saving…" after 400 ms | Alert above the button | – | – |
| **Detail actions** | Secondary buttons and one link, in two groups | Darker border | Outline | Pressed fill | Not rendered when not allowed (status or A26); never greyed | `aria-disabled` + busy label (Resend, Reactivate); dialogs own their busy state | Alert region above group 1 | – | – |
| **Summary list** | Email address, Role, Status | – | – | – | – | Loads with the page | – | – | Always read-only |
| **Detail page** | As above | – | `<h1>`, or the success banner after an action | – | – | Nothing for 1 s, then "Loading…" | Unknown or other-organisation ID: "Page not found" (app shell) | – | – |

### Phone vs laptop

| | Phone (below 40 em) | Laptop |
|---|---|---|
| List | Full-bleed rows, three lines each | Same rows, column capped at 40 rem, left-aligned. Not a table (see decision 1). |
| Invite / Edit | Single column, full-width fields and button | Same, in a 40 rem column. Email field at least 30 characters wide (GOV.UK). Button as wide as its label, left-aligned. |
| Summary list | Each key above its value | Key and value side by side (10 rem key column) once the container is 32 rem or wider |
| Actions | Stacked, full width, 44 px or taller, description above each | Same order, buttons `width: auto`, left-aligned |
| Dialogs | confirm-dialog.md: stacked, full width | Same |

Nothing is sticky. 200% text and 320 px: everything wraps, and nothing truncates (SC 1.4.4, 1.4.10, 1.4.12).

### Exact copy

| Where | Text | Mark |
|---|---|---|
| List `<h1>` | Users | V (4.0, 4.11) |
| List primary | Invite user | V |
| Row status line | `Invited · Worker` / `Active · Manager` / `Deactivated · Worker` | V words (4.11, 2) |
| Deactivated section heading | Deactivated | V word |
| List loading | Loading users… | P |
| List load failed | Couldn't load the users. Check your connection and try again. · Try again | P (same shape as participant-list-rows.md) |
| Back link (Invite, detail) | Users | P |
| Invite `<h1>` | Invite user | V |
| Name label / hint | Full name / Shown on the notes they write | P (label from GOV.UK) |
| Email label | Email address | P (GOV.UK) |
| Email hint (Invite) | Grow2Notes will email them a setup link. It works once and lasts 7 days. | P, from V "The email contains a setup link that works once and lasts 7 days." |
| Role legend | Role | V |
| Radio labels | Worker · Manager | V |
| Worker hint | Writes notes and reads past notes | P (from §2) |
| Manager hint | Can also review flagged notes, download reports, and set up participants and users | P (from §2) |
| Invite button / busy | Send invite / Sending… | V / S |
| Field errors | As form-validation.md "Users (4.11)": "Enter the person's name", "Name must be 100 characters or less", "Enter an email address", "Enter an email address in the correct format, like name@example.com", "Another account already uses this email address. Use a different one, or find the person in the users list.", "Choose Worker or Manager" | S |
| Invite network error | Not sent: no connection. Try again. | S (primary-actions.md) |
| Invite success (banner on list) | Invite sent to alex.park@example.org | S |
| Summary keys | Email address · Role · Status | P |
| Edit | Edit | V |
| Resend invite description | Sends a new setup link. The old link stops working. | P (6.6, 8.1) |
| Resend invite / busy | Resend invite / Sending… | V / S |
| Resend inline status | Setup link sent to alex.park@example.org at 4:12 pm. The old link no longer works. | P |
| Reset sign-in description | For a lost phone, a new phone or a forgotten password. Check who is asking first. | P (from 8.6 and 4.11) |
| Reset sign-in | Reset sign-in | V |
| Deactivate description | For someone who no longer works here. Their notes stay. | P (from the V dialog body) |
| Deactivate | Deactivate | V |
| Reactivate description | Lets them sign in again. | P |
| Reactivate / busy | Reactivate / Reactivating… | V / S |
| Reactivate success | Alex Park is reactivated. They can sign in again. | P |
| Deactivate dialog | Title "Deactivate Alex Park?" · body "Alex Park will be signed out everywhere now and can't sign in. Their notes stay." · confirm "Deactivate Alex Park" · "Go back" · success "Alex Park is deactivated." | S, body V |
| Deactivate dialog, **Invited** person | Body: "Alex Park hasn't set up their account yet. Their setup link will stop working." | P (the V body assumes an Active person; see Points left open) |
| Deactivate dialog, **yourself** | Title "Deactivate your own account?" · body "You will be signed out everywhere now and can't sign in again. Your notes stay." · confirm "Deactivate my account" | P |
| Reset sign-in dialog | As confirm-dialog.md §4: title "Reset sign-in for Alex Park?", the check-who-is-asking line, the email line, confirm "Reset sign-in for Alex Park", success "Sign-in reset. A new setup link has been emailed to alex.park@example.org." | S |
| Reset sign-in dialog, **yourself** | Title "Reset your own sign-in?" · body "You will be signed out everywhere now. A new setup link will be emailed to sam.lee@example.org." · confirm "Reset my sign-in" | P |
| Last-manager notice (detail) | You're the only active manager, so you can't reset your own sign-in, deactivate your account or make yourself a worker. To do any of these, first make someone else a manager. | P (rule V, A26) |
| Last-manager notice (edit) | You're the only active manager, so you can't change your email or make yourself a worker. First make someone else a manager. | P |
| Last-manager field errors | "[Name] is the only active manager. Make someone else a manager first." (Role) · "[Name] is the only active manager, so their email can't be changed here. Make someone else a manager first." (Email) | S (form-validation.md) |
| Last-manager in a dialog (race) | "Alex Park is the only active manager, so they cannot be deactivated." / "…so their sign-in cannot be reset here." | S (confirm-dialog.md) |
| Stale action (`412`) | Someone else has just changed Alex Park's account. The page now shows the latest details. | P (adapted from confirm-dialog.md) |
| Action network error | Not sent: no connection. Try again. (Resend) · Not reactivated: no connection. Try again. (Reactivate) | S pattern |
| Edit back link / `<h1>` | Alex Park / Edit user | P |
| Email hint (Edit) | Changing the email resets sign-in and sends a setup link to the new address. | V |
| Email hint (Edit, yourself) | Changing your email resets your sign-in. You'll be signed out, and a setup link will be sent to the new address. | P |
| Edit button / busy | Save / Saving… | S |
| Edit success | Changes saved · Changes saved. Sign-in has been reset and a setup link has been emailed to alex.p@example.org. | S / P |
| Own role → Worker | Changes saved. You are now a worker. (banner on Today) | P |

Names and emails appear exactly as stored, in normal case. Times are Melbourne time ("4:12 pm"). No name or email goes
into a URL, a query string, router `state` or a page title (4.0).

### Accessibility

**Semantics**
- List: `<ul role="list">` > `<li>` > `<Link>` (name only) + `<div id>` detail, referenced by the link's
  `aria-describedby`. The `·` separator is `aria-hidden`, with a visually hidden ", " so readers pause between parts.
  The chevron is `aria-hidden`. The Deactivated list sits under `<h2>Deactivated</h2>`.
- Forms: `<form noValidate>`. Every input has a `<label for>`. Hints and errors go in `aria-describedby`.
  `aria-invalid="true"` is set only while in error. Role is `<fieldset>` + `<legend>Role</legend>`, with each radio's
  hint linked by `aria-describedby` on that radio.
- Detail: `<h1>` name, then a `<dl>` with `<div>` groups (valid HTML, needed for row borders). Actions are `<button
  type="button">`, and Edit is a `<Link>` styled as a button (primary-actions.md). Each description `<p>` is linked to
  its button by `aria-describedby`.
- **ARIA used:** `aria-describedby`, `aria-invalid`, `aria-disabled` (busy only), `aria-hidden` (decoration),
  `role="list"` (Safari), `role="status"` (inline status, list loading) and `role="alert"` (an always-present action
  error container). No `aria-label` on buttons with visible text, no `role="menu"`, no `grid` or `table` roles.

**Keyboard**
- List: one Tab stop per row. Enter opens it. No arrow keys (not a composite widget).
- Invite / Edit: back link → Full name → Email address → Role (one Tab stop; arrows move and select, which is native) →
  Send invite / Save. Enter in a text field submits the form (implicit submission). That is safe, because Send invite
  only sends what is on screen and is validated first.
- Detail: back link → Edit → Resend invite or Reset sign-in → Deactivate or Reactivate. Dialog keys are owned by
  confirm-dialog.md (focus on the title, Escape and Back close, focus returns to the trigger).

**What screen readers hear** (expected; confirm in testing)
- Row: "Alex Park, link", then the description "alex.park@example.org, Invited, Worker".
- Radio: "Worker, radio button, checked, 1 of 2, Writes notes and reads past notes", inside "Role, group".
- After Send invite: on the list, focus is on the banner: "Invite sent to alex.park@example.org".
- After Resend invite: the busy label through the page `role="status"`, then the inline status sentence. Focus stays on
  the button.
- After Deactivate, Reset sign-in or Reactivate: the dialog closes and focus goes to the success banner above the
  `<h1>`, which is read out. The next swipe reads the name.
- Failed Send or Save: focus on the error summary, which lists the errors as links.

**Forced colours:** the radio's outline and dot are drawn with `border` in `currentColor`, so they survive. Row dividers
and summary-list borders are real borders. Busy buttons get `GrayText` (primary-actions.md).

**WCAG 2.2 criteria met:** 1.3.1 Info and Relationships · 1.3.2 Meaningful Sequence · 1.4.1 Use of Color (status in
words, radio state by shape) · 1.4.3 Contrast (Minimum) · 1.4.4 Resize Text · 1.4.10 Reflow (wrapping emails) · 1.4.11
Non-text Contrast (field borders, radio circle, focus) · 1.4.12 Text Spacing · 2.1.1 Keyboard · 2.4.2 Page Titled ·
2.4.3 Focus Order · 2.4.4 Link Purpose (In Context) · 2.4.6 Headings and Labels · 2.4.7 Focus Visible · 2.4.11 Focus
Not Obscured (nothing sticky) · 2.5.3 Label in Name · 2.5.8 Target Size (44 px or more) · 3.2.2 On Input (role commits
on Save) · 3.2.4 Consistent Identification · 3.3.1 Error Identification · 3.3.2 Labels or Instructions · 3.3.3 Error
Suggestion · 3.3.4 Error Prevention (Reset sign-in and Deactivate confirmed; Edit and Invite checked; Reactivate
reversible) · 3.3.7 Redundant Entry (values kept after errors; Edit prefilled) · 4.1.2 Name, Role, Value · 4.1.3
Status Messages. **1.3.5 Identify Input Purpose does not apply:** the fields describe another person, so they use
`autocomplete="off"`.

### Implementation notes (React 19, native HTML, CSS Modules)

- **No React Aria.** Links, a list, a `<dl>`, native radios and native `<dialog>` cover everything.
- **No form library** (design.md §7). Controlled inputs with `useState`, `onSubmit` + `preventDefault()`, one
  `validateUser()` shared by Invite and Edit, and `useMutation` for the request. Don't use React 19 `<form action>`:
  it resets uncontrolled fields after success and adds a second pending system (primary-actions.md).
- **One query:** `['admin', 'users']` from `GET /api/admin/users` (lists are returned whole, 6.1). The detail and edit
  pages `select` one user from it, with `staleTime: 0` so they refetch on mount. There is no `GET /api/admin/users/{id}`
  in 6.6, and none is needed.
- **`If-Match`:** every `PUT` and state-changing `POST` on a user needs the user's `ConcurrencyStamp` as an ETag (6.6).
  The list response in 6.6 doesn't show that field. It needs one (called `etag` below). See Points left open.
- **Mutations:** `retry: 0` for the user actions (the person retries), and network-only retries elsewhere, as in
  form-validation.md. Map the problem `code` to the copy table.
- **Inputs:** Full name `type="text" autocomplete="off" spellCheck={false} autoCapitalize="words"`. Email `type="email"
  autocomplete="off" spellCheck={false} autoCapitalize="none" autoCorrect="off"`. Don't use `maxLength`: it cuts off
  pasted text without saying so, so length is checked on submit instead. Some browsers may still offer saved-profile
  suggestions despite `autocomplete="off"` (unverified). That is harmless, because the manager has to pick one.
- **Messages travel in memory** (the status-messages.md store), never in router `state`, the URL or storage (D22).

```ts
// src/manage/users/userRules.ts
export type Role = 'Worker' | 'Manager';
export type Status = 'Invited' | 'Active' | 'Deactivated';
export type AdminUser = {
  id: string; email: string; displayName: string; role: Role; status: Status;
  etag: string; // ConcurrencyStamp; not yet in the 6.6 list shape
};

const collator = new Intl.Collator('en-AU', { sensitivity: 'base' });
export const byName = (a: AdminUser, b: AdminUser) => collator.compare(a.displayName, b.displayName);

export function isOnlyActiveManager(users: AdminUser[], id: string) {
  const active = users.filter((u) => u.role === 'Manager' && u.status === 'Active');
  return active.length === 1 && active[0].id === id;
}

/** The actions 4.11 allows for this person right now. The server is still the gate (409 user.last_manager). */
export function allowedActions(u: AdminUser, onlyManager: boolean) {
  return {
    resendInvite: u.status === 'Invited',
    resetSignIn: u.status === 'Active' && !onlyManager,
    deactivate: u.status !== 'Deactivated' && !onlyManager,
    reactivate: u.status === 'Deactivated',
  };
}

export function validateUser(v: { displayName: string; email: string; role: Role | null },
                             original?: AdminUser, onlyManager = false) {
  const e: Partial<Record<'displayName' | 'email' | 'role', string>> = {};
  const name = v.displayName.trim(), email = v.email.trim();
  if (!name) e.displayName = "Enter the person's name";
  else if (name.length > 100) e.displayName = 'Name must be 100 characters or less';
  if (!email) e.email = 'Enter an email address';
  else if (!/^[^\s@]+@[^\s@]+$/.test(email))
    e.email = 'Enter an email address in the correct format, like name@example.com';
  if (!v.role) e.role = 'Choose Worker or Manager';
  if (original && onlyManager) {                       // A26, checked before sending
    if (v.role === 'Worker')
      e.role = `${original.displayName} is the only active manager. Make someone else a manager first.`;
    if (email.toLowerCase() !== original.email.toLowerCase())
      e.email = `${original.displayName} is the only active manager, so their email can't be changed here. Make someone else a manager first.`;
  }
  return e;
}
```

```tsx
// src/manage/users/RoleRadios.tsx
import { useId } from 'react';
import type { Role } from './userRules';
import styles from './RoleRadios.module.css';

const ROLES: { value: Role; hint: string }[] = [
  { value: 'Worker', hint: 'Writes notes and reads past notes' },
  { value: 'Manager', hint: 'Can also review flagged notes, download reports, and set up participants and users' },
];

export function RoleRadios({ value, onChange, error }:
  { value: Role | null; onChange: (r: Role) => void; error?: string }) {
  const id = useId();
  return (
    <fieldset id="role" className={error ? `${styles.group} ${styles.invalid}` : styles.group}
              aria-describedby={error ? `${id}-error` : undefined}>
      <legend className={styles.legend}>Role</legend>
      {error && (
        <p id={`${id}-error`} className={styles.error}>
          <span className="visually-hidden">Error: </span>{error}
        </p>
      )}
      {ROLES.map((r) => (
        <div key={r.value} className={styles.item}>
          <label className={styles.row}>
            <input type="radio" name="role" value={r.value} className={styles.radio}
                   checked={value === r.value} onChange={() => onChange(r.value)}
                   aria-describedby={`${id}-${r.value}-hint`} />
            <span>{r.value}</span>
          </label>
          <p id={`${id}-${r.value}-hint`} className={styles.hint}>{r.hint}</p>
        </div>
      ))}
    </fieldset>
  );
}
```

```tsx
// src/manage/users/UserActions.tsx (sketch: Reset sign-in and Resend invite; Deactivate and Reactivate follow the same shape)
import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { Button } from '../../ui/Button';
import { postWithEtag, problemCode } from '../../api';
import { formatTime } from '../../time';
import { allowedActions, type AdminUser } from './userRules';
import styles from './UserActions.module.css';

type Props = { user: AdminUser; onlyManager: boolean; isSelf: boolean;
               showBanner: (text: string) => void };   // page renders + focuses the banner above <h1>

export function UserActions({ user, onlyManager, isSelf, showBanner }: Props) {
  const qc = useQueryClient();
  const can = allowedActions(user, onlyManager);
  const [confirmReset, setConfirmReset] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [noLongerPossible, setNoLongerPossible] = useState(false);
  const [sent, setSent] = useState('');
  const [actionError, setActionError] = useState('');

  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'users'] });

  const reset = useMutation({
    mutationFn: () => postWithEtag(`/api/admin/users/${user.id}/setup-link`, user.etag),
    retry: 0,
    onSuccess: async () => {
      if (isSelf) { window.location.replace('/'); return; }   // own sessions just ended
      await refresh();
      setConfirmReset(false);
      showBanner(`Sign-in reset. A new setup link has been emailed to ${user.email}.`);
    },
    onError: (e) => {
      const code = problemCode(e);
      if (code === 'user.last_manager') {
        setDialogError(`${user.displayName} is the only active manager, so their sign-in cannot be reset here.`);
        setNoLongerPossible(true);
      } else if (code === 'precondition.failed') {
        setDialogError(`Someone else has just changed ${user.displayName}'s account. The page now shows the latest details.`);
        setNoLongerPossible(true);
        refresh();
      } else setDialogError(`${user.displayName}'s sign-in was not reset. Check your connection, then try again.`);
    },
  });

  const resend = useMutation({
    mutationFn: () => postWithEtag(`/api/admin/users/${user.id}/setup-link`, user.etag),
    retry: 0,
    onMutate: () => { setSent(''); setActionError(''); },
    onSuccess: async () => {
      await refresh();
      setSent(`Setup link sent to ${user.email} at ${formatTime(new Date())}. The old link no longer works.`);
    },
    onError: () => setActionError('Not sent: no connection. Try again.'),
  });

  return (
    <div className={styles.actions}>
      <div role="alert" className={styles.error}>{actionError}</div>
      <div className={styles.group}>
        <Button as="link" to="edit" variant="secondary">Edit</Button>
        {can.resendInvite && (<>
          <p id="resend-desc" className={styles.desc}>Sends a new setup link. The old link stops working.</p>
          <Button variant="secondary" pending={resend.isPending} pendingLabel="Sending…"
                  aria-describedby="resend-desc" onClick={() => resend.mutate()}>Resend invite</Button>
          <p role="status" className={styles.status}>{sent}</p>
        </>)}
        {can.resetSignIn && (<>
          <p id="reset-desc" className={styles.desc}>
            For a lost phone, a new phone or a forgotten password. Check who is asking first.
          </p>
          <Button variant="secondary" aria-describedby="reset-desc"
                  onClick={() => { setDialogError(null); setNoLongerPossible(false); setConfirmReset(true); }}>
            Reset sign-in
          </Button>
        </>)}
      </div>
      {/* group 2 (Deactivate / Reactivate) or the last-manager notice goes here, after a divider */}

      {confirmReset && (
        <ConfirmDialog
          tone="warning"
          title={isSelf ? 'Reset your own sign-in?' : `Reset sign-in for ${user.displayName}?`}
          confirmLabel={isSelf ? 'Reset my sign-in' : `Reset sign-in for ${user.displayName}`}
          pending={reset.isPending} pendingText="Resetting sign-in…"
          error={dialogError} canConfirm={!noLongerPossible}
          onConfirm={() => reset.mutate()} onClose={() => setConfirmReset(false)}>
          {isSelf
            ? <p>You will be signed out everywhere now. A new setup link will be emailed to {user.email}.</p>
            : <>
                <p>Only do this after you have checked, by phone or in person, that {user.displayName} is the one asking.</p>
                <p>Their current way of signing in will be removed, and they will be signed out everywhere.
                   A new setup link will be emailed to {user.email}.</p>
              </>}
        </ConfirmDialog>
      )}
    </div>
  );
}
```

Notes on the sketch: the `role="status"` and `role="alert"` containers are always rendered, empty until needed (a live
region must exist before text is written into it). The `id`s are fixed because there is one user per page; use `useId`
if the component is ever repeated. `Button` is the shared component from primary-actions.md, including its `as="link"`
form for Edit.

```css
/* RoleRadios.module.css: same box size and drawing technique as the tick list (checkbox-list.md) */
.group { border: 0; margin: 0; padding: 0; min-inline-size: 0; }
.invalid { border-inline-start: 4px solid var(--colour-error); padding-inline-start: 0.75rem; }
.legend { font-weight: 700; padding: 0; margin-block-end: 0.5rem; }
.item + .item { margin-block-start: 0.5rem; }
.row { display: flex; align-items: center; gap: 0.75rem; min-block-size: 2.75rem; cursor: pointer; }
.radio {
  appearance: none; margin: 0; flex: none; box-sizing: border-box;
  inline-size: 2.5rem; block-size: 2.5rem;            /* 40 px; whole label row is 44 px or taller */
  border: 2px solid currentColor; border-radius: 50%;
  background: var(--colour-surface);
  display: grid; place-content: center;
}
.radio::before {                                       /* dot drawn with a border: survives forced colours */
  content: ''; inline-size: 0; block-size: 0;
  border: 0.625rem solid currentColor; border-radius: 50%;
  opacity: 0;
}
.radio:checked::before { opacity: 1; }                 /* no transition */
.radio:focus-visible { outline: 3px solid var(--colour-focus); outline-offset: 2px; }
.hint { margin: 0; padding-inline-start: 3.25rem; color: var(--colour-text-secondary); }  /* 4.5:1 or more */
@media (hover: hover) { .item:hover { background: var(--colour-row-hover); } }

/* UserDetail.module.css (summary list and wrapping) */
.summary { margin: 0; border-block-start: 1px solid var(--colour-border); container-type: inline-size; }
.summary > div { padding-block: 0.75rem; border-block-end: 1px solid var(--colour-border); }
.summary dt { font-weight: 700; }
.summary dd { margin: 0; overflow-wrap: anywhere; }   /* long emails wrap (SC 1.4.10, C33) */
@container (min-width: 32rem) {
  .summary > div { display: grid; grid-template-columns: 10rem 1fr; gap: 1rem; }
}
.divider { border: 0; border-block-start: 1px solid var(--colour-border); margin-block: 1.5rem; }
```

`overflow-wrap: anywhere` is used rather than C33's `break-word`, because `anywhere` also shrinks the min-content width
inside flex and grid items, which is where the email sits.

---

## Per-screen notes

**4.11 Users list**
- No search, filter, sort control, count or "last signed in" column. None is in 4.11, and at about 20 rows the list
  fits on two phone screens.
- The two sections are a layout choice, not a filter. If the owner prefers one flat list, drop the `<h2>` and sort
  everyone by name. Nothing else changes.
- Don't mark the viewer's own row ("(you)"). It is not in the design, and the detail page's copy covers the self cases.

**4.11 Invite user**
- **Worker is preselected.** GOV.UK says don't preselect, and its concern is people missing the question. Here the
  default is the least-privilege choice: if a manager forgets to change it, the new person gets less access, not more,
  and the list row shows "Invited · Worker" straight away. Most invites are workers (fewer than 20 workers, "a few"
  managers, D2, D24). AgDS allows a default where it makes sense, and NN/g recommends one. [Opinion, weighing these]
- No "Confirm email address" field. GOV.UK: only if research shows it works. The person's setup link going to a
  mistyped address is fixed with Edit, which sends a new link.
- No Cancel button. The back link and browser Back are the way out (GOV.UK question pages; primary-actions.md).
- The invite email's wording is out of scope for this file. It must say Grow2Notes only and contain no participant data
  (D42, 6.6).

**4.11 User detail**
- No invited, activated or deactivated dates. They are in the API but not in 4.11. The setup link page already tells an
  invitee with an expired link to "Ask a manager to send a new one" (4.1), which leads to Resend invite.
- The descriptions above Reset sign-in and Deactivate are short "what this is for" lines, not warnings. The full
  consequences are in the dialog, at the moment of commitment. Keeping the page lines plain avoids the habituation
  problem of repeated warnings.
- Reset sign-in's dialog starts with the identity check, because a reset is the path someone would use to take over an
  account (8.6, confirm-dialog.md).

**4.11 Edit user**
- The email consequence is shown as the field's hint **before** Save (design sentence, V), and repeated in the success
  banner after. design.md specifies no confirmation for an email change, so none is added. See Points left open.
- Role changes take effect on the person's next request (6.6). No copy is needed for someone else. For yourself, see
  Behaviour.

**Dialogs (confirm-dialog.md)**
- Deactivate: **standard** tone, because Reactivate exists (GOV.UK: warning only when it "cannot be easily undone").
  Reset sign-in: **warning** tone, because it cannot be undone.
- On success the trigger is gone, so focus goes to the success banner above the `<h1>`, not to `<body>`.

### Points design.md leaves open (gaps, not conflicts)

1. **Email change has no confirmation** (4.11) although it resets sign-in, which cannot be undone. This file relies on
   the always-visible hint (V) plus the success banner. SC 3.3.4 is arguably met by "Checked" (the form is validated
   before it is saved). Owner to confirm. confirm-dialog.md raises the same point.
2. **Deactivated person + Edit email.** 4.11 lets Edit change any user's email, and an email change "resets sign-in and
   sends a setup link". For a Deactivated person, that would send a setup link to someone who can't sign in. If the
   reset also sets `Status = Invited` (8.6), it would undo the deactivation without Reactivate. The UI follows whatever
   the API decides.
3. **Reactivating someone who never set up.** 6.6 says a reactivated user "signs in with their existing methods". A
   person deactivated while Invited has none. Should Reactivate return them to Invited (with Resend invite), or to
   Active?
4. **Deactivate copy for an Invited person.** The design's body ("signed out everywhere … Their notes stay") assumes an
   Active person. A variant is proposed above.
5. **ETag in the user list.** 6.6 requires `If-Match` from `ConcurrencyStamp`, but the `GET /api/admin/users` shape has
   no such field. Add one per user.
6. **Page titles.** app-shell-nav.md's fixed list of page names has "Users" and "User" but no "Invite user" or "Edit
   user". Add both, or use "User" for them.
7. **What managers type as the name.** Today shows "Alex P." (4.2) and version history shows "Sam Lee" (4.5) for the same
   `displayName`. This file labels the field "Full name". If Today should abbreviate, that belongs to Today's formatter,
   not to what the manager types.
8. **Differences between sibling files**, resolved here as stated:
   - Deactivate confirm tone: confirm-dialog.md says standard; primary-actions.md says warning. **This file: standard.**
   - Dialog initial focus: confirm-dialog.md says the title; primary-actions.md says Go back. **This file: the title**
     (the dialog component owns it).
   - Last-manager and email-in-use wording: three versions exist. **This file: form-validation.md for fields,
     confirm-dialog.md for dialogs.** primary-actions.md's "There must always be at least one active manager." and "This
     email address already has an account." should be dropped.
   - After Send invite: primary-actions.md lands on the new user's detail page. **This file: the list** (repeat invites
     at go-live; no need for the new user's ID in the `201` response).
   - Focus after Deactivate or Reset: the status line (primary-actions.md), the `<h1>` plus status region
     (confirm-dialog.md), or the focused success banner (status-messages.md). **This file: the focused success banner.**

---

## Anti-patterns to avoid

- **A `<table>` on phones restyled with `display: block`,** or a table on laptops whose rows are clickable. The first
  loses table semantics (Roselli). The second announces every cell as clickable.
- **Truncating email addresses with an ellipsis,** or letting them push the page sideways at 320 px.
- **Greying out or fading Deactivated rows.** It fails contrast and reads as disabled (status-tags.md).
- **Colour-coded status pills** (green Active, red Deactivated). Status is a word, and red is reserved for errors.
- **Disabled Deactivate or Reset sign-in buttons for the last manager,** with the reason only in a tooltip or `title`.
  Phones don't hover, and a disabled button drops out of the tab order.
- **Showing the Deactivate button and letting it fail** with `409 user.last_manager` when the client already knows the
  answer.
- **"Are you sure?" with Yes/No,** or a confirm button that doesn't name the person.
- **Red on the page triggers,** or a red button for Deactivate, which can be undone. Red is for the final Reset sign-in
  confirmation (and Discard draft) only.
- **Confirmation dialogs for Reactivate, Resend invite, Save or Send invite.** Each one teaches managers to click
  through the dialogs that matter.
- **Inline actions in list rows** (a Deactivate button on each row, or swipe-to-deactivate). A row has one action. A row
  link cannot contain buttons (HTML), and serious actions belong on the detail page, away from routine ones (NN/g).
- **The invite form in a modal dialog** on a phone.
- **`autocomplete="email"` or `"name"`** on the invite fields. The browser would offer the manager's own details.
- **`<select>` for a two-option role,** or radios laid out side by side with hints.
- **Changing the role or anything else on change** (`onChange` that saves). The role commits on Save (SC 3.2.2).
- **Toasts** for "Invite sent" or "Deactivated". Use the success banner or the inline status (status-messages.md).
- **Clearing the form** after a failed Send or Save, or after a `412` (SC 3.3.7).
- **Putting a user's email or name in the URL,** a query string, router `state` or the page title.
- **Adding features nobody asked for:** a search box, bulk invite, CSV import, role descriptions pages, "last signed in",
  invite expiry countdowns, an audit trail on the page, or hard delete. None is in 4.11, and A28 already logs every
  user change for the operator.

---

## Tensions with decisions

No conflict with D1–D43 was found.

One recorded for the future, against an assumed default rather than a decision: **A27 (one email address per account
across the whole app) with D3 (kept SaaS-ready).** If the product ever serves a second organisation, `409
user.email_in_use` would tell a manager in one organisation that an address already has an account in another. OWASP
calls responses like "This user ID is already in use" a user-enumeration risk and recommends identical responses
whether or not the account exists. https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
Today there is one organisation, invites come only from trusted managers, and A27 already notes the multi-provider
case. No change is recommended.

---

## Sources

Standards
- WCAG 2.2 Understanding 1.3.5 Identify Input Purpose: https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html
- WCAG 2.2 Understanding 1.4.10 Reflow: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
- WCAG 2.2 Technique C33 (long strings): https://www.w3.org/WAI/WCAG22/Techniques/css/C33
- WCAG 2.2 Technique H71 (fieldset and legend): https://www.w3.org/WAI/WCAG22/Techniques/html/H71
- WCAG 2.2 Understanding 3.2.2 On Input: https://www.w3.org/WAI/WCAG22/Understanding/on-input.html
- WCAG 2.2 Understanding 3.3.4 Error Prevention: https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html
- WCAG 2.2 Understanding 4.1.3 Status Messages: https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
- WAI-ARIA APG Radio Group pattern: https://www.w3.org/WAI/ARIA/apg/patterns/radio/
- HTML Living Standard, email state value sanitisation: https://html.spec.whatwg.org/multipage/input.html#email-state-(type=email)
- MDN `<input type="email">`: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/input/email

Research and research-based guidance
- NN/g, Mobile Tables: https://www.nngroup.com/articles/mobile-tables/
- NN/g, Checkboxes vs. Radio Buttons: https://www.nngroup.com/articles/checkboxes-vs-radio-buttons/
- NN/g, Confirmation Dialogs Can Prevent User Errors (If Not Overused): https://www.nngroup.com/articles/confirmation-dialog/
- NN/g, Proximity of Consequential Options (Laubheimer, 2021): https://www.nngroup.com/articles/proximity-consequential-options/
- NN/g, Modal & Nonmodal Dialogs (Fessenden, 2017): https://www.nngroup.com/articles/modal-nonmodal-dialog/
- Anderson et al., CHI 2015, habituation to security warnings (fMRI): https://scholarsarchive.byu.edu/facpub/9306/

Design systems (convention)
- GOV.UK Radios: https://design-system.service.gov.uk/components/radios/
- GOV.UK Button (disabled and warning buttons): https://design-system.service.gov.uk/components/button/
- GOV.UK Summary list: https://design-system.service.gov.uk/components/summary-list/
- GOV.UK Table: https://design-system.service.gov.uk/components/table/
- GOV.UK Email addresses pattern: https://design-system.service.gov.uk/patterns/email-addresses/
- GOV.UK Names pattern: https://design-system.service.gov.uk/patterns/names/
- GOV.UK Warning text: https://design-system.service.gov.uk/components/warning-text/
- GOV.UK Notification banner: https://design-system.service.gov.uk/components/notification-banner/
- GOV.UK Question pages: https://design-system.service.gov.uk/patterns/question-pages/
- GOV.UK Back link: https://design-system.service.gov.uk/components/back-link/
- NHS digital service manual, Table: https://service-manual.nhs.uk/design-system/components/table
- Australian Government Design System (AgDS), Radio: https://design-system.agriculture.gov.au/components/radio
- GitLab Pajamas, Destructive actions: https://design.gitlab.com/usability/destructive-actions
- Primer, ActionList: https://primer.style/components/action-list/

Expert opinion
- Adrian Roselli, A Responsive Accessible Table: https://adrianroselli.com/2017/11/a-responsive-accessible-table.html
- Adrian Roselli, Block Links, Cards, Clickable Regions, Etc.: https://adrianroselli.com/2020/02/block-links-cards-clickable-regions-etc.html
- Scott O'Hara, "Fixing" Lists (Safari list semantics): https://www.scottohara.me/blog/2019/01/12/lists-and-safari.html
- Vitaly Friedman, Hidden vs. Disabled In UX (Smashing, 2024): https://www.smashingmagazine.com/2024/05/hidden-vs-disabled-ux/
- Victor Ponamariov, How To Manage Dangerous Actions In User Interfaces (Smashing, 2024): https://www.smashingmagazine.com/2024/09/how-manage-dangerous-actions-user-interfaces/
- Sandrina Pereira, Making Disabled Buttons More Inclusive (CSS-Tricks, 2021): https://css-tricks.com/making-disabled-buttons-more-inclusive/

Security
- OWASP Authentication Cheat Sheet (user enumeration): https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html

Project sources
- design.md §2, §3.7, §4.0, §4.1, §4.11, §5.3 (ApplicationUser), §5.4, §6.1, §6.6, §6.7, §6.9, §8.1, §8.5, §8.6, §13
  (A22–A28, A32); decisions.md D2, D3, D22–D24, D42.
- Sibling component files: confirm-dialog.md, primary-actions.md, form-validation.md, status-messages.md,
  status-tags.md, participant-list-rows.md, checkbox-list.md, app-shell-nav.md.
