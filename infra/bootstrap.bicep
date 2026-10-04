// Run once by the developer as Owner of the subscription, before the first deploy (design.md §10.5). It holds the role
// and policy assignments, which the pipeline's identities cannot create; the database lock is in locks.bicep.
targetScope = 'subscription'

// This repository uses GitHub's immutable OIDC subjects, which carry the owner's and the repository's numeric IDs, so
// this is the prefix GitHub reports rather than plain owner/name. Both IDs are public.
@description('The sub_claim_prefix that gh api repos/<owner>/<repo>/actions/oidc/customization/sub returns, verbatim.')
param githubSubjectPrefix string = 'repo:Lethabarb@76515166/grow2notes@1403713365'

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
    params: {
      environmentName: environment
      githubSubjectPrefix: githubSubjectPrefix
    }
  }
]
