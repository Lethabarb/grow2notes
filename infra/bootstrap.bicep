// Run once by the developer as Owner of the subscription, before the first deploy (design.md §10.5). It holds the role
// and policy assignments, which the pipeline's identities cannot create; the database lock is in locks.bicep.
targetScope = 'subscription'

// The two groups, and what this file creates in them, are in Australia Southeast (D38).
var location = 'australiasoutheast'

var environments = [
  'test'
  'prod'
]

resource resourceGroups 'Microsoft.Resources/resourceGroups@2025-04-01' = [
  for environment in environments: {
    name: 'rg-grow2notes-${environment}-ause'
    location: location
    tags: {
      app: 'grow2notes'
      env: environment
    }
  }
]

module environmentResources 'modules/bootstrap-environment.bicep' = [
  for (environment, i) in environments: {
    name: 'bootstrap-${environment}'
    scope: resourceGroups[i]
  }
]
