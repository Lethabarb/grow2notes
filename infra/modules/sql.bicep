// The Azure SQL logical server and the app's database (design.md §9.3, §9.4, §10.1). The server takes only Microsoft
// Entra sign-ins, and only from the web app's subnet, apart from the temporary firewall rule each deploy adds for its
// migration and then removes (design.md §10.4).
targetScope = 'resourceGroup'

@description('The environment, test or prod.')
param environmentName string

@description('The region. Defaults to the resource group\'s, Australia Southeast (D38).')
param location string = resourceGroup().location

@description('The tags for the server and the database. Defaults to the resource group\'s.')
param tags object = resourceGroup().tags

@description('The VNet integration subnet, from network.bicep: the only network the server accepts.')
param subnetId string

// Both are passed in by every deploy and never committed, because the object ID belongs to the operator's tenant
// (D60, D64, D65). The parameters files hold empty placeholders, so a deploy that forgets them fails on these lengths
// before the server is touched. They are checked here rather than in main.bicep, because there Bicep would refuse to
// build the parameters files' placeholders.
@description('The display name of the Entra group that is the server\'s admin, "Grow2Notes SQL admins" (D64).')
@minLength(1)
param sqlAdminGroupName string

@description('The object ID of that Entra group, a GUID.')
@minLength(36)
@maxLength(36)
param sqlAdminGroupObjectId string

// Written the way Azure returns it (name GP_S_Gen5 with capacity 2, not GP_S_Gen5_2), so what-if shows no change.
@description('The database SKU: name, tier, family and capacity.')
param databaseSku resourceInput<'Microsoft.Sql/servers/databases@2025-01-01'>.sku

@description('The database\'s maximum size, in bytes.')
param databaseMaxSizeBytes int

// The free offer is what keeps test's database at no cost (design.md §10.2, §10.3). It needs a General Purpose
// serverless SKU of at most 4 vCores, at most 32 GB, local backup storage and at most 7 days of point-in-time
// restore.
@description('Whether the database uses the Azure SQL free offer, pausing until next month when it is used up.')
param useFreeOffer bool

@description('Where backups are kept: Local (LRS) in test, Geo (copied to Australia East) in prod (design.md §10.7).')
param backupStorageRedundancy 'Local' | 'Geo'

@description('How many days back point-in-time restore reaches: 7 in test, 35 in prod (design.md §10.3, §10.7).')
param pointInTimeRestoreDays int

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: 'sql-grow2notes-${environmentName}'
  location: location
  tags: tags
  properties: {
    // Entra-only, so the server has no SQL admin login or password, and nothing can sign in with one (design.md §9.4).
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminGroupName
      sid: sqlAdminGroupObjectId
      tenantId: tenant().tenantId
      principalType: 'Group'
    }
    // A minimum, so clients that support TLS 1.3 still get it; the app also connects with Encrypt=Strict (§9.3).
    minimalTlsVersion: '1.2'
    // The VNet rule and the deploy's temporary firewall rule both apply to the public endpoint, so it stays enabled.
    // There is deliberately no rule allowing all Azure services, which would admit other customers' resources too.
    publicNetworkAccess: 'Enabled'
  }
}

// Azure applies azureADOnlyAuthentication in the administrators block only when it creates the server, so this is what
// turns Entra-only authentication back on at the next deploy if it is ever switched off.
resource entraOnlyAuthentication 'Microsoft.Sql/servers/azureADOnlyAuthentications@2025-01-01' = {
  parent: server
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
}

// The web app's traffic arrives through the subnet's Microsoft.Sql service endpoint (design.md §7.7).
resource appSubnetRule 'Microsoft.Sql/servers/virtualNetworkRules@2025-01-01' = {
  parent: server
  name: 'app-subnet'
  properties: {
    virtualNetworkSubnetId: subnetId
  }
}

// The name has no environment, because the server's name already has one (design.md §10.1, §10.7).
resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: server
  name: 'sqldb-grow2notes'
  location: location
  tags: tags
  sku: databaseSku
  // No autoPauseDelay: a free-offer database that pauses when its free amount runs out accepts only the default, and a
  // DTU database such as S0 never pauses.
  properties: {
    maxSizeBytes: databaseMaxSizeBytes
    useFreeLimit: useFreeOffer
    // Pause rather than bill, so test stays free. Azure cannot switch a database back from billing to pausing.
    freeLimitExhaustionBehavior: useFreeOffer ? 'AutoPause' : null
    requestedBackupStorageRedundancy: backupStorageRedundancy
  }
}

resource pointInTimeRestore 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2025-01-01' = {
  parent: database
  name: 'default'
  properties: {
    retentionDays: pointInTimeRestoreDays
  }
}

@description('The server\'s host name, <name>.database.windows.net, for the connection string and the migration.')
output serverFullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName

@description('The database\'s name.')
output databaseName string = database.name
