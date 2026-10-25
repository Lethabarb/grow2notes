// The Key Vault, which holds one key and one secret (design.md §7.1, §9.3, §9.4, D92). The key is an RSA key, which
// wraps the Data Protection key ring that the app keeps in SQL. The secret is the Resend API key, resend-api-key, which
// the operator stores by hand and which no template holds or changes. The app identity's rights to wrap and unwrap and
// to read the secret come from the roles that bootstrap.bicep assigns on the resource group, so this file has no role
// assignment.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The region. Defaults to the resource group\'s, Australia Southeast (D38).')
param location string = resourceGroup().location

@description('The tags for the vault. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

@description('The VNet integration subnet, from network.bicep: the only network the vault accepts.')
param subnetId string

resource vault 'Microsoft.KeyVault/vaults@2026-02-01' = {
  name: 'kv-grow2notes-${environmentName}'
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    // Azure roles rather than access policies, so the app identity's role from bootstrap.bicep is its only access.
    enableRbacAuthorization: true
    // A deleted vault or key can be recovered for 90 days, the default and the maximum, and purge protection stops
    // anyone destroying it sooner. Azure fixes the retention when it creates the vault and never lets purge protection
    // be turned off; a vault deleted by mistake keeps its name for those 90 days, so it is recovered, not recreated.
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    // The app reaches the vault through the subnet's Microsoft.KeyVault service endpoint (design.md §7.7); every other
    // address is refused. ARM creates the key below through Key Vault's management plane, which the firewall does check:
    // with bypass 'None' the deploy fails with ForbiddenByFirewall (seen 7 Oct 2026), so trusted Azure services bypass.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: [
        {
          id: subnetId
        }
      ]
    }
  }
}

// ARM creates the key only if it does not exist yet: a redeploy never changes it or adds a version.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2026-02-01' = {
  parent: vault
  name: 'data-protection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

// Data Protection stores this URI in each key it wraps and unwraps with the key's current version, so the key must keep
// its one version: it has no rotation policy, and it must not be rotated by hand.
@description('The URI of the Data Protection wrapping key, without a version.')
output keyUri string = dataProtectionKey.properties.keyUri

// The secret itself is not declared here, so a deploy never creates, changes or reads it. The app reads its current
// version at each send, so a rotated key needs no deploy.
@description('The URI of the Resend API key secret, without a version, which the app reads as Email:ApiKeySecretUri.')
output emailApiKeySecretUri string = '${vault.properties.vaultUri}secrets/resend-api-key'
