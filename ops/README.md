# ops

Runbooks for the developer (design.md §7.3).

- `restore-drill.md`, with `restore-check.sql`, `restore-note.sql` and `restore-log.md`: the quarterly and yearly
  restore drills (design.md §10.7).
- `breach-response.md`: containing and reporting a data breach (design.md §9.10).
- `break-glass.md`: the operator commands run from the App Service console (design.md §7.4).
- `first-use-in-test.md`: the one ordered checklist of the checks by hand still outstanding in test, for the owner's
  first sign-in there (owner, 12 October 2026).
- `retention-and-destruction.md`, with `retention-list.sql` and `retention-destroy.sql`: the yearly retention task.
- `audit-extraction.md`, with `find-ids.sql` and `audit-extract.sql`: extracting audit records for a manager.

The `.sql` files are run with `sqlcmd` as the SQL Entra admin, and CI runs each one on made-up data. `find-ids.sql`
looks up the IDs that the others take.
