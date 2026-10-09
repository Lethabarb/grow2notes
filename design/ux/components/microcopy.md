# Wording, labels and formats

> **Precedence.** Where a screen spec's "Conflicts resolved" table differs from this file, the screen spec wins.
> For failure wording the editorial pass of 1 October 2026 made §9 below the single authority for every screen and
> for empty-loading-error.md.

Cross-cutting component. It covers every word Grow2Notes shows: labels, buttons, hints, errors, statuses, empty states,
page titles, dates, times, names and numbers. It owns four things the per-component documents do not:

1. **The glossary.** One term per concept, product-wide.
2. **The formats.** Exactly five date and time tokens, plus the rules for names and numbers.
3. **The voice rules.** Reading level, case, spelling, punctuation and words to avoid.
4. **The message patterns**, and one canonical wording wherever two component documents proposed different words for
   the same message (see "Alignment with other component documents").

Copy marks used below: **(V)** word for word from design.md. **(N)** design.md wording with only the date, time or
punctuation format normalised to the rules here. **(P)** proposed, not in design.md. Every (P) string follows a
pattern already used in design.md and adds no feature, screen, setting or data.

---

## Where it's used

| Screen (design.md) | Words on it | What differs here |
|---|---|---|
| 4.0 App shell, navigation, page titles | Today, Flagged, Report, Manage; Account, Sign out; page titles "Grow2Notes – Note" | Seen on every screen, so any drift in these few words breaks recognition everywhere (WCAG 3.2.4). Titles must carry no names. |
| 4.0 Session warning | "You'll be signed out in 2 minutes", "Stay signed in" | Interrupts a tired worker mid-note. Fewest words, no blame. |
| 4.1 Sign-in and account setup | Set up your account, passkey, password, authenticator app, 6-digit code, setup key, Sign in, Finish setup, the fixed failure message | Used once at setup, then briefly at each sign-in. The most unfamiliar vocabulary in the app, met by the least confident users. Security rules (A25) fix some wording. |
| 4.2 Today | "Today · Thursday 1 October", Your unfinished drafts, Find a participant, row statuses, empty and no-match messages, "Note for Jane Citizen submitted" | The busiest worker screen, read on a phone at the end of a shift. Times only (the date is in the header). |
| 4.3 Note form and submit confirmation | 1. Goals, 2. Common items, Every note, Which of these happened?, 3. Guided notes, Flag for manager, Reason, Submit note, save indicator, banners, errors, dialog | Highest stakes: wrong participant (3.9) and lost text. The participant's full name must appear in the header, dialog and button. Most strings are (V). |
| 4.4 Past notes and the read view | Past notes, Written by, Submitted, Edited, Flagged, To review, Reviewed, ticked / not ticked, Version history, Edit, Mark reviewed, Show older | Read by everyone; managers see extra tags. Dates name each note. |
| 4.5 Version history (managers) | "Version 3 · Sam Lee (manager) · Thu 1 Oct 2026, 5:03 pm", "Submitted", "Not edited since submit" | Manager only. Dense rows, date with time. |
| 4.6 Flagged notes (managers) | Flagged notes, To review (n), Reviewed, Flag removed in a later edit, Comment, Mark reviewed | Manager only. The count is the only number on any screen. |
| 4.7 Daily report (managers) | Daily report, Previous day, Next day, Download Word, Download PDF, empty message | No report text on screen (D43); the only messages explain why downloads are disabled. |
| 4.8 to 4.10 Manage: participants and goals, common items, guide prompts | Given name, Family name, Date of birth, Add goal, Add item, New group, Add group, Group name, Rename, Move up, Move down, Archive, Restore, help text, Guide prompts | Laptop-first managers. One-sentence help text is allowed here; the consequence ("notes started from now on") is the key message. |
| 4.11 Users (managers) | Invite user, Send invite, Resend invite, Reset sign-in, Deactivate, Reactivate, Invited / Active / Deactivated, Worker / Manager | Security-sensitive actions. Consequences must be stated in the confirmation, with the person's name. |
| 4.12 Export record (managers) | From date, To date, Format, PDF, Word, Include earlier versions of edited notes, Export | Date ranges. "Export" here, "Download" on the report: two different actions, two different words. |
| Downloaded files and the setup email (§11, A23) | "Daily progress notes", footer, file names | Not screens. §11 owns them. The glossary, spelling and the D42 name rule still apply. |

---

## Best practice

**Reading level and plain language**

- **[Standard]** WCAG 2.2 SC 3.1.5 Reading Level (AAA) asks for supplemental content when text "requires reading ability
  more advanced than the lower secondary education level after removal of proper names and titles". Not required at
  AA, but it is the recognised target, and it excludes participant and staff names from the measure.
  https://www.w3.org/WAI/WCAG22/Understanding/reading-level.html
- **[Convention]** The Australian Government Style Manual: "Writing to an Australian year 7 level makes content usable
  for most people." It reports that "about 44% of adults read at literacy level 1 to 2 (a low level)" (source: ABS 2013,
  PIAAC 4228.0, so data from 2011–12), and lists "their linguistic background" as a factor in literacy in English.
  https://www.stylemanual.gov.au/accessible-and-inclusive-content/literacy-and-access
- **[Convention]** NHS digital service manual: "We aim for a reading age of 9 to 11 years old", "short sentences of up
  to 20 words", active voice. Its health-literacy page says "more than 4 in 10 adults struggle with health content for
  the public", and that readability "tools cannot tell you how usable your content is".
  https://service-manual.nhs.uk/content/how-we-write · https://service-manual.nhs.uk/content/health-literacy
- **[Research]** NN/g's lower-literacy study (50 users, 2005): rewriting a site raised task success from 46% to 82% for
  lower-literacy users and from 68% to 93% for higher-literacy users; task time fell from 22.3 to 9.5 minutes and from
  14.3 to 5.1 minutes. Plain language helped both groups. https://www.nngroup.com/articles/writing-for-lower-literacy-users/
- **[Research]** NN/g found domain experts also prefer plain, succinct text; the Style Manual cites it and says the
  "preference for plain English increases with" education and topic complexity. Managers get the same plain voice.
  https://www.nngroup.com/articles/plain-language-experts/
- **[Standard]** ISO 24495-1:2023 defines plain language by reader outcomes (relevant, findable, understandable,
  usable), not by a reading score, and expects testing with intended readers. https://www.iplfederation.org/iso-standard/
- **[Convention]** W3C COGA "Making Content Usable for People with Cognitive and Learning Disabilities" (Working Group
  Note) lists patterns that suit tired readers and readers with English as an additional language: 4.4.1 Use Clear Words,
  4.4.2 Use a Simple Tense and Voice, 4.4.3 Avoid Double Negatives or Nested Clauses, 4.4.4 Use Literal Language
  ("I do not want unexplained, implied or ambiguous information"), 4.4.5 Keep Text Succinct, 4.4.9 Separate Each
  Instruction. https://www.w3.org/TR/coga-usable/
- **[Convention]** Style Manual: "Remove jargon, slang and idioms" (its example: "in light of" is an idiom), and a table
  of everyday replacements (start, not commence; help, not assist; about, not approximately).
  https://www.stylemanual.gov.au/writing-and-designing-content/clear-language-and-writing-style/plain-language-and-word-choice
- **[Convention]** Readability formulas only count sentence and word length. Use them to find the worst strings, then
  fix by restructuring, never by chopping out "because" or "unless" to move a score (NN/g, AHRQ, summarised in the
  ui-ux-design corpus). https://www.nngroup.com/articles/legibility-readability-comprehension/

**One term per concept**

- **[Research]** Nielsen heuristic 4: users "should not have to wonder whether different words, situations, or actions
  mean the same thing." A second name for the same object is a defect. https://www.nngroup.com/articles/consistency-and-standards/
- **[Standard]** WCAG 2.2 SC 3.2.4 Consistent Identification (AA): "Components that have the same functionality within a
  set of web pages are identified consistently." It "does not require identical labels", so "Report" (nav) and "Daily
  report" (page heading) can coexist. https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html
- **[Standard]** WCAG 2.2 SC 2.5.3 Label in Name (A): the accessible name must contain the visible text; "A best practice
  is to have the text of the label at the start of the name", so speech-input users can say what they see. This matters
  for workers who dictate. https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- **[Standard]** SC 2.4.6 Headings and Labels (AA) and SC 3.3.2 Labels or Instructions (A): labels describe purpose and
  are visible. https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html

