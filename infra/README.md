# infra

Bicep for the Azure resources (design.md §10.1, §10.5).

- `bootstrap.bicep` (with its module `modules/bootstrap-environment.bicep`) and `locks.bicep`: run by the developer as
  Owner. They hold every `Microsoft.Authorization` resource (role assignments, policy, locks), because the pipeline's
  identity cannot create them. `bootstrap.bicep` is deployed at subscription scope and `locks.bicep` at resource-group
  scope.
- `main.bicep` and the other modules in `modules/`: one environment; the pipeline runs it on every deploy.
  `modules/alerts.bicep` holds the three production alerts and is deployed only when `environmentName` is `prod`.
- `test.bicepparam` and `prod.bicepparam`: the parameters for each environment.
- `sql/grant-identities.sql`: run once as the Entra admin to add the app and deployment identities to the database.
- `sql/check-database.sql`: the operator's read-only script, run as the Entra admin when the operator wants to check
  the identities' database rights and the wrapped key ring. It is not run after each deploy (owner, 20 October 2026).

## Bootstrap (run once, as Owner)

`bootstrap.bicep` creates both environments' resource groups and what goes in them before any deploy: the identities,
the role and policy assignments and the budgets. Run it from the root of a clone of this repository, in Bash (Git Bash
on Windows). The subscription and tenant IDs, the repository's OIDC subject prefix (which holds the GitHub account's
and the repository's numeric IDs), the client IDs, the SQL admin group's object ID and the email addresses stay in the
shell, in Azure and in GitHub, and are never written into the repository (D42, D60, D65, D89).

You need:
- the Azure CLI, signed in (`az login`) with an account that is Owner of the subscription, and its Bicep
  (`az bicep upgrade` installs or updates it);
- the GitHub CLI, signed in (`gh auth login`) as an admin of this repository.

1. Pick the subscription, and check its name and your account:

   ```shell
   az account set --subscription '<subscription name or ID>'
   az account show --query '{subscription: name, signedInAs: user.name}' --output table
   ```

2. Read the repository's OIDC subject prefix into the shell. `gh` fills in `{owner}` and `{repo}` from the clone. Run
   steps 2 to 4 in the same shell, because steps 3 and 4 read it:

   ```shell
   github_subject_prefix=$(gh api repos/{owner}/{repo}/actions/oidc/customization/sub --jq .sub_claim_prefix)
   echo "$github_subject_prefix"
   ```

   It prints `repo:<owner>@<owner id>/<repo>@<repo id>`, because this repository uses GitHub's immutable subjects. Stop
   if it prints nothing or anything of another shape. Steps 3 and 4 pass it, as printed, as `githubSubjectPrefix`.
   Entra ID matches the federated credentials' subjects exactly, so any other value stops the deploys signing in, and
   `bootstrap.bicep` refuses an empty one.

3. Preview the deployment. `read` asks for the budget alerts' address, so it is not saved in your shell history:

   ```shell
   read -rp 'Budget alert email address: ' budget_email
   az deployment sub what-if --name grow2notes-bootstrap --location australiasoutheast \
     --template-file infra/bootstrap.bicep \
     --parameters budgetEmail="$budget_email" githubSubjectPrefix="$github_subject_prefix"
   ```

   On the first run it should create only:
   - at subscription scope, the resource groups `rg-grow2notes-test-ause` and `rg-grow2notes-prod-ause` (and the
     module deployments that fill them);
   - in each group, the identities `id-grow2notes-<env>-deploy` and `id-grow2notes-<env>-app`, one federated credential
     on the deploy identity, four role assignments (Contributor for the deploy identity, three roles for the app
     identity), the policy assignment `allowed-locations` and the budget `budget-grow2notes-<env>`.

   Stop if it shows a change outside the two groups, any role or policy assignment at subscription scope, or a change
   to or deletion of anything that already exists.

4. Deploy, with the same name and location (Azure refuses a deployment name it already holds in another location). It
   prints `Succeeded`:

   ```shell
   az deployment sub create --name grow2notes-bootstrap --location australiasoutheast \
     --template-file infra/bootstrap.bicep \
     --parameters budgetEmail="$budget_email" githubSubjectPrefix="$github_subject_prefix" \
     --query properties.provisioningState --output tsv
   ```

