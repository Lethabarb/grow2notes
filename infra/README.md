# infra

Bicep for the Azure resources (design.md §10.1, §10.5).

- `bootstrap.bicep` (with its module `modules/bootstrap-environment.bicep`) and `locks.bicep`: run by the developer as
  subscription Owner. They hold every `Microsoft.Authorization` resource (role assignments, policy, locks), because the
  pipeline's identity cannot create them.
- `main.bicep` and the other modules in `modules/`: one environment; the pipeline runs it on every deploy.
- `test.bicepparam` and `prod.bicepparam`: the parameters for each environment.
- `sql/grant-identities.sql`: run once as the Entra admin to add the app and deployment identities to the database.

## Bootstrap (run once, as Owner)

`bootstrap.bicep` creates both environments' resource groups and what goes in them before any deploy: the identities,
the role and policy assignments and the budgets. Run it from the root of a clone of this repository, in Bash (Git Bash
on Windows). The subscription and tenant IDs, the client IDs and the budget email address stay in the shell, in Azure
and in GitHub's environment secrets, and are never written into the repository (D42, D60, D65).

You need:
- the Azure CLI, signed in (`az login`) with an account that is Owner of the subscription, and its Bicep
  (`az bicep upgrade` installs or updates it);
- the GitHub CLI, signed in (`gh auth login`) as an admin of this repository.

1. Pick the subscription, and check its name and your account:

   ```shell
   az account set --subscription '<subscription name or ID>'
   az account show --query '{subscription: name, signedInAs: user.name}' --output table
   ```

2. Check the repository's OIDC subject prefix. `gh` fills in `{owner}` and `{repo}` from the clone:

   ```shell
   gh api repos/{owner}/{repo}/actions/oidc/customization/sub --jq .sub_claim_prefix
   ```

   It must print the default of `githubSubjectPrefix` in `bootstrap.bicep`. If it prints anything else, correct that
   default before going on, or the federated credentials will not match GitHub's tokens.

3. Preview the deployment. `read` asks for the budget alerts' address, so it is not saved in your shell history:

   ```shell
   read -rp 'Budget alert email address: ' budget_email
   az deployment sub what-if --name grow2notes-bootstrap --location australiasoutheast \
     --template-file infra/bootstrap.bicep --parameters budgetEmail="$budget_email"
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
     --template-file infra/bootstrap.bicep --parameters budgetEmail="$budget_email" \
     --query properties.provisioningState --output tsv
   ```

5. Show the four client IDs. When one is needed later, read it from the deployment again rather than saving it:

   ```shell
   az deployment sub show --name grow2notes-bootstrap --query properties.outputs.clientIds.value --output table
   ```

   Each environment's deployment identity client ID is that GitHub environment's `AZURE_CLIENT_ID`. Nothing needs the
   app identities' client IDs copied: `main.bicep` finds each app identity by its name (S00.02.02).

6. Create the GitHub `test` environment, and give it the three IDs as secrets. Their names are the ones `deploy.yml`
   passes to `azure/login`'s `client-id`, `tenant-id` and `subscription-id` (S00.02.03):

   ```shell
   # Only if the environment does not exist yet:
   gh api --method PUT repos/{owner}/{repo}/environments/test --silent

   client_id=$(az deployment sub show --name grow2notes-bootstrap \
     --query "properties.outputs.clientIds.value[?environment=='test'].deploymentIdentityClientId" --output tsv)
   gh secret set AZURE_CLIENT_ID --env test --body "$client_id"
   gh secret set AZURE_TENANT_ID --env test --body "$(az account show --query tenantId --output tsv)"
   gh secret set AZURE_SUBSCRIPTION_ID --env test --body "$(az account show --query id --output tsv)"
   gh secret list --env test
   gh secret list
   ```

   The environment should hold exactly those three secrets, and neither it nor the repository should hold a client
   secret or a publish profile. The `prod` environment gets the same three, with the prod row's client ID, in S00.02.04.

### Running it again

Deploys never need the bootstrap. Run it again only when `bootstrap.bicep` or its module changes, with steps 1, 3 and
4; the what-if should show only that change.

Also pass the budgets' start date, because Azure refuses to change it and the parameter's default is the first of the
current month. It is the first of the month of the first run, which the last run recorded:

```shell
budget_start_date=$(az deployment sub show --name grow2notes-bootstrap \
  --query properties.parameters.budgetStartDate.value --output tsv)
```

Then add `budgetStartDate="$budget_start_date"` after `budgetEmail="$budget_email"` in both commands.
