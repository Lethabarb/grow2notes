# infra

Bicep for the Azure resources (design.md §10.1, §10.5).

- `bootstrap.bicep` and `locks.bicep`: run by the developer as subscription Owner. They hold every
  `Microsoft.Authorization` resource (role assignments, policy, locks), because the pipeline's identity cannot create
  them.
- `main.bicep` and `modules/`: one environment; the pipeline runs it on every deploy.
- `test.bicepparam` and `prod.bicepparam`: the parameters for each environment.
- `sql/grant-identities.sql`: run once as the Entra admin to add the app and deployment identities to the database.
