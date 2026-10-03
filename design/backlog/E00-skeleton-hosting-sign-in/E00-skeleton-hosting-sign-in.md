---
id: E00
type: epic
title: Skeleton, hosting and sign-in
release: R1
milestone: M0
sources: [D3, D5, D21, D22, D23, D24, D32, D33, D38, D40, D42, A22, A23, A24, A25, A26, A27, A28, A33, A34, A35, A37, A38, "design.md §2", "design.md §4.0", "design.md §4.1", "design.md §4.11", "design.md §5.1–5.4", "design.md §5.8–5.10", "design.md §6.1", "design.md §6.2", "design.md §6.6", "design.md §7", "design.md §8", "design.md §9", "design.md §10", "design.md §14 M0", "ux/screens/sign-in.md", "ux/screens/app-shell.md", "ux/screens/users.md", "mcp-server.md §3.5"]
github:
azure:
---
# E00 Skeleton, hosting and sign-in

**Goal:** invited managers and workers set up MFA and sign in to Grow2Notes on a phone or a laptop, managers control
who can sign in and in which role, every state change can be audited, and every merge to `main` runs in the test
environment in Azure Australia Southeast, with production ready for manual deploys.

**Done when:** the *Done when* list of [design.md §14 M0](../../design.md#m0-skeleton-hosting-sign-in) passes in the
test environment, and every feature below is done ([README §9](../README.md#9-definition-of-done)).

## Features
- F00.01 Skeleton and continuous integration
- F00.02 Azure hosting and deployment
- F00.03 Tenancy, audit and operator commands
- F00.04 Sign-in, account setup and sessions
- F00.05 App shell
- F00.06 Users

## Scope coverage
This epic's rows are in [coverage.md](../coverage.md), under E00.
