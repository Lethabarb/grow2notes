# Breach response

What to do when there are reasonable grounds to suspect that participants' information has been seen or taken by
someone who should not have it. It follows the parts of design.md
[§9.10](../design/design.md#910-breach-response-notifiable-data-breaches-scheme): contain, work out what was exposed,
preserve evidence and prepare in advance. The operator commands it uses are in
[§7.4](../design/design.md#74-operator-commands) and [break-glass.md](break-glass.md). It follows the sections of
break-glass.md as far as they fit.

The names of people, contact details, extracts and filled-in records are never written in this file, which is in a
public repository. They live in the provider's own records storage ([A89](../design/design.md#13-assumed-defaults)).

## The legal position

Participants' health information is covered by the Privacy Act's notifiable data breaches scheme. The **provider** (the
organisation using Grow2Notes, not the operator) must take reasonable steps to complete an assessment within 30 days of
suspecting an eligible breach. If the breach is eligible, the provider notifies the Office of the Australian
Information Commissioner (OAIC) and the people affected, as soon as practicable, with the OAIC's notifiable data
breach form. The provider also considers its own NDIS Commission and insurer obligations, which are outside the app.

The assessment and any notification are the provider's. **The operator tells the provider without delay** when the
operator suspects a breach, and does not wait to be sure.

## Who decides, and who supports

- **The managers decide:** what is done, whether the breach is eligible, who is told and when.
- **The operator supports:** contains, preserves evidence, finds out what the records show, and keeps the decisions
  table up to date for the managers. The operator's contact details are given to the provider in writing, for the
  provider's own plan, and are kept in the provider's records storage. They are not written here.

## Prepare in advance

Done once, before go-live, and kept true:

- The operator gives the provider its contact details in writing, and the provider keeps them in its records storage
  with its own plan.
- The managers choose the place in their records storage for this file's records, and for the request record, and
  confirm that the operator can reach it ([A89](../design/design.md#13-assumed-defaults)).
- The managers have read this file and [audit-extraction.md](audit-extraction.md), so that they know what the log
  can and cannot say.
- The operator can sign in as the SQL Entra admin and open the SSH console, and has used `sqlcmd` (Go) with the
  scripts in test.

## When to use it

- **At the first reasonable ground to suspect a breach:** a lost or stolen device that was signed in, a sign-in or
  link in the wrong hands, a person who should not have access seeing records, or an Azure account or key that may have
  been exposed. Use it when in doubt: the early steps cost little.
- **In test,** only on made-up data (A35).

## What to have ready

- The decisions table below, copied into the managers' records storage as a new file for this incident, with the first
  row written as soon as the suspicion is raised.
- Signed in to Azure with `az login` as a member of the Entra group "Grow2Notes SQL admins" (D64), `sqlcmd` (Go), and a
  clone of this repository, as [audit-extraction.md](audit-extraction.md) lists.
- The request record for audit extracts (see [audit-extraction.md](audit-extraction.md)), in the same records storage.
- The place in the records storage for this incident's files, where the managers can reach them.

## Running it

Do the parts in this order. Contain first. Preserve evidence before any clean-up. Write each decision and action in
the decisions table as it happens, with the time and the role that decided.

### Contain

1. **A manager deactivates the accounts affected,** on the user's page in Users. Deactivating a person rotates their
   security stamp and locks the account, so their sessions end on their next request. It writes a `user.deactivated`
   event. A manager cannot deactivate the organisation's only active manager (A26): make another manager first, or run
   step 2.
2. **The operator runs `admin signout-all`** when it is not known whose session is in the wrong hands, or to end every
   session at once. It is in [break-glass.md](break-glass.md#admin-signout-all): follow it there, and tell the managers
   of each organisation first, as it says. It is not repeated here.
3. **Stop the production web app if the managers decide it is needed,** so that nothing more can be read or written:

   ```shell
   az webapp stop --resource-group rg-grow2notes-prod-ause --name app-grow2notes-prod
   ```

   Nobody can write notes while it is stopped, so the managers decide and the table records it. Start it again with
   `az webapp start` and the same two options once the managers decide it is safe, and check `/healthz/ready`.

### Preserve evidence, before any clean-up

Do these before resetting a sign-in, changing a setting, removing a firewall rule or deleting anything. Everything
goes into the records storage for the incident, and no local copy is kept.

1. **Export the relevant audit rows** with `ops/audit-extract.sql`, as [audit-extraction.md](audit-extraction.md) says.
   Add a row to the request record for each extract, with *Why* as a breach assessment. Extract more than one thing
   if needed: what the suspected account did, and what was done to each participant concerned, for the period from
   before the breach was possible to now.
2. **Export the application logs** (Application Insights and Log Analytics, `appi-grow2notes-prod`) for the same
   period. They keep 30 days (design.md [§10.1](../design/design.md#101-resources-production)), so do it at once.
3. **Export the Azure Activity Log** for the resource group `rg-grow2notes-prod-ause`, from the Azure portal, for the
   same period. It shows who changed the Azure resources, such as a firewall rule, a role or a setting. Azure keeps it
   for a limited time, so do it at once.
4. **Make a point-in-time copy of the database,** the way [restore-drill.md](restore-drill.md) makes one in step 1 of
   the quarterly drill, with a restore point and a name for this purpose (the restore point is inside the last 35
   days). The copy holds everything the database held, the audit rows' IP addresses included. Write the copy's name and
   restore point (UTC) in the decisions table, and not in [restore-log.md](restore-log.md). Only the operator reads
   it, and it is never attached to an app. **Keep it until the assessment ends,** then delete it as step 7 of the drill
   says, and record that in the table.

Do not run `retention-destroy.sql` while an assessment is open.

### Work out what was exposed

- **The audit log** shows what an account did that is recorded: sign-ins, note writes, flag reviews, changes to users
  and configuration, report downloads and participant record exports. [audit-extraction.md](audit-extraction.md) says
  how to read it, and what it does not hold.
- **The log does not record opening a note or a participant's history.** Every user can read every participant's notes
  in their organisation (D20), so **a misused account is assumed to have exposed every participant's notes**, unless
  other evidence narrows it ([A28](../design/design.md#13-assumed-defaults)). Never tell a manager that the log shows
  that a note was not read.
- **The Azure Activity Log** shows changes to the infrastructure, and the application logs show requests and errors.
- **IP addresses** are in the audit rows in the database, and so in the point-in-time copy. The extract leaves them
  out. If the assessment needs them, change `audit-extract.sql` by pull request, as audit-extraction.md says.

Give the managers what was found, in writing and in the records storage, as it is found.

## AI-assistant access

Use this section as well as the parts above when the suspicion is about an AI assistant that managers or Support use
through the MCP server ([mcp-server.md §11.5](../design/mcp-server.md)): a signed-in device or an account of an
assistant in the wrong hands, a wrong change made through an assistant, or a breach notice from an AI provider. An
assistant never receives note text (D49). What it could read is what the MCP look-ups return: participants' names and
goal wording, common items, guide prompts and staff names and emails.

### Contain, in this order

Stop at the first way that is enough. Each of the four is a decision for the managers, and the decisions table records
it.

1. **Turn the access off.** Set `Mcp__Enabled` to `false` on the production web app, as the app setting the Bicep file
   writes from its `mcpEnabled` parameter:

   ```shell
   az webapp config appsettings set --resource-group rg-grow2notes-prod-ause --name app-grow2notes-prod \
     --settings Mcp__Enabled=false
   ```

   The app restarts. Every MCP and OAuth path then answers `404`, so no assistant can connect or refresh, and the rest
   of the app is as before. Check that `/mcp` answers `404`. **The setting lasts only until the next deploy,** which
   writes the `mcpEnabled` parameter of `infra/main.bicep` back. So **hold production deploys** until a pull request
   sets `mcpEnabled = false` in `infra/prod.bicepparam` and is deployed, and turn the access on again only by a pull
   request that sets it to `true`, with the managers' agreement. This step does not wait for the evidence below: the
   audit rows are in the database and the setting does not touch them. Export the application logs at once
   (*Preserve evidence*, step 2), because they keep 30 days.
2. **Remove the assistant's registration,** with `admin mcp-client remove`
   ([break-glass.md](break-glass.md#admin-mcp-client)). It deletes the registration with the authorisations and tokens
   it issued, so that assistant can neither connect nor refresh for anyone, and other assistants are untouched. Use it
   when the fault is with one assistant and the access can stay on for the others.
3. **A manager deactivates the affected account,** as in step 1 of *Contain* above. The person's next call through any
   assistant is refused, and their authorisations are revoked.
4. **The operator runs `admin signout-all`** ([break-glass.md](break-glass.md#admin-signout-all)) when it is not known
   whose account is affected, as in step 2 of *Contain* above. It ends every session and every assistant connection in
   every organisation.

### Assess

Run `ops/audit-extract.sql` with `Via=mcp`, as [audit-extraction.md](audit-extraction.md) says, for the period from
before the breach was possible to now, and add a request row for each extract. With `Via=mcp` it keeps what was done
through an assistant (the rows with `"via": "mcp"` in Details) and the `mcp.*` events, among them when a person
connected an assistant or refused to. The `mcp.read` rows show what was disclosed: each look-up's tool and the IDs and
number of what it returned. Each row's `client` in Details names the assistant that received it. The `user.*` and
`participant.*` rows show what was changed. Use `Participant` or `User` with it to narrow the question. The log has
the same limits as before: it does not say what a person did with the answer in the assistant.

A burst of `429` answers on `/oauth/token` in the monthly Log Analytics query
([audit-extraction.md](audit-extraction.md#monthly-assistant-figures)) is a known, accepted signal and not a breach:
client IDs are published, so anyone can use up an assistant's count, which exposes nothing and stops when the sender
stops.

### Notify

- **Ask the affected person to delete the relevant chats** in the assistant, so that what was returned does not stay
  in their history.
- **The provider assesses under the notifiable data breaches scheme** as in *The legal position*, and that includes a
  breach at an AI provider: the provider stays accountable for information it disclosed to an AI provider overseas
  (Privacy Act s 26WC; [design.md §9.10](../design/design.md#910-breach-response-notifiable-data-breaches-scheme)).
- **The operator passes on any breach notice from its own AI provider without delay,** whenever the Support account
  connected through it ([mcp-server.md §7.5](../design/mcp-server.md)).

## The decisions table

The managers keep the filled-in copy in their records storage, never in this repository. One row for each decision or
action, from the first suspicion to the end of the assessment.

| Date and time | Decision or action | Decided by (role) | Notes |
|---|---|---|---|
| | | | |

Rows to expect: when the provider was told; each account deactivated; `admin signout-all` run; the web app stopped and
started; for AI-assistant access, `Mcp__Enabled` set to `false` and the deploy hold lifted, and each registration
removed; each extract, log export and Activity Log export, with its place in the records storage; the point-in-time
copy's name, restore point and deletion; the assessment's outcome; and whether the OAIC and the people affected were
told.

## If it is refused or fails

- **An extract is refused or fails:** see *If it is refused or fails* in [audit-extraction.md](audit-extraction.md).
  Evidence is not lost by waiting a few minutes, but the application logs age out, so export them first.
- **The point-in-time restore is refused:** the restore point must be inside the last 35 days and the copy's name must
  not exist already. See *If it fails* in [restore-drill.md](restore-drill.md).
- **`admin signout-all` fails:** see its *If it is refused or fails* in
  [break-glass.md](break-glass.md#admin-signout-all).
  Deactivating the affected accounts and stopping the web app still contain the breach.
- **A step here did not work as written:** tell the managers, finish the containment another way, and fix this file by
  pull request afterwards.
