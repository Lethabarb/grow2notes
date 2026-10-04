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

// Wrap and unwrap only, with the vault's one key, which protects the Data Protection key ring (design.md §9.3, §9.4).
var keyVaultCryptoServiceEncryptionUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'e147488a-f6f5-4113-8e2d-b22465e65bf6'
)

// Telemetry ingestion, because local auth is off for it (design.md §9.5).
var monitoringMetricsPublisherRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '3913510d-42f4-4e42-8a64-420c390055eb'
)

// Sending email with Entra ID needs read and write on the Communication Services resource. This is the only built-in
// Communication Services role; Contributor, the other built-in role Microsoft names for it, reaches every resource
// type. This role also lets the app list keys and delete the group's Communication and Email resources, which it
// never does.
var communicationAndEmailServiceOwnerRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '09976791-48a7-449e-bb21-39d1a415f350'
)

// The web app runs as this identity for SQL, Key Vault, telemetry and email (design.md §9.4). Its database access is a
// database user that grant-identities.sql adds, not an Azure role.
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-grow2notes-${environmentName}-app'
  location: resourceGroup().location
  tags: resourceGroup().tags
}

// Scoped to the group, because the vault, Application Insights and Communication Services are created later by
// main.bicep, whose identity cannot assign roles (design.md §10.5).
resource appIdentityRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleDefinitionId in [
    keyVaultCryptoServiceEncryptionUserRoleDefinitionId
    monitoringMetricsPublisherRoleDefinitionId
    communicationAndEmailServiceOwnerRoleDefinitionId
  ]: {
    name: guid(resourceGroup().id, appIdentity.id, roleDefinitionId)
    properties: {
      roleDefinitionId: roleDefinitionId
      principalId: appIdentity.properties.principalId
      principalType: 'ServicePrincipal'
    }
  }
]

output deploymentIdentityClientId string = deploymentIdentity.properties.clientId
output deploymentIdentityPrincipalId string = deploymentIdentity.properties.principalId
output appIdentityClientId string = appIdentity.properties.clientId
output appIdentityPrincipalId string = appIdentity.properties.principalId
