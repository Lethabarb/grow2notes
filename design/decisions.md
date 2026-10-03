# Grow2Notes: design decisions log

Running record of product decisions made during design discovery. Newest at the bottom of each section.

## Context

- Customer: a **registered NDIS provider**, small (**under 20 support workers**).
- Product: built for **one organisation now, kept SaaS-ready** (every record scoped to an organisation).
- Today: support workers fill in a **templated Word document every day, on a laptop**. No rostering or notes system
  to integrate with or migrate from.

## Decisions

| # | Date | Area | Decision |
|---|---|---|---|
| D1 | 2026-10-01 | Compliance tier | Build to registered-provider standard (NDIS Practice Standards audit evidence). |
| D2 | 2026-10-01 | Scale | Small org: under 20 workers. |
| D3 | 2026-10-01 | Tenancy | Single org now, SaaS-ready data model. |
| D4 | 2026-10-01 | Integration | Standalone. Replaces daily Word templates; no system to sync with. |
| D5 | 2026-10-01 | Devices | Phone and laptop (responsive web). |
| D6 | 2026-10-01 | Note unit | One note per participant per day. |
| D7 | 2026-10-01 | Goal result | Checkbox only (ticked = reached). |
| D8 | 2026-10-01 | Goal shape | Flat list of goals per participant. |
| D9 | 2026-10-01 | Note structure | Three sections: **Goals** (per-participant configurable checkboxes), **Common items** (org-wide configurable checklist shown for every participant; not goals), **Guided notes** (prompted free text). |
| D10 | 2026-10-01 | Common items | Checkbox only, same as goals. |
| D11 | 2026-10-01 | Common items | Same set for every participant; no per-participant hiding. Amended by D44: items are now in groups picked per note. |
| D12 | 2026-10-01 | Guided notes | One text box; the guide prompts are placeholder text inside it. |
| D13 | 2026-10-01 | Same-day notes | A participant only has one worker per day. |
| D14 | 2026-10-01 | Claim evidence | Kept elsewhere (timesheets/invoicing). Notes hold no times or support items. |
| D15 | 2026-10-01 | Edits | Submitted notes stay editable; every version kept and visible to managers. |
| D16 | 2026-10-01 | Incidents | Handled outside the app with existing incident forms. |
| D17 | 2026-10-01 | Review | Notes final on submit; manager must review flagged notes and can spot-check the rest. |
| D18 | 2026-10-01 | Flagging | Worker ticks 'Flag for manager' with a short reason; manager is alerted. |
| D19 | 2026-10-01 | Who edits | Author or a manager can edit a submitted note; each version records who changed it. |
| D20 | 2026-10-01 | Access | All workers can see and write notes for all participants. |
| D21 | 2026-10-01 | Devices | Mix of personal and org-provided devices; design for the personal-device case. |
| D22 | 2026-10-01 | Offline | Online only; drafts autosave to the server; nothing stored on the device. |
| D23 | 2026-10-01 | Sign-in | App accounts by email invite + MFA (authenticator app or passkey). |
| D24 | 2026-10-01 | Managers | A few equal managers, all seeing everything. Roles: worker, manager. |
| D25 | 2026-10-01 | Missing notes | No detection; the app only knows about notes that were written. |
| D26 | 2026-10-01 | Report content | Notes compiled (each note as written: goals ticked, common items ticked, narrative). No aggregate statistics. |
| D27 | 2026-10-01 | Report period | Daily. |
| D28 | 2026-10-01 | Report audience | Managers only; nothing leaves the organisation through the app. Amended by D48/D49: admin data (names and setup data, never note content) reaches the AI provider used with the MCP server. |
| D29 | 2026-10-01 | Export | PDF and Word (.docx). Amended by D43: downloaded only, never shown on screen. |
| D30 | 2026-10-01 | Report delivery | No emails. Amended by D43: the report is downloaded as a file, not read on screen. |
| D31 | 2026-10-01 | AI | None in the app. Amended by D48: an admin MCP server lets managers and support use an AI assistant for admin changes, with no note content. |
| D32 | 2026-10-01 | Stack | ASP.NET Core API + React (Vite, TypeScript). |
| D33 | 2026-10-01 | Hosting | Azure; data and backups in Australia. Region amended by D38. |
| D34 | 2026-10-01 | Guided notes | Plain placeholder; prompts show only while the box is empty. |
| D35 | 2026-10-01 | Note date | Workers write notes for today only. A forgotten past-day note is written by a manager. |
| D36 | 2026-10-01 | Group supports | Separate notes per participant; no group screen. |
| D37 | 2026-10-01 | Jurisdiction | Victoria only: Australia/Melbourne time zone; Health Records Act 2001 (Vic) applies. |
| D38 | 2026-10-01 | Region | Azure Australia Southeast (Melbourne) as the primary region, so primary data stays in Victoria. Geo-redundant backup copies go to Australia East (Sydney). Replaces the Sydney primary in the first design draft. |
| D39 | 2026-10-01 | Wrong participant | A note submitted on the wrong participant is fixed by editing it (old content kept in manager-only history). No 'Entered in error' state. |
| D40 | 2026-10-01 | Operator | The developer's business owns the production Azure subscription and operates the system for the provider, so a written hosting agreement is needed before go-live. |
| D41 | 2026-10-01 | Word notes | Historical Word notes are on workers' own laptops. They are not imported, but must be gathered into organisation storage before go-live. |
| D42 | 2026-10-01 | Name | The app is called **Grow2Notes**. The parent company's name must never appear in the app (screens, page titles, emails, exported files) or in committed source; checks read it from a secret. |
| D43 | 2026-10-01 | Daily report | Download only: a manager picks a day and downloads the report as Word (.docx) or PDF. No on-screen view of the report. Amends D29/D30. |
| D44 | 2026-10-03 | Common item groups | Common items are organised into **groups** (for example Community outing, In-home support, Personal care), set up by managers once for the organisation. On each note the writer **picks which groups apply** to that day, and only those groups' items are shown to tick. Any group can be picked for any participant. Amends D9 and D11. |
| D45 | 2026-10-03 | Every note group | One built-in group, **Every note**, is always shown on every note and cannot be unpicked (for items like 'Medication prompted'). |
| D46 | 2026-10-03 | Default picks | A new note starts with the same groups picked as that participant's most recent note. The writer can change them. |
| D47 | 2026-10-03 | Report: common items | For common items, the daily report shows the Every note group and the groups picked on that note, by name, each item ticked or not ticked. Groups not picked do not appear. |
| D48 | 2026-10-03 | Admin MCP server | Grow2Notes gets an **MCP server** so managers and support can use an AI assistant (e.g. Claude) for **admin changes**: invite, deactivate or reset users; add, edit or archive participants, goals, common item groups and items, and guide prompts. Look-ups only as far as an admin change needs them; no operator tasks through MCP. The workers' app stays AI-free. Amends D31. |
| D49 | 2026-10-03 | MCP data limit | The MCP server **never returns note content** (Guided notes text or ticks). Participant names and setup data are allowed. |
| D50 | 2026-10-03 | MCP users | **Managers** (each with their own Grow2Notes account, limited to what a manager can do) **and a support role** for the operator (the developer's business, D40). |
| D51 | 2026-10-03 | MCP hosting | A **remote MCP server hosted with the app** in Azure Melbourne, added to the AI assistant as a custom connector, signing in with Grow2Notes accounts and MFA. |
| D52 | 2026-10-03 | Backlog | The backlog (WBS: epics, features, user stories, tasks) lives as **Markdown in the repo** under design/backlog/, structured so it can be imported into a board later. |
| D53 | 2026-10-03 | Sprints | **2-week sprints.** |
| D54 | 2026-10-03 | Capacity | About **20 hours a week** of build time (one developer). |
| D55 | 2026-10-03 | MCP timing | The MCP server is built **after go-live**, as Release 2. Release 1 is the app (design.md M0-M6). |
| D56 | 2026-10-04 | MCP: any AI provider | The MCP server must work with **any MCP-capable AI assistant**, not only Claude. Nothing in the server may depend on one provider's apps, plans or settings. The privacy facts about the chosen provider still have to be handled outside the app (privacy policy, hosting agreement). |
| D57 | 2026-10-04 | Support's AI account | Support (the operator, D40) connects from the **operator's own AI account**, so the hosting agreement needs a sub-processor clause for the operator's AI provider. |
| D58 | 2026-10-04 | Support invites | Through MCP, the support role **can invite workers** (not managers). |
| D59 | 2026-10-04 | MCP reorder tools | **Keep** the reorder tools for goals, groups and items. |

## Research highlights that constrain the design

- NDIA record-keeping: every support needs date, hours or quantity, and support type; case notes should tie
  activities to the support item and goals.
- NDIS Amendment (Securing the NDIS for Future Generations) Act 2026 (in force 27 Aug 2026): claim records kept
  7 years; offences for altering or destroying records to defraud, so keep an append-only history.
- From 1 Dec 2026 the NDIS claim window drops from 2 years to 90 days, so late or draft notes are a billing risk.
- Practice Standards Core Module: information accurate, timely, confidential; consent to collect, use and disclose;
  participants can access and correct their records.

## Design

The full system design is in [design.md](design.md). Its assumed defaults (A1 onward) can be overridden by adding a
decision here.
