# Grow2Notes UX specs

This folder turns [design.md](../design.md) and [decisions.md](../decisions.md) into buildable UX specs. It adds no
feature, screen, setting, notification or data. **Component files** (`components/`) each research one part of the
interface: the best practice with graded evidence ([Research], [Standard], [Convention], [Opinion]), then a
recommendation for Grow2Notes, with code sketches. **Screen files** (`screens/`) say exactly what to build on each
screen of design.md §4: layout on phone and laptop, components in order, every state with exact copy, focus and
announcements, an accessibility checklist and acceptance criteria. Build from the screen file and open the component
file for the evidence. Where a screen's "Conflicts resolved" table differs from a component file, **the screen wins**
(every component file says so at the top). Copy marked **(P)** is proposed and needs the owner's approval. The
editorial pass of 1 October 2026 settled the cross-screen rules below once, for every screen. The update of
3 October 2026 added common-item groups (D44–D47) across the note form, read view, version history, files and the
Common items screen; their assumed defaults (design.md A41–A47, with A2, A3, A4 and A6 extended) are stated in
[group-picker](components/group-picker.md) and can be overridden by the owner.

## Global conventions

| Area | The rule every screen follows | Owner file |
|---|---|---|
| Type | System font stack; body 18 px, nothing under 16 px; weights 400 and 700 only; h1 28 px (32 px laptop); sentence case, never upper case. | [foundations](components/foundations.md) |
| Colour | Light theme only (`color-scheme: light`, page darkening still allowed); text tokens at 7:1 or better; red only for errors and the two warning buttons; meaning never by colour alone. | [foundations](components/foundations.md) |
| Status words and tags | Lifecycle states are plain text (Draft, Submitted, Invited, Active, Deactivated). Exceptions are tags: **amber** with a 1 px `--colour-attention-edge` border for Flagged and To review; **neutral grey** for Reviewed, Edited, Past-day note. Every tag keeps its border in forced colours. Fixed order: Flagged → To review / Reviewed → Edited → Past-day note. | [status-tags](components/status-tags.md) |
| Spacing and layout | One breakpoint, `@media (min-width: 40rem)`; 16 px gutters on phones, 32 px from 40rem; a 40rem reading column (setup screens may use 60rem); rows that must adapt use wrapping flex lines, not container queries; nothing sticky except the note form's top bar. | [foundations](components/foundations.md) |
| Tap targets | 44 × 44 px floor (A32); buttons, inputs and nav links 48 px; list rows and tick rows 56 px; 8 px between separate targets. | [foundations](components/foundations.md) |
| Buttons | Three styles: primary, secondary, warning. Warning (red) on exactly two buttons: "Discard draft for [name]" and "Reset sign-in for [name]". Never `disabled`: `aria-disabled` plus a visible reason. Busy label = the button's own verb + "…": page buttons after **400 ms**, dialogs show a status line after **1 s**. Navigation is a link, never a button. | [primary-actions](components/primary-actions.md) |
| Dialogs | Native `<dialog>` + `showModal()`, focus on the title, safe button "Go back", stacked full-width buttons, no backdrop dismiss, no animation. Used only for Submit, Discard draft, Deactivate, Reset sign-in, Guide prompts' leave warning and the session warning. | [confirm-dialog](components/confirm-dialog.md) |
| Messages | Load failed: "[Thing] did not load: [cause]. Try again.", then "[Thing] still did not load: …". Action failed: "Not [done]: [cause]. Try again." Cause is "no connection" or "something went wrong", for loads and actions alike. Nothing for 1 s, then "Loading [things]…"; no spinners or skeletons. Field errors: inline above the field and in the error summary (first in `<main>`, above the `<h1>`). Request failures: the `role="alert"` line above the pressed button, focus stays on it. The **only** success banner is "Note for [name] submitted" on Today. Empty sentences only after the data arrives. No toasts. | [microcopy §9](components/microcopy.md), [empty-loading-error](components/empty-loading-error.md), [form-validation](components/form-validation.md) |
| Live regions | Each page renders one visually hidden `<p role="status">` (`PageStatus`) from its first render; the shared Button writes busy labels there through context; screens with their own status line opt out (`announceBusy={false}`). No global live region. Every live region exists before text is written into it. | [app-shell §3](screens/app-shell.md), [primary-actions](components/primary-actions.md) |
| Focus | One route-change rule in the shell: a `[data-route-focus]` banner or error summary wins on every arrival; otherwise, on Back/Forward only, the list's `handle.returnFocus` row; otherwise the `<h1>`. 3 px near-black `outline`, 2 px offset, `Highlight` in forced colours. | [app-shell](screens/app-shell.md), [foundations](components/foundations.md) |
| Back links | One shared BackLink in the shell's before-main bar slot: label is the destination's name ("‹ Today", "‹ Past notes", "‹ Users"); it pops when the previous entry is the destination, otherwise pushes. Where a screen was opened from is kept in memory, never in router state. | [app-shell §3a](screens/app-shell.md), [note-identity-header](components/note-identity-header.md) |
| Identity heading | On every page about one note: one `<h1>` with an optional caption, the participant's full name (never truncated or re-cased) and the note date. | [note-identity-header](components/note-identity-header.md) |
| Dates and times | Melbourne time from the server, never the device clock. `dateLong` "Thursday 1 October 2026" in sentences and headings; `dateToday` "Thursday 1 October" (Today header only); `datePlain` "1 October 2026" (date of birth, ranges); `time` "4:12 pm" (12:00 is "midday"/"midnight"); `dateTime` "Thu 1 Oct 2026, 4:12 pm" next to times on another day. Non-breaking spaces; no relative times. | [microcopy §3](components/microcopy.md) |
| Page titles | "Grow2Notes – [page name]" from one typed list; "Error: " in front after a failed submit; never a name or data. | [app-shell §3](screens/app-shell.md) |
| Device and data | Nothing stored on the device: no `localStorage`, `sessionStorage`, IndexedDB or `<ScrollRestoration>`; `history.state` holds only React Router's key and `inScreen`. Queries: one silent retry, 10 s timeout (30 s for the two file downloads), `refetchOnWindowFocus` off except the listed opt-ins. Mutations: `retry: 0`. | [app-shell](screens/app-shell.md), [empty-loading-error](components/empty-loading-error.md), [form-validation](components/form-validation.md) |
| Names | The parent company's name never appears anywhere (D42); a CI check reads it from a secret, never from committed source. | [microcopy §8](components/microcopy.md) |
| Common item groups | On the note form, "2. Common items" holds **Every note** (always first, never in the picker), then "Which of these happened?" with one tick per group, then each picked group's items under its name, in the manager's order. Picks start from the participant's last submitted note (D46) with a line naming its date; items never start ticked. Picking never moves focus or scrolls; unpicking clears that group's ticks with no confirmation. The read view, version pages, daily report and record export show Every note and the picked groups only, by name, each item ticked or not ticked (D47). Workers never see the word "group". No new screen, dialog, notification or setting. | [group-picker](components/group-picker.md) |

