---
id: E06
type: epic
title: Hardening and go-live
release: R1
milestone: M6
sources: [D38, D40, D41, D42, A32, A35, A36, A38, A39, A40, "design.md §14 M6", "design.md §14 Go-live checklist", "design.md §9", "design.md §10", "design.md §12"]
github:
azure:
---
# E06 Hardening and go-live

**Goal:** Grow2Notes has passed a security and accessibility review, is backed up with a recorded restore test,
alerts the operator when it fails, is set up in production from the Word template, and every staff member switches
to it on one set date with the go-live checklist complete.

**Done when:** the *Done when* list of [design.md §14 M6](../../design.md#m6-hardening-and-go-live) passes, every
feature below is done, and the R1 release check in [README §9](../README.md#9-definition-of-done) is met: the
*Go-live readiness* checklist (F06.06) has an owner and evidence for every line, and the build that passed in test is
deployed to production.

## Features
- F06.01 Security review and hardening
- F06.02 Accessibility checks
- F06.03 Backups and restore drill
- F06.04 Monitoring and alerts
- F06.05 Production set-up
- F06.06 Go-live readiness and cut-over

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E06.
