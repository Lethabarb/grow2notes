// One environment's resources (design.md §10.1, §10.3, §10.5). The pipeline runs this file on every deploy as the
// environment's deployment identity, whose Contributor role cannot create role assignments or locks, so it has no
// Microsoft.Authorization resources: those are in bootstrap.bicep and locks.bicep.
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName 'test' | 'prod'

@description('The path App Service probes: /healthz in test, /healthz/ready in prod (design.md §10.3, §10.8).')
param healthCheckPath '/healthz' | '/healthz/ready'

@description('The address the app is reached at, https://<host>, which its links start with (design.md §8.1).')
param appOrigin string

// Every deploy passes these four from the environment's secrets, and they are never committed, because the object ID
// belongs to the operator's tenant and the addresses may contain the company's name (D42, D60, D64, D65). The
// parameters files leave them empty; sql.bicep and monitoring.bicep refuse the first three empty. An unset sender
// address is not passed, and the app then warns at start and answers every email Not sent (design.md §10.4).
@description('The display name of the Entra group that is the SQL server\'s admin, "Grow2Notes SQL admins" (D64).')
param sqlAdminGroupName string

@description('The object ID of that Entra group.')
param sqlAdminGroupObjectId string

@description('The developer\'s email address, which the operations alerts are sent to (design.md §10.1).')
param alertEmail string

@description('The address the app sends its setup-link email from, a bare address or "Grow2Notes <address>" (D92).')
param emailSenderAddress string

// The Resend API key, which each deploy writes to Key Vault when it is not empty (D93). It is secure so that ARM keeps
// the value out of the deployment's history, and it is in no parameters file, setting or output.
@description('The Resend API key, from the environment\'s GitHub secret. Empty leaves the vault\'s secret as it is.')
@secure()
param resendApiKey string = ''

@description('The database SKU, written the way Azure returns it: name, tier, family and capacity.')
param databaseSku resourceInput<'Microsoft.Sql/servers/databases@2025-01-01'>.sku

@description('The database\'s maximum size, in bytes.')
param databaseMaxSizeBytes int

@description('Whether the database uses the Azure SQL free offer, as test does (design.md §10.3).')
param useFreeOffer bool

@description('Where backups are kept: Local (LRS) in test, Geo (copied to Australia East) in prod (design.md §10.7).')
param backupStorageRedundancy 'Local' | 'Geo'

@description('How many days back point-in-time restore reaches: 7 in test, 35 in prod (design.md §10.3, §10.7).')
param pointInTimeRestoreDays int

@description('Whether AI-assistant access for managers and admins is on: on in test, off in prod (mcp-server.md).')
param mcpEnabled bool = false

// bootstrap.bicep creates the identity and its roles; this file only reads it, by the name bootstrap.bicep gives it, so
// no client ID has to be copied into the repository (D60).
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: 'id-grow2notes-${environmentName}-app'
}

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    environmentName: environmentName
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    environmentName: environmentName
    subnetId: network.outputs.subnetId
    sqlAdminGroupName: sqlAdminGroupName
    sqlAdminGroupObjectId: sqlAdminGroupObjectId
    databaseSku: databaseSku
    databaseMaxSizeBytes: databaseMaxSizeBytes
    useFreeOffer: useFreeOffer
    backupStorageRedundancy: backupStorageRedundancy
    pointInTimeRestoreDays: pointInTimeRestoreDays
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    alertEmail: alertEmail
    databaseId: sql.outputs.databaseId
    useFreeOffer: useFreeOffer
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    environmentName: environmentName
    subnetId: network.outputs.subnetId
    resendApiKey: resendApiKey
  }
}

// SqlClient signs in as the app identity, so the string holds no secret (design.md §9.4). Encrypt=Strict is TDS 8.0:
// TLS starts before any TDS message and the server's certificate is always validated, which TrustServerCertificate
// cannot turn off, so that keyword is left out (design.md §9.3).
var connectionString = join(
  [
    'Server=tcp:${sql.outputs.serverFullyQualifiedDomainName},1433'
    'Database=${sql.outputs.databaseName}'
    'Authentication=Active Directory Managed Identity'
    'User Id=${appIdentity.properties.clientId}'
    'Encrypt=Strict'
  ],
  ';'
)

