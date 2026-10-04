// Everything bootstrap.bicep creates inside one environment's resource group. It is deployed at resource-group scope
// so that the policy assignment and the budget cover only that group, not the rest of the shared subscription (D61,
// D63).
targetScope = 'resourceGroup'
