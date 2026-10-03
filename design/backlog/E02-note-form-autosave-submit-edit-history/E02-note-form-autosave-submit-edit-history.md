---
id: E02
type: epic
title: Note form, autosave, submit and edit history
release: R1
milestone: M2
sources: [D6, D9, D12, D13, D15, D17, D18, D19, D20, D22, D25, D34, D35, D37, D39, D44, D45, D46, D47, A3, A4, A8, A9, A10, A11, A13, A17, A33, A44, A45, A46, A47, "design.md §3.1–3.9", "design.md §4.2–4.5", "design.md §5.3–5.8", "design.md §6.3", "design.md §14 M2", "ux/screens/today.md", "ux/screens/note-form.md", "ux/screens/participant-notes.md", "ux/screens/version-history.md", "ux/components/group-picker.md"]
github:
azure:
---
# E02 Note form, autosave, submit and edit history

**Goal:** from Today, a worker or manager writes one note per participant per day (goals, the common items of the
groups that happened, Guided notes and an optional flag), every change is saved on the server and never on the
device, the note is submitted after a check that names the participant, the author or a manager can edit it as a new
version, everyone can read a participant's past notes, and managers can read every version and write a forgotten
past-day note.

**Done when:** [design.md §14 M2 *Done when*](../../design.md#m2-note-form-autosave-submit-edit-history) passes in
the test environment.

## Features
- F02.01 Today list
- F02.02 Note form and autosave
- F02.03 Submit and flag
- F02.04 Past notes and the read view
- F02.05 Common items and group picks
- F02.06 Edit a submitted note
- F02.07 Version history
- F02.08 Past-day notes

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E02.