**Terminology** (full glossary in [microcopy §2](components/microcopy.md); one term per concept):

| Use | Never |
|---|---|
| participant | client, patient, resident |
| note, draft, submit / Submitted | entry, log; in progress; send, finish |
| Edit, Save changes, Cancel, Edited, version | Update, Amend; revision |
| Past notes, past-day note | history, timeline; late or backdated note |
| tick / ticked / not ticked (also for groups under "Which of these happened?") | check, checked, select; "pick" on screen |
| group (managers' Common items screen only), Every note | category, section, set, type; "group" on any worker screen |
| Flag for manager, Flagged; To review, Reviewed, Mark reviewed | escalate, alert; resolved, closed, actioned |
| discard (drafts), archive / Restore (participants, goals, items, groups), deactivate / Reactivate (users) | delete, remove (nothing is ever deleted) |
| sign in, sign-in, sign out; passkey; 6-digit code; setup link; Reset sign-in | log in, login, OTP, 2FA, magic link |
| Report, Download Word / PDF; Export record; Manage | view report, print; Admin, Settings |

## Screens

| Screen | What it covers |
|---|---|
| [app-shell](screens/app-shell.md) | The frame around every screen (4.0): header and nav, Flagged badge, page titles, before-main bar and BackLink, the route-change focus rule, start-up and whole-page messages, session warning and signing in again in place. |
| [sign-in](screens/sign-in.md) | Sign in by passkey or by email, password and 6-digit code; signed out in place; account setup from the emailed link (4.1). |
| [today](screens/today.md) | Home for everyone: every active participant with today's note status, unfinished drafts, Find a participant, the banner after Submit (4.2). |
| [note-form](screens/note-form.md) | Write, autosave, edit and submit one note, including ticking which common-item groups happened; conflicts and refusals; the Submit and Discard confirmations; owns the note URLs (Routes) (4.3). |
| [participant-notes](screens/participant-notes.md) | Past notes for one participant and the read view of one note, including the manager's review panel, the same on every route (4.4, 4.6). |
| [version-history](screens/version-history.md) | Managers' list of a note's versions and one version in full (4.5). |
| [flagged](screens/flagged.md) | Managers' To review and Reviewed lists, the count, and the way back to the list (4.6). |
| [daily-report](screens/daily-report.md) | Choose a day and download its notes as Word or PDF; nothing shown on screen (4.7). |
| [participants](screens/participants.md) | Participants list, Add participant, participant detail with goals, Archive and Restore, Write past-day note (4.8). |
| [common-items](screens/common-items.md) | The organisation's common items in groups, Every note first: add, rename, reorder, archive and restore groups; add, reword, reorder, move between groups, archive and restore items (4.9). |
| [guide-prompts](screens/guide-prompts.md) | The guide prompts field with Save and the leave-without-saving warning (4.10). |
| [users](screens/users.md) | Users list, Invite user, user detail actions and Edit user (4.11). |
| [record-export](screens/record-export.md) | Export one participant's record for a date range as PDF or Word (4.12). |

## Components

| Component | What it covers | Used by |
|---|---|---|
| [foundations](components/foundations.md) | Type, colour tokens, spacing, targets, the breakpoint, focus ring, light theme, motion | every screen |
| [microcopy](components/microcopy.md) | Glossary, voice rules, date formats, message patterns, canonical wording (§9) | every screen |
| [empty-loading-error](components/empty-loading-error.md) | Loading, empty and failed states, `LoadRegion`, whole-page messages, the `api()` wrapper | every screen |
| [primary-actions](components/primary-actions.md) | Button styles, busy and unavailable states, the shared `Button` | every screen with actions |
| [form-validation](components/form-validation.md) | When to validate, error summary, inline errors, request failures, retries, radio pre-selection | note-form, sign-in, participants, users, record-export, common-items, guide-prompts |
| [app-shell-nav](components/app-shell-nav.md) | Header, nav, account menu, titles (superseded in places by the app-shell screen) | app-shell |
| [session-timeout](components/session-timeout.md) | 28-minute warning, signed out in place, signing in again | app-shell |
| [notification-badge](components/notification-badge.md) | The Flagged count on the nav item | app-shell, today, flagged |
| [status-messages](components/status-messages.md) | The Submit banner, notices and the in-memory message store | today, note-form, participants, users |
| [status-tags](components/status-tags.md) | Status words and the one tag style | today, participant-notes, flagged, version-history, users |
| [participant-list-rows](components/participant-list-rows.md) | Whole-row links with status lines | today, participants, users |
| [search-filter](components/search-filter.md) | Find a participant, filtering as you type | today, participants |
| [note-identity-header](components/note-identity-header.md) | Identity heading, BackLink, the note form's top bar | note-form, participant-notes, version-history, flagged, participants |
| [checkbox-list](components/checkbox-list.md) | Goal and common-item tick rows (form and read-only); the drawn box reused for radios | note-form, participant-notes, version-history, users, record-export, common-items |
| [group-picker](components/group-picker.md) | Common-item groups: Every note, "Which of these happened?", the picked groups' lists, picks copied from the last note (D46), what the read view and files show (D47), and the Common items screen's grouping; the shared assumed defaults | note-form, participant-notes, version-history, daily-report, record-export, common-items |
| [guided-notes-textarea](components/guided-notes-textarea.md) | The Guided notes box with guide prompts | note-form |
| [conditional-reveal](components/conditional-reveal.md) | Flag for manager with Reason; the character count | note-form, participant-notes |
| [autosave-status](components/autosave-status.md) | Autosave engine, save indicator, takeover and conflicts | note-form |
| [confirm-dialog](components/confirm-dialog.md) | Confirmation dialogs | note-form, participant-notes, users, guide-prompts |
| [note-read-view](components/note-read-view.md) | Submitted note read view | participant-notes, flagged, version-history |
| [review-panel](components/review-panel.md) | Mark reviewed with an optional comment | participant-notes (opened from flagged) |
| [chronological-list](components/chronological-list.md) | Date-ordered lists with Show older | participant-notes, flagged, version-history |
| [version-history](components/version-history.md) | Version list and one version | version-history |
| [tabs-segmented](components/tabs-segmented.md) | To review / Reviewed and Active / Archived view links | flagged, participants |
| [date-navigation](components/date-navigation.md) | Daily report date field and day links; past-day date | daily-report, participants, record-export |
| [file-download](components/file-download.md) | Word/PDF download buttons and status line | daily-report, record-export |
| [export-form](components/export-form.md) | Record export form | record-export |
| [list-editor](components/list-editor.md) | Ordered list editor with move, edit, archive, restore (one per group on Common items) | participants (goals), common-items |
| [text-and-date-inputs](components/text-and-date-inputs.md) | Participant name and date-of-birth inputs | participants, users |
| [settings-textarea](components/settings-textarea.md) | Guide prompts editor and the leave guard | guide-prompts, app-shell (sign out) |
| [user-management](components/user-management.md) | Users list, invite and user actions | users |
| [sign-in-form](components/sign-in-form.md) | Sign-in and setup forms | sign-in |
| [passkey-flows](components/passkey-flows.md) | Passkey setup and sign-in | sign-in |
| [totp-setup](components/totp-setup.md) | Authenticator setup and code entry | sign-in |

## Tensions with decisions

Gathered from the component and screen files. Each is recorded with its evidence only; **no change is
recommended**, and every spec builds the decision as written. Details and links are in the named file.

1. **Guide prompts as placeholder text that disappears on typing, up to 1,000 characters (D12, D34).** GOV.UK, NHS
   and NN/g advise against guidance in placeholders (memory strain, mistaken for filled text); the HTML spec means a
   placeholder as "a short hint". ([guided-notes-textarea](components/guided-notes-textarea.md))
2. **One optional tick per item (D7, D10, A4), and unpicked groups left out (D47).** An unticked box cannot tell "no"
   from "missed"; GOV.UK added a "none" option after research. A group missing from the read view or a file cannot
   show "did not happen" apart from "forgot to tick it". ([checkbox-list](components/checkbox-list.md),
   [group-picker](components/group-picker.md))
3. **Nothing stored on the device (D22).** Text typed during an outage is lost if the phone discards the tab; a first
   visit with no connection can only show an error; an unsent review comment does not survive a reload. Common mobile
   guidance keeps a local copy. ([autosave-status](components/autosave-status.md),
   [empty-loading-error](components/empty-loading-error.md), [review-panel](components/review-panel.md))
4. **Disabled controls (Submit while unsaved, downloads on an empty day, Next day on today; 3.4, 4.3, 4.7, A18).**
   GOV.UK, NHS and AgDS advise against disabled buttons; GOV.UK pagination hides an unusable next link. Built with
   `aria-disabled` and a stated reason. ([primary-actions](components/primary-actions.md),
   [date-navigation](components/date-navigation.md))
5. **A confirmation on every Submit (3.9, D39).** Warnings lose effect from the second exposure (Anderson et al., CHI
   2015); a passive check is weaker than re-entering the identifier (Adelman 2013). ([confirm-dialog](components/confirm-dialog.md),
   [note-identity-header](components/note-identity-header.md))
6. **Name only, no second identifier or photo (A5, scope).** SAFER 1.3: names alone are not sufficient; one
   observational study linked a banner photo to fewer wrong-patient orders. ([note-identity-header](components/note-identity-header.md))
7. **Cancel discards a pending edit without confirmation (3.5, A10).** SC 3.3.4 arguably covers stored working copies.
   ([confirm-dialog](components/confirm-dialog.md))
8. **Every worker reads every note, including flag reasons (D20).** The OAIC advises need-to-know internal access.
   ([note-read-view](components/note-read-view.md))
9. **Native date fields (4.7, 4.8, 4.12).** GOV.UK and NHS avoid `type="date"`; 2019 testing found Dragon and iOS
   VoiceOver problems; iOS ignores `min`/`max`. ([date-navigation](components/date-navigation.md))
10. **Long synchronous exports with no progress measure (§7.1, M5).** NN/g recommends percent-done feedback past 10 s.
    ([export-form](components/export-form.md))
11. **Exported files on personal phones (D21, §12).** iPhone downloads may default to a personal iCloud Drive
    (unverified); the app cannot control it. ([file-download](components/file-download.md))
12. **Time limits without warning: the 12-hour absolute limit (A24) and the 30-minute setup window (4.1).** The SC 2.2.1
    Understanding document treats security limits as time limits. The 28-minute warning also does not say work is
    saved (Home Office guidance). ([session-timeout](components/session-timeout.md), [totp-setup](components/totp-setup.md))
13. **Sign-in choices (D23 via A22, A23, A25; 4.1).** FIDO lists passkey management as a required pattern and found
    passkey autofill most successful; Microsoft (vendor data) advises passkey-first with no chooser; NIST SP 800-63B-4
    requires a compromised-password blocklist; the generic A25 message is partly undone by the two-step flow.
    ([passkey-flows](components/passkey-flows.md), [sign-in-form](components/sign-in-form.md), [sign-in](screens/sign-in.md))
14. **Family name required, lists sorted by it (A5).** W3C Internationalization advises not requiring a family name.
    ([text-and-date-inputs](components/text-and-date-inputs.md))
15. **One email address per account across the app (A27) with a SaaS-ready product (D3).** OWASP calls "already in use"
    responses user enumeration. ([user-management](components/user-management.md))
16. **Wording in design.md:** app name first in page titles (HMRC and W3C examples put the page first); "flag" is
    figurative (COGA 4.4.4); three strings with "can't" or "Please" (GOV.UK style); "To review" is a verb-phrase tag;
    "Your unfinished drafts" also holds pending edits (NN/g consistency). ([microcopy](components/microcopy.md),
    [status-tags](components/status-tags.md), [participant-list-rows](components/participant-list-rows.md))
17. **Design text, not decisions:** version history without compare or restore (a convention elsewhere); "30 at a
    time, then Show older" is weak for finding old notes (NN/g). ([version-history](components/version-history.md),
    [chronological-list](components/chronological-list.md))