**Case, spelling, punctuation, numbers**

- **[Convention]** Sentence case: GOV.UK Service Manual, "Use sentence case everywhere, except for proper nouns"; the
  Style Manual writes "all headings in sentence case" and warns against all capitals because "users could misread words".
  https://www.gov.uk/service-manual/design/writing-for-user-interfaces · https://www.stylemanual.gov.au/structuring-content/headings
- **[Research]** Passages in capitals are read 5–10% slower (Woodworth, Smith, Fisher; reviewed by Larson). Short tokens
  cost little. https://learn.microsoft.com/en-us/typography/develop/word-recognition
- **[Convention]** Australian spelling: the Style Manual says to choose one Australian dictionary (the Macquarie or the
  Australian Concise Oxford) and "always use the first entry".
  https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/spelling
- **[Convention]** Contractions: GOV.UK says "Avoid negative contractions like can't and don't. Many users find them
  harder to read, or misread them as the opposite of what they say", while allowing "simple contractions like 'you're'
  and 'we'll'". The Style Manual's "standard tone ... can use contractions". The two agree on positive contractions and
  differ only on negative ones. https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/ ·
  https://www.stylemanual.gov.au/writing-and-designing-content/clear-language-and-writing-style/voice-and-tone
- **[Convention]** GOV.UK: "Use sign in rather than log in"; do not use "click" ("not all users click"); "There's
  usually no need to say 'please'". Same A to Z page.
- **[Convention]** Numbers: the Style Manual says "Use numerals for '2' and above", with "1,000" style grouping.
  https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/choosing-numerals-or-words

**Dates and times**

- **[Convention]** Style Manual: day month year, "Thursday 31 December 2020", no comma after the day name; a non-breaking
  space between the day and the month so a line break splits the date before the year; "Only use abbreviations if space
  is limited"; "Words written in full are usually easier to read and understand"; September is "'Sept' or shorten to
  'Sep'", no full stops; times use a colon, "Write 'am' and 'pm' in lower case" with a non-breaking space; "Use 'noon',
  'midday' or 'midnight' instead of '12 am' or '12 pm'". Ranges use "to", "from … to" or "between … and".
  https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- **[Convention]** GOV.UK agrees on "midday" and "midnight" and on truncated months only "when space is an issue".
- **[Convention]** Absolute timestamps wherever a record may be checked later; relative time ("2 hours ago") breaks
  comparison with other records. No controlled study exists; this is convention for audit contexts (ui-ux-design
  corpus, Low confidence). Grow2Notes is an audit record (D1), so it uses absolute times only.
- **[Standard]** `Intl.DateTimeFormat` (ECMA-402). Verified locally in Node 24.12 with `en-AU` and
  `timeZone: 'Australia/Melbourne'`: long form gives "Thursday 1 October 2026" (correct); `dateStyle: 'full'` gives
  "Thursday, 1 October 2026" (comma: wrong); the short form gives "Thu, 1 Oct 2026" (comma: wrong) and "Sept"; times
  give "4:12 pm" with an ordinary space and "12:00 pm" at midday. Engines differ, so compose strings from parts.
  https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat/formatToParts

**Errors, empty states, statuses**

- **[Convention]** GOV.UK error messages: "Describe what has happened and tell them how to fix it"; do not use
  "please", "sorry", "valid" or "invalid"; "Use the same message next to the field and in the Error summary".
  https://design-system.service.gov.uk/components/error-message/
- **[Research]** NN/g: avoid words that blame ("invalid, illegal, or incorrect"); "offer some potential remedies";
  "Avoid humor since it can become stale if users encounter the error frequently".
  https://www.nngroup.com/articles/error-message-guidelines/
- **[Convention]** GOV.UK character count strings (govuk-frontend 5 source): "You have %{count} characters remaining",
  "You have %{count} characters too many", singular forms, and the error template "[whatever it is] must be [number]
  characters or less". https://design-system.service.gov.uk/components/character-count/
- **[Research]** NN/g empty states: say what the status is, and give a path where there is one.
  https://www.nngroup.com/articles/empty-state-interface-design/
- **[Research]** NN/g: never use a placeholder as the label; placeholders strain short-term memory and can be
  "mistaken for prefilled data". https://www.nngroup.com/articles/form-design-placeholders/

**Names**

- **[Convention]** W3C i18n: do not assume names split cleanly; if you split, label the parts "Family name" and "Given
  name"; do not change the case of names. https://www.w3.org/International/questions/qa-personal-names
- **[Convention]** Microsoft style guide: use singular "they" when you cannot write around a pronoun.
  https://learn.microsoft.com/en-us/style-guide/bias-free-communication

---

## Recommendation for Grow2Notes

### 1. Voice rules

| Rule | Detail | Grade |
|---|---|---|
| Reading level | Year 7 for every string (Style Manual, WCAG 3.1.5). Worker strings aim lower: one idea per sentence, at most 2 sentences per message, about 15 words per sentence, never more than 20 (NHS). | [Convention] |
| Person | "You" is the person using the app. Other staff are named. The participant is always named, never "the participant" in a message. | [Convention] |
| Tense and voice | Present tense, active voice, literal words. No idioms ("carry on", "all set", "heads up", "hang tight", "in light of"). | [Convention] COGA 4.4.2, 4.4.4 |
| Contractions | Positive contractions are fine ("It's", "You'll", "You've"). New strings use no negative contractions: "cannot", "did not", "is not". design.md's two "can't" strings stay word for word (see Tensions). | [Convention] GOV.UK |
| Banned in new copy | please, sorry, oops, invalid, valid, illegal, error occurred, click, tap, log in, login, OK/okay as a button, exclamation marks, emoji, humour. design.md's "Please check your ticks." stays word for word. | [Convention] GOV.UK, NN/g |
| Case | Sentence case for every label, button, heading, tab, tag and title. Never type text in capitals and never use `text-transform: uppercase`. The capitals in design.md's sketches ("JANE CITIZEN") mean large type. | [Convention] + [Research] |
| Spelling | Australian English, first entry in the Macquarie Dictionary: organisation, recognise, authorise, cancelled, colour, centre, program (as in "day program"), enrol. The `-ise` endings throughout. | [Convention] Style Manual |
| Numbers | Numerals for every count, limit, code and version ("6-digit code", "12 characters", "15 minutes", "version 2"). Group thousands: "1,000", "20,000". | [Convention] Style Manual |
| Full stops | Messages written as sentences end with a full stop. Labels, buttons, headings, tabs, tags, status lines and one-phrase success messages do not ("Note for Jane Citizen submitted"). | [Convention] |
| Quotes and apostrophes | Typographic: ’ and ‘ ’ ("Today’s note", "No participant matches ‘xyz’."). Type them directly in the UTF-8 copy module. | [Convention] Style Manual pages |
| Ellipsis | The single character "…" (U+2026), and only for something happening now: "Saving…", "Submitting…". | [Opinion] |
| Separators | " · " (middle dot) in status lines, as design.md uses it. Hidden from screen readers and replaced by a spoken comma (see Accessibility). En dash with spaces in page titles: "Grow2Notes – Note". | [Opinion] |
| Name of the app | Grow2Notes, exactly, everywhere. Never an abbreviation. The parent company's name never appears in any string, title, file, email, cookie, relying-party name or authenticator issuer (D42). | Decision D42 |

### 2. Glossary

One row per concept. "Never" lists the synonyms that must not appear in any visible string. Who: W = workers, M =
managers.

