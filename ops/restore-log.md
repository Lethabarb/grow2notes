# Restore log

One row for each restore drill and each real restore (ops/[restore-drill.md](restore-drill.md), design.md
[§10.7](../design/design.md#107-backups-and-tested-restore-a36)). The operator adds the row when the work is done, by
pull request.

A row holds no note ID, no name and no address, only a date, the kind of restore, a time, a result and a role.
A copy made as evidence for a breach is recorded in the breach decisions table
([breach-response.md](breach-response.md)), and never here.

- **Date (UTC)** is the day the drill or the restore was done, as `yyyy-mm-dd`.
- **Kind** is one of: `quarterly`, `yearly geo-restore`, `after a migration`, `failed-migration restore`, `outage`.
- **Restore point (UTC)** is the time restored to, as `yyyy-mm-ddThh:mm:ssZ`.
- **Result** is `passed`, or `failed` and in a few words what failed, with no ID or name. For a drill it ends
  `copy deleted` once the copy is gone, and the first drill adds `before go-live`, as in
  `passed, before go-live, copy deleted`.
- **Time taken** is from the start of the restore to its check passing, in minutes. It is what the 4 hour target (RTO)
  is measured against.
- **Ran by (role)** is the role of the person who ran it, such as `developer`, and not their name.

| Date (UTC) | Kind | Restore point (UTC) | Result | Time taken | Ran by (role) |
|---|---|---|---|---|---|