module app 'modules/app.bicep' = {
  name: 'app'
  params: {
    environmentName: environmentName
    appIdentityId: appIdentity.id
    subnetId: network.outputs.subnetId
    healthCheckPath: healthCheckPath
    // Written by app.bicep as Email:SenderAddress, the address on every email the app sends. Empty is written as it is
    // and counts as missing.
    emailSenderAddress: emailSenderAddress
    // The app reads these as ConnectionStrings:Grow2Notes, ManagedIdentity:ClientId and DataProtection:KeyVaultKeyUri.
    // The client ID picks the user-assigned identity for ManagedIdentityCredential, which signs in to Key Vault,
    // telemetry and the email key (design.md §9.4). The key URI has no version: Data Protection wraps each new key
    // with the key's current version (design.md §9.3). APPLICATIONINSIGHTS_CONNECTION_STRING is the name the Azure
    // Monitor OpenTelemetry distro reads; the string only says where to send, and the identity's token is what lets
    // the app send (design.md §9.5). Statsbeat, the exporter's own usage reports, would go to Microsoft outside
    // Australia, so it is off (design.md §10, D33).
    appSettings: {
      ConnectionStrings__Grow2Notes: connectionString
      ManagedIdentity__ClientId: appIdentity.properties.clientId
      DataProtection__KeyVaultKeyUri: keyVault.outputs.keyUri
      APPLICATIONINSIGHTS_CONNECTION_STRING: monitoring.outputs.applicationInsightsConnectionString
      APPLICATIONINSIGHTS_STATSBEAT_DISABLED: 'true'
      // Read as App:Origin. An invite's setup link starts with it, because the operator commands, which print one,
      // run where there is no request to take the address from (design.md §7.4, §8.1).
      App__Origin: appOrigin
      // Read as Mcp:Enabled. Off, the app answers 404 at every path of the AI-assistant access and registers no
      // authorisation server; the default is off, and only a parameter file turns it on (mcp-server.md §3.5).
      Mcp__Enabled: string(mcpEnabled)
      // Read as Email:ApiKeySecretUri. It is where the app finds the Resend API key it sends its setup-link email with:
      // it reads the key from Key Vault as the app identity at each send, so the key is in no setting. Every deploy
      // sets it, so an app in Azure never falls back to writing its emails, links and all, to its console (D92, D93).
      Email__ApiKeySecretUri: keyVault.outputs.emailApiKeySecretUri
      // App Service mounts the deployed zip read-only as wwwroot, so a running build never loads a later build's files,
      // and sends traffic to a newly started build only once /healthz answers 200 (design.md §10.4 step 7, A49).
      // /healthz in both environments, not prod's /healthz/ready, because it never touches the database (design.md
      // §10.8): a database outage cannot hold back a start.
      WEBSITE_RUN_FROM_PACKAGE: '1'
      WEBSITE_WARMUP_PATH: '/healthz'
      WEBSITE_WARMUP_STATUSES: '200'
    }
  }
}

// The production alerts, which are kept out of test (design.md §10.1, S06.04.01). The action group and test's own
// alert are in monitoring.bicep, which the web app's settings depend on.
module alerts 'modules/alerts.bicep' = if (environmentName == 'prod') {
  name: 'alerts'
  params: {
    environmentName: environmentName
    actionGroupId: monitoring.outputs.actionGroupId
    webAppId: app.outputs.webAppId
    databaseId: sql.outputs.databaseId
  }
}

@description('The web app\'s name, which the deploy step targets.')
output webAppName string = app.outputs.webAppName

@description('The web app\'s default host name, <name>.azurewebsites.net, where it is reached and smoke-tested.')
output defaultHostName string = app.outputs.defaultHostName

@description('The SQL server\'s host name, which the migration and grant-identities.sql connect to.')
output sqlServerFullyQualifiedDomainName string = sql.outputs.serverFullyQualifiedDomainName
