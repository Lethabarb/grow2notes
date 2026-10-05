// Run once by the developer as Owner of the subscription, before the first deploy (design.md §10.5). It holds the role
// and policy assignments, which the pipeline's identities cannot create; the database lock is in locks.bicep.
targetScope = 'subscription'

// This repository uses GitHub's immutable OIDC subjects, which carry the owner's and the repository's numeric IDs, so
// this is the prefix GitHub reports rather than plain owner/name. Both IDs are public.
@description('The sub_claim_prefix that gh api repos/<owner>/<repo>/actions/oidc/customization/sub returns, verbatim.')
param githubSubjectPrefix string = 'repo:Lethabarb@76515166/grow2notes@1403713365'

// Passed when the file is run and never committed, because it may contain the company's name (D42).
@description('The developer\'s email address for both budget alerts.')
param budgetEmail string

// Azure refuses a new budget that starts before the first of the current month, and refuses any change to the start
// date of an existing one. So the default suits the first run and any rerun in the same month, and a rerun in a later
// month must pass the date the budgets started. It has the form Azure stores, so what-if shows no change to it.
@description('The budgets\' start date, yyyy-MM-01T00:00:00Z. On a rerun in a later month, pass the existing one.')
param budgetStartDate string = '${utcNow('yyyy-MM')}-01T00:00:00Z'

// The two groups, and what this file creates in them, are in Australia Southeast (D38).
var location = 'australiasoutheast'

// Each group's monthly budget, AUD 100 in total (D63).
var environments = [
  {
    name: 'test'
    budgetAmount: 30
  }
  {
    name: 'prod'
    budgetAmount: 70
  }
]

resource resourceGroups 'Microsoft.Resources/resourceGroups@2025-04-01' = [
  for environment in environments: {
    name: 'rg-grow2notes-${environment.name}-ause'
    location: location
    tags: {
      app: 'grow2notes'
      env: environment.name
    }
  }
]

module environmentResources 'modules/bootstrap-environment.bicep' = [
  for (environment, i) in environments: {
    name: 'bootstrap-${environment.name}'
    scope: resourceGroups[i]
    params: {
      environmentName: environment.name
      githubSubjectPrefix: githubSubjectPrefix
      budgetAmount: environment.budgetAmount
      budgetEmail: budgetEmail
      budgetStartDate: budgetStartDate
    }
  }
]

// Client IDs are not secrets, but they belong to the operator's tenant, so the developer reads them from the deployment
// and they are never written into the repository (D60). This is one output looping over the environments, not one per
// identity: with each module scoped to resourceGroups[i], Bicep 0.44 compiles a reference to a single instance such as
// environmentResources[0] into a copyIndex() outside any loop, which Azure rejects.
@description('Per environment, the client IDs of its deployment identity (AZURE_CLIENT_ID in GitHub) and app identity.')
output clientIds array = [
  for (environment, i) in environments: {
    environment: environment.name
    deploymentIdentityClientId: environmentResources[i].outputs.deploymentIdentityClientId
    appIdentityClientId: environmentResources[i].outputs.appIdentityClientId
  }
]
