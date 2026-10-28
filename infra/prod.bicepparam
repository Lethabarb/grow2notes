// The production environment: what differs from test is in design.md §10.3.
using 'main.bicep'

param environmentName = 'prod'

// Readiness, which includes a database check: App Service takes an instance out of rotation when the database cannot
// be reached, and the health-check alert watches it (design.md §10.8). Prod's database is never paused.
param healthCheckPath = '/healthz/ready'

// The web app's default address, which App Service's own certificate covers (D91). It is also the passkey domain, so
// it must not change after the first person enrols (A101).
param appOrigin = 'https://app-grow2notes-prod.azurewebsites.net'

// A DTU Standard S0 database (10 DTUs) with 250 GB of storage, written the way Azure returns it: name Standard, tier
// Standard and capacity 10. Backups are kept in Geo-redundant storage, copied to Australia East, with 35 days of
// point-in-time restore; sql.bicep adds a monthly long-term backup kept for 12 months (design.md §10.1, §10.7).
param databaseSku = {
  name: 'Standard'
  tier: 'Standard'
  capacity: 10
}
param databaseMaxSizeBytes = 268435456000
param useFreeOffer = false
param backupStorageRedundancy = 'Geo'
param pointInTimeRestoreDays = 35

// Placeholders only. Every deploy passes the real values as parameters, from the prod environment's secrets, because
// the object ID belongs to the operator's tenant and the addresses may contain the company's name (D42, D60, D64, D65);
// sql.bicep and monitoring.bicep refuse the first three empty, and an empty sender address makes the app warn and
// answer every email Not sent (design.md §10.4 step 2). The Resend key is not here either: deploy.yml passes it as a
// secure parameter.
param sqlAdminGroupName = ''
param sqlAdminGroupObjectId = ''
param alertEmail = ''
param emailSenderAddress = ''