5. Show the four client IDs. When one is needed later, read it from the deployment again rather than saving it:

   ```shell
   az deployment sub show --name grow2notes-bootstrap --query properties.outputs.clientIds.value --output table
   ```

   Each environment's deployment identity client ID is that GitHub environment's `AZURE_CLIENT_ID`. Nothing needs the
   app identities' client IDs copied: `main.bicep` finds each app identity by its name (S00.02.02).

6. Create the GitHub `test` environment, and give it seven secrets, which `deploy.yml` reads (D65):
   - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`: the test deployment identity's client ID and the
     tenant and subscription IDs, which `deploy.yml` passes to `azure/login`'s `client-id`, `tenant-id` and
     `subscription-id`;
   - `SQL_ADMIN_GROUP_NAME` and `SQL_ADMIN_GROUP_OBJECT_ID`: the name and object ID of the Entra security group
     "Grow2Notes SQL admins", the SQL server's Entra admin in both environments, whose only member is the developer
     (D64);
   - `ALERT_EMAIL`: the address the operations alerts are sent to;
   - `EMAIL_SENDER_ADDRESS`: the address the app sends its setup-link email from, on the domain set up in "Email
     through Resend" below.

   `deploy.yml` passes the last four to `main.bicep` as `sqlAdminGroupName`, `sqlAdminGroupObjectId`, `alertEmail` and
   `emailSenderAddress`, in place of the empty placeholders in `test.bicepparam`. `read` asks for the addresses, as in
   step 3:

   ```shell
   # Only if the environment does not exist yet:
   gh api --method PUT repos/{owner}/{repo}/environments/test --silent

   client_id=$(az deployment sub show --name grow2notes-bootstrap \
     --query "properties.outputs.clientIds.value[?environment=='test'].deploymentIdentityClientId" --output tsv)
   gh secret set AZURE_CLIENT_ID --env test --body "$client_id"
   gh secret set AZURE_TENANT_ID --env test --body "$(az account show --query tenantId --output tsv)"
   gh secret set AZURE_SUBSCRIPTION_ID --env test --body "$(az account show --query id --output tsv)"

   # Only if the group does not exist yet, with you as its only member. Creating a group needs an Entra role or tenant
   # setting that allows it; being Owner of the subscription is not enough.
   az ad group create --display-name 'Grow2Notes SQL admins' --mail-nickname grow2notes-sql-admins --output none
   az ad group member add --group 'Grow2Notes SQL admins' \
     --member-id "$(az ad signed-in-user show --query id --output tsv)"

   gh secret set SQL_ADMIN_GROUP_NAME --env test --body 'Grow2Notes SQL admins'
   gh secret set SQL_ADMIN_GROUP_OBJECT_ID --env test \
     --body "$(az ad group show --group 'Grow2Notes SQL admins' --query id --output tsv)"
   read -rp 'Alert email address: ' alert_email
   gh secret set ALERT_EMAIL --env test --body "$alert_email"
   read -rp 'Email sender address, bare or as Grow2Notes <address>: ' email_sender_address
   gh secret set EMAIL_SENDER_ADDRESS --env test --body "$email_sender_address"

   gh secret list --env test
   gh secret list
   ```

   The environment should hold exactly those seven secrets, and neither it nor the repository should hold a client
   secret or a publish profile. The `prod` environment gets the same seven, with the prod row's client ID, and its
   deployment branches restricted to `main`: `ops/go-live.md` has the steps.

### Running it again

Deploys never need the bootstrap. Run it again only when `bootstrap.bicep` or its module changes, with steps 1 to
4. Run steps 2 to 4 in one shell, because step 2 reads the repository's OIDC subject prefix at run time and the later
steps pass it as `githubSubjectPrefix`, which has no default and is never saved (D89). The what-if should show only
the change.

Also pass the budgets' start date, because Azure refuses to change it and the parameter's default is the first of the
current month. It is the first of the month of the first run, which the last run recorded:

```shell
budget_start_date=$(az deployment sub show --name grow2notes-bootstrap \
  --query properties.parameters.budgetStartDate.value --output tsv)