18. **Starting with the last note's group picks (D46).** GOV.UK and NHS advise against pre-selecting checkboxes, and
    defaults tend to stay (Johnson and Goldstein 2003). The line naming the source note's date follows copy-forward
    safe practice (Joint Commission), but a daily line fades (Anderson et al. 2015). ([group-picker](components/group-picker.md))
19. **Groups and their items on one page (D44, no new screens).** GOV.UK and NHS put multi-part follow-ups on a later
    page; GOV.UK's 2021 testing found multi-field reveals confusing, and conditional reveals remain a known WCAG 4.1.2
    gap. Lists placed after the whole question (AgDS), with headings, a hint and `aria-expanded`, narrow the gap.
    ([group-picker](components/group-picker.md), [note-form](screens/note-form.md))
20. **Unpicking a group clears its ticks (A46, an assumed default, not a decision).** The flag reason, by contrast,
    comes back on re-tick (intent of SC 3.3.7, which does not strictly apply to optional ticks).
    ([group-picker](components/group-picker.md))

## Open questions

Only the ones that need the owner. Each screen file also asks for sign-off on its **(P)** strings.

1. **The Manage page.** Manage is reached through a `/manage` page holding four links. It could be read as an extra
   screen; the alternative is a disclosure in the nav. ([app-shell](screens/app-shell.md) Q1)
