---
id: E05
type: epic
title: Participant export, audit completeness and retention
release: R1
milestone: M5
sources: [D28, D29, A20, A28, A29, A30, "design.md §14 M5", "design.md §4.12", "design.md §11.6", "design.md §3.7", "design.md §5.4", "design.md §5.8", "design.md §10.9", "design.md §12", "ux/screens/record-export.md"]
github:
azure:
---
# E05 Participant export, audit completeness and retention

**Goal:** a manager can export any participant's record for an access or correction request, every audited event is
recorded and found by a test, the database itself stops history and the audit log being changed, archive and
deactivate behave the same on every screen and file, and the operator has written retention and audit extraction
runbooks that a manager has read.

**Done when:** the *Done when* list of [design.md §14 M5](../../design.md#m5-participant-export-audit-completeness-retention)
passes in the test environment, and every feature below is done ([README §9](../README.md#9-definition-of-done)).

## Features
- F05.01 Participant record export
- F05.02 Audit log and append-only records
- F05.03 Archive and deactivate end to end
- F05.04 Retention and audit extraction runbooks

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E05.
