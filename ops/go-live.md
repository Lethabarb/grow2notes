# Go-live run sheet

The operator's ordered run sheet for production, `https://app-grow2notes-prod.azurewebsites.net` (D91). It sets
production up, checks it against design.md §10.1 before any real data goes in, tries its alerts and email, and lists
the go-live lines of design.md §14. Nothing here is a work item or holds a task open (D90): every step is by hand, an
agent never runs one, and each is the operator's or the owner's. The Azure subscription is shared with another
production system, so every command here stays inside the two Grow2Notes resource groups, apart from the read-only
Defender check in B.

Rules for everyone who works through it:

- Work in Git Bash from the root of a clone of `main`, with the Azure CLI signed in and the GitHub CLI signed in as an
  admin of the repository. Steps marked **Owner** need the Azure Owner role on the subscription; the rest need only
  your own sign-in and the roles it names.
- Each check ends with a `Result:` line. Fill it in with the date, `pass` or `fail`, and who did it. Paste no raw
  output: it holds IDs and addresses, which the repository never keeps (D60, D89). Never write down a key, a setup link,
  a token, a prefix, an ID or an address. A `fail` is written as one plain sentence and told to the developer.
- Do the sections in order. A needs production not to exist yet; B to E assume A is done. C changes production on
  purpose, so do it before any real use.
- Names: `rg-grow2notes-prod-ause`, `app-grow2notes-prod`, `sql-grow2notes-prod`, `sqldb-grow2notes`,
  `kv-grow2notes-prod`, `vnet-grow2notes-prod`, `id-grow2notes-prod-app` and `id-grow2notes-prod-deploy`
  ([break-glass.md](break-glass.md) lists them for the SSH console).

## A. Set up, as Owner

### A1. The names are free (read-only)

`appOrigin` fixes the production host, and so the passkey domain (D91, A101), so if the web app's name is taken, stop:
the owner decides what to do, and nothing below is run.

```shell
subscription_id=$(az account show --query id --output tsv)
base="https://management.azure.com/subscriptions/$subscription_id/providers"
az rest --method post --url "$base/Microsoft.Web/checknameavailability?api-version=2023-12-01" \
  --body '{"name": "app-grow2notes-prod", "type": "Microsoft.Web/sites"}'
az rest --method post --url "$base/Microsoft.Sql/checkNameAvailability?api-version=2021-11-01" \
  --body '{"name": "sql-grow2notes-prod", "type": "Microsoft.Sql/servers"}'
az rest --method post --url "$base/Microsoft.KeyVault/checkNameAvailability?api-version=2023-07-01" \
  --body '{"name": "kv-grow2notes-prod", "type": "Microsoft.KeyVault/vaults"}'
```

Each answer is a name, whether it is available (`nameAvailable` or `available`) and a reason. Look for `true` three
times. A name that is taken by a Key Vault deleted with purge protection is unavailable
until its retention ends. The portal's name check on each resource type does the same if an API version here is
refused.

Result: ____ (date), ____ (pass or fail), by ____

### A2. The `prod` GitHub environment

Restrict deployments to `main`, add no required reviewer (A103: the repository is public, so the branch rule is
available, and the dispatch from `main` is the approval), and give it the same seven secrets as `test`, with the prod
deployment identity's client ID (`infra/README.md`, bootstrap step 6). Create the environment with its branch rule
first, so that GitHub never makes it without one:

```shell
gh api --method PUT repos/{owner}/{repo}/environments/prod --input - <<'EOF'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
gh api --method POST repos/{owner}/{repo}/environments/prod/deployment-branch-policies \
  --raw-field name=main --raw-field type=branch --silent

client_id=$(az deployment sub show --name grow2notes-bootstrap \
  --query "properties.outputs.clientIds.value[?environment=='prod'].deploymentIdentityClientId" --output tsv)
gh secret set AZURE_CLIENT_ID --env prod --body "$client_id"
gh secret set AZURE_TENANT_ID --env prod --body "$(az account show --query tenantId --output tsv)"
gh secret set AZURE_SUBSCRIPTION_ID --env prod --body "$(az account show --query id --output tsv)"
gh secret set SQL_ADMIN_GROUP_NAME --env prod --body 'Grow2Notes SQL admins'
gh secret set SQL_ADMIN_GROUP_OBJECT_ID --env prod \
  --body "$(az ad group show --group 'Grow2Notes SQL admins' --query id --output tsv)"
read -rp 'Alert email address: ' alert_email
gh secret set ALERT_EMAIL --env prod --body "$alert_email"
read -rp 'Email sender address, bare or as Grow2Notes <address>: ' email_sender_address
gh secret set EMAIL_SENDER_ADDRESS --env prod --body "$email_sender_address"
```

