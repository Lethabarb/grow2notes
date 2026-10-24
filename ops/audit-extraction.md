# Audit extraction

The audit log has no screen in v1, so when a manager asks a question about it, the developer, as the operator, extracts
the answer with `ops/audit-extract.sql` ([A28](../design/design.md#13-assumed-defaults)). This is a direct query on
production data, so it is done the same way every time, from a request that is recorded first
([A90](../design/design.md#13-assumed-defaults)). The script only reads. This file says what the log holds, how to
record a request, and when to run the script, what to have ready, how to run it and what it prints.

## What the log holds, and what it does not

The log's fields and its three indexes are in [design.md §5.3](../design/design.md#53-entities), and the events in
§5.4. In short, it holds who did what and when, in one organisation: sign-ins, note writes (draft started, submitted,
edited, discarded), flag reviews, configuration and user changes, report downloads and participant record exports, and
the operator commands, which have no actor ([A28](../design/design.md#13-assumed-defaults)).

- **It does not hold opening a note.** Opening a note, a participant's history or a version history is never
  recorded, and an autosave is not an event. So the log cannot say who read a note. For a breach, a misused
  account is assumed to have exposed every participant's notes (D20, design.md §9.10): never tell a manager that the
  log shows that a note was not read. Report downloads and participant record exports are recorded.
- **It holds no note text.** The Details column is metadata, but it is not only IDs. A participant event holds the
  participant's given name, family name and date of birth, a goal event holds the goal's wording, and a user event holds
  the person's email address and name. Those values stay in the log after a participant's records are destroyed (the
  log is never rewritten, §5.3). So an extract is personal information: handle it as the notes themselves are handled.
- **It can hold one event twice.** An event is written at least once: if the connection drops after a commit, the retry
  can store it again, with a new Row number (S00.03.03). Two rows with the same event type, entity, actor and Details at
  the same or almost the same time are one event. The script removes none and counts nothing, and neither should you:
  never give a manager a count of rows as a count of events.
- **The IP address stays in the database.** Each row holds the address the request came from. The extract leaves it
  out, so that it is not copied into a file. If a breach assessment needs addresses, change `audit-extract.sql` by pull
  request, with the manager's reason, and record that in the request.

## The request record

Before any query, add a row to the **request record**, kept in the provider's own records storage, in the place the
managers chose for it ([A90](../design/design.md#13-assumed-defaults)). It is never in this repository, which is public:
even a row without names would show that an investigation or a breach is under way. One row for each request, with
these columns:

| Date (Melbourne) | Asked by (role) | Why | Question and date range | Script and values | Run by (role) |
|---|---|---|---|---|---|

- **Asked by** and **Run by** are roles (a manager, the operator), not names.
- **Why** is the reason in a few words, such as a complaint, a breach assessment or a manager's own check.
- **Question and date range** is the question in words and the two dates.
- **Script and values** names `ops/find-ids.sql` and `ops/audit-extract.sql` with the values given, without the extract.
  The request covers the `find-ids.sql` run made for it.

## When to run it

- **When a manager asks a question the log can answer:** what was done to a participant's records, what a user did, or
  what happened in the organisation in a period. Ask who asked, why and for which dates, and write them in the request
  record first.
- **To preserve evidence in a suspected breach,** before anything is cleaned up (design.md §9.10), with one request row
  for each extract. [breach-response.md](breach-response.md) says when.
- **Not to answer who read a note:** the log cannot.
- **In test,** only on made-up data (A35).

## What to have ready

- The request record row (above), filled in except for *Run by*.
- The organisation's ID, and the participant's or user's ID, from `ops/find-ids.sql`. Copy an ID from its output into
  the command and never retype one: a mistyped ID matches nothing and the extract comes back empty. The script accepts
  an ID with no row, because a destroyed participant's rows stay.
- The first and last day to cover, as Melbourne dates (`yyyy-mm-dd`), both days included.
- `sqlcmd` (Go) (https://aka.ms/go-sqlcmd), signed in with `az login` as a member of the Entra group "Grow2Notes SQL
  admins", the SQL Entra admin (D64); and a clone of this repository, in Bash (Git Bash on Windows), with its root as
  the working directory.
- Somewhere to put the file: the records storage, where the manager who asked can reach it. Never an email or a chat.

## Running it

1. **Open a temporary firewall rule for your own IP address** on the SQL server, and delete it straight after, even if
   a script fails. The sign-in, the rule and the retry for error 40613, when a paused test database wakes, are those of
   `infra/sql/check-database.sql`'s header. Write no address down.
2. **Find the IDs.** The output goes to the screen only and is kept nowhere:

   ```shell
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes \
     --authentication-method ActiveDirectoryDefault -N strict -b -W -i ops/find-ids.sql
   ```

   In test, use `sql-grow2notes-test`. The first result set lists the organisations, the second the participants and
   the third the users, each with its ID.
3. **Run the extract** for the question, with the values from the table below. Every value is an ID, a date, or `any`.

   | Question | Participant | User |
   |---|---|---|
   | What was done to this participant | their ID | `any` |
   | What this user did | `any` | their ID |
   | Everything in the period | `any` | `any` |

   ```shell
   sqlcmd -S sql-grow2notes-prod.database.windows.net -d sqldb-grow2notes \
     --authentication-method ActiveDirectoryDefault -N strict -b -W \
     -v Organisation=<organisation ID> -v From=<yyyy-mm-dd> -v To=<yyyy-mm-dd> \
     -v Participant=<participant ID or any> -v User=<user ID or any> \
     -s $'\t' -y 0 -o <file>.tsv -i ops/audit-extract.sql
   echo "Exit code: $?"
   sed -i 2d <file>.tsv
   ```

   - `-s $'\t'` separates columns with a tab, so a name or a Details value with a comma stays in its cell. `-y 0` stops
     a long Details value being cut short. `-o` writes the result to the file, which is then a tab-separated file that
     a spreadsheet opens. `sed -i 2d` removes the line of dashes that `sqlcmd` prints under the header.
   - Save the file in a folder that is not in the repository, and name it for the request, not for a person.
4. **Check the file before it goes anywhere.** Open it and look for the header row; one line for each audit row, and no
   row split over two lines; the Row column rising from top to bottom; and a long Details value complete, ending with
   its closing brace. If the flags did not give that, correct this file by pull request.
5. **Put the file in the records storage** for the manager who asked, **delete the local copy,** and **delete the
   firewall rule.** Add *Run by* to the request record.

## What it writes and prints

It writes nothing. It reads the log's rows for that organisation, in that period, and the names of users and
participants to put beside their IDs, and prints one result set, oldest first, with ties in Row order. It prints no
count and no note text. The columns:

- **Time (Melbourne):** when the event was written, in Melbourne time with its offset: `+10:00` in winter and `+11:00`
  while the clocks are forward. The two days asked for start at Melbourne midnight and end at the midnight after.
- **Event:** the event type, such as `note.submitted` (design.md §5.4).
- **By:** who did it, by name. `Operator command` is an event with no actor, such as `admin.bootstrap` and
  `admin.signout_all`: the operator ran it. `(no longer on record)` is an actor whose ID no longer matches an account.
- **By ID:** the actor's ID, empty for an operator command. It stays when the name is not found.
- **Participant:** the participant the event was about, given name then family name. It is empty when the event is about
  no participant. `(no longer on record)` is a participant whose records were destroyed.
- **Participant ID:** their ID, empty when there is none. It stays when the name is not found.
- **Entity** and **Entity ID:** the kind of row the event is about, such as `Note`, `Participant` or `User`, and its ID.
- **Entity name:** the name, only when Entity is `User`: `(no longer on record)` if the account is gone, and empty for
  any other kind.
- **Details:** the metadata stored with the event, as stored: JSON that holds changed values, version numbers and
  dates. It never holds note text, and it is not only IDs (see above).
- **Row:** the row's number in the log. Two rows that look alike but have different numbers may be one event.

A name is the name today: it shows what the account or the participant is called now, not what they were called when
the event was written. The old and new values of a change are in Details.

A blank cell is empty, not the word NULL, and a tab or a line break in a name shows as a space. The file has no note
text, no IP address and no count. It does hold personal information (see above), so it goes only to the manager
who asked.

## If it is refused or fails

- **Exit code 1: refused.** The script checks every value before it reads anything, and writes one line saying what is
  wrong: the organisation is not an ID or is not found (copy it from `find-ids.sql` again), a date is not written
  `yyyy-mm-dd` or does not exist, From is after To, or Participant or User is neither an ID nor `any`. An empty value
  is refused as well. Put right what it names and run it again. Delete the file the refused run left, which is empty or
  partial.
- **`sqlcmd` stops saying a scripting variable is not defined:** a `-v` was left out. All five are needed.
- **Error 40613, or a sign-in or firewall error:** the database is paused or the rule is not open yet. See
  `infra/sql/check-database.sql`'s header, wait a minute and run it again.
- **Any other exit code, or a file that fails the check in *Running it*:** the file is not to be used. Delete it and run
  the script again with the same values. If it fails again, do not hand over a part of the answer; tell the manager
  that it failed and that the question is still open.
- **A file with only the header:** the script ran, and nothing matched. Check the dates and the IDs against
  `find-ids.sql` before telling the manager there is nothing in the log.

Release 2 adds the MCP server's events to the log (T38); this file does not cover them yet.