```

Then add `budgetStartDate="$budget_start_date"` after `githubSubjectPrefix="$github_subject_prefix"` in both
commands.

T22 swaps the app identity's role `Communication and Email Service Owner` for `Key Vault Secrets User` at resource-group
scope (D92). The what-if of that re-run should show only one new role assignment, `Key Vault Secrets User`, in each of
the two groups, and nothing else. A deploy in incremental mode never removes the old assignment, so delete it by hand,
in both groups, once the Communication Services resources are gone (`ops/first-use-in-test.md`):

```shell
for environment in test prod; do
  principal_id=$(az identity show --resource-group "rg-grow2notes-$environment-ause" \
    --name "id-grow2notes-$environment-app" --query principalId --output tsv)
  az role assignment delete --assignee "$principal_id" --role 'Communication and Email Service Owner' \
    --resource-group "rg-grow2notes-$environment-ause"
done
```

## Locking the database (run once per environment, as Owner)

`locks.bicep` puts a `CanNotDelete` lock named `grow2notes-database` on the environment's database, `sqldb-grow2notes`
(S00.02.05). It is not part of `main.bicep`, because the deployment identity cannot write a lock. Run it once per
environment, as Owner, after that environment's first deploy has created the database:

```shell
az deployment group create --resource-group rg-grow2notes-<env>-ause --name grow2notes-<env>-locks \
  --template-file infra/locks.bicep --parameters environmentName=<env>
```

Running it again changes nothing. To delete or rename the database on purpose, remove the lock first, as Owner, and run
the command again afterwards.

## Email through Resend

The app sends one kind of email, the setup link, through Resend's HTTP API (D92). The app identity reads Resend's API
key from the environment's Key Vault at each send, as the secret `resend-api-key`. The key is in no template, no app
setting, no GitHub secret and no deployment history: `Email__ApiKeySecretUri` holds only the secret's address, and
`Email__SenderAddress` is the `EMAIL_SENDER_ADDRESS` secret. Everything here is done by hand.

The owner sets up Resend once:
- Verify one sending domain that belongs to Grow2Notes. It is never the parent company's domain or a subdomain of it
  (D42). Both environments share it.
- Add the DNS records Resend gives, and a DMARC record. Turn open and click tracking off for the domain, because click
  tracking would send the setup link through Resend's redirect.
- Create one API key per environment, with the sending-only permission.
- Protect the Resend account with multi-factor sign-in and the fewest members it can have. Resend's dashboard shows the
  messages it sent, including setup links that have not been used yet (A102).

The operator then stores each environment's key. The vault accepts only the web app's subnet, and creating a secret
needs a role that no identity holds, so for the few minutes this takes the operator gives themselves
`Key Vault Secrets Officer` on the vault and adds their address to the vault's firewall, then removes both. A new role
assignment can take a few minutes to apply. `read -s` keeps the key out of the shell's history and the screen, and
`--output none` stops `az` printing the value back:

```shell
vault=kv-grow2notes-<env>
my_ip=$(curl -fsS --max-time 10 https://api.ipify.org)
vault_id=$(az keyvault show --name "$vault" --query id --output tsv)
me=$(az ad signed-in-user show --query id --output tsv)
az role assignment create --assignee "$me" --role 'Key Vault Secrets Officer' --scope "$vault_id" --output none
az keyvault network-rule add --name "$vault" --ip-address "$my_ip" --output none

read -rsp 'Resend API key for <env>: ' resend_api_key; echo
az keyvault secret set --vault-name "$vault" --name resend-api-key --value "$resend_api_key" --output none
unset resend_api_key

az keyvault network-rule remove --name "$vault" --ip-address "$my_ip" --output none
az role assignment delete --assignee "$me" --role 'Key Vault Secrets Officer' --scope "$vault_id"
```

Then set the sender, a bare address or `Grow2Notes <address>` on the verified domain, with `read` as in the bootstrap's
step 6:

```shell
read -rp 'Email sender address: ' email_sender_address
gh secret set EMAIL_SENDER_ADDRESS --env <env> --body "$email_sender_address"
```

Until `EMAIL_SENDER_ADDRESS` is set, a deploy stops at its what-if, before any change, because `app.bicep` refuses an
empty address. Until the secret exists, or while the app identity lacks `Key Vault Secrets User`, the app is
unaffected except that each send answers "Not sent".

To rotate the key, create a new one in Resend, store it with `az keyvault secret set` as above, which adds a version
the app reads at its next send, and then revoke the old key in Resend.