| Term in the app | What it means, in plain words | Use | Never | Who |
|---|---|---|---|---|
| **Grow2Notes** | This app | Page titles, wordmark, emails, files | Any other name (D42) | W M |
| **participant** | A person the organisation supports | "Find a participant", "Add participant", "Participants" | client, customer, resident, patient, service user | W M |
| **note** | One participant's record for one day: goals, common items, guided notes, flag | "Submit note", "today’s note", "Past notes" | progress note (on screen), entry, report, log, case note. "Progress notes" appears only in downloaded file titles (§11). | W M |
| **note date** | The day a note is for. Fixed when the draft starts; never changes. | Header under the name; "Note date" label (past-day note) | day of service, shift date | W M |
| **Today** | The current date in Melbourne, from the server; also the home screen | Nav item, header "Today · Thursday 1 October", "today’s note" | home, dashboard, my day | W M |
| **draft** | A note that has been started but not submitted. Saved on the server, not on the phone. | Status "Draft", "Discard draft", "Your unfinished drafts" | in progress, unsaved, pending, incomplete | W M |
| **saved / Saving… / Not saved** | Automatic saving to the server while you write (the note form's save indicator), **or** the result of an explicit **Save** button that stays on the page (participant details, goals, common items, guide prompts, users) | Save indicator; "Saved 4:12 pm" and "Not saved: …" next to an explicit Save. A busy label is always the button's own verb + "…": "Saving…" for Save, "Saving changes…" for Save changes, "Marking reviewed…" for Mark reviewed | synced, stored, uploaded, backed up | W M |
| **submit / Submitted** | Finish the note. It becomes final and appears in reports at once (D17). | "Submit note", "Submit note for Jane Citizen", status "Submitted" | send, finish, complete, lodge, finalise, post | W M |
| **Edit / Save changes / Cancel / Edited** | Change a submitted note. "Save changes" makes a new version; "Cancel" throws the unsaved changes away; "Edited" marks a note with more than one version. | Exactly these four words | Update, Amend, Modify, Revise, Undo | W M |
| **version** | One saved state of a submitted note. Version 1 is the note as submitted. | "Editing submitted note (version 2)", "Version history", "Version 3 · …" | revision, copy, snapshot (on screen), history (alone) | M (and the editor's heading) |
| **Past notes** | The list of one participant's notes, newest first | Link text and page name | history, timeline, log, records. "History" appears only in "Version history". | W M |
| **past-day note** | A note a manager writes for a date that was missed (D35) | "Write past-day note", "Past-day note, written by …" | late note, backdated note, catch-up note | M (W see the line) |
| **Goals** | The participant’s own goals. Ticked means reached (D7). | "1. Goals", "Add goal" | outcomes, targets, objectives, tasks | W M |
| **Common items** | The organisation’s everyday activities or items that are not goals, organised into groups (D44). Every note’s items show on every note; another group’s items show when the writer ticks that the group happened. Ticked means done (D10). | "2. Common items", "Add item", "New item in Community outing" (Common items screen) | tasks, activities, checklist, standard items | W M |
| **group** (of common items) | A named set of common items, for example Community outing or Personal care (D44). Managers set groups up once for the organisation. | **Managers only**, on the Common items screen: "New group", "Add group", "Group name", "Archived groups", the "Group" radios, hidden context "…, Community outing group". On the note form workers never see the word: the groups are listed by name under "Which of these happened?" | category, section, set, type, activity type, template, list (for a group); and "group" on any worker screen, because in NDIS work "group" also means group supports (D36) | M |
| **Every note** | The built-in group whose items show on every note (D45). Always first; it cannot be renamed, moved or archived (A43). | Its heading on the note form, the read view, the files and the Common items screen; "Always shown first, on every note." | Always, Default, General, Standard, Core, Daily, All notes, Every day | W M |
| **Guided notes** | The one free-text box | "3. Guided notes" | narrative, comments, description, free text | W M |
| **guide prompts** | The grey text in the empty Guided notes box. Not saved into notes. | Manager screen name; workers see the prompts, never the term (an error calls it "the grey text") | template, questions, hints | M |
| **tick / ticked / not ticked** | The state of a goal or common item, and of a group under "Which of these happened?" (the design calls ticking a group "picking" it; screens always say tick) | "Ticked means reached.", "Tick all that happened.", "These ticks are copied from the note for …", read view "ticked / not ticked" | check, checked, select, selected, completed; "pick" or "picked" on screen | W M |
| **Flag for manager / flagged / Flagged** | Ask a manager to look at a note, with a reason (D18) | Tick box "Flag for manager", "Reason", "Flagged for manager: Yes", tag and nav item "Flagged" | escalate, report, alert, concern, issue | W M |
| **To review / Reviewed / Mark reviewed** | A flagged note waiting for a manager, or one a manager has dealt with | Tabs, tags and the button, exactly | resolved, closed, actioned, approved, checked | M |
| **Comment** | A manager’s optional note when marking reviewed | "Comment (optional)" | note (that is a different concept), feedback, remarks | M |
| **discard** | Remove a draft from every screen. It is kept and audited; the day is free again. | "Discard draft" | delete, remove, cancel, clear, bin | W M |
| **archive / Archived / Restore** | Stop using a participant, goal, common item or group for new notes, without losing anything | "Archive", "Restore", toggle "Active / Archived", "Archived goals", "Archived groups" | delete, remove, hide, disable, deactivate | M |
| **deactivate / Deactivated / Reactivate** | Stop a user signing in. Their notes stay. | "Deactivate", "Reactivate", status "Deactivated" | archive, disable, suspend, remove, delete | M |
| **user / Users** | A person who can sign in | Manage > Users, "Invite user" | account holder, member, staff, employee | M |
| **Worker / Manager** | The two roles (D24). Lower case inside a sentence. | Role options, "(manager)" after a name | admin, supervisor, staff, carer, support worker (as a role label) | M |
| **invite / Invited / Send invite / Resend invite** | Add a person; the app emails them a setup link | Exactly these | register, add user, enrol | M |
| **setup link** | The single-use link in the email, valid 7 days (A23) | "setup link" (noun, one word); "Set up your account" (verb, two words) | activation link, invitation link, magic link | W M |
| **sign in / sign-in / sign out** | "Sign in" and "sign out" are verbs; "sign-in" is the noun or adjective | "Sign in", "Signed in as …", "Sign-in failed", "Reset sign-in", "Sign out" | log in, login, log on, log out, logout | W M |
| **passkey** | Signing in with your face, fingerprint or phone PIN | "Use a passkey (recommended)", "Sign in with a passkey" | biometric login, security key, FIDO | W M |
| **password / authenticator app / 6-digit code / setup key / QR code** | The other sign-in method (A22) | Exactly these | OTP, TOTP, 2FA, MFA, token, one-time password, verification code | W M |
| **Reset sign-in** | A manager removes someone’s sign-in method and emails a new setup link | Button and confirmation | reset password, unlock, recover account | M |
| **Report / Daily report / Download Word / Download PDF** | The day’s submitted notes as a file (D43). Never shown on screen. | Nav "Report", heading "Daily report", buttons | view report, preview, export (here), print | M |
| **Export record** | One participant’s notes for a date range, as a file | Button and screen name, "Export" | download (here), report, print | M |
| **Manage** | The setup area | Nav item and heading | Admin, Settings, Configuration, Setup | M |

Rules that follow from the glossary:

- **"Cancel" appears only in edit mode** (3.5). Every dialog's safe button is "Go back". A Cancel button that sometimes
  keeps and sometimes throws away work would be one word for two opposite actions.
- **"Delete" never appears.** Nothing in Grow2Notes is deleted (3.7, A29); the words are discard, archive and deactivate.
- **"Saved" never means "submitted".** The indicator says "Saved 9:42 am"; only Submit makes a note final.
- **"Group" is a manager word.** It appears only on the Common items screen. The note form, read view and files show
  each group by its own name and Every note by "Every note"; the question "Which of these happened?" needs no
  category word (group-picker.md).
- **"Pick" is a design word, "tick" is the screen word.** Specs say a group is "picked"; screens say "Tick all that
  happened." and "These ticks are copied…". One action, one visible verb.
- **"Your unfinished drafts" can hold a pending edit** (4.2, A17), which is not a draft in the glossary sense. (P) A
  pending-edit row reuses design.md's own words under the participant's name: "Editing submitted note", then the note
  date. A draft row shows the note date only, as designed. [Opinion: reuses existing copy; adds no data]

### 3. Formats

All dates and times are Melbourne dates and times from the server (D37, A33). The device clock is never used. No time
zone label is shown, because the app is Victoria only.

| Token | Output | Use it for | Never for |
|---|---|---|---|
| `dateLong` | Thursday 1 October 2026 | Any date that **names** something: the note-form header, the confirmation dialog, banners and every date inside a sentence, link text for a note in Past notes, the note date on draft rows and Flagged rows, empty-state messages | Next to a time |
| `dateToday` | Thursday 1 October | The Today header only (4.2), where the year adds nothing | Anywhere else |
| `datePlain` | 1 October 2026 | Date of birth, and From / To ranges ("between 1 January 2026 and 30 September 2026") | Note dates |
| `time` | 4:12 pm | A time on the same reference day (below). Exactly 12:00 pm shows as "midday" and 12:00 am as "midnight" (Style Manual, GOV.UK). | — |
| `dateTime` | Thu 1 Oct 2026, 4:12 pm | A date that **qualifies** a time stamp on a different day from the reference day: version rows, "Saved Wed 30 Sep 2026, 9:42 am", "Submitted Fri 2 Oct 2026, 8:05 am", "Edited · last change by …" | Sentences and headings |

**Reference day.** Status lines about live work (Today rows, the save indicator, "started 9:14 am") use Melbourne
today: show `time` alone when the event was today, otherwise `dateTime`. Metadata about a note (Submitted, Edited,
past-day line) uses the note date: `time` alone when the event was on the note date, otherwise `dateTime`. This is
design.md's own rule ("with its date when it was a later day", 4.4).

**Typography.** A non-breaking space (U+00A0) between the day number and the month ("1 October") and between the
time and am/pm ("4:12 pm"), so neither splits across lines (Style Manual). Abbreviations: Mon Tue Wed Thu Fri Sat Sun;
Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec; no full stops. The Style Manual allows the three-letter forms where
context makes the meaning clear, and a date next to a time does.

**No relative time.** Never "2 hours ago", "yesterday at", "just now". The one exception is design.md's after-midnight
sentence, which uses "yesterday" in a sentence about a rule, not as a time stamp.

**Durations.** Numerals and the full unit: "2 minutes", "15 minutes", "7 days", "30 seconds".

**Names.**
- **Participants:** given name then family name, exactly as stored, never re-cased: "Jane Citizen". The full name is
  used wherever the note is identified (form header, dialog title, confirm button, success message, Past notes heading,
  empty messages) because of 3.9. The given name alone appears only in design.md's "No goals set up for Jane yet."
- **Staff:** the user's `DisplayName` exactly as stored (one field, up to 100 characters, §5.3). Never derive a first
  name or an initial from it (W3C). So design.md's "Only Alex can finish it." is built as "Only Alex P. can finish it."
  with the same `DisplayName` as the first sentence. Managers decide how names show by how they type them on the Invite
  form. **(N)**
- **The signed-in user** is "You" in status lines: "Draft · You · saved 9:42 am", "Submitted · You · 4:12 pm".
- **Role suffix** "(manager)" only where design.md shows it: version rows and the past-day line.
- **Pronoun** for a named user: singular "they" ("Their notes stay.").

**Numbers and plurals.** `Intl.NumberFormat('en-AU')` for grouping. `Intl.PluralRules('en-AU')` for "1 character" /
"2 characters", "1 version" / "3 versions". The To review count in the tab: "To review (3)".

### 4. Message patterns

One shape per kind of message. A developer writing a new message picks the row and fills the slots.

| Kind | Pattern | Examples |
|---|---|---|
| Field error, empty | Enter [the thing] / Write [the thing] / Choose [A or B] | "Enter a reason for the flag" · "Choose Worker or Manager" |
| Field error, too long | [Thing] must be [n] characters or less | "Reason must be 200 characters or less" · "Guided notes must be 20,000 characters or less" |
| Field error, date | [Thing] must be in the past · [Thing] must be the same as or after [datePlain] | "Note date must be in the past" · "To date must be the same as or after 1 January 2026" |
| Error summary | Heading "There is a problem", then links with exactly the inline text | — |
| Character count | You can enter up to [n] characters · You have [n] characters remaining · You have [n] characters too many | GOV.UK strings, singular forms included |
| Action failed | Not [done]: [cause]. [What is safe, if anything.] Try again. Cause is "no connection" or "something went wrong". This is the shape of design.md's "Not saved: no connection." | "Not submitted: no connection. Your draft is saved. Try again." · "Not sent: something went wrong. Try again." |
| Load failed | [Thing] did not load: [cause]. Try again. Plus a "Try again" button. After a failed Try again: [Thing] still did not load: [cause]. Try again. | "The participant list did not load: no connection. Try again." · "The participant list still did not load: no connection. Try again." |
| No longer possible (conflict) | What changed, then what to do | "Sam Lee is the only active manager, so they cannot be deactivated. Make someone else a manager first." |
| Busy label | The button's own verb + "…", shown only after the request starts (page buttons after 400 ms; a dialog's status line after 1 s, primary-actions.md) | "Submitting…", "Discarding…", "Resetting sign-in…", "Saving…" (Save), "Saving changes…", "Marking reviewed…" |
| Success | [Object] [past participle], no full stop; a second sentence only if it adds a fact | "Note for Jane Citizen submitted" · "Sign-in reset for Sam Lee. A new setup link was emailed to sam.lee@example.org." |
| Empty state | What is empty; then the path, where design.md gives one | "No participants yet." + "Add participant" (managers) |
| Disabled control | A visible sentence next to it says why. design.md provides one for every disabled control. | Submit: the save indicator · Downloads: "No submitted notes for Thursday 1 October 2026." |
| Confirmation dialog | Title: the question, naming the object. Confirm button: verb + object. Safe button: "Go back". | "Discard the draft note for Jane Citizen?" / "Discard draft for Jane Citizen" / "Go back" |
| Page title | Grow2Notes – [page name] from a fixed list; "Error: " in front after a failed submit | "Grow2Notes – Note" · "Error: Grow2Notes – Note" |

### 5. States

Microcopy changes only in the states below. Hover, focus and active never change any text, and nothing is shown only
on hover: there are no tooltips and no `title` attributes carrying information (WCAG 1.4.13, NN/g tooltips).

| State | What the words do |
|---|---|
| Default | Persistent visible labels. Hints sit under the label as text, never as placeholder text. The only placeholder in the app is the guide prompts (D34). |
| Disabled | The label does not change. A visible sentence nearby gives the reason (pattern above). |
| Loading | Nothing for the first second; then "Loading…" or a specific "Loading participants…". Buttons show "[Verb]ing…". |
| Error | Pattern-based message next to the field and in the summary; failed requests use "Not [done]: [cause]." |
| Empty | The design.md empty string, verbatim. |
| Read-only | The same words as the editable form, with "ticked" / "not ticked" in place of tick boxes (checkbox-list.md). A manager reading someone else's draft sees the same sentence a worker sees: "Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it." **(N)** |
| Success | One-phrase message on the destination screen (status-messages.md owns placement). |

### 6. Phone and laptop

- **Identical words on both.** Never a shorter label for phones: two labels for one control fail recognition and
  WCAG 3.2.4. Text wraps instead.
- **Never truncate a participant's name** with an ellipsis, anywhere. The name is the wrong-participant guard (3.9).
  Long names wrap, including inside "Submit note for Jane Citizen".
- **Dates keep their non-breaking spaces**, so a narrow screen breaks "Thursday 1 October / 2026", never "1 / October".
- At 320 px wide and 200% text, every string must wrap without clipping. Plan for long names: `DisplayName` and
  participant names are up to 100 characters each.

### 7. Accessibility

- **Language of page (SC 3.1.1).** `<html lang="en-AU">`. Screen readers then pronounce Australian English. Names
  need no `lang` (proper names are exempt from 3.1.2).
- **Label in Name (SC 2.5.3).** The visible words are the start of every accessible name. Extra context is added as
  visually hidden text after the visible words, never with an `aria-label` that replaces them:
  `Edit<span class="vh"> goal: Makes own breakfast</span>`, `Flagged<span class="vh">, 3 to review</span>`. A worker
  who dictates "click Submit note for Jane Citizen" must hit the button.
- **Consistent Identification (SC 3.2.4).** Enforced by the glossary and by keeping every string in one module.
- **Headings and Labels (SC 2.4.6), Labels or Instructions (SC 3.3.2).** Every field has a visible label. "3. Guided
  notes" stays visible while the prompts show and after they go (D34).
- **Error Identification and Suggestion (SC 3.3.1, 3.3.3).** Every error names the field and the fix. The A25
  sign-in message is the one deliberate exception: it must not say which part was wrong.
- **Status Messages (SC 4.1.3).** Save indicator, success and count strings are announced through regions that exist
  before their text changes (owned by autosave-status.md and status-messages.md). This document only fixes their words.
- **Separators.** Render the middle dot as `<span aria-hidden="true"> · </span>` followed by a visually hidden `, `,
  so "Draft · You · saved 9:42 am" is read "Draft, You, saved 9:42 am" with pauses, not "Draft dot You". [Opinion;
  verify with NVDA and VoiceOver]
- **Abbreviated dates** appear only next to times. How screen readers speak "Thu" and "Oct" varies (unverified), which
  is a further reason to keep long dates in every sentence and link.
- **`<time dateTime>`** wraps every displayed date and time (`<time dateTime="2026-10-01">Thursday 1 October 2026</time>`).
  It is plain HTML semantics; no ARIA.
- **Use of Color (SC 1.4.1).** Every status is a word (design 4.0): Draft, Submitted, Flagged, To review, Reviewed,
  Edited, Invited, Active, Deactivated, Archived.
- **Page Titled (SC 2.4.2).** Fixed titles, no personal data (design 4.0).
- **WCAG 2.2 criteria this component helps meet:** 1.3.1, 1.4.1, 1.4.13, 2.4.2, 2.4.4, 2.4.6, 2.5.3, 3.1.1, 3.2.4,
  3.3.1, 3.3.2, 3.3.3, 4.1.2, 4.1.3. At AAA (not claimed): 3.1.3 Unusual Words and 3.1.5 Reading Level are the targets
  the voice rules aim at; 3.1.4 Abbreviations is not met by the short date form, and does not need to be at AA.

### 8. Implementation notes (React 19 + TypeScript + CSS Modules)

No i18n library: the app has one locale (en-AU) and the owner rejects unrequested machinery. Strings live in typed
TypeScript so a missing parameter is a compile error.

```
src/copy/
  format.ts      // the five date/time tokens, names, numbers, plurals
  strings.ts     // fixed strings, `as const`
  messages.ts    // functions for strings with slots
  apiErrors.ts   // design §6.9 error code -> message function
  Sep.tsx        // the spoken-comma separator
  copy.test.ts   // banned words, formats, D42
```

**Formats.** Compose from numeric parts so engine differences ("Sept", commas, spaces, "AM") cannot leak in. A note
date is a calendar date (`DateOnly` "2026-10-01" from the API): format it without any time-zone conversion.

```ts
// src/copy/format.ts
const TZ = 'Australia/Melbourne';
const NBSP = ' ';
const DAYS = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'] as const;
const MONTHS = ['January','February','March','April','May','June','July','August',
                'September','October','November','December'] as const;

export type Ymd = { y: number; m: number; d: number };            // calendar date, no zone
export const parseDateOnly = (s: string): Ymd => {
  const [y, m, d] = s.split('-').map(Number); return { y, m, d };
};
const dayName = ({ y, m, d }: Ymd) => DAYS[new Date(Date.UTC(y, m - 1, d)).getUTCDay()];

export const dateLong  = (x: Ymd) => `${dayName(x)} ${x.d}${NBSP}${MONTHS[x.m - 1]} ${x.y}`; // Thursday 1 October 2026
export const dateToday = (x: Ymd) => `${dayName(x)} ${x.d}${NBSP}${MONTHS[x.m - 1]}`;        // Thursday 1 October
export const datePlain = (x: Ymd) => `${x.d}${NBSP}${MONTHS[x.m - 1]} ${x.y}`;              // 1 October 2026
const dateShort = (x: Ymd) =>
  `${dayName(x).slice(0, 3)} ${x.d}${NBSP}${MONTHS[x.m - 1].slice(0, 3)} ${x.y}`;          // Thu 1 Oct 2026

const melb = new Intl.DateTimeFormat('en-AU', {
  timeZone: TZ, year: 'numeric', month: 'numeric', day: 'numeric',
  hour: 'numeric', minute: '2-digit', hourCycle: 'h23',
});
const inMelbourne = (utcIso: string) => {
  const p = Object.fromEntries(melb.formatToParts(new Date(utcIso)).map(x => [x.type, x.value]));
  return { y: +p.year, m: +p.month, d: +p.day, h: +p.hour, min: +p.minute };
};

export const time = (utcIso: string) => {
  const { h, min } = inMelbourne(utcIso);
  if (min === 0 && h === 12) return 'midday';
  if (min === 0 && h === 0) return 'midnight';
  return `${h % 12 || 12}:${String(min).padStart(2, '0')}${NBSP}${h < 12 ? 'am' : 'pm'}`;
};

/** Time alone on the reference day (Melbourne today, or the note date); otherwise "Thu 1 Oct 2026, 4:12 pm". */
export const stamp = (utcIso: string, referenceDay: Ymd) => {
  const t = inMelbourne(utcIso);
  const same = t.y === referenceDay.y && t.m === referenceDay.m && t.d === referenceDay.d;
  return same ? time(utcIso) : `${dateShort(t)}, ${time(utcIso)}`;
};

const nf = new Intl.NumberFormat('en-AU');
const pr = new Intl.PluralRules('en-AU');
export const count = (n: number, one: string, other: string) =>
  `${nf.format(n)} ${pr.select(n) === 'one' ? one : other}`;          // "1 character", "20,000 characters"
```

Melbourne "today" comes from the API (`GET /api/today` returns `date`), never `new Date()` on the device.

**Messages with slots.**

```ts
// src/copy/messages.ts
import { time } from './format';
// Cause is 'offline' | 'server', the keys of strings.cause's two cause words (§9), as src/api's classify() gives them.
import { strings, type Cause } from './strings';
export const msg = {
  noteSubmitted: (participant: string) => `Note for ${participant} submitted`,                 // (V)
  startedByOther: (author: string, participant: string, startedUtc: string) =>
    `${author} started today’s note for ${participant} at ${time(startedUtc)}. Only ${author} can finish it.`, // (N)
  noMatch: (typed: string) => `No participant matches ‘${typed.trim()}’.`,                    // (N)
  notDone: (done: string, cause: Cause, safe?: string) =>                   // §4 Action failed
    `Not ${done}: ${strings.cause[cause]}.${safe === undefined ? '' : ` ${safe}`} Try again.`,
  loadFailed: (what: string, cause: Cause, again: boolean) =>               // §4 Load failed, §9 still
    `${what} ${again ? 'still did not load' : 'did not load'}: ${strings.cause[cause]}. Try again.`,
} as const;
```

**API errors.** The server sends a code and parameters (§6.9); the client owns every word. One map, typed against the
code union, so a new code without wording fails the build:

```ts
// src/copy/apiErrors.ts
// p = the server's parameters (§6.9) plus what the screen already knows (the participant's name).
export const apiErrorCopy: Record<ApiErrorCode, (p: ApiErrorParams) => string> = {
  'note.in_progress_by_other': p => msg.startedByOther(p.authorDisplayName, p.participantName, p.startedAtUtc),
  'user.last_manager':         p => `${p.name} is the only active manager, so they cannot be ${p.action}. Make someone else a manager first.`,
  // … one row per code in design §6.9
};
```

**Separator.**

```tsx
// src/copy/Sep.tsx
import styles from './Sep.module.css';   // .vh = visually hidden
export const Sep = () => (<><span aria-hidden="true"> · </span><span className={styles.vh}>, </span></>);
```

**Keeping it consistent.** Turn on ESLint `react/jsx-no-literals` (eslint-plugin-react) so visible text cannot be typed
straight into JSX and every string goes through `src/copy`. [Opinion: the cheapest way to make the glossary enforceable
for a solo developer] `copy.test.ts` (Vitest) reads the `src/copy` sources and fails on:

- The parent company's name anywhere (D42), case-insensitive. The test reads the forbidden string from a CI secret
  (environment variable `FORBIDDEN_NAME`), never from committed source, so the name is never written into the
  repository. Run the same check in CI over the API's email templates, the report renderers, the passkey relying-party
  name and the authenticator issuer.
- Banned words: `/\b(log ?in|login|log out|click|invalid|oops|sorry)\b/i`, `/\bdelete/i`, and `please` except in the
  one design.md string.
- US spellings: `/\b(organiz|authoriz|recogniz|color\b|center\b|canceled)/i`.
- Format snapshots: `dateLong({y:2026,m:10,d:1})` is "Thursday 1 October 2026" (with U+00A0), `time` at 02:00Z on
  1 October 2026 is "midday", 15:30Z on 3 October 2026 is "1:30 am" and 16:30Z is "3:30 am" (daylight saving starts
  at 2:00 am on Sunday 4 October 2026; verified in Node 24.12). Run the same tests in Playwright WebKit, because
  Safari's Intl data can differ.

Server-side strings (setup email, report and export files) follow the same glossary and formats; §11 fixes their
exact layout. In .NET, use the `Australia/Melbourne` time zone and compose the strings the same way rather than
relying on culture defaults (unverified whether .NET's en-AU designators are "am"/"pm" on Linux).

**Testing the words.** Before go-live, ask 5 workers, including some who write in English as an additional language,
to say what each primary button will do before they press it (the ui-ux-design corpus suggests at least 80% should be
able to). Use a readability checker on worker-facing strings as a smoke alarm only (NHS). This needs no new feature.

### 9. Alignment with other component documents

Two or more component documents proposed different words for the same message. Build the canonical wording.

**Failure wording, settled once (editorial pass, 1 October 2026).** Every screen and empty-loading-error.md use the
rows below for loads and for actions. The screens had split between two wordings: four (app-shell, today,
common-items, users) used empty-loading-error.md's "Could not load {thing}." plus a cause sentence and banned
"something went wrong"; eight used the rows here. Chosen: these rows, for three reasons. They match design.md's own
"Not saved: no connection." shape; a single cause phrase serves loads and actions alike, so the shared `LoadRegion`
and the action-failure lines are built once; and "something went wrong" never appears alone, always inside a shape
that names what failed and ends "Try again." (NN/g error-message guidelines ask for exactly that). "There is a
problem with Grow2Notes" survives only as the heading of the two whole-page messages (crash and start-up server
failure), as GOV.UK uses it. empty-loading-error.md keeps the mechanism: 1 s delay, one silent retry, Try again
outside the status line, and the changed "still" text on a repeat so it is announced again. [Convention] GOV.UK,
NHS App; [Research] NN/g, https://www.nngroup.com/articles/error-message-guidelines/

| Message | Canonical | Differs in |
|---|---|---|
| Cause words (loads and actions) | "no connection" (fetch rejected, offline or timed out) · "something went wrong" (`5xx`, a `429` with no specific message, anything unexpected). Never alone: always inside one of the two shapes below, always followed by "Try again." | empty-loading-error.md ("Check your internet connection, then try again." / "There is a problem with Grow2Notes. Try again in a few minutes."), app-shell.md |
| Submit failed, no connection | Not submitted: no connection. Your draft is saved. Try again. | confirm-dialog.md ("The note was not submitted. Check your connection…"), primary-actions.md (no reassurance) |
| Any action failed, server | Not [done]: something went wrong. [Safe part.] Try again. | primary-actions.md, sign-in-form.md ("Something went wrong. Try again in a few minutes."), totp-setup.md ("…on our side…"), app-shell.md ("Not signed out: there is a problem with Grow2Notes…") |
| Page or list did not load | [Thing] did not load: [cause]. Try again. | participant-list-rows.md ("Couldn't load…"), app-shell-nav.md ("Can't connect to Grow2Notes…"), empty-loading-error.md ("Could not load {thing}." + cause sentence) |
| Load failed again (after Try again) | [Thing] still did not load: [cause]. Try again. | empty-loading-error.md ("Still could not load…") |
| Show older failed (Past notes, Flagged > Reviewed) | Older notes did not load: [cause]. Try again. · Older notes still did not load: [cause]. Try again. | participant-notes.md ("Select Show older to try again."), chronological-list.md ("This list did not load. Check your connection…") |
| Start-up failed (whole page) | `<h1>` Could not connect to Grow2Notes (no connection) or There is a problem with Grow2Notes (server); status line "Grow2Notes did not load: [cause]. Try again." | empty-loading-error.md, app-shell-nav.md |
| Stay signed in failed (session dialog) | No connection. Try again. | session-timeout.md ("…Check your internet and try again.") |
| No connection on sign-in | Not signed in: no connection. Try again. | sign-in-form.md, totp-setup.md ("No connection. Check your internet and try again.") |
| Sign-out blocked | Not signed out: your note is not saved yet. Keep this page open; retrying. | app-shell-nav.md ("…isn't saved yet…") |
| Help for a lost sign-in | If you cannot sign in, ask a manager to reset your sign-in. | sign-in-form.md ("Can't sign in?…"), totp-setup.md ("Cannot use your authenticator app?…") |
| Signed out in place | Heading "You've been signed out", then "Sign in again to go back to where you were." | session-timeout.md, sign-in-form.md ("…to carry on", an idiom) |
| Code wrong format | The code must be 6 digits | sign-in-form.md ("6 numbers"). "Digits" matches design's "6-digit code". |
| Code empty | Enter the 6-digit code from your authenticator app | sign-in-form.md (shorter form) |
| Flag reason empty | Enter a reason for the flag | conditional-reveal.md ("…for flagging this note") |
| Guided notes empty | Write your notes in the Guided notes box. Add "The grey text is only a guide." only when guide prompts exist. | guided-notes-textarea.md (first sentence only) |
| Too long | [Thing] must be [n] characters or less. One sentence; the counter already shows how many too many. | guided-notes-textarea.md ("or fewer"), form-validation.md (adds a second sentence) |
| Discard confirm button | Discard draft for Jane Citizen | primary-actions.md ("Discard draft") |
| Reset busy label | Resetting sign-in… | primary-actions.md ("Resetting…") |
| Last active manager | [Name] is the only active manager, so they cannot be [deactivated / made a worker]. / …so their sign-in cannot be reset. Then "Make someone else a manager first." | confirm-dialog.md, form-validation.md, primary-actions.md ("There must always be at least one active manager.") |
| Email already used | Another account already uses this email address. Use a different one, or find the person in the Users list. | primary-actions.md ("This email address already has an account.") |
| Exactly 12:00 | midday / midnight | participant-list-rows.md ("12:00 pm") |
| Date inside a range error | `datePlain`: "To date must be the same as or after 1 January 2026" | form-validation.md (example uses the weekday form) |
| Dates in the two note-form banners | dateLong (see Per-screen notes, 4.3) | status-messages.md (kept the abbreviated design.md form) |

---

## Per-screen notes

### 4.0 App shell, navigation, session

- Nav: "Today", "Flagged" (+ hidden ", 3 to review"), "Report", "Manage". Account button "Account"; panel "Signed in as
  Jo Smith"; "Sign out". Skip link "Skip to main content". **(V/P, app-shell-nav.md)**
- Page names, fixed (app-shell.md owns the typed union): Sign in · Set up your account · Today · Note · Past notes ·
  Version history · Flagged notes · Daily report · Manage · Participants · Add participant · Participant · Write
  past-day note · Common items · Guide prompts · Users · Invite user · User · Edit user · Export record · Page not
  found · There is a problem · Could not connect. Format "Grow2Notes – Note" **(V pattern)**. Kept in design.md's
  order (app name first): both orders pass SC 2.4.2, and the titles are generic by design.
- Session: "You'll be signed out in 2 minutes" / "Stay signed in" **(V)**. Later steps "…in 1 minute" use the plural
  helper.

### 4.1 Sign-in and account setup

- Keep every design.md string word for word: "Set up your account", "Use a passkey (recommended)", "Use a password and
  authenticator app", "Sign in with a passkey", "Finish setup", "This link has expired. Ask a manager to send a new
  one.", "Your setup session timed out. Open the link from your email again.", "Open this link in your browser".
- The failure message is fixed by A25: "Sign-in failed. Check your details and try again. After 5 failed attempts,
  sign-in pauses for 15 minutes." It is the one error that deliberately does not name the field.
- Field labels "Email address", "Password", "Create a password" with hint "Use at least 12 characters." (sign-in-form.md).
- This is where unfamiliar words cluster (passkey, authenticator app, setup key, QR code). Each is explained once, in
  one sentence, where it first appears (sign-in-form.md and totp-setup.md hints); never with jargon such as MFA or OTP.

### 4.2 Today

- Header "Today · Thursday 1 October" **(V)**, `dateToday`.
- "Your unfinished drafts" **(V)**. Draft row: participant name, then `dateLong`. Pending-edit row: participant name,
  "Editing submitted note", then `dateLong` **(P, reuses V words)**.
- Search label "Find a participant" **(V)**. No match: "No participant matches ‘xyz’." **(N: typographic quotes and a
  full stop)**. Empty: "No participants yet." **(V)**, managers also "Add participant" **(V)**.
- Rows: "Draft · You · saved 9:42 am", "Draft · Alex P. · started 9:14 am", "Submitted · Alex P. · 4:12 pm" **(V)**,
  "Submitted · You · 4:12 pm" **(P)**, tag "Flagged" **(V)**. Times only; an earlier-day time cannot occur on Today.
- Someone else's draft: "Alex P. started today’s note for Jane Citizen at 9:14 am. Only Alex P. can finish it." **(N,
  name rule)**.
