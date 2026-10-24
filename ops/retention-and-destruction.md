# Retention and destruction

The app deletes no participant records: participants are archived and users deactivated
([design.md §3.7](../design/design.md#37-archiving-nothing-is-deleted)). Records are destroyed only after the periods
for keeping them end, and only by the developer, with the reviewed script `retention-destroy.sql`, on the managers'
written decision. This file says what the rule is, how the yearly list reads it, and how to destroy one participant's
records.

The scripts are `retention-list.sql`, which only reads, and `retention-destroy.sql`, which deletes. Both are in this
folder, and the tests in CI run them on made-up data. The yearly task is in design.md
[§10.9](../design/design.md#109-routine-operations-developer); the first one that matters is in 2033.

## The rule

Keep a participant's records until the latest of these three (design.md
[A29](../design/design.md#13-assumed-defaults)):

1. **7 years after the last service.** The last service is the date of the participant's latest submitted note.
2. **Age 25, if any note was written while the participant was under 18.**
3. **7 years after the last NDIS claim for the participant's supports.** The database does not hold claim dates: the
   managers confirm this period from the invoicing system. No script can decide it.

These come from the NDIS Act s 45B, APP 11.2, and HPP 4.2 and 4.3 of the Health Records Act; the
[compliance rows](../design/design.md#12-compliance-mapping) say how.

### How the list reads each period

This is the reading that design.md [A91](../design/design.md#13-assumed-defaults) records. The owner may overturn it.

- **Last service** is the latest note date of a note that is Submitted. A draft or a discarded note is not a service.
- **A note written under 18** is a note in any status, drafts and discarded notes included, dated before the
  participant's 18th birthday. The list reads the note's date (`NoteDate`) and deliberately not the time it was created
  (`CreatedAtUtc`). The app refuses a note dated in the future (`note.future_date`), so a note's date is never later
  than the day it was written. Reading the date can therefore only find more notes under 18, and keep a record longer,
  never shorter.
- **A participant with no submitted note** has blank dates and the words *No submitted note*. The managers decide.
- **Age 25** is the 25th birthday. For a birthday on 29 February, SQL Server gives 28 February.
- **A period has ended** when its end date is before today's date in Melbourne.

## Where the records live

The yearly list, the destruction log (HPP 4.3) and the saved output of every preview and destroy run name people. They
are kept in the provider's own records storage, in a place the managers choose, and never in this repository (design.md
[A89](../design/design.md#13-assumed-defaults)). The repository holds the layouts and the roles only. The operator
writes each output straight to that place, keeps no local copy, and tells the managers it is there. The app sends
nothing: it emails no one, and the report is a download (D30, D43).

## The yearly list

### When to run it

Once a year, with the yearly geo-restore drill (design.md
[§10.9](../design/design.md#109-routine-operations-developer)). The first list that can name anyone is in 2033, seven
years after the first notes, and a participant under 18 stays on the list until they turn 25.

### What to have ready

- The operator's sign-in, and a temporary firewall rule for the operator's own address, as the header of
  `infra/sql/check-database.sql` describes. Run it as a member of the Entra group "Grow2Notes SQL admins", the SQL
  Entra admin (design.md [§9.4](../design/design.md#94-secrets-and-identities)). Delete the rule straight after.
- The organisation's ID, from `find-ids.sql`:

  ```shell
  sqlcmd -S <server>.database.windows.net -d <database> --authentication-method ActiveDirectoryDefault -N strict \
    -b -W -i ops/find-ids.sql
  ```

  `find-ids.sql` lists every organisation, participant and user with their IDs, and writes nothing. Copy an ID from
  its output and never retype it.
- The place in the provider's records storage for the yearly list.

### Running it

```shell
sqlcmd -S <server>.database.windows.net -d <database> --authentication-method ActiveDirectoryDefault -N strict \
  -b -W -v Organisation=<organisation ID> -i ops/retention-list.sql -o <file>
```

Then save `<file>` straight into the records storage, delete the local copy, delete the firewall rule and tell the
managers that the list is there. Do this for each organisation.

### What it writes and prints

It writes nothing. It reads the organisation's participants and notes and prints one row for each participant, Active
and Archived, in the order the app lists them (A5):

| Column | What it shows |
|---|---|
| Family name, Given name, Date of birth, Status | The participant, and whether they are Active or Archived |
| Last submitted note | The date of the latest submitted note, or blank |
| 7 years after last service | That date and 7 years, or blank |
| Note before 18 | Yes when a note in any status is dated before their 18th birthday, otherwise No |
| Turns 25 | Their 25th birthday, always shown |
| May have ended | *No submitted note*; Yes when 7 years (and age 25, if Note before 18) have passed; else No |
| Participant ID | The ID that `retention-destroy.sql` asks for |

*Blank* is printed as `NULL` by `sqlcmd`. *May have ended* is only a prompt for the managers: it knows nothing of the
NDIS claim period, so a Yes means "worth deciding", not "may be destroyed".

### If it is refused or fails

An organisation that is not an ID, or that does not exist, is refused with one line and exit code 1, and nothing is
written. Copy the ID from `find-ids.sql` and run it again. A sign-in or firewall problem, or error 40613 while the
database wakes, is handled as `infra/sql/check-database.sql` describes.

## Destroying one participant's records

### When to run it

Only after the managers' decision, in writing, that names each participant by name and date of birth, and records that
they have confirmed all three periods, including the NDIS claim period from the invoicing system. The decision is not
in this repository. The script cannot see claim dates, so it cannot protect against an early destruction: the decision
is the control. One run destroys one participant; run it again for the next.

Run it outside working hours. It waits up to 30 seconds for a lock and then rolls everything back.

### What to have ready

- The managers' written decision, naming the participant.
- A manager has **archived the participant in the app.** The script refuses a participant who is not Archived.
- The sign-in and temporary firewall rule, as above.
- From `find-ids.sql` or the yearly list: the organisation's ID and the participant's ID, copied. The participant's
  date of birth, as `yyyy-mm-dd`, copied from the list too. The script refuses unless the date of birth is the
  participant's, which proves that the ID is the person the managers named.

### Running it

First **preview**. It runs the same deletes in the same transaction and then rolls it back, so it changes nothing:

```shell
sqlcmd -S <server>.database.windows.net -d <database> --authentication-method ActiveDirectoryDefault -N strict \
  -b -W -v Organisation=<organisation ID> -v Participant=<participant ID> -v DateOfBirth=<yyyy-mm-dd> \
  -v Mode=preview -i ops/retention-destroy.sql -o <file>
```

Check the name against the managers' decision, and the counts against what you expect. Save the file straight into the
records storage and delete the local copy. The preview holds the log values, so they exist even if the output of the
destroy run is lost after it commits.

Then **destroy**, with the same command and `-v Mode=destroy`. Save that output the same way. Delete the firewall rule.

Then add one row to the destruction log, kept in the records storage, from result set 1 of the output:

| Name | Period covered from | Period covered to | Date of deletion |
|---|---|---|---|
| *Name* | *Period from* | *Period to*, or *no notes* when it prints `NULL` | *Date of deletion* |

### What it writes and prints

In one transaction, scoped to the organisation, it deletes the participant's rows from eight tables, in this order,
because the foreign keys have no cascade: `NoteVersionItem`, `NoteVersionGroup`, `NoteVersion`, `NoteReview`,
`NoteDraft`, `Note` (every status, drafts and discarded notes too), `Goal` and `Participant`.

It prints two result sets:

1. **Outcome** (*Preview: nothing was deleted* or *Destroyed*), **Name** (given name, then family name), **Period from**
   and **Period to** (the earliest and the latest note date in any status, `NULL` when there are no notes) and **Date of
   deletion** (today in Melbourne). These are the values of the log row.
2. **Table** and **Rows**: the rows deleted from each table, in the order above.

#### What stays

- **Common items and their groups** belong to the organisation, not the participant, and stay.
- **User accounts** stay.
- **The audit log (`AuditEvent`) stays,** and the script never writes to it, so a destruction leaves no audit row: the
  log above is the record. The table has no foreign keys and a retention task never rewrites it (design.md
  [§5.3](../design/design.md#auditevent-append-only)). Be clear about what that leaves: the audit rows of a
  participant's changes still hold the participant's ID, **and** their given name, family name and date of birth
  (`participant.*` events), and the wording of their goals (`goal.*` events). Those values outlive the destruction.
  The audit log never holds note text. Redacting the rows would rewrite the log, which design.md forbids, so this is
  the position for the owner to accept or change; it does not stop the first destruction in 2033.

### Why a script, and why this one

Design.md [§10.6](../design/design.md#106-database-migrations) says no ad-hoc SQL against production. The reviewed
`retention-destroy.sql` is the one exception that
[§5.8](../design/design.md#58-saving-a-version-history-is-never-changed),
[§9.4](../design/design.md#94-secrets-and-identities) and A29 allow: the app has no delete, its identity cannot delete
a participant, and destruction has to be a manual, logged task under a separate break-glass login. That login is the
SQL Entra admin, the Entra group "Grow2Notes SQL admins"; it is not a member of the app's database role. The script is
read in a pull request like any other change, and CI runs it on made-up data, including a test that fails when a new
table is added that nobody has decided about, so that a table that holds participant data is never forgotten.

### If it is refused or fails

- **A refusal changed nothing.** It is one line and exit code 1: a value that is not well formed, an organisation that
  does not exist, a participant who is not in that organisation (one message covers an unknown ID and another
  organisation's ID), a date of birth that does not match, a participant who is not Archived, or a mode that is not
  `preview` or `destroy`. Fix the value from the list and run it again.
- **A lock timeout (error 1222) rolled everything back.** Someone was using the data. Run it again later, outside
  working hours.
- **A foreign-key error (547) means a table the script does not know about.** The run rolled back and changed nothing.
  The scope test should have caught the table in CI; fix `retention-destroy.sql` and the test in a pull request, then
  run it again.
- **Any other error rolled back too,** as the script stops at the first error and undoes the transaction. Check the
  counts of a preview before trying again.

## Backups

A destroyed record stays in the Azure SQL backups until they expire: **35 days** in the point-in-time backups and up to
**12 months** in the long-term backups (design.md [§10.7](../design/design.md#107-backups-and-tested-restore-a36)).
Until then it is treated as beyond use: no one restores it for any purpose except a disaster. A copy made for a restore
drill is deleted after the drill ([restore-drill.md](restore-drill.md)).

## If the provider stops using Grow2Notes

The same periods still apply. Before the database is switched off, export each participant's full record, or keep the
database, until those periods end (design.md [§12](../design/design.md#12-compliance-mapping), HPP 4.2 and 4.3).
