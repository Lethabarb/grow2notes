# Restore drill and restores

A restore is a rehearsed, recorded routine, not something worked out during an outage. This file says how to run the
drills that design.md [§10.7](../design/design.md#107-backups-and-tested-restore-a36) asks for, and how to restore
after a failed migration or an outage of Australia Southeast. It follows the sections of
[break-glass.md](break-glass.md).

The targets are the design's (§10.7): an in-region restore loses at most 10 minutes of writes (RPO) and is back within
4 hours (RTO). The drills below prove that the backups restore and that the copy is sound, which is what those targets
rest on. The two scripts, in this folder, only read:

- `restore-check.sql` reads the whole database it is run on: the migration history, the latest audit event and note
  version times, the ID of the note with the latest version, the number of rows in every table, and the database's own
  pages (`DBCC CHECKDB`). It shows no name and no note text. It takes no variables.
- `restore-note.sql` reads one named note: the participant, the note's date, status, current version and author, and
  for each version when it was saved, by whom, how many items it holds and how long its guided notes are. It shows one
  note's metadata and no text. Its variable is `Note`.

The tests in CI run both scripts on made-up data. [restore-log.md](restore-log.md) is the record of each drill.

Restored production data is never attached to the test environment or to any app (A35, design.md §10.7). It is read
only by the operator, with these scripts, as the Entra admin. The copy exists for the length of the drill and is then
deleted.

## The quarterly drill

### When to run it

- **Once a quarter** (design.md [§10.9](../design/design.md#109-routine-operations-developer)).
- **After any release with a large migration,** the day it has been in production and the migration's effects are
  visible. Record the kind as *after a migration*.
- **Before go-live,** the first drill, with the extra step 5 below (A83,
  [design.md §13](../design/design.md#13-assumed-defaults)). Before there are notes the checks still prove that the
  restore works, the schema is complete and `DBCC CHECKDB` is clean.

### What to have ready

- Signed in to Azure with `az login`, as the Entra admin: a member of the Entra group "Grow2Notes SQL admins" (D64),
  who is the SQL Entra admin of `sql-grow2notes-prod`. It is the admin group of design.md
  [§9.4](../design/design.md#94-secrets-and-identities), and not a member of `grow2notes_runtime`.
- `sqlcmd` (Go) (https://aka.ms/go-sqlcmd), and a clone of this repository, in Bash (Git Bash on Windows), with its root
  as the working directory.
- A temporary firewall rule for your own IP address on `sql-grow2notes-prod`, deleted straight after the drill, even if
  a step fails. The sign-in and the rule are those of the header of `infra/sql/check-database.sql`. Write no address
  down. Check the server's firewall list before you finish, in case a rule is left open.
- The place to record the drill: [restore-log.md](restore-log.md).

### Running it

1. **Restore production to a copy** on the same server, to one hour ago:

   ```shell
   restore_point=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)
   echo "Restore point: $restore_point"
   az sql db restore --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod \
     --name sqldb-grow2notes --dest-name sqldb-grow2notes-restorecheck --time "$restore_point"
   ```

   Note the restore point (UTC) and the time the command took: both go in the log. A point-in-time restore makes a new
   database and changes nothing in `sqldb-grow2notes`. Keep this shell open, so that `restore_point` stays set.
2. **Check the copy.** Open the firewall rule, then run the check on the copy and read the output on the screen. Keep no
   copy of it:

   ```shell
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes-restorecheck \
     --authentication-method ActiveDirectoryDefault -N strict -b -W -i ops/restore-check.sql
   echo "Exit code: $?"
   ```

   It should print the two result sets (below), then `Exit code: 0`. If the copy has just been made and the first
   attempt fails to sign in, wait a minute and run it again.
3. **Judge the first result set.** *Latest migration* is the newest migration that the release deployed. *Latest audit
   event* and *Latest note version* are shortly before the restore point (a time after it means the wrong database was
   checked). *Note with the latest version* names a note, such as `<note ID> version 3`, for step 6.
4. **Judge the second.** Every table is listed, and no table that holds data, such as `Participant` or `AuditEvent`, is
   at 0 rows unless it is 0 in production too. Exit code 1 and an error message from `DBCC CHECKDB` mean the copy is
   damaged: see *If it fails*.
5. **Before go-live only,** run the same check on production, and compare the two. Production will be an hour ahead, so
   counts of the tables that change as people work may differ. The configuration tables must not: compare
   `Organisation`, `AspNetUsers`, `Participant`, `Goal`, `CommonItemGroup` and `CommonItem`. A difference means a change
   in the last hour, so check that there was one before accepting it. The latest migration must be the same.

   ```shell
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes \
     --authentication-method ActiveDirectoryDefault -N strict -b -W -i ops/restore-check.sql
   ```

   `DBCC CHECKDB` on production reads every page, so run this outside the usual note-writing hours (A35).
6. **From the first drill after go-live,** open the note that step 3 named, on the copy and on production. Copy its ID
   from the screen, never retype it:

   ```shell
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes-restorecheck \
     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Note=<note ID> -i ops/restore-note.sql
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes \
     --authentication-method ActiveDirectoryDefault -N strict -b -W -v Note=<note ID> -i ops/restore-note.sql
   ```

   The two must agree on the participant, the note's date, and every version saved before the restore point: the
   version number, when it was saved, by whom, and the number of items and the length of the guided notes. Production
   may have later versions, and a status or current version that has moved on since. A version before the restore point
   that differs, or is missing on the copy, is a failed drill. The scripts show no text, so the drill compares the shape
   of the record and never reads what a participant received.
7. **Delete the copy.** The `CanNotDelete` lock is on the production database only (design.md
   [§10.5](../design/design.md#105-infrastructure-as-code-bicep)), so the copy deletes normally:

   ```shell
   az sql db delete --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod \
     --name sqldb-grow2notes-restorecheck --yes
   ```

   Never delete `sqldb-grow2notes`. Read the name twice.
8. **Delete the firewall rule,** then **add a row to [restore-log.md](restore-log.md)**: the date, the kind, the
   restore point, the result, how long it took and your role. Never a note ID, a name or an address.

### What it writes and prints

The drill writes one row to `restore-log.md`, and in Azure it makes the copy and deletes it. `restore-check.sql` and
`restore-note.sql` write nothing, and the first prints:

- **Result set 1, *Check* and *Value*:** *Latest migration*; *Latest audit event (UTC)*; *Latest note version (UTC)*;
  and *Note with the latest version*, as `<note ID> version <n>`. Each of the last three says `none yet` while its table
  is empty, as before go-live. Times are UTC, like the restore point.
- **Result set 2, *Table* and *Rows*:** every table of the database by name, each with its exact number of rows.

`restore-check.sql` then runs `DBCC CHECKDB`, which prints nothing when the database is sound. `restore-note.sql`
prints two result sets:
the note (*Participant*, *Note date*, *Status*, *Current version*, *Author*), and its versions (*Version*, *Saved
(Melbourne)* with its offset, *Saved by*, *Items*, *Guided notes length*).

The output names a note and, from `restore-note.sql`, a participant and the people who saved its versions. It is read
on the screen and not saved. The record of the drill is the row in `restore-log.md`, and it holds none of that.

### If it fails

- **`az sql db restore` is refused or fails.** Nothing was made or changed. Read the message; a restore point must be
  inside the 35 days (design.md §10.7), and the copy's name must not already exist. A copy left from an earlier drill
  is deleted first.
- **Sign-in or firewall errors.** Handled as the header of `infra/sql/check-database.sql` describes.
- **`restore-check.sql` exits 1.** `DBCC CHECKDB` reports its errors on the screen: the restored copy is damaged, or the
  database was. This is a failed drill, and the result in the log is the word *failed*. Do not delete the copy until
  the error is understood: run the check on production too, and if production reports the errors, treat it as an
  incident and tell the managers. Restored data is still never attached to an app. Delete the copy afterwards.
- **A check disagrees** (step 3, 5 or 6). Record *failed*, say what disagreed in the log's *Result*, and tell the
  managers if a version is missing from the copy.
- **A step of this file did not work as written.** Fix this file by pull request, as part of the drill.

## The yearly geo-restore drill

### When to run it

Once a year, with the retention list (design.md [§10.9](../design/design.md#109-routine-operations-developer)). It
proves that the backups held in Australia East, the pair of Australia Southeast, can be restored, and that the restore
can stay in Victoria's region.

### Running it

1. **Make a temporary server in Australia Southeast** with Entra-only authentication and the group "Grow2Notes SQL
   admins" as its admin, as `infra/modules/sql.bicep` makes the production server (principal type *Group*, Entra-only).
   It is made by hand, because nothing else is to depend on it:

   ```shell
   az sql server create --resource-group rg-grow2notes-prod-ause --name sql-grow2notes-georestore \
     --location australiasoutheast --enable-ad-only-auth --external-admin-principal-type Group \
     --external-admin-name "Grow2Notes SQL admins" \
     --external-admin-sid "$(az ad group show --group 'Grow2Notes SQL admins' --query id --output tsv)"
   ```

   Add a temporary firewall rule for your own address on it, as for `sql-grow2notes-prod`.
2. **Restore `sqldb-grow2notes` from its geo-redundant backup into that server.** In the Azure portal, create a SQL
   database on `sql-grow2notes-georestore`, with *Use existing data* set to *Backup*, and choose the geo-backup of
   `sqldb-grow2notes`. Name it `sqldb-grow2notes-georestore`. Record the time the backup is as of, which the portal
   shows as the restore point, and how long the restore took.
3. **Run the same checks** as the quarterly drill, steps 2 to 6, against `sql-grow2notes-georestore` for the copy. The
   production side of each comparison is `sql-grow2notes-prod`. A geo-backup is some hours older than the live database,
   so only the versions saved before its restore point need to match.
4. **Delete the server,** which deletes the copy with it, and the firewall rule:

   ```shell
   az sql server delete --resource-group rg-grow2notes-prod-ause --name sql-grow2notes-georestore --yes
   ```

5. **Add a row to [restore-log.md](restore-log.md)** with the kind *yearly geo-restore*.

### If it fails

As the quarterly drill. If the portal does not offer the geo-backup, the database's backups are not geo-redundant:
that is a finding for the managers and the owner, not something to fix during the drill.

## The restore after a failed migration

### When to run it

Only when a migration has damaged data (design.md [§10.6](../design/design.md#106-database-migrations), *Rollback*).
A migration that fails and leaves the schema as it was needs no restore: redeploy the previous artifact. Run this when
data was lost or wrong. Decide quickly, and tell the managers when you start, as every minute the app takes writes is
another minute they would lose.

### What to have ready

- The restore point: the UTC time that the deploy recorded before the migration ran, step 3 of design.md
  [§10.4](../design/design.md#104-cicd-with-github-actions). It is in the deploy run's summary.
- The previous artifact: the CI run of the build that was in production before this deploy, which `deploy.yml`
  takes as its input.
- The sign-in and firewall rule, as above.

### Running it

1. **Restore to a copy at the recorded time,** as in step 1 of the quarterly drill, with `--time` set to the recorded
   time and the copy named `sqldb-grow2notes-restored`.
2. **Check it** with `restore-check.sql`, as in step 2 of the quarterly drill. The latest migration must be the one
   before the failed migration, and the audit and note version times must be just before the restore point.
3. **Stop the web app,** so that nothing writes while the copy is put in place, and note the time: writes between the
   restore point and now are lost.

   ```shell
   az webapp stop --resource-group rg-grow2notes-prod-ause --name app-grow2notes-prod
   ```

4. **Put the copy in place.** Rename the damaged database out of the way and the copy into its name:

   ```shell
   az sql db rename --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod \
     --name sqldb-grow2notes --new-name sqldb-grow2notes-damaged
   az sql db rename --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod \
     --name sqldb-grow2notes-restored --new-name sqldb-grow2notes
   ```

   This step is unconfirmed: the `CanNotDelete` lock on `sqldb-grow2notes` may refuse a rename, and only the owner can
   lift it. If the rename is refused for that reason, the owner lifts the lock, the operator renames, and the owner puts
   the lock back. Check in the portal that the lock covers the database now called `sqldb-grow2notes`; if it does not,
   the owner puts it back (design.md [§10.5](../design/design.md#105-infrastructure-as-code-bicep)). The app's identity
   and its role are inside the database, so they come back with the copy.
5. **Redeploy the previous artifact** with `deploy.yml`, naming that CI run, and start the web app. Its migration step
   finds nothing to apply. Check `/healthz/ready`.
6. **Tell the managers the window of lost writes:** from the restore point to the time the web app was stopped.
   Notes saved in that window are not in the database, and anyone who wrote one may need to write it again. Say which
   hours, and no more, in writing.
7. **Keep the damaged database** until the managers and the owner have seen what was lost, and then delete it. It holds
   production data: it is read only by the operator, and never attached to an app. Add a row to
   [restore-log.md](restore-log.md) with the kind *failed-migration restore*, and change the migration by pull request
   before it is deployed again.

### If it fails

- A restore that cannot be checked (step 2 reports errors) is not put in place. Restore again to an earlier time.
- If a rename is refused, perhaps by the lock (step 4), stop and read the message; the web app stays stopped, the
  damaged database is unchanged and nothing is lost by waiting.

## An outage of Australia Southeast

### When to run it

When Azure reports that Australia Southeast is unavailable and will not recover within the 4 hours (RTO) that the
design allows, and the managers agree to the data moving to Sydney. The data stays in Australia: the backups already
are replicated to Australia East (D33, D38).

### Running it

1. **Geo-restore `sqldb-grow2notes` into Australia East,** on a new server there with Entra-only authentication and the
   group "Grow2Notes SQL admins" as its admin, as for the yearly drill but with `--location australiaeast`.
2. **Check the copy** with `restore-check.sql`. It is as old as its geo-backup, and the managers are told the window.
3. **Redeploy the app in Australia East** with `infra/main.bicep` into a resource group there, pointing the new app
   at the restored database. Run `deploy.yml` for that environment, then check `/healthz/ready`.
4. **Tell the managers** the address if it has changed, and the window of lost writes. Record the kind *outage* in
   [restore-log.md](restore-log.md).

This section is at the level of detail that the design gives (§10.7). Some of it is not yet in the templates: the
resource group in Australia East, and the deployment identity's rights there (a finding for the owner, in T23's Notes).
The first real drill, or the first outage, corrects it by pull request.

### If it fails

Do not attach the restored data to anything that is not production's own app. Stop, and tell the managers.

## What the scripts read, and what they do not

`restore-check.sql` and `restore-note.sql` are read-only, and CI proves it against a database set to read only. They
read tables for their counts, times and one note's header and version metadata. They never show a note's text, and
`restore-note.sql` shows a participant's name only because it is opening one named note for the operator. A copy made
for evidence in a breach is a point-in-time copy made the same way, kept until the assessment ends and recorded in the
breach decisions table (see `breach-response.md`), never in `restore-log.md`.
