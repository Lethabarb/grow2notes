---
id: E07
type: epic
title: Admin MCP server
release: R2
milestone:
sources: [D48, D49, D50, D51, D55, D56, D57, D58, D59, "mcp-server.md"]
github:
azure:
---
# E07 Admin MCP server

**Goal:** a manager, or the operator's Support person, makes the same admin changes as the Manage screens by asking
any AI assistant the provider has approved. They sign in with their own Grow2Notes account and MFA, the same rules and
audit trail apply, and no tool ever returns note content ([mcp-server.md](../../mcp-server.md), D48–D51, D56–D59).

**Done when:** the automated and manual tests in [mcp-server.md §10](../../mcp-server.md#10-testing) pass in the test
environment and every feature below is done ([README §9](../README.md#9-definition-of-done) *Epic*, R2). The release
is done when the [§11.1 switch-on checklist](../../mcp-server.md#111-switch-on-checklist-release-2-per-environment) is
complete, `Mcp:Enabled` is on in production and the date is recorded (README §9 *Release*, R2).

## Features
- F07.01 Support role, sign-in and Connect an AI assistant
- F07.02 MCP endpoint, look-ups and audit
- F07.03 Setup tools
- F07.04 User tools with confirmation
- F07.05 Hardening, manual tests and switch-on

The features follow the five build slices in [mcp-server.md §1.4](../../mcp-server.md#14-timing-and-build-slices) one
to one ([README §3.4](../README.md#34-how-far-ahead-to-write)). Two placements need saying:
- `archive_participant` is a setup tool, but it always needs confirmation (mcp-server.md §5.3, MA6), which slice 4
  builds. So it is delivered in F07.04, with `restore_participant`.
- The test groups that every later tool joins under the Definition of Done (the tool matrix, the no-note-content
  tests, the audit test and the telemetry canary) are owned by F07.02, which builds them. Later tools add their rows
  as part of done.
- A row delivered by more than one feature is owned by the feature that starts it, the same rule as R1 (coverage.md,
  *How to read the tables*): §1.2 *Users* and *Participants* by F07.02, and the rules group (§10 item 7) by F07.03.
  Those features stay open until F07.04's stories finish the rows.

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E07.

## Open question
- [mcp-server.md §12](../../mcp-server.md#12-open-questions) **Q4** (owner): is confirmation by the assistant enough
  for `invite_user`, `reset_user_sign_in`, `deactivate_user` and `archive_participant`, or should these four be
  confirmed on a Grow2Notes page? Stories S07.04.01–S07.04.06 are not Ready until it is answered, and the answer is
  needed by Release 2 planning (Sprint 31 planning), because a Grow2Notes confirmation page would change Release 2's
  scope and dates. The recommendation is to keep the default (MA6), which the Release 2 forecast assumes.