Look for `gh secret list --env prod` naming exactly those seven, no client secret or publish profile in the repository's
secrets, and, in the repository's Settings, Environments, `prod` with *Deployment branches and tags* set to `main` and
no required reviewer. `EMAIL_SENDER_ADDRESS` is an address on the domain the owner verified in Resend (first-use step
151), which both environments share.

Result: ____ (date), ____ (pass or fail), by ____

### A3. The bootstrap is current, and the old email role is gone

First-use step 151 re-ran the bootstrap for both groups from the T22 branch, which gave the app identity
`Key Vault Secrets User` in the prod group too. If step 151 has not been done, do it now (`infra/README.md`, *Running it
again*, steps 1 to 4, passing the GitHub subject prefix at run time). Then delete the old
`Communication and Email Service Owner` assignment in the prod group, with the loop in *Running it again* limited to
`prod`. Look for the prod group's role assignments for `id-grow2notes-prod-app` being Key Vault Crypto Service
Encryption User, Key Vault Secrets User and Monitoring Metrics Publisher, and no Communication or Email role.

Result: ____ (date), ____ (pass or fail), by ____

### A4. The production Resend key (Owner of the Resend account)

In Resend, create a sending-only API key for production on the shared verified domain (A100). Confirm tracking is off
for the domain and that the account has multi-factor sign-in and the fewest members it can have (A102). Keep the key
for A7 only; it is never written down.

Result: ____ (date), ____ (pass or fail), by ____

### A5. Pick the CI run

Pick a CI run of a push to `main`, made after T22 merged, whose test deploy succeeded (the `Check the CI run` job
accepts nothing else), and no more than 90 days old, the time CI keeps its artifacts. The newest successful test deploy
is the usual choice:

```shell
gh run list --workflow deploy.yml --event workflow_run --status success --limit 10 \
  --json displayTitle,createdAt --jq '.[] | "\(.createdAt)  \(.displayTitle)"'
```

A title reads `Deploy CI run <id> attempt <n> to test`. The ID is the CI run's; the attempt must be that CI run's
current one, which is what the title records when the run was not repeated afterwards. Use the same CI run ID in A6 and
A9. Quiet-hours rule: the owner and the managers agree the time of A9 now (A35).

Result: ____ (date), ____ (pass or fail), by ____

### A6. Deploy `main.bicep` by hand, once

The key vault and the SQL server exist only after `main.bicep`, and `grant-identities.sql` must run before any
migration, so the first deploy is made by hand under the name the pipeline reuses, as S00.02.02 did for test. Use the
chosen run's artifact, so production gets exactly what test ran:

```shell
ci_run=<the CI run ID from A5>
gh run download "$ci_run" --name grow2notes --dir artifacts
read -rp 'Alert email address: ' alert_email
read -rp 'Email sender address: ' email_sender_address
deployment=(
  --resource-group rg-grow2notes-prod-ause
  --name grow2notes-prod-main
  --template-file artifacts/infra/main.json
  --parameters @artifacts/infra/prod.parameters.json
  --parameters sqlAdminGroupName='Grow2Notes SQL admins'
    sqlAdminGroupObjectId="$(az ad group show --group 'Grow2Notes SQL admins' --query id --output tsv)"
    alertEmail="$alert_email" emailSenderAddress="$email_sender_address"
  --only-show-errors
)
az deployment group what-if "${deployment[@]}"
az deployment group create "${deployment[@]}" --output none
```

