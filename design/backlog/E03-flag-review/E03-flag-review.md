---
id: E03
type: epic
title: Flag review
release: R1
milestone: M3
sources: [D16, D17, D18, D24, A6, A14, A15, A16, A28, "design.md §3.6", "design.md §4.6", "design.md §5.3", "design.md §5.4", "design.md §5.8", "design.md §6.4", "design.md §6.8", "design.md §14 M3", "ux/screens/flagged.md"]
github:
azure:
---
# E03 Flag review

**Goal:** every submitted note flagged for a manager reaches every manager's To review list and badge, stays there
until one manager marks it reviewed (with an optional comment), and comes back when a later edit adds a flag or
changes its reason; every review is recorded and audited, and the only alert is inside the app.

**Done when:** [design.md §14 M3 *Done when*](../../design.md#m3-flag-review) passes in the test environment, and
every feature below is done ([README §9](../README.md#9-definition-of-done)).

**Builds on:** E02 (the note form's Flag for manager box, the version save and the read view) and E00 (the app shell,
its `/me` refresh and the audit writer).

## Features
- F03.01 Flag for manager
- F03.02 Mark reviewed
- F03.03 Flagged notes lists and badge

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E03.
