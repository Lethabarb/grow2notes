// Everything bootstrap.bicep creates inside one environment's resource group. It is deployed at resource-group scope
// so that the policy assignment and the budget cover only that group, not the rest of the shared subscription (D61,
// D63).
targetScope = 'resourceGroup'

@description('The environment, test or prod. It is also the name of the GitHub environment whose jobs may deploy here.')
param environmentName string

@description('The OIDC subject prefix of the repository, repo:<owner>@<owner id>/<repo>@<repo id>.')
param githubSubjectPrefix string

// Built-in Contributor: enough to deploy main.bicep into the group, but it cannot create role assignments or locks,
// which is why those live in this file and locks.bicep (design.md §10.5).
var contributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'b24988ac-6180-42a0-ab88-20f7382dd24c'
)

// The pipeline signs in as this identity with OIDC, so GitHub holds only IDs and no credential (design.md §9.4, D65).
resource deploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-grow2notes-${environmentName}-deploy'
  location: resourceGroup().location
  tags: resourceGroup().tags
}

// GitHub issues this subject only to jobs that run in this environment of this repository, so a job on another
// repository, or one without the environment, cannot sign in. Entra matches it as an exact string, and the IDs in the
// prefix mean a repository later created under the same name does not match either.
resource githubFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: deploymentIdentity
  name: 'github-${environmentName}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: '${githubSubjectPrefix}:environment:${environmentName}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource deploymentIdentityContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deploymentIdentity.id, contributorRoleDefinitionId)
  properties: {
    roleDefinitionId: contributorRoleDefinitionId
    principalId: deploymentIdentity.properties.principalId
    // Naming the type skips Entra ID's principal lookup, which can fail for an identity created moments earlier.
    principalType: 'ServicePrincipal'
  }
}

output deploymentIdentityClientId string = deploymentIdentity.properties.clientId
output deploymentIdentityPrincipalId string = deploymentIdentity.properties.principalId