- After submit: "Note for Jane Citizen submitted" **(V)**.
- No counts, no "not started", no "missing" (D25, A17).

### 4.3 Note form and submit confirmation

- Header: full name, then the note date in `dateLong`.
- Section headings "1. Goals", "2. Common items", "3. Guided notes" **(V)**. Hints "Ticked means reached." and "Ticked
  means done." **(V, 3.1/4.3)**.
- Save indicator: "Saving…", "Saved 9:42 am", "Not saved: no connection. Keep this page open; retrying.", "Draft ·
  saved 9:42 am" **(V)**. On a later day: "Saved Wed 30 Sep 2026, 9:42 am" (`stamp`).
- Banners, normalised to `dateLong` because they are sentences (Style Manual: no abbreviations in body text; the brief's
  date style). Words unchanged **(N)**:
  - "Past-day note for Monday 28 September 2026, written on Thursday 1 October 2026."
  - "This draft is for Wednesday 30 September 2026. Submitting it now keeps that date."
- "The goal or common-item list was just changed. Please check your ticks." **(V)**
- "This note was changed on another device or tab." with "Keep the text on this screen" / "Load the other version"
  **(V)**. Version conflict button "Start again from the latest version" **(V, §6.3)**.
- "It's now after midnight, so this note can't be started for yesterday. Ask a manager to record it." **(V)**
- Empty lists: "No goals set up for Jane yet. A manager can add them." / "No common items set up." **(V)**
- Common item groups (D44–D46, group-picker.md): group heading "Every note"; question "Which of these happened?";
  hint "Tick all that happened. Their items show below."; copied line "These ticks are copied from the note for
  Wednesday 30 September 2026. Untick any that did not happen." (the source note's date in `dateLong`) **(V, design.md
  4.3)**. Each other group's heading is its name as stored. None of these say "today", because a past-day note is not
  about today, or "group".