2. **Success banners beyond Submit.** Not built: "Invite sent to …", "Changes saved", "Alex Park deactivated",
   "Alex Park reactivated", "Sign-in reset for …", "Changes saved. You are now a worker." and "Jane Citizen restored.
   They are back on Today." Focus moves to the heading or the changed Status row instead. Add any?
   ([users](screens/users.md) Q7, [participants](screens/participants.md) Q5)
3. **Review extras.** Not built: an "Earlier reviews" block on a note flagged again after a review, and a "Go to
   flagged notes" link after Mark reviewed. ([participant-notes](screens/participant-notes.md) Q1, Q2)
4. **Sign-in extras.** Not built: Show/Hide on the sign-in password (design gives it only when creating one), and a
   Copy setup key button. The setup key is the authenticator's shared secret: on the clipboard it can be kept by
   Windows clipboard history or synced by iOS Universal Clipboard, which sits badly with D22. ([sign-in](screens/sign-in.md) Q2, Q3)
5. **Compact name in the note form's sticky bar** once the large name scrolls away: keep or drop?
   ([note-form](screens/note-form.md) Q1)
6. **Reset sign-in and email changes.** Approve the confirmation dialog for Reset sign-in (design asks the manager to
   check who is asking but specifies no on-screen confirmation)? Should changing an email, which also resets sign-in,
   get the same dialog? ([users](screens/users.md) Q1, Q2)