Look for the what-if creating the resources of design.md §10.1 (including the three alerts and the long-term retention
policy) and nothing outside the prod group, then the create succeeding. If the key creation is refused by the vault's
firewall, that is the precedent of 7 October 2026 and the template already allows it. The web app has no build yet, so
its health check fails: the health-check alert fires until A9 and that is expected. The app's settings must already
name `Email__ApiKeySecretUri` and `Email__SenderAddress`.

**Owner's decision before this step:** `ops/security-review.md` F-7 says a 3072-bit Key Vault wrapping key is best made
when production is first deployed. Decide now whether to ask for it. If yes, stop and ask the developer for the change
to `keyvault.bicep` first. If no, the finding stays accepted.

Result: ____ (date), ____ (pass or fail), by ____ (F-7 decision: ____)

### A7. Store the Resend key in `kv-grow2notes-prod`

Follow `infra/README.md`, *Email through Resend*, for `kv-grow2notes-prod`: give yourself Key Vault Secrets Officer on
the vault, add your address to its firewall, `read -s` the key from A4 into `az keyvault secret set --name
resend-api-key`, then remove both. While your address is still allowed, list the vault's contents by name:

```shell
az resource list --resource-group rg-grow2notes-prod-ause --resource-type Microsoft.KeyVault/vaults/keys --query '[].name' --output tsv
az keyvault secret list --vault-name kv-grow2notes-prod --query '[].name' --output tsv
```

Look for one key, `data-protection` (listed through Resource Manager, because your role covers secrets only; the name
shows as `kv-grow2notes-prod/data-protection`), and one secret, `resend-api-key`. Then look for the firewall rule and
your role assignment gone.

Result: ____ (date), ____ (pass or fail), by ____

### A8. Grant the identities, before any migration

Run `infra/sql/grant-identities.sql` with `-v Environment=prod` against `sql-grow2notes-prod`, as a member of the Entra
group "Grow2Notes SQL admins". Its header has the `sqlcmd` (Go) command and the steps around it: add a firewall rule
for your own address first, and delete it straight after even if the script fails. If `sqlcmd` (Go) is not installed, as
on 7 and 9 October 2026, use the same .NET file-based script as then, which sends the file unchanged apart from the
`$(Environment)` substitution. Do this before A9, because the first migration needs the deployment identity to be in
`db_owner`.

Look for the script finishing without error, and the firewall rule gone (`az sql server firewall-rule list` shows none).

Result: ____ (date), ____ (pass or fail), by ____

### A9. The first production deploy

At the time agreed in A5, outside the usual note-writing hours (A35), dispatch Deploy from `main` with the CI run from
A5:

```shell
gh workflow run deploy.yml --ref main --field ci-run="$ci_run"
gh run list --workflow deploy.yml --limit 3
```

Look for a run titled `Deploy CI run <id> to prod` whose `Check the CI run` job and `Deploy to prod` job both succeed,
and whose log shows the migration applying and the smoke test passing. A 5xx alert around the deploy is expected (the
restart), and the health-check alert from A6 resolves. If the deploy fails, nothing is rolled back for you: read the log
and `restore-drill.md`, and tell the developer.

Result: ____ (date), ____ (pass or fail), by ____

### A10. The delete lock

Put `CanNotDelete` on the production database, as Owner, once the first deploy has created it (S00.02.04's fifth
criterion). Follow `infra/README.md`, *Locking the database*, with `<env>` as `prod`:

```shell
az deployment group create --resource-group rg-grow2notes-prod-ause --name grow2notes-prod-locks \
  --template-file infra/locks.bicep --parameters environmentName=prod
```

Look for the deployment succeeding and `az lock list --resource-group rg-grow2notes-prod-ause` showing
`grow2notes-database` as `CanNotDelete`. Do not try to delete anything in production. Test has already tried the server
delete and the rename (first-use step 155).

Result: ____ (date), ____ (pass or fail), by ____

### A11. Refusal checks

None of these changes anything, and none reaches Azure.