- "Flag for manager", "Reason" **(V)**, hint "You can enter up to 200 characters" then the GOV.UK count strings.
- Edit mode: heading "Editing submitted note (version 2)" **(V)**, where the number is the version being edited (it
  updates after "Start again from the latest version", per autosave-status.md). Buttons "Save changes" / "Cancel"
  **(V)**. The indicator still says "Saved 9:42 am" while editing; only "Save changes" makes the edit visible to
  others. Watch this in testing: an editor could read "Saved" as "done". **[Opinion]**
- Submit button "Submit note" **(V)**. Dialog: "Submit today’s note for" + "Jane Citizen" + `dateLong` + "Flagged for
  manager: No" / "Yes"; buttons "Submit note for Jane Citizen" / "Go back" **(V)**. When the note date is not today:
  "Submit the note for" **(P, confirm-dialog.md)**, because "today’s" would be false.
- Discard: "Discard the draft note for Jane Citizen?" **(V)**; buttons "Discard draft for Jane Citizen" / "Go back".

### 4.4 Past notes and the read view

- Link text "Past notes" **(V)**. Heading: the participant's full name. Rows: the note date in `dateLong` as the link
  text, then author and "Draft" / "Submitted", then tags "Edited", "Flagged", and for managers "To review" /
  "Reviewed" **(V)**. "Show older" **(V)**. Empty: "No notes yet for Jane Citizen." **(V)**