7. **Status after edge cases.** An email change on a deactivated person, and reactivating someone deactivated while
   still Invited: which status should the API return? ([users](screens/users.md) Q3, Q4)
8. **Worker pre-selected on Invite user**, the one exception to the no-preselection rule apart from D46's copied
   group picks (a decision, tension 18). Keep? ([users](screens/users.md) Q10)
9. **Small design readings to confirm:** the number in "Editing submitted note (version 2)"; "Submit the note for"
   when the note is not today's; whether pending edits in "Your unfinished drafts" say "Editing submitted note"; the
   download "ready" line on both download screens; "when a page loads" meaning every screen change for the badge.
   ([note-form](screens/note-form.md) Q2, Q3, [today](screens/today.md) Q2, [record-export](screens/record-export.md) Q6,
   [app-shell](screens/app-shell.md) Q3)
10. **API gaps to close before build** (owner and developer): per-row ETags (`rowVersion`, `ConcurrencyStamp`) for
    `If-Match` on participants and users; the participant's first-note date for the export's From default; the
    export's no-notes response; how `POST /api/admin/participants` returns the new ID; `isPastDayNote` on Past notes
    rows. The common-item group fields and endpoints group-picker.md listed as gaps (and archived common items in the
    list) are now in design.md §5.7, §6.3 and §6.6, and §6.6 now returns `rowVersion` on goals, common items and
    their groups.
    ([participants](screens/participants.md) Q2, [record-export](screens/record-export.md) Q1, Q2,
    [participant-notes](screens/participant-notes.md) Q3)
11. **Common item groups (D44–D47).** Confirm or override the assumed defaults A41–A47 (in particular where picks are
    copied from, that no group has to be picked, and that unpicking clears ticks) and the 200-character group name
    (A6, set in one place so every spec uses the same number). Separately, keep or drop the copied-picks line
    (A47, "These ticks are copied from the note for …"): it is an addition, not part of D46, and dropping it also
    removes `NoteDraft.PicksCopiedFrom`, the `picksCopiedFrom` field and the copied-line rows in note-form.md and
    group-picker.md. Approve the proposed manager-side strings
    ("Archived groups", the group announcements) and two Common items choices: group buttons in the item rows' order
    (Move up, Move down, Rename, Archive), and the Group radios only when there are two or more groups.
    ([common-items](screens/common-items.md) Q2–Q4, [note-form](screens/note-form.md) Q13)