1. Dispatch with the ID of a pull request's CI run (`gh run list --workflow ci.yml --event pull_request --limit 1 --json
   databaseId --jq '.[0].databaseId'`). Look for the run failing at `Check the CI run` and `Deploy to prod` not
   starting.
2. Dispatch with the ID of a CI run that has no successful test deploy of its current attempt, such as one from before
   T22 merged. Look for the same failure, naming the title it did not find.
3. Dispatch from another branch (`gh workflow run deploy.yml --ref <branch>`). Look for the refusal at the ref test in
   `Check the CI run` or, on a branch without it, by the `prod` environment's rule that only `main` may deploy. In
   neither case does the job reach Azure.

(S00.02.04's first and second criteria.)

Result: ____ (date), ____ (pass or fail), by ____

## B. Read-only checks

Each check names what to look for and nothing else. Run them after A, and again whenever production changes.

1. **Every §10.1 row exists with its settings.** List the group and compare it with design.md §10.1:
   `az resource list --resource-group rg-grow2notes-prod-ause --query '[].{name:name, type:type}' --output table`.
   Look for the identity, plan, web app, virtual network, SQL server and database, Key Vault, Log Analytics and
   Application Insights, the action group, and the three alerts, and no resource that §10.1 does not list.
   Then the web app: `az webapp config show --resource-group rg-grow2notes-prod-ause --name app-grow2notes-prod
   --query '{tls: minTlsVersion, alwaysOn: alwaysOn, health: healthCheckPath, ftps: ftpsState}'` (TLS 1.2, always on,
   `/healthz/ready`; FTP deployment off or FTPS only), and the SQL server: `az sql server show --resource-group
   rg-grow2notes-prod-ause --name sql-grow2notes-prod --query '{tls: minimalTlsVersion, public: publicNetworkAccess,
   entraOnly: administrators.azureAdOnlyAuthentication}'` (TLS 1.2, `Enabled` with only the VNet rule, Entra only), and
   the vault: `az keyvault show --name kv-grow2notes-prod --query '{rbac: properties.enableRbacAuthorization, soft:
   properties.enableSoftDelete, purge: properties.enablePurgeProtection, firewall:
   properties.networkAcls.defaultAction}'` (RBAC, soft delete and purge protection on, default action `Deny`).
   (S06.05.01's first criterion.)

   Result: ____ (date), ____ (pass or fail), by ____

2. **The database lock:** `az lock list --resource-group rg-grow2notes-prod-ause --query '[].{name: name, level:
   level}'` shows `grow2notes-database` as `CanNotDelete`. (S00.02.04's fifth criterion, S06.05.01.)

   Result: ____ (date), ____ (pass or fail), by ____

3. **FTP and basic-auth publishing are off.** For `ftp` and again for `scm`:
   `az resource show --resource-group rg-grow2notes-prod-ause --namespace Microsoft.Web --parent
   sites/app-grow2notes-prod --resource-type basicPublishingCredentialsPolicies --name ftp --query properties.allow`
   answers `false`.

   Result: ____ (date), ____ (pass or fail), by ____

4. **The federated credential trusts only `environment:prod`.**
   `az identity federated-credential list --resource-group rg-grow2notes-prod-ause --identity-name
   id-grow2notes-prod-deploy --query '[].subject' --output tsv` lists one subject, which ends in `:environment:prod`
   (its start holds the GitHub IDs: do not copy it). No other credential exists on either production identity.
   (S00.02.04's third criterion, S06.05.01.)

   Result: ____ (date), ____ (pass or fail), by ____

5. **Only production identities hold roles in the production group,** and no test identity does:

   ```shell
   rg=rg-grow2notes-prod-ause
   prod_ids=$(az identity list --resource-group $rg --query '[].principalId' --output tsv)
   test_ids=$(az identity list --resource-group rg-grow2notes-test-ause --query '[].principalId' --output tsv)
   for principal in $(az role assignment list --resource-group $rg --query '[].principalId' --output tsv | sort -u); do
     if grep -qx "$principal" <<< "$prod_ids"; then echo prod
     elif grep -qx "$principal" <<< "$test_ids"; then echo TEST
     else echo other; fi
   done | sort | uniq -c
   ```

   Look for no `TEST` line. `other` is a person or group with a role of their own; there should be few and you should
   know each. (S00.02.04's third criterion.)

   Result: ____ (date), ____ (pass or fail), by ____

6. **Defender for Cloud's free posture checks.** `az security pricing list --query '[].{plan: name, tier: pricingTier}'
   --output table` is subscription-wide and read-only: Grow2Notes needs no paid plan (design.md §10.1). Any paid plan
   listed belongs to the other workloads on the subscription, so change nothing; note only whether Grow2Notes turned
   one on. (S06.05.01's first criterion.)

   Result: ____ (date), ____ (pass or fail), by ____

7. **The database roles.** Run `infra/sql/check-database.sql` as the Entra admin, the way A8 runs
   `grant-identities.sql` (a temporary firewall rule, deleted straight after). Look for check A, the app identity in
   `grow2notes_runtime` and no other role with no DDL right, and check B, the deployment identity in `db_owner`, both
   `PASS`, and for the summary row to report no `FAIL`. Check C, the key ring wrapped by Key Vault, passes once the app
   has served a request. (S06.05.01's second criterion, S00.02.04.)

   Result: ____ (date), ____ (pass or fail), by ____

8. **Backups.** All three must read as follows:
   - `az sql db str-policy show --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod --name
     sqldb-grow2notes --query retentionDays` is `35`;
   - `az sql db ltr-policy show --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod --database
     sqldb-grow2notes --query '{monthly: monthlyRetention, weekly: weeklyRetention, yearly: yearlyRetention}'` is
     `P12M`, `PT0S` and `PT0S`;
   - `az sql db show --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod --name sqldb-grow2notes
     --query requestedBackupStorageRedundancy` is `Geo`.

   (S06.03.01's second criterion.)

   Result: ____ (date), ____ (pass or fail), by ____

9. **No drift.** Run the what-if of A6 again, with the same arguments and the artifact of the last production deploy.
   Look for no create, modify or delete of a setting that §10.1 lists. Azure sometimes reports properties it adds
   itself as changes; if the long-term policy shows `weekOfYear`, the fix is `weekOfYear: 0` in `sql.bicep` (T22's
   Notes), and if the database SKU shows a change, `databaseSku` in `prod.bicepparam` is in the wrong shape. Tell the
   developer in either case. (S06.03.01's first criterion.)

   Result: ____ (date), ____ (pass or fail), by ____

10. **The origin.** `az webapp config appsettings list --resource-group rg-grow2notes-prod-ause --name
    app-grow2notes-prod --query "[?name=='App__Origin'].value" --output tsv` is
    `https://app-grow2notes-prod.azurewebsites.net`, which fixes the passkey domain (D91). **Tell the owner:** if a
    custom domain will ever be added to the web app, add it before the first real person enrols a passkey, because
    passkeys enrolled before it stop working and those people enrol again (A101). The settings must also name
    `Email__ApiKeySecretUri` and `Email__SenderAddress` and hold no key. (S06.05.01's third criterion, in part: the
    default host and `App__Origin` are checked here, and the passkey prompt in D.)

    Result: ____ (date), ____ (pass or fail), by ____ (owner told: ____)

## C. Alerts, before go-live, with production unused

Alerts hold resource metrics only (§9.5). Do C after A10 and before any real use. The alerts are in
`modules/alerts.bicep`: HTTP 5xx (any in 5 minutes), health check (average below 50 over 15 minutes), and database load
(DTU average above 80 over 15 minutes), each evaluated every 5 minutes and resolving by itself.

### C1. The action group

In the Azure portal, open the action group `ag-grow2notes-prod`, choose *Test action group*, pick the sample type
*Metric alert*, and run it. Look for a sample alert email at the operations address within a few minutes, naming the
group and no participant data. (S06.04.01's second criterion.)

Result: ____ (date), ____ (pass or fail), by ____

### C2. A health-check failure fires, and resolves

Make the check fail without any change to the app:

```shell
subnet_id=$(az network vnet subnet show --resource-group rg-grow2notes-prod-ause --vnet-name vnet-grow2notes-prod \
  --name snet-app --query id --output tsv)
az sql server vnet-rule delete --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod --name app-subnet
az webapp restart --resource-group rg-grow2notes-prod-ause --name app-grow2notes-prod
```

`/healthz/ready` includes a database check, so with the rule gone it fails. Wait **at least 25 minutes** (a 15-minute
window, evaluated every 5 minutes, and the alert email's delay): look for an email that
`alert-grow2notes-prod-health-check` fired. The 5xx alert may fire too. Then put the rule back, either with
`az sql server vnet-rule create --resource-group rg-grow2notes-prod-ause --server sql-grow2notes-prod --name app-subnet
--subnet "$subnet_id"` or by dispatching Deploy again as in A9, which recreates it, and restart the web app. Wait for
the alerts to resolve (another window, so about 15 to 20 minutes) and look for the resolved emails. Finally open
`/healthz/ready` and look for `200`. (S06.04.01's third criterion.)

**Known blind spot, not a defect:** a stopped site emits no `HealthCheckStatus`, so it raises no health-check alert. The
5xx alert and the operator's own checks are what notice that. No availability test was added, because nobody asked for
one.

Result: ____ (date), ____ (pass or fail), by ____

## D. Email

### D1. The first invite passes SPF, DKIM and DMARC

At T27's first invite (the first manager's setup, S06.05.02), send the message to an address the operator reads and look
at the received headers (Show original in Gmail, View message source in Outlook.com): `spf=pass`, `dkim=pass` and
`dmarc=pass` in `Authentication-Results`, sent from the address in the `prod` environment's `EMAIL_SENDER_ADDRESS`,
and the link in the text being the plain `https://app-grow2notes-prod.azurewebsites.net/setup#…` link, not a Resend
tracking redirect. The first invite needs the first manager, so it cannot be sent earlier. (S06.05.01's fourth
criterion.)

Result: ____ (date), ____ (pass or fail), by ____

### D2. The passkey prompt

At T27's first enrolment, the first manager sets up with a passkey: the browser's prompt names
`app-grow2notes-prod.azurewebsites.net` and not the parent company. (S06.05.01's third criterion, D91.)

Result: ____ (date), ____ (pass or fail), by ____

### D3. Resend, checked by the owner

The owner confirms, in Resend and with the provider (A102):

- the region where Resend processes email, and that the privacy policy and the hosting agreement say that the setup-link
  email (a staff member's address, the subject and the link) is not processed in Australia;
- how long the plan keeps sent content (the pricing page gave 30 days of searchable logs on 25 October 2026), and that
  setup links not yet used can be seen in its dashboard, so the account has multi-factor sign-in and the fewest members;
- that the production key is sending-only, and that the old test key and any key no one uses are revoked;
- the wording of the privacy policy and of the hosting agreement's sub-processor list (design.md §12, §14).

Result: ____ (date), ____ (pass or fail), by ____

## E. The go-live lines (design.md §14)

These are the ten go-live lines of design.md §14, for the owner and the provider. They need people, so they are not
work items. Tick each with its evidence and owner on the provider's own copy of this sheet, not in the repository.

1. The privacy policy is updated (APP 1, HPP 5), including Resend as the sub-processor that sends the setup-link email.
2. The collection notice and consent wording are checked (APP 5, HPP 1.4, Practice Standards).
3. The data breach response plan names Grow2Notes and the operator's contact ([breach-response.md](breach-response.md)).
4. A written hosting agreement between the provider and the developer's business is signed, naming Microsoft and Resend
   as sub-processors (A102).
5. Every staff member has accepted their invite and set up MFA on their own phone, and at least two managers are
   active.
6. Staff onboarding (about 30 minutes) is done.
7. Participants, goals, common items (in their groups) and guide prompts are entered by one manager and checked by
   another.
8. The restore test is done: restore the latest production backup to a scratch database, open a known note and its
   history, record the date and result, then delete the copy ([restore-drill.md](restore-drill.md)).
9. Every historical Word note is gathered into organisation-controlled storage (D41), checked, and removed from the
   personal devices (A39).
10. Where the gathered Word notes are kept is written down, and the Word template stops being used from the switch-over
    date.

Also for the managers to arrange: the button-words check with five workers (S06.02.05, cut), which microcopy.md §8 asks
for before go-live. Each of five workers, some of whom write in English as an additional language, says what every
main button will do before pressing it, in test with made-up data (first-use step 23). Any button that fewer than four
of the five understood goes to the owner as a wording question, and no wording changes without the owner's approval.
Whether go-live waits for it is the owner's call (Q12).

Result: ____ (date), ____ (pass or fail), by ____