- Read view lines:
  - "Written by Priya Nair · Submitted 4:42 pm" (or `dateTime` when submitted on a later day).
  - "Edited · last change by Jo Smith, Fri 2 Oct 2026, 9:01 am" (status-messages.md wording).
  - Past-day line, one wording on screen and in files: "Past-day note, written by Jo Smith (manager) on Sat 3 Oct
    2026, 10:14 am" **(N)**. design.md has two orders (3.8 "written on … by …", §11.3 "written by … on …"); the
    §11.3 order reads more naturally and already carries "(manager)".
  - "Flagged for manager" with the reason; ticked items read "ticked" / "not ticked".
  - Common items: "Every note" and each picked group's name as sub-headings (D47); never the question or the copied
    line.
  - Managers: "Version history" link, review status and comment.
- Buttons "Edit", "Mark reviewed" **(V)**.

### 4.5 Version history

- Rows "Version 3 · Sam Lee (manager) · Thu 1 Oct 2026, 5:03 pm" **(N: weekday added by `dateTime`)**; "Version 1 ·
  Submitted · Priya Nair · Thu 1 Oct 2026, 4:12 pm" **(V label "Submitted")**. Empty: "Not edited since submit" **(V)**.

### 4.6 Flagged notes

- Heading "Flagged notes"; tabs "To review (3)" | "Reviewed" **(V)**.
- To review row: name, note date (`dateLong`), author, first line of the reason, "Flagged 4:12 pm" or `dateTime`;
  "Flag removed in a later edit" where it applies **(V)**.
- Review panel: label "Comment (optional)", hint "You can enter up to 500 characters", button "Mark reviewed".
  "(optional)" is the GOV.UK convention for optional fields **[Convention]**.
- Empty: "No flagged notes to review." **(V)**
- Conflict (`review.not_current`): "This note has changed or was already reviewed. Check the latest version before
  marking it reviewed." (primary-actions.md)

### 4.7 Daily report

- Heading "Daily report"; "Previous day", a date field labelled "Date" **(P)**, "Next day"; "Download Word", "Download
  PDF" **(V)**.
- Empty: "No submitted notes for Thursday 1 October 2026." **(V)**. No words such as "preview", "view" or "open
  report" (D43).
- Download failure: "Not downloaded: no connection. Try again." (pattern).

### 4.8 Participants and goals

- List: "Find a participant", toggle "Active" / "Archived", "Add participant" **(V)**.
- Labels "Given name", "Family name", "Date of birth" **(V, A5)**; date of birth shown with `datePlain`.
- Goals help text **(V)**: "Changes apply to notes started from now on. Notes already started or submitted keep the
  wording they were written with." Goal hint "You can enter up to 200 characters".
- Row buttons "Edit", "Move up", "Move down", "Archive"; archived section "Archived goals" with "Restore" **(V)**.
  Each accessible name adds the goal text after the visible word.
- Empty goals: "Notes will show an empty Goals section." **(V)**
- "Write past-day note" with a date field labelled "Note date" **(P)**.

### 4.9 Common items

- Help text **(V, design.md 4.9 after D44)**: "Items in Every note show on every note. For other groups, the writer
  ticks the ones that happened. Changes apply to notes started from now on." It replaces "Shown on every
  participant's note, in this order…", which stopped being true with D44.
- Every note line "Always shown first, on every note."; per-group "New item in [group name]" with "Add item"; empty
  group "No items in this group. It does not show on notes until it has one."; "Group name", "New group", "Add group",
  the "Group" radios; errors "Enter the group name" / "Group name must be 200 characters or less" **(V)**. Rows as for
  goals; "Archived items" per group and "Archived groups" **(P)**. Group buttons Move up, Move down, Rename, Archive,
  Restore, each with hidden ", [group name] group".

### 4.10 Guide prompts

- Label "Guide prompts", hint "You can enter up to 1,000 characters", "Save" **(V/P)**. Help text **(V)**: "Prompts are
  a guide only. They disappear as soon as someone starts typing and are not saved into notes."
- Saved: "Saved 4:12 pm" (same words as the note form). Unsaved-changes dialog "Leave without saving?" with "Stay on
  this page" / "Leave without saving" (primary-actions.md).
- **Advice for the managers who write the prompts** (content, not a feature): short questions, one per line, everyday
  words, no names or examples about a real participant, at most about 5 lines so they fit an empty phone box. The
  design.md sketch is a good model: "Mood and wellbeing today?", "What did you do together?", "Anything to follow up?".

### 4.11 Users

- Columns: name, email, role, status ("Invited", "Active", "Deactivated") **(V)**.
- Invite: "Name", "Email address", "Role" with "Worker" / "Manager", "Send invite" **(V/P)**. Success: "Invite sent to
  alex.park@example.org".
- Actions "Edit", "Resend invite", "Reset sign-in", "Deactivate", "Reactivate" **(V)**.
- Deactivate body: "Sam Lee will be signed out everywhere now and can't sign in. Their notes stay." **(V)**
- Reset sign-in confirmation names the person and the email address it will send to (confirm-dialog.md).

### 4.12 Export record

- Labels "From date", "To date", "Format" ("PDF" / "Word"), "Include earlier versions of edited notes", button
  "Export" **(V)**.
- Empty: "No submitted notes for Jane Citizen between 1 January 2026 and 30 September 2026." **(V with `datePlain`)**
- Range error on To date: "To date must be the same as or after 1 January 2026".

### Downloaded files and the setup email (§11, A23)

- Not screens. §11 fixes their exact layout and strings ("Daily progress notes", the footer, file names with dates only).
- The glossary, Australian spelling, the D42 name rule and the comma before a time apply. One alignment for the file
  owner: §11.3 writes "Fri 2 Oct 9:01 am" in one place and "Fri 2 Oct, 8:05 am" in another; use the comma in both.

---

## Anti-patterns to avoid

1. Two words for one thing: "delete" for discard, "history" for past notes, "check" for tick, "log in" for sign in.
2. One word for two things: "Cancel" meaning "close" in a dialog but "throw away my edit" in the form. Dialogs say
   "Go back".
3. "Saved" or "Done" after Submit. Submit's result is "submitted".
4. Placeholder text as a label or as the only hint. The guide prompts are the one placeholder (D34), and the label stays.
5. Relative times ("2 hours ago", "yesterday at 4 pm") in a compliance record.
6. Device-clock dates. Melbourne today comes from the server.
7. `dateStyle: 'full'` or the default short format, which add commas and "Sept".
8. "12:00 am" for midnight.
9. Abbreviated dates inside sentences, headings or link text.
10. Truncating a participant's name with an ellipsis.
11. Deriving "Alex" or "A. P." from a `DisplayName`.
12. Capitals for emphasis, or `text-transform: uppercase`.
13. "Error", "invalid", "oops", "sorry", "something went wrong" with no next step, or humour on any failure path.
14. Negative contractions and idioms in new copy ("can't", "carry on", "all set").
15. Hover-only tooltips or `title` attributes carrying information.
16. An `aria-label` that replaces the visible words (breaks SC 2.5.3 for workers who dictate).
17. Participant names in page titles, URLs or file names (design 4.0, A19).
18. US spelling ("organization", "canceled", "color").
19. Adding words design.md did not ask for: no onboarding tips, no "Did you know?", no celebratory success messages,
    no counts or "missing" labels (D25, D26).

---

## Tensions with decisions

1. **D12 / D34, guide prompts as placeholder text that disappears on typing.** NN/g lists short-term memory strain and
   placeholders being "mistaken for prefilled data" among the harms. Workers with English as an additional language
   who use the prompts to structure their note lose them at the first keystroke. The copy here softens one symptom:
   the empty-box error adds "The grey text is only a guide." when prompts exist. Recorded only.
2. **D18, the word "flag".** "Flag" as a verb is figurative; COGA 4.4.4 (Use Literal Language) and the Style Manual
   ("Remove jargon, slang and idioms") point towards literal words. The evidence is convention, not testing, and
   "flag" is common workplace English. The visible "Reason" field and "Flagged for manager: Yes" make the meaning
   concrete. Recorded only.
3. **design.md copy, not a D-numbered decision: negative contractions and "please".** "It's now after midnight, so this
   note can't be started…", "…and can't sign in. Their notes stay." and "Please check your ticks." GOV.UK says negative
   contractions are misread by many users and that "please" is usually unnecessary; the Style Manual's standard tone
   allows contractions. These three strings stay word for word. New strings follow the stricter GOV.UK rule.

---

## Sources

- Australian Government Style Manual, Dates and time: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/dates-and-time
- Style Manual, Literacy and access: https://www.stylemanual.gov.au/accessible-and-inclusive-content/literacy-and-access
- Style Manual, Plain language and word choice: https://www.stylemanual.gov.au/writing-and-designing-content/clear-language-and-writing-style/plain-language-and-word-choice
- Style Manual, Voice and tone: https://www.stylemanual.gov.au/writing-and-designing-content/clear-language-and-writing-style/voice-and-tone
- Style Manual, Spelling: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/spelling
- Style Manual, Choosing numerals or words: https://www.stylemanual.gov.au/grammar-punctuation-and-conventions/numbers-and-measurements/choosing-numerals-or-words
- Style Manual, Headings: https://www.stylemanual.gov.au/structuring-content/headings
- GOV.UK style guide, A to Z: https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/
- GOV.UK Service Manual, Writing for user interfaces: https://www.gov.uk/service-manual/design/writing-for-user-interfaces
- GOV.UK Design System, Error message: https://design-system.service.gov.uk/components/error-message/
- GOV.UK Design System, Character count: https://design-system.service.gov.uk/components/character-count/
- govuk-frontend 5.11.0 character count source (default strings): https://unpkg.com/govuk-frontend@5.11.0/dist/govuk/components/character-count/character-count.mjs
- GOV.UK Design System, Button: https://design-system.service.gov.uk/components/button/
- NHS digital service manual, How we write: https://service-manual.nhs.uk/content/how-we-write
- NHS digital service manual, Health literacy: https://service-manual.nhs.uk/content/health-literacy
- NN/g, Writing for lower-literacy users: https://www.nngroup.com/articles/writing-for-lower-literacy-users/
- NN/g, Plain language is for everyone, even experts: https://www.nngroup.com/articles/plain-language-experts/
- NN/g, Error message guidelines: https://www.nngroup.com/articles/error-message-guidelines/
- NN/g, Placeholders in form fields are harmful: https://www.nngroup.com/articles/form-design-placeholders/
- NN/g, Empty states in application design: https://www.nngroup.com/articles/empty-state-interface-design/
- NN/g, Consistency and standards: https://www.nngroup.com/articles/consistency-and-standards/
- NN/g, Legibility, readability and comprehension: https://www.nngroup.com/articles/legibility-readability-comprehension/
- WCAG 2.2 Understanding 3.1.5 Reading Level: https://www.w3.org/WAI/WCAG22/Understanding/reading-level.html
- WCAG 2.2 Understanding 3.2.4 Consistent Identification: https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html
- WCAG 2.2 Understanding 2.5.3 Label in Name: https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html
- WCAG 2.2 Understanding 2.4.6 Headings and Labels: https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html
- W3C, Making Content Usable for People with Cognitive and Learning Disabilities (COGA): https://www.w3.org/TR/coga-usable/
- W3C i18n, Personal names around the world: https://www.w3.org/International/questions/qa-personal-names
- ISO 24495-1:2023 plain language (via IPLF): https://www.iplfederation.org/iso-standard/
- Larson, The science of word recognition (all-caps reading speed): https://learn.microsoft.com/en-us/typography/develop/word-recognition
- Microsoft Writing Style Guide, Bias-free communication: https://learn.microsoft.com/en-us/style-guide/bias-free-communication
- MDN, Intl.DateTimeFormat.prototype.formatToParts: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat/formatToParts
- eslint-plugin-react, jsx-no-literals: https://github.com/jsx-eslint/eslint-plugin-react/blob/master/docs/rules/jsx-no-literals.md
- Local verification: Node 24.12 `Intl.DateTimeFormat('en-AU', { timeZone: 'Australia/Melbourne' })` outputs quoted in Best practice (run 1 October 2026).
- Grow2Notes design.md §3, §4, §5.3, §6.9, §11, §13 and decisions.md D1–D47.
